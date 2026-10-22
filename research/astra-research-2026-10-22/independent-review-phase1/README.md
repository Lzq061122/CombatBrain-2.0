# 第一阶段独立复查记录说明

归档日期：2026-10-23；以下为本会话2026-10-22已完成复查的记录，**不是本次归档重新执行**。

原始研究包清单1098项匹配；在副本运行30项回归与五个入口，36例比较/消融/敏感性统计与原交付一致，纯计时与环境信息除外。保留的日志和`reproduction.json`提供执行线索。

## 两条主要追加线索

1. `authority_report_counterexample.json`：授权1接受的旧route_blocked报告，在授权改2或撤权后仍进入新快照；旧命令写入被拒不代表旧反馈已隔离。第二阶段对这条路径作了显式修复与回归，但其修订baseline另有“换计划清除失败记忆”的后续问题，见[第二阶段独立复查](../independent-review-phase2/CombatBrain_Phase2_Independent_Review_2026-10-22.md)。
2. `three_way_search.json`：追加三队协同、两个支援者的构造例，贪心约170.8818，beam/枚举约309.7450。**该早期文件只保留结果，没有完整初态与生成脚本，不能独立复现精确数值，不作为正式验收样本。** 第二阶段已提供可复现的同类高阶正负例：[输入与方法结果](../phase2/results/baseline_high_order.json)、[生成器](../phase2/prototype/run_baseline_challenges.py)。应以这些完整资产继续审查。

所有值均为抽象模型值，不是Bannerlord战损或胜率；单个反例不证明beam、闭包或贪心普遍更好。第一阶段交付内的自有审查与本目录中的外部复查也应区分。
