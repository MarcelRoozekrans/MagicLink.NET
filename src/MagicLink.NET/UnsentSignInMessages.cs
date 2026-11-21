using Microsoft.Extensions.Logging;

namespace MagicLink;

/// <summary>The sender until one is registered: it sends nothing and says so in the log, without link or code.</summary>
internal sealed partial class UnsentSignInMessages(ILogger<UnsentSignInMessages> logger) : ISignInMessageSender
{
    /// <inheritdoc />
    public ValueTask SendAsync(SignInMessage message, CancellationToken cancellationToken)
    {
        NoSender(logger);
        return ValueTask.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "No mail sender is registered, so a sign-in email was not sent.")]
    private static partial void NoSender(ILogger logger);
}
