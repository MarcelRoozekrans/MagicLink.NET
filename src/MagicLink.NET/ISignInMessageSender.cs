namespace MagicLink;

/// <summary>
/// Sends the sign-in email (ADR-015). The default sends it over SMTP with the settings in
/// <see cref="MagicLinkSmtpOptions"/>; registering another implementation replaces it, for example a mail service with an
/// API, or a sender that only keeps the messages in a test.
/// </summary>
/// <remarks>
/// The sign-in page does not wait for this: it hands the message to a queue inside the process, and a background
/// service calls the sender, retries a failed send a few times and then logs it. A sender may therefore take its time,
/// and should throw when a send fails so it is retried. It should stop promptly when its token is cancelled: each
/// attempt gets 30 seconds, and an email that is still being sent after two minutes is given up. Never log <see cref="SignInMessage.Link"/> or
/// <see cref="SignInMessage.Code"/>: either one signs the person in.
/// </remarks>
internal interface ISignInMessageSender
{
    /// <summary>Sends <paramref name="message"/> to its address; throws when that fails.</summary>
    ValueTask SendAsync(SignInMessage message, CancellationToken cancellationToken);
}
