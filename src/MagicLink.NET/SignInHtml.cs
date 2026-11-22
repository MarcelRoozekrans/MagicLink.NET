using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;

namespace MagicLink;

/// <summary>
/// Writes the sign-in pages. They are plain HTML without scripts, so they need neither Razor nor runtime compilation
/// and run under Native AOT.
/// </summary>
internal static class SignInHtml
{
    private const string Style =
        "body{font-family:system-ui,sans-serif;max-width:28rem;margin:4rem auto;padding:0 1rem;line-height:1.5}" +
        "label,input,button{display:block;width:100%;box-sizing:border-box;font:inherit}" +
        "input,button{padding:.5rem;margin:.25rem 0 1rem}.error{color:#b00020}";

    // Only this exact style block may run; no scripts, frames, images or other sources at all.
    private static readonly string ContentSecurityPolicy =
        "default-src 'none'; style-src 'sha256-" + Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(Style))) +
        "'; frame-ancestors 'none'; base-uri 'none'";

    /// <summary>The page that asks for an email address.</summary>
    public static Task EmailFormAsync(HttpContext context, AntiforgeryTokenSet antiforgery, string returnUrl, string? email, string? error, int status) =>
        WriteAsync(context, status, "Sign in", new StringBuilder()
            .Append("<h1>Sign in</h1>")
            .Append(Error(error))
            .Append("<form method=\"post\" action=\"").Append(Encode(Action(context, SignInPage.Path))).Append("\">")
            .Append(Hidden(antiforgery.FormFieldName, antiforgery.RequestToken!))
            .Append(Hidden(SignInPage.ReturnUrlField, returnUrl))
            .Append("<label for=\"email\">Email address</label>")
            .Append("<input id=\"email\" name=\"").Append(SignInPage.EmailField).Append("\" type=\"email\" autocomplete=\"email\" required autofocus value=\"")
            .Append(Encode(email ?? string.Empty)).Append("\">")
            .Append("<button type=\"submit\">Send me a sign-in email</button>")
            .Append("</form>")
            .ToString());

    /// <summary>The page that asks for the code from the email.</summary>
    public static Task CodeFormAsync(HttpContext context, AntiforgeryTokenSet antiforgery, string email, string? error, int status) =>
        WriteAsync(context, status, "Type the code", new StringBuilder()
            .Append("<h1>Check your email</h1>")
            .Append("<p>If ").Append(Encode(email)).Append(" is known, you will receive an email with a link and a code. ")
            .Append("Open the link in this browser, or type the code here.</p>")
            .Append(Error(error))
            .Append("<form method=\"post\" action=\"").Append(Encode(Action(context, SignInPage.CodePath))).Append("\">")
            .Append(Hidden(antiforgery.FormFieldName, antiforgery.RequestToken!))
            .Append("<label for=\"code\">Code</label>")
            .Append("<input id=\"code\" name=\"").Append(SignInPage.CodeField).Append("\" autocomplete=\"one-time-code\" autocapitalize=\"characters\" spellcheck=\"false\" required autofocus>")
            .Append("<button type=\"submit\">Sign in</button>")
            .Append("</form>")
            .ToString());

    /// <summary>The page the email links to, in the browser that started the sign-in: one button signs in.</summary>
    public static Task ConfirmFormAsync(HttpContext context, AntiforgeryTokenSet antiforgery, string email, string linkToken) =>
        WriteAsync(context, StatusCodes.Status200OK, "Sign in", new StringBuilder()
            .Append("<h1>Sign in</h1>")
            .Append("<p>Sign in as ").Append(Encode(email)).Append(".</p>")
            .Append("<form method=\"post\" action=\"").Append(Encode(Action(context, SignInPage.LinkPath))).Append("\">")
            .Append(Hidden(antiforgery.FormFieldName, antiforgery.RequestToken!))
            .Append(Hidden(SignInPage.TokenField, linkToken))
            .Append("<button type=\"submit\">Sign in</button>")
            .Append("</form>")
            .ToString());

    /// <summary>The page the email links to, opened in another browser or on another device: it points to the code.</summary>
    public static Task OtherBrowserAsync(HttpContext context, int status) =>
        WriteAsync(context, status, "Use the code", "<h1>Use the code</h1>" +
            "<p>This link only signs you in in the browser where you started signing in. " +
            "Go back there and type the code from the email.</p>");

    /// <summary>The page for a sign-in that was used, expired, ran out of attempts, or is not known in this browser.</summary>
    public static Task NoLongerValidAsync(HttpContext context) =>
        WriteAsync(context, StatusCodes.Status410Gone, "Sign in again", "<h1>This sign-in no longer works</h1>" +
            "<p>The link or code was already used, has expired, or was typed wrong too often. " +
            "Go back to the application and sign in again.</p>");

    /// <summary>
    /// The page where a person approves a command-line tool or device: it shows who they are signed in as and asks for
    /// the user code the device shows, filled in when the address held it.
    /// </summary>
    public static Task DeviceFormAsync(HttpContext context, AntiforgeryTokenSet antiforgery, string email, string userCode, string? error, int status) =>
        WriteAsync(context, status, "Sign in on your device", new StringBuilder()
            .Append("<h1>Sign in on your device</h1>")
            .Append("<p>Type the code your device shows to sign it in as ").Append(Encode(email)).Append(". ")
            .Append("Only do this for a device you started signing in yourself.</p>")
            .Append(Error(error))
            .Append("<form method=\"post\" action=\"").Append(Encode(Action(context, DevicePage.Path))).Append("\">")
            .Append(Hidden(antiforgery.FormFieldName, antiforgery.RequestToken!))
            .Append("<label for=\"user_code\">Code from your device</label>")
            .Append("<input id=\"user_code\" name=\"").Append(DevicePage.UserCodeField).Append("\" autocomplete=\"off\" autocapitalize=\"characters\" spellcheck=\"false\" required autofocus value=\"")
            .Append(Encode(userCode)).Append("\">")
            .Append("<button type=\"submit\">Sign in on the device</button>")
            .Append("</form>")
            .ToString());

    /// <summary>The page after a device was approved.</summary>
    public static Task DeviceDoneAsync(HttpContext context) =>
        WriteAsync(context, StatusCodes.Status200OK, "Signed in", "<h1>Your device is signed in</h1>" +
            "<p>Go back to your device. You can close this page.</p>");

    /// <summary>The page after a device was refused, because the person may not sign in.</summary>
    public static Task DeviceDeniedAsync(HttpContext context) =>
        WriteAsync(context, StatusCodes.Status403Forbidden, "Not signed in", "<h1>Your device is not signed in</h1>" +
            "<p>This email address may not sign in.</p>");

    /// <summary>The page for a request this page cannot handle, such as a form without its antiforgery token.</summary>
    public static Task BadRequestAsync(HttpContext context) =>
        WriteAsync(context, StatusCodes.Status400BadRequest, "Sign in again", "<h1>Something went wrong</h1>" +
            "<p>Go back to the application and sign in again.</p>");

    private static async Task WriteAsync(HttpContext context, int status, string title, string body)
    {
        var response = context.Response;
        response.StatusCode = status;
        response.ContentType = "text/html; charset=utf-8";

        // Sign-in pages hold secrets in forms and addresses: never cache them, never frame them, never leak the
        // address, with its link token, to another site.
        response.Headers.CacheControl = "no-store";
        response.Headers.Pragma = "no-cache";
        response.Headers.ContentSecurityPolicy = ContentSecurityPolicy;
        response.Headers.XFrameOptions = "DENY";
        response.Headers.XContentTypeOptions = "nosniff";
        response.Headers["Referrer-Policy"] = "no-referrer";

        var html = "<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\">" +
            "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">" +
            "<meta name=\"robots\" content=\"noindex\">" +
            "<title>" + Encode(title) + "</title><style>" + Style + "</style></head><body>" + body + "</body></html>";
        await response.WriteAsync(html, context.RequestAborted).ConfigureAwait(false);
    }

    private static string Action(HttpContext context, string path) => context.Request.PathBase + path;

    // GetAndStoreTokens always returns a request token, so the forms pass it with !.
    private static string Hidden(string name, string value) =>
        "<input type=\"hidden\" name=\"" + Encode(name) + "\" value=\"" + Encode(value) + "\">";

    private static string Error(string? error) => error is null ? string.Empty : "<p class=\"error\" role=\"alert\">" + Encode(error) + "</p>";

    private static string Encode(string value) => HtmlEncoder.Default.Encode(value);
}
