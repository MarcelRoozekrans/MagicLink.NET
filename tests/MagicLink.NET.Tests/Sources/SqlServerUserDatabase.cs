using System.Data.Common;

namespace MagicLink.Tests.Sources;

/// <summary>The user database on SQL Server.</summary>
public sealed class SqlServerUserDatabase : UserDatabase
{
    public override string Variable => "MAGICLINK_TEST_SQLSERVER";

    public override DbProviderFactory Factory => Microsoft.Data.SqlClient.SqlClientFactory.Instance;

    public override string WritingQuery => "INSERT INTO notes (note) OUTPUT inserted.note VALUES (@email)";

    public override string FailingAfterItsRowsQuery => "SELECT group_name FROM memberships WHERE email = @email; RAISERROR(N'lookup failed', 16, 1)";

    protected override string[] CreateDatabaseSql() =>
        [
            $"CREATE DATABASE {DatabaseName}",
            $"CREATE LOGIN {ReaderName} WITH PASSWORD = '{ReaderPassword}', CHECK_POLICY = OFF",
        ];

    protected override string CreateReaderSql() => $"""
        CREATE USER {ReaderName} FOR LOGIN {ReaderName};
        ALTER ROLE db_datareader ADD MEMBER {ReaderName};
        """;

    protected override string[] DropSql() =>
        [
            $"IF DB_ID('{DatabaseName}') IS NOT NULL ALTER DATABASE {DatabaseName} SET SINGLE_USER WITH ROLLBACK IMMEDIATE",
            $"DROP DATABASE IF EXISTS {DatabaseName}",
            $"IF SUSER_ID('{ReaderName}') IS NOT NULL DROP LOGIN {ReaderName}",
        ];

    protected override string ConnectionStringFor(string server, string database, string? user, string? password)
    {
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(server) { InitialCatalog = database };
        if (user is not null)
        {
            // The reader signs in with its own login, also when the server's connection string uses Windows sign-in.
            builder.IntegratedSecurity = false;
            builder.UserID = user;
            builder.Password = password;
        }

        return builder.ConnectionString;
    }

    protected override void ClearPools() => Microsoft.Data.SqlClient.SqlConnection.ClearAllPools();
}
