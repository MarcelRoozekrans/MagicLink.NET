using MagicLink.Tests.Mail;

namespace MagicLink.Host.Tests.Mail;

/// <summary>The emails that reached the SMTP server the in-process host sends to.</summary>
internal sealed class SmtpTestMailbox(FakeSmtpServer server) : ITestMailbox
{
    public Task<IReadOnlyList<string>> ReadAsync(string email)
    {
        IReadOnlyList<string> emails = [.. server.Messages
            .Where(message => message.To.Contains("<" + email + ">", StringComparison.OrdinalIgnoreCase))
            .Select(static message => Mime.Body(message.Data))];
        return Task.FromResult(emails);
    }
}
