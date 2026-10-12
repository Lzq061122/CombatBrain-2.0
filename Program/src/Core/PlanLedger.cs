using System;
using System.Collections.Generic;
using System.Globalization;

namespace CombatBrain2.Core
{
    // 计划生命周期：建立、保持、失效处置。
    // 计划自己在条件不成立、任务超时、写令失败、命令不生效、
    // 命令被覆盖或编队失去资格时失效；失效后一次性处置全部开放任务。
    internal sealed class PlanLedger
    {
        private BattlePlan _current;
        private int _planCount;
        private int _invalidations;
        private readonly Dictionary<string, int> _invalidationReasons =
            new Dictionary<string, int>(StringComparer.Ordinal);

        internal BattlePlan Current { get { return _current; } }
        internal bool HasPlan { get { return _current != null && !_current.IsClosed; } }
        internal int PlanCount { get { return _planCount; } }
        internal int Invalidations { get { return _invalidations; } }

        internal int CountOf(PlanInvalidReason reason)
        {
            int n;
            return _invalidationReasons.TryGetValue(reason.ToString(), out n) ? n : 0;
        }

        // 条件未变且计划仍开放时可继续沿用旧计划。
        // 继续判定用更窄的继续门槛与兵力线，不用创建时的一次布尔相等（脚本 §4.5）。
        internal bool CanContinue(PlanConditions conditions, double now)
        {
            if (_current == null || _current.IsClosed) return false;
            if (conditions == null) return false;
            return conditions.CanContinueAs(_current);
        }

        internal BattlePlan Create(BattleState state, double now)
        {
            _planCount++;
            var id = "plan-" + _planCount.ToString(CultureInfo.InvariantCulture);
            var plan = PlanBuilder.Build(state, id, now);
            if (plan != null) _current = plan;
            return plan;
        }

        internal void Invalidate(PlanInvalidReason reason, string detail, double now)
        {
            if (_current == null || _current.IsClosed) return;
            _current.IsClosed = true;
            _current.ClosedAt = now;
            _current.InvalidReason = reason;
            _current.InvalidDetail = detail ?? "";
            _invalidations++;
            var key = reason.ToString();
            int n;
            _invalidationReasons.TryGetValue(key, out n);
            _invalidationReasons[key] = n + 1;
        }

        internal void ClearMission()
        {
            _current = null;
            _planCount = 0;
            _invalidations = 0;
            _invalidationReasons.Clear();
        }
    }
}
