param(
    [Parameter(Mandatory = $true)][string]$SessionDir,
    [double]$BudgetSeconds = 20.0
)

# Read-only reachability audit, correct window semantics.
#
# The relationship window is [WrittenAt, WrittenAt + 20s] -- it starts when the
# command is actually written, not when the plan is created. Earlier revisions of
# this script used the plan creation time and therefore understated the budget by
# the first-command delay (5.5 s in one observed case). Those results are void.
#
# Speed is measured only from samples inside that window where the formation really
# held a Move order. A formation never ordered to Move inside its own window is
# reported UNMEASURABLE -- that neither proves nor disproves reachability.
#
# The selected assignment is printed with its real outcome so an estimate is never
# mistaken for observation.

$ErrorActionPreference = 'Stop'

function Dist([double]$ax, [double]$ay, [double]$bx, [double]$by) {
    $dx = $bx - $ax; $dy = $by - $ay
    return [Math]::Sqrt($dx * $dx + $dy * $dy)
}

$log = Join-Path $SessionDir 'CombatBrain2.log'
$geometry = @{}; $created = @{}; $writes = @{}

foreach ($line in (Get-Content -LiteralPath $log -Encoding UTF8)) {
    if ($line -match 'COORDINATION_BOUND.*plan=(plan-\d+).*M\*=\(([-\d.]+),([-\d.]+)\) G\*=\(([-\d.]+),([-\d.]+)\) S\*=\(([-\d.]+),([-\d.]+)\)') {
        $geometry[$matches[1]] = [pscustomobject]@{
            Mx=[double]$matches[2]; My=[double]$matches[3]
            Gx=[double]$matches[4]; Gy=[double]$matches[5]
            Sx=[double]$matches[6]; Sy=[double]$matches[7] }
    }
    if ($line -match 'PLAN_CREATED.*?\bplan=(plan-\d+)\b.*?created_at=([\d.]+)') {
        $created[$matches[1]] = [double]$matches[2]
    }
    # Only first-write per task: exits are Hold rewrites and would overwrite the role.
    if ($line -match '\[CB2\]\[WRITE_EXECUTED\] task=(plan-\d+)-task-(\d+) plan=(plan-\d+) kind=Reposition key=(\d+/\d+) .*?role=(\w+) tactic=\(([-\d.]+),([-\d.]+)\) anchor=\(([-\d.]+),([-\d.]+)\) center=\(([-\d.]+),([-\d.]+)\) .*? at=([\d.]+)') {
        $k = $matches[1] + '#' + $matches[2]
        if (-not $writes.ContainsKey($k)) {
            $writes[$k] = [pscustomobject]@{
                Plan=$matches[3]; Key=$matches[4]; Role=$matches[5]
                TX=[double]$matches[6]; TY=[double]$matches[7]
                AX=[double]$matches[8]; AY=[double]$matches[9]
                CX=[double]$matches[10]; CY=[double]$matches[11]
                At=[double]$matches[12] }
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

function WindowResult($pts, [double]$from, [double]$to, [double]$tx, [double]$ty) {
    $w = @($pts | Where-Object { $_.T -ge $from -and $_.T -le $to })
    $move = @($w | Where-Object { $_.Order -eq 'Move' })
    if ($move.Count -lt 2) { return $null }
    $first = $move[0]; $last = $move[$move.Count - 1]
    $dt = $last.T - $first.T
    if ($dt -lt 2.0) { return $null }
    $travelled = Dist $first.X $first.Y $last.X $last.Y
    $startD = Dist $first.X $first.Y $tx $ty
    $endD = Dist $last.X $last.Y $tx $ty
    return [pscustomobject]@{
        V = $travelled / $dt
        StartD = $startD; EndD = $endD
        Closed = $startD - $endD
        Span = $dt; MoveSamples = $move.Count; WindowSamples = $w.Count }
}

foreach ($plan in ($created.Keys | Sort-Object)) {
    if (-not $geometry.ContainsKey($plan)) { continue }
    $g = $geometry[$plan]
    Write-Host ''
    Write-Host ('### {0}  created_at={1}' -f $plan, $created[$plan])

    foreach ($role in @('Guard', 'Support')) {
        $w = $null
        foreach ($k in $writes.Keys) {
            $c = $writes[$k]
            if ($c.Plan -eq $plan -and $c.Role -eq $role) { $w = $c; break }
        }
        if ($null -eq $w) { Write-Host ("    {0,-7} no write recorded" -f $role); continue }
        $tx = $g.Gx; $ty = $g.Gy
        if ($role -eq 'Support') { $tx = $g.Sx; $ty = $g.Sy }
        $need = Dist $w.CX $w.CY $tx $ty
        $deadline = $w.At + $BudgetSeconds
        $pts = TrackOf $w.Key
        $r = WindowResult $pts $w.At $deadline $tx $ty
        if ($null -eq $r) {
            Write-Host ("    {0,-7} key={1,-4} write={2,-7} need={3,5:N1}m  UNMEASURABLE (no Move inside its own window)" -f `
                $role, $w.Key, $w.At, $need)
            continue
        }
        $reqSpeed = $need / $BudgetSeconds
        $verdict = 'INSUFFICIENT'
        if ($r.EndD -le 5.0) { $verdict = 'ARRIVED' }
        Write-Host ("    {0,-7} key={1,-4} write={2,-7} deadline={3,-7} need={4,5:N1}m  vObs={5,4:N2} m/s  closed={6,5:N1}m in {7,4:N1}s  left={8,5:N1}m  needV={9,4:N2}  -> {10}" -f `
            $role, $w.Key, $w.At, $deadline, $need, $r.V, $r.Closed, $r.Span, $r.EndD, $reqSpeed, $verdict)
    }
}
