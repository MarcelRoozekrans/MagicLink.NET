namespace MagicLink.PostgreSql;

/// <summary>Ids are uuids in the database and strings to OpenIddict.</summary>
internal static class StoreIds
{
    internal static Guid New() => Guid.CreateVersion7();

    internal static string ToText(Guid id) => id.ToString("D");

    /// <summary>Parses an id from OpenIddict. Anything that is no uuid matches no row.</summary>
    internal static bool TryParse(string? id, out Guid value) => Guid.TryParse(id, out value);

    /// <summary>Parses an optional reference to another row; an id that is no uuid is a caller error.</summary>
    internal static Guid? ParseReference(string? id, string parameterName)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        return Guid.TryParse(id, out var value)
            ? value
            : throw new ArgumentException($"'{id}' is not an identifier of this store.", parameterName);
    }

    internal static string? ToText(Guid? id) => id is { } value ? ToText(value) : null;
}
