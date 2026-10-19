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

        // CB2 获选 TacticComponent 时的 GetTacticWeight 返回值。
        // 原生 TacticCharge 权重 = GetAIWeight()(~0.822/0.1) × WeightFactor(10000) ≈ 8222.2；
        // 挑战还要超过现任的 1.5 倍。这里取有限优先级，不沿用战果公式、不按兵力比浮动。
        internal const double TacticTaskPriority = 50000.0;

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

        // ---- P2 多编队协同参数（派单 §7，实测前冻结，不因失败放宽）----

        // 协同目标身份不随挂钟改变；评价时用同一次输入自带的时刻。
        internal const double P2Now = 200.0;

        // 协同几何：主力有限接近与保护/辅助目标点。
        internal const double CoordinationApproachMaxMeters = 25.0;
        internal const double CoordinationApproachDivisor = 4.0;
        internal const double CoordinationGuardForwardMeters = 10.0;
        internal const double CoordinationSupportLateralMeters = 12.0;

        // 到位判据：到规范目标点半径、保护的前向投影区间与侧向偏差、辅助的侧向投影区间。
        internal const double CoordinationArrivalRadiusMeters = 5.0;
        internal const double GuardForwardMinMeters = 5.0;
        internal const double GuardForwardMaxMeters = 15.0;
        internal const double GuardLateralMaxMeters = 5.0;
        internal const double SupportLateralMinMeters = 7.0;
        internal const double SupportLateralMaxMeters = 17.0;

        // 协同位置必须由连续至少 1 s 的两次或更多有效读取确认，单次读数不放行。
        internal const double CoordinationStableSeconds = 1.0;

        // 保护/辅助写入后最多等待 20 s 建立关系；到期未到位即终止该计划。
        // 这个截止与"3 s 内是否产生移动效果"是两件事，后者仍按 P1 的 FeedbackWindowSeconds。
        internal const double CoordinationRelationshipDeadlineSeconds = 20.0;

        // 职责人数继续线：剩余有效人数不少于 6 且不少于该职责分配时基线的 50%。相等允许继续。
        internal const int RoleUnitFloor = 6;
        internal const double RoleUnitRatioFloor = 0.50;

        // A5 竞争注入：挑战者相对规范目标点的侧向偏移量（派单 §9.2 场景 A 第 2 条）。
        // 偏移后仍须落在本任务的到位区域内，否则它就不是「同级合法」候选。
        internal const double P2CompetitionLateralMeters = 2.0;

        // A5 竞争注入窗口：注入期间必须持续到这个时长（派单 §9.2 要求 ≥8 s，
        // 跨至少两个评价点）。
        internal const double P2CompetitionWindowSeconds = 8.0;

        // A5 注入窗口：自首次注入起持续这么久。评价点间隔 3 s，跨越至少三个评价点，
        // 满足「注入期间 ≥8 s、跨至少两个评价点」（派单 §9.2 场景 A 第 2 条）。
        internal const double P2CompetitionSeconds = 9.0;

        // ---- P3 反馈、止损与重规划参数（派单 §3.2/§3.3，实测前冻结，不因失败放宽）----

        // 观察对象要求的敌方步兵有效成员下限。低于此值不冻结基线。
        internal const int P3MinimumEnemyMembers = 20;

        // 结果量纲：局部损失与整队损失的分母尺度。
        internal const double P3LocalLossScale = 0.20;
        internal const double P3SideLossScale = 0.10;

        // 速率窗口：与当前评价点至少相隔这么久的最近有效评价样本。
        internal const double P3RateWindowSeconds = 12.0;

        // ① 正反馈追加：局部战果阈值、己方累计损失上限、损失速率上限、效用下限。
        internal const double P3ReinforceLe = 0.15;
        internal const double P3ReinforceLf = 0.05;
        internal const double P3ReinforceRate = 0.05;
        internal const double P3ReinforceU = 0.60;

        // ② 高损失止损：整队累计损失线（无需速率窗口）与损失速率线。
        internal const double P3StopLossLf = 0.10;
        internal const double P3StopLossRate = 0.10;

        // ③ 无收益且有代价：收益上限、代价下限、效用上限与最小观察时间。
        internal const double P3DisengageLe = 0.05;
        internal const double P3DisengageLf = 0.05;
        internal const double P3DisengageU = 0.40;
        internal const double P3DisengageMinSeconds = 24.0;

        // 局部价值存续：观察对象当前仍需存在的活跃 E0 成员数。
        internal const int P3LocalValueMinMembers = 6;

        // 风险门半径：以拟新增主力目标点为中心统计敌方有效人数。
        internal const double P3RiskRadiusMeters = 30.0;

        // 继任计划新增主力的有限接近量。
        internal const double P3SuccessorApproachMaxMeters = 25.0;
        internal const double P3SuccessorApproachDivisor = 4.0;

        // 负反馈撤回：每个对象沿背离敌队质心方向移动的距离、到位半径与截止时间。
        internal const double P3WithdrawMeters = 20.0;
        internal const double P3WithdrawArrivalMeters = 5.0;
        internal const double P3WithdrawDeadlineSeconds = 20.0;
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

        // P3：本轮行动的实际结果反馈要求终止旧攻势并进入继任计划。
        ActionFeedback,
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

    // P2 提案被拒的具体原因。每一条都必须由可核查事实产生，不能合并成"非法"一个桶。
    internal enum ProposalRejectReason
    {
        None = 0,
        MissionMismatch,
        PlanMismatch,
        PlanClosed,
        StaleRevision,
        TaskUnknown,
        TaskClosed,
        TaskNotInPlan,
        FormationMismatch,
        ObjectMismatch,
        UnauthorizedSource,
        KindNotAllowedForRole,
        KindNotAllowedForClass,
        Ineligible,
        InvalidGeometry,
        Superseded,
    }

    // 提案来源。只有本计划自己的阶段转换可以改写另一对象的任务。
    internal enum ProposalSource
    {
        None = 0,
        TaskSubmission,
        PhaseTransition,
        TestInjection,
    }
}
