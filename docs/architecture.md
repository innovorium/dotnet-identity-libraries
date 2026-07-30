# Architecture

This document records the intended provider contract. The published
`0.1.0-alpha.2` packages do not implement it; see the README for current release
status.

## Purpose

This repository is intended to provide independent Marten persistence integrations for ASP.NET Core Identity and OpenIddict. It is not an identity server, user interface, application framework, or migration product.

## Package boundaries

### Innovorium.AspNetCore.Identity.Marten

Will own ASP.NET Core Identity store implementations, default document models, Marten schema configuration, and dependency-injection registration.

It does not own application profiles, organization memberships, authorization policy, connection strings, database lifecycle, or schema deployment.

### Innovorium.OpenIddict.Marten

Will own OpenIddict application, authorization, scope, and token stores; their default document models; Marten schema configuration; and dependency-injection registration.

It does not own server endpoints, consent user experience, signing credentials, issuer selection, or application-domain users.

## Shared decisions

- Target `net10.0` only.
- Use stable ASP.NET Core Identity 10, OpenIddict 7, and Marten 9 releases.
- Keep the packages independently installable and free of cross-package dependencies.
- Use one repository release train initially; both packages share a version even when only one changes.
- Reuse framework contracts instead of introducing a public abstractions package.
- Keep Identity commits isolated in a package-owned scoped lightweight session
  so an Identity operation cannot accidentally flush unrelated host work.
- Follow OpenIddict's scoped-store convention for its stores and document that
  a mutating store operation flushes the scoped Marten session.
- Enforce uniqueness in PostgreSQL indexes, not application pre-checks.
- Use Marten optimistic concurrency for mutable security documents.
- Keep automatic production schema changes disabled.
- Read authentication and revocation state from the PostgreSQL primary.
- Keep OpenIddict records issuer-global unless a host deliberately models separate issuers.

## Explicit non-goals

- Legacy database migration, import, export, or dual-write tooling.
- Event-sourced Identity or OpenIddict models.
- UI, account-management pages, or administrative consoles.
- A repository or unit-of-work abstraction over Marten.
- Multi-targeting older .NET versions.
- A shared public utility package without an independently proven consumer.

## Delivery sequence

1. Repository, build, package, security, and release foundation.
2. Complete ASP.NET Core Identity store surface and manager-level verification.
3. OpenIddict application and scope stores.
4. OpenIddict authorization and token stores, including pruning and revocation.
5. Optional fail-closed Identity tenancy after the global model is proven.

Public APIs are introduced only with behavior tests and documentation. Package boundaries or persisted document shapes change only through an explicit design decision.

## Initial customer contracts

The Identity provider starts with string identifiers and conventional
`UserManager`/`RoleManager` integration. Its first public surface is limited to
extensible Identity user and role documents, concrete stores, and
`IdentityBuilder.AddMartenStores()`. Complete Identity 10 coverage includes
passkeys before the provider is described as complete. Personal-data protection
is not claimed until `IProtectedUserStore<TUser>` is implemented and verified.

The OpenIddict provider starts with one opinionated `Guid`-identified document
model and `OpenIddictCoreBuilder.UseMarten()`. Application and scope stores are
the first delivery slice. Authorization and token stores follow only after the
registration, query, concurrency, and schema contracts are proven against
PostgreSQL. Custom entities, named document stores, and multi-tenant OpenIddict
storage are deferred until a concrete customer need justifies their permanent
API cost.

Both registration methods add mappings and store services only. They never set
a connection string, change Marten's schema auto-creation policy, apply schema
changes, or own database deployment.

## Persisted compatibility

Document identifiers, JSON property meanings, explicit Marten aliases, named
indexes, uniqueness constraints, and relationship semantics are compatibility
contracts. Changes to them require an explicit design decision and release note.
Uniqueness is enforced in PostgreSQL and race-related exceptions are translated
only when a package-owned named constraint is recognized; unknown database
failures remain visible to the host.
