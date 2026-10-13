using System;
using CombatBrain2.Core;
using Xunit;

namespace CombatBrain2.Tests
{
    // 退出尝试登记与归属判定（派单 §6）：纯逻辑，不触碰引擎。
    //
    // 三个身份必须分清（对应 Host 真实调用顺序）：
    //   originTaskId —— 被关闭的普通任务 ID（登记键的成分，也由退出任务携带）
    //   ownerTaskId  —— 当前接管该对象的任务 ID（归属检查用）
    //   obj          —— 编队对象身份
    // Host 顺序：关闭普通任务 → TryRegisterIfNotTakenOver → 入队并 TakeOwnership(退出任务)
    //            → 后续写令/超时前用 MayActOn(obj, origin, owner) 复核。
    public class ExitStopLedgerTests
    {
        [Fact]
        public void SameCloseSameObjectRegistersOnce()
        {
            var ledger = new ExitStopLedger();
            Assert.True(ledger.TryRegister("obj-a", "plan-1-task-1"));
            Assert.False(ledger.TryRegister("obj-a", "plan-1-task-1"));
            Assert.Equal(1, ledger.RegisteredCount);
        }

        [Fact]
        public void SameObjectUnderLaterTaskRegistersAgain()
        {
            var ledger = new ExitStopLedger();
            Assert.True(ledger.TryRegister("obj-a", "plan-1-task-1"));
            // 同一对象承担新计划、再次失效：新关闭任务产生新的退出尝试。
            Assert.True(ledger.TryRegister("obj-a", "plan-2-task-5"));
            Assert.Equal(2, ledger.RegisteredCount);
        }

        [Fact]
        public void ThreeDistinctObjectsAllRegister()
        {
            var ledger = new ExitStopLedger();
            Assert.True(ledger.TryRegister("obj-a", "plan-1-task-1"));
            Assert.True(ledger.TryRegister("obj-b", "plan-1-task-2"));
            Assert.True(ledger.TryRegister("obj-c", "plan-1-task-3"));
            Assert.Equal(3, ledger.RegisteredCount);
        }

        [Fact]
        public void MissionCleanupDoesNotInheritRegistration()
        {
            var ledger = new ExitStopLedger();
            Assert.True(ledger.TryRegister("obj-a", "plan-1-task-1"));
            ledger.TakeOwnership("obj-a", "plan-2-task-5");
            ledger.ClearMission();

            Assert.Equal(0, ledger.RegisteredCount);
            Assert.False(ledger.IsRegistered("obj-a", "plan-1-task-1"));
            Assert.Equal("", ledger.OwnerOf("obj-a"));
            // 清理后同一「对象 + 关闭任务」可重新登记，不继承上一场状态。
            Assert.True(ledger.TryRegister("obj-a", "plan-1-task-1"));
        }

        [Fact]
        public void EmptyObjectIdentityIsNeverRegistered()
        {
            var ledger = new ExitStopLedger();
            Assert.False(ledger.TryRegister("", "plan-1-task-1"));
            Assert.False(ledger.TryRegisterIfNotTakenOver("", "plan-1-task-1"));
            Assert.False(ledger.IsRegistered("", "plan-1-task-1"));
            Assert.False(ledger.MayActOn("", "plan-1-task-1", "plan-1-task-1-exit"));
            Assert.Equal(0, ledger.RegisteredCount);
        }

        // 缺口 1 的反例：普通新分配必须在公布时接管归属，
        // 而不是等首次写令——否则旧退出在两者之间仍可作用于该对象。
        [Fact]
        public void NewNormalAssignmentTakesOwnershipAtPublication()
        {
            var ledger = new ExitStopLedger();
            // 旧退出先成立并接管。
            ledger.TryRegister("obj-a", "plan-1-task-1");
            ledger.TakeOwnership("obj-a", "plan-1-task-1-exit");
            Assert.True(ledger.MayActOn("obj-a", "plan-1-task-1", "plan-1-task-1-exit"));

            // 新普通任务公布（尚未写令）即接管归属。
            ledger.TakeOwnership("obj-a", "plan-2-task-5");

            // 旧退出立刻失去处理权，不等新任务写令。
            Assert.False(ledger.MayActOn("obj-a", "plan-1-task-1", "plan-1-task-1-exit"));
            Assert.Equal("plan-2-task-5", ledger.OwnerOf("obj-a"));
        }

        // 缺口 2 的反例：新任务已接管后，迟到的旧关闭回调不得入队后夺回归属。
        [Fact]
        public void LateCloseCallbackCannotTakeOwnershipBack()
        {
            var ledger = new ExitStopLedger();
            ledger.TryRegister("obj-a", "plan-1-task-1");
            ledger.TakeOwnership("obj-a", "plan-1-task-1-exit");

            // 更新分配（普通任务或更新的退出任务）接管。
            ledger.TakeOwnership("obj-a", "plan-2-task-5");

            // 迟到的旧关闭回调尝试登记：必须被拒，且不得改动归属。
            Assert.False(ledger.TryRegisterIfNotTakenOver("obj-a", "plan-1-task-1"));
            Assert.Equal("plan-2-task-5", ledger.OwnerOf("obj-a"));
            Assert.False(ledger.MayActOn("obj-a", "plan-1-task-1", "plan-1-task-1-exit"));

            // 若放行入队，旧退出会重新成为拥有者——这里断言它根本没有被登记成新拥有者。
            ledger.TakeOwnership("obj-a", "plan-1-task-1-exit");
            Assert.True(ledger.StillOwnedBy("obj-a", "plan-1-task-1-exit"));
        }

        [Fact]
        public void FirstCloseCallbackIsNotBlockedWhenNotTakenOver()
        {
            var ledger = new ExitStopLedger();
            // 无任何接管时，首次关闭回调必须能登记（否则正当退出被误挡）。
            Assert.True(ledger.TryRegisterIfNotTakenOver("obj-a", "plan-1-task-1"));
            Assert.Equal(1, ledger.RegisteredCount);
        }

        // 缺口 3：登记用普通任务 ID、归属检查用退出任务 ID——两者必须经 OriginTaskId 关联。
        [Fact]
        public void ExitTaskIsAuthorizedThroughItsOriginIdentity()
        {
            var ledger = new ExitStopLedger();
            Assert.True(ledger.TryRegisterIfNotTakenOver("obj-a", "plan-1-task-1"));
            ledger.TakeOwnership("obj-a", "plan-1-task-1-exit");

            // Host 真实调用：origin=被关闭的普通任务，owner=退出任务自己。
            Assert.True(ledger.MayActOn("obj-a", "plan-1-task-1", "plan-1-task-1-exit"));
            // 用退出任务 ID 当 origin 查不到登记：身份不能混用。
            Assert.False(ledger.MayActOn("obj-a", "plan-1-task-1-exit", "plan-1-task-1-exit"));
        }

        // 反例必须覆盖"旧退出尚未到一秒"：归属判定不依赖时间。
        [Fact]
        public void OwnershipBlocksOldExitBeforeOneSecond()
        {
            var ledger = new ExitStopLedger();
            ledger.TryRegister("obj-a", "plan-1-task-1");
            ledger.TakeOwnership("obj-a", "plan-1-task-1-exit");
            ledger.TakeOwnership("obj-a", "plan-2-task-5");

            // 没有任何时间参数参与：判定在接管后立即成立，
            // 因此旧退出在 1 s 到期前就已被挡住，不必等到自然到期。
            Assert.False(ledger.MayActOn("obj-a", "plan-1-task-1", "plan-1-task-1-exit"));
            Assert.False(ledger.StillOwnedBy("obj-a", "plan-1-task-1-exit"));
        }

        [Fact]
        public void UnregisteredTaskMayNotActEvenIfOwnershipMatches()
        {
            var ledger = new ExitStopLedger();
            ledger.TakeOwnership("obj-a", "plan-2-task-5-exit");
            // 从未登记过的任务即使归属吻合也不得处理：登记与归属两个条件缺一不可。
            Assert.False(ledger.MayActOn("obj-a", "plan-2-task-5", "plan-2-task-5-exit"));
            Assert.True(ledger.StillOwnedBy("obj-a", "plan-2-task-5-exit"));
        }

        [Fact]
        public void OwnershipOfOneObjectDoesNotAffectAnother()
        {
            var ledger = new ExitStopLedger();
            ledger.TryRegister("obj-a", "plan-1-task-1");
            ledger.TryRegister("obj-b", "plan-1-task-2");
            ledger.TakeOwnership("obj-a", "plan-1-task-1-exit");
            ledger.TakeOwnership("obj-b", "plan-1-task-2-exit");
            ledger.TakeOwnership("obj-a", "plan-2-task-5");

            Assert.False(ledger.MayActOn("obj-a", "plan-1-task-1", "plan-1-task-1-exit"));
            Assert.True(ledger.MayActOn("obj-b", "plan-1-task-2", "plan-1-task-2-exit"));
        }

        [Fact]
        public void ReassigningObjectBackRestoresOwnershipForNewTask()
        {
            var ledger = new ExitStopLedger();
            ledger.TakeOwnership("obj-a", "plan-1-task-1-exit");
            ledger.TakeOwnership("obj-a", "plan-2-task-5");
            ledger.TakeOwnership("obj-a", "plan-3-task-7");

            // 归属被后续分配覆盖后，更早的任务不能再处理。
            Assert.False(ledger.StillOwnedBy("obj-a", "plan-1-task-1-exit"));
            Assert.False(ledger.StillOwnedBy("obj-a", "plan-2-task-5"));
            Assert.True(ledger.StillOwnedBy("obj-a", "plan-3-task-7"));
        }

        [Fact]
        public void UnguardedObjectIsNotTreatedAsTakenOver()
        {
            var ledger = new ExitStopLedger();
            ledger.TryRegister("obj-a", "plan-1-task-1");
            // 未登记归属不得被当成"已被接管"，否则正当退出会被误挡。
            Assert.True(ledger.StillOwnedBy("obj-a", "plan-1-task-1-exit"));
            Assert.True(ledger.MayActOn("obj-a", "plan-1-task-1", "plan-1-task-1-exit"));
        }

        // 注册入口不等于清槽入口：MayActOn 是给三个实际处理点（写令、超时撤权、清槽）
        // 共用的判据，登记成立本身不足以放行。
        [Fact]
        public void RegistrationAloneDoesNotAuthorizeAction()
        {
            var ledger = new ExitStopLedger();
            Assert.True(ledger.TryRegisterIfNotTakenOver("obj-a", "plan-1-task-1"));

            // 情形一：该对象已有别的新分配接管 —— 登记成立也不放行。
            ledger.TakeOwnership("obj-a", "plan-2-task-5");
            Assert.False(ledger.MayActOn("obj-a", "plan-1-task-1", "plan-1-task-1-exit"));
            Assert.True(ledger.IsRegistered("obj-a", "plan-1-task-1"));

            // 情形二：未登记过该次关闭 —— 归属吻合也不放行。
            Assert.False(ledger.MayActOn("obj-a", "plan-9-task-9", "plan-2-task-5"));
            Assert.True(ledger.StillOwnedBy("obj-a", "plan-2-task-5"));
        }

        [Fact]
        public void OwnershipFollowsRealHostOrderAcrossTwoCloses()
        {
            var ledger = new ExitStopLedger();

            // 第一次关闭：登记 → 入队接管。
            Assert.True(ledger.TryRegisterIfNotTakenOver("obj-a", "plan-1-task-1"));
            ledger.TakeOwnership("obj-a", "plan-1-task-1-exit");
            Assert.True(ledger.MayActOn("obj-a", "plan-1-task-1", "plan-1-task-1-exit"));

            // 新普通分配公布即接管。
            ledger.TakeOwnership("obj-a", "plan-2-task-5");
            Assert.False(ledger.MayActOn("obj-a", "plan-1-task-1", "plan-1-task-1-exit"));

            // 第二次关闭：登记 → 入队接管；旧退出全程不得复权。
            Assert.True(ledger.TryRegisterIfNotTakenOver("obj-a", "plan-2-task-5"));
            ledger.TakeOwnership("obj-a", "plan-2-task-5-exit");
            Assert.True(ledger.MayActOn("obj-a", "plan-2-task-5", "plan-2-task-5-exit"));
            Assert.False(ledger.MayActOn("obj-a", "plan-1-task-1", "plan-1-task-1-exit"));
        }

        // 普通任务撤权也用 StillOwnedBy 判归属（不套退出专用的登记条件）。
        // 关键：旧、新任务可能写同一种命令，实际值相等不能证明旧任务仍有权恢复。
        [Fact]
        public void NormalTaskCannotRevokeAfterNewerAssignmentTakesOver()
        {
            var ledger = new ExitStopLedger();
            ledger.TakeOwnership("obj-a", "plan-1-task-1");
            Assert.True(ledger.StillOwnedBy("obj-a", "plan-1-task-1"));

            // 新分配公布即接管（尚未获选、尚未写令）。
            ledger.TakeOwnership("obj-a", "plan-2-task-5");

            // 即使两条任务写入的命令枚举相同，旧任务也无权恢复：身份先于命令值。
            Assert.False(ledger.StillOwnedBy("obj-a", "plan-1-task-1"));
            Assert.True(ledger.StillOwnedBy("obj-a", "plan-2-task-5"));
        }

        // 未登记归属的对象不得被当成"已被接管"，否则普通任务的合法撤权会被误挡。
        [Fact]
        public void NormalTaskRevocationIsNotBlockedWhenNoOwnershipRecorded()
        {
            var ledger = new ExitStopLedger();
            Assert.True(ledger.StillOwnedBy("obj-a", "plan-1-task-1"));
        }

        // 普通任务的归属保护不要求登记：登记是退出流程的判据，套到普通任务上会让其永不撤权。
        [Fact]
        public void NormalTaskOwnershipDoesNotRequireRegistration()
        {
            var ledger = new ExitStopLedger();
            ledger.TakeOwnership("obj-a", "plan-1-task-1");

            Assert.False(ledger.IsRegistered("obj-a", "plan-1-task-1"));
            Assert.True(ledger.StillOwnedBy("obj-a", "plan-1-task-1"));
        }
    }
}
