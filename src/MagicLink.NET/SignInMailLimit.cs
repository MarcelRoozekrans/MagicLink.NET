namespace MagicLink;

/// <summary>
/// Counts the sign-in emails each address got in a sliding window, so nobody can flood a person's mailbox by starting
/// sign-ins for their address (spec "confirm sign-in with a link or a code", criterion 7).
/// </summary>
/// <remarks>
/// Only addresses that may sign in are counted, since only they get emails; the page answers the same either way, so
/// the count tells nobody which addresses exist.
/// </remarks>
internal sealed class SignInMailLimit(TimeProvider clock) : SlidingWindowLimit(clock);
