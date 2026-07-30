# Consumption and ownership guide

This guide describes the provider implementation. Install only a published
functional version recorded in the [changelog](../CHANGELOG.md), which also
contains version-specific behavior and upgrade notes.

## Choose the right artifact

| You have | Use it for | Do not use it for |
| --- | --- | --- |
| Published functional package | The behavior and limitations described here and in its changelog entry | Assuming unlisted stores, upgrade paths, or hosting behavior |

The samples use `ProjectReference` so their code always corresponds to the
source checked out beside them. They are not package-installation instructions.

## Install only the provider you use

Install one independent package:

```bash
dotnet add package Innovorium.AspNetCore.Identity.Marten
# or
dotnet add package Innovorium.OpenIddict.Marten
```

Both target `net10.0`. Identity depends on Marten `[9.21.0, 10.0.0)` and
`Microsoft.Extensions.Identity.Stores` `[10.0.10, 11.0.0)`. OpenIddict depends
on Marten `[9.21.0, 10.0.0)`, Npgsql `[9.0.4, 10.0.0)`, OpenIddict.Core
`[7.6.0, 8.0.0)`, and Weasel.Storage `[9.17.0, 10.0.0)`. Do not add the other
Innovorium package unless the host needs it.

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

`AutoCreate.None` is the production posture. This project neither owns nor
executes migrations. Run Marten's CLI against the host application with its
exact provider registrations to generate and verify review artifacts:

```csharp
builder.Host.ApplyJasperFxExtensions();

// Build and map the host as usual, then replace app.Run():
return await app.RunJasperFxCommands(args);
```

```bash
dotnet run --project <host-project> -- db-patch schema.sql --drop schema.drop.sql
dotnet run --project <host-project> -- db-assert
```

Review and deliver the forward script through the application's normal
database-release process. Test the generated drop script and backup restoration
before relying on either rollback path. The first application instance must not
modify a production schema. Connection secret rotation, backups, restoration,
telemetry, primary-read routing, and schema rollback remain host
responsibilities.

## Identity package boundary and supported stores

The source registration is intentionally ordinary ASP.NET Core Identity:

```csharp
using Innovorium.AspNetCore.Identity.Marten;

builder.Services
    .AddIdentityCore<ApplicationUser>()
    .AddRoles<ApplicationRole>()
    .AddMartenStores();
```

`ApplicationUser` derives from `MartenIdentityUser`; a role-enabled host uses a
role type deriving from `MartenIdentityRole`. The package supports the standard
user/password/email/phone/security-stamp/lockout/two-factor stores; claims,
external logins, authentication tokens, authenticator/recovery codes, passkeys;
and optional roles, memberships, and role claims. It uses package-owned
lightweight Marten sessions and optimistic concurrency.

It supports single-tenanted documents only. It does not implement
`IProtectedUserStore<TUser>`: hosts enabling `ProtectPersonalData` must disable
it or use a different store. The provider does not define an application
profile, membership model, product entitlements, or authorization policy. Model
those separately in host-owned documents/services keyed by the Identity user ID.

## OpenIddict package boundary and maintenance limit

The source registration supplies OpenIddict core stores only:

```csharp
using Innovorium.OpenIddict.Marten;

builder.Services.AddOpenIddict()
    .AddCore(options => options.UseMarten());
```

It persists the default Marten application, authorization, scope, and token
entities. It does not turn the host into an authorization server. Server
endpoints, consent, issuer selection, signing/encryption keys, token validation,
client provisioning policy, custom entities, named document stores, multi-tenant
storage, and operations are explicit host choices.
Each database operation uses a package-owned lightweight session. Calling an
OpenIddict manager therefore never commits unrelated documents staged in the
host application's scoped Marten session.

OpenIddict revoke and prune operations deliberately process at most 1,000,000
matching rows per invocation, in batches of 1,000. This bounds memory use and
transaction pressure. Repeat prune when it returns 1,000,000. Do not blindly
repeat an unfiltered revoke call because already-revoked rows can be selected
again; partition larger revoke workloads with filters that exclude completed
rows, such as an applicable status predicate, while monitoring database load
and cancellation behavior.

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

While the project is below 1.0, documents and APIs may change in a minor
release. Keep a tested backup and rollback plan; do not treat the library as a
migration service.
