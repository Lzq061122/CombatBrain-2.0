// Verbatim copies of the three engine tactic/behavior classes the selection
// chain compares our behavior against. Only the terse `base.Formation` prefix is
// kept; no scoring expression is altered.
using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;

namespace TaleWorlds.MountAndBlade
{
    public abstract class TacticComponent
    {
        protected TacticComponent(Team team)
        {
            Team = team;
        }

        public Team Team { get; protected set; }

        protected IEnumerable<Formation> FormationsIncludingSpecialAndEmpty => Team.FormationsIncludingSpecialAndEmpty;

        protected IEnumerable<Formation> FormationsIncludingEmpty => Team.FormationsIncludingEmpty;

        protected internal virtual void OnApply()
        {
        }

        public virtual void TickOccasionally()
        {
        }

        public static void SetDefaultBehaviorWeights(Formation f)
        {
            f.AI.SetBehaviorWeight<BehaviorCharge>(1f);
            f.AI.SetBehaviorWeight<BehaviorPullBack>(1f);
            f.AI.SetBehaviorWeight<BehaviorStop>(1f);
            f.AI.SetBehaviorWeight<BehaviorReserve>(1f);
        }
    }

    public class TacticCharge : TacticComponent
    {
        public TacticCharge(Team team)
            : base(team)
        {
        }

        public override void TickOccasionally()
        {
            foreach (Formation item in FormationsIncludingEmpty)
            {
                if (item.CountOfUnits > 0)
                {
                    item.AI.ResetBehaviorWeights();
                    SetDefaultBehaviorWeights(item);
                    item.AI.SetBehaviorWeight<BehaviorCharge>(10000f);
                }
            }
        }
    }

    public class BehaviorCharge : BehaviorComponent
    {
        public override float NavmeshlessTargetPositionPenalty => 1f;

        public BehaviorCharge(Formation formation)
            : base(formation)
        {
            CalculateCurrentOrder();
            BehaviorCoherence = 0.5f;
        }

        protected override void CalculateCurrentOrder()
        {
            CurrentOrder = Formation.CachedClosestEnemyFormation == null
                ? MovementOrder.MovementOrderCharge
                : MovementOrder.MovementOrderChargeToTarget(Formation.CachedClosestEnemyFormation.Formation);
        }

        public override void TickOccasionally()
        {
            base.TickOccasionally();
            CalculateCurrentOrder();
            Formation.SetMovementOrder(CurrentOrder);
        }

        protected override void OnBehaviorActivatedAux()
        {
            Formation.SetFiringOrder(FiringOrder.FiringOrderFireAtWill);
        }

        private float CalculateAIWeight()
        {
            FormationQuerySystem querySystem = Formation.QuerySystem;
            Formation.FormationQuerySystemHolder closest = Formation.CachedClosestEnemyFormation;
            float num = Formation.CachedAveragePosition.Distance(closest.Formation.CachedMedianPosition) / querySystem.MovementSpeedMaximum;
            float num3;
            if (!querySystem.IsCavalryFormation && !querySystem.IsRangedCavalryFormation)
            {
                float num2 = MBMath.ClampFloat(num, 4f, 10f);
                num3 = MBMath.Lerp(0.1f, 1f, 1f - (num2 - 4f) / 6f, 0f);
            }
            else if (num <= 4f)
            {
                float num4 = MBMath.ClampFloat(num, 0f, 4f);
                num3 = MBMath.Lerp(0.1f, 1.4f, num4 / 4f, 0f);
            }
            else
            {
                float num5 = MBMath.ClampFloat(num, 4f, 10f);
                num3 = MBMath.Lerp(0.1f, 1.4f, 1f - (num5 - 4f) / 6f, 0f);
            }
            float num6 = 0f;
            foreach (Team team in Mission.Current.Teams)
            {
                if (!team.IsEnemyOf(Formation.Team))
                {
                    continue;
                }
                foreach (Formation item in team.FormationsIncludingSpecialAndEmpty)
                {
                    if (item.CountOfUnits <= 0 || closest.Formation == item)
                    {
                        continue;
                    }
                    float num7 = item.CachedMedianPosition.Distance(closest.Formation.CachedMedianPosition) / item.QuerySystem.MovementSpeedMaximum;
                    if (num7 > num + 4f || (num <= 8f && !Formation.QuerySystem.IsSameAs(item.CachedClosestEnemyFormation)))
                    {
                        continue;
                    }
                    bool flag = false;
                    if (num <= 8f)
                    {
                        foreach (Team team2 in Formation.Team.Mission.Teams)
                        {
                            if (!team2.IsFriendOf(Formation.Team))
                            {
                                continue;
                            }
                            foreach (Formation item2 in team2.FormationsIncludingSpecialAndEmpty)
                            {
                                if (item2.CountOfUnits > 0 && item2 != Formation && item.QuerySystem.IsSameAs(item2.CachedClosestEnemyFormation) && item2.CachedMedianPosition.Distance(Formation.CachedAveragePosition) / item2.QuerySystem.MovementSpeedMaximum < num7 + 4f)
                                {
                                    flag = true;
                                    break;
                                }
                            }
                            if (flag)
                            {
                                break;
                            }
                        }
                    }
                    if (!flag)
                    {
                        num6 += item.QuerySystem.FormationMeleeFightingPower * item.QuerySystem.GetClassWeightedFactor(1f, 1f, 1f, 1f);
                    }
                }
            }
            float num8 = 0f;
            foreach (Team team3 in Mission.Current.Teams)
            {
                if (!team3.IsFriendOf(Formation.Team))
                {
                    continue;
                }
                foreach (Formation item3 in team3.FormationsIncludingSpecialAndEmpty)
                {
                    if (item3 != Formation && item3.CountOfUnits > 0 && closest == item3.CachedClosestEnemyFormation && item3.CachedMedianPosition.Distance(item3.CachedClosestEnemyFormation.Formation.CachedMedianPosition) / item3.QuerySystem.MovementSpeedMaximum < 4f)
                    {
                        num8 += item3.QuerySystem.FormationMeleeFightingPower * item3.QuerySystem.GetClassWeightedFactor(1f, 1f, 1f, 1f);
                    }
                }
            }
            float num9 = (Formation.QuerySystem.FormationMeleeFightingPower * querySystem.GetClassWeightedFactor(1f, 1f, 1f, 1f) + num8 + 1f) / (1f + num6 + closest.Formation.QuerySystem.FormationMeleeFightingPower * closest.QuerySystem.GetClassWeightedFactor(1f, 1f, 1f, 1f));
            num9 /= MBMath.ClampFloat(querySystem.Team.RemainingPowerRatio, 0.2f, 3f);
            if (num9 > 1f)
            {
                num9 = (num9 - 1f) / 3f;
                num9++;
            }
            num9 = MBMath.ClampFloat(num9, 0.1f, 1.25f);
            float num10 = 1f;
            float num11 = 1f;
            if (num <= 4f && num >= 1.5f)
            {
                num11 = 1.2f;
            }
            float num12 = 1f;
            if (num <= 4f && !querySystem.IsSameAs(closest.Formation.CachedClosestEnemyFormation))
            {
                num12 = 1.2f;
            }
            float num13 = querySystem.GetClassWeightedFactor(1f, 1f, 1.5f, 1.5f) * closest.QuerySystem.GetClassWeightedFactor(1f, 1f, 0.5f, 0.5f);
            return num3 * num9 * num10 * num11 * num12 * num13;
        }

        protected override float GetAiWeight()
        {
            float result = 0f;
            Formation.FormationQuerySystemHolder cachedClosestEnemyFormation = Formation.CachedClosestEnemyFormation;
            if (cachedClosestEnemyFormation == null)
            {
                if (Formation.Team.HasAnyEnemyTeamsWithAgents())
                {
                    result = 0.2f;
                }
            }
            else
            {
                result = CalculateAIWeight();
            }
            return result;
        }
    }

    public class BehaviorPullBack : BehaviorComponent
    {
        public BehaviorPullBack(Formation formation) : base(formation) { CalculateCurrentOrder(); }

        protected override void CalculateCurrentOrder()
        {
            CurrentOrder = MovementOrder.MovementOrderMove;
        }

        protected override float GetAiWeight()
        {
            return 0f;
        }
    }

    public class BehaviorStop : BehaviorComponent
    {
        public BehaviorStop(Formation formation) : base(formation) { CalculateCurrentOrder(); }

        protected override void CalculateCurrentOrder()
        {
            CurrentOrder = MovementOrder.MovementOrderStop;
        }

        protected override float GetAiWeight()
        {
            return 0f;
        }
    }

    public class BehaviorReserve : BehaviorComponent
    {
        public BehaviorReserve(Formation formation) : base(formation) { CalculateCurrentOrder(); }

        protected override void CalculateCurrentOrder()
        {
            CurrentOrder = MovementOrder.MovementOrderMove;
        }

        protected override float GetAiWeight()
        {
            return 0f;
        }
    }
}
