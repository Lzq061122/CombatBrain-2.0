# CombatBrain 2.0｜U-AUTH-03 原生管线集成 + 有界独占：已确认裁决（2026-10-22）

> **状态：U-AUTH-03 CONFIRMED（执行链架构方向）**。每种战术/命令域的保证级别、升级/降级条件、Harmony Patch/安全交还、性能与正式验收仍 OPEN。本文件仅用于 `research/requirements-clarification-20261021`，**不修改 master、现行 P3 实施合同、CBATH 或其他生产代码**。

## 一、产品负责人裁决（CONFIRMED）

2026-10-22，在 U-AUTH-03 问题“是否认可方案 C——原生管线集成为基础，必要时使用有界、可验证、可交还的 Harmony 独占控制？”后，产品负责人明确答复：**“认可”**。

因此签收 **方案 C：优先集成原生 Tactic → FormationAI → Behavior → Order 管线；需要保证任务执行且已具有权限、覆盖与可逆交还证据时，才对限定的 Formation/命令域使用受限 Harmony 独占控制**。

这一决定不意味着“原生 AI 只是接受建议就能满足 CombatBrain”；核心战术意图与任务必须有可检查的**实际执行结果**。也不意味着所有 Formation 常态全域独占：对原生管线已经可靠达成的行为，避免新增无必要的 Writer 或抑制。**拒绝**用提高写令频率、循环争夺原生控制、只观察任务“已发出”就报告成功等代替明确的控制策略。

### 已确认的约束与上位需求衔接

1. **权限优先**：延续 U-AUTH-01 稳定合法指挥域及动态可写 Formation 集合、U-AUTH-02 授权来源与当前原生状态双重检查、执行前再次核实。Harmony 提供技术拦截能力，**绝不授予玩家未给出的 F6 权限**；不触碰未授权友军与手动编队。
2. **单一有效控制路径**：默认尝试在原生管线中影响/替换战术产出、保留其合理基础行为，避免同一 Formation/命令域由 CB 和 Native 长期并列争写。若需要在特定域实施确定性 CB 控制，控制权与下令来源必须明确，不能仅调用 `SetControlledByAI(false)` 假定所有域已独占。
3. **受限独占可申请，但不是无条件升级**：必须有原生层面确认的控制资格、对相关写入路径的覆盖证据、可观察执行结果，以及在释放、权限撤回、异常、任务过期和 Mission 生命周期结束时**可逆交还**的机制。缺失证据不能假装 Full 已经启用或稳定。必要时必须表现为明确的能力不可用/任务重评，而非静默降级后声称完成。
4. **按实际关联命令域验证**：Movement、Facing、Arrangement、Firing、Form、Riding 等可能通过多个入口和 Formation 内部自维护相互影响；不能从单个 Movement Patch PASS 推广到 Facing/Arrangement 或全命令层。
5. **先资格再仲裁**：为任务选择合法候选与写令做许可/原始身份筛查，然后才进行合法候选间的评分/仲裁；过期 Token、Lease、Generation 与旧 Formation/Task 版本必须在写令边界被拒（具体 token 方案仍属实现选择，非强制某现有 TCL 类库）。
6. **双端接管/交还**：U-SIDE-01 敌方/玩家侧 F6 的独立 ON/OFF 必须使关闭侧真实恢复 Native，不保留 CB 抑制、权重或在途写令；手动玩家命令优先。继续遵守不修改 Agent 层以及计算预算的有界退化原则。
7. **证据分层**：注册 Harmony Patch、原生写入被拦、命令实际生效、任务效果持续、原生安全交还、全场稳定、Campaign/F6 集成都需要分别验证，不得将 TCL 已有研究当作 CB2 已完成部署或产品 PASS。

## 二、TCL 研究证据与不可过度外推的边界（EVIDENCE）

- TCL `codex/local-phase10-verification-20261006` 的 `docs/07-final-architecture.md` 提议“原生管线集成为常态，受证据支持的特定域才使用 Full Ownership/Lease/Token”，并反对持续并列 Writer 高频抢写；该方向可以作为 CB2 决策参考，**不是 CB2 的既有产品实现**。
- TCL 在目标 Bannerlord v1.4.8.119303 的实机中验证了 7 个 Harmony Patch 入口，run05 H2 的 **T1F0 Movement/HOLD 24.50s**、有效窗口内 **49 次 Native Movement 尝试被拒**；同时 **全场 G3 Oscillating FAIL**。不能由这一个窗口保证全场长期稳定。
- Facing/Arrangement Full、所有写入路径覆盖、到期/异常/多任务交还仍缺少完整证据；`Formation.Tick` 自维护路径可能绕过普通 Set*Order 写入口。
- TCL `WritePort.FlushTick` 有“先仲裁后许可”反例；`Acquire` 先改宿主 AI 状态、令牌/到期/Release 恢复不完整等差距仍未解决。不能直接拷贝 TCL 现有实现当作生产标准。
- CB2 当前 `Program/src/Host/Cb2Tactic.cs` 有基于可执行任务参与战术选择与取消/交还的有限代码，但其注释明确这是 P3 未接线/未部署候选，不是完整产品化命令接管。

## 三、反例与尚未解决的合同

| 反例 | 本架构必须满足 | OPEN |
|---|---|---|
| CB 规划“护弓兵”，原生 Behavior 反复改成追击 | 不能报告护卫已执行；需要有效任务执行/复核及必要时的合规升级或改派 | U-AUTH-04 什么任务需要保证控制、何时选择升级 |
| 只验证 Movement，包抄还要求 Facing/Arrangement | 不能把 Movement HOLD 的证据当作包抄完整执行 PASS | 命令域依赖、跨域副作用与覆盖矩阵 |
| 玩家把 F6 骑兵改为手动 | 在新命令实际写入前必须拒绝 CB 过期请求并停止冲突控制；合法交还 | 真实 F6 事件与回收延迟 |
| CB 一侧 OFF 或 Mission 结束 | 不残留 CB 写令和 Harmony 对原生的抑制，独立验证 Native 恢复 | 交还/异常/到期恢复 |
| 加盟领主、军团增援、Formation 合并重建 | 不能复用旧授权、旧命令或将非授权友军纳入 | 实际 Campaign Team/Army 与身份映射 |
| 某命令域不具备可恢复 Full 证据 | 不能临时无界强夺；真实表达“不具备执行保障” | 任务可行性筛查与受控退化 |

## 四、下一独立决策（OPEN）

**U-AUTH-04：对于需要保证执行的关键 Formation Task，能力保证应在“制定并承诺战术计划之前”预检，还是主要依靠运行时发现原生不遵守后再自动升级独占？** 该选择决定是否允许先承诺一项当前无合法可靠执行路径的关键职责、任务的保证级别以及失效重评方式；不得将数值阈值、具体任务白名单或域映射伪装成已签收。推荐考虑“规划前验证 + 运行时监测/必要时受限升级”的方案，但仍待负责人裁决。

后续依赖包括 U-TAC-03 合格参与成员与最高 Tactics 生命周期、U-ARMY 真实军团指挥映射、自动 CampaignFixture、D-DATA-01 实战证据设计。保持每轮只讨论一个核心 OPEN。
