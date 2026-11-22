using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using MagicLink;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the source of who may sign in and with which groups (ADR-014). There is one source: the one registered
/// last replaces any registered before it.
/// </summary>
public static class SignInSourceRegistration
{
    /// <summary>
    /// Reads who may sign in, and their groups, from a file kept in git; see <see cref="MagicLinkSignInFileOptions"/> for
    /// its format. MagicLink.NET does not start when the file cannot be read or is wrong, and says why.
    /// </summary>
    /// <param name="services">The host's services.</param>
    /// <param name="configure">Sets the path of the file.</param>
    public static IServiceCollection AddMagicLinkSignInFile(this IServiceCollection services, Action<MagicLinkSignInFileOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);
        services.Replace(ServiceDescriptor.Singleton<ISignInSource, SignInFileSource>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<MagicLinkSignInFileOptions>, SignInFileCheck>());
        services.AddOptions<MagicLinkSignInFileOptions>().ValidateOnStart();
        return services;
    }

    /// <summary>
    /// Reads who may sign in, and their groups, from an existing PostgreSQL or SQL Server database with a query; see
    /// <see cref="MagicLinkSignInDatabaseOptions"/> for what the query returns. MagicLink.NET only reads from it, and does
    /// not start when the query fails, and says why.
    /// </summary>
    /// <param name="services">The host's services.</param>
    /// <param name="provider">
    /// The ADO.NET provider of the database: <c>NpgsqlFactory.Instance</c> for PostgreSQL, or
    /// <c>SqlClientFactory.Instance</c> for SQL Server.
    /// </param>
    /// <param name="configure">Sets the connection string and the query.</param>
    public static IServiceCollection AddMagicLinkSignInDatabase(this IServiceCollection services, DbProviderFactory provider, Action<MagicLinkSignInDatabaseOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);
        services.Replace(ServiceDescriptor.Singleton(new SignInDatabaseProvider(provider)));
        services.Replace(ServiceDescriptor.Singleton<ISignInSource, SignInDatabaseSource>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<MagicLinkSignInDatabaseOptions>, SignInDatabaseCheck>());
        services.AddOptions<MagicLinkSignInDatabaseOptions>().ValidateOnStart();
        return services;
    }

    /// <summary>Uses an own source, such as one that asks an API or LDAP, created once for the whole process.</summary>
    /// <typeparam name="TSource">The source.</typeparam>
    /// <param name="services">The host's services.</param>
    public static IServiceCollection AddMagicLinkSignInSource<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TSource>(this IServiceCollection services)
        where TSource : class, ISignInSource
    {
        ArgumentNullException.ThrowIfNull(services);
        services.Replace(ServiceDescriptor.Singleton<ISignInSource, TSource>());
        return services;
    }
}
