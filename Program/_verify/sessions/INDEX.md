# P0 第一批 — 实机场次索引

本文件是唯一提交进仓库的场次索引。**完整场次材料（含 2.0 日志、引擎日志、控制面 JSON、CBATH 独立采样）外置归档，不入库**；归档摘要不写入归档内部（自指即失效）。

## 外置归档

| 项 | 值 |
|---|---|
| 归档（当前） | `E:\MBmod\CombatBrain2-evidence\20261010-p0a-sessions-run7.zip` |
| 大小 / 条目 | 8768831 B / 581 |
| SHA256 | `E825FF34A48F024BB5FA3436BA13F4A44430A60C295635D48197F5E10C27A2FB` |
| sidecar | `E:\MBmod\CombatBrain2-evidence\20261010-p0a-sessions-run7.zip.sha256` |
| 覆盖面 | `Program\_verify\sessions\` 全部 11 个场次目录（含隐藏文件），源清单 581 files / 12694917 B |
| 归档（第一批，已被上者取代） | `E:\MBmod\CombatBrain2-evidence\20261010-p0a-sessions.zip`，274030 B / 531，`B144E23AFCE18B7C45F21B799884ECEEF9D9CD274808A9884E6264CCDDB015FE`；**仅覆盖前 7 场（855706 B），不含 C 路线整改后的 4 场** |

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
| **`20261010-213621`（run 4）** | **13 / 124667** | **638** | **OK** | **是** | **C 路线整改后的决定性场次**；命令链走原生行为选择链（`BEHAVIOR_REGISTERED → ACTIVE_BEHAVIOR_CHANGED ours=1 → WRITE_EXECUTED route=native_behavior_apply`） |

- 前五场为过程失败场，**保留原始结果，不构成验收依据**。
- `20261010-164624` 为中间场：命令链已完整，但撤销原因记为 `native_rewrote_after_us`，且尚无 `SNAPSHOT_HEALTH`。
- run 1–3 的现有记录均未显示进入 Mission。run 1 有渲染资源不足证据，run 3 时间对应的 pid 44272 日志有 Granite OOM；run 2 只有启动访问异常，原因未定。不能统一归因或排除全部代码因素，见 `P0A-HANDOVER.md` §8 与 `P0A-CODEX-REREVIEW.md` §4。
- run 4 的真实原生获选与两次 Stop 落地已确认；主线程执行与原方法完成证据仍待补，不登记第一批整体通过，见 `P0A-CODEX-REREVIEW.md` §2。
- run 2/3 目录为空，原 ZIP 不含其控制台与 pid 44272 引擎日志；补存原字节到 `E:\MBmod\CombatBrain2-evidence\20261010-routec-codex-review\`，逐文件摘要见复验报告 §4。原 ZIP 未重写。
- `20261010-211555` 的 bytes（11711698）异常大，是因为该场保存了崩溃转储相关文件；其内容与判据无关，仍一并归档。

## 逐文件摘要（两个有效场次）

- run 6 `20261010-171526`：完整表（19 个文件 / 577266 B 的逐个 SHA256）见 `../P0A-HANDOVER.md` §7。
- run 4 `20261010-213621`：完整表（13 个文件 / 124667 B 的逐个 SHA256）见 `../P0A-HANDOVER.md` §10。
- 两者数据同源（均从磁盘现算），如有出入以归档内实际文件为准。

## 未提交内容说明

- `sessions/` 下全部场次材料：外置归档，仓库只留本索引。
- `gabs-config/bannerlord148-cb2/`：GABS 运行时状态（`bridge.json` / `runtime.json` / `history.json` / `launch.log`），非交付物。配置本体 `gabs-config/config.json` 已提交。
- `Program/_scratch/`：构建期反射探针与反编译产物（临时工具，非交付物），磁盘保留、不入库。
- `_verify/_lastrun*.txt`：实机脚本控制台输出副本，非交付物。
