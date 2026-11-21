using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace MagicLink.PostgreSql;

/// <summary>
/// Applies the store's migrations while the host is starting, before any hosted service or the web server starts,
/// so nothing reads the store before its tables exist. That includes the job table when the store registered the
/// pruning job's store, so the job's recurring seed, a hosted service, finds it.
/// </summary>
internal sealed class StoreMigrationService(StoreDatabase database, IOptions<MagicLinkPostgreSqlOptions> options, bool includeJobStore)
    : IHostedLifecycleService
{
    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        if (options.Value.ApplyMigrationsOnStartup)
        {
            await StoreMigrator.RunAsync(database.DataSource, includeJobStore, cancellationToken).ConfigureAwait(false);
        }
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
