namespace MagicLink.PostgreSql;

/// <summary>
/// OpenIddict's optional filters of FindAsync and RevokeAsync. An empty argument filters nothing, as in OpenIddict's
/// own stores. A client id that is no uuid can match no row.
/// </summary>
internal readonly record struct StoreFilter(string? Subject, Guid? Client, string? Status, string? Type, bool MatchesNothing)
{
    /// <summary>At most this many rows are pruned per statement, so a large prune never holds long locks.</summary>
    internal const int PruneBatch = 1_000;

    /// <summary>The most batches one prune runs, as in OpenIddict's own stores.</summary>
    internal const int PruneBatches = 1_000;

    internal static StoreFilter From(string? subject, string? client, string? status, string? type)
    {
        Guid? clientId = null;
        var matchesNothing = false;
        if (!string.IsNullOrEmpty(client))
        {
            if (StoreIds.TryParse(client, out var parsed))
            {
                clientId = parsed;
            }
            else
            {
                matchesNothing = true;
            }
        }

        return new StoreFilter(NullIfEmpty(subject), clientId, NullIfEmpty(status), NullIfEmpty(type), matchesNothing);
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;
}
