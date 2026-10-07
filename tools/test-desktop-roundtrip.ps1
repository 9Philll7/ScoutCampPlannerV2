param([switch]$IncludeParticipants)
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
    if ($IncludeParticipants) {
        $members = @(Invoke-RestMethod "$cloud/api/camps/$campId/explicit-permissions" -WebSession $session)
        foreach ($permission in @('health.participant-requirements.read', 'health.participant-requirements.edit')) {
            $grant = @{ permission = $permission; granted = $true } | ConvertTo-Json
            Invoke-RestMethod "$cloud/api/camps/$campId/explicit-permissions/$($members[0].membershipId)" -Method Put -WebSession $session -ContentType 'application/json' -Body $grant | Out-Null
        }
        $nodeBody = @{ name = 'Dummy leaf'; parentId = $null } | ConvertTo-Json
        $node = Invoke-RestMethod "$cloud/api/camps/$campId/structure" -Method Post -WebSession $session -ContentType 'application/json' -Body $nodeBody
        $dummy = @{ displayName = 'Dummy participant'; structureNodeId = $node.id; dietTypeId = $null; absentDays = @(); absentMealIds = @(); allergenIds = @(); intolerances = @() } | ConvertTo-Json
        Invoke-RestMethod "$cloud/api/camps/$campId/participants" -Method Post -WebSession $session -ContentType 'application/json' -Body $dummy | Out-Null
        $people = Invoke-RestMethod "$cloud/api/camps/$campId/participants" -WebSession $session
        $unitBody = @{ name = 'Dummy kitchen'; sortOrder = 0; defaultStructureNodeIds = @($node.id); participantFilter = 0 } | ConvertTo-Json
        Invoke-RestMethod "$cloud/api/camps/$campId/cooking-units" -Method Post -WebSession $session -ContentType 'application/json' -Body $unitBody | Out-Null
        $planning = Invoke-RestMethod "$cloud/api/camps/$campId/meal-planning" -WebSession $session
        $unitId = $planning.cookingUnits[0].id
        $planningBody = @{ expectedVersion = 0; demandMode = 1 } | ConvertTo-Json
        Invoke-RestMethod "$cloud/api/camps/$campId/meal-planning/participants" -Method Put -WebSession $session -ContentType 'application/json' -Body $planningBody | Out-Null
    }
    $outbound = Join-Path $testDirectory 'outbound.scoutcamp'
    Invoke-WebRequest "$cloud/api/camps/$campId/offline-package" -Method Post -WebSession $session -OutFile $outbound -UseBasicParsing
    $changed = @{ name = 'Locally edited'; startDate = '2026-10-01'; endDate = '2026-10-03' } | ConvertTo-Json
    Assert-Status 409 { Invoke-RestMethod "$cloud/api/camps/$campId" -Method Put -WebSession $session -ContentType 'application/json' -Body $changed }
    Invoke-RestMethod "$local/api/packages/import-initial" -Method Post -Headers $headers -ContentType 'application/octet-stream' -InFile $outbound | Out-Null
    $visible = @(Invoke-RestMethod "$local/api/tenants/$tenantId/camps" -Headers $headers)
    if ($visible.Count -ne 1 -or -not $visible[0].canEdit) { throw 'Imported camp is not editable.' }
    if ($IncludeParticipants) {
        Assert-Status 404 { Invoke-RestMethod "$local/api/camps/$campId/participants" -Headers $headers }
        Assert-Status 404 { Invoke-RestMethod "$local/api/camps/$campId/meal-planning" -Headers $headers }
        foreach ($permission in @('health.participant-requirements.read', 'health.participant-requirements.edit')) {
            $commandArguments = @('--Database:Provider=Sqlite',
                ('"--Database:ConnectionString=Data Source=' + (Join-Path $testDirectory 'local.db') + '"'),
                ('"--Audit:Directory=' + (Join-Path $testDirectory 'local-audit') + '"'),
                '--local-permission-command', 'grant', '--local-permission-camp', $campId,
                '--local-permission-transfer', $visible[0].activeTransferId,
                '--local-permission-name', $permission, '--local-permission-confirm', 'true')
            $commandProcess = Start-Process -FilePath $executable -ArgumentList $commandArguments -WindowStyle Hidden -Wait -PassThru `
                -RedirectStandardOutput (Join-Path $testDirectory "$permission.log") -RedirectStandardError (Join-Path $testDirectory "$permission.err")
            if ($commandProcess.ExitCode -ne 0) { throw 'Explicit local participant grant failed.' }
        }
        $participantView = Invoke-RestMethod "$local/api/camps/$campId/participants" -Headers $headers
        $document = $participantView.participants[0]
        $document.data.displayName = 'Locally edited dummy participant'
        $document.data.absentDays = @('2026-10-02')
        $participantUpdate = @{ expectedStateToken = $document.stateToken; data = $document.data } | ConvertTo-Json -Depth 8
        Invoke-RestMethod "$local/api/camps/$campId/participants/$($document.data.id)" -Method Put -Headers $headers -ContentType 'application/json' -Body $participantUpdate | Out-Null
        $configuration = Invoke-RestMethod "$local/api/camps/$campId/meal-planning/participants" -Headers $headers
        if ($configuration.demandMode -ne 1 -or $document.data.structureNodeId -ne $node.id) { throw 'Camp participant structure import failed.' }
        $localPlanning = Invoke-RestMethod "$local/api/camps/$campId/meal-planning" -Headers $headers
        if (($localPlanning | ConvertTo-Json -Depth 20) -match 'Locally edited dummy participant') { throw 'Name leaked into catering planning.' }
        $localUnit = $localPlanning.cookingUnits[0]
        $localUnit.participantFilter = 2
        Invoke-RestMethod "$local/api/camps/$campId/cooking-units/$unitId" -Method Put -Headers $headers -ContentType 'application/json' -Body ($localUnit | ConvertTo-Json -Depth 8) | Out-Null
        $configurationUpdate = @{ expectedVersion = $configuration.version; demandMode = 1 } | ConvertTo-Json
        Invoke-RestMethod "$local/api/camps/$campId/meal-planning/participants" -Method Put -Headers $headers -ContentType 'application/json' -Body $configurationUpdate | Out-Null
    }
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
    if ($IncludeParticipants) {
        $participantResult = Invoke-RestMethod "$cloud/api/camps/$campId/participants" -WebSession $session
        if ($participantResult.participants[0].data.displayName -ne 'Locally edited dummy participant' -or
            $participantResult.participants[0].data.absentDays[0] -ne '2026-10-02') { throw 'Participant roundtrip failed.' }
        $planningResult = Invoke-RestMethod "$cloud/api/camps/$campId/meal-planning/participants" -WebSession $session
        $planningOverview = Invoke-RestMethod "$cloud/api/camps/$campId/meal-planning" -WebSession $session
        if ($planningResult.version -ne 2 -or $planningOverview.cookingUnits[0].participantFilter -ne 2 -or
            $participantResult.participants[0].data.structureNodeId -ne $node.id) { throw 'Camp structure and cooking filter roundtrip failed.' }
        Assert-Status 404 { Invoke-RestMethod "$local/api/camps/$campId/participants" -Headers $headers }
    }
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
