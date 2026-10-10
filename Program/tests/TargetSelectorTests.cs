using System;
using System.Collections.Generic;
using CombatBrain2.Core;
using Xunit;

namespace CombatBrain2.Tests
{
    public class TargetSelectorTests
    {
        private static FormationRecord Rec(
            int team, int index, string cls, int units,
            bool playerTeam, bool aiControlled, double sampledAt, string order)
        {
            return new FormationRecord
            {
                MissionKey = "m1",
                SampledAt = sampledAt,
                TeamIndex = team,
                Side = playerTeam ? "Player" : "Enemy",
                IsPlayerTeam = playerTeam,
                FormationIndex = index,
                FormationClass = cls,
                UnitCount = units,
                PosX = 10.0 * index,
                PosY = 20.0 * team,
                MovementOrder = order,
                IsAiControlled = aiControlled,
                IsPlayerControlled = false,
                ObjectIdentity = "obj-" + team + "-" + index,
            };
        }

        private static FormationSnapshot Snap(params FormationRecord[] recs)
        {
            var s = new FormationSnapshot { MissionKey = "m1", SampledAt = 100.0 };
            s.Records.AddRange(recs);
            return s;
        }

        // 判据 6 相关：玩家拥有的编队不得被选中。
        [Fact]
        public void DoesNotSelectPlayerOwnedFormation()
        {
            var snap = Snap(Rec(0, 0, "Infantry", 50, true, true, 100.0, "StandYourGround"));
            var r = TargetSelector.Select(snap, 100.0);
            Assert.False(r.HasChoice());
            Assert.Contains("0/0:player_owned", r.Rejections);
        }

        // 非 AI 受控不得被选中。
        [Fact]
        public void DoesNotSelectNonAiControlledFormation()
        {
            var snap = Snap(Rec(1, 0, "Infantry", 50, false, false, 100.0, "StandYourGround"));
            var r = TargetSelector.Select(snap, 100.0);
            Assert.False(r.HasChoice());
            Assert.Contains("1/0:not_ai_controlled", r.Rejections);
        }

        // 非步兵不得被选中。
        [Fact]
        public void DoesNotSelectNonInfantryFormation()
        {
            var snap = Snap(Rec(1, 0, "Ranged", 50, false, true, 100.0, "StandYourGround"));
            var r = TargetSelector.Select(snap, 100.0);
            Assert.False(r.HasChoice());
            Assert.Contains("1/0:not_infantry:Ranged", r.Rejections);
        }

        // 快照年龄超过 2 s 上限不得被选中。
        [Fact]
        public void DoesNotSelectStaleSnapshot()
        {
            var snap = Snap(Rec(1, 0, "Infantry", 50, false, true, 100.0, "StandYourGround"));
            var r = TargetSelector.Select(snap, 100.0 + TestParameters.SnapshotMaxAgeSeconds + 0.001);
            Assert.False(r.HasChoice());
            Assert.Contains("1/0:snapshot_stale", r.Rejections);
        }

        // 恰好等于上限仍可用（> 才判陈旧）。
        [Fact]
        public void AcceptsSnapshotExactlyAtAgeLimit()
        {
            var snap = Snap(Rec(1, 0, "Infantry", 50, false, true, 100.0, "StandYourGround"));
            var r = TargetSelector.Select(snap, 100.0 + TestParameters.SnapshotMaxAgeSeconds);
            Assert.True(r.HasChoice());
        }

        // 空编队不得被选中。
        [Fact]
        public void DoesNotSelectEmptyFormation()
        {
            var snap = Snap(Rec(1, 0, "Infantry", 0, false, true, 100.0, "StandYourGround"));
            var r = TargetSelector.Select(snap, 100.0);
            Assert.False(r.HasChoice());
            Assert.Contains("1/0:empty", r.Rejections);
        }

        // 稳定排序字段：(TeamIndex, FormationIndex) 升序。
        [Fact]
        public void SelectsByStableAscendingOrder()
        {
            var snap = Snap(
                Rec(2, 3, "Infantry", 40, false, true, 100.0, "StandYourGround"),
                Rec(1, 1, "Infantry", 40, false, true, 100.0, "StandYourGround"),
                Rec(1, 0, "Infantry", 40, false, true, 100.0, "StandYourGround"));
            var r = TargetSelector.Select(snap, 100.0);
            Assert.True(r.HasChoice());
            Assert.Equal("1/0", r.Chosen.Key());
            Assert.Equal("prefer_non_stop_then_ascending(TeamIndex,FormationIndex)", r.SortBasis);
            Assert.Equal(3, r.Candidates.Count);
        }

        // 不硬编码 T1F0：只有一支符合时必须选它，即使索引不是 0。
        [Fact]
        public void DoesNotHardcodeHistoricalTarget()
        {
            var snap = Snap(
                Rec(1, 0, "Ranged", 40, false, true, 100.0, "StandYourGround"),
                Rec(1, 3, "Infantry", 40, false, true, 100.0, "StandYourGround"));
            var r = TargetSelector.Select(snap, 100.0);
            Assert.True(r.HasChoice());
            Assert.Equal("1/3", r.Chosen.Key());
        }

        // 目标已绑定后对象身份要能被核对。
        [Fact]
        public void ChosenCarriesObjectIdentity()
        {
            var snap = Snap(Rec(1, 2, "Infantry", 40, false, true, 100.0, "StandYourGround"));
            var r = TargetSelector.Select(snap, 100.0);
            Assert.Equal("obj-1-2", r.Chosen.ObjectIdentity);
        }

        // 优先选测试前 MovementOrder 非 Stop 的目标，以证明本次确实改变了状态。
        [Fact]
        public void PrefersNonStopTargetToProveStateChange()
        {
            var snap = Snap(
                Rec(1, 0, "Infantry", 40, false, true, 100.0, "StandYourGround"),
                Rec(1, 1, "Infantry", 40, false, true, 100.0, "Advance"));
            var r = TargetSelector.Select(snap, 100.0);
            Assert.True(r.HasChoice());
            Assert.Equal("1/1", r.Chosen.Key());
            Assert.Equal("advance", r.Chosen.MovementOrder.ToLowerInvariant());
        }

        // 全部已是 Stop 时仍可选：同值调用只能证明执行回执，不能证明状态变化。
        [Fact]
        public void FallsBackToStableOrderWhenAllAlreadyStopped()
        {
            var snap = Snap(
                Rec(1, 0, "Infantry", 40, false, true, 100.0, "StandYourGround"),
                Rec(1, 1, "Infantry", 40, false, true, 100.0, "StandYourGround"));
            var r = TargetSelector.Select(snap, 100.0);
            Assert.True(r.HasChoice());
            Assert.Equal("1/0", r.Chosen.Key());
        }

        // 无快照 / 空快照不抛异常。
        [Fact]
        public void HandlesMissingSnapshot()
        {
            var r = TargetSelector.Select(null, 100.0);
            Assert.False(r.HasChoice());
            Assert.Contains("no_snapshot", r.Rejections);

            var r2 = TargetSelector.Select(Snap(), 100.0);
            Assert.False(r2.HasChoice());
            Assert.Contains("snapshot_empty", r2.Rejections);
        }
    }
}
