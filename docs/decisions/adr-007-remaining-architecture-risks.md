# ADR-007 Remaining Architecture Risks

## Status

Proposed

## Context

The Architecture Spike validated the technical foundation described by ADR-005 and ADR-006. It deliberately did not resolve every production concern. This document records the remaining architecture risks and the decisions required before affected product functionality is considered production-ready.

This ADR does not decide the open points. Each area requires a separate, evidence-based decision and validation.

## Database migrations

### Current state

- EF Core create scripts successfully initialize new PostgreSQL and SQLite databases.
- No production migration strategy or tested upgrade path exists.

### To clarify

- how PostgreSQL migrations are generated, reviewed, deployed, and rolled back
- how SQLite migrations are applied safely inside the single-device application
- how existing databases are upgraded across application versions
- whether provider-specific migrations are maintained separately
- how backups, failed upgrades, and recovery are handled
- which compatibility window is supported

### Risk

Without a migration and recovery strategy, releases cannot safely evolve existing installations. A successful clean database creation does not demonstrate upgrade safety.

### Required validation

- upgrade tests for PostgreSQL and SQLite from at least one previous schema version
- failure and recovery tests
- verification that module ownership remains visible in migrations
- validation in Docker/server and packaged desktop operation

## Camp-package migration

### Current state

- The package format contains a version and version 1 is validated.
- The implementation accepts exactly version 1.
- No migration registry or old-version compatibility fixture exists.

### To clarify

- the distinction and lifecycle of container, manifest, and module-payload schema versions
- the supported package compatibility window
- whether migration occurs stepwise through every version or directly to the current version
- where the migration pipeline runs and how it reports unsupported or invalid packages
- how old camp packages are retained as immutable compatibility fixtures
- how signatures or encryption interact with migration in a future secured format

### Risk

Without an explicit migration pipeline, a later package version could make existing offline packages unreadable or encourage ad-hoc compatibility logic in domain code.

### Required validation

- immutable fixtures for every supported historic version
- forward-migration and unsupported-version tests
- preservation tests for IDs, relationships, tenant/camp identity, and module boundaries
- atomic import and rollback tests after migration

## Privacy and health data

### Current state

- Package payloads are protected by a SHA-256 checksum.
- Packages are not encrypted or signed.
- Authentication, authorization, audit logging, retention, archival, and anonymisation were outside the spike.
- The domain documentation identifies health data and a privacy lifecycle, but the complete workflow is not defined.

### To clarify

- encryption at rest and in transit, including key ownership, rotation, recovery, and offline use
- package authenticity and integrity through signing or authenticated encryption
- tenant-, camp-, role-, and possibly module-specific permissions
- authorization for export, import, access to health data, and administrative operations
- audit events, audit retention, and protection of audit data
- retention periods, archival rules, deletion, and anonymisation
- data minimisation for packages and local installations
- secure cleanup of temporary files, local databases, exports, backups, and logs

### Risk

The validated package mechanism is not suitable for production health data. A checksum alone neither protects confidentiality nor proves the sender. Implementing participant or health-data features before these decisions would create a high-risk data path.

### Required validation

- threat model and privacy review before health-data implementation
- authorization and tenant-isolation tests
- cryptographic package tests, including tampering, wrong keys, and key rotation
- audit completeness and access-control tests
- archival, deletion, and anonymisation tests across cloud, local server, single-device, and package copies

## Decision boundary

Before production development depends on one of these areas, the corresponding design must be captured in a dedicated ADR and validated with automated tests. Until then:

- database upgrades are not production-ready
- package compatibility beyond version 1 is not guaranteed
- real health data must not be stored in or transferred through the spike implementation

