# Phase 1.2 — Native AOT proof for OpenIddict Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A proof app runs OpenIddict's server and validation stack, its flow tests pass in-process and against the Native AOT binary in CI, and the result is written in the Spec.

**Architecture:** `samples/MagicLink.NET.AotProof` is a slim ASP.NET Core app with `PublishAot`, OpenIddict core on an in-memory application store, the server (code + PKCE, refresh, client credentials) and local validation. `tests/MagicLink.NET.AotProof.Tests` drives the flows over HTTP, in-process or against `MAGICLINK_AOT_PROOF_URL`.

**Tech Stack:** .NET 10, ASP.NET Core, OpenIddict 7.9.0, xUnit v3 on Microsoft.Testing.Platform, `Microsoft.AspNetCore.Mvc.Testing`.

**Spec:** `docs/superpowers/specs/2026-11-20-phase-1-2-aot-proof-design.md`, from `docs/specs/magiclink-net-prove-openiddict-runs-under-native-aot.md` and ADR-016.

## Global Constraints

- Target `net10.0` (from `Directory.Build.props`); package versions only in `Directory.Packages.props`.
- Warnings are errors; no suppression of trim or AOT warnings to get green.
- The proof app is not packable (`IsPackable=false`); CI's `pack` packs the whole solution.
- Proof-only settings (transport security off, ephemeral keys, fixed test subject) stay in the sample, never in `src/`.
- Conventional commits, no nested parentheses in commit bodies.

## Review Focus

- A token request with a wrong client secret must be refused with `invalid_client`, not issued a token — test in Task 2.
- `/api/me` without a token, or with a garbage token, must answer 401 — test in Task 2.
- A code redeemed with the wrong PKCE verifier must be refused with `invalid_grant` — test in Task 2.
- An unknown `client_id` at the authorization endpoint must not issue a code — test in Task 2.
- The in-memory store must compare client secrets in constant time and not return a client for a null or empty id — test in Task 1.

---

### Task 1: Proof app with OpenIddict and its in-memory application store

**Files:**
- Create: `samples/MagicLink.NET.AotProof/MagicLink.NET.AotProof.csproj` (`Microsoft.NET.Sdk.Web`, `PublishAot=true`, `IsPackable=false`, `InvariantGlobalization=true`)
- Create: `samples/MagicLink.NET.AotProof/Program.cs`
- Create: `samples/MagicLink.NET.AotProof/InMemoryApplicationStore.cs`, `ProofApplication.cs`
- Create: `tests/MagicLink.NET.AotProof.Tests/MagicLink.NET.AotProof.Tests.csproj`, `InMemoryApplicationStoreTests.cs`
- Modify: `MagicLink.NET.slnx` (add `/samples/` folder and the test project), `Directory.Packages.props`

**Interfaces:**
- Produces: `public partial class Program` (for `WebApplicationFactory<Program>`); clients `ProofClients.Confidential` = `proof-service` with secret `proof-secret`, `ProofClients.Public` = `proof-cli` with redirect URI `http://localhost/callback`; endpoints `/connect/authorize`, `/connect/token`, `/.well-known/openid-configuration`, `/.well-known/jwks`, `/api/me`.
- `InMemoryApplicationStore : IOpenIddictApplicationStore<ProofApplication>`; members not needed by the server throw `NotSupportedException`.

- [ ] Step 1: write `InMemoryApplicationStoreTests`: `FindByClientIdAsync("proof-service")` returns the confidential app; unknown, empty id returns null; the client type, redirect URIs and permissions read back as configured.
- [ ] Step 2: run `dotnet test --project tests/MagicLink.NET.AotProof.Tests` — fails to build.
- [ ] Step 3: implement the store, `ProofApplication`, and `Program.cs` wiring core (`ReplaceApplicationStore`/`SetDefaultApplicationEntity`), server (endpoints, flows, PKCE required, ephemeral keys, `DisableTokenStorage`, `DisableAuthorizationStorage`, `DisableScopeValidation`, ASP.NET Core passthrough for authorize, transport security off), validation (`UseLocalServer`, `UseAspNetCore`), the authorize handler signing in subject `proof-user`, and `/api/me`. Secret check: the default OpenIddict manager hashes and verifies secrets; the store keeps the hash produced at startup.
- [ ] Step 4: tests pass; `dotnet build -c Release` clean.
- [ ] Step 5: commit `test(aot): add OpenIddict proof app with an in-memory application store`.

### Task 2: Flow tests, in-process or against a URL

**Files:**
- Create: `tests/MagicLink.NET.AotProof.Tests/ProofHost.cs` (fixture: `HttpClient` from `WebApplicationFactory<Program>` or from `MAGICLINK_AOT_PROOF_URL`)
- Create: `tests/MagicLink.NET.AotProof.Tests/FlowTests.cs`

**Interfaces:**
- Consumes: Task 1 clients and endpoints.

- [ ] Step 1: write tests: `Discovery_lists_endpoints` (token, authorization, jwks URIs present; `code` and `S256` advertised); `Jwks_has_a_signing_key`; `Code_flow_with_pkce_returns_tokens_that_open_api_me` (`/api/me` returns `proof-user`); `Refresh_grant_returns_a_new_access_token`; `Client_credentials_returns_a_token_that_opens_api_me`; `Wrong_client_secret_is_refused` (`invalid_client`); `Wrong_pkce_verifier_is_refused` (`invalid_grant`); `Unknown_client_gets_no_code`; `Api_me_without_or_with_garbage_token_is_401`.
- [ ] Step 2: run; fix the proof app until all pass in-process.
- [ ] Step 3: `dotnet publish samples/MagicLink.NET.AotProof -c Release -r linux-x64 -o artifacts/aot` with zero warnings; start the binary on `http://127.0.0.1:5099`; run the tests with `MAGICLINK_AOT_PROOF_URL=http://127.0.0.1:5099`; record each warning or failure.
- [ ] Step 4: commit `test(aot): drive the OpenIddict flows in-process or against a URL`.

### Task 3: CI job and the written result

**Files:**
- Modify: `.github/workflows/ci.yml` (job `aot-proof`)
- Modify: `docs/specs/magiclink-net-prove-openiddict-runs-under-native-aot.md` (`## Result`)
- Modify: `README.md` if it lists the layout

- [ ] Step 1: add job `aot-proof`: checkout, setup-dotnet from `global.json`, publish as in Task 2 Step 3, start the binary in the background, wait up to 60 s for discovery, run the flow tests with `MAGICLINK_AOT_PROOF_URL`, print the binary's log on failure.
- [ ] Step 2: write `## Result` in the Spec: yes or no, OpenIddict 7.9.0 and SDK version, proven flows, not covered (device flow, PostgreSQL store), warnings found, the CI job as the standing check.
- [ ] Step 3: `dotnet format whitespace . --folder --verify-no-changes`, `dotnet build -c Release`, `dotnet test` pass.
- [ ] Step 4: commit `ci(aot): publish the proof app as Native AOT and run the flow tests on it`, then `docs(spec): write down the Native AOT proof result`.

## Checks

- build: `dotnet build -c Release`
- tests: `dotnet test`
