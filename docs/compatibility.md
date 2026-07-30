# Compatibility

## Supported compatibility

Both packages target `net10.0` only and remain independently installable.

| Package | Direct dependency ranges | Supported provider surface |
| --- | --- | --- |
| `Innovorium.AspNetCore.Identity.Marten` | Marten `[9.21.0, 10.0.0)`; `Microsoft.Extensions.Identity.Stores` `[10.0.10, 11.0.0)` | String-keyed `MartenIdentityUser` users, optional `MartenIdentityRole` roles, and the stores documented in the root README |
| `Innovorium.OpenIddict.Marten` | Marten `[9.21.0, 10.0.0)`; Npgsql `[9.0.4, 10.0.0)`; OpenIddict.Core `[7.6.0, 8.0.0)`; Weasel.Storage `[9.17.0, 10.0.0)` | Default Marten application, authorization, scope, and token entities/stores |

The upper bounds exclude the next major version until it is reviewed. Do not
add the other Innovorium package unless the host actually needs its provider.
The source samples use local projects and are not NuGet adoption evidence.

The default documents, aliases, indexes, and concurrency fields are
persisted-data contracts. This project supplies no
migrations and does not support `AutoCreate.CreateOrUpdate` as a production
upgrade mechanism. Generate, review, and deploy schema changes through the
host database process; retain a tested backup and rollback plan.

## Release policy

The changelog records version-specific public API, persistence compatibility,
upgrade guidance, and known limitations:

- The project does not supply migrations or an automatic upgrade path.
- No compatibility is implied with any ASP.NET Core Identity, OpenIddict, Marten, PostgreSQL, hosting, or deployment setup.
- While the project is below 1.0, APIs and persisted data may change in a minor release when documented in the changelog.
- Hosts remain responsible for testing the exact released version in their own deployment topology before adopting it.

## Version compatibility

Patch releases preserve documented public APIs and persisted contracts except
when a security or correctness defect makes that impossible. While the project
is below 1.0, a minor release may introduce breaking API or persisted-contract
changes; those changes must be called out in the changelog with explicit host
upgrade guidance. A major release is required for breaking changes after 1.0.

See [Customer experience](customer-experience.md) for consumption and responsibility boundaries.
