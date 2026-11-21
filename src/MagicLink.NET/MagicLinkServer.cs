using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OpenIddict.Server;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace MagicLink;

/// <summary>Registers MagicLink.NET's OpenID Connect server on OpenIddict.</summary>
internal static class MagicLinkServer
{
    /// <summary>
    /// Adds the OpenID Connect server for browser sign-in: the authorization code flow with PKCE, the person's session
    /// cookie, the sign-in page, and user info. The host still registers OpenIddict's store, through <c>AddCore</c>.
    /// </summary>
    /// <param name="builder">OpenIddict's builder.</param>
    /// <param name="configure">
    /// Sets what depends on the host: at least the issuer, with <c>SetIssuer</c>, and the signing and encryption keys,
    /// for example with <c>AddSigningCertificate</c>.
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

        // The link in the sign-in email is built on the issuer, never on a request's Host header, so it must be set.
        builder.Services.AddOptions<OpenIddictServerOptions>()
            .Validate(static options => options.Issuer is not null, "MagicLink.NET needs the issuer set, for example with SetIssuer: the link in the sign-in email is built on it.")
            .ValidateOnStart();

        builder.Services.AddOptions<MagicLinkOptions>()
            .Validate(static options => options.CodeLifetime > TimeSpan.Zero, "MagicLinkOptions.CodeLifetime must be positive.")
            .Validate(static options => options.MaxCodeAttempts > 0, "MagicLinkOptions.MaxCodeAttempts must be at least 1.")
            .ValidateOnStart();

        // The sign-in page: its forms carry antiforgery tokens, and its email goes out in the background (issue #6).
        builder.Services.AddAntiforgery();
        builder.Services.TryAddSingleton(TimeProvider.System);
        AddSignInMail(builder.Services);

        return builder;
    }

    /// <summary>
    /// The sign-in email goes to a queue in the process that a background service sends from, with retries (ADR-015).
    /// SMTP is the default sender; another registration of <see cref="ISignInMessageSender"/> replaces it, before or
    /// after this call.
    /// </summary>
    private static void AddSignInMail(IServiceCollection services)
    {
        services.TryAddSingleton<SignInMailQueue>();
        services.AddSignInMailDeliveryResiliencePolicies();
        services.AddHostedService<SignInMailWorker>();
        services.TryAddSingleton<ISignInMessageSender, SmtpSignInMessageSender>();

        // Checked at start with the other settings, but only while SMTP is the sender in use.
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<MagicLinkSmtpOptions>, SmtpSettingsCheck>());
        services.AddOptions<MagicLinkSmtpOptions>().ValidateOnStart();
    }
}
