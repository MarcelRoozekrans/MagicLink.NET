using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text.Json;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictExceptions;

namespace MagicLink.PostgreSql;

/// <summary>OpenIddict's token store on PostgreSQL.</summary>
internal sealed class MagicLinkTokenStore(StoreDatabase database) : IOpenIddictTokenStore<MagicLinkToken>
{
    private const string ConcurrencyMessage =
        "The token was concurrently updated and cannot be persisted in its current state.\nReload the token from the database and retry the operation.";

    public async ValueTask<long> CountAsync(CancellationToken cancellationToken)
    {
        var connection = database.Connect();
        await using (connection.ConfigureAwait(false))
        {
            return await new TokenRepository(connection).CountAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public ValueTask<long> CountAsync<TResult>(Func<IQueryable<MagicLinkToken>, IQueryable<TResult>> query, CancellationToken cancellationToken) =>
        throw QueryableNotSupported.For(nameof(MagicLinkTokenStore), nameof(CountAsync));

    public async ValueTask CreateAsync(MagicLinkToken token, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        var id = StoreIds.New();
        var concurrencyToken = StoreIds.NewConcurrencyToken();
        var row = ToRow(token, id, concurrencyToken);
        var connection = database.Connect();
        await using (connection.ConfigureAwait(false))
        {
            await new TokenRepository(connection).InsertAsync(row, cancellationToken).ConfigureAwait(false);
        }

        token.Id = StoreIds.ToText(id);
        token.ConcurrencyToken = concurrencyToken;
    }

    public async ValueTask DeleteAsync(MagicLinkToken token, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        var id = RequireId(token);
        var connection = database.Connect();
        await using (connection.ConfigureAwait(false))
        {
            if (await new TokenRepository(connection).DeleteAsync(id, token.ConcurrencyToken ?? string.Empty, cancellationToken).ConfigureAwait(false) == 0)
            {
                throw new ConcurrencyException(ConcurrencyMessage);
            }
        }
    }

    public async IAsyncEnumerable<MagicLinkToken> FindAsync(string? subject, string? client, string? status, string? type, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var filter = StoreFilter.From(subject, client, status, type);
        if (filter.MatchesNothing)
        {
            yield break;
        }

        var connection = database.Connect();
        await using (connection.ConfigureAwait(false))
        {
            var rows = new TokenRepository(connection).FindAsync(filter.Subject, filter.Client, filter.Status, filter.Type, cancellationToken);
            await foreach (var row in rows.ConfigureAwait(false))
            {
                yield return ToEntity(row)!;
            }
        }
    }

    public async IAsyncEnumerable<MagicLinkToken> FindByApplicationIdAsync(string identifier, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(identifier);
        if (!StoreIds.TryParse(identifier, out var id))
        {
            yield break;
        }

        var connection = database.Connect();
        await using (connection.ConfigureAwait(false))
        {
            await foreach (var row in new TokenRepository(connection).FindByApplicationIdAsync(id, cancellationToken).ConfigureAwait(false))
            {
                yield return ToEntity(row)!;
            }
        }
    }

    public async IAsyncEnumerable<MagicLinkToken> FindByAuthorizationIdAsync(string identifier, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(identifier);
        if (!StoreIds.TryParse(identifier, out var id))
        {
            yield break;
        }

        var connection = database.Connect();
        await using (connection.ConfigureAwait(false))
        {
            await foreach (var row in new TokenRepository(connection).FindByAuthorizationIdAsync(id, cancellationToken).ConfigureAwait(false))
            {
                yield return ToEntity(row)!;
            }
        }
    }

    public async ValueTask<MagicLinkToken?> FindByIdAsync(string identifier, CancellationToken cancellationToken)
    {
        if (!StoreIds.TryParse(identifier, out var id))
        {
            return null;
        }

        var connection = database.Connect();
        await using (connection.ConfigureAwait(false))
        {
            return ToEntity(await new TokenRepository(connection).FindByIdAsync(id, cancellationToken).ConfigureAwait(false));
        }
    }

    public async ValueTask<MagicLinkToken?> FindByReferenceIdAsync(string identifier, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(identifier);

        var connection = database.Connect();
        await using (connection.ConfigureAwait(false))
        {
            return ToEntity(await new TokenRepository(connection).FindByReferenceIdAsync(identifier, cancellationToken).ConfigureAwait(false));
        }
    }

    public async IAsyncEnumerable<MagicLinkToken> FindBySubjectAsync(string subject, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(subject);

        var connection = database.Connect();
        await using (connection.ConfigureAwait(false))
        {
            await foreach (var row in new TokenRepository(connection).FindBySubjectAsync(subject, cancellationToken).ConfigureAwait(false))
            {
                yield return ToEntity(row)!;
            }
        }
    }

    public ValueTask<string?> GetApplicationIdAsync(MagicLinkToken token, CancellationToken cancellationToken) => new(Require(token).ApplicationId);

    public ValueTask<TResult?> GetAsync<TState, TResult>(Func<IQueryable<MagicLinkToken>, TState, IQueryable<TResult>> query, TState state, CancellationToken cancellationToken) =>
        throw QueryableNotSupported.For(nameof(MagicLinkTokenStore), nameof(GetAsync));

    public ValueTask<string?> GetAuthorizationIdAsync(MagicLinkToken token, CancellationToken cancellationToken) => new(Require(token).AuthorizationId);

    public ValueTask<DateTimeOffset?> GetCreationDateAsync(MagicLinkToken token, CancellationToken cancellationToken) => new(Require(token).CreationDate);

    public ValueTask<DateTimeOffset?> GetExpirationDateAsync(MagicLinkToken token, CancellationToken cancellationToken) => new(Require(token).ExpirationDate);

    public ValueTask<string?> GetIdAsync(MagicLinkToken token, CancellationToken cancellationToken) => new(Require(token).Id);

    public ValueTask<string?> GetPayloadAsync(MagicLinkToken token, CancellationToken cancellationToken) => new(Require(token).Payload);

    public ValueTask<ImmutableDictionary<string, JsonElement>> GetPropertiesAsync(MagicLinkToken token, CancellationToken cancellationToken) => new(Require(token).Properties);

    public ValueTask<DateTimeOffset?> GetRedemptionDateAsync(MagicLinkToken token, CancellationToken cancellationToken) => new(Require(token).RedemptionDate);

    public ValueTask<string?> GetReferenceIdAsync(MagicLinkToken token, CancellationToken cancellationToken) => new(Require(token).ReferenceId);

    public ValueTask<string?> GetStatusAsync(MagicLinkToken token, CancellationToken cancellationToken) => new(Require(token).Status);

    public ValueTask<string?> GetSubjectAsync(MagicLinkToken token, CancellationToken cancellationToken) => new(Require(token).Subject);

    public ValueTask<string?> GetTypeAsync(MagicLinkToken token, CancellationToken cancellationToken) => new(Require(token).Type);

    public ValueTask<MagicLinkToken> InstantiateAsync(CancellationToken cancellationToken) => new(new MagicLinkToken());

    public async IAsyncEnumerable<MagicLinkToken> ListAsync(int? count, int? offset, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var connection = database.Connect();
        await using (connection.ConfigureAwait(false))
        {
            await foreach (var row in new TokenRepository(connection).ListAsync(count, offset ?? 0, cancellationToken).ConfigureAwait(false))
            {
                yield return ToEntity(row)!;
            }
        }
    }

    public IAsyncEnumerable<TResult> ListAsync<TState, TResult>(Func<IQueryable<MagicLinkToken>, TState, IQueryable<TResult>> query, TState state, CancellationToken cancellationToken) =>
        throw QueryableNotSupported.For(nameof(MagicLinkTokenStore), nameof(ListAsync));

    public async ValueTask<long> PruneAsync(DateTimeOffset threshold, CancellationToken cancellationToken)
    {
        var connection = database.Connect();
        await using (connection.ConfigureAwait(false))
        {
            var repository = new TokenRepository(connection);
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
            return await new TokenRepository(connection).RevokeAsync(filter.Subject, filter.Client, filter.Status, filter.Type, cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask<long> RevokeByApplicationIdAsync(string identifier, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(identifier);
        if (!StoreIds.TryParse(identifier, out var id))
        {
            return 0;
        }

        var connection = database.Connect();
        await using (connection.ConfigureAwait(false))
        {
            return await new TokenRepository(connection).RevokeByApplicationIdAsync(id, cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask<long> RevokeByAuthorizationIdAsync(string identifier, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(identifier);
        if (!StoreIds.TryParse(identifier, out var id))
        {
            return 0;
        }

        var connection = database.Connect();
        await using (connection.ConfigureAwait(false))
        {
            return await new TokenRepository(connection).RevokeByAuthorizationIdAsync(id, cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask<long> RevokeBySubjectAsync(string subject, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(subject);

        var connection = database.Connect();
        await using (connection.ConfigureAwait(false))
        {
            return await new TokenRepository(connection).RevokeBySubjectAsync(subject, cancellationToken).ConfigureAwait(false);
        }
    }

    public ValueTask SetApplicationIdAsync(MagicLinkToken token, string? identifier, CancellationToken cancellationToken) => Set(token, t => t.ApplicationId = identifier);

    public ValueTask SetAuthorizationIdAsync(MagicLinkToken token, string? identifier, CancellationToken cancellationToken) => Set(token, t => t.AuthorizationId = identifier);

    public ValueTask SetCreationDateAsync(MagicLinkToken token, DateTimeOffset? date, CancellationToken cancellationToken) => Set(token, t => t.CreationDate = date);

    public ValueTask SetExpirationDateAsync(MagicLinkToken token, DateTimeOffset? date, CancellationToken cancellationToken) => Set(token, t => t.ExpirationDate = date);

    public ValueTask SetPayloadAsync(MagicLinkToken token, string? payload, CancellationToken cancellationToken) => Set(token, t => t.Payload = payload);

    public ValueTask SetPropertiesAsync(MagicLinkToken token, ImmutableDictionary<string, JsonElement> properties, CancellationToken cancellationToken) => Set(token, t => t.Properties = properties);

    public ValueTask SetRedemptionDateAsync(MagicLinkToken token, DateTimeOffset? date, CancellationToken cancellationToken) => Set(token, t => t.RedemptionDate = date);

    public ValueTask SetReferenceIdAsync(MagicLinkToken token, string? identifier, CancellationToken cancellationToken) => Set(token, t => t.ReferenceId = identifier);

    public ValueTask SetStatusAsync(MagicLinkToken token, string? status, CancellationToken cancellationToken) => Set(token, t => t.Status = status);

    public ValueTask SetSubjectAsync(MagicLinkToken token, string? subject, CancellationToken cancellationToken) => Set(token, t => t.Subject = subject);

    public ValueTask SetTypeAsync(MagicLinkToken token, string? type, CancellationToken cancellationToken) => Set(token, t => t.Type = type);

    public async ValueTask UpdateAsync(MagicLinkToken token, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        var id = RequireId(token);
        var concurrencyToken = StoreIds.NewConcurrencyToken();
        var row = ToRow(token, id, concurrencyToken);
        var connection = database.Connect();
        await using (connection.ConfigureAwait(false))
        {
            if (await new TokenRepository(connection).UpdateAsync(row, token.ConcurrencyToken ?? string.Empty, cancellationToken).ConfigureAwait(false) == 0)
            {
                throw new ConcurrencyException(ConcurrencyMessage);
            }
        }

        token.ConcurrencyToken = concurrencyToken;
    }

    private static MagicLinkToken Require(MagicLinkToken token)
    {
        ArgumentNullException.ThrowIfNull(token);
        return token;
    }

    private static ValueTask Set(MagicLinkToken token, Action<MagicLinkToken> set)
    {
        set(Require(token));
        return ValueTask.CompletedTask;
    }

    private static Guid RequireId(MagicLinkToken token) =>
        StoreIds.TryParse(token.Id, out var id) ? id : throw new ArgumentException("The token has not been created by this store.", nameof(token));

    private static TokenRow ToRow(MagicLinkToken token, Guid id, string concurrencyToken) => new(
        id,
        concurrencyToken,
        StoreIds.ParseReference(token.ApplicationId, nameof(token)),
        StoreIds.ParseReference(token.AuthorizationId, nameof(token)),
        token.CreationDate?.ToUniversalTime(),
        token.ExpirationDate?.ToUniversalTime(),
        token.RedemptionDate?.ToUniversalTime(),
        token.Payload,
        StoreJson.WriteProperties(token.Properties),
        token.ReferenceId,
        token.Status,
        token.Subject,
        token.Type);

    private static MagicLinkToken? ToEntity(TokenRow? row) => row is null ? null : new MagicLinkToken
    {
        Id = StoreIds.ToText(row.Id),
        ConcurrencyToken = row.ConcurrencyToken,
        ApplicationId = StoreIds.ToText(row.ApplicationId),
        AuthorizationId = StoreIds.ToText(row.AuthorizationId),
        CreationDate = row.CreationDate,
        ExpirationDate = row.ExpirationDate,
        RedemptionDate = row.RedemptionDate,
        Payload = row.Payload,
        Properties = StoreJson.ReadProperties(row.Properties),
        ReferenceId = row.ReferenceId,
        Status = row.Status,
        Subject = row.Subject,
        Type = row.Type,
    };
}
