# P0 第一批 — 实机场次索引

Codex 2026-10-11 已按限定范围验收第一批最小命令通路：run 7 的单槽位同步修复及受影响通路已独立核对，原有阻挡关闭。当前决定性场次为 **run 7 `20261011-002337`**，当前验收见 `../P0A-CODEX-ACCEPTANCE.md`。未验证边界保留；不以单场 RESULT: OK 代替 P0 整体验收。

本文件是唯一提交进仓库的场次索引。**完整场次材料（含 2.0 日志、引擎日志、控制面 JSON、CBATH 独立采样）外置归档，不入库**；归档摘要不写入归档内部（自指即失效）。

## 外置归档

| 项 | 值 |
|---|---|
| 归档（当前，覆盖 17 场，P0 Exit Gate 3） | `E:\MBmod\CombatBrain2-evidence\20261011-gate3-sessions.zip` |
| 大小 / 条目 | **8829130 B / 610** |
| SHA256 | `35F99ECB26213FF23AA1719E055310CA8D5B2437C5579B19BF6FEDADA57F4A73` |
| sidecar | `E:\MBmod\CombatBrain2-evidence\20261011-gate3-sessions.zip.sha256` |
| 覆盖面 | `Program\_verify\sessions\` 全部 **17** 个场次目录（含 Gate 3 决定性场 `20261011-133859`）+ `_lastrun\*.txt` 控制台副本 |
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

- 前五场为过程失败场，**保留原始结果，不构成验收依据**。
- `20261010-164624` 为中间场：命令链已完整，但撤销原因记为 `native_rewrote_after_us`，且尚无 `SNAPSHOT_HEALTH`。
- run 1–3 的现有记录均未显示进入 Mission。run 1 有渲染资源不足证据，run 3 时间对应的 pid 44272 日志有 Granite OOM；run 2 只有启动访问异常，原因未定。不能统一归因或排除全部代码因素，见 `P0A-HANDOVER.md` §8 与 `P0A-CODEX-REREVIEW.md` §4。
- run 4 的真实原生获选与两次 Stop 落地已确认；主线程执行与原方法完成证据在 run 5 补齐，见 `P0A-HANDOVER.md` §3、§4.1。
- run 2/3 目录为空，原 ZIP 不含其控制台与 pid 44272 引擎日志；补存原字节到 `E:\MBmod\CombatBrain2-evidence\20261010-routec-codex-review\`，逐文件摘要见复验报告 §4。原 ZIP 未重写。
- `20261010-211555` 的 bytes（11711698）异常大，是因为该场保存了崩溃转储相关文件；其内容与判据无关，仍一并归档。

- `20261011-132100` / `20261011-132600` 为 Gate 3 的两个失败场：**失败原因是本机 Steam 当时未完成登录**（2.0 日志从未创建、无 crash dir、两者现象相同），与代码无关；不得与 run 2/3 的启动访问异常混为一谈。登录完成后同一命令（`run-p0.ps1 -OutDir ...\20261011-133859 -CombatHoldSec 120`）即跑通。
- **当前决定性场次为 Gate 3 的 `20261011-133859`**；run 7 `20261011-002337` 仍是 P0a 第一批的决定性场次。二者解决的是不同批次的问题：run 7 解决槽位同步，`20261011-133859` 解决控制权安全边沿（资格变化）。
- Gate 3 三项 Deviation 已在本单报告 §5 如实登记：`HANDOFF_CLEARED had_pending=0`（时点安排使然）、`SNAPSHOT_FORMATION` 未采到 `ai_ctrl=0`（窗口仅 3.994 s）、`HANDOFF_DROPPED reason=intent_closed` 本场 0 条尚无实机证据。

## 逐文件摘要（五个有效场次）

- **Gate 3 `20261011-133859`（当前决定性）**：13 个文件 / 95579 B，逐个 SHA256 见 `../P0-EXIT-GATE3-RUN-REPORT.md` §0/§4（本出场次已在归档 `20261011-gate3-sessions.zip` 内逐文件保存）。
- **run 7 `20261011-002337`（P0a 决定性）**：完整表（13 个文件 / 136097 B 的逐个 SHA256）见 `../P0A-HANDOVER.md` §7 场次 2。
- run 5 `20261010-225539`：完整表（13 个文件 / 136897 B 的逐个 SHA256）见 `../P0A-HANDOVER.md` §7 场次 3。
- run 4 `20261010-213621`：完整表（13 个文件 / 124667 B 的逐个 SHA256）见 `../P0A-HANDOVER.md` §7 场次 4。
- run 6 `20261010-171526`：完整表（19 个文件 / 577266 B 的逐个 SHA256）见 `../P0A-HANDOVER.md` §7 场次 1。
- 四者数据同源（均从磁盘现算），如有出入以归档内实际文件为准。

## 未提交内容说明

- `sessions/` 下全部场次材料：外置归档，仓库只留本索引。
- `gabs-config/bannerlord148-cb2/`：GABS 运行时状态（`bridge.json` / `runtime.json` / `history.json` / `launch.log`），非交付物。配置本体 `gabs-config/config.json` 已提交。
- `Program/_scratch/`：构建期反射探针与反编译产物（临时工具，非交付物），磁盘保留、不入库。
- `_verify/_lastrun*.txt`：实机脚本控制台输出副本，非交付物。
