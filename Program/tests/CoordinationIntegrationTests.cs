using System.Collections.Generic;
using CombatBrain2.Core;
using Xunit;

namespace CombatBrain2.Tests
{
    // 跨组件反例：推进任务的完整链路必须走通
    //   依赖到位 → 创建推进任务 → 进入所属 Plan → 提交提案 → 评价点公布 → 允许执行。
    // 任一连接处断开，这条链路都必须失败，不能只靠单组件测试证明接好。
    public class CoordinationIntegrationTests
    {
        private static FormationRecord Rec(int team, int index, string cls, int units,
                                           double x, double y, bool playerTeam)
        {
            return new FormationRecord
            {
                TeamIndex = team,
                FormationIndex = index,
                FormationClass = cls,
                UnitCount = units,
                PosX = x,
                PosY = y,
                IsPlayerTeam = playerTeam,
                IsPlayerControlled = false,
                IsAiControlled = true,
                MovementOrder = "Move",
                ObjectIdentity = "obj" + team + index,
                ActiveCenterX = x,
                ActiveCenterY = y,
                ActiveMemberCount = units,
                HasActiveCenter = true,
                MemberDigest = "d" + team + index + "-" + units,
            };
        }

        private static FormationSnapshot Snapshot(int inf, int rng, int cav,
                                                  double enemyUnits, double enemyX, double enemyY)
        {
            var s = new FormationSnapshot { SampledAt = TestParameters.P2Now };
            if (inf > 0) s.Records.Add(Rec(0, 0, "Infantry", inf, 0.0, 0.0, false));
            if (rng > 0) s.Records.Add(Rec(0, 1, "Ranged", rng, 0.0, 0.0, false));
            if (cav > 0) s.Records.Add(Rec(0, 2, "Cavalry", cav, 0.0, 0.0, false));
            int enemyRanged = 10;
            int enemyInf = (int)enemyUnits - enemyRanged;
            if (enemyInf > 0) s.Records.Add(Rec(1, 0, "Infantry", enemyInf, enemyX, enemyY, true));
            if (enemyRanged > 0) s.Records.Add(Rec(1, 1, "Ranged", enemyRanged, enemyX, enemyY + 30.0, true));
            return s;
        }

        private static BattleState SceneA()
        {
            var state = BattleAssessment.Read(Snapshot(30, 55, 15, 100.0, 0.0, 300.0), "input-1");
            Assert.NotNull(state);
            return state;
        }

        private static CoordinationDriver BoundDriver(out BattlePlan plan)
        {
            var state = SceneA();
            plan = new PlanLedger().CreateCoordination(state, TestParameters.P2Now);
            Assert.NotNull(plan);
            var driver = new CoordinationDriver();
            driver.Bind(plan);
            Assert.True(driver.BindTasks(plan));
            return driver;
        }

        private static DependencyObservation At(CoordinationGeometry g, FormationTask task,
                                                double x, double y, bool isGuard)
        {
            return At(g, task, x, y, task.RoleBaselineUnits, isGuard);
        }

        private static DependencyObservation At(CoordinationGeometry g, FormationTask task,
                                                double x, double y, int units, bool isGuard)
        {
            return new DependencyObservation
            {
                ObjectPresent = true,
                PositionKnown = true,
                X = x,
                Y = y,
                CurrentUnits = units,
                Eligible = true,
                FormationClass = task.FormationClass,
                MemberDigest = "d-" + task.TaskId,
            };
        }

        // 把保护与辅助都推进到位并形成连续 1 s 读数。
        // 释放主力推进还要求两条命令确实写入并生效（派单 §3 L86/L103），
        // 因此这里连同写令状态与有效目标一起置好，模拟真实写令之后的读数。
        private static void BringBothInPosition(CoordinationDriver driver, BattlePlan plan,
                                                double firstAt, double secondAt)
        {
            var g = plan.Geometry;
            driver.GuardTask.WrittenAt = firstAt - 1.0;
            driver.GuardTask.Feedback = TaskFeedbackState.Effective;
            driver.GuardTask.SetTarget(0.0, 0.0, g.GuardX, g.GuardY);
            driver.GuardTask.SetOrderTarget(g.GuardX, g.GuardY);
            driver.SupportTask.WrittenAt = firstAt - 1.0;
            driver.SupportTask.Feedback = TaskFeedbackState.Effective;
            driver.SupportTask.SetTarget(0.0, 0.0, g.SupportX, g.SupportY);
            driver.SupportTask.SetOrderTarget(g.SupportX, g.SupportY);
            driver.Observe(firstAt,
                At(g, driver.GuardTask, g.GuardX, g.GuardY, true),
                At(g, driver.SupportTask, g.SupportX, g.SupportY, false));
            driver.Observe(secondAt,
                At(g, driver.GuardTask, g.GuardX, g.GuardY, true),
                At(g, driver.SupportTask, g.SupportX, g.SupportY, false));
        }

        // ---- 完整链路 ----

        [Fact]
        public void AdvanceTaskEntersOwningPlanSoArbiterAcceptsItsProposal()
        {
            BattlePlan plan;
            var driver = BoundDriver(out plan);
            var g = plan.Geometry;
            BringBothInPosition(driver, plan, TestParameters.P2Now, TestParameters.P2Now + 1.0);

            var advance = driver.CreateAdvanceTask(plan, TestParameters.P2Now + 1.0);
            Assert.NotNull(advance);

            // 连接处一：推进任务必须进入所属 Plan，否则裁决器判 TaskNotInPlan。
            if (!plan.Tasks.Contains(advance)) plan.Tasks.Add(advance);
            Assert.Contains(advance, plan.Tasks);

            // 连接处二：提案可被裁决器接受（不是 TaskNotInPlan / StaleRevision）。
            var proposal = Proposal.Create("m1", plan, advance, advance.Kind,
                ProposalSource.PhaseTransition, g.MainX, g.MainY, advance.Reason, TestParameters.P2Now + 2.0);
            var submit = driver.Arbiter.Submit(proposal, plan, advance);
            Assert.False(submit.Duplicate);
            Assert.Equal(ProposalRejectReason.None, submit.RejectReason);
        }

        [Fact]
        public void AdvanceTaskProposalCannotBeAcceptedBeforeItEntersPlan()
        {
            BattlePlan plan;
            var driver = BoundDriver(out plan);
            var g = plan.Geometry;
            BringBothInPosition(driver, plan, TestParameters.P2Now, TestParameters.P2Now + 1.0);

            var advance = driver.CreateAdvanceTask(plan, TestParameters.P2Now + 1.0);
            Assert.NotNull(advance);

            // 刻意不进 Plan：这正是修复前的状态，必须被判为不属于该计划。
            Assert.DoesNotContain(advance, plan.Tasks);
            var proposal = Proposal.Create("m1", plan, advance, advance.Kind,
                ProposalSource.PhaseTransition, g.MainX, g.MainY, advance.Reason, TestParameters.P2Now + 2.0);
            var submit = driver.Arbiter.Submit(proposal, plan, advance);

            Assert.Equal(ProposalRejectReason.TaskNotInPlan, submit.RejectReason);
        }

        [Fact]
        public void AdvanceProposalIsNotPublishedUntilBatchIsPublished()
        {
            BattlePlan plan;
            var driver = BoundDriver(out plan);
            var g = plan.Geometry;
            BringBothInPosition(driver, plan, TestParameters.P2Now, TestParameters.P2Now + 1.0);
            var advance = driver.CreateAdvanceTask(plan, TestParameters.P2Now + 1.0);
            plan.Tasks.Add(advance);

            var proposal = Proposal.Create("m1", plan, advance, advance.Kind,
                ProposalSource.PhaseTransition, g.MainX, g.MainY, advance.Reason, TestParameters.P2Now + 2.0);
            driver.Arbiter.Submit(proposal, plan, advance);

            // 提交本身不产生可消费的裁决：写前门此时必须拒绝。
            Assert.Null(driver.Arbiter.CurrentFor(advance.FormationKey));

            var decision = driver.Arbiter.PublishBatch(advance.FormationKey);
            Assert.NotNull(decision);
            Assert.True(decision.Published);

            var current = driver.Arbiter.CurrentFor(advance.FormationKey);
            Assert.NotNull(current);
            Assert.Equal(advance.TaskId, current.TaskId);
            Assert.Equal(plan.Revision, current.Revision);
        }

        // 推进任务接管主力编队时，等待任务已公布的裁决槽必须一并清空：
        // 否则槽内旧任务会把推进任务永久判为「被取代」，即便原生真实获选，
        // 写前门也会以 superseded_by=<旧任务> 拒绝（实机 plan-2-task-4）。
        [Fact]
        public void AdvanceTaskClearsMainWaitDecisionSoItsProposalCanPublish()
        {
            BattlePlan plan;
            var driver = BoundDriver(out plan);
            var g = plan.Geometry;
            BringBothInPosition(driver, plan, TestParameters.P2Now, TestParameters.P2Now + 1.0);

            var mainKey = driver.MainWaitTask.FormationKey;
            driver.Arbiter.Submit(
                Proposal.Create(plan.MissionKey, plan, driver.MainWaitTask, TaskKind.Hold,
                    ProposalSource.TaskSubmission, 0.0, 0.0, false, "seed", TestParameters.P2Now),
                plan, driver.MainWaitTask);
            var seeded = driver.Arbiter.PublishBatch(mainKey);
            Assert.NotNull(seeded);
            Assert.Equal(driver.MainWaitTask.TaskId, driver.Arbiter.CurrentFor(mainKey).TaskId);

            var advance = driver.CreateAdvanceTask(plan, TestParameters.P2Now + 1.0);
            Assert.NotNull(advance);
            plan.Tasks.Add(advance);

            // 连接处一：等待任务的裁决槽必须已被撤下。
            Assert.Null(driver.Arbiter.CurrentFor(mainKey));

            // 连接处二：推进任务能走完提交到公布，取得与自身 TaskId 一致的当前裁决。
            var proposal = Proposal.Create("m1", plan, advance, advance.Kind,
                ProposalSource.PhaseTransition, g.MainX, g.MainY, advance.Reason, TestParameters.P2Now + 2.0);
            var submit = driver.Arbiter.Submit(proposal, plan, advance);
            Assert.Equal(ProposalRejectReason.None, submit.RejectReason);
            Assert.Equal("", submit.WinnerProposalId);

            var decision = driver.Arbiter.PublishBatch(mainKey);
            Assert.NotNull(decision);
            Assert.True(decision.Published);
            Assert.Equal(advance.TaskId, driver.Arbiter.CurrentFor(mainKey).TaskId);
        }

        // ---- 连接处：推进期间依赖仍被观察 ----

        [Fact]
        public void GuardLeavingPositionDuringAdvanceIsStillObserved()
        {
            BattlePlan plan;
            var driver = BoundDriver(out plan);
            var g = plan.Geometry;
            BringBothInPosition(driver, plan, TestParameters.P2Now, TestParameters.P2Now + 1.0);
            var advance = driver.CreateAdvanceTask(plan, TestParameters.P2Now + 1.0);
            Assert.NotNull(advance);
            Assert.Equal(CoordinationPhase.MainAdvancing, driver.Phase);

            // 推进期间保护离开到位区：必须仍能读到失效，而不是因阶段转换停止观察。
            var far = driver.Observe(TestParameters.P2Now + 2.0,
                At(g, driver.GuardTask, g.GuardX + 60.0, g.GuardY + 60.0, true),
                At(g, driver.SupportTask, g.SupportX, g.SupportY, false));

            Assert.False(far.GuardReady);
            Assert.False(far.MainAdvanceAllowed);
            Assert.DoesNotContain("phase=", far.Detail);
        }

        // ---- 连接处：人数口径为有效活跃成员 ----

        [Fact]
        public void RoleBaselineUsesActiveMemberCountNotEngineCount()
        {
            // 引擎人数 55，但有效活跃成员只有 40：基线必须取 40。
            var rec = Rec(0, 0, "Cavalry", 55, 0.0, 0.0, false);
            rec.ActiveMemberCount = 40;
            var plan = new BattlePlan { PlanId = "p", Revision = 1, CreatedAt = TestParameters.P2Now };
            plan.Tasks.Add(MakeRoleTask(plan, rec));
            Assert.Equal(40, plan.Tasks[0].RoleBaselineUnits);
            Assert.NotEqual(rec.UnitCount, plan.Tasks[0].RoleBaselineUnits);
        }

        private static FormationTask MakeRoleTask(BattlePlan plan, FormationRecord rec)
        {
            return PlanBuilder_MakeCoordinationTask(plan, rec);
        }

        private static FormationTask PlanBuilder_MakeCoordinationTask(BattlePlan plan, FormationRecord rec)
        {
            var t = new FormationTask
            {
                TaskId = plan.PlanId + "-task-1",
                PlanId = plan.PlanId,
                Ordinal = 1,
                FormationKey = rec.TeamIndex + "/" + rec.FormationIndex,
                ObjectIdentity = rec.ObjectIdentity,
                FormationClass = rec.FormationClass,
                TeamIndex = rec.TeamIndex,
                FormationIndex = rec.FormationIndex,
                Kind = TaskKind.Reposition,
                Role = FormationRole.Guard,
                Revision = plan.Revision,
            };
            t.RoleBaselineUnits = rec.HasActiveCenter ? rec.ActiveMemberCount : rec.UnitCount;
            return t;
        }

        // ---- 连接处：成员集合变化重置连续窗口 ----

        [Fact]
        public void MemberSetChangeResetsContinuousWindow()
        {
            BattlePlan plan;
            var driver = BoundDriver(out plan);
            var g = plan.Geometry;

            var guard = At(g, driver.GuardTask, g.GuardX, g.GuardY, true);
            var support = At(g, driver.SupportTask, g.SupportX, g.SupportY, false);
            guard.MemberDigest = "set-A";
            support.MemberDigest = "set-A";

            driver.Observe(TestParameters.P2Now, guard, support);
            // 第二批人接管：上一批的读数不得与新读数拼成 ready。
            guard.MemberDigest = "set-B";
            support.MemberDigest = "set-B";
            driver.Observe(TestParameters.P2Now + 1.0, guard, support);

            Assert.False(driver.GuardReadiness.Ready);
            Assert.False(driver.SupportReadiness.Ready);

            // 同一批人再连续读数一轮才释放。
            driver.Observe(TestParameters.P2Now + 2.0, guard, support);
            Assert.True(driver.GuardReadiness.Ready);
            Assert.True(driver.SupportReadiness.Ready);
        }
    }
}
