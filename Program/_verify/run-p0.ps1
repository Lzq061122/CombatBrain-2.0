param(
    [string]$OutDir = '',
    [int]$CombatHoldSec = 150,
    [int]$PollTimeoutSec = 240,
    [int]$LaunchWaitSec = 900,
    [int]$McpPort = 19580,
    [string]$GameId = 'bannerlord148-cb2'
)

$ErrorActionPreference = 'Stop'

$here = $PSScriptRoot
$gameDir = 'E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord'
$cbathTools = 'E:\MBmod\CombatBrain Autonomous test harness\tools'
$engineConfig = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'Mount and Blade II Bannerlord\Configs\engine_config.txt'
$cb2Log = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'CombatBrain2\CombatBrain2.log'
$cbathOut = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'CommandBrainATH'

if ([string]::IsNullOrEmpty($OutDir)) {
    $stamp = (Get-Date).ToString('yyyyMMdd-HHmmss')
    $OutDir = Join-Path $here "sessions\$stamp"
}
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
Write-Host "out_dir: $OutDir"

$script:Failures = @()
function Fail($msg) { $script:Failures += $msg; Write-Host "FAIL: $msg" }

# 游戏崩溃时引擎只在 crashes\<时间戳>\ 留 dump 与 watchdog 日志，不回写 rgl 日志；
# 不查这里会把崩溃误读成"卡住"。
$crashRoot = 'C:\ProgramData\Mount and Blade II Bannerlord\crashes'
$script:CrashDirsBefore = @(Get-ChildItem $crashRoot -Directory -ErrorAction SilentlyContinue | ForEach-Object { $_.Name })
function Report-NewCrashes {
    $now = @(Get-ChildItem $crashRoot -Directory -ErrorAction SilentlyContinue | ForEach-Object { $_.Name })
    $new = @($now | Where-Object { $script:CrashDirsBefore -notcontains $_ })
    if ($new.Count -gt 0) {
        Fail "game crashed; new crash dirs: $($new -join ',')"
        foreach ($d in $new) {
            $dd = Join-Path $crashRoot $d
            $dst = Join-Path $OutDir "crash-$d"
            New-Item -ItemType Directory -Force -Path $dst | Out-Null
            Get-ChildItem $dd -File | Where-Object { $_.Name -ne 'dump.dmp' } | Copy-Item -Destination $dst -Force -ErrorAction SilentlyContinue
            Write-Host "crash evidence saved: $dst"
        }
    } else {
        Write-Host "no new crash dir"
    }
}

. (Join-Path $cbathTools 'mcp-http.ps1')

# CBATH 工具统一经 games_call_tool 转发。
function Invoke-Tool([string]$name, [int]$timeout = 30) {
    return Invoke-McpHttp -Tool 'games_call_tool' `
        -Arguments @{ gameId = $GameId; tool = $name; timeout = $timeout } `
        -TimeoutSec ($timeout + 40)
}

# 工具返回值是 JSON-RPC 信封，真正的工具结果在 result.structuredContent；
# 只能按字段取值，不能用正则扫整个响应 —— 响应里同时有 before/after 两套状态。
function Get-ToolObject($r) {
    try {
        $j = $r | ConvertFrom-Json
        if ($j.result -and $j.result.structuredContent) { return $j.result.structuredContent }
        if ($j.result -and $j.result.content -and $j.result.content[0].text) {
            return ($j.result.content[0].text | ConvertFrom-Json)
        }
    } catch { }
    return $null
}

# ---------- preflight ----------
Write-Host "=== preflight ==="

$alive = @(Get-Process -Name 'Bannerlord' -ErrorAction SilentlyContinue | Where-Object { -not $_.HasExited })
if ($alive.Count -gt 0) {
    Write-Host "REFUSED: Bannerlord already running (pid $(($alive | ForEach-Object { $_.Id }) -join ','))"
    exit 2
}

$steam = Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -eq 'steam' }
if (-not $steam) {
    $se = (Get-ItemProperty 'HKCU:\Software\Valve\Steam' -ErrorAction SilentlyContinue).SteamExe
    if (-not $se) { $se = 'C:\Program Files (x86)\Steam\steam.exe' }
    Write-Host "steam not running, starting: $se"
    Start-Process -FilePath $se -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 45
}

# safely_exited 必须为 1：为 0 会静默阻断所有 Mod 加载。
if (Test-Path $engineConfig) {
    $cfg = [System.IO.File]::ReadAllText($engineConfig)
    if ($cfg -match 'safely_exited\s*=\s*(\d+)') {
        if ($Matches[1] -ne '1') {
            $cfg = $cfg -replace 'safely_exited\s*=\s*\d+', 'safely_exited = 1'
            [System.IO.File]::WriteAllText($engineConfig, $cfg)
            Write-Host "preflight: safely_exited rewritten to 1"
        } else {
            Write-Host "preflight: safely_exited already 1"
        }
    }
} else {
    Write-Host "WARN: engine_config.txt not found at $engineConfig"
}

if (Test-Path $cb2Log) { Remove-Item $cb2Log -Force }
Write-Host "preflight: CombatBrain2 log cleared"

# 该目录是 CBATH 跨进程累积的产物（按 pid 命名），不清空会把历史场次混进本场证据。
# CBATH 权威脚本 zero-touch-3x.ps1 的 preflight 同样删除这个目录。
if (Test-Path $cbathOut) { Remove-Item $cbathOut -Recurse -Force }
Write-Host "preflight: CommandBrainATH cleared"

# ---------- launch ----------
Write-Host "=== launch ==="
$cfgDir = Join-Path $here 'gabs-config'
$gabsPid = Open-McpHttp -ConfigDir $cfgDir -ListenPort $McpPort
Write-Host "gabs_pid: $gabsPid"

try {
    $r = Invoke-McpHttp -Tool 'games_start' -Arguments @{ gameId = $GameId; timeout = 150 } -TimeoutSec $LaunchWaitSec
    Write-Host "games_start: $r"
    if ($r -match 'already_running') { Fail 'games_start reported already_running'; throw 'already_running' }

    # 这台安装装了大量 Mod，游戏启动加载可达数分钟（冷启动 shader cache 曾用 211 秒）。
    # GABP 桥要等游戏主程序加载完才开始监听，所以必须长时间轮询 connect。
    # 成功判据沿用 GABS 自己的措辞；gabpConnected 在 bridge 未应答时也可能为 false。
    $deadline = (Get-Date).AddSeconds($LaunchWaitSec)
    $connected = $false
    $attempt = 0
    while ((Get-Date) -lt $deadline) {
        if (-not (Get-Process -Name 'Bannerlord' -ErrorAction SilentlyContinue | Where-Object { -not $_.HasExited })) {
            Fail 'Bannerlord exited during launch'; throw 'died_during_launch'
        }
        $attempt++
        try {
            $r = Invoke-McpHttp -Tool 'games_connect' -Arguments @{ gameId = $GameId; timeout = 40; forceTakeover = $true } -TimeoutSec 90
            $short = if ($r.Length -gt 300) { $r.Substring(0, 300) } else { $r }
            Write-Host "connect attempt $attempt : $short"
            if ($r -match 'Discovered|Re-synced|Already connected|Force-took') {
                $connected = $true
                Write-Host "games_connect ok"
                break
            }
        } catch {
            Write-Host "connect attempt $attempt threw: $_"
        }
        Start-Sleep -Seconds 10
    }
    if (-not $connected) { Fail 'games_connect never succeeded'; throw 'no_connect' }

    $deadline = (Get-Date).AddSeconds($PollTimeoutSec)
    $pingOk = $false
    while ((Get-Date) -lt $deadline) {
        try {
            $r = Invoke-Tool 'cbath/ping' 20
            if ($r -match 'pong') { $pingOk = $true; Write-Host "ping ok"; break }
        } catch { }
        Start-Sleep -Seconds 3
    }
    if (-not $pingOk) { Fail 'cbath ping never answered'; throw 'no_ping' }

    $r = Invoke-Tool 'cbath/get_build_identity' 25
    [System.IO.File]::WriteAllText((Join-Path $OutDir 'build-identity.json'), $r)
    Write-Host "build_identity: $r"

    # 必须先等到主菜单（InitialState）：h3_provider_start 会推一个 CustomBattle state，
    # 只有从主菜单推才成立。游戏加载途中调用是未定义行为（CBATH 权威脚本 zero-touch-3x.ps1
    # L264-274 同样先等 InitialState）。
    $deadline = (Get-Date).AddSeconds($LaunchWaitSec)
    $atMenu = $false
    while ((Get-Date) -lt $deadline) {
        $r = Invoke-Tool 'cbath/get_game_state' 25
        if ($r -match 'InitialState') { $atMenu = $true; break }
        Start-Sleep -Seconds 3
    }
    if (-not $atMenu) { Fail 'game never reached the main menu (InitialState)'; throw 'no_menu' }
    Write-Host "at main menu (InitialState)"

    $r = Invoke-Tool 'cbath/h3_provider_start' 40
    [System.IO.File]::WriteAllText((Join-Path $OutDir 'provider-start.json'), $r)
    if ($r -notmatch '"status"\s*:\s*"ok"') { Fail "h3_provider_start not ok: $r" }
    Write-Host "h3_provider_start sent"

    $deadline = (Get-Date).AddSeconds($PollTimeoutSec)
    $atScreen = $false
    while ((Get-Date) -lt $deadline) {
        $r = Invoke-Tool 'cbath/get_game_state' 25
        if ($r -match 'CustomBattleScreen') { $atScreen = $true; break }
        Start-Sleep -Seconds 3
    }
    if (-not $atScreen) { Fail 'CustomBattleScreen never reached'; throw 'no_screen' }
    Write-Host "at CustomBattleScreen"

    $r = Invoke-Tool 'cbath/h3_start_from_screen' 40
    [System.IO.File]::WriteAllText((Join-Path $OutDir 'start-from-screen.json'), $r)
    Write-Host "h3_start_from_screen sent"

    $deadline = (Get-Date).AddSeconds($PollTimeoutSec)
    $running = $false
    while ((Get-Date) -lt $deadline) {
        $r = Invoke-Tool 'cbath/get_mission_state' 25
        if ($r -match 'mission_running.{0,3}:\s*true') { $running = $true; break }
        Start-Sleep -Seconds 3
    }
    if (-not $running) { Fail 'mission never started'; throw 'no_mission' }
    Write-Host "mission_running"

    # 部署闸门：判据必须是 auto_finish_deployment 自身的 status=ok（CBATH 权威脚本
    # zero-touch-3x.ps1:306-324 与 TCL driver-script.ps1:229-236 用的都是这个）。
    # not_ready 响应里也带 before.allow_ai_ticking（引擎接管前的初始值就是 true），
    # 用正则扫 allow_ai_ticking 会在第一次 not_ready 上假通过、FinishDeployment 永不执行。
    $deadline = (Get-Date).AddSeconds($PollTimeoutSec)
    $deployed = $false
    $lastStatus = ''
    while ((Get-Date) -lt $deadline) {
        $r = Invoke-Tool 'cbath/auto_finish_deployment' 30
        [System.IO.File]::WriteAllText((Join-Path $OutDir 'deployment.json'), $r)
        $o = Get-ToolObject $r
        if ($o) {
            $lastStatus = [string]$o.status
            if ($lastStatus -eq 'ok') {
                Write-Host "deployment finished: status=ok team_setup_over=$($o.after.team_setup_over) allow_ai_ticking_after=$($o.after.allow_ai_ticking) player_team_formations=$($o.after.player_team_formations)"
                if ($o.after.allow_ai_ticking -ne $true) { Fail 'deployment ok but allow_ai_ticking_after is not true' }
                $deployed = $true
                break
            }
            if ($lastStatus -ne 'not_ready') { Fail "auto_finish_deployment status=$lastStatus : $r"; break }
        } else {
            Fail "auto_finish_deployment returned unparsable payload: $r"
            break
        }
        Start-Sleep -Seconds 2
    }
    if (-not $deployed) {
        if ($lastStatus -eq 'not_ready' -or $lastStatus -eq '') { Fail "auto_finish_deployment never reached ok (last status=$lastStatus)" }
    }

    $r = Invoke-Tool 'cbath/delegate_team_to_ai' 30
    [System.IO.File]::WriteAllText((Join-Path $OutDir 'delegate.json'), $r)
    $d = Get-ToolObject $r
    if ($d) { Write-Host "delegate_team_to_ai: status=$($d.status) all_ai_controlled=$($d.all_ai_controlled)" } else { Write-Host "delegate_team_to_ai: $r" }
    if ($d -and $d.status -ne 'ok') { Fail "delegate_team_to_ai status=$($d.status)" }

    # CBATH 的 FormationProbe 是独立只读观测路径（不经过 2.0 代码），
    # 用来与 2.0 自己的快照做交叉对照。取战斗窗口前后两组。
    $r = Invoke-Tool 'cbath/get_all_formations' 30
    [System.IO.File]::WriteAllText((Join-Path $OutDir 'formations-early.json'), $r)
    Write-Host "formations-early captured"

    Write-Host "=== combat window: $CombatHoldSec s ==="
    $end = (Get-Date).AddSeconds($CombatHoldSec)
    while ((Get-Date) -lt $end) { Start-Sleep -Seconds 5 }

    $r = Invoke-Tool 'cbath/get_all_formations' 30
    [System.IO.File]::WriteAllText((Join-Path $OutDir 'formations-late.json'), $r)
    Write-Host "formations-late captured"

    $r = Invoke-Tool 'cbath/get_mission_state' 25
    [System.IO.File]::WriteAllText((Join-Path $OutDir 'mission-state-end.json'), $r)
    Write-Host "mission_state at end: $r"

    $r = Invoke-Tool 'cbath/get_last_run' 30
    [System.IO.File]::WriteAllText((Join-Path $OutDir 'last-run.json'), $r)
    Write-Host "get_last_run captured"
}
catch {
    Write-Host "EXCEPTION: $_"
    Fail "exception: $_"
    Report-NewCrashes
}
finally {
    Write-Host "=== teardown ==="
    try { Invoke-Tool 'cbath/end_mission' 40 | Out-Null; Write-Host "end_mission sent" } catch { Write-Host "end_mission failed: $_" }
    Start-Sleep -Seconds 5

    # 先把本模块日志副本抢下来，再关游戏。
    if (Test-Path $cb2Log) {
        Copy-Item $cb2Log (Join-Path $OutDir 'CombatBrain2.log') -Force
        Write-Host "saved CombatBrain2.log ($((Get-Item $cb2Log).Length) bytes)"
    } else {
        Fail 'CombatBrain2.log was never created - module did not load'
    }

    try { Invoke-McpHttp -Tool 'games_stop' -Arguments @{ gameId = $GameId } -TimeoutSec 180 | Out-Null; Write-Host "games_stop sent" } catch { Write-Host "games_stop failed: $_" }

    $deadline = (Get-Date).AddSeconds(30)
    while ((Get-Date) -lt $deadline) {
        $a = @(Get-Process -Name 'Bannerlord' -ErrorAction SilentlyContinue | Where-Object { -not $_.HasExited })
        if ($a.Count -eq 0) { break }
        Start-Sleep -Seconds 2
    }
    $remain = @(Get-Process -Name 'Bannerlord' -ErrorAction SilentlyContinue | Where-Object { -not $_.HasExited })
    if ($remain.Count -gt 0) {
        Write-Host "WARN: Bannerlord still alive, killing pid $(($remain | ForEach-Object { $_.Id }) -join ',')"
        $remain | Stop-Process -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 3
    }
    $remain = @(Get-Process -Name 'Bannerlord' -ErrorAction SilentlyContinue | Where-Object { -not $_.HasExited })
    if ($remain.Count -gt 0) { Fail "residual Bannerlord process: $(($remain | ForEach-Object { $_.Id }) -join ',')" }
    else { Write-Host "no residual Bannerlord process" }

    Close-McpHttp

    # 再抓一次日志（收尾阶段的记录也要在证据里）。
    if (Test-Path $cb2Log) { Copy-Item $cb2Log (Join-Path $OutDir 'CombatBrain2.log') -Force }

    Report-NewCrashes

    if (Test-Path $cbathOut) {
        $dst = Join-Path $OutDir 'CBATH'
        New-Item -ItemType Directory -Force -Path $dst | Out-Null
        Copy-Item (Join-Path $cbathOut '*') $dst -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "saved CBATH outputs"
    }
}

Write-Host "=== summary ==="
Write-Host "out_dir: $OutDir"
if ($script:Failures.Count -eq 0) {
    Write-Host "RESULT: OK"
    exit 0
}
Write-Host "RESULT: FAILURES ($($script:Failures.Count))"
$script:Failures | ForEach-Object { Write-Host "  - $_" }
exit 1
