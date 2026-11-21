namespace MagicLink;

/// <summary>
/// Sends the sign-in email. Issue #6 brings the pluggable sender with SMTP as default and sending in the background;
/// until then this is the seam the sign-in page hands its message to.
/// </summary>
internal interface ISignInMessageSender
{
    /// <summary>Sends <paramref name="message"/> to its address.</summary>
    ValueTask SendAsync(SignInMessage message, CancellationToken cancellationToken);
}
