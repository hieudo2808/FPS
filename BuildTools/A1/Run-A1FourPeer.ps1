param(
    [Parameter(Mandatory = $true)]
    [string]$GameExecutable,
    [ValidateSet('baseline', 'impairment', 'reconnect', 'host-loss')]
    [string]$Scenario = 'baseline',
    [int]$DurationSeconds = 1800,
    [ValidateSet('normal', 'weak', 'severe')]
    [string]$Profile = 'normal',
    [int]$BurstAtSeconds = -1,
    [int]$DisconnectAtSeconds = -1,
    [int]$ReconnectAtSeconds = -1,
    [int]$HostLossAtSeconds = -1,
    [int]$EnemyCount = 30,
    [int]$Seed = 20260801,
    [switch]$ForceHostLoss
)

$ErrorActionPreference = 'Stop'
$resolvedExecutable = (Resolve-Path -LiteralPath $GameExecutable).Path
$projectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..')).Path
$runId = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
$runDirectory = Join-Path $projectRoot "Logs\A1\$runId"
$artifactDirectory = $runDirectory
$joinFile = Join-Path $runDirectory 'relay-join-code.txt'
$controlFile = Join-Path $runDirectory 'control.txt'
New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null

if ($Scenario -eq 'reconnect' -and $DisconnectAtSeconds -lt 0) { $DisconnectAtSeconds = 5 }
if ($Scenario -eq 'reconnect' -and $ReconnectAtSeconds -lt 0) { $ReconnectAtSeconds = 6 }
if ($Scenario -eq 'host-loss' -and $HostLossAtSeconds -lt 0) { $HostLossAtSeconds = 60 }
if ($Scenario -eq 'reconnect' -and $ReconnectAtSeconds -ge 0) {
    $DurationSeconds = [Math]::Max($DurationSeconds, $ReconnectAtSeconds + 20)
}

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
    disconnectAtSeconds = $DisconnectAtSeconds
    reconnectAtSeconds = $ReconnectAtSeconds
    hostLossAtSeconds = $HostLossAtSeconds
    burstAtSeconds = $BurstAtSeconds
    enemyCount = $EnemyCount
    seed = $Seed
    forceHostLoss = [bool]$ForceHostLoss
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
    playerPrefabHash = Get-HashOrEmpty (Join-Path $projectRoot 'Assets\Prefabs\Players\Player.prefab')
    networkPrefabsHash = Get-HashOrEmpty (Join-Path $projectRoot 'Assets\DefaultNetworkPrefabs.asset')
    verificationPolicyHash = Get-HashOrEmpty (Join-Path $projectRoot 'BuildTools\A1\A1VerificationPolicy.json')
}
$manifestPath = Join-Path $runDirectory 'run-manifest.json'
# Hash the complete provenance payload before adding the hash field itself.
# This avoids a self-referential hash while keeping the value in both the
# manifest and the final summary worktree object.
$manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath -Encoding utf8
$manifest.manifestHash = Get-HashOrEmpty $manifestPath
$manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath -Encoding utf8

function Start-A1Peer {
    param([string]$Name, [string]$Role)

    $arguments = @(
        '-batchmode', '-nographics',
        '-a1Verify', 'true',
        '-a1RunId', $runId,
        '-a1PeerId', $Name,
        '-a1Role', $Role,
        '-a1Scenario', $Scenario,
        '-a1Profile', $Profile,
        '-a1Duration', $DurationSeconds,
        '-a1EnemyCount', $EnemyCount,
        '-a1Seed', $Seed,
        '-a1JoinFile', $joinFile,
        '-a1ArtifactDir', $artifactDirectory,
        '-a1ControlFile', $controlFile,
        '-a1ServicesProfile', ("A1$runId$($Name -replace '[^A-Za-z0-9_-]', '')").Substring(0, [Math]::Min(30, ("A1$runId$($Name -replace '[^A-Za-z0-9_-]', '')").Length)),
        '-logFile', (Join-Path $runDirectory "$Name.log")
    )
    if ($BurstAtSeconds -ge 0) { $arguments += @('-a1BurstAt', $BurstAtSeconds) }
    if ($Name -eq 'client-1' -and $DisconnectAtSeconds -ge 0) { $arguments += @('-a1DisconnectAt', $DisconnectAtSeconds) }
    if ($Name -eq 'client-1' -and $ReconnectAtSeconds -ge 0) { $arguments += @('-a1ReconnectAt', $ReconnectAtSeconds) }
    if (-not $ForceHostLoss -and $Name -eq 'host' -and $HostLossAtSeconds -ge 0) { $arguments += @('-a1HostLossAt', $HostLossAtSeconds) }

    return Start-Process -FilePath $resolvedExecutable -ArgumentList $arguments -WorkingDirectory $runDirectory -WindowStyle Hidden -PassThru
}

$hostProcess = Start-A1Peer -Name 'host' -Role 'host'
$processes = @($hostProcess)

# Unity Services and AudioManager both persist PlayerPrefs during startup.
# Waiting for the host session before launching clients, then spacing client
# startup, prevents concurrent registry writes while preserving four live
# processes for the actual Relay test.
$hostReadyDeadline = (Get-Date).AddSeconds(60)
while ((Get-Date) -lt $hostReadyDeadline -and -not $hostProcess.HasExited) {
    $hostEventsPath = Join-Path $artifactDirectory 'host.events.jsonl'
    if (Test-Path -LiteralPath $hostEventsPath) {
        $hostReady = Select-String -LiteralPath $hostEventsPath -Pattern '"event(?:Name)?":"(services_ready|host_started|lobby_ready)"' -Quiet
        if ($hostReady) { break }
    }
    Start-Sleep -Milliseconds 250
}

foreach ($clientName in @('client-1', 'client-2', 'client-3')) {
    $processes += Start-A1Peer -Name $clientName -Role 'client'
    Start-Sleep -Seconds 3
}

$readinessDeadline = (Get-Date).AddSeconds(120)
$ready = $false
while (-not $ready -and (Get-Date) -lt $readinessDeadline) {
    $ready = $true
    foreach ($peerName in @('host', 'client-1', 'client-2', 'client-3')) {
        $candidate = Join-Path $artifactDirectory "$peerName.events.jsonl"
        if (-not (Test-Path -LiteralPath $candidate)) {
            $ready = $false
            break
        }
        $spawnCount = @(Select-String -LiteralPath $candidate -Pattern '"event(?:Name)?":"player_spawned"' -AllMatches).Count
        if ($spawnCount -lt 4) {
            $ready = $false
            break
        }
    }
    if (-not $ready) { Start-Sleep -Milliseconds 500 }
}

$startupFailureReason = $null
if (-not $ready) {
    $startupFailureReason = 'four_peer_readiness_timeout'
    # Do not wait for the scenario duration when the session never became a
    # four-peer run. This is especially important for Relay/Services startup
    # failures: the runner must still emit a machine-readable summary quickly.
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

$deadline = (Get-Date).AddSeconds([Math]::Max(120, $DurationSeconds + 120))
$forceHostLossSent = $false
while (($processes | Where-Object { -not $_.HasExited }).Count -gt 0 -and (Get-Date) -lt $deadline) {
    if ($ForceHostLoss -and -not $forceHostLossSent) {
        if ($hostProcess -and $HostLossAtSeconds -ge 0) {
            Start-Sleep -Seconds ([Math]::Max(1, $HostLossAtSeconds))
            if (-not $hostProcess.HasExited) { Stop-Process -Id $hostProcess.Id -Force }
            $forceHostLossSent = $true
        }
    }
    Start-Sleep -Milliseconds 500
}

foreach ($process in $processes) {
    if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force }
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
foreach ($peerName in @('host', 'client-1', 'client-2', 'client-3')) {
    $eventPath = Join-Path $artifactDirectory "$peerName.events.jsonl"
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

$eventFiles = @('host.events.jsonl', 'client-1.events.jsonl', 'client-2.events.jsonl', 'client-3.events.jsonl') | ForEach-Object { Join-Path $artifactDirectory $_ }
$missingEventFiles = @($eventFiles | Where-Object { -not (Test-Path -LiteralPath $_) })
Add-Check 'A1-ARTIFACTS' ($missingEventFiles.Count -eq 0) $eventFiles "Missing event files: $($missingEventFiles -join ', ')"
$startupStatus = if ($null -eq $startupFailureReason) { 'ready' } else { $startupFailureReason }
Add-Check 'A1-STARTUP' ($null -eq $startupFailureReason) $eventFiles "Startup status: $startupStatus"

$spawnedByPeer = @()
foreach ($peerName in @('host', 'client-1', 'client-2', 'client-3')) {
    $peerEvents = @($events | Where-Object { $_.peer -eq $peerName -and $_.eventName -eq 'player_spawned' })
    $spawnedByPeer += $peerEvents
    Add-Check "A1-PLAYER-SPAWN-$peerName" (@($peerEvents | Select-Object -ExpandProperty stablePlayerId -Unique).Count -eq 4) @((Join-Path $artifactDirectory "$peerName.events.jsonl")) "Unique players observed: $(@($peerEvents | Select-Object -ExpandProperty stablePlayerId -Unique).Count)"
}
Add-Check 'A1-CONNECT-4P' (@($spawnedByPeer | Select-Object -ExpandProperty stablePlayerId -Unique).Count -eq 4) $eventFiles 'All peers observed the same four stable players.'
 $globalStableIds = @($spawnedByPeer | Select-Object -ExpandProperty stablePlayerId -Unique | Sort-Object)
 $globalObjectIds = @($spawnedByPeer | Select-Object -ExpandProperty networkObjectId -Unique)
 $sameStableSet = $true
 foreach ($peerName in @('host', 'client-1', 'client-2', 'client-3')) {
    $peerIds = @($events | Where-Object { $_.peer -eq $peerName -and $_.eventName -eq 'player_spawned' } | Select-Object -ExpandProperty stablePlayerId -Unique | Sort-Object)
    $sameStableSet = $sameStableSet -and (($peerIds -join ',') -eq ($globalStableIds -join ','))
 }
$objectInvariant = if ($Scenario -eq 'reconnect') {
    # A reconnect is expected to receive a new NetworkObjectId. Validate the
    # stable identity set and the final four-player snapshot instead of treating
    # historical object ids as concurrent duplicates.
    $finalSnapshots = @($events | Where-Object { $_.eventName -eq 'player_snapshot' -and $_.reason -match 'players=4;stableIds=4' })
    $finalSnapshots.Count -ge 1
} else {
    $globalObjectIds.Count -eq 4
}
Add-Check 'A1-UNIQUE-IDS-AND-OBJECTS' ($globalStableIds.Count -eq 4 -and $objectInvariant -and $sameStableSet) $eventFiles 'Stable player IDs must be unique; reconnect may bind a new NetworkObjectId after the old object is gone.'
Add-Check 'A1-SCENE-SYNC' (@($events | Where-Object { $_.eventName -eq 'game_scene_ready' -and $_.scene -eq 'GameScene' } | Select-Object -ExpandProperty peer -Unique).Count -eq 4) $eventFiles 'Each peer must report the same GameScene.'
Add-Check 'A1-PREFAB-REGISTRATION' (@($events | Where-Object { $_.eventName -eq 'prefab_registration' -and $_.result -eq 'pass' }).Count -ge 4) $eventFiles 'Runtime PlayerPrefab registration must pass on every peer.'

if ($Scenario -in @('baseline', 'impairment', 'reconnect')) {
    $activityPass = @($events | Where-Object { $_.eventName -eq 'fire_result' }).Count -gt 0
    $activityPass = $activityPass -and @($events | Where-Object { $_.eventName -eq 'pickup_result' }).Count -gt 0
    $activityPass = $activityPass -and @($events | Where-Object { $_.eventName -eq 'enemy_state' }).Count -gt 0
    Add-Check 'A1-ACTIVITY-PATHS' $activityPass $eventFiles 'Fire, pickup and enemy replication paths must produce structured events.'
}

$warningEvents = @($events | Where-Object { $_.eventName -in @('network_warning', 'network_error') -and $_.result -eq 'fail' })
Add-Check 'A1-NO-UNALLOWLISTED-LOGS' ($warningEvents.Count -eq 0) $eventFiles "Fatal diagnostics: $($warningEvents.Count)"

$metricFiles = @('host.metrics.jsonl', 'client-1.metrics.jsonl', 'client-2.metrics.jsonl', 'client-3.metrics.jsonl') | ForEach-Object { Join-Path $artifactDirectory $_ }
$metricRecords = [System.Collections.Generic.List[object]]::new()
foreach ($metricFile in $metricFiles) {
    if (Test-Path -LiteralPath $metricFile) {
        foreach ($line in Get-Content -LiteralPath $metricFile) {
            if (-not [string]::IsNullOrWhiteSpace($line)) {
                try { $metricRecords.Add(($line | ConvertFrom-Json)) } catch { }
            }
        }
    }
}
$metricsPath = Join-Path $artifactDirectory 'metrics.json'
$maxGcAllocationBytes = if ($metricRecords.Count) { ($metricRecords | Measure-Object -Property gcAllocationBytes -Maximum).Maximum } else { 0 }
$maxActiveNetworkObjects = if ($metricRecords.Count) { ($metricRecords | Measure-Object -Property activeNetworkObjects -Maximum).Maximum } else { 0 }
$maxEnemyReplicationEventsPerSecond = if ($metricRecords.Count) { ($metricRecords | Measure-Object -Property enemyReplicationEventsPerSecond -Maximum).Maximum } else { 0 }
$metricSummary = [ordered]@{
    windowCount = $metricRecords.Count
    uplinkKBpsP95 = if ($metricRecords.Count) { ($metricRecords | Measure-Object -Property uplinkKBps -Maximum).Maximum } else { 0 }
    downlinkKBpsP95 = if ($metricRecords.Count) { ($metricRecords | Measure-Object -Property downlinkKBps -Maximum).Maximum } else { 0 }
    networkMainThreadP95Ms = if ($metricRecords.Count) { ($metricRecords | Measure-Object -Property networkMainThreadP95Ms -Maximum).Maximum } else { 0 }
    gcAllocationBytesMax = $maxGcAllocationBytes
    activeNetworkObjectsMax = $maxActiveNetworkObjects
    enemyReplicationEventsPerSecondMax = $maxEnemyReplicationEventsPerSecond
    records = $metricRecords
}
$metricSummary | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $metricsPath -Encoding utf8
$policy = Get-Content -Raw -LiteralPath (Join-Path $projectRoot 'BuildTools\A1\A1VerificationPolicy.json') | ConvertFrom-Json
$metricsPass = $metricRecords.Count -ge [int]$policy.minMetricWindows
$metricsPass = $metricsPass -and (($metricRecords | Measure-Object -Property uplinkKBps -Maximum).Maximum -le [double]$policy.maxUplinkKBps)
$metricsPass = $metricsPass -and (($metricRecords | Measure-Object -Property downlinkKBps -Maximum).Maximum -le [double]$policy.maxDownlinkKBps)
$metricsPass = $metricsPass -and (($metricRecords | Measure-Object -Property networkMainThreadP95Ms -Maximum).Maximum -le [double]$policy.maxNetworkMainThreadP95Ms)
$metricsPass = $metricsPass -and $maxGcAllocationBytes -le [double]$policy.maxGcAllocationBytesPerWindow
$metricsPass = $metricsPass -and $maxActiveNetworkObjects -le [int]$policy.maxActiveNetworkObjects
$metricsPass = $metricsPass -and $maxEnemyReplicationEventsPerSecond -le [double]$policy.maxEnemyReplicationEventsPerSecond
Add-Check 'A1-METRICS' $metricsPass $metricFiles "windows=$($metricRecords.Count);policy=$($manifest.verificationPolicyHash)"

if ($Scenario -eq 'reconnect') {
    $ackCount = @($events | Where-Object { $_.eventName -eq 'reconnect_restore_ack' }).Count
    # The grace boundary is the elapsed time between disconnect and reconnect.
    # The arguments are absolute elapsed times from run start. A client
    # disconnected at 5s and reconnecting at 64s is a 59s case;
    # reconnecting at 65s or later is expected to expire.
    $reconnectPass = if ($ReconnectAtSeconds - $DisconnectAtSeconds -ge 60) {
        @($events | Where-Object {
            $_.eventName -eq 'connection_failed' -and
            $_.reason -match 'ReconnectExpired|Reconnect reservation expired'
        }).Count -gt 0
    } else { $ackCount -gt 0 }
    Add-Check 'A1-RECONNECT-BOUNDARY' $reconnectPass $eventFiles "Restore acknowledgements: $ackCount"
}
if ($Scenario -eq 'host-loss') {
    Add-Check 'A1-HOST-LOSS-CLEANUP' (@($events | Where-Object { $_.eventName -eq 'connection_failed' -or $_.eventName -eq 'host_loss_cleanup' }).Count -ge 1) $eventFiles 'Clients must report terminal host-loss cleanup.'
}

$hasFailure = @($checks | Where-Object { -not $_.pass }).Count -gt 0
$exitCode = if (-not $hasFailure) { 0 }
    elseif ($null -ne $startupFailureReason) { 10 }
    elseif ($missingEventFiles.Count -gt 0 -or $metricRecords.Count -eq 0) { 70 }
    elseif ($warningEvents.Count -gt 0) { 60 }
    elseif ($Scenario -eq 'reconnect' -or $Scenario -eq 'host-loss') { 30 }
    elseif (@($checks | Where-Object { $_.id -match 'SPAWN|CONNECT|SCENE|PREFAB' -and -not $_.pass }).Count -gt 0) { 20 }
    else { 40 }
$summary = [ordered]@{
    runId = $runId
    scenario = $Scenario
    profile = $Profile
    pass = -not $hasFailure
    exitCode = $exitCode
    checks = $checks
    warnings = @($warningEvents | ForEach-Object { $_.reason })
    errors = @($events | Where-Object { $_.result -eq 'fail' } | ForEach-Object { $_.reason })
    worktree = $manifest
}
$summary | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $runDirectory 'summary.json') -Encoding utf8
'<a1-summary runId="{0}" scenario="{1}" pass="{2}" exitCode="{3}" />' -f $runId, $Scenario, (-not $hasFailure), $exitCode | Set-Content -LiteralPath (Join-Path $runDirectory 'summary.xml') -Encoding utf8
Write-Host "A1 artifacts: $runDirectory"
exit $exitCode
