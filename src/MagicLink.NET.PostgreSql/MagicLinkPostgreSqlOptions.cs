namespace MagicLink.PostgreSql;

/// <summary>Settings of the MagicLink.NET PostgreSQL store.</summary>
public sealed class MagicLinkPostgreSqlOptions
{
    /// <summary>
    /// Gets or sets the connection string of the PostgreSQL database that holds the store. Required: the host fails
    /// at startup without it. The store keeps its tables in schema <c>magiclink</c>, whatever search path is given.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets whether the store applies its migrations when the host starts, under a PostgreSQL advisory lock so
    /// several instances can start at once. Defaults to <see langword="true"/>. Switch it off where a DBA applies the
    /// scripts, which ship in the package under <c>sql/</c>.
    /// </summary>
    public bool ApplyMigrationsOnStartup { get; set; } = true;
}
