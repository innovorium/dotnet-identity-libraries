# Architecture

This document records the current provider contract. While the project is below
1.0, public and persisted contracts may change in a minor release;
version-specific changes belong in the [changelog](../CHANGELOG.md).

## Purpose

This repository is intended to provide independent Marten persistence integrations for ASP.NET Core Identity and OpenIddict. It is not an identity server, user interface, application framework, or migration product.

## Package boundaries

### Innovorium.AspNetCore.Identity.Marten

Owns ASP.NET Core Identity store implementations, default document models,
Marten schema configuration, and dependency-injection registration.

It does not own application profiles, organization memberships, authorization policy, connection strings, database lifecycle, or schema deployment.

### Innovorium.OpenIddict.Marten

Owns OpenIddict application, authorization, scope, and token stores; their
default document models; Marten schema configuration; and dependency-injection
registration.

It does not own server endpoints, consent user experience, signing credentials, issuer selection, or application-domain users.

## Shared decisions

- Target `net10.0` only.
- Use stable ASP.NET Core Identity 10, OpenIddict 7, and Marten 9 releases.
- Keep the packages independently installable and free of cross-package dependencies.
- Use one repository release train initially; both packages share a version even when only one changes.
- Reuse framework contracts instead of introducing a public abstractions package.
- Keep Identity commits isolated in a package-owned scoped lightweight session
  so an Identity operation cannot accidentally flush unrelated host work.
- Keep OpenIddict database operations in package-owned, per-operation
  lightweight sessions so a store mutation never flushes unrelated work from
  the host application's scoped Marten session.
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

## Delivery sequence and release boundary

1. Repository, build, package, security, and release foundation.
2. Identity and OpenIddict store implementation, persistence contracts, and
   source samples.
3. Release-grade compatibility review, PostgreSQL coverage, and published
   package documentation.
4. Optional fail-closed Identity tenancy only after the global model is proven.

Public APIs are introduced only with behavior tests and documentation. Package boundaries or persisted document shapes change only through an explicit design decision.

## Initial customer contracts

The Identity provider uses string identifiers and conventional
`UserManager`/`RoleManager` integration. Its source surface uses extensible
Identity user and role documents, concrete stores, and
`IdentityBuilder.AddMartenStores()`. Personal-data protection is not claimed
until `IProtectedUserStore<TUser>` is implemented and verified in a release.

The OpenIddict provider uses one opinionated `Guid`-identified document model
and `OpenIddictCoreBuilder.UseMarten()`. Its source surface includes application,
authorization, scope, and token stores. Custom entities, named document stores,
and multi-tenant OpenIddict storage remain deferred until a concrete customer
need justifies their permanent API cost.

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
