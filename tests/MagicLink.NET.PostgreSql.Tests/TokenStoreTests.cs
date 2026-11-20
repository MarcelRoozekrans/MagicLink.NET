using System.Collections.Immutable;
using System.Text.Json;
using static OpenIddict.Abstractions.OpenIddictExceptions;

namespace MagicLink.PostgreSql.Tests;

public sealed class TokenStoreTests(StoreFixture fixture) : IClassFixture<StoreFixture>
{
    private static readonly DateTimeOffset Old = DateTimeOffset.UtcNow.AddDays(-30);
    private static readonly DateTimeOffset Threshold = DateTimeOffset.UtcNow.AddDays(-14);

    private readonly StoreData _data = new(fixture);

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private MagicLinkTokenStore Store => _data.Tokens;

    [Fact]
    public async Task Created_token_is_found_by_every_key_with_every_field()
    {
        var application = await _data.ApplicationAsync();
        var authorization = await _data.AuthorizationAsync(application);
        var now = new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);
        var token = new MagicLinkToken
        {
            ApplicationId = application.Id,
            AuthorizationId = authorization.Id,
            CreationDate = now,
            ExpirationDate = now.AddHours(1),
            RedemptionDate = now.AddMinutes(5),
            Payload = "payload",
            ReferenceId = "ref-" + Guid.NewGuid().ToString("N"),
            Status = "redeemed",
            Subject = StoreData.NewSubject(),
            Type = "device_code",
            Properties = ImmutableDictionary<string, JsonElement>.Empty.Add("p", JsonSerializer.SerializeToElement(1)),
        };

        await Store.CreateAsync(token, Cancel);
        var byId = await Store.FindByIdAsync(token.Id!, Cancel);

        Assert.NotNull(byId);
        Assert.Equal(application.Id, byId.ApplicationId);
        Assert.Equal(authorization.Id, byId.AuthorizationId);
        Assert.Equal(now, byId.CreationDate);
        Assert.Equal(now.AddHours(1), byId.ExpirationDate);
        Assert.Equal(now.AddMinutes(5), byId.RedemptionDate);
        Assert.Equal("payload", byId.Payload);
        Assert.Equal("redeemed", byId.Status);
        Assert.Equal("device_code", byId.Type);
        Assert.Equal(1, byId.Properties["p"].GetInt32());
        Assert.Equal(token.Id, (await Store.FindByReferenceIdAsync(token.ReferenceId, Cancel))?.Id);
        Assert.Equal(token.Id, Assert.Single(await Store.FindByApplicationIdAsync(application.Id!, Cancel).ToListAsync(Cancel)).Id);
        Assert.Equal(token.Id, Assert.Single(await Store.FindByAuthorizationIdAsync(authorization.Id!, Cancel).ToListAsync(Cancel)).Id);
        Assert.Equal(token.Id, Assert.Single(await Store.FindBySubjectAsync(token.Subject, Cancel).ToListAsync(Cancel)).Id);
    }

    [Fact]
    public async Task Unknown_reference_id_and_ids_that_are_no_uuid_find_nothing()
    {
        Assert.Null(await Store.FindByReferenceIdAsync("ref-unknown-" + Guid.NewGuid(), Cancel));
        Assert.Null(await Store.FindByIdAsync("not-a-guid", Cancel));
        Assert.Empty(await Store.FindByAuthorizationIdAsync("not-a-guid", Cancel).ToListAsync(Cancel));
        Assert.Empty(await Store.FindByApplicationIdAsync("not-a-guid", Cancel).ToListAsync(Cancel));
    }

    [Fact]
    public async Task Find_applies_each_filter_that_is_given()
    {
        var application = await _data.ApplicationAsync();
        var subject = StoreData.NewSubject();
        var match = await _data.TokenAsync(application, subject: subject);
        await _data.TokenAsync(subject: subject);
        await _data.TokenAsync(application, subject: subject, status: "redeemed");
        await _data.TokenAsync(application, subject: subject, type: "authorization_code");

        Assert.Equal(match.Id, Assert.Single(await Store.FindAsync(subject, application.Id, "valid", "refresh_token", Cancel).ToListAsync(Cancel)).Id);
        Assert.Equal(4, (await Store.FindAsync(subject, null, null, null, Cancel).ToListAsync(Cancel)).Count);
        Assert.Equal(4, (await Store.FindAsync(subject, "", "", "", Cancel).ToListAsync(Cancel)).Count);
    }

    [Fact]
    public async Task Redeeming_with_a_stale_token_throws_a_concurrency_exception_so_a_code_is_used_once()
    {
        var token = await _data.TokenAsync(type: "authorization_code");
        var first = await Store.FindByIdAsync(token.Id!, Cancel);
        var second = await Store.FindByIdAsync(token.Id!, Cancel);

        first!.Status = "redeemed";
        await Store.UpdateAsync(first, Cancel);
        second!.Status = "redeemed";

        await Assert.ThrowsAsync<ConcurrencyException>(() => Store.UpdateAsync(second, Cancel).AsTask());
        await Assert.ThrowsAsync<ConcurrencyException>(() => Store.DeleteAsync(second, Cancel).AsTask());
    }

    [Fact]
    public async Task Prune_removes_old_tokens_that_are_not_valid_or_expired_or_whose_authorization_is_not_valid()
    {
        var revokedAuthorization = await _data.AuthorizationAsync(status: "revoked");
        var validAuthorization = await _data.AuthorizationAsync();
        var future = DateTimeOffset.UtcNow.AddDays(1);

        var oldRedeemed = await _data.TokenAsync(status: "redeemed", created: Old, expires: future);
        var oldExpired = await _data.TokenAsync(created: Old, expires: DateTimeOffset.UtcNow.AddMinutes(-1));
        var oldUnderRevokedAuthorization = await _data.TokenAsync(authorization: revokedAuthorization, created: Old, expires: future);
        var oldValid = await _data.TokenAsync(authorization: validAuthorization, created: Old, expires: future);
        var oldInactiveWithoutExpiry = await _data.TokenAsync(status: "inactive", created: Old);
        var recentRedeemed = await _data.TokenAsync(status: "redeemed");

        var pruned = await Store.PruneAsync(Threshold, Cancel);

        Assert.True(pruned >= 3);
        Assert.Null(await Store.FindByIdAsync(oldRedeemed.Id!, Cancel));
        Assert.Null(await Store.FindByIdAsync(oldExpired.Id!, Cancel));
        Assert.Null(await Store.FindByIdAsync(oldUnderRevokedAuthorization.Id!, Cancel));
        Assert.NotNull(await Store.FindByIdAsync(oldValid.Id!, Cancel));
        Assert.NotNull(await Store.FindByIdAsync(oldInactiveWithoutExpiry.Id!, Cancel));
        Assert.NotNull(await Store.FindByIdAsync(recentRedeemed.Id!, Cancel));
    }

    [Fact]
    public async Task Revoke_by_authorization_application_subject_and_filter_revoke_the_matching_tokens()
    {
        var application = await _data.ApplicationAsync();
        var authorization = await _data.AuthorizationAsync();
        var subject = StoreData.NewSubject();
        var byAuthorization = await _data.TokenAsync(authorization: authorization);
        var byApplication = await _data.TokenAsync(application);
        var bySubject = await _data.TokenAsync(subject: subject);
        var byFilter = await _data.TokenAsync(type: "access_token");
        var untouched = await _data.TokenAsync();

        Assert.Equal(1, await Store.RevokeByAuthorizationIdAsync(authorization.Id!, Cancel));
        Assert.Equal(1, await Store.RevokeByApplicationIdAsync(application.Id!, Cancel));
        Assert.Equal(1, await Store.RevokeBySubjectAsync(subject, Cancel));
        Assert.Equal(1, await Store.RevokeAsync(byFilter.Subject, null, "valid", "access_token", Cancel));
        Assert.Equal(0, await Store.RevokeByAuthorizationIdAsync(authorization.Id!, Cancel));

        foreach (var revoked in new[] { byAuthorization, byApplication, bySubject, byFilter })
        {
            Assert.Equal("revoked", (await Store.FindByIdAsync(revoked.Id!, Cancel))!.Status);
        }

        Assert.Equal("valid", (await Store.FindByIdAsync(untouched.Id!, Cancel))!.Status);
    }

    [Fact]
    public async Task Count_list_and_queryable_overloads()
    {
        await _data.TokenAsync();
        await _data.TokenAsync();

        var all = await Store.ListAsync(null, null, Cancel).ToListAsync(Cancel);

        Assert.Equal(await Store.CountAsync(Cancel), all.Count);
        Assert.Equal(all[1].Id, Assert.Single(await Store.ListAsync(1, 1, Cancel).ToListAsync(Cancel)).Id);
        await Assert.ThrowsAsync<NotSupportedException>(() => Store.CountAsync<int>(q => q.Select(_ => 1), Cancel).AsTask());
        await Assert.ThrowsAsync<NotSupportedException>(() => Store.GetAsync<int, int>((q, _) => q.Select(_ => 1), 0, Cancel).AsTask());
        Assert.Throws<NotSupportedException>(() => Store.ListAsync<int, int>((q, _) => q.Select(_ => 1), 0, Cancel));
    }
}
