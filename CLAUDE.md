# Working on MagicLink.NET

These rules are for any AI coding session that builds MagicLink.NET, including unattended runs.

## Start here

* Read `docs/README.md`, then every Spec in `docs/specs/` and every ADR in `docs/adr/` before writing code. They say what to build and why. The issues of a milestone each link to their Spec.
* From the start of the build, these documents are the source. If a criterion changes while building, update the Spec in `docs/specs/` in the same pull request and mention it in the issue.

## Unattended runs

When you are given one goal, such as "finish milestone First version", work through the issues of that milestone in the order of their dependencies.

* Per issue: a branch, tests first, the change, and one pull request that closes the issue.
* You may merge a pull request yourself once CI is green and your own review passes.
* Never create a release, publish a package or a container image, or force-push.
* Implementation choices inside a Spec (libraries, names, structure) are yours to make.
* A choice that would change behaviour, scope or architecture beyond the Specs and ADRs is not yours: leave that part, write the choice as an open question in the issue, and go on with the rest.
* At the end, leave a short summary in the milestone's last pull request or in a comment on the milestone's issues: what was built, and which open questions were written down.

## Libraries

* Prefer a ZeroAlloc library (https://github.com/ZeroAlloc-Net) where one fits the job. Likely fits: ZeroAlloc.ORM for the own OpenIddict store (EF Core is ruled out by ADR-016), ZeroAlloc.Results, ZeroAlloc.Validation, ZeroAlloc.Telemetry and ZeroAlloc.Resilience. ZeroAlloc.Templates may serve as a starting point.
* When a ZeroAlloc library exists for the job but you do not use it, say why in the pull request (for example: OpenIddict expects the standard ASP.NET Core dependency injection).
* Keep Native AOT in mind (ADR-016): avoid reflection-heavy libraries unless the AOT proof in issue #1 says it does not matter.

## Conventions

* Target .NET 10.
* Package ids are `MagicLink.NET` and `MagicLink.NET.*` (ADR-012).
* Tests run against real PostgreSQL and SQL Server; CI provides both as service containers (see `.github/workflows/ci.yml`).
