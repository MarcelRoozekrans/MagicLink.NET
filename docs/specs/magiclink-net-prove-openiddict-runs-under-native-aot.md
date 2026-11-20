# MagicLink.NET: prove OpenIddict runs under Native AOT

Status: agreed (2026-11-20)

## Goal

It is known, and written down, whether OpenIddict's server stack runs under Native AOT.

## Context

* Decision: [ADR-016](../adr/0016-aim-for-native-aot-with-an-own-store-on-postgresql.md), which aims for Native AOT without making it a requirement.

## Scope

**In**

* A short proof that OpenIddict's server stack runs under Native AOT.

**Out**

* Native AOT as a hard requirement, until the proof succeeds.

## Acceptance criteria

1. The result of the Native AOT proof is written down: does OpenIddict's server stack run under AOT, yes or no.

## Open questions

None yet.

## Tasks

* https://github.com/MarcelRoozekrans/MagicLink.NET/issues/1
