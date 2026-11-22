namespace MagicLink;

/// <summary>How the SMTP sender protects its connection to the mail server (ADR-015, amended 2026-11-22).</summary>
public enum MagicLinkSmtpSecurity
{
    /// <summary>
    /// Connect without TLS, then upgrade to TLS with STARTTLS before anything is sent, usually on port 587 or 25. A server
    /// that does not offer STARTTLS gets nothing: the send fails rather than going out unencrypted. The default.
    /// </summary>
    StartTls = 0,

    /// <summary>TLS from the first byte, usually on port 465.</summary>
    ImplicitTls = 1,

    /// <summary>
    /// No TLS at all. The email holds a link and a code that sign the person in, so use this only for a mail server on
    /// the same host or network.
    /// </summary>
    None = 2,
}
