using CombatBrain2.Core;
using Xunit;

namespace CombatBrain2.Tests
{
    // 退出后读数判定（只读取证）：纯逻辑，不触碰引擎。
    //
    // 判定只依赖退出任务自身与对象当前归属，不依赖当前计划是否还是产生它的那个——
    // 这正是它能活过计划切换的原因。三类事件必须分开，互相不能冒充。
    public class ExitStopObservationTests
    {
        [Fact]
        public void StandingOrderUnderOwnershipIsStopEvidence()
        {
            Assert.Equal(ExitStopObservation.Stopped,
                ExitStopObservation.Judge("plan-1-task-1-exit", "obj-a", "plan-1-task-1-exit",
                    true, "obj-a", "StandYourGround"));
            Assert.True(ExitStopObservation.CountsAsStopEvidence(ExitStopObservation.Stopped));
        }

        [Fact]
        public void StillAdvancingUnderOwnershipIsNotStopEvidence()
        {
            // 归属仍在自己、对象也没变，但订单还在推进：这就是"写完不等于停住"。
            Assert.Equal(ExitStopObservation.NotStopped,
                ExitStopObservation.Judge("plan-1-task-1-exit", "obj-a", "plan-1-task-1-exit",
                    true, "obj-a", "Charge"));
            Assert.False(ExitStopObservation.CountsAsStopEvidence(ExitStopObservation.NotStopped));
        }

        [Fact]
        public void SupersededBySuccessorCannotCountAsStop()
        {
            // 对象已被新分配接管：此刻若是 StandYourGround，那也是后继决定的结果，
            // 不能反过来记成本次退出成功；若是 Charge，同样不能记成本次退出失败。
            const string verdict = ExitStopObservation.Superseded;
            Assert.Equal(verdict,
                ExitStopObservation.Judge("plan-1-task-1-exit", "obj-a", "plan-2-task-1",
                    true, "obj-a", "StandYourGround"));
            Assert.False(ExitStopObservation.CountsAsStopEvidence(verdict));

            Assert.Equal(verdict,
                ExitStopObservation.Judge("plan-1-task-1-exit", "obj-a", "plan-2-task-1",
                    true, "obj-a", "Charge"));
        }

        [Fact]
        public void ReplacedObjectCannotCountAsStop()
        {
            // 编队对象已不是关闭时那个：读数与该退出无关，既不算成功也不算失败。
            Assert.Equal(ExitStopObservation.ObjectReplaced,
                ExitStopObservation.Judge("plan-1-task-1-exit", "obj-a", "plan-1-task-1-exit",
                    true, "obj-b", "StandYourGround"));
            Assert.False(ExitStopObservation.CountsAsStopEvidence(ExitStopObservation.ObjectReplaced));
        }

        [Fact]
        public void MissingTargetIsNeitherStopNorFailure()
        {
            Assert.Equal(ExitStopObservation.TargetGone,
                ExitStopObservation.Judge("plan-1-task-1-exit", "obj-a", "plan-1-task-1-exit",
                    false, "", ""));
            Assert.False(ExitStopObservation.CountsAsStopEvidence(ExitStopObservation.TargetGone));
        }

        [Fact]
        public void ClosedExitTaskStillJudgedAfterCurrentPlanSwitched()
        {
            // 本判定不接收"当前计划"，因此计划切换在结构上无法影响它。
            // 任务已关闭（ClosedAt 已置）但台账仍保留对象时，读数照常产生。
            var exit = new FormationTask
            {
                TaskId = "plan-1-task-1-exit",
                PlanId = "plan-1",
                OriginTaskId = "plan-1-task-1",
                FormationKey = "1/0",
                ObjectIdentity = "obj-a",
                WrittenAt = 11.032,
            };
            var ledger = new TaskLedger();
            ledger.Add(exit);
            ledger.CloseTask(exit.TaskId, 11.032);

            var kept = ledger.Get(exit.TaskId);
            Assert.NotNull(kept);
            Assert.True(kept.IsClosed);
            Assert.Equal(ExitStopObservation.Stopped,
                ExitStopObservation.Judge(kept.TaskId, kept.ObjectIdentity, kept.TaskId,
                    true, "obj-a", "StandYourGround"));
        }

        [Fact]
        public void UnwrittenExitTaskProducesNoReading()
        {
            // 未写出（未获选或未到执行点）的退出任务不产生读数，不能靠"没有读数"推断停住。
            var exit = new FormationTask
            {
                TaskId = "plan-1-task-1-exit",
                PlanId = "plan-1",
                OriginTaskId = "plan-1-task-1",
                ObjectIdentity = "obj-a",
            };
            Assert.True(exit.WrittenAt < 0.0);
        }
    }
}
