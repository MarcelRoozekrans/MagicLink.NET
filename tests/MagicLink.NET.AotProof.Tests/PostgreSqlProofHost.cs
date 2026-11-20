using Npgsql;

namespace MagicLink.AotProof.Tests;

/// <summary>
/// The proof app on the MagicLink.NET PostgreSQL store. In-process it runs on a database of its own, created on the
/// server named by MAGICLINK_TEST_POSTGRES and dropped afterwards. Without that variable its tests are skipped, except
/// in CI, where they fail. Against the native binary, CI started the binary on PostgreSQL.
/// </summary>
public sealed class PostgreSqlProofHost : ProofHost
{
    public const string ServerVariable = "MAGICLINK_TEST_POSTGRES";

    private readonly string? _server = Environment.GetEnvironmentVariable(ServerVariable);
    private readonly string _name = "magiclink_proof_" + Guid.NewGuid().ToString("N");
    private bool _created;

    protected override async ValueTask<ProofDatabase?> PrepareDatabaseAsync()
    {
        if (string.IsNullOrWhiteSpace(_server))
        {
            return new ProofDatabase(Ready: false, ConnectionString: null);
        }

        await using (var admin = new NpgsqlConnection(_server))
        {
            await admin.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE {_name}", admin);
            await command.ExecuteNonQueryAsync();
        }

        _created = true;
        return new ProofDatabase(Ready: true, new NpgsqlConnectionStringBuilder(_server) { Database = _name }.ConnectionString);
    }

    protected override Exception Unavailable()
    {
        if (string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Fail($"{ServerVariable} is not set. CI must run the flow tests on PostgreSQL.");
        }

        Assert.Skip($"Set {ServerVariable} to a PostgreSQL connection string to run the flow tests on the PostgreSQL store.");
        return null!; // Assert.Skip throws.
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (_created)
        {
            NpgsqlConnection.ClearAllPools();
            await using var admin = new NpgsqlConnection(_server);
            await admin.OpenAsync();
            await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS {_name} WITH (FORCE)", admin);
            await command.ExecuteNonQueryAsync();
        }
    }
}
