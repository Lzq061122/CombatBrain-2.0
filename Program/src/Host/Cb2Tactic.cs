using System;
using System.Collections.Generic;
using CombatBrain2.Core;
using TaleWorlds.MountAndBlade;

namespace CombatBrain2.Host
{
    // 战术目标：从 CB2 现有计划/任务台账里读出的、当前时刻真正可执行的那部分。
    // 只读语义，不产生新决策：任务身份、计划版本、职责与截止都在 TestDriver 里判定，
    // 这里只把它转成战术层能用的两点——参与竞争与否，以及该编队获选后服务哪个任务。
    internal static class Cb2TacticTarget
    {
        // 有可执行任务即为真。exit 任务的准入沿用 BattlePlan.IsExitTask 的既有口径，
        // 不在战术层另立一套退出判据。
        internal static bool TrySelect(TaskLedger tasks, PlanLedger plans, out FormationTask task)
        {
            task = null;
            if (tasks == null || plans == null) return false;
            BattlePlan plan = plans.Current;
            if (plan == null || plan.IsClosed) return false;
            foreach (FormationTask t in tasks.OpenTasks())
            {
                if (t == null || t.IsClosed) continue;
                if (!string.Equals(t.PlanId, plan.PlanId, StringComparison.Ordinal)) continue;
                if (t.Revision != plan.Revision) continue;
                // 已判失败的旧任务在关闭前仍留在台账里，但它不再是可服务的分配。
                if (t.Feedback == TaskFeedbackState.Failed) continue;
                task = t;
                return true;
            }
            return false;
        }
    }

    // P3 战术层接入的候选战术，未接线、未部署：
    //   1) 只在本方存在合法可执行任务时参与竞争；没有任务返回 0，战术选择回到原生。
    //   2) 获选后服务现有任务分配，不自行决定冲锋、推进或另选目标点；
    //      编队的具体命令仍由 TestDriver 的写令门与 CbHoldBehavior 产生。
    //   3) 被换下即交还原生：解除本方行为的武装，使该编队的原生行为依据重新生效。
    internal sealed class Cb2Tactic : TacticComponent
    {
        private readonly TaskLedger _tasks;
        private readonly PlanLedger _plans;
        private readonly Func<Formation, bool> _handBack;

        internal Cb2Tactic(Team team)
            : this(team, null, null, null)
        {
        }

        internal Cb2Tactic(Team team, TaskLedger tasks, PlanLedger plans, Func<Formation, bool> handBack)
            : base(team)
        {
            _tasks = tasks;
            _plans = plans;
            _handBack = handBack;
            Cb2TacticProbe.OnTacticConstructed(GetType(), true);
        }

        internal int ManageCalls { get; private set; }
        internal int HandBackCalls { get; private set; }

        // 原生在 MaxBy 里对现任乘 1.5 倍；挑战者要越过的是现任的 1.5 倍门槛。
        // 资格不成立时返回 0：与原生"无可用战术"同义，不参与分配。
        protected override float GetTacticWeight()
        {
            FormationTask task;
            return Cb2TacticTarget.TrySelect(_tasks, _plans, out task) ? (float)TestParameters.TacticTaskPriority : 0f;
        }

        public override void TickOccasionally()
        {
            ManageCalls++;
            if (Team == null || Team.TeamAI == null) return;
            base.TickOccasionally();
        }

        // 被换下的唯一入口是 TeamAIComponent 的 CurrentTactic setter（先 OnCancel 再换）。
        protected override void OnCancel()
        {
            base.OnCancel();
            if (_handBack == null) return;
            foreach (Formation f in FormationsIncludingEmpty)
            {
                if (f == null) continue;
                if (!_handBack(f)) continue;
                HandBackCalls++;
            }
        }
    }
}
