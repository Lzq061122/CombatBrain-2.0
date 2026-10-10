using System;
using CombatBrain2.Core;
using TaleWorlds.MountAndBlade;

namespace CombatBrain2.Host
{
    // Mission 行为：驱动采样、触发测试意图、观测原生写入。
    // 所有写令都经由 TestDriver，且只在主线程 Mission tick 内发生。
    internal sealed class SamplingBehavior : MissionBehavior
    {
        public override MissionBehaviorType BehaviorType
        {
            get { return MissionBehaviorType.Other; }
        }

        // 测试序列阶段。两条取消路径都必须实际触发：
        //   阶段 1/2 → 意图 A 自然到期（窗口到期取消）
        //   阶段 3/4 → 意图 B 由测试驱动器显式关闭（显式测试关闭）
        //   Mission 结束 → OnEndMission 清理（Mission 结束取消）
        private const int StageWaitDeploy = 0;
        private const int StageArmIntentA = 1;
        private const int StageIntentARunning = 2;
        private const int StageArmIntentB = 3;
        private const int StageIntentBRunning = 4;
        private const int StageDone = 5;

        private int _stage = StageWaitDeploy;
        private double _stageAt = -1.0;
        private double _closeAt = -1.0;
        private double _lastDeployProbeAt = -1.0;

        private bool _started;
        private bool _hooked;
        private double _nextSampleAt;
        private double _lastObserveAt = -1.0;
        private int _tickCount;

        public override void OnMissionTick(float dt)
        {
            var mission = Mission.Current;
            if (mission == null) return;

            if (!_started)
            {
                _started = true;
                TestDriver.Instance.OnMissionStart(mission);
                TestDriver.Instance.MarkMainThread(CurrentThreadId());
                Cb2Log.Line("BEHAVIOR_START",
                    "sampling_behavior_initialized tid=" + CurrentThreadId());
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

            TestDriver.Instance.Tick(mission);

            // 主线程执行点：消费非主线程回调转交过来的那一条命令。
            TestDriver.Instance.DrainPendingCommand(mission);

            if (!_hooked)
            {
                _hooked = true;
                HookFormations(mission);
            }

            AdvanceStage(mission, now);

            // 意图仍开着且尚未获选时，重复提交候选（证明候选阶段不写令）。
            // 一旦真实获选就停止提交，避免目标失效后偷偷换另一支继续同一意图。
            if (TestDriver.Instance.HasOpenIntent && !TestDriver.Instance.Selected)
            {
                _tickCount++;
                if (_tickCount % 5 == 0)
                {
                    TestDriver.Instance.SubmitCandidateOnly(mission);
                }
            }

            // 每 2 秒独立复读实际 MovementOrder，观测是否被原生改写。
            if (now >= _lastObserveAt + 2.0)
            {
                _lastObserveAt = now;
                TestDriver.Instance.ObservePostState(mission);
            }
        }

        private void AdvanceStage(Mission mission, double now)
        {
            switch (_stage)
            {
                case StageWaitDeploy:
                    {
                        bool deployed = false;
                        bool aiTicking = false;
                        try { deployed = mission.IsDeploymentFinished; } catch (Exception) { }
                        try { aiTicking = mission.AllowAiTicking; } catch (Exception) { }
                        if (deployed || aiTicking)
                        {
                            _stageAt = now + TestParameters.TriggerDelaySeconds;
                            _stage = StageArmIntentA;
                            Cb2Log.Line("TRIGGER_SCHEDULED",
                                "intent=A at=" + Cb2Log.F(_stageAt) +
                                " deployment_finished=" + (deployed ? 1 : 0) +
                                " allow_ai_ticking=" + (aiTicking ? 1 : 0));
                        }
                        else if (now >= _lastDeployProbeAt + 5.0)
                        {
                            _lastDeployProbeAt = now;
                            Cb2Log.Line("DEPLOY_GATE_WAIT",
                                "at=" + Cb2Log.F(now) +
                                " deployment_finished=0 allow_ai_ticking=0");
                        }
                        break;
                    }

                case StageArmIntentA:
                    if (now >= _stageAt)
                    {
                        if (TestDriver.Instance.TriggerIntent(mission))
                        {
                            TestDriver.Instance.SubmitCandidateOnly(mission);
                            _stage = StageIntentARunning;
                        }
                    }
                    break;

                // 意图 A 自然到期后进入下一阶段，两条路径都由 TestDriver 记日志。
                case StageIntentARunning:
                    if (!TestDriver.Instance.HasOpenIntent)
                    {
                        _stageAt = now + TestParameters.BetweenIntentSeconds;
                        _stage = StageArmIntentB;
                        Cb2Log.Line("SEQUENCE_INTENT_A_CLOSED",
                            "at=" + Cb2Log.F(now) + " next_intent_at=" + Cb2Log.F(_stageAt));
                    }
                    break;

                case StageArmIntentB:
                    if (now >= _stageAt)
                    {
                        if (TestDriver.Instance.TriggerIntent(mission))
                        {
                            TestDriver.Instance.SubmitCandidateOnly(mission);
                            _stage = StageIntentBRunning;
                        }
                    }
                    break;

                case StageIntentBRunning:
                    if (!TestDriver.Instance.HasOpenIntent)
                    {
                        _stage = StageDone;
                        Cb2Log.Line("SEQUENCE_INTENT_B_CLOSED", "at=" + Cb2Log.F(now));
                        break;
                    }
                    // 已执行写令后固定延迟显式关闭，验证显式关闭路径。
                    if (_closeAt < 0 && TestDriver.Instance.Executed)
                    {
                        _closeAt = now + TestParameters.ExplicitCloseAfterWriteSeconds;
                        Cb2Log.Line("EXPLICIT_CLOSE_SCHEDULED", "at=" + Cb2Log.F(_closeAt));
                    }
                    if (_closeAt > 0 && now >= _closeAt)
                    {
                        TestDriver.Instance.ExplicitClose(mission);
                        _stage = StageDone;
                    }
                    break;

                case StageDone:
                    break;
            }
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
