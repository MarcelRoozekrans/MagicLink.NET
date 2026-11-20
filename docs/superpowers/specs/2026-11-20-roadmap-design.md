# Roadmap Design — MagicLink.NET

**Date:** 2026-11-20
**Author:** Claude with Marcel Roozekrans
**Stage:** roadmap (whole project)
**Status:** approved 2026-11-20

## Goal

MagicLink.NET is a small identity provider for .NET. It signs people in with a link or a short code sent by email, and speaks OpenID Connect to everything else. It is meant for organizations without an identity provider of their own. The agreed specifications in `docs/specs/` and the decisions in `docs/adr/` are the source; this roadmap only orders the work.

## Target Users / Stakeholders

- **Primary:** organizations whose applications and CLIs need sign-in without running a full identity provider.
- **Secondary:** the other products in the product line, which use it as their single OIDC identity (product-line ADR-008).
- **Out of scope:** user registration, MFA management, anything beyond the two ways of delivery.

## Top-Level Success Criteria

- Every acceptance criterion of every Spec in `docs/specs/` holds.
- MagicLink.NET runs inside an ASP.NET Core app through `services.Add...` and on its own from a container image, with the same behaviour.
- The result of the Native AOT proof is written down, and the host ships as Native AOT if the proof succeeds.

## Milestones

### Milestone 1: First version

**Goal:** Deliver every Spec in `docs/specs/`, tracked as GitHub milestone "First version", issues #1–#12.

**Phases:**

1. Repo scaffolding and CI — `Surface: Infra`
2. Prove OpenIddict's server stack runs under Native AOT (#1) — `Surface: Backend`
3. Own OpenIddict store on PostgreSQL, without EF Core (#2) — `Surface: Data`
4. Browser sign-in through OIDC (#3) — `Surface: Backend`
5. Pluggable mail sender, SMTP by default, sending in the background (#6) — `Surface: Backend`
6. Pluggable source for who may sign in, with the git file as first implementation (#7) — `Surface: Backend`
7. Sign-in page: link with a button, code entry, request bound to the browser (#4) — `Surface: UI`
8. Same answer for unknown email addresses, refusal logged (#5) — `Surface: Backend`
9. Database source for PostgreSQL and SQL Server, read-only, checked at startup (#8) — `Surface: Data`
10. Test command for an email address (#9) — `Surface: Backend`
11. Refresh tokens (#10) — `Surface: Backend`
12. NuGet package with `services.Add...` registration (#11) — `Surface: Backend`
13. Container image (#12) — `Surface: Infra`

## Dependencies and Ordering Rationale

- Scaffolding first, so every issue merges through a pull request with the house checks.
- #1 before everything else: ADR-016 makes the AOT proof the first step, and its result decides whether later code must stay AOT-clean.
- #2 before #3: the OIDC flow needs OpenIddict's store.
- #6 and #7 before #4: the sign-in page sends the email and asks the source who may sign in.
- #5 needs the page and the sender. #8 and #9 build on the source from #7. #10 builds on the OIDC flow.
- #11 and #12 package what the other issues built, so they come last.

## Later Milestones

Not agreed yet. They are added when new Specs are agreed in `docs/specs/`. This deviates from the 3–7 milestone sweep on purpose: the agreed documents are the source, and this roadmap does not invent scope beyond them.

## Open Questions

- Is the package id `MagicLink.NET` free on nuget.org? A prefix reservation has been requested (ADR-012).
