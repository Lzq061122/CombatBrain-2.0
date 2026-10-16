using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CombatBrain2.Core
{
    // 一名成员在本行动中的终态。Routed 是战力脱离，单独统计；Deleted/None/读取异常
    // 属于未知，不能冒充伤亡。
    internal enum MemberEndState
    {
        None = 0,
        Killed,
        Unconscious,
        Routed,
        Deleted,
        Unknown,
    }

    // 行动结果评价的结论。Continue 表示沿用当前计划，不产生新决定。
    internal enum ActionFeedbackDecision
    {
        Continue = 0,
        Reinforce,
        StopLoss,
        Disengage,
    }

    // 一次评价的输入读数。风险读数由调用方用 RiskGate 从同一次采样算出后填入，
    // 评价本身不再另取采样。
    internal sealed class ActionFeedbackInput
    {
        internal double Now;
        internal bool IntegrityOk = true;
        internal string IntegrityNote = "";
        internal bool TargetObjectPresent = true;
        internal int TargetActiveE0Members;
        internal bool RegroupedAway;
        internal bool MyForceContinueLineHolds = true;
        internal bool RangedAdvantageStillHolds = true;
        internal bool PlanStillValid = true;
        internal bool RiskCenterKnown = true;
        internal int EnemyWithinRiskRadius;
        internal int MyRoleUnits;
        internal bool RolesAboveContinueLine = true;
    }

    // 一次评价的全部量化读数与结论。字段与派单 §4 生产日志最低字段一一对应。
    internal sealed class ActionFeedbackEvaluation
    {
        internal bool Complete;
        internal string IncompleteReason = "";

        internal double Now;
        internal double T0;
        internal double StableSeconds;

        internal int NE;
        internal int NF;
        internal int DE;
        internal int DF;
        internal int RoutedCount;
        internal int UnknownCount;

        internal double LE;
        internal double LF;
        internal double B;
        internal double C;
        internal double U;

        internal bool RateKnown;
        internal double Rf = double.NaN;
        internal double RateWindow = double.NaN;
        internal bool CostKnown;
        internal double CostTerm;
        internal double HRf = double.NaN;

        internal ActionFeedbackDecision Decision = ActionFeedbackDecision.Continue;
        internal string Reason = "";
        internal int ConsecutiveCount;
        internal bool ExceptionUsed;
        internal string ExceptionReason = "";
    }

    // 一轮远程优势行动的结果账本。t0、E0/F0 与分母一经冻结不再重置；
    // 继任计划通过 CloneForSuccessor 继承同一份累计损失。
    internal sealed class ActionFeedbackTracker
    {
        private const int SideEnemy = 1;
        private const int SideFriendly = 2;

        private sealed class Sample
        {
            internal double SampledAt;
            internal int DF;
        }

        private readonly Dictionary<string, int> _side = new Dictionary<string, int>();
        private readonly Dictionary<string, MemberEndState> _terminal =
            new Dictionary<string, MemberEndState>();
        private readonly List<Sample> _samples = new List<Sample>();

        internal string MissionKey = "";
        internal string PlanId = "";
        internal string PlanInputId = "";
        internal string ObserveObjectIdentity = "";

        // t0：该行动首次主力推进普通命令真正写入的 Mission 时刻。
        internal double T0;
        internal bool Frozen;

        // 分母，冻结后不因决策、换计划、转编或增援而重置。
        internal int NE;
        internal int NF;

        internal bool Reinforced;
        internal bool Stopped;
        internal bool OffensiveBlocked;

        // 继任等待期：旧攻势已经关闭、退出动作已排队，但尚未确认应用。
        // 这段时间里不得 Adopt 新计划、不得占槽、不得武装行为（派单 §2 整改1）。
        internal bool WaitingForExitConfirmation;
        internal bool ExitStandbyCompleted;
        internal string PendingStandbyPlanId = "";
        internal double StandbyDeadlineAt = -1.0;

        private int _consecutive;

        internal bool NoteTerminal(string missionKey, MemberIdentity member, MemberEndState state,
                                   double at)
        {
            if (!Frozen) return false;
            if (!string.Equals(missionKey, MissionKey, StringComparison.Ordinal)) return false;
            // t0 前的损失属于行动前代价，迟到送达也不计入本次行动结果；时刻不可读同样不记。
            if (!BattleState.IsFinite(at) || at < T0) return false;
            string key = member.Key();
            int side;
            if (!_side.TryGetValue(key, out side)) return false;
            if (_terminal.ContainsKey(key)) return false;
            _terminal[key] = state == MemberEndState.None ? MemberEndState.Unknown : state;
            return true;
        }

        // 冻结基线。成功时才建立 E0/F0 与分母；失败原因逐条可核对。
        internal bool Freeze(FormationSnapshot snapshot, int myTeamIndex, string mainFormationKey,
                             string planId, string planInputId, double t0, out string detail)
        {
            detail = "";
            if (snapshot == null) { detail = "no_snapshot"; return false; }
            var main = snapshot.FindByKey(mainFormationKey);
            if (main == null) { detail = "main_formation_missing"; return false; }
            if (!main.HasActiveCenter) { detail = "main_center_unknown"; return false; }

            FormationRecord observe = null;
            double best = double.MaxValue;
            for (int i = 0; i < snapshot.Records.Count; i++)
            {
                var r = snapshot.Records[i];
                if (r.TeamIndex == myTeamIndex) continue;
                if (!ActionFeedback.IsInfantryClass(r.FormationClass)) continue;
                if (!r.HasActiveCenter) continue;
                double dx = r.ActiveCenterX - main.ActiveCenterX;
                double dy = r.ActiveCenterY - main.ActiveCenterY;
                double d = Math.Sqrt(dx * dx + dy * dy);
                if (!BattleState.IsFinite(d)) continue;
                if (observe == null || d < best - 1e-9 ||
                    (Math.Abs(d - best) <= 1e-9 && ActionFeedback.CompareStable(r, observe) < 0))
                {
                    observe = r;
                    best = d;
                }
            }
            if (observe == null) { detail = "no_enemy_infantry"; return false; }
            if (observe.ActiveMembers.Count < TestParameters.P3MinimumEnemyMembers)
            {
                detail = "enemy_infantry_below_20";
                return false;
            }

            int nf = 0;
            for (int i = 0; i < snapshot.Records.Count; i++)
            {
                var r = snapshot.Records[i];
                if (r.TeamIndex != myTeamIndex) continue;
                nf += r.ActiveMembers.Count;
            }
            if (nf <= 0) { detail = "no_friendly_members"; return false; }

            _side.Clear();
            _terminal.Clear();
            _samples.Clear();
            MissionKey = snapshot.MissionKey;
            PlanId = planId;
            PlanInputId = planInputId;
            ObserveObjectIdentity = observe.ObjectIdentity;
            T0 = t0;
            NE = observe.ActiveMembers.Count;
            NF = nf;
            for (int i = 0; i < observe.ActiveMembers.Count; i++)
            {
                _side[observe.ActiveMembers[i].Key()] = SideEnemy;
            }
            for (int i = 0; i < snapshot.Records.Count; i++)
            {
                var r = snapshot.Records[i];
                if (r.TeamIndex != myTeamIndex) continue;
                for (int j = 0; j < r.ActiveMembers.Count; j++)
                {
                    _side[r.ActiveMembers[j].Key()] = SideFriendly;
                }
            }
            Frozen = true;
            Reinforced = false;
            Stopped = false;
            OffensiveBlocked = false;
            _consecutive = 0;
            // 行动起点按定义零损失，作为速率窗口的第一个有效样本。
            // 正常 3 s 节奏下这使得速率在 t0+12 s 的评价点即可用。
            _samples.Add(new Sample { SampledAt = t0, DF = 0 });
            detail = "frozen NE=" + NE + " NF=" + NF;
            return true;
        }

        // 继任计划：继承 E0/F0、t0、累计损失与速率窗口，不继承连续计数。
        internal ActionFeedbackTracker CloneForSuccessor(string planId)
        {
            var c = new ActionFeedbackTracker
            {
                MissionKey = MissionKey,
                PlanId = planId,
                PlanInputId = PlanInputId,
                ObserveObjectIdentity = ObserveObjectIdentity,
                T0 = T0,
                NE = NE,
                NF = NF,
                Frozen = Frozen,
                Reinforced = Reinforced,
                Stopped = Stopped,
                OffensiveBlocked = OffensiveBlocked,
            };
            c._side.Clear();
            foreach (var kv in _side) c._side[kv.Key] = kv.Value;
            foreach (var kv in _terminal) c._terminal[kv.Key] = kv.Value;
            c._samples.AddRange(_samples);
            return c;
        }

        // 继任计划换身份但保留同一行动基线：t0、E0/F0、累计损失与速率窗口都不重算
        // （派单 §2 整改1：继任保留 E0/F0 及累计损失）。
        internal void InheritFeedbackStandby(string planId)
        {
            PlanId = planId ?? "";
        }

        // 原继任只完成旧计划身份的收尾，新计划在退出确认之后才改名上场：
        // 在旧计划已经被关闭之后把它移交给新计划身份。
        // 等待期本身就处于冻结状态，所以这里不因 Frozen 拒绝。
        internal bool RetargetStandbyPlan(string fromPlanId, string toPlanId)
        {
            if (fromPlanId == null || fromPlanId.Length == 0) return false;
            if (!string.Equals(PlanId, fromPlanId, StringComparison.Ordinal)) return false;
            PlanId = toPlanId ?? "";
            return true;
        }

        // 退出动作的确认口径与 Host 的 ExitStopLedger 一致：每个对象每次实际关闭
        // 恰好登记一次，登记成立即授权。这里只做同口径状态转移，不引入新判据。
        // 正反馈继任只是换了分配，旧攻势并未被判定停止；把它也置成禁止进攻会让
        // 后续累计损失评价被直接跳过（独立验收 R1）。只有负反馈继任才落实禁入。
        internal void AfterStandbyCompleted(out bool stopped, out bool blocked)
        {
            stopped = false;
            blocked = false;
            if (WaitingForExitConfirmation && ExitStandbyCompleted && !Reinforced)
            {
                Stopped = true;
                OffensiveBlocked = true;
            }
            stopped = Stopped;
            blocked = OffensiveBlocked;
        }

        // 等退出确认期间旧攻势已被关闭；若到截止仍未被应用，本次接续就此终止，
        // 如实保留停止失败，不无限等待、不重投、不提前接管（派单 §2 整改1）。
        // 已判定的负反馈禁入是评价结论，不随本次等待的成败被清掉（独立验收 R1）：
        // 退出失败说明停不下来，不等于可以重新放行已被判定无效的攻势。
        internal void MarkStandbyFailed()
        {
            if (!WaitingForExitConfirmation) return;
            WaitingForExitConfirmation = false;
            ExitStandbyCompleted = false;
            PendingStandbyPlanId = "";
            StandbyDeadlineAt = -1.0;
        }

        private void CountTerminals(out int de, out int df, out int routed, out int unknown)
        {
            de = 0; df = 0; routed = 0; unknown = 0;
            foreach (var kv in _terminal)
            {
                int side;
                if (!_side.TryGetValue(kv.Key, out side)) continue;
                switch (kv.Value)
                {
                    case MemberEndState.Killed:
                    case MemberEndState.Unconscious:
                        if (side == SideEnemy) de++; else df++;
                        break;
                    case MemberEndState.Routed:
                        routed++;
                        break;
                    default:
                        unknown++;
                        break;
                }
            }
        }

        // 当前快照下的局部存续读数：原观察对象是否还在、E0 成员还剩几个在原对象内、
        // 是否有显著转编（多数 E0 成员已出现在别的敌编队里）。用对象身份匹配原目标，
        // 不用 Team/Formation 槽位——槽位会被转编复用。
        internal void ObserveSurvival(FormationSnapshot snapshot, out bool targetObjectPresent,
                                      out int targetActiveE0Members, out bool regroupedAway)
        {
            targetObjectPresent = false;
            targetActiveE0Members = 0;
            regroupedAway = false;
            if (snapshot == null || !Frozen) return;

            FormationRecord target = null;
            for (int i = 0; i < snapshot.Records.Count; i++)
            {
                var r = snapshot.Records[i];
                if (string.Equals(r.ObjectIdentity, ObserveObjectIdentity, StringComparison.Ordinal))
                {
                    target = r;
                    break;
                }
            }
            targetObjectPresent = target != null;

            int inTarget = 0;
            int outside = 0;
            foreach (var kv in _side)
            {
                if (kv.Value != SideEnemy) continue;
                for (int i = 0; i < snapshot.Records.Count; i++)
                {
                    var r = snapshot.Records[i];
                    bool found = false;
                    for (int j = 0; j < r.ActiveMembers.Count; j++)
                    {
                        if (string.Equals(r.ActiveMembers[j].Key(), kv.Key, StringComparison.Ordinal))
                        {
                            found = true;
                            break;
                        }
                    }
                    if (!found) continue;
                    if (target != null && ReferenceEquals(r, target)) inTarget++;
                    else outside++;
                    break;
                }
            }
            targetActiveE0Members = inTarget;
            regroupedAway = outside > inTarget;
        }

        // 冻结成员逐个可解释性核对：同一份有效输入里，每个 E0/F0 成员要么按稳定身份
        // 出现在当前活跃成员中（含转编、分离到别的编队），要么已有可信既有终态。
        // 无法解释的消失、身份冲突或读取不完整都返回 false，由调用方阻断该窗口的正负判定。
        internal bool MissingCoverage(string missionKey, FormationSnapshot snapshot, out string detail)
        {
            detail = "";
            if (!Frozen) { detail = "not_frozen"; return false; }
            if (snapshot == null) { detail = "no_snapshot"; return false; }
            if (!string.Equals(missionKey, MissionKey, StringComparison.Ordinal))
            {
                detail = "mission_changed";
                return false;
            }

            var present = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < snapshot.Records.Count; i++)
            {
                var r = snapshot.Records[i];
                if (r == null) continue;
                if (r.ActiveMembers == null) { detail = "members_unreadable"; return false; }
                for (int j = 0; j < r.ActiveMembers.Count; j++)
                {
                    var m = r.ActiveMembers[j];
                    string mk = m.Key();
                    if (mk.Length == 0) { detail = "members_unreadable"; return false; }
                    present.Add(mk);
                }
            }

            foreach (var kv in _side)
            {
                if (present.Contains(kv.Key)) continue;
                if (_terminal.ContainsKey(kv.Key)) continue;
                detail = "unexplained_member key=" + kv.Key;
                return false;
            }
            return true;
        }

        internal ActionFeedbackEvaluation Evaluate(ActionFeedbackInput input)
        {
            var ev = new ActionFeedbackEvaluation
            {
                Now = input != null ? input.Now : 0.0,
                T0 = T0,
                NE = NE,
                NF = NF,
            };
            if (!Frozen)
            {
                ev.IncompleteReason = "not_frozen";
                ev.Reason = "not_frozen";
                return ev;
            }
            if (!input.IntegrityOk)
            {
                // 完整性不成立的评价点既不参与连续成立，也不进入速率窗口。
                _consecutive = 0;
                ev.IncompleteReason = "integrity:" + input.IntegrityNote;
                ev.Reason = "integrity_unknown";
                return ev;
            }
            if (NE <= 0 || NF <= 0)
            {
                // 空分母是未知，不是零代价；不得据此给出任何方向的结论。
                _consecutive = 0;
                ev.IncompleteReason = "empty_denominator";
                ev.Reason = "empty_denominator";
                ev.Decision = ActionFeedbackDecision.Continue;
                return ev;
            }

            int de, df, routed, unknown;
            CountTerminals(out de, out df, out routed, out unknown);
            ev.DE = de; ev.DF = df; ev.RoutedCount = routed; ev.UnknownCount = unknown;
            ev.LE = (double)de / NE;
            ev.LF = (double)df / NF;
            ev.StableSeconds = input.Now - T0;

            // 完整性门先于决定表：存在无法解释的基线成员终态（Deleted/None/异常读取）时，
            // 本窗口不可判正负。未知点既不进入速率窗口，也不参与四类决定。
            if (unknown > 0)
            {
                _consecutive = 0;
                ev.HRf = double.NaN;
                ev.CostTerm = ev.LF;
                ev.IncompleteReason = "unknown_terminal count=" + unknown;
                ev.Reason = "unknown_terminal count=" + unknown;
                ev.Decision = ActionFeedbackDecision.Continue;
                ev.ConsecutiveCount = 0;
                return ev;
            }

            ev.Complete = true;

            double s = double.NaN;
            for (int i = 0; i < _samples.Count; i++)
            {
                var sample = _samples[i];
                if (input.Now - sample.SampledAt < TestParameters.P3RateWindowSeconds) continue;
                if (double.IsNaN(s) || sample.SampledAt > s) s = sample.SampledAt;
            }
            if (!double.IsNaN(s) && input.Now > s)
            {
                int dfThen = 0;
                for (int i = 0; i < _samples.Count; i++)
                {
                    if (_samples[i].SampledAt == s) { dfThen = _samples[i].DF; break; }
                }
                ev.RateWindow = input.Now - s;
                ev.Rf = (double)(df - dfThen) / (NF * ev.RateWindow);
                ev.RateKnown = BattleState.IsFinite(ev.Rf) && ev.Rf >= 0.0;
            }
            // 速率未知时只用累计损失项，不把未知补成 0。
            ev.HRf = ev.RateKnown ? TestParameters.P3RateWindowSeconds * ev.Rf : double.NaN;
            ev.CostTerm = ev.RateKnown ? Math.Max(ev.LF, ev.HRf) : ev.LF;
            ev.CostKnown = ev.RateKnown;
            ev.B = ActionFeedback.Clamp01(ev.LE / TestParameters.P3LocalLossScale);
            ev.C = ActionFeedback.Clamp01(ev.CostTerm / TestParameters.P3SideLossScale);
            ev.U = 0.5 * ev.B + 0.5 * (1.0 - ev.C);

            _samples.Add(new Sample { SampledAt = input.Now, DF = df });

            // 冲突优先级：安全失败 > 高损失止损 > 正反馈 > 继续。
            // 安全失败由既有通路处理，这里不产生 P3 决定。

            // ② 高损失止损：第一个满足条件的评价点即决定，允许打破稳定时间。
            bool stopByLoss = ev.LF >= TestParameters.P3StopLossLf;
            bool stopByRate = ev.RateKnown && ev.HRf >= TestParameters.P3StopLossRate;
            if (stopByLoss || stopByRate)
            {
                _consecutive = 1;
                Stopped = true;
                OffensiveBlocked = true;
                ev.Decision = ActionFeedbackDecision.StopLoss;
                ev.ConsecutiveCount = _consecutive;
                ev.ExceptionUsed = true;
                ev.ExceptionReason = "safety_first_break_stability";
                ev.Reason = "stop_loss " + (stopByLoss ? "cumulative" : "rate") +
                            " LF=" + ActionFeedback.Fmt(ev.LF) + " H*rf=" +
                            (ev.RateKnown ? ActionFeedback.Fmt(ev.HRf) : "unknown");
                return ev;
            }

            // ① 正反馈追加。
            string block = null;
            if (Reinforced) block = "already_reinforced";
            else if (OffensiveBlocked) block = "offensive_blocked";
            else if (input.Now - T0 < TestParameters.P3RateWindowSeconds) block = "window_not_mature";
            else if (!ev.RateKnown) block = "rate_unknown";
            else if (!input.TargetObjectPresent) block = "local_value_lost target_absent";
            else if (input.RegroupedAway) block = "local_value_lost regrouped";
            else if (input.TargetActiveE0Members < TestParameters.P3LocalValueMinMembers)
                block = "local_value_lost members=" + input.TargetActiveE0Members;
            else if (!input.MyForceContinueLineHolds) block = "force_continue_line_lost";
            else if (!input.RangedAdvantageStillHolds) block = "ranged_advantage_lost";
            else if (!input.PlanStillValid) block = "plan_invalid";
            else if (ev.LE < TestParameters.P3ReinforceLe) block = "no_local_gain";
            else if (ev.LF > TestParameters.P3ReinforceLf) block = "own_loss_too_high";
            else if (ev.HRf > TestParameters.P3ReinforceRate) block = "own_loss_rate_too_high";
            else if (ev.U < TestParameters.P3ReinforceU) block = "utility_below_reinforce";
            else if (!input.RiskCenterKnown) block = "risk_center_unknown";
            else if (!input.RolesAboveContinueLine) block = "role_below_continue_line";
            else if (input.EnemyWithinRiskRadius > input.MyRoleUnits) block = "enemy_more_than_roles";

            if (block == null)
            {
                _consecutive++;
                if (_consecutive >= 2)
                {
                    Reinforced = true;
                    ev.Decision = ActionFeedbackDecision.Reinforce;
                    ev.Reason = "reinforce LE=" + ActionFeedback.Fmt(ev.LE) + " LF=" + ActionFeedback.Fmt(ev.LF) +
                                " H*rf=" + ActionFeedback.Fmt(ev.HRf) + " U=" + ActionFeedback.Fmt(ev.U);
                }
                else
                {
                    ev.Reason = "reinforce_gate_1of2";
                }
                ev.ConsecutiveCount = _consecutive;
                return ev;
            }

            // ③ 无收益且有代价：连续两个评价点成立才调整撤回。
            bool noGain = ev.LE < TestParameters.P3DisengageLe;
            bool hasCost = ev.LF >= TestParameters.P3DisengageLf;
            bool lowUtility = ev.U < TestParameters.P3DisengageU;
            if (noGain && hasCost && lowUtility)
            {
                if (input.Now - T0 < TestParameters.P3DisengageMinSeconds)
                {
                    _consecutive = 0;
                    ev.Reason = "window_not_mature disengage";
                    return ev;
                }
                _consecutive++;
                if (_consecutive >= 2)
                {
                    Stopped = true;
                    OffensiveBlocked = true;
                    ev.Decision = ActionFeedbackDecision.Disengage;
                    ev.Reason = "disengage LE=" + ActionFeedback.Fmt(ev.LE) + " LF=" + ActionFeedback.Fmt(ev.LF) +
                                " U=" + ActionFeedback.Fmt(ev.U);
                }
                else
                {
                    ev.Reason = "disengage_gate_1of2";
                }
                ev.ConsecutiveCount = _consecutive;
                return ev;
            }

            _consecutive = 0;
            ev.ConsecutiveCount = 0;
            ev.Reason = "continue " + block + " LE=" + ActionFeedback.Fmt(ev.LE) + " LF=" + ActionFeedback.Fmt(ev.LF) +
                        " U=" + ActionFeedback.Fmt(ev.U) + " rate=" +
                        (ev.RateKnown ? ActionFeedback.Fmt(ev.Rf) : "unknown");
            return ev;
        }
    }

    internal static class ActionFeedback
    {
        // 负反馈有限撤回计划的身份标记。撤回任务不承担协同职责，
        // 因此 Role 必须为 None，避免落入协同几何与协同写前门。
        internal const string GoalWithdraw = "p3_withdraw";
        internal const string ReasonWithdraw = "p3_withdraw_reposition";
        internal const string ReasonWithdrawHold = "p3_withdraw_hold";

        internal static string Fmt(double v)
        {
            return v.ToString("F3", CultureInfo.InvariantCulture);
        }

        internal static double Clamp01(double v)
        {
            if (!BattleState.IsFinite(v)) return 0.0;
            if (v < 0.0) return 0.0;
            if (v > 1.0) return 1.0;
            return v;
        }

        internal static bool IsInfantryClass(string formationClass)
        {
            if (string.IsNullOrEmpty(formationClass)) return false;
            if (formationClass.IndexOf("Infantry", StringComparison.OrdinalIgnoreCase) < 0) return false;
            return formationClass.IndexOf("Ranged", StringComparison.OrdinalIgnoreCase) < 0;
        }

        // 风险门：以拟新增主力目标点为圆心，统计半径内所有敌方非坐骑有效人数，
        // 与本方三职责当前有效人数总和比较。等于允许；任一职责低于继续线则不追加。
        // 三职责必须齐备、互不相同，且任务上的对象身份仍与当前快照一致，否则映射
        // 过期或冲突，按合同不放行。
        internal static RiskGateResult RiskGate(FormationSnapshot snapshot, int myTeamIndex,
                                                List<RoleAssignment> roles,
                                                double centerX, double centerY)
        {
            var result = new RiskGateResult();
            if (snapshot == null) { result.Detail = "no_snapshot"; return result; }
            if (roles == null)
            {
                result.Detail = "roles_incomplete";
                return result;
            }

            int mine = 0;
            bool rolesHold = true;
            bool seenMain = false, seenGuard = false, seenSupport = false;
            var seenObjects = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < roles.Count; i++)
            {
                var role = roles[i];
                if (role == null) continue;
                if (role.Role == FormationRole.Main) seenMain = true;
                else if (role.Role == FormationRole.Guard) seenGuard = true;
                else if (role.Role == FormationRole.Support) seenSupport = true;
                else continue;
                if (string.IsNullOrEmpty(role.ObjectIdentity)
                    || !seenObjects.Add(role.ObjectIdentity))
                {
                    result.Detail = "roles_not_distinct";
                    return result;
                }
            }
            if (!seenMain || !seenGuard || !seenSupport)
            {
                result.Detail = "roles_incomplete";
                return result;
            }

            for (int i = 0; i < roles.Count; i++)
            {
                var role = roles[i];
                if (role == null) continue;
                if (role.Role == FormationRole.None) continue;
                var r = snapshot.FindByKey(role.FormationKey);
                if (r == null)
                {
                    result.Detail = "role_object_missing key=" + role.FormationKey;
                    return result;
                }
                if (!string.Equals(r.ObjectIdentity, role.ObjectIdentity, StringComparison.Ordinal))
                {
                    result.Detail = "role_identity_stale key=" + role.FormationKey;
                    return result;
                }
                mine += r.ActiveMemberCount;
                if (!PlanBuilder.RoleStrengthHolds(r.ActiveMemberCount, role.BaselineUnits))
                {
                    rolesHold = false;
                }
            }

            if (!BattleState.IsFinite(centerX) || !BattleState.IsFinite(centerY))
            {
                result.Detail = "center_unknown";
                return result;
            }
            result.CenterKnown = true;

            int enemy = 0;
            for (int i = 0; i < snapshot.Records.Count; i++)
            {
                var r = snapshot.Records[i];
                if (r.TeamIndex == myTeamIndex) continue;
                // 位置未知的敌方编队无法判定是否落入半径，跳过会低估风险等于放行；
                // 按合同不追加并说明原因。
                if (!r.HasActiveCenter)
                {
                    result.Detail = "enemy_center_unknown";
                    return result;
                }
                double dx = r.ActiveCenterX - centerX;
                double dy = r.ActiveCenterY - centerY;
                if (Math.Sqrt(dx * dx + dy * dy) <= TestParameters.P3RiskRadiusMeters)
                {
                    enemy += r.ActiveMemberCount;
                }
            }
            result.EnemyWithinRadius = enemy;

            result.MyRoleUnits = mine;
            result.RolesAboveContinueLine = rolesHold;
            if (!rolesHold) { result.Detail = "role_below_continue_line"; return result; }
            if (enemy > mine) { result.Detail = "enemy_more_than_roles"; return result; }
            result.Allowed = true;
            result.Detail = "allowed";
            return result;
        }

        // 距离相同时的稳定排序：先 TeamIndex/FormationIndex 槽位，再对象身份。
        internal static int CompareStable(FormationRecord a, FormationRecord b)
        {
            if (a.TeamIndex != b.TeamIndex) return a.TeamIndex < b.TeamIndex ? -1 : 1;
            if (a.FormationIndex != b.FormationIndex) return a.FormationIndex < b.FormationIndex ? -1 : 1;
            return string.CompareOrdinal(a.ObjectIdentity ?? "", b.ObjectIdentity ?? "");
        }

        // 每个被旧计划关掉的对象的退出结果。时刻是实际写出时刻，无法只靠布尔值区分
        // "及时成功"与"截止后才写出"（派单 §2 整改1）。
        internal sealed class ExitOutcome
        {
            internal string ObjectIdentity = "";
            internal bool Written;
            internal double WrittenAt = -1.0;
            internal bool OwnershipTakenByNewAssignment;
            internal bool Unapplied;
        }

        // 接续判定的全部输入：旧计划身份、逐对象退出结果、截止与继任有效性事实。
        // 只承认真实观测到的事实，缺项一律不放行。
        internal sealed class SuccessorStandby
        {
            internal string OldPlanId = "";
            internal string PendingPlanId = "";
            internal double Now;
            internal double DeadlineAt = -1.0;

            // 旧计划里必需的全部普通任务身份（排除退出任务）。缺项即不可判定。
            internal List<string> RequiredObjects;

            internal List<ExitOutcome> Exits;

            internal bool SuccessorPrepared;
            internal string SuccessorInvalidReason = "";
        }

        internal enum StandbyVerdict
        {
            Waiting = 0,
            Confirmed = 1,
            TimedOut = 2,
        }

        internal sealed class StandbyDecision
        {
            internal StandbyVerdict Verdict = StandbyVerdict.Waiting;
            internal string Detail = "";
        }

        // 退出确认门：旧计划里必需的全部对象都必须有"在截止前真正写出"的退出结果。
        // 只完成一部分不得公布继任；截止后才写出不得补算成功；截止到达即终止本次接续，
        // 如实保留停止失败（派单 §2 整改1）。
        internal static StandbyDecision DecideSuccessorStandby(SuccessorStandby input)
        {
            var d = new StandbyDecision();
            if (input == null) { d.Detail = "no_input"; return d; }
            if (!BattleState.IsFinite(input.Now)) { d.Detail = "now_unknown"; return d; }
            if (input.OldPlanId == null || input.OldPlanId.Length == 0)
            { d.Detail = "old_plan_missing"; return d; }
            if (input.PendingPlanId == null || input.PendingPlanId.Length == 0)
            { d.Detail = "pending_plan_missing"; return d; }
            if (input.RequiredObjects == null) { d.Detail = "required_objects_missing"; return d; }
            if (input.Exits == null) { d.Detail = "exit_facts_missing"; return d; }
            if (!BattleState.IsFinite(input.DeadlineAt)) { d.Detail = "deadline_unknown"; return d; }

            var exits = new Dictionary<string, ExitOutcome>(StringComparer.Ordinal);
            for (int i = 0; i < input.Exits.Count; i++)
            {
                var x = input.Exits[i];
                if (x == null || x.ObjectIdentity == null || x.ObjectIdentity.Length == 0) continue;
                if (!exits.ContainsKey(x.ObjectIdentity)) exits[x.ObjectIdentity] = x;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            // 只收集首个拦阻原因用于报告；终态一律由截止判定，具体原因不得提前返回——
            // 否则到截止仍停在"等待"，既不确认也不终止（派单 §2 整改1）。
            bool allApplied = true;
            string blocked = "";
            for (int i = 0; i < input.RequiredObjects.Count; i++)
            {
                string obj = input.RequiredObjects[i];
                if (obj == null || obj.Length == 0) { d.Detail = "required_object_empty"; return d; }
                if (!seen.Add(obj)) { d.Detail = "required_object_duplicated obj=" + obj; return d; }

                ExitOutcome x;
                if (!exits.TryGetValue(obj, out x))
                { allApplied = false; if (blocked.Length == 0) blocked = "exit_outcome_missing obj=" + obj; continue; }
                if (x.OwnershipTakenByNewAssignment)
                { allApplied = false; if (blocked.Length == 0) blocked = "ownership_taken obj=" + obj; continue; }
                if (x.Unapplied)
                { allApplied = false; if (blocked.Length == 0) blocked = "exit_unapplied obj=" + obj; continue; }
                if (!x.Written) { allApplied = false; continue; }
                if (!BattleState.IsFinite(x.WrittenAt))
                { allApplied = false; if (blocked.Length == 0) blocked = "exit_time_unknown obj=" + obj; continue; }
                // 截止后才写出不是及时停止：不计入已应用，按截止终止。
                if (x.WrittenAt > input.DeadlineAt)
                { allApplied = false; if (blocked.Length == 0) blocked = "exit_written_after_deadline obj=" + obj; continue; }
            }

            if (allApplied)
            {
                // 必需对象集为空说明本组没有可确认的停止结果，不能当作"全部已应用"放行。
                if (input.RequiredObjects.Count == 0) blocked = "required_objects_missing";
                else if (!input.SuccessorPrepared) blocked = "successor_not_prepared";
                else if (input.SuccessorInvalidReason != null && input.SuccessorInvalidReason.Length != 0)
                    blocked = input.SuccessorInvalidReason;

                if (blocked.Length == 0)
                {
                    d.Verdict = StandbyVerdict.Confirmed;
                    d.Detail = "all_exits_applied";
                    return d;
                }
            }

            if (input.Now >= input.DeadlineAt)
            {
                d.Verdict = StandbyVerdict.TimedOut;
                d.Detail = blocked.Length != 0 ? blocked : "exit_not_applied_in_time";
                return d;
            }

            d.Verdict = StandbyVerdict.Waiting;
            d.Detail = blocked.Length != 0 ? blocked : "awaiting_exit_confirmation";
            return d;
        }

        // 公布继任之前的逐任务复核：把任务上记录的对象身份与当前快照读到的身份比较，
        // 并按对象的实际状态核对归属、AI 资格、兵种类别与人数继续线。
        // 只核对键对应记录存在是不够的——对象可能已被重分配、失去 AI 控制或降员
        // （独立验收 R1）。任一条不成立即返回原因，不放行。
        internal static string SuccessorRolesStillValid(List<SuccessorRoleFact> roles,
                                                        int myTeamIndex,
                                                        bool coordination)
        {
            if (roles == null) return "roles_incomplete";

            var keys = new HashSet<string>(StringComparer.Ordinal);
            int main = 0, guard = 0, support = 0, counted = 0;
            for (int i = 0; i < roles.Count; i++)
            {
                var f = roles[i];
                if (f == null) continue;
                if (!f.Found) return coordination ? "role_object_missing" : "withdraw_object_missing";
                if (!f.HasActiveCenter || f.ActiveMemberCount <= 0)
                    return coordination ? "role_object_missing" : "withdraw_object_missing";
                // 计划准备时记下的对象身份必须仍是当前快照里的同一对象（独立验收 R1）。
                if (!string.Equals(f.CurrentObjectIdentity, f.ObjectIdentity, StringComparison.Ordinal))
                    return "role_identity_stale key=" + f.FormationKey;
                if (f.TeamIndex != myTeamIndex) return "role_not_ours key=" + f.FormationKey;
                if (!f.IsAiControlled) return "role_not_ours key=" + f.FormationKey;
                if (!PlanBuilder.ClassAllows(f.FormationClass, f.Kind))
                    return "role_class_not_allowed key=" + f.FormationKey;
                if (f.Kind != TaskKind.Hold &&
                    !PlanBuilder.RoleStrengthHolds(f.ActiveMemberCount, f.BaselineUnits))
                    return "role_below_continue_line key=" + f.FormationKey;
                if (!keys.Add(f.FormationKey))
                    return coordination ? "roles_not_distinct" : "withdraw_objects_not_distinct";
                counted++;
                switch (f.Role)
                {
                    case FormationRole.Main: main++; break;
                    case FormationRole.Guard: guard++; break;
                    case FormationRole.Support: support++; break;
                }
            }

            if (counted == 0) return "plan_not_prepared";
            if (!coordination) return "";
            if (main != 1 || guard != 1 || support != 1) return "roles_incomplete";
            return "";
        }

        // 继任计划新增主力的有限接近量：min(25m, 当前距离/4)。
        internal static bool ProjectedMainTarget(double mainX, double mainY, double enemyX,
                                                double enemyY, out double targetX,
                                                out double targetY)
        {
            targetX = mainX;
            targetY = mainY;
            if (!BattleState.IsFinite(mainX) || !BattleState.IsFinite(mainY) ||
                !BattleState.IsFinite(enemyX) || !BattleState.IsFinite(enemyY)) return false;
            double dx = enemyX - mainX;
            double dy = enemyY - mainY;
            double span = Math.Sqrt(dx * dx + dy * dy);
            if (span <= 0.0001) return false;
            double approach = Math.Min(TestParameters.P3SuccessorApproachMaxMeters,
                                       span / TestParameters.P3SuccessorApproachDivisor);
            targetX = mainX + dx / span * approach;
            targetY = mainY + dy / span * approach;
            return true;
        }

        // 有限撤回目标：从对象当前实际质心，沿背离敌队有效质心的方向移动 20m。
        // 敌我方向不可用时不编造退路，返回 false 由调用方只做终止。
        internal static bool WithdrawTarget(double ownX, double ownY, double enemyX, double enemyY,
                                            out double targetX, out double targetY)
        {
            targetX = ownX;
            targetY = ownY;
            if (!BattleState.IsFinite(ownX) || !BattleState.IsFinite(ownY) ||
                !BattleState.IsFinite(enemyX) || !BattleState.IsFinite(enemyY)) return false;
            double dx = ownX - enemyX;
            double dy = ownY - enemyY;
            double span = Math.Sqrt(dx * dx + dy * dy);
            if (span <= 0.0001) return false;
            targetX = ownX + dx / span * TestParameters.P3WithdrawMeters;
            targetY = ownY + dy / span * TestParameters.P3WithdrawMeters;
            return true;
        }

        internal static double Distance(double ax, double ay, double bx, double by)
        {
            double dx = ax - bx;
            double dy = ay - by;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }

    internal sealed class RiskGateResult
    {
        internal bool CenterKnown;
        internal bool Allowed;
        internal bool RolesAboveContinueLine;
        internal int EnemyWithinRadius;
        internal int MyRoleUnits;
        internal string Detail = "";
    }

    // 风险门的一个职责读数：角色、当前承担它的编队键、该编队当时公布的对象身份，
    // 以及该职责的继续线基线人数。三者必须来自同一次实际公布的当前分配。
    internal sealed class RoleAssignment
    {
        internal FormationRole Role = FormationRole.None;
        internal string FormationKey = "";
        internal string ObjectIdentity = "";
        internal int BaselineUnits;
    }

    // 继任计划逐任务的资格复核读数：任务上记录的身份与当前快照读到的身份分开列出，
    // 归属、AI 资格、类别与人数也各取一项事实，才能逐条说明是哪一项过期（独立验收 R1）。
    internal sealed class SuccessorRoleFact
    {
        internal FormationRole Role = FormationRole.None;
        internal string FormationKey = "";
        internal string ObjectIdentity = "";
        internal string FormationClass = "";
        internal TaskKind Kind = TaskKind.None;
        internal int BaselineUnits;

        internal bool Found;
        internal string CurrentObjectIdentity = "";
        internal int ActiveMemberCount;
        internal int TeamIndex;
        internal bool HasActiveCenter;
        internal bool IsAiControlled;
    }
}
