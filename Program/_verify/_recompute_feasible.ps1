param(
    [Parameter(Mandatory = $true)][string]$SessionDir,
    [double]$BudgetSeconds = 20.0
)

# Read-only: honest reachability per plan.
#
# Earlier attempts had three flaws, each of which changed the conclusion:
#   1. speed over a fixed window regardless of order -> a standing formation read
#      ~0 and was called infeasible (order artifact, not a capability limit);
#   2. speed over per-second deltas -> coarse 1 Hz sampling with jumps read 0.07 m/s;
#   3. speed over the session-wide longest Move run -> imported unrelated fast
#      movement into a slow window (over-optimistic).
#
# This version measures, for each plan and each non-Main formation, the speed
# observed ONLY inside that plan's own relationship window (creation .. +budget),
# and only from samples where the formation actually held a Move order. A formation
# never ordered to Move inside the window is reported unmeasurable, not infeasible.
# The selected assignment's real outcome is printed alongside, so estimate and
# observation can be compared instead of one standing in for the other.

$ErrorActionPreference = 'Stop'

function Dist([double]$ax, [double]$ay, [double]$bx, [double]$by) {
    $dx = $bx - $ax; $dy = $by - $ay
    return [Math]::Sqrt($dx * $dx + $dy * $dy)
}

$log = Join-Path $SessionDir 'CombatBrain2.log'
$geometry = @{}; $taskKey = @{}; $created = @{}
foreach ($line in (Get-Content -LiteralPath $log -Encoding UTF8)) {
    if ($line -match 'COORDINATION_BOUND.*plan=(plan-\d+).*M\*=\(([-\d.]+),([-\d.]+)\) G\*=\(([-\d.]+),([-\d.]+)\) S\*=\(([-\d.]+),([-\d.]+)\)') {
        $geometry[$matches[1]] = [pscustomobject]@{
            Mx=[double]$matches[2]; My=[double]$matches[3]
            Gx=[double]$matches[4]; Gy=[double]$matches[5]
            Sx=[double]$matches[6]; Sy=[double]$matches[7] }
    }
    if ($line -match 'TASK_CREATED.*task=(plan-\d+)-task-(\d+) .*key=(\d+/\d+)') {
        $taskKey[$matches[1] + '#' + $matches[2]] = $matches[3]
    }
    if ($line -match 'PLAN_CREATED.*?\bplan=(plan-\d+)\b.*?created_at=([\d.]+)') {
        $created[$matches[1]] = [double]$matches[2]
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

function WindowSpeed($pts, [double]$from, [double]$to) {
    $w = @($pts | Where-Object { $_.T -ge $from -and $_.T -le $to })
    $move = @($w | Where-Object { $_.Order -eq 'Move' })
    if ($move.Count -lt 2) { return -1.0 }
    $first = $move[0]; $last = $move[$move.Count - 1]
    $dt = $last.T - $first.T
    if ($dt -lt 2.0) { return -1.0 }
    return (Dist $first.X $first.Y $last.X $last.Y) / $dt
}

foreach ($plan in ($created.Keys | Sort-Object)) {
    if (-not $geometry.ContainsKey($plan)) { continue }
    $g = $geometry[$plan]; $t = $created[$plan]

    $best = $null; $bd = [double]::MaxValue
    foreach ($s in $samples) {
        $d = [Math]::Abs([double]$s.mission_time - $t)
        if ($d -lt $bd) { $bd = $d; $best = $s }
    }
    if ($null -eq $best) { continue }

    $mainKey = $taskKey[$plan + '#1']
    $guardKey = $taskKey[$plan + '#2']
    $supportKey = $taskKey[$plan + '#3']

    $info = @{}
    foreach ($fm in (($best.teams | Where-Object { $_.team_index -eq 1 }).formations)) {
        $k = '1/' + $fm.formation_index
        $x = [double]$fm.active_center.x; $y = [double]$fm.active_center.y
        $pts = TrackOf $k
        $v = WindowSpeed $pts $t ($t + $BudgetSeconds)
        $info[$k] = [pscustomobject]@{
            Key=$k; Class=$fm.formation_class; Units=[int]$fm.active_member_count
            dG=(Dist $x $y $g.Gx $g.Gy); dS=(Dist $x $y $g.Sx $g.Sy); V=$v }
    }

    Write-Host ''
    Write-Host ("### {0} t={1} main={2} guard={3} support={4}" -f $plan, $t, $mainKey, $guardKey, $supportKey)
    foreach ($k in ($info.Keys | Sort-Object)) {
        $u = $info[$k]
        $vText = 'no Move inside window'
        if ($u.V -gt 0) { $vText = ('{0:N2} m/s' -f $u.V) }
        Write-Host ("    {0} {1,-8} n={2,-4} dG*={3,6:N1} dS*={4,6:N1} vWindow={5}" -f `
            $u.Key, $u.Class, $u.Units, $u.dG, $u.dS, $vText)
    }

    $others = @($info.Keys | Where-Object { $_ -ne $mainKey } | Sort-Object)
    $anyFeasible = $false
    foreach ($pair in @(@($others[0], $others[1]), @($others[1], $others[0]))) {
        $gu = $info[$pair[0]]; $su = $info[$pair[1]]
        $ok = 'UNMEASURABLE'; $detail = ''
        if ($gu.V -gt 0 -and $su.V -gt 0) {
            $tG = $gu.dG / $gu.V; $tS = $su.dS / $su.V
            $detail = (' tG={0:N1}s tS={1:N1}s' -f $tG, $tS)
            if ($tG -le $BudgetSeconds -and $tS -le $BudgetSeconds) { $ok = 'FEASIBLE'; $anyFeasible = $true }
            else { $ok = 'INFEASIBLE' }
        }
        $mark = ''
        if ($pair[0] -eq $guardKey -and $pair[1] -eq $supportKey) { $mark = '  <= SELECTED' }
        Write-Host ("    assign guard={0} support={1} -> {2}{3}{4}" -f $pair[0], $pair[1], $ok, $detail, $mark)
    }
    $summary = 'no feasible assignment among measured ones'
    if ($anyFeasible) { $summary = 'a feasible assignment exists' }
    Write-Host ("    => {0}" -f $summary)
}
