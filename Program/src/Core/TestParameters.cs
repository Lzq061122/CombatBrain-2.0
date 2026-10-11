using System;

namespace CombatBrain2.Core
{
    // 本批测试参数。经批准后钉死，不因案例未触发或失败而放宽。
    internal static class TestParameters
    {
        // 已批准的测试参数：非战术参数。
        internal const double SnapshotMaxAgeSeconds = 2.0;
        internal const double IntentWindowSeconds = 20.0;
        internal const double SelectionWaitSeconds = 10.0;

        // Mission 就绪后延迟触发测试意图，给战场状态留出稳定时间。
        internal const double TriggerDelaySeconds = 15.0;

        // 两次测试意图之间的间隔，给第一次的撤销留出观测窗口。
        internal const double BetweenIntentSeconds = 5.0;

        // 写令成功后固定延迟显式关闭，验证显式关闭路径。
        internal const double ExplicitCloseAfterWriteSeconds = 6.0;

        // 本方行为在原生选择链里的权重。原生常规行为的 GetAiWeight 基准是 1f，
        // 且当前活跃行为带 1.2~2.0 的惯性加成；这里一次设定到位，
        // 不在每 tick 反复加压。这是本批测试参数，不是战术参数。
        internal const double HoldBehaviorWeight = 8.0;

        internal const double Now = 0.0;
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
