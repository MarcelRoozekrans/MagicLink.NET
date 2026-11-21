using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Server;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace MagicLink;

/// <summary>Registers MagicLink.NET's OpenID Connect server on OpenIddict.</summary>
internal static class MagicLinkServer
{
    /// <summary>
    /// Adds the OpenID Connect server for browser sign-in: the authorization code flow with PKCE, the person's session
    /// cookie, and user info. The host still registers OpenIddict's store, through <c>AddCore</c>.
    /// </summary>
    /// <param name="builder">OpenIddict's builder.</param>
    /// <param name="configure">
    /// Sets what depends on the host, at least the signing and encryption keys, for example with
    /// <c>AddSigningCertificate</c>.
    /// </param>
    public static OpenIddictBuilder AddMagicLinkServer(this OpenIddictBuilder builder, Action<OpenIddictServerBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.AddServer(options =>
        {
            options.SetAuthorizationEndpointUris(MagicLinkEndpoints.AuthorizationPath)
                .SetTokenEndpointUris(MagicLinkEndpoints.TokenPath)
                .SetUserInfoEndpointUris(MagicLinkEndpoints.UserInfoPath);

            options.AllowAuthorizationCodeFlow()
                .RequireProofKeyForCodeExchange();

            options.RegisterScopes(Scopes.OpenId, Scopes.Email);
            options.RegisterClaims(Claims.Subject, Claims.Email, Claims.EmailVerified, Claims.AuthenticationTime);

            // Applications and their APIs check access tokens as plain signed JWTs against the published keys, as with
            // any other identity provider, so access tokens are signed but not encrypted.
            options.DisableAccessTokenEncryption();

            // OpenIddict answers user info itself, but returns no email_verified; this adds it next to the email.
            options.AddEventHandler<OpenIddictServerEvents.HandleUserInfoRequestContext>(handler => handler
                .UseInlineHandler(static context =>
                {
                    if (context.Email is not null)
                    {
                        context.Claims[Claims.EmailVerified] = true;
                    }

                    return default;
                })
                .SetOrder(OpenIddictServerHandlers.UserInfo.AttachClaims.Descriptor.Order + 1_000)
                .SetType(OpenIddictServerHandlerType.Custom));

            options.UseAspNetCore()
                .EnableAuthorizationEndpointPassthrough()
                .EnableTokenEndpointPassthrough();

            configure(options);
        });

        builder.Services.AddAuthentication()
            .AddCookie(MagicLinkSession.Scheme, MagicLinkSession.Configure);

        return builder;
    }
}
