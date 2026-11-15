using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

#if ADMIN_EDITION
[assembly: AssemblyTitle("RX 工具箱")]
[assembly: AssemblyProduct("RX 工具箱")]
#else
[assembly: AssemblyTitle("RX 工具箱")]
[assembly: AssemblyProduct("RX 工具箱")]
#endif
[assembly: AssemblyDescription("GMod 指令/模型/职业速查 + 挤服 + 准星")]
[assembly: AssemblyCompany("RXSEND Breach mio")]
[assembly: AssemblyVersion("1.7.0.0")]
[assembly: AssemblyFileVersion("1.7.0.0")]

static class Program
{
    static bool g_dev;      // --dev 启动：多实例 + 标题带 DEV
    [STAThread]
    static void Main()
    {
        // 开发用：自检模式，不建窗口，只跑断言并写 selftest.log
        if (Environment.GetEnvironmentVariable("RX_SELFTEST") == "1")
        {
            int fail = MainForm.SelfTest();
            Environment.Exit(fail == 0 ? 0 : 1);
            return;
        }
        bool createdNew;
        string mutexName = @"Local\RXToolbox_SingleInstance_Mutex";
#if ADMIN_EDITION
        mutexName += "_Admin";
#endif
        // 开发用：--dev 或 RX_MULTI=1 可以让多个实例并存（正式版开着也能测 DEV 版）
        bool isDev = Environment.GetEnvironmentVariable("RX_MULTI") == "1";
        foreach (string a in Environment.GetCommandLineArgs())
        {
            if (string.Equals(a, "--dev", StringComparison.OrdinalIgnoreCase)) { isDev = true; break; }
        }
        g_dev = isDev;
        if (isDev)
            mutexName += "_" + System.Diagnostics.Process.GetCurrentProcess().Id;
        using (var mutex = new Mutex(true, mutexName, out createdNew))
        {
            if (!createdNew)
            {
                if (!Native.ActivateExistingInstance())
                {
                    MessageBox.Show("RX 工具箱已在运行中。", "RX 工具箱", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                return;
            }
            try
            {
                RunApp();
            }
            finally
            {
                try { mutex.ReleaseMutex(); } catch { }
                GC.KeepAlive(mutex);
            }
        }
    }

    static void RunApp()
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

        // 数据从 phrases.json 读（不再编进 exe）：先找数据目录，再退回 exe 目录
        string phrases = Path.Combine(dataDir, "phrases.json");
        if (!MainForm.Data.Load(phrases) && !string.Equals(dataDir, exeDir, StringComparison.OrdinalIgnoreCase))
            MainForm.Data.Load(Path.Combine(exeDir, "phrases.json"));
        if (!MainForm.Data.Loaded)
            Native.Log("data: 没找到 phrases.json（界面会是空表）");
        else
            Native.Log("data: " + MainForm.Data.SourcePath);

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

        form.Text = "RX 工具箱 " + MainForm.APP_VERSION + (g_dev ? " · DEV" : "");

        Application.Run(form);
    }
}
