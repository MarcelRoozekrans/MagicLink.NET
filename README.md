# MagicLink.NET

Passwordless sign-in with magic links for ASP.NET Core.

A user enters their email address and receives a link. Clicking the link signs them in. Tokens are random, stored only as hashes, expire, and work once.

> **Status:** pre-1.0. The public API may still change between minor versions.

## Packages

| Package | What it does | Status |
| --- | --- | --- |
| `MagicLink` | Token generation and validation, store and sender interfaces, in-memory store | in progress |
| `MagicLink.MailKit` | Sends the link by SMTP through MailKit | coming soon |
| `MagicLink.AspNetCore` | `AddMagicLink()`, request and verify endpoints, cookie sign-in | coming soon |
| `MagicLink.ZeroAlloc.Orm` | Token store on ZeroAlloc.ORM | coming soon |

All packages target .NET 10 and .NET 11.

## Building

```bash
dotnet build
dotnet test
```

The SDK version is pinned in `global.json`.

## License

MIT. See [LICENSE](https://github.com/MarcelRoozekrans/MagicLink.NET/blob/main/LICENSE).
