# CombatBrain 2.0｜Astra 两阶段研究资产与设计讨论入口

归档日期：2026-10-23。资料产生于2026-10-22。目标分支：`research/requirements-clarification-20261021`。

**这些资产用于继续需求澄清、学习和逐项选择。归档不等于采纳建议，不授权开发，不改变master、现行P3或历史验收。** 当前确认基线仍是[设计台账](../requirements-clarification-2026-10-21/CombatBrain-2.0-Confirmed-Design-Ledger-2026-10-22.md)，须读取其增量至§X。

## 在Chat模式继续

1. [新Chat接续入口](../requirements-clarification-2026-10-21/CombatBrain-2.0-Chat-Research-Handoff-2026-10-23.md)：说明读哪些文件、停在哪里、怎样逐项讨论。
2. [设计研究建议卡](../requirements-clarification-2026-10-21/CombatBrain-2.0-Astra-Design-Review-Cards-2026-10-23.md)：15项PROPOSED建议，关联旧决策、研究证据、反例、取舍和待讨论问题。
3. 原讨论进度仍为 **IF-FEEDBACK-02 → IF-EVAL-01 → IF-SAFE-01 → IF-TEST-01**，后续再处理相关任务语义、能力/数据、主动性、预算和发布细节。不要由旧交接中的D-EVAL-01开场倒退重问已确认架构。

本次没有代用户选择任何建议卡，也没有宣布澄清工作完成。

## 原始交付：完整保留

| 原包 | 内容 | 状态 |
|---|---|---|
| [CombatBrain_Research_2026-10-22.zip](archives/CombatBrain_Research_2026-10-22.zip) | 第一阶段主报告、原型、结果、证据和历史源码快照 | 字节不变；原清单1098项 |
| [CombatBrain_Phase2_Research_2026-10-22.zip](archives/CombatBrain_Phase2_Research_2026-10-22.zip) | 第二阶段报告、增量原型、动态实验、失败记录和校准工具 | 字节不变；原清单114项 |
| [CombatBrain_Phase2_Independent_Review_2026-10-22.zip](archives/CombatBrain_Phase2_Independent_Review_2026-10-22.zip) | 本会话对第二阶段的独立复跑、三项补查和复现脚本 | 字节不变；不含生产修复 |

## 不下载ZIP也能阅读

| 入口 | 用途 |
|---|---|
| [第一阶段主报告](phase1/主报告.md) | 条件机会、有限协同分配和当前计划比较 |
| [第一阶段原型](phase1/prototype/planner.py) | 核查实际算法，不能当生产实现 |
| [第一阶段独立复查说明](independent-review-phase1/README.md) | 旧反馈与三队互补复查的范围和证据限制 |
| [第二阶段主报告](phase2/第二阶段主报告.md) | 时间协同、主动与双边动态的增量和负结果 |
| [第二阶段在线控制器](phase2/prototype/controller.py) / [时间合同](phase2/prototype/temporal.py) | 两个尚未全部贯通的样机部分，不能混称通用规划器 |
| [第二阶段实际执行环境](phase2/prototype/world.py) | 合成状态转移，不是Bannerlord模拟器 |
| [实机采集与校准规格](phase2/calibration/schema.md) | E1–E4的待实测输入，示例全是合成数据 |
| [第二阶段独立复查](independent-review-phase2/CombatBrain_Phase2_Independent_Review_2026-10-22.md) | 88项单元回归、19条命令、25文件对比及新增R1/R2/R3 |
| [新增反例脚本](independent-review-phase2/probe_contract_gaps.py) / [输出](independent-review-phase2/contract_gap_results.json) | 换计划遗忘失败、退出资源被征用、Task版本凭证缺口 |
| [历史研究提示词](prompts/) | V1、V2、V2.1和第二阶段委托，仅作任务来源，不是当前开发派单 |

阅读视图保留代码原字节，Markdown只增加说明并将指向省略文件的链接映射到对应原ZIP。为避免重复存储，未展开第一阶段的六份历史源码树及第二阶段大体积逐拍原始轨迹；它们完整保存在原包。**完整复现应在原ZIP独立解压副本中执行，阅读视图不是另一个发布包。**

`asset-manifest.json`记录原包、各阅读文件的原路径/原SHA256、阅读版SHA256和变换说明。原报告的清单文件适用于原包，不能对增加说明的阅读视图直接声称全部相同。

## 已有证据与待证内容

- 第一阶段：既有复查记录为30项回归和五个运行入口，36例统计可复现；另有旧反馈及高阶互补反例。没有游戏收益证据。
- 第二阶段：独立副本完整复跑88项单元测试和19条命令；25个主要结果文件排除纯计时/机器信息后相同。新增3个合同探针仍暴露缺口，故不能称整个样机安全或全部通过。
- 最终864次动态比较中的staged为288次，其中216次双方无损伤、fix决策输出为0；不可把这些样本数当作联合牵制已经有效的证明。
- 两阶段都没有验证C#接入、真实游戏效果、Campaign/F6/军团/安全重编或万人性能。原型数值、TTL、保持罚分和合成净损伤不自动成为项目参数。

本目录仅归档并关联资料，未修复研究原型，也未更改项目确认状态。
