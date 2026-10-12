using System;
using System.Collections.Generic;

namespace CombatBrain2.Core
{
    internal sealed class RevocationPlan
    {
        internal bool Needed;
        internal string Key = "";
        internal string ObjectIdentity = "";
        internal string PreviousOrder = "";
        internal string OurOrder = "";
        internal string SkipReason = "";
    }

    internal sealed class WriteRecord
    {
        internal string TaskId = "";
        internal string Key = "";
        internal string ObjectIdentity = "";
        internal string PreviousOrder = "";
        internal string OurOrder = "";
        internal double WrittenAt;
    }

    // 写入账本：只记录本方确实写过的状态，用于退出时判断该撤销什么。
    // 不记录原生自身的写入，因此不会覆盖原生期间的新决策。
    internal sealed class WriteLedger
    {
        private readonly Dictionary<string, WriteRecord> _records = new Dictionary<string, WriteRecord>();

        internal void RecordOwnWrite(
            string taskId, string key, string objectIdentity,
            string previousOrder, string ourOrder, double missionTime)
        {
            _records[taskId] = new WriteRecord
            {
                TaskId = taskId,
                Key = key,
                ObjectIdentity = objectIdentity,
                PreviousOrder = previousOrder,
                OurOrder = ourOrder,
                WrittenAt = missionTime,
            };
        }

        internal RevocationPlan PlanRevocation(string taskId)
        {
            var plan = new RevocationPlan();
            WriteRecord r;
            if (!_records.TryGetValue(taskId ?? "", out r))
            {
                plan.SkipReason = "no_own_write";
                return plan;
            }
            // 同值写入没有改变状态，无需撤销。
            if (string.Equals(r.PreviousOrder, r.OurOrder, StringComparison.Ordinal))
            {
                plan.SkipReason = "same_value_no_change";
                return plan;
            }
            // 前值只以名称保存。带目标位置/目标的命令（Move、ChargeWithTarget、
            // FollowEntity 等）无法只凭名称忠实重建，宁可不写也不写出错误命令。
            if (!IsRestorableOrderName(r.PreviousOrder))
            {
                plan.SkipReason = "previous_order_not_restorable";
                return plan;
            }
            plan.Needed = true;
            plan.Key = r.Key;
            plan.ObjectIdentity = r.ObjectIdentity;
            plan.PreviousOrder = r.PreviousOrder;
            plan.OurOrder = r.OurOrder;
            return plan;
        }

        // 目标对象身份变了，说明当前对象不是本方当时改变的那一个，
        // 不得用旧的进入时状态覆盖它。
        internal RevocationPlan PlanRevocationIfSameObject(string taskId, string currentObjectIdentity)
        {
            var plan = PlanRevocation(taskId);
            if (!plan.Needed) return plan;
            if (!string.Equals(plan.ObjectIdentity, currentObjectIdentity, StringComparison.Ordinal))
            {
                plan.Needed = false;
                plan.SkipReason = "object_identity_changed";
            }
            return plan;
        }

        // 只有不带参数、可由名称唯一确定的命令才允许重建。
        // 这份清单必须与 Host 侧 OrderFromName 显式识别的名称保持一致：
        // 落到它 default 分支的名称不算可重建。
        internal static bool IsRestorableOrderName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            switch (name)
            {
                case "Advance":
                case "FallBack":
                case "Charge":
                case "StandYourGround":
                    return true;
                default:
                    return false;
            }
        }

        internal bool HasRecord(string taskId)
        {
            return _records.ContainsKey(taskId ?? "");
        }

        internal int RecordCount { get { return _records.Count; } }

        internal void ClearMission()
        {
            _records.Clear();
        }
    }
}
