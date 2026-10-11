# P0 Exit Gate 3 —— 控制权安全边沿：本场运行报告

> **Codex 2026-10-11 复核：场景 A/B 的生产否决、取消和撤权事实成立；Gate 3/P0 仍待独立失资格观测与玩家适用域补证。** had_pending=0 接受为本场时序；probe 实读不能替代写定的独立采样；“玩家接管不可达”原推断不予采纳。完整裁定见 `P0-EXIT-GATE3-CODEX-REVIEW.md`。启用文件已备份并移除，默认测试关闭态已恢复。

- Author Role：CombatBrain 2.0 Development Dispatcher / 实现方（本机会话执行）
- Reviewer：Codex（最终阶段结论由其复核落定）
- 目的：补齐 P0 Exit Gate 3，使 Movement 控制范围具备可复查的安全退出依据
- 任务来源：`E:/MBmod/TacticalCommandLab/_dispatch/2026-10-11-cb20-p0-stage-exit-control-safety.md`
- 工作区：`E:\MBmod\CombatBrain 2.0`，源码根 `Program\`
- 基线 HEAD：`0324426`；已验实现 `cbc9981`；既有决定性场次 run 7 = `20261011-002337`
- **本单决定性场次：`Program/_verify/sessions/20261011-133859/`**
- 写作纪律：每条判定都指向可直接核对的证物（日志行号 / 文件名 / tool 输出）。未取证的项写"未取证"，不给数字。

---

## 0. 交付物（三项）

| # | 物 | 位置 | 性质 |
|---|---|---|---|
| 1 | 场景判据（实现前写定） | `Program/_verify/P0-EXIT-GATE3-CONTROL-SAFETY-SCENARIOS.md`（14303 B） | Slot 0 |
| 2 | 本单运行报告 | 本文件 `Program/_verify/P0-EXIT-GATE3-RUN-REPORT.md` | Slot 2 |
| 3 | 原始材料归档（一次） | `E:\MBmod\CombatBrain2-evidence\20261011-gate3-sessions.zip`<br>size **8829130 B**，SHA256 **`35F99ECB26213FF23AA1719E055310CA8D5B2437C5579B19BF6FEDADA57F4A73`**（同名 `.sha256` 已写） | Slot 1 |

归档内容：`sessions/` 全部 17 个场次子树（含本单 `20261011-133859`）+ `_lastrun/`（`_lastrun*.txt` 副本）。已解压到临时目录核对后删除。**归档摘要不写在归档自身内部，只留在本文件与 `sessions/INDEX.md`。**

> 说明：`20261010-211555` 子目录体积大（35 files）是因为它含 `crash-2026-10-10_13.16.29\` 的 launcher/engine/module/thread/watchdog/minidump 素材，属 P0a 期间旧物，非本单新增。

---

## 1. 环境与身份

### 1.1 版本与日时锚点

| 项 | 值 | 证物 |
|---|---|---|
| 游戏版本 | `v1.4.8.119303` | `sessions/20261011-133859/build-identity.json` 的 `game_version`；`rgl_log_35032.txt` head `Build Version: 119303` |
| 日期/时刻 | 2026-10-11，mission 起于 `13:39:24.230`（WT） | `rgl_log_35032.txt` `[13:39:03.618] 11 Oct 2026 Sunday 13:39 中国标准时间`；`[13:39:24.230] [CB2][MISSION_START` |
| 计算机 / 用户 | `CHINAMI-DK8UOFK` / `Administrator` | `rgl_log_35032.txt` head |
| GPU | `NVIDIA GeForce RTX 5070 Ti`（adapter [0]） | `rgl_log_35032.txt` `Selected graphics adapter: [0] NVIDIA GeForce RTX 5070 Ti` |
| OS build / CPU 型号 / 线程数 | **未取证**（引擎日志 head 未含） | — |
| Mission 时长 | 战斗窗口参数 120 s；结束时 `mission_time = 118.987` | `sessions/20261011-133859/mission-state-end.json` |
| PID | **35032** | `build-identity.json` `pid=35032`；2.0 日志 L1 `pid=35032`；CBATH 产物 `cbath-gabp-35032.txt` |

### 1.2 加载集合三方对照（防止"少加载/多加载"谎报）

| 序 | `build-identity.json`（CBATH Id） | 2.0 日志 L1（显示名） | 引擎 `Command Args`（Id） |
|---|---|---|---|
| 0 | Bannerlord.Harmony | Harmony | Bannerlord.Harmony |
| 1 | Bannerlord.ButterLib | ButterLib | Bannerlord.ButterLib |
| 2 | Bannerlord.UIExtenderEx | UIExtenderEx | Bannerlord.UIExtenderEx |
| 3 | Bannerlord.MBOptionScreen | Mod Configuration Menu v5 | Bannerlord.MBOptionScreen |
| 4 | Native | Native | Native |
| 5 | SandBoxCore | SandBox Core | SandBoxCore |
| 6 | Sandbox | Sandbox | Sandbox |
| 7 | StoryMode | StoryMode | StoryMode |
| 8 | CustomBattle | CustomBattle | CustomBattle |
| 9 | CombatBrain2 | CombatBrain 2.0 (P0a) | CombatBrain2 |
| 10 | CombatBrainAutonomousTestHarness | CombatBrain Autonomous Test Harness | CombatBrainAutonomousTestHarness |

三方为同一份 11 模块集合，仅 Id / 显示名渲染差异，无缺无增。

### 1.3 本方 DLL 与 CBATH 身份

| Item | CombatBrain 2.0 | CBATH Harness |
|---|---|---|
| Id / 显示名 | `CombatBrain2` / `CombatBrain 2.0 (P0a)` | `CombatBrainAutonomousTestHarness` / `CombatBrain Autonomous Test Harness` |
| assembly_version | `0.0.0.0`（csproj 未设 `<Version>`） | `1.0.0.0` |
| path | `...\Modules\CombatBrain2\bin\Win64_Shipping_Client\CombatBrain2.dll` | 未取证 |
| size | 53248 B | 未取证 |
| sha256 | `4A7E9C1D05EFC0FE666E022789B11A99A149AF73491A579DFA83A9BEDCA918E8` | `93f6e154b6b92f38ce1ab642e64f15a7366cf7b93fdfc75ff0ee949c95dfe91b` |
| mvid | `9e538318-24bd-4148-9ce4-e9824cf83b23` | `e9f98865-06a7-47d8-ab7d-585cb445904a` |
| build_id | `b20261011-132044-a06f2e05` | `b20260925-013209-2b52d28b`（冻结基线） |
| 报告者 | 2.0 日志 L1 `RUNTIME_IDENTITY` | CBATH `runtime_identity` |
| 加载序 | idx 9 | idx 10 |

`deploy.ps1` 输出：`source_sha256 = deployed_sha256 = 4A7E9C1D...918E8`，与部署目录磁盘文件、以及日志 L1 自报的 `assembly_file_sha256=4a7e9c1d05efc0fe666e022789b11a99a149af73491a579dfa83a9bedca918e8` 三者一致。

**New vs Old（Codex 身份勘误）**：Old run 7 = `b20261011-002322-97c0ff2e`（47104 B，`27F8CBFA29AC1F13A848A61CDC2F761C7751517558CB294AD2DAA30113EF2416`）；New = 本单（53248 B，`4A7E9C1D...918E8`）。37376 B / `44845964…` 属于 run 6，不是 run 7。未做 byte-diff，不重启已收口的构建可复现性调查。

### 1.4 性能与稳定性数据

- **`rgl_log_35032.txt`（6270 行 / 424814 B）中 grep `Benchmark` 与 `FPS` 均 0 hit → 本场没有任何 Benchmark / FPS 数据，本报告不给任何帧率、帧时、GPU 利用率数字。**
- 崩溃 0：`run-p0.ps1` teardown 段 `Report-NewCrashes` 报 `no new crash dir`。
- 退出日志：`Pre Finalizing Managed Interface...OK!` / `Deleting Managed Interface...` / `Checking if any managed object still lives... There are no living managed objects. Managed Interface deleted.`；末行 `[13:41:37.997] Error: Non-Zero Device Reference Count! (ERC3456)`（资源计数 `#RT 156 / #DT 11 / #Data 19`）。此 ERC3456 与启动失败场次（pid 23808 / 6208，164 行、`#RT/#DT/#Data` 全 0、ERC302）**不是同一现象，不可混谈**。
- 日志中另出现 `Windows Free Memory: %34.954`、`Mount and Blade II Bannerlord Memory Usage: 2.617 Gb`（快照值，非本单关注项）。
- 本场无新截图：`Documents\Mount and Blade II Bannerlord\Screenshots` 共 52 个文件，最新为 `2026-08-27_16.06.37.png`。

### 1.5 时间基准

- **MT = Mission Time**（2.0 日志 `at=` 字段），本单一切判据按 MT。
- **WT = Wall Clock**（`rgl_log_35032.txt` 的 `[hh:mm:ss.mmm]` 前缀），仅用于交叉标定。

| Mark | WT | MT |
|---|---|---|
| RUNTIME_IDENTITY | 13:39:13.764 | (mission 前) |
| MISSION_START | 13:39:24.230 | 0.000 |
| TRIGGER_SCHEDULED (A) | 13:39:29.701 | 15.102 |
| INTENT_TRIGGERED intent-1 | 13:39:48.138 | 15.108 |
| REAL_SELECTION(1) / HANDOFF_DEFERRED(1) | 13:39:48.561 | 15.530 |
| AWAITING_NATIVE_APPLY(1) / PROBE_ELIGIBILITY_LOST(1) / PROBE_RESTORE_SCHEDULED(1) / HANDOFF_EXECUTED(1) | 13:39:48.568 | 15.537 |
| WRITE_BLOCKED(1) | 13:39:48.570 | 15.537 |
| INTENT_CANCELLED(1) | 13:39:48.574 | 15.545 |
| HANDOFF_CLEARED(1) / BEHAVIOR_DISARMED(1) / REVOKE_SKIPPED(1) / SEQUENCE_INTENT_A_CLOSED | 13:39:48.576-578 | 15.545 |
| PROBE_ELIGIBILITY_RESTORED(1) | 13:39:52.575 | 19.542 |
| INTENT_TRIGGERED intent-2 | 13:39:53.583 | 20.549 |
| REAL_SELECTION(2) / HANDOFF_DEFERRED(2) | 13:39:54.064 | 21.029 |
| HANDOFF_EXECUTED(2) / WRITE_EXECUTED / EXPLICIT_CLOSE_SCHEDULED | 13:39:54.072 | 21.037 |
| PROBE_ELIGIBILITY_LOST(2) / PROBE_RESTORE_SCHEDULED(2) | 13:39:54.078 | 21.044 |
| INTENT_CANCELLED(2) / HANDOFF_CLEARED(2) / BEHAVIOR_DISARMED(2) / REVOKE_SKIPPED(2) / SEQUENCE_INTENT_B_CLOSED | 13:39:54.086-087 | 21.052 |
| PROBE_ELIGIBILITY_RESTORED(2) | 13:39:58.086 | 25.050 |
| MISSION_CLEANUP | 13:41:32.166 | (mission 后清理) |

WT 与 MT 并非 1:1（`MISSION_START` WT 13:39:24.230 对应 MT 0.000，而 5.471 s 后 MT 已到 15.102），**不可互推**。

---

## 2. 控制域与范围冻结

本单控制域与 P0a run 7 完全一致，**未放宽**：

- 只控制 **非玩家队、非玩家拥有、AI 受控、非空步兵 Formation**，经 `CbHoldBehavior : BehaviorComponent` + `FormationAI.AddAiBehavior` + `SetBehaviorWeight` 进原生行为选择链（Route C）。
- 不申请 Formation 的 Full 控制、不建租约、不把玩家队加入可执行目标。
- `TargetSelector` 过滤：`UnitCount<=0`→`:empty`、`IsPlayerTeam || IsPlayerControlled`→`:player_owned`、`!IsAiControlled`→`:not_ai_controlled`、非 Infantry→`:not_infantry:`、`IsStale`→`:snapshot_stale`。
- 时间参数保持：快照上限 2.0 s、意图窗口 20.0 s、获选等待 10.0 s；截止未延长、无无限重试、未重绑另一对象续用旧意图。

**关于"原生玩家接管"是否可达（派单 §2 要求查清）**：

- 引擎层唯一授予玩家拥有的公开入口是 `Formation.PlayerOwner` setter（反编译 `Formation.cs:271-281`：`set { _playerOwner = value; SetControlledByAI(value == null); }`），它直接联动同一个状态位 `IsAIControlled`。
- `PlayerOwner` 赋值会联动 AI 控制状态，但两者不是同义状态：本场直接关闭 AI 后 PlayerOwner 仍为 null，不能由此声明完整玩家接管事件链已覆盖。
- 本单**没有伪造 `PlayerOwner`、没有裸反射写私有字段**；实机触发用公开原生 API `Formation.SetControlledByAI(false)` / `(true)`（反编译 `Formation.cs:788-817`）。
- 结论：**本单所有触发一律标为"测试接口触发的资格变化"，不标为 F1/F6 玩家接管，不宣称玩家接管事件链已通过。**

### 2.1 玩家接管适用性：当前尚未闭合的判断

1. 控制域过滤：`TargetSelector` 硬性排除 `IsPlayerTeam || IsPlayerControlled`，且 `!IsAiControlled` 亦排除；`ExecutionGate.RequestWrite` 再用 `player_owned` / `not_ai_controlled` 兜底。
2. 引擎侧耦合：PlayerOwner setter 先写独立的 `_playerOwner` 字段，再调用 SetControlledByAI；直接关闭 AI 并未设置 PlayerOwner，不能作等价推断。
3. 初始过滤不能证明对象后续永不成为玩家队。Codex 静态复核确认公开原生 Mission.JoinEnemyTeam 能改变 PlayerTeam；该路径的本场可达性没有实机取证，原“永久不可达”结论撤回。
4. CBATH 缺工具不是不适用依据。仍需提交当前控制域排除合法玩家控制变化的完整依据，或补对应真实原生控制路径的安全证据；本场测试资格切换不能直接替代。
5. 补充未验证项：目标在 selected 后由本方行为持有，真实 UI 玩家接管能否在本方持有期间合法发生，本单**未取到反证也未取证**，登记为未验证（见 §6）。

---

## 3. 本单改了什么（文件级）

### 3.1 变更清单

| # | 文件 | 性质 | 内容 |
|---|---|---|---|
| 1 | `Program/src/Host/EligibilityProbe.cs` | **新增**（245 行） | 测试适配，默认关闭。哨兵 `%USERPROFILE%\Documents\CombatBrain2\eligibility-probe.txt`；文件不存在则全程 return，零副作用。按 `intents=A,B` 给第 N 个意图分配模式。场景 A 触发条件：`SelectedAt>=0 && ExecutedAt<0 && Handoff.HasPending`；场景 B：`ExecutedAt>=0`。失资格保持 4.0 s 后逐 tick 单独遍历恢复（不依赖意图仍开放）。恢复只调 `SetControlledByAI(true)` 撤回自己的一次改动，不重发命令、不 `ResetBehaviorWeights`。 |
| 2 | `Program/tests/EligibilityLossTests.cs` | **新增** | 5 例：`CancelReasonHasExplicitAiControlLost`、`ClearingPendingKeepsMainThreadRegistration`、`AlreadyWrittenIntentNeverWritesAgainAfterEligibilityLoss`、`WriteAttemptsAreCountedPerIntent`、`UnwrittenIntentStaysAtZeroWriteAttempts` |
| 3 | `Program/src/Core/TestParameters.cs` | 修改 | `CancelReason` 增 `AiControlLost`（插在 `PlayerEligibilityLost` 与 `MissionEnded` 之间） |
| 4 | `Program/src/Core/ExecutionGate.cs` | 修改 | `IntentState` 增 `internal int WriteAttempts;`；`RequestWrite` 放行分支增 `s.WriteAttempts++;`；新增 `internal int WriteAttemptsFor(string intentId)` |
| 5 | `Program/src/Host/TestDriver.cs` | 修改（主体 +87/-9） | 见 3.2 |
| 6 | `Program/src/Host/SamplingBehavior.cs` | 修改（+3） | `TestDriver.Instance.Tick(mission)` 与 `DrainPendingCommand(mission)` 之间插入 `EligibilityProbe.Instance.OnMissionTick(mission);`（生产文件唯一新增调用点） |
| 7 | `Program/src/Host/BuildIdentity.g.cs` | 修改 | build_id → `b20261011-132044-a06f2e05`（`deploy.ps1` 自动重写） |
| 8 | `Program/CombatBrain2.csproj` | 修改（+1） | `<Compile Include="src\Host\EligibilityProbe.cs" />` |
| 9 | `Program/tests/CombatBrain2.Tests.csproj` | 修改（+1） | `<Compile Include="EligibilityLossTests.cs" />` |

**删除：0 个文件、0 个既有测试、0 条既有生产判断分支。** `git diff --stat`：7 files changed, 96 insertions(+), 9 deletions(-)。

### 3.2 `TestDriver.cs` 六项

1. **~L37-39**：新增只读访问器 `internal ThreadHandoff Handoff { get { return _handoff; } }`（只给 probe 判"真实待转交已存在"，不改写入路径、不碰锁内 setter）。
2. **~L284-290（`Tick` 绑定目标检查段）**：在 `likelyPlayerOwned(bound)` 之后新增
   ```csharp
   // 控制资格丢失（IsAIControlled=false）：Route C 的前提不再成立。
   // 只在此处取消还不够，取消路径必须同时撤权并让待写命令失效。
   if (!bound.IsAiControlled) { CancelIntent(CancelReason.AiControlLost, now); return; }
   ```
   `_snapshot` 在本 tick 开头刚采样，是当 tick 真实值。
3. **~L600-617（`DrainPendingCommand`）**：原 `if (_intent == null || _intent.IsClosed) return;` 改为先记 `HANDOFF_DROPPED intent=... key=... reason=intent_closed callback_tid=... exec_tid=... at=...` 再 return。
4. **~L776-803（`CancelIntent`）**：`INTENT_CANCELLED` 之后新增 `int hadPending = 0; try { if (_handoff.HasPending) hadPending = 1; _handoff.Clear(); } catch { }`，再记 `HANDOFF_CLEARED intent=... reason=... had_pending=... at=...`。顺序为**先清槽位，再 DisarmAllBehaviors / RevokeIfNeeded**。
5. **~L146-189（`DisarmAllBehaviors`）**：循环体改 `var b = pair.Value; if (b == null) continue; var f = b.Formation;`；实读 `weightBefore = b.WeightFactor`（Disarm 前）、调 `b.Disarm()` + `f.AI.SetBehaviorWeight<CbHoldBehavior>(0f)`、回读 `weightAfter` 与 `active_behavior_now`；`BEHAVIOR_DISARMED` 字段由写死 `weight=0` 改为 `armed_before= weight_before= weight_after= active_behavior_now= error= at=`。
6. **四处日志 + 两个新访问器**：`CANDIDATE_SUBMITTED` / `WRITE_BLOCKED` / `WRITE_EXECUTED` / `INTENT_CANCELLED` 的 `write_attempts=` 改为 `write_attempts_total=<全局> write_attempts_intent=<该意图>`（派单 §3 要求按 intent id 核对）；新增 `internal double SafeNow(Mission)` 与 `internal Formation FindFormationByKey(Mission, string)`。

### 3.3 刻意未动之处

- 真实获选判据（`f.AI.OnActiveBehaviorChanged` + `ReferenceEquals(f.AI.ActiveBehavior, ours)`）；
- 主线程执行点（`DrainPendingCommand` 在 Mission tick 内执行）；
- `ThreadHandoff._sync` 单槽位锁（一把锁保护投递/取出/清空），锁内不执行任何游戏 setter；
- 时间参数、Intent 阶段机、`TargetSelector` 过滤条件。

### 3.4 单元测试

`dotnet test tests/CombatBrain2.Tests.csproj --nologo -v q` → **失败 0，通过 65，跳过 0，总计 65**（60 例继承 + 5 例本单新增）。

> 范围声明：tests 工程只链 `src/Core` 的 7 个文件，**Host 侧 `TestDriver` / `EligibilityProbe` 改动不在单测覆盖内**，只能由实机判定。Core 绿灯不能替代实机事实。

---

## 4. 实机场次 20261011-133859 的逐条证据

日志文件：`sessions/20261011-133859/CombatBrain2.log`，448 行 / 75518 B。

### 4.1 marker 全量计数（判据底稿）

| marker | count |
|---|---|
| INDEPENDENT_SAMPLE | 119 |
| SNAPSHOT_HEALTH | 119 |
| POST_OBSERVE | 51 |
| BEHAVIOR_REGISTERED | 26 |
| CANDIDATE_SUBMITTED | 26 |
| SELECTION_CANDIDATE | 20 |
| SELECTION_REJECTED | 18 |
| SNAPSHOT_FORMATION | 28 |
| REAL_SELECTION | 2 |
| INTENT_TRIGGERED | 2 |
| INTENT_CANCELLED | 2 |
| REVOKE_SKIPPED | 2 |
| AWAITING_NATIVE_APPLY | 2 |
| BEHAVIOR_DISARMED | 2 |
| HANDOFF_CLEARED | 2 |
| HANDOFF_EXECUTED | 2 |
| HANDOFF_DEFERRED | 2 |
| ACTIVE_BEHAVIOR_CHANGED | 2 |
| PROBE_ELIGIBILITY_LOST | 2 |
| PROBE_RESTORE_SCHEDULED | 2 |
| PROBE_ELIGIBILITY_RESTORED | 2 |
| SELECTION_CANDIDATES | 2 |
| SEQUENCE_INTENT_A_CLOSED | 1 |
| SEQUENCE_INTENT_B_CLOSED | 1 |
| EXPLICIT_CLOSE_SCHEDULED | 1 |
| **WRITE_EXECUTED** | **1** |
| **WRITE_BLOCKED** | **1** |
| RUNTIME_IDENTITY / MISSION_START / MAIN_THREAD_MARKED / BEHAVIOR_START / PROBE_CONFIG / HOOK_FORMATIONS / TRIGGER_SCHEDULED / MISSION_CLEANUP | 各 1 |
| **NATIVE_MOVEMENT_APPLIED** | **0** |
| **HANDOFF_SLOT_BUSY** | **0** |
| **HANDOFF_DROPPED** | **0** |

### 4.2 载荷身份

日志 L1：`RUNTIME_IDENTITY module=CombatBrain2 build_id=b20261011-132044-a06f2e05 assembly_version=0.0.0.0 mvid=9e538318-24bd-4148-9ce4-e9824cf83b23 assembly_location=...\CombatBrain2.dll assembly_file_sha256=4a7e9c1d05efc0fe666e022789b11a99a149af73491a579dfa83a9bedca918e8 game_version=v1.4.8.0 pid=35032 active_modules=Harmony,ButterLib,UIExtenderEx,Mod Configuration Menu v5,Native,SandBox Core,Sandbox,StoryMode,CustomBattle,CombatBrain 2.0 (P0a),CombatBrain Autonomous Test Harness`

其余环境行：L2 `MISSION_START mission_key=battle_terrain_biome_096#37428684`；L3 `MAIN_THREAD_MARKED tid=30 source=mission_tick`；L4 `BEHAVIOR_START sampling_behavior_initialized tid=30`；L7 `PROBE_CONFIG modes=a,b source=eligibility-probe.txt note=test-adapter_triggered_eligibility_change`；L8 `HOOK_FORMATIONS count=16`；L9 `TRIGGER_SCHEDULED intent=A at=15.102 deployment_finished=1 allow_ai_ticking=1`。

### 4.3 场景 A（intent-1）—— 待写命令在资格变化后不得落地

目标对象：`key=1/0 obj=024fb8dd@Infantry`（Attacker 队 Infantry slot 0；`formations-early.json` 中 unit_count=20、`is_ai_controlled=true`、`movement_order=Move`）。

```
L38  INTENT_TRIGGERED intent=intent-1 at=15.108 deadline=35.108 window=20.000 selection_wait=10.000
L84  CANDIDATE_SUBMITTED intent=intent-1 key=1/0 obj=024fb8dd@Infantry order_before=Move behavior_registered=1 behavior_weight=8.000 at=15.522 write_attempts_total=0 write_attempts_intent=0
L85  ACTIVE_BEHAVIOR_CHANGED intent=intent-1 key=1/0 active_behavior=CbHoldBehavior ours=1 tid=45
L86  REAL_SELECTION intent=intent-1 key=1/0 side=Attacker obj=024fb8dd@Infantry at=15.530
L87  HANDOFF_DEFERRED intent=intent-1 key=1/0 callback_tid=45 main_tid=30 at=15.530
L88  AWAITING_NATIVE_APPLY intent=intent-1 ... armed_behavior=1 active_behavior_now=CbHoldBehavior at=15.537
L89  PROBE_ELIGIBILITY_LOST scenario=scenario_a intent=intent-1 mode=a key=1/0 before[obj=024fb8dd@Infantry ai_ctrl=1 player_owner=0 player_team=0 units=20 order=Move active_behavior=CbHoldBehavior behavior_count=25 at=15.537] after[obj=024fb8dd@Infantry ai_ctrl=0 player_owner=0 player_team=0 units=20 order=Move active_behavior=CbHoldBehavior behavior_count=25 at=15.537] applied=1 error=<none> at=15.537
L90  PROBE_RESTORE_SCHEDULED intent=intent-1 at=19.537 intent_deadline=35.108 before_deadline=1
L91  HANDOFF_EXECUTED intent=intent-1 ... callback_tid=45 exec_tid=30 is_main_thread=1 at=15.537
L92  WRITE_BLOCKED intent=intent-1 key=1/0 reason=not_ai_controlled at=15.537 write_attempts_total=0 write_attempts_intent=0
L93  INTENT_CANCELLED intent=intent-1 reason=AiControlLost at=15.545 write_attempts_total=0 write_attempts_intent=0
L94  HANDOFF_CLEARED intent=intent-1 reason=AiControlLost had_pending=0 at=15.545
L95  BEHAVIOR_DISARMED key=1/0 reason=AiControlLost armed_before=1 weight_before=8.000 weight_after=0.000 active_behavior_now=CbHoldBehavior error=<none> at=15.545
L96  REVOKE_SKIPPED intent=intent-1 reason=no_own_write
L97  SEQUENCE_INTENT_A_CLOSED at=15.545 next_intent_at=20.545
L108 PROBE_ELIGIBILITY_RESTORED source_intent=intent-1 mode=a key=1/0 before[ai_ctrl=0 active_behavior=CbHoldBehavior] after[ai_ctrl=1 active_behavior=BehaviorCautiousAdvance] applied=1 error=<none> at=19.542 intent_open_now=<none> same_intent_open=0 write_attempts_total=0 write_attempts_source_intent=0 note=any_order_change_here_is_native_SetControlledByAI_side_effect
```

满足的判据：

1. **真实获选**：`ACTIVE_BEHAVIOR_CHANGED ... ours=1`（非 `MarkSelected` 伪造）。
2. **真实待转交**：`HANDOFF_DEFERRED callback_tid=45 main_tid=30`（AI 线程 45 投递，主线程 30 消费）。
3. **真实资格变化**：`PROBE_ELIGIBILITY_LOST ... before[ai_ctrl=1] after[ai_ctrl=0] applied=1 error=<none>`，对象是同一实际 Formation（`obj=024fb8dd@Infantry` 前后不变）。
4. **没有写令**：唯一写入尝试报 `WRITE_BLOCKED reason=not_ai_controlled`；`write_attempts_intent=0`。**intent-1 写入尝试计数为 0**（`ExecutionGate.WriteAttempts` 只统计真正放行的写入）。
5. **取消原因为资格丢失**：`INTENT_CANCELLED reason=AiControlLost`（不是 `WindowExpired`，MT 15.545 远早于 deadline 35.108；不是 `SelectionTimeout`；不是 `already_written`）。
6. **撤权**：`BEHAVIOR_DISARMED armed_before=1 weight_before=8.000 weight_after=0.000`（回读值，非写死），且 `handoff` 槽位已 `HANDOFF_CLEARED`。
7. **一处偏差**：`HANDOFF_CLEARED had_pending=0` —— 见 §5.1。

### 4.4 场景 B（intent-2）—— 已生效命令撤权，且资格恢复后旧意图不复活

```
L111 INTENT_TRIGGERED intent=intent-2 at=20.549 deadline=40.549
L159 CANDIDATE_SUBMITTED intent=intent-2 ... at=21.015 write_attempts_total=0 write_attempts_intent=0
L160 ACTIVE_BEHAVIOR_CHANGED intent=intent-2 ... ours=1 tid=37
L161 REAL_SELECTION intent=intent-2 ... at=21.029
L162 HANDOFF_DEFERRED intent=intent-2 callback_tid=37 main_tid=30 at=21.029
L163 AWAITING_NATIVE_APPLY ... at=21.037
L164 HANDOFF_EXECUTED intent=intent-2 ... exec_tid=30 is_main_thread=1 at=21.037
L165 WRITE_EXECUTED intent=intent-2 key=1/0 obj=024fb8dd@Infantry invoked=1 error=<none> order_before=Move order_after=StandYourGround route=native_behavior_apply exec_tid=30 is_main_thread=1 original_body_entered=1 original_body_tid=30 at=21.037 write_attempts_total=1 write_attempts_intent=1
L166 EXPLICIT_CLOSE_SCHEDULED at=27.037
L167 PROBE_ELIGIBILITY_LOST scenario=scenario_b intent=intent-2 mode=b key=1/0 before[order=StandYourGround active_behavior=CbHoldBehavior ai_ctrl=1] after[order=StandYourGround active_behavior=CbHoldBehavior ai_ctrl=0] applied=1 error=<none> at=21.044
L168 PROBE_RESTORE_SCHEDULED intent=intent-2 at=25.044 intent_deadline=40.549 before_deadline=1
L169 INTENT_CANCELLED intent=intent-2 reason=AiControlLost at=21.052 write_attempts_total=1 write_attempts_intent=1
L170 HANDOFF_CLEARED intent=intent-2 reason=AiControlLost had_pending=0 at=21.052
L171 BEHAVIOR_DISARMED key=1/0 reason=AiControlLost armed_before=1 weight_before=8.000 weight_after=0.000 active_behavior_now=CbHoldBehavior error=<none> at=21.052
L172 REVOKE_SKIPPED intent=intent-2 reason=previous_order_not_restorable
L173 SEQUENCE_INTENT_B_CLOSED at=21.052
L184 PROBE_ELIGIBILITY_RESTORED source_intent=intent-2 mode=b key=1/0 before[ai_ctrl=0 order=StandYourGround active_behavior=CbHoldBehavior] after[ai_ctrl=1 order=Move active_behavior=BehaviorCautiousAdvance] applied=1 error=<none> at=25.050 intent_open_now=<none> same_intent_open=0 write_attempts_total=1 write_attempts_source_intent=1 note=any_order_change_here_is_native_SetControlledByAI_side_effect
```

满足的判据：

1. **真实写令 + 回执 + 后值**：`WRITE_EXECUTED invoked=1 error=<none> order_before=Move order_after=StandYourGround route=native_behavior_apply original_body_entered=1 original_body_tid=30`（真实进入原生 `SetMovementOrder` 体，且主线程 tid=30 执行）。
2. **资格变化早于自动显式关闭**：取消 MT 21.052 < `EXPLICIT_CLOSE_SCHEDULED at=27.037`（该时间点只是 StageIntentBRunning 已排的一次调度，随后被 StageDone 取替、`ExplicitClose` 未实际发生）；亦远早于 deadline 40.549。
3. **生产路径因资格丢失取消**：`INTENT_CANCELLED reason=AiControlLost`（**非 WindowExpired / 非 ExplicitClose**）。
4. **撤权**：`BEHAVIOR_DISARMED armed_before=1 weight_before=8.000 weight_after=0.000`。
5. **旧意图不再新写令**：intent-2 的 `write_attempts_intent` 终值为 1（即 MT 21.037 那次合法写令）；L169 之后至 L448 无任何 `WRITE_EXECUTED` / `WRITE_BLOCKED` / `BEHAVIOR_REGISTERED` / `REAL_SELECTION` / `HANDOFF_EXECUTED` / `CANDIDATE_SUBMITTED`（按 §4.1 marker 计数核对，各值均属取消前那一段）。
6. **失资格保持 ≥2 s**：MT 21.044 → 25.050，**4.006 s**（Codex 按原日志复算）。
7. **恢复发生在旧截止之前**：25.050 < 40.549；并观察到 Mission 结束（L448）。
8. **无复活**：`PROBE_ELIGIBILITY_RESTORED ... intent_open_now=<none> same_intent_open=0 write_attempts_source_intent=1`（值仍为取消前那一次）。after 串中 `order=Move active_behavior=BehaviorCautiousAdvance` 是引擎 `SetControlledByAI(true)` 内部 `AI.Tick()` + `SetMovementOrder(AI.ActiveBehavior.CurrentOrder)` 的原生副作用，**如实记录，不伪称为"撤销恢复"**。此时本方行为已 `weight_after=0.000` 且 `_armedBehaviors` 已清空，`FindBestBehavior()` 选出的 `BehaviorCautiousAdvance` 是原生自己的决策。

> 注：after 串里的 `order=Move` 与 `active_behavior=BehaviorCautiousAdvance` 是 `StateOf()` 在同一 Mission tick 的实读即时快照，不是本方写入。

### 4.5 收尾

- L448 `MISSION_CLEANUP intent_and_object_state_cleared`（日志最后一行）。
- `run-p0.ps1` 控制台（副本归档于 `Program/_verify/_lastrun10.txt`）：`RESULT: OK`，exit 0；`no residual Bannerlord process`；`no new crash dir`。
- `OnMissionEnd` 的清理项：`_gate.ClearMission(); _ledger.ClearMission(); _handoff.Clear(); _receiptTarget=null; DisarmAllBehaviors("MissionEnded", t); _snapshot=null; _intent=null;`

### 4.6 本场写入账实汇总

- **本方放行写入总数 = 1**（`WRITE_EXECUTED` count=1，属 intent-2 场景 B 阶段一那一次合法写令）。
- **本方被拒写入 = 1**（`WRITE_BLOCKED` count=1，属 intent-1 场景 A，`reason=not_ai_controlled`）。
- **本方之外计入本账的写令变更 = 0**：`NATIVE_MOVEMENT_APPLIED` count = 0。
- 全部写令由 Mission 主线程 tid=30 执行（`is_main_thread=1`），无跨线程写。

---

## 5. Deviation Register（如实登记，不事后改判据）

### 5.1 `HANDOFF_CLEARED had_pending=0` 与判据文档 §3 的偏差

- 判据文档写定：场景 A 前置为 `Handoff.HasPending == true`，预期观测 `had_pending=1`。
- 实际：`HANDOFF_CLEARED intent=intent-1 reason=AiControlLost had_pending=0 at=15.545`。
- **原因（可解释，不是判据被放宽）**：`EligibilityProbe.OnMissionTick` 的插入点在 `Tick` 之后、`DrainPendingCommand` 之前。在同一次 tick 内，`DrainPendingCommand` 先消费了槽位（L91 `HANDOFF_EXECUTED`）并由生产执行门以 `not_ai_controlled` 否决（L92 `WRITE_BLOCKED`），随后 `CancelIntent` 记下的 `had_pending` 已是消费后的 0。这是**时点安排的必然结果**。
- 场景 A 的核心事实仍完整成立：待转交命令未落地、写入尝试计数 = 0、否决原因为 `not_ai_controlled`、取消原因为 `AiControlLost`、行为权重由 8.000 回读为 0.000。

### 5.2 `SNAPSHOT_FORMATION` 未采到 `ai_ctrl=0`

- 判据文档第 6 节期望"失资格期间至少 2 次 1 Hz 独立采样带上 `ai_ctrl=0`"。
- 实际：`ai_ctrl=0` 全日志只出现 4 次，全在 probe 的 `before[...]`/`after[...]` 串里；`SNAPSHOT_FORMATION key=1/0` 全会话 3 条（L204 / L288 / L373）都是 `ai_ctrl=1`。
- 复算：A 失资格 4.005 s、B 4.006 s；三条详细快照时刻均不在窗口内。CBATH 本可采集 1 Hz 时间线，但当前 runner 在 end_mission 之前取 get_last_run，归档只有 status=no_run，没有保存结束后才可取得的独立时间线。
- Codex 裁定：probe before/after 与生产守卫能支持本场核心动作真实发生，不能替代预先写定的独立观测判据。本条继续缺证，不因列为 Deviation 自动判 PASS。

### 5.3 `HANDOFF_DROPPED reason=intent_closed` 分支本场未触发

- count = 0。原因同 5.1：两条意图被取消时槽位均已被当 tick 消费。
- 该新增分支**尚无实机证据**，登记为未验证（见 §6）。

### 5.4 `REVOKE_SKIPPED` 两次均为合理跳过

- intent-1：`reason=no_own_write`（从未写过，无需回退）。
- intent-2：`reason=previous_order_not_restorable`（`WriteLedger.IsRestorableOrderName` 只认 `Advance/FallBack/Charge/StandYourGround`，而 `previousOrder=Move` 不可忠实重建；**不拿 Stop 冒充**）。
- **因此 `REVOKE_EXECUTED` 本场仍为 0 条** —— 真实回退执行依旧未覆盖。

---

## 6. 尚未覆盖 / 未验证（继承 + 本单新增）

继承 `P0A-HANDOVER.md` §5 的全部未验证项，本单**未覆盖**：玩家接管边沿、目标失效分支、选择超时分支、`REVOKE_EXECUTED` 真实回退执行、快照陈旧拒绝路径（`age` 恒 0，因每 tick 重采样，2.0 s 上限从未压到）、下一 Mission 无残留、主线程只登记一次。

本单新增未验证：

1. `HANDOFF_DROPPED reason=intent_closed` 实机路径（count=0，见 5.3）。
2. 真实 UI 玩家接管能否在本方行为持有期间合法发生在当前控制域对象上（见 §2.1，只给出代码入口与引擎耦合依据，没有实测）。
3. 被控对象为 Cavalry / Ranged / HorseArcher / Skirmisher 时的同类通路（本场被控对象固定为 `key=1/0 Infantry`）。
4. 同 tick 内多个 Formation 同时失资格时 `DisarmAllBehaviors` 的多键遍历（本场 `_armedBehaviors` 只有 1 个键）。
5. Old DLL `44845964C8AD...` 仍不可复现（P0a 已登记）。
6. New vs Old 未做 byte-diff；New 编译链的是 NuGet ref，未对 `TaleWorlds.Core.dll` 之类做 publickeytoken / manifest 对齐对比。
7. 本场无 Benchmark / FPS 数据（见 1.4），无法给出任何性能数字。

---

## 7. 验收表核对（派单 §5 七条）

| # | 验收项 | 判定 | 最小证物 |
|---|---|---|---|
| 1 | **加载正确** | **PASS** | L1 `build_id=b20261011-132044-a06f2e05 assembly_file_sha256=4a7e9c1d...918e8 pid=35032 active_modules=...,CombatBrain 2.0 (P0a),CombatBrain Autonomous Test Harness`；磁盘 SHA256 `4A7E9C1D...918E8` / 53248 B；`source_sha256 == deployed_sha256` |
| 2 | **触发真实且适用** | **PASS**（标签：测试接口触发的资格变化） | L7 `PROBE_CONFIG modes=a,b source=eligibility-probe.txt`；L89 / L167 `before[ai_ctrl=1] after[ai_ctrl=0] applied=1 error=<none>`；改动对象同一实际 Formation `obj=024fb8dd@Infantry`；改动经公开原生 API `Formation.SetControlledByAI`（反编译 `Formation.cs:788`） |
| 3 | **场景 A 否决有效** | **PASS** | L92 `WRITE_BLOCKED reason=not_ai_controlled write_attempts_intent=0` → L93 `INTENT_CANCELLED reason=AiControlLost write_attempts_intent=0` → L95 `BEHAVIOR_DISARMED armed_before=1 weight_before=8.000 weight_after=0.000` |
| 4 | **场景 B 让位有效** | **PASS** | L165 `WRITE_EXECUTED intent=intent-2 write_attempts_intent=1` → L167 失资格 → L169 `INTENT_CANCELLED reason=AiControlLost`（非 WindowExpired / 非 ExplicitClose）→ L171 `BEHAVIOR_DISARMED weight_before=8.000 weight_after=0.000`；此后 intent-2 无新写入 |
| 5 | **资格恢复不复活** | **PASS** | L108（intent-1, MT 19.542）/ L184（intent-2, MT 25.050）`PROBE_ELIGIBILITY_RESTORED ... intent_open_now=<none> same_intent_open=0 write_attempts_source_intent=...`；两者均在旧 deadline（35.108 / 40.549）**之前**恢复，并观察到 L448 Mission 结束；取消后无任何 `WRITE_*` / `BEHAVIOR_REGISTERED` / `REAL_SELECTION` / `HANDOFF_EXECUTED` 再现 |
| 6 | **Mission 与环境收尾** | **PASS** | L448 `MISSION_CLEANUP`；`_lastrun10.txt` `RESULT: OK` exit 0、`no residual Bannerlord process`、`no new crash dir`；`rgl_log_35032.txt` 末段 `There are no living managed objects.` |
| 7 | **阶段退出判定** | **待 Codex** | 本文件提供事实与证据；最终阶段结论由 Codex 复核落定（派单 §6） |

---

## 8. FD-PoC 适用性（Route C 口径，不硬套 Route B 释放租约指标）

| FD-PoC P2 安全判据 | Route C 对应事实 | 证物 |
|---|---|---|
| 停止执行 | 待写命令被否决且槽位清空；已写者不再新写 | L92 / L94 / §4.1 计数 `WRITE_BLOCKED=1`、`WRITE_EXECUTED=1` |
| 撤销本方行为影响 | 本方行为 Disarm + 权重实读 8.000→0.000，`_armedBehaviors` 清空；不用 `ResetBehaviorWeights` 以免波及其它行为 | L95 / L171 |
| 旧意图不复活 | 取消即 `IsClosed=true` 且 `gate.Close`；资格恢复后 `same_intent_open=0`、无新写入 | L108 / L184 + §4.4 第 5 条 |

---

## 9. 结论

1. **修复在本场有效**：`TestDriver.Tick` 新增的 `!bound.IsAiControlled` 取消分支让"资格变化后旧命令不落地"成立；`CancelIntent` 内的 `_handoff.Clear()` 让待写命令失效；`DisarmAllBehaviors` 的实读前后值让撤权可核查。
2. **两条实际通路均有实机证据**：场景 A 写入 0 / 取消 `AiControlLost`；场景 B 合法写 1 / 取消 `AiControlLost` / 恢复后不复活。
3. **删除均为 0**：未删任何既有测试或既有生产分支；仅替换一处写死的日志字段为实读值。
4. **派单 §5 第 1-6 条自判 PASS**；第 7 条"阶段退出判定"交回 Codex。
5. **未验证项已逐条登记**（§6），不得据此推进依赖这些分支的功能。

### 备注：环境与后续清理项

- 启用文件原状态已由 Codex 原字节备份到 `E:\MBmod\CombatBrain2-evidence\20261011-gate3-codex-review\eligibility-probe-enabled-23EB8A27.txt`，随后移除启用路径，默认关闭态已恢复，详见独立复核 §5。原场启用配置仍有证物，不需要继续留在生效位置。
- 当前部署 DLL 仍是本单 New（`4A7E9C1D...918E8` / 53248 B），本次未修改产物；不把 run 6 的 `44845964…` 称为 run 7 身份。
