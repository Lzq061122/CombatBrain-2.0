# Read-only: for a given plan, dump the Guard formation's envelope state at 1 Hz
# between plan creation and plan invalidation, so "failed after 3s" can be told
# apart from "arrived but not recognised" and from "too slow to close 3m in 3s".
# Pure ASCII (PS 5.1 reads scripts as ANSI).
param(
    [string]$Session = "p3-P-20261018-210338",
    [int]$Plan = 5,
    [double]$From = 166.0,
    [double]$To = 174.0
)

$dir = Join-Path "Program\_verify\sessions" $Session
$log = Join-Path $dir "CombatBrain2.log"

# Geometry and role bindings come from the log itself, not from re-deriving them.
$bound = $null
Get-Content $log | Select-String -Pattern ("COORDINATION_BOUND\] plan=plan-" + $Plan + " ") | ForEach-Object {
    if ($_.Line -match "M\*=\(([-\d.]+),([-\d.]+)\) G\*=\(([-\d.]+),([-\d.]+)\) S\*=\(([-\d.]+),([-\d.]+)\) u=\(([-\d.]+),([-\d.]+)\) v=\(([-\d.]+),([-\d.]+)\)") {
        $bound = @{
            Mx = [double]$matches[1]; My = [double]$matches[2]
            Gx = [double]$matches[3]; Gy = [double]$matches[4]
            Sx = [double]$matches[5]; Sy = [double]$matches[6]
            ux = [double]$matches[7]; uy = [double]$matches[8]
            vx = [double]$matches[9]; vy = [double]$matches[10]
        }
    }
}
if ($null -eq $bound) { Write-Output "no COORDINATION_BOUND for plan-$Plan"; exit 1 }

# Guard key from the same line.
$guardKey = ""
Get-Content $log | Select-String -Pattern ("COORDINATION_BOUND\] plan=plan-" + $Plan + " ") | ForEach-Object {
    if ($_.Line -match "guard=plan-" + $Plan + "-task-(\d+)") {
        $taskNum = $matches[1]
        Get-Content $log | Select-String -Pattern ("TASK_CREATED\] task=plan-" + $Plan + "-task-" + $taskNum + " ") | ForEach-Object {
            if ($_.Line -match "key=(\d+/\d+)") { $guardKey = $matches[1] }
        }
    }
}
if ($guardKey -eq "") { Write-Output "cannot resolve guard key for plan-$Plan"; exit 1 }

$gTeam, $gIdx = $guardKey.Split('/')
Write-Output ("plan-{0} guard={1} M*=({2},{3}) G*=({4},{5}) u=({6},{7}) v=({8},{9})" -f `
    $Plan, $guardKey, $bound.Mx, $bound.My, $bound.Gx, $bound.Gy, $bound.ux, $bound.uy, $bound.vx, $bound.vy)
Write-Output ""

foreach ($f in Get-ChildItem $dir -Filter "p2-evidence-*.json" | Sort-Object Name) {
    $raw = Get-Content $f.FullName -Raw
    if ($raw -notmatch 'mission_time') { continue }
    try { $j = $raw | ConvertFrom-Json } catch { continue }
    $txt = $j.result.content[0].text
    if (-not $txt) { continue }
    try { $obj = $txt | ConvertFrom-Json } catch { continue }
    if (-not $obj.mission_time) { continue }
    $t = [double]$obj.mission_time
    if ($t -lt $From -or $t -gt $To) { continue }
    foreach ($team in $obj.teams) {
        if ($team.team_index -ne [int]$gTeam) { continue }
        foreach ($r in $team.formations) {
            if ($r.formation_index -ne [int]$gIdx) { continue }
            if (-not $r.active_center_known) { continue }
            $x = [double]$r.active_center.x; $y = [double]$r.active_center.y
            $dx = $x - $bound.Mx; $dy = $y - $bound.My
            $fwd = $dx * $bound.ux + $dy * $bound.uy
            $lat = $dx * $bound.vx + $dy * $bound.vy
            $dG = [Math]::Sqrt(($x - $bound.Gx) * ($x - $bound.Gx) + ($y - $bound.Gy) * ($y - $bound.Gy))
            $distOk = $dG -le 5.0
            $fwdOk = ($fwd -ge 5.0) -and ($fwd -le 15.0)
            $latOk = [Math]::Abs($lat) -le 5.0
            Write-Output ("t={0,7:N2} pos=({1},{2}) fwd={3,6:N2}{4} lat={5,6:N2}{6} distG*={7,5:N2}{8} units={9}" -f `
                $t, $x, $y, $fwd, $(if ($fwdOk) { " " } else { "*" }), $lat, $(if ($latOk) { " " } else { "*" }), `
                $dG, $(if ($distOk) { " " } else { "*" }), $r.active_member_count)
        }
    }
}
