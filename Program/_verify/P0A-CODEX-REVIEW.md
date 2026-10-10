# CombatBrain 2.0 P0 第一批独立验收（2026-10-10）

受审源码：`5f00bf91f3695377b51e4b8894402c427e008d54`；交接更正：`456c642a5252e04bbf3b1028b6af9959c849552b`。实机材料：`sessions/20261010-171526/`，pid 20316。审查者：Codex。

**结论：工程加载、已有双侧观测、两次外部 Stop 写入及有限关闭的事实保留；P0 第一批整体暂不通过。派单要求的 C 路线没有实现，现有 REAL_SELECTION 日志不能证明原生选择了 2.0 的行为。只需回到该命令通路整改，不重开 TCL 验证工程。**

本次只读取受审源码、测试工程定义、run 6 原始日志与独立快照、受审/部署文件身份；没有构建、测试、部署或启动游戏，不 push。开发报告的 Core 43/43 结果保留，不冒充本次重新执行的结果。测试工程只链接 5 个 Core 源文件，Host 不在该测试范围内。

## 1. 主线实现偏离

原派单 `TacticalCommandLab/_dispatch/2026-10-10-cb20-p0-minimal-command-path.md` 第 54–57 行要求：一个站定行为进入原生选择，候选预计算不写令；真实获选后才执行，跨线程转交时执行前重核当前行为、对象与意图。

当前实际链如下：

1. `Host/SubModule.cs` 只注册 MissionBehavior；工程编译的 13 个源文件没有自定义 Formation `BehaviorComponent` 或向 `FormationAI` 注册该候选的实现。
2. `Host/TestDriver.cs:291-325` 的 SubmitCandidateOnly 只选出目标，并在第 317 行调用本地 ExecutionGate.SubmitCandidate；`Core/ExecutionGate.cs:36-42` 只写本地字典。这不是向原生行为选择器提交候选。
3. `Host/SamplingBehavior.cs:181-215` 订阅所有 Formation 的 OnBeforeMovementOrderApplied，把非本方的普通写令回调交给 OnNativeSelected。
4. `Host/TestDriver.cs:43-85` 仅检查回调编队键与本地目标相同，就调用 MarkRealSelection；没有检查获选行为是 2.0 的站定行为。第 329–357 行随后写 REAL_SELECTION。
5. `Host/TestDriver.cs:179-185,361-426` 在下一 Mission tick 自行调用 SetMovementOrder(Stop)。它是独立于原生行为产出的外部写入。

run 6 也直接呈现该链：L63 为 `order_enum=Move our_write=0 source=not_ours`，L64 标 REAL_SELECTION，L65 下一 tick 写 Stop；第二条意图的 L223–224 重复此过程。原生发出了 Move，不能由此推导原生选中了 CB2 的 Stop 候选。日志名不是选择证据。

因此，两次 Move→StandYourGround 的后值变化是真实交付记录，但只能保留为外部写令 PoC，不能裁定 C/Route A 或 FD-PoC P11 通过。

## 2. 同一执行通路的静态资格缺口

`Host/TestDriver.cs:370-378` 的执行前检查只确认当前 Mission、CountOfUnits 和玩家归属，没有实时检查 IsAIControlled，也没有比较当前对象与已绑定对象；它还未检查当前 ActiveBehavior。`Core/ExecutionGate` 虽保存 ObjectIdentity，RequestWrite 只比较 Team/Formation 键，没有消费该对象身份。

这不证明 run 6 已发生对象替换或丢失 AI 控制；该场没有触发这些变化。但它不满足派单中跨线程/下一 tick 执行前的复核要求。实现真正的 C 通路时一并补上：同 Mission、同实际 Formation 对象、当前仍为本方获选行为、AI 受控、非玩家拥有、有限意图仍有效。不能取得资格时停止本次命令，不能按同索引绑定新对象。无需为了此项扩展通用框架或重跑所有生命周期场景。

## 3. 本次直接确认的事实和限制

| 项目 | 独立审查结果 |
|---|---|
| 产物身份 | 受审 bin 与部署 DLL 均 37376 B / `44845964c8ad9d825309ac053529f68be2b909efd8807a1bf7e57d02a667d8b2`，run 6 L1 记录同摘要、加载路径和 MVID。456c642 的复现口径更正保留 |
| 模块隔离 | run 6 身份与引擎模块记录显示 CombatBrain2 与 CBATH；旧 CombatBrain 未作为该场被测载荷加载 |
| 双侧观测 | CBATH early 在 mission_time=0.444 得到 Defender 99 / Attacker 95；late 在 152.908 得到 65 / 58。CB2 at=1.082 为 99/95，at=152.105 为 66/58；晚期差 1 人发生在不同采样时刻，战损解释是合理推断，不宣称已单独证明该一次损失的原因。跨时刻位置不能按逐字相等验收 |
| 快照健康 | 152 条 SNAPSHOT_HEALTH、已有无重复统计与实际刷新记录保留；age 恒 0 不证明陈旧快照拒绝已在实机触发 |
| 两次写入 | L65 与 L224 分别记录 Move→StandYourGround、invoked=1、无异常。invoked 仅在普通返回后置位，不能单独补全“原方法完成/未被跳过”证据；候选阶段没有 setter 的事实仍保留 |
| 原生获选 | **未实现／不通过**，原因见 §1；REAL_SELECTION 标记撤回为该判据的通过依据 |
| 到期及关闭 | intent-1 在 L185 到期关闭，intent-2 在 L253 显式关闭；逐意图核对，两者关闭后均没有该意图的 WRITE_EXECUTED。68 条 POST_OBSERVE 及 L557 MISSION_CLEANUP 保留 |
| 撤销和其他边界 | 两次 REVOKE_SKIPPED、零 REVOKE_EXECUTED；真实回退、玩家接管、目标失效、选择超时和下一 Mission 无残留继续未验证，不要求为了本次审查补齐全矩阵 |
| 清理 | 该场最后控制台记录 end_mission、games_stop、无残留进程、无新增崩溃目录及 RESULT: OK。属于原场证据，不冒充本次再次清理 |

run 6 `CombatBrain2.log` 当前 SHA256：`2a595638958d63725fbe53c60335ecf08146e6f0321cafd2eb45813fb7cf732e`，与交接索引一致。原日志及部署产物未修改。

## 4. 有限整改与停止条件

下一实现任务只修命令通路：

- 将单个站定 Behavior 真正注册到目标 Formation 的原生选择链。候选只计算命令，用本方行为的实际获选身份/回调作为交付依据；普通外部 setter 事件只保留为观测，不再授予执行资格。
- 如需转交主线程，执行前完成 §2 的实时复核。测试驱动只开启/撤销有限意图，不另有一条并列的直接写令腿。
- 取得原方法完成或被跳过的可核对事实及实际后值；invoked=1、Prefix 或既有 Stop 后值不能单独构成完成证明。
- 退出时撤销本方行为影响，证明旧意图不再执行；不强行重建旧 Move，不无限加权、不自动改成 B。
- 新构建只补必要生产逻辑检查和一场目标 C 通路验证；未获选保存原因，不能重新使用普通 Native Move 冒充通过。加载、状态读取和已完成工具工作直接复用，只有受影响部分才重复核对。

本次不修改生产代码，不自行实施下一任务；整改范围仍是原派单，没有扩大成新战术或平台建设。

## 5. 摘要与源码身份的决定

**保留 RuntimeIdentity 的 assembly_file_sha256，不取消、不降为无用字段，也不新增代码段摘要作为当前任务。** 它用来标识加载路径上的具体磁盘产物，并与部署文件绑定；源码提交、dirty diff、编译环境及当时编译输入是另一组事实。后者不能由 DLL 摘要相同或差异很少自动推导。

456c642 中的五个偏移区间只描述那两个 37376 B 文件。未来文件布局可能变化，不能固定清零这五段后宣称同一源码；需要比较时按实际 PE 元数据字段解释。该构建差异问题已经收口，不再为此重建或追查。
