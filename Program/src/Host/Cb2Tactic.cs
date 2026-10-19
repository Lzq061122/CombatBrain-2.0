using CombatBrain2.Core;
using TaleWorlds.MountAndBlade;

namespace CombatBrain2.Host
{
    // P3 战术层接入的推荐方案，未接线、未部署：以 Team.Tactics 的一批替代位置参与原生
    // 战术分配，而不是让 TacticCharge 与本方 Behavior 同时争写同一编队的行为。
    // 本类目前只用于离线反例，未经实机验证。
    internal sealed class Cb2Tactic : TacticComponent
    {
        internal Cb2Tactic(Team team)
            : base(team)
        {
            Cb2TacticProbe.OnTacticConstructed(GetType(), true);
        }

        // 原生每轮 TacticComponent.TickOccasionally 都会回到这里；本类按同一周期刷新
        // 编队行为权重，保持与原生战术同等的持续职责，而不是只决定一次。
        internal int ManageCalls { get; private set; }

        public override void TickOccasionally()
        {
            var teamAI = Team == null ? null : Team.TeamAI;
            if (teamAI == null) return;
            ManageCalls++;
            if (!teamAI.GetIsFirstTacticChosen) return;

            TacticalPosition point = null;
            var points = teamAI.TacticalPositions;
            if (points != null && points.Count > 0) point = points[0];
            if (point == null) return;

            foreach (var f in FormationsIncludingEmpty)
            {
                if (f == null || !f.IsAIControlled || f.CountOfUnits == 0) continue;
                ChooseBehavior(f, point);
            }

            base.TickOccasionally();
        }

        private static void ChooseBehavior(Formation f, TacticalPosition point)
        {
            // 用战术点到本编队当前质心的平面距离；与 TacticCharge 一样每轮重现默认权重后
            // 再改动，避免上一轮改过的权重在本轮被悄悄沿用。
            double dx = f.CurrentPosition.x - (double)point.Position.AsVec2.x;
            double dy = f.CurrentPosition.y - (double)point.Position.AsVec2.y;
            float distance = (float)System.Math.Sqrt(dx * dx + dy * dy);

            f.AI.ResetBehaviorWeights();
            SetDefaultBehaviorWeights(f);
            if (distance < (float)TestParameters.TacticEffectivenessDistance)
            {
                f.AI.SetBehaviorWeight<BehaviorCharge>(1000f);
                f.AI.SetBehaviorWeight<BehaviorSkirmish>(1f);
            }
            else
            {
                f.AI.SetBehaviorWeight<BehaviorAdvance>(1f);
            }
        }
    }
}
