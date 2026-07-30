# Innovorium .NET Identity Libraries

[![CI](https://github.com/innovorium/dotnet-identity-libraries/actions/workflows/ci.yml/badge.svg)](https://github.com/innovorium/dotnet-identity-libraries/actions/workflows/ci.yml)
[![CodeQL](https://github.com/innovorium/dotnet-identity-libraries/actions/workflows/codeql.yml/badge.svg)](https://github.com/innovorium/dotnet-identity-libraries/actions/workflows/codeql.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

Marten-backed storage providers for ASP.NET Core Identity and OpenIddict on .NET 10.

## Status

This project is under active development. No packages have been published and the public API is not yet stable.

Planned packages:

- `Innovorium.AspNetCore.Identity.Marten`
- `Innovorium.OpenIddict.Marten`

## Principles

- Follow the supported extension points of ASP.NET Core Identity, OpenIddict, and Marten.
- Target .NET 10 only and use verified stable dependency releases.
- Keep Identity and OpenIddict storage independently usable.
- Prefer small public APIs, explicit ownership, and database-enforced invariants.
- Keep connection, database lifecycle, schema deployment, and credentials under host control.
- Do not include legacy data migration, import, or dual-write tooling.

See [Architecture](docs/architecture.md), [Development](docs/development.md), and [Contributing](CONTRIBUTING.md).

## Local verification

```bash
dotnet tool restore
dotnet cake --target Verify
```

The Cake build restores dependencies, checks formatting, builds and tests, uses MinVer for Git-derived versions, inspects package contents, compiles a clean consumer, writes checksums, audits NuGet dependencies, and scans repository content and history for secrets when `gitleaks` is available.

## Security

Do not report vulnerabilities in public issues. Follow [SECURITY.md](SECURITY.md).

## License

Licensed under the [MIT License](LICENSE).
