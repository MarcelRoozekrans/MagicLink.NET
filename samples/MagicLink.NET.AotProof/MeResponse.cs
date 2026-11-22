namespace MagicLink.AotProof;

/// <summary>The answer of <c>/api/me</c>: the subject and groups of the access token.</summary>
/// <param name="Subject">The token's <c>sub</c> claim.</param>
/// <param name="Groups">The token's <c>groups</c> claim.</param>
public sealed record MeResponse(string? Subject, string[] Groups);
