> **研究资产阅读视图**：原件位于 [CombatBrain_Phase2_Research_2026-10-22.zip](../../archives/CombatBrain_Phase2_Research_2026-10-22.zip)，包内路径为 `evidence/world_contract.md`。本视图只增加说明并调整指向省略文件的 Markdown 链接；代码保持原样。完整复现请在原 ZIP 的独立解压副本中运行。研究建议未自动获批。

# Formation 级独立执行世界合同

本文件对应 `prototype/world.py` 与 `prototype/test_world.py`。这是可复现的合成执行过程，验证观测—决策—执行—反馈闭环；未拟合 Bannerlord/RBM，没有 Agent 物理、真实战损或游戏性能证据。执行模块不导入 planner，不调用候选评分，不读取算法名字，不返回战术总分。同一场景与 variant 对两侧使用同一套规则。

## 1. 可直接使用的接口

```python
from world import World, reactive_orders
world = World(scenario, variant='nominal')
obs0, obs1 = world.observe(0), world.observe(1)
world.issue(0, controller0(obs0))
world.issue(1, controller1(obs1))
world.step(dt=1.0)
actual0, actual1 = world.metrics(0), world.metrics(1)
```

两侧命令均在 `step` 前送入；移动提案和伤害提案分别从同一子步状态产生，统一提交。内部子步最大 0.25 秒，并对事件、延迟交付、准备完成时间切片。实验驱动应固定外部 `dt=1`；有限步长近似不等于连续物理精度。

| 接口 | 数据与含义 |
|---|---|
| `World(scenario, variant)` | `scenario.units` 是字典列表；必需 `id,side`，side 约定 0/1。`bounds=[xmin,xmax,ymin,ymax]`；可选 `duties,objectives,obstacles,events`。构造复制输入，不改场景。 |
| `observe(side)` | 返回 `t,side,units,events,duties,bounds,objectives,obstacles`。新副本、读取不消费。双方当前位置、健康、速度能力、近/远率、弹药、身份、真实 `vx/vy,moving,phase,contact_ids,suppression,damage_dealt/received` 可见。仅己方有 `current_order,pending` 与己方 Duty。 |
| `issue(side, orders)` | 每条含 `actor,kind`，攻击任务另有 `target`，move/retreat 含 `x,y`；可附 `delay,generation,authority`。种类：hold/move/fire/fix/strike/pressure/retreat。返回接受、拒绝或 unchanged 回执。 |
| `step(dt=1)` | 执行双方；返回本 step 的事件。`observe.events` 保留最近 step 批次及随后 issue 产生的回执。前一批不会无限累积，采集器需要逐 step 保存。 |
| `metrics(side)` | `hp,initial_hp,hp_lost,alive,authorized_alive,positions,ammo,damage_dealt/received,distance_travelled,ammo_used,orders_accepted/rejected,order_changes,orders_completed,route_blocks,attack_seconds,fixing_seconds,suppression_seconds,duty_covered_time,duty_required_time,duty_coverage` 等实际计数；不含 planner score。 |
| `reactive_orders(obs,mode,state)` | 仅接观测字典和调用者保管的有限状态字典。模式 hold/support/counter/withdraw/feint。不持有 World，不读取对手命令或未来剧本。 |

Unit 默认：hp/max_hp=100、speed=5 m/s、melee=5、ranged=0、ammo=40、range=55 m、armor=0、mounted=False、anti_mount=0、role=line、authorized=True、authority/generation=0。`melee/ranged` 是完整健康编队的合成输出率，ammo 是编队级连续齐射余量，不是士兵人数或逐支箭计数。hp=0 的初始编队仍需显式给正 max_hp。

公开观测不会返回 variant 名、真实速度/准备/牵制阈值等系数，也不返回未来 `scenario.events`、敌方 current/pending order 或敌方 order_* 回执。fix 事件没有 `required_strength`。公开的实际速度、伤害、抑制可能被控制器从历史样本推断；这属于已经发生的效果。环境参数表出现在研究说明和世界源码中是实验可复现要求，控制器运行时不得导入 `VARIANTS`、`SETUP` 或访问 World 私有字段。

## 2. 真实执行与预算

| 机制 | nominal 规则 | 可观测结果 |
|---|---|---|
| 移动 | 直线逐步移动，不超过 speed×实际速度系数×(1−suppression)×dt；速度不因 hp 下降自动改变。move/retreat 目的地夹到边界。 | x/y、vx/vy、moving、实际路程、arrived 完成事件。 |
| 准备 | hold/move=0 s，fire/fix/strike/pressure=1 s，retreat=0.5 s。准备时不移动；仍可按 55% 近战率防御。 | preparing，己方 order_ready。重复相同语义任务不重置准备或延迟时钟。 |
| 接触/射程 | 接触半径 6 m；strike/fix 接近至 5.4 m；fire/pressure 接近至 0.96×range，进入实际 range 才可射击。默认 range=55 m。 | 双方 contact_ids、moving、接触伤害或射击事件。 |
| 离开接触 | 目的地远离全部当前接触对象时，首次退出等待 2 s。等待时可受伤；随后才按受抑制速度运动。 | exiting 和实际退出时间。变更任务会重新经历相应准备/退出代价。 |
| 有限输出 | 每个存活且有权编队每子步仅一个伤害通道：接触优先近战，否则可远射。近战防御遇多个接触目标时**平分同一个预算**。不对每个目标复制一份实力。 | 每个 damage 事件含 actor/target/amount/channel/allocated_output。 |
| 伤害 | 预算=通道率×hp/max_hp×任务系数×dt。实际伤害=预算×0.9×反骑修正÷(1+目标 armor)。多来源过杀按比例缩放到目标剩余 hp，再同时提交；同子步互杀允许双方输出，此后死亡方无输出。 | 累计 dealt 与 received 及实际 hp 下降一致。 |
| 弹药 | 远射消耗 hp/max_hp×dt 的连续齐射余量；不足则同比缩小当次输出。近战不消耗 ammo，远程与近战不能双花。 | ammo≥0、ammo_used、damage。 |
| 任务成本 | fix 近战伤害仅正常的 35%；pressure 远射伤害仅正常的 70%；retreat 接触防御为 55%。这些任务没有额外免费伤害奖励。 | 发起者实际伤害、弹药消耗、受伤与时间。 |
| 初始 hold | 初始站位视为已准备。接触时防御；无接触且有远程能力时自动射击范围内最近可见目标。这是本抽象的固定原生防御基线。 | hold 也可能产生实际伤害；并非“等待就零输出”。 |
| 障碍 | 矩形只阻止直线移动穿越，报 route_blocked；不提供绕路、碰撞、真实导航或射线遮挡。 | blocked 与一次 route_blocked 回执。 |

“原速度”即单位的公开 speed，不乘 hp 比例；存活比例只作用于输出/弹药。该明确选择避免把残余战力率偷偷等价为行军速度。

`fire/fix/strike/pressure` 目标消失或代数变更时，旧指令不能转嫁到同 ID 的新编队；会完成为 `target_unavailable`，继续攻击新代目标必须重新 issue，重新准备。move/retreat 只在实际抵达时完成。攻击指令是持续任务，无“已经发命令所以成功”的完成捷径。

## 3. 牵制不是标签真值

fix 必须满足发起者存活且有权、准备结束、目标仍是同一编队代数、距离≤6 m。每个合格发起者贡献 `melee×hp/max_hp`，同目标同时求和；达到 `0.65×max(0.5,目标存活近战率)` 才产生实际 70% 移动抑制。fix 执行标签本身不会让依赖门通过。

每个 actor-target 接触窗口最多贡献 18 s（early_fix 为 5 s）。有效贡献时间耗尽后，即使仍持有 fix 任务也无抑制；至少实际离开 6 m 接触范围 4 s 才恢复窗口。改成 hold 再改回 fix 不刷新窗口。发起者依旧承受对手输出，而且只造成 35% 自身常规近战伤害，因而“固定住”有机会与损耗成本。

事件 `fix_effect` 给出 `actor,target,generation,authority,target_generation,contribution,total_strength,effective,suppression`；它来自几何与执行资源。贡献量是此抽象的诊断可观测量，不能直接当 Bannerlord 可采字段；游戏接入应以速度/接触关系/支援延迟等可采代理验证。抑制按子步起点接触判定，在该子步作用；离开接触后最迟下一子步取消，误差上界 0.25 s。

## 4. 身份、延迟命令、Duty 和事件

同一 actor 只有一条当前指令和一条待交付指令。后发指令覆盖先前待交付指令；delay 期间旧任务继续执行。准备期和退出等待属于实际执行时间，不是 planner 估计。相同 kind/target/x/y 且身份相同返回 unchanged，不能靠重复下发延长窗口或重置延迟。

命令提交与延迟交付均检查 side、alive、authorized、generation、authority。authority 事件使授权代数单调增加、清除待交付指令、释放旧任务为 hold；撤权编队在本抽象中不再移动、牵制或输出。**这模拟“退出本研究控制资源池”，未模拟游戏原生 AI 接管后的行为或收益。** 根本没有获得授权或尚未到达的增援不能被当作当前可用输出。

可选场景事件只在生效时公开：

- `{t,type:'authority',actor,authorized}`：撤权/再授权；每次递增 authority。
- `{t,type:'reinforcement',unit}`：此刻才加入编队、累计初始可用 hp，并报 reinforcement；之前 observe/issue 不知其存在。
- `{t,type:'replace',actor,unit}`：同 ID 新 generation，authority 也增；保留旧 hp/max_hp/ammo 与伤害累计、不免费复活补弹，旧命令失效。它只覆盖重新识别语义，未实现真实动态拆编/合编调度。

Duty 输入采用 `{asset,owner,coverage_radius,side?,start?,end?}`。owner 与 asset 必须当前存在且存活，owner 有权、在半径内才累计 covered_time；required_time 是所有激活 Duty 的总秒数，多个职责允许大于墙钟时间。只采样**抽象位置覆盖连续性**，没有声称位置邻近就保证资产安全；真正损耗由独立攻击过程产生。

事件主要包括 `damage,fix_effect,order_accepted,order_rejected,order_ready,order_completed,route_blocked,authority_changed,generation_changed,death,reinforcement`。事件 t 是子步发生时间（伤害按子步起点记账）；可通过 metrics 与相邻观测核对总量。己方回执不是敌方可见命令意图。所有实际效果事件对两侧公开，这是假定完整当前观测的合成试验。

## 5. 预先声明的世界偏差族

| variant | 相对 nominal 的唯一偏差族 | 要检验的问题 |
|---|---|---|
| nominal | 上述固定系数 | 基本闭环和可解释执行。 |
| slow_shared | 双方移动×0.72、准备×1.8 | 同一个行军/集结误差同时影响多单元；不能把独立 ±epsilon 当成充分鲁棒性。 |
| weak_fix | 阈值提高至1.5×目标存活近战率，抑制上限25% | 准备/接触都成立仍可能固定不足，依赖门必须看实际效果。 |
| early_fix | 固定贡献窗口18→5 s | 协同到达太晚、效果提前结束。 |
| counter_matchup | 反骑系数0.25→1.25；修正为 `1+c×min(1,anti_mount/max(melee,0.5))` | 公开名义实力不能保证真实反相性估计；只作用于近战攻击 mounted 目标。 |
| sticky_exit | 接触退出等待2→5 s | 已经投入的近战资源不能即时再利用。 |

上述偏差固定于一次试验开始，不按算法、获胜方或时刻自适应。它们是压力轴，不是已校准概率分布；不会把未经采样的世界泛化能力伪装成误差条。counter_matchup 只放大反骑优势，未覆盖所有兵种相克；weak_fix 的25%抑制也需要更高阈值首先成立。

与规划侧仍共享的假设必须保留：Formation 质点、可观察名义能力、距离/速度用于机动、有限输出率与当前健康比例相关、聚合伤害及弹药、完整当前观测。独立执行避免“把 planner 分数当 outcome”的循环，却不能消除这些建模共同假设。证据上限是这些轴上的合成闭环比较，不是游戏胜率、真实人数、CPU成本或战术全域通过。

## 6. 结构测试与复现

```bash
cd /Users/tianxq/Documents/ChatGPT/新教程/CombatBrain_Phase2_Research_2026-10-22/prototype
python3 -m unittest -v test_world.py
```

21 项结构测试通过：实际移动和准备、射程、弹药守恒、多个目标共享近战预算、近远不能双花、同步互杀与过杀归因、死亡无后续输出、延迟撤权/再授权、编队代数更替、再识别不回血补弹及可重新锁定新代目标、敌方意图/未来/参数不泄漏、观测副本、接触和强度门、固定成本、弱/早终止效果、标签切换不能刷新窗口、退出迟滞、双方共享速度偏差、直线路障、Duty覆盖、未到增援不可使用、仅观测反应与非法命令拒绝。一个测试可覆盖多个相关断言；21不是战术场景数或收益样本数。

下一步真实校准最小增量是每队位置/速度/接触邻接、实际到位与准备时刻、命令身份及控制代数、交战/射击占空比与弹药、局部损伤、退出接触时间、保护资产威胁及支援接触时刻。若只能采总战损，无法识别慢到达、弱牵制、反相性和退出阻力，不能用单个伤害折扣替代这些误差源。


## 7. 手工时间合同 → World 的机制桥接实跑

`prototype/run_temporal_execution.py` 使用真正的 `temporal.Plan / CommanderLedger / validate / observed_ready`，向 World 下发手工写好的任务图，保存完整场景、计划、关键逐秒状态、全部 step 效果事件、命令回执、身份验证、修复与释放记录到 `results/temporal_execution.json`。没有调用自主规划器生成该图，也没有把这项演示归为战术收益证据。

| 对照 | 联合效果首次被观测 | strike 实际下发 | move → fire | 故障与保持 |
|---|---:|---:|---|---|
| nominal_on_time | 5 s | 6 s，遵守计划最早时间 | move 在 5 s 被真实 arrived 回执确认释放；6 s fire，之后才出现实际射击 | Duty 全时覆盖。 |
| nominal_second_fixer_late | 9 s | 9 s | 同上，独立任务不因另一编队迟到重排 | 第二 fixer 实际命令交付迟到 4 s；预测窗口不能先验放行。 |
| early_fix_and_guard_revoke | 5 s | 6 s | 同上，失败后仍继续实际射击 | 10 s 观测牵制效果消失，只撤销依赖 consumer strike；11 s guard 撤权另行造成 Duty 空缺。 |

strike 释放时两个 fixer 的观测贡献在名义场景分别约 3.922、3.922，迟到场景约 3.624、3.982；每个都小于合同要求 6.5，合计才足够。`fix_effect.effective` 来自世界真实接触、准备、健康与阈值的结果；桥接还再次检查当前接触、actor 与 target 代数及授权，把事件明确绑定到对应 taskid。它没有凭任务名字、预估到达或计划 strength 自称成功。

每次实际写命令前都以当前 observe 建立命令范围的 `Plan` 调用 validate，原图的依赖和同 actor 排程仍由单一 ledger 检查。实际 accepted/unchanged 的指令才 mark_dispatched。move 完成采用绑定 mission/domain/revision/task/actor/generation/authority 的完整执行回执；取消 strike 后保留未释放占用，直到下一秒观察到 hold 且真实速度为零，才用取消前 revision 确认释放。effect facts 按 mission/domain/taskid/Task.revision 绑定，局部修复没有改变的任务继续接受其自身版本的新鲜事实；release 则匹配原 dispatch_revision，不因无关修复升级全局 revision 就拒绝真实完成回执。撤权 guard 没有伪造旧授权 release，保留 unresolved 占用和 Duty 缺口；不尝试擅自接管或寻找新 owner。实际 Duty 覆盖为 11/24≈45.83%，无关 fire 仍继续产生伤害。

运行检查区分 39 项场景机制断言、27 项事实负例检查和 2 项迟到对照断言；这些数量不是独立统计样本。负例包括仅有预测、仅一个 contributor、错 actor、过期事实、错 generation、错 mission、错 domain、错 taskid 和错 Task.revision。有效事实的 valid_until 是新鲜度 TTL，绝不保证未来若干秒继续生效；每个外部 tick 重新验证活跃依赖，失效响应延迟上界为此实验 1 秒。手工图和参数是明确的机制夹具，不宣称自主联合生成或在线全局最优。

```bash
cd /Users/tianxq/Documents/ChatGPT/新教程/CombatBrain_Phase2_Research_2026-10-22/prototype
python3 run_temporal_execution.py
python3 -m unittest -q test_world.py test_temporal.py
```

本次运行：三例的机制检查及迟到对照均通过；world 21 项与最终 temporal 13 项测试合计 34 项通过。进一步统计评估应读取主实验结果，不将本夹具重复计入收益样本。
