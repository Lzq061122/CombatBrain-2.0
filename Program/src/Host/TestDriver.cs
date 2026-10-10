using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
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
        private readonly ThreadHandoff _handoff = new ThreadHandoff();
        private readonly OriginalMethodReceipt _receipt = new OriginalMethodReceipt();
        private Formation _receiptTarget;
        private readonly Dictionary<string, CbHoldBehavior> _armedBehaviors =
            new Dictionary<string, CbHoldBehavior>();
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

        // Route C 的获选信号：原生 FormationAI 把本方注册的行为选为 ActiveBehavior。
        // 判定条件是行为归属（引用相等），不是"有命令写到同一编队"。
        // 原生选中别的编队或别的行为都不算本方获选。
        internal void OnActiveBehaviorChanged(Mission mission, Formation f, int threadId)
        {
            if (_intent == null || _intent.IsClosed) return;

            var pending = _gate.PendingKey(_intent.IntentId);
            if (pending.Length == 0) return;

            int teamIndex = -1;
            try { if (f.Team != null) teamIndex = f.Team.TeamIndex; } catch (Exception) { }
            var keyNow = KeyOf(teamIndex, f);

            if (!string.Equals(keyNow, pending, StringComparison.Ordinal))
            {
                // 别的编队切行为是常态，只记前若干条，避免淹没日志。
                _otherSelectionCount++;
                if (_otherSelectionCount <= 20)
                {
                    Cb2Log.Line("ACTIVE_BEHAVIOR_CHANGED_OTHER",
                        "intent=" + _intent.IntentId +
                        " pending=" + pending +
                        " native=" + keyNow +
                        " tid=" + threadId +
                        " seq=" + _otherSelectionCount);
                }
                else if (_otherSelectionCount == 21)
                {
                    Cb2Log.Line("ACTIVE_BEHAVIOR_CHANGED_OTHER_SUPPRESSED", "further_entries_not_logged");
                }
                return;
            }

            bool ours = IsOurActiveBehavior(keyNow, f);
            Cb2Log.Line("ACTIVE_BEHAVIOR_CHANGED",
                "intent=" + _intent.IntentId +
                " key=" + keyNow +
                " active_behavior=" + FormationSampler.ActiveBehaviorNameOf(f) +
                " ours=" + (ours ? 1 : 0) +
                " tid=" + threadId);

            if (!ours) return;

            if (_intent.SelectedAt < 0)
                MarkRealSelection(mission, f);
        }

        // 原生把命令应用到编队：只做观测与归属记录，不在这里写令。
        // 写令由下一个 Mission tick 的执行门路径完成。
        internal void OnNativeMovementOrderApplied(Mission mission, Formation f,
                                                   MovementOrder.MovementOrderEnum orderEnum, int threadId)
        {
            if (_intent == null || _intent.IsClosed) return;

            var pending = _gate.PendingKey(_intent.IntentId);
            if (pending.Length == 0) return;

            int teamIndex = -1;
            try { if (f.Team != null) teamIndex = f.Team.TeamIndex; } catch (Exception) { }
            var keyNow = KeyOf(teamIndex, f);
            if (!string.Equals(keyNow, pending, StringComparison.Ordinal)) return;

            bool ours = IsOurActiveBehavior(keyNow, f);
            Cb2Log.Line("NATIVE_MOVEMENT_APPLIED",
                "intent=" + _intent.IntentId +
                " formation=" + FormationSampler.FormationClassOf(f) +
                " order_enum=" + orderEnum +
                " tid=" + threadId +
                " our_write=0" +
                " source=" + (ours ? "native_apply_of_our_behavior" : "not_ours"));
        }

        // 行为归属判定：原生当前行为必须就是本方注册的那个实例。
        private bool IsOurActiveBehavior(string key, Formation f)
        {
            CbHoldBehavior ours;
            if (!_armedBehaviors.TryGetValue(key, out ours) || ours == null) return false;
            try { return ReferenceEquals(f.AI.ActiveBehavior, ours); }
            catch (Exception) { return false; }
        }

        private bool HasArmedBehavior(string key)
        {
            CbHoldBehavior b;
            return _armedBehaviors.TryGetValue(key, out b) && b != null && b.Armed;
        }

        private string ActiveBehaviorOfBound(Mission mission, FormationRecord bound)
        {
            var f = FormationSampler.FindFormation(
                mission, bound.TeamIndex,
                bound.FormationIndex.ToString(CultureInfo.InvariantCulture));
            if (f == null) return "<formation_not_found>";
            return FormationSampler.ActiveBehaviorNameOf(f);
        }

        // 退出时只把本方行为的权重清零并解除武装。
        // 不用 ResetBehaviorWeights：那会抹掉原生在本方写入期间作出的新权重决策。
        private void DisarmAllBehaviors(string reason, double now)
        {
            foreach (var pair in _armedBehaviors)
            {
                var b = pair.Value;
                if (b == null) continue;
                string error = "";
                try
                {
                    var f = b.Formation;
                    b.Disarm();
                    if (f != null) f.AI.SetBehaviorWeight<CbHoldBehavior>(0f);
                }
                catch (Exception e) { error = e.GetType().Name + ": " + e.Message; }

                Cb2Log.Line("BEHAVIOR_DISARMED",
                    "key=" + pair.Key +
                    " reason=" + reason +
                    " weight=0" +
                    " error=" + (error.Length == 0 ? "<none>" : error) +
                    " at=" + Cb2Log.F(now));
            }
            _armedBehaviors.Clear();
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
            _handoff.Clear();
            _receiptTarget = null;
            DisarmAllBehaviors("MissionEnded", t);
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

                // 写令不在 Mission tick 里并列进行。Route C 下它由原生
                // FormationAI 在本方行为被选中后调用 TickOccasionally 应用，
                // 落在原生主循环内、受原生 IsAIControlled 门控。
                if (_intent.ExecutedAt < 0 && !_gate.HasWritten(_intent.IntentId))
                {
                    Cb2Log.Line("AWAITING_NATIVE_APPLY",
                        "intent=" + _intent.IntentId +
                        " key=" + _intent.BoundKey +
                        " obj=" + _intent.BoundObjectIdentity +
                        " armed_behavior=" + (HasArmedBehavior(_intent.BoundKey) ? 1 : 0) +
                        " active_behavior_now=" + ActiveBehaviorOfBound(mission, bound) +
                        " at=" + Cb2Log.F(now));
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

            // Route C：把本方行为注册进该 Formation 的原生行为选择链。
            // 这里只注册与设定权重，不调用任何 setter、不写 MovementOrder。
            var f = FormationSampler.FindFormation(
                mission,
                selection.Chosen.TeamIndex,
                selection.Chosen.FormationIndex.ToString(CultureInfo.InvariantCulture));
            if (f == null)
            {
                Cb2Log.Line("CANDIDATE_SUBMIT_FAILED",
                    "intent=" + _intent.IntentId +
                    " key=" + selection.Chosen.Key() +
                    " reason=formation_not_found");
                return false;
            }

            var behavior = ArmBehavior(mission, f, selection.Chosen.Key());
            if (behavior == null) return false;

            _gate.SubmitCandidate(_intent.IntentId, selection.Chosen.Key(), now);
            Cb2Log.Line("CANDIDATE_SUBMITTED",
                "intent=" + _intent.IntentId +
                " key=" + selection.Chosen.Key() +
                " obj=" + selection.Chosen.ObjectIdentity +
                " order_before=" + selection.Chosen.MovementOrder +
                " behavior_registered=1" +
                " behavior_weight=" + Cb2Log.F(TestParameters.HoldBehaviorWeight) +
                " at=" + Cb2Log.F(now) +
                " write_attempts=" + _gate.WriteAttempts);
            return true;
        }

        // 注册本方行为并设定权重。AddAiBehavior 不设 WeightFactor，
        // 必须显式设定非零值，否则原生 FindBestBehavior 会直接跳过。
        private CbHoldBehavior ArmBehavior(Mission mission, Formation f, string key)
        {
            CbHoldBehavior behavior = null;
            string error = "";
            try
            {
                var ai = f.AI;
                if (ai == null) throw new InvalidOperationException("formation_ai_is_null");

                behavior = ai.GetBehavior<CbHoldBehavior>();
                if (behavior == null)
                {
                    behavior = new CbHoldBehavior(f);
                    ai.AddAiBehavior(behavior);
                }
                behavior.Arm();
                ai.SetBehaviorWeight<CbHoldBehavior>((float)TestParameters.HoldBehaviorWeight);
            }
            catch (Exception e)
            {
                error = e.GetType().Name + ": " + e.Message;
                behavior = null;
            }

            if (behavior == null)
            {
                Cb2Log.Line("BEHAVIOR_REGISTER_FAILED",
                    "intent=" + (_intent != null ? _intent.IntentId : "<none>") +
                    " key=" + key +
                    " error=" + (error.Length == 0 ? "<none>" : error));
                return null;
            }

            _armedBehaviors[key] = behavior;

            Cb2Log.Line("BEHAVIOR_REGISTERED",
                "intent=" + _intent.IntentId +
                " key=" + key +
                " behavior=" + behavior.GetType().Name +
                " weight=" + Cb2Log.F(TestParameters.HoldBehaviorWeight) +
                " behavior_count=" + FormationSampler.BehaviorCountOf(f) +
                " active_behavior=" + FormationSampler.ActiveBehaviorNameOf(f));
            return behavior;
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

        // 原方法体入口观测：只在本方这次调用的武装窗口内、且对象就是本方目标时记入回执。
        internal void ObserveOriginalEnter(Formation f, int threadId)
        {
            if (_receiptTarget == null || f == null) return;
            if (!ReferenceEquals(_receiptTarget, f)) return;
            _receipt.ObserveEnter(threadId);
        }

        // 主线程执行点的登记来源：Mission tick。只登记第一次，之后不再改。
        internal void MarkMainThread(int threadId)
        {
            bool first = !_handoff.HasMainThread;
            _handoff.MarkMainThread(threadId);
            if (first)
            {
                Cb2Log.Line("MAIN_THREAD_MARKED",
                    "tid=" + threadId +
                    " source=mission_tick");
            }
        }

        // 原生选中本方行为后由 FormationAI 调 TickOccasionally 进入这里。
        // 原生主循环不等于主线程：只有线程号等于已登记的主线程号才允许就地写令，
        // 否则把本次这一条命令最小转交给 Mission tick，不在这里执行。
        internal void OnBehaviorTick(Formation f, int threadId)
        {
            if (f == null) return;
            var mission = Mission.Current;
            if (mission == null) return;
            if (_intent == null || _intent.IsClosed) return;
            if (_intent.SelectedAt < 0) return;

            if (_handoff.IsMainThread(threadId))
            {
                _handoff.NoteDirect();
                Cb2Log.Line("HANDOFF_DIRECT",
                    "intent=" + _intent.IntentId +
                    " key=" + _intent.BoundKey +
                    " tid=" + threadId +
                    " at=" + Cb2Log.F(SafeTime(mission)));
                TryExecuteStop(mission, f, threadId);
                return;
            }

            bool deferred = _handoff.TryDefer(
                _intent.IntentId, _intent.BoundKey, _intent.BoundObjectIdentity,
                threadId, SafeTime(mission));

            Cb2Log.Line(deferred ? "HANDOFF_DEFERRED" : "HANDOFF_SLOT_BUSY",
                "intent=" + _intent.IntentId +
                " key=" + _intent.BoundKey +
                " callback_tid=" + threadId +
                " main_tid=" + _handoff.MainThreadId +
                " at=" + Cb2Log.F(SafeTime(mission)));
        }

        // 主线程执行点：只消费转交槽位里那一条命令，不重放历史、不批量。
        internal void DrainPendingCommand(Mission mission)
        {
            if (mission == null) return;
            if (!_handoff.HasPending) return;

            var p = _handoff.TakePending();
            if (_intent == null || _intent.IsClosed) return;
            if (!string.Equals(p.IntentId, _intent.IntentId, StringComparison.Ordinal)) return;
            if (_intent.SelectedAt < 0) return;

            var f = FindByKey(mission, p.Key);
            if (f == null)
            {
                Cb2Log.Line("HANDOFF_DROPPED",
                    "intent=" + p.IntentId + " key=" + p.Key + " reason=formation_not_found");
                return;
            }

            int tid = Thread.CurrentThread.ManagedThreadId;
            Cb2Log.Line("HANDOFF_EXECUTED",
                "intent=" + p.IntentId +
                " key=" + p.Key +
                " obj=" + p.ObjectIdentity +
                " callback_tid=" + p.CallbackThreadId +
                " exec_tid=" + tid +
                " is_main_thread=" + (_handoff.IsMainThread(tid) ? 1 : 0) +
                " at=" + Cb2Log.F(SafeTime(mission)));

            TryExecuteStop(mission, f, tid);
        }

        private static Formation FindByKey(Mission mission, string key)
        {
            var parts = key.Split('/');
            if (parts.Length != 2) return null;
            int teamIndex;
            if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out teamIndex)) return null;
            return FormationSampler.FindFormation(mission, teamIndex, parts[1]);
        }

        // 执行站定。仅在真实获选且已落到主线程执行点之后调用一次；重复回调被执行门去重。
        internal void TryExecuteStop(Mission mission, Formation f, int execThreadId)
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

            // 执行前三项复核，独立于快照，全部即时求值：
            //  1) 对象身份仍是获选时绑定的那个；
            //  2) 目标仍由 AI 控制（Route C 的前提，不申请 Full）；
            //  3) 原生当前行为仍是本方注册的那个实例。
            string currentObjectIdentity = "";
            try { currentObjectIdentity = ObjectIdentityOf(f); } catch (Exception) { }

            bool aiControlled = false;
            try { aiControlled = f.IsAIControlled; } catch (Exception) { }

            bool behaviorIsOurs = IsOurActiveBehavior(key, f);

            var decision = _gate.RequestWrite(
                _intent.IntentId, key, now, missionValid, targetValid, playerOwned,
                currentObjectIdentity, aiControlled, behaviorIsOurs);

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

            // 原方法完成/跳过回执：setter 普通返回只能说明调用回来了，
            // 不能说明原方法体真的执行过。观测点是原方法体第一句的原生事件——
            // 它被跳过时不会触发。这里在调用前后夹住武装窗口，拿到的才是本次调用的回执。
            _receiptTarget = f;
            _receipt.Arm();

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

            bool originalEntered = _receipt.Disarm();
            int originalThreadId = _receipt.EnterThreadId;
            _receiptTarget = null;

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
                " route=native_behavior_apply" +
                " exec_tid=" + execThreadId +
                " is_main_thread=" + (_handoff.IsMainThread(execThreadId) ? 1 : 0) +
                " original_body_entered=" + (originalEntered ? 1 : 0) +
                " original_body_tid=" + (originalEntered ? originalThreadId.ToString(CultureInfo.InvariantCulture) : "-1") +
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
            DisarmAllBehaviors(reason.ToString(), now);
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
