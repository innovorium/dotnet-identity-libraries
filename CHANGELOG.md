# Changelog

All notable changes to this project will be documented here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and releases use [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- An unreleased Marten-backed ASP.NET Core Identity source implementation with
  string-keyed extensible user and role documents, `AddMartenStores()`
  registration, account/security relationship stores, optimistic concurrency,
  and package-owned sessions.
- An unreleased OpenIddict source implementation with application,
  authorization, scope, and token documents/stores, `UseMarten()` registration,
  revision-aware persistence, bulk operations, and named Marten indexes.
- Standalone source samples that exercise `UserManager`/`RoleManager` and the
  OpenIddict application manager while keeping connection and schema lifecycle
  ownership in the host application.
- Consumption, persisted-contract, and upgrade-boundary guidance.

### Still not claimed

- A published functional NuGet package or a stable provider compatibility
  promise.
- Migration tooling, automatic production schema deployment, or a managed
  database lifecycle.
- Custom entity models, named document stores, or multi-tenant provider
  storage.
- OpenIddict server endpoints, consent UI, signing credentials, issuer policy,
  or token validation configuration.

The source work is not yet a provider release. Use the samples only from this
checkout and wait for versioned release notes before consuming a NuGet package.

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
