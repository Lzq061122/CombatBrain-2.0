# P1 六项修复 · 实机复验证据（第一轮）

修复依据：`Program/_verify/P1-CODEX-FIRST-LIVE-REVIEW.md`（commit `113dbb2`）§2 六项。
被复验的场次：`sessions\20261012-094559`（配置 P）、`sessions\20261012-094953`（配置 N）。
对照基线：修复前 `sessions\20261012-013102`（P）、`sessions\20261012-013618`（N）。

## 加载身份

| 项 | 值 |
|---|---|
| CombatBrain2.dll sha256 | `1CB4F9A264A8C408205E36B63FB1F62F47E18CD848783739B10EF6B8ED57DF53` |
| 大小 / 时间 | 83456 B / 2026-10-12 09:45:51 |
| 部署校验 | 构建产物与 `Modules\CombatBrain2\bin\Win64_Shipping_Client\CombatBrain2.dll` 摘要一致 |
| CBATH build | `b20261012-012421-305619bb`，sha256 `58d187b9ca0a3f564e10059abff286ef6220ed17d41eaba044260783140aadf5` |
| 两场编排结果 | 均 `RESULT: OK`，exit 0，无新增崩溃目录 |

## 逐项复验

### 修复 1 — 逐 tick 安全检查覆盖已写任务（对应 V10）

修复前 N 场：`PROBE_ELIGIBILITY_LOST … ai_ctrl=0 at=9.538` 之后计划仍开放到 `PLAN_CONTINUED … age=9.010 at=10.545`，即失资格后 4.005 s 未关闭。

修复后，两场均为**同一 tick 内完成关闭**：

```
P at=9.539   PROBE_ELIGIBILITY_LOST … ai_ctrl=0
P at=9.539   TASK_ELIGIBILITY_LOST task=plan-1-task-1 … reason=ai_control_lost source=tick_eligibility_check
P at=9.539   PLAN_INVALIDATED plan=plan-1 reason=FormationEligibilityLost

N at=15.542  PROBE_ELIGIBILITY_LOST … ai_ctrl=0
N at=15.542  TASK_ELIGIBILITY_LOST task=plan-2-task-1 … reason=ai_control_lost source=tick_eligibility_check
N at=15.542  PLAN_INVALIDATED plan=plan-2 reason=FormationEligibilityLost
```

结论成立点：
- 失资格到计划关闭的间隔由 4.005 s 降为 **0.000 s**，且不再依赖恢复引发的命令变化。
- 恢复日志 `plan_open_now=<none> same_plan_open=0`：旧计划在恢复之前就已关闭，恢复动作的副作用不可能再被当作取消原因。
- 资格变化没有被伪装成兵力变化：`TASK_ELIGIBILITY_LOST` 的触发源是 `source=tick_eligibility_check`，与 `PLAN_CONDITION_LOST`（远程/兵力比例）分开。

### 修复 2 — 延迟必须在写令前生效（对应 V9）

修复前：P 首令延迟/编队间隔 0.014/0.043 s，N 为 0.063/0.441 s。

修复后（普通任务，退出任务不适用）：

| 场 | 首令 | 延迟 | 第二条 | 编队间隔 |
|---|---|---|---|---|
| P | plan-1-task-2 at=3.041 | 1.505 | plan-1-task-1 at=4.585 | 1.544 |
| N | plan-1-task-2 at=3.039 | 1.502 | plan-1-task-1 at=4.592 | 1.553 |

`P1_FIRST_DECISION_SCHEDULED … first_command_delay=1.500`，且写令前有显式延后记录：

```
[WRITE_DELAYED] task=plan-1-task-1 plan=plan-1 key=1/1 reason=first_command_delay at=1.588
[WRITE_DELAYED] task=plan-1-task-2 plan=plan-1 key=1/0 reason=first_command_delay at=2.035
```

两项均 ≥ 1.5 s，满足 V9。

### 修复 3 — 退出动作生命周期与跨计划隔离（对应 V11）

- 一次退出 Stop 写出后关闭：P 有 `EXIT_STOP_DONE = 1`。
- 未获选的退出动作到期关闭，不再被 `SubmittedAt` 永久跳过：`EXIT_STOP_NOT_APPLIED … reason=not_selected_in_time submitted=0 waited=1.006`（P 1 条、N 2 条）。
- 旧计划退出任务反馈取消新计划的现象消失：两场 `TASK_FEEDBACK … -exit … state=failed = 0`。
- 跨计划守卫开销：`PLAN_INVALIDATE_REJECTED = 0`、`belongs_to_other_plan = 0`（未出现误伤）。

### 修复 4 — 反馈按 TaskKind 判定（对应 V6）

| 场 | 任务 | kind | 判定 | detail |
|---|---|---|---|---|
| P | plan-1-task-2 | Engage | effective | `engage_toward_enemy=4.57` |
| P | plan-1-task-1 | Reposition | effective | `projected_displacement=3.02 distance=21.98 from=25.00 closing=1` |
| P | plan-6-task-1 | Reposition | effective | `projected_displacement=3.02 distance=5.62 from=8.64 closing=1` |
| N | plan-1-task-1 | Hold | effective | `hold_no_displacement_required` |
| N | plan-1-task-2 | Reposition | failed | `projected_displacement=2.51 (<3.0) distance=19.20 from=20.00 closing=1` |

- Engage 不再落入 Hold 分支，改用朝敌接近量判定（`engage_toward_enemy`）。
- Reposition 使用「实际写令时位置」为基线（`from=` 与写令读回一致）、并同时要求投影达标与 `closing=1`。
- 无位置任务的 `hold_no_displacement_required` 仅在 kind=Hold 时出现。

### 修复 5 — 继续条件与控损（对应 V8、V10 之外的条件语义）

修复前 P 在 `strength_critical=1` 时仍创建 `exploit_ranged_advantage`。修复后：

```
P at=104.506  PLAN_CONDITION_LOST plan=plan-8 old=… strength_ratio=0.759 strength_critical=0
                              new=… strength_ratio=0.693 strength_critical=1
P at=106.665  PLAN_CREATED plan=plan-9 goal=hold_observation reason=strength_critical
                              ratio=0.568 line=0.70 strength_critical=1
P at=106.665  TASK_CREATED task=plan-9-task-1 kind=Hold reason=preserve_power_ranged_hold
P at=106.665  TASK_CREATED task=plan-9-task-2 kind=Hold reason=preserve_power_infantry_hold
```

- `strength_critical=1` 时不再走 `exploit_ranged_advantage`，改走 `hold_observation` + `preserve_power_*_hold`。
- 继续判定与创建判定分离：`PLAN_CONDITION_LOST` 携带 old/new 两组条件，而非一次创建时的布尔快照。

### 修复 6 — 每任务独立写入计数

`WRITE_EXECUTED` 的 `write_attempts_task=` 已改为真实写入次数（`TaskLedger.WritesOf`），与 `BlockedAttempts` 分离。
两场 `WRITE_BLOCKED = 0`、`WRITE_EXECUTED` P 10 / N 4，写入计数用于核对「候选零写入」与「已执行一次」两种情况，不再用阻塞次数冒充。

## 用户四条硬约束复核

| 约束 | 证据 |
|---|---|
| ① 每任务独立去重、独立记账 | `ExecutionGate._states` 按 taskId 键控；测试 `WriteCreditIsIndependentPerTask`、`ClosingOneTaskDoesNotAffectAnother` 全绿 |
| ② 安全取消逐 tick 检查；紧急退出不受正常指挥延迟约束 | `CheckConditionsEachTick` 先于快照新鲜度检查执行；退出任务不受 `WriteDelayBlock` 约束，资格失效同 tick 关闭 |
| ③ 命令未生效或被覆盖必须触发计划处置 | N 场 `PLAN_INVALIDATED reason=NotEffective detail=task=plan-1-task-2`；P 场 `reason=FormationEligibilityLost`。不是仅打印日志 |
| ④ 先取得稳定与生效证据，再触发资格失效 | 资格失效触发要求计划年龄 ≥8 s 且至少 2 个已写普通任务达到 Effective；P/N 均在写令与生效之后才失效 |

## 尚未判定项

- `REVOKE_EXECUTED` 两场均为 0（`REVOKE_SKIPPED` P 9 / N 4）。撤权计划已有记录，但未真正执行过，属既有未验证项，本轮不宣称通过。
- 后续计划变更（P `PLAN_CREATED` 9 / `PLAN_INVALIDATED` 9 / `PLAN_CHANGED` 1）需按各自实际原因逐条复核，不能按整场数量判失败。
- V1～V14 的最终判定归独立验收方。
