using MagicLink;
using OpenIddict.Server;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Hosts MagicLink.NET in an ASP.NET Core app.</summary>
public static class MagicLinkServiceCollectionExtensions
{
    /// <summary>
    /// Adds MagicLink.NET: an OpenID Connect provider on OpenIddict that signs people in with a link or a short code sent
    /// by email. Map its endpoints with <c>MapMagicLink</c>.
    /// </summary>
    /// <param name="services">The app's services.</param>
    /// <param name="configure">
    /// Sets what depends on the host: at least the issuer, with <c>SetIssuer</c>, and the signing and encryption keys,
    /// for example with <c>AddSigningCertificate</c>.
    /// </param>
    /// <returns>
    /// OpenIddict's builder. Add a store to it with <c>AddCore</c>, for example the PostgreSQL store of the
    /// MagicLink.NET.PostgreSql package: <c>.AddCore(core =&gt; core.UsePostgreSql(...))</c>.
    /// </returns>
    /// <remarks>
    /// MagicLink.NET also needs a source of who may sign in: <see cref="SignInSourceRegistration.AddMagicLinkSignInFile"/>,
    /// <see cref="SignInSourceRegistration.AddMagicLinkSignInDatabase"/> or an own <see cref="ISignInSource"/> registered
    /// with <see cref="SignInSourceRegistration.AddMagicLinkSignInSource{TSource}"/>. The sign-in email goes over SMTP
    /// with the settings in <see cref="MagicLinkSmtpOptions"/>, unless the app registers its own
    /// <see cref="ISignInMessageSender"/>. The app keeps its own authentication: MagicLink.NET's session is never the
    /// default scheme. MagicLink.NET does not start while any of this is missing or wrong, and says why.
    /// </remarks>
    public static OpenIddictBuilder AddMagicLink(this IServiceCollection services, Action<OpenIddictServerBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        return services.AddOpenIddict().AddMagicLinkServer(configure);
    }
}
