using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using static OpenIddict.Abstractions.OpenIddictExceptions;

namespace MagicLink.PostgreSql.Tests;

public sealed class ApplicationStoreTests(StoreFixture fixture) : IClassFixture<StoreFixture>
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private MagicLinkApplicationStore Store => new(fixture.Store);

    private async Task<MagicLinkApplication> CreateAsync(string? clientId = null, params string[] redirectUris)
    {
        var application = new MagicLinkApplication
        {
            ClientId = clientId ?? "client-" + Guid.NewGuid().ToString("N"),
            ClientType = "public",
            RedirectUris = [.. redirectUris],
        };
        await Store.CreateAsync(application, Cancel);
        return application;
    }

    [Fact]
    public async Task Created_application_is_found_by_id_and_client_id_with_every_field()
    {
        var properties = ImmutableDictionary<string, JsonElement>.Empty.Add("tier", JsonSerializer.SerializeToElement(3));
        var keys = new JsonWebKeySet();
        keys.Keys.Add(new JsonWebKey { Kty = "oct", Kid = "k1" });
        var application = new MagicLinkApplication
        {
            ApplicationType = "web",
            ClientId = "every-field",
            ClientSecret = "hash",
            ClientType = "confidential",
            ConsentType = "explicit",
            DisplayName = "Every field",
            DisplayNames = ImmutableDictionary<CultureInfo, string>.Empty.Add(CultureInfo.InvariantCulture, "Alle velden"),
            JsonWebKeySet = keys,
            Permissions = ["ept:token", "gt:client_credentials"],
            PostLogoutRedirectUris = ["https://app.example/out"],
            RedirectUris = ["https://app.example/in"],
            Requirements = ["ft:pkce"],
            Settings = ImmutableDictionary<string, string>.Empty.Add("tkn_lft:act", "00:10:00"),
            Properties = properties,
        };

        await Store.CreateAsync(application, Cancel);
        var byId = await Store.FindByIdAsync(application.Id!, Cancel);
        var byClientId = await Store.FindByClientIdAsync("every-field", Cancel);

        Assert.NotNull(byId);
        Assert.Equal(application.Id, byClientId?.Id);
        Assert.Equal("web", byId.ApplicationType);
        Assert.Equal("hash", byId.ClientSecret);
        Assert.Equal("confidential", byId.ClientType);
        Assert.Equal("explicit", byId.ConsentType);
        Assert.Equal("Every field", byId.DisplayName);
        Assert.Equal("Alle velden", byId.DisplayNames[CultureInfo.InvariantCulture]);
        Assert.Equal("k1", Assert.Single(byId.JsonWebKeySet!.Keys).Kid);
        Assert.Equal(["ept:token", "gt:client_credentials"], byId.Permissions);
        Assert.Equal(["https://app.example/out"], byId.PostLogoutRedirectUris);
        Assert.Equal(["https://app.example/in"], byId.RedirectUris);
        Assert.Equal(["ft:pkce"], byId.Requirements);
        Assert.Equal("00:10:00", byId.Settings["tkn_lft:act"]);
        Assert.Equal(3, byId.Properties["tier"].GetInt32());
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-guid")]
    public async Task Find_by_an_id_that_is_no_uuid_returns_null(string id)
    {
        Assert.Null(await Store.FindByIdAsync(id, Cancel));
    }

    [Fact]
    public async Task Unknown_client_id_returns_null()
    {
        Assert.Null(await Store.FindByClientIdAsync("nobody-" + Guid.NewGuid(), Cancel));
    }

    [Fact]
    public async Task Update_with_the_current_token_persists_and_sets_a_new_token()
    {
        var application = await CreateAsync();
        var before = application.ConcurrencyToken;

        application.DisplayName = "Renamed";
        await Store.UpdateAsync(application, Cancel);

        Assert.NotEqual(before, application.ConcurrencyToken, StringComparer.Ordinal);
        Assert.Equal("Renamed", (await Store.FindByIdAsync(application.Id!, Cancel))!.DisplayName);
    }

    [Fact]
    public async Task Update_with_a_stale_token_throws_a_concurrency_exception()
    {
        var application = await CreateAsync();
        var stale = await Store.FindByIdAsync(application.Id!, Cancel);
        application.DisplayName = "First";
        await Store.UpdateAsync(application, Cancel);

        stale!.DisplayName = "Second";

        await Assert.ThrowsAsync<ConcurrencyException>(() => Store.UpdateAsync(stale, Cancel).AsTask());
        Assert.Equal("First", (await Store.FindByIdAsync(application.Id!, Cancel))!.DisplayName);
    }

    [Fact]
    public async Task Delete_removes_the_application_and_a_stale_delete_throws()
    {
        var application = await CreateAsync();
        var stale = await Store.FindByIdAsync(application.Id!, Cancel);
        await Store.UpdateAsync(application, Cancel);

        await Assert.ThrowsAsync<ConcurrencyException>(() => Store.DeleteAsync(stale!, Cancel).AsTask());
        await Store.DeleteAsync(application, Cancel);

        Assert.Null(await Store.FindByIdAsync(application.Id!, Cancel));
    }

    [Fact]
    public async Task Find_by_redirect_uri_returns_exact_matches_only()
    {
        var uri = $"https://{Guid.NewGuid():N}.example/cb";
        var match = await CreateAsync(null, uri, "https://other.example/cb");
        await CreateAsync(null, uri + "/deeper");
        await CreateAsync(null, uri.ToUpperInvariant());

        var found = await Store.FindByRedirectUriAsync(uri, Cancel).ToListAsync(Cancel);

        Assert.Equal(match.Id, Assert.Single(found).Id);
    }

    [Fact]
    public async Task Find_by_post_logout_redirect_uri_returns_exact_matches_only()
    {
        var uri = $"https://{Guid.NewGuid():N}.example/out";
        var application = new MagicLinkApplication { ClientId = "out-" + Guid.NewGuid(), PostLogoutRedirectUris = [uri] };
        await Store.CreateAsync(application, Cancel);
        await Store.CreateAsync(new MagicLinkApplication { ClientId = "out-" + Guid.NewGuid(), PostLogoutRedirectUris = [uri + "x"] }, Cancel);

        var found = await Store.FindByPostLogoutRedirectUriAsync(uri, Cancel).ToListAsync(Cancel);

        Assert.Equal(application.Id, Assert.Single(found).Id);
    }

    [Fact]
    public async Task Count_and_list_with_count_and_offset_page_through_every_application()
    {
        await CreateAsync();
        await CreateAsync();
        await CreateAsync();
        var total = await Store.CountAsync(Cancel);

        var all = await Store.ListAsync(count: null, offset: null, Cancel).ToListAsync(Cancel);
        var first = await Store.ListAsync(count: 2, offset: 0, Cancel).ToListAsync(Cancel);
        var rest = await Store.ListAsync(count: null, offset: 2, Cancel).ToListAsync(Cancel);

        Assert.Equal(total, all.Count);
        Assert.Equal(2, first.Count);
        Assert.Equal(all.Select(a => a.Id), first.Concat(rest).Select(a => a.Id), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Queryable_overloads_throw_not_supported_naming_the_method()
    {
        var count = await Assert.ThrowsAsync<NotSupportedException>(() => Store.CountAsync<int>(q => q.Select(_ => 1), Cancel).AsTask());
        var get = await Assert.ThrowsAsync<NotSupportedException>(() => Store.GetAsync<int, int>((q, _) => q.Select(_ => 1), 0, Cancel).AsTask());
        var list = Assert.Throws<NotSupportedException>(() => Store.ListAsync<int, int>((q, _) => q.Select(_ => 1), 0, Cancel));

        Assert.Contains(nameof(Store.CountAsync), count.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(Store.GetAsync), get.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(Store.ListAsync), list.Message, StringComparison.Ordinal);
    }
}
