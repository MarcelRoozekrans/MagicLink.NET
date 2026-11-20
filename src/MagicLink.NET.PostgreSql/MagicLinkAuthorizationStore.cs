using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text.Json;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictExceptions;

namespace MagicLink.PostgreSql;

/// <summary>OpenIddict's authorization store on PostgreSQL.</summary>
internal sealed class MagicLinkAuthorizationStore(StoreDatabase database) : IOpenIddictAuthorizationStore<MagicLinkAuthorization>
{
    private const string ConcurrencyMessage =
        "The authorization was concurrently updated and cannot be persisted in its current state.\nReload the authorization from the database and retry the operation.";

    public async ValueTask<long> CountAsync(CancellationToken cancellationToken)
    {
        var connection = database.Connect();
        await using (connection.ConfigureAwait(false))
        {
            return await new AuthorizationRepository(connection).CountAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public ValueTask<long> CountAsync<TResult>(Func<IQueryable<MagicLinkAuthorization>, IQueryable<TResult>> query, CancellationToken cancellationToken) =>
        throw QueryableNotSupported.For(nameof(MagicLinkAuthorizationStore), nameof(CountAsync));

    public async ValueTask CreateAsync(MagicLinkAuthorization authorization, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authorization);

        var id = StoreIds.New();
        var token = StoreIds.NewConcurrencyToken();
        var row = ToRow(authorization, id, token);
        var connection = database.Connect();
        await using (connection.ConfigureAwait(false))
        {
            await new AuthorizationRepository(connection).InsertAsync(row, cancellationToken).ConfigureAwait(false);
        }

        authorization.Id = StoreIds.ToText(id);
        authorization.ConcurrencyToken = token;
    }

    public async ValueTask DeleteAsync(MagicLinkAuthorization authorization, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authorization);

        var id = RequireId(authorization);
        var connection = database.Connect();
        await using (connection.ConfigureAwait(false))
        {
            if (await new AuthorizationRepository(connection).DeleteAsync(id, authorization.ConcurrencyToken ?? string.Empty, cancellationToken).ConfigureAwait(false) == 0)
            {
                throw new ConcurrencyException(ConcurrencyMessage);
            }
        }
    }

    public async IAsyncEnumerable<MagicLinkAuthorization> FindAsync(
        string? subject, string? client, string? status, string? type, ImmutableArray<string>? scopes, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var filter = StoreFilter.From(subject, client, status, type);
        if (filter.MatchesNothing)
        {
            yield break;
        }

        string[]? required = scopes is { } asked ? (asked.IsDefault ? [] : [.. asked]) : null;
        var connection = database.Connect();
        await using (connection.ConfigureAwait(false))
        {
            var rows = new AuthorizationRepository(connection).FindAsync(filter.Subject, filter.Client, filter.Status, filter.Type, required, cancellationToken);
            await foreach (var row in rows.ConfigureAwait(false))
            {
                yield return ToEntity(row)!;
            }
        }
    }

    public async IAsyncEnumerable<MagicLinkAuthorization> FindByApplicationIdAsync(string identifier, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(identifier);
        if (!StoreIds.TryParse(identifier, out var id))
        {
            yield break;
        }

        var connection = database.Connect();
        await using (connection.ConfigureAwait(false))
        {
            await foreach (var row in new AuthorizationRepository(connection).FindByApplicationIdAsync(id, cancellationToken).ConfigureAwait(false))
            {
                yield return ToEntity(row)!;
            }
        }
    }

    public async ValueTask<MagicLinkAuthorization?> FindByIdAsync(string identifier, CancellationToken cancellationToken)
    {
        if (!StoreIds.TryParse(identifier, out var id))
        {
            return null;
        }

        var connection = database.Connect();
        await using (connection.ConfigureAwait(false))
        {
            return ToEntity(await new AuthorizationRepository(connection).FindByIdAsync(id, cancellationToken).ConfigureAwait(false));
        }
    }

    public async IAsyncEnumerable<MagicLinkAuthorization> FindBySubjectAsync(string subject, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(subject);

        var connection = database.Connect();
        await using (connection.ConfigureAwait(false))
        {
            await foreach (var row in new AuthorizationRepository(connection).FindBySubjectAsync(subject, cancellationToken).ConfigureAwait(false))
            {
                yield return ToEntity(row)!;
            }
        }
    }

    public ValueTask<string?> GetApplicationIdAsync(MagicLinkAuthorization authorization, CancellationToken cancellationToken) => new(Require(authorization).ApplicationId);

    public ValueTask<TResult?> GetAsync<TState, TResult>(Func<IQueryable<MagicLinkAuthorization>, TState, IQueryable<TResult>> query, TState state, CancellationToken cancellationToken) =>
        throw QueryableNotSupported.For(nameof(MagicLinkAuthorizationStore), nameof(GetAsync));

    public ValueTask<DateTimeOffset?> GetCreationDateAsync(MagicLinkAuthorization authorization, CancellationToken cancellationToken) => new(Require(authorization).CreationDate);

    public ValueTask<string?> GetIdAsync(MagicLinkAuthorization authorization, CancellationToken cancellationToken) => new(Require(authorization).Id);

    public ValueTask<ImmutableDictionary<string, JsonElement>> GetPropertiesAsync(MagicLinkAuthorization authorization, CancellationToken cancellationToken) => new(Require(authorization).Properties);

    public ValueTask<ImmutableArray<string>> GetScopesAsync(MagicLinkAuthorization authorization, CancellationToken cancellationToken) => new(Require(authorization).Scopes);

    public ValueTask<string?> GetStatusAsync(MagicLinkAuthorization authorization, CancellationToken cancellationToken) => new(Require(authorization).Status);

    public ValueTask<string?> GetSubjectAsync(MagicLinkAuthorization authorization, CancellationToken cancellationToken) => new(Require(authorization).Subject);

    public ValueTask<string?> GetTypeAsync(MagicLinkAuthorization authorization, CancellationToken cancellationToken) => new(Require(authorization).Type);

    public ValueTask<MagicLinkAuthorization> InstantiateAsync(CancellationToken cancellationToken) => new(new MagicLinkAuthorization());

    public async IAsyncEnumerable<MagicLinkAuthorization> ListAsync(int? count, int? offset, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var connection = database.Connect();
        await using (connection.ConfigureAwait(false))
        {
            await foreach (var row in new AuthorizationRepository(connection).ListAsync(count, offset ?? 0, cancellationToken).ConfigureAwait(false))
            {
                yield return ToEntity(row)!;
            }
        }
    }

    public IAsyncEnumerable<TResult> ListAsync<TState, TResult>(Func<IQueryable<MagicLinkAuthorization>, TState, IQueryable<TResult>> query, TState state, CancellationToken cancellationToken) =>
        throw QueryableNotSupported.For(nameof(MagicLinkAuthorizationStore), nameof(ListAsync));

    public async ValueTask<long> PruneAsync(DateTimeOffset threshold, CancellationToken cancellationToken)
    {
        var connection = database.Connect();
        await using (connection.ConfigureAwait(false))
        {
            var repository = new AuthorizationRepository(connection);
            long total = 0;
            for (var batch = 0; batch < StoreFilter.PruneBatches; batch++)
            {
                var deleted = await repository.PruneBatchAsync(threshold.ToUniversalTime(), cancellationToken).ConfigureAwait(false);
                total += deleted;
                if (deleted < StoreFilter.PruneBatch)
                {
                    break;
                }
            }

            return total;
        }
    }

    public async ValueTask<long> RevokeAsync(string? subject, string? client, string? status, string? type, CancellationToken cancellationToken)
    {
        var filter = StoreFilter.From(subject, client, status, type);
        if (filter.MatchesNothing)
        {
            return 0;
        }

        var connection = database.Connect();
        await using (connection.ConfigureAwait(false))
        {
            return await new AuthorizationRepository(connection).RevokeAsync(filter.Subject, filter.Client, filter.Status, filter.Type, cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask<long> RevokeByApplicationIdAsync(string identifier, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(identifier);
        if (!StoreIds.TryParse(identifier, out var id))
        {
            return 0;
        }

        var connection = database.Connect();
        await using (connection.ConfigureAwait(false))
        {
            return await new AuthorizationRepository(connection).RevokeByApplicationIdAsync(id, cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask<long> RevokeBySubjectAsync(string subject, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(subject);

        var connection = database.Connect();
        await using (connection.ConfigureAwait(false))
        {
            return await new AuthorizationRepository(connection).RevokeBySubjectAsync(subject, cancellationToken).ConfigureAwait(false);
        }
    }

    public ValueTask SetApplicationIdAsync(MagicLinkAuthorization authorization, string? identifier, CancellationToken cancellationToken) => Set(authorization, a => a.ApplicationId = identifier);

    public ValueTask SetCreationDateAsync(MagicLinkAuthorization authorization, DateTimeOffset? date, CancellationToken cancellationToken) => Set(authorization, a => a.CreationDate = date);

    public ValueTask SetPropertiesAsync(MagicLinkAuthorization authorization, ImmutableDictionary<string, JsonElement> properties, CancellationToken cancellationToken) => Set(authorization, a => a.Properties = properties);

    public ValueTask SetScopesAsync(MagicLinkAuthorization authorization, ImmutableArray<string> scopes, CancellationToken cancellationToken) => Set(authorization, a => a.Scopes = scopes);

    public ValueTask SetStatusAsync(MagicLinkAuthorization authorization, string? status, CancellationToken cancellationToken) => Set(authorization, a => a.Status = status);

    public ValueTask SetSubjectAsync(MagicLinkAuthorization authorization, string? subject, CancellationToken cancellationToken) => Set(authorization, a => a.Subject = subject);

    public ValueTask SetTypeAsync(MagicLinkAuthorization authorization, string? type, CancellationToken cancellationToken) => Set(authorization, a => a.Type = type);

    public async ValueTask UpdateAsync(MagicLinkAuthorization authorization, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authorization);

        var id = RequireId(authorization);
        var token = StoreIds.NewConcurrencyToken();
        var row = ToRow(authorization, id, token);
        var connection = database.Connect();
        await using (connection.ConfigureAwait(false))
        {
            if (await new AuthorizationRepository(connection).UpdateAsync(row, authorization.ConcurrencyToken ?? string.Empty, cancellationToken).ConfigureAwait(false) == 0)
            {
                throw new ConcurrencyException(ConcurrencyMessage);
            }
        }

        authorization.ConcurrencyToken = token;
    }

    private static MagicLinkAuthorization Require(MagicLinkAuthorization authorization)
    {
        ArgumentNullException.ThrowIfNull(authorization);
        return authorization;
    }

    private static ValueTask Set(MagicLinkAuthorization authorization, Action<MagicLinkAuthorization> set)
    {
        set(Require(authorization));
        return ValueTask.CompletedTask;
    }

    private static Guid RequireId(MagicLinkAuthorization authorization) =>
        StoreIds.TryParse(authorization.Id, out var id) ? id : throw new ArgumentException("The authorization has not been created by this store.", nameof(authorization));

    private static AuthorizationRow ToRow(MagicLinkAuthorization authorization, Guid id, string token) => new(
        id,
        token,
        StoreIds.ParseReference(authorization.ApplicationId, nameof(authorization)),
        authorization.CreationDate?.ToUniversalTime(),
        StoreJson.WriteProperties(authorization.Properties),
        [.. authorization.Scopes.OrEmpty()],
        authorization.Status,
        authorization.Subject,
        authorization.Type);

    private static MagicLinkAuthorization? ToEntity(AuthorizationRow? row) => row is null ? null : new MagicLinkAuthorization
    {
        Id = StoreIds.ToText(row.Id),
        ConcurrencyToken = row.ConcurrencyToken,
        ApplicationId = StoreIds.ToText(row.ApplicationId),
        CreationDate = row.CreationDate,
        Properties = StoreJson.ReadProperties(row.Properties),
        Scopes = [.. row.Scopes],
        Status = row.Status,
        Subject = row.Subject,
        Type = row.Type,
    };
}
