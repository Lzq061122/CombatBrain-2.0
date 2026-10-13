param(
    [ValidateSet('A', 'B', 'P')]
    [string]$Scenario = 'A',
    [int]$CombatHoldSec = 180,
    [int]$PollTimeoutSec = 240,
    [int]$LaunchWaitSec = 900,
    [int]$McpPort = 19580,
    [string]$GameId = 'bannerlord148-cb2',
    [int]$ProbeHoldSec = 4,
    [switch]$DisableProbe,
    [switch]$WhatIf
)

$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
$cb2DeployScript = Join-Path (Split-Path -Parent $here) '_deploy\deploy.ps1'
$cbathDeployScript = 'E:\MBmod\CBATH-P2\RD\CombatBrain-Autonomous-Test-Harness\experiments\test-mod\CBATH\build-deploy.ps1'
$collectScript = Join-Path $here 'run-p2.ps1'
$stageScript = Join-Path $here '_p2-exclusive-stage.ps1'

$coordination = 'E:\MBmod\BattleObservation-V1\coordination\Invoke-ExclusiveGame.ps1'
# Owner must stay ASCII: PS 5.1 reads a BOM-less .ps1 as ANSI, which would corrupt a
# non-ASCII literal and break parsing. The coordinator compares this value as an opaque string.
$owner = [System.Text.Encoding]::UTF8.GetString([byte[]]@(0xE4,0xB8,0xBB,0xE7,0xBA,0xBF,0xE7,0xBB,0x84))

if (-not (Test-Path -LiteralPath $cb2DeployScript)) { throw "CB2 deploy script missing: $cb2DeployScript" }
if (-not (Test-Path -LiteralPath $cbathDeployScript)) { throw "CBATH deploy script missing: $cbathDeployScript" }
if (-not (Test-Path -LiteralPath $collectScript)) { throw "collect script missing: $collectScript" }
if (-not (Test-Path -LiteralPath $stageScript)) { throw "stage script missing: $stageScript" }

if ($WhatIf) {
    Write-Host "=== run-p2-exclusive whatif ==="
    Write-Host "scenario:      $Scenario"
    Write-Host "coordination:  $coordination"
    Write-Host "owner:         $owner"
    Write-Host ""
    Write-Host "All three stages run INSIDE the coordination lock:"
    Write-Host "stage 1 deploy CB2   : $cb2DeployScript"
    Write-Host "  regenerates build_id, clean rebuild, deploys DLL, compares SHA256"
    Write-Host "stage 2 deploy CBATH : $cbathDeployScript"
    Write-Host "  regenerates CBATH build_id, clean rebuild, deploys harness, compares SHA256"
    Write-Host "stage 3 collect      : $collectScript"
    Write-Host "  -Scenario $Scenario -CombatHoldSec $CombatHoldSec -PollTimeoutSec $PollTimeoutSec"
    Write-Host "  -LaunchWaitSec $LaunchWaitSec -McpPort $McpPort -GameId $GameId -ProbeHoldSec $ProbeHoldSec"
    if ($DisableProbe) { Write-Host "  -DisableProbe" }
    Write-Host ""
    Write-Host "This dry run writes no DLL, no config, no sentinel and starts no game."
    Write-Host "RESULT: WHATIF"
    exit 0
}

if (-not (Test-Path -LiteralPath $coordination)) { throw "coordination entry missing: $coordination" }

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
    ProbeHoldSec     = $ProbeHoldSec
    DisableProbe     = [bool]$DisableProbe
}

& $coordination -Owner $owner -ScriptPath $stageScript -ScriptParameters $toolArgs
exit $LASTEXITCODE
