using System.Globalization;
using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace MagicLink;

/// <summary>
/// The default mail sender: sends the sign-in email over SMTP with the settings in <see cref="MagicLinkSmtpOptions"/>
/// (ADR-015). It uses the SmtpClient that comes with .NET, which needs no extra package and runs under Native AOT.
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

    /// <inheritdoc />
    public async ValueTask SendAsync(SignInMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var options = _options.Value;
        using var mail = new MailMessage(new MailAddress(options.From!), new MailAddress(message.Email))
        {
            Subject = options.Subject,
            Body = Body(message),
            IsBodyHtml = false,
        };

        // SmtpClient is not safe to share between sends that run at the same time, so each send has its own.
        using var client = new SmtpClient(options.Host, options.Port)
        {
            EnableSsl = options.UseStartTls,
            DeliveryMethod = SmtpDeliveryMethod.Network,
        };
        if (options.UserName is not null)
        {
            client.Credentials = new NetworkCredential(options.UserName, options.Password);
        }

        await client.SendMailAsync(mail, cancellationToken).ConfigureAwait(false);
    }

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
