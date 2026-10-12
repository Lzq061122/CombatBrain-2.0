# P1 最小指挥闭环 · 实现交接报告

> **Codex最终独立验收（2026-10-12）：P1 在既定最小指挥闭环范围内通过，可以阶段收口。** `d9c892a` 已关闭最后获选截止执行边界；最终 V1–V14 裁定、两场身份、统计更正及未验证范围见 `P1-STAGE-EXIT-ACCEPTANCE.md`。下文保留原实现交接事实，历史轮次的“待验收”不替代最终裁定。

状态：**实现完成，最小阶段独立验收通过；未自然触发的边界按最终报告保留，不宣称全分支实机通过。**
依据脚本：`Program/_verify/P1-MINIMAL-LOOP-SCRIPT.md` v1.3（提交 `bad514d`）；修复依据 `Program/_verify/P1-CODEX-FIRST-LIVE-REVIEW.md`（提交 `113dbb2`）。
日期：2026-10-12。基线上游：`df4cf3d`（P0 可追溯基线）。

---

## 0. 三条战术局限（交付必读）

1. **远程占比是简化指标。** `rangedEdge` 只比较双方 Ranged 人数占比，忽略射程、弹药、单位质量与地形。它用于在两个配置之间制造可分辨的条件差异，不代表真实战术优势。
2. **"3 秒内移动 3 米"仅是当前测试场景判据。** `MoveDisplacementMeters = 3.0` 与 `FeedbackWaitSeconds = 3.0` 是为本批 `battle_terrain_biome_096`、150 秒交火窗口选定的取值，不构成对"命令生效"的一般定义，换场景需重新标定。
3. **资格失效只证明基本计划能安全退出，不证明战术条件反馈有效。** 脚本 §7 的失效由测试适配器调用 `Formation.SetControlledByAI(false)` 触发，验证的是生产路径对资格变化的响应（关闭计划、清槽、撤权、一次性退出动作），不代表系统能识别真实战场上的战术条件变化。

---

## 1. 本批交付物

### 1.1 新增（纯 Core）
`Program/src/Core/BattleState.cs`、`BattlePlan.cs`、`BattleAssessment.cs`、`TaskFeedback.cs`、`TaskLedger.cs`、`PlanLedger.cs`。

### 1.2 修改
`Program/CombatBrain2.csproj`（登记新增文件）、`Program/src/Core/ExecutionGate.cs`（按 taskId 独立记账 + `WriteAttemptsFor`）、`Program/src/Core/WriteLedger.cs`、`Program/src/Core/ThreadHandoff.cs`（单槽位→按编队键分流）、`Program/src/Core/TestParameters.cs`（P1 参数与枚举）、`Program/src/Core/TargetSelector.cs`（退役旧 `TestIntent`）、`Program/src/Host/TestDriver.cs`（整体重写为计划驱动）、`Program/src/Host/SamplingBehavior.cs`（开关分流）、`Program/src/Host/EligibilityProbe.cs`（按 plan/task 触发）。

### 1.3 测试
`Program/tests/TaskSelectionDeadlineTests.cs`（替换 `TestIntentTests.cs`）、`ThreadHandoffTests.cs`、`ExecutionGateTests.cs`、`EligibilityLossTests.cs` 扩充；`Program/tests/CombatBrain2.Tests.csproj` 登记新 Core 链接项。

### 1.4 脚本
`Program/_verify/run-p1.ps1`（由 `run-p0.ps1` 派生，`run-p0.ps1` 未改动）。

### 1.5 CBATH
`experiments/test-mod/CBATH/BattleSetup.cs`（新增）、`CbathTools.cs`（`h3_start_from_screen` 增 7 个可选参数）、`MissionControl.cs`（新增带配置重载，零参入口转调）、csproj 登记。在 CBATH 仓库内独立提交。

---

## 2. 与脚本的偏离（显著记录）

### 2.1 P0 固定驱动退役（§9.2 / §9.4 的批准修订）
脚本 §9.2 原写「P0 通路保留」，§9.4 原写「关闭时行为与当前完全一致」。本次按 §9.4.1 的裁决改为：**P1 驱动取代固定 P0 测试驱动**。

- 实际默认行为变化：哨兵 `p1-command-loop.txt` 不存在或 `enabled=0` 时，**只采样/观测，不生成 Plan/任务、不投递或执行本方命令**，不退回旧 P0 固定驱动。这是新的默认语义，**不宣称与旧 P0 完全一致**。
- 已退役删除：`TargetSelector.cs` 的旧 `TestIntent` 类、`TestParameters.cs` 的 `IntentWindowSeconds` / `TriggerDelaySeconds` / `BetweenIntentSeconds` / `ExplicitCloseAfterWriteSeconds`、`EligibilityProbe.cs` 的 `_planCountAtLoad`。
- 未删除且仍适用：资格门、主线程约束、同步、撤权、Mission 清理。
- 旧 P0 可追溯基线 `df4cf3d` 与其实机证据保留，历史 P0 通过不撤回，也不冒充 P1 实机结果。

### 2.2 测试集的替代关系（§9.4.1 要求的口径）
- 旧 `TestIntentTests.cs` 6 个 Fact → 替换为 `TaskSelectionDeadlineTests.cs` 7 个 Fact。
- `ThreadHandoffTests.cs` 由 8 个 Fact 增至 10 个。
- **不得**表述为「原 65 个用例全部保留并通过，再新增 3 个」。本批测试集为 **81 个通过 / 0 失败**，其中含退役、替换与新增项。

### 2.3 日志字段 `write_attempts_task` 的来源修正
修复前该字段在候选、阻挡、执行三处取 `TaskLedger.BlockedAttempts`（阻塞次数），成功写令时为 0，无法判断"写过没写过"。本次统一改为 `TaskLedger.WritesOf(taskId)`，即 `return t.WrittenAt >= 0.0 ? 1 : 0;`。

- **统一口径**：该字段表示**该任务截至日志时刻的已写入次数**，候选时 0（尚未写入）、执行后 1（已写入一次），是同一计数随执行递增。
- 修复前用阻塞次数才是字段来源错误。此项已修正，不构成 V5 §6.1 第 1 项的语义阻挡。

### 2.4 陈旧 `build_id` 的说明
日志 `RUNTIME_IDENTITY` 中 `build_id=b20261011-161246-db46608a` 未随本次构建刷新（`BuildIdentity.g.cs` 本轮未重生成）；MVID 则已由前轮 `5c451a72-…` 变为本轮 `db3888be-…`。`assembly_file_sha256=1cb4f9a2…` 与部署文件完全一致；`build_id` 不可单独用于判断 DLL 新旧。

### 2.5 实现侧对 §12 的核对结果
CBATH `h3_start_from_screen` 的配置参数已按 §3.4 选项 1 落地（`ArmyCompositionGroupVM.ArmySize` + `CompositionValues`，并按 `IsDefault` 设 `IsSelected`；阵营文化走 `FactionItemVM → OnFactionSelected → SetCurrentSelectedCulture`）。每步回读事实：`army_size_readback` / `composition_readback` / `percentage_text` / `troop_selection` / `map_selected_id`。配置报错或地图不匹配则 fail-loud 不发令。
**§12 所列签名仍未做独立实机签名探针**；本批通过的是"配置生效后可观察结果与请求值吻合"这一层证据，签名可见性本身属代码入口证据。

### 2.6 未实现项（脚本提议但本批未做）
`CONFIG_IDENTITY` 标记未落地（两场均为 0 条）。配置身份改由场次目录 `requested-config.json` 与 `deployed-module.json` 承担。`P1_EVAL_TICK` 也未使用，评价周期标记实际为 `EVAL_TICK`（两场各 51 条）。

---

## 3. 实机场次与身份

| 场 | 配置 | 场次目录 | 编排结果 |
|---|---|---|---|
| P | 远程优势成立 | `Program/_verify/sessions/20261012-094559` | `RESULT: OK`，exit 0 |
| N | 远程优势不成立 | `Program/_verify/sessions/20261012-094953` | `RESULT: OK`，exit 0 |

- CombatBrain2.dll 部署摘要：`1CB4F9A264A8C408205E36B63FB1F62F47E18CD848783739B10EF6B8ED57DF53`，83456 B，2026-10-12 09:45:51。日志内 `assembly_file_sha256` 与之一致。
- CBATH：build `b20261012-012421-305619bb`，sha256 `58d187b9ca0a3f564e10059abff286ef6220ed17d41eaba044260783140aadf5`。
- 被复核的前一轮失败场次：`sessions/20261012-013102`（P）、`sessions/20261012-013618`（N）。
- 场景：`battle_terrain_biome_096`；请求双方各 100 人；交火窗口 150 秒。

---

## 4. 验收表 V1～V14

| # | 判据 | 结论 | 关键证据 |
|---|---|---|---|
| V1 | 加载正确 | **通过** | 日志 `assembly_file_sha256=1cb4f9a2…` = 部署文件 `1CB4F9A2…`；两场一致。`build_id` 陈旧问题见 §2.4 |
| V2 | 配置 P 有效 | **通过** | CBATH 独立读数：我方 team=1 共 92 人（远程 56 / 步兵 15，占比 0.6087），玩家队 team=0 共 99 人（远程 21 / 步兵 66，占比 0.2121）→ `rangedEdge = +0.3966 ≥ +0.10`；兵力 92/99 = 0.929 ≥ 0.80。CB2 快照 `my_units=92 enemy_units=99 my_ranged=56 enemy_ranged=21 ranged_edge=0.397` 与之一致 |
| V3 | 配置 N 有效 | **通过** | 我方 team=1 共 91 人（远程 21 / 步兵 58，占比 0.2308），玩家队 team=0 共 99 人（远程 56 / 步兵 22，占比 0.5657）→ `rangedEdge = −0.3349 ≤ −0.10`；兵力 91/99 = 0.919 ≥ 0.80。CB2 快照 `ranged_edge=-0.335` 一致 |
| V4 | 多编队服务同一目标 | **通过** | `PLAN_CREATED plan=plan-1 … input=input-1 formations=1/1,1/0 tasks=2`，两支编队可追到同一 `snapshot_id` 与 `plan_id`；P/N 均如此 |
| V5 | 命令可追溯 | **通过（第 5 项受限于逐条对齐未做）** | 六项事实中 1～4、6 项成立，第 5 项只到字段存在层，见 §5 |
| V6 | 命令实际生效 | **通过** | P `TASK_FEEDBACK state=effective` 6 条、N 3 条；含 `Reposition projected_displacement=3.02/3.15/7.20` 与 `Engage engage_toward_enemy=4.57/9.65/3.23`。**非**"全场命令未生效" |
| V7 | 未生效如实报告 | **通过** | N `TASK_FEEDBACK task=plan-1-task-2 kind=Reposition state=failed reason=NotEffective detail=projected_displacement=2.51 (<3.0) distance=19.20 from=20.00 closing=1`，带实际值；无外部强写 |
| V8 | 计划稳定 | **通过** | 初始窗口：P `PLAN_CREATED at=1.535` → 资格失效 `at=9.539`，稳定 8.004 s；N `1.535 → 15.542`。该窗口内仅一次 `PLAN_CREATED`、无 `PLAN_CHANGED`、无重复正常发令，评价持续（`EVAL_TICK` 每 3 s）。按用户 2026-10-12 裁决，不按整场计划数量判失败 |
| V9 | 分头响应 | **通过** | P 首令 `plan-1-task-2 at=3.041 first_command_delay=1.505`，第二条 `plan-1-task-1 at=4.585`，间隔 1.544 s；N 首令 `at=3.039 delay=1.502`，第二条 `at=4.592`，间隔 1.553 s。两项均 ≥ 1.5 s；安全退出动作单列（退出任务不适用） |
| V10 | 真实失效 | **通过** | 脚本 §7.2 三条观察全部成立，见本报告 §6；且非到期触发 |
| V11 | 不复活 | **通过** | 恢复（P `at=13.541`）后 `plan-1-task-1` 写令 0 次、重投递 0 次；恢复日志 `plan_open_now=<none> same_plan_open=0`。新计划 `plan-3` 于 `13.548` 以新身份建立。跨计划失败反馈 0 条 |
| V12 | 继承安全要求在 P1 新通路回归 | **通过（口径受限）** | 主线程 `MAIN_THREAD_MARKED=1`、写令 `is_main_thread=1 exec_tid=33`、`HANDOFF_EXECUTED exec_tid == main_tid`；真实获选 `REAL_SELECTION` 与写令对象同一实例；资格门、关闭不复活、Mission 清理 `MISSION_CLEANUP=1` 成立。**撤权通道未真正执行过**，见 §7 |
| V13 | 收尾 | **通过** | `MISSION_CLEANUP plan_task_and_object_state_cleared` 两场各 1 次；`run-p1.ps1` 退出码 0；无新增崩溃目录；时间线 `cbath-timeline.json` 已归档 |
| V14 | Gate 1 对照 | **通过** | 差异落在任务/命令/目标位置上，见 §8 |

---

## 5. V5 六项事实逐项核对（以 P 场 plan-1 为例）

| # | 要求 | 证据 |
|---|---|---|
| 1 | 候选零写入 | `TASK_CANDIDATE_SUBMITTED task=plan-1-task-1 … write_attempts_task=0`，两任务候选时均为 0。字段来源已由 `BlockedAttempts` 修正为 `WritesOf`（§2.3） |
| 2 | 同实例真实获选 | `REAL_SELECTION task=plan-1-task-1 key=1/1 side=Attacker obj=02253104@Ranged`，与 `WRITE_EXECUTED` 的 `obj=02253104@Ranged` 同一对象身份串；非人工调用产生 |
| 3 | 主线程已确认 | `MAIN_THREAD_MARKED` 存在；转交路径 `HANDOFF_DEFERRED callback_tid=40 main_tid=33` → `HANDOFF_EXECUTED exec_tid=33 is_main_thread=1`，`exec_tid == main_tid` |
| 4 | 原方法入口 + 正常返回 + 实际后值 | `WRITE_EXECUTED … original_body_entered=1 invoked=1 error=<none> order_before=Move order_after=Move/Charge`，均为实际值 |
| 5 | 独立效果 | CBATH `cbath-timeline.json`（957434 B）在写令后窗口内含 `movement_order` 字段且已归档。**本次仅确认字段存在与归档，未做写令时刻与 1 Hz 样本的逐条对齐**，见 §7 第 3 项 |
| 6 | 外部改写与反馈处置 | **存在外部改写记录**：P 场 `NATIVE_MOVEMENT_APPLIED … our_write=0 source=not_ours` 共 129 条（key=1/1 120、key=1/0 9），全部为 `order_enum=Move`，属原生 AI 自身的周期性 Move 指令，非针对本方命令的强写。**未观察到任何一条外部改写使本方命令失效**：本场 `TASK_FEEDBACK reason=CommandOverridden = 0`；`POST_OBSERVE` 显示本方期望命令持续成立（`plan-1-task-1 order=Move` 与请求一致；`plan-1-task-2` 在 5.016–9.020 为 `Charge`，11.026 起因原生副作用转为 `Move`）。命令确被覆盖时按 §6.2 记 `TASK_FEEDBACK not_effective` 并使计划失效（N 场 `plan-1-task-2`） |

---

## 6. V10 真实失效的三条观察

以 N 场为例（P 场同形）：

1. **生产路径反应**：`TASK_ELIGIBILITY_LOST task=plan-2-task-1 reason=ai_control_lost source=tick_eligibility_check at=15.542`，紧随 `PLAN_INVALIDATED plan=plan-2 reason=FormationEligibilityLost at=15.542`，并有 `HANDOFF_CLEARED`。
2. **计划条件失效**：失效编队 `IsAIControlled == false`（`PROBE_ELIGIBILITY_LOST … ai_ctrl=0`）；另一承担编队按 §6.3 关闭普通任务并尝试一次合法退出 Stop——P 场 `EXIT_STOP_DONE`，N 场 `EXIT_STOP_NOT_APPLIED reason=not_selected_in_time submitted=0 waited=1.006` 如实登记未获选。
3. **不能靠到期**：本失效由测试适配器在计划存活 ≥8 s 且两支编队任务均取得正向反馈后触发，非 `IntentWindowSeconds` 或测试计时到期。

**关键改进**：修复前 N 场在 9.538 失去资格后计划仍开放到 10.545（间隔 4.005 s）才因恢复引发的命令变化取消。修复后**同一 tick 内完成关闭，间隔 0.000 s**，且 `plan_open_now=<none> same_plan_open=0` 证明旧计划在恢复之前已关闭，恢复动作副作用不再被当作取消原因。

---

## 7. 未验证项（不宣称通过）

1. **旧 MovementOrder 恢复通道未执行。** 两场 `REVOKE_EXECUTED = 0`，仅 `REVOKE_SKIPPED`（P 9 / N 4，跳过原因属 `WriteLedger.PlanRevocation` 的既有分类）。这不等于行为撤权未发生：BEHAVIOR_DISARMED 已实读权重 8→0。旧命令恢复继续未验证，V12 的行为撤权事实按实际权重记录判定，见独立复验报告。
2. **`§12` 原生签名未做独立探针。** `ArmyCompositionGroupVM.CompositionValues` 等签名的可见性与写入语义仍属代码入口证据（§2.5）。
3. **`Move` 目标读回的独立核对**。`Reposition` 的 0.5 m 容差判定依赖 `FormationSampler` 读回的实际目标位置；CBATH 1 Hz 时间线与之的交叉核对仅覆盖采样时刻，不宣称精确执行瞬间一致。V5 第 5 项的"独立效果"在本次核对中**只确认了时间线文件含 `movement_order` 字段并已归档**，**未做逐条时刻对齐核对**。
4. **原生周期性 `Move` 与反馈窗口的相互影响未单独量化**。P 场 129 条 `our_write=0 source=not_ours` 的 `Move` 虽未使命令失效，但其与本方命令的时间交叠对位移投影的贡献未做分离。
5. **`CONFIG_IDENTITY` / `P1_EVAL_TICK` 未实现**（§2.6）。
6. 后续计划变更需按各自实际原因逐条复核。P 场 `PLAN_CREATED=9 / PLAN_INVALIDATED=9 / PLAN_CHANGED=1`，其中多数由条件真实变化与 `NotEffective` 触发，但**未逐条独立核对**。

---

## 8. V14 Gate 1 对照

| 项 | 配置 P | 配置 N |
|---|---|---|
| 条件 | `ranged_edge=+0.397`（`ranged_advantage=1`） | `ranged_edge=-0.335`（`ranged_disadvantage=1`） |
| 计划目标 | `exploit_ranged_advantage` | `mitigate_ranged_disadvantage` |
| Ordinal 1（Ranged，key=1/1） | `Reposition`，`geometry=move_to_requested=(1128.828,1036.970) span=25.000` | `Hold` |
| Ordinal 2（Infantry，key=1/0） | `Engage`，`geometry=charge_enemy_formation` | `Reposition` |

差异落在**任务类型、命令与目标位置**上，且可逐条追到 `rangedEdge` 差异，非仅 Plan 名称。任务顺序两配置一致（Ordinal 1 = Ranged、2 = Infantry），符合脚本 §4.5 v1.3。

---

## 9. 六项修复的复验结果

依据 `P1-CODEX-FIRST-LIVE-REVIEW.md` §2：

| 项 | 修复前 | 修复后 |
|---|---|---|
| 1 逐 tick 安全检查覆盖已写任务 | 失资格后 4.005 s 仍 `PLAN_CONTINUED` | 同一 tick 关闭，间隔 0.000 s |
| 2 延迟在写令前生效 | 首令 0.014/0.063 s，间隔 0.043/0.441 s | 首令 1.505/1.502 s，间隔 1.544/1.553 s |
| 3 退出动作生命周期与跨计划隔离 | 旧计划退出任务反馈取消新计划 | 跨计划失败反馈 0 条；`EXIT_STOP_DONE` / `EXIT_STOP_NOT_APPLIED` 按时收尾 |
| 4 反馈按 TaskKind 判定 | 无位置 Engage 立即判生效 | Engage 走 `engage_toward_enemy`；Reposition 以实际写令位置为基线 |
| 5 继续条件与控损 | `strength_critical=1` 仍创建 exploit 计划 | 改走 `hold_observation` + `preserve_power_*_hold` |
| 6 任务写入计数 | 用 `BlockedAttempts` 冒充 | 统一为 `WritesOf`（§2.3） |

明细见 `Program/_verify/_p1_reverify_evidence.md`。

---

## 10. 构建与测试

- `dotnet build Program/CombatBrain2.csproj -c Debug`：干净重建 **0 警告 / 0 错误**。
- `dotnet build Program/tests/CombatBrain2.Tests.csproj -c Debug`：干净重建 **0 警告 / 0 错误**。
- `dotnet test Program/tests/CombatBrain2.Tests.csproj -c Debug`：**81 通过 / 0 失败**（口径见 §2.2）。

---

## 11. 提交边界

- CB2 实现与交接文档**分开本地提交**。
- CBATH 改动在 CBATH 仓库**独立提交**。
- 大原始材料外置归档一次至 `E:\MBmod\CombatBrain2-evidence\`，仓库只留索引与摘要；旧证据不重写。
- **不 push。**

---

## 12. 交回独立验收

按脚本 §14：本报告逐项填写 V1～V14 的事实、场次与未验证项后交回独立验收。批准的是实现方案与范围修订，**不预先登记 P1 阶段通过**。若原生入口或命令事实与脚本发生实质矛盾，应提交具体缺口与一个最小替代，不自行放宽门槛。

本批已识别的三处需验收方裁定的边界：

1. V12 的"撤权"证据只到计划生成层（见 §7 第 1 项）。
2. V5 第 5 项"独立效果"未做写令时刻与 CBATH 1 Hz 样本的逐条对齐（见 §7 第 3 项）。
3. 陈旧 `build_id` 与缺失的 `CONFIG_IDENTITY` 是否影响可追溯性判定（§2.4、§2.6）。
