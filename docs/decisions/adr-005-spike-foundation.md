# ADR-005 Architecture Spike Foundation

## Status

Accepted

## Context

The architecture spike must validate the modular monolith, PostgreSQL and SQLite operation, offline camp packages, and atomic return imports. Several implementation choices must be fixed before the spike so that it validates one coherent target architecture.

## Decision

### Module persistence

Each module owns a separate EF Core `DbContext`:

- `PlatformDbContext`
- `CampDbContext`
- `CateringDbContext`

The contexts share one physical database per application instance.

PostgreSQL uses a separate schema for each module. SQLite uses module-specific table names because it has no schema support.

Modules must not use EF Core navigation properties across module boundaries. Cross-module references are represented by stable IDs and are resolved or validated through the owning module's contracts.

### Database by operating mode

- Cloud operation uses PostgreSQL.
- Local Docker-based camp instances use PostgreSQL.
- Single-device operation with Tauri uses SQLite.

SQLite is not the database for the local Docker-based multi-user instance.

### Camp package format

Offline transfer uses a versioned, domain-level camp package. A raw SQLite database file is not the interchange format.

The package contains a manifest that identifies at least:

- package format version
- tenant
- camp
- transfer ID
- baseline version
- included modules

The concrete container format, payload serialization, integrity protection, and package migration mechanism are to be validated by the architecture spike.

### Freeze and return validation

Starting an offline transfer freezes the affected camp for write operations in the source system. The transfer records a unique transfer ID and the source baseline version.

A return package is accepted only if its tenant, camp, transfer ID, and baseline match the active transfer. Parallel changes remain prohibited. If the source state diverges, the system rejects the import with a diagnostic result; it does not automatically merge changes.

### Atomic camp replacement

A successful return import atomically replaces the complete camp-related data for every module listed in the package manifest.

The replacement must not modify:

- other camps
- modules not listed in the manifest
- tenant-wide data
- users
- central catalogue data

The package may carry Platform references such as a tenant ID, but these references do not authorize replacing Platform-owned tenant or user data.

Atomic means that either all included module data is committed successfully or none of it is changed. The architecture spike must validate a shared transaction across the participating module `DbContext` instances with both PostgreSQL and SQLite.

## Consequences

- Module ownership is technically visible and can be enforced by architecture tests.
- Cross-module queries and relationships require explicit contracts instead of direct EF Core access.
- Module migrations and provider differences require explicit organization.
- Package contracts are decoupled from the internal database schema.
- Return imports need package validation, freeze-state validation, and a transaction coordinator inside the application composition layer.
- Package migration and backwards compatibility become explicit responsibilities.
- The spike must demonstrate the complete PostgreSQL to SQLite to PostgreSQL round trip while preserving stable IDs and relationships.

## Spike validation required

This ADR defines the intended architecture but does not claim that it has already been proven. The spike must specifically validate:

- separate module contexts using one physical database
- schema handling in PostgreSQL and table naming in SQLite
- shared atomic transactions across module contexts
- versioned package creation and validation
- preservation of IDs and relationships
- rejection of stale, mismatched, or repeated return packages
- rollback when any included module import fails
