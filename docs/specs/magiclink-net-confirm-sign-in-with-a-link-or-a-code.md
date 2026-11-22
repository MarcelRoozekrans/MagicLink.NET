# MagicLink.NET: confirm sign-in with a link or a code

Status: agreed (2026-11-20)

## Goal

A person confirms a sign-in by pressing a button on a page the email links to, or by typing a short code where the sign-in started.

## Context

* Decision: [ADR-011](../adr/0011-confirm-magic-link-sign-in-with-a-button-or-a-short-code.md).
* The provider that issues the tokens is in [run an OIDC provider for applications and CLIs](magiclink-net-run-an-oidc-provider-for-applications-and-clis.md).

## Scope

**In**

* A link in the email that opens a page with a sign-in button.
* A short code that can be typed where sign-in started.
* The same answer for a known and an unknown email address.
* A limit on how many sign-in emails one address gets.

**Out**

* Passwords, passkeys, social sign-in, and sign-in through messaging apps.

## Acceptance criteria

1. Opening the link in the email never signs anyone in; only pressing the button on the page does.
2. Typing the code where sign-in started signs the person in, also when the email was read on another device.
3. Link and code belong to one sign-in request and stop working once either is used. A code is valid for 10 minutes and allows 5 attempts.
4. An email address the source does not know gets no session and no email. The person sees the same message as for a known address ("if this address is known, you will receive an email"), so nobody can find out which addresses exist. MagicLink.NET logs the refusal for the operator.
5. A sign-in request is bound to the browser that started it: confirming in that browser completes the sign-in there.
6. Opening the link on another device or in another browser never signs anyone in; the page tells the person to type the code where sign-in started ([ADR-249](../adr/0249-confirm-the-sign-in-link-only-in-the-browser-that-started-the-sign-in.md)).
7. One address gets at most 5 sign-in emails in a sliding 15 minutes, so nobody can flood a person's mailbox by starting sign-ins for their address. Beyond that the person sees the same answer, but no email goes out, and MagicLink.NET logs it. The limit is counted in memory, per process. Added on 2026-11-21, issue #39.

## Open questions

None yet.

## Tasks

* https://github.com/MarcelRoozekrans/MagicLink.NET/issues/4
* https://github.com/MarcelRoozekrans/MagicLink.NET/issues/5
* https://github.com/MarcelRoozekrans/MagicLink.NET/issues/39
