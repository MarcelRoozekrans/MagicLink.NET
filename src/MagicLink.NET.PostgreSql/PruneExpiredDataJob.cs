using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using ZeroAlloc.Scheduling;

namespace MagicLink.PostgreSql;

/// <summary>
/// Prunes tokens and authorizations once an hour through OpenIddict's managers, keeping everything created in the last
/// 14 days, the same window as OpenIddict's Quartz integration. The job has no fields, so it is stored without a payload
/// and needs no job serializer; it takes the OpenIddict managers from the worker's scope for each run.
/// </summary>
[Job(Every = Every.Hour)]
internal sealed class PruneExpiredDataJob : IJob
{
    /// <summary>How long data is kept, from its creation, before it can be pruned.</summary>
    internal static readonly TimeSpan Retention = TimeSpan.FromDays(14);

    public async ValueTask ExecuteAsync(JobContext ctx, CancellationToken ct)
    {
        var threshold = DateTimeOffset.UtcNow - Retention;

        await ctx.Services.GetRequiredService<IOpenIddictTokenManager>().PruneAsync(threshold, ct).ConfigureAwait(false);
        await ctx.Services.GetRequiredService<IOpenIddictAuthorizationManager>().PruneAsync(threshold, ct).ConfigureAwait(false);
    }
}
