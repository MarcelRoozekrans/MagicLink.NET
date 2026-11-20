namespace MagicLink.PostgreSql.Tests;

public sealed class MigrationTests(PostgresDatabase database) : IClassFixture<PostgresDatabase>
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Migrations_create_the_four_tables_in_the_magiclink_schema()
    {
        await using var dataSource = StoreDataSource.Create(database.ConnectionString);

        await StoreMigrator.RunAsync(dataSource, Cancel);

        var tables = await database.ScalarAsync<long>(
            "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'magiclink' " +
            "AND table_name IN ('applications', 'authorizations', 'scopes', 'tokens')");
        Assert.Equal(4, tables);
    }

    [Fact]
    public async Task Running_the_migrations_twice_applies_nothing_the_second_time()
    {
        await using var dataSource = StoreDataSource.Create(database.ConnectionString);
        await StoreMigrator.RunAsync(dataSource, Cancel);

        var second = await StoreMigrator.RunAsync(dataSource, Cancel);

        Assert.Empty(second);
        Assert.Equal(1, await database.ScalarAsync<long>("SELECT count(*) FROM magiclink.__zaorm_migrations"));
    }
}
