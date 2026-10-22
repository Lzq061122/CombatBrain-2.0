> **研究资产阅读视图**：原件位于 [CombatBrain_Research_2026-10-22.zip](../../archives/CombatBrain_Research_2026-10-22.zip)，包内路径为 `prototype/README.md`。本视图只增加说明并调整指向省略文件的 Markdown 链接；代码保持原样。完整复现请在原 ZIP 的独立解压副本中运行。研究建议未自动获批。

# 原型与复跑说明

这是独立 Python 战术决策样机：产生有限动作、联合分配单队/两队动作包、在同一新快照中比较当前计划、接收反馈后调整候选。没有游戏接口，不是战斗模拟器，也没有编译或修改 CB2 生产代码。完整解释及 C# 集成规格见 [主报告](../主报告.md) 与 [integration_spec](../evidence/integration_spec.md)。

## 环境与运行

实际执行环境：Python 3.13.7、macOS 26.2 arm64；仅使用 Python 标准库，无需 pip、网络、游戏或源仓库依赖。代码语法要求 Python 3.10+，其他 Python/操作系统未实测。先在终端进入解压后的 `CombatBrain_Research_2026-10-22` 文件夹，再逐条运行：

```bash
python3 -m unittest discover -s prototype -p 'test_*.py' -v
python3 prototype/run_experiments.py
python3 prototype/run_feedback.py
python3 prototype/cb2_baseline_probe.py
python3 prototype/test_method_review.py
```

本次原始执行目录：`/Users/tianxq/Documents/ChatGPT/新教程/CombatBrain_Research_2026-10-22`。脚本根据自身位置定位输入/输出，解压目录可变；不依赖原始绝对路径。后四条命令会覆盖本包的 `prototype/results/` 对应结果，实验脚本还会重写 `cases.json`，建议先复制一份目录。墙钟时间会随机器变化。

`unittest` 发现并运行 30 项断言。`test_method_review.py` 是另一个直接运行的研究探针，不是 unittest 用例；它退出 0 不表示所有假设成立，要阅读其输出的计数与反例。随机矩阵固定种子 1729–1764；这些输入参与研究迭代，不构成独立实战留出集。`cases.json` 是生成器输出的结构化用例；复跑通过固定种子和 `fixture()` 重建，不读取该 JSON 作为唯一数据源。

可选：复核随包冻结的 TCL 派生物和 CBATH 早期时间序列（只读源快照，写本研究 metadata）：

```bash
python3 metadata/verify_native_snapshots.py
```

它核验 12 个派生文件哈希、ABA 37/35 一致性和 373 点历史时间序列；不重新运行游戏，不能补齐缺失的 TCL run04–11 或 CBATH 最终 zt 原始记录。

## 文件和证据入口

| 文件 | 内容及结论范围 |
|---|---|
| `planner.py` | 类型、能力聚合、ETA、动作生成、约束/情景评价、成包贪心/beam/枚举、相对当前选择与有限 Runtime |
| `run_experiments.py` / `cases.json` | 固定矩阵、构造部署、敏感性、变形、切换和预算序列 |
| `run_feedback.py` | 手工提交 probe 后注入无反应/阻路反馈；两个独立冻结域的调用顺序检查 |
| `cb2_baseline_probe.py` | 冻结 CB2 `AssignGuardAndSupport` 的小段 Python 转写；只比较几何子问题，不是整套 CB2 比赛 |
| `test_core.py` / `test_review.py` | 30 项机制/边界断言，包括本轮发现缺陷的回归 |
| `test_method_review.py` | 独立模型调用计数、强保持反例、多支援最终窗口、牵制终点、已生效任务检查 |
| `results/summary.json` / `matrix.json` | 最终统计与逐种子比较/消融 |
| `results/traces.json` | 选中动作、拒绝原因计数、开始时刻、依赖、每情景效果与假设输入 |
| `results/feedback.json` / `sequences.json` / `budgets.json` | 反馈、重复评价与预算退化轨迹 |
| `results/metamorphic.json` / `sensitivity.json` | 枚举对称性及参数改变后的选择变化 |
| `results/tests_final.txt` / `method_review.json` | 最终回归与独立探针原始结果 |
| `../evidence/prototype_review*` / `results/method_review_initial.json` | 保留初始失败、中间版本反例和修复记录；不得当作最终源码结果 |

## 最小数据与调用合同

| Python 类型/入口 | 最小意义 | C# 候选映射（规格，未编译） |
|---|---|---|
| `Group` / `Formation` | 实际子群能力、人数、几何、速度、弹药、LOS、剩余持续时间、授权及实例代数 | 普通 `CapabilityGroup` / `FormationSnapshot` class/struct；由 Host 聚合真实成员 |
| `Snapshot` | 本场合法域、实际可用资源、Duty、时间窗、阻路/失败关系、已观察生效动作 | 不可变 `DecisionSnapshot`；字段按成员/授权/关系/模型版本失效 |
| `Duty` | 保护资产、当前承担者、已观察接任者、牺牲标识及职责状态 | `DutyRecord`，由 Commander/Ledger 管理，与 Task 生命周期分离 |
| `Action` / `Assessment` | 一队一个主动作、对象及身份；合法性、情景结果、窗口和依赖 | `TaskCandidate` / `ConditionalEffect`；不拥有写令权 |
| `choose(snapshot, config, current, method)` | 默认 `greedy`；也可 `beam` 或 `exhaustive`；返回选择、原因和工作计数 | 纯 Core Planner，输出候选过渡，由现有单一 Ledger 提交 |
| `Runtime.adopt/report/feedback_snapshot/write/safety` | 有限提交/报告/拒旧/效果放行/安全 fixture | Host + Formation + Ledger 的分工规格，不能直接当生产状态机 |

长度单位米、时间秒、分数为抽象能力代理。`known=False` 只增加估计不确定性；能力数值仍由输入显式提供，生产未知画像的降级值须由 adapter 负责。`major_change`、准备完成、牵制有效和职责状态也来自构造输入，本样机没有实现其真实感知。`active_effects` 只有观察已生效时才可填入，不能用命令被接受替代。

实验配置为 30 秒窗口、beam 宽 48、最近 2 个目标、最多 10 个可选动作；必要 Hold/Duty 额外保留。默认 `Config()` 的宽度/候选上限与实验脚本不同，复现主表应运行脚本。`Config.expansions=3000` 最终表示额外 `assessment()` 调用上限，名称为实现兼容保留；保障/当前复核另花 1–2 次，端到端候选生成及写前核验不包含在此计数中。枚举忽略此上限，只用于小问题参照。

默认按最差情景减启发式误差排序；转换成本默认每变更队 2 代理单位。它们都是研究参数。`retain_all_scenarios=True` 是更强保持门的可选消融，不是默认策略。`write()` 只返回 `accepted_interface_only` 等状态，代表预计时间与已观察依赖允许效果放行，不是 Move 准备动作或真实游戏命令。

## 已观察结果及局限

最终 36 例：成包贪心 36/36 达到本候选域的枚举参照，beam 34/36；平均全部模型调用分别 278.00、385.39。此小规模下枚举平均约 252.61 次，不能证明搜索更省成本。去依赖检查没有改变这批随机输入的选择；切换成本消融也没有观察到稳定性收益。参数敏感性 90 次中有 25 次选择变化。

这些数据证明所列模型上的机制和搜索行为，不证明真实战斗模型正确。固定种子只是复现工具。三方法合计计时不等于单算法帧预算；完整枚举的变形性质不外推到有预算的贪心/beam。双端冻结评价不构成互相移动/反应的联合仿真；手工选 probe 后的反馈链不证明僵局会自动解决。

原型只表达 `Fix→Strike` 两层依赖；硬依赖要求一支足够强的 fixer，未支持多支弱队合成满足；一队在窗口内只执行一个主任务。实际路径、射界、脱离/转向成本、能力画像、效用校准、完整任务阶段、Native 保持/交还、Campaign/F6、重编和游戏收益均未完成。主报告第 7–8 节给出可推翻假设的最小实机验证与开发接续。
