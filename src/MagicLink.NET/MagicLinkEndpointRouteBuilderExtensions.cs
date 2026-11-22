using MagicLink;
using Microsoft.AspNetCore.Routing;

namespace Microsoft.AspNetCore.Builder;

/// <summary>Maps MagicLink.NET's endpoints in an ASP.NET Core app.</summary>
public static class MagicLinkEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps MagicLink.NET's endpoints: authorization and token under <c>/connect</c>, and the sign-in page under
    /// <c>/signin</c>. OpenIddict answers discovery, the key set and user info itself.
    /// </summary>
    /// <param name="endpoints">The app's endpoints.</param>
    /// <returns>The endpoints, for chaining.</returns>
    /// <remarks>
    /// The authentication middleware must run before these endpoints. A <c>WebApplication</c> adds it by itself; an app
    /// that orders its middleware itself calls <c>UseAuthentication</c> before <c>UseRouting</c> or the endpoints.
    /// </remarks>
    public static IEndpointRouteBuilder MapMagicLink(this IEndpointRouteBuilder endpoints) => endpoints.MapMagicLinkEndpoints();
}
