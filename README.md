# Innovorium .NET Identity Libraries

[![CI](https://github.com/innovorium/dotnet-identity-libraries/actions/workflows/ci.yml/badge.svg)](https://github.com/innovorium/dotnet-identity-libraries/actions/workflows/ci.yml)
[![CodeQL](https://github.com/innovorium/dotnet-identity-libraries/actions/workflows/codeql.yml/badge.svg)](https://github.com/innovorium/dotnet-identity-libraries/actions/workflows/codeql.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

Marten-backed ASP.NET Core Identity and OpenIddict storage providers for .NET 10.

## Release status

[`v0.1.0-alpha.2`](https://github.com/innovorium/dotnet-identity-libraries/releases/tag/v0.1.0-alpha.2) is published to reserve these NuGet package IDs and validate the repository, package, and release path:

- [`Innovorium.AspNetCore.Identity.Marten`](https://www.nuget.org/packages/Innovorium.AspNetCore.Identity.Marten/0.1.0-alpha.2)
- [`Innovorium.OpenIddict.Marten`](https://www.nuget.org/packages/Innovorium.OpenIddict.Marten/0.1.0-alpha.2)

They are **nonfunctional foundation packages**. They contain no public store, document-model, schema-registration, or dependency-injection API. Installing either package does not configure Marten, persist identity data, or add an authentication or OpenIddict provider. Do not use this release in an application or production environment.

The package IDs and version are real; provider behavior is not. A usable prerelease will say so explicitly in its release notes and will include a documented public API, behavior coverage, and a compatibility contract.

The `main` branch contains an **unreleased development snapshot** with the
Identity and OpenIddict registrations used by the source samples. It is not the
published `alpha.2` package and is not a release contract. The samples use
project references deliberately: they are a way to review and validate the
current source, not NuGet installation guidance.

Do not install `alpha.2` expecting the APIs shown below. Wait for a release
whose notes explicitly name its supported stores and published package version.

## Intended scope

The repository is intended to ship two independently usable packages:

- `Innovorium.AspNetCore.Identity.Marten` for ASP.NET Core Identity stores.
- `Innovorium.OpenIddict.Marten` for OpenIddict stores.

It will not be an identity server, UI, application framework, migration product, or owner of your connection, database lifecycle, schema deployment, credentials, application users, authorization policy, server endpoints, consent experience, signing keys, or issuer selection. See [Architecture](docs/architecture.md) for the intended boundaries.

## Source samples

The two minimal .NET 10 samples show the intended host-owned integration from a
checkout of this repository:

- [Identity sample](samples/Identity/README.md): `UserManager` and
  `RoleManager` backed by Marten.
- [OpenIddict sample](samples/OpenIddict/README.md): OpenIddict's application
  manager backed by Marten.

Each sample takes its PostgreSQL connection string from configuration and sets
Marten to `AutoCreate.None`. It never creates, upgrades, or applies a database
schema. Your delivery process owns reviewed schema changes, backups, recovery,
credentials, and production operations.

For the exact registration boundaries, persisted-contract cautions, and future
package adoption sequence, see the [consumption guide](docs/consumption.md).

## Before you adopt a future prerelease

Read the release notes, [compatibility policy](docs/compatibility.md), and [customer experience guide](docs/customer-experience.md). In particular:

- Prereleases may change public APIs and persisted document shapes without stable-version compatibility guarantees.
- Your host application remains responsible for PostgreSQL access, secrets, schema deployment, backups, observability, authentication endpoints, authorization, signing credentials, and recovery procedures.
- Do not assume a provider API exists from a package name, transitive dependency, issue discussion, or architecture document. Use only APIs documented for the exact released version.

## Ask, report, or contribute

- Start a [GitHub Discussion](https://github.com/innovorium/dotnet-identity-libraries/discussions) for questions, adoption interest, design feedback, or to compare approaches before opening a feature request.
- Open a [bug report](https://github.com/innovorium/dotnet-identity-libraries/issues/new?template=bug.yml) only for a reproducible defect in released behavior. For this foundation release, report documentation or packaging defects rather than missing provider behavior.
- Open a [feature proposal](https://github.com/innovorium/dotnet-identity-libraries/issues/new?template=feature.yml) for a bounded problem and its API, persistence, compatibility, and security implications.
- Report vulnerabilities privately; see [SECURITY.md](SECURITY.md). Do not disclose secrets, personal data, or active tokens in a public issue or discussion.

See [SUPPORT.md](SUPPORT.md) for the support boundary and [CONTRIBUTING.md](CONTRIBUTING.md) for the contributor workflow.

## Local verification

```bash
dotnet tool restore
dotnet cake --target Verify
```

The Cake build restores dependencies, checks formatting, builds and tests, inspects package contents, compiles an independent clean consumer for each package against its documented registration API, writes checksums, audits NuGet dependencies, and scans repository content and history for secrets when `gitleaks` is available. PostgreSQL integration tests also require `INNOVORIUM_TEST_POSTGRES`; CI supplies a disposable database and treats those tests as a release gate.

## License

Licensed under the [MIT License](LICENSE).
