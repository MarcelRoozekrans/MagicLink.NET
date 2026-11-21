using System.Data.Async.Adapters;
using Npgsql;
using ZeroAlloc.ORM.Migrations;
using ZeroAlloc.Scheduling.Orm;

namespace MagicLink.PostgreSql;

/// <summary>Applies the store's SQL migrations, under a PostgreSQL advisory lock so concurrent instances apply each once.</summary>
internal static class StoreMigrator
{
    /// <summary>The migration source name, recorded per applied migration in the history table.</summary>
    internal const string SourceName = "MagicLink.NET.PostgreSql";

    // "MLNETPG1": the store's own advisory lock, apart from the default lock other ZeroAlloc.ORM users take.
    private const long LockKey = 0x4D4C_4E45_5450_4731L;

    /// <summary>Runs every migration of the store not applied yet and returns those it applied.</summary>
    internal static Task<IReadOnlyList<Migration>> RunAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken)
        => RunAsync(dataSource, includeJobStore: false, cancellationToken);

    /// <summary>
    /// Runs every migration not applied yet and returns those it applied: the store's, then, with
    /// <paramref name="includeJobStore"/>, those of ZeroAlloc.Scheduling's job table. Each source runs under the store's
    /// advisory lock, and both record their versions in the store's history table, apart by source name.
    /// </summary>
    internal static async Task<IReadOnlyList<Migration>> RunAsync(NpgsqlDataSource dataSource, bool includeJobStore, CancellationToken cancellationToken)
    {
        var connection = dataSource.CreateConnection().AsAsync();
        await using (connection.ConfigureAwait(false))
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            // The history table is created in the search path's schema before the first migration runs.
            await new SchemaRepository(connection).EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

            var dialect = new PostgresMigrationDialect(LockKey);
            var source = new EmbeddedResourceMigrationSource(typeof(StoreMigrator).Assembly) { Name = SourceName };
            var applied = await new MigrationRunner(connection, source, dialect).RunAsync(cancellationToken).ConfigureAwait(false);
            if (!includeJobStore)
            {
                return applied;
            }

            var jobStore = await new MigrationRunner(connection, SchedulingOrmMigrations.Postgres, dialect).RunAsync(cancellationToken).ConfigureAwait(false);
            return [.. applied, .. jobStore];
        }
    }
}
