using System.Globalization;
using System.Net.Security;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace MagicLink;

/// <summary>
/// The default mail sender: sends the sign-in email over SMTP with the settings in <see cref="MagicLinkSmtpOptions"/>
/// (ADR-015, amended 2026-11-22). It uses MailKit, which handles STARTTLS as well as TLS from the first byte on port
/// 465, and runs under Native AOT.
/// </summary>
internal sealed class SmtpSignInMessageSender : ISignInMessageSender
{
    private readonly IOptions<MagicLinkSmtpOptions> _options;

    /// <summary>Creates the sender; its settings are checked when MagicLink.NET starts, and read on each send.</summary>
    public SmtpSignInMessageSender(IOptions<MagicLinkSmtpOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <summary>
    /// Checks the server's certificate instead of the system's trusted roots. Only tests set it, to trust the
    /// certificate of their own server; <see langword="null"/> leaves the check to the system.
    /// </summary>
    internal RemoteCertificateValidationCallback? CertificateValidation { get; init; }

    /// <inheritdoc />
    public async ValueTask SendAsync(SignInMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var options = _options.Value;
        using var mail = new MimeMessage();
        mail.From.Add(MailboxAddress.Parse(options.From!));
        mail.To.Add(MailboxAddress.Parse(message.Email));
        mail.Subject = options.Subject;
        mail.Body = new TextPart("plain") { Text = Body(message) };

        // An SmtpClient is not safe to share between sends that run at the same time, so each send has its own.
        using var client = new SmtpClient();
        if (CertificateValidation is not null)
        {
            client.ServerCertificateValidationCallback = CertificateValidation;
        }

        await client.ConnectAsync(options.Host!, options.Port, Socket(options.Security), cancellationToken).ConfigureAwait(false);
        if (options.UserName is not null)
        {
            await client.AuthenticateAsync(options.UserName, options.Password ?? string.Empty, cancellationToken).ConfigureAwait(false);
        }

        await client.SendAsync(new SendRequest(mail), cancellationToken).ConfigureAwait(false);
        await client.DisconnectAsync(quit: true, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// MailKit's name for each setting. STARTTLS is required, never "when available": a server that does not offer it
    /// gets no email rather than one sent unencrypted (spec "send the sign-in email", criterion 3).
    /// </summary>
    private static SecureSocketOptions Socket(MagicLinkSmtpSecurity security) => security switch
    {
        MagicLinkSmtpSecurity.StartTls => SecureSocketOptions.StartTls,
        MagicLinkSmtpSecurity.ImplicitTls => SecureSocketOptions.SslOnConnect,
        MagicLinkSmtpSecurity.None => SecureSocketOptions.None,
        _ => throw new ArgumentOutOfRangeException(nameof(security), security, "Unknown SMTP security setting."),
    };

    /// <summary>The text of the email: the link to open in the browser that started the sign-in, and the code.</summary>
    private static string Body(SignInMessage message) => string.Create(CultureInfo.InvariantCulture, $"""
        Someone asked to sign in with this email address at {message.Link.Host}.

        To sign in, open this link in the browser where you started, and press the button on the page:
        {message.Link.AbsoluteUri}

        Or type this code there:
        {message.Code}

        The link and the code work until {message.ExpiresAt.UtcDateTime:yyyy-MM-dd HH:mm} UTC. If you did not ask to sign in, you can ignore this email.
        """);
}
