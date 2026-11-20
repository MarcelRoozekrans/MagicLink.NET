# Working on MagicLink.NET

## Commands

```bash
dotnet build                                         # Debug build, both target frameworks
dotnet build -c Release                              # what CI builds; warnings are errors
dotnet test                                          # all test projects, net10.0 and net11.0
dotnet format whitespace . --folder --verify-no-changes
dotnet pack -c Release -p:Version=0.0.0-local -o artifacts
```

A pack without `-p:Version` fails on purpose with `MLVERSION001`; see `Directory.Build.props`.

## Layout

- `src/<Package>/` holds each shipped package; `tests/<Package>.Tests/` holds its tests.
- Every project targets `net10.0;net11.0`, set once in `Directory.Build.props`.
- Package versions live only in `Directory.Packages.props`. A `PackageReference` never carries a `Version`.
- Public members in `src/` need XML docs; the build fails without them.
- Text files use LF line endings.

## Commits and pull requests

- Conventional commits, checked by commitlint on every PR (`.commitlintrc.yml`). release-please builds the changelog and the version from them.
- Never put nested parentheses in a commit body. release-please cannot parse such a commit and drops it from the changelog, while the workflow still succeeds.
- `main` is protected: changes go in through a PR with green CI.

## Planning

Roadmap, milestone and conventions live in `docs/planning/`. Designs and plans live in `docs/superpowers/`.
