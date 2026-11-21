namespace MagicLink;

/// <summary>Hands a send to the registered <see cref="ISignInMessageSender"/>.</summary>
internal sealed class SignInMailDelivery(ISignInMessageSender sender) : ISignInMailDelivery
{
    /// <inheritdoc />
    public ValueTask SendAsync(SignInMessage message, CancellationToken cancellationToken) => sender.SendAsync(message, cancellationToken);
}
