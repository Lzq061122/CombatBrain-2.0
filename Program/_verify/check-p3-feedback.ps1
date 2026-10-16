<#
Offline checker for the bounded P3 feedback / loss-control / replanning dispatch.

It reads one raw session directory produced by run-p3.ps1 and recomputes the P3 numbers
independently:

  * E0 / F0 come from the CBATH engine evidence files (p2-evidence-*.json), not from the
    mod's own log. E0 is the observed enemy formation named by the freeze line; F0 is every
    formation of the mod's own team at that time.
  * DE / DF / routed / unknown are recounted from the CBATH terminal list
    (cbath-timeline.json), which the harness fills from the engine's own agent-removal
    callbacks. The mod's ACTION_TERMINAL lines are compared against it, not trusted.
  * LE / LF / rf / B / C / U are recomputed from those independent counts and compared with
    the values the mod printed.

CB2 log = what the mod decided. CBATH = what the battlefield did. Production scores are never
treated as ground truth.

Exit code 0 only means the checker ran. Each gate is reported separately as
pass / fail / unverified.

.PARAMETER SessionDir       Raw session directory to audit.
.PARAMETER ControlSessionDir  Optional second session arm used for gate 3. When given, the
                            two arms must differ only in the opponent counter-action.
.PARAMETER Tolerance        Absolute tolerance for recomputed-vs-logged ratios.
.PARAMETER OutFile          Optional report path. Defaults inside the session directory.
#>
param(
    [Parameter(Mandatory = $true)][string]$SessionDir,
    [string]$ControlSessionDir,
    [double]$Tolerance = 0.002,
    [string]$OutFile
)

$ErrorActionPreference = 'Stop'
$script:ChecksOk = $true
$script:Notes = New-Object System.Collections.Generic.List[string]
$script:Fails = New-Object System.Collections.Generic.List[string]

function Note([string]$m) { $script:Notes.Add($m) | Out-Null }
function Fail([string]$m) { $script:Fails.Add($m) | Out-Null; Write-Host "  FAIL: $m" }

# ---- parsing helpers -------------------------------------------------------------

function Read-Text([string]$path) {
    if (-not (Test-Path -LiteralPath $path)) { return $null }
    return [System.IO.File]::ReadAllText($path)
}

function Get-Field([string]$line, [string]$key) {
    $m = [regex]::Match($line, '(?:^|\s)' + [regex]::Escape($key) + '=(\S*)')
    if ($m.Success) { return $m.Groups[1].Value }
    return $null
}

function Num([string]$v) {
    if ([string]::IsNullOrEmpty($v) -or $v -eq '<none>' -or $v -eq 'NaN') { return [double]::NaN }
    $r = 0.0
    if ([double]::TryParse($v, [System.Globalization.NumberStyles]::Float,
            [System.Globalization.CultureInfo]::InvariantCulture, [ref]$r)) { return $r }
    return [double]::NaN
}

function Same([string]$a, [string]$b) { return [string]::Equals($a, $b, [System.StringComparison]::Ordinal) }

function Check([string]$label, [bool]$ok, [string]$detail) {
    if (-not $ok) { $script:ChecksOk = $false }
    if ($ok) { Write-Host ("  ok    " + $label + " " + $detail); return $true }
    Fail ($label + " " + $detail)
    return $false
}

# A gate verdict must be built from its own checks, never from a hand-picked subset: an
# individual check can fail while the gate still reports "pass" otherwise. Reset before a
# gate's checks, then AND $script:ChecksOk into that gate's verdict.
function Reset-Checks() { $script:ChecksOk = $true }

function Close([double]$a, [double]$b, [double]$tol) {
    if ([double]::IsNaN($a) -and [double]::IsNaN($b)) { return $true }
    if ([double]::IsNaN($a) -or [double]::IsNaN($b)) { return $false }
    return [math]::Abs($a - $b) -le $tol
}

function Clamp01([double]$v) {
    if ([double]::IsNaN($v) -or [double]::IsInfinity($v)) { return 0.0 }
    if ($v -lt 0.0) { return 0.0 }
    if ($v -gt 1.0) { return 1.0 }
    return $v
}

# Read a CB2 marker line into a key/value map (first occurrence of each key wins).
function Parse-Event([string]$line, [string]$marker) {
    $body = $line
    $idx = $line.IndexOf('] ')
    if ($idx -ge 0) { $body = $line.Substring($idx + 2) }
    $map = @{}
    foreach ($m in [regex]::Matches($body, '(?:^|\s)([A-Za-z_][A-Za-z0-9_]*)=(\S*)')) {
        $k = $m.Groups[1].Value
        if (-not $map.ContainsKey($k)) { $map[$k] = $m.Groups[2].Value }
    }
    $map['_marker'] = $marker
    $map['_line'] = $line
    return $map
}

function IdOf([string]$index, [string]$refHash) {
    return ($index + '@' + $refHash).ToLowerInvariant()
}

# ---- load session ----------------------------------------------------------------

$full = [System.IO.Path]::GetFullPath($SessionDir)
if (-not (Test-Path -LiteralPath $full)) { throw "session dir not found: $full" }
Write-Host "=== P3 offline check: $full ==="

$requested = Read-Text (Join-Path $full 'requested-config.json')
$cfg = if ($requested) { $requested | ConvertFrom-Json } else { $null }
$scenario = if ($cfg) { $cfg.scenario } else { '?' }
Write-Host "scenario=$scenario"

$logPath = Join-Path $full 'CombatBrain2.log'
$lines = @()
if (Test-Path -LiteralPath $logPath) { $lines = [System.IO.File]::ReadAllLines($logPath) }
Check 'cb2_log_present' ($lines.Count -gt 0) "path=$logPath lines=$($lines.Count)"

$timelinePath = Join-Path $full 'cbath-timeline.json'
$timeline = $null
if (Test-Path -LiteralPath $timelinePath) {
    $tl = (Read-Text $timelinePath) | ConvertFrom-Json
    if ($tl.result -and $tl.result.structuredContent) { $timeline = $tl.result.structuredContent }
    elseif ($tl.samples) { $timeline = $tl }
}
Check 'cbath_timeline_present' ($null -ne $timeline) "path=$timelinePath"

# ---- identity chain --------------------------------------------------------------

Write-Host ""
Write-Host "--- identity chain ---"
$deployed = Read-Text (Join-Path $full 'deployed-module.json')
$identity = Read-Text (Join-Path $full 'build-identity.json')
if ($deployed) {
    $d = $deployed | ConvertFrom-Json
    $props = $d.PSObject.Properties.Name
    $srcSha = if ($props -contains 'source_sha256') { $d.source_sha256 } else { '' }
    $depSha = if ($props -contains 'deployed_sha256') { $d.deployed_sha256 } else { '' }
    Note ("deployed_module sha=" + $depSha + " size=" + $d.size)
    if ($srcSha -and $depSha) {
        Check 'deploy_source_matches_deployed' (Same $srcSha $depSha) "source=$srcSha deployed=$depSha"
    }
}
$runtimeSha = $null
foreach ($l in $lines) {
    if ($l -match '\[RUNTIME_IDENTITY\]') {
        $runtimeSha = Get-Field $l 'assembly_file_sha256'
        Note ("runtime_identity build_id=" + (Get-Field $l 'build_id') + " sha=" + $runtimeSha)
        break
    }
}
if ($runtimeSha -and $deployed) {
    $d = $deployed | ConvertFrom-Json
    if ($d.PSObject.Properties.Name -contains 'deployed_sha256') {
        Check 'runtime_file_sha_matches_deployed' (Same $runtimeSha.ToLowerInvariant() $d.deployed_sha256.ToLowerInvariant()) "runtime=$runtimeSha"
    }
}
if ($identity) {
    $i = $identity | ConvertFrom-Json
    if ($i.result -and $i.result.structuredContent) { $i = $i.result.structuredContent }
    Note ("cbath_identity build_id=" + $i.build_id + " pid=" + $i.pid + " sha=" + $i.assembly_sha256)
}

# ---- collect events --------------------------------------------------------------

$frozen = @()
$evals = @()
$terminals = @()
$replans = @()
$invalidations = @()
$writes = @()
$closes = @()
$exitQueued = @()
$exitDone = @()
$exitNotApplied = @()
$exitSuperseded = @()
$handoffCleared = @()
$revoke = @()
$successors = @()
$withdrawEvents = @()
$taskCreated = @()
$behaviorDisarmed = @()
$selectionReject = @()
$evalTicks = @()
$reassignments = @()
$assignPublished = @()

foreach ($l in $lines) {
    if ($l.IndexOf('[CB2]', [System.StringComparison]::Ordinal) -lt 0) { continue }
    if ($l -match '\[ACTION_FEEDBACK_FROZEN\]') { $frozen += (Parse-Event $l 'ACTION_FEEDBACK_FROZEN'); continue }
    if ($l -match '\[ACTION_FEEDBACK\]') { $evals += (Parse-Event $l 'ACTION_FEEDBACK'); continue }
    if ($l -match '\[ACTION_TERMINAL\]') { $terminals += (Parse-Event $l 'ACTION_TERMINAL'); continue }
    if ($l -match '\[ACTION_REPLAN\]') { $replans += (Parse-Event $l 'ACTION_REPLAN'); continue }
    if ($l -match '\[PLAN_INVALIDATED\]') { $invalidations += (Parse-Event $l 'PLAN_INVALIDATED'); continue }
    if ($l -match '\[WRITE_EXECUTED\]') { $writes += (Parse-Event $l 'WRITE_EXECUTED'); continue }
    if ($l -match '\[TASK_CLOSED\]') { $closes += (Parse-Event $l 'TASK_CLOSED'); continue }
    if ($l -match '\[EXIT_STOP_QUEUED\]') { $exitQueued += (Parse-Event $l 'EXIT_STOP_QUEUED'); continue }
    if ($l -match '\[EXIT_STOP_DONE\]') { $exitDone += (Parse-Event $l 'EXIT_STOP_DONE'); continue }
    if ($l -match '\[EXIT_STOP_NOT_APPLIED\]') { $exitNotApplied += (Parse-Event $l 'EXIT_STOP_NOT_APPLIED'); continue }
    if ($l -match '\[EXIT_STOP_SUPERSEDED\]') { $exitSuperseded += (Parse-Event $l 'EXIT_STOP_SUPERSEDED'); continue }
    if ($l -match '\[HANDOFF_CLEARED\]') { $handoffCleared += (Parse-Event $l 'HANDOFF_CLEARED'); continue }
    if ($l -match '\[REVOKE_') { $revoke += (Parse-Event $l 'REVOKE'); continue }
    if ($l -match '\[SUCCESSOR_TASK\]') { $successors += (Parse-Event $l 'SUCCESSOR_TASK'); continue }
    if ($l -match '\[WITHDRAW_') { $withdrawEvents += (Parse-Event $l 'WITHDRAW'); continue }
    if ($l -match '\[TASK_CREATED\]') { $taskCreated += (Parse-Event $l 'TASK_CREATED'); continue }
    if ($l -match '\[BEHAVIOR_DISARMED\]') { $behaviorDisarmed += (Parse-Event $l 'BEHAVIOR_DISARMED'); continue }
    if ($l -match '\[REAL_SELECTION_NOT_SUBMITTED\]') { $selectionReject += (Parse-Event $l 'REAL_SELECTION_NOT_SUBMITTED'); continue }
    if ($l -match '\[EVAL_TICK\]') { $evalTicks += (Parse-Event $l 'EVAL_TICK'); continue }
    if ($l -match '\[COORDINATION_REASSIGNED\]') { $reassignments += (Parse-Event $l 'COORDINATION_REASSIGNED'); continue }
    if ($l -match '\[ASSIGNMENT_PUBLISHED\]') { $assignPublished += (Parse-Event $l 'ASSIGNMENT_PUBLISHED'); continue }
}

$gates = @{}
function Set-Gate([string]$name, [string]$verdict, [string]$detail) {
    $gates[$name] = [ordered]@{ verdict = $verdict; detail = $detail }
}
function Gate-Verdict([string]$name) {
    if ($gates.ContainsKey($name)) { return $gates[$name].verdict }
    return 'unverified'
}

# Scene fingerprint for gate 3, aligned to the arm's own freeze moment (t0).
# Source is the CBATH timeline rather than the mod log: it is stamped with mission time and is the
# only artefact that carries per-formation facing, which the contract names explicitly. The sample
# is chosen by nearest mission time to t0 - never by file-name order, which is what made the old
# implementation read an unrelated moment of the run.
# The contract asks for actual deployment, relative distance, facing and terrain relations, and
# says outright that equal coordinate strings are not the test. So the signature is
# rigid-invariant: map, army sizes, per-class composition, unit-weighted deployment centroids,
# the own-to-enemy relative distance, and the bearing of that vector in the own formation's frame.
# Terrain depth (height/slope) is not carried by any artefact and is recorded as absent, so the
# limit of this evidence stays visible instead of being read as a pass.
function Get-SceneFingerprint([string]$dir, [double]$t0) {
    $raw = Read-Text (Join-Path $dir 'cbath-timeline.json')
    if (-not $raw) { return $null }
    try { $o = $raw | ConvertFrom-Json } catch { return $null }
    if ($null -eq $o) { return $null }
    $text = $null
    try { $text = $o.result.content[0].text } catch { $text = $null }
    if (-not $text) { return $null }
    try { $tl = $text | ConvertFrom-Json } catch { return $null }
    if ($null -eq $tl -or $null -eq $tl.samples) { return $null }
    $best = $null
    $bestLabel = ''
    $bestDelta = [double]::MaxValue
    $i = 0
    foreach ($s in $tl.samples) {
        $i++
        $mt = Num ([string]$s.t)
        if ([double]::IsNaN($mt)) { continue }
        $delta = [math]::Abs($mt - $t0)
        if ($delta -lt $bestDelta) {
            $bestDelta = $delta; $best = $s
            $bestLabel = "sample[$i]@t=$($mt.ToString([System.Globalization.CultureInfo]::InvariantCulture))"
        }
    }
    if ($null -eq $best) { return $null }
    $fp = [ordered]@{ map = [string]$tl.mission_name; own_size = 0; enemy_size = 0
                      own_comp = ''; enemy_comp = ''
                      own_center = ''; enemy_center = ''; relative_distance = ''
                      own_facing = ''; bearing = ''; terrain = 'absent_depth_data'
                      sample = $bestLabel; delta = $bestDelta }
    $ocomp = @{}
    $ecomp = @{}
    $ownX = 0.0; $ownY = 0.0; $ownN = 0
    $foeX = 0.0; $foeY = 0.0; $foeN = 0
    $fxs = 0.0; $fys = 0.0; $fN = 0
    foreach ($t in $best.teams) {
        $isOwn = [bool]$t.is_player_team
        $units = 0
        foreach ($fm in $t.formations) {
            $cnt = [int]$fm.unit_count
            $units += $cnt
            $cls = [string]$fm.formation_class
            $px = [double]$fm.position.x
            $py = [double]$fm.position.y
            if ($isOwn) {
                $ocomp[$cls] = ([int]$ocomp[$cls]) + $cnt
                $ownX += $px * $cnt; $ownY += $py * $cnt; $ownN += $cnt
                $fxs += [double]$fm.facing.x * $cnt; $fys += [double]$fm.facing.y * $cnt; $fN += $cnt
            } else {
                $ecomp[$cls] = ([int]$ecomp[$cls]) + $cnt
                $foeX += $px * $cnt; $foeY += $py * $cnt; $foeN += $cnt
            }
        }
        if ($isOwn) { $fp['own_size'] = $units } else { $fp['enemy_size'] = $units }
    }
    $fp['own_comp'] = (($ocomp.GetEnumerator() | Sort-Object Name |
        ForEach-Object { "$($_.Key)=$($_.Value)" }) -join ',')
    $fp['enemy_comp'] = (($ecomp.GetEnumerator() | Sort-Object Name |
        ForEach-Object { "$($_.Key)=$($_.Value)" }) -join ',')
    if ($ownN -gt 0 -and $foeN -gt 0) {
        $ocx = $ownX / $ownN; $ocy = $ownY / $ownN
        $ecx = $foeX / $foeN; $ecy = $foeY / $foeN
        $inv = [System.Globalization.CultureInfo]::InvariantCulture
        $fp['own_center'] = "$([math]::Round($ocx, 1)),$([math]::Round($ocy, 1))"
        $fp['enemy_center'] = "$([math]::Round($ecx, 1)),$([math]::Round($ecy, 1))"
        $dxv = $ecx - $ocx; $dyv = $ecy - $ocy
        $fp['relative_distance'] = ([math]::Round([math]::Sqrt($dxv * $dxv + $dyv * $dyv), 1)).ToString($inv)
        if ($fN -gt 0) {
            $len = [math]::Sqrt($fxs * $fxs + $fys * $fys)
            if ($len -gt 0.0001) {
                $ux = $fxs / $len; $uy = $fys / $len
                $fp['own_facing'] = "$([math]::Round($ux, 3)),$([math]::Round($uy, 3))"
                $cross = $ux * $dyv - $uy * $dxv
                $dot = $ux * $dxv + $uy * $dyv
                $deg = [math]::Atan2($cross, $dot) * 180.0 / [math]::PI
                $fp['bearing'] = ([math]::Round($deg, 1)).ToString($inv)
            }
        }
    }
    return $fp
}

function Get-SampleCentroid($inner, [bool]$playerSide) {    $sx = 0.0; $sy = 0.0; $n = 0
    foreach ($t in $inner.teams) {
        if ([bool]$t.is_player_team -ne $playerSide) { continue }
        foreach ($fm in $t.formations) {
            if (-not $fm.active_center_known) { continue }
            $cnt = [int]$fm.active_member_count
            if ($cnt -le 0) { continue }
            $sx += [double]$fm.active_center.x * $cnt
            $sy += [double]$fm.active_center.y * $cnt
            $n += $cnt
        }
    }
    if ($n -le 0) { return @('', '') }
    return @(([math]::Round($sx / $n, 1)).ToString([System.Globalization.CultureInfo]::InvariantCulture),
             ([math]::Round($sy / $n, 1)).ToString([System.Globalization.CultureInfo]::InvariantCulture))
}

# Independent ~1Hz samples at or after a mission time, in sampling order. These come from the
# CBATH sampler rather than from the mod's own log, so they stay available after the arm
# converts and stops emitting feedback rows.
function Get-IndependentSamplesAfter([string]$dir, [double]$from) {
    $out = @()
    $files = @(Get-ChildItem -LiteralPath $dir -Filter 'p2-evidence-*.json' -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match '^p2-evidence-\d+\.json$' } | Sort-Object Name)
    foreach ($file in $files) {
        $raw = Read-Text $file.FullName
        if (-not $raw) { continue }
        try { $o = $raw | ConvertFrom-Json } catch { continue }
        if ($null -eq $o) { continue }
        $text = $null
        try { $text = $o.result.content[0].text } catch { $text = $null }
        if (-not $text) { continue }
        try { $inner = $text | ConvertFrom-Json } catch { continue }
        if ($null -eq $inner) { continue }
        $mt = [double]$inner.mission_time
        if ($mt -ge $from) { $out += @{ mission_time = $mt; name = $file.Name } }
    }
    return $out
}

# Independent per-formation order readback inside a mission-time window. The sampler reports
# movement_order / move_target straight from the engine, so it can contradict a stop the mod
# recorded. Sample formation ids carry a different class suffix than the mod log
# ("<hash>@Formation" vs "<hash>@<Class>"), so matching is on the hash part only.
function Get-IndependentOrderFacts([string]$dir, [double]$from, [double]$to) {
    $out = @()
    $files = @(Get-ChildItem -LiteralPath $dir -Filter 'p2-evidence-*.json' -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match '^p2-evidence-\d+\.json$' } | Sort-Object Name)
    foreach ($file in $files) {
        $raw = Read-Text $file.FullName
        if (-not $raw) { continue }
        try { $o = $raw | ConvertFrom-Json } catch { continue }
        if ($null -eq $o) { continue }
        $text = $null
        try { $text = $o.result.content[0].text } catch { $text = $null }
        if (-not $text) { continue }
        try { $inner = $text | ConvertFrom-Json } catch { continue }
        if ($null -eq $inner -or $null -eq $inner.teams) { continue }
        $mt = [double]$inner.mission_time
        if ($mt -lt $from -or $mt -gt $to) { continue }
        foreach ($team in $inner.teams) {
            if ($null -eq $team.formations) { continue }
            foreach ($fm in $team.formations) {
                $id = [string]$fm.formation_identity
                if ($id.Length -eq 0) { continue }
                $hash = $id
                $at = $id.IndexOf('@')
                if ($at -ge 0) { $hash = $id.Substring(0, $at) }
                $readable = $false
                $tx = [double]::NaN
                $ty = [double]::NaN
                if ($null -ne $fm.move_target) {
                    $readable = [bool]$fm.move_target.readable
                    if ($readable) { $tx = [double]$fm.move_target.x; $ty = [double]$fm.move_target.y }
                }
                $out += @{ mission_time = $mt; obj = $hash; order = [string]$fm.movement_order
                           readable = $readable; x = $tx; y = $ty }
            }
        }
    }
    return $out
}

# ---- freeze facts ----------------------------------------------------------------

Write-Host ""
Write-Host "--- freeze (t0) ---"
if ($frozen.Count -eq 0) {
    Fail 'no ACTION_FEEDBACK_FROZEN: P3 never armed, nothing to audit'
    Set-Gate 'gate1_positive' 'unverified' 'no freeze'
    Set-Gate 'gate2_negative' 'unverified' 'no freeze'
    Set-Gate 'gate3_control' 'unverified' 'no freeze'
} else {
    $f = $frozen[0]
    $t0 = Num $f['t0']
    $neLogged = [int](Num $f['NE'])
    $nfLogged = [int](Num $f['NF'])
    $observe = $f['observe']
    $mainKey = $f['main']
    $mainTeam = [int]($mainKey.Split('/')[0])
    $observeHex = $observe.Split('@')[0].ToLowerInvariant()
    Note ("freeze plan=" + $f['plan'] + " task=" + $f['task'] + " t0=" + $f['t0'] + " NE=" + $neLogged + " NF=" + $nfLogged + " observe=" + $observe)
    if ($frozen.Count -gt 1) { Note ("additional freezes: " + ($frozen.Count - 1)) }

    # independent baseline from engine evidence
    $evFiles = @(Get-ChildItem -LiteralPath $full -Filter 'p2-evidence-*.json' | Sort-Object Name)
    Check 'independent_baseline_samples_present' ($evFiles.Count -gt 0) "count=$($evFiles.Count)"

    $best = $null
    $bestDelta = [double]::MaxValue
    $evCache = @{}
    foreach ($file in $evFiles) {
        $txt = Read-Text $file.FullName
        if (-not $txt) { continue }
        $obj = $null
        try { $obj = $txt | ConvertFrom-Json } catch { continue }
        if ($obj.result -and $obj.result.structuredContent) { $obj = $obj.result.structuredContent }
        if (-not $obj.teams) { continue }
        $t = Num ([string]$obj.mission_time)
        if ([double]::IsNaN($t)) { continue }
        $evCache[$file.Name] = $obj
        $delta = [math]::Abs($t - $t0)
        if ($delta -lt $bestDelta) { $bestDelta = $delta; $best = $obj; $bestName = $file.Name }
    }
    Check 'independent_baseline_sample_found' ($null -ne $best) "nearest=$bestName delta=$([math]::Round($bestDelta,3))s"
    if ($bestDelta -gt 2.0) { Note "baseline sample is $([math]::Round($bestDelta,3))s from t0 (> 2s snapshot age)" }

    $e0 = @{}
    $f0 = @{}
    $neIndep = 0
    $nfIndep = 0
    $observeFound = $false
    foreach ($team in $best.teams) {
        $isMainTeam = ([int]$team.team_index -eq $mainTeam)
        foreach ($form in $team.formations) {
            $hex = ([string]$form.formation_identity).Split('@')[0].ToLowerInvariant()
            $isObserve = ($hex -eq $observeHex)
            $isInf = ([string]$form.formation_class).IndexOf('Infantry', [System.StringComparison]::OrdinalIgnoreCase) -ge 0
            if ($isObserve -and $isInf) { $observeFound = $true }
            foreach ($mem in $form.members) {
                if (-not $mem.counts_as_active_member) { continue }
                $id = IdOf ([string]$mem.index) ([string]$mem.ref_hash)
                if ($isObserve -and $isInf) { $e0[$id] = $true }
                if ($isMainTeam) { $f0[$id] = $true }
            }
        }
    }
    $neIndep = $e0.Count
    $nfIndep = $f0.Count
    Check 'observe_formation_found_in_engine_evidence' $observeFound "observe=$observe hex=$observeHex"
    Check 'NE_matches_engine_evidence' ($neIndep -eq $neLogged) "logged=$neLogged independent=$neIndep"
    Check 'NF_matches_engine_evidence' ($nfIndep -eq $nfLogged) "logged=$nfLogged independent=$nfIndep"
    Check 'E0_meets_minimum_20' ($neIndep -ge 20) "NE=$neIndep"

    # baseline on both sides of t0 (contract: valid independent samples either side of t0)
    $before = 0; $after = 0; $near = 0
    foreach ($k in $evCache.Keys) {
        $t = Num ([string]$evCache[$k].mission_time)
        if ($t -lt $t0) { $before++ } else { $after++ }
        if ([math]::Abs($t - $t0) -le 2.0) { $near++ }
    }
    Note "baseline samples before_t0=$before after_t0=$after within_2s=$near"
    Check 'baseline_samples_straddle_t0' ($before -gt 0 -and $after -gt 0) "before=$before after=$after"

    # ---- independent terminal counting from CBATH -------------------------------
    Write-Host ""
    Write-Host "--- independent terminal recount (CBATH) ---"
    $tlTerm = @()
    if ($timeline -and $timeline.terminals) { $tlTerm = $timeline.terminals }
    Note ("cbath terminal_count=" + $timeline.terminal_count + " overflow=" + $timeline.terminal_overflow + " listed=" + $tlTerm.Count)
    if ($timeline.terminal_overflow) { Fail 'cbath terminal overflow: terminal capture incomplete for this window' }

    # The tracker only owns one action: it stops attributing terminals the moment the frozen
    # baseline is reset (ACTION_FEEDBACK_RESET). Counts must be compared over the same window,
    # otherwise later deaths that the action never saw get charged to it.
    $resets = @()
    foreach ($l in $lines) { if ($l -match '\[ACTION_FEEDBACK_RESET\]') { $resets += (Parse-Event $l 'ACTION_FEEDBACK_RESET') } }
    $trackerEnd = [double]::PositiveInfinity
    foreach ($r in $resets) {
        $ra = Num $r['at']
        if (-not [double]::IsNaN($ra) -and $ra -lt $trackerEnd) { $trackerEnd = $ra }
    }
    if ($trackerEnd -lt [double]::MaxValue) {
        Note ("tracker window ends at ACTION_FEEDBACK_RESET at=" + $trackerEnd + "; terminals after that belong to later actions")
    } else {
        $trackerEnd = [double]::MaxValue
    }

    $deIndep = @{}     # enemy casualties, first time seen
    $dfIndep = @{}     # friendly casualties
    $routedIndep = @{}
    $unknownIndep = @{}
    $foreignTerm = 0
    foreach ($term in $tlTerm) {
        if ($term.is_mount) { continue }
        $at = Num ([string]$term.at)
        if ([double]::IsNaN($at) -or $at -lt $t0 -or $at -gt $trackerEnd) { continue }
        $id = IdOf ([string]$term.index) ([string]$term.ref_hash)
        $state = [string]$term.state
        if ($state -eq 'Killed' -or $state -eq 'Unconscious') {
            if ($e0.ContainsKey($id)) { if (-not $deIndep.ContainsKey($id)) { $deIndep[$id] = $at } }
            elseif ($f0.ContainsKey($id)) { if (-not $dfIndep.ContainsKey($id)) { $dfIndep[$id] = $at } }
            else { $foreignTerm++ }
        } elseif ($state -eq 'Routed') {
            if ($e0.ContainsKey($id) -or $f0.ContainsKey($id)) { if (-not $routedIndep.ContainsKey($id)) { $routedIndep[$id] = $at } }
        } else {
            if ($e0.ContainsKey($id) -or $f0.ContainsKey($id)) { if (-not $unknownIndep.ContainsKey($id)) { $unknownIndep[$id] = $at } }
        }
    }
    Note "independent casualties: DE=$($deIndep.Count) DF=$($dfIndep.Count) routed=$($routedIndep.Count) unknown=$($unknownIndep.Count) foreign=$foreignTerm"
    if ($foreignTerm -gt 0) { Note "$foreignTerm terminal events belong to members outside E0/F0 (not attributed)" }

    # cross-check against the mod's own ACTION_TERMINAL lines
    $modelTerm = @{}
    foreach ($t in $terminals) {
        $mem = $t['member']
        if (-not $mem) { continue }
        $at = Num $t['at']
        if ([double]::IsNaN($at) -or $at -lt $t0) { continue }
        $modelTerm[$mem.ToLowerInvariant()] = [string]$t['state']
    }
    $modelDe = 0; $modelDf = 0
    foreach ($k in $modelTerm.Keys) {
        $st = $modelTerm[$k]
        if ($st -ne 'Killed' -and $st -ne 'Unconscious') { continue }
        if ($e0.ContainsKey($k)) { $modelDe++ } elseif ($f0.ContainsKey($k)) { $modelDf++ }
    }
    Note "mod-reported ACTION_TERMINAL: DE=$modelDe DF=$modelDf (total lines=$($terminals.Count))"
    Check 'mod_and_engine_terminal_counts_agree' (($modelDe -eq $deIndep.Count) -and ($modelDf -eq $dfIndep.Count)) "mod DE=$modelDe DF=$modelDf engine DE=$($deIndep.Count) DF=$($dfIndep.Count)"

    # ---- decision replay on independent numbers ----------------------------------
    Write-Host ""
    Write-Host "--- per-evaluation recompute ---"
    $HEnv = 12.0
    $sideScale = 0.10
    $localScale = 0.20
    $maxLeErr = 0.0; $maxLfErr = 0.0; $maxBErr = 0.0; $maxCErr = 0.0; $maxUErr = 0.0
    $rows = 0; $numericMismatch = 0; $decisionMismatch = 0; $gate1Reinforce = 0
    $consecIndependent = 0; $stopSeen = $false; $stopDueAt = [double]::NaN
    $firstStopNow = [double]::NaN; $rewardMathRows = 0; $disengageMathRows = 0
    $unknownRows = 0; $completeRows = 0; $stopLoss = 0; $disengage = 0
    $sampleTimes = @($t0)
    $sampleDf = @{ $t0.ToString('F3') = 0 }

    foreach ($e in $evals) {
        $now = Num $e['now']
        if ([double]::IsNaN($now)) { continue }
        $rows++
        $de = 0; $dfc = 0
        foreach ($k in $deIndep.Keys) { if ($deIndep[$k] -le $now) { $de++ } }
        foreach ($k in $dfIndep.Keys) { if ($dfIndep[$k] -le $now) { $dfc++ } }
        $routed = 0
        foreach ($k in $routedIndep.Keys) { if ($routedIndep[$k] -le $now) { $routed++ } }
        $unknown = 0
        foreach ($k in $unknownIndep.Keys) { if ($unknownIndep[$k] -le $now) { $unknown++ } }

        $le = $de / $neIndep
        $lf = $dfc / $nfIndep
        # nearest valid evaluation sample at least 12s back, within this action
        $s = $null
        foreach ($st in $sampleTimes) { if (($now - $st) -ge $HEnv) { if ($null -eq $s -or $st -gt $s) { $s = $st } } }
        $rf = [double]::NaN
        $rateKnown = $false
        if ($null -ne $s) {
            $dfThen = 0
            foreach ($k in $dfIndep.Keys) { if ($dfIndep[$k] -le $s) { $dfThen++ } }
            $span = $now - $s
            if ($span -gt 0) { $rf = ($dfc - $dfThen) / ($nfIndep * $span); $rateKnown = $true }
        }
        $hrf = if ($rateKnown) { $HEnv * $rf } else { [double]::NaN }
        $b = Clamp01 ($le / $localScale)
        $costTerm = if ($rateKnown) { [math]::Max($lf, $hrf) } else { $lf }
        $c = Clamp01 ($costTerm / $sideScale)
        $u = 0.5 * $b + 0.5 * (1.0 - $c)

        $leL = Num $e['LE']; $lfL = Num $e['LF']; $bL = Num $e['B']; $cL = Num $e['C']; $uL = Num $e['U']
        $rfL = Num $e['rf']; $hrfL = Num $e['HRf']
        $maxLeErr = [math]::Max($maxLeErr, [math]::Abs($le - $leL))
        $maxLfErr = [math]::Max($maxLfErr, [math]::Abs($lf - $lfL))
        $maxBErr = [math]::Max($maxBErr, [math]::Abs($b - $bL))
        $maxCErr = [math]::Max($maxCErr, [math]::Abs($c - $cL))
        $maxUErr = [math]::Max($maxUErr, [math]::Abs($u - $uL))

        $numericOk = (Close $le $leL $Tolerance) -and (Close $lf $lfL $Tolerance) -and
                     (Close $b $bL $Tolerance) -and (Close $c $cL $Tolerance) -and (Close $u $uL $Tolerance)
        if ([int](Num $e['DE']) -ne $de -or [int](Num $e['DF']) -ne $dfc) { $numericOk = $false }
        if ((Same $e['complete'] '1')) { $completeRows++ }
        if ([int](Num $e['unknown']) -gt 0) { $unknownRows++ }
        if (-not $numericOk) { $numericMismatch++ }

        # Independent replay of the decision gates that the numbers alone determine.
        # The mod may additionally block a reward by the local-value and risk gates, so a
        # logged Continue while the reward maths holds is not a contradiction. A logged
        # decision the maths does NOT support is a contradiction, and so is delaying a
        # stop that the first qualifying point already required.
        $isStop = ($lf -ge 0.10) -or ($rateKnown -and $hrf -ge 0.10)
        $isDisengageNum = ($le -lt 0.05) -and ($lf -ge 0.05) -and ($u -lt 0.40) -and (($now - $t0) -ge 24.0)
        $isReinforceNum = (($now - $t0) -ge 12.0) -and ($le -ge 0.15) -and ($lf -le 0.05) -and
                          ($rateKnown -and $hrf -le 0.05) -and ($u -ge 0.60)

        # A StopLoss is due at the very first point whose own numbers cross the loss line.
        # Disengage and Reinforce are contract-gated on two consecutive qualifying points, so
        # their due moment is the second one; $consecIndependent still holds the previous row's
        # run length here (it is updated further down), which is exactly the contract's test.
        $disengageDue = $isDisengageNum -and ($consecIndependent -ge 1)
        $reinforceDue = $isReinforceNum -and ($consecIndependent -ge 1)

        $logged = $e['decision']
        if ($logged -eq 'StopLoss') { $stopLoss++ }
        if ($logged -eq 'Disengage') { $disengage++ }
        if ($logged -eq 'Reinforce') { $gate1Reinforce++ }

        if ($logged -eq 'StopLoss' -and -not $isStop) { $decisionMismatch++ }
        if ($logged -eq 'Disengage' -and -not (($isDisengageNum) -and ($consecIndependent -ge 1))) { $decisionMismatch++ }
        if ($logged -eq 'Reinforce' -and -not (($isReinforceNum) -and ($consecIndependent -ge 1))) { $decisionMismatch++ }

        if ($isStop -and -not $stopSeen) {
            $stopSeen = $true
            $stopDueAt = $now
            if ($logged -ne 'StopLoss') { Note ("loss stop was due at now=" + $now + " but the log decided " + $logged) }
        }
        # Record the independent verdict back onto the production row so later gates can
        # recompute the decision budget from the independent crossing, not from the
        # production row that merely happens to carry the decision.
        $e['_independentStopDue'] = $(if ($isStop) { '1' } else { '0' })
        $e['_independentDisengageDue'] = $(if ($disengageDue) { '1' } else { '0' })
        $e['_independentReinforceDue'] = $(if ($reinforceDue) { '1' } else { '0' })
        $e['_independentGateMath'] = $(if ($isReinforceNum -or $isDisengageNum -or $isStop) { '1' } else { '0' })
        if ($isStop -and $logged -eq 'StopLoss' -and [double]::IsNaN($firstStopNow)) { $firstStopNow = $now }
        if ($isReinforceNum) { $rewardMathRows++ }
        if ($isDisengageNum) { $disengageMathRows++ }

        if ($unknown -gt 0) { $consecIndependent = 0 }
        elseif ($isStop) { $consecIndependent = 1 }
        elseif ($isReinforceNum -or $isDisengageNum) { $consecIndependent++ }
        else { $consecIndependent = 0 }

        $sampleTimes += $now
        $sampleDf[$now.ToString('F3')] = $dfc
    }
    Note "evaluations=$rows complete=$completeRows unknown_rows=$unknownRows stoploss=$stopLoss disengage=$disengage reinforce=$gate1Reinforce"
    Note "reward_math_rows=$rewardMathRows disengage_math_rows=$disengageMathRows first_stop_due_at=$stopDueAt first_stop_logged_at=$firstStopNow"
    Note ("max recompute error: LE=" + [math]::Round($maxLeErr, 5) + " LF=" + [math]::Round($maxLfErr, 5) + " B=" + [math]::Round($maxBErr, 5) + " C=" + [math]::Round($maxCErr, 5) + " U=" + [math]::Round($maxUErr, 5))
    Check 'independent_recompute_matches_production' ($numericMismatch -eq 0) "mismatching evaluations=$numericMismatch of $rows (tolerance=$Tolerance)"
    Check 'logged_decisions_consistent_with_independent_numbers' ($decisionMismatch -eq 0) "inconsistent decisions=$decisionMismatch"
    if ($unknownRows -gt 0) { Note "$unknownRows evaluations carried unknown terminals; those windows cannot judge positive or negative per contract" }
    # Did this arm's own independent numbers ever cross a feedback gate? A matching decision is
    # only attributable to the counter-action when the counter-action arm itself reached the
    # gate; otherwise the comparison says nothing about the counter-action.
    $thisArmFeedbackCrossed = $stopSeen -or ($rewardMathRows -gt 0) -or ($disengageMathRows -gt 0)

    # ---- gate 1: positive feedback changes the following commitment --------------
    Write-Host ""
    Write-Host "--- gate 1: positive feedback ---"
    $posReplans = @($replans | Where-Object { $_['decision'] -eq 'Reinforce' })
    $negReplans = @($replans | Where-Object { $_['decision'] -eq 'StopLoss' -or $_['decision'] -eq 'Disengage' })
    if ($posReplans.Count -eq 0) {
        if ($gate1Reinforce -gt 0) {
            Set-Gate 'gate1_positive' 'fail' 'reinforce decision logged but no ACTION_REPLAN followed'
        } else {
            Set-Gate 'gate1_positive' 'unverified' 'no reinforce decision in this window'
        }
    } else {
        Reset-Checks
        $r = $posReplans[0]
        $newPlan = $r['new']
        $succ = @($successors | Where-Object { $_['new'] -eq $newPlan })
        $newWrites = @($writes | Where-Object { $_['plan'] -eq $newPlan })
        # A write under the successor plan is NOT by itself evidence of a new commitment:
        # an exit stop or a repeat of the old order also writes. Only a completed successor
        # with its own three-role commitment counts, so split exit writes out first.
        $newNormalWrites = @($newWrites | Where-Object { [string]$_['task'] -notmatch '-exit$' })
        $newExitWrites = @($newWrites | Where-Object { [string]$_['task'] -match '-exit$' })
        $succRoles = @($succ | ForEach-Object { [string]$_['role'] } | Where-Object { $_ -ne '' })
        $rolesCovered = @($succRoles | Select-Object -Unique)
        $hasMain = $rolesCovered -contains 'Main'
        $hasGuard = $rolesCovered -contains 'Guard'
        $hasSupport = $rolesCovered -contains 'Support'
        $succComplete = $hasMain -and $hasGuard -and $hasSupport
        Note ("reinforce replan new=" + $newPlan + " successor_tasks=" + $succ.Count + " roles=" + ($rolesCovered -join ',') + " normal_writes=" + $newNormalWrites.Count + " exit_writes=" + $newExitWrites.Count)
        Check 'gate1_successor_has_all_three_roles' $succComplete "main=$hasMain guard=$hasGuard support=$hasSupport roles=$(($rolesCovered) -join ',')"
        $ok = ($succ.Count -gt 0) -and ($newNormalWrites.Count -gt 0) -and $succComplete
        Check 'gate1_new_plan_has_tasks_and_real_writes' $ok "tasks=$($succ.Count) normal_writes=$($newNormalWrites.Count) roles_complete=$succComplete"

        # WRITE_EXECUTED carries no dedicated role= field: the role only appears inside the
        # geometry note ("move_to_role_point role=Main requested=(x,y) origin=(x,y) revision=n").
        # Parse it out, keeping only writes that really targeted a role point.
        $rolePointWrites = @()
        foreach ($w in $newNormalWrites) {
            $g = [string]$w['geometry']
            if ($g -notmatch '^move_to_role_point\s+role=(\w+)') { continue }
            $role = $matches[1]
            $req = ''
            if ($g -match 'requested=\(([^)]*)\)') { $req = $matches[1] }
            $org = ''
            if ($g -match 'origin=\(([^)]*)\)') { $org = $matches[1] }
            $rolePointWrites += [pscustomobject]@{ 'task' = [string]$w['task']; 'role' = $role; 'at' = (Num $w['at']); 'requested' = $req; 'origin' = $org }
        }
        $newMainWrites = @($rolePointWrites | Where-Object { $_['role'] -eq 'Main' })
        $newGuardWrites = @($rolePointWrites | Where-Object { $_['role'] -eq 'Guard' })
        $newSupportWrites = @($rolePointWrites | Where-Object { $_['role'] -eq 'Support' })

        # Compare the old plan's Main push the same way, through its own geometry note.
        $oldMainReqs = @()
        foreach ($w in $writes) {
            if ($w['plan'] -ne $r['old']) { continue }
            $g = [string]$w['geometry']
            if ($g -notmatch '^move_to_role_point\s+role=Main') { continue }
            if ($g -match 'requested=\(([^)]*)\)') { $oldMainReqs += $matches[1] }
        }
        $commitDetail = "old_main_requested=$(($oldMainReqs) -join ';') new_main_requested=$(($newMainWrites | ForEach-Object { $_['requested'] }) -join ';')"
        $distinct = $false
        if ($oldMainReqs.Count -gt 0 -and $newMainWrites.Count -gt 0) {
            $newReqs = @($newMainWrites | ForEach-Object { [string]$_['requested'] })
            $distinct = @($newReqs | Where-Object { $oldMainReqs -notcontains $_ }).Count -gt 0
        }
        Check 'gate1_commitment_actually_changed' $distinct $commitDetail

        # The new push must actually advance, not merely name a new point: compare the write's
        # target with the object's own position at that moment (both are in the geometry note).
        # A Main that only holds its ground is not a renewed commitment - that is exactly how
        # the old plan-2 arm failed (Main Hold, then cancelled).
        $mainAdvanced = $false
        $advanceDetail = ""
        foreach ($w in $newMainWrites) {
            $p = [string]$w['requested'] -split ','
            $q = [string]$w['origin'] -split ','
            if ($p.Count -ne 2 -or $q.Count -ne 2) { continue }
            $rx = 0.0; $ry = 0.0; $ox = 0.0; $oy = 0.0
            if (-not [double]::TryParse($p[0], [ref]$rx)) { continue }
            if (-not [double]::TryParse($p[1], [ref]$ry)) { continue }
            if (-not [double]::TryParse($q[0], [ref]$ox)) { continue }
            if (-not [double]::TryParse($q[1], [ref]$oy)) { continue }
            $moved = [math]::Sqrt(($rx - $ox) * ($rx - $ox) + ($ry - $oy) * ($ry - $oy))
            if ($moved -gt 0.5) {
                $mainAdvanced = $true
                $advanceDetail = "task=$($w['task']) origin=$($w['origin']) requested=$($w['requested']) moved=$([math]::Round($moved, 3))"
                break
            }
        }
        Check 'gate1_new_main_actually_advanced' $mainAdvanced "role_point_main_writes=$($newMainWrites.Count) proof=$advanceDetail"

        # Guard/Support must have their own real positional relationship to the new Main:
        # a successor that only re-issues the Main hold never places them.
        $guardPlaced = ($newGuardWrites.Count -gt 0)
        $supportPlaced = ($newSupportWrites.Count -gt 0)
        Check 'gate1_guard_and_support_placed' ($guardPlaced -and $supportPlaced) "guard_writes=$($newGuardWrites.Count) support_writes=$($newSupportWrites.Count)"

        Check 'gate1_reinforce_once_per_frozen_action' (($gate1Reinforce -eq 0) -or ($posReplans.Count -eq 1)) "reinforce_evals=$gate1Reinforce replans=$($posReplans.Count)"
        $verdict = if ($script:ChecksOk -and $ok -and $distinct -and $mainAdvanced -and $guardPlaced -and $supportPlaced) { 'pass' } else { 'fail' }
        Set-Gate 'gate1_positive' $verdict ("reinforce->" + $newPlan + " tasks=" + $succ.Count + " roles_complete=" + $succComplete + " normal_writes=" + $newNormalWrites.Count + " main_advanced=" + $mainAdvanced + " guard=" + $guardPlaced + " support=" + $supportPlaced)
    }

    # ---- gate 2: negative feedback stops the old push at the stated cadence ------
    Write-Host ""
    Write-Host "--- gate 2: negative feedback ---"
    $stopper = $null
    foreach ($r in $negReplans) { $stopper = $r; break }
    if ($null -eq $stopper) {
        # a loss stop that could not build a withdrawal still counts as termination only
        $stopEvals = @($evals | Where-Object { $_['decision'] -eq 'StopLoss' -or $_['decision'] -eq 'Disengage' })
        if ($stopEvals.Count -eq 0) {
            Set-Gate 'gate2_negative' 'unverified' 'no stop-loss or disengage decision in this window'
        } else {
            $oldInv = @($invalidations | Where-Object { $_['detail'] -like 'action_feedback*' })
            if ($oldInv.Count -gt 0) {
                Set-Gate 'gate2_negative' 'unverified' 'push stopped and plan invalidated, but no successor replan (termination only)'
            } else {
                Set-Gate 'gate2_negative' 'fail' 'stop decision logged without invalidating the old plan'
            }
        }
    } else {
      # This block runs at script scope, where a bare 'return' would end the whole run and skip
      # gate 3, the report and the GATES line. An inner scriptblock confines those exits here.
      & {
        Reset-Checks
        $oldPlan = $stopper['old']
        $newPlan = $stopper['new']
        Note ("stop replan old=" + $oldPlan + " new=" + $newPlan + " decision=" + $stopper['decision'] + " reason=" + $stopper['reason'])

        # The stop decision must be attributable to this arm's own old plan: an ACTION_REPLAN
        # for some other plan cannot stand in as this arm's stop evidence. Match the replan to
        # the invalidation of the old plan it claims to have abandoned.
        $inv = @($invalidations | Where-Object { [string]$_['plan'] -eq $oldPlan -and [string]$_['detail'] -like 'action_feedback*' })
        $invAt = if ($inv.Count -gt 0) { Num $inv[0]['at'] } else { [double]::NaN }
        if ($inv.Count -eq 0) {
            # The old plan can already have been closed by another route (condition lost, native
            # override) before the feedback decision was taken. BeginSuccessorPlan then refuses
            # to adopt a successor and logs invalidate=rejected, so no successor plan exists for
            # this stop. The old push did end, but this window cannot attest to the
            # feedback-owned stop path; keep 'fail' only when the old plan was left running.
            $otherInv = @($invalidations | Where-Object { [string]$_['plan'] -eq $oldPlan })
            if ($otherInv.Count -gt 0) {
                Set-Gate 'gate2_negative' 'unverified' 'push stopped and plan invalidated, but no successor replan (termination only)'
                return
            }
            Set-Gate 'gate2_negative' 'fail' ('replan for ' + $oldPlan + ' has no matching action_feedback invalidation of that same plan')
            return
        }
        Check 'gate2_replan_owns_old_plan_invalidation' ($inv.Count -gt 0) "old=$oldPlan invalidations_with_feedback_detail=$($inv.Count)"

        # Decision latency is recomputed from the independent loss/recompute evidence, not from
        # the production evaluation row that happens to sit next to the invalidation. The
        # independent rows (recomputed by this tool from the raw survival samples) establish
        # when the gate was actually reached; the production decision must follow within the
        # contract budget. Only the flag matching the decision that was taken may be used:
        # Disengage and Reinforce are two-point gates, StopLoss is a first-point gate.
        $dueFlag = if ($stopper['decision'] -eq 'StopLoss') { '_independentStopDue' }
                   elseif ($stopper['decision'] -eq 'Disengage') { '_independentDisengageDue' }
                   else { '_independentReinforceDue' }
        $crossAt = [double]::NaN
        foreach ($e in $evals) {
            if ([string]$e['plan'] -ne $oldPlan) { continue }
            if ([string]$e[$dueFlag] -ne '1') { continue }
            $t = Num $e['now']
            if ([double]::IsNaN($t)) { continue }
            $crossAt = $t
            break
        }
        $decLat = $invAt - $crossAt
        Check 'gate2_loss_to_decision_within_budget' ((-not [double]::IsNaN($crossAt)) -and ($decLat -le 3.0 + 1.0)) `
            "independent_crossing=$crossAt invalidated_at=$invAt latency=$([math]::Round($decLat,3))s (<=3s+tick) source=independent_recompute due=$dueFlag"

        # The stop decision must be a real exception in the production logs, not merely a
        # matching string: locate the production evaluation of the old plan that carries it.
        # Only the safety-first StopLoss path breaks the stability requirement; Disengage is a
        # contract-gated two-point decision and is not an exception, so requiring the exception
        # flag for it would reject a correct decision.
        $trig = $null
        foreach ($e in $evals) {
            if ([string]$e['plan'] -ne $oldPlan) { continue }
            if ($e['decision'] -ne $stopper['decision']) { continue }
            $trig = $e; break
        }
        if ($trig) {
            if ($stopper['decision'] -eq 'StopLoss') {
                Check 'gate2_loss_stop_records_exception' ($trig['exception'] -eq '1') "exception=$($trig['exception']) reason=$($trig['exception_reason']) at=$($trig['now'])"
            } else {
                Check 'gate2_loss_stop_records_exception' ($trig['exception'] -eq '1' -or [int](Num $trig['streak']) -ge 2) "decision=$($stopper['decision']) exception=$($trig['exception']) streak=$($trig['streak']) at=$($trig['now']) note=two_point_gate"
            }
        } else {
            Fail 'gate2 could not locate the triggering stop evaluation for the old plan'
        }
        # close / clear / revoke inside 1s of the decision. Only teardown events that belong
        # to the abandoned plan count; the successor plan is torn down much later and must not
        # be charged to this budget.
        $boundary = $invAt
        $lastAction = $boundary
        $teardown = @()
        foreach ($x in $closes) { if ([string]$x['plan'] -eq $oldPlan) { $teardown += $x } }
        foreach ($x in $handoffCleared) { if ([string]$x['plan'] -eq $oldPlan) { $teardown += $x } }
        foreach ($x in $revoke) { if ([string]$x['plan'] -eq $oldPlan -or [string]$x['task'] -like "$oldPlan*") { $teardown += $x } }
        foreach ($x in $exitQueued) { if ([string]$x['origin_plan'] -eq $oldPlan) { $teardown += $x } }
        foreach ($x in $teardown) {
            $a = Num $x['at']
            if (-not [double]::IsNaN($a)) { $lastAction = [math]::Max($lastAction, $a) }
        }
        $lat = $lastAction - $boundary
        Check 'gate2_close_clear_revoke_within_1s' ($lat -le 1.0) "invalidate_at=$boundary last_teardown=$lastAction latency=$([math]::Round($lat,3))s (<=1s) events=$($teardown.Count)"
        # The old push must actually stop, and the evidence has to be attributable per old
        # object: a write under the successor plan is not proof that *this* old object stopped.
        #
        # "Old assignment" means the objects the abandoned plan still owed at the moment it was
        # abandoned - not every object it ever touched. A task that an earlier role re-binding
        # already replaced (COORDINATION_REASSIGNED) or closed is no longer part of the current
        # revision, and demanding an exit-stop for it would charge the plan for a formation it
        # had already handed over; worse, stopping it would wrongly suppress an object that is
        # now acting on its own. So the required set is built from tasks that were still live
        # when the feedback fired, and each one must carry either an exit-stop that really
        # wrote, or an exit-stop that was honestly closed as not-applied.
        $replacedTasks = @{}
        foreach ($x in $reassignments) {
            $rt = [string]$x['old']
            if ($rt.Length -gt 0 -and -not $replacedTasks.ContainsKey($rt)) { $replacedTasks[$rt] = $true }
        }
        $oldAssignedObjs = @()
        $supersededAssigned = @()
        foreach ($c in $taskCreated) {
            if ([string]$c['plan'] -ne $oldPlan) { continue }
            $tid = [string]$c['task']
            if ($tid.Length -gt 0 -and $replacedTasks.ContainsKey($tid)) {
                $o = [string]$c['obj']
                if ($o.Length -gt 0 -and $supersededAssigned -notcontains $o) { $supersededAssigned += $o }
                continue
            }
            $o = [string]$c['obj']
            if ($o.Length -gt 0 -and $oldAssignedObjs -notcontains $o) { $oldAssignedObjs += $o }
        }
        if ($supersededAssigned.Count -gt 0) {
            Note ("old plan " + $oldPlan + " objects handed over by an earlier reassignment (not required to stop)=" + ($supersededAssigned -join ','))
        }
        # The required set is what the abandoned plan still owed when the feedback fired: its
        # authoritative assignment at the latest revision published on or before the decision.
        # TASK_CREATED alone cannot build that set - a role re-binding publishes an assignment for
        # an object that never got its own TASK_CREATED row under this plan, and leaving it out
        # lets a hand-over that skipped that object's exit read as a complete, timely stop.
        $assignedAt = @($assignPublished | Where-Object {
            [string]$_['plan'] -eq $oldPlan -and
            (-not [double]::IsNaN((Num $_['at']))) -and (Num $_['at']) -le $invAt })
        $oldAssignedObjs = @()
        if ($assignedAt.Count -gt 0) {
            $maxRev = -1
            foreach ($a in $assignedAt) {
                $r = Num $a['revision']
                if ((-not [double]::IsNaN($r)) -and ([int]$r -gt $maxRev)) { $maxRev = [int]$r }
            }
            foreach ($a in $assignedAt) {
                $r = Num $a['revision']
                if ([double]::IsNaN($r) -or ([int]$r -ne $maxRev)) { continue }
                $o = [string]$a['obj']
                if ($o.Length -gt 0 -and $oldAssignedObjs -notcontains $o) { $oldAssignedObjs += $o }
            }
            Note ("old plan " + $oldPlan + " required objects from ASSIGNMENT_PUBLISHED revision=" + $maxRev + " published_rows=" + $assignedAt.Count + " objs=" + ($oldAssignedObjs -join ','))
        } else {
            # Older sessions predate ASSIGNMENT_PUBLISHED rows? Keep the TASK_CREATED view then,
            # but say so: that view cannot see re-bound objects.
            foreach ($c in $taskCreated) {
                if ([string]$c['plan'] -ne $oldPlan) { continue }
                $tid = [string]$c['task']
                if ($tid.Length -gt 0 -and $replacedTasks.ContainsKey($tid)) { continue }
                $o = [string]$c['obj']
                if ($o.Length -gt 0 -and $oldAssignedObjs -notcontains $o) { $oldAssignedObjs += $o }
            }
            Note ("old plan " + $oldPlan + " has no ASSIGNMENT_PUBLISHED rows before the decision: required objects fall back to TASK_CREATED=" + ($oldAssignedObjs -join ','))
        }
        Note ("old plan " + $oldPlan + " assigned objects=" + $oldAssignedObjs.Count + " exit_stop queued=" + $exitQueued.Count + " done=" + $exitDone.Count + " not_applied=" + $exitNotApplied.Count + " superseded=" + $exitSuperseded.Count)
        $stoppedObjs = @(); $failedObjs = @(); $unaccountedObjs = @()
        foreach ($o in $oldAssignedObjs) {
            # The stop evidence must come from the exit this conversion queued for that object,
            # i.e. an exit task under the abandoned plan. A later exit under the successor plan
            # also ends up standing the object down, but it is the successor's own order, not
            # proof that this conversion stopped the old push in time - counting it would let a
            # hand-over that skipped the exit entirely still read as a timely stop.
            $done = @($exitDone | Where-Object { [string]$_['obj'] -eq $o -and [string]$_['plan'] -eq $oldPlan })
            $na = @($exitNotApplied | Where-Object { [string]$_['obj'] -eq $o -and [string]$_['plan'] -eq $oldPlan })
            if ($done.Count -gt 0) { $stoppedObjs += $o }
            elseif ($na.Count -gt 0) { $failedObjs += $o }
            else { $unaccountedObjs += $o }
        }
        Check 'gate2_every_old_object_accounted_for' ($unaccountedObjs.Count -eq 0) "assigned=$($oldAssignedObjs.Count) stopped=$($stoppedObjs.Count) stop_failed=$($failedObjs.Count) unaccounted=$($unaccountedObjs.Count)"
        # A per-object stop that was never applied is an honest failure, not a silent pass:
        # it must still be recorded, and the arm's verdict cannot claim the push stopped.
        Check 'gate2_old_push_actually_terminated' (($stoppedObjs.Count -gt 0) -and ($failedObjs.Count -eq 0)) `
            "stopped=$($stoppedObjs.Count) stop_failed=$($failedObjs.Count) of assigned=$($oldAssignedObjs.Count)"

        # Ordering is a separate conclusion from outcome, and the contract turns on it: a
        # successor may not adopt or publish before every required object's exit has been
        # confirmed. An exit-stop that the *new* assignment took over (EXIT_STOP_SUPERSEDED)
        # is therefore hand-over-before-confirmation, which is exactly the defect this stage
        # repairs - the object stopping later, under the successor's own order, does not
        # retroactively make the hand-over legal, and a Stop issued when the successor itself
        # is later cancelled cannot be counted back as a timely stop either.
        #
        # What is checked is only this conversion's hand-over. Refusing a *late* old exit once
        # a new identity legitimately owns the object stays necessary protection, so
        # EXIT_STOP_SUPERSEDED is not forbidden globally - only where it displaced an exit that
        # this conversion still owed.
        $prematureObjs = @()
        foreach ($o in $oldAssignedObjs) {
            $sup = @($exitSuperseded | Where-Object {
                if ([string]$_['obj'] -ne $o) { return $false }
                $a = Num $_['at']
                # Only supersessions belonging to this conversion window count: they sit between
                # the decision and the point where a legal hand-over could have completed.
                return (-not [double]::IsNaN($a)) -and ($a -ge $invAt) -and ($a -le ($invAt + 1.0))
            })
            if ($sup.Count -gt 0) { $prematureObjs += $o }
        }
        Check 'gate2_exit_not_superseded_before_confirmation' ($prematureObjs.Count -eq 0) `
            ("required=$($oldAssignedObjs.Count) taken_over_before_confirmation=$($prematureObjs.Count) objects=" + $(if ($prematureObjs.Count -eq 0) { '<none>' } else { $prematureObjs -join ',' }))
        Note ("exit outcomes in this conversion: done=" + @($exitDone | Where-Object { $oldAssignedObjs -contains [string]$_['obj'] }).Count + " not_applied=" + @($exitNotApplied | Where-Object { $oldAssignedObjs -contains [string]$_['obj'] }).Count + " superseded=" + @($exitSuperseded | Where-Object { $oldAssignedObjs -contains [string]$_['obj'] }).Count)

        # After the successor starts, no old object may be pushed further by a role-point write:
        # check by object identity, not by formation key alone (a key can be re-used by the new
        # plan). The exit stop that THIS conversion queued is a Hold on the old plan by design -
        # it is the stop being executed, not renewed progress, so it is excluded by task id.
        $advanceAfter = @()
        foreach ($w in $writes) {
            $a = Num $w['at']
            if ([double]::IsNaN($a) -or $a -le $invAt) { continue }
            if ([string]$w['task'] -match '-exit$') { continue }
            if ([string]$w['geometry'] -notlike 'move_to_role_point*') { continue }
            if ([string]$w['plan'] -eq $oldPlan) { $advanceAfter += $w; continue }
            if ($oldAssignedObjs -contains [string]$w['obj']) { $advanceAfter += $w }
        }
        Check 'gate2_no_further_old_role_advance_writes' ($advanceAfter.Count -eq 0) "old_objects=$($oldAssignedObjs.Count) offending_writes_after_stop=$($advanceAfter.Count)"

        # The mod's own "stop written" row is not proof the object actually stopped - the engine's
        # order readback is, and only the independent sampler carries it. An object that the mod
        # recorded as stopped must not still be executing the abandoned plan's own role-point
        # advance. Compared against the old plan's requested targets and restricted to the window
        # before a successor could legitimately publish (the <=1s exit budget): past that point
        # the successor's own orders are in force and a move to the same coordinates would no
        # longer be attributable to the old action.
        $oldRolePoints = @{}
        foreach ($w in $writes) {
            if ([string]$w['plan'] -ne $oldPlan) { continue }
            if ([string]$w['geometry'] -notlike 'move_to_role_point*') { continue }
            $o = [string]$w['obj']
            if ($o.Length -eq 0) { continue }
            $req = [regex]::Match([string]$w['requested'], '^\(([-0-9.]+),([-0-9.]+)\)$')
            if (-not $req.Success) { continue }
            $oldRolePoints[$o] = @([double]$req.Groups[1].Value, [double]$req.Groups[2].Value)
        }
        #
        # Two distinct questions live here and must not be collapsed into one:
        #   (a) is there a contradictory reading - the object still executing the abandoned
        #       role-point advance? -> that is a violation;
        #   (b) is there any independent, explainable reading at all for that object after its
        #       own stop was written? -> absence of evidence is "unverified", never "pass".
        # Folding (b) into (a) is what let a tampered run with every post-decision order field
        # removed still read as a timely stop: with no readable target the contradiction set came
        # out empty and an empty set was scored as success.
        #
        # The boundary is the object's OWN exit write, not the decision moment. The contract gives
        # the stop a limited budget: between the decision and that write the old plan is still
        # being wound down, so a reading there is a legitimate intermediate state, not a breach.
        # Charging the whole window from the decision effectively squeezed the budget to zero.
        # Once the stop HAS been written, however, any later reading of the old target is a breach,
        # so the window runs to the end of the run rather than to a fixed 1s.
        $stopAt = @{}
        foreach ($d in $exitDone) {
            if ([string]$d['plan'] -ne $oldPlan) { continue }
            $o = [string]$d['obj']
            $a = Num $d['at']
            if ($o.Length -eq 0 -or [double]::IsNaN($a)) { continue }
            if ((-not $stopAt.ContainsKey($o)) -or ($a -lt $stopAt[$o])) { $stopAt[$o] = $a }
        }
        $stillAdvancing = @()
        $unexplainedObjs = @()
        $pushedObjs = @($oldAssignedObjs | Where-Object { $oldRolePoints.ContainsKey($_) })
        if ($pushedObjs.Count -gt 0) {
            $facts = @(Get-IndependentOrderFacts $full $invAt ([double]::MaxValue))
            foreach ($o in $pushedObjs) {
                $pt = $oldRolePoints[$o]
                $hash = $o.Split('@')[0]
                $stop = if ($stopAt.ContainsKey($o)) { $stopAt[$o] } else { $invAt }
                $explained = 0
                $contradicted = $false
                foreach ($fct in $facts) {
                    if ([string]$fct.obj -ne $hash) { continue }
                    if ([double]$fct.mission_time -le $stop) { continue }
                    if ($fct.readable -and (-not [double]::IsNaN($fct.x)) -and (-not [double]::IsNaN($fct.y))) {
                        if ((Close $fct.x $pt[0] 0.5) -and (Close $fct.y $pt[1] 0.5)) {
                            $stillAdvancing += ($o + "@" + $fct.mission_time)
                            $contradicted = $true
                            break
                        }
                        $explained++
                    } elseif (([string]$fct.order).Length -gt 0 -and ([string]$fct.order -notmatch '^(Move|Advance)')) {
                        # A standing order that cannot be the abandoned advance: it explains the
                        # object without a target coordinate, which is how a real stop reads.
                        $explained++
                    }
                }
                if ((-not $contradicted) -and $explained -eq 0) { $unexplainedObjs += $o }
            }
        } else {
            Note "gate2 independent order readback: old plan has no move_to_role_point writes, nothing to contradict"
        }
        Check 'gate2_stopped_objects_not_still_executing_old_order' ($stillAdvancing.Count -eq 0) `
            ("required=$($oldAssignedObjs.Count) old_role_points=$($oldRolePoints.Count) still_on_old_order=" + $(if ($stillAdvancing.Count -eq 0) { '<none>' } else { $stillAdvancing -join ',' }))
        # Not a Check(): missing readings must not fail the arm, but they must also not be
        # silently spent as agreement. The verdict below turns this into "unverified".
        $evidenceOk = ($unexplainedObjs.Count -eq 0)
        if (-not $evidenceOk) {
            Note ("gate2 independent stop evidence missing for required objects (no explainable order reading after their own stop write): " + ($unexplainedObjs -join ',') + " - unverified, not agreement")
        } else {
            Note ("gate2 independent stop evidence present for pushed_objects=" + $pushedObjs.Count + " unexplained=0")
        }

        # The 8s post-conversion window must be observed at real evaluation points, not merely
        # counted off cadence ticks. The independent ~1Hz sampler is the evidence source here:
        # after the arm converts, the production feedback rows stop by design (the successor is
        # no longer an evaluated offensive plan), so production rows alone can never fill this
        # window - only the independent samples can, and the contract asks for the independent
        # ones anyway. Fewer than two samples means the window is unproven, not failed.
        $afterIndep = @(Get-IndependentSamplesAfter $full ($invAt + 8.0))
        $afterEvals = @($evals | Where-Object { (Num $_['now']) -ge ($invAt + 8.0) })
        $afterTicks = @($evalTicks | Where-Object { (Num $_['at']) -ge ($invAt + 8.0) })
        $stableOk = ($afterIndep.Count -ge 2)
        if (-not $stableOk) {
            # Missing evidence is "unverified", never "fail" - but only when nothing else has
            # already been proven wrong. A recorded violation outranks an unproven window.
            Note ("gate2_post_conversion_8s_two_points evidence missing: independent_samples_after_8s=" + $afterIndep.Count + " production_feedback_rows_after_8s=" + $afterEvals.Count + " cadence_ticks_after_8s=" + $afterTicks.Count + " - window unproven, not judged")
            $missingVerdict = if ($script:ChecksOk) { 'unverified' } else { 'fail' }
            Set-Gate 'gate2_negative' $missingVerdict ("old=" + $oldPlan + " new=" + $newPlan + " decision=" + $stopper['decision'] + " stopped=" + $stoppedObjs.Count + "/" + $oldAssignedObjs.Count + $(if ($script:ChecksOk) { " reason=fewer_than_two_independent_samples_in_8s_window" } else { " reason=check_failed_with_unproven_8s_window" }))
            return
        }
        $latestIndep = [double]($afterIndep[-1]['mission_time'])
        Check 'gate2_post_conversion_8s_two_points' $stableOk "independent_samples_after_8s=$($afterIndep.Count) latest=$latestIndep production_feedback_rows_after_8s=$($afterEvals.Count) cadence_ticks_after_8s=$($afterTicks.Count)"
        $revived = @($writes | Where-Object { $_['plan'] -eq $oldPlan -and (Num $_['at']) -gt ($invAt + 8.0) })
        Check 'gate2_old_plan_identity_not_revived' ($revived.Count -eq 0) "old_plan_writes_after_8s=$($revived.Count)"
        $withdrawArrived = @($withdrawEvents | Where-Object { $_['_marker'] -eq 'WITHDRAW_ARRIVED' })
        $withdrawHold = @($withdrawEvents | Where-Object { $_['_marker'] -eq 'WITHDRAW_HOLD_CREATED' })
        $withdrawFail = @($withdrawEvents | Where-Object { $_['_marker'] -eq 'WITHDRAW_COMMAND_FAILED' -or $_['_marker'] -eq 'WITHDRAW_DEADLINE' })
        Note ("withdraw: arrived=" + $withdrawArrived.Count + " hold_created=" + $withdrawHold.Count + " failed_or_deadline=" + $withdrawFail.Count)
        $retreatProven = ($stoppedObjs.Count -gt 0) -and ($failedObjs.Count -eq 0)
        # "No contradiction found" is not the same as "a stop was observed". When the checks pass
        # but some required object has no independent reading to explain it, the gate is unverified
        # rather than passed - the evidence for that object simply is not there.
        $v = if (-not $script:ChecksOk) { 'fail' }
             elseif ($retreatProven -and $stableOk -and $evidenceOk -and $revived.Count -eq 0 -and $unaccountedObjs.Count -eq 0 -and $prematureObjs.Count -eq 0) { 'pass' }
             else { 'unverified' }
        Set-Gate 'gate2_negative' $v ("old=" + $oldPlan + " new=" + $newPlan + " decision=" + $stopper['decision'] + " objects_stopped=" + $stoppedObjs.Count + "/" + $oldAssignedObjs.Count + " stop_failed=" + $failedObjs.Count + " unaccounted=" + $unaccountedObjs.Count + " taken_over_before_confirmation=" + $prematureObjs.Count + " independent_stop_evidence=" + $(if ($evidenceOk) { 'present' } else { 'incomplete:' + $unexplainedObjs.Count }))
      }
    }

    # ---- gate 3: control arm differs only in the key feedback --------------------
    Write-Host ""
    Write-Host "--- gate 3: control ---"
    if (-not $ControlSessionDir) {
        Set-Gate 'gate3_control' 'unverified' 'no control session supplied'
    } else {
        $control = [System.IO.Path]::GetFullPath($ControlSessionDir)
        if (-not (Test-Path -LiteralPath $control)) {
            Set-Gate 'gate3_control' 'fail' "control session missing: $control"
        } else {
            Reset-Checks
            $cReq = Read-Text (Join-Path $control 'requested-config.json')
            $ccfg = if ($cReq) { $cReq | ConvertFrom-Json } else { $null }
            $cLog = Read-Text (Join-Path $control 'CombatBrain2.log')
            $cFreeze = $false; $cReplan = $false; $cStop = $false
            $cFrozenCount = 0
            $cT0 = [double]::NaN
            if ($cLog) {
                foreach ($l in $cLog.Split("`n")) {
                    if ($l -match '\[ACTION_FEEDBACK_FROZEN\]') {
                        $cFrozenCount++
                        if ([double]::IsNaN($cT0)) {
                            $m = [regex]::Match($l, 't0=([-0-9.]+)')
                            if ($m.Success) { $cT0 = Num $m.Groups[1].Value }
                        }
                    }
                    if ($l -match '\[ACTION_REPLAN\]') { $cReplan = $true }
                    if ($l -match '\[ACTION_FEEDBACK\].*decision=(StopLoss|Disengage|Reinforce)') { $cStop = $true }
                }
            }
            # Interface-only comparison is not enough: the contract asks the two arms to be
            # compared on their source/behaviour artefacts and on the actual scene (deployment,
            # relative distances, terrain relations), not just on requested config fields. If
            # the scene itself cannot be shown equivalent, gate 3 is unverified, not passed.
            $sceneOk = $true
            $sceneDetail = @()
            # A single arm difference is the point of this comparison; everything else must
            # match. opponent_control is compared separately below (it IS the difference), and
            # scenario is excluded because it is the label of the arm, not a run condition.
            $armOk = $true
            $armFields = @('own_army_size', 'own_composition', 'enemy_army_size', 'enemy_composition', 'p1_enabled', 'p2_enabled', 'p3_enabled', 'competition_inject', 'combat_hold_seconds', 'opponent_delay_sec')
            if ($cfg -and $ccfg) {
                foreach ($k in $armFields) {
                    $a = $cfg.$k; $b = $ccfg.$k
                    if ("$a" -ne "$b") { $armOk = $false; Note "gate3 arm difference: $k this=$a control=$b" }
                }
                $oppThis = [int]$cfg.opponent_control
                $oppCtrl = [int]$ccfg.opponent_control
                Check 'gate3_arms_differ_only_in_counter_action' ($armOk -and ($oppThis -ne $oppCtrl)) "counter_action this=$oppThis control=$oppCtrl other_fields_match=$armOk"

                # Scene equivalence: the two arms must actually have run the same map with the same
                # initial deployment, proven from the run-time evidence rather than the requested
                # config. Each arm is sampled at its own freeze moment (t0).
                # Discrete facts (map, army sizes, per-class composition) must match exactly, and a
                # mismatch there is a real fail. Geometry is handled as the contract demands: an
                # exact match proves equivalence on that axis, while a difference does NOT disprove
                # it - so a differing deployment or bearing leaves the comparison unproven rather
                # than failing it, and no tolerance is invented to force either verdict.
                $thisScene = Get-SceneFingerprint $full $t0
                $ctrlScene = Get-SceneFingerprint $control $cT0
                $sceneGeomOk = $false
                if (-not $thisScene -or -not $ctrlScene) {
                    $sceneOk = $false
                    $sceneDetail += "scene_fingerprint_unavailable this=$([bool]$thisScene) control=$([bool]$ctrlScene)"
                } else {
                    foreach ($k in 'map', 'own_size', 'enemy_size', 'own_comp', 'enemy_comp') {
                        if ("$($thisScene[$k])" -ne "$($ctrlScene[$k])") {
                            $sceneOk = $false
                            $sceneDetail += ("$k this=$($thisScene[$k]) control=$($ctrlScene[$k])")
                        }
                    }
                    $geomDiff = @()
                    foreach ($k in 'own_center', 'enemy_center', 'relative_distance', 'own_facing', 'bearing') {
                        if ("$($thisScene[$k])" -ne "$($ctrlScene[$k])") {
                            $geomDiff += ("$k this=$($thisScene[$k]) control=$($ctrlScene[$k])")
                        }
                    }
                    $sceneGeomOk = ($geomDiff.Count -eq 0)
                    Note ("gate3 scene this: map=$($thisScene['map']) sizes=$($thisScene['own_size'])/$($thisScene['enemy_size']) " +
                        "comp=$($thisScene['own_comp'])|$($thisScene['enemy_comp']) center=$($thisScene['own_center'])->$($thisScene['enemy_center']) " +
                        "rel=$($thisScene['relative_distance']) facing=$($thisScene['own_facing']) bearing=$($thisScene['bearing']) " +
                        "$($thisScene['sample'])@delta=$([math]::Round($thisScene['delta'], 3))s terrain=$($thisScene['terrain'])")
                    Note ("gate3 control scene this: map=$($ctrlScene['map']) sizes=$($ctrlScene['own_size'])/$($ctrlScene['enemy_size']) " +
                        "comp=$($ctrlScene['own_comp'])|$($ctrlScene['enemy_comp']) center=$($ctrlScene['own_center'])->$($ctrlScene['enemy_center']) " +
                        "rel=$($ctrlScene['relative_distance']) facing=$($ctrlScene['own_facing']) bearing=$($ctrlScene['bearing']) " +
                        "$($ctrlScene['sample'])@delta=$([math]::Round($ctrlScene['delta'], 3))s terrain=$($ctrlScene['terrain'])")
                    if (-not $sceneGeomOk) {
                        Note ("gate3 scene geometry not equal, equivalence unproven (a differing position does not by itself disprove it): " +
                            ($geomDiff -join '; '))
                    }
                    if ($thisScene['terrain'] -eq 'absent_depth_data') {
                        Note "gate3 terrain relations: no artefact carries height/slope, so terrain equivalence is unproven rather than assumed"
                    }
                }
                # A discrete mismatch is a real contradiction inside the compared fields, so it fails.
                # Geometry is different: unequal deployment does not disprove equivalence, and equal
                # deployment does not prove it either, so it only decides whether equivalence is
                # proven. Unproven keeps the gate unverified instead of inventing a tolerance.
                Check 'gate3_scene_discrete_match' $sceneOk ("map/army/composition compared: " + $(if ($sceneDetail.Count -eq 0) { 'identical' } else { $sceneDetail -join '; ' }))
            } else {
                Fail 'gate3 could not read requested-config.json for both arms'
                $sceneOk = $false
            }

            # The arm with the counter-action must produce the decision and the other must not.
            # But "the control did nothing" only counts when the control's own feedback maths
            # also stayed below the gate - otherwise the difference is unexplained and the
            # comparison proves nothing about the counter-action.
            $thisArmDecided = ($posReplans.Count + $negReplans.Count) -gt 0
            $controlArmDecided = $cReplan -or $cStop
            Note "decisions: with_counter_action=$thisArmDecided without=$controlArmDecided freezes_control=$cFrozenCount"
            if ($controlArmDecided) {
                Check 'gate3_control_arm_has_no_matching_decision' $false "control replan=$cReplan decision=$cStop freezes=$cFrozenCount"
                $v = 'fail'
            } elseif (-not $sceneOk) {
                Check 'gate3_control_arm_has_no_matching_decision' $true "control replan=$cReplan decision=$cStop freezes=$cFrozenCount"
                $v = 'fail'
            } elseif (-not $sceneGeomOk) {
                # Scene equivalence is not proven, so the arms cannot be confirmed comparable.
                # That is missing evidence, not a contradiction: unverified.
                Check 'gate3_control_arm_has_no_matching_decision' $true "control replan=$cReplan decision=$cStop freezes=$cFrozenCount"
                $v = 'unverified'
            } elseif (-not $thisArmDecided) {
                Check 'gate3_control_arm_has_no_matching_decision' $true "control replan=$cReplan decision=$cStop freezes=$cFrozenCount"
                $v = 'fail'
            } elseif (-not $thisArmFeedbackCrossed) {
                # the counter-action arm never crossed its own gate either, so a matching
                # decision cannot be attributed to the counter-action
                Check 'gate3_control_arm_has_no_matching_decision' $true "control replan=$cReplan decision=$cStop"
                $v = 'unverified'
            } else {
                Check 'gate3_control_arm_has_no_matching_decision' $true "control replan=$cReplan decision=$cStop freezes=$cFrozenCount"
                $v = if ($script:ChecksOk -and $armOk -and $sceneOk -and $sceneGeomOk) { 'pass' } else { 'fail' }
            }
            Set-Gate 'gate3_control' $v ("with_counter_action=" + $thisArmDecided + " control=" + $controlArmDecided + " fields_match=" + $armOk + " scene_discrete=" + $sceneOk + " scene_geometry=" + $sceneGeomOk)
        }
    }
}

# ---- report ----------------------------------------------------------------------

Write-Host ""
Write-Host "=== gates ==="
foreach ($g in $gates.Keys) { Write-Host ("  " + $g + ": " + $gates[$g].verdict + " -- " + $gates[$g].detail) }
if ($script:Notes.Count -gt 0) {
    Write-Host ""
    Write-Host "=== notes ==="
    foreach ($n in $script:Notes) { Write-Host ("  " + $n) }
}
Write-Host ""
if ($script:Fails.Count -eq 0) { Write-Host 'CHECK RESULT: no failures' } else { Write-Host ('CHECK RESULT: ' + $script:Fails.Count + ' failure(s)'); foreach ($x in $script:Fails) { Write-Host ('  - ' + $x) } }
$verdicts = ($gates.Keys | ForEach-Object { "$_=$($gates[$_].verdict)" }) -join ' '
Write-Host ("GATES: " + $verdicts)

if (-not $OutFile) { $OutFile = Join-Path $full 'p3-offline-check.json' }
$report = [ordered]@{
    session              = $full
    scenario             = $scenario
    generated_utc        = (Get-Date).ToUniversalTime().ToString('o')
    tolerance            = $Tolerance
    gates                = $gates
    failures             = @($script:Fails)
    notes                = @($script:Notes)
    counts               = [ordered]@{
        evaluations        = $evals.Count
        freezes            = $frozen.Count
        mod_terminals      = $terminals.Count
        replans            = $replans.Count
        invalidations      = $invalidations.Count
        writes             = $writes.Count
        exit_stop_queued   = $exitQueued.Count
        exit_stop_done     = $exitDone.Count
        exit_stop_not_applied = $exitNotApplied.Count
        successor_tasks    = $successors.Count
        withdraw_events    = $withdrawEvents.Count
    }
}
[System.IO.File]::WriteAllText($OutFile, ($report | ConvertTo-Json -Depth 8), (New-Object System.Text.UTF8Encoding($false)))
Write-Host ("report: " + $OutFile)
exit 0
