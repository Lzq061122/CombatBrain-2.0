> **研究资产阅读视图**：原件位于 [CombatBrain_Phase2_Research_2026-10-22.zip](../archives/CombatBrain_Phase2_Research_2026-10-22.zip)，包内路径为 `README.md`。本视图只增加说明并调整指向省略文件的 Markdown 链接；代码保持原样。完整复现请在原 ZIP 的独立解压副本中运行。研究建议未自动获批。

# CombatBrain 第二阶段研究交付

先读 [第二阶段主报告](第二阶段主报告.md)。结论是：反馈消费、联合时间窗、实际释放和有限依赖候选值得接续；现有证据不支持把整套多阶段控制器替换第一阶段算法。游戏、DLL、C#构建均 **NOT_RUN**。

本包自包含 Python 原型、基线副本、输入生成器、完整结果、失败记录和校准工具，不依赖聊天、第一阶段目录或联网。研究只使用标准库；实际验证环境是 Python 3.13.7、Apple M1 Pro、macOS 26.2 arm64。建议 Python 3.10+，其他版本未实跑。源代码中冻结的游戏合同链接供后续接入追读，不是运行依赖。

## 运行

在解压后的本目录打开终端。以下命令会更新本目录中相应结果及计时，建议先保留原压缩包，或复制一份再运行。

```bash
python3 reproduce.py
python3 reproduce.py --full
```

默认快速检查运行6组单元测试、10项独立合同探针、实际时序桥接、12次动态smoke，以及校准示例和拒绝伪装游戏数据的检查。完整运行另重建搜索挑战、864次最终动态比较、192次消融、48次规模、48次远距离双端、模型/主动性诊断、32次近距离后验双端和失败轨迹。全部是合成/合同研究，不会运行游戏，也不访问生产仓库。日志和实际命令保存在 `evidence/reproduction.log` / `evidence/reproduction.json`；耗时依机器而变。

可以分别运行：

```bash
python3 prototype/run_baseline_challenges.py
python3 prototype/run_dynamic.py --suite final
python3 prototype/run_dynamic.py --suite ablations
python3 prototype/run_dynamic.py --suite scale
python3 prototype/run_dynamic.py --suite bilateral
python3 prototype/run_diagnostics.py
python3 prototype/run_temporal_execution.py
python3 prototype/run_followup_cases.py
python3 prototype/analyze_traces.py
python3 prototype/test_phase2_review.py --output results/review_final.json
python3 calibration/analyze.py calibration/example.csv --output calibration/example_analysis.json
```

`test_phase2_review.py` 必须显式指定上述输出，避免其开发期默认名覆盖 `review_initial.json`。`calibration/example.csv` 的497行全部为 SYNTHETIC；加 `--require-game` 应退出2。使用真实数据前读 [校准规格](calibration/schema.md)。来源字段不能替代录像/独立事件核验。

## 文件与证据索引

| 文件 | 用途与边界 |
|---|---|
| [主报告](第二阶段主报告.md) | 决策建议、失败结果、七项回答、生产差异合同和E1–E4否证实验 |
| [起点身份](evidence/start_identity.json) | 第一阶段文件/ZIP和CB2冻结提交；原路径是历史来源，不是运行依赖 |
| [原版planner](evidence/phase1_planner_original.py) / [基线修订说明](evidence/baseline_increment.md) | 原始副本逐字节保留；`prototype/baseline/` 修反馈消费、加可选closure，默认贪心不变 |
| [先验协议](evidence/protocol_before_results.md) / [冻结记录](evidence/frozen_for_holdout.json) | 开发、留出、适配纠错以及新种子启用顺序 |
| [适配审查](evidence/dynamic_adapter_review.md) | phase1提案器适配的范围；不等于原Runtime或生产CB2整体复现 |
| [独立审查](evidence/phase2_independent_review.md) | 初始缺陷、修复及10项探针；当前复现输出另见review_final |
| [执行环境合同](evidence/world_contract.md) | 合成World的输入、状态转移、观察边界和时间机制 |
| [方法来源](evidence/methods_phase2.md) / [下载记录](sources/methods/manifest.json) | 针对原始论文的可迁移机制；ZIP不重打包论文PDF |
| `prototype/temporal.py` | 联合窗口、DAG/资源合同、释放与局部关闭；手工计划桥接，不是通用自主规划器 |
| `prototype/controller.py` | 有限在线候选与阶段、主动位置/压力、保持/记忆；研究排序未校准 |
| `prototype/world.py` | 独立执行过程，不调用规划分数；仍是二维均匀能力简化 |
| `results/baseline_*.json` | 同候选、同价值模型的搜索正负例、候选域、规模、预算和反馈检查 |
| `results/final_*.json*` | 最终9300–9311，6模型×4对手×3方法×12初态=864次；最终新旧比较来源 |
| `results/ablations_*.json*` | 192次单机制消融；方法标签和参数在run_dynamic.py |
| `results/scale_*.json*` / `bilateral_*.json*` | 48次规模、48次远距离真实双端更新；很多等待，不能冒充战术优势 |
| `results/bilateral_close_*.json*` | 后验缩短初距32次；全部发生实际损伤，但不是确认留出 |
| `results/active_cases.json` / `model_challenges.json` | 定向主动正负例、96组固定候选独立结果挑战 |
| `results/selected_failure_traces.json` | 压力、退出、反击失败的完整逐拍轨迹 |
| `results/trace_behavior_analysis.json` | 仅代表性轨迹的ABA/切目标诊断，不能外推总体频率 |
| `results/temporal_execution.json` | 3个手工计划执行轨迹，39机制+27事实负例+2迟到检查 |
| `calibration/` | 四组游戏测量协议、CSV规范、分析器、合成示例和5项测试 |
| `evidence/reproduction.*` / `evidence/delivery_check.json` | 最终源码复跑日志、独立解压检查；时间不代表游戏性能 |
| `MANIFEST.sha256` | 包内文件完整性清单，自身除外；重新运行后结果哈希自然改变 |

## 哪些历史数据不能用于最终比较

`results/holdout_*`（7300–7311）存在phase1空target适配拒令；`results/confirm_*`（8300–8311）仍遗漏选前Duty、依赖执行门和近战probe可用性。这两批保留用于审计，均排除最终新旧优劣结论。`results/development_initial/`、`active_diagnostic_initial.json`、`review_initial/followup/accounting.json` 保留开发期问题；不要混进最终统计。`dev_*` 是开发样本。`reproduce.py --full` 不覆盖这些历史失败批次，也不会把已见种子重新包装成新留出。

最终phase1适配把第一阶段提案器输出接到相同World；单位、窗口、候选和风险准则与新方案不同。因此final是整策略比较，不能隔离“搜索算法贡献”。搜索比较只用baseline同口径实验。合同测试通过、合成执行有效和真实游戏收益是三类不同证据。

## 复现语义和完整性

同代码/种子的决策、执行状态和损伤应确定；`total_ms`、`episode_ms`、微计时、日志中的解释器绝对路径会随机器变化。置信区间仅描述构造初态的重采样；重复计时、每帧和同种子的不同模型不是独立战斗样本。

解压后、运行前可用标准库验证清单：

```bash
python3 - <<'PY'
import hashlib
from pathlib import Path
root = Path('.')
for line in (root / 'MANIFEST.sha256').read_text().splitlines():
    expected, name = line.split('  ', 1)
    assert hashlib.sha256((root / name).read_bytes()).hexdigest() == expected, name
print('MANIFEST PASS')
PY
```

第一阶段报告/ZIP未改；本轮没有修改生产源码、台账或P3，没有推送/PR。后续Agent先读报告第9–11节，按WP1→必要WP2→一个经测量的两阶段动作接续；不能将本包视为已验收游戏功能。
