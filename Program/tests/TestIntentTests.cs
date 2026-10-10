using System;
using CombatBrain2.Core;
using Xunit;

namespace CombatBrain2.Tests
{
    public class TestIntentTests
    {
        private static TestIntent Intent(double triggeredAt)
        {
            return new TestIntent
            {
                IntentId = "i1",
                TriggeredAt = triggeredAt,
                Deadline = triggeredAt + TestParameters.IntentWindowSeconds,
            };
        }

        // 窗口内有效，到期即关。
        [Fact]
        public void WindowClosesAtFixedDeadline()
        {
            var i = Intent(10.0);
            Assert.True(i.IsOpen(10.0));
            Assert.True(i.IsOpen(10.0 + TestParameters.IntentWindowSeconds - 0.001));
            Assert.False(i.IsOpen(10.0 + TestParameters.IntentWindowSeconds));
            Assert.False(i.IsOpen(10.0 + TestParameters.IntentWindowSeconds + 5.0));
        }

        // 截止点是固定的：不因重复回调而延长。
        [Fact]
        public void RepeatedCallbacksDoNotExtendWindow()
        {
            var i = Intent(10.0);
            var originalDeadline = i.Deadline;
            for (int n = 0; n < 100; n++)
            {
                i.IsOpen(10.0 + n * 0.1);
            }
            Assert.Equal(originalDeadline, i.Deadline);
        }

        // 首次获选也不延长窗口。
        [Fact]
        public void FirstSelectionDoesNotExtendWindow()
        {
            var i = Intent(10.0);
            var originalDeadline = i.Deadline;
            i.SelectedAt = 12.0;
            i.BoundKey = "1/0";
            i.IsOpen(12.0);
            Assert.Equal(originalDeadline, i.Deadline);
        }

        // 获选等待上限自触发时刻起算，不是自获选起算。
        [Fact]
        public void SelectionDeadlineIsRelativeToTrigger()
        {
            var i = Intent(10.0);
            Assert.Equal(10.0 + TestParameters.SelectionWaitSeconds, i.SelectionDeadline());
        }

        // 关闭后即使窗口未到也不再打开。
        [Fact]
        public void ClosedIntentStaysClosed()
        {
            var i = Intent(10.0);
            i.IsClosed = true;
            i.CancelReason = CancelReason.ExplicitClose;
            Assert.False(i.IsOpen(11.0));
        }

        // 参数值就是批准的三个数。
        [Fact]
        public void ApprovedParametersArePinned()
        {
            Assert.Equal(2.0, TestParameters.SnapshotMaxAgeSeconds);
            Assert.Equal(20.0, TestParameters.IntentWindowSeconds);
            Assert.Equal(10.0, TestParameters.SelectionWaitSeconds);
        }
    }
}
