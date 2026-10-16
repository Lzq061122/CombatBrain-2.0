param(
    [Parameter(Mandatory = $true)][string]$Cb2DeployPath,
    [Parameter(Mandatory = $true)][string]$CbathDeployPath,
    [Parameter(Mandatory = $true)][string]$CollectPath,
    [string]$Scenario = 'P',
    [int]$CombatHoldSec = 180,
    [int]$PollTimeoutSec = 240,
    [int]$LaunchWaitSec = 900,
    [int]$McpPort = 19580,
    [string]$GameId = 'bannerlord148-cb2',
    [int]$OpponentDelaySec = 6
)

$ErrorActionPreference = 'Stop'

# This script runs INSIDE the coordination lock. Its only job is to run the three stages in
# order and to report, honestly, how far it got. It never kills a game process, never removes
# the lock, never resets a shared switch and never deploys outside the lock: the lock and the
# resources belong to the coordinator, and a partially-run stage is a recorded fact, not
# something this script is entitled to clean up on its own.
#
# The stage that acquired the game is the one responsible for releasing it. So the stage list
# is walked in order, and each stage's completion is recorded before the next one starts. If a
# later stage fails, the record shows exactly which resources were acquired and which were not,
# and the failure is reported as a distinct, attributed failure rather than folded into a
# generic exception.

$stages = @(
    @{ name = 'deploy_cb2';   path = $Cb2DeployPath;   args = @{}; holds_game = $false }
    @{ name = 'deploy_cbath'; path = $CbathDeployPath; args = @{}; holds_game = $false }
    @{ name = 'collect';      path = $CollectPath;     args = @{
        Scenario         = $Scenario
        CombatHoldSec    = $CombatHoldSec
        PollTimeoutSec   = $PollTimeoutSec
        LaunchWaitSec    = $LaunchWaitSec
        McpPort          = $McpPort
        GameId           = $GameId
        OpponentDelaySec = $OpponentDelaySec
    }; holds_game = $true }
)

$completed = @()
$acquiredGame = $false
$gameStage = ''
$failure = $null
$failureStage = ''

foreach ($stage in $stages) {
    Write-Host ""
    Write-Host "=== stage: $($stage.name) ==="
    # Ownership of the game passes to this stage the moment it starts, not when it returns:
    # a collect stage that acquires the helper, starts the game and then fails has still
    # acquired the game, and its own end-state record is the authoritative account of what
    # it released. Recording acquisition only on success would report "game acquired: False"
    # for exactly the case this field exists to describe.
    if ($stage.holds_game) { $gameStage = $stage.name }
    try {
        if ($stage.args.Count -gt 0) {
            # Splatting only works on a variable: @($stage.args) would build a one-element
            # array and pass the whole hashtable as the first positional argument, silently
            # giving every real parameter its default value.
            $stageArgs = $stage.args
            & $stage.path @stageArgs
        } else {
            & $stage.path
        }
        $code = $LASTEXITCODE
        if ($null -eq $code) { $code = 0 }
        if ($code -ne 0) { throw "exit code $code" }
        $completed += $stage.name
        if ($stage.holds_game) { $acquiredGame = $true }
        Write-Host "stage $($stage.name): ok"
    } catch {
        $failure = $_.Exception.Message
        $failureStage = $stage.name
        break
    }
}

Write-Host ""
Write-Host "=== stage record ==="
Write-Host "completed:      $(if ($completed.Count -eq 0) { '<none>' } else { $completed -join ', ' })"
# Attempting and acquiring are different facts and are reported as such: entering the collect
# stage means this run took over the game lifecycle from that moment, while $acquiredGame only
# means the stage returned successfully. A collect stage that acquired the helper, started the
# game and then failed is an attempt with an unconfirmed result, and the collect stage's own
# end-state record is the authoritative account of what it actually released.
Write-Host "game stage attempted:  $(if ($gameStage.Length -eq 0) { '<none>' } else { $gameStage })"
Write-Host "game stage returned ok: $acquiredGame"
if ($failure) {
    Write-Host "failed stage:   $failureStage"
    Write-Host "failure:        $failure"
}

# The coordinator releases the lock and restores MBMOD_SHARED_GAME_PID in its own finally.
# A stage that failed before the collect stage never started a game, so there is nothing for
# this script to end; a failed collect stage is responsible for its own teardown and records
# it. Either way the exit code stays non-zero so the caller cannot read this as success.
if ($failure) {
    Write-Host "RESULT: FAILED at $failureStage"
    exit 1
}

Write-Host "RESULT: OK"
exit 0
