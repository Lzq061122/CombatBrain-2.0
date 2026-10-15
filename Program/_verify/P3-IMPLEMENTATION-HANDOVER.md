# P3 接续整改 — 实现交接

**状态：限定自检完成，待独立验收。** 本文不宣称 P3 通过，不授权进入 P4。
依据：`CB2-PROMPT-0005`（`P3-RESTART-REMEDIATION-DISPATCH.md`）；原验收合同仍为 `CB2-PROMPT-0004`（`P3-FEEDBACK-REPLANNING-DISPATCH.md`），公式、三门、命令域均未改变。

---

## 1. 来源、构建、部署与运行身份

### 1.1 实现提交

| 项 | CB2 | CBATH |
|---|---|---|
| 工作树 | `E:\MBmod\CombatBrain 2.0` | `E:\MBmod\CBATH-P2` |
| 分支 / HEAD | `master` / `ced0a1895929524d8d3f5a628e112370422f265c` | `codex/cb20-p2-evidence` / `5a2275de434e953802d8de8498b3f09ca1a4fbb6` |
| 时间 | 2026-10-16 00:10:00 +0800 | 2026-10-16 00:10:12 +0800 |
| 首行 | `fix(p3): close restart remediation items 1-5 for bounded feedback replanning` | `feat(p3-evidence): expose independent formation coverage, exit timing and counter-action arm` |
| 规模 | 18 files, +5212 / −11 | 6 files, +433 / −3 |

未 push、未建 PR，未用 `git add -A` / `commit -a`（逐路径暂存）。

CB2 提交后 `git status --porcelain` 仅剩 3 项，均为**故意留在工作树**：

- ` M AGENTS.md`、` M prompts/registry.json` — 治理维护者文件（不同编辑者），非本组交付物。
- ` M Program/src/Host/BuildIdentity.g.cs` — 自动生成身份；派单第 90 行明确记入运行身份、不作为手写源码成果提交。

CBATH 提交后 `git status --porcelain` 为空。

### 1.2 改动范围（相对审查基线 `_audit/cb20-p3/2026-10-15-progress-review/baseline.json`）

CB2 受审 61 文件中 **same=51 / changed=10 / missing=0**：

| 文件 | 基线 SHA256 前 16 | 现 SHA256 前 16 |
|---|---|---|
| `src\Core\ActionFeedback.cs` | 3A0A276452B2A1BA | 3FAF018C2D4CF51F |
| `src\Host\TestDriver.ActionFeedback.cs` | F24B2E5AE80C8E52 | C2E47CCF979E53D0 |
| `src\Host\TestDriver.cs` | 32D0E7FD498DF0E8 | 04495A6F1D23E3D6 |
| `src\Host\EligibilityProbe.cs` | F4F2F3B9557847DC | EAB7C3A34AF6F3EC |
| `src\Host\SamplingBehavior.cs` | 4C3611B039DDB32D | 3F774A0D22854AA1 |
| `src\Host\BuildIdentity.g.cs`（自动生成） | 29B676C033A16745 | 7A42D8A9782A5C08 |
| `tests\ActionFeedbackTests.cs` | E6177CE2114F0611 | 8C6D4F881EB54F8B |
| `_verify\check-p3-feedback.ps1` | 074F8685782E88C6 | 16BD2060627D1116 |
| `_verify\run-p3.ps1` | ED28ECA77F556C0B | 496E4D29A0179AB4 |
| `_verify\_p3-exclusive-stage.ps1` | 73F2F40179C0C7C4 | AC58C80FD45382EC |

新增（不在基线受审清单内）：`src/Core/ActionFeedback.cs`、`src/Host/TestDriver.ActionFeedback.cs`、`_verify/run-p3-exclusive.ps1`。（`tests/ActionFeedbackTests.cs` 在基线清单内，见上表。）

CBATH 受审 16 文件中 **same=15 / changed=1**：仅 `BuildIdentity.g.cs`（自动生成，`BD8186A99EF09812…` → `FCC743A4445558101C1BEB03A8413C53843A8778D4C40E67CD90ED10BFCD09F5`）。

**未动他方材料**：`src\Host\ObservationAdapter.cs` 与基线字节相同（`522495EE7032F4D5…`）；`sources/`、P2 历史、原始失败场未改；`E:\MBmod\BattleObservation-V1\{CB2,CBATH,TCL}` 未修改、未构建、未切换、未提交。

### 1.3 构建

```
cd "E:\MBmod\CombatBrain 2.0\Program"
dotnet build CombatBrain2.csproj -c Release -v q --nologo   → 0 个警告 0 个错误
dotnet test  tests\CombatBrain2.Tests.csproj -c Release -v q --nologo → 通过 276 / 失败 0 / 总计 276
```

本地产物：

| 产物 | 长度 | SHA256 |
|---|---|---|
| `Program\bin\Release\CombatBrain2.dll` | 173568 | `75DEB2D3F3AA6E672D033209EA6679E8AB3C2E9F59A013849A38F499F28687A0` |
| `Program\bin\Debug\CombatBrain2.dll` | 191488 | `1E980EB3ACD51410041A4B05B817EE15FE3323E2B79A64F3CE4737CF55B3AADC` |

`CombatBrain2.csproj` 内无 post-build 部署 target。`BuildIdentity.g.cs` 由 `_deploy\deploy.ps1` 在部署时重生成（当前内容 `BuildId = "b20261015-222818-c9f62071"`）。

测试构成 276 = **255 基线 + 21 新增**（全部在 `ActionFeedbackTests`：31 → 52）。其余 15 个测试类计数与基线一致。既有非本组引入的警告：`ProposalArbiterTests.cs(82,13)`、`(563,13)` 的 `xUnit2013`。

### 1.4 逐场运行身份

| 场次 | build_id | pid | assembly_file_sha256 前 16 | 部署长度 | 部署 SHA 前 16 |
|---|---|---|---|---|---|
| p3-P-20261015-004350 | b20261015-004347-1bf63a79 | 43832 | bb75414a4c4d01f2 | 182784 | BB75414A4C4D01F2 |
| p3-N-20261015-004851 | b20261015-004848-e5b2f53f | 44492 | a43f2c1beae69c32 | 182784 | A43F2C1BEAE69C32 |
| p3-N-20261015-005621 | b20261015-005619-50633b36 | 46636 | b9b0a38423895ad8 | 182784 | B9B0A38423895AD8 |
| p3-C-20261015-010928 | b20261015-010857-79e62453 | 56064 | 8fecd9b8a5844ba7 | 182784 | 8FECD9B8A5844BA7 |
| p3-P-20261015-193423 | b20261015-193418-c9783e27 | 37740 | 905fdcf8d637a4f0 | 190976 | 905FDCF8D637A4F0 |
| p3-P-20261015-195035 | b20261015-195032-52e8ca1a | 53432 | f40846deb289e3b1 | 190976 | F40846DEB289E3B1 |
| p3-P-20261015-203136 | b20261015-203133-f7690240 | 612 | b3b3aab3645f43c0 | 191488 | B3B3AAB3645F43C0 |
| p3-N-20261015-211938 | b20261015-211934-fd6da03a | 7732 | 91421cef819cd39f | 191488 | 91421CEF819CD39F |
| p3-N-20261015-213231 | b20261015-213227-5da13be9 | 44312 | 35a1fa95e0ea5dee | 191488 | 35A1FA95E0EA5DEE |
| p3-C-20261015-214157 | b20261015-214152-84d1958d | 56508 | fe9d9ace2c688bd9 | 191488 | FE9D9ACE2C688BD9 |
| p3-P-20261015-214532 | b20261015-214529-e411067d | 6428 | 68d7e0b4dd1ac385 | 191488 | 68D7E0B4DD1AC385 |
| p3-P-20261015-215314 | b20261015-215311-0e545b62 | 42904 | 342c73dc6287ec35 | 191488 | 342C73DC6287EC35 |
| p3-P-20261015-220249 | b20261015-220246-a9c223d9 | 58580 | 7a1f981094793b67 | 191488 | 7A1F981094793B67 |
| p3-C-20261015-220738 | b20261015-220735-18fd6c08 | 6380 | 7d5e848c0c4aad8e | 191488 | 7D5E848C0C4AAD8E |
| p3-P-20261015-221833 | b20261015-221826-977cb576 | 46552 | db58cbe71973f790 | 191488 | DB58CBE71973F790 |
| p3-P-20261015-222821 | b20261015-222818-c9f62071 | 24920 | 1e980eb3acd51410 | 191488 | 1E980EB3ACD51410 |

证据来源：各场 `CombatBrain2.log` 的 `RUNTIME_IDENTITY`（`assembly_file_sha256` / `pid` / `Location`）与 `deployed-module.json`（`path` / `size` / `sha256` / `last_write`）。182784 = 基线旧构建；190976 / 191488 = 本次新构建。`p3-P-20261015-222821` 的 `assembly_file_sha256` 与本组 `bin\Debug` 完全一致。

`p3-P-20261015-221833` CBATH 身份（`build-identity.json`）：`build_id=b20261015-221831-76da5cbd`、`assembly_sha256=be477d94514ff700770c6939d833a01933daeebe57e72661f9b0ba9279374b8c`、`mvid=6c443e22-98c3-4463-9da1-23f63ec73d88`、`pid=46552`、`game_version=v1.4.8.119303`，`active_modules` 含 `CombatBrain2` 与 `CombatBrainAutonomousTestHarness`。

CBATH 模块源码 SHA256（当前，均与 baseline.json 的 `cbath.reviewed_source_files` 一致）：`CbathTools.cs`=A894FA6979BF5E13…、`P2FormationEvidence.cs`=63F66BD9EAD0A174…、`P3OpponentControl.cs`=B0EFA5DE88CA510A…、`RunRecord.cs`=A26EA645402EF0C5…、`SamplingMissionBehavior.cs`=8239AA21DCF3C6A2…、`CombatBrainAutonomousTestHarness.csproj`=1521AD981EBA2E1E…

---

## 2. 整改 1–5 逐项结果

### 整改1：旧命令退出确认后才接续新分配 — 已实现

- 删除 Host 的缺陷函数 `ExitAppliedOnOwnObject()`（原扫 `_tasks.OpenTasks()`，而退出写完即 `CloseTask`，成功记录反被排除；且任意一条旧退出写出即 true）。
- 新增 Core 唯一生产判定入口 `ActionFeedback.DecideSuccessorStandby(SuccessorStandby)`，输入结构保留旧 Plan、必需对象、各对象退出结果与**实际写出时间**、归属是否被新分配接管、是否明确未应用、继任是否已准备及无效原因、退出截止。`DecayStandby` 只调这一个入口。
- `BeginSuccessorPlan` 改为「取号 → 构建 → `InvalidatePlan` → `EnterStandby`」，等待期内不 Adopt / 不 Publish / 不占槽 / 不武装。`CompleteStandby` 在确认后才 `Adopt` + `PublishSuccessorTasks` + `RetargetStandbyPlan`。
- 必需对象改由**旧计划的退出任务集**确定（原先取全部普通任务，而 `CoordinationDriver.RebindRole/RebindSameObject` 关闭被换掉的旧任务时不排退出，导致确认门永久 `exit_outcome_missing`）。
- `StandbyInvalidReason` 区分协同计划与撤退休整计划（`BuildWithdraw` 的 `Role=None` 不再被判 `roles_incomplete`）。
- `FeedbackTick` 增加逐 tick 等待期分支，避免 1s 截止落在两个 3s 决策点之间而既不确认也不超时。
- 保留：继任任务 ID 避开子串 `exit`；同一 t0 只追加一次；`CloneForSuccessor` 保留 E0/F0 与累计损失；3s/3m、1.5s、1s 截止均未放宽。

新增 13 个纯函数行为测试（Red→Green），覆盖五条要求：成功后关闭的退出仍可确认 / 只完成一个对象不得公布 / 超时退出不得补算成功 / 身份不匹配不影响新 Plan / 迟到旧退出不影响新 Plan，另含重复必需对象、事实缺失、继任失效、归属被接管、明确未应用。连同整改1 早期三条状态机测试（`StandbyTrackerKeepsOriginalPlanIdentityUntilRetargeted`、`StoppedIsOnlySetAfterExitConfirmation`、`StandbyFailureTerminatesSuccessionAndKeepsStopFailed`），`ActionFeedbackTests` 中 `Standby*`/`StoppedIsOnly*` 共 14 条。

**实机正例**（`p3-P-20261015-221833`，门2 pass）：

```
133.844 PLAN_INVALIDATED plan=plan-1 reason=NotEffective detail=action_feedback disengage
133.844 3× EXIT_STOP_QUEUED (plan=plan-1, kind=Hold, origin_plan=plan-1)
133.844 SUCCESSOR_STANDBY old=plan-1 pending=plan-2 decision=Disengage tasks=3 deadline=134.844
134.036/134.086/134.182 3× EXIT_STOP_DONE (030dd50e@Infantry / 038572bf@Ranged / 008c0c1e@HorseArcher)
134.187 SUCCESSOR_EXIT_CONFIRMED pending=plan-2 waited=0.344 detail=all_exits_applied
134.187 ACTION_REPLAN old=plan-1 new=plan-2 decision=Disengage tasks=3 standby_waited=1
```

三个必需对象全部在 `deadline=134.844` 之前真实写出，确认后才公布 plan-2；无一条被提前接管。

### 整改2：风险门只统计当前三职责的不同对象 — 已实现

`RiskGate` 签名由 `(snapshot, teamIndex, List<string> roleKeys, List<int> baselines, cx, cy)` 改为 `(snapshot, teamIndex, List<RoleAssignment> roles, cx, cy)`。校验顺序：`no_snapshot` → `roles_incomplete` → 第一遍 `roles_not_distinct`（空 `ObjectIdentity` 或重复对象）+ 三角色齐备性 → 第二遍逐角色 `role_object_missing key=` / `role_identity_stale key=` → 之后才是 `center_unknown` / `enemy_center_unknown` / `role_below_continue_line` / `enemy_more_than_roles` / `allowed`。

Host 侧删除遍历全部 `plan.Tasks` 的 `CollectRoleBaselines`（会重复计 Main），改为 `CollectRoleAssignments` 只从当前计划版本的权威槽位 `MainWaitTask` / `AdvanceTask` / `GuardTask` / `SupportTask` 各取一个，要求未关闭且 `PlanId`/`Revision` 与当前计划一致，否则该职责缺席。风险中心改用 `ProjectedMainCenter`（当前实际质心 + 目标方向），弃用 `plan.Geometry.MainX/MainY`。

反例已固化：三角色键重复（Main 0/0、Guard 0/1、Support 0/2 各 10 人）对敌 35 人 → 旧实现得 40 人放行，去重后 30 人正确拒绝。保留风险半径 30m、人数相等允许、未知位置不追加。

### 整改3：冻结成员必须逐个可解释 — 已实现

新增 `MissingCoverage(missionKey, snapshot, out detail)`：`not_frozen` / `no_snapshot` / `mission_changed` → 逐记录检查 `ActiveMembers` 可读 → `_side` 中每个冻结成员必须能在当前活跃成员中定位，或已有可信终态，否则 `unexplained_member key=`。接入 `RefreshFeedbackIntegrity`（`member_coverage <detail>`），无法解释即置 `_feedbackIntegrityOk = false`，阻断该窗口正负判定而非补为 0 损失。

三个区分性反例已写：缺一人无终态（`unexplained_member`，阻断 Reinforce）/ 正常转编仍可定位（`DF==0`）/ 已知终态后 Deleted（`DF==1`、`UnknownCount==0`、`Complete==true`）。骑兵骑手计入人数、马匹排除；`Killed/Unconscious` 不因后续 `Deleted` 降级。

### 整改4：修正复算工具的过度 PASS — 已实现

针对审查 `REVIEW.md` 第 4 条 P1（门2/门3 过度判通过）逐条修正 `Program/_verify/check-p3-feedback.ps1`：

**门1** 重写为五条可单列检查：`gate1_successor_has_all_three_roles`（从 `SUCCESSOR_TASK` 取 role 去重须含 Main/Guard/Support）、`gate1_new_plan_has_tasks_and_real_writes`（排除 `-exit$` 后统计普通写入）、`gate1_commitment_actually_changed`（比对旧/新 `move_to_role_point role=Main requested=(x,y)` 集合）、`gate1_new_main_actually_advanced`（`origin=(x,y)` → `requested=(x,y)` 欧氏距离 > 0.5m，Hold 不算）、`gate1_guard_and_support_placed`。五条全成立才算 pass。

**门2** 改为逐旧对象关联：`EXIT_STOP_DONE` / `EXIT_STOP_NOT_APPLIED` 均按 `obj` **且 `plan == 旧 Plan`** 筛选（原代码不筛 plan，后继计划的退出也能充当"本次转换及时停止旧对象"的证据）；新增 `gate2_exit_not_superseded_before_confirmation`，只针对本次 `[$invAt, $invAt + 1.0]` 窗口内的 `EXIT_STOP_SUPERSEDED` 判失败，不全局禁止该事件；8s 窗口改用 CBATH 独立采样（`p2-evidence-*.json`），不足两个独立采样点时记 `unverified`，不打 FAIL；决定预算从独立实际损失/有效速率窗口复算越线时点。

**门3** 增加实际部署比较（己方/敌方质心、人数），`$armFields` 移除 `opponent_control`（它本身即被比较的差异）、加入 `opponent_delay_sec`、排除 `scenario`。

**本轮修复的真实工具缺陷**：

1. 门2 分支体原运行在**脚本主作用域**，体内的裸 `return` 会终止整个脚本 → 跳过门3、报告和 `GATES` 行（实测 N-213231 修复前无任何输出却报 `CHECK RESULT: no failures`）。已用内层 `& { … }` 将那些退出限制在该 scriptblock 内。
2. `gate2_loss_stop_records_exception` 引用了不存在的 `$trig['consecutive']`，对 Disengage 恒判失败；改为 `exception -eq 1 -or streak >= 2`（Disengage 是受门的两点决策，只有 StopLoss 才要求 exception 旗标）。
3. 旧 Plan 已被 `ConditionChanged` 等另一路径关闭时，`gate2_replan_owns_old_plan_invalidation` 由 fail 改判 `unverified`（termination only）；仅当旧 Plan 完全未失效时仍判 fail。

**敏感性验证**（对审查副本 `_verify\_tamper3`，验证后已删；原始场次未清洗改写）：

| 副本 | 篡改 | 门2 变化 | 证明 |
|---|---|---|---|
| 基线 `p3-P-20261015-221833` | — | pass | 正例 |
| T2-fewsamples | 仅保留 `p2-evidence-0*.json` ≤120 | pass → **unverified**（`fewer_than_two_independent_samples_in_8s_window`） | 缺独立样本只记未验证，不无条件 PASS |
| T3-noexitdone | 删 3 行 `EXIT_STOP_DONE` 且 `plan=plan-1` | pass → **fail**（`objects_stopped=0/2 unaccounted=2`） | 缺逐对象及时停止证据判失败 |

历史旧误判样例的修正结果：`p3-N-20261015-005621` 现判 **fail**（`gate2_exit_not_superseded_before_confirmation required=2 taken_over_before_confirmation=2 objects=007dbf45@Infantry,035c31d5@Ranged`），保留为修复前缺陷的真实回归证据。

### 整改5：完整入口、来源身份与安全收尾 — 已实现

`Program/_verify/_p3-exclusive-stage.ps1` 全文重写为三 stage（`deploy_cb2` / `deploy_cbath` / `collect`），顺序执行并记录 `$completed` / `$acquiredGame` / `$failure` / `$failureStage`，结束打印 `=== stage record ===`，失败 `RESULT: FAILED at <stage>` + exit 1。**不杀进程、不删锁、不复位共享开关、不在锁外部署**；finally 只按本场已取得的资源记录。

**修掉一个真实参数传递缺陷**：`& $stage.path @($stage.args)` 中的 `@(hashtable)` 是数组构造而非 splatting，会把整个 hashtable 当第一个位置参数传入，令全部实参静默取默认值。已改为先赋值再 splat：`$stageArgs = $stage.args; & $stage.path @stageArgs`。

离线四条失败控制流（本地替身，未碰共享部署）：deploy_cb2 失败 → `completed: <none>` / exit 1；deploy_cbath 失败 → `completed: deploy_cb2` / exit 1；collect 失败 → `completed: deploy_cb2, deploy_cbath` / exit 1 且替身回显 `scenario=N`（证明参数已正确到达）；全成功 → `RESULT: OK` / exit 0。

四个脚本在 PS 5.1（5.1.26100.9444）下 `[Parser]::ParseFile` 全部通过；`run-p3-exclusive.ps1 -Scenario P -WhatIf` 输出 `owner: 主线组` / `RESULT: WHATIF` / exit 0。

---

## 3. 原合同三门逐门自检

检查器：`Program/_verify/check-p3-feedback.ps1`（61348 B，SHA256 `16BD2060627D111606ABCEB77BB38560933DF21E632F65CEDF06FE54CF3FC95E`）。
调用：`powershell.exe -NoProfile -ExecutionPolicy Bypass -File <chk> -SessionDir <dir> [-ControlSessionDir <dir2>]`。退出码 0 只表示检查器跑完，各门状态单列。

| 场次 | 门1 正 | 门2 负 | 门3 对照 |
|---|---|---|---|
| p3-P-20261015-221833 | unverified | **pass** | unverified |
| p3-N-20261015-005621 | unverified | **fail** | unverified |
| p3-N-20261015-213231 | unverified | unverified | unverified |
| p3-P-20261015-220249 | **fail** | unverified | unverified |
| p3-P-20261015-004350 | **fail** | unverified | unverified |
| p3-P-20261015-203136 | unverified | unverified | unverified |
| p3-P-20261015-222821 | unverified | unverified | unverified |
| p3-C-20261015-010928 | unverified | unverified | unverified |

### 门1（正反馈改变后续投入）— **未取得 pass**

原始索引：`p3-P-20261015-004350`（基线旧场）、`p3-P-20261015-220249`（修复后新场，build b20261015-220246-a9c223d9）。
两场失败明细相同：`gate1_commitment_actually_changed old_main_requested= new_main_requested=`、`gate1_new_main_actually_advanced role_point_main_writes=0 proof=`、`gate1_guard_and_support_placed guard_writes=0 support_writes=0`。

**根因为真实缺陷，非检查器过严**。`p3-P-20261015-220249` 逐行：

```
124.703 SUCCESSOR_STANDBY old=plan-1 pending=plan-2 decision=Reinforce tasks=3 deadline=125.703
125.197 SUCCESSOR_EXIT_CONFIRMED waited=0.494 detail=all_exits_applied
125.197 ACTION_REPLAN old=plan-1 new=plan-2 decision=Reinforce tasks=3 standby_waited=1
125.204/126.208/127.218/128.220 COORDINATION_STATE plan=plan-2 phase=AwaitingDependencies
        guard_ready=0 support_ready=0 advance_allowed=0 detail=guard=None/reads=0/ready=0 support=None/reads=0/ready=0
127.708 ACTION_BLOCKED plan=plan-2 reason=loss_not_resolved stopped=1
127.708 WRITE_DELAYED task=plan-2-task-2 / -task-3 reason=predecessor_not_written
128.039 WRITE_EXECUTED task=plan-2-task-1 kind=Hold geometry=hold_stop
128.544 TASK_FEEDBACK task-1 state=effective detail=hold_no_displacement_required
128.539 TASK_FEEDBACK task-1 state=failed reason=CommandOverridden → PLAN_INVALIDATED plan-2
```

`plan-2` 的 Guard(task-2)/Support(task-3) 始终 `candidate_not_submitted`，`COORDINATION_STATE` 全程 `guard=None support=None`，`advance_allowed=0` 因而从未生成推进任务。机制：`BattleAssessment.BuildSuccessorCoordination`（`:227`）只调 `BuildCoordination`，后者设 `guardTask.DependsOnTaskId = mainTask.TaskId`（`:212`）与 `supportTask.DependsOnTaskId = mainTask.TaskId`（`:214`）；`TestDriver.WriteDelayBlock`（`:1802`）的 `PredecessorNotWritten` 对 Ordinal 2/3 的 Guard/Support 返回 `predecessor_not_written`，而 Main 推进任务的前置门 `CoordinationPlan.MainAdvanceAllowed`（`:99`）又要求 `guard.WrittenAt >= 0 && support.WrittenAt >= 0` 且 Feedback 为 Effective —— 形成互锁，继任协同计划在原生覆盖（约 3.3s 后）之前无法建立保护/辅助关系，也无 `move_to_role_point role=Main` 写入。

对照组证据：**初始** `plan-1` 可走通——`p3-P-20261015-220249` 有 `WRITE_EXECUTED task=plan-1-task-2 geometry=move_to_role_point role=Guard`(7.591)、`task-3 role=Support`(10.641)、`COORDINATION_ADVANCE plan-1-task-4-g1`(18.747)、`WRITE_EXECUTED task-4-g1 role=Main`(23.039，readback_error=0.000)。故写令通路本身可用，问题是继任计划寿命不足。

独立复算：`p3-P-20261015-220249` counts `evaluations=34 freezes=1 replans=1 invalidations=2 writes=15 exit_stop_queued=6 exit_stop_done=6 successor_tasks=3`。

### 门2（负反馈调整/终止并停止旧命令）— **取得 pass 与 fail 各一**

- **pass 正例**：`p3-P-20261015-221833`（`gate2_negative: pass :: old=plan-1 new=plan-2 decision=Disengage objects_stopped=2/2 stop_failed=0 unaccounted=0 taken_over_before_confirmation=0`）。逐对象转换时间表见 §2 整改1；独立复算 `independent_crossing=133.844 invalidated_at=133.844 latency=0s`、`independent_samples_after_8s=292`、`max recompute error: LE=0 LF=0.00039 B=0 C=0.0003 U=0.00043`；`independent casualties: DE=53 DF=128` 与 mod 上报 `ACTION_TERMINAL: DE=53 DF=128` 一致。
- **fail 反例（预期保留）**：`p3-N-20261015-005621`（`required=2 taken_over_before_confirmation=2`）——用户 2026-10-15 裁定：它就是修复前缺陷的证据，不能因对象后来停住而认定旧退出接续符合合同。修正后的工具不再无条件 PASS。
- **unverified**：`p3-N-20261015-213231`（`push stopped and plan invalidated, but no successor replan (termination only)`）。

### 门3（对照只改变关键反馈）— **未取得 pass**

`gate3_arms_differ_only_in_counter_action` 成立（`counter_action this=1 control=0 other_fields_match=True`，比较 `own_army_size`/`own_composition`/`enemy_army_size`/`enemy_composition`/`p1_enabled`/`p2_enabled`/`p3_enabled`/`competition_inject`/`opponent_delay_sec`/`combat_hold_seconds`）。

`gate3_scene_equivalent` 全部 **FAIL**，实测对照四组：
- P-221833 vs C-220738：`own_center this=822.2,523.9 control=965.8,945.2; enemy_center this=1182,598.3 control=711.8,685.9`
- P-203136 vs C-214157：`own_center this=952.7,628.5 control=1301.8,863.9; enemy_center this=1002.2,946.9 control=1464.4,1128.4`
- P-004350 vs C-010928：`own_center this=1070.3,794.3 control=786.7,531; enemy_center this=759.1,833 control=1095.2,551.3`
- P-220249 vs C-220738：同类

P/C 两场实际开局部署质心相差数百米，属派单 §3 表已点明的证据缺口（"P/C t0 附近实际位置和朝向不同，缺实际部署/地形关系可比和行为产物等价证明"）。**这是工具的正确发现，不是工具缺陷。** 门3 用坐标字符串精确相等判场景等价，语义偏严；因本次 P/C 连地图部署都不同，任何判据都不成立，按最小改动原则未改判据。

---

## 4. 入口、WhatIf 与复算工具用法

### 4.1 唯一完整入口

```
E:\MBmod\BattleObservation-V1\coordination\Invoke-ExclusiveGame.ps1
  -Owner '主线组' -ScriptPath <_p3-exclusive-stage.ps1> -ScriptParameters <hashtable>
```

`Invoke-ExclusiveGame.ps1` 用 `[System.IO.File]::Open($lock, OpenOrCreate, ReadWrite, None)` 持锁 → 写 metadata `{owner,pid,script,started_utc}` → 设 `$env:MBMOD_SHARED_GAME_PID` → `& $resolvedScript @ScriptParameters` → 非零退出抛异常；`finally` 只释放自己的 lease，**不做任何进程清理**。

推荐包裹脚本：`Program/_verify/run-p3-exclusive.ps1`（自动填 Owner 与三个 stage 路径）。

```
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Program\_verify\run-p3-exclusive.ps1 `
  -Scenario P|N|C -CombatHoldSec 180 -OpponentDelaySec 6 [-WhatIf]
```

`-WhatIf` 实测：打印场景、协调路径、owner=`主线组`、只读身份项、三 stage 路径与参数，标注不写 DLL / 不写配置 / 不写哨兵 / 不启动游戏，输出 `RESULT: WHATIF`，exit 0。

stages：`deploy_cb2` = `Program\_deploy\deploy.ps1`（已有 Bannerlord 进程则 exit 2 拒绝；删 `obj`/`bin` 强制重建 Debug）；`deploy_cbath` = `E:\MBmod\CBATH-P2\RD\...\CBATH\build-deploy.ps1`；`collect` = `Program\_verify\run-p3.ps1`（~1 Hz 采样 `cbath/get_p2_formation_evidence`，写 `p1-command-loop.txt=enabled=0` / `p2-command-loop.txt=enabled=1` / `p3-feedback.txt=enabled=1`，清空两探针，跑 `h3_provider_start → h3_start_from_screen → auto_finish_deployment → delegate_team_to_ai`）。

### 4.2 场景配置（当前 `run-p3.ps1`）

| 场景 | 己方 | 敌方 | 对手反制 |
|---|---|---|---|
| P | 300 `159,106,35,0` | 60 `100,0,0,0` | 开（冻结后延迟 6s） |
| N | 100 `30,55,15,0` | 130 `95,25,10,0` | 开 |
| C | 300 `159,106,35,0` | 60 `100,0,0,0` | 关 |

P/C 均已修正为敌方 `100,0,0,0`（原先 `50,50,0,0` 会使 `ranged_edge` 为负，协同计划无法成立——该首跑失败根因已在 §5 记录）。

### 4.3 复算工具

```
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Program\_verify\check-p3-feedback.ps1 `
  -SessionDir <sessions\p3-X-...> [-ControlSessionDir <sessions\p3-C-...>]
```

读 `CombatBrain2.log` 表示"决定"，读 CBATH `p2-evidence-*.json` / `cbath-timeline.json` 表示"战场"，不把生产分数当真值；输出 `GATES:` 行与 `<SessionDir>\p3-offline-check.json`。注意：对照必须传另一个场次目录，把 C 场与自身比较必然使 `gate3_arms_differ_only_in_counter_action` FAIL。

**环境约束**：本机无 `pwsh` 命令，必须用 `C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe`。

### 4.4 共享资源取得与清理实读

- 锁 `E:\MBmod\BattleObservation-V1\coordination\shared-game-session.lock`（221 B，mtime 2026-10-15T23:55:12）：记录 owner=`本次 P3 观测接入执行方`、pid=21528，**该 pid 已不存在**；实测 `[File]::Open(..., None)` 可取得。锁文件残留不等于占锁 —— 判定占用必须看实际进程与目录活动。
- `coordination\handoff.md` mtime 仍为 2026-10-12T13:36:43，**无本轮资源交回记录**。
- **截至本文撰写（2026-10-16 00:39）共享资源由他方占用**：观测组于 00:36:22 起又一次独占运行——Bannerlord pid 52328（00:36:22 启动）存活，`Documents\CombatBrain2\` 开关被置为 `p1-command-loop.txt=enabled=0` / `p2-command-loop.txt=enabled=1` / `p3-feedback.txt=enabled=1` / 两探针空（mtime 00:36:22），`Documents\CommandBrainATH\` 出现 `cbath-*-52328.txt` 与 `current-battle.txt`（00:36:31–00:36:41）。更早一轮（23:55:13）部署点 DLL 为 194560 B / `1609BF99BDBDD06CF7FDF4C3F59652A3F2E6794E1E864D7C39A6756F203A4F90`，非本组产物。**按派单 §3/§4/§5，本组不抢用共享资源、不在锁外复位开关或强杀他方进程；因此本组未在提交后补跑实机。**
- 本组最后一次独占运行（`_lastrun-p3-exclusive-N2.txt`，21:40:02）结束状态：`completed: deploy_cb2, deploy_cbath, collect` / `game acquired: True` / `RESULT: OK`；该次收尾已实读复位`p1/p2/p3` 全为 0、两探针空、无本组残留进程、崩溃目录为空。

---

## 5. 失败与未覆盖项

1. **门1 未取得 pass。** 根因是继任协同计划的可执行性缺陷（§3 门1），已定位到源码行，未修复——超出本单五项整改范围，且需要新的实机窗口验证。派单 §3 表要求的"修复后正例须实际完成新保护/辅助关系及新主力有限推进"**未达成**。
2. **门3 未取得 pass。** P/C 实际部署不可比；未取匹配对照场次。
3. **晚场的独占入口证据不完整。** 只有 `p3-P-20261015-203136`、`p3-N-20261015-211938`、`p3-N-20261015-213231` 三场留存了完整 stage record（`_lastrun-p3-exclusive-*.txt`）。`p3-P-20261015-214532`、`-215314`、`-220249`、`p3-C-20261015-214157`、`-220738`、`p3-P-20261015-221833`、`-222821` 未留存 stage-record 日志；`gabs-config\bannerlord148-cb2\history.json` 显示 GABS 直启（`lastSuccessAt=2026-10-15T14:28:24Z` 对应 222821 场）不经协调锁。**这些晚场不作为"经 `Invoke-ExclusiveGame.ps1` 独占执行"的证据**；其运行身份（§1.4）与产物仍有效，但启动通道归属未证实。
4. **`p3-P-20261015-222821` 未冻结**（`ACTION_FEEDBACK_FROZEN=0`，三门 `unverified :: no freeze`）。该场不可用于任何门。
5. **提交后未能补跑实机。** `ced0a189` 提交（00:10）后共享资源一直被观测组占用（§4.4），本组不抢用共享资源，故提交后仅完成离线回归（0 警告 0 错误 / 276 通过）。§1.4 全部实机场次的源码 mtime 为 20:16–20:25、构建早于提交，**提交所记录的源码内容与当时实机运行的源码内容一致**（`git diff HEAD` 仅剩 3 项非源码文件），但提交后的 Release 产物 `75DEB2D3…` 从未实机运行。
6. **继任等待期的迟到回调与原生覆盖交叉路径** 只有 221833 一例正例，样本量不足以声称稳健。
7. **门3 判据偏严**（坐标字符串精确相等）未改，属已知工具局限。

**未触发的自然边界**（保留为未验证，不登记为通过）：获选超时、写令截止后才首次获选、`FLIGHT`/`STOP_LOSS` 触发的继任、P1/P2 相关回归——本批未受影响路径按 diff 继承旧证据，未重复整套场景。

---

开发侧结论：**限定自检完成，待独立验收。** 五项整改均已实现并通过离线验证（276/276、0 警告 0 错误）与敏感性验证；实机三门中门2 取得 pass 与 fail 各一，门1、门3 未取得 pass 且根因已定位。P3 阶段是否通过由独立审核裁定。
