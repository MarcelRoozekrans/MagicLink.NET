---
compress_memory: enabled
---

# Project Roadmap

Design: `docs/superpowers/specs/2026-11-20-roadmap-design.md`. Source of truth: `docs/specs/` and `docs/adr/`.

## Milestone 1: First version [status: active]
**Goal:** Deliver every Spec in `docs/specs/`, tracked as GitHub milestone "First version" (issues #1–#12).
**Started:** 2026-11-20
**Definition of Done:**
- [ ] Every acceptance criterion in `docs/specs/` holds, with tests where a test can show it
- [ ] Issues #1–#12 closed, each through a pull request with green CI
- [ ] Required status checks set on the `Main` ruleset
- [ ] The Native AOT proof result written down

### Phase 1.1: Repo scaffolding and CI [status: active]
**Goal:** Solution layout, build/test with PostgreSQL and SQL Server, ci-baseline checks, commitlint, coverage, release-please.
**Surface:** Infra
**HelpWanted:** no
**Plan:** `docs/superpowers/plans/2026-11-20-phase-1-1-scaffolding.md`

### Phase 1.2: Prove OpenIddict runs under Native AOT [status: pending]
**Goal:** Write down whether OpenIddict's server stack runs in an AOT-compiled ASP.NET Core app.
**Surface:** Backend
**HelpWanted:** no
**Plan:** _to be written_
**Issue:** #1

### Phase 1.3: Own OpenIddict store on PostgreSQL [status: pending]
**Goal:** An OpenIddict store for applications, authorizations, tokens and scopes on PostgreSQL through ZeroAlloc.ORM, without EF Core.
**Surface:** Data
**HelpWanted:** no
**Plan:** _to be written_
**Issue:** #2

### Phase 1.4: Browser sign-in through OIDC [status: pending]
**Goal:** An application that speaks OIDC signs a person in through MagicLink.NET in the browser.
**Surface:** Backend
**HelpWanted:** no
**Plan:** _to be written_
**Issue:** #3

### Phase 1.5: Pluggable mail sender [status: pending]
**Goal:** SMTP by setting alone, an own sender can replace it, sending in the background with retries.
**Surface:** Backend
**HelpWanted:** no
**Plan:** _to be written_
**Issue:** #6

### Phase 1.6: Pluggable source for who may sign in [status: pending]
**Goal:** A source interface for who may sign in and their groups, with the git file as first implementation.
**Surface:** Backend
**HelpWanted:** no
**Plan:** _to be written_
**Issue:** #7

### Phase 1.7: Sign-in page [status: pending]
**Goal:** Link with a button, code entry, request bound to the browser that started it.
**Surface:** UI
**HelpWanted:** no
**Plan:** _to be written_
**Issue:** #4

### Phase 1.8: Same answer for unknown addresses [status: pending]
**Goal:** Unknown addresses get the same message and timing, no email, and the refusal is logged.
**Surface:** Backend
**HelpWanted:** no
**Plan:** _to be written_
**Issue:** #5

### Phase 1.9: Database source for PostgreSQL and SQL Server [status: pending]
**Goal:** A read-only database source, checked once at startup with a dummy address.
**Surface:** Data
**HelpWanted:** no
**Plan:** _to be written_
**Issue:** #8

### Phase 1.10: Test command for an email address [status: pending]
**Goal:** A command that shows whether an address may sign in and with which groups, without sending email.
**Surface:** Backend
**HelpWanted:** no
**Plan:** _to be written_
**Issue:** #9

### Phase 1.11: Refresh tokens [status: pending]
**Goal:** An application extends a session with a refresh token, without a new email.
**Surface:** Backend
**HelpWanted:** no
**Plan:** _to be written_
**Issue:** #10

### Phase 1.12: NuGet package with services.Add registration [status: pending]
**Goal:** Host MagicLink.NET in an existing ASP.NET Core app through services.Add...
**Surface:** Backend
**HelpWanted:** no
**Plan:** _to be written_
**Issue:** #11

### Phase 1.13: Container image [status: pending]
**Goal:** A thin host around the package, with the same behaviour.
**Surface:** Infra
**HelpWanted:** no
**Plan:** _to be written_
**Issue:** #12
