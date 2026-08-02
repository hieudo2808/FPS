param(
    [Parameter(Mandatory = $true)]
    [string]$GameExecutable,
    [ValidateSet('baseline', 'impairment', 'reconnect', 'host-loss')]
    [string]$Scenario = 'baseline',
    [int]$DurationSeconds = 180,
    [ValidateSet('normal', 'weak', 'severe')]
    [string]$Profile = 'normal',
    [int]$BurstAtSeconds = -1,
    [int]$DisconnectAtSeconds = -1,
    [int]$ReconnectAtSeconds = -1,
    [int]$HostLossAtSeconds = -1,
    [int]$EnemyCount = 30,
    [int]$Seed = 20260801,
    [switch]$ObserveOnly
)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$resolvedExecutable = (Resolve-Path -LiteralPath $GameExecutable).Path
$runId = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
$runDirectory = Join-Path $projectRoot "Logs\A2\$runId"
$joinFile = Join-Path $runDirectory 'relay-join-code.txt'
$controlFile = Join-Path $runDirectory 'control.txt'
New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null

if ($Scenario -eq 'reconnect' -and $DisconnectAtSeconds -lt 0) { $DisconnectAtSeconds = 5 }
if ($Scenario -eq 'reconnect' -and $ReconnectAtSeconds -lt 0) { $ReconnectAtSeconds = $DisconnectAtSeconds + 1 }
if ($Scenario -eq 'host-loss' -and $HostLossAtSeconds -lt 0) { $HostLossAtSeconds = 60 }

function Get-HashOrEmpty([string]$path) {
    if (-not (Test-Path -LiteralPath $path)) { return '' }
    return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
}

$changedFiles = @(git -C $projectRoot status --short)
$packageManifestPath = Join-Path $projectRoot 'Packages\manifest.json'
$packageManifest = Get-Content -Raw $packageManifestPath | ConvertFrom-Json
$manifest = [ordered]@{
    runId = $runId
    scenario = $Scenario
    profile = $Profile
    durationSeconds = $DurationSeconds
    enemyCount = $EnemyCount
    seed = $Seed
    observeOnly = [bool]$ObserveOnly
    command = $MyInvocation.Line
    executable = $resolvedExecutable
    executableHash = Get-HashOrEmpty $resolvedExecutable
    buildManagedAssemblyHash = Get-HashOrEmpty (Join-Path (Split-Path $resolvedExecutable) 'FPS_Data\Managed\Assembly-CSharp.dll')
    buildNetworkSimulationAssemblyHash = Get-HashOrEmpty (Join-Path (Split-Path $resolvedExecutable) 'FPS_Data\Managed\FPS.NetworkSimulation.dll')
    unityVersion = ((Get-Content -Raw (Join-Path $projectRoot 'ProjectSettings\ProjectVersion.txt')) -split "`n" | Where-Object { $_ -match '^m_EditorVersion:' } | Select-Object -First 1).Trim()
    head = (git -C $projectRoot rev-parse HEAD).Trim()
    isDirty = ($changedFiles.Count -gt 0)
    changedFiles = $changedFiles
    packageManifestHash = Get-HashOrEmpty $packageManifestPath
    packageLockHash = Get-HashOrEmpty (Join-Path $projectRoot 'Packages\packages-lock.json')
    netcodeGameObjectsVersion = [string]$packageManifest.dependencies.'com.unity.netcode.gameobjects'
    multiplayerToolsVersion = [string]$packageManifest.dependencies.'com.unity.multiplayer.tools'
    multiplayerPlayModeVersion = [string]$packageManifest.dependencies.'com.unity.multiplayer.playmode'
    mainMenuHash = Get-HashOrEmpty (Join-Path $projectRoot 'Assets\Scenes\MainMenu.unity')
    gameSceneHash = Get-HashOrEmpty (Join-Path $projectRoot 'Assets\Scenes\GameScene.unity')
    playerPrefabHash = Get-HashOrEmpty (Join-Path $projectRoot 'Assets\Prefabs\Players\Player.prefab')
    adaptiveDomainHash = Get-HashOrEmpty (Join-Path $projectRoot 'Assets\FPS\Features\AI\Runtime\AdaptiveDifficultyDomain.cs')
    directorHash = Get-HashOrEmpty (Join-Path $projectRoot 'Assets\FPS\Features\AI\Runtime\AIDirector.cs')
}
$manifestPath = Join-Path $runDirectory 'run-manifest.json'
# Hash the complete provenance payload before adding the hash field itself.
# This avoids a self-referential hash while keeping the value in both the
# manifest and the final summary worktree object.
$manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath -Encoding utf8
$manifest.manifestHash = Get-HashOrEmpty $manifestPath
$manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath -Encoding utf8

function Start-A2Peer {
    param([string]$Name, [string]$Role)

    $arguments = @(
        '-batchmode', '-nographics',
        '-a2Verify', 'true',
        '-a2RunId', $runId,
        '-a2PeerId', $Name,
        '-a2Role', $Role,
        '-a2Scenario', $Scenario,
        '-a2Profile', $Profile,
        '-a2Duration', $DurationSeconds,
        '-a2EnemyCount', $EnemyCount,
        '-a2Seed', $Seed,
        '-a2JoinFile', $joinFile,
        '-a2ArtifactDir', $runDirectory,
        '-a2ControlFile', $controlFile,
        '-a2EnableAdaptive', 'true',
        '-a2ObserveOnly', ([bool]$ObserveOnly).ToString().ToLowerInvariant(),
        '-a2ServicesProfile', ("A2$runId$($Name -replace '[^A-Za-z0-9_-]', '')").Substring(0, [Math]::Min(30, ("A2$runId$($Name -replace '[^A-Za-z0-9_-]', '')").Length)),
        '-logFile', (Join-Path $runDirectory "$Name.log")
    )
    if ($BurstAtSeconds -ge 0) { $arguments += @('-a2BurstAt', $BurstAtSeconds) }
    if ($Name -eq 'client-1' -and $DisconnectAtSeconds -ge 0) { $arguments += @('-a2DisconnectAt', $DisconnectAtSeconds) }
    if ($Name -eq 'client-1' -and $ReconnectAtSeconds -ge 0) { $arguments += @('-a2ReconnectAt', $ReconnectAtSeconds) }
    if ($Name -eq 'host' -and $HostLossAtSeconds -ge 0) { $arguments += @('-a2HostLossAt', $HostLossAtSeconds) }
    Start-Process -FilePath $resolvedExecutable -ArgumentList $arguments -WorkingDirectory $runDirectory -WindowStyle Hidden -PassThru
}

$peerNames = @('host', 'client-1', 'client-2', 'client-3')
$hostProcess = Start-A2Peer 'host' 'host'
$processes = @($hostProcess)

# Avoid concurrent Unity Services/PlayerPrefs writes during standalone startup.
$hostReadyDeadline = (Get-Date).AddSeconds(60)
while ((Get-Date) -lt $hostReadyDeadline -and -not $hostProcess.HasExited) {
    $hostEventsPath = Join-Path $runDirectory 'host.events.jsonl'
    if (Test-Path -LiteralPath $hostEventsPath) {
        $hostReady = Select-String -LiteralPath $hostEventsPath -Pattern '"event(?:Name)?":"(services_ready|host_started|lobby_ready)"' -Quiet
        if ($hostReady) { break }
    }
    Start-Sleep -Milliseconds 250
}
foreach ($clientName in @('client-1', 'client-2', 'client-3')) {
    $processes += Start-A2Peer $clientName 'client'
    Start-Sleep -Seconds 3
}

$readinessDeadline = (Get-Date).AddSeconds(120)
$ready = $false
while (-not $ready -and (Get-Date) -lt $readinessDeadline) {
    $ready = $true
    foreach ($peerName in $peerNames) {
        $candidate = Join-Path $runDirectory "$peerName.events.jsonl"
        if (-not (Test-Path -LiteralPath $candidate)) { $ready = $false; break }
        $spawnCount = @(Select-String -LiteralPath $candidate -Pattern '"event(?:Name)?":"player_spawned"' -AllMatches).Count
        if ($spawnCount -lt 4) { $ready = $false; break }
    }
    if (-not $ready) { Start-Sleep -Milliseconds 500 }
}

$startupFailureReason = $null
if (-not $ready) {
    $startupFailureReason = 'four_peer_readiness_timeout'
    foreach ($process in $processes) {
        if ($process -and -not $process.HasExited) {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        }
    }
    foreach ($process in $processes) {
        if ($process) {
            try { $process.WaitForExit(5000) } catch { }
        }
    }
}

$timedOut = $false
$deadline = (Get-Date).AddSeconds([Math]::Max(120, $DurationSeconds + 120))
while (($processes | Where-Object { -not $_.HasExited }).Count -gt 0 -and (Get-Date) -lt $deadline) {
    Start-Sleep -Milliseconds 500
}
if ((Get-Date) -ge $deadline) { $timedOut = $true }
foreach ($process in $processes) {
    if ($process -and -not $process.HasExited) { Stop-Process -Id $process.Id -Force }
}

$events = [System.Collections.Generic.List[object]]::new()
function Normalize-EventRecord([object]$record) {
    if ($null -eq $record) { return $null }
    if ($null -eq $record.PSObject.Properties['eventName']) {
        $eventProperty = $record.PSObject.Properties['event']
        if ($null -ne $eventProperty) {
            $record | Add-Member -NotePropertyName eventName -NotePropertyValue ([string]$eventProperty.Value) -Force
        }
    }
    return $record
}
foreach ($peerName in $peerNames) {
    $eventPath = Join-Path $runDirectory "$peerName.events.jsonl"
    if (Test-Path -LiteralPath $eventPath) {
        foreach ($line in Get-Content -LiteralPath $eventPath) {
            if (-not [string]::IsNullOrWhiteSpace($line)) {
                try { $events.Add((Normalize-EventRecord ($line | ConvertFrom-Json))) } catch { }
            }
        }
    }
}

$checks = @()
function Add-Check([string]$id, [bool]$pass, [string[]]$evidence, [string]$message) {
    $script:checks += [ordered]@{ id = $id; pass = $pass; evidence = $evidence; message = $message }
}

$eventFiles = $peerNames | ForEach-Object { Join-Path $runDirectory "$_.events.jsonl" }
$missingEventFiles = @($eventFiles | Where-Object { -not (Test-Path -LiteralPath $_) })
Add-Check 'A2-ARTIFACTS' ($missingEventFiles.Count -eq 0 -and -not $timedOut) $eventFiles "Missing=$($missingEventFiles.Count);timedOut=$timedOut"
$startupStatus = if ($null -eq $startupFailureReason) { 'ready' } else { $startupFailureReason }
Add-Check 'A2-STARTUP' ($null -eq $startupFailureReason) $eventFiles "Startup status: $startupStatus"

$spawnedByPeer = @()
foreach ($peerName in $peerNames) {
    $peerEvents = @($events | Where-Object { $_.peer -eq $peerName -and $_.eventName -eq 'player_spawned' })
    $spawnedByPeer += $peerEvents
    Add-Check "A2-PLAYER-SPAWN-$peerName" (@($peerEvents | Select-Object -ExpandProperty stablePlayerId -Unique).Count -eq 4) @((Join-Path $runDirectory "$peerName.events.jsonl")) 'Each peer must observe four stable players.'
}
Add-Check 'A2-4P' (@($spawnedByPeer | Select-Object -ExpandProperty stablePlayerId -Unique).Count -eq 4) $eventFiles 'All peers must observe the same four stable player identities.'
Add-Check 'A2-PHASE-TRACE' (@($events | Where-Object { $_.eventName -eq 'director_phase_changed' }).Count -gt 0) $eventFiles 'Adaptive director must emit phase transitions.'
Add-Check 'A2-ADAPTIVE-SNAPSHOT' (@($events | Where-Object { $_.eventName -eq 'adaptive_state' }).Count -gt 0) $eventFiles 'Adaptive state snapshots must be observable.'
$dynamicEvents = @($events | Where-Object { $_.eventName -eq 'difficulty_evaluated' })
$dynamicRequired = $DurationSeconds -ge 75 -and -not [bool]$ObserveOnly
Add-Check 'A2-RELAX-EVALUATION' (-not $dynamicRequired -or $dynamicEvents.Count -gt 0) $eventFiles "Difficulty evaluations=$($dynamicEvents.Count);required=$dynamicRequired"
$warningEvents = @($events | Where-Object { $_.eventName -in @('network_warning', 'network_error') -and $_.result -eq 'fail' })
Add-Check 'A2-NO-FATAL-WARNINGS' ($warningEvents.Count -eq 0) $eventFiles "Fatal diagnostics=$($warningEvents.Count)"

$processFailure = @($processes | Where-Object { $_.HasExited -and $_.ExitCode -ne 0 })
$hasFailure = @($checks | Where-Object { -not $_.pass }).Count -gt 0 -or $processFailure.Count -gt 0
$exitCode = if ($null -ne $startupFailureReason) { 10 }
    elseif ($timedOut -or $missingEventFiles.Count -gt 0) { 70 }
    elseif ($warningEvents.Count -gt 0) { 60 }
    elseif ($processFailure.Count -gt 0) { 10 }
    elseif (@($checks | Where-Object { $_.id -match '4P|PLAYER-SPAWN' -and -not $_.pass }).Count -gt 0) { 20 }
    elseif ($dynamicEvents.Count -eq 0 -and $dynamicRequired) { 40 }
    elseif ($hasFailure) { 40 }
    else { 0 }

$summary = [ordered]@{
    runId = $runId
    scenario = $Scenario
    profile = $Profile
    pass = -not $hasFailure -and $exitCode -eq 0
    exitCode = $exitCode
    checks = $checks
    warnings = @($warningEvents | ForEach-Object { $_.reason })
    errors = @($events | Where-Object { $_.result -eq 'fail' } | ForEach-Object { $_.reason })
    worktree = $manifest
}
$summary | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $runDirectory 'summary.json') -Encoding utf8
'<a2-summary runId="{0}" scenario="{1}" pass="{2}" exitCode="{3}" />' -f $runId, $Scenario, ($summary.pass), $exitCode | Set-Content -LiteralPath (Join-Path $runDirectory 'summary.xml') -Encoding utf8
Write-Host "A2 artifacts: $runDirectory"
exit $exitCode
