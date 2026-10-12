<!-- PROMPT META
Name: CombatBrain 2.0 PAIPOS Workflow Entry
Version: 1.1
Author Role: Project Governance Maintainer
Model: GPT-6
Created: 2026-10-02
Updated: 2026-10-12
Target: All CombatBrain 2.0 Harness Agents
Scope: CombatBrain 2.0 / Workflow
-->

<!-- CHANGE LOG (newest first, keep last 3)
v1.1 2026-10-12 13:30 | role=Project Governance Maintainer | model=GPT-6 | editor=codex-cb2-observation-isolation-20261012 | note=规定 CB2 与观测组独立工作树、文件归属、接入合并与共享实机串行边界
v1.0 2026-10-02 00:18 | role=Project Governance Maintainer | model=GPT-6 | editor=codex-paipos-fix-20261002 | note=在实际 2.0 Git 根建立自动加载入口，直接注入完整 PAIPOS Lite 正文
v1.0a 2026-10-02 | role=Project Governance Maintainer | model=GPT-6 | editor=cbath-freeze-20260925 | note=新增 §3a：进游戏实测统一使用 CBATH（Zero-touch 入口 + 硬性前置 + 冻结说明）
-->

# CombatBrain 2.0 项目入口

本文件适用于本工作区的开发、设计和质检角色。PAIPOS Lite 全文直接置于下方，自动加载本文件即加载工作流，不依赖其他会话或外部文件。

- 本入口生命周期登记在本工作区的 `prompts/registry.json`；开始任务先确认本入口为 ACTIVE。未读取登记表时不得声称已经核对生命周期。
- 接手时简短说明当前角色、已加载的 PAIPOS Lite v0.1，以及当前任务的验证方式。不要求用户填写模板。
- 工作流负责理解需求、执行、验证和交接；涉及产品或架构判断时读取 `charter/Charter V0.2.md`，任务相关材料按需加载。2.0 当前实现和状态须以当前文件及证据核对。
- `E:\MBmod\CombatAI` 与 `E:\Agent\6a9268112aeaf49072bef052` 是其他工作区；不自动继承其冻结令、任务状态、建组规则或旧设计。仅在明确需要旧资产证据时读取。
- 角色切换由用户在另一 Harness 发起，不自动建组或调度其他角色。小而可控的修改自检，跨模块、核心行为或重要交付增加独立质检。
- 不自动加载 AGENTS.md 的 Harness，需将本文件作为该项目的启动指令；仅文件存在不能作为实际加载成功的证据。

---

# PAIPOS Lite v0.1

个人 AI 项目工作流 · 试运行草案 · 2026-10-01

**用户自然表达，AI 负责理解、执行和验证；需要换角色时，由用户手动切换 Harness。**本文件供当前角色读取，不要求用户填写模板。

## 1. 自然表达

- 需求、报错、困惑和修改意见都可以直接说。AI 结合当前材料整理，不要求用户先写任务说明。
- 只有歧义会明显改变行为、结果或代价时才追问；其余说明必要假设后继续。讨论和设想不自动视为实施决定。
- 明确的新指示更新当前工作；要求暂停或切换优先级时，保存可恢复进度。已授权目标内自主推进，不擅自增加功能；改变目标、突破硬约束或增加重要投入时，提出具体方案请用户决定。

## 2. 按意图选择入口

| 用户当前想做什么 | 初始角色 |
|---|---|
| 修报错、纠正行为、实现明确功能 | 开发 |
| 比较方案、判断方向、梳理说不清的体验问题 | 设计 |
| 检查已经交付的结果 | 质检 |

入口不决定最终归属，用户不必预先判断是不是架构问题。角色按职责划分，模型按实际能力和任务需要选择；Flash 开发、GPT 设计等只作为起始配置。

## 3. 接手者先判断，再推进

- 修改前核对预期、实际现象和影响范围，并明确如何检查完成。局部实现问题直接修复，不默认全面审计。
- 证据不足先补证据；诊断能力不足可以建议换强模型，不能因此认定架构错误。需要改变目标、模块职责或核心约定时，提供具体矛盾，建议交给设计。
- 遇到“代码改了但行为没变”，先确认运行的是新构建，再确认目标代码执行，最后分析源码逻辑。同一证据反复分析仍无进展时，改用新的验证手段。

## 3a. 进游戏实测：统一使用 CBATH

- **需要进游戏做实际测试/验证时（Bannerlord 实机运行），统一使用 CBATH（CombatBrain Autonomous Test Harness）作为唯一入口**，不另起炉灶手写进游戏脚本、不手动按 UI、不自建 input simulation。
- 入口与执行：`E:\MBmod\CombatBrain Autonomous test harness\tools\zero-touch-3x.ps1`（标准执行 `powershell -File tools\zero-touch-3x.ps1 -Rounds 3 -CombatHoldSec 180`）；基线/收口细节见同仓库 `RD\CombatBrain-Autonomous-Test-Harness\16-Phase2-Freeze-CBATH-Baseline.md`。
- 硬性前置：①框架模块（Harmony/ButterLib/UIExtenderEx/MCM）必须排在 `Native` 之前，否则表现为“崩溃”（全线程 Suspended、零日志）；②启动前 `safely_exited = 1`（先杀进程再复位）；③确认 steam 在跑（被强杀会带走 steam.exe 主进程，需重启等 45s+）。
- CBATH 已冻结，新能力需求驱动；`cb-legacy-offset2mission.ps1`/`cb-correlate.ps1` 只读 CombatBrain 日志做关联，不是进游戏入口。

## 3b. CB2 与 Observation adapter 并行开发

本节依据用户 2026-10-12 的隔离要求，避免两组在同一目录覆盖未提交工作。规则只改变开发协作，不改变 P1 战术要求、观测功能范围或验收判据。

- **开发目录分开**：CB2 主线使用 `E:\MBmod\CombatBrain 2.0`；观测组使用自己的 CB2 Git worktree 和 `codex/` 分支，不再写主线目录。观测组涉及的 CBATH、TCL 也各用自己的工作树。Git 分支本身不会隔离磁盘文件，不能在共享目录切分支冒充隔离。当前工作树路径以 `git worktree list` 和观测组的隔离交接为准；未核实目录前不声称隔离完成。
- **文件归属**：CB2 组维护 `Program/src/Core/`、`TestDriver.cs`、`FormationSampler.cs`、`SamplingBehavior.cs`、`EligibilityProbe.cs`、原生行为与命令执行。观测组维护 `Program/src/Host/ObservationAdapter.cs`，以及自身 CBATH `Observation/`、TCL 观测工具、测试和查看器。CB2 的 `IndependentObservation.cs` 是现有验收采样，不因名称相似移交给观测组。
- **共有接入由 CB2 组合入**：`TestDriver.cs` 内的事件位置、字段及生命周期接入，以及 `CombatBrain2.csproj`、构建身份、部署/实测脚本，由 CB2 组作为单一合入方。观测组可在自己的工作树制作最小接入补丁，交付基线提交、准确文件范围与 diff；不得直接回写主线。CB2 组重新读取当前内容并审查合入，不能拿旧的完整文件覆盖。观测组改 CBATH 工程/启动接入时同样提交独立补丁，不把原工作区的他方未提交改动顺手归入。
- **接口按约定调整**：现有 `Resolve/Emit/Shutdown` 接口及事件字段是双方接入约定，不在修战术行为时随意改名、删除或吞掉；观测组也不得借增加记录改变资格门、计划条件、发令次数或反馈判据。需要改变接口或移动事件位置时，随补丁写清理由与双方调用影响，再由 CB2 组合入。此次先保留现有目录与已提交适配器，不重写历史、不另建 SDK。
- **提交只收本组改动**：操作前核对工作树绝对路径、分支、HEAD 和脏文件；只按明确文件范围暂存，禁止用 `git add -A`、`git commit -a` 收走另一组成果。其他组拥有的文件有新变化时保留现场，不能还原为自己的旧副本。混合提交不能直接整体 cherry-pick，需按本次交付范围审查合入。
- **构建在本组目录，部署与实机串行**：两组独立构建各自 `bin/obj`，不得清理另一组输出。游戏模块、GABS 配置、Documents 下的观测身份文件、P1/资格探针开关和游戏进程仍是共享资源；同一时间只有一组使用这些资源。未获得当前实测方交接时只做离线开发，不部署、不启动/结束游戏、不改测试开关、不停另一组进程。交接包含来源路径/提交、实际 DLL SHA256、当前配置、开关状态和场次目录；接手方核对后使用。独立工作树不等于独立实机环境。
- **验收分开**：观测组功能不借 P1 通过状态自动通过；P1 也不把观测组件纳入其战术整改贡献。提交 `616b903` 已包含适配器，保持该历史；后续观测修复在观测分支提交、经接入审查合入。P1 当前仍待独立验收。

## 4. 按影响决定检查力度

- 小而可控的修改：开发执行、自检后结束。跨模块、核心行为或重要交付：增加独立质检，不强制每次经过三个角色。
- 检查既要验证约定行为，也要判断是否回应了原始需求。需求或产物改变后，重新核对旧验证是否适用。
- 完成时简短说明：改了什么、验证了什么、还有什么未确认。没有执行的验证不能写成通过。

## 5. 需要切换时准备交接

- 当前角色说明为什么需要切换、交给谁、需要解决哪个具体问题。由用户在另一个 Harness 发起，不自动调度，也不假设共享聊天历史或文件。
- 当前角色整理一份简短交接：原始需求和有效约束；当前代码或产物版本及未完成工作；已确认事实和证据；未确认事项；接手者的具体任务。用户不必重新组织需求。
- 接手者核对可访问的当前材料；前一模型的总结作为线索，不能代替证据。无法访问的材料明确指出。
- 只保存恢复、交接或重要决定需要的记录。小修改不强制创建任务文件和状态表；切换没有带来新证据或决定时，先补验证或提出必要取舍。

先用于真实任务，再按反复出现的问题修订。判断标准：用户能否少解释、少催促，并清楚知道结果和下一步。
