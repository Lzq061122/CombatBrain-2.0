> **研究资产阅读视图**：原件位于 [CombatBrain_Research_2026-10-22.zip](../../archives/CombatBrain_Research_2026-10-22.zip)，包内路径为 `evidence/prototype_review.md`。本视图只增加说明并调整指向省略文件的 Markdown 链接；代码保持原样。完整复现请在原 ZIP 的独立解压副本中运行。研究建议未自动获批。

# 研究原型独立审查

范围：`prototype/planner.py` 与实现前预登记 `evidence/evaluation_protocol.md`。审查者未修改planner；独立编写标准库unittest `prototype/test_review.py`，以具体反例推动修复。这里评估的是单窗口抽象规划机制和进程内执行夹具，不是Bannerlord运行时，不构成生产可部署认证。

## 初次结果与修复依据

初版SHA256 `c1fe2b05b99c4dc71fa12a9cd6d2e41b30714f8fcb1fce7ce8c419dc9bdc7e75`：12项测试，8失败、4通过。完整实跑输出在 `prototype_review_initial.txt`。新增时窗/支援反例后14项中10失败，见 `prototype_review_initial_timing.txt`。这不是只给正样本的自证测试。

| 反例 | 初版实际行为 | 要求的合同/修复方向 |
|---|---|---|
| 相同uid/gen/revision来自另一mission或domain | report接受 | mission/domain与报告身份一起过滤 |
| 选择结果生成后authority丢失或generation改变 | adopt仍信任旧assessment | 按当前Snapshot重验，不让过期评估决定接纳 |
| 不同generation复用uid，但旧报告sequence=100 | 新generation的sequence=1遭拒 | sequence账本按mission/domain/uid/generation/authority/revision隔离 |
| 60m外目标的效果行动尚未到达 | write立即接受 | 区分移动准备与战术效果放行；绝对not_before加独立到达/展开ready |
| options上限=1且存在保护责任 | mandatory protect被切掉 | mandatory单独保留，optional才截断 |
| safety调用使用另一mission快照 | 返回本mission逃逸例外 | safety同样核对作用域 |
| fixer t=26才到场，shooter t=2开火 | 整个窗口的敌增援已被抑制，与t=2到场fixer给射手相同收益 | 作用按起效时间重叠计算，迟到牵制不能抹掉之前支援 |
| 反骑单位距目标60m但speed=0 | 被当成即时支援，禁止骑兵行动 | protector关系包含ETA、路径和接战窗口 |
| 同敌射手面对4个攻击者 | 全额incoming分别加给所有攻击者 | 每个敌人一份总输出预算，按受击者分配；该预算只是公开的抽象假设 |
| 同计划目标移远，旧not_before到期但没有ready | 旧时间放行 | 时间不是到达事实；需要当前作用域的独立ready |
| 一支100 melee预备队同时覆盖两个资产 | 减损30，超过模型单队预算100×0.5×0.3=15 | 共享预备队覆盖预算；不能以“各只有一条hold action”掩盖反应层资源复用 |

额外正反合同：重复资源硬拒绝；当前对象硬失效不能被“无全局变化”的滞回留住；取消任务保留关键职责；尚无牵制则攻击方案不可行；旧revision反馈不能阻路；有效no_response反馈阻止同epoch重复probe；到达时间和观察ready都满足后确实可放行。

## 对主算法的判断

候选生成不读取场景名称；全枚举、beam与成包greedy使用同候选和assessment，方向正确。完整候选在最终接纳前重新走硬依赖与责任检查，beam的部分组合临时放松约束不会自动变成执行许可。

但是，beam的排序是启发式，部分打分仍可能把暂时低分的牵制者/高价值未来组合排走。放松“缺支援即不可行”只避免一种早删，不保证保留依赖组合。width=1固定种子20261022的第15例，在中间版本出现相对同候选全枚举6.9943的目标差距；完整输入/动作/评分与源码哈希记录在 `prototype_review_beam_negative.json`。该结果是搜索质量负例，不能指称越过硬约束，也不能扩大成默认width32必然有同样差距。

## 研究边界仍需明确

1. 效果函数的0.35牵制比、固定支援半径、总输出预算、损失系数、能力相加和损伤上限都属构造假设。测试通过验证内部合同，没有证明其战斗物理正确或能提高胜率。
2. 对关键资产的残余压力使用整个窗口的预计damage。它未积分晚到攻击之前的暴露，因此不能被称为动态因果战斗仿真。要验证真实伤亡必须进入校准模型或实机配对试验。
3. `ready`由夹具调用者提供，代表独立观察事实；这里没有实现游戏事件如何证明“某对象对某目标的当前牵制仍有效”，也没有真实命令交付确认。因此接口放行不等于游戏命令已经执行。
4. 高损失但仍有效的职责不应因一个损失阈值自动退出。该原型用责任与局部safety合同保留此边界，但并未建立从现场观察推断“仍有效”的完整估计器。
5. 不确定度是参数化误差带，不是校准置信区间；敌方hold/reinforce/counter是有限情景集合，没有统计概率。相对全枚举的最优值只在同一个候选与评价模型内成立。
6. 时间计算为路径/速度输入的简单ETA，不含真实地形绕行、队形展开、拥塞、到达分布、兵团变更、网络执行或引擎恢复。不同形成任务的效果时间仍是适配层责任。

## 最终复核

实际运行 `python3 -m unittest -v prototype/test_review.py`：**20项全部通过，退出0**。本轮受检 `planner.py` SHA256 `1730e609a23f509d07b6bb317f30dd68c4731137ad114371d47ae205fdb7b3c9`；运行前后源码哈希一致。完整输出在 `prototype_review_final.txt`。预备队反应已改为每队只分配给一个可及资产，并记录 `reserve_assignment`；mandatory候选、fresh采用、作用域/代际隔离、时窗/ready、迟到牵制、静止支援、敌输出预算、反馈合同均由反例复核。

结论：这20项研究合同在该受检版本通过，可以作为本轮机制研究交付；不能外推为生产P3、游戏效果、性能或全部算法正确性通过。源码后续若变更，以最后复测哈希为准。初始失败与搜索负结果必须随报告保留，不因修复而删除。
