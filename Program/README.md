# CombatBrain 2.0 — Canonical Source Root

> status: canonical
> authority: source-root
> role: CombatBrain 2.0 canonical source root

本目录是 CombatBrain 2.0 的**权威源码根**。

当前执行入口：[P3 原生战术分配与执行接入核查派单](_verify/P3-TACTIC-EXECUTION-REVIEW-DISPATCH.md)（`CB2-PROMPT-0009` v2.0）。本单核查最小接入方案，允许必要离线验证、只读取证及当前行为的最小实测与配套部署，无需另请实机放行；候选生产控制切换仍未批准，P3 尚未通过。原验收合同为 `CB2-PROMPT-0004`，生命周期以 `../prompts/registry.json` 为准；0008及更早执行单已就地归档，已批准策略、历史证据与未闭合项保留。

实机测试默认条件：按用户要求，后续 P1/P2/P3 及其他战斗测试统一开启 CBATH「死战不退」，双方同等禁止士气溃逃与原生全军撤逃，保留真实伤亡和战术撤回整队。使用主线 CBATH 工作树 `E:\MBmod\CBATH-P2` 的配套版本；进入战斗前和部署结束前核对 `cbath/get_fight_to_death` 的 `status=ok`、`patches_installed=true`、`enabled=true`，并归档 `cbath/get_last_run` 中的同名 `fight_to_death` 记录。缺工具/未开启不得继续测试；专门的溃逃对照场才显式关闭并标注。历史证据不补标、P/C 必须同设置。实测不再要求人工交回窗口：现场无活跃游戏且连接归属检查通过时，可经既有完整入口运行；共享锁是否启用以 AGENTS.md §4a 的现行开关为准；实际被占用不抢占，源码支持不等于已部署或实机生效。开关用法见 `E:\MBmod\CBATH-P2\README.md`。

- 2.0 实装代码以本目录为唯一权威来源。
- 实现层不得重新定义或覆盖 Charter 的上位产品目标和约束；
  上位约束见 `../charter/Charter V0.2.md`。
- 1.x 实现保留在 `E:\Agent\6a9268112aeaf49072bef052`，定位为
  legacy / reference implementation，不整体搬迁进本目录。
- 未来复用 1.x 能力时，按具体功能与经确认的设计边界逐项迁移或重实现。

## 当前状态

**Codex 2026-10-12 独立验收：P1 在既定最小指挥闭环范围内通过，可以阶段收口。** 当前结论及 V1–V14 逐项证据见 `_verify/P1-STAGE-EXIT-ACCEPTANCE.md`。最后获选截止整改已通过源码和受审 DLL 对照；自然实机超时/晚到获选、部分继续条件边沿仍未验证，不继续随机补场。观测组功能另行验收。

以下为 P0 与各轮交付历史记录：

**Codex 2026-10-11 独立验收：Gate 3 两项补证通过，P0 在当前 Movement／非玩家 AI 步兵编队控制范围内正式退出，可以进入 P1 最小指挥闭环开发。**
当前阶段结论见 `_verify/P0-STAGE-EXIT-ACCEPTANCE.md`；本批停止补场与整改，未验证边界保留。

2026-10-11 第三轮：Codex Gate 3 复核提出的**两项补证已交付**。
(a) 独立失资格观测：修 `_verify/run-p0.ps1` 取数时机后在补证场 `20261011-164000`（pid 16736，
build `b20261011-161246-db46608a`）取得 CBATH 独立 1 Hz 时间线，`is_ai_controlled=false` 共 8 条
（A 窗口 t=16.097/17.098/18.103/19.104，B 窗口 t=21.110/22.115/23.119/24.122），窗口前后有 True 对照。
(b) 玩家适用域：撤回原"接管不可达"结论，改用公开原生 `Mission.JoinEnemyTeam()` 实机走真实控制链
（补证场 `20261011-162100`，pid 44704，同 build）——同一已绑定对象的队成为 PlayerTeam 后，生产以
`player_owned` 否决、`PlayerEligibilityLost` 取消、`weight 8.000→0.000` 撤权；标记为
`test-adapter_triggered_playerteam_change`，不是 F1/F6 按键链。
另更正 run 7 身份勘误（47104 B / `27F8CBFA29AC1F13A848A61CDC2F761C7751517558CB294AD2DAA30113EF2416`，
不是 run 6 的 37376 B / `44845964…`）。
仍未验证：真实按键/UI 接管事件链、`JoinEnemyTeam` 后再经 `Team.AddTeamAI`/`SetPlayerRole` 重算
`IsAIControlled` 的组合、`REVOKE_EXECUTED` 真实回退、快照陈旧拒绝路径。Gate 3 已由 Codex 在上述限定范围内判定通过。
详见 `_verify/P0-EXIT-GATE3-RUN-REPORT.md` §2.1 / §4.7 / §4.8 / §5.2 与
`_verify/P0-EXIT-GATE3-CONTROL-SAFETY-SCENARIOS.md` §2.1 / §2.2。

P0 第一批（最小命令通路，派单 `TacticalCommandLab/_dispatch/2026-10-10-cb20-p0-minimal-command-path.md`）
已落地：原生行为注册（run 4）、主线程写令与原方法入口/返回/后值组合证据（run 5）、
转交槽位的同步保障（run 7）已通过实机。
决定性场次 `_verify/sessions/20261011-002337/`（run 7，build `b20261011-002322-97c0ff2e`，pid 1512）。
Codex 2026-10-11 独立验收：第一批最小命令通路在已限定范围内通过，
`ThreadHandoff` 槽位同步阻挡已关闭；本批停止整改与补场，未验证边界继续保留。
当前结论见 `_verify/P0A-CODEX-ACCEPTANCE.md`；旧复验报告保留为历史。

P0 Exit Gate 3（控制权安全边沿，派单 `TacticalCommandLab/_dispatch/2026-10-11-cb20-p0-stage-exit-control-safety.md`）
已交付本机证据：决定性场次 `_verify/sessions/20261011-133859/`（build `b20261011-132044-a06f2e05`，pid 35032，
DLL sha256 `4A7E9C1D05EFC0FE666E022789B11A99A149AF73491A579DFA83A9BEDCA918E8`）。
同场跑通两段：场景 A（待写命令在资格变化后被 `not_ai_controlled` 否决、写入尝试 0、取消 `AiControlLost`、撤权 8.000→0.000）
与场景 B（已生效命令遇资格变化同样取消 `AiControlLost`，失资格保持 4.006 s，恢复发生在旧截止之前，
恢复后该意图 `same_intent_open=0` 且无新写入）。**触发方式属于"测试接口触发的资格变化"**（公开原生
`Formation.SetControlledByAI`）；玩家队身份边沿由后续 mode c 补证，真实 UI 按键链仍未验证。
场景判据见 `_verify/P0-EXIT-GATE3-CONTROL-SAFETY-SCENARIOS.md`，证据与 Deviation 见
`_verify/P0-EXIT-GATE3-RUN-REPORT.md`。当前阶段判定见 Codex 的 `_verify/P0-STAGE-EXIT-ACCEPTANCE.md`。

P0 阶段退出核对：Exit Gate 1/2/4 在当前已交付范围内满足；Gate 3 的适用控制资格变化实机证据
由上述 `20261011-133859` 场交付，Codex 复核提出的两项缺口已由补证场 `20261011-162100` /
`20261011-164000` 补上并经 Codex 复核通过；P0 已在当前限定控制范围内满足四项退出条件。
`_verify/P0-STAGE-EXIT-CHECK.md` 保留首次核对历史，当前正式阶段结论以 `_verify/P0-STAGE-EXIT-ACCEPTANCE.md` 为准。

- 规模：`src/` 17 个 .cs（Core 8 + Host 9），`tests/` 8 个测试文件 **65 个**测试全绿
  （43 + 执行前三项复核 4 + 主线程转交 6 + 原方法回执 5 + 槽位并发 2 + Exit Gate 3 的 5）。
  Gate 3 新增 `src/Host/EligibilityProbe.cs`（测试适配：哨兵文件
  `Documents\CombatBrain2\eligibility-probe.txt` 不存在时全程无副作用；mode a/b 走
  `Formation.SetControlledByAI`，mode c 走公开原生 `Mission.JoinEnemyTeam()`）。
  Gate 3 三项 Deviation 中"缺独立失资格观测"已闭合；`had_pending=0` 按 Codex 裁定保持原措辞，
  `HANDOFF_DROPPED reason=intent_closed` 仍无实机证据（不作阻挡）。- 模块身份：`Id CombatBrain2`、程序集 `CombatBrain2.dll`、
  `SubModuleClassType CombatBrain2.Host.SubModule`，与旧 1.x `CombatBrain` 完全分离。
- 构建：`dotnet build CombatBrain2.csproj -c Debug`（.NET SDK 8，`net472`，
  引用 NuGet `Bannerlord.ReferenceAssemblies[.CustomBattle] 1.4.8.119303`）。
- 部署：`_deploy/deploy.ps1`，目标 `Modules\CombatBrain2\`，不触碰 `Modules\CombatBrain\`。
- 实机验证入口：`_verify/run-p0.ps1`，日志落在 `_verify/sessions/<快照时间>/`。
- 命令通路（TCL C 路线）：`CbHoldBehavior : BehaviorComponent` 经
  `FormationAI.AddAiBehavior` + `SetBehaviorWeight<CbHoldBehavior>(8.0)` 进入原生选择链；
  获选判据为 `FormationAI.OnActiveBehaviorChanged` + `ReferenceEquals(f.AI.ActiveBehavior, ours)`；
  获选行为的 `TickOccasionally` **不在回调线程写令** —— 由 `Core/ThreadHandoff` 把这一条命令
  转交到首次 Mission tick 登记的主线程执行点（`SamplingBehavior.OnMissionTick`）再写；
   转交单槽位 `Core/ThreadHandoff` 内部用一把 `lock` 保护（投递/取出/清空/登记均为短临界区，
   不建通用调度框架）；
  原方法体是否真的执行由 `Core/OriginalMethodReceipt` 夹住 `Formation.OnBeforeMovementOrderApplied`
  （`SetMovementOrder` 方法体第一句）观测，回执与 `order_after` 后值**分列**。
  不申请 Full、不关闭 AI、无 Authority/Lease。
- 已有实机事实（run 7）：加载身份 `assembly_file_sha256=27f8cbfa…`、双方编队只读快照与 CBATH
   独立观测对照（Defender 19/14/1/11/1=46、Attacker 20/17/6/22/9=74 逐队一致）、候选阶段零写入、
   `MAIN_THREAD_MARKED tid=33` → `HANDOFF_DEFERRED callback_tid=37` →
  `HANDOFF_EXECUTED exec_tid=33 is_main_thread=1`（53/53 全部转交，`HANDOFF_DIRECT` 0 条）、
  `WRITE_EXECUTED … order_before=Move order_after=StandYourGround original_body_entered=1 original_body_tid=33`、
  `WRITE_BLOCKED` 51 条全 `already_written` 且窗口结束后 0 条、
  行为撤权（`BEHAVIOR_DISARMED weight=0`）、末行 `MISSION_CLEANUP`。
- 已有实机事实（Gate 3 `20261011-133859`）：控制权安全退出三条成立 —— 资格变化后的待写命令不落地（`WRITE_BLOCKED reason=not_ai_controlled write_attempts_intent=0`）、本方行为及时撤权（`weight_before=8.000 weight_after=0.000`，实读回取值而非写死常量）、资格恢复后旧意图不复活（`intent_open_now=<none> same_intent_open=0`）。Gate 3 未覆盖：`HANDOFF_DROPPED reason=intent_closed` 该场 0 条，仍无实机证据。
- 未验证范围（不得据此推进依赖这些能力的功能）：玩家接管边沿、目标失效分支、
  选择超时分支、撤销的真实回退执行（`REVOKE_EXECUTED` 0 条，`previous_order_not_restorable`）、
  快照陈旧拒绝路径（`age` 恒 0，`over_limit=0`）、下一 Mission 无状态残留、
  主线程只登记一次（`ThreadHandoff.MarkMainThread` 不随 Mission tick 线程变化更新）、
  槽位并发测试的覆盖边界（40 轮加压跑绿 ≠ 形式化无竞态证明，见 `_verify/P0A-HANDOVER.md` §3.1 项 9/10）。
- 详见 `_verify/P0A-HANDOVER.md`（交接与判据）、`_verify/P0A-CODEX-ACCEPTANCE.md`（当前验收）、
  `_verify/P0-EXIT-GATE3-CONTROL-SAFETY-SCENARIOS.md`（Gate 3 场景判据）、
  `_verify/P0-EXIT-GATE3-RUN-REPORT.md`（Gate 3 证据与 Deviation）；
  之前的独立审查/复验报告保留为历史。
- "P0 第一批通过"不等于 P0 整体通过；战术决策、预备队产品规则等均未开工。
