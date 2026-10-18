<!-- PROMPT META
Name: CombatBrain 2.0 P3 Action Loop Recovery Dispatch
Version: 1.0
Author Role: CombatBrain 2.0 Development Dispatcher and Reviewer
Model: GPT-6
Created: 2026-10-18
Updated: 2026-10-18
Target: CombatBrain 2.0 Development Team
Scope: CombatBrain 2.0 / P3 action execution, bounded correction and failure recovery
-->

<!-- CHANGE LOG (newest first, keep last 3)
v1.0 2026-10-18 18:11 | role=CombatBrain 2.0 Development Dispatcher and Reviewer | model=GPT-6 | editor=codex-cb2-p3-action-loop-dispatch-20261018 | note=接续真实Guard早期失败与整场失败抑制，合并有效约束并登记旧执行单归档，要求连续推进至可判定的行为证据
-->

# P3 实际行动、有限纠偏与失败恢复接续派单

登记：`CB2-PROMPT-0008`。执行方：开发组。本单承接用户已批准的纠偏与闭环恢复工作；开发组接到本单后直接开工，无需再次询问是否实施。编写本单不代表已执行开发或通过验收。

## 1. 目标、权威与接手现场

改善的可观察行为：AI围绕统一意图组织三职责，实际执行行动，观察效果，进而继续、扩大优势、止损或重规划；不能只生成计划、停在精确站位或检查器通过，也不能把原生歼灭当作CB2指挥收益。上位依据为`charter/Charter V0.2.md` §11–16、§18–21、§27，按相关问题定点读，不重新写设计报告。

- `AGENTS.md` v2.3 / `CB2-PROMPT-0001`为现行工作流。先查`prompts/registry.json`生命周期，再读派单。
- 本单是当前开发接续入口。`CB2-PROMPT-0004`保留P3原始验收合同的作用，不是重新实施全部P3的任务；本单整合0005/0006/0007的未闭合工作及后续用户裁定，不要求通读已归档派单才能开工。P2已按`P2-STAGE-EXIT-ACCEPTANCE.md`限定通过，不重开P2。
- 编写现场：CB2 `E:\MBmod\CombatBrain 2.0`，`master`，HEAD `d9af815f3e194b67d6517b853fd1e2c5d39137cb`；唯一tracked dirty为`Program/src/Host/BuildIdentity.g.cs`，保留、不纳入提交。
- 本组CBATH：`E:\MBmod\CBATH-P2`，`codex/cb20-p2-evidence`，HEAD `f5a8916f23ac39fec93bec95c41627a8def73663`，tracked工作树干净。模块源码位于`RD/CombatBrain-Autonomous-Test-Harness/experiments/test-mod/CBATH/`。
- 开工重新核对路径、HEAD、暂存区与全部dirty；既有scratch、日志、离线装置、私有配置不清理、不混入提交。后续HEAD变化先读差异并续接，不停工请用户解释正常提交前进。禁止reset/clean或覆盖他方成果。

P3尚未通过。本单不批准P4，不更改原三门、评价公式、命令域或安全要求。

## 2. 不再重复争论的事实与边界

### 2.1 最近可用开发场

场次：`Program/_verify/sessions/p3-P-20261018-102648/`。完整控制台：`Program/_verify/_lastrun-p3-boundary-fix-r2-20261018.txt`。

- 本场CB2 build `b20261018-102643-9cb03fd9`，source/deployed/运行身份文件摘要均为`192bfaa103dad8a435faaebe1a76779bec45b28a06c5e1754cfd66313273f955`。
- 本场CBATH build `b20261018-102646-934e7ba7`，source/deployed/运行读回均为`2206799212869942325daedbe13d98befb721b1e2a8f91346fd00a36b3c658e5`；PID15500，主线程探针`ticks=6 enqueued=1 completed=1`。`62E48F56…`属于前一场，不能混入本场宣称二次构建。当前采集脚本未见collect内再次构建的调用。
- 启动成功、主线程派发成立；死战前置成立，`end-state.json`清理无失败。一次启动成功不证明此前异常不可复现，也不能单凭无崩溃证明异常边界命中过。CBATH `f5a8916`已提交；无新回归不重开桥研究、离线启动框架或dump分析。
- 全场只有一个计划。Guard是`plan-1-task-2`，不是Main。5.134秒写Move，8.135秒`TaskFeedback`判`NotEffective`：`from=3.74 distance=8.55 closed_distance=-4.81 since_write=3.000`，随后计划关闭，三个退出8.535/8.585/8.635秒完成。
- 此后`unchanged_failed_coordination`出现137次，最后418.601秒。`COORDINATION_ADVANCE`、`ROLE_CORRECTION`、`ACTION_FEEDBACK_FROZEN`、`ACTION_FEEDBACK`均为0。反制等待冻结而未进入目标选择分支；不改成按人数选Main，也不先修不存在的敌我选择失败。
- 第88行是Guard的原生Charge记录，位于第98行CB2写令之前；第93/101/103/105行属于Ranged，不能拿来证明Guard写后被覆盖。第109行Guard读回仍为Move。原生探针在`OnBeforeMovementOrderApplied`入口记录；活动行为归属和线程号不等于具体调用者与最终覆盖事实。

### 2.2 坐标、任务反馈与战果评价

令A为同批读数的当前锚点、C为有效成员质心、T为战术目标、R为写给引擎的锚点请求。当前公式正确：`R = T + (A−C)`；A=100、C=110、T=150得140，反号得160，直接返回A得100。不得改符号、返回A，或同步修改测试预期迎合旧汇报。现有`CoordinationOrderFor`将换算结果直接传给`MovementOrderMove`，未发现第二次加偏移；若有新证据另指出具体生产调用。

固定偏移只是假设，不是到位证明。令`O0=A−C`、到达读数`O1=A′−C′`、`e=A′−R`，则`C′−T=O0−O1+e`。采样必须限定同Mission/对象/任务及目标，遇关闭、接管或下一条命令取最早截止；不能跨任务拼出到达。写令时3.74m与5.254秒后采样2.76m不是同一时刻。

`TaskFeedback`的3s位移生效检查与P3 `ActionFeedbackStep`的战果评价是两层。质心语义Reposition用实际质心到战术目标的距离；`Effective`不等于完整职责关系成立。P3冻结只在Main推进普通命令实际写入时建立；没有伤亡仍可评价Continue。本场未推进所以未冻结，不据此判评价器坏，也不以“没交战”回避行动未建立。

## 3. 连续完成的工作

### 3.1 用现有样本定位Guard为何先失败

沿生产`TestDriver`任务观察/失效、`TryPublishRoleCorrection`、`CoordinationDriver.TryCreateRoleCorrection`、`TaskFeedback.Evaluate`核对实际顺序和条件。把5.134至8.135秒的同对象实际订单/目标、锚点、质心、活动行为与剩余预算对齐，区分转向/惯性、偏移变化与真实覆盖。不同线程或单条入口日志不作根因结论。

必须解释：有限纠偏未触发，是原锚点未到请求、命令/身份门不满足、预算不足、接线遗漏，还是纠偏条件与既定3s任务失败的时序不相容。原始数据足够就不另加标记；确实缺决定性事实才补最小读数。不要重新证明已知的“评价没跑”，不要为了覆盖lostRole日志刷场。

常规接线、同范围缺陷直接修，先写调用生产实际入口的反例，再实现并验证。若证据表明必须提前纠偏、延后TaskFeedback或改变原命令策略，停止该项策略变更，给出具体反例、最小改法及代价请求裁定；不要用改公式或检查器绕过。

### 3.2 查失败抑制的设计适用范围，修共同原因

`PlanLedger.RememberFailedCoordination`目前将NotEffective记录为离散态势键；相同兵力、对象和条件下会持续阻止新建。本场已证明其行为结果是整场无恢复，不代表应直接取消抑制。

区分一次执行失败与同一战术分配持续不可行；检查三职责空间关系的战术必要性、转向后的可实现性，以及哪些可观测事实能证明失败前提已改变。兵力不变不等于执行前提不变，坐标抖动或时间流逝也不自动构成新机会。不得未经裁定新增N秒重试、K次封盘、战果触发阈值、把连续坐标直接拼进key，或周期清空失败记录；这些不是本单指定方案。

已有语义的记忆误接、状态残留、重评条件漏接可在本单内修复。若必须改变“哪些失败可以重试/何时重试”的策略，先用本场证据提出一个最小方案，不列无证据菜单让用户代做定位。不能只改PlanLedger让计划数量增加，却留下首轮执行失败。

### 3.3 保留已闭合的执行与验收接线

不重做已有继任退出确认、三职责独立风险统计、冻结成员完整性、门2三值与逐对象证据、启动进程归属和安全清理。相关变更出现真实回归才重新打开。门2不能把提前接管退出当成功，缺独立证据为unverified；后继归属须匹配实际订单目标与生效区间。进程归属仅用本场GABS启动上下文，不能退回共享自报文件或PID候选数。不得把离线工具替身部署为生产死战/主线程工具。

## 4. 本单不变的约束与修改范围

- 保留5m及完整前向/侧向职责关系、连续1s有效读数、原20s关系绝对截止；8s计划最短稳定时间不是职责稳定窗口。0.5m不是新增质心精确站位要求。
- 保留1.5s首令延迟/编队间隔、3s任务反馈、现有评价公式与三门、退出1s安全预算、唯一裁决、资格、原生真实获选及主线程写令。不增权重、不关AI、不扩大命令域。
- 已批准纠偏为同Plan **Guard、Support各最多一次**，健康职责不重发；原锚点到达请求且实际质心不满足职责关系，命令/对象/Plan/Revision有效，剩余原预算可容纳等待及反馈才可指定。新任务身份/新写令额度，写前重新取同批A/C/T，不逐tick追写、不延寿、不另加未经批准的偏移稳定门。
- CB2仅改当前问题所需Core任务/协调/计划/投递、Host实际调用、相关测试、既有P3脚本与交回材料；CBATH仅在本组路径做有证据必要的最小改动。不动观测组`E:\MBmod\BattleObservation-V1\CB2`、`CBATH`、`TCL`及其integration候选，不改观测接口或拷贝旧整文件覆盖。

## 5. 验证、一个可判定开发场与交回

先做受影响的生产入口反例，重点覆盖已定位的失败与相邻正常路径：命令已落地但质心关系未成立、健康职责不受扰动、纠偏未写也不能逃出原截止、同失败不靠时间/抖动复活、相关前提变化可按已批准规则重评。不要为日志名造框架。继承的329/329仅是旧离线结果，不冒充本轮；C#改动后按累计影响做必要测试及net472构建，脚本改动做PS5.1语法/受影响反例/WhatIf，文档归档不跑无关全量。

离线验证通过即按本组明确路径本地提交，不再询问提交时机；不push、不建PR。随后现场无活跃游戏、实际可取协调锁时，直接经现有完整入口跑一个能区分原因的开发场，不等书面交回或其他会话回复：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "E:\MBmod\CombatBrain 2.0\Program\_verify\run-p3-exclusive.ps1" -Scenario P -CombatHoldSec 420
```

该入口已内部调用`Invoke-ExclusiveGame.ps1`，不得再次外包同一把锁。完整部署→启动→采集→清理均在锁内；锁元数据不是活动占用判据，禁止删锁抢占、forceTakeover或停未知进程。死战双方开启，核对两处前置实际读回并归档样本；每场各自绑定source/deployed/loaded身份，不能混用前场摘要。拿不到本场PID归属则记unresolved并保留进程。

开发场必须回答：Guard/Support实际动作与职责关系是否改善；有限纠偏若未触发的具体原因；Main是否实际推进并冻结；随后是否产生可解释评价与继续/追加/退出/重规划。没有交战不能宣称战果链成立，原生战果不能归功CB2。样本未触发或仍阻断就给具体结果，不原样追加同类420s刷场。需要调整开发场编排时先明确要区分的原因，保持现有能力范围；不把开发场充作正式P/C对照。行为稳定后才进0004正式P/N/C，不降低阶段验收。

**非必要不要回复，持续推进。** 常规定位、实现选择、离线检查、本地提交及已授权实测不逐项请示，不以定位小结或压缩摘要结束工作。仅真实阻塞、重大歧义或确需改变上述数值/命令策略时打断；提出问题前先完成不依赖裁定的工作。最终仅更新现有`P3-IMPLEMENTATION-HANDOVER.md`与有新增场次的`sessions/INDEX.md`，简短交回“修正了什么偏移、新增什么真实行为证据、还缺什么”。不另建审批、模板或长报告；未恢复行为不得宣称闭合，P3阶段结论仍由独立验收落定。
