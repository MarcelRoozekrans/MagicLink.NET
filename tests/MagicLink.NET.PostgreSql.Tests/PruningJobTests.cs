using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenIddict.Abstractions;
using ZeroAlloc.Scheduling;

namespace MagicLink.PostgreSql.Tests;

/// <summary>The pruning job on the store's database: its table, its seed and one run. Own database.</summary>
public sealed class PruningJobTests(PostgresDatabase database) : IClassFixture<PostgresDatabase>
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    /// <summary>A host that validates scopes, as every Development host does.</summary>
    private IHost BuildHost()
    {
        var builder = Host.CreateEmptyApplicationBuilder(settings: null);
        builder.ConfigureContainer(new DefaultServiceProviderFactory(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true }));
        builder.Services.AddOpenIddict().AddCore(options => options.UsePostgreSql(store => store.ConnectionString = database.ConnectionString));
        return builder.Build();
    }

    [Fact]
    public async Task Startup_creates_the_job_table_next_to_the_store_and_seeds_one_run()
    {
        using var host = BuildHost();

        await host.StartAsync(Cancel);
        await host.StopAsync(Cancel);

        Assert.Equal(1, await database.ScalarAsync<long>(
            "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'magiclink' AND table_name = 'schedulingjobs'"));
        Assert.Equal(1, await database.ScalarAsync<long>(
            "SELECT count(*) FROM magiclink.schedulingjobs WHERE typename = 'MagicLink.PostgreSql.PruneExpiredDataJob' AND status = 0"));
        Assert.Equal(4, await database.ScalarAsync<long>("SELECT count(*) FROM magiclink.__zaorm_migrations"));
    }

    [Fact]
    public async Task A_run_prunes_what_expired_more_than_14_days_ago_and_keeps_the_rest()
    {
        using var host = BuildHost();
        await host.StartAsync(Cancel);
        await using var scope = host.Services.CreateAsyncScope();
        var tokens = scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>();
        var authorizations = scope.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>();
        var now = DateTimeOffset.UtcNow;
        var subject = "pruned-" + Guid.NewGuid().ToString("N");

        await tokens.CreateAsync(Token(subject, created: now.AddDays(-30), expires: now.AddDays(-29)), Cancel);
        await tokens.CreateAsync(Token(subject, created: now, expires: now.AddHours(1)), Cancel);
        await authorizations.CreateAsync(Authorization(subject, OpenIddictConstants.Statuses.Revoked, created: now.AddDays(-30)), Cancel);
        await authorizations.CreateAsync(Authorization(subject, OpenIddictConstants.Statuses.Valid, created: now), Cancel);

        var executor = scope.ServiceProvider.GetServices<IJobTypeExecutor>()
            .Single(static executor => string.Equals(executor.TypeName, "MagicLink.PostgreSql.PruneExpiredDataJob", StringComparison.Ordinal));
        await executor.ExecuteAsync([], new JobContext { JobId = JobId.New(), Attempt = 1, ScheduledAt = now, Services = scope.ServiceProvider }, Cancel);

        Assert.Single(await ToListAsync(tokens.FindBySubjectAsync(subject, Cancel)));
        Assert.Single(await ToListAsync(authorizations.FindBySubjectAsync(subject, Cancel)));
        await host.StopAsync(Cancel);
    }

    private static OpenIddictTokenDescriptor Token(string subject, DateTimeOffset created, DateTimeOffset expires) => new()
    {
        Subject = subject,
        Status = OpenIddictConstants.Statuses.Valid,
        Type = OpenIddictConstants.TokenTypeHints.RefreshToken,
        CreationDate = created,
        ExpirationDate = expires,
    };

    private static OpenIddictAuthorizationDescriptor Authorization(string subject, string status, DateTimeOffset created) => new()
    {
        Subject = subject,
        Status = status,
        Type = OpenIddictConstants.AuthorizationTypes.Permanent,
        CreationDate = created,
    };

    private static async Task<List<T>> ToListAsync<T>(IAsyncEnumerable<T> source)
    {
        var list = new List<T>();
        await foreach (var item in source)
        {
            list.Add(item);
        }

        return list;
    }
}
