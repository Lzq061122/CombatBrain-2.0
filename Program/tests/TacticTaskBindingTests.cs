using System;
using System.Collections.Generic;
using CombatBrain2.Core;
using CombatBrain2.Host;
using Xunit;

namespace CombatBrain2.Tests
{
    // Cb2TacticTarget 的贯穿反例：战术层的参与资格直接由 CB2 的计划/任务台账决定。
    // 这里调用的是 Host 侧真正使用的那份判定（通过 links 链入同一源文件），
    // 不是为测试另造的一份模型。
    public class TacticTaskBindingTests
    {
        // 只覆盖「有没有可执行任务」这一层；引擎侧的选择顺序不在本文件验证。
        [Fact]
        public void NoLedgerOrNoOpenPlanMeansNoTacticTarget()
        {
            FormationTask task;
            var tasks = new TaskLedger();
            var plans = new PlanLedger();

            Assert.False(Cb2TacticTarget.TrySelect(null, plans, out task));
            Assert.False(Cb2TacticTarget.TrySelect(tasks, null, out task));
            Assert.False(Cb2TacticTarget.TrySelect(tasks, plans, out task));
            Assert.Null(task);

            // 台账里有任务、但当前没有开放计划（数据源为旧计划）→ 不参与。
            var stale = Task("plan-old", "task-old");
            tasks.Add(stale);
            Assert.False(Cb2TacticTarget.TrySelect(tasks, plans, out task));
        }

        [Fact]
        public void OpenTaskOfCurrentPlanIsTheTarget()
        {
            var tasks = new TaskLedger();
            var plans = new PlanLedger();
            var plan = Adopt(plans, "plan-1", 1);
            var t = Task(plan.PlanId, "task-1");
            tasks.Add(t);

            FormationTask picked;
            Assert.True(Cb2TacticTarget.TrySelect(tasks, plans, out picked));
            Assert.Same(t, picked);
        }

        // 旧版本的普通任务不得作为服务对象：分配改变后旧任务仍可能开着。
        [Fact]
        public void StaleRevisionIsNotATarget()
        {
            var tasks = new TaskLedger();
            var plans = new PlanLedger();
            var plan = Adopt(plans, "plan-1", 2);
            tasks.Add(Task(plan.PlanId, "task-old", revision: 1));

            FormationTask picked;
            Assert.False(Cb2TacticTarget.TrySelect(tasks, plans, out picked));
            Assert.Null(picked);
        }

        // 判失败但尚未关闭的任务不再是可服务的分配；关掉之后也不得复活。
        [Fact]
        public void FailedTaskIsNotATarget()
        {
            var tasks = new TaskLedger();
            var plans = new PlanLedger();
            var plan = Adopt(plans, "plan-1", 1);
            var failed = Task(plan.PlanId, "task-1");
            failed.Feedback = TaskFeedbackState.Failed;
            tasks.Add(failed);

            FormationTask picked;
            Assert.False(Cb2TacticTarget.TrySelect(tasks, plans, out picked));

            tasks.CloseTask(failed.TaskId, 10.0);
            Assert.False(Cb2TacticTarget.TrySelect(tasks, plans, out picked));
        }

        // 取消（关闭任务）与失资格（计划失效）是两条不同的路径，都必须退回"不参与"。
        [Fact]
        public void CancelAndInvalidationBothWithdrawParticipation()
        {
            var tasks = new TaskLedger();
            var plans = new PlanLedger();
            var plan = Adopt(plans, "plan-2", 1);
            var t = Task(plan.PlanId, "task-1");
            tasks.Add(t);

            FormationTask picked;
            Assert.True(Cb2TacticTarget.TrySelect(tasks, plans, out picked));

            tasks.CloseTask(t.TaskId, 12.0);
            Assert.False(Cb2TacticTarget.TrySelect(tasks, plans, out picked));

            // 计划失效：即使还有未关闭任务也不再参与。
            var again = Task(plan.PlanId, "task-2");
            tasks.Add(again);
            Assert.True(Cb2TacticTarget.TrySelect(tasks, plans, out picked));

            plans.Invalidate(PlanInvalidReason.FormationEligibilityLost, "role_lost", 13.0);
            Assert.False(Cb2TacticTarget.TrySelect(tasks, plans, out picked));
        }

        // 退出任务沿用现有口径（BattlePlan.IsExitTask），不另立一套；它关闭后同样退回原生。
        [Fact]
        public void ExitTaskFollowsTheExistingExitRule()
        {
            var tasks = new TaskLedger();
            var plans = new PlanLedger();
            var plan = Adopt(plans, "plan-3", 1);
            var exit = Task(plan.PlanId, "plan-3-exit-1");
            Assert.True(BattlePlan.IsExitTask(exit));
            tasks.Add(exit);

            FormationTask picked;
            Assert.True(Cb2TacticTarget.TrySelect(tasks, plans, out picked));
            Assert.Same(exit, picked);

            tasks.CloseTask(exit.TaskId, 20.0);
            Assert.False(Cb2TacticTarget.TrySelect(tasks, plans, out picked));
        }

        // 计划的 Current 换成新计划后，旧计划的任务不得继续服务。
        [Fact]
        public void PreviousPlanTasksStopBeingServed()
        {
            var tasks = new TaskLedger();
            var plans = new PlanLedger();
            var oldPlan = Adopt(plans, "plan-4", 1);
            tasks.Add(Task(oldPlan.PlanId, "task-1"));

            var newPlan = Adopt(plans, "plan-5", 1);
            FormationTask picked;
            Assert.False(Cb2TacticTarget.TrySelect(tasks, plans, out picked));

            var t = Task(newPlan.PlanId, "task-2");
            tasks.Add(t);
            Assert.True(Cb2TacticTarget.TrySelect(tasks, plans, out picked));
            Assert.Same(t, picked);
        }

        private static BattlePlan Adopt(PlanLedger plans, string planId, int revision)
        {
            var plan = new BattlePlan { PlanId = planId, Revision = revision };
            plans.Adopt(plan);
            return plan;
        }

        private static FormationTask Task(string planId, string taskId) { return Task(planId, taskId, 1); }

        private static FormationTask Task(string planId, string taskId, int revision)
        {
            return new FormationTask
            {
                TaskId = taskId,
                PlanId = planId,
                Revision = revision,
                FormationKey = "0/0",
                Kind = TaskKind.Hold,
                CreatedAt = 1.0,
            };
        }
    }
}
