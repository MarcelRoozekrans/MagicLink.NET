using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace MagicLink;

/// <summary>
/// The sign-in emails waiting to be sent, kept in memory only (ADR-015): after a restart an unsent email is lost and
/// the person requests a new one. The sign-in page adds to it without waiting; <see cref="SignInMailWorker"/> sends.
/// </summary>
internal sealed partial class SignInMailQueue(ILogger<SignInMailQueue> logger)
{
    /// <summary>How many emails may wait at once; beyond that a new one is dropped and logged.</summary>
    public const int Capacity = 1_000;

    private readonly Channel<SignInMessage> _channel = Channel.CreateBounded<SignInMessage>(new BoundedChannelOptions(Capacity)
    {
        FullMode = BoundedChannelFullMode.Wait, // So TryWrite says when the queue is full, instead of dropping silently.
        SingleWriter = false,
        SingleReader = false,
    });

    private int _queued;

    /// <summary>How many emails were added since the start; tests read it to see that no email was asked for.</summary>
    public int Queued => Volatile.Read(ref _queued);

    /// <summary>Where <see cref="SignInMailWorker"/> takes the emails from.</summary>
    public ChannelReader<SignInMessage> Reader => _channel.Reader;

    /// <summary>Adds <paramref name="message"/> without waiting; when the queue is full the email is dropped and logged.</summary>
    public void Enqueue(SignInMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (_channel.Writer.TryWrite(message))
        {
            Interlocked.Increment(ref _queued);
            return;
        }

        Full(logger, message.RequestId, Capacity);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The sign-in email for request {RequestId} was not sent: {Capacity} emails are already waiting to be sent.")]
    private static partial void Full(ILogger logger, string requestId, int capacity);
}
