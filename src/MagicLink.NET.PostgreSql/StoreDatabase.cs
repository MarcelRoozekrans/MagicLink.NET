using System.Data.Async;
using System.Data.Async.Adapters;
using Npgsql;

namespace MagicLink.PostgreSql;

/// <summary>
/// The store's own data source, registered under this internal type so it never clashes with a data source the
/// host registers. Every store operation takes its own pooled connection, so a stream that is still being read
/// never shares a connection with another query.
/// </summary>
internal sealed class StoreDatabase(NpgsqlDataSource dataSource) : IAsyncDisposable, IDisposable
{
    internal NpgsqlDataSource DataSource { get; } = dataSource;

    /// <summary>A new, closed connection; the generated repository methods open and close it.</summary>
    internal IAsyncDbConnection Connect() => DataSource.CreateConnection().AsAsync();

    public ValueTask DisposeAsync() => DataSource.DisposeAsync();

    public void Dispose() => DataSource.Dispose();
}
