using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace CombatBrain2.Tests
{
    // P3 战术层接入（CB2-TACTIC-TEAM-EXPORT v1）的离线反例。
    // 断言的是「原生战术分配链的结构性前提」，不是 Cb2Tactic 的战术优劣；
    // 实机行为不在本文件覆盖范围内。
    // 依据来自 Program/_scratch/d3 下对运行程序集的反编译：
    //   TeamAIComponent.cs:338-344 TickOccasionally -> CurrentTactic?.TickOccasionally()
    //   TeamAIComponent.cs:203-212 Tick 每 5 s 一次 MakeDecision，每 _occasionalTickTime 一次 TickOccasionally
    //   TeamAIComponent.cs:301     MaxBy(availableTactics, weight * (current ? 1.5 : 1))
    //   TeamAIComponent.cs:309-312 换将时 CurrentTactic.ResetTacticalPositions() 为 false 即强制替换
    //   TacticComponent.cs:615-618 ResetTacticalPositions 默认返回 false
    public class TacticLayerContractTests
    {
        [Fact]
        public void Engine_TickDrivesExactlyOneCurrentTactic()
        {
            var teamAi = ReadScratch("TeamAIComponent.cs");
            var tickBody = Slice(teamAi, "public virtual void TickOccasionally()", "public bool IsCurrentTactic");
            Assert.Contains("CurrentTactic?.TickOccasionally();", tickBody);
            Assert.DoesNotContain("_availableTactics", tickBody);

            // 原生只允许一个当前战术：分配候选池与当前战术是两个不同字段。
            Assert.Contains("private readonly List<TacticComponent> _availableTactics;", teamAi);
            Assert.Contains("CurrentTactic = tacticComponent;", teamAi);
        }

        [Fact]
        public void Engine_ReplacementCanBeRefusedByTheOutgoingTactic()
        {
            var teamAi = ReadScratch("full-mnb.cs");
            var decide = Slice(teamAi, "private void MakeDecision()", "public virtual void TickOccasionally()");
            Assert.Contains("if (!CurrentTactic.ResetTacticalPositions())", decide);
            Assert.Contains("if (tacticWeight > num)", decide);

            // 换将时要读 GetTacticWeight，而该成员是 protected internal：外部程序集无法调用，
            // 所以「注册进候选池后由引擎选中」是唯一能拿到 CurrentTactic 的入口。
            Assert.Contains("protected internal virtual float GetTacticWeight()", teamAi);
            Assert.Contains("public void AddTacticOption(TacticComponent tacticOption)", teamAi);
        }

        [Fact]
        public void Engine_FieldBattleRegistersTacticsThroughTeamFacade()
        {
            var full = ReadScratch("full-mnb.cs");

            // Team.AddTacticOption 是公开门面，内部才检查 HasTeamAi 并转给 TeamAI。
            // 两个同名成员（TeamAIComponent 与 Team）的先后顺序不稳定，按内容定位门面。
            const string facadeSignature = "public void AddTacticOption(TacticComponent tacticOption)";
            string facade = null;
            for (int at = full.IndexOf(facadeSignature, StringComparison.Ordinal);
                 at >= 0;
                 at = full.IndexOf(facadeSignature, at + facadeSignature.Length, StringComparison.Ordinal))
            {
                var candidate = full.Substring(at, 260);
                if (candidate.Contains("HasTeamAi"))
                {
                    facade = candidate;
                    break;
                }
            }
            Assert.True(facade != null, "未找到 Team.AddTacticOption 门面");
            Assert.Contains("TeamAI.AddTacticOption(tacticOption);", facade);

            // 野战的候选池由战役任务注册，依赖双方最高的战术技能，不是固定集合。
            // 同一枚举值出现两次：先建 TeamAIGeneral，再注册战术选项，取后一处。
            const string fieldCase = "case Mission.MissionTeamAITypeEnum.FieldBattle:";
            int firstCase = full.IndexOf(fieldCase, StringComparison.Ordinal);
            int registerCase = full.IndexOf(fieldCase, firstCase + fieldCase.Length, StringComparison.Ordinal);
            Assert.True(registerCase > firstCase, "未找到野战战术注册处");
            int siegeCase = full.IndexOf("case Mission.MissionTeamAITypeEnum.Siege:", registerCase, StringComparison.Ordinal);
            Assert.True(siegeCase > registerCase, "未找到野战战术注册段终点");
            var field = full.Substring(registerCase, siegeCase - registerCase);
            Assert.Contains("team.AddTacticOption(new TacticCharge(team));", field);
            Assert.Contains("AddTacticOption(new TacticFullScaleAttack(team));", field);
            Assert.Contains("num >= 20f", field);
            Assert.Contains("num >= 50f", field);
        }

        [Fact]
        public void CandidateTactic_UsesSameWeightsAndSameResetAsNativeCharge()
        {
            var source = ReadHostSource("Cb2Tactic.cs");
            var native = ReadScratch("TacticCharge.cs");

            Assert.Contains("SetBehaviorWeight<BehaviorCharge>", source);
            Assert.Contains("SetBehaviorWeight<BehaviorCharge>", native);

            // 两者都必须先清空再重设默认权重，不能只追加一条高权重；
            // 否则上一轮写入的权重会跨轮残留，另一方指挥者仍可能改写同一编队。
            Assert.Contains("ResetBehaviorWeights()", source);
            Assert.Contains("ResetBehaviorWeights()", native);
            Assert.Contains("SetDefaultBehaviorWeights(f)", source);
            Assert.Contains("SetDefaultBehaviorWeights(item)", native);

            // 与 TeamAIComponent.Tick 的逐个覆盖不同：本类不得直接写 BodyGuardFormation 相关行为。
            Assert.DoesNotContain("BodyGuardFormation", source);
        }

        [Fact]
        public void CandidateTactic_IsNotWiredIntoProduction()
        {
            // 只看类型本身：探针辅助类 Cb2TacticProbe 是离线钩子，不构成接线。
            var sources = HostSourceFiles()
                .Where(f => !f.EndsWith("Cb2Tactic.cs", StringComparison.Ordinal) &&
                            !f.EndsWith("Cb2TacticProbe.cs", StringComparison.Ordinal))
                .ToArray();
            foreach (var file in sources)
            {
                var text = File.ReadAllText(file);
                Assert.DoesNotContain("new Cb2Tactic(", text);
                Assert.DoesNotContain("TacticComponent", text);
            }
        }

        private static string Slice(string text, string from, string to)
        {
            int start = text.IndexOf(from, StringComparison.Ordinal);
            if (start < 0) throw new InvalidOperationException("未找到切片起点: " + from);
            int end = text.IndexOf(to, start + from.Length, StringComparison.Ordinal);
            if (end < 0) end = text.Length;
            return text.Substring(start, end - start);
        }

        private static IEnumerable<string> HostSourceFiles()
        {
            return Directory.GetFiles(Path.Combine(RepoRoot(), "src", "Host"), "*.cs");
        }

        private static string ReadHostSource(string name)
        {
            return File.ReadAllText(Path.Combine(RepoRoot(), "src", "Host", name));
        }

        private static string ReadScratch(string name)
        {
            return File.ReadAllText(Path.Combine(RepoRoot(), "_scratch", "d3", name));
        }

        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "CombatBrain2.csproj"))) return dir.FullName;
                dir = dir.Parent;
            }
            throw new InvalidOperationException("未找到 CombatBrain2.csproj");
        }
    }
}
