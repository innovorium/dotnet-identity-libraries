# Changelog

All notable changes to this project will be documented here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and releases use [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- A user-only ASP.NET Core Identity store slice with string-keyed extensible
  user documents, core account/security fields, Marten mapping, isolated
  sessions, optimistic concurrency, and `AddMartenStores()` registration.
- OpenIddict application and scope document/store slices with `UseMarten()`
  registration, revision-aware persistence, stable aliases, and named indexes.
- Focused contract, registration, package-consumer, and concurrency coverage.

### Not yet included

- Identity roles, claims, external logins, tokens, authenticator/recovery codes,
  or .NET 10 passkeys.
- OpenIddict authorization and token stores, cascades, revocation, or pruning.
- Complete PostgreSQL integration coverage for the OpenIddict slice.

These unreleased slices are not yet a complete provider release.

## [0.1.0-alpha.2] - 2026-07-30

### Added

- Published `Innovorium.AspNetCore.Identity.Marten` and `Innovorium.OpenIddict.Marten` to reserve their NuGet package IDs and exercise the release path.
- Repository, package, test, documentation, security, and release foundation for the planned providers.

### Not included

- No public provider, store, document model, schema-registration, or dependency-injection API.
- No working Marten persistence integration for ASP.NET Core Identity or OpenIddict.

`0.1.0-alpha.2` is a nonfunctional foundation release. It is not suitable for application or production use.

[Unreleased]: https://github.com/innovorium/dotnet-identity-libraries/compare/v0.1.0-alpha.2...HEAD
[0.1.0-alpha.2]: https://github.com/innovorium/dotnet-identity-libraries/releases/tag/v0.1.0-alpha.2
