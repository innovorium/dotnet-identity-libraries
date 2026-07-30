# Customer experience

This page sets expectations for evaluators, adopters, and contributors. It is intentionally explicit because the current release is a foundation, not a working provider.

## Start here

| If you need to… | Current answer | Next step |
| --- | --- | --- |
| Use Marten stores for ASP.NET Core Identity | Not available | Follow releases or discuss the intended use case in [Discussions](https://github.com/innovorium/dotnet-identity-libraries/discussions) |
| Use Marten stores for OpenIddict | Not available | Follow releases or discuss the intended use case in [Discussions](https://github.com/innovorium/dotnet-identity-libraries/discussions) |
| Validate a package ID or release pipeline | Available in `0.1.0-alpha.2` | Inspect the [release](https://github.com/innovorium/dotnet-identity-libraries/releases/tag/v0.1.0-alpha.2) and its NuGet packages |
| Report a documentation or package defect | Accepted | Use the repository's [bug report](https://github.com/innovorium/dotnet-identity-libraries/issues/new?template=bug.yml) |
| Propose a bounded future capability | Welcome | Start with [Discussions](https://github.com/innovorium/dotnet-identity-libraries/discussions), then open a focused proposal if it needs a tracked decision |
| Report a security concern | Accepted privately | Follow [SECURITY.md](../SECURITY.md) |

## What consuming a future provider will mean

A future functional release will provide a documented library integration, not a hosted service or turnkey identity system. Consumers will be expected to:

1. Choose the release version and verify its documented compatibility.
2. Configure their own PostgreSQL/Marten connection, credentials, deployment, backup, monitoring, and recovery practices.
3. Apply reviewed schema changes through their own delivery process; the library will not own production database lifecycle or automatic schema deployment.
4. Configure their own ASP.NET Core authentication endpoints, application authorization, OpenIddict server endpoints, consent flow, signing credentials, and issuer policy.
5. Test their exact application, persistence topology, and upgrade path before production adoption.

The library is not responsible for application profiles, organization membership, authorization policy, legacy-data migration, import/export, UI, administration, user support, or production operations.

## Clear, bounded collaboration

Use a Discussion to establish whether an idea belongs here. For a request that needs action, provide one problem, an owning package, expected behavior, and its persistence, compatibility, and security implications. Use an issue only when the report is reproducible or the proposal is ready to be tracked. This keeps public history useful without treating maintainers as a private support desk.

Maintainers will keep release notes, compatibility statements, and issue status as the public record of what is available. Discussion does not imply acceptance, timeline, support commitment, or API availability.

## Current release

`0.1.0-alpha.2` has no public provider API. Do not install it to build an application. See [README](../README.md), [compatibility](compatibility.md), and [support](../SUPPORT.md) for the corresponding release, technical, and support statements.
