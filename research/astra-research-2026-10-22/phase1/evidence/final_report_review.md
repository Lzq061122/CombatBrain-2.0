> **研究资产阅读视图**：原件位于 [CombatBrain_Research_2026-10-22.zip](../../archives/CombatBrain_Research_2026-10-22.zip)，包内路径为 `evidence/final_report_review.md`。本视图只增加说明并调整指向省略文件的 Markdown 链接；代码保持原样。完整复现请在原 ZIP 的独立解压副本中运行。研究建议未自动获批。

# 主报告独立交付核对

检查日期：2026-10-22。范围仅为仓库身份、TCL/CBATH证据层级、CB2集成合同与两份附录的一致性、主报告相对Markdown链接。未扩展研究、未修改主报告、未重跑原型或游戏，不把此检查当生产/战术收益验收。

**结论：在本次限定范围内，未发现会改变推荐或证据等级的错误。**

- 主报告六行仓库/分支/SHA均与本地冻结元数据一致；没有把TCL研究提交当测量提交。
- TCL局部Movement/HOLD窗口、G3失败、原始run04–11缺失及本轮12份派生物/ABA复核范围，和`native_audit.md`一致；没有把局部Hook/保持结论外推全域或战术收益。
- CBATH冻结最终3/3保持DOC/转述，较早373点时间序列单列；1Hz、900点、空队与目标ID缺口一致；整队AI工具未冒充Campaign/部分F6授权证明。
- 主报告§5与`integration_spec.md`一致：Commander/PlanLedger单一提交、Task取消不解除Duty、效果确认后交接、完整身份/版本、日志旁路、本地安全/牺牲优先、非事务重编和P3未批准变更边界均保留。
- 主报告明确新C#接口为候选规格，Runtime.write为接口fixture，实际引擎/危险识别/Campaign仍未验证；未发现把附录规格写成已实现的结论。

## 链接检查

- `evidence/legacy_audit.md`：存在。
- `evidence/native_audit.md`：存在。
- `evidence/integration_spec.md`：存在。
- `evidence/external_research.md`：存在。
- `evidence/evaluation_protocol.md`：存在。
- `prototype/results/cb2_baseline_probe.json`：存在。
- `evidence/prototype_review.md`：存在。
- `evidence/method_validity_review.md`：存在。
- `prototype/README.md`：本次检查时尚不存在。

`prototype/README.md`由主研究Agent明确正在创建；若上述记录为缺失，这是交付打包前待闭合项，不是研究结论错误。交付前应确认其存在并随包保留相同相对目录。其余主报告相对Markdown链接本次均可解析。

## 本次核对的文件身份

- `主报告.md` SHA256 `4439897f3910322f8de933a5f94009d07562d831efc81c501950e479ead55d15`
- `evidence/native_audit.md` SHA256 `9718c5f73b61fa169ced542d85eb6868a402100b580845183f641a085873617c`
- `evidence/integration_spec.md` SHA256 `d6196401257a905eaa537e37a951134b9444dfc08e414efba5d72cfe4281f6f5`

## 最终交付前闭合复核

2026-10-22再次只读检查：主报告全部 **9个相对Markdown链接均存在**，`prototype/README.md`缺失项已闭合。主报告末尾交付说明已更新为包含四库六份冻结源码快照；外部PDF/RBM工作缓存单独本机保留。此次不扩展研究，也不重跑原型。

当前主报告 SHA256：`4697b8c0a117df822fecf3950d8b1b1b4af4b723912dca8ce35e6856b02aacf8`。
