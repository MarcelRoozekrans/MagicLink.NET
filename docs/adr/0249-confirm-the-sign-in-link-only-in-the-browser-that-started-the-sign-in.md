# ADR-249: Confirm the sign-in link only in the browser that started the sign-in

## Status

accepted (2026-11-21). Supersedes the cross-device part of [ADR-011](0011-confirm-magic-link-sign-in-with-a-button-or-a-short-code.md).

## Context

ADR-011 binds a sign-in request to the browser that started it, and lets the link be confirmed on another device after an extra question ("was this sign-in yours?"). That leaves an attack open: someone starts a sign-in with a victim's email address in their own browser; the victim gets the email, presses the button on their phone, waves the extra question away, and the attacker's browser is signed in. People who read their mail on another device can already use the code, typed where sign-in started.

## Decision

* The link in the email signs in only when it is opened and confirmed in the browser that started the sign-in.
* Opened on another device or in another browser, the link never signs anyone in. The page says so and tells the person to type the code where sign-in started.
* The code keeps working as in ADR-011.

## Consequences

* The attack above is closed: a link confirmed in the victim's browser does nothing for the attacker's browser.
* The extra "was this sign-in yours?" question from ADR-011 is no longer needed.
* People who open the email on another device must use the code; the page on that device makes that clear.
