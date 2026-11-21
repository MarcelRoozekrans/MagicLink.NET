using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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

    /// <summary>Every run of the job, by job id, from both hosts.</summary>
    private readonly ConcurrentQueue<Guid> _runs = new();

    /// <summary>Holds both hosts' polls at the claim, so they compete for the same due run.</summary>
    private readonly ClaimGate _gate = new(hosts: 2);

    private IHost BuildHost()
    {
        var builder = Host.CreateEmptyApplicationBuilder(settings: null);
        builder.Services.AddOpenIddict().AddCore(options => options.UsePostgreSql(store => store.ConnectionString = database.ConnectionString));
        builder.Services.Configure<SchedulingOptions>(static options => options.PollingInterval = TimeSpan.FromMilliseconds(100));

        // The generated executor, wrapped so each run is recorded.
        builder.Services.RemoveAll<IJobTypeExecutor>();
        builder.Services.AddSingleton<IJobTypeExecutor>(new RecordingExecutor(new PruneExpiredDataJobTypeExecutor(), _runs));

        // The store's job store, wrapped so each poll passes the gate before it claims.
        var store = builder.Services.Single(static d => d.ServiceType == typeof(IJobStore));
        builder.Services.Remove(store);
        builder.Services.AddScoped<IJobStore>(provider => new GatedJobStore((IJobStore)store.ImplementationFactory!(provider), _gate));
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
            var seeded = await database.ScalarAsync<Guid>("SELECT id FROM magiclink.schedulingjobs");

            // Both hosts wait at the claim while the seeded run, due at the next full hour, is made due now; then
            // both claim at once.
            await _gate.ArmAsync(TimeSpan.FromSeconds(10), Cancel);
            await database.ScalarAsync<int>("UPDATE magiclink.schedulingjobs SET scheduledat = now() - interval '1 minute'");
            _gate.Release();

            // The run is marked Succeeded before its successor is inserted, so wait for both.
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while ((await CountAsync((int)JobStatus.Succeeded) != 1 || await CountAsync((int)JobStatus.Pending) != 1) && DateTime.UtcNow < deadline)
            {
                await Task.Delay(100, Cancel);
            }

            // Both workers have polled several more times by now, so a second run of the same due time would show here.
            await Task.Delay(TimeSpan.FromSeconds(1), Cancel);

            Assert.Equal([seeded], _runs);
            Assert.Equal(1, await CountAsync((int)JobStatus.Succeeded));
            Assert.Equal(1, await CountAsync((int)JobStatus.Pending));
            Assert.Equal(2, await database.ScalarAsync<long>("SELECT count(*) FROM magiclink.schedulingjobs"));
        }
        finally
        {
            await Task.WhenAll(first.StopAsync(Cancel), second.StopAsync(Cancel));
        }
    }

    /// <summary>
    /// Once armed, holds each host's next poll before it claims, until every host is waiting and the test releases them
    /// together. Unarmed, polls pass straight through.
    /// </summary>
    private sealed class ClaimGate(int hosts)
    {
        private readonly Lock _lock = new();
        private TaskCompletionSource? _allWaiting;
        private TaskCompletionSource? _released;
        private int _waiting;

        /// <summary>Arms the gate and returns once every host is waiting at it.</summary>
        public async Task ArmAsync(TimeSpan timeout, CancellationToken ct)
        {
            Task allWaiting;
            lock (_lock)
            {
                _waiting = 0;
                _allWaiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                allWaiting = _allWaiting.Task;
            }

            await allWaiting.WaitAsync(timeout, ct);
        }

        /// <summary>Lets the waiting polls claim, all at once, and disarms the gate.</summary>
        public void Release()
        {
            TaskCompletionSource? released;
            lock (_lock)
            {
                released = _released;
                _released = null;
                _allWaiting = null;
            }

            released?.TrySetResult();
        }

        public Task WaitAsync()
        {
            lock (_lock)
            {
                if (_released is null || _waiting >= hosts) return Task.CompletedTask;

                if (++_waiting == hosts) _allWaiting!.TrySetResult();
                return _released.Task;
            }
        }
    }

    /// <summary>Passes the gate, then claims through the store's own job store.</summary>
    private sealed class GatedJobStore(IJobStore inner, ClaimGate gate) : IJobStore
    {
        public async ValueTask<IReadOnlyList<JobEntry>> FetchPendingAsync(int batchSize, CancellationToken ct)
        {
            await gate.WaitAsync().WaitAsync(ct);
            return await inner.FetchPendingAsync(batchSize, ct);
        }

        public ValueTask EnqueueAsync(string typeName, byte[] payload, DateTimeOffset scheduledAt, int maxAttempts, string? cronExpression, CancellationToken ct) =>
            inner.EnqueueAsync(typeName, payload, scheduledAt, maxAttempts, cronExpression, ct);

        public ValueTask MarkSucceededAsync(JobId id, DateTimeOffset? nextRunAt, string? cronExpression, int maxAttempts, CancellationToken ct) =>
            inner.MarkSucceededAsync(id, nextRunAt, cronExpression, maxAttempts, ct);

        public ValueTask MarkFailedAsync(JobId id, int attempts, DateTimeOffset nextRetryAt, CancellationToken ct) =>
            inner.MarkFailedAsync(id, attempts, nextRetryAt, ct);

        public ValueTask DeadLetterAsync(JobId id, string error, CancellationToken ct) => inner.DeadLetterAsync(id, error, ct);

        public ValueTask UpsertRecurringAsync(string typeName, byte[] payload, DateTimeOffset scheduledAt, string? cronExpression, int maxAttempts, CancellationToken ct) =>
            inner.UpsertRecurringAsync(typeName, payload, scheduledAt, cronExpression, maxAttempts, ct);
    }

    /// <summary>Records the job id of each run, then runs the job.</summary>
    private sealed class RecordingExecutor(IJobTypeExecutor inner, ConcurrentQueue<Guid> runs) : IJobTypeExecutor
    {
        public string TypeName => inner.TypeName;

        public int MaxAttempts => inner.MaxAttempts;

        public ValueTask ExecuteAsync(byte[] payload, JobContext ctx, CancellationToken ct)
        {
            runs.Enqueue(ctx.JobId.Value);
            return inner.ExecuteAsync(payload, ctx, ct);
        }
    }
}
