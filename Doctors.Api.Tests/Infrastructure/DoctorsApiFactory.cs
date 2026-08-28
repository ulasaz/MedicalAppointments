using Doctors.Database;
using Doctors.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Doctors.Api.Tests.Infrastructure;

public class DoctorsApiFactory : WebApplicationFactory<Doctors.Controllers.DoctorsController>
{
    public FakeAppointmentsClient AppointmentsClient { get; } = new();

    public DoctorsApiFactory(string connectionString)
    {
        // Doctors.API's Program.cs reads the connection string from IConfiguration with top-level
        // statements, synchronously, BEFORE builder.Build() runs. WebApplicationFactory's
        // ConfigureWebHost -> ConfigureAppConfiguration callback only gets merged in DURING that
        // deferred Build() call, so it arrives too late to affect a value already read out of
        // config — an override there is silently a no-op and the test would hit the real dev
        // database (appsettings.json's "127.0.0.1:5432" default) instead of this Testcontainers
        // instance. Environment variables, by contrast, are read by WebApplication.CreateBuilder()
        // itself, so setting them here in the constructor — before the factory's host is ever
        // built — actually takes effect. This mirrors exactly how the real docker-compose
        // deployment overrides the same settings.
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", connectionString);
        // Points MassTransit's RabbitMQ transport at a host that resolves but isn't listening —
        // the bus connects lazily/asynchronously in the background and retries on failure, so
        // this doesn't block the test server from serving HTTP requests (Doctors.API itself
        // never publishes or consumes any messages).
        Environment.SetEnvironmentVariable("RabbitMq__Host", "localhost");
        Environment.SetEnvironmentVariable("RabbitMq__Username", "guest");
        Environment.SetEnvironmentVariable("RabbitMq__Password", "guest");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            // Replace the real HttpClient-backed IAppointmentsClient (which would try to reach
            // a live Appointments.API) with a fake so rating lookups are test-controlled. This
            // part is unaffected by the config-timing issue above — DI overrides apply correctly
            // regardless of when Program.cs reads IConfiguration.
            services.RemoveAll<IAppointmentsClient>();
            services.AddSingleton<IAppointmentsClient>(AppointmentsClient);
        });
    }

    public Task InitializeDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        db.Database.EnsureCreated();
        return Task.CompletedTask;
    }

    // Every entity behind this API is tenant-scoped, and Program.cs now rejects any request
    // (with a 400) that resolves no tenant at all. Authenticated test requests already carry
    // their own tenant_id claim via TestJwtTokenFactory, but anonymous-endpoint tests (search,
    // GET photo, etc.) have nothing else to resolve from — so this client carries the default
    // tenant header, matching how the real frontend always has a selected medical center
    // before it ever calls one of those endpoints. A claim-bearing JWT on individual requests
    // still wins over this header (Finbuckle tries the claim strategy first).
    public HttpClient CreateClientWithDefaultTenant()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TestJwtTokenFactory.DefaultTenantId);
        return client;
    }
}
