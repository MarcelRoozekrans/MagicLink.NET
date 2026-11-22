namespace MagicLink;

/// <summary>
/// Settings for MagicLink.NET sign-in requests.
/// </summary>
public sealed class MagicLinkOptions
{
    /// <summary>
    /// How long the short code in a sign-in email stays valid. Defaults to 10 minutes.
    /// </summary>
    public TimeSpan CodeLifetime { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// How many times a code may be tried before the sign-in request stops working. Defaults to 5.
    /// </summary>
    public int MaxCodeAttempts { get; set; } = 5;

    /// <summary>
    /// How many sign-in emails one address gets within <see cref="EmailLimitWindow"/>. Beyond that the sign-in page
    /// answers as always, but sends no email, so nobody can flood a person's mailbox. Defaults to 5.
    /// </summary>
    public int MaxEmailsPerAddress { get; set; } = 5;

    /// <summary>
    /// The sliding window <see cref="MaxEmailsPerAddress"/> counts in. Defaults to 15 minutes.
    /// </summary>
    public TimeSpan EmailLimitWindow { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// How many sign-ins one client IP address starts within <see cref="ClientLimitWindow"/>, for any addresses, known or
    /// not. Beyond that the sign-in page answers as always, but sends no email, so nobody can mail or probe many
    /// addresses from one machine. An IPv6 address counts by its /64 network. Defaults to 20.
    /// </summary>
    /// <remarks>
    /// The client's address is the connection's remote address. Behind a reverse proxy, the host must apply the
    /// forwarded headers of the proxies it trusts, or every person counts as the proxy.
    /// </remarks>
    public int MaxSignInsPerClient { get; set; } = 20;

    /// <summary>
    /// The sliding window <see cref="MaxSignInsPerClient"/> counts in. Defaults to 15 minutes.
    /// </summary>
    public TimeSpan ClientLimitWindow { get; set; } = TimeSpan.FromMinutes(15);
}
