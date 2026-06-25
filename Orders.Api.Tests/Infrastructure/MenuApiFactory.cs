using Menu.Database;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Orders.Api.Tests.Infrastructure;

public class MenuApiFactory : WebApplicationFactory<Menu.Controllers.MenuController>
{
    private readonly string _connectionString;
    private readonly string _rabbitMqHost;
    private readonly string _rabbitMqUsername;
    private readonly string _rabbitMqPassword;

    public MenuApiFactory(string connectionString, string rabbitMqHost, string rabbitMqUsername, string rabbitMqPassword)
    {
        _connectionString = connectionString;
        _rabbitMqHost = rabbitMqHost;
        _rabbitMqUsername = rabbitMqUsername;
        _rabbitMqPassword = rabbitMqPassword;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _connectionString,
                ["RabbitMq:Host"] = _rabbitMqHost,
                ["RabbitMq:Username"] = _rabbitMqUsername,
                ["RabbitMq:Password"] = _rabbitMqPassword
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
