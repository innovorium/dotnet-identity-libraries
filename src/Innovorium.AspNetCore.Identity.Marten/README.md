# Innovorium.AspNetCore.Identity.Marten

Marten-backed stores for ASP.NET Core Identity on .NET 10. The package persists Identity users, credentials, claims, logins, tokens, passkeys, and optional roles in PostgreSQL through the host application's Marten document store.

## Install

```bash
dotnet add package Innovorium.AspNetCore.Identity.Marten
```

Review the [changelog](https://github.com/innovorium/dotnet-identity-libraries/blob/main/CHANGELOG.md) before upgrading. While the project is below 1.0, public APIs and persisted contracts may change in a minor release.

## Configure

Configure Marten's connection before adding the Identity stores. The host owns the connection, credentials, PostgreSQL lifecycle, and schema policy.

```csharp
using Innovorium.AspNetCore.Identity.Marten;
using JasperFx;
using Marten;
using Microsoft.AspNetCore.Identity;

builder.Services.AddMarten(options =>
{
    options.Connection(
        builder.Configuration.GetConnectionString("Marten")
        ?? throw new InvalidOperationException(
            "ConnectionStrings:Marten is required."));
    options.AutoCreateSchemaObjects = AutoCreate.None;
});

builder.Services
    .AddIdentityCore<ApplicationUser>()
    .AddRoles<ApplicationRole>()
    .AddMartenStores();

public sealed class ApplicationUser : MartenIdentityUser;
public sealed class ApplicationRole : MartenIdentityRole;
```

For Identity without roles, omit `AddRoles<T>()` and the role type:

```csharp
builder.Services
    .AddIdentityCore<ApplicationUser>()
    .AddMartenStores();
```

`AddMartenStores()` replaces the registered Identity user store and, when roles are configured, the role store. It does not add an Identity UI, cookies, authentication schemes, authorization policy, or application profile model.

## Supported Identity capabilities

User support includes:

- users and queryable users;
- passwords, email, phone numbers, security stamps, lockout, and two-factor state;
- user claims and external logins;
- authentication tokens, authenticator keys, and recovery codes;
- passkeys;
- optional role membership.

Role-enabled registration adds queryable roles and role claims. Store writes use package-owned lightweight Marten sessions, optimistic concurrency, and standard `IdentityResult` errors for recognized uniqueness and concurrency failures.

## Constraints

- User types must derive from `MartenIdentityUser`; role types must derive from `MartenIdentityRole`. Both use ASP.NET Core Identity's string keys.
- Identity user and role documents must be single-tenanted. Marten conjoined tenancy is rejected.
- `IProtectedUserStore<TUser>` is not implemented. Keep `IdentityOptions.Stores.ProtectPersonalData` disabled or choose a provider that supports protected personal data.
- The package owns only the Identity persistence adapter. Your application still owns authentication configuration, authorization, secrets, operations, backups, recovery, and domain data associated with the user ID.

## Schema ownership

The package registers its Marten document mappings, indexes, optimistic-concurrency metadata, and relationship constraints. It does not select a database, change `AutoCreateSchemaObjects`, generate application migrations, or apply schema changes.

For production, keep `AutoCreateSchemaObjects = AutoCreate.None`. Run Marten's [official command-line tooling](https://martendb.io/configuration/cli.html) against the host application with its exact Identity and Marten registrations:

```csharp
builder.Host.ApplyJasperFxExtensions();

// Build and map the host as usual, then replace app.Run():
return await app.RunJasperFxCommands(args);
```

```bash
dotnet run --project <host-project> -- db-patch schema.sql --drop schema.drop.sql
dotnet run --project <host-project> -- db-assert
```

Review and deploy the generated forward SQL through your normal database release process. Treat the drop script and backup restoration as separately tested recovery paths; application instances should not require production DDL privileges.

## Project links

- [Repository and full documentation](https://github.com/innovorium/dotnet-identity-libraries)
- [Consumption and ownership guide](https://github.com/innovorium/dotnet-identity-libraries/blob/main/docs/consumption.md)
- [Report a reproducible bug](https://github.com/innovorium/dotnet-identity-libraries/issues/new?template=bug.yml)
- [Security policy](https://github.com/innovorium/dotnet-identity-libraries/blob/main/SECURITY.md)
- [MIT license](https://github.com/innovorium/dotnet-identity-libraries/blob/main/LICENSE)
