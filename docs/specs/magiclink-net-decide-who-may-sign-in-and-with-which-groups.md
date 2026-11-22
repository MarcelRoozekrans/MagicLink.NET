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

* *Should MagicLink.NET fetch the file from a git repository itself?* The first version reads the file from a path: a checkout of the repository, or a file mounted from it, such as a Kubernetes ConfigMap that a GitOps tool keeps up to date. It reads the file again when it changes, so a new commit applies without a restart. Cloning and pulling a repository would need credentials for it and a git client in the process. Written down while building issue #7.
* *How does an operator run the test command?* `CheckSignInCommand.TryRunAsync` is public: an app that hosts MagicLink.NET runs it on its built host when its arguments start with `check-sign-in`, and the container image does that itself, so `docker run magiclink-net check-sign-in ann@example.com` answers from the source in its settings without starting the server. Settled while merging issue #9 after issues #11 and #12.

## Decided while building

* The query of the database source gets the address as the parameter `@email` and returns one text column, a row per group. A row whose group is `NULL` lets the person in without that group; no row means the address may not sign in. The account it runs as may only read; that is what keeps the database unchanged. MagicLink.NET also runs the query in a transaction it always rolls back, which cleans up after a query that writes by mistake but cannot undo a COMMIT inside the query. An error in any statement of the query keeps everyone out. Issue #8.
* The startup check of criterion 4 also refuses a query that returns other than one text column or more than one result, and one that lets the made-up address in: that query ignores `@email` and would let anyone sign in. Issue #8.
* The test command is `check-sign-in <email address>`. It runs the start check of the source in use, then asks the source as a real sign-in would, and sends no email. It prints whether the address may sign in and its groups, and exits with 0 when it may, 1 when it may not, and 2 when that cannot be told. Issue #9.

## Tasks

* https://github.com/MarcelRoozekrans/MagicLink.NET/issues/7
* https://github.com/MarcelRoozekrans/MagicLink.NET/issues/8
* https://github.com/MarcelRoozekrans/MagicLink.NET/issues/9
