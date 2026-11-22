namespace MagicLink.Host.Tests.Mail;

/// <summary>Where the tests read the sign-in emails the host sent over SMTP.</summary>
internal interface ITestMailbox
{
    /// <summary>The text of each email to <paramref name="email"/>, oldest first.</summary>
    Task<IReadOnlyList<string>> ReadAsync(string email);
}
