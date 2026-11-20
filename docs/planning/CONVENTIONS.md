# Project Conventions

> Written by `init-conventions`. Do not hand-edit — re-run the sub-skill instead; the Commit & Release Protocol reads these fields. One exception: the writing-plans `## Checks` rule may also fill `**Build:**` and `**Test:**`, which the protocol does not read. `ci-watch` also reads `**CI timeout:**`, which the protocol does not. Every other field goes through a re-run.

**Established:** 2026-11-20

## Stack

**Language / runtime:** .NET 10 (net10.0), C# latest
**Package manager:** NuGet, central package management (`Directory.Packages.props`)
**Framework:** ASP.NET Core, OpenIddict (ADR-010)
**Datastore:** PostgreSQL for the own OpenIddict store through ZeroAlloc.ORM (ADR-016); PostgreSQL and SQL Server read-only for the source of who may sign in (ADR-014)
**Build:** `dotnet build`
**Test:** `dotnet test`

## Commits

**Format:** conventional
**Scopes:** free
**Scope source:** n/a
**Fallback when scope not allowed:** omit scope

## Branching

**Model:** feature-branch
**PR required:** yes
**Protected branches:** main
**CI timeout:** 60 (defaulted)

## Versioning & Release

**Scheme:** semver
**Released by:** release-please
**Milestone completion tags a release:** no
**Changelog:** auto

## Deployment

**Deploy target:** nuget.org (MagicLink.NET packages) and a container image
**Environments:** none
**Deployed by:** release-please workflow publish job on release; the container image publish comes with issue #12
