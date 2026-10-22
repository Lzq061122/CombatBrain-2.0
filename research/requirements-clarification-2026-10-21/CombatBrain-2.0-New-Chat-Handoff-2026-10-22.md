# CombatBrain 2.0｜新对话接续手册（2026-10-22）

> **2026-10-23导航更新**：本手册保留旧背景，当前讨论进度与研究建议请优先读[新Chat接续入口](CombatBrain-2.0-Chat-Research-Handoff-2026-10-23.md)。D-EVAL-01的旧开场已更新为IF-FEEDBACK-02；后续用户已确认 AR-01 的分语义信息有效性原则，见[专项记录](CombatBrain-2.0-IF-FEEDBACK-02-AR-01-Decision-2026-10-23.md)。下一题为 AR-02，具体接口仍 OPEN。

> 目标：用户因旧会话过长，转入新会话继续 **系统需求澄清与压力测试**。本文件是“交接索引”，不是 PRD 终稿、不是代码开发授权。确认原则均保存于本研究分支，`master` 与当前 P3 阶段合同未因本次归档而更新。

## 1. 新对话从哪里开始

**当前下一核心问题：IF-FEEDBACK-02 / AR-02「可验证派发身份与版本作用域」。AR-01 分语义有效性原则已确认，IF-FEEDBACK-02 整体仍 OPEN。**

D-EVAL-01架构及其后的D-CAP/D-PRED、U-AUTH和IF-FEEDBACK-01已有增量签收；完整进度见[2026-10-23接续入口](CombatBrain-2.0-Chat-Research-Handoff-2026-10-23.md)。

当前已确定 **D-ARCH-01 / D-ARCH-02 / U01.6 / D-OPP-01**。不要反复提问：

- **D-ARCH-01**：唯一 Commander 拥有全军 Operational Intent；Formation 在已授权 Task Contract 目标/约束内自主执行，不得擅自改全军战略方向。
- **U01.6**：全军意图在硬失效、重大威胁重排或决定性机会时条件切换，关键职责必须移交或明确接受空缺风险。
- **D-ARCH-02**：从战场 Threat/Vulnerability/Opportunity 产生希望改变的状态目标；再检验 Formation 能力与协同任务，而非仅从固定包抄/突破脚本中选择。
- **D-OPP-01**：用户已选 **B**：共享的稀疏 Formation/区域关系分析 + 有限可组合机会检测器 + Commander 全局评价。允许尝试通过牵制、施压主动创造机会，但敌方反应只能作为预测，不能写成已发生事实。

**当前用户没有签收新的风险人格 A/B/C**：上一对话助手一度将 B 标成推荐，又声称偏好 C；已纠错。两者并非互斥；此前有效的用户原则仍是：**优先争取胜利，避免无谓伤亡，允许必要牺牲，但具体是否冒险要看战局/收益/失败代价**。D-EVAL-01的评价架构已由后续裁决确认，具体风险与估值口径按台账及新增建议卡细化；不再次逼迫用户选择固定冒险人格。

## 2. 从 GitHub 加载什么（阅读次序）

研究分支：[`research/requirements-clarification-20261021`](https://github.com/Lzq061122/CombatBrain-2.0/tree/research/requirements-clarification-20261021)

1. **必须先读**：[已确认设计台账 2026-10-22](CombatBrain-2.0-Confirmed-Design-Ledger-2026-10-22.md)——截至当前全部明确确认原则与首版范围、历史冲突解释。
2. **再读**：[跨文档完整性审计](CombatBrain-2.0-Cross-Document-Completeness-Audit-2026-10-22.md)——哪些旧文件过期、恢复了哪些遗漏、怎样避免假 PASS。
3. [Requirements Integrated v0.5 DRAFT](CombatBrain-2.0-Requirements-Integrated-v0.5-DRAFT.md)——任务契约、战术架构、兵种画像、观察、D-OPP-01 与 §16 新补录。
4. [Intent Architecture Pressure Test v0.1](CombatBrain-2.0-Intent-Architecture-Pressure-Test-v0.1.md)——Operational Intent 生成、唯一控制链、稀疏关系、当前下一题 D-EVAL-01。
5. [Charter / Roadmap Change Proposal v0.2](CombatBrain-2.0-Charter-Roadmap-Change-Proposal-v0.2.md)——哪些确认需求将来需要回写 Charter 和 P0–P6；不代表当下合同变更。
6. [Open Decisions & Test Scenarios v0.2](CombatBrain-2.0-Open-Decisions-Test-Scenarios-v0.2.md)——TS01–TS43 分层压力测试候选，尤其 TS30–TS43 的军团/重组/权限/预算缺漏补测。
7. [CBATH Observability Proposal v0.1](CombatBrain-2.0-Observability-CBATH-Upgrade-Proposal-v0.1.md)——事件、独立验证与离线回放优先，实时 HUD 后置。

原始用户提供的三份在 `charter/`：`Charter V0.2.md`（内容 v0.3）、`CombatBrain-2.0-Requirements-Clarification.md`、`CombatBrain-2.0-Development-Roadmap-v0.1.md`（内容 v0.2）。它们是历史上位来源，但**晚于它们的用户明确裁决优先**。当前研究未直接覆盖三份原件。

## 3. 首版必须记住的“硬”条件

- **敌军 AI + 玩家 F6 必须同时达到可发布条件**，玩家直接命令和授权范围优先。
- **正常 Campaign 野战 + 多领主军团野战**必须支持；真实统帅、军团 Team 映射和援军所有权 **尚待查证**，不得凭一个 Team 对一个领主的假设建模。
- **首版必须有真实可用的 Formation 动态拆分、合并、按能力重编，配备 MCM 开关**；安全才重组，激战优先不大拆；可以安全局部抽调，失败只跳过重组不使其他指挥停摆；玩家手动接管保留当时合法编组并取消 CB 在途冲突命令。
- **严格仅 Commander/Formation，不碰 Agent 战斗 AI**。原版士气/个人溃逃首版可保留，指挥应识别并应对；全面士气重整仅是后续方向。
- **真实能力优于文化**；原版兵种离线多维画像、混编轻量动态聚合、Vanilla/RBM Combat 双模型；没有画像的第三方兵种仍必须基础安全可指挥。
- **RBM Combat 优先而非 RBM AI 共存**：关闭 RBM AI 是首版兼容方式；与其他抢同一指挥权的 AI 深度并存不在首版硬要求内。
- **Limitless 万人规模是条件式首版兼容目标**，要先证明实际活跃人数、可控 Formation、性能和 Mod 组合；不因尚未支持 10k 宣布正常首版必然不能发布。
- **低频 Commander 全局规划 + 及时 Formation 基本反馈 + 有界计算退化**：负载高时削减多余候选和预测，不得损害玩家权、关键职责、基本反馈与安全退出。
- **战术架构**：Commander → Operational Intent → 参数化/可验证任务契约 → Formation 局部自主 → 有权限的唯一执行门/原生命令 → 独立观测与反馈；主动创造局部优势，避免计划反复横跳。

## 4. 历史后续问题清单（状态以台账增量和新接续入口为准）

1. 首版 TaskType 最小、足够可组合的语义库与可测责任；如何处理失败、未知和约束违反。
2. Commander/Formation/原生 F6 与玩家接管的安全权责、军团队伍真实映射和统帅 Tactics。
3. 动态重编/合并腾槽/援军入场的合法时机、MCM 切换、异常降级和可证明行为。
4. 离线兵种画像字段、两种 Combat 校准、混编/未知值、安全时间预算。
5. 局部机会候选检测器最小集合、对手有限反应预测、稳定性、性能退化。
6. CBATH 独立观测、真实野战对照、角色/地图/随机种子、首版发布的正式成功门槛及 Mod 版本矩阵。

一轮只深入一个依赖最高的核心问题；**已经明确认可的原则直接复用**，没有定量证据则标 OPEN，不猜参数；发现矛盾时指出再讨论，不自动选择某个工程实现。

## 5. 项目状态与安全说明

- 之前 P0–P2 的验收具有**限定测试合同**；2026-10-20 的资料显示 P3 **尚未验收通过**，后续以最新提交/现场证据为准，不自动沿用旧状态。
- `master` 中旧 Requirements 依然保留历史时点“军团野战是否首版必要”的 OPEN；**这是历史文本未合并的状态差**，不是用户撤回了确认。研究分支新台账已经明确纠正。
- 新文档**不是生产实现指令**；用户要求先完成系统性需求澄清、压力测试、最终统一并产出审签版 Charter、Requirements、Roadmap，再确定正式 PR 和任务派发。
- GitHub 当前为研究中的单一可交接事实索引；讨论可继续在 Chat 中，避免反复在 Chat 与 Work 之间转发。

## 6. 新对话可直接发送的开场请求

> 继续CombatBrain 2.0系统需求澄清。先读取本分支`research/requirements-clarification-2026-10-21/CombatBrain-2.0-Chat-Research-Handoff-2026-10-23.md`、完整确认台账和其中链接的研究建议卡。Astra两阶段资料仅为证据和建议，尚未整体采纳。AR-01的分语义信息有效性原则已确认，当前从IF-FEEDBACK-02 / AR-02“可验证派发身份与版本作用域”继续，一次只讨论一个核心问题，先说明反例、取舍和推荐再让我决定；不要重问已签收架构，不修改master、生产代码或现行P3合同。
