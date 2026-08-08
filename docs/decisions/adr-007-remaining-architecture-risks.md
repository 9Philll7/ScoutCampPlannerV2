# ADR-007 Remaining Architecture Risks

## Status

Proposed

## Context

The Architecture Spike validated the technical foundation described by ADR-005 and ADR-006. It deliberately did not resolve every production concern. This document records the remaining architecture risks and the decisions required before affected product functionality is considered production-ready.

This ADR does not decide the open points. Each area requires a separate, evidence-based decision and validation.

## Database migrations

The migration structure and upgrade policy were decided in [ADR-008](adr-008-database-migration-strategy.md). The remaining release concern is automated SQLite backup/retention and operational verification of backup restoration.

### Current state

- Provider-specific EF Core migration assemblies now initialize and upgrade PostgreSQL and SQLite databases.
- Module-specific migration histories and a PostgreSQL advisory migration lock are implemented.
- SQLite and PostgreSQL upgrade preservation are covered by automated V1-to-V2 tests; the PostgreSQL test runs when a dedicated test connection is configured and was validated against the Docker development database on 2026-08-08.
- Automated SQLite pre-upgrade backup and retention are not implemented yet.

### To clarify

- how the desktop release creates, retains, and restores automatic SQLite pre-upgrade backups
- how backup restoration is exercised in release validation
- how long database upgrades from older product releases remain supported

### Risk

Without automated SQLite backup and regularly exercised restore procedures, a failed desktop upgrade could still require manual recovery. Every future migration also needs an upgrade fixture representing the previous supported product schema.

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
