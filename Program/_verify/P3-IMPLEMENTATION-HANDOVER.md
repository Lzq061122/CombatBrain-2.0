# P3 接续整改 — 实现交接

**当前状态：P3 未通过，实际行动—评价闭环尚未恢复。** 当前开发依据为 `CB2-PROMPT-0008`（`P3-ACTION-LOOP-RECOVERY-DISPATCH.md`）；0004仅保留原始验收合同作用。0002/0003及0005/0006/0007已在 `prompts/registry.json` 归档，保留原路径与原文字节，不按历史派单重新实施。此前各段是历史交接证据，不覆盖现行登记、后续用户裁定及本段当前状态。

- 接续现场：CB2 `95316fd`（本段改动前 `d9af815`），CBATH `f5a8916`；接手仍须核对实际HEAD和dirty，保留自动身份与既有scratch。
- 最新核对场 `p3-P-20261018-102648`（旧）：Guard在8.135秒NotEffective关闭；随后137次同状态失败抑制，推进、纠偏、冻结及战果评价均为0。
- 行动恢复场 `p3-P-20261018-210338`：纠偏、重试、Main推进、冻结与战果评价全部首次出现，同状态失败抑制为0；但接敌期无有效计划窗。
- 裁定①实施后的两个开发场 `p3-P-20261018-224524` / `p3-P-20261018-225324`：前者未触发①（Guard 全程 reads=0），后者**触发①并让 Guard 命令计入生效**；两场均因无 Main 写令被采集器判 `opponent counter-action never applied`，`RESULT: FAILURES (1)`，**不作为任何门的证据**。见下方「2026-10-18 裁定①（职责要求已满足计入生效）」。
- 当前未闭合（三项实质阻断均已定位到机制，需裁定是否改规则，见下节）：(a) **三编队编制下任一职责失效都无法重分配**——`BuildReassignments` 排除主力与被失效键后只剩正在担任另一职责的那一支，为其找替补时可用键已耗尽，必然 `replacement=<none>`；(b) **交战期新计划的 Guard/Support 进不了冻结包络**，plan 以约 9 秒周期重建、每次 3 秒内失败；(c) **原生战术每 tick 清零我方行为权重**（`TacticCharge` 的 `ResetBehaviorWeights()` + 我方 8.0 vs `BehaviorCharge` 10000 f），`plan-8` 因此三个任务全部错失 `REAL_SELECTION`。原记为 (c) 的「`225324` Support 中途停滞」**已用派单 §3.1 残差公式推翻**：锚点已到达请求点（0.139 m），差额来自锚点—质心偏移变化，不再构成阻断。**20 秒预算与 12 m 侧移已复算证明可执行（`210338` plan-2 两侧真实到位并维持 135 秒），本段不请求改动，也未改任何生产数值**；先前「Support 20 秒走不完」的记录是窗口起点错误，已在下节更正。不改正确的 `T+(A-C)` 符号，不以工具/启动成功代替战术闭环或阶段通过。**(d) 已闭合的机制核查：推进期依赖失效没有原地恢复入口**，`210338` plan-2 的 148.092 同时被两个门挡住——阶段门（`CoordinationDriver.cs:222-223` 仅等待期）与已耗尽的时间预算（写令 17.136 + 20 = 37.136，失效时 148.092 已超期约 111 秒）。按用户裁定：**不新增推进期恢复入口**，该限制记为**当前实现限制，不宣称已满足持续指挥目标**；等待期纠偏失败也不能推断推进期必然无效。交战期 plan-3/plan-7 的失败在现行判定下仍然正确。见下方「裁定两项的闭合」。
- 实机不再要求人工交回窗口；现场空闲时由现有完整入口自动取锁，保留实际占用、连接和进程归属保护。非必要不回复，已授权工作连续推进至集中交回。

### 2026-10-18 裁定①（职责要求已满足计入生效）

**用户裁定**：选①——完整职责关系已成立时计入该职责命令生效；保留现有推进释放门，不选②绕开它。A1 的严格 `>0` 保留。三项裁定与两处事实更正已写入 `P3-ACTION-LOOP-RECOVERY-DISPATCH.md` §4a（v1.1）并同步 `prompts/registry.json`。

**改了什么**（`95316fd`，4 files）

1. `CoordinationDriver.RoleRequirementSatisfied`：仅当前协同计划的 Guard/Support Reposition，且必须是本计划当前该侧的任务身份；命令走 `CommandStillApplicable`（已写入/同职责/同 Revision/读回未偏离）仍有效；到位复用 `Observe` 同一份 `GuardInPosition`/`SupportInPosition` 与连续 1 秒读数（`readiness.Ready`）。Main 推进与普通 Reposition 不走这条路径。
2. `TaskFeedback.PromoteToSatisfiedRoleRequirement`：把结论并入生效，`detail` 写 `role_requirement_already_satisfied was=<原判据读数>`，保留“目标本来已满足”与“本命令产生位移”的区别。
3. `TestDriver.FeedbackTick`：在 `Evaluate` 之后、写反馈状态之前调用，未满足时保留原位移判据与 3 秒截止。
4. 未改：20 秒预算、纠偏次数、重试额度、评价公式、三门、Main 释放门。

**验证**：新增 4 例（完整关系+1 秒窗口才成立、横向出区不成立、命令未写/被覆盖/目标不可读不算、只限当前协同职责、口径 detail 保留），红态 4/4 失败先于实现；全量 **339/339**；net472 Release `-t:Rebuild` 0 警告 0 错误。

**开发场一 `p3-P-20261018-224524`**（CB2 `b20261018-224517-0abbaf8a`，`f00e2868…`，202752 B，pid 54372；CBATH `b20261018-224522-59fcd619`，`256792d8…`）：①**未触发**。plan-1/plan-2 的 Guard 全程 `reads=0`（质心 10.66→7.73 m 且曾后退 `closed_distance=-2.58`），是真实漂移而非“已到位”；plan-2 于 17.135 失败后重试额度用尽，余下 `unchanged_failed_coordination=89`。`RESULT: FAILURES (1)`：`opponent counter-action never applied`（无 Main 写令 → 无冻结 → 反制无法武装）。

**开发场二 `p3-P-20261018-225324`**（CB2 `b20261018-225321-2c3dc283`，`1b711f06…`，202752 B，pid 53500；CBATH `b20261018-225323-8b84c467`，`efff6a52…`）：**①首次实机成立**——`plan-1-task-2` 在 5.606 记 `state=effective detail=role_requirement_already_satisfied was=closed_distance=-3.82 (<3.0) distance=4.25 from=0.43`，即质心本来就在职责区内、命令生效不再要求再走 3 m；同一计划 5.686 因质心已出区触发一次 Guard 纠偏（`deadline=25.136`），纠偏命令 3 秒后仍失败（`closed_distance=-6.91 distance=24.93`），plan-1 于 10.637 关闭。plan-2 里 Guard 从 21.534 起 `guard_ready=1 guard_loss=None`（①路径持续让它保持生效），但 **Support 的 Ranged 175 全程 `reads=0`**，39.088 以 `relationship_not_established guard_deadline=37.134 support_deadline=39.084` 到期关闭。`RESULT: FAILURES (1)` 同为反制未武装。

**下一处阻断：Support 在本场到不了 S\*；但「20 秒 / 12 m 不可执行」的先前记录已推翻**

本文件先前据 `p3-P-20261018-225324` 记过「Support 20 秒内走不完约 40 m、需改冻结数值」。该结论的**窗口口径错误**：它从计划创建时刻起算，而生产口径见 `Program/src/Core/CoordinationDriver.cs:434-440`：

```
if (task == null) return -1.0;
if (task.RelationshipDeadlineAt >= 0.0) return task.RelationshipDeadlineAt;
return task.WrittenAt < 0.0 ? -1.0
    : task.WrittenAt + TestParameters.CoordinationRelationshipDeadlineSeconds;
```

即预算从**写令时刻**起算（日志 `support_deadline=39.084` = 写令 19.084 + 20）。本场 Support 首令延迟 5.550 秒，按创建时刻算等于白扣掉 5.5 秒预算。

重算工具（只读、纯 ASCII，避免 PowerShell 5.1 按 ANSI 读脚本失败）：`Program/_verify/_recompute_reachable.ps1` —— 按写令起算窗口，且只取该编队**在本窗口内真实处于 `Move`** 的样本测速；窗口内无 `Move` 者报 UNMEASURABLE，不判「慢」。

**实测结果（`need` = 写令时真实质心到 G\*/S\* 距离，`left` = 窗口末剩余距离，到位半径 5 m）**

`p3-P-20261018-210338`：

| 计划 | Guard | Support | 判读 |
|---|---|---|---|
| plan-2 | 20.1 m @1.02 → left 1.6 m | 36.2 m @1.86 → left 0.3 m | **两侧均到位**；32.548 起 `guard_ready=1 support_ready=1 advance_allowed=1`，维持到 148.092 |
| plan-3 | 8.5 m @0.48 → left 4.0 m | 33.8 m @1.17 → left 12.7 m | Support 到不了 |
| plan-4 | 6.2 m @0.35 → left 3.4 m | 25.1 m @0.92 → left 11.0 m | Support 到不了 |
| plan-5 | 1.7 m @0.21 → left 2.9 m | 16.9 m @0.82 → left 7.0 m | Support 到不了 |
| plan-6 | 1.5 m @0.37 → left 5.8 m | 12.6 m @0.87 → left 6.4 m | Support 到不了 |
| plan-7 | 5.1 m @1.10 → 仅 2.8 s 窗口 | 本窗口内无 Move | UNMEASURABLE |
| plan-1 | 写令时仅差 6.1 m，但 19.3 s 内**反向拉开 24.7 m**（`closed=-24.7`） | — | 非路程不足，是真实漂移 |

`p3-P-20261018-225324`：按**质心**口径 plan-1 Support 需 34.5 m、plan-2 Support 需 39.5 m（18.9 s 走 24.5 m，left 14.9 m）。但同一窗口按**锚点**口径是 `(1145.718,869.299) → (1137.657,830.756)`，移动 39.0 m 且**到达**请求点（0.139 m）。两个口径分岔的原因见下文第 6、7 条（锚点—质心偏移由 2.10 m 变为 15.42 m）。因此这组数字说明的是「质心没到」，不是「编队走不动」。

**换分配能否可行**（`Program/_verify/_recompute_swap.ps1`，**反事实估算，非实测**：对调后的命令从未真正下发）：对每个计划把 Guard/Support 对调后重算，**没有任何一个计划能让两侧同时落在 20 秒内**——对调后总是「Ranged 去 G\* 需 48.7–55.1 m」或「Cavalry 改去 S\* 需 12.0 m @ 0.35–1.05 m/s 仍超时」。原因是几何本身：G\*、S\* 分别落在主力前后两侧各 12 m，Ranged 起手距两者都在 35–55 m，Cavalry 虽近，改去 S\* 后同样超出预算。

**结论（不写成「整套方案不可执行」）**：`210338` 的 plan-2 已实测**两侧在各自 20 秒窗口内真实到位**并维持 135 秒（13.5–148.1），证明「20 秒 + 12 m 侧移」在敌我距离较大（本场写令时约 35 m）时**确实可执行**；到不了只发生在敌方贴到十几米、S\* 相对支援编队反而变远的后期计划（plan-3..6，`need` 12.6–33.8 m、观测 0.82–1.17 m/s）。因此**不满足**用户「若无可行组合则判定当前部署不可执行」的触发条件，**本段不请求改 20 s / 12 m，也未改动任何生产数值**。

真实剩余阻断改为：`plan-2` 在 148.092 记 `COORDINATION_DEPENDENCY_LOST role=Guard loss=NotReady failure=None replacement=<none>` → 关闭，而 `BATTLE_STATE` 首次损失在 148.589。即**有效计划窗在接敌前 0.5 秒结束**；交战期 plan-3..8 各自 3 秒内失败或选择超时。下一步已执行，机制见下节；不继续调 20 s / 12 m。两场均无冻结/推进，故仍不能当正式 P 对照样本。

### 2026-10-18 依赖失效与交战期重建的真实机制（只读定位，未改生产代码）

**1. 148.092 的 `loss=NotReady` 属实，不是读数抖动，也不是 G\*/M\* 变了。** Guard（`1/2` Cavalry 53）质心 147.207 在 (1291.259,822.951)：fwd=13.41、lat=2.81、distG\*=4.42 → `GuardInPosition`=true；148.630 到 (1289.571,821.924)：fwd=15.25（**越过 15 m 上界**）、lat=2.10、distG\*=5.66 → false。1.42 秒内自己前进 1.98 m，越过了冻结的职责关系包络上界。`COORDINATION_STATE` 自 140.594 至 147.602 连续 `guard_ready=1 support_ready=1 advance_allowed=1`，下一 tick 即失效。

**2. `replacement=<none>` 是三编队编制的结构冲突，不是候选排序或阈值问题。** `Program/src/Host/TestDriver.cs:644` 先 `excluded = {lostTask.FormationKey}`；本方只有三支 AI 编队（`1/0` Cavalry 52 主力、`1/1` Ranged 175、`1/2` Cavalry 53 失效方）。`OrderedRoleCandidates`（`TestDriver.cs:588-611`）排除 `IsMainHolder`（`1/0`，见 `:623-632`）与 `excluded`（`1/2`），只剩 `1/1`；而 `1/1` 正是 Support 当前持有者，走 `takesOtherRole` 分支必须再为 Support 找替补（`:653-659`），可用键已全被排除 → `fillers.Count == 0` → `continue` → 返回 `null`。**三编队下任一职责失效都无法重分配。**

**3. 交战期计划以约 9 秒周期重建，每次 3 秒内失败。** `210338` 依次 148.092 / 155.088 / 164.086 / 173.086 / 182.086 / 191.133 / 205.112 失效；plan-3..7 判失败理由全是新 Guard 的 3 秒位移不足（`closed_distance` = 2.82 / 2.20 / −0.03 / −0.18 / 0.13，`distance` = 5.65 / 4.02 / 1.75 / 1.66 / 5.13），plan-8 为 `SelectionTimeout`。裁定① 只在质心**已经**满足完整职责关系时放行；plan-3..6 的质心并不满足（plan-3 自 148.63 起 `distG*=8.44`、`lat=−8.27` 持续越界），因此不会被计入生效。这是①的既有边界，不是回归。

**4. 成员 digest 抖动会重置连续读数。** `1/1` Ranged 在 165.61–176.00 共变化 5 次（f4799c9e→5b8704a9→3905e9b4→a018b46f→53d0bf0b→1b0aa5c7），每次 `DependencyReadiness.Clear()` 归零重计（`Program/src/Core/CoordinationPlan.cs:67-90`）；同时段 `1/2` Cavalry 0 次变化。交战减员使 Ranged 充当 Guard/Support 时很难攒够 1 秒连续窗口。

**5. `p3-P-20261018-225324` 的 Support 确实写出了命令，此前"从未写令"的判断是错的。** plan-2-task-3 @19.084 `WRITE_EXECUTED ... role=Support requested=(1137.607,830.626) order_target=(1137.607,830.626) readback_error=0.000 first_command_delay=5.550`；引擎 `move_target=(1137.607,830.626)` 保持到 40 s 不变、`order=Move`。

**6.（已更正）该编队不是"后半段停滞"：锚点其实到达了请求点，差额来自锚点与质心之间的偏移发生了变化。** 用派单 §3.1 已给的残差公式 `C' − T = O0 − O1 + e`（`O = A − C`、`e = A' − R`）复算 plan-2-task-3：

- 写令时 `O0 = (−1.734, −1.191)`、`|O0| = 2.104`（`WRITE_EXECUTED` 的 `anchor=(1145.718,869.299) center=(1147.452,870.490)`）；
- 窗口末 `O1 = (3.600, −14.990)`、`|O1| = 15.416`（`SNAPSHOT_FORMATION` 的 `pos=(1137.657,830.756) active_center=(1134.057,845.746)`）；
- `e = A' − R = (0.050, 0.130)`、`|e| = 0.139`。

代入得 `C' − T = (−5.284, 13.929)`，预测质心 `(1134.057, 845.746)`，与实测 `active_center` **逐位一致**。即：锚点**已到达**请求点（0.139 m，远在 0.5 m 容差内），质心距 S\* 的 14.898 m 几乎全部由 `|O0 − O1| = 14.794` 承担。此前"19.34–31.69 走 22.1 m、31.69–39.20 只走 2.6 m（0.35 m/s）、命令有效却停住"的读法**作废**——那一段是锚点到位后单位仍在收拢/重排，不是编队没有执行命令；"`1/2` Cavalry 阻挡"属未验证推测，一并撤回。

同一机制在 `210338` plan-2 的 Guard 上表现为另一面：31.5–121.6 s 期间 `|pos − requested|` 稳定在 0.22–0.31 m、`|C − G*|` 稳定在 1.55–1.62 m（偏移 9.93 m 恒定，两侧同时成立）；151.591 才出现 `|pos − requested| = 11.32` 而 `|pos − G*| = 1.78`、`|C − G*| = 8.51`，即锚点与质心朝相反方向分开。

**7. 由此得到的判据边界：写令换算 `R = T + (A − C)` 只在偏移 `A − C` 于移动期间稳定时才等价于"把质心送到 T"。** 实测该偏移并不恒定（`225324` 的 `1/1` 由 2.10 m 变到 15.42 m；`210338` 的 `1/2` 在 31.5–121.6 s 恒为 9.93 m，151.591 之后才变）。因此：

- 命令"是否写出、是否到达请求点"按锚点与实际读回的订单目标判定（本项两场均满足）；
- 职责关系是否成立仍按质心对冻结 `G*`/`S*` 判定（`GuardInPosition`/`SupportInPosition`，未改动）；
- 两者不一致时，**不能**用"命令有效但编队停滞"或"预算不足"解释，更不能据此改 20 s / 12 m。本段未改任何数值。

**8. `plan-8` 的 `SelectionTimeout` 由原生战术清零我方行为权重导致（新机制，本段未修复）。** 读数链：

- `195.110` `BEHAVIOR_REGISTERED task=plan-8-task-1 ... weight=8.000 active_behavior=CbHoldBehavior`，同刻 `TASK_CANDIDATE_SUBMITTED ... first_submitted_at=195.110 selection_deadline=205.110`；`task-2`/`task-3` 同刻 `WRITE_DELAYED reason=predecessor_not_written`，此后从未提交、也未注册行为（`Program/src/Core/TaskDelivery.cs:78-81` 的前序写令门）。
- `195.130 / 195.530 / 195.580` 三条 `ACTIVE_BEHAVIOR_CHANGED ... active_behavior=BehaviorCharge ours=0`（key 依次 `1/2`、`1/0`、`1/1`）。
- `205.112` `TASK_SELECTION_TIMEOUT task=plan-8-task-1 ... waited=10.002 limit=10.000`；三条 `BEHAVIOR_DISARMED reason=SelectionTimeout ... weight_before=0.000`；`206.112` 三条退出任务 `BEHAVIOR_DISARMED reason=ExitNotApplied ... weight_before=0.000`——即 205.112 退出任务经 `ArmBehavior` 重新写入 8.0 之后，**1 秒内又被清回 0**。

反编译对照（`Program/_scratch/decomp/`）：`BehaviorComponent.ResetBehavior()` 只做 `WeightFactor = 0f`；`FormationAI.ResetBehaviorWeights()` 对 `_behaviors` 全量调用它；`FormationAI.FindBestBehavior()` 在 `if (!(behavior.WeightFactor > 1E-07f)) continue;` 处**直接跳过被清零的行为，连 `GetAIWeight()` 都不调**；`TacticCharge.TickOccasionally()` 对每支有人的编队执行 `ResetBehaviorWeights()` → `SetDefaultBehaviorWeights(item)`（给 `BehaviorCharge/PullBack/Stop/Reserve` 各 1 f）→ `SetBehaviorWeight<BehaviorCharge>(10000f)`。我方 `ArmBehavior`（`Program/src/Host/TestDriver.cs:2605`）只写一次 `SetBehaviorWeight<CbHoldBehavior>(8.0)`（`Program/src/Core/TestParameters.cs:15`），全工程无第二处重设（`SetBehaviorWeight` 仅出现于 `2605` 与撤权时 `2680`）。因此一旦原生进入该战术，我方权重被清零且**不会自动恢复**；且 8.0 相对 10000 f 本来就差三个数量级。

**证据强度与边界**：`weight_before=0.000` 是直接读数；"当前正是 `TacticCharge`"是强推断——其它战术给 `BehaviorCharge` 只设 1 f，我方 8.0 本应胜出，但**日志未记录战术类名**。同机制在历史场次普遍存在（`BEHAVIOR_DISARMED weight_before=0.000` 占比）：`p3-P-20261015-022606` 24/24、`p3-P-20261015-193423` 6/6、`p2-A-20261014-022054` 3/3、`p2-A-20261013-111118` 3/3、`p3-P-20261018-210338` 6/28、`p3-P-20261018-001730` 9/60；另有 `p3-P-20261018-225324`、`-224524`、`-102648` 等 8 场为 0。**属 P2 起既有问题，不是 P3 新增。** 派单 §4 明列"不增权重、不关AI"，是否处理需裁定；本段未改动任何权重、注册或战术逻辑。

**结论**：本段不请求改 20 s / 12 m / 3 s / 5-15-5 包络，也未改任何生产数值。剩余阻断按优先级为：(a) 三编队下职责失效无法重分配（结构）；(b) 交战期新计划的 Guard/Support 3 秒内进不了职责包络，导致约 9 秒周期的重建循环；(c) **已更正**——`225324` 的 Ranged Support 不是停滞，锚点已到达请求点，差额来自偏移变化，**不再构成阻断**；(d) **新定位**——原生战术每 tick 清零我方 `WeightFactor` 并把 `BehaviorCharge` 置 10000 f，我方 8.0 既赢不了也不会自动恢复，导致 `plan-8` 三个任务全部错失 `REAL_SELECTION`。三项实质阻断都超出"接线修复"，需裁定是否改规则。

本段新增只读分析脚本（纯 ASCII，不参与构建）：`Program/_verify/_recompute_reachable.ps1`、`_recompute_swap.ps1`、`_check_digest_stability.ps1`、`_check_guard_loss.ps1`、`_check_guard_zone.ps1`、`_check_plan_guard_envelope.ps1`、`_trace_formation_to_target.ps1`、`_check_orders_at_plan.ps1`、`_check_side_consistency.ps1`、`_check_side_cost.ps1`、`_check_support_travel.ps1`、`_recompute_feasible.ps1`、`_recompute_assignment.ps1`。



### 2026-10-18 裁定两项的闭合：交战期失败重放，与推进期 NotReady 的恢复路径核查

用户当日裁定要求：①用每张计划自己的几何、任务身份和实际读数调用**当前生产判定**重放交战期失败，分清哪些已被裁定①修复、哪些仍不满足完整关系；②对本场 `NotReady` 核对原对象资格、剩余恢复预算与实际命令，明确是恢复接线遗漏还是现有策略已无合法恢复路径。两项均已完成，**只新增测试，未改任何生产代码、未发起新实机场。**

#### 项①：重放（`Program/tests/CoordinationEnvelopeReplayTests.cs`，6 例，已注册进 csproj）

用各计划 `COORDINATION_BOUND` 的冻结 `M*/G*/u/v` 直接构造 `CoordinationGeometry`，再调生产 `PlanBuilder.GuardInPosition`，喂该计划最后一条 `COORDINATION_STATE` 的 Guard 实际质心：

| 计划 | 质心 | fwd | lat | distG\* | GuardInPosition | 处置 |
|---|---|---|---|---|---|---|
| plan-3 | (1289.3,840.2) | 12.29 | −5.68 | 6.13 | **false** | 失败正确（lat 与 dist 双越界） |
| plan-4 | (1293.0,841.1) | 11.84 | −4.07 | 4.46 | **true** | 包络内但 `reads=69`、ready=0 → 1 秒连续窗口未建立，裁定①不成立 |
| plan-5 | (1296.6,842.2) | 9.01 | −1.39 | 1.71 | **true**（ready=1） | 现行代码会判生效；本场判失败是 `95316fd` 之前的行为 |
| plan-6 | (1297.7,843.2) | 8.50 | −0.61 | 1.62 | **true**（ready=1） | 同上 |
| plan-7 | (1302.2,853.0) | 4.88 | −1.04 | 5.22 | **false** | 失败正确（fwd 低于下界 5、dist 越界） |
| plan-8 | (1275.8,824.2) | −43.26 | 16.21 | 55.67 | false | 不构成包络样本：该任务从未写出命令，失败原因是 `SelectionTimeout` |

结论：交战期 8 张计划里，plan-3、plan-7 的失败在现行判定下**仍然正确**（质心确实不在冻结包络内）；plan-5、plan-6 属裁定①已修复的历史行为（本场建于 `2460fd8`，早于 `95316fd`）；plan-4 差的是连续 1 秒读数而非位置；plan-8 是选择超时。全量测试 **346/346 通过**。

#### 项②：本场 `NotReady` 时纠偏**同时**被阶段门和已耗尽的预算挡住（此前"唯一挡住它的是阶段门"是错的）

plan-2 在 148.092 判 `COORDINATION_DEPENDENCY_LOST role=Guard loss=NotReady failure=None` 时，逐项核对结果：

- **对象资格成立**：`TASK_CLOSED ... reason=NotEffective written=1 feedback=Effective qualified_now=1`。
- **命令一直有效**：`plan-2-task-2` 的 `POST_OBSERVE` 共 65 条，19.016 至 147.062 全程 `order=Move`、`feedback=Effective`。
- **纠偏次数额度没被消耗**：全场 `COORDINATION_ROLE_CORRECTION` 仅 1 条（plan-1 Guard @6.593），plan-2 为 0 次。
- **但时间预算早已用尽**：该任务写令 17.136，`RelationshipDeadline` = `WrittenAt + 20` = **37.136**；148.092 时已超期约 111 秒。`CoordinationDriver.cs:243` 的 `earliest + FeedbackWindowSeconds > deadline` 即便在阶段门不存在时也会拒绝。**次数没用过，不代表时间预算还能用。**
- **阶段门是第二个阻断**：`CoordinationDriver.cs:222-223` 要求 `_phase == CoordinationPhase.AwaitingDependencies`，而 148.092 时 `phase=MainAdvancing`（推进任务 `plan-2-task-4-g1` 已于 34.033 写出）。调用侧同样只在等待期尝试：`TestDriver.cs:439-441`。

**测试已按此更正**（`Program/tests/CoordinationDriverTests.cs`）。先前那条 `RoleCorrectionIsNotReachableOnceTheMainIsAdvancing` 把写令设在 200.0、调用设在 220.0，恰好用光了 20 秒预算，返回 null 分不清是哪个门，已废弃。现为三条同组输入（写令 200.0、调用 201.5、截止 220.0、`earliest` 203.0、加 3 秒反馈仍需 206.0 ≤ 220.0）的事实：

1. `RoleCorrectionIsCreatedInTheWaitingPhaseWhileTheBudgetRemains` — 等待期、预算充足 → 纠偏**真的建出来**（`Assert.NotNull` 且旧任务关闭）。没有这条，第 2 条的 null 无法归因。
2. `RoleCorrectionIsBlockedOnlyByTheAdvancePhaseUnderTheSameBudget` — 与第 1 条同时间同几何，只把阶段换成 `MainAdvancing` → 返回 null 且旧任务未关闭；同时断言 `CommandStillApplicable`、锚点已越过请求、`GuardInPosition` 为假。**这才把这一处 null 归因到阶段门。**
3. `RoleCorrectionIsRefusedOnceTheRelationshipBudgetIsSpent` — 用本场真实时序（写令 17.136、截止 37.136、调用 148.092）→ 返回 null，固定"预算耗尽即不可纠偏"，防止把次数没用过当成预算还能用。

**结论与当前处理**（用户 2026-10-18 裁定）：

- **不新增推进期恢复入口**；推进期依赖失效继续走安全结束与已批准的一次重试重规划路径，不增加恢复额度、不重置截止、不通过退回等待期取得新预算。
- 「推进期无原地恢复入口」记为**当前实现限制**，**不宣称它已满足持续指挥目标**。
- 等待期一次纠偏失败（`225324` plan-1：纠偏写令后 3 秒仍 `closed_distance=-6.91`、距离 24.93 m）**不能推断**推进期纠偏必然无效；反之也不能推断它必然有效。要判断得靠一次独立实验，本段不为此开场。

`RelationshipExpired` 不解释 148.092 的失效：`CoordinationDriver.cs:434-440` 的关系截止是 37.136 秒，但 `:411-421` 在 `readiness.Ready && CommandStillValid` 时直接 `return false`，所以关系仍成立时计划可以活过截止，这与既有测试 `RelationshipDeadlineDoesNotExpireAfterRelease` 一致。**该"超期仍存活"与"超期后不可纠偏"并存**：存活判定只要求关系已建立且命令有效，纠偏判定还要求能把 1.5 秒延迟加 3 秒反馈放进原截止内。

### 2026-10-18 行动恢复修复与开发场（CB2-PROMPT-0008 §3.1/§3.2）

**用户裁定（2026-10-18）**：A 采用“到达或沿原请求方向越过”的有限试验版，B 采用“一次重试预算”；明确这两项是策略调整，不是“语义未变的接线修复”。

**修正了什么**

1. `93eaff1` 纠偏门与3秒反馈时序不相容。纠偏要求该职责命令 `Feedback==Effective`，而正在漂移的职责按定义不会 Effective，且判定它失败的同一次3秒反馈会直接关闭计划，纠偏因此在构造上不可达。改为新增 `DependencyReadiness.CommandStillApplicable`（已写入/同职责/同 Revision/读回未偏离），`CommandStillValid` 保持“再加 Effective”，`MainAdvanceAllowed` 释放条件未放��。
2. `2460fd8` A1：原锚点判据由“距请求点≤0.5m”扩为“或沿写令方向越过请求点”。方向取 `R − A_write = T − C = Canonical − Origin`（任务已保存的写令读数，不新增字段、不每帧重算）；内积必须 `>0`，恰好垂直或方向为零时退回原到达判据——这一处比裁定文字更严，因为横向位移既非到达也非越过，且既有反例要求它仍被拒绝。
3. `2460fd8` B2：`PlanLedger` 对同一失败分配给一次重试额度，只在计划真正建成时消耗；键仍不含时间/输入号/坐标，抖动与轮询不刷新额度；前提变化（对象/资格/兵力/成员身份）自然换键，仍按既定规则重评。只对 `NotEffective` 记忆，未扩大其他失败类别。新增 `COORDINATION_RETRY` 日志。

**验证**：新增6例（窗口内纠偏可达、越过可通过、未到达仍拒绝、零方向不通过、一次重试后封锁、前提变化仍可新建），红态3例先失败再转绿；`FailedCoordinationDoesNotRecreateOnTimeOrPositionJitter` 按裁定改为守“额度只有一次”。全量 **335/335**；net472 Release `-t:Rebuild` 0警告0错误。

**开发场 `p3-P-20261018-210338`**（`run-p3-exclusive.ps1 -Scenario P -CombatHoldSec 420`，RESULT OK，443次1Hz独立采样）

| 项 | 前一场 102648 | 本场 210338 |
|---|---|---|
| 纠偏写入 | 0 | **1**（plan-1 Guard @6.593，写令 @8.133，readback_error=0.000） |
| 重试额度消耗 | — | **2**（plan-2 @13.540、plan-8 @193.609，两个不同失败分配） |
| Main 推进 | 0 | **1**（plan-2-task-4-g1 @32.228） |
| 战果冻结 | 0 | **1**（t0=34.033，NE=59 NF=280） |
| 战果评价 | 0 | **42**（均 `decision=Continue`：window_not_mature → no_local_gain，且全部在接触之前） |
| 同状态失败抑制 | 137 | **0** |
| 计划 | 1 | 8 |
| 交战 | 无（敌方 59 全程未损） | **有**：148.589 起，敌方 59→1（193.609）、298.680 起敌方队伍不存在；我方 280→249。交战期 CB2 无有效计划窗 |

身份：CB2 `b20261018-210334-093993ff`，source=deployed=运行读回 `901ed2bf…`（201728 B），pid 54264，`active_modules` 中 Harmony/ButterLib/UIExtenderEx/MBOptionSetting 均在 Native 之前。CBATH `b20261018-210337-0a5be7aa`，`1cf8a96b…`，主线程 `ticks=3 enqueued=1 completed=1`。`end-state`: `safely_exited=1`、`residual_own=[]`、`cleanup_failures=[]`；死战两处前置 `status=ok patches_installed=True`，last-run 421/421 `enabled=true`。

**关键读数**：纠偏写令 `tactic=(1306.809,851.183) anchor=(1297.302,836.030) center=(1302.093,844.305) requested=(1302.018,842.908)`，`T+(A−C)` 复算一致。plan-2 在推进116秒后于148.092记 `COORDINATION_DEPENDENCY_LOST role=Guard loss=NotReady failure=None`，替补为空 → 关闭，属推进期依赖失效的正常归因路径。

**仍缺 / 下一处共同原因（已由裁定①解决，保留作对照）**：plan-3..7 均在同一形态上关闭——Guard 的 Reposition 在3秒内无法产生3m位移，而质心距 G* 已达 1.75/1.66m（plan-5/6）。该冲突已按 2026-10-18 裁定①修复（见上方「裁定①」），修复后 Guard 不再因此被判死；现场阻断已前移到 Support 的 20 秒关系截止。

**本场交战与窗口不重叠（必须如实记录，勿写成“未交战”）**：`BATTLE_STATE` 首次出现损失在 148.589（我方 280 / 敌方 59→58），恰是 plan-2 于 148.092 失效之后；敌方 59→1 于 193.609，298.680 起 `enemy_team_not_found`（敌方队伍已不存在），我方 280→249。因此 plan-2 的 13.540–148.092 是零接触窗口，42 次 `Continue`（NE=59 NF=280、LE=LF=0）全部产生在接触之前，**不构成战果**；真正交战（148.6–194 s）期间 plan-3..8 各自在 3 秒内或选择超时中关闭，CB2 无有效计划窗，歼灭由原生完成，**不能归为 CB2 指挥收益**。


### 当前纠偏（2026-10-18）

- **行为目标**：恢复统一意图下“行动→实际效果→评价→后续决定”；到位和检查器是手段，原生歼灭不是CB2指挥收益。P3尚未通过，主闭环尚未恢复。
- **已修的实现偏移**：`AssignGuardAndSupport`在保护/辅助争同一候选后的辅助重选误用锚点距离，现复用首轮相同的质心取数。生产`BuildCoordination`反例旧版失败（选0/2），修复后选0/3；未改变角色、阈值或命令策略。这是离线发现的同源缺陷，不能认定它造成了第四场三编队的全部超时。
- **已有实机证据新增判读**：第四场285.101秒的`p2-evidence-0299.json`证明Guard锚点距请求0.078m，实际质心距G*15.949m；写时C−A=(18.025,3.259)，此刻=(3.328,9.364)。旧偏移推算会虚报到位，真实质心正确拒绝到位。Support距S*9.456m。已用生产`TaskFeedback`/`GuardInPosition`/`LossOf`重放：位移Effective、未到目标、readiness=0/NotReady可同时成立，不是Observe漏调用。
- **设计前提复核**：三不同职责可表达协同，但当前固定G*/S*只证明点位关系；距离排序不检验是否实际保护关键远程、遮蔽威胁或形成支援，不能据此声称战术收益。5m/连续1s是有效合同中的实现判据，尚无证据证明是所有队形的普遍战术必要条件；本次不擅自改动它们。一次冻结偏移无法保证变形后仍到位，已被实测证伪。当前PlanLedger仅看占比/兵力继续条件，未消费空间建立失败的原因，因而失败后可再次创建相似计划；不能把19次计划当作有效重规划成果。
- **验证**：相关红→绿；全量315/315；net472 Release构建0警告0错误。新增两例分别验证生产分配混用与第四场数据重放。离线复算明细：`E:\Agent\codex\2026-10-18\p3-course-correction\fourth-run-replay.json`。本次没有新实机场次，未部署、取锁或改共享开关；不把旧场重算称为新增运行行为。
- **工作流**：AGENTS §3d/§4已加入可观察战术目标、需求/安全/方案分列、重复整改回查设计前提、合同符合性与目标有效性双重复核；0006 v1.1落实到当前执行范围。不新增审批、模板或长报告。

**已获用户批准并实现的有限命令策略（2026-10-18）**：保留全部几何和到位阈值；仅当原锚点已在现有0.5m容差内、命令仍归当前对象/Plan/Revision、而真实质心尚不满足职责关系时，允许每职责最多一次纠偏。用当前同批A/C/T重新计算请求，经新任务身份、原裁决/资格/主线程/间隔门执行；不得复用已写任务额度，不延长原职责20s截止，剩余预算不足则不发。仍失败就结束，不因同一组对象/成员、相同目标与创建条件再次立即建同类计划；只有相关对象/成员/资格或作战目标条件发生可解释变化才重评。本方案改变了明确的一次写令与重复创建策略，已获用户批准；实现由现有CoordinationDriver/TaskDelivery/PlanLedger与Host实际调用。其目的为恢复实际到位与行动评价，不保证一次纠偏必成功。

离线红态4失败/7通过，恢复实现后相关用例通过；启动失败后补核“原生将成员移到位但纠偏尚未写出”不能逃过原截止的相邻反例（红1失败→绿），最终全量329/329通过；Debug/Release宿主构建均0警告0错误。纠偏不重发健康职责，未写令的新纠偏任务也受原绝对截止约束，写前复核同一截止。后续仍需一个能区分“固定偏移失效/有限纠偏是否实际改善”的开发场，保留反事实离线复算，确认同一有效计划中出现写令、到位/接敌和可解释评价。现场空闲时自动经既有独占入口持锁执行，死战开启；先开发行为、后正式P/N/C。

### 有限纠偏首次开发实测尝试：启动失败，未形成战斗样本

实现提交`0f8fac9`；场次`p3-P-20261018-012518`，既有独占入口`-Scenario P -CombatHoldSec 420`，只尝试一次。CB2 `b20261018-012511-c4d5e0f3`，source/deployed与运行RUNTIME_IDENTITY的DLL SHA256一致为`68EADA314F370AEE374322B6B1CE7975985DF16F9B8980A1E160094080F738BA`（200704 B）；CBATH本组来源`ac7c4ae`，部署构建`b20261018-012516-bd9c26d3`，source/deployed SHA256 `d8a71b11b42532741609683c11845f450f0c96c5c5f07dff15dffae991c3e011`。归属确认PID49084、helper44444，ping与启动上下文一致；CBATH运行身份查询随后超时，不能把部署身份冒充成功的运行读回。

两部署stage完成，collect在进入战斗前失败（`RESULT: FAILURES (6)` / `RESULT: FAILED at collect`）。CB2日志仅有RUNTIME_IDENTITY，无Mission、Plan、纠偏或评价样本。崩溃目录`crash-2026-10-17_17.26.01/rgl_log_errors_49084.txt`为`InvalidOperationException: Connection is not active`，栈位于GABP `TcpConnection.SendMessageAsync -> GabpServer.SendErrorResponseAsync -> OnMessageReceived`；具体触发原因未确认，未修改第三方桥或自动再开场。死战前置与评价三门均未完成验证，不能由缺读数推定通过。

原始`end-state.json`保留`safely_exited=0`的失败。确认游戏/helper进程均为0后，另经同一协调入口持锁恢复并读回`safely_exited=1`，保留配置修复前副本和`post-crash-recovery.json`；三开关0、两探针空，协调器返回后锁available。书面交回观测方。本场没有新增纠偏效果、到位、接敌或反馈决策证据；闭环实机验证仍缺，P3三门未通过。

**第二轮（复核退回三缺陷整改）**：独立复核 `P3-CONTINUATION-REVIEW-732f6df-20261016.md` 对提交 `732f6df` 判「仍退回整改」，给出三项 P1 缺陷与第 4 节更正要求。本文件已按该报告整补：§2.8b 记录坐标接线的接线级测试整补，§3.2/§3.3 记录门2证据判定收紧，§3.4 记录 R5 归属判定与 `end_mission` 前置，§4 与 §7 记录更正后的根因表述。**第二轮整改结论未经过独立复核。**

**第三轮（第二轮复核退回两处边界）**：独立复核 `P3-ROUND2-REVIEW-5ac034b-20261016.md` 对提交 `5ac034b` 判「离线收尾仍不通过」，剩两处：①启动失败且只出现一个他方 PID 时仍被当成本场进程；②门2 把后续合法新计划的同坐标写令当作旧动作未停。本文件相应整补：§3.3（e）记录门2 后继归属与定向探针，§3.4 第 5b 条记录「新增进程数不是所有权」并新增 `foreignonly` 模式。**第三轮整改结论未经过独立复核。**

**第四轮（第三轮复核退回两处边界 + 死战不退前置）**：独立复核 `P3-ROUND3-REVIEW-85e2171-20261016.md`（受审实现 `85e2171`，文档 `badd8d3`）判「离线收尾仍不通过」，剩两处：①门2 一旦出现后继写令就跳过后续全部独立订单核对，「仍执行旧目标」由 fail 变 pass、「独立订单缺失」由 unverified 变 pass；②`processStarted=true` 加差集唯一仍不能证明剩余 PID 属于本场，本场进程退出后只剩他方 PID 时该他方 PID 仍被结束。另要求补齐 `d64055d` 的「死战不退」实机前置。本文件相应整补：§3.3（f）记录门2 后继归属收紧与七场景探针，§3.4 第 5c 条记录「PID 身份由游戏进程自己报出」并新增 `startedthenexited`、`fighttodeathoff` 模式，§3.4 末尾记录死战不退前置接线位置。**第四轮整改结论未经过独立复核。**

**第五轮（定向复核只退回一处防误杀）**：定向复核 `P3-FOCUSED-REVIEW-1e7b088-20261017.md`（受审实现 `1e7b088`，现场 HEAD `76713ef`）确认门2 上轮两条反例与死战不退前置接线均已通过、不必重做，仅剩一处：**「身份文件证明它是谁，仍不能证明由本场启动」**——另一组加载 CBATH 时也会把自己的 PID 身份文件写进共享的 `Documents\CommandBrainATH`，在原 `foreignonly` 反例上加入这一正常行为后，入口仍认领并结束了他方假 PID `777999`。本轮整改：§3.4 第 5d 条改为**归属只认本场自己的启动上下文**（本场 GABS 的 `runtime.json` / `launch.log`），共享身份目录在归属判定中完全弃用，`cbath/ping` 降格为只给已点名 PID 补活性；隔离模拟新增 `foreignidentity` 模式并取 HEAD 红态对照。**第五轮整改结论未经过独立复核。**

**首场实机与第六轮（采集脚本缺陷修复）**：2026-10-17 10:47 经独占入口跑完首个用本轮源码的实机场 `p3-P-20261017-104721`（详见 §4 的「首个本轮源码实机场」）。它**同时验证了三件此前只有离线证据的事**：①部署与运行身份一致（source = deployed = `51A113C6…6400EE`，196096 B，pid=35948）；②§3.4 第 5d 条的归属判定在真实 GABS 上成立（`own game pids: 35948 basis=launch_context_pid_matched_new_process`，且 `cbath/ping` 报同一 pid）；③§3.5 死战不退前置在 `pre_mission` 与 `post_deployment` 两次实机读回均为 `status=ok patches_installed=True enabled=True`，`get_last_run` 的 181 个 1 Hz 采样全部 `enabled=true`。但本场 `RESULT: FAILURES (2)`、`failed stage: collect`，三门全 unverified，**不能作为任何门的证据**。两处失败都定位在主线自己的采集脚本 `Program/_verify/run-p3.ps1`（`Find-OurMain` 只认 Infantry 导致无 Infantry 阵容拿不到主阵、控制动作从未下发；`Assert-CleanEndState` 用 `ReadAllBytes` 读本脚本自己仍持有的锁文件必然失败），已修复并做红绿对照与十模式隔离复跑，**未改 C# 源码**。第六轮不改产品行为，只让下一次实机能真正跑出可判读的对照臂与干净的收尾读数。

**本轮为新开发会话接续。** 接手时存在未提交的 P3 成果（14 项已跟踪 dirty + 未跟踪生产源码 `TaskDelivery.cs`），按派单 §5 逐路径精准暂存提交，未 `git add -A` / `commit -a`、未 reset/clean、未回滚任何既有 dirty。

**第七轮（定向复核确认第六轮的兜底仍不是 Main）**：定向复核 `E:\Agent\codex\2026-10-17\p3-review-2a8de3b\REVIEW.md`（受审 `2a8de3b` / `4e170b8`）确认锁读取修复与十模式隔离可接受、死战模式实机成立，但指出第六轮的兜底选错了对象：它取「我方人数最多的编队」，本场拿到 Ranged 175 @ (906.361,959.823)，而冻结事件 `ACTION_FEEDBACK_FROZEN main=1/0` 指向的是 **Cavalry 52** @ (911.057,978.689)，两者相距 **19.442 m**。本轮只动采集脚本：按冻结行的 `main=<team>/<slot>` 键定位对象，并把实际瞄准的队伍/槽位/身份/兵种/人数/坐标写入 `opponent-target.json`；拿不到键或键找不到对象时如实失败，不再退化成「最大编队」冒充 Main。**第七轮结论未经过独立复核，不改产品代码、不改变任何门的判据。**

**第八轮（`lostRole` 归因修复，本轮唯一改 C# 的一次）**：第二场实机 `p3-P-20261017-181046` 暴露 `HandleAdvanceDependencyLost` 把「保护没有前提失效」直接等同于「失效的是辅助」。到位窗口断开时 `PremiseFailure` 可以仍是 `None`，于是日志与替补选择都指向错误的一侧。本轮把失效归因改为由实际依赖读数产生（新增 `RoleDependencyLoss` 与 `LostRoleOf`，与释放条件共用 `CommandStillValid`），补 4 个红测试，全量测试 309 → **313**。**未改到位半径、稳定窗口、评价公式、退出规则与计划失效条件；结论未经过独立复核，实机行为尚未验证。**

---

## 1. 来源、构建、部署与运行身份

### 1.1 接续基线与本轮提交

**第四场（2026-10-18 00:17）的实机记录已并入本节表格与 §4；本批只改本文件与 `sessions/INDEX.md`，未改任何 C# 与脚本（逐路径行数按入口 §4a 不内嵌自指数值，可用 `git show --numstat <本批提交>` 复核）。**

| 项 | 值 |
|---|---|
| 接续基线（接手时工作树 HEAD） | `f1f01a1` "Dispatch P3 coordinate and execution continuation for replacement developer session" |
| 本轮 CB2 提交 | `732f6df` "fix(p3): close coordinate, delivery, R1/R4/R5 continuation items"（master，本地，未 push / 未建 PR） |
| 上批 CB2 实现提交（历史） | `ced0a18` "fix(p3): close restart remediation items 1-5 for bounded feedback replanning" |
| 上批 CB2 交回提交（历史） | `46aca61` "docs(p3): hand back bounded restart remediation with self-check scope" |
| CBATH 提交（未变） | `E:\MBmod\CBATH-P2`，`codex/cb20-p2-evidence` / `5a2275de434e953802d8de8498b3f09ca1a4fbb6` |
| 本轮规模 | 19 files, +1467 / −164 |
| 本轮交回文档提交 | `5bbdeeb` "docs(p3): hand back the coordinate/execution continuation with self-check scope"（本文件 + `sessions/INDEX.md`，2 files, +352 / −142）；不含实现，不进入身份链 |
| 第二轮整补提交 | `5ac034b` "fix(p3): close review round-2 coordinate wiring, gate-2 evidence and process ownership"（master，本地，未 push / 未建 PR，11 files, +475 / −146） |
| 第三轮整改提交 | `85e2171` "fix(p3): attribute round-2 process ownership and gate-2 successor orders"（master，本地，未 push / 未建 PR，4 files, +120 / −30；父提交为另一组的 `d64055d`，其内容与本次无关） |
| 第三轮交回文档提交 | `badd8d3` "docs(p3): record the round-3 commit hash in the handover and session index"（2 files, +3 / −2） |
| 第四轮整改提交 | `1e7b088` "fix(p3): bind gate-2 successor readings to live orders and PID ownership to runtime identity"（master，本地，未 push / 未建 PR，4 files, +401 / −83；父提交 `badd8d3`） |
| 第五轮整改提交 | `573f098` "fix(p3): bind process ownership to this run's own launch context"（master，本地，未 push / 未建 PR，3 files, +127 / −82；父提交 `76713ef`） |
| 首场实机（本轮源码） | `p3-P-20261017-104721`，2026-10-17 10:47，经 `run-p3-exclusive.ps1 -Scenario P`；CB2 `b20261017-104710-f41051eb`、CBATH `b20261017-104719-6f4500f2`；`RESULT: FAILURES (2)`、三门全 unverified，见 §4 的「首个本轮源码实机场」 |
| 第六轮整改提交 | `2a8de3b` "fix(p3): correct aim-point selection and lock read-back after the first real run"（master，本地，未 push / 未建 PR，3 files, +89 / −13；父提交 `d04c65b`；只改 `Program/_verify/run-p3.ps1` 与两份文档，未改 C#） |
| 第七轮整改提交 | `8942e48` "fix(p3): aim the counter-action at the frozen Main key, not the largest formation"（master，本地，未 push / 未建 PR，3 files, +141 / −39；父提交 `4e170b8`；只改 `Program/_verify/run-p3.ps1` 与两份文档，未改 C#） |
| 第二场实机（本轮源码） | `p3-P-20261017-181046`，2026-10-17 18:10，经 `run-p3-exclusive.ps1 -Scenario P`；CB2 `b20261017-181039-a691fa85`、CBATH `b20261017-181044-83c59fba`；`RESULT: OK`、三门全 unverified，见 §4 的「第二个实机场」 |
| 第八轮整改（`lostRole` 归因） | `9958767` "fix(p3): attribute a lost dependency to the role that actually lost it"（master，本地，未 push / 未建 PR，6 files, +236 / −25；父提交 `cc67303`）；本轮唯一改 C# 的一次：`Program/src/Core/CoordinationPlan.cs` +42/−10、`Program/src/Core/CoordinationDriver.cs` +29、`Program/src/Host/TestDriver.cs` +13/−4、`Program/tests/CoordinationDriverTests.cs` +111、本文件 +39/−11、`sessions/INDEX.md` +2；全量测试 309 → 313 |
| 第八轮口径修订 | `a5f6e44` "docs(p3): record the corrected acceptance target for the lost-dependency fix"（1 file, +1 / −0；父提交 `9958767`）：按用户裁定写入「归因修正不预期让计划多活一段」及三条核对口径 |
| 第三场实机（本轮源码） | `p3-P-20261017-225827`，2026-10-17 22:58，经 `run-p3-exclusive.ps1 -Scenario P`；CB2 `b20261017-225822-2c279a81`、CBATH `b20261017-225826-4091f839`；`RESULT: OK`、三门全 unverified，归因分支未被触发，见 §4 的「第三个实机场」 |
| 第四场实机（本轮源码，定向开发实测） | `p3-P-20261018-001730`，2026-10-18 00:17，经 `run-p3-exclusive.ps1 -Scenario P -CombatHoldSec 420`；CB2 `b20261018-001725-aae2c73c`、CBATH `b20261018-001729-315ca01a`；`RESULT: OK`、三门全 unverified，**完成接敌与歼灭且补足长窗口证据**但全部发生在无有效计划期间，见 §4 的「第四个实机场」 |
| 第四场文档更新 | 本文件与 `sessions/INDEX.md`（随第四场一并提交，短哈希见 `git log`，按入口 §4a 不内嵌自指哈希）：新增第四个实机场小节、§1.3 表新增该场行、§7 新增第 12/13 条（`EXIT_STOP_NOT_APPLIED`、260 s 后协同再未建立） |

`f1f01a1` 只含派单与审查材料（7 files, +870），**未含实现**；派单 §1.1 表列的 `ced0a18` 是上批实现提交，工作树已在其之上。故本轮接续基线取工作树 HEAD `f1f01a1`，「未提交成果」即上批 `ced0a18` 之后的追加改动加本轮新写。

**本轮提交逐路径清单**（19 项，全部先核归属再暂存）：

```
Program/CombatBrain2.csproj                     +1
Program/_verify/_p3-exclusive-stage.ps1         +15/-1
Program/_verify/check-p3-feedback.ps1           +302/-...
Program/_verify/run-p3.ps1                      +169/-...
Program/src/Core/ActionFeedback.cs              +73
Program/src/Core/BattlePlan.cs                  +21/-...
Program/src/Core/CoordinationPlan.cs            +25/-...
Program/src/Core/TaskDelivery.cs                +88   （新文件）
Program/src/Core/TaskFeedback.cs                +20/-...
Program/src/Host/SamplingBehavior.cs            +19/-...
Program/src/Host/TestDriver.ActionFeedback.cs   +100/-...
Program/src/Host/TestDriver.cs                  +198/-...
Program/tests/ActionFeedbackTests.cs            +139
Program/tests/CombatBrain2.Tests.csproj         +2
Program/tests/CoordinateSemanticsTests.cs       +284  （新文件）
Program/tests/CoordinationDriverTests.cs        +4
Program/tests/CoordinationIntegrationTests.cs   +2
Program/tests/DependencyReadinessTests.cs       +8/-...
Program/tests/TaskSelectionDeadlineTests.cs     +161
```

提交后 `git status --porcelain` 剩余（**均为故意留在工作树，非本组交付物**）：

- ` M AGENTS.md`、` M prompts/registry.json` — 治理维护者文件（不同编辑者）。
- ` M Program/src/Host/BuildIdentity.g.cs` — 自动生成身份（`b20261014-120018-9828293f` → `b20261015-222818-c9f62071`），派单 L149 明示不混入。
- 未跟踪：`Program/_verify/_r5sim/`、`_stagesim/`、`_tamper4/`、`_ps51check.ps1`、`gabs-config/`、大量 `_lastrun-*.txt`、`Program/_scratch/`、`_scratch/`、`_scratch_chk.diff` — 均为本次离线取证脚手架与历史 runner 输出，按派单 L149 不混入提交。

**第二轮整补逐路径清单**（相对 `732f6df`，11 项；代码与脚本为 `git diff --numstat` 实测，两份文档随本批同交，行数取提交时实际值）：

```
Program/CombatBrain2.csproj                      +1
Program/_verify/P3-IMPLEMENTATION-HANDOVER.md    （本文件，随本批同交）
Program/_verify/check-p3-feedback.ps1            +60/-11
Program/_verify/run-p3.ps1                       +55/-4
Program/_verify/sessions/INDEX.md                （随本批同交）
Program/src/Core/BattlePlan.cs                   +15
Program/src/Core/ProposalArbiter.cs              +7/-16
Program/src/Core/ProposalWiring.cs               +91/-0    （新文件）
Program/src/Host/TestDriver.cs                   +15/-84
Program/tests/CombatBrain2.Tests.csproj          +1
Program/tests/CoordinateSemanticsTests.cs        +123
```

（与第一轮「docs 单独一个提交」不同，本轮文档与实现同批提交，便于审阅者一次看到状态与证据的对应关系。最终行数以 `git show --stat` 为准。）

**第三轮整改逐路径清单**（提交 `85e2171`，相对 `5ac034b`，4 项；`git diff --numstat` 实测）：

```
Program/_verify/check-p3-feedback.ps1            +36/-1
Program/_verify/run-p3.ps1                       +12/-12
Program/_verify/P3-IMPLEMENTATION-HANDOVER.md    +61/-13（本文件）
Program/_verify/sessions/INDEX.md               +10/-4
```

本轮**未改任何 C# 源码**：两处退回都在离线工具与收尾脚本里，生产 DLL 与上一轮一致。`Program/tests/CoordinateSemanticsTests.cs` 等 309 项测试保持通过，仅作为「未回归」的证据，不是本轮改动。

**第四轮整改逐路径清单**（相对 `badd8d3`，`git diff --numstat` 实测）：

```
Program/_verify/P3-IMPLEMENTATION-HANDOVER.md    +112/-40（本文件）
Program/_verify/check-p3-feedback.ps1            +95/-18
Program/_verify/run-p3.ps1                       +174/-21
Program/_verify/sessions/INDEX.md               +21/-4（随本批同交）
```

本轮同样**未改任何 C# 源码**（`git diff --name-only` 对 `Program/src`、`Program/tests` 为空），改动只在两个验证脚本、两份文档，以及未提交的隔离/探针脚手架（`Program/_verify/_r5sim/`、`_gate2probe/`）。按入口 §4a「仅改脚本或文档不机械重跑未变的 C# 全量测试」，本轮**未重跑 `dotnet test`**，沿用第三轮整改后的 309/309 读数（本轮无任何 C# 改动，`git diff` 可复核）；如独立复核要求新鲜读数，请复跑 §1.2 命令。

守则一处提示：`Program/README.md` 由 `d64055d`（另一组）加入的「死战不退」要求已在本轮接入 `run-p3.ps1`（§3.4），并新增 `fighttodeathoff` 隔离模式给出失败阻断证据。

**第五轮整改逐路径清单**（相对 `76713ef`，`git diff --numstat` 实测）：

```
Program/_verify/P3-IMPLEMENTATION-HANDOVER.md    （本文件，本批同改，不给自指行数）
Program/_verify/run-p3.ps1                       +69/-54（进程归属改绑本场启动上下文）
Program/_verify/sessions/INDEX.md               +10/-7（随本批同交）
```

本轮提交主题 `fix(p3): bind process ownership to this run's own launch context`，3 files、+127/-82；短哈希用 `git log --oneline` 取（按入口 §4a 不内嵌自指哈希）。

本轮同样**未改任何 C# 源码**（`git diff --name-only` 对 `Program/src`、`Program/tests` 为空），改动只在 `run-p3.ps1`、两份文档，以及未提交的隔离脚手架（`Program/_verify/_r5sim/`，新增 `foreignidentity` 模式）。按入口 §4a「仅改脚本或文档不机械重跑未变的 C# 全量测试」，本轮**未重跑 `dotnet test`**，沿用 309/309 读数；如独立复核要求新鲜读数，请复跑 §1.2 命令。

守则一处提示：这次退回的根因正是「用了共享目录里的文件当本场归属证据」。`Documents\CommandBrainATH` 是**共享**目录，任何 CBATH 实例都会写自己的身份文件；本场归属只能取自本场自己的 GABS 启动上下文。

**首场实机后的第六轮改动**（相对 `d04c65b`，`git diff --numstat` 实测）：

```
Program/_verify/run-p3.ps1                       +21/-4（Find-OurMain 兜底、Assert-CleanEndState 锁读取）
Program/_verify/P3-IMPLEMENTATION-HANDOVER.md    （本文件，本批同改，不给自指行数）
Program/_verify/sessions/INDEX.md               （随本批同交）
```

这两处不是新增功能，而是**首场实机暴露的采集脚本缺陷**：`Find-OurMain` 只认 Infantry 导致我方无 Infantry 的阵容拿不到主阵、控制动作从未下发；`Assert-CleanEndState` 用 `ReadAllBytes` 读本脚本仍持有的锁文件必然失败。前者让本场控制臂失效、后者让本场误报为清理失败。详见 §4 的「首个本轮源码实机场」。

**第七轮整改逐路径清单**（相对 `4e170b8`，`git diff --numstat` 实测）：

```
Program/_verify/run-p3.ps1                       +70/-33（删除 Find-OurMain、新增 Find-MainByKey / Get-FrozenMainKey、opponent-target.json、WhatIf 一行）
Program/_verify/P3-IMPLEMENTATION-HANDOVER.md    （本文件，本批同改，不给自指行数）
Program/_verify/sessions/INDEX.md               （随本批同交）
```

本轮同样**未改任何 C# 源码**（`git diff --name-only` 对 `Program/src`、`Program/tests` 只有自动生成的 `BuildIdentity.g.cs`），按入口 §4a 未重跑 `dotnet test`，沿用 309/309。整改细节与离线复算见 §4「首个本轮源码实机场」的第七轮段。提交主题 `fix(p3): aim the counter-action at the frozen Main key, not the largest formation`，3 files、+141/-39。

**第八轮整改逐路径清单**（`lostRole` 归因，相对 `cc67303`，`git diff --numstat` 实测）：

```
Program/src/Core/CoordinationPlan.cs            +42/-10（RoleDependencyLoss、CommandStillValid、LossOf/IsLost；MainAdvanceAllowed 改为复用同一判定）
Program/src/Core/CoordinationDriver.cs          +29/-0（DependencyOutcome.GuardLoss/SupportLoss、Observe 填归类、LostRoleOf）
Program/src/Host/TestDriver.cs                  +13/-4（HandleAdvanceDependencyLost 用 LostRoleOf；日志加 loss= / guard_loss= / support_loss=）
Program/tests/CoordinationDriverTests.cs        +111/-0（4 个依赖失效归因用例）
Program/_verify/P3-IMPLEMENTATION-HANDOVER.md    （本文件，本批同改，不给自指行数）
```

本轮**改了 C#**，按入口 §4a 已重跑全量测试与两套构建：`dotnet test` **313/313 通过**，Release / Debug 均 0 警告 0 错误。红态证据：把 `LostRoleOf` 临时换回旧规则后 39 项中 3 项失败（`GuardWindowBreakWithNoPremiseFailureIsReportedAsGuard` 报 `Expected: Guard / Actual: Support`）。**未改到位半径、稳定窗口、评价公式、退出规则与计划失效条件。**

**未动他方材料**：`E:\MBmod\BattleObservation-V1\{CB2,CBATH,TCL}` 未修改、未构建、未切换、未提交（`git worktree list` 复核：观测组两 worktree 仍在 `e2a0e38` / `925c224`）；`ObservationAdapter.cs` 约定接口与事件位置未改；`sources/` 与历史失败原始场未改。

### 1.2 构建与测试（提交后重跑）

```
cd "E:\MBmod\CombatBrain 2.0\Program"
dotnet build CombatBrain2.csproj -c Release --no-restore -t:Rebuild   → 0 个警告 0 个错误
dotnet build CombatBrain2.csproj -c Debug   --no-restore              → 0 个警告 0 个错误
dotnet test  tests\CombatBrain2.Tests.csproj -c Debug --nologo        → 通过 309 / 失败 0 / 总计 309
```

第二轮整改后重跑（2026-10-16）：

```
dotnet build CombatBrain2.csproj -c Release --no-restore --nologo     → 0 个警告 0 个错误
dotnet build CombatBrain2.csproj -c Debug   --no-restore --nologo     → 0 个警告 0 个错误
dotnet test  tests\CombatBrain2.Tests.csproj -c Debug --nologo        → 通过 309 / 失败 0 / 总计 309
```

第三轮整改后重跑（2026-10-16，仅脚本与文档，未改 C# 源码）：

```
dotnet build CombatBrain2.csproj -c Release --no-restore --nologo     → 0 个警告 0 个错误
dotnet test  tests\CombatBrain2.Tests.csproj -c Debug --nologo        → 通过 309 / 失败 0 / 总计 309
```

第四、五轮整改后**未重跑**（两轮都只改 `Program/_verify/` 下的脚本与文档，`git diff --name-only` 对 `Program/src`、`Program/tests` 为空，唯一命中的 `.cs` 是自动生成的 `BuildIdentity.g.cs`），按入口 §4a 沿用上表 309/309 作为未回归证据。第五轮实际执行的离线验证见 §3.4 / §5.4 / §5.5。

**首场实机（2026-10-17 10:47）后的第六轮改动**同样只动 `Program/_verify/run-p3.ps1`（`Find-OurMain` 兜底、`Assert-CleanEndState` 锁读取），未改 C#，故仍未重跑 `dotnet test`。这一轮有了一次真实构建与运行：`deploy.ps1` 重新生成了 `BuildIdentity.g.cs`（`build_id=b20261017-104710-f41051eb`），部署到游戏的是 `51A113C6A0F574781C2F6F96C1DD3B4E1A6AA85CF1FD402DF5B3B3B8246400EE`（196096 B）。本地产物哈希随 `BuildIdentity.g.cs` 每次构建变化，不与提交绑定。

本地产物（提交后重建）：

| 产物 | 长度 | SHA256（前 16） |
|---|---|---|
| `Program\bin\Release\CombatBrain2.dll` | 178176 | `57AFC58E24ED142B` |
| `Program\bin\Debug\CombatBrain2.dll` | 197120 | `0902EF29966A40E3` |

（上表为第八轮整改后本地重建读数；`BuildIdentity.g.cs` 每次构建重新生成，DLL 哈希随构建身份变化，不与提交绑定。）

第八轮（2026-10-17，`lostRole` 归因修复）改动 C#，已重跑：

```
dotnet build CombatBrain2.csproj -c Release --no-restore --nologo     → 0 个警告 0 个错误
dotnet build CombatBrain2.csproj -c Debug   --no-restore --nologo     → 0 个警告 0 个错误
dotnet test  tests\CombatBrain2.Tests.csproj -c Debug --nologo        → 通过 313 / 失败 0 / 总计 313
```

测试构成（逐文件实数 `[Fact]`+`[Theory]`，第八轮后共 **313**）：

| 测试文件 | `ced0a18` | 第二轮后 | 第八轮后 | 归属 |
|---|---|---|---|---|
| `Program/tests/CoordinateSemanticsTests.cs` | 0 | 15 | 15 | §2 坐标语义（新建文件；第二轮 +5） |
| `Program/tests/TaskSelectionDeadlineTests.cs` | 25 | 36 | 36 | 8 个投递时序/幂等用例（0005 批工作，随未跟踪的 `TaskDelivery.cs` 本次一并收） + 3 个 §3.1 用例 |
| `Program/tests/ActionFeedbackTests.cs` | 52 | 59 | 59 | §3.2 R1 继任状态用例 |
| `Program/tests/CoordinationDriverTests.cs` | 35 | 35 | 39 | 第八轮 4 个依赖失效归因用例 |

第一轮本会话新增 `[Fact]` 13 个（§2 的 10 个 + §3.1 的 3 个）；其余 15 个是第一轮开始前已在工作树内、随源码首次进入提交的用例。派单 §1.2 L47 记载「隔离源码快照独立 Rebuild 全量测试 291 通过」= 276 + 这 15 个未提交增量，与第一轮一致（`291 → 304`）。第二轮再 +5，全量 `304 → 309`。第八轮再 +4，全量 `309 → 313`。

`CombatBrain2.csproj` 内无 post-build 部署 target。测试工程保持 `net8.0`，只 `Link` Core 源、不链 Host。

### 1.3 逐场运行身份

| 场次 | build_id | pid | assembly_file_sha256 前 16 | 部署长度 | 部署 SHA 前 16 |
|---|---|---|---|---|---|
| p3-P-20261015-004350 | b20261015-004347-1bf63a79 | 43832 | bb75414a4c4d01f2 | 182784 | BB75414A4C4D01F2 |
| p3-N-20261015-004851 | b20261015-004848-e5b2f53f | 44492 | a43f2c1beae69c32 | 182784 | A43F2C1BEAE69C32 |
| p3-N-20261015-005621 | b20261015-005619-50633b36 | 46636 | b9b0a38423895ad8 | 182784 | B9B0A38423895AD8 |
| p3-C-20261015-010928 | b20261015-010857-79e62453 | 56064 | 8fecd9b8a5844ba7 | 182784 | 8FECD9B8A5844BA7 |
| p3-P-20261015-193423 | b20261015-193418-c9783e27 | 37740 | 905fdcf8d637a4f0 | 190976 | 905FDCF8D637A4F0 |
| p3-P-20261015-195035 | b20261015-195032-52e8ca1a | 53432 | f40846deb289e3b1 | 190976 | F40846DEB289E3B1 |
| p3-P-20261015-203136 | b20261015-203133-f7690240 | 612 | b3b3aab3645f43c0 | 191488 | B3B3AAB3645F43C0 |
| p3-N-20261015-211938 | b20261015-211934-fd6da03a | 7732 | 91421cef819cd39f | 191488 | 91421CEF819CD39F |
| p3-N-20261015-213231 | b20261015-213227-5da13be9 | 44312 | 35a1fa95e0ea5dee | 191488 | 35A1FA95E0EA5DEE |
| p3-C-20261015-214157 | b20261015-214152-84d1958d | 56508 | fe9d9ace2c688bd9 | 191488 | FE9D9ACE2C688BD9 |
| p3-P-20261015-214532 | b20261015-214529-e411067d | 6428 | 68d7e0b4dd1ac385 | 191488 | 68D7E0B4DD1AC385 |
| p3-P-20261015-215314 | b20261015-215311-0e545b62 | 42904 | 342c73dc6287ec35 | 191488 | 342C73DC6287EC35 |
| p3-P-20261015-220249 | b20261015-220246-a9c223d9 | 58580 | 7a1f981094793b67 | 191488 | 7A1F981094793B67 |
| p3-C-20261015-220738 | b20261015-220735-18fd6c08 | 6380 | 7d5e848c0c4aad8e | 191488 | 7D5E848C0C4AAD8E |
| p3-P-20261015-221833 | b20261015-221826-977cb576 | 46552 | db58cbe71973f790 | 191488 | DB58CBE71973F790 |
| p3-P-20261015-222821 | b20261015-222818-c9f62071 | 24920 | 1e980eb3acd51410 | 191488 | 1E980EB3ACD51410 |
| **p3-P-20261017-104721** | **b20261017-104710-f41051eb** | **35948** | 51a113c6a0f57478 | **196096** | 51A113C6A0F57478 |
| **p3-P-20261017-181046** | **b20261017-181039-a691fa85** | **56080** | 0a4dd23cbd06c310 | **196096** | 0A4DD23CBD06C310 |
| **p3-P-20261017-225827** | **b20261017-225822-2c279a81** | **8644** | a54bd145ea90db9f | **197120** | A54BD145EA90DB9F |
| **p3-P-20261018-001730** | **b20261018-001725-aae2c73c** | **16684** | 360ca33fc545c043 | **197120** | 360CA33FC545C043 |

证据来源：各场 `CombatBrain2.log` 的 `RUNTIME_IDENTITY`（`assembly_file_sha256` / `pid` / `Location`）与 `deployed-module.json`（`path` / `size` / `sha256` / `last_write`）。182784 = 基线旧构建；190976 / 191488 = 上批新构建；196096 / 197120 = 本轮新构建。

最后四行 `p3-P-20261017-104721`（10:47）、`p3-P-20261017-181046`（18:10）、`p3-P-20261017-225827`（22:58）、`p3-P-20261018-001730`（00:17）是**用本轮源码实机运行的四场**，均经独占入口 `run-p3-exclusive.ps1 -Scenario P`。第一场取自含第六轮未提交 runner 修复的工作树、`RESULT: FAILURES (2)`；第二场取自第七轮提交 `8942e48`/`bd5674e`、`RESULT: OK`；第三场取自第八轮提交 `9958767`/`a5f6e44`、`RESULT: OK`；第四场同为 `9958767`/`a5f6e44` 源码，采集窗口 `CombatHoldSec=420`、`RESULT: OK`。四场三门全 unverified，见 §4 的四个实机小节。

`p3-P-20261015-221833` CBATH 身份（`build-identity.json`）：`build_id=b20261015-221831-76da5cbd`、`assembly_sha256=be477d94514ff700770c6939d833a01933daeebe57e72661f9b0ba9279374b8c`、`mvid=6c443e22-98c3-4463-9da1-23f63ec73d88`、`pid=46552`、`game_version=v1.4.8.119303`，`active_modules` 含 `CombatBrain2` 与 `CombatBrainAutonomousTestHarness`。

CBATH 模块源码 SHA256（与 `_audit/cb20-p3/2026-10-16-coordinate-restart/baseline.json` 的 `cbath.reviewed_source_files` 逐项一致）：`CbathTools.cs`=A894FA6979BF5E13…、`P2FormationEvidence.cs`=63F66BD9EAD0A174…、`P3OpponentControl.cs`=B0EFA5DE88CA510A…、`RunRecord.cs`=A26EA645402EF0C5…、`SamplingMissionBehavior.cs`=8239AA21DCF3C6A2…、`CombatBrainAutonomousTestHarness.csproj`=1521AD981EBA2E1E…

**重要说明**：§1.3 中除最后三行外的实机场次都产生于本轮提交之前（源码 mtime 20:16–20:25、场次时间 22:00–23:00），它们**不能**为本轮提交给出任何门的结论。最后三行（`p3-P-20261017-104721` / `p3-P-20261017-181046` / `p3-P-20261017-225827`）用本轮源码实机跑过，但三门仍全 unverified。见 §4 的三个实机小节与 §7。

---

## 2. 位置语义全链修复（派单 §2）

### 2.1 已裁定的换算契约

派单 §2.1 裁定：设写前有效锚点 `A`、同对象同批读数质心 `C`、希望质心到达的战术目标 `T`，则 **实际下发的锚点目标 = T − (C − A) = T + (A − C)**。旧提议 `T + (C − A)` 方向错误，未采用。

唯一新增生产 API，`Program/src/Core/BattlePlan.cs:146`：

```csharp
internal static double AnchoredRequest(double tactic, double anchor, double center) => tactic + (anchor - center);
```

最小校验已固化为测试：`A=100, C=110, T=150 → 140`（下发 160 会把预期质心推到 170）。

### 2.2 字段语义分离（§2.2 第 1 条）

`Program/src/Core/BattlePlan.cs` 字段注释与用途已分开表达，未新建通用变换框架：

| 字段 | 语义 |
|---|---|
| `CanonicalX/Y` + `HasCanonicalTarget` | **战术目标 T**（期望质心位置），来自计划几何（含撤回的质心向后 20m 点） |
| `RequestedX/Y` + `HasPosition` | **本次实际写给引擎的锚点目标** |
| `BaselineX/Y` | 锚点语义任务记当时编队锚点；质心语义任务（`HasCanonicalTarget`）记同批读数有效成员质心 |
| `OrderTargetX/Y` | 原生 MovementOrder 的实际读回值（非请求值） |

提案阶段的 `TargetError`（`Program/src/Core/ProposalArbiter.cs:416`）保持原样：任务未写令时 `HasPosition=false` 走 `CanonicalX/Y`，写令后比 `RequestedX/Y`，两阶段语义各自正确。

### 2.3 写前换算与冻结（§2.2 第 2 条）

`Program/src/Host/TestDriver.cs:2000-2037` `CoordinationOrderFor`：

- 新增 `anchor_unreadable`（`:2093`）与 `center_unreadable`（`:2088`）两道门 —— **质心缺失不再退用编队锚点冒充**，直接不写令。
- `requestX/Y = FormationTask.AnchoredRequest(targetX, record.PosX, record.ActiveCenterX)`，并加 `non_finite_request` 门。
- `task.SetCanonicalTarget(targetX, targetY)` + `task.SetTarget(centerX, centerY, requestX, requestY)`；下发 `MovementOrderMove(requestX, requestY)`。
- note 改为 `move_to_role_point role=… tactic=(…) anchor=(…) center=(…) requested=(…) revision=`，五个量同时留痕。

撤回分支（`Program/src/Host/TestDriver.cs:1955` 一带）做同口径修复：`withdraw_center_unreadable` 门 + `AnchoredRequest(wx, wrec.PosX, wrec.ActiveCenterX)`，原点与基线用质心。

### 2.4 真实写令基线（§2.2 第 3 条）

`Program/src/Host/TestDriver.cs:1698-1736` 重写。原实现无条件 `task.SetBaseline(f.CurrentPosition)`（锚点）却对 `RequestedX/Y`（T 语义）算距离。现按任务语义分流：

- 质心语义（`task.HasCanonicalTarget`）：从 `_snapshot.FindByKey(task.FormationKey)` 取同批 `ActiveCenterX/Y` 作基线；快照缺失或 `!HasActiveCenter` → 记 `BASELINE_REFUSED … reason=center_unreadable`（`:1722`）并 **return 不写令**；`goalX/goalY = task.CanonicalX/Y`。
- 锚点语义（P1 普通位置任务）：保持 `f.CurrentPosition` + `task.RequestedX/Y`。
- 两者都写 `task.DistanceToTargetAtWrite = task.BaselineDistanceTo(goalX, goalY)`。

计划创建到发令之间的原生移动因此不计入本命令效果；到点与接近距离不再混用不同参考点。

### 2.5 命令读回核对实际下发目标（§2.2 第 4 条）

`OrderTargetReadbackError()`（`BattlePlan.cs:200`）语义未改：比较「当前实际引擎目标」与「本次锚点请求」，0.5 m 容差；读不到/被原生改目标/旧版本旧身份都不拿首次缓存顶替（`RefreshOrderTargetReadback` 读不到即 `ClearOrderTarget()`）。修复只改了**消费方**：不再拿锚点读回直接对 T。

### 2.6 效果与依赖各守本义（§2.2 第 5 条）

`Program/src/Core/TaskFeedback.cs:137-155` 的 Reposition 分支改为三层：

- `task.HasCanonicalTarget && !now.HasActiveCenter` → `detail = "center_unreadable"`，**不 effective**（`:137`）。
- 其余：`bool byCentroid = task.HasCanonicalTarget`（`:144`）；`refX/refY` 取质心或锚点（`:145-146`）；`goalX/goalY` 取 `CanonicalX/Y` 或 `RequestedX/Y`（`:147-148`）；`distance` 与 `from = BaselineDistanceTo(goalX, goalY)`（`:152`）同源。
- 判据数值未动：3 m 进度、0.5 m 到点、0.5 m 读回、5 m 职责到位仍相互独立，未合并或放宽。

`Program/src/Core/CoordinationPlan.cs:111-136`：`CommandTargetStillValid` 签名改为 `(BattlePlan plan, FormationTask task, bool isGuard)`，要求 `HasPosition`、`HasOrderTarget`、`OrderTargetReadbackError() <= 0.5`、`HasCanonicalTarget`、`task.PlanId == plan.PlanId`、`task.Revision == plan.Revision`，最后比较 `CanonicalX/Y` 与 `Geometry` 该职责规范点距离 ≤ 0.5 m。**不再调用 `PlanBuilder.GuardInPosition`/`SupportInPosition`**。

残留核查（grep 全 `src/`）：`GuardInPosition`/`SupportInPosition` 生产调用点只剩 2 处，均为**质心口径、正确**——`TestDriver.cs:782-783`（竞争注入候选合法性，用注入坐标）与 `CoordinationDriver.cs:200-201`（`ObserveOne` 用 `obs.X/obs.Y`，即 `ObservationFor` 填的 `rec.ActiveCenterX/Y`）。`CoordinationPlan.cs` 已无调用。

### 2.7 撤回链与 P1 边界（§2.2 第 6 条）

- `Program/src/Host/TestDriver.ActionFeedback.cs:820-823` `WithdrawTick` 到点判据改为 `goalX = task.HasCanonicalTarget ? task.CanonicalX : task.RequestedX`，用 `(rec.ActiveCenterX, rec.ActiveCenterY)` 对目标比 `<= P3WithdrawArrivalMeters(5.0)`。
- 撤回建任务（`BattleAssessment.cs:299-300`）仍以质心为 origin、`tx/ty` 为 requested/canonical，未改语义。
- P1 普通位置任务（`MakeTask` 用 `record.PosX/Y` 为 origin）**未全局改语义**，`AnchorDefinedTaskKeepsAnchorSemantics` 用例锁定。

### 2.8 红测试证据（§2.2 L76）

新增 `Program/tests/CoordinateSemanticsTests.cs`（10 个 `[Fact]`，已加入 `Program/tests/CombatBrain2.Tests.csproj`）。夹具分开给 `PosX`（锚点）与 `ActiveCenterX`（质心），使用生产实际调用的函数。

**修复前实测：失败 5 / 通过 5**，失败项与缺陷点一一对应：

| 用例 | 修复前 | 对应 §2.2 条 |
|---|---|---|
| `AnchorArrivalWithStalledCentroidIsNotEffective` | Expected Pending，Actual **Effective** | 3/5 |
| `OffsetApproximationCannotFakeArrival` | `ReachedTargetPoint` Expected False，Actual **True** | 7 |
| `UnreadableCentroidDoesNotFallBackToAnchor` | Expected Not Effective，Actual **Effective** | 2 |
| `StaleTacticTargetBlocksMainAdvanceEvenIfAnchorIsInside` | Expected False，Actual **True** | 5 |
| `CurrentTacticTargetStillReleasesMainAdvance` | Expected True，Actual **False** | 5 |

修复后 **10/10 通过**；全量由 276 → **304/304**（本会话新增 13 个，见 §1.2）。

### 2.8b 复核退回后的接线级测试（第二轮整补）

复核指出 §2.8 的夹具自己复刻了 Host 读取逻辑（`task.TryTacticTarget`），没有覆盖「公布提案→写前换算→当前裁决复核」的真实接线。

**处理**：把这条判定链从 Host 提到 Core —— 新建 `Program/src/Core/ProposalWiring.cs`，导出 `TacticTargetPoint` / `BuildProposal` / `RecheckPublished`。Host 的 `ProposalFor` 与 `CoordinationWriteBlock`（`TestDriver.cs`）改为直接转调，原来的 `RoleTargetPoint` 私有复本删除；`Program/CombatBrain2.csproj` 与 `Program/tests/CombatBrain2.Tests.csproj` 各加一条 `Compile Include`。测试工程不含 Host（net8.0，Host 依赖 TaleWorlds），提到 Core 后离线夹具才能走与实机同一份实现。

`Program/tests/CoordinateSemanticsTests.cs` 的 `ProposalOf` 改为调 `ProposalWiring.BuildProposal`，并新增 3 个用例：

| 用例 | 断言 | 覆盖 |
|---|---|---|
| `WriteGateRejectsTargetChangedAfterPublish` | 公布后职责点改 10 m → `decision_content_changed` | 写前复核真的拦得住 |
| `WriteGateBlocksWhenNoDecisionPublished` | 无已公布裁决 → `no_published_decision` | 查不到结果不得默认放行 |
| `WriteBoundaryConversionKeepsProposalIdentity`（加强） | 追加断言 `RecheckPublished(...) == null` | 非零二维偏移下正常命令能写出 |

**红态验证（本轮实测）**：把 `BattlePlan.TryTacticTarget` 的读取顺序倒回「先 Requested 后 Canonical」，`CoordinateSemanticsTests` 立刻 **2 失败 / 13 通过**（`WriteBoundaryConversionKeepsProposalIdentity` 期望 `...|0.000,35.000` 实得 `...|-10.000,33.000`；`CompetitionErrorUsesTacticTargetAfterWriteBoundary` 期望 0 实得 10），恢复后 15/15 通过 —— 夹具确实挂在生产实现上，不是自证。

全量测试由 304 → **309/309**：`CoordinateSemanticsTests.cs` 事实数 10 → 15（fix1 的 3 个 + 复核实要求的 2 个写前复核用例，另加强 1 个既有用例的断言）。

### 2.9 同步调整的既有测试

`DependencyReadinessTests.cs`（3 处）、`CoordinationDriverTests.cs`（`CommandsInEffect`）、`CoordinationIntegrationTests.cs`（`BringBothInPosition`）此前把 `SetOrderTarget` 设为几何点本身，与新语义冲突，已补 `SetCanonicalTarget`。`TaskSelectionDeadlineTests.cs:355 MoveRequiresClosingDistance` 是**锚点语义**用例（不填质心），未改，锁定 P1 语义不变。

### 2.10 不把固定偏移当实际到位（§2.2 第 7 条）

未引入任何按 tick 连续改写目标追质心的逻辑；近似不足时走既有失败/重规划路径（`TaskFeedback` Failed → `InvalidatePlan`），未增权重、未延寿、未循环重试。

---

## 3. 投递调度、R1、R4、R5（派单 §3）

### 3.1 投递调度（§3.1）

**结构核实结论：逐 tick 投递的结构在交接时已存在（未提交的 `SamplingBehavior.cs` 改动）**，`SubmitPending` 与 `PublishCoordinationProposals` 已移出 3 s 决策块，且 `SubmitPending` 在 `PublishCoordinationProposals` **之前**（否则首个到达写前门的提案会被当唯一竞争者提前公布）。本轮补的是**身份门**与**日志节流**。

**实机延迟归因**：`p3-P-20261015-220249` 的 125.197 公布 → 127.708 首次投递（合法最早 126.697，额外等待约 1.011 s），是**该场构建尚未做逐 tick 投递**所致，不是当前代码缺陷。证据：全场 `TASK_CANDIDATE_SUBMITTED` 时刻全部落在 3 s 评价网格（4.538 / 7.540 / 10.547 / … / 124.703×3 / 127.708）；`EVAL_TICK` 网格为 …124.708 / 127.708，plan-2 于 125.197 公布，下一个评价点才是 127.708。**逐 tick 结构落地后不会再现。** 未承诺固定 4.5 s 或 9 s 必写完。

**本轮修复（`Program/src/Core/TaskDelivery.cs`，88 行，新收录）**：`TimingDeferral` 新增

```csharp
if (f.ExitTask) return "";                       // :61  退出任务不受身份门与时序门
if (!f.HasPlan) return "not_in_current_plan";    // :64  关闭 L85 绕过点
```

`Decide`（`:47`）对退出任务仍直接 `Queue`。此前 `if (!f.HasPlan) return ""` 让已关闭/被替换/无当前计划的普通任务绕过全部时序门直接进 Queue —— 派单 L85 明示禁止，现已闭合。重分配任务从新分配时刻重新计时（`AssignedAt` 优先于 `PlanCreatedAt`）；`ExecutionGate.SubmitCandidate` 仍只记首次提交、重投不刷新获选截止（既有测试 `ExtendDeadline` 锁定）。

**日志节流（L87）**：`Program/src/Host/TestDriver.cs:46` 新增 `_lastDelayReason` 字典，`:1112-1114` 只在原因变化时打 `WRITE_DELAYED`，`OnMissionStart`（`:2914`）清空。不改 `check-p3-feedback.ps1` 的解析面（该脚本只解析 `REAL_SELECTION_NOT_SUBMITTED`，不解析 `WRITE_DELAYED`）。

**3 个新用例**（`TaskSelectionDeadlineTests.cs:723/736/754`）：`TaskWithoutCurrentPlanIsNotQueued`、`PlanIdentityGateIsDistinctFromTimingGates`、`ExitTaskIsQueuedEvenWithoutCurrentPlan`。

### 3.2 R1：继任状态与公布前合法性（§3.2）

审查退回项 R1 位于 `_audit/cb20-p3/2026-10-16-coordinate-restart/REVIEW.md:31-42`。四条已在工作树中修复（本轮补入版本库并补测）：

1. `Program/src/Core/ActionFeedback.cs:270-281` `AfterStandbyCompleted`：仅当 `WaitingForExitConfirmation && ExitStandbyCompleted && !Reinforced` 才置 `Stopped/OffensiveBlocked` —— **正反馈继任不再误置进攻禁入**。
2. `Program/src/Host/TestDriver.ActionFeedback.cs:13-45` `ActionFeedbackStep`：`OffensiveBlocked` 早退分支（`ACTION_BLOCKED reason=loss_not_resolved`）只在负反馈触发；② 止损分支先于 ① 正反馈执行，正反馈确认后累计代价评价仍继续。
3. `Program/src/Core/ActionFeedback.cs:287-294` `MarkStandbyFailed()` 只清等待态，**不清 `Stopped`/`OffensiveBlocked`**（已判定的负反馈禁入不因退出失败被解除）。
4. `Program/src/Host/TestDriver.ActionFeedback.cs:400-464` `StandbyInvalidReason` 从 `_snapshot.FindByKey` 补 `Found/CurrentObjectIdentity/ActiveMemberCount/TeamIndex/HasActiveCenter/IsAiControlled` 造 `SuccessorRoleFact`，调 `:452 ActionFeedback.SuccessorRolesStillValid(...)`（身份/归属/AI/兵种/人数继续线/互不相同）；协同计划再要求 `ProjectedMainCenterOf` 与 `ActionFeedback.RiskGate(...).Allowed`。

**7 个 R1 用例**（`Program/tests/ActionFeedbackTests.cs:1082-1192`）：`PositiveSuccessionKeepsOffensiveEvaluationAliveAfterExitConfirmation`、`NegativeSuccessionAppliesStopOnlyOnExitConfirmation`、`FailedExitConfirmationKeepsKnownNegativeBlock`、`SuccessorRolesRejectStaleObjectIdentity`、`SuccessorRolesRejectObjectLostAiControl`、`SuccessorRolesRejectRoleBelowContinueLine`、`SuccessorRolesAcceptCurrentObjects`。

截止行为未放宽：`DecideSuccessorStandby` 逐必需对象要求实际写出且 `WrittenAt <= DeadlineAt`；`allApplied` 后仍需 `RequiredObjects.Count != 0`、`SuccessorPrepared`、`SuccessorInvalidReason` 为空才 `Confirmed`；否则到 `DeadlineAt` 才 `TimedOut`。新分配延迟从**实际公布**起算（`PublishSuccessorTasks` 置 `RoleAssignedAt = now`）。

### 3.3 R4：独立复算与敏感性反例（§3.3）

审查退回项 R4 位于 `REVIEW.md:44-69`，三处已闭合：

**（a）门2 必需对象改取决定时版本。** `Program/_verify/check-p3-feedback.ps1:842-905`：从决定时刻前（`at <= invAt`）的 `ASSIGNMENT_PUBLISHED` 中取**最大 revision** 行，逐 `obj` 去重（不可用整场最后版本，也不可用后来 plan-2 的退出补算）；无该行时回退 `TASK_CREATED` 并显式 Note。退出证据只认**同 plan** 的退出任务（`:900-904`）。

`p3-P-20261015-221833` 本次必需对象为**三个**：

| 角色 | 对象 | 本次 `EXIT_STOP_DONE` | 说明 |
|---|---|---|---|
| Main | `030dd50e@Infantry` | 134.036 | order_before=StandYourGround |
| Support | `038572bf@Ranged` | 134.086 | order_before=Move |
| Guard | `008c0c1e@HorseArcher` | 134.182 | 重分配进入；**实际兵种 Cavalry，不能按槽位后缀推断** |

`SUCCESSOR_EXIT_CONFIRMED pending=plan-2 waited=0.344 detail=all_exits_applied` @134.187，`deadline=134.844`。**不再报 2/2。** `0364ad2d@Cavalry` 由更早一次重分配交出，不列为必需停止项（检查器显式 Note）。

**（b）逐条复跑旧反例（本轮实测）**：

| 场次 / 副本 | 门2 | 关键 detail |
|---|---|---|
| `p3-P-20261015-221833` | **pass** | `assigned=3 stopped=3 stop_failed=0 unaccounted=0 taken_over_before_confirmation=0` |
| `p3-N-20261015-005621`（旧硬反例） | **fail**（保留） | `unaccounted=3`、`stopped=0 of assigned=3`、`taken_over_before_confirmation=3`、`still_on_old_order=035c31d5@Ranged@142.972,0341f44c@HorseArcher@142.972` |
| `tamper-missing-reassigned-exit` | **fail** | `assigned=3 stopped=2 unaccounted=1`（删重分配 Guard 的本次退出即不再 pass） |
| `tamper-independent-order-contradiction` | **fail** | `still_on_old_order=008c0c1e@HorseArcher@134.746`（独立订单与停止声明冲突被拒） |
| `p3-N-20261015-004851` / `-211938` / `-213231` | unverified | 无 stop/disengage 决定 |
| `p3-P-20261015-222821` | unverified | 无 `ACTION_FEEDBACK_FROZEN` |

**（c）门3 按 t0 对齐，且不擅自扩容差。** `Get-SceneFingerprint([string]$dir, [double]$t0)` 重写为读 `cbath-timeline.json`（唯一带 per-formation `facing` 的产物），取 mission time 最接近 t0 的样本（不再取首个数字编号样本）。判定链：离散事实（map / own_size / enemy_size / own_comp / enemy_comp）不符 → **fail**；几何（own_center / enemy_center / relative_distance / own_facing / bearing）不等 → **unverified**（等价未证明，不判 fail、不扩容差）；地形独立记 Note。

`p3-P-20261015-220249` vs `p3-C-20261015-220738` 本轮实测：

```
ok    gate3_arms_differ_only_in_counter_action counter_action this=1 control=0 other_fields_match=True
ok    gate3_scene_discrete_match map/army/composition compared: identical
ok    gate3_control_arm_has_no_matching_decision control replan=False decision=False freezes=1
GATES: gate3_control: unverified -- with_counter_action=True control=False fields_match=True scene_discrete=True scene_geometry=False
```

两臂离散事实相同（map=`battle_terrain_biome_096`、sizes 21/184、comp `Infantry=21 | Cavalry=46,Infantry=29,Ranged=109`）；几何不同（P t0=23.039 样本 t=23.132：center `1287.3,897.8 → 1119.9,1028.5` rel=212.4，facing `-0.767,0.642`，bearing=2；C t0=20.051 样本 t=20.14：center `989.2,922.6 → 747.1,700.7` rel=328.4，facing `-0.546,-0.838`，bearing=-14.4）。

**无产物携带 height/slope**，故朝向与地形关系只能部分取证（facing 有、terrain 缺），verdict 中已如实记为 `terrain=absent_depth_data`，未假设等价。

**（d）第二轮整改：门2 判定改为三值，「没发现矛盾」不再等于「已看到停止」。** 复核第 2 项指出门2 在缺少独立订单证据时仍给 PASS。`check-p3-feedback.ps1` 的 `gate2_negative` 改为：

- 区间按**逐对象自身退出写出时刻** `$stopAt` 建立（取该 obj 在 `$exitDone` 中的最小 `at`，缺省 `$invAt`），扫描窗口由 `$invAt+1.0` 延到 `[double]::MaxValue` —— 停止写出之前的读数不再被算作违约；
- 新增 `$unexplainedObjs` / `$evidenceOk`：每条必需对象要有**独立可解释读数**（非 Move/Advance 的常备命令计为可解释）；
- 三值：`ChecksOk` 失败 → **fail**；`$evidenceOk` 为假 → **unverified**；全满足 → **pass**。gate 描述串新增 `independent_stop_evidence=present|incomplete:N`。

第二轮实跑（10 场，无回归）：`p3-P-20261015-221833` → gate2=pass `independent_stop_evidence=present`；`p3-P-20261015-220249`、`-004350` → gate1=fail；`-222821`/`N-004851`/`N-211938`/`N-213231` → 全 unverified；`N-005621` → gate2=fail；`tamper-missing-reassigned-exit`、`tamper-independent-order-contradiction` → gate2=fail。新增两例：`tamper-missing-independent-orders` → **unverified `incomplete:3`**（旧版会误判 pass）；`counterexample-before-exit-within-budget`（133.900 预算内读数）→ **pass**（旧版会误判 fail）。

**（e）第三轮整改（已被第四轮收紧取代）：门2 后续窗口按「合法后继写令」归属。** 第二轮把窗口延到 `MaxValue` 修掉了零预算误判，但坐标不是身份：旧角色点与新计划的普通写令可以合法落在同一点。第三轮引入逐对象 `$successorWrites`，只要阅读时刻之前有非旧 plan、非 `-exit` 的写令即归后继并计入可解释。**该判据过宽——它不核对后继写的是什么命令、什么目标，也不要求独立读数可解释**，第四轮复核据此判回（见（f））。

**（f）第四轮整改：后继写令必须与独立读数相互核对，不能只凭「存在一条后继写令」放行。** 复核 `P3-ROUND3-REVIEW-85e2171-20261016.md` §2 指出：第三轮只要某条写令早于读数就 `$explained++` 并 `continue`，于是「后继写向别处、对象仍奔向旧点」（应 fail）与「后继之后独立订单全缺」（应 unverified）都错误变成 pass。

- **写令记录携带身份与生效区间**：`$successorWrites[$obj]` 每条记 `at / task / geometry / x / y / until`；`until` 取该对象**下一条**写令时刻与该对象**最早 TASK_CLOSED 时刻**中的更早者。**后继退出、失效或被替换后不能沿用早先那条写令**——这正是「一次写令永久通行」的直接封堵。
- **写令目标取法**：先取记录的 `requested` 字段，回退才解析 `geometry`（后继持 `hold` 时 `geometry` 无坐标）。只用 `geometry` 会让后继停在原地却已改持 `hold` 的合法情形被误判为「目标不同」。
- **读数必须与后继目标吻合才算归后继**：仅当覆盖该时刻的后继写令**有目标坐标**且读数坐标与之 `0.5m` 内一致，才 `$explained++`（`$successorExplained`）；若后继写令存在但读数落在**旧角色点**（后继目标不同），记 `$stillAdvancing` + `$successorMismatched` 并 `break`；覆盖写令目标缺失（如 `hold`）而读数仍奔向旧点，同样判矛盾。无覆盖写令时保持原判据。
- **证据门不再只看「没发现矛盾」**：`$evidenceOk = ($unexplainedObjs.Count -eq 0) -and ($pushedObjectsWithFacts -eq $pushedObjs.Count)`，即**每个被推对象都必须有属于它自己的至少一条独立读数**。缺读数的对象使 verdict 落 `unverified`，而不是让「后继写令已存在」把它抬成 pass。
- 逐对象 `$stopAt` 起点、旧 plan 自身停止后继续写令的独立检查（`gate2_no_further_old_role_advance_writes`）、原四例判据均保留。

**归属判定的定向探针**（`Program/_verify/_gate2probe/run-probe.ps1`：按锚点 `        $oldRolePoints = @{}` 到 `        # The 8s post-conversion` 切出检查器里**同一段生产代码**执行，只替换写入记录与独立样本；锚点缺失即 `throw 'production fragment anchor missing'`；注入替身仅 `Num`/`Close`/`Note`/`Check`/`Get-IndependentOrderFacts` 五个纯函数）。七场景实测，同一探针分别指向工作树检查器（`checker_sha256=3DED788E7F2354344A5D1998666ECA8786AD5A0AACF8BCA9961691998EC3635E`）与第三轮 HEAD 版（`git show HEAD:Program/_verify/check-p3-feedback.ps1` 取出，`checker_sha256=16BEE51F5C9FEB6564D9069CF4F21245078B57151EF886F3AC59F5DEDDD3DBB8`；结果 `_gate2probe/gate2-probe-head-results.json`）：

| 场景 | 工作树 | 第三轮 HEAD | 说明 |
|---|---|---|---|
| `successor_write_before_same_coordinate_reading` | False | False | 后继同点普通令，读数为后继所写 —— 不矛盾 |
| `no_successor_write_same_coordinate_reading` | True | True | 无后继写令，读数落旧点 —— 矛盾 |
| `reading_before_successor_write` | True | True | 后继写令晚于读数 —— 矛盾 |
| `successor_exit_write_does_not_reattribute` | True | True | 后继只有 `-exit` 停止任务 —— 不归后继 |
| `successor_target_differs_but_independent_old_move` | **True** | **False（错）** | 后继目标 `(90,30)`、读数仍 `(50,0)` —— 对象仍奔向旧点 |
| `successor_hold_but_independent_old_move` | **True** | **False（错）** | 后继持 `hold`（无目标）、读数仍 `(50,0)` —— 无目标不能解释 |
| `successor_with_only_unreadable_independent_fact` | False | False | 无可读订单、无目标 —— **落 `unverified`**（note：`independent stop evidence missing for required objects`），不是矛盾也不是同意 |

后三行即复核新增的三场景；第三轮实现对其中两例判「无矛盾」（错误），工作树判矛盾。第一、六场景的 note 原文分别为 `gate2 readings explained by the successor's own live order (order target matches readback): object-a@Infantry@137.5=>`（归后继，坐标吻合）与 `gate2 successor order disagreed with the engine readback (successor target differs, object still moving to the abandoned role point): object-a@Infantry@137.5 successor=new-main target=(NaN,NaN) readback=(50,0)`（后继无目标、实测仍奔向旧点）。

**复核的两个完整检查器反例（本轮实测）**：复核用历史场次 `p3-P-20261015-221833` 的完整副本，只改 Main 的独立订单字段，其余（CB2 日志、终态、成员、决定、退出回执）一律不动 ——

| 副本 | 第三轮 HEAD | 工作树（本轮） |
|---|---|---|
| `review-85e2171-20261016/new_order_different_target_old_readback`（`137.034` 后 57 条读数改为 Move 到旧点） | pass（错） | **fail** `still_on_old_order=030dd50e@Infantry@137.564`，note `successor order disagreed … successor=plan-2-task-10 target=(1141.468,582.703) readback=(1147.344,603.044)` |
| `review-85e2171-20261016/new_order_missing_independent_orders`（自身退出后 60 条读数的 movement_order/move_target 全删） | pass（错） | `Check` ok 但 **unverified** `independent stop evidence missing for required objects … 030dd50e@Infantry` |

注：原场 `137.034` 的 Main 后继写令目标 `(1141.468,582.703)` 与旧 Main 角色点 `(1147.344,603.044)` 本就不同坐标 —— 第三轮「同坐标才归后继」的前提在该场并不成立，这也是误报被暴露的直接原因；**坐标是否相等不是判据，后继目标是否与读数一致才是**。

**全量矩阵与 HEAD 对照（第四轮实测）**：对 `sessions/` 下 41 个场次目录 + 复核证据目录下的 8 个反例副本，共 **49 个目录**逐一执行工作树与 HEAD 两版检查器并比对，**只有复核那两个 `new_order_*` 副本取值不同**（HEAD 均 pass → 工作树分别 fail / unverified），其余 47 个取值完全一致。工作树侧取值：`p3-P-20261015-221833` gate2=pass；`p3-N-20261015-005621` gate2=fail；`p3-P-20261015-220249`、`p3-P-20261015-004350` gate1=fail；`review-20261016/tamper-missing-reassigned-exit`、`tamper-independent-order-contradiction`、`_tamper4/A-noHorseArcherExitDone`、`_tamper4/B-samplesStillOldOrder` gate2=fail；`review-732f6df-20261016/tamper-missing-independent-orders` gate2=unverified；`counterexample-before-exit-within-budget` gate2=pass；其余全 unverified。本轮改动未使任何非目标场景改变取值。

### 3.4 R5：完整入口失败路径与 WhatIf（§3.4）

审查退回项 R5 位于 `REVIEW.md:71-80`，受审 `run-p3.ps1:320-377`/`:635-693` 与 `_p3-exclusive-stage.ps1:47-85`。四条：

1. **helper 移入 try**：`Program/_verify/run-p3.ps1:503-505` `try { $script:OwnGabsPid = Open-McpHttp ...; $script:McpOpened = $true }`。注释原文：「The helper must be opened INSIDE the try: it starts gabs before probing the endpoint and throws when the endpoint never becomes ready」。
2. **只按本场 PID 收尾**：全脚本 `Stop-Process` 只剩 1 处（`:975`），`:971-972` 先按 `$script:OwnGamePids -contains $_.Id` 过滤，`:985` 对非本场 PID 打 `Note-Others`。
3. **清理失败独立登记 + 结束实读**：`finally` 内每个 teardown 步骤独立 `try/catch` 并 `$script:CleanupFailures += …`（end_mission / games_stop / Close-McpHttp / crash report / sentinel restore / end-state read-back），末尾逐条 `Fail "cleanup: $cf"`；`Assert-CleanEndState` 实读 `safely_exited`、P1/P2/P3 开关、两探针空、本场残留 PID、helper 残留。
4. **stage 分列「开始尝试」与「实际取得」**：`Program/_verify/_p3-exclusive-stage.ps1:88-89` 改为两行：
```
game stage attempted:  <stage|none>
game stage returned ok: <Boolean>
```

`$gameStage` 在进入该 stage 时即置位（表示本场自此接管游戏生命周期），`$acquiredGame` 只表示该 stage 成功返回。

**第二轮整改补充（复核第 3 项）**：

5. **进程收尾改按实际归属证据限定。** 原实现把启动期间出现的**所有**同名新增 PID 都并入 `$script:OwnGamePids`——若他方进程恰在 `games_start` 调用期间启动就会被误杀。现改为三分支：`no_new_process` / **`launch_result_confirmed_single_new_process`**（唯一新增 **且** `games_start` 自身声明 `processStarted=true` 才归本场）/ **`ambiguous_multiple_new_processes`**（多个同名新增 → 不认领、不杀，记入 `$script:UnownedNewPids`）。归属不明时按 `process ownership unresolved: … ; left running` 登记清理失败，**不强杀未知 PID**。**这一条的三分支口径在第三轮被 5b 收紧、第四轮被 5c 取代**，此处仅记录当轮实现，勿按本条读当前代码。

5b. **第三轮整改：新增进程数不是所有权，`games_start` 的启动声明是唯一可核对的本次身份。** 第二轮仍把「**恰好一个**新同名 PID」直接当本场进程（`OwnGamePids = newPids`），与是否确认启动无关；启动失败而恰有一个他方 PID 出现时，该他方 PID 仍会进结束名单并被杀。复核反例就是删除本场假 PID 777001、只留他方 777999，结果 trace 出现 `stop-process:777999`、end-state 记 `own_game_pids=[777999] basis=single_new_process_during_launch`。
 - 现取条件改为 `$script:LaunchProcessStarted`（解析 `games_start` 返回的 `"processStarted"\s*:\s*true`）**与** `newPids.Count -eq 1` **同时成立**才可认领；两者缺一即降级为 `single_new_process_launch_result_unconfirmed`，PID 进 `$script:UnownedNewPids` 并保留运行。
 - `LaunchProcessStarted` 与「能否归属」解耦：启动调用已声明起进程时 `GameStarted` 仍为真（`games_stop` 等收尾照常走），但不因此获得杀 PID 的权力。
 - **不能仅依赖候选数=1，也不能假设「启动调用期间不会有其他进程出现」。** 要求的是先有可核对的本次身份，才进结束名单；没有就与多个未知 PID 同样登记 unresolved。

5c. **第四轮整改（口径已被第五轮 5d 取代，勿按本条读当前代码）：启动声明 + 差集唯一仍不是身份，PID 必须由游戏进程自己报出。** 第三轮把 `processStarted=true` 与「恰好一个新增 PID」当成所有权，但两者都不给出 PID：`processStarted` 只说明**曾经**起过进程，进程完全可能在启动回执生成后、runner 取后快照前退出，此时另一个同名进程出现，差集仍只有一个 PID——那个他方 PID 于是被判为本场并结束。复核隔离反例（`started-then-exited-foreign-only`，启动回执 `code=started_bridge_pending / processStarted=true / gabpConnected=false`，本场进程在启动侧最后一次存活检查后退出）实测输出 `own game pids: 777999 basis=launch_result_confirmed_single_new_process` → `stop-process:777999`，end-state 记 `own_game_pids=[777999]`。
 - 当轮口径（**已被第五轮 5d 取代**）：把共享目录 `Documents\CommandBrainATH` 下跨启动新出现的 `cbath-runtime-identity-<pid>.txt` 当身份，与差集取交，恰好 1 个即认领。**该目录是共享目录，另一组加载 CBATH 时也会写自己的同类文件，此口径不成立。**
 - 判定读数为 `runtime_identity_file_matched_launch_candidate` / `ambiguous_multiple_identified_processes` / `launched_process_exited(_other_process_present)` / `no_new_process` / `launch_candidate_pending_runtime_identity` / `new_process_without_launch_result`；仅在**没有身份文件**时才回落 `cbath/ping` 的自身 PID。**回落路径不是替代品**：连接未成功时（`startedthenexited`、`neverconnect` 的前半段）它不执行，故不能靠它认领。
 - 未归属时收尾把 `LaunchCandidatePids` 并入 `UnownedNewPids` 一并登记 `process ownership unresolved: … ; left running`；仍只对 `$script:OwnGamePids` 内的 PID 执行 `Stop-Process`。

5d. **第五轮整改：身份文件只证明「它是谁」，不证明「由本场启动」；归属只认本场自己的启动上下文。** 复核反例（`review-1e7b088\sim\foreign-with-cbath-identity.ps1`）：另一组也正常加载 CBATH，也会把自己的 PID 身份文件写进同一个共享的 `Documents\CommandBrainATH`。在本场启动失败、只出现他方 777999 的 `foreignonly` 情形上加入这一**完全正常**的行为后，第四轮实现仍输出 `own_game_pids=[777999] / ownership_basis=runtime_identity_file_matched_launch_candidate`，随后 `stop-process:777999 -> helper_closed`。身份文件命名了 777999，但没有任何证据说 777999 由本场启动。
 - **归属证据只取本场自己的启动上下文**：本场 GABS 实例（`Open-McpHttp -ConfigDir $cfgDir`，`$cfgDir = $here\gabs-config`）自己的 `$here\gabs-config\<gameId>\` 目录。两处来源：①`runtime.json` 的 `gamePid`（GABS 在 fenced spawn-state transition 里写入的 workload PID）；②`launch.log` 内子进程自打印的 `pid=`（该文件由本场 GABS 在 spawn 时 `O_TRUNC` 打开，天然按启动隔离）。新函数 `Get-LaunchContextPids` 取两者并集（`run-p3.ps1:488-512`）。**共享的 `Documents\CommandBrainATH` 身份目录在归属判定中已完全不再使用。**
 - 判定（启动 `finally` 内，不依赖连接，`run-p3.ps1:534-570`）：本场启动上下文跨启动调用新出现的 PID ∩ 新进程差集 → 恰好 1 个则 `OwnGamePids` + `launch_context_pid_matched_new_process`；多个 → `ambiguous_launch_context_pids`（全部保留）；上下文有名但无存活者 → `launched_process_exited` / `launched_process_exited_other_process_present`；无上下文且无新增 → `no_new_process`；无上下文但已声明启动 → `launch_candidate_pending_runtime_identity`；否则 → `new_process_without_launch_result`。
 - `cbath/ping` 的 `pid` 降格为**只给已由上下文点名的 PID 补活性**：连接成功且会话报告的 PID ∈ 本场启动上下文 → `session_runtime_pid_matched_launch_context`；否则不认领，记 `session_runtime_pid_not_in_launch_context`。另一组的游戏回 ping 与我们的完全一样，故 ping 单独不构成归属。ping 阶段会重读一次启动上下文（`run-p3.ps1:647`），使子进程的 `pid=` 恰好落在 `games_start` 返回之后的正常情形也能绑定，不把本来正常的场次误报成未归属清理。
 - **任何拿不到本场绑定的 PID 一律并入 `UnownedNewPids` 并登记 `process ownership unresolved: … ; left running`，绝不进入结束名单**；`Stop-Process` 仍只对 `$script:OwnGamePids` 内的 PID 执行。
6. **`end_mission` 只在确有本场会话归属时执行。** 解析 `games_connect` 结果为 `Discovered|Re-synced|Already connected` 时置 `$script:SessionConnected = $true`；收尾仅在此为真时才发 `end_mission`，否则打印 `end_mission skipped: this run never connected to a game session`。helper HTTP 可用不再被当作已取得连接的证明。
7. **`end-state.json` 新增四项归属记录**（由 `run-p3.ps1` 的 finally 写出，与 `_p3-exclusive-stage.ps1` 打印的 stage record 是两份不同的东西——后者只有 `completed` / `game stage attempted` / `game stage returned ok`）：`ownership_basis`、`launch_process_started`、`unowned_new_pids`、`session_connected`（现 `run-p3.ps1:160-176`），使归属判定可事后核对。
8. **`cbath/get_last_run` 归档失败只在确有本场会话时算失败。** 未连接的模式（`connfail`、`foreignonly`）本就没有 timeline，原实现会把「读取失败」记成清理失败；现仅在 `$script:SessionConnected` 为真时才 `Fail`，其余只打印结果。

**隔离模拟取证**（`Program/_verify/_r5sim/make-sim.ps1`：把真实 `run-p3.ps1` 按锚点正则切片、对 helper/进程表打桩后独立执行；**不触碰任何游戏进程、GABS helper 或共享开关**）。十模式实测（`_r5sim/r5-isolation-results.json`，runner SHA256 `E61FC9466A13263F1733A6841C1E05E9D1232A80392DB8CD2591B5B7E2EE817A`，generator SHA256 `79D671263AC90130C42631CDB7BE520DB0B91391DB20D8C1A8FAB61EDEDAE885`）：

| mode | helper_closed | batch_kill | foreign_pid_stopped | own_pid_stopped | end_mission_sent | failures |
|---|---|---|---|---|---|---|
| ok | True | False | False | True | **True** | `<none>` |
| connfail | True | False | False | False | False | `exception: simulated_endpoint_timeout` |
| neverconnect | True | False | False | True | False | `games_connect never succeeded \| exception: no_connect` |
| forcetook | True | False | False | True | False | `games_connect force-took a session this run did not own \| exception: force_takeover_refused` |
| startthrow | True | False | **False** | **True** | False | `exception: simulated_launch_timeout_after_process` |
| startthrow-other-during-launch | True | False | **False** | **True** | False | `exception: simulated_launch_timeout_after_process` |
| foreignonly（第三轮新增） | True | False | **False** | **False** | False | `exception: simulated_launch_failure_no_process \| cleanup: process ownership unresolved: 777999 … (basis=new_process_without_launch_result); left running` |
| **foreignidentity**（第五轮新增，复核反例） | True | False | **False** | **False** | False | `exception: simulated_launch_failure_no_process \| cleanup: process ownership unresolved: 777999 … (basis=new_process_without_launch_result); left running` |
| **startedthenexited**（第四轮新增） | True | False | **False** | **False** | False | `games_connect never succeeded \| exception: no_connect \| cleanup: process ownership unresolved: 777999 … (basis=launched_process_exited_other_process_present); left running` |
| **fighttodeathoff**（第四轮新增） | True | False | False | **True** | True | `fight_to_death not in force at pre_mission: status=ok patches_installed=True enabled=False \| exception: fight_to_death_off \| fight_to_death was off for 2 of 2 sampled battle seconds` |

十模式全部 `cleanup_reached=True`；`force_takeover_arg_sent` 恒 False；各模式 `end_state` 均为 `p1=enabled=0 p2=enabled=0 p3=enabled=0 probes_empty=True safely_exited=1 residual_own=0 helper_residual=0`。

关键路径：

- `connfail` trace = `helper_process_acquired -> helper_closed` —— **正是 R5 第一条要闭合的「连接失败时 helper 未进 Close-McpHttp」**。
- `ok` trace = `… -> tool:cbath/end_mission -> tool:cbath/get_last_run -> mcp:games_stop -> stop-process:777001 -> helper_closed`，end-state 记 `own_game_pids=[777001] basis=launch_context_pid_matched_new_process`：**认领依据是本场自己的启动上下文（GABS `runtime.json` / `launch.log` 里的 workload PID），不是候选数、也不是共享目录里的身份文件。**
- `end_mission_sent=True` 出现在 `ok` 与 `fighttodeathoff`（后者在 pre_mission 就已判定失败，收尾路径照常走完），其余模式均 False；`end_mission` 已改由实际连接的 Mission 归属决定，helper HTTP 可达不再作为已取得连接的证明。
- **`startthrow` 与 `startthrow-other-during-launch` 现在是 `own_pid_stopped=True`，这是收紧后的正面例**：这两个模式下 777001 确实由本场启动上下文点名且仍在新进程差集内，归属成立，于是正常结束本场 PID（第五轮改用启动上下文后这一正面路径保持不变）。
- **`foreignidentity` 是第五轮的核心反例**（复核反例的原样复现）：本场启动失败、不产生任何进程，他方 777999 出现**并且像任何正常 CBATH 实例一样把自己的身份文件写进共享的 `Documents\CommandBrainATH`**。第四轮实现会把该身份文件当作归属依据并输出 `own_game_pids=[777999] / basis=runtime_identity_file_matched_launch_candidate` + `stop-process:777999`（见下方 HEAD 红态对照）；第五轮改为只认本场启动上下文后，`own_pid_stopped=False`、`foreign_pid_stopped=False`，777999 记 unresolved 保留运行。
- **`startedthenexited` 是第四轮的核心反例，第五轮仍成立**：本场进程真实启动后在取快照前退出，差集只剩他方 777999，`games_connect` 始终失败。结果 `foreign_pid_stopped=False`、`own_pid_stopped=False`，end-state 记 `own_game_pids=[] / unowned_new_pids=[777999] / basis=launched_process_exited_other_process_present`。
- `fighttodeathoff` 在 `pre_mission` 检查点即抛 `fight_to_death_off`，未进入 mission；同时 last-run 归档记 `sample_enabled_false=2`（两个采样秒都读到 `enabled=false`）并对每个关闭的采样秒记失败；根字段与每样本都带 `fight_to_death`，故不触发「状态缺失」那条失败，两条失败信息因此不能混为一条。

**HEAD 红态对照（第五轮实测）**：把 HEAD（`76713ef`）的 `run-p3.ps1` 落盘（`cmd /c "git cat-file blob HEAD:Program/_verify/run-p3.ps1 > <tmp>"`，sha256 `954934B1AE29C694A8123B3998AF129C7EA347D5CE8AA422A3AA847CF7A6081D`），用同一套生成器（仅把 `$src` 与 `$runner` 精确指向该副本）复跑十模式：**`foreignidentity` → `own_pid_stopped=False`、`foreign_pid_stopped=True`，trace 尾为 `stop-process:777999 -> helper_closed`**，即旧版把身份文件当归属并结束了唯一他方 PID；其余九模式取值与修复版一致。即修复版不再复现该误杀。

**stage 三场景模拟**（`Program/_verify/_stagesim/stage-results.json`）：

| case | exit | completed | attempted | returned ok | result |
|---|---|---|---|---|---|
| all-ok | 0 | deploy_cb2, deploy_cbath, collect | collect | True | `RESULT: OK` |
| cb2-fail | 1 | `<none>` | `<none>` | False | `RESULT: FAILED at deploy_cb2` |
| cbath-fail | 1 | deploy_cb2 | `<none>` | False | `RESULT: FAILED at deploy_cbath` |
| collect-fail | 1 | deploy_cb2, deploy_cbath | **collect** | False | `RESULT: FAILED at collect` |

`collect-fail` 行是本条要求的直接证据：「开始尝试 collect」与「collect 成功返回」已分开记录。

**PS 5.1 语法与 WhatIf**：`run-p3.ps1`、`check-p3-feedback.ps1`、`_p3-exclusive-stage.ps1`、`run-p3-exclusive.ps1`、`_r5sim/make-sim.ps1`、`_r5sim/run-isolation.ps1`、`_gate2probe/run-probe.ps1` 经 `[Parser]::ParseFile` 全部 `OK (0 errors)`（PS 5.1.26100.9444）。`run-p3-exclusive.ps1 -Scenario P -WhatIf` → exit 0、输出末行 `RESULT: WHATIF`、前后 Bannerlord 进程数均为 0（无副作用）。

### 3.5 死战不退前置（第四轮新增，依据 `Program/README.md` 由 `d64055d` 加入的要求）

`Program/README.md` 要求：进战斗前与部署结束前核对 `cbath/get_fight_to_death` 的 `status=ok`、`patches_installed=true`、`enabled=true`，并归档 `cbath/get_last_run` 中的同名 `fight_to_death` 记录；缺工具或未开启不得继续测试。本轮把它接进 `run-p3.ps1`：

- `Assert-FightToDeath([string]$stage)`（`run-p3.ps1:303-321`）：调用 `Invoke-Tool 'cbath/get_fight_to_death' 25`，读不到即 `Fail` + `throw 'no_fight_to_death'`；读得到但 `status` ≠ `ok` 或 `patches_installed` 非真或 `enabled` 非真 → `Fail "fight_to_death not in force at ${stage}: status=… patches_installed=… enabled=…"` + `throw 'fight_to_death_off'`。成功时打印 `fight_to_death[$stage]: status=… patches_installed=… enabled=… policy=… mission_active=… panicked=… fleeing=…`。
- 两个检查点：`run-p3.ps1:704` `Assert-FightToDeath 'pre_mission'`（进战斗前）、`:756` `Assert-FightToDeath 'post_deployment'`（部署结束前）。**失败即抛，脚本不会继续进入 mission。**
- `observed_panicked` / `observed_fleeing` 大于 0 时只 `Note-Others` 提示核查，不据此改判——它们是真实回调计数，不是开关状态。
- 归档：`run-p3.ps1:900-933` 从 `cbath/get_last_run` 取根字段与每个 1 Hz 样本的 `fight_to_death`，打印 `fight_to_death[last_run]: root_enabled=… samples=… enabled_true=… enabled_false=… panicked=… fleeing=…`；任一采样秒 `enabled` 为假 → `Fail "fight_to_death was off for N of M sampled battle seconds"`；样本缺 `fight_to_death` 字段 → `Fail "fight_to_death state missing from N of M battle samples"`。
- **未取得任何实机读回**：以上行为只在 `fighttodeathoff` 隔离模式（桩）验证过；真实 `get_fight_to_death` 响应字段名以实机交接后第一次运行为准，若与桩不符需按实际响应修正解析（见 §7 第 4b 条）。

---

## 4. 原合同三门逐门自检

检查器：`Program/_verify/check-p3-feedback.ps1`（85771 B，SHA256 `3DED788E7F2354344A5D1998666ECA8786AD5A0AACF8BCA9961691998EC3635E`，为第四轮整改后读数）。
调用：`powershell.exe -NoProfile -ExecutionPolicy Bypass -File <chk> -SessionDir <绝对路径> [-ControlSessionDir <绝对路径>]`。**`-SessionDir` 必须传绝对路径**，相对路径会拼到工作目录导致 `session dir not found`。退出码 0 只表示检查器跑完，各门状态单列。

| 场次 | 门1 正 | 门2 负 | 门3 对照 |
|---|---|---|---|
| p3-P-20261015-221833 | unverified | **pass** | unverified |
| p3-N-20261015-005621 | unverified | **fail** | unverified |
| p3-N-20261015-213231 | unverified | unverified | unverified |
| p3-N-20261015-211938 | unverified | unverified | unverified |
| p3-N-20261015-004851 | unverified | unverified | unverified |
| p3-P-20261015-220249 | **fail** | unverified | unverified |
| p3-P-20261015-004350 | **fail** | unverified | unverified |
| p3-P-20261015-203136 | unverified | unverified | unverified |
| p3-P-20261015-222821 | unverified | unverified | unverified |
| p3-C-20261015-010928 | unverified | unverified | unverified |
| p3-P-20261017-104721 | unverified | unverified | unverified |
| p3-P-20261017-181046 | unverified | unverified | unverified |
| p3-P-20261017-225827 | unverified | unverified | unverified |

### 门1（正反馈改变后续投入）— **未取得 pass**

原始索引：`p3-P-20261015-004350`（基线旧场）、`p3-P-20261015-220249`（上批新场，build `b20261015-220246-a9c223d9`）。两场失败明细相同：`gate1_commitment_actually_changed old_main_requested= new_main_requested=`、`gate1_new_main_actually_advanced role_point_main_writes=0 proof=`、`gate1_guard_and_support_placed guard_writes=0 support_writes=0`。

**该场门1 的失败是真实的**（不是检查器过严），但它记录的是**旧构建**的行为，不能推广成当前提交的根因。`p3-P-20261015-220249` 逐行：

```
124.703 SUCCESSOR_STANDBY old=plan-1 pending=plan-2 decision=Reinforce tasks=3 deadline=125.703
125.197 SUCCESSOR_EXIT_CONFIRMED waited=0.494 detail=all_exits_applied
125.197 ACTION_REPLAN old=plan-1 new=plan-2 decision=Reinforce tasks=3 standby_waited=1
125.204/126.208/127.218/128.220 COORDINATION_STATE plan=plan-2 phase=AwaitingDependencies
        guard_ready=0 support_ready=0 advance_allowed=0 detail=guard=None/reads=0/ready=0 support=None/reads=0/ready=0
127.708 ACTION_BLOCKED plan=plan-2 reason=loss_not_resolved stopped=1
127.708 WRITE_DELAYED task=plan-2-task-2 / -task-3 reason=predecessor_not_written
128.039 WRITE_EXECUTED task=plan-2-task-1 kind=Hold geometry=hold_stop
128.044 TASK_FEEDBACK task-1 state=effective detail=hold_no_displacement_required
128.539 TASK_FEEDBACK task-1 state=failed reason=CommandOverridden → PLAN_INVALIDATED plan-2
```

`plan-2` 的 Guard(task-2)/Support(task-3) 始终 `candidate_not_submitted`，`COORDINATION_STATE` 全程 `guard=None support=None`，`advance_allowed=0` 因而从未生成推进任务。

**根因是继任计划寿命与投递节奏不足，不是依赖环。** 逐刻实测：125.197 公布 `plan-2` 三任务；该场构建（`b20261015-220247-b67b2b93`）的 `SubmitPending` 只在 3 s 评价点被调用，故 125.204/126.208/127.218 三个 tick 无任何投递日志，首个候选迟至 **127.708** 才提交（`TASK_CANDIDATE_SUBMITTED task=plan-2-task-1`），task-2/task-3 同刻因 `predecessor_not_written` 延后；128.039 task-1（Main Hold）写出；**128.539 task-1 被原生改目标 → `TASK_FEEDBACK state=failed reason=CommandOverridden` → `PLAN_INVALIDATED plan-2`**，task-2/task-3 从未获得提交机会。

`BattleAssessment.cs:212/214` 的依赖方向必须说清：`guardTask.DependsOnTaskId`/`supportTask.DependsOnTaskId` 指向的 `mainTask` 是 `:199` 建立的 **`TaskKind.Hold` 等待任务**（`coordination_main_hold_wait`），**不是**推进任务——推进任务由 `CoordinationTick` 在 `MainAdvanceAllowed` 成立后另行创建。因此依赖链是 `main-hold → guard/support → advance` 的无环 DAG，不存在「`DependsOnTaskId` 与 `MainAdvanceAllowed` 互锁」。旧交回文档（本轮已更正）把 `Hold` 等待任务与后续推进任务混为一条，由此得出的「假想依赖环」不成立。

该失败也不能用来证明当前提交的根因：`732f6df` 把 `SubmitPending` 改为逐 tick 调用（原 125.204 那种 tick 会立刻进入时序门判定），可预期缩短合法等待，但**是否足以让继任计划在被原生覆盖前完成 Guard/Support 落位与主力推进，只有新实机场次能回答**。

对照组证据：**初始** `plan-1` 可走通——`p3-P-20261015-220249` 有 `WRITE_EXECUTED task=plan-1-task-2 geometry=move_to_role_point role=Guard`(7.591)、`task-3 role=Support`(10.641)、`COORDINATION_ADVANCE plan-1-task-4-g1`(18.747)、`WRITE_EXECUTED task-4-g1 role=Main`(23.039，readback_error=0.000)。故写令通路本身可用，问题是继任计划寿命不足。

独立复算：`p3-P-20261015-220249` counts `evaluations=34 freezes=1 replans=1 invalidations=2 writes=15 exit_stop_queued=6 exit_stop_done=6 successor_tasks=3`。

**注意**：该场门1 失败是历史事实，但它是**旧构建**（`b20261015-220247-b67b2b93`）的结果，只能作为「历史没有观察到成功的继任推进」的证据，不能作为 `732f6df` 的根因结论，也不能据此断言逐 tick 投递后仍不会重现。门1 所需的「新保护/辅助关系 + 新主力有限推进」属于派单 §3.1（投递/提案/裁决链）与 §4（新职责与主力推进）的原有要求，`BattleAssessment`、`CoordinationPlan`/`CoordinationDriver` 均在派单 §5 白名单内，**无需另行申请授权**。

### 门2（负反馈调整/终止并停止旧命令）— **取得 pass 与 fail 各一**

- **pass 正例**：`p3-P-20261015-221833`。逐门检查全绿：`gate2_every_old_object_accounted_for assigned=3 stopped=3 stop_failed=0 unaccounted=0`、`gate2_old_push_actually_terminated stopped=3 stop_failed=0 of assigned=3`、`gate2_exit_not_superseded_before_confirmation required=3 taken_over_before_confirmation=0 objects=<none>`、`gate2_no_further_old_role_advance_writes offending_writes_after_stop=0`、`gate2_stopped_objects_not_still_executing_old_order still_on_old_order=<none>`、`gate2_post_conversion_8s_two_points independent_samples_after_8s=292`、`gate2_old_plan_identity_not_revived old_plan_writes_after_8s=0`；`gate2_loss_to_decision_within_budget independent_crossing=133.844 invalidated_at=133.844 latency=0s`；`gate2_loss_stop_records_exception decision=Disengage exception=0 streak=2 at=133.844 note=two_point_gate`（Disengage 是受门的两点决策，只有 StopLoss 才要求 exception 旗标）；`independent_recompute_matches_production mismatching evaluations=0 of 37 (tolerance=0.002)`、`logged_decisions_consistent_with_independent_numbers inconsistent decisions=0`；`independent casualties: DE=53 DF=128 routed=56 unknown=237 foreign=4` 与 mod 上报 `ACTION_TERMINAL: DE=53 DF=128` 一致。
- **fail 反例（预期保留）**：`p3-N-20261015-005621` — 用户 2026-10-15 裁定：它就是修复前缺陷的证据，不能因对象后来停住而认定旧退出接续符合合同。`NOT_APPLIED` 不是停止成功。修正后的工具不再无条件 PASS，四项 FAIL 全部保留。
- **unverified**：`p3-N-20261015-213231`（`push stopped and plan invalidated, but no successor replan (termination only)`）、`p3-N-20261015-211938`、`p3-N-20261015-004851`、`p3-P-20261015-222821`（无冻结）。

### 门3（对照只改变关键反馈）— **未取得 pass（unverified）**

`gate3_arms_differ_only_in_counter_action` 成立（比较 `own_army_size`/`own_composition`/`enemy_army_size`/`enemy_composition`/`p1_enabled`/`p2_enabled`/`p3_enabled`/`competition_inject`/`opponent_delay_sec`/`combat_hold_seconds`；`opponent_control` 被排除，它本身即被比较的差异；`scenario` 排除）。

`gate3_scene_discrete_match` 在 P-220249 / C-220738 上 **ok**（map/army/composition identical）。几何不等，故 verdict 为 **unverified**（不是 fail，也不是 pass）。四组对照的几何差异原始数据：

- P-221833 vs C-220738：`own_center this=822.2,523.9 control=965.8,945.2; enemy_center this=1182,598.3 control=711.8,685.9`
- P-203136 vs C-214157：`own_center this=952.7,628.5 control=1301.8,863.9; enemy_center this=1002.2,946.9 control=1464.4,1128.4`
- P-004350 vs C-010928：`own_center this=1070.3,794.3 control=786.7,531; enemy_center this=759.1,833 control=1095.2,551.3`
- P-220249 vs C-220738：见 §3.3(c)

P/C 两场实际开局部署质心相差数百米，属派单 §4 表已点明的证据缺口。**位置不同本身不能否定等价，但也不能当等价证明**——按派单「不擅自定新数值容差凑 PASS」的要求，维持 unverified。
另：`cbath-timeline.json`、`p2-evidence-*.json`、`formations-early.json` 三份产物**均不含 height/slope 字段**（正则扫描无命中），故地形关系无法证明，已独立记 Note。

### 首个本轮源码实机场（2026-10-17 10:47，P 场景）— **不能作为任何门的证据**

用户裁定「正式资源交回后先跑一个最小开发实测」。2026-10-17 09:53 的窗口确认（`coordination\handoff.md:22`）后，观测方确认未占用、同意主线先跑一场再书面交回。本场是**第一次用本轮源码在真实游戏里运行**，也是第一次由 `run-p3-exclusive.ps1` 走完「部署 CB2 → 部署 CBATH → CBATH 启动/采集 → 结束清理」全链。

**已通过（一手读数）**

- 构建与部署：CB2 `build_id=b20261017-104710-f41051eb`，`source_sha256` = `deployed_sha256` = `51A113C6A0F574781C2F6F96C1DD3B4E1A6AA85CF1FD402DF5B3B3B8246400EE`（196096 B）；CBATH `build_id=b20261017-104719-6f4500f2`，SHA256 `b58ba53840f333c83f1f06c71a09a3572a4183816a9ddadb5a9404a7597e13aa`。`build_identity` 回执确认运行中的进程 pid=35948、`game_version=v1.4.8.119303`、`active_modules` 含 `CombatBrain2` 与 `CombatBrainAutonomousTestHarness`。
- **§3.4 第 5d 条（第五轮进程归属）首次实机成立**：`own game pids: 35948 basis=launch_context_pid_matched_new_process`，`ping ok (runtime pid=35948)`，`game ownership resolved: pids=35948 basis=launch_context_pid_matched_new_process`。即本场 GABS 的 `runtime.json`/`launch.log` 确实能解析出 workload PID，且与游戏自报一致；共享 `Documents\CommandBrainATH` 身份文件未参与判定。**§7 第 5 条担心的「本组 config 目录里没有 runtime.json 样本」在真实启动后不再成立**——文件由 GABS 在 claim/spawn 时写入，clean stop 后删除，故只存在于运行期间。
- **§3.5 死战不退前置首次实机成立**：`fight_to_death[pre_mission]: status=ok patches_installed=True enabled=True policy=fight_to_death_v1 mission_active=False panicked=0 fleeing=0`；`post_deployment` 同（`mission_active=True`）；`get_last_run` 根 `enabled=true`、`samples=181 / enabled_true=181 / enabled_false=0 / max_observed_panicked=0 / max_observed_fleeing=0`。三份归档齐备。
- 清理：`end_mission sent`、`games_stop sent`、`mcp helper closed`、无新 crash 目录、`sentinel switches cleared: p1=0 p2=0 p3=0 probes empty`；事后复核 Bannerlord 0、gabs/gabp/mcp-http 0、锁 FREE、`safely_exited = 1`、两探针空。
- 采集量：`CombatBrain2.log` 259618 B / 1369 行，独立采样 184 点，`cbath-timeline.json` 750190 B，`p2-evidence-*.json` 184 份，场次目录 207 个文件。

**未通过：`RESULT: FAILURES (2)`，`failed stage: collect`**

门检查器在本场给出 `gate1_positive=unverified gate2_negative=unverified gate3_control=unverified`（窗口内 `no reinforce decision` / `no stop-loss or disengage decision` / `no control session supplied`）。**因此本场不能作为三门中任何一门的证据，也不构成 P3 阶段通过的依据。**

两处失败都定位在**主线自己的采集脚本 `Program/_verify/run-p3.ps1`**，不在 CB2 产品代码：

1. `our own main formation not found in engine evidence; counter-action not applied`。根因：`Find-OurMain` 只匹配 `formation_class -match 'Infantry'` 且排除 `Ranged`；本场我方（`is_player_team=$false`）三编队为 Cavalry 52 / Ranged 175 / Cavalry 53，**没有任何 Infantry**（Infantry 59 在敌方 team[0]），故返回 null，`opponent-action.json` 写成 `{"status":"own_main_not_found"}`，控制臂的反制动作从未下发。已改为「优先 Infantry，找不到时用人数最多的任意编队」兜底。
2. `cleanup: end-state read-back: ... shared-game-session.lock ... because it is being used by another process`。根因：`Assert-CleanEndState` 用 `File.ReadAllBytes` 读锁文件，而该锁正由 `Invoke-ExclusiveGame.ps1` 以 `FileShare.None` 持有（本脚本仍在锁内），必然抛 `IOException`。已改为读不到即记 `<held by the exclusive wrapper>`。这是**长期存在、此前从未暴露**的缺陷：历史 `_lastrun-p3-exclusive-*.txt` 均为 `RESULT: OK`，因为此前没有一场真正走到「锁内做 end-state 读回」这条路径。
   两处失败同源：它们都只在「我方无 Infantry 且本场真的跑完」这一组合下才会同时触发。

**红绿验证**（均为离线、用本场真实证据）

- 缺陷 1：用 AST 精确提取生产函数（`[Parser]::ParseFile` + `FindAll(FunctionDefinitionAst)`），喂本场 `p2-evidence-0062.json`。修复版 → `our main: 175 units at (906.361,959.823)`；HEAD 版 `Find-OurMain` → `<null> -> own_main_not_found`。
- 缺陷 2：用 `File.Open(...,'Open','ReadWrite','None')` 按入口同样方式真实持锁后对比。旧行为抛 `Exception calling "ReadAllBytes" ... because it is being used by another process`（与本场失败原文一致）；新行为 `lockText = '<held by the exclusive wrapper>'`，无清理失败；释放后 `lockText = '<file present>'`、锁 FREE。
- 十模式隔离复跑（`Program/_verify/_r5sim/`）：runner SHA256 `BF8F2E4BBC483A7BC97275043B943729B1A642C994C0F1445A36DFDA968FEDF1`，generator SHA256 `79D671263AC90130C42631CDB7BE520DB0B91391DB20D8C1A8FAB61EDEDAE885`，十模式 own/foreign/cleanup/endmission 与第五轮基线完全一致，无回归。
- PS 5.1 七脚本解析 0 错误；`run-p3-exclusive.ps1 -Scenario P -WhatIf` exit 0、前后 Bannerlord 均 0。

**第七轮：反制目标改按冻结的 Main 键定位（定向复核 `p3-review-2a8de3b`）**

第六轮的兜底让「找不到对象」变成「找到了另一个对象」，但没有让反制指向计划真正冻结的 Main。本场证据：

| 项 | 值 |
|---|---|
| 冻结行 | `ACTION_FEEDBACK_FROZEN plan=plan-4 task=plan-4-task-4-g1 main=1/0 t0=52.033 NE=59 NF=280 observe=02229289@Infantry` |
| `main=1/0` 实际指向 | team1/slot0 = `00bec9c9@Formation` **Cavalry 52** @ (911.057, 978.689) |
| 第六轮兜底选中 | `030dd50e@Formation` **Ranged 175** @ (906.361, 959.823)，即我方人数最多者 |
| 两者距离 | **19.442 m** |

整改（只动 `Program/_verify/run-p3.ps1`）：

- `Find-OurMain`（按人数猜）删除，改为 `Find-MainByKey($evidenceObj, $key)`：解析 `<team>/<slot>`，在该队伍内按 `formation_index` 取对象，返回队伍/槽位/`formation_identity`/兵种/人数/坐标；键畸形、队伍或槽位不存在、坐标不可读时返回 `null`，**不退化成任何近似**。
- 新增 `Get-FrozenMainKey($logText)`：从日志里取**最后一条** `ACTION_FEEDBACK_FROZEN ... main=<t>/<s>`（后一次冻结取代前一次），无该行返回空串。
- 触发时重新读取一次键（`applyKey`），把「武装时的键」与「实际使用时的键」都写进记录，避免在中途更换计划后仍瞄向已被取代的 Main。
- 新增 `opponent-target.json`：`{status, frozen_main_key, armed_main_key, team_index, formation_index, identity, class, unit_count, x, y, aimed_by}`。失败时 `opponent-action.json` 记 `{"status":"frozen_main_not_found"}`，不再有 `own_main_not_found` 这种把「我方哪个编队」当成 Main 的写法。
- WhatIf 增一行：`would aim: the formation named by the ACTION_FEEDBACK_FROZEN main=<team>/<slot> key, recorded in opponent-target.json`。

离线复算（`Program/_verify/_aimcheck/`，用生产函数 AST 提取、喂本场 `p2-evidence-0062.json`）：

| 检查 | 结果 |
|---|---|
| 冻结键解析（本场日志） | `1/0` |
| `Find-MainByKey` 命中 | `team=1 slot=0 identity=00bec9c9@Formation class=Cavalry units=52 @ (911.057,978.689)` |
| HEAD 版 `Find-OurMain` 同一证据 | `units=175 @ (906.361,959.823)`，距离 **19.442 m**（与复核一致） |
| 键指向别的槽位 `1/1` | 返回 `Ranged 030dd50e@Formation`（证明按槽位取，不按人数取） |
| 不存在的槽位 `1/9` / 队伍 `9/0` / 空键 / 畸形键 `x` | 一律 `null`，不猜测 |
| 日志无冻结行 | 键为空串，不默认任何值 |
| 两条冻结行（`1/0` 后 `1/1`） | 取后者 `1/1` |

八例全部符合预期（`check-aim-cases.ps1`，exit 0）。改后十模式隔离复跑无回归：runner SHA256 `E813D947BB0015B6CD211A50A9B1011D05BD39A82D6C8A79738B01CFF432633A`，generator SHA256 `79D671263AC90130C42631CDB7BE520DB0B91391DB20D8C1A8FAB61EDEDAE885`，十模式 own/foreign/cleanup/endmission 与第五/六轮基线一致，`batch_kill` 与 `force_takeover` 全为 0。PS 5.1 三脚本解析 0 错误；WhatIf exit 0、前后 Bannerlord 均 0。

**这项改动不承诺任何门通过。** 它只保证下一次实机的反制动作瞄向计划真正冻结的 Main，并且在瞄不到时如实失败。攻击远程编队本身可以是有效反制，但那种场景必须把目标记成远程编队，不能记成 Main。

**CB2 行为观察（参考，不是验收结论）**

- 锚点换算在真实写令中生效，含非零二维偏移。例：`plan-1-task-2` Guard `tactic=(915.280,975.371) anchor=(914.890,968.316) center=(916.005,977.762) requested=(914.165,965.925)`；`plan-4-task-4-g1` Main `tactic=(918.570,959.304) requested=(913.427,967.627)`。
- 全场 4 个 plan、4 次 `PLAN_INVALIDATED`、7 次 `EXIT_STOP_DONE`（8.533/8.583/8.633，全部 `order_after=StandYourGround`）、2 次 `EXIT_STOP_SUPERSEDED`（`object_taken_over_by_new_assignment`）。
- `ACTION_FEEDBACK` 52.553→178.571 共 43 条，全程 `LE=0.000 LF=0.000 U=0.500 decision=Continue`，`exception=0`；`STOP`/`Disengage` 从未触发。早期为 `window_not_mature`，64.556 起为 `no_local_gain`、`rf=0.000`。
- 日志无按帧刷屏：`WRITE_DELAYED` 29 条、`REAL_SELECTION_NOT_SUBMITTED` 64 条。
- **未覆盖**：本场 `RESULT: FAILURES (2)` 意味着控制臂反制动作未下发，故本场**不构成对照臂**，门3 无从评价。

### 第二个实机场（2026-10-17 18:10，P 场景，第七轮整改后）— **`RESULT: OK`，但三门仍全部 unverified**

观测方 2026-10-17 16:29 结束其第三场完整战例并交回。执行前只读核对：Bannerlord/GABS/mcp-http 全 0、锁 FREE（原持锁 pid 11696 已退出）、`safely_exited=1`、两探针空。经 `Invoke-ExclusiveGame.ps1`（Owner=主线组）跑唯一一场，未转 P/N/C 全套。

**运行身份**：CB2 `build_id=b20261017-181039-a691fa85`、`source_sha256 = deployed_sha256 = 0A4DD23CBD06C31041EABDDD459DCB9F6C1E3ABFD31DB4B88D33EFC019C31CF6`（196096 B）；CBATH `build_id=b20261017-181044-83c59fba`、`21a182d1ece43a86fa597712520e15f299249ede0855a95cc5d43489f75f136e`。`own game pids: 56080 basis=launch_context_pid_matched_new_process`，`ping ok (runtime pid=56080)`。死战不退两次前置均 `status=ok patches_installed=True enabled=True`，`get_last_run` 181/181 采样 enabled=true。场次 `p3-P-20261017-181046`（顶层 209 个文件、递归 212 个；`CombatBrain2.log` 287689 B / 1542 行；`cbath-timeline.json` 793426 B；`INDEPENDENT_SAMPLE` 181 条、编号样本 `p2-evidence-NNNN.json` 185 份）。收尾：`end_mission sent`、`games_stop sent`、`mcp helper closed`、无新 crash；事后复核进程全 0、锁 FREE、`safely_exited=1`、三开关 `enabled=0`、探针空。

**控制臂这次真正成立了。** 第六轮的两处缺陷修复后首次跑通：

| 项 | 值 |
|---|---|
| `opponent-target.json` | `status=aimed, frozen_main_key=1/0, armed_main_key=1/0, identity=00bec9c9@Formation, class=Cavalry, unit_count=52, x=860.389, y=823.109` |
| 冻结行 | `ACTION_FEEDBACK_FROZEN plan=plan-2 task=plan-2-task-4-g1 main=1/0 t0=30.548 NE=59 NF=280` |
| 反制对象 | `02229289@Formation` team0/slot0 **Infantry 59**，`center_before=(1080.521,1051.228)` |
| 反制结果 | `order_before=Move order_after=Move`、`move_target_readback=(860.389,823.109) readable=true`、`is_ai_controlled_after=false`、`order_error=""` |

冻结的 Main 是 **Cavalry 52**，不是第七轮之前会误选的 Ranged 175，`armed_main_key` 与 `frozen_main_key` 一致（中途未换计划）。反制对象是 CB2 视角的**敌方**（`BATTLE_STATE my_team=1 enemy_team=0`，team0 `is_player_team=true`）那支 Infantry 59；反制动作把它从 `(1080.521,1051.228)` 以普通 Move 调到冻结 Main 的 `(860.389,823.109)`，也就是**把敌方步兵拉向我方骑兵线**，不是去打远程线。

**反制确实造成接敌**（`cbath-timeline.json` 逐秒独立读数，我方 Main `1/0` 与反制编队 `0/0` 的质心距离；168/181 个样本两侧同时存在）：

```
 t= 34.08  330.2 m   52 人 / 59 人   (Move / Move)
 t= 60.10  238.6 m
 t= 90.10  157.7 m
 t=120.11   60.2 m
 t=140.13   10.1 m   52 人 / 55 人   (StandYourGround / Move)  ← 接敌
 t=147.14    9.2 m   52 人 / 29 人   ← 最近
 t=168.15   24.8 m   52 人 /  1 人
```

最小距离 9.2 m 出现在 t=147.14；反制编队人数 59→55→29→1，我方 Main 全程 52 人未变。

106 条 terminal（`terminal_count=106`、`terminal_overflow=False`）全部落在 `at` 136.466–180.925，即接敌之后；`Killed` 70 / `Deleted` 36，`is_mount` 全为 false。归属按编队：`team0/slot0`（反制那支 Infantry）59、`team0` 无编队 30、`team1/slot1`（我方 Ranged）11、`team1` 无编队 6 —— 合计 team0 89 / team1 17。施加方：`team1/1` 45、`team1/0` 6、`team1/2` 6、`team0/0` 11、无施加者（`-1/-1`）36、`team1/9` 2。**即反制编队在这一战里被打掉大半（59→1），我方主力骑兵 52 人全程未减。**

**CB2 侧行为（参考，不是验收结论）**

- 6 个 plan（`plan-1`…`plan-6`）、6 次 `PLAN_INVALIDATED`、18 次 `EXIT_STOP_DONE`、0 次 `EXIT_STOP_SUPERSEDED`、19 次 `TASK_CANDIDATE_SUBMITTED`、37 次 `WRITE_EXECUTED`、43 次 `WRITE_DELAYED`、103 次 `REAL_SELECTION_NOT_SUBMITTED`。
- 锚点换算继续生效且偏移非零：`plan-2-task-4-g1` Main `tactic=(869.393,840.088) anchor=(852.476,810.144) center=(855.795,819.348) requested=(866.074,830.884)`，`order_target=(866.074,830.884) readback_error=0.000`；`plan-6-task-2` Guard `tactic=(848.112,834.925) requested=(845.009,844.365)`。
- 协同链真实推进过：`plan-2` 从 18.554 起 `guard_ready=1`，29.556 起 `phase=MainAdvancing guard_ready=1 support_ready=1 advance_allowed=1`，维持到 130.661（102 次 `advance_allowed=1`），推进任务 `plan-2-task-4-g1` 于 28.571 建立、30.548 写出。
- **计划寿命仍然很短**：`plan-1` 8.135、`plan-3` 140.087、`plan-4` 149.133、`plan-5` 158.138、`plan-6` 167.134 均以 `NotEffective`（`closed_distance` 未达 3.0 m）失效。
- **`plan-2` 的 131.041 失效是 `lostRole` 误标注（不是 Support 真的丢了）**：日志写 `role=Support failure=None task=plan-2-task-3`，但逐秒独立采样显示 Support `030dd50e@Ranged` 全程在 S\* 0.23 m 内、`order=Move` 未变；真正越界的是 Guard `0162fb49@Cavalry`——131.139 时 `d(G*)=5.079 m`，刚超 5 m 到位半径（129.234 `4.303` / 130.181 `4.486` / 131.139 `5.079`），因此 `DependencyReadiness.Note(..., inPosition=false)` 把 `_guardReadiness` 清零，`MainAdvanceAllowed` 随即为假。原 `lostRole` 只在 `GuardFailure != None` 时取 Guard，否则一律取 Support；本场两侧 `PremiseFailure` 都是 `None`（人数/身份/资格全正常），只是到位窗口断掉，于是被记成 Support 且补救对象也按 Support 的候选排序挑选。**已在第八轮修复（见 §7 第 11 条），本场读数是修复前的行为。**
- `ACTION_FEEDBACK` 只有 **1 次冻结**（`plan-2 t0=30.548 NE=59 NF=280`），35 条评价全为 `LE=0.000 LF=0.000 U=0.500 decision=Continue`，`exception=0`，`STOP`/`Disengage` 从未触发；`ACTION_FEEDBACK_RESET at=136.596 reason=plan_identity_changed`（`plan-2 → plan-3`）。
- `plan-6` 三次写令全部 `first_command_delay` 偏大（1.931 / 3.531 / 5.481 s），且 167.134 立即失效——`plan-6` 的 Guard 写令在 164.134 才落地，写后 3 s 就被判 `closed_distance=2.78`。

**为什么三门仍全部 unverified**

`check-p3-feedback.ps1 -SessionDir p3-P-20261017-181046` → `CHECK RESULT: no failures`，独立复算 `mismatching evaluations=0 of 35`、`inconsistent decisions=0`、terminal 重算 `mod DE=1 DF=0 engine DE=1 DF=0`，**但**：

- `gate1_positive: unverified -- no reinforce decision in this window`：冻结窗口内从未出现 reinforce。
- `gate2_negative: unverified -- no stop-loss or disengage decision in this window`：`LE`/`LF` 在 35 条评价里全部为 0，止损条件从未触发过（也谈不上漏触发）。
- `gate3_control: unverified -- no control session supplied`：本场只有 P 臂，检查器没有对照组可比。反制动作成立 ≠ 对照臂成立，门3 需要 P 与 C 在同一场次规则下配对。

**因此：本场仍不构成任何门的通过，P3 不得阶段收口。** 它的价值是把「反制到底有没有打到冻结的 Main」从猜测变成了实测：之前无法判断，现在能证明 19 m 的偏差已被消除，且反制真的带来了接敌与伤亡。剩下的缺口是战术层面的——CB2 在接敌后没有产生正/负反馈决定，且计划在 3 s 内以 `closed_distance` 未达 3.0 m 连续失效。

### 第三个实机场（2026-10-17 22:58，P 场景，第八轮 `lostRole` 修复后）— **`RESULT: OK`，但归因分支未被触发、三门仍全部 unverified**

观测方 2026-10-17 22:28 正式交回（`coordination\handoff.md` 该条），明确只交给主线一次 `run-p3-exclusive.ps1 -Scenario P`。执行前只读核对：Bannerlord/GABS/mcp-http 全 0、锁 FREE、`safely_exited=1`、P1/P2/P3 均 `enabled=0`、两探针空。经 `Invoke-ExclusiveGame.ps1`（Owner=主线组）跑唯一一场，未扩 P/N/C。

**运行身份**：CB2 `build_id=b20261017-225822-2c279a81`、`source_sha256 = deployed_sha256 = A54BD145EA90DB9FE708E96F9F06D8B368BE13C8BF770D17087F157D9B62CA7D`（197120 B）；CBATH `build_id=b20261017-225826-4091f839`、`e89477deae806a862d38fc1e77919bf09fb1c4ff74ae269a952317efef46a984`；mcp helper `DF1A86CF0719F5B3938F06B29884C327F8CE8ED30B4FC42EB22D6B6C08BE33EA`。`own game pids: 8644 basis=launch_context_pid_matched_new_process`、`own_gabs_pid=17412`、`games_connect` 返回 `Already connected ... Re-synced 21 tools`、`ping ok (runtime pid=8644)`。死战不退两次前置均 `status=ok patches_installed=True enabled=True`，`get_last_run` `root_enabled=True samples=181 enabled_true=181 enabled_false=0`。场次 `p3-P-20261017-225827`（顶层 213 文件、递归 216；`CombatBrain2.log` 239028 B / 1214 行；独立采样 188 点；编号样本 188 份；`cbath-timeline.json` sample_count=181；mission `battle_terrain_biome_096`、`mission_time=180.970`）。收尾 `end_mission sent` / `games_stop sent` / `mcp helper closed` / 无新 crash / `sentinel switches cleared: p1=0 p2=0 p3=0 probes empty`；场后实测进程全 0、锁 FREE、`safely_exited=1`、三开关 `enabled=0`、探针空，`end-state.json` 的 `cleanup_failures=[]`。

**按用户裁定的三条口径核对（重点）**

| 口径 | 本场结果 |
|---|---|
| ①实际失效侧与 `role=`/`loss=`/`task=` 一致 | **无样本可比对**：全场 `COORDINATION_DEPENDENCY_LOST=0`。`plan-1` 是 `NotEffective`（`closed_distance` 未达 3.0 m，与依赖无关）；`plan-2` 唯一失效是 180.990 的 `reason=MissionEnded`。归因分支从未进入，不能判通过，也不能判失败。 |
| ②健康职责未被选成替换对象 | **无样本可比对**：本场没有 `replacement=` 出现（无依赖丢失即无替补生成）。 |
| ③接敌与伤亡是否落在有效计划存续期间 | **无判定对象**：`cbath-timeline.json` `terminal_count=0`，双方全程 `unit_count` 不变（我方 Cavalry 52 / 敌方 Infantry 59）；我方 `1/0` 与敌方 `0/0` 的质心距离 1.039 → **428.6 m**，5.049 → 400.0 m（最近的一次接近），随后缓慢降到 180.129 → **214.6 m**（全程最小）。**181 s 内未接敌、零伤亡。** |

**第八轮新增字段已在实机生效**：`COORDINATION_STATE` 178 条中，`advance_allowed=1` 有 156 条；`guard_loss` 分布 `None=160 / CommandInvalid=13 / NotReady=5`，`support_loss` 分布 `None=156 / CommandInvalid=15 / NotReady=7`。非 `None` 的 22 条**全部落在 `phase=AwaitingDependencies` 且 `advance_allowed=0`**：1.539–15.548 两侧均 `CommandInvalid`（尚未写令，符合 `CommandStillValid` 的 `WrittenAt < 0` 判据）；16.548–17.548 `guard_loss=NotReady support_loss=CommandInvalid`；18.548–20.549 两侧 `NotReady`；21.549–24.550 `guard_loss=None support_loss=NotReady`；25.395 起两侧均 `None`，直至 180.619 仍 `advance_allowed=1`。即该字段在真实运行中给出「已写令 / 未到位 / 命令是否仍成立」的区分，但**没有一次走到依赖丢失的日志分支**。

**CB2 侧行为（参考，不是验收结论）**

- 2 个 plan、2 次 `PLAN_INVALIDATED`、`EXIT_STOP_DONE=3`、`EXIT_STOP_SUPERSEDED=0`、`EXIT_STOP_NOT_APPLIED=0`、`TASK_CANDIDATE_SUBMITTED=7`、`WRITE_EXECUTED=10`、`WRITE_DELAYED=15`、`COORDINATION_ADVANCE=1`、`ACTION_FEEDBACK_RESET=0`。
- `plan-1` 1.539 建立（`ranged_edge=0.625`、`strength_ratio=4.746`），8.134 以 `NotEffective detail=task=plan-1-task-2 detail=closed_distance=0.34 (<3.0) distance=6.61 from=6.95 since_write=3.000` 失效；三条 exit 在 8.534/8.584/8.634 全部 `hold_stop`。
- `plan-2` 10.544 建立并**存活到任务结束**（180.990 `reason=MissionEnded`）。`COORDINATION_ADVANCE task=plan-2-task-4-g1 ... replaced=|plan-2-task-2|plan-2-task-3| at=25.395`，推进任务 27.035 写出（`first_command_delay=1.640`，`obj=014623e9@Infantry`，`move_to_role_point`）。
- 首令延迟仍按 1.5 s stagger 递增：每个 plan 的 Main Hold ≈1.99 s、Guard ≈3.59 s、Support ≈5.54 s。
- `ACTION_FEEDBACK` 51 条，`decision=Continue` **51/51**，`LE=0.000 LF=0.000 U=0.500 decision=Continue`，早期 `window_not_mature`、后期 `no_local_gain`，`exception=0`；**无 reinforce、无止损、无撤出**。
- 控制臂成立：`ACTION_FEEDBACK_FROZEN plan=plan-2 task=plan-2-task-4-g1 main=1/0 t0=27.035 NE=59 NF=280 observe=0244575d@Infantry`；反制令于 34.099 下发到敌方 `0244575d@Infantry`（team0/slot0，59 人），`center_before=(904.050,541.962)`、`move_target_readback=(996.964,942.341) readable=true`、与请求一致、`order_error=""`、`status=ok`。

**为什么三门仍全部 unverified**：`check-p3-feedback.ps1 -SessionDir p3-P-20261017-225827` → `CHECK RESULT: no failures`、`independent_recompute_matches_production mismatching evaluations=0 of 51 (tolerance=0.002)`、`mod_and_engine_terminal_counts_agree mod DE=0 DF=0 engine DE=0 DF=0`，但 `gate1_positive=unverified`（无 reinforce）、`gate2_negative=unverified`（无止损/撤出决定）、`gate3_control=unverified`（无对照会话）。**本场不构成任何门的证据，反而说明「反制成功 ≠ 接敌」：本场反制把敌方步兵调向我方骑兵线，但 181 s 内最近只到 214.6 m，零伤亡。**

**本场对第八轮修复的意义**：它证明 `guard_loss=`/`support_loss=` 能在实机正确区分「未写令 / 未到位 / 已就位」，并证明 `plan-2` 在一整场里没有再误报依赖丢失；但**归因分支本身未被触发**，因此不能据此宣称 `lostRole` 归因在实机正确，仍需一个真正出现依赖丢失的场次（第二场那样的条件本场没有复现）。

---

### 第四个实机场（2026-10-18 00:17，P 场景，`CombatHoldSec=420`）— **`RESULT: OK`，完成接敌与歼灭且补足长窗口证据，但全部发生在无有效计划期间**

用户 m05791 确认交回。本场是按用户裁定的**定向开发实测**：把采集窗口从 180 s 提到 420 s，其余配置不变（未启用 BattleSizeResized、未调整 `OpponentDelaySec`、死战不退全程开启）。执行前只读核对：Bannerlord/GABS/mcp-http 全 0、锁 FREE、`safely_exited=1`、P1/P2/P3 均 `enabled=0`、两探针空、CB2 HEAD `11fb697`、CBATH HEAD `ac7c4ae`（dirty 0）。

**运行身份**：CB2 `build_id=b20261018-001725-aae2c73c`、`source_sha256 = deployed_sha256 = 360CA33FC545C043C1376F18BF55B43FD6655F72E9737B24254816C1A939EFCC`（197120 B）；CBATH `build_id=b20261018-001729-315ca01a`、`a904eab55c4a152e749d76339a2f5dc6c49b73638b9b96f06abd121f5949b779`；mcp helper `DF1A86CF0719F5B3938F06B29884C327F8CE8ED30B4FC42EB22D6B6C08BE33EA`。`own game pids: 16684 basis=launch_context_pid_matched_new_process`、`own_gabs_pid=44776`、`games_connect` 返回 `Already connected ... Re-synced 21 tools`、`ping ok (runtime pid=16684)`。死战不退两次前置均 `status=ok patches_installed=True enabled=True`，`get_last_run` `root_enabled=True samples=421 enabled_true=421 enabled_false=0`。场次 `p3-P-20261018-001730`（顶层 464 文件、递归 467；`CombatBrain2.log` 733286 B / 3838 行；独立采样 440 点；编号样本 440 份；`cbath-timeline.json` sample_count=421；mission `battle_terrain_biome_096`、`mission_time=420.768`）。收尾 `end_mission sent` / `games_stop sent` / `mcp helper closed` / 无新 crash / `sentinel switches cleared: p1=0 p2=0 p3=0 probes empty`；场后进程全 0、锁 FREE、`safely_exited=1`、三开关 `enabled=0`、探针空，`cleanup_failures=[]`。

**按用户裁定的三条口径核对**

| 口径 | 本场结果 |
|---|---|
| ①实际失效侧与 `role=`/`loss=`/`task=` 一致 | **仍无样本可比对**：`COORDINATION_DEPENDENCY_LOST=0`（第二、三、四场连续三场都未触发）。`lostRole` 归因修复 (`9958767`) 的实机分支第三次未被覆盖。 |
| ②健康职责未被选成替换对象 | **无样本可比对**：全场没有 `replacement=` 出现。 |
| ③接敌与伤亡是否落在有效计划存续期间 | **有样本，且是明确的负面结论**：接敌与歼灭发生在 **317.227–404.190 s**，但同期 CB2 处于 `phase=AwaitingDependencies advance_allowed=0`，最后一次 `ACTION_FEEDBACK` 在 265.625 之后完全停止。**两个时间窗不重叠**——歼灭不是由任何有效计划推动的。 |

**接敌与伤亡（本场第一条真正有读数的口径）**

- `cbath-timeline.json`：`terminal_count=118`、`overflow=False`、`listed=118`；`Killed=59 / Deleted=59`，`at` 317.227–404.190。
- 受害者**全部是敌方 `team0/slot0` `0079c36c@Formation`**：`team0/0079c36c@Formation=59` + `team0/<none>=59`。**我方 team1 零阵亡**（引擎与 mod 两侧一致：`mod_and_engine_terminal_counts_agree mod DE=0 DF=0 engine DE=0 DF=0`）。
- 施加方：`team-1/slot-1=59`（无归属）、`team1/slot1=42`、`team1/slot9=9`、`team1/slot0=4`、`team1/slot2=3`、`team1/slot8=1`。
- 我方 `1/0` 与敌方 `0/0` 质心距离（`cbath-timeline.json` 的 `position`，队伍字段 `team_id`）：t=1.0 → **418.5 m**；t=4–9 → 387.6 m；t=61 → 416.1 m；t=169 → 265.1 m；t=190 → 233.8 m；t=230 → 173.1 m；t=260 → 102.8 m；t=290 → **69.2 m（局部最小）**；t=317.3 → 62.7 m（敌方开始掉人）；t=348.3 → 30.0 m；t=357.3 → 17.7 m；t=368.3 → **10.4 m（敌方剩 1）**；t=369.3 → 9.8 m。我方 `unit_count` **全程 52 不变**。

**CB2 侧行为（参考，不是验收结论）**

- `PLAN_CREATED=19`、`PLAN_INVALIDATED=19`、`COORDINATION_ADVANCE=2`、`EXIT_STOP_QUEUED=57`、`EXIT_STOP_DONE=49`、`EXIT_STOP_SUPERSEDED=2`、**`EXIT_STOP_NOT_APPLIED=6`**、`TASK_CANDIDATE_SUBMITTED=55`、`WRITE_EXECUTED=103`、`WRITE_DELAYED=132`（`first_command_delay` 59 / `predecessor_not_written` 38 / `formation_stagger` 35）、`WRITE_REJECTED=0`、`REAL_SELECTION_NOT_SUBMITTED=323`、`ACTION_FEEDBACK_FROZEN=2`、`ACTION_FEEDBACK_RESET=2`、`ACTION_FEEDBACK=66`、**`ACTION_TERMINAL=0`**、`COORDINATION_STATE=352`。
- 计划寿命：`plan-1` 1.532→8.135（`closed_distance=-11.99`）；`plan-2` 10.536→**34.139 `relationship_not_established guard_deadline=34.136 support_deadline=36.086 guard_ready=0 support_ready=1`**；`plan-3` 34.544→41.137；`plan-4` 43.547→50.137（**`order_target_readback_error=7.15 (>0.5) order_target=(1152.772,816.121) requested=(1146.872,820.156)`**）；`plan-5` 52.553→168.153（存活 115.6 s，第一个进入 `MainAdvancing`）；`plan-6` 169.590→176.136；`plan-7` 178.592→259.674（存活 81.1 s，第二个 `MainAdvancing`）；`plan-8` 262.624→**286.136 `relationship_not_established guard_ready=0 support_ready=0`**；`plan-9`..`plan-17` 每个存活 6.5–7.5 s（`closed_distance` 1.1–2.0 m 未达 3.0）；`plan-18` 367.709→**370.538 `CommandOverridden detail=order_now=Charge order_after=StandYourGround`**；`plan-19` 370.713→**382.217 `SelectionTimeout detail=task=plan-19-task-1 selection_timeout`**。
- `COORDINATION_ADVANCE` 只有两条：`plan-5-task-4-g1 at=60.170`、`plan-7-task-4-g1 at=186.237`，均 `replaced=|plan-N-task-2|plan-N-task-3|`。
- **260 s 之后再未建立协同关系**：352 条 `COORDINATION_STATE` 中 `MainAdvancing=182 / AwaitingDependencies=170`，但 `at>=260` 的 102 条**全部** `AwaitingDependencies`、`advance_allowed=0`；`at>=260` 段 `guard_loss` = CommandInvalid 83 / NotReady 19、`support_loss` = CommandInvalid 86 / NotReady 16、`support_ready` 全 0。
- `ACTION_FEEDBACK_FROZEN` 仅两窗口：`plan-5 task=plan-5-task-4-g1 main=1/0 t0=62.035`（→172.591 RESET）与 `plan-7 task=plan-7-task-4-g1 t0=188.035`（→265.625 RESET），两次 `reason=plan_identity_changed`。61 条评价全 `LE=0.000 LF=0.000 U=0.500 decision=Continue`（早期 `window_not_mature`，其余 `no_local_gain`）；**`now>=317` 的 `ACTION_FEEDBACK` 条数为 0** —— 整个接敌/歼灭过程没有任何动作评价。
- 控制臂：`opponent-target.json` = `{"status":"aimed","frozen_main_key":"1/0","armed_main_key":"1/0","team_index":1,"formation_index":0,"identity":"01a0a6ed@Formation","class":"Cavalry","unit_count":52,"x":1185.585,"y":828.572,"aimed_by":"action_feedback_frozen_main_key"}`；反制令于 69.114 下发至敌方 `0079c36c@Formation`（team0/slot0，59 人），`center_before=(903.680,542.081)`、`move_target_readback=(1185.585,828.572) readable=true`、`order_error=""`、`status=ok`。`opponent-release.json` **未生成**（420 s 后敌方 `team0 formations=0 units=0`，`Find-OpponentInfantry` 返回空），属采集脚本的无害空结果。

**本场新暴露的四个现象（均未改代码，留待定向下轮）**

1. **`EXIT_STOP_NOT_APPLIED=6`（安全相关）**：371.542 三条（`ownership=taken_over`，`plan-18`）与 383.218 三条（`ownership=own`，`plan-19`），全部 `submitted=1 selected=0 waited≈1.0 reason=not_completed_in_time`。同期原生切入 `ChargeToTarget` / `Charge`（`our_write=0 source=not_ours`），随后 `BEHAVIOR_DISARMED reason=ExitNotApplied`。即敌方全灭后原生行为接管，CB2 的停止令未获选、未生效，只能撤武装。
2. **`ACTION_TERMINAL=0` 而引擎有 118 次终结**：动作评价只在两个窗口内运行（62–172、188–265），接敌与歼灭全程无评价。这是评价窗口寿命问题，不是数据丢失。
3. **260 s 后协同关系再未建立**：`plan-8`..`plan-19` 共 12 个计划全部停在 `AwaitingDependencies`。`plan-8` 的 Guard/Support 令已在 266.135 / 268.082 写出且 `readback_error=0.000`，仍在 286.136 以 `guard_ready=0 support_ready=0` 超时；`plan-9` 到 292.647 时 `guard_ready=1`（`reads=931`）但 `support_ready=0`（`reads=0`）。**具体原因尚未定位**。
4. **锚点与质心在实战中可相距很远**：`plan-7-task-2` Guard `|anchor-center| = 19.1 m`、`plan-8-task-2` = 18.3 m、`plan-4-task-2` = 17.2 m（Support 多在 2.1 m，`plan-9` 之后升到 9.9 m）。`AnchoredRequest` 的方向与量值均正确，但说明「引擎锚点」在实战里可以是与部队质心完全不同的点，换算后的写入点会离战术目标 18 m 以上。

**为什么三门仍全部 unverified**：`check-p3-feedback.ps1 -SessionDir p3-P-20261018-001730` → `CHECK RESULT: no failures`、`independent_recompute_matches_production mismatching evaluations=0 of 61 (tolerance=0.002)`、`logged_decisions_consistent_with_independent_numbers inconsistent decisions=0`、`mod_and_engine_terminal_counts_agree mod DE=0 DF=0 engine DE=0 DF=0`，但 `gate1_positive=unverified`（窗口内无 reinforce）、`gate2_negative=unverified`（无止损/撤出决定）、`gate3_control=unverified`（无对照会话）。**本场不构成任何门的证据。**

**本场对后续开发的意义**：420 s 窗口达成本轮目标——第一次真正接敌并歼灭，也第一次把「CB2 在接敌期完全不参与评价」这一空白暴露成可测事实。它同时说明：单靠延长窗口不会自动让计划存活到接敌（`plan-5` 与 `plan-7` 各有一个 `MainAdvancing` 窗口，但都在接敌前被 `NotEffective` 关闭）。下一步应定向处理上述第 1 与第 3 项，而不是继续刷场。

---

## 5. 入口、WhatIf 与复算工具用法

### 5.1 唯一完整入口

```
E:\MBmod\BattleObservation-V1\coordination\Invoke-ExclusiveGame.ps1
  -Owner '主线组' -ScriptPath <_p3-exclusive-stage.ps1> -ScriptParameters <hashtable>
```

`Invoke-ExclusiveGame.ps1` 用 `[System.IO.File]::Open($lock, OpenOrCreate, ReadWrite, None)` 持锁 → 写 metadata `{owner,pid,script,started_utc}` → 设 `$env:MBMOD_SHARED_GAME_PID` → `& $resolvedScript @ScriptParameters` → 非零退出抛异常；`finally` 只释放自己的 lease，**不做任何进程清理**。

推荐包裹脚本：`Program/_verify/run-p3-exclusive.ps1`（自动填 Owner 与三个 stage 路径）。

```
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Program\_verify\run-p3-exclusive.ps1 `
  -Scenario P|N|C -CombatHoldSec 180 -OpponentDelaySec 6 [-WhatIf]
```

`-WhatIf` 实测：打印场景、协调路径、owner=`主线组`、只读身份项、三 stage 路径与参数，标注不写 DLL / 不写配置 / 不写哨兵 / 不启动游戏，输出 `RESULT: WHATIF`，exit 0。

stages：`deploy_cb2` = `Program\_deploy\deploy.ps1`（已有 Bannerlord 进程则 exit 2 拒绝；删 `obj`/`bin` 强制重建 Debug）；`deploy_cbath` = `E:\MBmod\CBATH-P2\RD\...\CBATH\build-deploy.ps1`；`collect` = `Program\_verify\run-p3.ps1`（~1 Hz 采样 `cbath/get_p2_formation_evidence`，写 `p1-command-loop.txt=enabled=0` / `p2-command-loop.txt=enabled=1` / `p3-feedback.txt=enabled=1`，清空两探针，跑 `h3_provider_start → h3_start_from_screen → auto_finish_deployment → delegate_team_to_ai`）。

### 5.2 场景配置（当前 `run-p3.ps1`）

| 场景 | 己方 | 敌方 | 对手反制 |
|---|---|---|---|
| P | 300 `159,106,35,0` | 60 `100,0,0,0` | 开（冻结后延迟 6s） |
| N | 100 `30,55,15,0` | 130 `95,25,10,0` | 开 |
| C | 300 `159,106,35,0` | 60 `100,0,0,0` | 关 |

P/C 均已修正为敌方 `100,0,0,0`（原先 `50,50,0,0` 会使 `ranged_edge` 为负，协同计划无法成立——该首跑失败根因见 §7）。各场景实参含 `-Scenario <X> -CombatHoldSec <n> -PollTimeoutSec <n> -LaunchWaitSec 900 -McpPort <n> -GameId bannerlord148-cb2 -OpponentDelaySec <n>`。

### 5.3 复算工具

```
powershell.exe -NoProfile -ExecutionPolicy Bypass -File Program\_verify\check-p3-feedback.ps1 `
  -SessionDir <绝对路径 sessions\p3-X-...> [-ControlSessionDir <绝对路径 sessions\p3-C-...>] [-OutFile <json>]
```

读 `CombatBrain2.log` 表示「决定」，读 CBATH `p2-evidence-*.json` 表示「战场」，不把生产分数当真值；输出 `GATES:` 行与 `<SessionDir>\p3-offline-check.json`。**注意**：对照必须传另一个场次目录，把 C 场与自身比较必然使 `gate3_arms_differ_only_in_counter_action` FAIL。

**环境约束**：本机无 `pwsh` 命令，必须用 `C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe`。

### 5.4 隔离取证脚手架（本轮新增，未提交，可复跑）

```
Program\_verify\_r5sim\make-sim.ps1    由真实 run-p3.ps1 按锚点切片生成 sim-<mode>.ps1
Program\_verify\_r5sim\r5-isolation-results.json   十模式结论
Program\_verify\_r5sim\run-isolation.ps1           一次跑完全部模式并写结果 JSON
Program\_verify\_gate2probe\run-probe.ps1          门2 后续归属判定的定向探针（切出检查器同一段生产代码）
Program\_verify\_gate2probe\gate2-probe-results.json   七场景结论（含指向 HEAD 版检查器的对照）
Program\_verify\_aimcheck\check-aim.ps1            反制目标定位的复算（生产函数 AST 提取 + 本场证据）
Program\_verify\_aimcheck\check-aim-cases.ps1      反制目标定位的八例边界（按槽位取、缺失不猜、后冻结取代前冻结）
Program\_verify\_aimcheck\aim-result.json          本场复算结果（含与 HEAD 版 19.442 m 的对照）
Program\_verify\_stagesim\              stage 替身与 stage-results.json
Program\_verify\_tamper4\               审查副本（38.8 MB，门2 缺证据敏感性）
```

十模式：`ok`、`connfail`、`neverconnect`、`forcetook`、`startthrow`、`startthrow-other-during-launch`、`foreignonly`（第三轮）、`startedthenexited`、`fighttodeathoff`（第四轮）、`foreignidentity`（第五轮，复核反例原样复现）。

运行隔离模拟须缩短等待窗口（如 `-LaunchWaitSec 6 -PollTimeoutSec 6`），否则桩的模拟时钟会刷出数百行 `connect attempt`。`run-isolation.ps1` 每次运行前会重建 sandbox（含共享身份目录与本场 config 目录两个），避免上一模式的残留文件被当成「启动前已存在」。

### 5.5 进程归属判定的依据与边界（第五轮口径）

收尾可强制结束的名单**只认同本场自己的启动上下文**：本场 GABS 实例（`$here\gabs-config\<gameId>\`）在 spawn 时写下的 workload PID。

| 来源 | 位置 | 强度 |
|---|---|---|
| 本场 GABS `runtime.json` 的 `gamePid` | `$here\gabs-config\<gameId>\runtime.json`，本场 GABS 在 fenced spawn transition 里写入 | **强：只有本场启动才有这一条记录** |
| 本场 GABS `launch.log` 里的 `pid=` | 同上目录，文件在 spawn 时 `O_TRUNC` 打开，子进程自打印 | **强：文件按启动隔离** |
| `cbath/ping` 的 `pid` | `CbathTools.cs` 的 `Ping()`，仅在会话连上后可用 | 中：只给上下文已点名的 PID 补活性；他方游戏回 ping 与己方无异 |
| `Documents\CommandBrainATH\cbath-runtime-identity-<pid>.txt` | 共享目录，任何 CBATH 实例都会写自己的 | **无效：命名了 PID，但不证明由本场启动（第五轮退回）** |
| `games_start` 的 `processStarted` | 启动回执，**不含 pid** | 弱：只证明「曾启动过某个进程」 |
| 进程快照差集 | 启动调用前后各取一次 | 弱：只证明「有同名新进程出现」，不证明是谁的 |

判定只用**本场启动上下文 ∩ 启动差集**；ping 只能给已点名的 PID 补活性，单独的弱来源都不足以进入结束名单（第三轮曾用「`processStarted=true` + 恰好一个新增 PID」，第四轮曾用共享身份文件，均被退回）。拿不到本场绑定时一律登记 `process ownership unresolved` 并保留进程运行，如实记为清理失败，不猜、不强杀。

对应反例（`startedthenexited`）：本场进程真实启动、身份文件已写出，随后在 diff 快照前退出，此时差集里只剩他方进程——旧实现会把它当本场进程结束。

### 5.6 共享资源交接与清理实读

- 本轮接手时：**无 Bannerlord 进程**；`coordination\handoff.md` mtime 仍为 **2026-10-12 13:36:43**，**无本轮正式交回记录**；`shared-game-session.lock`（221 B）mtime 2026-10-16 0:36:13。按派单 L137「旧锁 JSON 不是占用证明，也不是空闲交接证明」，判定无正式交回。
- 因此本会话**只做离线工作**：未部署、未启动/结束游戏、未改共享开关、未取锁、未运行任何实机场次。`_verify\sessions\` 目录中不存在创建于接手之后的新场次。
- 本组最后一次独占运行（`_lastrun-p3-exclusive-N2.txt`，21:40:02）结束状态：`completed: deploy_cb2, deploy_cbath, collect` / `RESULT: OK`；该次收尾已实读复位 `p1/p2/p3` 全为 0、两探针空、无本组残留进程、崩溃目录为空。
- 上一批交回记录（`P3-IMPLEMENTATION-HANDOVER.md` 旧版 §4.4）记载：截至 2026-10-16 00:39 共享资源由观测组占用。**本轮未复核该项是否已释放**，因为本单明确不要求为离线工作等交接，且未取得正式交回。

---

## 6. 逐场转换时序与独立复算

`p3-P-20261015-221833`（门2 正例，本轮实测复核）：

```
133.844 PLAN_INVALIDATED plan=plan-1 reason=NotEffective detail=action_feedback disengage
133.844 3× EXIT_STOP_QUEUED (plan=plan-1, kind=Hold)
133.844 SUCCESSOR_STANDBY old=plan-1 pending=plan-2 decision=Disengage tasks=3 deadline=134.844
134.036 EXIT_STOP_DONE plan-1-task-92-1-exit        obj=030dd50e@Infantry   order_before=StandYourGround
134.086 EXIT_STOP_DONE plan-1-task-92-r2-support-exit obj=038572bf@Ranged   order_before=Move
134.182 EXIT_STOP_DONE plan-1-task-92-r2-guard-exit  obj=008c0c1e@HorseArcher order_before=Move
134.187 SUCCESSOR_EXIT_CONFIRMED pending=plan-2 waited=0.344 detail=all_exits_applied
134.187 ACTION_REPLAN old=plan-1 new=plan-2 decision=Disengage tasks=3 standby_waited=1
```

三个必需对象全部在 `deadline=134.844` 之前实际写出，确认后才公布 plan-2；`taken_over_before_confirmation=0`；`withdraw: arrived=0 hold_created=0 failed_or_deadline=0`（撤回 3 秒效果未成功，未写成撤回成功）。

`ASSIGNMENT_PUBLISHED` 依据：`plan-1-task-92-r2-support` obj=`038572bf@Ranged` @125.090；`plan-1-task-92-r2-guard` obj=`008c0c1e@HorseArcher` @125.090；`plan-1-task-92-1` role=Main obj=`030dd50e@Infantry` @125.090；plan-2 三条 role=None @134.187。

---

## 7. 失败与未覆盖项

1. **门1 未取得 pass。** 除 2026-10-17 的首个本轮源码实机场（见第 3 条）外，全部实机场次都早于 `732f6df`，它们只能证明历史成功/失败与工具的敏感性，**不能为当前提交给出任何门的结论**。旧场观测到的继任协同计划未完成保护/辅助落位与主力推进，其直接机制是**投递节奏 + 继任计划寿命**（3 s 决策网格使首个候选迟至公布后 2.511 s 提交，Main Hold 写出后 0.5 s 即被原生改目标），**不是依赖环**——`DependsOnTaskId` 指向的是 Hold 等待任务，依赖链无环（详见 §4 门1）。因此不存在需要另行申请的「互锁修复授权」；门1 的后续验证属于派单 §4 原有要求。当前产物三个门**均未实机验证通过**。
2. **门3 未取得 pass（unverified）。** P/C 实际部署不可比 + 无地形深度产物；未取匹配对照场次。**未放宽容差凑 pass。**
3. **四场本轮源码实机场已完成，但都不能作为任何门的证据。** `p3-P-20261017-104721`（10:47，`RESULT: FAILURES (2)`、`failed stage: collect`，两处失败均在主线采集脚本、已修）、`p3-P-20261017-181046`（18:10，`RESULT: OK`、三 stage 全完成）、`p3-P-20261017-225827`（22:58，第八轮 `lostRole` 修复后，`RESULT: OK`）与 `p3-P-20261018-001730`（00:17，`CombatHoldSec=420`，`RESULT: OK`，完成接敌与歼灭且补足长窗口证据）三门均 unverified（无 reinforce、无止损/撤出决定、无对照会话）。其余场次（§1.3 前 16 行）都早于本轮提交。`732f6df` 及第二至第五轮整补的 C# 改动只经过离线验证（第二轮 309/309 测试与 Release/Debug 重建 0 警告 0 错误；第三、四、五轮未改 C#，沿用该读数）+ 四场实机运行；第八轮改 C# 后为 313/313 + 双构建 0 警告 0 错误。脚本侧另有 PS 5.1 语法、WhatIf、R5 十模式隔离模拟、门2 十三例反例/探针、坐标接线红态验证、49 目录门矩阵 HEAD 对照。**P3 三门仍未取得任何 pass。**
4. **R5 隔离模拟是替身证据，不能替代实机收尾。** 它证明控制流按预期收尾（helper 关闭、只杀本场启动上下文所确认的 PID、归属不明时保留并登记、stage 分列）。第五轮新增的 `foreignidentity` 与 `startedthenexited` 仍只是桩级复现。**实机已部分回答**：`p3-P-20261017-104721` 的 `own game pids: 35948 basis=launch_context_pid_matched_new_process` 与 `ping ok (runtime pid=35948)` 同时成立，证明启动上下文路径在真实 GABS 上可用、且与游戏自报一致；清理实读为 Bannerlord 0 / gabs 0 / 锁 FREE / `safely_exited=1`。但「他方 CBATH 同时写身份文件」「本场进程退出仅剩他方同名进程」两类竞争条件在实机中未出现，仍只有桩级证据。若实机发现本场启动上下文在收尾时不可读，正确结果是登记 unresolved 保留进程，**不是**退回共享身份文件或候选数当归属。

5. **`Get-LaunchContextPids` 的实机风险已基本排除，但仍需每场核对 basis。** 它读 `runtime.json` 的 `gamePid` 与 `launch.log` 里的 `pid=`。首场实机证明：`runtime.json` 由 GABS 在 claim/spawn 时写入、clean stop 后删除，因此只存在于运行期间（这也解释了为何静态检查 `gabs-config\` 时看不到它）。若字段改名，归属会退回 `launch_candidate_pending_runtime_identity`／`new_process_without_launch_result`——登记 unresolved 保留进程，不会误杀，但正常场次的 PID 也停不掉。**每场仍须核对 `own game pids:` 一行的 basis 取值。**
6. **死战不退前置已取得实机读回。** `p3-P-20261017-104721` 的 `pre_mission` 与 `post_deployment` 两次均为 `status=ok patches_installed=True enabled=True policy=fight_to_death_v1`，panicked=0 fleeing=0；`get_last_run` 根 `enabled=true`、`samples=181 / enabled_true=181 / enabled_false=0`。三份归档齐备。这一项从「只接线」升级为「一轮实机成立」，但仍只有单场、单一场景（P）；本场未覆盖士气溃逃的压力边界。
7. **反制目标的定位已改为按冻结键，并已在第二场实机成立。** 首场实机因 `Find-OurMain` 缺陷从未下发反制动作，第六轮的兜底又选错对象（Ranged 175 vs 冻结的 Cavalry 52，相距 19.442 m）；第七轮改为按 `ACTION_FEEDBACK_FROZEN main=<team>/<slot>` 定位。`p3-P-20261017-181046` 中 `opponent-target.json` 的 `frozen_main_key=armed_main_key=1/0`、`identity=00bec9c9@Formation`、`class=Cavalry`、`unit_count=52`，反制动作 `move_target_readback=(860.389,823.109)` 与该点一致；接敌最小距离 9.2 m（t=147.14），反制编队 59→1、我方骑兵全程 52。**仍只覆盖 P 臂一场，且未产生任何正/负反馈决定。**
8. **晚场的独占入口证据不完整**（沿用上批结论）。只有 `p3-P-20261015-203136`、`p3-N-20261015-211938`、`p3-N-20261015-213231` 三场留存完整 stage record（`_lastrun-p3-exclusive-*.txt`）。`p3-P-20261015-214532`、`-215314`、`-220249`、`p3-C-20261015-214157`、`-220738`、`p3-P-20261015-221833`、`-222821` 未留存 stage-record 日志，**不作为「经 `Invoke-ExclusiveGame.ps1` 独占执行」的证据**；其运行身份（§1.3）与产物仍有效，但启动通道归属未证实。
9. **`p3-P-20261015-222821` 未冻结**（`ACTION_FEEDBACK_FROZEN=0`，三门 `unverified :: no freeze`）。该场不可用于任何门。
10. **`_tamper4` 目录内含被修改过的 `CombatBrain2.log`（mtime 2026-10-16 1:34）**，用于门2 缺证据敏感性；原始场 `_verify\sessions\p3-P-20261015-221833` 未被清洗改写。审查副本位于 `E:\Agent\codex\2026-10-14\p3\work\review-20261016\`。

11. **`lostRole` 误归因（第二场实机暴露，第八轮已修复）。** 原 `HandleAdvanceDependencyLost` 在两侧 `PremiseFailure` 都为 `None` 时一律取 Support。第二场 `p3-P-20261017-181046` 的 `plan-2` 在 131.041 以此路径失效，日志写 `role=Support failure=None task=plan-2-task-3`，但独立采样证明真正断掉的是 Guard 的到位窗口（`d(G*)=4.303 → 4.486 → 5.079 m`，刚越 5 m 半径，见 §4 第二个实机场）。后果两层：①日志把健康的一侧报成失效方；②`BuildReassignments` 会按 Support 的职责点排序候选，有替补时会把健康职责的编队换掉。该分支当时在 `Program/tests/` 内**零覆盖**。
    - **修法**：失效归因改由实际依赖读数产生。新增 `RoleDependencyLoss{None, PremiseFailure, CommandInvalid, NotReady}`（`Program/src/Core/CoordinationPlan.cs`），`DependencyReadiness.LossOf(plan, task, isGuard, premise, readiness)` 按「前提失效 > 命令不再成立 > 连续到位窗口不成立」归类，`IsLost` 判参与；`CoordinationDriver.Observe` 每 tick 填 `GuardLoss`/`SupportLoss`，`LostRoleOf(outcome, out role)` 按同一份归类挑出失效职责（两侧同时失效时前提失效优先，再按 Guard 在前）。`MainAdvanceAllowed` 与归因共用新增的 `CommandStillValid`，释放条件与归因不会再各说各话。`TestDriver.HandleAdvanceDependencyLost` 改为消费 `LostRoleOf`，并在未归到具体职责时不再借用另一侧任务（日志 `task=<none>`）。日志新增 `loss=` 与 `guard_loss=/support_loss=`。
    - **未改**：到位半径、稳定窗口、评价公式、退出规则、计划失效条件均未动。
    - **测试**：`Program/tests/CoordinationDriverTests.cs` 新增 4 个 `[Fact]` —— `GuardWindowBreakWithNoPremiseFailureIsReportedAsGuard`、`SupportWindowBreakWithNoPremiseFailureIsReportedAsSupport`、`GuardCommandOverrideIsReportedAsCommandInvalid`、`PremiseFailureOutranksWindowAndCommandLoss`。红态（把 `LostRoleOf` 换回旧规则）3 失败 / 36 通过，其中 `GuardWindowBreakWithNoPremiseFailureIsReportedAsGuard` 报 `Expected: Guard / Actual: Support`；恢复后 39/39，全量 **313/313**。
    - **仍未验证**：实机归因。第三场 `p3-P-20261017-225827`（22:58，第八轮修复后）**没有触发归因分支**：全场 `COORDINATION_DEPENDENCY_LOST=0`，`plan-1` 以 `NotEffective` 失效、`plan-2` 存活到 `MissionEnded`，且该场零接敌零伤亡（`terminal_count=0`，最近距离 214.6 m）。第四场 `p3-P-20261018-001730`（420 s）**同样为 0**，且该场虽然完成接敌与歼灭，却发生在全部有效的 `MainAdvancing` 窗口之外。因此「`role=`/`loss=` 在真实场上归类正确」仍无正样本，**已连续三场未覆盖**；用户已明确不把它当作每场必达目标，保留为未覆盖边界。正面价值是 `guard_loss=`/`support_loss=` 已在实机区分「未写令 / 未到位 / 已就位」，且 `plan-2`（第三场）与 `plan-5`/`plan-7`（第四场）的 `MainAdvancing` 期间未再误报依赖丢失。
    - **第三场核对口径（已修正）**：归因修正**不预期让计划多活一段**。相同读数、没有替补时，计划仍然关闭是完全可能且可能就是正确的结果——本次改的是「报表与替补候选指向哪一侧」，不是依赖判据本身。第三场只核对三件事：①实际失效侧与 `role=/loss=/task=` 三者一致（`loss=NotReady` 对应到位窗口断、`loss=CommandInvalid` 对应命令被改写、`loss=PremiseFailure` 对应前提失效）；②健康职责没有被选成替换对象（`replacement=` 与 `task=` 不得是同一侧的对手方）；③接敌与伤亡是否发生在**有效计划存续期间**，即 `ACTION_FEEDBACK_FROZEN` 所在计划的有效窗口与 `terminal` 时间窗是否重叠。

**未触发的自然边界**（保留为未验证，不登记为通过）：获选超时、写令截止后才首次获选、`FLIGHT`/`STOP_LOSS` 触发的继任、P1/P2 相关回归——本轮 §2 改动了 `TaskFeedback`/`CoordinationPlan`/`TestDriver` 的位置语义判断，已用 309 项单测覆盖，但**未在任何实机窗口回归**。P1 锚点语义由 `AnchorDefinedTaskKeepsAnchorSemantics` 与 `TaskSelectionDeadlineTests.MoveRequiresClosingDistance` 锁定。

12. **`EXIT_STOP_NOT_APPLIED`（第四场首次出现，未修复）。** `p3-P-20261018-001730` 出现 6 条：371.542 三条（`ownership=taken_over`，`plan-18`）与 383.218 三条（`ownership=own`，`plan-19`），全部 `submitted=1 selected=0 waited≈1.0 reason=not_completed_in_time`。同一时刻原生 `NATIVE_MOVEMENT_APPLIED` 记录 `ChargeToTarget` / `Charge`（`our_write=0 source=not_ours`），随后 `BEHAVIOR_DISARMED reason=ExitNotApplied`。即敌方全灭后原生行为接管编队，CB2 的停止令在 1 s 预算内**从未获选**，只能撤武装。**最后一次Killed在370.751秒，404.190为最后一次Deleted。敌方全灭后不要求永久保有进攻控制权，但既存退出义务不能自动判成功；应分别核对结束撤权、未获选及新分配提前接管的实际路径，不放宽1s预算。** 三条 371.542 的 `ownership=taken_over` 说明退出任务被新分配接管，那一段的语义要单独核。

13. **260 s后协同关系未建立：已缩小原因，有限策略已获批准，实机效果待验证。** 现有读数证明plan-8锚点到达而质心未到位；`reads=0`统计连续满足到位关系的读数，不能与任务`POST_OBSERVE Effective`等同。生产复算及共同原因、设计假设失效与候选重选修复见本文“当前纠偏”。没有新增标记或实机场。

14. **W1 有界验证的结论：恢复权重不能让本方行为获选，该方案不成立（2026-10-19）。** 用户裁定 W1 只作有界验证、W2 不批准、W3 只记限制；本次只做离线验证，未改任何生产代码与数值。

    - **验证手段（离线、可复跑）**：`Program/_scratch/r9/` 下把反编译的 `FormationAI`（`FindBestBehavior` 选择链本体）、`TacticComponent`/`TacticCharge`（`TickOccasionally` 的清零+`BehaviorCharge=10000f`）、`BehaviorCharge`（`GetAiWeight`/`CalculateAIWeight` 评分本体）原文编译，配最小引擎语义 stub（`EngineStubs.cs`）与**逐字复制的** `CbHoldBehavior`（`CbHoldBehavior.Probe.cs`，仅把 `TestDriver.Instance.OnBehaviorTick` 换成计数桩）。运行：`Program/_scratch/r9/probe` → `dotnet run`。
    - **读数（`Program/_scratch/r9/probe-output.txt`）**：
      - [A] `weight_before_zero=8.000 → weight_after_zero=0.0000`、`charge_weight=10000.000`、`active_after_find=Charge`、`our_tick=0` —— 权重被原生清零后本方行为**完全不参选**（`FindBestBehavior` 的 `WeightFactor > 1E-07f` 门）。
      - [B] 清零后立刻恢复 `SetBehaviorWeight<CbHoldBehavior>(8f)`（`restored=8.000`），在 20/30/45/60 m 四个距离全部 `active=Charge`、`our_tick=0`。原生 Charge 侧 `GetAIWeight()×WeightFactor` 为 `8222.223`（20 m）/`1000.000`（30–60 m），本方为 `8.000`。
    - **结论**：`ours/charge = 8.0/8222.223 ≈ 0.001`（20 m）与 `8.0/1000.0 = 0.008`（30–60 m），**差距来自 `GetAiWeight()` 本体（Charge 的评分量级在 1e2–1e3，本方恒为 1），不是权重数量级**。按用户裁定「若恢复 8.0 仍不能获选则停止该方案、不开实机」——**W1 判定为方案不成立，未落生产接线，未开实机场**。
    - **该结论覆盖不到的边界**（如实记录，不当作已解决）：①探针只模拟单编队、无援军、无城堡/攻城分支；②只测了「清零后立即恢复」这一种时序，未穷举 `TacticCharge` 每约 1 s 清零与恢复交错的其他相位；③`BehaviorCharge` 的 `num10`（地形斜率项）在探针里恒为 1f，实机若地形项 >1 只会进一步拉大差距，不改变方向。
    - **对既有判断的影响**：`c170476` 记录的「权重被清零导致本方失去参选资格」这一**机制**成立且被本条独立复现；但据此提出的**对策**（恢复 8.0 权重）经本验证被否决。该条不改变其它未闭合项（三编队不可重分配、交战期进不了冻结包络、Support 偏移）的状态，也不作为任何门的证据。

---

## 8. 本轮新增概念清单

无新增架构、战术、指标或命令概念。`TaskDelivery`/`DeliveryFacts` 是接手时已存在的未提交实现（本轮收录并补身份门）。`FormationTask.AnchoredRequest` 是唯一新增生产 API，实现派单 §2.1 已裁定的换算；未引入新全局坐标系、未建通用变换框架。

第二轮新增 `Program/src/Core/ProposalWiring.cs`：它不是新概念，只是把 §2 已裁定的「战术目标语义」判定链（读哪个点、写前怎么复核）从 Host 搬到 Core，使离线夹具与实机走同一份实现。行为不变，`Host/TestDriver.cs` 相应减少 84 行（删除 `RoleTargetPoint`/`TryReadTargetPoint` 两份私有复本）。
