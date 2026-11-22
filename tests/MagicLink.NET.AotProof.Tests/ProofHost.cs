using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;

namespace MagicLink.AotProof.Tests;

/// <summary>
/// Gives the flow tests an <see cref="HttpClient"/> for the proof app, without cookies: each <see cref="ProofBrowser"/>
/// keeps its own. When MAGICLINK_AOT_PROOF_URL is set, the client calls that URL, which CI points at the Native AOT
/// binary it started on PostgreSQL. Otherwise the app runs in-process on a database of its own, created on the server
/// named by MAGICLINK_TEST_POSTGRES and dropped afterwards; without that variable the tests are skipped, except in CI,
/// where they fail.
/// </summary>
public sealed class ProofHost : IAsyncLifetime
{
    public const string UrlVariable = "MAGICLINK_AOT_PROOF_URL";
    public const string ServerVariable = "MAGICLINK_TEST_POSTGRES";

    private readonly string? _server = Environment.GetEnvironmentVariable(ServerVariable);
    private readonly string _name = "magiclink_proof_" + Guid.NewGuid().ToString("N");
    private bool _created;
    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;

    /// <summary>The client, or a skip when this host cannot run here.</summary>
    public HttpClient Client => _client ?? throw Unavailable();

    public async ValueTask InitializeAsync()
    {
        var url = Environment.GetEnvironmentVariable(UrlVariable);
        if (!string.IsNullOrWhiteSpace(url))
        {
#pragma warning disable CA2000 // The client owns the handler and disposes it with itself.
            _client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }, disposeHandler: true)
            {
                BaseAddress = new Uri(url),
            };
#pragma warning restore CA2000
            return;
        }

        if (string.IsNullOrWhiteSpace(_server))
        {
            return;
        }

        await using (var admin = new NpgsqlConnection(_server))
        {
            await admin.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE {_name}", admin);
            await command.ExecuteNonQueryAsync();
        }

        _created = true;
        var connectionString = new NpgsqlConnectionStringBuilder(_server) { Database = _name }.ConnectionString;

        // The tests never change config files, so the host need not watch them; watchers can exhaust inotify.
        Environment.SetEnvironmentVariable("DOTNET_hostBuilder__reloadConfigOnChange", "false");
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting(ProofClients.PostgresSetting, connectionString));
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        if (_created)
        {
            NpgsqlConnection.ClearAllPools();
            await using var admin = new NpgsqlConnection(_server);
            await admin.OpenAsync();
            await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS {_name} WITH (FORCE)", admin);
            await command.ExecuteNonQueryAsync();
        }
    }

    private static Exception Unavailable()
    {
        if (string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Fail($"{ServerVariable} is not set. CI must run the flow tests on PostgreSQL.");
        }

        Assert.Skip($"Set {ServerVariable} to a PostgreSQL connection string to run the flow tests.");
        return null!; // Assert.Skip throws.
    }
}
