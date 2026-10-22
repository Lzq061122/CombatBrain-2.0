> **研究资产阅读视图**：原件位于 [CombatBrain_Phase2_Research_2026-10-22.zip](../../archives/CombatBrain_Phase2_Research_2026-10-22.zip)，包内路径为 `calibration/schema.md`。本视图只增加说明并调整指向省略文件的 Markdown 链接；代码保持原样。完整复现请在原 ZIP 的独立解压副本中运行。研究建议未自动获批。

# 最小可否证实机校准规格（本轮 GAME = NOT_RUN）

本目录不连接 Bannerlord，不加载 DLL。`example.csv` 的全部行都是 **SYNTHETIC**，由 `make_example.py` 手工规律生成，只检验分析器。它不是微型战斗环境，也不是本轮原型结果。本规格针对冻结 CB2 `dd28965238a83c29ae824f7225d90e3b2c8bfce4`；以后改变构建必须新建 cohort。

运行：

```bash
cd /Users/tianxq/Documents/ChatGPT/新教程/CombatBrain_Phase2_Research_2026-10-22/calibration
python3 make_example.py
python3 -m unittest -v test_analyzer.py
python3 analyze.py example.csv --output example_analysis.json
python3 analyze.py real_observations.csv --require-game --output game_analysis.json
```

最后一条仅适用于将来采集的真实数据。分析器会拒绝将合成示例简单改成 GAME。它不能证明填写的来源真实；视频/独立物理事件采集与人工抽查仍不可少。

## 1. 实验与否证条件

先锁定协议、主要终点和有实际意义的最小差值，再采 pilot。每个独立 block 是一次初态/随机种子/侧位的配对试验；同一回放中的多 Formation、多 tick、同一 seed 的多指标不是独立重复。按 block 随机化各 arm 执行顺序，轮换侧位另分 cohort。至少覆盖无遮挡、绕路/窄口、LOS 受阻、职责交接四种**预先列出的条件**，不在结果好看后选择地图。能否复现 seed、初态不受上一轮残局影响，要先检查。

| 实验 | 最少配对 arms | 独立测量与否证 |
|---|---|---|
| ETA/窗口 | forecast；同一配置重复 | 发令→抵达有效位置、抵达→形成有效攻击/射线、取消→实际脱离并可复用；预测点与区间必须发令前冻结。按 Formation 视频/位置与攻击事件标注，不读 `FixSuccess`。若相关延迟常大于利用窗口剩余 slack，短窗协同不可依赖现估计。失联/未到达保留右删失。 |
| 联合 fix | none、single_a、single_b、joint、same_resource_alt | 完整友方 roster 在五组相同，未牵制部队位置/动作预先固定。观察被牵制敌方对另一区域**实际支援抵达时间**、实际伤亡与职责空窗；同资源替代组让 A+B 执行预先固定的直接打击或保护任务。若 joint 不能延迟支援，或延迟却全局伤亡更差，联合能力阈值/价值模型被否证。单看敌方朝向变化不算牵制成功。 |
| 压力的信息价值 | observe、blind、no_pressure | observe 与 blind 在观察前执行同样压力和退出前缀；blind 仅屏蔽这一次测试信号，保留正常合法当前观察，预先承诺后续基线动作。以同一终点的损失向量比较；`physical_prefix_id` 对应经过核验的观察前事件轨迹/协议，不能仅填相同 seed。no_pressure 检查压力总成本。若观察不改变可执行后续选择，当前策略信息价值为零；若改变但独立终点不改善，也不支持正价值。 |
| 多阶段/取消 | staged vs single_window；local_repair vs full_replan | 同候选/估值/预算，观察实际释放、二阶段发令与生效时刻、职责覆盖、失效到取消延迟、资源重叠、旧作用域写入与 ABA 反转。保留敌方提前退出、支援未生效、目标死亡、转移路径阻塞、接替者迟到。任何提前借用未释放 Formation、硬失效后仍利用旧效果或越权写令，都是合同失败；少取消不自动代表好。 |

压力实验须含无回应、晚回应、佯动后回撤与直接反制；观察分支的标签由物理事实/预先阈值确定，不能直接用敌方隐藏 policy 名称。压力自身死伤、耗时、弹药和退出损失须留日志；本最小 CSV 仅纳入损失/耗时/退出延迟，完整弹药/暴露轨迹放 `evidence_ref`。一局内反复压力的累计成本不能用最后一次成本代替。

完整所有 arms 的单位、装备、阵型、出生位置、地图/天气、难度、伤害倍率、AI控制方式、MOD版本及顺序均写入 `protocol_id` 指向的冻结配置；CSV 存关键身份和配置 hash。Vanilla/RBM、普通士气/不溃逃死斗分开采集和报告。死亡与溃退分别计数；不得把撤离地图或失踪直接当死亡。胜负可另做后续验证，本最小校准不由短窗杀伤推算胜率。

采样不是“打 N 局达到 X%”。Pilot 先检验事件是否能独立测到，再用独立 block 的配对差方差和预设最小实际差值规划确认样本；若使用均值与近似正态设计，先检查偏态/重尾，不能从每 tick 大样本得到虚假精度。极端尾部、删失率高、seed 不可复现时应扩大覆盖并改设计。确认集冻结阈值后单独运行；每次结果驱动修模需新留出集。脚本只给描述统计，不输出 p 值、置信区间或“通过率门槛”。

## 2. 长表 CSV

UTF-8，一行一个 run/entity/metric 观测。列名见 `analyze.FIELDS`；空值仅允许 `physical_prefix_id`（非压力配对）与四个预测/截止字段（非 ETA）。数字必须有限、非负；缺失记录不填 0。用新 block 表示新的独立重复，同一 block/arm 只能一个 run。

| 列 | 含义 |
|---|---|
| schema_version / evidence_kind | `1`；`GAME` 或 `SYNTHETIC`，不同种类永不混池 |
| experiment / protocol_id | `ETA/FIX/PRESSURE/STAGES`；冻结配置与主要终点协议 ID |
| block_id / run_id / arm | 配对初态重复、一次完整运行、干预组；具体可用 arm 见上表/脚本 |
| game_build / cb_commit | 精确游戏构建；40 位完整 CB2 SHA |
| mod_profile / scene | Vanilla 或 RBM 的版本/加载顺序配置 hash；地图与环境配置 ID |
| roster_hash / equipment_hash | 包含完整人数、兵种、阵型、初态位置，以及装备/技能的冻结清单 hash；不同 arm 不得少派部队 |
| opponent_policy / morale / side / seed | 实验控制策略 ID（只做分组，禁止给规划器当未来信息）；`normal/deathmatch`；侧位；种子 |
| horizon_s | 从共同干预 t0 起算的固定终点，所有 arm 一致；不随完成快慢延长 |
| measurement_source / evidence_ref | GAME 必须 `independent_capture`；视频/物理事件采集的可核验路径或记录 ID，不能只指规划器估值日志 |
| physical_prefix_id | 压力观察/屏蔽组的共同观察前物理前缀证据 ID；缺失或不一致时信息对比被排除，不能补写碰巧相同名字 |
| metric / entity_id / value / unit | 下表指标；ETA 为 actor 稳定 ID，其他为 `run`；数值或文本；`s/ms/count/text` |
| status | `OBSERVED` 或 `RIGHT_CENSORED`；后者 value 为已等待到但事件仍未发生的时间 |
| pred_lo_s / pred_hi_s / pred_point_s | ETA 发令前冻结的预测；只是预测带，未经校准不称置信区间 |
| deadline_s | 对应事件必须发生的最晚时间，可空；与 value 同一时间原点，ready_at 从共同 t0 起，duration 从该动作实际开始起 |

ETA 指标：`travel_s`、`setup_s`、`exit_s`、`ready_at_s`。若 formation 同一 run 里有多段，可用 actor_id:stage_id 作 entity；不能把同一回放当多次独立样本。窗口指标应优先用 ready_at_s，避免将 travel 和 setup 两个误差独立抽样而抹掉共同路径/地形误差。对已删失事件不算点误差/覆盖分母，但删失下限已经超出预测上界或 deadline 时记录“已证实漏覆盖/错窗”；不能把该时间当真实完成时间。

FIX 指标：`support_arrival_s`、四种终点死亡/溃退计数、`duty_gap_s`。support 未抵达必须在共同 horizon 处右删失；脚本报告 `min(实际抵达时间, horizon)`，这是有限窗支援延迟，不是永久阻断或无穷到达时间。交互量 `joint − single_a − single_b + none` 仅针对这个受限抵达时间；非线性、删失饱和和双方适应都会影响它，不能直接命名“协同战力”。损失向量与同资源替代组决定是否值得。

PRESSURE 指标：四种终点计数、`pressure_own_loss`、`pressure_time_s`、`exit_delay_s`，文本 `observation/action_before/action_after`。动作应完整编码可执行 actor–kind–target/phase，不能只写“attack”漏掉改目标。动作变化数只描述反应性，不能当奖励或信息价值。输出 observe−blind 才有机会隔离信号用途，observe−no_pressure 是压力整套行为的效果；两者不能混称。

STAGES 指标：四种终点计数、`planned_stages/completed_stages/cancelled_stages`、`duty_gap_s/resource_overlap_s/premature_starts/cancel_latency_s/stale_writes/replan_time_ms/aba_reversals`。`cancel_latency_s` 从独立失效事实可观察时到命令撤回生效；`resource_overlap_s` 是同一 Formation 实际被并发占用的时长；阶段发令与释放原始事件必须保留在 evidence_ref，不能只交汇总。

## 3. 输出解释与尚未覆盖

每个 cohort 的配置、模式、侧位、时窗、来源严格分开。所有数值差均为 left−right，符号不自动等于好坏。ETA 给点误差、实际 slack、右删失、按 block 的最坏误差；同回放全部偏晚是相关误差警讯，不是估计了相关结构。FIX 的 delay 与伤亡分别呈现。缺 arm/缺 metric 的配对明确列入 excluded；不得只报有完整好结果的部分。

最小脚本不做视频自动标注、因果识别、样本量自动计算、胜率外推、独立性证明或游戏真实性认证。校准数据还不能替代实战确认。统计置信区间若之后需要，必须按独立 block 重采样并先处理重复 seed、删失和多重试验；不在此处添加看似精确的区间。
