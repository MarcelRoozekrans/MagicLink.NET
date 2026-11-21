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
    /// several instances can start at once. With <see cref="PruneExpiredData"/> on, that includes the job table of
    /// ZeroAlloc.Scheduling. Defaults to <see langword="true"/>. Switch it off where a DBA applies the scripts, which
    /// ship in the package under <c>sql/</c>.
    /// </summary>
    public bool ApplyMigrationsOnStartup { get; set; } = true;

    /// <summary>
    /// Gets or sets whether an hourly job deletes tokens and authorizations created more than 14 days ago that are no
    /// longer valid, through OpenIddict's <c>PruneAsync</c>. The job runs on ZeroAlloc.Scheduling, with its jobs in the store's database, so several instances run
    /// it once per hour in total. Defaults to <see langword="true"/>. It is read when <c>UsePostgreSql</c> runs, so set
    /// it in the callback passed there.
    /// </summary>
    public bool PruneExpiredData { get; set; } = true;
}
