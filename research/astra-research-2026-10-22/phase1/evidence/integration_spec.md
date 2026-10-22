> **研究资产阅读视图**：原件位于 [CombatBrain_Research_2026-10-22.zip](../../archives/CombatBrain_Research_2026-10-22.zip)，包内路径为 `evidence/integration_spec.md`。本视图只增加说明并调整指向省略文件的 Markdown 链接；代码保持原样。完整复现请在原 ZIP 的独立解压副本中运行。研究建议未自动获批。

# 推荐战术闭环的可靠性与迁移合同（候选附录）

**RECOMMENDED：把新候选/分配/反馈算法接到一个 Commander 决策入口，由扩展后的 `PlanLedger` 单独提交计划、Task 与 Duty 变更；Host 只取证、验证授权与执行，Formation 只在当前安全包络内选局部动作。** 不新建第二个可取消任务/发布计划的健康状态机。普通受阻优先局部修补，不自动 Native handback；权限撤销或 CB 故障走独立交还路径。本附录是可实施候选合同，**未修改生产代码、现行 P3 或正式验收**。

## 1. 基线与实际迁移点

源码/台账基线：CB2 `research/requirements-clarification-20261021` 固定提交 **`dd28965238a83c29ae824f7225d90e3b2c8bfce4`**；本轮主线快照为 `2138038cef53216118d445d37c5245b1bef019db`。下文路径相对此固定研究提交，可用 `https://github.com/Lzq061122/CombatBrain-2.0/blob/dd28965238a83c29ae824f7225d90e3b2c8bfce4/<path>#L<line>` 定位。

已读 `AGENTS.md` 的 PAIPOS Lite v0.1 与 `prompts/registry.json`：入口登记 ACTIVE；此次身份为跨仓库只读研究/候选设计，验证方式为静态源码核对和隔离原型，不执行登记表中旧生产派单，也不声称满足朋友电脑的生产部署流程。生产修改/观测文件归属及 `Resolve/Emit/Shutdown` 接入边界保留。

| 现有事实（静态） | 候选迁移及须保留的行为 |
|---|---|
| `Program/src/Core/BattlePlan.cs:81-118,281-325` 已有 TaskId、ObjectIdentity、Plan Revision、Role、简单依赖和后继来源；没有独立 Duty 或完整安全包络 | 保留已有ID和revision语义，新增 Commander 所有的 DutyRecord/TaskContract；Duty不是Task关闭的副作用 |
| `Program/src/Core/PlanLedger.cs:13-26,45-51,89-147` 负责 Current、建立/Adopt/Invalidate，但可写字段和Host后继路径仍分散 | 从“保存当前计划”收口为“提交计划变更的唯一入口”；Planner 只返回不可执行的 Candidate，Host反馈只返回事实/建议 |
| `Program/src/Core/TaskFeedback.cs:19-60,65-155` 检查对象、命令族、实际目标读回和位移；`BattlePlan.cs:129-217` 已区分战术质心目标T、写引擎锚点、实际读回 | 保留作为 ExecutionEvidence：命令生效≠职责有效≠全局战果；沿用写前 `anchor=T+(A-C)`，不把创建计划前的原生位移算作本令效果 |
| `Program/src/Host/TestDriver.ActionFeedback.cs:170-172,518-595` 会直接决定后继；`:341-386` 退出确认后 `_plans.Adopt(successor)` | P3旧路径原样保留；新路线把其结果变为 `ReevaluationRequest`，仅 Commander 做全局净价值比较并通过Ledger提交。不能同时保留旧路径和新路径两个writer |
| `Program/src/Host/TestDriver.cs:2291-2397` 当前整计划失效会关闭任务、对仍合格对象排独立Stop、清handoff、撤行为 | 这是当前P3专项合同，不直接推广成“任务取消就Stop/退走”。新路线针对受影响任务修补；若旧任务关闭但关键Duty继续，提交当前代数的临时安全契约。迁移该生产行为须明确获准，旧门禁不被改写 |
| `Program/src/Core/ActionFeedback.cs:87-145` 冻结E0/F0及t0，区分伤亡/溃逃/Unknown，继任继承累计损失 | 可复用身份和损失账本；不要把专项U公式或局部损失直接当D-EVAL全局价值、也不把重编/Deleted未知计成击杀 |
| `Program/src/Host/ObservationAdapter.cs:262-321` 文件写失败会drop，Emit队列满会drop；已有独立线程 | 保留旁路接口和丢失计数。新增进程内反馈直接走现有主线程数据，不依赖该线程、JSONL或CBATH，不再另开线程 |
| `Program/src/Host/FormationSampler.cs:13-57,64-103,160-195` 采样逐Agent聚合；Mission与对象用运行期引用hash，槽位单独存 | 复用已有真实成员聚合，补局部生命周期代数；hash可诊断，不当无碰撞永久身份。聚合按成员事件/低频更新，不让每个候选重新逐Agent扫描 |

台账 `research/requirements-clarification-2026-10-21/CombatBrain-2.0-Confirmed-Design-Ledger-2026-10-22.md` **§S–X（215–282行）** 的时序必须保留：S明确CB独占战术决策但未批准全域Harmony Full；T有界局部自主；U职责包络与Task≠Duty；V有条件紧急例外；W进一步限定明示牺牲职责；X允许少量独立健康维度与重大事件，**未批准具体schema/阈值/ACK/TTL**。本附录的字段与实现是RECOMMENDED，不把它们倒写成CONFIRMED。

## 2. 权威和数据流

```text
引擎合法当前事实（主线程）
  └─ FormationSampler / 权限适配器 ─→ 不可变 DecisionSnapshot
         │                                  │
         │                                  ├─ 能力/关系/候选/组合/比较（纯Core、可中断）
         │                                  │        └─ Candidate / NoChange / Unresolved
         │                                  └─ 当前Plan与Duty快照 ───────────┐
         ├─ 局部执行事实 → 进程内 HealthSnapshot/重大变化位                  │
         └─ 每tick身份/撤权/局部安全检查 ─→ 合法局部动作                     │
                                                   │                       ▼
                                        Commander 唯一决策入口 → PlanLedger.TryCommit
                                                   │                │
                                                   │      当前Plan/Task/Duty/授权包络
                                                   │                ▼
                                                   └────→ Host ExecuteIfCurrent
                                                            │
                                                请求/读回/保持/效果各自记录
                                                            │
                                              ObservationAdapter.Emit（旁路）
```

- **Commander** 独占 Operational Intent、Task正式开闭、职责迁移/解除、接受职责空缺风险、全局价值比较。Planner纯函数和Formation报告没有这些权限。
- **PlanLedger** 是 Commander 的唯一修改账本，按合法指挥域各有一个实例或一份域内状态；不是新增的全局仲裁器。其拥有当前Plan、Duty记录、待提交迁移和失败记忆；不给外部可写 `Current` 引用。Host兼容代码最终也调用同一提交口。
- **Formation executor** 只读取当前契约并产生执行事实和局部动作建议；不能创建/取消Task、重新分配其他编队或改全军Intent。执行器内部允许“接近/展开/保持”等阶段，但阶段不是任务权威状态。
- **权限适配器** 是真实引擎授权来源的翻译器；权威依据是合法域与有效委托/直接命令，不是TeamIndex或`IsAIControlled`独值。Commander不能自己给适配器扩权。

## 3. 最小身份、事实和提交合同

以下类型是C#候选布局；具体文件名可变，不要求新框架：

```csharp
// 字段为概念类型；数值计数器只在本Mission实例内单调递增。
struct ExecutionScope {
    BattleId Battle;             // 新Mission新epoch，不用SceneName复用
    CommandDomainId LegalDomain; // Party/Army/角色映射形成的合法指挥域，非命令Movement域
    FormationId Formation;      // 本Mission对象标识；槽位只是diagnostic address
    long FormationGeneration;   // 槽位复用/对象替换/语义重建后更换
    long AuthorityGeneration;   // 有效授权撤销、重授、玩家接管均换代
}
struct ContractVersion {
    PlanId Plan;
    long PlanRevision;
    TaskId Task;
    long TaskRevision;
    long MembershipRevision;    // 成员组成更新使能力/预测失效，不自动取消Duty
}
struct Evidence<T> {
    T Value;
    EvidenceState State;        // Known / Unknown / Invalid
    EvidenceOrigin Origin;     // EngineRead / Derived / CalibratedModel / Prediction
    double ObservedAt;          // 引擎秒；不是收到时间冒充新鲜度
    long SnapshotRevision;
}
```

**BattleId与旁路battle_id分开**：运行时BattleId由Mission生命周期生成；没有CBATH身份文件也必须正常工作。ObservationAdapter可记录映射，但不能反过来决定计划身份。合法域标识稳定于当前指挥角色；授权集合是域内动态子集。

**不需要独立Registry服务**：现有Mission宿主上下文里保存对象引用→局部单调序号/代数即可，Mission结束清理。写入时仍以当前对象引用和宿主再读验证，不依赖hash不会碰撞。成员变动先更新MembershipRevision；真正Formation身份替换或不兼容重编才增加FormationGeneration。这样不会因一名伤员退出就误判整个对象“新生”，但旧能力预测不能继续用。

| 合同 | 生产者→消费者 | 必要内容与异常语义 |
|---|---|---|
| `DecisionSnapshot` | Host→Core | Battle/合法域、观测时刻/版本、当下实际存在的敌我Formation、能力区间、关系边、授权代数；Unknown显式，未入场援军不可填成可分配资源 |
| `CandidatePlan` | Planner→Commander | 输入版本、资源/依赖图、阶段条件、ETA区间、反应情景、净价值区间、当前计划对照；不含已发令事实、不享写权限 |
| `TaskContract` | Ledger→Formation/Host | Scope+Version、目标、允许动作、所承DutyIds、依赖效果谓词、成功/退出语义、局部安全包络、action capabilities；角色/单位需匹配，未证API不标支持 |
| `DutyRecord` | Ledger→Planner/Formation | DutyId、目的/对象/责任范围、当前责任人、必要效果与可观测证据、是否明示牺牲、可交接条件；Task关闭不删Duty，Unknown效果不等于失效 |
| `Prediction` | 估计器→Planner | 输入/成员/模型版本、时间范围、情景而非伪概率、效果区间与失配标记；跨版本不能缓存复用 |
| `TaskHealth` | Formation取证→Commander | 完整Scope+Version、单调Seq、ObservedAt、少量独立轴和证据引用；只报告，不开闭Task。可表达“推进受阻、牵制有效、高损失、授权仍合法” |
| `ExecutionReceipt` | Host→本地反馈 | Requested/Accepted/Readback/Held分别记，原生命令实际值、实际写时刻；接受不能冒充到位、保持或职责效果 |
| `TransitionResult` | Ledger→Host/Commander | Applied/NoChange/StaleInput/Rejected与原因；提交失败不发任何新任务/动作，保留仍合法的当前安排 |

**健康维度建议最小化**：先用 `execution_feasibility`、`task_progress`、`duty_effect`、`local_danger` 四轴，授权状态与所选安全动作作为独立事实/动作记录。仅在某任务需要时增加维度；全部带Known/Unknown、观察来源与时刻。`BLOCKED`只表示执行可行性，不隐含TaskFailed；`duty_effect=false`不是本地DutyCancelled。

**去重与新鲜度**：报告键使用完整 `(Battle, LegalDomain, FormationId, FormationGeneration, AuthorityGeneration, PlanId, PlanRevision, TaskId, TaskRevision)`，在同键内比较Seq；新键可从1开始。Mission/域不符、旧代数/旧版本、未来非法时刻直接拒收为当前决策输入；旧数据可留诊断但不能触发新计划失效。新鲜度按用途分别配置并校准：写入的身份/授权每次现读，短窗口依赖按其容忍延迟复查，低频能力画像可更旧；不拿一个全局TTL掩盖语义差异。

**进程内传递**：在现有主线程更新每Formation最新Health记录，重大变化用合并的原因位和受影响任务集合保持到Commander消费；新一轮评估前取当前快照复核。重复事件不会重建后继。无网络队列，无持久化ACK；本地消费序号只是去重水位。事件再多也先合并，Commander每个评价周期至多提交一次整合变化；紧急本地安全不排在候选搜索之后。

## 4. Plan/Task/Duty切换：先准备，再提交，不制造责任真空

建议把以下纯Core逻辑加入 `PlanLedger`，唯一调用者为Commander：

```text
TryCommit(candidate, expectedCurrentRevision, snapshot):
  若当前revision != expected：返回StaleInput
  复查候选的授权/对象/能力/动作保障；过期则返回Rejected
  校验资源时间窗不重复、依赖图无环、关键职责覆盖
  对每个被替换Task明确：Duty保持/已确认接任/Commander显式接受空缺
  若接任未生效且不能维持旧职责：不公布“交接完成”
  先构造完整新账本与当前代数的安全临时契约
  一个主线程提交点：递增PlanRevision，关闭受影响旧Task，发布新契约
  标记旧pending命令不可执行；返回执行差异（不直接SetOrder）
```

这不是引擎事务。它只是单线程纯Core状态切换的原子可见性；Host之后逐动作执行可能被拒，需要如实反馈。`TaskCancelled`后旧task命令永不恢复；临时安全动作来自新提交版本的独立契约，不能借“继续履职”重放旧任务。

**局部修补保留无关任务**：新PlanRevision把仍有效任务显式携带到当前版本，保留其实际写入证据/成本与稳定目标；仅受影响任务改TaskRevision。版本更新本身不重复发原生命令。旧版本报告不能直接改变新计划；未变化任务的证据只能通过显式身份/内容一致的carry-forward引用保留。若实现者认为这个映射过重，可首版保守重采反馈，但不可强制全军重新SetOrder。

**交接分两步，责任一次归属**：新保护者可先接近/准备；独立观察达到职责效果后，Commander同次提交把DutyOwner改给它并关闭旧保护Task。准备阶段旧Owner继续负责，资源表同时体现新者的准备成本，不能把新者又分给进攻。接替者失权/失效则取消接替提议，Duty仍归原Owner；原Owner也无法履职时记录职责空缺并请求Commander裁决，不能在报表中维持虚假“覆盖”。显式接受空缺含原因/风险事实/替代措施，不能靠超时自动生成。

**任务依赖绑定效果而非名字**：`DependsOn(FixDuty, effect_observed, freshness, valid_until)`，不能仅凭牵制Task“已创建/命令接受”放行侧击。依赖失效先停止尚未开始的依赖动作；已开始的动作评估合法局部修补与退出成本，不能机械Stop让贴身部队更危险。Commander随后比较继续/修补/改配；局部失败不直接取消全军计划。

## 5. 本地安全、牺牲职责与真正写令

每个现有宿主tick先处理身份/授权撤销，再做常数或局部关系边级的安全检查，最后才跑可削预算的Commander候选搜索。安全动作仍须满足引擎现实可执行性，不把“后退”当默认安全动作。

| 条件 | 当前可做 | 当前不可做 |
|---|---|---|
| 普通受阻/危险，包络内有可行动作 | 保持职责范围内重整、朝向/阵型调整、有限移动/防御并立即报告 | 自动取消Task/Duty、全军撤退、交还Native后立即反复抢回 |
| 严重可信迫近危险，普通包络无法缓解，非明示牺牲职责 | 比较保持防御与最小现实可行越界；选择足以降低危险且破坏职责最少的动作并报告 | 未经授权写令；假定有退路；改变他队职责 |
| 明示牺牲性关键职责仍有效或效果未知 | 原则上维持职责，能做包络内安全动作；立即报告风险与未知 | 用高伤亡/人数少/预测全灭单独解除职责或越界 |
| 明示牺牲职责已实质失去效果，有足够当前证据，且符合严重紧急条件 | 最小必要局部越界并报告；Commander后续正式处理Duty | 把职责失效等同全军失败，或自动改玩家撤退决定 |
| F6撤回/玩家直接命令/CB关闭/对象身份不明 | 立即让本代CB pending与安全契约失效，停止CB写入；按已证交还合同处理 | “安全优先”扩权、补发旧Stop、恢复旧授权、用IsAIControlled自行推断重授 |
| 无现实可行动作 | 返回Blocked+证据/未知，请求Commander；仅保持仍可合法执行的动作 | 伪造可达撤路或保证安全 |

**严重性与失能判定仍待引擎数据**：候选规则要求证据链，例如责任目标已离开可护卫区域且无法再施加必要效果、实际可用成员/位置/接战状态表明当前无法形成所需作用；须按具体Duty定义。单个人数阈值、单个死亡预测或“推进失败”不构成失能。Unknown不会被默认为false来放行越界；也不意味着永远冻结，仍可做不破坏职责的局部动作并优先触发Commander检查。

**协同包络**：Commander在发布时把需要保持的前沿/保护对象/通路、最大允许职责影响及依赖责任写进包络，而非只给每队“向后10米”。多个Formation同时避险时，局部执行检查的是同一个已批准协同约束；任何越界可能破坏其他任务的事实即时置重大事件。普通包络不得无期限续期；过期后只能使用当前代数的明确最低契约，不自动扩大动作集。

写入合同 `ExecuteIfCurrent(request)`：先核Battle/域、对象引用+代数、授权代数及当下直接命令；再核Plan/Task版本、Duty/依赖新鲜事实、动作capability与几何/资源可行性；然后发一次合法原生命令并读回。返回 `RejectedScope / StaleContract / DependencyUnproven / UnsupportedAction / EngineRejected / AcceptedAwaitingEffect`。不得让副作用发生后才补授权检查，也不能把“返回未抛异常”改写为任务成功。

## 6. 原生接入与安全重编

**原生接入**：按台账§S使用原生Tactic/Behavior接口承载CB已裁决目标与必要底层执行，授权内不保留Native高层长期改目标的并行writer。每种动作建立 capability 表：所需命令域、可观察效果、保持/释放条件、验证环境与未知项。候选生成/分配前先排掉无执行保障的动作；不能先选非法或不能保持的高分方案，临写才用静默降级掩盖。若需要逐域Reject，先取得该域完整需求下证据，TCL Movement局部实验不能替 Facing/Arrangement签收。

**handback分离**：普通战术受阻→CB内部局部修补；授权撤销→本代立即停写；CB故障→关闭本代执行来源，按已证顺序解除行为/权重/门控，让Native恢复可观察写入。缓存旧Native目标可能已不可达或对象已消失，不能无条件重发。恢复失败应保持CB不会继续冲突写并标可见故障，不宣称完整恢复。再次接管需新授权代数与通过恢复/能力条件，避免每tick抢回。

**动态重编为可观测步骤流程，不假设原子回滚**：

1. `Prepare`：读取当前合法成员/对象映射、可用目标槽位和宿主转编能力；确认未违反玩家手动/F6域、关键Duty有保持者或接任；把拟移动成员资源标为不可重复承诺。身份/成员未知就停止准备。
2. `ApplyStep`：主线程每次执行最小合法成员/编队变更；每步之前复查授权，之后回读实际结果，记录完成成员集合。未知结果标待核对，不重复盲调。步数/每tick预算有界。
3. `ObserveIntermediate`：旧新Formation并列可见，明确`reorganizing`只是宿主观察事实；更新成员版、能力区间与预测失效。未实际完成的兵不能被两个角色同时占有；增援只有实际入场进入观察后才可分配。
4. `CommitMapping`：只有回读身份/成员与Duty迁移满足条件，Commander才公布新任务分配；任务绑实际形成的对象，不绑预期槽位。若Formation语义身份改变，增代并淘汰旧报告/命令。
5. `Interrupted/Failed`：保留已合法完成的编组，取消尚未执行步骤，重读现场，再由Commander修补职责；不回放逆序来假装回滚。玩家中途接管即停后续写入，已合法编组不自动撤销。只有仍授权、确有可行逆操作且另行决策时，才做新的重编计划。

安全重编是首版硬范围，实验失败只能列明确阻塞及下一探针，不能以“降级”文字偷偷删范围。首次需在受控Mission验证转移/目标槽位/中间状态，再在Campaign多领主、部分F6、实际援军与结算链验证；前者不替后者。

## 7. P3迁移步骤与首批工作包

**当前批准边界**：只读研究可提出上述候选并在独立Core样机验证。改变生产P3的判据、退出Stop、后继时序、授权或权重需要明确列合同差异，记 **NEEDS_OWNER_DECISION（生产迁移范围批准）**；不是重新询问已签收原则。本轮不修改这些文件。建议先兼容适配，再受控替换，默认生产仍走既有P3路径。

| 包 | 建议文件/符号与步骤 | 必须保留/验收证据 | 阻塞与失败回退 |
|---|---|---|---|
| I1 单一权威提交口（纯Core先行） | `Core/PlanLedger.cs` 新候选 `TryCommitTransition`、`PlanTransition`；先建外部独立研究实现，再生成最小生产候选diff。将Create/Adopt/Invalidate语义纳入一个受控入口，旧P3通过兼容调用保留 | 同一旧revision并发提议仅一次提交；重复事件不会重复后继；旧Plan事件不能取消新Plan；无事件可写Current。先以实际Core调用路径验证，原型结果不冒充生产测试 | 生产接线未获准时只交候选diff与差异表；不让新Planner与Host旧后继并行 |
| I2 Duty与安全契约 | `BattlePlan.cs`新增候选`DutyRecord/TaskContract`，`PlanLedger`拥有Duty；修补受影响Task，关闭旧令同次建立新代数临时契约 | 取消Task仍有Duty；接任者只准备未生效不能释放旧Owner；明示牺牲高损但有效不越界；Unknown不伪装失能；无可行退路返回Blocked | 效果观察缺失时只保留职责内动作；涉及旧P3失效/Stop行为的替换列批准差异 |
| I3 反馈适配而非第二状态机 | `TaskFeedback.cs`结果映射`ExecutionEvidence`；`TestDriver.ActionFeedback.cs`输出事实/重评请求；新增小型`TaskHealth`类型；`ObservationAdapter`只镜像这些事件 | 命令Accepted、Readback、Held、DutyEffect、Outcome五层可独立取值；关闭/丢弃JSONL不影响控制；旧版本/重复/跨Mission报告拒收；多Formation事件合并一次重评 | P3冻结E0/F0/t0及既有判据不改；新全局评价与P3分开模式，只在获准迁移后接生产 |
| I4 写前授权与执行capability | Host现有资格/执行入口先只读E0探针；产出真实合法域、F6、角色映射，再实现`ExecuteIfCurrent` | 手动/F6混合、撤权/重授、换槽、迟到Stop、两端ON/OFF；接受/保持/交还单独证据；对象未知拒写 | 无可靠适配则该上下文标首版阻塞；不能用Team或AI布尔替代 |
| I5 最小重编接入 | Host候选`ReorganizationStep`状态与现有Sampler聚合；Ledger只发布真实映射的Task/Duty变更 | 每步完成成员集可核；中途玩家接管零后续写；失败后总成员账相符且无双重资源承诺；职责空缺显式 | 不假回滚；保留现场编组、停止在途步骤并报告，等待授权内新决策 |

各包都同时交“生产现状证据、候选变化、独立Core测试范围、待引擎证据”。不用新SDK/事件平台或遍历所有历史派单。实机统一复用CBATH已有入口，在当前授权的专用环境核查实际占用；本机无游戏，以上全部为待派发设计。

## 8. 本次Python原型实现边界

此段按本轮 `prototype/planner.py` 的符号审查；文件仍可能由主研究Agent修订，最终运行记录以其交付结果为准。

| 原型符号 | 实际做到 | 不能据此宣称 |
|---|---|---|
| `options / assessment / choose` | 从构造编队/能力、ETA、支援关系生成有限动作，检查资源/依赖/职责并比较当前安排 | 引擎ETA、射界、战斗物理或真实职责效果校准；完整多阶段执行器 |
| `Snapshot / Formation` | mission/domain/epoch/generation/authority等显式输入；path阻断、授权、可执行、弹药、视线由fixture提供 | 已实现真实Campaign域/F6映射；成员变动或引擎路径探针；完整高频观测 |
| `Duty.ready_successors / effective` | 约束分配时可保留当前Duty或允许已观察接任者 | 自动检测交接效果；独立Duty生命周期账本；TaskCancelled后实际部队继续履职 |
| `Runtime.adopt / cancel_tasks` | 内存动作版本与Duty保持的最小模型 | 已迁移C# PlanLedger、正式Task关闭、Host旧执行器撤权、原生安全交还 |
| `Runtime.report / write` | 进程内版本/授权/依赖检查，写返回`accepted_interface_only` | 真实引擎接受/保持/效果；跨进程消息可靠性。完整作用域与代数切换序号重置须按上文合同验证 |
| `Runtime.safety` | 用imminent/ordinary_feasible/escape_feasible/duty_effective输入演示普通/紧急/牺牲分支 | 已实现危险、失能或可达性检测；这些输入是fixture事实/假设，不是算法自己证明 |

因此原型可支持模型范围内决策/不变量结果；本附录的单一C#权威迁移、Duty效果观察、Campaign授权、真实执行及重编均是**规格/NEEDS_ENGINE_EVIDENCE**，不能计入“已经实现”。
