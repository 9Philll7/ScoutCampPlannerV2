$ErrorActionPreference = 'Stop'
$repository = Split-Path -Parent $PSScriptRoot
$executable = Join-Path $repository 'src/desktop/sidecar-publish/ScoutCampPlanner.Api.exe'
if (-not (Test-Path -LiteralPath $executable)) { throw 'Run tools/prepare-desktop.ps1 first.' }
$testDirectory = Join-Path ([IO.Path]::GetTempPath()) ('scp-roundtrip-' + [Guid]::NewGuid())
New-Item -ItemType Directory -Path $testDirectory | Out-Null
$processes = @()
$originalToken = $env:SingleDevice__AccessToken
function Free-Port {
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    $listener.Start()
    $port = $listener.LocalEndpoint.Port
    $listener.Stop()
    return $port
}
function Assert-Status($expected, [scriptblock]$action) {
    try { & $action | Out-Null } catch {
        if ([int]$_.Exception.Response.StatusCode -eq $expected) { return }
        throw
    }
    throw "Expected HTTP $expected."
}
try {
    $cloud = 'http://127.0.0.1:' + (Free-Port)
    $local = 'http://127.0.0.1:' + (Free-Port)
    $token = [Guid]::NewGuid().ToString('N') + [Guid]::NewGuid().ToString('N')
    $env:SingleDevice__AccessToken = $token
    $headers = @{ 'X-ScoutCampPlanner-Device' = $token }
    foreach ($mode in @('cloud', 'local')) {
        $url = if ($mode -eq 'cloud') { $cloud } else { $local }
        $enabled = if ($mode -eq 'cloud') { 'false' } else { 'true' }
        $arguments = @('--urls', $url, '--Database:Provider=Sqlite',
            ('"--Database:ConnectionString=Data Source=' + (Join-Path $testDirectory "$mode.db") + '"'),
            ('"--Audit:Directory=' + (Join-Path $testDirectory "$mode-audit") + '"'),
            "--SingleDevice:Enabled=$enabled", '--Logging:LogLevel:Default=Warning')
        $processes += Start-Process -FilePath $executable -ArgumentList $arguments -WindowStyle Hidden -PassThru `
            -RedirectStandardOutput (Join-Path $testDirectory "$mode.log") -RedirectStandardError (Join-Path $testDirectory "$mode.err")
        $ready = $false
        for ($attempt = 0; $attempt -lt 60; $attempt++) {
            try {
                Invoke-RestMethod "$url/api/setup/status" -Headers $headers | Out-Null
                $ready = $true; break
            } catch { Start-Sleep -Milliseconds 500 }
        }
        if (-not $ready) { throw "$mode failed to start. Logs: $testDirectory" }
    }
    Assert-Status 401 { Invoke-RestMethod "$local/api/session" }
    Assert-Status 403 { Invoke-RestMethod "$local/api/setup" -Method Post -Headers $headers -ContentType 'application/json' -Body '{}' }
    $password = [Guid]::NewGuid().ToString('N') + '!Aa9'
    $setup = @{ tenantName = 'Roundtrip test'; email = 'roundtrip@example.test'; password = $password } | ConvertTo-Json
    $created = Invoke-RestMethod "$cloud/api/setup" -Method Post -ContentType 'application/json' -Body $setup
    $login = @{ email = 'roundtrip@example.test'; password = $password } | ConvertTo-Json
    Invoke-RestMethod "$cloud/api/session" -Method Post -ContentType 'application/json' -Body $login -SessionVariable session | Out-Null
    $tenantId = $created.tenantId
    $candidates = @(Invoke-RestMethod "$cloud/api/tenants/$tenantId/camp-administrator-candidates" -WebSession $session)
    $body = @{ name = 'Roundtrip'; startDate = '2026-10-01'; endDate = '2026-10-03'; initialAdministratorMembershipIds = @($candidates[0].membershipId) } | ConvertTo-Json
    $camp = Invoke-RestMethod "$cloud/api/tenants/$tenantId/camps" -Method Post -ContentType 'application/json' -Body $body -WebSession $session
    $campId = $camp.id
    $outbound = Join-Path $testDirectory 'outbound.scoutcamp'
    Invoke-WebRequest "$cloud/api/camps/$campId/offline-package" -Method Post -WebSession $session -OutFile $outbound -UseBasicParsing
    $changed = @{ name = 'Locally edited'; startDate = '2026-10-01'; endDate = '2026-10-03' } | ConvertTo-Json
    Assert-Status 409 { Invoke-RestMethod "$cloud/api/camps/$campId" -Method Put -WebSession $session -ContentType 'application/json' -Body $changed }
    Invoke-RestMethod "$local/api/packages/import-initial" -Method Post -Headers $headers -ContentType 'application/octet-stream' -InFile $outbound | Out-Null
    $visible = @(Invoke-RestMethod "$local/api/tenants/$tenantId/camps" -Headers $headers)
    if ($visible.Count -ne 1 -or -not $visible[0].canEdit) { throw 'Imported camp is not editable.' }
    Invoke-RestMethod "$local/api/camps/$campId" -Method Put -Headers $headers -ContentType 'application/json' -Body $changed | Out-Null
    Assert-Status 403 { Invoke-RestMethod "$local/api/tenants/$tenantId/camps" -Method Post -Headers $headers -ContentType 'application/json' -Body $body }
    $returned = Join-Path $testDirectory 'return.scoutcamp'
    Invoke-WebRequest "$local/api/camps/$campId/return-package" -Method Post -Headers $headers -OutFile $returned -UseBasicParsing
    $remove = @{ transferId = $visible[0].activeTransferId; confirmLoss = $true } | ConvertTo-Json
    Assert-Status 403 { Invoke-RestMethod "$cloud/api/camps/$campId/remove-local-copy" -Method Post -WebSession $session -ContentType 'application/json' -Body $remove }
    Invoke-RestMethod "$local/api/camps/$campId/remove-local-copy" -Method Post -Headers $headers -ContentType 'application/json' -Body $remove | Out-Null
    $remainingTenants = Invoke-RestMethod "$local/api/tenants" -Headers $headers
    if ($remainingTenants.Count -ne 0) { throw 'Removed camp access remains.' }
    Invoke-RestMethod "$local/api/packages/import-initial" -Method Post -Headers $headers -ContentType 'application/octet-stream' -InFile $outbound | Out-Null
    Assert-Status 409 { Invoke-RestMethod "$cloud/api/packages/import-return?expectedCampId=$([Guid]::NewGuid())" -Method Post -WebSession $session -ContentType 'application/octet-stream' -InFile $returned }
    Invoke-RestMethod "$cloud/api/packages/import-return?expectedCampId=$campId" -Method Post -WebSession $session -ContentType 'application/octet-stream' -InFile $returned | Out-Null
    $result = @(Invoke-RestMethod "$cloud/api/tenants/$tenantId/camps" -WebSession $session)
    if ($result[0].isFrozen -or $result[0].name -ne 'Locally edited') { throw 'Returned changes or unfreeze missing.' }
    Assert-Status 409 { Invoke-RestMethod "$cloud/api/packages/import-return?expectedCampId=$campId" -Method Post -WebSession $session -ContentType 'application/octet-stream' -InFile $returned }
    Invoke-WebRequest "$cloud/api/camps/$campId/offline-package" -Method Post -WebSession $session -OutFile $outbound -UseBasicParsing
    $frozen = @(Invoke-RestMethod "$cloud/api/tenants/$tenantId/camps" -WebSession $session)[0]
    $cancel = @{ transferId = $frozen.activeTransferId; baselineVersion = $frozen.baselineVersion; confirmLoss = $true } | ConvertTo-Json
    Invoke-RestMethod "$cloud/api/camps/$campId/offline-transfer/cancel" -Method Post -WebSession $session -ContentType 'application/json' -Body $cancel | Out-Null
    Assert-Status 409 { Invoke-RestMethod "$cloud/api/camps/$campId/offline-transfer/cancel" -Method Post -WebSession $session -ContentType 'application/json' -Body $cancel }
    $recovered = @(Invoke-RestMethod "$cloud/api/tenants/$tenantId/camps" -WebSession $session)[0]
    if ($recovered.isFrozen -or $recovered.name -ne 'Locally edited') { throw 'Explicit recovery failed.' }
    Write-Host "PASS: HTTP roundtrip, local edit, access restrictions, stale-return rejection. Test artifacts: $testDirectory"
} finally {
    $env:SingleDevice__AccessToken = $originalToken
    foreach ($process in $processes) { if (-not $process.HasExited) { $process.Kill(); $process.WaitForExit() } }
}
