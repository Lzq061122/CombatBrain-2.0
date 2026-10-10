using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CombatBrain2.Core
{
    // 一个编队的采样记录。SampledAt 是实际采样时刻（Mission 时间），
    // 任何年龄判定都用它，不允许用刷新时刻冒充。
    internal sealed class FormationRecord
    {
        internal string MissionKey = "";
        internal double SampledAt;
        internal int TeamIndex;
        internal string Side = "";
        internal bool IsPlayerTeam;
        internal int FormationIndex;
        internal string FormationClass = "";
        internal int UnitCount;
        internal double PosX;
        internal double PosY;
        internal string MovementOrder = "";
        internal bool IsAiControlled;
        internal bool IsPlayerControlled;

        // 对象身份：不能只靠可复用的 Team/Formation 索引。
        // 宿主侧在采样时填入运行期对象标识，用于确认"还是同一个对象"。
        internal string ObjectIdentity = "";

        internal double AgeAt(double missionTime)
        {
            return missionTime - SampledAt;
        }

        internal bool IsStale(double missionTime)
        {
            return AgeAt(missionTime) > TestParameters.SnapshotMaxAgeSeconds;
        }

        internal string Key()
        {
            return TeamIndex.ToString(CultureInfo.InvariantCulture) + "/" +
                   FormationIndex.ToString(CultureInfo.InvariantCulture);
        }
    }

    internal sealed class FormationSnapshot
    {
        internal string MissionKey = "";
        internal double SampledAt;
        internal readonly List<FormationRecord> Records = new List<FormationRecord>();

        internal int CountNonEmpty()
        {
            return Records.Count;
        }

        internal bool HasDuplicateKeys(out string detail)
        {
            var seen = new HashSet<string>();
            foreach (var r in Records)
            {
                if (!seen.Add(r.Key()))
                {
                    detail = r.Key();
                    return true;
                }
            }
            detail = "";
            return false;
        }

        internal FormationRecord FindByKey(string key)
        {
            foreach (var r in Records)
            {
                if (r.Key() == key) return r;
            }
            return null;
        }

        internal string Describe()
        {
            var sb = new StringBuilder();
            sb.Append("snapshot mission=").Append(MissionKey)
              .Append(" sampled_at=").Append(SampledAt.ToString("F3", CultureInfo.InvariantCulture))
              .Append(" non_empty=").Append(Records.Count);
            foreach (var r in Records)
            {
                sb.Append("\n  ").Append(r.Key())
                  .Append(" side=").Append(r.Side)
                  .Append(" player_team=").Append(r.IsPlayerTeam ? 1 : 0)
                  .Append(" class=").Append(r.FormationClass)
                  .Append(" units=").Append(r.UnitCount)
                  .Append(" pos=").Append(r.PosX.ToString("F2", CultureInfo.InvariantCulture))
                  .Append(",").Append(r.PosY.ToString("F2", CultureInfo.InvariantCulture))
                  .Append(" order=").Append(r.MovementOrder)
                  .Append(" ai=").Append(r.IsAiControlled ? 1 : 0)
                  .Append(" obj=").Append(r.ObjectIdentity);
            }
            return sb.ToString();
        }
    }
}
