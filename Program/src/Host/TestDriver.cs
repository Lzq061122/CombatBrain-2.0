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
        private readonly ExitStopLedger _exits = new ExitStopLedger();
        // P2 协同状态机：只在 P2 开关下绑定，负责三职责依赖与阶段转换。
        private readonly CoordinationDriver _coordinationDriver = new CoordinationDriver();

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
        private double _lastCoordinationLogAt = -1.0;
        private double _maxSnapshotAgeSeen;
        private int _staleSnapshotCount;
        private int _freshSnapshotCount;
        private bool _firstCommandSeen;
        private int _exitStopAttempts;
        // 上一条实际写令的 mission 时刻；用于编队间 1.5 s 间隔（脚本 §6.1）。
        private double _lastWriteAt = -1.0;

        // A5 竞争注入：首次注入时刻、已覆盖的评价点轮数、窗口满足标记（派单 §9.2 要求 ≥8 s）。
        private double _competitionFirstAt = -1.0;
        private int _competitionRounds;
        private bool _competitionWindowLogged;

        internal ExecutionGate Gate { get { return _gate; } }
        internal WriteLedger Ledger { get { return _ledger; } }
        internal ThreadHandoff Handoff { get { return _handoff; } }
        internal TaskLedger Tasks { get { return _tasks; } }
        internal PlanLedger Plans { get { return _plans; } }
        internal BattlePlan Plan { get { return _plans.Current; } }
        internal BattleState State { get { return _state; } }
        internal FormationSnapshot Snapshot { get { return _snapshot; } }
        // P2 协作状态：供测试适配读取实际保护职责与依赖就绪状态（只读）。
        internal CoordinationDriver Coordination { get { return _coordinationDriver; } }

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
        // 计划种类由 P1/P2 开关决定，两者互斥（宿主侧保证不会同时为真）。
        private bool _coordination;

        internal void SetCommandLoop(bool coordination)
        {
            _coordination = coordination;
        }

        internal bool CoordinationMode
        {
            get { return _coordination; }
        }

        internal void DecisionTick(Mission mission)
        {
            if (mission == null) return;
            double now = SafeTime(mission);

            ObservationAdapter.Resolve(now);

            _snapshot = FormationSampler.Sample(mission, now);
            LogSnapshotHealth(now);

            if (!SnapshotFresh(now))
            {
                Cb2Log.Line("SNAPSHOT_REFUSED",
                    "use=decision sampled_at=" + Cb2Log.F(_snapshot.SampledAt) +
                    " age=" + Cb2Log.F(now - _snapshot.SampledAt) +
                    " limit=" + Cb2Log.F(TestParameters.SnapshotMaxAgeSeconds) +
                    " at=" + Cb2Log.F(now));
                ObservationAdapter.Emit("input.refused", now,
                    new EventFields()
                        .Add("reason", "stale_snapshot")
                        .Add("sampled_at", _snapshot.SampledAt)
                        .Add("age", now - _snapshot.SampledAt)
                        .ToString());
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
                ObservationAdapter.Emit("input.unavailable", now,
                    new EventFields()
                        .Add("input_id", inputId)
                        .Add("reason", "enemy_team_not_found")
                        .ToString());
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

            ObservationAdapter.Emit("input.read", now,
                new EventFields()
                    .Add("input_id", inputId)
                    .Add("sampled_at", state.SampledAt)
                    .Add("my_team", state.MyTeamIndex)
                    .Add("enemy_team", state.EnemyTeamIndex)
                    .Add("my_units", state.MyUnitCount)
                    .Add("enemy_units", state.EnemyUnitCount)
                    .Add("my_ranged", state.MyRangedCount)
                    .Add("enemy_ranged", state.EnemyRangedCount)
                    .Add("my_infantry", state.MyInfantryCount)
                    .Add("enemy_infantry", state.EnemyInfantryCount)
                    .Add("my_center_x", state.MyCenterX)
                    .Add("my_center_y", state.MyCenterY)
                    .Add("enemy_center_x", state.EnemyCenterX)
                    .Add("enemy_center_y", state.EnemyCenterY)
                    .ToString());

            // 条件未变且计划仍开放：继续沿用，不重建、不重发令。
            if (_plans.CanContinue(conditions, now))
            {
                Cb2Log.Line("PLAN_CONTINUED",
                    "plan=" + _plans.Current.PlanId +
                    " input=" + inputId +
                    " age=" + Cb2Log.F(now - _plans.Current.CreatedAt) +
                    " at=" + Cb2Log.F(now));
                ObservationAdapter.Emit("plan.continued", now,
                    new EventFields()
                        .Add("plan_id", _plans.Current.PlanId)
                        .Add("input_id", inputId)
                        .Add("age", now - _plans.Current.CreatedAt)
                        .ToString());
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
                ObservationAdapter.Emit("plan.changed", now,
                    new EventFields()
                        .Add("plan_id", _plans.Current.PlanId)
                        .Add("input_id", inputId)
                        .Add("old_goal", _plans.Current.Goal)
                        .Add("old_conditions", _plans.Current.Conditions.Describe())
                        .Add("new_conditions", conditions.Describe())
                        .ToString());
                InvalidatePlan(PlanInvalidReason.ConditionChanged,
                    "condition_changed old=" + _plans.Current.Conditions.Describe() +
                    " new=" + conditions.Describe(), now);
            }

            var plan = _coordination ? _plans.CreateCoordination(state, now) : _plans.Create(state, now);
            if (plan == null)
            {
                Cb2Log.Line("PLAN_UNAVAILABLE",
                    "input=" + inputId +
                    " reason=missing_controlled_class" +
                    " at=" + Cb2Log.F(now));
                ObservationAdapter.Emit("plan.unavailable", now,
                    new EventFields()
                        .Add("input_id", inputId)
                        .Add("reason", "missing_controlled_class")
                        .ToString());
                return;
            }

            Cb2Log.Line("PLAN_CREATED",
                plan.Describe() +
                " " + plan.Conditions.Describe() +
                " tasks=" + plan.Tasks.Count +
                " at=" + Cb2Log.F(now));

            ObservationAdapter.Emit("plan.created", now,
                new EventFields()
                    .Add("plan_id", plan.PlanId)
                    .Add("input_id", inputId)
                    .Add("goal", plan.Goal)
                    .Add("conditions", plan.Conditions.Describe())
                    .Add("task_count", plan.Tasks.Count)
                    .ToString());

            foreach (var t in plan.Tasks)
            {
                _tasks.Add(t);

                // 分配公布点：任务已带对象身份（MakeTask 从快照复制），此处起它接管该对象，
                // 该对象上的旧退出任务不得再处理其状态。不能等真实获选——
                // 公布到获选之间旧退出仍会作用于该对象。
                PublishAssignment(t, now);

                Cb2Log.Line("TASK_CREATED",
                    t.Describe() +
                    " reason=" + t.Reason +
                    " units=" + UnitCountOf(t.FormationKey) +
                    " origin=(" + Cb2Log.F(t.OriginX) + "," + Cb2Log.F(t.OriginY) + ")" +
                    " at=" + Cb2Log.F(now));

                ObservationAdapter.Emit("task.created", now,
                    new EventFields()
                        .Add("task_id", t.TaskId)
                        .Add("plan_id", plan.PlanId)
                        .Add("formation_key", t.FormationKey)
                        .Add("reason", t.Reason)
                        .Add("unit_count", UnitCountOf(t.FormationKey))
                        .Add("origin_x", t.OriginX)
                        .Add("origin_y", t.OriginY)
                        .ToString());
            }

            LogTaskCandidates(plan, now);

            // P2：协同计划建立后立即绑定三职责，后续逐 tick 观察依赖是否成立。
            if (_coordination && CoordinationDriver.IsCoordinationPlan(plan))
            {
                _coordinationDriver.Bind(plan);
                if (_coordinationDriver.BindTasks(plan))
                {
                    Cb2Log.Line("COORDINATION_BOUND",
                        "plan=" + plan.PlanId +
                        " revision=" + plan.Revision +
                        " main=" + _coordinationDriver.MainWaitTask.TaskId +
                        " guard=" + _coordinationDriver.GuardTask.TaskId +
                        " support=" + _coordinationDriver.SupportTask.TaskId +
                        " " + (plan.Geometry == null ? "geometry=<none>" : plan.Geometry.Describe()) +
                        " at=" + Cb2Log.F(now));
                }
            }
        }

        // P2 协同观察：逐 tick 核对保护与辅助的实际到位关系。
        // 关系未成立前主力保持等待；两个职责都满足连续有效读数后释放主力推进。
        internal void CoordinationTick(Mission mission)
        {
            if (!_coordination) return;
            var plan = _plans.Current;
            if (plan == null || !CoordinationDriver.IsCoordinationPlan(plan)) return;
            if (mission == null) return;

            double now = SafeTime(mission);

            // 关系观察必须用最新读数：依赖是否成立不能靠历史有效反馈放行。
            _snapshot = FormationSampler.Sample(mission, now);
            if (_snapshot == null) return;

            var guardObs = ObservationFor(_snapshot, _coordinationDriver.GuardTask);
            var supportObs = ObservationFor(_snapshot, _coordinationDriver.SupportTask);
            var outcome = _coordinationDriver.Observe(now, guardObs, supportObs);

            LogCoordination(plan, outcome, now);

            // 到期仍未建立关系：如实报告具体值并终止该 Plan。
            if ((outcome.GuardExpired || outcome.SupportExpired) && _coordinationDriver.Phase == CoordinationPhase.AwaitingDependencies)
            {
                string detail = "relationship_not_established guard_deadline=" +
                                Cb2Log.F(_coordinationDriver.GuardDeadline()) +
                                " support_deadline=" + Cb2Log.F(_coordinationDriver.SupportDeadline()) +
                                " guard_ready=" + (outcome.GuardReady ? 1 : 0) +
                                " support_ready=" + (outcome.SupportReady ? 1 : 0);
                Cb2Log.Line("COORDINATION_DEADLINE",
                    "plan=" + plan.PlanId + " " + detail + " at=" + Cb2Log.F(now));
                InvalidatePlan(PlanInvalidReason.NotEffective, detail, now, plan.PlanId);
                return;
            }

            // 依赖成立：建立主力推进任务（原子替换等待任务）。
            if (outcome.MainAdvanceAllowed && _coordinationDriver.Phase == CoordinationPhase.AwaitingDependencies)
            {
                var advance = _coordinationDriver.CreateAdvanceTask(plan, now);
                if (advance != null)
                {
                    // 阶段转换是计划内的分配变更：必须同步进 Plan、任务台账与当前归属三处。
                    // 只进台账会让裁决器按 TaskNotInPlan 拒绝推进提案。
                    plan.Tasks.Add(advance);
                    _tasks.Add(advance);
                    PublishAssignment(advance, now);
                    Cb2Log.Line("COORDINATION_ADVANCE",
                        advance.Describe() +
                        " replaced=" + _coordinationDriver.MainWaitTask.ReplacesTaskId +
                        " at=" + Cb2Log.F(now));
                }
            }

            // 推进期间依赖失效：保护或辅助不再满足前提，主力推进就失去依据。
            // 优先在合格编队里找替补重分配；无替补才关 Plan 记为不可执行。
            if (_coordinationDriver.Phase == CoordinationPhase.MainAdvancing &&
                !outcome.MainAdvanceAllowed)
            {
                HandleAdvanceDependencyLost(plan, outcome, now);
            }
        }

        // 推进期间保护或辅助前提失效的处置分支。
        // 有同类别合格且未被占用的替补就换人重分配，否则明确关闭 Plan。
        private void HandleAdvanceDependencyLost(BattlePlan plan, DependencyOutcome outcome, double now)
        {
            var lostRole = outcome.GuardFailure != RolePremiseFailure.None
                ? FormationRole.Guard : FormationRole.Support;
            var lostTask = lostRole == FormationRole.Guard
                ? _coordinationDriver.GuardTask : _coordinationDriver.SupportTask;
            var lostFailure = lostRole == FormationRole.Guard
                ? outcome.GuardFailure : outcome.SupportFailure;

            var replacement = FindReplacementCandidate(lostRole);
            Cb2Log.Line("COORDINATION_DEPENDENCY_LOST",
                "plan=" + plan.PlanId +
                " revision=" + plan.Revision +
                " role=" + lostRole +
                " failure=" + lostFailure +
                " task=" + (lostTask == null ? "<none>" : lostTask.TaskId) +
                " replacement=" + (replacement == null ? "<none>" : replacement.Key()) +
                " at=" + Cb2Log.F(now));

            if (replacement == null)
            {
                string detail = "advance_dependency_lost_without_replacement role=" + lostRole +
                                " failure=" + lostFailure +
                                " advance=" + (_coordinationDriver.AdvanceTaskId.Length == 0
                                    ? "<none>" : _coordinationDriver.AdvanceTaskId);
                InvalidatePlan(PlanInvalidReason.NotEffective, detail, now, plan.PlanId);
                return;
            }

            // 重分配：一次完整重分配只递增一次版本，旧任务全部关闭，三任务换新 ID。
            var next = _coordinationDriver.ReassignRole(plan, lostTask, lostRole, replacement, now);
            if (next == null)
            {
                Cb2Log.Line("COORDINATION_REASSIGN_FAILED",
                    "plan=" + plan.PlanId + " role=" + lostRole + " at=" + Cb2Log.F(now));
                return;
            }

            // 新分配必须同步进 Plan、任务台账与当前归属三处，否则裁决器按 TaskNotInPlan 拒绝。
            // 裁决槽作废由重分配入口统一处理：必须覆盖本计划全部参与编队，
            // 只清替补键会让未换职责的编队继续引用旧 ID，新任务永久被判"被取代"。
            PublishReboundTasks(plan, next, now);
            Cb2Log.Line("COORDINATION_REASSIGNED",
                "plan=" + plan.PlanId +
                " revision=" + plan.Revision +
                " role=" + lostRole +
                " old=" + (lostTask == null ? "<none>" : lostTask.TaskId) +
                " new=" + next.TaskId +
                " obj=" + next.ObjectIdentity +
                " baseline=" + next.RoleBaselineUnits +
                " at=" + Cb2Log.F(now));
        }

        // 重分配后把本版本新建的协同任务补进 Plan 与任务台账，并接管归属。
        // 只对尚未登记的做，避免重复入账。
        private void PublishReboundTasks(BattlePlan plan, FormationTask primary, double now)
        {
            foreach (var t in plan.Tasks)
            {
                if (t.IsClosed) continue;
                if (!string.Equals(t.PlanId, plan.PlanId, StringComparison.Ordinal)) continue;
                if (t.Revision != plan.Revision) continue;
                if (t.Role == FormationRole.None) continue;
                if (_tasks.Get(t.TaskId) != null) continue;
                _tasks.Add(t);
                PublishAssignment(t, now);
                Cb2Log.Line("COORDINATION_REBOUND_TASK",
                    t.Describe() + " at=" + Cb2Log.F(now));
            }
        }

        // 替补候选：同快照内类别允许 Reposition、资格有效、有实际人数，
        // 且不是当前三个职责正在使用的编队。不额外设人数门槛，人数是否够
        // 由下一轮 PremiseFailure 按该职责基线判定。
        private FormationRecord FindReplacementCandidate(FormationRole role)
        {
            var snapshot = _snapshot;
            if (snapshot == null) return null;
            var inUse = new List<string>();
            AddInUse(inUse, _coordinationDriver.MainWaitTask);
            AddInUse(inUse, _coordinationDriver.GuardTask);
            AddInUse(inUse, _coordinationDriver.SupportTask);
            AddInUse(inUse, _coordinationDriver.AdvanceTask);

            FormationRecord best = null;
            foreach (var r in snapshot.Records)
            {
                if (r.IsPlayerTeam || r.IsPlayerControlled) continue;
                if (!r.IsAiControlled) continue;
                if (!r.HasActiveCenter || r.ActiveMemberCount <= 0) continue;
                if (!PlanBuilder.ClassAllows(r.FormationClass, TaskKind.Reposition)) continue;
                if (inUse.Contains(r.Key())) continue;
                if (best == null || CompareStableRecord(r, best) < 0) best = r;
            }
            return best;
        }

        private static void AddInUse(List<string> keys, FormationTask task)
        {
            if (task == null) return;
            if (task.IsClosed) return;
            if (!keys.Contains(task.FormationKey)) keys.Add(task.FormationKey);
        }

        // 候选排序与计划建立时一致：先 TeamIndex，再 FormationIndex。
        private static int CompareStableRecord(FormationRecord a, FormationRecord b)
        {
            if (a.TeamIndex != b.TeamIndex) return a.TeamIndex < b.TeamIndex ? -1 : 1;
            if (a.FormationIndex != b.FormationIndex) return a.FormationIndex < b.FormationIndex ? -1 : 1;
            return string.CompareOrdinal(a.ObjectIdentity, b.ObjectIdentity);
        }

        // P2 评价点：为所有待写协同任务收提案，收齐后按编队一次公布。
        // 收集与公布只在这里发生；写前门只消费已公布结果并即时复核，
        // 否则首个到达写前门的提案会被当成唯一竞争者提前公布。
        internal void PublishCoordinationProposals(Mission mission, double now)
        {
            if (!_coordination) return;
            var plan = _plans.Current;
            if (plan == null || !CoordinationDriver.IsCoordinationPlan(plan)) return;

            string injectTargetId = "";
            if (EligibilityProbe.Instance.P2CompetitionEnabled)
            {
                var injectTarget = CompetitionInjectTarget(plan);
                if (injectTarget != null) injectTargetId = injectTarget.TaskId;
            }

            var formations = new List<string>();
            var blockedKeys = new List<string>();
            foreach (var task in _tasks.OpenTasks())
            {
                if (task.WrittenAt >= 0.0) continue;
                if (task.Role == FormationRole.None) continue;
                if (!string.Equals(task.PlanId, plan.PlanId, StringComparison.Ordinal)) continue;
                if (task.Revision != plan.Revision) continue;
                if (!formations.Contains(task.FormationKey)) formations.Add(task.FormationKey);
                // 目标未生成的任务不参与本轮竞争：缺目标不是「目标在原点」。
                // 不能只 submit 合法者就公布批次——本轮缺目标的任务下一轮可能补上，
                // 提前公布会让它永远来不及成为竞争者。
                var proposal = ProposalFor(task, mission, now);
                if (proposal == null) { blockedKeys.Add(task.FormationKey); continue; }
                _coordinationDriver.Arbiter.Submit(proposal, plan, task);
                // 注入只跟随已成功提交的生产候选：目标缺失的编队不注入，
                // 否则只含注入候选的批次会公布出写不出去的结果。
                if (injectTargetId.Length != 0 && string.Equals(task.TaskId, injectTargetId, StringComparison.Ordinal))
                {
                    InjectCompetition(plan, task, now);
                }
            }

            foreach (string key in formations)
            {
                if (blockedKeys.Contains(key)) continue;
                var batch = _coordinationDriver.Arbiter.OpenBatchFor(key);
                if (batch == null || batch.Revision != plan.Revision) continue;
                var decision = _coordinationDriver.Arbiter.PublishBatch(key);
                if (decision == null || !decision.Published) continue;
                int winnerIndex = -1;
                for (int i = 0; i < batch.Candidates.Count; i++)
                {
                    if (string.Equals(batch.Candidates[i].ProposalId, decision.WinnerProposalId, StringComparison.Ordinal))
                    {
                        winnerIndex = i;
                        break;
                    }
                }
                for (int i = 0; i < batch.Candidates.Count; i++)
                {
                    var c = batch.Candidates[i];
                    var ct = i < batch.Tasks.Count ? batch.Tasks[i] : null;
                    bool isWinner = i == winnerIndex;
                    var w = winnerIndex >= 0 ? batch.Candidates[winnerIndex] : null;
                    var wt = winnerIndex >= 0 && winnerIndex < batch.Tasks.Count ? batch.Tasks[winnerIndex] : null;
                    Cb2Log.Line("ARBITRATION_CANDIDATE",
                        "key=" + key +
                        " plan=" + plan.PlanId +
                        " revision=" + plan.Revision +
                        " task=" + c.TaskId +
                        " source=" + c.Source +
                        " kind=" + c.Kind +
                        " target=" + PointText(c.TargetX, c.TargetY, c.HasTarget) +
                        " target_error=" + Cb2Log.F(ProposalArbiter.TargetError(c, ct)) +
                        " outcome=" + (isWinner ? "winner" : "loser") +
                        " reason=" + (isWinner ? "selected_by_competition_order" : LoserReason(c, ct, w, wt)) +
                        " at=" + Cb2Log.F(now));
                }
                Cb2Log.Line("PROPOSAL_PUBLISHED",
                    "key=" + key +
                    " plan=" + plan.PlanId +
                    " revision=" + plan.Revision +
                    " candidates=" + batch.Candidates.Count +
                    " winner=" + decision.WinnerProposalId +
                    " at=" + Cb2Log.F(now));
            }
        }

        private static string PointText(double x, double y, bool hasTarget)
        {
            return hasTarget ? Cb2Log.F(x) + "," + Cb2Log.F(y) : "<none>";
        }

        // 落败原因直接复算裁决顺序的三条规则（派单 §5 第 3 条）：
        // 先「符合当前阶段任务 Kind」，再「距规范目标点误差升序」，最后 proposal_id 稳定序。
        // 日志与裁决器不共用同一段分支，避免两者一起写错却看不出差异。
        private static string LoserReason(Proposal c, FormationTask ct, Proposal w, FormationTask wt)
        {
            if (w == null) return "no_winner";
            bool cKindOk = ct != null && c.Kind == ct.Kind;
            bool wKindOk = wt != null && w.Kind == wt.Kind;
            if (cKindOk != wKindOk) return "kind_mismatch";
            double cErr = ProposalArbiter.TargetError(c, ct);
            double wErr = ProposalArbiter.TargetError(w, wt);
            if (cErr > wErr) return "higher_target_error";
            if (cErr < wErr) return "lower_target_error";
            return "proposal_id_order";
        }

        // A5 注入目标：计划内首个仍未写令的保护位置任务，其次辅助位置任务。
        // 按计划任务顺序取，不依赖字典遍历，同一评价点重复可复现。
        private static FormationTask CompetitionInjectTarget(BattlePlan plan)
        {
            FormationTask fallback = null;
            foreach (var t in plan.Tasks)
            {
                if (t.Kind != TaskKind.Reposition) continue;
                if (t.IsClosed || t.WrittenAt >= 0.0) continue;
                if (t.Revision != plan.Revision) continue;
                if (t.Role == FormationRole.Guard) return t;
                if (t.Role == FormationRole.Support && fallback == null) fallback = t;
            }
            return fallback;
        }

        // 派单 §9.2 场景 A 第 2 条：首次裁决前注入两个同级合法但目标不同的建议，
        // 之后在有活动任务时注入同对象冲突。注入只提交提案，不写引擎、不标记获选（§5）。
        private void InjectCompetition(BattlePlan plan, FormationTask task, double now)
        {
            var g = plan.Geometry;
            if (g == null || !task.HasCanonicalTarget) return;

            if (_competitionFirstAt < 0.0)
            {
                _competitionFirstAt = now;
                Cb2Log.Line("COMPETITION_INJECT_BEGIN",
                    "task=" + task.TaskId +
                    " key=" + task.FormationKey +
                    " role=" + task.Role +
                    " plan=" + plan.PlanId +
                    " revision=" + plan.Revision +
                    " at=" + Cb2Log.F(now));
            }
            _competitionRounds++;
            if (!_competitionWindowLogged && now - _competitionFirstAt >= TestParameters.P2CompetitionWindowSeconds)
            {
                _competitionWindowLogged = true;
                Cb2Log.Line("COMPETITION_INJECT_WINDOW_SATISFIED",
                    "task=" + task.TaskId +
                    " key=" + task.FormationKey +
                    " plan=" + plan.PlanId +
                    " elapsed=" + Cb2Log.F(now - _competitionFirstAt) +
                    " rounds=" + _competitionRounds +
                    " at=" + Cb2Log.F(now));
            }

            double lx = task.CanonicalX + g.LateralX * TestParameters.P2CompetitionLateralMeters;
            double ly = task.CanonicalY + g.LateralY * TestParameters.P2CompetitionLateralMeters;
            bool lateralLegal = task.Role == FormationRole.Guard
                ? PlanBuilder.GuardInPosition(g, lx, ly)
                : PlanBuilder.SupportInPosition(g, lx, ly);
            if (!lateralLegal)
            {
                // 侧向候选必须仍是本任务的合法位置候选；不合法就整次不注入，
                // 不能用非法候选冒充「同级合法」竞争（派单 §9.2）。
                Cb2Log.Line("COMPETITION_INJECT_SKIPPED",
                    "task=" + task.TaskId +
                    " key=" + task.FormationKey +
                    " reason=lateral_candidate_outside_position_constraint" +
                    " lateral=" + PointText(lx, ly, true) +
                    " at=" + Cb2Log.F(now));
                return;
            }

            var current = _coordinationDriver.Arbiter.CurrentFor(task.FormationKey);
            if (current == null)
            {
                SubmitInjected(plan, task, task.Kind, ProposalSource.TestInjection,
                    task.CanonicalX, task.CanonicalY, "first_competition_canonical", -1, now);
                SubmitInjected(plan, task, task.Kind, ProposalSource.TestInjection,
                    lx, ly, "first_competition_lateral", -1, now);
                return;
            }

            // 「越权来源请求与当前任务不同的命令/目标」（派单 §9.2）：来源非法 + 目标不同。
            SubmitInjected(plan, task, task.Kind, ProposalSource.None,
                lx, ly, "conflict_unauthorized_source", -1, now);
            SubmitInjected(plan, task, task.Kind, ProposalSource.TestInjection,
                task.CanonicalX, task.CanonicalY, "conflict_stale_revision", plan.Revision - 1, now);
            SubmitInjected(plan, task, task.Kind, ProposalSource.TaskSubmission,
                task.CanonicalX, task.CanonicalY, "conflict_duplicate_of_task_submission", -1, now);
            SubmitInjected(plan, task, task.Kind, ProposalSource.TestInjection,
                lx, ly, "conflict_different_target", -1, now);
        }

        private ArbitrationDecision SubmitInjected(BattlePlan plan, FormationTask task, TaskKind kind,
            ProposalSource source, double x, double y, string purpose, int revisionOverride, double now)
        {
            var p = Proposal.Create(_missionKey, plan, task, kind, source, x, y, true, purpose, now);
            if (revisionOverride >= 0 && revisionOverride != p.Revision)
            {
                // 版本是提案身份的一段：改版本必须同时重算 ID，
                // 否则提案会自报「旧版本」却带着新版本的身份。
                p.Revision = revisionOverride;
                p.ProposalId = Proposal.MakeId(p.MissionKey, p.PlanId, p.Revision, p.TaskId,
                                               p.FormationKey, p.ObjectIdentity, p.Kind, p.Source,
                                               p.TargetX, p.TargetY);
            }
            var decision = _coordinationDriver.Arbiter.Submit(p, plan, task);
            Cb2Log.Line("ARBITRATION_INJECT",
                "key=" + task.FormationKey +
                " plan=" + plan.PlanId +
                " revision=" + plan.Revision +
                " proposal_revision=" + p.Revision +
                " task=" + task.TaskId +
                " purpose=" + purpose +
                " source=" + source +
                " kind=" + kind +
                " target=" + PointText(x, y, true) +
                " outcome=" + DecisionOutcome(decision) +
                " detail=" + (decision == null ? "" : decision.Detail) +
                " at=" + Cb2Log.F(now));
            return decision;
        }

        private static string DecisionOutcome(ArbitrationDecision d)
        {
            if (d == null) return "no_decision";
            if (!d.Accepted) return "rejected:" + d.RejectReason;
            if (d.Duplicate) return "duplicate";
            if (d.LoserRejectReason != ProposalRejectReason.None) return "held_by_current:" + d.LoserRejectReason;
            return "batched";
        }

        // 提案内容完全由当前任务与几何派生，不按接收先后编号。
        // 目标尚未生成时返回 null：不能以 (0,0) 冒充有效目标，
        // 否则「无目标」会被当成「目标恰在原点」参与竞争与放行。
        private Proposal ProposalFor(FormationTask task, Mission mission, double now)
        {
            double x, y;
            bool hasTarget = TryReadTargetPoint(task, out x, out y);
            if (!hasTarget)
            {
                // 只有需要位置目标的命令（Reposition）才因无目标而放弃提案。
                // Hold/Engage 不带位置目标，无目标是它们的正常形态，
                // 不能因为拿不到坐标就不提交——否则等待阶段永远产生不了裁决结果。
                if (ProposalArbiter.NeedsTarget(task.Kind))
                {
                    Cb2Log.Line("PROPOSAL_TARGET_MISSING",
                        "task=" + task.TaskId + " key=" + task.FormationKey +
                        " role=" + task.Role + " kind=" + task.Kind + " at=" + Cb2Log.F(now));
                    return null;
                }
            }
            return Proposal.Create(_missionKey, _plans.Current, task, task.Kind,
                                   ProposalSource.TaskSubmission, x, y, hasTarget, task.Reason, now);
        }

        // P2 写令前裁决门：返回 null 表示已获本编队的唯一裁决结果，可继续写令；
        // 否则返回拒绝原因。本方法不收集、不公布，只消费评价点已公布的结果。
        private string CoordinationWriteBlock(FormationTask task, Mission mission, Formation f, double now)
        {
            var plan = _plans.Current;
            if (plan == null) return "no_open_plan";

            // 前置：任务必须属于当前开放计划且 revision 一致，旧版本不得写令。
            if (!string.Equals(task.PlanId, plan.PlanId, StringComparison.Ordinal))
                return "task_from_other_plan";
            if (task.Revision != plan.Revision)
                return "stale_revision task_revision=" + task.Revision + " plan_revision=" + plan.Revision;

            double targetX, targetY;
            string note;
            var order = OrderForTask(mission, f, task, now, out note);
            if (order.OrderEnum == MovementOrder.MovementOrderEnum.Invalid) return "task_position_invalid";

            bool hasTarget = TryReadTargetPoint(task, out targetX, out targetY);

            var current = _coordinationDriver.Arbiter.CurrentFor(task.FormationKey);
            if (current == null)
                return "no_published_decision";

            if (!string.Equals(current.TaskId, task.TaskId, StringComparison.Ordinal))
                return "superseded_by=" + current.TaskId;

            if (current.Revision != plan.Revision)
                return "published_revision_stale published=" + current.Revision + " plan=" + plan.Revision;

            // 即时复核：写令前重算的提案身份必须与已公布赢家一致，
            // 否则说明公布之后任务目标或版本又被改动，不能沿用旧裁决放行。
            var fresh = ProposalFor(task, mission, now);
            if (fresh == null)
                return "target_missing_at_write";
            if (!string.Equals(fresh.ProposalId, current.ProposalId, StringComparison.Ordinal))
                return "decision_content_changed fresh=" + fresh.ProposalId + " published=" + current.ProposalId;

            if (current.HasTarget && hasTarget)
            {
                double error = CoordinationGeometry.Distance(current.TargetX, current.TargetY, targetX, targetY);
                if (error > TestParameters.TargetPointToleranceMeters)
                    return "decision_target_mismatch error=" + Cb2Log.F(error);
            }

            return null;
        }

        // 协同任务的目标点由计划几何直接给出，与"是否已写过令"无关。
        // 提案阶段必须先拿到它：目标若只能从已写令的回读里取，就会形成
        // 「提案要目标 → 目标要写令 → 写令要提案已公布」的死锁，
        // 使计划永远无法产生第一条命令。
        private bool RoleTargetPoint(FormationTask task, out double x, out double y)
        {
            x = 0.0;
            y = 0.0;
            var plan = _plans.Current;
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

        private bool TryReadTargetPoint(FormationTask task, out double x, out double y)
        {
            x = 0.0;
            y = 0.0;
            if (task.HasOrderTarget)
            {
                x = task.OrderTargetX;
                y = task.OrderTargetY;
                return true;
            }
            if (task.HasPosition)
            {
                x = task.RequestedX;
                y = task.RequestedY;
                return true;
            }
            return RoleTargetPoint(task, out x, out y);
        }

        // 取某任务所属编队的最新采样，转成依赖观察输入。
        private DependencyObservation ObservationFor(FormationSnapshot snapshot, FormationTask task)
        {
            if (task == null) return null;
            var rec = snapshot.FindByKey(task.FormationKey);
            if (rec == null) return null;
            return new DependencyObservation
            {
                ObjectPresent = string.Equals(rec.ObjectIdentity, task.ObjectIdentity, StringComparison.Ordinal),
                PositionKnown = rec.HasActiveCenter &&
                                CoordinationGeometry.IsFinite(rec.ActiveCenterX) &&
                                CoordinationGeometry.IsFinite(rec.ActiveCenterY),
                X = rec.ActiveCenterX,
                Y = rec.ActiveCenterY,
                // 人数口径统一为有效活跃成员：职责基线与后续读数必须同源，
                // 引擎编队人数会把 detached/不可用成员算进来，与质心不是同一批人。
                CurrentUnits = rec.HasActiveCenter ? rec.ActiveMemberCount : 0,
                Eligible = rec.IsAiControlled && !rec.IsPlayerControlled && !rec.IsPlayerTeam,
                FormationClass = rec.FormationClass,
                MemberDigest = rec.MemberDigest ?? "",
            };
        }

        private void LogCoordination(BattlePlan plan, DependencyOutcome outcome, double now)
        {
            if (_lastCoordinationLogAt > 0.0 && now - _lastCoordinationLogAt < 1.0) return;
            _lastCoordinationLogAt = now;
            Cb2Log.Line("COORDINATION_STATE",
                "plan=" + plan.PlanId +
                " revision=" + plan.Revision +
                " phase=" + _coordinationDriver.Phase +
                " guard_ready=" + (outcome.GuardReady ? 1 : 0) +
                " support_ready=" + (outcome.SupportReady ? 1 : 0) +
                " advance_allowed=" + (outcome.MainAdvanceAllowed ? 1 : 0) +
                " guard_failure=" + outcome.GuardFailure +
                " support_failure=" + outcome.SupportFailure +
                " detail=" + outcome.Detail +
                " at=" + Cb2Log.F(now));
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

                // 延迟与顺序门必须在 arm 之前问：原生获选是一次性事件，
                // 若先 arm 再在写令路径拒绝，该次获选即被消耗，不会再重来（脚本 §6.1）。
                if (!BattlePlan.IsExitTask(task))
                {
                    string defer = WriteDelayBlock(task, now);
                    if (defer != null)
                    {
                        Cb2Log.Line("WRITE_DELAYED",
                            "task=" + task.TaskId +
                            " plan=" + task.PlanId +
                            " key=" + task.FormationKey +
                            " reason=" + defer +
                            " source=submit_pending" +
                            " at=" + Cb2Log.F(now));
                        continue;
                    }
                }

                var behavior = ArmBehavior(mission, f, task);
                if (behavior == null)
                {
                    InvalidatePlan(PlanInvalidReason.WriteFailed, "task=" + task.TaskId +
                        " reason=behavior_register_failed", now);
                    return;
                }

                task.NoteSubmission(now);
                _gate.SubmitCandidate(task.TaskId, task.FormationKey, now);
                Cb2Log.Line("TASK_CANDIDATE_SUBMITTED",
                    task.Describe() +
                    " behavior_registered=1" +
                    " behavior_weight=" + Cb2Log.F(TestParameters.HoldBehaviorWeight) +
                    " at=" + Cb2Log.F(now) +
                    " submission_count=" + task.SubmissionCount +
                    " first_submitted_at=" + Cb2Log.F(task.SubmittedAt) +
                    " selection_deadline=" + Cb2Log.F(task.SelectionDeadline()) +
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

            // 尚未投递过候选的任务不得被原生回调认领：此时编队上的我们行为可能来自上一计划的
            // 退出任务，SelectedAt 会落在一条没有等待截止的意图上，随后写令必被拒（实机 plan-2）。
            // 真实获选以本任务自己的投递为起点（脚本 §5.2、§6.3）。
            if (task.SubmittedAt < 0.0)
            {
                Cb2Log.Line("ACTIVE_BEHAVIOR_NOT_SUBMITTED",
                    "task=" + task.TaskId +
                    " plan=" + task.PlanId +
                    " key=" + keyNow +
                    " active_behavior=" + FormationSampler.ActiveBehaviorNameOf(f) +
                    " reason=candidate_not_submitted" +
                    " at=" + Cb2Log.F(SafeTime(mission)));
                return;
            }

            // 截止后才首次获选：等待期限已过，不得转入执行（脚本 §6.3）。
            // 截止前已经获选的任务走原规则，由 SelectedAt 判定，不受此门影响。
            if (task.SelectedAt < 0.0 && SelectionWindowClosed(task, SafeTime(mission)))
            {
                Cb2Log.Line("REAL_SELECTION_REJECTED",
                    "task=" + task.TaskId +
                    " plan=" + task.PlanId +
                    " key=" + keyNow +
                    " reason=selection_window_closed" +
                    " first_submitted_at=" + Cb2Log.F(task.SubmittedAt) +
                    " selection_deadline=" + Cb2Log.F(task.SelectionDeadline()) +
                    " at=" + Cb2Log.F(SafeTime(mission)));
                return;
            }

            if (task.SelectedAt < 0.0) MarkRealSelection(mission, f, task, "native_callback");
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

        // 分配公布：计划建立、阶段转换、职责重分配三条路径共用这一个入口。
        // 发布即接管归属，覆盖初始分配与后续版本；退出任务不参与（它处理的是已关闭对象）。
        internal void PublishAssignment(FormationTask task, double now)
        {
            if (task == null) return;
            if (BattlePlan.IsExitTask(task)) return;
            if (string.IsNullOrEmpty(task.ObjectIdentity)) return;

            _exits.TakeOwnership(task.ObjectIdentity, task.TaskId);
            Cb2Log.Line("ASSIGNMENT_PUBLISHED",
                "task=" + task.TaskId +
                " plan=" + task.PlanId +
                " revision=" + task.Revision +
                " role=" + task.Role +
                " obj=" + task.ObjectIdentity +
                " at=" + Cb2Log.F(now));
        }

        // 行为 tick 路径的获选判定：与获选回调同一组判据，只换证据来源。
        // 原生 ActiveBehavior 的 setter 只在引用变化时发事件（同实例连续当选不再发），
        // 所以换任务后新任务收不到回调。行为 tick 由原生 ActiveBehavior 分支驱动，
        // 进入它即证明原生此刻选中了本行为，用它补齐"真实本方行为获选"这一步。
        private void AcceptRealSelection(Mission mission, Formation f, FormationTask task, string key)
        {
            double now = SafeTime(mission);
            if (task.SubmittedAt < 0.0)
            {
                Cb2Log.Line("REAL_SELECTION_NOT_SUBMITTED",
                    "task=" + task.TaskId +
                    " key=" + key +
                    " reason=candidate_not_submitted" +
                    " source=behavior_tick" +
                    " at=" + Cb2Log.F(now));
                return;
            }

            if (SelectionWindowClosed(task, now))
            {
                Cb2Log.Line("REAL_SELECTION_REJECTED",
                    "task=" + task.TaskId +
                    " key=" + key +
                    " reason=selection_window_closed" +
                    " source=behavior_tick" +
                    " first_submitted_at=" + Cb2Log.F(task.SubmittedAt) +
                    " selection_deadline=" + Cb2Log.F(task.SelectionDeadline()) +
                    " at=" + Cb2Log.F(now));
                return;
            }

            MarkRealSelection(mission, f, task, "behavior_tick");
        }

        // 真实获选：唯一允许转入执行的状态转移。
        private void MarkRealSelection(Mission mission, Formation f, FormationTask task, string source)
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

            // 获选回调可能晚到：此时该任务可能已被新 revision 取代、或对象已被换走。
            // 只复核，不接管归属——归属在分配公布时转移（见 PublishAssignment），
            // 否则迟到回调会把归属从当前分配手里夺回来。
            string objNow = ObjectIdentityOf(f);
            if (!ReferenceEquals(PendingTaskForFormation(KeyOf(teamIndex, f)), task) ||
                !string.Equals(objNow, task.ObjectIdentity, StringComparison.Ordinal))
            {
                Cb2Log.Line("REAL_SELECTION_STALE",
                    "task=" + task.TaskId +
                    " revision=" + task.Revision +
                    " key=" + KeyOf(teamIndex, f) +
                    " obj_task=" + task.ObjectIdentity +
                    " obj_now=" + objNow +
                    " owner=" + _exits.OwnerOf(objNow));
                return;
            }

            task.FormationKey = KeyOf(teamIndex, f);
            task.ObjectIdentity = objNow;
            task.SelectedAt = now;

            _gate.MarkSelected(task.TaskId, task.FormationKey, now);
            _gate.BindObject(task.TaskId, task.ObjectIdentity);

            Cb2Log.Line("REAL_SELECTION",
                "task=" + task.TaskId +
                " plan=" + task.PlanId +
                " key=" + task.FormationKey +
                " side=" + side +
                " obj=" + task.ObjectIdentity +
                " source=" + source +
                " at=" + Cb2Log.F(now));

            ObservationAdapter.Resolve(now);
            ObservationAdapter.Emit("selection.real", now,
                new EventFields()
                    .Add("task_id", task.TaskId)
                    .Add("plan_id", task.PlanId)
                    .Add("formation_key", task.FormationKey)
                    .Add("side", side)
                    .Add("object_identity", task.ObjectIdentity)
                    .Add("source", source)
                    .ToString());
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
            if (task == null) return;

            // 原生只在 ActiveBehavior 引用变化时发事件：同一个行为实例连续当选不会再发，
            // 同一编队换任务后新任务会一直等不到 SelectedAt（实机 plan-2-task-4 十秒超时）。
            // 行为 tick 只由原生 FormationAI 的 ActiveBehavior 分支调用，
            // 进入这里本身就说明原生此刻选中的是本行为，故用它补做同一组获选判定。
            if (task.SelectedAt < 0.0) AcceptRealSelection(mission, f, task, keyNow);
            if (task.SelectedAt < 0.0) return;

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
                // 独立退出动作不受此门约束：它按自己的身份、来源与 1 s 截止判定，
                // 否则已获选的退出 Stop 会被普通任务的兜底误挡（脚本 §7.2）。
                var plan = _plans.Current;
                if (!BattlePlan.IsExitTask(task) &&
                    plan != null && plan.IsClosed &&
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

            // 延迟与顺序门已在 SubmitPending 中于 arm 之前判定，此处不再重复，
            // 否则会在原生获选之后再次拒绝并丢失该次获选。

            // 过期退出动作不得在安全时限之后仍写出（脚本 §7.2）：
            // 到点即关闭，最终 setter 前再拦一次，避免跨入新计划期间。
            if (BattlePlan.IsExitTask(task) &&
                now - task.CreatedAt >= TestParameters.SafetyCancelLatencySeconds)
            {
                _tasks.CloseTask(task.TaskId, now);
                Cb2Log.Line("EXIT_STOP_EXPIRED",
                    task.Describe() +
                    " reason=expired_before_write" +
                    " waited=" + Cb2Log.F(now - task.CreatedAt) +
                    " at=" + Cb2Log.F(now));
                return;
            }

            // 普通任务获选等待截止的最终防线：截止后不得取得写令资格。
            // 截止前已真实获选的任务不受影响（其 SelectedAt 早于截止）。
            if (task.SelectedAt >= task.SelectionDeadline())
            {
                _tasks.CloseTask(task.TaskId, now);
                _gate.Close(task.TaskId, CancelReason.SelectionTimeout, now);
                Cb2Log.Line("WRITE_REJECTED",
                    task.Describe() +
                    " reason=selection_window_closed" +
                    " first_submitted_at=" + Cb2Log.F(task.SubmittedAt) +
                    " selection_deadline=" + Cb2Log.F(task.SelectionDeadline()) +
                    " selected_at=" + Cb2Log.F(task.SelectedAt) +
                    " at=" + Cb2Log.F(now));
                return;
            }

            // 该对象已被后续分配接管时，旧退出任务不得再对它写令。
            // 放在 final setter 前，与超时检查同级：即使旧任务已获选、已转交，
            // 也不能在新分配生效之后覆盖其动作。
            if (BattlePlan.IsExitTask(task) &&
                !_exits.MayActOn(task.ObjectIdentity, task.OriginTaskId, task.TaskId))
            {
                _tasks.CloseTask(task.TaskId, now);
                Cb2Log.Line("EXIT_STOP_SUPERSEDED",
                    task.Describe() +
                    " reason=object_taken_over_by_new_assignment" +
                    " owner=" + _exits.OwnerOf(task.ObjectIdentity) +
                    " at=" + Cb2Log.F(now));
                return;
            }

            // P2 协同计划：写令前必须持有本编队本任务的当前裁决结果。
            // 提案在提交阶段入批，收齐后由裁决器统一公布；未公布的提案不得执行。
            // 原生其他写令者不受此门限制（这只是 CB2 内部提案的唯一裁决）。
            if (_coordination && task.Role != FormationRole.None)
            {
                string arbBlock = CoordinationWriteBlock(task, mission, f, now);
                if (arbBlock != null)
                {
                    Cb2Log.Line("WRITE_BLOCKED",
                        "task=" + task.TaskId +
                        " plan=" + task.PlanId +
                        " key=" + key +
                        " reason=" + arbBlock +
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

                ObservationAdapter.Emit("write.blocked", now,
                    new EventFields()
                        .Add("task_id", task.TaskId)
                        .Add("plan_id", task.PlanId)
                        .Add("formation_key", key)
                        .Add("reason", decision.BlockReason)
                        .Add("ai_controlled", aiControlled)
                        .Add("behavior_is_ours", behaviorIsOurs)
                        .Add("target_valid", targetValid)
                        .Add("player_owned", playerOwned)
                        .ToString());

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

            // 位移基线在真实写令这一刻固定：计划创建到发令之间的原生移动
            // 不能计入本命令的效果（P1-CODEX-REVERIFY §3.2）。
            try
            {
                var posNow = f.CurrentPosition;
                task.SetBaseline(posNow.x, posNow.y);
                if (task.HasPosition)
                {
                    task.DistanceToTargetAtWrite = task.BaselineDistanceTo(task.RequestedX, task.RequestedY);
                }
            }
            catch (Exception) { }

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

            // 读回实际命令目标点：取自原生 MovementOrder，不是请求值。
            // 只对位置任务有意义；读不到就不冒充有目标点。
            string readbackNote = "";
            if (task.HasPosition)
            {
                if (TryReadOrderTarget(f, out double orderTargetX, out double orderTargetY))
                {
                    task.SetOrderTarget(orderTargetX, orderTargetY);
                    readbackNote = " order_target=(" + Cb2Log.F(orderTargetX) + "," +
                                   Cb2Log.F(orderTargetY) + ")" +
                                   " readback_error=" + Cb2Log.F(task.OrderTargetReadbackError());
                }
                else
                {
                    readbackNote = " order_target=<unreadable>";
                }
            }

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
                readbackNote +
                " first_command_delay=" + Cb2Log.F(delay) +
                " write_attempts_total=" + _gate.WriteAttempts +
                " write_attempts_task=" + _tasks.WritesOf(task.TaskId));

            ObservationAdapter.Emit("write.executed", now,
                new EventFields()
                    .Add("task_id", task.TaskId)
                    .Add("plan_id", task.PlanId)
                    .Add("formation_key", key)
                    .Add("invoked", invoked)
                    .Add("error", error)
                    .Add("order_before", before)
                    .Add("order_after", after)
                    .Add("target_x", task.RequestedX)
                    .Add("target_y", task.RequestedY)
                    .Add("target_available", task.HasPosition)
                    .Add("order_enum", order.OrderEnum.ToString())
                    .Add("geometry", geometryNote)
                    .Add("exec_tid", execThreadId)
                    .Add("is_main_thread", _handoff.IsMainThread(execThreadId))
                    .Add("original_body_entered", originalEntered)
                    .Add("original_body_tid", originalEntered ? originalThreadId : -1)
                    .ToString());

            // 退出动作写完一次即收尾，不再留在开放任务里反复评估（脚本 §7.2）。
            if (BattlePlan.IsExitTask(task))
            {
                CloseTask(task, PlanInvalidReason.None, now);
                Cb2Log.Line("EXIT_STOP_DONE",
                    task.Describe() +
                    " origin_plan=" + task.PlanId +
                    " order_after=" + after +
                    " at=" + Cb2Log.F(now));
                ObservationAdapter.Emit("task.closed", now,
                    new EventFields()
                        .Add("task_id", task.TaskId)
                        .Add("plan_id", task.PlanId)
                        .Add("formation_key", key)
                        .Add("reason", "exit_stop_done")
                        .Add("order_after", after)
                        .ToString());
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

            // 发令顺序遵守既定 Ordinal：前序普通任务尚未实际写令时，
            // 后序必须等待（脚本 §4.5，Ordinal 1 = 远程，2 = 步兵）。
            if (PredecessorNotWritten(plan, task))
            {
                return "predecessor_not_written";
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

        // 本计划的普通任务里，是否存在 Ordinal 小于本任务、且尚未实际写令的前序任务。
        private bool PredecessorNotWritten(BattlePlan plan, FormationTask task)
        {
            foreach (var other in plan.Tasks)
            {
                if (other == null) continue;
                if (BattlePlan.IsExitTask(other)) continue;
                if (other.Ordinal >= task.Ordinal) continue;
                // 已关闭的前序任务不会再写令，不应拖住后序。
                if (other.IsClosed) continue;
                if (other.WrittenAt < 0.0) return true;
            }
            return false;
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
                    // P2 协同任务使用计划内的协同几何（主力 M*、保护 G*、辅助 S*），
                    // 不能落回 P1 的「前进/后撤」公式，否则几何测试与实机命令不是同一件事。
                    if (_coordination && task.Role != FormationRole.None)
                        return CoordinationOrderFor(mission, task, now, out note);

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

        // P2 协同命令：目标点由计划内的协同几何直接给出（派单 §4）。
        // 主力去 M*，保护去 G*，辅助去 S*；几何缺失或目标非有限就不写令，
        // 不用别的位置凑数——不达标就如实记为不可执行。
        private MovementOrder CoordinationOrderFor(Mission mission, FormationTask task, double now, out string note)
        {
            note = "";
            var plan = _plans.Current;
            if (plan == null)
            {
                note = "no_open_plan";
                return MovementOrder.MovementOrderNull;
            }
            if (plan.Geometry == null)
            {
                note = "no_coordination_geometry";
                return MovementOrder.MovementOrderNull;
            }
            if (!string.Equals(task.PlanId, plan.PlanId, StringComparison.Ordinal))
            {
                note = "task_from_other_plan";
                return MovementOrder.MovementOrderNull;
            }
            if (task.Revision != plan.Revision)
            {
                note = "stale_revision task_revision=" + task.Revision + " plan_revision=" + plan.Revision;
                return MovementOrder.MovementOrderNull;
            }

            var g = plan.Geometry;
            double targetX, targetY;
            switch (task.Role)
            {
                case FormationRole.Guard:
                    targetX = g.GuardX; targetY = g.GuardY;
                    break;
                case FormationRole.Support:
                    targetX = g.SupportX; targetY = g.SupportY;
                    break;
                case FormationRole.Main:
                    targetX = g.MainX; targetY = g.MainY;
                    break;
                default:
                    note = "role_not_in_coordination";
                    return MovementOrder.MovementOrderNull;
            }
            if (!CoordinationGeometry.IsFinite(targetX) || !CoordinationGeometry.IsFinite(targetY))
            {
                note = "non_finite_target";
                return MovementOrder.MovementOrderNull;
            }

            var record = _snapshot != null ? _snapshot.FindByKey(task.FormationKey) : null;
            if (record == null)
            {
                note = "no_current_record";
                return MovementOrder.MovementOrderNull;
            }
            double originX = record.HasActiveCenter ? record.ActiveCenterX : record.PosX;
            double originY = record.HasActiveCenter ? record.ActiveCenterY : record.PosY;
            task.SetTarget(originX, originY, targetX, targetY);
            note = "move_to_role_point role=" + task.Role +
                   " requested=(" + Cb2Log.F(targetX) + "," + Cb2Log.F(targetY) + ")" +
                   " origin=(" + Cb2Log.F(originX) + "," + Cb2Log.F(originY) + ")" +
                   " revision=" + plan.Revision;
            return MovementOrder.MovementOrderMove(
                new WorldPosition(mission.Scene, new Vec3((float)targetX, (float)targetY, 0f)));
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

            // 获选等待到期的普通任务在到期后的第一个主线程 tick 关闭、清槽、撤权。
            SweepExpiredSelections(now);

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
                    ObservationAdapter.Emit("task.feedback", now,
                        new EventFields()
                            .Add("task_id", task.TaskId)
                            .Add("plan_id", task.PlanId)
                            .Add("state", "effective")
                            .Add("since_write", now - task.WrittenAt)
                            .Add("detail", result.Detail)
                            .ToString());
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
                    ObservationAdapter.Emit("task.feedback", now,
                        new EventFields()
                            .Add("task_id", task.TaskId)
                            .Add("plan_id", task.PlanId)
                            .Add("state", "failed")
                            .Add("reason", result.Reason.ToString())
                            .Add("since_write", now - task.WrittenAt)
                            .Add("detail", result.Detail)
                            .ToString());
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
            // 逐 tick 战略条件路径与决策路径使用同一套继续语义（脚本 §4.5）：
            // 不用创建时的一次布尔相等当继续条件。
            if (conditions.CanContinueAs(plan)) return;

            Cb2Log.Line("PLAN_CONDITION_LOST",
                "plan=" + plan.PlanId +
                " old=" + plan.Conditions.Describe() +
                " new=" + conditions.Describe() +
                " at=" + Cb2Log.F(now));
            ObservationAdapter.Emit("plan.condition_lost", now,
                new EventFields()
                    .Add("plan_id", plan.PlanId)
                    .Add("old_conditions", plan.Conditions.Describe())
                    .Add("new_conditions", conditions.Describe())
                    .ToString());
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
            ObservationAdapter.Emit("plan.invalidated", now,
                new EventFields()
                    .Add("plan_id", plan.PlanId)
                    .Add("goal", plan.Goal)
                    .Add("reason", reason.ToString())
                    .Add("detail", detail)
                    .ToString());

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
                    ObservationAdapter.Emit("task.closed", now,
                        new EventFields()
                            .Add("task_id", t.TaskId)
                            .Add("plan_id", t.PlanId)
                            .Add("formation_key", t.FormationKey)
                            .Add("reason", reason.ToString())
                            .Add("written", t.WrittenAt >= 0.0)
                            .Add("feedback", t.Feedback.ToString())
                            .Add("qualified_now", false)
                            .Add("note", "task_belongs_to_other_plan")
                            .ToString());
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
                ObservationAdapter.Emit("task.closed", now,
                    new EventFields()
                        .Add("task_id", t.TaskId)
                        .Add("plan_id", t.PlanId)
                        .Add("formation_key", t.FormationKey)
                        .Add("reason", reason.ToString())
                        .Add("written", t.WrittenAt >= 0.0)
                        .Add("feedback", t.Feedback.ToString())
                        .Add("qualified_now", qualified)
                        .ToString());

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
            // P2 §6：每个对象每次实际关闭最多一次退出尝试；
            // 登记与去重状态由 Core 台账持有（ExitStopLedger），此处只消费其判定。
            if (closed.ObjectIdentity.Length == 0) return;
            // 迟到的旧关闭回调：该对象若已被更新分配接管，不得再登记，
            // 否则这条旧退出会在入队后重新成为拥有者并丢掉保护。
            if (!_exits.TryRegisterIfNotTakenOver(closed.ObjectIdentity, closed.TaskId))
            {
                Cb2Log.Line("EXIT_STOP_SUPPRESSED",
                    "task=" + closed.TaskId +
                    " obj=" + closed.ObjectIdentity +
                    " reason=" + (_exits.IsRegistered(closed.ObjectIdentity, closed.TaskId)
                        ? "already_registered_for_this_close"
                        : "object_taken_over_by_newer_assignment") +
                    " owner=" + _exits.OwnerOf(closed.ObjectIdentity) +
                    " at=" + Cb2Log.F(now));
                return;
            }

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
                OriginTaskId = closed.TaskId,
            };
            _tasks.Add(exit);
            // 退出任务登记成立后立即接管该对象的归属：此后旧退出任务
            // 不得再对这个对象写令、撤权或清槽。
            _exits.TakeOwnership(closed.ObjectIdentity, exit.TaskId);
            Cb2Log.Line("EXIT_STOP_QUEUED",
                exit.Describe() +
                " origin_plan=" + closed.PlanId +
                " at=" + Cb2Log.F(now));
        }

        // 退出动作不因未获选就反复尝试：到点未完成即如实记录并撤权。
        // 安全时限从入队时刻起算，不因提交或获选而重置；
        // 已提交、已获选、已转交但未写出的退出任务都必须受此期限覆盖（脚本 §7.2）。
        internal void SweepExitStops(Mission mission)
        {
            if (mission == null) return;
            double now = SafeTime(mission);
            foreach (var task in _tasks.OpenTasks())
            {
                if (!BattlePlan.IsExitTask(task)) continue;
                if (task.WrittenAt >= 0.0) continue;
                // 该对象已被新的分配接管时，这条旧退出任务只关闭自己，
                // 不得再撤该编队的行为权重（那是新分配的活动）。
                bool owned = _exits.MayActOn(task.ObjectIdentity, task.OriginTaskId, task.TaskId);
                double since = now - task.CreatedAt;
                if (since >= TestParameters.SafetyCancelLatencySeconds)
                {
                    _tasks.CloseTask(task.TaskId, now);
                    Cb2Log.Line("EXIT_STOP_NOT_APPLIED",
                        task.Describe() +
                        " reason=not_completed_in_time" +
                        " submitted=" + (task.SubmittedAt >= 0.0 ? 1 : 0) +
                        " selected=" + (task.SelectedAt >= 0.0 ? 1 : 0) +
                        " waited=" + Cb2Log.F(since) +
                        " ownership=" + (owned ? "own" : "taken_over") +
                        " at=" + Cb2Log.F(now));
                    if (owned)
                    {
                        DisarmBehaviorsFor(task.FormationKey, "ExitNotApplied", now);
                    }
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
        // 逐 tick 调用（FeedbackTick 与决策点都会进）；到期的第一个现有主线程 tick
        // 即完成关闭，不等 3 s 决策节奏（脚本 §5.2/§6.3）。
        private void SweepExpiredSelections(double now)
        {
            foreach (var task in _tasks.OpenTasks())
            {
                if (task.SubmittedAt < 0.0) continue;
                // 截止前已经真实获选的正常任务保留原执行规则，不按等待期限掐断。
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

        // 普通任务的获选等待截止是否已过。用于拒绝截止后才到来的真实获选与写令：
        // 停止再次投递不能让此前已 arm 的候选在截止后取得写令资格（脚本 §6.3）。
        private static bool SelectionWindowClosed(FormationTask task, double now)
        {
            if (task == null) return false;
            if (BattlePlan.IsExitTask(task)) return false;
            if (task.SubmittedAt < 0.0) return false;
            return now >= task.SelectionDeadline();
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
            // 归属保护对普通任务与退出任务都适用，但判据不同：
            //   普通任务 —— 只判「对象是否已被更新分配接管」，不套退出专用的登记条件
            //              （登记是退出流程的事，套上来会让未登记的普通任务永远无法恢复）。
            //   退出任务 —— 走 MayActOn：登记成立（本次关闭是它代表的那次）且未被新分配接管。
            // 两类都必须先过归属，再谈原有恢复条件：旧、新任务可能写同一种命令，
            // 实际值相等不足以证明旧任务仍有权恢复。
            Func<bool> owned;
            if (BattlePlan.IsExitTask(task))
                owned = () => _exits.MayActOn(task.ObjectIdentity, task.OriginTaskId, task.TaskId);
            else
                owned = () => string.IsNullOrEmpty(task.ObjectIdentity) ||
                              _exits.StillOwnedBy(task.ObjectIdentity, task.TaskId);

            if (!owned())
            {
                Cb2Log.Line("REVOKE_SKIPPED",
                    "task=" + task.TaskId +
                    " reason=object_taken_over_by_new_assignment" +
                    " is_exit=" + (BattlePlan.IsExitTask(task) ? 1 : 0) +
                    " owner=" + _exits.OwnerOf(task.ObjectIdentity) +
                    " at=" + Cb2Log.F(now));
                return;
            }

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
            ObservationAdapter.Emit("revoke.executed", now,
                new EventFields()
                    .Add("task_id", task.TaskId)
                    .Add("formation_key", guarded.Key)
                    .Add("ok", ok)
                    .Add("error", err)
                    .Add("order_before_revoke", before)
                    .Add("order_after_revoke", after)
                    .ToString());
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
            _lastCoordinationLogAt = -1.0;
            _maxSnapshotAgeSeen = 0.0;
            _staleSnapshotCount = 0;
            _freshSnapshotCount = 0;
            _firstCommandSeen = false;
            _exitStopAttempts = 0;
            _exits.ClearMission();
            _lastWriteAt = -1.0;
            Cb2Log.Line("MISSION_START", "mission_key=" + _missionKey);
            ObservationAdapter.Resolve(0.0);
            ObservationAdapter.Emit("mission.start", 0.0,
                new EventFields()
                    .Add("mission_key", _missionKey)
                    .Add("scene", _missionKey)
                    .ToString());
        }

        internal void OnMissionEnd()
        {
            var t = CurrentMissionTime();

            ObservationAdapter.Emit("mission.end", t,
                new EventFields()
                    .Add("mission_key", _missionKey)
                    .ToString());
            ObservationAdapter.Shutdown();

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
                ObservationAdapter.Emit("task.closed", t,
                    new EventFields()
                        .Add("task_id", task.TaskId)
                        .Add("plan_id", task.PlanId)
                        .Add("formation_key", task.FormationKey)
                        .Add("reason", "MissionEnded")
                        .ToString());
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
                    // 实际活跃成员质心与有效人数：与上面的参考位置分列，
                    // 用于判读成员级读取是否真的产出数据（ok=0 表示退回参考位置）。
                    " active_ok=" + (r.HasActiveCenter ? 1 : 0) +
                    " active_n=" + r.ActiveMemberCount +
                    " active_center=(" + Cb2Log.F(r.ActiveCenterX) + "," + Cb2Log.F(r.ActiveCenterY) + ")" +
                    " member_digest=" + (r.MemberDigest.Length == 0 ? "<empty>" : r.MemberDigest) +
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

        // 读回原生实际命令目标点。GetPosition 返回 Vec2，取不到即返回 false，
        // 不用当前位置或请求值顶替。
        private static bool TryReadOrderTarget(Formation f, out double x, out double y)
        {
            x = 0.0;
            y = 0.0;
            try
            {
                var order = f.GetReadonlyMovementOrderReference();
                var pos = order.GetPosition(f);
                x = pos.x;
                y = pos.y;
                return !double.IsNaN(x) && !double.IsNaN(y);
            }
            catch (Exception)
            {
                return false;
            }
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
