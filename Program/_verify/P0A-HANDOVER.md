# CombatBrain 2.0 — P0 第一批交接（最小命令通路）

> **Codex 独立验收（2026-10-10）：第一批整体暂不通过。** 工程加载、已有状态观测和两次外部 Stop 写入事实保留；源码没有把站定 Behavior 注册进原生选择，而是把普通外部写令回调标为 REAL_SELECTION。有限整改要求见 `P0A-CODEX-REVIEW.md` §4。

> **C 路线整改已完成并跑通（2026-10-10，run 4）。** 当前决定性场次改为 `Program\_verify\sessions\20261010-213621\`（pid 52288，build `b20261010-211405-653e521d`）。站定行为 `CbHoldBehavior` 已注册进原生 `FormationAI` 选择链，由原生选中后由原生侧应用写令；执行前补了对象身份／AI 受控／行为归属三项即时复核。下文 §3–§5、§9–§10 以 run 4 为准；run 6（`20261010-171526`）只保留为整改前的外部写令 PoC 与加载/观测事实。

派单：`E:\MBmod\TacticalCommandLab\_dispatch\2026-10-10-cb20-p0-minimal-command-path.md`
工作区：`E:\MBmod\CombatBrain 2.0`，开工 HEAD `a9ace1366f9aeb6de1a0f5a2cb039713004bc8fc`
有效实机场次：run 6 `20261010-171526\`（pid 20316，整改前）／**run 4 `20261010-213621\`（pid 52288，整改后，决定性）**

## 1. 本批实现范围

| 面 | 交付物 | 说明 |
|---|---|---|
| 独立工程与加载身份 | `Program\CombatBrain2.csproj`、`Program\src\Host\SubModule.cs`、`Program\_deploy\deploy.ps1`、`Program\_deploy\SubModule.xml` | 模块 `CombatBrain2` / `CombatBrain2.dll` / `CombatBrain2.Host.SubModule`，与 1.x `CombatBrain` 完全分离；启动时输出 `RUNTIME_IDENTITY` 到 `Documents\CombatBrain2\CombatBrain2.log` |
| 最小共享编队状态 | `src\Core\{FormationSnapshot,TargetSelector,TestParameters,ExecutionGate,WriteLedger}.cs`、`src\Host\{FormationSampler,IndependentObservation}.cs` | 双方非空编队的归属/对象身份/人数/兵种/位置/控制资格/当前 MovementOrder + Mission 身份与采样时间；只读，不修改 Agent 行为 |
| 一支步兵一条 Movement 通路（**C 路线整改后**） | `src\Host\{TestDriver,SamplingBehavior,CbHoldBehavior}.cs` | 新增 `CbHoldBehavior : BehaviorComponent`：通过 `FormationAI.AddAiBehavior` + `SetBehaviorWeight<CbHoldBehavior>(8.0)` 注册进原生选择链；原生 `FindBestBehavior` 选中后由 `FormationAI.TickOccasionally` 在原生主循环内应用 `SetMovementOrder(CurrentOrder=Stop)`。获选判据改为 `FormationAI.OnActiveBehaviorChanged` + `ReferenceEquals(f.AI.ActiveBehavior, ours)`，**不再把普通外部写令回调当获选**。不申请 Full、不关闭 AI、无 Authority/Lease |
| 有限退出与安全让位 | `ExecutionGate.Close`、`WriteLedger.PlanRevocation(IfSameObject)`、`TestDriver.{CancelIntent,RevokeIfNeeded,DisarmAllBehaviors,OnMissionEnd}` | 到期/显式关闭/目标失效/玩家资格丢失/Mission 结束五条取消路径；退出改为 `SetBehaviorWeight<CbHoldBehavior>(0f)` 逐个撤权（**不用 `ResetBehaviorWeights()`**，避免抹掉原生期间的新权重）；只撤销本方确实改变且仍由本方负责的状态 |
| 实机入口 | `Program\_verify\run-p0.ps1`、`Program\_verify\gabs-config\config.json` | 2.0 自己的 GABS game id `bannerlord148-cb2`，加载 `CombatBrain2` + CBATH，**排除旧 CombatBrain、CommandLabProbe**；不动 CBATH 核心与冻结入口 |
| 测试 | `Program\tests\CombatBrain2.Tests.csproj` + 5 个测试文件 | **47 个测试全绿**（43 + 执行前三项复核新增 4），调用真实 Core 实现（不复制算法自证）。测试工程只链 5 个 Core 源文件，**不编译 Host 层、不引 TaleWorlds** —— Host 正确性由实机证据支撑 |

本批**未实现**（按派单）：战场规划、预备队产品规则、玩家接管的产品化流程、自动建组、Facing/Arrangement/其他指令域。

## 2. 构建身份（本次运行实际加载的产物）

### 2.1 run 4（C 路线整改后，当前决定性产物）

- `build_id` = `b20261010-211405-653e521d`（`src\Host\BuildIdentity.g.cs`，部署脚本在 DLL 字节内按 ASCII 与 UTF-16 两种对齐核验印章确实编进产物）
- `mvid` = `65bbd58f-cf5e-4bda-886f-18bf794af645`
- `assembly_file_sha256` = `51068919F30B878090BCEFA5A496AB26578F1E6FD8B59CA8DBF076716DF3917A`（源产物与部署产物同摘要，`_deploy\deploy.ps1` 比对通过）
- 部署路径 = `E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\CombatBrain2\bin\Win64_Shipping_Client\CombatBrain2.dll`（**43008 B**，2026-10-10 21:14）
- 场次日志 = `sessions\20261010-213621\CombatBrain2.log` L1（`[CB2][RUNTIME_IDENTITY]`，pid 52288）：`assembly_location` 指向上述部署路径、`assembly_file_sha256` 与磁盘一致、`game_version=v1.4.8.0`、`active_modules=Harmony,ButterLib,UIExtenderEx,Mod Configuration Menu v5,Native,SandBox Core,Sandbox,StoryMode,CustomBattle,CombatBrain 2.0 (P0a),CombatBrain Autonomous Test Harness`（**无旧 CombatBrain**）
- CBATH 独立身份（`build-identity.json`，同 pid 52288）：`build_id=b20260925-013209-2b52d28b`、`mvid=e9f98865-06a7-47d8-ab7d-585cb445904a`、`assembly_sha256=93f6e154…`、`active_modules=…,CombatBrain2,CombatBrainAutonomousTestHarness`（**无 CombatBrain**）
- 说明：`assembly_file_sha256` 是**磁盘文件摘要**，不等于进程内存字节；`mvid` 是本场加载的程序集模块版本 id。

### 2.2 run 6（整改前产物，保留为外部写令 PoC 与加载/观测事实）

- `build_id` = `b20261010-171340-c6bc9392`
- `mvid` = `45b351b9-6b4a-4968-a3fa-13b44ba519df`
- `assembly_file_sha256` = `44845964C8AD9D825309AC053529F68BE2B909EFD8807A1BF7E57D02A667D8B2`
- 部署路径 = `E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\CombatBrain2\bin\Win64_Shipping_Client\CombatBrain2.dll`（37376 B，2026-10-10 17:13:41）
- 游戏版本 = `v1.4.8.119303`，日志报告 `v1.4.8.0`（`ApplicationVersion.FromString("v1.4.8")`）
- **回溯口径更正（2026-10-10，Codex）**：撤回“强制全新重编、Program\bin\Debug、已部署文件三方同为上述 SHA256”的回溯声明。开发本轮报告的全新重编摘要为 `732100BE1C3FD21F584A560D480E30DB879992A64911DBC62728AFCA1C03D834`；本次只读核对保留在 `Program\obj\Debug\CombatBrain2.dll` 的该产物与受审 `Program\bin\Debug\CombatBrain2.dll`：两者均为 37376 B，恰有 **72 字节构建元数据差异，其余 37304 字节逐字节相同**。差异位于 PE/调试目录时间戳、MVID、CodeView GUID 和 PDB 校验摘要；零基文件偏移分别为 136–139、33340–33355、35536–35539、35620–35635、35707–35738。此对照支持这些元数据以外的文件内容一致，不能证明当时完整编译输入已还原；`44845964…` 的全新重编摘要目前无法复现。本次未重新构建或运行测试。
- **run 6 身份保留**：该场 `sessions\20261010-171526\CombatBrain2.log` L1 的 `assembly_file_sha256` 与目前仍在盘的受审及部署文件均为上述 `44845964…`；场次的加载 MVID、路径及原始日志保持不变。加载身份以该场记录和对应部署产物为依据，不用本轮 `732100BE…` 产物替换或倒写该场身份。
- 说明：`assembly_file_sha256` 是**磁盘文件摘要**，不等于进程内存字节；`mvid` 是本场加载的程序集模块版本 id。

`SubModule.xml` 部署副本 SHA256 = `CF246650A1E7425639C9CA7D5B4DD2BC87542D89DA655A0F7B847422161F6168`（与源文件 `_deploy\SubModule.xml` 同摘要）。

## 3. 逐项判据（run 4 · `sessions\20261010-213621\` · C 路线整改后 · 当前决定性）

| 判据 | 结论 | 最短证据路径 |
|---|---|---|
| 独立加载 | **通过** | `CombatBrain2.log` L1 `[CB2][RUNTIME_IDENTITY] module=CombatBrain2 build_id=b20261010-211405-653e521d mvid=65bbd58f-… assembly_file_sha256=51068919… pid=52288 active_modules=…,CustomBattle,CombatBrain 2.0 (P0a),CombatBrain Autonomous Test Harness`（**无旧 CombatBrain**）；CBATH `build-identity.json` 同 pid 独立佐证（`active_modules=…,CombatBrain2,CombatBrainAutonomousTestHarness`） |
| 双方状态可信 | **通过** | 2.0 自身 `INDEPENDENT_SAMPLE at=182.721` → `team=0 Defender 5/47 \| team=1 Attacker 5/40 \| total_units=87`；CBATH 独立路径 `formations-late.json` `mission_time=182.769` → Defender 5/47、Attacker 5/40、TOTAL=87 —— **两条独立读取路径在同一时刻逐项一致**。早段对照：`formations-early.json` `mission_time=0.480` → Defender 4/99、Attacker 5/95、TOTAL=194。`SNAPSHOT_HEALTH` 183 条 `duplicate_keys=0`、`over_limit=0` 全 183 条 |
| 命令链成立 | **通过（Route C）** | L38 `INTENT_TRIGGERED at=15.462 deadline=35.462` → L59/61/63 `BEHAVIOR_REGISTERED key=1/0 behavior=CbHoldBehavior weight=8.000 behavior_count=25` → L60/62/64 `CANDIDATE_SUBMITTED … write_attempts=0`（候选阶段零写入）→ **L65 `ACTIVE_BEHAVIOR_CHANGED key=1/0 active_behavior=CbHoldBehavior ours=1 tid=56`** → L66 `REAL_SELECTION` → L67 `WRITE_EXECUTED order_before=Move order_after=StandYourGround route=native_behavior_apply write_attempts=1`。intent-2 同构：L206–L214（`ours=1 tid=44`、`write_attempts=2`） |
| 效果与覆盖可区分 | **通过（落地后仍被原生改写，如实记录）** | L67 `order_before=Move → order_after=StandYourGround`（非 Stop→Stop 的实际变化）；83 条 `POST_OBSERVE`：intent-1 在 `at=17.041→35.064` 连续 10 次 `order=StandYourGround same_object=1`，**`at=37.072/39.076` 变回 `Move`**；intent-2 在 `at=41.079/43.086/45.087` 三次 `StandYourGround`，**`at=47.093` 起至 `at=181.350` 全部 `Move`**。站定不持续独占，原生后续改写已如实报告 |
| 退出安全 | **部分通过** | L171 `INTENT_CANCELLED reason=WindowExpired at=35.466`（触发 15.462+20=35.462，实测 35.466，未延长）→ L172 `BEHAVIOR_DISARMED key=1/0 reason=WindowExpired weight=0 error=<none>`；L215 `EXPLICIT_CLOSE_SCHEDULED at=46.540` → L246 `INTENT_CANCELLED reason=ExplicitClose at=46.542` → L247 `BEHAVIOR_DISARMED reason=ExplicitClose weight=0`。51 条 `WRITE_BLOCKED` **全部 `reason=already_written`**，首条 `at=16.033`、末条 `at=46.528`，**46.6 s 之后 0 条**（退出后无旧意图写入）。`REVOKE_EXECUTED` **0 条**（见 §5 项 3） |
| 玩家与失效边界 | **未验证** | 场次内 `TARGET_INVALID` 0 条、`PLAYER_ELIGIBILITY_LOST` 0 条、`SELECTION_TIMEOUT` 0 条；`WRITE_BLOCKED` 51 条全为 `already_written`，无资格类拒绝被触发。离线覆盖见 §5 项 1 |
| 环境清理 | **通过** | `_lastrun4.txt`：`end_mission sent` / `saved CombatBrain2.log (104592 bytes)` / `games_stop sent` / `no residual Bannerlord process` / `no new crash dir` / `saved CBATH outputs` / **`RESULT: OK`**（脚本失败退出码非 0，不吞） |
| 快照年龄上限 | **未触发** | `SNAPSHOT_HEALTH` 183 条 `age=0.000 max_age_seen=0.000 max_age_limit=2.000 over_limit=0`，`refresh_interval_min=0.006~0.007`、`refresh_interval_max=0.008~0.009`、`records=10`。原因同 §3.1 项 1（每 tick 重采样） |

### 3.1 限制说明（必须与"通过"一并读）

1. **快照新鲜度上限从未被真正压到**：run 4 与 run 6 同因——`TestDriver.Tick` **每个 Mission tick 都用当前 `now` 重新采样**，`age` 恒为 0，2.0 秒上限从未触发。"陈旧快照被拒绝"在实机**仍无证据**，只有离线证据（`tests\SnapshotTests.cs`：`StaleSnapshotUsesActualSampleTimeNotRefreshTime`；`tests\TargetSelectorTests.cs`：`DoesNotSelectStaleSnapshot`、`AcceptsSnapshotExactlyAtAgeLimit`）。
2. **"两组独立观测"的含义**：2.0 自身的 `SNAPSHOT_FORMATION` 与 `INDEPENDENT_SAMPLE` 是同进程的重复采样，**不作为独立性依据**；独立性来自 CBATH 的独立只读路径（`get_all_formations` → `formations-*.json`，由 `TaleWorlds` 侧 `FormationProbe` 直接读取，不经过 2.0 代码）。run 4 晚期两条路径**逐项完全一致**（47/40、total 87，相距 0.048 s），强于 run 6 的"差 1 人需战损解释"。
3. **撤销未执行**：两条意图 `PreviousOrder` 都是 `"Move"`，`WriteLedger.IsRestorableOrderName` 只接受 `Advance`/`FallBack`/`Charge`/`StandYourGround`，故判 `previous_order_not_restorable`（L173、L248 `REVOKE_SKIPPED`）。这是**拒绝用不可忠实重建的值去覆盖原生新决策**，不是执行失败（`MovementOrder` 是值类型，`Move` 带目标/位置，无法按名忠实重建）。
4. **Mission 未自然结束**：`mission-state-end.json` = `{"mission_running":true,"mission_time":"182.790"}`，收尾是脚本发 `end_mission`。`MISSION_CLEANUP`（L638）已记录，但"下一 Mission 无旧状态残留"未验证。
5. **退出后编队回到 `Move` 是本方撤权的预期结果**：`BEHAVIOR_DISARMED weight=0` 后原生 `FindBestBehavior` 重新选出自己的行为并把命令改回。DTO 不宣称站定在意图结束后仍持续生效。
6. **`WriteLedger` 的"当前值仍等于本方写入值才回退"护栏**：本场因白名单未过而从未进入回退分支，故该护栏在 run 4 **没有实机触发**（离线证据见 `tests\RevocationTests.cs`）。

### 3.2 run 6（整改前）对照表 — 仅保留为外部写令 PoC 与加载/观测事实

Codex 已判该场的 `REAL_SELECTION` **不构成原生获选证据**（`P0A-CODEX-REVIEW.md` §1）：L63 是 `NATIVE_MOVEMENT_APPLIED … order_enum=Move our_write=0 source=not_ours`，原生发出的是 Move，不能推导原生选中了 2.0 的 Stop 候选。下表仅记录事实，不作为通过结论。

| 判据 | 结论（run 6，整改前） | 最短证据路径 |
|---|---|---|
| 独立加载 | **通过** | 同上 run 4 条目之外，run 6 另有引擎级证据：`engine-rgl_log_20316.txt` L122（Command Args 只列 `CombatBrain2` 与 `CombatBrainAutonomousTestHarness`）、L1268（`Loading assembly: ..\..\Modules\CombatBrain2\bin\Win64_Shipping_Client\CombatBrain2.dll`）、L1328（`[CB2][RUNTIME_IDENTITY]`） |
| 双方状态可信 | **通过，但见下方限制** | `CombatBrain2.log` L152–L161（10 条 `SNAPSHOT_FORMATION`）；`formations-early.json`（CBATH 独立路径，`mission_time=0.444`）与 `formations-late.json`（`mission_time=152.908`）逐条对照；`SNAPSHOT_HEALTH` 152 条全部 `duplicate_keys=0`、`duplicate_detail=<none>`。晚期比对 CBATH total=123（65/58）vs 2.0 `INDEPENDENT_SAMPLE at=152.105` total=124（66/58）——team1 一致，team0 差 1 人，由两快照相距 0.8 s 且战损仍在发生解释 |
| 命令链成立 | **不通过（Codex 判定）** | `CombatBrain2.log` L38→L59/60/62（`CANDIDATE_SUBMITTED … write_attempts=0`）→ L63（`NATIVE_MOVEMENT_APPLIED … order_enum=Move our_write=0 source=not_ours`）→ L64（`REAL_SELECTION`，**该标记已撤回为获选证据**）→ L65（`WRITE_EXECUTED … order_before=Move order_after=StandYourGround write_attempts=1`）。L65 的后值变化是真实交付记录，但它是**独立于原生行为产出的外部写入**，只能保留为外部写令 PoC |
| 效果与覆盖可区分 | **通过（一次落地，非持续独占）** | L65 的 `order_before=Move → order_after=StandYourGround`（实际变化）；L91–L554 共 68 条 `POST_OBSERVE` 中 `order=Move` 62 / `order=Charge` 6 / `order=StandYourGround` 0；L186 与 L254 `REVOKE_SKIPPED reason=previous_order_not_restorable` 如实报告未回退 |
| 退出安全 | **部分通过** | L185（`WindowExpired`，截止 35.426 实测 35.432）、L225+L253（`ExplicitClose`，计划 46.542 实测 46.547）、L254 之后到 L557（`MISSION_CLEANUP`，末行）**无任何** `WRITE_*`/`CANDIDATE_SUBMITTED`/`REAL_SELECTION`；`REVOKE_EXECUTED` **0 条**（见未验证项 3） |
| 玩家与失效边界 | **未验证** | 场次内 `WRITE_BLOCKED` 0 条、`PLAYER_ELIGIBILITY_LOST` 0 条、`TARGET_INVALID` 0 条、`SELECTION_TIMEOUT` 0 条；离线检查见 `tests\ExecutionGateTests.cs`（`WriteBlockedWhenTargetInvalidOrPlayerOwned`、`WriteBlockedAfterWindowExpiry`） |
| 环境清理 | **通过** | `sessions\20261010-171526\last-run-console.txt`（`end_mission sent` / `games_stop sent` / `no residual Bannerlord process` / `no new crash dir` / `saved CBATH outputs` / `RESULT: OK`）；`engine-rgl_log_20316.txt` 末行 `Error: Non-Zero Device Reference Count! (ERC3446)` 为正常退出痕迹；`engine-watchdog_log_20316.txt` 全程无 `Crash occurred` |

### 限制说明（必须与"通过"一并读）

1. **快照新鲜度上限从未被真正压到**：`SNAPSHOT_HEALTH` 152 条的 `age` 与 `max_age_seen` 恒为 `0.000`、`over_limit` 求和 0。原因是 `TestDriver.Tick` **每个 Mission tick 都用当前 `now` 重新采样**，2.0 秒上限从未触发。因此"陈旧快照被拒绝"在本场**没有实机证据**，只有离线证据（`tests\SnapshotTests.cs` 与 `tests\TargetSelectorTests.cs`：`DoesNotSelectStaleSnapshot`、`AcceptsSnapshotExactlyAtAgeLimit`）。实际刷新间隔已记录：`refresh_interval_min` 0.005–0.019 s，`refresh_interval_max` 最大 0.067 s。
2. **"两组独立观测"的含义**：2.0 自身的 `SNAPSHOT_FORMATION` 与 `INDEPENDENT_SAMPLE` 是同进程的重复采样，**不作为独立性依据**；对照用的是 CBATH 的独立只读路径（`get_all_formations` → `formations-early.json` / `formations-late.json`，由 `TaleWorlds` 侧 `FormationProbe` 直接读取，不经过 2.0 代码）。晚期比对：CBATH total=123（team0 65 / team1 58，`mission_time=152.908`）vs 2.0 `INDEPENDENT_SAMPLE at=152.105` total=124（team0 66 / team1 58）——team1 完全一致，team0 差 1 人，由两快照相距 0.8 s、战损仍在发生解释。
3. **撤销未执行**：两条意图的 `PreviousOrder` 都是 `"Move"`，`WriteLedger.IsRestorableOrderName` 只接受 `Advance`/`FallBack`/`Charge`/`StandYourGround`，故判 `previous_order_not_restorable`（`REVOKE_SKIPPED`）。这是**拒绝用不可忠实重建的值去覆盖原生新决策**，不是执行失败。真实回退路径（`REVOKE_EXECUTED`）本场未走到，登记为未验证。
4. **Mission 未自然结束**：`mission-state-end.json` 为 `{"mission_name":"battle_terrain_biome_096","mission_running":true,"mission_time":"152.926"}` —— 收尾是脚本发 `end_mission`。`MISSION_CLEANUP` 已记录，但"下一 Mission 无旧状态残留"未验证。
5. **本场原生对我们的 Stop 反应极快**：`L65` 于 `at=15.541` 写入 `StandYourGround`，紧接着 `L75` 就有原生 `NATIVE_MOVEMENT_APPLIED formation=Infantry order_enum=Move our_write=0 source=not_ours`，第一个 `POST_OBSERVE`（`L91`，`at=17.041`）已是 `Move`。因此"Stop 落地后被原生改回"是**观察到的真实现象**，不是观测点遗漏造成的假象（run 5 曾用不同记录路径捕捉到 1 条 `StandYourGround`，见 `sessions\20261010-164624\`）。

## 4. 命令链与退出现象（逐字摘录）

### 4.1 run 4 `sessions\20261010-213621\CombatBrain2.log`（C 路线整改后，决定性）

```
L7   [CB2][TRIGGER_SCHEDULED] intent=A at=15.459 deployment_finished=1 allow_ai_ticking=1
L38  [CB2][INTENT_TRIGGERED] intent=intent-1 at=15.462 deadline=35.462 window=20.000 selection_wait=10.000
L39  [CB2][SELECTION_CANDIDATES] intent=intent-1 sort_basis=prefer_non_stop_then_ascending(TeamIndex,FormationIndex) candidates=10 eligible=1/0
L59  [CB2][BEHAVIOR_REGISTERED] intent=intent-1 key=1/0 behavior=CbHoldBehavior weight=8.000 behavior_count=25 active_behavior=BehaviorCautiousAdvance
L60  [CB2][CANDIDATE_SUBMITTED] intent=intent-1 key=1/0 obj=02c17ea5@Infantry order_before=Move behavior_registered=1 behavior_weight=8.000 at=15.462 write_attempts=0
L61  [CB2][BEHAVIOR_REGISTERED] intent=intent-1 key=1/0 behavior=CbHoldBehavior weight=8.000 behavior_count=25 active_behavior=BehaviorCautiousAdvance
L62  [CB2][CANDIDATE_SUBMITTED] intent=intent-1 key=1/0 obj=02c17ea5@Infantry order_before=Move behavior_registered=1 behavior_weight=8.000 at=15.490 write_attempts=0
L63  [CB2][BEHAVIOR_REGISTERED] intent=intent-1 key=1/0 behavior=CbHoldBehavior weight=8.000 behavior_count=25 active_behavior=BehaviorCautiousAdvance
L64  [CB2][CANDIDATE_SUBMITTED] intent=intent-1 key=1/0 obj=02c17ea5@Infantry order_before=Move behavior_registered=1 behavior_weight=8.000 at=15.526 write_attempts=0
L65  [CB2][ACTIVE_BEHAVIOR_CHANGED] intent=intent-1 key=1/0 active_behavior=CbHoldBehavior ours=1 tid=56
L66  [CB2][REAL_SELECTION] intent=intent-1 key=1/0 side=Attacker obj=02c17ea5@Infantry at=15.533
L67  [CB2][WRITE_EXECUTED] intent=intent-1 key=1/0 obj=02c17ea5@Infantry invoked=1 error=<none> order_before=Move order_after=StandYourGround route=native_behavior_apply at=15.533 write_attempts=1
L171 [CB2][INTENT_CANCELLED] intent=intent-1 reason=WindowExpired at=35.466 write_attempts=1
L172 [CB2][BEHAVIOR_DISARMED] key=1/0 reason=WindowExpired weight=0 error=<none> at=35.466
L173 [CB2][REVOKE_SKIPPED] intent=intent-1 reason=previous_order_not_restorable
L174 [CB2][SEQUENCE_INTENT_A_CLOSED] at=35.466 next_intent_at=40.466
L187 [CB2][INTENT_TRIGGERED] intent=intent-2 at=40.470 deadline=60.470 window=20.000 selection_wait=10.000
L206 [CB2][BEHAVIOR_REGISTERED] intent=intent-2 key=1/0 behavior=CbHoldBehavior weight=8.000 behavior_count=25 active_behavior=BehaviorCautiousAdvance
L211 [CB2][CANDIDATE_SUBMITTED] intent=intent-2 key=1/0 obj=02c17ea5@Infantry order_before=Move behavior_registered=1 behavior_weight=8.000 at=40.526 write_attempts=1
L212 [CB2][ACTIVE_BEHAVIOR_CHANGED] intent=intent-2 key=1/0 active_behavior=CbHoldBehavior ours=1 tid=44
L213 [CB2][REAL_SELECTION] intent=intent-2 key=1/0 side=Attacker obj=02c17ea5@Infantry at=40.533
L214 [CB2][WRITE_EXECUTED] intent=intent-2 key=1/0 obj=02c17ea5@Infantry invoked=1 error=<none> order_before=Move order_after=StandYourGround route=native_behavior_apply at=40.533 write_attempts=2
L215 [CB2][EXPLICIT_CLOSE_SCHEDULED] at=46.540
L246 [CB2][INTENT_CANCELLED] intent=intent-2 reason=ExplicitClose at=46.542 write_attempts=2
L247 [CB2][BEHAVIOR_DISARMED] key=1/0 reason=ExplicitClose weight=0 error=<none> at=46.542
L248 [CB2][REVOKE_SKIPPED] intent=intent-2 reason=previous_order_not_restorable
L638 [CB2][MISSION_CLEANUP] intent_and_object_state_cleared
```

- **候选阶段零写入**：L60/62/64 三条 `CANDIDATE_SUBMITTED` 的 `write_attempts` 恒为 **0**（intent-2 的 L207/209/211 为 1，那是 intent-1 已写过的累计值，非本次候选产生）。全日志 `write_attempts` 取值集合 `{0,1,2}`，**最大 2**（每条意图只有一次写尝试）。
- **获选判据是引用相等，不是键比对**：L65/L212 的 `ACTIVE_BEHAVIOR_CHANGED … active_behavior=CbHoldBehavior ours=1` 来自 `FormationAI.OnActiveBehaviorChanged` + `ReferenceEquals(f.AI.ActiveBehavior, ours)`。整改前 run 6 用 `Formation.OnBeforeMovementOrderApplied`（pre-apply 时点）按编队键比对，会把原生自己的 Move 写入误判为本方获选——这正是 Codex §1 指认的缺陷，已删除该路径的执行资格，仅保留为观测（`NATIVE_MOVEMENT_APPLIED … source=…`）。
- **写令发生在原生主循环内**：`CbHoldBehavior.TickOccasionally()` 由原生 `FormationAI.TickOccasionally` 调用，写令受原生 `IsAIControlled` 门控（`TestDriver.TryExecuteStop` 是被它调用的，不再是 Mission tick 里的并列外部腿）。日志标记 `route=native_behavior_apply`。
- **线程**：`BEHAVIOR_START tid=30`（Mission tick 主线程）与 `ACTIVE_BEHAVIOR_CHANGED tid=56/tid=44`（另两条意图各一次）、8 条 `ACTIVE_BEHAVIOR_CHANGED_OTHER` 的 tid 各异（36/37/38/39/42/47）——**执行发生在与 Mission tick 不同的线程上下文，因此执行前三项复核是必要的**。
- **选择依据可复核**：L39 `SELECTION_CANDIDATES … sort_basis=prefer_non_stop_then_ascending(TeamIndex,FormationIndex) candidates=10 eligible=1/0`；17 条 `SELECTION_REJECTED` 为 9 条 `player_owned`（Defender 全队 `0/0`–`0/4`）与 8 条 `not_infantry`（Ranged×2、Cavalry×4、HorseArcher×2）。**目标不是硬编码的 T1F0/T1F3**，选定后绑定 `obj=02c17ea5@Infantry`，全场未换对象（83 条 `POST_OBSERVE` 的 `obj_now` 唯一值恒为该对象、`same_object=1`）。
- **截止点未被延长**：intent-1 触发 15.462 + 20 = 35.462，实测取消 35.466；intent-2 显式关闭计划 46.540，实测 46.542。三次重复提交（L60/62/64）与首次获选都没有延长窗口。
- **执行前三项复核的离线覆盖**（本场未触发拒绝，因三项恒为真）：`tests\ExecutionGateTests.cs` 的 `WriteBlockedWhenObjectIdentityChanged`、`WriteBlockedWhenNotAiControlled`、`WriteBlockedWhenActiveBehaviorNotOurs`、`WriteAllowedOnceWithFullPrecheck`。生产侧在 `TestDriver.TryExecuteStop` 于 `RequestWrite` **之前即时求值**（`ObjectIdentityOf(f)` / `f.IsAIControlled` / `IsOurActiveBehavior(key, f)`），不使用获选时的旧值。

### 4.2 run 6 `sessions\20261010-171526\CombatBrain2.log`（整改前，外部写令 PoC）

```
L38  [CB2][INTENT_TRIGGERED] intent=intent-1 at=15.426 deadline=35.426 window=20.000 selection_wait=10.000
L59  [CB2][CANDIDATE_SUBMITTED] intent=intent-1 key=1/0 obj=02827ee6@Infantry order_before=Move at=15.426 write_attempts=0
L60  [CB2][CANDIDATE_SUBMITTED] intent=intent-1 key=1/0 obj=02827ee6@Infantry order_before=Move at=15.473 write_attempts=0
L62  [CB2][CANDIDATE_SUBMITTED] intent=intent-1 key=1/0 obj=02827ee6@Infantry order_before=Move at=15.529 write_attempts=0
L63  [CB2][NATIVE_MOVEMENT_APPLIED] intent=intent-1 formation=Infantry order_enum=Move tid=36 our_write=0 source=not_ours
L64  [CB2][REAL_SELECTION] intent=intent-1 key=1/0 side=Attacker obj=02827ee6@Infantry at=15.529
L65  [CB2][WRITE_EXECUTED] intent=intent-1 key=1/0 obj=02827ee6@Infantry invoked=1 error=<none> order_before=Move order_after=StandYourGround at=15.541 write_attempts=1
L185 [CB2][INTENT_CANCELLED] intent=intent-1 reason=WindowExpired at=35.432 write_attempts=1
L186 [CB2][REVOKE_SKIPPED] intent=intent-1 reason=previous_order_not_restorable
L187 [CB2][SEQUENCE_INTENT_A_CLOSED] at=35.432 next_intent_at=40.432
L200 [CB2][INTENT_TRIGGERED] intent=intent-2 at=40.435 deadline=60.435 window=20.000 selection_wait=10.000
L223 [CB2][REAL_SELECTION] intent=intent-2 key=1/0 side=Attacker obj=02827ee6@Infantry at=40.536
L224 [CB2][WRITE_EXECUTED] intent=intent-2 key=1/0 obj=02827ee6@Infantry invoked=1 error=<none> order_before=Move order_after=StandYourGround at=40.542 write_attempts=2
L225 [CB2][EXPLICIT_CLOSE_SCHEDULED] at=46.542
L253 [CB2][INTENT_CANCELLED] intent=intent-2 reason=ExplicitClose at=46.547 write_attempts=2
L254 [CB2][REVOKE_SKIPPED] intent=intent-2 reason=previous_order_not_restorable
L557 [CB2][MISSION_CLEANUP] intent_and_object_state_cleared
```

- **候选阶段零写入**：L59/60/62 的 `write_attempts` 恒为 0；全日志 `write_attempts` 取值集合为 `{0,1,2}`，**最大 2**（每条意图只有一次写尝试）。
- **获选回调线程 ≠ 写令线程**：`NATIVE_MOVEMENT_APPLIED` 的 tid 集合为 `{3,34,35,36,37,38,39,40,41,42,43,44,46,47,48,49,50,52,56}`，而写令在主线程 `OnMissionTick` 内发出。设计上回调（`Formation.OnBeforeMovementOrderApplied`，pre-apply 时点）**不写令**，改由下一个 tick 执行，避免被外层原生写入立刻覆盖。
- **选择依据可复核**：`SELECTION_CANDIDATES intent=intent-1 sort_basis=prefer_non_stop_then_ascending(TeamIndex,FormationIndex) candidates=10 eligible=1/0`；9 条拒绝为 `0/0:player_owned`、`0/1:player_owned`、`0/2:player_owned`、`0/3:player_owned`、`0/4:player_owned`、`1/1:not_infantry:Ranged`、`1/2:not_infantry:Cavalry`、`1/3:not_infantry:HorseArcher`、`1/4:not_infantry:Cavalry` —— **唯一合格项 `1/0`**（team1=Attacker、非玩家队、AI 受控、Infantry、`order_before=Move`）。**目标不是硬编码的 T1F0/T1F3，而是按稳定排序字段当场选出**，选定后绑定 `obj=02827ee6@Infantry`，全场未换过对象（`POST_OBSERVE` 的 `obj_now` 唯一值恒为该对象、`same_object=1`）。
- **截止点未被延长**：intent-1 触发 15.426 + 20 = 35.426，实测取消 35.432；intent-2 显式关闭计划 46.542，实测 46.547。获选与重复回调都没有延长窗口（L59/60/62 三次重复提交后 deadline 仍为 35.426）。
- **该链已被 C 路线整改取代**：run 6 的"获选"来自写令回调键比对，run 4 起改为原生行为引用相等。此段仅作为对照与历史事实保留。

## 5. 未验证项（不得据此进入 P1）

> 以下为 **run 4 与 run 6 共同**未覆盖的项；两条 C 路线整改新增的判据（原生行为注册、引用相等获选）已在 run 4 验证。

1. **玩家接管边沿**：CBATH 冻结核心的 15 个工具中**没有任何一个能把控制权交还给玩家**（`delegate_team_to_ai` 是单向的 AI 授予）。按派单 L67/L79 保留离线检查、把实机边沿明确列为未验证。离线覆盖：`tests\ExecutionGateTests.cs` 的 `WriteBlockedWhenTargetInvalidOrPlayerOwned`（`player_owned` 拒绝）与 `tests\TargetSelectorTests.cs` 的 `DoesNotSelectPlayerOwnedFormation`。**run 4 亦未触发**（`PLAYER_ELIGIBILITY_LOST` 0 条）。
2. **目标失效分支**（`TARGET_INVALID`）与**选择超时分支**（`SELECTION_TIMEOUT`）：两场意图都在约 0.1 s 内获选、目标全程存活，两个分支均未触发（run 4：`AWAITING_NATIVE_APPLY` 0 条，说明获选后无悬空等待）。
3. **撤销的真实回退执行**（`REVOKE_EXECUTED`）：两场共 4 条意图全部因 `previous_order_not_restorable` 被正确跳过。撤销白名单 `WriteLedger.IsRestorableOrderName` 只接受 `Advance`/`FallBack`/`Charge`/`StandYourGround`，而实际 `previous_order` 均为 `Move`（值类型，带目标/位置，无法按名忠实重建），故刻意不拿 Stop 冒充。
4. **快照陈旧拒绝路径**（`over_limit=1`）：见 §3.1 项 1。两场 `age` 恒为 0。
5. **下一 Mission 无旧状态残留**：两场 Mission 均由脚本 `end_mission` 收尾，未自然进入下一场（`mission-state-end.json` 仍为 `mission_running:true`）。
6. **`assembly_version`** 报 `0.0.0.0`（csproj 未设 `<Version>`）；不影响本批判据，未修。
7. **C 路线未覆盖的原生交互**：本方行为权重 `8.0` 是在 `ArmBehavior` 里一次性设定的（原生常规行为 `GetAiWeight` 基准 1f，活跃行为带 1.2~2.0 惯性加成）。**"权重过小导致未获选"和"原生在获选后立刻改回"是两种不同的失败/降级**，本场只走通前者未发生的路径；若将来出现未获选，需按 `BEHAVIOR_REGISTERED` 的 `behavior_count`/`active_behavior` 与后续 `ACTIVE_BEHAVIOR_CHANGED_OTHER` 逐条区分，不能笼统归为"原生压制"。

## 6. 复现入口

```powershell
# 构建
cd 'E:\MBmod\CombatBrain 2.0\Program'
dotnet build CombatBrain2.csproj -c Debug

# 测试（47 个；43 + 执行前三项复核新增 4）
dotnet test tests\CombatBrain2.Tests.csproj -c Debug

# 部署（生成新 build_id、印章核验、清 bin/obj 真重编、目标目录守卫、源/部署 SHA256 比对）
powershell -NoProfile -ExecutionPolicy Bypass -File '.\_deploy\deploy.ps1'

# 实机（唯一入口；失败退出码非 0）
powershell -NoProfile -ExecutionPolicy Bypass -File '.\_verify\run-p0.ps1' -CombatHoldSec 180 -PollTimeoutSec 240 -LaunchWaitSec 900
```

实机前置：先确认无存活 `Bannerlord` 进程（`Get-Process -Name Bannerlord | Where-Object { -not $_.HasExited }`）；脚本会自行重写 `engine_config.txt` 的 `safely_exited = 1`（为 0 时游戏弹 Safe Mode 静默阻断全部 Mod 加载）。

**实机前置（本批新增，踩坑教训）**：连跑场次前**必须先确认 GPU 显存余量**。整改后前三场（run 1–3）全部在游戏到达 Mission 之前崩于 Granite GPU 缓存创建失败（见 §9），与本批源码无因果关系。判定方式：`& 'C:\Windows\System32\nvidia-smi.exe' --query-gpu=memory.used,memory.total,utilization.gpu --format=csv`；若 `memory.used` 接近总量，等待释放后再跑。

## 7. 证据索引

**外置归档（大原始材料）**

| 项 | 值 |
|---|---|
| **归档（当前，覆盖 11 场）** | `E:\MBmod\CombatBrain2-evidence\20261010-p0a-sessions-run7.zip` |
| 大小 / 条目 | **8768831 B / 581** |
| SHA256 | `E825FF34A48F024BB5FA3436BA13F4A44430A60C295635D48197F5E10C27A2FB` |
| sidecar | `E:\MBmod\CombatBrain2-evidence\20261010-p0a-sessions-run7.zip.sha256` |
| 覆盖面 | `Program\_verify\sessions\` 全部 **11** 个场次目录（含隐藏文件），源清单 581 files / 12694917 B |
| 归档（第一批，已被上者取代） | `E:\MBmod\CombatBrain2-evidence\20261010-p0a-sessions.zip`，274030 B / 531，`B144E23AFCE18B7C45F21B799884ECEEF9D9CD274808A9884E6264CCDDB015FE`；仅覆盖前 7 场（855706 B），不含 C 路线整改后的 4 场 |

归档摘要不写入归档内部（自指即失效），只在此处与 sidecar 记录。

**场次 1：`Program\_verify\sessions\20261010-171526\`（run 6，整改前，19 files / 577266 B）**

| 文件 | bytes | SHA256 |
|---|---|---|
| `CombatBrain2.log` | 90097 | `2A595638958D63725FBE53C60335ECF08146E6F0321CAFD2EB45813FB7CF732E` |
| `engine-rgl_log_20316.txt` | 441114 | `93DFCB72F16A6C9D9838A007898BAF3F21BCD09F5FECFF879E1F6AE0C2813051` |
| `engine-rgl_log_errors_20316.txt` | 182 | `36A1DD2B4F2CF1B1E45E767ACCBD014E8521BD0DB522FB099DE0B729E5EA571B` |
| `engine-watchdog_log_20316.txt` | 1525 | `AEC9350D95C92FCB00E6375ECE0595C728F47214ADE02C02D385A5E00876FAE0` |
| `last-run-console.txt` | 8328 | `AFD1761D70FB6664DC61BF12C587E303BAD79DB019E630893F22BA7A46E9E3BD` |
| `run-p0.ps1` | 14764 | `D1AE39B9F0CB75CD1E482362759A2DAEE8A66A74DDB282653BA5DC3951EC518C` |
| `gabs-config.json` | 1231 | `5AA5DEF28A30535A8F03A1D9A74F2DC34A6EC57D4BBC8D753B5102AAD30D36C8` |
| `build-identity.json` | 1695 | `B6B5F7D0BB632D9DB67536F46F8D93FD1720D7D305062BAFE98D2E6A2D4FA542` |
| `provider-start.json` | 906 | `6D93CB4875BC8BF162D1FE6B1A82D23DEC05270770C51A6C76128335AFD5CA78` |
| `start-from-screen.json` | 602 | `87186E0628FD6CCA548636AAD7DEA948F2656C172823B26383C4A0266B52E2B1` |
| `deployment.json` | 588 | `7AD2FA137C48C771582148EF2F0B81965E1D259C97433396FEE6385DD524ABBB` |
| `delegate.json` | 2626 | `B446CD6888F577C7708A50B8669D10EADB7B9963DBE9927BCACBBEF94CF0AA84` |
| `formations-early.json` | 5732 | `866812DE79E0217E96C31FF6B22C94550A4487E0666436190AAB6E7BBFA5D8A5` |
| `formations-late.json` | 6282 | `3ED9AE7C220B5B3C9791EA03A51680CCEFA15237E887EF1EE21FD16321435D5F` |
| `mission-state-end.json` | 290 | `009DB49143434EAE09E9F02425008FE156C4C37401190CFAD406B7CFB87BA04C` |
| `last-run.json` | 266 | `B22076F52E2DCE064AA1DB2B5E2E0FDE91C269CBFCE23FAC153D60E4F5CEA125` |
| `CBATH\cbath-gabp-20316.txt` | 176 | `05BEC9DBAEEB38A1E2457369E116E6794DFCEA590A6F82197139F57EA6D5A4FA` |
| `CBATH\cbath-runtime-identity-20316.txt` | 708 | `AB461BBD535811AC2628483F91A699EE5A3AF8D5595EDB87B4F9661A8D5FA106` |
| `CBATH\cbath-screen-20316.txt` | 154 | `73F634D553579E67D54A9C98B4C0ED623817C788F6B055FD6BE2DBAAD3DF66E3` |

**场次 2：`Program\_verify\sessions\20261010-213621\`（run 4，C 路线整改后 · 决定性，13 files / 124667 B）**

| 文件 | bytes | SHA256 |
|---|---|---|
| `CombatBrain2.log` | 104592 | `783482705563053702ABBF7B4EF84617EB64F31C9642710E86F9E4BB7C4C3DC5` |
| `build-identity.json` | 1695 | `A3BA8E924FB26C62F7008E4D2D29A0ABA4136A90AB66D130BCA32A5511A7671E` |
| `provider-start.json` | 906 | `A3C8957BF797635B16BB18A7C27C46C049476B4DCCB7668414855AA9A02B7538` |
| `start-from-screen.json` | 602 | `A23927F9FAB67F4A648FA07C148489F1FAEF5CAB1FAA8A9EF83A3C9AB7AEE5FC` |
| `deployment.json` | 588 | `4DA79F8B0CFB3525E27557065CEF98F4CA5B04DB3C9354F062327517ABFEA03A` |
| `delegate.json` | 2626 | `D8CC5BA82D1696F5CA5C6E4BDEFA463C9F54ADE5E01787129581796579551F93` |
| `formations-early.json` | 5752 | `7D8D270F711B669730D73C8656B192B29A762ADBD4E3A045967257E9C57B7DFC` |
| `formations-late.json` | 6312 | `1FEE4DF475B3D08B7E28FC3D60993DE7C0DA6CBB3DC13C1D2265D46E7856FCEE` |
| `mission-state-end.json` | 290 | `972ED73AC0ABD7140CCBCE0009C22B4E0FF50830D92998EAFBDD9DECB05F51B4` |
| `last-run.json` | 266 | `2382B0A6D4F86FFA3DCD10CAA1D87D8D2988B1A08CA200653DCF7A1CDDC4B7F0` |
| `CBATH\cbath-gabp-52288.txt` | 176 | `2B33B5EF801CB3FAF4E45CF4964B46F8A5DF6596DF2033739955B3E5E6299548` |
| `CBATH\cbath-runtime-identity-52288.txt` | 708 | `DFE080927809C613C663E5DE4CD3760071AE3ABD2E2914C45DA8932A3AF29463` |
| `CBATH\cbath-screen-52288.txt` | 154 | `764ACF4AC9CABF2748AB977B46EF177342A74328B6DC3FC2EE890555D15CB8C5` |

**场次索引（`Program\_verify\sessions\`，11 场 / 581 files / 12694917 B）**

完整索引（含每场 files/bytes/日志行数/RESULT/有效性）见 `sessions\INDEX.md`（仓库内唯一提交的场次索引）。

| 场次 | files/bytes | 有效 | 说明 |
|---|---|---|---|
| `20261010-152753` | 93 / 32947 | 否 | 早期失败场，无 2.0 日志 |
| `20261010-153359` | 96 / 33884 | 否 | 同上 |
| `20261010-155158` | 99 / 35224 | 否 | 同上 |
| `20261010-160417` | 103 / 37957 | 否 | 游戏在 CustomBattle 模块数据加载处崩溃（`h3_provider_start` 在加载途中被调用） |
| `20261010-161613` | 108 / 69978 | 否 | 部署闸门判据缺口，脚本被 `status:ok` 误放行；`no_player_team`，意图从未触发 |
| `20261010-164624` | 13 / 68450 | 中间场 | 命令链已完整；撤销原因记为 `native_rewrote_after_us`，`POST_OBSERVE` 捕捉到 1 条 `StandYourGround` |
| **`20261010-171526`** | **19 / 577266** | **是（run 6，整改前）** | 第一批决定性场次；Codex 验收后判 C 路线不成立 |
| `20261010-211555` | 35 / 11711698 | 否（run 1） | 整改后首场；模块已加载，游戏在主菜单后崩于 Granite GPU 缓存 OOM，**未进入 Mission** |
| `20261010-212554` | 0 / 0 | 否（run 2） | `games_start` 返回 `exited_during_start`，exitCode 3221225477 |
| `20261010-213002` | 0 / 0 | 否（run 3） | `games_start` 返回 `exited_during_start`，exitCode -1 |
| **`20261010-213621`** | **13 / 124667** | **是（run 4，整改后 · 决定性）** | C 路线整改后唯一成功场次 |

前五场与 run 1–3 是过程失败场，**不构成验收依据**，但保留原始结果未删。

## 8. C 路线整改后前三场崩溃的环境根因（run 1–3，非代码问题）

整改后部署（`build_id b20261010-211405-653e521d`，43008 B）连跑三场全败，**全部发生在游戏进入 Mission 之前**：

- run 1（`20261010-211555`，pid 2372）：走到 `at main menu (InitialState)` → `h3_provider_start sent` → `FAIL: CustomBattleScreen never reached`。`CombatBrain2.log` 只有 1 行 `RUNTIME_IDENTITY` —— **模块加载与身份输出正常**。崩溃日志 `C:\ProgramData\Mount and Blade II Bannerlord\crashes\2026-10-10_13.16.29\rgl_log_errors_2372.txt` 尾部：`ERROR: rglGPU_device_d3d11::create_render_texture` / `内存资源不足，无法完成此操作。`；`watchdog_log_2372.txt`：`#TW#Runtime#TW#GPU Crash#TW#1`。
- run 2（`20261010-212554`）：`games_start` 返回 `exited_during_start`，exitCode **3221225477**（0xC0000005），输出尾 `System.AccessViolationException … at TaleWorlds.Starter.Library.MBDotNet.WotsMainDotNet`。
- run 3（`20261010-213002`）：`exited_during_start`，exitCode -1，无输出。崩溃日志 `rgl_log_errors_44272.txt` 决定性三行：
  ```
  [21:30:09.593] Granite Error in Graphine::Granite::DX11::D3D11Device::CreateTextureInternal: resourceCallbacks->CreateTexture2D failed, HRESULT=-2147024882
  Granite Error in Graphine::Granite::Internal::PhysicalTexture::Initialize: Texture creation failed for a 16272x16128 texture
  ERROR: Error creating Granite GPU cache
  ```
  （`-2147024882` = 0x8007000E = **E_OUTOFMEMORY**）
- **排除的误导项**：`watchdog_log_*.txt` 尾部的 `System.IO.FileLoadException: File name: 'PresentationFramework, Version=6.0.2.0…' at WatchdogLib.Debugger.ask_report(...)` 是 **watchdog 自己抬转储 UI 时崩**，不是游戏崩。另有两个 `Bannerlord.exe` 条目（pid 56172、2372）在 WMI 里永久存在且杀不掉（`Stop-Process -Force` / `Process.Kill()` / `Invoke-CimMethod Terminate` ReturnValue=2 全无效），`.HasExited=True`、Handles 0~2 —— **它们是 WMI 缓存僵尸条目，不是真进程**，不得据此判定"游戏还在跑"。
- **处置**：等待显存自然释放后重跑。`nvidia-smi` 从 `memory.used=8696MiB / 16303MiB` 降到 `3125MiB`，随后 run 4 一次通过。
- **结论**：三场**没有任何一场进入 Mission**，与 C 路线代码整改无因果关系，不得据此修改验收参数或判据。

## 9. C 路线整改内容（对照 `P0A-CODEX-REVIEW.md` §4）

| Codex 要求 | 落实 | 证据 |
|---|---|---|
| ① 站定 Behavior 真注册进原生选择链 | 新增 `src\Host\CbHoldBehavior.cs`（`internal sealed class CbHoldBehavior : BehaviorComponent`）；`TestDriver.ArmBehavior` 调 `ai.GetBehavior<CbHoldBehavior>()`，无则 `new CbHoldBehavior(f)` + `ai.AddAiBehavior(behavior)`，再 `behavior.Arm()` + `ai.SetBehaviorWeight<CbHoldBehavior>(8.0)` | run 4 L59/L61/L63 `BEHAVIOR_REGISTERED … behavior=CbHoldBehavior weight=8.000 behavior_count=25` |
| ② 候选只计算命令，普通外部 setter 事件不再授予执行资格 | `SubmitCandidateOnly` 只做 `TargetSelector.Select` + `ArmBehavior` + `gate.SubmitCandidate`，**无 setter 调用**；`Formation.OnBeforeMovementOrderApplied` 只保留为观测（`OnNativeMovementOrderApplied`，记 `source=`） | run 4 L60/62/64 `CANDIDATE_SUBMITTED … write_attempts=0` |
| ③ 用本方行为的实际获选身份作交付依据 | 获选改由 `FormationAI.OnActiveBehaviorChanged` 驱动，`TestDriver.OnActiveBehaviorChanged` 用 `ReferenceEquals(f.AI.ActiveBehavior, ours)` 判 `ours=1`；`ours` 为假直接返回 | run 4 L65/L212 `ACTIVE_BEHAVIOR_CHANGED … active_behavior=CbHoldBehavior ours=1 tid=56/44` |
| ④ 转交主线程时执行前实时复核（同 Mission、同实际 Formation 对象、当前仍为本方获选行为、AI 受控、非玩家拥有、意图仍有效） | `TestDriver.TryExecuteStop` 在 `RequestWrite` **之前即时求值** `missionValid = ReferenceEquals(mission, Mission.Current)`、`targetValid = f.CountOfUnits > 0`、`playerOwned = f.Team.IsPlayerTeam \|\| f.PlayerOwner != null`、`currentObjectIdentity = ObjectIdentityOf(f)`、`aiControlled = f.IsAIControlled`、`behaviorIsOurs = IsOurActiveBehavior(key, f)`；`ExecutionGate.RequestWrite` 拒绝顺序追加 `object_identity_changed → not_ai_controlled → behavior_not_ours` | 生产侧见 `src\Host\TestDriver.cs:522-602`、`src\Core\ExecutionGate.cs`；离线 4 个新测试见 §4.1 末条 |
| ⑤ 取得原方法完成/被跳过的可核对事实及实际后值 | `WRITE_EXECUTED` 记 `invoked=1 error=<none> order_before=Move order_after=StandYourGround route=native_behavior_apply`。`order_after` 是**写令后独立复读** `OrderController.GetActiveMovementOrderOf(f)`，不是"我们写了什么"；`error` 非空即记为失败 | run 4 L67 / L214 |
| ⑥ 退出撤销本方行为影响、证明旧意图不再执行 | `CancelIntent`/`OnMissionEnd` 调 `DisarmAllBehaviors`：逐个 `Disarm()` + `SetBehaviorWeight<CbHoldBehavior>(0f)`，**刻意不用 `ResetBehaviorWeights()`**（会抹掉原生期间的新权重）；另有"当前值仍等于本方写入值才回退"护栏（`RevokeIfNeeded`） | run 4 L172/L247 `BEHAVIOR_DISARMED … weight=0`；51 条 `WRITE_BLOCKED` 全 `already_written`，末条 `at=46.528` 之后 0 条 |
| ⑦ 不强行重建旧 Move，不无限加权、不自动改成 B | `WriteLedger.IsRestorableOrderName` 白名单只认 `Advance/FallBack/Charge/StandYourGround`；`previous_order=Move` 判 `previous_order_not_restorable` 跳过。权重一次性设定 8.0，不每 tick 加压 | run 4 L173/L248 `REVOKE_SKIPPED reason=previous_order_not_restorable` |
| ⑧ 保留 `RuntimeIdentity.assembly_file_sha256`，不取消、不新增代码段摘要 | 未改动 `RuntimeIdentity.Emit()`；run 4 日志 L1 仍输出 `assembly_file_sha256=51068919…` | `src\Host\RuntimeIdentity.cs` 未改；run 4 L1 |
| ⑨ 只补必要生产逻辑检查和一场目标 C 通路验证 | 新增 4 个执行前复核测试（47/47 绿），实机只跑通 run 4 一场作为 C 通路验证 | §6 复现入口；`sessions\INDEX.md` |

## 10. 本批通过 ≠ P0 整体通过

- FD-PoC **P11** 按本次 Route **C** 范围登记（站定行为进入原生选择链，未申请 Full Authority、未关闭 AI、无 Authority/Lease）。
- FD-PoC **P1**（关闭 AI 后的跨域问题）因本批**未关闭 AI** → **不适用**。
- FD-PoC **P2**（玩家/阵亡边沿）按本场实际覆盖登记为**未验证**。
- 未验证能力不阻挡本批无关工程成果提交，但**不允许**依赖这些能力的 P1 产品功能进入开发。
- **本批整改仅覆盖 Scope C 的命令通路**；Scope B 的快照年龄上限实机未触发、Scope D 的真实回退执行未走到，均按未验证登记（见 §5）。
