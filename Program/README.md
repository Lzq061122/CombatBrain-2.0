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
已落地并取得限定实机证据；Codex 独立审查发现 C 路线尚未实现，第一批整体暂不通过。

- 规模：`src/` 13 个 .cs（Core 6 + Host 7），`tests/` 5 个测试文件 43 个测试全绿。
- 模块身份：`Id CombatBrain2`、程序集 `CombatBrain2.dll`、
  `SubModuleClassType CombatBrain2.Host.SubModule`，与旧 1.x `CombatBrain` 完全分离。
- 构建：`dotnet build CombatBrain2.csproj -c Debug`（.NET SDK 8，`net472`，
  引用 NuGet `Bannerlord.ReferenceAssemblies[.CustomBattle] 1.4.8.119303`）。
- 部署：`_deploy/deploy.ps1`，目标 `Modules\CombatBrain2\`，不触碰 `Modules\CombatBrain\`。
- 实机验证入口：`_verify/run-p0.ps1`，日志落在 `_verify/sessions/<快照时间>/`。
- 已有实机事实：独立加载身份、双方编队只读快照与独立观测对照、
  本地候选阶段不写令、两次外部 Stop 后值变化、有限窗口到期与显式关闭。
- 需要限定整改：单个站定 Behavior 尚未进入原生选择；普通写令回调被误标为
  REAL_SELECTION。补真实 C 通路及执行前对象/AI/当前行为复核，见
  `_verify/P0A-CODEX-REVIEW.md`。
- 未验证范围（不得据此推进依赖这些能力的功能）：玩家接管边沿、目标失效分支、
  选择超时分支、撤销的真实回退执行、快照陈旧拒绝路径、下一 Mission 无状态残留。
- "P0 第一批通过"不等于 P0 整体通过；战术决策、预备队产品规则等均未开工。
