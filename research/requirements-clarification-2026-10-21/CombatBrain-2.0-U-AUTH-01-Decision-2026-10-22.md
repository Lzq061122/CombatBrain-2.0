# CombatBrain 2.0｜U-AUTH-01 指挥域与动态写令授权：决策与反例压力测试（2026-10-22）

> 状态：**CONFIRMED 架构原则**；Game Mission 权限映射、具体事件与适配方式、自动化验收仍为 **OPEN**，本文反例的建议实现为 **PROPOSED**。此文仅在 `research/requirements-clarification-20261021` 分支记录，不修改 `master`、现行 P3 合同、生产代码或 CBATH。

## 1. 产品负责人签收的选择（CONFIRMED）

2026-10-22 用户答复：“**U-AUTH-01 我选择方案 C。请进行反例检验，确认后记录到研究分支，并继续下一个依赖问题。**”

正式确认**稳定合法指挥域（Legal Command Domain）+ 动态 Formation 写令授权集合（Authorized Formation Set）**：

1. **指挥域身份/责任稳定，授权动态**：Commander 围绕引擎/战役实际许可的管辖范围维护版本化 `Operational Intent`；F6 的单一 Formation 交还、增援或重编不必把整个指挥域/Commander 销毁重建；真正规则/统帅/权限边界发生变化时须合法重建或移交，Mission 结束不得持有失效身份。
2. **观察与命令分离**：可以使用既有 P07 的指挥视角战场观察信息分析所有相关阵营/友军/敌方状态；但**实际命令只允许写给当前拥有可证授权的 Formation**。玩家手动指令和其他未授权友军不可被 CB 夺令。高 Tactics 角色不因此获得指挥权。
3. **意图不等于任意执行资格**：`Operational Intent` 可以连续，但任务必须随控制集合的变化取消、交接、重新分配或按已确认 U01.6 条件重评；关键职责仍须有人合法履行或明确承受缺位风险。已失权任务不能在原位自动复活。
4. **双端控制开关延续 U-SIDE-01**：敌军/玩家侧 F6 的 CB 接管分别可启停。禁用方应恢复原生 AI 并证明没有 CB 遗留任务、写令或行为权重副作用，不能把 `IsAIControlled` 当成 CB 接管权限本身。
5. **与 U-TAC-02 一致**：最高 Tactics 从该稳定合法域关联的合格参战成员中取；`Authorized Formation Set` 暂时缩减，不必等价于“谁可提供 Tactics”集合缩减；具体成员资格/失能/候补/刷新另列 OPEN。

本裁决不意味着一个真实军团一定映射一个 `Mission.Team`，也不授权突破原生引擎真实指挥权。特别是“一个 Commander 可以跨多个 Team/Party 统一协调”是既有产品意向，而**真实可控域边界仍须由原生行为和版本 1.4.8.119303 的实机证据支撑**；不能假定同战斗 Side 上的所有 Team 都可写令。

## 2. 反例压力测试（CONFIRMED 安全要求；机制 PROPOSED）

| 编号 | 场景 | 必须满足的安全结果 | 尚待实现/验证 |
|---|---|---|---|
| UA01-1 | 玩家手动接管一支 F6 编队，同时另一支仍在 F6 | 失权编队立即停止 CB 冲突写令/重编；其他合法任务不中断；旧计划不凭缓存夺回 | 玩家命令链的可靠来源与时序；任务依赖转移 |
| UA01-2 | 一段时间 Authorized Formation Set 为空 | 不向任何编队写令；不误把指挥域消亡、不断重建或将 Native 归为 CB；稳定待命/退出条件明确 | 仅观察/待命状态、生命周期、性能 |
| UA01-3 | 增援 Formation 或原有空槽变为非空 |先核身份/Team/主从关系/玩家授权，再合法归入；不能因 `IsAIControlled=true` 就占用 | 增援/槽位版本、与 Army/Party 映射 |
| UA01-4 | 拆分、合并、成员迁移、FormationIndex 被重用 | 不允许旧任务/命令引用不同对象或错误成员；玩家接管后保留已合法重编结果 | 引擎重组、安全映射与身份 epoch |
| UA01-5 | 战场统帅/玩家角色变化、阵营授权变动、Mission teardown | 过期域、任务/控制引用失效；只能在真实合法的新域建立新 Commander | 真实原生事件和安全清理 |
| UA01-6 | CB 敌方关闭、我方开启；再反向或双方关闭 | 被关闭一侧确实由原生 AI 控制，且无残留 CB 行为、写令、权重；每侧独立 | MCM、Tactic/FormationAI 原生控制 handback |
| UA01-7 | F6 与原生程序引发的 IsAIControlled 变化不具有相同来源 | 没有可靠授权来源时不能擅自归入玩家 F6 域 | 原生 Controller/OrderController/控制权来源核查 |
| UA01-8 | 同战斗 Side 多 Team、多领主，但玩家只管其中一部分 | Commander 观察友军全部有关状态，只向合法域 Formation 写令，不跨 Party/Team 未授权夺权 | Campaign 权限映射与军团验收 |

**总体检验结论**：方案 C 无根本自相矛盾。若误把“稳定域”实现为“稳定不变的可写令 Formation 列表”，反例 1、3、4 均会失败；若把“AI 控制”实现为“CB 有授权”，反例 6、7、8 会失败；若为了连续 Intent 忽略原生权限，方案 C 实际上退化为越权方案 A。故应区分“域身份持续”“观察范围”“每次操作的写令资格”和“任务身份/版本”。

## 3. 现有仓库事实边界（EVIDENCE）

- `Program/src/Host/FormationSampler.cs` 能观测当前 Mission 中 Team/Formation 的编号、Side、PlayerTeam、IsAIControlled、IsPlayerControlled、对象引用身份及部分成员身份；**不代表**已解析玩家 F6 授权来源、Party/Army/多 Team 权限关系。
- `Program/src/Core/BattleAssessment.cs` 当前假定自己为非玩家 AI Team、敌人为 PlayerTeam 的 P3 限定路径，不是已实现通用域解析器。
- `Program/src/Host/EligibilityProbe.cs` 使用 `Formation.SetControlledByAI` / `Mission.JoinEnemyTeam` 的测试注入探针，证明有相关实验入口；不能据此宣称正式 F6/UI 权限转移链已验证。
- 公开 API 有 Formation 的 AI 控制开关与 Team 的 AI/OrderController 入口，但多个公开版本不等同于目标 Bannerlord **1.4.8.119303** 的实机正确性证据。
- 此研究决策不改动 P3 行为/测试合同，不宣称功能已经实现或 PASS。

## 4. 下一个独立 OPEN（U-AUTH-02）及后续依赖

**U-AUTH-02**：对于玩家侧 F6，如何建立可审计的**命令授权来源和变更追踪**：仅凭 `IsAIControlled` / Team 状态推断，还是必须结合原生 Controller/OrderController、玩家明确授权以及 CB 双端开关形成“可证明写令凭据”？执行前复核、来源无法判定时的安全回退与原生 handback 属于同一依赖问题；不要把自动 F6 状态等同于明确玩家授权。

其后 **U-TAC-03** 再明确本域最高 Tactics 的合格成员、角色/援军状态和无英雄回退；真实 Campaign Fixture 的可运行身份/授权证据亦需配合验证。这些都保持 OPEN，不能从方案 C 自动推断已授权具体字段、事件或判定阈值。
