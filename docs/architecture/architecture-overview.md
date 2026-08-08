# ScoutCampPlanner Architecture Overview

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

These decisions define the target architecture. Their technical feasibility, including shared transactions across module `DbContext` instances for PostgreSQL and SQLite, must be validated by the architecture spike.

## Operating modes

1. Cloud/server
2. Local lager instance
3. Single device

## Core principle

The repository documentation is the source of truth for architecture decisions.
