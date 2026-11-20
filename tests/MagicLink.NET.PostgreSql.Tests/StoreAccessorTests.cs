using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace MagicLink.PostgreSql.Tests;

/// <summary>The getters, setters and guards that need no database: a data source that is never opened is enough.</summary>
public sealed class StoreAccessorTests : IDisposable
{
    private static readonly ImmutableDictionary<string, JsonElement> Properties =
        ImmutableDictionary<string, JsonElement>.Empty.Add("p", JsonSerializer.SerializeToElement(1));

    private static readonly ImmutableDictionary<CultureInfo, string> Localized =
        ImmutableDictionary<CultureInfo, string>.Empty.Add(CultureInfo.InvariantCulture, "text");

    private readonly StoreDatabase _database = new(StoreDataSource.Create("Host=localhost;Database=unused"));

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Application_store_sets_and_gets_every_field()
    {
        var store = new MagicLinkApplicationStore(_database);
        var application = await store.InstantiateAsync(Cancel);
        var keys = new JsonWebKeySet();

        await store.SetApplicationTypeAsync(application, "web", Cancel);
        await store.SetClientIdAsync(application, "id", Cancel);
        await store.SetClientSecretAsync(application, "secret", Cancel);
        await store.SetClientTypeAsync(application, "public", Cancel);
        await store.SetConsentTypeAsync(application, "implicit", Cancel);
        await store.SetDisplayNameAsync(application, "name", Cancel);
        await store.SetDisplayNamesAsync(application, Localized, Cancel);
        await store.SetJsonWebKeySetAsync(application, keys, Cancel);
        await store.SetPermissionsAsync(application, ["permission"], Cancel);
        await store.SetPostLogoutRedirectUrisAsync(application, ["https://out"], Cancel);
        await store.SetPropertiesAsync(application, Properties, Cancel);
        await store.SetRedirectUrisAsync(application, ["https://in"], Cancel);
        await store.SetRequirementsAsync(application, ["requirement"], Cancel);
        await store.SetSettingsAsync(application, ImmutableDictionary<string, string>.Empty.Add("s", "v"), Cancel);

        Assert.Null(await store.GetIdAsync(application, Cancel));
        Assert.Equal("web", await store.GetApplicationTypeAsync(application, Cancel));
        Assert.Equal("id", await store.GetClientIdAsync(application, Cancel));
        Assert.Equal("secret", await store.GetClientSecretAsync(application, Cancel));
        Assert.Equal("public", await store.GetClientTypeAsync(application, Cancel));
        Assert.Equal("implicit", await store.GetConsentTypeAsync(application, Cancel));
        Assert.Equal("name", await store.GetDisplayNameAsync(application, Cancel));
        Assert.Same(Localized, await store.GetDisplayNamesAsync(application, Cancel));
        Assert.Same(keys, await store.GetJsonWebKeySetAsync(application, Cancel));
        Assert.Equal(["permission"], await store.GetPermissionsAsync(application, Cancel));
        Assert.Equal(["https://out"], await store.GetPostLogoutRedirectUrisAsync(application, Cancel));
        Assert.Same(Properties, await store.GetPropertiesAsync(application, Cancel));
        Assert.Equal(["https://in"], await store.GetRedirectUrisAsync(application, Cancel));
        Assert.Equal(["requirement"], await store.GetRequirementsAsync(application, Cancel));
        Assert.Equal("v", (await store.GetSettingsAsync(application, Cancel))["s"]);
    }

    [Fact]
    public async Task Scope_store_sets_and_gets_every_field()
    {
        var store = new MagicLinkScopeStore(_database);
        var scope = await store.InstantiateAsync(Cancel);

        await store.SetNameAsync(scope, "name", Cancel);
        await store.SetDescriptionAsync(scope, "description", Cancel);
        await store.SetDescriptionsAsync(scope, Localized, Cancel);
        await store.SetDisplayNameAsync(scope, "display", Cancel);
        await store.SetDisplayNamesAsync(scope, Localized, Cancel);
        await store.SetResourcesAsync(scope, ["api"], Cancel);
        await store.SetPropertiesAsync(scope, Properties, Cancel);

        Assert.Null(await store.GetIdAsync(scope, Cancel));
        Assert.Equal("name", await store.GetNameAsync(scope, Cancel));
        Assert.Equal("description", await store.GetDescriptionAsync(scope, Cancel));
        Assert.Same(Localized, await store.GetDescriptionsAsync(scope, Cancel));
        Assert.Equal("display", await store.GetDisplayNameAsync(scope, Cancel));
        Assert.Same(Localized, await store.GetDisplayNamesAsync(scope, Cancel));
        Assert.Equal(["api"], await store.GetResourcesAsync(scope, Cancel));
        Assert.Same(Properties, await store.GetPropertiesAsync(scope, Cancel));
    }

    [Fact]
    public async Task Authorization_store_sets_and_gets_every_field()
    {
        var store = new MagicLinkAuthorizationStore(_database);
        var authorization = await store.InstantiateAsync(Cancel);
        var created = DateTimeOffset.UtcNow;

        await store.SetApplicationIdAsync(authorization, "app", Cancel);
        await store.SetCreationDateAsync(authorization, created, Cancel);
        await store.SetScopesAsync(authorization, ["openid"], Cancel);
        await store.SetStatusAsync(authorization, "valid", Cancel);
        await store.SetSubjectAsync(authorization, "subject", Cancel);
        await store.SetTypeAsync(authorization, "permanent", Cancel);
        await store.SetPropertiesAsync(authorization, Properties, Cancel);

        Assert.Null(await store.GetIdAsync(authorization, Cancel));
        Assert.Equal("app", await store.GetApplicationIdAsync(authorization, Cancel));
        Assert.Equal(created, await store.GetCreationDateAsync(authorization, Cancel));
        Assert.Equal(["openid"], await store.GetScopesAsync(authorization, Cancel));
        Assert.Equal("valid", await store.GetStatusAsync(authorization, Cancel));
        Assert.Equal("subject", await store.GetSubjectAsync(authorization, Cancel));
        Assert.Equal("permanent", await store.GetTypeAsync(authorization, Cancel));
        Assert.Same(Properties, await store.GetPropertiesAsync(authorization, Cancel));
    }

    [Fact]
    public async Task Token_store_sets_and_gets_every_field()
    {
        var store = new MagicLinkTokenStore(_database);
        var token = await store.InstantiateAsync(Cancel);
        var now = DateTimeOffset.UtcNow;

        await store.SetApplicationIdAsync(token, "app", Cancel);
        await store.SetAuthorizationIdAsync(token, "authorization", Cancel);
        await store.SetCreationDateAsync(token, now, Cancel);
        await store.SetExpirationDateAsync(token, now.AddHours(1), Cancel);
        await store.SetRedemptionDateAsync(token, now.AddMinutes(1), Cancel);
        await store.SetPayloadAsync(token, "payload", Cancel);
        await store.SetReferenceIdAsync(token, "reference", Cancel);
        await store.SetStatusAsync(token, "valid", Cancel);
        await store.SetSubjectAsync(token, "subject", Cancel);
        await store.SetTypeAsync(token, "access_token", Cancel);
        await store.SetPropertiesAsync(token, Properties, Cancel);

        Assert.Null(await store.GetIdAsync(token, Cancel));
        Assert.Equal("app", await store.GetApplicationIdAsync(token, Cancel));
        Assert.Equal("authorization", await store.GetAuthorizationIdAsync(token, Cancel));
        Assert.Equal(now, await store.GetCreationDateAsync(token, Cancel));
        Assert.Equal(now.AddHours(1), await store.GetExpirationDateAsync(token, Cancel));
        Assert.Equal(now.AddMinutes(1), await store.GetRedemptionDateAsync(token, Cancel));
        Assert.Equal("payload", await store.GetPayloadAsync(token, Cancel));
        Assert.Equal("reference", await store.GetReferenceIdAsync(token, Cancel));
        Assert.Equal("valid", await store.GetStatusAsync(token, Cancel));
        Assert.Equal("subject", await store.GetSubjectAsync(token, Cancel));
        Assert.Equal("access_token", await store.GetTypeAsync(token, Cancel));
        Assert.Same(Properties, await store.GetPropertiesAsync(token, Cancel));
    }

    [Fact]
    public async Task Updating_or_deleting_an_entity_this_store_did_not_create_throws()
    {
        var applications = new MagicLinkApplicationStore(_database);
        var authorizations = new MagicLinkAuthorizationStore(_database);
        var scopes = new MagicLinkScopeStore(_database);
        var tokens = new MagicLinkTokenStore(_database);

        await Assert.ThrowsAsync<ArgumentException>(() => applications.UpdateAsync(new MagicLinkApplication(), Cancel).AsTask());
        await Assert.ThrowsAsync<ArgumentException>(() => applications.DeleteAsync(new MagicLinkApplication(), Cancel).AsTask());
        await Assert.ThrowsAsync<ArgumentException>(() => authorizations.UpdateAsync(new MagicLinkAuthorization(), Cancel).AsTask());
        await Assert.ThrowsAsync<ArgumentException>(() => authorizations.DeleteAsync(new MagicLinkAuthorization(), Cancel).AsTask());
        await Assert.ThrowsAsync<ArgumentException>(() => scopes.UpdateAsync(new MagicLinkScope(), Cancel).AsTask());
        await Assert.ThrowsAsync<ArgumentException>(() => scopes.DeleteAsync(new MagicLinkScope(), Cancel).AsTask());
        await Assert.ThrowsAsync<ArgumentException>(() => tokens.UpdateAsync(new MagicLinkToken(), Cancel).AsTask());
        await Assert.ThrowsAsync<ArgumentException>(() => tokens.DeleteAsync(new MagicLinkToken(), Cancel).AsTask());
    }

    [Fact]
    public async Task Lookups_by_an_id_that_is_no_uuid_return_nothing_without_a_query()
    {
        var authorizations = new MagicLinkAuthorizationStore(_database);
        var tokens = new MagicLinkTokenStore(_database);

        Assert.Empty(await authorizations.FindByApplicationIdAsync("not-a-guid", Cancel).ToListAsync(Cancel));
        Assert.Empty(await authorizations.FindAsync(null, "not-a-guid", null, null, null, Cancel).ToListAsync(Cancel));
        Assert.Equal(0, await authorizations.RevokeAsync(null, "not-a-guid", null, null, Cancel));
        Assert.Equal(0, await authorizations.RevokeByApplicationIdAsync("not-a-guid", Cancel));
        Assert.Empty(await tokens.FindAsync(null, "not-a-guid", null, null, Cancel).ToListAsync(Cancel));
        Assert.Equal(0, await tokens.RevokeAsync(null, "not-a-guid", null, null, Cancel));
        Assert.Equal(0, await tokens.RevokeByApplicationIdAsync("not-a-guid", Cancel));
        Assert.Equal(0, await tokens.RevokeByAuthorizationIdAsync("not-a-guid", Cancel));
        Assert.Empty(await new MagicLinkScopeStore(_database).FindByNamesAsync([], Cancel).ToListAsync(Cancel));
    }

    [Fact]
    public void Empty_values_are_stored_as_null_and_read_back_empty()
    {
        Assert.Null(StoreJson.WriteProperties(ImmutableDictionary<string, JsonElement>.Empty));
        Assert.Null(StoreJson.WriteStrings(ImmutableDictionary<string, string>.Empty));
        Assert.Null(StoreJson.WriteLocalized(ImmutableDictionary<CultureInfo, string>.Empty));
        Assert.Null(StoreJson.WriteKeySet(null));
        Assert.Empty(StoreJson.ReadProperties(null));
        Assert.Empty(StoreJson.ReadStrings(""));
        Assert.Empty(StoreJson.ReadLocalized(null));
        Assert.Null(StoreJson.ReadKeySet(null));
        Assert.Null(StoreIds.ToText((Guid?)null));
    }

    [Fact]
    public void A_null_json_string_value_reads_back_as_empty()
    {
        Assert.Equal(string.Empty, StoreJson.ReadStrings("""{"s":null}""")["s"]);
        Assert.Equal(string.Empty, StoreJson.ReadLocalized("""{"":null}""")[CultureInfo.InvariantCulture]);
    }
}
