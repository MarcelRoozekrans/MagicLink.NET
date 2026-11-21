# ADR-016: Aim for Native AOT, with an own store on PostgreSQL

## Status

accepted (2026-11-20), amended (2026-11-21): Native AOT is now a requirement

## Context

MagicLink.NET is built on OpenIddict ([ADR-010](0010-build-magiclink-net-on-openiddict.md)). Found on 2026-11-15: since OpenIddict 7.0 its assemblies are marked as trimming- and Native AOT-compatible on .NET 9 and higher, but its Entity Framework Core stores do not yet work under Native AOT, and its author had not yet tested the server stack in an AOT-compiled ASP.NET Core app. OpenIddict needs to store four kinds of data: applications, authorizations, tokens and scopes.

Options considered:

* Aim for Native AOT, with an own store without EF Core, and first prove the server stack works under AOT.
* No Native AOT for now, using OpenIddict's EF Core store.

## Decision

* MagicLink.NET aims for Native AOT.
* It gets its own small OpenIddict store on PostgreSQL, without EF Core.
* A short proof comes first: does OpenIddict's server stack run in an AOT-compiled ASP.NET Core app?
* Native AOT is not a hard requirement of the first version until that proof succeeds.

## Consequences

* MagicLink.NET can start fast and use little memory, which suits a small service that runs next to other products.
* The store is extra work to build and maintain, but it covers only four kinds of data.
* If the proof fails, MagicLink.NET ships without Native AOT first; the own store still works then.
* This store holds OpenIddict's own data. It is separate from the read-only user database of [ADR-014](0014-look-up-who-may-sign-in-and-their-groups-through-a-pluggable-source.md).

## Amendment (2026-11-21)

Both proofs this decision waited on have succeeded. OpenIddict 7.9.0's server stack runs under Native AOT (issue #1). The own store, `MagicLink.NET.PostgreSql`, runs under it too, and so does the device flow on that store (issue #2). The `aot-proof` CI job publishes the proof app as a native binary, starts it on PostgreSQL and runs the flow tests against it.

Native AOT is therefore a requirement from now on. Two checks enforce it:

* Every package under `src/` builds with `IsAotCompatible`, so a trim or AOT warning fails the build.
* `aot-proof` is a required check on `main`. It runs what the proof app publishes: OpenIddict and `MagicLink.NET.PostgreSql`. The core package `MagicLink.NET` is not in that binary yet; the proof app moves onto it when the core gets its hosting API (issue #11), and from then on the job covers the core too.
