using System.Net;
using Microsoft.Extensions.Logging;

namespace MagicLink.Tests.SignIn;

// docs/specs/magiclink-net-confirm-sign-in-with-a-link-or-a-code.md, criterion 4 (issue #5): an address the source does
// not know gets no session and no email, sees the same answer as a known address, and the refusal is logged.
public sealed class UnknownAddressTests(SignInHosts hosts) : IClassFixture<SignInHosts>
{
    private static readonly Uri AppMe = new(SignInHosts.App, "me");

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_unknown_address_gets_no_email()
    {
        var queued = hosts.Queued;

        using var response = await StartAsync(hosts.NewBrowser(), SignInHosts.UnlistedPerson);

        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        Assert.Equal(queued, hosts.Queued);
    }

    [Fact]
    public async Task An_unknown_address_gets_the_same_answer_as_a_known_one()
    {
        using var known = await StartAsync(hosts.NewBrowser(), SignInHosts.NormalizedPerson);
        using var unknown = await StartAsync(hosts.NewBrowser(), SignInHosts.UnlistedPerson);

        Assert.Equal(known.StatusCode, unknown.StatusCode);
        Assert.Equal(known.Headers.Location, unknown.Headers.Location);
        Assert.Equal(CookieShapes(known), CookieShapes(unknown));
        Assert.Equal(await known.Content.ReadAsStringAsync(Cancel), await unknown.Content.ReadAsStringAsync(Cancel));
        Assert.False(StartsSession(unknown));
    }

    [Fact]
    public async Task The_code_page_of_an_unknown_address_looks_like_that_of_a_known_one()
    {
        var knownBrowser = hosts.NewBrowser();
        var unknownBrowser = hosts.NewBrowser();
        (await StartAsync(knownBrowser, SignInHosts.NormalizedPerson)).Dispose();
        (await StartAsync(unknownBrowser, SignInHosts.UnlistedPerson)).Dispose();

        using var knownPage = await knownBrowser.NavigateAsync(SignInFlow.CodePage);
        using var unknownPage = await unknownBrowser.NavigateAsync(SignInFlow.CodePage);

        Assert.Equal(HttpStatusCode.OK, knownPage.StatusCode);
        Assert.Equal(knownPage.StatusCode, unknownPage.StatusCode);
        Assert.Equal(
            WithoutVaryingParts(await knownPage.Content.ReadAsStringAsync(Cancel), SignInHosts.NormalizedPerson),
            WithoutVaryingParts(await unknownPage.Content.ReadAsStringAsync(Cancel), SignInHosts.UnlistedPerson));
        Assert.Contains("If " + SignInHosts.UnlistedPerson + " is known", await unknownPage.Content.ReadAsStringAsync(Cancel), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_code_typed_for_an_unknown_address_is_counted_like_any_wrong_code_and_never_signs_in()
    {
        var browser = hosts.NewBrowser();
        (await StartAsync(browser, SignInHosts.UnlistedPerson)).Dispose();

        using var first = await SignInFlow.TypeCodeAsync(browser, "ABCD-EFGH");
        Assert.Equal(HttpStatusCode.BadRequest, first.StatusCode);
        Assert.Contains("You can try 4 more times.", await first.Content.ReadAsStringAsync(Cancel), StringComparison.Ordinal);
        Assert.False(StartsSession(first));

        for (var attempt = 2; attempt < 5; attempt++)
        {
            (await SignInFlow.TypeCodeAsync(browser, "ABCD-EFGH")).Dispose();
        }

        using var last = await SignInFlow.TypeCodeAsync(browser, "ABCD-EFGH");
        Assert.Equal(HttpStatusCode.Gone, last.StatusCode);
        Assert.False(StartsSession(last));
    }

    [Fact]
    public async Task The_refusal_is_logged_for_the_operator_without_the_address()
    {
        using var response = await StartAsync(hosts.NewBrowser(), SignInHosts.UnlistedPerson);
        var requestId = RequestId(response);

        var refusal = Assert.Single(SignInLog(requestId));
        Assert.Equal(LogLevel.Information, refusal.Level);
        Assert.Contains("may not sign in", refusal.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(hosts.Logs.Entries, entry => entry.Message.Contains(SignInHosts.UnlistedPerson, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task A_known_address_is_not_logged_as_refused()
    {
        using var response = await StartAsync(hosts.NewBrowser(), SignInHosts.NormalizedPerson);
        var requestId = RequestId(response);

        var started = Assert.Single(SignInLog(requestId));
        Assert.DoesNotContain("may not sign in", started.Message, StringComparison.Ordinal);
    }

    /// <summary>What the sign-in page logged about the sign-in request <paramref name="requestId"/>.</summary>
    private IEnumerable<(string Category, LogLevel Level, string Message)> SignInLog(string requestId) =>
        hosts.Logs.Entries.Where(entry => string.Equals(entry.Category, "MagicLink.SignIn", StringComparison.Ordinal) && entry.Message.Contains(requestId, StringComparison.Ordinal));

    /// <summary>Opens the sign-in page through the application and types <paramref name="email"/>; returns the answer.</summary>
    private static async Task<HttpResponseMessage> StartAsync(Browser browser, string email)
    {
        HtmlForm form;
        using (var emailForm = await SignInFlow.OpenEmailFormAsync(browser, AppMe))
        {
            form = await HtmlForm.ReadAsync(emailForm);
        }

        return await form.SubmitAsync(browser, ("email", email));
    }

    /// <summary>Each cookie the answer sets, with its value replaced by its length: what an onlooker can compare.</summary>
    private static List<string> CookieShapes(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var cookies)
            ? [.. cookies.Select(static cookie =>
            {
                var equals = cookie.IndexOf('=', StringComparison.Ordinal);
                var end = cookie.IndexOf(';', StringComparison.Ordinal);
                end = end < 0 ? cookie.Length : end;

                // The expiry is to the second, so two answers a moment apart may differ there.
                var attributes = string.Join(';', cookie[end..].Split(';').Where(static part => !part.TrimStart().StartsWith("expires=", StringComparison.OrdinalIgnoreCase)));
                return cookie[..equals] + "=<" + (end - equals - 1) + ">" + attributes;
            }).Order(StringComparer.Ordinal)]
            : [];

    /// <summary>The sign-in request named by the binding cookie the answer sets.</summary>
    private static string RequestId(HttpResponseMessage response)
    {
        var cookie = response.Headers.GetValues("Set-Cookie").Single(static cookie => cookie.StartsWith(SignInRequest.BindingCookie + "=", StringComparison.Ordinal));
        var value = cookie[(SignInRequest.BindingCookie.Length + 1)..];
        return value[..value.IndexOf('.', StringComparison.Ordinal)];
    }

    /// <summary>The page with the typed address and the antiforgery token, which differ per browser, taken out.</summary>
    private static string WithoutVaryingParts(string html, string email)
    {
        html = html.Replace(email, "<email>", StringComparison.Ordinal);
        var marker = "name=\"__RequestVerificationToken\" value=\"";
        var start = html.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
        {
            return html;
        }

        start += marker.Length;
        return html[..start] + html[html.IndexOf('"', start)..];
    }

    private static bool StartsSession(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var cookies)
        && cookies.Any(static cookie => cookie.StartsWith(MagicLinkSession.CookieName + "=", StringComparison.Ordinal));
}
