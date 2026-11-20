using MagicLink.PostgreSql;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers the MagicLink.NET PostgreSQL store on OpenIddict.</summary>
public static class OpenIddictPostgreSqlExtensions
{
    /// <summary>
    /// Keeps OpenIddict's applications, authorizations, scopes and tokens in PostgreSQL, through the MagicLink.NET
    /// store. The store uses its own data source, apart from any the host registers, and by default applies its
    /// migrations when the host starts.
    /// </summary>
    /// <param name="builder">OpenIddict's core builder.</param>
    /// <param name="configure">Sets the store's options; <see cref="MagicLinkPostgreSqlOptions.ConnectionString"/> is required.</param>
    /// <returns>The builder, for chaining.</returns>
    public static OpenIddictCoreBuilder UsePostgreSql(this OpenIddictCoreBuilder builder, Action<MagicLinkPostgreSqlOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddOptions<MagicLinkPostgreSqlOptions>()
            .Configure(configure)
            .Validate(
                static options => !string.IsNullOrWhiteSpace(options.ConnectionString),
                "MagicLinkPostgreSqlOptions.ConnectionString is required: set it in UsePostgreSql to the PostgreSQL database that holds the store.")
            .ValidateOnStart();

        builder.Services.TryAddSingleton(static provider =>
            new StoreDatabase(StoreDataSource.Create(provider.GetRequiredService<IOptions<MagicLinkPostgreSqlOptions>>().Value.ConnectionString!)));
        builder.Services.AddHostedService<StoreMigrationService>();

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
}
