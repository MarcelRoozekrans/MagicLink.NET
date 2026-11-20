# Session State — 2026-11-20

**Date:** 2026-11-20

## Current Position
- **Milestone:** 1 — First version (GitHub milestone "First version", issues #1–#12)
- **Phase:** 1.1 — Repo scaffolding and CI, complete. PR #16 was squash-merged as 270cead.
- **Last completed task:** Task 4 from `docs/superpowers/plans/2026-11-20-phase-1-1-scaffolding.md`. The `Main` ruleset now requires `build-test`, `pack`, `commitlint`, `baseline / gitleaks`, `baseline / codeql (csharp)`, `baseline / nuget-audit` and `codecov/project`.
- **Next task:** Phase 1.2 — Prove OpenIddict runs under Native AOT, issue #1. It has no design or plan yet.

## Open Decisions
- Issue #6, the mail sender: use MailKit, as the operator's goal named, or the built-in SMTP client. ADR-015 says only "pluggable, SMTP by default", and the product-line standards prefer owning small code over a heavy dependency. Decide in that phase's design and say why in its PR.
- Native AOT enforcement in CI: the standards want CI to enforce it, but ADR-016 makes it optional until issue #1 proves it. Add the AOT publish check in or right after Phase 1.2, depending on the proof's result.

## Blockers
- First publish only, not CI: the operator still has to set the `RELEASE_BOT_PRIVATE_KEY` repository secret, install the release bot App on this repository, and add a nuget.org trusted publishing policy for `release-please.yml`. Until then the release-please workflow fails at its token step on every push to main.

## Recommended Next Step
Run `resume-work`, then `start-next-phase`. It routes Phase 1.2, issue #1, through brainstorming at phase scope. Read `CLAUDE.md`, the product-line `standards.md`, `docs/specs/` and `docs/adr/` first. Specs and ADRs are the source; `docs/superpowers/` holds designs and plans. Build each issue on its own branch with one PR, tests first. Merge when CI is green and CodeRabbit has approved; CodeRabbit reviews this repository.
