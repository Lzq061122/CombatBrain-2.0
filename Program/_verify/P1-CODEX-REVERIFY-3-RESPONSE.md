# P1 第三轮复核响应

> Codex第四轮独立复核：首次提交锚点修复通过，但实际10 s截止仍仅在3 s决策点检查，晚到获选还可被写令门放行，同一获选等待整改尚未完整关闭。当前判定、受审DLL反例及限定整改见 `P1-CODEX-REVERIFY-4.md`；不因失效原因优先级另加任务。

对 `../P1-CODEX-REVERIFY-3.md`（提交 `ab1cce7`）§3 唯一阻挡的整改与复验记录。
判据：`../P1-MINIMAL-LOOP-SCRIPT.md` v1.3。本文件不预先登记 P1 阶段通过。

## 1. 结论摘要

- 第三轮 §3 唯一阻挡（获选等待 10 s 截止被重复投递不断刷新）已修复，P 场实机直接确证。
- §1.1 缺口 1、§1.2 缺口 2 按裁决不重开，未增加任何验收项目。
- 第三轮已独立对齐的 12 条写令未受影响，本轮未回退任何既有判据。

## 2. 整改内容

判据 L191：「`SelectionWaitSeconds` 获选等待上限 10.0 s，从该任务**首次**提交候选起算，不因重复回调重置；超时使 Plan 失效」。

修复前，同一 task_id 每 3 s 决策节奏都会重投一次，且每次都覆写提交时刻：

- `Program/src/Host/TestDriver.cs:336` `task.SubmittedAt = now;`
- `Program/src/Core/ExecutionGate.cs:40` `s.SubmittedAt = missionTime;`

`SelectionDeadline()` 取 `SubmittedAt + SelectionWaitSeconds`，于是截止随重投同步前移，永远追不上，超时分支不可达。

**改动 1 — `Program/src/Core/BattlePlan.cs`**：`FormationTask` 增加 `SubmissionCount` 字段与只记首次投递的入账方法。

```csharp
// 记一次候选投递。SubmittedAt 只记首次投递时刻：获选等待从首次提交起算，
// 重复投递（3 s 决策节奏下的常态）不得刷新截止（脚本 §5.2）。
internal void NoteSubmission(double missionTime)
{
    SubmissionCount++;
    if (SubmittedAt < 0.0) SubmittedAt = missionTime;
}
```

**改动 2 — `Program/src/Core/ExecutionGate.cs`**：`SubmitCandidate` 与 `FormationTask` 统一语义，并补两个只读访问器供核对。

```csharp
// 与 FormationTask 同语义：只记首次投递，重投不刷新等待截止（脚本 §5.2）。
if (s.SubmittedAt < 0.0) s.SubmittedAt = missionTime;
s.SubmissionCount++;
```

**改动 3 — `Program/src/Host/TestDriver.cs`**：`SubmitPending` 内改调 `task.NoteSubmission(now)`，`TASK_CANDIDATE_SUBMITTED` 增加 `submission_count` / `first_submitted_at` / `selection_deadline` 三个字段，使截止是否被刷新可从日志直接核对。

`SubmitPending` 中「已过截止不再投递」的门（`SelectionDeadline() <= now`）未改动：修复后截止固定，该门语义才真正成立。

## 3. 离线验证

新增两条测试并做红→绿双向确认：

- `Program/tests/TaskSelectionDeadlineTests.cs` → `RepeatedSubmissionDoesNotExtendDeadline`
- `Program/tests/ExecutionGateTests.cs` → `GateKeepsFirstSubmissionTime`

红灯（测试先行）：`error CS1061: "FormationTask"未包含"NoteSubmission"的定义`，4 处，exit 1。

敏感性验证：把两处 `if (... < 0.0) ... = missionTime;` 临时改回无条件赋值，两条测试随即 FAIL（`Assert.Equal` 值不一致，分别报在 `TaskSelectionDeadlineTests.cs:32`、`ExecutionGateTests.cs:121`）；恢复后全绿。证明两条测试确实能抓住旧行为。

结果：测试 90 通过 / 0 失败；`dotnet build Program\CombatBrain2.csproj -c Debug` 0 警告 0 错误。

## 4. 实机复验

经共享游戏协调入口串行执行（`Owner=主线组`），完整流程（部署 → CBATH 采集 → 清理），`RESULT: OK`、exit 0。

| 场次 | 配置 | build_id | 载入 sha256 | 大小 |
|---|---|---|---|---|
| `Program/_verify/sessions/20261012-145644` | P | `b20261012-145642-5d9874f4` | `467A10531BF01722F5E1146E8D9248141AFC3B7FE3CBDDDE630B6F0443B439E6` | 96768 B |
| `Program/_verify/sessions/20261012-150054` | N | `b20261012-150053-d975e954` | `1C42FE3307A7643B2D03034BC4136DFF472317319C4A5EC78BF83F75A69F4E4E` | 96768 B |

### 4.1 修复点直接证据（P 场）

同一任务在两轮中的对照：

| 项 | 上一轮 `20261012-142820` | 本轮 `20261012-145644` |
|---|---|---|
| `plan-4-task-1` 投递次数 | 15 | 4 |
| 提交时刻序列 | 97.721 … 139.788（每次覆写） | 94.654 / 97.659 / 100.662 / 103.662 |
| `first_submitted_at` | 随投递变动 | `94.654`（4 次完全不变） |
| `selection_deadline` | 随投递前移 | `104.654`（4 次完全不变） |
| 终止原因 | `FormationEligibilityLost` | `SelectionTimeout` |
| 从首次投递到关闭 | 42.213 s | 12.010 s |
| `TASK_SELECTION_TIMEOUT` | 0 | 1 |

日志原文：

```
[CB2][TASK_CANDIDATE_SUBMITTED] task=plan-4-task-1 plan=plan-4 kind=Reposition key=1/1 obj=00cb8a2d@Ranged order_before=Move order_after= behavior_registered=1 behavior_weight=8.000 at=94.654 submission_count=1 first_submitted_at=94.654 selection_deadline=104.654 write_attempts_total=5 write_attempts_task=0
[CB2][TASK_CANDIDATE_SUBMITTED] task=plan-4-task-1 ... at=97.659 submission_count=2 first_submitted_at=94.654 selection_deadline=104.654 ...
[CB2][TASK_CANDIDATE_SUBMITTED] task=plan-4-task-1 ... at=100.662 submission_count=3 first_submitted_at=94.654 selection_deadline=104.654 ...
[CB2][TASK_CANDIDATE_SUBMITTED] task=plan-4-task-1 ... at=103.662 submission_count=4 first_submitted_at=94.654 selection_deadline=104.654 ...
[CB2][TASK_SELECTION_TIMEOUT] task=plan-4-task-1 plan=plan-4 kind=Reposition key=1/1 obj=00cb8a2d@Ranged order_before=Move order_after= waited=12.010 limit=10.000 at=106.664
[CB2][PLAN_INVALIDATED] plan=plan-4 goal=exploit_ranged_advantage reason=SelectionTimeout detail=task=plan-4-task-1 selection_timeout at=106.664
```

时序解释：截止固定为 104.654，之后 `SubmitPending` 不再投递（无第 5 次），下一个决策 tick（3 s 节奏）106.664 检出超时。`waited = 106.664 − 94.654 = 12.010`，其中 10.000 为等待上限、2.010 为决策 tick 检测粒度，非延迟放大。

### 4.2 N 场说明

`20261012-150054` 共 5 次投递，全部一次获选（`REAL_SELECTION=5`、`WRITE_EXECUTED=5`），未出现重复投递，**因此本场不构成该修复点的验证**；其与修复不冲突，`TASK_SELECTION_TIMEOUT=0` 属正常。

### 4.3 未受影响项复核

| 项 | P 场 | N 场 |
|---|---|---|
| 写令顺序（Ranged task-1 先 / Infantry task-2 后） | 成立（9 条） | 成立（5 条） |
| 写令间隔最小值 | 2.950 s | 3.002 s |
| `PLAN_CHANGED` | 0 | 0 |
| `HANDOFF_DROPPED` | 0 | 0 |
| `PLAN_INVALIDATE_REJECTED` | 0 | 0 |
| `SNAPSHOT_REFUSED` | 0 | 0 |
| `EXIT_STOP_EXPIRED` | 0 | 0 |
| `EXIT_STOP_DONE` / `NOT_APPLIED` | 1 / 1 | 1 / 0 |
| `MISSION_CLEANUP` | 1 | 1 |

反馈按 TaskKind 分支仍成立：Hold → `hold_no_displacement_required`（`order_after=StandYourGround`）；Reposition → `closed_distance=3.18 / 3.17`，基线 `from=20.00` 取写令时位置；Engage → `engage_toward_enemy`（P `plan-5-task-2` 1.14、`plan-6-task-2` −1.32，均 <3.0 判 NotEffective）。

退出动作：N `plan-1-task-2-exit` 入队 10.548、获选 11.038、实际写出 `StandYourGround` 于 11.039，未超 1 s 安全时限；P `plan-3-task-1-exit` 写出于 92.086。

## 5. 归档

`E:\MBmod\CombatBrain2-evidence\20261012-p1-reverify3-sessions.zip`

- 280479 B / 46 条目（场次 `20261012-145644`、`20261012-150054` 全量 + `_p1_exclusive_P4.log`、`_p1_exclusive_N4.log`）
- SHA256 `91141A4ACE2530C06B545D4476D0C068606CFD77E442AE0ABCAD96D1FAF59051`（sidecar `.sha256` 已写）
- 逐文件对盘校验 **46/46 一致**
- `Program/_verify/sessions/INDEX.md` 已追加第三轮章节，上一批材料未重写

## 6. 未验证项与遗留说明

以下如实登记，不作为本轮通过依据：

1. **继续条件仍未实机触发。** P 场 `strength_ratio` 最低 0.778（4 次低于 0.80，均在 142.788 及以后，此时 `plan-4` 已于 139.934 关闭），N 场恒 0.919。按 `P1-CODEX-REVERIFY-3.md` §4，两场均不构成「活动计划跨越 70–80% 区间或 70% 兵力线仍继续」的证据。上一轮响应报告称 P 场 `strength_ratio` 恒 0.929 不准确，此处更正。
2. **目标读回缺失分支未在实机出现。** 两场共 5 条 Reposition 写令全部成功读回、误差 0.000；该分支仅由离线测试覆盖（缺口 1 的实机复核见 `P1-CODEX-REVERIFY-3.md` §1.1）。
3. **`SweepExpiredSelections` 仅在 `DecisionTick` 的条件未变分支被调用**（`TestDriver.cs:166`）。若同一决策 tick 内条件已变，则走 L171 的 `InvalidatePlan(ConditionChanged)` 后直接建新计划，超时检查被跳过。两条路径都会使旧计划失效，**不构成安全缺口**，故本轮按最小范围未改；此处登记以便后续判断是否需要统一失效原因优先级。
4. `Program/src/Host/BuildIdentity.g.cs` 为每次构建自动重写，未纳入本次提交。

## 7. 提交

- 实现：`fix(p1): anchor selection wait to first candidate submission`
- 文档：本文件、`Program/_verify/sessions/INDEX.md`
- 均未 push。

## 8. 三条战术局限（交付定位）

1. 远程占比是简化指标，忽略射程、弹药、质量与地形。
2. 「3 秒内移动 3 米」仅是当前测试场景的判据。
3. 资格失效只证明基本计划退出，不证明战术条件反馈有效。
