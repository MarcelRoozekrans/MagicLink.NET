# MagicLink.NET documentation

This folder holds what MagicLink.NET should do and why: the agreed specifications and the architecture decisions behind them. Code follows these documents; when a criterion changes during the build, the Spec here changes with it.

## Specs

A Spec describes one goal, its scope, and acceptance criteria someone else can check.

* [Run an OIDC provider for applications and CLIs](specs/magiclink-net-run-an-oidc-provider-for-applications-and-clis.md)
* [Confirm sign-in with a link or a code](specs/magiclink-net-confirm-sign-in-with-a-link-or-a-code.md)
* [Decide who may sign in and with which groups](specs/magiclink-net-decide-who-may-sign-in-and-with-which-groups.md)
* [Send the sign-in email through a pluggable sender](specs/magiclink-net-send-the-sign-in-email-through-a-pluggable-sender.md)
* [Prove OpenIddict runs under Native AOT](specs/magiclink-net-prove-openiddict-runs-under-native-aot.md)
* [Deliver as a NuGet package and a container image](specs/magiclink-net-deliver-as-a-nuget-package-and-a-container-image.md)

## Architecture decisions

ADR numbers run across a whole product line, so numbers missing here belong to decisions about other products or about the line as a whole.

* [ADR-009: Offer magic-link sign-in as a separate OIDC provider](adr/0009-offer-magic-link-sign-in-as-a-separate-oidc-provider.md)
* [ADR-010: Build MagicLink.NET on OpenIddict](adr/0010-build-magiclink-net-on-openiddict.md)
* [ADR-011: Confirm magic-link sign-in with a button or a short code](adr/0011-confirm-magic-link-sign-in-with-a-button-or-a-short-code.md)
* [ADR-012: Package ids for MagicLink.NET start with MagicLink.NET](adr/0012-package-ids-for-magiclink-net-start-with-magiclink-net.md)
* [ADR-014: Look up who may sign in, and their groups, through a pluggable source](adr/0014-look-up-who-may-sign-in-and-their-groups-through-a-pluggable-source.md)
* [ADR-015: Send sign-in email through a pluggable sender, SMTP by default](adr/0015-send-sign-in-email-through-a-pluggable-sender-smtp-by-default.md)
* [ADR-016: Aim for Native AOT, with an own store on PostgreSQL](adr/0016-aim-for-native-aot-with-an-own-store-on-postgresql.md)
* [ADR-219: License MagicLink.NET under MIT](adr/0219-license-magiclink-net-under-mit.md)
* [ADR-249: Confirm the sign-in link only in the browser that started the sign-in](adr/0249-confirm-the-sign-in-link-only-in-the-browser-that-started-the-sign-in.md)
