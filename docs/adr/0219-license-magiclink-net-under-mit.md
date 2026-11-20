# ADR-219: License MagicLink.NET under MIT

## Status

accepted (2026-11-20)

## Context

MagicLink.NET got its public repository on 2026-11-20, so it needed a licence. Other products in the line are MIT-licensed, and OpenIddict, which MagicLink.NET builds on, is Apache-licensed ([ADR-010](0010-build-magiclink-net-on-openiddict.md)). MIT keeps the products free of licence friction when they are combined.

## Decision

* MagicLink.NET is licensed under MIT.
* The repository is public: github.com/MarcelRoozekrans/MagicLink.NET.

## Consequences

* No licence friction when MagicLink.NET runs next to, or inside, the other products.
* Anyone may use, change and host it, including commercially.
