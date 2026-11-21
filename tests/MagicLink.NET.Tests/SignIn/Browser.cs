using System.Net;

namespace MagicLink.Tests.SignIn;

/// <summary>
/// A browser for in-memory hosts: it keeps cookies per site and follows redirects from one host to another, the way a
/// person's browser moves between an application and MagicLink.NET.
/// </summary>
public sealed class Browser(IReadOnlyDictionary<string, HttpMessageHandler> hosts)
{
    private readonly CookieContainer _cookies = new();
    private readonly List<Uri> _visited = [];

    /// <summary>Every address this browser requested, in order.</summary>
    public IList<Uri> Visited => _visited;

    /// <summary>Sends one request, with this browser's cookies, without following a redirect.</summary>
    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
    {
        var uri = request.RequestUri!;
        _visited.Add(uri);

        var cookieHeader = _cookies.GetCookieHeader(uri);
        if (cookieHeader.Length > 0)
        {
            request.Headers.Add("Cookie", cookieHeader);
        }

        using var client = new HttpClient(hosts[uri.Host], disposeHandler: false);
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        if (response.Headers.TryGetValues("Set-Cookie", out var setCookies))
        {
            foreach (var setCookie in setCookies)
            {
                _cookies.SetCookies(uri, setCookie);
            }
        }

        return response;
    }

    /// <summary>
    /// Opens <paramref name="address"/> and follows redirects, until a page answers or the next address is on
    /// <paramref name="stopAtHost"/>; then that address is returned in the redirect's Location.
    /// </summary>
    public async Task<HttpResponseMessage> NavigateAsync(Uri address, string? stopAtHost = null)
    {
        for (var hop = 0; hop < 20; hop++)
        {
            var response = await SendAsync(new HttpRequestMessage(HttpMethod.Get, address));
            if ((int)response.StatusCode is < 300 or >= 400)
            {
                return response;
            }

            var next = new Uri(address, response.Headers.Location!);
            if (string.Equals(next.Host, stopAtHost, StringComparison.Ordinal))
            {
                response.Headers.Location = next;
                return response;
            }

            response.Dispose();
            address = next;
        }

        throw new InvalidOperationException("More than 20 redirects.");
    }

    /// <summary>Forgets every cookie of <paramref name="site"/>, as when a person signs out of that site.</summary>
    public void ForgetCookiesOf(Uri site)
    {
        foreach (Cookie cookie in _cookies.GetCookies(site))
        {
            cookie.Expired = true;
        }
    }
}
