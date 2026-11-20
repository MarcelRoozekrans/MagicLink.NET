using ZeroAlloc.ORM;

namespace MagicLink.PostgreSql;

/// <summary>One row of <c>magiclink.scopes</c>, in column order.</summary>
internal sealed record ScopeRow(
    Guid Id,
    string ConcurrencyToken,
    string? Name,
    string? Description,
    [Json] string? Descriptions,
    string? DisplayName,
    [Json] string? DisplayNames,
    string[] Resources,
    [Json] string? Properties);
