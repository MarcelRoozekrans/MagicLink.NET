using Microsoft.AspNetCore.Mvc.Testing;

namespace MagicLink.AotProof.Tests;

/// <summary>
/// Gives the flow tests an <see cref="HttpClient"/> for the proof app. When MAGICLINK_AOT_PROOF_URL is set,
/// the client calls that URL, which CI points at the Native AOT binary; otherwise the app runs in-process.
/// </summary>
public sealed class ProofHost : IDisposable
{
    public const string UrlVariable = "MAGICLINK_AOT_PROOF_URL";

    private readonly WebApplicationFactory<Program>? _factory;

    public ProofHost()
    {
        var url = Environment.GetEnvironmentVariable(UrlVariable);
        if (string.IsNullOrWhiteSpace(url))
        {
            // The tests never change config files, so the host need not watch them; watchers can exhaust inotify.
            Environment.SetEnvironmentVariable("DOTNET_hostBuilder__reloadConfigOnChange", "false");
            _factory = new WebApplicationFactory<Program>();
            Client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        }
        else
        {
#pragma warning disable CA2000 // The client owns the handler and disposes it with itself.
            Client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }, disposeHandler: true) { BaseAddress = new Uri(url) };
#pragma warning restore CA2000
        }
    }

    public HttpClient Client { get; }

    public void Dispose()
    {
        Client.Dispose();
        _factory?.Dispose();
    }
}
