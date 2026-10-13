using System;
using System.Collections.Generic;
using CombatBrain2.Core;
using Xunit;

namespace CombatBrain2.Tests
{
    // P2 多编队协同。每个测试对应派单 §9.1 的一条必要反例。
    public class CoordinationTests
    {
        private static FormationRecord Rec(int team, int index, string cls, int units,
                                           double x, double y, bool playerTeam)
        {
            return new FormationRecord
            {
                MissionKey = "m1",
                SampledAt = 100.0,
                TeamIndex = team,
                Side = playerTeam ? "Player" : "Enemy",
                IsPlayerTeam = playerTeam,
                FormationIndex = index,
                FormationClass = cls,
                UnitCount = units,
                PosX = x,
                PosY = y,
                MovementOrder = "StandYourGround",
                IsAiControlled = !playerTeam,
                ObjectIdentity = "obj-" + team + "-" + index,
            };
        }

        // 我方（非玩家队）三支：步兵、远程、骑兵各一；敌方（玩家队）步兵 + 可选远程。
        private static FormationSnapshot Snapshot(int infUnits, int rngUnits, int cavUnits,
                                                 int enemyUnits, double enemyX, double enemyY,
                                                 int enemyRangedUnits)
        {
            var s = new FormationSnapshot { MissionKey = "m1", SampledAt = 100.0 };
            s.Records.Add(Rec(0, 0, "Infantry", infUnits, 0.0, 0.0, false));
            s.Records.Add(Rec(0, 1, "Ranged", rngUnits, 0.0, 20.0, false));
            s.Records.Add(Rec(0, 2, "Cavalry", cavUnits, 0.0, -20.0, false));
            s.Records.Add(Rec(1, 0, "Infantry", enemyUnits - enemyRangedUnits, enemyX, enemyY, true));
            if (enemyRangedUnits > 0)
            {
                s.Records.Add(Rec(1, 1, "Ranged", enemyRangedUnits, enemyX, enemyY + 30.0, true));
            }
            return s;
        }

        private static FormationSnapshot Snapshot(int infUnits, int rngUnits, int cavUnits,
                                                 int enemyUnits, double enemyX, double enemyY)
        {
            return Snapshot(infUnits, rngUnits, cavUnits, enemyUnits, enemyX, enemyY, 10);
        }

        private static BattleState StateOf(FormationSnapshot snapshot)
        {
            return BattleAssessment.Read(snapshot, "input-1");
        }

        // 场景 A 基础状态：我方 30 步兵 / 55 远程 / 15 骑兵，敌方 70/20/10。
        // 远程占比差 = 55/100 - 10/100 = 0.45 >= 0.10，满足 P2 首批唯一开放条件。
        private static BattleState SceneA()
        {
            return StateOf(Snapshot(30, 55, 15, 100, 0.0, 300.0));
        }

        // ---- §9.1 类别白名单：骑兵可 Hold/Reposition，骑射与未知类别一律拒绝 ----

        [Fact]
        public void CavalryAllowedForHoldAndReposition()
        {
            Assert.True(PlanBuilder.ClassAllows("Cavalry", TaskKind.Hold));
            Assert.True(PlanBuilder.ClassAllows("Cavalry", TaskKind.Reposition));
        }

        // 骑兵 Engage 不在本单控制范围：不得因为"类别可参与协同"就放开冲锋。
        [Fact]
        public void CavalryEngageRejected()
        {
            Assert.False(PlanBuilder.ClassAllows("Cavalry", TaskKind.Engage));
        }

        // IsRanged 会把 HorseArcher 当远程统计，因此不能复用 IsRanged 作 P2 白名单。
        [Fact]
        public void HorseArcherRejectedDespiteIsRanged()
        {
            Assert.True(BattleAssessment.IsRanged("HorseArcher"));
            Assert.False(PlanBuilder.ClassAllows("HorseArcher", TaskKind.Hold));
            Assert.False(PlanBuilder.ClassAllows("HorseArcher", TaskKind.Reposition));
        }

        [Fact]
        public void UnknownAndEmptyClassRejected()
        {
            Assert.False(PlanBuilder.ClassAllows("", TaskKind.Hold));
            Assert.False(PlanBuilder.ClassAllows("<unknown>", TaskKind.Hold));
            Assert.False(PlanBuilder.ClassAllows("MountedArchers", TaskKind.Reposition));
        }

        [Fact]
        public void InfantryAndRangedStillAllowed()
        {
            Assert.True(PlanBuilder.ClassAllows("Infantry", TaskKind.Reposition));
            Assert.True(PlanBuilder.ClassAllows("Ranged", TaskKind.Reposition));
        }

        // ---- §3 三职责分配：三个不同对象，同一 plan_revision ----

        [Fact]
        public void CoordinationPlanAssignsThreeDistinctFormations()
        {
            var state = SceneA();
            var plan = PlanBuilder.BuildCoordination(state, "plan-1", TestParameters.P2Now);

            Assert.NotNull(plan);
            Assert.Equal(PlanBuilder.GoalCoordination, plan.Goal);
            Assert.Equal(3, plan.Tasks.Count);

            var keys = new List<string>();
            var roles = new List<FormationRole>();
            foreach (var t in plan.Tasks)
            {
                keys.Add(t.FormationKey);
                roles.Add(t.Role);
                Assert.Equal(1, t.Revision);
                Assert.Equal(plan.PlanId, t.PlanId);
            }
            Assert.Equal(3, new HashSet<string>(keys).Count);
            Assert.Contains(FormationRole.Main, roles);
            Assert.Contains(FormationRole.Guard, roles);
            Assert.Contains(FormationRole.Support, roles);
        }

        // 三职责必须由不同对象承担：只有两支合格编队时不得让同一对象兼任两个职责。
        [Fact]
        public void TwoFormationsCannotFormCoordinationPlan()
        {
            var s = new FormationSnapshot { MissionKey = "m1", SampledAt = 100.0 };
            s.Records.Add(Rec(0, 0, "Infantry", 30, 0.0, 0.0, false));
            s.Records.Add(Rec(0, 1, "Ranged", 55, 0.0, 20.0, false));
            s.Records.Add(Rec(1, 0, "Infantry", 100, 0.0, 300.0, true));

            var plan = PlanBuilder.BuildCoordination(StateOf(s), "plan-1", TestParameters.P2Now);
            Assert.Null(plan);
        }

        // 第一批只在远程优势成立时创建；劣势与临界都不进入协同。
        [Fact]
        public void CoordinationOnlyUnderRangedAdvantage()
        {
            // 我方远程 15/100，敌方远程 10/100：远程占比差 = 0.05 < 0.10，创建门槛不成立。
            var weak = StateOf(Snapshot(45, 15, 40, 100, 0.0, 300.0, 10));
            Assert.Equal(0.05, weak.RangedEdge(), 3);
            Assert.False(weak.RangedEdge() >= TestParameters.RangedEdgeThreshold);
            Assert.Null(PlanBuilder.BuildCoordination(weak, "plan-1", TestParameters.P2Now));

            // 继续门槛也要求 >= +0.05：0.04 不满足。
            Assert.False(PlanConditions.From(weak).CoordinationCanContinue);
        }

        [Fact]
        public void CoordinationContinueNeedsContinueThreshold()
        {
            var hold = StateOf(Snapshot(40, 55, 5, 100, 0.0, 300.0));
            // 55/100 - 10/100 = 0.45
            Assert.True(PlanConditions.From(hold).CoordinationCanContinue);

            var drop = StateOf(Snapshot(45, 50, 5, 100, 0.0, 300.0));
            // 50/100 - 10/100 = 0.40，仍 >= 0.05，继续成立
            Assert.True(PlanConditions.From(drop).CoordinationCanContinue);

            var fall = StateOf(Snapshot(50, 45, 5, 100, 0.0, 300.0));
            // 45/100 - 10/100 = 0.35，仍 >= 0.05
            Assert.True(PlanConditions.From(fall).CoordinationCanContinue);
        }

        // ---- P2 协同计划的继续条件：远程优势继续线 + 兵力安全线 ----

        [Fact]
        public void CoordinationPlanContinuesOnRangedEdgeContinueLine()
        {
            var state = SceneA();
            var plan = PlanBuilder.BuildCoordination(state, "plan-1", TestParameters.P2Now);
            Assert.NotNull(plan);
            Assert.True(PlanConditions.From(state).CanContinueAs(plan));
        }

        // 远程优势跌破继续线（0.05）即不得继续协同。
        [Fact]
        public void CoordinationPlanStopsWhenRangedEdgeFallsBelowContinueLine()
        {
            var state = SceneA();
            var plan = PlanBuilder.BuildCoordination(state, "plan-1", TestParameters.P2Now);
            Assert.NotNull(plan);

            // 我方远程 4/100，敌方远程 10/100：远程占比差 = -0.06 < 0.05。
            var collapsed = StateOf(Snapshot(81, 4, 15, 100, 0.0, 300.0, 10));
            Assert.Equal(-0.06, collapsed.RangedEdge(), 3);
            Assert.False(PlanConditions.From(collapsed).CanContinueAs(plan));
        }

        // 兵力跌破继续线（0.70）即必须转向保存战力，即使远程优势仍在。
        [Fact]
        public void CoordinationPlanStopsWhenStrengthFallsBelowContinueLine()
        {
            var state = SceneA();
            var plan = PlanBuilder.BuildCoordination(state, "plan-1", TestParameters.P2Now);
            Assert.NotNull(plan);

            // 我方 60 人、敌方 100 人 → 0.60 < 0.70。
            var weakened = StateOf(Snapshot(20, 35, 5, 100, 0.0, 300.0, 10));
            var conditions = PlanConditions.From(weakened);
            Assert.True(conditions.StrengthRatio < TestParameters.ContinueStrengthLine);
            Assert.False(conditions.CanContinueAs(plan));
        }
    }
}
