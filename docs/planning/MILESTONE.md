# Milestone 1: First version

**Status:** active
**Started:** 2026-11-20

## Goal
Deliver every Spec in `docs/specs/`: MagicLink.NET runs as an OIDC provider on OpenIddict, signs people in with a link or a short code by email, decides who may sign in from a pluggable source, and ships as a NuGet package and a container image. Native AOT is the aim, decided by the proof in issue #1. Tracked as GitHub milestone "First version", issues #1–#12.

## Definition of Done
- [ ] Every acceptance criterion in `docs/specs/` holds, with tests where a test can show it
- [ ] Issues #1–#12 closed, each through a pull request with green CI
- [ ] Required status checks set on the `Main` ruleset
- [ ] The Native AOT proof result written down
- [ ] All tests passing

## Phases
1. Phase 1.1 — Repo scaffolding and CI [active]
2. Phase 1.2 — Prove OpenIddict runs under Native AOT [pending]
3. Phase 1.3 — Own OpenIddict store on PostgreSQL [pending]
4. Phase 1.4 — Browser sign-in through OIDC [pending]
5. Phase 1.5 — Pluggable mail sender [pending]
6. Phase 1.6 — Pluggable source for who may sign in [pending]
7. Phase 1.7 — Sign-in page [pending]
8. Phase 1.8 — Same answer for unknown addresses [pending]
9. Phase 1.9 — Database source for PostgreSQL and SQL Server [pending]
10. Phase 1.10 — Test command for an email address [pending]
11. Phase 1.11 — Refresh tokens [pending]
12. Phase 1.12 — NuGet package with services.Add registration [pending]
13. Phase 1.13 — Container image [pending]

## Audit History
| Date | Verdict | Gaps |
|---|---|---|
