using System.Net;

namespace MagicLink.Tests.SignIn;

// docs/specs/magiclink-net-confirm-sign-in-with-a-link-or-a-code.md, criterion 7 (issue #39): at most 5 sign-in emails
// per address in 15 minutes; beyond that the answer stays the same, but no email goes out.
public sealed class EmailLimitTests(DefaultLimitSignInHosts hosts) : IClassFixture<DefaultLimitSignInHosts>
{
    private static readonly Uri AppMe = new(SignInHosts.App, "me");

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_sixth_sign_in_within_15_minutes_gets_the_same_answer_but_no_email()
    {
        for (var i = 0; i < 5; i++)
        {
            var queued = hosts.Queued;
            (await StartAsync(hosts.NewBrowser(), SignInHosts.OneGroupPerson)).Dispose();
            Assert.Equal(queued + 1, hosts.Queued);
        }

        var browser = hosts.NewBrowser();
        var before = hosts.Queued;
        using var sixth = await StartAsync(browser, SignInHosts.OneGroupPerson);

        Assert.Equal(HttpStatusCode.SeeOther, sixth.StatusCode);
        Assert.Equal("/signin/code", sixth.Headers.Location!.OriginalString);
        Assert.Equal(before, hosts.Queued);
        using var codePage = await browser.NavigateAsync(SignInFlow.CodePage);
        Assert.Equal(HttpStatusCode.OK, codePage.StatusCode);
        Assert.Contains("If " + SignInHosts.OneGroupPerson + " is known", await codePage.Content.ReadAsStringAsync(Cancel), StringComparison.Ordinal);

        var requestId = RequestId(sixth);
        Assert.Contains(hosts.Logs.Entries, entry =>
            string.Equals(entry.Category, "MagicLink.SignIn", StringComparison.Ordinal)
            && entry.Message.Contains(requestId, StringComparison.Ordinal)
            && entry.Message.Contains("no email", StringComparison.Ordinal));

        // A quarter of an hour later the address gets emails again.
        hosts.Clock.Advance(TimeSpan.FromMinutes(15));
        var later = hosts.Queued;
        (await StartAsync(hosts.NewBrowser(), SignInHosts.OneGroupPerson)).Dispose();
        Assert.Equal(later + 1, hosts.Queued);
    }

    [Fact]
    public async Task The_limit_of_one_address_does_not_hold_back_another()
    {
        for (var i = 0; i < 6; i++)
        {
            (await StartAsync(hosts.NewBrowser(), SignInHosts.NoGroupsPerson)).Dispose();
        }

        var queued = hosts.Queued;
        (await StartAsync(hosts.NewBrowser(), SignInHosts.Person)).Dispose();
        Assert.Equal(queued + 1, hosts.Queued);
    }

    private static async Task<HttpResponseMessage> StartAsync(Browser browser, string email)
    {
        HtmlForm form;
        using (var emailForm = await SignInFlow.OpenEmailFormAsync(browser, AppMe))
        {
            form = await HtmlForm.ReadAsync(emailForm);
        }

        return await form.SubmitAsync(browser, ("email", email));
    }

    private static string RequestId(HttpResponseMessage response)
    {
        var cookie = response.Headers.GetValues("Set-Cookie").Single(static cookie => cookie.StartsWith(SignInRequest.BindingCookie + "=", StringComparison.Ordinal));
        var value = cookie[(SignInRequest.BindingCookie.Length + 1)..];
        return value[..value.IndexOf('.', StringComparison.Ordinal)];
    }
}
