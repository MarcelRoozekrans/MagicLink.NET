# MagicLink.NET

A small identity provider for .NET that signs people in with a link or a short code sent by email, and speaks OpenID Connect (OIDC) to everything else. It is meant for organizations without an identity provider of their own: applications keep speaking plain OIDC, and MagicLink.NET handles the email sign-in behind it.

**Status:** not built yet. The design is agreed. OpenIddict's server stack is proven to run under Native AOT; the proof app is in `samples/MagicLink.NET.AotProof` and CI re-runs it on every pull request. The own OpenIddict store on PostgreSQL is in `src/MagicLink.NET.PostgreSql`. The core in `src/MagicLink.NET` has the OIDC server for browser sign-in, the authorization code flow with PKCE, and the sign-in page where a person confirms with the link or the code, sending the sign-in email in the background, over SMTP by default, and a pluggable source of who may sign in and with which groups, with a file in git or a read-only query on an existing PostgreSQL or SQL Server database, and a `check-sign-in` command that shows what the source says about an address; the groups go into the `groups` claim of the tokens. An address the source does not know gets no email, but the same answer as a known one, and one address gets at most 5 sign-in emails in 15 minutes. An existing ASP.NET Core app hosts it with `services.AddMagicLink(...)`, and the container image runs it on its own as a Native AOT binary.

## What it will do

* Act as an OIDC provider on [OpenIddict](https://github.com/openiddict/openiddict-core), for browser applications and for command-line tools through the device flow.
* Send a sign-in email with a link and a short code. Opening the link never signs anyone in; only pressing the button on the page, or typing the code, does. This keeps link-scanning mail filters from using up the link.
* Decide who may sign in, and with which groups, from an existing database (read-only) or a file in git, and put the groups in the token.
* Run on its own as a container, or inside an existing ASP.NET Core app as a NuGet package.

## Hosting it in an ASP.NET Core app

```csharp
builder.Services.AddMagicLink(server => server
        .SetIssuer(new Uri("https://signin.example.com/"))
        .AddSigningCertificate(signing)
        .AddEncryptionCertificate(encryption))
    .AddCore(core => core.UsePostgreSql(store => store.ConnectionString = storeConnectionString));
builder.Services.AddMagicLinkSignInFile(options => options.Path = "people.json");
builder.Services.Configure<MagicLinkSmtpOptions>(smtp =>
{
    smtp.Host = "smtp.example.com";
    smtp.From = "Sign-in <signin@example.com>";
});

var app = builder.Build();
app.MapMagicLink();
```

`MagicLink.NET.PostgreSql` holds OpenIddict's data. Instead of the file, `AddMagicLinkSignInDatabase` reads who may sign in from an existing database, and an own `ISignInSource` or `ISignInMessageSender` replaces the source or the SMTP sender. MagicLink.NET does not start while a required setting is missing or wrong, and says which.

## Running it from the container image

The image is a thin host around the same packages, published as a Native AOT binary on a chiseled base image. No image is published yet; build it from the repository root:

```bash
docker build -t magiclink-net .
```

It builds for the machine it runs on, linux/amd64 or linux/arm64. Native AOT does not cross-compile between the two, so build an arm64 image on an arm64 machine.

Every setting comes from an environment variable, or from an `appsettings.json` mounted in `/app`. The container listens on port 8080 and runs as a user without root rights, so mounted files must be readable for it.

| Variable | What it sets |
| --- | --- |
| `MagicLink__Issuer` | The https address people and applications reach MagicLink.NET on. The links in the sign-in email start with it. |
| `MagicLink__Store__ConnectionString` | The PostgreSQL database that holds MagicLink.NET's own data. `MagicLink__Store__ApplyMigrationsOnStartup` and `MagicLink__Store__PruneExpiredData` default to `true`. |
| `MagicLink__SigningCertificate__Path`, `__Password` | A PKCS#12 file, `.pfx`, with the certificate and private key that sign tokens. |
| `MagicLink__EncryptionCertificate__Path`, `__Password` | The same for the certificate that encrypts tokens. |
| `MagicLink__SignInFile__Path` | The file of who may sign in and with which groups, kept in git. |
| `MagicLink__SignInDatabase__ConnectionString`, `__Query` | Instead of the file: a query on an existing PostgreSQL database. SQL Server works only when an app hosts MagicLink.NET, because its client library does not run under Native AOT. |
| `MagicLink__Smtp__Host`, `__Port`, `__UseStartTls`, `__UserName`, `__Password`, `__From`, `__Subject` | The SMTP server that sends the sign-in email. |
| `MagicLink__CodeLifetime`, `MagicLink__MaxCodeAttempts` | How long a code works, `00:10:00` by default, and how often it may be tried, 5 by default. |
| `MagicLink__MaxEmailsPerAddress`, `MagicLink__EmailLimitWindow` | How many sign-in emails one address gets, 5 by default, within a window of `00:15:00` by default. |
| `MagicLink__MaxSignInsPerClient`, `MagicLink__ClientLimitWindow` | How many sign-ins one client IP address starts, for any addresses, 20 by default, within a window of `00:15:00` by default. An IPv6 address counts by its /64 network. |
| `MagicLink__ForwardedHeaders__KnownProxies__0`, `MagicLink__ForwardedHeaders__KnownNetworks__0` | The reverse proxies whose `X-Forwarded-For` and `X-Forwarded-Proto` headers are trusted, as IP addresses or as networks such as `10.0.0.0/8`. Behind a proxy, set them, or every person counts as the proxy against the per-client limit. |
| `MagicLink__Clients__0__ClientId` | An application that may sign people in. Number the clients from 0. |
| `MagicLink__Clients__0__RedirectUris__0` | Where MagicLink.NET sends the person back to with the code: an https address, or http on localhost or a loopback address. Number them from 0. |
| `MagicLink__Clients__0__DeviceFlow` | `true` for a CLI that signs in with the device flow: the person types a user code in a browser. A client needs redirect URIs, the device flow, or both. |
| `MagicLink__Clients__0__ClientSecret` | The secret of a web app with a server side. Without it the client is public, such as a CLI, and proves itself with PKCE. |
| `MagicLink__Clients__0__Scopes__0` | `email`, and `offline_access` for refresh tokens. Every client may ask for `openid`. |
| `MagicLink__Clients__0__DisplayName` | The name of the application, for people. |

`docker run magiclink-net check-sign-in ann@example.com`, with the same settings, shows whether an address may sign in and with which groups, without starting the server. It exits with 0 when the address may sign in, 1 when it may not, and 2 when that cannot be told.

The clients in the settings are the full list: each start adds new ones, updates changed ones and removes clients that are no longer listed, with their tokens. The container does not start while a setting is missing or wrong, and names each one.

OpenID Connect needs HTTPS. Either let Kestrel serve it, with `ASPNETCORE_URLS=https://+:8443` and the certificate in `ASPNETCORE_Kestrel__Certificates__Default__Path` and `__Password`, or put a proxy in front that ends TLS and set `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`. That setting trusts the forwarded headers of any caller, so the container must then be reachable through the proxy only.

## How it is designed

What MagicLink.NET should do, and why, is in [docs](docs/README.md): the agreed specifications and the architecture decisions.


## Building

```bash
dotnet build
dotnet test
```

The SDK version is pinned in `global.json`. Tests that need PostgreSQL or SQL Server read their connection strings from `MAGICLINK_TEST_POSTGRES` and `MAGICLINK_TEST_SQLSERVER`; CI provides both databases.

## License

[MIT](https://github.com/MarcelRoozekrans/MagicLink.NET/blob/main/LICENSE)
