using System.Collections.Immutable;
using System.Text.Json;

namespace MagicLink.PostgreSql;

/// <summary>An OpenIddict authorization, as the MagicLink.NET PostgreSQL store keeps it.</summary>
public sealed class MagicLinkAuthorization
{
    /// <summary>Gets the id the store gave the authorization, or <see langword="null"/> before it is created.</summary>
    public string? Id { get; internal set; }

    /// <summary>Gets the value that changes on every update; an update made with an older value fails.</summary>
    public string? ConcurrencyToken { get; internal set; }

    /// <summary>Gets or sets the id of the application the authorization belongs to.</summary>
    public string? ApplicationId { get; set; }

    /// <summary>Gets or sets when the authorization was created.</summary>
    public DateTimeOffset? CreationDate { get; set; }

    /// <summary>Gets or sets the granted scopes.</summary>
    public ImmutableArray<string> Scopes { get; set; } = [];

    /// <summary>Gets or sets the status, such as <c>valid</c> or <c>revoked</c>.</summary>
    public string? Status { get; set; }

    /// <summary>Gets or sets the subject the authorization was given to.</summary>
    public string? Subject { get; set; }

    /// <summary>Gets or sets the type, <c>ad-hoc</c> or <c>permanent</c>.</summary>
    public string? Type { get; set; }

    /// <summary>Gets or sets additional properties.</summary>
    public ImmutableDictionary<string, JsonElement> Properties { get; set; } = ImmutableDictionary<string, JsonElement>.Empty;
}
