---
compress_memory: enabled
---

# Project Roadmap

Design: `docs/superpowers/specs/2026-11-20-roadmap-design.md`

## Milestone 1: First working sign-in [status: active]
**Goal:** Ship 0.1 on NuGet with the whole flow working: request a link, receive the email, click it, get signed in.
**Started:** 2026-11-20
**Definition of Done:**
- [ ] All six phases complete, each merged through a PR with green CI
- [ ] Required status checks set on the `Main` ruleset
- [ ] The sample app signs a user in end to end, using the in-memory store and an SMTP test server
- [ ] Store contract tests pass against the in-memory and ZeroAlloc.ORM stores
- [ ] 0.1 packages published to nuget.org by release-please

### Phase 1.1: Repo scaffolding and CI [status: active]
**Goal:** Solution layout, build/test workflow, ci-baseline checks, commitlint, release-please and codecov, modelled on Daedalus.NET.
**Surface:** Infra
**HelpWanted:** no
**Plan:** `docs/superpowers/plans/2026-11-20-phase-1-1-scaffolding.md`

### Phase 1.2: Core tokens and in-memory store [status: pending]
**Goal:** Generate, hash, expire and single-use validate tokens behind a store interface, with an in-memory store and reusable store contract tests.
**Surface:** Backend
**HelpWanted:** no
**Plan:** _to be written_

### Phase 1.3: Email abstraction and MailKit sender [status: pending]
**Goal:** An email sender interface, a MailKit SMTP sender and a default plain and HTML message.
**Surface:** Backend
**HelpWanted:** no
**Plan:** _to be written_

### Phase 1.4: ASP.NET Core integration [status: pending]
**Goal:** `AddMagicLink()`, request-link and verify endpoints, cookie sign-in and an email-to-user resolver hook.
**Surface:** Backend
**HelpWanted:** no
**Plan:** _to be written_

### Phase 1.5: ZeroAlloc.ORM token store [status: pending]
**Goal:** A token store on ZeroAlloc.ORM that passes the store contract tests.
**Surface:** Data
**HelpWanted:** no
**Plan:** _to be written_

### Phase 1.6: Sample app and README quickstart [status: pending]
**Goal:** A runnable sample app and a README quickstart that take a new app to a working sign-in.
**Surface:** Docs
**HelpWanted:** no
**Plan:** _to be written_

## Milestone 2: Security hardening [status: pending]
**Goal:** Make the library safe to expose on the public internet by default.
**Definition of Done:**
- [ ] Rate limits, responses that don't reveal accounts, optional browser binding, and audit events and metrics shipped and tested
- [ ] Threat model documented

### Phase 2.1: Rate limiting per email address and per IP [status: pending]
**Goal:** Limit link requests per email address and per client IP.
**Surface:** Backend
**HelpWanted:** no
**Plan:** _to be written_

### Phase 2.2: Responses that don't reveal accounts [status: pending]
**Goal:** Make request-link responses and timing identical whether or not the account exists.
**Surface:** Backend
**HelpWanted:** no
**Plan:** _to be written_

### Phase 2.3: Optional browser binding [status: pending]
**Goal:** Optionally require a link to be opened in the browser that requested it.
**Surface:** Backend
**HelpWanted:** no
**Plan:** _to be written_

### Phase 2.4: Audit events and OpenTelemetry metrics [status: pending]
**Goal:** Emit audit events and metrics for requests, sign-ins and failures.
**Surface:** Backend
**HelpWanted:** no
**Plan:** _to be written_

### Phase 2.5: Threat model and security docs [status: pending]
**Goal:** Document the threat model and safe deployment guidance.
**Surface:** Docs
**HelpWanted:** no
**Plan:** _to be written_

## Milestone 3: Extensibility [status: pending]
**Goal:** Let applications shape the emails and plug the sign-in into their existing user model.
**Definition of Done:**
- [ ] Templates, Identity integration and short codes shipped, tested and shown in samples

### Phase 3.1: Custom and localised email templates [status: pending]
**Goal:** Let apps supply their own email templates, localised per culture.
**Surface:** Backend
**HelpWanted:** no
**Plan:** _to be written_

### Phase 3.2: ASP.NET Core Identity integration [status: pending]
**Goal:** A package that signs in existing Identity users through SignInManager.
**Surface:** Backend
**HelpWanted:** no
**Plan:** _to be written_

### Phase 3.3: Short-code alternative [status: pending]
**Goal:** A short code the user can type when the link opens on another device.
**Surface:** Backend
**HelpWanted:** no
**Plan:** _to be written_

### Phase 3.4: Samples for the extension points [status: pending]
**Goal:** Sample code for templates, Identity and short codes.
**Surface:** Docs
**HelpWanted:** no
**Plan:** _to be written_

## Milestone 4: 1.0 [status: pending]
**Goal:** Freeze a reviewed public API and release 1.0.
**Definition of Done:**
- [ ] Public API reviewed, API-compat checks in CI, docs site live, security review done, 1.0 published

### Phase 4.1: Public API review and cleanup [status: pending]
**Goal:** Review and tidy the public API before freezing it.
**Surface:** Refactor
**HelpWanted:** no
**Plan:** _to be written_

### Phase 4.2: API-compat checks in CI [status: pending]
**Goal:** Fail CI on unintended public API breaks.
**Surface:** Infra
**HelpWanted:** no
**Plan:** _to be written_

### Phase 4.3: Benchmarks [status: pending]
**Goal:** Benchmarks for token generation, validation and the stores.
**Surface:** Backend
**HelpWanted:** no
**Plan:** _to be written_

### Phase 4.4: Docs site [status: pending]
**Goal:** A docs site covering setup, extension points and security.
**Surface:** Docs
**HelpWanted:** no
**Plan:** _to be written_

### Phase 4.5: Security review and 1.0 release [status: pending]
**Goal:** An independent security review, then release 1.0.
**Surface:** Mixed
**HelpWanted:** no
**Plan:** _to be written_
