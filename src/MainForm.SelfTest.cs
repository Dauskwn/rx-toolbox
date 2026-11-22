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
#if ADMIN_EDITION
            foreach (var kv in navPages)
            {
                Control pg = kv.Value;
                if (pg == null) continue;
                sb.AppendLine();
                sb.AppendLine("[nav " + kv.Key + "] " + pg.Text + "  (" + pg.Controls.Count + " 个直接子控件)");
                DumpControls(sb, pg, "  ", 0);
            }
#else
            for (int t = 0; t < tabs.TabPages.Count; t++)
            {
                TabPage pg = tabs.TabPages[t];
                sb.AppendLine();
                sb.AppendLine("[tab " + t + "] " + pg.Text + "  (" + pg.Controls.Count + " 个直接子控件)");
                DumpControls(sb, pg, "  ", 0);
            }
#endif
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
        chk("目标名：含分号自动加引号防控制台断句", ApplyTargetName("!tp ^", "a;b") == "!tp \"a;b\"", ApplyTargetName("!tp ^", "a;b"));

        var pkt = new List<byte>();
        pkt.AddRange(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x44, 4 });
        Action<byte, string, int, float> addPl = delegate(byte pIdx, string nm, int sc, float dur)
        {
            pkt.Add(pIdx);
            pkt.AddRange(Encoding.UTF8.GetBytes(nm));
            pkt.Add(0);
            pkt.AddRange(BitConverter.GetBytes(sc));
            pkt.AddRange(BitConverter.GetBytes(dur));
        };
        addPl(0, "顾彼惜朝", 3, 125.9f);
        addPl(1, "若葉 睦", 0, 12.1f);
        addPl(2, "   ", 0, 5.0f);
        addPl(3, "♯###503", -1, 3600.0f);
        var parsedPls = ParseA2SPlayersPayload(pkt.ToArray());
        var truncPls = ParseA2SPlayersPayload(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x44, 1, 0, 65, 66 });
        chk("A2S_PLAYER 协议包解析：正常解码中/日/符号昵称、过滤空槽与截断兜底",
            parsedPls.Count == 3
            && parsedPls[0].Name == "顾彼惜朝" && parsedPls[0].DurationSec == 125 && parsedPls[0].Score == 3
            && parsedPls[1].Name == "若葉 睦" && parsedPls[1].DurationSec == 12
            && parsedPls[2].Name == "♯###503" && parsedPls[2].DurationSec == 3600
            && truncPls.Count == 0, "");

        var fpSrc = new List<A2SPlayerInfo>();
        fpSrc.Add(new A2SPlayerInfo { Name = "Bot15", DurationSec = 500, IsBot = true });
        fpSrc.Add(new A2SPlayerInfo { Name = "顾彼惜朝（omo）", DurationSec = 180, IsBot = false, NamePinyin = PinyinHelper.BuildTitle("顾彼惜朝（omo）") });
        fpSrc.Add(new A2SPlayerInfo { Name = "若葉 睦", DurationSec = 20, IsBot = false, NamePinyin = PinyinHelper.BuildTitle("若葉 睦") });
        fpSrc.Add(new A2SPlayerInfo { Name = "♯###503", DurationSec = 600, IsBot = false, NamePinyin = PinyinHelper.BuildTitle("♯###503") });
        var fpAllNoBot = OnlinePlayerPickerForm.FilterAndSort(fpSrc, "", true, 0);
        var fpWithBot = OnlinePlayerPickerForm.FilterAndSort(fpSrc, "", false, 1);
        var fpPy = OnlinePlayerPickerForm.FilterAndSort(fpSrc, "gbxc", true, 0);
        var fpSym = OnlinePlayerPickerForm.FilterAndSort(fpSrc, "###", true, 2);
        chk("在线玩家筛选排序：空搜留全量、隐藏 Bot、拼音/符号命中与三种排序模式",
            fpAllNoBot.Count == 3 && fpAllNoBot[0].Name == "若葉 睦" && fpAllNoBot[2].Name == "♯###503"
            && fpWithBot.Count == 4 && fpWithBot[0].Name == "♯###503"
            && fpPy.Count == 1 && fpPy[0].Name == "顾彼惜朝（omo）"
            && fpSym.Count == 1 && fpSym[0].Name == "♯###503"
            && A2SPlayerInfo.DetectBotName("Bot15") && !A2SPlayerInfo.DetectBotName("Bottom"), "");

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

            chk("更新下载 + SHA256 校验通过", DownloadAndVerify(fileUrl, goodSha, dest, null, true) && File.Exists(dest), "");
            chk("更新下载：哈希不对时拒绝", !DownloadAndVerify(fileUrl, new string('0', 64), Path.Combine(tmpDir, "bad.bin"), null, true), "");
            chk("更新下载：空哈希时拒绝", !DownloadAndVerify(fileUrl, "", Path.Combine(tmpDir, "emptysha.bin"), null, true), "");
            chk("更新下载：拒绝非本机 http://，允许 https:// 与本机回环端口",
                !IsHttpsUrl("http://rx-tools.pages.dev/a.exe")
                && IsHttpsUrl("https://rx-tools.pages.dev/a.exe")
                && !IsAllowedUpdateUrl("http://example.com/a.exe")
                && !IsAllowedUpdateUrl("http://192.168.1.10/a.exe")
                && IsAllowedUpdateUrl("http://127.0.0.1:18080/version.json")
                && IsAllowedUpdateUrl("http://localhost:18080/a.exe")
                && !DownloadAndVerify("http://example.com/a.exe", goodSha, Path.Combine(tmpDir, "http.bin")), "");
            chk("更新下载：生产默认拒绝 file:// 与 UNC 路径",
                !DownloadAndVerify(fileUrl, goodSha, Path.Combine(tmpDir, "blocked_file.bin"))
                && !DownloadAndVerify("file://attacker/share/evil.exe", goodSha, Path.Combine(tmpDir, "blocked_unc.bin"), null, true), "");
            chk("更新跳转校验：禁止 HTTPS 降级为 HTTP 或回环地址",
                IsAllowedRedirectTarget("https://rx-tools.pages.dev/a", "https://cdn.example.com/b")
                && !IsAllowedRedirectTarget("https://rx-tools.pages.dev/a", "http://cdn.example.com/b")
                && !IsAllowedRedirectTarget("https://rx-tools.pages.dev/a", "http://127.0.0.1:8080/b")
                && IsAllowedRedirectTarget("http://127.0.0.1:18080/a", "http://127.0.0.1:18080/b"), "");
            using (var testBmp = new Bitmap(32, 16, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(testBmp)) g.Clear(Color.Black);
                using (var sb1 = CreateSoftwareBitmapFromBitmap(testBmp, 1))
                using (var sb2 = CreateSoftwareBitmapFromBitmap(testBmp, 2))
                {
                    chk("OCR 零 PNG 位图直传：scale=1 与 scale=2 正常生成 SoftwareBitmap",
                        sb1 != null && sb1.PixelWidth == 32 && sb1.PixelHeight == 16
                        && sb2 != null && sb2.PixelWidth == 64 && sb2.PixelHeight == 32, "");
                }
            }
            Native.LogWarn(null, null);
            Native.LogWarn("selftest", new InvalidOperationException("rate-limit-check"));
            Native.LogWarn("selftest", new InvalidOperationException("rate-limit-check"));
            chk("Native.LogWarn 空参数与限流调用不抛异常", true, "");

            string savedLogPath = Native.LogPath;
            try
            {
                string testLog = Path.Combine(tmpDir, "rx_test.log");
                Native.LogPath = testLog;
                long emptySz = Native.GetLogSizeBytes();
                File.WriteAllBytes(testLog, new byte[12]);
                File.WriteAllBytes(testLog + ".old", new byte[8]);
                long twoSz = Native.GetLogSizeBytes();
                bool cleared = Native.ClearLog();
                long afterSz = Native.GetLogSizeBytes();
                chk("Native.GetLogSizeBytes / ClearLog 统计与清空主备日志准确",
                    emptySz == 0 && twoSz == 20 && cleared && afterSz == 0
                    && !File.Exists(testLog) && !File.Exists(testLog + ".old"), "");
            }
            finally { Native.LogPath = savedLogPath; }

            chk("准星中心偏移钳制 Overlay.ClampOffset 边界正确",
                Overlay.ClampOffset(-999) == Overlay.OffsetMin
                && Overlay.ClampOffset(999) == Overlay.OffsetMax
                && Overlay.ClampOffset(3) == 3, "");

            var cpList = new List<CrosshairPreset>();
            var cpA = new CrosshairPreset
            {
                Name = "狙击红点,微偏", StyleIndex = 2, ColorIndex = 4, Thickness = 3, Length = 12, Gap = 4,
                DotSize = 3, OutlineWidth = 2, OpacityPct = 85, OffsetX = -2, OffsetY = 3, CustomImagePath = ""
            };
            var cpB = new CrosshairPreset
            {
                Name = "自定义;PNG", StyleIndex = 0, ColorIndex = 0, Thickness = 2, Length = 10, Gap = 5,
                DotSize = 1, OutlineWidth = 1, OpacityPct = 90, OffsetX = 1, OffsetY = -1,
                CustomImagePath = @"C:\dir\cross,semi;test.png"
            };
            cpList.Add(cpA);
            cpList.Add(cpB);
            string cpSer = CrosshairPreset.SerializePresets(cpList);
            var cpBack = CrosshairPreset.DeserializePresets(cpSer);
            chk("准星方案 CrosshairPreset 序列化往返无损（含特殊标点与偏移）",
                cpBack.Count == 2
                && cpBack[0].Name == "狙击红点,微偏" && cpBack[0].OffsetX == -2 && cpBack[0].OffsetY == 3 && cpBack[0].OpacityPct == 85
                && cpBack[1].Name == "自定义;PNG" && cpBack[1].CustomImagePath == @"C:\dir\cross,semi;test.png", "");

            var cpDefaults = CrosshairPreset.DeserializePresets("broken-data");
            using (var ovTest = new Overlay())
            {
                cpDefaults[1].ApplyScalarToOverlay(ovTest);
                bool matchBefore = cpDefaults[1].MatchesOverlay(ovTest);
                ovTest.OffsetX = 2;
                bool matchAfter = cpDefaults[1].MatchesOverlay(ovTest);
                chk("准星默认方案 4 套加载与 MatchesOverlay 匹配/改动检测",
                    cpDefaults.Count == 4 && matchBefore && !matchAfter, "");
            }

            using (var strip = new CrosshairPresetStrip())
            {
                strip.Size = new Size(498, 74);
                strip.Presets = cpDefaults;
                var rc0 = strip.GetCardRect(0);
                var rc3 = strip.GetCardRect(3);
                chk("CrosshairPresetStrip 卡片命中测试与单实例唤醒消息常量检查",
                    strip.HitTest(new Point(rc0.X + 4, rc0.Y + 4)) == 0
                    && strip.HitTest(new Point(rc3.X + 4, rc3.Y + 4)) == 3
                    && Native.WM_RX_WAKE == 0x8001, "");
            }
#if ADMIN_EDITION
            chk("MiniJson：损坏输入不死循环", MiniJson.Parse("{bad:123,@}") != null, "");
            chk("MiniJson：字面量精确校验（拒绝畸形 txyz/fxyz/nxyz）",
                !true.Equals(MiniJson.Parse("txyz"))
                && !false.Equals(MiniJson.Parse("fxyz"))
                && MiniJson.Parse("nxyz") != null
                && true.Equals(MiniJson.Parse("true"))
                && false.Equals(MiniJson.Parse("false"))
                && MiniJson.Parse("null") == null, "");
            string encHub = ProtectHubKey("test-hub-secret-191");
            chk("HubKey DPAPI 加密落盘与解密往返一致",
                encHub.StartsWith("dpapi:", StringComparison.Ordinal) && UnprotectHubKey(encHub) == "test-hub-secret-191", "");
            byte[] redactedBoth = RedactHubKeyFromSettings(Encoding.UTF8.GetBytes("ThemeMode=1\r\nHubKey=plain\r\nHubKeyEnc=" + encHub + "\r\n"));
            string redactedTxt = Encoding.UTF8.GetString(redactedBoth);
            chk("备份脱敏：同时剔除明文 HubKey 与密文 HubKeyEnc",
                redactedTxt.IndexOf("HubKey", StringComparison.Ordinal) < 0 && redactedTxt.IndexOf("ThemeMode=1", StringComparison.Ordinal) >= 0, "");
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
#if ADMIN_EDITION
            chk("热键配置表长度一致（9 项含全局命令面板）",
                HK_COUNT == 9 && HKDefVk.Length == 9 && HKDefMod.Length == 9 && HKFunc.Length == 9 && HKTip.Length == 9
                && HKDefVk[8] == 0x4B && HKDefMod[8] == 0x0001, "");
#else
            chk("热键配置表长度一致（8 项）",
                HK_COUNT == 8 && HKDefVk.Length == 8 && HKDefMod.Length == 8 && HKFunc.Length == 8 && HKTip.Length == 8, "");
#endif

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
            bool has180Warn = false;
            for (int ci = 0; ci < CHANGELOG.Length; ci++)
            {
                if (CHANGELOG[ci].StartsWith("1.8.0|", StringComparison.Ordinal) && CHANGELOG[ci].Contains("Rx工具箱.exe"))
                {
                    has180Warn = true;
                    break;
                }
            }
            chk("更新日志首条与当前版本一致且高亮标记格式正确",
                CHANGELOG.Length > 4
                && CHANGELOG[0].StartsWith(APP_VERSION_NUM + "|", StringComparison.Ordinal)
                && has180Warn
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

                chk("纯说明条目与可折叠分类头视觉判定区分（IsInfoBanner vs IsCollapsible）",
                    wl.IsInfoBanner(0) && !wl.IsCollapsible(0)
                    && !wl.IsInfoBanner(1) && wl.IsCollapsible(1)
                    && !wl.IsInfoBanner(2)
                    && !wl.IsInfoBanner(6) && wl.IsCollapsible(6), "");
            }

            using (var wlPerf = new WrapList())
            {
                wlPerf.DefaultCollapsed = true;
                wlPerf.Size = new Size(760, 300);
                int grpCnt = 4, perGrp = 40;
                int totRows = grpCnt * (1 + perGrp);
                string[] pL = new string[totRows];
                string[] pV = new string[totRows];
                bool[] pH = new bool[totRows];
                string[] pN = new string[totRows];
                int pos = 0;
                for (int g = 0; g < grpCnt; g++)
                {
                    pH[pos] = true; pL[pos] = "分组" + g; pV[pos] = ""; pN[pos] = ""; pos++;
                    for (int r = 0; r < perGrp; r++)
                    {
                        pH[pos] = false;
                        pL[pos] = "条目" + g + "_" + r;
                        pV[pos] = "!cmd ^ " + r;
                        pN[pos] = "注解" + r;
                        pos++;
                    }
                }
                wlPerf.SetData(pL, pV, pH, pN);
                int vw0 = wlPerf.TestViewWidth;
                int m0 = wlPerf.TestMeasureCount;
                wlPerf.ToggleSection(0, false);   // 展开第0组（40项 > 300px，滚动条从无到有）
                int vw1 = wlPerf.TestViewWidth;
                int m1 = wlPerf.TestMeasureCount;
                wlPerf.ToggleSection(0, false);   // 收起第0组（滚动条从有到无）
                int m2 = wlPerf.TestMeasureCount;
                wlPerf.Height = 600;              // 纯垂直拉高窗口
                int m3 = wlPerf.TestMeasureCount;
                wlPerf.ToggleSection(0, false);
                wlPerf.ScrollTop = 180;
                int targetRow = 10;
                int relY = wlPerf.TestRowTop(targetRow) - wlPerf.ScrollTop + 8;
                int hitRow = wlPerf.TestHitTest(relY);
                chk("分类折叠性能：固定槽位按需懒测，滚动条切换与垂直缩放零重测且二分命中准确",
                    m0 == 4 && vw0 == vw1 && m1 == 44 && m2 == 44 && m3 == 44 && hitRow == targetRow,
                    string.Format("m0={0} m1={1} m2={2} m3={3} vw={4}/{5} hit={6}/{7}", m0, m1, m2, m3, vw0, vw1, hitRow, targetRow));
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

            // ---- V1.9.0 HUD 渲染与下一事件预告 ----
            var next1 = TimerChainPreset.FindNextUpcomingNode(nStd, 1060);
            var next2 = TimerChainPreset.FindNextUpcomingNode(nStd, 960);
            var nextNone = TimerChainPreset.FindNextUpcomingNode(nStd, 10);
            chk("倒计时下一事件预告 FindNextUpcomingNode",
                next1 != null && next1.LabelText == "17:00 事件①"
                && next2 != null && next2.LabelText == "15:00 轻收开门"
                && nextNone == null, "");

            string pngPath = Path.Combine(tmpDir, "alpha_cross.png");
            using (var srcPng = new Bitmap(16, 16, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                for (int py = 0; py < 16; py++)
                    for (int px = 0; px < 16; px++)
                        srcPng.SetPixel(px, py, Color.FromArgb(0, 0, 0, 0));
                srcPng.SetPixel(8, 8, Color.FromArgb(120, 0, 255, 0));
                srcPng.Save(pngPath, System.Drawing.Imaging.ImageFormat.Png);
            }
            using (Bitmap outPng = Overlay.PrepareCrosshairImage(pngPath))
            {
                chk("32-bit 准星 PrepareCrosshairImage 保留 PNG 半透明 Alpha",
                    outPng != null && outPng.GetPixel(0, 0).A == 0 && outPng.GetPixel(8, 8).A == 120,
                    outPng == null ? "null" : ("a=" + outPng.GetPixel(8, 8).A));
            }

            string bmpPath = Path.Combine(tmpDir, "opaque_cross.bmp");
            using (var srcBmp = new Bitmap(16, 16, System.Drawing.Imaging.PixelFormat.Format24bppRgb))
            {
                for (int py = 0; py < 16; py++)
                    for (int px = 0; px < 16; px++)
                        srcBmp.SetPixel(px, py, Color.Black);
                srcBmp.SetPixel(8, 8, Color.White);
                srcBmp.Save(bmpPath, System.Drawing.Imaging.ImageFormat.Bmp);
            }
            using (Bitmap outBmp = Overlay.PrepareCrosshairImage(bmpPath))
            {
                chk("32-bit 准星 PrepareCrosshairImage 不透明图自动抠底",
                    outBmp != null && outBmp.GetPixel(0, 0).A == 0 && outBmp.GetPixel(8, 8).A == 255,
                    outBmp == null ? "null" : ("corner=" + outBmp.GetPixel(0, 0).A + " center=" + outBmp.GetPixel(8, 8).A));
            }

            using (var ovDef = new Overlay())
            using (var cdDef = new CountdownOverlay())
            {
                chk("HUD 默认外观与语义色板检查",
                    ovDef.OutlineWidth == 1 && ovDef.OpacityPct == 100
                    && !cdDef.UseCardStyle
                    && !ThemePalette.Accent.IsEmpty
                    && !ThemePalette.Success.IsEmpty
                    && !ThemePalette.Danger.IsEmpty
                    && ThemePalette.Success.ToArgb() != ThemePalette.Danger.ToArgb(), "");
            }

#if ADMIN_EDITION
            Color bgItem, fgItem, bgRole, fgRole, bgScp, fgScp, bgCust, fgCust;
            ThemePalette.GetSourceBadgeColors("物品表", out bgItem, out fgItem);
            ThemePalette.GetSourceBadgeColors("角色表", out bgRole, out fgRole);
            ThemePalette.GetSourceBadgeColors("SCP角色", out bgScp, out fgScp);
            ThemePalette.GetSourceBadgeColors("自定义", out bgCust, out fgCust);
            chk("管理版来源徽标语义色板区分",
                !bgItem.IsEmpty && !fgItem.IsEmpty && !bgRole.IsEmpty && !fgRole.IsEmpty
                && !bgScp.IsEmpty && !fgScp.IsEmpty && !bgCust.IsEmpty && !fgCust.IsEmpty
                && fgItem.ToArgb() != fgRole.ToArgb() && fgRole.ToArgb() != fgScp.ToArgb()
                && fgScp.ToArgb() != fgCust.ToArgb(), "");

            // ---- V1.9.0 管理版工作台、拼音与命令面板自检 ----
            chk("拼音首字母引擎 PinyinHelper.GetInitials",
                PinyinHelper.GetInitials("血肉枪") == "xrq"
                && PinyinHelper.GetInitials("MTF开局") == "mtfkj"
                && PinyinHelper.GetInitials("清理") == "ql"
                && PinyinHelper.GetInitials("") == "",
                PinyinHelper.GetInitials("血肉枪"));

            var palEntries = new List<PaletteEntry>();
            palEntries.Add(new PaletteEntry
            {
                PinKey = MakePinKey("指令表", "标题精确"),
                SourceTab = "指令表", Title = "标题精确", Body = "!ex", Alt = "", Ulx = "", Note = "",
                TitlePy = PinyinHelper.GetInitials("标题精确"), IsPinned = false, RecentOrder = -1
            });
            palEntries.Add(new PaletteEntry
            {
                PinKey = MakePinKey("物品表", "血肉枪"),
                SourceTab = "物品表", Title = "血肉枪", Body = "!give ^ xrq", Alt = "", Ulx = "", Note = "",
                TitlePy = PinyinHelper.GetInitials("血肉枪"), IsPinned = false, RecentOrder = -1
            });
            palEntries.Add(new PaletteEntry
            {
                PinKey = MakePinKey("角色表", "置顶常用"),
                SourceTab = "角色表", Title = "置顶常用", Body = "!fs ^ mtf", Alt = "", Ulx = "", Note = "",
                TitlePy = PinyinHelper.GetInitials("置顶常用"), IsPinned = true, RecentOrder = -1
            });
            var topPinned = CommandPaletteForm.ScoreAndFilter(palEntries, "", 10);
            var hitPy = CommandPaletteForm.ScoreAndFilter(palEntries, "xrq", 10);
            var hitAnd = CommandPaletteForm.ScoreAndFilter(palEntries, "物品 xrq", 10);
            chk("全局命令面板跨表检索评分与置顶排序",
                topPinned.Count >= 1 && topPinned[0].Title == "置顶常用"
                && hitPy.Count == 1 && hitPy[0].Title == "血肉枪"
                && hitAnd.Count == 1 && hitAnd[0].Title == "血肉枪", "");

            palEntries[1].RecentOrder = 0;
            var emptyFull = CommandPaletteForm.ScoreAndFilter(palEntries, "", 10);
            chk("命令面板空查询不折叠（收藏 → 最近复制 → 普通条目完整保留）",
                emptyFull.Count == 3
                && emptyFull[0].Title == "置顶常用"
                && emptyFull[1].Title == "血肉枪"
                && emptyFull[2].Title == "标题精确", "");

            chk("自定义动态目标替换 ApplyCustomTarget",
                ApplyCustomTarget("!give {target} gun", "John Doe") == "!give \"John Doe\" gun"
                && ApplyCustomTarget("!tp ^", "Alice") == "!tp Alice"
                && ApplyCustomTarget("!give {target} gun", "") == "!give ^ gun", "");

            chk("常用收藏键与备份清单包含 pins.txt",
                MakePinKey("物品表", "血肉枪") == "物品表\u0001血肉枪"
                && Array.IndexOf(BackupFileNames, "pins.txt") >= 0, "");

            chk("命令面板半透明工具函数 Clamp 与预设轮换",
                CommandPaletteForm.ClampOpacityPct(10) == 55
                && CommandPaletteForm.ClampOpacityPct(120) == 100
                && CommandPaletteForm.ClampOpacityPct(86) == 86
                && CommandPaletteForm.NextOpacityPreset(86) == 74
                && CommandPaletteForm.NextOpacityPreset(100) == 86, "");

            var scrTest = new Rectangle(0, 0, 1920, 1080);
            var rcC = CommandPaletteForm.ComputeDockBounds(scrTest, CommandPaletteForm.DockCenter);
            var rcL = CommandPaletteForm.ComputeDockBounds(scrTest, CommandPaletteForm.DockLeft);
            var rcR = CommandPaletteForm.ComputeDockBounds(scrTest, CommandPaletteForm.DockRight);
            chk("命令面板三档弹出位置与抽屉尺寸计算",
                CommandPaletteForm.NormalizeDockMode(99) == CommandPaletteForm.DockCenter
                && CommandPaletteForm.NextDockMode(0) == 1
                && CommandPaletteForm.NextDockMode(1) == 2
                && CommandPaletteForm.NextDockMode(2) == 0
                && CommandPaletteForm.DockModeLabel(0) == "居中"
                && CommandPaletteForm.DockModeLabel(1) == "左侧"
                && CommandPaletteForm.DockModeLabel(2) == "右侧"
                && rcC.Width == 700 && rcC.Height == 470
                && rcL.X == 12 && rcL.Width == 456 && rcL.Height >= 520 && rcL.Height <= 920
                && rcR.Right == 1908 && rcR.Width == 456 && rcR.Height == rcL.Height, "");

            var pyRobot = PinyinHelper.BuildTitle("机器人");
            chk("智能拼音：全拼 / 首字母 / 混拼与防误搜边界对齐",
                PinyinHelper.MatchScore(pyRobot, "jqr") > 0
                && PinyinHelper.MatchScore(pyRobot, "jiqiren") > 0
                && PinyinHelper.MatchScore(pyRobot, "jiqr") > 0
                && PinyinHelper.MatchScore(pyRobot, "jiqir") > 0
                && PinyinHelper.MatchScore(pyRobot, "jqir") > 0
                && PinyinHelper.MatchScore(pyRobot, "jiqiren") > PinyinHelper.MatchScore(pyRobot, "jqr")
                && PinyinHelper.MatchScore(pyRobot, "iq") == 0
                && PinyinHelper.MatchScore(pyRobot, "ir") == 0
                && PinyinHelper.MatchScore(pyRobot, "iren") == 0
                && PinyinHelper.MatchScore(pyRobot, "jrn") == 0, "");

            chk("智能拼音：GBK 二级字与游戏常用生僻字补全",
                PinyinHelper.GetInitials("混沌分裂者") == "hdflz"
                && PinyinHelper.MatchScore("混沌分裂者", "hdflz") > 0
                && PinyinHelper.MatchScore("混沌分裂者", "hundun") > 0
                && PinyinHelper.MatchScore("混沌分裂者", "hundunflz") > 0
                && PinyinHelper.MatchScore("霰弹枪", "xdq") > 0
                && PinyinHelper.MatchScore("霰弹枪", "xiandan") > 0
                && PinyinHelper.MatchScore("霰弹枪", "xiandq") > 0
                && PinyinHelper.MatchScore("手铐", "sk") > 0
                && PinyinHelper.MatchScore("手铐", "shk") > 0
                && PinyinHelper.MatchScore("手铐", "shoukao") > 0, "");

            chk("智能拼音：多音字（重/血）与 lv/lu 别名支持",
                PinyinHelper.MatchScore("重装", "zz") > 0
                && PinyinHelper.MatchScore("重装", "zhongz") > 0
                && PinyinHelper.MatchScore("重装", "cz") > 0
                && PinyinHelper.MatchScore("重装", "chongz") > 0
                && PinyinHelper.MatchScore("血肉枪", "xrq") > 0
                && PinyinHelper.MatchScore("血肉枪", "xuerq") > 0
                && PinyinHelper.MatchScore("血肉枪", "xuerouqiang") > 0
                && PinyinHelper.MatchScore("血肉枪", "xierouqiang") > 0
                && PinyinHelper.MatchScore("绿卡", "lvka") > 0
                && PinyinHelper.MatchScore("绿卡", "luka") > 0
                && PinyinHelper.MatchScore("绿卡", "lk") > 0, "");

            var pyRankEntries = new List<PaletteEntry>();
            pyRankEntries.Add(new PaletteEntry
            {
                PinKey = MakePinKey("物品表", "暗黑步枪"),
                SourceTab = "物品表", Title = "暗黑步枪", Body = "!give ^ ahbq", Alt = "", Ulx = "", Note = "",
                TitlePy = PinyinHelper.GetInitials("暗黑步枪"), TitlePinyin = PinyinHelper.BuildTitle("暗黑步枪"),
                IsPinned = false, RecentOrder = -1
            });
            pyRankEntries.Add(new PaletteEntry
            {
                PinKey = MakePinKey("物品表", "阿卡特突击步枪"),
                SourceTab = "物品表", Title = "阿卡特突击步枪", Body = "!give ^ akt", Alt = "", Ulx = "", Note = "",
                TitlePy = PinyinHelper.GetInitials("阿卡特突击步枪"), TitlePinyin = PinyinHelper.BuildTitle("阿卡特突击步枪"),
                IsPinned = false, RecentOrder = -1
            });
            pyRankEntries.Add(new PaletteEntry
            {
                PinKey = MakePinKey("物品表", "AK47"),
                SourceTab = "物品表", Title = "AK47", Body = "!give ^ ak47", Alt = "", Ulx = "", Note = "",
                TitlePy = PinyinHelper.GetInitials("AK47"), TitlePinyin = PinyinHelper.BuildTitle("AK47"),
                IsPinned = false, RecentOrder = -1
            });
            pyRankEntries.Add(new PaletteEntry
            {
                PinKey = MakePinKey("物品表", "机器人"),
                SourceTab = "物品表", Title = "机器人", Body = "!give ^ bot", Alt = "", Ulx = "", Note = "",
                TitlePy = PinyinHelper.GetInitials("机器人"), TitlePinyin = PinyinHelper.BuildTitle("机器人"),
                IsPinned = false, RecentOrder = -1
            });
            var rankAk = CommandPaletteForm.ScoreAndFilter(pyRankEntries, "ak", 10);
            var rankJiqr = CommandPaletteForm.ScoreAndFilter(pyRankEntries, "jiqr", 10);
            chk("智能拼音：英文标题优先于拼音模糊匹配且混拼命中精确条目",
                rankAk.Count >= 1 && rankAk[0].Title == "AK47"
                && rankJiqr.Count == 1 && rankJiqr[0].Title == "机器人", "");

            bool themeMapOk = (ModeToSettingsThemeIndex(0) == 0 && ModeToSettingsThemeIndex(2) == 1 && ModeToSettingsThemeIndex(1) == 2
                && SettingsThemeIndexToMode(0) == 0 && SettingsThemeIndexToMode(1) == 2 && SettingsThemeIndexToMode(2) == 1);
            for (int tm = 0; tm <= 2; tm++)
            {
                if (SettingsThemeIndexToMode(ModeToSettingsThemeIndex(tm)) != tm) themeMapOk = false;
            }
            int segEvtCount = 0;
            using (var segT = new RxSegmentedControl())
            {
                segT.Items = new string[] { "跟随系统", "护眼浅色", "夜间深色" };
                segT.SelectedIndexChanged += delegate { segEvtCount++; };
                segT.SetSelectedSilent(2);
                segT.SetSelectedSilent(1);
                if (segT.SelectedIndex != 1 || segEvtCount != 0) themeMapOk = false;
            }
            chk("设置页主题分段索引与 themeMode 双向映射且静默同步不触发事件", themeMapOk, "");

            string iq1, iq2, iq3, iq4, iq5;
            bool p1 = CommandPaletteForm.TryParseInlinePlayerQuery("@", out iq1);
            bool p2 = CommandPaletteForm.TryParseInlinePlayerQuery("@张三", out iq2);
            bool p3 = CommandPaletteForm.TryParseInlinePlayerQuery("@  xm", out iq3);
            bool p4 = CommandPaletteForm.TryParseInlinePlayerQuery("xrq", out iq4);
            bool p5 = CommandPaletteForm.TryParseInlinePlayerQuery("", out iq5);
            string sl1 = CommandPaletteForm.ShortServerLabel("1服 · RXBreach (18/64)");
            string sl2 = CommandPaletteForm.ShortServerLabel("");
            string sl3 = CommandPaletteForm.ShortServerLabel("123456789012345678901234567890 (1/64)");
            chk("命令面板 @ 选人前缀解析与服务器胶囊短标题格式化",
                p1 && iq1 == ""
                && p2 && iq2 == "张三"
                && p3 && iq3 == "xm"
                && !p4 && iq4 == ""
                && !p5 && iq5 == ""
                && sl1 == "1服 · RXBreach"
                && sl2 == "选择服务器"
                && sl3 == "1234567890123456789…", "");
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
