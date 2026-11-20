using System.Collections.Immutable;
using OpenIddict.Abstractions;

namespace MagicLink.AotProof.Tests;

public sealed class InMemoryApplicationStoreTests
{
    private static readonly CancellationToken None = CancellationToken.None;

    private static async Task<(InMemoryApplicationStore Store, ProofApplication Application)> StoreWithOneClient()
    {
        var store = new InMemoryApplicationStore();
        var application = await store.InstantiateAsync(None);
        await store.SetClientIdAsync(application, "proof-cli", None);
        await store.SetClientTypeAsync(application, OpenIddictConstants.ClientTypes.Public, None);
        await store.SetRedirectUrisAsync(application, ["http://localhost/callback"], None);
        await store.SetPermissionsAsync(application, [OpenIddictConstants.Permissions.Endpoints.Token], None);
        await store.CreateAsync(application, None);
        return (store, application);
    }

    [Fact]
    public async Task Finds_a_client_by_its_client_id()
    {
        var (store, application) = await StoreWithOneClient();

        Assert.Same(application, await store.FindByClientIdAsync("proof-cli", None));
        Assert.Same(application, await store.FindByIdAsync(application.Id, None));
    }

    [Theory]
    [InlineData("")]
    [InlineData("PROOF-CLI")]
    [InlineData("unknown")]
    public async Task Finds_nothing_for_an_unknown_or_empty_client_id(string clientId)
    {
        var (store, _) = await StoreWithOneClient();

        Assert.Null(await store.FindByClientIdAsync(clientId, None));
        Assert.Null(await store.FindByIdAsync(clientId, None));
    }

    [Fact]
    public async Task Reads_back_what_was_set()
    {
        var (store, application) = await StoreWithOneClient();

        Assert.Equal(OpenIddictConstants.ClientTypes.Public, await store.GetClientTypeAsync(application, None));
        Assert.Equal(["http://localhost/callback"], await store.GetRedirectUrisAsync(application, None));
        Assert.Equal([OpenIddictConstants.Permissions.Endpoints.Token], await store.GetPermissionsAsync(application, None));
        Assert.Equal(application.Id, await store.GetIdAsync(application, None));
        Assert.Equal(1, await store.CountAsync(None));
    }

    [Fact]
    public async Task Finds_clients_by_redirect_uri_and_lists_them()
    {
        var (store, application) = await StoreWithOneClient();

        Assert.Equal([application], await ToList(store.FindByRedirectUriAsync("http://localhost/callback", None)));
        Assert.Empty(await ToList(store.FindByRedirectUriAsync("http://localhost/other", None)));
        Assert.Equal([application], await ToList(store.ListAsync(count: null, offset: null, None)));
        Assert.Empty(await ToList(store.ListAsync(count: 1, offset: 1, None)));
    }

    [Fact]
    public async Task Rejects_a_second_application_with_the_same_id_and_forgets_a_deleted_one()
    {
        var (store, application) = await StoreWithOneClient();

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await store.CreateAsync(application, None));
        await store.DeleteAsync(application, None);
        Assert.Null(await store.FindByClientIdAsync("proof-cli", None));
    }

    private static async Task<List<ProofApplication>> ToList(IAsyncEnumerable<ProofApplication> source)
    {
        var list = new List<ProofApplication>();
        await foreach (var item in source)
        {
            list.Add(item);
        }

        return list;
    }
}
