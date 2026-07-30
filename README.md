# Innovorium .NET Identity Libraries

[![CI](https://github.com/innovorium/dotnet-identity-libraries/actions/workflows/ci.yml/badge.svg)](https://github.com/innovorium/dotnet-identity-libraries/actions/workflows/ci.yml)
[![CodeQL](https://github.com/innovorium/dotnet-identity-libraries/actions/workflows/codeql.yml/badge.svg)](https://github.com/innovorium/dotnet-identity-libraries/actions/workflows/codeql.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

Foundation packages for future Marten-backed ASP.NET Core Identity and OpenIddict storage providers on .NET 10.

## Status: not ready to consume

[`v0.1.0-alpha.2`](https://github.com/innovorium/dotnet-identity-libraries/releases/tag/v0.1.0-alpha.2) is published to reserve these NuGet package IDs and validate the repository, package, and release path:

- [`Innovorium.AspNetCore.Identity.Marten`](https://www.nuget.org/packages/Innovorium.AspNetCore.Identity.Marten/0.1.0-alpha.2)
- [`Innovorium.OpenIddict.Marten`](https://www.nuget.org/packages/Innovorium.OpenIddict.Marten/0.1.0-alpha.2)

They are **nonfunctional foundation packages**. They contain no public store, document-model, schema-registration, or dependency-injection API. Installing either package does not configure Marten, persist identity data, or add an authentication or OpenIddict provider. Do not use this release in an application or production environment.

The package IDs and version are real; provider behavior is not. A usable prerelease will say so explicitly in its release notes and will include a documented public API, behavior coverage, and a compatibility contract.

The `main` branch now contains unreleased, partial provider slices for a
user-only Identity store and OpenIddict application/scope stores. They are not
the published `alpha.2` packages and are not yet a complete provider contract;
roles, Identity credentials/claims/passkeys, and OpenIddict authorization/token
storage still remain.

## Intended scope

The repository is intended to ship two independently usable packages:

- `Innovorium.AspNetCore.Identity.Marten` for ASP.NET Core Identity stores.
- `Innovorium.OpenIddict.Marten` for OpenIddict stores.

It will not be an identity server, UI, application framework, migration product, or owner of your connection, database lifecycle, schema deployment, credentials, application users, authorization policy, server endpoints, consent experience, signing keys, or issuer selection. See [Architecture](docs/architecture.md) for the intended boundaries.

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

The Cake build restores dependencies, checks formatting, builds and tests, inspects package contents, compiles a clean consumer, writes checksums, audits NuGet dependencies, and scans repository content and history for secrets when `gitleaks` is available. Passing this gate verifies the repository foundation; it does not establish a working Identity or OpenIddict provider.

## License

Licensed under the [MIT License](LICENSE).
