using System;

namespace CombatBrain2.Core
{
    // 本批测试参数。经批准后钉死，不因案例未触发或失败而放宽。
    internal static class TestParameters
    {
        // 已批准的测试参数：非战术参数。
        internal const double SnapshotMaxAgeSeconds = 2.0;
        internal const double SelectionWaitSeconds = 10.0;

        // 本方行为在原生选择链里的权重。原生常规行为的 GetAiWeight 基准是 1f，
        // 且当前活跃行为带 1.2~2.0 的惯性加成；这里一次设定到位，
        // 不在每 tick 反复加压。这是本批测试参数，不是战术参数。
        internal const double HoldBehaviorWeight = 8.0;

        internal const double Now = 0.0;

        // P1 最小指挥闭环参数。评价周期与计划稳定时间不参与资格安全时限：
        // 安全资格检查在每个已确认主线程的 Mission tick 上逐 tick 执行。
        internal const double DecisionCadenceSeconds = 3.0;
        internal const double PlanMinStableSeconds = 8.0;
        internal const double FirstCommandDelaySeconds = 1.5;
        internal const double FormationStaggerSeconds = 1.5;
        internal const double FeedbackWindowSeconds = 3.0;

        // 条件门槛：远程占比差与兵力对比。
        // 创建分支/主对照用 RangedEdgeThreshold（±0.10）；继续判定用更窄的 ±0.05。
        internal const double RangedEdgeThreshold = 0.10;
        internal const double RangedEdgeContinueThreshold = 0.05;
        internal const double StrengthTolerance = 0.80;
        internal const double StrengthDisadvantageLine = 0.70;
        // 继续兵力线：我方不少于敌方的 70%。
        internal const double ContinueStrengthLine = 0.70;

        // 位置任务的有限移动距离。
        internal const double RangedApproachMaxMeters = 25.0;
        internal const double InfantryFallbackMeters = 20.0;

        // 命令生效判据：位移投影门槛与目标点到达容差。
        internal const double EffectiveDisplacementMeters = 3.0;
        internal const double TargetPointToleranceMeters = 0.5;

        // 实际失效发生到关闭任务、清旧待写命令及撤权。不受评价周期与计划稳定时间约束。
        internal const double SafetyCancelLatencySeconds = 1.0;
    }

    internal enum CancelReason
    {
        None = 0,
        WindowExpired,
        SelectionTimeout,
        ExplicitClose,
        TargetInvalid,
        PlayerEligibilityLost,
        AiControlLost,
        MissionEnded,
    }

    // 任务类型。本批只用这三种，不一次做满功能草案的任务库。
    internal enum TaskKind
    {
        None = 0,
        Hold,
        Engage,
        Reposition,
    }

    // 计划失效/任务反馈失败的实际原因。每一条都必须由可核查事实产生。
    internal enum PlanInvalidReason
    {
        None = 0,
        ConditionChanged,
        SelectionTimeout,
        WriteFailed,
        NotEffective,
        CommandOverridden,
        FormationEligibilityLost,
        MissionEnded,
    }

    internal enum TaskFeedbackState
    {
        None = 0,
        Pending,
        Effective,
        Failed,
    }

    internal enum SelectionRejectReason
    {
        None = 0,
        Empty,
        PlayerOwned,
        NotAiControlled,
        NotInfantry,
        SnapshotStale,
        NoMission,
    }
}
