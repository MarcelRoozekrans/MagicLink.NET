# Issue #2 design — own OpenIddict store on PostgreSQL

**Date:** 2026-11-20
**Issue:** #2, from Spec [run an OIDC provider for applications and CLIs](../../specs/magiclink-net-run-an-oidc-provider-for-applications-and-clis.md)
**Status:** approved 2026-11-20

## Intent

OpenIddict keeps four kinds of data: applications (clients), authorizations, tokens and scopes. MagicLink.NET stores them itself in PostgreSQL, without Entity Framework Core, so the provider stays small and can ship as Native AOT (ADR-016). Issue #1 proved OpenIddict's server runs under AOT with an in-memory store; this issue gives it a real one.

## Goals

- OpenIddict runs against the own store for all four kinds of data (issue #2).
- The store has no dependency on EF Core (issue #2, ADR-016, product-line ADR-228).
- The store's tests run against a real PostgreSQL instance (issue #2).
- The store is Native AOT-compatible: trim and AOT warnings are errors, and the AOT proof runs on it.
- With token storage in place, the device flow works; the AOT proof's flow tests cover it.

## Non-goals

- The read-only database of who may sign in (ADR-014; issues #7 and #8).
- SQL Server or any other database for this store. Only PostgreSQL (ADR-016).
- The `services.AddMagicLink()` hosting API (issue #11). This issue registers the store on OpenIddict's own builder; issue #11 wraps it.
- Client credentials as a feature: out of scope in the Spec. The store does not block it, but nothing here is built for it.

## Anti-goals

- A store that works in tests but grows without bound in production: expired data must be pruned.
- A store that silently loses a concurrent update: OpenIddict's concurrency token must be enforced.
- Runtime reflection, or an EF Core reference anywhere in the dependency graph.

## Constraints

- Package ids `MagicLink.NET` and `MagicLink.NET.*` (ADR-012). Target `net10.0`. Versions only in `Directory.Packages.props`.
- Async members return `ValueTask` where OpenIddict's interface allows, take a `CancellationToken` and use `ConfigureAwait(false)` (ADR-226). The store interfaces are OpenIddict's, so their signatures are fixed.
- Public members need XML docs; warnings are errors; `IsAotCompatible` for everything under `src/`.
- Tests run against PostgreSQL 16, the version CI provides, through `MAGICLINK_TEST_POSTGRES`.

## Approaches

1. **ZeroAlloc.ORM repositories with hand-written SQL** (chosen). One generated `[Query]` or `[Command]` method per store operation; plain SQL migrations through ZeroAlloc.ORM's migration runner. Fits ADR-228 and the ZeroAlloc preference, AOT-safe through source generation.
2. **ZeroAlloc.ORM's compile-time LINQ.** Almost every query is a lookup by one key, as short in SQL; its grouping limits would push the harder queries back to SQL, so mixing adds nothing.
3. **Plain Npgsql without an ORM.** Ruled out by product-line ADR-228 and the ZeroAlloc preference in `CLAUDE.md`.
4. **OpenIddict's EF Core store.** Ruled out by ADR-016 and ADR-228.

## Decided with the operator

- **Separate adapter package.** `MagicLink.NET` stays free of database code; the store lives in a new package `MagicLink.NET.PostgreSql`, following product-line ADR-224. The delivery Spec, which says "a NuGet package, the core", is updated in the same PR to name the core and its PostgreSQL store package.
- **Pruning with ZeroAlloc.Scheduling.** Expired tokens and authorizations are removed by a recurring job on ZeroAlloc.Scheduling, with its ZeroAlloc.ORM job store on the same PostgreSQL, so several instances share one schedule and the job runs once per hour in total. It can be switched off. The interval is fixed at one hour, because ZeroAlloc.Scheduling fixes recurring intervals at compile time.
- **A named connection for the job store, fixed in ZeroAlloc.Scheduling.Orm.** `WithOrm()` today resolves the app's single unnamed `IAsyncDbConnection`, which would clash with a host that uses ZeroAlloc.ORM on its own database. ZeroAlloc.Scheduling.Orm gets a `WithOrm` overload that takes a connection key, as ZeroAlloc.Outbox.Orm already has, and a release. MagicLink then registers its own keyed connection. The store and its tests are built meanwhile; the pruning job lands once that release is out.
- **Migrations at startup, can be switched off.** On startup the store applies new `.sql` migrations under ZeroAlloc.ORM's PostgreSQL advisory lock. A setting switches this off where a DBA applies the scripts; the scripts ship in the package for that.
- **The `IQueryable` overloads throw.** OpenIddict's stores have `CountAsync`, `GetAsync` and `ListAsync` overloads that take an `IQueryable` query. ZeroAlloc.ORM has no runtime LINQ. OpenIddict's server never calls them; they throw `NotSupportedException` with a message that names the method, and the package README says so.

## Design

### Package and registration

`src/MagicLink.NET.PostgreSql/`, package id `MagicLink.NET.PostgreSql`, with `tests/MagicLink.NET.PostgreSql.Tests/`. Dependencies: OpenIddict.Core, Npgsql, ZeroAlloc.ORM (with its generator), ZeroAlloc.Scheduling with ZeroAlloc.Scheduling.Orm.

Registration on OpenIddict's core builder:

```csharp
services.AddOpenIddict()
    .AddCore(options => options.UsePostgreSql(store =>
    {
        store.ConnectionString = "...";       // required
        store.ApplyMigrationsOnStartup = true; // default
        store.PruneExpiredData = true;         // default
    }));
```

`UsePostgreSql` sets the four default entity types and replaces the four stores, registers an `NpgsqlDataSource` for the store (built with `NpgsqlSlimDataSourceBuilder` and `EnableArrays`, keyed so it cannot clash with the host's own data source), a hosted service that runs the migrations before the host starts serving, and the pruning job. A missing connection string fails at startup with a clear message (fail closed, product-line ADR-231).

### Schema

All tables live in schema `magiclink`, created by the first migration. Ids are `uuid`, generated by the store, exposed to OpenIddict as strings. Every table has `concurrency_token text not null`, a new random value on every update.

- `applications`: `client_id` unique, `client_secret` (OpenIddict's hash, never a plain secret), `client_type`, `application_type`, `consent_type`, `display_name`, `display_names jsonb`, `json_web_key_set jsonb`, `permissions text[]`, `post_logout_redirect_uris text[]`, `redirect_uris text[]`, `requirements text[]`, `settings jsonb`, `properties jsonb`.
- `authorizations`: `application_id` references `applications` with cascade delete, `subject`, `status`, `type`, `scopes text[]`, `creation_date timestamptz`, `properties jsonb`. Indexes on `(application_id, subject, status, type)`.
- `scopes`: `name` unique, `display_name`, `display_names jsonb`, `description`, `descriptions jsonb`, `resources text[]`, `properties jsonb`.
- `tokens`: `application_id` and `authorization_id` reference their tables with cascade delete, `subject`, `type`, `status`, `payload`, `reference_id` unique, `creation_date`, `expiration_date`, `redemption_date` (all `timestamptz`), `properties jsonb`. Indexes on `(application_id, subject, status, type)` and on `expiration_date` for pruning.

JSON columns are bound as `[Json] string`, serialized with a source-generated `JsonSerializerContext`. Localized names and descriptions are stored as a JSON object keyed by culture name.

### Stores

Four public entity classes (`MagicLinkApplication`, `MagicLinkAuthorization`, `MagicLinkScope`, `MagicLinkToken`) and four stores implementing `IOpenIddictApplicationStore<T>` and its siblings. Each store holds an internal `partial` ZeroAlloc.ORM repository with one method per operation.

- **Updates** run `UPDATE ... WHERE id = @id AND concurrency_token = @expected` and set a new token; zero rows affected throws OpenIddict's `ConcurrencyException`, which the managers expect.
- **Deletes** of an application or authorization remove its tokens and authorizations by cascade, as the EF Core store does.
- **Find and list** methods return `IAsyncEnumerable<T>` streamed from the database, with `[EnumeratorCancellation]`.
- **Revoke and prune** methods (`RevokeAsync`, `RevokeByAuthorizationIdAsync`, `RevokeBySubjectAsync` and the rest of OpenIddict 7.9.0's interface, `PruneAsync` on tokens and authorizations) are single SQL statements returning the row count.
- **Pruning rules** follow OpenIddict's EF Core store: tokens and authorizations older than the threshold that are not valid, or whose tokens have expired; ad-hoc authorizations with no valid token left.

### Pruning job

A `[Job(Every = Every.Hour)]` class in the package calls `IOpenIddictTokenManager.PruneAsync` and `IOpenIddictAuthorizationManager.PruneAsync` with a threshold of now minus 14 days, the same default as OpenIddict's Quartz integration. It is registered only when `PruneExpiredData` is true, which `UsePostgreSql` reads from its callback when it runs.

- **Connection.** The job store uses MagicLink's own keyed `IAsyncDbConnection`, built from the store's data source, through the keyed `WithOrm` overload. That data source sets `Search Path=magiclink`, so ZeroAlloc.Scheduling's `SchedulingJobs` table, which its migration creates without a schema, lands in the `magiclink` schema too.
- **Migrations.** ZeroAlloc.Scheduling.Orm's migration source runs in the same startup step as the store's own, under the store's advisory lock, and `ApplyMigrationsOnStartup = false` switches both off. Its SQL ships next to the store's scripts, under `sql/scheduling/`, for DBAs.
- **Scheduling setup.** When the host has not called `AddScheduling`, the package calls it and adds the job store. When the host has, the package only adds its job, which then runs on the host's job store; such a host calls `AddScheduling` before `UsePostgreSql`. Both cases are tested.
- **No job serializer.** The job is a class without fields, so ZeroAlloc.Scheduling (2.4.1 and later) generates an executor and a recurring seed that never serialize it. The package registers no serializer and does not reference ZeroAlloc.Serialisation, and a host whose own serializer does not know the job's type still works.
- **Scope.** ZeroAlloc.Scheduling's worker creates a DI scope per run and passes it as `JobContext.Services`, so the job takes the scoped OpenIddict managers from there, not from its constructor.
- **Internal job.** The job stays internal. ZeroAlloc.Scheduling 2.4.2 gives the generated registration of an internal job internal access, documents the generated members, and resolves the scoped job store of the recurring seed in a scope of its own; up to 2.4.1 the registration was public, its missing docs broke the build, and a host that validates scopes failed to start.
- **Once in total.** The recurring job is seeded with one atomic insert, which a unique index over the live runs of a recurring job keeps to one row, and the store claims a due job with a claim token. A test starts two hosts on one database and checks one recurring row and one run per due time.

### The AOT proof moves to the real store

`samples/MagicLink.NET.AotProof` uses `UsePostgreSql` when `MAGICLINK_AOT_PROOF_POSTGRES` is set, and keeps its in-memory store otherwise, so the in-process flow tests still run without a database. Token and authorization storage are switched back on when the PostgreSQL store is used, and the device flow is added: its flow test requests a device code, approves it through a proof-only endpoint, and redeems it. The `aot-proof` CI job gets a PostgreSQL service and runs the native binary on the real store, which covers the store under Native AOT.

## Tests

- **Store tests** in `tests/MagicLink.NET.PostgreSql.Tests`, against PostgreSQL from `MAGICLINK_TEST_POSTGRES`. Each test class creates its own database and drops it afterwards, so tests run in parallel. Covered per store: create and find by each key, update with the right and a stale concurrency token, delete with cascade, the list and find streams, revoke and prune with rows on both sides of the threshold, and the `IQueryable` overloads throwing.
- **Migration tests:** migrations on an empty database create the schema; running them twice changes nothing; two runs started at once both succeed.
- **Registration tests:** a missing connection string fails at startup; `ApplyMigrationsOnStartup = false` leaves the database empty; `PruneExpiredData = false` registers no job.
- **Flow tests:** the existing flow tests keep running on the in-memory store without a database. On the PostgreSQL store they run again, plus the device flow, in-process and against the native binary. The device-flow test needs token storage, so it follows the database rule below.
- **CI:** the `aot-proof` job gets the same `postgres:16` service as `build-test`, with the same credentials. Store test classes create their own database through the connection in `MAGICLINK_TEST_POSTGRES`.
- **Columns:** `text[]` columns map to `string[]` in ZeroAlloc.ORM's repository rows; the entity classes expose OpenIddict's immutable collections and convert at the store boundary.
- When `MAGICLINK_TEST_POSTGRES` is not set, the database tests are skipped with a message naming the variable, except in CI (`CI=true`), where they fail, so CI can never pass by skipping them.

## Left to the builder

- Names of internal repository classes and SQL details, within the schema above.
- Exact index definitions beyond those named.
- How the hosted migration service orders itself before ZeroAlloc.Scheduling's recurring-job registration, as long as migrations finish first.

## Risks

| Risk | Impact | Mitigation |
|---|---|---|
| The host app also uses ZeroAlloc.Scheduling with its own store | The host's `IJobStore` registration wins over ours, since `WithOrm` uses `TryAdd`, and the job runs on the host's store | Acceptable: the job still runs once per due time on the host's store. Documented in the README, and tested. |
| The ZeroAlloc.Scheduling.Orm release is delayed | The pruning job cannot land | The store, migrations and flow tests do not depend on it; they merge first, and the job follows in its own PR, still under issue #2. |
| ZeroAlloc.ORM's generator meets a column shape it cannot map | Store cannot be written as designed | Fall back to `string` plus own conversion for that column; report it as a ZeroAlloc.ORM issue. |
| OpenIddict's pruning rules differ from the EF Core store's in a detail | Valid data pruned or expired data kept | Port the conditions from OpenIddict 7.9.0's EF Core store source and test both sides of each condition. |
