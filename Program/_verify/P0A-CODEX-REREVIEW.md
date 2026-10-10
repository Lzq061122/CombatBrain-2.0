# P0 第一批 Route C 整改独立复验（2026-10-10）

受审实现：`0345012`；交接：`cb17869`；决定性场次：`sessions/20261010-213621/`（run 4，pid 52288）。本报告接续 `P0A-CODEX-REVIEW.md`，原报告保留为整改前历史。

**结论：真正的原生获选已经成立，写令确由获选行为发起，两次 Stop 实际落地。第一批整体仍暂不通过：主线程执行保障和原方法完成/被跳过的证据尚未落实。只整改这条执行通路，不重开 TCL 验证工程，不追查崩溃全因果。**

本次只读取源码、既有场次记录和磁盘身份，没有构建、测试、部署或启动游戏。开发组报告的 Core 47/47 与构建零警告零错误保留为开发组结果，不冒充本次重跑；Core 测试不覆盖 Host 的线程与原生回调。

## 1. 三个交接问题的裁定

| 问题 | 裁定 | 依据与边界 |
|---|---|---|
| `ACTIVE_BEHAVIOR_CHANGED ours=1` 能否证明本方行为获选 | **能，本版证据成立** | `TestDriver.ArmBehavior` 真正调用 `AddAiBehavior` 和 `SetBehaviorWeight`；`IsOurActiveBehavior` 使用注册实例与 `f.AI.ActiveBehavior` 的引用相等。run 4 L59–67、L206–214 串起注册、候选零写入、真实实例获选和后值变化。不是只凭日志名称 |
| `route=native_behavior_apply` 能否证明原生路径写令 | **字段本身不能；源码支持获选行为发起** | 实际调用是 `CbHoldBehavior.TickOccasionally → TestDriver.ExecuteFromBehavior → TryExecuteStop → f.SetMovementOrder(Stop)`。Mission tick 已不再独立调用 Stop 执行器。不是引擎自动把 `CurrentOrder` 写下去；执行者仍是本方代码。该来源不等于主线程已确认 |
| run 1–3 归档能否排除代码导致崩溃 | **不能统一排除** | run 1 有渲染纹理创建失败/资源不足；run 3 时间对应的 pid 44272 原始引擎日志有 Granite `E_OUTOFMEMORY`。run 2 只有启动 `0xC0000005`/`AccessViolationException`，无 GPU OOM 证据。未到 Mission 支持“本场未执行 Mission 命令通路”，不能排除所有模块加载代码或其他因素 |

## 2. 仍需落实的原派单要求

### 主线程执行点（阻挡项）

派单第 56 行明确要求先核对回调线程，再在已确认的主线程执行点写令。run 4 L3 的 Mission tick 初始化记录 `tid=30`；实际获选事件 L65/L212 为 `tid=56/44`。这些是不同线程的直接事实，但不能据此把 WRITE_EXECUTED 的线程号补成 56/44：该日志未记录写令线程。

`CbHoldBehavior.cs:34–37` 与 `TestDriver.cs:512–519` 同步进入 setter，没有转交、线程身份断言或拒绝机制。“原生主循环”与“主线程”不能互换；当前源码注释声称主线程，没有实现保障。现有对象身份/AI 受控/行为归属即时复核值得保留，但不补足线程要求。

有限整改：记录真实原生行为回调线程与 Mission tick 执行线程；若不一致，只把该真实获选行为产生的本次命令最小转交给已确认的主线程执行点。执行前再次核对同 Mission、同实际对象、当前仍为本方 ActiveBehavior、AI 受控、非玩家拥有、意图未关闭且当前时间未超过截止。不得退回“普通 Native Move 触发、下一 tick 自行写 Stop”的旧通路，不建立通用调度框架。

### 原方法完成/被跳过的证据（阻挡项）

派单第 57 行明确排除“普通返回”作为唯一完成证明。`TestDriver.cs:573–574` 调 setter 普通返回后将 `invoked=true`；生产源码没有本次调用的原方法是否执行/被跳过回执。run 4 的 `Move → StandYourGround` 证明实际状态变化，不能单独证明在加载 Harmony 等模块的环境中原方法体未被跳过。

只为本方这次 setter 补可对应同意图/同对象的原方法完成或跳过事实，并与实际后值分列。不要扩展全局命令审计或改动 CBATH。整改有源码变化后只补必要检查和一场受影响通路验证；其他已经成立的事实复用。

## 3. 保留的实际成果与身份边界

- run 4 L1 实际加载摘要与当前部署文件同为 `51068919F30B878090BCEFA5A496AB26578F1E6FD8B59CA8DBF076716DF3917A`，43008 B；加载 MVID `65bbd58f-cf5e-4bda-886f-18bf794af645`。旧 CombatBrain 未列入该场加载模块。
- 当前 `Program/bin/Debug` 和 `obj/Debug` DLL 为 `6E87AD62E6F6A7DD75510472B60D7C7CCFF0F7BA83A8D42F2856B38A01B55C0C`，43008 B，mtime 21:59:35；因此“源输出与部署同摘要”只保留为部署时记录，不能当作复验时三方相同。源码/测试无受控改动，BuildIdentity.g.cs 与 HEAD blob 相同。本次未比较两个 DLL 的代码内容，不推断其差异类型，也不为此追加重编或可复现性调查；run 4 加载身份由原场日志与部署文件绑定。
- 双侧状态：CB2 at=1.040 为 Defender 99 / Attacker 95，与 CBATH early mission_time=0.480 的人数相符；晚期 CB2 at=182.721 为双方各 5 编队、47/40 人，CBATH late at=182.769 同为 47/40、total 87。跨采样时刻的位置不宣称逐字一致。
- 本次逐意图扫描 638 行日志：2 条 WRITE_EXECUTED，两者关闭后各自 0 条新写入；51 条 WRITE_BLOCKED、183 条 SNAPSHOT_HEALTH。L172/L247 的行为撤权及 L638 Mission 清理保留。2 条 REVOKE_SKIPPED 不升级为真实回退已验证；age 恒 0 不升级为陈旧拒绝已实机验证。
- run 4 控制台记录 end_mission、games_stop、无残留进程及 RESULT: OK；last-run.json 的 `no_run` 不是自然战斗结算完成证据。本场 Mission 是脚本结束。
- 玩家接管、目标失效、选择超时、真实回退与下一 Mission 无残留继续未验证；不要求为本次整改补齐全矩阵。

## 4. 失败场次与证据保存

run 1 已归档的 pid 2372 引擎错误为 `create_render_texture` 与资源不足，GPU Crash 标志支持渲染资源故障；不能把该场直接写成已确认的 Granite 缓存故障。run 3 的 `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_44272.txt` L146–152 在 21:30:09 记录 Granite 创建 16272×16128 纹理失败、HRESULT -2147024882 与 GPU cache 错误，时间对应 run 3。run 2 原因未定。

显存下降后同部署 run 4 成功，支持环境资源解释，但不是对三场所有代码因素的因果排除实验。没有证据表明本方 Mission 命令通路导致这些启动失败；不因此改验收参数、扩大源码整改或要求补崩溃复现实验。

原 sessions 外置 ZIP 本次复算仍为 `E825FF34A48F024BB5FA3436BA13F4A44430A60C295635D48197F5E10C27A2FB`，未修改。run 2/3 场次目录为空，原 ZIP 不含其控制台与 pid 44272 引擎日志；为防未跟踪副本丢失，本次另存原字节到 `E:\MBmod\CombatBrain2-evidence\20261010-routec-codex-review\`，不入源码仓库、不重写旧归档：

| 文件 | bytes | SHA256 |
|---|---|---|
| run2-console.txt | 4998 | BC0993A4035C9C8BE756814F6BC94E6673B4FA0BB6A2FA0A82B7F89538D261AE |
| run3-console.txt | 3784 | DC050EBBB9440313358E949AB2766973690B8D78B219F6A97E889A568DB4DC69 |
| run4-console.txt | 8330 | CB36C978DBFC2DF9C78D7276D2D44E400445BD8C9A932C274F75357B6AE8EB3E |
| rgl_log_errors_44272.txt | 1524 | 7D1C5A68AC1F312454297A6F7D99B1565BAA737BE6BD4CD570031F6449B415D1 |
| rgl_log_44272.txt | 13030 | 18DA02CEA8FFEA22A612E0DA1577CBF945960C5BE0B5690CBBD69DFE9B97D6E8 |

本次仅提交复验报告与相关口径更正，不改实现、构建产物、部署文件或原场日志，不 push。
