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
已落地并完成原生行为注册整改，run 4 实际获选与 Stop 落地成立
（`_verify/sessions/20261010-213621/`，build `b20261010-211405-653e521d`）。
Codex 复验仍暂不通过第一批整体：主线程执行保障与原方法完成/被跳过证据尚待落实；只限执行通路整改。

- 规模：`src/` 14 个 .cs（Core 6 + Host 8，新增 `Host/CbHoldBehavior.cs`），
  `tests/` 5 个测试文件 **47 个**测试全绿（43 + 执行前三项复核新增 4）。
- 模块身份：`Id CombatBrain2`、程序集 `CombatBrain2.dll`、
  `SubModuleClassType CombatBrain2.Host.SubModule`，与旧 1.x `CombatBrain` 完全分离。
- 构建：`dotnet build CombatBrain2.csproj -c Debug`（.NET SDK 8，`net472`，
  引用 NuGet `Bannerlord.ReferenceAssemblies[.CustomBattle] 1.4.8.119303`）。
- 部署：`_deploy/deploy.ps1`，目标 `Modules\CombatBrain2\`，不触碰 `Modules\CombatBrain\`。
- 实机验证入口：`_verify/run-p0.ps1`，日志落在 `_verify/sessions/<快照时间>/`。
- 命令通路（TCL C 路线，真实获选与落地已验证）：`CbHoldBehavior : BehaviorComponent` 经
  `FormationAI.AddAiBehavior` + `SetBehaviorWeight<CbHoldBehavior>(8.0)` 进入原生选择链；
  获选判据为 `FormationAI.OnActiveBehaviorChanged` + `ReferenceEquals(f.AI.ActiveBehavior, ours)`；
  获选行为的 `TickOccasionally` 同步进入本方 TestDriver setter；
  `route=native_behavior_apply` 本身不能证明主线程或原方法体完成。不申请 Full、不关闭 AI、无 Authority/Lease。
- 已有实机事实：独立加载身份、双方编队只读快照与独立观测对照（run 4 晚期两条独立
  路径逐项一致：47/40、total 87）、候选阶段零写入、两次 Move→StandYourGround 实际后值、
  有限窗口到期与显式关闭后无旧意图写入、行为撤权（`BEHAVIOR_DISARMED weight=0`）。
- 未验证范围（不得据此推进依赖这些能力的功能）：玩家接管边沿、目标失效分支、
  选择超时分支、撤销的真实回退执行（`REVOKE_EXECUTED` 0 条，`previous_order_not_restorable`）、
  快照陈旧拒绝路径（`age` 恒 0）、下一 Mission 无状态残留。
- 详见 `_verify/P0A-HANDOVER.md`（交接与判据）、`_verify/P0A-CODEX-REREVIEW.md`（当前复验）；
  `_verify/P0A-CODEX-REVIEW.md` 保留为整改前历史。
- "P0 第一批通过"不等于 P0 整体通过；战术决策、预备队产品规则等均未开工。
