# Development

## Prerequisites

- .NET SDK 10.0.302, managed by `mise`
- PostgreSQL when integration tests are introduced
- Cake 6.2.0, restored from the repository's .NET tool manifest
- `gitleaks` for the local secret scan

Activate the repository toolchain:

```bash
mise install
mise activate
dotnet tool restore
```

## Verification

Run the complete local gate:

```bash
dotnet cake --target Verify
```

Cake tasks include `Clean`, `Restore`, `Format`, `Build`, `Test`, `Pack`, `Inspect-Packages`, `Consumer-Smoke-Test`, `Checksums`, `Security`, `Verify`, and `Release`. Select one with `dotnet cake --target <task>`.

`build.cake` is the only build orchestration surface. Add Cake addins or modules only when Cake's built-in aliases cannot express a required capability, pin every extension version, and keep package or framework behavior out of the build layer.

MinVer derives the shared package version from Git history and `v` tags. Full history is therefore required when packaging in CI. Cake verifies release tags and a clean worktree through the Git CLI. Cake.Git is intentionally not loaded because its current 5.0.1 package supports x64 only, while this repository must build on both x64 CI and ARM64 contributor machines.

Build-only packages such as MinVer and analyzers must use `PrivateAssets="all"`. The `Inspect-Packages` task reads each generated NuSpec and fails unless its consumer-visible dependency set exactly matches the package's approved runtime/API dependencies.

## Dependency policy

- Production dependencies use current verified stable releases.
- Preview packages require an accepted design issue and must not silently replace stable dependencies.
- Central versions live in `Directory.Packages.props`; project files declare dependencies without versions.
- Lock files are committed and CI restores in locked mode.
- Dependabot proposes version updates; maintainers review release notes and compatibility before merging.

## Schema policy

Libraries register Marten schema configuration. Hosts own PostgreSQL connections and schema deployment. Production applications must disable automatic resource creation and apply reviewed schema changes through their own deployment process.
