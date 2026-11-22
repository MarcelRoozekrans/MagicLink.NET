using System.Net;
using System.Security.Claims;
using MagicLink.PostgreSql.Tests;
using MagicLink.Tests.SignIn;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using static MagicLink.Tests.SignIn.Oidc;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace MagicLink.Tests.Hosting;

// docs/specs/magiclink-net-deliver-as-a-nuget-package-and-a-container-image.md, criterion 1: MagicLink.NET can be hosted
// in an existing ASP.NET Core app through services.Add..., with the same behaviour as on its own (issue #11).
public sealed class HostedInAnAppTests(HostedInAnAppTests.ExistingApp app) : IClassFixture<HostedInAnAppTests.ExistingApp>
{
    [Fact]
    public async Task A_person_signs_in_to_an_application_through_magiclink_hosted_in_the_app()
    {
        var browser = app.NewBrowser();
        var (authorize, verifier, _) = NewAuthorization();

        using var emailForm = await browser.NavigateAsync(authorize);
        Assert.Equal(HttpStatusCode.OK, emailForm.StatusCode);
        Assert.Equal("/signin", browser.Visited[^1].AbsolutePath);

        using (var sent = await (await HtmlForm.ReadAsync(emailForm)).SubmitAsync(browser, ("email", SignInHosts.Person)))
        {
            Assert.Equal(HttpStatusCode.SeeOther, sent.StatusCode);
        }

        var message = await app.Mailbox.WaitForAsync(0);
        using var confirmed = await SignInFlow.TypeCodeAsync(browser, message.Code);
        var code = await CodeFrom(await SignInFlow.ContinueAsync(browser, confirmed, stopAtHost: SignInHosts.App.Host));
        using var tokens = await RedeemAsync(browser, code, verifier);

        using var idToken = Payload(tokens.RootElement.GetProperty("id_token").GetString()!);
        Assert.Equal(SignInHosts.NormalizedPerson, idToken.RootElement.GetProperty("email").GetString());
        Assert.Equal(ExistingApp.Groups, Strings(idToken.RootElement.GetProperty("groups")));
    }

    [Fact]
    public async Task The_app_keeps_its_own_pages_and_its_own_sign_in()
    {
        var browser = app.NewBrowser();

        using var open = await browser.SendAsync(new HttpRequestMessage(HttpMethod.Get, new Uri(SignInHosts.MagicLink, "/hello")));
        Assert.Equal(HttpStatusCode.OK, open.StatusCode);
        Assert.Equal("hello", await open.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        // The app's own scheme is still its default, as the only scheme it registered: it challenges for the app's pages,
        // and signs a person in to the app without naming a scheme. MagicLink.NET's session is never the default.
        using var closed = await browser.SendAsync(new HttpRequestMessage(HttpMethod.Get, new Uri(SignInHosts.MagicLink, "/private")));
        Assert.Equal(HttpStatusCode.Redirect, closed.StatusCode);
        Assert.Equal(ExistingApp.OwnLoginPath, closed.Headers.Location!.AbsolutePath);

        using (var signedIn = await browser.SendAsync(new HttpRequestMessage(HttpMethod.Post, new Uri(SignInHosts.MagicLink, ExistingApp.OwnLoginPath))))
        {
            Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);
        }

        using var opened = await browser.SendAsync(new HttpRequestMessage(HttpMethod.Get, new Uri(SignInHosts.MagicLink, "/private")));
        Assert.Equal(HttpStatusCode.OK, opened.StatusCode);
        Assert.Equal("private for app-user", await opened.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// An app with its own pages and its own cookie sign-in, which hosts MagicLink.NET with the public API only:
    /// <c>AddMagicLink</c>, a store, a source of who may sign in, a sender, and <c>MapMagicLink</c>. The app relies on
    /// ASP.NET Core's implicit default scheme, and requires a signed-in user on every page that does not allow
    /// anonymous access.
    /// </summary>
    public sealed class ExistingApp : IAsyncLifetime
    {
        public const string OwnLoginPath = "/own-login";

        public static readonly string[] Groups = ["staff"];

        // Test hosts read no settings files, so they need no file watchers.
        private static readonly string[] HostArgs = ["--hostBuilder:reloadConfigOnChange=false"];

        private readonly PostgresDatabase _database = new();
        private WebApplication? _app;

        internal Mailbox Mailbox { get; } = new();

        public Browser NewBrowser()
        {
            _ = _database.ConnectionString; // Skips, or fails in CI, without a database.
            var handler = _app!.GetTestServer().CreateHandler();
            return new Browser(new Dictionary<string, HttpMessageHandler>(StringComparer.Ordinal) { [SignInHosts.MagicLink.Host] = handler });
        }

        public async ValueTask InitializeAsync()
        {
            await _database.InitializeAsync();
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(PostgresDatabase.Variable)))
            {
                return;
            }

            var builder = WebApplication.CreateSlimBuilder(HostArgs);
            builder.WebHost.UseTestServer(server => server.BaseAddress = SignInHosts.MagicLink);

            // What the app had before it hosted MagicLink.NET: one scheme, so the default without naming it, and a
            // fallback policy that closes every page not marked anonymous.
            builder.Services.AddAuthentication()
                .AddCookie(options => options.LoginPath = OwnLoginPath);
            builder.Services.AddAuthorizationBuilder()
                .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

            // MagicLink.NET, through the public hosting API.
            builder.Services.AddMagicLink(server => server.SetIssuer(SignInHosts.MagicLink).AddEphemeralEncryptionKey().AddEphemeralSigningKey())
                .AddCore(core => core.UsePostgreSql(store => store.ConnectionString = _database.ConnectionString));
            builder.Services.AddMagicLinkSignInSource<StaffSource>();
            builder.Services.AddSingleton<ISignInMessageSender>(Mailbox);

            _app = builder.Build();
            _app.MapGet("/hello", static () => "hello").AllowAnonymous();
            _app.MapGet("/private", static (ClaimsPrincipal user) => "private for " + user.Identity!.Name);
            _app.MapPost(OwnLoginPath, static (HttpContext context) => context.SignInAsync(
                new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "app-user")], "own")))).AllowAnonymous();
            _app.MapMagicLink();
            await _app.StartAsync();

            await using var scope = _app.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>().CreateAsync(new OpenIddictApplicationDescriptor
            {
                ClientId = SignInHosts.ClientId,
                ClientType = ClientTypes.Public,
                RedirectUris = { SignInHosts.AppCallback },
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

        public async ValueTask DisposeAsync()
        {
            if (_app is not null)
            {
                await _app.DisposeAsync();
            }

            await _database.DisposeAsync();
        }
    }

    /// <summary>An own source, as a host writes it against the public interface: every address is staff.</summary>
    private sealed class StaffSource : ISignInSource
    {
        public ValueTask<SignInAccess> LookUpAsync(string email, CancellationToken cancellationToken) =>
            ValueTask.FromResult(SignInAccess.Allowed(ExistingApp.Groups));
    }
}
