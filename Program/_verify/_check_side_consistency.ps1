param(
    [Parameter(Mandatory = $true)][string]$SessionDir
)

# Read-only check: is the lateral side (S* placed on the plus or minus side of M*)
# chosen for the same formation that finally executes Support?
# CoordinationGeometry.Compute picks the side using supportOrigin = rest[0];
# AssignGuardAndSupport may then give Support to a different formation.

$ErrorActionPreference = 'Stop'

function Dist([double]$ax, [double]$ay, [double]$bx, [double]$by) {
    $dx = $bx - $ax
    $dy = $by - $ay
    return [Math]::Sqrt($dx * $dx + $dy * $dy)
}

$log = Join-Path $SessionDir 'CombatBrain2.log'
$geometry = @{}
$taskKey = @{}
$created = @{}
$listing = @{}

foreach ($line in (Get-Content -LiteralPath $log -Encoding UTF8)) {
    if ($line -match 'COORDINATION_BOUND.*plan=(plan-\d+).*M\*=\(([-\d.]+),([-\d.]+)\) G\*=\(([-\d.]+),([-\d.]+)\) S\*=\(([-\d.]+),([-\d.]+)\) u=\(([-\d.]+),([-\d.]+)\) v=\(([-\d.]+),([-\d.]+)\)') {
        $geometry[$matches[1]] = [pscustomobject]@{
            Mx = [double]$matches[2]; My = [double]$matches[3]
            Sx = [double]$matches[6]; Sy = [double]$matches[7]
            Vx = [double]$matches[10]; Vy = [double]$matches[11]
        }
    }
    if ($line -match 'TASK_CREATED.*task=(plan-\d+)-task-(\d+) .*key=(\d+/\d+)') {
        $taskKey[$matches[1] + '#' + $matches[2]] = $matches[3]
    }
    if ($line -match 'PLAN_CREATED.*?\bplan=(plan-\d+)\b.*?\bformations=([\d,/]+)') {
        $listing[$matches[1]] = $matches[2]
    }
    if ($line -match 'PLAN_CREATED.*?\bplan=(plan-\d+)\b.*?created_at=([\d.]+)') {
        $created[$matches[1]] = [double]$matches[2]
    }
}

$files = Get-ChildItem -LiteralPath $SessionDir -Filter 'p2-evidence-*.json'
$samples = @()
foreach ($f in $files) {
    try {
        $raw = (Get-Content -LiteralPath $f.FullName -Raw -Encoding UTF8 | ConvertFrom-Json).result.content[0].text
        $samples += ($raw | ConvertFrom-Json)
    } catch { }
}
$samples = $samples | Sort-Object { [double]$_.mission_time }

foreach ($plan in ($created.Keys | Sort-Object)) {
    if (-not $geometry.ContainsKey($plan)) { continue }
    $g = $geometry[$plan]

    $best = $null
    $bd = [double]::MaxValue
    foreach ($s in $samples) {
        $delta = [Math]::Abs([double]$s.mission_time - $created[$plan])
        if ($delta -lt $bd) { $bd = $delta; $best = $s }
    }
    if ($null -eq $best) { continue }

    $pos = @{}
    foreach ($fm in (($best.teams | Where-Object { $_.team_index -eq 1 }).formations)) {
        $pos['1/' + $fm.formation_index] = @([double]$fm.active_center.x, [double]$fm.active_center.y)
    }

    $plusX = $g.Mx + $g.Vx * 12.0
    $plusY = $g.My + $g.Vy * 12.0
    $minusX = $g.Mx - $g.Vx * 12.0
    $minusY = $g.My - $g.Vy * 12.0

    $chosenSide = 'plus'
    if ((Dist $g.Sx $g.Sy $minusX $minusY) -lt (Dist $g.Sx $g.Sy $plusX $plusY)) { $chosenSide = 'minus' }

    $keys = @($pos.Keys | Sort-Object)
    if ($keys.Count -lt 3) { continue }
    $restZero = $keys[1]
    $actualSupport = $taskKey[$plan + '#3']

    $sideRest = 'unknown'
    if ($pos.ContainsKey($restZero)) {
        $dp = Dist $pos[$restZero][0] $pos[$restZero][1] $plusX $plusY
        $dm = Dist $pos[$restZero][0] $pos[$restZero][1] $minusX $minusY
        if ($dm -lt $dp) { $sideRest = 'minus' } else { $sideRest = 'plus' }
    }
    $sideSupport = 'unknown'
    if ($actualSupport -and $pos.ContainsKey($actualSupport)) {
        $dp2 = Dist $pos[$actualSupport][0] $pos[$actualSupport][1] $plusX $plusY
        $dm2 = Dist $pos[$actualSupport][0] $pos[$actualSupport][1] $minusX $minusY
        if ($dm2 -lt $dp2) { $sideSupport = 'minus' } else { $sideSupport = 'plus' }
    }

    $flags = @()
    if ($sideRest -ne $chosenSide) { $flags += 'rest0_would_pick_other' }
    if ($sideSupport -ne $chosenSide) { $flags += 'support_prefers_other_side' }
    $flagText = 'ok'
    if ($flags.Count -gt 0) { $flagText = ($flags -join ',') }

    Write-Host ("{0} formations={1} chosen={2} rest0={3}->{4} actualSupport={5}->{6} [{7}]" -f `
        $plan, $listing[$plan], $chosenSide, $restZero, $sideRest, $actualSupport, $sideSupport, $flagText)
}
