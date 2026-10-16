using System;
using System.Globalization;
using CombatBrain2.Core;
using Xunit;

namespace CombatBrain2.Tests
{
    // P3 派单 §2：位置语义。
    // 战术目标 T 的语义是「期望有效成员质心到达的位置」；真正写给引擎的是由 T
    // 按写前读数换算出来的锚点目标。两者是同一世界坐标里的不同参考点，
    // 任何判据、日志与复算都不得把锚点当成质心的证据。
    public class CoordinateSemanticsTests
    {
        private static FormationTask Reposition()
        {
            return new FormationTask
            {
                TaskId = "t1",
                PlanId = "plan-1",
                Revision = 1,
                Ordinal = 1,
                Kind = TaskKind.Reposition,
                Reason = "coordination_guard_reposition",
                FormationKey = "0/0",
                FormationClass = "Infantry",
                ObjectIdentity = "obj@0/0",
                OrderAfter = "Move",
                WrittenAt = 10.0,
                Role = FormationRole.Guard,
            };
        }

        // 锚点（引擎编队参考位置）与有效成员质心分开给，模拟真实采样：
        // PosX/PosY 是锚点，ActiveCenterX/Y 是「人实际在哪」。
        private static FormationRecord Rec(string key, double anchorX, double centerX, bool centerKnown = true)
        {
            var parts = key.Split('/');
            var r = new FormationRecord
            {
                MissionKey = "m",
                SampledAt = 10.0,
                TeamIndex = 0,
                FormationIndex = int.Parse(parts[1], CultureInfo.InvariantCulture),
                FormationClass = "Infantry",
                UnitCount = 50,
                PosX = anchorX,
                PosY = 0.0,
                MovementOrder = "Move",
                IsAiControlled = true,
                IsPlayerControlled = false,
                IsPlayerTeam = false,
                ObjectIdentity = "obj@" + key,
            };
            if (centerKnown)
            {
                r.HasActiveCenter = true;
                r.ActiveCenterX = centerX;
                r.ActiveCenterY = 0.0;
                r.ActiveMemberCount = 50;
                r.MemberDigest = "d-" + key;
            }
            return r;
        }

        // 派单 §2.1 最小校验：A=100、C=110、T=150 必须下发 140。
        // 写成 T+(C-A)=160 会把预期质心推到 170，方向相反。
        [Fact]
        public void AnchoredRequestUsesRuledSignConvention()
        {
            Assert.Equal(140.0, FormationTask.AnchoredRequest(150.0, 100.0, 110.0), 6);
            Assert.Equal(160.0, FormationTask.AnchoredRequest(150.0, 110.0, 100.0), 6);
        }

        // 锚点到达战术目标、有效成员质心没动：本命令没有推动人员，不得判生效。
        [Fact]
        public void AnchorArrivalWithStalledCentroidIsNotEffective()
        {
            var t = Reposition();
            t.SetCanonicalTarget(20.0, 0.0);      // T：期望质心位置
            t.SetBaseline(0.0, 0.0);              // 写前质心
            t.SetTarget(0.0, 0.0, 20.0, 0.0);     // 写给引擎的锚点目标
            t.SetOrderTarget(20.0, 0.0);          // 读回锚点 = 本次请求

            var r = TaskFeedback.Evaluate(t, Rec("0/0", 20.0, 0.0), 11.0, double.NaN, double.NaN);

            Assert.Equal(TaskFeedbackState.Pending, r.State);
        }

        // 质心真正到达 T 才算到点；判据必须用质心而不是锚点。
        [Fact]
        public void CentroidArrivalIsEffective()
        {
            var t = Reposition();
            t.SetCanonicalTarget(20.0, 0.0);
            t.SetBaseline(0.0, 0.0);
            t.SetTarget(0.0, 0.0, 20.0, 0.0);
            t.SetOrderTarget(20.0, 0.0);

            var r = TaskFeedback.Evaluate(t, Rec("0/0", 20.0, 19.8), 11.0, double.NaN, double.NaN);

            Assert.Equal(TaskFeedbackState.Effective, r.State);
            Assert.Contains("reached_target_point", r.Detail);
        }

        // 质心不可读时不得退用锚点冒充质心（派单 §2.2 第 2 条）。
        [Fact]
        public void UnreadableCentroidDoesNotFallBackToAnchor()
        {
            var t = Reposition();
            t.SetCanonicalTarget(20.0, 0.0);
            t.SetBaseline(0.0, 0.0);
            t.SetTarget(0.0, 0.0, 20.0, 0.0);
            t.SetOrderTarget(20.0, 0.0);

            var r = TaskFeedback.Evaluate(t, Rec("0/0", 20.0, 0.0, centerKnown: false), 11.0,
                                          double.NaN, double.NaN);

            Assert.NotEqual(TaskFeedbackState.Effective, r.State);
            Assert.Contains("center_unreadable", r.Detail);
        }

        // 转向/阵型变化后偏移会变：锚点已经压住本次请求，但质心离 T 还差 5 m，
        // 不得据此宣称已到达（可以是位移生效，但不是到点）。
        [Fact]
        public void OffsetApproximationCannotFakeArrival()
        {
            var t = Reposition();
            t.SetCanonicalTarget(20.0, 0.0);
            t.SetBaseline(0.0, 0.0);
            t.SetTarget(0.0, 0.0, 30.0, 0.0);
            t.SetOrderTarget(30.0, 0.0);

            var r = TaskFeedback.Evaluate(t, Rec("0/0", 30.0, 25.0), 11.0, double.NaN, double.NaN);

            Assert.False(t.ReachedTargetPoint);
            Assert.DoesNotContain("reached_target_point", r.Detail);
        }

        // 读回核对的对象是「本次实际锚点请求」，不是战术目标 T：
        // 锚点请求与读回一致即命令没被改写，即使二者与 T 相差甚远。
        [Fact]
        public void ReadbackComparesAnchorRequestNotTacticTarget()
        {
            var t = Reposition();
            t.SetCanonicalTarget(20.0, 0.0);
            t.SetBaseline(0.0, 0.0);
            t.SetTarget(0.0, 0.0, 30.0, 0.0);
            t.SetOrderTarget(30.4, 0.0);

            var r = TaskFeedback.Evaluate(t, Rec("0/0", 30.4, 19.9), 11.0, double.NaN, double.NaN);

            Assert.Equal(TaskFeedbackState.Effective, r.State);
            Assert.Contains("reached_target_point", r.Detail);
        }

        // 命令目标被原生改写：读回与本次锚点请求不符，不得继续判到位。
        [Fact]
        public void OverriddenOrderTargetBlocksArrival()
        {
            var t = Reposition();
            t.SetCanonicalTarget(20.0, 0.0);
            t.SetBaseline(0.0, 0.0);
            t.SetTarget(0.0, 0.0, 30.0, 0.0);
            t.SetOrderTarget(35.0, 0.0);          // 原生把目标改到别处

            var r = TaskFeedback.Evaluate(t, Rec("0/0", 35.0, 19.9), 11.0, double.NaN, double.NaN);

            Assert.NotEqual(TaskFeedbackState.Effective, r.State);
            Assert.Contains("order_target_readback_error", r.Detail);
        }

        // P1 普通位置任务（没有战术目标、按锚点定义）语义不变。
        [Fact]
        public void AnchorDefinedTaskKeepsAnchorSemantics()
        {
            var t = new FormationTask
            {
                TaskId = "t-p1",
                PlanId = "plan-1",
                Revision = 1,
                Kind = TaskKind.Reposition,
                Reason = "ranged_advantage_advance",
                FormationKey = "0/0",
                ObjectIdentity = "obj@0/0",
                OrderAfter = "Move",
                WrittenAt = 10.0,
            };
            t.SetTarget(0.0, 0.0, 20.0, 0.0);
            t.SetBaseline(0.0, 0.0);
            t.SetOrderTarget(20.0, 0.0);

            var arrived = TaskFeedback.Evaluate(t, Rec("0/0", 19.8, 0.0), 11.0, double.NaN, double.NaN);
            Assert.Equal(TaskFeedbackState.Effective, arrived.State);
            Assert.Contains("reached_target_point", arrived.Detail);
        }

        private static BattlePlan CoordinationPlan(CoordinationGeometry g, out FormationTask guard,
                                                   out FormationTask support)
        {
            var plan = new BattlePlan
            {
                PlanId = "plan-1",
                Goal = PlanBuilder.GoalCoordination,
                Revision = 1,
                Geometry = g,
            };
            var main = new FormationTask
            {
                TaskId = "t1", PlanId = plan.PlanId, Role = FormationRole.Main,
                Kind = TaskKind.Hold, Revision = plan.Revision,
            };
            guard = new FormationTask
            {
                TaskId = "t2", PlanId = plan.PlanId, Role = FormationRole.Guard,
                Kind = TaskKind.Reposition, Revision = plan.Revision,
                WrittenAt = 11.0, Feedback = TaskFeedbackState.Effective,
            };
            support = new FormationTask
            {
                TaskId = "t3", PlanId = plan.PlanId, Role = FormationRole.Support,
                Kind = TaskKind.Reposition, Revision = plan.Revision,
                WrittenAt = 11.0, Feedback = TaskFeedbackState.Effective,
            };
            plan.Tasks.Add(main);
            plan.Tasks.Add(guard);
            plan.Tasks.Add(support);
            return plan;
        }

        // 命令锚点即使碰巧落在到位区内，只要战术目标已不属于当前几何的职责点，
        // 就不得释放主力推进（派单 §2.2 第 5 条：不能把命令锚点直接送入到位判据）。
        [Fact]
        public void StaleTacticTargetBlocksMainAdvanceEvenIfAnchorIsInside()
        {
            var g = CoordinationGeometry.Compute(0.0, 0.0, 0.0, 300.0, 0.0, -20.0);
            FormationTask guard, support;
            var plan = CoordinationPlan(g, out guard, out support);

            var gr = new DependencyReadiness();
            var sr = new DependencyReadiness();
            gr.Note(100.0, true);
            gr.Note(101.2, true);
            sr.Note(100.0, true);
            sr.Note(101.2, true);

            // 命令没被改写（读回 = 锚点请求），锚点 (0,35) 正好等于 G*。
            // 战术目标却停在 (0,45)，已不属于当前几何的保护职责点。
            guard.SetCanonicalTarget(0.0, 45.0);
            guard.SetTarget(0.0, 0.0, 0.0, 35.0);
            guard.SetOrderTarget(0.0, 35.0);
            support.SetCanonicalTarget(g.SupportX, g.SupportY);
            support.SetTarget(0.0, 0.0, g.SupportX, g.SupportY);
            support.SetOrderTarget(g.SupportX, g.SupportY);

            Assert.False(DependencyReadiness.MainAdvanceAllowed(plan, guard, support, gr, sr));
        }

        // 职责点与当前几何一致、命令未被改写时仍应释放：修复不得把正常链路一起关掉。
        // 换算后的锚点与 G* 不同正是修复后的正常形态，命令锚点不参与职责到位判据。
        [Fact]
        public void CurrentTacticTargetStillReleasesMainAdvance()
        {
            var g = CoordinationGeometry.Compute(0.0, 0.0, 0.0, 300.0, 0.0, -20.0);
            FormationTask guard, support;
            var plan = CoordinationPlan(g, out guard, out support);

            var gr = new DependencyReadiness();
            var sr = new DependencyReadiness();
            gr.Note(100.0, true);
            gr.Note(101.2, true);
            sr.Note(100.0, true);
            sr.Note(101.2, true);

            guard.SetCanonicalTarget(g.GuardX, g.GuardY);
            guard.SetTarget(0.0, 0.0, 0.0, 41.0);
            guard.SetOrderTarget(0.0, 41.0);
            support.SetCanonicalTarget(g.SupportX, g.SupportY);
            support.SetTarget(0.0, 0.0, 18.0, 25.0);
            support.SetOrderTarget(18.0, 25.0);

            Assert.True(DependencyReadiness.MainAdvanceAllowed(plan, guard, support, gr, sr));
        }
    }
}
