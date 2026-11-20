namespace MagicLink.PostgreSql;

/// <summary>
/// The store runs hand-written SQL and has no runtime LINQ provider, so OpenIddict's <c>IQueryable</c> overloads
/// throw. OpenIddict's server never calls them.
/// </summary>
internal static class QueryableNotSupported
{
    internal static NotSupportedException For(string store, string method) =>
        new($"{store}.{method} with an IQueryable query is not supported: the MagicLink.NET PostgreSQL store has no runtime LINQ. Use the store's other members.");
}
