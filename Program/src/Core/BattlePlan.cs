using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CombatBrain2.Core
{
    // 计划依赖的继续条件。条件在每次评价点用同一套读法复算，
    // 不成立即让计划失效，不做部分保留。
    internal sealed class PlanConditions
    {
        internal double RangedEdge;
        internal double StrengthRatio;
        internal bool RangedAdvantageHolds;
        internal bool RangedDisadvantageHolds;
        internal bool StrengthWithinTolerance;
        internal bool StrengthCritical;

        internal static PlanConditions From(BattleState state)
        {
            var c = new PlanConditions();
            c.RangedEdge = state.RangedEdge();
            c.StrengthRatio = state.StrengthRatio();
            c.RangedAdvantageHolds = c.RangedEdge >= TestParameters.RangedEdgeThreshold;
            c.RangedDisadvantageHolds = c.RangedEdge <= -TestParameters.RangedEdgeThreshold;
            c.StrengthWithinTolerance = c.StrengthRatio >= TestParameters.StrengthTolerance;
            c.StrengthCritical = c.StrengthRatio < TestParameters.StrengthDisadvantageLine;
            return c;
        }

        internal bool SameAs(PlanConditions other)
        {
            if (other == null) return false;
            return RangedAdvantageHolds == other.RangedAdvantageHolds &&
                   RangedDisadvantageHolds == other.RangedDisadvantageHolds &&
                   StrengthWithinTolerance == other.StrengthWithinTolerance &&
                   StrengthCritical == other.StrengthCritical;
        }

        // 继续判定（脚本 §4.5）：不用创建时的一次布尔相等，而用更窄的继续门槛。
        // 兵力低于继续线即必须转向保存战力，不再按原目标继续。
        internal bool CanContinueAs(BattlePlan plan)
        {
            if (plan == null) return false;
            if (StrengthRatio < TestParameters.ContinueStrengthLine) return false;
            if (StrengthCritical) return false;

            if (plan.Goal == "exploit_ranged_advantage")
            {
                return RangedEdge >= TestParameters.RangedEdgeContinueThreshold;
            }
            if (plan.Goal == "mitigate_ranged_disadvantage")
            {
                return RangedEdge <= -TestParameters.RangedEdgeContinueThreshold;
            }
            return false;
        }

        internal string Describe()
        {
            return "ranged_edge=" + RangedEdge.ToString("F3", CultureInfo.InvariantCulture) +
                   " strength_ratio=" + StrengthRatio.ToString("F3", CultureInfo.InvariantCulture) +
                   " ranged_advantage=" + (RangedAdvantageHolds ? 1 : 0) +
                   " ranged_disadvantage=" + (RangedDisadvantageHolds ? 1 : 0) +
                   " strength_within_tolerance=" + (StrengthWithinTolerance ? 1 : 0) +
                   " strength_critical=" + (StrengthCritical ? 1 : 0);
        }
    }

    // 一支编队承担的普通任务。每个任务有独立身份与独立写入额度：
    // 命令是否生效、是否超时、是否被覆盖都按任务自己的事实判定。
    internal sealed class FormationTask
    {
        internal string TaskId = "";
        internal string PlanId = "";
        internal int Ordinal;

        internal string FormationKey = "";
        internal string ObjectIdentity = "";
        internal string FormationClass = "";
        internal int TeamIndex;
        internal int FormationIndex;

        internal TaskKind Kind = TaskKind.None;
        internal string Reason = "";

        internal double CreatedAt;
        internal double SubmittedAt = -1.0;
        internal double SelectedAt = -1.0;
        internal double WrittenAt = -1.0;
        internal double FeedbackAt = -1.0;

        internal double OriginX;
        internal double OriginY;
        internal double RequestedX;
        internal double RequestedY;
        internal bool HasPosition;
        internal bool ReachedTargetPoint;

        // 位移基线：实际写令那一刻的位置。计划创建到发令之间的原生移动
        // 不计入本命令效果（P1-CODEX-REVERIFY §3.2）。
        internal double BaselineX;
        internal double BaselineY;
        internal double DistanceToTargetAtWrite = -1.0;

        // 实际命令目标点的读回值，取自原生 MovementOrder，不是请求值。
        internal bool HasOrderTarget;
        internal double OrderTargetX;
        internal double OrderTargetY;

        // 位置任务的目标点由执行层在发令前按当前几何计算并写入。
        internal void SetTarget(double originX, double originY, double requestedX, double requestedY)
        {
            OriginX = originX;
            OriginY = originY;
            RequestedX = requestedX;
            RequestedY = requestedY;
            HasPosition = true;
        }

        // 写令时固定位移基线；基线缺失时不得用计划创建时刻的位置顶替。
        internal void SetBaseline(double x, double y)
        {
            BaselineX = x;
            BaselineY = y;
        }

        internal void SetOrderTarget(double x, double y)
        {
            OrderTargetX = x;
            OrderTargetY = y;
            HasOrderTarget = true;
        }

        internal double BaselineDistanceTo(double x, double y)
        {
            double dx = x - BaselineX;
            double dy = y - BaselineY;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        internal double DistanceFromBaseline(double x, double y)
        {
            return BaselineDistanceTo(x, y);
        }

        internal double OrderTargetReadbackError()
        {
            if (!HasOrderTarget || !HasPosition) return -1.0;
            double dx = OrderTargetX - RequestedX;
            double dy = OrderTargetY - RequestedY;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        internal string OrderBefore = "";
        internal string OrderAfter = "";

        internal TaskFeedbackState Feedback = TaskFeedbackState.None;
        internal PlanInvalidReason FailureReason = PlanInvalidReason.None;
        internal string FailureDetail = "";

        internal bool IsClosed;
        internal double ClosedAt = -1.0;

        internal double SelectionDeadline()
        {
            return SubmittedAt < 0.0 ? CreatedAt + TestParameters.SelectionWaitSeconds
                                     : SubmittedAt + TestParameters.SelectionWaitSeconds;
        }

        internal double FeedbackDeadline()
        {
            return WrittenAt < 0.0 ? -1.0 : WrittenAt + TestParameters.FeedbackWindowSeconds;
        }

        internal string Describe()
        {
            return "task=" + TaskId +
                   " plan=" + PlanId +
                   " kind=" + Kind +
                   " key=" + FormationKey +
                   " obj=" + ObjectIdentity +
                   " order_before=" + OrderBefore +
                   " order_after=" + OrderAfter;
        }
    }

    // 一个计划：目标 + 理由 + 承担任务的编队 + 继续条件 + 失效条件。
    // 计划与具体命令分层：计划不含 Order，命令由任务在真实获选后产生。
    internal sealed class BattlePlan
    {
        internal string PlanId = "";
        internal string MissionKey = "";
        internal string Goal = "";
        internal string Reason = "";
        internal string PlanInputId = "";
        internal double CreatedAt;
        internal double InputSampledAt;

        internal PlanConditions Conditions = new PlanConditions();

        internal readonly List<FormationTask> Tasks = new List<FormationTask>();
        internal readonly List<string> RequiredFormations = new List<string>();

        internal bool IsClosed;
        internal double ClosedAt = -1.0;
        internal PlanInvalidReason InvalidReason = PlanInvalidReason.None;
        internal string InvalidDetail = "";

        internal int NormalTaskCount()
        {
            int n = 0;
            foreach (var t in Tasks)
            {
                if (!IsExitTask(t)) n++;
            }
            return n;
        }

        internal static bool IsExitTask(FormationTask t)
        {
            return t != null && t.TaskId.IndexOf("exit", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal string Describe()
        {
            var sb = new StringBuilder();
            sb.Append("plan=").Append(PlanId)
              .Append(" goal=").Append(Goal)
              .Append(" reason=").Append(Reason)
              .Append(" input=").Append(PlanInputId)
              .Append(" created_at=").Append(CreatedAt.ToString("F3", CultureInfo.InvariantCulture))
              .Append(" input_sampled_at=").Append(InputSampledAt.ToString("F3", CultureInfo.InvariantCulture))
              .Append(" formations=").Append(string.Join(",", RequiredFormations.ToArray()));
            return sb.ToString();
        }
    }
}
