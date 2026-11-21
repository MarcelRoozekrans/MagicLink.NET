using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Npgsql;
using OpenIddict.Abstractions;

namespace MagicLink.PostgreSql.Tests;

public sealed class RegistrationTests(PostgresDatabase database) : IClassFixture<PostgresDatabase>
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static IHost BuildHost(Action<MagicLinkPostgreSqlOptions> configure)
    {
        var builder = Host.CreateEmptyApplicationBuilder(settings: null);
        builder.Services.AddOpenIddict().AddCore(options => options.UsePostgreSql(configure));
        return builder.Build();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public async Task A_missing_connection_string_fails_at_startup(string? connectionString)
    {
        using var host = BuildHost(store => store.ConnectionString = connectionString);

        var error = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync(Cancel));

        Assert.Contains(nameof(MagicLinkPostgreSqlOptions.ConnectionString), error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Migrations_off_leaves_the_database_empty()
    {
        using var host = BuildHost(store =>
        {
            store.ConnectionString = database.ConnectionString;
            store.ApplyMigrationsOnStartup = false;
            store.PruneExpiredData = false;
        });

        await host.StartAsync(Cancel);
        await host.StopAsync(Cancel);

        Assert.Equal(0, await database.ScalarAsync<long>("SELECT count(*) FROM pg_namespace WHERE nspname = 'magiclink'"));
    }

    [Fact]
    public async Task Migrations_off_with_pruning_on_fails_at_startup_until_the_job_table_exists()
    {
        using var host = BuildHost(store =>
        {
            store.ConnectionString = database.ConnectionString;
            store.ApplyMigrationsOnStartup = false;
        });

        var error = await Assert.ThrowsAsync<PostgresException>(() => host.StartAsync(Cancel));

        Assert.Equal(PostgresErrorCodes.UndefinedTable, error.SqlState);
        Assert.Equal(0, await database.ScalarAsync<long>("SELECT count(*) FROM pg_namespace WHERE nspname = 'magiclink'"));
    }
}
