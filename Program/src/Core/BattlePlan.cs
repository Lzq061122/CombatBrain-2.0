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

        // P2 协同的继续判定：只在更窄的继续门槛上成立。
        internal bool CoordinationCanContinue;

        internal static PlanConditions From(BattleState state)
        {
            var c = new PlanConditions();
            c.RangedEdge = state.RangedEdge();
            c.StrengthRatio = state.StrengthRatio();
            c.RangedAdvantageHolds = c.RangedEdge >= TestParameters.RangedEdgeThreshold;
            c.RangedDisadvantageHolds = c.RangedEdge <= -TestParameters.RangedEdgeThreshold;
            c.StrengthWithinTolerance = c.StrengthRatio >= TestParameters.StrengthTolerance;
            c.StrengthCritical = c.StrengthRatio < TestParameters.StrengthDisadvantageLine;
            c.CoordinationCanContinue = c.RangedEdge >= TestParameters.RangedEdgeContinueThreshold;
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
            // P2 协同计划与 P1 远程优势同门槛继续，但兵力安全线同样先行。
            if (plan.Goal == PlanBuilder.GoalCoordination)
            {
                return CoordinationCanContinue;
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

        // ---- P2 协同字段（派单 §3）----
        // 任务在同一开放 Plan 下的完整分配版本。职责/对象/依赖集改变必须增版本，
        // 并让三者旧普通任务全部关闭后用新 ID 重新绑定；旧版本不得写令。
        internal int Revision = 1;
        internal FormationRole Role = FormationRole.None;
        // 依赖字段：本任务保护谁 / 辅助谁 / 依赖哪条状态。
        internal string GuardsTaskId = "";
        internal string SupportsTaskId = "";
        internal string DependsOnTaskId = "";
        // 依赖实际成立后才关等待任务并原子指定新推进任务 ID。
        internal string ReplacesTaskId = "";
        // 命令建议身份：由内容派生，可稳定比较，不能按接收先后编号。
        internal string ProposalId = "";
        internal ProposalSource Source = ProposalSource.TaskSubmission;
        // 职责分配时的实读人数基线，用于人数继续线（>=6 且 >= 该基线 50%）。
        internal int RoleBaselineUnits;
        internal double RoleAssignedAt = -1.0;
        // 退出任务专用：它由哪次普通任务关闭产生。
        // 归属判定需要这个身份才能把「这次关闭」与「这条退出任务」关联起来。
        internal string OriginTaskId = "";

        internal double CreatedAt;
        internal double SubmittedAt = -1.0;
        internal int SubmissionCount;
        internal double SelectedAt = -1.0;
        internal double WrittenAt = -1.0;
        internal double FeedbackAt = -1.0;

        internal double OriginX;
        internal double OriginY;

        // 两个距离基准分开表达（派单 §2.2 第 1 条）：
        //   CanonicalX/Y —— 战术目标 T，语义是「希望到达的质心位置」；
        //   RequestedX/Y —— 本次实际写给引擎的锚点目标，由 T 按写前读数换算得到。
        // 二者是同一世界坐标里的不同参考点，任何判据、日志与复算都不得把两者混作距离。
        internal double RequestedX;
        internal double RequestedY;
        internal bool HasPosition;
        internal bool ReachedTargetPoint;

        // 位移基线：实际写令那一刻的进度起点。锚点语义任务用当时的编队锚点；
        // 质心语义任务（HasCanonicalTarget）用当时同批读数的有效成员质心。
        // 计划创建到发令之间的原生移动不计入本命令效果（P1-CODEX-REVERIFY §3.2）。
        internal double BaselineX;
        internal double BaselineY;
        internal double DistanceToTargetAtWrite = -1.0;

        // 写令边界换算（派单 §2.1 已裁定）：把战术目标 T 换算成本次写给引擎的锚点目标。
        // A=写前有效锚点，C=同对象同批读数质心，则 anchor = T + (A - C)。
        // 反了会系统性把预期质心推到 2(C-A) 之外：A=100、C=110、T=150 必须得 140。
        internal static double AnchoredRequest(double tactic, double anchor, double center)
        {
            return tactic + (anchor - center);
        }

        // 实际命令目标点的读回值，取自原生 MovementOrder，不是请求值。
        internal bool HasOrderTarget;
        internal double OrderTargetX;
        internal double OrderTargetY;

        // 战术目标点 T（主力 M*、保护 G*、辅助 S*；P3 撤回为质心向后 20 m 点）。
        // 语义是「期望质心位置」，与 RequestedX/Y（写给引擎的锚点目标）分开。
        // 提案阶段任务还没写令，RequestedX/Y 仍是空值；首次竞争的
        //「距规范目标点的误差」必须取这个点，否则该规则在首次裁决时失效（派单 §5 第 3 条）。
        internal bool HasCanonicalTarget;
        internal double CanonicalX;
        internal double CanonicalY;

        internal void SetCanonicalTarget(double x, double y)
        {
            CanonicalX = x;
            CanonicalY = y;
            HasCanonicalTarget = true;
        }

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

        // 每次读回都刷新成"当前实际命令目标点"：写令时读一次，之后每个反馈 tick
        // 与依赖复核前再读一次。只保留最后一次成功读回的值，不用首次缓存充当当前值。
        internal void SetOrderTarget(double x, double y)
        {
            OrderTargetX = x;
            OrderTargetY = y;
            HasOrderTarget = true;
        }

        // 本次读不到实际命令目标点时清掉可读事实：未知不等于"目标已正确落地"。
        internal void ClearOrderTarget()
        {
            HasOrderTarget = false;
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

        // 记一次候选投递。SubmittedAt 只记首次投递时刻：获选等待从首次提交起算，
        // 重复投递（3 s 决策节奏下的常态）不得刷新截止（脚本 §5.2）。
        internal void NoteSubmission(double missionTime)
        {
            SubmissionCount++;
            if (SubmittedAt < 0.0) SubmittedAt = missionTime;
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

        // P3：继任计划的实际来源与触发反馈。普通计划为空/未触发。
        // t0 与冻结集合由 ActionFeedbackTracker 继承，不在计划上复制.
        internal string ParentPlanId = "";
        internal double FeedbackAt = -1.0;
        internal string FeedbackReason = "";
        internal double FeedbackUtility = double.NaN;

        // P2：同一开放 Plan 下一次完整任务分配的版本号。分配改变即增，
        // 旧版本任务不得写令，也不得取消或清掉新版本的槽位。
        internal int Revision = 1;

        // P2：本计划的三职责协同几何。主路径没有几何时为 null。
        internal CoordinationGeometry Geometry;

        // P2：任务种类按职责/类别的相称性判定，不靠调用点约定。
        internal bool Allows(TaskKind kind)
        {
            foreach (var t in Tasks)
            {
                if (IsExitTask(t)) continue;
                if (t.Kind == kind) return true;
            }
            return false;
        }

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
              .Append(" revision=").Append(Revision.ToString(CultureInfo.InvariantCulture))
              .Append(" created_at=").Append(CreatedAt.ToString("F3", CultureInfo.InvariantCulture))
              .Append(" input_sampled_at=").Append(InputSampledAt.ToString("F3", CultureInfo.InvariantCulture))
              .Append(" formations=").Append(string.Join(",", RequiredFormations.ToArray()));
            return sb.ToString();
        }
    }
}
