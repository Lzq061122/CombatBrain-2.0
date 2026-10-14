param(
    [ValidateSet('P', 'N')]
    [string]$Config = 'P',
    [string]$OutDir = '',
    [int]$CombatHoldSec = 150,
    [int]$PollTimeoutSec = 240,
    [int]$LaunchWaitSec = 900,
    [int]$McpPort = 19580,
    [string]$GameId = 'bannerlord148-cb2',
    [int]$ProbeHoldSec = 4,
    [switch]$DisableP1,
    [switch]$DisableProbe,
    [switch]$WhatIf
)

$ErrorActionPreference = 'Stop'

$here = $PSScriptRoot
$gameDir = 'E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord'
$cbathTools = 'E:\MBmod\CombatBrain Autonomous test harness\tools'
$engineConfig = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'Mount and Blade II Bannerlord\Configs\engine_config.txt'
$cb2Log = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'CombatBrain2\CombatBrain2.log'
$cb2Dir = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'CombatBrain2'
$p1Sentinel = Join-Path $cb2Dir 'p1-command-loop.txt'
# P1 and P2 are mutually exclusive: SamplingBehavior refuses to run either when both
# switches read enabled=1. A previous P2 session restores its own sentinel to the
# pre-run state, which may be enabled=1, so P1 must clear it explicitly instead of
# silently running with no command source.
$p2Sentinel = Join-Path $cb2Dir 'p2-command-loop.txt'
$probeSentinel = Join-Path $cb2Dir 'eligibility-probe.txt'
$cbathOut = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'CommandBrainATH'

# Config P/N request composition (script 3.2 / 3.3). Order: infantry, ranged, cavalry, horse archer.
switch ($Config) {
    'P' {
        $ownArmySize = 100; $ownComp = '30,55,15,0'
        $enemyArmySize = 100; $enemyComp = '70,20,10,0'
    }
    'N' {
        $ownArmySize = 100; $ownComp = '70,20,10,0'
        $enemyArmySize = 100; $enemyComp = '30,55,15,0'
    }
}

if ([string]::IsNullOrEmpty($OutDir)) {
    $stamp = (Get-Date).ToString('yyyyMMdd-HHmmss')
    $OutDir = Join-Path $here "sessions\$stamp"
}
# The preview branch must not create evidence directories.
Write-Host "config: $Config"
Write-Host "out_dir: $OutDir"

$script:Failures = @()
function Fail($msg) { $script:Failures += $msg; Write-Host "FAIL: $msg" }

# On a crash the engine only writes dumps plus a watchdog log under crashes\<timestamp>\,
# never back into the rgl log. Not checking here turns a crash into a false "hang".
$crashRoot = 'C:\ProgramData\Mount and Blade II Bannerlord\crashes'
$script:CrashDirsBefore = @(Get-ChildItem $crashRoot -Directory -ErrorAction SilentlyContinue | ForEach-Object { $_.Name })
function Report-NewCrashes {
    $now = @(Get-ChildItem $crashRoot -Directory -ErrorAction SilentlyContinue | ForEach-Object { $_.Name })
    $new = @($now | Where-Object { $script:CrashDirsBefore -notcontains $_ })
    if ($new.Count -gt 0) {
        Fail "game crashed; new crash dirs: $($new -join ',')"
        foreach ($d in $new) {
            $dd = Join-Path $crashRoot $d
            $dst = Join-Path $OutDir "crash-$d"
            New-Item -ItemType Directory -Force -Path $dst | Out-Null
            Get-ChildItem $dd -File | Where-Object { $_.Name -ne 'dump.dmp' } | Copy-Item -Destination $dst -Force -ErrorAction SilentlyContinue
            Write-Host "crash evidence saved: $dst"
        }
    } else {
        Write-Host "no new crash dir"
    }
}

# Sentinel files (P1 switch and eligibility probe adapter).
$script:SentinelBackups = @()
function Restore-Sentinels {
    foreach ($saved in $script:SentinelBackups) {
        if ($saved.Existed) {
            [System.IO.File]::WriteAllBytes($saved.Path, $saved.Bytes)
        } elseif (Test-Path -LiteralPath $saved.Path) {
            Remove-Item -LiteralPath $saved.Path -Force
        }
    }
    Write-Host "sentinel state restored"
}
# enabled is read at the first Mission load, so it must be written before the game starts (script 9.4).
# ASCII only, so the PowerShell 5.1 Set-Content default ANSI encoding is harmless here.
function Write-Sentinels {
    New-Item -ItemType Directory -Force -Path $cb2Dir | Out-Null

    foreach ($path in @($p1Sentinel, $p2Sentinel, $probeSentinel)) {
        $existed = Test-Path -LiteralPath $path
        $savedBytes = if ($existed) { [System.IO.File]::ReadAllBytes($path) } else { $null }
        $script:SentinelBackups += @{ Path = $path; Existed = $existed; Bytes = $savedBytes }
    }
    $p1Value = if ($DisableP1) { '0' } else { '1' }
    $probeValue = if ($DisableProbe) { '' } else { "mode=a`r`nhold_seconds=$ProbeHoldSec" }
    Set-Content -LiteralPath $p1Sentinel -Value "enabled=$p1Value" -Encoding Ascii
    Set-Content -LiteralPath $p2Sentinel -Value "enabled=0" -Encoding Ascii
    Set-Content -LiteralPath $probeSentinel -Value $probeValue -Encoding Ascii

    Write-Host "p1 sentinel:      $p1Sentinel  -> enabled=$p1Value"
    Write-Host "p2 sentinel:      $p2Sentinel  -> enabled=0 (mutually exclusive with p1)"
    Write-Host "probe sentinel:   $probeSentinel  -> $(if ($DisableProbe) { '(empty)' } else { "mode=a hold_seconds=$ProbeHoldSec" })"
}


if ($WhatIf) {
    Write-Host "=== whatif ==="
    Write-Host "config $Config : own army=$ownArmySize comp=$ownComp | enemy army=$enemyArmySize comp=$enemyComp"
    Write-Host "would clear: $cb2Log"
    Write-Host "would clear: $cbathOut"
    Write-Host "would run: h3_provider_start -> h3_start_from_screen(own=$ownComp enemy=$enemyComp) -> auto_finish_deployment -> delegate_team_to_ai"
    Write-Host "combat window: $CombatHoldSec s"
    Write-Host "would write: $p1Sentinel -> enabled=$(if ($DisableP1) { '0' } else { '1' })"
    Write-Host "would write: $probeSentinel -> mode=a hold_seconds=$ProbeHoldSec (unless disabled)"
    Write-Host "side mapping: own non-player AI = enemy side; opponent player team = player side"
    Write-Host "RESULT: WHATIF"
    exit 0
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
. (Join-Path $cbathTools 'mcp-http.ps1')

# All CBATH tools are forwarded through games_call_tool.
function Invoke-Tool([string]$name, [int]$timeout = 30, $toolArgs = $null) {
    $inner = @{ gameId = $GameId; tool = $name; timeout = $timeout }
    if ($toolArgs) { $inner['arguments'] = $toolArgs }
    return Invoke-McpHttp -Tool 'games_call_tool' `
        -Arguments $inner `
        -TimeoutSec ($timeout + 40)
}

# Tool replies are JSON-RPC envelopes; the real result lives in result.structuredContent.
# Read fields only - the same reply carries both before and after state, so a regex over
# the whole response can match the wrong one.
function Get-ToolObject($r) {
    try {
        $j = $r | ConvertFrom-Json
        if ($j.result -and $j.result.structuredContent) { return $j.result.structuredContent }
        if ($j.result -and $j.result.content -and $j.result.content[0].text) {
            return ($j.result.content[0].text | ConvertFrom-Json)
        }
    } catch { }
    return $null
}

# h3_start_from_screen with configuration parameters. Requested values are recorded verbatim
# so the report can show requested vs actual (script 3.5.4).
#
# Side mapping (per dispatcher ruling): in this batch our side is the NON-player AI team,
# which this fixed entry exposes as the enemy side. Delegating the player team to AI does not
# change IsPlayerTeam and does not make it controllable by CB2, so it stays the opposing side.
#   enemyArmySize / enemyComposition  <- our requested composition
#   playerArmySize / playerComposition <- the opposing player team
# Parameter names must match CbathTools.H3StartFromScreen exactly.
#
# Nesting: games_call_tool accepts only arguments/gameId/timeout/tool at the top level, and the
# target tool's own parameters go inside "arguments" (same shape as run-p0.ps1's Invoke-Tool).
function Invoke-StartConfigured {
    $toolArgs = @{
        playerArmySize    = $enemyArmySize
        playerComposition = $enemyComp
        enemyArmySize     = $ownArmySize
        enemyComposition  = $ownComp
    }
    return Invoke-Tool 'cbath/h3_start_from_screen' 60 $toolArgs
}

# ---------- preflight ----------
Write-Host "=== preflight ==="

$alive = @(Get-Process -Name 'Bannerlord' -ErrorAction SilentlyContinue | Where-Object { -not $_.HasExited })
if ($alive.Count -gt 0) {
    Write-Host "REFUSED: Bannerlord already running (pid $(($alive | ForEach-Object { $_.Id }) -join ','))"
    exit 2
}

$steam = Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -eq 'steam' }
if (-not $steam) {
    $se = (Get-ItemProperty 'HKCU:\Software\Valve\Steam' -ErrorAction SilentlyContinue).SteamExe
    if (-not $se) { $se = 'C:\Program Files (x86)\Steam\steam.exe' }
    Write-Host "steam not running, starting: $se"
    Start-Process -FilePath $se -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 45
}

# safely_exited must be 1: a 0 silently blocks all mod loading.
if (Test-Path $engineConfig) {
    $cfg = [System.IO.File]::ReadAllText($engineConfig)
    if ($cfg -match 'safely_exited\s*=\s*(\d+)') {
        if ($Matches[1] -ne '1') {
            $cfg = $cfg -replace 'safely_exited\s*=\s*\d+', 'safely_exited = 1'
            [System.IO.File]::WriteAllText($engineConfig, $cfg)
            Write-Host "preflight: safely_exited rewritten to 1"
        } else {
            Write-Host "preflight: safely_exited already 1"
        }
    }
} else {
    Write-Host "WARN: engine_config.txt not found at $engineConfig"
}

if (Test-Path $cb2Log) { Remove-Item $cb2Log -Force }
Write-Host "preflight: CombatBrain2 log cleared"

# CBATH accumulates per-pid output across processes; not clearing it mixes earlier sessions
# into this session's evidence. The authoritative CBATH script zero-touch-3x.ps1 does the same.
if (Test-Path $cbathOut) { Remove-Item $cbathOut -Recurse -Force }
Write-Host "preflight: CommandBrainATH cleared"

# Deployed identity: the DLL actually loaded this session, for the V1 sha256 comparison.
$deployDll = Join-Path $gameDir 'Modules\CombatBrain2\bin\Win64_Shipping_Client\CombatBrain2.dll'
if (Test-Path $deployDll) {
    $hash = (Get-FileHash $deployDll -Algorithm SHA256).Hash
    $item = Get-Item $deployDll
    $rec = [ordered]@{ path = $deployDll; size = $item.Length; sha256 = $hash; last_write = $item.LastWriteTime.ToString('o') }
    [System.IO.File]::WriteAllText((Join-Path $OutDir 'deployed-module.json'), ($rec | ConvertTo-Json -Compress))
    Write-Host "deployed module: $hash ($($item.Length) B, $($item.LastWriteTime))"
} else {
    Fail "CombatBrain2.dll not found at $deployDll - module not deployed"
    throw 'no_deployed_module'
}

# ---------- launch ----------
Write-Host "=== launch ==="
$cfgDir = Join-Path $here 'gabs-config'
$gabsPid = Open-McpHttp -ConfigDir $cfgDir -ListenPort $McpPort
Write-Host "gabs_pid: $gabsPid"

try {
    Write-Sentinels
    $r = Invoke-McpHttp -Tool 'games_start' -Arguments @{ gameId = $GameId; timeout = 150 } -TimeoutSec $LaunchWaitSec
    Write-Host "games_start: $r"
    if ($r -match 'already_running') { Fail 'games_start reported already_running'; throw 'already_running' }

    # This install carries many mods and startup can take minutes (a cold shader cache once
    # took 211 s). The GABP bridge only listens after the game finishes loading, so connect
    # must be polled for a long time. Success wording follows GABS itself; gabpConnected can
    # read false while the bridge is not yet answering.
    $deadline = (Get-Date).AddSeconds($LaunchWaitSec)
    $connected = $false
    $attempt = 0
    while ((Get-Date) -lt $deadline) {
        if (-not (Get-Process -Name 'Bannerlord' -ErrorAction SilentlyContinue | Where-Object { -not $_.HasExited })) {
            Fail 'Bannerlord exited during launch'; throw 'died_during_launch'
        }
        $attempt++
        try {
            $r = Invoke-McpHttp -Tool 'games_connect' -Arguments @{ gameId = $GameId; timeout = 40; forceTakeover = $true } -TimeoutSec 90
            $short = if ($r.Length -gt 300) { $r.Substring(0, 300) } else { $r }
            Write-Host "connect attempt $attempt : $short"
            if ($r -match 'Discovered|Re-synced|Already connected|Force-took') {
                $connected = $true
                Write-Host "games_connect ok"
                break
            }
        } catch {
            Write-Host "connect attempt $attempt threw: $_"
        }
        Start-Sleep -Seconds 10
    }
    if (-not $connected) { Fail 'games_connect never succeeded'; throw 'no_connect' }

    $deadline = (Get-Date).AddSeconds($PollTimeoutSec)
    $pingOk = $false
    while ((Get-Date) -lt $deadline) {
        try {
            $r = Invoke-Tool 'cbath/ping' 20
            if ($r -match 'pong') { $pingOk = $true; Write-Host "ping ok"; break }
        } catch { }
        Start-Sleep -Seconds 3
    }
    if (-not $pingOk) { Fail 'cbath ping never answered'; throw 'no_ping' }

    $r = Invoke-Tool 'cbath/get_build_identity' 25
    [System.IO.File]::WriteAllText((Join-Path $OutDir 'build-identity.json'), $r)
    Write-Host "build_identity: $r"

    # Must wait for the main menu (InitialState) first: h3_provider_start pushes a CustomBattle
    # state and only from the main menu is that valid. Calling during loading is undefined
    # behavior; the authoritative CBATH script zero-touch-3x.ps1 L264-274 waits for InitialState too.
    $deadline = (Get-Date).AddSeconds($LaunchWaitSec)
    $atMenu = $false
    while ((Get-Date) -lt $deadline) {
        $r = Invoke-Tool 'cbath/get_game_state' 25
        if ($r -match 'InitialState') { $atMenu = $true; break }
        Start-Sleep -Seconds 3
    }
    if (-not $atMenu) { Fail 'game never reached the main menu (InitialState)'; throw 'no_menu' }
    Write-Host "at main menu (InitialState)"

    $r = Invoke-Tool 'cbath/h3_provider_start' 40
    [System.IO.File]::WriteAllText((Join-Path $OutDir 'provider-start.json'), $r)
    if ($r -notmatch '"status"\s*:\s*"ok"') { Fail "h3_provider_start not ok: $r" }
    Write-Host "h3_provider_start sent"

    $deadline = (Get-Date).AddSeconds($PollTimeoutSec)
    $atScreen = $false
    while ((Get-Date) -lt $deadline) {
        $r = Invoke-Tool 'cbath/get_game_state' 25
        if ($r -match 'CustomBattleScreen') { $atScreen = $true; break }
        Start-Sleep -Seconds 3
    }
    if (-not $atScreen) { Fail 'CustomBattleScreen never reached'; throw 'no_screen' }
    Write-Host "at CustomBattleScreen"

    # Requested composition is applied inside this call, before ExecuteStart (script 3.4);
    # the reply carries the per-step read-back facts.
    $r = Invoke-StartConfigured
    [System.IO.File]::WriteAllText((Join-Path $OutDir 'start-from-screen.json'), $r)
    Write-Host "h3_start_from_screen (own=$ownComp enemy=$enemyComp) sent"
    $s = Get-ToolObject $r
    if ($s) {
        Write-Host "  config apply: status=$($s.status) map=$($s.map_selected_id)"
        if ($s.status -ne 'ok') { Fail "configured start status=$($s.status) : $r" }
    } else {
        Fail "h3_start_from_screen returned unparsable payload: $r"
    }

    $deadline = (Get-Date).AddSeconds($PollTimeoutSec)
    $running = $false
    while ((Get-Date) -lt $deadline) {
        $r = Invoke-Tool 'cbath/get_mission_state' 25
        if ($r -match 'mission_running.{0,3}:\s*true') { $running = $true; break }
        Start-Sleep -Seconds 3
    }
    if (-not $running) { Fail 'mission never started'; throw 'no_mission' }
    Write-Host "mission_running"

    # Deployment gate: the criterion must be auto_finish_deployment's own status=ok, which is
    # what both the authoritative zero-touch-3x.ps1:306-324 and TCL driver-script.ps1:229-236 use.
    # A not_ready reply also carries before.allow_ai_ticking (true before the engine takes over),
    # so regexing allow_ai_ticking would falsely pass on the first not_ready and FinishDeployment
    # would never run.
    $deadline = (Get-Date).AddSeconds($PollTimeoutSec)
    $deployed = $false
    $lastStatus = ''
    while ((Get-Date) -lt $deadline) {
        $r = Invoke-Tool 'cbath/auto_finish_deployment' 30
        [System.IO.File]::WriteAllText((Join-Path $OutDir 'deployment.json'), $r)
        $o = Get-ToolObject $r
        if ($o) {
            $lastStatus = [string]$o.status
            if ($lastStatus -eq 'ok') {
                Write-Host "deployment finished: status=ok team_setup_over=$($o.after.team_setup_over) allow_ai_ticking_after=$($o.after.allow_ai_ticking) player_team_formations=$($o.after.player_team_formations)"
                if ($o.after.allow_ai_ticking -ne $true) { Fail 'deployment ok but allow_ai_ticking_after is not true' }
                $deployed = $true
                break
            }
            if ($lastStatus -ne 'not_ready') { Fail "auto_finish_deployment status=$lastStatus : $r"; break }
        } else {
            Fail "auto_finish_deployment returned unparsable payload: $r"
            break
        }
        Start-Sleep -Seconds 2
    }
    if (-not $deployed) {
        if ($lastStatus -eq 'not_ready' -or $lastStatus -eq '') { Fail "auto_finish_deployment never reached ok (last status=$lastStatus)" }
    }

    $r = Invoke-Tool 'cbath/delegate_team_to_ai' 30
    [System.IO.File]::WriteAllText((Join-Path $OutDir 'delegate.json'), $r)
    $d = Get-ToolObject $r
    if ($d) { Write-Host "delegate_team_to_ai: status=$($d.status) all_ai_controlled=$($d.all_ai_controlled)" } else { Write-Host "delegate_team_to_ai: $r" }
    if ($d -and $d.status -ne 'ok') { Fail "delegate_team_to_ai status=$($d.status)" }

    # CBATH FormationProbe is an independent read-only observation path (it does not go through
    # 2.0 code) and is used to cross-check 2.0's own snapshot. Taken before and after the window.
    $r = Invoke-Tool 'cbath/get_all_formations' 30
    [System.IO.File]::WriteAllText((Join-Path $OutDir 'formations-early.json'), $r)
    Write-Host "formations-early captured"

    Write-Host "=== combat window: $CombatHoldSec s ==="
    $end = (Get-Date).AddSeconds($CombatHoldSec)
    while ((Get-Date) -lt $end) { Start-Sleep -Seconds 5 }

    $r = Invoke-Tool 'cbath/get_all_formations' 30
    [System.IO.File]::WriteAllText((Join-Path $OutDir 'formations-late.json'), $r)
    Write-Host "formations-late captured"

    $r = Invoke-Tool 'cbath/get_mission_state' 25
    [System.IO.File]::WriteAllText((Join-Path $OutDir 'mission-state-end.json'), $r)
    Write-Host "mission_state at end: $r"

    $r = Invoke-Tool 'cbath/get_last_run' 30
    [System.IO.File]::WriteAllText((Join-Path $OutDir 'last-run.json'), $r)
    Write-Host "get_last_run captured"
}
catch {
    Write-Host "EXCEPTION: $_"
    Fail "exception: $_"
    Report-NewCrashes
}
finally {
    try {
    Write-Host "=== teardown ==="
    try { Invoke-Tool 'cbath/end_mission' 40 | Out-Null; Write-Host "end_mission sent" } catch { Write-Host "end_mission failed: $_" }
    Start-Sleep -Seconds 5

    # CBATH's RunRecord is only complete in OnEndMission, so the full 1 Hz independent timeline
    # is only available after end_mission.
    try {
        $r = Invoke-Tool 'cbath/get_last_run' 30
        [System.IO.File]::WriteAllText((Join-Path $OutDir 'cbath-timeline.json'), $r)
        Write-Host "cbath-timeline captured"
    } catch { Write-Host "cbath-timeline capture failed: $_" }
    # Grab this module's log copy before the game goes away.
    if (Test-Path $cb2Log) {
        Copy-Item $cb2Log (Join-Path $OutDir 'CombatBrain2.log') -Force
        Write-Host "saved CombatBrain2.log ($((Get-Item $cb2Log).Length) bytes)"
    } else {
        Fail 'CombatBrain2.log was never created - module did not load'
    }

    try { Invoke-McpHttp -Tool 'games_stop' -Arguments @{ gameId = $GameId } -TimeoutSec 180 | Out-Null; Write-Host "games_stop sent" } catch { Write-Host "games_stop failed: $_" }

    $deadline = (Get-Date).AddSeconds(30)
    while ((Get-Date) -lt $deadline) {
        $a = @(Get-Process -Name 'Bannerlord' -ErrorAction SilentlyContinue | Where-Object { -not $_.HasExited })
        if ($a.Count -eq 0) { break }
        Start-Sleep -Seconds 2
    }
    $remain = @(Get-Process -Name 'Bannerlord' -ErrorAction SilentlyContinue | Where-Object { -not $_.HasExited })
    if ($remain.Count -gt 0) {
        Write-Host "WARN: Bannerlord still alive, killing pid $(($remain | ForEach-Object { $_.Id }) -join ',')"
        $remain | Stop-Process -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 3
    }
    $remain = @(Get-Process -Name 'Bannerlord' -ErrorAction SilentlyContinue | Where-Object { -not $_.HasExited })
    if ($remain.Count -gt 0) { Fail "residual Bannerlord process: $(($remain | ForEach-Object { $_.Id }) -join ',')" }
    else { Write-Host "no residual Bannerlord process" }

    Close-McpHttp

    # Take the log once more so teardown records are included in the evidence.
    if (Test-Path $cb2Log) { Copy-Item $cb2Log (Join-Path $OutDir 'CombatBrain2.log') -Force }

    Report-NewCrashes

    if (Test-Path $cbathOut) {
        $dst = Join-Path $OutDir 'CBATH'
        New-Item -ItemType Directory -Force -Path $dst | Out-Null
        Copy-Item (Join-Path $cbathOut '*') $dst -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "saved CBATH outputs"
    }
    } finally {
        Restore-Sentinels
    }
}

# Record requested configuration values verbatim (script 3.5.4: requested vs actual).
$reqRec = [ordered]@{
    config               = $Config
    own_army_size        = $ownArmySize
    own_composition      = $ownComp
    enemy_army_size      = $enemyArmySize
    enemy_composition    = $enemyComp
    p1_enabled           = $(if ($DisableP1) { 0 } else { 1 })
    probe_modes          = $(if ($DisableProbe) { '' } else { 'a' })
    probe_hold_seconds   = $ProbeHoldSec
    combat_hold_seconds  = $CombatHoldSec
}
[System.IO.File]::WriteAllText((Join-Path $OutDir 'requested-config.json'), ($reqRec | ConvertTo-Json -Compress))
Write-Host "requested-config.json written"

Write-Host "=== summary ==="
Write-Host "config: $Config"
Write-Host "out_dir: $OutDir"
if ($script:Failures.Count -eq 0) {
    Write-Host "RESULT: OK"
    exit 0
}
Write-Host "RESULT: FAILURES ($($script:Failures.Count))"
$script:Failures | ForEach-Object { Write-Host "  - $_" }
exit 1
