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
}
