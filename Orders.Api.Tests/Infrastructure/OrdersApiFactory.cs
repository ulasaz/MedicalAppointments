using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orders.Database;
using Orders.Helpers;

namespace Orders.Api.Tests.Infrastructure;

public class OrdersApiFactory : WebApplicationFactory<Orders.Controllers.OrdersController>
{
    private readonly string _connectionString;
    private readonly string _rabbitMqHost;
    private readonly string _rabbitMqUsername;
    private readonly string _rabbitMqPassword;
    private readonly MenuApiFactory _menuApiFactory;

    public OrdersApiFactory(
        string connectionString,
        string rabbitMqHost,
        string rabbitMqUsername,
        string rabbitMqPassword,
        MenuApiFactory menuApiFactory)
    {
        _connectionString = connectionString;
        _rabbitMqHost = rabbitMqHost;
        _rabbitMqUsername = rabbitMqUsername;
        _rabbitMqPassword = rabbitMqPassword;
        _menuApiFactory = menuApiFactory;
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

        builder.ConfigureServices(services =>
        {
            // Replace MenuClient's HttpClient to point to the in-process Menu.API test server
            var menuHttpClient = _menuApiFactory.CreateDefaultClient();
            services.AddSingleton<MenuClient>(_ => new MenuClient(menuHttpClient));
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
