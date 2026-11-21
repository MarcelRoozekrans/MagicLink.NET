using System.Net;
using System.Text.RegularExpressions;

namespace MagicLink.Tests.SignIn;

/// <summary>The form on a sign-in page: where it posts to and its hidden fields, such as the antiforgery token.</summary>
internal sealed partial class HtmlForm(Uri action, IReadOnlyList<KeyValuePair<string, string>> hidden)
{
    public Uri Action => action;

    public IReadOnlyList<KeyValuePair<string, string>> Hidden => hidden;

    /// <summary>Reads the one form of <paramref name="page"/>, an answer from MagicLink.NET.</summary>
    public static async Task<HtmlForm> ReadAsync(HttpResponseMessage page)
    {
        var html = await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var form = FormTag().Match(html);
        Assert.True(form.Success, "The page has no form: " + html);
        var fields = HiddenInput().Matches(html)
            .Select(static match => KeyValuePair.Create(WebUtility.HtmlDecode(match.Groups["name"].Value), WebUtility.HtmlDecode(match.Groups["value"].Value)))
            .ToList();
        return new HtmlForm(new Uri(SignInHosts.MagicLink, WebUtility.HtmlDecode(form.Groups["action"].Value)), fields);
    }

    /// <summary>True when <paramref name="html"/> holds a form.</summary>
    public static bool IsIn(string html) => FormTag().IsMatch(html);

    /// <summary>Posts the form with its hidden fields and <paramref name="fields"/>, as pressing its button does.</summary>
    public Task<HttpResponseMessage> SubmitAsync(Browser browser, params (string Name, string Value)[] fields) =>
        browser.SendAsync(new HttpRequestMessage(HttpMethod.Post, action)
        {
            Content = new FormUrlEncodedContent([.. hidden, .. fields.Select(static field => KeyValuePair.Create(field.Name, field.Value))]),
        });

    [GeneratedRegex("<form method=\"post\" action=\"(?<action>[^\"]*)\">", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex FormTag();

    [GeneratedRegex("<input type=\"hidden\" name=\"(?<name>[^\"]*)\" value=\"(?<value>[^\"]*)\">", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex HiddenInput();
}
