using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace CombatBrain2.Core
{
    // 2.0 自己的日志。固定在 Documents\CombatBrain2\CombatBrain2.log。
    // 与旧 CombatBrain 的日志路径完全分离。
    internal static class Cb2Log
    {
        private static readonly object Gate = new object();
        private static string _path;
        private static bool _resolved;

        internal static string Path_
        {
            get
            {
                if (!_resolved)
                {
                    _resolved = true;
                    try
                    {
                        var dir = System.IO.Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                            "CombatBrain2");
                        Directory.CreateDirectory(dir);
                        _path = System.IO.Path.Combine(dir, "CombatBrain2.log");
                    }
                    catch (Exception)
                    {
                        _path = null;
                    }
                }
                return _path;
            }
        }

        internal static void Line(string marker, string message)
        {
            var text = "[CB2][" + marker + "] " + message;
            lock (Gate)
            {
                try { TaleWorlds.Library.Debug.Print(text); } catch (Exception) { }
                try { Console.WriteLine(text); } catch (Exception) { }
                var p = Path_;
                if (p == null) return;
                try { File.AppendAllText(p, text + "\n", Encoding.UTF8); } catch (Exception) { }
            }
        }

        internal static string F(double v)
        {
            return v.ToString("F3", CultureInfo.InvariantCulture);
        }

        internal static string I(int v)
        {
            return v.ToString(CultureInfo.InvariantCulture);
        }
    }
}
