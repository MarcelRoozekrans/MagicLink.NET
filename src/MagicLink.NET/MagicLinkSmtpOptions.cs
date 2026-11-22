namespace MagicLink;

/// <summary>
/// Settings for sending the sign-in email over SMTP, the default way MagicLink.NET sends it (ADR-015). They are needed
/// only while no other mail sender is registered; MagicLink.NET then does not start without <see cref="Host"/> and
/// <see cref="From"/>.
/// </summary>
public sealed class MagicLinkSmtpOptions
{
    /// <summary>The SMTP server's host name.</summary>
    public string? Host { get; set; }

    /// <summary>The SMTP server's port. Defaults to 587, the submission port.</summary>
    public int Port { get; set; } = 587;

    /// <summary>
    /// How the connection to the server is protected. Defaults to <see cref="MagicLinkSmtpSecurity.StartTls"/>; use
    /// <see cref="MagicLinkSmtpSecurity.ImplicitTls"/> for a server on port 465.
    /// </summary>
    public MagicLinkSmtpSecurity Security { get; set; } = MagicLinkSmtpSecurity.StartTls;

    /// <summary>The user name to sign in to the SMTP server with, or <see langword="null"/> to send without signing in.</summary>
    public string? UserName { get; set; }

    /// <summary>The password that goes with <see cref="UserName"/>.</summary>
    public string? Password { get; set; }

    /// <summary>The sender of the email, an address with an optional name, such as <c>Sign-in &lt;signin@example.com&gt;</c>.</summary>
    public string? From { get; set; }

    /// <summary>The subject of the email. Defaults to "Your sign-in link and code".</summary>
    public string Subject { get; set; } = "Your sign-in link and code";
}
