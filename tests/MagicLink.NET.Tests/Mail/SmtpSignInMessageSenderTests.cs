using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace MagicLink.Tests.Mail;

// docs/specs/magiclink-net-send-the-sign-in-email-through-a-pluggable-sender.md, criterion 1 (issue #6): mail goes out
// over SMTP by setting alone.
public sealed class SmtpSignInMessageSenderTests
{
    private static readonly SignInMessage Message = new(
        "person@example.com",
        new Uri("https://magiclink.test/signin/link?token=the-link-token-that-is-long-enough-to-make-the-line-wrap-somewhere"),
        "ABCD-EFGH",
        new DateTimeOffset(2026, 11, 21, 14, 5, 0, TimeSpan.Zero),
        "request-1");

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_email_goes_out_over_smtp_with_the_link_and_the_code()
    {
        await using var server = new FakeSmtpServer();
        var sender = Sender(server);

        await sender.SendAsync(Message, Cancel);

        var received = Assert.Single(server.Messages);
        Assert.Contains("<signin@example.com>", received.From, StringComparison.Ordinal);
        Assert.Contains("<person@example.com>", received.To, StringComparison.Ordinal);
        Assert.Null(received.UserName);
        Assert.Contains("Subject: Your sign-in link and code", received.Data, StringComparison.Ordinal);
        var body = Mime.Body(received.Data);
        Assert.Contains(Message.Link.AbsoluteUri, body, StringComparison.Ordinal);
        Assert.Contains("ABCD-EFGH", body, StringComparison.Ordinal);
        Assert.Contains("2026-11-21 14:05 UTC", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_sender_signs_in_to_the_smtp_server_when_a_user_name_is_set()
    {
        await using var server = new FakeSmtpServer(advertiseLogin: true);
        var sender = Sender(server, options =>
        {
            options.UserName = "mailer";
            options.Password = "mailer-password";
        });

        await sender.SendAsync(Message, Cancel);

        var received = Assert.Single(server.Messages);
        Assert.Equal("mailer", received.UserName);
        Assert.Equal("mailer-password", received.Password);
    }

    [Fact]
    public async Task A_refused_email_throws_so_it_is_retried()
    {
        await using var server = new FakeSmtpServer(refuseRecipients: true);
        var sender = Sender(server);

        await Assert.ThrowsAnyAsync<SmtpException>(() => sender.SendAsync(Message, Cancel).AsTask());

        Assert.Empty(server.Messages);
    }

    [Fact]
    public async Task Cancelling_stops_a_send_that_waits_for_a_stalled_server()
    {
        await using var server = new FakeSmtpServer(withholdDataReply: true);
        var sender = Sender(server);
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Cancel);
        cancel.CancelAfter(TimeSpan.FromMilliseconds(500));
        var started = TimeProvider.System.GetTimestamp();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sender.SendAsync(Message, cancel.Token).AsTask());

        Assert.True(TimeProvider.System.GetElapsedTime(started) < TimeSpan.FromSeconds(10));
        Assert.Single(server.Messages);
    }

    private static SmtpSignInMessageSender Sender(FakeSmtpServer server, Action<MagicLinkSmtpOptions>? configure = null)
    {
        var options = new MagicLinkSmtpOptions
        {
            Host = "127.0.0.1",
            Port = server.Port,
            UseStartTls = false,
            From = "Sign-in <signin@example.com>",
        };
        configure?.Invoke(options);
        return new SmtpSignInMessageSender(Options.Create(options));
    }
}
