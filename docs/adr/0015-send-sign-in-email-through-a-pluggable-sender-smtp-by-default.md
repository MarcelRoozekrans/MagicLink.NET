# ADR-015: Send sign-in email through a pluggable sender, SMTP by default

## Status

accepted (2026-11-20), amended (2026-11-22): the SMTP default is built on MailKit

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

## Amendment (2026-11-22)

The SMTP default is built on MailKit instead of the SmtpClient in `System.Net.Mail`. That SmtpClient supports only STARTTLS, on port 587 or 25, so a mail server that offers only TLS from the first byte, on port 465, needed an own sender. Microsoft also advises against that SmtpClient for new code. MailKit handles both STARTTLS and TLS from the first byte.

MagicLink.NET requires Native AOT ([ADR-016](0016-aim-for-native-aot-with-an-own-store-on-postgresql.md)), so MailKit has to pass the same checks as the rest: the `IsAotCompatible` build and the `aot-proof` job. A probe on 2026-11-22 published MailKit 5.0.0 under Native AOT without trim or AOT warnings and ran the native binary.

Consequences:

* MagicLink.NET depends on MailKit and MimeKit, both MIT licensed.
* The setting for the connection changes from `UseStartTls` to `Security` in `MagicLinkSmtpOptions`, with `StartTls` as the default, `ImplicitTls` and `None`. This breaks the public API before 1.0.
* The pluggable sender itself does not change: an own sender still replaces the SMTP default without changing MagicLink.NET.
