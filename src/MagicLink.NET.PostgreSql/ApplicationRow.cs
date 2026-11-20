using ZeroAlloc.ORM;

namespace MagicLink.PostgreSql;

/// <summary>One row of <c>magiclink.applications</c>, in column order.</summary>
internal sealed record ApplicationRow(
    Guid Id,
    string ConcurrencyToken,
    string? ApplicationType,
    string? ClientId,
    string? ClientSecret,
    string? ClientType,
    string? ConsentType,
    string? DisplayName,
    [Json] string? DisplayNames,
    [Json] string? JsonWebKeySet,
    string[] Permissions,
    string[] PostLogoutRedirectUris,
    string[] RedirectUris,
    string[] Requirements,
    [Json] string? Settings,
    [Json] string? Properties);
