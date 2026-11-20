# MagicLink.NET: run an OIDC provider for applications and CLIs

Status: agreed (2026-11-20)

## Goal

An application that speaks OIDC, in the browser or as a CLI, signs a person in through MagicLink.NET and keeps the session alive without a new email.

## Context

* Why a separate OIDC provider: [ADR-009](../adr/0009-offer-magic-link-sign-in-as-a-separate-oidc-provider.md). Built on OpenIddict: [ADR-010](../adr/0010-build-magiclink-net-on-openiddict.md). Own store on PostgreSQL: [ADR-016](../adr/0016-aim-for-native-aot-with-an-own-store-on-postgresql.md).
* First user: the CLI plugin of Themis.NET, which signs in with a device code.
* How the person proves who they are (link, code) is in [confirm sign-in with a link or a code](magiclink-net-confirm-sign-in-with-a-link-or-a-code.md).

## Scope

**In**

* An OIDC provider on OpenIddict, for browser applications and for CLIs through the device flow.
* Refresh tokens, so a session can be extended without a new email.
* Its own OpenIddict store on PostgreSQL, without EF Core.

**Out**

* Passwords, passkeys, social sign-in, and sign-in through messaging apps.
* Client credentials (a service signing in to another service without a person): later, in its own small Spec.

## Acceptance criteria

1. An application that speaks OIDC can sign a person in through MagicLink.NET in the browser.
2. An application can extend a session with a refresh token, without the person requesting a new email.

A criterion for the Themis.NET CLI plugin was removed on 2026-11-20, because that plugin does not exist yet and the criterion could not be checked.

## Open questions

* *The device flow for CLIs stays in scope but has no acceptance criterion now. Is it checked later through the Themis.NET Specs, or does it get its own criterion here?*

## Tasks

* https://github.com/MarcelRoozekrans/MagicLink.NET/issues/2
* https://github.com/MarcelRoozekrans/MagicLink.NET/issues/3
* https://github.com/MarcelRoozekrans/MagicLink.NET/issues/10
