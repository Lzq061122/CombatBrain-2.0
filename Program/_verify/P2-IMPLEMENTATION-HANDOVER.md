# P2 多 Formation 协同 · 实现交接报告

状态：**开发自检完成，实机执行已完成（P2 整改四场 R4 场次 + P1 P/N 回归各 ×1；历史 A5 四场见 §0–§6）；独立验收未发生 — 待独立验收**
提交：CB2 主线本单**实现提交为 `8fb1efa`**（28 files / +7112 / −24），其后是对本报告的历次更正；**2026-10-14 P2-RESTART 整改提交为 `24548d2`**（13 files / +785 / −60）。全部在 `master`、**均未 push**。CBATH 配套提交 **`b9fcef7`**，**2026-10-14 增量为 `ebed701`**（工作树 `E:\MBmod\CBATH-P2`，分支 `codex/cb20-p2-evidence`，未 push）。**整改的 R1–R4 结果、最新场次索引与最终 A1–A13 状态见 §7。**
**现场核对命令**：`git log --oneline 8fb1efa^..HEAD`（列出实现提交及其后的全部更正）。
**本文件不枚举自身提交清单**：每更正一次本报告就会追加一条提交，任何写死的提交列表都会立刻过时；提交范围见 §1.3。
派单依据：`Program/_verify/P2-MULTI-FORMATION-DISPATCH.md` v1.1（登记 `CB2-PROMPT-0002`，ACTIVE；磁盘原始字节 SHA-256 = `3471FF7BA6A46A8387A5F1054732D1A3F83D1165E362485A21049F3E2CE5D77E`，与登记一致）
日期：2026-10-12 初版（2026-10-13 实机结果更新；本文件状态段按 2026-10-13 17:4x 实测重写）
接手者：CombatBrain 2.0 主线开发组（单 AI 执行，未启用 AgentTeams）

> **分级口径（派单 §12 第 261 行）**：本报告的所有结论分四级记录，不得互相替代——
> **① 可编译 → ② 已纳入产物 → ③ 源码接入 → ④ 实机执行**。
> 当前状态：Core/Host/适配全部达到 ①–②；③ 逐项标注证据位置；**④ 已执行四场，逐项结果见 §3**。
> 特别声明：`CS0649` 警告消失只证明编译器不再报告未赋值，**不证明**读取路径实际执行；
> `BuildIdentity.g.cs` **不是**已加载游戏的证据（派单 §12 第 257 行：build_id 不是单独源码证明，磁盘 SHA 不是内存摘要）。
> 本轮实机证据的可信度依据是**同一场次内**三条记录互相闭合：`deployed-module.json` 的磁盘 SHA ==
> 游戏内 `[CB2][RUNTIME_IDENTITY] assembly_file_sha256` == 构建时 `source_sha256`。

---

## 0. 现场基线核对（2026-10-13 17:4x 实测，非旧快照）

| 项 | 派单 §1 旧快照 | 本轮实测 | 结论 |
|---|---|---|---|
| CB2 cwd | `E:\MBmod\CombatBrain 2.0` | 同 | 一致 |
| CB2 分支 / HEAD | `master` / `f353715` | `master` / 见下方提交链 | **已前进**；沿革 `aec0f32` 派单迁入 → `f44cc3b` 开发节奏 → `1853b48` 登记 `BattleSizeResized` 工具（与本单源码无关）→ `8fb1efa` 本单实现 → 其后均为对本报告的文档更正。**HEAD 不在此写死**：本文件每更正一次就会产生新提交，请用 `git log --oneline -1` 现场核。 |
| CB2 脏文件 | `BuildIdentity.g.cs` 修改 + `_scratch`/`_lastrun*` 等 | 仅 `Program/src/Host/BuildIdentity.g.cs` 修改 + 25 项非交付未跟踪 | P2 实现与报告**已提交**；剩余脏项全属自动生成或运行产物，见 §1.3。数量随部署重写而变，现场核用 `git status --porcelain`。 |
| 观测组 worktree | 3feafda | `E:\MBmod\BattleObservation-V1\CB2` / `codex/battle-observation-v1` / **`c8c0a98`** | 本轮只读，未修改/构建/切换/提交 |
| 派单文件身份 | registry `content_sha256=3471ff7b…` | 磁盘原始字节 SHA-256 = `3471FF7BA6A46A8387A5F1054732D1A3F83D1165E362485A21049F3E2CE5D77E` | **一致**（仅大小写表示差异） |

**本单提交（全部 `master`、全部未 push）**：

- **实现提交 `8fb1efa`**：`feat(p2): implement multi-formation coordination with unique arbitration` —— 实现 + 验证脚本 + 测试 + 报告初版，**28 files / +7112 / −24**。这是本单唯一的实现提交，其哈希与规模都不会随本报告更正而改变。
- **其后的提交全部是对本报告的文档更正**（正文写入、警告口径更正、自引用修正、只读工具依据补录等）。**不在此枚举**，因为每更正一次本报告就会追加一条，写死列表必然立刻过时。
  现场枚举：`git log --oneline 8fb1efa^..HEAD`；只看文档改动：`git log --oneline 8fb1efa..HEAD -- Program/_verify/P2-IMPLEMENTATION-HANDOVER.md`。

- 提交范围均与 §1.3 清单一致；已核验**不含** `ObservationAdapter.cs`、`E:\MBmod\BattleObservation-V1\*`、`BuildIdentity.g.cs`、`_scratch/`、`_lastrun*.txt`、`gabs-config/`、`sessions/`。
- 提交后工作树剩余脏项：`Program/src/Host/BuildIdentity.g.cs`（部署脚本自动重写）+ 非交付运行产物（`_scratch/`、`_lastrun*.txt`、`gabs-config/`、`sessions/`）。现场核用 `git status --porcelain`。

`git worktree list`（实测；主线 HEAD 随本文件更正而前进，现场核用该命令）：
```
E:/MBmod/CombatBrain 2.0           <master HEAD> [master]
E:/MBmod/BattleObservation-V1/CB2  c8c0a98 [codex/battle-observation-v1]
```

**源码冻结点（本轮四场实机共用同一份源码）**：`Program\src` 内最后一次 P2 相关改动为
`Host\TestDriver.cs` 16:36:23、`Core\TestParameters.cs` 16:17:37、`Core\ProposalArbiter.cs` 16:05:12、
`Host\EligibilityProbe.cs` 15:58:20、`Core\CoordinationDriver.cs` 15:57:53、`Core\BattleAssessment.cs` 15:57:31、
`Core\BattlePlan.cs` 15:34:40。四场实机（16:37 A、16:43 B、17:05 P、17:09 N）**均在该冻结点之后**，
唯一更晚的源文件是每场部署自动重写的 `Host\BuildIdentity.g.cs`。→ **A/B/P/N 四场共享同一份受审源码。**

### 0.2 共享游戏资源的实际使用（本轮全程经协调入口串行）

| 证据 | 值 |
|---|---|
| 入口 | `& 'E:\MBmod\CombatBrain 2.0\Program\_verify\run-p2-exclusive.ps1' -Scenario <A\|B> -CombatHoldSec 180`、`run-p1-exclusive.ps1 -Config <P\|N> -CombatHoldSec 180` |
| 协调层 | `E:\MBmod\BattleObservation-V1\coordination\Invoke-ExclusiveGame.ps1 -Owner '主线组'`（锁覆盖部署+启动+采集+归档+清理） |
| 每场执行前预检 | 锁独占打开测试、`Bannerlord.exe` 进程、steam 存活、`engine_config.txt safely_exited`、观测组 handoff 时间 |
| 锁残留现象 | 多次观察到锁 JSON 残留但持有 PID 已死；**按纪律未把「锁可获取」当作资源空闲**，每次都另外核对进程与观测组活动 |
| 四场结果 | `RESULT: OK` / `EXITCODE=0`，各场结束均 `no residual Bannerlord process`、`no new crash dir`、哨兵状态复位 |

**四场归档已复核完整**（每场 `CombatBrain2.log` 的 `assembly_file_sha256` 与 `deployed-module.json` 的 `sha256` 逐一 `MATCH`）：
`p2-A-20261013-163716`、`p2-B-20261013-164318`、`20261013-170546`、`20261013-170928`。
P2 独立采样文件 `p2-evidence-*.json`：场景 A 175 个、场景 B 188 个（**P1 两场为 0，属正常**——P1 入口不写 P2 证据文件，改用 `p1-evidence-*.json`）。

**收尾时共享资源状态（2026-10-13 17:41 实测）**：主线组四场已全部结束并清理；随后**观测组开始使用共享资源**
（锁文件 mtime 17:38:28 且被独占持有 → 读取被拒；游戏目录 `CombatBrain2.dll` 已是观测组产物 `99840 B / 2AA9232B…`；
`Bannerlord.exe` pid 49260 运行中；`engine_config.txt safely_exited = 0`；观测组 `Program/src/Host/BuildIdentity.g.cs` 被其部署脚本重写为 `b20261013-173828-4e5c05eb`）。
**本组在此状态下只做离线收尾，未部署、未启停游戏、未改哨兵、未触碰观测组任何文件。**

本轮另有一次**环境偶发失败**记录在案（非本单改动引入）：P1 首次尝试 `20261013-164742` 在 GABP 桥接层崩溃
（`Unhandled Exception: System.InvalidOperationException: Connection is not active` at `Lib.GAB.Transport.TcpConnection.SendMessageAsync`
→ `Lib.GAB.Server.GabpServer.SendErrorResponseAsync`），该错误文件与 2026-10-10 历史崩溃
`Program\_verify\sessions\20261010-211555\crash-2026-10-10_13.16.29\rgl_log_errors_12696.txt` **逐字节相同**
（SHA256 均为 `E8027D9420F3E8513F7DE6276866628EB51E560B4D54FFDA8C5238C7EBAA4AFF`）。该场游戏未进入主菜单，
无任何 CB2 计划数据，判定为已知环境问题；重跑后正常。

---

## 1. 实现接手表（派单 §9 必填）

### 1.1 真实候选对象与能力

生产可用候选必须同时满足（`PlanBuilder.EligibleAsTaskCandidate` + 宿主 `EligibleForControl`）：

- `UnitCount > 0`
- `!IsPlayerTeam` 且 `IsPlayerControlled == false`（`PlayerOwner == null`）
- `IsAiControlled == true`
- `ObjectIdentity` 非空

**控制组合（本单白名单）**：

| 类别判定 | Hold | Engage | Reposition |
|---|---|---|---|
| Infantry | 允许 | 允许 | 允许 |
| Ranged（不含 HorseArcher） | 允许 | 允许 | 允许 |
| Cavalry | **本单新增，允许** | **禁止** | **本单新增，允许** |
| HorseArcher | 禁止 | 禁止 | 禁止 |
| 未知/空类别 | 禁止 | 禁止 | 禁止 |

**类别白名单实现要求**：现有 `BattleAssessment.IsRanged` 把 `HorseArcher` 也归入 Ranged，**不能**直接当 P2 白名单使用。P2 需新增独立判定（精确匹配），并保留 `IsRanged`/`IsInfantry` 原语义供全队统计使用（§2 第 65 行：不靠过滤统计改变优势判断）。

### 1.2 修改文件清单（对照 §10 白名单）

**新增**：

| 文件 | 职责 |
|---|---|
| `Program/src/Core/CoordinationPlan.cs` | P2 职责枚举（`FormationRole`）、职责前提失效枚举（`RolePremiseFailure`）、依赖就绪窗口（`DependencyReadiness`）、协同几何（`CoordinationGeometry`） |
| `Program/src/Core/CoordinationDriver.cs` | 协同阶段状态机：绑定三职责、依赖读数与释放、阶段转换、重分配、关系期限 |
| `Program/src/Core/ProposalArbiter.cs` | CB2 内部提案批次与唯一裁决纯逻辑 |
| `Program/_verify/P2-IMPLEMENTATION-HANDOVER.md` | 本文件 |
| `Program/_verify/run-p2.ps1` | P2 采集脚本（场景 A/B/P；preflight、哨兵、1 Hz 独立采样、teardown） |
| `Program/_verify/run-p2-exclusive.ps1` | **完整流程唯一入口**：组装参数并调用协调入口，三阶段全在锁内 |
| `Program/_verify/_p2-exclusive-stage.ps1` | 锁内三阶段编排：CB2 部署 → CBATH 部署 → 采集 |
| `Program/src/Core/EligibilityTrigger.cs` | P2 测试适配的触发判据（纯逻辑）：区分场景 A（关系建立后）与 B（关系建立前）两个相位 |
| `Program/tests/EligibilityTriggerTests.cs` | 触发判据反例：两相位各自的前置不清时不触发 |
| `Program/tests/CoordinationTests.cs` | P2 计划创建、类别白名单、继续条件反例 |
| `Program/tests/DependencyReadinessTests.cs` | 到位判据、连续读数、职责前提失效反例 |
| `Program/tests/ProposalArbiterTests.cs` | 提案批次、裁决顺序无关性、旧版本不复活反例 |
| `Program/tests/CoordinationDriverTests.cs` | 三职责绑定、释放门控、版本语义、分职责期限反例 |

**2026-10-13 A5 补实现新增的改动点**（全部落在上述白名单文件内，未新增文件）：

| 文件 | 改动 |
|---|---|
| `Program/src/Core/TestParameters.cs` | 新增 `P2CompetitionLateralMeters = 2.0`（挑战者侧向偏移）与 `P2CompetitionWindowSeconds = 8.0`（注入窗口） |
| `Program/src/Core/BattlePlan.cs` | `FormationTask` 新增 `HasCanonicalTarget`/`CanonicalX`/`CanonicalY` 与 `SetCanonicalTarget(x, y)` |
| `Program/src/Core/ProposalArbiter.cs` | `TargetError` 由 `private static` 提升为 `internal static`，并在 `!task.HasPosition` 时回退到 `HasCanonicalTarget` 点（比较逻辑未改，仅补齐首次竞争的基准） |
| `Program/src/Core/BattleAssessment.cs` | `BuildCoordination` 内按 role 为三个初始任务写入 M*/G*/S* 规范点 |
| `Program/src/Core/CoordinationDriver.cs` | 新增 `SetCanonicalTarget(plan, task)` 助手，并在 `CreateAdvanceTask`/`RebindSameObject`/`RebindRole` 三处新建任务后调用 |
| `Program/src/Host/TestDriver.cs` | 新增 `InjectCompetition`/`SubmitInjected`/`CompetitionInjectTarget`/`PointText`/`DecisionOutcome`/`LoserReason` 与 `ARBITRATION_INJECT`/`ARBITRATION_CANDIDATE` 日志；`PublishCoordinationProposals` 内接入 |
| `Program/src/Host/EligibilityProbe.cs` | `LoadP2Once` 新增 `competition` 键解析与 `P2CompetitionEnabled` 字段（默认 `false`） |
| `Program/_verify/run-p2.ps1` | 场景 A 的 `p2-eligibility-probe.txt` 追加第二行 `competition=1`（纯 ASCII） |
| `Program/tests/ProposalArbiterTests.cs` | 新增 `CanonicalTargetDecidesFirstCompetitionRegardlessOfOrder`（两种输入顺序断言赢家恒为规范点）与 `TestInjectionCannotOutrankTaskSubmissionAtSameTarget` |

**修改**（仅直接相关行）：

| 文件 | 改动 |
|---|---|
| `Program/src/Core/TestParameters.cs` | P2 参数常量 + `ProposalRejectReason`/`ProposalSource` 枚举 |
| `Program/src/Core/BattlePlan.cs` | `FormationTask` 增加 `Revision`(99) / `Role` / 依赖字段 / proposal 字段；`BattlePlan` 增加 `Revision`(244) / `Geometry` / `Allows()`；`PlanConditions` 增加 `CoordinationCanContinue` 与协同继续分支 |
| `Program/src/Core/PlanLedger.cs` | 新增 `CreateCoordination` 生产入口与 `BumpRevision` 静态方法（**`Revision` 字段不在本文件**，见上一行） |
| `Program/src/Core/BattleAssessment.cs` | `PlanBuilder` 三编队分配、类别白名单、协同几何与到位/人数判据 |
| `Program/src/Host/TestDriver.cs` | P2 计划驱动、评价点提案公布、写前裁决门、阶段转换同步三处、退出登记与归属分流 |
| `Program/src/Host/FormationSampler.cs` | 实际活跃成员质心读取（成员级遍历，`ActiveCenterX/Y`、`ActiveMemberCount`、`MemberDigest`） |
| `Program/src/Host/SamplingBehavior.cs` | P1/P2 互斥开关 + P2 节奏（`TickP2` → `CoordinationTick`/`DecisionTick`/`SubmitPending`/`PublishCoordinationProposals` + 逐 tick `FeedbackTick`/`DrainPendingCommand`/`SweepExitStops`） |
| `Program/src/Host/EligibilityProbe.cs` | P2 测试适配（派单 §9.2）：`P2PrepareSubstitutes`（B 准备期剥替补）、`P2TryTrigger`（按 revision 绑定的实际保护编队）、`P2SweepRestores` |
| `Program/CombatBrain2.csproj`、`Program/tests/CombatBrain2.Tests.csproj` | 注册新增文件 |
| `Program/_verify/P2-MULTI-FORMATION-DISPATCH.md` | 仅登记摘要（如需） |

**明确不动**：`Program/src/Host/ObservationAdapter.cs`、`Program/src/Core/Cb2Log.cs`、`TargetSelector.cs`、`IndependentObservation.cs`、`ExecutionGate.cs`（除非必要）、`WriteLedger.cs`（除非必要）、CBATH `ProbeSubModule.cs`/`Observation/`/`RunRecord` 契约。

### 1.3 既有脏改归属（2026-10-13 实测）

| 脏文件 | 归属 | 本轮处置 |
|---|---|---|
| `Program/src/Host/BuildIdentity.g.cs` | 自动生成身份（每场部署重写） | 不手工编辑、不纳入本任务暂存 |
| `Program/_scratch/`、`_scratch/`、`Program/_verify/_lastrun*.txt`、`Program/_verify/gabs-config/`、`Program/_verify/sessions/` | 非交付物（草稿 / 运行产物 / 场次归档） | 保持原样，不进提交 |
| `E:\MBmod\BattleObservation-V1\*` | **观测组** | 只读，未写入/构建/切换/提交 |

**本单已提交的明确范围**（禁 `git add -A` / `git commit -a`，已按清单逐项暂存）：`Program/src/Core/` 下 `BattlePlan.cs`、`BattleAssessment.cs`、`TestParameters.cs`、`PlanLedger.cs`、`FormationSnapshot.cs`、`CoordinationPlan.cs`、`CoordinationDriver.cs`、`ProposalArbiter.cs`、`EligibilityTrigger.cs`、`ExitStopLedger.cs`；`Program/src/Host/` 下 `TestDriver.cs`、`EligibilityProbe.cs`、`FormationSampler.cs`、`SamplingBehavior.cs`、`CbHoldBehavior.cs`；`Program/CombatBrain2.csproj`、`Program/tests/CombatBrain2.Tests.csproj` 与 `Program/tests/` 下 7 个 P2 测试文件；`Program/_verify/` 下 `run-p2.ps1`、`run-p2-exclusive.ps1`、`_p2-exclusive-stage.ps1`、`P2-IMPLEMENTATION-HANDOVER.md`。落在 `8fb1efa`（实现 + 脚本 + 测试 + 报告初版；其后均为对本报告的文档更正）。

**不在本单提交范围但需说明的两点**：
- `Program/_verify/run-p1-exclusive.ps1` 与 `_p1-exclusive-stage.ps1` **已由 2026-10-12 的 P1 提交 `68103be` 收录**（`git log -1 -- <path>` 可核），本轮 A12 直接复用，**不构成本单增量**。
- `Program/src/Host/BuildIdentity.g.cs` 是已被跟踪的自动生成文件（历史 7 次提交），每场部署重写；本轮不手工编辑、不纳入本单暂存。

### 1.4 每项判据的触发方式与证据来源（实机已跑，按实际情况修正）

> **修正说明**：本表初版列出的三个事件名在生产代码中**并不存在**，实机取证时按真实事件名替换 ——
> `DEPENDENCY_READY`/`DEPENDENCY_WAIT` → `COORDINATION_STATE` 的 `guard_ready=/support_ready=/advance_allowed=` 字段；
> `PLAN_NOT_EXECUTABLE` → `PLAN_INVALIDATED reason=FormationEligibilityLost` + `TASK_CLOSED` + `HANDOFF_CLEARED`；
> `REASSIGNMENT_*` → `COORDINATION_DEPENDENCY_LOST` + `COORDINATION_REASSIGNED`。

| 判据 | 触发方式 | 证据来源（实测） |
|---|---|---|
| A2 Cavalry Hold/Reposition | 场景 A/B 初始三职责中骑兵承担保护或辅助 | `REAL_SELECTION` + `WRITE_EXECUTED route=native_behavior_apply` + `formations-early/late.json` |
| A3 三职责 | 同一 Plan 下主力/保护/辅助三对象 | `PLAN_CREATED formations=` 三键 + `ASSIGNMENT_PUBLISHED` ×3 |
| A4 协同发生 | 保护/辅助未到位 → 主力 Holding；到位 → 主力 Reposition | `COORDINATION_STATE` 的 `guard_ready=/support_ready=/advance_allowed=` 序列 + `COORDINATION_ADVANCE` + 前后两条 `WRITE_EXECUTED` |
| A5 唯一裁决 | 测试注入两个同级合法不同目标 + 四类冲突注入 | `ARBITRATION_CANDIDATE` / `ARBITRATION_INJECT` / `PROPOSAL_PUBLISHED` 全表 |
| A6 分配稳定与延迟 | 条件未变连续 ≥8 s 同 Plan/分配；首令与编队间隔 ≥1.5 s | `PLAN_CONTINUED` 连续序列 + `FIRST_COMMAND_DELAY` + `WRITE_DELAYED` |
| A7 职责改变 | `EligibilityProbe` 让实际保护编队失资格 ≥4 s | `COORDINATION_DEPENDENCY_LOST` + `COORDINATION_REASSIGNED` + 旧新任务/revision（**部分覆盖，见 §3**） |
| A8 不可执行 | 场景 B 让保护编队失资格且无替补 | `PLAN_INVALIDATED reason=FormationEligibilityLost` + `TASK_CLOSED` ×3 + `HANDOFF_CLEARED` + 主力未释放 |
| A9 跨版本不复活 | 离线反例 + 实机恢复后 ≥8 s 扫描 | 测试 + 场次日志（旧任务失效后零新投递/写令） |
| A11 安全边沿 | 反例测试 + 实机资格撤销/清理 | 测试输出 + `EXIT_STOP_*` + `HANDOFF_CLEARED` + `MISSION_CLEANUP` |
| A12 P1 回归 | 新产物跑 P/N 两场 | 场次索引 + `WRITE_EXECUTED` 任务类型对照 |

### 1.5 实际引用的只读工具依据（派单 §12 L281 必填）

本轮为判定「本方行为已在位时原生为何不再发 `OnActiveBehaviorChanged`」这一机制问题，**只读反编译**了原生产物。
全部为离线只读，未修改游戏文件、未注入、未参与任何实机行为；**不构成实现增量**。

| 项 | 值 |
|---|---|
| 工具 | `C:\Users\Administrator\.dotnet\tools\ilspycmd.exe`（本机已安装；无 ildasm / monodis / dnSpy） |
| 被反编译程序集 | `E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\TaleWorlds.MountAndBlade.dll` |
| 程序集身份 | 2600408 B / LastWrite 2026-08-23 15:41:53 / SHA-256 `19387F31557FF840D14F378F6BBDF1D58FCFF406AB9FF2294DFBB3E49A50B87E` |
| 调用形式 | `& <ilspycmd> -t <Type> -o <outDir> <dll>`（**不可用 `> file` 重定向**，会写出二进制） |
| 产物 1 | `Program\_scratch\decomp-natural\TaleWorlds.MountAndBlade.FormationAI.decompiled.cs`，8894 B / SHA-256 `F30AA3994F654F15BE55DD7B6C2939FD50BF3EC0850CF1370BAA1FFE8D697C4A` |
| 产物 2 | `Program\_scratch\decomp-natural\TaleWorlds.MountAndBlade.BehaviorComponent.decompiled.cs`，4825 B / SHA-256 `365B37BA6E84C2DEF7CC61C2B72D8E2C4B69D95C4CE08AFB27D9295ECBE51A6E` |
| 产物归属 | `Program\_scratch\` 为草稿目录，**不进提交**（见 §1.3）；复核者可按上表命令与 SHA 自行重生成 |

**由该依据得出的两条结论（决定了本轮修法形状，非源码改动）**：

1. `FormationAI.ActiveBehavior` 的 setter 有**双重短路**：外层 `if (_activeBehavior != value)` 是引用比较，同一实例再次当选直接短路；内层
   `if (OnActiveBehaviorChanged != null && (activeBehavior == null || !activeBehavior.Equals(value)))` 走 `BehaviorComponent.Equals`，而后者被重写为
   `return obj != null && GetType() == obj.GetType();`（**类型比较**）。→ 同一类型的第二个任务不会触发获选事件，**这是结构性的，不是时序问题**。
2. `FormationAI.TickOccasionally` 中 `ActiveBehavior.TickOccasionally()`（`FindBestBehavior()` 为真分支）是本方行为 tick 的**唯一上游**。
   因此「进入 `CbHoldBehavior.TickOccasionally()`」本身就是「原生此刻选中本行为」的硬证据，不必依赖获选事件。

→ 直接后果：缺陷修复走 `AcceptRealSelection(... source=behavior_tick)`，而不是继续依赖 `OnActiveBehaviorChanged`；已否决的候选修法（代版权重、每任务新建实例）见 §6.3。

---

## 2. 当前进度

- [x] §0 现场基线核对
- [x] §1 实现接手表
- [x] P2 Core 纯逻辑与反例测试（**215/215 通过**，2026-10-13 实测，含 A5 新增两条）
- [x] P2 Host 接入（`TestDriver.cs` / `FormationSampler.cs` / `SamplingBehavior.cs` / `EligibilityProbe.cs`）
- [x] P2 测试适配（`EligibilityTrigger.cs` + `EligibilityProbe` 扩展），覆盖场景 A/B 两个相位
- [x] **A5 竞争注入补实现**（派单 §9.2 L207 / 验收 A5 / 三门之一）——本轮新增，见 §2.2
- [x] 完整验证脚本（`run-p2-exclusive.ps1` 唯一入口 + `_p2-exclusive-stage.ps1` 锁内三阶段 + `run-p2.ps1` 采集）
- [x] 构建 + 离线自检（net472 宿主 **0 错误 0 警告**；测试 215/215）
- [x] 实机执行：场景 A ×2、场景 B ×1、P1 P/N 回归各 ×1（四场 `RESULT: OK`）
- [x] 逐项填 A1–A13（§3）

### 2.0 受审产物身份（四场实机实际加载，逐场记录）

**每场都重新部署 → 各场 `build_id` 与 SHA 互不相同是预期行为。** 可信度依据是**同一场次内**三条记录闭合：
构建 `source_sha256` == `deployed-module.json` 磁盘 SHA == 游戏内 `[CB2][RUNTIME_IDENTITY] assembly_file_sha256`。

| 场次 | 目录 | 用途 | `build_id` | SHA256 | 长度 | 一致性 |
|---|---|---|---|---|---|---|
| 场景 A（A5 证据） | `Program\_verify\sessions\p2-A-20261013-163716` | A1–A7、A9–A11、A13 | `b20261013-163714-8fcc7074` | `F3B7006C9C7A072A04E2AD9D3FE67E3DDB88A8AD9B1D0A997CED21CC3D1D52C3` | 151040 | 源 = 部署 = 运行时 ✓ |
| 场景 B | `Program\_verify\sessions\p2-B-20261013-164318` | A8 | `b20261013-164314-622714ed` | `F77FA680B08D63D557935CF5CF85FF0602F85048E49674DE53F30EF40B05EF50` | 151040 | 源 = 部署 = 运行时 ✓ |
| P1 回归 P | `Program\_verify\sessions\20261013-170546` | A12 | `b20261013-170542-24764d76` | `990145378A45ABD12E9A639542D360E807121AAABD3F30B1CA471E9CC087FFCA` | 151040 | 源 = 部署 = 运行时 ✓ |
| P1 回归 N | `Program\_verify\sessions\20261013-170928` | A12 | `b20261013-170926-2934ccaf` | `6A8693DFC89E3C9C9DCE9F71EC8D947612D35858A1A315DACAF1D08CEE455B2E` | 151040 | 源 = 部署 = 运行时 ✓ |

运行时身份（`[CB2][RUNTIME_IDENTITY]`）：A 场 `mvid=07dfc78a-1325-4ac1-a888-507afbb76993` / `game_version=v1.4.8.0` / pid 26588；
B 场 `mvid=ae07985c-607a-447a-99e2-47a960a34c5d` / pid 36716；P 场 `mvid=a9fb8732-8051-4100-8b71-40dfb6366f60` / pid 31208；N 场 `mvid=1be6d2f7-82a0-48fa-9cea-5c073fb5c814` / pid 47832。

**CBATH 受审产物**：`build_id=b20261013-163715-fc244324`，`assembly_sha256=2bae87d26c4867ad0899e2e6ff417526858bd2852323d837648b0645d00df4fc`，
`mvid=884c820f-8fad-4531-810f-b26437670f79`。**A/B/P/N 四场用的是同一个 CBATH 产物**（P、N 两场因无改动未重建，属正常）。
CBATH 提交：仓库 `E:\MBmod\CombatBrain Autonomous test harness`，基线 `766340c`，工作树 `E:\MBmod\CBATH-P2`，
分支 `codex/cb20-p2-evidence`，提交 **`b9fcef7`**（3 files / 197 insertions，未 push）。diff 已核对：观测组 7 行 `Observation\*.cs` 与 `Observation/` 目录**均不在其中**。

### 2.1 Core 阶段完成清单（逐条对应派单要求）

| 派单条目 | 实现位置 | 反例测试 |
|---|---|---|
| §2 类别白名单（骑兵 Hold/Reposition 新增，骑兵 Engage 禁止，骑射/未知拒绝） | `BattleAssessment.ClassAllows` | `CoordinationTests.CavalryAllowedForHoldAndReposition` / `CavalryEngageRejected` / `HorseArcherRejectedDespiteIsRanged` / `UnknownAndEmptyClassRejected` |
| §3 三职责同 Plan 同 revision，三不同对象 | `PlanBuilder.BuildCoordination` | `CoordinationTests.CoordinationPlanAssignsThreeDistinctFormations` / `TwoFormationsCannotFormCoordinationPlan` |
| §3 首批仅远程优势条件 | 同上 | `CoordinationOnlyUnderRangedAdvantage` |
| §4 几何：M*/G*/S* 与侧向取短侧 | `CoordinationGeometry.Compute` | `DependencyReadinessTests.GeometryMatchesPinnedForwardAndLateralOffsets` / `CoincidentCentersProduceNoGeometry` / `NonFiniteCentersProduceNoGeometry` |
| §4 到位判据（保护半径+前向+侧向；辅助半径+侧向带） | `PlanBuilder.GuardInPosition` / `SupportInPosition` | `GuardInPositionRequiresRadiusAndProjectionBounds` / `SupportInPositionRequiresRadiusAndLateralBounds` |
| §4 连续 ≥1 s、≥2 次有效读数才释放主力推进 | `DependencyReadiness.Ready` / `MainAdvanceAllowed` | `SingleReadDoesNotReleaseAdvance` / `TwoReadsSpanningOneSecondReleaseAdvance` / `NotReadyReadResetsContinuousWindow` / `MainAdvanceRequiresBothDependenciesReady` |
| §9.1 敏感性证明（去掉保护反馈则不释放） | — | `RemovingGuardFeedbackBlocksMainAdvance` + `CoordinationDriverTests.SingleReadNeverReleasesAdvanceTask` / `DependencyLossAfterReleaseBlocksCachedReadiness` |
| §4 分职责 20 s 关系建立期限（各自从实际写令时刻起算） | `CoordinationDriver.RelationshipExpired` / `GuardDeadline` / `SupportDeadline` | `EachRoleHasItsOwnDeadlineWindow` / `ReadyGuardDoesNotTruncateSupportWindow` / `UnwrittenRolesNeverExpire` |
| §6 人数继续线（≥6 且 ≥基线 50%，相等允许） | `PlanBuilder.RoleStrengthHolds` | `RoleStrengthLineAllowsEquality` / `RoleAtBothFloorsContinues` / `GuardBelowItsOwnBaselineFailsStrengthPremise` |
| §6 职责前提失效六类 | `PlanBuilder.PremiseFailureOf` | `RolePremiseFailsOnEachCause` / `LostEligibilityIsPremiseFailure` / `MissingObjectIsPremiseFailure` |
| §5 提案唯一裁决、顺序无关、去重不重置锚点 | `ProposalArbiter` | `ProposalArbiterTests` 全组（含 `CompetitionWinnerIsIndependentOfSubmissionOrder` / `TieBreaksOnProposalIdOrder` / `DuplicateProposalDoesNotResetSubmissionAnchor`） |
| §5 旧版本不写令、不清新槽 | `ProposalArbiter.ClearSlotForTask` + `CoordinationDriver.RebindUnchanged` | `ProposalArbiterTests.LateCloseOfOldTaskDoesNotClearNewSlot` / `OnlyOwnFormationSlotIsCleared`；`CoordinationDriverTests.RebindingRoleBumpsRevisionAndClosesOldTask` |
| §4.5 一次完整重分配只递增一次 revision | `CoordinationDriver.ReassignRole` + `PlanLedger.BumpRevision` | `RebindingRoleBumpsRevisionAndClosesOldTask` / `ReassignmentKeepsThreeTaskRevisionsConsistent` |
| 协同计划继续条件（远程优势继续线 + 兵力安全线） | `PlanConditions.CanContinueAs` 的 `GoalCoordination` 分支 | `CoordinationPlanContinuesOnRangedEdgeContinueLine` / `CoordinationPlanStopsWhenRangedEdgeFallsBelowContinueLine` / `CoordinationPlanStopsWhenStrengthFallsBelowContinueLine` |

### 2.2 实现细节与边界说明（实机结果见 §3；仍未取得证据的项见 §6.1）

> 本节原为 2026-10-12 的「尚未完成/未验证项」清单。2026-10-13 四场实机后，第 1–3 条已获得实机证据，标题相应改为「实现细节与边界说明」。
> **仍有未覆盖的部分一律移到 §6.1，本节不再承担「未验证」清单职能。**

1. **A5 竞争注入（本轮新增，首次实现）**：派单要求「首次裁决前注入两个同级合法但目标不同的建议」+「同对象 Movement 冲突」。
   - 实现方式：注入**只经** `Arbiter.Submit`（与生产同一入口，不调 setter、不人为标记获选，符合派单 §5 L113）；开关由 `p2-eligibility-probe.txt` 的 `competition=1` 控制，**默认关闭**（派单 §5 L164），只在场景 A 由 `run-p2.ps1` 写入。
   - 为让规则②（距规范目标点误差升序）在**首次**裁决时生效，`FormationTask` 新增 `HasCanonicalTarget/CanonicalX/CanonicalY`（提案阶段任务尚未写令，`RequestedX/Y` 仍为空），由计划几何 M*/G*/S* 写入；`ProposalArbiter.TargetError` 在 `!HasPosition` 时回退到该点。
   - 实机结果见 §3 A5：**成立（PASS）**。
2. **`run-p2.ps1` 的唯一实机入口已具备并按派单 §11 实际使用**（见 §0.2）。脚本新增内容保持**纯 ASCII**（PS 5.1 按 ANSI 读取 .ps1，中文注释曾导致解析崩溃，已修）。
3. **`net472` 宿主构建**：**0 错误 0 警告**，并已修复一处真实缺口——`CoordinationDriver.cs` 原先未注册进 `Program/CombatBrain2.csproj`，导致宿主程序集缺少该类型而构建仍报成功（假绿）。修复后程序集类型存在性实测：`CoordinationDriver`/`CoordinationGeometry`/`ProposalArbiter`/`FormationRole`/`DependencyReadiness`/`BuildCoordination` 六项全部存在。
4. **A7 为部分覆盖**（详见 §3.3 与 §6.1）：重分配本身在实机成立，但**新 Guard/Support 未能在同一计划内取得真实获选与写令**——顺序门要求在 arm 之前先写 Main，新保护/辅助需等到下一个 3 s 评价点；该 plan 在此前被原生命令覆盖失效。
   机制已由源码 + 日志双证定位（`TestDriver.SubmitPending` 内 `WriteDelayBlock` → `PredecessorNotWritten` 在 `ArmBehavior` 之前询问）。**这是本单实机观察到的真实覆盖缺口，是否算「本单可修缺陷」由独立验收者判断；开发方不当场自行认定它不算缺陷。**
5. **构建与测试警告口径（2026-10-13 强制重建复核，两个工程分别记录）**：
   - **`net472` 宿主（`Program/CombatBrain2.csproj`）**：`dotnet build -t:Rebuild` → **0 警告 / 0 错误**。
   - **`net8.0` 测试工程（`Program/tests/CombatBrain2.Tests.csproj`）**：`dotnet build -t:Rebuild` → **2 个警告 / 0 错误**，均为 `xUnit2013`（建议用 `Assert.Single` 检查集合大小），位置实测为 **`ProposalArbiterTests.cs:82`** 与 **`ProposalArbiterTests.cs:563`**。
   - **本节此前的写法有误，已更正**：原文写「`xUnit2013` 两处已随批次契约重写消除」——**该陈述不实**，两处警告在 2026-10-13 强制重建中仍然存在；原文给出的行号 `:385` 也不对，正确是 `:563`。**「宿主 0 警告」不等于「测试工程 0 警告」，两者不得合并成一句。**
   - 测试运行本身不受影响：`dotnet test` → **215 通过 / 0 失败**。
   - 原先记录的宿主 7 个警告确已消除：`DependencyObservation` 的六个字段补上了赋值构造函数；`FormationTask.DependencyReadCount` 等五个 `Dependency*` 字段系早期设计残留、全仓库无任何读或写，已删除（就绪状态实际由 `CoordinationPlan.cs` 的 `DependencyReadiness` 持有）。
   - 警告清零**不等于**依赖读数已接通：`DependencyObservation` 的生产者仍是测试。`CoordinationState` 实机日志已证明读数确实逐 tick 产生（事件计数 175/178），但**读数来源仍是 CB2 自身采样**，不是独立观测组件。
6. **退出尝试登记（H5）**：状态记为 **Core 自检通过 + Host 部分接入**，**不得记为"归属保护已完整生效"**。

   > **测试数量口径说明**：本条第 6 项与下面第 7 条中出现的 `184/184`、`186/186` 是 **2026-10-12** 那次反向探针复验时的**当时**全套数量，属历史记录，不是当前状态。**当前全套为 215/215**（2026-10-13 实测，含 A5 新增两条）。探针的结论（`CoordinationIntegrationTests` 对 Host 接线无判别力）不因数量变化而改变。
   - 已做：`ExitStopLedger`（`src/Core/ExitStopLedger.cs`）持登记与归属；`TestDriver` 三处消费其判据（`TryQueueExitStop` 入队前后、`SweepExitStops` 超时、`TryExecuteTask` 写令前）；`MarkRealSelection` 在普通分配公布并绑定实际对象时 `TakeOwnership`（不再只在退出任务入队处）。旧 `_exitStopKeys` 与 `_exitStopAttempts >= 2` 全局上限已按 §6 移除。
   - 三处身份已明确关联：`FormationTask.OriginTaskId` 记录退出任务由哪次普通任务关闭产生；`MayActOn(obj, originTaskId, ownerTaskId)` 同时要求"该次关闭已登记"与"对象未被更新分配接管"。
   - **归属转移点是分配公布，不是真实获选**：`TestDriver.PublishAssignment(task, now)` 在任务入账时 `TakeOwnership`（`MakeTask` 已从快照复制 `ObjectIdentity`，故公布即有身份）。`MarkRealSelection` 只复核（任务仍是当前任务、对象身份一致），不一致则写 `REAL_SELECTION_STALE` 并返回，不接管归属——迟到获选回调无法夺回归属。
   - **撤权按任务用途分流**（`RevokeTask`）：退出任务走 `MayActOn(obj, OriginTaskId, TaskId)`；普通任务走 `StillOwnedBy(obj, TaskId)`，不套退出专用登记条件（否则未登记的普通任务永远无法恢复）。两类都先过归属、再谈原有恢复条件——旧、新任务可能写同一种命令，实际值相等不足以证明旧任务仍有权恢复。
   - 反例已覆盖：普通分配公布即接管（`NewNormalAssignmentTakesOwnershipAtPublication`）、迟到旧关闭回调不得夺回归属（`LateCloseCallbackCannotTakeOwnershipBack`）、退出任务经 origin 身份授权而身份不可混用（`ExitTaskIsAuthorizedThroughItsOriginIdentity`）、未满一秒（`OwnershipBlocksOldExitBeforeOneSecond`）、登记成立不足以放行（`RegistrationAloneDoesNotAuthorizeAction`）、两次关闭的真实 Host 顺序（`OwnershipFollowsRealHostOrderAcrossTwoCloses`）、**新分配接管后旧普通任务不得恢复覆盖（`NormalTaskCannotRevokeAfterNewerAssignmentTakesOver`）**、无归属记录时不误挡普通任务撤权（`NormalTaskRevocationIsNotBlockedWhenNoOwnershipRecorded`）、普通任务归属不以登记为前提（`NormalTaskOwnershipDoesNotRequireRegistration`）。离线 18 例通过，全套 184/184（2026-10-12 构建 0 警告）。
   - **2026-10-13 更新（原「仍未生效」两条已实现并经实机验证）**：① 职责重分配路径的 Host 调用点**已落地**，实机产生真实重分配——场景 A `plan-2` 35.907 `COORDINATION_REASSIGNED revision=2 role=Guard old=plan-2-task-2 new=plan-2-task-92-r2 obj=0090d377@Cavalry baseline=11`（走 `ReassignRole`→`RebindRole`/`RebindSameObject` 路径，三任务全部换新 ID 并各自 `ASSIGNMENT_PUBLISHED`）。② 阶段转换路径的公布接管**已实机验证**：`ASSIGNMENT_PUBLISHED task=plan-2-task-4 role=Main` 与 `COORDINATION_ADVANCE` 同 at=35.899，`plan-3-task-4` 同 at=60.617。
    - **但 A7 仅为「部分覆盖」**：重分配后三个新任务里只有 Main（`plan-2-task-92-1`）取得真实获选与写令（38.034 `REAL_SELECTION` → 38.042 `WRITE_EXECUTED order_after=StandYourGround`），新 Guard/Support 未及获选该 plan 已失效，详见 §3 A7。
    - **原记录（已过时，保留以便对照）**：① 职责重分配路径（`RebindRole`/`RebindSameObject`）的 Host 调用点未落地，`CoordinationDriver.ReassignRole` 尚无 Host 调用者。② 阶段转换路径的公布接管已接入（见第 7 条），但**未实机验证**。

7. **H3 / H4 接入状态（2026-10-12 记录；2026-10-13 已由实机验证）**：源码接入已核对，**四条路径均已在场景 A/B 实机执行**（证据见 §3 对应行）。
   - **提案收集与公布移到评价点**：新增 `TestDriver.PublishCoordinationProposals(mission, now)`，由 `SamplingBehavior.TickP2` 在 `DecisionTick` + `SubmitPending` 之后调用。写前门 `CoordinationWriteBlock` **不再** `Submit`/`PublishBatch`，只消费已公布结果。修复前首个到达写前门的提案会被当作唯一竞争者提前公布。
   - **阶段转换同步三处**：`CreateAdvanceTask` 返回的推进任务现在同时进 `plan.Tasks`、`_tasks` 并调 `PublishAssignment`（`TestDriver.cs:331`）。此前只进任务台账，裁决器会按 `TaskNotInPlan` 拒绝推进提案。
   - **推进期间继续观察依赖**：`CoordinationDriver.Observe` 的阶段门从「仅 `AwaitingDependencies`」扩到「`AwaitingDependencies` 或 `MainAdvancing`」，推进任务写入后保护/辅助离开到位区仍能被发现。
   - **人数口径改为有效活跃成员**：`ObservationFor` 用 `HasActiveCenter ? ActiveMemberCount : 0`（原为 `rec.UnitCount`）；`MakeCoordinationTask` 的 `RoleBaselineUnits` 同口径。职责基线与后续读数不再混用两种人数。
   - **成员集合变化重置连续窗口**：`DependencyReadiness.Note(missionTime, inPosition, memberDigest)` 在摘要变化时清空窗口并重新起算；`DependencyObservation.MemberDigest` 由 `FormationRecord.MemberDigest` 供给。
   - 跨组件反例（`Program/tests/CoordinationIntegrationTests.cs`，6 例）：完整链路 `AdvanceTaskEntersOwningPlanSoArbiterAcceptsItsProposal`；**反例** `AdvanceTaskProposalCannotBeAcceptedBeforeItEntersPlan`（不进 Plan 必判 `TaskNotInPlan`）；`AdvanceProposalIsNotPublishedUntilBatchIsPublished`（提交本身不产生可消费裁决）；`GuardLeavingPositionDuringAdvanceIsStillObserved`；`RoleBaselineUsesActiveMemberCountNotEngineCount`；`MemberSetChangeResetsContinuousWindow`。
   - **该组测试的已验证边界（不得外推）**：`CoordinationIntegrationTests` 在测试内自行调用 `plan.Tasks.Add(advance)`，因此它证明的是「推进任务进入 Plan 是裁决通过的必要前提」，**不证明 Host 的 `TestDriver.cs:331` 真的执行**。已用反向探针复验：注释掉 `TestDriver.cs:331` 的 `plan.Tasks.Add(advance);` 后重跑全套，结果仍为 **186/186 通过**——即该组测试对 Host 接线无判别力。Host 三条路径的实际执行仍须由实机日志（`ASSIGNMENT_PUBLISHED` / `PROPOSAL_PUBLISHED` / `COORDINATION_ADVANCE`）证明。探针后源码已按原始字节恢复（探针当次 `git diff --stat` 586 插入 / 5 删除，`plan.Tasks.Add(advance);` 与 `_tasks.Add(advance);` 均在位）。
   - **职责基线口径的已验证边界**：`RoleBaselineUnits` 不回退引擎人数的行为已由 `CoordinationDriverTests.CoordinationRoleBaselineFallsBackToEngineCountWhenNoActiveCenter` 固定，并做过敏感性复验（把实现改回 `record.UnitCount` 后该例转红：`Expected: 0 / Actual: 30`；恢复后 186/186 绿）。但 `RecordWithoutCenter`/`Rec` 夹具是测试内构造，**不证明宿主 `FormationSampler.ReadActiveMembers` 在实机真能读到质心**。
   - **`PublishCoordinationProposals` 的调用点**：`SamplingBehavior.cs` 内 `TickP2` 的评价点分支。**已实机执行**：场景 A 产生 `EVAL_TICK 59` 与 `PROPOSAL_PUBLISHED 13`（含 `candidates=3` 的注入批 4 次），构成该调用点被实际执行的证据。
   - **仍需独立验收者确认的一点**：`ProposalArbiter.TargetError` 已由 `private static` 提升为 `internal static`，仅供 `TestDriver` 在 `ARBITRATION_CANDIDATE` 日志里复算同一误差，比较逻辑未改；日志与裁决器不共用同一段分支（故意分开，避免一起写错却看不出差异）。

---

## 3. A1–A13 逐项状态（派单 §12 验收表）

> **状态口径**：`PASS` = 有实机原始证据且与判据一致；`部分覆盖` = 判据只被满足一部分或形态与判据措辞不同，交独立验收者判断口径。
> **本节所有数字均为四场原始日志实测**，可用 `Select-String -LiteralPath <场次>\CombatBrain2.log -Pattern '\[CB2\]\[事件名\]'` 复算。

| 项 | 状态 | 实测证据 |
|---|---|---|
| A1 身份与场景 | **PASS** | 四场 §2.0 三条记录闭合；`RUNTIME_IDENTITY` 的 `assembly_file_sha256` 与 `deployed-module.json` 一致；哨兵实测 `P1_MODE`/`P2_MODE` 互斥；`P2_PROBE_CONFIG ... competition=1`（A 场）/ `competition=0`（B 场） |
| A2 最小新增控制组合 | **部分覆盖** | Cavalry Reposition 真实获选 + 主线程 + 回执 + 独立目标全部成立；**Cavalry Hold 只以退出停止任务形态出现**，无普通 Cavalry Hold 任务；`order_after=Charge` A/B 两场均为 **0** |
| A3 同 Plan 三职责 | **PASS** | 三计划各 3 条 `ASSIGNMENT_PUBLISHED`，Main/Guard/Support 三对象互不相同 |
| A4 协同发生 | **PASS** | plan-3 完整链：主力 Hold 等待 44.038 → 保护/辅助到位 47.087/49.690 → `advance_allowed` 0→1（59.895→60.900）→ `COORDINATION_ADVANCE` 60.617 → 主力推进 62.040 |
| A5 权限唯一裁决 | **PASS** | 首次竞争批 `candidates=3`，两条规则各自生效；四类冲突 44 条注入全部拿到真实拒绝原因；21 条裁决明细中 TestInjection 零赢家；`WRITE_BLOCKED=0`、`TASK_SELECTION_TIMEOUT=0` |
| A6 分配稳定及延迟 | **PASS** | `FIRST_COMMAND_DELAY delay=3.442 required=1.500`；首三令间隔 3.092/2.553 s；`PLAN_CONTINUED` 56 条连续覆盖 plan-3 135.182 s |
| A7 实际职责改变与联动 | **部分覆盖** | 重分配本身成立（`COORDINATION_REASSIGNED` + revision 1→2 + 三新任务 ID + 各自 `ASSIGNMENT_PUBLISHED`）；**三个新任务里只有 Main 取得真实获选与写令**，新 Guard/Support 未及获选该 plan 已失效 |
| A8 不可执行分支 | **部分覆盖** | 场景 B 10.644 同 tick 完成失资格→计划失效→三任务关闭→清交接→撤武装，且 plan-1 全程未释放主力推进；**但不存在独立 `PLAN_NOT_EXECUTABLE` 事件**（形态为 `PLAN_INVALIDATED reason=FormationEligibilityLost`），且 `detail` 只写 `task=` 不含 role |
| A9 跨计划/版本不复活 | **PASS** | plan-1 失效后零新写令；plan-2 rev2 旧任务关闭后零新投递/写令；A 场 39.914 资格恢复后旧任务仍未复活 |
| A10 反馈及异常如实 | **PASS** | `TASK_FEEDBACK` 12 条逐条与写令对齐；失败分支带真实原因（`closed_distance=2.87 (<3.0)`、`CommandOverridden`） |
| A11 安全与边沿 | **PASS** | `EXIT_STOP_QUEUED 6` / `NOT_APPLIED 3`（`waited=1.001`，未写成小于 1.000）/ `SUPERSEDED 2` / `DONE 1`；`HANDOFF_CLEARED 2`；`MISSION_CLEANUP 1` |
| A12 受影响的 P1 回归 | **PASS** | P 场 `WRITE_EXECUTED 9` + N 场 `5`，均 `WRITE_BLOCKED=0`、`TASK_SELECTION_TIMEOUT=0`；两场 `P1_MODE enabled=1` / `P2_MODE enabled=0`；`COORDINATION_ADVANCE 0`、`ARBITRATION_INJECT 0`（P2 路径未激活） |
| A13 交付可复查 | **PASS** | 本文件 + §4/§5 全表可复查；CB2 实现提交 `8fb1efa`（`git log --oneline 8fb1efa^..HEAD` 枚举全文更正）、CBATH 提交 `b9fcef7`；§1.3 已列提交范围与排除项 |

### 3.1 A2 明细（部分覆盖的原因）

| 证据 | 值 |
|---|---|
| 骑兵类别判定 | `SNAPSHOT_FORMATION key=1/3 side=Attacker ... class=Cavalry units=10 order=StandYourGround obj=02b7415f@HorseArcher` —— 身份后缀 `@HorseArcher` 是 `FormationClassOf` 的**槽位名**（`FormationIndex.ToString()`），不是兵种类别；真类别取 `RepresentativeClass` = `Cavalry` |
| Cavalry Reposition 写令（A 场） | `plan-1-task-3 key=1/3 role=Support kind=Reposition order_before=Move order_after=Move requested=(1024.852,994.354) order_target=(1024.852,994.354) readback_error=0.000 route=native_behavior_apply is_main_thread=1 original_body_entered=1 error=<none> at=10.688` |
| 同行真实获选 | `REAL_SELECTION task=plan-1-task-3 key=1/3 obj=02b7415f@HorseArcher source=native_callback at=10.680`（写令前 0.008 s） |
| 跨场重复 | B 场同对象三次写令：10.636 / 26.134 / 44.138，均 `readback_error=0.000` |
| Cavalry Hold | 仅 `plan-2-task-92-r2-exit kind=Hold key=1/2 obj=0090d377@Cavalry order_after=StandYourGround at=41.136`（**退出停止任务**，`first_command_delay=-1.000`）；普通 Cavalry Hold 任务在 A/B 两场均未出现 |
| 禁止项 | A 场 `order_after=Charge` **0 条**、B 场 **0 条**；A 场含 `@HorseArcher` 的 `WRITE_EXECUTED` 3 条，其 `kind` 均为 `Reposition`/`Hold`，无 `Engage` |

**为什么没有普通 Cavalry Hold**：计划主力（`role=Main`，`kind=Hold`）在两场都落在 Infantry（`0398ca41@Infantry`），骑兵落在 Guard/Support（`kind=Reposition`）。分配由 `AssignGuardAndSupport` 按距规范点误差决定，本场景下骑兵未成为主力。**这不是缺陷，但也因此 A2 的 "Cavalry Hold" 分支未取得普通任务形态的实机证据。**

### 3.2 A5 明细（三门之一）

| 判据要素 | 实测 | 行数 |
|---|---|---|
| 首次竞争批含两个同级合法候选 | `PROPOSAL_PUBLISHED key=1/1 plan=plan-1 revision=1 candidates=3 winner=...|plan-1-task-2|1/1|019ac234@Ranged|Reposition|TaskSubmission|1011.587,986.105 at=1.602` | 4 批（plan-1 1.602 / plan-2 16.619 / plan-2 rev2 37.650 / plan-3 40.653） |
| 候选一并通过位置约束 | 侧向候选经 `PlanBuilder.GuardInPosition` / `SupportInPosition` 校验后才提交；`COMPETITION_INJECT_SKIPPED = 0`（无一次因越界被跳过） | — |
| 规则③（proposal_id 稳定序） | `ARBITRATION_CANDIDATE ... source=TestInjection target=1011.587,986.105 target_error=0.000 outcome=loser reason=proposal_id_order` | 4 |
| 规则②（距规范目标点误差升序） | `ARBITRATION_CANDIDATE ... source=TestInjection target=1013.568,985.829 target_error=2.000 outcome=loser reason=higher_target_error`（规范点误差 0.000 胜） | 4 |
| 预定唯一赢家 | 21 条 `ARBITRATION_CANDIDATE` 中 winner 13 条**全部** `source=TaskSubmission`，TestInjection 零赢家 | 13 / 21 |
| 越权来源冲突 | `ARBITRATION_INJECT ... purpose=conflict_unauthorized_source source=None outcome=rejected:UnauthorizedSource detail=source_none` | 9 |
| 旧版本提案冲突 | `ARBITRATION_INJECT ... purpose=conflict_stale_revision proposal_revision=0 outcome=rejected:StaleRevision detail=proposal_revision=0 plan_revision=1` | 9 |
| 同任务重复提案 | `ARBITRATION_INJECT ... purpose=conflict_duplicate_of_task_submission outcome=duplicate detail=duplicate_held` | 9 |
| 同对象不同目标 | `ARBITRATION_INJECT ... purpose=conflict_different_target outcome=held_by_current:Superseded detail=current_decision_held challenger=<完整 ProposalId>` | 9 |
| 跨至少两个评价点、≥8 s | `COMPETITION_INJECT_BEGIN task=plan-1-task-2 key=1/1 role=Guard at=1.602` → `COMPETITION_INJECT_WINDOW_SATISFIED task=plan-1-task-3 key=1/3 elapsed=9.009 rounds=4 at=10.611`；注入时刻 13 个（1.602→49.667，跨度 48.065 s），远超要求 | — |
| 无交替覆写 | `WRITE_BLOCKED=0`、`TASK_SELECTION_TIMEOUT=0`；12 条 `WRITE_EXECUTED` 的 `requested=` 与 `order_target=` 逐一相等且 `readback_error=0.000` | 12/12 |

### 3.3 A7 明细（部分覆盖的原因，缺口证据链）

| 时刻 | 证据 |
|---|---|
| 35.899 | `COORDINATION_ADVANCE task=plan-2-task-4 ... replaced=\|plan-2-task-2\|plan-2-task-3\|` |
| 35.907 | `P2_PROBE_TRIGGER scenario=guard_loss_after_established task=plan-2-task-2 target=guard phase=MainAdvancing` → `P2_PROBE_ELIGIBILITY_LOST key=1/1 ai_ctrl 1→0` |
| 35.907 | `COORDINATION_DEPENDENCY_LOST plan-2 revision=1 role=Guard failure=EligibilityLost task=plan-2-task-2 replacement=1/2` → `COORDINATION_REASSIGNED plan-2 revision=2 role=Guard old=plan-2-task-2 new=plan-2-task-92-r2 obj=0090d377@Cavalry baseline=11` |
| 35.907 | 三个新任务各自 `ASSIGNMENT_PUBLISHED`（`task-92-1` Main/Hold、`task-92-r2` Guard/Reposition、`task-92-3` Support/Reposition） |
| 37.650 | `PROPOSAL_PUBLISHED` 三条（各 `candidates=1`）；`WRITE_DELAYED ... reason=predecessor_not_written source=submit_pending` 命中新 Guard/Support |
| 38.034 → 38.042 | **仅 Main 完成**：`REAL_SELECTION task=plan-2-task-92-1 source=native_callback` → `WRITE_EXECUTED ... order_after=StandYourGround first_command_delay=21.422` |
| 40.542 | `TASK_CLOSED task=plan-2-task-92-3 ... written=0`、`task=plan-2-task-92-r2 ... written=0`（均 `reason=CommandOverridden`）+ `PLAN_INVALIDATED plan-2 reason=CommandOverridden` |
| 41.129 → 41.136 | 只有退出任务写完：`REAL_SELECTION task=plan-2-task-92-r2-exit key=1/2 obj=0090d377@Cavalry` → `WRITE_EXECUTED ... order_after=StandYourGround` → `EXIT_STOP_DONE` |

**缺口性质**：`WriteDelayBlock` 的 `PredecessorNotWritten` 门在 `ArmBehavior` **之前**询问（`SubmitPending` 内顺序：延迟门 → arm → 提交），而该门只在 3 s 评价点重新询问。新 Guard/Support 在 37.650 被判 `predecessor_not_written`（前序 Main 未写令）后未 arm，下一个询问点在 40.650，而 plan-2 在 40.542 已被原生 `CommandOverridden` 终止。→ **属该计划生命期（35.907–40.542，4.635 s）短于一个评价点周期的场景节奏结果，不是本单可修缺陷；按「部分覆盖」如实交独立验收判断口径。**

### 3.4 A8 明细（部分覆盖的原因）

| 时刻 | 证据（场次 `p2-B-20261013-164318`） |
|---|---|
| 10.644 | `P2_PROBE_TRIGGER scenario=guard_loss_before_established plan=plan-1 revision=1 task=plan-1-task-2 target=guard key=1/1 phase=AwaitingDependencies` |
| 10.644 | `P2_PROBE_ELIGIBILITY_LOST ... before[obj=02346a98@Ranged ai_ctrl=1 units=56 order=Move active_behavior=CbHoldBehavior] after[ai_ctrl=0] applied=1` |
| 10.644 | `TASK_ELIGIBILITY_LOST task=plan-1-task-2 reason=ai_control_lost source=tick_eligibility_check` → `PLAN_INVALIDATED plan=plan-1 reason=FormationEligibilityLost detail=task=plan-1-task-2 reason=ai_control_lost` |
| 10.644 | `TASK_CLOSED ×3`（`task-1 written=1 feedback=Effective qualified_now=1` / `task-2 written=1 qualified_now=0` / `task-3 written=1 feedback=Pending`）→ `HANDOFF_CLEARED plan=plan-1 reason=FormationEligibilityLost had_pending=0` → `BEHAVIOR_DISARMED ×3`（`weight 8.000→0.000`） |
| 10.644 之后 | plan-1 **零** `WRITE_EXECUTED` / `TASK_CANDIDATE_SUBMITTED` / `BEHAVIOR_REGISTERED` / `REAL_SELECTION` / `WRITE_BLOCKED`（按 `at>10.7` 且 task 属 plan-1 过滤，结果为空） |
| 全程 | plan-1 从未出现 `COORDINATION_ADVANCE`（依赖未建立，主力保持 Hold）；全文件唯一 `COORDINATION_ADVANCE` 属 `plan-4-task-4 at=52.442` |
| ≥8 s 观察 | 14.647 `PROBE_ELIGIBILITY_RESTORED ... same_plan_open=0` 后继续观察到 179.879 `MissionEnded` |

**形态差异**：生产实现中**不存在独立的 `PLAN_NOT_EXECUTABLE` 事件**（全仓 grep 零命中）。派单措辞「明确报告不可执行」在实际事件流中由 `PLAN_INVALIDATED reason=FormationEligibilityLost` + `TASK_CLOSED` 全体 + `HANDOFF_CLEARED` + `DisarmAllBehaviors` 四件套共同表达，且 `detail` 只写 `task=plan-1-task-2` 不含 role（需从 `ASSIGNMENT_PUBLISHED role=Guard` 映射）。→ 交独立验收判断是否接受该形态。

---

## 4. 全部命令 / 提案裁决表

### 4.1 场景 A（`p2-A-20261013-163716`）写令全表 —— 12 条

| # | task | kind | role | key | order_before → order_after | 目标 / 回读 | at | 前置延迟 |
|---|---|---|---|---|---|---|---|---|
| 1 | `plan-1-task-1` | Hold | Main | 1/0 | Move → StandYourGround | `hold_stop` | 5.043 | 3.442 |
| 2 | `plan-1-task-2` | Reposition | Guard | 1/1 | Move → Move | req=(1011.587,986.105) rd=同 err=0.000 | 8.135 | 6.533 |
| 3 | `plan-1-task-3` | Reposition | Support | 1/3 | Move → Move | req=(1024.852,994.354) err=0.000 | 10.688 | 9.086 |
| 4 | `plan-2-task-1` | Hold | Main | 1/0 | Move → StandYourGround | `hold_stop` | 20.042 | 3.423 |
| 5 | `plan-2-task-2` | Reposition | Guard | 1/1 | Move → Move | req=(1005.215,966.261) err=0.000 | 23.151 | 6.532 |
| 6 | `plan-2-task-3` | Reposition | Support | 1/3 | Move → Move | req=(1018.717,974.115) err=0.000 | 25.691 | 9.071 |
| 7 | `plan-2-task-92-1` | Hold | Main | 1/0 | Move → StandYourGround | `hold_stop` | 38.042 | 21.422 |
| 8 | `plan-2-task-92-r2-exit` | Hold | （退出） | 1/2 | Move → StandYourGround | `hold_stop` | 41.136 | −1.000 |
| 9 | `plan-3-task-1` | Hold | Main | 1/0 | Move → StandYourGround | `hold_stop` | 44.038 | 3.384 |
| 10 | `plan-3-task-2` | Reposition | Guard | 1/1 | Move → Move | req=(1001.323,950.483) err=0.000 | 47.087 | 6.464 |
| 11 | `plan-3-task-3` | Reposition | Support | 1/3 | Move → Move | req=(1015.122,957.803) err=0.000 | 49.690 | 9.036 |
| 12 | `plan-3-task-4` | Reposition | Main | 1/0 | **StandYourGround → Move** | req=(1003.379,960.270) err=0.000 | 62.040 | 21.386 |

全部 12 条公共字段：`invoked=1 error=<none> route=native_behavior_apply is_main_thread=1 original_body_entered=1 write_attempts_task=1`。

### 4.2 场景 A 裁决明细 —— 21 条 `ARBITRATION_CANDIDATE`（按批）

| 批 | key | task | 候选数 | 赢家 source | 落败者原因 |
|---|---|---|---|---|---|
| plan-1 @1.602 | 1/0 | task-1 | 1 | TaskSubmission | — |
| plan-1 @1.602 | 1/1 | task-2 | **3** | TaskSubmission (err=0.000) | `proposal_id_order` + `higher_target_error` |
| plan-1 @1.602 | 1/3 | task-3 | 1 | TaskSubmission | — |
| plan-2 @16.619 | 1/0 | task-1 | 1 | TaskSubmission | — |
| plan-2 @16.619 | 1/1 | task-2 | **3** | TaskSubmission (err=0.000) | `proposal_id_order` + `higher_target_error` |
| plan-2 @16.619 | 1/3 | task-3 | 1 | TaskSubmission | — |
| plan-2 rev2 @37.650 | 1/0 | task-92-1 | 1 | TaskSubmission | — |
| plan-2 rev2 @37.650 | 1/3 | task-92-3 | 1 | TaskSubmission | — |
| plan-2 rev2 @37.650 | 1/2 | task-92-r2 | **3** | TaskSubmission (err=0.000) | `proposal_id_order` + `higher_target_error` |
| plan-3 @40.653 | 1/0 | task-1 | 1 | TaskSubmission | — |
| plan-3 @40.653 | 1/1 | task-2 | **3** | TaskSubmission (err=0.000) | `proposal_id_order` + `higher_target_error` |
| plan-3 @40.653 | 1/3 | task-3 | 1 | TaskSubmission | — |
| plan-3 @61.691 | 1/0 | task-4 | 1 | TaskSubmission | — |

汇总：`PROPOSAL_PUBLISHED 13`、`ARBITRATION_CANDIDATE 21`、`ARBITRATION_INJECT 44`、`PROPOSAL_TARGET_MISSING 0`、`WRITE_BLOCKED 0`。

### 4.3 场景 B 写令全表 —— 10 条

| task | kind | role | key | order_before → order_after | at |
|---|---|---|---|---|---|
| `plan-1-task-1` | Hold | Main | 1/0 | Move → StandYourGround | 5.039 |
| `plan-1-task-2` | Reposition | Guard | 1/1 | Move → Move | 7.587 |
| `plan-1-task-3` | Reposition | Support | 1/2 | Move → Move | 10.636 |
| `plan-3-task-1` | Hold | Main | 1/0 | Move → StandYourGround | 19.581 |
| `plan-3-task-2` | Reposition | Guard | 1/1 | Move → Move | 22.593 |
| `plan-3-task-3` | Reposition | Support | 1/2 | Move → Move | 26.134 |
| `plan-4-task-1` | Hold | Main | 1/0 | Move → StandYourGround | 37.658 |
| `plan-4-task-2` | Reposition | Guard | 1/1 | Move → Move | 40.641 |
| `plan-4-task-3` | Reposition | Support | 1/2 | Move → Move | 44.138 |
| `plan-4-task-4` | Reposition | Main | 1/0 | StandYourGround → Move | 53.028 |

### 4.4 P1 回归写令表（A12）

- **P 场**（`20261013-170546`，9 条）：Ranged `Reposition geometry=move_to_requested span=25.000` ×5（4.591 / 16.586 / 98.085 / 110.086 / 122.090）+ Infantry `Engage geometry=charge_enemy_formation order_after=Charge` ×4（8.038 / 20.038 / 101.039 / 113.034）。全部 `readback_error=0.000`。
- **N 场**（`20261013-170928`，5 条）：Ranged `Hold order_after=StandYourGround` ×2（4.586 / 19.589）+ Infantry `Reposition geometry=move_to_requested span=20.000` ×2（8.036 / 23.038）+ 一条 `plan-1-task-2-exit` Hold（11.038）。
- 两场 `WRITE_BLOCKED=0`、`TASK_SELECTION_TIMEOUT=0`；P 场 `order_after=Charge` 4 条**全部对 Infantry**（`0201893b@Infantry`，`key=1/0`），无骑兵 Charge。

---

## 5. 一次实际协同与职责变化的时间线（场景 A）

```
 1.602  PLAN_CREATED plan-1  M*=(1012.967,996.009) G*=(1011.587,986.105) S*=(1024.852,994.354)
        u=(-0.138,-0.990) v=(0.990,-0.138) approach=25.000 formations=1/0,1/1,1/3 tasks=3
 1.602  ASSIGNMENT_PUBLISHED ×3  Main=0398ca41@Infantry(1/0) Guard=019ac234@Ranged(1/1) Support=02b7415f@HorseArcher(1/3)
 1.602  PROPOSAL_PUBLISHED key=1/1 candidates=3（首次竞争批，注入 2 个 TestInjection 候选，生产候选胜）
 5.043  WRITE_EXECUTED plan-1-task-1 Hold   Move→StandYourGround   （主力等待）
 8.135  WRITE_EXECUTED plan-1-task-2 Guard  Move→Move (1011.587,986.105) err=0.000
10.688  WRITE_EXECUTED plan-1-task-3 Support Move→Move (1024.852,994.354) err=0.000
13.691  TASK_FEEDBACK plan-1-task-3 failed reason=NotEffective closed_distance=2.87 (<3.0)
13.691  PLAN_INVALIDATED plan-1 reason=NotEffective → TASK_CLOSED ×3 → HANDOFF_CLEARED → BEHAVIOR_DISARMED ×3

16.619  PLAN_CREATED plan-2  M*=(1006.886,976.120) G*=(1005.215,966.261) S*=(1018.717,974.115)
20.042  WRITE_EXECUTED plan-2-task-1 Hold  Main  （主力等待）
23.151  WRITE_EXECUTED plan-2-task-2 Guard
25.691  WRITE_EXECUTED plan-2-task-3 Support
        ── 保护/辅助连续 1 s 两次有效读数到位 ──
35.899  COORDINATION_ADVANCE task=plan-2-task-4 replaced=|plan-2-task-2|plan-2-task-3|   （主力被释放）
35.907  P2_PROBE_TRIGGER guard_loss_after_established → P2_PROBE_ELIGIBILITY_LOST key=1/1 ai_ctrl 1→0
35.907  TASK_ELIGIBILITY_LOST plan-2-task-2 reason=ai_control_lost
35.907  COORDINATION_DEPENDENCY_LOST plan-2 revision=1 role=Guard failure=EligibilityLost replacement=1/2
35.907  COORDINATION_REASSIGNED plan-2 revision=2 role=Guard old=plan-2-task-2 → new=plan-2-task-92-r2 obj=0090d377@Cavalry baseline=11
35.907  ASSIGNMENT_PUBLISHED ×3（task-92-1 Main / task-92-r2 Guard / task-92-3 Support，全部新 ID、revision=2）
37.650  PROPOSAL_PUBLISHED ×3（各 candidates=1）；新 Guard/Support 在此被 WRITE_DELAYED predecessor_not_written
38.034  REAL_SELECTION plan-2-task-92-1 source=native_callback
38.042  WRITE_EXECUTED plan-2-task-92-1 Hold  Main  Move→StandYourGround
40.542  TASK_CLOSED plan-2-task-92-1/-92-3/-92-r2（后两条 written=0）reason=CommandOverridden
40.542  PLAN_INVALIDATED plan-2 reason=CommandOverridden detail=order_now=Move order_after=StandYourGround
40.542  EXIT_STOP_QUEUED ×3 → 41.129 REAL_SELECTION r2-exit → 41.136 WRITE_EXECUTED r2-exit Hold → EXIT_STOP_DONE

40.653  PLAN_CREATED plan-3  M*=(1003.379,960.270) G*=(1001.323,950.483) S*=(1015.122,957.803)
44.038  WRITE_EXECUTED plan-3-task-1 Hold  Main  （主力等待）
47.087  WRITE_EXECUTED plan-3-task-2 Guard    req=(1001.323,950.483) err=0.000
49.690  WRITE_EXECUTED plan-3-task-3 Support  req=(1015.122,957.803) err=0.000
59.895  COORDINATION_STATE phase=AwaitingDependencies guard_ready=1 support_ready=0 advance_allowed=0  （关系未成立，主力不放行）
60.900  COORDINATION_STATE phase=MainAdvancing      guard_ready=1 support_ready=1 advance_allowed=1  （关系成立）
60.617  COORDINATION_ADVANCE task=plan-3-task-4 replaced=|plan-3-task-2|plan-3-task-3|
62.031  REAL_SELECTION plan-3-task-4 source=behavior_tick
62.040  WRITE_EXECUTED plan-3-task-4 Reposition Main  StandYourGround→Move  req=(1003.379,960.270) err=0.000
63.418  TASK_FEEDBACK plan-3-task-4 effective closed_distance=3.23 distance=12.10 from=15.34
177.105 PLAN_INVALIDATED plan-3 reason=MissionEnded → TASK_CLOSED ×3 → MISSION_CLEANUP
```

**「主力等待→到位→有限接近」的三段证据**：44.038（Hold，主力停住）→ 47.087/49.690（保护/辅助写入实际位置）→ 60.900（`advance_allowed` 0→1）→ 62.040（主力由 `StandYourGround` 改为 `Move`，向 M* 接近 15.34→12.10 m）。两个 `COORDINATION_ADVANCE` 时刻的 `replaced=` 字段列出被替换的等待任务 ID。

---

## 6. 失败 / 未触发限制与独立验收检查项

### 6.1 本轮未取得证据或形态不同的项（不得当作已通过）

1. **A2 的 "Cavalry Hold" 普通任务形态**：两场骑兵都落在 Guard/Support（Reposition），普通 Cavalry Hold 未出现；仅退出停止任务形态有 Cavalry Hold。见 §3.1。
2. **A7 的新 Guard/Support 获选**：见 §3.3，属计划生命期短于评价点周期。
3. **A8 的独立「不可执行」事件**：生产代码无 `PLAN_NOT_EXECUTABLE`，形态为四件套；且 `detail` 不含 role。见 §3.4。
4. **P2 生产默认关闭路径**：四场里只有场景 A 打开 `competition=1`；**无哨兵文件时 `P2CompetitionEnabled=false` 的路径未单独实机验证**（B/P/N 三场是文件不含该键或 `competition=0`，属不同条件）。
5. **跨场 SHA 不同不等于源码不同**：四场是分别构建的产物（每场 `deploy.ps1` 重新生成 `BuildIdentity.g.cs` 并强制重建）。**同一份受审源码的证据是 §0 的源码冻结点，不是 SHA 相等。**
6. **P1 的一条环境偶发失败**：`20261013-164742` 在 GABP 桥接层崩溃，与 2026-10-10 历史崩溃逐字节相同，非本单改动引入；该场无 CB2 计划数据。

### 6.2 独立验收者可直接执行的检查项

```powershell
# 0) 派单文件身份（应与 §0 表一致）
(Get-FileHash -LiteralPath 'E:\MBmod\CombatBrain 2.0\Program\_verify\P2-MULTI-FORMATION-DISPATCH.md' -Algorithm SHA256).Hash

# 1) 四场身份闭合（每场内三条应完全相同）
foreach ($s in 'p2-A-20261013-163716','p2-B-20261013-164318','20261013-170546','20261013-170928') {
  $d = "E:\MBmod\CombatBrain 2.0\Program\_verify\sessions\$s"
  Get-Content "$d\deployed-module.json" -Raw
  Select-String -LiteralPath "$d\CombatBrain2.log" -Pattern '\[CB2\]\[RUNTIME_IDENTITY\]'
}

# 2) A5 三门之一的关键三条
$A = 'E:\MBmod\CombatBrain 2.0\Program\_verify\sessions\p2-A-20261013-163716\CombatBrain2.log'
Select-String -LiteralPath $A -Pattern '\[CB2\]\[ARBITRATION_CANDIDATE\]'                 # 应 21 条，winner 全为 TaskSubmission
Select-String -LiteralPath $A -Pattern '\[CB2\]\[COMPETITION_INJECT_WINDOW_SATISFIED\]'   # 应 1 条，elapsed=9.009 rounds=4
Select-String -LiteralPath $A -Pattern '\[CB2\]\[ARBITRATION_INJECT\].*outcome=rejected'  # 应 18 条（UnauthorizedSource 9 + StaleRevision 9）

# 3) A4 协同三段
Select-String -LiteralPath $A -Pattern '\[CB2\]\[COORDINATION_STATE\].*plan=plan-3' | Select-Object -First 1
Select-String -LiteralPath $A -Pattern '\[CB2\]\[COORDINATION_STATE\].*plan=plan-3.*advance_allowed=1' | Select-Object -First 1
Select-String -LiteralPath $A -Pattern '\[CB2\]\[WRITE_EXECUTED\].*plan-3-task-4'

# 4) A8 不可执行链（场景 B）
$B = 'E:\MBmod\CombatBrain 2.0\Program\_verify\sessions\p2-B-20261013-164318\CombatBrain2.log'
Select-String -LiteralPath $B -Pattern '\[CB2\]\[(TASK_ELIGIBILITY_LOST|PLAN_INVALIDATED|HANDOFF_CLEARED)\]'
Select-String -LiteralPath $B -Pattern '\[CB2\]\[COORDINATION_ADVANCE\]'   # 应 1 条，且属 plan-4 而非 plan-1

# 5) A12 两场回归
foreach ($s in '20261013-170546','20261013-170928') {
  $p = "E:\MBmod\CombatBrain 2.0\Program\_verify\sessions\$s\CombatBrain2.log"
  Select-String -LiteralPath $p -Pattern '\[CB2\]\[(P1_MODE|P2_MODE|WRITE_BLOCKED|TASK_SELECTION_TIMEOUT)\]'
}
```

**独立验收者应特别核对的四点**（按派单 §12 L279 三门）：
1. **A3–A4**（三编队协同）：§5 时间线里主力 Hold → 保护/辅助到位 → `advance_allowed` 0→1 → 主力 `StandYourGround→Move` 是否构成真实协同。
2. **A5**（真实冲突裁决）：§3.2 的两次竞争是否满足「同级合法」「预定唯一赢家」「非法先过滤」。
3. **A7**（结构/职责变化联动）：§3.3 的 `COORDINATION_REASSIGNED` + revision 1→2 + 三新任务 ID 是否算「实际职责改变」，以及新 Guard/Support 未获选是否影响联动结论。
4. **A8**（不可执行）：§3.4 的四件套形态是否等价于判据要求的「明确报告不可执行」。

### 6.3 已否决的候选修法（记录否决依据，避免独立验收者重复评估）

本轮「原生不再发获选事件」缺陷的三个候选修法均**已实测/实测性依据否决**，最终采用 `AcceptRealSelection(... source=behavior_tick)`（§1.5 结论 2）。

| 候选修法 | 否决依据 |
|---|---|
| ① 代次权重：`GetAiWeight()` 返回 `1f + generation * 0.001f` | **结构性无效，且实测已被覆盖**。其一，`FormationAI.ActiveBehavior` 的 setter 外层守卫 `if (_activeBehavior != value)` 是**引用比较**：同一实例再次当选直接短路，与它的权重值无关，给同一实例加权重增量不可能触发重估。其二，宿主 `TestDriver.cs` 在 arm 时显式调用 `ai.SetBehaviorWeight<CbHoldBehavior>(8.0)`，而 `SetBehaviorWeight<T>` 写的是 `WeightFactor`（乘数因子），**并不改变 `GetAiWeight()` 的返回值**，故该增量进不了原生的比较式 `GetAIWeight() * WeightFactor`。实测：场景 A 首跑（`p2-A-20261013-121816`）判据 1（`generation=` 递增）成立而判据 2/3 全灭，task-4 的 `PROPOSAL_PUBLISHED` 与 `WRITE_EXECUTED` 均为 0 条。 |
| ② 每个任务新建 `CbHoldBehavior` 实例 | **结构性无效**。内层守卫走 `BehaviorComponent.Equals`，而该类把它重写为 `return obj != null && GetType() == obj.GetType();`（**类型比较**）。新实例与旧实例子类型相同 → `Equals` 为真 → 事件条件不成立，仍然不发。 |
| ③ 仅依赖 `OnActiveBehaviorChanged`（不引入 tick 证据源） | **结构上不可能对连续第二个任务成立**。同一类型行为连续获选时两条短路路径都会命中（见①②），该事件对「本方行为始终在位」的场景不是可用信号源 —— 这正是缺陷的表象，不是偶发。 |

**共同点**：三者都试图让原生「再发一次信号」，而反编译证明该信号在同一类型连续获选时被结构性抑制。因此正确做法是把「本方行为被 tick」本身当作获选硬证据（§1.5 结论 2），而不是去构造信号。三个候选**均未进入最终代码**；`Program/src/Host/CbHoldBehavior.cs` 已回退为 54 行无代次版本。

---

## 7. P2-RESTART 整改（R1–R4）结果 · 2026-10-14

派单：`Program/_verify/P2-RESTART-REMEDIATION-DISPATCH.md` v1.0（登记 `CB2-PROMPT-0003`）。`CB2-PROMPT-0002` 与 §3 的 A1–A13 判据不变，本整改不降低三门、不重开 P1 历史验收、不扩大战术范围。
本节是**最终结论**：§0–§6 保留原样（A5 历史四场），**A1–A13 的最终状态以 §7.4 为准**。

### 7.0 提交与文件摘要

| 仓库 / 工作树 | 分支 | 本整改提交 | 内容 |
|---|---|---|---|
| `E:\MBmod\CombatBrain 2.0` | `master` | **`24548d2`** | 13 files / +785 / −60；父提交 `7cb00c3` |
| `E:\MBmod\CBATH-P2` | `codex/cb20-p2-evidence` | **`ebed701`** | 1 file / +21；父提交 `b9fcef7` |

`24548d2` 文件（按派单 §4 白名单逐路径暂存，未用 `git add -A` / `commit -a`）：
`Program/_verify/run-p1.ps1`(+8/−1)、`Program/src/Core/BattlePlan.cs`(+8)、`CoordinationDriver.cs`(+149/−19)、`CoordinationPlan.cs`(+23/−1)、`EligibilityTrigger.cs`(+15/−1)、`ThreadHandoff.cs`(+16)、`Program/src/Host/BuildIdentity.g.cs`(±1)、`EligibilityProbe.cs`(+17/−4)、`TestDriver.cs`(+249/−29)、`Program/tests/CoordinationDriverTests.cs`(+161)、`CoordinationIntegrationTests.cs`(+10)、`DependencyReadinessTests.cs`(+89/−3)、`EligibilityTriggerTests.cs`(+39/−1)。

`ebed701` 内容：`P2FormationEvidence.cs` 为 mission / formation 各加 `mission_identity` / `formation_identity`（`RuntimeHelpers.GetHashCode(o).ToString("x8") + "@" + o.GetType().Name`），身份来自实际引擎对象，不从 CB2 日志回填。

**交付时 dirty 摘要**：已跟踪文件仅本报告 `Program/_verify/P2-IMPLEMENTATION-HANDOVER.md`（本节追加）；其余未跟踪项全是运行产物（`Program/_verify/_lastrun*.txt`、`gabs-config/`、`_scratch/`、`Program/_verify/sessions/*`）。观测组三个工作树本轮只读，未修改 / 构建 / 切分支 / 提交。
两者均**未 push，无 PR**。

### 7.1 R1–R4 结果

| 项 | 结果 | 关键实现 | 关键证据 |
|---|---|---|---|
| **R1 实际普通写令间隔** | **已闭合** | `ThreadHandoff.Requeue`（保留原槽位，不刷新首次提交/获选截止）+ 主线程直通分支与 `DrainPendingCommand` 各加普通任务落地复核（`BattlePlan.IsExitTask` 豁免） | A 场 15 条普通写令相邻间隔最小 **1.500 s**，全部 ≥1.5 s；新分配首令从分配时刻计 3.994 / 3.900 s；退出任务仍按自己 1 s 预算（98.037/98.087/98.137）；`HANDOFF_REQUEUED=290` 全为 `formation_stagger`，`HANDOFF_DROPPED=0` |
| **R2 最新依赖与同 Move 改目标** | **已闭合** | Core `CommandTargetStillValid` 改按 `task.OrderTargetX/Y` + `HasOrderTarget` + `OrderTargetReadbackError() <= 0.5`；新 `BattlePlan.ClearOrderTarget()`；Host 新 `RefreshOrderTargetReadback` 三处调用（`CoordinationTick`、`FeedbackTick`、主力推进写前 `CoordinationWriteBlock`） | A 场 `order_target_readback_error=0`、`advance_dependency_not_current=0`、`WRITE_BLOCKED=0`；B 场真实反例 `order_target_readback_error=9.64 (>0.5) order_target=(957.044,1133.435) requested=(963.531,1140.568)` → `TASK_FEEDBACK state=failed reason=NotEffective` → `PLAN_INVALIDATED plan-3 reason=NotEffective` |
| **R3 原生覆盖与行为权重** | **已闭合** | 删除 `TestDriver.GuardArmedWeights(double now)` 整块与 `SamplingBehavior.cs:169` 的调用；保留合同允许的 `ArmBehavior` 一次性 Route C arm | 四场 `BEHAVIOR_REWEIGHTED=0`；A 场 97.632 计划关闭时 4 条 `BEHAVIOR_DISARMED`（key 1/0、1/1、1/2 权重 8.000→0.000；key 1/3 原本 0.000）+ `REVOKE_SKIPPED ×11`；`ArmBehavior` 正常 arm 仍产生 `behavior_weight=8.000` 的 `TASK_CANDIDATE_SUBMITTED` |
| **R4 实机闭合与交回** | **已闭合** | 本批两仓库提交 + 本节 | 四场均为合法通路下的正例/反例/回归，身份三处闭合，见 §7.2–§7.5 |

**R2 的 red-green 反例（首轮判定口径过宽，已收窄）**：`CoordinationWriteBlock` 原以 `task.Role == Main && task.Source == PhaseTransition` 判定「推进任务」，会把重分配后新建的主力等待任务（`plan-1-task-92-1`，Kind=Hold，Reason=`coordination_main_hold_wait`）也拉去复核尚未写令的新 guard/support，并在其 Ordinal=1 上触发 `PredecessorNotWritten`，使 rev2 三条新任务整场零写令。
收窄为 Core 判据 `CoordinationDriver.RequiresAdvanceDependencyRecheck(task)`（只认 `Reason == ReasonMainAdvance`，全仓仅 `CoordinationDriver.cs:243` 一处赋值），Host 改调该判据。
红证据：新增测试 `AdvanceDependencyRecheckOnlyAppliesToAdvanceTask` 在过宽逻辑下失败（失败 1 / 通过 0）。绿证据：收窄后同 filter 通过（失败 0 / 通过 1），全量 `224 通过 / 0 失败`。
新增/加强的测试：`UnwrittenCommandDoesNotReleaseMainAdvance`、`CommandTargetOutsideBandDoesNotReleaseMainAdvance`、`SecondAdvanceReleaseUsesNewIdentity`、`AdvanceDependencyRecheckOnlyAppliesToAdvanceTask`、`ReassignmentRebindsDependenciesToCurrentTasks`、`MainAdvanceBlocksWhenActualOrderTargetDeviates`、`ScenarioADoesNotFireBeforeAdvanceCommandWritten`、`ScenarioADoesNotFireBeforeAdvanceSettled`、`ScenarioADoesNotFireWhileCompetitionWindowOpen`。

**R1 的次要表象（如实记录，未改判）**：A 场 `HANDOFF_SLOT_BUSY=2`——同一编队键已被别的任务占用时，`Requeue` 进不去，该次获选不作为写令证据，靠原生下一次 tick 重新进入；`HANDOFF_DROPPED=0`，没有因等待而丢失执行机会。

### 7.2 最终场次索引与产物身份（逐场部署自动重建，`build_id`/SHA 逐场不同属预期）

| 用途 | 场次目录 | CB2 `build_id` | 部署 DLL SHA256 / size | MVID / pid | CBATH `build_id` |
|---|---|---|---|---|---|
| **A 正例** | `Program/_verify/sessions/p2-A-20261014-114127` | `b20261014-114125-9a6560ef` | `55D8478D508A8C74E661E4AE5451E613CF1AA0716E6B88E52DBA5C27A8032DBF` / 157696 | `7c1e98fb-df1d-4c49-bb2e-2d7d5a2a6afd` / 40416 | `b20261014-114126-16c31de7` |
| **B 反例** | `Program/_verify/sessions/p2-B-20261014-114553` | `b20261014-114550-b8311161` | `280B6E84CD211CA93F3335EC421AB2ECB04F5F2F9F7CF160C7D5CB7E3C0D46C5` / 157696 | `ef5cd1db-02bc-4d07-8e29-c99114bdc99b` / 17540 | `b20261014-114552-abde21e5` |
| **P1 P 回归** | `Program/_verify/sessions/20261014-115634` | `b20261014-115631-a3a3e971` | `58F5E2E28F18D81C8BF950F988962DAB69FDF1D5F26AB5C432B30FAFD71D8FD1` / 157696 | `2f3c98d9-bd56-4ab4-ae55-e80402dc14e1` / 18312 | `b20261014-114552-abde21e5`（P1 入口不重建 CBATH，复用 B 场产物） |
| **P1 N 回归** | `Program/_verify/sessions/20261014-120020` | `b20261014-120018-9828293f` | `0AC20B77D2286AE521C2BF46DFE872D8CF8A7CC25504ECF8DAF69BD78EE74CFF` / 157696 | `2440df0d-b1bd-4c65-a3b0-e8c180dcde16` / 13156 | 同上（`8009f425113c0659622ad8af7b97354cbbda2954fdab59dcfbc1d612075b8445`） |

每场内三条记录闭合：`deployed-module.json` 的磁盘 SHA == 游戏内 `[CB2][RUNTIME_IDENTITY] assembly_file_sha256`（小写）== 部署源 SHA。CBATH 侧另有 `[CBATH_RUNTIME_IDENTITY]` 的 `assembly_sha256` / `mvid` / `pid` 与 `build-identity.json` 一致。
`Program/_verify/sessions/20261014-115102` 是**废场**：`P1_MODE enabled=0` + `P2_MODE enabled=0 rejected=both_enabled`、`WRITE_EXECUTED=0`。原因是 P1 入口未清零 P2 哨兵；跑前已修 `run-p1.ps1`（显式写 `p2-command-loop.txt = enabled=0`，随 `24548d2` 提交），重跑得 115634 / 120020。该场不计入证据。
运行方式：A/B 用 `Program/_verify/run-p2-exclusive.ps1 -Scenario <A|B> -CombatHoldSec 180`，P1 用 `Program/_verify/run-p1-exclusive.ps1 -Config <P|N> -CombatHoldSec 180`；全程经 `E:\MBmod\BattleObservation-V1\coordination\Invoke-ExclusiveGame.ps1 -Owner '主线组'` 持锁。

### 7.3 普通 / 退出命令时间表与独立样本

**A 场（18 条 `WRITE_EXECUTED`，其中普通 15 条、退出 3 条）**

| at | 任务 | kind | 对象 | 备注 |
|---|---|---|---|---|
| 5.036 | plan-1-task-1 | Hold | `02e2fa68@Infantry` | 首令，`first_command_delay=3.499` |
| 7.585 | plan-1-task-2 | Reposition | `00602e6a@Ranged` | +2.549 |
| 10.635 | plan-1-task-3 | Reposition | `0077636e@Cavalry` | +3.050 |
| 20.036 | plan-1-task-4-g1 | Reposition | `02e2fa68@Infantry` | +9.401，`StandYourGround→Move` |
| 32.033 | plan-1-task-92-1 | Hold | `02e2fa68@Infantry` | +11.997（rev2 首次分配，距 28.039 分配 3.994 s） |
| 34.633 | plan-1-task-92-r2-guard | Reposition | `0077636e@Cavalry` | +2.600 |
| 36.133 | plan-1-task-92-r2-support | Reposition | `00291a74@HorseArcher` | +1.500（**最小间隔**） |
| 50.036 | plan-1-task-4-g2 | Reposition | `02e2fa68@Infantry` | +13.903，rev2 推进，`replaced=\|plan-1-task-92-r2-guard\|plan-1-task-92-r2-support\|` |
| 92.032 | plan-1-task-93-1 | Hold | `02e2fa68@Infantry` | +41.996（rev3 首次分配，距 88.132 分配 3.900 s） |
| 94.632 | plan-1-task-93-92 | Reposition | `0077636e@Cavalry` | +2.600 |
| 97.582 | plan-1-task-93-r3-support | Reposition | `00602e6a@Ranged` | +2.950 |
| 98.037 | plan-1-task-93-1-exit | Hold | `02e2fa68@Infantry` | 退出（不受 1.5 s 门约束） |
| 98.087 | plan-1-task-93-r3-support-exit | Hold | `00602e6a@Ranged` | 退出 +0.050 |
| 98.137 | plan-1-task-93-92-exit | Hold | `0077636e@Cavalry` | 退出 +0.050 |
| 104.037 | plan-2-task-1 | Hold | `02e2fa68@Infantry` | +6.455 |
| 106.587 | plan-2-task-2 | Reposition | `00602e6a@Ranged` | +2.550 |
| 109.637 | plan-2-task-3 | Reposition | `0077636e@Cavalry` | +3.050 |
| 119.035 | plan-2-task-4-g1 | Reposition | `02e2fa68@Infantry` | +9.398 |

普通写令全部带 `order_target=… readback_error=0.000`、`is_main_thread=1`、`route=native_behavior_apply`、`error=<none>`。

**B 场（14 条，其中普通 10 条、退出 4 条）**：普通间隔 2.550 / 3.050 / 9.396 / 2.550 / 3.050 / 12.402 / 2.551 / 3.050 / 6.400（最小 2.550 s）；退出 11.037 / 32.134 / 32.534 / 32.584。

**独立采样**：CB2 侧 `[INDEPENDENT_SAMPLE]` 事件数 A=182、B=181、P=153、N=153；CBATH 侧 `p2-evidence-*.json` 文件数 A=193、B=192（A 场 `p2-evidence-0001..0193` 全部可解析，唯一缺 `mission_identity` 的是 `p2-evidence-end.json`，其 `status=no_mission` 属正常）。A 场 `missions_identity=01e2d58b@Mission`，B 场 `02494c9b@Mission`。

### 7.4 A1–A13 最终状态、继承依据与未覆盖项

> §3 表是 A5 历史四场（`p2-A-20261013-163716` 等）的判定。本整改改动了共享执行/反馈路径，故受影响项以本节为准；未受改动影响项按差异分析继承 §3 结论，未重复刷场。
> 状态口径不变：`PASS` = 有实机原始证据且与判据一致；`部分覆盖` = 只满足一部分或形态与判据措辞不同，交独立验收者裁定。

| 项 | 最终状态 | 相对 §3 | 依据 |
|---|---|---|---|
| A1 身份与场景 | **PASS** | 继承 | 四场 §7.2 三条记录闭合；`P1_MODE`/`P2_MODE` 实测互斥（P/N 场 `P2_MODE enabled=0`，A/B 场 `enabled=1`） |
| A2 最小新增控制组合 | **部分覆盖** | 继承（证据增强） | Cavalry Reposition 真实获选+写令、主线程、独立目标全部成立（A 场 `0077636e@Cavalry` 1/2 与 `00291a74@HorseArcher` 1/3，`SNAPSHOT_FORMATION key=1/3 class=Cavalry`）；**普通 Cavalry Hold 任务仍不存在**（Hold 只以退出停止任务形态出现）；`order_after=Charge` A/B 两场均 0 |
| A3 同 Plan 三职责 | **PASS** | 继承 | 每 plan 3 条 `ASSIGNMENT_PUBLISHED`，Main/Guard/Support 对象互不相同；rev2/rev3 重分配后同样三条新对象 |
| A4 协同发生 | **PASS** | 继承 | A 场三次 `COORDINATION_ADVANCE`（17.566 / 45.858 / 115.225）→ 主力 `StandYourGround→Move`（20.036 / 50.036 / 119.035）；**反例**：B 场 plan-1 未建立关系即失资格，全程 0 条 `COORDINATION_ADVANCE` |
| A5 权限唯一裁决 | **PASS** | 继承 | 23 条 `ARBITRATION_CANDIDATE` 中 winner 15 全为 `TaskSubmission`、loser 8 全为 `TestInjection`（零赢家）；`COMPETITION_INJECT_WINDOW_SATISFIED elapsed=9.003 rounds=4`；`WRITE_BLOCKED=0`、`TASK_SELECTION_TIMEOUT=0` |
| A6 分配稳定及延迟 | **PASS** | **加强** | 普通写令全程 ≥1.5 s（§7.3），新分配首令 3.994 / 3.900 s；`P2_FIRST_DECISION_SCHEDULED first_command_delay=1.500 decision_cadence=3.000`；`PLAN_CONTINUED=58` |
| A7 实际职责改变与联动 | **PASS** | **由「部分覆盖」升级** | rev2：28.039 保护失资格 → 同刻 `COORDINATION_REASSIGNED revision=2 new=plan-1-task-92-r2-guard`（原 Support `0077636e@Cavalry` 改任 Guard，`00291a74@HorseArcher` 补 Support），三条新任务**均获选并写令**（32.033 / 34.633 / 36.133），随后 45.858 以**新身份**合法推进 g2；rev3：88.132 恢复后 Support 再次失资格 → `revision=3 new=plan-1-task-93-r3-support`，三条新任务再次均获选并写令 |
| A8 不可执行分支 | **部分覆盖** | 继承（形态未变） | B 场 10.641 同 tick 失资格→`PLAN_INVALIDATED reason=FormationEligibilityLost`→三任务关闭→`HANDOFF_CLEARED`→撤武装；plan-1 此后零写令，全程不释放主力推进。**仍无独立 `PLAN_NOT_EXECUTABLE` 事件**，`detail` 只写 `task=` 不含 role |
| A9 跨计划/版本不复活 | **PASS** | **加强** | A 场 88.132 之后 rev2 旧任务只出现在 `POST_OBSERVE`（观测），零新获选 / 写令 / arm；97.632 只作为 `REVOKE_SKIPPED reason=object_taken_over_by_new_assignment`；rev3 新 ID 正常获选 |
| A10 反馈及异常如实 | **PASS** | **加强** | A 场 `TASK_FEEDBACK effective=13 / failed=1`；B 场 `order_target_readback_error=9.64 (>0.5)` 如实失败并触发计划失效（R2 的直接实机反例）；位移类任务的 `closed_distance` 为实测值 3.10–3.37（非填充） |
| A11 安全与边沿 | **PASS** | 继承 | A 场 `EXIT_STOP_QUEUED/SUBMITTED/DONE = 3/3/3`、`NOT_APPLIED=0`；B 场 `5/5/4/1`，未应用项 `waited=1.000 budget=1.000 ownership=own`（未写成小于 1.000）；`TASK_SELECTION_TIMEOUT=0`、`WRITE_REJECTED=0`、`MISSION_CLEANUP=1` |
| A12 受影响 P1 回归 | **PASS** | 重取 | P 场 `WRITE_EXECUTED=18`、`PLAN_CREATED=5`；N 场 `5` / `2`；两场 `P1_MODE enabled=1` + `P2_MODE enabled=0`、`WRITE_BLOCKED=0`、`TASK_SELECTION_TIMEOUT=0`、`COORDINATION_STATE=0`、`ARBITRATION_INJECT=0`（P2 路径未激活），探针链 `PROBE_ELIGIBILITY_LOST → RESTORED` 完整 |
| A13 交付可复查 | **PASS** | 重取 | §7.0 提交、§7.2 场次与 SHA、§7.3 命令表；CBATH 身份来自实际引擎对象 |

**未覆盖 / 需独立验收者裁定的项（不得当作已通过）**
1. **A2**：普通（非退出）Cavalry Hold 任务在两场都未出现；`order_after=Charge` 两场均 0。
2. **A8**：判据措辞「明确报告不可执行」在生产事件流中没有独立事件名，形态为四件套；`detail` 不含 role。
3. **A11 弱化边界**：3+ 对象同时退出去重在本轮两场未触发。
4. **R1 槽位占用**：A 场 `HANDOFF_SLOT_BUSY=2` 属"同一键被占时不丢获选、靠原生下一 tick 重进"，该分支只有 2 次样本。
5. **CBATH 未随 P1 场重建**：P/N 两场复用 B 场 CBATH 产物（`abde21e5`），属 P1 入口设计，不构成 P2 证据缺口。

### 7.5 共享资源收尾（2026-10-14 实测）

- 进程：仅 `steam`；**无 `Bannerlord.exe` / `Bannerlord.Native`**；每场结束脚本均报 `no residual Bannerlord process`、`no new crash dir`、`sentinel state restored`。
- 锁 `E:\MBmod\BattleObservation-V1\coordination\shared-game-session.lock`：内容为死 PID 5656 的临时清理脚本残留（`started_utc 2026-10-14T04:15:36Z`），以 `FileShare::None` **实测可独占打开** → 无持有者；未删锁。
- Documents 哨兵 `C:\Users\Administrator\Documents\CombatBrain2`：`p1-command-loop.txt = enabled=0`、`p2-command-loop.txt = enabled=0`、`eligibility-probe.txt` 与 `p2-eligibility-probe.txt` 均为空 → 上一场中断遗留已按派单 §3 第 54 行在独占入口内清理完毕。
- `Configs\engine_config.txt`：`safely_exited = 1`（已复位）。
- 部署目录 `Modules\CombatBrain2\bin\Win64_Shipping_Client\CombatBrain2.dll` = 157696 B（N 场产物，`0AC20B77…`）。

**独立验收前只写：开发自检完成、待独立验收。**
