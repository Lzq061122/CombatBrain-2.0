using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using CombatBrain2.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace CombatBrain2.Host
{
    // P1 最小指挥闭环的驱动器。执行层只做三件事：
    //  1) 按决策节奏从战场状态形成计划，把普通任务分给编队；
    //  2) 在真实获选与主线程执行点上按任务身份写令；
    //  3) 读回实际效果，条件失效或任务失败时让计划失效并撤权。
    // Route C：不申请 Full、不关闭原生 AI、不并行抢写。
    internal sealed class TestDriver
    {
        internal static TestDriver Instance = new TestDriver();

        private readonly ExecutionGate _gate = new ExecutionGate();
        private readonly WriteLedger _ledger = new WriteLedger();
        private readonly ThreadHandoff _handoff = new ThreadHandoff();
        private readonly OriginalMethodReceipt _receipt = new OriginalMethodReceipt();
        private readonly TaskLedger _tasks = new TaskLedger();
        private readonly PlanLedger _plans = new PlanLedger();

        private Formation _receiptTarget;
        private readonly Dictionary<string, CbHoldBehavior> _armedBehaviors =
            new Dictionary<string, CbHoldBehavior>();

        private FormationSnapshot _snapshot;
        private BattleState _state;
        private string _missionKey = "";
        private string _lastSampledMissionKey = "";

        private int _inputCount;
        private bool _loggedSelectionDetail;
        private int _otherSelectionCount;
        private double _lastSnapshotLogAt = -1.0;
        private double _lastSnapshotDetailAt = -1.0;
        private double _maxSnapshotAgeSeen;
        private int _staleSnapshotCount;
        private int _freshSnapshotCount;
        private bool _firstCommandSeen;
        private int _exitStopAttempts;
        // 上一条实际写令的 mission 时刻；用于编队间 1.5 s 间隔（脚本 §6.1）。
        private double _lastWriteAt = -1.0;

        internal ExecutionGate Gate { get { return _gate; } }
        internal WriteLedger Ledger { get { return _ledger; } }
        internal ThreadHandoff Handoff { get { return _handoff; } }
        internal TaskLedger Tasks { get { return _tasks; } }
        internal PlanLedger Plans { get { return _plans; } }
        internal BattlePlan Plan { get { return _plans.Current; } }
        internal BattleState State { get { return _state; } }
        internal FormationSnapshot Snapshot { get { return _snapshot; } }

        // 计划仍开放即视为还有工作；普通任务全部结束且无退出任务时为 false。
        internal bool HasOpenWork
        {
            get
            {
                if (_plans.HasPlan) return true;
                return _tasks.OpenTasks().Count > 0;
            }
        }

        internal bool AnyTaskWritten { get { return _gate.WriteAttempts > 0; } }

        // ================= 阶段一：决策节奏与计划形成 =================

        // 由宿主按决策节奏调用（不是每 tick）。形成或沿用计划，并生成任务。
        internal void DecisionTick(Mission mission)
        {
            if (mission == null) return;
            double now = SafeTime(mission);

            _snapshot = FormationSampler.Sample(mission, now);
            LogSnapshotHealth(now);

            if (!SnapshotFresh(now))
            {
                Cb2Log.Line("SNAPSHOT_REFUSED",
                    "use=decision sampled_at=" + Cb2Log.F(_snapshot.SampledAt) +
                    " age=" + Cb2Log.F(now - _snapshot.SampledAt) +
                    " limit=" + Cb2Log.F(TestParameters.SnapshotMaxAgeSeconds) +
                    " at=" + Cb2Log.F(now));
                return;
            }

            _inputCount++;
            var inputId = "input-" + _inputCount.ToString(CultureInfo.InvariantCulture);
            var state = BattleAssessment.Read(_snapshot, inputId);
            _state = state;

            if (state == null)
            {
                Cb2Log.Line("BATTLE_STATE_UNAVAILABLE",
                    "input=" + inputId + " at=" + Cb2Log.F(now) + " reason=enemy_team_not_found");
                return;
            }

            var conditions = PlanConditions.From(state);
            Cb2Log.Line("BATTLE_STATE",
                "input=" + inputId +
                " sampled_at=" + Cb2Log.F(state.SampledAt) +
                " my_team=" + state.MyTeamIndex +
                " enemy_team=" + state.EnemyTeamIndex +
                " my_units=" + state.MyUnitCount +
                " enemy_units=" + state.EnemyUnitCount +
                " my_ranged=" + state.MyRangedCount +
                " enemy_ranged=" + state.EnemyRangedCount +
                " my_infantry=" + state.MyInfantryCount +
                " enemy_infantry=" + state.EnemyInfantryCount +
                " my_center=(" + Cb2Log.F(state.MyCenterX) + "," + Cb2Log.F(state.MyCenterY) + ")" +
                " enemy_center=(" + Cb2Log.F(state.EnemyCenterX) + "," + Cb2Log.F(state.EnemyCenterY) + ")" +
                " " + conditions.Describe() +
                " at=" + Cb2Log.F(now));

            // 条件未变且计划仍开放：继续沿用，不重建、不重发令。
            if (_plans.CanContinue(conditions, now))
            {
                Cb2Log.Line("PLAN_CONTINUED",
                    "plan=" + _plans.Current.PlanId +
                    " input=" + inputId +
                    " age=" + Cb2Log.F(now - _plans.Current.CreatedAt) +
                    " at=" + Cb2Log.F(now));
                SweepExpiredSelections(now);
                return;
            }

            // 旧计划还开着但条件已变：先按实际原因让它失效。
            if (_plans.HasPlan)
            {
                Cb2Log.Line("PLAN_CHANGED",
                    "plan=" + _plans.Current.PlanId +
                    " input=" + inputId +
                    " old_goal=" + _plans.Current.Goal +
                    " old=" + _plans.Current.Conditions.Describe() +
                    " new=" + conditions.Describe() +
                    " at=" + Cb2Log.F(now));
                InvalidatePlan(PlanInvalidReason.ConditionChanged,
                    "condition_changed old=" + _plans.Current.Conditions.Describe() +
                    " new=" + conditions.Describe(), now);
            }

            var plan = _plans.Create(state, now);
            if (plan == null)
            {
                Cb2Log.Line("PLAN_UNAVAILABLE",
                    "input=" + inputId +
                    " reason=missing_controlled_class" +
                    " at=" + Cb2Log.F(now));
                return;
            }

            Cb2Log.Line("PLAN_CREATED",
                plan.Describe() +
                " " + plan.Conditions.Describe() +
                " tasks=" + plan.Tasks.Count +
                " at=" + Cb2Log.F(now));

            foreach (var t in plan.Tasks)
            {
                _tasks.Add(t);
                Cb2Log.Line("TASK_CREATED",
                    t.Describe() +
                    " reason=" + t.Reason +
                    " units=" + UnitCountOf(t.FormationKey) +
                    " origin=(" + Cb2Log.F(t.OriginX) + "," + Cb2Log.F(t.OriginY) + ")" +
                    " at=" + Cb2Log.F(now));
            }

            LogTaskCandidates(plan, now);
        }

        private void LogTaskCandidates(BattlePlan plan, double now)
        {
            if (_loggedSelectionDetail) return;
            _loggedSelectionDetail = true;
            var snapshot = _snapshot;
            if (snapshot == null) return;
            int eligible = 0;
            foreach (var r in snapshot.Records)
            {
                bool empty = r.UnitCount <= 0;
                bool playerOwned = r.IsPlayerTeam || r.IsPlayerControlled;
                bool notAi = !r.IsAiControlled;
                bool notControlled = !BattleAssessment.IsInfantry(r.FormationClass) &&
                                     !BattleAssessment.IsRanged(r.FormationClass);
                bool fresh = !r.IsStale(snapshot.SampledAt);
                if (!empty && !playerOwned && !notAi && !notControlled && fresh) eligible++;
                Cb2Log.Line("TASK_CANDIDATE",
                    "key=" + r.Key() +
                    " side=" + r.Side +
                    " player_team=" + (r.IsPlayerTeam ? 1 : 0) +
                    " player_ctrl=" + (r.IsPlayerControlled ? 1 : 0) +
                    " ai_ctrl=" + (r.IsAiControlled ? 1 : 0) +
                    " class=" + r.FormationClass +
                    " units=" + r.UnitCount +
                    " order=" + r.MovementOrder +
                    " in_control_scope=" + ((!empty && !playerOwned && !notAi && !notControlled && fresh) ? 1 : 0));
            }
            Cb2Log.Line("TASK_CANDIDATES",
                "plan=" + plan.PlanId +
                " controlled_scope=" + eligible +
                " sort_basis=ascending(TeamIndex,FormationIndex)" +
                " at=" + Cb2Log.F(now));
        }

        // ================= 阶段二：候选提交与真实获选 =================

        // 主线程调用：把尚无获选的任务挂到目标编队的原生行为选择链上。
        internal void SubmitPending(Mission mission)
        {
            if (mission == null) return;
            double now = SafeTime(mission);

            foreach (var task in _tasks.OpenTasks())
            {
                if (task.WrittenAt >= 0.0) continue;
                if (task.SelectedAt >= 0.0) continue;
                if (task.SelectionDeadline() <= now && task.SubmittedAt >= 0.0) continue;

                var f = FindByKey(mission, task.FormationKey);
                if (f == null) continue;

                // 执行前的即时资格核验：任务承担的编队必须仍在控制范围内。
                if (!EligibleForControl(f))
                {
                    var reason = EligibilityFailureOf(f);
                    Cb2Log.Line("TASK_ELIGIBILITY_LOST",
                        task.Describe() + " reason=" + reason + " at=" + Cb2Log.F(now));
                    InvalidatePlan(PlanInvalidReason.FormationEligibilityLost,
                        "task=" + task.TaskId + " reason=" + reason, now);
                    return;
                }

                var behavior = ArmBehavior(mission, f, task);
                if (behavior == null)
                {
                    InvalidatePlan(PlanInvalidReason.WriteFailed, "task=" + task.TaskId +
                        " reason=behavior_register_failed", now);
                    return;
                }

                task.SubmittedAt = now;
                _gate.SubmitCandidate(task.TaskId, task.FormationKey, now);
                Cb2Log.Line("TASK_CANDIDATE_SUBMITTED",
                    task.Describe() +
                    " behavior_registered=1" +
                    " behavior_weight=" + Cb2Log.F(TestParameters.HoldBehaviorWeight) +
                    " at=" + Cb2Log.F(now) +
                    " write_attempts_total=" + _gate.WriteAttempts +
                    " write_attempts_task=" + _tasks.WritesOf(task.TaskId));
            }
        }

        // 原生活动行为切换：只在这里确认"真实获选"，不在这里写令。
        internal void OnActiveBehaviorChanged(Mission mission, Formation f, int threadId)
        {
            if (f == null) return;

            int teamIndex = -1;
            try { if (f.Team != null) teamIndex = f.Team.TeamIndex; } catch (Exception) { }
            var keyNow = KeyOf(teamIndex, f);

            var task = PendingTaskForFormation(keyNow);
            if (task == null)
            {
                // 别的编队切行为是常态，只记前若干条，避免淹没日志。
                _otherSelectionCount++;
                if (_otherSelectionCount <= 20)
                {
                    Cb2Log.Line("ACTIVE_BEHAVIOR_CHANGED_OTHER",
                        "native=" + keyNow + " tid=" + threadId + " seq=" + _otherSelectionCount);
                }
                else if (_otherSelectionCount == 21)
                {
                    Cb2Log.Line("ACTIVE_BEHAVIOR_CHANGED_OTHER_SUPPRESSED", "further_entries_not_logged");
                }
                return;
            }

            bool ours = IsOurActiveBehavior(keyNow, f);
            Cb2Log.Line("ACTIVE_BEHAVIOR_CHANGED",
                "task=" + task.TaskId +
                " plan=" + task.PlanId +
                " key=" + keyNow +
                " active_behavior=" + FormationSampler.ActiveBehaviorNameOf(f) +
                " ours=" + (ours ? 1 : 0) +
                " tid=" + threadId +
                " at=" + Cb2Log.F(SafeTime(mission)));

            if (!ours) return;

            if (task.SelectedAt < 0.0) MarkRealSelection(mission, f, task);
        }

        // 原生把命令应用到编队：只做观测与归属记录，不在这里写令。
        internal void OnNativeMovementOrderApplied(Mission mission, Formation f,
                                                   MovementOrder.MovementOrderEnum orderEnum, int threadId)
        {
            if (f == null) return;
            int teamIndex = -1;
            try { if (f.Team != null) teamIndex = f.Team.TeamIndex; } catch (Exception) { }
            var keyNow = KeyOf(teamIndex, f);
            if (PendingTaskForFormation(keyNow) == null) return;

            bool ours = IsOurActiveBehavior(keyNow, f);
            Cb2Log.Line("NATIVE_MOVEMENT_APPLIED",
                "key=" + keyNow +
                " formation=" + FormationSampler.FormationClassOf(f) +
                " order_enum=" + orderEnum +
                " tid=" + threadId +
                " our_write=0" +
                " source=" + (ours ? "native_apply_of_our_behavior" : "not_ours"));
        }

        // 真实获选：唯一允许转入执行的状态转移。
        private void MarkRealSelection(Mission mission, Formation f, FormationTask task)
        {
            double now = SafeTime(mission);
            int teamIndex = -1;
            string side = "";
            try
            {
                var team = f.Team;
                if (team != null) { teamIndex = team.TeamIndex; side = team.Side.ToString(); }
            }
            catch (Exception) { }

            task.FormationKey = KeyOf(teamIndex, f);
            task.ObjectIdentity = ObjectIdentityOf(f);
            task.SelectedAt = now;

            _gate.MarkSelected(task.TaskId, task.FormationKey, now);
            _gate.BindObject(task.TaskId, task.ObjectIdentity);

            Cb2Log.Line("REAL_SELECTION",
                "task=" + task.TaskId +
                " plan=" + task.PlanId +
                " key=" + task.FormationKey +
                " side=" + side +
                " obj=" + task.ObjectIdentity +
                " at=" + Cb2Log.F(now));
        }

        // ================= 阶段三：主线程执行点 =================

        // 原生选中本方行为后由 FormationAI 调 TickOccasionally 进入这里。
        // 原生主循环不等于主线程：线程号等于已登记主线程号才允许就地写令。
        internal void OnBehaviorTick(Formation f, int threadId)
        {
            if (f == null) return;
            var mission = Mission.Current;
            if (mission == null) return;

            int teamIndex = -1;
            try { if (f.Team != null) teamIndex = f.Team.TeamIndex; } catch (Exception) { }
            var keyNow = KeyOf(teamIndex, f);
            var task = PendingTaskForFormation(keyNow);
            if (task == null || task.SelectedAt < 0.0) return;

            if (_handoff.IsMainThread(threadId))
            {
                _handoff.NoteDirect();
                Cb2Log.Line("HANDOFF_DIRECT",
                    "task=" + task.TaskId +
                    " key=" + keyNow +
                    " tid=" + threadId +
                    " at=" + Cb2Log.F(SafeTime(mission)));
                TryExecuteTask(mission, f, task, threadId);
                return;
            }

            bool deferred = _handoff.TryDefer(
                _missionKey, task.PlanId, task.TaskId, keyNow, task.ObjectIdentity,
                threadId, SafeTime(mission));

            Cb2Log.Line(deferred ? "HANDOFF_DEFERRED" : "HANDOFF_SLOT_BUSY",
                "task=" + task.TaskId +
                " key=" + keyNow +
                " callback_tid=" + threadId +
                " main_tid=" + _handoff.MainThreadId +
                " at=" + Cb2Log.F(SafeTime(mission)));
        }

        // 主线程执行点：消费待执行槽位。取出后仍要按当前任务与资格即时复核。
        internal void DrainPendingCommand(Mission mission)
        {
            if (mission == null) return;
            if (!_handoff.HasPending) return;

            var pending = _handoff.TakeAllPending();
            int tid = Thread.CurrentThread.ManagedThreadId;

            foreach (var p in pending)
            {
                var task = _tasks.Get(p.TaskId);
                if (task == null || task.IsClosed)
                {
                    Cb2Log.Line("HANDOFF_DROPPED",
                        "task=" + p.TaskId +
                        " key=" + p.Key +
                        " reason=" + (task == null ? "task_unknown" : "task_closed") +
                        " callback_tid=" + p.CallbackThreadId +
                        " exec_tid=" + tid +
                        " at=" + Cb2Log.F(SafeTime(mission)));
                    continue;
                }
                if (task.WrittenAt >= 0.0)
                {
                    Cb2Log.Line("HANDOFF_DROPPED",
                        "task=" + task.TaskId + " key=" + p.Key + " reason=already_written_at=" +
                        Cb2Log.F(task.WrittenAt));
                    continue;
                }
                if (task.SelectedAt < 0.0)
                {
                    Cb2Log.Line("HANDOFF_DROPPED",
                        "task=" + task.TaskId + " key=" + p.Key + " reason=not_selected");
                    continue;
                }
                // 计划已失效但任务尚未逐条关闭（关闭是同步的，这里只作兜底）。
                var plan = _plans.Current;
                if (plan != null && plan.IsClosed &&
                    string.Equals(plan.PlanId, task.PlanId, StringComparison.Ordinal))
                {
                    Cb2Log.Line("HANDOFF_DROPPED",
                        "task=" + task.TaskId + " key=" + p.Key + " reason=plan_closed");
                    continue;
                }

                var f = FindByKey(mission, p.Key);
                if (f == null)
                {
                    Cb2Log.Line("HANDOFF_DROPPED",
                        "task=" + task.TaskId + " key=" + p.Key + " reason=formation_not_found");
                    continue;
                }

                Cb2Log.Line("HANDOFF_EXECUTED",
                    "task=" + task.TaskId +
                    " key=" + p.Key +
                    " obj=" + p.ObjectIdentity +
                    " callback_tid=" + p.CallbackThreadId +
                    " exec_tid=" + tid +
                    " is_main_thread=" + (_handoff.IsMainThread(tid) ? 1 : 0) +
                    " at=" + Cb2Log.F(SafeTime(mission)));

                TryExecuteTask(mission, f, task, tid);
            }
        }

        // 执行一个任务的命令。写令前即时复核：任务、计划、资格、对象、本方行为。
        private void TryExecuteTask(Mission mission, Formation f, FormationTask task, int execThreadId)
        {
            if (task == null || f == null) return;
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

            string currentObjectIdentity = "";
            try { currentObjectIdentity = ObjectIdentityOf(f); } catch (Exception) { }

            bool aiControlled = false;
            try { aiControlled = f.IsAIControlled; } catch (Exception) { }

            bool behaviorIsOurs = IsOurActiveBehavior(key, f);

            // 普通任务在真实写令前保证：计划创建后首令延迟、以前一条实际写令为基准的 1.5 s 间隔。
            // 退出安全动作沿用既有例外，不受此约束。
            if (!BattlePlan.IsExitTask(task))
            {
                string defer = WriteDelayBlock(task, now);
                if (defer != null)
                {
                    Cb2Log.Line("WRITE_DELAYED",
                        "task=" + task.TaskId +
                        " plan=" + task.PlanId +
                        " key=" + key +
                        " reason=" + defer +
                        " at=" + Cb2Log.F(now));
                    return;
                }
            }

            var decision = _gate.RequestWrite(
                task.TaskId, key, now, missionValid, targetValid, playerOwned,
                currentObjectIdentity, aiControlled, behaviorIsOurs);

            if (!decision.Allowed)
            {
                _tasks.NoteBlockedAttempt(task.TaskId);
                Cb2Log.Line("WRITE_BLOCKED",
                    "task=" + task.TaskId +
                    " plan=" + task.PlanId +
                    " key=" + key +
                    " reason=" + decision.BlockReason +
                    " at=" + Cb2Log.F(now) +
                    " write_attempts_total=" + _gate.WriteAttempts +
                    " write_attempts_task=" + _tasks.WritesOf(task.TaskId));

                // 资格或对象层面的拒绝说明计划前提已破：立即让计划失效，
                // 不等下一次评价，也不保持"看起来成功"的假状态。
                var reason = FailureReasonOf(decision.BlockReason);
                if (reason != PlanInvalidReason.None)
                {
                    InvalidatePlan(reason, "task=" + task.TaskId + " block=" + decision.BlockReason, now);
                }
                return;
            }

            var order = OrderForTask(mission, f, task, now, out string geometryNote);
            if (order.OrderEnum == MovementOrder.MovementOrderEnum.Invalid)
            {
                _tasks.NoteBlockedAttempt(task.TaskId);
                Cb2Log.Line("TASK_POSITION_INVALID",
                    "task=" + task.TaskId + " key=" + key + " detail=" + geometryNote +
                    " at=" + Cb2Log.F(now));
                InvalidatePlan(PlanInvalidReason.WriteFailed,
                    "task=" + task.TaskId + " reason=position_invalid", now);
                return;
            }

            string before = "";
            try { before = OrderNameOf(f); } catch (Exception e) { before = "<err:" + e.GetType().Name + ">"; }

            task.OrderBefore = before;

            _receiptTarget = f;
            _receipt.Arm();

            bool invoked = false;
            string error = "";
            try
            {
                InOurWrite = true;
                f.SetMovementOrder(order);
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

            task.WrittenAt = now;
            task.OrderAfter = after;
            task.Feedback = TaskFeedbackState.Pending;
            task.ObjectIdentity = currentObjectIdentity;
            _tasks.NoteWrite(task.TaskId);
            _ledger.RecordOwnWrite(task.TaskId, key, task.ObjectIdentity, before, after, now);
            _lastWriteAt = now;

            double delay = FirstCommandDelay(task, now);
            Cb2Log.Line("WRITE_EXECUTED",
                task.Describe() +
                " invoked=" + (invoked ? 1 : 0) +
                " error=" + (error.Length == 0 ? "<none>" : error) +
                " route=native_behavior_apply" +
                " geometry=" + geometryNote +
                " exec_tid=" + execThreadId +
                " is_main_thread=" + (_handoff.IsMainThread(execThreadId) ? 1 : 0) +
                " original_body_entered=" + (originalEntered ? 1 : 0) +
                " original_body_tid=" + (originalEntered ? originalThreadId.ToString(CultureInfo.InvariantCulture) : "-1") +
                " at=" + Cb2Log.F(now) +
                " first_command_delay=" + Cb2Log.F(delay) +
                " write_attempts_total=" + _gate.WriteAttempts +
                " write_attempts_task=" + _tasks.WritesOf(task.TaskId));

            // 退出动作写完一次即收尾，不再留在开放任务里反复评估（脚本 §7.2）。
            if (BattlePlan.IsExitTask(task))
            {
                CloseTask(task, PlanInvalidReason.None, now);
                Cb2Log.Line("EXIT_STOP_DONE",
                    task.Describe() +
                    " origin_plan=" + task.PlanId +
                    " order_after=" + after +
                    " at=" + Cb2Log.F(now));
            }
        }

        // 返回非 null 表示本条写令尚未到可发时刻，值为延后原因。
        private string WriteDelayBlock(FormationTask task, double now)
        {
            var plan = _plans.Current;
            if (plan == null || !string.Equals(plan.PlanId, task.PlanId, StringComparison.Ordinal))
            {
                return null;
            }

            double sinceCreate = now - plan.CreatedAt;
            if (sinceCreate < TestParameters.FirstCommandDelaySeconds)
            {
                return "first_command_delay";
            }

            if (_lastWriteAt >= 0.0)
            {
                double sinceWrite = now - _lastWriteAt;
                if (sinceWrite < TestParameters.FormationStaggerSeconds)
                {
                    return "formation_stagger";
                }
            }
            return null;
        }

        private double FirstCommandDelay(FormationTask task, double now)
        {
            var plan = _plans.Current;
            if (plan == null || !string.Equals(plan.PlanId, task.PlanId, StringComparison.Ordinal)) return -1.0;
            if (now - plan.CreatedAt < TestParameters.FirstCommandDelaySeconds) return -1.0;
            if (!_firstCommandSeen)
            {
                _firstCommandSeen = true;
                Cb2Log.Line("FIRST_COMMAND_DELAY",
                    "plan=" + plan.PlanId +
                    " task=" + task.TaskId +
                    " delay=" + Cb2Log.F(now - plan.CreatedAt) +
                    " required=" + Cb2Log.F(TestParameters.FirstCommandDelaySeconds) +
                    " at=" + Cb2Log.F(now));
            }
            return now - plan.CreatedAt;
        }

        // 按任务类型构造真实命令。位置由发令前的当前快照几何算出，
        // 不伪造坐标；算不出有效目标点就不写令。
        private MovementOrder OrderForTask(Mission mission, Formation f, FormationTask task,
                                           double now, out string note)
        {
            note = "";
            switch (task.Kind)
            {
                case TaskKind.Hold:
                    note = "hold_stop";
                    return MovementOrder.MovementOrderStop;

                case TaskKind.Engage:
                {
                    var enemy = PickEnemyFormation(mission);
                    if (enemy == null)
                    {
                        note = "no_enemy_formation";
                        return MovementOrder.MovementOrderNull;
                    }
                    note = "charge_enemy_formation obj=" + ObjectIdentityOf(enemy);
                    return MovementOrder.MovementOrderCharge;
                }

                case TaskKind.Reposition:
                {
                    var state = _state;
                    if (state == null)
                    {
                        note = "no_battle_state";
                        return MovementOrder.MovementOrderNull;
                    }
                    double ux, uy;
                    if (!state.TryUnitDirection(out ux, out uy))
                    {
                        note = "zero_span_no_direction";
                        return MovementOrder.MovementOrderNull;
                    }
                    var record = _snapshot != null ? _snapshot.FindByKey(task.FormationKey) : null;
                    if (record == null)
                    {
                        note = "no_current_record";
                        return MovementOrder.MovementOrderNull;
                    }
                    double originX = record.PosX;
                    double originY = record.PosY;
                    double span;
                    double direction;
                    if (task.Reason == "ranged_advantage_advance")
                    {
                        double centerDistance = state.CenterDistance();
                        span = Math.Min(TestParameters.RangedApproachMaxMeters, centerDistance / 2.0);
                        direction = 1.0;
                    }
                    else
                    {
                        span = TestParameters.InfantryFallbackMeters;
                        direction = -1.0;
                    }
                    double targetX = originX + direction * ux * span;
                    double targetY = originY + direction * uy * span;
                    if (!BattleState.IsFinite(targetX) || !BattleState.IsFinite(targetY))
                    {
                        note = "non_finite_target";
                        return MovementOrder.MovementOrderNull;
                    }
                    task.SetTarget(originX, originY, targetX, targetY);
                    note = "move_to_requested=(" + Cb2Log.F(targetX) + "," + Cb2Log.F(targetY) + ")" +
                           " span=" + Cb2Log.F(span);
                    return MovementOrder.MovementOrderMove(new WorldPosition(mission.Scene, new Vec3((float)targetX, (float)targetY, 0f)));
                }

                default:
                    note = "task_kind_none";
                    return MovementOrder.MovementOrderNull;
            }
        }

        private Formation PickEnemyFormation(Mission mission)
        {
            var state = _state;
            if (state == null || _snapshot == null) return null;
            FormationRecord best = null;
            foreach (var r in state.EnemyFormations)
            {
                if (r.UnitCount <= 0) continue;
                if (best == null || r.UnitCount > best.UnitCount) best = r;
            }
            if (best == null) return null;
            return FindByKey(mission, best.Key());
        }

        // ================= 阶段四：效果反馈与任务收尾 =================

        // 每个已确认主线程的 Mission tick 都调用：读回实际效果、检查条件。
        internal void FeedbackTick(Mission mission)
        {
            if (mission == null) return;
            double now = SafeTime(mission);

            _snapshot = FormationSampler.Sample(mission, now);

            // 安全与条件检查逐 tick 做，不受决策节奏约束。
            CheckConditionsEachTick(now);

            if (!SnapshotFresh(now))
            {
                Cb2Log.Line("SNAPSHOT_REFUSED",
                    "use=feedback sampled_at=" + Cb2Log.F(_snapshot.SampledAt) +
                    " age=" + Cb2Log.F(now - _snapshot.SampledAt) +
                    " limit=" + Cb2Log.F(TestParameters.SnapshotMaxAgeSeconds) +
                    " at=" + Cb2Log.F(now));
                return;
            }

            foreach (var task in _tasks.OpenTasks())
            {
                if (task.WrittenAt < 0.0) continue;
                var rec = _snapshot.FindByKey(task.FormationKey);
                double enemyX = double.NaN;
                double enemyY = double.NaN;
                var enemyState = BattleAssessment.Read(_snapshot, _plans.Current != null
                    ? _plans.Current.PlanInputId
                    : "");
                if (enemyState != null)
                {
                    enemyX = enemyState.EnemyCenterX;
                    enemyY = enemyState.EnemyCenterY;
                }
                var result = TaskFeedback.Evaluate(task, rec, now, enemyX, enemyY);
                var previous = task.Feedback;
                task.Feedback = result.State;
                task.FailureReason = result.Reason;
                task.FailureDetail = result.Detail;

                if (result.State == TaskFeedbackState.Effective && previous != TaskFeedbackState.Effective)
                {
                    if (task.FeedbackAt < 0.0) task.FeedbackAt = now;
                    Cb2Log.Line("TASK_FEEDBACK",
                        task.Describe() +
                        " state=effective" +
                        " since_write=" + Cb2Log.F(now - task.WrittenAt) +
                        " detail=" + result.Detail +
                        " at=" + Cb2Log.F(now));
                }
                else if (result.State == TaskFeedbackState.Failed && previous != TaskFeedbackState.Failed)
                {
                    Cb2Log.Line("TASK_FEEDBACK",
                        task.Describe() +
                        " state=failed" +
                        " reason=" + result.Reason +
                        " since_write=" + Cb2Log.F(now - task.WrittenAt) +
                        " detail=" + result.Detail +
                        " at=" + Cb2Log.F(now));
                    InvalidatePlan(result.Reason,
                        "task=" + task.TaskId + " detail=" + result.Detail, now, task.PlanId);
                    return;
                }
            }
        }

        // 条件在每个 tick 复算：一旦不成立，立即让计划失效并处置任务。
        private void CheckConditionsEachTick(double now)
        {
            var plan = _plans.Current;
            if (plan == null || plan.IsClosed) return;

            // 资格检查先于快照检查：统计按全队保留失去 AI 资格的士兵，
            // 资格变化不会改变远程/兵力比例，必须独立检查已写任务。
            if (CheckPlanEligibilityEachTick(plan, now)) return;

            if (_snapshot == null) return;

            var state = BattleAssessment.Read(_snapshot, plan.PlanInputId);
            if (state == null) return;
            _state = state;

            var conditions = PlanConditions.From(state);
            if (conditions.SameAs(plan.Conditions)) return;

            Cb2Log.Line("PLAN_CONDITION_LOST",
                "plan=" + plan.PlanId +
                " old=" + plan.Conditions.Describe() +
                " new=" + conditions.Describe() +
                " at=" + Cb2Log.F(now));
            InvalidatePlan(PlanInvalidReason.ConditionChanged,
                "tick_condition_changed new=" + conditions.Describe(), now);
        }

        // 逐 tick 检查当前计划的承担编队是否仍有有效对象、非玩家、AI 受控（脚本 §7.1）。
        // 返回 true 表示已因资格失效关闭计划。
        private bool CheckPlanEligibilityEachTick(BattlePlan plan, double now)
        {
            foreach (var task in _tasks.OpenTasks())
            {
                if (BattlePlan.IsExitTask(task)) continue;
                if (task.PlanId != plan.PlanId) continue;
                var f = FindByKey(Mission.Current, task.FormationKey);
                if (f == null) continue;
                if (EligibleForControl(f)) continue;

                var reason = EligibilityFailureOf(f);
                Cb2Log.Line("TASK_ELIGIBILITY_LOST",
                    task.Describe() + " reason=" + reason +
                    " source=tick_eligibility_check" +
                    " at=" + Cb2Log.F(now));
                InvalidatePlan(PlanInvalidReason.FormationEligibilityLost,
                    "task=" + task.TaskId + " reason=" + reason, now);
                return true;
            }
            return false;
        }

        // 计划失效：一次性关闭本计划的全部普通任务、清本计划的待写命令、撤权；
        // 仍合格的编队另发一次独立退出 Stop。
        // sourcePlanId 指定失效归属；旧计划任务触发的失效不得取消新计划（脚本 §7.2）。
        internal void InvalidatePlan(PlanInvalidReason reason, string detail, double now)
        {
            InvalidatePlan(reason, detail, now, null);
        }

        internal void InvalidatePlan(PlanInvalidReason reason, string detail, double now, string sourcePlanId)
        {
            var plan = _plans.Current;
            if (plan == null || plan.IsClosed) return;

            if (!string.IsNullOrEmpty(sourcePlanId) &&
                !string.Equals(sourcePlanId, plan.PlanId, StringComparison.Ordinal))
            {
                Cb2Log.Line("PLAN_INVALIDATE_REJECTED",
                    "current=" + plan.PlanId +
                    " source_plan=" + sourcePlanId +
                    " reason=" + reason +
                    " note=stale_task_from_old_plan" +
                    " at=" + Cb2Log.F(now));
                return;
            }

            _plans.Invalidate(reason, detail, now);
            Cb2Log.Line("PLAN_INVALIDATED",
                "plan=" + plan.PlanId +
                " goal=" + plan.Goal +
                " reason=" + reason +
                " detail=" + detail +
                " at=" + Cb2Log.F(now));

            var open = _tasks.OpenTasks();
            foreach (var t in open)
            {
                if (BattlePlan.IsExitTask(t)) continue;
                if (!string.Equals(t.PlanId, plan.PlanId, StringComparison.Ordinal))
                {
                    // 旧计划的普通任务：按本次失效原因一并关闭，但不参与退出 Stop 排队。
                    CloseTask(t, reason, now);
                    Cb2Log.Line("TASK_CLOSED",
                        t.Describe() +
                        " reason=" + reason +
                        " written=" + (t.WrittenAt >= 0.0 ? 1 : 0) +
                        " feedback=" + t.Feedback +
                        " qualified_now=0" +
                        " note=task_belongs_to_other_plan" +
                        " at=" + Cb2Log.F(now));
                    continue;
                }
                var rec = _snapshot != null ? _snapshot.FindByKey(t.FormationKey) : null;
                bool qualified = rec != null && !likelyPlayerOwned(rec) && rec.IsAiControlled &&
                                 rec.UnitCount > 0;
                CloseTask(t, reason, now);
                Cb2Log.Line("TASK_CLOSED",
                    t.Describe() +
                    " reason=" + reason +
                    " written=" + (t.WrittenAt >= 0.0 ? 1 : 0) +
                    " feedback=" + t.Feedback +
                    " qualified_now=" + (qualified ? 1 : 0) +
                    " at=" + Cb2Log.F(now));

                if (qualified) TryQueueExitStop(t, now);
            }

            int hadPending = _handoff.HasPending ? 1 : 0;
            _handoff.Clear();
            Cb2Log.Line("HANDOFF_CLEARED",
                "plan=" + plan.PlanId +
                " reason=" + reason +
                " had_pending=" + hadPending +
                " at=" + Cb2Log.F(now));

            DisarmAllBehaviors(reason.ToString(), now);
            RevokeAllWritten(plan, now);
        }

        private void TryQueueExitStop(FormationTask closed, double now)
        {
            // 每编队最多一次退出尝试；本阶段不建重试框架。
            if (_exitStopAttempts >= 2) return;
            if (closed.ObjectIdentity.Length == 0) return;

            _exitStopAttempts++;
            var exit = new FormationTask
            {
                TaskId = closed.TaskId + "-exit",
                PlanId = closed.PlanId,
                Ordinal = 90 + _exitStopAttempts,
                FormationKey = closed.FormationKey,
                ObjectIdentity = closed.ObjectIdentity,
                FormationClass = closed.FormationClass,
                TeamIndex = closed.TeamIndex,
                FormationIndex = closed.FormationIndex,
                Kind = TaskKind.Hold,
                Reason = "exit_after_plan_invalidated origin_task=" + closed.TaskId,
                CreatedAt = now,
            };
            _tasks.Add(exit);
            Cb2Log.Line("EXIT_STOP_QUEUED",
                exit.Describe() +
                " origin_plan=" + closed.PlanId +
                " at=" + Cb2Log.F(now));
        }

        // 退出动作不因未获选就反复尝试：到点未获选即如实记录并撤权。
        // 安全时限从已投递时刻起算；已投递但未获选的退出任务不能因此被永久跳过（脚本 §7.2）。
        internal void SweepExitStops(Mission mission)
        {
            if (mission == null) return;
            double now = SafeTime(mission);
            foreach (var task in _tasks.OpenTasks())
            {
                if (!BattlePlan.IsExitTask(task)) continue;
                if (task.WrittenAt >= 0.0) continue;
                if (task.SelectedAt >= 0.0) continue;
                double since = task.SubmittedAt >= 0.0
                    ? now - task.SubmittedAt
                    : now - task.CreatedAt;
                if (since >= TestParameters.SafetyCancelLatencySeconds)
                {
                    _tasks.CloseTask(task.TaskId, now);
                    Cb2Log.Line("EXIT_STOP_NOT_APPLIED",
                        task.Describe() +
                        " reason=not_selected_in_time" +
                        " submitted=" + (task.SubmittedAt >= 0.0 ? 1 : 0) +
                        " waited=" + Cb2Log.F(since) +
                        " at=" + Cb2Log.F(now));
                    DisarmBehaviorsFor(task.FormationKey, "ExitNotApplied", now);
                }
            }
        }

        private void CloseTask(FormationTask task, PlanInvalidReason reason, double now)
        {
            task.IsClosed = true;
            task.ClosedAt = now;
            task.FailureReason = reason;
            _tasks.CloseTask(task.TaskId, now);
            _gate.Close(task.TaskId, CancelReasonFor(reason), now);
        }

        private static CancelReason CancelReasonFor(PlanInvalidReason reason)
        {
            switch (reason)
            {
                case PlanInvalidReason.ConditionChanged: return CancelReason.TargetInvalid;
                case PlanInvalidReason.SelectionTimeout: return CancelReason.SelectionTimeout;
                case PlanInvalidReason.WriteFailed: return CancelReason.TargetInvalid;
                case PlanInvalidReason.NotEffective: return CancelReason.TargetInvalid;
                case PlanInvalidReason.CommandOverridden: return CancelReason.TargetInvalid;
                case PlanInvalidReason.FormationEligibilityLost: return CancelReason.AiControlLost;
                case PlanInvalidReason.MissionEnded: return CancelReason.MissionEnded;
                default: return CancelReason.None;
            }
        }

        private static PlanInvalidReason FailureReasonOf(string blockReason)
        {
            switch (blockReason)
            {
                case "not_ai_controlled": return PlanInvalidReason.FormationEligibilityLost;
                case "player_owned": return PlanInvalidReason.FormationEligibilityLost;
                case "object_identity_changed": return PlanInvalidReason.FormationEligibilityLost;
                case "behavior_not_ours": return PlanInvalidReason.WriteFailed;
                case "target_invalid": return PlanInvalidReason.WriteFailed;
                case "mission_invalid": return PlanInvalidReason.MissionEnded;
                default: return PlanInvalidReason.None;
            }
        }

        // 选择超时：任务提交候选后到点仍未获选，计划失效。
        private void SweepExpiredSelections(double now)
        {
            foreach (var task in _tasks.OpenTasks())
            {
                if (task.SubmittedAt < 0.0) continue;
                if (task.SelectedAt >= 0.0) continue;
                if (now < task.SelectionDeadline()) continue;
                Cb2Log.Line("TASK_SELECTION_TIMEOUT",
                    task.Describe() +
                    " waited=" + Cb2Log.F(now - task.SubmittedAt) +
                    " limit=" + Cb2Log.F(TestParameters.SelectionWaitSeconds) +
                    " at=" + Cb2Log.F(now));
                InvalidatePlan(PlanInvalidReason.SelectionTimeout,
                    "task=" + task.TaskId + " selection_timeout", now);
                return;
            }
        }

        // ================= 行为注册与撤权 =================

        private CbHoldBehavior ArmBehavior(Mission mission, Formation f, FormationTask task)
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
                    "task=" + task.TaskId +
                    " key=" + task.FormationKey +
                    " error=" + (error.Length == 0 ? "<none>" : error));
                return null;
            }

            _armedBehaviors[task.FormationKey] = behavior;

            Cb2Log.Line("BEHAVIOR_REGISTERED",
                "task=" + task.TaskId +
                " key=" + task.FormationKey +
                " behavior=" + behavior.GetType().Name +
                " weight=" + Cb2Log.F(TestParameters.HoldBehaviorWeight) +
                " behavior_count=" + FormationSampler.BehaviorCountOf(f) +
                " active_behavior=" + FormationSampler.ActiveBehaviorNameOf(f));
            return behavior;
        }

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


        // 退出时只把本方行为的权重清零并解除武装。
        // 不用 ResetBehaviorWeights：那会抹掉原生在本方写入期间作出的新权重决策。
        private void DisarmAllBehaviors(string reason, double now)
        {
            var keys = new List<string>(_armedBehaviors.Keys);
            foreach (var key in keys) DisarmBehavior(key, reason, now);
        }

        private void DisarmBehaviorsFor(string key, string reason, double now)
        {
            DisarmBehavior(key, reason, now);
        }

        private void DisarmBehavior(string key, string reason, double now)
        {
            CbHoldBehavior b;
            if (!_armedBehaviors.TryGetValue(key, out b)) return;
            _armedBehaviors.Remove(key);
            if (b == null) return;

            var f = b.Formation;
            float weightBefore = -1f;
            float weightAfter = -1f;
            int armedBefore = b.Armed ? 1 : 0;
            string behaviorNow = "<none>";
            string error = "";
            try
            {
                try { weightBefore = b.WeightFactor; } catch (Exception) { }
                b.Disarm();
                if (f != null)
                {
                    f.AI.SetBehaviorWeight<CbHoldBehavior>(0f);
                    try
                    {
                        weightAfter = ((CbHoldBehavior)f.AI.GetBehavior<CbHoldBehavior>()).WeightFactor;
                    }
                    catch (Exception) { }
                    try
                    {
                        var active = f.AI.ActiveBehavior;
                        behaviorNow = active == null ? "<none>" : active.GetType().Name;
                    }
                    catch (Exception) { }
                }
            }
            catch (Exception e) { error = e.GetType().Name + ": " + e.Message; }

            Cb2Log.Line("BEHAVIOR_DISARMED",
                "key=" + key +
                " reason=" + reason +
                " armed_before=" + armedBefore +
                " weight_before=" + Cb2Log.F(weightBefore) +
                " weight_after=" + Cb2Log.F(weightAfter) +
                " active_behavior_now=" + behaviorNow +
                " error=" + (error.Length == 0 ? "<none>" : error) +
                " at=" + Cb2Log.F(now));
        }

        // 只撤销本方确实改变且仍由本方负责的状态；只撤本计划写下的令，
        // 不追撤其他计划的写入（脚本 §7.2）。
        private void RevokeAllWritten(BattlePlan plan, double now)
        {
            foreach (var task in _tasks.AllTasksSorted())
            {
                if (task.WrittenAt < 0.0) continue;
                if (plan != null && !string.Equals(task.PlanId, plan.PlanId, StringComparison.Ordinal)) continue;
                RevokeTask(task, now);
            }
        }

        private void RevokeTask(FormationTask task, double now)
        {
            var plan = _ledger.PlanRevocation(task.TaskId);
            if (!plan.Needed)
            {
                Cb2Log.Line("REVOKE_SKIPPED",
                    "task=" + task.TaskId + " reason=" + plan.SkipReason);
                return;
            }

            var mission = Mission.Current;
            var f = FindByPlan(mission, plan.Key);
            if (f == null)
            {
                Cb2Log.Line("REVOKE_SKIPPED", "task=" + task.TaskId + " reason=target_gone");
                return;
            }

            var currentObj = ObjectIdentityOf(f);
            var guarded = _ledger.PlanRevocationIfSameObject(task.TaskId, currentObj);
            if (!guarded.Needed)
            {
                Cb2Log.Line("REVOKE_SKIPPED",
                    "task=" + task.TaskId + " reason=" + guarded.SkipReason);
                return;
            }

            string before = "";
            try { before = OrderNameOf(f); } catch (Exception) { }

            // 只有当前实际值仍等于本方写入的值时才回退，
            // 避免覆盖原生在本方写入之后作出的新决策。
            if (!string.Equals(before, guarded.OurOrder, StringComparison.Ordinal))
            {
                Cb2Log.Line("REVOKE_SKIPPED",
                    "task=" + task.TaskId +
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
                "task=" + task.TaskId +
                " key=" + guarded.Key +
                " ok=" + (ok ? 1 : 0) +
                " error=" + (err.Length == 0 ? "<none>" : err) +
                " order_before_revoke=" + before +
                " order_after_revoke=" + after +
                " at=" + Cb2Log.F(now));
        }

        // ================= 资格判定 =================

        private static bool EligibleForControl(Formation f)
        {
            try
            {
                if (f == null) return false;
                if (f.CountOfUnits <= 0) return false;
                if (f.Team != null && f.Team.IsPlayerTeam) return false;
                if (f.PlayerOwner != null) return false;
                return f.IsAIControlled;
            }
            catch (Exception) { return false; }
        }

        private static string EligibilityFailureOf(Formation f)
        {
            try
            {
                if (f == null) return "formation_null";
                if (f.CountOfUnits <= 0) return "empty";
                if (f.Team != null && f.Team.IsPlayerTeam) return "player_team";
                if (f.PlayerOwner != null) return "player_owner";
                if (!f.IsAIControlled) return "ai_control_lost";
            }
            catch (Exception e) { return "probe_error:" + e.GetType().Name; }
            return "eligible";
        }

        private static bool likelyPlayerOwned(FormationRecord r)
        {
            return r.IsPlayerTeam || r.IsPlayerControlled;
        }

        private FormationTask PendingTaskForFormation(string key)
        {
            var task = _tasks.OpenTaskForFormation(key);
            if (task == null || task.IsClosed) return null;
            if (task.WrittenAt >= 0.0) return null;
            return task;
        }

        private int UnitCountOf(string key)
        {
            if (_snapshot == null) return -1;
            var r = _snapshot.FindByKey(key);
            return r == null ? -1 : r.UnitCount;
        }

        // ================= 观测与生命周期 =================

        internal void ObserveOriginalEnter(Formation f, int threadId)
        {
            if (_receiptTarget == null || f == null) return;
            if (!ReferenceEquals(_receiptTarget, f)) return;
            _receipt.ObserveEnter(threadId);
        }

        internal void MarkMainThread(int threadId)
        {
            bool first = !_handoff.HasMainThread;
            _handoff.MarkMainThread(threadId);
            if (first)
            {
                Cb2Log.Line("MAIN_THREAD_MARKED", "tid=" + threadId + " source=mission_tick");
            }
        }

        internal static double CurrentMissionTime()
        {
            var m = Mission.Current;
            if (m == null) return 0.0;
            return SafeTime(m);
        }

        internal static volatile bool InOurWrite;

        internal void OnMissionStart(Mission mission)
        {
            _missionKey = FormationSampler.MissionKeyOf(mission);
            _lastSampledMissionKey = _missionKey;
            _loggedSelectionDetail = false;
            _lastSnapshotLogAt = -1.0;
            _lastSnapshotDetailAt = -1.0;
            _maxSnapshotAgeSeen = 0.0;
            _staleSnapshotCount = 0;
            _freshSnapshotCount = 0;
            _firstCommandSeen = false;
            _exitStopAttempts = 0;
            _lastWriteAt = -1.0;
            Cb2Log.Line("MISSION_START", "mission_key=" + _missionKey);
        }

        internal void OnMissionEnd()
        {
            var t = CurrentMissionTime();

            var plan = _plans.Current;
            if (plan != null && !plan.IsClosed)
            {
                Cb2Log.Line("PLAN_INVALIDATED",
                    "plan=" + plan.PlanId + " goal=" + plan.Goal +
                    " reason=" + PlanInvalidReason.MissionEnded +
                    " detail=mission_ended at=" + Cb2Log.F(t));
            }

            foreach (var task in _tasks.OpenTasks())
            {
                CloseTask(task, PlanInvalidReason.MissionEnded, t);
                Cb2Log.Line("TASK_CLOSED",
                    task.Describe() + " reason=MissionEnded at=" + Cb2Log.F(t));
            }

            _gate.ClearMission();
            _ledger.ClearMission();
            _handoff.Clear();
            _tasks.ClearMission();
            _plans.ClearMission();
            _receiptTarget = null;
            DisarmAllBehaviors("MissionEnded", t);
            _snapshot = null;
            _state = null;
            Cb2Log.Line("MISSION_CLEANUP", "plan_task_and_object_state_cleared");
        }

        // 快照新鲜度：年龄一律用实际采样时刻与实际 Mission 时间算。
        // 超过上限的快照不得用于生成任务或判断效果，不是仅记日志。
        private bool SnapshotFresh(double now)
        {
            if (_snapshot == null) return false;
            return (now - _snapshot.SampledAt) <= TestParameters.SnapshotMaxAgeSeconds;
        }

        // 快照健康度：年龄一律用实际采样时刻算，不允许用刷新时刻冒充。
        internal void LogSnapshotHealth(double now)
        {
            if (_snapshot == null) return;

            double age = now - _snapshot.SampledAt;
            if (age > _maxSnapshotAgeSeen) _maxSnapshotAgeSeen = age;
            if (age > TestParameters.SnapshotMaxAgeSeconds) _staleSnapshotCount++;
            else _freshSnapshotCount++;

            if (_lastSnapshotLogAt > 0.0 && now - _lastSnapshotLogAt < 1.0) return;
            _lastSnapshotLogAt = now;

            string dupDetail;
            bool hasDup = _snapshot.HasDuplicateKeys(out dupDetail);
            double maxAge = _maxSnapshotAgeSeen;
            _maxSnapshotAgeSeen = 0.0;

            Cb2Log.Line("SNAPSHOT_HEALTH",
                "sampled_at=" + Cb2Log.F(_snapshot.SampledAt) +
                " age=" + Cb2Log.F(age) +
                " max_age_seen=" + Cb2Log.F(maxAge) +
                " max_age_limit=" + Cb2Log.F(TestParameters.SnapshotMaxAgeSeconds) +
                " over_limit=" + (maxAge > TestParameters.SnapshotMaxAgeSeconds ? 1 : 0) +
                " refused_stale_total=" + _staleSnapshotCount +
                " accepted_fresh_total=" + _freshSnapshotCount +
                " records=" + _snapshot.CountNonEmpty() +
                " duplicate_keys=" + (hasDup ? 1 : 0) +
                " duplicate_detail=" + (hasDup ? dupDetail : "<none>"));

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

        // 独立读取实际 MovementOrder，供反馈窗口外的交叉核对。
        internal void ObservePostState(Mission mission)
        {
            if (mission == null) return;
            var snapshot = FormationSampler.Sample(mission, SafeTime(mission));
            var plan = _plans.Current;
            if (plan == null) return;

            foreach (var task in plan.Tasks)
            {
                if (task.WrittenAt < 0.0) continue;
                var rec = snapshot.FindByKey(task.FormationKey);
                if (rec == null)
                {
                    Cb2Log.Line("POST_OBSERVE", "task=" + task.TaskId + " target_gone");
                    continue;
                }
                bool sameObject = string.Equals(rec.ObjectIdentity, task.ObjectIdentity, StringComparison.Ordinal);
                Cb2Log.Line("POST_OBSERVE",
                    "task=" + task.TaskId +
                    " key=" + rec.Key() +
                    " order=" + rec.MovementOrder +
                    " same_object=" + (sameObject ? 1 : 0) +
                    " obj_now=" + rec.ObjectIdentity +
                    " feedback=" + task.Feedback +
                    " at=" + Cb2Log.F(snapshot.SampledAt));
            }
        }

        // ================= 查找与转换 =================

        private static Formation FindByKey(Mission mission, string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            var parts = key.Split('/');
            if (parts.Length != 2) return null;
            int teamIndex;
            if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out teamIndex)) return null;
            return FormationSampler.FindFormation(mission, teamIndex, parts[1]);
        }

        private static Formation FindByPlan(Mission mission, string key)
        {
            return FindByKey(mission, key);
        }

        internal double SafeNow(Mission mission)
        {
            return SafeTime(mission);
        }

        internal Formation FindFormationByKey(Mission mission, string key)
        {
            return FindByKey(mission, key);
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
