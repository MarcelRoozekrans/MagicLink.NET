namespace MagicLink.Host;

/// <summary>An application that may sign people in through MagicLink.NET.</summary>
internal sealed class ClientSettings
{
    /// <summary>The application's client id.</summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// The secret of a confidential client, such as a web app with a server side. Without it the client is public, such as
    /// a CLI or a single-page app, which proves itself with PKCE alone.
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>The name of the application, for people.</summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// The addresses MagicLink.NET may send the person back to with the authorization code. A client with at least one
    /// uses the authorization code flow with PKCE.
    /// </summary>
    public List<string> RedirectUris { get; set; } = [];

    /// <summary>
    /// Whether the client may sign in with the device flow, as a CLI does: the person types a user code in a browser. A
    /// client may use the device flow, the authorization code flow, or both.
    /// </summary>
    public bool DeviceFlow { get; set; }

    /// <summary>
    /// The scopes the application may ask for besides <c>openid</c>: <c>email</c>, and <c>offline_access</c> for refresh
    /// tokens.
    /// </summary>
    public List<string> Scopes { get; set; } = [];
}
