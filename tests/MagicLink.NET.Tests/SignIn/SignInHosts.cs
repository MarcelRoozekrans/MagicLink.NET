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
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace MagicLink.Tests.SignIn;

/// <summary>
/// MagicLink.NET on PostgreSQL and an OpenID Connect client application, both in memory. The application uses ASP.NET
/// Core's own OpenID Connect handler, so it speaks OIDC as any application would.
/// </summary>
/// <remarks>
/// <see cref="Mailbox"/> replaces the SMTP sender and catches the link and code; the emails reach it in the background,
/// as they would reach a mail server. A test class signs the same people in many times, so these hosts allow far more
/// emails per address than the default; <see cref="DefaultLimitSignInHosts"/> keeps the default.
/// </remarks>
public class SignInHosts : IAsyncLifetime
{
    public const string ClientId = "test-app";

    /// <summary>The person who signs in, written as a person might type it.</summary>
    public const string Person = " Person@Example.com ";

    /// <summary>The same address as MagicLink.NET keeps it.</summary>
    public const string NormalizedPerson = "person@example.com";

    /// <summary>A person the file lists with one group, <c>readers</c>.</summary>
    public const string OneGroupPerson = "one-group@example.com";

    /// <summary>A person the file lists without groups.</summary>
    public const string NoGroupsPerson = "no-groups@example.com";

    /// <summary>A person the file lists until a test takes them out.</summary>
    public const string RemovedLaterPerson = "removed-later@example.com";

    /// <summary>An address the file does not list.</summary>
    public const string UnlistedPerson = "unlisted@example.com";

    /// <summary>The groups of <see cref="Person"/> in the file.</summary>
    public static readonly string[] PersonGroups = ["readers", "writers"];

    public static readonly Uri MagicLink = new("https://magiclink.test/");
    public static readonly Uri App = new("https://app.test/");
    public static readonly Uri AppCallback = new(App, "signin-oidc");

    // Test hosts read no settings files, so they need no file watchers; machines limit how many a user may have.
    private static readonly string[] HostArgs = ["--hostBuilder:reloadConfigOnChange=false"];

    private readonly PostgresDatabase _database = new();
    private readonly string _directory = Directory.CreateTempSubdirectory("magiclink-signin-hosts-").FullName;
    private readonly List<string> _listed = [NormalizedPerson, OneGroupPerson, NoGroupsPerson, RemovedLaterPerson];
    private readonly Dictionary<string, string[]> _movedTo = new(StringComparer.Ordinal);
    private WebApplication? _magicLink;
    private WebApplication? _app;

    /// <summary>The sign-in emails MagicLink.NET sent.</summary>
    internal Mailbox Mailbox { get; } = new();

    /// <summary>
    /// How many sign-in emails MagicLink.NET queued so far. The page queues an email before it answers, so unlike
    /// <see cref="Mailbox"/>, which the email reaches later, this shows straight after an answer whether an email was asked for.
    /// </summary>
    internal int Queued => MagicLinkServices.GetRequiredService<SignInMailQueue>().Queued;

    /// <summary>How many sign-in emails an address gets in the window, or null for the default.</summary>
    protected virtual int? MaxEmailsPerAddress => 1_000;

    /// <summary>What MagicLink.NET logged.</summary>
    internal ListLoggerProvider Logs { get; } = new();

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

    /// <summary>The file that says who may sign in, as a checkout of a git repository would hold it.</summary>
    private string PeopleFile => Path.Combine(_directory, "people.json");

    /// <summary>Takes <paramref name="email"/> out of the file, as a new commit would.</summary>
    internal async Task RemoveFromFileAsync(string email)
    {
        _listed.Remove(email);
        await WritePeopleFileAsync();
    }

    /// <summary>Gives <paramref name="email"/> other groups in the file, as a new commit would.</summary>
    internal async Task MoveToGroupsAsync(string email, params string[] groups)
    {
        _movedTo[email] = groups;
        await WritePeopleFileAsync();
    }

    public async ValueTask InitializeAsync()
    {
        await WritePeopleFileAsync();
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
        Directory.Delete(_directory, recursive: true);
    }

    private async Task WritePeopleFileAsync()
    {
        var people = _listed.Select(email => email switch
        {
            _ when _movedTo.TryGetValue(email, out var moved) => new Dictionary<string, object>(StringComparer.Ordinal) { ["email"] = email, ["groups"] = moved },
            NormalizedPerson => new Dictionary<string, object>(StringComparer.Ordinal) { ["email"] = "Person@Example.com", ["groups"] = PersonGroups },
            OneGroupPerson => new Dictionary<string, object>(StringComparer.Ordinal) { ["email"] = email, ["groups"] = new[] { "readers" } },
            _ => new Dictionary<string, object>(StringComparer.Ordinal) { ["email"] = email },
        });
        var previous = File.Exists(PeopleFile) ? File.GetLastWriteTimeUtc(PeopleFile) : DateTime.UtcNow;
        await File.WriteAllTextAsync(PeopleFile, JsonSerializer.Serialize(new { people }));

        // A newer time, as a checkout of a new commit gives the file, also when this runs within the clock's resolution.
        File.SetLastWriteTimeUtc(PeopleFile, previous.AddSeconds(1));
    }

    private WebApplication BuildMagicLink(string connectionString)
    {
        var builder = WebApplication.CreateSlimBuilder(HostArgs);
        builder.WebHost.UseTestServer(server => server.BaseAddress = MagicLink);

        builder.Services.AddOpenIddict()
            .AddCore(options => options.UsePostgreSql(store => store.ConnectionString = connectionString))
            .AddMagicLinkServer(server => server.SetIssuer(MagicLink).AddEphemeralEncryptionKey().AddEphemeralSigningKey());
        builder.Services.AddMagicLinkSignInFile(options => options.Path = PeopleFile);
        builder.Services.AddSingleton<ISignInMessageSender>(Mailbox);
        builder.Services.AddSingleton<TimeProvider>(Clock);
        builder.Logging.AddProvider(Logs);
        if (MaxEmailsPerAddress is { } maxEmails)
        {
            builder.Services.Configure<MagicLinkOptions>(options => options.MaxEmailsPerAddress = maxEmails);
        }

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
                Permissions.GrantTypes.RefreshToken,
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
