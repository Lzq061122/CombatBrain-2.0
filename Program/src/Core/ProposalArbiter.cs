using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CombatBrain2.Core
{
    // 一个提交给裁决入口的命令建议。提案不直接写引擎，也不能自行声明高优先级。
    internal sealed class Proposal
    {
        internal string MissionKey = "";
        internal string PlanId = "";
        internal int Revision;
        internal string TaskId = "";
        internal string FormationKey = "";
        internal string ObjectIdentity = "";
        internal string ProposalId = "";
        internal ProposalSource Source = ProposalSource.None;
        internal TaskKind Kind = TaskKind.None;
        internal FormationRole RequestedRole = FormationRole.None;
        internal double TargetX;
        internal double TargetY;
        internal bool HasTarget;
        internal string Reason = "";
        internal double SubmittedAt = -1.0;

        // 身份由来源与建议内容派生，可稳定比较；不按接收先后编号。
        internal static string MakeId(string missionKey, string planId, int revision, string taskId,
                                      string formationKey, string objectIdentity, TaskKind kind,
                                      ProposalSource source, double targetX, double targetY)
        {
            var sb = new StringBuilder();
            sb.Append(missionKey ?? "").Append('|')
              .Append(planId ?? "").Append('|')
              .Append(revision.ToString(CultureInfo.InvariantCulture)).Append('|')
              .Append(taskId ?? "").Append('|')
              .Append(formationKey ?? "").Append('|')
              .Append(objectIdentity ?? "").Append('|')
              .Append(kind.ToString()).Append('|')
              .Append(source.ToString()).Append('|')
              .Append(targetX.ToString("F3", CultureInfo.InvariantCulture)).Append(',')
              .Append(targetY.ToString("F3", CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        // 生产与测试注入共用的构造成员：身份、版本与来源由内容一次定齐，
        // 调用方不得事后自报更高优先级。
        // hasTarget=false 表示目标尚未生成：此时不冒充原点，改用 TargetKey 的空值记号，
        // 让「没有目标」与「目标恰好在 (0,0)」在身份上可区分。
        internal static Proposal Create(string missionKey, BattlePlan plan, FormationTask task,
                                        TaskKind kind, ProposalSource source,
                                        double targetX, double targetY, string reason, double submittedAt)
        {
            return Create(missionKey, plan, task, kind, source, targetX, targetY, true, reason, submittedAt);
        }

        internal static Proposal Create(string missionKey, BattlePlan plan, FormationTask task,
                                        TaskKind kind, ProposalSource source,
                                        double targetX, double targetY, bool hasTarget,
                                        string reason, double submittedAt)
        {
            var p = new Proposal();
            p.MissionKey = missionKey ?? "";
            p.PlanId = plan == null ? "" : plan.PlanId;
            p.Revision = plan == null ? 0 : plan.Revision;
            p.TaskId = task == null ? "" : task.TaskId;
            p.FormationKey = task == null ? "" : task.FormationKey;
            p.ObjectIdentity = task == null ? "" : task.ObjectIdentity;
            p.Kind = kind;
            p.Source = source;
            p.HasTarget = hasTarget;
            p.TargetX = hasTarget ? targetX : 0.0;
            p.TargetY = hasTarget ? targetY : 0.0;
            p.Reason = reason ?? "";
            p.SubmittedAt = submittedAt;
            p.ProposalId = hasTarget
                ? MakeId(p.MissionKey, p.PlanId, p.Revision, p.TaskId, p.FormationKey,
                         p.ObjectIdentity, p.Kind, p.Source, p.TargetX, p.TargetY)
                : MakeTargetlessId(p.MissionKey, p.PlanId, p.Revision, p.TaskId, p.FormationKey,
                                   p.ObjectIdentity, p.Kind, p.Source);
            return p;
        }

        // 无目标提案的身份：不含坐标，避免与「目标为原点(0,0)」的合法提案撞同一 ID。
        internal static string MakeTargetlessId(string missionKey, string planId, int revision, string taskId,
                                                string formationKey, string objectIdentity,
                                                TaskKind kind, ProposalSource source)
        {
            var sb = new StringBuilder();
            sb.Append(missionKey).Append('|')
              .Append(planId).Append('|')
              .Append(revision).Append('|')
              .Append(taskId).Append('|')
              .Append(formationKey).Append('|')
              .Append(objectIdentity).Append('|')
              .Append(kind.ToString()).Append('|')
              .Append(source.ToString()).Append('|')
              .Append("<no-target>");
            return sb.ToString();
        }

        internal static string TargetKey(double x, double y)
        {
            return x.ToString("F3", CultureInfo.InvariantCulture) + "," +
                   y.ToString("F3", CultureInfo.InvariantCulture);
        }
    }

    internal sealed class ArbitrationDecision
    {
        internal bool Accepted;
        internal bool Duplicate;
        internal bool Published;
        internal string WinnerProposalId = "";
        internal ProposalRejectReason RejectReason = ProposalRejectReason.None;
        internal ProposalRejectReason LoserRejectReason = ProposalRejectReason.None;
        internal string Detail = "";

        internal static ArbitrationDecision Reject(ProposalRejectReason reason, string detail)
        {
            return new ArbitrationDecision
            {
                Accepted = false,
                RejectReason = reason,
                Detail = detail ?? "",
            };
        }
    }

    // 当前已公布的有效裁决：每个真实编队的 Movement 域最多一条。
    internal sealed class CurrentDecision
    {
        internal string FormationKey = "";
        internal string ProposalId = "";
        internal string TaskId = "";
        internal int Revision;
        internal string ObjectIdentity = "";
        internal TaskKind Kind = TaskKind.None;
        internal double TargetX;
        internal double TargetY;
        internal bool HasTarget;
        internal string TargetKey = "";
    }

    // 一批待公布的同级合法提案。裁决器持有批次状态，提交方不能自行决定何时公布。
    internal sealed class ArbitrationBatch
    {
        internal string FormationKey = "";
        internal int Revision;
        internal double OpenedAt = -1.0;
        internal readonly List<Proposal> Candidates = new List<Proposal>();
        internal readonly List<FormationTask> Tasks = new List<FormationTask>();

        internal bool Contains(string proposalId)
        {
            foreach (var p in Candidates)
            {
                if (string.Equals(p.ProposalId, proposalId, StringComparison.Ordinal)) return true;
            }
            return false;
        }
    }

    // P2 唯一裁决（派单 §5）。只覆盖 CB2 内部提交的提案；原生其他写令者仍可覆盖。
    // 批次边界由裁决器自己管理：没有已公布结果时收提案，一旦公布即成当前结果；
    // 只有显式关闭当前结果后才重新开始收题。替换权限不由提案自报的来源授予。
    internal sealed class ProposalArbiter
    {
        private readonly Dictionary<string, CurrentDecision> _byFormation =
            new Dictionary<string, CurrentDecision>(StringComparer.Ordinal);
        private readonly Dictionary<string, ArbitrationBatch> _openBatch =
            new Dictionary<string, ArbitrationBatch>(StringComparer.Ordinal);
        private readonly Dictionary<string, double> _firstSubmittedAt =
            new Dictionary<string, double>(StringComparer.Ordinal);

        internal CurrentDecision CurrentFor(string formationKey)
        {
            CurrentDecision c;
            return _byFormation.TryGetValue(formationKey ?? "", out c) ? c : null;
        }

        internal ArbitrationBatch OpenBatchFor(string formationKey)
        {
            ArbitrationBatch b;
            return _openBatch.TryGetValue(formationKey ?? "", out b) ? b : null;
        }

        internal double FirstSubmissionAt(string taskId)
        {
            double t;
            return _firstSubmittedAt.TryGetValue(taskId ?? "", out t) ? t : -1.0;
        }

        // 提交一条提案。返回值只说明这条提案在当前批次里的处境；
        // 是否已经产生对外有效结果由 Published 标志区分。
        internal ArbitrationDecision Submit(Proposal p, BattlePlan plan, FormationTask task)
        {
            if (p == null) return ArbitrationDecision.Reject(ProposalRejectReason.TaskUnknown, "no_proposal");

            // 1) 先验合法性。旧 Plan / 旧 revision / 已关闭任务 / 不同对象 /
            //    未授权来源 / 类别不允许的命令，全部先拒绝。
            if (plan == null) return ArbitrationDecision.Reject(ProposalRejectReason.PlanMismatch, "no_plan");
            if (plan.IsClosed) return ArbitrationDecision.Reject(ProposalRejectReason.PlanClosed, plan.PlanId);
            if (!string.Equals(p.PlanId, plan.PlanId, StringComparison.Ordinal))
            {
                return ArbitrationDecision.Reject(ProposalRejectReason.PlanMismatch,
                    "proposal_plan=" + p.PlanId + " plan=" + plan.PlanId);
            }
            if (p.Revision != plan.Revision)
            {
                return ArbitrationDecision.Reject(ProposalRejectReason.StaleRevision,
                    "proposal_revision=" + p.Revision.ToString(CultureInfo.InvariantCulture) +
                    " plan_revision=" + plan.Revision.ToString(CultureInfo.InvariantCulture));
            }
            if (task == null)
            {
                return ArbitrationDecision.Reject(ProposalRejectReason.TaskUnknown, p.TaskId);
            }
            if (task.IsClosed || task.WrittenAt >= 0.0)
            {
                return ArbitrationDecision.Reject(ProposalRejectReason.TaskClosed,
                    "written_at=" + task.WrittenAt.ToString("F3", CultureInfo.InvariantCulture));
            }
            if (task.Revision != plan.Revision)
            {
                return ArbitrationDecision.Reject(ProposalRejectReason.StaleRevision,
                    "task_revision=" + task.Revision.ToString(CultureInfo.InvariantCulture));
            }
            if (!string.Equals(task.PlanId, plan.PlanId, StringComparison.Ordinal) ||
                !PlanContains(plan, task.TaskId))
            {
                return ArbitrationDecision.Reject(ProposalRejectReason.TaskNotInPlan, task.TaskId);
            }
            if (!string.Equals(p.TaskId, task.TaskId, StringComparison.Ordinal))
            {
                return ArbitrationDecision.Reject(ProposalRejectReason.TaskNotInPlan,
                    "proposal_task=" + p.TaskId + " task=" + task.TaskId);
            }
            if (!string.Equals(p.FormationKey, task.FormationKey, StringComparison.Ordinal))
            {
                return ArbitrationDecision.Reject(ProposalRejectReason.FormationMismatch,
                    "proposal_key=" + p.FormationKey + " task_key=" + task.FormationKey);
            }
            if (!string.Equals(p.ObjectIdentity, task.ObjectIdentity, StringComparison.Ordinal))
            {
                return ArbitrationDecision.Reject(ProposalRejectReason.ObjectMismatch,
                    "proposal_obj=" + p.ObjectIdentity + " task_obj=" + task.ObjectIdentity);
            }
            if (p.Source == ProposalSource.None)
            {
                return ArbitrationDecision.Reject(ProposalRejectReason.UnauthorizedSource, "source_none");
            }
            if (!PlanBuilder.ClassAllows(task.FormationClass, p.Kind))
            {
                return ArbitrationDecision.Reject(ProposalRejectReason.KindNotAllowedForClass,
                    "class=" + task.FormationClass + " kind=" + p.Kind);
            }
            if (p.RequestedRole != FormationRole.None && p.RequestedRole != task.Role)
            {
                return ArbitrationDecision.Reject(ProposalRejectReason.KindNotAllowedForRole,
                    "requested_role=" + p.RequestedRole + " task_role=" + task.Role);
            }
            if (!PlanBuilder.ClassAllows(task.FormationClass, task.Kind))
            {
                return ArbitrationDecision.Reject(ProposalRejectReason.KindNotAllowedForClass,
                    "task_kind_not_allowed class=" + task.FormationClass + " kind=" + task.Kind);
            }
            // 需要位置目标的命令必须在合法性阶段就拒绝无目标提案。
            // 不能只靠竞争排序：唯一候选若是无目标 Reposition，它照样会被公布，
            // 然后写令阶段才发现没有目标点，等于让无效提案占用槽位。
            if (!p.HasTarget && NeedsTarget(p.Kind))
            {
                return ArbitrationDecision.Reject(ProposalRejectReason.InvalidGeometry,
                    "target_missing_for_kind kind=" + p.Kind);
            }

            // 首次提交锚点只记一次，重投不刷新。
            if (!_firstSubmittedAt.ContainsKey(task.TaskId))
            {
                _firstSubmittedAt[task.TaskId] = p.SubmittedAt;
            }

            CurrentDecision existing;
            _byFormation.TryGetValue(p.FormationKey, out existing);

            // 3) 同一任务的完全相同提案去重，不重置首次提交锚点。
            if (existing != null &&
                string.Equals(existing.ProposalId, p.ProposalId, StringComparison.Ordinal))
            {
                return new ArbitrationDecision
                {
                    Accepted = true,
                    Duplicate = true,
                    Published = true,
                    WinnerProposalId = existing.ProposalId,
                    Detail = "duplicate_held",
                };
            }

            if (existing != null)
            {
                return new ArbitrationDecision
                {
                    Accepted = true,
                    Published = true,
                    WinnerProposalId = existing.ProposalId,
                    LoserRejectReason = ProposalRejectReason.Superseded,
                    Detail = "current_decision_held challenger=" + p.ProposalId,
                };
            }

            // 2) 该编队尚未公布结果：进入本批收题。
            ArbitrationBatch batch = GetOrOpenBatch(p.FormationKey, p.Revision, p.SubmittedAt);
            if (batch.Contains(p.ProposalId))
            {
                return new ArbitrationDecision
                {
                    Accepted = true,
                    Duplicate = true,
                    Detail = "batch_duplicate",
                };
            }
            batch.Candidates.Add(p);
            batch.Tasks.Add(task);

            return new ArbitrationDecision
            {
                Accepted = true,
                Detail = "batched pending_publish count=" +
                         batch.Candidates.Count.ToString(CultureInfo.InvariantCulture),
            };
        }

        // 公布本批：在全部候选里唯一选取并成为该编队的当前有效结果。
        internal ArbitrationDecision PublishBatch(string formationKey)
        {
            ArbitrationBatch batch;
            if (!_openBatch.TryGetValue(formationKey ?? "", out batch) || batch.Candidates.Count == 0)
            {
                return ArbitrationDecision.Reject(ProposalRejectReason.None, "no_open_batch");
            }

            // 本批里必须存在一条可执行候选。Reposition 无目标已在合法性阶段被拒，
            // 所以走到这里仍无目标的只可能是 Hold/Engage —— 它们合法且可执行。
            // 这道守卫只兜底「批里全是无目标 Reposition」这种本不该出现的状态。
            bool anyViable = false;
            for (int i = 0; i < batch.Candidates.Count; i++)
            {
                if (batch.Candidates[i].HasTarget || !NeedsTarget(batch.Candidates[i].Kind))
                {
                    anyViable = true;
                    break;
                }
            }
            if (!anyViable)
            {
                _openBatch.Remove(batch.FormationKey);
                return ArbitrationDecision.Reject(ProposalRejectReason.InvalidGeometry,
                    "no_viable_candidate count=" +
                    batch.Candidates.Count.ToString(CultureInfo.InvariantCulture));
            }

            int best = 0;
            for (int i = 1; i < batch.Candidates.Count; i++)
            {
                if (CompareFirstCompetition(batch.Candidates[i], batch.Tasks[i],
                                            batch.Candidates[best], batch.Tasks[best]) < 0)
                {
                    best = i;
                }
            }

            var winner = batch.Candidates[best];
            _byFormation[batch.FormationKey] = MakeCurrent(winner);
            _openBatch.Remove(batch.FormationKey);

            return new ArbitrationDecision
            {
                Accepted = true,
                Published = true,
                WinnerProposalId = winner.ProposalId,
                Detail = "published count=" +
                         batch.Candidates.Count.ToString(CultureInfo.InvariantCulture),
            };
        }

        // 首次竞争排序：Kind 匹配优先，再目标误差升序，最后 proposal_id 序。
        private static int CompareFirstCompetition(Proposal candidate, FormationTask candidateTask,
                                                   Proposal incumbent, FormationTask incumbentTask)
        {
            bool candKindOk = candidate.Kind == candidateTask.Kind;
            bool incKindOk = incumbent.Kind == incumbentTask.Kind;
            if (candKindOk != incKindOk) return candKindOk ? -1 : 1;

            double candErr = TargetError(candidate, candidateTask);
            double incErr = TargetError(incumbent, incumbentTask);
            int byError = candErr.CompareTo(incErr);
            if (byError != 0) return byError;

            return string.CompareOrdinal(candidate.ProposalId, incumbent.ProposalId);
        }

        // 命令是否必须有位置目标。只有 Reposition 需要：它写 Move 到具体点。
        // Hold 执行 Stop，Engage 执行普通 Charge（不是指定目标的 ChargeToTarget），
        // 两者都不带位置目标。
        internal static bool NeedsTarget(TaskKind kind)
        {
            return kind == TaskKind.Reposition;
        }

        // 目标误差：无目标的提案没有「距规范目标点的误差」可言，
        // 不能用 0 当最优值参与竞争，否则它会排在真正给出目标的提案之前。
        // 返回 +∞ 让它在误差比较里始终最后；只有全部候选都无目标时才落到 proposal_id 序。
        // 基准点取任务实际请求位置；任务还没写令（首次竞争）时回退到计划几何的规范目标点，
        // 否则两个候选误差同为 0，规则②失效（派单 §5 第 3 条）。
        internal static double TargetError(Proposal p, FormationTask task)
        {
            if (task == null) return 0.0;
            double baseX, baseY;
            if (task.HasPosition)
            {
                baseX = task.RequestedX;
                baseY = task.RequestedY;
            }
            else if (task.HasCanonicalTarget)
            {
                baseX = task.CanonicalX;
                baseY = task.CanonicalY;
            }
            else
            {
                return 0.0;
            }
            if (!p.HasTarget) return double.PositiveInfinity;
            double dx = p.TargetX - baseX;
            double dy = p.TargetY - baseY;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static CurrentDecision MakeCurrent(Proposal p)
        {
            return new CurrentDecision
            {
                FormationKey = p.FormationKey,
                ProposalId = p.ProposalId,
                TaskId = p.TaskId,
                Revision = p.Revision,
                ObjectIdentity = p.ObjectIdentity,
                Kind = p.Kind,
                TargetX = p.TargetX,
                TargetY = p.TargetY,
                HasTarget = p.HasTarget,
                TargetKey = Proposal.TargetKey(p.TargetX, p.TargetY),
            };
        }

        private ArbitrationBatch GetOrOpenBatch(string formationKey, int revision, double openedAt)
        {
            ArbitrationBatch b;
            if (_openBatch.TryGetValue(formationKey ?? "", out b) && b.Revision == revision) return b;
            b = new ArbitrationBatch
            {
                FormationKey = formationKey ?? "",
                Revision = revision,
                OpenedAt = openedAt,
            };
            _openBatch[b.FormationKey] = b;
            return b;
        }

        // 关闭当前结果：先撤下有效结果，再重新开始收题（派单 §5 第 4 步）。
        internal void CloseCurrent(string formationKey)
        {
            _byFormation.Remove(formationKey ?? "");
            _openBatch.Remove(formationKey ?? "");
        }

        // 关闭时仅清自己的槽位：旧任务晚到不得清新任务的槽。
        internal void CloseCurrentForTask(string formationKey, string taskId)
        {
            CurrentDecision c;
            if (!_byFormation.TryGetValue(formationKey ?? "", out c)) return;
            if (!string.Equals(c.TaskId, taskId, StringComparison.Ordinal)) return;
            CloseCurrent(formationKey);
        }

        internal void ClearMission()
        {
            _byFormation.Clear();
            _openBatch.Clear();
            _firstSubmittedAt.Clear();
        }

        private static bool PlanContains(BattlePlan plan, string taskId)
        {
            foreach (var t in plan.Tasks)
            {
                if (t != null && string.Equals(t.TaskId, taskId, StringComparison.Ordinal)) return true;
            }
            return false;
        }
    }
}
