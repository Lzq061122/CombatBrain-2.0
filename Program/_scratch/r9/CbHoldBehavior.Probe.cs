// Verbatim copy of CombatBrain2 CbHoldBehavior with the two engine-side calls
// replaced by observable stubs, so the probe compiles the *same* class shape.
using System.Threading;
using TaleWorlds.MountAndBlade;

namespace CombatBrain2.Host
{
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

        public override void TickOccasionally()
        {
            if (!_armed) return;
            BehaviorTickSink.Record(base.Formation);
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

    internal static class BehaviorTickSink
    {
        internal static int Count;
        internal static Formation Last;

        internal static void Record(Formation f)
        {
            Count++;
            Last = f;
        }
    }
}
