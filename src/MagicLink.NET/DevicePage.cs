using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace MagicLink;

/// <summary>
/// The page where a person approves a command-line tool or other device that signs in with the device authorization
/// grant (RFC 8628). The person signs in here with the link or the code from the email, as for any application, and then
/// types the user code the device shows. Only that POST approves the device; opening the page, also with the user code
/// in its address, never does, so a link someone else sent cannot sign their device in as this person.
/// </summary>
internal static partial class DevicePage
{
    /// <summary>The verification address the device tells the person to open.</summary>
    public const string Path = "/" + MagicLinkEndpoints.VerificationPath;

    /// <summary>Where the browser goes once the device is approved.</summary>
    public const string DonePath = Path + "/done";

    /// <summary>Where the browser goes when the person may not sign in.</summary>
    public const string DeniedPath = Path + "/denied";

    /// <summary>The query and form field with the user code, as RFC 8628 names it.</summary>
    public const string UserCodeField = Parameters.UserCode;

    /// <summary>Maps the verification page under <see cref="Path"/>, and the pages it ends on.</summary>
    public static IEndpointRouteBuilder MapDevicePage(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(Path, new RequestDelegate(FormAsync));
        endpoints.MapPost(Path, new RequestDelegate(ApproveAsync));
        endpoints.MapGet(DonePath, new RequestDelegate(SignInHtml.DeviceDoneAsync));
        endpoints.MapGet(DeniedPath, new RequestDelegate(SignInHtml.DeviceDeniedAsync));
        return endpoints;
    }

    /// <summary>
    /// Asks a person with a session for the user code, filled in when the address holds it. Without a session it sends
    /// the browser to the sign-in page first, and back here afterwards.
    /// </summary>
    internal static async Task FormAsync(HttpContext context)
    {
        var userCode = UserCode(context);
        if (await MagicLinkSession.FindAsync(context).ConfigureAwait(false) is not var (email, _))
        {
            await SignInFirstAsync(context, userCode).ConfigureAwait(false);
            return;
        }

        await SignInHtml.DeviceFormAsync(context, SignInPage.Antiforgery(context).GetAndStoreTokens(context), email, userCode, error: null, StatusCodes.Status200OK).ConfigureAwait(false);
    }

    /// <summary>
    /// Approves the device whose user code was typed, for the person with a session, when the source of who may sign in
    /// still lets them in; otherwise refuses it, so the device stops waiting.
    /// </summary>
    internal static async Task ApproveAsync(HttpContext context)
    {
        if (await SignInPage.ReadFormAsync(context).ConfigureAwait(false) is null)
        {
            await SignInHtml.BadRequestAsync(context).ConfigureAwait(false);
            return;
        }

        var userCode = UserCode(context);
        if (await MagicLinkSession.FindAsync(context).ConfigureAwait(false) is not var (email, signedInAt))
        {
            // The session ended while the page was open: sign in again and come back with the code still filled in.
            await SignInFirstAsync(context, userCode).ConfigureAwait(false);
            return;
        }

        // OpenIddict has read the user code from the form. A code it does not know, or that expired, still succeeds here,
        // as the verification page may be opened without a code, but with an empty principal: only a real user code
        // names the device it belongs to.
        var device = await context.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme).ConfigureAwait(false);
        if (!device.Succeeded || string.IsNullOrEmpty(device.Principal.GetClaim(Claims.ClientId)))
        {
            WrongUserCode(Logger(context));
            await SignInHtml.DeviceFormAsync(
                context, SignInPage.Antiforgery(context).GetAndStoreTokens(context), email, userCode,
                "That code is not right, or it expired. Type the code your device shows now.", StatusCodes.Status400BadRequest).ConfigureAwait(false);
            return;
        }

        // Asked on every approval, as on every authorization: a person taken out of the source keeps their session
        // cookie until it expires, but gets no tokens with it.
        var access = await context.RequestServices.GetRequiredService<ISignInSource>().LookUpAsync(email, context.RequestAborted).ConfigureAwait(false);
        if (!access.MaySignIn)
        {
            Refused(Logger(context));
            await context.SignOutAsync(MagicLinkSession.Scheme).ConfigureAwait(false);

            // OpenIddict marks the device code refused, so the device's next poll gets access_denied instead of waiting.
            var refused = MagicLinkEndpoints.Error(Errors.AccessDenied, "This email address may not sign in.");
            refused.RedirectUri = context.Request.PathBase + DeniedPath;
            await context.ForbidAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, refused).ConfigureAwait(false);
            return;
        }

        var identity = MagicLinkIdentity.Create(email, signedInAt, device.Principal.GetScopes(), access.Groups);
        Approved(Logger(context));
        await context.SignInAsync(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { RedirectUri = context.Request.PathBase + DonePath }).ConfigureAwait(false);
    }

    /// <summary>Sends the browser to the sign-in page, to come back to this page with <paramref name="userCode"/>.</summary>
    private static Task SignInFirstAsync(HttpContext context, string userCode)
    {
        var returnUrl = context.Request.PathBase + Path;
        if (userCode.Length > 0)
        {
            returnUrl += "?" + UserCodeField + "=" + Uri.EscapeDataString(userCode);
        }

        return context.ChallengeAsync(MagicLinkSession.Scheme, new AuthenticationProperties { RedirectUri = returnUrl });
    }

    /// <summary>The user code from the address or the form, as OpenIddict read it; empty without one.</summary>
    private static string UserCode(HttpContext context) =>
        (context.GetOpenIddictServerRequest() ?? throw new InvalidOperationException("OpenIddict did not attach the verification request.")).UserCode ?? string.Empty;

    private static ILogger Logger(HttpContext context) => context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("MagicLink.SignIn");

    [LoggerMessage(Level = LogLevel.Information, Message = "A device was approved; it receives its tokens on its next poll.")]
    private static partial void Approved(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "A user code was typed that is not known or has expired; no device is approved.")]
    private static partial void WrongUserCode(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "A person with a session may no longer sign in, by the source of who may sign in; the device is refused and the session is ended.")]
    private static partial void Refused(ILogger logger);
}
