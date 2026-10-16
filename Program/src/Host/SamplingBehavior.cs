using System;
using System.IO;
using CombatBrain2.Core;
using TaleWorlds.MountAndBlade;
using AgentState = TaleWorlds.Core.AgentState;

namespace CombatBrain2.Host
{
    // Mission 行为：驱动采样、按决策节奏形成计划、观测原生写入与效果反馈。
    // 所有写令都经由 TestDriver，且只在主线程 Mission tick 内发生。
    //
    // 写令来源由哨兵文件 p1-command-loop.txt 的 enabled 决定：
    //   开启 → P1 节奏：3 s 决策、逐 tick 条件与安全、每 tick 反馈与槽位消费；
    //   关闭（默认）→ 不产生任何计划与命令，只保留采样与观测。
    internal sealed class SamplingBehavior : MissionBehavior
    {
        public override MissionBehaviorType BehaviorType
        {
            get { return MissionBehaviorType.Other; }
        }

        private bool _started;
        private bool _hooked;
        private double _nextSampleAt;
        private double _lastObserveAt = -1.0;

        // P1 节奏状态。
        private bool _p1Loaded;
        private bool _p1Enabled;
        private double _nextDecisionAt = -1.0;
        private int _evalCount;

        // P2 节奏状态。与 P1 互斥：两者同时开启时不跑任何一路。
        private bool _p2Enabled;
        private double _nextP2DecisionAt = -1.0;
        private int _p2EvalCount;
        // 本场实际进入过 P2 骨架。P3 反馈可以把循环降级到 P1，所以不能用
        // "当前是否处于 P2 模式"判断本场是否跑过 P2 路径。
        private bool _p2Ran;

        // P3 结果反馈状态。它是 P2 循环上的一个选项，不产生独立写令循环。
        private bool _p3Enabled;

        private static string ConfigPathOf(string fileName)
        {
            return Path.Combine(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CombatBrain2"),
                fileName);
        }

        private static string P1ConfigPath()
        {
            return ConfigPathOf("p1-command-loop.txt");
        }

        private static string P2ConfigPath()
        {
            return ConfigPathOf("p2-command-loop.txt");
        }

        private static string P3ConfigPath()
        {
            return ConfigPathOf("p3-feedback.txt");
        }

        // 读哨兵文件里的 enabled。文件缺失或未设 = 关闭。
        private static bool ReadEnabledFlag(string path)
        {
            string value = ReadFlagValue(path, "enabled");
            return string.Equals(value, "1", StringComparison.Ordinal) ||
                   string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);
        }

        // 开关之外的参数也走同一格式，便于测试注入显式配置。
        private static string ReadFlagValue(string path, string key)
        {
            string value = "";
            try
            {
                if (File.Exists(path))
                {
                    foreach (var raw in File.ReadAllLines(path))
                    {
                        string line = raw == null ? "" : raw.Trim();
                        if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                        int eq = line.IndexOf('=');
                        if (eq <= 0) continue;
                        if (string.Equals(line.Substring(0, eq).Trim(), key, StringComparison.OrdinalIgnoreCase))
                        {
                            value = line.Substring(eq + 1).Trim();
                        }
                    }
                }
            }
            catch (Exception) { }
            return value;
        }

        private void LoadP1Once()
        {
            if (_p1Loaded) return;
            _p1Loaded = true;

            string p1Value = ReadFlagValue(P1ConfigPath(), "enabled");
            string p2Value = ReadFlagValue(P2ConfigPath(), "enabled");
            bool p1Wanted = IsOn(p1Value);
            bool p2Wanted = IsOn(p2Value);

            // P1 与 P2 互斥：两条路径都会写同一批编队的 MovementOrder，
            // 同时开启无法归因，因此拒绝运行，不静默挑一条。
            bool both = p1Wanted && p2Wanted;
            _p1Enabled = p1Wanted && !both;
            _p2Enabled = p2Wanted && !both;

            // P3 是 P2 写令循环上的结果反馈选项：它不产生独立写令循环，
            // 只在 P2 已运行时才有效。缺失/未设 = 关闭。
            string p3Value = ReadFlagValue(P3ConfigPath(), "enabled");
            bool p3Wanted = IsOn(p3Value);
            _p3Enabled = p3Wanted && _p2Enabled;

            Cb2Log.Line("P1_MODE",
                "enabled=" + (_p1Enabled ? 1 : 0) +
                " value=" + (p1Value.Length == 0 ? "<unset>" : p1Value) +
                " source=p1-command-loop.txt" +
                " disabled_meaning=no_command_source");

            Cb2Log.Line("P2_MODE",
                "enabled=" + (_p2Enabled ? 1 : 0) +
                " value=" + (p2Value.Length == 0 ? "<unset>" : p2Value) +
                " source=p2-command-loop.txt" +
                (both ? " rejected=both_enabled" : "") +
                " p1_enabled=" + (_p1Enabled ? 1 : 0) +
                " disabled_meaning=no_command_source");

            Cb2Log.Line("P3_MODE",
                "enabled=" + (_p3Enabled ? 1 : 0) +
                " value=" + (p3Value.Length == 0 ? "<unset>" : p3Value) +
                " source=p3-feedback.txt" +
                " requires=p2" +
                " p2_enabled=" + (_p2Enabled ? 1 : 0) +
                (p3Wanted && !_p2Enabled ? " rejected=p2_not_enabled" : "") +
                " disabled_meaning=no_action_feedback");
        }

        private static bool IsOn(string value)
        {
            return string.Equals(value, "1", StringComparison.Ordinal) ||
                   string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);
        }

        public override void OnMissionTick(float dt)
        {
            var mission = Mission.Current;
            if (mission == null) return;

            LoadP1Once();

            if (!_started)
            {
                _started = true;
                TestDriver.Instance.OnMissionStart(mission);
                TestDriver.Instance.MarkMainThread(CurrentThreadId());
                Cb2Log.Line("BEHAVIOR_START",
                    "sampling_behavior_initialized tid=" + CurrentThreadId() +
                    " p1_enabled=" + (_p1Enabled ? 1 : 0) +
                    " p2_enabled=" + (_p2Enabled ? 1 : 0));
            }

            double now = 0.0;
            try { now = (double)mission.CurrentTime; } catch (Exception) { }

            // 1 Hz 独立只读观测。
            if (now >= _nextSampleAt)
            {
                _nextSampleAt = now + 1.0;
                var reading = IndependentObservation.Read();
                Cb2Log.Line("INDEPENDENT_SAMPLE",
                    "mission=" + reading.MissionKey +
                    " at=" + Cb2Log.F(reading.At) +
                    " " + string.Join(" | ", reading.Lines.ToArray()));
            }

            // 测试适配：只在哨兵文件开启时改变一个真实编队的 AI 控制资格。
            // 是否推进 P2 适配由本层自己记住，不依赖 TestDriver 的命令模式：
            // P3 反馈可以把循环降级到 P1，那之后 P2 适配仍需按计划恢复资格。
            EligibilityProbe.Instance.OnMissionTick(mission, now, _p2Ran);

            if (!_hooked)
            {
                _hooked = true;
                HookFormations(mission);
            }

            if (_p1Enabled) TickP1(mission, now);
            else if (_p2Enabled) TickP2(mission, now);

            // 每 2 秒独立复读实际 MovementOrder，观测是否被原生改写。
            if (now >= _lastObserveAt + 2.0)
            {
                _lastObserveAt = now;
                TestDriver.Instance.ObservePostState(mission);
            }
        }

        // P2 节奏：与 P1 同一套决策骨架，但建立协同计划并在逐 tick 复核依赖。
        private void TickP2(Mission mission, double now)
        {
            _p2Ran = true;
            TestDriver.Instance.SetCommandLoop(true, _p3Enabled);

            if (_nextP2DecisionAt < 0.0)
            {
                _nextP2DecisionAt = now + TestParameters.FirstCommandDelaySeconds;
                Cb2Log.Line("P2_FIRST_DECISION_SCHEDULED",
                    "at=" + Cb2Log.F(_nextP2DecisionAt) +
                    " first_command_delay=" + Cb2Log.F(TestParameters.FirstCommandDelaySeconds) +
                    " decision_cadence=" + Cb2Log.F(TestParameters.DecisionCadenceSeconds));
            }

            if (now >= _nextP2DecisionAt)
            {
                _nextP2DecisionAt = now + TestParameters.DecisionCadenceSeconds;
                _p2EvalCount++;
                Cb2Log.Line("EVAL_TICK",
                    "eval=" + _p2EvalCount +
                    " mode=p2" +
                    " at=" + Cb2Log.F(now) +
                    " cadence=" + Cb2Log.F(TestParameters.DecisionCadenceSeconds));
                TestDriver.Instance.DecisionTick(mission);
            }

            // 候选投递按逐 tick 进行：决策仍每 3 s 一次，但已经公布、满足执行门的任务
            // 在首个可用主线程 tick 就要投递，不能再等下一个评价点——多出的整段决策周期
            // 会让继任计划在原生命令覆盖前都还没 arm 上行为。
            // 幂等与延迟/顺序/间隔门由 SubmitPending 与 TaskDelivery 保证。
            TestDriver.Instance.SubmitPending(mission);
            // 提案收集与公布同样逐 tick：写前门只消费已公布结果，且必须在投递之前完成本轮收题，
            // 否则首个到达写前门的提案会被当作唯一竞争者提前公布。
            TestDriver.Instance.PublishCoordinationProposals(mission, now);

            // 协同观察按逐 tick 进行：依赖是否成立不能等到下个 3 s 评价点，
            // 否则"保护已离开到位区"会被延迟发现。
            TestDriver.Instance.CoordinationTick(mission);

            TestDriver.Instance.FeedbackTick(mission);
            TestDriver.Instance.DrainPendingCommand(mission);
            TestDriver.Instance.SweepExitStops(mission);
        }

        // P1 节奏：决策按 3 s，条件/安全/反馈按逐 tick，槽位消费在主线程执行点。
        private void TickP1(Mission mission, double now)
        {
            TestDriver.Instance.SetCommandLoop(false, false);

            if (_nextDecisionAt < 0.0)
            {
                _nextDecisionAt = now + TestParameters.FirstCommandDelaySeconds;
                Cb2Log.Line("P1_FIRST_DECISION_SCHEDULED",
                    "at=" + Cb2Log.F(_nextDecisionAt) +
                    " first_command_delay=" + Cb2Log.F(TestParameters.FirstCommandDelaySeconds) +
                    " decision_cadence=" + Cb2Log.F(TestParameters.DecisionCadenceSeconds));
            }

            if (now >= _nextDecisionAt)
            {
                _nextDecisionAt = now + TestParameters.DecisionCadenceSeconds;
                _evalCount++;
                Cb2Log.Line("EVAL_TICK",
                    "eval=" + _evalCount +
                    " at=" + Cb2Log.F(now) +
                    " cadence=" + Cb2Log.F(TestParameters.DecisionCadenceSeconds));
                TestDriver.Instance.DecisionTick(mission);
            }

            // 与 P2 同一口径：决策仍每 3 s 一次，投递改为逐 tick。
            // 已经满足执行门的任务不得再等下一个评价点；幂等由任务自身的提交状态保证。
            TestDriver.Instance.SubmitPending(mission);

            // 安全资格与条件逐 tick 检查，不受决策节奏与稳定时间约束。
            TestDriver.Instance.FeedbackTick(mission);

            // 主线程执行点：消费非主线程回调转交过来的待执行命令。
            TestDriver.Instance.DrainPendingCommand(mission);

            // 退出动作到点未获选即如实记录并撤权，安全时限不受发令间隔约束。
            TestDriver.Instance.SweepExitStops(mission);
        }

        // Route C 的两个原生信号：
        //  1) FormationAI.OnActiveBehaviorChanged —— 原生把某个行为选为 ActiveBehavior，
        //     这是"本方获选"的唯一判据，判定靠行为引用归属；
        //  2) Formation.OnBeforeMovementOrderApplied —— 原生把命令应用到编队，仅作观测。
        // 两者都只订阅原生自带事件，不 patch、不改原生行为。
        private void HookFormations(Mission mission)
        {
            int hooked = 0;
            try
            {
                foreach (var team in mission.Teams)
                {
                    if (team == null) continue;
                    foreach (var f in team.FormationsIncludingEmpty)
                    {
                        if (f == null) continue;
                        f.AI.OnActiveBehaviorChanged += OnActiveBehaviorChanged;
                        f.OnBeforeMovementOrderApplied += OnNativeMovementOrder;
                        hooked++;
                    }
                }
            }
            catch (Exception e)
            {
                Cb2Log.Line("HOOK_ERROR", e.GetType().Name + ": " + e.Message);
            }
            Cb2Log.Line("HOOK_FORMATIONS", "count=" + hooked);
        }

        private void OnActiveBehaviorChanged(Formation f)
        {
            var mission = Mission.Current;
            if (mission == null || f == null) return;
            TestDriver.Instance.OnActiveBehaviorChanged(mission, f, CurrentThreadId());
        }

        private void OnNativeMovementOrder(Formation f, MovementOrder.MovementOrderEnum orderEnum)
        {
            // 原方法完成/跳过回执：该事件是 SetMovementOrder 方法体的第一句，
            // 在武装窗口内出现即证明本次调用的原方法体真的被执行；被跳过时不会出现。
            TestDriver.Instance.ObserveOriginalEnter(f, CurrentThreadId());

            // 本方自己的 setter 调用不算原生应用。
            if (TestDriver.InOurWrite) return;

            var mission = Mission.Current;
            if (mission == null) return;

            TestDriver.Instance.OnNativeMovementOrderApplied(mission, f, orderEnum, CurrentThreadId());
        }

        // 回调线程核对：OnBeforeMovementOrderApplied 在调用 SetMovementOrder 的线程上同步触发。
        // 记录下来才能证明写令确实发生在主线程执行点。
        private static int CurrentThreadId()
        {
            try { return System.Threading.Thread.CurrentThread.ManagedThreadId; }
            catch (Exception) { return -1; }
        }

        // P3 终态捕获：Mission 的公开移除回调在写 State 之后逐行为调用，
        // 这里只读实际终态并交给 TestDriver 记账，不用 1 Hz 轮询（会漏终态）。
        // OnAgentDeleted 晚于 OnAgentRemoved，同一成员由台账按身份去重。
        public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent,
                                            AgentState agentState, KillingBlow blow)
        {
            if (!_p3Enabled) return;
            if (affectedAgent == null) return;
            double now = 0.0;
            try
            {
                var mission = Mission.Current;
                if (mission != null) now = (double)mission.CurrentTime;
            }
            catch (Exception) { }
            TestDriver.Instance.OnAgentTerminal(affectedAgent, agentState, now);
        }

        public override void OnAgentDeleted(Agent affectedAgent)
        {
            if (!_p3Enabled) return;
            if (affectedAgent == null) return;
            double now = 0.0;
            try
            {
                var mission = Mission.Current;
                if (mission != null) now = (double)mission.CurrentTime;
            }
            catch (Exception) { }
            TestDriver.Instance.OnAgentTerminal(affectedAgent, AgentState.Deleted, now);
        }

        protected override void OnEndMission()
        {
            TestDriver.Instance.OnMissionEnd();
        }
    }
}
