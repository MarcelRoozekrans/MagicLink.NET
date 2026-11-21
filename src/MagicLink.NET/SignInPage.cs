using System.Net.Mail;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace MagicLink;

/// <summary>
/// The sign-in page: a person types their email address, receives a link and a code, and confirms the sign-in by
/// pressing the button on the page the link opens, or by typing the code, in the browser that started it (ADR-011,
/// ADR-249). Only a POST from that browser starts a session; opening the link never does.
/// </summary>
internal static partial class SignInPage
{
    /// <summary>Where the email address is asked for; the authorization endpoint sends people here.</summary>
    public const string Path = MagicLinkSession.SignInPath;

    /// <summary>Where the code is typed.</summary>
    public const string CodePath = Path + "/code";

    /// <summary>Where the link in the email points.</summary>
    public const string LinkPath = Path + "/link";

    /// <summary>The query and form field with the path to return to.</summary>
    public const string ReturnUrlField = "ReturnUrl";

    /// <summary>The form field with the email address.</summary>
    public const string EmailField = "email";

    /// <summary>The form field with the code.</summary>
    public const string CodeField = "code";

    /// <summary>The query and form field with the link token.</summary>
    public const string TokenField = "token";

    // The longest address SMTP can deliver to.
    private const int MaxEmailLength = 254;

    /// <summary>Maps the sign-in page under <see cref="Path"/>.</summary>
    public static IEndpointRouteBuilder MapSignInPage(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(Path, new RequestDelegate(EmailFormAsync));
        endpoints.MapPost(Path, new RequestDelegate(StartAsync));
        endpoints.MapGet(CodePath, new RequestDelegate(CodeFormAsync));
        endpoints.MapPost(CodePath, new RequestDelegate(ConfirmCodeAsync));
        endpoints.MapGet(LinkPath, new RequestDelegate(LinkAsync));
        endpoints.MapPost(LinkPath, new RequestDelegate(ConfirmLinkAsync));
        return endpoints;
    }

    /// <summary>Asks for the email address, for a sign-in that returns to a path on this site.</summary>
    internal static Task EmailFormAsync(HttpContext context)
    {
        var returnUrl = context.Request.Query[ReturnUrlField].ToString();
        if (!LocalUrl.IsLocal(returnUrl))
        {
            return SignInHtml.BadRequestAsync(context);
        }

        return SignInHtml.EmailFormAsync(context, Antiforgery(context).GetAndStoreTokens(context), returnUrl, email: null, error: null, StatusCodes.Status200OK);
    }

    /// <summary>
    /// Starts a sign-in request for the typed address, binds it to this browser, queues the email with the link and code,
    /// and moves on to the page that asks for the code.
    /// </summary>
    internal static async Task StartAsync(HttpContext context)
    {
        var form = await ReadFormAsync(context).ConfigureAwait(false);
        var returnUrl = form?[ReturnUrlField].ToString() ?? string.Empty;
        if (form is null || !LocalUrl.IsLocal(returnUrl))
        {
            await SignInHtml.BadRequestAsync(context).ConfigureAwait(false);
            return;
        }

        var typed = form[EmailField].ToString().Trim();
        if (!IsEmailAddress(typed))
        {
            await SignInHtml.EmailFormAsync(context, Antiforgery(context).GetAndStoreTokens(context), returnUrl, typed, "Type a valid email address.", StatusCodes.Status400BadRequest).ConfigureAwait(false);
            return;
        }

        var services = context.RequestServices;
        var options = services.GetRequiredService<IOptions<MagicLinkOptions>>().Value;
        var now = Now(context);
        var email = MagicLinkSession.Normalize(typed);
        var (request, cookie, linkToken, code) = await SignInRequest.CreateAsync(
            Tokens(context), email, returnUrl, now, options.CodeLifetime, context.RequestAborted).ConfigureAwait(false);

        context.Response.Cookies.Append(SignInRequest.BindingCookie, cookie, BindingCookieOptions(now + options.CodeLifetime));
        Started(Logger(context), request.Id);

        // Sent in the background (ADR-015): the answer does not wait for the mail server, and takes as long whether or
        // not an email goes out.
        services.GetRequiredService<SignInMailQueue>().Enqueue(new SignInMessage(email, LinkTo(context, linkToken), code, now + options.CodeLifetime, request.Id));

        // 303: the browser follows with a GET, so reloading the next page does not start another request.
        context.Response.StatusCode = StatusCodes.Status303SeeOther;
        context.Response.Headers.Location = context.Request.PathBase + CodePath;
    }

    /// <summary>Asks for the code of the sign-in request this browser started.</summary>
    internal static async Task CodeFormAsync(HttpContext context)
    {
        var request = await SignInRequest.FindByCookieAsync(Tokens(context), BindingCookie(context), context.RequestAborted).ConfigureAwait(false);
        if (request is null || !request.IsUsable(Now(context), MaxAttempts(context)))
        {
            await SignInHtml.NoLongerValidAsync(context).ConfigureAwait(false);
            return;
        }

        await SignInHtml.CodeFormAsync(context, Antiforgery(context).GetAndStoreTokens(context), request.Email, error: null, StatusCodes.Status200OK).ConfigureAwait(false);
    }

    /// <summary>Signs the person in when the typed code belongs to the sign-in request this browser started.</summary>
    internal static async Task ConfirmCodeAsync(HttpContext context)
    {
        var form = await ReadFormAsync(context).ConfigureAwait(false);
        if (form is null)
        {
            await SignInHtml.BadRequestAsync(context).ConfigureAwait(false);
            return;
        }

        var tokens = Tokens(context);
        var cookie = BindingCookie(context);
        var request = await SignInRequest.FindByCookieAsync(tokens, cookie, context.RequestAborted).ConfigureAwait(false);
        if (request is null)
        {
            await SignInHtml.NoLongerValidAsync(context).ConfigureAwait(false);
            return;
        }

        var maxAttempts = MaxAttempts(context);
        var outcome = await request.TryCodeAsync(tokens, cookie!, form[CodeField].ToString(), Now(context), maxAttempts, context.RequestAborted).ConfigureAwait(false);
        switch (outcome)
        {
            case CodeOutcome.SignedIn:
                await CompleteAsync(context, request).ConfigureAwait(false);
                return;

            case CodeOutcome.Wrong:
                WrongCode(Logger(context), request.Id);
                await SignInHtml.CodeFormAsync(context, Antiforgery(context).GetAndStoreTokens(context), request.Email, WrongCodeMessage(request.AttemptsLeft(maxAttempts)), StatusCodes.Status400BadRequest).ConfigureAwait(false);
                return;

            case CodeOutcome.TryAgain:
                await SignInHtml.CodeFormAsync(context, Antiforgery(context).GetAndStoreTokens(context), request.Email, "That did not work. Type the code once more.", StatusCodes.Status409Conflict).ConfigureAwait(false);
                return;

            default:
                NoLongerValid(Logger(context), request.Id);
                await SignInHtml.NoLongerValidAsync(context).ConfigureAwait(false);
                return;
        }
    }

    /// <summary>
    /// The page the email links to. Opening it never signs anyone in. In the browser that started the sign-in it shows
    /// the button that does; anywhere else it tells the person to type the code there (ADR-249).
    /// </summary>
    internal static async Task LinkAsync(HttpContext context)
    {
        var linkToken = context.Request.Query[TokenField].ToString();
        var request = await SignInRequest.FindByLinkAsync(Tokens(context), linkToken, context.RequestAborted).ConfigureAwait(false);
        if (request is null || !request.IsUsable(Now(context), MaxAttempts(context)))
        {
            await SignInHtml.NoLongerValidAsync(context).ConfigureAwait(false);
            return;
        }

        if (!request.BelongsTo(BindingCookie(context)))
        {
            OtherBrowser(Logger(context), request.Id);
            await SignInHtml.OtherBrowserAsync(context, StatusCodes.Status200OK).ConfigureAwait(false);
            return;
        }

        await SignInHtml.ConfirmFormAsync(context, Antiforgery(context).GetAndStoreTokens(context), request.Email, linkToken).ConfigureAwait(false);
    }

    /// <summary>Signs the person in when the button is pressed in the browser that started the sign-in.</summary>
    internal static async Task ConfirmLinkAsync(HttpContext context)
    {
        var form = await ReadFormAsync(context).ConfigureAwait(false);
        if (form is null)
        {
            await SignInHtml.BadRequestAsync(context).ConfigureAwait(false);
            return;
        }

        var tokens = Tokens(context);
        var request = await SignInRequest.FindByLinkAsync(tokens, form[TokenField].ToString(), context.RequestAborted).ConfigureAwait(false);
        if (request is null || !request.IsUsable(Now(context), MaxAttempts(context)))
        {
            await SignInHtml.NoLongerValidAsync(context).ConfigureAwait(false);
            return;
        }

        if (!request.BelongsTo(BindingCookie(context)))
        {
            OtherBrowser(Logger(context), request.Id);
            await SignInHtml.OtherBrowserAsync(context, StatusCodes.Status403Forbidden).ConfigureAwait(false);
            return;
        }

        if (!await request.TryRedeemAsync(tokens, Now(context), context.RequestAborted).ConfigureAwait(false))
        {
            await SignInHtml.NoLongerValidAsync(context).ConfigureAwait(false);
            return;
        }

        await CompleteAsync(context, request).ConfigureAwait(false);
    }

    /// <summary>Starts the session for the confirmed request and returns to where the sign-in started.</summary>
    private static async Task CompleteAsync(HttpContext context, SignInRequest request)
    {
        // Checked when the request started; checked again, since the store is outside this page's control.
        if (!LocalUrl.IsLocal(request.ReturnUrl))
        {
            await SignInHtml.BadRequestAsync(context).ConfigureAwait(false);
            return;
        }

        context.Response.Cookies.Delete(SignInRequest.BindingCookie, BindingCookieOptions(expires: null));
        await MagicLinkSession.SignInAsync(context, request.Email).ConfigureAwait(false);
        SignedIn(Logger(context), request.Id);
        await Results.LocalRedirect(request.ReturnUrl).ExecuteAsync(context).ConfigureAwait(false);
    }

    /// <summary>The posted form, or null when it is not a form or fails the antiforgery check.</summary>
    private static async Task<IFormCollection?> ReadFormAsync(HttpContext context)
    {
        if (!context.Request.HasFormContentType)
        {
            return null;
        }

        if (!await Antiforgery(context).IsRequestValidAsync(context).ConfigureAwait(false))
        {
            AntiforgeryFailed(Logger(context));
            return null;
        }

        return await context.Request.ReadFormAsync(context.RequestAborted).ConfigureAwait(false);
    }

    /// <summary>
    /// The page the email links to, under the issuer: the address applications know MagicLink.NET by. The request's
    /// own Host header is not used, so a forged one cannot send the link token to another site.
    /// </summary>
    private static Uri LinkTo(HttpContext context, string linkToken)
    {
        // AddMagicLinkServer refuses to start without an issuer, so it is set here.
        var issuer = context.RequestServices.GetRequiredService<IOptionsMonitor<OpenIddictServerOptions>>().CurrentValue.Issuer!;
        return new Uri(issuer.AbsoluteUri.TrimEnd('/') + LinkPath + "?" + TokenField + "=" + Uri.EscapeDataString(linkToken));
    }

    /// <summary>Whether <paramref name="typed"/> is a single email address, as SMTP can deliver to, with nothing around it.</summary>
    internal static bool IsEmailAddress(string typed) =>
        typed.Length is > 0 and <= MaxEmailLength
        && MailAddress.TryCreate(typed, out var address)
        && string.Equals(address.Address, typed, StringComparison.Ordinal);

    private static string WrongCodeMessage(int attemptsLeft) =>
        attemptsLeft == 1 ? "That code is not right. You can try once more." : $"That code is not right. You can try {attemptsLeft} more times.";

    private static CookieOptions BindingCookieOptions(DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        Secure = true,

        // Lax: sent when the link in the email opens the page, a top-level navigation from another site, so the page
        // can tell whether this is the browser that started the sign-in; never sent with a form posted from another site.
        SameSite = SameSiteMode.Lax,
        Path = "/",
        Expires = expires,
        IsEssential = true,
    };

    private static string? BindingCookie(HttpContext context) => context.Request.Cookies[SignInRequest.BindingCookie];

    private static IOpenIddictTokenManager Tokens(HttpContext context) => context.RequestServices.GetRequiredService<IOpenIddictTokenManager>();

    private static IAntiforgery Antiforgery(HttpContext context) => context.RequestServices.GetRequiredService<IAntiforgery>();

    private static int MaxAttempts(HttpContext context) => context.RequestServices.GetRequiredService<IOptions<MagicLinkOptions>>().Value.MaxCodeAttempts;

    private static DateTimeOffset Now(HttpContext context) => context.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();

    private static ILogger Logger(HttpContext context) => context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("MagicLink.SignIn");

    [LoggerMessage(Level = LogLevel.Information, Message = "Sign-in request {RequestId} started.")]
    private static partial void Started(ILogger logger, string requestId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Sign-in request {RequestId} was confirmed; the person is signed in.")]
    private static partial void SignedIn(ILogger logger, string requestId);

    [LoggerMessage(Level = LogLevel.Information, Message = "A wrong code was typed for sign-in request {RequestId}.")]
    private static partial void WrongCode(ILogger logger, string requestId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Sign-in request {RequestId} no longer works: it was used, expired, or ran out of attempts.")]
    private static partial void NoLongerValid(ILogger logger, string requestId);

    [LoggerMessage(Level = LogLevel.Information, Message = "The link of sign-in request {RequestId} was opened in another browser; it does not sign in there.")]
    private static partial void OtherBrowser(ILogger logger, string requestId);

    [LoggerMessage(Level = LogLevel.Information, Message = "A sign-in form failed the antiforgery check.")]
    private static partial void AntiforgeryFailed(ILogger logger);
}
