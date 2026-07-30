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
6. The `release` GitHub environment has required reviewers, prevents
   self-review and administrator bypass, and approves publication.

Release artifacts include packages, symbol packages, and `SHA256SUMS`.
GitHub releases generated from tags containing a prerelease suffix are marked as prereleases automatically.

The release workflow keeps build inputs away from publishing credentials. Its read-only `build` job checks out without persisted credentials, runs the Cake `Release` target, and uploads only the verified package set. The downstream `publish` job starts only after the protected `release` environment approves it, downloads that immutable workflow artifact without checking out repository code, verifies `SHA256SUMS`, creates package attestations, and publishes the same files to NuGet.org and the GitHub release.

NuGet.org publication uses trusted publishing with GitHub OIDC and a short-lived API key requested immediately before publication. Configure and verify a NuGet trusted-publishing policy for the Innovorium owner, this repository, `release.yml`, and the `release` environment before creating a release tag. NuGet's `login` action requires the public username of the policy creator, which is declared directly in the workflow; no long-lived NuGet credential or repository secret is required.

## Failure policy

NuGet packages are immutable. Never overwrite or reuse a version. If a release is defective, mark it deprecated on NuGet.org, document the issue, and publish a corrected version. Delete or replace a Git tag only before publication; after publication, provenance must remain intact.
