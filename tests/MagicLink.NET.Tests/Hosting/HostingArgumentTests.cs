using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace MagicLink.Tests.Hosting;

public sealed class HostingArgumentTests
{
    [Fact]
    public void AddMagicLink_needs_the_services_and_the_server_settings()
    {
        Assert.Throws<ArgumentNullException>("services", static () => MagicLinkServiceCollectionExtensions.AddMagicLink(null!, static _ => { }));
        Assert.Throws<ArgumentNullException>("configure", static () => new ServiceCollection().AddMagicLink(null!));
    }

    [Fact]
    public void MapMagicLink_needs_the_endpoints() =>
        Assert.Throws<ArgumentNullException>("endpoints", static () => ((IEndpointRouteBuilder)null!).MapMagicLink());
}
