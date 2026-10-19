using System;

namespace CombatBrain2.Core
{
    // 退出任务的退出后读数判定（只读取证）。
    //
    // 退出任务写完一次即关闭，且从不进入 BattlePlan.Tasks，因此它的读数不能挂在
    // 当前计划上：计划切换或 Mission 结束时那一路会一起消失。判定只看三件事，
    // 全部来自退出任务自身与对象当前的归属，不依赖当前计划是否还是产生它的那个。
    //
    // 三类事件必须分开，互相不能冒充：
    //   stopped        —— 对象仍是这次退出的同一对象、归属仍是该退出任务、订单已静止
    //   superseded     —— 对象已被后继分配接管；此时订单来自后继，不能算本次退出成功
    //   object_replaced—— 编队对象已不是关闭时那个；此刻的订单与该退出无关
    internal static class ExitStopObservation
    {
        internal const string Stopped = "stopped";
        internal const string NotStopped = "not_stopped";
        internal const string Superseded = "superseded";
        internal const string ObjectReplaced = "object_replaced";
        internal const string TargetGone = "target_gone";

        // 静止订单：编队已停。与 check-p3-feedback.ps1 对"真实停住"的读法一致——
        // 任何不是推进类的订单都说明旧攻势没有在继续。
        internal static bool IsStandingOrder(string movementOrder)
        {
            return string.Equals(movementOrder, "StandYourGround", StringComparison.Ordinal)
                || string.Equals(movementOrder, "Hold", StringComparison.Ordinal);
        }

        internal static string Judge(string exitTaskId, string exitObjectIdentity,
            string ownerTaskId, bool found, string observedObjectIdentity, string movementOrder)
        {
            if (!found) return TargetGone;
            if (!string.Equals(observedObjectIdentity ?? "", exitObjectIdentity ?? "", StringComparison.Ordinal))
                return ObjectReplaced;
            if (!string.Equals(ownerTaskId ?? "", exitTaskId ?? "", StringComparison.Ordinal))
                return Superseded;
            return IsStandingOrder(movementOrder) ? Stopped : NotStopped;
        }

        // 只有 stopped 才算这次退出产生了"已停"的读数。
        // superseded 与 object_replaced 都意味着订单已不代表本次退出，既不能记成功也不能记失败。
        internal static bool CountsAsStopEvidence(string verdict)
        {
            return string.Equals(verdict, Stopped, StringComparison.Ordinal);
        }
    }
}
