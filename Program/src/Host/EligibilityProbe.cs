using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CombatBrain2.Core;
using TaleWorlds.MountAndBlade;

namespace CombatBrain2.Host
{
    // 测试适配（默认关闭）：用公开原生接口改变一个真实编队的"本方可控资格"，
    // 给 P1 计划的真实失效提供实机触发与观测。触发条件按脚本 §7.1：
    // 同一计划连续存活 ≥ 8 s 且两个编队的普通任务都已取得正面反馈之后，
    // 只触发一次，保持 ≥ 4 s 再恢复；稳定性前提不满足时如实记录未触发。
    //   mode a/b：Formation.SetControlledByAI(false) —— AI 控制资格丢失。
    //   mode c  ：Mission.JoinEnemyTeam()          —— 公开原生入口，把 PlayerTeam 换成绑目标所在的队，
    //              使同一实际对象在引擎里变成玩家队（Team.IsPlayerTeam 由 Mission.PlayerTeam 派生）。
    // 不伪造玩家事件、不写私有字段、不绕过生产过滤与执行门、不自行写 MovementOrder。
    internal sealed class EligibilityProbe
    {
        internal static EligibilityProbe Instance = new EligibilityProbe();

        private const string ConfigFileName = "eligibility-probe.txt";

        // P2 测试适配的独立配置文件（默认关闭）。与 P1 探针分开，
        // 保证 P1 契约不受影响，且只有在 P2 路径开启时才会生效。
        private const string P2ConfigFileName = "p2-eligibility-probe.txt";

        // 失资格保持窗口：至少 4 秒，留出独立采样与逐 tick 检查的机会（脚本 §7.1）。
        private const double HoldSeconds = 4.0;

        // 场景 B 的替补保持窗口：须覆盖保护失资格后的处置证据采集，明显长于 4 s。
        private const double P2SubstituteKeepSeconds = 30.0;

        // 场景 A 的保护失资格窗口。R3 之后生产不再逐 tick 补回行为权重，
        // 原生 SetControlledByAI 的副作用会清零权重；恢复若落在重分配后新主力刚写令的时刻，
        // 会把这个尚未推进的计划直接打断（实机 32.040 恢复，5 ms 前刚写令）。
        // 窗口须晚于整条新身份链的写令与合法推进，合同下限仍是「至少 4 s」。
        private const double P2GuardLossHoldSeconds = 60.0;

        private sealed class Applied
        {
            internal string Mode = "";
            internal bool LossApplied;
            internal bool Restored;
            internal double RestoreAt = -1.0;
            internal Formation Target;
            internal string TargetKey = "";
            internal string PlanId = "";
            internal string TaskId = "";
            internal Team OriginalPlayerTeam;
        }

        private readonly List<Applied> _applied = new List<Applied>();
        private readonly List<string> _modes = new List<string>();
        private bool _loaded;
        private bool _fired;

        // ---- P2 测试适配状态（与上面的 P1 探针完全独立）----
        private P2AdaptationMode _p2Mode = P2AdaptationMode.None;
        private bool _p2Loaded;
        private bool _p2Fired;
        // 场景 B 的准备期已处理的替补对象；记录原状态以便只恢复自己改过的对象。
        private readonly List<Applied> _p2Substitutes = new List<Applied>();
        private bool _p2PrepareDone;

        // A5 竞争注入开关：只在 p2 配置文件显式给出 competition=1 时打开，
        // 缺文件或未设即关闭（派单 §9.1「P2 测试注入单独显式开启、默认关闭」）。
        internal bool P2CompetitionEnabled;

        private EligibilityProbe() { }

        private static string ConfigPath()
        {
            return Path.Combine(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CombatBrain2"),
                ConfigFileName);
        }

        private static string P2ConfigPath()
        {
            return Path.Combine(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CombatBrain2"),
                P2ConfigFileName);
        }

        // P2 适配配置：只在 p2_mode 明确给出时生效，缺文件或未设即为关闭。
        private void LoadP2Once()
        {
            if (_p2Loaded) return;
            _p2Loaded = true;
            string raw = "";
            string competition = "";
            try
            {
                string p = P2ConfigPath();
                if (File.Exists(p))
                {
                    foreach (var line in File.ReadAllLines(p))
                    {
                        string one = line == null ? "" : line.Trim();
                        if (one.Length == 0 || one.StartsWith("#", StringComparison.Ordinal)) continue;
                        int eq = one.IndexOf('=');
                        if (eq <= 0) continue;
                        string k = one.Substring(0, eq).Trim();
                        string v = one.Substring(eq + 1).Trim();
                        if (string.Equals(k, "p2_mode", StringComparison.OrdinalIgnoreCase))
                        {
                            raw = v.ToLowerInvariant();
                            continue;
                        }
                        if (string.Equals(k, "competition", StringComparison.OrdinalIgnoreCase))
                        {
                            competition = v.ToLowerInvariant();
                        }
                    }
                }
            }
            catch (Exception) { }

            if (raw == "guard_loss_after_established") _p2Mode = P2AdaptationMode.GuardLossAfterEstablished;
            else if (raw == "guard_loss_before_established") _p2Mode = P2AdaptationMode.GuardLossBeforeEstablished;
            else _p2Mode = P2AdaptationMode.None;

            P2CompetitionEnabled = competition == "1" || competition == "on" || competition == "true";

            Cb2Log.Line("P2_PROBE_CONFIG",
                "p2_mode=" + (raw.Length == 0 ? "<off>" : raw) +
                " source=" + P2ConfigFileName +
                " hold_seconds=" + Cb2Log.F(_p2Mode == P2AdaptationMode.GuardLossAfterEstablished ? P2GuardLossHoldSeconds : HoldSeconds) +
                " competition=" + (P2CompetitionEnabled ? "1" : "0") +
                " note=test_adapter_only_never_changes_production_gating");
        }

        private void LoadOnce()
        {
            if (_loaded) return;
            _loaded = true;
            try
            {
                string p = ConfigPath();
                if (!File.Exists(p)) return;
                foreach (var raw in File.ReadAllLines(p))
                {
                    string line = raw == null ? "" : raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = line.Substring(0, eq).Trim();
                    string v = line.Substring(eq + 1).Trim();
                    if (string.Equals(k, "mode", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(k, "modes", StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (var piece in v.Split(','))
                        {
                            string one = piece.Trim().ToLowerInvariant();
                            if (one.Length == 0) continue;
                            _modes.Add(one);
                        }
                    }
                }
            }
            catch (Exception) { }

            Cb2Log.Line("PROBE_CONFIG",
                "modes=" + (_modes.Count == 0 ? "<off>" : string.Join(",", _modes.ToArray())) +
                " source=" + ConfigFileName +
                " hold_seconds=" + Cb2Log.F(HoldSeconds) +
                " stable_seconds_required=" + Cb2Log.F(TestParameters.PlanMinStableSeconds) +
                " note=test-adapter_triggered_eligibility_change");
        }

        // 每个已确认主线程的 Mission tick 调用。
        // 触发与恢复分开：恢复即使计划已经失效也必须执行，这是"恢复后旧计划不复活"观测的前提。
        internal void OnMissionTick(Mission mission)
        {
            LoadOnce();
            if (mission == null) return;

            double now = TestDriver.Instance.SafeNow(mission);

            if (_modes.Count > 0 && !_fired) TryTrigger(mission, now);
            SweepRestores(mission, now);

            // P2 适配独立推进：默认关闭，且只在 P2 路径运行时才起作用。
            LoadP2Once();
            if (_p2Mode != P2AdaptationMode.None && TestDriver.Instance.CoordinationMode)
            {
                P2PrepareSubstitutes(mission, now);
                if (!_p2Fired) P2TryTrigger(mission, now);
                P2SweepRestores(mission, now);
            }
        }

        // ================= P2 测试适配（派单 §9.2） =================

        // 场景 B 的准备期：读取真实候选，保留能建立合法三职责计划的三支对象，
        // 把其余合格替补的 AI 资格关闭。额外对象继续计入全队人数。
        // 本适配只改资格，不关计划、不改依赖结果、不删士兵。
        private void P2PrepareSubstitutes(Mission mission, double now)
        {
            if (_p2Mode != P2AdaptationMode.GuardLossBeforeEstablished) return;
            if (_p2PrepareDone) return;

            var snapshot = TestDriver.Instance.Snapshot;
            var plan = TestDriver.Instance.Plan;
            if (snapshot == null || plan == null) return;

            // 计划尚未建立时先等：要保留的正是"能建立合法三职责计划"的那三支。
            if (!CoordinationDriver.IsCoordinationPlan(plan)) return;

            var keep = new List<string>();
            var d = TestDriver.Instance.Coordination;
            if (d != null)
            {
                if (d.MainWaitTask != null) keep.Add(d.MainWaitTask.FormationKey);
                if (d.GuardTask != null) keep.Add(d.GuardTask.FormationKey);
                if (d.SupportTask != null) keep.Add(d.SupportTask.FormationKey);
            }
            if (keep.Count < 3) return;

            int processed = 0;
            var keys = new List<string>();
            foreach (var r in snapshot.Records)
            {
                if (r.IsPlayerTeam || r.IsPlayerControlled) continue;
                if (!r.IsAiControlled) continue;
                if (!PlanBuilder.ClassAllows(r.FormationClass, TaskKind.Reposition)) continue;
                if (keep.Contains(r.Key())) continue;

                var f = TestDriver.Instance.FindFormationByKey(mission, r.Key());
                if (f == null) continue;

                var a = new Applied
                {
                    Mode = "p2_substitute",
                    Target = f,
                    TargetKey = r.Key(),
                    PlanId = plan.PlanId,
                };
                _p2Substitutes.Add(a);
                P2ApplyLoss(a, now);
                processed++;
                keys.Add(r.Key());
            }

            _p2PrepareDone = true;
            Cb2Log.Line("P2_PROBE_SUBSTITUTES",
                "plan=" + plan.PlanId +
                " kept=" + string.Join(",", keep.ToArray()) +
                " disabled_count=" + processed +
                " disabled=" + (keys.Count == 0 ? "<none>" : string.Join(",", keys.ToArray())) +
                " note=extra_substitutes_keep_counting_toward_team_strength" +
                " at=" + Cb2Log.F(now));
        }

        // 触发判据全部交给纯逻辑 EligibilityTrigger，便于离线反例逐条钉住。
        private void P2TryTrigger(Mission mission, double now)
        {
            var plan = TestDriver.Instance.Plan;
            var d = TestDriver.Instance.Coordination;
            if (d == null) return;

            var input = new EligibilityTriggerInput
            {
                Mode = _p2Mode,
                PlanOpen = plan != null && !plan.IsClosed,
                PlanIsCoordination = plan != null && CoordinationDriver.IsCoordinationPlan(plan),
                GuardTaskPresent = d.GuardTask != null,
                GuardTaskRevision = d.GuardTask != null ? d.GuardTask.Revision : -1,
                PlanRevision = plan != null ? plan.Revision : -1,
                RelationshipEstablished = d.GuardReadiness != null && d.GuardReadiness.Ready &&
                                          d.SupportReadiness != null && d.SupportReadiness.Ready,
                MainAdvancing = d.Phase == CoordinationPhase.MainAdvancing,
                AdvanceCommandWritten = d.AdvanceTask != null && d.AdvanceTask.WrittenAt >= 0.0,
                AdvanceWrittenAgeSeconds = d.AdvanceTask != null && d.AdvanceTask.WrittenAt >= 0.0
                    ? now - d.AdvanceTask.WrittenAt : -1.0,
                CompetitionWindowSatisfied = TestDriver.Instance.CompetitionWindowSatisfied,
                GuardCommandWritten = d.GuardTask != null && d.GuardTask.WrittenAt >= 0.0,
                SupportCommandWritten = d.SupportTask != null && d.SupportTask.WrittenAt >= 0.0,
                MainStillWaiting = d.MainWaitTask != null && !d.MainWaitTask.IsClosed,
                PlanAgeSeconds = plan != null ? now - plan.CreatedAt : 0.0,
                StableSecondsRequired = TestParameters.PlanMinStableSeconds,
            };

            var decision = EligibilityTrigger.Decide(input);
            if (!decision.Fire) return;

            // 目标永远是版本绑定的实际保护编队，不是 OpenTasks 里第一个已生效对象。
            var f = TestDriver.Instance.FindFormationByKey(mission, d.GuardTask.FormationKey);
            if (f == null)
            {
                Cb2Log.Line("P2_PROBE_TARGET_MISSING",
                    "plan=" + plan.PlanId +
                    " key=" + d.GuardTask.FormationKey +
                    " reason=guard_formation_not_found at=" + Cb2Log.F(now));
                return;
            }

            _p2Fired = true;
            var a = new Applied
            {
                Mode = "p2",
                Target = f,
                TargetKey = d.GuardTask.FormationKey,
                PlanId = plan.PlanId,
                TaskId = d.GuardTask.TaskId,
            };
            _applied.Add(a);

            Cb2Log.Line("P2_PROBE_TRIGGER",
                "scenario=" + decision.Reason +
                " plan=" + plan.PlanId +
                " revision=" + plan.Revision +
                " task=" + d.GuardTask.TaskId +
                " target=" + EligibilityTrigger.DescribeTarget(d.GuardTask.FormationKey,
                                                            d.GuardTask.Revision, plan.Revision) +
                " phase=" + d.Phase +
                " note=real_guard_object_selected_by_revision" +
                " at=" + Cb2Log.F(now));

            P2ApplyLoss(a, now);
        }

        // 与 P1 的 ApplyLoss 同样的公开原生入口，但日志前缀独立，便于区分两路适配。
        private void P2ApplyLoss(Applied plan, double now)
        {
            var f = plan.Target;
            plan.LossApplied = true;

            string before = StateOf(f, now, plan);
            string error = "";
            bool became = false;
            try
            {
                f.SetControlledByAI(false);
                became = !f.IsAIControlled;
            }
            catch (Exception e) { error = e.GetType().Name + ": " + e.Message; }

            Cb2Log.Line("P2_PROBE_ELIGIBILITY_LOST",
                "scenario=" + plan.Mode +
                " plan=" + plan.PlanId +
                " task=" + plan.TaskId +
                " key=" + plan.TargetKey +
                " before[" + before + "]" +
                " after[" + StateOf(f, now, plan) + "]" +
                " applied=" + (became ? 1 : 0) +
                " error=" + (error.Length == 0 ? "<none>" : error) +
                " note=test_adapter_triggered_eligibility_change" +
                " at=" + Cb2Log.F(now));

            plan.RestoreAt = now + P2SubstituteHoldSeconds(plan);
            Cb2Log.Line("P2_PROBE_RESTORE_SCHEDULED",
                "plan=" + plan.PlanId +
                " key=" + plan.TargetKey +
                " at=" + Cb2Log.F(plan.RestoreAt) +
                " hold=" + Cb2Log.F(P2SubstituteHoldSeconds(plan)) +
                " at_now=" + Cb2Log.F(now));
        }

        // 场景 B 的替补应在本次失效处置证据采集完成前保持不可用，
        // 因此保持窗口明显长于保护编队；场景 B 的保护编队仍按合同下限 4 s，
        // 只有场景 A 需要把恢复推迟到重分配后的新身份链完成之后。
        private double P2SubstituteHoldSeconds(Applied plan)
        {
            if (plan.Mode == "p2_substitute") return P2SubstituteKeepSeconds;
            if (_p2Mode == P2AdaptationMode.GuardLossAfterEstablished) return P2GuardLossHoldSeconds;
            return HoldSeconds;
        }

        private void P2SweepRestores(Mission mission, double now)
        {
            foreach (var plan in _applied)
            {
                if (plan.Mode != "p2") continue;
                if (!plan.LossApplied || plan.Restored) continue;
                if (plan.RestoreAt < 0 || now < plan.RestoreAt) continue;
                P2ApplyRestore(plan, now);
            }
            foreach (var plan in _p2Substitutes)
            {
                if (!plan.LossApplied || plan.Restored) continue;
                if (plan.RestoreAt < 0 || now < plan.RestoreAt) continue;
                P2ApplyRestore(plan, now);
            }
        }

        // 只恢复本适配实际改过资格的对象，逐一记原状态。
        private void P2ApplyRestore(Applied plan, double now)
        {
            plan.Restored = true;
            var f = plan.Target;
            if (f == null) return;
            string before = StateOf(f, now, plan);
            string error = "";
            bool became = false;
            try
            {
                f.SetControlledByAI(true);
                became = f.IsAIControlled;
            }
            catch (Exception e) { error = e.GetType().Name + ": " + e.Message; }

            Cb2Log.Line("P2_PROBE_ELIGIBILITY_RESTORED",
                "scenario=" + plan.Mode +
                " plan=" + plan.PlanId +
                " task=" + plan.TaskId +
                " key=" + plan.TargetKey +
                " before[" + before + "]" +
                " after[" + StateOf(f, now, plan) + "]" +
                " applied=" + (became ? 1 : 0) +
                " error=" + (error.Length == 0 ? "<none>" : error) +
                " at=" + Cb2Log.F(now) +
                " note=only_objects_this_adapter_changed");
        }

        // 触发前提（脚本 §7.1）：计划连续存活 ≥ 8 s，且两个编队的普通任务都已取得正面反馈。
        // 前提不满足就如实记录未触发，不为凑数提前让计划失效。
        private void TryTrigger(Mission mission, double now)
        {
            var plan = TestDriver.Instance.Plan;
            if (plan == null || plan.IsClosed) return;

            double age = now - plan.CreatedAt;
            if (age < TestParameters.PlanMinStableSeconds) return;

            var tasks = TestDriver.Instance.Tasks.OpenTasks();
            int effective = 0;
            int normal = 0;
            FormationTask chosen = null;
            foreach (var t in tasks)
            {
                if (BattlePlan.IsExitTask(t)) continue;
                if (t.WrittenAt < 0.0) continue;
                normal++;
                if (t.Feedback != TaskFeedbackState.Effective) continue;
                effective++;
                if (chosen == null) chosen = t;
            }

            // 需要"两个编队的普通任务都已取得正面反馈"：脚本要求先有稳定与生效证据再触发资格失效。
            if (normal < 2 || effective < 2 || chosen == null) return;

            var f = TestDriver.Instance.FindFormationByKey(mission, chosen.FormationKey);
            if (f == null) return;

            _fired = true;
            var mode = _modes[0];
            var a = new Applied
            {
                Mode = mode,
                Target = f,
                PlanId = chosen.PlanId,
                TaskId = chosen.TaskId,
                TargetKey = chosen.FormationKey,
            };
            _applied.Add(a);

            if (mode == "c") ApplyTeamJoin(mission, a, now);
            else ApplyLoss(a, now, mode);
        }

        private void ApplyLoss(Applied plan, double now, string mode)
        {
            var f = plan.Target;
            plan.TargetKey = KeyOf(f);
            plan.LossApplied = true;

            string before = StateOf(f, now, plan);
            string error = "";
            bool became = false;
            try
            {
                f.SetControlledByAI(false);
                became = !f.IsAIControlled;
            }
            catch (Exception e) { error = e.GetType().Name + ": " + e.Message; }

            Cb2Log.Line("PROBE_ELIGIBILITY_LOST",
                "scenario=" + mode +
                " plan=" + plan.PlanId +
                " task=" + plan.TaskId +
                " key=" + plan.TargetKey +
                " before[" + before + "]" +
                " after[" + StateOf(f, now, plan) + "]" +
                " applied=" + (became ? 1 : 0) +
                " error=" + (error.Length == 0 ? "<none>" : error) +
                " note=test-adapter_triggered_eligibility_change" +
                " at=" + Cb2Log.F(now));

            ScheduleRestore(plan, now);
        }

        // mode c：调用原生公开入口 Mission.JoinEnemyTeam()，把 PlayerTeam 换成绑目标所在队。
        // 这是真实原生控制链（UI 用的同一入口），不是伪造 PlayerOwner、也不是 F1/F6 按键。
        private void ApplyTeamJoin(Mission mission, Applied plan, double now)
        {
            var f = plan.Target;
            plan.TargetKey = KeyOf(f);
            plan.LossApplied = true;

            Team before0 = null;
            Team target0 = null;
            string error = "";
            bool flipped = false;
            string pre = "";
            try
            {
                before0 = mission.PlayerTeam;
                target0 = f.Team;
                if (before0 != null && target0 != null && before0 == target0)
                {
                    // 方向不对：JoinEnemyTeam 会把 PlayerTeam 换到另一队，反而远离绑目标。
                    Cb2Log.Line("PROBE_TEAM_JOIN_SKIPPED",
                        "plan=" + plan.PlanId + " mode=c key=" + plan.TargetKey +
                        " reason=bound_team_is_already_player_team" +
                        " state[" + StateOf(f, now, plan) + "]" +
                        " at=" + Cb2Log.F(now));
                    return;
                }
                pre = MissionIds(before0, target0) + " " + TeamChain(mission);
                plan.OriginalPlayerTeam = before0;
                mission.JoinEnemyTeam();
                flipped = mission.PlayerTeam != before0;
            }
            catch (Exception e) { error = e.GetType().Name + ": " + e.Message; }

            string afterIds = "";
            try { afterIds = MissionIds(mission.PlayerTeam, f.Team); } catch (Exception) { }

            Cb2Log.Line("PROBE_TEAM_JOIN",
                "plan=" + plan.PlanId +
                " mode=c" +
                " key=" + plan.TargetKey +
                " applied=" + (flipped ? 1 : 0) +
                " before[" + pre + "]" +
                " after[" + afterIds + "]" +
                " target_now_player_team=" + (f.Team != null && f.Team.IsPlayerTeam ? 1 : 0) +
                " after[" + StateOf(f, now, plan) + "]" +
                " error=" + (error.Length == 0 ? "<none>" : error) +
                " note=test-adapter_triggered_playerteam_change" +
                " at=" + Cb2Log.F(now));

            ScheduleRestore(plan, now);
        }

        private void ScheduleRestore(Applied plan, double now)
        {
            plan.RestoreAt = now + HoldSeconds;
            Cb2Log.Line("PROBE_RESTORE_SCHEDULED",
                "plan=" + plan.PlanId +
                " at=" + Cb2Log.F(plan.RestoreAt) +
                " hold=" + Cb2Log.F(HoldSeconds) +
                " at_now=" + Cb2Log.F(now));
        }

        // 记录原生控制链的可达性前置：Attacker/Defender Leader 与 MainAgent 是否存在。
        // JoinEnemyTeam 在这两个 Leader 为空时不会翻转 PlayerTeam，这条记录用于区分"不可达"与"没走到"。
        private static string TeamChain(Mission mission)
        {
            if (mission == null) return "chain=<no_mission>";
            int a = 0, d = 0, m = 0, ma = 0;
            try { a = (mission.AttackerTeam != null && mission.AttackerTeam.Leader != null) ? 1 : 0; } catch (Exception) { }
            try { d = (mission.DefenderTeam != null && mission.DefenderTeam.Leader != null) ? 1 : 0; } catch (Exception) { }
            try { m = Agent.Main != null ? 1 : 0; } catch (Exception) { }
            try { ma = mission.MainAgent != null ? 1 : 0; } catch (Exception) { }
            return "attacker_leader=" + a + " defender_leader=" + d + " agent_main=" + m + " main_agent=" + ma;
        }

        private static string MissionIds(Team playerTeam, Team targetTeam)
        {
            string p = playerTeam == null ? "<none>" : playerTeam.TeamIndex.ToString(CultureInfo.InvariantCulture);
            string t = targetTeam == null ? "<none>" : targetTeam.TeamIndex.ToString(CultureInfo.InvariantCulture);
            return "player_team=" + p + " bound_team=" + t;
        }

        private void SweepRestores(Mission mission, double now)
        {
            foreach (var plan in _applied)
            {
                if (!plan.LossApplied || plan.Restored) continue;
                if (plan.RestoreAt < 0 || now < plan.RestoreAt) continue;
                ApplyRestore(plan, now);
            }
        }

        private void ApplyRestore(Applied plan, double now)
        {
            plan.Restored = true;
            var f = plan.Target;
            if (f == null) return;
            string before = StateOf(f, now, plan);
            string error = "";
            bool became = false;
            try
            {
                if (plan.Mode == "c")
                {
                    // 只撤回本适配自己的环境改动：把 PlayerTeam 换回原队。
                    var mission = Mission.Current;
                    if (mission != null && plan.OriginalPlayerTeam != null)
                    {
                        mission.PlayerTeam = plan.OriginalPlayerTeam;
                        became = mission.PlayerTeam == plan.OriginalPlayerTeam;
                    }
                }
                else
                {
                    f.SetControlledByAI(true);
                    became = f.IsAIControlled;
                }
            }
            catch (Exception e) { error = e.GetType().Name + ": " + e.Message; }

            var currentPlan = TestDriver.Instance.Plan;
            Cb2Log.Line("PROBE_ELIGIBILITY_RESTORED",
                "source_plan=" + plan.PlanId +
                " source_task=" + plan.TaskId +
                " mode=" + plan.Mode +
                " key=" + plan.TargetKey +
                " before[" + before + "]" +
                " after[" + StateOf(f, now, plan) + "]" +
                " applied=" + (became ? 1 : 0) +
                " error=" + (error.Length == 0 ? "<none>" : error) +
                " at=" + Cb2Log.F(now) +
                " plan_open_now=" + (currentPlan != null && !currentPlan.IsClosed ? currentPlan.PlanId : "<none>") +
                " same_plan_open=" + (currentPlan != null && !currentPlan.IsClosed &&
                    string.Equals(currentPlan.PlanId, plan.PlanId, StringComparison.Ordinal) ? 1 : 0) +
                " write_attempts_total=" + TestDriver.Instance.Gate.WriteAttempts +
                " write_attempts_source_task=" + TestDriver.Instance.Gate.WriteAttemptsFor(plan.TaskId) +
                " note=" + (plan.Mode == "c"
                    ? "any_state_change_here_is_native_PlayerTeam_change_side_effect"
                    : "any_order_change_here_is_native_SetControlledByAI_side_effect"));
        }

        private static string KeyOf(Formation f)
        {
            if (f == null) return "";
            try
            {
                return f.Team.TeamIndex.ToString(CultureInfo.InvariantCulture) + "/" +
                       FormationSampler.FormationKeyOf(f);
            }
            catch (Exception) { return ""; }
        }

        private string StateOf(Formation f, double now, Applied plan)
        {
            if (f == null) return "<none>";
            plan.TargetKey = KeyOf(f);
            return "obj=" + FormationSampler.ObjectIdentityOf(f) +
                " ai_ctrl=" + (f.IsAIControlled ? 1 : 0) +
                " player_owner=" + (f.PlayerOwner != null ? 1 : 0) +
                " player_team=" + (f.Team != null && f.Team.IsPlayerTeam ? 1 : 0) +
                " units=" + f.CountOfUnits.ToString(CultureInfo.InvariantCulture) +
                " order=" + FormationSampler.OrderNameOf(f) +
                " active_behavior=" + FormationSampler.ActiveBehaviorNameOf(f) +
                " behavior_count=" + FormationSampler.BehaviorCountOf(f) +
                " at=" + Cb2Log.F(now);
        }
    }
}
