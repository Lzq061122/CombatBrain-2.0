using System;
using System.Globalization;
using System.IO;
using System.Text;
using CombatBrain2.Core;
using TaleWorlds.MountAndBlade;

namespace CombatBrain2.Host
{
    // 运行期身份：从本次运行实际取，不再用磁盘时间戳或旧日志代替。
    internal static class RuntimeIdentity
    {
        internal static void Emit()
        {
            var sb = new StringBuilder();
            sb.Append("module=CombatBrain2");
            sb.Append(" build_id=").Append(BuildIdentity.BuildId);
            sb.Append(" assembly_version=").Append(Version());
            sb.Append(" mvid=").Append(Mvid());
            sb.Append(" assembly_location=").Append(Location());
            sb.Append(" assembly_file_sha256=").Append(FileSha256());
            sb.Append(" game_version=").Append(GameVersion());
            sb.Append(" pid=").Append(Pid());
            sb.Append(" active_modules=").Append(ActiveModules());
            Cb2Log.Line("RUNTIME_IDENTITY", sb.ToString());
        }

        internal static string Version()
        {
            try { return typeof(RuntimeIdentity).Assembly.GetName().Version.ToString(); }
            catch (Exception e) { return "<err:" + e.GetType().Name + ">"; }
        }

        internal static string Pid()
        {
            try { return System.Diagnostics.Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture); }
            catch (Exception e) { return "<err:" + e.GetType().Name + ">"; }
        }

        // 编译器为每个程序集写入的模块版本 id。GuidAttribute 不会自动生成，
        // 读它只会得到 "<none>"；MVID 必须从 ManifestModule 取。
        internal static string Mvid()
        {
            try { return typeof(RuntimeIdentity).Assembly.ManifestModule.ModuleVersionId.ToString(); }
            catch (Exception e) { return "<err:" + e.GetType().Name + ">"; }
        }

        internal static string Location()
        {
            try { return typeof(RuntimeIdentity).Assembly.Location; }
            catch (Exception e) { return "<err:" + e.GetType().Name + ">"; }
        }

        // 加载位置上那个文件的 SHA256。这是磁盘文件摘要，
        // 不等于进程内存里的字节，不能用来宣称两者相同。
        internal static string FileSha256()
        {
            try
            {
                var p = typeof(RuntimeIdentity).Assembly.Location;
                if (string.IsNullOrEmpty(p) || !File.Exists(p)) return "<no_file>";
                using (var sha = System.Security.Cryptography.SHA256.Create())
                using (var fs = File.OpenRead(p))
                {
                    var hash = sha.ComputeHash(fs);
                    var sb = new StringBuilder(hash.Length * 2);
                    foreach (var b in hash) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                    return sb.ToString();
                }
            }
            catch (Exception e) { return "<err:" + e.GetType().Name + ">"; }
        }

        internal static string GameVersion()
        {
            try { return TaleWorlds.Library.ApplicationVersion.FromString("v1.4.8").ToString(); }
            catch (Exception) { return "<unavailable>"; }
        }

        internal static string ActiveModules()
        {
            try
            {
                var mods = TaleWorlds.ModuleManager.ModuleHelper.GetActiveModules();
                var sb = new StringBuilder();
                bool first = true;
                foreach (var m in mods)
                {
                    if (!first) sb.Append(",");
                    first = false;
                    sb.Append(m.Name);
                }
                return sb.Length == 0 ? "<none>" : sb.ToString();
            }
            catch (Exception e) { return "<err:" + e.GetType().Name + ">"; }
        }
    }
}
