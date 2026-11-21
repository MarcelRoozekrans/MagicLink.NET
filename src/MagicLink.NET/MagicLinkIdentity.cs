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
        identity.SetDestinations(Destinations);
        return identity;
    }

    /// <summary>
    /// Puts the groups of a principal restored from an authorization code back into one claim. Reading the code splits
    /// the list into a claim per group, and OpenIddict writes a single claim as a string, while applications expect the
    /// groups as a list whatever its length.
    /// </summary>
    public static void KeepGroupsAList(ClaimsPrincipal principal)
    {
        var groups = principal.GetClaims(GroupsClaim);
        if (!groups.IsEmpty && principal.Identity is ClaimsIdentity identity)
        {
            identity.RemoveClaims(GroupsClaim);
            SetGroups(identity, groups);

            // A new claim has no destinations yet; the same rules as at authorization give them.
            identity.SetDestinations(Destinations);
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

    private static IEnumerable<string> Destinations(Claim claim)
    {
        switch (claim.Type)
        {
            case Claims.Subject:
                return [OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken];

            // The address is proven, so email_verified is always true; both go out only with the email scope.
            case Claims.Email or Claims.EmailVerified when claim.Subject is { } identity && identity.HasScope(Scopes.Email):
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
