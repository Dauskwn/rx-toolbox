using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

public partial class MainForm
{
    static readonly string[] BackupFileNames = new string[]
    {
        "phrases.json", "data-custom.txt", "custom.txt", "servers.txt", "settings.ini", "pins.txt"
    };

    string CurrentPhrasesPath
    {
        get { return string.IsNullOrEmpty(dataRootDir) ? "" : Path.Combine(dataRootDir, "phrases.json"); }
    }

    string PendingDataPath
    {
        get { return string.IsNullOrEmpty(dataRootDir) ? "" : Path.Combine(dataRootDir, "phrases.new.json"); }
    }

    string BackupDirPath
    {
        get { return string.IsNullOrEmpty(dataRootDir) ? "" : Path.Combine(dataRootDir, "backups"); }
    }

    public static bool TabListEquals(IList<string> cur, string[] baseArr)
    {
        int nCur = (cur == null) ? 0 : cur.Count;
        int nBase = (baseArr == null) ? 0 : baseArr.Length;
        if (nCur != nBase) return false;
        for (int i = 0; i < nCur; i++)
        {
            if (!string.Equals(cur[i] ?? "", baseArr[i] ?? "", StringComparison.Ordinal))
                return false;
        }
        return true;
    }

    static string TabDiffItemKey(string raw, Dictionary<string, int> seen, ref string curMajor, ref string curSub, out string titleOut, out string joinedOut)
    {
        string t, m, a, u, n;
        DevSplitItem(raw ?? "", out t, out m, out a, out u, out n);
        titleOut = t;
        joinedOut = DevJoinItem(t, m, a, u, n);
        string scopeKey;
        if (m.Length == 0)
        {
            if (t.StartsWith("▓")) { curMajor = t.Substring(1).Trim(); curSub = ""; }
            else curSub = t;
            scopeKey = "H|" + curMajor + "|" + t;
        }
        else
        {
            scopeKey = "I|" + curMajor + "|" + curSub + "|" + t;
        }
        int c0;
        seen.TryGetValue(scopeKey, out c0);
        seen[scopeKey] = c0 + 1;
        return scopeKey + "#" + c0;
    }

    public static void ComputeTabDiff(
        IList<string> curItems, IList<string> newItems,
        out int add, out int chg, out int del,
        List<string> addedTitles, List<string> changedTitles, List<string> removedTitles)
    {
        var before = new Dictionary<string, string>();
        var beforeTitles = new Dictionary<string, string>();
        var seen = new Dictionary<string, int>();
        string curMajor = "", curSub = "";
        if (curItems != null)
        {
            for (int i = 0; i < curItems.Count; i++)
            {
                string t, joined;
                string key = TabDiffItemKey(curItems[i], seen, ref curMajor, ref curSub, out t, out joined);
                before[key] = joined;
                beforeTitles[key] = t;
            }
        }

        add = 0; chg = 0; del = 0;
        var after = new Dictionary<string, string>();
        var seen2 = new Dictionary<string, int>();
        curMajor = ""; curSub = "";
        if (newItems != null)
        {
            for (int i = 0; i < newItems.Count; i++)
            {
                string t, joined;
                string key = TabDiffItemKey(newItems[i], seen2, ref curMajor, ref curSub, out t, out joined);
                after[key] = joined;
                string oldCmd;
                if (!before.TryGetValue(key, out oldCmd))
                {
                    add++;
                    if (addedTitles != null) addedTitles.Add(t);
                }
                else if (oldCmd != joined)
                {
                    chg++;
                    if (changedTitles != null) changedTitles.Add(t);
                }
            }
        }
        foreach (var b in before)
        {
            if (!after.ContainsKey(b.Key))
            {
                del++;
                if (removedTitles != null) removedTitles.Add(beforeTitles[b.Key]);
            }
        }
    }

    public static string[] LoadOverrideTabNames(string overridePath)
    {
        var list = new List<string>();
        try
        {
            if (string.IsNullOrEmpty(overridePath) || !File.Exists(overridePath)) return new string[0];
            foreach (string raw in File.ReadAllLines(overridePath, Encoding.UTF8))
            {
                string line = raw ?? "";
                if (line.StartsWith("###", StringComparison.Ordinal))
                {
                    string name = line.Substring(3).Trim();
                    if (name.Length > 0 && !list.Contains(name)) list.Add(name);
                }
            }
        }
        catch { }
        return list.ToArray();
    }

    static bool AtomicWriteBytes(string path, byte[] bytes)
    {
        if (string.IsNullOrEmpty(path)) return false;
        string tmp = path + ".tmp";
        try
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllBytes(tmp, bytes ?? new byte[0]);
            if (File.Exists(path))
            {
                try { File.Replace(tmp, path, null); return true; }
                catch { File.Copy(tmp, path, true); return true; }
            }
            try { File.Move(tmp, path); return true; }
            catch { File.Copy(tmp, path, true); return true; }
        }
        catch (Exception ex)
        {
            Native.Log("AtomicWriteBytes fail (" + path + "): " + ex.Message);
            return false;
        }
        finally
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
        }
    }

    static byte[] RedactHubKeyFromSettings(byte[] raw)
    {
        if (raw == null || raw.Length == 0) return raw;
        try
        {
            string text = Encoding.UTF8.GetString(raw);
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            var kept = new List<string>(lines.Length);
            bool changed = false;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                int eq = line.IndexOf('=');
                string sk = eq > 0 ? line.Substring(0, eq).Trim() : "";
                if (sk == "HubKey" || sk == "HubKeyEnc")
                {
                    changed = true;
                    continue;
                }
                kept.Add(line);
            }
            if (!changed) return raw;
            return new UTF8Encoding(false).GetBytes(string.Join("\r\n", kept.ToArray()));
        }
        catch { return raw; }
    }

    public static bool CreateBackupArchiveToFile(
        string destPath, string dataDir, string exeDir, string customCrossPath,
        string kind, string note, out string err)
    {
        err = "";
        try
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"format\": \"rxtoolbox-backup-1\",\n");
            sb.Append("  \"created\": \"").Append(JsonEsc(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"))).Append("\",\n");
            sb.Append("  \"version\": \"").Append(JsonEsc(APP_VERSION_NUM)).Append("\",\n");
            sb.Append("  \"kind\": \"").Append(JsonEsc(string.IsNullOrEmpty(kind) ? "manual" : kind)).Append("\",\n");
            sb.Append("  \"note\": \"").Append(JsonEsc(note ?? "")).Append("\",\n");
            sb.Append("  \"files\": {\n");

            bool first = true;
            Action<string, byte[]> writeEntry = delegate(string key, byte[] raw)
            {
                if (!first) sb.Append(",\n");
                first = false;
                sb.Append("    \"").Append(JsonEsc(key)).Append("\": \"")
                  .Append(Convert.ToBase64String(raw ?? new byte[0])).Append("\"");
            };

            for (int i = 0; i < BackupFileNames.Length; i++)
            {
                string fn = BackupFileNames[i];
                string src = string.IsNullOrEmpty(dataDir) ? "" : Path.Combine(dataDir, fn);
                if ((!File.Exists(src)) && fn == "phrases.json" && !string.IsNullOrEmpty(exeDir))
                    src = Path.Combine(exeDir, fn);
                if (!string.IsNullOrEmpty(src) && File.Exists(src))
                {
                    byte[] rawBytes = File.ReadAllBytes(src);
                    if (fn == "settings.ini") rawBytes = RedactHubKeyFromSettings(rawBytes);
                    writeEntry(fn, rawBytes);
                }
            }

            string crossSrc = "";
            if (!string.IsNullOrEmpty(customCrossPath) && File.Exists(customCrossPath))
                crossSrc = customCrossPath;
            else if (!string.IsNullOrEmpty(dataDir) && File.Exists(Path.Combine(dataDir, "crosshair.png")))
                crossSrc = Path.Combine(dataDir, "crosshair.png");
            if (!string.IsNullOrEmpty(crossSrc))
                writeEntry("crosshair.png.b64", File.ReadAllBytes(crossSrc));

            sb.Append("\n  }\n}\n");
            if (!AtomicWriteText(destPath, sb.ToString()))
            {
                err = "写入备份文件失败";
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            err = ex.Message;
            return false;
        }
    }

    static Dictionary<string, object> ParseBackupEnvelope(
        string path, out string created, out string version, out string kind, out string note,
        out Dictionary<string, object> rawMap, out string err)
    {
        created = ""; version = ""; kind = ""; note = "";
        rawMap = null;
        err = "";
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            err = "备份文件不存在";
            return null;
        }
        string raw = File.ReadAllText(path, Encoding.UTF8);
        var root = MiniJson.Parse(raw) as Dictionary<string, object>;
        if (root == null)
        {
            err = "备份文件 JSON 格式无效";
            return null;
        }
        object fObj;
        if (!root.TryGetValue("format", out fObj) || Convert.ToString(fObj) != "rxtoolbox-backup-1")
        {
            err = "不支持的备份文件格式";
            return null;
        }
        object o;
        if (root.TryGetValue("created", out o) && o != null) created = Convert.ToString(o);
        if (root.TryGetValue("version", out o) && o != null) version = Convert.ToString(o);
        if (root.TryGetValue("kind", out o) && o != null) kind = Convert.ToString(o);
        if (root.TryGetValue("note", out o) && o != null) note = Convert.ToString(o);

        object filesObj;
        if (!root.TryGetValue("files", out filesObj))
        {
            err = "备份文件中缺少内容清单";
            return null;
        }
        rawMap = filesObj as Dictionary<string, object>;
        if (rawMap == null)
        {
            err = "备份文件内容清单格式错误";
            return null;
        }
        return root;
    }

    public static bool ParseBackupSummary(
        string path, out string created, out string version, out string kind, out string note,
        out List<string> fileKeys, out string err)
    {
        created = ""; version = ""; kind = ""; note = "";
        fileKeys = new List<string>();
        err = "";
        try
        {
            Dictionary<string, object> rawMap;
            if (ParseBackupEnvelope(path, out created, out version, out kind, out note, out rawMap, out err) == null)
                return false;
            foreach (var kv in rawMap) fileKeys.Add(kv.Key);
            if (fileKeys.Count == 0)
            {
                err = "备份文件中不包含任何可还原数据";
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            err = ex.Message;
            return false;
        }
    }

    public static bool ParseBackupArchive(
        string path, out string created, out string version, out string kind, out string note,
        out Dictionary<string, byte[]> files, out string err)
    {
        created = ""; version = ""; kind = ""; note = "";
        files = new Dictionary<string, byte[]>();
        err = "";
        try
        {
            Dictionary<string, object> map;
            if (ParseBackupEnvelope(path, out created, out version, out kind, out note, out map, out err) == null)
                return false;
            foreach (var kv in map)
            {
                string b64 = kv.Value == null ? "" : Convert.ToString(kv.Value);
                byte[] decoded = Convert.FromBase64String(b64);
                if (kv.Key == "phrases.json")
                {
                    string jsonStr = Encoding.UTF8.GetString(decoded);
                    if (decoded.Length > 0 && Data.ParseTables(jsonStr) == null)
                    {
                        err = "备份中的 phrases.json 数据已损坏";
                        return false;
                    }
                }
                files[kv.Key] = decoded;
            }
            if (files.Count == 0)
            {
                err = "备份文件中不包含任何可还原数据";
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            err = ex.Message;
            return false;
        }
    }

    public static int PruneAutoBackups(string backupDir, int keepMax)
    {
        int removed = 0;
        if (string.IsNullOrEmpty(backupDir) || !Directory.Exists(backupDir) || keepMax < 0) return 0;
        try
        {
            var autoFiles = new List<string>(Directory.GetFiles(backupDir, "*_auto*.rxbak"));
            autoFiles.Sort(delegate(string a, string b) { return string.CompareOrdinal(b, a); }); // 降序：最新在前
            for (int i = keepMax; i < autoFiles.Count; i++)
            {
                try { File.Delete(autoFiles[i]); removed++; } catch { }
            }
        }
        catch { }
        return removed;
    }

    public static bool RestoreBackupFiles(
        string dataDir, Dictionary<string, byte[]> files,
        bool restoreCustom, bool restoreOverrides, bool restorePhrases,
        bool restoreSettings, bool restoreServers, bool restoreCrossImg,
        out string err)
    {
        err = "";
        if (string.IsNullOrEmpty(dataDir) || files == null)
        {
            err = "无效的目标目录或备份内容";
            return false;
        }
        try
        {
            if (restoreCustom && files.ContainsKey("custom.txt"))
                if (!AtomicWriteBytes(Path.Combine(dataDir, "custom.txt"), files["custom.txt"]))
                    throw new Exception("写入 custom.txt 失败");

            if (restoreOverrides && files.ContainsKey("data-custom.txt"))
                if (!AtomicWriteBytes(Path.Combine(dataDir, "data-custom.txt"), files["data-custom.txt"]))
                    throw new Exception("写入 data-custom.txt 失败");

            if (restorePhrases && files.ContainsKey("phrases.json"))
                if (!AtomicWriteBytes(Path.Combine(dataDir, "phrases.json"), files["phrases.json"]))
                    throw new Exception("写入 phrases.json 失败");

            if (restoreServers && files.ContainsKey("servers.txt"))
                if (!AtomicWriteBytes(Path.Combine(dataDir, "servers.txt"), files["servers.txt"]))
                    throw new Exception("写入 servers.txt 失败");

            if (restoreCrossImg && files.ContainsKey("crosshair.png.b64"))
                if (!AtomicWriteBytes(Path.Combine(dataDir, "crosshair.png"), files["crosshair.png.b64"]))
                    throw new Exception("写入 crosshair.png 失败");

            if (restoreSettings && files.ContainsKey("settings.ini"))
            {
                string settingsContent = Encoding.UTF8.GetString(files["settings.ini"]);
                if (restoreCrossImg && files.ContainsKey("crosshair.png.b64"))
                {
                    string targetImg = Path.Combine(dataDir, "crosshair.png");
                    var lines = new List<string>(settingsContent.Replace("\r\n", "\n").Split('\n'));
                    bool replacedImg = false;
                    for (int i = 0; i < lines.Count; i++)
                    {
                        if (lines[i].TrimStart().StartsWith("CrossImg=", StringComparison.Ordinal))
                        {
                            lines[i] = "CrossImg=" + targetImg;
                            replacedImg = true;
                        }
                    }
                    if (!replacedImg) lines.Add("CrossImg=" + targetImg);
                    settingsContent = string.Join("\r\n", lines.ToArray());
                    if (!settingsContent.EndsWith("\r\n", StringComparison.Ordinal)) settingsContent += "\r\n";
                    if (!AtomicWriteText(Path.Combine(dataDir, "settings.ini"), settingsContent))
                        throw new Exception("写入 settings.ini 失败");
                }
                else
                {
                    if (!AtomicWriteBytes(Path.Combine(dataDir, "settings.ini"), files["settings.ini"]))
                        throw new Exception("写入 settings.ini 失败");
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            err = ex.Message;
            return false;
        }
    }

    public static bool StagePendingDataFile(string srcVerifiedFile, string pendingPath, out string err)
    {
        err = "";
        try
        {
            if (string.IsNullOrEmpty(srcVerifiedFile) || !File.Exists(srcVerifiedFile))
            {
                err = "找不到待暂存的新版数据文件";
                return false;
            }
            string txt = File.ReadAllText(srcVerifiedFile, Encoding.UTF8);
            if (Data.ParseTables(txt) == null)
            {
                err = "新版数据文件格式校验失败";
                return false;
            }
            TryDeleteFile(pendingPath, 2);
            MoveWithRetry(srcVerifiedFile, pendingPath, 3);
            return true;
        }
        catch (Exception ex)
        {
            err = ex.Message;
            return false;
        }
    }

    public static bool ApplyPendingDataToDisk(
        string pendingPath, string targetPhrasesPath, string overridePath,
        bool clearAllOverrides, ICollection<string> tabsToClearOverride, out string err)
    {
        err = "";
        try
        {
            if (string.IsNullOrEmpty(pendingPath) || !File.Exists(pendingPath))
            {
                err = "待应用的数据文件不存在";
                return false;
            }
            byte[] raw = File.ReadAllBytes(pendingPath);
            string txt = Encoding.UTF8.GetString(raw);
            if (Data.ParseTables(txt) == null)
            {
                err = "待应用数据文件格式无效";
                return false;
            }
            if (!AtomicWriteBytes(targetPhrasesPath, raw))
            {
                err = "写入 phrases.json 失败";
                return false;
            }

            if (clearAllOverrides)
            {
                if (!string.IsNullOrEmpty(overridePath)) AtomicWriteText(overridePath, "");
            }
            else if (tabsToClearOverride != null && tabsToClearOverride.Count > 0 && !string.IsNullOrEmpty(overridePath) && File.Exists(overridePath))
            {
                var lines = File.ReadAllLines(overridePath, Encoding.UTF8);
                var sb = new StringBuilder();
                string cur = null;
                bool keepCur = true;
                var clearSet = new HashSet<string>(tabsToClearOverride, StringComparer.Ordinal);
                for (int i = 0; i < lines.Length; i++)
                {
                    string ln = lines[i] ?? "";
                    if (ln.StartsWith("###", StringComparison.Ordinal))
                    {
                        cur = ln.Substring(3).Trim();
                        keepCur = !clearSet.Contains(cur);
                        if (keepCur) sb.Append(ln).Append("\r\n");
                        continue;
                    }
                    if (cur != null && keepCur) sb.Append(ln).Append("\r\n");
                }
                AtomicWriteText(overridePath, sb.ToString());
            }

            TryDeleteFile(pendingPath, 2);
            return true;
        }
        catch (Exception ex)
        {
            err = ex.Message;
            return false;
        }
    }

    bool CreateBackupSnapshot(string kind, string note, out string createdFile)
    {
        createdFile = "";
        string dir = BackupDirPath;
        if (string.IsNullOrEmpty(dir)) return false;
        SaveSettings();
        string tag = (kind == "auto") ? "auto" : "manual";
        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        string path = Path.Combine(dir, stamp + "_" + tag + ".rxbak");
        for (int n = 1; File.Exists(path) && n < 100; n++)
            path = Path.Combine(dir, stamp + "_" + n + "_" + tag + ".rxbak");

        string crossImg = (overlay != null) ? overlay.CustomImagePath : "";
        string exeDir = Path.GetDirectoryName(Application.ExecutablePath);
        string err;
        if (!CreateBackupArchiveToFile(path, dataRootDir, exeDir, crossImg, tag, note, out err))
        {
            Native.Log("backup fail (" + tag + "): " + err);
            return false;
        }
        createdFile = path;
        if (tag == "auto") PruneAutoBackups(dir, 10);
        Native.Log("backup ok (" + tag + "): " + Path.GetFileName(path) + " | " + note);
        return true;
    }

    void QuickManualBackup()
    {
        string note = PromptText("一键备份本地数据", "备份备注（可选，方便日后辨认）：", "手动备份");
        if (note == null) return;
        string path;
        if (CreateBackupSnapshot("manual", note.Length == 0 ? "手动备份" : note, out path))
        {
            if (toast != null) toast.ShowText("已备份至 backups\\" + Path.GetFileName(path));
        }
        else
        {
            MessageBox.Show(this, "备份失败，请确认当前目录可写。", "备份失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    void UpdatePendingDataStateUi()
    {
        if (lblPendingDataState == null) return;
        bool hasPend = !string.IsNullOrEmpty(PendingDataPath) && File.Exists(PendingDataPath);
        if (hasPend)
        {
            lblPendingDataState.Text = "⚠ 本地有待应用的新版数据（phrases.new.json），点左侧管理查看";
            SetDynamicLabelColor(lblPendingDataState, Color.FromArgb(184, 88, 44));
        }
        else
        {
            lblPendingDataState.Text = "支持手动/自动备份全部设置与文案，随时可选恢复";
            SetDynamicLabelColor(lblPendingDataState, Color.FromArgb(110, 122, 140));
        }
    }

    void CheckPendingDataOnStartup()
    {
        try
        {
            string pend = PendingDataPath;
            if (string.IsNullOrEmpty(pend) || !File.Exists(pend))
            {
                UpdatePendingDataStateUi();
                return;
            }
            string cur = File.Exists(CurrentPhrasesPath) ? CurrentPhrasesPath : Data.SourcePath;
            if (!string.IsNullOrEmpty(cur) && File.Exists(cur))
            {
                if (string.Equals(Sha256File(pend), Sha256File(cur), StringComparison.OrdinalIgnoreCase))
                {
                    TryDeleteFile(pend, 1);
                    UpdatePendingDataStateUi();
                    return;
                }
            }
            UpdatePendingDataStateUi();
            if (toast != null)
                toast.ShowText("本地有待查看的新版指令表（phrases.new.json），可在「设置 → 数据对比与备份管理」中决定是否替换");
        }
        catch { }
    }

    bool DecideDataUpdateForSelfUpdate(bool dataChanged, string newVer, out bool replaceDataNow, out bool keepEditedTabs)
    {
        replaceDataNow = true;
        keepEditedTabs = true;

        if (!dataChanged)
        {
            return MessageBox.Show(this,
                "更新到 v" + newVer + "？\r\n\r\n" +
                "程序会自动下载、校验、原地替换并重启，你的设置和自定义数据都会保留。",
                "自动更新", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK;
        }

        if (dataUpdateMode == 1)
        {
            return MessageBox.Show(this,
                "更新到 v" + newVer + "（含数据表更新）？\r\n\r\n" +
                "将先自动备份旧数据，再更新未修改的官方页签；你在本地编辑过的页签、自定义文案与设置都会保留。",
                "自动更新", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK;
        }

        if (dataUpdateMode == 2)
        {
            replaceDataNow = false;
            return MessageBox.Show(this,
                "更新到 v" + newVer + "？\r\n\r\n" +
                "按你的设置，新版指令表将先下载保存为 phrases.new.json，不自动替换当前指令表。",
                "自动更新", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK;
        }

        var overrides = LoadDataOverrides();
        return PromptDataUpdateDialog(newVer, overrides, out replaceDataNow, out keepEditedTabs);
    }

    bool PromptDataUpdateDialog(string newVer, Dictionary<string, List<string>> overrides, out bool replaceNow, out bool keepEditedTabs)
    {
        replaceNow = true;
        keepEditedTabs = true;

        using (var dlg = new Form())
        {
            dlg.Text = "确认更新到 v" + newVer;
            dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
            dlg.StartPosition = FormStartPosition.CenterParent;
            dlg.MinimizeBox = false;
            dlg.MaximizeBox = false;
            bool hasOv = (overrides != null && overrides.Count > 0);
            dlg.ClientSize = new Size(560, hasOv ? 344 : 308);
            dlg.Font = TrackFont("Microsoft YaHei UI", 9.5f, FontStyle.Regular);

            int y = 16;
            var lblHead = new Label();
            lblHead.Text = "新版 v" + newVer + " 同时包含「指令/物品等数据表（phrases.json）」更新。";
            lblHead.Font = TrackFont("Microsoft YaHei UI", 10f, FontStyle.Bold);
            lblHead.ForeColor = Color.FromArgb(26, 58, 104);
            lblHead.SetBounds(16, y, 528, 24);
            dlg.Controls.Add(lblHead);
            y += 28;

            var lblDesc = new Label();
            if (hasOv)
            {
                var names = new List<string>(overrides.Keys);
                lblDesc.Text = "检测到你曾在本机编辑过：" + string.Join("、", names.ToArray()) +
                               "\r\n（你的“自定义”页文案、设置、服务器收藏无论怎么选都会完整保留）";
                lblDesc.Height = 40;
            }
            else
            {
                lblDesc.Text = "新版指令表会先完整下载并校验到你本地，请选择本次更新后的处理方式\r\n（你的“自定义”页文案、设置、服务器收藏无论怎么选都会完整保留）：";
                lblDesc.Height = 40;
            }
            lblDesc.ForeColor = Color.FromArgb(75, 85, 102);
            lblDesc.SetBounds(16, y, 528, 40);
            dlg.Controls.Add(lblDesc);
            y += 48;

            var rbReplace = new RadioButton();
            rbReplace.Text = "下载并启用新版数据表（替换前会自动在 backups\\ 生成完整备份）";
            rbReplace.Font = TrackFont("Microsoft YaHei UI", 9.5f, FontStyle.Bold);
            rbReplace.SetBounds(16, y, 528, 24);
            rbReplace.Checked = true;
            dlg.Controls.Add(rbReplace);
            y += 28;

            var chkKeepOv = new CheckBox();
            chkKeepOv.Text = "保留我在本机编辑过的上述页签（仅更新未改过的页；不勾则全部采用新版）";
            chkKeepOv.SetBounds(36, y, 504, 24);
            chkKeepOv.Checked = true;
            chkKeepOv.Visible = hasOv;
            dlg.Controls.Add(chkKeepOv);
            if (hasOv) y += 30;

            var rbStage = new RadioButton();
            rbStage.Text = "先下载新版指令表到本地（存为 phrases.new.json），暂不替换当前列表";
            rbStage.Font = TrackFont("Microsoft YaHei UI", 9.5f, FontStyle.Bold);
            rbStage.SetBounds(16, y, 528, 24);
            dlg.Controls.Add(rbStage);
            y += 26;

            var lblStageTip = new Label();
            lblStageTip.Text = "更新重启后当前表保持原样，可随时到「关于 → 数据对比与备份管理」\r\n查看差异并手动应用。";
            lblStageTip.ForeColor = Color.FromArgb(110, 122, 140);
            lblStageTip.SetBounds(36, y, 504, 42);
            dlg.Controls.Add(lblStageTip);
            y += 48;

            rbReplace.CheckedChanged += delegate { chkKeepOv.Enabled = rbReplace.Checked; };

            var bOk = new Button();
            bOk.Text = "开始更新";
            bOk.DialogResult = DialogResult.OK;
            bOk.SetBounds(348, dlg.ClientSize.Height - 46, 96, 32);
            dlg.Controls.Add(bOk);

            var bCancel = new Button();
            bCancel.Text = "取消";
            bCancel.DialogResult = DialogResult.Cancel;
            bCancel.SetBounds(452, dlg.ClientSize.Height - 46, 92, 32);
            dlg.Controls.Add(bCancel);

            dlg.AcceptButton = bOk;
            dlg.CancelButton = bCancel;
            ThemeDialog(dlg);

            if (dlg.ShowDialog(this) != DialogResult.OK) return false;
            replaceNow = rbReplace.Checked;
            keepEditedTabs = chkKeepOv.Checked;
            return true;
        }
    }

    bool PromptRestoreChecklistDialog(
        IWin32Window owner, string title, string created, string note, Dictionary<string, byte[]> files,
        out bool rCustom, out bool rOverrides, out bool rPhrases,
        out bool rSettings, out bool rServers, out bool rCrossImg)
    {
        rCustom = rOverrides = rPhrases = rSettings = rServers = rCrossImg = false;
        using (var dlg = new Form())
        {
            dlg.Text = title;
            dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
            dlg.StartPosition = FormStartPosition.CenterParent;
            dlg.MinimizeBox = false;
            dlg.MaximizeBox = false;
            dlg.Font = TrackFont("Microsoft YaHei UI", 9.5f, FontStyle.Regular);

            var lblMeta = new Label();
            lblMeta.Text = "备份时间：" + (string.IsNullOrEmpty(created) ? "未知" : created)
                         + (string.IsNullOrEmpty(note) ? "" : ("　·　" + note));
            lblMeta.ForeColor = Color.FromArgb(40, 74, 120);
            lblMeta.SetBounds(16, 14, 448, 22);
            dlg.Controls.Add(lblMeta);

            var lblWarn = new Label();
            lblWarn.Text = "请勾选要还原的内容（还原前会自动备份当前配置到 backups\\，可随时后悔撤回）：";
            lblWarn.ForeColor = Color.FromArgb(110, 120, 136);
            lblWarn.SetBounds(16, 40, 448, 36);
            dlg.Controls.Add(lblWarn);

            int y = 84;
            Func<string, string, CheckBox> addChk = delegate(string key, string label)
            {
                if (!files.ContainsKey(key)) return null;
                var cb = new CheckBox();
                cb.Text = label;
                cb.Checked = true;
                cb.SetBounds(20, y, 440, 24);
                dlg.Controls.Add(cb);
                y += 28;
                return cb;
            };

            CheckBox cCustom = addChk("custom.txt", "「自定义」页全部文案 (custom.txt)");
            CheckBox cOv = addChk("data-custom.txt", "用户编辑过的指令表修改 (data-custom.txt)");
            CheckBox cPhr = addChk("phrases.json", "官方基准指令/物品数据表 (phrases.json)");
            CheckBox cSet = addChk("settings.ini", "软件设置、主题、热键与倒计时方案 (settings.ini)");
            CheckBox cSrv = addChk("servers.txt", "服务器收藏夹 (servers.txt)");
            CheckBox cImg = addChk("crosshair.png.b64", "自定义准星图片 (crosshair.png)");

            dlg.ClientSize = new Size(480, y + 60);

            var bOk = new Button();
            bOk.Text = "确定还原";
            bOk.DialogResult = DialogResult.OK;
            bOk.SetBounds(268, y + 14, 96, 32);
            dlg.Controls.Add(bOk);

            var bCancel = new Button();
            bCancel.Text = "取消";
            bCancel.DialogResult = DialogResult.Cancel;
            bCancel.SetBounds(372, y + 14, 92, 32);
            dlg.Controls.Add(bCancel);

            dlg.AcceptButton = bOk;
            dlg.CancelButton = bCancel;
            ThemeDialog(dlg);

            if (dlg.ShowDialog(owner) != DialogResult.OK) return false;
            rCustom = (cCustom != null && cCustom.Checked);
            rOverrides = (cOv != null && cOv.Checked);
            rPhrases = (cPhr != null && cPhr.Checked);
            rSettings = (cSet != null && cSet.Checked);
            rServers = (cSrv != null && cSrv.Checked);
            rCrossImg = (cImg != null && cImg.Checked);
            return rCustom || rOverrides || rPhrases || rSettings || rServers || rCrossImg;
        }
    }

    void HotReloadRestoredState(bool customChanged, bool dataChanged, bool settingsChanged, bool serversChanged, bool crossImgChanged)
    {
        try
        {
            if (customChanged)
            {
                cTitles = new string[0];
                cTexts = new string[0];
                LoadCustom();
                RebuildCustomPage();
            }

            if (dataChanged)
            {
                string phrasesFile = (!string.IsNullOrEmpty(CurrentPhrasesPath) && File.Exists(CurrentPhrasesPath))
                    ? CurrentPhrasesPath : Data.SourcePath;
                if (!string.IsNullOrEmpty(phrasesFile)) Data.Load(phrasesFile);
                var overrides = LoadDataOverrides();
                for (int i = 0; i < dataTabLabels.Length; i++)
                {
                    string lbl = dataTabLabels[i];
                    string[] baseData = GetLoadedDataArrayForTab(lbl);
                    dataTabBase[i] = baseData;
                    List<string> ovList;
                    if (overrides.TryGetValue(lbl, out ovList))
                        dataTabLists[i] = new List<string>(ovList);
                    else
                        dataTabLists[i] = new List<string>(baseData);
                    RebuildDataPage(i, false);
                }
            }

            if (serversChanged)
            {
                favList = new string[0];
                favCount = new int[0];
                favName = new string[0];
                favMax = new int[0];
                LoadFavs();
                FillSrvList();
                ApplyModeUi();
            }

            if (settingsChanged)
            {
                crossImgToLoad = "";
                LoadSettings();
                UpdateTrayIconVisibility();
                ApplyDevToolsTab(devToolsOn);
                ApplyTheme();
                FillSrvList();
                ApplyModeUi();
                UpdateHotkeyLabels();
                if (!hotkeysOff) ApplyHotkeys();
                if (!string.IsNullOrEmpty(crossImgToLoad) && File.Exists(crossImgToLoad))
                    LoadCrosshairImage(crossImgToLoad, true);
                else if (crossImgChanged && File.Exists(Path.Combine(dataRootDir, "crosshair.png")))
                    LoadCrosshairImage(Path.Combine(dataRootDir, "crosshair.png"), true);
                else if (overlay != null && !string.IsNullOrEmpty(overlay.CustomImagePath))
                    ClearCrosshairImage();
                SyncCrossPresetUiState();
                SyncSettingsPageUi();
            }
            else if (crossImgChanged && File.Exists(Path.Combine(dataRootDir, "crosshair.png")))
            {
                LoadCrosshairImage(Path.Combine(dataRootDir, "crosshair.png"), true);
            }

            UpdatePendingDataStateUi();
            SyncSettingsPageUi();
            RefreshUi(true);
        }
        catch (Exception ex)
        {
            Native.Log("hot reload restore EX: " + ex.Message);
        }
    }

    static string[] GetLoadedDataArrayForTab(string label)
    {
        if (label == "指令表") return Data.Command ?? new string[0];
        if (label == "技能表") return Data.Ability ?? new string[0];
        if (label == "物品表") return Data.Weapon ?? new string[0];
        if (label == "模型表") return Data.Model ?? new string[0];
        if (label == "角色表") return Data.Job ?? new string[0];
        if (label == "SCP角色") return Data.Scp ?? new string[0];
        return new string[0];
    }

    void ShowDiffDetailsDialog(IWin32Window owner, string title, Dictionary<string, string[]> targetTables, string sourceDesc)
    {
        var sb = new StringBuilder();
        sb.Append("对比基准：").Append(sourceDesc).Append("\r\n");
        sb.Append("----------------------------------------\r\n\r\n");
        bool any = false;
        for (int i = 0; i < dataTabLabels.Length; i++)
        {
            string tab = dataTabLabels[i];
            string[] targetArr;
            if (!targetTables.TryGetValue(tab, out targetArr) || targetArr == null) continue;
            var added = new List<string>();
            var changed = new List<string>();
            var removed = new List<string>();
            int a, c, d;
            ComputeTabDiff(dataTabLists[i], targetArr, out a, out c, out d, added, changed, removed);
            if (a == 0 && c == 0 && d == 0) continue;
            any = true;
            sb.Append("【").Append(tab).Append("】  新增 ").Append(a)
              .Append(" / 修改 ").Append(c).Append(" / 移除 ").Append(d).Append("\r\n");
            int limit = 25;
            for (int k = 0; k < added.Count && k < limit; k++) sb.Append("  + 新增：").Append(added[k]).Append("\r\n");
            if (added.Count > limit) sb.Append("  … 其余 ").Append(added.Count - limit).Append(" 条新增\r\n");
            for (int k = 0; k < changed.Count && k < limit; k++) sb.Append("  ~ 修改：").Append(changed[k]).Append("\r\n");
            if (changed.Count > limit) sb.Append("  … 其余 ").Append(changed.Count - limit).Append(" 条修改\r\n");
            for (int k = 0; k < removed.Count && k < limit; k++) sb.Append("  - 移除：").Append(removed[k]).Append("\r\n");
            if (removed.Count > limit) sb.Append("  … 其余 ").Append(removed.Count - limit).Append(" 条移除\r\n");
            sb.Append("\r\n");
        }
        if (!any) sb.Append("当前所有页面条目与目标数据完全一致，没有任何差异。");

        using (var f = new Form())
        {
            f.Text = title;
            f.FormBorderStyle = FormBorderStyle.Sizable;
            f.StartPosition = FormStartPosition.CenterParent;
            f.MinimumSize = new Size(520, 380);
            f.ClientSize = new Size(640, 480);
            f.Font = TrackFont("Microsoft YaHei UI", 9.5f, FontStyle.Regular);

            var tb = new TextBox();
            tb.Multiline = true;
            tb.ReadOnly = true;
            tb.ScrollBars = ScrollBars.Both;
            tb.WordWrap = false;
            tb.Dock = DockStyle.Fill;
            tb.Font = TrackFont("Consolas", 9.5f, FontStyle.Regular);
            tb.Text = sb.ToString();
            f.Controls.Add(tb);

            var bar = new Panel();
            bar.Dock = DockStyle.Bottom;
            bar.Height = 46;
            var bClose = new Button();
            bClose.Text = "关闭";
            bClose.DialogResult = DialogResult.OK;
            bClose.SetBounds(530, 8, 96, 30);
            bClose.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            bar.Controls.Add(bClose);
            f.Controls.Add(bar);

            f.AcceptButton = bClose;
            f.CancelButton = bClose;
            ThemeDialog(f);
            tb.Select(0, 0);
            f.ShowDialog(owner);
        }
    }

    void ShowDataBackupManager(IWin32Window owner)
    {
        using (var dlg = new Form())
        {
            dlg.Text = "数据对比与本地备份管理";
            dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
            dlg.StartPosition = FormStartPosition.CenterParent;
            dlg.MinimizeBox = false;
            dlg.MaximizeBox = false;
            dlg.ClientSize = new Size(780, 620);
            dlg.Font = TrackFont("Microsoft YaHei UI", 9.5f, FontStyle.Regular);

            bool netBusy = false;
            bool dlgClosed = false;
            dlg.FormClosed += delegate { dlgClosed = true; };

            var lblPendingInfo = new Label();
            lblPendingInfo.SetBounds(14, 12, 752, 22);
            lblPendingInfo.AutoEllipsis = true;
            lblPendingInfo.Font = TrackFont("Microsoft YaHei UI", 9.5f, FontStyle.Bold);
            dlg.Controls.Add(lblPendingInfo);

            var btnFetchData = new Button(); btnFetchData.Text = "检查/拉取最新数据"; btnFetchData.SetBounds(14, 40, 146, 30);
            var btnDiffDetail = new Button(); btnDiffDetail.Text = "查看详细差异"; btnDiffDetail.SetBounds(166, 40, 112, 30);
            var btnApplyKeep = new Button(); btnApplyKeep.Text = "采用新版(保留自改页)"; btnApplyKeep.SetBounds(284, 40, 166, 30);
            var btnApplyAll = new Button(); btnApplyAll.Text = "全部换新(清除自改)"; btnApplyAll.SetBounds(456, 40, 156, 30);
            var btnDiscardPend = new Button(); btnDiscardPend.Text = "放弃待更新暂存"; btnDiscardPend.SetBounds(618, 40, 148, 30);
            dlg.Controls.AddRange(new Control[] { btnFetchData, btnDiffDetail, btnApplyKeep, btnApplyAll, btnDiscardPend });

            var lvTabs = new BufferedListView();
            lvTabs.View = View.Details;
            lvTabs.FullRowSelect = true;
            lvTabs.GridLines = false;
            lvTabs.HideSelection = false;
            lvTabs.MultiSelect = false;
            lvTabs.SetBounds(14, 78, 752, 194);
            lvTabs.Columns.Add("数据表", 104);
            lvTabs.Columns.Add("当前来源", 156);
            lvTabs.Columns.Add("条目数", 78);
            lvTabs.Columns.Add("差异对比", 390);
            dlg.Controls.Add(lvTabs);

            var lblTabHint = new Label();
            lblTabHint.Text = "提示：仅改动过的页签记入 data-custom.txt，未改页随官方底表更新。";
            lblTabHint.ForeColor = Color.FromArgb(110, 122, 140);
            lblTabHint.SetBounds(14, 284, 514, 20);
            lblTabHint.AutoEllipsis = true;
            dlg.Controls.Add(lblTabHint);

            var btnClearOneTab = new Button();
            btnClearOneTab.Text = "清除选中页本地修改（还原官方）";
            btnClearOneTab.SetBounds(534, 280, 232, 28);
            dlg.Controls.Add(btnClearOneTab);

            var lblBakSec = new Label();
            lblBakSec.Text = "本地完整备份（保存与读取 .rxbak，涵盖：自定义文案 / 指令修改 / 底表 / 设置 / 收藏夹）";
            lblBakSec.Font = TrackFont("Microsoft YaHei UI", 9.5f, FontStyle.Bold);
            lblBakSec.ForeColor = Color.FromArgb(26, 58, 104);
            lblBakSec.SetBounds(14, 318, 752, 22);
            dlg.Controls.Add(lblBakSec);

            var lvBackups = new BufferedListView();
            lvBackups.View = View.Details;
            lvBackups.FullRowSelect = true;
            lvBackups.GridLines = false;
            lvBackups.HideSelection = false;
            lvBackups.MultiSelect = false;
            lvBackups.SetBounds(14, 344, 752, 182);
            lvBackups.Columns.Add("备份时间", 170);
            lvBackups.Columns.Add("类型", 60);
            lvBackups.Columns.Add("备注", 208);
            lvBackups.Columns.Add("包含内容", 290);
            dlg.Controls.Add(lvBackups);

            var btnBakCreate = new Button(); btnBakCreate.Text = "＋ 立即备份"; btnBakCreate.SetBounds(14, 536, 118, 32);
            var btnBakRestore = new Button(); btnBakRestore.Text = "还原选中…"; btnBakRestore.SetBounds(138, 536, 112, 32);
            var btnBakExport = new Button(); btnBakExport.Text = "导出当前(.rxbak)"; btnBakExport.SetBounds(256, 536, 136, 32);
            var btnBakImport = new Button(); btnBakImport.Text = "从外部读取…"; btnBakImport.SetBounds(398, 536, 126, 32);
            var btnBakDel = new Button(); btnBakDel.Text = "删除"; btnBakDel.SetBounds(530, 536, 86, 32);
            var btnBakDir = new Button(); btnBakDir.Text = "打开备份目录"; btnBakDir.SetBounds(622, 536, 144, 32);
            dlg.Controls.AddRange(new Control[] { btnBakCreate, btnBakRestore, btnBakExport, btnBakImport, btnBakDel, btnBakDir });

            var lblStatusBar = new Label();
            lblStatusBar.SetBounds(14, 583, 642, 22);
            lblStatusBar.AutoEllipsis = true;
            lblStatusBar.ForeColor = Color.FromArgb(90, 100, 116);
            dlg.Controls.Add(lblStatusBar);

            var btnClose = new Button();
            btnClose.Text = "关闭";
            btnClose.DialogResult = DialogResult.OK;
            btnClose.SetBounds(666, 578, 100, 32);
            dlg.Controls.Add(btnClose);

            Dictionary<string, string[]> pendingTables = null;
            List<string> backupPaths = new List<string>();

            Action refreshTabs = delegate
            {
                lvTabs.Items.Clear();
                pendingTables = null;
                bool hasPend = !string.IsNullOrEmpty(PendingDataPath) && File.Exists(PendingDataPath);
                if (hasPend)
                {
                    try { pendingTables = Data.ParseTables(File.ReadAllText(PendingDataPath, Encoding.UTF8)); }
                    catch { pendingTables = null; }
                }

                var ovSet = new HashSet<string>(LoadOverrideTabNames(dataOverridePath), StringComparer.Ordinal);
                int sumAdd = 0, sumChg = 0, sumDel = 0;

                for (int i = 0; i < dataTabLabels.Length; i++)
                {
                    string lbl = dataTabLabels[i];
                    bool isOv = ovSet.Contains(lbl) || !TabListEquals(dataTabLists[i], i < dataTabBase.Length ? dataTabBase[i] : null);
                    string srcTxt = isOv ? "本地自定义修改" : "官方底表 (phrases.json)";

                    string diffTxt = "一致";
                    if (pendingTables != null)
                    {
                        string[] pArr;
                        if (pendingTables.TryGetValue(lbl, out pArr))
                        {
                            int a, c, d;
                            ComputeTabDiff(dataTabLists[i], pArr, out a, out c, out d, null, null, null);
                            sumAdd += a; sumChg += c; sumDel += d;
                            diffTxt = (a == 0 && c == 0 && d == 0) ? "与新版一致"
                                : string.Format("对比新版：新增 {0} / 修改 {1} / 移除 {2}", a, c, d);
                        }
                    }
                    else if (isOv && i < dataTabBase.Length)
                    {
                        int a, c, d;
                        ComputeTabDiff(dataTabBase[i], dataTabLists[i], out a, out c, out d, null, null, null);
                        diffTxt = string.Format("相对底表：新增 {0} / 修改 {1} / 移除 {2}", a, c, d);
                    }

                    var it = new ListViewItem(lbl);
                    it.Tag = isOv ? "ov" : "";
                    it.SubItems.Add(srcTxt);
                    it.SubItems.Add(dataTabLists[i].Count + " 条");
                    it.SubItems.Add(diffTxt);
                    lvTabs.Items.Add(it);
                }

                bool usablePend = (pendingTables != null);
                btnApplyKeep.Enabled = usablePend;
                btnApplyAll.Enabled = usablePend;
                btnDiscardPend.Enabled = hasPend;
                btnDiffDetail.Enabled = usablePend || ovSet.Count > 0;

                if (usablePend)
                {
                    lblPendingInfo.Text = string.Format("⚠ 检测到已下载的新版数据表（phrases.new.json）：合计新增 {0}、修改 {1}、移除 {2}", sumAdd, sumChg, sumDel);
                    SetDynamicLabelColor(lblPendingInfo, Color.FromArgb(184, 88, 44));
                }
                else
                {
                    lblPendingInfo.Text = "当前暂无待应用的新版数据（可点下方按钮检查最新官方数据或管理本地备份）";
                    SetDynamicLabelColor(lblPendingInfo, Color.FromArgb(26, 58, 104));
                }
                btnClearOneTab.Enabled = false;
            };

            lvTabs.SelectedIndexChanged += delegate
            {
                bool canClr = (lvTabs.SelectedItems.Count > 0 && Convert.ToString(lvTabs.SelectedItems[0].Tag) == "ov");
                btnClearOneTab.Enabled = canClr;
            };

            Action refreshBackups = delegate
            {
                lvBackups.Items.Clear();
                backupPaths.Clear();
                string dir = BackupDirPath;
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                {
                    var files = new List<string>(Directory.GetFiles(dir, "*.rxbak"));
                    files.Sort(delegate(string a, string b) { return string.CompareOrdinal(b, a); });
                    for (int i = 0; i < files.Count; i++)
                    {
                        string cr, vr, kd, nt, pErr;
                        List<string> bKeys;
                        if (!ParseBackupSummary(files[i], out cr, out vr, out kd, out nt, out bKeys, out pErr))
                            continue;
                        backupPaths.Add(files[i]);
                        var tags = new List<string>();
                        if (bKeys.Contains("custom.txt")) tags.Add("自定义文案");
                        if (bKeys.Contains("data-custom.txt")) tags.Add("指令修改");
                        if (bKeys.Contains("phrases.json")) tags.Add("底表");
                        if (bKeys.Contains("settings.ini")) tags.Add("设置");
                        if (bKeys.Contains("servers.txt")) tags.Add("收藏夹");
                        if (bKeys.Contains("crosshair.png.b64")) tags.Add("准星图");

                        var item = new ListViewItem(string.IsNullOrEmpty(cr) ? Path.GetFileNameWithoutExtension(files[i]) : cr);
                        item.SubItems.Add(kd == "auto" ? "自动" : "手动");
                        item.SubItems.Add(nt ?? "");
                        item.SubItems.Add(string.Join(" · ", tags.ToArray()));
                        lvBackups.Items.Add(item);
                    }
                }
                btnBakRestore.Enabled = lvBackups.SelectedItems.Count > 0;
                btnBakDel.Enabled = lvBackups.SelectedItems.Count > 0;
            };

            lvBackups.SelectedIndexChanged += delegate
            {
                bool hasSel = lvBackups.SelectedItems.Count > 0;
                btnBakRestore.Enabled = hasSel;
                btnBakDel.Enabled = hasSel;
            };

            btnFetchData.Click += delegate
            {
                if (netBusy) return;
                if (string.IsNullOrEmpty(updateUrl) || !IsAllowedUpdateUrl(updateUrl))
                {
                    lblStatusBar.Text = "未配置有效的 https:// 更新地址";
                    return;
                }
                netBusy = true;
                btnFetchData.Enabled = false;
                lblStatusBar.Text = "正在查询并比对线上最新指令表…";
                string uUrl = updateUrl;
                var th = new Thread(delegate()
                {
                    string statusMsg = "";
                    try
                    {
                        string json = HttpGetText(uUrl, 12000);
                        string dUrl = JsonField(json, "dataUrl");
                        string dSha = JsonField(json, "dataSha256");
                        if (!IsAllowedUpdateUrl(dUrl) || string.IsNullOrEmpty(dSha) || dSha.Length != 64)
                            throw new Exception("线上清单未提供有效的 https:// 数据表下载地址");

                        string curSha = File.Exists(CurrentPhrasesPath) ? Sha256File(CurrentPhrasesPath) : "";
                        if (string.Equals(curSha, dSha, StringComparison.OrdinalIgnoreCase))
                        {
                            TryDeleteFile(PendingDataPath, 1);
                            statusMsg = "线上数据表与本机当前基准一致（SHA256 相同），已是最新";
                        }
                        else
                        {
                            string tmp = PendingDataPath + ".upd";
                            TryDeleteFile(tmp, 1);
                            if (!DownloadAndVerify(dUrl, dSha, tmp))
                                throw new Exception("数据表下载或 SHA256 校验失败");
                            string sErr;
                            if (!StagePendingDataFile(tmp, PendingDataPath, out sErr))
                                throw new Exception(sErr);
                            statusMsg = "已下载最新官方数据到本地（phrases.new.json），请在上方表格查看差异并决定是否应用";
                        }
                    }
                    catch (Exception ex)
                    {
                        statusMsg = "检查/下载最新数据失败：" + ex.Message;
                    }

                    try
                    {
                        if (!dlgClosed && !dlg.IsDisposed && dlg.IsHandleCreated)
                        {
                            dlg.BeginInvoke((MethodInvoker)delegate
                            {
                                if (dlgClosed || dlg.IsDisposed) return;
                                netBusy = false;
                                btnFetchData.Enabled = true;
                                lblStatusBar.Text = statusMsg;
                                refreshTabs();
                                UpdatePendingDataStateUi();
                            });
                        }
                    }
                    catch { }
                });
                th.IsBackground = true;
                th.Start();
            };

            btnDiffDetail.Click += delegate
            {
                if (pendingTables != null)
                    ShowDiffDetailsDialog(dlg, "待应用新版数据差异详情 (phrases.new.json)", pendingTables, "当前页面内容 vs 待应用新版");
                else
                {
                    var baseMap = new Dictionary<string, string[]>();
                    for (int i = 0; i < dataTabLabels.Length; i++)
                        if (i < dataTabBase.Length) baseMap[dataTabLabels[i]] = dataTabBase[i];
                    ShowDiffDetailsDialog(dlg, "本地修改与当前底表差异", baseMap, "本地当前内容 vs 当前官方底表 (phrases.json)");
                }
            };

            btnApplyKeep.Click += delegate
            {
                if (pendingTables == null) return;
                if (MessageBox.Show(dlg,
                        "用已下载的 phrases.new.json 替换本地官方底表，并保留你已改过的页签？\r\n" +
                        "（替换前会自动创建一份完整本地备份，可随时恢复）",
                        "应用新版数据", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
                    return;
                string bakPath, err;
                CreateBackupSnapshot("auto", "应用新版数据(保留自改页)前自动备份", out bakPath);
                if (!ApplyPendingDataToDisk(PendingDataPath, CurrentPhrasesPath, dataOverridePath, false, null, out err))
                {
                    MessageBox.Show(dlg, "应用失败：" + err, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                HotReloadRestoredState(false, true, false, false, false);
                refreshTabs();
                refreshBackups();
                lblStatusBar.Text = "已应用新版数据到未修改页签，你的自定义页签已保留并自动备份";
            };

            btnApplyAll.Click += delegate
            {
                if (pendingTables == null) return;
                if (MessageBox.Show(dlg,
                        "全部替换为新版官方数据（将清空 data-custom.txt 中你对指令/物品等表的本地修改）？\r\n" +
                        "（替换前会自动创建完整备份，“自定义”页文案和设置不会受影响）",
                        "全部采用新版", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK)
                    return;
                string bakPath, err;
                CreateBackupSnapshot("auto", "全部换新前自动备份", out bakPath);
                if (!ApplyPendingDataToDisk(PendingDataPath, CurrentPhrasesPath, dataOverridePath, true, null, out err))
                {
                    MessageBox.Show(dlg, "替换失败：" + err, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                HotReloadRestoredState(false, true, false, false, false);
                refreshTabs();
                refreshBackups();
                lblStatusBar.Text = "已全部更新为新版官方数据并即时刷新";
            };

            btnDiscardPend.Click += delegate
            {
                if (MessageBox.Show(dlg, "删除本地暂存的 phrases.new.json（不影响正在使用的数据）？",
                        "删除暂存", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
                    return;
                TryDeleteFile(PendingDataPath, 2);
                refreshTabs();
                UpdatePendingDataStateUi();
                lblStatusBar.Text = "已删除待应用暂存文件";
            };

            btnClearOneTab.Click += delegate
            {
                if (lvTabs.SelectedItems.Count == 0) return;
                string tabName = lvTabs.SelectedItems[0].Text;
                int idx = Array.IndexOf(dataTabLabels, tabName);
                if (idx < 0 || idx >= dataTabBase.Length) return;
                if (MessageBox.Show(dlg, "清除「" + tabName + "」的本机修改，改回跟随当前官方底表？\r\n（清除前会自动备份一次）",
                        "清除单页修改", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
                    return;
                string bakPath;
                CreateBackupSnapshot("auto", "还原单页(" + tabName + ")前自动备份", out bakPath);
                dataTabLists[idx] = new List<string>(dataTabBase[idx]);
                SaveDataOverrides();
                RebuildDataPage(idx, false);
                refreshTabs();
                refreshBackups();
                lblStatusBar.Text = "已将「" + tabName + "」还原为当前底表数据";
            };

            btnBakCreate.Click += delegate
            {
                string note = PromptText("创建本地备份", "备份备注（可选）：", "手动备份");
                if (note == null) return;
                string path;
                if (CreateBackupSnapshot("manual", note.Length == 0 ? "手动备份" : note, out path))
                {
                    refreshBackups();
                    lblStatusBar.Text = "已保存本地备份：" + Path.GetFileName(path);
                }
                else
                {
                    MessageBox.Show(dlg, "保存备份失败，请检查目录写权限。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };

            Action<string> doRestoreFromPath = delegate(string bakFile)
            {
                string cr, vr, kd, nt, pErr;
                Dictionary<string, byte[]> bFiles;
                if (!ParseBackupArchive(bakFile, out cr, out vr, out kd, out nt, out bFiles, out pErr))
                {
                    MessageBox.Show(dlg, "读取备份失败：" + pErr, "无法还原", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                bool rC, rO, rP, rSe, rSv, rImg;
                if (!PromptRestoreChecklistDialog(dlg, "选择要还原的内容", cr, nt, bFiles,
                    out rC, out rO, out rP, out rSe, out rSv, out rImg))
                    return;

                string preBak;
                CreateBackupSnapshot("auto", "还原备份前自动保护", out preBak);
                string rErr;
                if (!RestoreBackupFiles(dataRootDir, bFiles, rC, rO, rP, rSe, rSv, rImg, out rErr))
                {
                    MessageBox.Show(dlg, "还原失败：" + rErr, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                HotReloadRestoredState(rC, rO || rP, rSe, rSv, rImg);
                refreshTabs();
                refreshBackups();
                lblStatusBar.Text = "已按勾选项还原配置并即时生效（原状态已自动备份）";
            };

            btnBakRestore.Click += delegate
            {
                if (lvBackups.SelectedIndices.Count == 0) return;
                int si = lvBackups.SelectedIndices[0];
                if (si < 0 || si >= backupPaths.Count) return;
                doRestoreFromPath(backupPaths[si]);
            };

            btnBakExport.Click += delegate
            {
                using (var sfd = new SaveFileDialog())
                {
                    sfd.Filter = "RX 工具箱备份包 (*.rxbak)|*.rxbak|所有文件 (*.*)|*.*";
                    sfd.FileName = "RX工具箱备份-" + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".rxbak";
                    sfd.Title = "导出完整配置备份";
                    try { sfd.InitialDirectory = BackupDirPath; } catch { }
                    if (sfd.ShowDialog(dlg) != DialogResult.OK) return;
                    SaveSettings();
                    string crossImg = (overlay != null) ? overlay.CustomImagePath : "";
                    string exeDir = Path.GetDirectoryName(Application.ExecutablePath);
                    string eErr;
                    if (CreateBackupArchiveToFile(sfd.FileName, dataRootDir, exeDir, crossImg, "manual", "手动导出备份", out eErr))
                        lblStatusBar.Text = "已导出备份包：" + sfd.FileName;
                    else
                        MessageBox.Show(dlg, "导出失败：" + eErr, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            btnBakImport.Click += delegate
            {
                using (var ofd = new OpenFileDialog())
                {
                    ofd.Filter = "RX 工具箱备份包 (*.rxbak)|*.rxbak|所有文件 (*.*)|*.*";
                    ofd.Title = "读取外部备份包";
                    if (ofd.ShowDialog(dlg) != DialogResult.OK) return;
                    doRestoreFromPath(ofd.FileName);
                }
            };

            btnBakDel.Click += delegate
            {
                if (lvBackups.SelectedIndices.Count == 0) return;
                int si = lvBackups.SelectedIndices[0];
                if (si < 0 || si >= backupPaths.Count) return;
                string f = backupPaths[si];
                if (MessageBox.Show(dlg, "确定删除选中的本地备份文件？\r\n" + Path.GetFileName(f),
                        "删除备份", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
                    return;
                TryDeleteFile(f, 2);
                refreshBackups();
                lblStatusBar.Text = "已删除备份：" + Path.GetFileName(f);
            };

            btnBakDir.Click += delegate
            {
                try
                {
                    if (!string.IsNullOrEmpty(BackupDirPath) && !Directory.Exists(BackupDirPath))
                        Directory.CreateDirectory(BackupDirPath);
                    using (var p = System.Diagnostics.Process.Start(BackupDirPath)) { }
                }
                catch (Exception ex) { lblStatusBar.Text = "打开失败：" + ex.Message; }
            };

            dlg.AcceptButton = btnClose;
            dlg.CancelButton = btnClose;
            refreshTabs();
            refreshBackups();
            ThemeDialog(dlg);
            dlg.ShowDialog(owner);
        }
    }
}
