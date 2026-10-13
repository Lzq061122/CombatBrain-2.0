using CombatBrain2.Core;
using Xunit;

namespace CombatBrain2.Tests
{
    // P2 测试适配的触发判据反例（派单 §9.2）。
    // 重点钉住两件事：A 与 B 的相位不同；以及永远取版本绑定的实际保护对象。
    public class EligibilityTriggerTests
    {
        // 场景 A 的完整前置：关系已建立、主力已推进、计划稳定足够久。
        private static EligibilityTriggerInput Established()
        {
            return new EligibilityTriggerInput
            {
                Mode = P2AdaptationMode.GuardLossAfterEstablished,
                PlanOpen = true,
                PlanIsCoordination = true,
                GuardTaskPresent = true,
                GuardTaskRevision = 1,
                PlanRevision = 1,
                RelationshipEstablished = true,
                MainAdvancing = true,
                GuardCommandWritten = true,
                SupportCommandWritten = true,
                MainStillWaiting = false,
                PlanAgeSeconds = 30.0,
                StableSecondsRequired = 8.0,
            };
        }

        // 场景 B 的前置：命令已写入但关系尚未建立，主力仍 Hold。
        private static EligibilityTriggerInput Unestablished()
        {
            var i = Established();
            i.Mode = P2AdaptationMode.GuardLossBeforeEstablished;
            i.RelationshipEstablished = false;
            i.MainAdvancing = false;
            i.MainStillWaiting = true;
            i.PlanAgeSeconds = 6.0;
            return i;
        }

        [Fact]
        public void AdapterIsOffByDefault()
        {
            var i = Established();
            i.Mode = P2AdaptationMode.None;
            Assert.False(EligibilityTrigger.Decide(i).Fire);
        }

        [Fact]
        public void ScenarioAFiresWhenFullyEstablished()
        {
            Assert.True(EligibilityTrigger.Decide(Established()).Fire);
        }

        [Fact]
        public void ScenarioADoesNotFireBeforeRelationshipEstablished()
        {
            var i = Established();
            i.RelationshipEstablished = false;
            var d = EligibilityTrigger.Decide(i);
            Assert.False(d.Fire);
            Assert.Equal("relationship_not_established", d.Reason);
        }

        [Fact]
        public void ScenarioADoesNotFireBeforeMainAdvances()
        {
            var i = Established();
            i.MainAdvancing = false;
            var d = EligibilityTrigger.Decide(i);
            Assert.False(d.Fire);
            Assert.Equal("main_not_advancing", d.Reason);
        }

        [Fact]
        public void ScenarioADoesNotFireBeforeStableWindow()
        {
            var i = Established();
            i.PlanAgeSeconds = 7.9;
            var d = EligibilityTrigger.Decide(i);
            Assert.False(d.Fire);
            Assert.Equal("plan_not_stable age=7.90 required=8.00", d.Reason);
        }

        [Fact]
        public void ScenarioBFiresWhileUnestablishedAndMainHolding()
        {
            Assert.True(EligibilityTrigger.Decide(Unestablished()).Fire);
        }

        [Fact]
        public void ScenarioBDoesNotFireOnceRelationshipEstablished()
        {
            var i = Unestablished();
            i.RelationshipEstablished = true;
            var d = EligibilityTrigger.Decide(i);
            Assert.False(d.Fire);
            Assert.Equal("relationship_already_established", d.Reason);
        }

        [Fact]
        public void ScenarioBDoesNotFireBeforeCommandsWritten()
        {
            var i = Unestablished();
            i.GuardCommandWritten = false;
            var d = EligibilityTrigger.Decide(i);
            Assert.False(d.Fire);
            Assert.Equal("commands_not_written guard_written=0 support_written=1", d.Reason);
        }

        [Fact]
        public void ScenarioBDoesNotFireAfterMainStopsHolding()
        {
            var i = Unestablished();
            i.MainStillWaiting = false;
            var d = EligibilityTrigger.Decide(i);
            Assert.False(d.Fire);
            Assert.Equal("main_no_longer_holding", d.Reason);
        }

        // 相位互斥：A 的前置不能触发 B，B 的前置不能触发 A。
        [Fact]
        public void ScenarioBNeverFiresWithScenarioAInputs()
        {
            var i = Established();
            i.Mode = P2AdaptationMode.GuardLossBeforeEstablished;
            Assert.False(EligibilityTrigger.Decide(i).Fire);
        }

        [Fact]
        public void ScenarioANeverFiresWithScenarioBInputs()
        {
            var i = Unestablished();
            i.Mode = P2AdaptationMode.GuardLossAfterEstablished;
            Assert.False(EligibilityTrigger.Decide(i).Fire);
        }

        // 版本未稳定时不得对旧版本的保护任务下手，等下一 tick。
        [Fact]
        public void StaleGuardRevisionIsNeverTargeted()
        {
            var i = Established();
            i.GuardTaskRevision = 1;
            i.PlanRevision = 2;
            var d = EligibilityTrigger.Decide(i);
            Assert.False(d.Fire);
            Assert.Equal("guard_revision_unsettled task_rev=1 plan_rev=2", d.Reason);
        }

        [Fact]
        public void NonCoordinationPlanIsNeverTargeted()
        {
            var i = Established();
            i.PlanIsCoordination = false;
            var d = EligibilityTrigger.Decide(i);
            Assert.False(d.Fire);
            Assert.Equal("not_coordination_plan", d.Reason);
        }

        [Fact]
        public void ClosedPlanIsNeverTargeted()
        {
            var i = Established();
            i.PlanOpen = false;
            var d = EligibilityTrigger.Decide(i);
            Assert.False(d.Fire);
            Assert.Equal("plan_not_open", d.Reason);
        }

        [Fact]
        public void MissingGuardTaskIsNeverTargeted()
        {
            var i = Established();
            i.GuardTaskPresent = false;
            var d = EligibilityTrigger.Decide(i);
            Assert.False(d.Fire);
            Assert.Equal("guard_task_absent", d.Reason);
        }

        // 触发原因必须能区分 A 与 B，实机日志据此判定哪条分支被覆盖。
        [Fact]
        public void TriggerReasonDistinguishesScenarioAFromB()
        {
            Assert.Equal("guard_loss_after_established", EligibilityTrigger.Decide(Established()).Reason);
            Assert.Equal("guard_loss_before_established", EligibilityTrigger.Decide(Unestablished()).Reason);
        }
    }
}
