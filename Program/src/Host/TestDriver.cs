using System;
using System.Collections.Generic;
using System.Globalization;
using CombatBrain2.Core;
using TaleWorlds.MountAndBlade;

namespace CombatBrain2.Host
{
    // 测试驱动器：把本批的测试意图落到真实宿主上。
    // Route C：不申请 Full、不关闭原生 AI、不并行抢写。
    internal sealed class TestDriver
    {
        internal static TestDriver Instance = new TestDriver();

        private readonly ExecutionGate _gate = new ExecutionGate();
        private readonly WriteLedger _ledger = new WriteLedger();
        private TestIntent _intent;
        private FormationSnapshot _snapshot;
        private string _missionKey = "";
        private int _triggerCount;
        private int _selectionEvaluations;
        private bool _loggedSelectionDetail;
        private int _otherSelectionCount;
        private double _lastSnapshotLogAt = -1.0;
        private double _lastSnapshotDetailAt = -1.0;
        private double _prevSampleAt = -1.0;
        private double _maxSnapshotAgeSeen;
        private double _minSampleInterval = -1.0;
        private double _maxSampleInterval;

        internal ExecutionGate Gate { get { return _gate; } }
        internal WriteLedger Ledger { get { return _ledger; } }
        internal TestIntent Intent { get { return _intent; } }
        internal FormationSnapshot Snapshot { get { return _snapshot; } }

        internal bool HasOpenIntent { get { return _intent != null && !_intent.IsClosed; } }
        internal bool Selected { get { return _intent != null && _intent.SelectedAt >= 0; } }
        internal bool Executed { get { return _intent != null && _intent.ExecutedAt >= 0; } }

        // 原生真实应用了移动命令：这是 Route C 的获选信号来源。
        // 只有本次测试意图已提交候选、且原生选中的正是候选对象时，
        // 才转入执行阶段。原生选别的编队不算本方获选。
        internal void OnNativeSelected(Mission mission, Formation f,
                                       MovementOrder.MovementOrderEnum orderEnum, int threadId)
        {
            if (_intent == null || _intent.IsClosed) return;

            var pending = _gate.PendingKey(_intent.IntentId);
            if (pending.Length == 0) return;

            int teamIndex = -1;
            try { if (f.Team != null) teamIndex = f.Team.TeamIndex; } catch (Exception) { }
            var keyNow = KeyOf(teamIndex, f);

            if (!string.Equals(keyNow, pending, StringComparison.Ordinal))
            {
                // 原生对别的编队写令是常态，只记前若干条，避免淹没日志。
                _otherSelectionCount++;
                if (_otherSelectionCount <= 20)
                {
                    Cb2Log.Line("NATIVE_SELECTION_OTHER",
                        "intent=" + _intent.IntentId +
                        " pending=" + pending +
                        " native=" + keyNow +
                        " order_enum=" + orderEnum +
                        " tid=" + threadId +
                        " seq=" + _otherSelectionCount);
                }
                else if (_otherSelectionCount == 21)
                {
                    Cb2Log.Line("NATIVE_SELECTION_OTHER_SUPPRESSED", "further_entries_not_logged");
                }
                return;
            }

            Cb2Log.Line("NATIVE_MOVEMENT_APPLIED",
                "intent=" + _intent.IntentId +
                " formation=" + FormationSampler.FormationClassOf(f) +
                " order_enum=" + orderEnum +
                " tid=" + threadId +
                " our_write=0" +
                " source=not_ours");

            if (_intent.SelectedAt < 0)
                MarkRealSelection(mission, f);
            // 实际写令不在本回调内进行：这是原生 setter 的 pre-apply 时点，
            // 嵌套写令会被外层原生写入立即覆盖。改由下一个 Mission tick 执行。
        }

        // 标记本方自身的写入，避免把本方的 setter 调用误判为"原生获选"。
        internal static volatile bool InOurWrite;

        internal static double CurrentMissionTime()
        {
            var m = Mission.Current;
            if (m == null) return 0.0;
            return SafeTime(m);
        }

        internal void OnMissionStart(Mission mission)
        {
            _missionKey = FormationSampler.MissionKeyOf(mission);
            _loggedSelectionDetail = false;
            _lastSnapshotLogAt = -1.0;
            _lastSnapshotDetailAt = -1.0;
            _prevSampleAt = -1.0;
            _maxSnapshotAgeSeen = 0.0;
            _minSampleInterval = -1.0;
            _maxSampleInterval = 0.0;
            Cb2Log.Line("MISSION_START", "mission_key=" + _missionKey);
        }

        internal void OnMissionEnd()
        {
            var t = CurrentMissionTime();
            if (_intent != null && !_intent.IsClosed)
            {
                _intent.IsClosed = true;
                _intent.CancelReason = CancelReason.MissionEnded;
                _intent.CancelledAt = t;
                _gate.Close(_intent.IntentId, CancelReason.MissionEnded, t);
                Cb2Log.Line("INTENT_CANCELLED",
                    "intent=" + _intent.IntentId + " reason=MissionEnded at=" + Cb2Log.F(t));
            }

            // Mission 结束清掉本方意图与对象状态。
            _gate.ClearMission();
            _ledger.ClearMission();
            _snapshot = null;
            _intent = null;
            Cb2Log.Line("MISSION_CLEANUP", "intent_and_object_state_cleared");
        }

        // 由宿主 Tick 驱动。写令只在本方法内、且已确认主线程时发生。
        internal void Tick(Mission mission)
        {
            if (mission == null) return;
            double now = SafeTime(mission);

            // 快照刷新（只读）。
            _snapshot = FormationSampler.Sample(mission, now);
            LogSnapshotHealth(now);

            if (_intent == null) return;

            // 到期：截止点固定，不因回调延长。
            if (!_intent.IsClosed && now >= _intent.Deadline)
            {
                CancelIntent(CancelReason.WindowExpired, now);
                return;
            }
            if (_intent.IsClosed) return;

            // 获选等待上限：自触发时刻起算，超时取消且不自动重触发。
            if (_intent.SelectedAt < 0)
            {
                if (now >= _intent.SelectionDeadline())
                {
                    CancelIntent(CancelReason.SelectionTimeout, now);
                    return;
                }
            }

            // 目标失效或资格变化：取消，不换另一支继续同一意图。
            if (_intent.SelectedAt >= 0 && _intent.BoundKey.Length > 0)
            {
                var bound = _snapshot.FindByKey(_intent.BoundKey);
                if (bound == null)
                {
                    CancelIntent(CancelReason.TargetInvalid, now);
                    return;
                }
                if (likelyPlayerOwned(bound))
                {
                    CancelIntent(CancelReason.PlayerEligibilityLost, now);
                    return;
                }

                // 已获选但尚未写令：在获选后的下一个 tick 执行。
                if (_intent.ExecutedAt < 0 && !_gate.HasWritten(_intent.IntentId))
                {
                    var f = FormationSampler.FindFormation(
                        mission, bound.TeamIndex,
                        bound.FormationIndex.ToString(CultureInfo.InvariantCulture));
                    if (f != null) TryExecuteStop(mission, f);
                }
            }
        }

        // 快照健康度：年龄一律用实际采样时刻算，不允许用刷新时刻冒充。
        // 采样每 tick 都做；这里累计时效统计并按 1 Hz 落盘。
        private void LogSnapshotHealth(double now)
        {
            if (_snapshot == null) return;

            // 实际刷新间隔逐 tick 统计（两次采样之间的 Mission 时间差）。
            double interval = _prevSampleAt >= 0.0 ? _snapshot.SampledAt - _prevSampleAt : -1.0;
            _prevSampleAt = _snapshot.SampledAt;
            if (interval >= 0.0)
            {
                if (_minSampleInterval < 0.0 || interval < _minSampleInterval) _minSampleInterval = interval;
                if (interval > _maxSampleInterval) _maxSampleInterval = interval;
            }

            double age = now - _snapshot.SampledAt;
            if (age > _maxSnapshotAgeSeen) _maxSnapshotAgeSeen = age;

            if (_lastSnapshotLogAt > 0.0 && now - _lastSnapshotLogAt < 1.0) return;
            _lastSnapshotLogAt = now;

            string dupDetail;
            bool hasDup = _snapshot.HasDuplicateKeys(out dupDetail);
            double maxAge = _maxSnapshotAgeSeen;
            double minInterval = _minSampleInterval;
            double maxInterval = _maxSampleInterval;
            _maxSnapshotAgeSeen = 0.0;
            _minSampleInterval = -1.0;
            _maxSampleInterval = 0.0;

            Cb2Log.Line("SNAPSHOT_HEALTH",
                "sampled_at=" + Cb2Log.F(_snapshot.SampledAt) +
                " age=" + Cb2Log.F(age) +
                " max_age_seen=" + Cb2Log.F(maxAge) +
                " max_age_limit=" + Cb2Log.F(TestParameters.SnapshotMaxAgeSeconds) +
                " over_limit=" + (maxAge > TestParameters.SnapshotMaxAgeSeconds ? 1 : 0) +
                " refresh_interval_min=" + Cb2Log.F(minInterval) +
                " refresh_interval_max=" + Cb2Log.F(maxInterval) +
                " records=" + _snapshot.CountNonEmpty() +
                " duplicate_keys=" + (hasDup ? 1 : 0) +
                " duplicate_detail=" + (hasDup ? dupDetail : "<none>"));

            // 每 30 s 落一次双方编队明细：敌我归属、人数、位置、当前移动命令。
            // 这是 2.0 自身的读取路径，用于与 CBATH 的独立观测对照。
            if (_lastSnapshotDetailAt > 0.0 && now - _lastSnapshotDetailAt < 30.0) return;
            _lastSnapshotDetailAt = now;
            foreach (var r in _snapshot.Records)
            {
                Cb2Log.Line("SNAPSHOT_FORMATION",
                    "key=" + r.Key() +
                    " side=" + r.Side +
                    " player_team=" + (r.IsPlayerTeam ? 1 : 0) +
                    " player_ctrl=" + (r.IsPlayerControlled ? 1 : 0) +
                    " ai_ctrl=" + (r.IsAiControlled ? 1 : 0) +
                    " class=" + r.FormationClass +
                    " units=" + r.UnitCount +
                    " pos=(" + Cb2Log.F(r.PosX) + "," + Cb2Log.F(r.PosY) + ")" +
                    " order=" + r.MovementOrder +
                    " obj=" + r.ObjectIdentity +
                    " age=" + Cb2Log.F(age));
            }
        }

        private static bool likelyPlayerOwned(FormationRecord r)
        {
            return r.IsPlayerTeam || r.IsPlayerControlled;
        }

        // 显式触发测试意图。真实获选由原生侧回调确认后才调用 MarkSelected。
        // 返回是否真的开启了一个新意图。
        internal bool TriggerIntent(Mission mission)
        {
            if (mission == null) return false;
            double now = SafeTime(mission);
            if (_intent != null && !_intent.IsClosed)
            {
                Cb2Log.Line("INTENT_TRIGGER_SKIPPED", "an_open_intent_exists");
                return false;
            }

            _triggerCount++;
            _loggedSelectionDetail = false;
            var id = "intent-" + _triggerCount.ToString(CultureInfo.InvariantCulture);
            _intent = new TestIntent
            {
                IntentId = id,
                TriggeredAt = now,
                Deadline = now + TestParameters.IntentWindowSeconds,
            };

            Cb2Log.Line("INTENT_TRIGGERED",
                "intent=" + id +
                " at=" + Cb2Log.F(now) +
                " deadline=" + Cb2Log.F(_intent.Deadline) +
                " window=" + Cb2Log.F(TestParameters.IntentWindowSeconds) +
                " selection_wait=" + Cb2Log.F(TestParameters.SelectionWaitSeconds));
            return true;
        }

        // 候选阶段：只生成命令，绝不调用 setter。
        // 返回是否已提交候选；写入尝试计数在获选前必须保持为 0。
        internal bool SubmitCandidateOnly(Mission mission)
        {
            if (_intent == null || _intent.IsClosed) return false;

            var snapshot = _snapshot ?? FormationSampler.Sample(mission, SafeTime(mission));
            double now = SafeTime(mission);

            var selection = TargetSelector.Select(snapshot, now);
            _selectionEvaluations++;

            if (!_loggedSelectionDetail)
            {
                _loggedSelectionDetail = true;
                Cb2Log.Line("SELECTION_CANDIDATES",
                    "intent=" + _intent.IntentId +
                    " sort_basis=" + selection.SortBasis +
                    " candidates=" + selection.Candidates.Count +
                    " eligible=" + (selection.HasChoice() ? selection.Chosen.Key() : "<none>"));
                foreach (var c in selection.Candidates)
                    Cb2Log.Line("SELECTION_CANDIDATE", c);
                foreach (var rj in selection.Rejections)
                    Cb2Log.Line("SELECTION_REJECTED", rj);
            }

            if (!selection.HasChoice()) return false;

            _gate.SubmitCandidate(_intent.IntentId, selection.Chosen.Key(), now);
            Cb2Log.Line("CANDIDATE_SUBMITTED",
                "intent=" + _intent.IntentId +
                " key=" + selection.Chosen.Key() +
                " obj=" + selection.Chosen.ObjectIdentity +
                " order_before=" + selection.Chosen.MovementOrder +
                " at=" + Cb2Log.F(now) +
                " write_attempts=" + _gate.WriteAttempts);
            return true;
        }

        // 原生真实获选回调后调用。这是唯一允许转入执行的状态转移。
        internal void MarkRealSelection(Mission mission, Formation f)
        {
            if (_intent == null || _intent.IsClosed || f == null) return;
            double now = SafeTime(mission);
            int teamIndex = -1;
            string side = "";
            try
            {
                var team = f.Team;
                if (team != null) { teamIndex = team.TeamIndex; side = team.Side.ToString(); }
            }
            catch (Exception) { }

            var key = KeyOf(teamIndex, f);
            var obj = ObjectIdentityOf(f);

            _intent.BoundKey = key;
            _intent.BoundObjectIdentity = obj;
            _intent.SelectedAt = now;

            _gate.MarkSelected(_intent.IntentId, key, now);
            _gate.BindObject(_intent.IntentId, obj);

            Cb2Log.Line("REAL_SELECTION",
                "intent=" + _intent.IntentId +
                " key=" + key +
                " side=" + side +
                " obj=" + obj +
                " at=" + Cb2Log.F(now));
        }

        // 执行站定。仅在真实获选之后调用一次；重复回调被执行门去重。
        internal void TryExecuteStop(Mission mission, Formation f)
        {
            if (_intent == null || f == null) return;
            double now = SafeTime(mission);

            int teamIndex = -1;
            try { if (f.Team != null) teamIndex = f.Team.TeamIndex; } catch (Exception) { }
            var key = KeyOf(teamIndex, f);

            bool missionValid = mission != null && ReferenceEquals(mission, Mission.Current);
            bool targetValid = false;
            try { targetValid = f.CountOfUnits > 0; } catch (Exception) { }
            bool playerOwned = false;
            try { playerOwned = f.Team != null && f.Team.IsPlayerTeam; } catch (Exception) { }
            try { if (f.PlayerOwner != null) playerOwned = true; } catch (Exception) { }

            var decision = _gate.RequestWrite(
                _intent.IntentId, key, now, missionValid, targetValid, playerOwned);

            if (!decision.Allowed)
            {
                Cb2Log.Line("WRITE_BLOCKED",
                    "intent=" + _intent.IntentId +
                    " key=" + key +
                    " reason=" + decision.BlockReason +
                    " at=" + Cb2Log.F(now) +
                    " write_attempts=" + _gate.WriteAttempts);
                return;
            }

            string before = "";
            try { before = OrderNameOf(f); } catch (Exception e) { before = "<err:" + e.GetType().Name + ">"; }

            bool invoked = false;
            string error = "";
            try
            {
                InOurWrite = true;
                f.SetMovementOrder(MovementOrder.MovementOrderStop);
                invoked = true;
            }
            catch (Exception e)
            {
                error = e.GetType().Name + ": " + e.Message;
            }
            finally
            {
                InOurWrite = false;
            }

            string after = "";
            try { after = OrderNameOf(f); } catch (Exception e) { after = "<err:" + e.GetType().Name + ">"; }

            _intent.ExecutedAt = now;
            _ledger.RecordOwnWrite(_intent.IntentId, key, _intent.BoundObjectIdentity, before, after, now);

            Cb2Log.Line("WRITE_EXECUTED",
                "intent=" + _intent.IntentId +
                " key=" + key +
                " obj=" + _intent.BoundObjectIdentity +
                " invoked=" + (invoked ? 1 : 0) +
                " error=" + (error.Length == 0 ? "<none>" : error) +
                " order_before=" + before +
                " order_after=" + after +
                " at=" + Cb2Log.F(now) +
                " write_attempts=" + _gate.WriteAttempts);
        }

        // 独立读取实际 MovementOrder，判断本次变化是否被后续原生改写。
        internal void ObservePostState(Mission mission)
        {
            if (_intent == null) return;
            if (_intent.BoundKey.Length == 0) return;
            var snapshot = FormationSampler.Sample(mission, SafeTime(mission));
            var rec = snapshot.FindByKey(_intent.BoundKey);
            if (rec == null)
            {
                Cb2Log.Line("POST_OBSERVE", "intent=" + _intent.IntentId + " target_gone");
                return;
            }
            bool sameObject = string.Equals(rec.ObjectIdentity, _intent.BoundObjectIdentity, StringComparison.Ordinal);
            Cb2Log.Line("POST_OBSERVE",
                "intent=" + _intent.IntentId +
                " key=" + rec.Key() +
                " order=" + rec.MovementOrder +
                " same_object=" + (sameObject ? 1 : 0) +
                " obj_now=" + rec.ObjectIdentity +
                " at=" + Cb2Log.F(snapshot.SampledAt));
        }

        internal void CancelIntent(CancelReason reason, double now)
        {
            if (_intent == null || _intent.IsClosed) return;
            _intent.IsClosed = true;
            _intent.CancelReason = reason;
            _intent.CancelledAt = now;
            _gate.Close(_intent.IntentId, reason, now);
            Cb2Log.Line("INTENT_CANCELLED",
                "intent=" + _intent.IntentId +
                " reason=" + reason +
                " at=" + Cb2Log.F(now) +
                " write_attempts=" + _gate.WriteAttempts);
            RevokeIfNeeded(now);
        }

        internal void ExplicitClose(Mission mission)
        {
            CancelIntent(CancelReason.ExplicitClose, SafeTime(mission));
        }

        // 只撤销本方确实改变且仍由本方负责的状态。
        private void RevokeIfNeeded(double now)
        {
            var plan = _ledger.PlanRevocation(_intent.IntentId);
            if (!plan.Needed)
            {
                Cb2Log.Line("REVOKE_SKIPPED",
                    "intent=" + _intent.IntentId + " reason=" + plan.SkipReason);
                return;
            }

            var mission = Mission.Current;
            var f = FindByPlan(mission, plan.Key);
            if (f == null)
            {
                Cb2Log.Line("REVOKE_SKIPPED", "intent=" + _intent.IntentId + " reason=target_gone");
                return;
            }

            // 对象身份变了就不覆盖新对象。
            var currentObj = ObjectIdentityOf(f);
            var guarded = _ledger.PlanRevocationIfSameObject(_intent.IntentId, currentObj);
            if (!guarded.Needed)
            {
                Cb2Log.Line("REVOKE_SKIPPED",
                    "intent=" + _intent.IntentId + " reason=" + guarded.SkipReason);
                return;
            }

            string before = "";
            try { before = OrderNameOf(f); } catch (Exception) { }

            // 只有当前实际值仍等于本方写入的值时才回退，
            // 避免覆盖原生在本方写入之后作出的新决策。
            if (!string.Equals(before, guarded.OurOrder, StringComparison.Ordinal))
            {
                Cb2Log.Line("REVOKE_SKIPPED",
                    "intent=" + _intent.IntentId +
                    " reason=native_rewrote_after_us order_now=" + before +
                    " our_order=" + guarded.OurOrder);
                return;
            }

            bool ok = false;
            string err = "";
            try
            {
                InOurWrite = true;
                f.SetMovementOrder(OrderFromName(guarded.PreviousOrder));
                ok = true;
            }
            catch (Exception e) { err = e.GetType().Name + ": " + e.Message; }
            finally { InOurWrite = false; }

            string after = "";
            try { after = OrderNameOf(f); } catch (Exception) { }

            Cb2Log.Line("REVOKE_EXECUTED",
                "intent=" + _intent.IntentId +
                " key=" + guarded.Key +
                " ok=" + (ok ? 1 : 0) +
                " error=" + (err.Length == 0 ? "<none>" : err) +
                " order_before_revoke=" + before +
                " order_after_revoke=" + after +
                " at=" + Cb2Log.F(now));
        }

        private static Formation FindByPlan(Mission mission, string key)
        {
            var parts = key.Split('/');
            if (parts.Length != 2) return null;
            int teamIndex;
            if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out teamIndex)) return null;
            return FormationSampler.FindFormation(mission, teamIndex, parts[1]);
        }

        private static string KeyOf(int teamIndex, Formation f)
        {
            return teamIndex.ToString(CultureInfo.InvariantCulture) + "/" +
                   FormationSampler.FormationKeyOf(f);
        }

        private static string ObjectIdentityOf(Formation f)
        {
            return RuntimeHelpers.GetHashCode(f).ToString("x8", CultureInfo.InvariantCulture) +
                   "@" + FormationSampler.FormationClassOf(f);
        }

        // 实际生效的移动命令类型。读的是 OrderController 的真实当前值，
        // 不是本方记录，也不是缓存。
        private static string OrderNameOf(Formation f)
        {
            try { return ActiveOrderOf(f).ToString(); }
            catch (Exception e) { return "<err:" + e.GetType().Name + ">"; }
        }

        private static OrderType ActiveOrderOf(Formation f)
        {
            return OrderController.GetActiveMovementOrderOf(f);
        }

        private static MovementOrder OrderFromName(string name)
        {
            if (string.IsNullOrEmpty(name)) return MovementOrder.MovementOrderStop;
            if (name.IndexOf("StandYourGround", StringComparison.OrdinalIgnoreCase) >= 0)
                return MovementOrder.MovementOrderStop;
            if (name.IndexOf("Advance", StringComparison.OrdinalIgnoreCase) >= 0)
                return MovementOrder.MovementOrderAdvance;
            if (name.IndexOf("FallBack", StringComparison.OrdinalIgnoreCase) >= 0)
                return MovementOrder.MovementOrderFallBack;
            if (name.IndexOf("Charge", StringComparison.OrdinalIgnoreCase) >= 0)
                return MovementOrder.MovementOrderCharge;
            return MovementOrder.MovementOrderStop;
        }

        private static double SafeTime(Mission mission)
        {
            try { return (double)mission.CurrentTime; } catch (Exception) { return 0.0; }
        }
    }
}
