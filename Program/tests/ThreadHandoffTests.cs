using CombatBrain2.Core;
using Xunit;

namespace CombatBrain2.Tests
{
    // 主线程执行保障：获选回调若不在已确认的主线程上，不得就地写令，
    // 只能把本次命令最小转交给主线程执行点。
    public class ThreadHandoffTests
    {
        [Fact]
        public void UnknownMainThreadRejectsEveryThread()
        {
            var h = new ThreadHandoff();

            Assert.False(h.HasMainThread);
            Assert.False(h.IsMainThread(30));
            Assert.False(h.IsMainThread(56));
        }

        [Fact]
        public void NonMainThreadCallbackIsDeferredNotExecuted()
        {
            var h = new ThreadHandoff();
            h.MarkMainThread(30);

            Assert.False(h.IsMainThread(56));
            Assert.True(h.TryDefer("i1", "1/0", "aaa@Infantry", 56, 15.5));
            Assert.True(h.HasPending);
            Assert.Equal(1, h.DeferredCount);
            Assert.Equal(0, h.DirectCount);

            var p = h.TakePending();
            Assert.Equal("i1", p.IntentId);
            Assert.Equal("1/0", p.Key);
            Assert.Equal("aaa@Infantry", p.ObjectIdentity);
            Assert.Equal(56, p.CallbackThreadId);
            Assert.False(h.HasPending);
        }

        [Fact]
        public void MainThreadCallbackCountsAsDirect()
        {
            var h = new ThreadHandoff();
            h.MarkMainThread(30);

            Assert.True(h.IsMainThread(30));
            h.NoteDirect();

            Assert.Equal(1, h.DirectCount);
            Assert.Equal(0, h.DeferredCount);
            Assert.False(h.HasPending);
        }

        [Fact]
        public void PendingSlotHoldsOnlyOneCommand()
        {
            var h = new ThreadHandoff();
            h.MarkMainThread(30);

            Assert.True(h.TryDefer("i1", "1/0", "aaa@Infantry", 56, 15.5));
            Assert.False(h.TryDefer("i1", "1/1", "bbb@Infantry", 57, 15.6));

            var p = h.TakePending();
            Assert.Equal("1/0", p.Key);
            Assert.Equal(1, h.DeferredCount);
        }

        [Fact]
        public void ClearDropsPendingCommand()
        {
            var h = new ThreadHandoff();
            h.MarkMainThread(30);
            h.TryDefer("i1", "1/0", "aaa@Infantry", 56, 15.5);

            h.Clear();

            Assert.False(h.HasPending);
        }

        [Fact]
        public void MainThreadMarkIsTakenOnce()
        {
            var h = new ThreadHandoff();
            h.MarkMainThread(30);
            h.MarkMainThread(31);

            Assert.Equal(30, h.MainThreadId);
        }
    }
}
