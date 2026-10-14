$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $root 'Program/tests/bin/Debug/net8.0/CombatBrain2.Tests.dll'))
$flags = [Reflection.BindingFlags]'Instance,Static,Public,NonPublic'
$type = $assembly.GetType('CombatBrain2.Core.ThreadHandoff', $true)
$handoff = [Activator]::CreateInstance($type, $true)
function InvokeMember($name, [object[]]$arguments) {
    $type.GetMethod($name, $flags).Invoke($handoff, $arguments)
}
function ReadField($obj, $name) {
    $obj.GetType().GetField($name, $flags).GetValue($obj)
}
function AssertFact($condition, $message) {
    if (-not $condition) { throw $message }
}
$registered = InvokeMember 'TryDefer' ([object[]]@('mission', 'plan', 'task', '1/2', 'object', [int]12, [double]35.178))
AssertFact $registered 'First defer failed'
$first = InvokeMember 'TakeAllPending' ([object[]]@())
$command = $first[0]
for ($i = 0; $i -lt 290; $i++) {
    $null = InvokeMember 'Requeue' ([object[]]@($command))
    $next = InvokeMember 'TakeAllPending' ([object[]]@())
    AssertFact ($next.Count -eq 1) 'Pending command was lost'
    AssertFact ([object]::ReferenceEquals($command, $next[0])) 'Command identity was replaced'
    AssertFact ((ReadField $next[0] 'MissionTime') -eq 35.178) 'First callback time was refreshed'
    AssertFact ((ReadField $next[0] 'TaskId') -eq 'task') 'Task identity was replaced'
}
$result = [ordered]@{
    iterations = 290
    same_pending_object = $true
    same_callback_time = 35.178
    requeue_count = $type.GetProperty('RequeueCount', $flags).GetValue($handoff)
    scope = 'Core handoff only; not a simulated Host or concurrent-slot proof'
}
$result | ConvertTo-Json
