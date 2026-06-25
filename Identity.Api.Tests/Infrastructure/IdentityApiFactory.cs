using Identity.Database;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Api.Tests.Infrastructure;

public class IdentityApiFactory : WebApplicationFactory<Identity.Controllers.AuthController>
{
    private readonly string _connectionString;

    public IdentityApiFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _connectionString,
                ["JwtSettings:Secret"] = "MySuperSecretKeyThatIsVeryLongAndSecureForLunchOrderingSystem",
                ["Jwt:ExpirationInMinutes"] = "60"
            });
        });
    }

    public Task InitializeDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        db.Database.EnsureCreated();
        return Task.CompletedTask;
    }
}
