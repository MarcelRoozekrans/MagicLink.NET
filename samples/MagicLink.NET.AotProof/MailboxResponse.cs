namespace MagicLink.AotProof;

/// <summary>The answer of <c>/proof/mailbox</c>.</summary>
/// <param name="Count">How many sign-in emails went to the address.</param>
/// <param name="Code">The code in the last of them; null when none went.</param>
public sealed record MailboxResponse(int Count, string? Code);
