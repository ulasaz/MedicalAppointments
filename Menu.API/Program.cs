using System.Text;
using Finbuckle.MultiTenant.Abstractions;
using Finbuckle.MultiTenant.AspNetCore.Extensions;
using Finbuckle.MultiTenant.Extensions;
using LunchBoards.Scheduler.Parsers;
using MassTransit;
using Menu.API.Parsers;
using Menu.Database;
using Menu.Interfaces;
using Menu.Processing;
using Menu.Scrappers;
using Menu.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using BarLodziakMenuParserEasyOcr = Menu.Parsers.BarLodziakMenuParserEasyOcr;
using FacebookMenuParser = Menu.Parsers.FacebookMenuParser;

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
var conString = builder.Configuration.GetConnectionString("DefaultConnection") ??
                throw new InvalidOperationException("Connection string not found.");
builder.Services.AddDbContext<DatabaseContext>(options =>
    options.UseNpgsql(conString));

builder.Services.AddScoped<IMenuService, MenuService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IImageDownloader, GoogleDriveImageDownloader>();
builder.Services.AddScoped<IOcrProcessor, AzureOcrProcessor>();
builder.Services.AddScoped<GoogleDriveScraper>();
builder.Services.AddScoped<FacebookScraper>();
builder.Services.AddScoped<IScraper, ScraperDispatcher>();
builder.Services.AddScoped<IMenuParser, BarLodziakMenuParserEasyOcr>();
builder.Services.AddScoped<IMenuParser, FacebookMenuParser>();

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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("MySuperSecretKeyThatIsVeryLongAndSecureForLunchOrderingSystem"))
        };
    });
builder.Services.AddControllers();

builder.Services.AddHealthChecks();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen();

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
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred during migrations");
    }
}

app.UseMultiTenant();
app.UseCors("AllowAll");
app.UseRequestLocalization();

app.MapHealthChecks("/health");
app.MapControllers();

app.UseSwagger();
app.UseSwaggerUI();

app.Run();