<!-- PROMPT META
Name: CombatBrain 2.0 P2 Restart Remediation Dispatch
Version: 1.0
Author Role: CombatBrain 2.0 Development Dispatcher and Reviewer
Model: GPT-6
Created: 2026-10-13
Updated: 2026-10-13
Target: CombatBrain 2.0 Mainline Development Agent
Scope: CombatBrain 2.0 / P2 Limited Remediation and Runtime Evidence
-->

<!-- CHANGE LOG (newest first, keep last 3)
v1.0 2026-10-13 22:16 | role=CombatBrain 2.0 Development Dispatcher and Reviewer | model=GPT-6 | editor=codex-cb2-p2-restart-dispatch-20261013 | note=按当前源码和最新实机复核交接未提交整改，限定完成间隔、实时依赖、原生覆盖与实机证据闭合
-->

# P2 接续整改派单

用户因原开发会话上下文损坏要求重新接手。**收到本单的新开发会话直接接续已授权 P2 整改，完成必要验证与实机证据后集中交回，不再逐项询问是否继续。** 单 AI 执行，不自动建组、创建其他聊天或转发消息。编写本单的会话只做质检与派单。

本单登记 `CB2-PROMPT-0003`；`CB2-PROMPT-0002` 继续作为 P2 正式验收合同。本单补充当前接手事实和剩余任务，不降低原合同三门及 A1–A13，不重开 P1 历史验收，不扩大战术范围。**当前 P2 未通过阶段退出。**

## 1. 接手基线与阅读顺序

开发 cwd 必须是 `E:\MBmod\CombatBrain 2.0`，不是 ChatGPT 项目镜像，也不在 TCL 落盘。先核实际 Git 根、分支、HEAD、暂存区、脏文件，再查 registry 状态与摘要，读取：

1. `AGENTS.md`（含 PAIPOS Lite v0.1、§4a 长期开发节奏）、`E:\MBmod\AGENTS.md`、`E:\MBmod\manager\Prompt 题头元数据规范.md`。
2. 本单；`_audit/cb20-p2/2026-10-13-restart-review/RECHECK.md` 与 `current-snapshot.json`。
3. `Program/_verify/P2-MULTI-FORMATION-DISPATCH.md` v1.1 的 §3–§12；原 P1 最终验收按原单引用保留。
4. 只对仍需定位的问题阅读当前源码、最新 A 原始证据和旧审查；旧 `P2-IMPLEMENTATION-HANDOVER.md` 未同步最新整改，不能照抄其当前状态。

编写时源代码基线：CB2 `master / f60f8c0` + 11 个已跟踪脏文件；CBATH `E:\MBmod\CBATH-P2`、`codex/cb20-p2-evidence / b9fcef7` + P2FormationEvidence.cs 脏增量。本单/审查提交会使 CB2 HEAD 前进，但不改变这份源码基线；接手以现场和快照逐字节对照。**不要 reset、checkout 还原、清理未跟踪目录，或重建一份源码覆盖现有修复。** 若快照后又有改动，重新读差异后接续，不能覆盖。

已验证应保留：未写令不得释放、g1/g2 推进身份、整批 ReassignRoles 与新版本依赖重绑、候选距离/人数排序、推进写后稳定窗口、新分配延迟、独立 Mission/Formation 身份、预算内退出投递入口。现有测试本次 222/222，两个宿主重建成功；这些不是整体通过依据。只复查实际改动/必要反例，不按测试数追进度。

## 2. 必须完成的有限整改

| 项 | 现场缺口 | 完成条件 |
|---|---|---|
| R1 实际普通写令间隔 | 新 A 重分配 Guard/Support 在 40.643/40.705 写令，间隔 0.062 s；二者可同时 arm，预检查后缺落地约束 | 初始、重分配、推进的普通命令实际间隔均≥1.5 s，新分配首令从新分配时刻计≥1.5 s。修正同批顺序/前序或待执行时段，确保真正写入时成立；不得放宽常量。等待不伪造新获选或刷新首次提交/获选截止，避免简单拒绝后丢失执行机会。安全退出仍走自己的一秒预算。 |
| R2 最新依赖与同 Move 改目标 | Core 验证的是 Requested 点，反馈用首次缓存；主力写前只核自己的提案内容。独立反例：读回偏离20 m，推进门仍 true | 保护/辅助当前实际 Movement、有效目标、可读性、身份/版本/资格/人数/质心/成员摘要一起复核；同 Move 改目标误差>0.5 m、未知或关系失效不得放行。释放任务与实际写主力推进前都使用最新有效读数，不能仅沿用 Effective/Ready。已写推进失依赖按合同即时安全处置。3 s 效果窗口与20 s 关系窗口仍分开。 |
| R3 原生覆盖与行为权重 | GuardArmedWeights 每 tick 把原生归零权重补回8，新 A 出现6次，且覆盖P1 | 移除这条未经合同授权的逐 tick 保权路径；只为当前合法候选按既有 Route C 正常 arm/获选，旧任务和失资格对象不维持权重。原生覆盖应如实失败/撤权，不能用补权、抢令、压制原生或重试延长计划。若在允许通路下始终无法形成要求的实机链，保存唯一执行证据和具体矛盾，交回一个最小替代，不继续刷场次或擅自扩权。 |
| R4 实机闭合与交回 | 新 A 虽有实际角色转换，但违反R1/R3；新 B 归档不完整；新修复及CBATH身份未提交，旧报告未更新 | 取得合法通路下的 A 正例、完整 B 反例和必要P1 P/N回归；源/dirty清单→构建→部署→实际加载可核对，原始记录能逐命令复查，收尾状态明确。本任务源码与CBATH增量独立本地提交，更新简短交回，不 push、不建PR。 |

R1–R4 是本次任务编号，不是新架构。无需新建调度框架、租约、全局 Harmony 拦截或通用观测产品。读取 API 先核本机引用和既有调用，不猜成员；目标读取有缓存副作用时按公开支持的副本方式处理，不私读引擎字段。

先补能区分 R1/R2 与陈旧事件影响新身份的最小反例，再精准修改。保留已修好的推进 ID、依赖与旧事件隔离；旧审查反例脚本有些前提已随修复改变，不能把不再适用的夹具失败当成实现失败。源码只用精准 edit/apply_patch/write，不用 shell 文本替换回写，不顺手格式化整文件。

## 3. 实机证据安排

先处理已有失败场的证据与共享状态，随后使用现有完整入口。**不把 RESULT: OK、测试全绿或更长计划寿命当成阶段通过。**

- 最新 A：`Program/_verify/sessions/p2-A-20261013-210823`。保留其真实角色转换和新身份事实，不能计作最终合法正例；R1/R3 修正后重取受影响链。
- 最新 B：`p2-B-20261013-211339` 无完整 CombatBrain2.log，末端 JSON 0020/0021 不可解析。共享 B 日志已由审查逐字节保全到 `_audit/cb20-p2/2026-10-13-restart-review/recovered-B-log.bin`，大量 NUL，不能清洗后冒充完整取消证据。原场保留失败/证据不足状态。
- 编写时未见 Bannerlord.exe；锁 JSON 残留主线组 PID16316，但该 PID 未见。P1 哨兵0、P2哨兵1、P2探针仍是 before_established。接手重新核归属与占用，在独占入口内清理本组遗留开关并记录结果；不能删锁抢占、强杀他组或把进程空闲当正式交接。
- A 须先有三职责实际行为、关系建立和主力实际有限接近、稳定≥8 s及竞争窗口；再让实际 Guard 失资格。有真实替补，生产规则使原 Support 改 Guard、另一对象补 Support；新任务均获选/写令，主力实际 Hold，关系重新成立后用新身份合法推进。保持失资格至少4 s，恢复时刻可在合同范围内按真实前置安排，记录有限保持时间及原生恢复副作用；不得直接置Ready、硬编码角色交换或用保权压住覆盖。恢复后观察≥8 s，旧任务不复活。
- B 在准备期真实消除额外合格替补（自然只有三支时如实记录无需制造第四支），人数仍计入统计。未建立关系且主力Hold时实际Guard失资格；生产明确不可执行、旧普通任务不释放、每个合格对象在预算内有真实退出尝试与真实结果。不要求强造某个事件名，不把撤武装等同士兵已停步。
- 本轮修改共享执行/反馈路径，保留一组新受审产物 P1 P/N必要回归。未受影响的 A5 等旧证据可以按差异分析继承，别重复刷全套场次。只因源码变化、判据失败或必要证据缺失补验。

实机前读取观测组根 README.md 和 coordination/handoff.md；完整“配套部署→开关→既有CBATH启动/采集→归档→正常/异常清理”全在 `Invoke-ExclusiveGame.ps1 -Owner '主线组'` 内。使用本组 `run-p2-exclusive.ps1` 与其 stage 脚本；P1用既有完整独占入口，不直接运行只采集脚本。PS5.1 验证脚本新增内容（含注释）遵守现有 ASCII-only 约束。

仅清理本场拥有的资源，恢复本场修改的原配置；原状态缺证据时明确交回实际状态，不能猜“已恢复”。启动前冻结实际源码提交或dirty文件摘要、两份DLL SHA/长度、加载CLR Location/MVID和运行身份。source_sha256 若指构建输出DLL应明确注明，不冒充源码摘要。

## 4. 文件与提交边界

- CB2：原合同白名单内的协调 Core、必要 FormationSnapshot/TaskFeedback 与 Host TestDriver/FormationSampler/SamplingBehavior/EligibilityProbe、相关测试/工程及现有完整验证脚本。本任务必要字段在现有结构扩展，不建立无关框架。
- CBATH：只在 `E:\MBmod\CBATH-P2` 接续当前 P2FormationEvidence.cs 增量；如需薄入口/Compile注册按原白名单。身份来自实际引擎对象，不从CB2日志回填；身份哈希与代表类别分别解释。精确提交本任务路径，不从原脏CBATH树拿整工程。
- 观测组三个 `E:\MBmod\BattleObservation-V1\CB2/CBATH/TCL` 工作树禁止修改、构建、切分支或提交；不改 ObservationAdapter.cs、观测组接口或未经验收的观测功能。TCL只读。保留既有 BuildIdentity.g.cs 和运行/临时文件，自动生成身份不是手工源码提交。
- 不用 git add -A / commit -a。只按明确路径或必要分块暂存；CB2实现、CBATH增量、交回材料分辨清楚。没有 push 或PR授权。

## 5. 集中交回

按 AGENTS §4a 连续完成，不碎片汇报、重复确认、追测试数量或增文档篇幅。仅重大歧义、新增授权或真实阻塞打断。资源受阻继续可独立推进的离线任务，并保留明确阻塞。

交回只需：实现提交/dirty摘要与CBATH提交；R1–R4结果及关键反例；最终有效 A/B/P/N索引、产物身份、必要普通/退出命令时间表和独立样本；A1–A13最终状态、继承依据与未覆盖项；共享资源收尾。追加到现有交接报告的最新结论和必要引用即可，不复制整份历史。独立验收前只写“开发自检完成、待独立验收”。
