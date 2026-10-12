using System;
using CombatBrain2.Core;
using Xunit;

namespace CombatBrain2.Tests
{
    // 控制权安全边沿：资格失效后，待写命令必须不再可送达旧意图。
    public class EligibilityLossTests
    {
        // 取消原因枚举必须显式区分"AI 控制资格丢失"，
        // 不能借用 PlayerEligibilityLost 或 WindowExpired 冒充。
        [Fact]
        public void CancelReasonHasExplicitAiControlLost()
        {
            Assert.True(Enum.IsDefined(typeof(CancelReason), "AiControlLost"));
            Assert.NotEqual(CancelReason.PlayerEligibilityLost, CancelReason.AiControlLost);
        }

        // 资格失效取消时清掉的必须是"待写命令槽位"，不能顺带清掉主线程登记。
        // 否则后续同一 Mission 内的所有回调都会被当成非主线程而失去主线程执行点语义。
        [Fact]
        public void ClearingPendingKeepsMainThreadRegistration()
        {
            var h = new ThreadHandoff();
            h.MarkMainThread(33);
            Assert.True(h.TryDefer("m1", "plan-0", "t1", "1/0", "aaa@Infantry", 37, 15.5));
            Assert.True(h.HasPending);

            h.Clear();

            Assert.False(h.HasPending);
            Assert.True(h.HasMainThread);
            Assert.Equal(33, h.MainThreadId);
            Assert.True(h.IsMainThread(33));
        }

        // 旧意图已写入后丢失资格：再次请求写入不得被放行。
        // "只命中 already_written"不足以证明让位，但放行一定是错的——
        // 真正的让位证据在 Host 侧撤权日志里，这里保证 RequestWrite 不会放行。
        [Fact]
        public void AlreadyWrittenIntentNeverWritesAgainAfterEligibilityLoss()
        {
            var gate = new ExecutionGate();
            gate.SubmitCandidate("i2", "1/0", 40.0);
            gate.BindObject("i2", "aaa@Infantry");
            gate.MarkSelected("i2", "1/0", 40.5);

            var first = gate.RequestWrite("i2", "1/0", 40.6, true, true, false, "aaa@Infantry", true, true);
            Assert.True(first.Allowed);

            // 资格丢失：not_ai_controlled 为 false 之后仍不得放行，且写入计数不增。
            for (int n = 0; n < 10; n++)
            {
                var d = gate.RequestWrite("i2", "1/0", 41.0 + n * 0.1, true, true, false, "aaa@Infantry", false, true);
                Assert.False(d.Allowed);
            }
            Assert.Equal(1, gate.WriteAttempts);
        }

        // 按意图分别计数：全局累加会掩盖"某个具体意图是否又写了一次"。
        [Fact]
        public void WriteAttemptsAreCountedPerIntent()
        {
            var gate = new ExecutionGate();
            gate.SubmitCandidate("i1", "1/0", 15.0);
            gate.BindObject("i1", "aaa@Infantry");
            gate.MarkSelected("i1", "1/0", 15.5);
            var d1 = gate.RequestWrite("i1", "1/0", 15.6, true, true, false, "aaa@Infantry", true, true);
            Assert.True(d1.Allowed);

            gate.SubmitCandidate("i2", "1/1", 40.0);
            gate.BindObject("i2", "bbb@Archers");
            gate.MarkSelected("i2", "1/1", 40.5);
            var d2 = gate.RequestWrite("i2", "1/1", 40.6, true, true, false, "bbb@Archers", true, true);
            Assert.True(d2.Allowed);

            // 被拒绝的请求不得计入该意图的写入次数。
            var d3 = gate.RequestWrite("i2", "1/1", 40.7, true, true, false, "bbb@Archers", false, true);
            Assert.False(d3.Allowed);

            Assert.Equal(1, gate.WriteAttemptsFor("i1"));
            Assert.Equal(1, gate.WriteAttemptsFor("i2"));
            Assert.Equal(2, gate.WriteAttempts);
            Assert.Equal(0, gate.WriteAttemptsFor("unknown"));
        }

        // 场景 A 的核心：未写入的意图在资格丢失后不得产生任何写入尝试。
        [Fact]
        public void UnwrittenIntentStaysAtZeroWriteAttempts()
        {
            var gate = new ExecutionGate();
            gate.SubmitCandidate("i1", "1/0", 15.0);
            gate.BindObject("i1", "aaa@Infantry");
            gate.MarkSelected("i1", "1/0", 15.5);

            var d = gate.RequestWrite("i1", "1/0", 15.6, true, true, false, "aaa@Infantry", false, true);
            Assert.False(d.Allowed);
            Assert.Equal(0, gate.WriteAttempts);
        }
    }
}
