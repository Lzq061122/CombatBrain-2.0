using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CombatBrain2.Core
{
    // 从同一份快照读出战场状态。只使用采样记录本身的字段，
    // 不新增引擎私有访问，也不在评价中途重新采样。
    internal static class BattleAssessment
    {
        internal static BattleState Read(FormationSnapshot snapshot, string planInputId)
        {
            if (snapshot == null) return null;

            var state = new BattleState
            {
                PlanInputId = planInputId,
                MissionKey = snapshot.MissionKey,
                SampledAt = snapshot.SampledAt,
            };

            // 本批我方是实际受控的非玩家 AI 队，敌方是与它对立的玩家队。
            // 归属按实际队关系判定，不硬编码 team 号，也不把"其余记录"泛化成一方。
            // 两类归属各须唯一确定，任一缺失就不生成状态。
            foreach (var r in snapshot.Records)
            {
                if (r.UnitCount <= 0) continue;
                if (r.TeamIndex < 0) continue;
                if (r.IsPlayerTeam)
                {
                    if (state.EnemyTeamIndex < 0) state.EnemyTeamIndex = r.TeamIndex;
                }
                else
                {
                    if (state.MyTeamIndex < 0) state.MyTeamIndex = r.TeamIndex;
                }
            }
            if (state.MyTeamIndex < 0 || state.EnemyTeamIndex < 0) return null;

            double myWeight = 0.0;
            double mySumX = 0.0;
            double mySumY = 0.0;
            double enemyWeight = 0.0;
            double enemySumX = 0.0;
            double enemySumY = 0.0;

            foreach (var r in snapshot.Records)
            {
                if (r.UnitCount <= 0) continue;
                if (r.TeamIndex == state.EnemyTeamIndex)
                {
                    state.EnemyFormations.Add(r);
                    state.EnemyUnitCount += r.UnitCount;
                    if (IsRanged(r.FormationClass))
                    {
                        state.EnemyRangedCount += r.UnitCount;
                    }
                    if (IsInfantry(r.FormationClass))
                    {
                        state.EnemyInfantryCount += r.UnitCount;
                    }
                    enemyWeight += r.UnitCount;
                    enemySumX += r.PosX * r.UnitCount;
                    enemySumY += r.PosY * r.UnitCount;
                }
                else if (r.TeamIndex == state.MyTeamIndex)
                {
                    state.MyFormations.Add(r);
                    state.MyUnitCount += r.UnitCount;
                    if (IsRanged(r.FormationClass))
                    {
                        state.MyRangedCount += r.UnitCount;
                    }
                    if (IsInfantry(r.FormationClass))
                    {
                        state.MyInfantryCount += r.UnitCount;
                    }
                    myWeight += r.UnitCount;
                    mySumX += r.PosX * r.UnitCount;
                    mySumY += r.PosY * r.UnitCount;
                }
            }

            if (myWeight > 0.0)
            {
                state.MyCenterX = mySumX / myWeight;
                state.MyCenterY = mySumY / myWeight;
            }
            if (enemyWeight > 0.0)
            {
                state.EnemyCenterX = enemySumX / enemyWeight;
                state.EnemyCenterY = enemySumY / enemyWeight;
            }

            return state;
        }

        internal static bool IsRanged(string formationClass)
        {
            return Contains(formationClass, "Ranged") || Contains(formationClass, "HorseArcher");
        }

        internal static bool IsInfantry(string formationClass)
        {
            return Contains(formationClass, "Infantry") && !IsRanged(formationClass);
        }

        private static bool Contains(string value, string token)
        {
            if (string.IsNullOrEmpty(value)) return false;
            return value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    // 从战场状态形成计划：目标、理由、承担任务的编队、继续条件。
    // 计划不包含具体 Order，命令由任务在真实获选后产生。
    internal static class PlanBuilder
    {
        internal const string GoalCoordination = "coordination_ranged_advantage";

        // P2 控制组合白名单。精确匹配，不沿用 IsRanged（后者把 HorseArcher 也算远程）。
        // 骑兵本单只开放 Hold/Reposition；骑兵 Engage 与所有 HorseArcher/未知类别一律拒绝。
        internal static bool ClassAllows(string formationClass, TaskKind kind)
        {
            if (string.IsNullOrEmpty(formationClass)) return false;
            if (kind != TaskKind.Hold && kind != TaskKind.Engage && kind != TaskKind.Reposition)
            {
                return false;
            }

            bool infantry = string.Equals(formationClass, "Infantry", StringComparison.OrdinalIgnoreCase);
            bool ranged = string.Equals(formationClass, "Ranged", StringComparison.OrdinalIgnoreCase);
            bool cavalry = string.Equals(formationClass, "Cavalry", StringComparison.OrdinalIgnoreCase);

            if (infantry || ranged) return true;
            if (cavalry) return kind != TaskKind.Engage;
            return false;
        }

        // P2 协同计划：同一开放 Plan 下三个不同对象分别承担主力/保护/辅助。
        // 只在远程优势成立时创建，且三支编队必须都能承担所分配的职责。
        internal static BattlePlan BuildCoordination(BattleState state, string planId, double now)
        {
            if (state == null) return null;

            var conditions = PlanConditions.From(state);
            if (!conditions.RangedAdvantageHolds) return null;
            if (conditions.StrengthCritical) return null;
            if (state.MyTeamIndex < 0 || state.EnemyTeamIndex < 0) return null;

            var candidates = CoordinationCandidates(state);
            // 三职责必须由不同对象承担：不足三支就不形成协同计划，不做兼任降级。
            if (candidates.Count < 3) return null;

            double enemyX, enemyY;
            if (!TryActiveCenter(state.EnemyFormations, out enemyX, out enemyY)) return null;

            // 主力职责先定：按稳定序取第一支，几何的原点 M0 用该编队自己的实际质心，
            // 不用全队加权中心（那是另一件事，不能拿来充当主力位置）。
            var main = candidates[0];
            double mainX, mainY;
            if (!MainOriginOf(main, out mainX, out mainY)) return null;

            var rest = new List<FormationRecord>();
            for (int i = 1; i < candidates.Count; i++) rest.Add(candidates[i]);

            double supportOriginX, supportOriginY;
            if (!MainOriginOf(rest[0], out supportOriginX, out supportOriginY)) return null;
            var geometry = CoordinationGeometry.Compute(mainX, mainY, enemyX, enemyY,
                                                        supportOriginX, supportOriginY);
            if (geometry == null) return null;

            // 几何定下后按职责的实际位置要求分配，不再按候选顺序硬性指派：
            // 距离规范点更近的承担该职责，减少无谓移动。
            FormationRecord guard;
            FormationRecord support;
            if (!AssignGuardAndSupport(geometry, rest, out guard, out support)) return null;

            var plan = new BattlePlan
            {
                PlanId = planId,
                MissionKey = state.MissionKey,
                PlanInputId = state.PlanInputId,
                CreatedAt = now,
                InputSampledAt = state.SampledAt,
                Conditions = conditions,
                Goal = GoalCoordination,
                Revision = 1,
                Geometry = geometry,
                Reason = "ranged_edge=" + conditions.RangedEdge.ToString("F3", CultureInfo.InvariantCulture) +
                         ">=threshold " + geometry.Describe(),
            };

            plan.RequiredFormations.Add(main.Key());
            plan.RequiredFormations.Add(guard.Key());
            plan.RequiredFormations.Add(support.Key());

            var mainTask = MakeCoordinationTask(plan, 1, main, TaskKind.Hold, FormationRole.Main,
                "coordination_main_hold_wait");
            var guardTask = MakeCoordinationTask(plan, 2, guard, TaskKind.Reposition, FormationRole.Guard,
                "coordination_guard_reposition");
            var supportTask = MakeCoordinationTask(plan, 3, support, TaskKind.Reposition, FormationRole.Support,
                "coordination_support_reposition");

            // 任务还没写令，RequestedX/Y 仍是空值；首次竞争的"距规范目标点误差"
            // 必须取计划几何点，否则两个候选误差同为 0（派单 §5 第 3 条）。
            mainTask.SetCanonicalTarget(geometry.MainX, geometry.MainY);
            guardTask.SetCanonicalTarget(geometry.GuardX, geometry.GuardY);
            supportTask.SetCanonicalTarget(geometry.SupportX, geometry.SupportY);

            guardTask.DependsOnTaskId = mainTask.TaskId;
            guardTask.GuardsTaskId = mainTask.TaskId;
            supportTask.DependsOnTaskId = mainTask.TaskId;
            supportTask.SupportsTaskId = mainTask.TaskId;
            mainTask.DependsOnTaskId = "";
            mainTask.ReplacesTaskId = "|" + guardTask.TaskId + "|" + supportTask.TaskId + "|";

            plan.Tasks.Add(mainTask);
            plan.Tasks.Add(guardTask);
            plan.Tasks.Add(supportTask);
            return plan;
        }

        // P3 正反馈继任：三职责几何按当前实际质心与敌目标方向重算，主力先回 Hold。
        // 推进仍由既有依赖放行路径产生，不在这里预放行。
        internal static BattlePlan BuildSuccessorCoordination(BattleState state, string planId, double now,
                                                              string parentPlanId, double feedbackAt,
                                                              string reason, double utility)
        {
            var plan = BuildCoordination(state, planId, now);
            if (plan == null) return null;
            plan.ParentPlanId = parentPlanId ?? "";
            plan.FeedbackAt = feedbackAt;
            plan.FeedbackReason = reason ?? "";
            plan.FeedbackUtility = utility;
            return plan;
        }

        // P3 负反馈有限撤回：每个原受控且仍合法的对象，以自己当前实际质心
        // 沿背离敌队有效质心方向移动固定距离。不建协同关系、不发 Charge。
        // 单个对象方向或质心不可用即跳过；一个都建不出来返回 null，
        // 调用方只能如实终止，不编造退路。
        internal static BattlePlan BuildWithdraw(BattleState state, string planId, double now,
                                                 List<FormationTask> sources, string reason)
        {
            if (state == null) return null;
            if (sources == null) return null;
            if (!BattleState.IsFinite(state.EnemyCenterX) || !BattleState.IsFinite(state.EnemyCenterY))
                return null;

            var plan = new BattlePlan
            {
                PlanId = planId,
                MissionKey = state.MissionKey,
                PlanInputId = state.PlanInputId,
                CreatedAt = now,
                InputSampledAt = state.SampledAt,
                Conditions = PlanConditions.From(state),
                Goal = ActionFeedback.GoalWithdraw,
                Revision = 1,
                Reason = reason ?? "withdraw",
            };

            int ordinal = 10;
            foreach (var src in sources)
            {
                if (src == null) continue;
                if (BattlePlan.IsExitTask(src)) continue;
                if (src.IsClosed) continue;
                if (string.IsNullOrEmpty(src.ObjectIdentity)) continue;

                FormationRecord rec = null;
                for (int i = 0; i < state.MyFormations.Count; i++)
                {
                    if (string.Equals(state.MyFormations[i].Key(), src.FormationKey, StringComparison.Ordinal))
                    {
                        rec = state.MyFormations[i];
                        break;
                    }
                }
                if (rec == null) continue;
                if (!EligibleAsTaskCandidate(rec)) continue;
                if (!rec.HasActiveCenter) continue;

                double tx, ty;
                if (!ActionFeedback.WithdrawTarget(rec.ActiveCenterX, rec.ActiveCenterY,
                                                   state.EnemyCenterX, state.EnemyCenterY,
                                                   out tx, out ty)) continue;

                var task = MakeTask(plan, ordinal, rec, TaskKind.Reposition, ActionFeedback.ReasonWithdraw);
                task.OriginX = rec.ActiveCenterX;
                task.OriginY = rec.ActiveCenterY;
                task.Role = FormationRole.None;
                task.RoleBaselineUnits = rec.ActiveMemberCount;
                task.RoleAssignedAt = now;
                task.Revision = 1;
                task.Source = ProposalSource.PhaseTransition;
                task.SetTarget(rec.ActiveCenterX, rec.ActiveCenterY, tx, ty);
                task.SetCanonicalTarget(tx, ty);
                plan.Tasks.Add(task);
                plan.RequiredFormations.Add(rec.Key());
                ordinal++;
            }

            if (plan.Tasks.Count == 0) return null;
            return plan;
        }

        // 保护职责：实际中心至有效 G* <=5 m，且相对有效 M* 朝敌投影在 [5,15] m、侧向绝对偏差 <=5 m。
        internal static bool GuardInPosition(CoordinationGeometry g, double x, double y)
        {
            if (g == null) return false;
            if (!CoordinationGeometry.IsFinite(x) || !CoordinationGeometry.IsFinite(y)) return false;
            if (CoordinationGeometry.Distance(x, y, g.GuardX, g.GuardY) >
                TestParameters.CoordinationArrivalRadiusMeters) return false;

            double forward = g.ForwardOffsetFrom(g.MainX, g.MainY, x, y);
            if (forward < TestParameters.GuardForwardMinMeters) return false;
            if (forward > TestParameters.GuardForwardMaxMeters) return false;

            double lateral = g.LateralOffsetFrom(g.MainX, g.MainY, x, y);
            return Math.Abs(lateral) <= TestParameters.GuardLateralMaxMeters;
        }

        // 辅助职责：实际中心至有效 S* <=5 m，且相对 M* 的侧向绝对投影在 [7,17] m。
        internal static bool SupportInPosition(CoordinationGeometry g, double x, double y)
        {
            if (g == null) return false;
            if (!CoordinationGeometry.IsFinite(x) || !CoordinationGeometry.IsFinite(y)) return false;
            if (CoordinationGeometry.Distance(x, y, g.SupportX, g.SupportY) >
                TestParameters.CoordinationArrivalRadiusMeters) return false;

            double lateral = Math.Abs(g.LateralOffsetFrom(g.MainX, g.MainY, x, y));
            return lateral >= TestParameters.SupportLateralMinMeters &&
                   lateral <= TestParameters.SupportLateralMaxMeters;
        }

        // 职责人数继续线：剩余有效人数 >=6 且 >= 该职责分配时基线的 50%。相等允许继续。
        internal static bool RoleStrengthHolds(int currentUnits, int baselineUnits)
        {
            if (baselineUnits <= 0) return false;
            if (currentUnits < TestParameters.RoleUnitFloor) return false;
            return (double)currentUnits >= baselineUnits * TestParameters.RoleUnitRatioFloor;
        }

        // 职责前提失效判定（§6 第 133 行）：按固定顺序逐条核对，
        // 位置未知单独成项，不与人数不足混为一谈。
        internal static RolePremiseFailure PremiseFailureOf(bool objectPresent, string formationClass,
                                                            int currentUnits, int baselineUnits,
                                                            bool eligible, bool positionKnown)
        {
            if (!objectPresent) return RolePremiseFailure.ObjectGone;
            if (!positionKnown) return RolePremiseFailure.PositionUnknown;
            if (!eligible) return RolePremiseFailure.EligibilityLost;
            if (!ClassAllows(formationClass, TaskKind.Reposition)) return RolePremiseFailure.ClassNotInCombination;
            if (!RoleStrengthHolds(currentUnits, baselineUnits)) return RolePremiseFailure.StrengthBelowLine;
            return RolePremiseFailure.None;
        }

        internal static RolePremiseFailure PremiseFailureOf(bool objectPresent, string formationClass,
                                                            int currentUnits, int baselineUnits, bool eligible)
        {
            return PremiseFailureOf(objectPresent, formationClass, currentUnits, baselineUnits, eligible, true);
        }

        // 实际活跃成员加权中心。任一记录位置非有限即返回失败，不用残缺值凑中心。
        // 位置优先取成员级实际质心；只有采样本就没读到质心时，才退回编队参考位置。
        // 两条口径分列，日志可区分，不把参考位置冒充成实际中心。
        private static bool TryActiveCenter(List<FormationRecord> records, out double cx, out double cy)
        {
            return TryActiveCenter(records, out cx, out cy, out _);
        }

        private static bool TryActiveCenter(List<FormationRecord> records, out double cx, out double cy,
                                            out int usedFallbackCount)
        {
            cx = 0.0;
            cy = 0.0;
            usedFallbackCount = 0;
            double weight = 0.0;
            double sx = 0.0;
            double sy = 0.0;
            foreach (var r in records)
            {
                if (r.UnitCount <= 0) continue;
                double px = r.PosX;
                double py = r.PosY;
                if (r.HasActiveCenter)
                {
                    px = r.ActiveCenterX;
                    py = r.ActiveCenterY;
                }
                else
                {
                    usedFallbackCount++;
                }
                if (!CoordinationGeometry.IsFinite(px) || !CoordinationGeometry.IsFinite(py)) return false;
                weight += r.UnitCount;
                sx += px * r.UnitCount;
                sy += py * r.UnitCount;
            }
            if (weight <= 0.0) return false;
            cx = sx / weight;
            cy = sy / weight;
            return CoordinationGeometry.IsFinite(cx) && CoordinationGeometry.IsFinite(cy);
        }

        // 单个编队的几何原点：优先成员级实际质心，缺少质心读数时才退回参考位置。
        private static bool MainOriginOf(FormationRecord r, out double x, out double y)
        {
            x = r.PosX;
            y = r.PosY;
            if (r.HasActiveCenter)
            {
                x = r.ActiveCenterX;
                y = r.ActiveCenterY;
            }
            return CoordinationGeometry.IsFinite(x) && CoordinationGeometry.IsFinite(y);
        }

        // 三职责的候选池：合格 + 类别可参与本单控制组合，按稳定序排列。
        private static List<FormationRecord> CoordinationCandidates(BattleState state)
        {
            var list = new List<FormationRecord>();
            foreach (var r in state.MyFormations)
            {
                if (!EligibleAsTaskCandidate(r)) continue;
                if (!ClassAllows(r.FormationClass, TaskKind.Reposition)) continue;
                list.Add(r);
            }
            list.Sort(CompareStable);
            return list;
        }

        // 按候选到两个规范点的实际距离分配保护与辅助职责。
        // 让更接近某规范点的编队去承担该职责，避免"按序指派"造成无谓横移。
        // 距离相同则按稳定序（rest 已排序），保证同一输入得到同一分配。
        private static bool AssignGuardAndSupport(CoordinationGeometry g, List<FormationRecord> rest,
                                                  out FormationRecord guard, out FormationRecord support)
        {
            guard = null;
            support = null;
            if (g == null || rest == null || rest.Count < 2) return false;

            double bestGuardError = double.MaxValue;
            double bestSupportError = double.MaxValue;
            int guardAt = -1;
            int supportAt = -1;

            for (int i = 0; i < rest.Count; i++)
            {
                var r = rest[i];
                double rx, ry;
                if (!MainOriginOf(r, out rx, out ry)) continue;

                double guardError = CoordinationGeometry.Distance(rx, ry, g.GuardX, g.GuardY);
                double supportError = CoordinationGeometry.Distance(rx, ry, g.SupportX, g.SupportY);

                if (guardError < bestGuardError) { bestGuardError = guardError; guardAt = i; }
                if (supportError < bestSupportError) { bestSupportError = supportError; supportAt = i; }
            }

            // 两个职责不能落在同一对象上；冲突时让保护先占位，辅助在其余候选里重挑。
            if (guardAt < 0 || supportAt < 0) return false;
            if (guardAt == supportAt)
            {
                supportAt = -1;
                bestSupportError = double.MaxValue;
                for (int i = 0; i < rest.Count; i++)
                {
                    if (i == guardAt) continue;
                    var r = rest[i];
                    if (!CoordinationGeometry.IsFinite(r.PosX) || !CoordinationGeometry.IsFinite(r.PosY)) continue;
                    double error = CoordinationGeometry.Distance(r.PosX, r.PosY, g.SupportX, g.SupportY);
                    if (error < bestSupportError)
                    {
                        bestSupportError = error;
                        supportAt = i;
                    }
                }
                if (supportAt < 0) return false;
            }

            guard = rest[guardAt];
            support = rest[supportAt];
            return guard != null && support != null && !ReferenceEquals(guard, support);
        }

        private static FormationTask MakeCoordinationTask(BattlePlan plan, int ordinal, FormationRecord record,
                                                         TaskKind kind, FormationRole role, string reason)
        {
            var t = MakeTask(plan, ordinal, record, kind, reason);
            t.Role = role;
            t.Revision = plan.Revision;
            // 基线必须是实际有效活跃人数。没有实际中心就无从判定人数，
            // 记 0 并让 PremiseFailure 报 PositionUnknown，不回退引擎人数——
            // 引擎人数含 detached/不可用成员，与质心不是同一批人。
            t.RoleBaselineUnits = record.HasActiveCenter ? record.ActiveMemberCount : 0;
            t.RoleAssignedAt = plan.CreatedAt;
            return t;
        }

        internal static BattlePlan Build(BattleState state, string planId, double now)
        {
            if (state == null) return null;

            var conditions = PlanConditions.From(state);
            var ranged = PickFirst(state.MyFormations, BattleAssessment.IsRanged);
            var infantry = PickFirst(state.MyFormations, BattleAssessment.IsInfantry);

            // 缺任一类别就不生成双编队计划，不做单编队降级冒充。
            if (ranged == null || infantry == null) return null;

            var plan = new BattlePlan
            {
                PlanId = planId,
                MissionKey = state.MissionKey,
                PlanInputId = state.PlanInputId,
                CreatedAt = now,
                InputSampledAt = state.SampledAt,
                Conditions = conditions,
            };

            plan.RequiredFormations.Add(ranged.Key());
            plan.RequiredFormations.Add(infantry.Key());

            if (conditions.StrengthCritical)
            {
                // 兵力已低于保存战力线：不按原目标进攻，两支编队转为保持（脚本 §4.5）。
                plan.Goal = "hold_observation";
                plan.Reason = "strength_critical ratio=" +
                              conditions.StrengthRatio.ToString("F3", CultureInfo.InvariantCulture) +
                              " line=" + TestParameters.StrengthDisadvantageLine.ToString("F2",
                                  CultureInfo.InvariantCulture);
                plan.Tasks.Add(MakeTask(plan, 1, ranged, TaskKind.Hold, "preserve_power_ranged_hold"));
                plan.Tasks.Add(MakeTask(plan, 2, infantry, TaskKind.Hold, "preserve_power_infantry_hold"));
            }
            else if (conditions.RangedAdvantageHolds)
            {
                // 己方远程优势：远程压上到有限接近点，步兵向前接敌。
                plan.Goal = "exploit_ranged_advantage";
                plan.Reason = "ranged_edge=" + conditions.RangedEdge.ToString("F3", CultureInfo.InvariantCulture) +
                              ">=threshold";
                plan.Tasks.Add(MakeTask(plan, 1, ranged, TaskKind.Reposition,
                    "ranged_advantage_advance"));
                plan.Tasks.Add(MakeTask(plan, 2, infantry, TaskKind.Engage,
                    "ranged_advantage_engage"));
            }
            else if (conditions.RangedDisadvantageHolds)
            {
                // 己方远程劣势：远程保持位置，步兵后撤到有限距离。
                plan.Goal = "mitigate_ranged_disadvantage";
                plan.Reason = "ranged_edge=" + conditions.RangedEdge.ToString("F3", CultureInfo.InvariantCulture) +
                              "<=-threshold";
                plan.Tasks.Add(MakeTask(plan, 1, ranged, TaskKind.Hold,
                    "ranged_disadvantage_hold"));
                plan.Tasks.Add(MakeTask(plan, 2, infantry, TaskKind.Reposition,
                    "ranged_disadvantage_fallback"));
            }
            else
            {
                // 条件不成立：不给编队派普通任务，只保持观察。
                plan.Goal = "hold_observation";
                plan.Reason = "no_condition_met ranged_edge=" +
                              conditions.RangedEdge.ToString("F3", CultureInfo.InvariantCulture);
            }

            return plan;
        }

        // 任务按稳定顺序建立：两配置一致，Ordinal 1 = 远程类，2 = 步兵类。
        private static FormationTask MakeTask(BattlePlan plan, int ordinal, FormationRecord record,
                                              TaskKind kind, string reason)
        {
            return new FormationTask
            {
                TaskId = plan.PlanId + "-task-" + ordinal.ToString(CultureInfo.InvariantCulture),
                PlanId = plan.PlanId,
                Ordinal = ordinal,
                FormationKey = record.Key(),
                ObjectIdentity = record.ObjectIdentity,
                FormationClass = record.FormationClass,
                TeamIndex = record.TeamIndex,
                FormationIndex = record.FormationIndex,
                Kind = kind,
                Reason = reason,
                CreatedAt = plan.CreatedAt,
                OrderBefore = record.MovementOrder,
                OriginX = record.PosX,
                OriginY = record.PosY,
            };
        }

        // 任务候选必须在完整资格门之后才能入选：仅人数和兵种类别不构成资格。
        private static bool EligibleAsTaskCandidate(FormationRecord r)
        {
            if (r.UnitCount <= 0) return false;
            if (r.IsPlayerTeam) return false;
            if (r.IsPlayerControlled) return false;
            if (!r.IsAiControlled) return false;
            if (string.IsNullOrEmpty(r.ObjectIdentity)) return false;
            return true;
        }

        private static FormationRecord PickFirst(List<FormationRecord> records, Func<string, bool> predicate)
        {
            FormationRecord best = null;
            foreach (var r in records)
            {
                if (!EligibleAsTaskCandidate(r)) continue;
                if (!predicate(r.FormationClass)) continue;
                if (best == null || CompareStable(r, best) < 0) best = r;
            }
            return best;
        }

        private static int CompareStable(FormationRecord a, FormationRecord b)
        {
            if (a.TeamIndex != b.TeamIndex) return a.TeamIndex.CompareTo(b.TeamIndex);
            return a.FormationIndex.CompareTo(b.FormationIndex);
        }
    }
}
