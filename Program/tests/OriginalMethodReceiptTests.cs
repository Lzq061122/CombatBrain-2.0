using CombatBrain2.Core;
using Xunit;

namespace CombatBrain2.Tests
{
    // 原方法完成/被跳过的回执：只有武装窗口内观察到原方法体入口，
    // 才能说这次 setter 的原方法真的执行了；普通返回不算。
    public class OriginalMethodReceiptTests
    {
        [Fact]
        public void EnteredWhenOriginalBodyRuns()
        {
            var r = new OriginalMethodReceipt();

            r.Arm();
            r.ObserveEnter(56);
            bool entered = r.Disarm();

            Assert.True(entered);
            Assert.Equal(56, r.EnterThreadId);
        }

        [Fact]
        public void SkippedWhenOriginalBodyNeverRuns()
        {
            var r = new OriginalMethodReceipt();

            r.Arm();
            bool entered = r.Disarm();

            Assert.False(entered);
            Assert.Equal(-1, r.EnterThreadId);
        }

        [Fact]
        public void EnterIsIgnoredWhileNotArmed()
        {
            var r = new OriginalMethodReceipt();

            r.ObserveEnter(56);
            r.Arm();
            bool entered = r.Disarm();

            Assert.False(entered);
        }

        [Fact]
        public void ArmResetsPreviousObservation()
        {
            var r = new OriginalMethodReceipt();

            r.Arm();
            r.ObserveEnter(56);
            r.Disarm();

            r.Arm();
            bool entered = r.Disarm();

            Assert.False(entered);
        }

        [Fact]
        public void RepeatedEnterKeepsFirstThread()
        {
            var r = new OriginalMethodReceipt();

            r.Arm();
            r.ObserveEnter(56);
            r.ObserveEnter(57);
            bool entered = r.Disarm();

            Assert.True(entered);
            Assert.Equal(56, r.EnterThreadId);
        }
    }
}
