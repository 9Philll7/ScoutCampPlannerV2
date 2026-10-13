param([Parameter(Mandatory)][string]$Package,
    [string]$Executable = (Join-Path $PSScriptRoot '../src/desktop/sidecar-publish/ScoutCampPlanner.Api.exe'),
    [string]$DatabaseSnapshot,
    [string]$AuditSnapshotDirectory,
    [switch]$ExpectDatabaseError)
$ErrorActionPreference = 'Stop'
$directory = Join-Path ([IO.Path]::GetTempPath()) ('scp-seed-import-' + [Guid]::NewGuid())
New-Item -ItemType Directory -Path $directory | Out-Null
if ($DatabaseSnapshot) {
    if (-not $AuditSnapshotDirectory) { throw 'A database snapshot requires its matching audit directory.' }
    Copy-Item -LiteralPath $DatabaseSnapshot -Destination (Join-Path $directory 'local.db')
    Copy-Item -LiteralPath $AuditSnapshotDirectory -Destination (Join-Path $directory 'audit') -Recurse
}
$listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
$listener.Start()
$url = 'http://127.0.0.1:' + $listener.LocalEndpoint.Port
$listener.Stop()
$previousToken = $env:SingleDevice__AccessToken
$env:SingleDevice__AccessToken = [Guid]::NewGuid().ToString('N') + [Guid]::NewGuid().ToString('N')
$headers = @{ 'X-ScoutCampPlanner-Device' = $env:SingleDevice__AccessToken; Origin = 'http://tauri.localhost' }
$process = $null
try {
    $arguments = @('--urls', $url, '--Database:Provider=Sqlite',
        ('"--Database:ConnectionString=Data Source=' + (Join-Path $directory 'local.db') + '"'),
        ('"--Audit:Directory=' + (Join-Path $directory 'audit') + '"'), '--SingleDevice:Enabled=true')
    $process = Start-Process -FilePath $executable -ArgumentList $arguments -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $directory 'backend.log') -RedirectStandardError (Join-Path $directory 'backend.err')
    $ready = $false
    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        try { Invoke-RestMethod "$url/api/setup/status" -Headers $headers | Out-Null; $ready = $true; break }
        catch { Start-Sleep -Milliseconds 500 }
    }
    if (-not $ready) { throw 'Sidecar did not start.' }
    if ($ExpectDatabaseError) {
        $failed = $false
        try { Invoke-RestMethod "$url/api/packages/import-initial" -Method Post -Headers $headers -ContentType 'application/octet-stream' -InFile $Package | Out-Null }
        catch {
            $response = $_.Exception.Response
            if (-not $response -or [int]$response.StatusCode -ne 500) { throw }
            if ($response.Headers['Access-Control-Allow-Origin'] -ne $headers.Origin) { throw 'Database error lost its CORS header.' }
            $errorBody = $_.ErrorDetails.Message
            if (-not $errorBody) {
                $reader = [IO.StreamReader]::new($response.GetResponseStream())
                try { $errorBody = $reader.ReadToEnd() } finally { $reader.Dispose() }
            }
            if ($errorBody -notmatch 'package_database_error') { throw 'Database error response is not explicit.' }
            $failed = $true
        }
        if (-not $failed) { throw 'Expected a database error, but import succeeded.' }
        Write-Host 'PASS database error returns explicit HTTP 500 with CORS.'
    } else {
        Invoke-RestMethod "$url/api/packages/import-initial" -Method Post -Headers $headers -ContentType 'application/octet-stream' -InFile $Package | Out-Null
        Write-Host 'PASS package imported into isolated sidecar test database.'
    }
} finally {
    if ($process -and -not $process.HasExited) { Stop-Process -Id $process.Id }
    $env:SingleDevice__AccessToken = $previousToken
    Write-Host "Diagnostic data retained: $directory"
}
