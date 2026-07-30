# Innovorium.OpenIddict.Marten

Marten persistence for OpenIddict core on .NET 10 and PostgreSQL.

This package registers opinionated, `Guid`-identified Marten documents and stores for OpenIddict applications, authorizations, scopes, and tokens. Use the standard OpenIddict managers and descriptors; the package adds storage, not an identity server.

## Install

```bash
dotnet add package Innovorium.OpenIddict.Marten
```

Review the [changelog](https://github.com/innovorium/dotnet-identity-libraries/blob/main/CHANGELOG.md) before upgrading. While the project is below 1.0, public APIs and persisted contracts may change in a minor release.

## Register Marten and OpenIddict

The host owns the PostgreSQL connection and Marten lifecycle. Register Marten first, then select these stores for OpenIddict core:

```csharp
using JasperFx;
using Marten;
using Microsoft.Extensions.DependencyInjection;

var connectionString = builder.Configuration.GetConnectionString("Marten")
    ?? throw new InvalidOperationException("ConnectionStrings:Marten is required.");

builder.Services.AddMarten(options =>
{
    options.Connection(connectionString);
    options.AutoCreateSchemaObjects = AutoCreate.None;
});

builder.Services.AddOpenIddict()
    .AddCore(options => options.UseMarten());
```

`UseMarten()` registers the default application, authorization, scope, and token entities and their OpenIddict stores. It does not select a connection, change `AutoCreateSchemaObjects`, or apply database changes. Register a custom `TimeProvider` before `UseMarten()` when token pruning must use a host-controlled clock; otherwise `TimeProvider.System` is used.

Each database operation opens and disposes a package-owned lightweight Marten session. An OpenIddict mutation therefore cannot flush unrelated work pending in the host application's scoped `IDocumentSession`, and a failed operation cannot leave queued work for a later call.

## Schema ownership

The host owns schema generation, review, deployment, backup, and recovery. Keep automatic production changes disabled, generate a patch from the host containing the exact Marten and OpenIddict registrations, review and apply it through your database delivery process, then assert the deployed schema:

Expose Marten's JasperFx commands in the host before `builder.Build()`, then use
the command runner instead of `app.Run()`:

```csharp
builder.Host.ApplyJasperFxExtensions();

// Build and map the host as usual, then:
return await app.RunJasperFxCommands(args);
```

```bash
dotnet run --project src/YourHost/YourHost.csproj -- db-patch schema.sql --drop schema.drop.sql
dotnet run --project src/YourHost/YourHost.csproj -- db-assert
```

See Marten's official [command-line tooling](https://martendb.io/configuration/cli.html) and [schema migrations and patches](https://martendb.io/schema/migrations) documentation. This package does not ship or execute migrations.

## Maintenance bounds

Authorization and token revocation and pruning process rows in batches of 1,000, with a ceiling of 1,000 batches—1,000,000 matching rows—per invocation. Returned counts are the rows changed or removed by that invocation.

When prune returns `1,000,000`, treat the result as potentially truncated and
invoke prune again under the host's scheduling, cancellation, load, and retry
policy. Do not blindly repeat an unfiltered revoke call: already-revoked rows
can be selected again. For more than 1,000,000 revoke targets, partition the
operation with filters that exclude completed rows, such as an applicable
status predicate, or another stable host-owned partitioning strategy.

Revocation detects revision drift between selection and update and raises an
OpenIddict concurrency exception instead of revoking a record that no longer
matches; retry or repartition according to the host's concurrency policy.

## Scope and limits

- Supported storage: the package's single-tenanted application, authorization, scope, and token documents in the default Marten document store.
- Not supported: custom OpenIddict entity types, named Marten stores, or multi-tenant OpenIddict storage.
- Not included: OpenIddict server or validation configuration, endpoints, issuer selection, consent UI, signing or encryption credentials, client-provisioning policy, application users, or authorization policy.
- Not owned: PostgreSQL credentials, schema deployment, migrations, backups, monitoring, or recovery.

Configure OpenIddict server or validation components separately when the host needs them.

## Project links

- [Source and documentation](https://github.com/innovorium/dotnet-identity-libraries)
- [Compatibility policy](https://github.com/innovorium/dotnet-identity-libraries/blob/main/docs/compatibility.md)
- [Report a bug](https://github.com/innovorium/dotnet-identity-libraries/issues/new?template=bug.yml)
- [Ask or discuss](https://github.com/innovorium/dotnet-identity-libraries/discussions)
- [Security policy](https://github.com/innovorium/dotnet-identity-libraries/blob/main/SECURITY.md)
- [MIT license](https://github.com/innovorium/dotnet-identity-libraries/blob/main/LICENSE)
