using System.Collections.Immutable;

namespace MagicLink.PostgreSql;

internal static class ImmutableArrayExtensions
{
    /// <summary>A <c>default</c> immutable array, which OpenIddict may hand over, as an empty one.</summary>
    internal static ImmutableArray<T> OrEmpty<T>(this ImmutableArray<T> array) => array.IsDefault ? [] : array;
}
