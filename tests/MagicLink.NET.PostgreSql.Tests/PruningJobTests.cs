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
    public async Task A_run_prunes_what_is_no_longer_valid_and_older_than_14_days_and_keeps_the_rest()
    {
        using var host = BuildHost();
        await host.StartAsync(Cancel);
        await using var scope = host.Services.CreateAsyncScope();
        var tokens = scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>();
        var authorizations = scope.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>();
        var now = DateTimeOffset.UtcNow;
        var subject = "pruned-" + Guid.NewGuid().ToString("N");

        // Both sides of each rule: old and no longer valid goes; recent, or still valid, stays.
        await tokens.CreateAsync(Token(subject, created: now.AddDays(-30), expires: now.AddDays(-29)), Cancel);
        var recentExpired = await tokens.GetIdAsync(await tokens.CreateAsync(Token(subject, created: now.AddDays(-1), expires: now.AddHours(-1)), Cancel), Cancel);
        var oldValid = await tokens.GetIdAsync(await tokens.CreateAsync(Token(subject, created: now.AddDays(-30), expires: now.AddDays(1)), Cancel), Cancel);
        await authorizations.CreateAsync(Authorization(subject, OpenIddictConstants.Statuses.Revoked, created: now.AddDays(-30)), Cancel);
        var recentRevoked = await authorizations.GetIdAsync(
            await authorizations.CreateAsync(Authorization(subject, OpenIddictConstants.Statuses.Revoked, created: now.AddDays(-1)), Cancel), Cancel);
        var oldPermanent = await authorizations.GetIdAsync(
            await authorizations.CreateAsync(Authorization(subject, OpenIddictConstants.Statuses.Valid, created: now.AddDays(-30)), Cancel), Cancel);

        var executor = scope.ServiceProvider.GetServices<IJobTypeExecutor>()
            .Single(static executor => string.Equals(executor.TypeName, "MagicLink.PostgreSql.PruneExpiredDataJob", StringComparison.Ordinal));
        await executor.ExecuteAsync([], new JobContext { JobId = JobId.New(), Attempt = 1, ScheduledAt = now, Services = scope.ServiceProvider }, Cancel);

        var keptTokens = new List<string?>();
        await foreach (var token in tokens.FindBySubjectAsync(subject, Cancel))
        {
            keptTokens.Add(await tokens.GetIdAsync(token, Cancel));
        }

        var keptAuthorizations = new List<string?>();
        await foreach (var authorization in authorizations.FindBySubjectAsync(subject, Cancel))
        {
            keptAuthorizations.Add(await authorizations.GetIdAsync(authorization, Cancel));
        }

        Assert.Equal(new[] { recentExpired, oldValid }.Order(StringComparer.Ordinal), keptTokens.Order(StringComparer.Ordinal));
        Assert.Equal(new[] { recentRevoked, oldPermanent }.Order(StringComparer.Ordinal), keptAuthorizations.Order(StringComparer.Ordinal));
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
}
