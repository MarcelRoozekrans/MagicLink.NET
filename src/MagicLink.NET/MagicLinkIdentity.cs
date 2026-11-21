using System.Security.Claims;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace MagicLink;

/// <summary>Builds the identity OpenIddict turns into tokens for a signed-in person.</summary>
internal static class MagicLinkIdentity
{
    /// <summary>
    /// The identity for the person with <paramref name="email"/>, who signed in at <paramref name="authenticatedAt"/>,
    /// for an authorization request that asked for <paramref name="scopes"/>.
    /// </summary>
    /// <remarks>
    /// The subject is the normalized email address: the source of who may sign in knows people only by their address
    /// (ADR-014), so the address is the one stable identifier there is.
    /// </remarks>
    public static ClaimsIdentity Create(string email, DateTimeOffset authenticatedAt, IEnumerable<string> scopes)
    {
        var address = MagicLinkSession.Normalize(email);
        var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, Claims.Email, Claims.Role);
        identity.SetClaim(Claims.Subject, address)
            .SetClaim(Claims.Email, address)
            .SetClaim(Claims.EmailVerified, value: true)
            .SetClaim(Claims.AuthenticationTime, authenticatedAt.ToUnixTimeSeconds());
        identity.SetScopes(scopes);
        identity.SetDestinations(Destinations);
        return identity;
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

            case Claims.AuthenticationTime:
                return [OpenIddictConstants.Destinations.IdentityToken];

            default:
                return [];
        }
    }
}
