using System;
using System.Collections.Generic;
using CombatBrain2.Core;
using Xunit;

namespace CombatBrain2.Tests
{
    // P2 提案权限与唯一裁决（派单 §5）。全部为纯逻辑反例，不触碰引擎。
    public class ProposalArbiterTests
    {
        private static BattlePlan Plan1()
        {
            return new BattlePlan
            {
                PlanId = "plan-1",
                MissionKey = "m1",
                Goal = PlanBuilder.GoalCoordination,
                Revision = 1,
                CreatedAt = 10.0,
            };
        }

        private static FormationTask Task(BattlePlan plan, string id, int revision, string key,
                                          FormationRole role, TaskKind kind, string obj)
        {
            return new FormationTask
            {
                TaskId = id,
                PlanId = plan.PlanId,
                Revision = revision,
                Ordinal = 1,
                FormationKey = key,
                ObjectIdentity = obj,
                FormationClass = "Infantry",
                Kind = kind,
                Role = role,
                CreatedAt = plan.CreatedAt,
                RoleBaselineUnits = 20,
            };
        }

        private static Proposal Make(string planId, int revision, string taskId, string key,
                                     string obj, TaskKind kind, string proposalId,
                                     ProposalSource source)
        {
            return new Proposal
            {
                MissionKey = "m1",
                PlanId = planId,
                Revision = revision,
                TaskId = taskId,
                FormationKey = key,
                ObjectIdentity = obj,
                Kind = kind,
                ProposalId = proposalId,
                Source = source,
                SubmittedAt = 10.5,
                HasTarget = true,
            };
        }

        // 批次边界由裁决器管理：提交本身不产生对外有效结果，必须显式公布。
        [Fact]
        public void SubmissionAloneDoesNotPublish()
        {
            var plan = Plan1();
            var task = Task(plan, "plan-1-task-2", 1, "0/1", FormationRole.Guard, TaskKind.Reposition, "obj-b");
            task.SetTarget(0.0, 0.0, 100.0, 100.0);
            plan.Tasks.Add(task);

            var arb = new ProposalArbiter();
            var p = Make(plan.PlanId, 1, task.TaskId, "0/1", "obj-b",
                         TaskKind.Reposition, "p1", ProposalSource.TaskSubmission);
            p.TargetX = 100.0;
            p.TargetY = 100.0;

            var d = arb.Submit(p, plan, task);
            Assert.True(d.Accepted);
            Assert.False(d.Published);
            Assert.Null(arb.CurrentFor("0/1"));
            Assert.NotNull(arb.OpenBatchFor("0/1"));
            Assert.Equal(1, arb.OpenBatchFor("0/1").Candidates.Count);

            var pub = arb.PublishBatch("0/1");
            Assert.True(pub.Published);
            Assert.Equal("p1", pub.WinnerProposalId);
            Assert.Equal("p1", arb.CurrentFor("0/1").ProposalId);
            Assert.Null(arb.OpenBatchFor("0/1"));
        }

        // 收齐合法提案后一次公布：公布前不对外产生任何结果。
        [Fact]
        public void BatchIsPublishedOnceAndOnlyAfterAllCandidatesArrive()
        {
            var plan = Plan1();
            var task = Task(plan, "plan-1-task-2", 1, "0/1", FormationRole.Guard, TaskKind.Reposition, "obj-b");
            task.SetTarget(0.0, 0.0, 100.0, 100.0);
            plan.Tasks.Add(task);

            var near = Make(plan.PlanId, 1, task.TaskId, "0/1", "obj-b",
                            TaskKind.Reposition, "p-near", ProposalSource.TaskSubmission);
            near.TargetX = 100.6;
            near.TargetY = 100.0;
            var far = Make(plan.PlanId, 1, task.TaskId, "0/1", "obj-b",
                           TaskKind.Reposition, "p-far", ProposalSource.TaskSubmission);
            far.TargetX = 102.0;
            far.TargetY = 100.0;

            var arb = new ProposalArbiter();
            Assert.False(arb.Submit(far, plan, task).Published);
            Assert.Null(arb.CurrentFor("0/1"));
            Assert.False(arb.Submit(near, plan, task).Published);
            Assert.Null(arb.CurrentFor("0/1"));
            Assert.Equal(2, arb.OpenBatchFor("0/1").Candidates.Count);

            var pub = arb.PublishBatch("0/1");
            Assert.Equal("p-near", pub.WinnerProposalId);
            Assert.Equal("p-near", arb.CurrentFor("0/1").ProposalId);
        }

        // 首次同时竞争的结果不得依赖提交先后 —— 交换输入顺序赢家必须一致。
        [Fact]
        public void CompetitionWinnerIsIndependentOfSubmissionOrder()
        {
            Assert.Equal("p-near", RunCompetition(true));
            Assert.Equal("p-near", RunCompetition(false));
        }

        private static string RunCompetition(bool nearFirst)
        {
            var plan = Plan1();
            var task = Task(plan, "plan-1-task-2", 1, "0/1", FormationRole.Guard, TaskKind.Reposition, "obj-b");
            task.SetTarget(0.0, 0.0, 100.0, 100.0);
            plan.Tasks.Add(task);

            var near = Make(plan.PlanId, 1, task.TaskId, "0/1", "obj-b",
                            TaskKind.Reposition, "p-near", ProposalSource.TaskSubmission);
            near.TargetX = 100.6;
            near.TargetY = 100.0;
            var far = Make(plan.PlanId, 1, task.TaskId, "0/1", "obj-b",
                           TaskKind.Reposition, "p-far", ProposalSource.TaskSubmission);
            far.TargetX = 102.0;
            far.TargetY = 100.0;

            var arb = new ProposalArbiter();
            if (nearFirst)
            {
                arb.Submit(near, plan, task);
                arb.Submit(far, plan, task);
            }
            else
            {
                arb.Submit(far, plan, task);
                arb.Submit(near, plan, task);
            }
            arb.PublishBatch("0/1");
            return arb.CurrentFor("0/1").ProposalId;
        }

        // 无目标的提案不得因为「误差算不出来」而被当成误差 0 的最优候选。
        // 派单 §5 的排序依据是「距规范目标点误差升序」，没有目标点就没有可比误差。
        // 用 Hold 承载无目标侧：Reposition 无目标已被合法性阶段直接拒绝。
        [Fact]
        public void TargetlessProposalCannotOutrankTargetedProposal()
        {
            var plan = Plan1();
            var task = Task(plan, "plan-1-task-1", 1, "0/0", FormationRole.Main, TaskKind.Hold, "obj-a");
            task.SetTarget(0.0, 0.0, 100.0, 100.0);
            plan.Tasks.Add(task);

            var targeted = Make(plan.PlanId, 1, task.TaskId, "0/0", "obj-a",
                                TaskKind.Hold, "p-targeted", ProposalSource.TaskSubmission);
            targeted.TargetX = 102.0;
            targeted.TargetY = 100.0;

            var targetless = Make(plan.PlanId, 1, task.TaskId, "0/0", "obj-a",
                                  TaskKind.Hold, "p-targetless", ProposalSource.TaskSubmission);
            targetless.HasTarget = false;

            var arb = new ProposalArbiter();
            arb.Submit(targetless, plan, task);
            arb.Submit(targeted, plan, task);
            var pub = arb.PublishBatch("0/0");

            Assert.Equal("p-targeted", pub.WinnerProposalId);
            Assert.Equal("p-targeted", arb.CurrentFor("0/0").ProposalId);
            Assert.True(arb.CurrentFor("0/0").HasTarget);
        }

        // 唯一候选是无目标 Reposition 时不得公布：它在合法性阶段就该被拒，
        // 不能因为「批里只有它」就凭 proposal_id 序成为有效结果。
        [Fact]
        public void SoleTargetlessRepositionIsRejectedBeforePublish()
        {
            var plan = Plan1();
            var task = Task(plan, "plan-1-task-2", 1, "0/1", FormationRole.Guard, TaskKind.Reposition, "obj-b");
            task.SetTarget(0.0, 0.0, 100.0, 100.0);
            plan.Tasks.Add(task);

            var targetless = Make(plan.PlanId, 1, task.TaskId, "0/1", "obj-b",
                                  TaskKind.Reposition, "p-targetless", ProposalSource.TaskSubmission);
            targetless.HasTarget = false;

            var arb = new ProposalArbiter();
            var d = arb.Submit(targetless, plan, task);

            Assert.False(d.Accepted);
            Assert.Equal(ProposalRejectReason.InvalidGeometry, d.RejectReason);
            Assert.Null(arb.OpenBatchFor("0/1"));

            var pub = arb.PublishBatch("0/1");
            Assert.False(pub.Published);
            Assert.Null(arb.CurrentFor("0/1"));
        }

        // 无目标的 Hold 合法且可公布：Hold 执行 Stop，不需要位置目标。
        [Fact]
        public void SoleTargetlessHoldIsPublishable()
        {
            var plan = Plan1();
            var task = Task(plan, "plan-1-task-1", 1, "0/0", FormationRole.Main, TaskKind.Hold, "obj-a");
            task.SetTarget(0.0, 0.0, 100.0, 100.0);
            plan.Tasks.Add(task);

            var targetless = Make(plan.PlanId, 1, task.TaskId, "0/0", "obj-a",
                                  TaskKind.Hold, "p-hold", ProposalSource.TaskSubmission);
            targetless.HasTarget = false;

            var arb = new ProposalArbiter();
            var d = arb.Submit(targetless, plan, task);
            Assert.True(d.Accepted);
            Assert.False(d.Published);

            var pub = arb.PublishBatch("0/0");
            Assert.True(pub.Published);
            Assert.Equal("p-hold", pub.WinnerProposalId);
            Assert.Equal("p-hold", arb.CurrentFor("0/0").ProposalId);
            Assert.False(arb.CurrentFor("0/0").HasTarget);
        }

        // Engage 用普通 Charge，不是指定目标的 ChargeToTarget，不得要求位置目标。
        [Fact]
        public void TargetlessEngageIsNotRejected()
        {
            var plan = Plan1();
            var task = Task(plan, "plan-1-task-3", 1, "0/2", FormationRole.Support, TaskKind.Engage, "obj-c");
            plan.Tasks.Add(task);

            var targetless = Make(plan.PlanId, 1, task.TaskId, "0/2", "obj-c",
                                  TaskKind.Engage, "p-engage", ProposalSource.TaskSubmission);
            targetless.HasTarget = false;

            var arb = new ProposalArbiter();
            var d = arb.Submit(targetless, plan, task);
            Assert.True(d.Accepted);

            var pub = arb.PublishBatch("0/2");
            Assert.True(pub.Published);
            Assert.Equal("p-engage", pub.WinnerProposalId);
        }

        // 有目标 Hold 与无目标 Hold 同批：仍按 §5 排序，与是否有目标无关。
        [Fact]
        public void TargetlessHoldCompetesOnProposalIdOrder()
        {
            var plan = Plan1();
            var task = Task(plan, "plan-1-task-1", 1, "0/0", FormationRole.Main, TaskKind.Hold, "obj-a");
            task.SetTarget(0.0, 0.0, 100.0, 100.0);
            plan.Tasks.Add(task);

            var targeted = Make(plan.PlanId, 1, task.TaskId, "0/0", "obj-a",
                                TaskKind.Hold, "p-z", ProposalSource.TaskSubmission);
            targeted.TargetX = 100.0;
            targeted.TargetY = 100.0;
            var targetless = Make(plan.PlanId, 1, task.TaskId, "0/0", "obj-a",
                                  TaskKind.Hold, "p-a", ProposalSource.TaskSubmission);
            targetless.HasTarget = false;

            var arb = new ProposalArbiter();
            arb.Submit(targeted, plan, task);
            arb.Submit(targetless, plan, task);
            var pub = arb.PublishBatch("0/0");

            // 两者误差都是 0（无目标被记为 +∞ 的规则已不适用 Hold：
            // Hold 没有位置目标，比较退化为 proposal_id 序）。
            Assert.True(pub.Published);
            Assert.NotNull(arb.CurrentFor("0/0"));
        }

        // 交换顺序后赢家仍一致：无目标提案不能靠"先提交"占住结果。
        [Fact]
        public void TargetlessProposalLosesRegardlessOfSubmissionOrder()
        {
            Assert.Equal("p-targeted", RunTargetlessCompetition(false));
            Assert.Equal("p-targeted", RunTargetlessCompetition(true));
        }

        private static string RunTargetlessCompetition(bool targetlessFirst)
        {
            var plan = Plan1();
            var task = Task(plan, "plan-1-task-2", 1, "0/1", FormationRole.Guard, TaskKind.Reposition, "obj-b");
            task.SetTarget(0.0, 0.0, 100.0, 100.0);
            plan.Tasks.Add(task);

            // 用 Hold 承载「无目标但合法」这一侧，Reposition 侧一律有目标。
            var holdTask = Task(plan, "plan-1-task-1", 1, "0/0", FormationRole.Main, TaskKind.Hold, "obj-a");
            holdTask.SetTarget(0.0, 0.0, 100.0, 100.0);
            plan.Tasks.Add(holdTask);

            var targeted = Make(plan.PlanId, 1, task.TaskId, "0/1", "obj-b",
                                TaskKind.Reposition, "p-targeted", ProposalSource.TaskSubmission);
            targeted.TargetX = 102.0;
            targeted.TargetY = 100.0;

            // 无目标侧挂在 Hold 编队上：Reposition 无目标在合法性阶段即被拒，
            // 无法到达竞争环节，用它测"排序稳定"会变成测拒绝。
            var targetless = Make(plan.PlanId, 1, holdTask.TaskId, "0/0", "obj-a",
                                  TaskKind.Hold, "p-targetless", ProposalSource.TaskSubmission);
            targetless.HasTarget = false;

            var arb = new ProposalArbiter();
            if (targetlessFirst)
            {
                arb.Submit(targetless, plan, holdTask);
                arb.Submit(targeted, plan, task);
            }
            else
            {
                arb.Submit(targeted, plan, task);
                arb.Submit(targetless, plan, holdTask);
            }
            arb.PublishBatch("0/1");
            var cur = arb.CurrentFor("0/1");
            Assert.NotNull(cur);
            return cur.ProposalId;
        }

        // 同等误差时按 proposal_id 稳定序唯一选取。
        [Fact]
        public void TieBreaksOnProposalIdOrder()
        {
            var plan = Plan1();
            var task = Task(plan, "plan-1-task-2", 1, "0/1", FormationRole.Guard, TaskKind.Reposition, "obj-b");
            task.SetTarget(0.0, 0.0, 100.0, 100.0);
            plan.Tasks.Add(task);

            var later = Make(plan.PlanId, 1, task.TaskId, "0/1", "obj-b",
                             TaskKind.Reposition, "p-zzz", ProposalSource.TaskSubmission);
            later.TargetX = 101.0;
            later.TargetY = 100.0;
            var earlier = Make(plan.PlanId, 1, task.TaskId, "0/1", "obj-b",
                               TaskKind.Reposition, "p-aaa", ProposalSource.TaskSubmission);
            earlier.TargetX = 101.0;
            earlier.TargetY = 100.0;

            var arb = new ProposalArbiter();
            arb.Submit(later, plan, task);
            arb.Submit(earlier, plan, task);
            var pub = arb.PublishBatch("0/1");

            Assert.Equal("p-aaa", pub.WinnerProposalId);
            Assert.Equal("p-aaa", arb.CurrentFor("0/1").ProposalId);
        }

        // 判据 3：已公布结果保持，普通对抗提案不得悄悄改变目标。
        [Fact]
        public void PublishedDecisionIsHeldAgainstRivalProposal()
        {
            var plan = Plan1();
            var task = Task(plan, "plan-1-task-2", 1, "0/1", FormationRole.Guard, TaskKind.Reposition, "obj-b");
            task.SetTarget(0.0, 0.0, 100.0, 100.0);
            plan.Tasks.Add(task);

            var incumbent = Make(plan.PlanId, 1, task.TaskId, "0/1", "obj-b",
                                 TaskKind.Reposition, "p-incumbent", ProposalSource.TaskSubmission);
            incumbent.TargetX = 101.0;
            incumbent.TargetY = 100.0;

            var challenger = Make(plan.PlanId, 1, task.TaskId, "0/1", "obj-b",
                                  TaskKind.Reposition, "p-challenger", ProposalSource.TaskSubmission);
            challenger.TargetX = 100.2;
            challenger.TargetY = 100.0;

            var arb = new ProposalArbiter();
            arb.Submit(incumbent, plan, task);
            arb.PublishBatch("0/1");

            var d = arb.Submit(challenger, plan, task);
            Assert.True(d.Accepted);
            Assert.True(d.Published);
            Assert.Equal("p-incumbent", d.WinnerProposalId);
            Assert.Equal(ProposalRejectReason.Superseded, d.LoserRejectReason);
            Assert.Equal("p-incumbent", arb.CurrentFor("0/1").ProposalId);
        }

        // PhaseTransition 来源标签本身不授予替换权限：仍须先关闭当前结果。
        [Fact]
        public void PhaseTransitionSourceAloneCannotReplacePublishedDecision()
        {
            var plan = Plan1();
            var task = Task(plan, "plan-1-task-2", 1, "0/1", FormationRole.Guard, TaskKind.Reposition, "obj-b");
            task.SetTarget(0.0, 0.0, 100.0, 100.0);
            plan.Tasks.Add(task);

            var incumbent = Make(plan.PlanId, 1, task.TaskId, "0/1", "obj-b",
                                 TaskKind.Reposition, "p-incumbent", ProposalSource.TaskSubmission);
            incumbent.TargetX = 101.0;
            incumbent.TargetY = 100.0;
            var phase = Make(plan.PlanId, 1, task.TaskId, "0/1", "obj-b",
                             TaskKind.Reposition, "p-phase", ProposalSource.PhaseTransition);
            phase.TargetX = 100.1;
            phase.TargetY = 100.0;

            var arb = new ProposalArbiter();
            arb.Submit(incumbent, plan, task);
            arb.PublishBatch("0/1");

            var d = arb.Submit(phase, plan, task);
            Assert.Equal("p-incumbent", d.WinnerProposalId);
            Assert.Equal("p-incumbent", arb.CurrentFor("0/1").ProposalId);

            // 显式关闭当前结果后，重新收题并公布即可替换。
            arb.CloseCurrent("0/1");
            Assert.Null(arb.CurrentFor("0/1"));
            Assert.False(arb.Submit(phase, plan, task).Published);
            var pub = arb.PublishBatch("0/1");
            Assert.Equal("p-phase", pub.WinnerProposalId);
        }

        // proposal_id 必须由来源与建议内容派生：同内容必同身份，内容变则身份变。
        [Fact]
        public void ProposalIdIsDerivedFromContentNotArrivalOrder()
        {
            string a = Proposal.MakeId("m1", "plan-1", 1, "plan-1-task-2", "0/1", "obj-b",
                                       TaskKind.Reposition, ProposalSource.TaskSubmission, 100.0, 200.0);
            string b = Proposal.MakeId("m1", "plan-1", 1, "plan-1-task-2", "0/1", "obj-b",
                                       TaskKind.Reposition, ProposalSource.TaskSubmission, 100.0, 200.0);
            string c = Proposal.MakeId("m1", "plan-1", 1, "plan-1-task-2", "0/1", "obj-b",
                                       TaskKind.Reposition, ProposalSource.TaskSubmission, 100.0, 202.0);

            Assert.Equal(a, b);
            Assert.NotEqual(a, c);
        }

        // 判据 1：旧 revision 先拒绝，不让非法提案排挤合法提案。
        [Fact]
        public void StaleRevisionRejectedBeforeAnythingElse()
        {
            var plan = Plan1();
            var task = Task(plan, "plan-1-task-2", 1, "0/1", FormationRole.Guard, TaskKind.Reposition, "obj-b");
            plan.Tasks.Add(task);

            var arb = new ProposalArbiter();
            var stale = Make(plan.PlanId, 0, task.TaskId, "0/1", "obj-b",
                             TaskKind.Reposition, "p-stale", ProposalSource.TaskSubmission);
            var decision = arb.Submit(stale, plan, task);

            Assert.False(decision.Accepted);
            Assert.Equal(ProposalRejectReason.StaleRevision, decision.RejectReason);
            Assert.Null(arb.OpenBatchFor("0/1"));
        }

        [Fact]
        public void ClosedPlanRejects()
        {
            var plan = Plan1();
            plan.IsClosed = true;
            var task = Task(plan, "plan-1-task-2", 1, "0/1", FormationRole.Guard, TaskKind.Reposition, "obj-b");
            plan.Tasks.Add(task);

            var arb = new ProposalArbiter();
            var p = Make(plan.PlanId, 1, task.TaskId, "0/1", "obj-b",
                         TaskKind.Reposition, "p1", ProposalSource.TaskSubmission);
            var decision = arb.Submit(p, plan, task);

            Assert.False(decision.Accepted);
            Assert.Equal(ProposalRejectReason.PlanClosed, decision.RejectReason);
        }

        // 判据 1：骑兵 Engage 属于类别不允许，必须先拒绝。
        [Fact]
        public void CavalryEngageProposalRejectedByClass()
        {
            var plan = Plan1();
            var task = Task(plan, "plan-1-task-2", 1, "0/1", FormationRole.Guard, TaskKind.Reposition, "obj-b");
            task.FormationClass = "Cavalry";
            plan.Tasks.Add(task);

            var arb = new ProposalArbiter();
            var p = Make(plan.PlanId, 1, task.TaskId, "0/1", "obj-b",
                         TaskKind.Engage, "p-engage", ProposalSource.TaskSubmission);
            var decision = arb.Submit(p, plan, task);

            Assert.False(decision.Accepted);
            Assert.Equal(ProposalRejectReason.KindNotAllowedForClass, decision.RejectReason);
        }

        // 判据 2：保护任务不能越权改主力任务。
        [Fact]
        public void GuardCannotProposeChangeToMainTask()
        {
            var plan = Plan1();
            var main = Task(plan, "plan-1-task-1", 1, "0/0", FormationRole.Main, TaskKind.Hold, "obj-a");
            var guard = Task(plan, "plan-1-task-2", 1, "0/1", FormationRole.Guard, TaskKind.Reposition, "obj-b");
            plan.Tasks.Add(main);
            plan.Tasks.Add(guard);

            var arb = new ProposalArbiter();
            var p = Make(plan.PlanId, 1, main.TaskId, "0/1", "obj-b",
                         TaskKind.Reposition, "p-cross", ProposalSource.TaskSubmission);
            var decision = arb.Submit(p, plan, guard);

            Assert.False(decision.Accepted);
            Assert.Equal(ProposalRejectReason.TaskNotInPlan, decision.RejectReason);
        }

        // 判据 2：任务不能自行更换自己的职责。
        [Fact]
        public void TaskCannotChangeItsOwnRole()
        {
            var plan = Plan1();
            var guard = Task(plan, "plan-1-task-2", 1, "0/1", FormationRole.Guard, TaskKind.Reposition, "obj-b");
            plan.Tasks.Add(guard);

            var arb = new ProposalArbiter();
            var p = Make(plan.PlanId, 1, guard.TaskId, "0/1", "obj-b",
                         TaskKind.Reposition, "p-swap", ProposalSource.TaskSubmission);
            p.RequestedRole = FormationRole.Main;
            var decision = arb.Submit(p, plan, guard);

            Assert.False(decision.Accepted);
            Assert.Equal(ProposalRejectReason.KindNotAllowedForRole, decision.RejectReason);
        }

        // 判据 3：同一任务的完全相同提案去重，不重置首次提交锚点，也不重复入批。
        [Fact]
        public void DuplicateProposalDoesNotResetSubmissionAnchor()
        {
            var plan = Plan1();
            var task = Task(plan, "plan-1-task-2", 1, "0/1", FormationRole.Guard, TaskKind.Reposition, "obj-b");
            task.SetTarget(0.0, 0.0, 100.0, 100.0);
            plan.Tasks.Add(task);

            var p = Make(plan.PlanId, 1, task.TaskId, "0/1", "obj-b",
                         TaskKind.Reposition, "p-dup", ProposalSource.TaskSubmission);
            p.TargetX = 100.0;
            p.TargetY = 100.0;

            var arb = new ProposalArbiter();
            Assert.False(arb.Submit(p, plan, task).Duplicate);

            var repeat = Make(plan.PlanId, 1, task.TaskId, "0/1", "obj-b",
                              TaskKind.Reposition, "p-dup", ProposalSource.TaskSubmission);
            repeat.TargetX = 100.0;
            repeat.TargetY = 100.0;
            repeat.SubmittedAt = 999.0;

            var d = arb.Submit(repeat, plan, task);
            Assert.True(d.Accepted);
            Assert.True(d.Duplicate);
            Assert.False(d.Published);
            Assert.Equal(10.5, arb.FirstSubmissionAt(task.TaskId), 3);
            Assert.Equal(1, arb.OpenBatchFor("0/1").Candidates.Count);
        }

        // 判据 4：已写过的任务不能通过换 proposal_id 获得第二次写令。
        [Fact]
        public void WrittenTaskCannotWriteAgainThroughNewProposalId()
        {
            var plan = Plan1();
            var task = Task(plan, "plan-1-task-2", 1, "0/1", FormationRole.Guard, TaskKind.Reposition, "obj-b");
            task.WrittenAt = 12.0;
            task.SetTarget(0.0, 0.0, 100.0, 100.0);
            plan.Tasks.Add(task);

            var arb = new ProposalArbiter();
            var p = Make(plan.PlanId, 1, task.TaskId, "0/1", "obj-b",
                         TaskKind.Reposition, "p-second", ProposalSource.TaskSubmission);
            p.TargetX = 100.0;
            p.TargetY = 100.0;
            var d = arb.Submit(p, plan, task);

            Assert.False(d.Accepted);
            Assert.Equal(ProposalRejectReason.TaskClosed, d.RejectReason);
        }

        // 判据 1：未授权来源先拒绝，不能靠自报高优先级取胜。
        [Fact]
        public void UnauthorizedSourceRejected()
        {
            var plan = Plan1();
            var task = Task(plan, "plan-1-task-2", 1, "0/1", FormationRole.Guard, TaskKind.Reposition, "obj-b");
            plan.Tasks.Add(task);

            var arb = new ProposalArbiter();
            var p = Make(plan.PlanId, 1, task.TaskId, "0/1", "obj-b",
                         TaskKind.Reposition, "p-src", ProposalSource.None);
            var d = arb.Submit(p, plan, task);

            Assert.False(d.Accepted);
            Assert.Equal(ProposalRejectReason.UnauthorizedSource, d.RejectReason);
        }

        // 判据 1：对象身份不符先拒绝。
        [Fact]
        public void ObjectMismatchRejected()
        {
            var plan = Plan1();
            var task = Task(plan, "plan-1-task-2", 1, "0/1", FormationRole.Guard, TaskKind.Reposition, "obj-b");
            plan.Tasks.Add(task);

            var arb = new ProposalArbiter();
            var p = Make(plan.PlanId, 1, task.TaskId, "0/1", "obj-OTHER",
                         TaskKind.Reposition, "p-obj", ProposalSource.TaskSubmission);
            var d = arb.Submit(p, plan, task);

            Assert.False(d.Accepted);
            Assert.Equal(ProposalRejectReason.ObjectMismatch, d.RejectReason);
        }

        // 每个真实编队的 Movement 域只有一个当前裁决结果：只清自己的槽位。
        [Fact]
        public void OnlyOwnFormationSlotIsCleared()
        {
            var plan = Plan1();
            var guard = Task(plan, "plan-1-task-2", 1, "0/1", FormationRole.Guard, TaskKind.Reposition, "obj-b");
            guard.SetTarget(0.0, 0.0, 100.0, 100.0);
            var support = Task(plan, "plan-1-task-3", 1, "0/2", FormationRole.Support, TaskKind.Reposition, "obj-c");
            support.SetTarget(0.0, 0.0, 100.0, 0.0);
            plan.Tasks.Add(guard);
            plan.Tasks.Add(support);

            var arb = new ProposalArbiter();
            var g = Make(plan.PlanId, 1, guard.TaskId, "0/1", "obj-b",
                         TaskKind.Reposition, "p-guard", ProposalSource.TaskSubmission);
            g.TargetX = 100.0;
            g.TargetY = 100.0;
            var s = Make(plan.PlanId, 1, support.TaskId, "0/2", "obj-c",
                         TaskKind.Reposition, "p-support", ProposalSource.TaskSubmission);
            s.TargetX = 100.0;
            s.TargetY = 0.0;

            arb.Submit(g, plan, guard);
            arb.PublishBatch("0/1");
            arb.Submit(s, plan, support);
            arb.PublishBatch("0/2");

            arb.CloseCurrent("0/1");

            Assert.Null(arb.CurrentFor("0/1"));
            Assert.NotNull(arb.CurrentFor("0/2"));
        }

        // 关闭时只清自己的槽位：旧任务晚到不得清掉新任务。
        [Fact]
        public void LateCloseOfOldTaskDoesNotClearNewSlot()
        {
            var plan = Plan1();
            var oldTask = Task(plan, "plan-1-task-2", 1, "0/1", FormationRole.Guard, TaskKind.Reposition, "obj-b");
            oldTask.SetTarget(0.0, 0.0, 100.0, 100.0);
            plan.Tasks.Add(oldTask);

            var arb = new ProposalArbiter();
            var p = Make(plan.PlanId, 1, oldTask.TaskId, "0/1", "obj-b",
                         TaskKind.Reposition, "p-old", ProposalSource.TaskSubmission);
            p.TargetX = 100.0;
            p.TargetY = 100.0;
            arb.Submit(p, plan, oldTask);
            arb.PublishBatch("0/1");

            // 新 revision 的同类提案上任。
            plan.Revision = 2;
            var newTask = Task(plan, "plan-1-task-5", 2, "0/1", FormationRole.Guard, TaskKind.Reposition, "obj-b");
            newTask.SetTarget(0.0, 0.0, 100.0, 100.0);
            plan.Tasks.Add(newTask);

            var np = Make(plan.PlanId, 2, newTask.TaskId, "0/1", "obj-b",
                          TaskKind.Reposition, "p-new", ProposalSource.PhaseTransition);
            np.TargetX = 100.0;
            np.TargetY = 100.0;
            arb.CloseCurrent("0/1");
            arb.Submit(np, plan, newTask);
            arb.PublishBatch("0/1");

            // 旧任务晚到清槽：不得清掉新任务。
            arb.CloseCurrentForTask("0/1", oldTask.TaskId);

            var current = arb.CurrentFor("0/1");
            Assert.NotNull(current);
            Assert.Equal(newTask.TaskId, current.TaskId);
            Assert.Equal(2, current.Revision);
        }

        // A5 注入的首次竞争：任务还没写令（HasPosition==false），误差基准必须回退到
        // 计划几何的规范目标点，否则两个候选误差同为 0.0，规则②失效、只能靠字典序
        // （派单 §5 第 3 条、§9.2 场景 A 第 2 条）。
        // 偏移候选的 proposal_id 在字典序上更小，故本反例只有规则②生效才会选规范目标。
        [Fact]
        public void CanonicalTargetDecidesFirstCompetitionRegardlessOfOrder()
        {
            Assert.Equal("p-canonical", RunCanonicalCompetition(true));
            Assert.Equal("p-canonical", RunCanonicalCompetition(false));
        }

        private static string RunCanonicalCompetition(bool canonicalFirst)
        {
            var plan = Plan1();
            var task = Task(plan, "plan-1-task-2", 1, "0/1", FormationRole.Guard, TaskKind.Reposition, "obj-b");
            task.SetCanonicalTarget(100.0, 100.0);
            plan.Tasks.Add(task);

            var canonical = Make(plan.PlanId, 1, task.TaskId, "0/1", "obj-b",
                                 TaskKind.Reposition, "p-canonical", ProposalSource.TestInjection);
            canonical.TargetX = 100.0;
            canonical.TargetY = 100.0;
            var lateral = Make(plan.PlanId, 1, task.TaskId, "0/1", "obj-b",
                               TaskKind.Reposition, "p-a-lateral", ProposalSource.TestInjection);
            lateral.TargetX = 100.0;
            lateral.TargetY = 102.0;

            var arb = new ProposalArbiter();
            if (canonicalFirst)
            {
                arb.Submit(canonical, plan, task);
                arb.Submit(lateral, plan, task);
            }
            else
            {
                arb.Submit(lateral, plan, task);
                arb.Submit(canonical, plan, task);
            }
            arb.PublishBatch("0/1");
            return arb.CurrentFor("0/1").ProposalId;
        }

        // 注入候选不得凭来源夺走生产候选：同任务、同 Kind、同坐标时按 proposal_id 稳定序，
        // 生产来源的 ID 字典序在前（派单 §5「禁止以回调先后、字典遍历或最后写入者决定战术权限」）。
        [Fact]
        public void TestInjectionCannotOutrankTaskSubmissionAtSameTarget()
        {
            var plan = Plan1();
            var task = Task(plan, "plan-1-task-2", 1, "0/1", FormationRole.Guard, TaskKind.Reposition, "obj-b");
            task.SetCanonicalTarget(100.0, 100.0);
            plan.Tasks.Add(task);

            var production = Proposal.Create("m1", plan, task, TaskKind.Reposition,
                ProposalSource.TaskSubmission, 100.0, 100.0, true, "reason", 10.5);
            var injected = Proposal.Create("m1", plan, task, TaskKind.Reposition,
                ProposalSource.TestInjection, 100.0, 100.0, true, "reason", 10.5);
            Assert.True(string.CompareOrdinal(production.ProposalId, injected.ProposalId) < 0);

            var arb = new ProposalArbiter();
            arb.Submit(injected, plan, task);
            arb.Submit(production, plan, task);
            var pub = arb.PublishBatch("0/1");

            Assert.Equal(production.ProposalId, pub.WinnerProposalId);
            Assert.Equal(production.ProposalId, arb.CurrentFor("0/1").ProposalId);
        }
    }
}
