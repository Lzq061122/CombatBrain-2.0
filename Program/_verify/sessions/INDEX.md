# P0 第一批 — 实机场次索引

本文件是唯一提交进仓库的场次索引。**完整场次材料（含 2.0 日志、引擎日志、控制面 JSON、CBATH 独立采样）外置归档，不入库**；归档摘要不写入归档内部（自指即失效）。

## 外置归档

| 项 | 值 |
|---|---|
| 归档（当前，覆盖 13 场） | `E:\MBmod\CombatBrain2-evidence\20261010-p0a-sessions-run9.zip` |
| 大小 / 条目 | **8774320 B / 594** |
| SHA256 | `BC4FA254822B4CA468E889FDF83D1AA36EA86BE3184987C02D2AC5F791DC830A` |
| sidecar | `E:\MBmod\CombatBrain2-evidence\20261010-p0a-sessions-run9.zip.sha256` |
| 覆盖面 | `Program\_verify\sessions\` 全部 **13** 个场次目录（含隐藏文件），源 594 files / 12834616 B |
| 归档（上批，已被上者取代） | `E:\MBmod\CombatBrain2-evidence\20261010-p0a-sessions-run7.zip`，8768831 B / 581，`E825FF34A48F024BB5FA3436BA13F4A44430A60C295635D48197F5E10C27A2FB`；仅覆盖 11 场 |
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
| **`20261010-225539`（run 5）** | **13 / 136897** | **754** | **OK** | **是** | **当前决定性场次**：主线程转交 + 原方法回执整改后唯一成功场。`MAIN_THREAD_MARKED tid=33` → `ACTIVE_BEHAVIOR_CHANGED ours=1 tid=43` → `HANDOFF_DEFERRED callback_tid=43 main_tid=33` → `HANDOFF_EXECUTED exec_tid=33 is_main_thread=1` → `WRITE_EXECUTED … original_body_entered=1 original_body_tid=33`；53 条转交全部成功，`HANDOFF_DIRECT` 0 条 |

- 前五场为过程失败场，**保留原始结果，不构成验收依据**。
- `20261010-164624` 为中间场：命令链已完整，但撤销原因记为 `native_rewrote_after_us`，且尚无 `SNAPSHOT_HEALTH`。
- run 1–3 的现有记录均未显示进入 Mission。run 1 有渲染资源不足证据，run 3 时间对应的 pid 44272 日志有 Granite OOM；run 2 只有启动访问异常，原因未定。不能统一归因或排除全部代码因素，见 `P0A-HANDOVER.md` §8 与 `P0A-CODEX-REREVIEW.md` §4。
- run 4 的真实原生获选与两次 Stop 落地已确认；主线程执行与原方法完成证据在 run 5 补齐，见 `P0A-HANDOVER.md` §3、§4.1。
- run 2/3 目录为空，原 ZIP 不含其控制台与 pid 44272 引擎日志；补存原字节到 `E:\MBmod\CombatBrain2-evidence\20261010-routec-codex-review\`，逐文件摘要见复验报告 §4。原 ZIP 未重写。
- `20261010-211555` 的 bytes（11711698）异常大，是因为该场保存了崩溃转储相关文件；其内容与判据无关，仍一并归档。

## 逐文件摘要（三个有效场次）

- **run 5 `20261010-225539`（当前决定性）**：完整表（13 个文件 / 136897 B 的逐个 SHA256）见 `../P0A-HANDOVER.md` §7 场次 2。
- run 4 `20261010-213621`：完整表（13 个文件 / 124667 B 的逐个 SHA256）见 `../P0A-HANDOVER.md` §7 场次 3。
- run 6 `20261010-171526`：完整表（19 个文件 / 577266 B 的逐个 SHA256）见 `../P0A-HANDOVER.md` §7 场次 4。
- 三者数据同源（均从磁盘现算），如有出入以归档内实际文件为准。

## 未提交内容说明

- `sessions/` 下全部场次材料：外置归档，仓库只留本索引。
- `gabs-config/bannerlord148-cb2/`：GABS 运行时状态（`bridge.json` / `runtime.json` / `history.json` / `launch.log`），非交付物。配置本体 `gabs-config/config.json` 已提交。
- `Program/_scratch/`：构建期反射探针与反编译产物（临时工具，非交付物），磁盘保留、不入库。
- `_verify/_lastrun*.txt`：实机脚本控制台输出副本，非交付物。
