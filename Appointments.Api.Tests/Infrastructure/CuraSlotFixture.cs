using Npgsql;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace Appointments.Api.Tests.Infrastructure;

public class CuraSlotFixture : IAsyncLifetime
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

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _rabbitMq.StartAsync());

        var baseConn = _postgres.GetConnectionString();

        await CreateDatabaseAsync(baseConn, "CuraSlot_Identity");
        await CreateDatabaseAsync(baseConn, "CuraSlot_Doctors");
        await CreateDatabaseAsync(baseConn, "CuraSlot_Appointments");

        var identityConn = ReplaceDatabase(baseConn, "CuraSlot_Identity");

        IdentityFactory = new IdentityApiFactory(identityConn);

        await IdentityFactory.InitializeDatabaseAsync();
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
        await _postgres.DisposeAsync();
        await _rabbitMq.DisposeAsync();
    }
}
