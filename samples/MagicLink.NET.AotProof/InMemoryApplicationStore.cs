using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;

namespace MagicLink.AotProof;

/// <summary>
/// The smallest OpenIddict application store the server needs, kept in memory. It exists only for the
/// Native AOT proof; the real store on PostgreSQL is Phase 1.3. Queries over <see cref="IQueryable"/>
/// are not supported, since the server never issues them.
/// </summary>
public sealed class InMemoryApplicationStore : IOpenIddictApplicationStore<ProofApplication>
{
    private readonly ConcurrentDictionary<string, ProofApplication> _applications = new(StringComparer.Ordinal);

    public ValueTask<long> CountAsync(CancellationToken cancellationToken) => ValueTask.FromResult((long)_applications.Count);

    public ValueTask<long> CountAsync<TResult>(Func<IQueryable<ProofApplication>, IQueryable<TResult>> query, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public ValueTask CreateAsync(ProofApplication application, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(application);
        if (application.ClientId is not null && _applications.Values.Any(other => string.Equals(other.ClientId, application.ClientId, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("An application with this client id already exists.");
        }

        if (!_applications.TryAdd(application.Id, application))
        {
            throw new InvalidOperationException("An application with this id already exists.");
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask DeleteAsync(ProofApplication application, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(application);
        _applications.TryRemove(application.Id, out _);
        return ValueTask.CompletedTask;
    }

    public ValueTask<ProofApplication?> FindByIdAsync(string identifier, CancellationToken cancellationToken)
        => ValueTask.FromResult(string.IsNullOrEmpty(identifier) ? null : _applications.GetValueOrDefault(identifier));

    public ValueTask<ProofApplication?> FindByClientIdAsync(string identifier, CancellationToken cancellationToken)
        => ValueTask.FromResult(string.IsNullOrEmpty(identifier)
            ? null
            : _applications.Values.FirstOrDefault(application => string.Equals(application.ClientId, identifier, StringComparison.Ordinal)));

    public IAsyncEnumerable<ProofApplication> FindByPostLogoutRedirectUriAsync(string uri, CancellationToken cancellationToken)
        => Where(application => application.PostLogoutRedirectUris.Contains(uri, StringComparer.Ordinal), cancellationToken);

    public IAsyncEnumerable<ProofApplication> FindByRedirectUriAsync(string uri, CancellationToken cancellationToken)
        => Where(application => application.RedirectUris.Contains(uri, StringComparer.Ordinal), cancellationToken);

    public ValueTask<string?> GetApplicationTypeAsync(ProofApplication application, CancellationToken cancellationToken) => Get(application, a => a.ApplicationType);

    public ValueTask<TResult?> GetAsync<TState, TResult>(Func<IQueryable<ProofApplication>, TState, IQueryable<TResult>> query, TState state, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public ValueTask<string?> GetClientIdAsync(ProofApplication application, CancellationToken cancellationToken) => Get(application, a => a.ClientId);

    public ValueTask<string?> GetClientSecretAsync(ProofApplication application, CancellationToken cancellationToken) => Get(application, a => a.ClientSecret);

    public ValueTask<string?> GetClientTypeAsync(ProofApplication application, CancellationToken cancellationToken) => Get(application, a => a.ClientType);

    public ValueTask<string?> GetConsentTypeAsync(ProofApplication application, CancellationToken cancellationToken) => Get(application, a => a.ConsentType);

    public ValueTask<string?> GetDisplayNameAsync(ProofApplication application, CancellationToken cancellationToken) => Get(application, a => a.DisplayName);

    public ValueTask<ImmutableDictionary<CultureInfo, string>> GetDisplayNamesAsync(ProofApplication application, CancellationToken cancellationToken) => Get(application, a => a.DisplayNames);

    public ValueTask<string?> GetIdAsync(ProofApplication application, CancellationToken cancellationToken) => Get<string?>(application, a => a.Id);

    public ValueTask<JsonWebKeySet?> GetJsonWebKeySetAsync(ProofApplication application, CancellationToken cancellationToken) => Get(application, a => a.JsonWebKeySet);

    public ValueTask<ImmutableArray<string>> GetPermissionsAsync(ProofApplication application, CancellationToken cancellationToken) => Get(application, a => a.Permissions);

    public ValueTask<ImmutableArray<string>> GetPostLogoutRedirectUrisAsync(ProofApplication application, CancellationToken cancellationToken) => Get(application, a => a.PostLogoutRedirectUris);

    public ValueTask<ImmutableDictionary<string, JsonElement>> GetPropertiesAsync(ProofApplication application, CancellationToken cancellationToken) => Get(application, a => a.Properties);

    public ValueTask<ImmutableArray<string>> GetRedirectUrisAsync(ProofApplication application, CancellationToken cancellationToken) => Get(application, a => a.RedirectUris);

    public ValueTask<ImmutableArray<string>> GetRequirementsAsync(ProofApplication application, CancellationToken cancellationToken) => Get(application, a => a.Requirements);

    public ValueTask<ImmutableDictionary<string, string>> GetSettingsAsync(ProofApplication application, CancellationToken cancellationToken) => Get(application, a => a.Settings);

    public ValueTask<ProofApplication> InstantiateAsync(CancellationToken cancellationToken) => ValueTask.FromResult(new ProofApplication());

    public IAsyncEnumerable<ProofApplication> ListAsync(int? count, int? offset, CancellationToken cancellationToken)
    {
        var applications = _applications.Values.OrderBy(application => application.Id, StringComparer.Ordinal).Skip(offset ?? 0);
        return ToAsync(count is { } take ? applications.Take(take) : applications, cancellationToken);
    }

    public IAsyncEnumerable<TResult> ListAsync<TState, TResult>(Func<IQueryable<ProofApplication>, TState, IQueryable<TResult>> query, TState state, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public ValueTask SetApplicationTypeAsync(ProofApplication application, string? type, CancellationToken cancellationToken) => Set(application, a => a.ApplicationType = type);

    public ValueTask SetClientIdAsync(ProofApplication application, string? identifier, CancellationToken cancellationToken) => Set(application, a => a.ClientId = identifier);

    public ValueTask SetClientSecretAsync(ProofApplication application, string? secret, CancellationToken cancellationToken) => Set(application, a => a.ClientSecret = secret);

    public ValueTask SetClientTypeAsync(ProofApplication application, string? type, CancellationToken cancellationToken) => Set(application, a => a.ClientType = type);

    public ValueTask SetConsentTypeAsync(ProofApplication application, string? type, CancellationToken cancellationToken) => Set(application, a => a.ConsentType = type);

    public ValueTask SetDisplayNameAsync(ProofApplication application, string? name, CancellationToken cancellationToken) => Set(application, a => a.DisplayName = name);

    public ValueTask SetDisplayNamesAsync(ProofApplication application, ImmutableDictionary<CultureInfo, string> names, CancellationToken cancellationToken) => Set(application, a => a.DisplayNames = names);

    public ValueTask SetJsonWebKeySetAsync(ProofApplication application, JsonWebKeySet? set, CancellationToken cancellationToken) => Set(application, a => a.JsonWebKeySet = set);

    public ValueTask SetPermissionsAsync(ProofApplication application, ImmutableArray<string> permissions, CancellationToken cancellationToken) => Set(application, a => a.Permissions = permissions);

    public ValueTask SetPostLogoutRedirectUrisAsync(ProofApplication application, ImmutableArray<string> uris, CancellationToken cancellationToken) => Set(application, a => a.PostLogoutRedirectUris = uris);

    public ValueTask SetPropertiesAsync(ProofApplication application, ImmutableDictionary<string, JsonElement> properties, CancellationToken cancellationToken) => Set(application, a => a.Properties = properties);

    public ValueTask SetRedirectUrisAsync(ProofApplication application, ImmutableArray<string> uris, CancellationToken cancellationToken) => Set(application, a => a.RedirectUris = uris);

    public ValueTask SetRequirementsAsync(ProofApplication application, ImmutableArray<string> requirements, CancellationToken cancellationToken) => Set(application, a => a.Requirements = requirements);

    public ValueTask SetSettingsAsync(ProofApplication application, ImmutableDictionary<string, string> settings, CancellationToken cancellationToken) => Set(application, a => a.Settings = settings);

    public ValueTask UpdateAsync(ProofApplication application, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(application);
        _applications[application.Id] = application;
        return ValueTask.CompletedTask;
    }

    private static ValueTask<T> Get<T>(ProofApplication application, Func<ProofApplication, T> read)
    {
        ArgumentNullException.ThrowIfNull(application);
        return ValueTask.FromResult(read(application));
    }

    private static ValueTask Set(ProofApplication application, Action<ProofApplication> write)
    {
        ArgumentNullException.ThrowIfNull(application);
        write(application);
        return ValueTask.CompletedTask;
    }

    private IAsyncEnumerable<ProofApplication> Where(Func<ProofApplication, bool> predicate, CancellationToken cancellationToken)
        => ToAsync(_applications.Values.Where(predicate).ToList(), cancellationToken);

    private static async IAsyncEnumerable<ProofApplication> ToAsync(IEnumerable<ProofApplication> applications, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var application in applications)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return application;
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }
}
