# ScoutCampPlanner

ScoutCampPlanner is being developed as a modular monolith for cloud, local server, and Windows single-device operation. The Architecture Spike is complete; this repository is the validated technical baseline for subsequent product development, not a product release.

## Prerequisites

- .NET SDK 10.0.200
- Node.js 24.15 or newer
- PostgreSQL 18 or Docker for server-mode validation
- Rust 1.84 or newer for the Tauri build
- Visual Studio with the **Desktop development with C++** workload, MSVC x64 tools, and a Windows SDK

## Build and test

```powershell
dotnet build ScoutCampPlanner.slnx
dotnet test tests/ScoutCampPlanner.ArchitectureTests/ScoutCampPlanner.ArchitectureTests.csproj --no-build
dotnet test tests/ScoutCampPlanner.PackageTests/ScoutCampPlanner.PackageTests.csproj --no-build

Set-Location src/frontend
npm install
npm run build
```

## Run the SQLite backend

```powershell
dotnet run --project src/backend/ScoutCampPlanner.Api/ScoutCampPlanner.Api.csproj --urls http://127.0.0.1:5180
```

OpenAPI is available at `/openapi/v1.json` in the Development environment. The health endpoint is `/health`.

## Local PostgreSQL instance

```powershell
docker compose -f deploy/docker/compose.yml up --build
```

Run the PostgreSQL transaction test against that instance:

```powershell
$env:SCOUTCAMPPLANNER_POSTGRES_TEST = 'Host=127.0.0.1;Port=55432;Database=scoutcampplanner;Username=scoutcampplanner;Password=local-development-only'
dotnet test tests/ScoutCampPlanner.PackageTests/ScoutCampPlanner.PackageTests.csproj --filter "FullyQualifiedName~PostgreSqlPackageTests"
```

The compose file is intended for local development only. Its credentials must not be reused in another environment.

## Desktop preparation

```powershell
./tools/prepare-desktop.ps1
Set-Location src/desktop
npm install
npm run tauri build
```

The preparation script publishes the ASP.NET Core API as a Windows x64 self-contained sidecar using the filename expected by Tauri.

Successful bundles are written below:

```text
src/desktop/src-tauri/target/release/bundle/msi/
src/desktop/src-tauri/target/release/bundle/nsis/
```

## Architecture status

See the [architecture baseline](docs/architecture/baseline-status.md), [ADR-006](docs/decisions/adr-006-architecture-spike-validation.md), and the [spike report](docs/spike/results.md) for validated capabilities, open production decisions, remaining operational checks, and known limitations.
