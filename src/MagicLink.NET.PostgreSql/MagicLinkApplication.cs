using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace MagicLink.PostgreSql;

/// <summary>An OpenIddict application (a client), as the MagicLink.NET PostgreSQL store keeps it.</summary>
public sealed class MagicLinkApplication
{
    /// <summary>Gets the id the store gave the application, or <see langword="null"/> before it is created.</summary>
    public string? Id { get; internal set; }

    /// <summary>Gets the value that changes on every update; an update made with an older value fails.</summary>
    public string? ConcurrencyToken { get; internal set; }

    /// <summary>Gets or sets the application type, such as <c>web</c> or <c>native</c>.</summary>
    public string? ApplicationType { get; set; }

    /// <summary>Gets or sets the client id.</summary>
    public string? ClientId { get; set; }

    /// <summary>Gets or sets the hashed client secret. OpenIddict hashes it; the store never sees a plain secret.</summary>
    public string? ClientSecret { get; set; }

    /// <summary>Gets or sets the client type, <c>public</c> or <c>confidential</c>.</summary>
    public string? ClientType { get; set; }

    /// <summary>Gets or sets the consent type.</summary>
    public string? ConsentType { get; set; }

    /// <summary>Gets or sets the display name.</summary>
    public string? DisplayName { get; set; }

    /// <summary>Gets or sets the localized display names.</summary>
    public ImmutableDictionary<CultureInfo, string> DisplayNames { get; set; } = ImmutableDictionary<CultureInfo, string>.Empty;

    /// <summary>Gets or sets the client's JSON Web Key Set.</summary>
    public JsonWebKeySet? JsonWebKeySet { get; set; }

    /// <summary>Gets or sets the permissions.</summary>
    public ImmutableArray<string> Permissions { get; set; } = [];

    /// <summary>Gets or sets the redirect URIs allowed after sign-out.</summary>
    public ImmutableArray<string> PostLogoutRedirectUris { get; set; } = [];

    /// <summary>Gets or sets the redirect URIs.</summary>
    public ImmutableArray<string> RedirectUris { get; set; } = [];

    /// <summary>Gets or sets the requirements, such as PKCE.</summary>
    public ImmutableArray<string> Requirements { get; set; } = [];

    /// <summary>Gets or sets the settings, such as token lifetimes.</summary>
    public ImmutableDictionary<string, string> Settings { get; set; } = ImmutableDictionary<string, string>.Empty;

    /// <summary>Gets or sets additional properties.</summary>
    public ImmutableDictionary<string, JsonElement> Properties { get; set; } = ImmutableDictionary<string, JsonElement>.Empty;
}
