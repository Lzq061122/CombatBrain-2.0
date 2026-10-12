using System;
using System.IO;
using CombatBrain2.Core;
using TaleWorlds.MountAndBlade;

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

        private static string P1ConfigPath()
        {
            return Path.Combine(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CombatBrain2"),
                "p1-command-loop.txt");
        }

        private void LoadP1Once()
        {
            if (_p1Loaded) return;
            _p1Loaded = true;
            string value = "";
            try
            {
                string p = P1ConfigPath();
                if (File.Exists(p))
                {
                    foreach (var raw in File.ReadAllLines(p))
                    {
                        string line = raw == null ? "" : raw.Trim();
                        if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                        int eq = line.IndexOf('=');
                        if (eq <= 0) continue;
                        if (string.Equals(line.Substring(0, eq).Trim(), "enabled",
                                StringComparison.OrdinalIgnoreCase))
                        {
                            value = line.Substring(eq + 1).Trim();
                        }
                    }
                }
            }
            catch (Exception) { }

            _p1Enabled = string.Equals(value, "1", StringComparison.Ordinal) ||
                         string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(value, "on", StringComparison.OrdinalIgnoreCase);

            Cb2Log.Line("P1_MODE",
                "enabled=" + (_p1Enabled ? 1 : 0) +
                " value=" + (value.Length == 0 ? "<unset>" : value) +
                " source=p1-command-loop.txt" +
                " disabled_meaning=no_command_source");
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
                    " p1_enabled=" + (_p1Enabled ? 1 : 0));
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
            EligibilityProbe.Instance.OnMissionTick(mission);

            if (!_hooked)
            {
                _hooked = true;
                HookFormations(mission);
            }

            if (_p1Enabled) TickP1(mission, now);

            // 每 2 秒独立复读实际 MovementOrder，观测是否被原生改写。
            if (now >= _lastObserveAt + 2.0)
            {
                _lastObserveAt = now;
                TestDriver.Instance.ObservePostState(mission);
            }
        }

        // P1 节奏：决策按 3 s，条件/安全/反馈按逐 tick，槽位消费在主线程执行点。
        private void TickP1(Mission mission, double now)
        {
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
                // 决策之后立即在同一个主线程 tick 内投递候选，缩短评价到获选的间隔。
                TestDriver.Instance.SubmitPending(mission);
            }

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

        protected override void OnEndMission()
        {
            TestDriver.Instance.OnMissionEnd();
        }
    }
}
