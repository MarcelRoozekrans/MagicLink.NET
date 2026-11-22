using MagicLink;
using Microsoft.AspNetCore.Authentication;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;

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
    /// default scheme, and an app that relied on its only scheme being the default keeps that default. MagicLink.NET does not start while any of this is missing or wrong, and says why.
    /// </remarks>
    public static OpenIddictBuilder AddMagicLink(this IServiceCollection services, Action<OpenIddictServerBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = services.AddOpenIddict().AddMagicLinkServer(configure);
        services.PostConfigure<AuthenticationOptions>(static options => KeepTheAppsDefaultScheme(options, AutoDefaultSuppressed()));
        return builder;
    }

    /// <summary>The AppContext switch with which an app turns ASP.NET Core's implicit default scheme off.</summary>
    internal const string SuppressAutoDefaultSchemeSwitch = "Microsoft.AspNetCore.Authentication.SuppressAutoDefaultScheme";

    private static bool AutoDefaultSuppressed() => AppContext.TryGetSwitch(SuppressAutoDefaultSchemeSwitch, out var suppressed) && suppressed;

    /// <summary>
    /// ASP.NET Core makes an app's only scheme its default without the app naming it. MagicLink.NET adds two schemes of
    /// its own, its session and OpenIddict's, which would take that default away; this gives it back. A default the app
    /// set itself is left alone, and so is an app with more schemes, which had no implicit default before, and an app that
    /// switched the implicit default off with <see cref="SuppressAutoDefaultSchemeSwitch"/>.
    /// </summary>
    /// <param name="options">The app's authentication options.</param>
    /// <param name="autoDefaultSuppressed">Whether the app switched ASP.NET Core's implicit default off.</param>
    internal static void KeepTheAppsDefaultScheme(AuthenticationOptions options, bool autoDefaultSuppressed)
    {
        if (autoDefaultSuppressed || options.DefaultScheme is not null)
        {
            return;
        }

        var own = options.Schemes
            .Where(static scheme => !string.Equals(scheme.Name, MagicLinkSession.Scheme, StringComparison.Ordinal)
                && !string.Equals(scheme.Name, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, StringComparison.Ordinal))
            .Take(2)
            .ToList();
        if (own.Count == 1)
        {
            options.DefaultScheme = own[0].Name;
        }
    }
}
