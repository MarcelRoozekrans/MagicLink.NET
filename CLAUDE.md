# Working on MagicLink.NET

These rules are for any AI coding session that builds MagicLink.NET, including unattended runs.

## Start here

* Read the product-line standards first: https://github.com/MarcelRoozekrans/product-line/blob/main/standards.md. They hold the rules for every product in the line (Native AOT, warnings as errors, ZeroAlloc libraries first, Result values, no EF Core in applications, and the rules for unattended build sessions). The decisions behind them are in that repository's `docs/adr/`.
* Then read `docs/README.md`, every Spec in `docs/specs/` and every ADR in `docs/adr/` of this repository. They say what MagicLink.NET does and why. The issues of a milestone each link to their Spec.
* From the start of the build, these documents are the source. If a criterion changes while building, update the Spec in `docs/specs/` in the same pull request and mention it in the issue.

## Unattended runs

When you are given one goal, such as "finish milestone First version", work through the issues of that milestone in the order of their dependencies, following the unattended rules in `standards.md`. At the end, leave a short summary in the milestone's last pull request or in a comment on its issues: what was built, and which open questions were written down.

## What is specific to MagicLink.NET

* Likely ZeroAlloc fits: ZeroAlloc.ORM for the own OpenIddict store (EF Core is ruled out by ADR-016), ZeroAlloc.Results, ZeroAlloc.Validation, ZeroAlloc.Telemetry and ZeroAlloc.Resilience. ZeroAlloc.Templates may serve as a starting point. OpenIddict expects the standard ASP.NET Core dependency injection; do not fight it.
* Native AOT is a goal, not yet a requirement, until the proof in issue #1 succeeds (ADR-016).
* Target .NET 10. Package ids are `MagicLink.NET` and `MagicLink.NET.*` (ADR-012).
* Tests run against real PostgreSQL and SQL Server; CI provides both as service containers (see `.github/workflows/ci.yml`).
