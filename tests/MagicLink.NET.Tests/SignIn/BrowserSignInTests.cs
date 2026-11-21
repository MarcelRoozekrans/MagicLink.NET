using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace MagicLink.Tests.SignIn;

// docs/specs/magiclink-net-run-an-oidc-provider-for-applications-and-clis.md, criterion 1: an application that speaks
// OIDC can sign a person in through MagicLink.NET in the browser (issue #3).
public sealed class BrowserSignInTests(SignInHosts hosts) : IClassFixture<SignInHosts>
{
    private static readonly Uri AppMe = new(SignInHosts.App, "me");

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_application_signs_a_person_in_through_the_browser()
    {
        var browser = hosts.NewBrowser();

        using var response = await browser.NavigateAsync(AppMe);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(browser.Visited, static uri => string.Equals(uri.Host, SignInHosts.MagicLink.Host, StringComparison.Ordinal) && string.Equals(uri.AbsolutePath, "/signin", StringComparison.Ordinal));
        using var me = await ReadJson(response);
        Assert.Equal(SignInHosts.NormalizedPerson, me.RootElement.GetProperty("sub").GetString());
        Assert.Equal(SignInHosts.NormalizedPerson, me.RootElement.GetProperty("email").GetString());
        Assert.Equal("true", me.RootElement.GetProperty("email_verified").GetString());

        // Access tokens are signed JWTs, not encrypted ones, so an application's API can check them with the key set.
        using var accessToken = Payload(me.RootElement.GetProperty("access_token").GetString()!);
        Assert.Equal(SignInHosts.NormalizedPerson, accessToken.RootElement.GetProperty("sub").GetString());
        Assert.Equal(SignInHosts.MagicLink.ToString(), accessToken.RootElement.GetProperty("iss").GetString());

        using var idToken = Payload(me.RootElement.GetProperty("id_token").GetString()!);
        Assert.Equal(SignInHosts.ClientId, idToken.RootElement.GetProperty("aud").GetString());
        Assert.True(idToken.RootElement.TryGetProperty("auth_time", out _));
    }

    [Fact]
    public async Task A_person_with_a_session_signs_in_to_an_application_without_signing_in_again()
    {
        var browser = hosts.NewBrowser();
        (await browser.NavigateAsync(AppMe)).Dispose();
        browser.ForgetCookiesOf(SignInHosts.App);
        var visits = hosts.SignInPageVisits;

        using var response = await browser.NavigateAsync(AppMe);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(visits, hosts.SignInPageVisits);
    }

    [Fact]
    public async Task The_session_cookie_is_secure_http_only_and_lax()
    {
        var browser = hosts.NewBrowser();
        var (_, _, challenge) = NewAuthorization();

        using var signInPage = await browser.NavigateAsync(new Uri(SignInHosts.MagicLink, "/signin?ReturnUrl=" + Uri.EscapeDataString(challenge)), stopAtHost: SignInHosts.MagicLink.Host);

        var cookie = Assert.Single(signInPage.Headers.GetValues("Set-Cookie"), static value => value.StartsWith(MagicLinkSession.CookieName + "=", StringComparison.Ordinal));
        Assert.Contains("; secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("; httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("; samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("; path=/", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Without_a_session_the_authorization_request_goes_to_the_sign_in_page_and_back()
    {
        var browser = hosts.NewBrowser();
        var (authorize, _, _) = NewAuthorization();

        using var response = await browser.SendAsync(new HttpRequestMessage(HttpMethod.Get, authorize));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var signIn = new Uri(authorize, response.Headers.Location!);
        Assert.Equal("/signin", signIn.AbsolutePath);
        var returnUrl = QueryHelpers.ParseQuery(signIn.Query)["ReturnUrl"].ToString();
        Assert.Equal(authorize.PathAndQuery, returnUrl);
    }

    [Fact]
    public async Task Code_flow_issues_tokens_and_user_info_for_the_signed_in_person()
    {
        var browser = hosts.NewBrowser();
        var (authorize, verifier, _) = NewAuthorization();

        var code = await CodeFrom(await browser.NavigateAsync(authorize, stopAtHost: SignInHosts.App.Host));
        using var tokens = await RedeemAsync(browser, code, verifier);

        using var idToken = Payload(tokens.RootElement.GetProperty("id_token").GetString()!);
        Assert.Equal(SignInHosts.NormalizedPerson, idToken.RootElement.GetProperty("sub").GetString());
        Assert.Equal(SignInHosts.NormalizedPerson, idToken.RootElement.GetProperty("email").GetString());
        Assert.True(idToken.RootElement.GetProperty("email_verified").GetBoolean());

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(SignInHosts.MagicLink, "/connect/userinfo"));
        request.Headers.Authorization = new("Bearer", tokens.RootElement.GetProperty("access_token").GetString());
        using var userInfo = await browser.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, userInfo.StatusCode);
        using var claims = await ReadJson(userInfo);
        Assert.Equal(SignInHosts.NormalizedPerson, claims.RootElement.GetProperty("sub").GetString());
        Assert.Equal(SignInHosts.NormalizedPerson, claims.RootElement.GetProperty("email").GetString());
        Assert.True(claims.RootElement.GetProperty("email_verified").GetBoolean());
    }

    [Fact]
    public async Task Without_the_email_scope_the_tokens_carry_no_email_address()
    {
        var browser = hosts.NewBrowser();
        var (authorize, verifier, _) = NewAuthorization(scope: "openid");

        var code = await CodeFrom(await browser.NavigateAsync(authorize, stopAtHost: SignInHosts.App.Host));
        using var tokens = await RedeemAsync(browser, code, verifier);

        using var idToken = Payload(tokens.RootElement.GetProperty("id_token").GetString()!);
        Assert.Equal(SignInHosts.NormalizedPerson, idToken.RootElement.GetProperty("sub").GetString());
        Assert.False(idToken.RootElement.TryGetProperty("email", out _));
        using var accessToken = Payload(tokens.RootElement.GetProperty("access_token").GetString()!);
        Assert.False(accessToken.RootElement.TryGetProperty("email", out _));
    }

    [Fact]
    public async Task A_code_works_only_once()
    {
        var browser = hosts.NewBrowser();
        var (authorize, verifier, _) = NewAuthorization();
        var code = await CodeFrom(await browser.NavigateAsync(authorize, stopAtHost: SignInHosts.App.Host));
        (await RedeemAsync(browser, code, verifier)).Dispose();

        using var second = await PostTokenAsync(browser, code, verifier);

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        using var error = await ReadJson(second);
        Assert.Equal("invalid_grant", error.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task A_code_needs_the_matching_pkce_verifier()
    {
        var browser = hosts.NewBrowser();
        var (authorize, _, _) = NewAuthorization();
        var code = await CodeFrom(await browser.NavigateAsync(authorize, stopAtHost: SignInHosts.App.Host));

        using var response = await PostTokenAsync(browser, code, NewAuthorization().Verifier);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Prompt_none_without_a_session_returns_login_required_to_the_application()
    {
        var browser = hosts.NewBrowser();
        var (authorize, _, _) = NewAuthorization(extra: "&prompt=none");

        using var response = await browser.NavigateAsync(authorize, stopAtHost: SignInHosts.App.Host);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var callback = response.Headers.Location!;
        Assert.Equal(SignInHosts.AppCallback.AbsolutePath, callback.AbsolutePath);
        Assert.Equal("login_required", QueryHelpers.ParseQuery(callback.Query)["error"].ToString());
        Assert.DoesNotContain(browser.Visited, static uri => string.Equals(uri.AbsolutePath, "/signin", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Prompt_none_with_a_session_returns_a_code()
    {
        var browser = hosts.NewBrowser();
        (await browser.NavigateAsync(AppMe)).Dispose();
        var (authorize, _, _) = NewAuthorization(extra: "&prompt=none");

        await CodeFrom(await browser.NavigateAsync(authorize, stopAtHost: SignInHosts.App.Host));
    }

    [Theory]
    [InlineData("&prompt=login")]
    [InlineData("&max_age=0")]
    public async Task An_application_can_ask_a_signed_in_person_to_sign_in_again(string extra)
    {
        var browser = hosts.NewBrowser();
        (await browser.NavigateAsync(AppMe)).Dispose();
        var (authorize, _, _) = NewAuthorization(extra: extra);

        using var first = await browser.SendAsync(new HttpRequestMessage(HttpMethod.Get, authorize));
        Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);
        var signIn = new Uri(authorize, first.Headers.Location!);
        Assert.Equal("/signin", signIn.AbsolutePath);

        // Back from the sign-in page, the request no longer asks for it, so the new session is accepted.
        var returnUrl = QueryHelpers.ParseQuery(signIn.Query)["ReturnUrl"].ToString();
        Assert.DoesNotContain(extra[1..], returnUrl, StringComparison.Ordinal);
        Assert.Contains("code_challenge=", returnUrl, StringComparison.Ordinal);
        await CodeFrom(await browser.NavigateAsync(signIn, stopAtHost: SignInHosts.App.Host));
    }

    [Fact]
    public async Task Other_prompt_values_survive_the_way_back_from_the_sign_in_page()
    {
        var browser = hosts.NewBrowser();
        var (authorize, _, _) = NewAuthorization(extra: "&prompt=login%20consent");

        using var response = await browser.SendAsync(new HttpRequestMessage(HttpMethod.Get, authorize));

        var signIn = new Uri(authorize, response.Headers.Location!);
        var returnUrl = QueryHelpers.ParseQuery(signIn.Query)["ReturnUrl"].ToString();
        Assert.Equal("consent", QueryHelpers.ParseQuery(new Uri(SignInHosts.MagicLink, returnUrl).Query)["prompt"].ToString());
    }

    [Fact]
    public async Task A_recent_session_meets_max_age()
    {
        var browser = hosts.NewBrowser();
        (await browser.NavigateAsync(AppMe)).Dispose();
        var visits = hosts.SignInPageVisits;
        var (authorize, _, _) = NewAuthorization(extra: "&max_age=3600");

        await CodeFrom(await browser.NavigateAsync(authorize, stopAtHost: SignInHosts.App.Host));

        Assert.Equal(visits, hosts.SignInPageVisits);
    }

    [Fact]
    public async Task An_authorization_request_can_be_posted()
    {
        var browser = hosts.NewBrowser();
        var (authorize, _, _) = NewAuthorization();
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(SignInHosts.MagicLink, "/connect/authorize"))
        {
            Content = new FormUrlEncodedContent(QueryHelpers.ParseQuery(authorize.Query).Select(static pair => KeyValuePair.Create(pair.Key, pair.Value.ToString()))),
        };

        using var response = await browser.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var signIn = new Uri(authorize, response.Headers.Location!);
        var returnUrl = QueryHelpers.ParseQuery(signIn.Query)["ReturnUrl"].ToString();
        await CodeFrom(await browser.NavigateAsync(new Uri(SignInHosts.MagicLink, returnUrl), stopAtHost: SignInHosts.App.Host));
    }

    [Fact]
    public async Task Discovery_announces_the_code_flow_with_pkce_and_user_info()
    {
        var browser = hosts.NewBrowser();

        using var response = await browser.SendAsync(new HttpRequestMessage(HttpMethod.Get, new Uri(SignInHosts.MagicLink, "/.well-known/openid-configuration")));

        using var document = await ReadJson(response);
        var root = document.RootElement;
        Assert.Equal("https://magiclink.test/connect/authorize", root.GetProperty("authorization_endpoint").GetString());
        Assert.Equal("https://magiclink.test/connect/token", root.GetProperty("token_endpoint").GetString());
        Assert.Equal("https://magiclink.test/connect/userinfo", root.GetProperty("userinfo_endpoint").GetString());
        Assert.Contains("S256", Strings(root.GetProperty("code_challenge_methods_supported")), StringComparer.Ordinal);
        Assert.Contains("email", Strings(root.GetProperty("scopes_supported")), StringComparer.Ordinal);
        Assert.Equal(["authorization_code"], Strings(root.GetProperty("grant_types_supported")));
    }

    private static (Uri Authorize, string Verifier, string Challenge) NewAuthorization(string scope = "openid email", string extra = "")
    {
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var query = QueryHelpers.AddQueryString("/connect/authorize", new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["client_id"] = SignInHosts.ClientId,
            ["redirect_uri"] = SignInHosts.AppCallback.ToString(),
            ["response_type"] = "code",
            ["scope"] = scope,
            ["state"] = Guid.NewGuid().ToString("N"),
            ["nonce"] = Guid.NewGuid().ToString("N"),
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256",
        });
        return (new Uri(SignInHosts.MagicLink, query + extra), verifier, query);
    }

    private static Task<string> CodeFrom(HttpResponseMessage response)
    {
        using (response)
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var callback = response.Headers.Location!;
            Assert.Equal(SignInHosts.AppCallback.GetLeftPart(UriPartial.Path), callback.GetLeftPart(UriPartial.Path));
            var code = QueryHelpers.ParseQuery(callback.Query)["code"].ToString();
            Assert.NotEmpty(code);
            return Task.FromResult(code);
        }
    }

    private static async Task<JsonDocument> RedeemAsync(Browser browser, string code, string verifier)
    {
        using var response = await PostTokenAsync(browser, code, verifier);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadJson(response);
    }

    private static Task<HttpResponseMessage> PostTokenAsync(Browser browser, string code, string verifier) =>
        browser.SendAsync(new HttpRequestMessage(HttpMethod.Post, new Uri(SignInHosts.MagicLink, "/connect/token"))
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = SignInHosts.ClientId,
                ["code"] = code,
                ["redirect_uri"] = SignInHosts.AppCallback.ToString(),
                ["code_verifier"] = verifier,
            }),
        });

    private static async Task<JsonDocument> ReadJson(HttpResponseMessage response) =>
        await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(Cancel), cancellationToken: Cancel);

    /// <summary>The payload of a signed JWT; an encrypted token has five parts and fails here.</summary>
    private static JsonDocument Payload(string jwt)
    {
        var parts = jwt.Split('.');
        Assert.Equal(3, parts.Length);
        return JsonDocument.Parse(WebEncoders.Base64UrlDecode(parts[1]));
    }

    private static string[] Strings(JsonElement array) => [.. array.EnumerateArray().Select(static item => item.GetString()!)];

    private static string Base64Url(byte[] bytes) => WebEncoders.Base64UrlEncode(bytes);
}
