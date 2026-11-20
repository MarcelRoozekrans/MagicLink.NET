using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictExceptions;

namespace MagicLink.PostgreSql;

/// <summary>OpenIddict's application store on PostgreSQL.</summary>
internal sealed class MagicLinkApplicationStore(StoreDatabase database) : IOpenIddictApplicationStore<MagicLinkApplication>
{
    private const string ConcurrencyMessage =
        "The application was concurrently updated and cannot be persisted in its current state.\nReload the application from the database and retry the operation.";

    public async ValueTask<long> CountAsync(CancellationToken cancellationToken)
    {
        var connection = database.Connect();
        await using (connection.ConfigureAwait(false))
        {
            return await new ApplicationRepository(connection).CountAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public ValueTask<long> CountAsync<TResult>(Func<IQueryable<MagicLinkApplication>, IQueryable<TResult>> query, CancellationToken cancellationToken) =>
        throw QueryableNotSupported.For(nameof(MagicLinkApplicationStore), nameof(CountAsync));

    public async ValueTask CreateAsync(MagicLinkApplication application, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(application);

        var id = StoreIds.New();
        var token = StoreIds.NewConcurrencyToken();
        var connection = database.Connect();
        await using (connection.ConfigureAwait(false))
        {
            await new ApplicationRepository(connection).InsertAsync(ToRow(application, id, token), cancellationToken).ConfigureAwait(false);
        }

        application.Id = StoreIds.ToText(id);
        application.ConcurrencyToken = token;
    }

    public async ValueTask DeleteAsync(MagicLinkApplication application, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(application);

        // Its authorizations and tokens go with it, by cascade.
        var id = RequireId(application);
        var connection = database.Connect();
        await using (connection.ConfigureAwait(false))
        {
            var deleted = await new ApplicationRepository(connection).DeleteAsync(id, application.ConcurrencyToken ?? string.Empty, cancellationToken).ConfigureAwait(false);
            if (deleted == 0)
            {
                throw new ConcurrencyException(ConcurrencyMessage);
            }
        }
    }

    public async ValueTask<MagicLinkApplication?> FindByIdAsync(string identifier, CancellationToken cancellationToken)
    {
        if (!StoreIds.TryParse(identifier, out var id))
        {
            return null;
        }

        var connection = database.Connect();
        await using (connection.ConfigureAwait(false))
        {
            return ToEntity(await new ApplicationRepository(connection).FindByIdAsync(id, cancellationToken).ConfigureAwait(false));
        }
    }

    public async ValueTask<MagicLinkApplication?> FindByClientIdAsync(string identifier, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(identifier);

        var connection = database.Connect();
        await using (connection.ConfigureAwait(false))
        {
            return ToEntity(await new ApplicationRepository(connection).FindByClientIdAsync(identifier, cancellationToken).ConfigureAwait(false));
        }
    }

    public async IAsyncEnumerable<MagicLinkApplication> FindByPostLogoutRedirectUriAsync(string uri, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(uri);

        var connection = database.Connect();
        await using (connection.ConfigureAwait(false))
        {
            await foreach (var row in new ApplicationRepository(connection).FindByPostLogoutRedirectUriAsync([uri], cancellationToken).ConfigureAwait(false))
            {
                yield return ToEntity(row)!;
            }
        }
    }

    public async IAsyncEnumerable<MagicLinkApplication> FindByRedirectUriAsync(string uri, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(uri);

        var connection = database.Connect();
        await using (connection.ConfigureAwait(false))
        {
            await foreach (var row in new ApplicationRepository(connection).FindByRedirectUriAsync([uri], cancellationToken).ConfigureAwait(false))
            {
                yield return ToEntity(row)!;
            }
        }
    }

    public ValueTask<TResult?> GetAsync<TState, TResult>(Func<IQueryable<MagicLinkApplication>, TState, IQueryable<TResult>> query, TState state, CancellationToken cancellationToken) =>
        throw QueryableNotSupported.For(nameof(MagicLinkApplicationStore), nameof(GetAsync));

    public ValueTask<string?> GetApplicationTypeAsync(MagicLinkApplication application, CancellationToken cancellationToken) => new(Require(application).ApplicationType);

    public ValueTask<string?> GetClientIdAsync(MagicLinkApplication application, CancellationToken cancellationToken) => new(Require(application).ClientId);

    public ValueTask<string?> GetClientSecretAsync(MagicLinkApplication application, CancellationToken cancellationToken) => new(Require(application).ClientSecret);

    public ValueTask<string?> GetClientTypeAsync(MagicLinkApplication application, CancellationToken cancellationToken) => new(Require(application).ClientType);

    public ValueTask<string?> GetConsentTypeAsync(MagicLinkApplication application, CancellationToken cancellationToken) => new(Require(application).ConsentType);

    public ValueTask<string?> GetDisplayNameAsync(MagicLinkApplication application, CancellationToken cancellationToken) => new(Require(application).DisplayName);

    public ValueTask<ImmutableDictionary<CultureInfo, string>> GetDisplayNamesAsync(MagicLinkApplication application, CancellationToken cancellationToken) => new(Require(application).DisplayNames);

    public ValueTask<string?> GetIdAsync(MagicLinkApplication application, CancellationToken cancellationToken) => new(Require(application).Id);

    public ValueTask<JsonWebKeySet?> GetJsonWebKeySetAsync(MagicLinkApplication application, CancellationToken cancellationToken) => new(Require(application).JsonWebKeySet);

    public ValueTask<ImmutableArray<string>> GetPermissionsAsync(MagicLinkApplication application, CancellationToken cancellationToken) => new(Require(application).Permissions);

    public ValueTask<ImmutableArray<string>> GetPostLogoutRedirectUrisAsync(MagicLinkApplication application, CancellationToken cancellationToken) => new(Require(application).PostLogoutRedirectUris);

    public ValueTask<ImmutableDictionary<string, JsonElement>> GetPropertiesAsync(MagicLinkApplication application, CancellationToken cancellationToken) => new(Require(application).Properties);

    public ValueTask<ImmutableArray<string>> GetRedirectUrisAsync(MagicLinkApplication application, CancellationToken cancellationToken) => new(Require(application).RedirectUris);

    public ValueTask<ImmutableArray<string>> GetRequirementsAsync(MagicLinkApplication application, CancellationToken cancellationToken) => new(Require(application).Requirements);

    public ValueTask<ImmutableDictionary<string, string>> GetSettingsAsync(MagicLinkApplication application, CancellationToken cancellationToken) => new(Require(application).Settings);

    public ValueTask<MagicLinkApplication> InstantiateAsync(CancellationToken cancellationToken) => new(new MagicLinkApplication());

    public async IAsyncEnumerable<MagicLinkApplication> ListAsync(int? count, int? offset, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var connection = database.Connect();
        await using (connection.ConfigureAwait(false))
        {
            await foreach (var row in new ApplicationRepository(connection).ListAsync(count, offset ?? 0, cancellationToken).ConfigureAwait(false))
            {
                yield return ToEntity(row)!;
            }
        }
    }

    public IAsyncEnumerable<TResult> ListAsync<TState, TResult>(Func<IQueryable<MagicLinkApplication>, TState, IQueryable<TResult>> query, TState state, CancellationToken cancellationToken) =>
        throw QueryableNotSupported.For(nameof(MagicLinkApplicationStore), nameof(ListAsync));

    public ValueTask SetApplicationTypeAsync(MagicLinkApplication application, string? type, CancellationToken cancellationToken) => Set(application, a => a.ApplicationType = type);

    public ValueTask SetClientIdAsync(MagicLinkApplication application, string? identifier, CancellationToken cancellationToken) => Set(application, a => a.ClientId = identifier);

    public ValueTask SetClientSecretAsync(MagicLinkApplication application, string? secret, CancellationToken cancellationToken) => Set(application, a => a.ClientSecret = secret);

    public ValueTask SetClientTypeAsync(MagicLinkApplication application, string? type, CancellationToken cancellationToken) => Set(application, a => a.ClientType = type);

    public ValueTask SetConsentTypeAsync(MagicLinkApplication application, string? type, CancellationToken cancellationToken) => Set(application, a => a.ConsentType = type);

    public ValueTask SetDisplayNameAsync(MagicLinkApplication application, string? name, CancellationToken cancellationToken) => Set(application, a => a.DisplayName = name);

    public ValueTask SetDisplayNamesAsync(MagicLinkApplication application, ImmutableDictionary<CultureInfo, string> names, CancellationToken cancellationToken) => Set(application, a => a.DisplayNames = names);

    public ValueTask SetJsonWebKeySetAsync(MagicLinkApplication application, JsonWebKeySet? set, CancellationToken cancellationToken) => Set(application, a => a.JsonWebKeySet = set);

    public ValueTask SetPermissionsAsync(MagicLinkApplication application, ImmutableArray<string> permissions, CancellationToken cancellationToken) => Set(application, a => a.Permissions = permissions);

    public ValueTask SetPostLogoutRedirectUrisAsync(MagicLinkApplication application, ImmutableArray<string> uris, CancellationToken cancellationToken) => Set(application, a => a.PostLogoutRedirectUris = uris);

    public ValueTask SetPropertiesAsync(MagicLinkApplication application, ImmutableDictionary<string, JsonElement> properties, CancellationToken cancellationToken) => Set(application, a => a.Properties = properties);

    public ValueTask SetRedirectUrisAsync(MagicLinkApplication application, ImmutableArray<string> uris, CancellationToken cancellationToken) => Set(application, a => a.RedirectUris = uris);

    public ValueTask SetRequirementsAsync(MagicLinkApplication application, ImmutableArray<string> requirements, CancellationToken cancellationToken) => Set(application, a => a.Requirements = requirements);

    public ValueTask SetSettingsAsync(MagicLinkApplication application, ImmutableDictionary<string, string> settings, CancellationToken cancellationToken) => Set(application, a => a.Settings = settings);

    public async ValueTask UpdateAsync(MagicLinkApplication application, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(application);

        var id = RequireId(application);
        var token = StoreIds.NewConcurrencyToken();
        var connection = database.Connect();
        await using (connection.ConfigureAwait(false))
        {
            var updated = await new ApplicationRepository(connection)
                .UpdateAsync(ToRow(application, id, token), application.ConcurrencyToken ?? string.Empty, cancellationToken)
                .ConfigureAwait(false);
            if (updated == 0)
            {
                throw new ConcurrencyException(ConcurrencyMessage);
            }
        }

        application.ConcurrencyToken = token;
    }

    private static MagicLinkApplication Require(MagicLinkApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);
        return application;
    }

    private static ValueTask Set(MagicLinkApplication application, Action<MagicLinkApplication> set)
    {
        set(Require(application));
        return ValueTask.CompletedTask;
    }

    private static Guid RequireId(MagicLinkApplication application) =>
        StoreIds.TryParse(application.Id, out var id) ? id : throw new ArgumentException("The application has not been created by this store.", nameof(application));

    private static ApplicationRow ToRow(MagicLinkApplication application, Guid id, string token) => new(
        id,
        token,
        application.ApplicationType,
        application.ClientId,
        application.ClientSecret,
        application.ClientType,
        application.ConsentType,
        application.DisplayName,
        StoreJson.WriteLocalized(application.DisplayNames),
        StoreJson.WriteKeySet(application.JsonWebKeySet),
        [.. application.Permissions.OrEmpty()],
        [.. application.PostLogoutRedirectUris.OrEmpty()],
        [.. application.RedirectUris.OrEmpty()],
        [.. application.Requirements.OrEmpty()],
        StoreJson.WriteStrings(application.Settings),
        StoreJson.WriteProperties(application.Properties));

    private static MagicLinkApplication? ToEntity(ApplicationRow? row) => row is null ? null : new MagicLinkApplication
    {
        Id = StoreIds.ToText(row.Id),
        ConcurrencyToken = row.ConcurrencyToken,
        ApplicationType = row.ApplicationType,
        ClientId = row.ClientId,
        ClientSecret = row.ClientSecret,
        ClientType = row.ClientType,
        ConsentType = row.ConsentType,
        DisplayName = row.DisplayName,
        DisplayNames = StoreJson.ReadLocalized(row.DisplayNames),
        JsonWebKeySet = StoreJson.ReadKeySet(row.JsonWebKeySet),
        Permissions = [.. row.Permissions],
        PostLogoutRedirectUris = [.. row.PostLogoutRedirectUris],
        RedirectUris = [.. row.RedirectUris],
        Requirements = [.. row.Requirements],
        Settings = StoreJson.ReadStrings(row.Settings),
        Properties = StoreJson.ReadProperties(row.Properties),
    };
}
