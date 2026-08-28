using System.Text;
using System.Text.Json.Serialization;
using Appointments.Database;
using Appointments.Helpers;
using Appointments.Interfaces;
using Appointments.Services;
using Finbuckle.MultiTenant.Abstractions;
using Finbuckle.MultiTenant.AspNetCore.Extensions;
using Finbuckle.MultiTenant.Extensions;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Stripe;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

var supportedCultures = new[] { "en", "pl" };

StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"];
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.SetDefaultCulture("en")
        .AddSupportedCultures(supportedCultures)
        .AddSupportedUICultures(supportedCultures);
});

// Same trust model as Doctors.API: claim strategy for authenticated calls, header
// strategy as an anonymous fallback, EchoStore because the identifier is already
// validated upstream by Identity.API before it ever reaches a claim or header here.
builder.Services.AddMultiTenant<TenantInfo>()
    .WithClaimStrategy("tenant_id")
    .WithHeaderStrategy("X-Tenant-Id")
    .WithEchoStore();


builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMq:Host"], h =>
        {
            h.Username(builder.Configuration["RabbitMq:Username"] ?? "guest");
            h.Password(builder.Configuration["RabbitMq:Password"] ?? "guest");
        });
    });
});


builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<TenantPropagationHandler>();
builder.Services.AddHttpClient<IDoctorsClient, DoctorsClient>(client =>
        client.BaseAddress = new Uri(builder.Configuration["ServiceUrls:DoctorsApi"] ?? "http://localhost:5002"))
    .AddHttpMessageHandler<TenantPropagationHandler>();
builder.Services.AddHttpClient<IIdentityClient, IdentityClient>(client =>
    client.BaseAddress = new Uri(builder.Configuration["ServiceUrls:IdentityApi"] ?? "http://localhost:5001"));

var conString = builder.Configuration.GetConnectionString("DefaultConnection") ??
                throw new InvalidOperationException("Connection string not found.");
builder.Services.AddDbContext<DatabaseContext>(options =>
    options.UseNpgsql(conString));

builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<IAppointmentService, AppointmentService>();

builder.Services.AddSwaggerGen();

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});;

builder.Services.AddHealthChecks();

builder.Services.AddEndpointsApiExplorer();

var jwtSecret = builder.Configuration["JwtSettings:Secret"]
    ?? "MySuperSecretKeyThatIsVeryLongAndSecureForCuraSlotSystem";

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "CuraSlot.Identity",
            ValidAudience = "CuraSlot.Services",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<DatabaseContext>();
        await context.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred during migrations");
    }
}

app.UseRequestLocalization();

app.UseAuthentication();

// Must run after UseAuthentication(): the claim strategy reads HttpContext.User,
// which isn't populated yet earlier in the pipeline.
app.UseMultiTenant();

// Every entity here is [MultiTenant]-scoped, and Finbuckle's query filter throws a
// NullReferenceException (rather than filtering to zero rows) when no tenant resolved
// at all — e.g. the platform super-admin's token (deliberately carries no tenant_id
// claim) with no X-Tenant-Id header either. Turn that crash into a clear 400 instead of
// a 500. The Stripe webhook is exempt: Stripe calls it with no auth and no knowledge of
// our tenants at all, so it resolves its own tenant context manually (see PaymentsController).
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? string.Empty;
    if (path.StartsWith("/health") || path.StartsWith("/swagger") || path.StartsWith("/api/payments/webhook"))
    {
        await next();
        return;
    }

    var tenantContextAccessor = context.RequestServices.GetRequiredService<IMultiTenantContextAccessor<TenantInfo>>();
    if (tenantContextAccessor.MultiTenantContext?.TenantInfo == null)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new { message = "A medical center context is required (sign in, or send X-Tenant-Id)." });
        return;
    }

    await next();
});

app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllers();

app.UseSwagger();
app.UseSwaggerUI();

app.Run();