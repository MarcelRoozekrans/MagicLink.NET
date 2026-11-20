# Roadmap Design — MagicLink.NET

**Date:** 2026-11-20
**Author:** Claude with Marcel Roozekrans
**Stage:** roadmap (whole project)
**Status:** approved 2026-11-20

## Goal

MagicLink.NET is a public NuGet library for passwordless sign-in with magic links in .NET applications. It generates single-use, expiring links, delivers them by email, validates them when clicked, and signs the user in through ASP.NET Core authentication. Token storage and email delivery are pluggable: the library ships an in-memory store, a ZeroAlloc.ORM store and a MailKit sender. At the project level, success means an ASP.NET Core developer can add magic-link sign-in with a few lines of setup, trust it with real users, and replace any piece (store, sender, user mapping) without forking.

## Target Users / Stakeholders

- **Primary:** ASP.NET Core developers who want passwordless email sign-in without running an identity server. They need a small setup surface, safe defaults and clear extension points.
- **Secondary:** the maintainer's own applications, which will adopt it; and ZeroAlloc.ORM users, who get a first-party store.
- **Out of scope:** non-ASP.NET hosts beyond what the framework-agnostic core allows, full identity-provider features (OAuth/OIDC server, MFA management, user registration UI).

## Top-Level Success Criteria

- A new ASP.NET Core app goes from `dotnet add package` to a working sign-in by following the README quickstart, with no other setup than SMTP settings.
- Tokens are stored only as hashes, expire, and can be used once; this is covered by tests against every shipped store.
- The store, the email sender and the email-to-user mapping can each be replaced through a public interface, shown in the sample app.
- 1.0 ships with a reviewed public API, API-compat checks in CI, docs and a security review.

## Milestones

### Milestone 1: First working sign-in

**Goal:** Ship 0.1 on NuGet with the whole flow working: request a link, receive the email, click it, get signed in.

**Phases:**

1. Repo scaffolding and CI — `Surface: Infra`
2. Core tokens and in-memory store — `Surface: Backend`
3. Email abstraction and MailKit sender — `Surface: Backend`
4. ASP.NET Core integration — `Surface: Backend`
5. ZeroAlloc.ORM token store — `Surface: Data`
6. Sample app and README quickstart — `Surface: Docs`

### Milestone 2: Security hardening

**Goal:** Make the library safe to expose on the public internet by default.

**Phases:**

1. Rate limiting per email address and per IP — `Surface: Backend`
2. Responses that don't reveal whether an account exists — `Surface: Backend`
3. Optional binding of a link to the requesting browser — `Surface: Backend`
4. Audit events and OpenTelemetry metrics — `Surface: Backend`
5. Threat model and security docs — `Surface: Docs`

### Milestone 3: Extensibility

**Goal:** Let applications shape the emails and plug the sign-in into their existing user model.

**Phases:**

1. Custom and localised email templates — `Surface: Backend`
2. ASP.NET Core Identity integration package — `Surface: Backend`
3. Short-code alternative for links opened on another device — `Surface: Backend`
4. Samples for the new extension points — `Surface: Docs`

### Milestone 4: 1.0

**Goal:** Freeze a reviewed public API and release 1.0.

**Phases:**

1. Public API review and cleanup — `Surface: Refactor`
2. API-compat checks in CI — `Surface: Infra`
3. Benchmarks — `Surface: Backend`
4. Docs site — `Surface: Docs`
5. Security review and 1.0 release — `Surface: Mixed`

## Dependencies and Ordering Rationale

- **M1 → M2:** hardening needs a working flow to harden; rate limits and response shaping wrap the M1 endpoints.
- **M2 → M3:** a public sign-in library has to be safe before it grows features. Chosen by the maintainer on 2026-11-20.
- **M3 → M4:** the API is reviewed and frozen only once all planned extension points exist, so 1.0 doesn't break them later.
- **Within M1:** scaffolding first so every later phase merges through CI. Core tokens before the integration, because the integration calls the token service. The email phase depends only on the core. The ZeroAlloc.ORM store runs against the store contract tests from phase 1.2. The sample comes last, because it uses everything.
- **External:** ZeroAlloc.ORM (targets net10.0 and net11.0, which sets this library's target frameworks), MailKit, ASP.NET Core.

## Decisions Made in the Brainstorm

- Public NuGet library; target frameworks net10.0 and net11.0, matching ZeroAlloc.ORM.
- In M1, a verified link signs in a principal carrying the email claim. An optional `IMagicLinkUserResolver` lets the app map the email to its own user and claims, or reject it. ASP.NET Core Identity integration waits for M3.
- Package split (proposal, settled in phase designs): `MagicLink` (core, no ASP.NET Core dependency), `MagicLink.AspNetCore`, `MagicLink.MailKit`, `MagicLink.ZeroAlloc.Orm`.

## Risk Register

| Risk | Impact | Mitigation |
|---|---|---|
| A token-handling flaw in a security library | Account takeover for adopters | Hash-only storage, constant-time comparison, single-use enforced atomically in the store; M2 hardening and an M4 security review |
| ZeroAlloc.ORM API changes | The store package breaks | Pin the version through central package management; store contract tests catch regressions |
| Email deliverability problems are blamed on the library | Support load | Keep sending behind an interface and document SMTP setup |

## Open Questions

None. Per-milestone detail is settled by `new-milestone` and the per-phase brainstorms.
