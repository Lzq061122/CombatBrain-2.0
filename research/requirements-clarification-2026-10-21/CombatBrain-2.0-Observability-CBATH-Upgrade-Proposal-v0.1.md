# CombatBrain 2.0 — 战术可观测性与 CBATH 升级建议 v0.1

> 日期：2026-10-21。状态：**研究分支提案 / 非实现授权 / 不替代 P3 现有验收合同**。需求来源：Commander 按战术价值改变目标、Formation 任务进展评价、自适应观察窗口及失败经验、Developer Mode 可视化、对 RBM AI Log 的核查。以下分为 VERIFIED（源码与手册确认）、CONFIRMED-DIRECTION（用户认可方向）与 PROPOSED（未定实现）。

## 1. 研究结论与边界
- **已核实的 RBM AI Log**：`RealisticBattleAiModule/AiModule/Diagnostics/AiBehaviorLogic.cs` 以每秒周期快照记录 Team 战术、Formation 的 ActiveBehavior/移动命令；战术、行为、命令改变时记 CHANGE；每五秒针对步兵编队生成 AGENTS/LAG 队列用于诊断队形离散与落后士兵。写入端 `AiBehaviorLog.cs` 缓冲并定期 Flush；每场战斗单独文件、自动控制最新十份。该单兵诊断含按单位读取目标槽位、导航相关工作，**不是万人同屏默认配置的参考实现**。
- **已核实的 CB2 实现（当前 master 于研究时，未验证 CBATH 本体源码）**：`Program/src/Host/ObservationAdapter.cs` 有 `Resolve/Emit/Shutdown`、带 `battle_id/session_id/mission_time/producer_seq/record_id` 的 JSONL、有界队列和异步写盘。 `Program/src/Host/TestDriver.cs` 已产生 `input.read`、`plan.created/changed/continued`、`task.created/feedback/closed`、`write.executed/blocked`、`plan.invalidated`、`mission.start/end` 等事件；`TestDriver.ActionFeedback.cs` 已有 `action.feedback`。事件名称存在不等于数据足以诊断全部长期目标。
- **角色边界**：CB2 的战场输入/任务评估属于 AI 核心；CB2 事件遥测是旁路；CBATH 执行实机采集、独立观测、回放/对照和离线分析；游戏内 Developer HUD 可在 CB2 中以只读方式展示。**不得把 CBATH 测试工具变成 Commander 的必需运行依赖**。
- **硬性边界**：不改 Agent 单兵战斗 AI；可在必要时只读诊断，但不得常规高频逐 Agent 采样。Player/F6 控制权保护、现有 P3 任务/门槛不因观测扩展而被破坏。研究分支不修改生产代码。

## 2. 升级目标
让开发者、测试者和用户选择性查看：**AI 为什么选择此目标 → 期待什么变化 → Formation 实际做了什么 → 战况发生了什么变化 → 何时/为何重新评价 → 为什么继续、调整方法或切换目标**。同时允许**独立观测**验证「AI 自称的效果」，支持定位命令竞争、重复尝试、战术横跳和性能问题。

### 2.1 不要把一个“日志”混成三个概念
| 数据 | 来源 | 可信边界 |
|---|---|---|
| 战术意图（Commander/Formation） | CB2 决策侧发事件 | 证明 AI 计算/选择了什么，不证明成功执行 |
| 战场状态与命令实际后值 | CB2 原生快照及 CBATH 独立观测 | 证明可观测事实，无法自动证明是某任务独力造成 |
| 任务评价与推断 | CB2 的任务进度/价值评估器 | 预测或判断，不等于地面真值；记录依赖和不确定性 |

## 3. 建议增加的结构化事件（PROPOSED）
保留已有事件，采用**增量 schema 扩展**，禁止随意重命名/删除现行 `Resolve/Emit/Shutdown` 和已用事件字段。下列事件名称仅为方案，不是已实现：
| 候选事件 | 必须能回答的问题 | 主要字段示例 |
|---|---|---|
| `task.progress` | 任务为何判定有进展、停滞或恶化？ | task_id、goal_type、window_id、metric_observed、trend、confidence、data_age |
| `task.obstruction` | 路线/兵力/威胁使任务受阻了吗？ | task_id、cause、evidence_ref、time_since_progress |
| `task.attempt` | 重复的具体方法是否已经失败？条件是否改变？ | approach_key、outcome、blocking_state_signature |
| `plan.reevaluation` | 为什么本轮需要重新比较？ | trigger（periodic/stall/value_opportunity/hard_invalid/emergency）、input_snapshot_id |
| `plan.option_compared` | 为什么继续突破不如转为消耗？ | option_id、feasible、constraint_checks、expected_effect、risk、switch_cost、uncertainty、rejected_reason |
| `task.reassigned` | 谁批准更换目标？原任务如何收束？ | old_task_id、new_task_id、authorization、reason |
| `task.compliance` | 未完成是战斗受阻还是越权？ | compliance_status、constraint_id、observed_evidence |
| `diagnostics.health` | 遥测有无丢失、降级、超预算？ | dropped_count、snapshot_age、diagnostic_cost、queue_depth |

共同键建议 `battle_id / mission_time / team_id / formation_id / plan_id / task_id / event_seq / schema_version`；动态 Formation 重新分组时要避免 ID 混淆。全局时间需与 CBATH 独立采样可对齐。
- **不要每次重评价都把所有候选方案完整 JSON 化**；只在详细诊断模式保留多候选的详细比较。常规模式保留决定、原因代码与必要证据。
- 因果区分：`observed`（观察事实）、`inferred`（估计）、`intended`（计划）字段或事件语义不能混用。第三方观测只记录独立可观察结果。
- 契约守约状态与任务结局分开：被敌军打退可能 FAILED，但不等于 COMPLIANCE_VIOLATION。信息不足记录 UNKNOWN。

## 4. 双时基：变化事件 + 稀疏快照（PROPOSED）
1. **高价值变化即时事件化**：计划选定/改派、任务状态改变、玩家接管、命令执行/拦截、目标/权限硬失效，优先记事件发生的游戏时间；不能以每秒采样替代。
2. **低频状态快照**：任务进度趋势、Formation 部署、当前人数、弹药分布摘要、威胁、任务依赖健康。采样频率可配置，默认不为每编队每帧采集。RBM 每秒仅是参考，不视为 CB 的最终频率。
3. **昂贵的深度诊断按需启用**：只针对选中 Formation 和限定时间，或离线 CBATH 实测; 不默认计算单兵槽位误差、逐击日志或遍历全部 10k Agent。
4. **短期失败尝试记忆来自战术层**，诊断只读导出，不应因为开启日志才存在，也不让调试 UI 改变记忆和重规划逻辑。

## 5. 操作模式与性能（PROPOSED）
| 模式 | 用途 | 采集建议 |
|---|---|---|
| OFF（默认） | 普通玩家 | 仅 AI 自身必要的任务评估状态；不生成调试字符串/磁盘明细/历史 UI 曲线 |
| SUMMARY | 轻量诊断 | 关键事件、有限频率状态摘要、错误/切换原因 |
| DETAILED | 开发者或回归定位 | 启用更多候选评价/任务趋势，对选中 Formation 限定深度 |
| CBATH-SESSION | 自动测试/回放 | 与 CBATH 会话标识对齐，独立采集真实命令后值、实际战场变化和覆盖情况 |

- 有界内存环形缓冲、异步写盘、背压和丢失计数、任务结束归档、每场战斗单独身份。旧日志滚动归档策略需要单独设计；RBM 保留 10 份不自动成为需求，**不要影响 CBATH 的正式证据留存**。
- 正常模式下开/关 Developer HUD 不改变 Commander 输入、任务选择和结果；性能测量覆盖通常人数到 Limitless 扩大后的实际可用战斗场景。最终 CPU/分配/丢日志阈值待实测裁决，不虚构预算。
- 默认 HUD 不遍历 Agent：只读最近一次已发布的任务摘要；低频 UI 刷新；若日志系统失败，不得影响 AI 决策或游戏稳定。
- 诊断运行与普通运行的公平比较，要核查观测开销是否改变决策时序；结果必须分开记录。

## 6. MCM Developer Mode、游戏内 HUD 与 CBATH 查看器
- MCM: `Developer Mode` 总开关，`Show Task Monitor`、`Diagnostics Detail`、`Selected Formation` 及导出开关。配置 UI 不等于战场叠加 UI。
- 战斗内 HUD 只读显示：当前计划与战术目标、Formations 任务、进度趋势、观察窗口、滞回/切换代价的状态、最新阻碍和重评原因、最近几次任务变化。仅建议方案，不决定最终 UI 布局。
- CBATH/离线查看器：时间线对齐 `intended → planned → write attempted/executed/blocked → observed state → feedback → replan`；比较敌军/友军与 RBM/原版基线；导出任务级验证报告。
- **不要**通过 HUD 直接控制战术作为本期范围；观测和指挥操作两者权限分离。

## 7. 建议对接开发流程
- 设计阶段：为任务契约定义标准 telemetry schema、观测字段与缺失口径，优先复用 `ObservationAdapter` 和已有事件。
- 实施阶段：保持现行 CB2 与观测组的独立工作树/文件归属；CB2 组改核心任务事件与宿主接入；观测组单独扩展 CBATH/TCL 采集与查看器；统一在隔离集成分支验证。先补现有事件的可观察性缺口，再增加 HUD。
- 验收：A 事件链的完整性与 ID 对齐；B 观察事实和任务判断不混淆；C 玩家接管/权限变化和任务重分配；D 观测开/关的决策不变性与帧时开销；E 日志缓冲/丢失可见；F 失败、任务受阻、行为偏离分别有正确解释。
- 建议在 P3 当前合同不变的前提下添加**独立可观测性工作包候选**，与 P4 战术重评能力开发同步定义协议，在 P5—P6 做完整性能与可诊断性回归。正式阶段挂接与优先级由负责人在修订 Roadmap 时审批，不在此自动增补 P3 Exit Gate。

## 8. 当前未决事项（U11）
1. CBATH 现有仓库本体的采样、回放、数据布局和独立观测覆盖率——目前只核实 CB2 主仓的 CBATH 接入约束及 ObservationAdapter，**未完成 CBATH 本体代码审计**。
2. 游戏内独立 HUD 与离线 CBATH 查看器各做到何种深度；首版哪些必须交付。
3. 详细事件 schema、数据持续时间、实时缓冲/归档上限和大规模采样成本。
4. RBM 和 Vanilla 对照究竟是并行测试还是同场组合运行；不能同时接管 Formation 后宣称兼容。
5. 用户对 Developer Mode 的公开程度和隐私/磁盘开销提示。

## 9. RBM 核查资料
- 手册： https://fellow93.github.io/RealisticBattleProject/features/ai/ （AI log、Overlay、Developer Mode）
- 设置： https://fellow93.github.io/RealisticBattleProject/settings/ （开关与默认行为）
- 源码： https://github.com/Fellow93/RealisticBattleProject/blob/master/RealisticBattleAiModule/AiModule/Diagnostics/AiBehaviorLogic.cs
- 源码： https://github.com/Fellow93/RealisticBattleProject/blob/master/RealisticBattleAiModule/AiModule/Diagnostics/AiBehaviorLog.cs
- CB2 主仓： `AGENTS.md`、`Program/src/Host/ObservationAdapter.cs`、`Program/src/Host/TestDriver.cs`、`Program/src/Host/TestDriver.ActionFeedback.cs`。
