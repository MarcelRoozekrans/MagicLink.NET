using System.Security.Claims;
using MagicLink;
using MagicLink.AotProof;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

// Proof that MagicLink.NET runs under Native AOT (issues #1 and #11, ADR-016): an app with an API of its own hosts
// MagicLink.NET through the public hosting API, on the PostgreSQL store. Everything marked proof-only here is a shortcut
// for the proof and never belongs in src/.
var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.TypeInfoResolverChain.Insert(0, ProofJsonContext.Default));

var postgres = builder.Configuration[ProofClients.PostgresSetting];
if (string.IsNullOrWhiteSpace(postgres))
{
    throw new InvalidOperationException($"Set {ProofClients.PostgresSetting} to the PostgreSQL database the proof runs on.");
}

var issuer = new Uri(builder.Configuration[ProofClients.IssuerSetting] ?? "http://localhost/");

builder.Services.AddMagicLink(server =>
    {
        server.SetIssuer(issuer);

        // Proof-only: keys live in memory, and CI calls the native binary over plain HTTP.
        server.AddEphemeralEncryptionKey()
            .AddEphemeralSigningKey();
        server.UseAspNetCore()
            .DisableTransportSecurityRequirement();
    })
    .AddCore(core => core.UsePostgreSql(store => store.ConnectionString = postgres))
    .AddValidation(validation =>
    {
        validation.UseLocalServer();
        validation.UseAspNetCore();
    });

// Proof-only: one person may sign in, and the sign-in email stays in memory, where the flow tests read the code.
builder.Services.AddMagicLinkSignInSource<ProofSignInSource>();
builder.Services.AddSingleton<ProofMailbox>();
builder.Services.AddSingleton<ISignInMessageSender>(static services => services.GetRequiredService<ProofMailbox>());

// Registered after the store, so the migrations have run when the client is registered.
builder.Services.AddHostedService<ProofClientRegistration>();

// The app's own API checks MagicLink.NET's access tokens; its default scheme stays its own.
builder.Services.AddAuthentication(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
builder.Services.AddAuthorization();

var app = builder.Build();

app.MapMagicLink();

app.MapGet("/api/me", (ClaimsPrincipal user) => new MeResponse(user.GetClaim(Claims.Subject), [.. user.GetClaims(ProofClients.GroupsClaim)]))
    .RequireAuthorization();

// Proof-only: the sign-in emails to an address, whose code a person would read in their mailbox.
app.MapGet("/proof/mailbox", (string email, ProofMailbox mailbox) => mailbox.For(email));

await app.RunAsync().ConfigureAwait(false);

/// <summary>Exposes the entry point to <c>WebApplicationFactory</c> in the flow tests.</summary>
public partial class Program;
