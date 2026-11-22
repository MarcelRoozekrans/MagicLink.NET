# MagicLink.NET: send the sign-in email through a pluggable sender

Status: agreed (2026-11-20)

## Goal

MagicLink.NET sends the sign-in email through SMTP by default, or through a sender the operator replaces it with, without slowing the answer to the person.

## Context

* Decision: [ADR-015](../adr/0015-send-sign-in-email-through-a-pluggable-sender-smtp-by-default.md).

## Scope

**In**

* A pluggable mail sender, with SMTP as the default, over STARTTLS or TLS from the first byte.
* Sending in the background, inside the process, with a few retries.

**Out**

* A lasting queue: after a restart an unsent email is lost.

## Acceptance criteria

1. Mail goes out over SMTP by setting alone, and an own sender can replace it without changing MagicLink.NET.
2. The sign-in email is sent in the background, inside the process, so the answer takes as long for a known address as for an unknown one. A failed send is retried a few times and then logged. There is no lasting queue: after a restart an unsent email is lost and the person requests a new one.
3. The SMTP sender connects with STARTTLS, on port 587 or 25, or with TLS from the first byte, on port 465, as set by `Security` in `MagicLinkSmtpOptions`. It never falls back to an unencrypted connection unless `Security` is `None`. Added on 2026-11-22, issue #54.

## Open questions

* *How many sign-ins may be started, per address and per client?* Settled. Per address by issue #39, in criterion 7 of [confirm sign-in with a link or a code](magiclink-net-confirm-sign-in-with-a-link-or-a-code.md): one address gets at most 5 sign-in emails in a sliding 15 minutes, set by `MaxEmailsPerAddress` and `EmailLimitWindow` in `MagicLinkOptions`. Per client by issue #55, in criterion 8 there: one client IP address starts at most 20 sign-ins in a sliding 15 minutes, set by `MaxSignInsPerClient` and `ClientLimitWindow`. Both limits are counted in memory, per process: with several processes behind a load balancer, each one allows that many. Beyond a limit the person sees the same answer and no email goes out. The queue still holds at most 1,000 unsent emails and drops and logs any beyond that. Written down while building issue #4 and again for issue #6; the per-address part settled on 2026-11-21, the per-client part on 2026-11-22.
* *Does SMTP need TLS from the first byte, on port 465?* Settled on 2026-11-22 by criterion 3: the SMTP sender moves from the SmtpClient that comes with .NET, which supports only STARTTLS, to MailKit, which handles both ([ADR-015](../adr/0015-send-sign-in-email-through-a-pluggable-sender-smtp-by-default.md), amended 2026-11-22).

## Tasks

* https://github.com/MarcelRoozekrans/MagicLink.NET/issues/6
* https://github.com/MarcelRoozekrans/MagicLink.NET/issues/54
