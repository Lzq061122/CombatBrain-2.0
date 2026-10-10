# CombatBrain 2.0 — Canonical Source Root

> status: canonical
> authority: source-root
> role: CombatBrain 2.0 canonical source root

本目录是 CombatBrain 2.0 的**权威源码根**。

- 2.0 实装代码以本目录为唯一权威来源。
- 实现层不得重新定义或覆盖 Charter 的上位产品目标和约束；
  上位约束见 `../charter/Charter V0.2.md`。
- 1.x 实现保留在 `E:\Agent\6a9268112aeaf49072bef052`，定位为
  legacy / reference implementation，不整体搬迁进本目录。
- 未来复用 1.x 能力时，按具体功能与经确认的设计边界逐项迁移或重实现。

## 当前状态

P0 第一批（最小命令通路，派单 `TacticalCommandLab/_dispatch/2026-10-10-cb20-p0-minimal-command-path.md`）
已落地：原生行为注册（run 4）、主线程写令与原方法入口/返回/后值组合证据（run 5）、
转交槽位的同步保障（run 7）已通过实机。
决定性场次 `_verify/sessions/20261011-002337/`（run 7，build `b20261011-002322-97c0ff2e`，pid 1512）。
Codex 2026-10-11 独立验收：第一批最小命令通路在已限定范围内通过，
`ThreadHandoff` 槽位同步阻挡已关闭；本批停止整改与补场，未验证边界继续保留。
当前结论见 `_verify/P0A-CODEX-ACCEPTANCE.md`；旧复验报告保留为历史。

- 规模：`src/` 16 个 .cs（Core 8 + Host 8），`tests/` 7 个测试文件 **60 个**测试全绿
  （43 + 执行前三项复核 4 + 主线程转交 6 + 原方法回执 5 + 槽位并发 2）。
- 模块身份：`Id CombatBrain2`、程序集 `CombatBrain2.dll`、
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
- 未验证范围（不得据此推进依赖这些能力的功能）：玩家接管边沿、目标失效分支、
  选择超时分支、撤销的真实回退执行（`REVOKE_EXECUTED` 0 条，`previous_order_not_restorable`）、
  快照陈旧拒绝路径（`age` 恒 0，`over_limit=0`）、下一 Mission 无状态残留、
  主线程只登记一次（`ThreadHandoff.MarkMainThread` 不随 Mission tick 线程变化更新）、
  槽位并发测试的覆盖边界（40 轮加压跑绿 ≠ 形式化无竞态证明，见 `_verify/P0A-HANDOVER.md` §3.1 项 9/10）。
- 详见 `_verify/P0A-HANDOVER.md`（交接与判据）、`_verify/P0A-CODEX-ACCEPTANCE.md`（当前验收）；
  之前的独立审查/复验报告保留为历史。
- "P0 第一批通过"不等于 P0 整体通过；战术决策、预备队产品规则等均未开工。
