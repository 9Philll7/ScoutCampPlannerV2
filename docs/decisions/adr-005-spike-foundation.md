# ADR-005 Architecture Spike Foundation

## Status

Accepted

## Context

The architecture spike was intended to validate the modular monolith, PostgreSQL and SQLite operation, offline camp packages, and atomic return imports. Several implementation choices were fixed before implementation so that the spike validated one coherent target architecture.

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

The concrete container format, payload serialization, and integrity protection were left to the architecture spike. Package migration beyond the initial format remained an explicit follow-up concern.

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

Atomic means that either all included module data is committed successfully or none of it is changed. The architecture spike was required to validate a shared transaction across the participating module `DbContext` instances with both PostgreSQL and SQLite.

## Consequences

- Module ownership is technically visible and can be enforced by architecture tests.
- Cross-module queries and relationships require explicit contracts instead of direct EF Core access.
- Module migrations and provider differences require explicit organization.
- Package contracts are decoupled from the internal database schema.
- Return imports need package validation, freeze-state validation, and a transaction coordinator inside the application composition layer.
- Package migration and backwards compatibility become explicit responsibilities.
- The spike must demonstrate the complete PostgreSQL to SQLite to PostgreSQL round trip while preserving stable IDs and relationships.

## Spike validation outcome

The Architecture Spike completed the required validation on 2026-08-08:

- separate module contexts using one physical database
- schema handling in PostgreSQL and table naming in SQLite
- shared atomic transactions across module contexts
- versioned package creation and validation
- preservation of IDs and relationships
- rejection of stale, mismatched, or repeated return packages
- rollback when any included module import fails

All listed points were confirmed for the implemented Platform, Camp, and Catering slice. [ADR-006](adr-006-architecture-spike-validation.md) records the accepted result, evidence, limitations, and consequences. Remaining production risks are tracked in [ADR-007](adr-007-remaining-architecture-risks.md).
