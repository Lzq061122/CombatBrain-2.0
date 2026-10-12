## P1 第一批 — 实机场次索引

**Codex 当前裁定（2026-10-12，第二轮）：P1 尚未通过。** `616b903` 的发令顺序与继续条件实现整改通过；新两场 10 条写令已独立对齐，正常基线和实际目标读回成立。仍须关闭目标读回缺失误判生效、已获选退出任务被 plan_closed 误挡两处原判据缺陷，详见 `../P1-CODEX-REVERIFY-2.md`。原复验报告保留为历史；下方 RESULT: OK 与实现方自检描述不替代阶段判定。

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
- `gabs-config/bannerlord148-cb2/`：GABS 运行时状态（`bridge.json` / `runtime.json` / `history.json` / `launch.log`），非交付物。配置本体 `gabs-config/config.json` 已提交。
- `Program/_scratch/`：构建期反射探针与反编译产物（临时工具，非交付物），磁盘保留、不入库。
- `_verify/_lastrun*.txt`：实机脚本控制台输出副本，非交付物。
