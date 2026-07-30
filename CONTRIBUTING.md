# Contributing

Thank you for helping improve the Innovorium .NET Identity Libraries.

## Before opening a change

- Use a GitHub issue for material features or public API changes.
- Keep Identity and OpenIddict concerns in their owning packages.
- Do not add migration tooling, UI, or application-domain authorization concerns.
- Report security issues privately according to [SECURITY.md](SECURITY.md).

## Development workflow

1. Create a focused branch from `main`.
2. Make one cohesive change with tests and documentation.
3. Run `dotnet cake --target Verify`.
4. Update `CHANGELOG.md` when behavior or public API changes.
5. Open a pull request and explain compatibility, persistence, and security effects.

All contributions must follow the [Code of Conduct](CODE_OF_CONDUCT.md). By contributing, you agree that your contribution is licensed under the repository's MIT License.
