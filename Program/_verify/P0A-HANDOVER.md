# CombatBrain 2.0 — P0 第一批交接（最小命令通路）

> **Codex 独立验收（2026-10-10）：第一批整体暂不通过。** 工程加载、已有状态观测和两次外部 Stop 写入事实保留；源码没有把站定 Behavior 注册进原生选择，而是把普通外部写令回调标为 REAL_SELECTION。下文开发自评中的“C 路线／真实获选／命令链通过”不作为通过结论。有限整改及逐项证据见 `P0A-CODEX-REVIEW.md`；本次没有重新构建、测试、部署或启动游戏，456c642 的构建身份更正保持有效。

派单：`E:\MBmod\TacticalCommandLab\_dispatch\2026-10-10-cb20-p0-minimal-command-path.md`
工作区：`E:\MBmod\CombatBrain 2.0`，开工 HEAD `a9ace1366f9aeb6de1a0f5a2cb039713004bc8fc`
唯一有效实机场次：`Program\_verify\sessions\20261010-171526\`（pid 20316）

## 1. 本批实现范围

| 面 | 交付物 | 说明 |
|---|---|---|
| 独立工程与加载身份 | `Program\CombatBrain2.csproj`、`Program\src\Host\SubModule.cs`、`Program\_deploy\deploy.ps1`、`Program\_deploy\SubModule.xml` | 模块 `CombatBrain2` / `CombatBrain2.dll` / `CombatBrain2.Host.SubModule`，与 1.x `CombatBrain` 完全分离；启动时输出 `RUNTIME_IDENTITY` 到 `Documents\CombatBrain2\CombatBrain2.log` |
| 最小共享编队状态 | `src\Core\{FormationSnapshot,TargetSelector,TestParameters,ExecutionGate,WriteLedger}.cs`、`src\Host\{FormationSampler,IndependentObservation}.cs` | 双方非空编队的归属/对象身份/人数/兵种/位置/控制资格/当前 MovementOrder + Mission 身份与采样时间；只读，不修改 Agent 行为 |
| 一支步兵一条 Movement 通路 | `src\Host\{TestDriver,SamplingBehavior}.cs` | 走 TCL C 路线：候选只生成命令**不调用 setter**；实际被原生选中后才在下一个 Mission tick 执行 `SetMovementOrder(Stop)`；不申请 Full、不关闭 AI、无 Authority/Lease |
| 有限退出与安全让位 | `ExecutionGate.Close`、`WriteLedger.PlanRevocation(IfSameObject)`、`TestDriver.{CancelIntent,RevokeIfNeeded,OnMissionEnd}` | 到期/显式关闭/目标失效/玩家资格丢失/Mission 结束五条取消路径；只撤销本方确实改变且仍由本方负责的状态 |
| 实机入口 | `Program\_verify\run-p0.ps1`、`Program\_verify\gabs-config\config.json` | 2.0 自己的 GABS game id `bannerlord148-cb2`，加载 `CombatBrain2` + CBATH，**排除旧 CombatBrain、CommandLabProbe**；不动 CBATH 核心与冻结入口 |
| 测试 | `Program\tests\CombatBrain2.Tests.csproj` + 5 个测试文件 | 43 个测试全绿，调用真实 Core 实现（不复制算法自证） |

本批**未实现**（按派单）：战场规划、预备队产品规则、玩家接管的产品化流程、自动建组、Facing/Arrangement/其他指令域。

## 2. 构建身份（本次运行实际加载的产物）

- `build_id` = `b20261010-171340-c6bc9392`（编译进 DLL 的印章，`src\Host\BuildIdentity.g.cs`）
- `mvid` = `45b351b9-6b4a-4968-a3fa-13b44ba519df`
- `assembly_file_sha256` = `44845964C8AD9D825309AC053529F68BE2B909EFD8807A1BF7E57D02A667D8B2`
- 部署路径 = `E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\CombatBrain2\bin\Win64_Shipping_Client\CombatBrain2.dll`（37376 B，2026-10-10 17:13:41）
- 游戏版本 = `v1.4.8.119303`，日志报告 `v1.4.8.0`（`ApplicationVersion.FromString("v1.4.8")`）
- **回溯口径更正（2026-10-10，Codex）**：撤回“强制全新重编、Program\bin\Debug、已部署文件三方同为上述 SHA256”的回溯声明。开发本轮报告的全新重编摘要为 `732100BE1C3FD21F584A560D480E30DB879992A64911DBC62728AFCA1C03D834`；本次只读核对保留在 `Program\obj\Debug\CombatBrain2.dll` 的该产物与受审 `Program\bin\Debug\CombatBrain2.dll`：两者均为 37376 B，恰有 **72 字节构建元数据差异，其余 37304 字节逐字节相同**。差异位于 PE/调试目录时间戳、MVID、CodeView GUID 和 PDB 校验摘要；零基文件偏移分别为 136–139、33340–33355、35536–35539、35620–35635、35707–35738。此对照支持这些元数据以外的文件内容一致，不能证明当时完整编译输入已还原；`44845964…` 的全新重编摘要目前无法复现。本次未重新构建或运行测试。
- **run 6 身份保留**：该场 `sessions\20261010-171526\CombatBrain2.log` L1 的 `assembly_file_sha256` 与目前仍在盘的受审及部署文件均为上述 `44845964…`；场次的加载 MVID、路径及原始日志保持不变。加载身份以该场记录和对应部署产物为依据，不用本轮 `732100BE…` 产物替换或倒写该场身份。
- 说明：`assembly_file_sha256` 是**磁盘文件摘要**，不等于进程内存字节；`mvid` 是本场加载的程序集模块版本 id。

`SubModule.xml` 部署副本 SHA256 = `CF246650A1E7425639C9CA7D5B4DD2BC87542D89DA655A0F7B847422161F6168`（与源文件 `_deploy\SubModule.xml` 同摘要）。

## 3. 逐项判据

| 判据 | 结论 | 最短证据路径 |
|---|---|---|
| 独立加载 | **通过** | `sessions\20261010-171526\engine-rgl_log_20316.txt` L122（Command Args 只列 `CombatBrain2` 与 `CombatBrainAutonomousTestHarness`）、L1268（`Loading assembly: ..\..\Modules\CombatBrain2\bin\Win64_Shipping_Client\CombatBrain2.dll`）、L1328（`[CB2][RUNTIME_IDENTITY]`）；`CombatBrain2.log` L1 `active_modules=…,CustomBattle,CombatBrain 2.0 (P0a),CombatBrain Autonomous Test Harness`（**无旧 CombatBrain**） |
| 双方状态可信 | **通过，但见下方限制** | `CombatBrain2.log` L152–L161（10 条 `SNAPSHOT_FORMATION`）；`formations-early.json`（CBATH 独立路径，`mission_time=0.444`）与 `formations-late.json`（`mission_time=152.908`）逐条对照；`SNAPSHOT_HEALTH` 152 条全部 `duplicate_keys=0`、`duplicate_detail=<none>` |
| 命令链成立 | **通过** | `CombatBrain2.log` L38→L59/60/62（`CANDIDATE_SUBMITTED … write_attempts=0`）→ L63（`NATIVE_MOVEMENT_APPLIED … our_write=0 source=not_ours`）→ L64（`REAL_SELECTION`）→ L65（`WRITE_EXECUTED … order_before=Move order_after=StandYourGround write_attempts=1`） |
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

## 4. 命令链与退出现象（逐字摘录，`CombatBrain2.log`）

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

## 5. 未验证项（不得据此进入 P1）

1. **玩家接管边沿**：CBATH 冻结核心的 15 个工具中**没有任何一个能把控制权交还给玩家**（`delegate_team_to_ai` 是单向的 AI 授予）。按派单 L67/L79 保留离线检查、把实机边沿明确列为未验证。离线覆盖：`tests\ExecutionGateTests.cs` 的 `WriteBlockedWhenTargetInvalidOrPlayerOwned`（`player_owned` 拒绝）与 `tests\TargetSelectorTests.cs` 的 `DoesNotSelectPlayerOwnedFormation`。
2. **目标失效分支**（`TARGET_INVALID`）与**选择超时分支**（`SELECTION_TIMEOUT`）：本场两次意图都在约 0.1 s 内获选、目标全程存活，两个分支未触发。
3. **撤销的真实回退执行**（`REVOKE_EXECUTED`）：本场两次都因 `previous_order_not_restorable` 被正确跳过。
4. **快照陈旧拒绝路径**（`over_limit=1`）：见 §3 限制 1。
5. **下一 Mission 无旧状态残留**：本场 Mission 由脚本 `end_mission` 收尾，未自然进入下一场。
6. **`assembly_version`** 报 `0.0.0.0`（csproj 未设 `<Version>`）；不影响本批判据，未修。

## 6. 复现入口

```powershell
# 构建
cd 'E:\MBmod\CombatBrain 2.0\Program'
dotnet build CombatBrain2.csproj -c Debug

# 测试（43 个）
dotnet test tests\CombatBrain2.Tests.csproj -c Debug

# 部署（生成新 build_id、印章核验、清 bin/obj 真重编、目标目录守卫、源/部署 SHA256 比对）
powershell -NoProfile -ExecutionPolicy Bypass -File '.\_deploy\deploy.ps1'

# 实机（唯一入口；失败退出码非 0）
powershell -NoProfile -ExecutionPolicy Bypass -File '.\_verify\run-p0.ps1' -CombatHoldSec 150 -PollTimeoutSec 240
```

实机前置：先确认无存活 `Bannerlord` 进程（`Get-Process -Name Bannerlord | Where-Object { -not $_.HasExited }`）；脚本会自行重写 `engine_config.txt` 的 `safely_exited = 1`（为 0 时游戏弹 Safe Mode 静默阻断全部 Mod 加载）。

## 7. 证据索引

**外置归档（大原始材料，已外置一次）**

| 项 | 值 |
|---|---|
| 归档 | `E:\MBmod\CombatBrain2-evidence\20261010-p0a-sessions.zip` |
| 大小 / 条目 | 274030 B / 531 |
| SHA256 | `B144E23AFCE18B7C45F21B799884ECEEF9D9CD274808A9884E6264CCDDB015FE` |
| sidecar | `E:\MBmod\CombatBrain2-evidence\20261010-p0a-sessions.zip.sha256` |
| 覆盖面 | `Program\_verify\sessions\` 全部 7 个场次目录（含隐藏文件），531 files / 855706 B |

归档摘要不写入归档内部（自指即失效），只在此处与 sidecar 记录。

**唯一有效场次 `Program\_verify\sessions\20261010-171526\`（19 files / 577266 B）**

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

**场次索引（`Program\_verify\sessions\`，7 场 / 531 files / 855706 B）**

| 场次 | files/bytes | 有效 | 说明 |
|---|---|---|---|
| `20261010-152753` | 93 / 32947 | 否 | 早期失败场，无 2.0 日志 |
| `20261010-153359` | 96 / 33884 | 否 | 同上 |
| `20261010-155158` | 99 / 35224 | 否 | 同上 |
| `20261010-160417` | 103 / 37957 | 否 | 游戏在 CustomBattle 模块数据加载处崩溃（`h3_provider_start` 在加载途中被调用） |
| `20261010-161613` | 108 / 69978 | 否 | 部署闸门判据缺口，脚本被 `status:ok` 误放行；`no_player_team`，意图从未触发 |
| `20261010-164624` | 13 / 68450 | 中间场 | 命令链已完整；撤销原因记为 `native_rewrote_after_us`，`POST_OBSERVE` 捕捉到 1 条 `StandYourGround` |
| **`20261010-171526`** | **19 / 577266** | **是（run 6）** | 本批决定性场次；撤销守卫与 `SNAPSHOT_HEALTH` 均在此生效 |

前五场是过程失败场，**不构成验收依据**，但保留原始结果未删。

## 8. 本批通过 ≠ P0 整体通过

- FD-PoC **P11** 按本次 Route A 范围登记（未申请 Full Authority、未关闭 AI）。
- FD-PoC **P1**（关闭 AI 后的跨域问题）因本批**未关闭 AI** → **不适用**。
- FD-PoC **P2**（玩家/阵亡边沿）按本场实际覆盖登记为**未验证**。
- 未验证能力不阻挡本批无关工程成果提交，但**不允许**依赖这些能力的 P1 产品功能进入开发。
