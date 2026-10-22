> **研究资产阅读视图**：原件位于 [CombatBrain_Phase2_Research_2026-10-22.zip](../../archives/CombatBrain_Phase2_Research_2026-10-22.zip)，包内路径为 `evidence/methods_phase2.md`。本视图只增加说明并调整指向省略文件的 Markdown 链接；代码保持原样。完整复现请在原 ZIP 的独立解压副本中运行。研究建议未自动获批。

# 第二轮定向方法研究与校准决定

检索/读取日期：2026-10-22。基线沿用第一轮冻结 CB2 `dd28965238a83c29ae824f7225d90e3b2c8bfce4`，本轮没有重新审查四库，也不修改 production/P3/正式 Commander 权威。只增补四个会改变设计边界的原始来源，不重做综述；不从演示推断商业游戏内部。游戏实测 **NOT_RUN**。

## 1. 本轮设计建议（研究推论，不是论文已经证明 CB2 有效）

阶段的最小单位应是“可观察先决条件 + 明确占用/释放 + 有界利用窗口 + 失效后退出”。先实现两阶段：移动/牵制形成条件，观察成立后利用；每 Formation 只有一个正式 owner。将来阶段是承诺/预约，不能在前一任务未实际释放时借用。多队 fix 的门槛在同一重叠时间片内合计，预测覆盖整段利用窗口只是候选可行性；实际执行仍需持续观察、过期后取消或修补后继。图上的时间一致性和当下 effective 不能保证效果在未来持续。

模型误差与对手反应应独立命名、交叉验证：同一地形/通路造成多个 ETA 同向偏差，是共同误差因子；敌军留守、支援、反制是有资源与行程约束的策略分支。不要把多个敌方支援都加到每个目标上，也不要把“反制分支”当 ETA 校准误差。未知概率时均值只是一项研究权重，区间只是假设范围。比较名义选择、有限保守选择与无概率最差分支时，明确其成本与尾部行为，不把风险指标当作胜利目标。

主动候选至少区分：①改变位置/火力角度带来的直接效果；②由一个具体后续动作兑现的机会；③观察改变未来选择带来的信息价值。不能把一个物理效果在三栏重复加分。若所有可观察分支都选相同下一动作，则该策略的纯信息价值为零。分支不同也仅是必要条件：信号到达前错过窗口、反应可伪装、退出成本过高时仍不值得施压。未标定反应/信号概率前，先以分歧检查、可回退路径与累计成本限额筛候选，不声称算出了期望 VOI。

终点价值采用可审计的短后续候选：记录需要哪些资源、何时释放、从终点到后续任务的转移/准备成本、最早失效条件。滚动重算时不把同一尚未兑现的机会当作新收益累加；若持续没有位置/条件进展，重新判断它是否仍可达到。当前计划比较排除沉没成本，保留尚未付出的脱离、交接、转移与准备成本。以上是有限原型规则，不是稳定性或最优性定理。

## 2. 四个选型来源

### S1：时间预测可行不等于在线可执行

Morris、Muscettola、Vidal，*Dynamic Control of Plans with Temporal Uncertainty*，2001；[NASA 原文入口](https://ntrs.nasa.gov/citations/20010091013) / [6 页作者报告](https://ntrs.nasa.gov/api/citations/20010091013/downloads/20010091013.pdf)。检索串 `Morris Muscettola Vidal 2001 Dynamic Control Plans Temporal Uncertainty pdf`，后定向 `site.ntrs.nasa.gov`。读取原文 §1–2、controllability 定义、§3–5 的等待约束讨论与 §6 执行段；没有逐条复核其算法完备性证明。NASA 页面日期为 2001-01-30，网页显示 Public / Work of the US Gov. Public Use Permitted；本地下载 403，正文由 web 的 PDF 全文接口读取，未伪造本地文件。

机制：STNU 区分可控制时刻与受外界控制的 contingent 终点；动态策略只能依赖已发生观察。其独立持续时间范围、传播算法和形式保证不能直接搬到战斗资源/效果模型。CB2 缺口是 ETA 到点自动等同准备完成。采用观察门控和执行失败路径；暂不引入完整 STNU 求解器。代价是额外观测、过期和后继检查。最小否证：相同预测计划，让实际支援晚到/早停，确认无未观察利用、未释放复用；再测等待造成的真实错窗成本。

### S2：共同模型误差与“所有坏事一起发生”的代价

Bertsimas、Sim，*The Price of Robustness*，Operations Research 52(1)，2004，35–53；[作者 MIT 全文](https://www.mit.edu/~dbertsim/papers/melvyn/The-Price-Of-Robustness-OR52.pdf)，[出版页](https://pubsonline.informs.org/doi/10.1287/opre.1030.0065)。检索串 `site.web.mit.edu dbertsim price robustness pdf 2004`。读取 PDF pp.1–5 的问题/保护预算/概率假设及 p.9 §5 Correlated Data；未复核附录所有界。版权 INFORMS，未使用其代码。

机制：控制保护程度以展示保守性的代价；相关不确定参数可由共同因子表示。概率界依赖明确的独立、对称、有界因子假设。CB2 应把同一路段多 Formation 的 ETA 偏差绑定到共享因子，报告保护程度变化，不把手填区间称覆盖率保证。论文线性鲁棒优化和概率界不直接适用战斗。代价是更多一致情景与校准。最小否证：加入共同路径延迟、系统性 fix 高估，并留出未见误差；检查新增阶段是否因模型共偏差全失效。采用共同因子测试；不采用未经验证的概率保证或任意 Γ“置信度”。

### S3：信息要在决策之前可得，且要改变有用选择

Ross D. Shachter，*Efficient Value of Information Computation*，1999；[作者 Stanford 全文](https://web.stanford.edu/~shachter/pubs/valinfo.pdf)，[作者出版目录](https://www-leland.stanford.edu/~shachter/publications.html) 将该文列为 1999。检索串 `site.ai.stanford.edu Shachter value information efficient 1999 pdf`。8 页全文文本读完，重点 §2 信息时序、§4 requisite observations、§5 重用计算；未独立证明 cluster-tree 算法。arXiv 2013 是后续上传，不能写成原创发表年。未发现此 PDF 对应代码许可，不复制实现。

机制：比较信息在决策前/后可得时的最优期望效用；不相关观察可不改变决策。CB2 的当前部署虽已知，仍可能未知敌方反应/效果；压力又会改变世界，不能直接当免费 clairvoyance。采用“同物理压力、有/无该信号”的独立对照；观察后最优动作相同则该策略信息收益为零，是此决策定义下的推论。代价是信号模型、分支后续动作和实际压力成本。最小否证：无回应/伪回应/来不及用，以及分支全同动作；观察组若仅更多换令但独立终点无改善，不支持信息机制。暂不引入影响图求解器。

### S4：终点价值需要可行后续，旧解需要重新检验

Rawlings、Mayne、Diehl，*Model Predictive Control: Theory, Computation, and Design*，2nd edition，作者站点第 6 次印刷 PDF；[作者页面](https://sites.chemengr.ucsb.edu/~jbraw/mpc/) / [全文](https://sites.chemengr.ucsb.edu/~jbraw/mpc/MPC-book-2nd-edition-6th-printing.pdf)。检索串 `site.sites.engineering.ucsb.edu rawlings model predictive control terminal pdf book`。只读 §2.3、§2.4 的终端可行/下降条件及 §2.7 suboptimal MPC、§2.11 终端项讨论（PDF pp.157–169、197–198、215）；821 页未全读。作者站版权为 Nob Hill Publishing；未复制书中代码，未将代码下载链接当开源许可。

机制：有限窗终端代价/集合连接可行后续；次优控制可使用平移后的可行旧序列作候选；扰动会破坏名义递归可行性。CB2 不能由添加“未来机会分”就得到稳定保证，也不能把旧计划未经当前观察复核当 incumbent。采用短、具体的后续可行性核查与剩余成本重算。代价是预测终点状态和转移检查。最小否证：两处位置机会相互吸引导致反复重定位、支援迟到破坏后续、预算 0 时旧计划失效。暂不采用 MPC 稳定性结论；战斗非平稳、模型误差与收益定义不满足书中前提。

## 3. 校准交付与解释边界

`calibration/schema.md` 给出四类配对实机规格，`analyze.py` 是标准库长表分析器，`make_example.py` 生成 497 行明确 SYNTHETIC 数据，`example_analysis.json` 是其描述统计。按配置、Vanilla/RBM、士气模式、侧位、时窗和证据来源分 cohort；右删失不填 0，ETA 给独立 block 最坏误差与真实 slack；FIX 分开有限窗支援延迟和双方死亡/溃退；压力分开信号用途与整套压力成本；多阶段检查硬合同失败与修补副作用。

2026-10-22 验证：`python3 -m unittest -v test_analyzer.py` 5 项通过，覆盖合成标签、删失、非有限数/重复拒绝、无匹配物理前缀不做信息配对、RBM/士气分组。`python3 analyze.py example.csv --output example_analysis.json` 输出 4 个 SYNTHETIC cohort，GAME evidence=false。这只验证分析器，不是战斗证据。脚本不产生胜率、显著性或置信区间；需要先 pilot 再按实际方差与实用差值设计确认实验。

## 4. 来源保存与尚未采用的路线

`sources/methods/manifest.json` 记录 3 个下载成功 PDF 的 URL、大小与 SHA256，另保留 NASA 403；`.selected.txt` 仅为阅读提取。NASA 在 web 全文可读，不等同本地下载成功。先搜 Howard 1966，未取得可靠作者/出版方全文，因此没有用搜索摘要支撑设计，改读 Shachter 原文。Mayne 2000 检索只找到摘要/书目，终点机制改以作者公开教材原节支撑，不伪称读完该原论文。

不采用完整 POMDP/影响图/STNU/MPC 工具栈：本轮重点是可观察依赖、资源守恒、模型与执行分离以及可否证校准。已有四库接口和游戏 Authority 仍优先；外部算法名称不能增加新的正式控制权，也不能越过 P3 边界。
