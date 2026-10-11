using System;
using System.Collections.Generic;

namespace CombatBrain2.Core
{
    internal sealed class WriteDecision
    {
        internal bool Allowed;
        internal string BlockReason = "";
    }

    // 执行门。本批的核心约束集中在这里：
    //  - 候选阶段只生成命令，绝不调用 setter；
    //  - 只有被原生真实选中之后才允许写入一次；
    //  - 同一意图/激活的重复回调去重；
    //  - 写入前用实时条件复检，不依赖陈旧快照。
    internal sealed class ExecutionGate
    {
        private readonly Dictionary<string, IntentState> _states = new Dictionary<string, IntentState>();
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
            internal int WriteAttempts;
        }

        internal void SubmitCandidate(string intentId, string key, double missionTime)
        {
            var s = GetOrCreate(intentId);
            s.Key = key;
            s.SubmittedAt = missionTime;
        }

        internal void MarkSelected(string intentId, string key, double missionTime)
        {
            var s = GetOrCreate(intentId);
            s.Key = key;
            s.SelectedAt = missionTime;
            s.Selected = true;
        }

        internal void BindObject(string intentId, string objectIdentity)
        {
            GetOrCreate(intentId).ObjectIdentity = objectIdentity;
        }

        // 实时条件由调用方传入。执行前三项复核（对象身份、AI 受控、行为归属）
        // 独立于快照，全部在写入前即时求值，不允许用获选时的旧值顶替。
        internal WriteDecision RequestWrite(
            string intentId, string key, double missionTime,
            bool missionValid, bool targetValid, bool playerOwned,
            string currentObjectIdentity, bool aiControlled, bool behaviorIsOurs)
        {
            var d = new WriteDecision();
            IntentState s;
            if (!_states.TryGetValue(intentId, out s))
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
            if (s.Key != key)
            {
                d.BlockReason = "different_object";
                return d;
            }
            if (s.ObjectIdentity.Length > 0 && s.ObjectIdentity != currentObjectIdentity)
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
            s.WriteAttempts++;
            d.Allowed = true;
            return d;
        }

        // 按意图查询写入次数：全局累加会掩盖"某个具体意图是否又写了一次"。
        internal int WriteAttemptsFor(string intentId)
        {
            IntentState state;
            if (_states.TryGetValue(intentId, out state) && state.WriteAttempts > 0) return state.WriteAttempts;
            return 0;
        }

        internal void Close(string intentId, CancelReason reason, double missionTime)
        {
            var s = GetOrCreate(intentId);
            s.Closed = true;
            s.CloseReason = reason;
        }

        internal bool IsSelected(string intentId)
        {
            IntentState s;
            return _states.TryGetValue(intentId, out s) && s.Selected;
        }

        // 已提交但尚未获选的候选对象键。原生选中别的编队时用它做比对。
        internal string PendingKey(string intentId)
        {
            IntentState s;
            if (!_states.TryGetValue(intentId, out s)) return "";
            return s.Key;
        }

        internal bool HasWritten(string intentId)
        {
            IntentState s;
            return _states.TryGetValue(intentId, out s) && s.Written;
        }

        internal void ClearMission()
        {
            _states.Clear();
        }

        private IntentState GetOrCreate(string intentId)
        {
            IntentState s;
            if (!_states.TryGetValue(intentId, out s))
            {
                s = new IntentState();
                _states[intentId] = s;
            }
            return s;
        }
    }
}
