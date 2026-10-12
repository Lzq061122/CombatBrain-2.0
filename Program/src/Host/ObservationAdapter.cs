using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace CombatBrain2.Host
{
    // Writes structured decision-chain events as JSON lines.
    //
    // This is an observation adapter only: it never reads game objects, never
    // touches engine state and never influences a decision. TestDriver already
    // reaches a logging call at every decision point, so the adapter is called
    // from exactly those places.
    //
    // battle_id / session_id come from the file CBATH publishes while a battle
    // is running. When that file is absent (CB2 running without the observation
    // recorder) no events are written at all, so CB2 keeps its original
    // behaviour.
    //
    // The write is a non-blocking enqueue: a background thread owns the file.
    internal static class ObservationAdapter
    {
        private static readonly object Gate = new object();
        private static readonly Encoding NoBomUtf8 = new UTF8Encoding(false);

        private static string _battleId;
        private static string _sessionId;
        private static string _directory;
        private static string _eventPath;
        private static bool _enabled;
        private static double _boundMissionTime = double.MinValue;
        private static double _lastAttemptAt = double.MinValue;
        private static bool _attempted;

        private static BlockingCollection<string> _queue;
        private static Thread _thread;
        private static int _dropped;
        private static long _producerSeq;
        private static long _recordId;
        private static int _flushedLineCount;
        private static int _flushFailureCount;

        private const int QueueCapacity = 4096;
        private const int FlushIntervalMs = 500;

        internal static bool Enabled { get { return _enabled; } }
        internal static string BattleId { get { return _battleId; } }
        internal static string SessionId { get { return _sessionId; } }
        internal static string EventPath { get { return _eventPath; } }
        internal static int DroppedCount { get { return _dropped; } }

        // Lines confirmed on disk. Named "written" would be wrong: a queued
        // line is not written until the background thread's append returns.
        internal static int WrittenCount { get { return _flushedLineCount; } }
        internal static int FlushFailureCount { get { return _flushFailureCount; } }
        internal static long ProducerSeq { get { return _producerSeq; } }

        // Binds the adapter to the battle CBATH is currently recording.
        //
        // Retries: a mission that starts before CBATH publishes its package
        // keeps looking for the identity file, so a late publish is still
        // picked up rather than lost for the whole battle.
        //
        // Stops: once this battle has been closed the identity file still
        // names it, so it is never re-bound. A battle can only be bound once,
        // and a new mission starts a new binding.
        //
        // Rebinds: while the current battle is open, a different battle_id
        // means the recorder moved on; that is a real switch, not a retry, so
        // it is not rate-limited.
        internal static void Resolve(double missionTime)
        {
            lock (Gate)
            {
                string battleId, sessionId, directory;
                if (!TryReadIdentity(out battleId, out sessionId, out directory))
                {
                    if (!_attempted || ElapsedSinceAttempt(missionTime))
                    {
                        _attempted = true;
                        _lastAttemptAt = missionTime;
                    }
                    return;
                }

                if (_boundMissionTime != double.MinValue)
                {
                    if (string.Equals(battleId, _closedBattleId, StringComparison.Ordinal)) return;
                    if (string.Equals(battleId, _battleId, StringComparison.Ordinal))
                    {
                        if (_enabled) return;
                    }
                }

                if (_boundMissionTime == double.MinValue && _attempted &&
                    missionTime >= _lastAttemptAt && !ElapsedSinceAttempt(missionTime))
                {
                    return;
                }
                _attempted = true;
                _lastAttemptAt = missionTime;

                CloseWriter();
                _closedBattleId = null;
                _battleId = battleId;
                _sessionId = sessionId;
                _directory = directory;
                _eventPath = Path.Combine(directory, "cb2-events.jsonl");
                _enabled = true;
                _boundMissionTime = missionTime;
                OpenWriter();
            }
        }

        private static string _closedBattleId;

        private static bool ElapsedSinceAttempt(double missionTime)
        {
            if (missionTime < _lastAttemptAt) return true;
            return (missionTime - _lastAttemptAt) >= IdentityRetrySeconds;
        }

        private const double IdentityRetrySeconds = 2.0;

        // Reads the identity file. Returns false when the file is absent or
        // does not describe a usable package, which is the normal state when
        // CB2 runs without the observation recorder.
        private static bool TryReadIdentity(out string battleId, out string sessionId, out string directory)
        {
            battleId = null; sessionId = null; directory = null;
            try
            {
                var path = IdentityFilePath();
                if (!File.Exists(path)) return false;
                foreach (var raw in File.ReadAllLines(path))
                {
                    if (raw == null) continue;
                    int eq = raw.IndexOf('=');
                    if (eq <= 0) continue;
                    string key = raw.Substring(0, eq).Trim();
                    string value = raw.Substring(eq + 1).Trim();
                    if (key == "battle_id") battleId = value;
                    else if (key == "session_id") sessionId = value;
                    else if (key == "directory") directory = value;
                }
            }
            catch (Exception)
            {
                return false;
            }
            return !string.IsNullOrEmpty(battleId) &&
                   !string.IsNullOrEmpty(directory) &&
                   Directory.Exists(directory);
        }

        private static string IdentityFilePath()
        {
            return Path.Combine(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                             "CommandBrainATH"),
                "current-battle.txt");
        }

        private static void OpenWriter()
        {
            _producerSeq = 0;
            _recordId = 0;
            _flushedLineCount = 0;
            _flushFailureCount = 0;
            _queue = new BlockingCollection<string>(QueueCapacity);
            _thread = new Thread(Drain) { IsBackground = true, Name = "cb2-observation" };
            _thread.Start();
        }

        // Closing is two steps on purpose. Shutdown stops accepting new lines
        // because the battle is over and the recorder is gone; Complete flushes
        // what is already queued. A caller that has terminating events still to
        // write must emit them before Shutdown, or they are refused rather than
        // queued.
        private static void CloseWriter()
        {
            var q = _queue;
            var t = _thread;
            _queue = null;
            _thread = null;
            if (q != null)
            {
                try { q.CompleteAdding(); } catch (Exception) { }
            }
            if (t != null)
            {
                try { t.Join(1500); } catch (Exception) { }
            }
        }

        internal static void Shutdown()
        {
            lock (Gate)
            {
                _closedBattleId = _battleId;
                _enabled = false;
                CloseWriter();
            }
        }

        // Emits the terminating events and then stops the writer. Callers use
        // this instead of Shutdown when they still own events that must land.
        internal static void ShutdownAfter(Action terminatingEvents)
        {
            lock (Gate)
            {
                if (terminatingEvents != null)
                {
                    try { terminatingEvents(); } catch (Exception) { }
                }
                _closedBattleId = _battleId;
                _enabled = false;
                CloseWriter();
            }
        }

        private static void Drain()
        {
            var buffer = new StringBuilder();
            int bufferedLines = 0;
            long lastFlushTicks = DateTime.UtcNow.Ticks;
            try
            {
                var q = _queue;
                if (q == null) return;
                while (!q.IsCompleted)
                {
                    string line;
                    if (q.TryTake(out line, FlushIntervalMs / 2))
                    {
                        buffer.Append(line).Append('\n');
                        bufferedLines++;
                    }
                    // A quiet battle must still reach disk: without the time
                    // check a low event rate would sit in memory until mission
                    // end, and an abnormal exit would lose all of it.
                    bool due = bufferedLines > 0 &&
                               (DateTime.UtcNow.Ticks - lastFlushTicks) >=
                               TimeSpan.TicksPerMillisecond * FlushIntervalMs;
                    if (buffer.Length >= 16 * 1024 || due)
                    {
                        Flush(buffer, bufferedLines);
                        bufferedLines = 0;
                        lastFlushTicks = DateTime.UtcNow.Ticks;
                    }
                }
            }
            catch (Exception) { }
            finally
            {
                Flush(buffer, bufferedLines);
            }
        }

        private static void Flush(StringBuilder buffer, int lineCount)
        {
            if (buffer.Length == 0) return;
            try
            {
                if (_eventPath == null)
                {
                    Interlocked.Add(ref _dropped, lineCount);
                }
                else
                {
                    File.AppendAllText(_eventPath, buffer.ToString(), NoBomUtf8);
                    // Counted only after the write succeeded: a line that never
                    // reached disk must not be reported as written.
                    Interlocked.Add(ref _flushedLineCount, lineCount);
                }
            }
            catch (Exception)
            {
                Interlocked.Increment(ref _flushFailureCount);
                Interlocked.Add(ref _dropped, lineCount);
            }
            buffer.Length = 0;
        }

        // Records one decision-chain event. Returns immediately.
        //
        // kind        stable event name, e.g. "plan.created"
        // missionTime mission CurrentTime, the shared alignment key
        // fields      pre-rendered JSON members, or null
        internal static void Emit(string kind, double missionTime, string fields)
        {
            if (!_enabled) return;
            var q = _queue;
            if (q == null) return;

            try
            {
                long seq = Interlocked.Increment(ref _producerSeq);
                long record = Interlocked.Increment(ref _recordId);
                var sb = new StringBuilder(256);
                sb.Append('{');
                sb.Append("\"kind\":").Append(Json.Str(kind));
                sb.Append(",\"battle_id\":").Append(Json.Str(_battleId));
                sb.Append(",\"session_id\":").Append(Json.Str(_sessionId));
                sb.Append(",\"source\":\"cb2\"");
                sb.Append(",\"producer_seq\":").Append(Json.Num(seq));
                sb.Append(",\"record_id\":").Append(Json.Num(record));
                sb.Append(",\"mission_time\":").Append(Json.Num(missionTime));
                sb.Append(",\"wall_clock_utc\":").Append(Json.Str(
                    DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture)));
                if (!string.IsNullOrEmpty(fields))
                {
                    sb.Append(',').Append(fields);
                }
                sb.Append('}');

                if (!q.TryAdd(sb.ToString())) Interlocked.Increment(ref _dropped);
            }
            catch (Exception) { }
        }
    }

    // Minimal JSON string/number rendering shared by the adapter.
    internal static class Json
    {
        internal static string Str(string s)
        {
            if (s == null) return "null";
            var sb = new StringBuilder(s.Length + 8);
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        internal static string Num(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "null";
            return v.ToString("R", CultureInfo.InvariantCulture);
        }

        internal static string Num(int v) { return v.ToString(CultureInfo.InvariantCulture); }

        internal static string Bool(bool v) { return v ? "true" : "false"; }
    }

    // Builds the "extra fields" fragment passed to Emit.
    internal sealed class EventFields
    {
        private readonly StringBuilder _sb = new StringBuilder(256);

        internal EventFields Add(string key, string value)
        {
            Sep();
            _sb.Append(Json.Str(key)).Append(':').Append(Json.Str(value));
            return this;
        }

        internal EventFields Add(string key, int value)
        {
            Sep();
            _sb.Append(Json.Str(key)).Append(':').Append(Json.Num(value));
            return this;
        }

        internal EventFields Add(string key, double value)
        {
            Sep();
            _sb.Append(Json.Str(key)).Append(':').Append(Json.Num(value));
            return this;
        }

        internal EventFields Add(string key, bool value)
        {
            Sep();
            _sb.Append(Json.Str(key)).Append(':').Append(Json.Bool(value));
            return this;
        }

        internal EventFields AddRaw(string key, string token)
        {
            Sep();
            _sb.Append(Json.Str(key)).Append(':').Append(token ?? "null");
            return this;
        }

        private void Sep()
        {
            if (_sb.Length > 0) _sb.Append(',');
        }

        public override string ToString() { return _sb.ToString(); }
    }
}
