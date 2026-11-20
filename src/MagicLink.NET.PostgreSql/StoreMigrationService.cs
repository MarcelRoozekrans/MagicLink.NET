using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace MagicLink.PostgreSql;

/// <summary>
/// Applies the store's migrations while the host is starting, before any hosted service or the web server starts,
/// so nothing reads the store before its tables exist.
/// </summary>
internal sealed class StoreMigrationService(StoreDatabase database, IOptions<MagicLinkPostgreSqlOptions> options) : IHostedLifecycleService
{
    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        if (options.Value.ApplyMigrationsOnStartup)
        {
            await StoreMigrator.RunAsync(database.DataSource, cancellationToken).ConfigureAwait(false);
        }
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
