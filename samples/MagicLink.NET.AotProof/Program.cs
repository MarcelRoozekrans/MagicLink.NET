using System.Security.Claims;
using MagicLink.AotProof;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using OpenIddict.Validation.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

// Proof that OpenIddict's server and validation stack runs under Native AOT (issue #1, ADR-016).
// Everything marked proof-only here is a shortcut for the proof and never belongs in src/.
var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.TypeInfoResolverChain.Insert(0, ProofJsonContext.Default));

builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        options.SetDefaultApplicationEntity<ProofApplication>();
        options.ReplaceApplicationStore<ProofApplication, InMemoryApplicationStore>(ServiceLifetime.Singleton);
    })
    .AddServer(options =>
    {
        options.SetAuthorizationEndpointUris("connect/authorize")
            .SetTokenEndpointUris("connect/token");

        options.AllowAuthorizationCodeFlow()
            .RequireProofKeyForCodeExchange()
            .AllowRefreshTokenFlow()
            .AllowClientCredentialsFlow();

        options.RegisterScopes(Scopes.OpenId, Scopes.OfflineAccess);

        // Proof-only: keys live in memory, and nothing but the application store is persisted.
        options.AddEphemeralEncryptionKey()
            .AddEphemeralSigningKey();
        options.DisableTokenStorage();
        options.DisableAuthorizationStorage();
        options.DisableScopeValidation();

        options.UseAspNetCore()
            .EnableAuthorizationEndpointPassthrough()
            .EnableTokenEndpointPassthrough()
            .DisableTransportSecurityRequirement(); // Proof-only: CI calls the native binary over plain HTTP.
    })
    .AddValidation(options =>
    {
        options.UseLocalServer();
        options.UseAspNetCore();
    });

builder.Services.AddAuthentication(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/connect/authorize", (HttpContext context) =>
{
    var request = context.GetOpenIddictServerRequest()
        ?? throw new InvalidOperationException("The OpenIddict server request cannot be retrieved.");

    // Proof-only: signs in a fixed subject at once; the sign-in page is Phase 1.7.
    var identity = NewIdentity(ProofClients.TestSubject);
    identity.SetScopes(request.GetScopes());
    return Results.SignIn(new ClaimsPrincipal(identity), properties: null, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
});

app.MapPost("/connect/token", async (HttpContext context) =>
{
    var request = context.GetOpenIddictServerRequest()
        ?? throw new InvalidOperationException("The OpenIddict server request cannot be retrieved.");

    if (request.IsClientCredentialsGrantType())
    {
        var identity = NewIdentity(request.ClientId!);
        identity.SetScopes(request.GetScopes());
        return Results.SignIn(new ClaimsPrincipal(identity), properties: null, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType())
    {
        var result = await context.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme).ConfigureAwait(false);
        return Results.SignIn(result.Principal!, properties: null, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    return Results.Forbid(authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
});

app.MapGet("/api/me", (ClaimsPrincipal user) => new MeResponse(user.GetClaim(Claims.Subject)))
    .RequireAuthorization();

await RegisterClientsAsync(app.Services).ConfigureAwait(false);
await app.RunAsync().ConfigureAwait(false);

static ClaimsIdentity NewIdentity(string subject)
{
    var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme, Claims.Name, Claims.Role);
    identity.SetClaim(Claims.Subject, subject);
    identity.SetDestinations(static _ => [Destinations.AccessToken]);
    return identity;
}

static async Task RegisterClientsAsync(IServiceProvider services)
{
    var scope = services.CreateAsyncScope();
    await using var _ = scope.ConfigureAwait(false);
    var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

    await manager.CreateAsync(new OpenIddictApplicationDescriptor
    {
        ClientId = ProofClients.ConfidentialClientId,
        ClientSecret = ProofClients.ConfidentialClientSecret,
        ClientType = ClientTypes.Confidential,
        Permissions = { Permissions.Endpoints.Token, Permissions.GrantTypes.ClientCredentials },
    }).ConfigureAwait(false);

    await manager.CreateAsync(new OpenIddictApplicationDescriptor
    {
        ClientId = ProofClients.PublicClientId,
        ClientType = ClientTypes.Public,
        RedirectUris = { new Uri(ProofClients.PublicRedirectUri) },
        Permissions =
        {
            Permissions.Endpoints.Authorization,
            Permissions.Endpoints.Token,
            Permissions.GrantTypes.AuthorizationCode,
            Permissions.GrantTypes.RefreshToken,
            Permissions.ResponseTypes.Code,
            Permissions.Prefixes.Scope + Scopes.OfflineAccess,
        },
        Requirements = { Requirements.Features.ProofKeyForCodeExchange },
    }).ConfigureAwait(false);
}

/// <summary>Exposes the entry point to <c>WebApplicationFactory</c> in the flow tests.</summary>
public partial class Program;
