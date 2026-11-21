using ZeroAlloc.Resilience;

namespace MagicLink;

/// <summary>
/// One send of a sign-in email, retried a few times by the proxy ZeroAlloc.Resilience generates for this interface:
/// 3 attempts in all, 2 and then 4 seconds apart with some jitter, and at most 30 seconds per attempt.
/// </summary>
/// <remarks>
/// The retry sits here, not on <see cref="ISignInMessageSender"/>, so it also covers a sender the operator registers.
/// </remarks>
[Retry(MaxAttempts = 3, BackoffMs = 2_000, Jitter = true, PerAttemptTimeoutMs = 30_000)]
internal interface ISignInMailDelivery
{
    /// <summary>Sends <paramref name="message"/>; throws when that fails.</summary>
    ValueTask SendAsync(SignInMessage message, CancellationToken cancellationToken);
}
