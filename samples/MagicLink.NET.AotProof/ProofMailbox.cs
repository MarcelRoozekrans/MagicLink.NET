using System.Collections.Concurrent;

namespace MagicLink.AotProof;

/// <summary>
/// Keeps the sign-in emails to each address in memory instead of sending them, so the flow tests can type the code.
/// Proof-only: a real sender never exposes the code.
/// </summary>
internal sealed class ProofMailbox : ISignInMessageSender
{
    private readonly ConcurrentDictionary<string, MailboxResponse> _last = new(StringComparer.Ordinal);

    public ValueTask SendAsync(SignInMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        _last.AddOrUpdate(message.Email, static (_, code) => new MailboxResponse(1, code), static (_, last, code) => new MailboxResponse(last.Count + 1, code), message.Code);
        return ValueTask.CompletedTask;
    }

    /// <summary>How many emails went to <paramref name="email"/>, and the code of the last one.</summary>
    public MailboxResponse For(string email) => _last.TryGetValue(email, out var last) ? last : new MailboxResponse(0, Code: null);
}
