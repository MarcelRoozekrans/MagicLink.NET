# ADR-012: Package ids for MagicLink.NET start with MagicLink.NET

## Status

accepted (2026-11-20)

## Context

MagicLink.NET needs NuGet package ids. Another product in the line used a shortened prefix (`ThemisNet.`), and `MagicLinkNet.` was proposed to match it. The decision was that this naming is not right and that MagicLink.NET should simply use its own name.

Options considered:

* `MagicLinkNet.`, following the other product.
* `MagicLink.`
* `MagicLink.NET`, the product name itself.

## Decision

* The main package id is `MagicLink.NET`, and further packages start with `MagicLink.NET.` (for example `MagicLink.NET.Abstractions`).
* NuGet ids are not case-sensitive, so the capitals only affect how the name is shown.

## Consequences

* Package names match the product name, so nobody has to translate between them.
* *Open: whether the id* `MagicLink.NET` *and the prefix* `MagicLink.NET.` *are free on nuget.org. A prefix reservation has been requested.*
* The same rule was later made for the whole product line (ADR-013, a product-line decision not published in this repository).
