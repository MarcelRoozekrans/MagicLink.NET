using Microsoft.Extensions.Logging;
using ZeroAlloc.Resilience;

namespace MagicLink.Tests.Mail;

// docs/specs/magiclink-net-send-the-sign-in-email-through-a-pluggable-sender.md, criterion 2 (issue #6): the email is
// sent in the background, a failed send is retried a few times and then logged, and nothing outlives the process.
public sealed class SignInMailWorkerTests
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_queued_email_is_sent_in_the_background()
    {
        var sender = new FlakySender(failures: 0);
        await using var worker = await Worker.StartAsync(sender);

        worker.Queue.Enqueue(Message("request-1"));

        var sent = await sender.NextAsync();
        Assert.Equal("request-1", sent.RequestId);
        Assert.Equal(1, worker.Queue.Queued);
        Assert.Contains("request-1", (await worker.Log.WaitForAsync(LogLevel.Information)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_failed_send_is_retried()
    {
        var sender = new FlakySender(failures: 2);
        await using var worker = await Worker.StartAsync(sender);

        worker.Queue.Enqueue(Message("request-1"));

        Assert.Equal("request-1", (await sender.NextAsync()).RequestId);
        Assert.Equal(3, sender.Calls);
        Assert.DoesNotContain(worker.Log.Entries, static entry => entry.Level == LogLevel.Error);
    }

    [Fact]
    public async Task A_send_that_keeps_failing_is_logged_without_link_or_code_and_the_next_email_still_goes_out()
    {
        var sender = new FlakySender(failures: 3);
        await using var worker = await Worker.StartAsync(sender);

        worker.Queue.Enqueue(Message("request-1"));
        var error = await worker.Log.WaitForAsync(LogLevel.Error);
        worker.Queue.Enqueue(Message("request-2"));

        Assert.Equal("request-2", (await sender.NextAsync()).RequestId);
        Assert.Contains("request-1", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-token", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("ABCD-EFGH", error.Message, StringComparison.Ordinal);

        // The log shows what the sender threw, not the retry's wrapper around it.
        Assert.IsType<InvalidOperationException>(error.Exception);
        Assert.Equal(4, sender.Calls);
    }

    [Fact]
    public async Task A_sender_that_throws_something_unexpected_does_not_stop_the_sending()
    {
        var sender = new FlakySender(failures: 1, failure: static () => new OperationCanceledException("not from stopping"));
        await using var worker = await Worker.StartAsync(sender, attempts: 1);

        worker.Queue.Enqueue(Message("request-1"));
        var error = await worker.Log.WaitForAsync(LogLevel.Error);
        worker.Queue.Enqueue(Message("request-2"));

        Assert.Equal("request-2", (await sender.NextAsync()).RequestId);
        Assert.Contains("request-1", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_sender_that_ignores_cancellation_is_given_up_at_the_deadline()
    {
        var sender = new FlakySender(failures: 0, ignoreCancellation: true);
        await using var worker = await Worker.StartAsync(sender, attempts: 1, deadline: TimeSpan.FromMilliseconds(200));

        worker.Queue.Enqueue(Message("request-1"));
        var error = await worker.Log.WaitForAsync(LogLevel.Error);

        Assert.Contains("request-1", error.Message, StringComparison.Ordinal);
        Assert.IsType<TimeoutException>(error.Exception);
    }

    [Fact]
    public async Task A_send_that_fails_after_the_deadline_is_not_tried_again()
    {
        var sender = new GatedSender(fail: true);
        await using var worker = await Worker.StartAsync(sender, attempts: 3, deadline: TimeSpan.FromMilliseconds(100));

        worker.Queue.Enqueue(Message("request-1"));
        await worker.Log.WaitForAsync(LogLevel.Error);
        sender.Open();
        await sender.EndedAsync(1);

        // The retry would start its next attempt after 1 ms; the deadline cancelled it.
        await Task.Delay(TimeSpan.FromMilliseconds(200), Cancel);
        Assert.Equal(1, sender.Started);
    }

    [Fact]
    public async Task Sends_given_up_at_the_deadline_still_count_toward_the_parallel_limit()
    {
        var sender = new GatedSender(fail: false);
        await using var worker = await Worker.StartAsync(sender, attempts: 1, deadline: TimeSpan.FromMilliseconds(50));
        for (var i = 0; i < SignInMailWorker.Parallel + 2; i++)
        {
            worker.Queue.Enqueue(Message("request-" + i));
        }

        await worker.Log.WaitForCountAsync(LogLevel.Error, SignInMailWorker.Parallel);
        await Task.Delay(TimeSpan.FromMilliseconds(200), Cancel);
        Assert.Equal(SignInMailWorker.Parallel, sender.Started);

        sender.Open();
        await sender.EndedAsync(SignInMailWorker.Parallel + 2);
        Assert.Equal(SignInMailWorker.Parallel, sender.MaxActive);
    }

    [Fact]
    public async Task Stopping_drops_what_is_still_waiting_without_an_error()
    {
        var sender = new FlakySender(failures: 0, waitForever: true);
        var worker = await Worker.StartAsync(sender);
        for (var i = 0; i < SignInMailWorker.Parallel + 2; i++)
        {
            worker.Queue.Enqueue(Message("request-" + i));
        }

        await sender.StartedAsync(SignInMailWorker.Parallel);
        await worker.DisposeAsync();

        Assert.Empty(sender.Sent);
        Assert.DoesNotContain(worker.Log.Entries, static entry => entry.Level >= LogLevel.Warning);
    }

    [Fact]
    public void A_full_queue_drops_the_email_and_logs_it()
    {
        var log = new ListLogger<SignInMailQueue>();
        var queue = new SignInMailQueue(log);
        for (var i = 0; i < SignInMailQueue.Capacity; i++)
        {
            Assert.True(queue.Enqueue(Message("request-" + i)));
        }

        Assert.False(queue.Enqueue(Message("one-too-many")));

        Assert.Equal(SignInMailQueue.Capacity, queue.Queued);
        var entry = Assert.Single(log.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("one-too-many", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_email_is_given_up_after_two_minutes_by_default()
    {
        using var worker = new SignInMailWorker(
            new SignInMailQueue(new ListLogger<SignInMailQueue>()), new FlakySender(failures: 0), new SignInMailDeliveryResiliencePolicies(), new ListLogger<SignInMailWorker>());

        Assert.Equal(TimeSpan.FromMinutes(2), worker.SendDeadline);
    }

    [Fact]
    public void The_default_retry_makes_three_attempts()
    {
        var retry = new SignInMailDeliveryResiliencePolicies().Retry;

        Assert.Equal(3, retry.MaxAttempts);
        Assert.Equal(2_000, retry.BackoffMs);
        Assert.Equal(30_000, retry.PerAttemptTimeoutMs);
    }

    private static SignInMessage Message(string requestId) =>
        new("person@example.com", new Uri("https://magiclink.test/signin/link?token=secret-token"), "ABCD-EFGH", DateTimeOffset.UtcNow.AddMinutes(10), requestId);

    private sealed class Worker : IAsyncDisposable
    {
        private readonly SignInMailWorker _worker;

        private Worker(SignInMailWorker worker, SignInMailQueue queue, ListLogger<SignInMailWorker> log)
        {
            _worker = worker;
            Queue = queue;
            Log = log;
        }

        public SignInMailQueue Queue { get; }

        public ListLogger<SignInMailWorker> Log { get; }

        public static async Task<Worker> StartAsync(ISignInMessageSender sender, int attempts = 3, TimeSpan? deadline = null)
        {
            var queue = new SignInMailQueue(new ListLogger<SignInMailQueue>());
            var log = new ListLogger<SignInMailWorker>();
            var policies = new SignInMailDeliveryResiliencePolicies { Retry = new RetryPolicy(attempts, 1, false, 0) };
            var worker = new SignInMailWorker(queue, sender, policies, log) { SendDeadline = deadline ?? TimeSpan.FromSeconds(30) };
            await worker.StartAsync(Cancel);
            return new Worker(worker, queue, log);
        }

        public async ValueTask DisposeAsync()
        {
            await _worker.StopAsync(CancellationToken.None);
            _worker.Dispose();
        }
    }

    /// <summary>Ignores its token and waits until <see cref="Open"/>, then fails or succeeds; counts what runs at once.</summary>
    private sealed class GatedSender(bool fail) : ISignInMessageSender
    {
        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _started;
        private int _ended;
        private int _active;
        private int _maxActive;

        public int Started => Volatile.Read(ref _started);

        public int MaxActive => Volatile.Read(ref _maxActive);

        public void Open() => _gate.TrySetResult();

        public async ValueTask SendAsync(SignInMessage message, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _started);
            var active = Interlocked.Increment(ref _active);
            int max;
            while (active > (max = Volatile.Read(ref _maxActive)) && Interlocked.CompareExchange(ref _maxActive, active, max) != max)
            {
            }

            try
            {
                await _gate.Task;
                if (fail)
                {
                    throw new InvalidOperationException("The mail server gave up.");
                }
            }
            finally
            {
                Interlocked.Decrement(ref _active);
                Interlocked.Increment(ref _ended);
            }
        }

        public async Task EndedAsync(int count)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Cancel);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            while (Volatile.Read(ref _ended) < count)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(10), timeout.Token);
            }
        }
    }

    /// <summary>Fails the first <c>failures</c> calls, then keeps what it is asked to send.</summary>
    private sealed class FlakySender(int failures, Func<Exception>? failure = null, bool waitForever = false, bool ignoreCancellation = false) : ISignInMessageSender
    {
        private readonly System.Threading.Channels.Channel<SignInMessage> _sent = System.Threading.Channels.Channel.CreateUnbounded<SignInMessage>();
        private readonly Lock _lock = new();
        private readonly List<SignInMessage> _kept = [];
        private int _calls;
        private int _started;

        public int Calls => Volatile.Read(ref _calls);

        public IReadOnlyList<SignInMessage> Sent
        {
            get
            {
                lock (_lock)
                {
                    return [.. _kept];
                }
            }
        }

        public async ValueTask SendAsync(SignInMessage message, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _calls) <= failures)
            {
                throw failure?.Invoke() ?? new InvalidOperationException("The mail server is down.");
            }

            if (ignoreCancellation)
            {
                await new TaskCompletionSource().Task;
            }

            if (waitForever)
            {
                Interlocked.Increment(ref _started);
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }

            lock (_lock)
            {
                _kept.Add(message);
            }

            await _sent.Writer.WriteAsync(message, cancellationToken);
        }

        public async Task<SignInMessage> NextAsync()
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Cancel);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            return await _sent.Reader.ReadAsync(timeout.Token);
        }

        public async Task StartedAsync(int count)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Cancel);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            while (Volatile.Read(ref _started) < count)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(10), timeout.Token);
            }
        }
    }
}
