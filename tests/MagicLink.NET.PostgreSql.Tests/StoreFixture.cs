namespace MagicLink.PostgreSql.Tests;

/// <summary>A migrated database of its own per test class, with the store's data source on it.</summary>
public sealed class StoreFixture : IAsyncLifetime
{
    private readonly PostgresDatabase _database = new();
    private StoreDatabase? _store;

    internal StoreDatabase Store => _store ?? throw new InvalidOperationException(_database.ConnectionString);

    public PostgresDatabase Database => _database;

    public async ValueTask InitializeAsync()
    {
        await _database.InitializeAsync();
        if (Environment.GetEnvironmentVariable(PostgresDatabase.Variable) is { Length: > 0 })
        {
            _store = new StoreDatabase(StoreDataSource.Create(_database.ConnectionString));
            await StoreMigrator.RunAsync(_store.DataSource, CancellationToken.None);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_store is not null)
        {
            await _store.DisposeAsync();
        }

        await _database.DisposeAsync();
    }
}
