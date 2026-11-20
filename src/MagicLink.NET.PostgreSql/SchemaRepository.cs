using System.Data.Async;
using Npgsql;
using ZeroAlloc.ORM;

namespace MagicLink.PostgreSql;

/// <summary>Creates the store's schema, which may race with another instance doing the same.</summary>
[Dialect(SqlDialect.PostgreSql)]
internal sealed partial class SchemaRepository(IAsyncDbConnection connection)
{
    // unique_violation on pg_namespace: another instance created the schema between the check and the insert.
    private const string UniqueViolation = "23505";

    [Command("CREATE SCHEMA IF NOT EXISTS magiclink")]
    private partial ValueTask<int> CreateSchemaAsync(CancellationToken cancellationToken);

    /// <summary>Creates schema <c>magiclink</c> unless it exists.</summary>
    internal async ValueTask EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        try
        {
            await CreateSchemaAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (PostgresException exception) when (exception.SqlState is UniqueViolation or PostgresErrorCodes.DuplicateSchema)
        {
            // Created concurrently; nothing left to do.
        }
    }
}
