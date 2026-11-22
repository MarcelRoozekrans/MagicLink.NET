using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace MagicLink.Tests.SignIn;

/// <summary>What an application does over OpenID Connect: build an authorization request, redeem its code, read tokens.</summary>
internal static class Oidc
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    public static (Uri Authorize, string Verifier, string Challenge) NewAuthorization(string scope = "openid email", string extra = "")
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

    public static Task<string> CodeFrom(HttpResponseMessage response)
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

    public static async Task<JsonDocument> RedeemAsync(Browser browser, string code, string verifier)
    {
        using var response = await PostTokenAsync(browser, code, verifier);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadJson(response);
    }

    public static Task<HttpResponseMessage> PostTokenAsync(Browser browser, string code, string verifier) =>
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

    public static Task<HttpResponseMessage> PostRefreshAsync(Browser browser, string refreshToken, string? scope = null)
    {
        var form = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = SignInHosts.ClientId,
            ["refresh_token"] = refreshToken,
        };
        if (scope is not null)
        {
            form["scope"] = scope;
        }

        return browser.SendAsync(new HttpRequestMessage(HttpMethod.Post, new Uri(SignInHosts.MagicLink, "/connect/token"))
        {
            Content = new FormUrlEncodedContent(form),
        });
    }

    /// <summary>Asks for a device code and a user code, as a command-line tool does.</summary>
    public static async Task<JsonDocument> RequestDeviceCodeAsync(Browser cli, string scope = "openid email offline_access")
    {
        using var response = await cli.SendAsync(new HttpRequestMessage(HttpMethod.Post, new Uri(SignInHosts.MagicLink, "/connect/device"))
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["client_id"] = SignInHosts.CliClientId,
                ["scope"] = scope,
            }),
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadJson(response);
    }

    /// <summary>Polls the token endpoint with <paramref name="deviceCode"/>, as a command-line tool does.</summary>
    public static Task<HttpResponseMessage> PostDeviceCodeAsync(Browser cli, string deviceCode) =>
        cli.SendAsync(new HttpRequestMessage(HttpMethod.Post, new Uri(SignInHosts.MagicLink, "/connect/token"))
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
                ["client_id"] = SignInHosts.CliClientId,
                ["device_code"] = deviceCode,
            }),
        });

    /// <summary>The <c>error</c> of an error response.</summary>
    public static async Task<string?> ErrorOf(HttpResponseMessage response)
    {
        using var error = await ReadJson(response);
        return error.RootElement.GetProperty("error").GetString();
    }

    public static async Task<JsonDocument> ReadJson(HttpResponseMessage response) =>
        await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(Cancel), cancellationToken: Cancel);

    /// <summary>The payload of a signed JWT; an encrypted token has five parts and fails here.</summary>
    public static JsonDocument Payload(string jwt)
    {
        var parts = jwt.Split('.');
        Assert.Equal(3, parts.Length);
        return JsonDocument.Parse(WebEncoders.Base64UrlDecode(parts[1]));
    }

    public static string[] Strings(JsonElement array) => [.. array.EnumerateArray().Select(static item => item.GetString()!)];

    public static string Base64Url(byte[] bytes) => WebEncoders.Base64UrlEncode(bytes);
}
