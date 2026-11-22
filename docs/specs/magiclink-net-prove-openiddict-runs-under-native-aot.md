# MagicLink.NET: prove OpenIddict runs under Native AOT

Status: agreed (2026-11-20)

## Goal

It is known, and written down, whether OpenIddict's server stack runs under Native AOT.

## Context

* Decision: [ADR-016](../adr/0016-aim-for-native-aot-with-an-own-store-on-postgresql.md), which aims for Native AOT without making it a requirement.

## Scope

**In**

* A short proof that OpenIddict's server stack runs under Native AOT.

**Out**

* Native AOT as a hard requirement, until the proof succeeds.

## Acceptance criteria

1. The result of the Native AOT proof is written down: does OpenIddict's server stack run under AOT, yes or no.

## Result

**Yes: OpenIddict's server stack runs under Native AOT** (2026-11-20, OpenIddict 7.9.0, .NET SDK 10.0.403, linux-x64).

* The proof app `samples/MagicLink.NET.AotProof` publishes with `PublishAot` and no trim or AOT warning; warnings are errors in this repository, so a warning would fail the publish.
* It runs OpenIddict core, the server and the validation stack, not the degraded mode. With `MAGICLINK_AOT_PROOF_POSTGRES` set it runs on the own PostgreSQL store, `MagicLink.NET.PostgreSql` (issue #2), with token and authorization storage; without it, on an in-memory application store.
* The flow tests in `tests/MagicLink.NET.AotProof.Tests` pass against the native binary on PostgreSQL: discovery, the key set, the authorization code flow with PKCE, the refresh token grant, the client credentials grant, the device authorization flow, and a protected endpoint that validates the access token. Refusals hold too: a wrong client secret, a wrong PKCE verifier, an unknown client, an unknown user code, a device code redeemed twice, and a call without a valid token.
* Standing check: the `aot-proof` job in `.github/workflows/ci.yml` publishes the proof app as a native binary, starts it on a PostgreSQL service and runs the flow tests against it on every pull request, so an OpenIddict or SDK upgrade that breaks Native AOT turns CI red.

Since issue #11 the proof app hosts the core, `MagicLink.NET`, through its public hosting API, so the native binary runs MagicLink.NET itself and not only OpenIddict. Its flow tests now sign a person in through the core's sign-in page with the code from the email, and cover discovery, the key set, the authorization code flow with PKCE, the refresh token grant, the groups from the source in the access token, an API of the host that checks the token, and the refusals of a wrong PKCE verifier, an unknown client and a call without a valid token. The in-memory store, the client credentials grant and the proof-only device flow went with the move; see the update of 2026-11-22 in ADR-016.

## Open questions

None yet.

## Tasks

* https://github.com/MarcelRoozekrans/MagicLink.NET/issues/1
