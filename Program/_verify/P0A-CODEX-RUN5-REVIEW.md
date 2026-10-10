# P0 第一批 run 5 独立验收（2026-10-10）

受审实现 `8c52650`，交接 HEAD `1495bc1`；场次 `sessions/20261010-225539/`，pid 17452，build `b20261010-224859-60fa713e`。本报告接续 run 4 的 `P0A-CODEX-REREVIEW.md`；旧报告保留为历史，不再用于否定本轮已取得的实机事实。

**结论：run 5 的主线程写令与原方法入口/正常返回/实际后值组合证据成立。原来的线程路由与回执缺口已经整改；第一批整体仍暂不通过，剩余问题限于新引入的跨线程转交槽位缺少同步保障。只补该槽位，不重开 TCL、不扩展生命周期矩阵、不调查构建元数据差异。**

本次只读源码、既有场次与部署身份，并静态查看当前游戏 DLL；没有构建、测试、部署或启动游戏。开发组 58/58 与构建零警告零错误保留为开发组结果，不冒充本次重跑。

## 1. 两项整改的实机裁定

| 项目 | 裁定与依据 |
|---|---|
| 真正获选 | 保留通过。注册实例与 ActiveBehavior 引用相等，L70/L305 `ours=1`，L71/L306 REAL_SELECTION；候选阶段未产生本意图写入 |
| 转交到主线程 | **本场通过。** L3/L4 Mission tick 登记线程 33；获选事件为 43/34。回调仅投递，`SamplingBehavior.OnMissionTick` 在 Tick 后调用 DrainPendingCommand。L75/L310 两次实际写入均 `exec_tid=33 is_main_thread=1`；53 次 DEFERRED 和 53 次 EXECUTED，零 DIRECT、SLOT_BUSY、DROPPED |
| 原方法完成证据 | **本场组合证据通过。** 本方调用前设置实际 Formation 引用并武装回执；事件按引用匹配目标。当前游戏 DLL 的 Formation.SetMovementOrder 第一条语句确实触发 OnBeforeMovementOrderApplied。两次均记录入口线程 33、普通返回且无异常，并独立复读 Move→StandYourGround。入口事件本身不是出口事件；此处裁定依赖入口、返回及后值的组合，不只凭 entered 或 invoked 单个字段。真实 Prefix 跳过分支没有在本场触发 |
| 到期/关闭 | 754 行日志逐意图扫描：共 2 条 WRITE_EXECUTED，各自取消后 0 条新写入。L253/L254 到期关闭/行为撤权；L364/L365 显式关闭/行为撤权；51 条 WRITE_BLOCKED 全 already_written；L754 Mission 清理 |

## 2. 唯一剩余阻挡：转交槽位同步

`src/Core/ThreadHandoff.cs` 的 `_pending` 是普通字段。回调线程在 `TryDefer` 检查空槽、构造对象并赋值，主线程通过 `HasPending` 和 `TakePending` 读取并清空；`Clear` 也直接写空。类内没有 lock、Volatile 或 Interlocked，宿主也没有围绕这些访问提供同步。

引用赋值原子不等于对象字段已经安全发布，也不等于“检查空槽然后放入”的组合操作原子。没有显式的发布/读取同步，消费者对命令字段的可见性没有在本方实现中保障；如果生产回调重叠，两个生产者还可能都报告投递成功。这里指出的是实现缺口，**不是声称 run 5 已丢命令、发生并发覆盖或有非主线程写入**。

当前游戏 Mission.OnTick 静态路径先调用 MissionBehavior.OnMissionTick，再调用原生 TickAgentsAndTeamsAsync；但该托管调用顺序不能单独证明原生异步任务与下一次 Mission tick 对槽位的访问永远互斥，本次未取得这样的引擎同步合同。不能把这个未证明的前提作为普通字段安全的依据。

新增的 ThreadHandoffTests 是顺序调用，验证了槽位容量和线程号判断，没有验证跨线程安全发布/互斥。run 5 的 53 次成功转交证明本场路由有效，不证明槽位具备同步保障。

**有限整改：** 用同一个小锁保护待执行命令的发布、取出和清空，或用正确的原子发布/交换实现同一单槽位。锁内只处理槽位与必要计数，锁外执行游戏 setter，不建立通用队列或调度框架。消费端以一次原子取出结果判空，避免依赖分开的 HasPending→TakePending 检查。只补直接检查该生产逻辑的必要用例；有源码变更才按原派单补受影响通路场次，不重跑已成立的旁证。下一验收只查这个修复及其受影响事实。

主线程登记一次、下一 Mission、玩家/目标失效、真实回退、陈旧拒绝等既有未验证项继续登记，不新增这些场景作为本轮退出条件。回执的真实跳过分支也不要求为此额外安装一个跳过补丁。

## 3. 已核对身份与原始材料

- 部署 DLL 46592 B，SHA256 `C2004C3BAF4216118CBE88D41200DDA793C7A637BEFB51F0B469ABA94CD7A8D7`，与 run 5 L1 相同；MVID `0063dd50-8837-421d-a299-69bc647eea7e`。该场加载列表无旧 CombatBrain；CBATH 身份同 pid 17452。
- 当前 bin DLL 摘要 `F72110F341D1A79EE158A4FDF7B4F1F200F5D393DB4888E6A4261BC1B48E8ACF`，不宣称当前 bin 与部署同摘要。本次未比较其差异类型，不重建；该差异不改变 run 5 的加载身份记录。
- run 5 CombatBrain2.log SHA256 `6C8D5BAD053CA755B2C15D07FBEBAF1A3BDCE6151955D49186EEEA732DFAAA5C`，754 行。CBATH early at=0.782 为 99/95；late at=183.105 为 Defender 5/76、Attacker 4/61，与 CB2 at=182.923 一致，相隔 0.182 s。人数与编队数相符不升级为跨时刻位置逐字相同。
- 外置 ZIP `20261010-p0a-sessions-run9.zip` 当前 SHA256 `BC4FA254822B4CA468E889FDF83D1AA36EA86BE3184987C02D2AC5F791DC830A`，与交接相符；未修改。
- `_lastrun6.txt` 实际属于本次成功 run 5，记录脚本 end_mission、games_stop、无残留进程、RESULT: OK；`_lastrun5.txt` 属于 225030 启动失败。成功控制台未在该场 13 个文件中，已另存原字节，避免未跟踪副本丢失。
- 当前游戏 `TaleWorlds.MountAndBlade.dll` SHA256 `19387F31557FF840D14F378F6BBDF1D58FCFF406AB9FF2294DFBB3E49A50B87E`。只做静态反编译读取，没有改引擎；托管源码辅助查看不能替代实机回调事实。

补充只读材料存于 `E:\MBmod\CombatBrain2-evidence\20261010-run5-codex-review\`：

| 文件 | SHA256 |
|---|---|
| run5-console.txt | 861AC034BAF33548595CDD45560B7BD298C84612C36624AC39A1C0489319E88F |
| Formation.static.cs | 255512E390F22B73D0EFAFF15703C1C7EA63C7AB7D5A1CB886026BB7DDB8B886 |
| Mission.static.cs | 76739AB37D045A9D323127A331F7B5BFD931B56C4D7230AD5EA3B96B35393246 |

本次只提交验收文档，不修改生产源码、测试、构建产物、部署文件或原场日志，不 push。
