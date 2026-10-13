param(
    [Parameter(Mandatory = $true)][string]$Cb2DeployPath,
    [Parameter(Mandatory = $true)][string]$CbathDeployPath,
    [Parameter(Mandatory = $true)][string]$CollectPath,
    [string]$Scenario = 'A',
    [int]$CombatHoldSec = 180,
    [int]$PollTimeoutSec = 240,
    [int]$LaunchWaitSec = 900,
    [int]$McpPort = 19580,
    [string]$GameId = 'bannerlord148-cb2',
    [int]$ProbeHoldSec = 4,
    [bool]$DisableProbe = $false
)

$ErrorActionPreference = 'Stop'

# All three stages run under the coordination lock: deployment of both modules, then
# launch/collect/teardown. Deploying outside the lock would overwrite shared DLLs while
# another session may be using them.
Write-Host "=== stage 1: deploy CB2 ==="
& $Cb2DeployPath
if ($LASTEXITCODE -ne 0) { throw "CB2 deploy failed with exit code $LASTEXITCODE" }

Write-Host "=== stage 2: deploy CBATH ==="
& $CbathDeployPath
if ($LASTEXITCODE -ne 0) { throw "CBATH deploy failed with exit code $LASTEXITCODE" }

Write-Host "=== stage 3: collect ==="
$collectArgs = @{
    Scenario       = $Scenario
    CombatHoldSec  = $CombatHoldSec
    PollTimeoutSec = $PollTimeoutSec
    LaunchWaitSec  = $LaunchWaitSec
    McpPort        = $McpPort
    GameId         = $GameId
    ProbeHoldSec   = $ProbeHoldSec
}
if ($DisableProbe) { $collectArgs['DisableProbe'] = $true }

& $CollectPath @collectArgs
if ($LASTEXITCODE -ne 0) { throw "collection failed with exit code $LASTEXITCODE" }
