using System.Data.Common;

namespace MagicLink.Tests.Sources;

/// <summary>The user database on SQL Server.</summary>
public sealed class SqlServerUserDatabase : UserDatabase
{
    public override string Variable => "MAGICLINK_TEST_SQLSERVER";

    public override DbProviderFactory Factory => Microsoft.Data.SqlClient.SqlClientFactory.Instance;

    public override string WritingQuery => "INSERT INTO notes (note) OUTPUT inserted.note VALUES (@email)";

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
            $"ALTER DATABASE {DatabaseName} SET SINGLE_USER WITH ROLLBACK IMMEDIATE",
            $"DROP DATABASE {DatabaseName}",
            $"DROP LOGIN {ReaderName}",
        ];

    protected override string ConnectionStringFor(string server, string database, string? user, string? password)
    {
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(server) { InitialCatalog = database };
        if (user is not null)
        {
            builder.UserID = user;
            builder.Password = password;
        }

        return builder.ConnectionString;
    }

    protected override void ClearPools() => Microsoft.Data.SqlClient.SqlConnection.ClearAllPools();
}
