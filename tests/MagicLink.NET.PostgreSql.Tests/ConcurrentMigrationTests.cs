namespace MagicLink.PostgreSql.Tests;

/// <summary>Migrations started at the same time on an empty database both succeed. Own database, since it must start empty.</summary>
public sealed class ConcurrentMigrationTests(PostgresDatabase database) : IClassFixture<PostgresDatabase>
{
    [Fact]
    public async Task Two_runs_started_at_once_both_succeed_and_apply_once()
    {
        await using var first = StoreDataSource.Create(database.ConnectionString);
        await using var second = StoreDataSource.Create(database.ConnectionString);

        var runs = await Task.WhenAll(
            StoreMigrator.RunAsync(first, TestContext.Current.CancellationToken),
            StoreMigrator.RunAsync(second, TestContext.Current.CancellationToken));

        Assert.Equal(1, runs.Sum(applied => applied.Count));
    }
}
