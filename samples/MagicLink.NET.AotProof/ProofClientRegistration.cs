using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace MagicLink.AotProof;

/// <summary>
/// Registers the proof's fixed clients when the app starts. A client that already exists is left alone, so a restart on
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
            ClientId = ProofClients.ConfidentialClientId,
            ClientSecret = ProofClients.ConfidentialClientSecret,
            ClientType = ClientTypes.Confidential,
            Permissions = { Permissions.Endpoints.Token, Permissions.GrantTypes.ClientCredentials },
        };

        yield return new OpenIddictApplicationDescriptor
        {
            ClientId = ProofClients.PublicClientId,
            ClientType = ClientTypes.Public,
            RedirectUris = { new Uri(ProofClients.PublicRedirectUri) },
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.Token,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.GrantTypes.RefreshToken,
                Permissions.ResponseTypes.Code,
                Permissions.Prefixes.Scope + Scopes.OfflineAccess,
            },
            Requirements = { Requirements.Features.ProofKeyForCodeExchange },
        };

        yield return new OpenIddictApplicationDescriptor
        {
            ClientId = ProofClients.DeviceClientId,
            ClientType = ClientTypes.Public,
            Permissions =
            {
                Permissions.Endpoints.DeviceAuthorization,
                Permissions.Endpoints.Token,
                Permissions.GrantTypes.DeviceCode,
            },
        };
    }
}
