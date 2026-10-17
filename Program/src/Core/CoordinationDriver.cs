using System;
using System.Collections.Generic;
using System.Globalization;

namespace CombatBrain2.Core
{
    // P2 同一开放 Plan 下三职责协同的状态机。纯逻辑：不读引擎、不写令，
    // 只根据外部喂进来的"实际读数"决定任务阶段与依赖是否成立。
    internal enum CoordinationPhase
    {
        None = 0,
        // 主力等待保护与辅助到位。
        AwaitingDependencies,
        // 依赖已成立，主力推进任务已指定。
        MainAdvancing,
        // 主力推进已写入或已撤权，协同不再改变动作。
        Finished,
    }

    // 一次依赖读数的结果。到位与人数前提分列，便于如实报告具体值。
    internal sealed class DependencyObservation
    {
        internal bool ObjectPresent;
        internal bool PositionKnown;
        internal double X;
        internal double Y;
        internal int CurrentUnits;
        internal bool Eligible;
        internal string FormationClass = "";
        // 有效活跃成员集合摘要：与 CurrentUnits 一起构成"还是这批人、还有这么多人"的判据。
        internal string MemberDigest = "";

        internal DependencyObservation()
        {
        }

        internal DependencyObservation(bool objectPresent, bool positionKnown, double x, double y,
                                       int currentUnits, bool eligible, string formationClass)
        {
            ObjectPresent = objectPresent;
            PositionKnown = positionKnown;
            X = x;
            Y = y;
            CurrentUnits = currentUnits;
            Eligible = eligible;
            FormationClass = formationClass ?? "";
        }

        internal RolePremiseFailure PremiseFailure(int baselineUnits)
        {
            return PlanBuilder.PremiseFailureOf(ObjectPresent, FormationClass,
                CurrentUnits, baselineUnits, Eligible, PositionKnown);
        }
    }

    // 依赖裁决的可读结果：本 tick 是否允许释放主力推进。
    internal sealed class DependencyOutcome
    {
        internal bool MainAdvanceAllowed;
        internal bool GuardReady;
        internal bool SupportReady;
        internal RolePremiseFailure GuardFailure;
        internal RolePremiseFailure SupportFailure;
        // 各职责独立计时：窗口自其实际写令时刻开始，未写令即未开始。
        internal bool GuardExpired;
        internal bool SupportExpired;
        // 依赖失效的职责归类：前提失效与「前提仍在但命令/到位窗口不再成立」分开报告，
        // 否则前提为 None 时无法说明究竟是哪一侧失去依据。
        internal RoleDependencyLoss GuardLoss;
        internal RoleDependencyLoss SupportLoss;
        internal string Detail = "";
    }

    // 一次完整重分配中的单个职责替换。同一批里的多条替换共用同一个版本递增，
    // 用来表达「原辅助改保护、另一对象补辅助」这种同版本内的职责转换（派单 §6）。
    internal sealed class RoleReplacement
    {
        internal FormationRole Role;
        internal FormationTask Old;
        internal FormationRecord Replacement;
    }

    internal sealed class CoordinationDriver
    {
        private readonly ProposalArbiter _arbiter = new ProposalArbiter();
        private readonly DependencyReadiness _guardReadiness = new DependencyReadiness();
        private readonly DependencyReadiness _supportReadiness = new DependencyReadiness();

        private string _planId = "";
        private CoordinationPhase _phase = CoordinationPhase.None;
        private BattlePlan _plan;
        // 推进任务代次：阶段转换不增 plan_revision，同一计划可多次释放主力推进，
        // 每次都必须换新身份，否则第二次真实获选会被 already_written 拒绝（独立验收缺陷 2）。
        private int _advanceGeneration;

        internal ProposalArbiter Arbiter { get { return _arbiter; } }
        internal CoordinationPhase Phase { get { return _phase; } }
        internal DependencyReadiness GuardReadiness { get { return _guardReadiness; } }
        internal DependencyReadiness SupportReadiness { get { return _supportReadiness; } }

        // 本计划的主力等待任务、保护任务、辅助任务与推进任务。
        internal FormationTask MainWaitTask;
        internal FormationTask GuardTask;
        internal FormationTask SupportTask;
        internal FormationTask AdvanceTask;
        internal string AdvanceTaskId = "";

        internal void Bind(BattlePlan plan)
        {
            _plan = plan;
            _planId = plan == null ? "" : plan.PlanId;
            _phase = plan == null ? CoordinationPhase.None : CoordinationPhase.AwaitingDependencies;
            _arbiter.ClearMission();
            _guardReadiness.Reset();
            _supportReadiness.Reset();
            MainWaitTask = null;
            GuardTask = null;
            SupportTask = null;
            AdvanceTask = null;
            AdvanceTaskId = "";
            _advanceGeneration = 0;
        }

        internal static bool IsCoordinationPlan(BattlePlan plan)
        {
            return plan != null && plan.Goal == PlanBuilder.GoalCoordination && plan.Geometry != null;
        }

        // 从计划里挑出三个职责任务。角色不唯一即视为计划不可用。
        internal bool BindTasks(BattlePlan plan)
        {
            MainWaitTask = null;
            GuardTask = null;
            SupportTask = null;
            if (plan == null) return false;
            foreach (var t in plan.Tasks)
            {
                if (BattlePlan.IsExitTask(t)) continue;
                if (t.Role == FormationRole.Main && MainWaitTask == null) MainWaitTask = t;
                else if (t.Role == FormationRole.Guard && GuardTask == null) GuardTask = t;
                else if (t.Role == FormationRole.Support && SupportTask == null) SupportTask = t;
            }
            return MainWaitTask != null && GuardTask != null && SupportTask != null;
        }

        // 喂一次实际读数：两层判据都要过（几何到位 + 人数/资格前提）。
        // 未到位会清空连续窗口，符合"必须连续至少 1 s 的两次或更多有效读取"。
        internal DependencyOutcome Observe(double missionTime, DependencyObservation guard,
                                           DependencyObservation support)
        {
            var result = new DependencyOutcome();
            // 推进期间也必须继续观察依赖：主力推进任务写入后，
            // 保护/辅助离开到位区仍须能被发现，不能因阶段转换就停止读数。
            if (_phase != CoordinationPhase.AwaitingDependencies &&
                _phase != CoordinationPhase.MainAdvancing)
            {
                result.Detail = "phase=" + _phase;
                return result;
            }

            var plan = _plan;
            if (plan == null || plan.Geometry == null)
            {
                result.Detail = "no_geometry";
                return result;
            }

            result.GuardFailure = ObserveOne(guard, GuardTask, _guardReadiness,
                plan.Geometry, missionTime, true);
            result.SupportFailure = ObserveOne(support, SupportTask, _supportReadiness,
                plan.Geometry, missionTime, false);

            result.GuardReady = result.GuardFailure == RolePremiseFailure.None &&
                                _guardReadiness.Ready;
            result.SupportReady = result.SupportFailure == RolePremiseFailure.None &&
                                  _supportReadiness.Ready;
            result.GuardLoss = DependencyReadiness.LossOf(plan, GuardTask, true,
                result.GuardFailure, _guardReadiness);
            result.SupportLoss = DependencyReadiness.LossOf(plan, SupportTask, false,
                result.SupportFailure, _supportReadiness);
            result.MainAdvanceAllowed = DependencyReadiness.MainAdvanceAllowed(
                plan, GuardTask, SupportTask, _guardReadiness, _supportReadiness);

            // 各职责分别计时：一个到期不截断另一个尚未开始的窗口。
            result.GuardExpired = RelationshipExpired(plan, GuardTask, _guardReadiness, missionTime);
            result.SupportExpired = RelationshipExpired(plan, SupportTask, _supportReadiness, missionTime);

            result.Detail = "guard=" + Describe(guard, result.GuardFailure, _guardReadiness) +
                            " support=" + Describe(support, result.SupportFailure, _supportReadiness);
            return result;
        }

        private static RolePremiseFailure ObserveOne(DependencyObservation obs, FormationTask task,
                                                     DependencyReadiness readiness,
                                                     CoordinationGeometry g,
                                                     double missionTime, bool isGuard)
        {
            if (task == null || obs == null)
            {
                if (readiness != null) readiness.Note(missionTime, false);
                return RolePremiseFailure.ObjectGone;
            }
            var failure = obs.PremiseFailure(task.RoleBaselineUnits);
            bool inPosition = false;
            if (failure == RolePremiseFailure.None)
            {
                inPosition = isGuard
                    ? PlanBuilder.GuardInPosition(g, obs.X, obs.Y)
                    : PlanBuilder.SupportInPosition(g, obs.X, obs.Y);
            }
            readiness.Note(missionTime, inPosition, obs.MemberDigest);
            return failure;
        }

        // 把一次观察结果归类到具体职责。两侧同时失去依据时按「谁的前提/命令先坏」
        // 排序上报；两侧同类的并列情形按 Guard 在前，由调用方按实际读数取舍。
        internal static RoleDependencyLoss LostRoleOf(DependencyOutcome outcome, out FormationRole role)
        {
            role = FormationRole.None;
            if (outcome == null) return RoleDependencyLoss.None;
            bool guardLost = DependencyReadiness.IsLost(outcome.GuardLoss);
            bool supportLost = DependencyReadiness.IsLost(outcome.SupportLoss);
            if (!guardLost && !supportLost) return RoleDependencyLoss.None;
            if (guardLost && supportLost)
            {
                // 前提失效比窗口断开更硬：先报前提失效那一侧。
                bool guardPremise = outcome.GuardLoss == RoleDependencyLoss.PremiseFailure;
                bool supportPremise = outcome.SupportLoss == RoleDependencyLoss.PremiseFailure;
                role = guardPremise || !supportPremise ? FormationRole.Guard : FormationRole.Support;
                return guardPremise || !supportPremise ? outcome.GuardLoss : outcome.SupportLoss;
            }
            role = guardLost ? FormationRole.Guard : FormationRole.Support;
            return guardLost ? outcome.GuardLoss : outcome.SupportLoss;
        }

        private static string Describe(DependencyObservation obs, RolePremiseFailure failure,
                                       DependencyReadiness readiness)
        {
            if (obs == null) return "<none>";
            return failure + "/reads=" + readiness.ReadCount +
                   "/ready=" + (readiness.Ready ? 1 : 0) +
                   "/pos=(" + obs.X.ToString("F1", CultureInfo.InvariantCulture) + "," +
                   obs.Y.ToString("F1", CultureInfo.InvariantCulture) + ")" +
                   "/units=" + obs.CurrentUnits;
        }

        // 依赖成立后关掉主力等待任务，并原子指定新的推进任务 ID。
        // 同一角色/对象/分配不变时不增 revision（派单 §3）。
        internal FormationTask CreateAdvanceTask(BattlePlan plan, double now)
        {
            if (plan == null || MainWaitTask == null) return null;
            if (_phase != CoordinationPhase.AwaitingDependencies) return null;
            // 依赖未成立不得指定推进任务：单次读数、前提失效、任一职责离开到位区都不放行。
            if (!DependencyReadiness.MainAdvanceAllowed(plan, GuardTask, SupportTask,
                    _guardReadiness, _supportReadiness)) return null;

            var main = MainWaitTask;
            _advanceGeneration++;
            var advance = new FormationTask
            {
                // 推进任务每次释放都换新身份：阶段转换不增 plan_revision，
                // 沿用固定 ID 会让第二次推进撞上第一次的写令额度（独立验收缺陷 2）。
                TaskId = plan.PlanId + "-task-4-g" + _advanceGeneration,
                PlanId = plan.PlanId,
                Ordinal = 4,
                FormationKey = main.FormationKey,
                ObjectIdentity = main.ObjectIdentity,
                FormationClass = main.FormationClass,
                TeamIndex = main.TeamIndex,
                FormationIndex = main.FormationIndex,
                Kind = TaskKind.Reposition,
                Reason = ReasonMainAdvance,
                CreatedAt = now,
                Revision = plan.Revision,
                Role = FormationRole.Main,
                RoleBaselineUnits = main.RoleBaselineUnits,
                RoleAssignedAt = now,
                Source = ProposalSource.PhaseTransition,
                DependsOnTaskId = GuardTask == null ? "" : GuardTask.TaskId,
                GuardsTaskId = GuardTask == null ? "" : GuardTask.TaskId,
                SupportsTaskId = SupportTask == null ? "" : SupportTask.TaskId,
            };
            AdvanceTask = advance;
            AdvanceTaskId = advance.TaskId;
            SetCanonicalTarget(plan, advance);

            main.IsClosed = true;
            main.ClosedAt = now;
            main.FailureReason = PlanInvalidReason.None;
            main.FailureDetail = "dependency_satisfied_replaced_by=" + advance.TaskId;

            // 等待任务与推进任务共用同一个编队键：不清旧裁决槽，推进任务的提案
            // 会被槽内的等待任务永久判为"被取代"，即使真实获选也写不出去（派单 §3 要求清其槽位）。
            ClearSlotOf(main);

            _phase = CoordinationPhase.MainAdvancing;
            return advance;
        }

        // 关系建立超时：每个职责分别从自己的实际写令时刻起算二十秒。
        // 任一职责到期仍未到位即报告该职责超时；两个职责的窗口互相独立，
        // 一个到期不截断另一个尚未开始的窗口。
        internal bool RelationshipExpired(BattlePlan plan, double now)
        {
            return RelationshipExpired(plan, GuardTask, _guardReadiness, now) ||
                   RelationshipExpired(plan, SupportTask, _supportReadiness, now);
        }

        // 单个职责是否超时：尚未写令即窗口未开始，不算超时；
        // 已写令且已到位即该职责已满足，不再受二十秒约束。
        internal static bool RelationshipExpired(BattlePlan plan, FormationTask task,
                                                 DependencyReadiness readiness, double now)
        {
            if (plan == null || task == null) return false;
            if (task.WrittenAt < 0.0) return false;
            if (readiness != null && readiness.Ready) return false;
            return now >= task.WrittenAt + TestParameters.CoordinationRelationshipDeadlineSeconds;
        }

        // 分别查询各职责的到位截止时刻：尚未写令返回 -1，表示窗口未开始。
        internal double GuardDeadline()
        {
            return GuardTask != null && GuardTask.WrittenAt >= 0.0
                ? GuardTask.WrittenAt + TestParameters.CoordinationRelationshipDeadlineSeconds : -1.0;
        }

        internal double SupportDeadline()
        {
            return SupportTask != null && SupportTask.WrittenAt >= 0.0
                ? SupportTask.WrittenAt + TestParameters.CoordinationRelationshipDeadlineSeconds : -1.0;
        }

        internal bool GuardExpired(BattlePlan plan, double now)
        {
            return RelationshipExpired(plan, GuardTask, _guardReadiness, now);
        }

        internal bool SupportExpired(BattlePlan plan, double now)
        {
            return RelationshipExpired(plan, SupportTask, _supportReadiness, now);
        }

        // 职责前提失效：在第一个确认主线程 tick 冻结、清槽、撤旧行为影响。
        internal RolePremiseFailure PremiseFailure(BattlePlan plan, DependencyObservation guard,
                                                   DependencyObservation support)
        {
            if (plan == null) return RolePremiseFailure.None;
            var g = guard == null ? RolePremiseFailure.ObjectGone
                                  : guard.PremiseFailure(GuardTask == null ? 0 : GuardTask.RoleBaselineUnits);
            if (g != RolePremiseFailure.None) return g;
            if (support == null) return RolePremiseFailure.ObjectGone;
            return support.PremiseFailure(SupportTask == null ? 0 : SupportTask.RoleBaselineUnits);
        }

        internal void MarkFinished() { _phase = CoordinationPhase.Finished; }

        internal void ClearMission()
        {
            _arbiter.ClearMission();
            _guardReadiness.Reset();
            _supportReadiness.Reset();
            _planId = "";
            _phase = CoordinationPhase.None;
            _plan = null;
            MainWaitTask = null;
            GuardTask = null;
            SupportTask = null;
            AdvanceTask = null;
            AdvanceTaskId = "";
            _advanceGeneration = 0;
        }

        // 计划级重分配入口：一次完整重分配只递增一次版本，由本入口统一递增。
        // 新任务与既有三任务同版本；旧普通任务全部关闭；换保护或辅助必须重置就绪窗口。
        internal FormationTask ReassignRole(BattlePlan plan, FormationTask old, FormationRole role,
                                           FormationRecord replacement, double now)
        {
            if (plan == null || old == null || replacement == null) return null;
            var only = new List<RoleReplacement>
            {
                new RoleReplacement { Role = role, Old = old, Replacement = replacement },
            };
            return ReassignRoles(plan, only, now);
        }

        // 一次完整重分配：同一版本下可以同时改多个职责（派单 §6 L139
        // 「允许原辅助改保护、原未承担任务的合格编队补辅助」）。
        // 旧任务全部关闭并换新 ID，返回本批换到新对象的那个职责任务。
        internal FormationTask ReassignRoles(BattlePlan plan, IList<RoleReplacement> replacements, double now)
        {
            if (plan == null || replacements == null || replacements.Count == 0) return null;
            PlanLedger.BumpRevision(plan);

            // 版本推进后旧版本的推进任务失去依据：先关闭，避免它作为旧 revision 的开放
            // 普通任务继续占用 plan.Tasks，拖住新职责任务的写令授权。
            if (AdvanceTask != null && !AdvanceTask.IsClosed)
            {
                AdvanceTask.IsClosed = true;
                AdvanceTask.ClosedAt = now;
                AdvanceTask.FailureDetail = "advance_superseded_by_revision=" +
                                            plan.Revision.ToString(CultureInfo.InvariantCulture);
            }
            AdvanceTask = null;
            AdvanceTaskId = "";

            // 推进阶段依赖失效后回到等待阶段：主力必须重新有一条本版本的等待任务，
            // 否则三职责不完整，后续即使依赖再次成立也无法释放主力。
            EnsureMainWaitTask(plan, now);

            FormationTask primary = null;
            foreach (var r in replacements)
            {
                if (r == null || r.Old == null || r.Replacement == null) continue;
                var next = RebindRole(plan, r.Old, r.Role, r.Replacement, now);
                if (next != null && primary == null) primary = next;
            }
            if (primary == null) return null;

            // 同一完整分配下三任务版本必须一致：未换的职责把普通任务用新 ID 重新绑定。
            RebindUnchanged(plan, replacements, now);
            // 三任务都变成新版本 ID 之后，把依赖集整体指向本版本，
            // 否则新分配仍指向已关闭的旧任务（独立验收缺陷 4）。
            RebindDependencies(plan);
            _phase = CoordinationPhase.AwaitingDependencies;
            // 新 ID 绑定后，本计划参与编队的裁决结果全部作废重开；
            // 只清替补键会让未换职责的编队继续引用旧任务，使新任务永远被判被取代。
            ClearRoleSlots();

            // 未换职责的 RebindUnchanged 会把职责任务整体换掉，返回本批第一个
            // 实际换到新对象的职责任务；调用方只用它做日志与发布定位。
            foreach (var r in replacements)
            {
                if (r == null || r.Old == null) continue;
                if (r.Role == FormationRole.Guard && GuardTask != null && !GuardTask.IsClosed) return GuardTask;
                if (r.Role == FormationRole.Support && SupportTask != null && !SupportTask.IsClosed) return SupportTask;
                if (r.Role == FormationRole.Main && MainWaitTask != null && !MainWaitTask.IsClosed) return MainWaitTask;
            }
            return primary;
        }

        // 未换职责的普通任务随新版本重新绑定新 ID，旧 ID 关闭。
        // 本批已换对象的职责不再重绑：否则会把刚建好的新任务再换一次，
        // 同一版本内出现两条同职责任务（第二批替换只能有一条生效）。
        private void RebindUnchanged(BattlePlan plan, IList<RoleReplacement> replacements, double now)
        {
            bool mainDone = RoleReplaced(replacements, FormationRole.Main);
            bool guardDone = RoleReplaced(replacements, FormationRole.Guard);
            bool supportDone = RoleReplaced(replacements, FormationRole.Support);

            if (!mainDone && MainWaitTask != null && !MainWaitTask.IsClosed)
                MainWaitTask = RebindSameObject(plan, MainWaitTask, now);
            if (!guardDone && GuardTask != null && !GuardTask.IsClosed)
                GuardTask = RebindSameObject(plan, GuardTask, now);
            if (!supportDone && SupportTask != null && !SupportTask.IsClosed)
                SupportTask = RebindSameObject(plan, SupportTask, now);
        }

        private static bool RoleReplaced(IList<RoleReplacement> replacements, FormationRole role)
        {
            if (replacements == null) return false;
            foreach (var r in replacements)
            {
                if (r != null && r.Role == role) return true;
            }
            return false;
        }

        // 依赖释放时主力等待任务被关闭；回到等待阶段必须补一条本版本的等待任务，
        // 否则三职责不完整，即使依赖再次成立也没有任务可转为推进（实机 19.1s 后即此形状）。
        private void EnsureMainWaitTask(BattlePlan plan, double now)
        {
            if (MainWaitTask == null) return;
            if (!MainWaitTask.IsClosed) return;
            MainWaitTask = RebindSameObject(plan, MainWaitTask, now);
        }

        // 版本推进后本计划参与编队的裁决结果全部作废重开。
        // 只清替补键会让未换职责的编队继续引用旧任务，使同对象的重新绑定任务
        // 被永久判为"被取代"（实机 key=1/2 连续 superseded_by=plan-1-task-3）。
        private void ClearRoleSlots()
        {
            ClearSlotOf(MainWaitTask);
            ClearSlotOf(GuardTask);
            ClearSlotOf(SupportTask);
        }

        private void ClearSlotOf(FormationTask task)
        {
            if (task == null || task.FormationKey.Length == 0) return;
            _arbiter.CloseCurrent(task.FormationKey);
        }

        // 版本推进/换对象后重建的任务要继承计划几何的规范目标点：
        // 提案阶段还没写令，RequestedX/Y 为空，首次竞争只能靠这个点算误差（派单 §5 第 3 条）。
        private static void SetCanonicalTarget(BattlePlan plan, FormationTask task)
        {
            if (plan == null || task == null || plan.Geometry == null) return;
            var g = plan.Geometry;
            if (task.Role == FormationRole.Main) task.SetCanonicalTarget(g.MainX, g.MainY);
            else if (task.Role == FormationRole.Guard) task.SetCanonicalTarget(g.GuardX, g.GuardY);
            else if (task.Role == FormationRole.Support) task.SetCanonicalTarget(g.SupportX, g.SupportY);
        }

        // 某职责的规范目标点。候选排序与任务目标点必须同源，
        // 否则「距对应位置的距离」在不同口径下排序（派单 §3.1 L81）。
        internal static bool RolePoint(BattlePlan plan, FormationRole role, out double x, out double y)
        {
            x = 0.0;
            y = 0.0;
            if (plan == null || plan.Geometry == null) return false;
            var g = plan.Geometry;
            if (role == FormationRole.Main) { x = g.MainX; y = g.MainY; return true; }
            if (role == FormationRole.Guard) { x = g.GuardX; y = g.GuardY; return true; }
            if (role == FormationRole.Support) { x = g.SupportX; y = g.SupportY; return true; }
            return false;
        }

        // 主力推进任务的专有原因，全工程只在 CreateAdvanceTask 一处赋值。
        internal const string ReasonMainAdvance = "coordination_main_advance";

        // 写令前是否需要复核保护/辅助依赖仍然就绪。只有推进任务需要：
        // 重分配产生的新主力等待任务同样来自 PhaseTransition，但那一刻新的
        // 保护/辅助尚未写令，按推进任务复核必然不过，新分配会整场写不出命令
        // （实测 p2-A-20261014-022054 的 plan-1-task-92-1）。
        internal static bool RequiresAdvanceDependencyRecheck(FormationTask task)
        {
            if (task == null) return false;
            if (task.Role != FormationRole.Main) return false;
            return string.Equals(task.Reason, ReasonMainAdvance, StringComparison.Ordinal);
        }

        // 同一角色/对象/分配不变，但版本推进：换新任务 ID，职责与依赖集保持。
        private FormationTask RebindSameObject(BattlePlan plan, FormationTask old, double now)
        {
            var next = new FormationTask
            {
                TaskId = plan.PlanId + "-task-" + (90 + plan.Revision).ToString(CultureInfo.InvariantCulture) +
                         "-" + old.Ordinal.ToString(CultureInfo.InvariantCulture),
                PlanId = plan.PlanId,
                Ordinal = old.Ordinal,
                FormationKey = old.FormationKey,
                ObjectIdentity = old.ObjectIdentity,
                FormationClass = old.FormationClass,
                TeamIndex = old.TeamIndex,
                FormationIndex = old.FormationIndex,
                Kind = old.Kind,
                Reason = old.Reason,
                CreatedAt = now,
                Revision = plan.Revision,
                Role = old.Role,
                RoleBaselineUnits = old.RoleBaselineUnits,
                RoleAssignedAt = now,
                // 依赖集不在这里复制：由 RebindDependencies 在三个任务都换成本版本 ID 后统一重绑。
                ReplacesTaskId = old.TaskId,
                Source = ProposalSource.PhaseTransition,
            };
            old.IsClosed = true;
            old.ClosedAt = now;
            old.FailureDetail = "revision_rebound_to=" + next.TaskId;
            SetCanonicalTarget(plan, next);
            plan.Tasks.Add(next);
            return next;
        }

        // 替代指定：职责换到新对象时，旧普通任务全部用新 ID 重新绑定。
        // 版本由计划级重分配入口统一递增，本方法只使用已确定的 plan.Revision。
        internal FormationTask RebindRole(BattlePlan plan, FormationTask old, FormationRole role,
                                         FormationRecord replacement, double now)
        {
            if (plan == null || old == null || replacement == null) return null;
            var next = new FormationTask
            {
                // 同版本内两个职责可能同时换对象（原辅助改保护、替补补辅助），
                // ID 必须带角色标签，否则两条新任务会撞成同一个 ID。
                TaskId = plan.PlanId + "-task-" + (90 + plan.Revision).ToString(CultureInfo.InvariantCulture) +
                         "-r" + plan.Revision.ToString(CultureInfo.InvariantCulture) + "-" + RoleTag(role),
                PlanId = plan.PlanId,
                Ordinal = 90 + plan.Revision,
                FormationKey = replacement.Key(),
                ObjectIdentity = replacement.ObjectIdentity,
                FormationClass = replacement.FormationClass,
                TeamIndex = replacement.TeamIndex,
                FormationIndex = replacement.FormationIndex,
                Kind = TaskKind.Reposition,
                Reason = old.Reason,
                CreatedAt = now,
                Revision = plan.Revision,
                Role = role,
                // 职责基线与判据同口径：始终取有效活跃成员数，不是引擎编队人数。
                RoleBaselineUnits = replacement.HasActiveCenter ? replacement.ActiveMemberCount : 0,
                RoleAssignedAt = now,
                ReplacesTaskId = old.TaskId,
                Source = ProposalSource.PhaseTransition,
            };
            old.IsClosed = true;
            old.ClosedAt = now;
            old.FailureDetail = "role_rebound_to=" + next.TaskId;

            if (role == FormationRole.Guard)
            {
                GuardTask = next;
                // 保护换对象：该职责的连续有效窗口作废，不得沿用旧窗口放行主力。
                _guardReadiness.Reset();
            }
            else if (role == FormationRole.Support)
            {
                SupportTask = next;
                _supportReadiness.Reset();
            }
            else
            {
                // 主力换对象：依赖集整体重绑，等待窗口清零。
                MainWaitTask = next;
                _guardReadiness.Reset();
                _supportReadiness.Reset();
            }
            plan.Tasks.Add(next);
            SetCanonicalTarget(plan, next);
            return next;
        }

        private static string RoleTag(FormationRole role)
        {
            if (role == FormationRole.Guard) return "guard";
            if (role == FormationRole.Support) return "support";
            if (role == FormationRole.Main) return "main";
            return "none";
        }

        // 版本推进后三个职责任务都换了身份，依赖集必须整体指向新版本的任务。
        // 沿用旧依赖会让新分配指向已关闭的旧任务（独立验收缺陷 4）：
        // 独立复核时新 Support 的 SupportsTaskId 仍是被关闭的旧主力任务。
        private static void RebindDependencies(BattlePlan plan)
        {
            if (plan == null) return;
            FormationTask main = null;
            FormationTask guard = null;
            FormationTask support = null;
            foreach (var t in plan.Tasks)
            {
                if (t == null || BattlePlan.IsExitTask(t)) continue;
                // plan.Tasks 累积历史版本任务，只认本版本尚未关闭的那三个。
                if (t.IsClosed) continue;
                if (t.Role == FormationRole.Main && main == null) main = t;
                else if (t.Role == FormationRole.Guard && guard == null) guard = t;
                else if (t.Role == FormationRole.Support && support == null) support = t;
            }
            if (main == null || guard == null || support == null) return;

            // 与建计划时同一形状：保护、辅助都依赖本版本的主力等待任务，
            // 主力的替换集列出本版本的另外两个职责。
            guard.DependsOnTaskId = main.TaskId;
            guard.GuardsTaskId = main.TaskId;
            support.DependsOnTaskId = main.TaskId;
            support.SupportsTaskId = main.TaskId;
            main.DependsOnTaskId = "";
            main.ReplacesTaskId = "|" + guard.TaskId + "|" + support.TaskId + "|";
        }
    }
}