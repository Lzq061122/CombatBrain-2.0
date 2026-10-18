param(
    [Parameter(Mandatory = $true)][string]$SessionDir,
    [double]$BudgetSeconds = 20.0,
    [double]$ArrivalRadius = 5.0
)

# Read-only counterfactual: would swapping Guard/Support have arrived?
#
# Window semantics: the relationship budget runs [WrittenAt, WrittenAt + 20s] per
# task. For a swapped assignment the command was never actually issued, so this is
# a COUNTERFACTUAL ESTIMATE: same formation object, its observed in-window speed,
# its actual position at the real write time. It can rank assignments; it cannot
# prove that a swapped command would have been written or obeyed.
#
# Speed uses only samples where that formation really held a Move order inside the
# window. A formation with no Move inside the window is UNMEASURABLE, never "slow".

$ErrorActionPreference = 'Stop'

function Dist([double]$ax, [double]$ay, [double]$bx, [double]$by) {
    $dx = $bx - $ax; $dy = $by - $ay
    return [Math]::Sqrt($dx * $dx + $dy * $dy)
}

$log = Join-Path $SessionDir 'CombatBrain2.log'
$geometry = @{}; $writes = @{}

foreach ($line in (Get-Content -LiteralPath $log -Encoding UTF8)) {
    if ($line -match 'COORDINATION_BOUND.*plan=(plan-\d+).*M\*=\(([-\d.]+),([-\d.]+)\) G\*=\(([-\d.]+),([-\d.]+)\) S\*=\(([-\d.]+),([-\d.]+)\)') {
        $geometry[$matches[1]] = [pscustomobject]@{
            Gx=[double]$matches[4]; Gy=[double]$matches[5]
            Sx=[double]$matches[6]; Sy=[double]$matches[7] }
    }
    if ($line -match '\[CB2\]\[WRITE_EXECUTED\] task=(plan-\d+)-task-(\d+) plan=(plan-\d+) kind=Reposition key=(\d+/\d+) .*?role=(\w+) .*?center=\(([-\d.]+),([-\d.]+)\) .*? at=([\d.]+)') {
        $k = $matches[1] + '#' + $matches[2]
        if (-not $writes.ContainsKey($k)) {
            $writes[$k] = [pscustomobject]@{
                Plan=$matches[3]; Key=$matches[4]; Role=$matches[5]
                CX=[double]$matches[6]; CY=[double]$matches[7]; At=[double]$matches[8] }
        }
    }
}

$samples = @()
foreach ($f in (Get-ChildItem -LiteralPath $SessionDir -Filter 'p2-evidence-*.json')) {
    try {
        $raw = (Get-Content -LiteralPath $f.FullName -Raw -Encoding UTF8 | ConvertFrom-Json).result.content[0].text
        $samples += ($raw | ConvertFrom-Json)
    } catch { }
}
$samples = $samples | Sort-Object { [double]$_.mission_time }

function TrackOf([string]$key) {
    $slot = [int]($key.Split('/')[1])
    $pts = @()
    foreach ($s in $samples) {
        foreach ($fm in (($s.teams | Where-Object { $_.team_index -eq 1 }).formations)) {
            if ($fm.formation_index -ne $slot) { continue }
            $pts += [pscustomobject]@{
                T = [double]$s.mission_time
                X = [double]$fm.active_center.x
                Y = [double]$fm.active_center.y
                Order = $fm.movement_order }
        }
    }
    return $pts
}

function SpeedInWindow($pts, [double]$from, [double]$to) {
    $w = @($pts | Where-Object { $_.T -ge $from -and $_.T -le $to })
    $move = @($w | Where-Object { $_.Order -eq 'Move' })
    if ($move.Count -lt 2) { return -1.0 }
    $first = $move[0]; $last = $move[$move.Count - 1]
    $dt = $last.T - $first.T
    if ($dt -lt 2.0) { return -1.0 }
    return (Dist $first.X $first.Y $last.X $last.Y) / $dt
}

$planIds = @($writes.Values | ForEach-Object { $_.Plan } | Sort-Object -Unique)
foreach ($plan in $planIds) {
    if (-not $geometry.ContainsKey($plan)) { continue }
    $g = $geometry[$plan]

    $gW = $null; $sW = $null
    foreach ($c in $writes.Values) {
        if ($c.Plan -ne $plan) { continue }
        if ($c.Role -eq 'Guard') { $gW = $c }
        if ($c.Role -eq 'Support') { $sW = $c }
    }
    if ($null -eq $gW -or $null -eq $sW) { continue }

    Write-Host ''
    Write-Host ('### {0}  guardWrite={1} supportWrite={2}' -f $plan, $gW.At, $sW.At)

    # Compare under the earlier write time, which is the harsher (shorter) budget.
    $t0 = [Math]::Min($gW.At, $sW.At)
    $t1 = $t0 + $BudgetSeconds

    $facts = @{}
    foreach ($c in @($gW, $sW)) {
        $pts = TrackOf $c.Key
        $v = SpeedInWindow $pts $t0 $t1
        $s = $null
        foreach ($p in $pts) { if ($p.T -ge $t0) { $s = $p; break } }
        if ($null -eq $s) { continue }
        $facts[$c.Key] = [pscustomobject]@{
            Key=$c.Key; V=$v
            dG=(Dist $s.X $s.Y $g.Gx $g.Gy)
            dS=(Dist $s.X $s.Y $g.Sx $g.Sy) }
    }

    $keys = @($facts.Keys | Sort-Object)
    if ($keys.Count -lt 2) { Write-Host '    insufficient tracks'; continue }

    foreach ($pairName in @('AS-WRITTEN', 'SWAPPED')) {
        $gk = $gW.Key; $sk = $sW.Key
        if ($pairName -eq 'SWAPPED') { $gk = $sW.Key; $sk = $gW.Key }
        if (-not $facts.ContainsKey($gk) -or -not $facts.ContainsKey($sk)) { continue }
        $gu = $facts[$gk]; $su = $facts[$sk]
        $line = '    {0,-10} guard={1} support={2}' -f $pairName, $gk, $sk
        $ok = $true; $notes = @()
        foreach ($side in @(@('G',$gu,$gu.dG), @('S',$su,$su.dS))) {
            $name = $side[0]; $u = $side[1]; $d = $side[2]
            if ($u.V -le 0) { $notes += ("{0}: unresolvable speed" -f $name); $ok = $false; continue }
            $t = $d / $u.V
            $mark = 'ok'
            if ($t -gt $BudgetSeconds) { $mark = 'LATE'; $ok = $false }
            $notes += ("{0}: {1:N1}m @ {2:N2}m/s = {3:N1}s {4}" -f $name, $d, $u.V, $t, $mark)
        }
        $verdict = 'FAIL'
        if ($ok) { $verdict = 'ARRIVES' }
        $line + '  ' + ($notes -join ' | ') + '  -> ' + $verdict
    }
}
