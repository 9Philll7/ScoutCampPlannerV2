$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repositoryRoot 'src/backend/ScoutCampPlanner.Api/ScoutCampPlanner.Api.csproj'
$publishDirectory = Join-Path $repositoryRoot 'src/desktop/sidecar-publish'
$binaryDirectory = Join-Path $repositoryRoot 'src/desktop/src-tauri/binaries'
$targetBinary = Join-Path $binaryDirectory 'ScoutCampPlanner.Api-x86_64-pc-windows-msvc.exe'

New-Item -ItemType Directory -Force -Path $publishDirectory, $binaryDirectory | Out-Null
dotnet publish $project -c Release -r win-x64 --self-contained true -o $publishDirectory /p:PublishSingleFile=true -m:1 /nr:false
if ($LASTEXITCODE -ne 0) {
    throw "Sidecar publish failed with exit code $LASTEXITCODE."
}
Copy-Item -LiteralPath (Join-Path $publishDirectory 'ScoutCampPlanner.Api.exe') -Destination $targetBinary -Force

Write-Host "Prepared Tauri sidecar: $targetBinary"
