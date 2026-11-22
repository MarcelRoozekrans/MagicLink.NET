using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MagicLink.Host.Tests.Mail;
using Microsoft.AspNetCore.WebUtilities;

namespace MagicLink.Host.Tests;

// docs/specs/magiclink-net-deliver-as-a-nuget-package-and-a-container-image.md, criterion 1: MagicLink.NET runs on its
// own from the container image, a thin host around the packages, with the same behaviour as hosted in an app (issue #12).
// CI runs these tests in-process, and against the container image in the container-image job.
public sealed class FlowTests(HostUnderTest host) : IClassFixture<HostUnderTest>
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private HttpClient Client => host.Client;

    [Fact]
    public async Task Discovery_names_the_configured_issuer_and_the_endpoints()
    {
        using var document = await GetJson("/.well-known/openid-configuration");
        var root = document.RootElement;

        Assert.Equal(host.Issuer.AbsoluteUri, root.GetProperty("issuer").GetString());
        Assert.Equal(new Uri(host.Issuer, "connect/authorize").AbsoluteUri, root.GetProperty("authorization_endpoint").GetString());
        Assert.Equal(new Uri(host.Issuer, "connect/token").AbsoluteUri, root.GetProperty("token_endpoint").GetString());
        Assert.Contains("S256", root.GetProperty("code_challenge_methods_supported").EnumerateArray().Select(static item => item.GetString()), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Tokens_are_signed_with_the_configured_certificate()
    {
        using var document = await GetJson("/.well-known/jwks");

        var key = Assert.Single(document.RootElement.GetProperty("keys").EnumerateArray());
        var thumbprint = Convert.ToHexString(WebEncoders.Base64UrlDecode(key.GetProperty("x5t").GetString()!));
        Assert.Equal(host.SigningThumbprint, thumbprint, ignoreCase: true);
    }

    [Fact]
    public async Task A_listed_person_signs_in_to_a_configured_public_client_and_refreshes()
    {
        var verifier = NewVerifier();
        var code = await AuthorizeAsync(TestSetup.PublicClient, verifier, "openid email offline_access");

        using var tokens = await ReadJson(await RedeemAsync(TestSetup.PublicClient, secret: null, code, verifier));
        using var access = Payload(tokens.RootElement.GetProperty("access_token").GetString()!);
        Assert.Equal(TestSetup.Person, access.RootElement.GetProperty("sub").GetString());
        Assert.Equal(TestSetup.Group, GroupsOf(access.RootElement).Single());
        using var identity = Payload(tokens.RootElement.GetProperty("id_token").GetString()!);
        Assert.Equal(TestSetup.Person, identity.RootElement.GetProperty("email").GetString());

        using var refreshed = await PostToken(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = TestSetup.PublicClient,
            ["refresh_token"] = tokens.RootElement.GetProperty("refresh_token").GetString()!,
        });
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
    }

    [Fact]
    public async Task A_confidential_client_redeems_its_code_with_its_secret_only()
    {
        var verifier = NewVerifier();
        var code = await AuthorizeAsync(TestSetup.ConfidentialClient, verifier, "openid");

        using (var wrong = await RedeemAsync(TestSetup.ConfidentialClient, "not-the-secret", code, verifier))
        {
            Assert.Equal("invalid_client", await Error(wrong));
        }

        var secondVerifier = NewVerifier();
        var secondCode = await AuthorizeAsync(TestSetup.ConfidentialClient, secondVerifier, "openid");
        using var tokens = await RedeemAsync(TestSetup.ConfidentialClient, TestSetup.ClientSecret, secondCode, secondVerifier);
        Assert.Equal(HttpStatusCode.OK, tokens.StatusCode);
    }

    [Fact]
    public async Task A_client_without_offline_access_in_its_settings_gets_no_refresh_token()
    {
        using var response = await Client.GetAsync(AuthorizeUri(TestSetup.ConfidentialClient, Challenge(NewVerifier()), "openid offline_access"), Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Contains("ID2065", await response.Content.ReadAsStringAsync(Cancel), StringComparison.Ordinal); // offline_access not allowed
    }

    [Fact]
    public async Task A_client_that_is_not_configured_gets_no_code()
    {
        using var response = await Client.GetAsync(AuthorizeUri("not-configured", Challenge(NewVerifier()), "openid"), Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task An_address_that_is_not_listed_gets_the_same_answer_and_no_email()
    {
        var browser = new TestBrowser(Client);
        using var emailForm = await browser.NavigateAsync(AuthorizeUri(TestSetup.PublicClient, Challenge(NewVerifier()), "openid"));

        using var sent = await browser.SubmitFormAsync(emailForm, ("email", TestSetup.Outsider));

        Assert.Equal(HttpStatusCode.SeeOther, sent.StatusCode);
        Assert.Equal("/signin/code", TestBrowser.PathOf(sent.Headers.Location!));
        await Task.Delay(500, Cancel);
        Assert.Empty(await host.Mailbox.ReadAsync(TestSetup.Outsider));
    }

    /// <summary>
    /// Signs <see cref="TestSetup.Person"/> in through the host's sign-in page in a fresh browser, with the code from the
    /// email, and returns the authorization code the application receives.
    /// </summary>
    private async Task<string> AuthorizeAsync(string clientId, string verifier, string scope)
    {
        var browser = new TestBrowser(Client);
        using var emailForm = await browser.NavigateAsync(AuthorizeUri(clientId, Challenge(verifier), scope));
        Assert.Equal(HttpStatusCode.OK, emailForm.StatusCode);

        var sentBefore = (await host.Mailbox.ReadAsync(TestSetup.Person)).Count;
        using (var sent = await browser.SubmitFormAsync(emailForm, ("email", TestSetup.Person)))
        {
            Assert.Equal(HttpStatusCode.SeeOther, sent.StatusCode);
        }

        var mailCode = await host.Mailbox.WaitForCodeAsync(TestSetup.Person, sentBefore);
        using var codeForm = await browser.NavigateAsync("/signin/code");
        using var confirmed = await browser.SubmitFormAsync(codeForm, ("code", mailCode));
        Assert.Equal(HttpStatusCode.Redirect, confirmed.StatusCode);

        using var response = await browser.NavigateAsync(TestBrowser.PathOf(confirmed.Headers.Location!));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!;
        Assert.StartsWith(TestSetup.RedirectUri, location.AbsoluteUri, StringComparison.Ordinal);
        var query = QueryHelpers.ParseQuery(location.Query);
        Assert.Equal("host-state", query["state"].ToString());
        return query["code"].ToString();
    }

    private Task<HttpResponseMessage> RedeemAsync(string clientId, string? secret, string code, string verifier)
    {
        var form = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = clientId,
            ["code"] = code,
            ["code_verifier"] = verifier,
            ["redirect_uri"] = TestSetup.RedirectUri,
        };
        if (secret is not null)
        {
            form["client_secret"] = secret;
        }

        return PostToken(form);
    }

    private async Task<HttpResponseMessage> PostToken(Dictionary<string, string> form)
    {
        using var content = new FormUrlEncodedContent(form);
        return await Client.PostAsync("/connect/token", content, Cancel);
    }

    private static string AuthorizeUri(string clientId, string challenge, string scope) => QueryHelpers.AddQueryString("/connect/authorize", new Dictionary<string, string?>(StringComparer.Ordinal)
    {
        ["client_id"] = clientId,
        ["response_type"] = "code",
        ["redirect_uri"] = TestSetup.RedirectUri,
        ["scope"] = scope,
        ["code_challenge"] = challenge,
        ["code_challenge_method"] = "S256",
        ["state"] = "host-state",
    });

    private static string NewVerifier() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    private static string Challenge(string verifier) => WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    private static JsonDocument Payload(string jwt) => JsonDocument.Parse(WebEncoders.Base64UrlDecode(jwt.Split('.')[1]));

    private static IEnumerable<string?> GroupsOf(JsonElement token)
    {
        var groups = token.GetProperty("groups");
        return groups.ValueKind == JsonValueKind.Array ? groups.EnumerateArray().Select(static group => group.GetString()) : [groups.GetString()];
    }

    private async Task<JsonDocument> GetJson(string path)
    {
        using var response = await Client.GetAsync(path, Cancel);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadJson(response);
    }

    private static async Task<string?> Error(HttpResponseMessage response)
    {
        Assert.False(response.IsSuccessStatusCode);
        using var body = await ReadJson(response);
        return body.RootElement.GetProperty("error").GetString();
    }

    private static async Task<JsonDocument> ReadJson(HttpResponseMessage response)
    {
        using (response)
        {
            return JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancel));
        }
    }
}
