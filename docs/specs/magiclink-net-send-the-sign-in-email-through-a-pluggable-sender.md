# MagicLink.NET: send the sign-in email through a pluggable sender

Status: agreed (2026-11-20)

## Goal

MagicLink.NET sends the sign-in email through SMTP by default, or through a sender the operator replaces it with, without slowing the answer to the person.

## Context

* Decision: [ADR-015](../adr/0015-send-sign-in-email-through-a-pluggable-sender-smtp-by-default.md).

## Scope

**In**

* A pluggable mail sender, with SMTP as the default.
* Sending in the background, inside the process, with a few retries.

**Out**

* A lasting queue: after a restart an unsent email is lost.

## Acceptance criteria

1. Mail goes out over SMTP by setting alone, and an own sender can replace it without changing MagicLink.NET.
2. The sign-in email is sent in the background, inside the process, so the answer takes as long for a known address as for an unknown one. A failed send is retried a few times and then logged. There is no lasting queue: after a restart an unsent email is lost and the person requests a new one.

## Open questions

* *How many sign-ins may be started, per address and per client?* Per address this is settled by issue #39, in criterion 7 of [confirm sign-in with a link or a code](magiclink-net-confirm-sign-in-with-a-link-or-a-code.md): one address gets at most 5 sign-in emails in a sliding 15 minutes, set by `MaxEmailsPerAddress` and `EmailLimitWindow` in `MagicLinkOptions`. The limit is counted in memory, per process: with several processes behind a load balancer, each one allows that many. Beyond that the person sees the same answer and no email goes out. Still open is a limit per client or per IP address, which would also bound sign-ins started for many different addresses. The queue still holds at most 1,000 unsent emails and drops and logs any beyond that. Written down while building issue #4 and again for issue #6; the per-address part settled on 2026-11-21.
* *Does SMTP need TLS from the first byte, on port 465?* The SMTP sender uses the SmtpClient that comes with .NET, which runs under Native AOT, but supports only STARTTLS, on port 587 or 25. A server that offers only port 465 needs an own sender, or a different SMTP client in a later version.

## Tasks

* https://github.com/MarcelRoozekrans/MagicLink.NET/issues/6
