using System.Net;

namespace MagicLink.Tests.SignIn;

// docs/specs/magiclink-net-confirm-sign-in-with-a-link-or-a-code.md, criterion 3: a code is valid for 10 minutes.
// A class of its own, so moving MagicLink.NET's clock forward does not upset the other sign-in tests' hosts.
public sealed class SignInExpiryTests(SignInHosts hosts) : IClassFixture<SignInHosts>
{
    private static readonly Uri AppMe = new(SignInHosts.App, "me");

    [Fact]
    public async Task After_ten_minutes_neither_link_nor_code_works()
    {
        var browser = hosts.NewBrowser();
        SignInMessage message;
        using (var emailForm = await SignInFlow.OpenEmailFormAsync(browser, AppMe))
        {
            message = await hosts.StartAsync(browser, emailForm);
        }

        HtmlForm codeForm;
        using (var page = await browser.NavigateAsync(SignInFlow.CodePage))
        {
            codeForm = await HtmlForm.ReadAsync(page);
        }

        hosts.Clock.Advance(TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(1));

        using var code = await codeForm.SubmitAsync(browser, ("code", message.Code));
        using var link = await browser.NavigateAsync(message.Link);

        Assert.Equal(HttpStatusCode.Gone, code.StatusCode);
        Assert.Equal(HttpStatusCode.Gone, link.StatusCode);
    }
}
