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
    /// How long one email may take, its retries included, before it is given up. The retry gives each attempt 30 seconds
    /// through the cancellation token; this also frees the worker from a sender that does not honour that token.
    /// </summary>
    internal TimeSpan SendDeadline { get; init; } = TimeSpan.FromMinutes(2);

    private readonly SignInMailQueue _queue;
    private readonly ISignInMailDelivery _delivery;
    private readonly ILogger<SignInMailWorker> _logger;

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
            await foreach (var message in _queue.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
            {
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
        try
        {
            // A send still running at the deadline is left to finish or fail on its own; its outcome is not awaited.
            await _delivery.SendAsync(message, stoppingToken).AsTask().WaitAsync(SendDeadline, stoppingToken).ConfigureAwait(false);
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

    [LoggerMessage(Level = LogLevel.Information, Message = "The sign-in email for request {RequestId} was sent.")]
    private static partial void Sent(ILogger logger, string requestId);

    [LoggerMessage(Level = LogLevel.Error, Message = "The sign-in email for request {RequestId} could not be sent, also after retrying; the person can request a new one.")]
    private static partial void NotSent(ILogger logger, string requestId, Exception exception);
}
