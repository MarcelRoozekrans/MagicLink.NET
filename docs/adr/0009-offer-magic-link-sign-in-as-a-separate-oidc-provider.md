# ADR-009: Offer magic-link sign-in as a separate OIDC provider

## Status

accepted (2026-11-15)

## Context

The products in this product line sign people in through the organization's identity provider over OpenID Connect (OIDC) (ADR-005), and "one identity" is one of the ways the products integrate (ADR-008). Both are product-line decisions and are not published in this repository. ADR-005 left a magic link (a sign-in link sent by email) open as a "maybe" for organizations without an identity provider. Most of the packages in the product line already support OIDC.

Options considered:

* A small identity provider that signs people in with a magic link and speaks OIDC to everything else.
* A magic-link module that each product takes in as a second sign-in method next to OIDC. Simpler per product, but every product would have two sign-in routes and need to send email, which breaks "one identity".

## Decision

* Magic-link sign-in is offered as a small identity provider that speaks OIDC, including the device-code flow.
* It is a separate product, usable on its own by any application that speaks OIDC: MagicLink.NET.
* The other products keep speaking OIDC only; they do not get a magic-link login of their own.

## Consequences

* Products need no change to support organizations without an identity provider.
* Only one product sends sign-in email.
* Building an OIDC provider is real work (tokens, keys, the device-code flow). It is built on OpenIddict rather than from scratch: see [ADR-010](0010-build-magiclink-net-on-openiddict.md).
* For people who sign in this way, teams and roles must come from groups kept in this provider (see [ADR-014](0014-look-up-who-may-sign-in-and-their-groups-through-a-pluggable-source.md)).
