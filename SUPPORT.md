# Support

## Current release boundary

Support covers reproducible defects in the documented Identity and OpenIddict
storage behavior of the latest functional release.

There are no guaranteed response or resolution times. Participation is best effort and public by default, except for security reports.

## Choose the right channel

| Need | Channel | Include |
| --- | --- | --- |
| Question, adoption interest, design feedback, or workflow comparison | [GitHub Discussions](https://github.com/innovorium/dotnet-identity-libraries/discussions) | Goal, constraints, and the exact release or document considered |
| Reproducible defect in released behavior | [Bug report](https://github.com/innovorium/dotnet-identity-libraries/issues/new?template=bug.yml) | Package and version, .NET and dependency versions, smallest safe reproduction, expected and actual behavior |
| Focused request for future behavior or public API | [Feature proposal](https://github.com/innovorium/dotnet-identity-libraries/issues/new?template=feature.yml) | Problem, proposed contract, persistence, compatibility, and security effects |
| Vulnerability or exposure | [Private security report](https://github.com/innovorium/dotnet-identity-libraries/security/advisories/new) | Follow [SECURITY.md](SECURITY.md); do not disclose publicly |

For a provider release, report missing or incorrect documented behavior with a
safe reproduction. Package-reservation releases are limited to packaging or
documentation defects.

## What maintainers can act on

Maintainers can clarify documented scope, correct released package or documentation defects, and consider bounded proposals. They cannot take responsibility for host-owned PostgreSQL operations, credentials, schema deployment, backups, application authorization, authentication endpoints, signing keys, issuer configuration, or production recovery. See [docs/customer-experience.md](docs/customer-experience.md) for the full boundary.
