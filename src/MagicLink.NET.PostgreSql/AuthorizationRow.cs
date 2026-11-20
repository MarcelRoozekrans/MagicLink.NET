using ZeroAlloc.ORM;

namespace MagicLink.PostgreSql;

/// <summary>One row of <c>magiclink.authorizations</c>, in column order.</summary>
internal sealed record AuthorizationRow(
    Guid Id,
    string ConcurrencyToken,
    Guid? ApplicationId,
    DateTimeOffset? CreationDate,
    [Json] string? Properties,
    string[] Scopes,
    string? Status,
    string? Subject,
    string? Type);
