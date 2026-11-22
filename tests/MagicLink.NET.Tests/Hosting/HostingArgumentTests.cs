using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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

    [Fact]
    public void The_apps_only_scheme_stays_its_default() =>
        Assert.Equal("own", DefaultScheme(static authentication => authentication.AddCookie("own")));

    [Fact]
    public void A_default_the_app_set_itself_stays()
    {
        var services = new ServiceCollection();
        services.AddAuthentication("second").AddCookie("own").AddCookie("second");

        Assert.Equal("second", DefaultScheme(services));
    }

    [Fact]
    public void An_app_with_more_schemes_and_no_default_gets_none() =>
        Assert.Null(DefaultScheme(static authentication => authentication.AddCookie("own").AddCookie("second")));

    [Fact]
    public void An_app_without_schemes_of_its_own_gets_no_default() =>
        Assert.Null(DefaultScheme(static _ => { }));

    private static string? DefaultScheme(Action<AuthenticationBuilder> app)
    {
        var services = new ServiceCollection();
        app(services.AddAuthentication());
        return DefaultScheme(services);
    }

    private static string? DefaultScheme(ServiceCollection services)
    {
        services.AddMagicLink(static _ => { });
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<AuthenticationOptions>>().Value.DefaultScheme;
    }
}
