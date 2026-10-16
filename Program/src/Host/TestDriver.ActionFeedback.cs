using System;
using System.Collections.Generic;
using CombatBrain2.Core;
using TaleWorlds.MountAndBlade;
using AgentState = TaleWorlds.Core.AgentState;

namespace CombatBrain2.Host
{
    internal sealed partial class TestDriver
    {
        // P3 单次评价：在 DecisionTick 的 CanContinue 提前返回之前调用。
        // 返回 true 表示本轮已由结果评价作出决定（调用方应结束本次决策）。
        private bool ActionFeedbackStep(Mission mission, BattleState state, PlanConditions conditions, double now)
        {
            // 继任等待期优先：旧攻势已关闭、退出动作尚未确认应用时不产生新分配。
            if (_feedback.WaitingForExitConfirmation)
            {
                DecayStandby(now);
                return true;
            }

            var plan = _plans.Current;
            if (plan == null) return false;

            // 有限撤回计划有自己的生命周期，不回到条件创建路径。
            if (string.Equals(plan.Goal, ActionFeedback.GoalWithdraw, StringComparison.Ordinal))
            {
                if (!plan.IsClosed) WithdrawTick(plan, now);
                return true;
            }

            // 已知负反馈未消退：不再按旧 P2 条件重建同一攻势，保持交回原生的明确状态。
            if (_feedback.OffensiveBlocked)
            {
                if (_lastFeedbackBlockLogAt < 0.0 || now - _lastFeedbackBlockLogAt >= 9.0)
                {
                    _lastFeedbackBlockLogAt = now;
                    Cb2Log.Line("ACTION_BLOCKED",
                        "plan=" + plan.PlanId +
                        " reason=loss_not_resolved" +
                        " stopped=" + (_feedback.Stopped ? 1 : 0) +
                        " at=" + Cb2Log.F(now));
                }
                return true;
            }

            // 基线只在主力推进普通命令真正写入时建立（见 TryExecuteTask 调用点）。
            // 未写令前不能评价：t0 前的损失不算该行动的结果（派单 §3.1）。
            if (!_feedback.Frozen) return false;

            // 冻结集合属于某个确定的计划身份；当前计划换了身份且没有已知负反馈时，
            // 这次行动已经结束：清掉基线，交回正常 P2 条件路径。
            // 继任等待期的新计划尚未上位，基线不参与判正负，身份不符也不算这次行动结束。
            if (!string.Equals(_feedback.PlanId, plan.PlanId, StringComparison.Ordinal))
            {
                Cb2Log.Line("ACTION_FEEDBACK_RESET",
                    "frozen_plan=" + _feedback.PlanId +
                    " current_plan=" + plan.PlanId +
                    " reason=plan_identity_changed" +
                    " at=" + Cb2Log.F(now));
                _feedback = new ActionFeedbackTracker();
                _withdrawHoldDone.Clear();
                return false;
            }

            string evaluatePlanId = _feedback.PlanId;
            RefreshFeedbackIntegrity();

            bool targetPresent;
            int targetActive;
            bool regrouped;
            _feedback.ObserveSurvival(_snapshot, out targetPresent, out targetActive, out regrouped);

            // 风险门读数：以拟新增主力目标点为中心统计 30 m 内敌方非坐骑有效人数，
            // 并与三职责当前有效人数和各自继续线比较。中心未知即不追加。
            bool riskCenterKnown = false;
            int enemyWithinRadius = 0;
            int myRoleUnits = 0;
            bool rolesAboveLine = true;
            string riskDetail = "not_a_coordination_plan";
            if (_coordination && plan.Geometry != null)
            {
                double cx, cy;
                if (ProjectedMainCenter(plan, out cx, out cy))
                {
                    var roleRoles = CollectRoleAssignments(plan);
                    var gate = ActionFeedback.RiskGate(_snapshot, state.MyTeamIndex,
                                                       roleRoles, cx, cy);
                    riskCenterKnown = gate.CenterKnown;
                    enemyWithinRadius = gate.EnemyWithinRadius;
                    myRoleUnits = gate.MyRoleUnits;
                    rolesAboveLine = gate.RolesAboveContinueLine;
                    riskDetail = gate.Detail;
                }
                else
                {
                    riskDetail = "projected_center_unknown";
                }
            }
            else
            {
                // 非协同计划没有三职责风险门，按合同只对协同追加设门。
                riskCenterKnown = true;
            }

            var input = new ActionFeedbackInput
            {
                Now = now,
                IntegrityOk = _feedbackIntegrityOk,
                IntegrityNote = _feedbackIntegrityNote,
                TargetObjectPresent = targetPresent,
                TargetActiveE0Members = targetActive,
                RegroupedAway = regrouped,
                MyForceContinueLineHolds = !conditions.StrengthCritical,
                RangedAdvantageStillHolds =
                    conditions.RangedEdge >= TestParameters.RangedEdgeContinueThreshold,
                PlanStillValid = !plan.IsClosed,
                RiskCenterKnown = riskCenterKnown,
                EnemyWithinRiskRadius = enemyWithinRadius,
                MyRoleUnits = myRoleUnits,
                RolesAboveContinueLine = rolesAboveLine,
            };

            if (evaluatePlanId == plan.PlanId) _feedbackEvalCount++;

            var ev = _feedback.Evaluate(input);
            Cb2Log.Line("ACTION_FEEDBACK",
                "plan=" + plan.PlanId +
                " t0=" + Cb2Log.F(ev.T0) +
                " now=" + Cb2Log.F(ev.Now) +
                " stable=" + Cb2Log.F(ev.StableSeconds) +
                " NE=" + ev.NE +
                " NF=" + ev.NF +
                " DE=" + ev.DE +
                " DF=" + ev.DF +
                " routed=" + ev.RoutedCount +
                " unknown=" + ev.UnknownCount +
                " LE=" + Cb2Log.F(ev.LE) +
                " LF=" + Cb2Log.F(ev.LF) +
                " rf=" + Cb2Log.F(ev.Rf) +
                " HRf=" + Cb2Log.F(ev.HRf) +
                " B=" + Cb2Log.F(ev.B) +
                " C=" + Cb2Log.F(ev.C) +
                " U=" + Cb2Log.F(ev.U) +
                " complete=" + (ev.Complete ? 1 : 0) +
                " streak=" + ev.ConsecutiveCount +
                " decision=" + ev.Decision +
                " reason=" + ev.Reason +
                " exception=" + (ev.ExceptionUsed ? 1 : 0) +
                (ev.ExceptionUsed ? " exception_reason=" + ev.ExceptionReason : ""));

            ObservationAdapter.Emit("action.feedback", now,
                new EventFields()
                    .Add("plan_id", plan.PlanId)
                    .Add("t0", ev.T0)
                    .Add("stable_seconds", ev.StableSeconds)
                    .Add("ne", ev.NE).Add("nf", ev.NF)
                    .Add("de", ev.DE).Add("df", ev.DF)
                    .Add("routed", ev.RoutedCount).Add("unknown", ev.UnknownCount)
                    .Add("le", ev.LE).Add("lf", ev.LF)
                    .Add("rate_known", ev.RateKnown ? 1 : 0)
                    .Add("rf", ev.RateKnown ? ev.Rf : double.NaN)
                    .Add("b", ev.B).Add("c", ev.C).Add("u", ev.U)
                    .Add("complete", ev.Complete ? 1 : 0)
                    .Add("streak", ev.ConsecutiveCount)
                    .Add("decision", ev.Decision.ToString())
                    .Add("reason", ev.Reason)
                    .ToString());

            if (ev.Decision == ActionFeedbackDecision.Continue) return false;

            BeginSuccessorPlan(state, plan, ev, now);
            return true;
        }

        // 在主力推进普通命令真正写入这一刻冻结基线：t0 就是这次写入的 Mission 时刻，
        // 观察对象与分母都在此采样确定，之后不再重置（派单 §3.1）。
        private void PrepareFeedbackBaseline(Mission mission, FormationTask task, double now)
        {
            if (task == null) return;
            if (!_coordination) return;
            if (task.Role != FormationRole.Main) return;
            if (!string.Equals(task.Reason, CoordinationDriver.ReasonMainAdvance, StringComparison.Ordinal)) return;
            if (_feedback.Frozen) return;
            if (_missionKey.Length == 0) return;

            var state = _state;
            if (state == null) return;

            string mainKey = task.FormationKey ?? "";
            if (mainKey.Length == 0) return;

            // 写入这一刻同一主线程重新采样：t0 前的损失不能算作本行动结果，
            // 用上一个评价点的快照会把写令前后的成员变化混在一起。
            var frozenSnapshot = FormationSampler.Sample(mission, now);
            if (frozenSnapshot == null) return;

            string detail;
            string inputId = state.PlanInputId ?? "";
            if (!_feedback.Freeze(frozenSnapshot, state.MyTeamIndex, mainKey, task.PlanId, inputId, now, out detail))
            {
                Cb2Log.Line("ACTION_FEEDBACK_FREEZE_FAILED",
                    "plan=" + task.PlanId +
                    " task=" + task.TaskId +
                    " main=" + mainKey +
                    " detail=" + detail +
                    " at=" + Cb2Log.F(now));
                return;
            }

            _withdrawPlanId = "";
            _withdrawHoldLogged = false;
            _withdrawStableLogged = false;
            _withdrawStartedAt = -1.0;
            _withdrawHoldDone.Clear();
            _feedbackEvalCount = 0;

            Cb2Log.Line("ACTION_FEEDBACK_FROZEN",
                "plan=" + task.PlanId +
                " task=" + task.TaskId +
                " main=" + mainKey +
                " t0=" + Cb2Log.F(now) +
                " NE=" + _feedback.NE +
                " NF=" + _feedback.NF +
                " observe=" + _feedback.ObserveObjectIdentity +
                " detail=" + detail);
        }

        // 继任等待期的逐 tick 推进：只有等旧攻势的退出动作在本对象上被真正应用之后，
        // 才允许新计划上位；到既有退出截止仍未应用就明确终止本次接续，
        // 保留"停止失败"的如实状态，不无限等待、不重投、不提前接管。
        private void DecayStandby(double now)
        {
            if (!_feedback.WaitingForExitConfirmation) return;

            var d = ActionFeedback.DecideSuccessorStandby(CollectStandbyFacts(now));

            if (d.Verdict == ActionFeedback.StandbyVerdict.Confirmed)
            {
                _feedback.ExitStandbyCompleted = true;
                bool stopped, blocked;
                _feedback.AfterStandbyCompleted(out stopped, out blocked);
                Cb2Log.Line("SUCCESSOR_EXIT_CONFIRMED",
                    "pending=" + _feedback.PendingStandbyPlanId +
                    " waited=" + Cb2Log.F(now - _feedback.StandbyDeadlineAt +
                                           TestParameters.SafetyCancelLatencySeconds) +
                    " detail=" + d.Detail +
                    " at=" + Cb2Log.F(now));
                CompleteStandby(now);
                return;
            }

            if (d.Verdict == ActionFeedback.StandbyVerdict.TimedOut)
            {
                _feedback.MarkStandbyFailed();
                Cb2Log.Line("SUCCESSOR_STANDBY_FAILED",
                    "pending=" + _feedback.PendingStandbyPlanId +
                    " reason=" + d.Detail +
                    " limit=" + Cb2Log.F(TestParameters.SafetyCancelLatencySeconds) +
                    " at=" + Cb2Log.F(now));
            }
        }

        // 退出确认门的输入：旧计划身份、逐对象的退出结果与时刻、截止与继任有效性。
        // 退出任务写出后立即被关闭，因此不能靠扫描 OpenTasks() 判断"是否已应用"——
        // 必须从台账与已关闭任务记录里取事实（派单 §2 整改1）。
        private ActionFeedback.SuccessorStandby CollectStandbyFacts(double now)
        {
            var facts = new ActionFeedback.SuccessorStandby
            {
                OldPlanId = _standbyOldPlanId,
                PendingPlanId = _feedback.PendingStandbyPlanId,
                Now = now,
                DeadlineAt = _feedback.StandbyDeadlineAt,
                RequiredObjects = new List<string>(),
                Exits = new List<ActionFeedback.ExitOutcome>(),
                SuccessorPrepared = _standbyPlan != null,
                SuccessorInvalidReason = _standbyPlan != null
                    ? StandbyInvalidReason(_standbyPlan) : "plan_not_prepared",
            };

            // 必需对象 = 本次失效真正为其排队了退出停止的对象，也就是旧计划的退出任务集。
            // 不能改取"旧计划全部普通任务的对象"：同一计划里被版本重绑换掉的旧任务
            // （RebindRole/RebindSameObject 会直接关闭旧任务）从未承担最终分配，
            // 也不会有退出结果，把它们算作必需项会让确认门永远缺项（派单 §2 整改1）。
            var all = _tasks.AllTasksSorted();
            var exitTasks = new Dictionary<string, FormationTask>(StringComparer.Ordinal);
            for (int i = 0; i < all.Count; i++)
            {
                var t = all[i];
                if (t == null) continue;
                if (!BattlePlan.IsExitTask(t)) continue;
                if (!string.Equals(t.PlanId, facts.OldPlanId, StringComparison.Ordinal)) continue;
                if (t.ObjectIdentity == null || t.ObjectIdentity.Length == 0) continue;
                FormationTask prev;
                if (!exitTasks.TryGetValue(t.ObjectIdentity, out prev)) exitTasks[t.ObjectIdentity] = t;
            }

            foreach (var kv in exitTasks)
            {
                facts.RequiredObjects.Add(kv.Key);
            }
            facts.RequiredObjects.Sort(StringComparer.Ordinal);

            // 逐对象的退出结果：合并退出任务记录（已关闭的也要看）与归属台账。
            for (int i = 0; i < facts.RequiredObjects.Count; i++)
            {
                string obj = facts.RequiredObjects[i];
                var outcome = new ActionFeedback.ExitOutcome { ObjectIdentity = obj };

                FormationTask exit;
                if (exitTasks.TryGetValue(obj, out exit))
                {
                    outcome.Written = exit.WrittenAt >= 0.0;
                    outcome.WrittenAt = exit.WrittenAt;
                    // 已经明确判为"未在时限内应用"的退出不得补算成功。
                    outcome.Unapplied =
                        exit.IsClosed && exit.FailureReason == PlanInvalidReason.None &&
                        !outcome.Written;
                }

                // 对象归属已被更新的分配拿走：这条退出不再代表本次接续的停止结果。
                string owner = _exits.OwnerOf(obj);
                outcome.OwnershipTakenByNewAssignment =
                    owner.Length != 0 && !IsOriginExitOwner(exit, owner);

                facts.Exits.Add(outcome);
            }

            return facts;
        }

        // 归属台账里的 owner 就是这条退出自己，才算"未被新分配接管"。
        private static bool IsOriginExitOwner(FormationTask exit, string owner)
        {
            return exit != null && string.Equals(exit.TaskId, owner, StringComparison.Ordinal);
        }

        // 退出确认之后才把新分配改名为继任计划身份并上场：
        // 计划对象在等待期就已经取号构好，但既不 Adopt 也不占槽、不武装。
        private void CompleteStandby(double now)
        {
            string newPlanId = _feedback.PendingStandbyPlanId;
            string oldPlanId = _standbyOldPlanId;
            var successor = _standbyPlan;

            _feedback.WaitingForExitConfirmation = false;
            _feedback.ExitStandbyCompleted = false;
            _feedback.PendingStandbyPlanId = "";
            _feedback.StandbyDeadlineAt = -1.0;
            _standbyPlan = null;
            _standbyOldPlanId = "";

            if (successor == null || newPlanId.Length == 0)
            {
                Cb2Log.Line("SUCCESSOR_STANDBY_FAILED",
                    "pending=" + newPlanId + " reason=plan_not_prepared at=" + Cb2Log.F(now));
                return;
            }

            // 确认之后重新核对最新状态：对象仍是本方的合格对象、
            // 三职责齐备且互不相同，都以当前快照与当前计划版本为准。
            string invalid = StandbyInvalidReason(successor);
            if (invalid.Length != 0)
            {
                Cb2Log.Line("SUCCESSOR_STANDBY_FAILED",
                    "pending=" + newPlanId +
                    " reason=" + invalid +
                    " at=" + Cb2Log.F(now));
                return;
            }

            // 身份先移交再上场：新分配从实际公布时刻起算，
            // 与旧计划身份彻底分离，旧计划不会再匹配到这个跟踪器。
            if (!_feedback.RetargetStandbyPlan(oldPlanId, newPlanId))
            {
                Cb2Log.Line("SUCCESSOR_STANDBY_FAILED",
                    "pending=" + newPlanId +
                    " reason=identity_not_retargeted tracker_plan=" + _feedback.PlanId +
                    " old=" + oldPlanId +
                    " at=" + Cb2Log.F(now));
                return;
            }

            _plans.Adopt(successor);
            PublishSuccessorTasks(successor, oldPlanId, now);

            Cb2Log.Line("ACTION_REPLAN",
                "old=" + oldPlanId +
                " new=" + newPlanId +
                " decision=" + _standbyDecision +
                " reason=" + _standbyReason +
                " tasks=" + successor.Tasks.Count +
                " standby_waited=1 at=" + Cb2Log.F(now));
        }

        // 确认门用当前快照重新核对：任务上记的对象身份必须仍是当前同一对象、仍属本方
        // 且仍受 AI 控制、兵种类别仍允许该职责、人数仍在继续线以上，互不相同；协同继任
        // 还必须三职责齐备且通过当前风险门，任一不成立就不公布计划（独立验收 R1）。
        private string StandbyInvalidReason(BattlePlan successor)
        {
            if (successor == null) return "plan_not_prepared";
            if (_snapshot == null) return "no_snapshot";
            if (_state == null) return "no_state";

            // 撤退休整计划的任务不承担协同职责（Role=None），只核对对象仍合格且互不相同；
            // 三职责齐备与风险门只适用于协同继任计划（派单 §2 整改1）。
            bool coordination = string.Equals(successor.Goal, PlanBuilder.GoalCoordination,
                                             StringComparison.Ordinal);

            var facts = new List<SuccessorRoleFact>();
            var roles = new List<RoleAssignment>();
            for (int i = 0; i < successor.Tasks.Count; i++)
            {
                var t = successor.Tasks[i];
                if (t == null) continue;
                if (BattlePlan.IsExitTask(t)) continue;

                var fact = new SuccessorRoleFact
                {
                    Role = t.Role,
                    FormationKey = t.FormationKey ?? "",
                    ObjectIdentity = t.ObjectIdentity ?? "",
                    FormationClass = t.FormationClass ?? "",
                    Kind = t.Kind,
                    BaselineUnits = t.RoleBaselineUnits,
                };
                var rec = _snapshot.FindByKey(t.FormationKey);
                if (rec != null)
                {
                    fact.Found = true;
                    fact.CurrentObjectIdentity = rec.ObjectIdentity ?? "";
                    fact.ActiveMemberCount = rec.ActiveMemberCount;
                    fact.TeamIndex = rec.TeamIndex;
                    fact.HasActiveCenter = rec.HasActiveCenter;
                    fact.IsAiControlled = rec.IsAiControlled;
                }
                facts.Add(fact);

                if (t.Role != FormationRole.None)
                {
                    roles.Add(new RoleAssignment
                    {
                        Role = t.Role,
                        FormationKey = t.FormationKey ?? "",
                        ObjectIdentity = t.ObjectIdentity ?? "",
                        BaselineUnits = t.RoleBaselineUnits,
                    });
                }
            }

            string why = ActionFeedback.SuccessorRolesStillValid(facts, _state.MyTeamIndex, coordination);
            if (why.Length != 0) return why;

            if (coordination)
            {
                double cx, cy;
                if (!ProjectedMainCenterOf(successor, out cx, out cy)) return "projected_center_unknown";
                var gate = ActionFeedback.RiskGate(_snapshot, _state.MyTeamIndex, roles, cx, cy);
                if (!gate.Allowed) return "risk_" + gate.Detail;
            }

            return "";
        }

        // 继任计划自己的主力实际质心：必须在计划公布之前按该计划将要承担 Main 的任务取，
        // 不能用还绑在旧计划上的协调驱动槽位（那时 successor 尚未 Adopt）。
        private bool ProjectedMainCenterOf(BattlePlan plan, out double cx, out double cy)
        {
            cx = 0.0;
            cy = 0.0;
            if (plan == null) return false;
            if (_snapshot == null || _state == null) return false;

            FormationTask mainTask = null;
            for (int i = 0; i < plan.Tasks.Count; i++)
            {
                var t = plan.Tasks[i];
                if (t == null || BattlePlan.IsExitTask(t)) continue;
                if (t.Role != FormationRole.Main) continue;
                mainTask = t;
                break;
            }
            if (mainTask == null) return false;

            var rec = _snapshot.FindByKey(mainTask.FormationKey);
            if (rec == null || !rec.HasActiveCenter) return false;

            return ActionFeedback.ProjectedMainTarget(rec.ActiveCenterX, rec.ActiveCenterY,
                                                      _state.EnemyCenterX, _state.EnemyCenterY,
                                                      out cx, out cy);
        }

        // 在旧攻势关闭之前就把继任意图准备好：只取号与构建计划对象，
        // 不 Adopt、不入槽、不武装。等待期结束后改用同一个计划身份上场。
        private BattlePlan PrepareStandbyPlan(BattleState state, string newId, double now,
                                              ActionFeedbackDecision decision, string reason,
                                              double utility, List<FormationTask> sources,
                                              string oldPlanId)
        {
            BattlePlan plan;
            if (decision == ActionFeedbackDecision.StopLoss ||
                decision == ActionFeedbackDecision.Disengage)
            {
                plan = PlanBuilder.BuildWithdraw(state, newId, now, sources,
                    "p3_withdraw " + reason);
            }
            else
            {
                plan = PlanBuilder.BuildSuccessorCoordination(state, newId, now,
                    oldPlanId, now, reason, utility);
            }
            return plan;
        }

        // 继任计划：正反馈追加或负反馈有限撤回。两者都只先把旧攻势关闭并把继任意图
        // 准备好，新分配一律要等旧退出被确认应用之后才 Adopt/Publish（派单 §2 整改1）。
        private void BeginSuccessorPlan(BattleState state, BattlePlan current,
                                        ActionFeedbackEvaluation ev, double now)
        {
            string oldPlanId = current.PlanId;

            if (ev.Decision == ActionFeedbackDecision.StopLoss ||
                ev.Decision == ActionFeedbackDecision.Disengage)
            {
                // 负反馈：优先结束攻势；仍合格且方向/世界位置可用时改用有限撤回。
                var sources = new List<FormationTask>();
                var open = _tasks.OpenTasks();
                for (int i = 0; i < open.Count; i++)
                {
                    var t = open[i];
                    if (t == null) continue;
                    if (BattlePlan.IsExitTask(t)) continue;
                    if (!string.Equals(t.PlanId, oldPlanId, StringComparison.Ordinal)) continue;
                    sources.Add(t);
                }

                if (!StillCurrentPlan(oldPlanId))
                {
                    Cb2Log.Line("ACTION_REPLAN",
                        "old=" + oldPlanId + " new=- decision=" + ev.Decision +
                        " reason=" + ev.Reason + " invalidate=rejected");
                    return;
                }

                // 撤回目标在旧任务关闭前算好：旧对象与当前质心都还在开放任务上。
                // 顺序仍是「先关旧、再退出、后公布」，这里只做计算，不产生写令。
                string newId = _plans.NextPlanId();
                var withdraw = PrepareStandbyPlan(state, newId, now, ev.Decision, ev.Reason,
                    ev.U, sources, oldPlanId);
                if (withdraw == null)
                {
                    // 方向或世界位置不可用：不编造安全退路，只如实终止旧攻势。
                    InvalidatePlan(PlanInvalidReason.NotEffective,
                        "action_feedback " + ev.Reason + " withdraw_unavailable", now, oldPlanId);
                    _feedback.Stopped = true;
                    _feedback.OffensiveBlocked = true;
                    Cb2Log.Line("ACTION_REPLAN",
                        "old=" + oldPlanId + " new=- decision=" + ev.Decision +
                        " reason=" + ev.Reason + " withdraw=none");
                    return;
                }

                InvalidatePlan(PlanInvalidReason.NotEffective,
                    "action_feedback " + ev.Reason, now, oldPlanId);
                EnterStandby(state, withdraw, oldPlanId, ev, now);
                return;
            }

            if (ev.Decision == ActionFeedbackDecision.Reinforce)
            {
                if (!StillCurrentPlan(oldPlanId))
                {
                    Cb2Log.Line("ACTION_REPLAN",
                        "old=" + oldPlanId + " new=- decision=" + ev.Decision +
                        " reason=" + ev.Reason + " invalidate=rejected");
                    return;
                }

                string newId = _plans.NextPlanId();
                var successor = PrepareStandbyPlan(state, newId, now, ev.Decision, ev.Reason,
                    ev.U, null, oldPlanId);
                if (successor == null)
                {
                    InvalidatePlan(PlanInvalidReason.NotEffective,
                        "action_feedback " + ev.Reason, now, oldPlanId);
                    Cb2Log.Line("ACTION_REPLAN",
                        "old=" + oldPlanId + " new=- decision=" + ev.Decision +
                        " reason=" + ev.Reason + " successor=none");
                    return;
                }

                InvalidatePlan(PlanInvalidReason.NotEffective,
                    "action_feedback " + ev.Reason, now, oldPlanId);
                EnterStandby(state, successor, oldPlanId, ev, now);
                return;
            }
        }

        // 进入继任等待期：此时旧攻势已经关闭、退出动作已排队，但一条新分配都还没有公布。
        // 等待期内计划只被持有，既不 Adopt 也不占槽、不武装，旧计划身份仍是 _plans.Current。
        private void EnterStandby(BattleState state, BattlePlan prepared, string oldPlanId,
                                  ActionFeedbackEvaluation ev, double now)
        {
            _standbyPlan = prepared;
            _standbyOldPlanId = oldPlanId;
            _standbyDecision = ev.Decision;
            _standbyReason = ev.Reason;

            var next = _feedback.CloneForSuccessor(oldPlanId);
            next.Reinforced = ev.Decision == ActionFeedbackDecision.Reinforce;
            next.WaitingForExitConfirmation = true;
            next.ExitStandbyCompleted = false;
            next.PendingStandbyPlanId = prepared.PlanId;
            next.StandbyDeadlineAt = now + TestParameters.SafetyCancelLatencySeconds;
            _feedback = next;

            if (ev.Decision == ActionFeedbackDecision.StopLoss ||
                ev.Decision == ActionFeedbackDecision.Disengage)
            {
                _withdrawPlanId = prepared.PlanId;
                _withdrawHoldLogged = false;
                _withdrawStableLogged = false;
                _withdrawStartedAt = now;
            }

            Cb2Log.Line("SUCCESSOR_STANDBY",
                "old=" + oldPlanId +
                " pending=" + prepared.PlanId +
                " decision=" + ev.Decision +
                " reason=" + ev.Reason +
                " tasks=" + prepared.Tasks.Count +
                " deadline=" + Cb2Log.F(next.StandbyDeadlineAt) +
                " note=awaiting_exit_confirmation at=" + Cb2Log.F(now));
        }

        // 继任计划只在旧计划仍是当前计划时才允许生成：旧任务触发的评价
        // 不得取消或替换已经换了身份的新计划。
        private bool StillCurrentPlan(string planId)
        {
            var current = _plans.Current;
            return current != null
                && !current.IsClosed
                && string.Equals(current.PlanId, planId, StringComparison.Ordinal);
        }

        private void PublishSuccessorTasks(BattlePlan plan, string oldPlanId, double now)
        {
            if (plan == null) return;
            for (int i = 0; i < plan.Tasks.Count; i++)
            {
                var task = plan.Tasks[i];
                if (task == null) continue;
                task.Source = ProposalSource.PhaseTransition;
                task.RoleAssignedAt = now;
                _tasks.Add(task);
                PublishAssignment(task, now);
                Cb2Log.Line("TASK_CREATED",
                    "plan=" + plan.PlanId +
                    " task=" + task.TaskId +
                    " role=" + task.Role +
                    " kind=" + task.Kind +
                    " formation=" + task.FormationKey +
                    " reason=" + task.Reason +
                    " inherited_from=" + oldPlanId);
                Cb2Log.Line("SUCCESSOR_TASK",
                    "old=" + oldPlanId +
                    " new=" + plan.PlanId +
                    " task=" + task.TaskId +
                    " role=" + task.Role +
                    " kind=" + task.Kind +
                    " formation=" + task.FormationKey +
                    " canonical=" + task.HasCanonicalTarget);
            }

            if (_coordination && CoordinationDriver.IsCoordinationPlan(plan))
            {
                _coordinationDriver.Bind(plan);
                _coordinationDriver.BindTasks(plan);
                Cb2Log.Line("COORDINATION_BOUND",
                    "plan=" + plan.PlanId +
                    " main=" + (_coordinationDriver.MainWaitTask != null
                        ? _coordinationDriver.MainWaitTask.TaskId : "-") +
                    " guard=" + (_coordinationDriver.GuardTask != null
                        ? _coordinationDriver.GuardTask.TaskId : "-") +
                    " support=" + (_coordinationDriver.SupportTask != null
                        ? _coordinationDriver.SupportTask.TaskId : "-") +
                    " inherited_from=" + oldPlanId);
            }
        }

        // 风险中心＝拟新增主力目标：原点必须用当前实际质心、方向用当前敌方质心，
        // 不能用旧 plan.Geometry.MainX/MainY（那是计划构建时的读数，会掩盖实际位移）。
        private bool ProjectedMainCenter(BattlePlan plan, out double cx, out double cy)
        {
            cx = 0.0;
            cy = 0.0;
            if (plan == null) return false;
            if (_snapshot == null || _state == null) return false;

            var mainTask = _coordinationDriver != null ? _coordinationDriver.MainWaitTask : null;
            if (mainTask == null || mainTask.IsClosed)
                mainTask = _coordinationDriver != null ? _coordinationDriver.AdvanceTask : null;
            if (mainTask == null || mainTask.IsClosed) return false;

            var rec = _snapshot.FindByKey(mainTask.FormationKey);
            if (rec == null || !rec.HasActiveCenter) return false;

            return ActionFeedback.ProjectedMainTarget(rec.ActiveCenterX, rec.ActiveCenterY,
                                                      _state.EnemyCenterX, _state.EnemyCenterY,
                                                      out cx, out cy);
        }

        // 三职责的当前合法分配：只按当前计划版本的权威槽位取，不遍历 plan.Tasks
        // （历史版本、已关闭的 Main 等待任务与 Main 推进任务共用一个键，遍历会重复计人数）。
        private List<RoleAssignment> CollectRoleAssignments(BattlePlan plan)
        {
            var roles = new List<RoleAssignment>();
            if (plan == null) return roles;
            if (_coordinationDriver == null) return roles;
            if (!CoordinationDriver.IsCoordinationPlan(plan)) return roles;

            AppendRoleAssignment(plan, roles, FormationRole.Main, _coordinationDriver.MainWaitTask);
            AppendRoleAssignment(plan, roles, FormationRole.Main, _coordinationDriver.AdvanceTask);
            AppendRoleAssignment(plan, roles, FormationRole.Guard, _coordinationDriver.GuardTask);
            AppendRoleAssignment(plan, roles, FormationRole.Support, _coordinationDriver.SupportTask);
            return roles;
        }

        private void AppendRoleAssignment(BattlePlan plan, List<RoleAssignment> roles,
                                          FormationRole role, FormationTask task)
        {
            if (task == null || task.IsClosed) return;
            if (!string.Equals(task.PlanId, plan.PlanId, StringComparison.Ordinal)) return;
            if (task.Revision != plan.Revision) return;
            roles.Add(new RoleAssignment
            {
                Role = role,
                FormationKey = task.FormationKey ?? "",
                ObjectIdentity = task.ObjectIdentity ?? "",
                BaselineUnits = task.RoleBaselineUnits,
            });
        }

        // 逐样本完整性：Mission 未变化、快照无重复键、基线成员集合可读。
        // 任一条不成立时该窗口不能用于判正负（派单 §3.1）。
        private void RefreshFeedbackIntegrity()
        {
            _feedbackIntegrityOk = true;
            _feedbackIntegrityNote = "";

            if (_snapshot == null)
            {
                _feedbackIntegrityOk = false;
                _feedbackIntegrityNote = "no_snapshot";
                return;
            }
            if (!string.Equals(_snapshot.MissionKey, _feedback.MissionKey, StringComparison.Ordinal))
            {
                _feedbackIntegrityOk = false;
                _feedbackIntegrityNote = "mission_changed";
                return;
            }
            string dupDetail;
            if (_snapshot.HasDuplicateKeys(out dupDetail))
            {
                _feedbackIntegrityOk = false;
                _feedbackIntegrityNote = "duplicate_keys " + dupDetail;
                return;
            }
            for (int i = 0; i < _snapshot.Records.Count; i++)
            {
                var r = _snapshot.Records[i];
                if (r.TeamIndex != _state.MyTeamIndex) continue;
                if (r.UnitCount > 0 && !r.HasActiveCenter)
                {
                    _feedbackIntegrityOk = false;
                    _feedbackIntegrityNote = "friendly_center_unreadable key=" + r.Key();
                    return;
                }
            }

            // 冻结成员逐个可解释：每个 t0 成员要么仍能在本样本按稳定身份定位到活跃成员
            // （含转编、分离到其他 Formation），要么已有可信既有终态。无法解释的消失
            // 必须阻断该窗口的正负判定，不能补为 0 损失（派单 §3.3）。
            string coverageDetail;
            if (!_feedback.MissingCoverage(_snapshot.MissionKey, _snapshot, out coverageDetail))
            {
                _feedbackIntegrityOk = false;
                _feedbackIntegrityNote = "member_coverage " + coverageDetail;
                return;
            }
        }

        // 有限撤回计划的生命周期：到达各自目标 5 m 内转 Hold（独立新任务）；
        // 20 s 未到或命令 3 s 效果失败即终止撤回 Plan 并按既有退出路径处理，不循环重试。
        private void WithdrawTick(BattlePlan plan, double now)
        {
            if (_withdrawStartedAt < 0.0)
            {
                _withdrawStartedAt = plan.CreatedAt;
                _withdrawHoldLogged = false;
            }

            int total = 0;
            int arrived = 0;
            int failed = 0;
            foreach (var task in _tasks.OpenTasks())
            {
                if (task == null) continue;
                if (BattlePlan.IsExitTask(task)) continue;
                if (!string.Equals(task.PlanId, plan.PlanId, StringComparison.Ordinal)) continue;
                if (task.Kind != TaskKind.Reposition) continue;
                total++;
                if (task.Feedback == TaskFeedbackState.Failed) failed++;

                var rec = _snapshot != null ? _snapshot.FindByKey(task.FormationKey) : null;
                // 到点判据比的是有效成员质心与战术目标 T（派单 §2.2 第 5/6 条）：
                // RequestedX/Y 现在是写给引擎的锚点请求，不能拿来当质心到达的证据。
                double goalX = task.HasCanonicalTarget ? task.CanonicalX : task.RequestedX;
                double goalY = task.HasCanonicalTarget ? task.CanonicalY : task.RequestedY;
                if (rec != null && rec.HasActiveCenter && task.HasPosition &&
                    ActionFeedback.Distance(rec.ActiveCenterX, rec.ActiveCenterY, goalX, goalY)
                        <= TestParameters.P3WithdrawArrivalMeters)
                {
                    arrived++;
                    if (task.WrittenAt < 0.0) continue;
                    if (task.Feedback != TaskFeedbackState.Effective) continue;
                    if (_withdrawHoldDone.Contains(task.TaskId)) continue;

                    // 到点后转 Hold：独立新任务，不改写原任务的种类。
                    _withdrawHoldDone.Add(task.TaskId);
                    var hold = new FormationTask
                    {
                        TaskId = plan.PlanId + "-hold-" + (plan.Tasks.Count + 1),
                        PlanId = plan.PlanId,
                        Ordinal = task.Ordinal + 100,
                        FormationKey = task.FormationKey,
                        ObjectIdentity = task.ObjectIdentity,
                        FormationClass = task.FormationClass,
                        TeamIndex = task.TeamIndex,
                        FormationIndex = task.FormationIndex,
                        Kind = TaskKind.Hold,
                        Reason = ActionFeedback.ReasonWithdrawHold,
                        CreatedAt = now,
                        Role = FormationRole.None,
                        RoleBaselineUnits = task.RoleBaselineUnits,
                        RoleAssignedAt = now,
                        Revision = plan.Revision,
                        Source = ProposalSource.PhaseTransition,
                        OriginTaskId = task.TaskId,
                    };
                    plan.Tasks.Add(hold);
                    _tasks.Add(hold);
                    PublishAssignment(hold, now);
                    Cb2Log.Line("WITHDRAW_HOLD_CREATED",
                        "plan=" + plan.PlanId +
                        " origin=" + task.TaskId +
                        " hold=" + hold.TaskId +
                        " key=" + hold.FormationKey +
                        " at=" + Cb2Log.F(now));
                }
            }

            bool deadlinePassed = (now - _withdrawStartedAt) >= TestParameters.P3WithdrawDeadlineSeconds;
            if (arrived >= total && total > 0 && !_withdrawHoldLogged)
            {
                _withdrawHoldLogged = true;
                Cb2Log.Line("WITHDRAW_ARRIVED",
                    "plan=" + plan.PlanId +
                    " arrived=" + arrived + "/" + total +
                    " elapsed=" + Cb2Log.F(now - _withdrawStartedAt) +
                    " at=" + Cb2Log.F(now));
                if (!_withdrawStableLogged)
                {
                    _withdrawStableLogged = true;
                    Cb2Log.Line("WITHDRAW_STABLE",
                        "plan=" + plan.PlanId +
                        " note=offensive_stopped_and_hold_established" +
                        " at=" + Cb2Log.F(now));
                }
                return;
            }

            // 命令 3 s 效果窗口内未生效：立即终止撤回 Plan，走既有退出路径。
            // 与 20 s 到点合并成同一个终止出口，不循环重试。
            if (failed > 0)
            {
                Cb2Log.Line("WITHDRAW_COMMAND_FAILED",
                    "plan=" + plan.PlanId +
                    " failed=" + failed + "/" + total +
                    " elapsed=" + Cb2Log.F(now - _withdrawStartedAt) +
                    " at=" + Cb2Log.F(now));
                InvalidatePlan(PlanInvalidReason.NotEffective,
                    "withdraw_command_failed failed=" + failed + "/" + total, now, plan.PlanId);
                return;
            }

            if (deadlinePassed)
            {
                Cb2Log.Line("WITHDRAW_DEADLINE",
                    "plan=" + plan.PlanId +
                    " arrived=" + arrived + "/" + total +
                    " elapsed=" + Cb2Log.F(now - _withdrawStartedAt) +
                    " limit=" + Cb2Log.F(TestParameters.P3WithdrawDeadlineSeconds) +
                    " at=" + Cb2Log.F(now));
                InvalidatePlan(PlanInvalidReason.NotEffective,
                    "withdraw_deadline arrived=" + arrived + "/" + total, now, plan.PlanId);
            }
        }
    }
}
