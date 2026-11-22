# Changelog

## [0.2.0](https://github.com/MarcelRoozekrans/MagicLink.NET/compare/v0.1.2...v0.2.0) (2026-11-22)


### ⚠ BREAKING CHANGES

* footer, and its title carries the ! as well, because a squash merge makes the title the commit on main. Removing suppressions needs no marker. The workflow also runs on title edits.

### Continuous Integration

* validate the public API against the last release ([952978a](https://github.com/MarcelRoozekrans/MagicLink.NET/commit/952978a5a99daa4879717464214768b486ea40be)), closes [#27](https://github.com/MarcelRoozekrans/MagicLink.NET/issues/27)

## [0.1.2](https://github.com/MarcelRoozekrans/MagicLink.NET/compare/v0.1.1...v0.1.2) (2026-11-22)


### Features

* confirm sign-in on the page with a button or a code ([#31](https://github.com/MarcelRoozekrans/MagicLink.NET/issues/31)) ([683405e](https://github.com/MarcelRoozekrans/MagicLink.NET/commit/683405e875f40b93c9993027f073ea5dbd670194))
* decide who may sign in and with which groups through a pluggable source ([#35](https://github.com/MarcelRoozekrans/MagicLink.NET/issues/35)) ([eb79e21](https://github.com/MarcelRoozekrans/MagicLink.NET/commit/eb79e21577d40f9b54a16a0fadb85d4175b87bc7))
* extend a session with a refresh token instead of a new email ([d53630e](https://github.com/MarcelRoozekrans/MagicLink.NET/commit/d53630ea6eeff79b878f3ff3b102c88c5c54027b)), closes [#10](https://github.com/MarcelRoozekrans/MagicLink.NET/issues/10)
* give an unknown email address the same answer as a known one ([#38](https://github.com/MarcelRoozekrans/MagicLink.NET/issues/38)) ([a307512](https://github.com/MarcelRoozekrans/MagicLink.NET/commit/a307512fd2d1add8f59d5e2422c3f5ba886711f7)), closes [#5](https://github.com/MarcelRoozekrans/MagicLink.NET/issues/5)
* host MagicLink.NET in an ASP.NET Core app with services.AddMagicLink ([3969c45](https://github.com/MarcelRoozekrans/MagicLink.NET/commit/3969c455993246b7478191cc8868d7ba2567023a)), closes [#11](https://github.com/MarcelRoozekrans/MagicLink.NET/issues/11)
* limit how many sign-in emails one address gets ([f03eb62](https://github.com/MarcelRoozekrans/MagicLink.NET/commit/f03eb62854c32e30115d1f0246e6a5224a121c35)), closes [#39](https://github.com/MarcelRoozekrans/MagicLink.NET/issues/39)
* read who may sign in from an existing PostgreSQL or SQL Server database ([539b58e](https://github.com/MarcelRoozekrans/MagicLink.NET/commit/539b58e8598ba3819e066f06446ca4fa766220c1)), closes [#8](https://github.com/MarcelRoozekrans/MagicLink.NET/issues/8)
* run MagicLink.NET on its own from a container image ([002e9c3](https://github.com/MarcelRoozekrans/MagicLink.NET/commit/002e9c3714b03a297fa72335405b28052ccfa2cf)), closes [#11](https://github.com/MarcelRoozekrans/MagicLink.NET/issues/11)
* send the sign-in email in the background, over SMTP by default ([#33](https://github.com/MarcelRoozekrans/MagicLink.NET/issues/33)) ([8d4d6ac](https://github.com/MarcelRoozekrans/MagicLink.NET/commit/8d4d6ac5efbf55879b2b0efead17863da331bd65)), closes [#6](https://github.com/MarcelRoozekrans/MagicLink.NET/issues/6)
* show whether an address may sign in with the check-sign-in command ([6d3d218](https://github.com/MarcelRoozekrans/MagicLink.NET/commit/6d3d21825ae15128e312369b15d56808d7280b7c)), closes [#9](https://github.com/MarcelRoozekrans/MagicLink.NET/issues/9)
* sign a person in on a CLI through the device authorization grant ([8d665d5](https://github.com/MarcelRoozekrans/MagicLink.NET/commit/8d665d5b4c7f7bab6ce0b7cfb5648b68060d54a7)), closes [#24](https://github.com/MarcelRoozekrans/MagicLink.NET/issues/24)
* sign people in to OIDC applications through the browser ([4093547](https://github.com/MarcelRoozekrans/MagicLink.NET/commit/4093547be501651fd14125f49d4f513831c22218))

## [0.1.1](https://github.com/MarcelRoozekrans/MagicLink.NET/compare/v0.1.0...v0.1.1) (2026-11-21)


### Features

* **postgresql:** prune expired tokens and authorizations every hour ([72f85db](https://github.com/MarcelRoozekrans/MagicLink.NET/commit/72f85dbe200ac39161d9c6d5c1db3451c5ad4ead))

## 0.1.0 (2026-11-20)


### Features

* **postgresql:** own OpenIddict store on PostgreSQL ([#20](https://github.com/MarcelRoozekrans/MagicLink.NET/issues/20)) ([3568edb](https://github.com/MarcelRoozekrans/MagicLink.NET/commit/3568edb4f411aa175ece2d9f679badafa2fa83c1))
