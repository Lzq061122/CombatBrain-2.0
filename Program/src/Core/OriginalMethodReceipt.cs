namespace CombatBrain2.Core
{
    // 原方法完成/被跳过的回执。派单明确排除"普通返回"作为唯一完成证明：
    // setter 调用正常返回只能说明调用回来了，不能说明原方法体真的执行过。
    // 观测点取原方法体第一条语句触发的原生事件——它被跳过时不会触发。
    // 只有在本方这次调用的武装窗口内观测到该事件，才记为"原方法已执行"。
    internal sealed class OriginalMethodReceipt
    {
        private bool _armed;
        private bool _entered;
        private int _enterThreadId = -1;

        internal int EnterThreadId { get { return _enterThreadId; } }

        internal void Arm()
        {
            _armed = true;
            _entered = false;
            _enterThreadId = -1;
        }

        internal void ObserveEnter(int threadId)
        {
            if (!_armed) return;
            if (_entered) return;
            _entered = true;
            _enterThreadId = threadId;
        }

        internal bool Disarm()
        {
            bool entered = _entered;
            _armed = false;
            _entered = false;
            return entered;
        }
    }
}
