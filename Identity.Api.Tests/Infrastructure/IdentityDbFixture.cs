using Npgsql;
using Testcontainers.PostgreSql;

namespace Identity.Api.Tests.Infrastructure;

public class IdentityDbFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    public IdentityApiFactory Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var baseConnectionString = _postgres.GetConnectionString();
        await CreateDatabaseAsync(baseConnectionString, "LunchOrdering_Identity");

        var connectionString = ReplaceDatabase(baseConnectionString, "LunchOrdering_Identity");
        Factory = new IdentityApiFactory(connectionString);

        await Factory.InitializeDatabaseAsync();
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
        await Factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
