# CombatBrain 2.0｜U-AUTH-02 写令授权证据与 Harmony/TCL 迁移研究（2026-10-22）

> 状态：**U-AUTH-02 CONFIRMED（产品/权限架构原则）**；Harmony 具体部署/运行时权限证据来源、Native 控制手递手交接和各域安全接管为 **OPEN**。TCL 是独立科研与实机验证仓库，不等于 CB2 已实现此能力。**仅研究分支；不修改 master/P3/CBATH 合同。**

## 1. 用户明确裁决与背景（CONFIRMED）

2026-10-22，用户回答：“**关于这个权限问题，补充一下，我们的2.0 mod是基于harmony的，应该按理说是能接管的，之前的TCL项目也研究过这个，你可以去看一下仓库；我同意C**。”

因此确认 **U-AUTH-02 方案 C：授权来源 + 实时原生状态 + 写入前复核**，配合已确认的 U-AUTH-01 稳定合法指挥域/动态可写 Formation 集合、U-SIDE-01 敌我独立接管开关和玩家手动权优先：

- **来源**：只有已核实的原生控制权/玩家当前 F6 委托/CB 开关与合法域才能构成 CB 对目标 Formation 的权限依据；`Formation.IsAIControlled` 或 `Team.IsPlayerTeam` 单独不足以证明玩家给 CB 的授权。
- **实时**：玩家手动接管、F6 撤回、其他领主独立指挥、增援、拆并、Mission 结束等须实时改变授权集合，不将缓存视为当前事实。
- **写入点**：运行时发出/执行每条命令前复核对象身份、域、授权来源、控制策略和版本；权限未知或失效时不抢该 Formation 指令，取消冲突/过期请求，其他合法编队继续运行。
- **Harmony 定位**：可用 Harmony 接管、观察或抑制已证实的原生管线环节，但**技术上的 Patch 权力 ≠ 玩家/战役语义上的授权权力**。是否由“原生管线集成”常态执行业务，还是为必要命令使用严格独占写入口拦截，是下一工程决策，不能在本次借 U-AUTH-02 的“C”签收 TCL 架构 C。
- **恢复**：敌我接管关闭、玩家 F6 收回或 Mission 结束必须能合法交还 Native；“停发 CB 新指令”不等于清除了行为/控制残留。强拦截能力未在某域证实或异常时，不得以高频抢写冒充完整控制。

## 2. 读取 TCL GitHub 的直接证据（EVIDENCE）

TCL 仓库：`Lzq061122/TacticalCommandLab`，读取 `codex/local-phase10-verification-20261006`（分支头 `a4524aa1cad75f085ad9c866a4dda457dfd6ad49`），优先以 `docs/07-final-architecture.md` 与 `docs/local-phase10-verification-20261006.md` 的**后续收口与审查边界**解释早期 `docs/02`、`docs/10` 内尚未实测的历史文本。

- **目标版本吻合**：本地 Phase 10 实际运行 Bannerlord **v1.4.8.119303**，与 CB2 当前指定游戏版本一致；Harmony 模块 `2.4.2.248`，`0Harmony.dll` 文件 `2.4.2.0`。该结论仅对该实测环境与当次 Mod 组合成立。
- **Patch 确认**：Patch 账本有 **7** 个生效入口：`SetMovementOrder`、`SetFacingOrder`、`SetArrangementOrder`、`SetControlledByAI`、`SetFormOrder`、`SetFiringOrder`、`SetRidingOrder`。准确区分“Patch 确实生效”和“此入口被真正调用”。
- **限定实机成功**：run05 H2 在 T1F0/Movement/HOLD **24.50 秒有效意图窗口**内持续 hold **100%**，窗口内 **49 次 Native Movement 写入尝试均被拒**，G1 与 G2 PASS；**同场全局 G3 Oscillating FAIL**。不能声称所有编队/全场稳定、所有接管域成功。
- **尚未验证**：Facing Full、Arrangement Full、全量写入口覆盖与各场景泛化。原生 `Formation.Tick` 内的 Arrangement 自维护路径及 Movement substitute 写回有结构性绕过风险；对某一次采样没有观察到缺口不能推断 100% 覆盖。
- **H3 代价**：`Formation.SetControlledByAI(false)` 在特定环境能抑制原生 AI 路径，但也停掉正常 FormationAI 行为选择，**不等于**优雅地仅接管某项 Order；H3 判据通过不代表全场稳定或原生管线集成已验证。
- **交还研究**：run08–11 的 REL 指标 0/0/0/1（最后为正确识别的预期失败）；某些场次 Native 自然恢复，但仍**未证明**到期、异常或内部 Timer/Behavior 状态的完整恢复。
- **重要 Core 反例**：`docs/07-final-architecture.md` §5.5 最小复现显示 `WritePort.FlushTick` 先仲裁再检查权限：非法高优先级候选可淘汰合法低优先级候选，最后两者均被拒，最终命令 `Unset`。生产契约应当**先做权限许可与原始身份检查，再对合法候选进行仲裁**。这是 TCL 发现的未修复实现差距，不应直接复制代码当作 CB2 完整方案。
- **生命周期其他缺口**：`ProbeRuntime.Acquire` 在取得 lease 之前改宿主 AI 控制（失败回滚无保证）；到期 sweep 不等同于宿主恢复；在途旧令的原始 Token/Generation 证据不全；Release 异常路径与原方法成功完成的取证仍有缺口。
- **TCL 架构建议（不是 CB2 已签收）**：`docs/07-final-architecture.md` 将研究中的 A=并列抢写、B=基于 Full Lease 的独占、C=原生管线集成；推荐**常态 TCL-C、确需确定性时在已证明可恢复的域内受限 TCL-B**，拒绝并列 Writer 频繁抢写。这里的 TCL-C 与本次 **U-AUTH-02 方案 C（授权证据）不是同一分类**。Phase 11 通过的是设计/审计交付，非全域接管或 CB2 集成验收。

## 3. 压力测试与必须保持的安全边界

| 情境 | U-AUTH-02 应做到 | TCL 证据/遗留风险 |
|---|---|---|
| 玩家手动接管原 F6 编队 | 立即失去 CB 写令资格；旧命令/异步任务不可复活 | Harmony 可拦，但必须识别来源和真实命令顺序；TCL 未证明真实 UI/F6 完整链 |
| 敌侧 CB OFF、我方 CB ON | 敌侧实际 Native 控制，不因残留 Harmony gate 阻断 | REL 只证明部分恢复行为，异常/到期/全域仍 OPEN |
| 获取 Full 控制失败 | 不遗留 `SetControlledByAI(false)` 或阻断 Native | TCL 现有 Acquire 顺序有已登记的回滚缺口 |
| 一条命令被拖延，期间 Formation 被重新分配/重编 | 旧请求携带创建时的身份/Token/Generation，被写入点拒绝 | TCL 核查当前同步 Probe 未实现此完整合同 |
| 不同域同时写入且互相级联 | 权限检查和跨域副作用要有覆盖证据 | Movement 有限定成功，Facing/Arrangement 未验收，Arrangement 可能绕过常规入口 |
| 非法高优先级竞争者与合法低优先级竞争者 | 非法源先淘汰，再从合法源中仲裁 | TCL §5.5 有运行的反例，现有 TCL Core 次序相反 |

## 4. 代码状态与下一依赖

CB2 研究分支当前 `Program/src/Host/SamplingBehavior.cs` 等仍是 P1/P2/P3 限定入口与测试循环，`FormationSampler.cs` 有 AI/玩家状态采集但没有证明授权来源的生产级实现；TCL Probe 不直接成为 CB2 已部署代码或产品合同。**未改任何生产代码**。

**NEXT OPEN：U-AUTH-03 原生执行链的接管与可逆交还策略。** 需在 Harmony 已有证据下决定：默认在原生 Tactic→FormationAI→Behavior 链中集成（不新增竞争 Writer），对于必须保证的 CB Order 是否允许经分域 Ownership/Lease/Token 和写入口防护进入受限独占；还是选择其他明确策略。不能把“Harmony 能 Patch”当成完整全域 Full 的许可证，不能牺牲首版 F6/敌军真实接管、CB OFF 真 Native 和玩家指令优先。具体命令域、哪条原生 Patch 与恢复方式仍需在目标版本按自动场景验证。

其后仍有 U-TAC-03（最高 Tactics 合格角色与变更规则）、U-ARMY、CampaignFixture、D-DATA-01。保持单问题推进。
