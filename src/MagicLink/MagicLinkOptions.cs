namespace MagicLink;

/// <summary>
/// Settings that control how magic links are issued and validated.
/// </summary>
public sealed class MagicLinkOptions
{
    /// <summary>
    /// The longest lifetime a magic link may have: 24 hours.
    /// </summary>
    public static readonly TimeSpan MaxTokenLifetime = TimeSpan.FromHours(24);

    /// <summary>
    /// How long a magic link stays valid after it is issued. Defaults to 15 minutes.
    /// </summary>
    public TimeSpan TokenLifetime { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Checks that the settings are usable.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// <see cref="TokenLifetime"/> is zero, negative, or longer than <see cref="MaxTokenLifetime"/>.
    /// </exception>
    public void Validate()
    {
        if (TokenLifetime <= TimeSpan.Zero || TokenLifetime > MaxTokenLifetime)
        {
            throw new InvalidOperationException(
                $"{nameof(TokenLifetime)} must be greater than zero and at most {MaxTokenLifetime}, but was {TokenLifetime}.");
        }
    }
}
