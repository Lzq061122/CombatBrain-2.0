# P0 Exit Gate 3 —— 控制权安全边沿：控制域与场景判据

- 派单：`E:/MBmod/TacticalCommandLab/_dispatch/2026-10-11-cb20-p0-stage-exit-control-safety.md`（v1.0，2026-10-11）
- 工作区：`E:\MBmod\CombatBrain 2.0`，源码根 `Program\`
- 基线 HEAD：`0324426`；已验实现 `cbc9981`；决定性既有场次 run 7 = `Program\_verify\sessions\20261011-002337\`
- 本文件性质：**改代码前先写定的控制范围与判据**。后续实机判定以本文件为准；若实机需要改判据，只能在报告里如实登记原因，不得事后放宽。

---

## 1. 当前控制域（C 路线，不改）

| 项 | 取值 | 代码位置 |
|---|---|---|
| 路线 | C：注册本方 `CbHoldBehavior` 进原生行为选择链，由原生 `FormationAI` 选中后走 `TickOccasionally` 应用命令 | `Program/src/Host/CbHoldBehavior.cs`、`Program/src/Host/TestDriver.cs:ArmBehavior` |
| 可选对象 | 非空步兵 Formation（`UnitCount>0` 且 class 为 Infantry） | `Program/src/Core/TargetSelector.cs` → `:empty` / `:not_infantry` |
| 排除 | `IsPlayerTeam`、`IsPlayerControlled`（`PlayerOwner != null`）、`!IsAiControlled`、快照陈旧 | `TargetSelector.Select` → `:player_owned` / `:not_ai_controlled` / `:snapshot_stale` |
| 不申请 Full 人群控制、不建租约、不把玩家队加入可执行目标 | —— | 同上；`ExecutionGate.RequestWrite` 再以 `player_owned` / `not_ai_controlled` 兜底 |
| 时间参数（保持不变） | 快照上限 2.0 s、意图窗口 20 s、获选等待 10 s | `Program/src/Core/TestParameters.cs` |

---

## 2. 真实玩家接管在当前被控对象上**不可达**（代码入口 + 引擎依据）—— **初版结论，已被下方 §2.1 修订**

派单 §2 要求先查清，CBATH 缺工具不能作为依据。以下为反编译真实游戏程序集
`E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\TaleWorlds.MountAndBlade.dll`
全量反编译产物（`Program\_scratch\decomp\full\`）的调用点证据：

1. `Formation.IsAIControlled` 的 setter 为私有（`Formation.cs:172` `public bool IsAIControlled { get; private set; } = true;`），外部唯一公开写入口是 `public void SetControlledByAI(bool, bool = false)`（`Formation.cs:788`）。
2. 另一条会联动改资格的路径是 `PlayerOwner` setter：`Formation.cs:271-281` `set { _playerOwner = value; SetControlledByAI(value == null); }`。
3. **`PlayerOwner` 的非 null 赋值点共 16 处，全部要求玩家/己方语境**，没有一处会给敌方 AI 编队授予玩家所有：
   - `OrderController.cs:72`（构造，前置 `Team.IsPlayerGeneral`）
   - `Team.cs:375` `AssignPlayerAsSergeantOfFormation`
   - `AssignPlayerRoleInTeamMissionController.cs:58`（前置 `team != Mission.PlayerTeam → return` 且 `team.IsPlayerGeneral`）、`:157`
   - `Mission.cs:4511`、`MissionNetworkComponent.cs:303/1722`（多人网络）
   - 其余为 `= null`：`LordsHallFightMissionController.cs:382`、`GeneralsAndCaptainsAssignmentLogic.cs:218/247`、`Formation.cs:1932`
4. 玩家下令触发的接管：`OrderController.cs:726` `if (selectedFormation.IsAIControlled && selectedFormation.PlayerOwner != null) SetControlledByAI(false);` —— **前提是该编队已有 `PlayerOwner`**。
5. `Team.IsPlayerGeneral` / `IsPlayerSergeant` 为 private set（`Team.cs:98/100`），只能由 `SetPlayerRole` / 构造 / 网络包设置。

**结论（初版，已被 2026-10-11 Codex 复核驳回并修订，见下方 §2.1）**：初版只论证了“绑定那一刻 `PlayerOwner` 不可能存在”，但 `Team.IsPlayerTeam` 是派生量（`Team.cs:40-50` `return Mission.PlayerTeam == this;`），可被原生公开入口 `Mission.JoinEnemyTeam()` 改变，因此**“绑定时的过滤”不足以证明以后身份不会变化**。初版结论不成立，改由 §2.1 承接。

### 2.1 修订：玩家身份可变的原生路径与边沿覆盖（2026-10-11）

- `Mission.JoinEnemyTeam()`（`TaleWorlds.MountAndBlade\Mission.cs:4659-4691`）是公开原生方法，在 `PlayerTeam` 为攻/守任一方且对方 `Team.Leader != null` 时把 `PlayerTeam` 换成另一队。全 DLL grep 只有定义与 `FailedAssert` 两处，`OnPlayerTeamChanged` 在 DLL 内也无订阅者 —— 它是给 UI/外部用的入口，不是内部自触发。
- 该路径**不写 `Formation.PlayerOwner`、不直接改 `Formation.IsAIControlled`**，但会翻转 `Team.IsPlayerTeam`（派生比较），从而改变本方 `TargetSelector` 的 `IsPlayerTeam` 输入；引擎若再次走过 `Team.AddTeamAI(...)`（`Team.cs:447-453`）或 `Team.SetPlayerRole(...)`（`Team.cs:707-715`），`IsAIControlled` 会按新的 `Mission.PlayerTeam` 重算。
- 因此本单把“玩家身份变化”也当作**资格变化的一种**，用同一套已实现的生产检查覆盖，而不是声明它不适用：
  - `TestDriver.Tick` 的绑目标检查先由 `likelyPlayerOwned(bound)`（`IsPlayerTeam || IsPlayerControlled`）判 `PlayerEligibilityLost`，再由 `!bound.IsAiControlled` 判 `AiControlLost`；
  - `TryExecuteStop` 的执行前复核即时求值 `playerOwned`、`aiControlled`、`behaviorIsOurs`，交给 `ExecutionGate.RequestWrite`。
- 本单实机补一场 mode `c`：用 `EligibilityProbe` 调 `Mission.JoinEnemyTeam()`（公开原生入口，非 F1/F6 按键、非伪造 `PlayerOwner`），使**同一已绑定实际对象**在引擎里变成玩家队成员，观测生产路径是否同样让位。日志固定标注 `note=test-adapter_triggered_playerteam_change`。
- 仍不覆盖：真实按键/UI 接管事件链、阵亡导致的资格变化、多人 peer 路径。这些按“未验证”登记，不写成通过。

---

### 2.2 场景 C：绑目标经原生 `JoinEnemyTeam()` 变成玩家队后，本方让位（mode c，本轮补证）

| 项 | 内容 |
|---|---|
| 触发 | `EligibilityProbe` mode `c`，在 `intent.SelectedAt >= 0` 之后的 Mission tick 调 `Mission.Current.JoinEnemyTeam()`（公开原生入口），把 `PlayerTeam` 换成绑目标所在队 |
| 前置如实记录 | `attacker_leader` / `defender_leader` / `agent_main` / `main_agent` 是否成立（用于区分“原生不可达”与“没走到”） |
| 预期生产反应 | 同一 tick `TryExecuteStop` 由执行门以 `player_owned` 拒绝（`write_attempts_intent` 仍为 0）；下一 `Tick` 由 `likelyPlayerOwned` 判 `INTENT_CANCELLED reason=PlayerEligibilityLost`；`BEHAVIOR_DISARMED weight_before=8.000 weight_after=0.000` |
| 恢复 | 4.0 s 后 `mission.PlayerTeam = plan.OriginalPlayerTeam`（只撤回本适配自己的环境改动），记录 `intent_open_now` / `same_intent_open` |
| 不得 | 不得标为 F1/F6 按键；不得伪造 `PlayerOwner`；不得放宽生产玩家队过滤 |

## 3. 本单实机触发方式（如实标注为“测试接口触发的资格变化”）

- 接口：**公开原生方法 `Formation.SetControlledByAI(false)` / `(true)`**（`Formation.cs:788`），作用在**已经被选中、已绑定的同一个实际 Formation 对象**上。
- 理由：该位与「是否被 AI 控制」是同一个状态位；`PlayerOwner` setter 也是通过它生效（第 2 节证据 2）。用它做实机触发 = 用原生入口驱动的真实资格变化，而不是反射写私有字段。
- 标签纪律：日志与报告一律写 **“测试接口触发的资格变化”**。不得改称 F1/F6 玩家接管，不得据此宣称阵亡事件链通过。
- 必须由测试适配记录调用前后的真实值：`IsAIControlled`、`PlayerOwner`、`OrderController.GetActiveMovementOrderOf(f)`、`f.AI.ActiveBehavior` 类型名、`CbHoldBehavior.WeightFactor`、`units`。原生接口可能同时改变其他实际状态（见第 5 节“副作用”），逐项记录，不伪造“撤销恢复”。

---

## 4. 测试适配：`EligibilityProbe`（默认关闭）

- 新文件 `Program/src/Host/EligibilityProbe.cs`，进 `Program/CombatBrain2.csproj` 的 `<Compile>` 列表。
- 开关：哨兵文件 `Documents\CombatBrain2\eligibility-probe.txt`。**文件不存在 → 完全禁用**：不调任何游戏接口、不写任何 MovementOrder、不产生额外日志（除禁用态一次性说明）。
- 配置格式（一行）：`intents=A,B` —— 第 N 个触发的意图取第 N 个模式；缺对应模式的意图不做任何翻转。缺失该行或文件不存在即禁用。也接受 `mode=`。
- 模式取值：`a`（场景 A：`Formation.SetControlledByAI(false)`，触发条件 `SelectedAt>=0 && ExecutedAt<0 && Handoff.HasPending`）、`b`（场景 B：同上接口，触发条件 `ExecutedAt>=0`）、**`c`（场景 C：调公开原生 `Mission.JoinEnemyTeam()`，触发条件 `SelectedAt>=0`，见 §2.2）**。`c` 的恢复是 `mission.PlayerTeam = OriginalPlayerTeam`（只撤回自己的一次改动），与前两个模式的 `SetControlledByAI(true)` 不同。恢复保持窗口同为 ≥4.0 s。
- **恢复纪律（实现已按此写定）**：`SetControlledByAI(false)` 后保持失资格 ≥4.0 s（`SnapshotMaxAgeSeconds + 2.0`）再恢复；恢复由 probe 在每个 tick 检查到点后执行，**不依赖该意图仍然开放**——即使意图已被取消也要恢复，因为这只撤回 probe 自己的环境改动，且这是观测“资格恢复后旧意图不复活”的前提。恢复后本 probe 对该意图不再动作。
- 调用点（生产文件仅此一处新增）：`Program/src/Host/SamplingBehavior.cs` 在 `TestDriver.Instance.Tick(mission)` 之后、`TestDriver.Instance.DrainPendingCommand(mission)` 之前插入一行 `EligibilityProbe.OnMissionTick(mission);`。
  选这个时点的意义：**资格变化发生在当 tick 的产品检查点之后、主线程消费之前**，因此能同时压出两件事——
  (a) 紧随其后的 `DrainPendingCommand` 必须由**生产执行门**否决（写入计数不变）；
  (b) 再下一个 tick 的 `Tick` 检查点必须识别资格失效并取消+撤权（而不是只靠一次拒绝就宣称已让位）。
- 适配本体**不绕过生产过滤与执行门**，不产生额外 Movement Stop；失资格期间是否还能被原生选中、以及是否已撤权，由生产路径与只读采样给出。

---

## 5. 场景 A：已获选、待转交，资格变化后不得写令（intent-1）

**前置条件（缺一即不算命中本场景）：**

1. `REAL_SELECTION` 已出现（真实原生获选：本方 `CbHoldBehavior` 引用被原生设为 `ActiveBehavior`，由 `f.AI.OnActiveBehaviorChanged` 事件判定，非人工 `MarkSelected`）；
2. `HANDOFF_DEFERRED` 已出现且 `TestDriver.Instance.Handoff.HasPending == true`（真实待转交命令，由非主线程 `CbHoldBehavior.TickOccasionally` 投递）；
3. `_intent.ExecutedAt < 0` 且 `Gate.HasWritten(intent-1) == false`；
4. 对象是当前 Mission 中正常符合资格的目标（`key=1/0` 型非玩家队 AI 步兵）。

**触发：**满足上述条件时，`EligibilityProbe` 对该实际对象调用 `SetControlledByAI(false)` 一次，记 `PROBE_ELIGIBILITY_CHANGED`（含 before/after 全部实际值、tid、at）。此后同一 Mission 内不再对本意图翻转。

**观察点与预期：**

| 观察点 | 预期 |
|---|---|
| `DrainPendingCommand`（同 tick，紧接触发之后） | 走到 `TryExecuteStop`，`aiControlled=false` 即时求值 → `WRITE_BLOCKED reason=not_ai_controlled`（或等价的生产否决），**write_attempts 仍为 0**；无 `WRITE_EXECUTED` |
| 下一 Mission tick 的 `Tick` 检查点 | 新增的 `IsAiControlled` 判定命中 → `INTENT_CANCELLED intent=intent-1 reason=AiControlLost at=...` |
| 撤权 | `BEHAVIOR_DISARMED key=... reason=AiControlLost armed_before=1 weight_before=8.000 weight_after=0.000 active_behavior_now=...` |
| 待写命令失效 | `CancelIntent` 清槽位，记 `HANDOFF_CLEARED intent=intent-1 reason=AiControlLost had_pending=1`；此后本意图若再有入槽，按 `HANDOFF_DROPPED reason=intent_closed` 丢弃 |
| 回执/后置 | 本意图无 `WRITE_EXECUTED`，无 `REVOKE_EXECUTED`（未曾写入，按 `REVOKE_SKIPPED reason=no_own_write`） |

**通过判据：** `intent-1` 的写入计数增量为 **0**（即整个 intent-1 期间该意图一次都没写）；取消原因为 `AiControlLost`；有 `BEHAVIOR_DISARMED` 撤权日志。`SLOT_BUSY`、未获选、选择超时、“尚未执行”均**不**算通过。

---

## 6. 场景 B：命令已生效，资格变化后撤权且旧意图不复活（intent-2）

**阶段一（必须真实，复用既有通路）：** `intent-2` 取得 `REAL_SELECTION` → `HANDOFF_DEFERRED` → 主线程 `HANDOFF_EXECUTED` → `WRITE_EXECUTED order_before=Move order_after=StandYourGround` + 原方法入口回执（`original_body_entered=1`） + `orig_before/orig_after` 实际后值（回执结构沿用已验实现 `cbc9981`）。

**阶段二（资格变化，必须早于自动显式关闭与 20 s 截止）：**
- 触发条件：`SelectedAt>=0 && ExecutedAt>=0` 的**下一个 Mission tick**（约 execute+25 ms），`EligibilityProbe` 调 `SetControlledByAI(false)` 一次，记 `PROBE_ELIGIBILITY_CHANGED`。
- 预期：`INTENT_CANCELLED intent=intent-2 reason=AiControlLost`（**不是** `WindowExpired`、也不是 `ExplicitClose`）；`BEHAVIOR_DISARMED ... reason=AiControlLost armed_before=1 weight_before=8.000 weight_after=0.000`。
- 此后 `intent-2` 的 `write_attempts` 不再增加（停在场景 B 阶段一那一次写入对应的累计值）。**只命中 `already_written` 不算已让位。**

**阶段三（保持失资格 ≥2 s + 恢复 + 观察到截止后 ≥2 s）：**
- 失资格保持：自失资格时刻起 **≥2.0 s** 内 probe 不恢复（期间至少有 2 次 1 Hz 独立只读采样带上 `ai_ctrl=0`）。**观测来源必须是 CBATH 的 1 Hz 独立时间线**（`cbath/get_last_run` 在 `end_mission` 之后取，见 §8）；本方 probe 自身读出的 before/after **不能替代**这条判据（Codex 裁定）。
- 恢复：在 `intent-2` 原 `Deadline` **之前**调用 `SetControlledByAI(true)`，记 `PROBE_ELIGIBILITY_RESTORED`（同样含 before/after 全部实际值）；
- 观察至 `Deadline + 2.0 s` 之后：**`intent-2` 不得再出现 `WRITE_EXECUTED` / `BEHAVIOR_REGISTERED`（本方重武装）/ `REAL_SELECTION` / `HANDOFF_EXECUTED`**；按 intent id 核对写入计数，不得因为后续（本场无）新意图的合法写入被误算。
- 恢复时的原生副作用必须逐项记录：`SetControlledByAI(true)` 内部会 `ForceTickOccasionally=true; AI.Tick(); ...; AI.ActiveBehavior.OnBehaviorActivated(); SetMovementOrder(AI.ActiveBehavior.CurrentOrder);`（`Formation.cs:788-810`）——即**恢复本身会让原生重新推一次当前最优行为的命令**。本方此时已撤权（weight 0、`GetAiWeight()=0`），应当不再参与选择。不得把此次原生重推当作“本方复活”，也不得隐瞒它。
- probe 恢复时**只撤回自己的那一次 `SetControlledByAI(false)`**：不重发不可忠实重建的旧 Move、不调 `ResetBehaviorWeights`、不覆盖已发生的新决策。

---

## 7. 允许的生产修复清单（只补“资格失效”这一条事实）

1. `Program/src/Core/TestParameters.cs`：`CancelReason` 增 `AiControlLost`。
2. `Program/src/Host/TestDriver.cs` → `Tick` 的绑定目标检查：在原有 `likelyPlayerOwned(bound)` 之后补
   `if (!bound.IsAiControlled) { CancelIntent(CancelReason.AiControlLost, now); return; }`
   （`_snapshot` 在本 tick 开头刚采样，是当 tick 真实值。）
3. `CancelIntent`：取消时清掉待写命令 `_handoff.Clear();` 并记 `HANDOFF_CLEARED ... had_pending=`，使“待写命令失效”可核查。
4. `DrainPendingCommand`：取出 pending 后若意图已关闭，由静默 return 改为记 `HANDOFF_DROPPED reason=intent_closed`。
5. `DisarmAllBehaviors`：`BEHAVIOR_DISARMED` 日志改为记录 `armed_before`、`weight_before`（实读 `WeightFactor`）、`weight_after`、`active_behavior_now`，不再写死 `weight=0`，让“撤权”可被独立核对。
6. 给 `TestDriver` 加只读访问器 `internal ThreadHandoff Handoff { get; }`，供 probe 判定“真实待转交已存在”（读取用，不改写入路径、不碰锁内 setter）。

**不做**：不改真实获选判据、不改主线程执行点、不动槽位锁、不改已批准的时间参数；不 Introduce 通用注入平台。

---

## 8. 本场提取形态与写入计数口径

- 同一场 Mission，两个独立有限意图：`intent-1` → 场景 A（mode=A），`intent-2` → 场景 B（mode=B）；同一时刻只保留一个活动意图（现阶段机 A→B 间隔 5 s，保持不变）。
- `intent-2` 被资格取消后，`SamplingBehavior.StageIntentBRunning` 会因 `!HasOpenIntent` 转 `StageDone`，`EXPLICIT_CLOSE_SCHEDULED` 不会触发 —— ExplicitClose 路径本场**不重跑**，沿用 run 7 既有证据。
- 写入计数一律按 intent id 取该意图生命周期内 `Gate.WriteAttempts` 的**增量**：场景 A 期望 0；场景 B 期望在场景 B 阶段一写令时 +1，此后不再变化。
- Exit Gate 3 的适用 FD-PoC 判据按 C 路线记录：**停止执行 / 撤销本方行为影响 / 旧意图不复活**；不套 Route B 的释放租约指标。真实玩家接管与阵亡按第 2 节登记为**不适用**（不混用标签）。

---

## 9. 失败时的处理纪律（派单 §5）

- 先跑一场最小验证；只有判据未触发、失败或源码变更才补场。
- 判据未触发时，先改触发条件或引入能区分原因的新证据，不在同一段源码/日志上反复猜。
- 安全路径失败就修具体原因，**不放宽判据**；需要扩大控制域或改路线图门槛才能通过时，停止并交回矛盾与证据，不自行豁免门槛、不以“工具不可达”登记 PASS。
- 启动期环境失败（如既有记录的 Granite 缓存初始化失败）单独记录，不自动认定与代码无因果。
