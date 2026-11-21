using System.Data.Common;

namespace MagicLink.Tests.Sources;

/// <summary>
/// An existing user database, as an organization would have one: a fresh database per test class on the server named by
/// <see cref="Variable"/>, with people and their groups, and an account that may only read it. Without the variable the
/// tests are skipped, except in CI, where they fail: CI must never pass by skipping the database tests.
/// </summary>
public abstract class UserDatabase : IAsyncLifetime
{
    /// <summary>People and their groups: Ann is in two groups, one listed twice; Bob is in none.</summary>
    private const string Schema = """
        CREATE TABLE people (id int NOT NULL PRIMARY KEY, email varchar(200) NOT NULL);
        CREATE TABLE memberships (email varchar(200) NOT NULL, group_name varchar(100) NULL);
        CREATE TABLE notes (note varchar(200) NOT NULL);
        INSERT INTO people (id, email) VALUES (1, 'ann@example.com'), (2, 'bob@example.com');
        INSERT INTO memberships (email, group_name) VALUES ('ann@example.com', 'admins'), ('ann@example.com', 'developers'), ('ann@example.com', 'admins');
        """;

    private readonly string _suffix = Guid.NewGuid().ToString("N");
    private string? _adminConnectionString;
    private string? _readerConnectionString;

    /// <summary>The environment variable with a connection string to the server, for an account that may create databases.</summary>
    public abstract string Variable { get; }

    /// <summary>The ADO.NET provider of the engine.</summary>
    public abstract DbProviderFactory Factory { get; }

    /// <summary>A query that writes a note for the address and returns it.</summary>
    public abstract string WritingQuery { get; }

    /// <summary>The connection string of the account that owns this database and may write to it.</summary>
    public string AdminConnectionString => _adminConnectionString ?? Skip();

    /// <summary>The connection string of an account that may only read this database.</summary>
    public string ReaderConnectionString => _readerConnectionString ?? Skip();

    /// <summary>The name of this class's database.</summary>
    protected string DatabaseName => "magiclink_users_" + _suffix;

    /// <summary>The name of the account that may only read.</summary>
    protected string ReaderName => "magiclink_reader_" + _suffix;

    /// <summary>The password of the account that may only read.</summary>
    protected static string ReaderPassword => "Reader!Passw0rd";

    private string? Server => Environment.GetEnvironmentVariable(Variable);

    public async ValueTask InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(Server))
        {
            return;
        }

        foreach (var statement in CreateDatabaseSql())
        {
            await RunAsync(Server, statement);
        }

        var admin = ConnectionStringFor(Server, DatabaseName, user: null, password: null);
        await RunAsync(admin, Schema);
        await RunAsync(admin, CreateReaderSql());
        _adminConnectionString = admin;
        _readerConnectionString = ConnectionStringFor(Server, DatabaseName, ReaderName, ReaderPassword);
    }

    public async ValueTask DisposeAsync()
    {
        if (_adminConnectionString is null)
        {
            return;
        }

        ClearPools();
        foreach (var statement in DropSql())
        {
            await RunAsync(Server!, statement);
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>Runs <paramref name="sql"/> on this class's database as its owner and returns the first value.</summary>
    public async Task<object?> ScalarAsync(string sql)
    {
        var connection = Factory.CreateConnection()!;
        await using (connection)
        {
            connection.ConnectionString = AdminConnectionString;
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        }
    }

    /// <summary>Creates this class's database and the account that may only read, each statement on its own.</summary>
    protected abstract string[] CreateDatabaseSql();

    /// <summary>Creates the account that may only read, run on this class's database as its owner.</summary>
    protected abstract string CreateReaderSql();

    /// <summary>Drops the database and the account, each statement on its own.</summary>
    protected abstract string[] DropSql();

    protected abstract string ConnectionStringFor(string server, string database, string? user, string? password);

    protected abstract void ClearPools();

    private async Task RunAsync(string connectionString, string sql)
    {
        var connection = Factory.CreateConnection()!;
        await using (connection)
        {
            connection.ConnectionString = connectionString;
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync();
        }
    }

    private string Skip()
    {
        if (string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Fail($"{Variable} is not set. CI must run the database tests.");
        }

        Assert.Skip($"Set {Variable} to a connection string to run the database tests.");
        return null!; // Assert.Skip throws.
    }
}
