using Npgsql;

namespace MagicLink.PostgreSql;

/// <summary>Builds the store's own <see cref="NpgsqlDataSource"/>, apart from any data source the host registers.</summary>
internal static class StoreDataSource
{
    /// <summary>The schema that holds every table of the store.</summary>
    internal const string Schema = "magiclink";

    /// <summary>
    /// Builds a slim, AOT-friendly data source with arrays and JSON enabled. The search path is the store's schema,
    /// so the migration history table lands next to the store's tables.
    /// </summary>
    internal static NpgsqlDataSource Create(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString) { SearchPath = Schema };
        return new NpgsqlSlimDataSourceBuilder(builder.ConnectionString)
            .EnableArrays()
            .EnableJsonTypes()
            .Build();
    }
}
