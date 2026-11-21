using System.Net;
using Microsoft.AspNetCore.WebUtilities;

namespace MagicLink.Tests.SignIn;

// docs/specs/magiclink-net-confirm-sign-in-with-a-link-or-a-code.md, criteria 1, 2, 3, 5 and 6 (issue #4), and
// ADR-249: the link signs in only in the browser that started the sign-in.
public sealed class SignInPageTests(SignInHosts hosts) : IClassFixture<SignInHosts>
{
    private static readonly Uri AppMe = new(SignInHosts.App, "me");

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Opening_the_link_never_signs_anyone_in()
    {
        var (browser, message) = await StartAsync();

        using var page = await browser.NavigateAsync(message.Link);

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.False(StartsSession(page));
        var html = await page.Content.ReadAsStringAsync(Cancel);
        Assert.Contains("Sign in as " + SignInHosts.NormalizedPerson, html, StringComparison.Ordinal);
        Assert.True(await HasNoSessionAsync(browser));
    }

    [Fact]
    public async Task Pressing_the_button_in_the_browser_that_started_signs_in_and_returns()
    {
        var (browser, message) = await StartAsync();

        using var confirmed = await SignInFlow.PressButtonAsync(browser, message.Link);

        Assert.True(StartsSession(confirmed));
        await AssertSignedInAtAppAsync(browser, confirmed);
    }

    [Fact]
    public async Task Typing_the_code_in_the_browser_that_started_signs_in_and_returns()
    {
        var (browser, message) = await StartAsync();

        using var confirmed = await SignInFlow.TypeCodeAsync(browser, message.Code);

        Assert.True(StartsSession(confirmed));
        await AssertSignedInAtAppAsync(browser, confirmed);
    }

    [Fact]
    public async Task The_code_may_be_typed_in_lower_case_and_without_the_dash()
    {
        var (browser, message) = await StartAsync();

        using var confirmed = await SignInFlow.TypeCodeAsync(browser, " " + message.Code.Replace("-", " ", StringComparison.Ordinal).ToLowerInvariant() + " ");

        Assert.True(StartsSession(confirmed));
    }

    [Fact]
    public async Task The_link_in_another_browser_points_to_the_code_and_never_signs_in()
    {
        var (browser, message) = await StartAsync();
        var other = hosts.NewBrowser();

        using var page = await other.NavigateAsync(message.Link);

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var html = await page.Content.ReadAsStringAsync(Cancel);
        Assert.Contains("type the code", html, StringComparison.Ordinal);
        Assert.DoesNotContain(SignInHosts.NormalizedPerson, html, StringComparison.Ordinal);
        Assert.False(HtmlForm.IsIn(html));
        Assert.True(await HasNoSessionAsync(other));

        // The code still works where the sign-in started.
        using var confirmed = await SignInFlow.TypeCodeAsync(browser, message.Code);
        Assert.True(StartsSession(confirmed));
    }

    [Fact]
    public async Task Posting_the_link_from_another_browser_never_signs_in()
    {
        var (browser, message) = await StartAsync();
        var other = hosts.NewBrowser();
        var form = await AnyFormOfAsync(other);
        var token = QueryHelpers.ParseQuery(message.Link.Query)["token"].ToString();

        using var response = await new HtmlForm(new Uri(SignInHosts.MagicLink, "/signin/link"), form.Hidden).SubmitAsync(other, ("token", token));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(StartsSession(response));
        Assert.Contains("type the code", await response.Content.ReadAsStringAsync(Cancel), StringComparison.Ordinal);
        Assert.True(await HasNoSessionAsync(other));

        // The link still works where the sign-in started.
        using var confirmed = await SignInFlow.PressButtonAsync(browser, message.Link);
        Assert.True(StartsSession(confirmed));
    }

    [Fact]
    public async Task The_code_typed_in_another_browser_never_signs_in()
    {
        var (browser, message) = await StartAsync();
        var other = hosts.NewBrowser();
        var form = await AnyFormOfAsync(other);

        using var response = await new HtmlForm(SignInFlow.CodePage, form.Hidden).SubmitAsync(other, ("code", message.Code));

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
        Assert.False(StartsSession(response));

        using var confirmed = await SignInFlow.TypeCodeAsync(browser, message.Code);
        Assert.True(StartsSession(confirmed));
    }

    [Fact]
    public async Task Once_the_code_is_used_neither_link_nor_code_works()
    {
        var (browser, message) = await StartAsync();
        var form = await CodeFormAsync(browser);
        (await form.SubmitAsync(browser, ("code", message.Code))).Dispose();

        using var again = await form.SubmitAsync(browser, ("code", message.Code));
        using var link = await browser.NavigateAsync(message.Link);

        Assert.Equal(HttpStatusCode.Gone, again.StatusCode);
        Assert.Equal(HttpStatusCode.Gone, link.StatusCode);
    }

    [Fact]
    public async Task Once_the_link_is_used_neither_link_nor_code_works()
    {
        var (browser, message) = await StartAsync();
        var codeForm = await CodeFormAsync(browser);
        using var page = await browser.NavigateAsync(message.Link);
        var button = await HtmlForm.ReadAsync(page);
        (await button.SubmitAsync(browser)).Dispose();

        using var again = await button.SubmitAsync(browser);
        using var code = await codeForm.SubmitAsync(browser, ("code", message.Code));

        Assert.Equal(HttpStatusCode.Gone, again.StatusCode);
        Assert.Equal(HttpStatusCode.Gone, code.StatusCode);
    }

    [Fact]
    public async Task A_code_allows_five_attempts()
    {
        var (browser, message) = await StartAsync();
        var form = await CodeFormAsync(browser);
        var wrong = WrongCode(message.Code);

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            using var response = await form.SubmitAsync(browser, ("code", wrong));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("not right", await response.Content.ReadAsStringAsync(Cancel), StringComparison.Ordinal);
        }

        using var fifth = await form.SubmitAsync(browser, ("code", wrong));
        using var right = await form.SubmitAsync(browser, ("code", message.Code));
        using var link = await browser.NavigateAsync(message.Link);

        Assert.Equal(HttpStatusCode.Gone, fifth.StatusCode);
        Assert.Equal(HttpStatusCode.Gone, right.StatusCode);
        Assert.False(StartsSession(right));
        Assert.Equal(HttpStatusCode.Gone, link.StatusCode);
    }

    [Fact]
    public async Task Parallel_guesses_cannot_exceed_five_attempts()
    {
        var (browser, message) = await StartAsync();
        var form = await CodeFormAsync(browser);
        var wrong = WrongCode(message.Code);

        var responses = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => form.SubmitAsync(browser, ("code", wrong))));
        using var right = await form.SubmitAsync(browser, ("code", message.Code));

        // Each attempt is counted in the store before its code is compared; a guess that collides with another is
        // neither counted nor compared and is told to try again. So at most 4 guesses come back "wrong", a 5th ends
        // the request, and the right code works afterwards only while fewer than 5 were compared.
        var statuses = responses.Select(static response => response.StatusCode).ToList();
        foreach (var response in responses)
        {
            response.Dispose();
        }

        var wrongCount = statuses.Count(static status => status == HttpStatusCode.BadRequest);
        var ended = statuses.Contains(HttpStatusCode.Gone);
        Assert.All(statuses, static status => Assert.Contains(status, new[] { HttpStatusCode.BadRequest, HttpStatusCode.Conflict, HttpStatusCode.Gone }));
        Assert.InRange(wrongCount, 0, 4);
        Assert.Equal(ended ? HttpStatusCode.Gone : HttpStatusCode.Found, right.StatusCode);
    }

    [Fact]
    public async Task A_wrong_code_still_lets_the_right_one_sign_in()
    {
        var (browser, message) = await StartAsync();
        var form = await CodeFormAsync(browser);
        (await form.SubmitAsync(browser, ("code", WrongCode(message.Code)))).Dispose();

        using var confirmed = await form.SubmitAsync(browser, ("code", message.Code));

        Assert.True(StartsSession(confirmed));
    }

    [Fact]
    public async Task A_new_sign_in_in_the_same_browser_replaces_the_earlier_one()
    {
        var (browser, first) = await StartAsync();
        SignInMessage second;
        using (var emailForm = await SignInFlow.OpenEmailFormAsync(browser, AppMe))
        {
            second = await hosts.StartAsync(browser, emailForm);
        }

        using var oldLink = await browser.NavigateAsync(first.Link);
        Assert.Equal(HttpStatusCode.OK, oldLink.StatusCode);
        Assert.False(HtmlForm.IsIn(await oldLink.Content.ReadAsStringAsync(Cancel)));

        using var confirmed = await SignInFlow.TypeCodeAsync(browser, second.Code);
        Assert.True(StartsSession(confirmed));
    }

    [Fact]
    public async Task The_email_holds_a_link_on_the_issuer_and_a_code_valid_for_ten_minutes()
    {
        var before = hosts.Clock.GetUtcNow();

        var (_, message) = await StartAsync();

        Assert.Equal(SignInHosts.NormalizedPerson, message.Email);
        Assert.Equal(SignInHosts.MagicLink.Host, message.Link.Host);
        Assert.Equal("/signin/link", message.Link.AbsolutePath);
        Assert.Matches("^[A-Z0-9]{4}-[A-Z0-9]{4}$", message.Code);
        Assert.InRange(message.ExpiresAt, before + TimeSpan.FromMinutes(10), hosts.Clock.GetUtcNow() + TimeSpan.FromMinutes(10));
    }

    [Fact]
    public async Task A_forged_host_header_does_not_change_the_link()
    {
        var browser = hosts.NewBrowser(alias: "evil.example");
        var evil = new Uri("https://evil.example/");
        HtmlForm form;
        using (var emailForm = await browser.NavigateAsync(new Uri(evil, "/signin?ReturnUrl=%2F")))
        {
            Assert.Equal(HttpStatusCode.OK, emailForm.StatusCode);
            form = await HtmlForm.ReadAsync(emailForm);
        }

        var queued = hosts.Queued;
        using var response = await new HtmlForm(new Uri(evil, form.Action.PathAndQuery), form.Hidden).SubmitAsync(browser, ("email", SignInHosts.Person));

        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        Assert.Equal(SignInHosts.MagicLink.Host, (await hosts.Mailbox.WaitForAsync(queued)).Link.Host);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not an address")]
    [InlineData("Person <person@example.com>")]
    public async Task An_invalid_address_is_asked_for_again_and_gets_no_email(string email)
    {
        var browser = hosts.NewBrowser();
        HtmlForm form;
        using (var emailForm = await SignInFlow.OpenEmailFormAsync(browser, AppMe))
        {
            form = await HtmlForm.ReadAsync(emailForm);
        }

        var queued = hosts.Queued;
        using var response = await form.SubmitAsync(browser, ("email", email));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("valid email address", await response.Content.ReadAsStringAsync(Cancel), StringComparison.Ordinal);
        Assert.Equal(queued, hosts.Queued);
    }

    [Theory]
    [InlineData("/signin")]
    [InlineData("/signin/code")]
    [InlineData("/signin/link")]
    public async Task A_form_without_its_antiforgery_token_is_refused(string path)
    {
        var (browser, message) = await StartAsync();
        var token = QueryHelpers.ParseQuery(message.Link.Query)["token"].ToString();

        using var response = await browser.SendAsync(new HttpRequestMessage(HttpMethod.Post, new Uri(SignInHosts.MagicLink, path))
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ReturnUrl"] = "/",
                ["email"] = SignInHosts.Person,
                ["code"] = message.Code,
                ["token"] = token,
            }),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(StartsSession(response));
    }

    [Fact]
    public async Task A_posted_return_url_must_stay_on_this_site()
    {
        var browser = hosts.NewBrowser();
        HtmlForm form;
        using (var emailForm = await SignInFlow.OpenEmailFormAsync(browser, AppMe))
        {
            form = await HtmlForm.ReadAsync(emailForm);
        }

        var hidden = form.Hidden.Select(static field => string.Equals(field.Key, "ReturnUrl", StringComparison.Ordinal) ? KeyValuePair.Create(field.Key, "//evil.example/") : field).ToList();
        var queued = hosts.Queued;
        using var response = await new HtmlForm(form.Action, hidden).SubmitAsync(browser, ("email", SignInHosts.Person));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(queued, hosts.Queued);
    }

    [Fact]
    public async Task The_code_page_without_a_sign_in_in_this_browser_says_it_no_longer_works()
    {
        var browser = hosts.NewBrowser();

        using var response = await browser.NavigateAsync(SignInFlow.CodePage);

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("?token=unknown")]
    public async Task An_unknown_link_says_it_no_longer_works(string query)
    {
        var browser = hosts.NewBrowser();

        using var response = await browser.NavigateAsync(new Uri(SignInHosts.MagicLink, "/signin/link" + query));

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
    }

    [Fact]
    public async Task Sign_in_pages_are_not_cached_framed_or_leaking_the_link()
    {
        var (browser, message) = await StartAsync();

        using var page = await browser.NavigateAsync(message.Link);

        Assert.Contains("no-store", page.Headers.CacheControl!.ToString(), StringComparison.Ordinal);
        Assert.Equal("DENY", Assert.Single(page.Headers.GetValues("X-Frame-Options")));
        Assert.Equal("no-referrer", Assert.Single(page.Headers.GetValues("Referrer-Policy")));
        var policy = Assert.Single(page.Headers.GetValues("Content-Security-Policy"));
        Assert.Contains("default-src 'none'", policy, StringComparison.Ordinal);
        Assert.Contains("frame-ancestors 'none'", policy, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_binding_cookie_is_secure_http_only_and_lax()
    {
        var browser = hosts.NewBrowser();
        HtmlForm form;
        using (var emailForm = await SignInFlow.OpenEmailFormAsync(browser, AppMe))
        {
            form = await HtmlForm.ReadAsync(emailForm);
        }

        var queued = hosts.Queued;
        using var response = await form.SubmitAsync(browser, ("email", SignInHosts.Person));

        // The next test expects every earlier email to have arrived, so this one is waited for too.
        await hosts.Mailbox.WaitForAsync(queued);
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), static value => value.StartsWith("__Host-MagicLink.SignIn=", StringComparison.Ordinal));
        Assert.Contains("; secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("; httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("; samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("; path=/", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", cookie, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A fresh browser that opened the application, landed on the sign-in page and typed its address.</summary>
    private async Task<(Browser Browser, SignInMessage Message)> StartAsync()
    {
        var browser = hosts.NewBrowser();
        using var emailForm = await SignInFlow.OpenEmailFormAsync(browser, AppMe);
        return (browser, await hosts.StartAsync(browser, emailForm));
    }

    private static async Task<HtmlForm> CodeFormAsync(Browser browser)
    {
        using var page = await browser.NavigateAsync(SignInFlow.CodePage);
        return await HtmlForm.ReadAsync(page);
    }

    /// <summary>A form of <paramref name="browser"/>'s own, for its antiforgery token: the email form.</summary>
    private static async Task<HtmlForm> AnyFormOfAsync(Browser browser)
    {
        using var page = await SignInFlow.OpenEmailFormAsync(browser, AppMe);
        return await HtmlForm.ReadAsync(page);
    }

    /// <summary>Follows a confirmed sign-in back through the application and checks it now knows the person.</summary>
    private static async Task AssertSignedInAtAppAsync(Browser browser, HttpResponseMessage confirmed)
    {
        Assert.StartsWith("/connect/authorize?", confirmed.Headers.Location!.OriginalString, StringComparison.Ordinal);
        using var me = await SignInFlow.ContinueAsync(browser, confirmed);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Contains(SignInHosts.NormalizedPerson, await me.Content.ReadAsStringAsync(Cancel), StringComparison.Ordinal);
    }

    /// <summary>True when the browser still lands on the sign-in page when it opens the application.</summary>
    private static async Task<bool> HasNoSessionAsync(Browser browser)
    {
        using var response = await browser.NavigateAsync(AppMe);
        return string.Equals(browser.Visited[^1].AbsolutePath, "/signin", StringComparison.Ordinal);
    }

    private static bool StartsSession(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var cookies)
        && cookies.Any(static cookie => cookie.StartsWith(MagicLinkSession.CookieName + "=", StringComparison.Ordinal));

    private static string WrongCode(string code) => code.StartsWith('A') ? "B" + code[1..] : "A" + code[1..];
}
