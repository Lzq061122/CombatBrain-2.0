using System;
using System.Globalization;

namespace CombatBrain2.Core
{
    // 提案公布与写前复核的判定链（派单 §2.2 第 1 条）。
    // 放在 Core 是为了让离线夹具走与实机完全相同的方法：
    // Host 只负责取读取数与记日志，读哪个坐标、怎么比对都在这里。
    internal static class ProposalWiring
    {
        // 提案身份、竞争误差与写前复核统一读这一个点：
        // 质心语义任务取战术目标 T，按锚点定义的任务取其请求点；
        // 任务还没有目标时（首次公布提案）回落到计划几何的职责点。
        internal static bool TacticTargetPoint(BattlePlan plan, FormationTask task, out double x, out double y)
        {
            if (task != null && task.TryTacticTarget(out x, out y)) return true;
            return RoleTargetPoint(plan, task, out x, out y);
        }

        // 协同任务的目标点由计划几何直接给出，与「是否已写过令」无关。
        // 目标若只能从已写令的回读里取，就会形成「提案要目标 → 目标要写令 →
        // 写令要提案已公布」的死锁，使计划永远无法产生第一条命令。
        private static bool RoleTargetPoint(BattlePlan plan, FormationTask task, out double x, out double y)
        {
            x = 0.0;
            y = 0.0;
            if (task == null) return false;
            if (plan == null || plan.Geometry == null) return false;
            if (!string.Equals(task.PlanId, plan.PlanId, StringComparison.Ordinal)) return false;
            if (task.Revision != plan.Revision) return false;

            var g = plan.Geometry;
            switch (task.Role)
            {
                case FormationRole.Guard: x = g.GuardX; y = g.GuardY; break;
                case FormationRole.Support: x = g.SupportX; y = g.SupportY; break;
                case FormationRole.Main: x = g.MainX; y = g.MainY; break;
                default: return false;
            }
            return CoordinationGeometry.IsFinite(x) && CoordinationGeometry.IsFinite(y);
        }

        // 公布阶段构造提案。返回 null 表示需要位置目标的命令读不到目标：
        // 不能以 (0,0) 冒充有效目标，否则「无目标」会被当成「目标恰在原点」参与竞争。
        internal static Proposal BuildProposal(string missionKey, BattlePlan plan, FormationTask task, double now)
        {
            double x, y;
            bool hasTarget = TacticTargetPoint(plan, task, out x, out y);
            if (!hasTarget && ProposalArbiter.NeedsTarget(task.Kind)) return null;
            return Proposal.Create(missionKey, plan, task, task.Kind, ProposalSource.TaskSubmission,
                                   x, y, hasTarget, task.Reason, now);
        }

        // 写令前复核：返回 null 表示可继续写令，否则返回拒绝原因。
        // 即时复核用与公布同源的提案身份，避免写前换算（RequestedX/Y 换成锚点请求）
        // 把同一份战术意图算成两个 ID，使正常命令被判成内容变化而永远写不出去。
        internal static string RecheckPublished(ProposalArbiter arbiter, string missionKey,
                                                BattlePlan plan, FormationTask task, double now)
        {
            if (arbiter == null || plan == null || task == null) return "no_published_decision";

            var current = arbiter.CurrentFor(task.FormationKey);
            if (current == null) return "no_published_decision";

            if (!string.Equals(current.TaskId, task.TaskId, StringComparison.Ordinal))
                return "superseded_by=" + current.TaskId;

            if (current.Revision != plan.Revision)
                return "published_revision_stale published=" + current.Revision + " plan=" + plan.Revision;

            var fresh = BuildProposal(missionKey, plan, task, now);
            if (fresh == null) return "target_missing_at_write";

            if (!string.Equals(fresh.ProposalId, current.ProposalId, StringComparison.Ordinal))
                return "decision_content_changed fresh=" + fresh.ProposalId +
                       " published=" + current.ProposalId;

            double targetX, targetY;
            bool hasTarget = TacticTargetPoint(plan, task, out targetX, out targetY);
            if (current.HasTarget && hasTarget)
            {
                double error = CoordinationGeometry.Distance(current.TargetX, current.TargetY, targetX, targetY);
                if (error > TestParameters.TargetPointToleranceMeters)
                    return "decision_target_mismatch error=" +
                           error.ToString("F3", CultureInfo.InvariantCulture);
            }

            return null;
        }
    }
}
