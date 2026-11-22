using Npgsql;
using OpenIddict.Abstractions;

namespace MagicLink.Host;

/// <summary>
/// Makes the store hold the clients of the settings when the host starts: it adds new ones, updates the others to match,
/// and removes clients that are no longer in the settings, with their authorizations and tokens. The container has no
/// other way to register clients, so its settings are the full list. Instances that start together take turns, under a
/// PostgreSQL advisory lock.
/// </summary>
internal sealed partial class ClientRegistration(IServiceProvider services, HostSetup setup, ILogger<ClientRegistration> logger) : IHostedService
{
    // "MLNETCL1": the host's own advisory lock, apart from the store's migration lock.
    private const long LockKey = 0x4D4C_4E45_5443_4C31L;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(setup.Configuration.Store.ConnectionString);
        await using (connection.ConfigureAwait(false))
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await LockAsync(connection, "SELECT pg_advisory_lock(@key)", cancellationToken).ConfigureAwait(false);
            try
            {
                await RegisterAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                await LockAsync(connection, "SELECT pg_advisory_unlock(@key)", CancellationToken.None).ConfigureAwait(false);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task LockAsync(NpgsqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        var command = new NpgsqlCommand(sql, connection);
        await using (command.ConfigureAwait(false))
        {
            command.Parameters.AddWithValue("key", LockKey);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task RegisterAsync(CancellationToken cancellationToken)
    {
        var scope = services.CreateAsyncScope();
        await using (scope.ConfigureAwait(false))
        {
            var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
            var configured = setup.Clients.Select(static client => client.ClientId!).ToHashSet(StringComparer.Ordinal);

            var stored = new List<object>();
            await foreach (var application in manager.ListAsync(count: null, offset: null, cancellationToken).ConfigureAwait(false))
            {
                stored.Add(application);
            }

            // Adds and updates first, then removes: when the store refuses a client, the start fails without having
            // removed any.
            foreach (var client in setup.Clients)
            {
                // A fresh copy each time: OpenIddict may change the descriptor it is given, such as hashing the secret.
                var descriptor = Copy(client);
                var application = await manager.FindByClientIdAsync(descriptor.ClientId!, cancellationToken).ConfigureAwait(false);
                if (application is null)
                {
                    await manager.CreateAsync(descriptor, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await manager.UpdateAsync(application, descriptor, cancellationToken).ConfigureAwait(false);
                }
            }

            foreach (var application in stored)
            {
                var clientId = await manager.GetClientIdAsync(application, cancellationToken).ConfigureAwait(false);
                if (clientId is null || !configured.Contains(clientId))
                {
                    await manager.DeleteAsync(application, cancellationToken).ConfigureAwait(false);
                    LogRemoved(logger, clientId);
                }
            }

            LogRegistered(logger, setup.Clients.Count);
        }
    }

    private static OpenIddictApplicationDescriptor Copy(OpenIddictApplicationDescriptor client)
    {
        var copy = new OpenIddictApplicationDescriptor
        {
            ClientId = client.ClientId,
            ClientSecret = client.ClientSecret,
            ClientType = client.ClientType,
            DisplayName = client.DisplayName,
        };
        copy.Permissions.UnionWith(client.Permissions);
        copy.Requirements.UnionWith(client.Requirements);
        copy.RedirectUris.UnionWith(client.RedirectUris);
        return copy;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Client {ClientId} is no longer in the settings, so it was removed, with its authorizations and tokens.")]
    private static partial void LogRemoved(ILogger logger, string? clientId);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Count} clients from the settings are registered.")]
    private static partial void LogRegistered(ILogger logger, int count);
}
