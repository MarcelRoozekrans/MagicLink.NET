using System.Collections.Immutable;
using System.Text.Json;

namespace MagicLink.PostgreSql;

/// <summary>An OpenIddict token, as the MagicLink.NET PostgreSQL store keeps it.</summary>
public sealed class MagicLinkToken
{
    /// <summary>Gets the id the store gave the token, or <see langword="null"/> before it is created.</summary>
    public string? Id { get; internal set; }

    /// <summary>Gets the value that changes on every update; an update made with an older value fails.</summary>
    public string? ConcurrencyToken { get; internal set; }

    /// <summary>Gets or sets the id of the application the token was issued to.</summary>
    public string? ApplicationId { get; set; }

    /// <summary>Gets or sets the id of the authorization the token belongs to.</summary>
    public string? AuthorizationId { get; set; }

    /// <summary>Gets or sets when the token was created.</summary>
    public DateTimeOffset? CreationDate { get; set; }

    /// <summary>Gets or sets when the token expires.</summary>
    public DateTimeOffset? ExpirationDate { get; set; }

    /// <summary>Gets or sets when the token was redeemed.</summary>
    public DateTimeOffset? RedemptionDate { get; set; }

    /// <summary>Gets or sets the token payload, kept for reference tokens such as device and user codes.</summary>
    public string? Payload { get; set; }

    /// <summary>Gets or sets the reference id, the value a client presents for a reference token.</summary>
    public string? ReferenceId { get; set; }

    /// <summary>Gets or sets the status, such as <c>valid</c>, <c>redeemed</c> or <c>revoked</c>.</summary>
    public string? Status { get; set; }

    /// <summary>Gets or sets the subject the token was issued for.</summary>
    public string? Subject { get; set; }

    /// <summary>Gets or sets the token type.</summary>
    public string? Type { get; set; }

    /// <summary>Gets or sets additional properties.</summary>
    public ImmutableDictionary<string, JsonElement> Properties { get; set; } = ImmutableDictionary<string, JsonElement>.Empty;
}
