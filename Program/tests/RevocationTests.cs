using System;
using CombatBrain2.Core;
using Xunit;

namespace CombatBrain2.Tests
{
    // 撤销：只恢复本方确实改变且仍由本方负责的状态。
    public class RevocationTests
    {
        [Fact]
        public void RevocationPlannedWhenOwnWriteSucceeded()
        {
            var ledger = new WriteLedger();
            ledger.RecordOwnWrite("i1", "1/0", "obj-1-0", "StandYourGround", "Stop", 101.1);
            var plan = ledger.PlanRevocation("i1");
            Assert.True(plan.Needed);
            Assert.Equal("1/0", plan.Key);
            Assert.Equal("StandYourGround", plan.PreviousOrder);
        }

        [Fact]
        public void NoRevocationWhenNothingChanged()
        {
            var ledger = new WriteLedger();
            ledger.RecordOwnWrite("i1", "1/0", "obj-1-0", "Stop", "Stop", 101.1);
            var plan = ledger.PlanRevocation("i1");
            Assert.False(plan.Needed);
        }

        [Fact]
        public void NoRevocationWhenNoWriteHappened()
        {
            var ledger = new WriteLedger();
            var plan = ledger.PlanRevocation("i1");
            Assert.False(plan.Needed);
        }

        // 目标对象身份变了就不再是本方负责的状态，不得覆盖新对象的决策。
        [Fact]
        public void RevocationSkippedWhenObjectIdentityChanged()
        {
            var ledger = new WriteLedger();
            ledger.RecordOwnWrite("i1", "1/0", "obj-1-0", "StandYourGround", "Stop", 101.1);
            var plan = ledger.PlanRevocationIfSameObject("i1", "obj-9-9");
            Assert.False(plan.Needed);
            Assert.Equal("object_identity_changed", plan.SkipReason);
        }

        // Mission 结束时清掉全部意图与对象状态。
        [Fact]
        public void MissionEndClearsAllState()
        {
            var ledger = new WriteLedger();
            ledger.RecordOwnWrite("i1", "1/0", "obj-1-0", "StandYourGround", "Stop", 101.1);
            ledger.RecordOwnWrite("i2", "1/3", "obj-1-3", "StandYourGround", "Stop", 102.1);
            ledger.ClearMission();
            Assert.False(ledger.PlanRevocation("i1").Needed);
            Assert.False(ledger.PlanRevocation("i2").Needed);
        }

        // 前值无法从名称忠实重建时不得回退：MovementOrderMove 带目标位置，
        // 只凭 "Move" 这个名称重建会写出错误的命令并假报已撤销。
        [Fact]
        public void RevocationSkippedWhenPreviousOrderNotRestorable()
        {
            var ledger = new WriteLedger();
            ledger.RecordOwnWrite("i1", "1/0", "obj-1-0", "Move", "StandYourGround", 101.1);
            var plan = ledger.PlanRevocation("i1");
            Assert.False(plan.Needed);
            Assert.Equal("previous_order_not_restorable", plan.SkipReason);
        }

        // ChargeWithTarget 与 Charge 名称相近但带目标，同样不可忠实重建。
        [Fact]
        public void RevocationSkippedForParameterizedOrderNames()
        {
            var ledger = new WriteLedger();
            ledger.RecordOwnWrite("i1", "1/0", "obj-1-0", "ChargeWithTarget", "StandYourGround", 101.1);
            Assert.False(ledger.PlanRevocation("i1").Needed);
        }

        // 无参数命令可以原样重建，仍应回退。
        [Fact]
        public void RevocationAllowedForRestorableOrders()
        {
            foreach (var name in new[] { "Advance", "FallBack", "Charge" })
            {
                var ledger = new WriteLedger();
                ledger.RecordOwnWrite("i1", "1/0", "obj-1-0", name, "StandYourGround", 101.1);
                Assert.True(ledger.PlanRevocation("i1").Needed);
            }
        }

        // 实机观测到写 MovementOrderStop 后 OrderController 报 "StandYourGround"。
        // 若前值以此为名，OrderFromName 能还原成同一条命令，故必须算可重建。
        [Fact]
        public void StandYourGroundIsRestorable()
        {
            Assert.True(WriteLedger.IsRestorableOrderName("StandYourGround"));
            Assert.True(WriteLedger.IsRestorableOrderName("Advance"));
            Assert.True(WriteLedger.IsRestorableOrderName("FallBack"));
            Assert.True(WriteLedger.IsRestorableOrderName("Charge"));
            Assert.False(WriteLedger.IsRestorableOrderName("Move"));
            Assert.False(WriteLedger.IsRestorableOrderName("FollowEntity"));
            Assert.False(WriteLedger.IsRestorableOrderName("FollowMe"));
            Assert.False(WriteLedger.IsRestorableOrderName("Retreat"));
            Assert.False(WriteLedger.IsRestorableOrderName(""));
        }

        // 退出后不得再有本方旧意图的新写入。
        [Fact]
        public void NoNewWritesAfterClose()
        {
            var gate = new ExecutionGate();
            gate.SubmitCandidate("i1", "1/0", 100.0);
            gate.BindObject("i1", "aaa@Infantry");
            gate.MarkSelected("i1", "1/0", 101.0);
            gate.RequestWrite("i1", "1/0", 101.1, true, true, false, "aaa@Infantry", true, true);
            var before = gate.WriteAttempts;
            gate.Close("i1", CancelReason.ExplicitClose, 105.0);
            for (int n = 0; n < 30; n++)
                gate.RequestWrite("i1", "1/0", 105.1 + n * 0.1, true, true, false, "aaa@Infantry", true, true);
            Assert.Equal(before, gate.WriteAttempts);
        }
    }
}
