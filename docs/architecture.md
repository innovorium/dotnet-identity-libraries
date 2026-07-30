# Architecture

## Purpose

This repository provides independent Marten persistence integrations for ASP.NET Core Identity and OpenIddict. It is not an identity server, user interface, application framework, or migration product.

## Package boundaries

### Innovorium.AspNetCore.Identity.Marten

Owns ASP.NET Core Identity store implementations, default document models, Marten schema configuration, and dependency-injection registration.

It does not own application profiles, organization memberships, authorization policy, connection strings, database lifecycle, or schema deployment.

### Innovorium.OpenIddict.Marten

Owns OpenIddict application, authorization, scope, and token stores; their default document models; Marten schema configuration; and dependency-injection registration.

It does not own server endpoints, consent user experience, signing credentials, issuer selection, or application-domain users.

## Shared decisions

- Target `net10.0` only.
- Use stable ASP.NET Core Identity 10, OpenIddict 7, and Marten 9 releases.
- Keep the packages independently installable and free of cross-package dependencies.
- Use one repository release train initially; both packages share a version even when only one changes.
- Reuse framework contracts instead of introducing a public abstractions package.
- Use one host-owned, dependency-injection-scoped lightweight Marten session.
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
