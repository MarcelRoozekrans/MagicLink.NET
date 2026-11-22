using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;

namespace MagicLink.Tests.Mail;

// docs/specs/magiclink-net-send-the-sign-in-email-through-a-pluggable-sender.md, criterion 1 (issue #6): mail goes out
// over SMTP by setting alone; criterion 3 (issue #54): over STARTTLS or TLS from the first byte, never unencrypted unless
// set to.
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

        await Assert.ThrowsAnyAsync<SmtpCommandException>(() => sender.SendAsync(Message, Cancel).AsTask());

        Assert.Empty(server.Messages);
    }

    [Fact]
    public async Task Cancelling_stops_a_send_that_waits_for_a_stalled_server()
    {
        await using var server = new FakeSmtpServer(withholdDataReply: true);
        var sender = Sender(server);
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Cancel);
        var send = sender.SendAsync(Message, cancel.Token).AsTask();

        // Cancelled only once the server holds the email and withholds its answer.
        using (var waiting = CancellationTokenSource.CreateLinkedTokenSource(Cancel))
        {
            waiting.CancelAfter(TimeSpan.FromSeconds(30));
            while (server.Messages.Count == 0)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(10), waiting.Token);
            }
        }

        var cancelled = TimeProvider.System.GetTimestamp();
        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => send);
        Assert.True(TimeProvider.System.GetElapsedTime(cancelled) < TimeSpan.FromSeconds(10));
        Assert.Single(server.Messages);
    }

    [Fact]
    public async Task The_email_goes_out_over_starttls_by_default()
    {
        await using var server = new FakeSmtpServer(advertiseLogin: true, tls: FakeSmtpTls.StartTls);
        var sender = Sender(server, options =>
        {
            options.Security = MagicLinkSmtpSecurity.StartTls;
            options.UserName = "mailer";
            options.Password = "mailer-password";
        }, trustServer: true);

        await sender.SendAsync(Message, Cancel);

        var received = Assert.Single(server.Messages);
        Assert.True(received.Secure);
        Assert.Equal("mailer", received.UserName);
        Assert.Contains(Message.Link.AbsoluteUri, Mime.Body(received.Data), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_email_goes_out_over_tls_from_the_first_byte_as_on_port_465()
    {
        await using var server = new FakeSmtpServer(advertiseLogin: true, tls: FakeSmtpTls.Implicit);
        var sender = Sender(server, options =>
        {
            options.Security = MagicLinkSmtpSecurity.ImplicitTls;
            options.UserName = "mailer";
            options.Password = "mailer-password";
        }, trustServer: true);

        await sender.SendAsync(Message, Cancel);

        var received = Assert.Single(server.Messages);
        Assert.True(received.Secure);
        Assert.Equal("mailer-password", received.Password);
    }

    [Fact]
    public async Task A_server_without_starttls_gets_no_email_rather_than_an_unencrypted_one()
    {
        await using var server = new FakeSmtpServer();
        var sender = Sender(server, options => options.Security = MagicLinkSmtpSecurity.StartTls, trustServer: true);

        await Assert.ThrowsAnyAsync<NotSupportedException>(() => sender.SendAsync(Message, Cancel).AsTask());

        Assert.Empty(server.Messages);
    }

    [Fact]
    public async Task A_server_certificate_the_system_does_not_trust_gets_no_email()
    {
        await using var server = new FakeSmtpServer(tls: FakeSmtpTls.Implicit);
        var sender = Sender(server, options => options.Security = MagicLinkSmtpSecurity.ImplicitTls);

        await Assert.ThrowsAnyAsync<SslHandshakeException>(() => sender.SendAsync(Message, Cancel).AsTask());

        Assert.Empty(server.Messages);
    }

    [Fact]
    public void Starttls_is_the_default()
    {
        Assert.Equal(MagicLinkSmtpSecurity.StartTls, new MagicLinkSmtpOptions().Security);
    }

    private static SmtpSignInMessageSender Sender(
        FakeSmtpServer server, Action<MagicLinkSmtpOptions>? configure = null, bool trustServer = false)
    {
        var options = new MagicLinkSmtpOptions
        {
            Host = "127.0.0.1",
            Port = server.Port,
            Security = MagicLinkSmtpSecurity.None,
            From = "Sign-in <signin@example.com>",
        };
        configure?.Invoke(options);

        // The test server's certificate is its own; trusting exactly that one keeps the rest of the check.
        var expected = server.Certificate?.GetCertHashString();
        return new SmtpSignInMessageSender(Options.Create(options))
        {
            CertificateValidation = trustServer
                ? (_, certificate, _, _) => certificate is not null && string.Equals(certificate.GetCertHashString(), expected, StringComparison.Ordinal)
                : null,
        };
    }
}
