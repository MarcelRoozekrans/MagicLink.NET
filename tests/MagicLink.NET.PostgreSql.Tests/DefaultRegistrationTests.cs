using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenIddict.Abstractions;

namespace MagicLink.PostgreSql.Tests;

/// <summary>The default registration migrates at startup and serves all four OpenIddict managers. Own database.</summary>
public sealed class DefaultRegistrationTests(PostgresDatabase database) : IClassFixture<PostgresDatabase>
{
    [Fact]
    public async Task Startup_migrates_and_the_managers_run_on_the_store()
    {
        var cancel = TestContext.Current.CancellationToken;
        var builder = Host.CreateEmptyApplicationBuilder(settings: null);
        builder.Services.AddOpenIddict().AddCore(options => options.UsePostgreSql(store => store.ConnectionString = database.ConnectionString));
        using var host = builder.Build();

        await host.StartAsync(cancel);
        await using var scope = host.Services.CreateAsyncScope();
        var applications = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var authorizations = scope.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>();
        var scopes = scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();
        var tokens = scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>();

        var application = await applications.CreateAsync(
            new OpenIddictApplicationDescriptor { ClientId = "registered", ClientSecret = "secret", ClientType = OpenIddictConstants.ClientTypes.Confidential },
            cancel);
        var applicationId = await applications.GetIdAsync(application, cancel);
        var authorization = await authorizations.CreateAsync(
            new OpenIddictAuthorizationDescriptor { ApplicationId = applicationId, Subject = "s", Status = "valid", Type = "permanent" }, cancel);
        await scopes.CreateAsync(new OpenIddictScopeDescriptor { Name = "api" }, cancel);
        await tokens.CreateAsync(
            new OpenIddictTokenDescriptor { ApplicationId = applicationId, AuthorizationId = await authorizations.GetIdAsync(authorization, cancel), Subject = "s", Status = "valid", Type = "access_token" },
            cancel);

        var found = await applications.FindByClientIdAsync("registered", cancel);
        Assert.NotNull(found);
        Assert.True(await applications.ValidateClientSecretAsync(found, "secret", cancel));
        Assert.Equal(1, await scopes.CountAsync(cancel));
        Assert.Equal(1, await tokens.CountAsync(cancel));
        Assert.Equal(1, await authorizations.CountAsync(cancel));

        await host.StopAsync(cancel);
    }
}
