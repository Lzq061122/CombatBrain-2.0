## P1 第一批 — 实机场次索引

**Codex最终裁定（2026-10-12）：P1 最小指挥闭环通过，可以阶段收口。** `d9c892a` 已修复最后获选截止执行边界；决定性场次为 P `20261012-152820`、N `20261012-153146`，11 条写令逐条对齐独立时间线。最终受审 DLL 拒绝截止时及截止后才首次获选的普通任务，及时获选不误拒。自然实机超时/晚到分支仍未触发，不登记为实机通过。V1–V14、P 退出统计更正及保留边界见 `../P1-STAGE-EXIT-ACCEPTANCE.md`；下文保留各轮历史交接。


**2026-10-12 第四轮（我方整改）：获选等待截止成为执行边界，已按 §5.5 复验。** 依据 `../P1-CODEX-REVERIFY-4.md` §2，补两处执行边界：(1) 到期关闭由 3 s 决策点改为每 tick（`FeedbackTick` 内调 `SweepExpiredSelections`）；(2) 截止后才首次获选不得取得写令资格，共四处防线——`OnActiveBehaviorChanged` 拒绝（`REAL_SELECTION_REJECTED`）、`TryExecuteTask` 拒绝（`WRITE_REJECTED`）、`ExecutionGate.RequestWrite` 增加 `selection_window_closed` 复核、`IntentState.SelectionDeadline` 由首次投递固定。**离线用受审构建产物验证复核 §2.2 原反例**（首次提交 10、获选 22、截止 20）：`Allowed=False / BlockReason=selection_window_closed`；区分性对照 获选 19.5 与同刻 10 均 `Allowed=True`。**两场实机均未触发该路径**：P `20261012-152820` 7 次投递中 6 次获选（22–50 ms），另有退出候选未获选，N `20261012-153146` 5 次投递全部 13–477 ms 内获选，`TASK_SELECTION_TIMEOUT / REAL_SELECTION_REJECTED / WRITE_REJECTED` 全为 0，故本批不构成该修复点的实机触发证据（离线反例与单测为证）。归档 `E:\MBmod\CombatBrain2-evidence\20261012-p1-reverify4-sessions.zip`（244956 B / 46 条目，SHA256 `D08A4699795AC5B2F9C7FCCC54169DEA79A895149AD6F9926C00700DBB267D27`，逐文件 46/46 一致）。**本批结论不预先登记 P1 阶段通过。**

| 场次 | 配置 | files | RESULT | 有效 | 说明 |
|---|---|---|---|---|---|
| **`20261012-152820`** | P | 22 | **OK** | **是** | **执行边界整改后场（P）**。build `b20261012-152815-b2152454`，sha256 `E9127151E0CDAD38C36E9A32740F0DA5604047A0C62D61B540E447B0BF87A9FC`（97792 B），MVID 见场次目录。`ranged_edge=0.397 strength_ratio=0.929`。`TASK_CANDIDATE_SUBMITTED=7 / REAL_SELECTION=6 / WRITE_EXECUTED=6 / PLAN_CREATED=3 / PLAN_CONTINUED=46 / PLAN_CHANGED=0`。**未发生获选超时**：`plan-4-task-1` 19.559 投递→19.581 获选（22 ms）；`plan-1-task-1` 4.542→4.592（50 ms）。写令顺序严格 Ranged(task-1)→Infantry(task-2)。`HANDOFF_DROPPED=0 / MISSION_CLEANUP=1` |
| **`20261012-153146`** | N | 22 | **OK** | **是** | **执行边界整改后场（N）**。build `b20261012-153145-85ff2ec0`，sha256 `856AFD22AC81762871D4BAFDDEF3C32065ECED9528C3CB46EAAF51A0AA9225DA`（97792 B）。`ranged_edge=-0.335`。`TASK_CANDIDATE_SUBMITTED=5 / REAL_SELECTION=5 / WRITE_EXECUTED=5`。**未发生获选超时**：获选延迟 13–477 ms（`plan-4-task-2` 22.570→23.031）。退出动作 `EXIT_STOP_QUEUED=1 / DONE=1 / NOT_APPLIED=0`，`plan-1-task-2-exit` 11.036 写出。`HANDOFF_DROPPED=0 / MISSION_CLEANUP=1` |


**2026-10-12 第三轮：获选等待锚点整改后复验完成，再次交回独立验收。** 依据 `../P1-CODEX-REVERIFY-3.md` §3，整改仅一处：同一 task_id 固定首次候选提交时刻，重复投递不刷新 10 s 获选截止。决定性场次 `20261012-145644`（P）与 `20261012-150054`（N）。原始材料归档 `E:\MBmod\CombatBrain2-evidence\20261012-p1-reverify3-sessions.zip`（280479 B / 46 条目，SHA256 `91141A4ACE2530C06B545D4476D0C068606CFD77E442AE0ABCAD96D1FAF59051`，逐文件 46/46 一致）。**本批结论同样不预先登记 P1 阶段通过。**

| 场次 | 配置 | files | RESULT | 有效 | 说明 |
|---|---|---|---|---|---|
| **`20261012-145644`** | P | 22 | **OK** | **是** | **第三轮整改后决定性场（P）**。build `b20261012-145642-5d9874f4`，sha256 `467A10531BF01722F5E1146E8D9248141AFC3B7FE3CBDDDE630B6F0443B439E6`（96768 B）。`ranged_edge=0.397 strength_ratio=0.929`。**本轮修复点直接证据**：`plan-4-task-1` 投递 4 次（94.654/97.659/100.662/103.662），`first_submitted_at=94.654`、`selection_deadline=104.654` 四次不变；104.654 后停止投递；106.664 `TASK_SELECTION_TIMEOUT waited=12.010 limit=10.000`；`plan-4` 关闭 `reason=SelectionTimeout`。9 条写令顺序严格 Ranged(task-1)→Infantry(task-2)，间隔 2.950–8.551 s 均 ≥1.5 s。`REAL_SELECTION=9 / WRITE_EXECUTED=9`，`PLAN_CHANGED=0 / HANDOFF_DROPPED=0 / PLAN_INVALIDATE_REJECTED=0 / SNAPSHOT_REFUSED=0 / EXIT_STOP_EXPIRED=0`。退出动作 `EXIT_STOP_DONE=1 / NOT_APPLIED=1` |
| **`20261012-150054`** | N | 22 | **OK** | **是** | **第三轮整改后决定性场（N）**。build `b20261012-150053-d975e954`，sha256 `1C42FE3307A7643B2D03034BC4136DFF472317319C4A5EC78BF83F75A69F4E4E`（96768 B）。`ranged_edge=-0.335 strength_ratio=0.919`。5 次投递全部一次获选，**无重投场景，不构成该修复点的验证**，但与修复不冲突。写令顺序 `plan-1/4-task-1`（Hold）先、`-task-2`（Reposition）后，间隔 3.451–8.547 s。`EXIT_STOP_DONE=1 / NOT_APPLIED=0`，`plan-1-task-2-exit` 11.039 实际写出 `StandYourGround`。`PLAN_CHANGED=0 / HANDOFF_DROPPED=0 / MISSION_CLEANUP=1`。反馈按 TaskKind 分支：Hold `hold_no_displacement_required`、Reposition `closed_distance=3.18/3.17 from=20.00` |
**2026-10-12：P1 最小指挥闭环两场主对照已跑完，交回独立验收。** 实现交接见 `../P1-IMPLEMENTATION-HANDOVER.md`，六项修复复验证据见 `../_p1_reverify_evidence.md`，修复依据 `../P1-CODEX-FIRST-LIVE-REVIEW.md`（提交 `113dbb2`）。本批结论**不预先登记 P1 阶段通过**。

| 场次 | 配置 | files / bytes | RESULT | 有效 | 说明 |
|---|---|---|---|---|---|
| **`20261012-094559`** | P | 16 / 1102.9 KB | **OK** | **是** | **P1 主对照（远程优势成立）**。build 载入 sha256 `1CB4F9A264A8C408205E36B63FB1F62F47E18CD848783739B10EF6B8ED57DF53`（83456 B），pid 40848。请求我方 30/55/15/0、敌方 70/20/10/0；CBATH 独立读数我方 team=1 共 92 人（远程 56 / 步兵 15）、玩家队 team=0 共 99 人（远程 21 / 步兵 66）→ `rangedEdge=+0.3966`。`PLAN_CREATED=9 / PLAN_INVALIDATED=9 / PLAN_CHANGED=1 / PLAN_CONTINUED=27`；`WRITE_EXECUTED=10`（含 1 条退出）、`TASK_FEEDBACK effective=6 / failed=0`；首令 `at=3.041 first_command_delay=1.505`，编队间隔 1.544 s；资格失效 `at=9.539` 同 tick 关闭计划 |
| **`20261012-094953`** | N | 16 / 1174.7 KB | **OK** | **是** | **P1 主对照（远程优势不成立）**。同 build sha256，pid 20924。请求我方 70/20/10/0、敌方 30/55/15/0；CBATH 独立读数我方 team=1 共 91 人（远程 21 / 步兵 58）、玩家队 team=0 共 99 人（远程 56 / 步兵 22）→ `rangedEdge=−0.3349`。`PLAN_CREATED=3 / PLAN_INVALIDATED=3 / PLAN_CHANGED=0 / PLAN_CONTINUED=47`；`WRITE_EXECUTED=4`、`TASK_FEEDBACK effective=3 / failed=1`（`plan-1-task-2` `NotEffective projected_displacement=2.51 <3.0`）；资格失效 `at=15.542` 同 tick 关闭计划 |
| `20261012-013102` | P | 16 / 1014.5 KB | OK | 否（已被取代） | P1 前轮场次。**保留为被复核的失败证据**：V9 首令延迟 0.014 s、编队间隔 0.043 s；V10 失资格后 4.005 s 计划仍开放；V11 旧计划退出任务反馈取消新计划。载入 sha256 `51F53C56…` |
| `20261012-013618` | N | 16 / 1137.8 KB | OK | 否（已被取代） | 同上，V9 为 0.063/0.441 s。载入 sha256 `51F53C56…` |
| `20261012-012443` | P | 10 / 5.4 KB | FAILURES (3) | 否 | **runner 参数名缺陷场**：`h3_start_from_screen` 以扁平参数调用，桥接层拒绝 `Unknown argument "enemyArmySize"`。属 §5.5(c) 相关源码改变，**2.0 代码未执行**，不构成对 §12 VM 路径的证据 |

**2026-10-12 第二轮：四处整改后复验完成，再次交回独立验收。** 依据 `../P1-CODEX-REVERIFY.md` 的四处限定整改，修改后按 §5.5 复跑 P/N。整改后的决定性场次为 `20261012-122809`（P）与 `20261012-123126`（N），载入 sha256 `88709E862E904CD183BE5F97F01348DE73B8523AFEF9FCBC0A1DB27312EB4246`。**本批结论同样不预先登记 P1 阶段通过**；上一批两场保留为对照。

| 场次 | 配置 | files | RESULT | 有效 | 说明 |
|---|---|---|---|---|---|
| **`20261012-122809`** | P | 22 | **OK** | **是** | **四处整改后决定性场（P）**。build `b20261012-122324-537f0129`，sha256 `88709E86…`（95232 B）。`ranged_edge=0.397`。**发令顺序重回脚本固定序**：`plan-1/4/5-task-1`（Ranged）先写、`-task-2`（Infantry）后写，三处均成立。首令延迟 3.035–3.336 s、编队间隔 2.946–3.456 s（均 ≥1.5 s）；`WRITE_DELAYED` 由 `submit_pending` 发起（`first_command_delay` 6 次、`predecessor_not_written` 3 次）。`WRITE_EXECUTED=6`、`TASK_FEEDBACK=7`、`PLAN_CONDITION_LOST=0`、`PLAN_CHANGED=0`、`PLAN_INVALIDATE_REJECTED=0`。资格失效 9.761 / 136.430 均同 tick 关闭本计划；`MISSION_CLEANUP=1` |
| **`20261012-123126`** | N | 22 | **OK** | **是** | **四处整改后决定性场（N）**。同 build 与 sha256。`ranged_edge=-0.335`。顺序同形：`plan-1/4-task-1`（Hold）先写、`-task-2`（Reposition）后写。首令延迟 3.053/3.501 s、间隔 3.449/2.960 s。`WRITE_EXECUTED=4`、`TASK_FEEDBACK=4`（`closed_distance` 均以写令时基线计：`from=20.00`）、`PLAN_CONDITION_LOST=0`、`MISSION_CLEANUP=1` |
| `20261012-121443` | P | 22 | OK | 否（已被取代） | 四处整改的中间场：顺序与延迟已达标，但**暴露延迟门位置缺陷**（`plan-3` 提交后仅记 `first_command_delay`，该次原生一次性获选即被消耗，此后至 Mission 结束无写出）。保留为缺陷对照 |
| `20261012-121754` | N | 22 | OK | 否（已被取代） | 同上，缺陷同形 |
| `20261012-122329` | P | 7 | FAILURES (2) | 否 | **启动期失败场**：CBATH `ping` 未应答（`no_ping`），2.0 模块已加载但桥接未就绪。属 §5.5(c)，代码未执行；同条件重跑即通过 |

- 四处整改的逐项证据见 `../P1-CODEX-REVERIFY-RESPONSE.md`。
- `20261012-121443` / `20261012-121754` 不重写，保留为修复前后对照。

- P1 两场的逐项验收（V1～V14）与未验证项登记见 `../P1-IMPLEMENTATION-HANDOVER.md` §4 与 §7。
- 前轮两场**不重写**，作为修复前后对照保留。
- `_lastrun*.txt` 控制台副本按既有约定不入库。

## P0 批次 — 实机场次索引

**当前阶段结论（Codex，2026-10-11）：Gate 3 两项补证经独立复核通过，P0 在限定控制范围内正式退出，可以进入 P1。** 本轮补证场为 `20261011-162100`（玩家队身份边沿）与 `20261011-164000`（独立失资格时间线），阶段验收见 `../P0-STAGE-EXIT-ACCEPTANCE.md`。下方既有批次描述保留为历史，待复核措辞不代表当前状态。

**2026-10-11 第三轮：Gate 3 补证已完成。** Codex 复核提出的两项缺口已补：`20261011-164000` 提供 CBATH 独立 1 Hz 失资格观测（8 条 `is_ai_controlled=false`），`20261011-162100` 以公开原生 `Mission.JoinEnemyTeam()` 补齐玩家身份变化的适用域边沿；run 7 身份勘误已更正。受审决定性场仍为 `20261011-133859`，阶段结论待 Codex 再复核，见 `../P0-EXIT-GATE3-RUN-REPORT.md` §7.1。`../P0-EXIT-GATE3-CODEX-REVIEW.md` 保留为历史审查结论，不改写；原始归档不重写，本轮材料另归档为 `20261011-gate3-supplement-sessions.zip`。

Codex 2026-10-11 已按限定范围验收第一批最小命令通路：run 7 的单槽位同步修复及受影响通路已独立核对，原有阻挡关闭。当前决定性场次为 **run 7 `20261011-002337`**，当前验收见 `../P0A-CODEX-ACCEPTANCE.md`。未验证边界保留；不以单场 RESULT: OK 代替 P0 整体验收。

# CombatBrain 2.0 实机场次索引

本文件是唯一提交进仓库的场次索引。**完整场次材料外置归档，不入库**；归档摘要不写入归档内部（自指即失效）。

## 外置归档

| 项 | 值 |
|---|---|
| **归档（当前，P1 四处整改后复验：2 决定场 + 2 中间场 + 1 启动失败场，并含上批 5 场，共 8 场）** | `E:\MBmod\CombatBrain2-evidence\20261012-p1-reverify-sessions.zip` |
| 大小 / 条目 | **753161 B / 157** |
| SHA256 | `A13CA02B52A87FA49A73F5540C340834A01F3F53D786EB31E8E42D7C734FA3D2` |
| sidecar | `E:\MBmod\CombatBrain2-evidence\20261012-p1-reverify-sessions.zip.sha256` |
| 覆盖面 | `20261012-122809`（整改后 P，决定性）、`20261012-123126`（整改后 N，决定性）、`20261012-121443`/`20261012-121754`（整改中间场，含延迟门位置缺陷）、`20261012-122329`（启动期 ping 失败场）、`20261012-094559`/`20261012-094953`（上批 P/N）、`20261012-013102`/`20261012-013618`（前轮失败证据）、`20261012-012443`（runner 参数名缺陷场） |
| 逐场次哈希校验 | 已对 8 个场次逐个「解压后文件集合哈希 vs 源目录」比对，**8/8 MATCH** |
| 归档（上批，覆盖 19 场，含 Gate 3 补证两场） | `E:\MBmod\CombatBrain2-evidence\20261011-gate3-supplement-sessions.zip` |
| 大小 / 条目 | **8925131 B / 666** |
| SHA256 | `C86C44A5A7B323A8D9D9AE4733BC24EB22C79FFB3514A8D5A0E1A254799183C4` |
| sidecar | `E:\MBmod\CombatBrain2-evidence\20261011-gate3-supplement-sessions.zip.sha256` |
| 覆盖面 | `Program\_verify\sessions\` 全部 **19** 个场次目录（含 Gate 3 决定性场 `20261011-133859`、补证场 `20261011-162100` / `20261011-164000`）+ `_lastrun\*.txt` 11 个控制台副本 |
| 归档（上批，覆盖 17 场，P0 Exit Gate 3） | `E:\MBmod\CombatBrain2-evidence\20261011-gate3-sessions.zip` |
| 大小 / 条目 | 8829130 B / 610 |
| SHA256 | `35F99ECB26213FF23AA1719E055310CA8D5B2437C5579B19BF6FEDADA57F4A73` |
| sidecar | `E:\MBmod\CombatBrain2-evidence\20261011-gate3-sessions.zip.sha256` |
| 覆盖面 | 17 个场次目录 + `_lastrun\*.txt`；**已被引上者取代**（不重写，保留为上一时点的完整快照） |
| 归档（上批，P0a 收口，已被上者取代） | `E:\MBmod\CombatBrain2-evidence\20261010-p0a-sessions-run10.zip`，8800734 B / 610，`EE6EB6D1BF261252B4F5F93E162847DA12A29720955813296FDF3CBA04E49919`；仅覆盖 14 场 |
| 归档（上批，已被上者取代） | `E:\MBmod\CombatBrain2-evidence\20261010-p0a-sessions-run9.zip`，8774320 B / 594，`BC4FA254822B4CA468E889FDF83D1AA36EA86BE3184987C02D2AC5F791DC830A`；仅覆盖 13 场 |
| 归档（更早，已被上者取代） | `E:\MBmod\CombatBrain2-evidence\20261010-p0a-sessions-run7.zip`，8768831 B / 581，`E825FF34A48F024BB5FA3436BA13F4A44430A60C295635D48197F5E10C27A2FB`；仅覆盖 11 场 |
| 归档（第一批，已被上者取代） | `E:\MBmod\CombatBrain2-evidence\20261010-p0a-sessions.zip`，274030 B / 531，`B144E23AFCE18B7C45F21B799884ECEEF9D9CD274808A9884E6264CCDDB015FE`；**仅覆盖前 7 场（855706 B），不含 C 路线整改后的场次** |

## 场次索引

| 场次 | files / bytes | 2.0 日志行数 | RESULT | 有效 | 说明 |
|---|---|---|---|---|---|
| `20261010-152753` | 93 / 32947 | 0 | 无 | 否 | 早期失败场，无 2.0 日志 |
| `20261010-153359` | 96 / 33884 | 0 | 无 | 否 | 同上 |
| `20261010-155158` | 99 / 35224 | 0 | 无 | 否 | 同上 |
| `20261010-160417` | 103 / 37957 | 1 | 无 | 否 | 游戏在 CustomBattle 模块数据加载处崩溃（`h3_provider_start` 在加载途中被调用） |
| `20261010-161613` | 108 / 69978 | 182 | 无 | 否 | 部署闸门判据缺口，脚本被 `status:ok` 误放行；`no_player_team`，意图从未触发 |
| `20261010-164624` | 13 / 68450 | 355 | 无 | 中间场 | 命令链已完整；撤销原因记为 `native_rewrote_after_us` |
| **`20261010-171526`（run 6）** | **19 / 577266** | **557** | **OK** | **是** | 第一批决定性场次；Codex 验收后判 C 路线不成立 |
| `20261010-211555`（run 1） | 35 / 11711698 | 1 | FAILURES | 否 | C 路线整改后首场；模块已加载（`RUNTIME_IDENTITY` 1 行），主菜单后 `create_render_texture` 失败、资源不足，**未进入 Mission**；未直接证明是 Granite 缓存故障 |
| `20261010-212554`（run 2） | 0 / 0 | 0 | FAILURES | 否 | `games_start` 返回 `exited_during_start`，exitCode 3221225477（0xC0000005）；2.0 日志未创建 |
| `20261010-213002`（run 3） | 0 / 0 | 0 | FAILURES | 否 | `games_start` 返回 `exited_during_start`，exitCode -1；2.0 日志未创建 |
| `20261010-213621`（run 4） | 13 / 124667 | 638 | OK | 是 | C 路线整改后场次；命令链走原生行为选择链（`BEHAVIOR_REGISTERED → ACTIVE_BEHAVIOR_CHANGED ours=1 → WRITE_EXECUTED route=native_behavior_apply`）；主线程与原方法回执整改前的中间态 |
| `20261010-225030`（run 5 首跑） | 0 / 0 | 0 | FAILURES | 否 | 启动期退出；`C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_34796.txt` 末行 `Error: Non-Zero Device Reference Count! (ERC302)`；2.0 日志未创建（模块未进入加载期）。未改动任何代码，紧接着重跑即通过 |
| `20261010-225539`（run 5） | 13 / 136897 | 754 | OK | 是 | 主线程转交 + 原方法回执整改后成功场（槽位同步整改前的对照）。`MAIN_THREAD_MARKED tid=33` → `ACTIVE_BEHAVIOR_CHANGED ours=1 tid=43` → `HANDOFF_DEFERRED callback_tid=43 main_tid=33` → `HANDOFF_EXECUTED exec_tid=33 is_main_thread=1` → `WRITE_EXECUTED … original_body_entered=1 original_body_tid=33`；53 条转交全部成功，`HANDOFF_DIRECT` 0 条 |
| **`20261011-002337`（run 7）** | **13 / 136097** | **746** | **OK** | **是** | **P0a 第一批决定性场次**：`ThreadHandoff` 槽位加锁后唯一成功场（build `b20261011-002322-97c0ff2e`，pid 1512）。`MAIN_THREAD_MARKED tid=33` → `ACTIVE_BEHAVIOR_CHANGED ours=1 tid=37` → `HANDOFF_DEFERRED callback_tid=37 main_tid=33` → `HANDOFF_EXECUTED exec_tid=33 is_main_thread=1` → `WRITE_EXECUTED order_before=Move order_after=StandYourGround original_body_entered=1 original_body_tid=33`；`HANDOFF_DEFERRED 53 / HANDOFF_EXECUTED 53 / SLOT_BUSY 0 / DROPPED 0`，`WRITE_EXECUTED` 2 条 |
| `20261011-132100` | 0 / 0 | 0 | FAILURES | 否 | Gate 3 初跑启动期失败：Steam 未登录；2.0 日志未创建（`CombatBrain2.log was never created - module did not load`），`RESULT: FAILURES (3)`，无 crash dir |
| `20261011-132600` | 0 / 0 | 0 | FAILURES | 否 | 同上重跑，同样在 Steam 未登录条件下失败 |
| **`20261011-133859`（Gate 3）** | **13 / 95579** | **448** | **OK** | **是** | **P0 Exit Gate 3 决定性场次**：build `b20261011-132044-a06f2e05`，pid 35032，sha256 `4A7E9C1D05EFC0FE666E022789B11A99A149AF73491A579DFA83A9BEDCA918E8`。场景 A（intent-1，MT 15.108–15.545）：`REAL_SELECTION` → `HANDOFF_DEFERRED callback_tid=45 main_tid=30` → `PROBE_ELIGIBILITY_LOST before[ai_ctrl=1] after[ai_ctrl=0] applied=1` → `WRITE_BLOCKED reason=not_ai_controlled write_attempts_intent=0` → `INTENT_CANCELLED reason=AiControlLost` → `HANDOFF_CLEARED had_pending=0` → `BEHAVIOR_DISARMED armed_before=1 weight_before=8.000 weight_after=0.000`。场景 B（intent-2，MT 20.549–25.050）：`WRITE_EXECUTED order_before=Move order_after=StandYourGround original_body_entered=1 original_body_tid=30 write_attempts_intent=1` → 失资格 → `INTENT_CANCELLED reason=AiControlLost` → `BEHAVIOR_DISARMED weight_before=8.000 weight_after=0.000` → 恢复后 `PROBE_ELIGIBILITY_RESTORED intent_open_now=<none> same_intent_open=0 write_attempts_source_intent=1`。全会话 `WRITE_EXECUTED 1 / WRITE_BLOCKED 1 / NATIVE_MOVEMENT_APPLIED 0 / HANDOFF_SLOT_BUSY 0 / HANDOFF_DROPPED 0`。详见 `../P0-EXIT-GATE3-RUN-REPORT.md` |

| **`20261011-162100`（Gate 3 补证场 1，mode c）** | **14 / 832432** | **395** | **OK** | **是** | 同一 build `b20261011-161246-db46608a`（mvid `eea0ebe0-31e3-41cf-a871-9d4646299f8d`，sha256 `C0D59AE7124DA67DB8FD3B39CBB4B6FA4B4779694780F5D748A9074268946EE4`，55808 B），pid **44704**，哨兵 `intents=c`。**用于闭合 Codex §3 玩家接管适用域缺口**：`PROBE_TEAM_JOIN ... before[player_team=0 bound_team=1 attacker_leader=1 defender_leader=1 agent_main=1 main_agent=1] after[player_team=1 bound_team=1] target_now_player_team=1`（公开原生 `Mission.JoinEnemyTeam()`）→ `WRITE_BLOCKED reason=player_owned write_attempts_intent=0` → `INTENT_CANCELLED reason=PlayerEligibilityLost` → `BEHAVIOR_DISARMED weight_before=8.000 weight_after=0.000` → 恢复 `PROBE_ELIGIBILITY_RESTORED mode=c intent_open_now=<none> same_intent_open=0`。CBATH 独立时间线 `team_id=1.is_player_team` 在 t=16.110/17.113/18.120/19.120 为 True（t=15.107 False、t=20.125 回 False）。首次产出有效 `cbath-timeline.json`（749046 B）。详见 `../P0-EXIT-GATE3-RUN-REPORT.md` §4.7 |
| **`20261011-164000`（Gate 3 补证场 2，a,b 重跑）** | **14 / 831585** | **364** | **OK** | **是** | 同 build `b20261011-161246-db46608a`，pid **16736**，哨兵 `intents=a,b`。重跑已受审的 a/b 场景，唯一变化是 runner 取数时机。**用于闭合 Codex §2 缺证**：`cbath-timeline.json` `status=ok run_id=run-20261011-081849-1 sample_count=91`，独立 `is_ai_controlled=false` 共 **8 条**（A 窗口 t=16.097/17.098/18.103/19.104；B 窗口 t=21.110/22.115/23.119/24.122），窗口前后均有 True 对照。生产行与受审场同形：`WRITE_BLOCKED reason=not_ai_controlled write_attempts_intent=0` → `INTENT_CANCELLED reason=AiControlLost`；`WRITE_EXECUTED write_attempts_intent=1` → `INTENT_CANCELLED reason=AiControlLost`。详见 `../P0-EXIT-GATE3-RUN-REPORT.md` §4.8 |

- 前五场为过程失败场，**保留原始结果，不构成验收依据**。
- `20261010-164624` 为中间场：命令链已完整，但撤销原因记为 `native_rewrote_after_us`，且尚无 `SNAPSHOT_HEALTH`。
- run 1–3 的现有记录均未显示进入 Mission。run 1 有渲染资源不足证据，run 3 时间对应的 pid 44272 日志有 Granite OOM；run 2 只有启动访问异常，原因未定。不能统一归因或排除全部代码因素，见 `P0A-HANDOVER.md` §8 与 `P0A-CODEX-REREVIEW.md` §4。
- run 4 的真实原生获选与两次 Stop 落地已确认；主线程执行与原方法完成证据在 run 5 补齐，见 `P0A-HANDOVER.md` §3、§4.1。
- run 2/3 目录为空，原 ZIP 不含其控制台与 pid 44272 引擎日志；补存原字节到 `E:\MBmod\CombatBrain2-evidence\20261010-routec-codex-review\`，逐文件摘要见复验报告 §4。原 ZIP 未重写。
- `20261010-211555` 的 bytes（11711698）异常大，是因为该场保存了崩溃转储相关文件；其内容与判据无关，仍一并归档。

- `20261011-132100` / `20261011-132600` 为 Gate 3 的两个失败场：**失败原因是本机 Steam 当时未完成登录**（2.0 日志从未创建、无 crash dir、两者现象相同），与代码无关；不得与 run 2/3 的启动访问异常混为一谈。登录完成后同一命令（`run-p0.ps1 -OutDir ...\20261011-133859 -CombatHoldSec 120`）即跑通。
- **当前决定性场次为 Gate 3 的 `20261011-133859`**；run 7 `20261011-002337` 仍是 P0a 第一批的决定性场次。二者解决的是不同批次的问题：run 7 解决槽位同步，`20261011-133859` 解决控制权安全边沿（资格变化）。
- Gate 3 三项 Deviation 原始状态见本单报告 §5；**2026-10-11 补证后的处置**：
  - `HANDOFF_CLEARED had_pending=0`（时点安排使然）—— Codex 接受，本报告从未把 had_pending=0 描述为"清掉了一条非空命令"；
  - `SNAPSHOT_FORMATION` 未采到 `ai_ctrl=0` —— **已闭合**：补证场 `20261011-164000` 的 CBATH 独立 1 Hz 时间线给出 8 条 `is_ai_controlled=false`（报告 §4.8 / §5.2）；
  - `HANDOFF_DROPPED reason=intent_closed` 本场 0 条 —— 仍无可证明的实机证据，Codex 判"不作阻挡"，维持未验证登记（报告 §5.3 / §6 第 1 条）。
- `20261011-162100` / `20261011-164000` 是 Codex 复核（`P0-EXIT-GATE3-CODEX-REVIEW.md`，提交 `f274745`）要求的两项补证场；二者与受审场 `20261011-133859` 的关系是"补缺独立于 probe 自读 / 补玩家身份边沿"，不是替换决定性场。
- **2026-10-11 勘误**：run 7 正确身份为 47104 B / `27F8CBFA29AC1F13A848A61CDC2F761C7751517558CB294AD2DAA30113EF2416`（`20261011-002337\CombatBrain2.log` L1 已核对）；上版各处把 run 7 写成 37376 B / `44845964…`（run 6 的值）的地方已在本轮更正（报告 §1.3、§6 第 5 条）。

## 逐文件摘要（七个有效场次）

- **Gate 3 `20261011-133859`（当前决定性）**：13 个文件 / 95579 B，逐个 SHA256 见 `../P0-EXIT-GATE3-RUN-REPORT.md` §0/§4（本出场次已在归档 `20261011-gate3-sessions.zip` 内逐文件保存）。
- **run 7 `20261011-002337`（P0a 决定性）**：完整表（13 个文件 / 136097 B 的逐个 SHA256）见 `../P0A-HANDOVER.md` §7 场次 2。
- run 5 `20261010-225539`：完整表（13 个文件 / 136897 B 的逐个 SHA256）见 `../P0A-HANDOVER.md` §7 场次 3。
- run 4 `20261010-213621`：完整表（13 个文件 / 124667 B 的逐个 SHA256）见 `../P0A-HANDOVER.md` §7 场次 4。
- run 6 `20261010-171526`：完整表（19 个文件 / 577266 B 的逐个 SHA256）见 `../P0A-HANDOVER.md` §7 场次 1。
- **Gate 3 补证场 1 `20261011-162100`**：14 个文件 / 832432 B。`cbath-timeline.json` 749046 B → `FF7EEE788FFE7E374BF41A119696F2B40259CA79A19A24878F78D0EBB3AADC88`；`CombatBrain2.log` 63378 B → `FCB13C640FEEF6A9CCC441D5888188E32E5344E71F43B59D955BA7804AEA0C53`；`CombatBrain2.log` SHA256 见上，其余文件摘要见报告 §4.7 与本报告下方。
- **Gate 3 补证场 2 `20261011-164000`**：14 个文件 / 831585 B。`cbath-timeline.json` 750858 B → `A352C2B9FD1AD5F26FBE892A8CB97609A750A4C79CCF3FA5D53CD7AEFFB5DA19`；`CombatBrain2.log` 60678 B → `62681CEFD650DA98DEAAB4084FD9127BD576BC20FB423B5CDBFEFE06A6168ED1`。
- 六者数据同源（均从磁盘现算），如有出入以归档内实际文件为准。

## 未提交内容说明

- `sessions/` 下全部场次材料：外置归档，仓库只留本索引。
- `gabs-config/bannerlord148-cb2/`：GABS 运行时状态（`bridge.json` / `runtime.json` / `history.json` / `launch.log`），非交付物。配置本体 `gabs-config/config.json` 已提交。**`runtime.json` 只在游戏运行期间存在**——GABS 在 claim/spawn 时写入、clean stop 后删除；静态检查该目录时看不到它属正常，`launch.log` 是每次启动 `O_TRUNC` 重写的。
- `Program/_scratch/`：构建期反射探针与反编译产物（临时工具，非交付物），磁盘保留、不入库。
- `Program/_verify/_r5sim/`、`_gate2probe/`、`_stagesim/`、`_tamper4/`：第四、五轮及首场实机后的离线取证脚手架（隔离模式生成器与结果 JSON、门2 定向探针、stage 替身、审查副本），非交付物，磁盘保留、不入库。用法见 `../P3-IMPLEMENTATION-HANDOVER.md` §5.4。
- `_verify/_lastrun*.txt`：实机脚本控制台输出副本，非交付物。

## 第三轮 P1 复验（2026-10-12，两处缺口修复后）

第三轮针对 `P1-CODEX-REVERIFY-2.md` §3 两处缺口修复后的复验，两场均由共享协调入口 `coordination/Invoke-ExclusiveGame.ps1`（Owner=主线组）串行执行完整流程。

- **配置 P `20261012-142820`**：`RESULT: OK`，exit 0。载入模块 SHA256 `36545E1984F92FF76817D4D25D74243946D1E86BA691CBFE61AC5AEBFF444D8D`（96256 B，与部署文件一致）。
- **配置 N `20261012-143135`**：`RESULT: OK`，exit 0。载入模块 SHA256 `3A3D66E7282280D8742A1DB10848CD6AD34ED9CB89C45967A587FF1AEC0EB7E9`（96256 B，与部署文件一致，同源码重复构建摘要不同属既定事实）。

两场关键事实：

- 缺口 1（读回）：P 两条 Reposition `readback_error=0.000`；N 三条 Reposition `readback_error=0.000`。缺失读回不再判生效。
- 缺口 2（退出生命周期）：N `plan-1-task-2-exit` 10.269 入队 → 11.031 `REAL_SELECTION` → 11.038 `EXIT_STOP_DONE`（`order_after=StandYourGround`）；P `plan-3-task-1-exit` 94.536 入队 → 95.089 `EXIT_STOP_DONE`。两场 `HANDOFF_DROPPED=0`。
- 未获得退出实例：P `plan-1-task-2-exit`、N `plan-4-task-1-exit` 均在 1 s 期限内按 `not_completed_in_time` 关闭并撤权。
- 发令顺序：两场每个计划均先 Ranged（task-1）后 Infantry（task-2）。
- 写令延迟：均 ≥1.5 s（P 首令 3.014/3.491，N 由 `first_command_delay` 6 次与 `predecessor_not_written` 3 次拦截）。
- 继续条件：两场 `PLAN_CONDITION_LOST=0`、`PLAN_CHANGED=0`；但 strength_ratio 分别为 P 恒 0.929、N 恒 0.919，**均未落入 (0.70,0.80) 区间，继续条件未被实机触发**，不得登记为已验证。
- 两场 `MISSION_CLEANUP=1`、`PLAN_INVALIDATE_REJECTED=0`、无新崩溃目录、无残留进程。

外置归档：`E:\MBmod\CombatBrain2-evidence\20261012-p1-reverify2-sessions.zip`，309541 B / 46 条目，SHA256 `AD10F24223A864C481D1A8A7BCCECBF72534996B48DAF12C47866A60CADEF9C7`；逐文件对盘 **46/46 MATCH**。含两场次目录与 `_p1_exclusive_P.log` / `_p1_exclusive_N.log`。


## P3 接续整改 — 实机场次索引（2026-10-16 追加）

依据 `CB2-PROMPT-0005`（`../P3-RESTART-REMEDIATION-DISPATCH.md`）与 `CB2-PROMPT-0006`（`../P3-COORDINATE-EXECUTION-CONTINUATION-DISPATCH.md`）；原验收合同 `CB2-PROMPT-0004`。**状态：限定自检完成，待独立验收。** 完整说明见 `../P3-IMPLEMENTATION-HANDOVER.md`。

CB2 实现提交 `ced0a1895929524d8d3f5a628e112370422f265c`（2026-10-16 00:10:00）→ 接续提交 `732f6df` "fix(p3): close coordinate, delivery, R1/R4/R5 continuation items"（master，19 files, +1467/−164）→ 交回文档提交 `5bbdeeb`（2 files, +352/−142）→ 独立复核退回后第二轮整补 `5ac034b` "fix(p3): close review round-2 coordinate wiring, gate-2 evidence and process ownership"（11 files, +475/−146，含新建 `Program/src/Core/ProposalWiring.cs`）→ 第二轮复核退回后**第三轮整改提交 `85e2171`** "fix(p3): attribute round-2 process ownership and gate-2 successor orders"（4 files, +120/−30）+ 文档提交 `badd8d3` → 第三轮复核退回后**第四轮整改提交 `1e7b088`** "fix(p3): bind gate-2 successor readings to live orders and PID ownership to runtime identity"（相对 `badd8d3`：`Program/_verify/check-p3-feedback.ps1` +95/−18、`Program/_verify/run-p3.ps1` +173/−21、本文件 +21/−4、`../P3-IMPLEMENTATION-HANDOVER.md` 随批同交；**未改 C# 源码**），其文档哈希提交 `3ecfa2c`。第五轮定向复核退回一处防误杀后**本轮整改提交 `573f098`** "fix(p3): bind process ownership to this run's own launch context"（3 files, +127/−82；相对 `76713ef` 逐路径：`Program/_verify/run-p3.ps1` +69/−54 归属改绑本场启动上下文、`../P3-IMPLEMENTATION-HANDOVER.md` +48/−21、本文件 +10/−7），**同样未改 C# 源码**。CBATH 实现提交 `5a2275de434e953802d8de8498b3f09ca1a4fbb6`（`codex/cb20-p2-evidence`，未变）。第四、五轮后均未重跑 `dotnet test`（两轮无 C# 改动，沿用 309/309）。全部提交为本地提交，未 push、未建 PR。

**首场实机后的第六轮改动提交 `2a8de3b`** "fix(p3): correct aim-point selection and lock read-back after the first real run"（3 files, +89/−13；相对 `d04c65b` 逐路径实测：`Program/_verify/run-p3.ps1` +21/−4 `Find-OurMain` 兵种兜底与 `Assert-CleanEndState` 锁读取、`../P3-IMPLEMENTATION-HANDOVER.md` +61/−5、本文件 +7/−4）；**未改 C# 源码**，故未重跑 `dotnet test`。

**第七轮改动（定向复核 `E:\Agent\codex\2026-10-17\p3-review-2a8de3b\REVIEW.md`，尚未提交）**：第六轮的兜底让控制动作得以下发，但它选的是「我方人数最多的编队」，不是冻结事件指明的 Main——本场拿到 `030dd50e@Formation` Ranged 175 @ (906.361,959.823)，而 `ACTION_FEEDBACK_FROZEN plan=plan-4 task=plan-4-task-4-g1 main=1/0 t0=52.033` 指向的是 `00bec9c9@Formation` **Cavalry 52** @ (911.057,978.689)，相距 **19.442 m**。整改只动 `Program/_verify/run-p3.ps1`：删除按人数猜的 `Find-OurMain`，新增 `Find-MainByKey`（按 `main=<team>/<slot>` 取对象，键畸形/队伍或槽位不存在/坐标不可读一律不返回对象）与 `Get-FrozenMainKey`（取日志中最后一条冻结行的键），触发时重读一次键并同时记录「武装时的键」与「实际使用的键」，实际瞄准的对象写入 `opponent-target.json`；瞄不到即 `{"status":"frozen_main_not_found"}` 如实失败，不再有「最大编队冒充 Main」。**本轮未改 C# 源码，未重跑 `dotnet test`（沿用 309/309）。这项改动不承诺任何门通过，也不改变门的判据；攻击远程编队本身可以是有效反制，但那种场景必须把目标记成远程编队。**

第二轮整补针对独立复核退回的三个 P1 缺陷与一个文档问题：①提案身份/竞争误差/写前复核统一使用战术目标语义（判定链提到 `ProposalWiring`）；②门2 改为三值判定，「没发现矛盾」不再等于「已看到停止」；③进程收尾按实际归属证据限定、归属不明不强杀、`end_mission` 只在确有本场连接时执行；④更正 handover/INDEX 中「依赖环互锁」与「超出授权」的说法。

第三轮整改针对第二轮复核（`P3-ROUND2-REVIEW-5ac034b-20261016.md`）退回的两处边界：①**新增进程数不是所有权**——`games_start` 只有自身声明 `processStarted=true` 时，唯一新增同名 PID 才可进结束名单，否则与多个未知 PID 一样登记 unresolved 并保留运行（新增 `foreignonly` 隔离模式复现复核反例）；②**门2 后续归属**——停止时刻之后的读数，若该对象在此之前已有合法后继普通写令（非旧 plan、非 `-exit`），归后继，不再仅因坐标与旧角色点相等就判旧动作未停；缺证据仍 unverified、矛盾仍 fail。

第四轮整改针对第三轮复核（`P3-ROUND3-REVIEW-85e2171-20261016.md`）退回的两处边界与一项前置：①**后继写令不再是通行证**——第三轮只要存在一条后继写令就跳过该对象后续全部独立读数，「仍执行旧目标」与「独立订单缺失」因此被误报为 pass；现要求后继写令带上目标与生效区间，读数必须与后继目标吻合才算解释，写向别处则判旧令未停；②**可结束名单必须绑定游戏进程自报的 PID 身份**——`processStarted=true` 加差集唯一只证明「曾启动过某个进程」，本场进程退出后剩余的他方 PID 会被误杀（新增 `startedthenexited` 隔离模式复现，第三轮 runner 上 `foreign_pid_stopped=True`）；③接入 `cbath/get_fight_to_death` 前置校验与 `cbath/get_last_run` 归档（新增 `fighttodeathoff` 隔离模式）。

第五轮定向复核（`P3-FOCUSED-REVIEW-1e7b088-20261017.md`，受审 `1e7b088`）确认门2 两条反例与死战不退前置接线均已通过、不必重做，只退回一处：**「身份文件证明它是谁，仍不能证明由本场启动」**。第四轮把共享目录 `Documents\CommandBrainATH` 下新出现的 `cbath-runtime-identity-<pid>.txt` 当归属依据，但另一组加载 CBATH 时也会把自己的身份文件写进**同一个共享目录**——在该反例上加入这一正常行为后，入口仍认领并结束了他方假 PID `777999`（新增 `foreignidentity` 隔离模式复现，第四轮 runner 上 `foreign_pid_stopped=True`）。整改：归属只认**本场自己的启动上下文**（本场 GABS 的 `runtime.json` `gamePid` 与 `launch.log` 里的 `pid=`，两者都在 `$here\gabs-config\<gameId>\` 内、随本场启动写入），`cbath/ping` 的 `pid` 降格为只给已点名 PID 补活性，共享身份目录在归属判定中完全弃用。**拿不到本场绑定的 PID 一律登记 unresolved 保留运行，绝不强杀。**

门状态以 `../check-p3-feedback.ps1` 逐门单列为准；退出码 0 只表示工具跑完。`-SessionDir` 必须传**绝对路径**。第四轮已对 `sessions/` 下全部 41 个场次目录 + 复核证据目录下 8 个反例副本（共 49 个目录）用工作树与 HEAD 两版检查器比对，**只有复核点名的那两个副本改变取值**（均由第三轮的误判 pass 变为 fail / unverified），其余 47 个逐门取值完全一致；非 `unverified` 的仍只有下表四行（`P-221833` 门2 pass、`N-005621` 门2 fail、`P-220249`/`P-004350` 门1 fail），无回归。

**下表的绝大多数场次产生于第二轮整补之前**（源码 mtime 20:16–20:25）。第五轮及之前的整改结论只经过离线验证，未在任何实机运行过——接手时无正式资源交回，按派单 §4/§5 只做离线工作。**表末 `p3-P-20261017-104721` 是唯一一个用本轮源码实机跑过的场次**（第五轮内容已提交 + 本轮两处未提交 runner 修复），但它 `RESULT: FAILURES (2)`、三门全 unverified，同样不构成任何门的证据。

| 场次 | build_id | 部署 SHA 前 16 | 门1 | 门2 | 门3 | 说明 |
|---|---|---|---|---|---|---|
| `p3-P-20261015-004350` | b20261015-004347-1bf63a79 | BB75414A4C4D01F2 | fail | unverified | unverified | 基线旧场；继任仅 Main Hold，遭原生覆盖 |
| `p3-N-20261015-004851` | b20261015-004848-e5b2f53f | A43F2C1BEAE69C32 | unverified | unverified | unverified | 基线旧场；无 stop/disengage 决定 |
| `p3-N-20261015-005621` | b20261015-005619-50633b36 | B9B0A38423895AD8 | unverified | **fail** | unverified | 修复前缺陷证据：`unaccounted=3`、`stopped=0 of assigned=3`、`taken_over_before_confirmation=3`、`still_on_old_order=035c31d5@Ranged,0341f44c@HorseArcher`；按裁定保留 fail |
| `p3-C-20261015-010928` | b20261015-010857-79e62453 | 8FECD9B8A5844BA7 | unverified | unverified | unverified | 基线中性场 |
| `p3-P-20261015-203136` | b20261015-203133-f7690240 | B3B3AAB3645F43C0 | unverified | unverified | unverified | 经 `run-p3-exclusive.ps1` 独占执行（stage record 完整） |
| `p3-N-20261015-211938` | b20261015-211934-fd6da03a | 91421CEF819CD39F | unverified | unverified | unverified | 经 `run-p3-exclusive.ps1` 独占执行（stage record 完整）；无 stop/disengage 决定 |
| `p3-N-20261015-213231` | b20261015-213227-5da13be9 | 35A1FA95E0EA5DEE | unverified | unverified | unverified | 旧 Plan 已被 `ConditionChanged` 关闭（termination only） |
| `p3-C-20261015-214157` | b20261015-214152-84d1958d | FE9D9ACE2C688BD9 | — | — | 对照 | 用作门3 对照 |
| `p3-P-20261015-214532` | b20261015-214529-e411067d | 68D7E0B4DD1AC385 | — | — | — | 启动通道证据缺失 |
| `p3-P-20261015-215314` | b20261015-215311-0e545b62 | 342C73DC6287EC35 | — | — | — | 启动通道证据缺失 |
| `p3-P-20261015-220249` | b20261015-220246-a9c223d9 | 7A1F981094793B67 | **fail** | unverified | unverified | Reinforce 场；继任 Guard/Support 在首个候选提交（127.708）前已因 3 s 决策网格耗尽窗口，Main Hold 写出后 0.5 s 被原生改目标 |
| `p3-C-20261015-220738` | b20261015-220735-18fd6c08 | 7D5E848C0C4AAD8E | unverified | unverified | 对照 | 门3 对照；离散事实与 P-220249 相同，几何不同 → 维持 unverified |
| `p3-P-20261015-221833` | b20261015-221826-977cb576 | DB58CBE71973F790 | unverified | **pass** | unverified | 门2 正例：必需对象**三个**（Main `030dd50e@Infantry` 134.036 / Support `038572bf@Ranged` 134.086 / Guard `008c0c1e@HorseArcher` 134.182，最后一者实际兵种 Cavalry），全在 `deadline=134.844` 前写出，`waited=0.344`，`taken_over_before_confirmation=0` |
| `p3-P-20261015-222821` | b20261015-222818-c9f62071 | 1E980EB3ACD51410 | unverified | unverified | unverified | `ACTION_FEEDBACK_FROZEN=0`，P3 未武装，不可用于任何门 |
| **`p3-P-20261017-104721`** | **b20261017-104710-f41051eb** | **51A113C6A0F57478** | unverified | unverified | unverified | **首个本轮源码实机场**（2026-10-17 10:47，经独占入口，196096 B）。`RESULT: FAILURES (2)`、`failed stage: collect`；三门全 unverified（无 reinforce、无止损/撤出决定、无对照会话）。两处失败均在主线采集脚本、非产品代码，见 `../P3-IMPLEMENTATION-HANDOVER.md` §4 的「首个本轮源码实机场」；**不可作为任何门的证据**。本场还暴露：反制目标取「我方人数最多」的 Ranged 175，而冻结的 `main=1/0` 是 Cavalry 52，相距 19.442 m |

未取得 pass 的两门与根因：门1 — 旧构建下场观测到继任协同计划未完成保护/辅助落位与主力推进，直接机制是**投递节奏 + 继任计划寿命**（3 s 决策网格使首个候选迟至公布后 2.511 s 提交，Main Hold 写出后 0.5 s 即被原生改目标），**不存在依赖环**：`BattleAssessment.cs:212/214` 的 `DependsOnTaskId` 指向 `:199` 的 Hold 等待任务，推进任务另行创建，依赖链 `main-hold → guard/support → advance` 无环；门3 — P/C 实际开局部署质心相差数百米且无地形深度产物，场景等价既未证明也未否定。**除最后一行外，全部实机场次早于本轮提交，不能为当前产物给出任何门的结论。** 详见 `../P3-IMPLEMENTATION-HANDOVER.md` §4、§7。

**整改反例（非实机场次，保留为工具敏感性证据）**

| 副本 | 门2 | 关键 detail |
|---|---|---|
| `E:\Agent\codex\2026-10-14\p3\work\review-20261016\tamper-missing-reassigned-exit` | **fail** | `assigned=3 stopped=2 unaccounted=1`（删重分配 Guard 的本次 `EXIT_STOP_DONE`） |
| `...\tamper-independent-order-contradiction` | **fail** | `still_on_old_order=008c0c1e@HorseArcher@134.746`（独立订单与停止声明冲突） |
| 第二轮新增 `tamper-missing-independent-orders` | **unverified** | `independent_stop_evidence=incomplete:3`（删光独立订单字段后不再算 pass，旧版会误判 pass） |
| 第二轮新增 `counterexample-before-exit-within-budget` | **pass** | 133.900 的预算内中间读数不再被判违约（旧版会误判 fail） |
| 第三轮定向探针 `successor_write_before_same_coordinate_reading` | **不矛盾** | 后继在 137.0 对同一点写下普通令，137.5 的 `(50,0)` 读数归后继（第三轮修掉的误判；指向第二轮检查器复跑为 `contradiction=True`） |
| 第三轮定向探针 `no_successor_write_same_coordinate_reading` | **矛盾** | 无任何后继写令时同坐标读数仍是旧令未停 |
| 第三轮定向探针 `reading_before_successor_write` | **矛盾** | 后继写令（140.0）晚于读数（137.5），不得提前归属 |
| 第三轮定向探针 `successor_exit_write_does_not_reattribute` | **矛盾** | 后继只有 `-exit` 停止任务写下同一点，不算续令 |
| 第四轮新增副本 `new_order_different_target_old_readback` | **fail** | 后继改为写向别处后，其后 57 条读数仍 Move 到旧点；第三轮检查器误报 **pass** |
| 第四轮新增副本 `new_order_missing_independent_orders` | **unverified** | 删除某对象退出后的全部订单/目标读数即 `incomplete:1`；第三轮检查器误报 **pass** |
| 第四轮定向探针 `successor_target_differs_but_independent_old_move` | **矛盾** | 后继目标 `(90,30)` 与快照读回 `(50,0)` 不符 → 旧令仍在执行；HEAD 版误报不矛盾 |
| 第四轮定向探针 `successor_hold_but_independent_old_move` | **矛盾** | 后继只写 `hold`（无目标），不能解释指向旧点的 Move 读数；HEAD 版误报不矛盾 |
| 第四轮定向探针 `successor_with_only_unreadable_independent_fact` | **不矛盾（证据不足）** | 唯一读数无可读订单/目标 → 不判违约，由 `independent_stop_evidence=incomplete` 判 unverified |

**进程归属的隔离反例（同一套十模式模拟，见 `../P3-IMPLEMENTATION-HANDOVER.md` §3.4）**

| 模式 | own_pid_stopped | foreign_pid_stopped | 归属依据 |
|---|---|---|---|
| `ok` | True | False | `launch_context_pid_matched_new_process`（本场 GABS `runtime.json` / `launch.log` 指名的 workload PID） |
| `startthrow`（含 `-other-during-launch`） | True | False | 本场启动上下文指名的 777001 仍在新进程差集中 |
| `foreignonly` | False | **False** | 本场启动失败无进程、只一个他方 PID → `new_process_without_launch_result`，登记 unresolved 保留 |
| **`foreignidentity`（第五轮新增，复核反例）** | False | **False** | 他方 777999 照常把自己的身份文件写进共享的 `Documents\CommandBrainATH`；**身份文件不是归属**，777999 登记 unresolved 保留。**第四轮 runner 在同一模式上 `foreign_pid_stopped=True`** |
| **`startedthenexited`** | False | **False** | 本场进程已退出、差集只剩他方 → `launched_process_exited_other_process_present`；**第三轮 runner 在同一模式上 `foreign_pid_stopped=True`** |
| `fighttodeathoff` | True | False | 死战不退未开启 → `pre_mission` 即阻断，不进入 mission |

**十三例都必须按上表取值**（其中六例是场次副本：前四行 + `new_order_*` 两行；另七例是定向探针场景）。原始场 `p3-P-20261015-221833` 未被清洗改写。门2 现为三值判定：`ChecksOk` 失败 → fail；缺少独立可解释读数 → unverified；全满足 → pass。探针位于 `../../_gate2probe/run-probe.ps1`（切出检查器同一段生产代码，只替换写入记录与独立样本，不读写任何场次目录）。进程归属的隔离反例见下表。

**反制目标定位的离线复算（第七轮，非实机场次）**

| 检查 | 结果 |
|---|---|
| 本场冻结键解析 | `1/0`（取最后一条 `ACTION_FEEDBACK_FROZEN ... main=<t>/<s>`） |
| 按键命中 | `team=1 slot=0 identity=00bec9c9@Formation class=Cavalry units=52 @ (911.057,978.689)` |
| 与第六轮兜底（我方人数最多）之差 | `030dd50e@Formation` Ranged 175 @ (906.361,959.823)，相距 **19.442 m** |
| 键指向别槽位 `1/1` | 返回 `Ranged 030dd50e@Formation`（按槽位取，不按人数取） |
| 不存在槽位 `1/9` / 队伍 `9/0` / 空键 / 畸形 `x` | 一律不返回对象，不猜 |
| 日志无冻结行 / 两条冻结行 | 空串 / 取后者 |

