param(
    [Parameter(Mandatory = $true)][string]$SessionDir
)

# Read-only: what order did each formation carry at each plan-creation moment?
# A formation ordered to Hold shows ~0 observed speed; that is an order artifact,
# NOT evidence that it cannot travel. Travel feasibility must therefore be judged
# only on formations that were actually ordered to move.

$ErrorActionPreference = 'Stop'

$log = Join-Path $SessionDir 'CombatBrain2.log'
$created = @{}
foreach ($line in (Get-Content -LiteralPath $log -Encoding UTF8)) {
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
    $t = $created[$plan]
    $best = $null; $bd = [double]::MaxValue
    foreach ($s in $samples) {
        $d = [Math]::Abs([double]$s.mission_time - $t)
        if ($d -lt $bd) { $bd = $d; $best = $s }
    }
    if ($null -eq $best) { continue }
    $parts = @()
    foreach ($fm in (($best.teams | Where-Object { $_.team_index -eq 1 }).formations)) {
        $parts += ("1/" + $fm.formation_index + " " + $fm.formation_class + " " + $fm.movement_order)
    }
    Write-Host ("{0} t={1} sample={2}: {3}" -f $plan, $t, $best.mission_time, ($parts -join ' | '))
}
