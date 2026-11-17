using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

#if ADMIN_EDITION
[assembly: AssemblyTitle("RX 工具箱（管理版）")]
[assembly: AssemblyProduct("RX 工具箱（管理版）")]
[assembly: AssemblyDescription("GMod 服务器指令速查与辅助工具（管理版）")]
#else
[assembly: AssemblyTitle("RX 工具箱")]
[assembly: AssemblyProduct("RX 工具箱")]
[assembly: AssemblyDescription("GMod 挤服助手 + 屏幕准星 + 对局倒计时 + 自定义常用文本")]
#endif
[assembly: AssemblyCompany("RXToolbox")]
[assembly: AssemblyVersion("1.8.2.0")]
[assembly: AssemblyFileVersion("1.8.2.0")]

static class Program
{
    public const string TARGET_EXE_NAME = "Rx工具箱.exe";
    static bool g_dev;      // --dev 启动：多实例 + 标题带 DEV
    static string g_migratedFrom = "";
    public static bool IsDev { get { return g_dev; } }
    public static string MigratedFrom { get { return g_migratedFrom; } }

    public static bool ShouldMigrateLegacyExeName(string fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return false;
        return Regex.IsMatch(fileName.Trim(), @"^RX工具箱\s*V\d+(?:\.\d+)*\.exe$", RegexOptions.IgnoreCase);
    }

    public static string ParseMigratedFromArg(string[] args)
    {
        if (args == null) return "";
        for (int i = 0; i + 1 < args.Length; i++)
        {
            if (string.Equals(args[i], "--migrated-from", StringComparison.OrdinalIgnoreCase))
                return (args[i + 1] ?? "").Trim();
        }
        return "";
    }

    public static bool IsRxToolboxProcessName(string processName)
    {
        if (string.IsNullOrEmpty(processName)) return false;
        return processName.StartsWith("RX工具箱", StringComparison.OrdinalIgnoreCase)
            || processName.StartsWith("RX 工具箱", StringComparison.OrdinalIgnoreCase);
    }

    public static int ParseAfterUpdatePid(string[] args)
    {
        if (args == null) return 0;
        for (int i = 0; i + 1 < args.Length; i++)
        {
            if (string.Equals(args[i], "--after-update", StringComparison.OrdinalIgnoreCase))
            {
                int pid;
                if (int.TryParse(args[i + 1], out pid) && pid > 0) return pid;
            }
        }
        return 0;
    }

    static void WaitForOldProcessExit(int oldPid, int timeoutMs)
    {
        if (oldPid <= 0) return;
        try
        {
            using (var cur = System.Diagnostics.Process.GetCurrentProcess())
            using (var p = System.Diagnostics.Process.GetProcessById(oldPid))
            {
                if (p.Id == cur.Id) return;
                if ((string.Equals(p.ProcessName, cur.ProcessName, StringComparison.OrdinalIgnoreCase) || IsRxToolboxProcessName(p.ProcessName)) && !p.HasExited)
                    p.WaitForExit(timeoutMs);
            }
        }
        catch { }
    }

    static bool TryMigrateLegacyExeName(int selfPid)
    {
        try
        {
            Assembly asm = Assembly.GetEntryAssembly();
            string exePath = (asm != null && !string.IsNullOrEmpty(asm.Location))
                ? Path.GetFullPath(asm.Location)
                : Path.GetFullPath(Environment.GetCommandLineArgs()[0]);
            string oldName = Path.GetFileName(exePath);
            if (!ShouldMigrateLegacyExeName(oldName)) return false;

            string dir = Path.GetDirectoryName(exePath);
            if (string.IsNullOrEmpty(dir) || !MainForm.CanWriteDir(dir)) return false;

            string targetExe = Path.Combine(dir, TARGET_EXE_NAME);
            if (string.Equals(exePath, targetExe, StringComparison.OrdinalIgnoreCase)) return false;

            MainForm.CleanupUpdateArtifacts(exePath, "", false);

            string backedTarget = "";
            if (File.Exists(targetExe))
            {
                backedTarget = MainForm.PickBackupPath(targetExe);
                MainForm.MoveWithRetry(targetExe, backedTarget, 3);
            }

            try
            {
                MainForm.MoveWithRetry(exePath, targetExe, 4);
                var psi = new System.Diagnostics.ProcessStartInfo();
                psi.FileName = targetExe;
                psi.Arguments = "--after-update " + selfPid + " --migrated-from \"" + oldName.Replace("\"", "") + "\"";
                psi.WorkingDirectory = dir;
                psi.UseShellExecute = false;
                using (var launched = System.Diagnostics.Process.Start(psi)) { }
                return true;
            }
            catch
            {
                try
                {
                    if (!File.Exists(exePath) && File.Exists(targetExe))
                        MainForm.MoveWithRetry(targetExe, exePath, 3);
                    if (!string.IsNullOrEmpty(backedTarget) && File.Exists(backedTarget) && !File.Exists(targetExe))
                        MainForm.MoveWithRetry(backedTarget, targetExe, 3);
                }
                catch { }
                return false;
            }
        }
        catch
        {
            return false;
        }
    }

    [STAThread]
    static void Main()
    {
        // 必须在任何网络请求之前：.NET Framework 默认不启用 TLS 1.2+，
        // 会出现「未能创建 SSL/TLS 安全通道」，导致首次运行拉数据失败
        try
        {
            System.Net.ServicePointManager.SecurityProtocol |=
                (System.Net.SecurityProtocolType)3072 | (System.Net.SecurityProtocolType)12288;
        }
        catch { }

        // 开发用：自检模式，不建窗口，只跑断言并写 selftest.log
        if (Environment.GetEnvironmentVariable("RX_SELFTEST") == "1")
        {
            int fail = MainForm.SelfTest();
            Environment.Exit(fail == 0 ? 0 : 1);
            return;
        }
        string[] args = Environment.GetCommandLineArgs();
        int selfPid = System.Diagnostics.Process.GetCurrentProcess().Id;
        int afterUpdatePid = ParseAfterUpdatePid(args);
        g_migratedFrom = ParseMigratedFromArg(args);
        if (afterUpdatePid == selfPid) afterUpdatePid = 0;
        if (afterUpdatePid > 0) WaitForOldProcessExit(afterUpdatePid, 15000);

        bool createdNew;
        string mutexName = @"Local\RXToolbox_SingleInstance_Mutex";
#if ADMIN_EDITION
        mutexName += "_Admin";
#endif
        // 开发用：--dev 或 RX_MULTI=1 可以让多个实例并存（正式版开着也能测 DEV 版）
        bool isDev = Environment.GetEnvironmentVariable("RX_MULTI") == "1";
        foreach (string a in args)
        {
            if (string.Equals(a, "--dev", StringComparison.OrdinalIgnoreCase)) { isDev = true; break; }
        }
        g_dev = isDev;
        if (isDev)
            mutexName += "_" + selfPid;
        using (var mutex = new Mutex(true, mutexName, out createdNew))
        {
            bool ownsMutex = createdNew;
            if (!ownsMutex && afterUpdatePid > 0)
            {
                try { ownsMutex = mutex.WaitOne(5000); }
                catch (AbandonedMutexException) { ownsMutex = true; }
            }
            if (!ownsMutex)
            {
                if (!Native.ActivateExistingInstance())
                {
                    MessageBox.Show("RX 工具箱已在运行中。", "RX 工具箱", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                return;
            }
#if ADMIN_EDITION
            if (!isDev && string.IsNullOrEmpty(g_migratedFrom) && Environment.GetEnvironmentVariable("RX_DUMP_UI") != "1")
            {
                if (TryMigrateLegacyExeName(selfPid))
                {
                    if (ownsMutex)
                    {
                        try { mutex.ReleaseMutex(); } catch { }
                        ownsMutex = false;
                    }
                    return;
                }
            }
#endif
            try
            {
                RunApp(afterUpdatePid > 0);
            }
            finally
            {
                if (ownsMutex)
                {
                    try { mutex.ReleaseMutex(); } catch { }
                }
                GC.KeepAlive(mutex);
            }
        }
    }

#if ADMIN_EDITION
    // 数据文件的地址：优先用 settings.ini 里 UpdateUrl 同目录的 phrases.json
    static string DeriveDataUrl(string dataDir)
    {
        const string Default = "https://rx-toolbox.pages.dev/phrases.json";
        try
        {
            string ini = Path.Combine(dataDir, "settings.ini");
            if (File.Exists(ini))
            {
                foreach (string line in File.ReadAllLines(ini))
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    if (line.Substring(0, eq).Trim() != "UpdateUrl") continue;
                    string v = line.Substring(eq + 1).Trim();
                    if (v.Length == 0) break;
                    int slash = v.LastIndexOf('/');
                    if (slash <= 0) break;
                    return v.Substring(0, slash + 1) + "phrases.json";
                }
            }
        }
        catch { }
        return Default;
    }
#endif

    static void RunApp(bool afterUpdate)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e)
        {
            if (e != null && e.Exception != null)
                Native.Log("UI ThreadException: " + e.Exception);
        };
        AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
        {
            if (e != null && e.ExceptionObject != null)
                Native.Log("Domain UnhandledException: " + e.ExceptionObject);
        };

        string exeDir = AppDomain.CurrentDomain.BaseDirectory;
        string dataDir = exeDir;

        // 程序目录不可写（例如装在 Program Files）时回退到 %LOCALAPPDATA%
        try
        {
            string probe = Path.Combine(exeDir, ".wtest_" + Guid.NewGuid().ToString("N"));
            File.WriteAllText(probe, "x");
            File.Delete(probe);
        }
        catch
        {
            dataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RXToolbox");
            try { Directory.CreateDirectory(dataDir); } catch { }
        }

        Native.LogPath = Path.Combine(dataDir, "rx.log");
#if ADMIN_EDITION
        MainForm.CleanupUpdateArtifacts(Application.ExecutablePath, Path.Combine(dataDir, "phrases.json"), afterUpdate);
        if (!string.Equals(dataDir, exeDir, StringComparison.OrdinalIgnoreCase))
            MainForm.CleanupUpdateArtifacts(Application.ExecutablePath, Path.Combine(exeDir, "phrases.json"), false);

        // 数据从 phrases.json 读（不再编进 exe）：先找数据目录，再退回 exe 目录
        string phrases = Path.Combine(dataDir, "phrases.json");
        if (!MainForm.Data.Load(phrases) && !string.Equals(dataDir, exeDir, StringComparison.OrdinalIgnoreCase))
            MainForm.Data.Load(Path.Combine(exeDir, "phrases.json"));
        if (!MainForm.Data.Loaded)
        {
            // 从 1.7.0 升级上来的用户：老版本的更新器不会下载数据文件，
            // 所以这里自愈一次，把 phrases.json 拉下来
            Native.Log("data: 没找到 phrases.json，尝试自动获取");
            string dataUrl = DeriveDataUrl(dataDir);
            if (!string.IsNullOrEmpty(dataUrl) &&
                (dataUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                 dataUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)))
            {
                string target = Path.Combine(dataDir, "phrases.json");
                try
                {
                    var req = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(dataUrl);
                    req.Timeout = 6000;
                    req.ReadWriteTimeout = 6000;
                    req.Proxy = null;
                    req.UserAgent = "RXToolbox-FirstRun";
                    using (var resp = (System.Net.HttpWebResponse)req.GetResponse())
                    using (var st = resp.GetResponseStream())
                    using (var fs = File.Create(target))
                    {
                        st.CopyTo(fs);
                    }
                    if (new FileInfo(target).Length > 1024)
                    {
                        MainForm.Data.Load(target);
                        Native.Log("data: 自动下载成功 " + target);
                    }
                    else
                    {
                        try { File.Delete(target); } catch { }
                    }
                }
                catch (Exception ex) { Native.Log("data: 自动下载失败 " + ex.Message); }
            }
        }
        if (!MainForm.Data.Loaded)
            Native.Log("data: 仍然没有数据（界面会是空表，可重新下载完整压缩包）");
        else
            Native.Log("data: " + MainForm.Data.SourcePath);
#else
        MainForm.CleanupUpdateArtifacts(Application.ExecutablePath, "", afterUpdate);
#endif

        var titles = new List<string>();
        var lists = new List<string[]>();

#if ADMIN_EDITION
        titles.Add("指令表"); lists.Add(MainForm.Data.Command);
        titles.Add("技能表"); lists.Add(MainForm.Data.Ability);
        titles.Add("物品表"); lists.Add(MainForm.Data.Weapon);
        titles.Add("模型表"); lists.Add(MainForm.Data.Model);
        titles.Add("角色表"); lists.Add(MainForm.Data.Job);
        titles.Add("SCP角色"); lists.Add(MainForm.Data.Scp);
#endif

        string icoPath = Path.Combine(exeDir, "rx.ico");
        MainForm form = null;
        try
        {
            form = new MainForm(icoPath, titles.ToArray(), lists.ToArray(), dataDir);
        }
        catch (Exception ex)
        {
            Native.Log("startup EX: " + ex);
            MessageBox.Show("启动失败：" + ex.Message, "RX 工具箱", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        form.Text = "RX 工具箱 " + MainForm.APP_VERSION + MainForm.APP_EDITION_SUFFIX + (g_dev ? " · DEV" : "");

        Application.Run(form);
    }
}
