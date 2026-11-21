using System.Net;

namespace MagicLink.Tests.SignIn;

/// <summary>What a person does on the sign-in page, step by step.</summary>
internal static class SignInFlow
{
    public static readonly Uri CodePage = new(SignInHosts.MagicLink, "/signin/code");

    /// <summary>
    /// Opens <paramref name="address"/> and follows redirects. When that lands on the sign-in page, the person types
    /// their address and then the code from the email, and the browser goes on to where the sign-in started.
    /// </summary>
    public static async Task<HttpResponseMessage> SignInAsync(this SignInHosts hosts, Browser browser, Uri address, string? stopAtHost = null)
    {
        var response = await browser.NavigateAsync(address, stopAtHost);
        if (!IsEmailForm(browser, response))
        {
            return response;
        }

        SignInMessage message;
        using (response)
        {
            message = await hosts.StartAsync(browser, response);
        }

        using var confirmed = await TypeCodeAsync(browser, message.Code);
        return await ContinueAsync(browser, confirmed, stopAtHost);
    }

    /// <summary>Follows the redirect a confirmed sign-in answers with, back to where the sign-in started.</summary>
    public static async Task<HttpResponseMessage> ContinueAsync(Browser browser, HttpResponseMessage confirmed, string? stopAtHost = null)
    {
        Assert.Equal(HttpStatusCode.Redirect, confirmed.StatusCode);
        return await browser.NavigateAsync(new Uri(SignInHosts.MagicLink, confirmed.Headers.Location!), stopAtHost);
    }

    /// <summary>Opens <paramref name="address"/>, which must land on the email form, and returns that page.</summary>
    public static async Task<HttpResponseMessage> OpenEmailFormAsync(Browser browser, Uri address)
    {
        var response = await browser.NavigateAsync(address);
        Assert.True(IsEmailForm(browser, response), "Expected the sign-in page.");
        return response;
    }

    /// <summary>Types <paramref name="email"/> on <paramref name="emailForm"/> and returns the email that is sent.</summary>
    public static async Task<SignInMessage> StartAsync(this SignInHosts hosts, Browser browser, HttpResponseMessage emailForm, string email = SignInHosts.Person)
    {
        var form = await HtmlForm.ReadAsync(emailForm);
        var queued = hosts.Queued;

        using var response = await form.SubmitAsync(browser, ("email", email));

        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        Assert.Equal("/signin/code", response.Headers.Location!.OriginalString);
        Assert.Equal(queued + 1, hosts.Queued);

        // Each test waits for its own email, so every email queued before this one has arrived already.
        return await hosts.Mailbox.WaitForAsync(queued);
    }

    /// <summary>Opens the code page and types <paramref name="code"/>; returns the answer to that form.</summary>
    public static async Task<HttpResponseMessage> TypeCodeAsync(Browser browser, string code)
    {
        using var page = await browser.NavigateAsync(CodePage);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var form = await HtmlForm.ReadAsync(page);
        return await form.SubmitAsync(browser, ("code", code));
    }

    /// <summary>Opens the link from the email and presses the button on its page; returns the answer to that form.</summary>
    public static async Task<HttpResponseMessage> PressButtonAsync(Browser browser, Uri link)
    {
        using var page = await browser.NavigateAsync(link);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var form = await HtmlForm.ReadAsync(page);
        return await form.SubmitAsync(browser);
    }

    private static bool IsEmailForm(Browser browser, HttpResponseMessage response) =>
        response.StatusCode == HttpStatusCode.OK && string.Equals(browser.Visited[^1].AbsolutePath, "/signin", StringComparison.Ordinal);
}
