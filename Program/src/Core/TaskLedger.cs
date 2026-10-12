using System;
using System.Collections.Generic;

namespace CombatBrain2.Core
{
    // 多任务台账：每个任务独立身份、独立写入额度、独立生命周期。
    // 允许同一编队先后承担新任务，但同一时刻该编队只能有一个未关闭任务。
    internal sealed class TaskLedger
    {
        private readonly Dictionary<string, FormationTask> _tasks =
            new Dictionary<string, FormationTask>(StringComparer.Ordinal);

        private int _writeAttemptsTotal;

        // 只统计真正落地的写入，被门拒绝的不计。
        internal int WriteAttemptsTotal { get { return _writeAttemptsTotal; } }

        internal int TaskCount { get { return _tasks.Count; } }

        internal ICollection<string> TaskIds { get { return _tasks.Keys; } }

        // 被拒绝的写入尝试逐任务计数，用于"候选不等于写令"的核对。
        private readonly Dictionary<string, int> _blockedAttempts =
            new Dictionary<string, int>(StringComparer.Ordinal);

        internal FormationTask Get(string taskId)
        {
            if (string.IsNullOrEmpty(taskId)) return null;
            FormationTask t;
            return _tasks.TryGetValue(taskId, out t) ? t : null;
        }

        internal void Add(FormationTask task)
        {
            if (task == null || string.IsNullOrEmpty(task.TaskId)) return;
            _tasks[task.TaskId] = task;
        }

        internal int BlockedAttempts(string taskId)
        {
            int n;
            return _blockedAttempts.TryGetValue(taskId ?? "", out n) ? n : 0;
        }

        // 该任务的真实写入次数（0 或 1）。与 BlockedAttempts 分开，二者不可互相冒充。
        internal int WritesOf(string taskId)
        {
            var t = Get(taskId);
            if (t == null) return 0;
            return t.WrittenAt >= 0.0 ? 1 : 0;
        }

        internal void NoteBlockedAttempt(string taskId)
        {
            if (string.IsNullOrEmpty(taskId)) return;
            int n;
            _blockedAttempts.TryGetValue(taskId, out n);
            _blockedAttempts[taskId] = n + 1;
        }

        internal void NoteWrite(string taskId)
        {
            _writeAttemptsTotal++;
            var t = Get(taskId);
            if (t != null && t.WrittenAt < 0.0) t.WrittenAt = TestParameters.Now;
        }

        // 同一编队是否已有未关闭任务（含退出任务）。
        internal FormationTask OpenTaskForFormation(string formationKey)
        {
            foreach (var t in _tasks.Values)
            {
                if (t.IsClosed) continue;
                if (string.Equals(t.FormationKey, formationKey, StringComparison.Ordinal)) return t;
            }
            return null;
        }

        // 收集需要处置的开放任务（按创建顺序稳定排序）。
        internal List<FormationTask> OpenTasks()
        {
            var list = new List<FormationTask>();
            foreach (var t in _tasks.Values)
            {
                if (!t.IsClosed) list.Add(t);
            }
            list.Sort(CompareByOrdinal);
            return list;
        }

        internal List<FormationTask> AllTasksSorted()
        {
            var list = new List<FormationTask>(_tasks.Values);
            list.Sort(CompareByIdentity);
            return list;
        }

        internal void CloseTask(string taskId, double now)
        {
            var t = Get(taskId);
            if (t == null || t.IsClosed) return;
            t.IsClosed = true;
            t.ClosedAt = now;
        }

        internal void ClearMission()
        {
            _tasks.Clear();
            _blockedAttempts.Clear();
            _writeAttemptsTotal = 0;
        }

        private static int CompareByOrdinal(FormationTask a, FormationTask b)
        {
            int c = string.CompareOrdinal(a.PlanId, b.PlanId);
            if (c != 0) return c;
            return a.Ordinal.CompareTo(b.Ordinal);
        }

        private static int CompareByIdentity(FormationTask a, FormationTask b)
        {
            return string.CompareOrdinal(a.TaskId, b.TaskId);
        }
    }
}
