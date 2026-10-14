using System;
using CombatBrain2.Core;
using Xunit;

namespace CombatBrain2.Tests
{
    // P2 协同到位与依赖释放。纯逻辑，不触碰引擎。
    public class DependencyReadinessTests
    {
        // M0=(0,0)，E0=(0,300)：u=(0,1)，v=(-1,0)。
        // approach = min(25, 300/4=75) = 25 -> M*=(0,25)
        // G* = M* + 10u = (0,35)；S* = M* +/- 12v = (+/-12, 25)
        private static CoordinationGeometry Geometry()
        {
            return CoordinationGeometry.Compute(0.0, 0.0, 0.0, 300.0, 0.0, -20.0);
        }

        [Fact]
        public void GeometryMatchesPinnedForwardAndLateralOffsets()
        {
            var g = Geometry();
            Assert.NotNull(g);
            Assert.Equal(0.0, g.MainX, 3);
            Assert.Equal(25.0, g.MainY, 3);
            Assert.Equal(0.0, g.GuardX, 3);
            Assert.Equal(35.0, g.GuardY, 3);
            // 辅助侧向 12 m：原点在 (0,-20)，取距离较短的一侧。
            Assert.Equal(12.0, Math.Abs(g.SupportX), 3);
            Assert.Equal(25.0, g.SupportY, 3);
            Assert.Equal(25.0, g.ApproachMeters, 3);
        }

        // 中心重合不产生几何：调用方必须放弃，而不是伪造一个点。
        [Fact]
        public void CoincidentCentersProduceNoGeometry()
        {
            Assert.Null(CoordinationGeometry.Compute(0.0, 0.0, 0.0, 0.0, 0.0, 0.0));
        }

        [Fact]
        public void NonFiniteCentersProduceNoGeometry()
        {
            Assert.Null(CoordinationGeometry.Compute(double.NaN, 0.0, 0.0, 300.0, 0.0, 0.0));
            Assert.Null(CoordinationGeometry.Compute(0.0, 0.0, double.PositiveInfinity, 300.0, 0.0, 0.0));
        }

        // 保护到位：到 G* 半径内，且相对 M* 前向投影 [5,15]、侧向绝对值 <=5。
        [Fact]
        public void GuardInPositionRequiresRadiusAndProjectionBounds()
        {
            var g = Geometry();
            Assert.True(PlanBuilder.GuardInPosition(g, 0.0, 35.0));
            Assert.False(PlanBuilder.GuardInPosition(g, 0.0, 28.0));
            Assert.False(PlanBuilder.GuardInPosition(g, 0.0, 41.0));
            Assert.False(PlanBuilder.GuardInPosition(g, 5.5, 35.0));
        }

        // 辅助到位：到 S* 半径内，且相对 M* 侧向绝对值 [7,17]。
        [Fact]
        public void SupportInPositionRequiresRadiusAndLateralBounds()
        {
            var g = Geometry();
            Assert.True(PlanBuilder.SupportInPosition(g, g.SupportX, 25.0));
            Assert.False(PlanBuilder.SupportInPosition(g, 6.0, 25.0));
            Assert.False(PlanBuilder.SupportInPosition(g, double.NaN, 25.0));
        }

        // 连续有效读数：单次读数不放行；必须 >=1 s 且两次以上。
        [Fact]
        public void SingleReadDoesNotReleaseAdvance()
        {
            var r = new DependencyReadiness();
            r.Note(100.0, true);
            Assert.False(r.Ready);
            Assert.Equal(1, r.ReadCount);
        }

        [Fact]
        public void TwoReadsSpanningOneSecondReleaseAdvance()
        {
            var r = new DependencyReadiness();
            r.Note(100.0, true);
            r.Note(101.2, true);
            Assert.True(r.Ready);
            Assert.Equal(2, r.ReadCount);
        }

        // 未到位不建立连续读数；恢复必须重新计时，不沿用旧 ready。
        [Fact]
        public void NotReadyReadResetsContinuousWindow()
        {
            var r = new DependencyReadiness();
            r.Note(100.0, true);
            r.Note(101.2, true);
            Assert.True(r.Ready);

            r.Note(102.0, false);
            Assert.False(r.Ready);
            Assert.Equal(0, r.ReadCount);

            r.Note(103.0, true);
            Assert.False(r.Ready);
            r.Note(104.2, true);
            Assert.True(r.Ready);
        }

        [Fact]
        public void UnknownReadIsNotReady()
        {
            var r = new DependencyReadiness();
            r.Note(100.0, true);
            r.Note(101.2, true);
            Assert.True(r.Ready);

            r.Note(102.0, false);
            Assert.False(r.Ready);
        }

        // 依赖集：保护与辅助都必须就绪才释放主力推进。
        [Fact]
        public void MainAdvanceRequiresBothDependenciesReady()
        {
            var plan = new BattlePlan
            {
                PlanId = "plan-1",
                Goal = PlanBuilder.GoalCoordination,
                Revision = 1,
                CreatedAt = 10.0,
                Geometry = Geometry(),
            };
            var main = new FormationTask
            {
                TaskId = "plan-1-task-1", PlanId = plan.PlanId, Revision = 1, Ordinal = 1,
                Role = FormationRole.Main, Kind = TaskKind.Hold,
                FormationKey = "0/0", ObjectIdentity = "obj-a", FormationClass = "Ranged",
                CreatedAt = 10.0, RoleBaselineUnits = 55,
            };
            var guard = new FormationTask
            {
                TaskId = "plan-1-task-2", PlanId = plan.PlanId, Revision = 1, Ordinal = 2,
                Role = FormationRole.Guard, Kind = TaskKind.Reposition,
                FormationKey = "0/1", ObjectIdentity = "obj-b", FormationClass = "Infantry",
                CreatedAt = 10.0, RoleBaselineUnits = 30,
                DependsOnTaskId = main.TaskId, GuardsTaskId = main.TaskId,
                // 命令已写入并生效、有效目标仍在到位区内：释放条件的前置部分。
                WrittenAt = 11.0, Feedback = TaskFeedbackState.Effective,
            };
            guard.SetTarget(0.0, 0.0, plan.Geometry.GuardX, plan.Geometry.GuardY);
            guard.SetOrderTarget(plan.Geometry.GuardX, plan.Geometry.GuardY);
            var support = new FormationTask
            {
                TaskId = "plan-1-task-3", PlanId = plan.PlanId, Revision = 1, Ordinal = 3,
                Role = FormationRole.Support, Kind = TaskKind.Reposition,
                FormationKey = "0/2", ObjectIdentity = "obj-c", FormationClass = "Cavalry",
                CreatedAt = 10.0, RoleBaselineUnits = 15,
                DependsOnTaskId = main.TaskId, SupportsTaskId = main.TaskId,
                WrittenAt = 11.0, Feedback = TaskFeedbackState.Effective,
            };
            support.SetTarget(0.0, 0.0, plan.Geometry.SupportX, plan.Geometry.SupportY);
            support.SetOrderTarget(plan.Geometry.SupportX, plan.Geometry.SupportY);
            plan.Tasks.Add(main);
            plan.Tasks.Add(guard);
            plan.Tasks.Add(support);

            var guardReady = new DependencyReadiness();
            var supportReady = new DependencyReadiness();

            Assert.False(DependencyReadiness.MainAdvanceAllowed(plan, guard, support, guardReady, supportReady));

            guardReady.Note(100.0, true);
            guardReady.Note(101.2, true);
            Assert.False(DependencyReadiness.MainAdvanceAllowed(plan, guard, support, guardReady, supportReady));

            supportReady.Note(100.0, true);
            supportReady.Note(101.2, true);
            Assert.True(DependencyReadiness.MainAdvanceAllowed(plan, guard, support, guardReady, supportReady));
        }

        // 去掉保护位置反馈后主力推进不释放；恢复连续读数后重新释放。
        [Fact]
        public void RemovingGuardFeedbackBlocksMainAdvance()
        {
            var g0 = Geometry();
            var plan = new BattlePlan
            {
                PlanId = "plan-1",
                Goal = PlanBuilder.GoalCoordination,
                Revision = 1,
                Geometry = g0,
            };
            var main = new FormationTask { TaskId = "t1", PlanId = plan.PlanId, Role = FormationRole.Main, Kind = TaskKind.Hold };
            var guard = new FormationTask
            {
                TaskId = "t2", PlanId = plan.PlanId, Role = FormationRole.Guard, Kind = TaskKind.Reposition,
                Revision = plan.Revision, WrittenAt = 11.0, Feedback = TaskFeedbackState.Effective,
            };
            guard.SetTarget(0.0, 0.0, g0.GuardX, g0.GuardY);
            guard.SetOrderTarget(g0.GuardX, g0.GuardY);
            var support = new FormationTask
            {
                TaskId = "t3", PlanId = plan.PlanId, Role = FormationRole.Support, Kind = TaskKind.Reposition,
                Revision = plan.Revision, WrittenAt = 11.0, Feedback = TaskFeedbackState.Effective,
            };
            support.SetTarget(0.0, 0.0, g0.SupportX, g0.SupportY);
            support.SetOrderTarget(g0.SupportX, g0.SupportY);
            plan.Tasks.Add(main);
            plan.Tasks.Add(guard);
            plan.Tasks.Add(support);

            var g = new DependencyReadiness();
            var s = new DependencyReadiness();
            g.Note(100.0, true);
            g.Note(101.2, true);
            s.Note(100.0, true);
            s.Note(101.2, true);
            Assert.True(DependencyReadiness.MainAdvanceAllowed(plan, guard, support, g, s));

            var gLost = new DependencyReadiness();
            Assert.False(DependencyReadiness.MainAdvanceAllowed(plan, guard, support, gLost, s));

            gLost.Note(110.0, true);
            gLost.Note(111.2, true);
            Assert.True(DependencyReadiness.MainAdvanceAllowed(plan, guard, support, gLost, s));
        }

        // 独立反例（派单 §2 R2）：保护/辅助的当前实际命令目标点偏离到位区时不得释放主力推进，
        // 即使历史反馈仍是 Effective、连续读数仍就绪、请求点仍写在到位区内。
        // 只核 RequestedX/Y 的实现会在这三处反例上错误放行。
        [Fact]
        public void MainAdvanceBlocksWhenActualOrderTargetDeviates()
        {
            var g0 = Geometry();
            var plan = new BattlePlan
            {
                PlanId = "plan-1",
                Goal = PlanBuilder.GoalCoordination,
                Revision = 1,
                Geometry = g0,
            };
            var main = new FormationTask { TaskId = "t1", PlanId = plan.PlanId, Role = FormationRole.Main, Kind = TaskKind.Hold };
            var guard = new FormationTask
            {
                TaskId = "t2", PlanId = plan.PlanId, Role = FormationRole.Guard, Kind = TaskKind.Reposition,
                Revision = plan.Revision, WrittenAt = 11.0, Feedback = TaskFeedbackState.Effective,
            };
            var support = new FormationTask
            {
                TaskId = "t3", PlanId = plan.PlanId, Role = FormationRole.Support, Kind = TaskKind.Reposition,
                Revision = plan.Revision, WrittenAt = 11.0, Feedback = TaskFeedbackState.Effective,
            };
            plan.Tasks.Add(main);
            plan.Tasks.Add(guard);
            plan.Tasks.Add(support);

            var g = new DependencyReadiness();
            var s = new DependencyReadiness();
            g.Note(100.0, true);
            g.Note(101.2, true);
            s.Note(100.0, true);
            s.Note(101.2, true);

            // 前置：请求点与实际读回点一致时释放。
            guard.SetTarget(0.0, 0.0, g0.GuardX, g0.GuardY);
            guard.SetOrderTarget(g0.GuardX, g0.GuardY);
            support.SetTarget(0.0, 0.0, g0.SupportX, g0.SupportY);
            support.SetOrderTarget(g0.SupportX, g0.SupportY);
            Assert.True(DependencyReadiness.MainAdvanceAllowed(plan, guard, support, g, s));

            // 反例一：实际命令目标点偏离 20 m（枚举仍是 Move，请求点没变）。
            guard.SetOrderTarget(g0.GuardX + 20.0, g0.GuardY);
            Assert.False(DependencyReadiness.MainAdvanceAllowed(plan, guard, support, g, s));
            guard.SetOrderTarget(g0.GuardX, g0.GuardY);
            Assert.True(DependencyReadiness.MainAdvanceAllowed(plan, guard, support, g, s));

            // 反例二：实际命令目标点不可读（未知不得当成已落地）。
            guard.ClearOrderTarget();
            Assert.False(DependencyReadiness.MainAdvanceAllowed(plan, guard, support, g, s));
            guard.SetOrderTarget(g0.GuardX, g0.GuardY);

            // 反例三：辅助侧同样受约束。
            support.SetOrderTarget(g0.SupportX, g0.SupportY + 20.0);
            Assert.False(DependencyReadiness.MainAdvanceAllowed(plan, guard, support, g, s));
        }

        // 职责人数继续线：>=6 且 >= 基线 50%，相等允许继续。
        [Fact]
        public void RoleStrengthLineAllowsEquality()
        {
            Assert.True(PlanBuilder.RoleStrengthHolds(10, 20));
            Assert.False(PlanBuilder.RoleStrengthHolds(9, 20));
            Assert.False(PlanBuilder.RoleStrengthHolds(5, 10));
            Assert.True(PlanBuilder.RoleStrengthHolds(6, 12));
            Assert.False(PlanBuilder.RoleStrengthHolds(6, 100));
        }

        // 前提失效必须覆盖：人数跌破、对象移除、类别离开能力组合、资格丧失、位置未知。
        [Fact]
        public void RolePremiseFailsOnEachCause()
        {
            Assert.Equal(RolePremiseFailure.None,
                PlanBuilder.PremiseFailureOf(true, "Cavalry", 15, 15, true));
            Assert.Equal(RolePremiseFailure.StrengthBelowLine,
                PlanBuilder.PremiseFailureOf(true, "Cavalry", 5, 15, true));
            Assert.Equal(RolePremiseFailure.ObjectGone,
                PlanBuilder.PremiseFailureOf(false, "Cavalry", 15, 15, true));
            Assert.Equal(RolePremiseFailure.ClassNotInCombination,
                PlanBuilder.PremiseFailureOf(true, "HorseArcher", 15, 15, true));
            Assert.Equal(RolePremiseFailure.EligibilityLost,
                PlanBuilder.PremiseFailureOf(true, "Cavalry", 15, 15, false));
            Assert.Equal(RolePremiseFailure.PositionUnknown,
                PlanBuilder.PremiseFailureOf(true, "Cavalry", 15, 15, true, false));
        }
    }
}
