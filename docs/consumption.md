# Consumption and ownership guide

This guide describes the current source integration and the rules a future
functional package release must preserve. It does not make
`0.1.0-alpha.2` usable: that NuGet release contains no provider API.

## Choose the right artifact

| You have | Use it for | Do not use it for |
| --- | --- | --- |
| `0.1.0-alpha.2` from NuGet | Validating package identity and release metadata | Application integration or persistence |
| This repository's `main` checkout | Reviewing the unreleased source and running the source samples | A versioned production dependency |
| A future functional release | The behavior and compatibility statement in that release's notes | Assuming unlisted stores, upgrade paths, or hosting behavior |

The samples use `ProjectReference` so their code always corresponds to the
source checked out beside them. When a functional package is published, replace
that local reference only with the exact package/version and instructions named
in its release notes.

## Registration does not create a database

Both providers add Marten mappings and framework store services. They do not
select a connection, obtain credentials, change Marten auto-creation, or apply
database objects. The host must do those explicitly:

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

`AutoCreate.None` is the production posture. Generate, review, and deploy any
Marten/PostgreSQL schema change through the application's normal migration or
database-release process. This project neither owns nor executes migrations.
The first application instance must not be the component that modifies a
production schema. Connection secret rotation, backups, restoration, telemetry,
and primary-read routing remain host responsibilities.

## Identity package boundary

The source registration is intentionally ordinary ASP.NET Core Identity:

```csharp
using Innovorium.AspNetCore.Identity.Marten;

builder.Services
    .AddIdentityCore<ApplicationUser>()
    .AddRoles<ApplicationRole>()
    .AddMartenStores();
```

`ApplicationUser` derives from `MartenIdentityUser`; a role-enabled host uses a
role type deriving from `MartenIdentityRole`. The provider is storage for the
Identity account and credential lifecycle. It does not define an application
profile, membership model, product entitlements, or authorization policy.
Model those separately in host-owned documents/services keyed by the Identity
user ID. The Identity sample demonstrates manager consumption without adding
those unrelated domains.

## OpenIddict package boundary

The source registration supplies OpenIddict core stores only:

```csharp
using Innovorium.OpenIddict.Marten;

builder.Services.AddOpenIddict()
    .AddCore(options => options.UseMarten());
```

It persists OpenIddict's application, authorization, scope, and token entities
in the source implementation. It does not turn the host into an authorization
server. Server endpoints, consent, issuer selection, signing/encryption keys,
token validation, client provisioning policy, and operations are explicit host
choices. See the OpenIddict sample for manager-level client creation only.
Each database operation uses a package-owned lightweight session. Calling an
OpenIddict manager therefore never commits unrelated documents staged in the
host application's scoped Marten session.

OpenIddict revoke and prune operations deliberately process at most 1,000,000
matching rows per invocation, in batches of 1,000. This bounds memory use and
transaction pressure. Operators with a larger backlog must invoke the relevant
maintenance operation again until it returns fewer than 1,000,000 affected
rows, while monitoring normal database load and cancellation behavior.

## Persisted data and upgrades

Document IDs, aliases, JSON field meaning, concurrency fields, named indexes,
and relationship semantics are persistence contracts once released. They are
not application implementation details that can be casually altered. Before a
release upgrade:

1. Read the target release's compatibility and security notes.
2. Compare its declared persisted-contract and schema changes with the deployed
   version.
3. Review and deploy the required database change through your own process.
4. Test upgrade, concurrency, token/revocation behavior, backup restore, and
   rollback in a topology representative of production.

Prerelease documents and APIs may change before a stable release. Keep a tested
backup and rollback plan; do not treat the library as a migration service.
