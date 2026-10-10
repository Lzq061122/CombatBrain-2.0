using System;
using System.Globalization;
using CombatBrain2.Core;
using TaleWorlds.MountAndBlade;

namespace CombatBrain2.Host
{
    // 编队采样：只读。不修改任何 Agent 行为。
    internal static class FormationSampler
    {
        internal static FormationSnapshot Sample(Mission mission, double missionTime)
        {
            var snapshot = new FormationSnapshot
            {
                MissionKey = MissionKeyOf(mission),
                SampledAt = missionTime,
            };
            if (mission == null) return snapshot;

            try
            {
                foreach (var team in mission.Teams)
                {
                    if (team == null) continue;
                    foreach (var f in team.FormationsIncludingEmpty)
                    {
                        if (f == null) continue;
                        int units = 0;
                        try { units = f.CountOfUnits; } catch (Exception) { }
                        if (units == 0) continue;

                        var rec = new FormationRecord
                        {
                            MissionKey = snapshot.MissionKey,
                            SampledAt = missionTime,
                            TeamIndex = SafeInt(() => team.TeamIndex),
                            Side = SafeString(() => team.Side.ToString()),
                            IsPlayerTeam = SafeBool(() => team.IsPlayerTeam),
                            FormationIndex = (int)f.FormationIndex,
                            FormationClass = RepresentativeClassOf(f),
                            UnitCount = units,
                            PosX = SafeDouble(() => (double)f.CurrentPosition.x),
                            PosY = SafeDouble(() => (double)f.CurrentPosition.y),
                            MovementOrder = OrderNameOf(f),
                            IsAiControlled = SafeBool(() => f.IsAIControlled),
                            IsPlayerControlled = SafeBool(() => f.PlayerOwner != null),
                            // 运行期对象标识：TeamIndex/FormationIndex 是可复用索引，
                            // 这里再叠加对象自身的引用标识，用于确认"还是同一个对象"。
                            ObjectIdentity = ObjectIdentityOf(f),
                        };
                        snapshot.Records.Add(rec);
                    }
                }
            }
            catch (Exception) { }
            return snapshot;
        }

        internal static string FormationClassOf(Formation f)
        {
            try { return f.FormationIndex.ToString(); } catch (Exception) { return "<unknown>"; }
        }

        // 兵种类别：取实际代表兵种，用于"步兵"资格判定与日志。
        internal static string RepresentativeClassOf(Formation f)
        {
            try { return f.RepresentativeClass.ToString(); } catch (Exception) { return "<unknown>"; }
        }

        // 槽位键：与 Core 的 FormationRecord.Key() 同构（TeamIndex/FormationIndex 数值）。
        // 编队类名只用于日志可读性，不参与键匹配。
        internal static string FormationKeyOf(Formation f)
        {
            try { return ((int)f.FormationIndex).ToString(CultureInfo.InvariantCulture); }
            catch (Exception) { return "<unknown>"; }
        }

        // 实际生效的移动命令类型，取自 OrderController 的静态当前值查询。
        internal static string OrderNameOf(Formation f)
        {
            try
            {
                return OrderController.GetActiveMovementOrderOf(f).ToString();
            }
            catch (Exception e) { return "<err:" + e.GetType().Name + ">"; }
        }

        internal static string MissionKeyOf(Mission mission)
        {
            if (mission == null) return "";
            return SafeString(() => mission.SceneName) + "#" +
                   RuntimeHelpers.GetHashCode(mission).ToString(CultureInfo.InvariantCulture);
        }

        // 原生当前活跃行为名。本方是否获选必须靠它判定，不能靠"有命令写到同一编队"。
        internal static string ActiveBehaviorNameOf(Formation f)
        {
            if (f == null) return "<no_formation>";
            return SafeString(() =>
            {
                var ai = f.AI;
                if (ai == null) return "<no_ai>";
                var b = ai.ActiveBehavior;
                return b == null ? "<none>" : b.GetType().Name;
            });
        }

        internal static int BehaviorCountOf(Formation f)
        {
            if (f == null) return -1;
            return SafeInt(() =>
            {
                var ai = f.AI;
                return ai == null ? -1 : ai.BehaviorCount;
            });
        }

        internal static string ObjectIdentityOf(Formation f)
        {
            // 以运行期对象引用哈希作为身份的一部分，避免只靠可复用索引。
            return RuntimeHelpers.GetHashCode(f).ToString("x8", CultureInfo.InvariantCulture) +
                   "@" + FormationClassOf(f);
        }

        internal static Formation FindFormation(Mission mission, int teamIndex, string formationSlot)
        {
            if (mission == null) return null;
            try
            {
                foreach (var team in mission.Teams)
                {
                    if (team == null || team.TeamIndex != teamIndex) continue;
                    foreach (var f in team.FormationsIncludingEmpty)
                    {
                        if (f == null) continue;
                        if (string.Equals(FormationKeyOf(f), formationSlot, StringComparison.Ordinal)) return f;
                    }
                }
            }
            catch (Exception) { }
            return null;
        }

        private static string SafeString(Func<string> f)
        {
            try { return f() ?? "<null>"; } catch (Exception e) { return "<err:" + e.GetType().Name + ">"; }
        }

        private static bool SafeBool(Func<bool> f)
        {
            try { return f(); } catch (Exception) { return false; }
        }

        private static int SafeInt(Func<int> f)
        {
            try { return f(); } catch (Exception) { return -1; }
        }

        private static double SafeDouble(Func<double> f)
        {
            try { return f(); } catch (Exception) { return double.NaN; }
        }
    }

    internal static class RuntimeHelpers
    {
        internal static int GetHashCode(object o)
        {
            return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o);
        }
    }
}
