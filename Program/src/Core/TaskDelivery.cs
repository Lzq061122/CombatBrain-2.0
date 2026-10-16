using System;

namespace CombatBrain2.Core
{
    // 一条待执行任务此刻的投递事实。全部由调用方从现有台账读取，不在这里反查引擎。
    internal sealed class DeliveryFacts
    {
        internal bool HasPlan;
        internal bool ExitTask;
        internal bool Written;
        internal bool Selected;
        internal bool Submitted;
        internal double Now;
        internal double AssignedAt = -1.0;
        internal double PlanCreatedAt = -1.0;
        internal bool PredecessorPending;
        internal double LastWriteAt = -1.0;
    }

    // 待执行任务的投递判定。评价节奏与投递节奏是两件事：计划仍按既定节奏评价，
    // 但已经满足执行门的任务应在首个可用主线程 tick 投递候选，不能再等下一个评价点。
    // 这里只做纯判定，不 arm 行为、不写引擎、不改任务状态。
    internal static class TaskDelivery
    {
        internal enum Verdict
        {
            Queue = 0,
            AlreadyWritten,
            AlreadySelected,
            AlreadySubmitted,
            Deferred,
        }

        // deferReason 的取值与 WRITE_DELAYED 既有文案同源：判定与日志共用一套取值，
        // 避免两处各写一份三条件而实际口径不同。
        internal static Verdict Decide(DeliveryFacts f, out string deferReason)
        {
            deferReason = "";
            if (f == null) return Verdict.Deferred;
            if (f.Written) return Verdict.AlreadyWritten;
            if (f.Selected) return Verdict.AlreadySelected;
            // 已投递即不再重复投递：逐 tick 调度只负责把尚未投递的任务尽早挂上行为，
            // 重复 arm 会反复给原生选择施压并堆积提交计数。
            if (f.Submitted) return Verdict.AlreadySubmitted;

            // 退出动作不受首令延迟与发令间隔约束，它只受自己的安全时限约束。
            if (f.ExitTask) return Verdict.Queue;

            deferReason = TimingDeferral(f);
            return deferReason.Length == 0 ? Verdict.Queue : Verdict.Deferred;
        }

        // 三条落地时序门：新分配首令延迟、既定发令顺序、相邻普通写令间隔。
        // 独立成一个入口，是因为获选后的执行路径也要在写令前复核同一组门，
        // 而那条路径上的任务早已投递过，不能按「已投递」提前放行。
        internal static string TimingDeferral(DeliveryFacts f)
        {
            if (f == null) return "";
            // 退出动作属于已关闭的旧计划，必然 HasPlan=false，且不受首令延迟与发令间隔约束：
            // 它只受自己的安全时限约束，身份门与时序门都不得拦下它。
            if (f.ExitTask) return "";
            // 普通任务必须属于当前开放计划：已关闭、被替换或无当前计划的任务
            // 不得因取数结果 HasPlan=false 反而绕过时序/身份门直接排队（派单 §3.1）。
            if (!f.HasPlan) return "not_in_current_plan";

            // 重分配的普通任务从新分配时刻重新计时（派单 §7 L155）：
            // 沿用计划创建时刻会让替补刚上任就立刻可发令，跳过应有的 1.5 s。
            double sinceAssign = f.AssignedAt >= 0.0 ? f.Now - f.AssignedAt : f.Now - f.PlanCreatedAt;
            if (sinceAssign < TestParameters.FirstCommandDelaySeconds)
            {
                return "first_command_delay";
            }

            // 发令顺序遵守既定 Ordinal：前序普通任务尚未实际写令时，后序必须等待。
            if (f.PredecessorPending)
            {
                return "predecessor_not_written";
            }

            if (f.LastWriteAt >= 0.0 && f.Now - f.LastWriteAt < TestParameters.FormationStaggerSeconds)
            {
                return "formation_stagger";
            }

            return "";
        }
    }
}
