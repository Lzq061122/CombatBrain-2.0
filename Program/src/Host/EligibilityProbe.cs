using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CombatBrain2.Core;
using TaleWorlds.MountAndBlade;

namespace CombatBrain2.Host
{
    // 测试适配（默认关闭）：用公开原生接口 Formation.SetControlledByAI 改变一个真实编队的 AI 控制资格，
    // 给"控制资格变化"这条边界提供实机触发与观测。
    // 不伪造玩家事件、不写私有字段、不绕过生产过滤与执行门、不自行写 MovementOrder。
    // 触发方式按意图序号配置：intents=A,B 表示 intent-1 走场景 A、intent-2 走场景 B。
    internal sealed class EligibilityProbe
    {
        internal static EligibilityProbe Instance = new EligibilityProbe();

        private const string ConfigFileName = "eligibility-probe.txt";

        private sealed class Plan
        {
            internal string Mode = "";
            internal bool LossApplied;
            internal bool Restored;
            internal double RestoreAt = -1.0;
            internal Formation Target;
            internal string TargetKey = "";
        }

        private readonly Dictionary<string, Plan> _plans = new Dictionary<string, Plan>();
        private readonly List<string> _modes = new List<string>();
        private bool _loaded;

        private EligibilityProbe() { }

        private static string ConfigPath()
        {
            return Path.Combine(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CombatBrain2"),
                ConfigFileName);
        }

        private void LoadOnce()
        {
            if (_loaded) return;
            _loaded = true;
            string rawLine = "";
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
                    if (string.Equals(k, "intents", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(k, "mode", StringComparison.OrdinalIgnoreCase))
                    {
                        rawLine = v;
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
                " note=test-adapter_triggered_eligibility_change");
        }

        private string ModeFor(string intentId)
        {
            // intent-N -> 第 N 个配置。
            int index = -1;
            int dash = intentId == null ? -1 : intentId.LastIndexOf('-');
            if (dash >= 0 && dash + 1 < intentId.Length)
            {
                int parsed;
                if (int.TryParse(intentId.Substring(dash + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                {
                    index = parsed - 1;
                }
            }
            if (index < 0 || index >= _modes.Count) return "";
            return _modes[index];
        }

        private Plan PlanFor(string intentId)
        {
            Plan p;
            if (!_plans.TryGetValue(intentId, out p))
            {
                p = new Plan();
                p.Mode = ModeFor(intentId);
                _plans[intentId] = p;
            }
            return p;
        }

        // 插入点：TestDriver.Tick 之后、DrainPendingCommand 之前。
        internal void OnMissionTick(Mission mission)
        {
            LoadOnce();
            if (_modes.Count == 0) return;
            if (mission == null) return;

            double now = TestDriver.Instance.SafeNow(mission);
            var intent = TestDriver.Instance.Intent;

            if (intent != null && !intent.IsClosed && intent.BoundKey.Length > 0)
            {
                var active = PlanFor(intent.IntentId);
                if (active.Mode.Length != 0 && !active.LossApplied)
                {
                    var planMode = active.Mode;
                    if (planMode == "a")
                    {
                        // 场景 A：已真实获选、命令已真实转交但尚未被主线程消费。
                        if (intent.SelectedAt >= 0 && intent.ExecutedAt < 0 && TestDriver.Instance.Handoff.HasPending)
                        {
                            ApplyLoss(mission, intent, active, now, "scenario_a");
                            return;
                        }
                    }
                    else if (planMode == "b")
                    {
                        // 场景 B：本方命令已经主线程执行并生效。
                        if (intent.ExecutedAt >= 0)
                        {
                            ApplyLoss(mission, intent, active, now, "scenario_b");
                            return;
                        }
                    }
                }
            }

            // 恢复：即使意图已经被取消也要执行。这只撤回本适配自己的环境改动，
            // 且这是观测"资格恢复后旧意图不复活"的前提。
            var keys = new List<string>(_plans.Keys);
            foreach (var id in keys)
            {
                var plan = _plans[id];
                if (!plan.LossApplied || plan.Restored) continue;
                if (plan.RestoreAt < 0 || now < plan.RestoreAt) continue;
                ApplyRestore(intent == null ? id : intent.IntentId, plan, now, intent);
            }
        }

        private void ApplyLoss(Mission mission, TestIntent intent, Plan plan, double now, string scenario)
        {
            var f = TestDriver.Instance.FindFormationByKey(mission, intent.BoundKey);
            if (f == null) return;

            plan.Target = f;
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
                "scenario=" + scenario +
                " intent=" + intent.IntentId +
                " mode=" + plan.Mode +
                " key=" + plan.TargetKey +
                " before[" + before + "]" +
                " after[" + StateOf(f, now, plan) + "]" +
                " applied=" + (became ? 1 : 0) +
                " error=" + (error.Length == 0 ? "<none>" : error) +
                " at=" + Cb2Log.F(now));

            // 失资格保持窗口：至少 2 秒，留出独立采样机会；且必须早于旧意图截止。
            double hold = TestParameters.SnapshotMaxAgeSeconds + 2.0;
            plan.RestoreAt = now + hold;
            Cb2Log.Line("PROBE_RESTORE_SCHEDULED",
                "intent=" + intent.IntentId +
                " at=" + Cb2Log.F(plan.RestoreAt) +
                " intent_deadline=" + Cb2Log.F(intent.Deadline) +
                " before_deadline=" + (plan.RestoreAt < intent.Deadline ? 1 : 0));
        }

        private void ApplyRestore(string intentLabel, Plan plan, double now, TestIntent current)
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

            Cb2Log.Line("PROBE_ELIGIBILITY_RESTORED",
                "source_intent=" + intentLabel +
                " mode=" + plan.Mode +
                " key=" + plan.TargetKey +
                " before[" + before + "]" +
                " after[" + StateOf(f, now, plan) + "]" +
                " applied=" + (became ? 1 : 0) +
                " error=" + (error.Length == 0 ? "<none>" : error) +
                " at=" + Cb2Log.F(now) +
                " intent_open_now=" + (current != null && !current.IsClosed ? current.IntentId : "<none>") +
                " same_intent_open=" + (current != null && !current.IsClosed &&
                    string.Equals(current.IntentId, intentLabel, StringComparison.Ordinal) ? 1 : 0) +
                " write_attempts_total=" + TestDriver.Instance.Gate.WriteAttempts +
                " write_attempts_source_intent=" + TestDriver.Instance.Gate.WriteAttemptsFor(intentLabel) +
                " note=any_order_change_here_is_native_SetControlledByAI_side_effect");
        }

        private string StateOf(Formation f, double now, Plan plan)
        {
            if (f == null) return "<none>";
            plan.TargetKey = f.Team.TeamIndex.ToString(CultureInfo.InvariantCulture) + "/" +
                FormationSampler.FormationKeyOf(f);
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
