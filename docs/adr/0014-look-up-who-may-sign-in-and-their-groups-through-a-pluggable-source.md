# ADR-014: Look up who may sign in, and their groups, through a pluggable source

## Status

accepted (2026-11-20)

## Context

MagicLink.NET is for organizations without an identity provider. The products behind it take teams and roles from the `groups` claim of the identity provider (ADR-005, a product-line decision not published here). So on every sign-in MagicLink.NET must answer two questions about an email address: may it sign in at all, and which groups is it in? Without the first, anyone with a mailbox gets a session.

The question was whether this could be coupled to an existing database. Options considered:

* Groups kept in a file in git.
* Groups managed in MagicLink.NET itself, with its own admin page and database.
* No groups; each product assigns roles itself.
* A pluggable source behind one interface, with ready-made implementations.

## Decision

* One pluggable source answers both questions for an email address: whether it may sign in, and its groups. It is registered with `services.Add...`. Its name is not fixed yet.
* The first version ships two implementations:
  * **An existing database:** a connection string and a query that returns the groups for an email address. No row means the address may not sign in. Plain ADO.NET, so Native AOT stays possible.
  * **A file in git:** for organizations without a user database of their own.
* Anyone can write their own implementation against the same interface (an API, LDAP).
* MagicLink.NET only reads from an existing database. It never creates or changes users there; the existing application stays their owner.
* The groups go into the `groups` claim of the token.

## Consequences

* Products behind MagicLink.NET need no change: they see the same `groups` claim as from any other identity provider.
* An organization can reuse the user list it already has, without moving it.
* MagicLink.NET has no admin page for users or groups; changes happen in the database or in git.
* The first version supports PostgreSQL and SQL Server for the existing database. Other engines can be added later as a source of their own.
* A wrong query can lock everyone out or let too many in. MagicLink.NET runs the query once at startup and refuses to start if it fails, and a test command shows for a given address whether it may sign in and with which groups.
