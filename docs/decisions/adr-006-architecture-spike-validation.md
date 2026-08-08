# ADR-006 Architecture Spike Validation

## Status

Accepted

## Context

ADR-005 defined the architecture foundation that the Architecture Spike had to test. The spike implemented a minimal vertical slice through Platform, Camp, and Catering. Its purpose was to validate the technical feasibility of the target architecture, not to deliver product functionality.

The validation was completed on 2026-08-08. The detailed evidence and executed commands are recorded in [`docs/spike/results.md`](../spike/results.md).

## Goal of the spike

The spike tested whether one shared Domain and Application approach can support the three intended operating models while preserving module boundaries and enabling controlled offline camp transfer:

- cloud/server operation with PostgreSQL
- a local Docker-based camp instance with PostgreSQL
- Windows single-device operation with Tauri, an ASP.NET Core sidecar, and SQLite

It also tested the complete camp-package path from a cloud-style database to a local database and back, including identity preservation, validation, atomic replacement, and rollback.

## Tested architecture decisions

- Modular Monolith with directional dependencies between Platform, Camp, and Catering
- separate Domain and Infrastructure assemblies and a module-owned EF Core `DbContext`
- PostgreSQL schemas and SQLite module-specific table names
- shared transactions across module contexts
- versioned domain-level camp packages instead of database-file transfer
- freeze, transfer ID, baseline, integrity, and package-direction validation
- atomic return import and rejection of stale, repeated, mismatched, or modified packages
- Angular integration with the ASP.NET Core REST API and OpenAPI
- Tauri desktop hosting with a self-contained ASP.NET Core sidecar
- Docker-based PostgreSQL and backend operation

## Confirmed decisions

- The Modular Monolith and its current module boundaries are technically viable. Architecture tests enforce the essential dependency rules.
- Platform, Camp, and Catering can own separate Domain and Infrastructure areas and separate EF Core contexts while using one physical database.
- The same Domain and Application logic operates with PostgreSQL and SQLite; provider-specific behavior remains in Infrastructure and composition.
- PostgreSQL supports separate schemas for the three modules. SQLite supports the intended separation through module-specific table names.
- All participating module contexts can join one transaction with both providers. The PostgreSQL rollback test confirmed that a failure in a later module does not leave earlier module changes committed.
- The version 1 ZIP camp package can complete the Cloud → Local → Cloud roundtrip while preserving IDs and relationships.
- Freeze, transfer, baseline, direction, checksum, and included-module validation work for the tested package version. Return import atomically replaces the included Camp and Catering data and does not replace Platform-owned data.
- The ASP.NET Core API, OpenAPI output, Angular production build, and frontend/API integration work together.
- Docker Compose can run the backend with PostgreSQL and report a healthy database connection.
- Tauri can start the ASP.NET Core sidecar with SQLite, reuse its local database, stop the sidecar with the parent process, and produce MSI and NSIS bundles.

## Known limitations

- Database initialization uses EF Core create scripts. A production migration and database-upgrade strategy does not yet exist.
- Package version 1 exists, but there is no package migration registry or compatibility fixture for an older version.
- The package checksum detects changes but does not provide encryption, authenticity, or a digital signature.
- Authentication, authorization, the concrete role model, audit logging, and the privacy lifecycle were outside the spike.
- Real health data must not be transferred with the current unencrypted and unsigned package implementation.
- Only Camp and Catering contribute domain data to the package. Platform supplies only the tenant snapshot needed to bootstrap the local instance and is not replaced on return.
- API errors do not yet have a stable Problem Details contract.
- Clean-machine installation/removal and normal user-driven Tauri shutdown remain operational checks before a desktop release.
- The Angular development server has a known moderate transitive dependency advisory recorded in the spike report; production bundles do not contain that dependency.

## Consequences

- ADR-005 is technically validated and remains the architecture foundation for subsequent development.
- The repository can be used as the stable baseline for product development; the spike itself is not a product release.
- New product work must retain the proven module ownership, dependency direction, provider-independent Domain/Application logic, and explicit camp-package boundary.
- Production development must address the remaining migration, package-compatibility, security, privacy, and operational risks recorded in ADR-007.
- Adding further modules or business functionality requires explicit contracts and corresponding architecture, integration, and compatibility tests.

