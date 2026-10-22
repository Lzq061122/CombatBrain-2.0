> **研究资产阅读视图**：原件位于 [CombatBrain_Phase2_Research_2026-10-22.zip](../../archives/CombatBrain_Phase2_Research_2026-10-22.zip)，包内路径为 `evidence/dynamic_adapter_review.md`。本视图只增加说明并调整指向省略文件的 Markdown 链接；代码保持原样。完整复现请在原 ZIP 的独立解压副本中运行。研究建议未自动获批。

# 动态 Phase1 适配快审

本次限定只读审查 `choose_phase1 → Controller.decide → World.issue`，未改 root 代码，未新增研究情境或运行整批动态实验。审查时 `controller.py` SHA-256 为 `bc63cb02d4a76c9fe18085bda7ebf3a11c9b44411563ee54cae13eca26c5e0e0`，`world.py` 为 `6e325c8e9e0168b4fa7475d566081055918fccf1e04ee1ec719360c887361d3f`；依赖的 baseline planner 为 `efe0b280acfa503e9cbf7a58a89430fca877dd3418e621eb20e7a04dad51a806`。

## 发现与已完成修复

除主作者已发现的 Duty 未传入选择、assessment 依赖未进入观察 gate，本次另发现纯近战 probe 的语义不匹配：Phase1 给 probe 的输出为 `max(ranged, .35*melee)`，World 的 pressure 却停在远程距离并只产生 ranged 输出。因此直接将近战 probe 翻译为 pressure 可让一个预计有收益的动作实际零输出，且 staged 本来只生成远程 pressure，构成不公平映射。

主作者已在 `controller.py:178` 选前候选适配中过滤没有 ranged 能力的 probe，保留原 baseline 源码及其评分。当前代码也已将 Duty 传入 Snapshot、将 assessment.dependencies 传入共同执行 gate，并删除 hold 的空 target。复跑 `python3 -m unittest discover -s prototype -p test_adapter.py -v`：2/2 PASS。另对一支纯近战与远处远程敌队的最小输入，当前适配返回 hold，没有错误 pressure。该快速检查不构成全部依赖、Duty 和动态表现的完整验收。

临时替换模块级 `p.options` 带 `finally` 恢复，在当前串行脚本中可用；不适合并发或生产。移植应改成显式候选 provider 参数。

## 比较时必须声明的三个边界

1. **名称应为“Phase1 proposal 适配基线”。** 它调用旧选择器，再使用本轮 Controller 的 commitments、共同效果 gate 和 World 执行，不是旧 Runtime 的完整动态复现。旧 `adopt/report/feedback_snapshot/write/safety` 没有接入；当前 `current_phase1` 是上次选择的 proposal，并非已签收且生效的完整状态。`Controller.update` 虽产生 failed、model_scale、exits，这些信息没有全部回流给 `p.choose`：旧反馈消费修复的 13 个测试不能据此声称动态实验覆盖了旧 Runtime 闭环，也不能把 proposal 适配较差归因为原贪心搜索本身。

2. **不是同模型消融。** Phase1 适配将 hp 四舍五入作人数、melee/ranged 除以 10 作能力，采用继承的 30 秒窗口、最坏反应下界与误差带；新 Controller 使用另一套 16 秒预测、默认反应平均、不同射程/展开/牵制假设与主动候选。World 同时给两者相同状态、延迟和结果过程，这支持比较两个完整适配策略在该抽象环境中的表现，不能把差异单独归于多阶段、闭包或搜索深度。机制归因应优先引用新 Controller 内单因素消融和独立同模型基线实验。当前大量主样本 max_hp 固定为 100，能力缩放不能外推到任意兵力/单位定义。

3. **相同 budget 数字不是相同 CPU 预算。** Phase1 budget 计搜索 assessment 调用，必做 fallback/current 另列；新 Controller budget 计 forecast 次数，候选构造、约束、反馈、排序等工作不同。应保留逐方法端到端决策时间和 work 字段，不能称公平同毫秒预算竞赛。旧回合动态结果只能按对应源码身份解释；主作者已决定原 holdout/confirm 保留为开发与失败记录，最终新旧比较用适配修复后的新 9300–9311 输入，不能把旧批次合并抹去修复前偏差。

在上述范围说明下，本次未发现需要另行扩大修复的适配阻断项；这不是对 World、预测模型或动态研究全部逻辑的重新认证。
