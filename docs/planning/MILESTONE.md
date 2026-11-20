# Milestone 1: First working sign-in

**Status:** active
**Started:** 2026-11-20

## Goal
Ship 0.1 on NuGet with the whole magic-link flow working. A user requests a link, receives it by email (MailKit/SMTP), clicks it, and is signed in through the ASP.NET Core cookie scheme as a principal with their email claim, optionally mapped by an app-supplied user resolver. Tokens are random, stored only as hashes, expire, and can be used once. They live in a pluggable store, with in-memory and ZeroAlloc.ORM implementations. The repo has full CI from the first phase on.

## Definition of Done
- [ ] All six phases complete, each merged through a PR with green CI
- [ ] Required status checks set on the `Main` ruleset
- [ ] The sample app signs a user in end to end, using the in-memory store and an SMTP test server
- [ ] Store contract tests pass against the in-memory and ZeroAlloc.ORM stores
- [ ] All tests passing
- [ ] 0.1 packages published to nuget.org by release-please

## Phases
1. Phase 1.1 — Repo scaffolding and CI [active]
2. Phase 1.2 — Core tokens and in-memory store [pending]
3. Phase 1.3 — Email abstraction and MailKit sender [pending]
4. Phase 1.4 — ASP.NET Core integration [pending]
5. Phase 1.5 — ZeroAlloc.ORM token store [pending]
6. Phase 1.6 — Sample app and README quickstart [pending]

## Audit History
| Date | Verdict | Gaps |
|---|---|---|
