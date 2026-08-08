# ADR-008 Database Migration Strategy

## Status

Accepted

## Context

The Architecture Spike initialized new databases with EF Core create scripts. This proved that the current model works with PostgreSQL and SQLite, but it did not provide an upgrade path for existing installations.

Platform, Camp, and Catering own separate `DbContext` instances. PostgreSQL and SQLite also have different schema capabilities. A single shared migration history or one provider-neutral migration set would hide module ownership and make provider-specific changes unsafe.

## Decision

### Migration assemblies

Migrations are separated by database provider:

- `ScoutCampPlanner.Migrations.PostgreSql`
- `ScoutCampPlanner.Migrations.Sqlite`

Each assembly contains separate migration folders and model snapshots for Platform, Camp, and Catering. The migration projects are composition projects: they may reference all current module Infrastructure projects, but module Infrastructure and Domain projects must not reference a migration project.

The repository pins `dotnet-ef` through `.config/dotnet-tools.json`. Migration generation must use the repository-local tool and the correct provider project.

### Migration history

Each module owns a separate EF Core migration history:

- PostgreSQL: `platform.__EFMigrationsHistory`, `camp.__EFMigrationsHistory`, and `catering.__EFMigrationsHistory`
- SQLite: `__EFMigrationsHistory_platform`, `__EFMigrationsHistory_camp`, and `__EFMigrationsHistory_catering`

This prevents one context from treating another module's migration as applied and keeps module evolution independently traceable.

### Application order

The application applies migrations in dependency order:

1. Platform
2. Camp
3. Catering

PostgreSQL obtains a database advisory lock before schema creation and migration. This serializes competing application starts. SQLite single-device operation assumes one application process owns the database file.

EF Core records each completed module migration. Module migrations are not wrapped in one cross-module schema transaction. If a later module fails, deployment stops and a subsequent start resumes from the recorded state. Destructive or non-transactional changes require explicit deployment and recovery instructions in the migration review.

### Compatibility baseline

The generated initial migrations establish the first production migration baseline. The former create-script databases belong to the Architecture Spike and are not automatically adopted because the spike was not a product release.

Startup detects a database with spike tables but without the module migration history and stops with an explicit diagnostic instead of attempting to create duplicate tables. Required spike test data must be backed up separately before a fresh development database is created.

The migration tests retain data while upgrading Camp and Catering from their initial table migration to the current index migration. Future schema changes must extend this chain; the initial migrations must not be edited after this baseline is released.

### Backup and recovery

- PostgreSQL deployments must create and verify an external database backup before applying migrations.
- Desktop releases must back up the SQLite database before an upgrade that contains pending migrations. Automated desktop backup and retention behavior must be completed before the first product release.
- Down migrations support local development and tests but are not the default production rollback mechanism.
- Production recovery restores the pre-deployment backup or deploys a reviewed forward-fix migration.

## Consequences

- New persistent model changes require migrations for both providers and the owning module.
- Pull requests must review generated operations, snapshots, destructive changes, provider differences, and deployment notes.
- Upgrade tests must start from the previous supported migration and verify data preservation for PostgreSQL and SQLite.
- Application startup no longer uses generated create scripts.
- PostgreSQL upgrades require a reachable database and backup procedure. SQLite upgrade backup automation remains a release blocker tracked by ADR-007.
- Package schema migration remains independent of database migration and is not solved by this ADR.

## Validation

Validated on 2026-08-08:

- SQLite upgrades Platform, Camp, and Catering from the initial migration state to the current state while preserving existing tenant, camp, cooking-unit, and meal-plan data.
- PostgreSQL performs the same V1-to-V2 upgrade with separate module schemas and histories while preserving the same data and relationships.
- Both providers apply the Camp and Catering index migrations and report no pending migrations after a repeated update.
- The existing PostgreSQL package rollback integration test still succeeds after introducing migrations.
- The Docker backend image includes both migration assemblies, starts against the migrated PostgreSQL volume, reports that the database is current, and returns a healthy PostgreSQL status.

The pre-ADR-008 PostgreSQL spike database was backed up before resetting the dedicated development schemas. Automated SQLite release backup and restore validation remain open as described above and in ADR-007.
