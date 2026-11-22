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
3. A CLI can sign a person in with the OAuth device authorization grant: it receives a user code, the person enters it in a browser and signs in there with the link or the code, and the CLI then receives its tokens.

A criterion for the Themis.NET CLI plugin was removed on 2026-11-20, because that plugin does not exist yet and the criterion could not be checked.

## Open questions

* The device flow gets its own criterion (3) here, not through the Themis.NET Specs (Marcel, 2026-11-21).
* An application gets a refresh token when it asks for the `offline_access` scope and its registration allows the refresh token grant. Each refresh gives a new refresh token. On every refresh MagicLink.NET asks the source of who may sign in again, as it does on every authorization: a person taken out of the source gets `invalid_grant`, and a person moved to other groups gets tokens with the new groups. A refresh may ask for fewer scopes than its refresh token holds; the email address then stays out of the new access and identity tokens, while the new refresh token keeps the scopes of the old one. A refresh token lasts OpenIddict's default of 14 days; the host can change that in the `AddMagicLinkServer` callback with `SetRefreshTokenLifetime`. Written down while building issue #10.
* A CLI asks for a device code at `/connect/device` and tells the person to open `/connect/verify`. That page needs a session at MagicLink.NET: without one the person first signs in with the link or the code from the email, and comes back to the page. The page shows who the person is signed in as and asks for the user code; only pressing its button approves the device. Opening the page with the user code in its address (`verification_uri_complete`) fills the code in but approves nothing, so a link from someone else cannot sign their device in as this person. MagicLink.NET asks the source of who may sign in when the person approves, and again when the CLI collects its tokens: a person taken out of the source in between gets `access_denied` on the CLI, and a person moved to other groups gets tokens with the new groups. A person refused at approval also loses their session, as at the authorization endpoint, and the CLI gets `access_denied` on its next poll instead of waiting for the code to expire. The application registration needs the device authorization endpoint, the token endpoint and the device code grant; with the refresh token grant and the `offline_access` scope the CLI also gets a refresh token. Written down while building issue #24.

## Tasks

* https://github.com/MarcelRoozekrans/MagicLink.NET/issues/2
* https://github.com/MarcelRoozekrans/MagicLink.NET/issues/3
* https://github.com/MarcelRoozekrans/MagicLink.NET/issues/10
* https://github.com/MarcelRoozekrans/MagicLink.NET/issues/24
