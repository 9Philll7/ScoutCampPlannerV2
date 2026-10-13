param(
    [ValidateSet('Seed', 'Reset', 'Start')][string]$Action = 'Seed',
    [ValidateRange(1024, 65535)][int]$Port = 5180
)
$ErrorActionPreference = 'Stop'
$repository = Split-Path -Parent $PSScriptRoot
$directory = Join-Path $repository '.dev-data/meal-planning-increment-2'
$originalDotnet = $env:DOTNET_ENVIRONMENT
$originalAspnet = $env:ASPNETCORE_ENVIRONMENT
if (($originalDotnet -and $originalDotnet -notin @('Development', 'Test')) -or
    ($originalAspnet -and $originalAspnet -notin @('Development', 'Test'))) {
    throw 'Nur Development/Test erlaubt. Bestehende Production/Staging-Umgebung wird nicht ueberschrieben.'
}
try {
    $env:DOTNET_ENVIRONMENT = 'Development'
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    if ($Action -ne 'Start') {
        & dotnet build (Join-Path $PSScriptRoot 'ScoutCampPlanner.MealPlanningSeed') -m:1 /nr:false
        if ($LASTEXITCODE -ne 0) { throw 'Seed-Build fehlgeschlagen.' }
        $seedArguments = @('run', '--no-build', '--project', (Join-Path $PSScriptRoot 'ScoutCampPlanner.MealPlanningSeed'))
        if ($Action -eq 'Reset') { $seedArguments += @('--', '--reset') }
        & dotnet @seedArguments
        if ($LASTEXITCODE -ne 0) { throw 'Testdaten wurden nicht aktiviert. Bisheriger Stand bleibt erhalten.' }
        return
    }
    $manifest = Get-Content -LiteralPath (Join-Path $directory 'current.json') -Raw | ConvertFrom-Json
    if ($manifest.seed -ne 'meal-planning-increment-2-v1' -or $manifest.database -notmatch '^TEST-[0-9a-f]{32}\.db$') {
        throw 'Ungueltiger Testdaten-Zeiger. Keine Datenbank wird gestartet.'
    }
    $database = Join-Path $directory $manifest.database
    foreach ($path in @($directory, (Split-Path -Parent $directory), $database)) {
        if ((Get-Item -LiteralPath $path).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Umgeleitete Testdatenpfade sind nicht erlaubt.' }
    }
    # Never stop an unrelated backend. The user chooses whether to stop it or use another port.
    $occupied = Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue
    if ($occupied) { throw "Port $Port ist belegt. Bestehendes Backend selbst stoppen oder -Port verwenden." }
    Write-Host "TEST Backend: http://localhost:$Port; nur Dummy-Daten verwenden. Strg+C beendet es."
    Write-Host 'Login: acceptance@example.test / TEST-only-MealPlanning-2026!'
    & dotnet build (Join-Path $repository 'src/backend/ScoutCampPlanner.Api') -m:1 /nr:false
    if ($LASTEXITCODE -ne 0) { throw 'Backend-Build fehlgeschlagen.' }
    & dotnet run --no-build --project (Join-Path $repository 'src/backend/ScoutCampPlanner.Api') --no-launch-profile -- `
        --urls "http://localhost:$Port" --Database:Provider=Sqlite "--Database:ConnectionString=Data Source=$database" `
        --SingleDevice:Enabled=false "--Audit:Directory=$directory/audit-$($manifest.generation)"
    if ($LASTEXITCODE -ne 0) { throw 'TEST Backend wurde mit Fehler beendet.' }
} finally {
    $env:DOTNET_ENVIRONMENT = $originalDotnet
    $env:ASPNETCORE_ENVIRONMENT = $originalAspnet
}
