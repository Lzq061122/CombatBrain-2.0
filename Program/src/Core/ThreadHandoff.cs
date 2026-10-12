using System;
using System.Collections.Generic;

namespace CombatBrain2.Core
{
    // 转交到主线程的最小载体：只保存"本次这一条命令"，不建调度框架。
    // 载体带 mission/plan/task 身份，主线程取出后仍要按当前任务复核。
    internal sealed class PendingCommand
    {
        internal string MissionKey = "";
        internal string PlanId = "";
        internal string TaskId = "";
        internal string Key = "";
        internal string ObjectIdentity = "";
        internal int CallbackThreadId = -1;
        internal double MissionTime;
    }

    // 主线程执行保障。派单要求先核对回调线程，再在已确认的主线程执行点写令。
    // 主线程号由宿主 Mission tick 首次进入时登记一次；未登记前任何线程都不视为主线程。
    // 非主线程来的获选回调不允许就地写令，只能把本次命令放进待执行槽位。
    // 不同编队各有独立槽位：一支编队的占用不会挡住另一支编队的命令。
    internal sealed class ThreadHandoff
    {
        // 跨线程共享：回调线程投递、主线程取出、主线程清空都要过同一把锁。
        private readonly object _sync = new object();
        private int _mainThreadId = -1;
        private readonly Dictionary<string, PendingCommand> _pendingByKey =
            new Dictionary<string, PendingCommand>(StringComparer.Ordinal);
        private int _directCount;
        private int _deferredCount;
        private int _slotBusyCount;
        private int _duplicateCount;

        internal bool HasMainThread { get { lock (_sync) { return _mainThreadId >= 0; } } }
        internal int MainThreadId { get { lock (_sync) { return _mainThreadId; } } }
        internal bool HasPending { get { lock (_sync) { return _pendingByKey.Count > 0; } } }
        internal int PendingCount { get { lock (_sync) { return _pendingByKey.Count; } } }
        internal int DirectCount { get { lock (_sync) { return _directCount; } } }
        internal int DeferredCount { get { lock (_sync) { return _deferredCount; } } }
        internal int SlotBusyCount { get { lock (_sync) { return _slotBusyCount; } } }
        internal int DuplicateCount { get { lock (_sync) { return _duplicateCount; } } }

        internal void MarkMainThread(int threadId)
        {
            lock (_sync)
            {
                if (_mainThreadId < 0) _mainThreadId = threadId;
            }
        }

        internal bool IsMainThread(int threadId)
        {
            lock (_sync)
            {
                return _mainThreadId >= 0 && threadId == _mainThreadId;
            }
        }

        internal void NoteDirect()
        {
            lock (_sync)
            {
                _directCount++;
            }
        }

        // 每个编队键最多一条待执行命令：同一任务重复投递去重，
        // 不同任务占用同一编队键时保留先到者，不覆盖。
        internal bool TryDefer(string missionKey, string planId, string taskId, string key,
                               string objectIdentity, int callbackThreadId, double missionTime)
        {
            lock (_sync)
            {
                PendingCommand existing;
                if (_pendingByKey.TryGetValue(key ?? "", out existing))
                {
                    if (string.Equals(existing.TaskId, taskId, StringComparison.Ordinal))
                    {
                        _duplicateCount++;
                    }
                    else
                    {
                        _slotBusyCount++;
                    }
                    return false;
                }
                _pendingByKey[key ?? ""] = new PendingCommand
                {
                    MissionKey = missionKey ?? "",
                    PlanId = planId ?? "",
                    TaskId = taskId ?? "",
                    Key = key ?? "",
                    ObjectIdentity = objectIdentity ?? "",
                    CallbackThreadId = callbackThreadId,
                    MissionTime = missionTime
                };
                _deferredCount++;
                return true;
            }
        }

        // 取出全部待执行命令（按编队键稳定排序），主线程逐个复核。
        internal List<PendingCommand> TakeAllPending()
        {
            lock (_sync)
            {
                var list = new List<PendingCommand>(_pendingByKey.Values);
                _pendingByKey.Clear();
                list.Sort(CompareByKey);
                return list;
            }
        }

        internal void Clear()
        {
            lock (_sync)
            {
                _pendingByKey.Clear();
            }
        }

        private static int CompareByKey(PendingCommand a, PendingCommand b)
        {
            return string.CompareOrdinal(a.Key, b.Key);
        }
    }
}
