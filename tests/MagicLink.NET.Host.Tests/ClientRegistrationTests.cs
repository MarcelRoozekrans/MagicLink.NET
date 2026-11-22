using MagicLink.PostgreSql.Tests;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace MagicLink.Host.Tests;

// The container has no code of its own to register applications with, so the clients in its settings are the clients it
// knows: each start adds new ones, updates changed ones and removes the ones taken out of the settings.
public sealed class ClientRegistrationTests(PostgresDatabase database) : IClassFixture<PostgresDatabase>, IAsyncDisposable
{
    private readonly TestSetup _setup = new();
    private readonly List<HostFactory> _hosts = [];

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    public async ValueTask DisposeAsync()
    {
        foreach (var host in _hosts)
        {
            await host.DisposeAsync();
        }

        _setup.Dispose();
    }

    [Fact]
    public async Task Each_start_makes_the_store_hold_the_configured_clients()
    {
        await StartAsync(_setup.Settings(database.ConnectionString, smtpPort: 25));

        var changed = _setup.Settings(database.ConnectionString, smtpPort: 25);
        changed["MagicLink:Clients:0:RedirectUris:0"] = "http://localhost/other-callback";
        changed.Remove("MagicLink:Clients:0:Scopes:1");
        changed["MagicLink:Clients:1:ClientId"] = "host-test-new";
        changed["MagicLink:Clients:1:DisplayName"] = "New app";
        var services = await StartAsync(changed);

        await using var scope = services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var cli = await manager.FindByClientIdAsync(TestSetup.PublicClient, Cancel);
        Assert.NotNull(cli);
        Assert.Equal(["http://localhost/other-callback"], await manager.GetRedirectUrisAsync(cli, Cancel));
        Assert.False(await manager.HasPermissionAsync(cli, Permissions.GrantTypes.RefreshToken, Cancel));
        Assert.True(await manager.HasPermissionAsync(cli, Permissions.Scopes.Email, Cancel));
        Assert.True(await manager.HasClientTypeAsync(cli, ClientTypes.Public, Cancel));
        Assert.Null(await manager.FindByClientIdAsync(TestSetup.ConfidentialClient, Cancel));
        var added = await manager.FindByClientIdAsync("host-test-new", Cancel);
        Assert.NotNull(added);
        Assert.Equal("New app", await manager.GetDisplayNameAsync(added, Cancel));
        Assert.True(await manager.HasClientTypeAsync(added, ClientTypes.Confidential, Cancel));
        Assert.True(await manager.ValidateClientSecretAsync(added, TestSetup.ClientSecret, Cancel));
    }

    [Fact]
    public async Task A_changed_secret_replaces_the_old_one()
    {
        var first = _setup.Settings(database.ConnectionString, smtpPort: 25);
        first["MagicLink:Clients:1:ClientId"] = "host-test-rotated";
        await StartAsync(first);

        var rotated = new Dictionary<string, string?>(first, StringComparer.Ordinal) { ["MagicLink:Clients:1:ClientSecret"] = "the-new-secret" };
        var services = await StartAsync(rotated);

        await using var scope = services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var client = await manager.FindByClientIdAsync("host-test-rotated", Cancel);
        Assert.NotNull(client);
        Assert.True(await manager.ValidateClientSecretAsync(client, "the-new-secret", Cancel));
        Assert.False(await manager.ValidateClientSecretAsync(client, TestSetup.ClientSecret, Cancel));
    }

    [Fact]
    public async Task Instances_that_start_together_register_each_client_once()
    {
        var settings = _setup.Settings(database.ConnectionString, smtpPort: 25);
        settings["MagicLink:Clients:0:ClientId"] = "host-test-together";

        var hosts = Enumerable.Range(0, 3).Select(_ => new HostFactory(settings)).ToList();
        try
        {
            await Task.WhenAll(hosts.Select(host => Task.Run(() => host.Services, Cancel)));

            await using var scope = hosts[0].Services.CreateAsyncScope();
            var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
            Assert.NotNull(await manager.FindByClientIdAsync("host-test-together", Cancel));
        }
        finally
        {
            foreach (var host in hosts)
            {
                await host.DisposeAsync();
            }
        }
    }

    /// <summary>Starts the host with <paramref name="settings"/> until the clients are registered, and returns its services.</summary>
    private async Task<IServiceProvider> StartAsync(Dictionary<string, string?> settings)
    {
        var host = new HostFactory(settings);
        _hosts.Add(host);
        return await Task.Run(() => host.Services, Cancel);
    }
}
