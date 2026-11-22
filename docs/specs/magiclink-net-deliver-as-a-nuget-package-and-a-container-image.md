# MagicLink.NET: deliver as a NuGet package and a container image

Status: agreed (2026-11-20)

## Goal

MagicLink.NET can be hosted inside an existing ASP.NET Core app or run on its own, with the same behaviour.

## Context

* Package ids: [ADR-012](../adr/0012-package-ids-for-magiclink-net-start-with-magiclink-net.md).
* MagicLink.NET stays usable on its own, like every product in the line (ADR-008, a product-line decision not published here).

## Scope

**In**

* NuGet packages to host in an own ASP.NET Core app: the core, `MagicLink.NET`, and its PostgreSQL store, `MagicLink.NET.PostgreSql`, which holds OpenIddict's data (ADR-016).
* A container image, a thin host around those packages.

**Out**

* Anything beyond these two ways of delivery.

## Acceptance criteria

1. MagicLink.NET can be hosted in an existing ASP.NET Core app through the NuGet packages with `services.Add...`, and can run on its own from the container image, with the same behaviour.

## How a host uses it

An ASP.NET Core app hosts MagicLink.NET with `services.AddMagicLink(server => ...)`, which sets the issuer and keys, then `.AddCore(core => core.UsePostgreSql(...))` for the store, a source of who may sign in such as `services.AddMagicLinkSignInFile(...)`, and `app.MapMagicLink()`. The app keeps its own authentication and pages: MagicLink.NET's session is never the default scheme. An own source or sender implements the public `ISignInSource` or `ISignInMessageSender`. The container image of issue #12 is a thin host on the same calls, so both ways behave alike.

## Open questions

* *Is the package id* `MagicLink.NET` *free on nuget.org?* Settled: `MagicLink.NET` and `MagicLink.NET.PostgreSql` are published on nuget.org, at 0.1.2, owned by MarcelRoozekrans. The reservation of the `MagicLink.NET` prefix has been requested but is not verified yet: the nuget.org search API shows `verified: false` for both packages. Checked on 2026-11-22.
* *Where do the container's clients come from?* An app that hosts MagicLink.NET registers its applications in code, through OpenIddict. The container has no code of its own, so it reads them from its settings, `MagicLink__Clients__N__...`, and those settings are the full list: each start adds new clients, updates changed ones and removes the ones no longer listed, with their authorizations and tokens. Removing them keeps a client taken out of the settings from signing anyone in. A client gets the authorization code flow with PKCE when it has redirect URIs, the device flow of issue #24 with `DeviceFlow` set to `true`, and refresh tokens with the `offline_access` scope. Written down while building issue #12.
* *Can the container read who may sign in from SQL Server?* Not yet. Microsoft.Data.SqlClient 7.1.2 gives trim warnings under Native AOT, so the container reads from a file or from PostgreSQL only. An app that hosts MagicLink.NET without Native AOT can still use SQL Server. Written down while building issue #12.
* *Does the container run the `check-sign-in` command?* Yes, since issue #9 merged: when its arguments start with `check-sign-in`, the host runs the command on its built host instead of starting the server and exits with the command's code, as in `docker run magiclink-net check-sign-in ann@example.com`. It needs the same settings as the server.
* *Which platforms does the image support?* The Dockerfile builds for linux-x64 only. An arm64 image needs a Native AOT build for that platform, on an arm64 runner or with a cross-compiler. Written down while building issue #12.

## Tasks

* https://github.com/MarcelRoozekrans/MagicLink.NET/issues/11
* https://github.com/MarcelRoozekrans/MagicLink.NET/issues/12
