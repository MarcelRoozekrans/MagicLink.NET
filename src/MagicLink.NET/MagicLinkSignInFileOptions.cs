namespace MagicLink;

/// <summary>
/// Settings for the file that says who may sign in and with which groups, kept in git (ADR-014). MagicLink.NET does not
/// start while this file is the source and cannot be read.
/// </summary>
/// <remarks>
/// The file is JSON, with comments and trailing commas allowed. Each person has an email address and, optionally,
/// groups; an address that is not listed may not sign in:
/// <code>
/// {
///   "people": [
///     { "email": "ann@example.com", "groups": ["admins", "developers"] },
///     { "email": "bob@example.com" }
///   ]
/// }
/// </code>
/// MagicLink.NET reads the file again when its time or size changes, for example when a new commit is checked out. While
/// a changed file cannot be read, nobody can sign in, and the reason is logged.
/// </remarks>
public sealed class MagicLinkSignInFileOptions
{
    /// <summary>The path of the file. A relative path is taken from the working directory.</summary>
    public string? Path { get; set; }
}
