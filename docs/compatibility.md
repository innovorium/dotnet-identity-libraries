# Compatibility

## `0.1.0-alpha.2`: build baseline, not provider compatibility

`0.1.0-alpha.2` is a published, nonfunctional foundation release. Its package IDs are reserved, but neither package exposes a provider API or performs persistence. The table records the dependencies against which the assemblies and repository foundation were built; it is **not** a supported application-integration or production-compatibility guarantee.

| Component | Pinned supported range | Resolved foundation build |
| --- | --- | --- |
| .NET | `net10.0` only | SDK 10.0.302 / runtime 10.0.10 |
| ASP.NET Core Identity stores | `[10.0.10, 11.0.0)` | `Microsoft.Extensions.Identity.Stores` 10.0.10 |
| Marten | `[9.21.0, 10.0.0)` | 9.21.0 |
| OpenIddict core | `[7.6.0, 8.0.0)` | 7.6.0 |

The ranges exclude the next major version until it has been reviewed. Preview dependencies are outside this baseline.

## Preview policy

When a functional prerelease is announced, its release notes will state the exact supported package and framework versions, public API status, persistence/document compatibility, upgrade guidance, and any known limitations. Until then:

- There is no provider behavior to rely on and no migration or upgrade path to promise.
- No compatibility is implied with any ASP.NET Core Identity, OpenIddict, Marten, PostgreSQL, hosting, or deployment setup.
- Prerelease APIs and persisted data, once introduced, may change before a stable major release.
- Hosts remain responsible for testing the exact released version in their own deployment topology before adopting it.

See [Customer experience](customer-experience.md) for consumption and responsibility boundaries.
