param(
    [ValidateSet('P', 'N', 'C')]
    [string]$Scenario = 'P',
    [string]$OutDir = '',
    [int]$CombatHoldSec = 180,
    [int]$PollTimeoutSec = 240,
    [int]$LaunchWaitSec = 900,
    [int]$McpPort = 19580,
    [string]$GameId = 'bannerlord148-cb2',
    [int]$OpponentDelaySec = 6,
    [switch]$WhatIf
)

$ErrorActionPreference = 'Stop'

$here = $PSScriptRoot
$gameDir = 'E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord'
# Dispatch 7 item 5: do not run against the other group's tools tree. Use this group's own
# task tree and pin the helper to a registered, byte-identical frozen copy.
$cbathTools = 'E:\MBmod\CBATH-P2\tools'
$cbathToolsFrozen = 'E:\MBmod\CombatBrain Autonomous test harness\tools\mcp-http.ps1'
$cbathToolsExpectedSha = 'DF1A86CF0719F5B3938F06B29884C327F8CE8ED30B4FC42EB22D6B6C08BE33EA'
$engineConfig = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'Mount and Blade II Bannerlord\Configs\engine_config.txt'
$cb2Log = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'CombatBrain2\CombatBrain2.log'
$cb2Dir = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'CombatBrain2'
$p1Sentinel = Join-Path $cb2Dir 'p1-command-loop.txt'
$p2Sentinel = Join-Path $cb2Dir 'p2-command-loop.txt'
$probeSentinel = Join-Path $cb2Dir 'eligibility-probe.txt'
$p2ProbeSentinel = Join-Path $cb2Dir 'p2-eligibility-probe.txt'
$p3Sentinel = Join-Path $cb2Dir 'p3-feedback.txt'
$cbathOut = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'CommandBrainATH'

# Scenario composition. Order: infantry, ranged, cavalry, horse archer.
# P3 dispatch 6: start from own 30/55/15/0 against enemy 70/20/10/0.
#   RangedEdge = 0.55 - 0.20 = +0.35 (>= +0.10 creation threshold)
#   own infantry 30 / ranged 55 / cavalry 15 -> three eligible formations, each >= 6
#
# P : positive-feedback window. The successor needs runway, and that is what fixes these
#     numbers. 004350 (own 113 vs enemy 22) did reach Reinforce (LE=0.429 LF=0.049) at
#     t=118.999, but the enemy was down to twelve men at that decision and dead three
#     seconds later, so plan-2 was overridden before it re-established guard/support and
#     sent an advance of its own. Every earlier attempt failed on one of the two sides of
#     that tension: enemy 90 produced no kill before our main was reclaimed by its native
#     Charge, and enemy 55 / enemy 34 turned into stop-loss or disengage because our own
#     casualties crossed 10% first. A larger enemy leaves the successor room after the 15%
#     trigger, while a larger own force keeps NF wide enough that 10% of it is not reached
#     during the same window.
#     Back to 22: run 214532 used enemy 45 and never engaged. The opponent Move pushed the
#     enemy infantry to the friendly main's position, but the two lines only closed at about
#     1.2 m/s and stayed 170 m apart for the whole 180 s window, so there were no losses at
#     all (DE=0 DF=0, 47 evaluations, every one Continue) and no decision to make. Enemy 22
#     is the count 004350 reached Reinforce with, so keeping it makes the new run a genuine
#     same-condition before/after comparison of the exit-confirmation fix.
#     Ratio note: both arms deliberately deviate from the suggested 100-vs-100 manning; the
#     P/C pair keeps composition, terrain and deployment identical and differs only in the
#     single ordinary opponent counter-action.
#     Ranged edge: the enemy must stay infantry-only. `RangedEdge` is our ranged share minus
#     theirs, and our own share is 40/113 = 0.398, so any enemy ranged arm above that fraction
#     makes it negative and `BuildCoordination` refuses the plan until the enemy's ranged
#     formations are already destroyed. Run 193423 used enemy 50,50 and only created its plan
#     at t=94.6 s, by which point the lines were 7.9 m apart: guard/support never reached
#     position, no advance was ever issued, and P3 never froze. Infantry-only keeps the edge
#     at +0.398 from the first sample, which is what gives guard and support their runway.
# N : loss-driven window. The enemy is heavier than us (130 against 100) so a real counter
#     attack costs our side more than the 10% the stop-loss gate needs; the ranged edge
#     (+0.35) still leaves a legitimate coordination plan to be stopped. One ordinary
#     opponent Move is again the only opponent action.
# C : control for whichever composition P uses. Identical composition/terrain/deployment
#     to P, and the ONLY difference is that the opponent does NOT apply that ordinary Move.
switch ($Scenario) {
    'P' {
        $ownArmySize = 300; $ownComp = '159,106,35,0'
        $enemyArmySize = 60; $enemyComp = '100,0,0,0'
        $opponentControl = $true
    }
    'N' {
        $ownArmySize = 100; $ownComp = '30,55,15,0'
        $enemyArmySize = 130; $enemyComp = '95,25,10,0'
        $opponentControl = $true
    }
    'C' {
        $ownArmySize = 300; $ownComp = '159,106,35,0'
        $enemyArmySize = 60; $enemyComp = '100,0,0,0'
        $opponentControl = $false
    }
}

if ([string]::IsNullOrEmpty($OutDir)) {
    $stamp = (Get-Date).ToString('yyyyMMdd-HHmmss')
    $OutDir = Join-Path $here "sessions\p3-$Scenario-$stamp"
}

Write-Host "scenario: $Scenario"
Write-Host "out_dir: $OutDir"

$script:Failures = @()
function Fail($msg) { $script:Failures += $msg; Write-Host "FAIL: $msg" }

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

function Note-Others([string]$msg) { Write-Host "NOTE: $msg" }

# End-state read-back demanded by the dispatch: no residual process, lock released, P1/P2/P3
# all closed, both probes empty, safely_exited=1. Read, not assumed - a cleanup step that
# reported success must still be corroborated by the actual files and process table.
function Assert-CleanEndState {
    $ownLeft = @(Get-Process -Name 'Bannerlord' -ErrorAction SilentlyContinue |
        Where-Object { -not $_.HasExited -and ($script:OwnGamePids -contains $_.Id) })
    if ($ownLeft.Count -gt 0) { $script:CleanupFailures += "end-state: own Bannerlord still running: $(($ownLeft | ForEach-Object { $_.Id }) -join ',')" }

    $helperLeft = 0
    if ($script:OwnGabsPid -gt 0) {
        $helperLeft = @(Get-Process -Id $script:OwnGabsPid -ErrorAction SilentlyContinue).Count
    }
    if ($helperLeft -gt 0) { $script:CleanupFailures += "end-state: mcp helper still running pid=$($script:OwnGabsPid)" }

    $opts = [ordered]@{}
    foreach ($pair in @(@('p1', $p1Sentinel), @('p2', $p2Sentinel), @('p3', $p3Sentinel))) {
        $v = if (Test-Path -LiteralPath $pair[1]) { (Get-Content -LiteralPath $pair[1] -Raw -ErrorAction SilentlyContinue) } else { $null }
        $opts[$pair[0]] = if ($null -eq $v) { '<absent>' } else { $v.Trim() }
        if ($opts[$pair[0]] -ne 'enabled=0') { $script:CleanupFailures += "end-state: $($pair[0]) sentinel is '$($opts[$pair[0]])', expected enabled=0" }
    }
    $probes = [ordered]@{}
    foreach ($pair in @(@('eligibility', $probeSentinel), @('p2_eligibility', $p2ProbeSentinel))) {
        $v = if (Test-Path -LiteralPath $pair[1]) { (Get-Content -LiteralPath $pair[1] -Raw -ErrorAction SilentlyContinue) } else { $null }
        $probes[$pair[0]] = if ($null -eq $v) { '<absent>' } else { $v.Trim() }
        if ($probes[$pair[0]] -ne '') { $script:CleanupFailures += "end-state: $($pair[0]) probe is not empty: '$($probes[$pair[0]])'" }
    }

    $safelyExited = '<unreadable>'
    if (Test-Path -LiteralPath $engineConfig) {
        $cfg = [System.IO.File]::ReadAllText($engineConfig)
        $m = [regex]::Match($cfg, 'safely_exited\s*=\s*(\d+)')
        if ($m.Success) { $safelyExited = $m.Groups[1].Value }
    }
    if ($safelyExited -ne '1') { $script:CleanupFailures += "end-state: safely_exited=$safelyExited, expected 1" }

    # The coordination lock is held by the exclusive wrapper, not by this script, and the wrapper
    # opens it FileShare.None. Reading it here therefore fails for as long as this run is still
    # inside the lock, so an unreadable lock is reported as "held", never as a cleanup failure:
    # the wrapper releases it after this script returns.
    $lockPath = 'E:\MBmod\BattleObservation-V1\coordination\shared-game-session.lock'
    $lockText = '<absent>'
    if (Test-Path -LiteralPath ($lockPath + '.disabled')) {
        $lockText = '<disabled by user>'
    } elseif (Test-Path -LiteralPath $lockPath) {
        try {
            $lockBytes = [System.IO.File]::ReadAllBytes($lockPath)
            $lockText = ($lockBytes | ForEach-Object { $_.ToString('x2') }) -join ''
        } catch {
            $lockText = '<held by the exclusive wrapper>'
        }
    }

    $rec = [ordered]@{
        own_game_pids        = $script:OwnGamePids
        ownership_basis      = $script:OwnershipBasis
        launch_process_started = $script:LaunchProcessStarted
        unowned_new_pids     = $script:UnownedNewPids
        session_connected    = $script:SessionConnected
        own_gabs_pid         = $script:OwnGabsPid
        residual_own_pids    = @($ownLeft | ForEach-Object { $_.Id })
        helper_residual      = $helperLeft
        options              = $opts
        probes               = $probes
        safely_exited        = $safelyExited
        lock_file_sha_hex    = if ($lockText.Length -gt 64) { '<file present>' } else { $lockText }
        lock_file_present    = (Test-Path -LiteralPath $lockPath)
        cleanup_failures     = $script:CleanupFailures
    }
    [System.IO.File]::WriteAllText((Join-Path $OutDir 'end-state.json'), ($rec | ConvertTo-Json -Depth 6 -Compress))
    Write-Host "end-state: p1=$($opts['p1']) p2=$($opts['p2']) p3=$($opts['p3']) probes_empty=$(($probes.Values | Where-Object { $_ -eq '' }).Count -eq 2) safely_exited=$safelyExited residual_own=$($rec.residual_own_pids.Count) helper_residual=$helperLeft"
}

# Dispatch 7 item 8: the run must end with the P1/P2/P3 options CLOSED and both probes EMPTY.
# Restoring whatever happened to be there before would leave an option enabled after a crashed
# earlier run, so the pre-run state is recorded as evidence and the closed state is written
# unconditionally.
function Restore-Sentinels {
    foreach ($path in @($p1Sentinel, $p2Sentinel, $probeSentinel, $p2ProbeSentinel, $p3Sentinel)) {
        if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Force }
    }
    New-Item -ItemType Directory -Force -Path $cb2Dir | Out-Null
    Set-Content -LiteralPath $p1Sentinel -Value "enabled=0" -Encoding Ascii
    Set-Content -LiteralPath $p2Sentinel -Value "enabled=0" -Encoding Ascii
    Set-Content -LiteralPath $p3Sentinel -Value "enabled=0" -Encoding Ascii
    Set-Content -LiteralPath $probeSentinel -Value "" -Encoding Ascii
    Set-Content -LiteralPath $p2ProbeSentinel -Value "" -Encoding Ascii
    Write-Host "sentinel switches cleared: p1=0 p2=0 p3=0 probes empty"
}

$script:PreRunSentinels = @()
function Capture-PreRunSentinels {
    foreach ($path in @($p1Sentinel, $p2Sentinel, $probeSentinel, $p2ProbeSentinel, $p3Sentinel)) {
        $existed = Test-Path -LiteralPath $path
        $value = if ($existed) { (Get-Content -LiteralPath $path -Raw -ErrorAction SilentlyContinue) } else { $null }
        $script:PreRunSentinels += [ordered]@{
            path    = $path
            existed = $existed
            value   = if ($null -eq $value) { '<absent>' } else { $value.Trim() }
        }
    }
}

# P3 runs on the P2 command loop: the P1 switch is explicitly 0 and P2 explicitly 1, so a
# stray p1 sentinel cannot leave both paths enabled. P3 itself is the outcome-feedback
# option and is enabled separately through p3-feedback.txt.
# Both eligibility probes and the P2 competition injection stay EMPTY for every P3 scene
# (dispatch 7 item 3): probe-driven eligibility loss and injected competition would make a
# negative result indistinguishable from a scripted one.
# ASCII only, so PS 5.1's ANSI Set-Content default is harmless.
function Write-Sentinels {
    New-Item -ItemType Directory -Force -Path $cb2Dir | Out-Null

    Write-Host "sentinel paths (pre-run state recorded in pre-run-sentinels.json)"
    Set-Content -LiteralPath $p1Sentinel -Value "enabled=0" -Encoding Ascii
    Set-Content -LiteralPath $p2Sentinel -Value "enabled=1" -Encoding Ascii
    Set-Content -LiteralPath $probeSentinel -Value "" -Encoding Ascii
    Set-Content -LiteralPath $p2ProbeSentinel -Value "" -Encoding Ascii
    Set-Content -LiteralPath $p3Sentinel -Value "enabled=1" -Encoding Ascii

    Write-Host "p1 sentinel:      $p1Sentinel  -> enabled=0"
    Write-Host "p2 sentinel:      $p2Sentinel  -> enabled=1"
    Write-Host "probe sentinel:   $probeSentinel  -> (empty)"
    Write-Host "p2 probe sentinel:$p2ProbeSentinel  -> (empty)"
    Write-Host "p3 sentinel:      $p3Sentinel  -> enabled=1"
}

if ($WhatIf) {
    Write-Host "=== whatif ==="
    Write-Host "scenario $Scenario : own army=$ownArmySize comp=$ownComp | enemy army=$enemyArmySize comp=$enemyComp"
    Write-Host "opponent counter-action: $opponentControl (delay ${OpponentDelaySec}s after the action freezes)"
    Write-Host "would aim: the formation named by the ACTION_FEEDBACK_FROZEN main=<team>/<slot> key, recorded in opponent-target.json"
    Write-Host "would clear: $cb2Log"
    Write-Host "would clear: $cbathOut"
    Write-Host "would run: h3_provider_start -> h3_start_from_screen -> auto_finish_deployment -> delegate_team_to_ai"
    Write-Host "would require: cbath/get_fight_to_death status=ok patches_installed=true enabled=true at pre_mission and post_deployment"
    Write-Host "would archive: fight-to-death-pre_mission.json , fight-to-death-post_deployment.json , fight-to-death-last-run.json"
    Write-Host "would sample: cbath/get_p2_formation_evidence at ~1 Hz for $CombatHoldSec s"
    Write-Host "would write: $p2Sentinel -> enabled=1 ; $p1Sentinel -> enabled=0 ; $p3Sentinel -> enabled=1"
    Write-Host "would leave empty: $probeSentinel , $p2ProbeSentinel"
    Write-Host "RESULT: WHATIF"
    exit 0
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
Capture-PreRunSentinels
[System.IO.File]::WriteAllText((Join-Path $OutDir 'pre-run-sentinels.json'),
    ($script:PreRunSentinels | ConvertTo-Json -Depth 4))
Write-Host "pre-run sentinel state saved to pre-run-sentinels.json"

# The MCP helper is the only piece taken from the other group's tree. Verify it byte-for-byte
# against the registered frozen copy before sourcing it, so a changed helper cannot silently
# alter how this scene talks to the game.
$helperPath = Join-Path $cbathTools 'mcp-http.ps1'
if (-not (Test-Path -LiteralPath $helperPath)) { Fail "mcp-http.ps1 missing: $helperPath"; throw 'no_mcp_helper' }
if (-not (Test-Path -LiteralPath $cbathToolsFrozen)) { Fail "frozen mcp-http.ps1 missing: $cbathToolsFrozen"; throw 'no_frozen_helper' }
$helperSha = (Get-FileHash -LiteralPath $helperPath -Algorithm SHA256).Hash
$frozenSha = (Get-FileHash -LiteralPath $cbathToolsFrozen -Algorithm SHA256).Hash
$helperSrc = [ordered]@{
    group_copy     = $helperPath
    group_copy_sha = $helperSha
    frozen_source  = $cbathToolsFrozen
    frozen_sha     = $frozenSha
    expected_sha   = $cbathToolsExpectedSha
    identical      = ($helperSha -eq $frozenSha)
}
[System.IO.File]::WriteAllText((Join-Path $OutDir 'mcp-helper-identity.json'), ($helperSrc | ConvertTo-Json -Compress))
Write-Host "mcp helper sha: $helperSha"
if ($helperSha -ne $cbathToolsExpectedSha) {
    Fail "mcp-http.ps1 in the task tree is not the registered frozen copy (expected $cbathToolsExpectedSha)"
    throw 'mcp_helper_mismatch'
}
. (Join-Path $cbathTools 'mcp-http.ps1')

function Invoke-Tool([string]$name, [int]$timeout = 30, $toolArgs = $null) {
    $inner = @{ gameId = $GameId; tool = $name; timeout = $timeout }
    if ($toolArgs) { $inner['arguments'] = $toolArgs }
    return Invoke-McpHttp -Tool 'games_call_tool' `
        -Arguments $inner `
        -TimeoutSec ($timeout + 40)
}

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

# Fight-to-death is a precondition of every combat test, not a reported detail: the switch is read
# back from the harness itself rather than assumed, and a missing tool or a switched-off mode stops
# the run before it produces evidence. Each read is archived as its own record so the mode actually
# in force at each stage can be audited later.
function Assert-FightToDeath([string]$stage) {
    $r = Invoke-Tool 'cbath/get_fight_to_death' 25
    if ([string]::IsNullOrWhiteSpace($r)) { Fail "cbath/get_fight_to_death returned nothing at ${stage}"; throw 'no_fight_to_death' }
    [System.IO.File]::WriteAllText((Join-Path $OutDir ("fight-to-death-$stage.json")), $r)
    $f = Get-ToolObject $r
    if (-not $f) { Fail "cbath/get_fight_to_death unreadable at ${stage}: $r"; throw 'no_fight_to_death' }
    $status = [string]$f.status
    $installed = ($f.patches_installed -eq $true)
    $enabled = ($f.enabled -eq $true)
    Write-Host "fight_to_death[$stage]: status=$status patches_installed=$installed enabled=$enabled policy=$($f.policy) mission_active=$($f.mission_active) panicked=$($f.observed_panicked) fleeing=$($f.observed_fleeing)"
    if ($status -ne 'ok' -or -not $installed -or -not $enabled) {
        Fail "fight_to_death not in force at ${stage}: status=$status patches_installed=$installed enabled=$enabled"
        throw 'fight_to_death_off'
    }
    $panicked = if ($null -eq $f.observed_panicked) { 0 } else { [int]$f.observed_panicked }
    $fleeing = if ($null -eq $f.observed_fleeing) { 0 } else { [int]$f.observed_fleeing }
    if ($panicked -ne 0 -or $fleeing -ne 0) {
        Note-Others "fight_to_death[$stage] reports observed_panicked=$panicked observed_fleeing=$fleeing; investigate rather than assume the block held"
    }
}

# Side mapping: our side is the NON-player AI team, which this fixed entry exposes as the
# enemy side. Parameter names must match CbathTools.H3StartFromScreen exactly.
function Invoke-StartConfigured {
    $toolArgs = @{
        playerArmySize    = $enemyArmySize
        playerComposition = $enemyComp
        enemyArmySize     = $ownArmySize
        enemyComposition  = $ownComp
    }
    return Invoke-Tool 'cbath/h3_start_from_screen' 60 $toolArgs
}

# The opponent of the P3 loop is the game's PLAYER team (that is the side CB2 does not
# control). Pick its largest pure-Infantry formation as the single object of the one
# ordinary counter-action, and read that decision out of the engine evidence, not from
# any mod log.
# The engine evidence exposes active_center / positions.current_position and
# engine_unit_count. Reading a non-existent "position"/"unit_count" silently yields 0,
# which would aim the counter-action at the world origin instead of the real line.
function Get-FormationPoint($f) {
    if ($null -ne $f.active_center -and $f.active_center_known) {
        return [pscustomobject]@{ x = [double]$f.active_center.x; y = [double]$f.active_center.y }
    }
    if ($null -ne $f.positions -and $null -ne $f.positions.current_position) {
        return [pscustomobject]@{ x = [double]$f.positions.current_position.x
                                  y = [double]$f.positions.current_position.y }
    }
    return $null
}

function Find-OpponentInfantry($evidenceObj) {
    if ($null -eq $evidenceObj -or $null -eq $evidenceObj.teams) { return $null }
    foreach ($team in $evidenceObj.teams) {
        if (-not $team.is_player_team) { continue }
        $best = $null
        $bestUnits = -1
        foreach ($f in $team.formations) {
            $cls = [string]$f.formation_class
            if ($cls -notmatch 'Infantry') { continue }
            if ($cls -match 'Ranged') { continue }
            $u = [int]$f.engine_unit_count
            if ($u -gt $bestUnits) { $bestUnits = $u; $best = $f }
        }
        if ($null -ne $best) {
            $pt = Get-FormationPoint $best
            if ($null -eq $pt) { return $null }
            return [pscustomobject]@{
                team_index      = [int]$team.team_index
                formation_index = [int]$best.formation_index
                unit_count      = $bestUnits
                x               = $pt.x
                y               = $pt.y
            }
        }
        return $null
    }
    return $null
}

# The formation CB2 actually froze as Main, located by the key the freeze line itself carries.
# main=<teamIndex>/<formationIndex> is a formation slot, not a size class: the largest formation
# on our side can be a ranged line the plan never froze, and aiming at that would look like a
# counter-action while pointing somewhere the plan was never attacking. Anything other than the
# keyed formation is a different target, so it must not be reported as Main.
function Find-MainByKey($evidenceObj, [string]$key) {
    if ($null -eq $evidenceObj -or $null -eq $evidenceObj.teams) { return $null }
    if ($key -notmatch '^(\d+)/(\d+)$') { return $null }
    $ti = [int]$Matches[1]
    $fi = [int]$Matches[2]
    foreach ($team in $evidenceObj.teams) {
        if ([int]$team.team_index -ne $ti) { continue }
        foreach ($f in $team.formations) {
            if ([int]$f.formation_index -ne $fi) { continue }
            $pt = Get-FormationPoint $f
            if ($null -eq $pt) { return $null }
            return [pscustomobject]@{
                team_index      = $ti
                formation_index = $fi
                identity        = [string]$f.formation_identity
                class           = [string]$f.formation_class
                unit_count      = [int]$f.engine_unit_count
                x               = $pt.x
                y               = $pt.y
            }
        }
    }
    return $null
}

# Latest main key in the log. A later freeze supersedes an earlier one, and only the key on the
# freeze line ties the aim point to the plan that is actually being measured.
function Get-FrozenMainKey([string]$logText) {
    $hits = [regex]::Matches($logText, 'ACTION_FEEDBACK_FROZEN[^\r\n]*\bmain=(\d+)/(\d+)')
    if ($hits.Count -eq 0) { return '' }
    $last = $hits[$hits.Count - 1]
    return $last.Groups[1].Value + '/' + $last.Groups[2].Value
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

if (Test-Path $cbathOut) { Remove-Item $cbathOut -Recurse -Force }
Write-Host "preflight: CommandBrainATH cleared"

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

# Resource bookkeeping, so teardown can end exactly what this run acquired and nothing else.
# The helper must be opened INSIDE the try: it starts gabs before probing the endpoint and
# throws when the endpoint never becomes ready, and with the call outside the try that throw
# left the acquired helper process running with no cleanup.
$script:OwnGabsPid = 0
$script:OwnGamePids = @()
$script:McpOpened = $false
$script:GameStarted = $false
$script:SessionConnected = $false
# A before/after diff proves "newly appeared", not "started by this run", and the launch flag only
# says that a process was started at some point - neither one names a PID. A single new same-name
# process is therefore a CANDIDATE: the diff is taken immediately after the launch call, but the
# process it counted can exit before anything else reads it, and another group's process can take
# its place. What turns a candidate into an owned PID is a PID that this run's own launch context
# names. Until such a binding arrives, nothing is killable.
$script:LaunchCandidatePids = @()
$script:LaunchProcessStarted = $false
$script:OwnershipResolved = $false
$script:RuntimeGamePid = 0
# PIDs named by this run's own launch context. See Get-LaunchContextPids below.
$script:LaunchContextPids = @()
$script:OwnershipBasis = ''
$script:UnownedNewPids = @()

# Which PIDs did THIS run's launch produce? The answer can only come from state this run owns: the
# GABS config dir the helper was pointed at with --configDir. GABS stamps runtime.json with the
# workload PID of the launch it performed and truncates launch.log while spawning, so a PID found
# there is one this run's launch produced. The shared Documents identity directory is deliberately
# NOT used: another group running CBATH writes its own game's identity file there, so a file in it
# proves which process announced a PID but never who started that process.
$script:GabsGameDir = Join-Path $cfgDir $GameId
function Get-LaunchContextPids {
    $pids = @()
    $rt = Join-Path $script:GabsGameDir 'runtime.json'
    if (Test-Path -LiteralPath $rt) {
        try {
            $j = [System.IO.File]::ReadAllText($rt) | ConvertFrom-Json
            if ($j.gamePid -and [int]$j.gamePid -gt 0) { $pids += [int]$j.gamePid }
        } catch { }
    }
    $ll = Join-Path $script:GabsGameDir 'launch.log'
    if (Test-Path -LiteralPath $ll) {
        try { $txt = [System.IO.File]::ReadAllText($ll) } catch { $txt = '' }
        # CBATH's identity block puts pid= on its own line; CB2's puts it inline in one long line.
        foreach ($m in [regex]::Matches($txt, '(?m)^pid=(\d+)\s*$')) { $pids += [int]$m.Groups[1].Value }
        foreach ($m in [regex]::Matches($txt, 'RUNTIME_IDENTITY[^\r\n]*\bpid=(\d+)')) { $pids += [int]$m.Groups[1].Value }
    }
    return @($pids | Select-Object -Unique)
}

try {
    $script:OwnGabsPid = Open-McpHttp -ConfigDir $cfgDir -ListenPort $McpPort
    $script:McpOpened = $true
    Write-Host "gabs_pid: $script:OwnGabsPid"

    Write-Sentinels
    # The launch call can produce the game process and still fail afterwards (endpoint timeout,
    # refused connect), so the new-PID set is computed by diffing around the call in a finally:
    # a snapshot taken only on the success path would lose that information entirely.
    $beforePids = @(Get-Process -Name 'Bannerlord' -ErrorAction SilentlyContinue |
        Where-Object { -not $_.HasExited } | ForEach-Object { $_.Id })
    $beforeLaunchContextPids = Get-LaunchContextPids
    try {
        $r = Invoke-McpHttp -Tool 'games_start' -Arguments @{ gameId = $GameId; timeout = 150 } -TimeoutSec $LaunchWaitSec
        Write-Host "games_start: $r"
        # The launch result is the run's own statement that it started a process; parse it before
        # the diff so the attribution below can use it.
        if ($r -match '"processStarted"\s*:\s*true') { $script:LaunchProcessStarted = $true }
        if ($r -match 'already_running') { Fail 'games_start reported already_running'; throw 'already_running' }
    }
    finally {
        $newPids = @(Get-Process -Name 'Bannerlord' -ErrorAction SilentlyContinue |
            Where-Object { -not $_.HasExited } | ForEach-Object { $_.Id } |
            Where-Object { $beforePids -notcontains $_ })
        # Only PIDs that appeared in this run's own launch context AND are alive among the new
        # processes. Both halves matter: the context half proves this run's GABS performed the
        # launch, the alive half keeps a PID that has already exited out of the killable list.
        $script:LaunchContextPids = @(Get-LaunchContextPids | Where-Object { $beforeLaunchContextPids -notcontains $_ })
        # A PID named by another group's process is not in this run's context at all, and a PID
        # this run started and lost is no longer alive - neither one becomes killable here.
        $contextCandidate = @($script:LaunchContextPids | Where-Object { $newPids -contains $_ })
        if ($contextCandidate.Count -eq 1) {
            $script:OwnGamePids = @($contextCandidate[0])
            $script:LaunchCandidatePids = @()
            $script:OwnershipBasis = 'launch_context_pid_matched_new_process'
        } elseif ($contextCandidate.Count -gt 1) {
            # Two processes are both named by this run's own launch context and both are alive.
            # Nothing available here says which one this run should end.
            $script:LaunchCandidatePids = @()
            $script:UnownedNewPids = $newPids
            $script:OwnershipBasis = 'ambiguous_launch_context_pids'
        } elseif ($script:LaunchContextPids.Count -gt 0) {
            # This run's launch named a PID that is no longer alive: the process it started has
            # exited, and anything the diff still sees is somebody else's.
            $script:LaunchCandidatePids = @()
            $script:UnownedNewPids = $newPids
            $script:OwnershipBasis = if ($newPids.Count -eq 0) { 'launched_process_exited' } else { 'launched_process_exited_other_process_present' }
        } elseif ($newPids.Count -eq 0) {
            $script:OwnershipBasis = 'no_new_process'
        } elseif ($script:LaunchProcessStarted) {
            $script:LaunchCandidatePids = $newPids
            $script:OwnershipBasis = 'launch_candidate_pending_runtime_identity'
        } else {
            $script:UnownedNewPids = $newPids
            $script:OwnershipBasis = 'new_process_without_launch_result'
        }
    }
    # The launch result is this run's own statement that it started a game, so it stays true even
    # when the new process could not be attributed.
    if ($script:LaunchProcessStarted) { $script:GameStarted = $true }
    # Every outcome above except "still waiting for an identity" is a settled decision. The teardown
    # report must not call a settled attribution unresolved just because the session never got far
    # enough to confirm it a second way.
    if ($script:OwnershipBasis -ne 'launch_candidate_pending_runtime_identity') { $script:OwnershipResolved = $true }
    Write-Host "own game pids: $(if ($script:OwnGamePids.Count -gt 0) { $script:OwnGamePids -join ',' } else { '<none>' }) basis=$script:OwnershipBasis"

    $deadline = (Get-Date).AddSeconds($LaunchWaitSec)
    $connected = $false
    $attempt = 0
    while ((Get-Date) -lt $deadline) {
        if (-not (Get-Process -Name 'Bannerlord' -ErrorAction SilentlyContinue | Where-Object { -not $_.HasExited })) {
            Fail 'Bannerlord exited during launch'; throw 'died_during_launch'
        }
        $attempt++
        $forceTook = ''
        try {
            # forceTakeover stays false: if another group already owns the game session, this run
            # must be refused rather than seizing their connection without a hand-over.
            $r = Invoke-McpHttp -Tool 'games_connect' -Arguments @{ gameId = $GameId; timeout = 40; forceTakeover = $false } -TimeoutSec 90
            $short = if ($r.Length -gt 300) { $r.Substring(0, 300) } else { $r }
            Write-Host "connect attempt $attempt : $short"
            if ($r -match 'Discovered|Re-synced|Already connected') {
                $connected = $true
                $script:SessionConnected = $true
                Write-Host "games_connect ok"
                break
            }
            if ($r -match 'Force-took') { $forceTook = $short }
        } catch {
            Write-Host "connect attempt $attempt threw: $_"
        }
        # Checked outside the try so the refusal is not swallowed by the retry handler above.
        if ($forceTook.Length -gt 0) {
            Fail "games_connect force-took a session this run did not own: $forceTook"
            throw 'force_takeover_refused'
        }
        Start-Sleep -Seconds 10
    }
    if (-not $connected) { Fail 'games_connect never succeeded'; throw 'no_connect' }

    $deadline = (Get-Date).AddSeconds($PollTimeoutSec)
    $pingOk = $false
    $runtimePid = 0
    while ((Get-Date) -lt $deadline) {
        try {
            $r = Invoke-Tool 'cbath/ping' 20
            if ($r -match 'pong') {
                $pingOk = $true
                $po = Get-ToolObject $r
                if ($po -and $null -ne $po.pid) {
                    $runtimePid = [int]$po.pid
                } else {
                    $m = [regex]::Match($r, '"pid"\s*:\s*(\d+)')
                    if ($m.Success) { $runtimePid = [int]$m.Groups[1].Value }
                }
                Write-Host "ping ok (runtime pid=$runtimePid)"
                break
            }
        } catch { }
        Start-Sleep -Seconds 3
    }
    if (-not $pingOk) { Fail 'cbath ping never answered'; throw 'no_ping' }
    $script:RuntimeGamePid = $runtimePid

    # A connected session that reports the PID it runs in adds liveness to a PID this run's own
    # launch context names: the process answered, so it is alive now, and the context proves this
    # run's GABS performed its launch. A PID the launch context does not name is not this run's -
    # another group's game answers a ping exactly as well as ours.
    #
    # The context is read again here rather than reusing the launch-time snapshot: the child prints
    # its identity while it loads, which can land just after games_start returned. By now the
    # session answered, so a slow record still binds and a normal run does not end up reported as
    # an unresolved cleanup it never was.
    $script:LaunchContextPids = @(Get-LaunchContextPids | Where-Object { $beforeLaunchContextPids -notcontains $_ })
    if ($script:OwnGamePids.Count -eq 0) {
        if ($runtimePid -gt 0 -and ($script:LaunchContextPids -contains $runtimePid)) {
            $script:OwnGamePids = @($runtimePid)
            $script:OwnershipBasis = 'session_runtime_pid_matched_launch_context'
        } elseif ($script:OwnershipBasis -eq '' -or $script:OwnershipBasis -eq 'launch_candidate_pending_runtime_identity') {
            # Keep a more specific launch-phase finding (process exited, several context pids) as
            # the recorded basis; this lookup only replaces the "still waiting" placeholder.
            $script:OwnershipBasis = if ($runtimePid -gt 0) { 'session_runtime_pid_not_in_launch_context' } else { 'runtime_identity_unavailable' }
        }
        # Neither the pid the session reported nor anything the launch context named could be bound,
        # so no PID is killable. They are still reported: teardown must neither kill them nor stay
        # silent about them.
        $script:UnownedNewPids = @(@($script:UnownedNewPids) + @($script:LaunchCandidatePids) | Select-Object -Unique)
        $script:LaunchCandidatePids = @()
    }
    $script:OwnershipResolved = $true
    Write-Host "game ownership resolved: pids=$(if ($script:OwnGamePids.Count -gt 0) { $script:OwnGamePids -join ',' } else { '<none>' }) basis=$script:OwnershipBasis"

    # cbath/ping answers from the TCP thread and says nothing about the game's tick loop, so it
    # cannot tell "the bridge is up" apart from "the bridge is up and the main thread is still
    # dispatching". cbath/get_build_identity is served from the main thread, so it is the first
    # reading that proves the thread the whole run depends on is still alive. The wait is bounded
    # because the main thread stops dispatching near the end of startup instead of failing.
    $mainThreadDeadline = (Get-Date).AddSeconds($PollTimeoutSec)
    $mainThreadOk = $false
    while ((Get-Date) -lt $mainThreadDeadline) {
        if (-not (Get-Process -Name 'Bannerlord' -ErrorAction SilentlyContinue | Where-Object { -not $_.HasExited })) {
            Fail 'Bannerlord exited while waiting for the main thread'
            throw 'died_during_launch'
        }
        $r = ''
        try {
            $r = Invoke-Tool 'cbath/get_build_identity' 25
            [System.IO.File]::WriteAllText((Join-Path $OutDir 'build-identity.json'), $r)
            $bi = Get-ToolObject $r
            if ($bi -and $bi.build_id) {
                $mainThreadOk = $true
                Write-Host "main thread dispatching: build_id=$($bi.build_id) pid=$($bi.pid) ticks=$($bi.main_thread.ticks) enqueued=$($bi.main_thread.enqueued) completed=$($bi.main_thread.completed)"
                break
            }
            Write-Host "main thread not dispatching yet: $($r.Substring(0, [Math]::Min(200, $r.Length)))"
        } catch {
            $msg = "$_"
            Write-Host "main thread probe threw: $msg"
            if ($msg -match 'forcibly closed|connection refused|not connected|Not connected|No connection') {
                Fail "the game session was lost while waiting for the main thread: $msg"
                throw 'session_lost_before_main_thread'
            }
        }
        Start-Sleep -Seconds 3
    }
    if (-not $mainThreadOk) {
        Fail "the game's main thread never served a tool call within $PollTimeoutSec s of the bridge answering"
        throw 'no_main_thread'
    }

    # Both entries the P3 evidence depends on must exist in the loaded harness: the member
    # evidence reader and the scenario-only opponent control. A missing tool means the
    # deployed harness is not the reviewed artifact, so nothing below would be valid.
    $r = Invoke-Tool 'cbath/get_p2_formation_evidence' 25
    $ev0 = Get-ToolObject $r
    if ($ev0 -and $ev0.status) {
        Write-Host "p2 evidence tool present: status=$($ev0.status)"
    } else {
        Fail "cbath/get_p2_formation_evidence unavailable: $r"
        throw 'no_p2_evidence_tool'
    }

    if ($opponentControl) {
        $r = Invoke-Tool 'cbath/p3_opponent_hold' 25 @{ teamIndex = 0; formationIndex = 0 }
        $p3o = Get-ToolObject $r
        if ($p3o -and ($p3o.status -eq 'ok' -or $p3o.status -eq 'no_mission')) {
            Write-Host "p3 opponent control tool present: status=$($p3o.status)"
        } else {
            Fail "cbath/p3_opponent_hold unavailable: $r"
            throw 'no_p3_opponent_tool'
        }
    }

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

    Assert-FightToDeath 'pre_mission'

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

    # Deployment gate: the criterion must be auto_finish_deployment's own status=ok. A not_ready
    # reply also carries before.allow_ai_ticking (true before the engine takes over), so regexing
    # allow_ai_ticking would falsely pass on the first not_ready.
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

    Assert-FightToDeath 'post_deployment'

    $r = Invoke-Tool 'cbath/delegate_team_to_ai' 30
    [System.IO.File]::WriteAllText((Join-Path $OutDir 'delegate.json'), $r)
    $d = Get-ToolObject $r
    if ($d) { Write-Host "delegate_team_to_ai: status=$($d.status) all_ai_controlled=$($d.all_ai_controlled)" } else { Write-Host "delegate_team_to_ai: $r" }
    if ($d -and $d.status -ne 'ok') { Fail "delegate_team_to_ai status=$($d.status)" }

    # Authoritative pre-window baseline: the independent engine read, not the mod's own log.
    $r = Invoke-Tool 'cbath/get_all_formations' 30
    [System.IO.File]::WriteAllText((Join-Path $OutDir 'formations-early.json'), $r)
    Write-Host "formations-early captured"

    $r = Invoke-Tool 'cbath/get_p2_formation_evidence' 30
    [System.IO.File]::WriteAllText((Join-Path $OutDir 'p2-evidence-early.json'), $r)
    Write-Host "p2-evidence-early captured"

    # ~1 Hz independent sampling for the whole window, keeping the real mission timestamp.
    # The engine-side reads are the acceptance evidence; the mod's own log is what they are
    # cross-checked against, never the reverse.
    Write-Host "=== combat window: $CombatHoldSec s (~1 Hz independent sampling) ==="
    $opponentDone = $false
    $opponentArmedAt = $null
    $frozenMainKey = ''
    $end = (Get-Date).AddSeconds($CombatHoldSec)
    $tick = 0
    while ((Get-Date) -lt $end) {
        $tick++
        $r = Invoke-Tool 'cbath/get_p2_formation_evidence' 25
        [System.IO.File]::WriteAllText((Join-Path $OutDir ("p2-evidence-{0:d4}.json" -f $tick)), $r)
        $e = Get-ToolObject $r
        if ($e -and $e.status) {
            $mt = if ($e.mission_time) { $e.mission_time } else { '?' }
            Write-Host ("  t{0:d4} mission_time={1} status={2}" -f $tick, $mt, $e.status)
        } else {
            Write-Host ("  t{0:d4} evidence unparsable: {1}" -f $tick, $r)
        }

        # Control scene: exactly one ordinary opponent counter-action, applied during the
        # real action phase. It is armed by the mod's own freeze line (t0 = the moment the
        # main advance was actually written), then delayed, so it never pre-empts the action.
        if ($opponentControl -and -not $opponentDone -and (Test-Path $cb2Log)) {
            if ($null -eq $opponentArmedAt) {
                $tail = ''
                try { $tail = [System.IO.File]::ReadAllText($cb2Log) } catch { }
                if ($tail -match 'ACTION_FEEDBACK_FROZEN') {
                    $opponentArmedAt = Get-Date
                    $frozenMainKey = Get-FrozenMainKey $tail
                    Write-Host "  opponent counter-action armed at $($opponentArmedAt.ToString('HH:mm:ss')) (action t0 frozen, main=$frozenMainKey)"
                }
            } elseif (((Get-Date) - $opponentArmedAt).TotalSeconds -ge $OpponentDelaySec) {
                $opponentDone = $true
                $op = Find-OpponentInfantry $e
                if ($null -eq $op) {
                    Fail 'opponent formation not found in engine evidence; counter-action not applied'
                    [System.IO.File]::WriteAllText((Join-Path $OutDir 'opponent-action.json'),
                        '{"status":"formation_not_found"}')
                } else {
                    # Push the opponent's main body onto the formation CB2 actually froze as
                    # Main. A plain Move to a world point is an ordinary order; the aim is the
                    # keyed formation read from the same engine evidence, so the counter-action
                    # reaches the line the plan is really measured on. Without the key there is
                    # no Main to aim at - guessing one would silently record a different target.
                    $ours = $null
                    # The freeze that armed this action is not necessarily still the latest one
                    # when it fires, so re-read the key: aiming at a superseded Main would record
                    # a target the plan is no longer measured on.
                    $applyKey = $frozenMainKey
                    $applyTail = ''
                    try { $applyTail = [System.IO.File]::ReadAllText($cb2Log) } catch { }
                    $latestKey = Get-FrozenMainKey $applyTail
                    if ($latestKey.Length -gt 0) { $applyKey = $latestKey }
                    if ($applyKey.Length -gt 0) { $ours = Find-MainByKey $e $applyKey }
                    if ($null -eq $ours) {
                        Fail "frozen main formation $applyKey not found in engine evidence; counter-action not applied"
                        [System.IO.File]::WriteAllText((Join-Path $OutDir 'opponent-action.json'),
                            '{"status":"frozen_main_not_found"}')
                    } else {
                        $tx = [math]::Round($ours.x, 3)
                        $ty = [math]::Round($ours.y, 3)
                        $target = [pscustomobject]@{
                            status           = 'aimed'
                            frozen_main_key  = $applyKey
                            armed_main_key   = $frozenMainKey
                            team_index       = $ours.team_index
                            formation_index  = $ours.formation_index
                            identity         = $ours.identity
                            class            = $ours.class
                            unit_count       = $ours.unit_count
                            x                = $tx
                            y                = $ty
                            aimed_by         = 'action_feedback_frozen_main_key'
                        }
                        [System.IO.File]::WriteAllText((Join-Path $OutDir 'opponent-target.json'),
                            ($target | ConvertTo-Json -Compress))
                        Write-Host ("  opponent counter-action aims at frozen Main {0} = {1} {2} units {3} @ ({4},{5})" -f `
                            $applyKey, $ours.identity, $ours.class, $ours.unit_count, $tx, $ty)
                        $oa = Invoke-Tool 'cbath/p3_opponent_move' 30 @{
                            teamIndex      = $op.team_index
                            formationIndex = $op.formation_index
                            x              = $tx
                            y              = $ty
                        }
                        [System.IO.File]::WriteAllText((Join-Path $OutDir 'opponent-action.json'), $oa)
                        Write-Host "  opponent counter-action: $oa"
                        $oap = Get-ToolObject $oa
                        if (-not $oap -or $oap.status -ne 'ok') { Fail "opponent counter-action failed: $oa" }
                    }
                }
            }
        }

        Start-Sleep -Milliseconds 900
    }
    Write-Host "independent samples written: $tick"

    if ($opponentControl) {
        if (-not $opponentDone) { Fail 'opponent counter-action never applied during the window' }
        # Hand the formation back once, so the control difference stays a single action.
        $op = Find-OpponentInfantry (Get-ToolObject (Invoke-Tool 'cbath/get_p2_formation_evidence' 25))
        if ($null -ne $op) {
            $rel = Invoke-Tool 'cbath/p3_opponent_release' 30 @{
                teamIndex = $op.team_index; formationIndex = $op.formation_index
            }
            [System.IO.File]::WriteAllText((Join-Path $OutDir 'opponent-release.json'), $rel)
            Write-Host "opponent release: $rel"
        }
    }

    $r = Invoke-Tool 'cbath/get_all_formations' 30
    [System.IO.File]::WriteAllText((Join-Path $OutDir 'formations-late.json'), $r)
    Write-Host "formations-late captured"

    $r = Invoke-Tool 'cbath/get_mission_state' 25
    [System.IO.File]::WriteAllText((Join-Path $OutDir 'mission-state-end.json'), $r)
    Write-Host "mission_state at end: $r"

    if (Test-Path $cb2Log) {
        Copy-Item $cb2Log (Join-Path $OutDir 'CombatBrain2.live.log') -Force
        Write-Host "live CombatBrain2.log snapshot saved"
    }
}
catch {
    Write-Host "EXCEPTION: $_"
    Fail "exception: $_"
    Report-NewCrashes
}
finally {
    Write-Host "=== teardown ==="
    $script:CleanupFailures = @()
    # Every teardown step is guarded separately: a cleanup step that throws must be recorded as
    # its own failure instead of masking the run's real result, and later steps must still run.
    try {
        # end_mission is a remote action on the game session, so it needs an actually owned
        # session, not merely a reachable helper: an MCP endpoint that answers does not prove
        # this run ever connected to the game it would be ending.
        if ($script:SessionConnected) {
            Invoke-Tool 'cbath/end_mission' 40 | Out-Null
            Write-Host "end_mission sent"
        } else {
            Write-Host "end_mission skipped: this run never connected to a game session"
        }
    }
    catch { Write-Host "end_mission failed: $_"; $script:CleanupFailures += "end_mission: $_" }
    if ($script:McpOpened) {
    Start-Sleep -Seconds 5

    # CBATH's RunRecord is only complete in OnEndMission, so the full 1 Hz independent
    # timeline - and the terminal-state records - only exist after end_mission.
    try {
        $r = Invoke-Tool 'cbath/get_last_run' 30
        [System.IO.File]::WriteAllText((Join-Path $OutDir 'cbath-timeline.json'), $r)
        Write-Host "cbath-timeline captured"
        # The timeline is where the mode actually in force during the battle is recorded: the root
        # field and every 1 Hz sample carry their own fight_to_death. Extracting them here keeps the
        # switch state of the battle that produced this evidence auditable on its own.
        $lr = Get-ToolObject $r
        if ($lr) {
            $samples = @($lr.samples)
            # Property access, not ['name']: these are PSCustomObjects, and indexing one by key
            # yields nothing under Windows PowerShell 5.1, which is where this script runs.
            $sampleStates = @($samples | ForEach-Object { $_.fight_to_death })
            $sampleEnabled = @($sampleStates | Where-Object { $_ -and $_.enabled -eq $true }).Count
            $sampleDisabled = @($sampleStates | Where-Object { $_ -and $_.enabled -eq $false }).Count
            $samplePanicked = 0
            $sampleFleeing = 0
            foreach ($s in $sampleStates) {
                if ($s) {
                    if ($s.observed_panicked) { $samplePanicked = [Math]::Max($samplePanicked, [int]$s.observed_panicked) }
                    if ($s.observed_fleeing) { $sampleFleeing = [Math]::Max($sampleFleeing, [int]$s.observed_fleeing) }
                }
            }
            $rec = [ordered]@{
                root                = $lr.fight_to_death
                sample_count        = $samples.Count
                samples_with_state  = @($sampleStates | Where-Object { $_ }).Count
                sample_enabled_true = $sampleEnabled
                sample_enabled_false = $sampleDisabled
                max_observed_panicked = $samplePanicked
                max_observed_fleeing  = $sampleFleeing
            }
            [System.IO.File]::WriteAllText((Join-Path $OutDir 'fight-to-death-last-run.json'), ($rec | ConvertTo-Json -Depth 6 -Compress))
            Write-Host "fight_to_death[last_run]: root_enabled=$($lr.fight_to_death.enabled) samples=$($samples.Count) enabled_true=$sampleEnabled enabled_false=$sampleDisabled panicked=$samplePanicked fleeing=$sampleFleeing"
            if ($sampleDisabled -gt 0) { Fail "fight_to_death was off for $sampleDisabled of $($samples.Count) sampled battle seconds" }
            # An absent sample field and a present-but-disabled one are different failures; reporting the
            # first for the second would send the next reader looking for a missing field that is there.
            $sampleMissing = $samples.Count - @($sampleStates | Where-Object { $_ }).Count
            if ($sampleMissing -gt 0) { Fail "fight_to_death state missing from $sampleMissing of $($samples.Count) battle samples" }
        } elseif ($script:SessionConnected) {
            Fail "cbath/get_last_run unreadable: $r"
        }
    } catch { Write-Host "cbath-timeline capture failed: $_" }
    try {
        $r = Invoke-Tool 'cbath/get_p2_formation_evidence' 25
        [System.IO.File]::WriteAllText((Join-Path $OutDir 'p2-evidence-end.json'), $r)
        Write-Host "p2-evidence-end captured"
    } catch { Write-Host "p2-evidence-end capture failed: $_" }
    }

    if (Test-Path $cb2Log) {
        Copy-Item $cb2Log (Join-Path $OutDir 'CombatBrain2.log') -Force
        Write-Host "saved CombatBrain2.log ($((Get-Item $cb2Log).Length) bytes)"
    } elseif ($script:GameStarted) {
        Fail 'CombatBrain2.log was never created - module did not load'
    }

    try {
        if ($script:McpOpened -and $script:SessionConnected) {
            Invoke-McpHttp -Tool 'games_stop' -Arguments @{ gameId = $GameId } -TimeoutSec 180 | Out-Null
            Write-Host "games_stop sent"
        } elseif ($script:GameStarted) {
            Write-Host "games_stop skipped: session was never connected by this run"
        } else {
            Write-Host "games_stop skipped: this run never started a game"
        }
    } catch { Write-Host "games_stop failed: $_"; $script:CleanupFailures += "games_stop: $_" }

    $deadline = (Get-Date).AddSeconds(30)
    while ((Get-Date) -lt $deadline) {
        $a = @(Get-Process -Name 'Bannerlord' -ErrorAction SilentlyContinue | Where-Object { -not $_.HasExited })
        if ($a.Count -eq 0) { break }
        Start-Sleep -Seconds 2
    }
    # Only the PIDs this run launched may be stopped. Stopping every process that happens to
    # share the name would kill another group's session and destroy evidence this run does not own.
    $remain = @(Get-Process -Name 'Bannerlord' -ErrorAction SilentlyContinue |
        Where-Object { -not $_.HasExited -and ($script:OwnGamePids -contains $_.Id) })
    if ($remain.Count -gt 0) {
        Write-Host "WARN: own Bannerlord still alive, killing pid $(($remain | ForEach-Object { $_.Id }) -join ',')"
        $remain | Stop-Process -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 3
    }
    $remain = @(Get-Process -Name 'Bannerlord' -ErrorAction SilentlyContinue |
        Where-Object { -not $_.HasExited -and ($script:OwnGamePids -contains $_.Id) })
    if ($remain.Count -gt 0) { $script:CleanupFailures += "residual own Bannerlord process: $(($remain | ForEach-Object { $_.Id }) -join ',')" }

    $others = @(Get-Process -Name 'Bannerlord' -ErrorAction SilentlyContinue |
        Where-Object { -not $_.HasExited -and ($script:OwnGamePids -notcontains $_.Id) })
    if ($others.Count -gt 0) {
        Note-Others "Bannerlord processes not owned by this run were left alone: $(($others | ForEach-Object { $_.Id }) -join ',')"
    }

    # Ownership that could not be established is a cleanup failure, not something to hide: this run
    # launched a process but cannot say which PID it was, so it stopped none rather than risk ending
    # another group's session.
    if (-not $script:OwnershipResolved) {
        $script:UnownedNewPids = @(@($script:UnownedNewPids) + @($script:LaunchCandidatePids) | Select-Object -Unique)
    }
    if ($script:UnownedNewPids.Count -gt 0) {
        $script:CleanupFailures += "process ownership unresolved: $($script:UnownedNewPids -join ',') appeared during the launch call and could not be attributed to this run (basis=$script:OwnershipBasis); left running"
    } elseif (-not $script:OwnershipResolved -and $script:LaunchProcessStarted) {
        $script:CleanupFailures += "process ownership unresolved: no PID this run's own launch context names is still alive (basis=$script:OwnershipBasis); no PID was killed"
    }

    # The helper starts gabs before it probes the endpoint, so a failed handshake still leaves an
    # acquired process behind. $script:GabsProc is set by the dot-sourced helper, so it is the
    # reliable "was a helper acquired" flag; $McpOpened only records a working endpoint.
    try { if ($script:McpOpened -or $null -ne $script:GabsProc) { Close-McpHttp; Write-Host "mcp helper closed" } }
    catch { Write-Host "Close-McpHttp failed: $_"; $script:CleanupFailures += "Close-McpHttp: $_" }

    if (Test-Path $cb2Log) { Copy-Item $cb2Log (Join-Path $OutDir 'CombatBrain2.log') -Force }

    try { Report-NewCrashes } catch { Write-Host "crash report failed: $_"; $script:CleanupFailures += "crash report: $_" }

    if (Test-Path $cbathOut) {
        $dst = Join-Path $OutDir 'CBATH'
        New-Item -ItemType Directory -Force -Path $dst | Out-Null
        Copy-Item (Join-Path $cbathOut '*') $dst -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "saved CBATH outputs"
    }

    # Close the shared switches first, then read the end state back: the read-back must see what
    # the run actually leaves behind, so it cannot run before the restore.
    try { Restore-Sentinels } catch { Write-Host "sentinel restore failed: $_"; $script:CleanupFailures += "sentinel restore: $_" }

    # End-state read-back, recorded as evidence rather than assumed.
    try { Assert-CleanEndState } catch { Write-Host "end-state read-back failed: $_"; $script:CleanupFailures += "end-state read-back: $_" }

    foreach ($cf in $script:CleanupFailures) { Fail "cleanup: $cf" }
}

$reqRec = [ordered]@{
    scenario            = $Scenario
    own_army_size       = $ownArmySize
    own_composition     = $ownComp
    enemy_army_size     = $enemyArmySize
    enemy_composition   = $enemyComp
    p1_enabled          = 0
    p2_enabled          = 1
    p3_enabled          = 1
    probe_modes         = ''
    p2_probe_mode       = ''
    competition_inject  = 0
    opponent_control    = [int]($opponentControl)
    opponent_delay_sec  = $OpponentDelaySec
    combat_hold_seconds = $CombatHoldSec
    out_dir             = $OutDir
}
[System.IO.File]::WriteAllText((Join-Path $OutDir 'requested-config.json'), ($reqRec | ConvertTo-Json -Compress))
Write-Host "requested-config.json written"

Write-Host "=== summary ==="
Write-Host "scenario: $Scenario"
Write-Host "out_dir: $OutDir"
if ($script:Failures.Count -eq 0) {
    Write-Host "RESULT: OK"
    exit 0
}
Write-Host "RESULT: FAILURES ($($script:Failures.Count))"
$script:Failures | ForEach-Object { Write-Host "  - $_" }
exit 1
