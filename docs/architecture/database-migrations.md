# Database Migration Workflow

## Restore the repository tool

From the repository root:

```powershell
dotnet tool restore
```

The local manifest pins `dotnet-ef` to the EF Core version used by the solution.

## One-time transition from the spike

Databases created by the former EF create-script bootstrap have no migration history and are intentionally not adopted automatically. Startup detects this state and stops with a diagnostic error. The spike was not a product release, so create a fresh development database after preserving any test data you still need.

For a former desktop test database, close ScoutCampPlanner and move the file to a recoverable backup name:

```powershell
$spikeDatabase = Join-Path $env:LOCALAPPDATA 'org.scoutcampplanner.desktop\scoutcampplanner.db'
if (Test-Path -LiteralPath $spikeDatabase) {
    Move-Item -LiteralPath $spikeDatabase -Destination "$spikeDatabase.spike-backup"
}
```

For the repository-root SQLite backend, do the same with `scoutcampplanner.db` if it exists.

The Docker development database also needs a fresh volume if it was created by the spike bootstrap. This destroys only the Compose-managed development volume, so export anything needed first:

```powershell
docker compose -f deploy/docker/compose.yml down --volumes
docker compose -f deploy/docker/compose.yml up --build
```

Do not use these reset steps for any database containing product or otherwise valuable data.

## Create a migration

Every model change needs one migration for PostgreSQL and one for SQLite. Replace `<Module>` with `Platform`, `Camp`, or `Catering`, `<Context>` with its `DbContext` type, and `<Name>` with the same descriptive migration name for both providers.

SQLite:

```powershell
dotnet ef migrations add <Name> `
  --context <Context> `
  --project src/backend/ScoutCampPlanner.Migrations.Sqlite/ScoutCampPlanner.Migrations.Sqlite.csproj `
  --startup-project src/backend/ScoutCampPlanner.Api/ScoutCampPlanner.Api.csproj `
  --output-dir <Module> `
  -- `
  --Database:Provider=Sqlite `
  "--Database:ConnectionString=Data Source=migration-design.db"
```

PostgreSQL model generation does not connect to the design-time database, but a syntactically valid connection string is required:

```powershell
dotnet ef migrations add <Name> `
  --context <Context> `
  --project src/backend/ScoutCampPlanner.Migrations.PostgreSql/ScoutCampPlanner.Migrations.PostgreSql.csproj `
  --startup-project src/backend/ScoutCampPlanner.Api/ScoutCampPlanner.Api.csproj `
  --output-dir <Module> `
  -- `
  --Database:Provider=PostgreSql `
  "--Database:ConnectionString=Host=127.0.0.1;Database=design;Username=design;Password=design"
```

Never reuse a generated migration from one provider in the other provider's project.

## Review checklist

- Migration and snapshot belong to the correct module and provider.
- No migration modifies a table owned by another module.
- PostgreSQL schema and SQLite table names match the architecture rules.
- Both providers express the same domain outcome.
- Destructive operations include an explicit data-preservation and recovery plan.
- The previous supported database upgrades successfully and retains its data.
- A fresh database reaches the same current model.
- The application has no pending migrations after startup.

## Apply and verify

Application startup applies Platform, Camp, and Catering migrations in that order. PostgreSQL uses an advisory migration lock. Before production execution:

1. Stop writes or place the deployment in maintenance mode.
2. Create and verify a PostgreSQL backup, or create the release-managed SQLite backup.
3. Deploy one reviewed application version and observe the migration logs.
4. Verify `/health` and the migration integration tests.
5. Resume normal operation only after application and data checks succeed.

The PostgreSQL integration test requires:

```powershell
$env:SCOUTCAMPPLANNER_POSTGRES_TEST = 'Host=127.0.0.1;Port=55432;Database=scoutcampplanner;Username=scoutcampplanner;Password=local-development-only'
dotnet test tests/ScoutCampPlanner.DatabaseMigrationTests/ScoutCampPlanner.DatabaseMigrationTests.csproj
```

The test database schemas are dropped and recreated. Never point this variable at a database containing valuable data.
