# ScoutCampPlanner Architecture Overview

## Validation status

The target architecture defined by ADR-005 was technically validated by the Architecture Spike on 2026-08-08. The validation and its evidence are recorded in [ADR-006](../decisions/adr-006-architecture-spike-validation.md) and the [spike report](../spike/results.md).

The validated repository state is a development baseline, not a product release. Operational restore validation, package compatibility, security, privacy, and selected desktop release concerns remain open and are tracked in [ADR-007](../decisions/adr-007-remaining-architecture-risks.md).

## Technical baseline

Backend:
- ASP.NET Core / .NET
- Entity Framework Core
- REST API
- OpenAPI

Frontend:
- Angular
- TypeScript
- Angular Material
- Angular CDK
- AG Grid Community only where required

Database:
- PostgreSQL server operation
- PostgreSQL for local Docker-based camp instances
- SQLite single-device operation

Desktop:
- Tauri with local ASP.NET Core sidecar

Architecture:
- Modular Monolith

The following technology decisions are confirmed by executable builds and tests:

- shared Domain and Application logic with PostgreSQL and SQLite
- ASP.NET Core REST API and OpenAPI
- Angular production build and API integration
- Docker-based ASP.NET Core/PostgreSQL operation
- Tauri 2 with a self-contained ASP.NET Core sidecar and SQLite

## Current module model

The validated module dependency direction is:

`Platform → Camp → Catering`

- Platform, Camp, and Catering each own separate Domain and Infrastructure assemblies.
- Platform and Camp expose explicit contracts to downstream consumers.
- Each module owns its data and EF Core context.
- Domain assemblies remain independent of framework, persistence-provider, UI, and other module implementations.
- Finance, Program, and Material remain future modules and were not part of the spike.

## Persistence boundaries

- Each module owns its own EF Core `DbContext`.
- All module contexts use the same physical database for an application instance.
- PostgreSQL uses a separate database schema per module.
- SQLite uses module-specific table names because SQLite does not support schemas.
- Cross-module EF Core navigation properties are not allowed.
- References across module boundaries use stable IDs and are resolved or validated through module contracts.

## Camp package transfer

- Camp packages are versioned domain-level transfer packages. A raw SQLite database file is not used as the transfer format.
- Starting an offline phase freezes the camp in the source system for write operations.
- Every offline transfer has a transfer ID and a baseline version.
- A return package is accepted only for the matching active transfer and baseline.
- Diverging data is not merged automatically. A mismatch causes a controlled import rejection.
- A successful return import atomically replaces the complete camp-related data of every module listed in the package manifest.
- Other camps, modules not listed in the manifest, tenant-wide data, and central catalogue data remain unchanged.
- Platform references required by the package, such as the tenant ID, do not authorize replacement of tenant or user data.

These decisions were validated for PostgreSQL and SQLite by the Architecture Spike. Shared transactions, atomic replacement, rollback, identity preservation, and the version 1 package validation rules are covered by automated tests.

## Operating modes

1. Cloud/server: ASP.NET Core and PostgreSQL
2. Local camp instance: Docker-based ASP.NET Core and PostgreSQL
3. Single device: Windows, Tauri, ASP.NET Core sidecar, and SQLite

All three operating models are technically confirmed. Clean-machine desktop installation/removal and normal user-driven shutdown remain release-readiness checks rather than architecture blockers.

## Still open

- database migrations are provider- and module-specific and SQLite pre-upgrade backups are automatic as defined by [ADR-008](../decisions/adr-008-database-migration-strategy.md); operational clean-machine restore validation remains open
- camp-package schema migration and compatibility beyond version 1
- encryption, signing, permissions, audit logging, retention, archival, and anonymisation
- authentication, tenant authorization, and the concrete role model
- stable API error contracts
- release-readiness checks for packaged desktop installers

These open items must be resolved before the affected production functionality is released. They do not invalidate the proven technical baseline.

## Core principle

The repository documentation is the source of truth for architecture decisions.
