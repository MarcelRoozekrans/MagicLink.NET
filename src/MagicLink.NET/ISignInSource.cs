namespace MagicLink;

/// <summary>
/// Decides who may sign in, and with which groups (ADR-014). MagicLink.NET asks it each time it issues tokens for a
/// person, so a change in the source applies at the next sign-in to an application. The groups go into the
/// <c>groups</c> claim of the tokens.
/// </summary>
/// <remarks>
/// MagicLink.NET ships a file kept in git as a source, registered with
/// <see cref="SignInSourceRegistration.AddMagicLinkSignInFile"/>; anyone can write their own, for example against an API
/// or LDAP, and register it with <see cref="SignInSourceRegistration.AddMagicLinkSignInSource{TSource}"/>. MagicLink.NET
/// does not start without a source. A source only reads: it never creates or changes people.
/// </remarks>
internal interface ISignInSource
{
    /// <summary>
    /// Whether <paramref name="email"/> may sign in, and if so with which groups. The address is normalized: trimmed
    /// and in lower case. Returns <see cref="SignInAccess.Refused"/> for an address the source does not know; throws when
    /// the source cannot answer, so that nobody signs in on a guess.
    /// </summary>
    ValueTask<SignInAccess> LookUpAsync(string email, CancellationToken cancellationToken);
}
