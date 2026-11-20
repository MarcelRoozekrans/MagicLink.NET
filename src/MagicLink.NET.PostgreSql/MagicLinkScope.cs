using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;

namespace MagicLink.PostgreSql;

/// <summary>An OpenIddict scope, as the MagicLink.NET PostgreSQL store keeps it.</summary>
public sealed class MagicLinkScope
{
    /// <summary>Gets the id the store gave the scope, or <see langword="null"/> before it is created.</summary>
    public string? Id { get; internal set; }

    /// <summary>Gets the value that changes on every update; an update made with an older value fails.</summary>
    public string? ConcurrencyToken { get; internal set; }

    /// <summary>Gets or sets the unique name of the scope.</summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets the description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the localized descriptions.</summary>
    public ImmutableDictionary<CultureInfo, string> Descriptions { get; set; } = ImmutableDictionary<CultureInfo, string>.Empty;

    /// <summary>Gets or sets the display name.</summary>
    public string? DisplayName { get; set; }

    /// <summary>Gets or sets the localized display names.</summary>
    public ImmutableDictionary<CultureInfo, string> DisplayNames { get; set; } = ImmutableDictionary<CultureInfo, string>.Empty;

    /// <summary>Gets or sets the resources (audiences) the scope grants access to.</summary>
    public ImmutableArray<string> Resources { get; set; } = [];

    /// <summary>Gets or sets additional properties.</summary>
    public ImmutableDictionary<string, JsonElement> Properties { get; set; } = ImmutableDictionary<string, JsonElement>.Empty;
}
