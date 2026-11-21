using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace MagicLink;

/// <summary>
/// The person's session at MagicLink.NET itself: a cookie that says who proved their email address in this browser.
/// The authorization endpoint reads it; the sign-in page sets it once a link or code is confirmed.
/// </summary>
internal static class MagicLinkSession
{
    /// <summary>The authentication scheme of the session cookie. It is never the host's default scheme.</summary>
    public const string Scheme = "MagicLink.Session";

    /// <summary>The cookie's name. The <c>__Host-</c> prefix makes browsers require Secure, path / and no domain.</summary>
    public const string CookieName = "__Host-MagicLink.Session";

    /// <summary>Where a person without a session is sent to sign in, with the authorization request as return URL.</summary>
    public const string SignInPath = "/signin";

    /// <summary>How long a session lasts before the person has to confirm their email address again.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(8);

    /// <summary>Settings of the session cookie.</summary>
    public static void Configure(CookieAuthenticationOptions options)
    {
        options.Cookie.Name = CookieName;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;

        // Lax, not Strict: the browser must send the cookie when an application redirects it to the authorization
        // endpoint, which is a top-level navigation from another site.
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.Path = "/";
        options.LoginPath = SignInPath;
        options.ExpireTimeSpan = Lifetime;
        options.SlidingExpiration = false;
    }

    /// <summary>
    /// Starts a session for a person who proved they own <paramref name="email"/>. The sign-in page calls this after
    /// the link or the code is confirmed in the browser that started the sign-in.
    /// </summary>
    public static Task SignInAsync(HttpContext context, string email)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var identity = new ClaimsIdentity(Scheme, Claims.Email, Claims.Role);
        identity.AddClaim(new Claim(Claims.Email, Normalize(email)));
        return context.SignInAsync(Scheme, new ClaimsPrincipal(identity));
    }

    /// <summary>Email addresses compare without regard to case or surrounding white space.</summary>
    public static string Normalize(string email) => email.Trim().ToLowerInvariant();
}
