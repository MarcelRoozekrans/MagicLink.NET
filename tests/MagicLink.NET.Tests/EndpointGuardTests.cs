using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace MagicLink.Tests;

// Guards for states OpenIddict never produces: they fail loudly instead of signing anyone in.
public sealed class EndpointGuardTests
{
    [Fact]
    public async Task A_refresh_token_or_device_code_without_a_subject_is_never_looked_up()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity("test"));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () => await MagicLinkEndpoints.LookUpAgainAsync(new DefaultHttpContext(), principal));

        Assert.Contains("without a subject", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_verification_page_needs_the_request_openiddict_attaches()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => DevicePage.FormAsync(new DefaultHttpContext()));

        Assert.Contains("verification request", error.Message, StringComparison.Ordinal);
    }
}
