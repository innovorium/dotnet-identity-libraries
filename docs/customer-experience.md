# Customer experience

This page sets expectations for evaluators, adopters, and contributors to the
current providers.

## Start here

| If you need to… | Current answer | Next step |
| --- | --- | --- |
| Use Marten stores for ASP.NET Core Identity | Supported by functional releases | Install only the Identity package and follow the [consumption guide](consumption.md) |
| Use Marten stores for OpenIddict core | Supported by functional releases | Install only the OpenIddict package and follow the [consumption guide](consumption.md) |
| Review version-specific behavior | Recorded in the changelog | Read the [changelog](../CHANGELOG.md) and matching GitHub release |
| Report a documentation or package defect | Accepted | Use the repository's [bug report](https://github.com/innovorium/dotnet-identity-libraries/issues/new?template=bug.yml) |
| Propose a bounded future capability | Welcome | Start with [Discussions](https://github.com/innovorium/dotnet-identity-libraries/discussions), then open a focused proposal if it needs a tracked decision |
| Report a security concern | Accepted privately | Follow [SECURITY.md](../SECURITY.md) |

## Evaluating the source

The repository includes source samples for the Identity and OpenIddict
registrations. They are deliberately local-project examples and always reflect
the checked-out revision rather than proving a NuGet installation. See
[Identity sample](../samples/Identity/README.md) and
[OpenIddict sample](../samples/OpenIddict/README.md).

The samples set `AutoCreate.None` and fail when their host-owned connection
string is absent. They assume that the application team has already reviewed
and applied the required Marten schema in the target environment. Never put
production credentials in sample configuration or use automatic schema updates
as a deployment strategy.

## What consuming the provider means

The functional release provides a documented library integration, not a
hosted service or turnkey identity system. Consumers are expected to:

1. Choose the release version and verify its documented compatibility.
2. Configure their own PostgreSQL/Marten connection, credentials, deployment, backup, monitoring, and recovery practices.
3. Apply reviewed schema changes through their own delivery process; the library will not own production database lifecycle or automatic schema deployment.
4. Configure their own ASP.NET Core authentication endpoints, application authorization, OpenIddict server endpoints, consent flow, signing credentials, and issuer policy.
5. Test their exact application, persistence topology, and upgrade path before production adoption.

The library is not responsible for application profiles, organization membership, authorization policy, legacy-data migration, import/export, UI, administration, user support, or production operations.

### Keep identity separate from application profiles

The Identity user is an authentication and account record. Keep product-owned
profile data, such as a display preference, customer attributes, or workspace
membership, in host-owned documents and services keyed by the Identity user ID.
That separation keeps credential lifecycle and application-domain lifecycle
independent: the provider does not become a profile, tenancy, CRM, or helpdesk
model. It is the same practical boundary that keeps a support system's requester
identity distinct from the ticket or customer context it belongs to.

## Clear, bounded collaboration

Use a Discussion to establish whether an idea belongs here. For a request that needs action, provide one problem, an owning package, expected behavior, and its persistence, compatibility, and security implications. Use an issue only when the report is reproducible or the proposal is ready to be tracked. This keeps public history useful without treating maintainers as a private support desk.

Maintainers will keep the changelog, compatibility statements, and issue status as the public record of what is available. Discussion does not imply acceptance, timeline, support commitment, or API availability.

## Release boundary

The changelog and compatibility policy define the supported release boundary.
See [README](../README.md), [compatibility](compatibility.md), and
[support](../SUPPORT.md) for the corresponding technical and support statements.
