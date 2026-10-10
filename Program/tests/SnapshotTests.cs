using System;
using CombatBrain2.Core;
using Xunit;

namespace CombatBrain2.Tests
{
    public class SnapshotTests
    {
        private static FormationRecord Rec(int team, int index, double sampledAt)
        {
            return new FormationRecord
            {
                MissionKey = "m1",
                SampledAt = sampledAt,
                TeamIndex = team,
                Side = "Enemy",
                FormationIndex = index,
                FormationClass = "Infantry",
                UnitCount = 10,
                MovementOrder = "StandYourGround",
                IsAiControlled = true,
                ObjectIdentity = "obj-" + team + "-" + index,
            };
        }

        // 年龄用实际采样时刻算，不用刷新时刻。
        [Fact]
        public void AgeUsesSampledTimeNotRefreshTime()
        {
            var r = Rec(1, 0, 100.0);
            Assert.Equal(1.5, r.AgeAt(101.5), 3);
            Assert.False(r.IsStale(101.5));
            Assert.True(r.IsStale(102.5));
        }

        // 刷新时间戳不能把年龄变小：旧采样点仍然是旧采样点。
        [Fact]
        public void TouchingSnapshotDoesNotRejuvenateSample()
        {
            var s = new FormationSnapshot { MissionKey = "m1", SampledAt = 100.0 };
            s.Records.Add(Rec(1, 0, 100.0));
            s.SampledAt = 200.0; // 冒充刷新
            Assert.True(s.Records[0].IsStale(103.0));
        }

        // 可复用索引造成的重复键必须能被检出。
        [Fact]
        public void DetectsDuplicateKeys()
        {
            var s = new FormationSnapshot { MissionKey = "m1", SampledAt = 100.0 };
            s.Records.Add(Rec(1, 0, 100.0));
            s.Records.Add(Rec(1, 0, 100.0));
            string detail;
            Assert.True(s.HasDuplicateKeys(out detail));
            Assert.Equal("1/0", detail);
        }

        [Fact]
        public void NoDuplicateKeysWhenDistinct()
        {
            var s = new FormationSnapshot { MissionKey = "m1", SampledAt = 100.0 };
            s.Records.Add(Rec(1, 0, 100.0));
            s.Records.Add(Rec(2, 0, 100.0));
            string detail;
            Assert.False(s.HasDuplicateKeys(out detail));
        }

        // 按键查找用于"还是同一个对象"的核对。
        [Fact]
        public void FindByKeyReturnsBoundObject()
        {
            var s = new FormationSnapshot { MissionKey = "m1", SampledAt = 100.0 };
            s.Records.Add(Rec(1, 2, 100.0));
            var found = s.FindByKey("1/2");
            Assert.NotNull(found);
            Assert.Equal("obj-1-2", found.ObjectIdentity);
            Assert.Null(s.FindByKey("9/9"));
        }
    }
}
