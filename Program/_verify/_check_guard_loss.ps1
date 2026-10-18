# Read-only replay: what changed for the Guard at the 148.092 dependency loss.
# Pure ASCII (PS 5.1 reads scripts as ANSI).
param([string]$Session = "p3-P-20261018-210338")

$dir = Join-Path "Program\_verify\sessions" $Session
$log = Join-Path $dir "CombatBrain2.log"
if (-not (Test-Path $log)) { Write-Output "missing log: $log"; exit 1 }

Write-Output "=== COORDINATION_STATE tail around 140-150s ==="
Get-Content $log | Select-String -Pattern "COORDINATION_STATE" |
    Select-String -Pattern "at=1[45][0-9]\." |
    ForEach-Object {
        $line = $_.Line
        if ($line -match "at=(\d+\.\d+)") {
            $t = [double]$matches[1]
            if ($t -ge 140 -and $t -le 150) { Write-Output ("{0,-8} {1}" -f $t, $line) }
        }
    }

Write-Output ""
Write-Output "=== Guard task write/feedback lines ==="
Get-Content $log | Select-String -Pattern "plan-2-task-2" |
    ForEach-Object { $_.Line } | Select-Object -First 25

Write-Output ""
Write-Output "=== last 6 COORDINATION_ADVANCE / advance task lines ==="
Get-Content $log | Select-String -Pattern "plan-2-task-4-g1" |
    ForEach-Object { $_.Line } | Select-Object -First 12

Write-Output ""
Write-Output "=== BATTLE_STATE around contact ==="
Get-Content $log | Select-String -Pattern "BATTLE_STATE" |
    Select-String -Pattern "at=14[5-9]\." |
    ForEach-Object { $_.Line } | Select-Object -First 6
