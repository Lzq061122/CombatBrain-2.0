# CombatBrain 2.0｜IF-FEEDBACK-01 多维任务健康反馈与重大变化事件（2026-10-22）

> **CONFIRMED：IF-FEEDBACK-01 方案 C（反馈组织原则）**，即“**少量独立状态维度 + 重大变化事件**”。具体状态轴枚举、事件字段、频率、严重性阈值、滞回/去重、持久化、消息可靠性和验收门槛均 **OPEN/PROPOSED**。以下是架构反例检验，**非游戏实机 PASS**。只改研究分支，**不修改 master、现行 P3 合同、CBATH 或生产代码**。

## 一、产品负责人明确签收

2026-10-22 用户答复：**“IF-FEEDBACK-01 我选择方案 C。请检查是否会造成重复状态机、频繁重规划或反馈与战术价值评价职责重叠；压力测试后记录确认结果，并按依赖顺序继续。”**

因此批准如下上层原则：

1. **双通道但单一权威**：Formation 对 Commander 提供少量独立、可判未知的任务健康维度；关键健康变化、职责风险、局部紧急行动及需要重评的事实可以事件化即时报告。**这些健康维度与事件都是 TaskContract 的派生观测/诊断，不是新的任务状态机或第二个任务所有者**。任务创建、取消、职责交接及 Revision 只能由当前合法 Commander/已批准单一 Task 生命周期管理与写令边界实施；不能因记录一条 `BLOCKED` 或 `FAILED` 事件直接修改任务。
2. **事实 / Formation 局部判断 / Commander 裁决分离**：健康报告区分实际读取的兵数、位置、命令执行观察等事实，与“路线阻塞”“预计无法持续牵制”等局部推断及不确定性；Formation **没有**全军战术价值、胜负收益、撤销他队任务、解除牺牲命令或全局重规划的决策权。
3. **事件不等于换令**：重大变化事件可以请求 Commander 提前对相关计划/依赖做**有界检查**；是否维持/调整 Intent 必须由 Commander 结合 D-EVAL-01 相对当前计划的全局净价值及 U01.6 的硬失效/重大威胁/决定性机会、职责交接、换令成本等再裁决。必须保留重复报告/细小抖动的抑制；**报告不会自动产生一个新计划**。
4. **异常安全路径与全局重规划解耦**：在 U-AUTH-04 B / U-AUTH-05 C 的普通安全包络内，Formation 可及时执行局部合规避险并上报；U-AUTH-06 C / U-AUTH-06b C 的紧急越界仍按已确认的严格条件独立判许可；**不得因 Commander 尚未处理健康事件而停止必要本地安全行为**。Local Safety 的命令与普通 Task 命令共用唯一执行权限/版本裁决，不新增并列 Writer。
5. **记录/控制解耦**：运行时 Commander 所需的健康事实、重大事件不能只依赖 CBATH/异步 JSONL 观测队列；CBATH 仍是独立、可降级的旁路日志，不能成为运行时决策必须等待/消费的数据源。
6. **实现细节没有通过本次投票被签收**：推荐候选维度 `Progress`、`Feasibility`、`Threat`、`DutyIntegrity`、`LocalSafetyAction`、`Compliance/Authority` 仅为 PROPOSED；可根据实机证据并合并、调整字段，而不必为每一维度创造独立状态机。未批准任何定期刷新毫秒数、数据 Schema、事件严重性枚举或每一维度独立阈值。

## 二、三项专门压力测试

### A. 避免重复状态机

**风险 A1：** `TaskFeedback` 把未取得位移的 Engage 判 `Failed`，`ActionFeedbackTracker` 在 P3 又因自身全场计算选择后继计划，新健康系统若再维护 `TaskState.Failed` 并直接关闭当前 `TaskContract`，会形成三个相互矛盾的任务主张。**要求**：现行 P3 专项代码的历史职责保持，未来设计以单一授权计划/任务生命周期为权威，兼容现有 `PlanLedger` 与 `FormationTask` 的身份/Revision；新健康结果只作为带证据的派生观察/申请，不能自行变更生命周期。是否扩展 PlanLedger 或引入受其约束的正式 Task Registry 是 OPEN，不能宣称现有 PlanLedger 已完整实现所需合同。

**风险 A2：** 不同维度互相冲突（进攻受阻、职责有效、战损很大、控制权限仍合法）。**要求**：状态维度本身不合成为单个绝对 `FAILED`；状态未知仍为 UNKNOWN，Commander 根据职责及任务合同解释。行为阶段/防抖可有有限局部记忆，但不能拥有另一个能开关关键 Task 的权威状态机。

### B. 避免频繁重规划

**风险 B1：** 威胁在临界附近反复跨越触发点、连续发布重大事件。**要求**：健康读数变化、事件生成与全军重评分层；允许事件去重、持续证据/升级、冷却与状态滞回（具体数值 OPEN）。只有 Commander 判断已达 U01.6 的转换门槛才重写 Operational Intent。

**风险 B2：** 紧急事件需要迅速响应，简单低频计划器导致 Formation 被灭。**要求**：Formation 按既定 Local Safety Envelope 立即采取合法动作；重大事件只触发有限早期评估/求援，不强制全军规划或拉长安全行动响应时间。

**风险 B3：** 多个 Formation 同时报告风险，彼此独立生成“必须重新规划”结论。**要求**：Commander 结合职责依赖/共享战场快照做一次有界综合检查；抑制重复触发，防止局部自保叠加破坏全军意图。

### C. 避免与 D-EVAL-01 决策权冲突

**风险 C1：** Formation 损失很高，但有效牵制敌军，局部评价被误认为全军计划亏损。**要求**：汇报战损与职责贡献的观测/推断并列，不得自动将高战损当作总体失败或取消骑兵侧击。Commander 用 D-EVAL-01 比较当前计划/可行候选整体边际价值。

**风险 C2：** Formation 预测高概率阵线崩溃并上报“已丧失职责”。**要求**：事实、预测、置信度与未观察到的情况分离；`AT_RISK/UNKNOWN` 不冒充“已客观无法履职”；严守 U-AUTH-06b 明示牺牲职责的高门槛。

**风险 C3：** Formation 发生局部合法撤离后，报告 `ActionSucceeded`，被 Commander 当作“战术达成”。**要求**：写令被接受、局部安全动作执行、任务效果、总体价值属于不同证据；允许出现“安全行为有效、原牵制职责受损”。

## 三、跨模块对抗反例（纸面审查；非 PASS）

| ID | 输入 | 不合格表现 | 必须满足的机制边界 |
|---|---|---|---|
| IF01-1 | 突破受阻但仍牵制敌方主力 | `BLOCKED` 自动取消所有职责 | 任务行动/职责/价值分别判断；只允许有权 Commander 解除 |
| IF01-2 | 伤亡高，Commander 明示不惜代价坚守 | `HighLoss` 直接触发自由撤退 | 不等于已失去有效牵制；遵 U-AUTH-06b |
| IF01-3 | 风险数值临界摆动 | 每 tick 事件、全军重新派单、前进后撤振荡 | 变化门槛/滞回/事件去重，整体换令由 Commander 决定 |
| IF01-4 | 弓兵面临即将到来的骑兵冲击 | 等待 Commander 日志队列/下一全局 tick 才允许自保 | Local Safety 直接在合法包络内动作并异步上报 |
| IF01-5 | 日志写入失败/CBATH 未安装 | Commander 收不到紧急报告 | 决策闭环的进程内反馈与 CBATH 观测分离 |
| IF01-6 | 任务重组/玩家 F6 接管，旧报告延迟到达 | 旧 Formation 事件使新身份/新 Task 退回旧计划 | 必须带来源身份/版本并过期过滤（字段精确方案 IF-FEEDBACK-02 OPEN） |
| IF01-7 | 状态与事件重复送达/不同顺序 | 重复关闭任务，反复消耗重试额度 | 幂等消费与权威快照校对，去重/ACK 策略 OPEN |
| IF01-8 | 局部防御命令执行成功，但牵制职责已失去 | 安全行为成功自动标整个 Task 成功 | 动作执行结果与职责真实效果分离 |
| IF01-9 | 三个编队同时报告能力下降 | 触发三次相互竞争的全局 Planner | Commander 汇总依赖和共享事实，再单点裁决 |
| IF01-10 | 某维度未知，另一维度已显示危险 | 缺字段默认为安全、或者预测变成已失能事实 | 支持 UNKNOWN/证据来源/新鲜度，不制造确定性 |
| IF01-11 | 系统负载升高，健康采样次数减少 | 降级策略丢掉权限复核、急迫安全反馈 | 保证基本权限、安全反馈和任务/职责连续性，不承诺已批准刷新频率 |
| IF01-12 | Agent 溃逃导致 Formation 有效兵力下降 | 把个体溃逃误记为 Formation 自主全军撤退 | 不修改 Agent；合法报告有效能力及职责缺位 |

**审查结论**：已选方案 C **可与既有架构一致**，但要实行“**单一任务生命周期写权 + 非权威多维健康观察 + 重大事实事件 + Commander 唯一全局重评和裁决**”。若为每个维度建可自主开关任务的状态机，或让每条事件自动跑 D-EVAL-01 并发布新 Intent，方案 C 将违背 D-ARCH-01、C17 与 U01.6。

## 四、真实源码证据（截至本次读取的研究分支）

- `Program/src/Core/BattlePlan.cs`: `FormationTask` 已有 `TaskId/PlanId`、`ObjectIdentity`、`Revision`、`Role` 与 `GuardsTaskId/SupportsTaskId/DependsOnTaskId` 等；这些是实体键与有限依赖基础，**不构成完整的职责锚点或健康状态合同**。
- `Program/src/Core/TaskFeedback.cs`: 对 `Engage/Reposition/Hold` 等有限任务按写令后命令族、位移和 deadline 判断 `TaskFeedbackState.Pending/Effective/Failed`，有 `PlanInvalidReason` 与 `RoleRequirementSatisfied` 提升路径；**它主要是行动是否生效的反馈，不是全军 Task 意义上的所有失败/失能状态**。
- `Program/src/Core/PlanLedger.cs`: 已有计划创建/Continue/Invalidate/BumpRevision/失败分配重试/清理；仍未有本提议的统一多维健康报告接口和任务紧急越界合同。
- `Program/src/Host/TestDriver.ActionFeedback.cs`: P3 `ActionFeedbackTracker` 专项读取快照、计算并在某些反馈决定后调用 `BeginSuccessorPlan`；存在未来通用反馈若另行控制后继计划就重复决定的**迁移风险**。不能在未正式变更 P3 之前重写其既有逻辑或暗称统一已经完成。
- `Program/src/Host/ObservationAdapter.cs`: `Emit` 输出 JSONL 的 CBATH 旁路观测事件，队列满可以丢弃；它**不能是 Commander 接收安全关键事件的唯一渠道**。旁路观察日志与运行时决策消息需要明确隔离。
- D-EVAL-01 已确认的是由 Commander 负责**相对当前 Intent 的全局净价值评估**，该输入可包含局部任务健康证据，但 Formation 不得直接输出整场胜率、全局净收益或自决任务重排。

## 五、后续单一 OPEN

**NEXT IF-FEEDBACK-02｜多维健康事实/事件的身份与时序契约**：如何保证每条重大 Formation 报告有可验证的任务/Formation 身份和版本、新鲜度、来源/证据与覆盖权；当玩家撤回 F6、编队重组、旧任务换版、多人同时报告、事件重复/延迟时，Commander 如何消费消息且不重放旧行动？并保证紧急反馈不依赖 CBATH 的允许丢失日志路径。建议比较“只查实时快照”“纯事件流”“**当前权威健康快照 + 版本化变更事件**”；这是 PROPOSED 而非本次签收。具体字段、TTL/去重、ACK、更新频率仍 OPEN。

之后按依赖顺序研究 IF-EVAL-01 局部反馈如何触发但不主导 Commander D-EVAL-01 重评、IF-SAFE-01 临时安全包络原子交接、IF-TEST-01 真实引擎/CBATH 验收，再接 U-TAC-03、U-ARMY、Campaign Fixture。以上确认均不视为已经代码实现或测试通过。
