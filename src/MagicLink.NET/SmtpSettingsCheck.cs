using System.Net.Mail;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MagicLink;

/// <summary>
/// Checks <see cref="MagicLinkSmtpOptions"/> when MagicLink.NET starts, but only while SMTP is the sender in use: an
/// operator who registers an own sender needs none of these settings.
/// </summary>
/// <remarks>
/// The sender is looked up while validating, not injected: the SMTP sender itself depends on these options, so
/// injecting it would make the options depend on themselves.
/// </remarks>
internal sealed class SmtpSettingsCheck(IServiceProvider services) : IValidateOptions<MagicLinkSmtpOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, MagicLinkSmtpOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (services.GetRequiredService<ISignInMessageSender>() is not SmtpSignInMessageSender)
        {
            return ValidateOptionsResult.Skip;
        }

        var failures = new List<string>();
        if (string.IsNullOrWhiteSpace(options.Host))
        {
            failures.Add("MagicLink.NET sends the sign-in email over SMTP unless another mail sender is registered, so MagicLinkSmtpOptions.Host must be set.");
        }

        if (options.Port is <= 0 or > ushort.MaxValue)
        {
            failures.Add("MagicLinkSmtpOptions.Port must be between 1 and 65535.");
        }

        if (!MailAddress.TryCreate(options.From, out _))
        {
            failures.Add("MagicLinkSmtpOptions.From must be the email address the sign-in email is sent from.");
        }

        if (string.IsNullOrWhiteSpace(options.Subject))
        {
            failures.Add("MagicLinkSmtpOptions.Subject must not be empty.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
