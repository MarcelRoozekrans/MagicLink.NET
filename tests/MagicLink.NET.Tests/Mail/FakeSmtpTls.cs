namespace MagicLink.Tests.Mail;

/// <summary>Whether and how <see cref="FakeSmtpServer"/> offers TLS.</summary>
internal enum FakeSmtpTls
{
    None,
    StartTls,
    Implicit,
}
