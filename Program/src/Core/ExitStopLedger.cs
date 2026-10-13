using System;
using System.Collections.Generic;

namespace CombatBrain2.Core
{
    // 退出尝试的登记与归属判定（派单 §6）。
    // 规则：每个对象每次实际关闭最多一次退出尝试；
    //       新分配接管同一对象后，旧退出任务不得再处理该对象的状态。
    //
    // 身份：登记以「对象 + 被关闭的普通任务」为键（一次关闭只登记一次）；
    //       归属以「对象 -> 当前接管任务」记录。退出任务携带 OriginTaskId，
    //       因此「这条退出任务是否仍然是该对象当前那次关闭的代表」可以判定，
    //       而不需要调用方自己对齐两套身份。
    internal sealed class ExitStopLedger
    {
        // 登记键：对象 + 被关闭任务。同一对象后来的新任务产生新键，因此允许新的退出尝试。
        private readonly HashSet<string> _registered = new HashSet<string>(StringComparer.Ordinal);

        // 对象当前归属：对象 -> 接管它的任务 ID。
        private readonly Dictionary<string, string> _owner =
            new Dictionary<string, string>(StringComparer.Ordinal);

        internal int RegisteredCount { get { return _registered.Count; } }

        internal static string KeyOf(string objectIdentity, string closedTaskId)
        {
            return (objectIdentity ?? "") + "|" + (closedTaskId ?? "");
        }

        // 登记一次退出尝试；已登记过该「对象 + 关闭任务」则拒绝。
        internal bool TryRegister(string objectIdentity, string closedTaskId)
        {
            if (string.IsNullOrEmpty(objectIdentity)) return false;
            return _registered.Add(KeyOf(objectIdentity, closedTaskId));
        }

        internal bool IsRegistered(string objectIdentity, string closedTaskId)
        {
            if (string.IsNullOrEmpty(objectIdentity)) return false;
            return _registered.Contains(KeyOf(objectIdentity, closedTaskId));
        }

        // 普通分配公布时接管对象。H3：必须在分配成立时调用，
        // 不能等首次写令，否则旧退出在新任务公布与写令之间仍可作用于该对象。
        internal void TakeOwnership(string objectIdentity, string taskId)
        {
            if (string.IsNullOrEmpty(objectIdentity)) return;
            _owner[objectIdentity] = taskId ?? "";
        }

        // 归属判定：该对象当前是否仍由该任务接管。
        // 未登记归属时返回 true——不把"未知"当成"被接管"，
        // 否则任何未登记对象上的正当退出都会被误挡。
        internal bool StillOwnedBy(string objectIdentity, string taskId)
        {
            if (string.IsNullOrEmpty(objectIdentity)) return false;
            string current;
            if (!_owner.TryGetValue(objectIdentity, out current)) return true;
            return string.Equals(current, taskId ?? "", StringComparison.Ordinal);
        }

        // 迟到的旧关闭回调：只有在没有更新归属时才允许登记。
        // 新分配（普通任务或更新的退出任务）已经接管时返回 false，调用方不得入队。
        internal bool TryRegisterIfNotTakenOver(string objectIdentity, string closedTaskId)
        {
            if (string.IsNullOrEmpty(objectIdentity)) return false;
            if (!StillOwnedBy(objectIdentity, closedTaskId)) return false;
            return TryRegister(objectIdentity, closedTaskId);
        }

        // 退出任务能否处理该对象：该对象的最近一次关闭就是它对应的那次关闭
        // （按 OriginTaskId + 对象登记成立），且对象未被更新分配接管。
        // 两个条件缺一不可：只登记不判归属会让迟到旧退出夺回归属；
        // 只判归属不查登记会让从未登记的退出任务获得处理权。
        internal bool MayActOn(string objectIdentity, string originTaskId, string ownerTaskId)
        {
            if (string.IsNullOrEmpty(objectIdentity)) return false;
            if (!IsRegistered(objectIdentity, originTaskId)) return false;
            return StillOwnedBy(objectIdentity, ownerTaskId);
        }

        internal string OwnerOf(string objectIdentity)
        {
            if (string.IsNullOrEmpty(objectIdentity)) return "";
            string current;
            return _owner.TryGetValue(objectIdentity, out current) ? current : "";
        }

        internal void ClearMission()
        {
            _registered.Clear();
            _owner.Clear();
        }
    }
}
