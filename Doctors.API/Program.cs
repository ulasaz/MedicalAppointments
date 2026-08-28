using System.Text;
using System.Text.Json.Serialization;
using MassTransit;
using Doctors.Database;
using Doctors.Helpers;
using Doctors.Interfaces;
using Doctors.Services;
using Finbuckle.MultiTenant.Abstractions;
using Finbuckle.MultiTenant.AspNetCore.Extensions;
using Finbuckle.MultiTenant.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

var supportedCultures = new[] { "en", "pl" };

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.SetDefaultCulture("en")
        .AddSupportedCultures(supportedCultures)
        .AddSupportedUICultures(supportedCultures);
});

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

// Tenant is resolved from the JWT's "tenant_id" claim when the caller is authenticated
// (patients/doctors browsing while logged in), falling back to the X-Tenant-Id header
// for anonymous doctor search on a specific medical center's public page. Both strategies
// and the header itself are trusted as-is (EchoStore) because the identifier only ever
// reaches here already validated: Identity.API only mints the claim for a real, active
// MedicalCenter, and the header is set by our own frontend from that same directory.
builder.Services.AddMultiTenant<TenantInfo>()
    .WithClaimStrategy("tenant_id")
    .WithHeaderStrategy("X-Tenant-Id")
    .WithEchoStore();

var conString = builder.Configuration.GetConnectionString("DefaultConnection") ??
                throw new InvalidOperationException("Connection string not found.");
builder.Services.AddDbContext<DatabaseContext>(options =>
    options.UseNpgsql(conString));

builder.Services.AddScoped<IDoctorService, DoctorService>();
builder.Services.AddScoped<IMedicalServiceService, MedicalServiceService>();

builder.Services.AddTransient<TenantPropagationHandler>();
builder.Services.AddHttpClient<IAppointmentsClient, AppointmentsClient>(client =>
        client.BaseAddress = new Uri(builder.Configuration["ServiceUrls:AppointmentsApi"] ?? "http://localhost:5003"))
    .AddHttpMessageHandler<TenantPropagationHandler>();

var jwtSecret = builder.Configuration["JwtSettings:Secret"]
    ?? "MySuperSecretKeyThatIsVeryLongAndSecureForCuraSlotSystem";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddHealthChecks();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen();

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
// at all — e.g. an anonymous search with no X-Tenant-Id header, or the platform
// super-admin's token (which deliberately carries no tenant_id claim). Turn that crash
// into a clear 400 instead of a 500.
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? string.Empty;
    if (path.StartsWith("/health") || path.StartsWith("/swagger"))
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