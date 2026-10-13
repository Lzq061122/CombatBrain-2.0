$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$testBin = Join-Path $repo 'Program/tests/bin/Debug/net8.0'
foreach ($name in @('xunit.core.dll', 'xunit.assert.dll')) {
    $null = [Reflection.Assembly]::LoadFrom((Join-Path $testBin $name))
}
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $testBin 'CombatBrain2.Tests.dll'))
$flags = [Reflection.BindingFlags]'Instance,Static,Public,NonPublic'
function Get-Field($obj, [string]$name) {
    $obj.GetType().GetField($name, $flags).GetValue($obj)
}
function Invoke-Method($obj, [string]$name, [object[]]$arguments) {
    $obj.GetType().GetMethod($name, $flags).Invoke($obj, $arguments)
}
$testType = $assembly.GetType('CombatBrain2.Tests.CoordinationDriverTests', $true)
$outArguments = [object[]]@($null, $null)
$driver = $testType.GetMethod('BoundDriver', $flags).Invoke($null, $outArguments)
$plan = $outArguments[0]
$geometry = Get-Field $plan 'Geometry'
$at = $testType.GetMethods($flags) | Where-Object { $_.Name -eq 'At' -and $_.GetParameters().Count -eq 5 }
function Observe-Ready([double]$time) {
    $guard = Get-Field $driver 'GuardTask'
    $support = Get-Field $driver 'SupportTask'
    $g = $at.Invoke($null, [object[]]@($geometry, $guard, (Get-Field $geometry 'GuardX'), (Get-Field $geometry 'GuardY'), $true))
    $s = $at.Invoke($null, [object[]]@($geometry, $support, (Get-Field $geometry 'SupportX'), (Get-Field $geometry 'SupportY'), $false))
    Invoke-Method $driver 'Observe' ([object[]]@($time, $g, $s))
}
$null = Observe-Ready 200.0
$ready = Observe-Ready 201.0
$oldGuard = Get-Field $driver 'GuardTask'
$oldSupport = Get-Field $driver 'SupportTask'
$first = Invoke-Method $driver 'CreateAdvanceTask' ([object[]]@($plan, [double]201))
$result = [ordered]@{
    unwritten_dependencies = [ordered]@{
        guard_written_at = Get-Field $oldGuard 'WrittenAt'
        support_written_at = Get-Field $oldSupport 'WrittenAt'
        guard_feedback = (Get-Field $oldGuard 'Feedback').ToString()
        support_feedback = (Get-Field $oldSupport 'Feedback').ToString()
        advance_allowed = Get-Field $ready 'MainAdvanceAllowed'
        advance_created = $null -ne $first
    }
}
$replacement = $testType.GetMethod('Rec', $flags).Invoke($null,
    [object[]]@([int]0, [int]3, 'Cavalry', [int]12, [double]10, [double]10, $false))
$role = [Enum]::Parse($assembly.GetType('CombatBrain2.Core.FormationRole'), 'Guard')
$null = Invoke-Method $driver 'ReassignRole' ([object[]]@($plan, $oldGuard, $role, $replacement, [double]202))
$newGuard = Get-Field $driver 'GuardTask'
$newSupport = Get-Field $driver 'SupportTask'
$newMain = Get-Field $driver 'MainWaitTask'
$result.rebound_dependencies = [ordered]@{
    old_support_object = Get-Field $oldSupport 'ObjectIdentity'
    new_support_object = Get-Field $newSupport 'ObjectIdentity'
    old_main_dependency = Get-Field $newMain 'DependsOnTaskId'
    current_guard_id = Get-Field $newGuard 'TaskId'
    current_support_id = Get-Field $newSupport 'TaskId'
    guard_guards_task = Get-Field $newGuard 'GuardsTaskId'
    support_supports_task = Get-Field $newSupport 'SupportsTaskId'
    current_main_id = Get-Field $newMain 'TaskId'
}
$null = Observe-Ready 203.0
$null = Observe-Ready 204.0
$second = Invoke-Method $driver 'CreateAdvanceTask' ([object[]]@($plan, [double]204))
if ($null -eq $first -or $null -eq $second) { throw 'Probe did not reach both advance transitions' }
$firstId = Get-Field $first 'TaskId'
$secondId = Get-Field $second 'TaskId'
$gate = [Activator]::CreateInstance($assembly.GetType('CombatBrain2.Core.ExecutionGate'), $true)
$key = Get-Field $first 'FormationKey'
$objectId = Get-Field $first 'ObjectIdentity'
$reason = [Enum]::Parse($assembly.GetType('CombatBrain2.Core.CancelReason'), 'None')
$null = Invoke-Method $gate 'SubmitCandidate' ([object[]]@($firstId, $key, [double]201))
$null = Invoke-Method $gate 'MarkSelected' ([object[]]@($firstId, $key, [double]201.1))
$firstWrite = Invoke-Method $gate 'RequestWrite' ([object[]]@($firstId, $key, [double]201.2, $true, $true, $false, $objectId, $true, $true))
if (-not (Get-Field $firstWrite 'Allowed')) { throw 'First write not allowed' }
$null = Invoke-Method $gate 'SubmitCandidate' ([object[]]@($secondId, $key, [double]204))
$null = Invoke-Method $gate 'MarkSelected' ([object[]]@($secondId, $key, [double]204.1))
$secondWrite = Invoke-Method $gate 'RequestWrite' ([object[]]@($secondId, $key, [double]204.2, $true, $true, $false, $objectId, $true, $true))
$null = Invoke-Method $gate 'Close' ([object[]]@($firstId, $reason, [double]204.3))
$afterClose = Invoke-Method $gate 'RequestWrite' ([object[]]@($secondId, $key, [double]204.4, $true, $true, $false, $objectId, $true, $true))
$intent = (Get-Field $gate '_states')[$secondId]
$ledger = [Activator]::CreateInstance($assembly.GetType('CombatBrain2.Core.TaskLedger'), $true)
$null = Invoke-Method $ledger 'Add' ([object[]]@($first))
$null = Invoke-Method $ledger 'Add' ([object[]]@($second))
$result.second_advance_identity = [ordered]@{
    first_id = $firstId
    second_id = $secondId
    first_revision = Get-Field $first 'Revision'
    second_revision = Get-Field $second 'Revision'
    ids_equal = $firstId -eq $secondId
    first_write_allowed = Get-Field $firstWrite 'Allowed'
    second_write_allowed = Get-Field $secondWrite 'Allowed'
    second_write_reason = Get-Field $secondWrite 'BlockReason'
    after_old_identity_closed_reason = Get-Field $afterClose 'BlockReason'
    gate_still_closed_after_new_submission = Get-Field $intent 'Closed'
    first_submission_anchor_after_new_submission = Get-Field $intent 'SubmittedAt'
    old_lookup_now_returns_new_task = [object]::ReferenceEquals((Invoke-Method $ledger 'Get' ([object[]]@($firstId))), $second)
}
$json = $result | ConvertTo-Json -Depth 8
$json | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'core-probe-results.json') -Encoding utf8
$json
