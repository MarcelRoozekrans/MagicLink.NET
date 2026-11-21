using System.Data.Common;

namespace MagicLink.Tests.Sources;

/// <summary>The user database on PostgreSQL.</summary>
public sealed class PostgreSqlUserDatabase : UserDatabase
{
    public override string Variable => "MAGICLINK_TEST_POSTGRES";

    public override DbProviderFactory Factory => Npgsql.NpgsqlFactory.Instance;

    public override string WritingQuery => "INSERT INTO notes (note) VALUES (@email) RETURNING note";

    public override string FailingAfterItsRowsQuery => "SELECT group_name FROM memberships WHERE email = @email; SELECT 1 / 0";

    protected override string[] CreateDatabaseSql() =>
        [
            $"CREATE DATABASE {DatabaseName}",
            $"CREATE ROLE {ReaderName} LOGIN PASSWORD '{ReaderPassword}'",
        ];

    protected override string CreateReaderSql() => $"""
        GRANT CONNECT ON DATABASE {DatabaseName} TO {ReaderName};
        GRANT USAGE ON SCHEMA public TO {ReaderName};
        GRANT SELECT ON ALL TABLES IN SCHEMA public TO {ReaderName};
        """;

    protected override string[] DropSql() =>
        [
            $"DROP DATABASE IF EXISTS {DatabaseName} WITH (FORCE)",
            $"DROP ROLE IF EXISTS {ReaderName}",
        ];

    protected override string ConnectionStringFor(string server, string database, string? user, string? password)
    {
        var builder = new Npgsql.NpgsqlConnectionStringBuilder(server) { Database = database };
        if (user is not null)
        {
            builder.Username = user;
            builder.Password = password;
        }

        return builder.ConnectionString;
    }

    protected override void ClearPools() => Npgsql.NpgsqlConnection.ClearAllPools();
}
