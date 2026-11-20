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

None yet.

## Tasks

* https://github.com/MarcelRoozekrans/MagicLink.NET/issues/6
