# Session State — 2026-11-21

**Date:** 2026-11-21

## Current Position
- **Milestone:** 1 — First version (GitHub milestone "First version").
- **Done:** issues #1, #2, #3, #4 and #6 are closed.
- **Open:** issues #5, #7, #8, #9, #10, #11, #12 and #24.

## How to continue
Build sessions in this repository do not use superpowers or its planning flow, so `resume-work`, `start-next-phase` and the `docs/superpowers/` plans are no longer the way forward (product-line ADR-253). Instead:

1. Read `AGENTS.md`, the product-line `standards.md`, `docs/specs/` and `docs/adr/`. Specs and ADRs are the source.
2. Take the open issues of the milestone in the order of their dependencies. Each issue: its own branch, one pull request, a failing test first.
3. Merge when CI is green and CodeRabbit has approved. Write an open question into the issue instead of deciding it silently.

## Blockers
- First publish only, not CI: the operator must set the `RELEASE_BOT_PRIVATE_KEY` repository secret, install the release bot App on the repository, and add a nuget.org trusted publishing policy for `release-please.yml`. Until then the release-please workflow fails at its token step on every push to main.
