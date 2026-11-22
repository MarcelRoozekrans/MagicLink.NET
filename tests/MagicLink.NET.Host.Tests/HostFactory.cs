using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MagicLink.Host.Tests;

/// <summary>The container's host in-process, with the given settings instead of environment variables.</summary>
internal sealed class HostFactory(IReadOnlyDictionary<string, string?> settings) : WebApplicationFactory<Program>
{
    static HostFactory()
    {
        // The tests never change config files, so the host need not watch them; watchers can exhaust inotify.
        Environment.SetEnvironmentVariable("DOTNET_hostBuilder__reloadConfigOnChange", "false");
    }

    /// <summary>A client without cookies or redirects, over HTTPS as OpenIddict requires.</summary>
    public HttpClient CreateHttpsClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = TestSetup.InProcessIssuer,
        AllowAutoRedirect = false,
        HandleCookies = false,
    });

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        foreach (var (key, value) in settings)
        {
            builder.UseSetting(key, value);
        }
    }
}
