namespace MagicLink;

/// <summary>What the sign-in email tells the person: the link to confirm with a button, and the code to type.</summary>
/// <param name="Email">The address the person typed, normalized.</param>
/// <param name="Link">The page with the sign-in button. It works only in the browser that started the sign-in.</param>
/// <param name="Code">The short code to type where the sign-in started.</param>
/// <param name="ExpiresAt">When link and code stop working.</param>
internal sealed record SignInMessage(string Email, Uri Link, string Code, DateTimeOffset ExpiresAt);
