using System.Threading;
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
        // 原生主循环不等于主线程：这里只负责把本次命令交给 TestDriver 判决，
        // 由它决定就地执行还是最小转交给已确认的主线程执行点。
        // 本方法只有 FormationAI.TickOccasionally 的 ActiveBehavior 分支会调用，
        // 所以每次进入这里本身就是「原生已选中本行为」的真实证据。
        public override void TickOccasionally()
        {
            if (!_armed) return;
            TestDriver.Instance.OnBehaviorTick(base.Formation, Thread.CurrentThread.ManagedThreadId);
        }

        protected override void CalculateCurrentOrder()
        {
            base.CurrentOrder = MovementOrder.MovementOrderStop;
        }

        // 取证探针：本类之外能改写本行为权重的入口只有 SetBehaviorWeight<T>（写任意值，
        // 调用方即 CB2 撤权路径）和这里（写 0）。重写本方法即覆盖全部原生清零路径。
        // 记录清零前的权重，使"非零变零"（一次真实的剥夺）与"本来就已为零的重复清零"
        // （同一战术 tick 反复触发）可区分，后者不代表再次失去执行机会。
        public override void ResetBehavior()
        {
            float weightBefore = WeightFactor;
            base.ResetBehavior();
            TestDriver.Instance.OnBehaviorResetWeight(Formation, weightBefore, WeightFactor);
        }

        protected override float GetAiWeight()
        {
            return _armed ? 1f : 0f;
        }
    }
}
