namespace MagicLink.Host;

/// <summary>
/// The reverse proxies whose forwarded headers the container trusts (spec "confirm sign-in with a link or a code",
/// criterion 8). Without any, the client's IP address is the connection's remote address, which behind a proxy is the
/// proxy's, so every person would count against the same per-client sign-in limit.
/// </summary>
internal sealed class ForwardedHeadersSettings
{
    /// <summary>IP addresses of trusted proxies, such as <c>10.0.0.5</c>.</summary>
    public List<string> KnownProxies { get; set; } = [];

    /// <summary>Networks of trusted proxies in CIDR notation, such as <c>10.0.0.0/8</c>.</summary>
    public List<string> KnownNetworks { get; set; } = [];
}
