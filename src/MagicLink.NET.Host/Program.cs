using MagicLink;
using MagicLink.Host;
using Npgsql;

// The container image's app (issue #12): MagicLink.NET on its PostgreSQL store, with every setting from configuration,
// such as environment variables like MagicLink__Issuer. It hosts the packages through services.AddMagicLink, as any
// app does, so it behaves the same; the clients, which an app registers in code, come from the settings here.
var builder = WebApplication.CreateSlimBuilder(args);

// Lets Kestrel serve HTTPS with a certificate from configuration, such as ASPNETCORE_Kestrel__Certificates__Default__Path,
// which the slim builder leaves out.
builder.WebHost.UseKestrelHttpsConfiguration();

var section = builder.Configuration.GetSection(HostConfiguration.Section);
var setup = HostSetup.Read(section.Get<HostConfiguration>() ?? new HostConfiguration());
if (setup.IsFailure)
{
    throw new InvalidOperationException(setup.Error);
}

var host = setup.Value;
var settings = host.Configuration;
builder.Services.AddSingleton(host);

builder.Services.AddMagicLink(server => server
        .SetIssuer(host.Issuer)
        .AddSigningCertificate(host.SigningCertificate)
        .AddEncryptionCertificate(host.EncryptionCertificate))
    .AddCore(core => core.UsePostgreSql(store =>
    {
        store.ConnectionString = settings.Store.ConnectionString;
        store.ApplyMigrationsOnStartup = settings.Store.ApplyMigrationsOnStartup;
        store.PruneExpiredData = settings.Store.PruneExpiredData;
    }));

if (host.UsesSignInDatabase)
{
    builder.Services.AddMagicLinkSignInDatabase(NpgsqlFactory.Instance, options =>
    {
        options.ConnectionString = settings.SignInDatabase.ConnectionString;
        options.Query = settings.SignInDatabase.Query;
    });
}
else
{
    builder.Services.AddMagicLinkSignInFile(options => options.Path = settings.SignInFile.Path);
}

builder.Services.Configure<MagicLinkOptions>(section);
builder.Services.Configure<MagicLinkSmtpOptions>(section.GetSection("Smtp"));

// Registered after the store, so its migrations have run when the clients are registered.
builder.Services.AddHostedService<ClientRegistration>();

var app = builder.Build();

// docker run magiclink-net check-sign-in ann@example.com answers from the source in the settings and exits, without
// starting the server (issue #9).
if (await CheckSignInCommand.TryRunAsync(app.Services, args, Console.Out, CancellationToken.None).ConfigureAwait(false) is { } exitCode)
{
    return exitCode;
}

app.MapMagicLink();
await app.RunAsync().ConfigureAwait(false);
return 0;

/// <summary>The container's host. Public for the tests' <c>WebApplicationFactory</c>.</summary>
public partial class Program;
