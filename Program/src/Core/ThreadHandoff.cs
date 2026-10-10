namespace CombatBrain2.Core
{
    // 转交到主线程的最小载体：只保存"本次这一条命令"，不建调度框架。
    internal sealed class PendingCommand
    {
        internal string IntentId = "";
        internal string Key = "";
        internal string ObjectIdentity = "";
        internal int CallbackThreadId = -1;
        internal double MissionTime;
    }

    // 主线程执行保障。派单要求先核对回调线程，再在已确认的主线程执行点写令。
    // 主线程号由宿主 Mission tick 首次进入时登记一次；未登记前任何线程都不视为主线程。
    // 非主线程来的获选回调不允许就地写令，只能把本次命令放进单槽位转交。
    internal sealed class ThreadHandoff
    {
        private int _mainThreadId = -1;
        private PendingCommand _pending;
        private int _directCount;
        private int _deferredCount;

        internal bool HasMainThread { get { return _mainThreadId >= 0; } }
        internal int MainThreadId { get { return _mainThreadId; } }
        internal bool HasPending { get { return _pending != null; } }
        internal int DirectCount { get { return _directCount; } }
        internal int DeferredCount { get { return _deferredCount; } }

        internal void MarkMainThread(int threadId)
        {
            if (_mainThreadId < 0) _mainThreadId = threadId;
        }

        internal bool IsMainThread(int threadId)
        {
            return _mainThreadId >= 0 && threadId == _mainThreadId;
        }

        internal void NoteDirect()
        {
            _directCount++;
        }

        // 同一个意图只保留一条待执行命令：后来者不覆盖先到的那条。
        internal bool TryDefer(string intentId, string key, string objectIdentity,
                              int callbackThreadId, double missionTime)
        {
            if (_pending != null) return false;
            _pending = new PendingCommand
            {
                IntentId = intentId,
                Key = key,
                ObjectIdentity = objectIdentity,
                CallbackThreadId = callbackThreadId,
                MissionTime = missionTime
            };
            _deferredCount++;
            return true;
        }

        internal PendingCommand TakePending()
        {
            var p = _pending;
            _pending = null;
            return p;
        }

        internal void Clear()
        {
            _pending = null;
        }
    }
}
