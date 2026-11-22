# Working on MagicLink.NET

These rules are for any AI coding session that builds MagicLink.NET, including unattended runs.

## Start here

* Read the product-line standards first: https://github.com/MarcelRoozekrans/product-line/blob/main/standards.md. They hold the rules for every product in the line (Native AOT, warnings as errors, ZeroAlloc libraries first, Result values, no EF Core in applications, and the rules for unattended build sessions). The decisions behind them are in that repository's `docs/adr/`.
* Then read `docs/README.md`, every Spec in `docs/specs/` and every ADR in `docs/adr/` of this repository. They say what MagicLink.NET does and why. The issues of a milestone each link to their Spec.
* From the start of the build, these documents are the source. If a criterion changes while building, update the Spec in `docs/specs/` in the same pull request and mention it in the issue.

## No superpowers skills

Do not use the superpowers skills (`superpowers:*`, such as brainstorming, writing-plans, subagent-driven-development and executing-plans) in this repository. Work straight from the Specs in `docs/specs/` and the ADRs in `docs/adr/`. The existing files under `docs/superpowers/` stay as a record but get no new plans or designs.

## Unattended runs

When you are given one goal, such as "finish milestone First version", work through the issues of that milestone in the order of their dependencies, following the unattended rules in `standards.md`. At the end, leave a short summary in the milestone's last pull request or in a comment on its issues: what was built, and which open questions were written down.

## What is specific to MagicLink.NET

* Likely ZeroAlloc fits: ZeroAlloc.ORM for the own OpenIddict store (EF Core is ruled out by ADR-016), ZeroAlloc.Results, ZeroAlloc.Validation, ZeroAlloc.Telemetry and ZeroAlloc.Resilience. ZeroAlloc.Templates may serve as a starting point. OpenIddict expects the standard ASP.NET Core dependency injection; do not fight it.
* Native AOT is a requirement (ADR-016, amended 2026-11-21). Every package under `src/` builds with `IsAotCompatible`, and the `aot-proof` CI job runs the native proof binary. The proof app hosts the core through its public hosting API on `MagicLink.NET.PostgreSql`, so the job covers OpenIddict, the store and the core; a feature added to the core gets a flow test there when it can run without a person's mailbox.
* Target .NET 10. Package ids are `MagicLink.NET` and `MagicLink.NET.*` (ADR-012).
* Tests run against real PostgreSQL and SQL Server; CI provides both as service containers (see `.github/workflows/ci.yml`).

## Commands

```bash
dotnet build -c Release                                 # what CI builds; warnings are errors
dotnet test                                             # needs MAGICLINK_TEST_POSTGRES and MAGICLINK_TEST_SQLSERVER for database tests
dotnet format whitespace . --folder --verify-no-changes
dotnet pack -c Release -p:Version=0.0.0-local -o artifacts
```

A pack without `-p:Version` fails on purpose with `MLVERSION001`; see `Directory.Build.props`.

## Layout and commits

* `src/<Package>/` holds each shipped package and `tests/<Package>.Tests/` its tests. `src/MagicLink.NET.Host/` is the container image's app, not a package; the `Dockerfile` at the root builds the image, and the required `container-image` CI job runs the host's flow tests against it. Every project targets `net10.0`, set once in `Directory.Build.props`.
* Package versions live only in `Directory.Packages.props`; a `PackageReference` never carries a `Version`.
* Public members in `src/` need XML docs; the build fails without them.
* Conventional commits, checked by commitlint on every pull request (`.commitlintrc.yml`). release-please builds the changelog and the version from them. Never put nested parentheses in a commit body: release-please cannot parse such a commit and drops it from the changelog while the workflow still succeeds.
* Required checks on `main`: `build-test`, `pack`, `commitlint`, `baseline / gitleaks`, `baseline / codeql (csharp)`, `baseline / nuget-audit`, `codecov/project`, `aot-proof` and `container-image`.
