using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace MagicLink;

/// <summary>Registers MagicLink.NET's OpenID Connect server on OpenIddict.</summary>
internal static class MagicLinkServer
{
    /// <summary>
    /// Adds the OpenID Connect server: the authorization code flow with PKCE for browser sign-in, the device
    /// authorization grant for command-line tools, refresh tokens for applications that ask for <c>offline_access</c>,
    /// the person's session cookie, the sign-in page, and user info. The host still registers OpenIddict's store, through
    /// <c>AddCore</c>; it must store tokens, as the PostgreSQL store does.
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
            ConfigureServer(options);
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
            .Validate<IServiceProviderIsService>(
                static (_, services) => services.IsService(typeof(ISignInSource)),
                "MagicLink.NET needs a source of who may sign in, for example a file with AddMagicLinkSignInFile: without one it cannot tell who may sign in.")
            .ValidateOnStart();

        // The sign-in page: its forms carry antiforgery tokens, and its email goes out in the background (issue #6).
        builder.Services.AddAntiforgery();
        builder.Services.TryAddSingleton(TimeProvider.System);
        AddSignInMail(builder.Services);

        return builder;
    }

    /// <summary>The endpoints, flows, scopes and claims MagicLink.NET's server uses, before the host's own settings.</summary>
    private static void ConfigureServer(OpenIddictServerBuilder options)
    {
        options.SetAuthorizationEndpointUris(MagicLinkEndpoints.AuthorizationPath)
            .SetTokenEndpointUris(MagicLinkEndpoints.TokenPath)
            .SetUserInfoEndpointUris(MagicLinkEndpoints.UserInfoPath)
            .SetDeviceAuthorizationEndpointUris(MagicLinkEndpoints.DeviceAuthorizationPath)
            .SetEndUserVerificationEndpointUris(MagicLinkEndpoints.VerificationPath);

        options.AllowAuthorizationCodeFlow()
            .RequireProofKeyForCodeExchange();

        // An application extends a session with a refresh token instead of a new email (Spec: run an OIDC provider,
        // criterion 2). Each refresh gives a new refresh token, OpenIddict's default.
        options.AllowRefreshTokenFlow();

        // A command-line tool signs a person in with a user code the person types in a browser (Spec: run an OIDC
        // provider, criterion 3).
        options.AllowDeviceAuthorizationFlow();

        options.RegisterScopes(Scopes.OpenId, Scopes.Email, Scopes.OfflineAccess);
        options.RegisterClaims(Claims.Subject, Claims.Email, Claims.EmailVerified, Claims.AuthenticationTime, MagicLinkIdentity.GroupsClaim);

        // Applications and their APIs check access tokens as plain signed JWTs against the published keys, as with
        // any other identity provider, so access tokens are signed but not encrypted.
        options.DisableAccessTokenEncryption();

        AddUserInfoClaims(options);

        options.UseAspNetCore()
            .EnableAuthorizationEndpointPassthrough()
            .EnableTokenEndpointPassthrough()
            .EnableEndUserVerificationEndpointPassthrough();
    }

    /// <summary>
    /// OpenIddict answers user info itself, but returns no email_verified and no groups; this adds the first next to the
    /// email, and the groups the access token carries.
    /// </summary>
    private static void AddUserInfoClaims(OpenIddictServerBuilder options)
    {
        options.AddEventHandler<OpenIddictServerEvents.HandleUserInfoRequestContext>(handler => handler
            .UseInlineHandler(static context =>
            {
                if (context.Email is not null)
                {
                    context.Claims[Claims.EmailVerified] = true;
                }

                var groups = context.AccessTokenPrincipal?.GetClaims(MagicLinkIdentity.GroupsClaim) ?? [];
                if (!groups.IsEmpty)
                {
                    context.Claims[MagicLinkIdentity.GroupsClaim] = new OpenIddictParameter(groups!);
                }

                return default;
            })
            .SetOrder(OpenIddictServerHandlers.UserInfo.AttachClaims.Descriptor.Order + 1_000)
            .SetType(OpenIddictServerHandlerType.Custom));
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
