using System.Buffers;
using System.Collections.Immutable;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace MagicLink;

/// <summary>Builds the identity OpenIddict turns into tokens for a signed-in person.</summary>
internal static class MagicLinkIdentity
{
    /// <summary>The claim with the person's groups, as the source of who may sign in returns them (ADR-014).</summary>
    public const string GroupsClaim = "groups";

    /// <summary>Where each claim goes when the email scope is granted; one delegate for every identity, created once.</summary>
    private static readonly Func<Claim, IEnumerable<string>> WithEmail = static claim => Destinations(claim, email: true);

    /// <summary>Where each claim goes when the email scope is not granted.</summary>
    private static readonly Func<Claim, IEnumerable<string>> WithoutEmail = static claim => Destinations(claim, email: false);

    /// <summary>
    /// The identity for the person with <paramref name="email"/>, who signed in at <paramref name="authenticatedAt"/>,
    /// for an authorization request that asked for <paramref name="scopes"/>, with the <paramref name="groups"/> the source
    /// of who may sign in returned for them.
    /// </summary>
    /// <remarks>
    /// The subject is the normalized email address: the source of who may sign in knows people only by their address
    /// (ADR-014), so the address is the one stable identifier there is.
    /// </remarks>
    public static ClaimsIdentity Create(string email, DateTimeOffset authenticatedAt, IEnumerable<string> scopes, ImmutableArray<string> groups)
    {
        var address = MagicLinkSession.Normalize(email);
        var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, Claims.Email, Claims.Role);
        identity.SetClaim(Claims.Subject, address)
            .SetClaim(Claims.Email, address)
            .SetClaim(Claims.EmailVerified, value: true)
            .SetClaim(Claims.AuthenticationTime, authenticatedAt.ToUnixTimeSeconds());
        if (!groups.IsDefaultOrEmpty)
        {
            // One claim holding a JSON array: as separate claims, a single group would go out as a string, and applications
            // expect a list whatever its length.
            SetGroups(identity, groups);
        }

        identity.SetScopes(scopes);
        identity.SetDestinations(identity.HasScope(Scopes.Email) ? WithEmail : WithoutEmail);
        return identity;
    }

    /// <summary>
    /// Puts the groups of a principal restored from an authorization code back into one claim. Reading the code splits
    /// the list into a claim per group, and OpenIddict writes a single claim as a string, while applications expect the
    /// groups as a list whatever its length.
    /// </summary>
    public static void KeepGroupsAList(ClaimsPrincipal principal)
    {
        ReplaceGroups(principal, principal.GetClaims(GroupsClaim));

        // A new claim has no destinations yet; the same rules as at authorization give them.
        principal.SetDestinations(principal.HasScope(Scopes.Email) ? WithEmail : WithoutEmail);
    }

    /// <summary>
    /// Prepares a principal restored from a refresh token or a device code for new tokens: the groups become
    /// <paramref name="groups"/>, as the source of who may sign in returns them now, and every claim goes where the
    /// <paramref name="scopes"/> this request grants allow. A refresh may ask for fewer scopes than the refresh token
    /// holds; OpenIddict narrows the scopes of the access token then, but keeps each claim's stored destinations, so
    /// they are set again here.
    /// </summary>
    public static void PrepareForNewTokens(ClaimsPrincipal principal, ImmutableArray<string> groups, ImmutableArray<string> scopes)
    {
        ReplaceGroups(principal, groups);
        principal.SetDestinations(scopes.Contains(Scopes.Email, StringComparer.Ordinal) ? WithEmail : WithoutEmail);
    }

    /// <summary>Replaces the groups claims of <paramref name="principal"/> with one claim holding <paramref name="groups"/>.</summary>
    private static void ReplaceGroups(ClaimsPrincipal principal, ImmutableArray<string> groups)
    {
        principal.RemoveClaims(GroupsClaim);
        if (!groups.IsDefaultOrEmpty)
        {
            SetGroups(principal.Identities.First(), groups);
        }
    }

    /// <summary>The groups as one claim holding a JSON array, which OpenIddict writes as a list even for one group.</summary>
    private static void SetGroups(ClaimsIdentity identity, ImmutableArray<string> groups) =>
        identity.AddClaim(new Claim(GroupsClaim, JsonList(groups), JsonClaimValueTypes.JsonArray));

    /// <summary><paramref name="values"/> as a JSON array, written without reflection so it works under Native AOT.</summary>
    private static string JsonList(ImmutableArray<string> values)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartArray();
            foreach (var value in values)
            {
                writer.WriteStringValue(value);
            }

            writer.WriteEndArray();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static IEnumerable<string> Destinations(Claim claim, bool email)
    {
        switch (claim.Type)
        {
            case Claims.Subject:
                return [OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken];

            // The address is proven, so email_verified is always true; both go out only with the email scope.
            case Claims.Email or Claims.EmailVerified when email:
                return [OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken];

            // Applications take teams and roles from the groups (ADR-009), as from any other identity provider, so they
            // go out with every sign-in, whatever the scopes.
            case GroupsClaim:
                return [OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken];

            case Claims.AuthenticationTime:
                return [OpenIddictConstants.Destinations.IdentityToken];

            default:
                return [];
        }
    }
}
