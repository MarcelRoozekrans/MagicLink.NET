using System.Net;

namespace MagicLink.Tests.SignIn;

// docs/specs/magiclink-net-send-the-sign-in-email-through-a-pluggable-sender.md, criterion 2 (issue #6): the email is
// sent in the background, so the answer does not wait for the mail server.
public sealed class BackgroundMailTests(SignInHosts hosts) : IClassFixture<SignInHosts>
{
    private static readonly Uri AppMe = new(SignInHosts.App, "me");

    [Fact]
    public async Task The_page_answers_before_the_email_is_sent()
    {
        var browser = hosts.NewBrowser();
        HtmlForm form;
        using (var emailForm = await SignInFlow.OpenEmailFormAsync(browser, AppMe))
        {
            form = await HtmlForm.ReadAsync(emailForm);
        }

        var queued = hosts.Queued;
        var arrived = hosts.Mailbox.Count;
        hosts.Mailbox.Hold();
        try
        {
            using var response = await form.SubmitAsync(browser, ("email", SignInHosts.Person));

            Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
            Assert.Equal(queued + 1, hosts.Queued);
            Assert.Equal(arrived, hosts.Mailbox.Count);
        }
        finally
        {
            hosts.Mailbox.Release();
        }

        var message = await hosts.Mailbox.WaitForAsync(arrived);
        using var confirmed = await SignInFlow.TypeCodeAsync(browser, message.Code);
        using var me = await SignInFlow.ContinueAsync(browser, confirmed);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }
}
