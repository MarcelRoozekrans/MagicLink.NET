using MagicLink;
using Microsoft.AspNetCore.Http;
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
    /// <para>
    /// The authentication middleware must run before these endpoints. A <c>WebApplication</c> adds it by itself; an app
    /// that orders its middleware itself calls <c>UseAuthentication</c> before <c>UseRouting</c> or the endpoints.
    /// </para>
    /// <para>
    /// The endpoints allow anonymous access, so an app whose fallback policy requires a signed-in user still lets people
    /// reach the sign-in page. They do their own checks: OpenIddict validates each request, and the authorization endpoint
    /// issues a code only to a person with a MagicLink.NET session.
    /// </para>
    /// </remarks>
    public static IEndpointRouteBuilder MapMagicLink(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGroup(string.Empty)
            .AllowAnonymous()
            .MapMagicLinkEndpoints();
        return endpoints;
    }
}
