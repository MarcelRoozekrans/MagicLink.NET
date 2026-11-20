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

## Open questions

* *Is the package id* `MagicLink.NET` *free on nuget.org? A prefix reservation has been requested.*

## Tasks

* https://github.com/MarcelRoozekrans/MagicLink.NET/issues/11
* https://github.com/MarcelRoozekrans/MagicLink.NET/issues/12
