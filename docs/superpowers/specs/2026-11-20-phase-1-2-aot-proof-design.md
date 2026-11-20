# Phase 1.2 Design — Prove OpenIddict runs under Native AOT

**Date:** 2026-11-20
**Phase:** 1.2
**Surface:** Backend
**Stage:** phase (one phase, implementation-ready)
**Status:** approved 2026-11-20

> Approval: the operator's milestone goal hands implementation choices inside a Spec to the build session (product-line `standards.md`, unattended rules) and asked for this phase to be designed, planned and built in one go. Every choice below is the session's call; none changes behaviour, scope or architecture beyond the Spec and ADR-016.

## Goal

It is known, written down and checked by CI whether OpenIddict's server stack runs in a Native AOT-compiled ASP.NET Core app (Spec "prove OpenIddict runs under Native AOT", issue #1, ADR-016).

## Why

ADR-016 aims MagicLink.NET at Native AOT but holds the requirement back until a proof shows OpenIddict's server works under it. OpenIddict 7 marks its assemblies AOT-compatible, but its author had not tested the server stack in an AOT app. The answer decides whether later phases add an AOT publish check to CI and ship the container image as a native binary.

## Approaches

1. **A proof app published with `PublishAot`, the same flow tests run against it in-process and as a native binary in CI.** (chosen) The proof is repeatable, every later OpenIddict upgrade re-runs it, and the CI job is the AOT check the product-line standards ask for.
2. **A one-off manual publish, with the result written down.** Cheapest, but the answer goes stale with the next OpenIddict or SDK release and nothing enforces it.
3. **Make `src/MagicLink.NET` itself the AOT app.** The package has no host yet; phases 1.3 and 1.4 build it. Proving against code that does not exist yet would mean designing those phases now.

## Approach

**Proof app.** A new project `samples/MagicLink.NET.AotProof/`: an ASP.NET Core app built with `WebApplication.CreateSlimBuilder`, `PublishAot=true`, not packable, in the solution under `/samples/`. It references OpenIddict 7.9.0 (`OpenIddict.Core`, `OpenIddict.Server.AspNetCore`, `OpenIddict.Validation.AspNetCore`, `OpenIddict.Validation.ServerIntegration`) and runs the full stack, not degraded mode:

- **Core with a store.** OpenIddict's application manager runs on a small in-memory `IOpenIddictApplicationStore` that the proof owns, with one confidential client and one public client. Token, authorization and scope storage are switched off (`DisableTokenStorage`, `DisableAuthorizationStorage`, `DisableScopeValidation`), so no other store is needed. The real PostgreSQL store is Phase 1.3; this in-memory one exists only for the proof.
- **Server.** Discovery, JWKS, the authorization endpoint with passthrough, and the token endpoint. Flows: authorization code with PKCE, refresh token, client credentials. The authorization endpoint signs in a fixed test subject at once, without a sign-in page, since the sign-in page is Phase 1.7. Ephemeral signing and encryption keys. The transport security requirement is switched off so CI can call the native binary over plain HTTP; this is a proof-only setting.
- **Validation.** A protected endpoint `/api/me` uses OpenIddict validation with the local server integration and returns the token's subject.

**Flow tests.** A new test project `tests/MagicLink.NET.AotProof.Tests/` drives the flows over HTTP: discovery lists the endpoints, JWKS has a key, the code flow with PKCE returns access and refresh tokens, the access token opens `/api/me`, the refresh grant returns a new access token, client credentials works, a wrong client secret is refused, and `/api/me` refuses a call without a token. The test fixture hosts the proof app in-process through `WebApplicationFactory`; when the environment variable `MAGICLINK_AOT_PROOF_URL` is set it instead points an `HttpClient` at that URL. The same tests therefore run in `build-test` against the JIT app and in the new CI job against the native binary.

**CI job `aot-proof`.** In `ci.yml`: publish the proof app with `dotnet publish -c Release -r linux-x64`. Warnings are errors in this repo, so any trim or AOT warning fails the publish. The job then starts the native binary, waits until discovery answers, runs the flow tests with `MAGICLINK_AOT_PROOF_URL` set, and stops the binary. Adding it to the `Main` ruleset's required checks is the operator's call; the PR says so.

**The written result.** The Spec gets a `## Result` section: yes or no, the OpenIddict and SDK versions, which flows were proven, what was not covered, and the warnings found if any. ADR-016 stays as decided; the Spec links the CI job as the standing check. If the proof fails, the result says where, the CI job stays out of the required set, and the PR names the next step instead of hiding the failure.

## Files / Modules Touched

- `samples/MagicLink.NET.AotProof/` — new proof app: `Program.cs`, the in-memory application store, csproj
- `tests/MagicLink.NET.AotProof.Tests/` — new flow tests and fixture
- `MagicLink.NET.slnx` — add both projects
- `Directory.Packages.props` — OpenIddict 7.9.0 packages, `Microsoft.AspNetCore.Mvc.Testing`
- `.github/workflows/ci.yml` — new `aot-proof` job
- `docs/specs/magiclink-net-prove-openiddict-runs-under-native-aot.md` — `## Result`
- `README.md` — one line on where the AOT proof lives, if the README lists layout

## Tests

- Integration: the flow tests above, in-process in `build-test`, against the native binary in `aot-proof`.
- Unit: the in-memory application store's lookups by client id and the client secret check.
- Regression: the `aot-proof` job re-runs on every PR, so an OpenIddict or SDK upgrade that breaks AOT turns CI red.

## Not covered by this proof

- The device authorization flow. In OpenIddict it needs token storage, which arrives with the PostgreSQL store in Phase 1.3. The result says so; the `aot-proof` job grows to cover it once that store exists.
- The PostgreSQL store under AOT, which is Phase 1.3's own concern (ZeroAlloc.ORM is source-generated).

## Open Questions

None. Making `aot-proof` a required check is left to the operator and named in the PR.

## Risks

| Risk | Impact | Mitigation |
|---|---|---|
| OpenIddict or ASP.NET Core emits trim or AOT warnings | Publish fails under warnings-as-errors | That is a finding, not a build problem: record each warning in the result. Suppress nothing to get green. |
| The binary publishes but fails at runtime, such as reflection-based JSON | Flow tests fail against the native binary | The tests show which flow fails; record it as the result. |
| The proof app is counted in coverage | Coverage gate shifts | `coverage.config.xml` excludes samples; confirm the module path matches, and test the proof app well anyway. |
| Native publish is slow on the runner | Longer CI | Separate job, runs in parallel with `build-test`. |
