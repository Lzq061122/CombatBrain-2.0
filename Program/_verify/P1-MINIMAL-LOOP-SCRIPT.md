# P1 最小指挥闭环 —— 实现前脚本（v1，待 Codex 确认）

状态：**待确认**。本文件是实现前判据。确认后才动代码；实测期间不得边跑边改通过口径。
日期：2026-10-11。基线 HEAD `fe73825`（P0 已由 Codex 接受退出）。派单：`E:\MBmod\TacticalCommandLab\_dispatch\2026-10-11-cb20-p1-minimal-loop-script.md`（v1.0，提交 `7d22cce`，未 push）。
与 `P1-MINIMAL-LOOP-OUTLINE.md`（v2）冲突时以本文件为准。

---

## 0. 已采用范围（来自派单 §2，不再询问）

| 项 | 采用 |
|---|---|
| Q1 任务类型 | `Hold` + `Engage` + `Reposition`，暂不加 `Withdraw` |
| Q2 控制类别 | 只扩到 Infantry＋Ranged；骑兵/骑射暂缓；不建全兵种×全命令矩阵 |
| Q3 两种条件 | 允许 CBATH 最小开战配置扩展（`h3_start_from_screen` 可选参数），不建通用配置框架 |
| Q4 读取来源 | 自采快照为主，`QuerySystem` 补必需字段，不新增私有访问 |
| 反馈分工 | CB2 自己读真实状态做在线反馈；CBATH 1 Hz 时间线做独立验收 |
| 多编队转交 | P0 单槽位**必须最小适配**后才能承担多编队，不宣称原样复用 |

---

## 1. 控制域与身份口径

**控制域（P1 扩张后）**：非玩家队、`IsPlayerTeam == false`、`PlayerOwner == null`、`IsAIControlled == true`、非空（`CountOfUnits > 0`）、`FormationClass` 属 `Infantry` 或 `Ranged`、快照年龄 ≤ 2.0 s。

**身份口径（不硬编码）**
- 生产代码**不得**出现 team1 / formation 0 / formation 1 这类常量；目标队与槽位一律由**实际队关系**（`team.IsPlayerTeam == false`）与快照决定。
- 每场必须记录并归档：`mission_key`、双方 `team_index`/`side`/`is_player_team`、被指挥编队的 `formation_index`/`formation_class`/对象身份、以及"实际控制域"的判定依据。
- "team1 是我方"是**本批场次的事实记录**，不是控制关系；报告中按此措辞。

---

## 2. 条件与目标

### 2.1 关键条件定义
**条件 = 双方远程（Ranged）占比差 + 兵力基本约束。**

- 判据变量：
  - `ownRangedRatio = 我方被指挥队 Ranged 人数 / 我方被指挥队总人数`
  - `enemyRangedRatio = 敌方队 Ranged 人数 / 敌方队总人数`
  - `rangedEdge = ownRangedRatio − enemyRangedRatio`
  - 兵力约束：我方总人数 ≥ 敌方的 80%、敌方总人数 ≥ 我方的 80%（避免一方碾压导致"胜负已分"污染对照）
- 优势成立阈值：**`rangedEdge ≥ +0.10`**
- 优势不成立阈值：**`rangedEdge ≤ −0.10`**
- **判定边界**：只在阈值成立时登记 Exit Gate 1 通过。若某场实际构成使 `rangedEdge` 落在 `(−0.10, +0.10)` 内、或兵力约束不满足，该场**不得**登记 Gate 1 通过，按 §5.5 补场。
- 全部比例由**双方实际生成后读到的**人数计算（我方读自策略快照，敌方读自快照；两场都由 CBATH 独立工具交叉核对）。**不得**用请求值或优势标志代替。

### 2.2 胜利与控损如何进入最小选择
- **胜利**：以"我方相对兵力未处于劣势 + 具备推进条件"作为可继续作战的前提；不引入完整胜利分。
- **控损**：我方总人数低于敌方 **70%** 时视为劣势，此时 Plan 应倾向 `Hold`（保持队形、减少接触），而不是继续 `Engage`。
- **不写加权和**：Charter §18 的 50/50 是概念权重，本阶段**不得**写成 `0.5 × 胜利分 + 0.5 × 伤亡分`。
- 选择顺序（决策规则，最小且可解释）：
  1. 若无合格敌方目标 → 目标为空，Plan 不产生任务（记 `no_target`）。
  2. 若我方总人数 < 敌方 × 0.70 → 目标 = "保存战力"，任务偏好 `Hold`。
  3. 否则到第 3 条。
  4. 若 `rangedEdge ≥ +0.10` → 目标 = "以远程优势压制"，任务偏好 `Engage`（步兵为 `Engage`，Ranged 为 `Reposition` 到射程占位）。
  5. 否则若 `rangedEdge ≤ −0.10` → 目标 = "抵消敌方远程优势"，任务偏好 `Hold` 或 `Reposition`（收拢、缩短暴露）。
  6. 否则（`rangedEdge` 在中间带）→ 目标 = "维持接触"，任务偏好 `Hold`。
- 上述顺序只在**同一次评价**内使用；不引入评分表。

---

## 3. 两个开战配置

### 3.1 固定量（两场必须一致）
- 场景：`battle_terrain_biome_096`（沿用 `MissionControl.DefaultScene`）。
- 文化/兵种来源：沿用当前入口默认。
- 总兵力基准：双方各 **恰好 100**（现构成下 team0 = 24+25+25+25 = 99、team1 = 20+25+13+25+12 = 95；请求值取 100，实际值以生成后读数为准，**允许差异**）。
- 时间：白天、季节沿用默认。

### 3.2 配置 P（远程优势成立）
| 侧 | 步兵 | 远程 | 骑兵 | 骑射 |
|---|---|---|---|---|
| 我方（被委派队） | 30% | 55% | 15% | 0% |
| 敌方 | 70% | 20% | 10% | 0% |

请求值：我方 30/55/15/0；敌方 70/20/10/0。
预期实际：我方约 30 步 + 55 远；敌方约 70 步 + 20 远 → `ownRangedRatio ≈ 0.55`、`enemyRangedRatio ≈ 0.22` → `rangedEdge ≈ +0.33`（≥ +0.10 成立）。

### 3.3 配置 N（远程优势不成立）
| 侧 | 步兵 | 远程 | 骑兵 | 骑射 |
|---|---|---|---|---|
| 我方（被委派队） | 70% | 20% | 10% | 0% |
| 敌方 | 30% | 55% | 15% | 0% |

预期 `rangedEdge ≈ −0.33`（≤ −0.10 不成立）。

### 3.4 配比如何施加（设计，不在本单实施）
- 在 `cbath/h3_start_from_screen` 的**调用链内、`ExecuteStart` 之前**设置。现有 VM 路径：`ScreenManager.TopScreen` → 反射取 `CustomBattleVM` 字段 → `ExecuteStart()`。
- 可选配置的施加点（择一，实测时以"公开属性可写"为准）：
  1. `CustomBattleVM.PlayerSide/EnemySide` 的 `ArmyCompositionGroupVM.ArmySize` + `CompositionValues`（`public int[]`），再调 `ArmyCompositionItemVM.RefreshCompositionValue()`；
  2. 或直接只读核对 `PrepareBattleData()` 的产物 `CustomBattleData.PlayerParty/EnemyParty` 中 `CustomBattleCombatant` 的 `AddCharacter(characterObject, number)`。
- **未核实项**：`ArmyCompositionGroupVM.CompositionValues` 与 `ArmyCompositionItemVM.CompositionValue` 的写入语义（谁是谁的派生）、`RefreshCompositionValue()` 是否会按 `CompositionValues` 重算四项占比、`CustomBattlePlayerType` 字段名与可选值、`ArmySize` 的 setter 可见性。以上目前只有**代码入口证据（反编译）**，**未实机验证**，不得记为已通过。
- 若可写属性路径不成立，退路是新建**一个薄入口**工具（名字暂定 `cbath/h3_start_from_screen_configured`），内部仍走同一 `CustomBattleVM.ExecuteStart()`，不复制业务逻辑。

### 3.5 两配置的最终有效判据（两场都适用）
1. CBATH 独立读数（`get_all_formations` 或 `get_last_run` 时间线）显示**实际生成**了非玩家 AI 队的 Infantry **与** Ranged 编队，人数非零。
2. 计算出的 `rangedEdge` 落在对应门槛之外（P 场 ≥ +0.10；N 场 ≤ −0.10）。
3. 兵力约束满足（互相 ≥ 80%）。
4. 请求值 / 实际值差异必须原样记录在报告里；不满足 1～3 时不得登记 Gate 1 通过，按 §5.5 处理。

### 3.6 配置扩展自身的验证（并入 P1 两场启动证据）
- **无参数入口行为保留**：正式实现时先在**不改参数**的情况下跑一次旧路径（可与 P0 回归合并），确认 `h3_start_from_screen` 旧调用仍得到默认构成（24/25/25/25 与 20/25/13/25/12 附近的既有形态）。
- 两配置的**请求值与实际值**可核对（见 §3.5.4）。
- 每场结束取 `cbath/get_last_run` 时间线；清理结果与 `run-p1.ps1` 退出码保留。
- CBATH 改动**在 CBATH 仓库内独立提交**，不混进 CB2 源码提交。

---

## 4. 任务分配

### 4.1 三种任务的作用（不能只是给 Order 换名字）
| 任务 | 对应 Movement 命令 | 在整体目标中的作用 | 期望结果 |
|---|---|---|---|
| `Hold` | `MovementOrderStop` | 占住当前有利位置、减少无谓接触/伤亡；不主动推进 | 编队停在原地，独立状态与 `Stop` 一致 |
| `Engage` | `MovementOrderCharge` | 主动接敌、把当前优势转化为接触压力 | 编队朝敌推进，独立状态为 `Charge` 且位置朝敌移动 |
| `Reposition` | `MovementOrderMove(WorldPosition)` | 把我方编队移动到**算出来的**位置以改变条件（Ranged 进入射程占位；劣势时收拢） | 实际目标位置 = 请求位置（容差内）；位置向目标靠拢或明确报未生效 |

### 4.2 配置 P（远程优势成立）的分配
- **目标**：以远程优势压制敌方（自身 Ranged 占比高、敌方 Ranged 弱）。
- **理由**：`rangedEdge ≥ +0.10` 且兵力约束满足（记录双方实际人数与 `rangedEdge`）。
- **承担编队与任务**（多支服务同一目标）：
  - `Ranged` 编队 → `Reposition`，目标位置 = 我方阵线**朝敌方向**、距当前 Ranged 位置约 **+25 m**（保持射程与安全距离的占位点，由决策层按坐标算出并记录）。
  - `Infantry` 编队 → `Engage`（前排压上，为 Ranged 争取输出时间）。
  - **不得**在编队未获选、未确认主线程、执行门未通过时产生写令。
- **继续条件**：`rangedEdge ≥ +0.05` **且**我方总人数 ≥ 敌方 × 0.70 **且**目标编队的 `IsAiControlled == true`。
- **失效动作**：进入安全处置（见 §6.3）。

### 4.3 配置 N（远程优势不成立）的分配
- **目标**：抵消敌方远程优势、保存战力。
- **理由**：`rangedEdge ≤ −0.10`（记录双方实际人数与 `rangedEdge`）。
- **承担编队与任务**：
  - `Ranged` 编队 → `Hold`（留在当前掩护位置，不主动暴露）。
  - `Infantry` 编队 → `Reposition`，目标位置 = **朝我方后方/掩护方向**（与 P 场方向**相反**）的收拢点，距离约 **+20 m**；或在已处于收拢状态时取 `Hold`。
- **继续条件**：`rangedEdge ≤ −0.05` **且**我方总人数 ≥ 敌方 × 0.70 **且**目标编队 `IsAIControlled == true`。
- **失效动作**：同 §6.3。

### 4.4 两场的差异要求（Exit Gate 1 的实际内容）
差异必须落在**编队→任务/命令/目标位置**上，且可逐条追到 `rangedEdge` 的差异。若两场出现同一批编队、同一任务、同一命令（仅 Plan 名称不同），**不得**登记 Gate 1 通过。

---

## 5. 参数表（初值）

### 5.1 已冻结值（沿袭 P0，不变）
| 参数 | 值 | 说明 |
|---|---|---|
| `SnapshotMaxAgeSeconds` | 2.0 s | 快照最大年龄，不变 |
| `WriteGate` 顺序与理由 | 不变 | `ExecutionGate.RequestWrite` 的拒绝序不重排 |
| 行为权重 | 8.0 | 沿用 `HoldBehaviorWeight` |

### 5.2 P1 新参数（建议初值，实现前给一次有依据的修订；确认后冻结）
| 参数 | 初值 | 依据 |
|---|---|---|
| `DecisionCadenceSeconds` 决策周期 | 3.0 s | 派单建议；快于计划稳定时间，允许按节奏评价而不改计划 |
| `PlanMinStableSeconds` 计划最小稳定时间 | 8.0 s | 派单建议；≥ 编队间延时总和，保证分头响应的可测窗口 |
| `FirstCommandDelaySeconds` 首次指挥延迟 | 1.5 s | 派单建议 "至少 1 s"；取 1.5 s 留出评价→计划→投递的可观测间隔 |
| `FormationStaggerSeconds` 编队间发令间隔 | 1.5 s | 派单建议 "至少 1 s"；取 1.5 s 便于 1 Hz 独立采样区分先后 |
| `SelectionWaitSeconds` 获选等待上限 | 10.0 s | 沿用 P0 |
| `FeedbackWaitSeconds` 实际反馈等待窗口 | 3.0 s | 写令后最多等 3 s 读实际状态；超时即报 `not_effective` |
| `MovePositionToleranceMeters` 目标位置容差 | 0.5 m | 读回的运动目标位置与请求位置的允许差 |
| `MoveDisplacementMeters` 位移下限 | 3.0 m | 反馈窗口内编队实际位移 ≥ 3 m 才算"朝目标移动" |
| `SafetyCancelLatencySeconds` 安全取消时限 | 1.0 s | 资格丧失/Mission 结束从发生到撤权的最长允许时间 |

### 5.3 不得沿用为 Plan 寿命
**P0 的 `IntentWindowSeconds = 20.0` 是测试意图窗口，不得自动变成所有 P1 Plan 的寿命。** P1 的 Plan 存活由**继续条件**决定（真实条件失效才结束，见 §6.3），不设固定 20 s 到期；测试适配的计时器不得作为计划的结束依据。

### 5.4 安全取消不受节奏约束
资格丧失（`AiControlLost` / `PlayerEligibilityLost`）、Mission 结束、编队对象失效：**立即**取消/撤权，不受 `PlanMinStableSeconds` / `FirstCommandDelaySeconds` / `FormationStaggerSeconds` 限制。

### 5.5 补场规则
只有以下三种情况补场：(a) 判据未触发，(b) 判据失败，(c) 相关源码改变。同一段源码/日志不得反复猜；先补能区分原因的证据（唯一标记、额外字段），再决定补场。

---

## 6. 命令事实与在线反馈

### 6.1 每条命令必须留下的事实
1. **候选零写入**：提交候选阶段 `write_attempts_intent == 0`（候选不等于写令）。
2. **同实例真实获选**：`REAL_SELECTION` 的编队对象身份与后续写令的编队对象身份**同一实例**（对象身份串相等），且不是由 `MarkSelected` 之类人工调用产生。
3. **主线程已确认**：`MAIN_THREAD_MARKED` 存在，写令处 `is_main_thread=1`；若命令走转交，须有 `HANDOFF_EXECUTED` 且 `exec_tid == main_tid`。
4. **原方法入口 + 正常返回 + 实际后值**：`WRITE_EXECUTED` 带 `original_body_entered=1`、`invoked=1`、`error=` 空、`order_before`/`order_after` 为实际值。
5. **独立效果**：CBATH 1 Hz 时间线在写令后的窗口内出现对应 `movement_order`。
6. **外部改写与反馈处置**：若原生在写令后再次改写该编队命令（`NATIVE_MOVEMENT_APPLIED ... our_write=0` 或反馈读到的命令与请求不符），必须**如实报告**并按 §6.2 处置，不改成外部强写。

### 6.2 各命令的"效果"判据
| 命令 | 判定依据 | 不算通过 |
|---|---|---|
| `Stop` | 写后实际命令类型 = `StandYourGround`（或等价的 Stop 形态），且独立状态一致 | 只看 setter 被调用 |
| `Charge` | 写后实际命令类型 = `Charge`，且反馈窗口内编队位置**朝敌方向**移动 ≥ `MoveDisplacementMeters` | 只看枚举名/调用次数 |
| `Move(position)` | 反馈读到的**实际运动目标位置**与请求位置差 ≤ 0.5 m，并且在窗口内位移 ≥ 3 m 或已到达目标附近 | 只写日志不检验位置 |

- 超时或读数不符 → 记 `TASK_FEEDBACK not_effective` 并给出实际值；**不得**升级为外部强写。
- 新增命令域（`Charge` / `Move(position)`）与新增类别（Ranged）在**首次实际使用时**验证，不建完整 PoC 矩阵。

### 6.3 在线反馈与失效处置（CB2 自己读，不依赖 CBATH）
- CB2 每个决策周期自读：目标编队的实际 `IsAIControlled`、`PlayerOwner`、`IsPlayerTeam`、`CountOfUnits`、实际活动命令、对 `Move` 的实际目标位置、当前位置。
- **Plan 层反应**：任一承担编队的继续条件不再成立 → 该任务按失效动作处置（`Stop` 或转入已定义的安全动作），并记 `PLAN_INVALIDATED` + 各任务处置。
- **安全动作集合（本阶段仅此一种）**：把**仍满足资格**的承担编队置为 `MovementOrderStop` 并撤权；不新写其他命令。
- 处置必须观察到 **Plan 层与其他任务**的反应，不能只贴一条旧 P0 的 setter 拒绝日志。

---

## 7. 真实失效（本单的"失效反馈"证据）

### 7.1 触发方式
在**已生效**之后，用公开原生测试接口触发承担编队的资格变化——沿用 P0 已验的 `EligibilityProbe` 通路（`Formation.SetControlledByAI(false)`），标记为**测试接口触发的资格变化**，不标为玩家按键接管、不伪造 `PlayerOwner`。

### 7.2 必须同时满足的两个观察
1. **生产路径反应**：`INTENT_CANCELLED reason=AiControlLost`（或 `PlayerEligibilityLost`）+ `BEHAVIOR_DISARMED weight_before=8 → weight_after=0` + 待写槽位按该编队键被清（`HANDOFF_CLEARED`）。
2. **计划条件失效**：该承担编队**不再满足计划的成立条件**（`IsAIControlled == false`），CB2 自己的在线反馈读到这一事实并触发 `PLAN_INVALIDATED`；**其他仍合格的承担编队**按 §6.3 的安全动作处置（`Stop` + 撤权），并记录其前后实际命令。
3. **不能靠到期**：本失效**不得**由 `IntentWindowSeconds` / 测试计时到期触发；必须是条件真实失效。

### 7.3 恢复与不复活
- 测试接口改回资格后，**旧 Plan/任务不得复活**：不重新 arming、不重新投递、不产生新的服务该旧目标的写令。
- 按 `plan_id` / `task_id` 核对：不得因后续新 Plan 的合法写入使累计计数增加而误判。
- 恢复动作本身的原生副作用（`SetControlledByAI(true)` 内的 `AI.Tick()` + `SetMovementOrder(AI.ActiveBehavior.CurrentOrder)`）必须逐项记录，不得伪称"已撤销恢复"。

---

## 8. 时序与四类时间

每场记录并归档以下四类时间戳（Mission 时间，F3 精度）：
- **评价时间**（每个决策周期）——`EVAL_TICK`
- **计划变更时间**（新建/替换 Plan）——`PLAN_CREATED` / `PLAN_CHANGED`
- **命令投递时间**（每编队写入时间）——`WRITE_EXECUTED.at`
- **实际执行时间**（CB2 反馈读到的实际状态变化时刻；CBATH 时间线交叉）——`TASK_FEEDBACK.at`

必查断言：
- 稳定窗口内 `PLAN_CREATED/CHANGED` 出现次数 == 1（逐 Tick 改主意 = 失败）。
- 不同编队的 `WRITE_EXECUTED.at` 两两间隔 ≥ `FormationStaggerSeconds`（全体同时响应 = 失败）。
- 首次命令的 `WRITE_EXECUTED.at` − 首次 `PLAN_CREATED.at` ≥ `FirstCommandDelaySeconds`。

---

## 9. 实现范围（允许改动的文件）

### 9.1 CB2 新增（Core，纯逻辑，可单测）
- `Program/src/Core/BattleState.cs`
- `Program/src/Core/Assessment.cs`
- `Program/src/Core/PlanModel.cs`（目标/理由/继续条件/失效条件）
- `Program/src/Core/TaskModel.cs`（三种任务 + 期望结果）
- `Program/src/Core/PlanParameters.cs`（§5.2 参数）

### 9.2 CB2 修改（最小适配）
- `Program/src/Core/TargetSelector.cs`：控制域加 `Ranged`；新增"多编队分组选取"入口（不改变 P0 单目标选取的既有判据与排序语义）。
- `Program/src/Core/ThreadHandoff.cs`：单槽位 → **按编队键分流**；`TryDefer` 冲突语义、`TakePending(key)`、`Clear(key|all)` 逐项写清；主线程登记与锁语义不变。
- `Program/src/Core/FormationSnapshot.cs`：加编队类别辅助（`Infantry`/`Ranged` 判定）与按队/类别遍历入口；其它字段不动。
- `Program/src/Host/TestDriver.cs`：新增 P1 通路（评价→计划→任务→投递→反馈）；P0 通路保留。
- `Program/src/Host/CbHoldBehavior.cs`：行为按当前任务产出命令（不再写死 `Stop`）；`Hold` 行为等价于现状。
- `Program/src/Host/SamplingBehavior.cs`：按开关分流 P0 固定驱动 / P1 自动指挥（**互斥**）。
- `Program/src/Host/FormationSampler.cs`：如需，补 P1 反馈所需字段（实际目标位置等）。
- `Program/src/Host/EligibilityProbe.cs`：支持按 `plan_id`/编队键触发资格变化（沿用既有哨兵文件，默认关闭）。
- `Program/CombatBrain2.csproj`：登记新增 `.cs`（`EnableDefaultCompileItems=false`，漏登记则不参与编译）。

### 9.3 测试
- `Program/tests/`：新增 `PlanTests.cs` / `AssessmentTests.cs` / `MultiFormationHandoffTests.cs`；扩展 `TargetSelectorTests.cs`（Ranged）、`ThreadHandoffTests.cs`（按 key 分流）。既有 65 个用例必须继续全绿。

### 9.4 开关与互斥
- 沿用哨兵文件方式（`Documents\CombatBrain2\` 下），默认关闭；关闭时行为与当前完全一致。
- **同一场 Mission 只能有一个写令来源**：P0 固定驱动与 P1 自动指挥互斥，由开关决定，不得同时运行。

### 9.5 CBATH（在 CBATH 仓库内独立提交）
- `h3_start_from_screen` 增加**可选**配置参数（见 §3.4）；无参数时行为不变。
- 必要时新增一个薄入口（见 §3.4 退路）。
- 不改 CBATH 冻结核心的既有工具语义。

### 9.6 脚本
- `Program/_verify/run-p1.ps1`（由 `run-p0.ps1` 派生）：两场主对照 + 配置参数 + P1 开关；保留 `run-p0.ps1` 不变以供 P0 回归。

### 9.7 不加的东西
不加无关兼容层、不加插件架构、不加通用调度框架、不加全兵种×全命令矩阵、不引入私有字段反射访问。

---

## 10. 日志与可追溯性

一条命令必须能串起以下链条（同一 `task_id` 贯穿）：

```
snapshot_id → plan_id → task_id → formation对象身份 → 原因
  → CANDIDATE_SUBMITTED → REAL_SELECTION → HANDOFF_* → WRITE_EXECUTED
  → TASK_FEEDBACK(实际状态) → (失效时) PLAN_INVALIDATED → 任务处置
```

必须保留的字段：
- 各阶段 **Mission 时间**（§8 四类时间）；
- 命令的**目标位置**（`Reposition` 请求位置与实际读回位置）；
- 控制资格（`IsAIControlled` / `PlayerOwner` / `IsPlayerTeam`）在**写令前**与**反馈时**的值；
- 加载身份（沿用 `RUNTIME_IDENTITY`：`build_id` / `mvid` / `assembly_file_sha256` / `pid`）；
- 编队身份：`mission_key`、`team_index`、`side`、`formation_index`、`formation_class`、对象身份串。

新增标记（拟定，实现时保持前缀一致）：`P1_EVAL_TICK`、`BATTLE_STATE`、`ASSESSMENT`、`PLAN_CREATED`、`PLAN_CHANGED`、`PLAN_INVALIDATED`、`TASK_ASSIGNED`、`TASK_WRITE`、`TASK_FEEDBACK`、`TASK_NOT_EFFECTIVE`、`CONFIG_IDENTITY`。

---

## 11. 验收表

| # | 判据 | 通过条件 | 观测来源 | 状态 |
|---|---|---|---|---|
| V1 | 加载正确 | 两场 `RUNTIME_IDENTITY` 的 `assembly_file_sha256` 与部署文件一致；`build_id` 记录在案 | 场次目录 `build-identity.json` + 日志 | 待实机 |
| V2 | 配置 P 有效 | 实际生成非玩家 AI 队的 Infantry 与 Ranged 且人数非零；`rangedEdge ≥ +0.10`；兵力约束满足 | CBATH `get_all_formations` / 时间线 + 我方快照 | 待实机 |
| V3 | 配置 N 有效 | 同上但 `rangedEdge ≤ −0.10` | 同上 | 待实机 |
| V4 | 多编队服务同一目标 | 同一次评价输出里 ≥ 2 支编队、各有任务，且都可追到同一 `snapshot_id` + `plan_id` | 我方日志 | 待实机 |
| V5 | 命令可追溯 | 每条命令满足 §6.1 的 1～6 六项事实 | 我方日志 + CBATH 时间线 | 待实机 |
| V6 | 命令实际生效 | `Stop`/`Charge`/`Move(position)` 分别满足 §6.2 判据（**不是**"全场命令未生效"） | CB2 在线反馈 + CBATH 时间线 | 待实机 |
| V7 | 未生效如实报告 | 任一命令未达判据时，存在 `TASK_FEEDBACK not_effective` 且带实际值；无外部强写 | 我方日志 | 待实机 |
| V8 | 计划稳定 | 稳定窗口内 `PLAN_CREATED/CHANGED` == 1；无逐 Tick 改主意 | 四类时间戳 | 待实机 |
| V9 | 分头响应 | 不同编队 `WRITE_EXECUTED.at` 两两间隔 ≥ 1.5 s；首次命令延迟 ≥ 1.5 s | 四类时间戳 | 待实机 |
| V10 | 真实失效 | §7.2 的 1～3 全部成立，且**不是**到期触发 | 我方日志 + CBATH 时间线 | 待实机 |
| V11 | 不复活 | 恢复资格后旧 `plan_id` 无新写令、无重投递、无重 arming | 我方日志按 `plan_id` 扫描 | 待实机 |
| V12 | P0 回归 | 主线程、资格门、撤权、关闭不复活、Mission 清理在新通路上成立 | 同两场日志 | 待实机 |
| V13 | 收尾 | Mission 结束清理完成；`run-p1.ps1` 退出码与无新崩溃；时间线归档 | 场次目录 | 待实机 |
| V14 | Gate 1 对照 | 两场差异落在任务/命令/目标位置，且可追到 `rangedEdge` 差异（非仅 Plan 名称） | V4 日志 + 两场对比 | 待实机 |

**V6 的禁止项**：不得把"全场命令都未生效、只完整打印日志"登记为可用闭环。

---

## 12. 尚未核实的原生配置入口签名（代码入口证据，非实机通过）

以下来自反编译/阅读，**未经实机验证**，报告中必须标为"代码入口证据"：
- `CustomBattleVM`：`PlayerSide` / `EnemySide` / `CompositionGroup` 字段的可见性与类型；`ExecuteStart()` 为 `public`（已在既有 CBATH 路径中实际被反射调用过，但**带配置参数**的路径未验）。
- `ArmyCompositionGroupVM`：`CompositionValues`（`public int[]`）、`ArmySize` 的 setter 可见性、`MaxArmySize` / `MinArmySize` 语义、`SetCurrentSelectedCulture` 调用时机。
- `ArmyCompositionItemVM`：`CompositionValue` 与 `RefreshCompositionValue()` 的语义（是否按 `CompositionValues` 重算、是否触发 UI 回调）。
- `CustomBattleVM.GetBattleCompositionDataFromCompositionGroup` 把 `CompositionValue / 100f` 作为百分比传给 `CustomBattleHelper.GetTroopCounts`（`armySize--` 后按百分比取整、步兵为余数）。
- `CustomBattlePlayerType` 的字段名与可选值。
- `CustomBattleCombatant.AddCharacter` / `SetGeneral` 的可见性与副作用。
- `Mission.JoinEnemyTeam()`（P0 mode c 用过，**本单不用**）。

**处理方式**：实现阶段先用最小只读探针核对签名（不改引擎），再决定用现有入口加参数还是新增薄入口；核对结果写回本文件。

---

## 13. 成本与提交边界
- 主对照：**两场**（配置 P / 配置 N）。同场承载命令效果、计划稳定、分头响应、在线反馈与新增域（`Charge` / `Move(position)` / Ranged）安全证据。
- 补场仅限 §5.5 三种情况。
- 提交拆分：CBATH 小改在 CBATH 仓库独立提交；CB2 实现与交接文档分开本地提交；大原始材料外置归档一次；旧证据不重写。**不 push**。
- 本单为文档阶段：**不修改 CB2/CBATH 实现，不构建、不部署、不启动游戏**。

---

## 14. 停止点
本文件与 `P1-MINIMAL-LOOP-OUTLINE.md`（v2）本地提交后**停止**，交回 Codex 确认。确认前不写实现代码。
