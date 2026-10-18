param(
    [Parameter(Mandatory = $true)][string]$SessionDir
)

# Read-only recompute: use real centroids from evidence, enumerate every legal
# three-role permutation, and judge the 20 s relationship budget against the
# speed actually observed in that same session. No production value is changed.

$ErrorActionPreference = 'Stop'

function Dist([double]$ax, [double]$ay, [double]$bx, [double]$by) {
    $dx = $bx - $ax
    $dy = $by - $ay
    return [Math]::Sqrt($dx * $dx + $dy * $dy)
}

$log = Join-Path $SessionDir 'CombatBrain2.log'
if (-not (Test-Path -LiteralPath $log)) { throw "missing log: $log" }

$geometry = @{}
$taskKey = @{}
$created = @{}

foreach ($line in (Get-Content -LiteralPath $log -Encoding UTF8)) {
    if ($line -match 'COORDINATION_BOUND.*plan=(plan-\d+).*M\*=\(([-\d.]+),([-\d.]+)\) G\*=\(([-\d.]+),([-\d.]+)\) S\*=\(([-\d.]+),([-\d.]+)\)') {
        $geometry[$matches[1]] = [pscustomobject]@{
            Mx = [double]$matches[2]; My = [double]$matches[3]
            Gx = [double]$matches[4]; Gy = [double]$matches[5]
            Sx = [double]$matches[6]; Sy = [double]$matches[7]
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
Write-Host ("samples=" + $samples.Count)

function SampleAt([double]$t) {
    $best = $null
    $bd = [double]::MaxValue
    foreach ($s in $script:samples) {
        $delta = [Math]::Abs([double]$s.mission_time - $t)
        if ($delta -lt $bd) { $bd = $delta; $best = $s }
    }
    return $best
}

# Average speed actually observed for one formation between two times.
function ObservedSpeed([string]$key, [double]$t0, [double]$t1) {
    $a = SampleAt $t0
    $b = SampleAt $t1
    if ($null -eq $a -or $null -eq $b) { return -1.0 }
    $fa = (($a.teams | Where-Object { $_.team_index -eq 1 }).formations | Where-Object { ('1/' + $_.formation_index) -eq $key })
    $fb = (($b.teams | Where-Object { $_.team_index -eq 1 }).formations | Where-Object { ('1/' + $_.formation_index) -eq $key })
    if ($null -eq $fa -or $null -eq $fb) { return -1.0 }
    $moved = Dist ([double]$fa.active_center.x) ([double]$fa.active_center.y) ([double]$fb.active_center.x) ([double]$fb.active_center.y)
    $elapsed = [double]$b.mission_time - [double]$a.mission_time
    if ($elapsed -le 0.05) { return -1.0 }
    return $moved / $elapsed
}

foreach ($plan in ($created.Keys | Sort-Object)) {
    if (-not $geometry.ContainsKey($plan)) { continue }
    $g = $geometry[$plan]
    $t = $created[$plan]
    $sample = SampleAt $t
    if ($null -eq $sample) { continue }
    $myTeam = $sample.teams | Where-Object { $_.team_index -eq 1 }

    $units = @{}
    foreach ($fm in $myTeam.formations) {
        $k = '1/' + $fm.formation_index
        $units[$k] = [pscustomobject]@{
            Key = $k
            Class = $fm.formation_class
            Members = [int]$fm.active_member_count
            X = [double]$fm.active_center.x
            Y = [double]$fm.active_center.y
            dG = (Dist ([double]$fm.active_center.x) ([double]$fm.active_center.y) $g.Gx $g.Gy)
            dS = (Dist ([double]$fm.active_center.x) ([double]$fm.active_center.y) $g.Sx $g.Sy)
            Speed = (ObservedSpeed $k $t ($t + 12.0))
        }
    }

    $mainKey = $taskKey[$plan + '#1']
    $guardKey = $taskKey[$plan + '#2']
    $supportKey = $taskKey[$plan + '#3']

    Write-Host ''
    Write-Host ("### " + $plan + " t=" + $t + " sample=" + $sample.mission_time)
    Write-Host ("    G*=(" + $g.Gx + "," + $g.Gy + ") S*=(" + $g.Sx + "," + $g.Sy + ") budget=20s")
    foreach ($k in ($units.Keys | Sort-Object)) {
        $u = $units[$k]
        $reachG = 999.0
        $reachS = 999.0
        if ($u.Speed -gt 0.1) { $reachG = $u.dG / $u.Speed; $reachS = $u.dS / $u.Speed }
        Write-Host ("    {0} {1,-8} n={2,-4} dG*={3,6:N2} dS*={4,6:N2} v={5,5:N2} tG={6,6:N1}s tS={7,6:N1}s" -f `
            $u.Key, $u.Class, $u.Members, $u.dG, $u.dS, $u.Speed, $reachG, $reachS)
    }
    Write-Host ("    actual: main=" + $mainKey + " guard=" + $guardKey + " support=" + $supportKey)

    $keys = @($units.Keys | Sort-Object)
    foreach ($m in $keys) {
        $rest = @($keys | Where-Object { $_ -ne $m })
        if ($rest.Count -lt 2) { continue }
        foreach ($pair in @(@($rest[0], $rest[1]), @($rest[1], $rest[0]))) {
            $gk = $pair[0]
            $sk = $pair[1]
            $gu = $units[$gk]
            $su = $units[$sk]
            $tG = 999.0
            $tS = 999.0
            if ($gu.Speed -gt 0.1) { $tG = $gu.dG / $gu.Speed }
            if ($su.Speed -gt 0.1) { $tS = $su.dS / $su.Speed }
            $ok = ($tG -le 20.0) -and ($tS -le 20.0)
            Write-Host ("    perm main={0} guard={1}(tG={2,6:N1}) support={3}(tS={4,6:N1}) feasible={5}" -f `
                $m, $gk, $tG, $sk, $tS, $ok)
        }
    }
}
