using System.Net;
using System.Text.RegularExpressions;

namespace MagicLink.Host.Tests;

/// <summary>
/// One person's browser on the host: it keeps the cookies the host sets and follows redirects on the host.
/// </summary>
internal sealed partial class TestBrowser(HttpClient client)
{
    private readonly Dictionary<string, string> _cookies = new(StringComparer.Ordinal);

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    /// <summary>Sends one request with this browser's cookies, without following a redirect.</summary>
    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
    {
        if (_cookies.Count > 0)
        {
            request.Headers.Add("Cookie", string.Join("; ", _cookies.Select(static cookie => cookie.Key + "=" + cookie.Value)));
        }

        var response = await client.SendAsync(request, Cancel);
        if (response.Headers.TryGetValues("Set-Cookie", out var setCookies))
        {
            foreach (var setCookie in setCookies)
            {
                var pair = setCookie.Split(';', 2)[0].Split('=', 2);
                _cookies[pair[0].Trim()] = pair[1];
            }
        }

        return response;
    }

    /// <summary>
    /// Opens <paramref name="path"/> and follows redirects on the app, until a page answers or a redirect leaves the app;
    /// that redirect is returned.
    /// </summary>
    public async Task<HttpResponseMessage> NavigateAsync(string path)
    {
        for (var hop = 0; hop < 10; hop++)
        {
            var response = await SendAsync(new HttpRequestMessage(HttpMethod.Get, path));
            if ((int)response.StatusCode is < 300 or >= 400 || !OnApp(response.Headers.Location!))
            {
                return response;
            }

            path = PathOf(response.Headers.Location!);
            response.Dispose();
        }

        throw new InvalidOperationException("More than 10 redirects.");
    }

    /// <summary>Reads the one form on <paramref name="page"/> and posts it with its hidden fields and <paramref name="fields"/>.</summary>
    public async Task<HttpResponseMessage> SubmitFormAsync(HttpResponseMessage page, params (string Name, string Value)[] fields)
    {
        var html = await page.Content.ReadAsStringAsync(Cancel);
        var form = FormTag().Match(html);
        Assert.True(form.Success, "The page has no form: " + html);
        var hidden = HiddenInput().Matches(html)
            .Select(static match => KeyValuePair.Create(WebUtility.HtmlDecode(match.Groups["name"].Value), WebUtility.HtmlDecode(match.Groups["value"].Value)));
        return await SendAsync(new HttpRequestMessage(HttpMethod.Post, WebUtility.HtmlDecode(form.Groups["action"].Value))
        {
            Content = new FormUrlEncodedContent([.. hidden, .. fields.Select(static field => KeyValuePair.Create(field.Name, field.Value))]),
        });
    }

    /// <summary>
    /// The path and query of <paramref name="location"/>. MagicLink.NET builds some addresses on its issuer, which is not
    /// the address CI calls the host on, so the browser stays on the client's base address.
    /// </summary>
    public static string PathOf(Uri location) => location.IsAbsoluteUri ? location.PathAndQuery : location.OriginalString;

    private static bool OnApp(Uri location) =>
        !location.IsAbsoluteUri || !location.AbsoluteUri.StartsWith(TestSetup.RedirectUri, StringComparison.Ordinal);

    [GeneratedRegex("<form method=\"post\" action=\"(?<action>[^\"]*)\">", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex FormTag();

    [GeneratedRegex("<input type=\"hidden\" name=\"(?<name>[^\"]*)\" value=\"(?<value>[^\"]*)\">", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex HiddenInput();
}
