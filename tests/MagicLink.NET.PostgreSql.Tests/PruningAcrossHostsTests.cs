using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ZeroAlloc.Scheduling;

namespace MagicLink.PostgreSql.Tests;

/// <summary>
/// Several instances on one database share one schedule: one recurring row, and one run per due time. Own database,
/// since it counts every row of the job table.
/// </summary>
public sealed class PruningAcrossHostsTests(PostgresDatabase database) : IClassFixture<PostgresDatabase>
{
    private const string Job = "MagicLink.PostgreSql.PruneExpiredDataJob";

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private IHost BuildHost()
    {
        var builder = Host.CreateEmptyApplicationBuilder(settings: null);
        builder.Services.AddOpenIddict().AddCore(options => options.UsePostgreSql(store => store.ConnectionString = database.ConnectionString));
        builder.Services.Configure<SchedulingOptions>(static options => options.PollingInterval = TimeSpan.FromMilliseconds(100));
        return builder.Build();
    }

    private Task<long> CountAsync(int status) =>
        database.ScalarAsync<long>($"SELECT count(*) FROM magiclink.schedulingjobs WHERE typename = '{Job}' AND status = {status}");

    [Fact]
    public async Task Two_hosts_seed_one_recurring_row_and_run_it_once_per_due_time()
    {
        using var first = BuildHost();
        using var second = BuildHost();
        await Task.WhenAll(first.StartAsync(Cancel), second.StartAsync(Cancel));
        try
        {
            Assert.Equal(1, await database.ScalarAsync<long>("SELECT count(*) FROM magiclink.schedulingjobs"));

            // The seeded run is due at the next full hour; make it due now.
            await database.ScalarAsync<int>("UPDATE magiclink.schedulingjobs SET scheduledat = now() - interval '1 minute'");

            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (await CountAsync((int)JobStatus.Succeeded) == 0 && DateTime.UtcNow < deadline)
            {
                await Task.Delay(100, Cancel);
            }

            // Both workers keep polling for a while, so a second run of the same due time would show up here.
            await Task.Delay(TimeSpan.FromSeconds(1), Cancel);

            Assert.Equal(1, await CountAsync((int)JobStatus.Succeeded));
            Assert.Equal(1, await CountAsync((int)JobStatus.Pending));
            Assert.Equal(2, await database.ScalarAsync<long>("SELECT count(*) FROM magiclink.schedulingjobs"));
        }
        finally
        {
            await Task.WhenAll(first.StopAsync(Cancel), second.StopAsync(Cancel));
        }
    }
}
