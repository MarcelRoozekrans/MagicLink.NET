using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictExceptions;

namespace MagicLink.PostgreSql;

/// <summary>OpenIddict's scope store on PostgreSQL.</summary>
internal sealed class MagicLinkScopeStore(StoreDatabase database) : IOpenIddictScopeStore<MagicLinkScope>
{
    private const string ConcurrencyMessage =
        "The scope was concurrently updated and cannot be persisted in its current state.\nReload the scope from the database and retry the operation.";

    public async ValueTask<long> CountAsync(CancellationToken cancellationToken)
    {
        var connection = database.Connect();
        await using (connection.ConfigureAwait(false))
        {
            return await new ScopeRepository(connection).CountAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public ValueTask<long> CountAsync<TResult>(Func<IQueryable<MagicLinkScope>, IQueryable<TResult>> query, CancellationToken cancellationToken) =>
        throw QueryableNotSupported.For(nameof(MagicLinkScopeStore), nameof(CountAsync));

    public async ValueTask CreateAsync(MagicLinkScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var id = StoreIds.New();
        var token = StoreIds.NewConcurrencyToken();
        var connection = database.Connect();
        await using (connection.ConfigureAwait(false))
        {
            await new ScopeRepository(connection).InsertAsync(ToRow(scope, id, token), cancellationToken).ConfigureAwait(false);
        }

        scope.Id = StoreIds.ToText(id);
        scope.ConcurrencyToken = token;
    }

    public async ValueTask DeleteAsync(MagicLinkScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var id = RequireId(scope);
        var connection = database.Connect();
        await using (connection.ConfigureAwait(false))
        {
            if (await new ScopeRepository(connection).DeleteAsync(id, scope.ConcurrencyToken ?? string.Empty, cancellationToken).ConfigureAwait(false) == 0)
            {
                throw new ConcurrencyException(ConcurrencyMessage);
            }
        }
    }

    public async ValueTask<MagicLinkScope?> FindByIdAsync(string identifier, CancellationToken cancellationToken)
    {
        if (!StoreIds.TryParse(identifier, out var id))
        {
            return null;
        }

        var connection = database.Connect();
        await using (connection.ConfigureAwait(false))
        {
            return ToEntity(await new ScopeRepository(connection).FindByIdAsync(id, cancellationToken).ConfigureAwait(false));
        }
    }

    public async ValueTask<MagicLinkScope?> FindByNameAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        var connection = database.Connect();
        await using (connection.ConfigureAwait(false))
        {
            return ToEntity(await new ScopeRepository(connection).FindByNameAsync(name, cancellationToken).ConfigureAwait(false));
        }
    }

    public async IAsyncEnumerable<MagicLinkScope> FindByNamesAsync(ImmutableArray<string> names, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (names.IsDefaultOrEmpty)
        {
            yield break;
        }

        var connection = database.Connect();
        await using (connection.ConfigureAwait(false))
        {
            await foreach (var row in new ScopeRepository(connection).FindByNamesAsync([.. names], cancellationToken).ConfigureAwait(false))
            {
                yield return ToEntity(row)!;
            }
        }
    }

    public async IAsyncEnumerable<MagicLinkScope> FindByResourceAsync(string resource, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(resource);

        var connection = database.Connect();
        await using (connection.ConfigureAwait(false))
        {
            await foreach (var row in new ScopeRepository(connection).FindByResourceAsync([resource], cancellationToken).ConfigureAwait(false))
            {
                yield return ToEntity(row)!;
            }
        }
    }

    public ValueTask<TResult?> GetAsync<TState, TResult>(Func<IQueryable<MagicLinkScope>, TState, IQueryable<TResult>> query, TState state, CancellationToken cancellationToken) =>
        throw QueryableNotSupported.For(nameof(MagicLinkScopeStore), nameof(GetAsync));

    public ValueTask<string?> GetDescriptionAsync(MagicLinkScope scope, CancellationToken cancellationToken) => new(Require(scope).Description);

    public ValueTask<ImmutableDictionary<CultureInfo, string>> GetDescriptionsAsync(MagicLinkScope scope, CancellationToken cancellationToken) => new(Require(scope).Descriptions);

    public ValueTask<string?> GetDisplayNameAsync(MagicLinkScope scope, CancellationToken cancellationToken) => new(Require(scope).DisplayName);

    public ValueTask<ImmutableDictionary<CultureInfo, string>> GetDisplayNamesAsync(MagicLinkScope scope, CancellationToken cancellationToken) => new(Require(scope).DisplayNames);

    public ValueTask<string?> GetIdAsync(MagicLinkScope scope, CancellationToken cancellationToken) => new(Require(scope).Id);

    public ValueTask<string?> GetNameAsync(MagicLinkScope scope, CancellationToken cancellationToken) => new(Require(scope).Name);

    public ValueTask<ImmutableDictionary<string, JsonElement>> GetPropertiesAsync(MagicLinkScope scope, CancellationToken cancellationToken) => new(Require(scope).Properties);

    public ValueTask<ImmutableArray<string>> GetResourcesAsync(MagicLinkScope scope, CancellationToken cancellationToken) => new(Require(scope).Resources);

    public ValueTask<MagicLinkScope> InstantiateAsync(CancellationToken cancellationToken) => new(new MagicLinkScope());

    public async IAsyncEnumerable<MagicLinkScope> ListAsync(int? count, int? offset, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var connection = database.Connect();
        await using (connection.ConfigureAwait(false))
        {
            await foreach (var row in new ScopeRepository(connection).ListAsync(count, offset ?? 0, cancellationToken).ConfigureAwait(false))
            {
                yield return ToEntity(row)!;
            }
        }
    }

    public IAsyncEnumerable<TResult> ListAsync<TState, TResult>(Func<IQueryable<MagicLinkScope>, TState, IQueryable<TResult>> query, TState state, CancellationToken cancellationToken) =>
        throw QueryableNotSupported.For(nameof(MagicLinkScopeStore), nameof(ListAsync));

    public ValueTask SetDescriptionAsync(MagicLinkScope scope, string? description, CancellationToken cancellationToken) => Set(scope, s => s.Description = description);

    public ValueTask SetDescriptionsAsync(MagicLinkScope scope, ImmutableDictionary<CultureInfo, string> descriptions, CancellationToken cancellationToken) => Set(scope, s => s.Descriptions = descriptions);

    public ValueTask SetDisplayNameAsync(MagicLinkScope scope, string? name, CancellationToken cancellationToken) => Set(scope, s => s.DisplayName = name);

    public ValueTask SetDisplayNamesAsync(MagicLinkScope scope, ImmutableDictionary<CultureInfo, string> names, CancellationToken cancellationToken) => Set(scope, s => s.DisplayNames = names);

    public ValueTask SetNameAsync(MagicLinkScope scope, string? name, CancellationToken cancellationToken) => Set(scope, s => s.Name = name);

    public ValueTask SetPropertiesAsync(MagicLinkScope scope, ImmutableDictionary<string, JsonElement> properties, CancellationToken cancellationToken) => Set(scope, s => s.Properties = properties);

    public ValueTask SetResourcesAsync(MagicLinkScope scope, ImmutableArray<string> resources, CancellationToken cancellationToken) => Set(scope, s => s.Resources = resources);

    public async ValueTask UpdateAsync(MagicLinkScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var id = RequireId(scope);
        var token = StoreIds.NewConcurrencyToken();
        var connection = database.Connect();
        await using (connection.ConfigureAwait(false))
        {
            if (await new ScopeRepository(connection).UpdateAsync(ToRow(scope, id, token), scope.ConcurrencyToken ?? string.Empty, cancellationToken).ConfigureAwait(false) == 0)
            {
                throw new ConcurrencyException(ConcurrencyMessage);
            }
        }

        scope.ConcurrencyToken = token;
    }

    private static MagicLinkScope Require(MagicLinkScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return scope;
    }

    private static ValueTask Set(MagicLinkScope scope, Action<MagicLinkScope> set)
    {
        set(Require(scope));
        return ValueTask.CompletedTask;
    }

    private static Guid RequireId(MagicLinkScope scope) =>
        StoreIds.TryParse(scope.Id, out var id) ? id : throw new ArgumentException("The scope has not been created by this store.", nameof(scope));

    private static ScopeRow ToRow(MagicLinkScope scope, Guid id, string token) => new(
        id,
        token,
        scope.Name,
        scope.Description,
        StoreJson.WriteLocalized(scope.Descriptions),
        scope.DisplayName,
        StoreJson.WriteLocalized(scope.DisplayNames),
        [.. scope.Resources.OrEmpty()],
        StoreJson.WriteProperties(scope.Properties));

    private static MagicLinkScope? ToEntity(ScopeRow? row) => row is null ? null : new MagicLinkScope
    {
        Id = StoreIds.ToText(row.Id),
        ConcurrencyToken = row.ConcurrencyToken,
        Name = row.Name,
        Description = row.Description,
        Descriptions = StoreJson.ReadLocalized(row.Descriptions),
        DisplayName = row.DisplayName,
        DisplayNames = StoreJson.ReadLocalized(row.DisplayNames),
        Resources = [.. row.Resources],
        Properties = StoreJson.ReadProperties(row.Properties),
    };
}
