using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CombatBrain2.Core
{
    // 从同一份快照读出战场状态。只使用采样记录本身的字段，
    // 不新增引擎私有访问，也不在评价中途重新采样。
    internal static class BattleAssessment
    {
        internal static BattleState Read(FormationSnapshot snapshot, string planInputId)
        {
            if (snapshot == null) return null;

            var state = new BattleState
            {
                PlanInputId = planInputId,
                MissionKey = snapshot.MissionKey,
                SampledAt = snapshot.SampledAt,
            };

            // 本批我方是实际受控的非玩家 AI 队，敌方是与它对立的玩家队。
            // 归属按实际队关系判定，不硬编码 team 号，也不把"其余记录"泛化成一方。
            // 两类归属各须唯一确定，任一缺失就不生成状态。
            foreach (var r in snapshot.Records)
            {
                if (r.UnitCount <= 0) continue;
                if (r.TeamIndex < 0) continue;
                if (r.IsPlayerTeam)
                {
                    if (state.EnemyTeamIndex < 0) state.EnemyTeamIndex = r.TeamIndex;
                }
                else
                {
                    if (state.MyTeamIndex < 0) state.MyTeamIndex = r.TeamIndex;
                }
            }
            if (state.MyTeamIndex < 0 || state.EnemyTeamIndex < 0) return null;

            double myWeight = 0.0;
            double mySumX = 0.0;
            double mySumY = 0.0;
            double enemyWeight = 0.0;
            double enemySumX = 0.0;
            double enemySumY = 0.0;

            foreach (var r in snapshot.Records)
            {
                if (r.UnitCount <= 0) continue;
                if (r.TeamIndex == state.EnemyTeamIndex)
                {
                    state.EnemyFormations.Add(r);
                    state.EnemyUnitCount += r.UnitCount;
                    if (IsRanged(r.FormationClass))
                    {
                        state.EnemyRangedCount += r.UnitCount;
                    }
                    if (IsInfantry(r.FormationClass))
                    {
                        state.EnemyInfantryCount += r.UnitCount;
                    }
                    enemyWeight += r.UnitCount;
                    enemySumX += r.PosX * r.UnitCount;
                    enemySumY += r.PosY * r.UnitCount;
                }
                else if (r.TeamIndex == state.MyTeamIndex)
                {
                    state.MyFormations.Add(r);
                    state.MyUnitCount += r.UnitCount;
                    if (IsRanged(r.FormationClass))
                    {
                        state.MyRangedCount += r.UnitCount;
                    }
                    if (IsInfantry(r.FormationClass))
                    {
                        state.MyInfantryCount += r.UnitCount;
                    }
                    myWeight += r.UnitCount;
                    mySumX += r.PosX * r.UnitCount;
                    mySumY += r.PosY * r.UnitCount;
                }
            }

            if (myWeight > 0.0)
            {
                state.MyCenterX = mySumX / myWeight;
                state.MyCenterY = mySumY / myWeight;
            }
            if (enemyWeight > 0.0)
            {
                state.EnemyCenterX = enemySumX / enemyWeight;
                state.EnemyCenterY = enemySumY / enemyWeight;
            }

            return state;
        }

        internal static bool IsRanged(string formationClass)
        {
            return Contains(formationClass, "Ranged") || Contains(formationClass, "HorseArcher");
        }

        internal static bool IsInfantry(string formationClass)
        {
            return Contains(formationClass, "Infantry") && !IsRanged(formationClass);
        }

        private static bool Contains(string value, string token)
        {
            if (string.IsNullOrEmpty(value)) return false;
            return value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    // 从战场状态形成计划：目标、理由、承担任务的编队、继续条件。
    // 计划不包含具体 Order，命令由任务在真实获选后产生。
    internal static class PlanBuilder
    {
        internal static BattlePlan Build(BattleState state, string planId, double now)
        {
            if (state == null) return null;

            var conditions = PlanConditions.From(state);
            var ranged = PickFirst(state.MyFormations, BattleAssessment.IsRanged);
            var infantry = PickFirst(state.MyFormations, BattleAssessment.IsInfantry);

            // 缺任一类别就不生成双编队计划，不做单编队降级冒充。
            if (ranged == null || infantry == null) return null;

            var plan = new BattlePlan
            {
                PlanId = planId,
                MissionKey = state.MissionKey,
                PlanInputId = state.PlanInputId,
                CreatedAt = now,
                InputSampledAt = state.SampledAt,
                Conditions = conditions,
            };

            plan.RequiredFormations.Add(ranged.Key());
            plan.RequiredFormations.Add(infantry.Key());

            if (conditions.StrengthCritical)
            {
                // 兵力已低于保存战力线：不按原目标进攻，两支编队转为保持（脚本 §4.5）。
                plan.Goal = "hold_observation";
                plan.Reason = "strength_critical ratio=" +
                              conditions.StrengthRatio.ToString("F3", CultureInfo.InvariantCulture) +
                              " line=" + TestParameters.StrengthDisadvantageLine.ToString("F2",
                                  CultureInfo.InvariantCulture);
                plan.Tasks.Add(MakeTask(plan, 1, ranged, TaskKind.Hold, "preserve_power_ranged_hold"));
                plan.Tasks.Add(MakeTask(plan, 2, infantry, TaskKind.Hold, "preserve_power_infantry_hold"));
            }
            else if (conditions.RangedAdvantageHolds)
            {
                // 己方远程优势：远程压上到有限接近点，步兵向前接敌。
                plan.Goal = "exploit_ranged_advantage";
                plan.Reason = "ranged_edge=" + conditions.RangedEdge.ToString("F3", CultureInfo.InvariantCulture) +
                              ">=threshold";
                plan.Tasks.Add(MakeTask(plan, 1, ranged, TaskKind.Reposition,
                    "ranged_advantage_advance"));
                plan.Tasks.Add(MakeTask(plan, 2, infantry, TaskKind.Engage,
                    "ranged_advantage_engage"));
            }
            else if (conditions.RangedDisadvantageHolds)
            {
                // 己方远程劣势：远程保持位置，步兵后撤到有限距离。
                plan.Goal = "mitigate_ranged_disadvantage";
                plan.Reason = "ranged_edge=" + conditions.RangedEdge.ToString("F3", CultureInfo.InvariantCulture) +
                              "<=-threshold";
                plan.Tasks.Add(MakeTask(plan, 1, ranged, TaskKind.Hold,
                    "ranged_disadvantage_hold"));
                plan.Tasks.Add(MakeTask(plan, 2, infantry, TaskKind.Reposition,
                    "ranged_disadvantage_fallback"));
            }
            else
            {
                // 条件不成立：不给编队派普通任务，只保持观察。
                plan.Goal = "hold_observation";
                plan.Reason = "no_condition_met ranged_edge=" +
                              conditions.RangedEdge.ToString("F3", CultureInfo.InvariantCulture);
            }

            return plan;
        }

        // 任务按稳定顺序建立：两配置一致，Ordinal 1 = 远程类，2 = 步兵类。
        private static FormationTask MakeTask(BattlePlan plan, int ordinal, FormationRecord record,
                                              TaskKind kind, string reason)
        {
            return new FormationTask
            {
                TaskId = plan.PlanId + "-task-" + ordinal.ToString(CultureInfo.InvariantCulture),
                PlanId = plan.PlanId,
                Ordinal = ordinal,
                FormationKey = record.Key(),
                ObjectIdentity = record.ObjectIdentity,
                FormationClass = record.FormationClass,
                TeamIndex = record.TeamIndex,
                FormationIndex = record.FormationIndex,
                Kind = kind,
                Reason = reason,
                CreatedAt = plan.CreatedAt,
                OrderBefore = record.MovementOrder,
                OriginX = record.PosX,
                OriginY = record.PosY,
            };
        }

        // 任务候选必须在完整资格门之后才能入选：仅人数和兵种类别不构成资格。
        private static bool EligibleAsTaskCandidate(FormationRecord r)
        {
            if (r.UnitCount <= 0) return false;
            if (r.IsPlayerTeam) return false;
            if (r.IsPlayerControlled) return false;
            if (!r.IsAiControlled) return false;
            if (string.IsNullOrEmpty(r.ObjectIdentity)) return false;
            return true;
        }

        private static FormationRecord PickFirst(List<FormationRecord> records, Func<string, bool> predicate)
        {
            FormationRecord best = null;
            foreach (var r in records)
            {
                if (!EligibleAsTaskCandidate(r)) continue;
                if (!predicate(r.FormationClass)) continue;
                if (best == null || CompareStable(r, best) < 0) best = r;
            }
            return best;
        }

        private static int CompareStable(FormationRecord a, FormationRecord b)
        {
            if (a.TeamIndex != b.TeamIndex) return a.TeamIndex.CompareTo(b.TeamIndex);
            return a.FormationIndex.CompareTo(b.FormationIndex);
        }
    }
}
