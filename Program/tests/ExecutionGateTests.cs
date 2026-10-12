using System;
using System.Collections.Generic;
using CombatBrain2.Core;
using Xunit;

namespace CombatBrain2.Tests
{
    // 这些测试面向尚未实现的行为：执行门、去重、撤销。
    public class ExecutionGateTests
    {
        // 候选阶段绝不允许写入：提交候选不得产生任何写入尝试。
        [Fact]
        public void CandidateSubmissionPerformsNoWrite()
        {
            var gate = new ExecutionGate();
            gate.SubmitCandidate("i1", "1/0", 100.0);
            Assert.Equal(0, gate.WriteAttempts);
        }

        // 未被原生选中时，即使候选已提交也不得写入。
        [Fact]
        public void WriteBlockedBeforeRealSelection()
        {
            var gate = new ExecutionGate();
            gate.SubmitCandidate("i1", "1/0", 100.0);
            var decision = gate.RequestWrite("i1", "1/0", 101.0, true, true, false, "aaa@Infantry", true, true);
            Assert.False(decision.Allowed);
            Assert.Equal(0, gate.WriteAttempts);
        }

        // 只有真实获选后才允许写入一次。
        [Fact]
        public void WriteAllowedOnceAfterRealSelection()
        {
            var gate = new ExecutionGate();
            gate.SubmitCandidate("i1", "1/0", 100.0);
            gate.BindObject("i1", "aaa@Infantry");
            gate.MarkSelected("i1", "1/0", 101.0);
            var d1 = gate.RequestWrite("i1", "1/0", 101.1, true, true, false, "aaa@Infantry", true, true);
            Assert.True(d1.Allowed);
            Assert.Equal(1, gate.WriteAttempts);
        }

        // 多编队不得共用一次写入额度：两个任务各自拥有独立额度，
        // 先写者不消耗后写者的额度。
        [Fact]
        public void WriteCreditIsIndependentPerTask()
        {
            var gate = new ExecutionGate();
            gate.SubmitCandidate("task-a", "1/0", 100.0);
            gate.BindObject("task-a", "aaa@Infantry");
            gate.MarkSelected("task-a", "1/0", 101.0);
            gate.SubmitCandidate("task-b", "1/1", 100.0);
            gate.BindObject("task-b", "bbb@Ranged");
            gate.MarkSelected("task-b", "1/1", 101.0);

            var da = gate.RequestWrite("task-a", "1/0", 101.1, true, true, false, "aaa@Infantry", true, true);
            Assert.True(da.Allowed);
            // task-a 已写，不得再写第二次。
            var da2 = gate.RequestWrite("task-a", "1/0", 101.2, true, true, false, "aaa@Infantry", true, true);
            Assert.False(da2.Allowed);
            Assert.Equal("already_written", da2.BlockReason);

            // task-b 的额度未被 task-a 消耗，仍可独立写入。
            var db = gate.RequestWrite("task-b", "1/1", 101.3, true, true, false, "bbb@Ranged", true, true);
            Assert.True(db.Allowed);
            Assert.Equal(1, gate.WriteAttemptsFor("task-b"));

            // 总计 2 次：每个任务各一次，没有共用。
            Assert.Equal(2, gate.WriteAttempts);

            // 关闭一个任务不影响另一个任务的额度。
            gate.Close("task-a", CancelReason.ExplicitClose, 102.0);
            Assert.Equal(0, gate.WriteAttemptsFor("task-c"));
        }

        // 一个任务的关闭/额度不影响另一个任务。
        [Fact]
        public void ClosingOneTaskDoesNotAffectAnother()
        {
            var gate = new ExecutionGate();
            gate.SubmitCandidate("task-a", "1/0", 100.0);
            gate.MarkSelected("task-a", "1/0", 101.0);
            gate.Close("task-a", CancelReason.PlayerEligibilityLost, 102.0);
            Assert.Equal("intent_closed",
                gate.RequestWrite("task-a", "1/0", 102.1, true, true, false, "aaa@Infantry", true, true).BlockReason);

            gate.SubmitCandidate("task-b", "1/1", 100.0);
            gate.BindObject("task-b", "bbb@Ranged");
            gate.MarkSelected("task-b", "1/1", 101.0);
            var db = gate.RequestWrite("task-b", "1/1", 102.2, true, true, false, "bbb@Ranged", true, true);
            Assert.True(db.Allowed);
            Assert.Equal(1, gate.WriteAttemptsFor("task-b"));
        }

        // 同一意图/激活的重复回调必须去重，不得重复写。
        [Fact]
        public void DuplicateCallbacksAreDeduplicated()
        {
            var gate = new ExecutionGate();
            gate.SubmitCandidate("i1", "1/0", 100.0);
            gate.BindObject("i1", "aaa@Infantry");
            gate.MarkSelected("i1", "1/0", 101.0);
            gate.RequestWrite("i1", "1/0", 101.1, true, true, false, "aaa@Infantry", true, true);
            for (int n = 0; n < 50; n++)
            {
                var d = gate.RequestWrite("i1", "1/0", 101.2 + n * 0.01, true, true, false, "aaa@Infantry", true, true);
                Assert.False(d.Allowed);
            }
            Assert.Equal(1, gate.WriteAttempts);
        }

        // 玩家拥有 / 目标失效 / 资格丢失时不得写入。
        [Fact]
        public void WriteBlockedWhenTargetInvalidOrPlayerOwned()
        {
            var gate = new ExecutionGate();
            gate.SubmitCandidate("i1", "1/0", 100.0);
            gate.BindObject("i1", "aaa@Infantry");
            gate.MarkSelected("i1", "1/0", 101.0);

            Assert.False(gate.RequestWrite("i1", "1/0", 101.1, true, true, true, "aaa@Infantry", true, true).Allowed);   // 玩家拥有
            Assert.False(gate.RequestWrite("i1", "1/0", 101.2, false, true, false, "aaa@Infantry", true, true).Allowed); // Mission 无效
            Assert.False(gate.RequestWrite("i1", "1/0", 101.3, true, false, false, "aaa@Infantry", true, true).Allowed); // 目标失效
            Assert.Equal(0, gate.WriteAttempts);
        }

        // 不同对象不得借用已获选的意图写入。
        [Fact]
        public void WriteBlockedForDifferentObject()
        {
            var gate = new ExecutionGate();
            gate.SubmitCandidate("i1", "1/0", 100.0);
            gate.BindObject("i1", "aaa@Infantry");
            gate.MarkSelected("i1", "1/0", 101.0);
            var d = gate.RequestWrite("i1", "1/3", 101.1, true, true, false, "aaa@Infantry", true, true);
            Assert.False(d.Allowed);
            Assert.Equal(0, gate.WriteAttempts);
        }

        // 窗口到期后不得再写入。
        [Fact]
        public void WriteBlockedAfterWindowExpiry()
        {
            var gate = new ExecutionGate();
            gate.SubmitCandidate("i1", "1/0", 100.0);
            gate.BindObject("i1", "aaa@Infantry");
            gate.MarkSelected("i1", "1/0", 101.0);
            gate.Close("i1", CancelReason.WindowExpired, 120.0);
            Assert.False(gate.RequestWrite("i1", "1/0", 120.1, true, true, false, "aaa@Infantry", true, true).Allowed);
            Assert.Equal(0, gate.WriteAttempts);
        }

        // 执行前复核（Codex §4）：对象身份必须仍是获选时绑定的那个。
        [Fact]
        public void WriteBlockedWhenObjectIdentityChanged()
        {
            var gate = new ExecutionGate();
            gate.SubmitCandidate("i1", "1/0", 100.0);
            gate.BindObject("i1", "aaa@Infantry");
            gate.MarkSelected("i1", "1/0", 101.0);
            var d = gate.RequestWrite("i1", "1/0", 101.1, true, true, false, "bbb@Infantry", true, true);
            Assert.False(d.Allowed);
            Assert.Equal("object_identity_changed", d.BlockReason);
            Assert.Equal(0, gate.WriteAttempts);
        }

        // 执行前复核：目标必须仍由 AI 控制。
        [Fact]
        public void WriteBlockedWhenNotAiControlled()
        {
            var gate = new ExecutionGate();
            gate.SubmitCandidate("i1", "1/0", 100.0);
            gate.BindObject("i1", "aaa@Infantry");
            gate.MarkSelected("i1", "1/0", 101.0);
            var d = gate.RequestWrite("i1", "1/0", 101.1, true, true, false, "aaa@Infantry", false, true);
            Assert.False(d.Allowed);
            Assert.Equal("not_ai_controlled", d.BlockReason);
            Assert.Equal(0, gate.WriteAttempts);
        }

        // 执行前复核：原生当前行为必须仍是本方注册的那个行为。
        [Fact]
        public void WriteBlockedWhenActiveBehaviorNotOurs()
        {
            var gate = new ExecutionGate();
            gate.SubmitCandidate("i1", "1/0", 100.0);
            gate.BindObject("i1", "aaa@Infantry");
            gate.MarkSelected("i1", "1/0", 101.0);
            var d = gate.RequestWrite("i1", "1/0", 101.1, true, true, false, "aaa@Infantry", true, false);
            Assert.False(d.Allowed);
            Assert.Equal("behavior_not_ours", d.BlockReason);
            Assert.Equal(0, gate.WriteAttempts);
        }

        // 复核全部通过才允许写一次，且重复回调仍去重。
        [Fact]
        public void WriteAllowedOnceWithFullPrecheck()
        {
            var gate = new ExecutionGate();
            gate.SubmitCandidate("i1", "1/0", 100.0);
            gate.BindObject("i1", "aaa@Infantry");
            gate.MarkSelected("i1", "1/0", 101.0);
            var d1 = gate.RequestWrite("i1", "1/0", 101.1, true, true, false, "aaa@Infantry", true, true);
            Assert.True(d1.Allowed);
            var d2 = gate.RequestWrite("i1", "1/0", 101.2, true, true, false, "aaa@Infantry", true, true);
            Assert.False(d2.Allowed);
            Assert.Equal("already_written", d2.BlockReason);
            Assert.Equal(1, gate.WriteAttempts);
        }

        // 候选键必须可读回，供原生选中比对。
        [Fact]
        public void PendingKeyExposesSubmittedCandidate()
        {
            var gate = new ExecutionGate();
            gate.SubmitCandidate("i1", "1/0", 100.0);
            Assert.Equal("1/0", gate.PendingKey("i1"));
        }

        // 未提交候选时没有可比的键。
        [Fact]
        public void PendingKeyEmptyBeforeSubmission()
        {
            var gate = new ExecutionGate();
            Assert.Equal("", gate.PendingKey("i1"));
        }

        // 原生选中别的编队：候选键仍是本方那支，不会漂移到别的对象。
        [Fact]
        public void PendingKeyStaysBoundToSubmittedCandidate()
        {
            var gate = new ExecutionGate();
            gate.SubmitCandidate("i1", "1/0", 100.0);
            gate.MarkSelected("i1", "1/0", 101.0);
            Assert.Equal("1/0", gate.PendingKey("i1"));
            Assert.NotEqual("1/3", gate.PendingKey("i1"));
        }

        // 独立退出动作必须能被单独识别：普通任务的"计划已关闭"兜底门只对普通任务成立，
        // 否则已获选的退出 Stop 会被误挡（脚本 §7.2）。
        [Fact]
        public void IndependentExitTaskIsDistinguishableFromNormalTask()
        {
            var exit = new FormationTask { TaskId = "plan-1-task-1-exit", PlanId = "plan-1", Kind = TaskKind.Hold };
            var normal = new FormationTask { TaskId = "plan-1-task-1", PlanId = "plan-1", Kind = TaskKind.Reposition };
            Assert.True(BattlePlan.IsExitTask(exit));
            Assert.False(BattlePlan.IsExitTask(normal));
        }
    }
}
