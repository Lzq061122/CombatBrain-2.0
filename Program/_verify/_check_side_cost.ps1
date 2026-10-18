param(
    [Parameter(Mandatory = $true)][string]$SessionDir
)

# Read-only: how much does the lateral side choice cost the formation that
# actually executes Support?  Compares its distance to the chosen S* against
# the distance to the mirrored S* (same 12 m offset, opposite side of M*).

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

    $best = $null; $bd = [double]::MaxValue
    foreach ($s in $samples) {
        $d = [Math]::Abs([double]$s.mission_time - $created[$plan])
        if ($d -lt $bd) { $bd = $d; $best = $s }
    }
    if ($null -eq $best) { continue }

    # 12 s observed speed for the formation that actually carries Support.
    $supKey = $taskKey[$plan + '#3']
    if (-not $supKey) { continue }
    $slot = [int]($supKey.Split('/')[1])

    $pos = @{}
    foreach ($fm in (($best.teams | Where-Object { $_.team_index -eq 1 }).formations)) {
        $pos['1/' + $fm.formation_index] = $fm
    }
    if (-not $pos.ContainsKey($supKey)) { continue }

    # speed from the first/last sample inside a 12 s window after plan creation
    $window = @($samples | Where-Object {
        [double]$_.mission_time -ge $created[$plan] -and
        [double]$_.mission_time -le $created[$plan] + 12.0
    })
    $speed = 0.0
    if ($window.Count -ge 2) {
        $a = $null; $b = $null
        foreach ($s in $window) {
            foreach ($fm in (($s.teams | Where-Object { $_.team_index -eq 1 }).formations)) {
                if ($fm.formation_index -eq $slot) {
                    if ($null -eq $a) { $a = $fm }
                    $b = $fm
                }
            }
            if ($null -eq $a -and $null -ne $b) { $a = $b }
        }
        if ($null -ne $a -and $null -ne $b) {
            $dt = [double]$window[-1].mission_time - [double]$window[0].mission_time
            if ($dt -gt 0.1) {
                $speed = (Dist ([double]$a.active_center.x) ([double]$a.active_center.y) ([double]$b.active_center.x) ([double]$b.active_center.y)) / $dt
            }
        }
    }

    $cx = [double]$pos[$supKey].active_center.x
    $cy = [double]$pos[$supKey].active_center.y
    $dChosen = Dist $cx $cy $g.Sx $g.Sy
    $mirrorX = $g.Mx - ($g.Sx - $g.Mx)
    $mirrorY = $g.My - ($g.Sy - $g.My)
    $dMirror = Dist $cx $cy $mirrorX $mirrorY

    $tChosen = 999.0
    $tMirror = 999.0
    if ($speed -gt 0.05) {
        $tChosen = [Math]::Round($dChosen / $speed, 1)
        $tMirror = [Math]::Round($dMirror / $speed, 1)
    }

    $verdict = 'no_change'
    if ($dMirror + 1.0 -lt $dChosen) { $verdict = 'mirror_shorter' }
    if ($dChosen + 1.0 -lt $dMirror) { $verdict = 'chosen_shorter' }

    Write-Host ("{0} support={1} v={2:F2} dS*_chosen={3:F1}m ({4}s)  dS*_mirror={5:F1}m ({6}s)  [{7}]" -f `
        $plan, $supKey, $speed, $dChosen, $tChosen, $dMirror, $tMirror, $verdict)
}
