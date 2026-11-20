using System.Collections.Immutable;
using System.Text.Json;
using static OpenIddict.Abstractions.OpenIddictExceptions;

namespace MagicLink.PostgreSql.Tests;

public sealed class AuthorizationStoreTests(StoreFixture fixture) : IClassFixture<StoreFixture>
{
    private static readonly DateTimeOffset Old = DateTimeOffset.UtcNow.AddDays(-30);
    private static readonly DateTimeOffset Threshold = DateTimeOffset.UtcNow.AddDays(-14);

    private readonly StoreData _data = new(fixture);

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private MagicLinkAuthorizationStore Store => _data.Authorizations;

    [Fact]
    public async Task Created_authorization_is_found_by_id_application_and_subject_with_every_field()
    {
        var application = await _data.ApplicationAsync();
        var created = new DateTimeOffset(2026, 11, 20, 10, 0, 0, TimeSpan.FromHours(1));
        var authorization = new MagicLinkAuthorization
        {
            ApplicationId = application.Id,
            CreationDate = created,
            Scopes = ["openid", "offline_access"],
            Status = "valid",
            Subject = StoreData.NewSubject(),
            Type = "permanent",
            Properties = ImmutableDictionary<string, JsonElement>.Empty.Add("p", JsonSerializer.SerializeToElement(true)),
        };

        await Store.CreateAsync(authorization, Cancel);
        var byId = await Store.FindByIdAsync(authorization.Id!, Cancel);

        Assert.NotNull(byId);
        Assert.Equal(application.Id, byId.ApplicationId);
        Assert.Equal(created, byId.CreationDate);
        Assert.Equal(["openid", "offline_access"], byId.Scopes);
        Assert.Equal("valid", byId.Status);
        Assert.Equal(authorization.Subject, byId.Subject);
        Assert.Equal("permanent", byId.Type);
        Assert.True(byId.Properties["p"].GetBoolean());
        Assert.Equal(authorization.Id, Assert.Single(await Store.FindByApplicationIdAsync(application.Id!, Cancel).ToListAsync(Cancel)).Id);
        Assert.Equal(authorization.Id, Assert.Single(await Store.FindBySubjectAsync(authorization.Subject!, Cancel).ToListAsync(Cancel)).Id);
    }

    [Fact]
    public async Task Find_applies_each_filter_that_is_given_and_requires_every_scope_asked_for()
    {
        var application = await _data.ApplicationAsync();
        var other = await _data.ApplicationAsync();
        var subject = StoreData.NewSubject();
        var match = await _data.AuthorizationAsync(application, subject, scopes: ["openid", "email"]);
        await _data.AuthorizationAsync(other, subject);
        await _data.AuthorizationAsync(application, subject, status: "revoked");
        await _data.AuthorizationAsync(application, subject, type: "ad-hoc");

        var exact = await Store.FindAsync(subject, application.Id, "valid", "permanent", null, Cancel).ToListAsync(Cancel);
        var bySubject = await Store.FindAsync(subject, null, null, null, null, Cancel).ToListAsync(Cancel);
        var emptyIsNoFilter = await Store.FindAsync(subject, "", "", "", null, Cancel).ToListAsync(Cancel);
        var withScopes = await Store.FindAsync(subject, null, null, null, ["email"], Cancel).ToListAsync(Cancel);
        var missingScope = await Store.FindAsync(subject, null, null, null, ["email", "profile"], Cancel).ToListAsync(Cancel);
        var garbageClient = await Store.FindAsync(subject, "not-a-guid", null, null, null, Cancel).ToListAsync(Cancel);

        Assert.Equal(match.Id, Assert.Single(exact).Id);
        Assert.Equal(4, bySubject.Count);
        Assert.Equal(4, emptyIsNoFilter.Count);
        Assert.Equal(match.Id, Assert.Single(withScopes).Id);
        Assert.Empty(missingScope);
        Assert.Empty(garbageClient);
    }

    [Fact]
    public async Task Update_and_delete_with_a_stale_token_throw_a_concurrency_exception()
    {
        var authorization = await _data.AuthorizationAsync();
        var stale = await Store.FindByIdAsync(authorization.Id!, Cancel);
        authorization.Status = "revoked";
        await Store.UpdateAsync(authorization, Cancel);

        await Assert.ThrowsAsync<ConcurrencyException>(() => Store.UpdateAsync(stale!, Cancel).AsTask());
        await Assert.ThrowsAsync<ConcurrencyException>(() => Store.DeleteAsync(stale!, Cancel).AsTask());
        Assert.Equal("revoked", (await Store.FindByIdAsync(authorization.Id!, Cancel))!.Status);
    }

    [Fact]
    public async Task Deleting_an_authorization_deletes_its_tokens()
    {
        var authorization = await _data.AuthorizationAsync();
        var token = await _data.TokenAsync(authorization: authorization);

        await Store.DeleteAsync(authorization, Cancel);

        Assert.Null(await Store.FindByIdAsync(authorization.Id!, Cancel));
        Assert.Null(await _data.Tokens.FindByIdAsync(token.Id!, Cancel));
    }

    [Fact]
    public async Task Deleting_an_application_deletes_its_authorizations_and_tokens()
    {
        var application = await _data.ApplicationAsync();
        var authorization = await _data.AuthorizationAsync(application);
        var token = await _data.TokenAsync(application, authorization);

        await _data.Applications.DeleteAsync(application, Cancel);

        Assert.Null(await Store.FindByIdAsync(authorization.Id!, Cancel));
        Assert.Null(await _data.Tokens.FindByIdAsync(token.Id!, Cancel));
    }

    [Fact]
    public async Task Setting_an_application_id_that_is_no_uuid_fails_on_save()
    {
        var authorization = new MagicLinkAuthorization { ApplicationId = "not-a-guid", Status = "valid" };

        await Assert.ThrowsAsync<ArgumentException>(() => Store.CreateAsync(authorization, Cancel).AsTask());
    }

    [Fact]
    public async Task Prune_removes_old_authorizations_that_are_not_valid_or_ad_hoc_without_tokens()
    {
        var subject = StoreData.NewSubject();
        var oldRevoked = await _data.AuthorizationAsync(subject: subject, status: "revoked", created: Old);
        var oldAdHoc = await _data.AuthorizationAsync(subject: subject, type: "ad-hoc", created: Old);
        var oldValidPermanent = await _data.AuthorizationAsync(subject: subject, created: Old);
        var oldAdHocWithToken = await _data.AuthorizationAsync(subject: subject, type: "ad-hoc", created: Old);
        await _data.TokenAsync(authorization: oldAdHocWithToken);
        var recentRevoked = await _data.AuthorizationAsync(subject: subject, status: "revoked");

        var pruned = await Store.PruneAsync(Threshold, Cancel);

        Assert.True(pruned >= 2);
        Assert.Null(await Store.FindByIdAsync(oldRevoked.Id!, Cancel));
        Assert.Null(await Store.FindByIdAsync(oldAdHoc.Id!, Cancel));
        Assert.NotNull(await Store.FindByIdAsync(oldValidPermanent.Id!, Cancel));
        Assert.NotNull(await Store.FindByIdAsync(oldAdHocWithToken.Id!, Cancel));
        Assert.NotNull(await Store.FindByIdAsync(recentRevoked.Id!, Cancel));
    }

    [Fact]
    public async Task Revoke_by_subject_application_and_filter_revoke_the_matching_authorizations()
    {
        var application = await _data.ApplicationAsync();
        var subject = StoreData.NewSubject();
        var bySubject = await _data.AuthorizationAsync(subject: subject);
        var byApplication = await _data.AuthorizationAsync(application);
        var byFilter = await _data.AuthorizationAsync(subject: StoreData.NewSubject(), type: "ad-hoc");
        var untouched = await _data.AuthorizationAsync();

        Assert.Equal(1, await Store.RevokeBySubjectAsync(subject, Cancel));
        Assert.Equal(1, await Store.RevokeByApplicationIdAsync(application.Id!, Cancel));
        Assert.Equal(1, await Store.RevokeAsync(byFilter.Subject, null, "valid", "ad-hoc", Cancel));
        Assert.Equal(0, await Store.RevokeBySubjectAsync(subject, Cancel));

        foreach (var revoked in new[] { bySubject, byApplication, byFilter })
        {
            Assert.Equal("revoked", (await Store.FindByIdAsync(revoked.Id!, Cancel))!.Status);
        }

        var stillValid = await Store.FindByIdAsync(untouched.Id!, Cancel);
        Assert.Equal("valid", stillValid!.Status);
    }

    [Fact]
    public async Task Revoking_sets_a_new_concurrency_token()
    {
        var authorization = await _data.AuthorizationAsync();

        await Store.RevokeBySubjectAsync(authorization.Subject!, Cancel);
        authorization.Type = "ad-hoc";

        await Assert.ThrowsAsync<ConcurrencyException>(() => Store.UpdateAsync(authorization, Cancel).AsTask());
    }

    [Fact]
    public async Task Count_list_and_queryable_overloads()
    {
        await _data.AuthorizationAsync();
        await _data.AuthorizationAsync();

        var all = await Store.ListAsync(null, null, Cancel).ToListAsync(Cancel);

        Assert.Equal(await Store.CountAsync(Cancel), all.Count);
        Assert.Equal(all[1].Id, Assert.Single(await Store.ListAsync(1, 1, Cancel).ToListAsync(Cancel)).Id);
        await Assert.ThrowsAsync<NotSupportedException>(() => Store.CountAsync<int>(q => q.Select(_ => 1), Cancel).AsTask());
        await Assert.ThrowsAsync<NotSupportedException>(() => Store.GetAsync<int, int>((q, _) => q.Select(_ => 1), 0, Cancel).AsTask());
        Assert.Throws<NotSupportedException>(() => Store.ListAsync<int, int>((q, _) => q.Select(_ => 1), 0, Cancel));
    }
}
