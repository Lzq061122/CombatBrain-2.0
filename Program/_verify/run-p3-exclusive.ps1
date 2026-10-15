param(
    [ValidateSet('P', 'N', 'C')]
    [string]$Scenario = 'P',
    [int]$CombatHoldSec = 180,
    [int]$PollTimeoutSec = 240,
    [int]$LaunchWaitSec = 900,
    [int]$McpPort = 19580,
    [string]$GameId = 'bannerlord148-cb2',
    [int]$OpponentDelaySec = 6,
    [switch]$WhatIf
)

$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
$cb2Root = Split-Path -Parent (Split-Path -Parent $here)
$cb2DeployScript = Join-Path (Split-Path -Parent $here) '_deploy\deploy.ps1'
$cbathRepo = 'E:\MBmod\CBATH-P2'
$cbathDeployScript = Join-Path $cbathRepo 'RD\CombatBrain-Autonomous-Test-Harness\experiments\test-mod\CBATH\build-deploy.ps1'
$collectScript = Join-Path $here 'run-p3.ps1'
$stageScript = Join-Path $here '_p3-exclusive-stage.ps1'

$observationRoot = 'E:\MBmod\BattleObservation-V1'
$observationReadme = Join-Path $observationRoot 'README.md'
$observationHandoff = Join-Path $observationRoot 'coordination\handoff.md'
$coordination = Join-Path $observationRoot 'coordination\Invoke-ExclusiveGame.ps1'

# Owner must stay ASCII: PS 5.1 reads a BOM-less .ps1 as ANSI, which would corrupt a
# non-ASCII literal and break parsing. The coordinator compares this value as an opaque string.
$owner = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xE4,0xB8,0xBB,0xE7,0xBA,0xBF,0xE7,0xBB,0x84))

if (-not (Test-Path -LiteralPath $cb2DeployScript)) { throw "CB2 deploy script missing: $cb2DeployScript" }
if (-not (Test-Path -LiteralPath $cbathDeployScript)) { throw "CBATH deploy script missing: $cbathDeployScript" }
if (-not (Test-Path -LiteralPath $collectScript)) { throw "collect script missing: $collectScript" }
if (-not (Test-Path -LiteralPath $stageScript)) { throw "stage script missing: $stageScript" }
if (-not (Test-Path -LiteralPath $observationReadme)) { throw "observation README missing: $observationReadme" }
if (-not (Test-Path -LiteralPath $observationHandoff)) { throw "handover note missing: $observationHandoff" }

if ($WhatIf) {
    Write-Host "=== run-p3-exclusive whatif ==="
    Write-Host "scenario:      $Scenario"
    Write-Host "coordination:  $coordination"
    Write-Host "owner:         $owner"
    Write-Host ""
    Write-Host "read-only, outside the lock:"
    Write-Host "  source identity  : $cb2Root and $cbathRepo (git status/HEAD, no write)"
    Write-Host "  handover notes   : $observationReadme , $observationHandoff"
    Write-Host ""
    Write-Host "All three stages run INSIDE the coordination lock:"
    Write-Host "stage 1 deploy CB2   : $cb2DeployScript"
    Write-Host "  regenerates build_id, clean rebuild, deploys DLL, compares SHA256"
    Write-Host "stage 2 deploy CBATH : $cbathDeployScript"
    Write-Host "  regenerates CBATH build_id, clean rebuild, deploys harness, compares SHA256"
    Write-Host "stage 3 collect      : $collectScript"
    Write-Host "  -Scenario $Scenario -CombatHoldSec $CombatHoldSec -PollTimeoutSec $PollTimeoutSec"
    Write-Host "  -LaunchWaitSec $LaunchWaitSec -McpPort $McpPort -GameId $GameId -OpponentDelaySec $OpponentDelaySec"
    Write-Host ""
    Write-Host "This dry run writes no DLL, no config, no sentinel and starts no game."
    Write-Host "RESULT: WHATIF"
    exit 0
}

# ---------- read-only source and handover check, deliberately outside the lock ----------
# Dispatch 7 item 1: verify the sources and the handover before claiming the shared game.
# Nothing here writes, builds or switches anything.
Write-Host "=== read-only source check (outside the lock) ==="

function Show-GitIdentity([string]$root) {
    Write-Host "root: $root"
    if (-not (Test-Path -LiteralPath $root)) { throw "expected source root missing: $root" }
    $branch = (& git -C $root rev-parse --abbrev-ref HEAD) 2>&1
    $head = (& git -C $root rev-parse HEAD) 2>&1
    if ($LASTEXITCODE -ne 0) { throw "not a git work tree: $root" }
    Write-Host "  branch: $branch"
    Write-Host "  head:   $head"
    $status = @(& git -C $root status --porcelain) 2>&1
    Write-Host "  dirty entries: $($status.Count)"
    foreach ($line in $status) { Write-Host "    $line" }
}

Show-GitIdentity $cb2Root
Show-GitIdentity $cbathRepo

Write-Host "--- handover notes ---"
Get-Content -LiteralPath $observationReadme -TotalCount 6 | ForEach-Object { Write-Host "  readme: $_" }
Get-Content -LiteralPath $observationHandoff -TotalCount 4 | ForEach-Object { Write-Host "  handoff: $_" }

$runner = Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -match 'bannerlord' }
if ($runner) { throw "Bannerlord already running (pid $(($runner | ForEach-Object { $_.Id }) -join ',')); refusing to claim the shared game" }

if (-not (Test-Path -LiteralPath $coordination)) { throw "coordination entry missing: $coordination" }

Write-Host ""
$toolArgs = @{
    Cb2DeployPath    = $cb2DeployScript
    CbathDeployPath  = $cbathDeployScript
    CollectPath      = $collectScript
    Scenario         = $Scenario
    CombatHoldSec    = $CombatHoldSec
    PollTimeoutSec   = $PollTimeoutSec
    LaunchWaitSec    = $LaunchWaitSec
    McpPort          = $McpPort
    GameId           = $GameId
    OpponentDelaySec = $OpponentDelaySec
}

& $coordination -Owner $owner -ScriptPath $stageScript -ScriptParameters $toolArgs
exit $LASTEXITCODE
