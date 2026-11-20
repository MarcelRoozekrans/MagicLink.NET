# MagicLink.NET: decide who may sign in and with which groups

Status: agreed (2026-11-20)

## Goal

MagicLink.NET looks up whether an email address may sign in and which groups the person has, and puts those groups in the token.

## Context

* Decision: [ADR-014](../adr/0014-look-up-who-may-sign-in-and-their-groups-through-a-pluggable-source.md).
* Teams and roles in the products come from these groups ([ADR-009](../adr/0009-offer-magic-link-sign-in-as-a-separate-oidc-provider.md)).

## Scope

**In**

* A pluggable source that decides who may sign in and returns their groups, with two implementations: a read-only query on an existing PostgreSQL or SQL Server database, and a file in git.
* Groups in the `groups` claim of the token.
* A startup check of the configured query and a command to test an address.

**Out**

* A page to manage users or groups, and any writing to the user database.
* Database engines other than PostgreSQL and SQL Server for the user database.

## Acceptance criteria

1. The groups the source returns appear in the `groups` claim of the token.
2. The database source works with an account that may only read.
3. The database source works with PostgreSQL and with SQL Server.
4. At startup MagicLink.NET runs the configured query once with a dummy address. If that fails, for example through a syntax error or a missing column, MagicLink.NET does not start and says why.
5. A test command takes an email address and shows whether it may sign in and with which groups, for both the database source and the git file, without sending an email.

## Open questions

None yet.

## Tasks

* https://github.com/MarcelRoozekrans/MagicLink.NET/issues/7
* https://github.com/MarcelRoozekrans/MagicLink.NET/issues/8
* https://github.com/MarcelRoozekrans/MagicLink.NET/issues/9
