using Npgsql;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace Orders.Api.Tests.Infrastructure;

public class LunchOrderingFixture : IAsyncLifetime
{
    // Non-guest credentials to avoid RabbitMQ's loopback-only restriction for 'guest'
    private const string RabbitUser = "testadmin";
    private const string RabbitPass = "testpass";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder()
        .WithImage("rabbitmq:3.13-alpine")
        .WithUsername(RabbitUser)
        .WithPassword(RabbitPass)
        .Build();

    public IdentityApiFactory IdentityFactory { get; private set; } = null!;
    public MenuApiFactory MenuFactory { get; private set; } = null!;
    public OrdersApiFactory OrdersFactory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _rabbitMq.StartAsync());

        var baseConn = _postgres.GetConnectionString();

        await CreateDatabaseAsync(baseConn, "LunchOrdering_Identity");
        await CreateDatabaseAsync(baseConn, "LunchOrdering_Menu");
        await CreateDatabaseAsync(baseConn, "LunchOrdering_Orders");

        var identityConn = ReplaceDatabase(baseConn, "LunchOrdering_Identity");
        var menuConn = ReplaceDatabase(baseConn, "LunchOrdering_Menu");
        var ordersConn = ReplaceDatabase(baseConn, "LunchOrdering_Orders");

        var rabbitMqHost = $"rabbitmq://{_rabbitMq.Hostname}:{_rabbitMq.GetMappedPublicPort(5672)}";

        IdentityFactory = new IdentityApiFactory(identityConn);
        MenuFactory = new MenuApiFactory(menuConn, rabbitMqHost, RabbitUser, RabbitPass);

        _ = MenuFactory.CreateClient();

        OrdersFactory = new OrdersApiFactory(ordersConn, rabbitMqHost, RabbitUser, RabbitPass, MenuFactory);

        await Task.WhenAll(
            IdentityFactory.InitializeDatabaseAsync(),
            MenuFactory.InitializeDatabaseAsync(),
            OrdersFactory.InitializeDatabaseAsync()
        );
    }

    private static async Task CreateDatabaseAsync(string connectionString, string dbName)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString) { Database = "postgres" };
        await using var conn = new NpgsqlConnection(builder.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"CREATE DATABASE \"{dbName}\"";
        try { await cmd.ExecuteNonQueryAsync(); }
        catch (PostgresException ex) when (ex.SqlState == "42P04") { }
    }

    private static string ReplaceDatabase(string connectionString, string dbName)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString) { Database = dbName };
        return builder.ConnectionString;
    }

    public async Task DisposeAsync()
    {
        await IdentityFactory.DisposeAsync();
        await MenuFactory.DisposeAsync();
        await OrdersFactory.DisposeAsync();
        await _postgres.DisposeAsync();
        await _rabbitMq.DisposeAsync();
    }
}
