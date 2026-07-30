# Releases

## Versioning

The two packages initially share one SemVer release train. They remain independently installable and neither package depends on the other.

- Tags use `vMAJOR.MINOR.PATCH` or a valid SemVer prerelease suffix.
- MinVer derives versions from full Git history, using `v` as the tag prefix and `0.1` as the initial version line.
- The Cake `Release` target requires a clean worktree and proves that the immutable tag exactly matches the MinVer package version.
- Breaking public API, persisted-document, normalization, index-scope, tenancy-default, or supported-upstream-major changes require a major release after 1.0.
- Before 1.0, breaking changes are allowed only when clearly documented in the changelog.

## Release requirements

Before creating a tag:

1. `dotnet cake --target Verify` passes at the exact target commit.
2. CI and CodeQL are green.
3. Public API files and `CHANGELOG.md` describe the release.
4. Cake's package inspection verifies exact dependency ranges, shared package versions, portable symbols with commit-bound SourceLink, README, MIT license metadata, and the repository URL and commit.
5. A clean consumer can restore and compile against the produced packages.
6. Before publication, an authorized release owner verifies the configured
   GitHub environment protections and NuGet trusted-publishing policy.

Release artifacts include packages, symbol packages, and `SHA256SUMS`; the
workflow also creates GitHub artifact attestations for the package files.
GitHub generates the release description from the commits and pull requests
included in the tag. The changelog remains the maintained release history.
GitHub releases generated from tags containing a prerelease suffix are marked as prereleases automatically.

The release workflow is designed to keep build inputs away from publishing
credentials: its read-only build job should run the Cake `Release` target and
upload only the verified package set; publication should consume that immutable
artifact, verify `SHA256SUMS`, create attestations, and publish the same files.
Before release, an authorized owner must verify that the active workflow and
environment actually enforce those expectations.

NuGet.org publication is intended to use trusted publishing with GitHub OIDC
and a short-lived API key requested immediately before publication. Before a
tag is created, an authorized release owner must verify the active NuGet policy
and its repository, workflow, environment, and policy-creator settings. No
long-lived NuGet credential or repository secret should be required.

## Failure policy

NuGet packages are immutable. Never overwrite or reuse a version. If a release is defective, mark it deprecated on NuGet.org, document the issue, and publish a corrected version. Delete or replace a Git tag only before publication; after publication, provenance must remain intact.
