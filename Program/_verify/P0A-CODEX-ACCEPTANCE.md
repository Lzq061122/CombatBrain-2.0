# P0 第一批最小命令通路独立验收收口（2026-10-11）

受审实现：`cbc9981`；交接 HEAD：`2f7d118`。决定性场次：`sessions/20261011-002337/`（run 7，pid 1512，build `b20261011-002322-97c0ff2e`）。

**结论：在原派单已限定的最小命令通路范围内验收通过。`P0A-CODEX-RUN5-REVIEW.md` §2 的唯一剩余阻挡已关闭，本批停止整改与补场。此结论不代表 P0 整体完成，不把已声明的未验证边界改成通过。**

本次只读当前源码、提交差异、run 7 原始记录、部署文件和外置 ZIP；没有重新构建、测试、部署或启动游戏，没有修改实现或原始材料。开发组的加锁前并发测试红灯、加锁后 60/60 结果保留为开发组执行证据；本次独立确认的是测试设计、同步实现及受影响实机通路，不冒充重新执行这些测试。

## 1. 最后一项阻挡的关闭依据

`ThreadHandoff` 使用同一把 `_sync` 锁保护槽位发布、取出、清空、主线程登记与相关读取/计数。TryDefer 的空槽检查、命令构造、赋值和计数位于同一临界区；TakePending 在同一临界区读取并清空，提供互斥和安全发布。锁内不调用游戏 setter。该修改满足此前提出的单槽位同步要求，没有引入通用队列或调度框架。

新增两个并发 Fact 分别检查空槽竞争投递至多接受一次、同一条命令竞争取出恰好交付一次；使用 Barrier 对齐真实工作线程。测试不是形式化证明，加锁代码本身是同步保障的主要依据，见 HANDOVER §3.1 项 10。

宿主当前只有 Mission tick 消费/清空槽位，保留的 HasPending→TakePending 两次调用不宣称是一个整体原子操作；本批未增加多个消费方。此前提出的安全发布和原子取出已由 TakePending 的锁落实。

## 2. run 7 受影响事实的独立核对

| 项目 | 结果 |
|---|---|
| 加载身份 | L1 pid 1512、MVID `3a2aa527-9e3d-45c8-94dd-0e85736ce3c8`，DLL SHA256 与当前部署文件相同；旧 CombatBrain 未列入本场加载模块，CBATH 身份同 pid |
| 真正获选与候选零写入 | L58–73 注册→候选→实际实例获选→转交→写令，候选未产生本意图写入；L68/L300 `ours=1`，L69/L301 REAL_SELECTION |
| 主线程转交 | MAIN_THREAD_MARKED 为 33；53 条 DEFERRED、53 条 EXECUTED，所有执行记录 `exec_tid=33 is_main_thread=1`；DIRECT、SLOT_BUSY、DROPPED 各 0 |
| 实际写令与回执 | L73/L305 共两条 WRITE_EXECUTED：`invoked=1 error=<none>`、`original_body_entered=1 original_body_tid=33`、Move→StandYourGround。按前次已经确认的“方法入口＋调用正常返回＋独立实际后值”组合口径成立；入口单个字段不升级为完整执行证明 |
| 有限退出 | L252/L253 到期取消与行为撤权，L358/L359 显式关闭与撤权。逐意图扫描 746 行日志，两者各自关闭后 0 条 WRITE_EXECUTED；51 条 WRITE_BLOCKED 全 already_written。L746 Mission 清理。停止本方行为影响与原生后续接续事实保留；真实回退 setter 仍未执行 |
| 双侧观测 | CBATH early at=0.168 为 Defender 4/99、Attacker 5/95，与 CB2 at=1.075 相符；late at=182.479 为双方各 5 编队、46/74 人，与 CB2 at=181.674 相符。跨时刻位置不宣称逐字相同 |
| 清理证据边界 | 本场 CB2 Mission 清理及 CBATH MissionScreen→CustomBattleScreen 转场已直接核对；本次只读检查无存活 Bannerlord。完整控制台 RESULT: OK 与原场脚本退出码沿用开发交接，不冒充本次重新清理或重新取得了该控制台全文。已验入口的失败退出路径保留 |

## 3. 身份和归档

- 部署 DLL：47104 B，SHA256 `27F8CBFA29AC1F13A848A61CDC2F761C7751517558CB294AD2DAA30113EF2416`；与 run 7 L1 一致，不混用 run 5 的 C2004C3B 产物。
- 源/部署 SubModule.xml 同为 `CF246650A1E7425639C9CA7D5B4DD2BC87542D89DA655A0F7B847422161F6168`。
- run 7 CombatBrain2.log：746 行，SHA256 `2FC5580C62892CF4F1D175814C5B87A15A6F7556A8CF2A761A6539051C0E38C3`；场次 13 files / 136097 B。
- `E:\MBmod\CombatBrain2-evidence\20261010-p0a-sessions-run10.zip`：8800734 B，610 entries，其中 607 个文件；SHA256 `EE6EB6D1BF261252B4F5F93E162847DA12A29720955813296FDF3CBA04E49919`。本次核对其 run 7 条目齐全、摘要与交接一致；未重写归档，也不追加产物可复现性调查。

## 4. 停止条件与后续范围

旧审查报告保持历史原文；本报告为当前验收结论。玩家接管、目标失效、选择超时、真实回退、陈旧快照拒绝、下一 Mission、权重竞争降级、线程登记取舍及回执/并发覆盖边界按 HANDOVER §3.1、§5 继续登记。未验证能力不能作为下一批产品功能已经可用的前提；不为本批一次性补齐全部矩阵。

**本批不增加 Stopwatch 每 tick 插桩，不跑 CBATH-only 性能对照。** 目前材料不足以证明有需要处理的性能问题，也不足以证明性能已达标。等出现具体卡顿、耗时预算或下一产品任务的性能要求，再针对实际问题确定测量范围。

下一步回到 CombatBrain 2.0 产品任务，按路线图选择依赖已验证能力的有限交付；本批固定测试驱动器不算 AI 规划能力，第一批收口不自动批准 P1 全量开发。只提交本次验收文档及状态更新，不 push。
