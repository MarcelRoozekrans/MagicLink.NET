using System.Data.Async.Adapters;
using Npgsql;
using ZeroAlloc.ORM.Migrations;

namespace MagicLink.PostgreSql;

/// <summary>Applies the store's SQL migrations, under a PostgreSQL advisory lock so concurrent instances apply each once.</summary>
internal static class StoreMigrator
{
    /// <summary>The migration source name, recorded per applied migration in the history table.</summary>
    internal const string SourceName = "MagicLink.NET.PostgreSql";

    // "MLNETPG1": the store's own advisory lock, apart from the default lock other ZeroAlloc.ORM users take.
    private const long LockKey = 0x4D4C_4E45_5450_4731L;

    /// <summary>Runs every migration not applied yet and returns those it applied.</summary>
    internal static async Task<IReadOnlyList<Migration>> RunAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        var connection = dataSource.CreateConnection().AsAsync();
        await using (connection.ConfigureAwait(false))
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            // The history table is created in the search path's schema before the first migration runs.
            await new SchemaRepository(connection).EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

            var source = new EmbeddedResourceMigrationSource(typeof(StoreMigrator).Assembly) { Name = SourceName };
            var runner = new MigrationRunner(connection, source, new PostgresMigrationDialect(LockKey));
            return await runner.RunAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
