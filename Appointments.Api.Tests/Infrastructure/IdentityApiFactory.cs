using Identity.Database;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Appointments.Api.Tests.Infrastructure;

public class IdentityApiFactory : WebApplicationFactory<Identity.Controllers.AuthController>
{
    public IdentityApiFactory(string connectionString)
    {
        // Identity.API's Program.cs reads the connection string from IConfiguration with
        // top-level statements, synchronously, BEFORE builder.Build() runs. WebApplicationFactory's
        // ConfigureWebHost -> ConfigureAppConfiguration callback only gets merged in DURING that
        // deferred Build() call, so it arrives too late to affect a value already read out of
        // config — the override was silently a no-op and every test run was hitting the real
        // dev database (appsettings.json's "127.0.0.1:5432" default). Environment variables, by
        // contrast, are read by WebApplication.CreateBuilder() itself, so setting them here in the
        // constructor — before the factory's host is ever built — actually takes effect. This
        // mirrors exactly how the real docker-compose deployment overrides the same settings.
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", connectionString);
        Environment.SetEnvironmentVariable("JwtSettings__Secret", "MySuperSecretKeyThatIsVeryLongAndSecureForCuraSlotSystem");
        Environment.SetEnvironmentVariable("Jwt__ExpirationInMinutes", "60");
    }

    public Task InitializeDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        db.Database.EnsureCreated();
        return Task.CompletedTask;
    }
}
