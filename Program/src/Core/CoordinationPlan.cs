using System;
using System.Collections.Generic;
using System.Globalization;

namespace CombatBrain2.Core
{
    // P2 协同职责。首批只开放远程优势下的三职责协同，任务种类仍只有 Hold/Engage/Reposition。
    internal enum FormationRole
    {
        None = 0,
        Main,
        Guard,
        Support,
    }

    // 职责前提失效的具体原因。每一条都必须由可核查事实产生。
    internal enum RolePremiseFailure
    {
        None = 0,
        StrengthBelowLine,
        ObjectGone,
        ClassNotInCombination,
        EligibilityLost,
        PositionUnknown,
    }

    // 一个职责的连续有效读数。派单 §4：保护/辅助必须由连续至少 1 s 的两次或更多
    // 有效读取确认；未知/陈旧/身份变化时重新建立，不沿用旧 ready。
    internal sealed class DependencyReadiness
    {
        internal double FirstReadAt = -1.0;
        internal double LastReadAt = -1.0;
        internal int ReadCount;

        // 连续窗口绑定在同一批成员上：成员集合变化即视为新的关系，
        // 必须先重新建立连续读数，不能把换人前后的读数拼成 ready。
        private string _digest = "";

        internal string Digest { get { return _digest; } }

        internal bool Ready
        {
            get
            {
                if (ReadCount < 2) return false;
                return (LastReadAt - FirstReadAt) >= TestParameters.CoordinationStableSeconds;
            }
        }

        // 记一次读数。未到位即清空连续窗口，避免把断续读数拼成 ready。
        // memberDigest 非空且与上一读数不同时同样清空。
        internal void Note(double missionTime, bool inPosition)
        {
            Note(missionTime, inPosition, "");
        }

        internal void Note(double missionTime, bool inPosition, string memberDigest)
        {
            if (!inPosition)
            {
                Clear();
                _digest = "";
                return;
            }
            string digest = memberDigest ?? "";
            if (digest.Length > 0 && _digest.Length > 0 &&
                !string.Equals(digest, _digest, StringComparison.Ordinal))
            {
                Clear();
                _digest = digest;
                FirstReadAt = missionTime;
                LastReadAt = missionTime;
                ReadCount = 1;
                return;
            }
            if (digest.Length > 0) _digest = digest;
            if (ReadCount == 0 || FirstReadAt < 0.0) FirstReadAt = missionTime;
            LastReadAt = missionTime;
            ReadCount++;
        }

        private void Clear()
        {
            FirstReadAt = -1.0;
            LastReadAt = -1.0;
            ReadCount = 0;
        }

        internal void Reset()
        {
            Clear();
            _digest = "";
        }

        // 主力推进的唯一释放条件：本计划三职责齐备，保护与辅助的命令都已实际写入、
        // 已生效且当前有效目标未偏离，并建立了连续有效读数。
        // 任一未就绪即不释放；不得只用一条历史 Effective 反馈放行。
        // 位置读数只说明编队此刻在某处，不能替代"这条命令写了且仍在生效"（派单 §3 L86/L103/L107）。
        internal static bool MainAdvanceAllowed(BattlePlan plan, FormationTask guard, FormationTask support,
                                                DependencyReadiness guardReady, DependencyReadiness supportReady)
        {
            if (plan == null || plan.IsClosed) return false;
            if (guard == null || support == null) return false;
            if (guard.IsClosed || support.IsClosed) return false;
            if (guard.Role != FormationRole.Guard) return false;
            if (support.Role != FormationRole.Support) return false;
            if (guard.WrittenAt < 0.0 || support.WrittenAt < 0.0) return false;
            if (guard.Revision != plan.Revision || support.Revision != plan.Revision) return false;
            if (guard.Feedback != TaskFeedbackState.Effective) return false;
            if (support.Feedback != TaskFeedbackState.Effective) return false;
            if (!CommandTargetStillValid(plan.Geometry, guard, true)) return false;
            if (!CommandTargetStillValid(plan.Geometry, support, false)) return false;
            if (guardReady == null || !guardReady.Ready) return false;
            if (supportReady == null || !supportReady.Ready) return false;
            return true;
        }

        // 该职责当前实际命令目标点是否仍落在本职责的到位区域内，且与有效请求点一致。
        // 未写令、未读到实际命令目标（未知）、几何缺失或目标已偏离请求点都算无效：
        // 既不接受"位置碰巧对"，也不接受用请求点或首次缓存冒充当前命令。
        private static bool CommandTargetStillValid(CoordinationGeometry g, FormationTask task, bool isGuard)
        {
            if (g == null || task == null) return false;
            if (!task.HasPosition) return false;
            if (!task.HasOrderTarget) return false;
            if (task.OrderTargetReadbackError() > TestParameters.TargetPointToleranceMeters) return false;
            return isGuard
                ? PlanBuilder.GuardInPosition(g, task.OrderTargetX, task.OrderTargetY)
                : PlanBuilder.SupportInPosition(g, task.OrderTargetX, task.OrderTargetY);
        }
    }

    // 协同几何：主力有限接近点、保护目标点、辅助目标点。
    // 全部在同一 BattleState 的有限二维坐标上计算，单位米。
    internal sealed class CoordinationGeometry
    {
        internal double MainX;
        internal double MainY;
        internal double GuardX;
        internal double GuardY;
        internal double SupportX;
        internal double SupportY;
        internal double ForwardX;
        internal double ForwardY;
        internal double LateralX;
        internal double LateralY;
        internal double ApproachMeters;

        // 主力初始实际中心 M0 与敌方全队活跃成员加权中心 E0。
        // 中心重合、方向非有限、目标点非法时不产生几何，调用方必须放弃而不是伪造点。
        internal static CoordinationGeometry Compute(double m0x, double m0y,
                                                    double e0x, double e0y,
                                                    double supportOriginX, double supportOriginY)
        {
            if (!IsFinite(m0x) || !IsFinite(m0y) || !IsFinite(e0x) || !IsFinite(e0y)) return null;

            double dx = e0x - m0x;
            double dy = e0y - m0y;
            double span = Math.Sqrt(dx * dx + dy * dy);
            if (span <= 0.0001) return null;

            double ux = dx / span;
            double uy = dy / span;
            double vx = -uy;
            double vy = ux;
            if (!IsFinite(ux) || !IsFinite(uy)) return null;

            double approach = Math.Min(TestParameters.CoordinationApproachMaxMeters,
                                       span / TestParameters.CoordinationApproachDivisor);
            if (!IsFinite(approach) || approach <= 0.0) return null;

            var g = new CoordinationGeometry();
            g.ForwardX = ux;
            g.ForwardY = uy;
            g.LateralX = vx;
            g.LateralY = vy;
            g.ApproachMeters = approach;

            g.MainX = m0x + ux * approach;
            g.MainY = m0y + uy * approach;
            if (!IsFinite(g.MainX) || !IsFinite(g.MainY)) return null;

            g.GuardX = g.MainX + ux * TestParameters.CoordinationGuardForwardMeters;
            g.GuardY = g.MainY + uy * TestParameters.CoordinationGuardForwardMeters;

            // 侧向符号取使候选移动距离较短的一侧，相等取正向。
            double plusX = g.MainX + vx * TestParameters.CoordinationSupportLateralMeters;
            double plusY = g.MainY + vy * TestParameters.CoordinationSupportLateralMeters;
            double minusX = g.MainX - vx * TestParameters.CoordinationSupportLateralMeters;
            double minusY = g.MainY - vy * TestParameters.CoordinationSupportLateralMeters;
            if (!IsFinite(plusX) || !IsFinite(plusY)) return null;
            if (!IsFinite(minusX) || !IsFinite(minusY)) return null;

            double dPlus = Distance(supportOriginX, supportOriginY, plusX, plusY);
            double dMinus = Distance(supportOriginX, supportOriginY, minusX, minusY);
            if (!IsFinite(dPlus) || !IsFinite(dMinus)) return null;

            if (dMinus < dPlus)
            {
                g.SupportX = minusX;
                g.SupportY = minusY;
            }
            else
            {
                g.SupportX = plusX;
                g.SupportY = plusY;
            }

            return g;
        }

        // 相对某参考点沿协同前向/侧向轴的投影。
        internal double ForwardOffsetFrom(double rx, double ry, double x, double y)
        {
            return (x - rx) * ForwardX + (y - ry) * ForwardY;
        }

        internal double LateralOffsetFrom(double rx, double ry, double x, double y)
        {
            return (x - rx) * LateralX + (y - ry) * LateralY;
        }

        internal static double Distance(double ax, double ay, double bx, double by)
        {
            double dx = bx - ax;
            double dy = by - ay;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        internal static bool IsFinite(double v)
        {
            return !double.IsNaN(v) && !double.IsInfinity(v);
        }

        internal string Describe()
        {
            return "M*=(" + Fmt(MainX) + "," + Fmt(MainY) + ")" +
                   " G*=(" + Fmt(GuardX) + "," + Fmt(GuardY) + ")" +
                   " S*=(" + Fmt(SupportX) + "," + Fmt(SupportY) + ")" +
                   " u=(" + Fmt(ForwardX) + "," + Fmt(ForwardY) + ")" +
                   " v=(" + Fmt(LateralX) + "," + Fmt(LateralY) + ")" +
                   " approach=" + Fmt(ApproachMeters);
        }

        private static string Fmt(double v)
        {
            return v.ToString("F3", CultureInfo.InvariantCulture);
        }
    }
}
