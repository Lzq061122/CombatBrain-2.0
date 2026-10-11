# P0 阶段退出独立验收（2026-10-11）

受审 HEAD：`51bde01`；本轮补证实现 `d9e6b9c`、交接 `51bde01`。验收人：Codex。阶段门槛沿用 `../../charter/CombatBrain-2.0-Development-Roadmap-v0.1.md` §3，不修改判据。

**结论：Gate 3 两项补证通过；P0 在当前 Movement／非玩家 AI 步兵编队控制范围内满足四项退出条件，正式退出，可以进入 P1 最小指挥闭环开发。** 这是工程基座与安全命令通路的阶段结论，P1 的自动规划和反馈能力尚未实现、尚未验收。

本轮直接读取补证源码差异、两场原始 CB2 日志、CBATH 完整时间线、控制台结果、当前部署身份与外置归档。没有重新构建、跑测试、启动游戏或修改实现。开发提交记录中的 65/65 测试作为开发执行证据保留，本轮 Host 判定依据为实机材料。旧审查报告保留为各次审核时的历史结论。

## 1. 本轮两项补证

### 1.1 独立失资格观测：通过

场次 `sessions/20261011-164000/`，pid 16736，CBATH `run_id=run-20261011-081849-1`，`status=ok`、91 次采样、间隔 1 s，Mission 结束时 MT 90.619。独立数据取自结束 Mission 后完成的 RunRecord，并在停止游戏前保存；原先提前取到的 `last-run.json status=no_run` 不用作本轮旁证。

同一 `team_id=1 / formation_index=0`：

| 场景 | CBATH 独立实读 | 同场生产反应 |
|---|---|---|
| A：获选后、写入前失资格 | t=16.097/17.098/18.103/19.104，`is_ai_controlled=false`、order=Move；t=15.096/20.105 为 true | CB2 L68 真实获选，L71 资格变化，L74 `not_ai_controlled` 否决且本意图写入尝试 0；L75 AiControlLost 取消，L77 权重 8→0 |
| B：写入后失资格 | t=21.110/22.115/23.119/24.122，`is_ai_controlled=false`、order=StandYourGround；t=20.105/25.129/26.135 为 true | L149 主线程实际 Move→StandYourGround，带原方法入口/正常返回/后值组合证据；L152 失资格，L154 AiControlLost 取消，L156 权重 8→0 |

两窗口各 4 条独立观测，满足预先写定的至少 2 次要求。L90/L168 分别于 MT 19.539/25.047 恢复资格，早于原截止 35.447/40.550；`same_intent_open=0`。逐意图扫描至 L364 Mission 清理，取消后同意图新写入、候选提交和真实获选均为 0；观察时长超过旧截止+2 s。

### 1.2 玩家队身份边沿：通过当前适用范围

场次 `sessions/20261011-162100/`，pid 44704，mode c。源码通过公开原生 `Mission.JoinEnemyTeam()` 触发真实 PlayerTeam 变化，未伪造 PlayerOwner，也未放宽生产过滤。

同一已绑定对象 `02c17ea5@Infantry`、key=1/0：L66 真实获选，L69 于 MT 15.539 将 player_team 从 0 切为 1，bound_team 仍为 1，`target_now_player_team=1`；L72 生产执行门以 `player_owned` 拒绝，本意图写入尝试 0；L73 `PlayerEligibilityLost` 取消；L75 实读行为权重 8→0。

CBATH 独立时间线 `run_id=run-20261011-081451-1`、91 条样本：team 1 在 t=16.110/17.113/18.120/19.120 为玩家队，前后 t=15.107/20.125 为非玩家队。mode c 全程 AI 标志仍为 true，证明本场触发的是玩家队过滤，不能标成 AI 关闭或玩家 owner 赋值分支。

L88 于 MT 19.546 恢复原 PlayerTeam，早于 intent-1 原截止 35.478，旧意图保持关闭。恢复由测试适配将 `Mission.PlayerTeam` 设置为保存的原队，仅撤回其自身环境修改；不称为完整反向玩家接管链。扫描至 L395 Mission 清理，同旧意图无新写入、候选提交或真实获选。intent-2 的合法新写入另有新触发，不计为旧意图复活。

原“玩家接管不可达”的推断已撤回。真实 UI/F1/F6 按键链仍未验证，本次只接受标明来源的原生测试接口玩家队身份边沿；这满足上一轮要求补取的当前域证据，不扩大为全部玩家控制链安全证明。

## 2. 四项阶段退出条件

| Exit Gate | 当前判定 | 依据与适用范围 |
|---|---|---|
| 1. 同场身份→双方状态→命令→实际效果，独立记录分开保存 | 通过当前最小通路 | 继承 `P0A-CODEX-ACCEPTANCE.md` 和 `P0-STAGE-EXIT-CHECK.md` 中 run 7 已验结果；本轮新场加载身份与当前部署相符，CB2 与 CBATH 原始材料分开保存 |
| 2. 命令实际作用，后续覆盖可辨认 | 通过 Movement Stop | run 7 已验实际 Move→StandYourGround 及后续变化；本轮 B 再次实际生效，失资格后独立观测仍为 Stop，恢复时改为 Move。来源不足的后续覆盖继续标为来源未判定 |
| 3. 适用控制权边沿不继续抢令 | 通过当前控制范围 | 原有到期、显式关闭与 Mission 清理证据保留；本轮独立补齐 AI 失资格及玩家队身份变化后的否决/取消/撤权，并确认资格恢复不会复活旧意图。FD-PoC P2 在本范围内有结论；保持 AI 的当前原生行为路线不使用 Full/租约与 Facing/Arrangement，FD-PoC P1 不适用，不能登记为已验证；原生行为正向生效 FD-PoC P11 已验 |
| 4. 刷新、敌我归属、人数独立核对，未知字段标记 | 通过已交付状态范围 | 继承 run 7 双方编队、人数、刷新与重复键核对；本轮资格/玩家队变化新增独立对照。age 恒 0 不是陈旧拒绝实机通过；预备队等未实现语义不作为已知字段提供 |

Gate 1/2/4 本轮不重开全部验收，沿用已验实现与原始证据；`d9e6b9c` 未改生产决策/执行门分支，新增项为默认关闭的测试适配 mode c、结束后旁证取数和构建身份。

## 3. 身份、归档与默认关闭态

两场共用 build `b20261011-161246-db46608a`，MVID `eea0ebe0-31e3-41cf-a871-9d4646299f8d`。两场 L1 均未加载旧 CombatBrain；当前部署 `CombatBrain2.dll` 55808 B，SHA256 `C0D59AE7124DA67DB8FD3B39CBB4B6FA4B4779694780F5D748A9074268946EE4`，与场次加载记录相符。

| 证物 | 实算结果 |
|---|---|
| `sessions/20261011-164000/cbath-timeline.json` | 750858 B；SHA256 `A352C2B9FD1AD5F26FBE892A8CB97609A750A4C79CCF3FA5D53CD7AEFFB5DA19` |
| `sessions/20261011-162100/cbath-timeline.json` | 749046 B；SHA256 `FF7EEE788FFE7E374BF41A119696F2B40259CA79A19A24878F78D0EBB3AADC88` |
| `E:\MBmod\CombatBrain2-evidence\20261011-gate3-supplement-sessions.zip` | 8925131 B；666 条目；SHA256 `C86C44A5A7B323A8D9D9AE4733BC24EB22C79FFB3514A8D5A0E1A254799183C4`；核对包含两场日志/时间线及 `_lastrun11/12` 控制台副本 |

两场控制台均 RESULT: OK，记录实际 end_mission、games_stop、无残留进程和无新 crash；本轮当前无存活 Bannerlord。`C:\Users\Administrator\Documents\CombatBrain2\eligibility-probe.txt` 复读不存在，测试适配默认关闭。旧归档不重写，运行期未跟踪项保持原样。

run 7 身份更正沿用本轮受审文档：47104 B / `27F8CBFA29AC1F13A848A61CDC2F761C7751517558CB294AD2DAA30113EF2416`。这是历史身份勘误，不重启已结束的构建摘要可复现性调查。

## 4. 保留限制与下一阶段

`HANDOFF_DROPPED reason=intent_closed`、取消时清除非空槽位、真实 UI 按键接管、PlayerOwner 赋值、JoinEnemyTeam 后 AddTeamAI/SetPlayerRole 重算组合、目标失效/选择超时、真实 `REVOKE_EXECUTED` 回退、陈旧快照拒绝、下一 Mission 无残留、其他命令域/兵种/消费者范围，仍按实际覆盖登记为未验证。撤销行为权重已实测，不等于把之前不可恢复的 Move 命令恢复成功。

这些限制不追加为当前 P0 补场要求。P0 收口后停止整改、性能插桩与基线扩展；P1 若引入依赖未验证命令域或新控制方式的方案，在其派单中明确所需前置，不沿用本次结论扩大覆盖。

下一项开发应落到 P1 的最小整体计划：不同关键条件产生不同目标/任务，多个 Formation 服务同一目标，命令可追溯到快照/Plan/任务，观察生效与失效，并从首次自动指挥起落实决策节奏、计划稳定时间及非零指挥延迟。具体方案和参数在 P1 实现前另写判据；本次不实现 P1，不更改路线图，不 push。
