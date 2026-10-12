# P1 第四轮复核响应

对 `../P1-CODEX-REVERIFY-4.md`（提交 `b262053`）§2 的整改与复验记录。
判据：`../P1-MINIMAL-LOOP-SCRIPT.md` v1.3 §5.2/L191、§6.3。本文件不预先登记 P1 阶段通过。

## 1. 结论摘要

- 第四轮 §2 指出的两处执行边界缺口已修复：到期关闭改为每 tick，截止后才首次获选不得取得写令资格。
- §1 已通过的首次提交锚点未重开；读回缺失、独立退出动作等已通过项未重开；未增加验收项目。
- 离线用**受审构建产物**复现第四轮 §2.2 原反例，已由 `Allowed=true` 变为 `selection_window_closed`。
- **两场实机均未触发该路径**（投递后 13–500 ms 内获选），故本批实机不构成该修复点的触发证据；证据为离线反例与单元测试。此点如实登记。

## 2. 整改内容

第四轮 §2 指出：停止重复投递不能代替关闭已 arm 的候选；期间任务仍有获选资格，且截止后的晚到获选能进入写令门（离线反例：首次 `SubmitCandidate=10`、重投 `13`、`22` 才 `MarkSelected`，`RequestWrite` 返回 `Allowed=true`）。

### 2.1 到期关闭改为每 tick（§2.1）

修改前 `SweepExpiredSelections` 只在 `DecisionTick` 的「计划条件未变」分支调用（3 s 节奏），实际关闭延迟 2.010 s。

- `Program/src/Host/TestDriver.cs`：`FeedbackTick` 内 `CheckConditionsEachTick(now)` 之后新增 `SweepExpiredSelections(now);`，与既有逐 tick 安全检查同一位置。
- `SweepExpiredSelections` 注释更新：逐 tick 调用，到期的第一个现有主线程 tick 即完成关闭，不等 3 s 决策节奏。
- 保留 `if (task.SelectedAt >= 0.0) continue;`：截止前已真实获选的正常任务按原执行规则，不按等待期限掐断。

### 2.2 截止成为写令执行边界（§2.2）

新增判据 `SelectionWindowClosed(task, now)`：非退出任务、已投递、且 `now >= SelectionDeadline()`。

四处防线：

1. **真实获选入口** — `OnActiveBehaviorChanged` 在 `MarkRealSelection` 之前：`if (task.SelectedAt < 0.0 && SelectionWindowClosed(task, SafeTime(mission)))` 则记 `REAL_SELECTION_REJECTED reason=selection_window_closed` 并返回，不进入执行状态。
2. **最终写令门（Host）** — `TryExecuteTask` 在退出动作过期检查之后、`RequestWrite` 之前：`if (task.SelectedAt >= task.SelectionDeadline())` 则关闭任务与门状态，记 `WRITE_REJECTED reason=selection_window_closed` 并返回。
3. **最终写令门（Core）** — `ExecutionGate.RequestWrite` 在 `not_selected` 之后新增复核：`if (s.SelectedAt >= s.SelectionDeadline) { d.BlockReason = "selection_window_closed"; return d; }`。该防线使离线直接调用受审 DLL 也保持同一语义。
4. **截止来源固定** — `IntentState` 增加 `internal double SelectionDeadline`，仅在首次 `SubmitCandidate` 时置 `missionTime + TestParameters.SelectionWaitSeconds`，重投不刷新，与 `FormationTask` 同语义。

退出动作的 1 s 规则保留不变（`SelectionWindowClosed` 对退出任务直接返回 false）。

## 3. 离线验证（受审构建产物）

### 3.1 复现第四轮 §2.2 原反例

用 `System.Reflection.Assembly.LoadFrom` 加载本次构建产物 `Program/bin/Debug/CombatBrain2.dll`（97792 B，SHA256 `856AFD22AC81762871D4BAFDDEF3C32065ECED9528C3CB46EAAF51A0AA9225DA`，与部署到游戏的 N 场载入值一致），反射调用 `CombatBrain2.Core.ExecutionGate`：

| 场景（首次提交 10，截止 20） | Allowed | BlockReason |
|---|---|---|
| 获选 22（截止后 2 s） | **False** | `selection_window_closed` |
| 获选 19.5（截止前 0.5 s） | True | — |
| 获选 10（提交同刻） | True | — |

第一行即第四轮报告记录的 `Allowed=true` 反例，现已拒绝；后两行为区分性对照，证明截止前获选不被误拒。

### 3.2 单元测试

`Program/tests/ExecutionGateTests.cs` 新增两条，并做红→绿确认：

- `LateSelectionAfterDeadlineIsRejectedAtWriteGate` — 首次提交 10、重投 13、获选 22，期望 `Allowed=false`、`BlockReason=="selection_window_closed"`、`WriteAttempts==0`。
- `SelectionBeforeDeadlineStillWritesAfterLongWait` — 获选 19.5、25.0 写令，期望 `Allowed=true`。

敏感性：把 `if (s.SelectedAt >= s.SelectionDeadline)` 临时替换为 `if (false)`，`LateSelectionAfterDeadlineIsRejectedAtWriteGate` 随即 FAIL（`Assert.False() Failure`，1 失败 91 通过）；恢复后全绿。

结果：测试 **92 通过 / 0 失败**；`dotnet build Program\CombatBrain2.csproj -c Debug` 0 警告 0 错误。

## 4. 实机复验

经共享游戏协调入口串行执行（`Owner=主线组`），完整流程（部署 → CBATH 采集 → 清理），两场均 `RESULT: OK`、exit 0。

| 场次 | 配置 | build_id | 载入 sha256 | 大小 |
|---|---|---|---|---|
| `Program/_verify/sessions/20261012-152820` | P | `b20261012-152815-b2152454` | `E9127151E0CDAD38C36E9A32740F0DA5604047A0C62D61B540E447B0BF87A9FC` | 97792 B |
| `Program/_verify/sessions/20261012-153146` | N | `b20261012-153145-85ff2ec0` | `856AFD22AC81762871D4BAFDDEF3C32065ECED9528C3CB46EAAF51A0AA9225DA` | 97792 B |

### 4.1 本路径未被实机触发（如实登记）

| 项 | P `20261012-152820` | N `20261012-153146` |
|---|---|---|
| `TASK_CANDIDATE_SUBMITTED` | 7 | 5 |
| `REAL_SELECTION` | 6 | 5 |
| `WRITE_EXECUTED` | 6 | 5 |
| 投递→获选延迟 | 22–50 ms | 13–477 ms |
| `TASK_SELECTION_TIMEOUT` | 0 | 0 |
| `REAL_SELECTION_REJECTED` | 0 | 0 |
| `WRITE_REJECTED` | 0 | 0 |

两场每一条候选都在 0.5 s 内获选，因此超时与晚到获选分支都没有机会出现。对比第三轮 P 场 `20261012-145644`（同配置）曾在 `plan-4-task-1` 出现 4 次重投与超时，本轮该任务 19.559 投递、19.581 获选（22 ms）。这是原生获选行为的场间差异，不是整改结果。

**因此本批实机不构成该修复点的触发证据。** 该修复点的证据是 §3 的离线反例（受审产物）与单元测试。

### 4.2 未受影响项复核

| 项 | P | N |
|---|---|---|
| 写令顺序 Ranged(task-1) → Infantry(task-2) | 成立 | 成立 |
| `PLAN_CHANGED` | 0 | 0 |
| `HANDOFF_DROPPED` | 0 | 0 |
| `PLAN_INVALIDATE_REJECTED` | 0 | 0 |
| `SNAPSHOT_REFUSED` | 0 | 0 |
| `EXIT_STOP_QUEUED` / `DONE` / `NOT_APPLIED` | 2 / 1 / 1 | 1 / 1 / 0 |
| `MISSION_CLEANUP` | 1 | 1 |

退出动作按既定时限：N `plan-1-task-2-exit` 10.553 投递、11.029 获选、11.036 写出 `StandYourGround`。

## 5. 归档

`E:\MBmod\CombatBrain2-evidence\20261012-p1-reverify4-sessions.zip`

- 244956 B / 46 条目（场次 `20261012-152820`、`20261012-153146` 全量 + `_p1_exclusive_P5.log`、`_p1_exclusive_N5.log`）
- SHA256 `D08A4699795AC5B2F9C7FCCC54169DEA79A895149AD6F9926C00700DBB267D27`（sidecar `.sha256` 已写）
- 逐文件对盘校验 **46/46 一致**
- `Program/_verify/sessions/INDEX.md` 已追加第四轮章节，前几批材料未重写

## 6. 未验证项与遗留说明

1. **本轮修复点无实机触发证据**（见 §4.1），仅有离线反例与单测。若需实机证据，须等到原生获选延迟超过 10 s 的场次，不能人为构造。
2. **继续条件仍未实机触发。** P `strength_ratio` 0.929（本场与第三轮一致）、N 0.919，均在 (0.80, 1.0] 内，两场都未跨越 70–80% 区间或 70% 兵力线。
3. 第四轮 §4 明确不要求统一 `ConditionChanged` 与超时的失效原因优先级，本轮未改该处。
4. `Program/src/Host/BuildIdentity.g.cs` 为每次构建自动重写，不纳入提交。

## 7. 三条战术局限（交付定位）

1. 远程占比是简化指标，忽略射程、弹药、质量与地形。
2. 「3 秒内移动 3 米」仅是当前测试场景的判据。
3. 资格失效只证明基本计划退出，不证明战术条件反馈有效。
