namespace MagicLink;

/// <summary>
/// Settings for an existing PostgreSQL or SQL Server database that says who may sign in and with which groups (ADR-014).
/// MagicLink.NET only reads from it, and does not start while this database is the source and its query fails.
/// </summary>
/// <remarks>
/// The query gets the email address as the parameter <c>@email</c>, trimmed and in lower case, and returns one text
/// column: a row per group of that person. A row whose group is <c>NULL</c> lets the person sign in without that group,
/// and no row at all means the address may not sign in. A person without groups therefore needs one row with
/// <c>NULL</c>, which a left join gives:
/// <code>
/// SELECT m.group_name
/// FROM people p LEFT JOIN memberships m ON m.person_id = p.id
/// WHERE lower(p.email) = @email
/// </code>
/// Give MagicLink.NET an account that may only read: MagicLink.NET never writes to this database, and the account's
/// permissions are what keep a mistaken query from changing it. Each look-up runs the query in a transaction that is
/// always rolled back, which cleans up after such a query but cannot undo everything, such as a COMMIT in the query
/// itself or a sequence it advances. The query must return one result; an error in any of its statements keeps everyone
/// out. At startup MagicLink.NET runs the query once with a made-up address, and refuses to start when that fails,
/// returns other than one text column or more than one result, or lets the made-up address in.
/// </remarks>
public sealed class MagicLinkSignInDatabaseOptions
{
    /// <summary>The connection string of the database, for an account that may only read.</summary>
    public string? ConnectionString { get; set; }

    /// <summary>The query that returns the groups of the address in <c>@email</c>; see the remarks of this class.</summary>
    public string? Query { get; set; }
}
