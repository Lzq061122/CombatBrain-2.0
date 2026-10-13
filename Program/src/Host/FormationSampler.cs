using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
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
                        ReadActiveMembers(f, rec);
                        snapshot.Records.Add(rec);
                    }
                }
            }
            catch (Exception) { }
            return snapshot;
        }

        // 实际活跃成员质心：逐 Agent 等权平均，只统计自身活跃、隶属本编队、
        // 位置有限的成员。总人数与有效人数分列，两者不等时以有效人数为准做几何判定。
        // 读取失败或零有效成员时 HasActiveCenter 保持 false，调用方必须放弃依赖该值的计划。
        private static void ReadActiveMembers(Formation f, FormationRecord rec)
        {
            try
            {
                int active = 0;
                double sumX = 0.0;
                double sumY = 0.0;
                var indices = new List<int>();
                f.ApplyActionOnEachUnit(agent =>
                {
                    if (agent == null) return;
                    if (!agent.IsActive()) return;
                    if (!ReferenceEquals(agent.Formation, f)) return;
                    double x, y;
                    try
                    {
                        var p = agent.Position;
                        x = (double)p.x;
                        y = (double)p.y;
                    }
                    catch (Exception) { return; }
                    if (!BattleState.IsFinite(x) || !BattleState.IsFinite(y)) return;
                    active++;
                    sumX += x;
                    sumY += y;
                    indices.Add(agent.Index);
                }, null);

                rec.ActiveMemberCount = active;
                rec.MemberDigest = DigestOf(indices);
                if (active <= 0) return;
                rec.ActiveCenterX = sumX / active;
                rec.ActiveCenterY = sumY / active;
                rec.HasActiveCenter = true;
            }
            catch (Exception) { }
        }

        // 成员集合摘要：按 Agent.Index 升序拼接后取稳定哈希。
        // 只用于判断"有效成员集合是否变化"，不用于几何计算。
        private static string DigestOf(List<int> indices)
        {
            if (indices == null || indices.Count == 0) return "";
            indices.Sort();
            var sb = new StringBuilder();
            for (int i = 0; i < indices.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(indices[i].ToString(CultureInfo.InvariantCulture));
            }
            return sb.ToString();
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
