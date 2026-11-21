using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZeroAlloc.Resilience;

namespace MagicLink;

/// <summary>
/// Sends the emails in <see cref="SignInMailQueue"/> in the background, a few at a time, so a slow or failing mail
/// server delays no answer to the person (ADR-015). A send that still fails after its retries is logged, without link
/// or code, and dropped.
/// </summary>
internal sealed partial class SignInMailWorker : BackgroundService
{
    /// <summary>How many emails are sent at the same time.</summary>
    public const int Parallel = 4;

    /// <summary>
    /// How long one email may take, its retries included, before it is given up: its token is cancelled, so no further
    /// attempt starts, and the worker stops waiting for it. Each attempt also gets 30 seconds from the retry.
    /// </summary>
    internal TimeSpan SendDeadline { get; init; } = TimeSpan.FromMinutes(2);

    private readonly SignInMailQueue _queue;
    private readonly ISignInMailDelivery _delivery;
    private readonly ILogger<SignInMailWorker> _logger;

    // Not disposed: a send given up at the deadline may still end, and release its slot, after the worker is disposed.
    private readonly SemaphoreSlim _slots = new(Parallel);

    /// <summary>Wraps the registered sender in the retry.</summary>
    public SignInMailWorker(SignInMailQueue queue, ISignInMessageSender sender, SignInMailDeliveryResiliencePolicies policies, ILogger<SignInMailWorker> logger)
    {
        _queue = queue;
        _delivery = new ISignInMailDeliveryResilienceProxy(new SignInMailDelivery(sender), policies);
        _logger = logger;
    }

    /// <inheritdoc />
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var senders = new Task[Parallel];
        for (var i = 0; i < senders.Length; i++)
        {
            senders[i] = SendAllAsync(stoppingToken);
        }

        return Task.WhenAll(senders);
    }

    private async Task SendAllAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (true)
            {
                // A slot is held until its send has really ended, also one given up at the deadline, so sends that
                // ignore their token cannot pile up beyond Parallel.
                await _slots.WaitAsync(stoppingToken).ConfigureAwait(false);
                SignInMessage message;
                try
                {
                    message = await _queue.Reader.ReadAsync(stoppingToken).ConfigureAwait(false);
                }
                catch
                {
                    _slots.Release();
                    throw;
                }

                await SendAsync(message, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Stopping: what is still waiting is lost, as ADR-015 accepts.
        }
    }

    private async Task SendAsync(SignInMessage message, CancellationToken stoppingToken)
    {
        // At the deadline the token is cancelled, so the retry starts no further attempt.
        var deadline = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        deadline.CancelAfter(SendDeadline);
        var send = _delivery.SendAsync(message, deadline.Token).AsTask();
        _ = EndAsync(send, deadline);
        try
        {
            // A sender that ignores its token is not waited for beyond the deadline; its slot stays taken until it ends.
            await send.WaitAsync(SendDeadline, stoppingToken).ConfigureAwait(false);
            Sent(_logger, message.RequestId);
        }
#pragma warning disable CA1031 // Whatever a sender throws must not stop the emails of everyone else.
        catch (Exception exception) when (exception is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
#pragma warning restore CA1031
        {
            // The retry proxy wraps the last failure; the log shows what the sender itself threw.
            NotSent(_logger, message.RequestId, exception is ResilienceException { InnerException: { } inner } ? inner : exception);
        }
    }

    /// <summary>Frees the slot and the deadline once the send has ended, however it ends.</summary>
    private async Task EndAsync(Task send, CancellationTokenSource deadline)
    {
        try
        {
            await send.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
        finally
        {
            deadline.Dispose();
            _slots.Release();
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "The sign-in email for request {RequestId} was sent.")]
    private static partial void Sent(ILogger logger, string requestId);

    [LoggerMessage(Level = LogLevel.Error, Message = "The sign-in email for request {RequestId} could not be sent, also after retrying; the person can request a new one.")]
    private static partial void NotSent(ILogger logger, string requestId, Exception exception);
}
