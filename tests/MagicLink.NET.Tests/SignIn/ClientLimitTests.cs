using System.Net;

namespace MagicLink.Tests.SignIn;

// docs/specs/magiclink-net-confirm-sign-in-with-a-link-or-a-code.md, criterion 8 (issue #55): at most 20 sign-ins per
// client IP address in 15 minutes, for any addresses; beyond that the answer stays the same, but no email goes out. Each
// test uses addresses of its own, since the hosts and their counts are shared.
public sealed class ClientLimitTests(SignInHosts hosts) : IClassFixture<SignInHosts>
{
    private static readonly Uri AppMe = new(SignInHosts.App, "me");

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_21st_sign_in_from_one_client_gets_the_same_answer_but_no_email()
    {
        hosts.ClientAddress = IPAddress.Parse("192.0.2.10");

        // Sign-ins for unknown addresses count too, or the limit would tell known and unknown addresses apart.
        for (var i = 0; i < 20; i++)
        {
            (await StartAsync(hosts.NewBrowser(), i % 2 == 0 ? SignInHosts.Person : "nobody" + i + "@example.com")).Dispose();
        }

        var browser = hosts.NewBrowser();
        var before = hosts.Queued;
        using var last = await StartAsync(browser, SignInHosts.Person);

        Assert.Equal(HttpStatusCode.SeeOther, last.StatusCode);
        Assert.Equal("/signin/code", last.Headers.Location!.OriginalString);
        Assert.Equal(before, hosts.Queued);
        using var codePage = await browser.NavigateAsync(SignInFlow.CodePage);
        Assert.Contains("If " + SignInHosts.NormalizedPerson + " is known", await codePage.Content.ReadAsStringAsync(Cancel), StringComparison.Ordinal);
        var requestId = RequestId(last);
        Assert.Contains(hosts.Logs.Entries, entry =>
            string.Equals(entry.Category, "MagicLink.SignIn", StringComparison.Ordinal)
            && entry.Message.Contains(requestId, StringComparison.Ordinal)
            && entry.Message.Contains("client IP address", StringComparison.Ordinal));

        // Another client is not held back.
        hosts.ClientAddress = IPAddress.Parse("192.0.2.11");
        var other = hosts.Queued;
        (await StartAsync(hosts.NewBrowser(), SignInHosts.Person)).Dispose();
        Assert.Equal(other + 1, hosts.Queued);

        // A quarter of an hour later the first client may start sign-ins again.
        hosts.Clock.Advance(TimeSpan.FromMinutes(15));
        hosts.ClientAddress = IPAddress.Parse("192.0.2.10");
        var later = hosts.Queued;
        (await StartAsync(hosts.NewBrowser(), SignInHosts.Person)).Dispose();
        Assert.Equal(later + 1, hosts.Queued);
    }

    [Fact]
    public async Task An_ipv4_client_counts_the_same_whether_or_not_the_socket_maps_it_to_ipv6()
    {
        var client = IPAddress.Parse("198.51.100.20");
        hosts.ClientAddress = client.MapToIPv6();
        for (var i = 0; i < 20; i++)
        {
            (await StartAsync(hosts.NewBrowser(), SignInHosts.Person)).Dispose();
        }

        hosts.ClientAddress = client;
        var before = hosts.Queued;
        (await StartAsync(hosts.NewBrowser(), SignInHosts.Person)).Dispose();
        Assert.Equal(before, hosts.Queued);
    }

    [Fact]
    public async Task Ipv6_clients_in_one_64_network_share_their_count()
    {
        for (var i = 0; i < 20; i++)
        {
            hosts.ClientAddress = IPAddress.Parse("2001:db8:aa:1::" + (i + 1).ToString("x", System.Globalization.CultureInfo.InvariantCulture));
            (await StartAsync(hosts.NewBrowser(), SignInHosts.Person)).Dispose();
        }

        hosts.ClientAddress = IPAddress.Parse("2001:db8:aa:1:ffff::1");
        var before = hosts.Queued;
        (await StartAsync(hosts.NewBrowser(), SignInHosts.Person)).Dispose();
        Assert.Equal(before, hosts.Queued);

        hosts.ClientAddress = IPAddress.Parse("2001:db8:aa:2::1");
        (await StartAsync(hosts.NewBrowser(), SignInHosts.Person)).Dispose();
        Assert.Equal(before + 1, hosts.Queued);
    }

    private static async Task<HttpResponseMessage> StartAsync(Browser browser, string email)
    {
        HtmlForm form;
        using (var emailForm = await SignInFlow.OpenEmailFormAsync(browser, AppMe))
        {
            form = await HtmlForm.ReadAsync(emailForm);
        }

        return await form.SubmitAsync(browser, ("email", email));
    }

    private static string RequestId(HttpResponseMessage response)
    {
        var cookie = response.Headers.GetValues("Set-Cookie").Single(static cookie => cookie.StartsWith(SignInRequest.BindingCookie + "=", StringComparison.Ordinal));
        var value = cookie[(SignInRequest.BindingCookie.Length + 1)..];
        return value[..value.IndexOf('.', StringComparison.Ordinal)];
    }
}
