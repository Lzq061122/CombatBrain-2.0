param(
    [ValidateSet('P', 'N')]
    [string]$Config = 'P',
    [int]$CombatHoldSec = 150,
    [int]$PollTimeoutSec = 240,
    [int]$LaunchWaitSec = 900,
    [int]$McpPort = 19580,
    [string]$GameId = 'bannerlord148-cb2',
    [int]$ProbeHoldSec = 4,
    [switch]$DisableP1,
    [switch]$DisableProbe,
    [switch]$WhatIf
)

$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
$deployScript = Join-Path (Split-Path -Parent $here) '_deploy\deploy.ps1'
$collectScript = Join-Path $here 'run-p1.ps1'
$stageScript = Join-Path $here '_p1-exclusive-stage.ps1'

$coordination = 'E:\MBmod\BattleObservation-V1\coordination\Invoke-ExclusiveGame.ps1'
$owner = '主线组'

if (-not (Test-Path -LiteralPath $deployScript)) { throw "deploy script missing: $deployScript" }
if (-not (Test-Path -LiteralPath $collectScript)) { throw "collect script missing: $collectScript" }
if (-not (Test-Path -LiteralPath $stageScript)) { throw "stage script missing: $stageScript" }

if ($WhatIf) {
    Write-Host "=== run-p1-exclusive whatif ==="
    Write-Host "config:        $Config"
    Write-Host "coordination:  $coordination"
    Write-Host "owner:         $owner"
    Write-Host ""
    Write-Host "Both stages run INSIDE the coordination lock."
    Write-Host "stage 1 deploy : $deployScript"
    Write-Host "  regenerates build_id, clean rebuild, deploys DLL, compares SHA256"
    Write-Host "stage 2 collect: $collectScript"
    Write-Host "  -Config $Config -CombatHoldSec $CombatHoldSec -PollTimeoutSec $PollTimeoutSec"
    Write-Host "  -LaunchWaitSec $LaunchWaitSec -McpPort $McpPort -GameId $GameId -ProbeHoldSec $ProbeHoldSec"
    if ($DisableP1) { Write-Host "  -DisableP1" }
    if ($DisableProbe) { Write-Host "  -DisableProbe" }
    Write-Host ""
    Write-Host "This dry run writes no DLL, no config, no sentinel and starts no game."
    Write-Host "RESULT: WHATIF"
    exit 0
}

if (-not (Test-Path -LiteralPath $coordination)) { throw "coordination entry missing: $coordination" }

$toolArgs = @{
    DeployPath     = $deployScript
    CollectPath    = $collectScript
    Config         = $Config
    CombatHoldSec  = $CombatHoldSec
    PollTimeoutSec = $PollTimeoutSec
    LaunchWaitSec  = $LaunchWaitSec
    McpPort        = $McpPort
    GameId         = $GameId
    ProbeHoldSec   = $ProbeHoldSec
    DisableP1      = [bool]$DisableP1
    DisableProbe   = [bool]$DisableProbe
}

& $coordination -Owner $owner -ScriptPath $stageScript -ScriptParameters $toolArgs
exit $LASTEXITCODE
