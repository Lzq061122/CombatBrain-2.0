# Read-only: does the Guard formation's member digest stay stable across the
# 3 s feedback window of a plan? A digest change clears DependencyReadiness,
# which would make "role requirement already satisfied" false at feedback time.
# Pure ASCII (PS 5.1 reads scripts as ANSI).
param(
    [string]$Session = "p3-P-20261018-210338",
    [string]$Team = "1",
    [int]$Idx = 1,
    [double]$From = 165.0,
    [double]$To = 176.0
)

$dir = Join-Path "Program\_verify\sessions" $Session
$prev = $null
$changes = 0
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
        if ($tm.team_index -ne [int]$Team) { continue }
        foreach ($r in $tm.formations) {
            if ($r.formation_index -ne $Idx) { continue }
            $dig = $r.member_digest
            $mark = ""
            if ($null -ne $prev -and $dig -ne $prev) { $mark = "  <<< DIGEST CHANGED"; $changes++ }
            $prev = $dig
            Write-Output ("t={0,7:N2} units={1,4} digest={2}{3}" -f $t, $r.active_member_count, $dig, $mark)
        }
    }
}
Write-Output ""
Write-Output ("digest changes in window: {0}" -f $changes)
