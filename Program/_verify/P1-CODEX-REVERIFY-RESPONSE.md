# P1 四处整改响应（回应 `P1-CODEX-REVERIFY.md`）

日期：2026-10-12
依据：`Program/_verify/P1-CODEX-REVERIFY.md`（独立复验，文档提交 `38766ee`，未 push）§3 四处限定整改。
范围：只修这四处，不新增验收项、不重开已验收通路、不改参数与两配置。
本文件只陈述实际做了什么与实机可核对的证据；**不预先登记 P1 阶段通过**，阶段判定归独立验收方。

---

## 0. 结论摘要

四处整改均已实现并经两场实机复验；复验中发现并修复了一处**由整改本身引入、且会吃掉发令机会**的缺陷（延迟门位置，见 §5）。整改后决定性场次：

| 场次 | 配置 | 载入 sha256 | RESULT |
|---|---|---|---|
| `20261012-122809` | P | `88709E862E904CD183BE5F97F01348DE73B8523AFEF9FCBC0A1DB27312EB4246` | OK |
| `20261012-123126` | N | 同上 | OK |

同 build `b20261012-122324-537f0129`，MVID `207b5935-15ef-4a98-bbe0-df8c6809d155`，产物 95232 B，与部署文件摘要一致。

---

## 1. 发令顺序（复验 §3.1）

**要求**：脚本 §4.5 固定 `Ordinal 1 = Ranged`、`2 = Infantry`，远程先写、步兵后写；不能只凭候选列表有序宣称有序。

**实现**：`WriteDelayBlock` 增加前序门 `PredecessorNotWritten(plan, task)`——本计划内存在 `Ordinal` 更小且尚未实际写令的普通任务时，后序返回 `predecessor_not_written` 并等待。

**实机证据（整改后决定性场）**：

| 场次 | 写令序列（task / kind / at） |
|---|---|
| P `20261012-122809` | `plan-1-task-1` Reposition 4.591 → `plan-1-task-2` Engage 8.044 → `plan-4-task-1` Reposition 19.597 → `plan-4-task-2` Engage 23.053 → `plan-5-task-1` Reposition 101.101 → `plan-5-task-2` Engage 104.047 |
| N `20261012-123126` | `plan-1-task-1` Hold 4.600 → `plan-1-task-2` Reposition 8.049 → `plan-4-task-1` Hold 20.089 → `plan-4-task-2` Reposition 23.049 |

三个（P）/两个（N）计划**无一例外**先写 Ranged 再写 Infantry，与脚本固定序一致。`WRITE_DELAYED` 中 `predecessor_not_written` 在 P 场出现 3 次，正是该门被实际触发的证据。

**对照（整改前同轮）**：P `20261012-121443` 为 `task-1` 3.091 → `task-2` 4.594，顺序其实已对；而独立复验所指的失败序（步兵先）出现在上一批 `20261012-094559` / `20261012-094953`。本轮两者均已按固定序写出。

---

## 2. 反馈基线与目标读回（复验 §3.2）

**要求**：位移基线必须从实际写令时建立（不能含写令前运动）；Move 必须读回实际命令目标点并与有效请求点比较，不能用"当前位置到请求点的距离"代替。

**实现**：

1. `FormationTask` 新增 `BaselineX/BaselineY`、`DistanceToTargetAtWrite`、`HasOrderTarget/OrderTargetX/OrderTargetY`，以及 `SetBaseline/SetOrderTarget/OrderTargetReadbackError`。
2. 写令前固定基线（`TestDriver` 取 `f.CurrentPosition`）。
3. 写令后读回实际目标点：`TryReadOrderTarget` 走 `Formation.GetReadonlyMovementOrderReference()` → `MovementOrder.GetPosition(Formation)`（公开 API，无反射、无新私有字段；`BehaviorFireFromInfantryCover.cs:30` 为同一用法范例）。
4. `TaskFeedback` 按 `TaskKind` 分流：Engage 用写令基线；Reposition 先查 `OrderTargetReadbackError() > 0.5 m` 即判未生效，否则按"到目标距离减少 ≥3 m 或已到达容差"判定；Hold 保持 `hold_no_displacement_required`。

**实机证据**：

- 目标读回：P `order_target=(1188.013,859.202) readback_error=0.000`；N `order_target=(894.335,636.496) readback_error=0.000`。
- 写令基线：P `plan-1-task-1` `closed_distance=3.31 distance=21.69 from=25.00`（25.00 − 21.69 = 3.31，`from` 即写令时到目标距离）；N `plan-1-task-2` `closed_distance=3.02 distance=16.98 from=20.00`、`plan-4-task-2` `closed_distance=3.20 distance=16.80 from=20.00`。
- 按类型分流：P `plan-1-task-2` Engage `engage_toward_enemy=…`（朝敌位移判据），N `plan-1-task-1` Hold `hold_no_displacement_required`。

**测试**：新增 `RepositionProgressUsesWriteTimeBaseline`（x=24→`closed_distance=4.00` 生效；x=22→`closed_distance=2.00` 未达门槛；并记明若误用计划创建位置当基线会算出 22 而错误生效）、`RepositionRejectsMismatchedOrderTarget`（请求 25 / 实际目标 30 → `order_target_readback_error=5.00`）、`EngageProgressUsesWriteTimeBaseline`。

---

## 3. 退出动作时限（复验 §3.3）

**要求**：按既定 1 s 安全期限覆盖未完成的退出动作，**无论已提交、已获选或已转交**；超时关闭/清槽/撤权；不扩成额外重试框架。

**实现**：`SweepExitStops` 删除 `if (task.SelectedAt >= 0.0) continue;`，时限一律按 `now - task.CreatedAt` 计（不因提交/获选重置），日志改记 `reason=not_completed_in_time` 并带 `submitted=` / `selected=`；写令路径最终 setter 前再加一次过期拦截（`EXIT_STOP_EXPIRED`）。

**实机证据**：两场均为 `EXIT_STOP_QUEUED at=9.567` → `EXIT_STOP_NOT_APPLIED reason=not_completed_in_time submitted=0|1 selected=0 waited=1.004|1.015 at=10.571|10.582`，**在 1 s 安全期限内关闭，且未跨入后续计划**（P 后续 plan-4 建于 16.562、N 建于同轮之后）。退出任务日志该场共 3 行后即终止，无跨计划影响：`PLAN_INVALIDATE_REJECTED=0`。

**与脚本 L235 的一致性**：脚本明确允许"若当前原生未选中本方行为，记录 `exit_stop_not_applied`，在 1 s 安全期限内撤权，不强行外部写令"。本场 `selected=0`（原生未选中本方行为）即属该分支，故 `EXIT_STOP_DONE=0` 不构成失败；撤权在同 tick 完成（`HANDOFF_CLEARED` + 两条 `BEHAVIOR_DISARMED weight 8.000→0.000`）。

**对照（整改前）**：上一批 P `20261012-094559` 退出 Stop 在关闭后 4.498 s 才写出并跨入新计划，本轮已消除。

---

## 4. 继续条件统一（复验 §3.4）

**要求**：逐 tick 战略条件路径与决策路径使用同一套已定继续语义（±0.05 继续阈值、我方 ≥70% 兵力线）；不得用主对照有效性条件或创建时布尔相等当继续条件；不新增阈值或迟滞参数。

**实现**：`CheckConditionsEachTick` 由 `conditions.SameAs(plan.Conditions)` 改为 `conditions.CanContinueAs(plan)`（`PlanLedger.CanContinue` 同源）。

**实机证据**：两场 `PLAN_CONDITION_LOST = 0`、`PLAN_CHANGED = 0`。

直接考验在 N 场：`strength_ratio` 采样范围 **0.785–0.919**，其中 **49 次低于 0.80**（最低 0.785），即已跌破主对照 80% 有效性线但高于 70% 继续线，计划**继续而未取消**（N `PLAN_CREATED=2 / PLAN_INVALIDATED=2`，两次失效分别为探针失资格与 Mission 结束）。

对照（整改前）：P 曾在 `strength_ratio=0.798` 时仅因 80% 布尔变化取消 plan-3，随后在 0.80 附近往返，造成 9 次计划创建/失效。

**如实登记**：本轮 P 场 `strength_ratio` 恒为 0.929，**未落入 (0.70, 0.80) 区间**，故该修复在 P 场未被触发；成立的是 N 场。

---

## 5. 复验中发现并修复的整改引入缺陷（延迟门位置）

**现象**：中间场 P `20261012-121443` 中 `plan-3`（探针恢复后新建计划）于 13.611 创建并提交两个任务，14.040 / 14.090 各记一次 `WRITE_DELAYED reason=first_command_delay`，**此后至 Mission 结束（151.785）再无任何投递或写出**；N `20261012-121754` 缺陷同形。

**根因**：延迟门当时只位于写令路径 `TryExecuteTask`（由 `OnActiveBehaviorChanged` → `DrainPendingCommand` 进入），即**在原生获选已被消费之后**判定。原生获选是一次性事件：该次被拒绝后行为在 14.532/14.582 已切走（`ours=0`），不会再来。故延迟窗口开启（约 15.1）后再无机会写出。

**修复**：把延迟与顺序门移到 `SubmitPending` 中、`ArmBehavior` **之前**——被延后的任务不 arm、不消耗获选，下一决策 tick 重试；同时删除写令路径中的重复延迟门（否则会二次拒绝、重现缺陷）。`WRITE_DELAYED` 随之带 `source=submit_pending`。

**验证**：整改后决定性场 P `WRITE_EXECUTED` 由 2 条增至 6 条，`plan-4`、`plan-5` 的正常任务均在延迟窗口开启后实际写出；N 由 2 条增至 4 条。该缺陷若无此步，P/N 的"恢复后新计划"实际不会发令，会掩盖真实行为。

---

## 6. 构建与测试

- 主工程干净重建：**0 个警告 / 0 个错误**（`Remove-Item bin,obj` 后重建）。
- 测试：**86 通过 / 0 失败**（基线 74；本轮新增 5 个：`RepositionProgressUsesWriteTimeBaseline`、`RepositionRejectsMismatchedOrderTarget`、`EngageProgressUsesWriteTimeBaseline`、`ContinueAndCreateThresholdsAreDistinct`、`ExitStopDeadlineIsNotResetBySubmissionOrSelection`，并修正既有 `MoveRequiresClosingDistance` 的断言口径）。
- 部署：`Program/_deploy/deploy.ps1`，`DEPLOY OK`，`source_sha256 = deployed_sha256 = 88709E86…`。

---

## 7. 未验证项与需要独立验收方注意的事实

1. **`REVOKE_EXECUTED=0`**：如复验报告所述，这是旧 MovementOrder 恢复通道零次，**不能**据此否定已发生的**行为撤权**；两场 `BEHAVIOR_DISARMED weight 8.000→0.000` 均有据。本轮未新增撤权恢复证据。
2. **P 场未触发 70–80% 继续区间**（见 §4），该修复的考验仅来自 N 场。
3. **退出 Stop 两场均未实际写出**（`selected=0`），均按脚本 L235 的"如实登记不获选"分支关闭；本轮**没有**一次真正发出的退出 Stop 实例。
4. **本轮实机产物含未提交的辅助层**：工作树内有用户自行开发、尚未提交的 `Program/src/Host/ObservationAdapter.cs`（JSON 行事件记录层）及 `TestDriver.cs` 中相应 `Emit` 调用、`BuildIdentity.g.cs` 里的 build 戳、`_deploy/deploy.ps1` 重写。该层不读游戏对象、不参与决策，且在 CBATH 未发布 battle_id 文件时完全惰性；但它确实包含在本次部署产物中。**四处整改的归因请以本文件所列为准，勿把该层的行为计入整改效果。**
5. **构建产物摘要不可单独判源码版本**：`BuildIdentity.g.cs` 每次构建自动重写为时间戳 `BuildId`，同一份源码重复构建摘要不同。判定应同时给出 MVID 或 `build_id`。
6. 启动期失败场 `20261012-122329`（CBATH `ping` 未应答，`no_ping`）按 §5.5(c) 登记，重跑即通过，**不构成对代码的证据**。
7. 本轮**未**触碰 CBATH 冻结核心；`gabs-config/config.json` 与未跟踪的 `bannerlord148-nocb/` 与本批无关，未提交、未删除、未修改。

---

## 8. 归档

- 新归档：`E:\MBmod\CombatBrain2-evidence\20261012-p1-reverify-sessions.zip`，**753161 B / 157 条目**，SHA256 `A13CA02B52A87FA49A73F5540C340834A01F3F53D786EB31E8E42D7C734FA3D2`，sidecar `.sha256` 已写。
- 覆盖面：8 场（`20261012-122809`、`20261012-123126`、`20261012-121443`、`20261012-121754`、`20261012-122329`、`20261012-094559`、`20261012-094953`、`20261012-013102`、`20261012-013618`、`20261012-012443`）+ `_p1_reverify*.log`。
- 逐场次「文件集合哈希 vs 源目录」比对：**8/8 MATCH**。
- `sessions/INDEX.md` 已加第二轮章节并更新外置归档表首行；上一批场次**不重写**。
