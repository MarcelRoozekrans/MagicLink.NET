# Session State — 2026-11-20

**Date:** 2026-11-20

## Current Position
- **Milestone:** 1 — First version (GitHub milestone "First version", issues #1–#12)
- **Phase:** 1.1 — Repo scaffolding and CI: complete, PR #16 squash-merged as 270cead.
- **Last completed task:** Task 4 of `docs/superpowers/plans/2026-11-20-phase-1-1-scaffolding.md`. `Main` ruleset requires `build-test`, `pack`, `commitlint`, `baseline / gitleaks`, `baseline / codeql (csharp)`, `baseline / nuget-audit`, `codecov/project`.
- **Next task:** Phase 1.2 — Prove OpenIddict runs under Native AOT, issue #1. No design or plan yet.

## Open Decisions
- Issue #6 mail sender: MailKit (named in operator's goal) or built-in SMTP client. ADR-015 says only "pluggable, SMTP by default"; product-line standards prefer owning small code over heavy dependency. Decide in phase design, say why in PR.
- Native AOT enforcement in CI: standards want CI to enforce it; ADR-016 makes it optional until issue #1 proves it. Add AOT publish check in or right after Phase 1.2, per proof result.

## Blockers
- First publish only, not CI: operator must set `RELEASE_BOT_PRIVATE_KEY` repository secret, install release bot App on repo, add nuget.org trusted publishing policy for `release-please.yml`. Until then release-please workflow fails at its token step on every push to main.

## Recommended Next Step
Run `resume-work`, then `start-next-phase`: routes Phase 1.2 (issue #1) through brainstorming at phase scope. First read `CLAUDE.md`, product-line `standards.md`, `docs/specs/`, `docs/adr/`. Specs and ADRs are the source; `docs/superpowers/` holds designs and plans. Each issue: own branch, one PR, tests first. Merge when CI green and CodeRabbit approved; CodeRabbit reviews this repo.
