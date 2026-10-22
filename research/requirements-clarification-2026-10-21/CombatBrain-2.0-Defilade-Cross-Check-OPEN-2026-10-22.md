# CombatBrain 2.0｜《Defilade》交叉审查：覆盖、接口缺口与 OPEN 队列（2026-10-22）

> **研究审查文件 / 不是新的已确认需求**。本文件仅整理用户由《Defilade》相关讨论带来的架构核对要点；没有核验《Defilade》实际源代码，也不以《Defilade》为实现正确性证据。仅按 CB2 的 GitHub 研究分支当前决策与代码区分 **CONFIRMED 已覆盖 / OPEN 接口待定义 / PROPOSED 建议 / EVIDENCE 限定工程事实**；未获确认的建议**不录入**正式确认设计台账、不修改 master/P3/CBATH。

## 1. 读取与证据身份

来源：`research/requirements-clarification-20261021`，该轮读取最新 HEAD `f69fb4e508d62334ede102e8ee7ca69af28b8633`（写入新记录前），包括：
- `CombatBrain-2.0-Confirmed-Design-Ledger-2026-10-22.md` §§B、K～U；
- `D-EVAL-01-Decision`、`D-CAP-01-Decision`、`D-PRED-01-Decision`；
- `U-AUTH-01`～`U-AUTH-05` 决策，`Intent-Architecture-Pressure-Test-v0.1`；
- `Open-Decisions-Test-Scenarios-v0.2`、`Requirements-Integrated-v0.5-DRAFT`、`Cross-Document-Completeness-Audit`；
- 现场源代码 `Program/src/Core/BattlePlan.cs`、`TaskFeedback.cs`、`PlanLedger.cs`。

此处只评估所提供的**概念和接口需求**是否已在 CB2 决策中出现；不声称游戏《Defilade》的内部 AI 系统一定采用相同架构。

## 2. 交叉映射（不把既有原则包装为新功能）

| 用户提供的启发 | 已有 CB2 决策与状态 | 应如何处理 |
|---|---|---|
| Commander 负责全局战术价值与是否重规划 | **CONFIRMED** D-EVAL-01 全球边际价值比较、U01.6 全局变更、D-ARCH-01 单一 Commander 意图 | **COVERED**；不得重问或建立第二价值决策器 |
| Formation 监测任务进展、受阻、战损与局部威胁 | **CONFIRMED 原则** TaskContract、D-ARCH-01、U-AUTH-04/05；实际反馈字段/频率 **OPEN** | **COVERED 原则，接口缺口 OPEN** |
| 局部战损不是整体战术失败 | **CONFIRMED** D-EVAL-01、P09、任务效果/合规双轴、TS01 | **COVERED**；不能把受损/被推退自动认定抗命或全军失败 |
| Formation 可在授权内应急/局部脱离 | **CONFIRMED** U-AUTH-04 B、U-AUTH-05 C；U-AUTH-06 C（同轮另行签收） | **COVERED**；紧急超约束例外需继续限制 |
| 明确任务目标、关键保护职责、允许安全行为、异常报告 | **CONFIRMED** Task Contract 语义、C17、U-AUTH-05 安全包络；**具体 schema OPEN** | **COVERED 原则，接口缺口 OPEN** |
| 玩家有效命令优先，不干预 Agent | **CONFIRMED** P05/P06、U-AUTH-01/02/03a | **COVERED** |
| 区分正常执行、能力下降、暂时受阻、实际不可行 | **CONFIRMED 分层原则**任务状态与合规分离；当前候选枚举尚未签收 | **COVERED 目标，模型 OPEN**；不能把这些字符串当已批准的最终枚举 |
| 由 Formation 结构化报告，Commander 汇总并重新评价 | **CONFIRMED 架构职责** D-ARCH-01、U01.6、D-EVAL-01；跨层事件契约未定义 | **COVERED 原则，接口缺口 OPEN** |
| 抑制局部波动导致频繁重规划与命令振荡 | **CONFIRMED** C17、U01.6、局部快速健康检测/低频全局重评；具体时序参数 OPEN | **COVERED 原则，接口/测试 OPEN** |

**总体结论**：没有发现要推翻 Commander 的独占全局战术权、Formation 有约束自主性、任务结果/合规分离或 Agent 原生层边界的新架构问题。用户讨论并非新需求批准，严禁覆盖先前已签收规则。

## 3. 值得登记的具体接口/合同缺口（OPEN，不录入确认台账）

1. **IF-FEEDBACK-01｜多维任务健康报告**：任务 `Progress`、`Feasibility`、`Threat`、`DutyIntegrity`、`LocalSafetyAction`、`Compliance/Authority` 是否应拆为独立字段/状态轴？报告中哪些是客观 Snapshot，哪些是 Formation 判断，哪些需 Commander 裁决？不得由 Formation 把“尚未推进”直接升级为全军失败。
2. **IF-FEEDBACK-02｜事件交接与时序**：Formation 事件如何绑定 Mission/Domain/Formation 身份 epoch、`TaskId/TaskRevision`、时间、证据、风险/严重级别、请求支援的动作和原因？去重、失效/过期、Commander ACK 与旧命令不可复活的语义如何定义？是否复用现有 `PlanLedger` 而不是新建互相冲突的状态机？
3. **IF-EVAL-01｜反馈接入全局价值评估**：如何把局部 `BLOCKED/AT_RISK`、损失及职责变化变为 Commander D-EVAL-01 的合法事实输入与重评触发器，同时严格区分“触发评估”与“必然放弃旧计划”？如何绑定局部效果评估和独立的最终胜负/伤亡证据，不循环自证？
4. **IF-SAFE-01｜任务终止到过渡安全状态的原子交接**：`TaskCancelled` 不等于 `DutyCancelled`；需要可审计的职责锚点、临时安全包络版本、立即失效的旧 writer 与恢复/报告机制，避免空挡、双 Writer 和高频摆动。
5. **IF-TEST-01｜反例与观测合同**：注册“局部高伤亡但全局牵制有效”“任务受阻但尚可继续”“退出安全动作后再次触发”“多编队同时避险致联动失守”“失权/F6 手动夺令”“Commander 刻意牺牲”等测试输入，分别记录反馈延迟、合规/结果、重评次数、任务写入与实际引擎动作、CB vs Native handback 和统计对照。未批准具体阈值，不能声称完成运行测试。

**现有代码边界（EVIDENCE）**：`BattlePlan.cs` 的 `FormationTask` 已有 `TaskId`、`Revision`、`Role`、简单协同依赖；`TaskFeedback.cs` 返回 `TaskFeedbackState`（目前主要使用 `Pending/Effective/Failed`）与 `PlanInvalidReason`，缺乏已确认的多维健康报告与职责锚点；`PlanLedger.cs` 维护计划 Revision、Invalidate 与有限失败重试。这些是接口构建起点，不是新机制已完成的证据。

## 4. 真正未解决的独立设计问题 vs 已覆盖内容

- **真正新需裁决的核心冲突**：**U-AUTH-06b：Commander 明示接受牺牲的关键职责与 Formation 极端生存例外之间的优先级**。此问题来自 U-AUTH-06 而非《Defilade》的独立新原则；本交叉审查强化了必须明确其任务合同表示和遵守/失败区别。
- **工程级开放项而不是另一个全局架构提案**：`IF-FEEDBACK-01/02`、`IF-EVAL-01`、`IF-SAFE-01`、`IF-TEST-01` 应在上述权限语义定案后，随 U-TASK、U-OBS、U-ACCEPT、CBATH 研究按依赖逐一确定。不应抢在 U-AUTH-06b 前重新质疑已确认的局部脱离权限。
- **既有长期 OPEN 继续保留**：U-TAC-03 最高技能参与人员和生效时序、多领主 Campaign 真实控制身份、D-DATA-01 实机校准、具体 Command/Formation Hook 覆盖和发布测试门槛均未因审查自动解决。

## 5. 变更管控

1. 本文件仅是 OPEN 清单与覆盖性审查，不批准字段 Schema、阈值、算法、行为清单及测试通过状态。
2. **只**将用户本轮明确选择的 U-AUTH-06 C 记录为新的 CONFIRMED 决策；`Defilade` 交叉审查建议不进入正式确认设计台账。
3. 不打断当前依赖链：**下一项先 U-AUTH-06b**，之后回到 Local Safety/Task Feedback 接口收口，再进入相关 U-TAC/U-ARMY/测试。
