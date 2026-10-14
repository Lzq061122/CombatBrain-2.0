using System;

namespace CombatBrain2.Core
{
    // P2 测试适配的触发判据（派单 §9.2）。纯逻辑：只读已经取好的现场值，
    // 不读引擎、不改生产状态，便于离线反例逐条钉住触发条件。
    //
    // 两个场景的相位不同，这是关键区别：
    //   场景 A（派单 line 208）：关系已建立、主力已推进、稳定窗口与竞争验证都已完成之后，
    //                            才让实际保护编队失资格，用于验证真实重分配。
    //   场景 B（派单 line 214）：关系尚未建立、主力仍 Hold 时让实际保护编队失资格，
    //                            用于验证"没有三支合格对象"时的明确不可执行分支。
    // 现有 P1 探针的"两项已生效 + OpenTasks 首个有效对象"既选错对象，也无法表达 B 的相位。
    internal enum P2AdaptationMode
    {
        None = 0,
        // 场景 A：关系建立之后让实际保护编队失去资格。
        GuardLossAfterEstablished,
        // 场景 B：关系尚未建立时让实际保护编队失去资格。
        GuardLossBeforeEstablished,
    }

    // 决策时点的现场读数。全部由调用方在已确认主线程上取好。
    internal sealed class EligibilityTriggerInput
    {
        internal P2AdaptationMode Mode = P2AdaptationMode.None;
        // 当前开放计划；null 或已关闭时不允许触发。
        internal bool PlanOpen;
        internal bool PlanIsCoordination;
        // 实际保护任务的版本必须与计划版本一致，否则视为尚未稳定，等下一 tick。
        internal bool GuardTaskPresent;
        internal int GuardTaskRevision = -1;
        internal int PlanRevision = -1;
        // 关系是否已建立（两侧就绪窗口均成立）。
        internal bool RelationshipEstablished;
        // 主力是否已经推进（推进任务已指定）。
        internal bool MainAdvancing;
        // 推进任务是否已实际写令、写后已稳定多久。派单 §9.2 要求场景 A 在真实动作
        // 及既定稳定窗口完成后才触发：只指定了任务、命令还没写出去不算。
        internal bool AdvanceCommandWritten;
        internal double AdvanceWrittenAgeSeconds = -1.0;
        // A5 竞争窗口（派单 §9.2 要求 ≥8 s、跨至少两个评价点）是否已完成。
        // 未开启注入时视为已完成。
        internal bool CompetitionWindowSatisfied = true;
        // 两个职责任务的命令是否都已写入：写入之前 20 s 窗口尚未开始，不属于 B 描述的相位。
        internal bool GuardCommandWritten;
        internal bool SupportCommandWritten;
        // 主力等待任务是否仍开放且未被替换。
        internal bool MainStillWaiting;
        // 计划已存活秒数与稳定门槛。
        internal double PlanAgeSeconds;
        internal double StableSecondsRequired;

        internal EligibilityTriggerInput()
        {
        }
    }

    internal sealed class EligibilityTriggerDecision
    {
        internal bool Fire;
        internal string Reason = "";
        internal string Detail = "";

        internal EligibilityTriggerDecision(bool fire, string reason, string detail)
        {
            Fire = fire;
            Reason = reason;
            Detail = detail;
        }
    }

    internal static class EligibilityTrigger
    {
        // 只判"是否触发"。返回不触发时给出具体原因，便于实机区分
        // "没走到"与"前提不满足"，不为凑数提前触发。
        internal static EligibilityTriggerDecision Decide(EligibilityTriggerInput input)
        {
            if (input == null) return No("no_input");
            if (input.Mode == P2AdaptationMode.None) return No("mode_off");
            if (!input.PlanOpen) return No("plan_not_open");
            if (!input.PlanIsCoordination) return No("not_coordination_plan");
            if (!input.GuardTaskPresent) return No("guard_task_absent");
            if (input.GuardTaskRevision != input.PlanRevision)
                return No("guard_revision_unsettled task_rev=" + input.GuardTaskRevision +
                          " plan_rev=" + input.PlanRevision);

            if (input.Mode == P2AdaptationMode.GuardLossBeforeEstablished)
            {
                // 场景 B：命令已写入（20 s 窗口确实开始）但关系尚未建立，且主力仍 Hold。
                if (!input.GuardCommandWritten || !input.SupportCommandWritten)
                    return No("commands_not_written guard_written=" + B(input.GuardCommandWritten) +
                              " support_written=" + B(input.SupportCommandWritten));
                if (input.RelationshipEstablished) return No("relationship_already_established");
                if (!input.MainStillWaiting) return No("main_no_longer_holding");
                return Yes("guard_loss_before_established");
            }

            // 场景 A：关系已建立、主力已真正推进，且计划已稳定足够久。
            if (!input.RelationshipEstablished) return No("relationship_not_established");
            if (!input.MainAdvancing) return No("main_not_advancing");
            // 「任务已指定」不等于「主力已实际推进」：必须看到推进命令确实写出去，
            // 并在写后稳定一段时间才允许触发（派单 §9.2 L208、独立验收证据缺口）。
            if (!input.AdvanceCommandWritten) return No("advance_not_written");
            if (input.AdvanceWrittenAgeSeconds < input.StableSecondsRequired)
                return No("advance_not_settled age=" + F(input.AdvanceWrittenAgeSeconds) +
                          " required=" + F(input.StableSecondsRequired));
            if (!input.CompetitionWindowSatisfied) return No("competition_window_open");
            if (input.PlanAgeSeconds < input.StableSecondsRequired)
                return No("plan_not_stable age=" + F(input.PlanAgeSeconds) +
                          " required=" + F(input.StableSecondsRequired));
            return Yes("guard_loss_after_established");
        }

        // 取实际保护编队：由当前版本绑定的保护任务决定，
        // 不是 OpenTasks 顺序里第一个已生效对象。
        internal static string DescribeTarget(string guardFormationKey, int guardRevision, int planRevision)
        {
            return "target=guard key=" + (guardFormationKey.Length == 0 ? "<none>" : guardFormationKey) +
                   " task_rev=" + guardRevision +
                   " plan_rev=" + planRevision;
        }

        private static EligibilityTriggerDecision Yes(string reason)
        {
            return new EligibilityTriggerDecision(true, reason, "trigger_condition_met");
        }

        private static EligibilityTriggerDecision No(string reason)
        {
            return new EligibilityTriggerDecision(false, reason, "trigger_condition_unmet");
        }

        private static string B(bool v) { return v ? "1" : "0"; }

        private static string F(double v) { return v.ToString("F2", System.Globalization.CultureInfo.InvariantCulture); }
    }
}
