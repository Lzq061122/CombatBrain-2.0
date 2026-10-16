# P3 接续整改 — 实现交接

**状态：限定自检完成，待独立验收。** 本文不宣称 P3 通过，不授权进入 P4。
依据：`CB2-PROMPT-0006`（`P3-COORDINATE-EXECUTION-CONTINUATION-DISPATCH.md`，SHA256 `368e880bfff6802bc034df96fe08395e038eaba74eba28654d7bd306019ddd7e`）。
`CB2-PROMPT-0005`（`P3-RESTART-REMEDIATION-DISPATCH.md`）与 `CB2-PROMPT-0004`（`P3-FEEDBACK-REPLANNING-DISPATCH.md`）继续有效；本次未改变公式、三门判据或命令域。

**第二轮（复核退回三缺陷整改）**：独立复核 `P3-CONTINUATION-REVIEW-732f6df-20261016.md` 对提交 `732f6df` 判「仍退回整改」，给出三项 P1 缺陷与第 4 节更正要求。本文件已按该报告整补：§2.8b 记录坐标接线的接线级测试整补，§3.2/§3.3 记录门2证据判定收紧，§3.4 记录 R5 归属判定与 `end_mission` 前置，§4 与 §7 记录更正后的根因表述。**第二轮整改结论未经过独立复核。**

**本轮为新开发会话接续。** 接手时存在未提交的 P3 成果（14 项已跟踪 dirty + 未跟踪生产源码 `TaskDelivery.cs`），按派单 §5 逐路径精准暂存提交，未 `git add -A` / `commit -a`、未 reset/clean、未回滚任何既有 dirty。

---

## 1. 来源、构建、部署与运行身份

### 1.1 接续基线与本轮提交

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

本地产物（提交后重建）：

| 产物 | 长度 | SHA256（前 16） |
|---|---|---|
| `Program\bin\Release\CombatBrain2.dll` | 177664 | `488585FBC248369B` |
| `Program\bin\Debug\CombatBrain2.dll` | 196096 | `7DD4E1AEB4BB98B3` |

（上表为第二轮整改后本地重建读数；`BuildIdentity.g.cs` 每次构建重新生成，DLL 哈希随构建身份变化，不与提交绑定。）

测试构成（逐文件实数 `[Fact]`+`[Theory]`，第二轮后共 **309**）：

| 测试文件 | `ced0a18` | 第二轮后 | 差 | 归属 |
|---|---|---|---|---|
| `Program/tests/CoordinateSemanticsTests.cs` | 0 | 15 | +15 | §2 坐标语义（新建文件；本轮 +5） |
| `Program/tests/TaskSelectionDeadlineTests.cs` | 25 | 36 | +11 | 8 个投递时序/幂等用例（0005 批工作，随未跟踪的 `TaskDelivery.cs` 本次一并收） + 3 个 §3.1 用例 |
| `Program/tests/ActionFeedbackTests.cs` | 52 | 59 | +7 | §3.2 R1 继任状态用例 |

第一轮本会话新增 `[Fact]` 13 个（§2 的 10 个 + §3.1 的 3 个）；其余 15 个是第一轮开始前已在工作树内、随源码首次进入提交的用例。派单 §1.2 L47 记载「隔离源码快照独立 Rebuild 全量测试 291 通过」= 276 + 这 15 个未提交增量，与第一轮一致（`291 → 304`）。第二轮再 +5（`CoordinateSemanticsTests` 由 10 → 15），全量 `304 → 309`。

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

证据来源：各场 `CombatBrain2.log` 的 `RUNTIME_IDENTITY`（`assembly_file_sha256` / `pid` / `Location`）与 `deployed-module.json`（`path` / `size` / `sha256` / `last_write`）。182784 = 基线旧构建；190976 / 191488 = 上批新构建。

`p3-P-20261015-221833` CBATH 身份（`build-identity.json`）：`build_id=b20261015-221831-76da5cbd`、`assembly_sha256=be477d94514ff700770c6939d833a01933daeebe57e72661f9b0ba9279374b8c`、`mvid=6c443e22-98c3-4463-9da1-23f63ec73d88`、`pid=46552`、`game_version=v1.4.8.119303`，`active_modules` 含 `CombatBrain2` 与 `CombatBrainAutonomousTestHarness`。

CBATH 模块源码 SHA256（与 `_audit/cb20-p3/2026-10-16-coordinate-restart/baseline.json` 的 `cbath.reviewed_source_files` 逐项一致）：`CbathTools.cs`=A894FA6979BF5E13…、`P2FormationEvidence.cs`=63F66BD9EAD0A174…、`P3OpponentControl.cs`=B0EFA5DE88CA510A…、`RunRecord.cs`=A26EA645402EF0C5…、`SamplingMissionBehavior.cs`=8239AA21DCF3C6A2…、`CombatBrainAutonomousTestHarness.csproj`=1521AD981EBA2E1E…

**重要说明**：§1.3 全部实机场次都产生于本轮提交之前（源码 mtime 20:16–20:25、场次时间 22:00–23:00）。本轮提交的源码改动 **未在任何实机运行过**。这不是遗漏，而是本单的资源条件——见 §4.4。

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

### 3.4 R5：完整入口失败路径与 WhatIf（§3.4）

审查退回项 R5 位于 `REVIEW.md:71-80`，受审 `run-p3.ps1:320-377`/`:635-693` 与 `_p3-exclusive-stage.ps1:47-85`。四条：

1. **helper 移入 try**：`Program/_verify/run-p3.ps1:443` 起 `try { $script:OwnGabsPid = Open-McpHttp ...; $script:McpOpened = $true }`。注释原文：「The helper must be opened INSIDE the try: it starts gabs before probing the endpoint and throws when the endpoint never becomes ready」。
2. **只按本场 PID 收尾**：全脚本 `Stop-Process` 只剩 1 处（`:783`），`:779-780` 先按 `$script:OwnGamePids -contains $_.Id` 过滤，`:790-794` 对非本场 PID 打 `Note-Others`。
3. **清理失败独立登记 + 结束实读**：`finally` 内每个 teardown 步骤独立 `try/catch` 并 `$script:CleanupFailures += …`（end_mission / games_stop / Close-McpHttp / crash report / sentinel restore / end-state read-back），末尾逐条 `Fail "cleanup: $cf"`；`Assert-CleanEndState` 实读 `safely_exited`、P1/P2/P3 开关、两探针空、本场残留 PID、helper 残留。
4. **stage 分列「开始尝试」与「实际取得」**：`Program/_verify/_p3-exclusive-stage.ps1:88-89` 改为两行：

```
game stage attempted:  <stage|none>
game stage returned ok: <Boolean>
```

`$gameStage` 在进入该 stage 时即置位（表示本场自此接管游戏生命周期），`$acquiredGame` 只表示该 stage 成功返回。

**第二轮整改补充（复核第 3 项）**：

5. **进程收尾改按实际归属证据限定。** 原实现把启动期间出现的**所有**同名新增 PID 都并入 `$script:OwnGamePids`——若他方进程恰在 `games_start` 调用期间启动就会被误杀。现改为三分支：`no_new_process` / `single_new_process_during_launch`（唯一新增才归本场）/ **`ambiguous_multiple_new_processes`**（多个同名新增 → 不认领、不杀，记入 `$script:UnownedNewPids`）；`games_start` 结果未确认时降级为 `single_new_process_launch_result_unconfirmed`。归属不明时按 `process ownership unresolved: … ; left running` 登记清理失败，**不强杀未知 PID**。
6. **`end_mission` 只在确有本场会话归属时执行。** `:518` 解析 `games_connect` 结果为 `Discovered|Re-synced|Already connected` 时置 `$script:SessionConnected = $true`；`:772` 仅在此为真时才发 `end_mission`，否则打印 `end_mission skipped: this run never connected to a game session`。helper HTTP 可用不再被当作已取得连接的证明。
7. stage record（`_p3-exclusive-stage.ps1:163/165`）新增 `ownership_basis` / `launch_process_started` / `unowned_new_pids` / `session_connected` 四项，使归属判定可事后核对。

**隔离模拟取证**（`Program/_verify/_r5sim/make-sim.ps1`：把真实 `run-p3.ps1` 按锚点正则切片、对 helper/进程表打桩后独立执行；**不触碰任何游戏进程、GABS helper 或共享开关**）。六模式实测（`_r5sim/r5-isolation-results.json`，runner SHA256 `FE917341959068FBEC0B11E0A4CC9D27027A2E7F44D64F486A7662A6D6CE01F3`，generator SHA256 `8F9B2F451B5CBBCB2F5B35B981CD3A7E6AD8DA4CE8C64322F00D02F58EF71C37`）：

| mode | helper_closed | batch_kill | foreign_pid_stopped | own_pid_stopped | end_mission_sent | failures |
|---|---|---|---|---|---|---|
| ok | True | False | False | True | **True** | `<none>` |
| connfail | True | False | False | False | False | `exception: simulated_endpoint_timeout` |
| neverconnect | True | False | False | True | False | `games_connect never succeeded \| exception: no_connect` |
| forcetook | True | False | False | True | False | `games_connect force-took a session this run did not own \| exception: force_takeover_refused` |
| startthrow | True | False | False | True | False | `exception: simulated_launch_timeout_after_process` |
| **startthrow-other-during-launch** | True | False | **False** | **False** | False | `exception: simulated_launch_timeout_after_process \| cleanup: process ownership unresolved: 777001,777999 appeared during the launch call and could not be attributed to this run (basis=ambiguous_multiple_new_processes); left running` |

六模式全部 `cleanup_reached=True`；`force_takeover_arg_sent` 恒 False。关键路径：

- `connfail` trace = `helper_process_acquired -> helper_closed` —— **正是 R5 第一条要闭合的「连接失败时 helper 未进 Close-McpHttp」**。
- `startthrow` trace = `helper_process_acquired -> mcp:games_start -> … -> stop-process:777001 -> helper_closed` —— 启动调用已产生进程后抛错，本场 PID 仍被归属并结束（此前名单在 `games_start` 成功返回后才取，会漏记）。
- `ok` trace = `… -> tool:cbath/get_mission_state -> tool:cbath/end_mission -> tool:cbath/get_last_run -> mcp:games_stop -> stop-process:777001 -> helper_closed` —— **`end_mission_sent=True` 只在 `ok` 模式出现**，其余模式（含 `neverconnect`、`forcetook`）均 False；即 `end_mission` 已改由实际连接的 Mission 归属（`$script:SessionConnected`）决定，helper HTTP 可达不再作为已取得连接的证明。
- 各模式 `end_state` 均为 `p1=enabled=0 p2=enabled=0 p3=enabled=0 probes_empty=True safely_exited=1 residual_own=0 helper_residual=0`。

桩表含他人进程 `777999`（仅在启动快照之后出现），用于暴露按名批量强杀。前五模式均未命中；第六模式 `startthrow-other-during-launch` 让两个同名新进程在启动调用期间同时出现，结果**不再认领、不再批量强杀**（`foreign_pid_stopped=False`），而是如实登记归属不明并保留其运行——**这正是复核第 3 项要求的「PID 差集不等于本场所有权」**。

**stage 三场景模拟**（`Program/_verify/_stagesim/stage-results.json`）：

| case | exit | completed | attempted | returned ok | result |
|---|---|---|---|---|---|
| all-ok | 0 | deploy_cb2, deploy_cbath, collect | collect | True | `RESULT: OK` |
| cb2-fail | 1 | `<none>` | `<none>` | False | `RESULT: FAILED at deploy_cb2` |
| cbath-fail | 1 | deploy_cb2 | `<none>` | False | `RESULT: FAILED at deploy_cbath` |
| collect-fail | 1 | deploy_cb2, deploy_cbath | **collect** | False | `RESULT: FAILED at collect` |

`collect-fail` 行是本条要求的直接证据：「开始尝试 collect」与「collect 成功返回」已分开记录。

**PS 5.1 语法与 WhatIf**：`run-p3.ps1`、`check-p3-feedback.ps1`、`_p3-exclusive-stage.ps1`、`run-p3-exclusive.ps1`、`_r5sim/make-sim.ps1` 经 `[Parser]::ParseFile` 全部 `OK (0 errors)`（PS 5.1.26100.9444）。`run-p3-exclusive.ps1 -Scenario P -WhatIf` → exit 0、输出末行 `RESULT: WHATIF`、前后 Bannerlord 进程数均为 0（无副作用）。

---

## 4. 原合同三门逐门自检

检查器：`Program/_verify/check-p3-feedback.ps1`（78262 B，SHA256 `998BE4883F18307153FD12F7325EE4C71326ECB410863FF8CC5EFFE2DB7365C2`，为第二轮整改后读数）。
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
Program\_verify\_r5sim\r5-isolation-results.json   六模式结论
Program\_verify\_stagesim\              stage 替身与 stage-results.json
Program\_verify\_tamper4\               审查副本（38.8 MB，门2 缺证据敏感性）
```

运行隔离模拟须缩短等待窗口（如 `-LaunchWaitSec 6 -PollTimeoutSec 6`），否则桩的模拟时钟会刷出数百行 `connect attempt`。

### 5.5 共享资源交接与清理实读

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

1. **门1 未取得 pass。** 本轮全部实机场次都早于 `732f6df`，它们只能证明历史成功/失败与工具的敏感性，**不能为当前提交给出任何门的结论**。旧场观测到的继任协同计划未完成保护/辅助落位与主力推进，其直接机制是**投递节奏 + 继任计划寿命**（3 s 决策网格使首个候选迟至公布后 2.511 s 提交，Main Hold 写出后 0.5 s 即被原生改目标），**不是依赖环**——`DependsOnTaskId` 指向的是 Hold 等待任务，依赖链无环（详见 §4 门1）。因此不存在需要另行申请的「互锁修复授权」；门1 的后续验证属于派单 §4 原有要求。当前产物三个门**均未实机验证**。
2. **门3 未取得 pass（unverified）。** P/C 实际部署不可比 + 无地形深度产物；未取匹配对照场次。**未放宽容差凑 pass。**
3. **本轮提交的源码未在任何实机运行过。** §1.3 全部场次的源码 mtime 为 20:16–20:25、构建早于本次提交。`732f6df` 与第二轮整补的改动只经过离线验证（309/309 测试、Release/Debug 重建 0 警告 0 错误、PS 5.1 语法、WhatIf、R5 隔离模拟与门2 篡改反例、坐标接线红态验证）。**资源未正式交回，按派单只做离线工作。**
4. **R5 隔离模拟是替身证据，不是实机证据。** 它证明控制流按预期收尾（helper 关闭、只杀本场 PID、清理失败登记、stage 分列），但不能替代一次真实 `Invoke-ExclusiveGame.ps1` 全流程收尾的实读。
5. **晚场的独占入口证据不完整**（沿用上批结论）。只有 `p3-P-20261015-203136`、`p3-N-20261015-211938`、`p3-N-20261015-213231` 三场留存完整 stage record（`_lastrun-p3-exclusive-*.txt`）。`p3-P-20261015-214532`、`-215314`、`-220249`、`p3-C-20261015-214157`、`-220738`、`p3-P-20261015-221833`、`-222821` 未留存 stage-record 日志，**不作为「经 `Invoke-ExclusiveGame.ps1` 独占执行」的证据**；其运行身份（§1.3）与产物仍有效，但启动通道归属未证实。
6. **`p3-P-20261015-222821` 未冻结**（`ACTION_FEEDBACK_FROZEN=0`，三门 `unverified :: no freeze`）。该场不可用于任何门。
7. **`_tamper4` 目录内含被修改过的 `CombatBrain2.log`（mtime 2026-10-16 1:34）**，用于门2 缺证据敏感性；原始场 `_verify\sessions\p3-P-20261015-221833` 未被清洗改写。审查副本位于 `E:\Agent\codex\2026-10-14\p3\work\review-20261016\`。

**未触发的自然边界**（保留为未验证，不登记为通过）：获选超时、写令截止后才首次获选、`FLIGHT`/`STOP_LOSS` 触发的继任、P1/P2 相关回归——本轮 §2 改动了 `TaskFeedback`/`CoordinationPlan`/`TestDriver` 的位置语义判断，已用 309 项单测覆盖，但**未在任何实机窗口回归**。P1 锚点语义由 `AnchorDefinedTaskKeepsAnchorSemantics` 与 `TaskSelectionDeadlineTests.MoveRequiresClosingDistance` 锁定。

---

## 8. 本轮新增概念清单

无新增架构、战术、指标或命令概念。`TaskDelivery`/`DeliveryFacts` 是接手时已存在的未提交实现（本轮收录并补身份门）。`FormationTask.AnchoredRequest` 是唯一新增生产 API，实现派单 §2.1 已裁定的换算；未引入新全局坐标系、未建通用变换框架。

第二轮新增 `Program/src/Core/ProposalWiring.cs`：它不是新概念，只是把 §2 已裁定的「战术目标语义」判定链（读哪个点、写前怎么复核）从 Host 搬到 Core，使离线夹具与实机走同一份实现。行为不变，`Host/TestDriver.cs` 相应减少 84 行（删除 `RoleTargetPoint`/`TryReadTargetPoint` 两份私有复本）。
