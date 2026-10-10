using TaleWorlds.MountAndBlade;

namespace CombatBrain2.Host
{
    // Route C 的执行载体：一个常规 BehaviorComponent，注册进 FormationAI 的原生选择链。
    // 本方不直接写 MovementOrder，也不 RaiseWeight；由原生 FindBestBehavior 竞争选中后，
    // 原生自己在 TickOccasionally 里执行 SetMovementOrder(CurrentOrder)。
    // 未 armed 时权重为 0，完全不参与竞争，避免影响非本批时段。
    internal sealed class CbHoldBehavior : BehaviorComponent
    {
        private bool _armed;

        internal CbHoldBehavior(Formation formation)
            : base(formation)
        {
            base.CurrentOrder = MovementOrder.MovementOrderStop;
            base.BehaviorCoherence = 0f;
        }

        internal bool Armed { get { return _armed; } }

        internal void Arm()
        {
            _armed = true;
        }

        internal void Disarm()
        {
            _armed = false;
        }

        // 原生选中本行为后由 FormationAI 调用（受 IsAIControlled 门控）。
        // 写令发生在这里——即原生主循环内、主线程上——而不是本方另起一条腿抢写。
        public override void TickOccasionally()
        {
            if (!_armed) return;
            TestDriver.Instance.ExecuteFromBehavior(base.Formation);
        }

        protected override void CalculateCurrentOrder()
        {
            base.CurrentOrder = MovementOrder.MovementOrderStop;
        }

        protected override float GetAiWeight()
        {
            return _armed ? 1f : 0f;
        }
    }
}
