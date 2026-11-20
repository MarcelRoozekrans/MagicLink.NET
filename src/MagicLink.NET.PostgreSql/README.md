# MagicLink.NET.PostgreSql

The PostgreSQL store for [MagicLink.NET](https://github.com/MarcelRoozekrans/MagicLink.NET). It keeps OpenIddict's applications, authorizations, scopes and tokens in PostgreSQL without Entity Framework Core, and it is Native AOT compatible.

## Use

```csharp
services.AddOpenIddict()
    .AddCore(options => options.UsePostgreSql(store =>
    {
        store.ConnectionString = "Host=...;Database=...;Username=...;Password=...";
        store.ApplyMigrationsOnStartup = true; // the default
    }));
```

`ConnectionString` is required; without it the host fails at startup. The store builds its own data source from it, so it does not clash with a data source the host registers.

## Schema and migrations

The tables live in schema `magiclink`: `applications`, `authorizations`, `scopes` and `tokens`, with the migration history in `magiclink.__zaorm_migrations`. When the host starts, the store applies new migrations under a PostgreSQL advisory lock, so several instances can start at once.

Where a DBA applies the schema, set `ApplyMigrationsOnStartup = false`. The scripts ship in the package under `sql/`, numbered in the order they apply.

## Behaviour

* An update or delete succeeds only with the concurrency token the entity was read with; otherwise it throws OpenIddict's `ConcurrencyException`. That is how an authorization code or refresh token is redeemed once.
* Deleting an application deletes its authorizations and tokens. Deleting an authorization deletes its tokens.
* `PruneAsync` follows the rules of OpenIddict's Entity Framework Core store and deletes in batches of 1000 rows.
* The `CountAsync`, `GetAsync` and `ListAsync` overloads that take an `IQueryable` query throw `NotSupportedException`: the store runs hand-written SQL and has no runtime LINQ. OpenIddict's server does not call them.

Tested against PostgreSQL 16.
