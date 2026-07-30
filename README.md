# Innovorium .NET Identity Libraries

[![CI](https://github.com/innovorium/dotnet-identity-libraries/actions/workflows/ci.yml/badge.svg)](https://github.com/innovorium/dotnet-identity-libraries/actions/workflows/ci.yml)
[![CodeQL](https://github.com/innovorium/dotnet-identity-libraries/actions/workflows/codeql.yml/badge.svg)](https://github.com/innovorium/dotnet-identity-libraries/actions/workflows/codeql.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

Marten-backed ASP.NET Core Identity and OpenIddict storage providers for .NET 10.

## Release status

The source implements the providers. Before installing a package, check the
[changelog](CHANGELOG.md) and [GitHub releases](https://github.com/innovorium/dotnet-identity-libraries/releases)
to select a published functional version; early package-reservation releases do
not contain the provider APIs.

## Intended scope

The repository ships two independently usable packages:

- `Innovorium.AspNetCore.Identity.Marten` for ASP.NET Core Identity stores.
- `Innovorium.OpenIddict.Marten` for OpenIddict stores.

It will not be an identity server, UI, application framework, migration product, or owner of your connection, database lifecycle, schema deployment, credentials, application users, authorization policy, server endpoints, consent experience, signing keys, or issuer selection. See [Architecture](docs/architecture.md) for the intended boundaries.

## Package installation

The packages are independently installable; install only the provider your host
uses.

### ASP.NET Core Identity

```bash
dotnet add package Innovorium.AspNetCore.Identity.Marten
```

Supported: string-keyed users deriving from `MartenIdentityUser`, optional roles
deriving from `MartenIdentityRole`, and the standard user/password/email/phone/
security-stamp/lockout/two-factor stores plus claims, external logins,
authentication tokens, authenticator/recovery codes, passkeys, roles, and role
claims. `AddMartenStores()` replaces the Identity user/role stores only.

```csharp
using Innovorium.AspNetCore.Identity.Marten;

builder.Services
    .AddIdentityCore<ApplicationUser>()
    .AddRoles<ApplicationRole>()
    .AddMartenStores();

sealed class ApplicationUser : MartenIdentityUser;
sealed class ApplicationRole : MartenIdentityRole;
```

This package supports single-tenanted Identity documents only. It does not
implement `IProtectedUserStore<TUser>`: disable `ProtectPersonalData` or choose
another store. It does not provide application profiles, memberships,
entitlements, or authorization policy.

### OpenIddict core

```bash
dotnet add package Innovorium.OpenIddict.Marten
```

Supported: the default Marten application, authorization, scope, and token
entities/stores registered by `UseMarten()`, including OpenIddict manager
queries, revocation, and pruning.

```csharp
using Innovorium.OpenIddict.Marten;

builder.Services.AddOpenIddict()
    .AddCore(options => options.UseMarten());
```

The package does not configure an OpenIddict server, endpoints, consent,
issuer, signing/encryption credentials, validation, client-provisioning policy,
custom entity types, named document stores, or multi-tenant storage. Revoke and
prune process at most 1,000,000 matching rows per invocation in batches of
1,000. Repeat prune when it returns 1,000,000. Do not blindly repeat an
unfiltered revoke call because already-revoked rows can be selected again;
partition larger revoke workloads with filters that exclude completed rows.

## Host-owned Marten and schema lifecycle

Register Marten before either provider and keep production schema creation
disabled:

```csharp
using JasperFx;
using Marten;

builder.Services.AddMarten(options =>
{
    options.Connection(builder.Configuration.GetConnectionString("Marten")
        ?? throw new InvalidOperationException("ConnectionStrings:Marten is required."));
    options.AutoCreateSchemaObjects = AutoCreate.None;
});
```

Neither package selects a connection, obtains credentials, changes
auto-creation, supplies migrations, or applies database objects. From the host
application configured with the exact provider registrations, use Marten's
official CLI to generate and verify reviewed artifacts:

```csharp
builder.Host.ApplyJasperFxExtensions();

// Build and map the host as usual, then replace app.Run():
return await app.RunJasperFxCommands(args);
```

```bash
dotnet run --project <host-project> -- db-patch schema.sql --drop schema.drop.sql
dotnet run --project <host-project> -- db-assert
```

Review and deliver the forward script through the host database process, test
the generated drop script and backup restoration in a representative
environment, and run `db-assert` as a deployment check. The first production
application instance must not make schema changes.

## Source samples

The source-only [Identity](samples/Identity/README.md) and
[OpenIddict](samples/OpenIddict/README.md) samples remain checkout evaluation
tools, not NuGet installation guidance.

For compatibility, persisted-contract cautions, and upgrade boundaries, see the
[consumption guide](docs/consumption.md).

## Compatibility limits

Read the [changelog](CHANGELOG.md), [compatibility policy](docs/compatibility.md), and [customer experience guide](docs/customer-experience.md). In particular:

- While the project is below 1.0, public APIs and persisted document shapes may
  change in a minor release when documented in the changelog.
- Your host application remains responsible for PostgreSQL access, secrets, schema deployment, backups, observability, authentication endpoints, authorization, signing credentials, and recovery procedures.
- Use only APIs documented for the exact published version.

## Ask, report, or contribute

- Start a [GitHub Discussion](https://github.com/innovorium/dotnet-identity-libraries/discussions) for questions, adoption interest, design feedback, or to compare approaches before opening a feature request.
- Open a [bug report](https://github.com/innovorium/dotnet-identity-libraries/issues/new?template=bug.yml) only for a reproducible defect in published behavior.
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
