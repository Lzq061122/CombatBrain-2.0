$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$bin = Join-Path $root 'Program/tests/bin/Debug/net8.0'
foreach ($name in @('xunit.core.dll','xunit.assert.dll')) { $null=[Reflection.Assembly]::LoadFrom((Join-Path $bin $name)) }
$a=[Reflection.Assembly]::LoadFrom((Join-Path $bin 'CombatBrain2.Tests.dll'))
$flags=[Reflection.BindingFlags]'Instance,Static,Public,NonPublic'
function Field($o,$n) { $o.GetType().GetField($n,$flags).GetValue($o) }
function Call($o,$n,[object[]]$args_) { $o.GetType().GetMethod($n,$flags).Invoke($o,$args_) }
$test=$a.GetType('CombatBrain2.Tests.CoordinationDriverTests',$true)
$args_=[object[]]@($null,$null)
$d=$test.GetMethod('BoundDriver',$flags).Invoke($null,$args_)
$p=$args_[0]
$g=Field $p 'Geometry'
$at=$test.GetMethods($flags) | Where-Object { $_.Name -eq 'At' -and $_.GetParameters().Count -eq 5 }
function Ready([double]$t) {
    $gt=Field $d 'GuardTask'; $st=Field $d 'SupportTask'
    $go=$at.Invoke($null,[object[]]@($g,$gt,(Field $g 'GuardX'),(Field $g 'GuardY'),$true))
    $so=$at.Invoke($null,[object[]]@($g,$st,(Field $g 'SupportX'),(Field $g 'SupportY'),$false))
    Call $d 'Observe' ([object[]]@($t,$go,$so))
}
$null=Ready 200
$o=Ready 201
$unwritten=Call $d 'CreateAdvanceTask' ([object[]]@($p,[double]201))
$result=[ordered]@{unwritten_allowed=(Field $o 'MainAdvanceAllowed'); unwritten_advance_created=($null -ne $unwritten)}
$null=$test.GetMethod('CommandsInEffect',$flags).Invoke($null,[object[]]@($p,$d,[double]200))
$guard=Field $d 'GuardTask'
$null=Call $guard 'SetOrderTarget' ([object[]]@([double]((Field $g 'GuardX')+20),[double](Field $g 'GuardY')))
$changed=Ready 201.2
$result.changed_readback_error=Call $guard 'OrderTargetReadbackError' ([object[]]@())
$result.changed_readback_still_allows_advance=Field $changed 'MainAdvanceAllowed'
$null=$test.GetMethod('CommandsInEffect',$flags).Invoke($null,[object[]]@($p,$d,[double]200))
$first=Call $d 'CreateAdvanceTask' ([object[]]@($p,[double]201.3))
if ($null -eq $first) { throw 'Valid first advance not created' }
$oldGuard=Field $d 'GuardTask'
$rec=$test.GetMethod('Rec',$flags).Invoke($null,[object[]]@([int]0,[int]3,'Cavalry',[int]12,[double]10,[double]10,$false))
$role=[Enum]::Parse($a.GetType('CombatBrain2.Core.FormationRole'),'Guard')
$null=Call $d 'ReassignRole' ([object[]]@($p,$oldGuard,$role,$rec,[double]202))
$main=Field $d 'MainWaitTask'; $guard=Field $d 'GuardTask'; $support=Field $d 'SupportTask'
$result.guard_dependency_matches_new_main=(Field $guard 'GuardsTaskId') -eq (Field $main 'TaskId')
$result.support_dependency_matches_new_main=(Field $support 'SupportsTaskId') -eq (Field $main 'TaskId')
$null=$test.GetMethod('CommandsInEffect',$flags).Invoke($null,[object[]]@($p,$d,[double]202))
$null=Ready 203
$null=Ready 204
$second=Call $d 'CreateAdvanceTask' ([object[]]@($p,[double]204))
if ($null -eq $second) { throw 'Valid second advance not created' }
$result.first_advance_id=Field $first 'TaskId'
$result.second_advance_id=Field $second 'TaskId'
$result.advance_ids_distinct=$result.first_advance_id -ne $result.second_advance_id
$json=$result | ConvertTo-Json
$json | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'probe-results.json') -Encoding utf8
$json
