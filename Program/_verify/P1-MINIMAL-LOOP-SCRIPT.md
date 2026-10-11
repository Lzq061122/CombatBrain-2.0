<!-- PROMPT META
Name: CombatBrain 2.0 P1 Minimal Loop Implementation Script
Version: 1.3
Author Role: CombatBrain 2.0 Development Planner
Model: GPT-6
Created: 2026-10-11
Updated: 2026-10-12
Target: CombatBrain 2.0 Development Team
Scope: P1 / Infantry and Ranged / Minimal Plan Execution and Feedback
Reviewer: Codex
-->

<!-- CHANGE LOG (newest first, keep last 3)
v1.3 2026-10-12 01:02 | role=CombatBrain 2.0 Development Dispatcher and Reviewer | model=GPT-6 | editor=codex-cb20-p1-team-statistics-clarification-20261012 | note=明确受控队身份、双方全队统计与编队候选资格分离，保持配比阈值和任务顺序不变
v1.2 2026-10-12 00:20 | role=CombatBrain 2.0 Development Dispatcher and Reviewer | model=GPT-6 | editor=codex-cb20-p1-driver-replacement-decision-20261012 | note=明确批准P1取代固定P0驱动，删除旧意图与专属测试，改为新通路安全要求回归，默认关闭只采样不发令
v1.1 2026-10-11 22:21 | role=CombatBrain 2.0 Development Dispatcher and Reviewer | model=GPT-6 | editor=codex-cb20-p1-script-confirmation-20261011 | note=明确任务级执行门、多编队转交、安全退出例外与未生效反馈处置，修正时间和几何判据并确认进入限定实现
-->

# P1 最小指挥闭环 —— 实现前脚本（v1.3，Codex 已确认）

状态：**Codex 已确认，允许按本脚本进入限定实现**。确认基于文档和现有源码约束，不是 P1 实机通过。§5 参数在本版本冻结；实测期间不得边跑边改通过口径。v1 来自 `789272c`，本轮只修改本文件，不改实现、构建或部署。
2026-10-12 当前裁决：采用 P1 驱动取代固定 P0 测试驱动，具体范围与报告口径见 §9.4.1。本次明确授权是 v1.1 对 P0 驱动保留约定的修订，不把已有未提交重写解释为开工前该约定已不存在。其他任务、参数、效果与安全验收条件保持不变。
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

**受控队身份、统计范围与任务资格分开：**
- 本批两队 CustomBattle 中，我方是实际选定的非玩家 AI 受控队，敌方是与它对立的另一队（本批为玩家队）。按实际队关系/标识建立归属，不把玩家队委派给 AI 理解为玩家身份消失，不硬编码 team/formation 号，不把“不属于某队的所有记录”泛化成任意场景的一方。没有唯一明确的受控队/对立队关系时不给未知归属填值、不生成 Plan；本单不扩展多队仲裁。
- 两方统计按各自实际 Team 的全部非空编队记录：玩家队作为敌方照常计入敌方总人数、远程人数及中心位置；我方未受控的骑兵，或暂失去 AI 资格的我方编队，仍属于我方全队统计。不能为提高“可写性”过滤统计分母，不能把某编队资格丧失当作该编队士兵消失或队伍换边。
- `MyFormations` 可保留我方全队记录以供统计；`PlanBuilder.PickFirst` 另对任务候选应用 §1 的完整资格门。同队合格 Infantry/Ranged 各选一支；玩家拥有、非 AI、空、陈旧或身份无效的记录不参与任务选择，但不因此删掉仍存在的士兵统计。敌方玩家编队从不参与我方候选。
- 候选仅代表采样时合格，不保证一定可写；真实本方行为获选、主线程、对象身份及执行前即时资格复核仍按原要求执行。以上是既有归属/统计/安全门的澄清，不改变验收阈值、不新增实机场次。

---

## 2. 条件与目标

### 2.1 关键条件定义
**条件 = 双方远程（Ranged）占比差 + 兵力基本约束。**

- 判据变量：
  - `ownRangedRatio = 我方实际受控队全队 Ranged 人数 / 我方实际受控队全队总人数`（不是仅两支受命编队或仅当前可写单位）
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
  3. 否则按下述 `rangedEdge` 规则选择。
  4. 若 `rangedEdge ≥ +0.10` → 目标 = "以远程优势压制"，任务偏好 `Engage`（步兵为 `Engage`，Ranged 为 `Reposition` 到射程占位）。
  5. 否则若 `rangedEdge ≤ −0.10` → 目标 = "抵消敌方远程优势"，任务偏好 `Hold` 或 `Reposition`（收拢、缩短暴露）。
  6. 否则（`rangedEdge` 在中间带）→ 目标 = "维持接触"，任务偏好 `Hold`。
- 上述顺序只在**同一次评价**内使用；不引入评分表。

---

## 3. 两个开战配置

### 3.1 固定量（两场必须一致）
- 场景：`battle_terrain_biome_096`（沿用 `MissionControl.DefaultScene`）。
- 文化/兵种来源：沿用当前入口默认。
- 总兵力请求值：双方各 **100**（现构成下 team0 = 24+25+25+25 = 99、team1 = 20+25+13+25+12 = 95）；不要求实际恰好 100，实际值以生成后读数及 §3.5 为准。
- 时间：白天、季节沿用默认。

### 3.2 配置 P（远程优势成立）
| 侧 | 步兵 | 远程 | 骑兵 | 骑射 |
|---|---|---|---|---|
| 我方（实际受控的非玩家 AI 队） | 30% | 55% | 15% | 0% |
| 敌方 | 70% | 20% | 10% | 0% |

请求值：我方 30/55/15/0；敌方 70/20/10/0。
请求配比下占比：我方 55/100、敌方 20/100 → `rangedEdge = +0.35`；实际仍按生成后全队总人数计算，不能把未受控的骑兵从比例分母中移除。骑兵只被观测并沿用原生控制，不扩入本批控制类别。

### 3.3 配置 N（远程优势不成立）
| 侧 | 步兵 | 远程 | 骑兵 | 骑射 |
|---|---|---|---|---|
| 我方（实际受控的非玩家 AI 队） | 70% | 20% | 10% | 0% |
| 敌方 | 30% | 55% | 15% | 0% |

请求配比下 `rangedEdge = −0.35`（≤ −0.10）；实际由生成后读数决定。两场的主判定依据只有 §2 的实际远程占比差和兵力约束，不把骑兵数量差异另算成战术效果。

### 3.4 配比如何施加（设计，不在本单实施）
- 在 `cbath/h3_start_from_screen` 的**调用链内、`ExecuteStart` 之前**设置。现有 VM 路径：`ScreenManager.TopScreen` → 反射取 `CustomBattleVM` 字段 → `ExecuteStart()`。
- 可选配置的施加点（择一，实测时以"公开属性可写"为准）：
  1. `CustomBattleVM.PlayerSide/EnemySide` 的 `ArmyCompositionGroupVM.ArmySize` + `CompositionValues`（`public int[]`），再调 `ArmyCompositionItemVM.RefreshCompositionValue()`；
  2. 只读核对 `PrepareBattleData()` 的生成链及产物。`AddCharacter` 等只用于理解引擎构成来源，不批准绕过 VM 另建军队或启动通路；公开配置路径确实不可用时先报告具体签名矛盾。
- **未核实项**：`ArmyCompositionGroupVM.CompositionValues` 与 `ArmyCompositionItemVM.CompositionValue` 的写入语义（谁是谁的派生）、`RefreshCompositionValue()` 是否会按 `CompositionValues` 重算四项占比、`CustomBattlePlayerType` 字段名与可选值、`ArmySize` 的 setter 可见性。以上目前只有**代码入口证据（反编译）**，**未实机验证**，不得记为已通过。
- 若可写属性路径不成立，退路是新建**一个薄入口**工具（名字暂定 `cbath/h3_start_from_screen_configured`），内部仍走同一 `CustomBattleVM.ExecuteStart()`，不复制业务逻辑。

### 3.5 两配置的最终有效判据（两场都适用）
1. CBATH 独立读数（`get_all_formations` 或 `get_last_run` 时间线）显示**实际生成**了非玩家 AI 队的 Infantry **与** Ranged 编队，人数非零。
2. 计算出的 `rangedEdge` 落在对应门槛之外（P 场 ≥ +0.10；N 场 ≤ −0.10）。
3. 兵力约束满足（互相 ≥ 80%）。
4. 请求值 / 实际值差异必须原样记录在报告里；不满足 1～3 时不得登记 Gate 1 通过，按 §5.5 处理。

### 3.6 配置扩展自身的验证（并入 P1 两场启动证据）
- **无参数入口行为保留**：空配置直接沿用原 `ExecuteStart` 分支，不设置任何构成属性；用分支核对及必要的调用契约测试确认，既有 P0 默认路径实机证据保留。若修改实际影响旧路径，才补该路径实机，不默认增加第三场。
- 两配置的**请求值与实际值**可核对（见 §3.5.4）。
- 每场结束取 `cbath/get_last_run` 时间线；清理结果与 `run-p1.ps1` 退出码保留。
- CBATH 改动**在 CBATH 仓库内独立提交**，不混进 CB2 源码提交。

---

## 4. 任务分配

### 4.1 三种任务的作用（不能只是给 Order 换名字）
| 任务 | 对应 Movement 命令 | 在整体目标中的作用 | 期望结果 |
|---|---|---|---|
| `Hold` | `MovementOrderStop` | 保持当前位置，不主动推进；本批未判定地形优势或掩护质量 | 实际命令为 Stop，独立状态一致；不保证个体完全静止或伤亡下降 |
| `Engage` | `MovementOrderCharge` | 主动接敌、把当前优势转化为接触压力 | 编队朝敌推进，独立状态为 `Charge` 且位置朝敌移动 |
| `Reposition` | `MovementOrderMove(WorldPosition)` | 根据双方位置计算有限接近/后撤点；本批不声称已验证射程或掩护收益 | 实际目标位置 = 有效请求位置（容差内）；位置向目标靠拢或明确报未生效 |

### 4.2 配置 P（远程优势成立）的分配
- **目标**：以远程优势压制敌方（自身 Ranged 占比高、敌方 Ranged 弱）。
- **理由**：`rangedEdge ≥ +0.10` 且兵力约束满足（记录双方实际人数与 `rangedEdge`）。
- **承担编队与任务**（多支服务同一目标）：
  - `Ranged` 编队 → `Reposition`，目标位置为朝敌有限接近点，步长上限 **25 m**（计算定义见 §4.5）。这不等于已经进入射程或获得有利掩护。
  - `Infantry` 编队 → `Engage`（前排压上，为 Ranged 争取输出时间）。
  - **不得**在编队未获选、未确认主线程、执行门未通过时产生写令。
- **继续条件**：`rangedEdge ≥ +0.05`、我方总人数 ≥ 敌方 × 0.70、两支承担编队均保持 §1 全部资格，且任务反馈未失败。超时、对象改变、目标消失、玩家身份变化等统一按 §6.3 处理。
- **失效动作**：进入安全处置（见 §6.3）。

### 4.3 配置 N（远程优势不成立）的分配
- **目标**：抵消敌方远程优势、保存战力。
- **理由**：`rangedEdge ≤ −0.10`（记录双方实际人数与 `rangedEdge`）。
- **承担编队与任务**：
  - `Ranged` 编队 → `Hold`（保持当前位置，不声称存在已识别的掩护）。
  - `Infantry` 编队 → `Reposition`，目标位置为背离敌方中心 **20 m** 的点（与 P 场方向相反，见 §4.5）；独立核对实际目标及位移。
- **继续条件**：`rangedEdge ≤ −0.05`、我方总人数 ≥ 敌方 × 0.70、两支承担编队均保持 §1 全部资格，且任务反馈未失败。
- **失效动作**：同 §6.3。

### 4.4 两场的差异要求（Exit Gate 1 的实际内容）
差异必须落在**编队→任务/命令/目标位置**上，且可逐条追到 `rangedEdge` 的差异。若两场出现同一批编队、同一任务、同一命令（仅 Plan 名称不同），**不得**登记 Gate 1 通过。

### 4.5 选择与位置计算的最小定义
- 从同一快照按 §1 确定受控队，Infantry/Ranged 各选一支通过完整资格门的编队；同类别多支按 `(TeamIndex, FormationIndex)` 稳定升序选第一支，绑定实际对象，缺任一类别不生成本批双编队 Plan。全队人数/中心的读取与候选过滤分开，不能用仅人数和兵种检查代替资格门。
- `u` 为从我方全队单位数加权中心指向敌方全队单位数加权中心的归一化二维向量。P 点 = Ranged 当前位置 + `u × min(25 m, 双方中心距离/2)`；N 点 = Infantry 当前位置 − `u × 20 m`。零距离、未知/非法坐标或公开导航检查不通过时不伪造位置、不强写，记录位置无效并停止生成该任务。
- 记录原始几何点与经引擎公开导航处理的有效请求点；0.5 m 读回容差比较的是有效请求点与实际命令目标点。无公开可达性/目标点读取入口时报告具体缺口，不读新增私有字段补值。
- 普通任务按 Ranged、Infantry 的稳定顺序发令；后者的实际写入不早于前者实际写入 + 1.5 s。前一任务尚未完成等待时，不因后者计划时间已到而同时释放两条命令。
- 两配置统一 `Ordinal 1 = Ranged`、`Ordinal 2 = Infantry`；P 为 Ranged Reposition / Infantry Engage，N 为 Ranged Hold / Infantry Reposition。角色、位置公式和发令顺序相互一致，不因配置改变类别序号。

---

## 5. 参数表（初值）

### 5.1 已冻结值（沿袭 P0，不变）
| 参数 | 值 | 说明 |
|---|---|---|
| `SnapshotMaxAgeSeconds` | 2.0 s | 快照最大年龄，不变 |
| `WriteGate` 顺序与理由 | 不变 | `ExecutionGate.RequestWrite` 的拒绝序不重排 |
| 行为权重 | 8.0 | 沿用 `HoldBehaviorWeight` |

### 5.2 P1 新参数（v1.1 已确认并冻结）
| 参数 | 初值 | 依据 |
|---|---|---|
| `DecisionCadenceSeconds` 决策周期 | 3.0 s | 派单建议；快于计划稳定时间，允许按节奏评价而不改计划 |
| `PlanMinStableSeconds` 计划最小稳定时间 | 8.0 s | 派单建议；≥ 编队间延时总和，保证分头响应的可测窗口 |
| `FirstCommandDelaySeconds` 首次指挥延迟 | 1.5 s | 派单建议 "至少 1 s"；取 1.5 s 留出评价→计划→投递的可观测间隔 |
| `FormationStaggerSeconds` 编队间发令间隔 | 1.5 s | 派单建议 "至少 1 s"；取 1.5 s 便于 1 Hz 独立采样区分先后 |
| `SelectionWaitSeconds` 获选等待上限 | 10.0 s | 从该任务首次提交候选起算，不因重复回调重置；超时使 Plan 失效 |
| `FeedbackWaitSeconds` 实际反馈等待窗口 | 3.0 s | 写令后最多等 3 s 读实际状态；超时即报 `not_effective` |
| `MovePositionToleranceMeters` 目标位置容差 | 0.5 m | 读回的运动目标位置与请求位置的允许差 |
| `MoveDisplacementMeters` 位移下限 | 3.0 m | 反馈窗口内编队实际位移 ≥ 3 m 才算"朝目标移动" |
| `SafetyCancelLatencySeconds` 安全取消时限 | 1.0 s | 实际失效发生到关闭任务、清旧待写命令及撤权；不承诺不合格编队还会执行 Stop |

### 5.3 不得沿用为 Plan 寿命
**P0 的 `IntentWindowSeconds = 20.0` 是测试意图窗口，不得自动变成所有 P1 Plan 的寿命。** P1 的 Plan 存活由**继续条件**决定（真实条件失效才结束，见 §6.3），不设固定 20 s 到期；测试适配的计时器不得作为计划的结束依据。

### 5.4 安全取消不受节奏约束
资格丧失（`AiControlLost` / `PlayerEligibilityLost`）、Mission 结束、编队对象失效：**立即**取消/撤权，不受 `PlanMinStableSeconds` / `FirstCommandDelaySeconds` / `FormationStaggerSeconds` 限制。

安全资格检查在每个已确认主线程的 Mission tick 及写令前执行；不能只在 3 s 评价点检查。3 s 用于战场评价和效果反馈窗口，不用它兑现 1 s 安全时限。正常任务与退出 Stop 分别标记；§8/V9 的 1.5 s 发令间隔只约束正常指挥，不约束紧急取消和退出动作。

### 5.5 补场规则
只有以下三种情况补场：(a) 判据未触发，(b) 判据失败，(c) 相关源码改变。同一段源码/日志不得反复猜；先补能区分原因的证据（唯一标记、额外字段），再决定补场。

---

## 6. 命令事实与在线反馈

### 6.1 每条命令必须留下的事实
1. **候选零写入**：每个新任务/命令的 `write_attempts_task == 0`（候选不等于写令）；不要求一个 Plan 内第二支编队的候选出现时 Plan 全局累计仍为 0。
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
- 位移从本任务实际写入时的位置开始计算，不能计入发令前的原生移动。Charge 沿该时刻朝敌方向的位移投影 ≥ 3 m；Move 以到有效目标点的距离减少 ≥ 3 m、或已进入目标附近 0.5 m 为依据，横向漂移不算朝目标生效。CB2 实际值与 CBATH 1 Hz 位置序列交叉核对，不把 1 Hz 样本时间宣称为精确执行瞬间。

### 6.3 在线反馈与失效处置（CB2 自己读，不依赖 CBATH）
- 安全资格按 §5.4 每 tick 实读；已写命令的效果也在主线程反馈检查点读取，不等待下一次 3 s 战场评价才开始计时。反馈 3 s 从各任务的实际写入时刻分别起算。
- 普通任务获选超过 10 s、写令调用失败、3 s 内效果不成立、或已成立的命令后续被覆盖时，记录对应实际原因并使 Plan 失效；不能只打印 `not_effective` 然后继续保持假成功。继续条件/rangedEdge 等战略量按 3 s 评价；资格变化立即失效。
- **Plan 层反应**：任一承担编队的继续条件不成立 → 关闭该 Plan 的全部普通任务、禁止旧投递并清槽，记录一次 `PLAN_INVALIDATED`；逐编队记录后续处置。
- **安全动作集合（本阶段仅此一种）**：不合格、失效对象或已结束 Mission 只关闭/清槽/撤权，绝不补 Stop。其他仍合格编队可产生一次独立退出 Stop，仍需真实本方行为获选、主线程和写令前即时门检查；若当前原生未选中本方行为，记录 `exit_stop_not_applied`，在 1 s 安全期限内撤权，不强行外部写令、不等 10 s 来延误撤权。Stop 后值和后来被原生改写分别记录，不保证持续独占。
- 退出 Stop 使用新的独立 `task_id`，以 `origin_plan_id/origin_task_id` 追溯原因，不重新打开旧 Plan、不复用其已写过/已关闭的门状态。每编队最多一次退出尝试、随后撤权；该退出记录不算旧普通任务复活，也不允许借它恢复正常作战命令。
- 同一反馈失败原因尚未解除时，不在每次评价中重建相同任务反复试写；相关真实条件变化或资格恢复后，可以经新评价形成新 Plan。处理失败所需的是本批最小停止/反馈状态，不另建重试框架。
- 处置必须观察到 **Plan 层与其他任务**的反应，不能只贴一条旧 P0 的 setter 拒绝日志。

---

## 7. 真实失效（本单的"失效反馈"证据）

### 7.1 触发方式
在**已生效**之后，用公开原生测试接口触发承担编队的资格变化——沿用 P0 已验的 `EligibilityProbe` 通路（`Formation.SetControlledByAI(false)`），标记为**测试接口触发的资格变化**，不标为玩家按键接管、不伪造 `PlayerOwner`。

每场仅一次：同一 Plan 已连续存活至少 8 s，且两支编队的普通任务均取得正向反馈后，测试适配将其中 Ranged 的 AI 资格设为 false，保持至少 4 s 后恢复。计时仅安排测试触发，生产失效必须由实际资格变化判定；若稳定/生效前置未达成就如实记未触发，不提前关闭 Plan 凑数。恢复后继续观察至少 8 s（两次以上评价机会）和 Mission 清理。

### 7.2 必须同时满足的三个观察
1. **生产路径反应**：`INTENT_CANCELLED reason=AiControlLost`（或 `PlayerEligibilityLost`）+ `BEHAVIOR_DISARMED weight_before=8 → weight_after=0` + 待写槽位按该编队键被清（`HANDOFF_CLEARED`）。
2. **计划条件失效**：该承担编队**不再满足计划的成立条件**（`IsAIControlled == false`），生产检查触发 `PLAN_INVALIDATED`；其他承担编队按 §6.3 关闭普通任务、尝试一次合法退出 Stop 或如实登记不获选未执行、并撤权。记录前后实际状态及执行/未执行原因，不把撤权日志冒充 Stop 已生效。
3. **不能靠到期**：本失效**不得**由 `IntentWindowSeconds` / 测试计时到期触发；必须是条件真实失效。

### 7.3 恢复与不复活
- 测试接口改回资格后，**旧 Plan/普通任务不得复活**：不重新 arming、不重新投递、不用旧任务写令。§6.3 的一次性退出动作单独识别；后来经新评价形成的新 Plan 可以选择相同目标，但必须用新身份、履行延迟和真实获选，不能只换 ID 继续消费旧待写命令。
- 按 `plan_id` / `task_id` 核对：不得因后续新 Plan 的合法写入使累计计数增加而误判。
- 恢复动作本身的原生副作用（`SetControlledByAI(true)` 内的 `AI.Tick()` + `SetMovementOrder(AI.ActiveBehavior.CurrentOrder)`）必须逐项记录，不得伪称"已撤销恢复"。

---

## 8. 时序与四类时间

每场记录并归档以下四类时间戳（Mission 时间，F3 精度）：
- **评价时间**（每个决策周期）——`EVAL_TICK`
- **计划变更时间**（新建/替换 Plan）——`PLAN_CREATED` / `PLAN_CHANGED`
- **命令投递时间**（实际进入待执行载体的时刻）——带 task_id 的 `HANDOFF_DEFERRED.at`；直接路径用实际调度日志，不以计划到期时间代替。
- **实际执行时间**（主线程真实 setter 调用时刻）——`WRITE_EXECUTED.at`；另外记录 `TASK_FEEDBACK.at` 为首次观测时刻，CBATH 样本是独立观测时刻，不与 setter 时间混写。

必查断言：
- 条件未变、未失效的连续至少 8 s 窗口内，仅初次 `PLAN_CREATED` 一次、无 `PLAN_CHANGED`；评价仍有记录、无重复正常发令。安全取消不受此稳定要求阻挡。
- 同一正常 Plan 的不同编队初次 `WRITE_EXECUTED.at` 间隔 ≥ `FormationStaggerSeconds`（全体同时响应 = 失败）；紧急退出动作不受该间隔约束。
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
- 分流以 Mission 内稳定编队键及实际对象身份为基础，每编队最多一个 pending，载体带 mission/plan/task 身份；同任务重复投递去重，不覆盖不同未关闭任务，同编队新任务只能在旧任务关闭清槽后进入。不同编队独立存放，消费后仍即时复核当前任务、Plan、资格、对象和本方行为；所有发布/取出/清空继续受同一同步约束，Mission 结束全清。不能只按编队槽位号判为同一任务。
- `Program/src/Core/ExecutionGate.cs` / `WriteLedger.cs`：每个普通任务及退出命令用独立状态键，不能让一个 `plan_id` 共用一次写入额度或覆盖另一支编队的账本。必要时改 `TestParameters.cs` 的取消原因枚举；既有 P0 拒绝顺序、即时资格门和单次去重语义保留。
- `Program/src/Core/FormationSnapshot.cs`：加编队类别辅助（`Infantry`/`Ranged` 判定）与按队/类别遍历入口；其它字段不动。
- `Program/src/Host/TestDriver.cs`：由 P1 通路（评价→计划→任务→投递→反馈）取代固定 P0 测试驱动，不恢复旧固定 A/B 流程。P0 已验安全要求在新通路上保留，历史源码与实机证据按 §9.4.1 留存。
- `Program/src/Host/CbHoldBehavior.cs`：行为按当前任务产出命令（不再写死 `Stop`）；`Hold` 行为等价于现状。
- `Program/src/Host/SamplingBehavior.cs`：开关启用时运行 P1 自动指挥；关闭时只采样/观测，不退回旧 P0 固定驱动。
- `Program/src/Host/FormationSampler.cs`：如需，补 P1 反馈所需字段（实际目标位置等）。
- `Program/src/Host/EligibilityProbe.cs`：支持按 `plan_id`/编队键触发资格变化（沿用既有哨兵文件，默认关闭）。
- `Program/CombatBrain2.csproj`：登记新增 `.cs`（`EnableDefaultCompileItems=false`，漏登记则不参与编译）。
- 构建自动更新 `Program/src/Host/BuildIdentity.g.cs`，不手工伪造身份；必要时在 `Cb2Log.cs` 只补上述字段，不重写日志机制。

### 9.3 测试
- `Program/tests/`：覆盖 Plan、评价、任务反馈、独立记账及多编队转交；可以按实际纯 Core 结构组织测试文件，扩展 `TargetSelectorTests.cs`（Ranged）、`ThreadHandoffTests.cs`（按 key 分流）。仍适用的既有安全用例必须保留并通过；仅针对已删除 P0 固定驱动的测试按 §9.4.1 退役/替换，不再要求原 65 个用例逐项原样保留。
- 相应更新 `Program/tests/CombatBrain2.Tests.csproj` 的纯 Core 链接项；必要的执行门/账本取消与退出动作测试纳入上述范围，不把 Host 编译进纯 Core 测试冒充实机证明。

### 9.4 开关与互斥
- 沿用哨兵文件方式（`Documents\CombatBrain2\` 下），P1 默认关闭；关闭态不生成 Plan/任务，不投递或执行本方命令，只保留采样/观测。这是新的默认语义，不宣称与旧 P0 固定驱动完全一致。
- 当前 `p1-command-loop.txt` 的 enabled 在 Mission 首次加载时确定。不存在或 enabled=0 对应关闭态；不把中途修改/删除文件称为已实现热关闭。资格丧失、任务/Plan 失效及 Mission 结束仍必须按既定安全要求清槽撤权。
- **同一场 Mission 只有 P1 一条本方自动指挥来源**；资格测试适配默认关闭，其实际环境修改必须如实标记，不能冒充本方自动指挥。

### 9.4.1 P0 驱动替代的明确裁决与证据口径
- 批准删除 `TargetSelector.cs` 的旧 `TestIntent` 类、`TestParameters.cs` 的 `IntentWindowSeconds` / `TriggerDelaySeconds` / `BetweenIntentSeconds` / `ExplicitCloseAfterWriteSeconds` 四个 P0 专属常量，以及 `EligibilityProbe.cs` 中已不参与当前流程的 `_planCountAtLoad`。不得顺带删除仍适用的资格门、主线程约束、同步、撤权与 Mission 清理。
- 批准以 `TaskSelectionDeadlineTests.cs` 替换旧 `TestIntentTests.cs`；旧固定 20 s 意图窗口不属于 P1 Plan 的寿命要求。测试报告列明退役、替换和新增项，不用总数相同/更多宣称原基线原样回归。
- 本次直接核对：旧 TestIntent 文件 6 个 Fact，替换文件 7 个 Fact；ThreadHandoff 文件由 8 个 Fact 增至 10 个，净增 3。开发报告的当前 68 个通过可以作为该轮测试结果记录，但不能写成“原 65 个用例全部保留并通过，再新增 3 个”。本裁决未重新运行测试，不独立背书其构建/执行读数。
- 旧 P0 可追溯基线为 `df4cf3d`（源码与 P0 阶段退出时相同，后续为文档变更），旧实机身份、场次与归档保留。历史 P0 通过不撤回，也不冒充新 P1 的实机结果；本批不要求增加旧驱动兼容模式或另跑完整 P0 场。
- V12 定义为**受影响的新 P1 通路上的安全要求回归**：主线程、真实获选、资格门、撤权、关闭不复活与 Mission 清理。报告显著记录此次对原 §9.2/§9.4 的批准修订和实际默认行为变化，不能称为旧 P0 固定场景逐字回归。
- 当前 `run-p1.ps1` 尚不存在，尚无两场 P1 主对照记录。所有依赖新实机证据的 V1～V14 条目继续待实机；下一步按本脚本实现 runner 和必要配置入口，再进行既定两场，不追加验收项目、不 push。

### 9.5 CBATH（在 CBATH 仓库内独立提交）
- 实现落点限定为既有 `experiments/test-mod/CBATH/CbathTools.cs` / `MissionControl.cs`、自动生成的 `BuildIdentity.g.cs` 及必要项目登记项；实际完整路径按 CBATH 现有源码根核对。采样、RunRecord 与其他工具语义不因配置扩展改动。
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
| V8 | 计划稳定 | 条件未变连续 ≥ 8 s；初次新建一次、无计划变更或重复正常发令，评价继续 | 四类时间戳 | 待实机 |
| V9 | 分头响应 | 同一正常 Plan 两编队初次实际写入间隔 ≥ 1.5 s；首次命令延迟 ≥ 1.5 s；安全退出例外单列 | 四类时间戳 | 待实机 |
| V10 | 真实失效 | §7.2 的 1～3 全部成立，且**不是**到期触发 | 我方日志 + CBATH 时间线 | 待实机 |
| V11 | 不复活 | 恢复后观察 ≥ 8 s，旧普通任务无新写令/重投递/重 arming；一次退出动作与新评价的新 Plan 独立识别 | 日志按 plan/task 身份和命令用途扫描 | 待实机 |
| V12 | 继承安全要求在 P1 新通路回归 | 主线程、真实获选、资格门、撤权、关闭不复活、Mission 清理在新通路上成立；旧固定 P0 驱动已退役，见 §9.4.1 | 同两场日志 | 待实机 |
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

**处理方式**：实现阶段先用最小只读探针核对签名（不改引擎），再决定用现有入口加参数还是新增薄入口；签名和实际入口结果写入实现交接报告，不事后改本脚本的冻结判据。现有 CBATH 取得 VM 的反射路径可沿用；“不新增私有访问”指不新增引擎私有字段读取，不要求重写已有入口。

---

## 13. 成本与提交边界
- 主对照：**两场**（配置 P / 配置 N）。同场承载命令效果、计划稳定、分头响应、在线反馈与新增域（`Charge` / `Move(position)` / Ranged）安全证据。
- 补场仅限 §5.5 三种情况。
- 提交拆分：CBATH 小改在 CBATH 仓库独立提交；CB2 实现与交接文档分开本地提交；大原始材料外置归档一次；旧证据不重写。**不 push**。
- 本轮 Codex 确认提交仅修改本脚本，未修改实现、构建、部署或启动游戏。确认后允许开发组按 §9 实现，执行必要测试/回归、限定部署、CBATH 两场主对照、归档与本地提交，不 push；无需另建派单或再询问 Q1～Q5。

---

## 14. 停止点
原文档阶段停止点已完成，v1.1 已确认进入实现；v1.2 明确批准固定 P0 驱动替代及当前删除范围，继续实现 runner 与既定实机交付，无需重复询问这两项。实现交接时逐项填写 V1～V14 的事实、场次与未验证项，然后交回独立验收。这里批准的是实现方案及本次范围修订，不预先登记 P1 阶段通过；若原生入口或命令事实与脚本发生实质矛盾，提交具体缺口与一个最小替代，不自行放宽门槛。
