# Architecture Spike Results

## Scope

This spike implements the minimum vertical slice through Platform, Camp, and Catering. It is an architecture proof, not complete product functionality.

Evaluation date: 2026-08-08.

## Confirmed

### Modular monolith

- Platform, Camp, and Catering have separate Domain and Infrastructure assemblies.
- Platform and Camp expose explicit contract assemblies.
- Each module owns its EF Core `DbContext`.
- Domain assemblies do not reference ASP.NET Core, EF Core, Npgsql, or another module's implementation.
- Architecture tests enforce the most important dependency rules.

### SQLite and package roundtrip

- The three module contexts operate on one physical SQLite connection.
- Initial and return imports enlist all participating contexts in one database transaction.
- A versioned ZIP package carries a JSON payload and SHA-256 checksum.
- Tenant, camp, transfer, baseline, direction, and included modules are validated.
- A tested cloud-to-local-to-cloud simulation preserves entity IDs and relationships.
- Offline changes replace the included cloud module data.
- Successful return import ends the freeze.
- Repeated or stale return packages are rejected without changing existing data.
- A modified package is rejected by integrity validation.

### PostgreSQL and Docker

- PostgreSQL 18 starts through Docker Compose on the dedicated host test port `55432`.
- Platform, Camp, and Catering create and use their intended PostgreSQL schemas.
- All module contexts share one physical Npgsql connection and transaction.
- A PostgreSQL return import preserves IDs and replaces the included data successfully.
- An intentionally duplicated Catering entity causes the import to fail and rolls back the earlier module changes.
- The original Catering data remains present after rollback.
- The backend Docker image builds reproducibly with the pinned .NET SDK policy.
- The complete backend/PostgreSQL Compose stack starts successfully.
- The containerized `/health` endpoint reports PostgreSQL as healthy.
- Containerized OpenAPI generation exposes six API paths.

### Backend and frontend

- The ASP.NET Core API builds with zero warnings and exposes REST endpoints and OpenAPI.
- A real first start against a file-based SQLite database completed successfully.
- The `/health` endpoint returned a healthy SQLite result.
- The Angular 22 production build completed successfully.
- The feature-oriented frontend calls the Camp API and can download an initial camp package.

### Tauri

- Rust 1.97.1, MSVC 14.50, and the Windows SDK compile the Tauri 2 application successfully.
- The self-contained ASP.NET Core sidecar publishes with the required Tauri target-triple filename.
- The desktop application starts the sidecar with SQLite configuration and stores the database below the application-specific local data directory.
- Sidecar `/health` reports a healthy SQLite provider from the running desktop build.
- The existing SQLite database is reused after rebuilding and restarting the desktop application.
- The sidecar monitors the Tauri parent process and terminates automatically after forced parent termination; no orphan API process remains.
- WiX produces an x64 MSI bundle successfully.
- NSIS produces an x64 setup executable successfully.
- A project-specific tent and calendar app icon is included in the executable and both bundles.

## Known limitations

- Database initialization uses generated EF Core create scripts for a new database. Production migrations for both providers are not implemented yet.
- Package version 1 has no migration registry or old package fixture yet.
- Packages are checksummed but not encrypted or signed. They are not suitable for real health data.
- Authentication, roles, tenant authorization, audit logging, and privacy lifecycle are outside this spike slice.
- The initial package includes a tenant snapshot to bootstrap the local instance; return import does not replace Platform data.
- Only the Camp and Catering module contributions exist.
- API error responses are not yet mapped to a stable problem-details contract.
- Installation and removal on a clean Windows test machine have not yet been performed.
- Database upgrade behavior remains dependent on the missing production migration strategy.
- The current Angular CLI dependency reports a moderate development-time path traversal advisory in a transitive static-file server. No patched Angular 22 CLI release was available during evaluation. Production bundles do not include this dependency; `ng serve` should remain limited to trusted local development until upgraded.

## Commands executed successfully

```text
dotnet build ScoutCampPlanner.slnx
dotnet test tests/ScoutCampPlanner.ArchitectureTests/ScoutCampPlanner.ArchitectureTests.csproj --no-build
dotnet test tests/ScoutCampPlanner.PackageTests/ScoutCampPlanner.PackageTests.csproj --no-build
npm run build (src/frontend)
GET http://127.0.0.1:5180/health
SCOUTCAMPPLANNER_POSTGRES_TEST=... dotnet test ...PostgreSqlPackageTests
docker compose -f deploy/docker/compose.yml up -d --build backend
GET http://127.0.0.1:5180/openapi/v1.json
npm run tauri build (src/desktop)
Desktop start -> SQLite health -> forced parent termination -> sidecar exit
```

Test result: 6 passed, 0 failed, including the PostgreSQL integration test.

## Remaining exit criteria

The implemented architecture spike is technically validated. Before treating the desktop bundles as a releasable product installer, these operational checks remain:

1. Install and remove either the MSI or NSIS bundle on a clean Windows test machine.
2. Verify normal user-driven window shutdown in addition to the proven forced-termination behavior.
3. Implement and validate database upgrade behavior once production migrations exist.
