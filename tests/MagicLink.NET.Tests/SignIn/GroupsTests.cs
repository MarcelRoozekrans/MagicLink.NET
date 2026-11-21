using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using static MagicLink.Tests.SignIn.Oidc;

namespace MagicLink.Tests.SignIn;

// docs/specs/magiclink-net-decide-who-may-sign-in-and-with-which-groups.md, criterion 1: the groups the source returns
// appear in the groups claim of the token (issue #7). SignInHosts reads who may sign in from a file, as from git.
public sealed class GroupsTests(SignInHosts hosts) : IClassFixture<SignInHosts>
{
    [Fact]
    public async Task The_groups_from_the_file_are_in_the_tokens_and_user_info()
    {
        var browser = hosts.NewBrowser();
        var (authorize, verifier, _) = NewAuthorization();

        var code = await CodeFrom(await hosts.SignInAsync(browser, authorize, stopAtHost: SignInHosts.App.Host));
        using var tokens = await RedeemAsync(browser, code, verifier);

        using var idToken = Payload(tokens.RootElement.GetProperty("id_token").GetString()!);
        Assert.Equal(SignInHosts.PersonGroups, Strings(idToken.RootElement.GetProperty("groups")));
        using var accessToken = Payload(tokens.RootElement.GetProperty("access_token").GetString()!);
        Assert.Equal(SignInHosts.PersonGroups, Strings(accessToken.RootElement.GetProperty("groups")));

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(SignInHosts.MagicLink, "/connect/userinfo"));
        request.Headers.Authorization = new("Bearer", tokens.RootElement.GetProperty("access_token").GetString());
        using var userInfo = await browser.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, userInfo.StatusCode);
        using var claims = await ReadJson(userInfo);
        Assert.Equal(SignInHosts.PersonGroups, Strings(claims.RootElement.GetProperty("groups")));
    }

    [Fact]
    public async Task One_group_is_still_a_list()
    {
        using var tokens = await SignInAsync(SignInHosts.OneGroupPerson);

        using var idToken = Payload(tokens.RootElement.GetProperty("id_token").GetString()!);
        var groups = idToken.RootElement.GetProperty("groups");
        Assert.Equal(JsonValueKind.Array, groups.ValueKind);
        Assert.Equal(["readers"], Strings(groups));
        using var accessToken = Payload(tokens.RootElement.GetProperty("access_token").GetString()!);
        Assert.Equal(JsonValueKind.Array, accessToken.RootElement.GetProperty("groups").ValueKind);
    }

    [Fact]
    public async Task A_person_without_groups_gets_no_groups_claim()
    {
        using var tokens = await SignInAsync(SignInHosts.NoGroupsPerson);

        using var idToken = Payload(tokens.RootElement.GetProperty("id_token").GetString()!);
        Assert.Equal(SignInHosts.NoGroupsPerson, idToken.RootElement.GetProperty("sub").GetString());
        Assert.False(idToken.RootElement.TryGetProperty("groups", out _));
        using var accessToken = Payload(tokens.RootElement.GetProperty("access_token").GetString()!);
        Assert.False(accessToken.RootElement.TryGetProperty("groups", out _));
    }

    [Fact]
    public async Task An_address_taken_out_of_the_file_after_signing_in_gets_no_code_for_the_application()
    {
        var browser = hosts.NewBrowser();
        (await SignInAsync(browser, NewAuthorization().Authorize, SignInHosts.RemovedLaterPerson)).Dispose();
        await hosts.RemoveFromFileAsync(SignInHosts.RemovedLaterPerson);

        // The session is still there, but the file no longer lists the address.
        using var response = await browser.NavigateAsync(NewAuthorization().Authorize, stopAtHost: SignInHosts.App.Host);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("access_denied", QueryHelpers.ParseQuery(response.Headers.Location!.Query)["error"].ToString());
    }

    /// <summary>Signs <paramref name="email"/> in through an authorization request and redeems the code.</summary>
    private async Task<JsonDocument> SignInAsync(string email)
    {
        var browser = hosts.NewBrowser();
        var (authorize, verifier, _) = NewAuthorization();
        return await RedeemAsync(browser, await CodeFrom(await SignInAsync(browser, authorize, email)), verifier);
    }

    /// <summary>Types <paramref name="email"/> and its code, and follows the sign-in until the application's callback.</summary>
    private async Task<HttpResponseMessage> SignInAsync(Browser browser, Uri authorize, string email)
    {
        SignInMessage message;
        using (var emailForm = await SignInFlow.OpenEmailFormAsync(browser, authorize))
        {
            message = await hosts.StartAsync(browser, emailForm, email);
        }

        using var confirmed = await SignInFlow.TypeCodeAsync(browser, message.Code);
        return await SignInFlow.ContinueAsync(browser, confirmed, stopAtHost: SignInHosts.App.Host);
    }
}
