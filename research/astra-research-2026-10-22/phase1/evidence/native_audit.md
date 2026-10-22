> **研究资产阅读视图**：原件位于 [CombatBrain_Research_2026-10-22.zip](../../archives/CombatBrain_Research_2026-10-22.zip)，包内路径为 `evidence/native_audit.md`。本视图只增加说明并调整指向省略文件的 Markdown 链接；代码保持原样。完整复现请在原 ZIP 的独立解压副本中运行。研究建议未自动获批。

# TCL 与 CBATH 只读审计（2026-10-22）

**推荐结论：复用 TCL 的逐域证据口径、写前 fencing 与显式 handback 思路，复用 CBATH 的启动/主线程调用/身份/归档基础；不要复制 TCL 的“常态建议型 C、必要时 Full B”作为 CB2 产品权威，也不要把 CBATH 的整队 `SetControlledByAI` 工具当作 Campaign 真实 F6 授权适配器。** 最有价值的最小增量是：先证明 CB2 计划所需的具体命令能保持及交还，再把执行、任务职责效果、战果分层采集。没有这些数据，侧击/牵制协同算法可在纯 Core 开发，但真实收益仍是条件式假设。

## 1. 冻结身份与证据等级

本轮按 GitHub API 读取一次最新分支，然后固定 SHA 下载独立 archive。未修改或推送源仓库；没有游戏、部署或历史派单执行。三个快照均未找到 `AGENTS.md` 或 LICENSE 文件，GitHub API license 也为 null。仓库公开可读不等于自动获得复制代码许可：本报告复用机制/接口经验，代码复制前须确认权利。

| 快照 | 冻结 SHA | 本地目录 |
|---|---|---|
| TacticalCommandLab `master` | `c86167a887cb3c1691dd12fa2d19385d937e8364` | `sources/tcl/default/` |
| TacticalCommandLab `codex/local-phase10-verification-20261006` | `a4524aa1cad75f085ad9c866a4dda457dfd6ad49` | `sources/tcl/phase10/` |
| Bannerlord-CBATH `main` | `acbd5d94a45770bdeaa046f24a72b4d803054388` | `sources/cbath/default/` |

下文 `T:` 均指 TCL 实验分支固定 SHA，`B:` 均指 CBATH 固定 SHA。可生成永久链接：`https://github.com/Lzq061122/TacticalCommandLab/blob/a4524aa1cad75f085ad9c866a4dda457dfd6ad49/<path>#L<line>` 与 `https://github.com/Lzq061122/Bannerlord-CBATH/blob/acbd5d94a45770bdeaa046f24a72b4d803054388/<path>#L<line>`。`B:CBATH/` 简写路径为 `RD/CombatBrain-Autonomous-Test-Harness/experiments/test-mod/CBATH/`；`B:RD/` 为 `RD/CombatBrain-Autonomous-Test-Harness/`。

**证据可得性必须单列：**

- TCL 默认分支 HANDOFF 顶部仍以 H1 完成、H2/H2b/H3/REL 待跑为最新；实验分支顶部已到 Phase 11 基本交付通过（`T:HANDOFF.md:3-11`）。不能合并成同一个时期。
- TCL 收口文档明示 run04–11 原始日志在被忽略的 `_audit/_local-phase10-20261006/`，未进 Git（`T:docs/phase10-closure-20261007.md:9-16`）。本轮此目录确实不存在。测量身份是 `adab4cf` 加逐场四文件 dirty diff；匹配源码后录入 `e57e157`；最终研究 SHA 不是测量 SHA。
- 本轮已核验 TCL 收口索引列出的 **12 个派生 JSON/CSV 哈希全部匹配**。从派生 changes CSV 独立重算 run05/run06 的 ABA 为 **37/35**，与派生 triples 的逐对象/行号集合相同；目标 T1F0 贡献 **3/2**。这只证明派生物内部一致，不能替代缺失原始日志的完整复核。
- TCL `_audit/run7-h1-log.txt:6-20` 是可读历史驱动日志，直接支持版本、模块隔离及自定义战斗流程；其列出的 H1 原始 metrics/telemetry 不在此次快照中。因此 **0.149/0.150s** 仍引用报告转述，不称本轮重测。
- CBATH README 说最终 `zt-*.json` 在仓库，但本轮快照没有该批原始文件。最终 Zero-touch 3/3 记为 **DOC/历史实测转述**（`B:RD/16-Phase2-Freeze-CBATH-Baseline.md:12-45`，`B:RD/evidence/P2-010-result.md:115-148`）。另有较早 `p2-006-cbath-timeline-live.json` 可读：本轮实际解析 **373 点**，相邻时间差 **1.000–1.029s**、中位数 **1.004s**；它验证该较早历史记录的粒度，不能冒充最终冻结批次。
- 本轮核验结果保存 `metadata/native_snapshot_audit.json`；未使用相关记忆事实，未运行 TCL 原始 C# 回放或任何游戏测试。

## 2. 已有机制—证据—复用—缺口

| 已有机制 | 实际证据与界限 | 复用判断 | CB2 仍缺什么 |
|---|---|---|---|
| TCL 写入口闸门 | **静态事实**：Movement/Facing/Arrangement 进入 Gate；Form/Firing/Riding 与 AI 开关只观测不拦截。`T:src/CommandLab.BannerlordProbe/OrderWritePatches.cs:108-263` | 改造。可复用逐域观察和拒绝原则；不要全域复制 Full | CB2 每类任务到底需要哪些域、非目标写入口/自维护路径覆盖，以及玩家直接命令优先 |
| TCL 中性动作 | **静态事实**：`Command` 明确拒绝非 Movement，只有中性 `NeutralOrders`；没有 Facing/Arrangement 独立下令。`T:src/CommandLab.BannerlordProbe/ProbeRuntime.cs:357-385` | 经验/探针，不是战术执行器 | Move-to-position/target、朝向、阵型、开火等真实任务参数和成功观察 |
| H1 外部抢写 | **DOC/历史实测转述**：纯 Vanilla 未夺权的 HOLD 约 0.149/0.150s 后被覆盖；ActiveBehavior 名不是调用栈归因。`T:HANDOFF.md:147-183` | 保留反例：高频 SetOrder 不是权威方案 | 实际 CB2 接入后 Native writer 是否仍竞争，不能只看写频率 |
| H2 Movement 保持 | **DOC/历史实测转述 + 派生数据**：T1F0、HOLD、24.50s；G1=0、G2=100%；Native 50 次被拒（意图窗内49）；全场 G3 FAIL。`T:docs/07-final-architecture.md:31-38,94-110` | 受限候选证据 | 多实体、实际任务、玩家 F6、异常退出、TTL 到期、其他命令域和 Campaign |
| G3 失败诊断 | **本轮派生复核**：run05/run06 122/120 变更、63 键、37/35 ABA；大部分在接管前初始化，但目标仍贡献3/2；不能称“全由其他编队造成”。`T:docs/phase10-closure-20261007.md:31-84` | 复用全场+目标+时段并列口径 | 新实验预先区分初始化、保持、交还，保留旧 G3 不改历史验收；局部保持不能冒充全场稳定 |
| H3 关 AI 路线 | **DOC/历史实测转述**：同场关闭区间 Native Movement=0，释放同刻4次（3拒1允），其后10次同值写；实际 AI.Tick/Behavior 调用数未测。`T:docs/phase10-closure-20261007.md:86-102` | 仅反例/条件式路线，不能默认采用 | 是否连带停掉必要行为、玩家军士分支、多域副作用；开关组合结果不等于单开关因果 |
| Release/恢复 | **静态事实 + DOC/历史实测转述**：恢复可在 release 前/后配置；None 组可自然恢复；两个 Move struct 重发即时类型/位置相符；错误顺序重发被拒，REL=0/0/0/1。`T:src/CommandLab.BannerlordProbe/ProbeRuntime.cs:511-564`；收口 `:104-119` | 保留显式 handback 与错误时序测试；缓存旧原生命令须校验仍有效 | TTL 到期/异常恢复、目标消失、旧坐标、完整原生内部状态；Reset/重发普遍必要性未证 |
| 对象身份 | **静态事实**：TCL `T{Team序号}F{Formation.Index}`、引用绑定，孤儿拒绝；CBATH `team_id+formation_index`、没有 incarnation。`T:ProbeRuntime.cs:167-246`；`B:CBATH/SamplingMissionBehavior.cs:75-106` | 改造，不能直接做 CB2 长期身份 | Mission epoch、formation incarnation、成员/授权版本、重编映射；Team/slot 不等于 Party/领主/合法域 |
| CBATH 自动进入战斗 | **静态事实**：CustomBattleProvider→屏幕反射 VM→ExecuteStart；部署单次检查 TeamSetupOver→FinishDeployment。`B:CBATH/MissionControl.cs:243-319`；`AutoDeployControl.cs:16-62` | 复用启动与显式状态轮询 | 现有成功路径不提供可指定兵种/兵力/地形/装备/种子的完整试验生成器，更不提供 Campaign Encounter |
| CBATH 交 AI | **静态事实**：无差别遍历 `mission.PlayerTeam.FormationsIncludingEmpty`，调用 `SetControlledByAI(true,false)`，检查字段。`B:CBATH/AutoDeployControl.cs:65-122` | 测试工具可留；不可作为 CB2 授权证据 | 真实部分 F6、玩家直接命令、军团指挥角色、阵营双端独立开关和撤权原因 |
| CBATH 观察 | **静态事实**：引擎侧1s，最多900点，跳过空 Formation；位置锚点、朝向、人数、三类命令、AI flag、行为类型；敌目标恒 unavailable。`B:CBATH/SamplingMissionBehavior.cs:12-13,36-50,75-137` | 保留1Hz趋势，按具体缺口做最小扩展 | 空编队/消失身份、900点后静默停止、接战/射界/弹药/能力/士气溃逃原因、任务依赖是否生效、短于1秒授权边界 |
| CBATH 运行身份/结果 | **静态事实**：build_id、MVID、磁盘 assembly SHA、游戏版本、PID、active_modules；完成后保留最近一次 run 内存结果。`B:CBATH/RuntimeIdentity.cs:26-44,104-112`；`RunRecord.cs:16-61` | 直接复用概念与返回合同 | 新试验需另存 CB2 配置/方案/场景身份；磁盘哈希不等于内存字节验证；多 run 不应覆盖未归档结果 |

## 3. 版本、权威与原生边界

1. **目标版本一致但证据不等价。** CBATH 工程 `net472`，ReferenceAssemblies 与 CustomBattle 都固定 `1.4.8.119303`（`B:CBATH/CombatBrainAutonomousTestHarness.csproj:4,30-35`）。TCL Probe `net472`、依赖硬编码 Windows 游戏 DLL 路径（`T:src/CommandLab.BannerlordProbe/CommandLab.BannerlordProbe.csproj:3-47`）；收口报告实际游戏版本同上。TCL 源注释仍残留 v1.3.4，不应用该注释覆盖运行版本。无本机目标 DLL，**本轮未证明目标框架构建通过**。
2. **TCL 常态 C 与 CB2 产品权威不是同一个合同。** `T:docs/07-final-architecture.md:55-64` 的 C 是“原生侧决定执行/仲裁权留原生”，并明确 C 不保证独占；这是 TCL 局部研究结论。CB2 已确认合法域内独占战术决策，应选“接入/替换原生高层产出，保持所需底层执行”的路线。若仍留原生高层改目标，仅称 C 不满足产品约束；若主张改 CB2 约束，单列 NEEDS_OWNER_DECISION。本审计不提出这种改变。
3. **原生权威只能逐行动能力签收。** 生成任务前筛掉没有执行保障的候选；执行前再查身份/授权。不能让高分但无能力的侧击挤走合法防守，事后才拒绝。每个行动 capability 记录所需域、保持条件、交还策略、证据环境，未证能力标 NEEDS_ENGINE_EVIDENCE。
4. **TCL identity 不覆盖动态重编。** Team 序号/Formation 槽位可作诊断地址；它们不能证明 Party 所属、有效玩家命令或 F6 来源。CB2 需要 MissionEpoch+FormationIncarnation+MembershipRevision+AuthorityRevision，宿主对象引用只在当前主线程观察快照中有效。此处是针对重编和迟到命令的最小合同，不建议另造通用 Registry 平台。
5. **参考探针的 fail-open 不是产品故障恢复完成。** Prefix catch 返回 true 是实验装置优先不崩宿主（`T:OrderWritePatches.cs:133-137`）；CB2 必须先停后续自写、撤销本代 pending 动作，再完成可证 handback。只放开一个入口仍可能残留 CB Behavior/权重/任务执行器。
6. **1Hz 可以看趋势，无法证明亚秒控制边界。** TCL H1 覆盖约0.15s，而 CBATH 每秒一次；无需全面改成5Hz。优先对写入/撤权/身份变更/职责交接事件按引擎时钟记录，在需要证明保持的对象上做有限窗口独立采样。生产反馈仍是进程内，不等待 CBATH 或磁盘日志。

## 4. 最小引擎探针与采集增量（RECOMMENDED，均未执行）

| 探针/顺序 | 最小场景与干预 | 记录与独立判据 | 否证/失败后备 |
|---|---|---|---|
| E0 授权映射 | 正常 Campaign：玩家领军、受他人指挥、多领主 Army；两 Formation 一手动一 F6；直接命令→F6→收回；援军实际入场。敌方启用/玩家关闭再互换 | 只读 Party/Army/Team/Formation/Player role 映射；真实委托事件来源；AuthorityRevision；逐写入裁决。检查手动编队零 CB 写入、撤权后旧命令不复活、观察对象不自动入授权集 | 无法可靠识别某角色时，该上下文禁止 CB 写入并明确不满足首版该范围；不能用 IsAIControlled 猜测。纯 Core 不被阻塞 |
| E1 每项动作保持与交还 | 受控 Mission 先测真正要用于候选的 Move-to-position、面对威胁、队形/开火组合；逐域单独再组合；授权窗、玩家接管、异常、到期、目标消失 | CB request/host actual/source/identity/time；独立实际位置/朝向/队形/开火；成功接受、持续保持、交还后 Native 合法写分开；记录 Harmony patch 与旁路覆盖。只对本项域/环境签 capability | 保持或交还失败则禁用该行动组合，候选退回已验证动作；不以高频抢写补救，不自动 Full 拦全域 |
| E2 ETA 与支援机会 | 弓兵暴露，枪兵距目标可近/远，骑兵可近/远；开阔/窄路，Vanilla Combat/RBM Combat 分开；攻击与不攻击为对照 | Formation hull/实际位移、进入有效作战范围时刻、目标/支援有效接战窗口、弹药/损失/溃逃原因。ETA 估计区间覆盖实测误差；侧击开始前的牵制效果独立标注 | 支援总早于骑兵，或机动 ETA 区间太宽，使本模型不能稳定区分陷阱时，保守剪掉/转有上限施压；不倒调权重迎合结果 |
| E3 协同贡献与反制 | 正面牵制+侧击，牵制高损失仍有效；敌方保持、援护、反击三类；对照是相同资源的独立贪心分配/当前方案，做去依赖与去切换成本消融 | 分开 command accepted、screen/fix effect、flank arrival、全局损失与结果；支援窗口是否仍在，角色是否重复承诺；固定战场初态、换攻防角色，记录敌方实际行为而非预设其配合 | 依赖模型导致过度等待或高损牵制却无侧击收益时，缩短预测视野/增局部修补；若没有稳健收益，保留简单基线 |
| E4 动态重编 | 受控 Mission 验证 transfer 可行性后，Campaign 援军/军团下拆分、合并；执行中玩家接管、目标槽位复用、成员转移失败 | 每步成员数和可读成员签名、old/new incarnation、任务/Duty 迁移、授权；中间状态可观测；转移完成前不把两份兵同时承诺；失败时报告已完成步骤 | 不假设事务回滚；保留实际合法编组，停止在途冲突动作，Commander 安排职责修补；不可因此宣布首版重编范围取消 |
| E5 长战与预算 | 超15分钟正常规则战斗、多 Formation；同一输入分别正常/受限预算 | 900点饱和检测或分段落盘、采集丢失计数；持续权限/安全/反馈是否保留；Core候选和关系边计数、宿主聚合/预测/诊断分项CPU分布 | 先削候选/预测/诊断；日志失败不能阻断进程内控制；不要用原型毫秒外推游戏帧预算 |

### 按需求收窄 CBATH 改动

- **保持冻结基线不动**：在独立 CB2 实验 adapter 增加 E0/E1 所需事件输出，既有 CBATH 八条判据仍用于老基础设施基线；新增实验验收独立命名。具体需求与采集缺口已明确，符合冻结文档 `B:RD/16-Phase2-Freeze-CBATH-Baseline.md:168-188` 的需求驱动扩展触发条件。
- **任务侧最小事件**：`mission_id, tick, engine_time, formation_incarnation, membership_revision, authority_revision, plan/task/duty_version, event_type, reason, command_requested, host_observed`。默认只记录变化，批量采集到内存队列；主线程读取，异步落盘不反向成为反馈必需条件。不要引入 ACK、持久化消息总线、线程/租约除非能证明此适配确实需要。
- **独立效果观察**：保留 CB2 自报 TaskHealth，但加入足以否证自报的引擎事实——有效位置/前沿、面向、实际接战对象、队形/速度、弹药及成员状态聚合。人数下降可能是伤亡、转队、溃逃或撤场；动态重编场景下不能沿用“CountOfUnits 下降 = 伤亡”的简化验收。
- **身份与新鲜度**：在当前 Mission 内为可见对象分配实例标识并追踪退场/重编，绝不把 slot 当生命周期；对象不明时输出 unknown 并拒绝依赖该对象的写入。保留 `unavailable`，不虚构 closest enemy ID。采样加 `truncated/dropped/sample_age`，900点之后明确终止/分段状态，不能静默给出完整战斗印象。
- **实验构造**：现有主通路使用默认 CustomBattle 配置，先增一个只承载 E2/E3 变量的 manifest（角色、实际兵种/数量/装备、地图、部署、模组配置、seed若引擎可控），启动后回读实际值。配置失败则该轮 invalid，不能以请求值替代实际值。复杂 Mission 不替代 Campaign 链。
- **正常规则优先**：此冻结 CBATH 代码未显示特殊士气/强制死战策略；也没有足以证明全套正常士气结局的字段。新试验必须记录规则/模组配置、溃逃/退出原因；任何受控死战实验单列，不外推正常战斗。

## 5. 最小真实评估协议

首次实机先做可辨识性 pilot：确认 E0/E1 真正可观测、任务效果标签一致且干预成功，再决定战术试验重复数。每组保存全部失败/invalid，分别报告基础设施失败率、动作保持率、任务职责效果、胜负/损失。不要拿“≥40样本、≥10日志、3/3成功”当新AI收益门槛。

主对照使用相同观测、动作 capability、权限与预算；CB2推荐、简单竞争方案、当前可运行方案能公平复现时才比较。Vanilla AI 与 RBM AI 分开实验，RBM Combat 配置保持相同；不能把 CB2+RBM Combat 对 Vanilla Combat 的规则差异当算法收益。成对起始场景、攻防/初始位置互换，敌方响应多种且记录实际路径。没有可控随机种子时用场景/初始读回和运行顺序随机化记录替代“完美配对”声明。

先依据 pilot 的方差、失效率与负责人关心的最小实际差异估算后续样本量，报告区间和原始分布；不预设无依据胜率发布线。E0/E1通过仅证明其具体环境内的权限/执行；E2/E3才开始提供真实引擎下战术机制/战果证据；Campaign 集成仍独立验收。所有本轮引擎实验均为 **NEEDS_ENGINE_EVIDENCE / NOT_RUN**。

## 6. 可独立派发的首批工作包

**WP-N1 合法域与授权探针（先于任何真实战术启用）**：输入 CB2 稳定指挥域/F6 合同；建议宿主 `AuthorityAdapter` 与实验 observer，不改纯 Core 规则。先只读打印 E0 的映射与来源，再加写前检查。须保留玩家直接命令优先、两端独立开关、未知拒写；验收是 Campaign 真实事件序列+独立实际写入，不能只验证布尔字段。阻塞为 API/角色语义不可得，后备为特定上下文 fail-safe Native handback 并列首版阻塞。

**WP-N2 任务行动 capability 证据表**：输入推荐最小任务集合，建议在 Host action adapter 声明 Move/Facing/Arrangement/Firing 的需求与支持范围。一次只接入一个任务动作，跑 E1；输出 `Supported/Unsupported/Unknown` 与原因，Core 分配前即可剪除。保持 P3 合同；缺覆盖则退到已证组合，不擅增 Harmony 全域拦截。

**WP-N3 最小观测增量**：输入 E2/E3 具体可区分假设，沿用 CBATH 主线程与 run身份，建立外置 CB2 实验 adapter，加入 incarnation、执行事件、职责效果独立观察、样本饱和标记和场景实际读回。验收包括日志关闭仍能运行反馈、1Hz保留、短时事件不丢、空/复用槽位不串台。缺游戏时可先完成 schema 与读写/时序单元验证，标非引擎事实。

**WP-N4 Campaign 无人值守接续**：重用 GABS lifecycle，新增受控存档/测试 Campaign 从 Party/Army→Encounter→Mission→部分F6→援军→结算的测试脚本；生产规则不改。仅 E0 场景必要构造，避免先重建通用平台。以真实事件与结算读回验收，失败保留 run和配置；受控 Mission 成功不抵该包验收。

## 7. 本审计未覆盖

未逐行审查所有 Core 仲裁/Lease 实现；未获得 TCL run04–11 与 CBATH final zero-touch 原始归档；未读/执行本机目标 DLL；未做游戏运行、构建、性能或战术收益比较；未证明 Campaign、多人军团、真实 F6、动态重编及万人规模可行。所有建议都按缺口定位，并非旧仓库规范对 CB2 的新增裁决。
