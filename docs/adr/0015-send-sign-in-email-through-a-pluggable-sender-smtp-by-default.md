# ADR-015: Send sign-in email through a pluggable sender, SMTP by default

## Status

accepted (2026-11-20)

## Context

MagicLink.NET sends every sign-in as an email with a link and a short code ([ADR-011](0011-confirm-magic-link-sign-in-with-a-button-or-a-short-code.md)). Organizations send mail in different ways: their own SMTP server, a mail service with an API, or nothing at all in a test setup.

Options considered:

* Only SMTP settings.
* A pluggable sender, with SMTP as the default implementation.

## Decision

* Email goes out through a pluggable sender, registered with `services.Add...`. Its name is not fixed yet.
* The first version ships an SMTP implementation as the default.

## Consequences

* An organization with an SMTP server only fills in settings.
* A mail service with an API, or a sender that only logs in tests, can be added without changing MagicLink.NET.
* The sender must be quick or asynchronous, since the person waits for the email. Sending happens in the background inside the process, with a few retries and then a log entry, and without a lasting queue. That also keeps the answer equally fast for known and unknown addresses.
