# Changelog

All notable changes to this project will be documented here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and releases use [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.1.0] - 2026-07-30

### Added

- Marten-backed ASP.NET Core Identity with
  string-keyed extensible user and role documents, `AddMartenStores()`
  registration, account/security relationship stores, optimistic concurrency,
  and package-owned sessions.
- OpenIddict application,
  authorization, scope, and token documents/stores, `UseMarten()` registration,
  revision-aware persistence, bulk operations, and named Marten indexes.
- Independent package installation and registration guidance, including
  host-owned Marten schema lifecycle and maintenance limits.

### Not included

- Migration tooling, automatic production schema deployment, or a managed
  database lifecycle.
- Custom entity models, named document stores, or multi-tenant provider
  storage.
- Identity protected-personal-data support or conjoined tenancy.
- OpenIddict server endpoints, consent UI, signing credentials, issuer policy,
  or token validation configuration.

This is the first functional release. Because the project is still below 1.0,
public APIs and persisted contracts may change in a future minor release when
documented in this changelog. `alpha.2` remains nonfunctional.

## [0.1.0-alpha.2] - 2026-07-30

### Added

- Published `Innovorium.AspNetCore.Identity.Marten` and `Innovorium.OpenIddict.Marten` to reserve their NuGet package IDs and exercise the release path.
- Repository, package, test, documentation, security, and release foundation for the planned providers.

### Not included

- No public provider, store, document model, schema-registration, or dependency-injection API.
- No working Marten persistence integration for ASP.NET Core Identity or OpenIddict.

`0.1.0-alpha.2` is a nonfunctional foundation release. It is not suitable for application or production use.

[Unreleased]: https://github.com/innovorium/dotnet-identity-libraries/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/innovorium/dotnet-identity-libraries/compare/v0.1.0-alpha.2...v0.1.0
[0.1.0-alpha.2]: https://github.com/innovorium/dotnet-identity-libraries/releases/tag/v0.1.0-alpha.2
