using System;
using System.Collections.Generic;
using CombatBrain2.Core;
using Xunit;

namespace CombatBrain2.Tests
{
    // P3 结果评价：冻结基线、终态账本、公式边界、四类决定与转换继承。
    public class ActionFeedbackTests
    {
        private const double H = 12.0;

        // ---------------- 构造助手 ----------------

        private static FormationRecord Rec(int team, int slot, string cls, int units,
                                           double x, double y, string obj, int firstIndex)
        {
            var r = new FormationRecord
            {
                MissionKey = "m#1",
                SampledAt = 100.0,
                TeamIndex = team,
                FormationIndex = slot,
                FormationClass = cls,
                UnitCount = units,
                PosX = x,
                PosY = y,
                ObjectIdentity = obj,
                IsAiControlled = true,
                MovementOrder = "Stop",
                ActiveMemberCount = units,
                ActiveCenterX = x,
                ActiveCenterY = y,
                HasActiveCenter = true,
                MemberDigest = "digest-" + firstIndex,
            };
            for (int i = 0; i < units; i++)
            {
                r.ActiveMembers.Add(new MemberIdentity
                {
                    Index = firstIndex + i,
                    RefHash = 0x10000 + firstIndex + i,
                });
            }
            return r;
        }

        private static FormationSnapshot Snap(params FormationRecord[] records)
        {
            var s = new FormationSnapshot { MissionKey = "m#1", SampledAt = 100.0 };
            for (int i = 0; i < records.Length; i++) s.Records.Add(records[i]);
            return s;
        }

        // 敌 40 人步兵 + 本 40 人步兵，用于精确构出 LE = 0.15 / LF = 0.05 的边界。
        private static FormationSnapshot SnapOf(int enemyUnits, int myUnits,
                                                out FormationRecord enemy, out FormationRecord mine)
        {
            enemy = Rec(1, 0, "Infantry", enemyUnits, 100.0, 0.0, "e0@Infantry", 1000);
            mine = Rec(0, 0, "Infantry", myUnits, 0.0, 0.0, "m0@Infantry", 2000);
            return Snap(enemy, mine);
        }

        private static MemberIdentity IdOf(FormationRecord r, int nth)
        {
            return r.ActiveMembers[nth];
        }

        private static RoleAssignment Role(string key, string obj, int baseline, FormationRole role)
        {
            return new RoleAssignment
            {
                Role = role,
                FormationKey = key,
                ObjectIdentity = obj,
                BaselineUnits = baseline,
            };
        }

        private static List<RoleAssignment> Roles(params RoleAssignment[] roles)
        {
            return new List<RoleAssignment>(roles);
        }

        // 冻结：敌方 30 人步兵在 (100,0)，本方 20 人步兵在 (0,0)。t0 = 50.0。
        private static ActionFeedbackTracker Frozen(out FormationSnapshot snap,
                                                    int enemyUnits = 30, int myUnits = 20)
        {
            var enemy = Rec(1, 0, "Infantry", enemyUnits, 100.0, 0.0, "e0@Infantry", 1000);
            var mine = Rec(0, 0, "Infantry", myUnits, 0.0, 0.0, "m0@Infantry", 2000);
            snap = Snap(enemy, mine);
            var t = new ActionFeedbackTracker();
            string detail;
            bool ok = t.Freeze(snap, 0, "0/0", "plan-1", "input-1", 50.0, out detail);
            Assert.True(ok, detail);
            return t;
        }

        private static ActionFeedbackInput Input(double now)
        {
            return new ActionFeedbackInput
            {
                Now = now,
                IntegrityOk = true,
                TargetObjectPresent = true,
                TargetActiveE0Members = 30,
                RegroupedAway = false,
                MyForceContinueLineHolds = true,
                RangedAdvantageStillHolds = true,
                PlanStillValid = true,
                RiskCenterKnown = true,
                EnemyWithinRiskRadius = 10,
                MyRoleUnits = 20,
                RolesAboveContinueLine = true,
            };
        }

        // 杀掉 E0 前 n 人、F0 前 k 人。
        private static void Kill(ActionFeedbackTracker t, FormationSnapshot s, int nE0, int nF0, double at)
        {
            var enemy = s.Records[0];
            var mine = s.Records[1];
            for (int i = 0; i < nE0; i++)
            {
                Assert.True(t.NoteTerminal("m#1", IdOf(enemy, i), MemberEndState.Killed, at));
            }
            for (int i = 0; i < nF0; i++)
            {
                Assert.True(t.NoteTerminal("m#1", IdOf(mine, i), MemberEndState.Unconscious, at));
            }
        }

        // ---------------- 基线冻结 ----------------

        [Fact]
        public void FreezeRequiresTwentyActiveEnemyInfantry()
        {
            var small = Rec(1, 0, "Infantry", 19, 100.0, 0.0, "e0@Infantry", 1000);
            var mine = Rec(0, 0, "Infantry", 20, 0.0, 0.0, "m0@Infantry", 2000);
            var t = new ActionFeedbackTracker();
            string detail;
            Assert.False(t.Freeze(Snap(small, mine), 0, "0/0", "plan-1", "input-1", 50.0, out detail));
            Assert.Equal("enemy_infantry_below_20", detail);
            Assert.False(t.Frozen);
        }

        [Fact]
        public void FreezePicksNearestEnemyInfantryAndCollectsWholeSide()
        {
            var near = Rec(1, 0, "Infantry", 22, 60.0, 0.0, "near@Infantry", 1000);
            var far = Rec(1, 1, "Infantry", 25, 200.0, 0.0, "far@Infantry", 3000);
            var ranged = Rec(1, 2, "Ranged", 30, 50.0, 0.0, "er@Ranged", 5000);
            var mineA = Rec(0, 0, "Infantry", 12, 0.0, 0.0, "mA@Infantry", 2000);
            var mineB = Rec(0, 1, "Ranged", 8, 5.0, 0.0, "mB@Ranged", 4000);
            var t = new ActionFeedbackTracker();
            string detail;
            Assert.True(t.Freeze(Snap(near, far, ranged, mineA, mineB), 0, "0/0", "plan-1", "input-1",
                50.0, out detail), detail);

            Assert.Equal(22, t.NE);
            Assert.Equal(20, t.NF);
            Assert.Equal("near@Infantry", t.ObserveObjectIdentity);
            Assert.Equal(50.0, t.T0, 3);
        }

        // ---------------- 公式与未知 ----------------

        [Fact]
        public void EmptyDenominatorIsUnknownNotZeroCost()
        {
            var t = new ActionFeedbackTracker();
            var enemy = Rec(1, 0, "Infantry", 30, 100.0, 0.0, "e0@Infantry", 1000);
            var mine = Rec(0, 0, "Infantry", 20, 0.0, 0.0, "m0@Infantry", 2000);
            string detail;
            Assert.True(t.Freeze(Snap(enemy, mine), 0, "0/0", "plan-1", "input-1", 50.0, out detail));
            t.NF = 0;

            var ev = t.Evaluate(Input(70.0));
            Assert.False(ev.Complete);
            Assert.Equal("empty_denominator", ev.IncompleteReason);
            Assert.Equal(ActionFeedbackDecision.Continue, ev.Decision);
            Assert.False(ev.CostKnown);
            Assert.False(ev.RateKnown);
        }

        [Fact]
        public void RateUnknownBeforeTwelveSecondWindow()
        {
            var t = Frozen(out _);
            var ev = t.Evaluate(Input(58.0));
            Assert.False(ev.RateKnown);
            Assert.False(ev.CostKnown);
            Assert.True(double.IsNaN(ev.Rf));
            Assert.False(double.IsNaN(ev.LF));
        }

        [Fact]
        public void RateUsesNearestSampleAtLeastTwelveSecondsBack()
        {
            var snap2 = Frozen(out var snap);
            Kill(snap2, snap, 3, 0, 55.0);
            Assert.Equal(0.10, snap2.Evaluate(Input(62.0)).LE, 6);
            // 62 有效样本入账；74 与 62 相差 12 s，窗口成立。
            var ev = snap2.Evaluate(Input(74.0));
            Assert.True(ev.RateKnown);
            Assert.Equal(12.0, ev.RateWindow, 3);
            Assert.Equal(0.0, ev.Rf, 9);
        }

        [Fact]
        public void PositiveGateRequiresTwoConsecutivePointsAndTwelveSeconds()
        {
            var t = Frozen(out var snap);
            Kill(t, snap, 5, 0, 60.0); // LE = 5/30 = 0.1667 >= 0.15

            var first = t.Evaluate(Input(63.0));
            Assert.Equal(ActionFeedbackDecision.Continue, first.Decision);
            Assert.Equal(1, first.ConsecutiveCount);

            var second = t.Evaluate(Input(66.0));
            Assert.Equal(ActionFeedbackDecision.Reinforce, second.Decision);
            Assert.Equal(2, second.ConsecutiveCount);
            Assert.True(second.StableSeconds >= TestParameters.PlanMinStableSeconds);
            Assert.False(second.ExceptionUsed);
        }

        [Fact]
        public void PositiveGateRejectsBeforeTwelveSecondsFromT0()
        {
            var t = Frozen(out var snap);
            Kill(t, snap, 5, 0, 55.0);
            t.Evaluate(Input(57.0));
            var ev = t.Evaluate(Input(60.0));
            Assert.Equal(ActionFeedbackDecision.Continue, ev.Decision);
            Assert.Contains("window", ev.Reason);
        }

        [Fact]
        public void PositiveGateBoundaryValuesAreInclusive()
        {
            // LE = 6/40 = 0.15，LF = 2/40 = 0.05，U = 0.5*0.75 + 0.5*0.5 = 0.625 >= 0.60
            FormationRecord enemy, mine;
            var snap = SnapOf(40, 40, out enemy, out mine);
            var t = new ActionFeedbackTracker();
            string detail;
            Assert.True(t.Freeze(snap, 0, "0/0", "plan-1", "input-1", 50.0, out detail), detail);
            for (int i = 0; i < 6; i++) t.NoteTerminal("m#1", IdOf(enemy, i), MemberEndState.Killed, 60.0);
            for (int i = 0; i < 2; i++) t.NoteTerminal("m#1", IdOf(mine, i), MemberEndState.Killed, 60.0);

            var first = t.Evaluate(Input(63.0));
            Assert.Equal(0.15, first.LE, 9);
            Assert.Equal(0.05, first.LF, 9);
            Assert.True(first.U >= TestParameters.P3ReinforceU, first.U.ToString());
            Assert.Equal(1, first.ConsecutiveCount);

            var second = t.Evaluate(Input(66.0));
            Assert.Equal(ActionFeedbackDecision.Reinforce, second.Decision);
        }

        [Fact]
        public void StopLossTriggersOnCumulativeLossWithoutRateWindow()
        {
            var t = Frozen(out var snap, 30, 100);
            var mine = snap.Records[1];
            for (int i = 0; i < 10; i++) t.NoteTerminal("m#1", IdOf(mine, i), MemberEndState.Killed, 55.0);

            var ev = t.Evaluate(Input(55.5));
            Assert.False(ev.RateKnown);
            Assert.Equal(0.10, ev.LF, 9);
            Assert.Equal(ActionFeedbackDecision.StopLoss, ev.Decision);
            Assert.True(ev.ExceptionUsed);
            Assert.True(ev.StableSeconds < TestParameters.PlanMinStableSeconds);
            Assert.True(t.Stopped);
        }

        [Fact]
        public void RateTermIsSubsumedByCumulativeLoss()
        {
            // 速率窗口不小于 H 时 H*rf <= LF 恒成立：H*rf >= 0.10 必然蕴含 LF >= 0.10。
            // 这是冻结公式的性质，记录为实机核对项，不据此改动公式。
            var t = Frozen(out var snap, 30, 100);
            var mine = snap.Records[1];
            for (int i = 0; i < 3; i++) t.NoteTerminal("m#1", IdOf(mine, i), MemberEndState.Killed, 55.0);
            Assert.True(t.Evaluate(Input(62.0)).RateKnown);
            for (int i = 3; i < 7; i++) t.NoteTerminal("m#1", IdOf(mine, i), MemberEndState.Killed, 70.0);

            var ev = t.Evaluate(Input(74.0));
            Assert.True(ev.RateKnown);
            Assert.True(ev.HRf <= ev.LF + 1e-9, ev.HRf + " > " + ev.LF);
            Assert.Equal(Math.Max(ev.LF, ev.HRf), ev.CostTerm, 9);
        }

        [Fact]
        public void HighLossPrecedesPositiveReinforcement()
        {
            var t = Frozen(out var snap, 30, 20);
            Kill(t, snap, 6, 2, 60.0); // LE 高、LF = 0.10 同时成立
            t.Evaluate(Input(63.0));
            var ev = t.Evaluate(Input(66.0));
            Assert.Equal(ActionFeedbackDecision.StopLoss, ev.Decision);
        }

        [Fact]
        public void NoGainWithCostNeedsTwoPointsAndTwentyFourSeconds()
        {
            var t = Frozen(out var snap);
            Kill(t, snap, 1, 1, 60.0); // LE = 0.033 < 0.05, LF = 0.05
            t.Evaluate(Input(70.0));
            var early = t.Evaluate(Input(73.0));
            Assert.Equal(ActionFeedbackDecision.Continue, early.Decision);
            Assert.Contains("window", early.Reason);

            t.Evaluate(Input(74.0));
            var ev = t.Evaluate(Input(77.0));
            Assert.Equal(ActionFeedbackDecision.Disengage, ev.Decision);
            Assert.True(ev.U < TestParameters.P3DisengageU);
        }

        [Fact]
        public void UnknownPointInterruptsConsecutiveStreak()
        {
            var t = Frozen(out var snap);
            Kill(t, snap, 5, 0, 60.0);
            Assert.Equal(1, t.Evaluate(Input(63.0)).ConsecutiveCount);

            var bad = Input(66.0);
            bad.IntegrityOk = false;
            bad.IntegrityNote = "terminal_lost";
            var interrupted = t.Evaluate(bad);
            Assert.False(interrupted.Complete);
            Assert.Equal(0, interrupted.ConsecutiveCount);

            var again = t.Evaluate(Input(69.0));
            Assert.Equal(1, again.ConsecutiveCount);
            Assert.Equal(ActionFeedbackDecision.Continue, again.Decision);
        }

        [Fact]
        public void RegroupedTargetBlocksPositive()
        {
            var t = Frozen(out var snap);
            Kill(t, snap, 5, 0, 60.0);
            t.Evaluate(Input(63.0));
            var input = Input(66.0);
            input.RegroupedAway = true;
            var ev = t.Evaluate(input);
            Assert.Equal(ActionFeedbackDecision.Continue, ev.Decision);
            Assert.Contains("local_value", ev.Reason);
        }

        [Fact]
        public void RiskGateEqualIsAllowedAndMoreEnemyBlocks()
        {
            var myRoles = Roles(Role("0/0", "m0@Infantry", 20, FormationRole.Main),
                                Role("0/1", "m1@Infantry", 20, FormationRole.Guard),
                                Role("0/2", "m2@Infantry", 20, FormationRole.Support));
            var snap = Snap(Rec(0, 0, "Infantry", 20, 0.0, 0.0, "m0@Infantry", 2000),
                            Rec(0, 1, "Infantry", 20, 2.0, 0.0, "m1@Infantry", 3000),
                            Rec(0, 2, "Infantry", 20, 4.0, 0.0, "m2@Infantry", 4000),
                            Rec(1, 0, "Infantry", 20, 30.0, 0.0, "e0@Infantry", 1000));
            var equal = ActionFeedback.RiskGate(snap, 0, myRoles, 30.0, 0.0);
            Assert.True(equal.CenterKnown);
            Assert.True(equal.Allowed);
            Assert.Equal(20, equal.EnemyWithinRadius);
            Assert.Equal(60, equal.MyRoleUnits);

            var snap2 = Snap(Rec(0, 0, "Infantry", 20, 0.0, 0.0, "m0@Infantry", 2000),
                             Rec(0, 1, "Infantry", 20, 2.0, 0.0, "m1@Infantry", 3000),
                             Rec(0, 2, "Infantry", 20, 4.0, 0.0, "m2@Infantry", 4000),
                             Rec(1, 0, "Infantry", 61, 30.0, 0.0, "e0@Infantry", 1000));
            var blocked = ActionFeedback.RiskGate(snap2, 0, myRoles, 30.0, 0.0);
            Assert.False(blocked.Allowed);
            Assert.Equal("enemy_more_than_roles", blocked.Detail);
        }

        [Fact]
        public void RiskGateIgnoresEnemyOutsideThirtyMeters()
        {
            var myRoles = Roles(Role("0/0", "m0@Infantry", 20, FormationRole.Main),
                                Role("0/1", "m1@Infantry", 20, FormationRole.Guard),
                                Role("0/2", "m2@Infantry", 20, FormationRole.Support));
            var far = Rec(1, 0, "Infantry", 40, 200.0, 0.0, "e0@Infantry", 1000);
            var near = Rec(1, 1, "Infantry", 5, 30.0, 0.0, "e1@Infantry", 3000);
            var snap = Snap(Rec(0, 0, "Infantry", 20, 0.0, 0.0, "m0@Infantry", 2000),
                            Rec(0, 1, "Infantry", 20, 2.0, 0.0, "m1@Infantry", 3000),
                            Rec(0, 2, "Infantry", 20, 4.0, 0.0, "m2@Infantry", 4000),
                            far, near);
            var gate = ActionFeedback.RiskGate(snap, 0, myRoles, 30.0, 0.0);
            Assert.Equal(5, gate.EnemyWithinRadius);
            Assert.True(gate.Allowed);
        }

        [Fact]
        public void RiskGateBlocksWhenRoleBelowContinueLine()
        {
            var myRoles = Roles(Role("0/0", "m0@Infantry", 20, FormationRole.Main),
                                Role("0/1", "m1@Ranged", 20, FormationRole.Guard),
                                Role("0/2", "m2@Infantry", 20, FormationRole.Support));
            var snap = Snap(Rec(0, 0, "Infantry", 20, 0.0, 0.0, "m0@Infantry", 2000),
                            Rec(0, 1, "Ranged", 4, 0.0, 0.0, "m1@Ranged", 4000),
                            Rec(0, 2, "Infantry", 20, 4.0, 0.0, "m2@Infantry", 6000),
                            Rec(1, 0, "Infantry", 3, 30.0, 0.0, "e0@Infantry", 1000));
            var gate = ActionFeedback.RiskGate(snap, 0, myRoles, 30.0, 0.0);
            Assert.False(gate.Allowed);
            Assert.Equal("role_below_continue_line", gate.Detail);
        }

        [Fact]
        public void RiskGateUnknownCenterBlocksAndExplains()
        {
            var snap = Snap(Rec(0, 0, "Infantry", 20, 0.0, 0.0, "m0@Infantry", 2000),
                            Rec(0, 1, "Infantry", 20, 2.0, 0.0, "m1@Infantry", 3000),
                            Rec(0, 2, "Infantry", 20, 4.0, 0.0, "m2@Infantry", 4000));
            var gate = ActionFeedback.RiskGate(snap, 0,
                Roles(Role("0/0", "m0@Infantry", 20, FormationRole.Main),
                      Role("0/1", "m1@Infantry", 20, FormationRole.Guard),
                      Role("0/2", "m2@Infantry", 20, FormationRole.Support)),
                double.NaN, double.NaN);
            Assert.False(gate.CenterKnown);
            Assert.False(gate.Allowed);
            Assert.Equal("center_unknown", gate.Detail);
        }

        // ---------------- 终态与身份 ----------------

        [Fact]
        public void TerminalCountsOncePerIdentity()
        {
            var t = Frozen(out var snap);
            var enemy = snap.Records[0];
            Assert.True(t.NoteTerminal("m#1", IdOf(enemy, 0), MemberEndState.Killed, 60.0));
            Assert.False(t.NoteTerminal("m#1", IdOf(enemy, 0), MemberEndState.Killed, 61.0));
            var ev = t.Evaluate(Input(75.0));
            Assert.Equal(1, ev.DE);
        }

        [Fact]
        public void ReusedIndexWithDifferentObjectIsNotCasualty()
        {
            var t = Frozen(out var snap);
            var enemy = snap.Records[0];
            var reused = new MemberIdentity { Index = enemy.ActiveMembers[0].Index, RefHash = 0xABC };
            Assert.False(t.NoteTerminal("m#1", reused, MemberEndState.Killed, 60.0));
            var ev = t.Evaluate(Input(75.0));
            Assert.Equal(0, ev.DE);
        }

        [Fact]
        public void DeletedAndUnknownDoNotPoseAsKilled()
        {
            var t = Frozen(out var snap);
            var enemy = snap.Records[0];
            Assert.True(t.NoteTerminal("m#1", IdOf(enemy, 0), MemberEndState.Deleted, 60.0));
            Assert.True(t.NoteTerminal("m#1", IdOf(enemy, 1), MemberEndState.Unknown, 60.0));
            var ev = t.Evaluate(Input(75.0));
            Assert.Equal(0, ev.DE);
            Assert.Equal(2, ev.UnknownCount);
        }

        [Fact]
        public void RoutedIsSeparatedFromCasualties()
        {
            var t = Frozen(out var snap);
            var mine = snap.Records[1];
            Assert.True(t.NoteTerminal("m#1", IdOf(mine, 0), MemberEndState.Routed, 60.0));
            var ev = t.Evaluate(Input(75.0));
            Assert.Equal(0, ev.DF);
            Assert.Equal(1, ev.RoutedCount);
        }

        [Fact]
        public void ForeignMissionTerminalIsNotAttributed()
        {
            var t = Frozen(out var snap);
            var enemy = snap.Records[0];
            Assert.False(t.NoteTerminal("other#9", IdOf(enemy, 0), MemberEndState.Killed, 60.0));
            Assert.Equal(0, t.Evaluate(Input(75.0)).DE);
        }

        // ---------------- 追加一次、继承与不重建 ----------------

        [Fact]
        public void ReinforceHappensOncePerFrozenAction()
        {
            var t = Frozen(out var snap);
            Kill(t, snap, 6, 0, 55.0);
            t.Evaluate(Input(63.0));
            Assert.Equal(ActionFeedbackDecision.Reinforce, t.Evaluate(Input(66.0)).Decision);
            Assert.True(t.Reinforced);

            t.Evaluate(Input(69.0));
            var ev = t.Evaluate(Input(72.0));
            Assert.NotEqual(ActionFeedbackDecision.Reinforce, ev.Decision);
            Assert.Contains("already_reinforced", ev.Reason);
        }

        [Fact]
        public void SuccessorInheritsFrozenSetsAndCumulativeLoss()
        {
            var t = Frozen(out var snap);
            Kill(t, snap, 2, 1, 55.0);
            var ev = t.Evaluate(Input(63.0));

            var heir = t.CloneForSuccessor("plan-2");
            Assert.Equal(t.T0, heir.T0, 3);
            Assert.Equal(t.NE, heir.NE);
            Assert.Equal(t.NF, heir.NF);
            Assert.Equal(ev.DE, heir.Evaluate(Input(64.0)).DE);
            Assert.Equal(ev.DF, heir.Evaluate(Input(64.0)).DF);
            Assert.Equal("plan-2", heir.PlanId);
        }

        [Fact]
        public void StoppedActionBlocksOffensiveReplanAndKeepLoss()
        {
            var t = Frozen(out var snap);
            Kill(t, snap, 0, 3, 60.0); // LF = 0.15
            Assert.Equal(ActionFeedbackDecision.StopLoss, t.Evaluate(Input(61.0)).Decision);
            Assert.True(t.OffensiveBlocked);

            var heir = t.CloneForSuccessor("plan-2");
            Assert.True(heir.OffensiveBlocked);
            Kill(t, snap, 6, 0, 70.0);
            var ev = heir.Evaluate(Input(80.0));
            Assert.NotEqual(ActionFeedbackDecision.Reinforce, ev.Decision);
            Assert.True(ev.DF >= 3);
        }

        [Fact]
        public void ProjectedMainTargetUsesQuarterDistanceCappedAtTwentyFive()
        {
            double tx, ty;
            Assert.True(ActionFeedback.ProjectedMainTarget(0.0, 0.0, 40.0, 0.0, out tx, out ty));
            Assert.Equal(10.0, tx, 3);
            Assert.Equal(0.0, ty, 3);

            Assert.True(ActionFeedback.ProjectedMainTarget(0.0, 0.0, 400.0, 0.0, out tx, out ty));
            Assert.Equal(25.0, tx, 3);
        }

        // ---------------- 合同偏差反例（2026-10-14 复核） ----------------

        // "非坐骑"指排除马匹；编队单位集合本身不含马匹（Agent.Formation 对坐骑为 null），
        // 因此骑兵骑手必须计入己方分母与敌方风险人数，不能按 Cavalry 类名剔除。
        [Fact]
        public void CavalryRidersCountInFriendlyDenominatorAndRiskRadius()
        {
            var enemy = Rec(1, 0, "Infantry", 30, 100.0, 0.0, "e0@Infantry", 1000);
            var mineInf = Rec(0, 0, "Infantry", 12, 0.0, 0.0, "m0@Infantry", 2000);
            var mineCav = Rec(0, 1, "Cavalry", 8, 5.0, 0.0, "m1@Cavalry", 4000);
            var mineSup = Rec(0, 2, "Infantry", 12, 8.0, 0.0, "m2@Infantry", 6000);
            var snap = Snap(enemy, mineInf, mineCav, mineSup);
            var freezeSnap = Snap(enemy, mineInf, mineCav);

            var t = new ActionFeedbackTracker();
            string detail;
            Assert.True(t.Freeze(freezeSnap, 0, "0/0", "plan-1", "input-1", 50.0, out detail), detail);
            Assert.Equal(20, t.NF);

            var myRoles = Roles(Role("0/0", "m0@Infantry", 12, FormationRole.Main),
                                Role("0/1", "m1@Cavalry", 8, FormationRole.Guard),
                                Role("0/2", "m2@Infantry", 12, FormationRole.Support));
            var gate = ActionFeedback.RiskGate(snap, 0, myRoles, 30.0, 0.0);
            Assert.Equal(0, gate.EnemyWithinRadius);
            Assert.True(gate.Allowed);

            var enemyCav = Rec(1, 1, "Cavalry", 30, 30.0, 0.0, "e1@Cavalry", 5000);
            var gate2 = ActionFeedback.RiskGate(
                Snap(enemy, mineInf, mineCav, mineSup, enemyCav),
                0, myRoles, 30.0, 0.0);
            Assert.Equal(30, gate2.EnemyWithinRadius);
            Assert.True(gate2.Allowed);
            Assert.Equal("allowed", gate2.Detail);
        }

        // 完整性门先于决定表：存在无法解释的基线成员终态时，本窗口不可判正负，
        // 即使收益与代价数值正好满足正反馈边界也不得追加。
        [Fact]
        public void UnknownTerminalMakesWindowUnjudgeableAndBlocksReinforce()
        {
            FormationRecord enemy, mine;
            var snap = SnapOf(40, 40, out enemy, out mine);
            var t = new ActionFeedbackTracker();
            string detail;
            Assert.True(t.Freeze(snap, 0, "0/0", "plan-1", "input-1", 50.0, out detail), detail);
            for (int i = 0; i < 6; i++) t.NoteTerminal("m#1", IdOf(enemy, i), MemberEndState.Killed, 60.0);
            Assert.True(t.NoteTerminal("m#1", IdOf(mine, 5), MemberEndState.Deleted, 60.0));

            var first = t.Evaluate(Input(63.0));
            Assert.False(first.Complete);
            Assert.Contains("unknown_terminal", first.IncompleteReason);
            Assert.Equal(ActionFeedbackDecision.Continue, first.Decision);
            Assert.Equal(0, first.ConsecutiveCount);
            Assert.False(first.RateKnown);

            var second = t.Evaluate(Input(66.0));
            Assert.False(second.Complete);
            Assert.NotEqual(ActionFeedbackDecision.Reinforce, second.Decision);
        }

        // t0 前发生的损失不算行动结果，包括 t0 后才送达的迟到终态。
        [Fact]
        public void TerminalBeforeT0IsRejectedAsPreActionLoss()
        {
            var t = Frozen(out var snap);
            var enemy = snap.Records[0];
            var mine = snap.Records[1];

            Assert.False(t.NoteTerminal("m#1", IdOf(enemy, 0), MemberEndState.Killed, 49.9));
            Assert.False(t.NoteTerminal("m#1", IdOf(mine, 0), MemberEndState.Killed, 12.0));
            Assert.False(t.NoteTerminal("m#1", IdOf(enemy, 1), MemberEndState.Killed, double.NaN));

            var ev = t.Evaluate(Input(63.0));
            Assert.Equal(0, ev.DE);
            Assert.Equal(0, ev.DF);

            Assert.True(t.NoteTerminal("m#1", IdOf(enemy, 2), MemberEndState.Killed, 55.0));
            Assert.Equal(1, t.Evaluate(Input(66.0)).DE);
        }

        // 敌方编队位置未知时无法判定它是否落入半径，低估风险等于放行；按合同不追加。
        [Fact]
        public void RiskGateBlocksWhenEnemyCenterUnknown()
        {
            var noCenter = Rec(1, 0, "Infantry", 25, 0.0, 0.0, "e0@Infantry", 1000);
            noCenter.HasActiveCenter = false;
            var snap = Snap(Rec(0, 0, "Infantry", 20, 0.0, 0.0, "m0@Infantry", 2000),
                            Rec(0, 1, "Infantry", 20, 2.0, 0.0, "m1@Infantry", 3000),
                            Rec(0, 2, "Infantry", 20, 4.0, 0.0, "m2@Infantry", 4000),
                            noCenter);

            var gate = ActionFeedback.RiskGate(snap, 0,
                Roles(Role("0/0", "m0@Infantry", 20, FormationRole.Main),
                      Role("0/1", "m1@Infantry", 20, FormationRole.Guard),
                      Role("0/2", "m2@Infantry", 20, FormationRole.Support)),
                30.0, 0.0);
            Assert.False(gate.Allowed);
            Assert.Equal("enemy_center_unknown", gate.Detail);
        }

        // ---------------- 整改2：风险门只统计当前三职责的不同对象 ----------------

        // 实际己方三对象各 10 人、敌 35 人。复算端若把重复的 Main 键当成两个职责，
        // 会得到 40 人而放行；去重后 30 人必须正确拒绝。
        [Fact]
        public void RiskGateCountsEachRoleObjectOnceEvenWhenKeysRepeat()
        {
            var roles = new List<RoleAssignment>
            {
                new RoleAssignment { Role = FormationRole.Main, FormationKey = "0/0",
                                     ObjectIdentity = "m0@Infantry", BaselineUnits = 10 },
                new RoleAssignment { Role = FormationRole.Guard, FormationKey = "0/1",
                                     ObjectIdentity = "m1@Infantry", BaselineUnits = 10 },
                new RoleAssignment { Role = FormationRole.Support, FormationKey = "0/2",
                                     ObjectIdentity = "m2@Ranged", BaselineUnits = 10 },
            };
            var snap = Snap(Rec(0, 0, "Infantry", 10, 0.0, 0.0, "m0@Infantry", 2000),
                            Rec(0, 1, "Infantry", 10, 2.0, 0.0, "m1@Infantry", 3000),
                            Rec(0, 2, "Ranged", 10, 4.0, 0.0, "m2@Ranged", 4000),
                            Rec(1, 0, "Infantry", 35, 30.0, 0.0, "e0@Infantry", 1000));

            var gate = ActionFeedback.RiskGate(snap, 0, roles, 30.0, 0.0);
            Assert.True(gate.CenterKnown);
            Assert.Equal(30, gate.MyRoleUnits);
            Assert.Equal(35, gate.EnemyWithinRadius);
            Assert.False(gate.Allowed);
            Assert.Equal("enemy_more_than_roles", gate.Detail);
        }

        // 三职责指向同一对象时不得计三次：同一对象只计一次。
        [Fact]
        public void RiskGateRejectsRolesThatShareOneObject()
        {
            var roles = new List<RoleAssignment>
            {
                new RoleAssignment { Role = FormationRole.Main, FormationKey = "0/0",
                                     ObjectIdentity = "m0@Infantry", BaselineUnits = 10 },
                new RoleAssignment { Role = FormationRole.Guard, FormationKey = "0/0",
                                     ObjectIdentity = "m0@Infantry", BaselineUnits = 10 },
                new RoleAssignment { Role = FormationRole.Support, FormationKey = "0/0",
                                     ObjectIdentity = "m0@Infantry", BaselineUnits = 10 },
            };
            var snap = Snap(Rec(0, 0, "Infantry", 10, 0.0, 0.0, "m0@Infantry", 2000),
                            Rec(1, 0, "Infantry", 11, 30.0, 0.0, "e0@Infantry", 1000));

            var gate = ActionFeedback.RiskGate(snap, 0, roles, 30.0, 0.0);
            Assert.False(gate.Allowed);
            Assert.Equal("roles_not_distinct", gate.Detail);
        }

        // 三职责缺一个（保护对象已被摘除）时映射不完整，不得放行。
        [Fact]
        public void RiskGateRejectsIncompleteRoleMapping()
        {
            var roles = new List<RoleAssignment>
            {
                new RoleAssignment { Role = FormationRole.Main, FormationKey = "0/0",
                                     ObjectIdentity = "m0@Infantry", BaselineUnits = 10 },
                new RoleAssignment { Role = FormationRole.Guard, FormationKey = "0/1",
                                     ObjectIdentity = "m1@Infantry", BaselineUnits = 10 },
            };
            var snap = Snap(Rec(0, 0, "Infantry", 10, 0.0, 0.0, "m0@Infantry", 2000),
                            Rec(0, 1, "Infantry", 10, 2.0, 0.0, "m1@Infantry", 3000),
                            Rec(1, 0, "Infantry", 3, 30.0, 0.0, "e0@Infantry", 1000));

            var gate = ActionFeedback.RiskGate(snap, 0, roles, 30.0, 0.0);
            Assert.False(gate.Allowed);
            Assert.Equal("roles_incomplete", gate.Detail);
        }

        // 任务记录的键与当前快照的对象身份不一致（对象被替换/转编）时映射过期，不得放行。
        [Fact]
        public void RiskGateRejectsStaleRoleIdentity()
        {
            var roles = new List<RoleAssignment>
            {
                new RoleAssignment { Role = FormationRole.Main, FormationKey = "0/0",
                                     ObjectIdentity = "m0@Infantry", BaselineUnits = 10 },
                new RoleAssignment { Role = FormationRole.Guard, FormationKey = "0/1",
                                     ObjectIdentity = "m1@Infantry", BaselineUnits = 10 },
                new RoleAssignment { Role = FormationRole.Support, FormationKey = "0/2",
                                     ObjectIdentity = "m2@Ranged", BaselineUnits = 10 },
            };
            var snap = Snap(Rec(0, 0, "Infantry", 10, 0.0, 0.0, "m0@Infantry", 2000),
                            Rec(0, 1, "Infantry", 10, 2.0, 0.0, "replaced@Infantry", 3000),
                            Rec(0, 2, "Ranged", 10, 4.0, 0.0, "m2@Ranged", 4000),
                            Rec(1, 0, "Infantry", 3, 30.0, 0.0, "e0@Infantry", 1000));

            var gate = ActionFeedback.RiskGate(snap, 0, roles, 30.0, 0.0);
            Assert.False(gate.Allowed);
            Assert.StartsWith("role_identity_stale", gate.Detail);
        }

        // ---------------- 整改3：冻结成员逐个可解释 ----------------

        // F0 = 100、当前 99 且缺失者没有任何终态：不能补为 0 损失，
        // 该窗口不可判正负，即使敌损足够也不得追加。
        [Fact]
        public void MissingFrozenMemberWithoutTerminalIsUnknownAndBlocksReinforce()
        {
            var enemy = Rec(1, 0, "Infantry", 20, 100.0, 0.0, "e0@Infantry", 1000);
            var mine = Rec(0, 0, "Infantry", 100, 0.0, 0.0, "m0@Infantry", 2000);
            var snap = Snap(enemy, mine);
            var t = new ActionFeedbackTracker();
            string detail;
            Assert.True(t.Freeze(snap, 0, "0/0", "plan-1", "input-1", 50.0, out detail), detail);
            Assert.Equal(100, t.NF);

            for (int i = 0; i < 3; i++) t.NoteTerminal("m#1", IdOf(enemy, i), MemberEndState.Killed, 60.0);

            // 当前快照只剩 99 人，第 99 人（index 2098）既不在当前集合里，也没有终态。
            var now = Snap(enemy, Rec(0, 0, "Infantry", 99, 0.0, 0.0, "m0@Infantry", 2000));
            t.Evaluate(Input(63.0));

            Assert.False(t.MissingCoverage("m#1", now, out string why), why);
            Assert.Contains("unexplained_member", why);
        }

        // 正常转编：缺失者出现在别的己方编队里，仍可按稳定身份定位，不计死亡。
        [Fact]
        public void RegroupedMemberStaysExplainableAndIsNotACasualty()
        {
            var enemy = Rec(1, 0, "Infantry", 20, 100.0, 0.0, "e0@Infantry", 1000);
            var mine = Rec(0, 0, "Infantry", 100, 0.0, 0.0, "m0@Infantry", 2000);
            var snap = Snap(enemy, mine);
            var t = new ActionFeedbackTracker();
            string detail;
            Assert.True(t.Freeze(snap, 0, "0/0", "plan-1", "input-1", 50.0, out detail), detail);

            for (int i = 0; i < 3; i++) t.NoteTerminal("m#1", IdOf(enemy, i), MemberEndState.Killed, 60.0);

            // 第 90..99 人转编到 0/1；0/0 只剩 90 人。
            var moved = Rec(0, 1, "Infantry", 10, 5.0, 0.0, "m1@Infantry", 2090);
            var now = Snap(enemy, Rec(0, 0, "Infantry", 90, 0.0, 0.0, "m0@Infantry", 2000), moved);

            t.Evaluate(Input(63.0));
            Assert.True(t.MissingCoverage("m#1", now, out string why), why);
            Assert.Equal(0, t.Evaluate(Input(66.0)).DF);
        }

        // 已确认 Killed 的成员随后以 Deleted 出现，不得降为未知，也不得重复计数。
        [Fact]
        public void ConfirmedTerminalIsNotDowngradedByLaterDeleted()
        {
            var t = Frozen(out var snap);
            var mine = snap.Records[1];
            Assert.True(t.NoteTerminal("m#1", IdOf(mine, 0), MemberEndState.Killed, 60.0));
            Assert.False(t.NoteTerminal("m#1", IdOf(mine, 0), MemberEndState.Deleted, 61.0));
            Assert.False(t.NoteTerminal("m#1", IdOf(mine, 0), MemberEndState.Unknown, 62.0));
            Assert.False(t.NoteTerminal("m#1", IdOf(mine, 0), MemberEndState.Killed, 63.0));

            var ev = t.Evaluate(Input(75.0));
            Assert.Equal(1, ev.DF);
            Assert.Equal(0, ev.UnknownCount);
            Assert.True(ev.Complete);
        }

        // ---------------- 整改1：旧退出确认后才接续新分配 ----------------

        // 继任等待期保留行动基线，但计划身份只在旧计划关闭之后才移交：
        // 等待期内持有的是旧计划身份，新分配还没上场。
        [Fact]
        public void StandbyTrackerKeepsOriginalPlanIdentityUntilRetargeted()
        {
            var t = Frozen(out var snap);
            Assert.Equal("plan-1", t.PlanId);

            // 等待期先持有旧计划身份，新分配只记在待命位上。
            t.InheritFeedbackStandby("plan-1");
            Assert.Equal("plan-1", t.PlanId);

            // 旧计划关闭之前不得改名为新计划身份。
            Assert.False(t.RetargetStandbyPlan("plan-1-other", "plan-2"));
            Assert.Equal("plan-1", t.PlanId);

            // 旧计划关闭之后才允许移交身份。
            Assert.True(t.RetargetStandbyPlan("plan-1", "plan-2"));
            Assert.Equal("plan-2", t.PlanId);

            // 移交后不得再次移交，身份只能前进一次。
            Assert.False(t.RetargetStandbyPlan("plan-1", "plan-3"));
            Assert.Equal("plan-2", t.PlanId);

            // t0 与 E0/F0 都保留，不因换身份重算；累计损失继续累加。
            Assert.Equal(50.0, t.T0);
            Assert.Equal(30, t.NE);
            Assert.Equal(20, t.NF);
            Kill(t, snap, 3, 1, 60.0);
            var ev = t.Evaluate(Input(63.0));
            Assert.Equal(3, ev.DE);
            Assert.Equal(1, ev.DF);
        }

        // 退出确认成立才把 Stopped 置位；未确认时停止状态不得提前成立，
        // 否则后续停止证据会引用一条从未被应用的退出。
        [Fact]
        public void StoppedIsOnlySetAfterExitConfirmation()
        {
            var t = Frozen(out _);
            t.WaitingForExitConfirmation = true;
            t.ExitStandbyCompleted = false;

            bool stopped, blocked;
            t.AfterStandbyCompleted(out stopped, out blocked);
            Assert.False(stopped);
            Assert.False(blocked);
            Assert.False(t.Stopped);
            Assert.False(t.OffensiveBlocked);
        }

        // 到退出截止仍未被应用：明确终止本次接续，如实保留停止失败，
        // 既不把自己写成已停止，也不留下可被后续重新引用的待命计划。
        [Fact]
        public void StandbyFailureTerminatesSuccessionAndKeepsStopFailed()
        {
            var t = Frozen(out _);
            t.WaitingForExitConfirmation = true;
            t.ExitStandbyCompleted = false;
            t.PendingStandbyPlanId = "plan-2";
            t.StandbyDeadlineAt = 61.0;

            t.MarkStandbyFailed();

            Assert.False(t.WaitingForExitConfirmation);
            Assert.False(t.ExitStandbyCompleted);
            Assert.False(t.Stopped);
            Assert.False(t.OffensiveBlocked);
            Assert.Equal("", t.PendingStandbyPlanId);
            Assert.Equal(-1.0, t.StandbyDeadlineAt);
        }

        // ---------------- 整改1：退出确认门的生产判定（DecideSuccessorStandby） ----------------

        // 旧计划关掉了两个对象，两条退出都在截止前真正写出：确认成立，
        // 且判定只看逐对象的实际写出时刻，不看退出任务当前是否还在开单。
        [Fact]
        public void StandbyConfirmsWhenEveryRequiredObjectExitWasWrittenInTime()
        {
            var input = Standby("61.0");
            input.RequiredObjects.Add("oA@Infantry");
            input.RequiredObjects.Add("oB@Infantry");
            input.Exits.Add(ExitWritten("oA@Infantry", 60.2));
            input.Exits.Add(ExitWritten("oB@Infantry", 60.9));

            var d = ActionFeedback.DecideSuccessorStandby(input);

            Assert.Equal(ActionFeedback.StandbyVerdict.Confirmed, d.Verdict);
            Assert.Equal("all_exits_applied", d.Detail);
        }

        // 只完成一个对象不得公布继任：缺项与未写出都必须拦住，
        // 不能因为"至少有一条退出写过了"就上位。
        [Fact]
        public void StandbyDoesNotConfirmWhenOnlyOneObjectExited()
        {
            var missing = Standby("60.5");
            missing.RequiredObjects.Add("oA@Infantry");
            missing.RequiredObjects.Add("oB@Infantry");
            missing.Exits.Add(ExitWritten("oA@Infantry", 60.2));

            var d1 = ActionFeedback.DecideSuccessorStandby(missing);
            Assert.Equal(ActionFeedback.StandbyVerdict.Waiting, d1.Verdict);

            var pending = Standby("60.5");
            pending.RequiredObjects.Add("oA@Infantry");
            pending.RequiredObjects.Add("oB@Infantry");
            pending.Exits.Add(ExitWritten("oA@Infantry", 60.2));
            pending.Exits.Add(new ActionFeedback.ExitOutcome
            {
                ObjectIdentity = "oB@Infantry",
                Written = false,
            });

            var d2 = ActionFeedback.DecideSuccessorStandby(pending);
            Assert.Equal(ActionFeedback.StandbyVerdict.Waiting, d2.Verdict);
        }

        // 截止后才写出的退出不是及时停止：过了截止就终止本次接续，
        // 不能把迟到的写入补算成成功。
        [Fact]
        public void StandbyDoesNotCreditAnExitWrittenAfterTheDeadline()
        {
            var late = Standby("62.0");
            late.RequiredObjects.Add("oA@Infantry");
            late.Exits.Add(ExitWritten("oA@Infantry", 61.4));

            var d = ActionFeedback.DecideSuccessorStandby(late);

            Assert.Equal(ActionFeedback.StandbyVerdict.TimedOut, d.Verdict);
            Assert.Equal("exit_written_after_deadline obj=oA@Infantry", d.Detail);
        }

        // 到截止仍没有任何对象写出：终止本次接续，如实保留"停止未应用"，
        // 不无限等待、不重投。
        [Fact]
        public void StandbyTimesOutAndTerminatesWhenExitsWereNotApplied()
        {
            var input = Standby("61.0");
            input.RequiredObjects.Add("oA@Infantry");
            input.Exits.Add(new ActionFeedback.ExitOutcome
            {
                ObjectIdentity = "oA@Infantry",
                Written = false,
            });

            var d = ActionFeedback.DecideSuccessorStandby(input);

            Assert.Equal(ActionFeedback.StandbyVerdict.TimedOut, d.Verdict);
            Assert.Equal("exit_not_applied_in_time", d.Detail);
        }

        // 过了退出截止就不得再停留在"等待"：即使拦阻原因不是"未写出"，
        // 而是某个必需对象根本没有退出结果，也必须明确终止本次接续，
        // 否则等待期永不结束，既无确认也无失败记录（派单 §2 整改1）。
        [Fact]
        public void StandbyTerminatesAtDeadlineWhenARequiredObjectHasNoExitFacts()
        {
            var input = Standby("62.0");
            input.RequiredObjects.Add("oA@Infantry");
            input.RequiredObjects.Add("oB@Infantry");
            input.Exits.Add(ExitWritten("oA@Infantry", 60.2));

            var d = ActionFeedback.DecideSuccessorStandby(input);

            Assert.Equal(ActionFeedback.StandbyVerdict.TimedOut, d.Verdict);
            Assert.Equal("exit_outcome_missing obj=oB@Infantry", d.Detail);
        }

        // 身份不匹配：退出结果记在别的对象上，必需对象等于没退出，不得放行。
        [Fact]
        public void StandbyRejectsExitOutcomeRecordedOnAnotherIdentity()
        {
            var input = Standby("60.5");
            input.RequiredObjects.Add("oA@Infantry");
            input.Exits.Add(ExitWritten("oX@Infantry", 60.2));

            var d = ActionFeedback.DecideSuccessorStandby(input);

            Assert.Equal(ActionFeedback.StandbyVerdict.Waiting, d.Verdict);
            Assert.Equal("exit_outcome_missing obj=oA@Infantry", d.Detail);
        }

        // 对象已被新分配接管：这条退出不再代表本次接续的停止结果，不放行。
        [Fact]
        public void StandbyRejectsOwnershipTakenByNewAssignment()
        {
            var input = Standby("60.5");
            input.RequiredObjects.Add("oA@Infantry");
            input.Exits.Add(new ActionFeedback.ExitOutcome
            {
                ObjectIdentity = "oA@Infantry",
                Written = true,
                WrittenAt = 60.2,
                OwnershipTakenByNewAssignment = true,
            });

            var d = ActionFeedback.DecideSuccessorStandby(input);

            Assert.Equal(ActionFeedback.StandbyVerdict.Waiting, d.Verdict);
            Assert.Equal("ownership_taken obj=oA@Infantry", d.Detail);
        }

        // 退出已被明确判为未应用：即使另外的对象都已写出，也不得公布继任。
        [Fact]
        public void StandbyRejectsAnExitMarkedUnapplied()
        {
            var input = Standby("60.5");
            input.RequiredObjects.Add("oA@Infantry");
            input.RequiredObjects.Add("oB@Infantry");
            input.Exits.Add(ExitWritten("oA@Infantry", 60.2));
            input.Exits.Add(new ActionFeedback.ExitOutcome
            {
                ObjectIdentity = "oB@Infantry",
                Unapplied = true,
            });

            var d = ActionFeedback.DecideSuccessorStandby(input);

            Assert.Equal(ActionFeedback.StandbyVerdict.Waiting, d.Verdict);
            Assert.Equal("exit_unapplied obj=oB@Infantry", d.Detail);
        }

        // 确认时刻的重新核对：退出都及时写出了，但继任计划在当前状态下已不成立
        // （对象缺失/职责不齐），仍然不得公布，缺必需对象就不编造伪完整计划。
        [Fact]
        public void StandbyDoesNotPublishWhenSuccessorIsNoLongerValid()
        {
            var input = Standby("60.5");
            input.RequiredObjects.Add("oA@Infantry");
            input.Exits.Add(ExitWritten("oA@Infantry", 60.2));
            input.SuccessorInvalidReason = "role_object_missing";

            var d = ActionFeedback.DecideSuccessorStandby(input);

            Assert.Equal(ActionFeedback.StandbyVerdict.Waiting, d.Verdict);
            Assert.Equal("role_object_missing", d.Detail);
        }

        // 同一对象在旧计划里有两条必需任务：必须去重，重复键不得让判定把它当两个对象。
        [Fact]
        public void StandbyRejectsDuplicatedRequiredObject()
        {
            var input = Standby("61.0");
            input.RequiredObjects.Add("oA@Infantry");
            input.RequiredObjects.Add("oA@Infantry");
            input.Exits.Add(ExitWritten("oA@Infantry", 60.2));

            var d = ActionFeedback.DecideSuccessorStandby(input);

            Assert.Equal(ActionFeedback.StandbyVerdict.Waiting, d.Verdict);
            Assert.StartsWith("required_object_duplicated", d.Detail);
        }

        // 缺输入不得默认放行。
        [Fact]
        public void StandbyRejectsIncompleteFacts()
        {
            var noRequired = Standby("61.0");
            Assert.Equal("required_objects_missing",
                         ActionFeedback.DecideSuccessorStandby(noRequired).Detail);

            var noDeadline = new ActionFeedback.SuccessorStandby
            {
                OldPlanId = "plan-1",
                PendingPlanId = "plan-2",
                Now = 61.0,
                DeadlineAt = double.NaN,
                RequiredObjects = new List<string> { "oA@Infantry" },
                Exits = new List<ActionFeedback.ExitOutcome> { ExitWritten("oA@Infantry", 60.2) },
                SuccessorPrepared = true,
            };
            Assert.Equal("deadline_unknown",
                         ActionFeedback.DecideSuccessorStandby(noDeadline).Detail);

            var noExits = Standby("61.0");
            noExits.RequiredObjects.Add("oA@Infantry");
            noExits.Exits = null;
            Assert.Equal("exit_facts_missing",
                         ActionFeedback.DecideSuccessorStandby(noExits).Detail);
        }

        private static ActionFeedback.SuccessorStandby Standby(string nowText)
        {
            double now;
            Assert.True(double.TryParse(nowText, System.Globalization.NumberStyles.Float,
                                        System.Globalization.CultureInfo.InvariantCulture, out now));
            return new ActionFeedback.SuccessorStandby
            {
                OldPlanId = "plan-1",
                PendingPlanId = "plan-2",
                Now = now,
                DeadlineAt = 61.0,
                RequiredObjects = new List<string>(),
                Exits = new List<ActionFeedback.ExitOutcome>(),
                SuccessorPrepared = true,
            };
        }

        private static ActionFeedback.ExitOutcome ExitWritten(string obj, double at)
        {
            return new ActionFeedback.ExitOutcome
            {
                ObjectIdentity = obj,
                Written = true,
                WrittenAt = at,
            };
        }
    }
}
