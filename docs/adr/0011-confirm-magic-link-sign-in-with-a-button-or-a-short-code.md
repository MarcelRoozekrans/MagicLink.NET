# ADR-011: Confirm magic-link sign-in with a button or a short code

## Status

accepted (2026-11-20)

## Context

MagicLink.NET signs people in with a link sent by email ([ADR-009](0009-offer-magic-link-sign-in-as-a-separate-oidc-provider.md), [ADR-010](0010-build-magiclink-net-on-openiddict.md)). Email clients and security scanners often open links before the person does. A link that signs in as soon as it is opened is then used up, or signs in the wrong party. One vendor deprecated its magic-link product for this reason.

Options considered:

* A confirm step: the link opens a page, and only pressing a button (a POST) signs in. Scanners open links but do not press buttons.
* A short code in the email that the person types on the screen where they are signing in. Works across devices; needs protection against guessing.
* Both.

## Decision

* The email holds both a link and a short code. The link opens a page with a sign-in button; only pressing it signs the person in. The code can be typed on the screen where sign-in started instead.
* A sign-in request is bound to the browser that started it. When the link is confirmed on another device, MagicLink.NET first asks the person to confirm it was them.
* Link and code belong to one sign-in request and both become invalid once either is used.

## Consequences

* Opening the link alone never signs anyone in, so scanners cannot use it up.
* People who read mail on their phone and sign in on a laptop can use the code.
* The code needs a short lifetime and a limit on attempts: a code is valid for 10 minutes and allows 5 attempts (decided 2026-11-20).
* The device flow for command-line tools is not affected: OpenIddict already lets the person enter a user code in the browser, and MagicLink.NET only handles the browser sign-in behind it.
