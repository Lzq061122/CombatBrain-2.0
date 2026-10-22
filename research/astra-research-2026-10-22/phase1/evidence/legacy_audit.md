> **研究资产阅读视图**：原件位于 [CombatBrain_Research_2026-10-22.zip](../../archives/CombatBrain_Research_2026-10-22.zip)，包内路径为 `evidence/legacy_audit.md`。本视图只增加说明并调整指向省略文件的 Markdown 链接；代码保持原样。完整复现请在原 ZIP 的独立解压副本中运行。研究建议未自动获批。

# CombatBrain legacy 静态审计与可复现性

审计日期：2026-10-22（Asia/Shanghai）。冻结仓库：`Lzq061122/Bannerlord-CombatBrain`，默认分支 `main`，SHA `e30a3543f3a249ac458b0894bfc1fd7a17a17f2d`。独立下载 GitHub tarball 至 `sources/legacy.tar.gz`，提取至 `sources/legacy/`。下载来源、归档 SHA256、检查命令与完整输出在 `evidence/legacy_source.json`。没有修改生产文件、提交、部署或执行历史派单。

下文代码路径均相对于此冻结仓库；固定证据链接前缀为 `https://github.com/Lzq061122/Bannerlord-CombatBrain/blob/e30a3543f3a249ac458b0894bfc1fd7a17a17f2d/`。行号是本次冻结快照的行号。**源码事实、转写反例、本次实际检查、历史运行报告四种证据分开。**

## 入口与边界

已读 `AGENTS.md`、`README.md`、`docs/knowledge/INDEX.md`、PAIPOS Lite v0.1、CURRENT_STATE、CONSTITUTION；追加源码仅为解释战术能力、输入契约与验证链。Windows 外置生命周期登记表本机不可访问，登记项状态未核验（原流程称 BOOTSTRAP_FAILURE）；按本次显式只读研究授权继续，未把旧任务卡当行动指令。仓内 `CombatBrain-2.0/` 只作历史镜像识别，不作为独立 CB2 仓库最新设计决定。

CURRENT_STATE 的 `last_verified_commit=395fe13...`，不是冻结 HEAD。GitHub compare 显示 HEAD 较它领先 51 个提交，包含 `CombatAiMissionLogic.cs`、`RouteAControl.cs` 等 C# 与测试变更；不能把该状态档的 1297 PASS 或旧运行结果直接贴到 HEAD。`docs/offline-validation-hardening-2026-10-06.md:18–24` 自己也明确新验证机制未跑完整 Windows 套件、未推进已验证基线。

## 算法事实与能力边界

| 面向战术智能的问题 | 实际算法、符号与证据 | 可以说明什么；不能说明什么 |
|---|---|---|
| 候选从哪里来 | `TacticManual.Order/Matrix/Counter` 固定 9 意图×12 特征与 9×6 信念反制表；`BeliefIntentEngine.Evaluate` 遍历九项取线性分最高者（Intent/TacticManual.cs:17–79,98–121；BeliefIntentEngine.cs:41–85） | 已有便宜的反应式基线；没有根据当前地形/兵力生成带目标、执行者、前置条件、时间窗的方案 |
| 对手推断 | `OpponentModel.ApplySignal` 累加预设向量，3s 同信号冷却；统一8s半衰衰减；归一化后用最大概率≥0.5、熵≤1.8判断可读（Intent/OpponentModel.cs:222–320） | 是启发式证据累加，不是已校准的贝叶斯后验或对手策略搜索；统一缩放会在归一化中抵消，证据老化不等于读出置信老化 |
| 自军主力/敌军目标 | `MainBodySelector.Select` 60m邻接图+BFS，最大人数簇；平局偏步射/小ID。`OperationalObjectiveSelector.Select` 对已知敌做同类聚类，最大簇占已知敌≥50%才有目标（Coordination/MainBodySelector.cs:29–116；OperationalObjectiveSelector.cs:30–109） | 可复用确定性聚合；最大敌簇不是最佳机会。三簇各100人且相距>60m时全部已知仍无目标；100+99人数交替会跳目标，无持续计划身份 |
| 协同任务 | `FormationMissionAssigner.Assign` 为按撤退→预备→兵种的固定映射；近战骑兵进攻意图一律Flank，步射一律SupportMainEffort；骑射仅Exploit+Pursue时追击（Coordination/FormationMissionAssigner.cs:110–164） | 有角色词汇，没有搜索“谁牵制、谁侧击、谁掩护谁”的组合。没有角色唯一性、支援容量、机会互斥或联合收益 |
| 局部投入判断 | `CommitmentEstimator.Estimate` 按距离/标称速度估ETA；友军60m、10s容差；敌方接敌点100m内、已知且及时；`Evaluate` 合并支援不足/局部劣势/孤立风险，至少两项才Denied（CommitmentEvaluator.cs:134–250,268–392） | 可保留安全门与估计基线。支援只因靠近且速度够即被计入；无任务承诺、路线阻塞、朝向、已分配对象，所以同一友军能被多个编队同时算成全额支援 |
| 主力释放/资源投入 | `armyReleased = HasOperationalObjective && TargetAlignedWithObjective`（CommitmentEvaluator.cs:199–221）；`ExplicitReserve=false` 在胶水固定（Ai/CombatAiMissionLogic.cs:828–834） | 有近似释放门，未形成真正的预备队资源账本或计划批准事件 |
| 战斗阶段 | `BattlePhaseFSM.Advance` 五态、5s驻留；OwnSurvival<0.4立即Retreat且终态；Advance→Engage须接触；Engage→Exploit须弱化及敌存活<0.4或Pursue（Battle/BattlePhaseFSM.cs:68–123） | 状态上下文可以提取；不是可恢复的战役计划。Stalemate不消费、Exploit无回退、增援后不能退出Retreat，是明确第一版限制 |
| 骑兵机会 | `CavalryWaveFSM` Scout→Setup→Charge→Recall→Reform；机会枚举只有Gap/ChargeEnded（Cavalry/CavalryWaveFSM.cs:15–57）。胶水Setup超时且存在敌人便伪造 `EnemyLineGap`（Ai/CavalryGlue.cs:176–196） | 有波次节奏；不能据机会名称声称真实缺口、协同牵制或冲锋窗口已检测 |
| 射手与阵地 | `RangedBrain` 单目标线性加分（英雄+3、重骑+1.5、距离惩罚、弹药匹配），弹药≤15%退/近战（Ranged/RangedBrain.cs:39–81）。阵地七原因中射界恒1、位置价值/推进/地形变化恒0（Ai/CombatAiMissionLogic.cs:1865–1871） | 已有局部打分与保命门；没有射线覆盖、集火饱和度、友军遮挡、跨编队火力分配。四类阵地输入仍是未接线占位 |
| 命令协同 | `FormationCommandArbiter.Resolve` 每编队分别解Movement/Facing/Arrangement；按优先级→冲突表→source/kind/id决胜；含时效、身份、未知目标、外部覆盖拒绝（Coordination/FormationCommandArbiter.cs:92–230） | 命令冲突仲裁已具备纯核心；它保证本地命令一致，不保证战术组合有效或多单位同步 |
| 实施与反馈 | `RouteAControl.Update` 交付超时释放、离散意图改变+冷却+有界次数才重取（Coordination/RouteAControl.cs:186–238）。`ExperienceGlue` 仅切意图时用窗口敌净掉人速率对历史均值判成败；冷宫后按信誉挑替代（Ai/ExperienceGlue.cs:89–95,123–187） | 控制交付证据很有价值；杀伤代理未辨“牵制成功但没杀人/撤退成功/敌增援”，也未绑定联合计划的预期效果。命令已下达、机会产生、任务完成、战术成功必须分开 |

## 已确认的源码缺陷与反例

### L1：保持时间累加到不同候选，当前方案评分冻结

固定证据：[BeliefIntentEngine.Evaluate](https://github.com/Lzq061122/Bannerlord-CombatBrain/blob/e30a3543f3a249ac458b0894bfc1fd7a17a17f2d/CombatBrain/Intent/BeliefIntentEngine.cs#L60) `:60–85`；F1 `IntentEngine.cs:48–72` 同构。

- 没有pending candidate标识；任何非current候选超过阈值都共用 `_holdTimer`。默认权重、belief=null、文化=null：先11拍（每拍0.5s）令 `Arc=.9, EnemCav=.8, FlankEx=.7`，ProtectArcher最优；第12拍换成 `Cav=1,FlankEx=1,Gap=1`，Flank只出现0.5s即切换，而注释承诺新意图持续6s。
- 阈值比较的是缓存 `_currentScore`，仅current仍是第一名或切换时更新；不重算当前计划的当前价值。先 `Line=Height=Dist=1` 得Defend=.9；然后持续 `Power=Moral=PenB=1`：Attack=.8，当前Defend=-.3，但0.8不超过历史0.9×1.15，连续120拍/60s仍保持Defend。
- 本次从冻结C#解析矩阵，Python逐句转写Evaluate核对两反例（结果在metadata）。这不是C#编译或游戏运行复现；说明可确定的算法语义。现有防抖测试只测固定候选11拍/12拍（OfflineTests/Program.cs:803–824），没有覆盖候选互换/当前分值衰退。
- `BeliefIntentEngine` 构造参数 `weights` 保存但从不参与Evaluate，后者直接用TacticManual静态表（:11,26–29,50–52）。因此不能把“注入经验修正后的矩阵”当已实现接口。

### L2：感知记忆被降为bool，随后回读隐藏真值；另有完全绕过迷雾的目标入口

- `KnowledgeView` 保存位置/时间，TryGet计算衰减置信和位置噪声（Perception/KnowledgeView.cs:31–61）；但 `IsEnemyFormationKnown` 丢弃out info，只返回存在布尔（Ai/CombatAiMissionLogic.cs:551–559）。`CoordinationFrameBuilder.BuildEnemy/BuildSnapshot` 再读真实当前位置、人数、完整度、速度（:30–64）。敌人t=0被看见、t=1进入山后、t=10移动100m，记录未超过25s，决策收到的是t=10真实中心而不是t=0记忆中心。置信在12.5s已降至0，仍可Known到25s。**这是调用链推导，不是实机确认。**
- `TickTargeting` 实际只检查敌我与150m半径，不调用KnowledgeView/IsEnemyFormationKnown（Ai/CombatAiMissionLogic.cs:2026–2046）。150m内山后从未看见的编队仍可进入候选；结果优先流入Commitment（:941–952）。且:2030把“玩家队免被本Mod指挥”扩大成“敌方目标候选排除玩家队”。
- 队级意图也读所有敌存活（:639–649）和真实敌队中心/展开度（ComposeVector :1217–1253），OpponentModelGlue:113–120逐敌编队取当前聚合无迷雾门。故仅在新规划器入口加一个Known字段不足以修复输入契约。
- 2.0应强制 `ObservedSnapshot` 作为唯一敌方信息接口，附source/observed_at/age/confidence；决策不得持有可回读引擎实体。敌隐藏真值变化而观察流不变时，候选和决策必须不变。

### L3：完整度单位不一致，造成战力估计饱和

`FormationIntegrity.Compute` 返回0..100（Formation/FormationIntegrity.cs:50–65）；`CoordinationFrameBuilder` 未除100直接写Snapshot（:52–61），Snapshot却声称0..1（Coordination/FormationCoordSnapshot.cs:20），`CommitmentEstimator.IntegrityCoefficient`把>1全部钳为1（CommitmentEvaluator.cs:283–294）。因此完整度10/50/100的编队都获得完整战力系数1，10%完整度按契约本应0.55。需改接口单位并加胶水到纯函数的集成契约测试；只给纯函数输入0.1会掩盖错误。

### L4：目标迟滞没有身份有效性，可能永久保留旧目标

`FormationTargeting.Select` 比较新旧位置≤7m就返回last（Ai/FormationTargeting.cs:100–107），没有检查旧目标ID仍在候选。旧ID=1、位置(30,0)已消失，新有效ID=2、位置(32,0)连续出现，每0.5s都保留ID=1并刷新Time。应先做身份/时效/可观测性验证，再对同一目标的位置使用迟滞。这里是源码可确定反例，未运行游戏。

### L5：知识与学习的置信、成败和已执行意图不闭环

- OpponentModel所有维度同乘衰减，归一化后相对概率/熵保持不变，直到总量≤1e-6突然均匀化（Intent/OpponentModel.cs:270–320）。8s半衰不是“8s后少一半可信”。应独立保存证据质量/新鲜度，未知度不能靠归一化掩掉。
- 经验仅当Engine报切换时才调整，且 `state.LastIntent` 被替代意图覆盖，但Engine内部current仍保留原候选（Ai/CombatAiMissionLogic.cs:680–700）。下次评分与实际执行意图可能分叉。低杀伤也可能是成功牵制/无伤撤退，敌增援被直接记作反制（ExperienceGlue.cs:152–164）；没有因果归属。
- `CampaignHabits`整条链已主动禁用 `_habitsEnabled=false`，原因是早期把自己意图输出当玩家行为，循环自证（ExperienceGlue.cs:67–71,128–132）。这是重要反例经验，不是“跨战学习已完成”。

## 资产处置建议

| 处置 | 资产 | 复用方式/先决条件 |
|---|---|---|
| 可直接提取逻辑与测试思想 | VecMath、BracePlan/ProtectPlan几何、纯BattlePhase输入输出、MainBody BFS、Proposal拒绝原因、CommandArbiter确定性决胜、命令归因/交付确认、验证组完整性检查 | 作为独立组件或朴素基线；迁移时保留身份/时间戳/因果字段，不宣称原参数适合2.0 |
| 必须改造 | KnowledgeView→Snapshot链、IntentEngine/BeliefIntentEngine、CommitmentEstimator、FormationTargeting、战斗/骑兵FSM、ExperienceGlue | 修上述L1–L5；增加单位类型、能力/路线/时窗、角色依赖、保留资源和反馈语义；先做不接游戏的机制实验 |
| 主要作为经验 | Route A有界重试、覆盖后断流、玩家豁免冲突、静态测试与运行脱节、CampaignHabits循环自证 | 设计验收反例与适配契约；不能据这些控制面修复声称智能层已完成 |
| 淘汰为主决策方案 | 固定九意图表充当全军计划；射手类别硬优先；超时伪造骑兵缺口；最大敌簇唯一目标；将存在记录等同持续可见；无context的冷宫替换 | 可保留作弱基线/消融项，不作为2.0主脑。删除/替换尚未执行 |

资源分配缺口应以反例检验：两个攻击者各自把同一支100人友军算为100人支援，联合方案实际可兑现支援只能分配一次；“射手能射到目标”与“射手压制可支撑侧击”不是同一关系，后者需时间窗、作用目标、朝向/射线、持续时长与任务承诺共同成立。

## 本次验证与历史证据

本次实际运行（固定快照、macOS、bundled Node）：

```text
/Users/tianxq/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node --test --test-reporter=tap OfflineTests/VerificationContracts.test.js
35 tests / 35 pass / 0 fail，退出0。
相同node执行 VerificationContracts.js --source <frozen repo>
[PASS] Verification contracts: --source，退出0。
```

另执行默认矩阵解析与Evaluate逐句Python转写，两个L1反例满足预期。它们是研究模型核对，不是旧C#本体运行。**未执行**C#构建、OfflineTests.exe、真实DLL反编译、完整变异脚本、游戏采样。PATH无dotnet、PowerShell/pwsh、ilspycmd、Mono/C#编译器；OfflineTests.csproj:97–134硬编码Windows游戏/Harmony/MCM DLL，目标net472；不能把“纯函数可提取”写成“现有完整OfflineTests可跨平台开箱即跑”。

完整已有基线入口为 `OfflineTests/RunOfflineTests.ps1:68–105`：重建生产/测试、检查器自测、C#回归、回归组完整性、静态契约，输出源码/DLL哈希和Runtime=NOT_RUN。其设计值得继承，仍需Windows和合法游戏安装。研究目录为归档快照无.git，完整脚本本身还要求git rev-parse；应在合法Windows clone的同SHA运行，不在此伪造通过。

历史材料限定：CURRENT_STATE记录r3 `395fe13`离线1297与诊断衍生 `a24e921` 的特定运行场景；文件说明r3类覆盖事件6次、正向EdgeRetry形态0次，由裁定接受替代机制证据；CPS-4仍Pending、GO不通过。上述是仓内历史报告主张，本次未读取朋友电脑原始日志或复现实机，**不能作为2026-10-22冻结HEAD的实测，也不能把控制通路验收等同战术收益验收**。

## 建议主研究问题

值得研究的增量是：在统一可观测快照上，生成带角色/资源/时窗的少量可执行战术候选，组合地评估“创造机会者→利用机会者”的支援关系，比较当前计划与候选计划在同一新态势下的价值；只在收益覆盖切换代价、关键依赖仍能兑现时换计划，并用预期效果证据反馈。旧工程可提供朴素基线、几何估计和控制契约，不能靠新增日志字段补出该能力。
