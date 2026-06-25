using System.Text;
using Finbuckle.MultiTenant.Abstractions;
using Finbuckle.MultiTenant.AspNetCore.Extensions;
using Finbuckle.MultiTenant.Extensions;
using Identity.Database;
using Identity.Helpers;
using Identity.Interfaces;
using Identity.Models;
using Identity.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Volo.Abp.Data;
using DataSeeder = Identity.Helpers.DataSeeder;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

var supportedCultures = new[] { "en", "pl" };

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.SetDefaultCulture("en")
        .AddSupportedCultures(supportedCultures)
        .AddSupportedUICultures(supportedCultures);
});

builder.Services.AddMultiTenant<TenantInfo>()
    .WithHeaderStrategy("X-Tenant-Id")
    .WithConfigurationStore();

var conString = builder.Configuration.GetConnectionString("DefaultConnection") ??
                throw new InvalidOperationException("Connection string not found.");

builder.Services.AddDbContext<DatabaseContext>((serviceProvider, options) =>
{
    options.UseNpgsql(conString)
           .EnableSensitiveDataLogging();
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<IPasswordHelper, PasswordHelper>();
builder.Services.AddScoped<IIdentityService, IdentityService>();
builder.Services.AddScoped<ITokenService, TokenService>();

builder.Services.AddControllers();

builder.Services.AddHealthChecks();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen();

var jwtSecret = builder.Configuration["JwtSettings:Secret"]
    ?? "MySuperSecretKeyThatIsVeryLongAndSecureForLunchOrderingSystem";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "LunchOrdering.Identity",
            ValidAudience = "LunchOrdering.Services",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.WithOrigins(
                "http://localhost:4200",
                "http://cafe2.localhost:4200",
                "http://cafe1.localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<DatabaseContext>();
        
        await context.Database.MigrateAsync(); 
        
        Console.WriteLine("Database was created");
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error during migrations");
    }
}

var seeder = new DataSeeder(app.Services);

await seeder.SeedAsync(("cafe1"));
await seeder.SeedAsync(("cafe2"));


app.UseMultiTenant();
app.UseCors("AllowAll");
app.UseRequestLocalization();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllers();

app.UseSwagger();
app.UseSwaggerUI();

app.Run();