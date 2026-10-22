# CombatBrain 2.0｜U-TAC-02 最高 Tactics 来源范围：已确认裁决（2026-10-22）

> 状态：**CONFIRMED（产品规则层）**。实现字段、具体运行时映射、极端场景和验收 OPEN。本文件仅为研究分支决策，不修改 `master`、现行 P3 代码/合同或 CBATH。

## 1. 裁决

项目负责人在 U-TAC-02 的 A/B/C 选项中明确回复：**“U-TAC-02 我选择方案 B。请记录并继续按依赖关系推进。”**

**U-TAC-02 方案 B：每个 CombatBrain Commander 使用其当前合法指挥域所关联的合格参战成员中，最高的原版 Tactics 等级，作为指挥能力预设的输入。**

不采用整个友军阵营的无条件最高值（方案 A），也不机械地限制为实际统帅的直属 Party 成员（方案 C）。

必须区分两个概念：
- **Command Authority（指挥权）**：引擎及玩家/F6/原生指挥关系赋予哪些 Formation 的实际控制资格。选取最高 Tactics **不能增加**指挥权限。
- **Tactical Competence（能力）**：在上述合法指挥域的合格参战成员中取 `max(Tactics)`，仅用于已确认的 CB 指挥能力预设；不是原版必然如此的断言，不修改角色技能或养成。

说明性反例：同阵营另一领主 Tactics 更高、却不属于本 Commander 的合法指挥域，**不得**因此借用他的技能；同一合法域内的参战角色即使不是名义统帅，**可以参与最高值比较**。Campaign 多领主不等于每领主一个 `Mission.Team`；勿用 `TeamIndex`、Party 所有权或玩家阵营关系单一代理真正的权限。

## 2. 仍然 OPEN 的细化

1. **U-AUTH-01（下一项基础依赖）**：如何由真实 Bannerlord Campaign/Custom Battle Mission 的 `Team`、`Formation`、实际军团/Party、玩家/F6 原生权限构造 Commander 的 **Legal Command Domain**（合法 Formation 集合），并跟踪域身份/生命周期？不能事先将 Party/领主/Team 等价。
2. **U-TAC-03**：`EligibleParticipants` 的具体成员边界，包括某英雄是否需要当前入场、派遣/待入场、阵亡/失能、手动控制 Formation 所属角色、无合格英雄时安全回退，以及何时重读最高值。需要在 U-AUTH-01 之后明确；不能因为上述工程细节 OPEN 就将 A/C 视为重新备选。
3. **Tactics→预设**：数值区间/能力差异和性能预算，不能人为减慢低技能指挥官基础反应；降级及变化时保留有效任务/权限、稳定 Operational Intent。
4. **验证**：无人值守引擎测试模拟并观察多领主、玩家部分 F6、增援、队长更换、技能最高者不在授权域内，以及自动化真实 Campaign Mission 身份/权限链；仅模拟角色列表不证明引擎真实权限。

## 3. 现有证据与变更边界

- 研究分支 `Program/src/Host/FormationSampler.cs` 遍历 `mission.Teams` 和编队，只读 `TeamIndex`、`Side`、`IsPlayerTeam`、`IsAiControlled`、`IsPlayerControlled`，没有真实 Army/Party/Commander Tactics 关联实现。
- `Program/src/Core/BattleAssessment.cs` 现有限定测试路径主要用玩家/非玩家 Team 构成两方，与本产品的新合法域决策不是同一已实现契约。
- 延续已经确认的首版敌方/玩家 F6 双端覆盖、敌我接管独立开关、单一受控域 Commander、Campaign 多领主必须支持、无需人工游玩 Campaign 的自动化实测原则。

本记录不声明已具备可靠的技能解析、指挥域图或战场权限判定；不改变生产代码与 P3 合同。
