using System.Security.Claims;
using System.Text.Json;
using MagicLink.PostgreSql.Tests;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace MagicLink.Tests.SignIn;

/// <summary>
/// MagicLink.NET on PostgreSQL and an OpenID Connect client application, both in memory. The application uses ASP.NET
/// Core's own OpenID Connect handler, so it speaks OIDC as any application would.
/// </summary>
/// <remarks>
/// Sending the email is issue #6; until then <see cref="Mailbox"/> catches the link and code the page would mail.
/// </remarks>
public sealed class SignInHosts : IAsyncLifetime
{
    public const string ClientId = "test-app";

    /// <summary>The person who signs in, written as a person might type it.</summary>
    public const string Person = " Person@Example.com ";

    /// <summary>The same address as MagicLink.NET keeps it.</summary>
    public const string NormalizedPerson = "person@example.com";

    public static readonly Uri MagicLink = new("https://magiclink.test/");
    public static readonly Uri App = new("https://app.test/");
    public static readonly Uri AppCallback = new(App, "signin-oidc");

    // Test hosts read no settings files, so they need no file watchers; machines limit how many a user may have.
    private static readonly string[] HostArgs = ["--hostBuilder:reloadConfigOnChange=false"];

    private readonly PostgresDatabase _database = new();
    private WebApplication? _magicLink;
    private WebApplication? _app;

    /// <summary>The sign-in emails MagicLink.NET would have sent.</summary>
    internal Mailbox Mailbox { get; } = new();

    /// <summary>MagicLink.NET's clock, which a test can move forward.</summary>
    internal TestClock Clock { get; } = new();

    /// <summary>MagicLink.NET's services, to put a sign-in request in the store as only a damaged store would hold it.</summary>
    internal IServiceProvider MagicLinkServices
    {
        get
        {
            _ = _database.ConnectionString; // Skips, or fails in CI, without a database.
            return _magicLink!.Services;
        }
    }

    /// <summary>A fresh browser, without cookies, that can reach both hosts.</summary>
    public Browser NewBrowser() => NewBrowser(alias: null);

    /// <summary>
    /// A fresh browser that also reaches MagicLink.NET under <paramref name="alias"/>, as a request with a forged Host
    /// header would.
    /// </summary>
    public Browser NewBrowser(string? alias)
    {
        _ = _database.ConnectionString; // Skips, or fails in CI, without a database.
        var hosts = new Dictionary<string, HttpMessageHandler>(StringComparer.Ordinal)
        {
            [MagicLink.Host] = _magicLink!.GetTestServer().CreateHandler(),
            [App.Host] = _app!.GetTestServer().CreateHandler(),
        };
        if (alias is not null)
        {
            hosts[alias] = hosts[MagicLink.Host];
        }

        return new Browser(hosts);
    }

    public async ValueTask InitializeAsync()
    {
        await _database.InitializeAsync();
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(PostgresDatabase.Variable)))
        {
            return;
        }

        _magicLink = BuildMagicLink(_database.ConnectionString);
        await _magicLink.StartAsync();
        await RegisterClientAsync(_magicLink.Services);

        _app = BuildApp(_magicLink.GetTestServer().CreateHandler());
        await _app.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
        }

        if (_magicLink is not null)
        {
            await _magicLink.DisposeAsync();
        }

        await _database.DisposeAsync();
    }

    private WebApplication BuildMagicLink(string connectionString)
    {
        var builder = WebApplication.CreateSlimBuilder(HostArgs);
        builder.WebHost.UseTestServer(server => server.BaseAddress = MagicLink);

        builder.Services.AddOpenIddict()
            .AddCore(options => options.UsePostgreSql(store => store.ConnectionString = connectionString))
            .AddMagicLinkServer(server => server.SetIssuer(MagicLink).AddEphemeralEncryptionKey().AddEphemeralSigningKey());
        builder.Services.AddSingleton<ISignInMessageSender>(Mailbox);
        builder.Services.AddSingleton<TimeProvider>(Clock);

        var app = builder.Build();
        app.UseAuthentication();
        app.MapMagicLinkEndpoints();
        return app;
    }

    private static async Task RegisterClientAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        await manager.CreateAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = ClientId,
            ClientType = ClientTypes.Public,
            RedirectUris = { AppCallback },
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.Token,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.ResponseTypes.Code,
                Permissions.Scopes.Email,
            },
            Requirements = { Requirements.Features.ProofKeyForCodeExchange },
        });
    }

    private static WebApplication BuildApp(HttpMessageHandler magicLink)
    {
        var builder = WebApplication.CreateSlimBuilder(HostArgs);
        builder.WebHost.UseTestServer(server => server.BaseAddress = App);

        builder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddCookie()
            .AddOpenIdConnect(options =>
            {
                options.Authority = MagicLink.ToString();
                options.BackchannelHttpHandler = magicLink;
                options.ClientId = ClientId;
                options.ResponseType = "code";
                options.ResponseMode = "query"; // The test browser follows redirects; it does not post forms.
                options.UsePkce = true;
                options.Scope.Clear();
                options.Scope.Add(Scopes.OpenId);
                options.Scope.Add(Scopes.Email);
                options.GetClaimsFromUserInfoEndpoint = true;
                options.MapInboundClaims = false;
                options.SaveTokens = true;
            });
        builder.Services.AddAuthorization();

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet("/me", new RequestDelegate(MeAsync)).RequireAuthorization();
        return app;
    }

    /// <summary>What the application knows about the signed-in person, and the tokens it received.</summary>
    private static async Task MeAsync(HttpContext context)
    {
        var user = context.User;
        var me = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [Claims.Subject] = user.FindFirstValue(Claims.Subject),
            [Claims.Email] = user.FindFirstValue(Claims.Email),
            [Claims.EmailVerified] = user.FindFirstValue(Claims.EmailVerified),
            ["id_token"] = await context.GetTokenAsync("id_token"),
            ["access_token"] = await context.GetTokenAsync("access_token"),
        };
        context.Response.ContentType = "application/json";
        await JsonSerializer.SerializeAsync(context.Response.Body, me, cancellationToken: context.RequestAborted);
    }
}
