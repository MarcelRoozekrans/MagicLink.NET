using Npgsql;

namespace MagicLink.PostgreSql.Tests;

/// <summary>
/// A fresh PostgreSQL database per test class, created on the server named by MAGICLINK_TEST_POSTGRES and
/// dropped afterwards, so test classes run in parallel. Without the variable the tests are skipped, except
/// in CI, where they fail: CI must never pass by skipping the database tests.
/// </summary>
public sealed class PostgresDatabase : IAsyncLifetime
{
    public const string Variable = "MAGICLINK_TEST_POSTGRES";

    private readonly string? _server = Environment.GetEnvironmentVariable(Variable);
    private readonly string _name = "magiclink_test_" + Guid.NewGuid().ToString("N");
    private string? _connectionString;

    /// <summary>The connection string of this class's own database.</summary>
    public string ConnectionString
    {
        get
        {
            if (_connectionString is not null)
            {
                return _connectionString;
            }

            if (string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Fail($"{Variable} is not set. CI must run the database tests.");
            }

            Assert.Skip($"Set {Variable} to a PostgreSQL connection string to run the database tests.");
            return null!; // Assert.Skip throws.
        }
    }

    public async ValueTask InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(_server))
        {
            return;
        }

        await using (var admin = new NpgsqlConnection(_server))
        {
            await admin.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE {_name}", admin);
            await command.ExecuteNonQueryAsync();
        }

        _connectionString = new NpgsqlConnectionStringBuilder(_server) { Database = _name }.ConnectionString;
    }

    public async ValueTask DisposeAsync()
    {
        if (_connectionString is null)
        {
            return;
        }

        NpgsqlConnection.ClearAllPools();
        await using var admin = new NpgsqlConnection(_server);
        await admin.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS {_name} WITH (FORCE)", admin);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Runs one scalar query against this class's database.</summary>
    public async Task<T?> ScalarAsync<T>(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        var value = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        return value is null or DBNull ? default : (T)value;
    }
}
