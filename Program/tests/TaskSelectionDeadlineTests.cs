using System;
using System.Globalization;
using CombatBrain2.Core;
using Xunit;

namespace CombatBrain2.Tests
{
    public class TaskSelectionDeadlineTests
    {
        // 获选等待上限按"提交时刻"起算；尚未提交时按创建时刻起算。
        [Fact]
        public void SelectionDeadlineIsRelativeToSubmit()
        {
            var t = new FormationTask { TaskId = "t1", CreatedAt = 10.0 };
            Assert.Equal(10.0 + TestParameters.SelectionWaitSeconds, t.SelectionDeadline());

            t.SubmittedAt = 12.0;
            Assert.Equal(12.0 + TestParameters.SelectionWaitSeconds, t.SelectionDeadline());
        }

        // 截止点是固定的：重复读取不延长，已获选也不延长。
        [Fact]
        public void RepeatedReadsDoNotExtendDeadline()
        {
            var t = new FormationTask { TaskId = "t1", CreatedAt = 10.0, SubmittedAt = 11.0 };
            var original = t.SelectionDeadline();
            for (int n = 0; n < 100; n++)
            {
                t.SelectionDeadline();
            }
            Assert.Equal(original, t.SelectionDeadline());

            t.SelectedAt = 11.5;
            Assert.Equal(original, t.SelectionDeadline());
        }

        // 反馈窗口自写令时刻起算；未写入时不存在窗口。
        [Fact]
        public void FeedbackDeadlineIsRelativeToWrite()
        {
            var t = new FormationTask { TaskId = "t1", CreatedAt = 10.0 };
            Assert.Equal(-1.0, t.FeedbackDeadline());

            t.WrittenAt = 13.0;
            Assert.Equal(13.0 + TestParameters.FeedbackWindowSeconds, t.FeedbackDeadline());
        }

        // 计划条件判据的阈值边界：条件由 BattleState 派生，不手工赋值。
        [Fact]
        public void PlanConditionsUsePinnedThresholds()
        {
            // 我方远程占比 0.5、敌方 0.2 → 差值 0.3，高于远程优势门槛。
            var mine = new BattleState { MyUnitCount = 100, MyRangedCount = 50, EnemyUnitCount = 100, EnemyRangedCount = 20 };
            var c1 = PlanConditions.From(mine);
            Assert.True(c1.RangedEdge > TestParameters.RangedEdgeThreshold);
            Assert.True(c1.RangedAdvantageHolds);
            Assert.False(c1.RangedDisadvantageHolds);
            Assert.True(c1.StrengthWithinTolerance);
            Assert.False(c1.StrengthCritical);

            // 敌方远程优势：差值 -0.3，命中劣势判据。
            var theirs = new BattleState { MyUnitCount = 100, MyRangedCount = 20, EnemyUnitCount = 100, EnemyRangedCount = 50 };
            var c2 = PlanConditions.From(theirs);
            Assert.True(c2.RangedEdge < -TestParameters.RangedEdgeThreshold);
            Assert.True(c2.RangedDisadvantageHolds);
            Assert.False(c2.RangedAdvantageHolds);

            // 兵力低于劣势线：StrengthCritical 成立且不再落在容差内。
            var weak = new BattleState
            {
                MyUnitCount = (int)(TestParameters.StrengthDisadvantageLine * 100) - 1,
                EnemyUnitCount = 100,
            };
            var c3 = PlanConditions.From(weak);
            Assert.True(c3.StrengthCritical);
            Assert.False(c3.StrengthWithinTolerance);
        }

        // 条件相同判定只看四个布尔，不看原始数值。
        [Fact]
        public void ConditionComparisonIgnoresRawValues()
        {
            var a = new BattleState { MyUnitCount = 100, MyRangedCount = 50, EnemyUnitCount = 100, EnemyRangedCount = 20 };
            var b = new BattleState { MyUnitCount = 200, MyRangedCount = 100, EnemyUnitCount = 200, EnemyRangedCount = 40 };
            Assert.True(PlanConditions.From(a).SameAs(PlanConditions.From(b)));

            var different = new BattleState { MyUnitCount = 100, MyRangedCount = 20, EnemyUnitCount = 100, EnemyRangedCount = 50 };
            Assert.False(PlanConditions.From(a).SameAs(PlanConditions.From(different)));
        }

        // 无敌人时兵力比按 0 处理，不做除零。
        [Fact]
        public void StrengthRatioIsZeroWithoutEnemy()
        {
            var s = new BattleState { MyUnitCount = 100, EnemyUnitCount = 0 };
            Assert.Equal(0.0, s.StrengthRatio());
        }

        // 参数值就是批准的数。
        [Fact]
        public void ApprovedParametersArePinned()
        {
            Assert.Equal(2.0, TestParameters.SnapshotMaxAgeSeconds);
            Assert.Equal(10.0, TestParameters.SelectionWaitSeconds);
            Assert.Equal(3.0, TestParameters.DecisionCadenceSeconds);
            Assert.Equal(8.0, TestParameters.PlanMinStableSeconds);
            Assert.Equal(3.0, TestParameters.FeedbackWindowSeconds);
            Assert.Equal(1.0, TestParameters.SafetyCancelLatencySeconds);
        }

        // 本批我方是非玩家 AI 队、敌方是玩家队；归属按 IsPlayerTeam 判定，
        // 不能把非玩家队当敌方，也不能用"其余记录"兜底。
        [Fact]
        public void ControlledNonPlayerTeamIsOwnSideAndPlayerTeamIsEnemy()
        {
            var s = new FormationSnapshot { MissionKey = "m", SampledAt = 5.0 };
            s.Records.Add(Rec("0/0", team: 0, playerTeam: false, cls: "Infantry", units: 60, x: 0.0));
            s.Records.Add(Rec("0/1", team: 0, playerTeam: false, cls: "Ranged", units: 40, x: 0.0));
            s.Records.Add(Rec("1/0", team: 1, playerTeam: true, cls: "Infantry", units: 30, x: 100.0));
            s.Records.Add(Rec("1/1", team: 1, playerTeam: true, cls: "Ranged", units: 70, x: 100.0));

            var state = BattleAssessment.Read(s, "input-1");

            Assert.NotNull(state);
            Assert.Equal(0, state.MyTeamIndex);
            Assert.Equal(1, state.EnemyTeamIndex);
            // 我方非玩家队 100 人（步 60 / 远 40）；敌方玩家队 100 人（步 30 / 远 70）。
            Assert.Equal(100, state.MyUnitCount);
            Assert.Equal(40, state.MyRangedCount);
            Assert.Equal(100, state.EnemyUnitCount);
            Assert.Equal(70, state.EnemyRangedCount);
            // 远程占比差 = 0.40 - 0.70 = -0.30，即敌方远程优势，与本批配置 N 一致。
            Assert.Equal(-0.30, state.RangedEdge(), 3);
        }

        // 缺任一归属就不生成状态，不给未知归属填值。
        [Fact]
        public void MissingEitherSideYieldsNoState()
        {
            var onlyMine = new FormationSnapshot { MissionKey = "m", SampledAt = 5.0 };
            onlyMine.Records.Add(Rec("0/0", team: 0, playerTeam: false, cls: "Infantry", units: 60, x: 0.0));
            Assert.Null(BattleAssessment.Read(onlyMine, "input-1"));

            var onlyPlayer = new FormationSnapshot { MissionKey = "m", SampledAt = 5.0 };
            onlyPlayer.Records.Add(Rec("1/0", team: 1, playerTeam: true, cls: "Infantry", units: 30, x: 0.0));
            Assert.Null(BattleAssessment.Read(onlyPlayer, "input-1"));
        }

        // 统计口径是全队：暂失 AI 资格或玩家所有的编队仍计入所属队人数，
        // 但不参与任务候选。两者必须分开。
        [Fact]
        public void StatisticsCoverWholeTeamWhileCandidatesRequireEligibility()
        {
            var s = new FormationSnapshot { MissionKey = "m", SampledAt = 5.0 };
            s.Records.Add(Rec("0/0", team: 0, playerTeam: false, cls: "Infantry", units: 60, x: 0.0));
            s.Records.Add(Rec("0/1", team: 0, playerTeam: false, cls: "Ranged", units: 25, x: 0.0, aiControlled: false));
            s.Records.Add(Rec("0/2", team: 0, playerTeam: false, cls: "Ranged", units: 15, x: 0.0, playerControlled: true));
            s.Records.Add(Rec("1/0", team: 1, playerTeam: true, cls: "Infantry", units: 40, x: 100.0));
            s.Records.Add(Rec("1/1", team: 1, playerTeam: true, cls: "Ranged", units: 60, x: 100.0));

            var state = BattleAssessment.Read(s, "input-1");

            // 统计包含全部 100 人：60 步 + 25 暂失 AI 资格的远 + 15 玩家所有的远。
            Assert.Equal(100, state.MyUnitCount);
            Assert.Equal(40, state.MyRangedCount);
            Assert.Equal(60, state.MyInfantryCount);

            // 候选只取通过完整资格门者：两支远程都被排除，因此缺少 Ranged 候选。
            var plan = PlanBuilder.Build(state, "plan-1", 5.0);
            Assert.Null(plan);
        }

        // 两配置任务序号一致：Ordinal 1 = 远程，2 = 步兵；N 场角色为远程 Hold、步兵 Reposition。
        [Fact]
        public void TaskOrdinalsAndRolesMatchBothConfigs()
        {
            var advantage = new BattleState
            {
                MissionKey = "m",
                SampledAt = 5.0,
                MyTeamIndex = 0,
                EnemyTeamIndex = 1,
                MyUnitCount = 100,
                MyRangedCount = 70,
                MyInfantryCount = 30,
                EnemyUnitCount = 100,
                EnemyRangedCount = 20,
                MyCenterX = 0.0,
                MyCenterY = 0.0,
                EnemyCenterX = 100.0,
                EnemyCenterY = 0.0,
            };
            advantage.MyFormations.Add(Rec("0/0", team: 0, playerTeam: false, cls: "Infantry", units: 30, x: 0.0, order: "Advance"));
            advantage.MyFormations.Add(Rec("0/1", team: 0, playerTeam: false, cls: "Ranged", units: 70, x: 0.0, order: "Advance"));

            var p = PlanBuilder.Build(advantage, "plan-p", 5.0);
            Assert.NotNull(p);
            Assert.Equal("exploit_ranged_advantage", p.Goal);
            Assert.Equal(2, p.Tasks.Count);
            Assert.Equal(1, p.Tasks[0].Ordinal);
            Assert.Equal(TaskKind.Reposition, p.Tasks[0].Kind);
            Assert.Equal("0/1", p.Tasks[0].FormationKey);
            Assert.Equal(2, p.Tasks[1].Ordinal);
            Assert.Equal(TaskKind.Engage, p.Tasks[1].Kind);
            Assert.Equal("0/0", p.Tasks[1].FormationKey);

            var disadvantage = new BattleState
            {
                MissionKey = "m",
                SampledAt = 5.0,
                MyTeamIndex = 0,
                EnemyTeamIndex = 1,
                MyUnitCount = 100,
                MyRangedCount = 20,
                MyInfantryCount = 80,
                EnemyUnitCount = 100,
                EnemyRangedCount = 70,
                MyCenterX = 0.0,
                MyCenterY = 0.0,
                EnemyCenterX = 100.0,
                EnemyCenterY = 0.0,
            };
            disadvantage.MyFormations.Add(Rec("0/0", team: 0, playerTeam: false, cls: "Infantry", units: 80, x: 0.0, order: "Advance"));
            disadvantage.MyFormations.Add(Rec("0/1", team: 0, playerTeam: false, cls: "Ranged", units: 20, x: 0.0, order: "Advance"));

            var n = PlanBuilder.Build(disadvantage, "plan-n", 5.0);
            Assert.NotNull(n);
            Assert.Equal("mitigate_ranged_disadvantage", n.Goal);
            Assert.Equal(2, n.Tasks.Count);
            Assert.Equal(1, n.Tasks[0].Ordinal);
            Assert.Equal(TaskKind.Hold, n.Tasks[0].Kind);
            Assert.Equal("0/1", n.Tasks[0].FormationKey);
            Assert.Equal(2, n.Tasks[1].Ordinal);
            Assert.Equal(TaskKind.Reposition, n.Tasks[1].Kind);
            Assert.Equal("0/0", n.Tasks[1].FormationKey);
        }

        private static FormationRecord Rec(string key, int team, bool playerTeam, string cls, int units, double x,
                                           bool aiControlled = true, bool playerControlled = false, string order = "Stop")
        {
            var parts = key.Split('/');
            return new FormationRecord
            {
                MissionKey = "m",
                SampledAt = 5.0,
                TeamIndex = team,
                Side = playerTeam ? "Attacker" : "Defender",
                IsPlayerTeam = playerTeam,
                FormationIndex = int.Parse(parts[1], CultureInfo.InvariantCulture),
                FormationClass = cls,
                UnitCount = units,
                PosX = x,
                PosY = 0.0,
                MovementOrder = order,
                IsAiControlled = aiControlled,
                IsPlayerControlled = playerControlled,
                ObjectIdentity = "obj@" + key + "@" + cls,
            };
        }

        // 无位置的 Engage 不得走 Hold 路径被立即判生效；必须按朝敌位移判定。
        [Fact]
        public void EngageWithoutPositionIsNotInstantlyEffective()
        {
            var t = new FormationTask
            {
                TaskId = "t1",
                PlanId = "plan-1",
                Kind = TaskKind.Engage,
                FormationKey = "0/0",
                ObjectIdentity = "obj@0/0@Infantry",
                OriginX = 0.0,
                OriginY = 0.0,
                OrderAfter = "Charge",
                WrittenAt = 10.0,
            };
            var rec = Rec("0/0", team: 0, playerTeam: false, cls: "Infantry", units: 50, x: 0.5, order: "Charge");

            // 敌方中心在正前方 100 米，位移仅 0.5 米：未达 3 米门槛，且窗口未到 → Pending。
            var pending = TaskFeedback.Evaluate(t, rec, 11.0, 100.0, 0.0);
            Assert.Equal(TaskFeedbackState.Pending, pending.State);
            Assert.DoesNotContain("hold_no_displacement_required", pending.Detail);

            // 超过反馈窗口仍无朝敌位移 → 未生效。
            var failed = TaskFeedback.Evaluate(t, rec, 20.0, 100.0, 0.0);
            Assert.Equal(TaskFeedbackState.Failed, failed.State);
            Assert.Equal(PlanInvalidReason.NotEffective, failed.Reason);
        }

        // 朝敌位移达到门槛即生效；背敌位移不算。
        [Fact]
        public void EngageRequiresTowardEnemyDisplacement()
        {
            var t = new FormationTask
            {
                TaskId = "t1",
                PlanId = "plan-1",
                Kind = TaskKind.Engage,
                FormationKey = "0/0",
                ObjectIdentity = "obj@0/0@Infantry",
                OriginX = 0.0,
                OriginY = 0.0,
                OrderAfter = "Charge",
                WrittenAt = 10.0,
            };

            var forward = Rec("0/0", team: 0, playerTeam: false, cls: "Infantry", units: 50, x: 5.0, order: "Charge");
            var toward = TaskFeedback.Evaluate(t, forward, 11.0, 100.0, 0.0);
            Assert.Equal(TaskFeedbackState.Effective, toward.State);
            Assert.Contains("engage_toward_enemy", toward.Detail);

            var backward = Rec("0/0", team: 0, playerTeam: false, cls: "Infantry", units: 50, x: -5.0, order: "Charge");
            var away = TaskFeedback.Evaluate(t, backward, 11.0, 100.0, 0.0);
            Assert.Equal(TaskFeedbackState.Pending, away.State);
        }

        // Hold 保持按位置保持判定，仍不对位移作要求。
        [Fact]
        public void HoldKeepsPositionCriterion()
        {
            var t = new FormationTask
            {
                TaskId = "t1",
                PlanId = "plan-1",
                Kind = TaskKind.Hold,
                FormationKey = "0/1",
                ObjectIdentity = "obj@0/1@Ranged",
                OrderAfter = "Stop",
                WrittenAt = 10.0,
            };
            var rec = Rec("0/1", team: 0, playerTeam: false, cls: "Ranged", units: 20, x: 1.0, order: "Stop");
            var r = TaskFeedback.Evaluate(t, rec, 11.0, 100.0, 0.0);
            Assert.Equal(TaskFeedbackState.Effective, r.State);
            Assert.Equal("hold_no_displacement_required", r.Detail);
        }

        // Move 位移投影达标但距离未递减时不得判生效。
        [Fact]
        public void MoveRequiresClosingDistance()
        {
            var t = new FormationTask
            {
                TaskId = "t1",
                PlanId = "plan-1",
                Kind = TaskKind.Reposition,
                FormationKey = "0/0",
                ObjectIdentity = "obj@0/0@Infantry",
                OrderAfter = "Move",
                WrittenAt = 10.0,
            };
            t.SetTarget(0.0, 0.0, 20.0, 0.0);

            // 起点距离 20；远离到 -30 时距离变为 50 > 20，投影为负、closing=0 → 不生效。
            var awayRec = Rec("0/0", team: 0, playerTeam: false, cls: "Infantry", units: 50, x: -30.0, order: "Move");
            var away = TaskFeedback.Evaluate(t, awayRec, 11.0, double.NaN, double.NaN);
            Assert.Equal(TaskFeedbackState.Pending, away.State);
            Assert.Contains("closing=0", away.Detail);

            // 靠近过程中进入 0.5 m 容差 → 到达判定生效。
            var arrived = Rec("0/0", team: 0, playerTeam: false, cls: "Infantry", units: 50, x: 19.8, order: "Move");
            var at = TaskFeedback.Evaluate(t, arrived, 11.0, double.NaN, double.NaN);
            Assert.Equal(TaskFeedbackState.Effective, at.State);
            Assert.Contains("reached_target_point", at.Detail);
        }

        // 继续判定用 ±0.05 的窄门槛：远程优势衰减到 0.10 以下但仍在 0.05 以上时可以继续。
        [Fact]
        public void ContinueUsesNarrowerRangedThreshold()
        {
            var plan = new BattlePlan { PlanId = "plan-1", Goal = "exploit_ranged_advantage" };
            var c = new PlanConditions { RangedEdge = 0.07, StrengthRatio = 0.90 };
            Assert.True(c.CanContinueAs(plan));

            var decayed = new PlanConditions { RangedEdge = 0.03, StrengthRatio = 0.90 };
            Assert.False(decayed.CanContinueAs(plan));
        }

        // 兵力低于继续线（70%）时不得继续原计划。
        [Fact]
        public void ContinueStopsBelowStrengthLine()
        {
            var plan = new BattlePlan { PlanId = "plan-1", Goal = "exploit_ranged_advantage" };
            var weak = new PlanConditions { RangedEdge = 0.40, StrengthRatio = 0.65 };
            Assert.False(weak.CanContinueAs(plan));

            var ok = new PlanConditions { RangedEdge = 0.40, StrengthRatio = 0.75 };
            Assert.True(ok.CanContinueAs(plan));
        }

        // 兵力低于保存战力线时不给进攻任务，转为双 Hold。
        [Fact]
        public void CriticalStrengthProducesPreservePowerHold()
        {
            var s = new BattleState
            {
                MissionKey = "m",
                SampledAt = 5.0,
                MyTeamIndex = 0,
                EnemyTeamIndex = 1,
                MyUnitCount = 60,
                MyRangedCount = 40,
                MyInfantryCount = 20,
                EnemyUnitCount = 100,
                EnemyRangedCount = 20,
                MyCenterX = 0.0,
                MyCenterY = 0.0,
                EnemyCenterX = 100.0,
                EnemyCenterY = 0.0,
            };
            s.MyFormations.Add(Rec("0/0", team: 0, playerTeam: false, cls: "Infantry", units: 20, x: 0.0, order: "Advance"));
            s.MyFormations.Add(Rec("0/1", team: 0, playerTeam: false, cls: "Ranged", units: 40, x: 0.0, order: "Advance"));

            // 远程优势明显（0.60 对 0.20 → edge=+0.40），但兵力只有 0.60 < 0.70。
            var plan = PlanBuilder.Build(s, "plan-c", 5.0);
            Assert.NotNull(plan);
            Assert.Equal("hold_observation", plan.Goal);
            Assert.Equal(2, plan.Tasks.Count);
            Assert.All(plan.Tasks, t => Assert.Equal(TaskKind.Hold, t.Kind));
        }
    }
}
