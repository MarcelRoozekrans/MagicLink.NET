using System.Net;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace MagicLink.Tests.SignIn;

// The sign-in page under conditions a person does not cause: damaged cookies, requests the store holds damaged,
// requests that are not forms, and parallel presses of the button. None of them may sign anyone in or fail with an error.
public sealed class SignInRobustnessTests(SignInHosts hosts) : IClassFixture<SignInHosts>
{
    private const string ValidPayload = "{\"return_url\":\"/\",\"binding_hash\":\"x\",\"code_hash\":\"y\",\"attempts\":0}";

    private static readonly Uri AppMe = new(SignInHosts.App, "me");

    [Theory]
    [InlineData("no-dot")]
    [InlineData(".secret")]
    [InlineData("id.")]
    [InlineData("not-a-uuid.secret")]
    [InlineData("8d0f3c4e-1b2a-4c5d-9e8f-0a1b2c3d4e5f.secret")]
    public async Task A_damaged_binding_cookie_finds_no_sign_in(string cookie)
    {
        var browser = hosts.NewBrowser();
        browser.SetCookie(SignInHosts.MagicLink, SignInRequest.BindingCookie, cookie);

        using var page = await browser.NavigateAsync(SignInFlow.CodePage);

        Assert.Equal(HttpStatusCode.Gone, page.StatusCode);
    }

    [Fact]
    public async Task A_binding_cookie_with_the_wrong_secret_finds_no_sign_in()
    {
        var browser = hosts.NewBrowser();
        var cookie = await CreateRequestAsync("/", browser: null);

        // The id of a real sign-in request, with a secret that is not its own.
        browser.SetCookie(SignInHosts.MagicLink, SignInRequest.BindingCookie, cookie[..cookie.IndexOf('.', StringComparison.Ordinal)] + ".wrong-secret");
        using var page = await browser.NavigateAsync(SignInFlow.CodePage);

        Assert.Equal(HttpStatusCode.Gone, page.StatusCode);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"return_url\":null,\"binding_hash\":\"x\",\"code_hash\":\"y\",\"attempts\":0}")]
    [InlineData("{\"return_url\":\"/\",\"binding_hash\":\"x\",\"code_hash\":\"y\",\"attempts\":\"0\"}")]
    [InlineData("{\"return_url\":\"/\",\"binding_hash\":\"x\",\"code_hash\":\"y\",\"attempts\":1.5}")]
    [InlineData("")]
    public async Task A_link_to_a_request_with_a_damaged_payload_no_longer_works(string payload)
    {
        var link = await StoreTokenAsync(SignInRequest.TokenType, SignInHosts.NormalizedPerson, hosts.Clock.GetUtcNow().AddMinutes(10), payload);

        using var page = await hosts.NewBrowser().NavigateAsync(link);

        Assert.Equal(HttpStatusCode.Gone, page.StatusCode);
    }

    [Fact]
    public async Task A_link_to_a_token_of_another_type_no_longer_works()
    {
        var link = await StoreTokenAsync(TokenTypeHints.AccessToken, SignInHosts.NormalizedPerson, hosts.Clock.GetUtcNow().AddMinutes(10), ValidPayload);

        using var page = await hosts.NewBrowser().NavigateAsync(link);

        Assert.Equal(HttpStatusCode.Gone, page.StatusCode);
    }

    [Fact]
    public async Task A_link_to_a_request_without_an_address_no_longer_works()
    {
        var link = await StoreTokenAsync(SignInRequest.TokenType, subject: null, hosts.Clock.GetUtcNow().AddMinutes(10), ValidPayload);

        using var page = await hosts.NewBrowser().NavigateAsync(link);

        Assert.Equal(HttpStatusCode.Gone, page.StatusCode);
    }

    [Fact]
    public async Task A_link_to_a_request_without_an_end_no_longer_works()
    {
        var link = await StoreTokenAsync(SignInRequest.TokenType, SignInHosts.NormalizedPerson, expiresAt: null, ValidPayload);

        using var page = await hosts.NewBrowser().NavigateAsync(link);

        Assert.Equal(HttpStatusCode.Gone, page.StatusCode);
    }

    [Fact]
    public async Task A_stored_return_url_off_this_site_signs_nobody_in()
    {
        var browser = hosts.NewBrowser();
        var code = await CreateRequestAsync("//evil.example/", browser);

        using var confirmed = await SignInFlow.TypeCodeAsync(browser, code);

        Assert.Equal(HttpStatusCode.BadRequest, confirmed.StatusCode);
        Assert.False(confirmed.Headers.TryGetValues("Set-Cookie", out var cookies) && cookies.Any(static cookie => cookie.StartsWith(MagicLinkSession.CookieName + "=", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("/signin")]
    [InlineData("/signin/code")]
    [InlineData("/signin/link")]
    public async Task A_post_that_is_not_a_form_is_refused(string path)
    {
        var (browser, _) = await StartAsync();

        using var response = await browser.SendAsync(new HttpRequestMessage(HttpMethod.Post, new Uri(SignInHosts.MagicLink, path))
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json"),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Posting_an_unknown_link_token_signs_nobody_in()
    {
        var (browser, _) = await StartAsync();
        HtmlForm form;
        using (var page = await SignInFlow.OpenEmailFormAsync(browser, AppMe))
        {
            form = await HtmlForm.ReadAsync(page);
        }

        using var response = await new HtmlForm(new Uri(SignInHosts.MagicLink, "/signin/link"), form.Hidden).SubmitAsync(browser, ("token", "unknown"));

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
    }

    [Fact]
    public async Task Pressing_the_button_twice_at_once_signs_in_once()
    {
        var (browser, message) = await StartAsync();
        HtmlForm button;
        using (var page = await browser.NavigateAsync(message.Link))
        {
            button = await HtmlForm.ReadAsync(page);
        }

        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => button.SubmitAsync(browser)));
        var statuses = responses.Select(static response => response.StatusCode).ToList();
        foreach (var response in responses)
        {
            response.Dispose();
        }

        Assert.Single(statuses, HttpStatusCode.Found);
        Assert.All(statuses, static status => Assert.Contains(status, new[] { HttpStatusCode.Found, HttpStatusCode.Gone }));
    }

    [Fact]
    public async Task The_right_code_on_the_last_attempt_still_signs_in()
    {
        var (browser, message) = await StartAsync();
        HtmlForm form;
        using (var page = await browser.NavigateAsync(SignInFlow.CodePage))
        {
            form = await HtmlForm.ReadAsync(page);
        }

        var wrong = message.Code.StartsWith('A') ? "B" + message.Code[1..] : "A" + message.Code[1..];
        for (var attempt = 1; attempt <= 4; attempt++)
        {
            (await form.SubmitAsync(browser, ("code", wrong))).Dispose();
        }

        using var confirmed = await form.SubmitAsync(browser, ("code", message.Code));

        Assert.Equal(HttpStatusCode.Found, confirmed.StatusCode);
    }

    private async Task<(Browser Browser, SignInMessage Message)> StartAsync()
    {
        var browser = hosts.NewBrowser();
        using var emailForm = await SignInFlow.OpenEmailFormAsync(browser, AppMe);
        return (browser, await hosts.StartAsync(browser, emailForm));
    }

    /// <summary>
    /// Starts a sign-in request straight in the store, returning to <paramref name="returnUrl"/>, and binds it to
    /// <paramref name="browser"/>. Returns the code when bound to a browser, else the binding cookie.
    /// </summary>
    private async Task<string> CreateRequestAsync(string returnUrl, Browser? browser)
    {
        await using var scope = hosts.MagicLinkServices.CreateAsyncScope();
        var tokens = scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>();
        var (_, cookie, _, code) = await SignInRequest.CreateAsync(
            tokens, SignInHosts.NormalizedPerson, returnUrl, hosts.Clock.GetUtcNow(), TimeSpan.FromMinutes(10), TestContext.Current.CancellationToken);
        if (browser is null)
        {
            return cookie;
        }

        browser.SetCookie(SignInHosts.MagicLink, SignInRequest.BindingCookie, cookie);
        return code;
    }

    /// <summary>Puts a token in the store as it is, and returns the sign-in link that names it.</summary>
    private async Task<Uri> StoreTokenAsync(string type, string? subject, DateTimeOffset? expiresAt, string payload)
    {
        var reference = Guid.NewGuid().ToString("N");
        await using var scope = hosts.MagicLinkServices.CreateAsyncScope();
        var tokens = scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>();
        await tokens.CreateAsync(
            new OpenIddictTokenDescriptor
            {
                Type = type,
                Status = Statuses.Valid,
                Subject = subject,
                CreationDate = hosts.Clock.GetUtcNow(),
                ExpirationDate = expiresAt,
                ReferenceId = reference,
                Payload = payload.Length == 0 ? null : payload,
            },
            TestContext.Current.CancellationToken);
        return new Uri(SignInHosts.MagicLink, "/signin/link?token=" + reference);
    }
}
