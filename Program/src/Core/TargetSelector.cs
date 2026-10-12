using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CombatBrain2.Core
{
    internal sealed class SelectionResult
    {
        internal FormationRecord Chosen;
        internal string SortBasis = "";
        internal readonly List<string> Candidates = new List<string>();
        internal readonly List<string> Rejections = new List<string>();

        internal bool HasChoice()
        {
            return Chosen != null;
        }
    }

    // 目标选择：非空、非玩家拥有、AI 受控、步兵。
    // 排序字段固定为 (TeamIndex, FormationIndex) 升序；候选与拒绝原因全部记录。
    internal static class TargetSelector
    {
        internal static SelectionResult Select(FormationSnapshot snapshot, double missionTime)
        {
            var result = new SelectionResult();
            if (snapshot == null)
            {
                result.Rejections.Add("no_snapshot");
                return result;
            }
            if (snapshot.Records.Count == 0)
            {
                result.Rejections.Add("snapshot_empty");
                return result;
            }

            var eligible = new List<FormationRecord>();
            foreach (var r in snapshot.Records)
            {
                result.Candidates.Add(Describe(r));

                if (r.UnitCount <= 0)
                {
                    result.Rejections.Add(r.Key() + ":empty");
                    continue;
                }
                if (r.IsPlayerTeam || r.IsPlayerControlled)
                {
                    result.Rejections.Add(r.Key() + ":player_owned");
                    continue;
                }
                if (!r.IsAiControlled)
                {
                    result.Rejections.Add(r.Key() + ":not_ai_controlled");
                    continue;
                }
                if (!IsInfantry(r.FormationClass))
                {
                    result.Rejections.Add(r.Key() + ":not_infantry:" + r.FormationClass);
                    continue;
                }
                if (r.IsStale(missionTime))
                {
                    result.Rejections.Add(r.Key() + ":snapshot_stale");
                    continue;
                }
                eligible.Add(r);
            }

            if (eligible.Count == 0)
            {
                result.Rejections.Add("no_eligible_candidate");
                return result;
            }

            eligible.Sort(CompareByStableOrder);
            result.Chosen = eligible[0];
            result.SortBasis = "prefer_non_stop_then_ascending(TeamIndex,FormationIndex)";
            return result;
        }

        // 优先非 Stop：只有从非 Stop 改成 Stop 才能证明本次改变了状态。
        // 全为 Stop 时回退到稳定顺序，此时同值调用只能证明执行回执。
        private static int CompareByStableOrder(FormationRecord a, FormationRecord b)
        {
            int sa = IsStopOrder(a.MovementOrder) ? 1 : 0;
            int sb = IsStopOrder(b.MovementOrder) ? 1 : 0;
            if (sa != sb) return sa.CompareTo(sb);
            if (a.TeamIndex != b.TeamIndex) return a.TeamIndex.CompareTo(b.TeamIndex);
            return a.FormationIndex.CompareTo(b.FormationIndex);
        }

        private static bool IsStopOrder(string order)
        {
            if (string.IsNullOrEmpty(order)) return false;
            return order.IndexOf("StandYourGround", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsInfantry(string formationClass)
        {
            if (string.IsNullOrEmpty(formationClass)) return false;
            return formationClass.IndexOf("Infantry", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string Describe(FormationRecord r)
        {
            return r.Key() + " team=" + r.TeamIndex.ToString(CultureInfo.InvariantCulture) +
                   " side=" + r.Side +
                   " class=" + r.FormationClass +
                   " units=" + r.UnitCount.ToString(CultureInfo.InvariantCulture) +
                   " ai=" + (r.IsAiControlled ? 1 : 0) +
                   " player_team=" + (r.IsPlayerTeam ? 1 : 0) +
                   " order=" + r.MovementOrder +
                   " obj=" + r.ObjectIdentity;
        }
    }
}
