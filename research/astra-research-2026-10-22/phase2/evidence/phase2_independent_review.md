> **研究资产阅读视图**：原件位于 [CombatBrain_Phase2_Research_2026-10-22.zip](../../archives/CombatBrain_Phase2_Research_2026-10-22.zip)，包内路径为 `evidence/phase2_independent_review.md`。本视图只增加说明并调整指向省略文件的 Markdown 链接；代码保持原样。完整复现请在原 ZIP 的独立解压副本中运行。研究建议未自动获批。

# 第二轮独立合同与方法审查

日期 2026-10-22。审查者没有修改 `temporal.py/controller.py/world.py`；自有反例为 `prototype/test_phase2_review.py`。下面区分静态发现、实际运行记录、算法简化和游戏证据；所有测试均为 Python 合同探针，**不是战斗/游戏实测**。

## 实际记录与阻断点

首次脚本运行前，主作者已经修复同 actor 隐式释放门、同动作新增依赖、helper 换用途核查和联合贡献去重。本次首次执行 `results/review_initial.json` 已看到这些修复，不能倒写为本审查“先红后绿”。该文件记录当时源码 SHA。随后 `review_followup.json` 增加取消后复用和 setup 剩余成本探针，`review_accounting.json` 增加切换记账反例；旧文件保留，不覆盖历史证据。

实际检出并由主作者修复的问题：

1. **取消不等于释放。** 一个合法计划中同 actor first=0..3、second=3..9，没有显式 after。正常路径会等待 first 观察释放；但 `invalidate(first)` 把 first 从图删除后，t4 立即开放 second。实际任务可能仍在脱离。失效闭包不能删除尚占用资源的物理事实；需保留待释放占用记录或阻塞同 actor 后继，直至真正 release。
2. **两轮选择的切换成本参照不同。** 受控估值 base=(a strike,b hold) 得10，both strike得20，(a hold,b strike)得20.5。第一轮选20−0.8=19.2；第二轮只扣本次0.8，错误接受20.5，但相对同一 base 应扣双切换1.6=18.9。这不是贪心搜索局限，是比较量不一致，污染 keeping 消融。反例没有冒充真实作战效果。
3. **停止进展从初始点累计。** move 已前进10m后，在 t99、t100 都静止且无输出，仍因“距初始点>3m”保持承诺。历史曾进展不代表最近仍进展；需最近进展时间/窗口或明确限制其只是初期起步检查。
4. **负预算切片。** `budget=-1` 被 Python 当作“除尾项外全部”，实际调用2次估值。配置应拒绝负数或钳制为0。正常非负预算在已有日志中是额外评估上限，生成/排序/基础复核不包含在该上限内。

另外两项应区别处理：早期 `observed_ready` 对已经被 validator 拒绝的 missing-contributor 计划仍抛 KeyError，是输入健壮性，不是合法计划绕过约束；最终已改成返回 False。旧 `acknowledge_release(id, True)` 不带代际/authority/revision仍能释放新作用域任务，最终已经改为完整作用域字典并拒绝 Boolean。

冻结后复跑原探针，`results/review_final.json` 记录最终运行源码 SHA：十项全部为 SAFE，包括无效贡献输入、取消后复用、完整 release 凭证、同动作新增依赖、helper 换用途、负预算、滚动停滞、warm setup 和记账同参照。取消修复保留已 dispatched 任务的待释放占用，同时关闭同 actor 隐式后继；正式迁移仍须把 `mark_dispatched` 与实际发令绑定。carryover 任务原 dispatch revision 的 release 延续由主作者合同测试覆盖，本审查没有添加额外范围或将其冒充本脚本独立复验。

## 模型/证据局限

早期探针中 warm fire（已观察 attacking、current_order=fire）和 cold fire（当前 hold，首次请求 fire）被赋予相同价值；最终复验已区分已准备同命令，不再重复支付 setup。这个修复仍不能声称完整处理未来剩余准备/退出/交接成本。`.8 × switch` 是研究正则项，不是实测损失；最终两轮选择已统一相对同一个 base 的全计划调整值。

早期 pressure 超成本仅 fallback hold；最终静态代码已加入回到尝试起点的 retreat 与退出中占用，累计损失也改为按 actor 跨尝试计。以上退出改动尚未由本审查脚本独立验证可行出口，且设计本就不保证出口可行；world 的迟滞/阻挡仍可能让撤离失败。独立 world 的全程损失向量比内部压力计数更适合作为主要终点，不把停止发 pressure 等同脱离接触成功。

通用 temporal 合同样机与 dynamic controller 仍是不同层次：前者检验小 DAG/资源预约/联合窗口，后者只执行 move→fire、fix条件→利用等子集。动态循环不能自动验证通用 DAG 的所有合同；直接 unit tests 也不能替代双边执行。观察到的 suppression 是当前事实；预测 valid_until 不能变成整个未来窗口已验证的保证。

forecast 三分支并非对手概率模型：无概率平均只能是声明的研究权重。资源守恒有意义，但估值与 world 仍共享抽象几何/能力假设，world 不是独立的 Bannerlord 验证。固定时窗实际损失优于仅比较 planner score，仍不能推算游戏胜率。主动 advance_fire 的定向正例证明该候选能产生位置→攻击的物理链；counter 负例、withdraw 未兑现与无收益随机组必须一并保留。

公平比较须分开同候选搜索、表示、主动候选、估值和执行保持。预算是评估调用预算；候选生成、约束检查、基础当前计划和依赖检查、最终构造、反馈成本都另报。一个三分支 full forecast 与旧单窗 partial assessment 成本不同，次数相同不等于相同端到端资源。空间稳定枚举可消除任意数组顺序的影响，但截断候选导致的方向/几何偏置依然需要限制说明。

## 复现

```bash
cd /Users/tianxq/Documents/ChatGPT/新教程/CombatBrain_Phase2_Research_2026-10-22/prototype
python3 test_phase2_review.py --output ../results/review_final.json
```

脚本输出 SAFE/GAP 是针对各探针的有限结论，不是整个算法已经证明安全。`new_dependency_same_action`、`repurposed_helper` 和记账探针使用受控 forecast，仅检验依赖/比较流程；不计入任何战斗净收益样本。
