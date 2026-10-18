using System;
using System.Globalization;

namespace CombatBrain2.Core
{
    // 命令生效判据。对每条已写命令，在同一份后续快照上复算，
    // 不把"写令调用返回了"当成"命令生效了"。
    internal static class TaskFeedback
    {
        internal sealed class Result
        {
            internal TaskFeedbackState State = TaskFeedbackState.Pending;
            internal PlanInvalidReason Reason = PlanInvalidReason.None;
            internal string Detail = "";
        }

        // 在反馈窗口内判定；窗口未到时返回 Pending，不提前下结论。
        // enemyX/enemyY 为敌方中心参考点（无有效敌方时为 NaN），用于 Engage 的朝敌位移判定。
        internal static Result Evaluate(FormationTask task, FormationRecord now, double missionTime,
                                        double enemyX, double enemyY)
        {
            var result = new Result();
            if (task == null)
            {
                result.State = TaskFeedbackState.Failed;
                result.Reason = PlanInvalidReason.NotEffective;
                result.Detail = "no_task";
                return result;
            }
            if (task.WrittenAt < 0.0)
            {
                result.State = TaskFeedbackState.Pending;
                result.Detail = "not_written_yet";
                return result;
            }

            if (now == null)
            {
                result.State = TaskFeedbackState.Failed;
                result.Reason = PlanInvalidReason.CommandOverridden;
                result.Detail = "object_gone";
                return result;
            }

            if (!string.Equals(now.ObjectIdentity, task.ObjectIdentity, StringComparison.Ordinal))
            {
                result.State = TaskFeedbackState.Failed;
                result.Reason = PlanInvalidReason.CommandOverridden;
                result.Detail = "object_identity_changed obj_now=" + now.ObjectIdentity;
                return result;
            }

            // 已被原生改写成别的命令：本任务的命令不再生效。
            if (!SameOrderFamily(now.MovementOrder, task.OrderAfter))
            {
                result.State = TaskFeedbackState.Failed;
                result.Reason = PlanInvalidReason.CommandOverridden;
                result.Detail = "order_now=" + now.MovementOrder + " order_after=" + task.OrderAfter;
                return result;
            }

            bool effective = false;
            string detail = "";

            if (task.Kind == TaskKind.Engage)
            {
                // Engage：必须朝敌方向产生位移；原地不动或后退不算生效。
                // 位移基线取实际写令时的位置，不含发令前的原生移动。
                bool haveEnemy = !double.IsNaN(enemyX) && !double.IsNaN(enemyY);
                if (!haveEnemy)
                {
                    detail = "engage_no_enemy_anchor";
                }
                else
                {
                    double ex = enemyX - task.BaselineX;
                    double ey = enemyY - task.BaselineY;
                    double span = Math.Sqrt(ex * ex + ey * ey);
                    if (span <= 0.0001)
                    {
                        detail = "engage_zero_span_no_direction";
                    }
                    else
                    {
                        double moveX = now.PosX - task.BaselineX;
                        double moveY = now.PosY - task.BaselineY;
                        double toward = (moveX * ex + moveY * ey) / span;
                        if (toward >= TestParameters.EffectiveDisplacementMeters)
                        {
                            effective = true;
                            detail = "engage_toward_enemy=" +
                                     toward.ToString("F2", CultureInfo.InvariantCulture) +
                                     " since_write=" +
                                     Fmt(missionTime - task.WrittenAt);
                        }
                        else
                        {
                            detail = "engage_toward_enemy=" +
                                     toward.ToString("F2", CultureInfo.InvariantCulture) +
                                     " (<" + TestParameters.EffectiveDisplacementMeters.ToString("F1",
                                         CultureInfo.InvariantCulture) + ")" +
                                     " since_write=" +
                                     Fmt(missionTime - task.WrittenAt);
                        }
                    }
                }
            }
            else if (task.Kind == TaskKind.Reposition && task.HasPosition)
            {
                // Reposition：先核对实际命令目标点的读回误差，
                // 再按到目标距离减少 >=3 m 或已到达判定进度。
                double readback = task.OrderTargetReadbackError();
                if (readback < 0.0)
                {
                    // 没有读到实际命令目标点：这不是"目标已正确落地"的事实。
                    // 保持 Pending，到既定反馈截止再报失败，不借缺失值继续距离判定。
                    detail = "order_target_unreadable";
                }
                else if (readback > TestParameters.TargetPointToleranceMeters)
                {
                    // 命令目标点与有效请求点不符：本命令没有落在请求的位置上。
                    detail = "order_target_readback_error=" +
                             readback.ToString("F2", CultureInfo.InvariantCulture) +
                             " (>" + TestParameters.TargetPointToleranceMeters.ToString("F1",
                                 CultureInfo.InvariantCulture) + ")" +
                             " order_target=(" +
                             Fmt(task.OrderTargetX) + "," +
                             Fmt(task.OrderTargetY) + ")" +
                             " requested=(" +
                             Fmt(task.RequestedX) + "," +
                             Fmt(task.RequestedY) + ")";
                }
                else if (task.HasCanonicalTarget && !now.HasActiveCenter)
                {
                    // 质心语义任务的判据依赖有效成员质心：读不到就如实记为不可读，
                    // 不用编队锚点冒充质心继续判定（派单 §2.2 第 2 条）。
                    detail = "center_unreadable";
                }
                else
                {
                    // 质心语义任务：进度与到点都以有效成员质心对战术目标 T 计算，
                    // 命令锚点只是写给引擎的请求，不参与距离判定（派单 §2.2 第 3/5 条）。
                    // 锚点语义任务（P1 普通位置任务）保持原有口径。
                    bool byCentroid = task.HasCanonicalTarget;
                    double refX = byCentroid ? now.ActiveCenterX : now.PosX;
                    double refY = byCentroid ? now.ActiveCenterY : now.PosY;
                    double goalX = byCentroid ? task.CanonicalX : task.RequestedX;
                    double goalY = byCentroid ? task.CanonicalY : task.RequestedY;
                    double dx = refX - goalX;
                    double dy = refY - goalY;
                    double distance = Math.Sqrt(dx * dx + dy * dy);
                    double from = task.BaselineDistanceTo(goalX, goalY);
                    if (distance <= TestParameters.TargetPointToleranceMeters)
                    {
                        effective = true;
                        task.ReachedTargetPoint = true;
                        detail = "reached_target_point distance=" +
                                 distance.ToString("F2", CultureInfo.InvariantCulture) +
                                 " from=" + from.ToString("F2", CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        // 进度：到目标距离减少至少 3 m 才算本命令产生了位移效果。
                        double closed = from - distance;
                        if (closed >= TestParameters.EffectiveDisplacementMeters)
                        {
                            effective = true;
                            detail = "closed_distance=" +
                                     closed.ToString("F2", CultureInfo.InvariantCulture) +
                                     " distance=" + distance.ToString("F2", CultureInfo.InvariantCulture) +
                                     " from=" + from.ToString("F2", CultureInfo.InvariantCulture) +
                                     " since_write=" +
                                     Fmt(missionTime - task.WrittenAt);
                        }
                        else
                        {
                            detail = "closed_distance=" +
                                     closed.ToString("F2", CultureInfo.InvariantCulture) +
                                     " (<" + TestParameters.EffectiveDisplacementMeters.ToString("F1",
                                         CultureInfo.InvariantCulture) + ")" +
                                     " distance=" + distance.ToString("F2", CultureInfo.InvariantCulture) +
                                     " from=" + from.ToString("F2", CultureInfo.InvariantCulture) +
                                     " since_write=" +
                                     Fmt(missionTime - task.WrittenAt);
                        }
                    }
                }
            }
            else if (task.Kind == TaskKind.Hold)
            {
                // Hold：位置保持即可，不对位移作要求。
                detail = "hold_no_displacement_required";
                effective = true;
            }
            else if (task.HasPosition)
            {
                // 有位置但类型不适用位移判据：不冒充生效，也不编造方向。
                detail = "no_displacement_criteria_for_kind=" + task.Kind;
            }
            else
            {
                // 其余类型没有可判定的几何依据：不冒充生效，也不编造方向。
                detail = "no_criteria_for_kind=" + task.Kind;
            }

            if (effective)
            {
                result.State = TaskFeedbackState.Effective;
                result.Detail = detail;
                return result;
            }

            if (missionTime >= task.FeedbackDeadline())
            {
                result.State = TaskFeedbackState.Failed;
                result.Reason = PlanInvalidReason.NotEffective;
                result.Detail = detail;
                return result;
            }

            result.State = TaskFeedbackState.Pending;
            result.Detail = detail;
            return result;
        }

        // 已批准裁定①：职责关系已成立时，把「要求已满足」并入反馈结论。
        // 只改结论与口径记录，不改位移判据本身；调用方必须先用
        // CoordinationDriver.RoleRequirementSatisfied 证明关系成立。
        internal static void PromoteToSatisfiedRoleRequirement(Result result)
        {
            if (result == null || result.State == TaskFeedbackState.Effective) return;
            result.State = TaskFeedbackState.Effective;
            result.Reason = PlanInvalidReason.None;
            result.Detail = "role_requirement_already_satisfied was=" + (result.Detail ?? "");
        }

        // 命令族比较：同名或同族视为仍是本任务写入的那条命令。
        private static bool SameOrderFamily(string a, string b)
        {
            string fa = Family(a);
            string fb = Family(b);
            if (fa.Length == 0 || fb.Length == 0) return false;
            return string.Equals(fa, fb, StringComparison.Ordinal);
        }

        private static string Fmt(double v)
        {
            return v.ToString("F3", CultureInfo.InvariantCulture);
        }

        private static string Family(string order)
        {
            if (string.IsNullOrEmpty(order)) return "";
            if (order.IndexOf("Charge", StringComparison.OrdinalIgnoreCase) >= 0) return "Charge";
            if (order.IndexOf("Stop", StringComparison.OrdinalIgnoreCase) >= 0) return "Stop";
            if (order.IndexOf("StandYourGround", StringComparison.OrdinalIgnoreCase) >= 0) return "Stop";
            if (order.IndexOf("Move", StringComparison.OrdinalIgnoreCase) >= 0) return "Move";
            if (order.IndexOf("Advance", StringComparison.OrdinalIgnoreCase) >= 0) return "Advance";
            if (order.IndexOf("FallBack", StringComparison.OrdinalIgnoreCase) >= 0) return "FallBack";
            return "";
        }
    }
}
