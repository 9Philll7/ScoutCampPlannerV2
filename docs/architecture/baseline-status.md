# Architecture Baseline Status

Evaluation date: 2026-08-08

## Current state

The Architecture Spike is complete and the repository is the validated technical baseline for subsequent development. It is not a product release.

Validated capabilities:

- Modular Monolith with enforced boundaries for Platform, Camp, and Catering
- separate module-owned Domain, Infrastructure, contracts, and EF Core contexts
- shared Domain and Application logic with PostgreSQL and SQLite
- module separation through PostgreSQL schemas and SQLite table naming
- provider-specific, module-owned EF Core migrations with tested SQLite and PostgreSQL upgrades
- shared transactions and atomic rollback across module contexts
- version 1 camp-package roundtrip from cloud-style PostgreSQL to local SQLite and back
- preservation of IDs and relationships, controlled freeze, baseline validation, atomic replacement, and rejection of invalid returns
- ASP.NET Core REST API, OpenAPI, Angular integration, and production frontend build
- Docker-based backend/PostgreSQL operation
- Tauri desktop operation with an ASP.NET Core sidecar, persistent SQLite database, process lifecycle handling, and generated MSI/NSIS bundles

The detailed evidence is in [ADR-006](../decisions/adr-006-architecture-spike-validation.md) and the [Architecture Spike report](../spike/results.md).

## Technical basis

- Backend: .NET 10, ASP.NET Core, Entity Framework Core, REST, OpenAPI
- Frontend: Angular 22, TypeScript, Angular Material, Angular CDK
- Server database: PostgreSQL 18
- Single-device database: SQLite
- Local server deployment: Docker Compose with ASP.NET Core and PostgreSQL
- Windows desktop: Tauri 2 with a self-contained ASP.NET Core sidecar
- Architecture: Modular Monolith with directional contracts and module-owned persistence
- Offline transfer: versioned ZIP camp package with JSON payload and SHA-256 checksum; no automatic synchronization or merge

## Still open

The following decisions are required before the affected areas are production-ready:

- operational backup-restore validation on a packaged clean-machine installation; migrations and automatic SQLite pre-upgrade backups are defined by ADR-008
- a package migration registry, historic compatibility fixtures, and a supported compatibility window
- encryption and authenticity protection for packages and sensitive local data
- final Argon2id parameter calibration, versioned password denylist, audit/package-security spike validation and legal retention review, and privacy lifecycle; authentication, Unicode password-length counting, identity, authorization, the initial password-security libraries, and the audit transfer direction are defined by ADR-009 through ADR-012 and the focused security-library validation
- retention, archival, deletion, and anonymisation rules for personal and health data
- a stable API Problem Details contract
- clean-machine installer/removal and normal desktop shutdown checks
- remediation of the recorded Angular development-server dependency advisory when a compatible patched release is available

ADR-007 records the architecture risks and required validation without pre-deciding their solutions.

## Next development step

Before implementing health data or other sensitive workflows, resolve the remaining security, authorization, and privacy decisions in ADR-007. Authentication modes are defined by ADR-009 and the production database migration strategy by ADR-008.

After those foundations are decided, the recommended first product slice is tenant authentication/authorization and the basic Camp structure workflow. It should retain the validated module boundaries and add only the contracts, persistence migrations, API behavior, frontend flow, and tests required for that slice. Catering, Finance, Program, Material, and expanded participant/health functionality should follow as separate increments rather than extending the spike indiscriminately.
