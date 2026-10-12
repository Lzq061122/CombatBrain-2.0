using System;
using System.Collections.Generic;

namespace CombatBrain2.Core
{
    internal sealed class WriteDecision
    {
        internal bool Allowed;
        internal string BlockReason = "";
    }

    // 写令门：执行前即时复核。每个任务用独立状态键，
    // 一个任务写过一次不消耗另一支编队的写入额度。
    internal sealed class ExecutionGate
    {
        private readonly Dictionary<string, IntentState> _states =
            new Dictionary<string, IntentState>(StringComparer.Ordinal);

        // 只统计真正允许落地的写入；同一状态键重复放行不重复计数。
        private int _writeAttempts;

        internal int WriteAttempts { get { return _writeAttempts; } }

        private sealed class IntentState
        {
            internal string Key = "";
            internal string ObjectIdentity = "";
            internal double SubmittedAt = -1.0;
            internal double SelectedAt = -1.0;
            internal bool Selected;
            internal bool Written;
            internal bool Closed;
            internal CancelReason CloseReason = CancelReason.None;
        }

        internal void SubmitCandidate(string taskId, string key, double missionTime)
        {
            var s = GetOrCreate(taskId);
            s.Key = key ?? "";
            s.SubmittedAt = missionTime;
        }

        internal void MarkSelected(string taskId, string key, double missionTime)
        {
            var s = GetOrCreate(taskId);
            s.Key = key ?? "";
            s.SelectedAt = missionTime;
            s.Selected = true;
        }

        internal void BindObject(string taskId, string objectIdentity)
        {
            var s = GetOrCreate(taskId);
            s.ObjectIdentity = objectIdentity ?? "";
        }

        // 执行前复核。逐条拒绝都写明原因，供生产路径按事实取消。
        internal WriteDecision RequestWrite(string taskId, string key, double missionTime,
                                            bool missionValid, bool targetValid, bool playerOwned,
                                            string currentObjectIdentity, bool aiControlled,
                                            bool behaviorIsOurs)
        {
            var d = new WriteDecision();

            IntentState s;
            if (string.IsNullOrEmpty(taskId) || !_states.TryGetValue(taskId, out s))
            {
                d.BlockReason = "no_intent";
                return d;
            }
            if (s.Closed)
            {
                d.BlockReason = "intent_closed";
                return d;
            }
            if (!s.Selected)
            {
                d.BlockReason = "not_selected";
                return d;
            }
            if (s.Written)
            {
                d.BlockReason = "already_written";
                return d;
            }
            if (!string.Equals(s.Key, key, StringComparison.Ordinal))
            {
                d.BlockReason = "different_object";
                return d;
            }
            if (s.ObjectIdentity.Length > 0 &&
                !string.Equals(s.ObjectIdentity, currentObjectIdentity, StringComparison.Ordinal))
            {
                d.BlockReason = "object_identity_changed";
                return d;
            }
            if (!aiControlled)
            {
                d.BlockReason = "not_ai_controlled";
                return d;
            }
            if (!behaviorIsOurs)
            {
                d.BlockReason = "behavior_not_ours";
                return d;
            }
            if (!missionValid)
            {
                d.BlockReason = "mission_invalid";
                return d;
            }
            if (!targetValid)
            {
                d.BlockReason = "target_invalid";
                return d;
            }
            if (playerOwned)
            {
                d.BlockReason = "player_owned";
                return d;
            }

            s.Written = true;
            _writeAttempts++;
            d.Allowed = true;
            return d;
        }

        internal void Close(string taskId, CancelReason reason, double missionTime)
        {
            var s = GetOrCreate(taskId);
            s.Closed = true;
            s.CloseReason = reason;
        }

        internal bool IsSelected(string taskId)
        {
            IntentState s;
            return !string.IsNullOrEmpty(taskId) && _states.TryGetValue(taskId, out s) && s.Selected;
        }

        internal string PendingKey(string taskId)
        {
            IntentState s;
            if (!string.IsNullOrEmpty(taskId) && _states.TryGetValue(taskId, out s)) return s.Key;
            return "";
        }

        internal bool HasWritten(string taskId)
        {
            IntentState s;
            return !string.IsNullOrEmpty(taskId) && _states.TryGetValue(taskId, out s) && s.Written;
        }

        // 该任务身份是否已被放行写过（0/1）。按任务独立计数，不与其他编队共享额度。
        internal int WriteAttemptsFor(string taskId)
        {
            return HasWritten(taskId) ? 1 : 0;
        }

        internal void ClearMission()
        {
            _states.Clear();
            _writeAttempts = 0;
        }

        private IntentState GetOrCreate(string taskId)
        {
            IntentState s;
            if (!_states.TryGetValue(taskId ?? "", out s))
            {
                s = new IntentState();
                _states[taskId ?? ""] = s;
            }
            return s;
        }
    }
}
