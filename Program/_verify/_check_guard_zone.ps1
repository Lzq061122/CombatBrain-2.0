# Read-only: locate the cavalry (1/2) sample nearest the 148.092 dependency loss.
# Pure ASCII (PS 5.1 reads scripts as ANSI).
param([string]$Session = "p3-P-20261018-210338")

$dir = Join-Path "Program\_verify\sessions" $Session
$Mx = 1297.234; $My = 835.285
$ux = -0.611; $uy = -0.791; $vx = 0.791; $vy = -0.611
$Gx = 1291.119; $Gy = 827.373

foreach ($f in Get-ChildItem $dir -Filter "p2-evidence-*.json" | Sort-Object Name) {
    $raw = Get-Content $f.FullName -Raw
    if ($raw -notmatch 'mission_time') { continue }
    try { $j = $raw | ConvertFrom-Json } catch { continue }
    $txt = $j.result.content[0].text
    if (-not $txt) { continue }
    try { $obj = $txt | ConvertFrom-Json } catch { continue }
    if (-not $obj.mission_time) { continue }
    $t = [double]$obj.mission_time
    if ($t -lt 147.0 -or $t -gt 149.2) { continue }
    foreach ($team in $obj.teams) {
        foreach ($r in $team.formations) {
            if ($team.team_index -ne 1) { continue }
            if ($r.formation_index -ne 2) { continue }
            $x = [double]$r.active_center.x; $y = [double]$r.active_center.y
            $dx = $x - $Mx; $dy = $y - $My
            $fwd = $dx * $ux + $dy * $uy
            $lat = $dx * $vx + $dy * $vy
            $dG = [Math]::Sqrt(($x - $Gx) * ($x - $Gx) + ($y - $Gy) * ($y - $Gy))
            $inPos = ($dG -le 5.0) -and ($fwd -ge 5.0) -and ($fwd -le 15.0) -and ([Math]::Abs($lat) -le 5.0)
            Write-Output ("t={0,8:N3} center=({1},{2}) fwd={3,6:N2} lat={4,6:N2} distG*={5,5:N2} units={6} inPos={7}" -f `
                $t, $x, $y, $fwd, $lat, $dG, $r.active_member_count, $inPos)
        }
    }
}
