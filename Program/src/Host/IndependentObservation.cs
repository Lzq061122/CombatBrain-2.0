using System;
using System.Collections.Generic;
using CombatBrain2.Core;
using TaleWorlds.MountAndBlade;

namespace CombatBrain2.Host
{
    // 独立只读观测：与 2.0 自身快照走不同读取路径，用于跨来源对照。
    // 不参与决策，不写任何状态。
    internal static class IndependentObservation
    {
        internal sealed class Reading
        {
            internal string MissionKey = "";
            internal double At;
            internal readonly List<string> Lines = new List<string>();
        }

        internal static Reading Read()
        {
            var r = new Reading();
            var mission = Mission.Current;
            if (mission == null)
            {
                r.Lines.Add("no_mission");
                return r;
            }

            r.MissionKey = FormationSampler.MissionKeyOf(mission);
            r.At = Safe(() => (double)mission.CurrentTime);

            int total = 0;
            foreach (var team in mission.Teams)
            {
                if (team == null) continue;
                int teamUnits = 0;
                int teamFormations = 0;
                foreach (var f in team.FormationsIncludingEmpty)
                {
                    if (f == null) continue;
                    int units = 0;
                    try { units = f.CountOfUnits; } catch (Exception) { }
                    if (units == 0) continue;
                    teamFormations++;
                    teamUnits += units;
                }
                total += teamUnits;
                r.Lines.Add(
                    "team=" + team.TeamIndex +
                    " side=" + SafeSide(team) +
                    " formations=" + teamFormations +
                    " units=" + teamUnits);
            }
            r.Lines.Add("total_units=" + total);
            return r;
        }

        private static string SafeSide(Team team)
        {
            try { return team.Side.ToString(); } catch (Exception) { return "<unknown>"; }
        }

        private static double Safe(Func<double> f)
        {
            try { return f(); } catch (Exception) { return double.NaN; }
        }
    }
}
