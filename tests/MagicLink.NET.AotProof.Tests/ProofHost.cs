using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MagicLink.AotProof.Tests;

/// <summary>
/// Gives the flow tests an <see cref="HttpClient"/> for the proof app. When MAGICLINK_AOT_PROOF_URL is set,
/// the client calls that URL, which CI points at the Native AOT binary; otherwise the app runs in-process.
/// </summary>
public abstract class ProofHost : IAsyncLifetime
{
    public const string UrlVariable = "MAGICLINK_AOT_PROOF_URL";

    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;

    /// <summary>The client, or a skip when this host cannot run here.</summary>
    public HttpClient Client => _client ?? throw Unavailable();

    /// <summary>Whether the client calls a running binary instead of an in-process app.</summary>
    protected static bool OnBinary => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(UrlVariable));

    public async ValueTask InitializeAsync()
    {
        if (OnBinary)
        {
#pragma warning disable CA2000 // The client owns the handler and disposes it with itself.
            _client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }, disposeHandler: true)
            {
                BaseAddress = new Uri(Environment.GetEnvironmentVariable(UrlVariable)!),
            };
#pragma warning restore CA2000
            return;
        }

        var postgres = await PrepareDatabaseAsync();
        if (postgres is { Ready: false })
        {
            return;
        }

        // The tests never change config files, so the host need not watch them; watchers can exhaust inotify.
        Environment.SetEnvironmentVariable("DOTNET_hostBuilder__reloadConfigOnChange", "false");
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            if (postgres is { ConnectionString: { } connectionString })
            {
                builder.UseSetting(ProofClients.PostgresSetting, connectionString);
            }
        });
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    public virtual async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>Prepares the database the in-process app runs on, or returns null for the in-memory store.</summary>
    protected virtual ValueTask<ProofDatabase?> PrepareDatabaseAsync() => ValueTask.FromResult<ProofDatabase?>(null);

    /// <summary>What to do when the host could not start: skip, or fail in CI.</summary>
    protected virtual Exception Unavailable() => new InvalidOperationException("The proof host did not start.");
}
