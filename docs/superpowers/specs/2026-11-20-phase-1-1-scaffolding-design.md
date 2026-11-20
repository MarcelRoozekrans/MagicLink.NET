# Phase 1.1 Design — Repo scaffolding and CI

**Date:** 2026-11-20
**Phase:** 1.1
**Surface:** Infra
**Stage:** phase (one phase, implementation-ready)
**Status:** approved 2026-11-20

## Goal

Every later phase merges through a pull request with the house checks green: build, tests, a coverage gate, commitlint and the ci-baseline security checks. Releases are cut by release-please and published to nuget.org.

## Approach

**Solution layout.** Use `MagicLink.slnx` at the root, with `src/` for the packages and `tests/` for the test projects, each test project named `<Package>.Tests`. This phase creates the core package `src/MagicLink` and `tests/MagicLink.Tests`. Later phases add `MagicLink.MailKit`, `MagicLink.AspNetCore` and `MagicLink.ZeroAlloc.Orm` next to them, each with its own test project. Every project targets `net10.0;net11.0`. `global.json` pins SDK 11.0.100 with `rollForward: latestFeature` and the Microsoft.Testing.Platform runner, matching ZeroAlloc.ORM.

**Build settings.** These come from AI.Sentinel, the closest library-shaped repo in the house:
- `Directory.Build.props` turns on nullable, implicit usings and `TreatWarningsAsErrors`, NuGetAudit at moderate, and the house analyzers (Meziantou, Roslynator, ErrorProne.NET, ZeroAlloc.Analyzers).
- It also sets shared package metadata: MIT licence expression, the README in the package, an embedded PDB, and `ContinuousIntegrationBuild` for `src/` only.
- It has a pack guard modelled on ZeroAlloc.ORM's, which fails a pack that got no `-p:Version`.
- Central package management lives in `Directory.Packages.props`.
- `.editorconfig`, `.gitattributes` (LF everywhere) and `.gitignore` are copied from the house repos.

**Tests.** The tests use xUnit v3 on Microsoft.Testing.Platform, with coverage from Microsoft.Testing.Extensions.CodeCoverage and a `coverage.config.xml` that leaves out tests, samples and generated code. ci-baseline's coverage gate needs production code to measure. This phase therefore ships one real type that phase 1.2 extends: `MagicLinkOptions`, with `TokenLifetime` (default 15 minutes) and a `Validate()` that rejects a lifetime of zero, a negative one, or one over 24 hours. The tests cover it fully.

**Workflows:**
- `ci.yml`:
  - job `build-test` checks formatting (`dotnet format whitespace --folder`), restores, builds in Release, tests with coverage, runs the ci-baseline coverage gate (floor 80) and uploads to Codecov through OIDC;
  - job `pack` packs every `src/` project with a probe version, so packaging breaks show up on PRs;
  - job `commitlint` runs on PRs with the house `.commitlintrc.yml`.
- `baseline-checks.yml` calls `ci-baseline/.github/workflows/baseline.yml@v1` with `solution: MagicLink.slnx`.
- `history-scan.yml` is the ci-baseline history-scan caller.
- `release-please.yml` runs on every push to main, modelled on AI.Sentinel:
  - The release bot App token opens the release PR, so that PR runs CI.
  - A step catches feat and fix commits that release-please dropped silently.
  - When a release is created, a `publish` job builds with `-p:Version`, tests, packs every `src/` project, and pushes to nuget.org through trusted publishing (`NuGet/login@v1`, `vars.NUGET_USER`). It then uploads the packages to the GitHub release and attaches the ci-baseline SBOM.
- `release-please-config.json` uses release-type `simple` with `bump-minor-pre-major`, `bump-patch-for-minor-pre-major`, the house changelog sections and `initial-version: 0.1.0`. The manifest starts empty, so the first release is 0.1.0.

**Repo hygiene.** `codecov.yml` is the exact ci-baseline block. The repo also gets `renovate.json`, `.github/pull_request_template.md`, a README stub with the goal and the package list, and `AGENTS.md` with the build and test commands, the commit rules and the no-nested-parentheses rule for commit bodies.

**Repository settings, done by Claude.** Set the repository variables `RELEASE_BOT_CLIENT_ID` and `NUGET_USER`, copying the values from AI.Sentinel. Once CI has reported on the phase PR, add the required status checks to the `Main` ruleset: `build-test`, `commitlint`, `baseline / gitleaks`, `baseline / codeql (csharp)`, `baseline / nuget-audit` and `codecov/project`.

**Done by the operator, outside this phase's merge.** These steps need Marcel and block only the first publish, not CI:
- Set the `RELEASE_BOT_PRIVATE_KEY` secret.
- Make sure the release bot App is installed on this repo.
- Add a nuget.org trusted publishing policy for `MarcelRoozekrans/MagicLink.NET`, workflow `release-please.yml`.

## Files / Modules Touched

- `MagicLink.slnx`, `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`, `.gitattributes`, `.gitignore`, `coverage.config.xml`, `codecov.yml`
- `src/MagicLink/MagicLink.csproj`, `src/MagicLink/MagicLinkOptions.cs`
- `tests/MagicLink.Tests/MagicLink.Tests.csproj`, `tests/MagicLink.Tests/MagicLinkOptionsTests.cs`
- `.github/workflows/ci.yml`, `baseline-checks.yml`, `history-scan.yml`, `release-please.yml`
- `release-please-config.json`, `.release-please-manifest.json`, `.commitlintrc.yml`, `.github/renovate.json`, `.github/pull_request_template.md`
- `README.md`, `AGENTS.md`

## Tests

- Unit: `MagicLinkOptions` defaults and `Validate()`, covering a lifetime of zero, a negative one, one over the maximum, and a valid one.
- Pipeline:
  - `dotnet build` and `dotnet test` pass locally on both target frameworks;
  - `dotnet pack -p:Version=0.0.0-ci` passes, and a pack without a version fails with the guard's error;
  - the phase PR shows every required check green.

## Open Questions

None.

## Risks

| Risk | Impact | Mitigation |
|---|---|---|
| A required check name differs from what the ruleset lists | Every PR is blocked | Add the required checks only after reading the check names the phase PR actually reported |
| Codecov never reports `codecov/project` on the first PR | `codecov/project` can't be required yet | Require it only once it has reported; otherwise file an issue and require it after the first main push |
| The secret, App install or NuGet policy is missing at release time | The publish job fails | The publish job can be re-run; AI.Sentinel's republish-by-dispatch input is kept |
