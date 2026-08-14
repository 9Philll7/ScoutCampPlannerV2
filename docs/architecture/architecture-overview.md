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
- Windows 11 x64 as the supported release reference
- security-maintained Windows 10 22H2 x64 as a tolerated transitional best-effort platform under [ADR-013](../decisions/adr-013-desktop-platform-support.md)

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

- Platform owns separate Domain, Application, and Infrastructure assemblies. Camp and Catering currently own separate Domain and Infrastructure assemblies; Application assemblies are introduced per module when the first product use cases require them.
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
3. Single device: Windows x64, Tauri, ASP.NET Core sidecar, and SQLite

All three operating models are technically confirmed. Windows 11 x64 is the supported desktop reference. Security-maintained Windows 10 22H2 x64 is tolerated on a transitional best-effort basis as defined by ADR-013. Clean-machine desktop installation/removal, normal user-driven shutdown, Windows 10 smoke coverage, and minimum-hardware validation remain release-readiness checks rather than architecture blockers.

## Authentication modes

[ADR-009](../decisions/adr-009-authentication-modes.md) defines the authentication direction:

- password authentication for cloud/server users
- cloud authentication for connected local server instances
- a separately derived local verifier using the same user password for prepared offline access
- an optional, device-local password for Windows single-device operation

Central password verifiers are not exported and local verifiers are not part of camp packages. Cloud and local server instances use instance-bound, server-managed cookie sessions. Tauri uses a per-launch channel secret and, when a local password is configured, an additional in-memory unlock session. ADR-009 defines password and session behavior, including Unicode-scalar length boundaries. ADR-014 removes the separate password denylist and requires the local strength estimator for every accepted password length. Platform provides application-owned password contracts and Infrastructure implementations for the agreed policy, versioned Argon2id verifiers, and rehash detection. The implementation isolates `Konscious.Security.Cryptography.Argon2` 1.3.1 and `zxcvbn-core` 7.0.92 in Infrastructure. User accounts, tenant memberships, separate password credentials, and tenant role assignments are persisted. Empty installations provide a one-time setup endpoint and Angular form that atomically create the first active owner account and tenant; normal authentication endpoints, sessions, audited identity workflows, and authorization endpoints are not yet implemented. ADR-010 assigns identities, memberships, and credential persistence to Platform and requires explicit tenant isolation. ADR-011 defines the extensible initial roles, permissions, and audit access roles; its versioned central role-to-permission catalogue and tenant assignment persistence are implemented. [ADR-012](../decisions/adr-012-security-audit-events.md) defines the security audit catalogue, Platform-owned append-only persistence, HMAC-chain integrity model, technical retention defaults, authorized access, and version-2 transfer direction. The [audit security validation](../spike/audit-security-validation.md) confirms canonical encoding and HMAC chaining on Windows and Linux, provider atomicity and concurrency, checkpoint recovery, key storage and rotation, blocked mode, backup and restore, monthly retention segments, provider-backed performance, and transfer-scoped package-version-2 encryption and signatures. Platform now provides the productive append contract, persistence migrations, active-key loader, byte-compatible canonical encoding, journal initialization, serialized event/head append transactions, and a Platform-local executor that commits or rolls back a business change and its audit event in one database transaction for both providers. Cross-module transaction coordination, checkpoint advancement, verification, host and use-case integration, package-version-2 serialization and migration, and legal retention review remain open.

## Still open

- database migrations are provider- and module-specific and SQLite pre-upgrade backups are automatic as defined by [ADR-008](../decisions/adr-008-database-migration-strategy.md); operational clean-machine restore validation remains open
- camp-package schema migration and compatibility beyond version 1
- package and sensitive-local-data encryption and signing, technical validation of the defined audit model, legal retention review, privacy retention, archival, and anonymisation
- Windows 10 compatibility benchmarking of the defined single-device Argon2id profile, audit/package-security spike validation, and legal retention review; authentication and operating-mode Argon2id profiles, Unicode password-length counting, password strength, identity, authorization, the initial password-security libraries, and the audit transfer direction are defined by ADR-009 through ADR-012, ADR-014, and the focused security-library validation
- stable API error contracts
- release-readiness checks for packaged desktop installers on the ADR-013 platform matrix, including final desktop Argon2id calibration

These open items must be resolved before the affected production functionality is released. They do not invalidate the proven technical baseline.

## Core principle

The repository documentation is the source of truth for architecture decisions.
