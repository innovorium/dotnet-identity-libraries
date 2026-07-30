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
4. Cake's package inspection verifies dependency bounds, portable symbols, README, license, and repository metadata.
5. A clean consumer can restore and compile against the produced packages.
6. The protected `release` GitHub environment approves publication.

Release artifacts include packages, symbol packages, and `SHA256SUMS`.
GitHub releases generated from tags containing a prerelease suffix are marked as prereleases automatically.

NuGet.org publication uses trusted publishing with GitHub OIDC and a short-lived API key. Configure a NuGet trusted-publishing policy for the Innovorium owner, this repository, `release.yml`, and the `release` environment. NuGet's `login` action requires the public username of the policy creator, which is declared directly in the workflow; no long-lived NuGet credential or repository secret is required.

## Failure policy

NuGet packages are immutable. Never overwrite or reuse a version. If a release is defective, mark it deprecated on NuGet.org, document the issue, and publish a corrected version. Delete or replace a Git tag only before publication; after publication, provenance must remain intact.
