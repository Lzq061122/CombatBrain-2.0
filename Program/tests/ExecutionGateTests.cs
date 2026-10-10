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
    }
}
