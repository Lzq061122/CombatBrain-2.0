using System;
using CombatBrain2.Core;
using Xunit;

namespace CombatBrain2.Tests
{
    // 回放 p3-P-20261018-210338 交战期（接敌后）各计划的失败判定。
    // 目的：用每张计划自己的几何与当时实际读数，调用当前生产判定，
    // 分清「该失败已被裁定①修复覆盖」与「质心确实不满足完整职责关系」。
    //
    // 读数来源（全部为日志原值，不是重算值）：
    //   - 几何：各计划自己的 COORDINATION_BOUND（M*/G*/u/v）
    //   - 质心：该计划失效前最后一条 COORDINATION_STATE 中该职责的 pos
    //   - ready/reads：同一条 COORDINATION_STATE
    // 坐标在日志里保留 1 位小数，因此本回放只用于区分「明显在包络内／外」，
    // 不用于判定贴边情形。
    public class CoordinationEnvelopeReplayTests
    {
        // 生产几何只有 Compute 入口；回放必须用日志里已冻结的那一份，
        // 因此按同一组字段直接还原，不重新按质心推导（否则就不是「每张计划自己的几何」）。
        private static CoordinationGeometry Geometry(double mx, double my, double gx, double gy,
                                                     double ux, double uy, double vx, double vy)
        {
            return new CoordinationGeometry
            {
                MainX = mx,
                MainY = my,
                GuardX = gx,
                GuardY = gy,
                ForwardX = ux,
                ForwardY = uy,
                LateralX = vx,
                LateralY = vy,
            };
        }

        // plan-3 @154.608：Guard 为 1/1（Ranged 172）。
        // 侧向 −5.68 m 越出 |lat|<=5，距 G* 6.13 m 越出 5 m。属「确实不满足完整关系」。
        [Fact]
        public void ReplayPlan3GuardIsOutsideTheFrozenEnvelope()
        {
            var g = Geometry(1301.632, 845.782, 1295.096, 838.213,
                             -0.654, -0.757, 0.757, -0.654);
            Assert.False(PlanBuilder.GuardInPosition(g, 1289.3, 840.2));
        }

        // plan-4 @163.596：Guard 为 1/1（Ranged 155）。
        // 位置已在包络内（fwd 11.84、lat −4.07、距 G* 4.46），但该 tick 的连续读数
        // 只累积了 69 次采样（ready=0）：失效时刻距本 tick 仅 0.49 s，
        // 1 s 连续窗口尚未建立，裁定①不成立。
        [Fact]
        public void ReplayPlan4GuardIsInsideTheFrozenEnvelopeButReadinessWasNotEstablished()
        {
            var g = Geometry(1303.688, 847.623, 1297.304, 839.926,
                             -0.638, -0.770, 0.770, -0.638);
            Assert.True(PlanBuilder.GuardInPosition(g, 1293.0, 841.1));
        }

        // plan-5 @172.601：Guard 为 1/1（Ranged 147），ready=1。
        // 位置与连续读数同时成立 → 裁定①（95316fd）在当前生产代码下会判为生效，
        // 而本场在 173.086 仍以 closed_distance=-0.03 判失败。
        [Fact]
        public void ReplayPlan5GuardWouldSatisfyRulingOneUnderCurrentCode()
        {
            var g = Geometry(1304.046, 847.458, 1296.853, 840.511,
                             -0.719, -0.695, 0.695, -0.719);
            Assert.True(PlanBuilder.GuardInPosition(g, 1296.6, 842.2));
        }

        // plan-6 @181.606：Guard 为 1/1（Ranged 145），ready=1。同上，裁定①覆盖。
        [Fact]
        public void ReplayPlan6GuardWouldSatisfyRulingOneUnderCurrentCode()
        {
            var g = Geometry(1304.727, 848.024, 1296.906, 841.793,
                             -0.782, -0.623, 0.623, -0.782);
            Assert.True(PlanBuilder.GuardInPosition(g, 1297.7, 843.2));
        }

        // plan-7 @190.608：Guard 为 1/2（Cavalry 52），ready=0。
        // 前向 4.88 m 低于 5 m 下界、距 G* 5.22 m 越出 5 m。属「确实不满足完整关系」。
        [Fact]
        public void ReplayPlan7GuardIsOutsideTheFrozenEnvelope()
        {
            var g = Geometry(1306.259, 850.102, 1297.092, 854.096,
                             -0.917, 0.399, -0.399, -0.917);
            Assert.False(PlanBuilder.GuardInPosition(g, 1302.2, 853.0));
        }

        // plan-8 在 205.112 以 SelectionTimeout 结束，没有任何写令与反馈，
        // 不构成「包络判定」样本：这里只固定这一事实，避免把它误列为包络失败。
        [Fact]
        public void ReplayPlan8NeverProducedAnEnvelopeVerdict()
        {
            var g = Geometry(1309.746, 855.540, 1314.241, 864.473,
                             0.450, 0.893, -0.893, 0.450);
            // 实际读数是 (1275.8, 824.2)：fwd −43.26、distG* 55.67，远在包络外。
            // 该 false 不是「包络判失败」，因为该任务从未写出命令，失败原因是选择超时。
            Assert.False(PlanBuilder.GuardInPosition(g, 1275.8, 824.2));
        }
    }
}
