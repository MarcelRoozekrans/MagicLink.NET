using ZeroAlloc.ORM;

namespace MagicLink.PostgreSql;

/// <summary>One row of <c>magiclink.tokens</c>, in column order.</summary>
internal sealed record TokenRow(
    Guid Id,
    string ConcurrencyToken,
    Guid? ApplicationId,
    Guid? AuthorizationId,
    DateTimeOffset? CreationDate,
    DateTimeOffset? ExpirationDate,
    DateTimeOffset? RedemptionDate,
    string? Payload,
    [Json] string? Properties,
    string? ReferenceId,
    string? Status,
    string? Subject,
    string? Type);
