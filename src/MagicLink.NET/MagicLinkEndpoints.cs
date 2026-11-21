using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace MagicLink;

/// <summary>
/// The OpenID Connect endpoints MagicLink.NET answers itself. OpenIddict validates each request before it gets here;
/// discovery, the key set and user info are answered by OpenIddict alone.
/// </summary>
internal static partial class MagicLinkEndpoints
{
    public const string AuthorizationPath = "connect/authorize";
    public const string TokenPath = "connect/token";
    public const string UserInfoPath = "connect/userinfo";

    /// <summary>Maps the authorization and token endpoints, and the sign-in page.</summary>
    public static IEndpointRouteBuilder MapMagicLinkEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapMethods("/" + AuthorizationPath, [HttpMethods.Get, HttpMethods.Post], new RequestDelegate(AuthorizeAsync));
        endpoints.MapPost("/" + TokenPath, new RequestDelegate(TokenAsync));
        endpoints.MapSignInPage();
        return endpoints;
    }

    /// <summary>
    /// Issues an authorization code to a person with a session. Without a session, or when the application asks the
    /// person to sign in again, it sends the browser to the sign-in page and back here afterwards.
    /// </summary>
    internal static async Task AuthorizeAsync(HttpContext context)
    {
        var request = context.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("OpenIddict did not attach the authorization request.");

        var session = await context.AuthenticateAsync(MagicLinkSession.Scheme).ConfigureAwait(false);
        var email = session.Succeeded ? session.Principal.FindFirst(Claims.Email)?.Value : null;
        var signedInAt = session.Properties?.IssuedUtc;

        if (email is null || signedInAt is null || MustSignInAgain(request, signedInAt.Value, context.RequestServices.GetRequiredService<TimeProvider>()))
        {
            if (request.HasPromptValue(PromptValues.None))
            {
                await context.ForbidAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, Error(Errors.LoginRequired, "The person is not signed in.")).ConfigureAwait(false);
                return;
            }

            await context.ChallengeAsync(MagicLinkSession.Scheme, new AuthenticationProperties { RedirectUri = ReturnUrl(context) }).ConfigureAwait(false);
            return;
        }

        // Asked on every authorization, not once at sign-in, so a person taken out of the source, or moved to other
        // groups, gets no tokens with what no longer holds.
        var access = await context.RequestServices.GetRequiredService<ISignInSource>().LookUpAsync(email, context.RequestAborted).ConfigureAwait(false);
        if (!access.MaySignIn)
        {
            Refused(Logger(context));
            await context.SignOutAsync(MagicLinkSession.Scheme).ConfigureAwait(false);
            await context.ForbidAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, Error(Errors.AccessDenied, "This email address may not sign in.")).ConfigureAwait(false);
            return;
        }

        var identity = MagicLinkIdentity.Create(email, signedInAt.Value, request.GetScopes(), access.Groups);
        await context.SignInAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, new ClaimsPrincipal(identity)).ConfigureAwait(false);
    }

    /// <summary>Exchanges an authorization code for tokens carrying the identity the code was issued for.</summary>
    internal static async Task TokenAsync(HttpContext context)
    {
        var request = context.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("OpenIddict did not attach the token request.");

        // OpenIddict rejects every grant type the server does not allow, so only the code grant gets here.
        if (!request.IsAuthorizationCodeGrantType())
        {
            throw new InvalidOperationException($"The grant type '{request.GrantType}' is not handled.");
        }

        var result = await context.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            await context.ForbidAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, Error(Errors.InvalidGrant, "The authorization code is no longer valid.")).ConfigureAwait(false);
            return;
        }

        MagicLinkIdentity.KeepGroupsAList(result.Principal);
        await context.SignInAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, result.Principal).ConfigureAwait(false);
    }

    /// <summary>
    /// True when the application asked the person to sign in again (<c>prompt=login</c>) or set a <c>max_age</c> the
    /// session is older than.
    /// </summary>
    private static bool MustSignInAgain(OpenIddictRequest request, DateTimeOffset signedInAt, TimeProvider time)
    {
        if (request.HasPromptValue(PromptValues.Login))
        {
            return true;
        }

        // In seconds, as max_age is: a long can exceed what TimeSpan.FromSeconds takes.
        return request.MaxAge is { } maxAge && (time.GetUtcNow() - signedInAt).TotalSeconds > maxAge;
    }

    /// <summary>
    /// This authorization request, to come back to after signing in. Without <c>prompt=login</c>, so the new session
    /// is accepted on return instead of sending the person to sign in once more.
    /// </summary>
    private static string ReturnUrl(HttpContext context)
    {
        var query = new QueryBuilder();
        IEnumerable<KeyValuePair<string, StringValues>> parameters = context.Request.HasFormContentType ? context.Request.Form : context.Request.Query;
        foreach (var (name, values) in parameters)
        {
            if (string.Equals(name, Parameters.Prompt, StringComparison.Ordinal))
            {
                var prompt = WithoutLogin(values.ToString());
                if (prompt.Length > 0)
                {
                    query.Add(name, prompt);
                }

                continue;
            }

            if (string.Equals(name, Parameters.MaxAge, StringComparison.Ordinal))
            {
                // The new session is younger than any max_age, so on return it no longer applies.
                continue;
            }

            query.Add(name, values.ToArray()!);
        }

        return context.Request.PathBase + context.Request.Path + query.ToQueryString();
    }

    /// <summary>The prompt values of <paramref name="prompt"/>, space-separated, without <c>login</c>.</summary>
    private static string WithoutLogin(string prompt)
    {
        var kept = new List<string>();
        foreach (var value in prompt.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!string.Equals(value, PromptValues.Login, StringComparison.Ordinal))
            {
                kept.Add(value);
            }
        }

        return string.Join(' ', kept);
    }

    private static ILogger Logger(HttpContext context) => context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("MagicLink.SignIn");

    [LoggerMessage(Level = LogLevel.Information, Message = "A person with a session may no longer sign in, by the source of who may sign in; the session is ended and no tokens are issued.")]
    private static partial void Refused(ILogger logger);

    private static AuthenticationProperties Error(string error, string description) => new(new Dictionary<string, string?>(StringComparer.Ordinal)
    {
        [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description,
    });
}
