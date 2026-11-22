using MagicLink.PostgreSql.Tests;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace MagicLink.AotProof.Tests;

// The proof restarts on the same database and then gives its client the permissions of the current proof, such as the
// device flow, which a database from before issue #24 lacks.
public sealed class ProofClientRegistrationTests(PostgresDatabase database) : IClassFixture<PostgresDatabase>
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_restart_gives_an_existing_client_the_current_permissions()
    {
        await using (var first = Start())
        {
            await using var scope = first.Services.CreateAsyncScope();
            var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
            var client = await manager.FindByClientIdAsync(ProofClients.PublicClientId, Cancel);
            var older = new OpenIddictApplicationDescriptor();
            await manager.PopulateAsync(older, client!, Cancel);
            older.Permissions.Remove(Permissions.GrantTypes.DeviceCode);
            await manager.UpdateAsync(client!, older, Cancel);
        }

        await using var second = Start();
        await using var after = second.Services.CreateAsyncScope();
        var restarted = after.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var updated = await restarted.FindByClientIdAsync(ProofClients.PublicClientId, Cancel);
        Assert.True(await restarted.HasPermissionAsync(updated!, Permissions.GrantTypes.DeviceCode, Cancel));
    }

    private WebApplicationFactory<Program> Start()
    {
        Environment.SetEnvironmentVariable("DOTNET_hostBuilder__reloadConfigOnChange", "false");
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting(ProofClients.PostgresSetting, database.ConnectionString));
        _ = factory.Services;
        return factory;
    }
}
