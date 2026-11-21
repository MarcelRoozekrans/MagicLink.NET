using System.Net;
using System.Text.Json;
using static MagicLink.Tests.SignIn.Oidc;

namespace MagicLink.Tests.SignIn;

// docs/specs/magiclink-net-run-an-oidc-provider-for-applications-and-clis.md, criterion 2: an application can extend a
// session with a refresh token, without the person requesting a new email (issue #10).
public sealed class RefreshTokenTests(SignInHosts hosts) : IClassFixture<SignInHosts>
{
    private const string OfflineScope = "openid email offline_access";

    [Fact]
    public async Task An_application_extends_a_session_with_a_refresh_token_without_a_new_email()
    {
        using var tokens = await SignInAsync(OfflineScope);
        var queued = hosts.Queued;

        // A server-side application or a CLI refreshes without the person's browser, so without its session cookie.
        using var response = await PostRefreshAsync(hosts.NewBrowser(), RefreshToken(tokens));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var refreshed = await ReadJson(response);
        using var accessToken = Payload(refreshed.RootElement.GetProperty("access_token").GetString()!);
        Assert.Equal(SignInHosts.NormalizedPerson, accessToken.RootElement.GetProperty("sub").GetString());
        Assert.Equal(SignInHosts.NormalizedPerson, accessToken.RootElement.GetProperty("email").GetString());
        Assert.Equal(SignInHosts.PersonGroups, Strings(accessToken.RootElement.GetProperty("groups")));
        using var idToken = Payload(refreshed.RootElement.GetProperty("id_token").GetString()!);
        Assert.Equal(SignInHosts.NormalizedPerson, idToken.RootElement.GetProperty("sub").GetString());
        Assert.Equal(SignInHosts.PersonGroups, Strings(idToken.RootElement.GetProperty("groups")));
        Assert.Equal(queued, hosts.Queued);
    }

    [Fact]
    public async Task Each_refresh_gives_a_new_refresh_token()
    {
        using var tokens = await SignInAsync(OfflineScope);

        using var response = await PostRefreshAsync(hosts.NewBrowser(), RefreshToken(tokens));

        using var refreshed = await ReadJson(response);
        var next = RefreshToken(refreshed);
        Assert.NotEqual(RefreshToken(tokens), next, StringComparer.Ordinal);
        using var again = await PostRefreshAsync(hosts.NewBrowser(), next);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
    }

    [Fact]
    public async Task Without_offline_access_there_is_no_refresh_token()
    {
        using var tokens = await SignInAsync("openid email");

        Assert.False(tokens.RootElement.TryGetProperty("refresh_token", out _));
    }

    [Fact]
    public async Task Without_the_email_scope_a_refreshed_token_carries_no_email_address()
    {
        using var tokens = await SignInAsync("openid offline_access");

        using var response = await PostRefreshAsync(hosts.NewBrowser(), RefreshToken(tokens));

        using var refreshed = await ReadJson(response);
        using var accessToken = Payload(refreshed.RootElement.GetProperty("access_token").GetString()!);
        Assert.Equal(SignInHosts.NormalizedPerson, accessToken.RootElement.GetProperty("sub").GetString());
        Assert.False(accessToken.RootElement.TryGetProperty("email", out _));
        using var idToken = Payload(refreshed.RootElement.GetProperty("id_token").GetString()!);
        Assert.False(idToken.RootElement.TryGetProperty("email", out _));
    }

    [Theory]
    [InlineData(SignInHosts.Person)]
    [InlineData(SignInHosts.NoGroupsPerson)]
    public async Task A_refresh_that_narrows_the_scopes_to_leave_out_email_gets_no_email_address(string email)
    {
        using var tokens = await SignInAsync(OfflineScope, email);

        using var response = await PostRefreshAsync(hosts.NewBrowser(), RefreshToken(tokens), scope: "openid offline_access");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var refreshed = await ReadJson(response);
        using var accessToken = Payload(refreshed.RootElement.GetProperty("access_token").GetString()!);
        Assert.False(accessToken.RootElement.TryGetProperty("email", out _));
        Assert.False(accessToken.RootElement.TryGetProperty("email_verified", out _));
        using var idToken = Payload(refreshed.RootElement.GetProperty("id_token").GetString()!);
        Assert.False(idToken.RootElement.TryGetProperty("email", out _));
        Assert.False(idToken.RootElement.TryGetProperty("email_verified", out _));
    }

    [Fact]
    public async Task A_narrowed_refresh_leaves_the_scopes_of_the_next_refresh_token_as_they_were()
    {
        using var tokens = await SignInAsync(OfflineScope);
        using var narrowed = await PostRefreshAsync(hosts.NewBrowser(), RefreshToken(tokens), scope: "openid offline_access");
        using var narrowedTokens = await ReadJson(narrowed);

        using var response = await PostRefreshAsync(hosts.NewBrowser(), RefreshToken(narrowedTokens));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var refreshed = await ReadJson(response);
        using var accessToken = Payload(refreshed.RootElement.GetProperty("access_token").GetString()!);
        Assert.Equal(SignInHosts.NormalizedPerson, accessToken.RootElement.GetProperty("email").GetString());
        using var idToken = Payload(refreshed.RootElement.GetProperty("id_token").GetString()!);
        Assert.True(idToken.RootElement.GetProperty("email_verified").GetBoolean());
    }

    [Fact]
    public async Task A_refresh_token_cannot_widen_the_scopes()
    {
        using var tokens = await SignInAsync("openid offline_access");

        using var response = await PostRefreshAsync(hosts.NewBrowser(), RefreshToken(tokens), scope: "openid email offline_access");

        // OpenIddict answers a scope the refresh token was not issued for with invalid_grant.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var error = await ReadJson(response);
        Assert.Equal("invalid_grant", error.RootElement.GetProperty("error").GetString());
        Assert.Contains("scope", error.RootElement.GetProperty("error_description").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_refreshed_token_carries_the_groups_the_source_returns_now()
    {
        using var tokens = await SignInAsync(OfflineScope, SignInHosts.OneGroupPerson);
        await hosts.MoveToGroupsAsync(SignInHosts.OneGroupPerson, "writers", "admins");

        using var response = await PostRefreshAsync(hosts.NewBrowser(), RefreshToken(tokens));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var refreshed = await ReadJson(response);
        using var accessToken = Payload(refreshed.RootElement.GetProperty("access_token").GetString()!);
        Assert.Equal(["writers", "admins"], Strings(accessToken.RootElement.GetProperty("groups")));
        using var idToken = Payload(refreshed.RootElement.GetProperty("id_token").GetString()!);
        Assert.Equal(["writers", "admins"], Strings(idToken.RootElement.GetProperty("groups")));
    }

    [Fact]
    public async Task A_person_taken_out_of_the_source_cannot_refresh()
    {
        using var tokens = await SignInAsync(OfflineScope, SignInHosts.RemovedLaterPerson);
        await hosts.RemoveFromFileAsync(SignInHosts.RemovedLaterPerson);

        using var response = await PostRefreshAsync(hosts.NewBrowser(), RefreshToken(tokens));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var error = await ReadJson(response);
        Assert.Equal("invalid_grant", error.RootElement.GetProperty("error").GetString());
        Assert.Contains(hosts.Logs.Entries, static entry => entry.Message.Contains("refresh", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task An_unknown_refresh_token_is_refused()
    {
        using var response = await PostRefreshAsync(hosts.NewBrowser(), "not-a-refresh-token");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var error = await ReadJson(response);
        Assert.Equal("invalid_grant", error.RootElement.GetProperty("error").GetString());
    }

    private static string RefreshToken(JsonDocument tokens) => tokens.RootElement.GetProperty("refresh_token").GetString()!;

    /// <summary>Signs <paramref name="email"/> in with <paramref name="scope"/> and redeems the code.</summary>
    private async Task<JsonDocument> SignInAsync(string scope, string email = SignInHosts.Person)
    {
        var browser = hosts.NewBrowser();
        var (authorize, verifier, _) = NewAuthorization(scope);
        SignInMessage message;
        using (var emailForm = await SignInFlow.OpenEmailFormAsync(browser, authorize))
        {
            message = await hosts.StartAsync(browser, emailForm, email);
        }

        using var confirmed = await SignInFlow.TypeCodeAsync(browser, message.Code);
        var code = await CodeFrom(await SignInFlow.ContinueAsync(browser, confirmed, stopAtHost: SignInHosts.App.Host));
        return await RedeemAsync(browser, code, verifier);
    }
}
