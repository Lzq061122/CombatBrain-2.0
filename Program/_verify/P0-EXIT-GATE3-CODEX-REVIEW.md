# P0 Exit Gate 3 独立复核（2026-10-11）

受审实现 `95f49c3`、报告 `274a9bd`；场次 `sessions/20261011-133859/`，pid 35032，build `b20261011-132044-a06f2e05`。本报告接续 `P0-STAGE-EXIT-CHECK.md`，不撤回第一批最小通路的已验成果。

**结论：场景 A 的生产否决、场景 B 的生产取消/撤权、资格恢复后旧意图不复活均有实机证据，接受这些核心成果。Gate 3 和 P0 阶段暂不登记整体通过：还缺写定的独立失资格观测，以及足以支持玩家接管“不适用”的适用域依据。当前需补证，不是重新修整条命令通路。**

本次只读源码、原始场次、部署身份、归档与当前游戏程序集的静态内容；未构建、重跑测试、部署或启动游戏。65 个 Core 测试结果作为开发执行证据保留，不作为 Host 的实机证明。另已保存并移除测试启用文件，恢复默认关闭态，见 §5。

## 1. 直接确认的实际结果

- 场景 A：L85–87 原生实际实例获选并投递，L89 同对象 `024fb8dd@Infantry` 经公开原生 API 由 ai_ctrl=1 变为 0；L91 消费待转交命令，L92 在生产执行门被 `not_ai_controlled` 拒绝，本意图放行写入为 0。L93 因 AiControlLost 取消，L95 实读权重 8→0。没有提前热关闭或假 MarkSelected。
- 场景 B：L165 在登记的线程 30 实际写 Move→StandYourGround，记录原方法入口/正常返回/实际后值组合证据；L167 资格变化，L169 在下一检查点因 AiControlLost 取消，L171 权重实读 8→0。不是到期、显式关闭或仅靠 already_written。
- L108/L184 分别于原截止前恢复资格，恢复后旧意图不开放。逐意图扫描全部 **448 行**，取消后无同意图新 WRITE_EXECUTED、注册、获选或 HANDOFF_EXECUTED；L448 Mission 清理。B 的观察延续到 MT 118.987，超过旧截止 40.549+2 s。
- 实读时间差：A 为 19.542−15.537=**4.005 s**；B 为 25.050−21.044=**4.006 s**，满足派单 ≥2 s。开发消息的“648 行”与报告的“3.994 s”均不是本场复算结果。
- 部署文件 53248 B，SHA256 `4A7E9C1D05EFC0FE666E022789B11A99A149AF73491A579DFA83A9BEDCA918E8`，与 L1 一致。场次日志 SHA256 `699670D3708D8E5CCB8A76DDD5F3632C9B9ADEFD9780D1961744583C856C2E2B`。
- 原 ZIP SHA256 `35F99ECB26213FF23AA1719E055310CA8D5B2437C5579B19BF6FEDADA57F4A73` 核对相符，包含本场数据及 `_lastrun/_lastrun10.txt`。控制台 RESULT: OK、无残留进程、无新 crash 与 Mission 清理保留为原场事实。

## 2. 三条偏差逐项裁定

| 偏差 | 裁定 |
|---|---|
| had_pending=0 | **接受场景 A 核心判据成立。** 触发时确有真实 pending，命令先被 TakePending 取出并由执行门拒绝，随后下一 tick 才取消，因此取消时为空合理。取消清除非空槽位的实机分支未因此得到验证，不能把 had_pending=0 写成清掉了一条非空命令 |
| 没有独立 ai_ctrl=0 样本 | **不接受用 probe 自读替代该独立判据的 PASS。** 核心资格变化有 probe 实读、执行门实时读取与生产采样取消交叉支撑，但这些属于 CB2 内部路径。预先写定的“至少两次 1 Hz 独立观测 ai_ctrl=0”以及派单的 CBATH 独立对照尚未交付，不能仅列 Deviation 后自动视为满足 |
| HANDOFF_DROPPED intent_closed 为 0 | **继续未验证，不单独增加为阶段阻挡。** 这不是场景 A/B 成立所必需的另一种清槽时序，也不要求为了该分支再造一场 |

独立观测缺口的具体原因不是“4 s 窗口太短”：CBATH `SamplingMissionBehavior` 已有每 1 s Capture 的独立时间线，并在 OnEndMission 后写入 RunRecord.Complete。当前 `run-p0.ps1` 在第 263 行先取 get_last_run，第 274 行才 end_mission，第 285 行 games_stop；归档中的本场 last-run.json 真实内容是 **status=no_run**。它没有保存结束后才可取得的完整时间线，本次游戏已停止，无法把其内存记录补当原场现存证物。

最小补证是：使用现有 CBATH，在 end_mission 及本场 RunRecord 完成之后、games_stop 之前取 get_last_run，确认 status=ok、场次/时间匹配并保存完整 samples；或在两个失资格窗口直接取现有独立只读工具的数据。不要修改冻结的 CBATH 核心，不延长意图截止，也不建立新采样平台。

## 3. 玩家接管“不适用”依据尚不成立

运行报告 §2.1 既称不可达，又把“持有期间真实接管能否合法发生”列为未取证。这不能支持不适用结论。初始排除玩家队/PlayerOwner 不等于该对象以后永不成为玩家队；`SetControlledByAI(false)` 也不等于设置 PlayerOwner。本场失资格后 PlayerOwner 仍为 null，原始日志本身就说明它们是不同状态。

本次从**当前游戏 DLL**独立静态查看：`Mission.JoinEnemyTeam()` 是公开原生方法，在敌方 Leader 存在时改变控制 Agent 并设置 PlayerTeam 为另一队（静态源码 L4659–4684）。这不是本次已执行的场景，不证明所有 UI/F6 路径均可达；但它表明队伍的玩家身份可通过原生控制路径变化，报告未排除这一前提。仅以 selector 的初始过滤不能证明永久不可达。

下一步只补这个适用性判断：说明合法原生路径能否使已绑定对象成为玩家队/玩家拥有，或提供当前场景确实排除该路径的完整依据。若可达，复用最小测试适配验证同对象资格变化后的生产让位；可从 JoinEnemyTeam 这条真实原生控制链查起，记录实际前后状态，不能伪造 PlayerOwner、放宽生产玩家过滤或把接口调用冒充 F1/F6 按键。若需补场，把该证据与独立采样补取合并安排，已成立的 AI-loss 场景不重新开发。

## 4. 未验证项和勘误边界

真实玩家事件链、阵亡导致的资格变化、目标失效、选择超时、真实回退、陈旧拒绝、下一 Mission、其他命令/兵种/并发消费范围等仍按实际覆盖登记。当前无需性能基线、完整类型矩阵、元数据 byte-diff 或 publickeytoken 专项。API 触发资格变化的事实不能顺带把这些项登记通过。

报告误将 run 7 的身份写成 run 6 的 37376 B/44845964：正确 run 7 为 47104 B、`27F8CBFA29AC1F13A848A61CDC2F761C7751517558CB294AD2DAA30113EF2416`。这是文档身份勘误，不影响本场 4A7E9C1D 部署/加载绑定，也不重启已收口的可复现性调查。

## 5. 测试适配已恢复默认关闭

本次读取启用文件并先保存原字节到 `E:\MBmod\CombatBrain2-evidence\20261011-gate3-codex-review\eligibility-probe-enabled-23EB8A27.txt`，SHA256 `23EB8A27821EFB08D50813D387D3A0A630231ADA669D314CAF22B397F0DCE5F3`；核对源/副本摘要一致后，移除 `C:\Users\Administrator\Documents\CombatBrain2\eligibility-probe.txt`，复读确认不存在。操作时无存活 Bannerlord；LoadOnce 在下次游戏启动见文件不存在即保留空模式并返回，恢复默认无资格修改的状态。没有改生产 DLL。

补充静态证物 `Mission.current.cs`（当前引擎反编译，只作阅读辅助）SHA256 `76739AB37D045A9D323127A331F7B5BFD931B56C4D7230AD5EA3B96B35393246`；last-run-no-run.json SHA256 `64727FF533716C1984BB28833E00018414D03C394BBF21E79B00B7E18A5DBAA3`，均在上述补充目录。原始 ZIP 未重写。

本次仅提交独立复核及报告事实勘误，未修改源码、测试、部署或原场数据，不 push。P0 暂不退出；复核下一次只看独立旁证与玩家适用域的这两处补证，不重开整条 P0 或 TCL 验证工程。
