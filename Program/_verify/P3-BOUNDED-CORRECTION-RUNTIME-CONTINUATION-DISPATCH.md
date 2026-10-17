<!-- PROMPT META
Name: CombatBrain 2.0 P3 Bounded Correction Runtime Continuation Dispatch
Version: 1.1
Author Role: CombatBrain 2.0 Development Dispatcher and Reviewer
Model: GPT-6
Created: 2026-10-18
Updated: 2026-10-18
Target: CombatBrain 2.0 Development Team
Scope: CombatBrain 2.0 / P3 startup recovery and bounded correction runtime evidence
-->

<!-- CHANGE LOG (newest first, keep last 3)
v1.1 2026-10-18 02:14 | role=Project Governance Maintainer | model=GPT-6 | editor=codex-cb2-resource-policy-20261018 | note=按用户要求取消正式新窗口审批，空闲自动持锁实测；已验证修复先按路径本地提交
v1.0 2026-10-18 01:43 | role=CombatBrain 2.0 Development Dispatcher and Reviewer | model=GPT-6 | editor=codex-cb2-p3-runtime-dispatch-20261018 | note=从已实现的有限纠偏续接，纠正旧汇报判据，定位战斗前启动异常并取得一次有效开发场
-->

# P3 有限纠偏与运行闭环接续派单

登记：`CB2-PROMPT-0007`。执行方为开发组。用户下发本单后，在下列范围内连续推进，不再申请已批准的方案A，不从旧基线重写纠偏。当前会话只编写派单和登记，本次没有修改产品、部署或运行游戏。

**目标行为：** AI围绕同一作战意图组织实际行动，观察真实效果，再继续、扩大优势、止损或重规划。恢复启动并取得一次能判断现有有限纠偏是否改善该链路的开发实测；精确站位、检查器和测试是手段，不能把原生歼灭当作CB2指挥成功。P3未通过，P4未授权。

`0004`保留原P3验收合同与三门，`0006 v1.2`保留已批准的有限纠偏和实现边界；本单是当前接续工作，不回退这些资产或重开已闭合整改。入口`AGENTS.md v2.2`的§3d/§4a/§4b持续适用。

## 1. 以当前现场接续

| 项目 | 编写时现场与含义 |
|---|---|
| CB2 | `E:\MBmod\CombatBrain 2.0`，`master`，HEAD `a758799fb124944ceda288dd3d1e132f6d161924` |
| 已有实现 | `17e3af8`统一候选重选的质心口径并落实防偏移纪律；`0f8fac9`实现每职责一次纠偏、原截止和同状态失败抑制；`a758799`补齐原生位置就绪但纠偏未写出仍须到期的边界 |
| 交回证据 | `7fd0d95`记录首次纠偏实测尝试的启动失败和恢复；当前`P3-IMPLEMENTATION-HANDOVER.md`开头为最新摘要 |
| 本组CBATH | `E:\MBmod\CBATH-P2`，`codex/cb20-p2-evidence`，`ac7c4aeeee1cebf12800e67197dd08eeaac2afb9`，tracked工作树干净 |
| CB2已有dirty | tracked仅`Program/src/Host/BuildIdentity.g.cs`，自动生成身份，保留不提交；既有scratch、只读取证脚本、运行日志、GABS配置也保留 |
| 当前验证 | 最近限定全量329/329，net472 Debug/Release各0警告0错误；这是已记录的离线证据，接手未重跑时不得称本轮运行 |

先读registry→AGENTS→本单→0006的“已批准有限纠偏”→现有交回材料开头及相关源码；产品判断按需读Charter §7、§11–16、§23、§26–27。0004按涉及的评价/退出/三门条款核对，不机械逐份重读全部历史。

旧开发汇报的`HEAD=8dba289`已过期，只读取证结果可复用，不能代替当前实现状态。接手HEAD继续前进时先读差异、保留进展；派单/登记提交造成HEAD变化不构成停工理由。不得reset/clean或收走他方成果。

## 2. 已批准且已实现的方案，直接核对接线

- `CoordinationDriver.TryCreateRoleCorrection`：仅在等待依赖阶段，命令与对象/版本有效、原锚点已到请求点、实际质心未满足职责关系时，**同一Plan每职责最多一次**。Guard与Support分别计数，不是全Plan共享一次。健康职责不重发；任务使用新身份和新写令额度。
- 真正写令前仍按同批有效A/C/T计算请求，经过原资格、裁决、主线程、首令延迟和编队间隔门。不得用纠偏指定时的缓存代替写前读数，不绕开原生真实获选。
- 继承原职责20s绝对截止，指定、等待获选及写出均不重置；不足以容纳原延迟与3s反馈窗口就不指定。即使原生把成员移到位，未写出或命令已失效的纠偏仍不能借此越过截止。相关代码见`FormationTask.RelationshipDeadlineAt`、`TaskDelivery.TimingDeferral`和`CoordinationDriver.RelationshipExpired`。
- `PlanLedger.RememberFailedCoordination/CreateCoordination`消费NotEffective失败；同状态不因时间/位置抖动重建，相关对象、成员、人数、资格或目标条件改变后可重评，Mission切换清空。不得让“抑制重复失败”变成永久停机，也不得为了继续出计划放开抑制。
- 不另建Core判定框架，不为`correction_budget_exhausted`等单个日志名重构。只有当前代码的真实接线缺陷或证据缺口才补最小改动。

## 3. 纠正旧定位汇报，不改验收尺度

1. 令`O_write=A−C`、`O_arrival=A′−C′`、`R=T+O_write`、`e=A′−R`，完整向量关系是`C′−T=O_write−O_arrival+e`。只有锚点精确到达R时才能省略e。模长残差可用于核对几何量，不能独立证明引擎重设锚点是唯一物理根因。
2. 职责到位采用**5m及完整前向/侧向关系，连续至少1s有效读数**；`PlanMinStableSeconds=8s`不是职责到位稳定窗口。0.5m用于原有到点/读回等判据，不新增为“质心必须精确到0.5m”的验收要求。2m误差本身不能作为必须纠偏的红例。
3. 第四场`p3-P-20261018-001730`的plan-8在285.101秒，Guard锚点距请求约0.078m、质心距G*约15.949m；Support质心距S*约9.456m。写前偏移在后续变化已被证据支持；“第二次写令会保持偏移不变”仍未证明，不以代数Green替代引擎行为。
4. 保留`Program/_verify/_scratch/`中的只读脚本，但先核统计边界。`locate-offset.ps1`当前从写后所有样本找首次距请求≤1m，未限制到任务关闭或同对象下一次命令，可能跨任务取到“到达”。复用时将样本限定于同Mission、同对象、实际命令目标一致且本任务仍有效的区间，关闭/被接管/下一次命令取最早结束；1m只是该脚本选样尺度，不是生产容差。无法界定的样本标缺证据，不补推。
5. `POST_OBSERVE Effective`是位移/命令反馈，readiness是连续职责关系，不能因reads=0就认定Observe未调用。`ACTION_TERMINAL`也不是全场终态总数；Killed和后续Deleted不得重复计为两次伤亡。

## 4. 首先处理真实阻塞：战斗前启动异常

失败场次：`Program/_verify/sessions/p3-P-20261018-012518`；完整控制台`Program/_verify/_lastrun-p3-bounded-correction-20261018.txt`。

- 该场受试实现为`0f8fac9`，不含后续`a758799`。CB2加载身份SHA256与source/deployed一致为`68EADA314F370AEE374322B6B1CE7975985DF16F9B8980A1E160094080F738BA`；仅有RUNTIME_IDENTITY，无Mission/Plan/纠偏样本。不能归为产品行为失败或通过。
- 崩溃文件`crash-2026-10-17_17.26.01/rgl_log_errors_49084.txt`：`InvalidOperationException: Connection is not active`，栈在GABP `TcpConnection.SendMessageAsync → GabpServer.SendErrorResponseAsync → OnMessageReceived`。这定位了异常组件，没有证明是谁、为何先断开。
- 先离线对齐启动、连接/ping、首次CBATH调用、超时和崩溃时间；核查实际桥/模块版本、调用顺序与运行状态。可验证“桥连上是否等于工具已可安全执行”等假设，不预设必须加固定sleep或升级第三方。
- 允许精准修本组现有启动/采集脚本，或本组CBATH必要的就绪/异常处理接线。复用当前GABS/CBATH入口，不自建启动器、UI模拟、通用重试框架或吞掉错误。若确需修改第三方桥实现/版本或扩大命令域，提交明确位置、触发证据和最小方案再裁定；无法本组修复时给出具体外部阻塞，完成其余离线工作，不盲目重启刷场。
- 该场原`end-state.json`的safely_exited=0失败必须保留；后续恢复见`post-crash-recovery.json`。不得把修复配置后的读回倒写成原运行正常收尾。

## 5. 验证与自动实测准入

相关离线验证优先调用现有生产入口：偏移未变且职责已满足不纠偏；锚点到而真实质心仍越界才考虑纠偏；健康职责不重发；新身份/次数预算；未获选或未写出仍受原截止；纠偏后正常Observe→主力推进路径；失败状态不因抖动重建、相关事实变化后可重评。已覆盖且未受改动影响的用例可继承，不重造数学模型或无关边界。脚本改动跑受影响PS5.1语法/定向反例与WhatIf；不机械重跑未变的C#全量。

按2026-10-18用户最新指令，取消每轮人工交回/书面窗口批准。上次01:19/01:29记录仅供追溯，不再需要判定旧窗口是否还能使用，也不需观测方新增交回条目。相关离线验证通过后，已授权任务在现场无活跃游戏且协调入口实际取得锁时直接执行一次有界开发场，不再请示资源许可。实际有运行或互斥锁占用仍须停止，不凭旧锁元数据判断。

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "E:\MBmod\CombatBrain 2.0\Program\_verify\run-p3-exclusive.ps1" -Scenario P -CombatHoldSec 420
```

整个部署CB2→部署本组CBATH→启动/采集→结束清理由既有`Invoke-ExclusiveGame.ps1`持锁，Owner=`主线组`。保持死战不退，核对真实get_fight_to_death的status/patches_installed/enabled以及样本归档。不得锁外改配置、部署，或强杀未知归属进程。实际被占用时仅做离线工作，不抢占；不再向其他会话索要例行窗口，不自动建新会话。

该次启动与验证合为一次尝试，不先开一场探启动再自动开第二场。若再次启动失败，保全证据并清理，释放锁；不自动刷场。若需要特殊部署或改变开发场景，说明它要区分的原因和原始实测配置，不把其结果冒充正式P/C对照。不得为触发lostRole日志随机改变人数或延时。

### 本次实机必须回答

- 实际加载了哪份构建，启动问题是否消失；死战是否真实开启，结束是否实读干净。
- 有纠偏时：原/新任务、对象、写前A/C/T、实际请求/读回、原截止及真实写入时刻是什么；一次纠偏是否改善真实职责关系，随后是否有主力实际行动、冻结评价与后续决定。
- 无纠偏时：是条件已满足、不满足触发条件、预算不足，还是执行失败。一次未触发不当通过，也不为补日志再刷场。
- 评价/伤亡/原生动作必须按有效计划与实际任务窗口关联。没有因果链只报告未验证，不把原生战果当本方收益。

行为稳定后才进入原合同正式P/N/C；本单一场开发实测不替代阶段验收，不新增通过标准，也不承诺一次纠偏必然恢复全链。

## 6. 修改边界与交回

CB2仅改与本单直接相关的既有Core任务/协调/计划/投递、Host接线、现有测试、P3脚本和当前交回材料。CBATH仅在`E:\MBmod\CBATH-P2`本组范围做有证据必要的最小接入修改；不得改动/构建/提交观测组`E:\MBmod\BattleObservation-V1\CB2`、`CBATH`、`TCL`或其integration候选，不改观测接口、不复制旧全文件覆盖当前实现、不重写历史失败样本。

不改变5m/1s/20s、1.5s延迟/间隔、3s反馈窗口、评价公式、退出1s安全预算、唯一裁决/主线程、原生获选和已开放兵种×命令类别。不得改用锚点冒充质心到位、逐tick追写、延长计划寿命或扩大已批准纠偏次数。提前触发、另加偏移稳定门或把次数改为全Plan一次均是方案差异，要带证据明确提出，不能偷偷当成“方案A”重做。

相关离线验证通过即本地逐路径提交本组已授权改动，实机证据随后补记；无需再询问先提交还是后提交。不push、不建PR；自动BuildIdentity和既存scratch/日志/私有启动配置不混入。仅更新现有`P3-IMPLEMENTATION-HANDOVER.md`与实际新增场次的`sessions/INDEX.md`，不要求报告自身哈希另造提交，不建长报告或新审批模板。

交回简短写清：纠正了什么偏移；新增了什么真实行为证据；源码/构建/部署/运行身份及清理；还缺什么。区分离线已验证、实机成立、未触发和外部阻塞。未跑的场次与未证明的分支不得登记通过；P3阶段结论仍独立落定。

本轮新增概念清单：无；复用已有任务、职责、有限纠偏、计划与评价术语。
