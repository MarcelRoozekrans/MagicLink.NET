# ADR-010: Build MagicLink.NET on OpenIddict

## Status

accepted (2026-11-15)

## Context

MagicLink.NET is a separate product that signs people in with a magic link and speaks OIDC to the other products, including the device-code flow for command-line tools ([ADR-009](0009-offer-magic-link-sign-in-as-a-separate-oidc-provider.md)). The question was whether to build an OIDC provider from scratch or start from an existing one. Found on 2026-11-15 (web search, not verified in depth):

* OpenIddict: Apache-licensed, supports the device authorization flow, and is a framework in which the application writes its own sign-in and authorization endpoints.
* Duende IdentityServer: a paid licence in production; the free Community Edition is not meant for software built for others to host.
* Open.IdentityServer: a fork of IdentityServer4, announced in June 2026; maturity not checked.
* Building from scratch: full control, but tokens, keys and the device-code flow are real work.
* Added on 2026-11-20: an existing open-source provider on OpenIddict (MPL-2.0) already offers passwordless sign-in by email and messaging apps. Using it, with MagicLink.NET as a thin layer on top, would save work but tie sign-in to its choices and its much larger scope.

## Decision

* MagicLink.NET is built on OpenIddict.
* MagicLink.NET is built from its own code; it does not build on that existing provider (2026-11-20).

## Consequences

* The licence fits an open-source product under MIT ([ADR-219](0219-license-magiclink-net-under-mit.md)).
* The device-code flow comes with OpenIddict; MagicLink.NET writes the sign-in by magic link, the email sending and the groups for teams and roles.
* MagicLink.NET depends on OpenIddict's release cycle and support.
* MagicLink.NET stays small and usable on its own, at the cost of rebuilding part of what the existing provider already does.
* Native AOT: OpenIddict 7.0 marks its assemblies as trimming- and AOT-compatible on .NET 9 and higher, but its Entity Framework Core stores do not work under Native AOT yet (found 2026-11-15). Decided later in [ADR-016](0016-aim-for-native-aot-with-an-own-store-on-postgresql.md).
