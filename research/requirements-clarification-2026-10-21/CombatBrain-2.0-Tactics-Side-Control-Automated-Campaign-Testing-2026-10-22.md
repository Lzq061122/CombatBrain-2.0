# CombatBrain 2.0｜Tactics 取值、双端独立接管、自动化战役验证（2026-10-22）

> **性质**：需求澄清增量记录，仅在 `research/requirements-clarification-20261021`。CONFIRMED 是负责人明确表达的产品选择；OPEN 是不可偷填的实现/验收细节；PROPOSED 是本轮推荐但尚未获用户逐项批准的机制。**不代表已有能力、部署可用或 P3 验收结论**。不修改 master、现行 P3 或 CBATH 测试合同。

## 1. 本轮产品负责人明确表达的要求

### U-TAC-01：按参与成员中最高 Tactics 决定 Commander 能力（CONFIRMED：方向；OPEN：候选范围）

用户明确提出：“**我觉得用战术等级最高的人的这个值，来作为整个 commander 能力的判定的标准，这是没问题的**。”

- 指挥**能力**输入以指定战场友方合格成员中的**最高原版 Tactics 值**为准；沿用原版技能成长，使用已有 Tactics 预设映射框架，不增加独立养成。
- 必须与真实 **Command Authority**（哪支 Formation 受谁/玩家/当前 F6 托管）分离：有最高技能的成员不因此成为获得全军指挥权的角色。
- **OPEN**：合格“成员”的确切定义（同阵营在场英雄 / 实际参与军团各 Party 的角色 / 当前 Commander 合法控制范围的在场角色等）；不存在英雄、角色受伤或离场、替换统帅、玩家 F6 参与范围、独立 AI Team 等情况下何时重算和使用什么 fallback。不能从“最高”推出跨敌对侧、跨未授权部队、跨未入场援军读取技能，也不能把原版选统帅方式当已验证。
- 旧文 C10/U-ARMY 的 Tactics 来源 OPEN 经本轮澄清为“**取最高技能的方向确认，候选集合和生命周期 OPEN**”。若另有历史助手转述“实际统帅技能”的说法，以产品负责人的**最新明确裁决**为准，不声称旧文已签署相反规则。

### U-SIDE-01：我方/敌方 AI 接管独立开关（CONFIRMED：首版需求）

用户明确提出 CombatBrain 可“**只管敌方 AI，或者说只管我方 AI**”，“**敌方 AI 的接管关掉，这样就是原版 AI 接入敌方，我方 AI 接入我方**”，并强调“**这个功能得做**”，目的包括与原版 AI 受控对照，以检验战术性能。

- 用户可独立开启/关闭**敌方 AI 的 CB 指挥接管**和**玩家侧合法 F6 托管的 CB 接管**，但必须保持已确认的首版双端可用，不能把关某一侧当取消另一侧发布需求。
- **OFF 语义原则**：被关闭的该侧应回到**真正原生指挥控制**，CB 不得继续产生该侧候选、任务写令、遗留控制权、隐形 Order 或行为权重干预；CB 可以不影响结果地旁路观察，但也要核真实性和额外开销。
- **ON 语义原则**：仅接管授权 AI Formation；我方仍尊重玩家手动命令与 F6 逐 Formation 控制权；“我方开启”不等于接管玩家手动 Formation、所有同盟军或替换整个 Team AI。
- 推荐的四组对照：CB敌 OFF/我方OFF＝Native/Native；敌ON/我方OFF＝CB/Native；敌OFF/我方ON＝Native/CB；双ON＝CB/CB（以上以玩家队、AI托管身份和真实参战阵营为准，而非固定 teamIndex）。注意 CB/Native 的双方标签需按战场实际席位记录；战斗条件、战斗规则、士气、角色技能、地图、初始部署与玩家干预要匹配。
- **OPEN**：MCM 设置名称、默认值、即时生效/下场生效、战中切换的安全交还、F6 指令仲裁、原生 Tactic/FormationAI/Harmony 接管拆装、无残留的实际验证及设置持久化。

### U-AUTO-01：无人值守自动化实机验证为主要开发测试方式（CONFIRMED：用户要求/开发约束；OPEN：具体实现）

用户明确表示**不接受必须本人亲自游玩 Campaign 才能测试**，要求预设场景并借工具复现 Campaign 相对 Custom Battle 的差异，以减少低效人工操作。

- 用 **真实 Bannerlord Mission/Agent/Formation 战斗引擎**在无人值守状态下跑受控场景：混编、不同来源/技能的指挥官、军团队伍/Formation 分配、场景地形部署、波次/援军、普通士气、玩家合法托管与手动权限冲突等。自动生成、执行、收集独立证据、清理；不要求用户手工跑场。
- **等价性边界**：仅在 Custom Battle 创建 Campaign 风格的军团阵容和兵种，并不代表进入了真实 Campaign 遭遇流程。可证明战术核心在同类条件下的功能/性能与若干反例，**不能单独证明** Campaign 的 `MapEvent`/`PlayerEncounter` → `CampaignMission` 创建、实际 Party/Army/指挥权/F6/Perk、生效加成、援军/结算生命周期正常。
- **推荐两条均自动化的入口（PROPOSED）**：
  1. `ScenarioFixture` / CustomBattle 真实引擎受控实验：尽量可重复构造复杂配置，完成多数战术逻辑/预测和 A/B 对照。
  2. `CampaignFixture` / 固定战役存档或测试专用世界状态 + 自动化触发真实 Encounter→Campaign Mission：少量关键模式适配及泛化烟测/回归，自动采集并退出；是否可无UI可靠触发和精确复现要按目标 1.4.8.119303 与 CBATH 实际能力核查，**不得宣称现已支持**。
- 预定环境要区分原版战斗士气与 CBATH 的“死战不退”干预，后者现行 P3 规定不修改；新预测/发布验证另设隔离场。CBATH 为开发工具和独立观测，不是正式 CB Commander 运行时依赖。
- **D-DATA-01 仍 OPEN**：需要经过“合成 Mission 能覆盖哪些 Campaign 差异 + 实际 Campaign 流程哪些不可替代”的差异矩阵与可行性验证后再批准数据路线；不强求用户人工打 Campaign。

## 2. 与现有仓库实现相关的证据（EVIDENCE）

- `Program/src/Host/SubModule.cs` 在 `OnMissionBehaviorInitialize` 给 Mission 加载 `SamplingBehavior`，没有直接限制 CustomBattle；**这不证明** Campaign 端的身份/场景/权限/AI 行为正确。
- `Program/src/Core/BattleAssessment.cs` 当前状态假设被控制者为非玩家 Team、敌方为玩家 Team，依赖 `IsPlayerTeam`；`FormationSampler.cs` 遍历所有 `mission.Teams` 的编队，但遍历不等于正确的参战军团与 Side/权限映射。
- `Program/src/Host/SamplingBehavior.cs` 现有 P1/P2/P3 哨兵开关为测试循环模式，不是产品级**敌我独立接管开关**；`Cb2Tactic` 已有有限取消/交还草图，不等于所有真实控制权回收或原生基线已通过。
- `Program/_verify/run-p3.ps1` 固定从 `CustomBattleScreen` 启动，经 CBATH 配置人数/兵种构成、代理控制和限制性敌方单次动作/采样；未建立真实 Campaign Encounter 的自动化入口。
- 旧需求已经确认：敌方 AI 增强和玩家 F6 托管**均属首版硬要求**、指挥权限优先、军团野战首版必须支持、Agent 层不修改。新开关是补充，可用于强弱对照，不改变上述产品范围。

## 3. 下一单一决策候选（OPEN）

**U-TAC-02：最高 Tactics 的取值范围与身份变化**。建议优先裁定实际计算集合，避免跨未授权英雄/没有参与实际战斗的角色错误借用 Tactics。也可先为 U-AUTO-01 做 API/可行性证据，但不能把未经实测的 Campaign 自动化能力当成既有能力。
