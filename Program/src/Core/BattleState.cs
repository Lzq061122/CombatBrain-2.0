using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CombatBrain2.Core
{
    // 一次评价用的战场状态：只包含本批决策真正读取的字段。
    // 每条记录都绑定同一个快照，不允许用局部重新采样冒充同一状态。
    internal sealed class BattleState
    {
        internal string PlanInputId = "";
        internal string MissionKey = "";
        internal double SampledAt;
        internal int MyTeamIndex = -1;
        internal int EnemyTeamIndex = -1;

        internal int MyUnitCount;
        internal int EnemyUnitCount;
        internal int MyRangedCount;
        internal int EnemyRangedCount;
        internal int MyInfantryCount;
        internal int EnemyInfantryCount;

        internal double MyCenterX;
        internal double MyCenterY;
        internal double EnemyCenterX;
        internal double EnemyCenterY;

        internal readonly List<FormationRecord> MyFormations = new List<FormationRecord>();
        internal readonly List<FormationRecord> EnemyFormations = new List<FormationRecord>();

        internal double RangedEdge()
        {
            return RangedShare(MyRangedCount, MyUnitCount) - RangedShare(EnemyRangedCount, EnemyUnitCount);
        }

        internal double StrengthRatio()
        {
            if (EnemyUnitCount <= 0) return 0.0;
            return (double)MyUnitCount / (double)EnemyUnitCount;
        }

        internal double CenterDistance()
        {
            double dx = EnemyCenterX - MyCenterX;
            double dy = EnemyCenterY - MyCenterY;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        // 单位化方向向量只在两侧中心都有效且距离非零时有定义；
        // 否则返回 false，调用方必须放弃生成位置任务而不是伪造一个点。
        internal bool TryUnitDirection(out double ux, out double uy)
        {
            ux = 0.0;
            uy = 0.0;
            double distance = CenterDistance();
            if (distance <= 0.0001) return false;
            ux = (EnemyCenterX - MyCenterX) / distance;
            uy = (EnemyCenterY - MyCenterY) / distance;
            return IsFinite(ux) && IsFinite(uy);
        }

        internal static double RangedShare(int ranged, int total)
        {
            if (total <= 0) return 0.0;
            return (double)ranged / (double)total;
        }

        internal static bool IsFinite(double v)
        {
            return !double.IsNaN(v) && !double.IsInfinity(v);
        }

        internal string Describe()
        {
            var sb = new StringBuilder();
            sb.Append("plan_input=").Append(PlanInputId)
              .Append(" sampled_at=").Append(SampledAt.ToString("F3", CultureInfo.InvariantCulture))
              .Append(" my_team=").Append(MyTeamIndex.ToString(CultureInfo.InvariantCulture))
              .Append(" enemy_team=").Append(EnemyTeamIndex.ToString(CultureInfo.InvariantCulture))
              .Append(" my_units=").Append(MyUnitCount.ToString(CultureInfo.InvariantCulture))
              .Append(" enemy_units=").Append(EnemyUnitCount.ToString(CultureInfo.InvariantCulture))
              .Append(" my_ranged=").Append(MyRangedCount.ToString(CultureInfo.InvariantCulture))
              .Append(" enemy_ranged=").Append(EnemyRangedCount.ToString(CultureInfo.InvariantCulture))
              .Append(" my_infantry=").Append(MyInfantryCount.ToString(CultureInfo.InvariantCulture))
              .Append(" enemy_infantry=").Append(EnemyInfantryCount.ToString(CultureInfo.InvariantCulture))
              .Append(" ranged_edge=").Append(RangedEdge().ToString("F3", CultureInfo.InvariantCulture))
              .Append(" strength_ratio=").Append(StrengthRatio().ToString("F3", CultureInfo.InvariantCulture))
              .Append(" center_distance=").Append(CenterDistance().ToString("F2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }
    }
}
