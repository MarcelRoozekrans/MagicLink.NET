# Phase 1.1 Repo Scaffolding and CI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A buildable, tested, packable MagicLink.NET solution with house CI, ci-baseline checks and release-please publishing to nuget.org.

**Architecture:** `MagicLink.slnx` with `src/MagicLink` (core package) and `tests/MagicLink.Tests`. Shared MSBuild settings go in `Directory.Build.props` and `Directory.Packages.props`. GitHub Actions workflows are modelled on AI.Sentinel (library CI and release) and call MarcelRoozekrans/ci-baseline `@v1`.

**Tech Stack:** .NET SDK 10 (net10.0), xUnit v3 on Microsoft.Testing.Platform, Microsoft.Testing.Extensions.CodeCoverage, GitHub Actions, release-please v5, NuGet trusted publishing.

**Spec:** `docs/superpowers/specs/2026-11-20-phase-1-1-scaffolding-design.md`

## Global Constraints

- Target framework: `net10.0` for every project.
- `global.json`: SDK `10.0.100`, `rollForward: latestFeature`, test runner `Microsoft.Testing.Platform`.
- Package versions only in `Directory.Packages.props`; no `Version` attribute on a `PackageReference` in a csproj.
- `TreatWarningsAsErrors=true`; nullable and implicit usings enabled.
- Test projects are named `<Package>.Tests` and live under `tests/`.
- Every text file uses LF line endings.
- The required check names, exactly: `build-test`, `pack`, `commitlint`, `baseline / gitleaks`, `baseline / codeql (csharp)`, `baseline / nuget-audit`, `codecov/project`.
- Commit messages are conventional, with no nested parentheses in bodies.

## Review Focus

- A pack without `-p:Version` must fail with error code `MLVERSION001` rather than shipping version 1.0.0. Task 1 Step 6 tests this.
- `MagicLinkOptions.Validate()` at the exact boundary: 24 hours is valid, 24 hours plus one tick is not. Task 1 tests both.
- The coverage gate must find reports. The test step writes Cobertura files to `TestResults/**/*.cobertura.xml`. Task 2 Step 3 runs the same command locally and checks the files exist.
- The release workflow must not publish on a push that created no release. The `publish` job is guarded on `release_created` or `workflow_dispatch`, and Task 3 Step 2 checks the guard expression.
- On a PR, every job named in the required checks has to start. Task 4 Step 3 reads the check names the PR actually reported before setting the ruleset.

---

### Task 1: Solution skeleton and MagicLinkOptions

**Files:**
- Create: `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`, `.gitattributes`, `.gitignore`, `MagicLink.slnx`, `README.md` (stub)
- Create: `src/MagicLink/MagicLink.csproj`, `src/MagicLink/MagicLinkOptions.cs`
- Test: `tests/MagicLink.Tests/MagicLink.Tests.csproj`, `tests/MagicLink.Tests/MagicLinkOptionsTests.cs`

**Interfaces:**
- Produces: `namespace MagicLink; public sealed class MagicLinkOptions { public static readonly TimeSpan MaxTokenLifetime; /* 24h */ public TimeSpan TokenLifetime { get; set; } /* default 15 min */ public void Validate(); /* throws InvalidOperationException naming TokenLifetime */ }`. Phase 1.2 extends this class.

- [x] **Step 1: Write the build files.**
  - `global.json`: exact values from Global Constraints.
  - `Directory.Build.props`: copy the AI.Sentinel props with these changes:
    - `TargetFramework` is `net10.0`;
    - `RootNamespace` is `MagicLink`;
    - analyzer `PackageReference`s carry no versions;
    - packaging metadata: `Authors` Marcel Roozekrans, `PackageLicenseExpression` MIT, `PackageProjectUrl` and `RepositoryUrl` https://github.com/MarcelRoozekrans/MagicLink.NET, `PackageReadmeFile` README.md packed from the repo root, `PackageTags` `passwordless;magic-link;authentication;aspnetcore`;
    - no logo;
    - add the version pack guard modelled on ZeroAlloc.ORM's, with codes renamed `MLVERSION001` and `MLVERSION002`.
  - `Directory.Packages.props` sets `ManagePackageVersionsCentrally=true` and holds the analyzer versions from AI.Sentinel, plus `xunit.v3` 4.0.3 and `Microsoft.Testing.Extensions.CodeCoverage`. The latest stable version of the code-coverage package is resolved with `dotnet package search`.
  - `.editorconfig`, `.gitattributes` and `.gitignore` come from AI.Sentinel, with Daedalus's `.gitattributes` contents.
  - A stub `README.md` holding the package name, because the pack in Step 6 packs it. Task 4 fills it in.
- [x] **Step 2: Create the projects.**
  - `src/MagicLink/MagicLink.csproj`: `PackageId` MagicLink, with the description "Passwordless sign-in with magic links: single-use, expiring tokens with pluggable storage and email delivery."
  - `tests/MagicLink.Tests/MagicLink.Tests.csproj`: `OutputType` Exe, `IsPackable` false, `NoWarn` MA0004, references `xunit.v3`, the code-coverage package and the project `src/MagicLink`.
  - Add both projects to `MagicLink.slnx` with `dotnet sln MagicLink.slnx add`.
- [x] **Step 3: Write the failing tests** in `MagicLinkOptionsTests`:
  - `TokenLifetime_DefaultsTo15Minutes`: `Assert.Equal(TimeSpan.FromMinutes(15), new MagicLinkOptions().TokenLifetime)`.
  - `Validate_AcceptsDefault`: `new MagicLinkOptions().Validate()` does not throw.
  - `Validate_AcceptsMaximum`: a lifetime of `TimeSpan.FromHours(24)` does not throw.
  - `Validate_RejectsOutOfRange`, a `[Theory]` over the tick values `0`, `-1` and `TimeSpan.FromHours(24).Ticks + 1`. `Assert.Throws<InvalidOperationException>` must throw, and the message must contain `TokenLifetime`.
- [x] **Step 4: Run** `dotnet test`. Expected: compile failure, because `MagicLinkOptions` does not exist yet.
- [x] **Step 5: Implement `MagicLinkOptions`** per the Interfaces block. The class has XML docs on every public member.
- [x] **Step 6: Verify.**
  - `dotnet build -c Release` has 0 warnings.
  - `dotnet test` passes on net10.0.
  - `dotnet format whitespace . --folder --verify-no-changes` exits 0.
  - `dotnet pack src/MagicLink -c Release -p:Version=0.0.0-ci -o artifacts` writes `MagicLink.0.0.0-ci.nupkg`.
  - `dotnet pack src/MagicLink -c Release -o artifacts` fails with `MLVERSION001`.
- [x] **Step 7: Commit** with `build: scaffold solution with core project and tests`.

### Task 2: CI and baseline workflows

**Files:**
- Create: `.github/workflows/ci.yml`, `.github/workflows/baseline-checks.yml`, `.github/workflows/history-scan.yml`, `codecov.yml`, `coverage.config.xml`, `.commitlintrc.yml`

**Interfaces:**
- Produces: the job names `build-test`, `pack` and `commitlint` in workflow `CI`, and `baseline` in workflow `baseline-checks`. Task 4 requires these names.

- [x] **Step 1: Write `ci.yml`.**
  - It runs on push to main and on pull requests to main, with `concurrency` cancelling in-progress runs per ref and top-level `permissions: contents: read`.
  - Job `build-test`, `runs-on: ubuntu-latest`, `permissions: contents: read, id-token: write`:
    1. checkout v7;
    2. setup-dotnet v6 with `global-json-file: global.json` and `dotnet-version: 10.0.x`;
    3. `dotnet format whitespace . --folder --verify-no-changes`;
    4. `dotnet restore MagicLink.slnx`;
    5. `dotnet build MagicLink.slnx --no-restore -c Release`;
    6. `dotnet test MagicLink.slnx --no-build -c Release --coverage --coverage-output-format cobertura --coverage-settings coverage.config.xml --results-directory TestResults`;
    7. `MarcelRoozekrans/ci-baseline/actions/coverage-gate@v1` with `reports: TestResults/**/*.cobertura.xml`;
    8. codecov-action v7 with `use_oidc: true`, the same files, `disable_search: true` and `fail_ci_if_error: false`.
  - Job `pack` (needs `build-test`): set up the same way, then `dotnet pack MagicLink.slnx -c Release -p:Version=0.0.0-ci -o nupkg`, and fail if `nupkg/` has no `.nupkg`.
  - Job `commitlint`: copy Daedalus's, PR-only, including the `skip-commitlint` label opt-out.
- [x] **Step 2: Write the other files.**
  - `baseline-checks.yml` and `history-scan.yml` per the ci-baseline README, with `solution: MagicLink.slnx` and `dotnet-version: global-json`.
  - `codecov.yml` is the exact ci-baseline block.
  - `coverage.config.xml` is AI.Sentinel's, with the module include regex `.*MagicLink.*\.dll$`.
  - `.commitlintrc.yml` is Daedalus's, with the comment referring to MagicLink.NET.
- [x] **Step 3: Verify.**
  - Every workflow parses: `python3 -c "import yaml,sys;[yaml.safe_load(open(f)) for f in sys.argv[1:]]" .github/workflows/*.yml`.
  - If `actionlint` is available, run it.
  - Run the Step 1 test command locally and check that `find TestResults -name '*.cobertura.xml'` lists at least 1 file for the net10.0 test app.
- [x] **Step 4: Commit** with `ci: add build, test, coverage, commitlint and baseline workflows`.

### Task 3: Release workflow and repository variables

**Files:**
- Create: `.github/workflows/release-please.yml`, `release-please-config.json`, `.release-please-manifest.json`

- [x] **Step 1: Write `release-please.yml`** from AI.Sentinel's.
  - Drop the `native` job, the RID-package steps and the archive steps.
  - `publish` needs only `release-please` and runs when `release_created == 'true'`, or on `workflow_dispatch` on main.
  - Its steps:
    1. checkout the tag on dispatch;
    2. set up the SDK from `global.json`;
    3. `dotnet build MagicLink.slnx -c Release -p:Version=$VERSION`;
    4. `dotnet test MagicLink.slnx -c Release --no-build`;
    5. `dotnet pack MagicLink.slnx -c Release --no-build -p:Version=$VERSION -o nupkg`;
    6. `NuGet/login@v1` with `user: ${{ vars.NUGET_USER }}`;
    7. push `nupkg/*.nupkg` with `--skip-duplicate`;
    8. `gh release upload "$TAG" --clobber nupkg/*.nupkg`;
    9. ci-baseline `sbom@v1` with `solution: MagicLink.slnx` and `tag: ${{ env.TAG }}`.
  - Keep the step that catches feat and fix commits release-please dropped silently, and the release bot token step.
- [x] **Step 2: Verify the triggers and the config.**
  - The YAML parses.
  - `grep -n "release_created == 'true'" .github/workflows/release-please.yml` matches the `publish` job's `if`.
  - The config has the house changelog sections, release-type `simple`, `bump-minor-pre-major`, `bump-patch-for-minor-pre-major` and `"initial-version": "0.1.0"`.
  - The manifest is `{}`.
- [x] **Step 3: Set the repository variables.**
  - `gh variable set RELEASE_BOT_CLIENT_ID` and `gh variable set NUGET_USER` on MarcelRoozekrans/MagicLink.NET, with the values from AI.Sentinel.
  - Verify with `gh variable list -R MarcelRoozekrans/MagicLink.NET`.
- [x] **Step 4: Commit** with `ci: add release-please workflow publishing to nuget.org`.

### Task 4: Repo docs, PR and required checks

**Files:**
- Create: `README.md`, `AGENTS.md`, `.github/renovate.json`, `.github/pull_request_template.md`

- [x] **Step 1: Write the docs.**
  - `README.md`: the project goal from the roadmap design, the planned package list with "coming soon" marks, the licence, and status "pre-release".
  - `AGENTS.md`: the build, test and format commands; the layout rules from Global Constraints; the commit rules, including no nested parentheses in commit bodies.
  - `renovate.json` and the PR template are copied from AI.Sentinel or Daedalus and stripped of anything specific to those repos.
  - Apply anti-slop to the prose.
- [x] **Step 2: Commit, push and open the PR.**
  - Commit with `docs: add README, contributor notes and repo templates`.
  - Push the branch.
  - Open the PR titled `build: scaffold repo, CI and release pipeline (phase 1.1)`.
- [x] **Step 3: Wait for CI and set the ruleset.**
  - Read the check names the PR reported.
  - When every name in Global Constraints is present and green, add the `required_status_checks` rule to ruleset 27747701: strict policy, GitHub Actions checks with `integration_id` 15368, and `codecov/project` without one.
  - Verify by reading the ruleset back.
  - If `codecov/project` has not reported, require the others and file an issue to add it.
- [x] **Step 4: Merge** with a squash merge once every required check is green.

## Checks

- build: `dotnet build`
- tests: `dotnet test`

## Amendment 2026-11-20: aligned with the agreed specs

While this phase was being built, the agreed specs (`docs/specs/`), the ADRs (`docs/adr/`) and a build-session CI were merged to main. The operator chose to follow them. The phase changed in these ways; where the text above disagrees, this section wins:

- The solution is `MagicLink.NET.slnx`, the core project `src/MagicLink.NET` with package id `MagicLink.NET` (ADR-012), and the tests `tests/MagicLink.NET.Tests`.
- Target framework `net10.0` only, SDK 10 in `global.json`. Projects under `src/` set `IsAotCompatible` (product-line standards, ADR-016).
- `MagicLinkOptions` holds the code limits from the confirm-sign-in Spec: `CodeLifetime` of 10 minutes and `MaxCodeAttempts` of 5. The invented `TokenLifetime` and its `Validate()` are gone.
- `ci.yml` keeps main's PostgreSQL and SQL Server service containers and test connection strings in the `build-test` job.
- The release `publish` job does not run the tests, because they need those databases; the release commit already passed CI as the release pull request.
- `AGENTS.md` is folded into main's `CLAUDE.md`. `README.md` is main's, with a Building section and an absolute licence link.
- The required checks also include `pack`.
