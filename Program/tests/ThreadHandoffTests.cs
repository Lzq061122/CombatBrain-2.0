using CombatBrain2.Core;
using System.Threading;
using System.Threading.Tasks;
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
            Assert.True(h.TryDefer("m1", "plan-0", "t1", "1/0", "aaa@Infantry", 56, 15.5));
            Assert.True(h.HasPending);
            Assert.Equal(1, h.DeferredCount);
            Assert.Equal(0, h.DirectCount);

            var taken = h.TakeAllPending();
            Assert.Single(taken);
            var p = taken[0];
            Assert.Equal("t1", p.TaskId);
            Assert.Equal("plan-0", p.PlanId);
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

        // 同一编队键上先到者保留：不同任务不得覆盖槽位里已有的命令。
        [Fact]
        public void SameKeyDifferentTaskKeepsFirstArrival()
        {
            var h = new ThreadHandoff();
            h.MarkMainThread(30);

            Assert.True(h.TryDefer("m1", "plan-0", "t1", "1/0", "aaa@Infantry", 56, 15.5));
            Assert.False(h.TryDefer("m1", "plan-0", "t2", "1/0", "aaa@Infantry", 57, 15.6));

            var taken = h.TakeAllPending();
            Assert.Single(taken);
            Assert.Equal("t1", taken[0].TaskId);
            Assert.Equal(1, h.DeferredCount);
            Assert.Equal(1, h.SlotBusyCount);
            Assert.Equal(0, h.DuplicateCount);
        }

        // 同一编队键上同一任务重复投递计入去重，不重复占用。
        [Fact]
        public void SameKeySameTaskCountsAsDuplicate()
        {
            var h = new ThreadHandoff();
            h.MarkMainThread(30);

            Assert.True(h.TryDefer("m1", "plan-0", "t1", "1/0", "aaa@Infantry", 56, 15.5));
            Assert.False(h.TryDefer("m1", "plan-0", "t1", "1/0", "aaa@Infantry", 56, 15.6));

            var taken = h.TakeAllPending();
            Assert.Single(taken);
            Assert.Equal(1, h.DeferredCount);
            Assert.Equal(0, h.SlotBusyCount);
            Assert.Equal(1, h.DuplicateCount);
        }

        // 每个编队各有独立槽位：一支编队的占用不得挡住另一支编队的命令。
        [Fact]
        public void DifferentKeysHaveIndependentSlots()
        {
            var h = new ThreadHandoff();
            h.MarkMainThread(30);

            Assert.True(h.TryDefer("m1", "plan-0", "t1", "1/0", "aaa@Infantry", 56, 15.5));
            Assert.True(h.TryDefer("m1", "plan-0", "t2", "1/1", "bbb@Infantry", 57, 15.6));

            var taken = h.TakeAllPending();
            Assert.Equal(2, taken.Count);
            Assert.Equal("1/0", taken[0].Key);
            Assert.Equal("1/1", taken[1].Key);
            Assert.Equal(2, h.DeferredCount);
            Assert.Equal(0, h.SlotBusyCount);
        }

        [Fact]
        public void ClearDropsPendingCommand()
        {
            var h = new ThreadHandoff();
            h.MarkMainThread(30);
            h.TryDefer("m1", "plan-0", "t1", "1/0", "aaa@Infantry", 56, 15.5);

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

        // 同一空槽位被多个回调线程同时投递时，只允许一条进槽位，
        // 且 DeferredCount 必须与实际接受的条数一致（不能丢更新）。
        [Fact]
        public void ConcurrentDeferAttemptsAreSerialized()
        {
            for (int round = 0; round < 40; round++)
            {
                var h = new ThreadHandoff();
                h.MarkMainThread(30);

                int accepted = 0;
                int workerCount = 8;
                var threads = new Thread[workerCount];
                using (var barrier = new Barrier(workerCount))
                {
                    for (int i = 0; i < workerCount; i++)
                    {
                        int tid = 100 + i;
                        threads[i] = new Thread(() =>
                        {
                            barrier.SignalAndWait();
                            if (h.TryDefer("m1", "plan-0", "t" + tid, "1/0", "obj" + tid, tid, 15.5))
                                Interlocked.Increment(ref accepted);
                        });
                        threads[i].Start();
                    }
                    foreach (var t in threads) t.Join();
                }

                Assert.True(accepted <= 1, "accepted=" + accepted + " round=" + round);
                Assert.Equal(accepted, h.DeferredCount);
            }
        }

        // 槽位里那条命令被取出时只能被交付一次，不能两个消费方同时拿到同一条。
        [Fact]
        public void PendingCommandIsDeliveredExactlyOnce()
        {
            for (int round = 0; round < 40; round++)
            {
                var h = new ThreadHandoff();
                h.MarkMainThread(30);
                h.TryDefer("m1", "plan-0", "t1", "1/0", "aaa@Infantry", 56, 15.5);

                int taken = 0;
                int workerCount = 4;
                var threads = new Thread[workerCount];
                using (var barrier = new Barrier(workerCount))
                {
                    for (int i = 0; i < workerCount; i++)
                    {
                        threads[i] = new Thread(() =>
                        {
                            barrier.SignalAndWait();
                            var p = h.TakeAllPending();
                            if (p != null && p.Count > 0) Interlocked.Add(ref taken, p.Count);
                        });
                        threads[i].Start();
                    }
                    foreach (var t in threads) t.Join();
                }

                Assert.Equal(1, taken);
                Assert.False(h.HasPending);
            }
        }
    }
}
