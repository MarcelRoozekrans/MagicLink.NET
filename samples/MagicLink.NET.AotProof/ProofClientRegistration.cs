using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace MagicLink.AotProof;

/// <summary>
/// Registers the proof's fixed client when the app starts. When it already exists, it is left alone, so a restart on
/// the same PostgreSQL database works. Proof-only.
/// </summary>
internal sealed class ProofClientRegistration(IServiceProvider services) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var scope = services.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
            foreach (var client in Clients())
            {
                if (await manager.FindByClientIdAsync(client.ClientId!, cancellationToken).ConfigureAwait(false) is null)
                {
                    await manager.CreateAsync(client, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static IEnumerable<OpenIddictApplicationDescriptor> Clients()
    {
        yield return new OpenIddictApplicationDescriptor
        {
            ClientId = ProofClients.PublicClientId,
            ClientType = ClientTypes.Public,
            RedirectUris = { new Uri(ProofClients.PublicRedirectUri) },
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.DeviceAuthorization,
                Permissions.Endpoints.Token,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.GrantTypes.DeviceCode,
                Permissions.GrantTypes.RefreshToken,
                Permissions.ResponseTypes.Code,
                Permissions.Prefixes.Scope + Scopes.OfflineAccess,
            },
            Requirements = { Requirements.Features.ProofKeyForCodeExchange },
        };
    }
}
