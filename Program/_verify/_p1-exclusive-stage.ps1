param(
    [Parameter(Mandatory = $true)][string]$DeployPath,
    [Parameter(Mandatory = $true)][string]$CollectPath,
    [string]$Config = 'P',
    [int]$CombatHoldSec = 150,
    [int]$PollTimeoutSec = 240,
    [int]$LaunchWaitSec = 900,
    [int]$McpPort = 19580,
    [string]$GameId = 'bannerlord148-cb2',
    [int]$ProbeHoldSec = 4,
    [bool]$DisableP1 = $false,
    [bool]$DisableProbe = $false
)

$ErrorActionPreference = 'Stop'

Write-Host "=== stage 1: deploy ==="
& $DeployPath
if ($LASTEXITCODE -ne 0) { throw "deploy failed with exit code $LASTEXITCODE" }

Write-Host "=== stage 2: collect ==="
$collectArgs = @{
    Config         = $Config
    CombatHoldSec  = $CombatHoldSec
    PollTimeoutSec = $PollTimeoutSec
    LaunchWaitSec  = $LaunchWaitSec
    McpPort        = $McpPort
    GameId         = $GameId
    ProbeHoldSec   = $ProbeHoldSec
}
if ($DisableP1) { $collectArgs['DisableP1'] = $true }
if ($DisableProbe) { $collectArgs['DisableProbe'] = $true }

& $CollectPath @collectArgs
if ($LASTEXITCODE -ne 0) { throw "collection failed with exit code $LASTEXITCODE" }
