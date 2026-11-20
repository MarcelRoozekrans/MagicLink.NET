using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace MagicLink.AotProof.Tests;

/// <summary>The OpenIddict flows every store must serve. Each derived class runs them on one store.</summary>
public abstract class FlowTests(ProofHost host)
{
    private const string RedirectUri = "http://localhost/callback";

    protected HttpClient Client => host.Client;

    protected static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Discovery_lists_the_endpoints_and_pkce()
    {
        using var document = await GetJson("/.well-known/openid-configuration");
        var root = document.RootElement;

        Assert.EndsWith("/connect/token", root.GetProperty("token_endpoint").GetString(), StringComparison.Ordinal);
        Assert.EndsWith("/connect/authorize", root.GetProperty("authorization_endpoint").GetString(), StringComparison.Ordinal);
        Assert.EndsWith("/.well-known/jwks", root.GetProperty("jwks_uri").GetString(), StringComparison.Ordinal);
        Assert.Contains("code", Strings(root.GetProperty("response_types_supported")), StringComparer.Ordinal);
        Assert.Contains("S256", Strings(root.GetProperty("code_challenge_methods_supported")), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Jwks_has_a_signing_key()
    {
        using var document = await GetJson("/.well-known/jwks");

        var key = Assert.Single(document.RootElement.GetProperty("keys").EnumerateArray());
        Assert.Equal("RSA", key.GetProperty("kty").GetString());
        Assert.Equal("sig", key.GetProperty("use").GetString());
    }

    [Fact]
    public async Task Code_flow_with_pkce_returns_tokens_that_open_api_me()
    {
        var verifier = NewVerifier();
        var code = await Authorize(ProofClients.PublicClientId, Challenge(verifier));

        using var tokens = await RedeemCode(code, verifier);

        Assert.Equal(HttpStatusCode.OK, tokens.StatusCode);
        using var body = await ReadJson(tokens);
        Assert.True(body.RootElement.TryGetProperty("refresh_token", out _));
        Assert.Equal(ProofClients.TestSubject, await CallApiMe(body.RootElement.GetProperty("access_token").GetString()!));
    }

    [Fact]
    public async Task Refresh_grant_returns_a_new_access_token()
    {
        var verifier = NewVerifier();
        var code = await Authorize(ProofClients.PublicClientId, Challenge(verifier));
        using var first = await ReadJson(await RedeemCode(code, verifier));

        using var refreshed = await PostToken(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = ProofClients.PublicClientId,
            ["refresh_token"] = first.RootElement.GetProperty("refresh_token").GetString()!,
        });

        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        using var body = await ReadJson(refreshed);
        Assert.NotEqual(first.RootElement.GetProperty("access_token").GetString(), body.RootElement.GetProperty("access_token").GetString(), StringComparer.Ordinal);
        Assert.Equal(ProofClients.TestSubject, await CallApiMe(body.RootElement.GetProperty("access_token").GetString()!));
    }

    [Fact]
    public async Task Client_credentials_returns_a_token_that_opens_api_me()
    {
        using var response = await PostToken(ClientCredentials(ProofClients.ConfidentialClientSecret));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await ReadJson(response);
        Assert.Equal(ProofClients.ConfidentialClientId, await CallApiMe(body.RootElement.GetProperty("access_token").GetString()!));
    }

    [Fact]
    public async Task Wrong_client_secret_is_refused()
    {
        using var response = await PostToken(ClientCredentials("not-the-secret"));

        Assert.Equal("invalid_client", await Error(response));
    }

    [Fact]
    public async Task Wrong_pkce_verifier_is_refused()
    {
        var code = await Authorize(ProofClients.PublicClientId, Challenge(NewVerifier()));

        using var response = await RedeemCode(code, NewVerifier());

        Assert.Equal("invalid_grant", await Error(response));
    }

    [Fact]
    public async Task Unknown_client_gets_no_code()
    {
        using var response = await Client.GetAsync(AuthorizeUri("unknown-client", Challenge(NewVerifier())), Cancel);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Contains("ID2052", await response.Content.ReadAsStringAsync(Cancel), StringComparison.Ordinal); // invalid client_id
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-token")]
    public async Task Api_me_without_a_valid_token_is_401(string? token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        using var response = await Client.SendAsync(request, Cancel);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static Dictionary<string, string> ClientCredentials(string secret) => new(StringComparer.Ordinal)
    {
        ["grant_type"] = "client_credentials",
        ["client_id"] = ProofClients.ConfidentialClientId,
        ["client_secret"] = secret,
    };

    private static string NewVerifier() => Base64Url(RandomNumberGenerator.GetBytes(32));

    private static string Challenge(string verifier) => Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    private static string Base64Url(byte[] bytes) => WebEncoders.Base64UrlEncode(bytes);

    private static string AuthorizeUri(string clientId, string challenge) => QueryHelpers.AddQueryString("/connect/authorize", new Dictionary<string, string?>(StringComparer.Ordinal)
    {
        ["client_id"] = clientId,
        ["response_type"] = "code",
        ["redirect_uri"] = RedirectUri,
        ["scope"] = "openid offline_access",
        ["code_challenge"] = challenge,
        ["code_challenge_method"] = "S256",
        ["state"] = "proof-state",
    });

    private async Task<string> Authorize(string clientId, string challenge)
    {
        using var response = await Client.GetAsync(AuthorizeUri(clientId, challenge), Cancel);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        var location = response.Headers.Location!;
        Assert.StartsWith(RedirectUri, location.ToString(), StringComparison.Ordinal);
        var query = QueryHelpers.ParseQuery(location.Query);
        Assert.Equal("proof-state", query["state"].ToString());
        return query["code"].ToString();
    }

    private Task<HttpResponseMessage> RedeemCode(string code, string verifier) => PostToken(new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["grant_type"] = "authorization_code",
        ["client_id"] = ProofClients.PublicClientId,
        ["code"] = code,
        ["code_verifier"] = verifier,
        ["redirect_uri"] = RedirectUri,
    });

    protected async Task<HttpResponseMessage> PostToken(IEnumerable<KeyValuePair<string, string>> form)
    {
        using var content = new FormUrlEncodedContent(form);
        return await Client.PostAsync("/connect/token", content, Cancel);
    }

    protected async Task<string?> CallApiMe(string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await Client.SendAsync(request, Cancel);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = await ReadJson(response);
        return body.RootElement.GetProperty("subject").GetString();
    }

    private async Task<JsonDocument> GetJson(string path)
    {
        using var response = await Client.GetAsync(path, Cancel);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadJson(response);
    }

    protected static async Task<string?> Error(HttpResponseMessage response)
    {
        Assert.False(response.IsSuccessStatusCode);
        using var body = await ReadJson(response);
        return body.RootElement.GetProperty("error").GetString();
    }

    protected static async Task<JsonDocument> ReadJson(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancel));

    private static List<string> Strings(JsonElement array) => array.EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToList();
}
