# P3 开发交回独立复核（2026-10-16）

**结论：交回材料已核收；本次不通过独立验收，P3 不具备阶段退出条件。**

已独立确认 276/276 测试通过，CB2 Release 与 CBATH Debug 的 net472 工程均重建为 0 警告、0 错误。整改2、3在本次限定的源码接入与离线反例范围内可以确认；整改1、4、5仍有具体缺口。门1失败、门3未验证；门2候选场有真实及时退出和合法接续的有效证据，但检查器存在新的可复现误报，不能用它的 PASS 代替完整验收。

本次仅只读受审工作树、复制审查快照，在隔离目录构建、复算和运行替身。未改产品源码、原始场次、共享配置、部署或游戏进程；未进入 P4。没有将他方尚未交回资源当作本组可用窗口。

## 一、来源与独立验证

| 项目 | 独立读数 |
|---|---|
| CB2 主线 HEAD | `46aca61ca63fb0d4ef61715588065b398e34ff58`，交回文档提交 |
| CB2 实现提交 | `ced0a1895929524d8d3f5a628e112370422f265c` |
| CBATH HEAD | `5a2275de434e953802d8de8498b3f09ca1a4fbb6`，工作树干净 |
| 交回文件 SHA256 | `1DF9B3B9A7AA66DD60CA7EA95ACD957D11258ECE87309DE72869CCD3A5206A70` |
| CB2 已跟踪 dirty | `AGENTS.md`、`Program/src/Host/BuildIdentity.g.cs`、`prompts/registry.json`；保留原样 |
| 相对前次审查清单 | CB2：61项中51相同、10变化；CBATH：16项中15相同、仅自动身份变化 |
| CB2 隔离重建 | `dotnet build CombatBrain2.csproj -c Release --no-restore -t:Rebuild`，0警告0错误 |
| CBATH 隔离重建 | `dotnet build CombatBrainAutonomousTestHarness.csproj -c Debug --no-restore -t:Rebuild`，0警告0错误 |
| 测试 | 先在隔离副本 Rebuild，再 `dotnet test -c Release --no-build --no-restore`，276通过、0失败、0跳过 |
| 测试工程构建警告 | 原有两条 `ProposalArbiterTests.cs` 的 xUnit2013；不与两个宿主工程的零警告混淆 |
| 四个验证脚本 | Windows PowerShell 5.1语法解析均0错误；独占入口 `-WhatIf` 返回0，未执行部署或游戏 |

隔离审查目录：`E:\Agent\codex\2026-10-14\p3\work\review-20261016`。复用了已恢复的依赖资产，避免修改生产工作树的 bin/obj 或构建身份。源码差异和逐文件摘要见证据包 `source-manifest.json`；四个原始场次的审查副本摘要见 `session-manifest.json`。

上述 SHA256 是交回文件摘要，不是 Git HEAD 的另一种哈希。当前干净的实现源码与历史运行源码是否一致，不能只靠当前 git diff、文件修改时间及 DLL 长度证明；有效场次仍须有可复查的当时来源/构建身份链。

## 二、需要退回的缺陷

### R1：退出确认后的正负状态混用，接续整改仍未闭合

受审位置：`E:\MBmod\CombatBrain 2.0\Program\src\Core\ActionFeedback.cs:268`、`:283`，`Program\src\Host\TestDriver.ActionFeedback.cs:232`、`:399`。

1. `DecayStandby` 对正反馈和负反馈都调用 `AfterStandbyCompleted`；后者只要退出确认成立，就无条件将 `Stopped=true`、`OffensiveBlocked=true`。隔离加载新重建的 Core 代码，设置真实生产链会产生的 `Reinforced=true / WaitingForExitConfirmation=true / ExitStandbyCompleted=true`，调用后确实得到 `offensive_blocked=true`。
2. Host 的 `ActionFeedbackStep:33–44` 因此在正常正反馈继任时提前返回，连后续累计损失评价也跳过。正例日志已经出现对应现象：`p3-P-20261015-220249` 于125.197确认退出并公布 Reinforce，127.708却记为 `ACTION_BLOCKED reason=loss_not_resolved stopped=1`。
3. 反方向也存在问题：`MarkStandbyFailed` 无条件清掉 `OffensiveBlocked`。对已知负反馈、退出未确认的状态运行同一生产方法，结果为 `waiting=false / stopped=false / offensive_blocked=false`。退出失败不应自动消除已知负反馈的进攻禁入条件。本次确认的是状态缺陷，未声称已经在实机观察到由此重建攻势。
4. `StandbyInvalidReason:399–430` 只检查 key 对应记录存在、质心可读、人数大于0和角色数量；没有比较准备任务与最新记录的对象身份，也未重新核对本方归属、AI/兵种资格及正反馈风险门。注释所说的“当前对象仍合格”尚未完整实现；后续写前门可能拒绝，不能代替公布之前的合法性核对。

现有测试 `StoppedIsOnlySetAfterExitConfirmation` 只测“尚未确认”；`StandbyFailureTerminatesSuccessionAndKeepsStopFailed` 还断言失败后禁入清零，未覆盖上述正负分支语义。需要用准备→等待→确认/失败→实际后续评价的同一生产路径补上关键反例，再做最小修复。

**对门1根因的修正：**交回文档将 Guard/Support 依赖与 Main 放行条件解释为永久互锁，证据不足。所依赖的 Main 是先写出的 Hold 等待任务，主力推进另有任务；本场 Main Hold 已于128.039真实写出，128.539即被原生命令覆盖，下一决策点前计划已失效。`SamplingBehavior` 仍会独立调用 `SubmitPending`，不能把 `ACTION_BLOCKED` 误说成所有写令都停了。已确认的是错误的反馈禁入状态、没有实际建立新 Guard/Support 与推进、以及随后原生覆盖；尚不能据此认定唯一根因是永久依赖环。

### R4：检查器漏掉重分配后的对象，且不核独立停止读数

受审位置：`E:\MBmod\CombatBrain 2.0\Program\_verify\check-p3-feedback.ps1:792–858`、`:885–894`。

门2必需对象仅来自 `TASK_CREATED` 的初始 `obj`，重分配时仅删除旧任务，没有补进新对象。`p3-P-20261015-221833` 本次停止实际涉及：

| 当前职责 | 对象 | 本次退出实际写出 |
|---|---|---|
| Main | `030dd50e@Infantry` | 134.036 |
| Support | `038572bf@Ranged` | 134.086 |
| Guard（重分配进入） | `008c0c1e@HorseArcher` | 134.182 |

最后一个标识的槽位后缀不等于实际兵种；独立样本显示其实际为 Cavalry。检查器却只报告 `objects_stopped=2/2`，遗漏当前 Guard。它还使用整个日志的重分配集合，未严格限制在本次决定之前。

独立敏感性复现只修改审查副本，结果如下：

| 副本 | 改动 | 当前工具结果 | 应有约束 |
|---|---|---|---|
| `tamper-missing-reassigned-exit` | 仅删去重分配 Guard 的本次 `EXIT_STOP_DONE` | **门2仍pass** | 本次确认所需的第三对象记录缺失，不能无条件通过 |
| `tamper-independent-order-contradiction` | 将134.187–145秒间该对象的11个独立样本改为仍执行旧Move/目标，生产日志保持原样 | **门2仍pass** | 独立战场读数与生产停止声称冲突，应拒绝通过 |

第二个复现不是在证明原场实际没有停止；它证明工具不敏感于直接否定停止的独立证据。`Get-IndependentSamplesAfter` 在门2后续窗口只提供时间和文件名，样本数量充足不能代替订单、目标和对象身份核验。

应按决定时的当前分配版本重建完整对象集合，逐对象对照本次旧计划退出任务、实际写出/失败、确认前后身份及独立订单/目标读数。不能用后来 plan-2 的退出补算 plan-1，也不能排除本轮新进入职责的 Guard。仍保留“诚实 NOT_APPLIED 不是停止成功”的既定裁定。

门3还存在已知口径问题：`:231–262` 取的是首个数字编号样本，非按 t0/干预时刻对齐；`:959–964` 将取到的两方绝对质心字符串完全相等当作等价条件。原派单明示位置不同不自动否定等价。当前材料不足以证明可比，维持未验证；不得反过来把同坐标、或坐标不同本身当作完整地形/部署可比性的证明。行为产物与当时来源等价证据也须独立补齐。

### R5：连接失败未进清理，仍有同名进程批量结束路径

受审位置：`E:\MBmod\CombatBrain 2.0\Program\_verify\run-p3.ps1:320–377`、`:635–693`，以及 `_p3-exclusive-stage.ps1:47–85`。

- 预处理和 `Open-McpHttp` 都在清理所依赖的 try/finally 外；连接建立失败时，已启动的 GABS helper 没有进入 `Close-McpHttp`。在审查副本精确提取未修改的374–695行，以本地 `Open-McpHttp` 替身模拟“已取得helper、端点超时”后，轨迹只有 `helper_process_acquired → outer_caught_simulated_endpoint_timeout`，`cleanup_reached=false`。未启动真实 helper 或游戏。
- `run-p3.ps1:669–674` 仍枚举全部同名 Bannerlord 并批量 `Stop-Process -Force`，没有本场已取得PID的限定；与派单“只结束本场拥有PID、不按同名批量强杀”的要求直接不符。
- 结束任务、停止游戏等异常只打印失败信息；不能据此确认清理失败会独立登记。本脚本也没有在结束后实读 `safely_exited=1`；启动前把它改成1不等于退出后成功证据。
- stage记录和参数splat修复已存在，但 stage 的 `$acquiredGame` 只在collect成功返回后置位，不能覆盖collect部分取得资源再失败的情况。交回所列四条替身测试是 stage 成败/参数传递测试，不能代替派单要求的“连接建立失败”和“清理失败”覆盖。

需要最小地把本场资源取得与异常收尾包进同一受保护范围，按真实取得的PID/连接处理，独立记录清理失败。保持完整流程在独占入口中；禁止用审查测试碰共享部署或实际游戏。

## 三、保留确认的成果与场次边界

| 整改 | 本次结论 |
|---|---|
| 1 退出确认后再接续 | 有真实进展：等待期无提前公布，逐tick核对、已关闭退出回执、截止和确认后公布均有代码及候选场证据；上述状态与公布前合法性缺口未闭合 |
| 2 当前三职责风险 | 在本次限定范围确认：Host取当前权威任务并限制plan/revision/open；Core按对象身份拒绝重复与缺失；拟新增中心从当前Main实际质心计算。相关反例测试通过 |
| 3 冻结成员可解释 | 在本次限定范围确认：Host接入 `MissingCoverage`，冻结成员缺席且无终态时阻断评价；已有可信终态、转移成员、未知终态等相关测试通过。不自动外推所有实机生命周期边界 |
| 4 独立复算三门 | 公式与历史负例纠正有效；新发现上述两种误报，未闭合 |
| 5 完整入口与安全收尾 | 入口、参数传递、语法与WhatIf有效；连接及清理边界未闭合 |

独立复算 `p3-P-20261015-221833`：t0=23.047，NE=99，NF=184；133.844的 Disengage 对应独立 DE=0、DF=17（17个友方终态的攻击方均为敌队0），与当场日志一致。旧退出三条均在deadline=134.844前写出，134.187确认后才公布plan-2。独立134.746样本中三个当前职责对象均为StandYourGround；137.564起Main才执行新撤回Move，Guard/Support保持停止；141.314之后Main又停止。撤回未满足3秒/3米效果，140.034如实取消，不能记为撤回成功。该场虽名为P，实际证明的是负反馈窗口。

`p3-P-20261015-220249` 的真实正反馈也得到独立支持：t0=23.039，NE=21，NF=184，124.703为DE=8、DF=6，伤亡来源阵营相符。但plan-2只有Main Hold写出，没有新Guard/Support普通动作和主力有限推进，**门1失败**。

| 证据 | 当前复核结果 |
|---|---|
| 新负反馈场 `p3-P-20261015-221833` | 原工具门2pass已重现；人工逐三个对象核对支持本场及时停止与随后接续。完整验收仍受工具缺陷、来源/独占流程材料缺口限制 |
| 历史负例 `p3-N-20261015-005621` | 门2fail已重现，保留修复前缺陷原判定 |
| 正例 `p3-P-20261015-220249` | 门1fail已重现 |
| 上述正例配 `p3-C-20261015-220738` | 门3unverified已重现；场景和行为可比证据不足 |

晚场缺完整独占执行记录，不能登记为完整合同场次。反之，GABS history中的启动时间本身也不能证明绕过协调锁：正常独占流程同样会调用GABS；应准确写为“启动通道归属未证实”。当前代码干净或mtime早于提交也不能独立证明历史构建的源码一致性。

## 四、处理要求

保留本次已确认的整改2、3和真实场次事实。开发在原授权范围内先闭合R1/R4/R5，不必为这些既有合同缺口另行请求扩大范围。新增命令域、系统或修改公式阈值仍不在授权内。

门1的正反馈后续实际执行明确属于原整改派单§3，交回文档“超出本单五项整改范围”的说法不成立。先用能区分原因的离线/调用链反例修复状态与执行问题；资源正式交回后，再经完整独占入口取得当前受审来源的新正例和可比对照。无需无目的重跑已确认材料，也不得以延长计划寿命、增强权重、降低效果门槛换取通过。

本报告是本次开发交回的退回意见，不是P3阶段通过文件；P4仍未授权。

## 证据包索引

- `build-cb2.txt`、`build-cbath.txt`、`build-tests.txt`、`tests.txt`、`test-results/independent.trx`：独立重建/测试。
- `source-manifest.json`、`session-manifest.json`：来源、差异与原始审查副本摘要。
- `new-negative.json`、`old-negative.json`、`positive-control.json`：原工具独立复跑输出。
- `independent-facts.json`、`audit_facts.py`：冻结集合、逐终态伤亡、攻击方与关键独立订单窗口。
- `standby-probe.ps1/json`：正确认误禁入、负失败清禁入的生产方法复现。
- `tamper-missing-reassigned-exit.json`、`tamper-description.json`：漏第三对象的敏感性反例。
- `tamper-independent-order-contradiction.json`、`tamper-orders-description.json`、`tamper_orders.py`：独立订单与停止声明冲突仍pass的反例。
- `connection-probe.ps1/json`：连接建立失败不进入清理的隔离复现。
- `scripts/`与`source-snapshot/`：受审脚本/源码，不依赖后续工作树仍保持本次内容。
