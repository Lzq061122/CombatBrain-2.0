# Read-only: for one formation key, print order / distance-to-target / closed
# distance across a window, and whether the role envelope was ever entered.
# Pure ASCII (PS 5.1 reads scripts as ANSI).
param(
    [string]$Session = "p3-P-20261018-225324",
    [int]$Team = 1,
    [int]$Idx = 1,
    [double]$Tx = 1139.341,
    [double]$Ty = 831.817,
    [double]$From = 18.0,
    [double]$To = 40.0
)

$dir = Join-Path "Program\_verify\sessions" $Session
foreach ($f in Get-ChildItem $dir -Filter "p2-evidence-*.json" | Sort-Object Name) {
    $raw = Get-Content $f.FullName -Raw
    if ($raw -notmatch 'mission_time') { continue }
    try { $j = $raw | ConvertFrom-Json } catch { continue }
    $txt = $j.result.content[0].text
    if (-not $txt) { continue }
    try { $o = $txt | ConvertFrom-Json } catch { continue }
    if (-not $o.mission_time) { continue }
    $t = [double]$o.mission_time
    if ($t -lt $From -or $t -gt $To) { continue }
    foreach ($tm in $o.teams) {
        if ($tm.team_index -ne $Team) { continue }
        foreach ($r in $tm.formations) {
            if ($r.formation_index -ne $Idx) { continue }
            if (-not $r.active_center_known) { continue }
            $x = [double]$r.active_center.x
            $y = [double]$r.active_center.y
            $dx = $Tx - $x; $dy = $Ty - $y
            $d = [Math]::Sqrt($dx * $dx + $dy * $dy)
            $inZone = 'no'
            if ($d -le 5.0) { $inZone = 'YES' }
            Write-Output ("t={0,7:N2} order={1,-18} units={2,4} pos=({3,8:N1},{4,8:N1}) dTarget={5,6:N2} inEnvelope={6}" -f `
                $t, $r.movement_order, $r.active_member_count, $x, $y, $d, $inZone)
        }
    }
}
