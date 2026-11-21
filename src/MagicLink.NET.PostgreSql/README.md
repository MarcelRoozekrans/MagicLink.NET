# MagicLink.NET.PostgreSql

The PostgreSQL store for [MagicLink.NET](https://github.com/MarcelRoozekrans/MagicLink.NET). It keeps OpenIddict's applications, authorizations, scopes and tokens in PostgreSQL without Entity Framework Core, and it is Native AOT compatible.

## Use

```csharp
services.AddOpenIddict()
    .AddCore(options => options.UsePostgreSql(store =>
    {
        store.ConnectionString = "Host=...;Database=...;Username=...;Password=...";
        store.ApplyMigrationsOnStartup = true; // the default
        store.PruneExpiredData = true;         // the default
    }));
```

`ConnectionString` is required; without it the host fails at startup. The store builds its own data source from it, so it does not clash with a data source the host registers.

## Schema and migrations

The tables live in schema `magiclink`: `applications`, `authorizations`, `scopes` and `tokens`, with the migration history in `magiclink.__zaorm_migrations`. With pruning on, the job table `SchedulingJobs` of [ZeroAlloc.Scheduling](https://github.com/ZeroAlloc-Net/ZeroAlloc.Scheduling) lives there too. When the host starts, the store applies new migrations under a PostgreSQL advisory lock, so several instances can start at once.

Where a DBA applies the schema, set `ApplyMigrationsOnStartup = false`. The scripts ship in the package under `sql/`, numbered in the order they apply, and the job table's under `sql/scheduling/`: apply those after the store's, with `search_path` set to `magiclink`. With pruning on and the job table missing, the host fails at startup.

## Pruning expired data

With `PruneExpiredData` on, the default, a job runs once an hour and deletes tokens and authorizations created more than 14 days ago that are no longer valid, through OpenIddict's `PruneAsync`, whose rules are listed under Behaviour. It runs on ZeroAlloc.Scheduling with its jobs in the store's database, so several instances run it once per hour in total. The job needs no job serializer.

`PruneExpiredData` is read when `UsePostgreSql` runs, so set it in the callback passed there.

* When the host does not use ZeroAlloc.Scheduling, `UsePostgreSql` calls `AddScheduling` and keeps the jobs in the store's database.
* When the host does, it must call `AddScheduling` before `UsePostgreSql`. The store then only adds its job, which runs on the host's job store, and does not create the job table. With a job store that several instances do not share, such as the in-memory one, every instance runs the job.

A run that a stopped or crashed host left unfinished is run again once its claim is older than ZeroAlloc.Scheduling's `SchedulingOptions.ClaimTimeout`, 15 minutes by default. A run that takes longer than that runs a second time while the first still runs, which pruning allows: both delete the same expired rows. Set the timeout with `services.Configure<SchedulingOptions>(o => o.ClaimTimeout = ...)`, or in the host's own `AddScheduling` callback. A run that fails on every attempt is dead-lettered, and the next hour's run is still scheduled.

Set `PruneExpiredData = false` to prune some other way.

## Behaviour

* An update or delete succeeds only with the concurrency token the entity was read with; otherwise it throws OpenIddict's `ConcurrencyException`. That is how an authorization code or refresh token is redeemed once.
* Deleting an application deletes its authorizations and tokens. Deleting an authorization deletes its tokens.
* `PruneAsync` follows the rules of OpenIddict's Entity Framework Core store and deletes in batches of 1000 rows.
* The `CountAsync`, `GetAsync` and `ListAsync` overloads that take an `IQueryable` query throw `NotSupportedException`: the store runs hand-written SQL and has no runtime LINQ. OpenIddict's server does not call them.

Tested against PostgreSQL 16.
