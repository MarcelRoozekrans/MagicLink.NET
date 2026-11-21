# MagicLink.NET

A small identity provider for .NET that signs people in with a link or a short code sent by email, and speaks OpenID Connect (OIDC) to everything else. It is meant for organizations without an identity provider of their own: applications keep speaking plain OIDC, and MagicLink.NET handles the email sign-in behind it.

**Status:** not built yet. The design is agreed. OpenIddict's server stack is proven to run under Native AOT; the proof app is in `samples/MagicLink.NET.AotProof` and CI re-runs it on every pull request. The own OpenIddict store on PostgreSQL is in `src/MagicLink.NET.PostgreSql`. The core in `src/MagicLink.NET` has the OIDC server for browser sign-in, the authorization code flow with PKCE, and the sign-in page where a person confirms with the link or the code, sending the sign-in email in the background, over SMTP by default, and a pluggable source of who may sign in and with which groups, with a file in git as the first one; the groups go into the `groups` claim of the tokens. The hosting API comes next.

## What it will do

* Act as an OIDC provider on [OpenIddict](https://github.com/openiddict/openiddict-core), for browser applications and for command-line tools through the device flow.
* Send a sign-in email with a link and a short code. Opening the link never signs anyone in; only pressing the button on the page, or typing the code, does. This keeps link-scanning mail filters from using up the link.
* Decide who may sign in, and with which groups, from an existing database (read-only) or a file in git, and put the groups in the token.
* Run on its own as a container, or inside an existing ASP.NET Core app as a NuGet package.

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
