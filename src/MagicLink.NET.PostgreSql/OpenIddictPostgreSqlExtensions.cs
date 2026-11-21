using MagicLink.PostgreSql;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using System.Data.Async;
using ZeroAlloc.Scheduling;
using ZeroAlloc.Scheduling.Orm;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers the MagicLink.NET PostgreSQL store on OpenIddict.</summary>
public static class OpenIddictPostgreSqlExtensions
{
    /// <summary>
    /// Keeps OpenIddict's applications, authorizations, scopes and tokens in PostgreSQL, through the MagicLink.NET
    /// store. The store uses its own data source, apart from any the host registers, and by default applies its
    /// migrations when the host starts and prunes expired data every hour.
    /// </summary>
    /// <param name="builder">OpenIddict's core builder.</param>
    /// <param name="configure">
    /// Sets the store's options; <see cref="MagicLinkPostgreSqlOptions.ConnectionString"/> is required. It runs once
    /// here as well, to read <see cref="MagicLinkPostgreSqlOptions.PruneExpiredData"/>.
    /// </param>
    /// <returns>The builder, for chaining.</returns>
    /// <remarks>
    /// The pruning job runs on ZeroAlloc.Scheduling. When the host has not called <c>AddScheduling</c>, this method
    /// calls it and keeps the jobs in the store's database. A host that uses ZeroAlloc.Scheduling itself must call
    /// <c>AddScheduling</c> before this method; the job then runs on the host's job store.
    /// </remarks>
    public static OpenIddictCoreBuilder UsePostgreSql(this OpenIddictCoreBuilder builder, Action<MagicLinkPostgreSqlOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var settings = new MagicLinkPostgreSqlOptions();
        configure(settings);

        builder.Services.AddOptions<MagicLinkPostgreSqlOptions>()
            .Configure(configure)
            .Validate(
                static options => !string.IsNullOrWhiteSpace(options.ConnectionString),
                "MagicLinkPostgreSqlOptions.ConnectionString is required: set it in UsePostgreSql to the PostgreSQL database that holds the store.")
            .ValidateOnStart();

        builder.Services.TryAddSingleton(static provider =>
            new StoreDatabase(StoreDataSource.Create(provider.GetRequiredService<IOptions<MagicLinkPostgreSqlOptions>>().Value.ConnectionString!)));
        var ownsJobStore = settings.PruneExpiredData && AddPruningJob(builder.Services);
        builder.Services.AddHostedService(provider => new StoreMigrationService(
            provider.GetRequiredService<StoreDatabase>(),
            provider.GetRequiredService<IOptions<MagicLinkPostgreSqlOptions>>(),
            ownsJobStore));

        return builder
            .SetDefaultApplicationEntity<MagicLinkApplication>()
            .SetDefaultAuthorizationEntity<MagicLinkAuthorization>()
            .SetDefaultScopeEntity<MagicLinkScope>()
            .SetDefaultTokenEntity<MagicLinkToken>()
            .ReplaceApplicationStore<MagicLinkApplication, MagicLinkApplicationStore>()
            .ReplaceAuthorizationStore<MagicLinkAuthorization, MagicLinkAuthorizationStore>()
            .ReplaceScopeStore<MagicLinkScope, MagicLinkScopeStore>()
            .ReplaceTokenStore<MagicLinkToken, MagicLinkTokenStore>();
    }

    /// <summary>
    /// Adds the pruning job. Without the host's own <c>AddScheduling</c>, it also adds the scheduler with a job store on
    /// the store's database, and returns <see langword="true"/>: the store then migrates the job table too.
    /// </summary>
    private static bool AddPruningJob(IServiceCollection services)
    {
        if (services.Any(static descriptor => descriptor.ServiceType == typeof(SchedulingWorkerService)))
        {
            new StoreSchedulingBuilder(services).AddPruneExpiredDataJob();
            return false;
        }

        // The keyed connection comes from the store's data source, whose search path is the store's schema, so the
        // job table lands next to the store's tables.
        services.TryAddKeyedScoped(
            StoreDatabase.JobStoreConnectionKey,
            static (provider, _) => provider.GetRequiredService<StoreDatabase>().Connect());
        services.AddScheduling()
            .WithOrm(OrmSchedulingDialect.Postgres, StoreDatabase.JobStoreConnectionKey)
            .AddPruneExpiredDataJob();
        return true;
    }

    /// <summary>Adds the job to a scheduler the host registered itself, whose builder this method does not have.</summary>
    private sealed class StoreSchedulingBuilder(IServiceCollection services) : ISchedulingBuilder
    {
        public IServiceCollection Services { get; } = services;
    }
}
