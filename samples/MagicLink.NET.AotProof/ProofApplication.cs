using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace MagicLink.AotProof;

/// <summary>An OpenIddict application kept in memory by <see cref="InMemoryApplicationStore"/>.</summary>
public sealed class ProofApplication
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
    public string? ApplicationType { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? ClientType { get; set; }
    public string? ConsentType { get; set; }
    public string? DisplayName { get; set; }
    public ImmutableDictionary<CultureInfo, string> DisplayNames { get; set; } = ImmutableDictionary<CultureInfo, string>.Empty;
    public JsonWebKeySet? JsonWebKeySet { get; set; }
    public ImmutableArray<string> Permissions { get; set; } = [];
    public ImmutableArray<string> PostLogoutRedirectUris { get; set; } = [];
    public ImmutableDictionary<string, JsonElement> Properties { get; set; } = ImmutableDictionary<string, JsonElement>.Empty;
    public ImmutableArray<string> RedirectUris { get; set; } = [];
    public ImmutableArray<string> Requirements { get; set; } = [];
    public ImmutableDictionary<string, string> Settings { get; set; } = ImmutableDictionary<string, string>.Empty;
}
