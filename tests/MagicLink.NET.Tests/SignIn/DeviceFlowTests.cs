using System.Net;
using System.Text.Json;
using static MagicLink.Tests.SignIn.Oidc;

namespace MagicLink.Tests.SignIn;

// docs/specs/magiclink-net-run-an-oidc-provider-for-applications-and-clis.md, criterion 3: a CLI signs a person in with
// the OAuth device authorization grant. It receives a user code, the person enters it in a browser and signs in there
// with the link or the code, and the CLI then receives its tokens (issue #24).
public sealed class DeviceFlowTests(SignInHosts hosts) : IClassFixture<SignInHosts>
{
    private static readonly Uri Verify = new(SignInHosts.MagicLink, "/connect/verify");

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_cli_signs_a_person_in_who_types_the_user_code_and_the_code_from_the_email()
    {
        var cli = hosts.NewBrowser();
        using var device = await RequestDeviceCodeAsync(cli);
        var root = device.RootElement;
        Assert.Equal(Verify.ToString(), root.GetProperty("verification_uri").GetString());
        var userCode = root.GetProperty("user_code").GetString()!;
        var deviceCode = root.GetProperty("device_code").GetString()!;

        using (var pending = await PostDeviceCodeAsync(cli, deviceCode))
        {
            Assert.Equal(HttpStatusCode.BadRequest, pending.StatusCode);
            Assert.Equal("authorization_pending", await ErrorOf(pending));
        }

        // The person opens the verification address in a browser, signs in there with the code from the email, and
        // types the user code the CLI shows.
        var browser = hosts.NewBrowser();
        using (var form = await hosts.SignInAsync(browser, Verify))
        {
            Assert.Equal(HttpStatusCode.OK, form.StatusCode);
            Assert.Equal(Verify.AbsolutePath, browser.Visited[^1].AbsolutePath);
            using var done = await ApproveAsync(browser, form, userCode);
            Assert.Equal(HttpStatusCode.OK, done.StatusCode);
        }

        using var response = await PostDeviceCodeAsync(cli, deviceCode);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var tokens = await ReadJson(response);
        using var accessToken = Payload(tokens.RootElement.GetProperty("access_token").GetString()!);
        Assert.Equal(SignInHosts.NormalizedPerson, accessToken.RootElement.GetProperty("sub").GetString());
        Assert.Equal(SignInHosts.NormalizedPerson, accessToken.RootElement.GetProperty("email").GetString());
        Assert.Equal(SignInHosts.PersonGroups, Strings(accessToken.RootElement.GetProperty("groups")));
        using var idToken = Payload(tokens.RootElement.GetProperty("id_token").GetString()!);
        Assert.Equal(SignInHosts.CliClientId, idToken.RootElement.GetProperty("aud").GetString());
        Assert.Equal(SignInHosts.PersonGroups, Strings(idToken.RootElement.GetProperty("groups")));

        // With offline_access the CLI keeps the session alive without a new email, as any application does.
        using var refreshed = await cli.SendAsync(new HttpRequestMessage(HttpMethod.Post, new Uri(SignInHosts.MagicLink, "/connect/token"))
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = SignInHosts.CliClientId,
                ["refresh_token"] = tokens.RootElement.GetProperty("refresh_token").GetString()!,
            }),
        });
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
    }

    [Fact]
    public async Task A_person_signs_in_with_the_link_from_the_email_and_the_user_code_filled_in()
    {
        var cli = hosts.NewBrowser();
        using var device = await RequestDeviceCodeAsync(cli);
        var complete = new Uri(device.RootElement.GetProperty("verification_uri_complete").GetString()!);
        var deviceCode = device.RootElement.GetProperty("device_code").GetString()!;

        var browser = hosts.NewBrowser();
        SignInMessage message;
        using (var emailForm = await SignInFlow.OpenEmailFormAsync(browser, complete))
        {
            message = await hosts.StartAsync(browser, emailForm, SignInHosts.OneGroupPerson);
        }

        using (var confirmed = await SignInFlow.PressButtonAsync(browser, message.Link))
        using (var form = await SignInFlow.ContinueAsync(browser, confirmed))
        {
            Assert.Equal(HttpStatusCode.OK, form.StatusCode);

            // Opening the verification address with the user code in it shows the code, and approves nothing yet.
            var html = await form.Content.ReadAsStringAsync(Cancel);
            Assert.Contains(device.RootElement.GetProperty("user_code").GetString()!, html, StringComparison.Ordinal);
            Assert.Contains(SignInHosts.OneGroupPerson, html, StringComparison.Ordinal);
            using (var pending = await PostDeviceCodeAsync(cli, deviceCode))
            {
                Assert.Equal("authorization_pending", await ErrorOf(pending));
            }

            var filledIn = await HtmlForm.ReadAsync(form);
            using var done = await filledIn.SubmitAsync(browser, ("user_code", device.RootElement.GetProperty("user_code").GetString()!));
            Assert.Equal(HttpStatusCode.Redirect, done.StatusCode);
        }

        using var response = await PostDeviceCodeAsync(cli, deviceCode);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var tokens = await ReadJson(response);
        using var accessToken = Payload(tokens.RootElement.GetProperty("access_token").GetString()!);
        Assert.Equal(SignInHosts.OneGroupPerson, accessToken.RootElement.GetProperty("sub").GetString());
        Assert.Equal(["readers"], Strings(accessToken.RootElement.GetProperty("groups")));
    }

    [Fact]
    public async Task A_person_with_a_session_approves_a_cli_without_a_new_email()
    {
        var browser = hosts.NewBrowser();
        (await hosts.SignInAsync(browser, Verify)).Dispose();
        var queued = hosts.Queued;
        var cli = hosts.NewBrowser();
        using var device = await RequestDeviceCodeAsync(cli);

        using (var form = await browser.NavigateAsync(Verify))
        using (var done = await ApproveAsync(browser, form, device.RootElement.GetProperty("user_code").GetString()!))
        {
            Assert.Equal(HttpStatusCode.OK, done.StatusCode);
        }

        using var response = await PostDeviceCodeAsync(cli, device.RootElement.GetProperty("device_code").GetString()!);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(queued, hosts.Queued);
    }

    [Fact]
    public async Task A_device_code_gives_tokens_only_once()
    {
        var cli = hosts.NewBrowser();
        var deviceCode = await ApprovedDeviceCodeAsync(cli, SignInHosts.Person);
        (await PostDeviceCodeAsync(cli, deviceCode)).Dispose();

        using var again = await PostDeviceCodeAsync(cli, deviceCode);

        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.Equal("invalid_grant", await ErrorOf(again));
    }

    [Fact]
    public async Task Without_the_email_scope_the_tokens_carry_no_email_address()
    {
        var cli = hosts.NewBrowser();
        var deviceCode = await ApprovedDeviceCodeAsync(cli, SignInHosts.Person, scope: "openid");

        using var response = await PostDeviceCodeAsync(cli, deviceCode);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var tokens = await ReadJson(response);
        using var accessToken = Payload(tokens.RootElement.GetProperty("access_token").GetString()!);
        Assert.Equal(SignInHosts.NormalizedPerson, accessToken.RootElement.GetProperty("sub").GetString());
        Assert.False(accessToken.RootElement.TryGetProperty("email", out _));
        using var idToken = Payload(tokens.RootElement.GetProperty("id_token").GetString()!);
        Assert.False(idToken.RootElement.TryGetProperty("email", out _));
        Assert.Equal(SignInHosts.PersonGroups, Strings(idToken.RootElement.GetProperty("groups")));
        Assert.False(tokens.RootElement.TryGetProperty("refresh_token", out _));
    }

    [Fact]
    public async Task A_wrong_user_code_approves_nothing()
    {
        var cli = hosts.NewBrowser();
        using var device = await RequestDeviceCodeAsync(cli);
        var browser = hosts.NewBrowser();
        using var form = await hosts.SignInAsync(browser, Verify);

        using var answer = await ApproveAsync(browser, form, "0000-0000-0000");

        Assert.Equal(HttpStatusCode.BadRequest, answer.StatusCode);
        Assert.True(HtmlForm.IsIn(await answer.Content.ReadAsStringAsync(Cancel)), "The page asks for the code again.");
        using var pending = await PostDeviceCodeAsync(cli, device.RootElement.GetProperty("device_code").GetString()!);
        Assert.Equal("authorization_pending", await ErrorOf(pending));
    }

    [Fact]
    public async Task The_verification_form_needs_its_antiforgery_token()
    {
        var cli = hosts.NewBrowser();
        using var device = await RequestDeviceCodeAsync(cli);
        var browser = hosts.NewBrowser();
        (await hosts.SignInAsync(browser, Verify)).Dispose();

        using var answer = await browser.SendAsync(new HttpRequestMessage(HttpMethod.Post, Verify)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["user_code"] = device.RootElement.GetProperty("user_code").GetString()!,
            }),
        });

        Assert.Equal(HttpStatusCode.BadRequest, answer.StatusCode);
        using var pending = await PostDeviceCodeAsync(cli, device.RootElement.GetProperty("device_code").GetString()!);
        Assert.Equal("authorization_pending", await ErrorOf(pending));
    }

    [Fact]
    public async Task A_session_that_ended_before_the_form_was_sent_signs_in_again_and_keeps_the_user_code()
    {
        var cli = hosts.NewBrowser();
        using var device = await RequestDeviceCodeAsync(cli);
        var userCode = device.RootElement.GetProperty("user_code").GetString()!;
        var browser = hosts.NewBrowser();
        using var form = await hosts.SignInAsync(browser, Verify);
        browser.ForgetCookie(SignInHosts.MagicLink, MagicLinkSession.CookieName);

        using var answer = await ApproveAsync(browser, form, userCode);

        Assert.Equal(HttpStatusCode.Redirect, answer.StatusCode);
        var signInPage = new Uri(SignInHosts.MagicLink, answer.Headers.Location!);
        Assert.Equal("/signin", signInPage.AbsolutePath);
        using var signIn = await hosts.SignInAsync(browser, signInPage);
        Assert.Equal(Verify.AbsolutePath, browser.Visited[^1].AbsolutePath);
        Assert.Contains(userCode, await signIn.Content.ReadAsStringAsync(Cancel), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_person_taken_out_of_the_source_cannot_approve_a_cli()
    {
        var cli = hosts.NewBrowser();
        using var device = await RequestDeviceCodeAsync(cli);
        var browser = hosts.NewBrowser();
        using var form = await hosts.SignInAsync(browser, Verify, email: SignInHosts.RemovedLaterPerson);
        await hosts.RemoveFromFileAsync(SignInHosts.RemovedLaterPerson);

        using var answer = await ApproveAsync(browser, form, device.RootElement.GetProperty("user_code").GetString()!);

        Assert.Equal(HttpStatusCode.Forbidden, answer.StatusCode);
        using var refused = await PostDeviceCodeAsync(cli, device.RootElement.GetProperty("device_code").GetString()!);
        Assert.Equal("access_denied", await ErrorOf(refused));

        // The session ends with it, as at the authorization endpoint.
        using var again = await browser.NavigateAsync(Verify);
        Assert.Equal("/signin", browser.Visited[^1].AbsolutePath);
    }

    [Fact]
    public async Task A_person_taken_out_of_the_source_after_approving_gets_no_tokens()
    {
        var cli = hosts.NewBrowser();
        var deviceCode = await ApprovedDeviceCodeAsync(cli, SignInHosts.LeavesLaterPerson);
        await hosts.RemoveFromFileAsync(SignInHosts.LeavesLaterPerson);

        using var response = await PostDeviceCodeAsync(cli, deviceCode);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("access_denied", await ErrorOf(response));
        Assert.Contains(hosts.Logs.Entries, static entry => entry.Message.Contains("device code", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task The_tokens_carry_the_groups_the_source_returns_when_the_cli_collects_them()
    {
        var cli = hosts.NewBrowser();
        var deviceCode = await ApprovedDeviceCodeAsync(cli, SignInHosts.NoGroupsPerson);
        await hosts.MoveToGroupsAsync(SignInHosts.NoGroupsPerson, "operators");

        using var response = await PostDeviceCodeAsync(cli, deviceCode);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var tokens = await ReadJson(response);
        using var accessToken = Payload(tokens.RootElement.GetProperty("access_token").GetString()!);
        Assert.Equal(["operators"], Strings(accessToken.RootElement.GetProperty("groups")));
    }

    /// <summary>Types <paramref name="userCode"/> on the verification form and presses its button.</summary>
    private static async Task<HttpResponseMessage> ApproveAsync(Browser browser, HttpResponseMessage form, string userCode)
    {
        var verification = await HtmlForm.ReadAsync(form);
        var answer = await verification.SubmitAsync(browser, ("user_code", userCode));
        // A redirect to the page that says how it went is followed; any other answer is returned as it is.
        if (answer.StatusCode != HttpStatusCode.Redirect || answer.Headers.Location?.ToString().StartsWith("/connect/verify/", StringComparison.Ordinal) != true)
        {
            return answer;
        }

        using (answer)
        {
            return await browser.NavigateAsync(new Uri(SignInHosts.MagicLink, answer.Headers.Location));
        }
    }

    /// <summary>A device code that <paramref name="email"/> approved in a browser.</summary>
    private async Task<string> ApprovedDeviceCodeAsync(Browser cli, string email, string scope = "openid email offline_access")
    {
        using var device = await RequestDeviceCodeAsync(cli, scope);
        var browser = hosts.NewBrowser();
        using var form = await hosts.SignInAsync(browser, Verify, email: email);
        using var done = await ApproveAsync(browser, form, device.RootElement.GetProperty("user_code").GetString()!);
        Assert.Equal(HttpStatusCode.OK, done.StatusCode);
        return device.RootElement.GetProperty("device_code").GetString()!;
    }
}
