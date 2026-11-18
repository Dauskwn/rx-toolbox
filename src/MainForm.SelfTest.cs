using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using WSize = Windows.Foundation.Size;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

public partial class MainForm
{
    void DumpUi()
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("RX 工具箱 UI dump  " + APP_VERSION + "  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("title " + Text);
            sb.AppendLine("form " + ClientSize.Width + "x" + ClientSize.Height);
            for (int t = 0; t < tabs.TabPages.Count; t++)
            {
                TabPage pg = tabs.TabPages[t];
                sb.AppendLine();
                sb.AppendLine("[tab " + t + "] " + pg.Text + "  (" + pg.Controls.Count + " 个直接子控件)");
                DumpControls(sb, pg, "  ", 0);
            }
            string dir = Path.GetDirectoryName(Application.ExecutablePath);
            if (!CanWriteDir(dir))
            {
                dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RXToolbox");
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(Path.Combine(dir, "dump-ui.log"), sb.ToString(), new UTF8Encoding(true));
        }
        catch { }
    }

    static void DumpControls(StringBuilder sb, Control parent, string indent, int depth)
    {
        if (depth > 3) return;
        foreach (Control c in parent.Controls)
        {
            string text = c.Text ?? "";
            if (text.Length > 40) text = text.Substring(0, 40) + "…";
            text = text.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");
            sb.Append(indent).Append(c.GetType().Name).Append(" \"").Append(text).Append("\"");
            sb.Append("  x=").Append(c.Left).Append(" y=").Append(c.Top);
            sb.Append(" w=").Append(c.Width).Append(" h=").Append(c.Height);
            sb.Append(" vis=").Append(c.Visible ? 1 : 0);
            if (!c.Enabled) sb.Append(" disabled");
            sb.AppendLine();
            if (c.Controls.Count > 0) DumpControls(sb, c, indent + "  ", depth + 1);
        }
    }

    public static int SelfTest()
    {
        int pass = 0, fail = 0;
        var sb = new StringBuilder();
#if ADMIN_EDITION
        // 数据在 exe 旁边就先生效，方便跑数据体检
        try
        {
            string d = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "phrases.json");
            Data.Load(d);
        }
        catch { }
        sb.AppendLine("RX 工具箱自检 " + APP_VERSION + " " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        sb.AppendLine("数据：" + (Data.Loaded ? Data.SourcePath : "未找到 phrases.json（数据相关断言会跳过）"));
#else
        sb.AppendLine("RX 工具箱自检 " + APP_VERSION + " " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
#endif
        sb.AppendLine();

        Action<string, bool, string> chk = delegate(string name, bool ok, string detail)
        {
            if (ok) { pass++; sb.AppendLine("  PASS  " + name); }
            else { fail++; sb.AppendLine("  FAIL  " + name + "    " + detail); }
        };

#if ADMIN_EDITION
        // ---- 1. 条目编码/解析往返 ----
        string[] samples = new string[]
        {
            DevJoinItem("普通", "!a ^", "", "", ""),
            DevJoinItem("只有注解", "!b ^", "", "", "这是注解"),
            DevJoinItem("带备用", "!c ^", "!devforcespawn ^ c", "", ""),
            DevJoinItem("备用加注解", "!d ^", "!dev ^ d", "", "注解D"),
            DevJoinItem("带ulx", "!e ^", "", "ulx e ^", ""),
            DevJoinItem("三样都有", "!f ^", "!dev ^ f", "ulx f ^", "注解F"),
        };
        foreach (string item in samples)
        {
            string t0, m0, a0, u0, n0;
            DevSplitItem(item, out t0, out m0, out a0, out u0, out n0);
            string again = DevJoinItem(t0, m0, a0, u0, n0);
            chk("编码往返 " + t0, again == item, "期望 " + Esc0(item) + " 实际 " + Esc0(again));
            chk("注解不串行 " + t0,
                m0.IndexOf('\u0003') < 0 && m0.IndexOf('\u0004') < 0 && m0.IndexOf('\u0002') < 0,
                "主指令 = " + Esc0(m0));
        }

        // ---- 2. 快照解析 ----
        string snap = "# 注释\r\n【指令表】\r\n标题\t主指令\t备用\tulx\t备注\r\n" +
                      "分组\t\t\t\t\r\n甲\t!x ^\t\t\t注解甲\r\n乙\t!y ^\t!dev ^ y\tulx y ^\t\r\n" +
                      "【物品表】\r\n标题\t主指令\t备用\tulx\t备注\r\n丙\t!giveweapon ^ w\t\t\t\r\n";
        string meta;
        var parsed = ParseDataSnapshot(snap, out meta);
        chk("快照表数=2", parsed.Count == 2, "实际 " + parsed.Count);
        chk("指令表 3 条", parsed.ContainsKey("指令表") && parsed["指令表"].Count == 3,
            parsed.ContainsKey("指令表") ? parsed["指令表"].Count.ToString() : "缺表");

        // ---- 3. ulx 推导 ----
        chk("ulx: !x ^", UlxOf("!x ^", "") == "ulx x ^", UlxOf("!x ^", ""));
        chk("ulx: 已有写法优先", UlxOf("!x ^", "ulx force_spawn ^ X") == "ulx force_spawn ^ X", "");
        chk("ulx: 非 ! 命令不变", UlxOf("stopsound", "") == "stopsound", UlxOf("stopsound", ""));
        chk("ulx: 已是 ulx 不变", UlxOf("ulx luarun X()", "") == "ulx luarun X()", "");
#endif

        // ---- 4. JSON 转义 ----
        string raw = "引号\"反斜杠\\换行\n制表\t控制\u000b\u000c中文";
        string esc = JsonEsc(raw);
        chk("JSON 往返", JsonUnesc(esc) == raw && esc.IndexOf("\\u000b", StringComparison.Ordinal) >= 0, "得到 " + Esc0(JsonUnesc(esc)));
        chk("JSON 字段读取", JsonField("{\"a\":\"x\\ny\",\"b\":2}", "a") == "x\ny", "");
        string pathSnap = "C:\\Games\\SteamLibrary\\steamapps\\common\\GarrysMod\\garrysmod\\addons\\sample\\file.txt";
        string pathJson = "{\"ok\":true,\"data\":\"" + JsonEsc(pathSnap) + "\"}";
        string pathRead = JsonField(pathJson, "data");
        chk("JSON 反斜杠路径不二次反转义", pathRead == pathSnap, "得到 " + Esc0(pathRead));

        // ---- 5. 版本比较 ----
        chk("版本 1.6.1 > 1.6.0", CompareVersion("1.6.1", "1.6.0") > 0, "");
        chk("版本 1.6.10 > 1.6.9", CompareVersion("1.6.10", "1.6.9") > 0, "");
        chk("版本 1.6.0 == 1.6.0", CompareVersion("1.6.0", "1.6.0") == 0, "");
        chk("版本 1.5.9 < 1.6.0", CompareVersion("1.5.9", "1.6.0") < 0, "");

#if ADMIN_EDITION
                // ---- 5.4 目标名替换 ----
        chk("目标名：普通替换", ApplyTargetName("!strip ^", "暮嵐") == "!strip 暮嵐", ApplyTargetName("!strip ^", "暮嵐"));
        chk("目标名：含空格加引号", ApplyTargetName("!strip ^", "John Smith") == "!strip \"John Smith\"", ApplyTargetName("!strip ^", "John Smith"));
        chk("目标名：已带引号不重复加", ApplyTargetName("!strip ^", "\"A B\"") == "!strip \"A B\"", "");
        chk("目标名：留空不动", ApplyTargetName("!strip ^", "  ") == "!strip ^", "");
        chk("目标名：没有 ^ 不动", ApplyTargetName("stopsound", "X") == "stopsound", "");
        chk("目标名：多个 ^ 全部替换", ApplyTargetName("!cmd ^ ^", "X") == "!cmd X X", ApplyTargetName("!cmd ^ ^", "X"));

        // ---- 5.5 条目编辑器（PromptItem 走 DevSplitItem/DevJoinItem）----
                string editRaw = "带注解条目" + "\u0001" + "!cmd ^" + "\u0002" + "\u0003" + "这是注解";
                string et, em, ea, eu, en;
                DevSplitItem(editRaw, out et, out em, out ea, out eu, out en);
                chk("编辑器：读得到注解", en == "这是注解", "得到 " + Esc0(en));
                chk("编辑器：命令没被注解污染", em == "!cmd ^", "得到 " + Esc0(em));
                chk("编辑器：原样存回", DevJoinItem(et, em, ea, eu, en) == editRaw, "不一致");
                string dirtyJoined = DevJoinItem("标\u0003题", "指\u0004令", "备\u0001用", "ulx\u0002x", "注\u0003解");
                string ct0, cm0, ca0, cu0, cn0;
                DevSplitItem(dirtyJoined, out ct0, out cm0, out ca0, out cu0, out cn0);
                chk("CleanCtrl：过滤 \\u0001~\\u0004 防串列",
                    ct0 == "标 题" && cm0 == "指 令" && ca0 == "备 用" && cu0 == "ulx x" && cn0 == "注 解",
                    Esc0(dirtyJoined));

                // ---- 5.6 data-custom.txt 转义往返 ----
                chk("持久化转义往返", UnescapeItem(EscapeItem(editRaw)) == editRaw, "不一致");

        // ---- 6. 内置数据体检 ----
        int badCmd = 0, badUlx = 0, emptyTitle = 0, total = 0;
        var tables = new Dictionary<string, string[]>();
        tables["指令表"] = Data.Command;
        tables["技能表"] = Data.Ability;
        tables["物品表"] = Data.Weapon;
        tables["模型表"] = Data.Model;
        tables["角色表"] = Data.Job;
        tables["SCP角色"] = Data.Scp;
        foreach (var kv in tables)
        {
            foreach (string item in kv.Value)
            {
                total++;
                string t0, m0, a0, u0, n0;
                DevSplitItem(item, out t0, out m0, out a0, out u0, out n0);
                if (t0.Trim().Length == 0) emptyTitle++;
                if (m0.IndexOf('\u0003') >= 0 || m0.IndexOf('\u0002') >= 0 || m0.IndexOf('\u0004') >= 0) badCmd++;
                if (!string.IsNullOrEmpty(u0) && !u0.StartsWith("ulx", StringComparison.OrdinalIgnoreCase)) badUlx++;
            }
        }
        if (!Data.Loaded || total == 0)
        {
            chk("数据体检（无数据文件，跳过）", true, "");
        }
        else
        {
            chk("数据总条数 > 0", total > 0, "实际 " + total);
        }
        if (Data.Loaded && total > 0)
        {
            chk("没有空标题", emptyTitle == 0, "空标题 " + emptyTitle + " 条");
            chk("指令里没有残留分隔符", badCmd == 0, badCmd + " 条");
            chk("显式 ulx 都以 ulx 开头", badUlx == 0, badUlx + " 条");
        }
#endif

        // ---- 自动更新：下载 + 校验（用本地 file:// 真实走一遍） ----
        try
        {
            string tmpDir = Path.Combine(Path.GetTempPath(), "rx_selftest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tmpDir);
            string src = Path.Combine(tmpDir, "payload.bin");
            string payload = "RX toolbox data payload " + DateTime.Now.Ticks;
            File.WriteAllText(src, payload);
            string goodSha = Sha256File(src);
            string dest = Path.Combine(tmpDir, "got.bin");
            string fileUrl = new Uri(src).AbsoluteUri;

            chk("更新下载 + SHA256 校验通过", DownloadAndVerify(fileUrl, goodSha, dest) && File.Exists(dest), "");
            chk("更新下载：哈希不对时拒绝", !DownloadAndVerify(fileUrl, new string('0', 64), Path.Combine(tmpDir, "bad.bin")), "");
            chk("更新下载：空哈希时拒绝", !DownloadAndVerify(fileUrl, "", Path.Combine(tmpDir, "emptysha.bin")), "");
            chk("更新下载：拒绝非本机 http://，允许 https:// 与本机回环端口",
                !IsHttpsUrl("http://rx-toolbox.pages.dev/a.exe")
                && IsHttpsUrl("https://rx-toolbox.pages.dev/a.exe")
                && !IsAllowedUpdateUrl("http://example.com/a.exe")
                && !IsAllowedUpdateUrl("http://192.168.1.10/a.exe")
                && IsAllowedUpdateUrl("http://127.0.0.1:18080/version.json")
                && IsAllowedUpdateUrl("http://localhost:18080/a.exe")
                && !DownloadAndVerify("http://example.com/a.exe", goodSha, Path.Combine(tmpDir, "http.bin")), "");
#if ADMIN_EDITION
            chk("MiniJson：损坏输入不死循环", MiniJson.Parse("{bad:123,@}") != null, "");
            chk("MiniJson：超深嵌套安全拦截不栈溢出",
                Data.ParseTables(new string('[', 120) + new string(']', 120)) == null, "");
            chk("版本库错误提示：识别 401 unauthorized",
                HubErrorText(new Exception("HTTP 401: unauthorized")).IndexOf("密钥不对", StringComparison.Ordinal) >= 0, "");
            string mlSnap = "【指令表】\r\n标题\t主指令\t备用\tulx\t备注\r\n" +
                            "多行项\t" + DevCell("say hi\nsay\tbye") + "\t\t\t" + DevCell("带说明") + "\r\n";
            string mlMeta;
            var mlParsed = ParseDataSnapshot(mlSnap, out mlMeta);
            string mlT = "", mlM = "", mlA = "", mlU = "", mlN = "";
            if (mlParsed.ContainsKey("指令表") && mlParsed["指令表"].Count == 1)
                DevSplitItem(mlParsed["指令表"][0], out mlT, out mlM, out mlA, out mlU, out mlN);
            chk("快照多行与制表符无损往返", mlM.Contains("\n") && mlM.Contains("say\tbye") && mlN == "带说明", Esc0(mlM));
            var hubRows = ParseHubList("{\"ok\":true,\"items\":[{\"id\":\"2026-ab\",\"ts\":\"2026-11-16T12:00:00Z\",\"author\":\"王月\",\"note\":\"含{花括号}备注\",\"size\":2048}]}");
            chk("版本库列表：备注含花括号不丢失", hubRows.Count == 1 && hubRows[0][2] == "含{花括号}备注", "");
            chk("ulx 名字映射（forcespawn → force_spawn）",
                UlxOf("!forcespawn ^ x", "") == "ulx force_spawn ^ x"
                && UlxOf("!devforcespawn ^ x", "") == "ulx dev_force_spawn ^ x"
                && UlxOf("!forcescp ^ x", "") == "ulx force_scp ^ x"
                && UlxOf("!forceround", "") == "ulx forceround", "");
#endif

            chk("倒计时格式化 MM:SS",
                CountdownOverlay.FormatMmSs(900) == "15:00"
                && CountdownOverlay.FormatMmSs(1020) == "17:00"
                && CountdownOverlay.FormatMmSs(6000) == "100:00"
                && CountdownOverlay.FormatMmSs(90) == "01:30"
                && CountdownOverlay.FormatMmSs(0) == "00:00"
                && CountdownOverlay.FormatMmSs(-5) == "00:00", "");
            chk("时间链空字符串反序列化为空列表",
                TimerChainPreset.DeserializeNodes("").Count == 0, "");
            var sampleNode = TimerChainPreset.MakeNode(1, 870, 810, 0, 2, 1, "刷支援");
            var sampleList = new List<TimerChainNode>();
            sampleList.Add(sampleNode);
            var deSampleList = TimerChainPreset.DeserializeNodes(TimerChainPreset.SerializeNodes(sampleList));
            chk("时间链单条节点序列化往返",
                deSampleList.Count == 1 && deSampleList[0].TriggerMode == 1
                && deSampleList[0].StartSec == 870 && deSampleList[0].EndSec == 810
                && deSampleList[0].FlashMode == 1 && deSampleList[0].LabelText == "刷支援", "");
            chk("热键配置表长度一致（8 项）",
                HKDefVk.Length == 8 && HKDefMod.Length == 8 && HKFunc.Length == 8 && HKTip.Length == 8, "");

            var defPresets = TimerChainPreset.CreateDefaultPresets();
            string serChains = TimerChainPreset.SerializePresets(defPresets);
            var dePresets = TimerChainPreset.DeserializePresets(serChains);
            chk("时间链预设序列化往返",
                dePresets.Count == 2
                && dePresets[0].Name == "标准对局" && dePresets[0].TotalSec == 1080 && dePresets[0].Nodes.Count == 0
                && dePresets[1].Name == "标准对局(带标注)" && dePresets[1].TotalSec == 1080
                && dePresets[1].Nodes.Count == 14 && dePresets[1].Nodes[1].LabelText == "15:00 轻收开门", "");

            var nStd = dePresets[1].Nodes;
            int tc; int tf; string tl;
            TimerChainPreset.EvaluateTimeline(nStd, 1050, 0, out tc, out tf, out tl);
            chk("时间链 17:30 事件①标注", tc == 0 && tf == 0 && tl == "17:00 事件①", "");
            TimerChainPreset.EvaluateTimeline(nStd, 1040, 0, out tc, out tf, out tl);
            chk("时间链 17:20 恢复常态", tc == 0 && tf == 0 && tl == "", "");
            TimerChainPreset.EvaluateTimeline(nStd, 620, 0, out tc, out tf, out tl);
            chk("时间链 10:20 低频闪烁", tc == 0 && tf == 1 && tl == "", "");
            TimerChainPreset.EvaluateTimeline(nStd, 150, 0, out tc, out tf, out tl);
            chk("时间链 02:30 亮红+低频闪烁+核弹标注叠加", tc == 5 && tf == 1 && tl == "2:00 核弹激活倒计时", "");
            TimerChainPreset.EvaluateTimeline(nStd, 60, 0, out tc, out tf, out tl);
            chk("时间链 01:00 亮红+中频闪烁", tc == 5 && tf == 2 && tl == "", "");
            TimerChainPreset.EvaluateTimeline(nStd, 10, 0, out tc, out tf, out tl);
            chk("时间链 00:10 亮红+高频闪烁", tc == 5 && tf == 3 && tl == "", "");

            // ---- 纯 C# 自更新（参数解析 / 同目录替换 / 回滚 / 清理 .old） ----
            chk("命令行 --after-update 解析",
                Program.ParseAfterUpdatePid(new string[] { "rx.exe", "--after-update", "4321", "--dev" }) == 4321
                && Program.ParseAfterUpdatePid(new string[] { "rx.exe", "--dev" }) == 0
                && Program.ParseAfterUpdatePid(new string[] { "rx.exe", "--after-update", "bad" }) == 0, "");

            string updDir = Path.Combine(tmpDir, "upd_case");
            Directory.CreateDirectory(updDir);
            string exeF = Path.Combine(updDir, "RXToolbox.exe");
            string updExeF = exeF + ".upd";
            File.WriteAllText(exeF, "OLD_EXE");
            File.WriteAllText(updExeF, "NEW_EXE_12345");
#if ADMIN_EDITION
            string dataF = Path.Combine(updDir, "phrases.json");
            string updDataF = dataF + ".upd";
            File.WriteAllText(dataF, "OLD_DATA");
            File.WriteAllText(updDataF, "NEW_DATA_67890");
            string oldExeU, oldDataU, swapErr;
            bool swapOk = ApplySelfUpdateFiles(
                exeF, updExeF, new FileInfo(updExeF).Length,
                dataF, updDataF, new FileInfo(updDataF).Length,
                out oldExeU, out oldDataU, out swapErr);
            chk("自更新原地替换成功",
                swapOk
                && File.ReadAllText(exeF) == "NEW_EXE_12345"
                && File.ReadAllText(oldExeU) == "OLD_EXE"
                && !File.Exists(updExeF)
                && File.ReadAllText(dataF) == "NEW_DATA_67890"
                && File.ReadAllText(oldDataU) == "OLD_DATA"
                && !File.Exists(updDataF), swapErr);

            RollbackSelfUpdateFiles(exeF, oldExeU, updExeF, dataF, oldDataU, updDataF);
            chk("自更新启动失败回滚成功",
                File.ReadAllText(exeF) == "OLD_EXE"
                && File.ReadAllText(dataF) == "OLD_DATA"
                && File.Exists(updExeF)
                && File.Exists(updDataF), "");

            File.WriteAllText(exeF + ".old.999", "STALE");
            string legacyOldF = Path.Combine(updDir, "RX工具箱 V1.7.exe.old.123");
            File.WriteAllText(legacyOldF, "LEGACY_STALE");
            CleanupUpdateArtifacts(exeF, dataF, false);
            chk("自更新残留 .old/.upd 清理",
                !File.Exists(exeF + ".old")
                && !File.Exists(exeF + ".old.999")
                && !File.Exists(legacyOldF)
                && !File.Exists(updExeF)
                && !File.Exists(dataF + ".old")
                && !File.Exists(updDataF), "");

            File.WriteAllText(updExeF, "NEW_EXE_SIZE_TEST");
            string dummyOldE, dummyOldD, badErr;
            bool badSwap = ApplySelfUpdateFiles(exeF, updExeF, 999999L, dataF, "", 0, out dummyOldE, out dummyOldD, out badErr);
            chk("自更新大小校验不符自动拒绝并保留旧版",
                !badSwap && File.ReadAllText(exeF) == "OLD_EXE", badErr);

            chk("旧版本文件名迁移识别（仅匹配 RX工具箱 V*.exe）",
                Program.ShouldMigrateLegacyExeName("RX工具箱 V1.7.exe")
                && Program.ShouldMigrateLegacyExeName("rx工具箱 V1.6.2.exe")
                && !Program.ShouldMigrateLegacyExeName("Rx工具箱.exe")
                && !Program.ShouldMigrateLegacyExeName("OtherApp.exe"), "");

            chk("命令行 --migrated-from 与进程名识别",
                Program.ParseMigratedFromArg(new string[] { "rx.exe", "--after-update", "9", "--migrated-from", "RX工具箱 V1.7.exe" }) == "RX工具箱 V1.7.exe"
                && Program.IsRxToolboxProcessName("Rx工具箱")
                && Program.IsRxToolboxProcessName("RX工具箱 V1.7")
                && !Program.IsRxToolboxProcessName("notepad"), "");
#else
            string oldExeU, oldDataU, swapErr;
            bool swapOk = ApplySelfUpdateFiles(
                exeF, updExeF, new FileInfo(updExeF).Length,
                "", "", 0,
                out oldExeU, out oldDataU, out swapErr);
            chk("自更新原地替换成功",
                swapOk
                && File.ReadAllText(exeF) == "NEW_EXE_12345"
                && File.ReadAllText(oldExeU) == "OLD_EXE"
                && !File.Exists(updExeF), swapErr);

            RollbackSelfUpdateFiles(exeF, oldExeU, updExeF, "", "", "");
            chk("自更新启动失败回滚成功",
                File.ReadAllText(exeF) == "OLD_EXE"
                && File.Exists(updExeF), "");

            File.WriteAllText(exeF + ".old.999", "STALE");
            CleanupUpdateArtifacts(exeF, "", false);
            chk("自更新残留 .old/.upd 清理",
                !File.Exists(exeF + ".old")
                && !File.Exists(exeF + ".old.999")
                && !File.Exists(updExeF), "");

            File.WriteAllText(updExeF, "NEW_EXE_SIZE_TEST");
            string dummyOldE, dummyOldD, badErr;
            bool badSwap = ApplySelfUpdateFiles(exeF, updExeF, 999999L, "", "", 0, out dummyOldE, out dummyOldD, out badErr);
            chk("自更新大小校验不符自动拒绝并保留旧版",
                !badSwap && File.ReadAllText(exeF) == "OLD_EXE", badErr);
#endif

            bool sawWarnLine;
            string strippedWarn = StripWarnChangelogLine("!!【重要提醒】测试", out sawWarnLine);
#if ADMIN_EDITION
            chk("管理版窗口标题后缀标识", APP_EDITION_SUFFIX == "（管理版）", APP_EDITION_SUFFIX);
            chk("更新日志首条与当前版本一致且高亮标记格式正确",
                CHANGELOG.Length > 3
                && CHANGELOG[0].StartsWith(APP_VERSION_NUM + "|", StringComparison.Ordinal)
                && CHANGELOG[3].StartsWith("1.8.0|", StringComparison.Ordinal)
                && CHANGELOG[3].Contains("Rx工具箱.exe")
                && sawWarnLine && strippedWarn.StartsWith("【重要提醒】", StringComparison.Ordinal), "");
#else
            chk("公开版更新日志、无管理后缀与独立更新源配置检查",
                CHANGELOG.Length == 5
                && CHANGELOG[0].StartsWith("1.8.0|", StringComparison.Ordinal)
                && APP_EDITION_SUFFIX == ""
                && UPDATE_URL_DEFAULT == "https://rx-tools.pages.dev/version.json"
                && sawWarnLine && strippedWarn.StartsWith("【重要提醒】", StringComparison.Ordinal)
                && !string.Join("\n", CHANGELOG).Contains("!!"), "");
#endif

#if ADMIN_EDITION
            // ---- 分类折叠列表（独立说明条兼容 / 大类嵌套 / 键盘跳过 / 切档保留） ----
            using (var wl = new WrapList())
            {
                wl.Size = new Size(800, 480);
                string[] wLabels = new string[] {
                    "提示：单独说明栏",
                    "D级人员", "精巧手法", "强制夺取",
                    "科研部门", "招募",
                    "▓大类A", "子组1", "物品1", "子组2", "物品2"
                };
                string[] wValues = new string[] {
                    "",
                    "", "!a ^", "!b ^",
                    "", "!c ^",
                    "", "", "!g ^ 1", "", "!g ^ 2"
                };
                bool[] wHeads = new bool[] {
                    true,
                    true, false, false,
                    true, false,
                    true, true, false, true, false
                };
                string[] wNotes = new string[wLabels.Length];
                wl.SetData(wLabels, wValues, wHeads, wNotes);

                wl.ToggleSection(0, false);
                chk("分类折叠：无下属独立段不可折叠且不影响下方分组",
                    !wl.IsCollapsible(0) && !wl.IsCollapsed(0) && !wl.IsRowHidden(1) && !wl.IsRowHidden(2), "");

                wl.Sel = 2;
                wl.ToggleSection(1, false);
                chk("分类折叠：点击收起普通分类并跳过已折叠条目",
                    wl.IsCollapsible(1) && wl.IsCollapsed(1)
                    && wl.IsRowHidden(2) && wl.IsRowHidden(3)
                    && !wl.IsRowHidden(4) && !wl.IsRowHidden(5)
                    && wl.Sel == -1 && wl.FindSelectable(1, 1) == 5, "");

                wl.ToggleSection(7, false);   // 先收起 子组1
                wl.ToggleSection(6, false);   // 再收起 ▓大类A
                bool bothHidden = wl.IsCollapsed(6) && wl.IsRowHidden(7) && wl.IsRowHidden(8) && wl.IsRowHidden(10) && !wl.IsRowHidden(6);
                wl.ToggleSection(6, false);   // 展开 ▓大类A，子组1应保持折叠
                chk("分类折叠：▓ 大类与子分类嵌套折叠及状态保留",
                    bothHidden && !wl.IsCollapsed(6) && wl.IsCollapsed(7)
                    && !wl.IsRowHidden(7) && wl.IsRowHidden(8) && !wl.IsRowHidden(10), "");

                string[] wValuesUlx = (string[])wValues.Clone();
                wValuesUlx[5] = "ulx c ^";
                wl.SetData(wLabels, wValuesUlx, wHeads, wNotes);
                chk("分类折叠：同表切档/填ID保留折叠状态",
                    wl.IsCollapsed(1) && wl.IsCollapsed(7) && !wl.IsCollapsed(6), "");

                wl.ToggleSection(6, false);   // 收起 ▓大类A
                wl.ScrollToIndex(8);          // 跳转 子组1 下条目应自动展开 大类A 与 子组1
                chk("分类折叠：分类跳转自动展开父级与当前组",
                    !wl.IsCollapsed(6) && !wl.IsCollapsed(7) && !wl.IsRowHidden(8), "");

                wl.SetData(new string[] { "物品1" }, new string[] { "!g ^ 1" }, new bool[] { false }, new string[] { "" });
                chk("分类折叠：进入筛选后复位折叠状态",
                    !wl.IsCollapsed(0) && wl.FindSelectable(0, 1) == 0, "");

                // 验证禁用了 StandardDoubleClick 且动画进行中连点多个分类都能立即响应
                wl.SetData(wLabels, wValues, wHeads, wNotes);
                int top1 = wl.TestRowTop(1);
                wl.TestSimulateFastClickAtY(top1 + 6, 0);
                int top4DuringAnim = wl.TestRowTop(4);
                // 模拟点击第 2 个分类时上方仍在收缩导致 MouseUp 坐标相对位移 20px
                wl.TestSimulateFastClickAtY(top4DuringAnim + 6, 20);
                bool multiOk = !wl.TestHasStandardDoubleClick() && wl.IsCollapsed(1) && wl.IsCollapsed(4);
                // 同位置连续快速回点 2 次（相当于原地高速连击）也必须每次都切换成功
                int top1Now = wl.TestRowTop(1);
                wl.TestSimulateFastClickAtY(top1Now + 6, 0);
                bool expandOk = !wl.IsCollapsed(1);
                wl.TestSimulateFastClickAtY(top1Now + 6, 0);
                bool refoldOk = wl.IsCollapsed(1);
                chk("分类折叠：禁用双击吞点击且动画中快速连点多个分类均即时生效",
                    multiOk && expandOk && refoldOk,
                    "c1=" + wl.IsCollapsed(1) + " c4=" + wl.IsCollapsed(4));

                wl.DefaultCollapsed = true;
                wl.SetData(new string[0], new string[0], new bool[0], new string[0]);
                wl.SetData(wLabels, wValues, wHeads, wNotes);
                bool startAllFolded = !wl.IsCollapsed(0) && wl.IsCollapsed(1) && wl.IsCollapsed(4) && wl.IsCollapsed(6) && wl.IsCollapsed(7) && wl.AreAllCollapsed();
                wl.ToggleSection(1, false);
                wl.SetData(wLabels, wValuesUlx, wHeads, wNotes);
                bool keepUserAdj = !wl.IsCollapsed(1) && wl.IsCollapsed(4) && !wl.AreAllCollapsed();
                wl.SetAllCollapsed(false);
                bool expandedAll = !wl.IsCollapsed(1) && !wl.IsCollapsed(4) && !wl.IsCollapsed(6) && !wl.IsCollapsed(7) && !wl.AreAllCollapsed();
                wl.SetAllCollapsed(true);
                bool collapsedAll = wl.AreAllCollapsed();
                chk("分类折叠：默认全收起、单次运行保留手动状态与一键全部展开/收起",
                    startAllFolded && keepUserAdj && expandedAll && collapsedAll, "");
            }
#endif

#if ADMIN_EDITION
            // ---- 数据更新选择、差异对比与 .rxbak 本地备份/还原 ----
            var bAdded = new List<string>();
            var bChg = new List<string>();
            var bRem = new List<string>();
            int dAdd, dChg, dDel;
            ComputeTabDiff(
                new string[] { DevJoinItem("甲", "!a", "", "", ""), DevJoinItem("甲", "!a2", "", "", ""), DevJoinItem("旧项", "!old", "", "", "") },
                new string[] { DevJoinItem("甲", "!a", "", "", ""), DevJoinItem("甲", "!a2_mod", "", "", ""), DevJoinItem("新项", "!new", "", "", "") },
                out dAdd, out dChg, out dDel, bAdded, bChg, bRem);
            chk("差异对比：重名区分与新增/修改/移除计数",
                dAdd == 1 && dChg == 1 && dDel == 1
                && bAdded.Count == 1 && bAdded[0] == "新项"
                && bChg.Count == 1 && bChg[0] == "甲"
                && bRem.Count == 1 && bRem[0] == "旧项", "");

            int secAdd, secChg, secDel;
            ComputeTabDiff(
                new string[] { "分组A", DevJoinItem("L85", "!g a1", "", "", ""), "分组B", DevJoinItem("L85", "!g b1", "", "", "") },
                new string[] { "分组A", "分组B", DevJoinItem("L85", "!g b1", "", "", "") },
                out secAdd, out secChg, out secDel, null, null, null);
            chk("差异对比：按所属分类隔离同名项（删除分组A不影响分组B）",
                secAdd == 0 && secChg == 0 && secDel == 1,
                string.Format("add={0} chg={1} del={2}", secAdd, secChg, secDel));

            chk("页签一致性判断（未改动表不写入 data-custom）",
                TabListEquals(new List<string>(new string[] { "x", "y" }), new string[] { "x", "y" })
                && !TabListEquals(new List<string>(new string[] { "x", "z" }), new string[] { "x", "y" }), "");

            string bakTestDir = Path.Combine(tmpDir, "bak_case");
            string bakSubDir = Path.Combine(bakTestDir, "backups");
            Directory.CreateDirectory(bakSubDir);
            string origCustom = "标题1\u0001内容A\u0002标题2\u0001多行\\n内容B\u0002";
            string origPhrases = "{\"指令表\":[{\"title\":\"测试指令\",\"text\":\"!ping\"}],\"技能表\":[],\"物品表\":[],\"模型表\":[],\"角色表\":[],\"SCP角色\":[]}";
            string origOverride = "###指令表\r\n测试指令\u0001!ping_custom\r\n###技能表\r\n自定义技能\u0001!ab\r\n";
            File.WriteAllText(Path.Combine(bakTestDir, "custom.txt"), origCustom, new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(bakTestDir, "phrases.json"), origPhrases, new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(bakTestDir, "data-custom.txt"), origOverride, new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(bakTestDir, "settings.ini"), "ThemeMode=1\r\nHubKey=secret-test-key\r\n", new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(bakTestDir, "servers.txt"), "1.2.3.4:27015|s1|srv\r\n", new UTF8Encoding(false));
            File.WriteAllBytes(Path.Combine(bakTestDir, "crosshair.png"), new byte[] { 137, 80, 78, 71, 1, 2, 3, 4 });

            string bakPkg = Path.Combine(bakSubDir, "20261117-120000_manual.rxbak");
            string bakErr;
            bool bakCreated = CreateBackupArchiveToFile(bakPkg, bakTestDir, bakTestDir, "", "manual", "测试备注{1}", out bakErr);
            string pCr = "", pVr = "", pKd = "", pNt = "", pErr = "";
            List<string> sumKeys = null;
            bool sumOk = bakCreated && ParseBackupSummary(bakPkg, out pCr, out pVr, out pKd, out pNt, out sumKeys, out pErr);
            chk("备份包轻量摘要读取（不解正文）", sumOk && sumKeys != null && sumKeys.Contains("settings.ini") && sumKeys.Contains("crosshair.png.b64"), pErr);
            Dictionary<string, byte[]> pFiles = null;
            bool bakParsed = bakCreated && ParseBackupArchive(bakPkg, out pCr, out pVr, out pKd, out pNt, out pFiles, out pErr);
            string bakIni = (pFiles != null && pFiles.ContainsKey("settings.ini")) ? Encoding.UTF8.GetString(pFiles["settings.ini"]) : "";
            chk("备份包 .rxbak 创建、脱敏与无损解析（剔除 HubKey）",
                bakParsed && pFiles != null
                && pKd == "manual" && pNt == "测试备注{1}"
                && Encoding.UTF8.GetString(pFiles["custom.txt"]) == origCustom
                && pFiles.ContainsKey("crosshair.png.b64")
                && pFiles["crosshair.png.b64"].Length == 8
                && bakIni.Contains("ThemeMode=1") && !bakIni.Contains("secret-test-key"), bakErr + " " + pErr);

            string badBak = Path.Combine(bakSubDir, "bad.rxbak");
            File.WriteAllText(badBak, "{\"format\":\"wrong\"}", Encoding.UTF8);
            Dictionary<string, byte[]> dummyFiles;
            string dummyErr;
            chk("备份包格式异常拦截",
                !ParseBackupArchive(badBak, out pCr, out pVr, out pKd, out pNt, out dummyFiles, out dummyErr), "");

            // 测试选择性恢复：先修改 custom 和 settings，只恢复 custom，确认 settings 未被覆盖
            File.WriteAllText(Path.Combine(bakTestDir, "custom.txt"), "被覆盖掉", new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(bakTestDir, "settings.ini"), "ThemeMode=2\r\n", new UTF8Encoding(false));
            string rErr = "";
            bool selRestored = (pFiles != null) && RestoreBackupFiles(bakTestDir, pFiles, true, false, false, false, false, false, out rErr);
            chk("备份包选择性还原（只还原 custom 不动 settings）",
                selRestored
                && File.ReadAllText(Path.Combine(bakTestDir, "custom.txt"), Encoding.UTF8) == origCustom
                && File.ReadAllText(Path.Combine(bakTestDir, "settings.ini"), Encoding.UTF8) == "ThemeMode=2\r\n", rErr);

            // 测试暂存 phrases.new.json 与选择性替换（保留指定页修改）
            string pendSrc = Path.Combine(bakTestDir, "phrases.json.upd");
            string pendDst = Path.Combine(bakTestDir, "phrases.new.json");
            string newPhrases = "{\"指令表\":[{\"title\":\"测试指令\",\"text\":\"!ping2\"}],\"技能表\":[{\"title\":\"新技能\",\"text\":\"!ab2\"}],\"物品表\":[],\"模型表\":[],\"角色表\":[],\"SCP角色\":[]}";
            File.WriteAllText(pendSrc, newPhrases, new UTF8Encoding(false));
            string stgErr = "", appErr = "";
            bool stgOk = StagePendingDataFile(pendSrc, pendDst, out stgErr);
            bool appOk = stgOk && ApplyPendingDataToDisk(pendDst, Path.Combine(bakTestDir, "phrases.json"), Path.Combine(bakTestDir, "data-custom.txt"), false, new string[] { "技能表" }, out appErr);
            string[] remOv = LoadOverrideTabNames(Path.Combine(bakTestDir, "data-custom.txt"));
            chk("待应用新版暂存与按页保留/清除覆盖",
                appOk
                && !File.Exists(pendDst)
                && File.ReadAllText(Path.Combine(bakTestDir, "phrases.json"), Encoding.UTF8) == newPhrases
                && remOv.Length == 1 && remOv[0] == "指令表", stgErr + " " + appErr);

            // 测试自动备份轮转清理（只删多余 *_auto*.rxbak，不动 manual）
            for (int k = 1; k <= 5; k++)
                File.WriteAllText(Path.Combine(bakSubDir, "20261117-00000" + k + "_auto.rxbak"), "{}", Encoding.UTF8);
            int pruned = PruneAutoBackups(bakSubDir, 2);
            chk("自动备份数量裁剪且保留手动备份",
                pruned == 3
                && Directory.GetFiles(bakSubDir, "*_auto*.rxbak").Length == 2
                && File.Exists(bakPkg), "pruned=" + pruned);
#endif

            try { Directory.Delete(tmpDir, true); } catch { }
        }
        catch (Exception ex)
        {
            chk("更新下载自检可运行", false, ex.GetType().Name);
        }

        sb.AppendLine();
        sb.AppendLine(string.Format("结果：{0} 通过，{1} 失败", pass, fail));

        try
        {
            string dir = Path.GetDirectoryName(Application.ExecutablePath);
            if (!CanWriteDir(dir))
            {
                dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RXToolbox");
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(Path.Combine(dir, "selftest.log"), sb.ToString(), new UTF8Encoding(true));
        }
        catch { }
        Console.WriteLine(sb.ToString());
        return fail;
    }
}
