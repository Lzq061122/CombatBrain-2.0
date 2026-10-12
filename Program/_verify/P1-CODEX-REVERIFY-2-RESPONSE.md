# P1 第二轮复核的整改响应

本文回应 `P1-CODEX-REVERIFY-2.md`（提交 `1660069`）§3 的两处缺口，并更正该复核 §4 指出的本文前身错误。判据仍为 `P1-MINIMAL-LOOP-SCRIPT.md` v1.3。本文是实现方响应，**不是独立验收结论**。

## 1. 结论摘要

- 两处缺口均已修复，并各自取得实机证据（§3、§4）。
- 复验按原 §5.5 经共享协调入口串行执行两场：配置 P `20261012-142820`、配置 N `20261012-143135`，均 `RESULT: OK`、exit 0。
- 两场均以完整流程执行：部署 → 摘要核对 → CBATH 启动与采集 → 证据保存与清理，全部在同一个共享锁内。
- 继续条件在两场**均未被实机触发**（§6.4），不得按已验证登记。

## 2. 修复内容

### 2.1 缺口 1：没有读回目标仍判生效

`Program/src/Core/TaskFeedback.cs`，Reposition 分支原为：

```csharp
double readback = task.OrderTargetReadbackError();
if (readback >= 0.0 && readback > TestParameters.TargetPointToleranceMeters)
```

`OrderTargetReadbackError()` 在 `HasOrderTarget=false` 时返回 `-1.0`，负值落入 `readback >= 0.0` 为假的分支，于是直接进入距离进度判断并可能判生效。改为：

```csharp
double readback = task.OrderTargetReadbackError();
if (readback < 0.0)
{
    detail = "order_target_unreadable";
}
else if (readback > TestParameters.TargetPointToleranceMeters)
{ ... }
```

读回缺失时不再借缺失值继续距离判定；该状态在反馈窗口内保持 Pending，到 `FeedbackDeadline()` 后由文件末尾统一给出 Failed + `NotEffective`。

### 2.2 缺口 2：已获选的退出动作被普通任务兜底门丢弃

`Program/src/Host/TestDriver.cs` 的 `DrainPendingCommand`，原兜底门在计划已关闭且 PlanId 相同时丢弃待写任务，未区分独立退出动作。改为前置 `!BattlePlan.IsExitTask(task)`：

```csharp
var plan = _plans.Current;
if (!BattlePlan.IsExitTask(task) &&
    plan != null && plan.IsClosed &&
    string.Equals(plan.PlanId, task.PlanId, StringComparison.Ordinal))
```

普通旧任务继续受关闭计划门阻断；独立退出动作改由自身身份、来源与 1 s 截止判定，`TryExecuteTask` 中既有的 `EXIT_STOP_EXPIRED` 门仍在最终 setter 前拦一次。未恢复旧普通任务，未添加外部强写。

### 2.3 测试

- 新增 `TaskSelectionDeadlineTests.RepositionWithoutOrderTargetReadbackIsNeverEffective`：构造 Reposition 且不调用 `SetOrderTarget`，断言窗口内 Pending 且 Detail 为 `order_target_unreadable`，过期后 Failed + `PlanInvalidReason.NotEffective` + 同 Detail。
- 新增 `ExecutionGateTests.IndependentExitTaskIsDistinguishableFromNormalTask`。
- 套件 **88 通过 / 0 失败**。
- 缺口 1 做过红→绿敏感性验证：把 `if (readback < 0.0)` 临时改成 `if (false)` 后该测试失败（1 失败 / 86 通过），恢复后 88 通过，证明该测试确实能抓住旧行为。

## 3. 缺口 1 的实机证据

两场写令的读回字段：

| 场次 | 任务 | 类别 | `readback_error` |
|---|---|---|---|
| P | `plan-1-task-1` | Reposition | 0.000 |
| P | `plan-3-task-1` | Reposition | 0.000 |
| N | `plan-1-task-2` | Reposition | 0.000 |
| N | `plan-4-task-2` | Reposition | 0.000 |
| N | `plan-5-task-2` | Reposition | 0.000 |

读回成立时判生效是合法的；本条修复约束的是读回**不成立**的分支，该分支未在两场出现，故实机证据限于"读回路径正常工作、未误判"。

## 4. 缺口 2 的实机证据

N 场正是上一轮的反例场，本次走完整链：

```
[EXIT_STOP_QUEUED] task=plan-1-task-2-exit ... origin_plan=plan-1 at=10.269
[REAL_SELECTION]   task=plan-1-task-2-exit key=1/0 obj=0280df8b@Infantry at=11.031
[EXIT_STOP_DONE]   task=plan-1-task-2-exit ... order_after=StandYourGround at=11.038
```

入队到写出 0.769 s，未超 1 s 期限，实际写出 `StandYourGround`。P 场同类实例：

```
[EXIT_STOP_QUEUED] task=plan-3-task-1-exit ... origin_plan=plan-3 at=94.536
[EXIT_STOP_DONE]   task=plan-3-task-1-exit ... order_after=StandYourGround at=95.089
```

两场 `HANDOFF_DROPPED=0`、`EXIT_STOP_EXPIRED=0`、`PLAN_INVALIDATE_REJECTED=0`。

未获得的退出实例仍按既定时限处置：P `plan-1-task-2-exit`（`waited=1.004`）与 N `plan-4-task-1-exit`（`waited=1.003`）均以 `EXIT_STOP_NOT_APPLIED reason=not_completed_in_time` 关闭并撤权，未跨入后续计划。

## 5. 已通过项的复核（未重开）

- **发令顺序**：两场每个计划均先 Ranged（task-1）后 Infantry（task-2）。P 写令序列 `plan-1-task-1` 4.586 → `plan-1-task-2` 8.043 → `plan-3-task-1` 17.085 → `plan-3-task-2` 20.037；N 为 `plan-1-task-1` 4.589 → `plan-1-task-2` 8.042 → `plan-4-task-1` 19.589 → `plan-4-task-2` 23.035 → `plan-5-task-1` 32.087 → `plan-5-task-2` 35.040。
- **写令延迟**：P 首令延迟 3.014 / 3.491；N 由 `WRITE_DELAYED` 拦截 9 次（`first_command_delay` 6、`predecessor_not_written` 3）。全部 ≥ 1.5 s。
- **资格**：N 场 `PROBE_ELIGIBILITY_LOST` 1 次，随后 `PLAN_INVALIDATED reason=FormationEligibilityLost` 同 tick 关闭本计划。
- **清理**：两场 `MISSION_CLEANUP=1`，无新崩溃目录，无残留 Bannerlord 进程。

## 6. 更正

### 6.1 载入身份
本轮两场载入模块 SHA256 分别为 P `36545E1984F92FF76817D4D25D74243946D1E86BA691CBFE61AC5AEBFF444D8D`、N `3A3D66E7282280D8742A1DB10848CD6AD34ED9CB89C45967A587FF1AEC0EB7E9`；两者均为 96256 B，且各自与部署文件摘要一致。同源码重复构建摘要不同，判定源码版本须同时给 MVID 或 build_id。

### 6.2 前身报告的错误
复核 §4 指出的下列错值以本轮数据与复核结论为准，前身报告不再作为证据：部署 DLL 字节数（应为 96256）、`closed_distance` 示例取值、N 场退出任务 `selected` 取值、继续条件触发场次、以及归档覆盖清单。

### 6.3 继续条件未被触发
两场 `BATTLE_STATE` 的 `strength_ratio`：P 恒 0.929，N 恒 0.919；落入 (0.70, 0.80) 的样本数均为 0。两场 `PLAN_CONDITION_LOST=0`、`PLAN_CHANGED=0`。因此**继续条件的实机触发仍无证据**，本轮不登记为已验证。

## 7. 未验证项

- 读回**不成立**分支未在实机出现（§3）。
- 继续条件未在两场被触发（§6.3）。
- 70–80% 区间与 70% 继续兵力线只有离线测试覆盖。
- 实现产物含观测组适配器（已由观测组维护，不计入本轮两处修复的贡献）。
- 本文是实现方响应，`RESULT: OK` 与 exit 0 只是编排结果，不能替代 V1–V14 或 P1 阶段判定。

## 8. 归档

- 外置归档：`E:\MBmod\CombatBrain2-evidence\20261012-p1-reverify2-sessions.zip`，309541 B / 46 条目，SHA256 `AD10F24223A864C481D1A8A7BCCECBF72534996B48DAF12C47866A60CADEF9C7`。
- 逐文件对盘 **46/46 MATCH**，覆盖两个场次目录与 `_p1_exclusive_P.log`、`_p1_exclusive_N.log`。
- `Program/_verify/sessions/INDEX.md` 已追加第三轮章节；上一批场次材料未重写。
