using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using static OpenIddict.Abstractions.OpenIddictExceptions;

namespace MagicLink.PostgreSql.Tests;

public sealed class ScopeStoreTests(StoreFixture fixture) : IClassFixture<StoreFixture>
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private MagicLinkScopeStore Store => new(fixture.Store);

    private async Task<MagicLinkScope> CreateAsync(string? name = null, params string[] resources)
    {
        var scope = new MagicLinkScope { Name = name ?? "scope-" + Guid.NewGuid().ToString("N"), Resources = [.. resources] };
        await Store.CreateAsync(scope, Cancel);
        return scope;
    }

    [Fact]
    public async Task Created_scope_is_found_by_id_and_name_with_every_field()
    {
        var scope = new MagicLinkScope
        {
            Name = "every-field",
            Description = "Reads things",
            Descriptions = ImmutableDictionary<CultureInfo, string>.Empty.Add(CultureInfo.InvariantCulture, "Leest dingen"),
            DisplayName = "Read",
            DisplayNames = ImmutableDictionary<CultureInfo, string>.Empty.Add(CultureInfo.InvariantCulture, "Lezen"),
            Resources = ["api-1", "api-2"],
            Properties = ImmutableDictionary<string, JsonElement>.Empty.Add("p", JsonSerializer.SerializeToElement("v")),
        };

        await Store.CreateAsync(scope, Cancel);
        var byId = await Store.FindByIdAsync(scope.Id!, Cancel);
        var byName = await Store.FindByNameAsync("every-field", Cancel);

        Assert.NotNull(byId);
        Assert.Equal(scope.Id, byName?.Id);
        Assert.Equal("Reads things", byId.Description);
        Assert.Equal("Leest dingen", byId.Descriptions[CultureInfo.InvariantCulture]);
        Assert.Equal("Read", byId.DisplayName);
        Assert.Equal("Lezen", byId.DisplayNames[CultureInfo.InvariantCulture]);
        Assert.Equal(["api-1", "api-2"], byId.Resources);
        Assert.Equal("v", byId.Properties["p"].GetString());
    }

    [Fact]
    public async Task Find_by_names_returns_the_named_scopes_only()
    {
        var a = await CreateAsync();
        var b = await CreateAsync();
        await CreateAsync();

        var found = await Store.FindByNamesAsync([a.Name!, b.Name!, "missing"], Cancel).ToListAsync(Cancel);

        Assert.Equal(new[] { a.Id, b.Id }.Order(StringComparer.Ordinal), found.Select(s => s.Id).Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Find_by_resource_returns_exact_matches_only()
    {
        var resource = "api-" + Guid.NewGuid().ToString("N");
        var match = await CreateAsync(null, resource);
        await CreateAsync(null, resource + "-other");

        var found = await Store.FindByResourceAsync(resource, Cancel).ToListAsync(Cancel);

        Assert.Equal(match.Id, Assert.Single(found).Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-guid")]
    public async Task Find_by_an_id_that_is_no_uuid_returns_null(string id)
    {
        Assert.Null(await Store.FindByIdAsync(id, Cancel));
    }

    [Fact]
    public async Task Update_and_delete_with_a_stale_token_throw_a_concurrency_exception()
    {
        var scope = await CreateAsync();
        var stale = await Store.FindByIdAsync(scope.Id!, Cancel);
        scope.DisplayName = "Current";
        await Store.UpdateAsync(scope, Cancel);

        await Assert.ThrowsAsync<ConcurrencyException>(() => Store.UpdateAsync(stale!, Cancel).AsTask());
        await Assert.ThrowsAsync<ConcurrencyException>(() => Store.DeleteAsync(stale!, Cancel).AsTask());

        Assert.Equal("Current", (await Store.FindByIdAsync(scope.Id!, Cancel))!.DisplayName);
        await Store.DeleteAsync(scope, Cancel);
        Assert.Null(await Store.FindByIdAsync(scope.Id!, Cancel));
    }

    [Fact]
    public async Task Count_and_list_page_through_every_scope()
    {
        await CreateAsync();
        await CreateAsync();

        var total = await Store.CountAsync(Cancel);
        var all = await Store.ListAsync(null, null, Cancel).ToListAsync(Cancel);
        var page = await Store.ListAsync(1, 1, Cancel).ToListAsync(Cancel);

        Assert.Equal(total, all.Count);
        Assert.Equal(all[1].Id, Assert.Single(page).Id);
    }

    [Fact]
    public async Task Queryable_overloads_throw_not_supported()
    {
        await Assert.ThrowsAsync<NotSupportedException>(() => Store.CountAsync<int>(q => q.Select(_ => 1), Cancel).AsTask());
        await Assert.ThrowsAsync<NotSupportedException>(() => Store.GetAsync<int, int>((q, _) => q.Select(_ => 1), 0, Cancel).AsTask());
        Assert.Throws<NotSupportedException>(() => Store.ListAsync<int, int>((q, _) => q.Select(_ => 1), 0, Cancel));
    }
}
