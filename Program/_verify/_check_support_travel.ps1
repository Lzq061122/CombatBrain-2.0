param(
    [Parameter(Mandatory = $true)][string]$SessionDir
)

# Read-only: for formations that CB2 actually ordered to Move as Support, measure
# how far they really travelled after the write, and whether their native order
# stayed Move. Only these readings can say anything about travel reachability;
# a formation sitting on StandYourGround proves nothing.

$ErrorActionPreference = 'Stop'

function Dist([double]$ax, [double]$ay, [double]$bx, [double]$by) {
    $dx = $bx - $ax; $dy = $by - $ay
    return [Math]::Sqrt($dx * $dx + $dy * $dy)
}

$log = Join-Path $SessionDir 'CombatBrain2.log'
$writes = @()
foreach ($line in (Get-Content -LiteralPath $log -Encoding UTF8)) {
    if ($line -match 'WRITE_EXECUTED.*task=(plan-\d+)-task-\d+ .*kind=Reposition key=(\d+/\d+) .*role=Support tactic=\(([-\d.]+),([-\d.]+)\).*center=\(([-\d.]+),([-\d.]+)\).*at=([\d.]+)') {
        $writes += [pscustomobject]@{
            Plan = $matches[1]; Key = $matches[2]
            TacticX = [double]$matches[3]; TacticY = [double]$matches[4]
            CenterX = [double]$matches[5]; CenterY = [double]$matches[6]
            At = [double]$matches[7]
        }
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

foreach ($w in $writes) {
    $slot = [int]($w.Key.Split('/')[1])
    $window = @($samples | Where-Object {
        [double]$_.mission_time -ge $w.At -and [double]$_.mission_time -le $w.At + 20.0
    })
    if ($window.Count -lt 2) { Write-Host ("{0} support={1} at={2}: no window" -f $w.Plan, $w.Key, $w.At); continue }

    $first = $null; $last = $null; $moveOrders = 0; $otherOrders = 0
    $orders = @()
    foreach ($s in $window) {
        foreach ($fm in (($s.teams | Where-Object { $_.team_index -eq 1 }).formations)) {
            if ($fm.formation_index -ne $slot) { continue }
            if ($null -eq $first) { $first = $fm }
            $last = $fm
            if ($fm.movement_order -eq 'Move') { $moveOrders++ } else { $otherOrders++; $orders += $fm.movement_order }
        }
    }
    if ($null -eq $first -or $null -eq $last) { continue }

    $dt = [double]$window[-1].mission_time - [double]$window[0].mission_time
    $travel = Dist ([double]$first.active_center.x) ([double]$first.active_center.y) `
                   ([double]$last.active_center.x) ([double]$last.active_center.y)
    $speed = 0.0
    if ($dt -gt 0.1) { $speed = $travel / $dt }
    $remain = Dist ([double]$last.active_center.x) ([double]$last.active_center.y) $w.TacticX $w.TacticY
    $startGap = Dist ([double]$first.active_center.x) ([double]$first.active_center.y) $w.TacticX $w.TacticY

    $orderNote = 'all_move'
    if ($otherOrders -gt 0) { $orderNote = 'other_orders=' + (($orders | Select-Object -Unique) -join ',') }

    Write-Host ("{0} support={1} at={2} startGap={3:F1}m travel20s={4:F1}m v={5:F2} remain={6:F1}m order={7}" -f `
        $w.Plan, $w.Key, $w.At, $startGap, $travel, $speed, $remain, $orderNote)
}
