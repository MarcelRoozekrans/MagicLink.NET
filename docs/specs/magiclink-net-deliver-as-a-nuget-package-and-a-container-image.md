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

* *Is the package id* `MagicLink.NET` *free on nuget.org? A prefix reservation has been requested.*

## Tasks

* https://github.com/MarcelRoozekrans/MagicLink.NET/issues/11
* https://github.com/MarcelRoozekrans/MagicLink.NET/issues/12
