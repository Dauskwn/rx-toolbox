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
    // ---------------- 在线版本库（Pages Functions + KV） ----------------
    string HubBase()
    {
        string b = hubUrl;
        if (string.IsNullOrEmpty(b)) b = "https://rx-toolbox.pages.dev/api";
        return b.TrimEnd('/');
    }

    string HubCall(string path, string method, string body)
    {
        string url = HubBase() + path;
        var req = (HttpWebRequest)WebRequest.Create(url);
        req.Method = method;
        req.Timeout = 20000;
        req.ReadWriteTimeout = 20000;
        req.UserAgent = "RXToolbox/" + APP_VERSION_NUM;
        req.Headers["X-Key"] = hubKey ?? "";
        if (body != null)
        {
            req.ContentType = "application/json; charset=utf-8";
            byte[] data = Encoding.UTF8.GetBytes(body);
            req.ContentLength = data.Length;
            using (var st = req.GetRequestStream()) st.Write(data, 0, data.Length);
        }
        try
        {
            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                return sr.ReadToEnd();
        }
        catch (WebException we)
        {
            if (we.Response != null)
            {
                using (var sr = new StreamReader(we.Response.GetResponseStream(), Encoding.UTF8))
                    return sr.ReadToEnd();
            }
            throw;
        }
    }

    static string Sha256Text(string text)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text ?? "");
        using (var sha = new System.Security.Cryptography.SHA256Managed())
        {
            byte[] hash = sha.ComputeHash(bytes);
            var sb = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("x2"));
            return sb.ToString();
        }
    }

    static string HubStr(Dictionary<string, object> o, string k)
    {
        object v;
        if (!o.TryGetValue(k, out v) || v == null) return "";
        if (v is double) return ((long)Math.Round((double)v)).ToString(System.Globalization.CultureInfo.InvariantCulture);
        return Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture) ?? "";
    }

    internal static List<string[]> ParseHubList(string json)
    {
        var rows = new List<string[]>();
        if (string.IsNullOrEmpty(json)) return rows;
        var root = MiniJson.Parse(json) as Dictionary<string, object>;
        if (root == null) return rows;
        object itemsObj;
        if (!root.TryGetValue("items", out itemsObj)) return rows;
        var items = itemsObj as List<object>;
        if (items == null) return rows;
        for (int i = 0; i < items.Count; i++)
        {
            var o = items[i] as Dictionary<string, object>;
            if (o == null) continue;
            string id = HubStr(o, "id");
            if (string.IsNullOrEmpty(id)) continue;
            string ts = HubStr(o, "ts");
            string when = ts;
            try
            {
                DateTime dt = DateTime.Parse(ts, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal);
                when = dt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            }
            catch { }
            rows.Add(new string[] {
                when,
                HubStr(o, "author"),
                HubStr(o, "note"),
                HubStr(o, "size"),
                id
            });
        }
        return rows;
    }

    static string HubErrorText(Exception ex)
    {
        string m = ex == null ? "" : ex.Message;
        string low = m.ToLowerInvariant();
        if (m.IndexOf("unauthorized", StringComparison.OrdinalIgnoreCase) >= 0)
            return "密钥不对（服务器拒绝了）";
        if (low.Contains("timed out") || low.Contains("超时"))
            return "连接服务器超时，检查网络或服务器地址";
        if (low.Contains("resolve") || low.Contains("解析"))
            return "域名解析失败（可能被运营商拦了），换网络再试";
        if (low.Contains("502") || low.Contains("503") || low.Contains("500"))
            return "服务器暂时不可用，稍后再试";
        if (low.Contains("connection") || low.Contains("连接"))
            return "连不上服务器，检查网络或地址是否填错";
        return m;
    }

    void ShowSelectedVersionDetail(string prefixLine)
    {
        if (lvVersions == null || txtDevStatus == null) return;
        if (lvVersions.SelectedItems.Count == 0)
        {
            if (!string.IsNullOrEmpty(prefixLine)) DevSetStatus(prefixLine);
            return;
        }
        var it = lvVersions.SelectedItems[0];
        string when = it.Text ?? "";
        string who = it.SubItems.Count > 1 ? (it.SubItems[1].Text ?? "") : "";
        string rawNote = "";
        if (it.SubItems.Count > 2)
        {
            object tagNote = it.SubItems[2].Tag;
            rawNote = tagNote != null ? Convert.ToString(tagNote) : (it.SubItems[2].Text ?? "");
        }
        string sz = it.SubItems.Count > 3 ? (it.SubItems[3].Text ?? "") : "";
        string id = Convert.ToString(it.Tag) ?? "";

        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(prefixLine)) sb.Append(prefixLine).Append("\r\n");
        sb.Append("选中版本：").Append(when);
        if (who.Length > 0) sb.Append("　|　提交人：").Append(who);
        if (sz.Length > 0) sb.Append("　|　大小：").Append(sz);
        if (id.Length > 0) sb.Append("　|　ID：").Append(id);
        sb.Append("\r\n----------------------------------------\r\n");
        if (string.IsNullOrWhiteSpace(rawNote))
            sb.Append("（无备注说明）");
        else
            sb.Append(rawNote.Replace("\r\n", "\n").Replace("\n", "\r\n"));
        DevSetStatus(sb.ToString());
    }

    volatile bool hubBusy = false;

    void HubRunAsync(string busyMsg, Func<string> work, Action<string, Exception> done)
    {
        if (hubBusy)
        {
            toast.ShowText("正在请求服务器，请稍候…");
            return;
        }
        hubBusy = true;
        DevSetStatus(busyMsg);
        ThreadPool.QueueUserWorkItem(delegate
        {
            string res = null;
            Exception err = null;
            try { res = work(); }
            catch (Exception ex) { err = ex; }
            SafeBeginInvoke(delegate
            {
                hubBusy = false;
                done(res, err);
            });
        });
    }

    void DevHubRefresh()
    {
        HubRunAsync("正在刷新服务器版本列表…",
            delegate { return HubCall("/list", "GET", null); },
            delegate(string json, Exception ex)
            {
                if (ex != null)
                {
                    DevSetStatus("拉取列表失败：" + ex.Message + "\r\n（检查服务器地址和密钥是否填对）");
                    return;
                }
                List<string[]> rows = ParseHubList(json);
                if (lvVersions == null) return;
                lvVersions.BeginUpdate();
                lvVersions.Items.Clear();
                for (int i = 0; i < rows.Count; i++)
                {
                    string rawNote = rows[i][2] ?? "";
                    string flatNote = rawNote.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");
                    var it = new ListViewItem(rows[i][0]);
                    it.SubItems.Add(rows[i][1]);
                    var noteSub = it.SubItems.Add(flatNote);
                    noteSub.Tag = rawNote;
                    string sz = rows[i][3];
                    int n;
                    if (int.TryParse(sz, out n)) sz = (n / 1024) + " KB";
                    it.SubItems.Add(sz);
                    it.Tag = rows[i][4];
                    if (rawNote.Length > 0) it.ToolTipText = rawNote;
                    lvVersions.Items.Add(it);
                }
                lvVersions.EndUpdate();
                if (lvVersions.Items.Count > 0)
                {
                    lvVersions.Items[0].Selected = true;
                    ShowSelectedVersionDetail("服务器上共 " + rows.Count + " 个版本（单击列表查看完整备注，双击或点按钮下载应用）：");
                }
                else
                {
                    DevSetStatus("服务器上共 0 个版本。");
                }
            });
    }

    void DevHubUpload()
    {
        if (string.IsNullOrEmpty(hubKey))
        {
            DevSetStatus("请先在“密钥”框中填写密钥后再上传。");
            return;
        }
        if (hubBusy)
        {
            toast.ShowText("正在请求服务器，请稍候…");
            return;
        }
        // 上传者由服务器按密钥绑定，客户端不再询问
        string note = PromptText("上传到服务器", "这次改了什么（支持多行，可留空）：", "", true);
        if (note == null) note = "";
        string data = BuildDataSnapshot(false, "", note);
        string body = "{\"note\":\"" + JsonEsc(note) +
                      "\",\"sha256\":\"" + Sha256Text(data) +
                      "\",\"data\":\"" + JsonEsc(data) + "\"}";
        HubRunAsync("正在上传数据到服务器…",
            delegate { return HubCall("/submit", "POST", body); },
            delegate(string resp, Exception ex)
            {
                if (ex != null)
                {
                    DevSetStatus("上传失败：" + HubErrorText(ex));
                    return;
                }
                if (resp != null && resp.IndexOf("\"ok\":true", StringComparison.Ordinal) >= 0)
                {
                    string who = JsonField(resp, "author");
                    DevSetStatus("已上传（上传者：" + (who.Length > 0 ? who : "未识别") + "）：" + JsonField(resp, "id") + "\r\n由于云端缓存，列表通常在十余秒后刷新显示。");
                }
                else
                {
                    DevSetStatus("上传失败：" + resp);
                }
            });
    }

    void DevHubDownload()
    {
        if (lvVersions == null || lvVersions.SelectedItems.Count == 0)
        {
            DevSetStatus("请先在列表中选择一个版本。");
            return;
        }
        string id = Convert.ToString(lvVersions.SelectedItems[0].Tag);
        HubRunAsync("正在从服务器下载版本 " + id + "…",
            delegate { return HubCall("/get?id=" + Uri.EscapeDataString(id), "GET", null); },
            delegate(string resp, Exception ex)
            {
                if (ex != null)
                {
                    DevSetStatus("下载失败：" + HubErrorText(ex));
                    return;
                }
                string data = JsonField(resp, "data");
                if (string.IsNullOrEmpty(data))
                {
                    DevSetStatus("无法获取该版本数据：" + resp);
                    return;
                }
                string meta;
                var parsed = ParseDataSnapshot(data, out meta);
                if (parsed.Count == 0)
                {
                    DevSetStatus("数据解析失败，格式异常。");
                    return;
                }
                var sb = new StringBuilder();
                foreach (var kv in parsed) sb.Append("· ").Append(kv.Key).Append("：").Append(kv.Value.Count).Append(" 条\r\n");
                if (MessageBox.Show(this, meta + "\r\n" + sb + "\r\n确定将其应用为本机数据吗？（会覆盖 data-custom.txt）",
                    "下载并应用", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
                {
                    DevSetStatus("已取消应用版本 " + id + "。");
                    return;
                }
                ApplyDataOverrides(parsed);
                DevSetStatus("已应用版本 " + id + "，已即时生效。");
            });
    }

    void DevHubDelete()
    {
        if (lvVersions == null || lvVersions.SelectedItems.Count == 0)
        {
            DevSetStatus("请先在列表中选择一个版本。");
            return;
        }
        if (hubBusy)
        {
            toast.ShowText("正在请求服务器，请稍候…");
            return;
        }
        var it = lvVersions.SelectedItems[0];
        string id = Convert.ToString(it.Tag);
        string who = it.SubItems.Count > 1 ? it.SubItems[1].Text : "";
        string note = it.SubItems.Count > 2 ? it.SubItems[2].Text : "";
        string ask = "确定要从服务器删除这个版本吗？\r\n\r\n" + it.Text + "　" + who + "\r\n" + note +
                     "\r\n\r\n删除后无法恢复，所有人的列表中也会同步移除。";
        if (MessageBox.Show(this, ask, "删除版本", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK)
            return;
        HubRunAsync("正在删除版本 " + id + "…",
            delegate { return HubCall("/del?id=" + Uri.EscapeDataString(id), "POST", ""); },
            delegate(string resp, Exception ex)
            {
                if (ex != null)
                {
                    DevSetStatus("删除失败：" + HubErrorText(ex));
                    return;
                }
                if (resp != null && resp.IndexOf("\"ok\":true", StringComparison.Ordinal) >= 0)
                {
                    if (it.ListView == lvVersions) lvVersions.Items.Remove(it);
                    DevSetStatus("已删除版本：" + id);
                }
                else if (resp != null && resp.IndexOf("forbidden", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    DevSetStatus("这个密钥没有删除权限（只有作者密钥可以删）。");
                }
                else
                {
                    DevSetStatus("删除失败：" + resp);
                }
            });
    }

    void LoadHubSettings()
    {
        try
        {
            if (string.IsNullOrEmpty(settingsPath) || !File.Exists(settingsPath)) return;
            foreach (string line in File.ReadAllLines(settingsPath, Encoding.UTF8))
            {
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string k = line.Substring(0, eq).Trim(), v = line.Substring(eq + 1).Trim();
                if (k == "DevTools") devToolsOn = (v == "1");
                else if (k == "HubUrl") hubUrl = v;
                else if (k == "HubKey") hubKey = v;
            }
        }
        catch { }
    }

    void SaveHubSettings()
    {
        SaveSettings();
    }

    // ---------------- 开发者工具 ----------------
    // 数据快照文件格式（Tab 分隔，Excel 可直接打开）：

    string BuildDataSnapshot(bool submission, string author, string note)
    {
        var sb = new StringBuilder();
        sb.Append("# RX 工具箱 数据表\n");
        sb.Append("# 版本 ").Append(APP_VERSION_NUM).Append("\n");
        sb.Append("# 时间 ").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm")).Append("\n");
        if (submission)
        {
            sb.Append("# 提交人 ").Append(DevCell(author)).Append("\n");
            sb.Append("# 说明 ").Append(DevCell(note)).Append("\n");
        }
        else if (!string.IsNullOrEmpty(note))
        {
            sb.Append("# 说明 ").Append(DevCell(note)).Append("\n");
        }
        sb.Append("# 每行一条，Tab 分隔：标题 / 主指令 / 备用 / ulx / 备注。空行和 # 开头的行会被忽略。\n");
        sb.Append("# 主指令留空的行是分组标题（界面上显示为蓝色）。\n\n");
        for (int i = 0; i < dataTabLabels.Length; i++)
        {
            sb.Append("【").Append(dataTabLabels[i]).Append("】\n");
            sb.Append("标题\t主指令\t备用\tulx\t备注\n");
            List<string> rows = dataTabLists[i];
            for (int k = 0; k < rows.Count; k++)
            {
                string t, m, a, u, n;
                DevSplitItem(rows[k], out t, out m, out a, out u, out n);
                sb.Append(DevCell(t)).Append('\t').Append(DevCell(m)).Append('\t')
                  .Append(DevCell(a)).Append('\t').Append(DevCell(u)).Append('\t')
                  .Append(DevCell(n)).Append('\n');
            }
            sb.Append("\n");
        }
        return sb.ToString();
    }

    static Dictionary<string, List<string>> ParseDataSnapshot(string text, out string meta)
    {
        meta = "";
        var map = new Dictionary<string, List<string>>();
        if (string.IsNullOrEmpty(text)) return map;
        string[] lines = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        string cur = null;
        bool headerSkipped = false;
        foreach (string raw in lines)
        {
            string line = raw ?? "";
            if (line.Length == 0) { headerSkipped = false; continue; }
            if (line[0] == '#')
            {
                if (line.StartsWith("# 提交人", StringComparison.Ordinal) ||
                    line.StartsWith("# 说明", StringComparison.Ordinal) ||
                    line.StartsWith("# 版本", StringComparison.Ordinal))
                    meta += line.Substring(1).Trim() + "　";
                continue;
            }
            if (line[0] == '【')
            {
                int end = line.IndexOf('】');
                cur = end > 1 ? line.Substring(1, end - 1) : null;
                if (cur != null && !map.ContainsKey(cur)) map[cur] = new List<string>();
                headerSkipped = false;
                continue;
            }
            if (cur == null) continue;
            if (!headerSkipped && line.StartsWith("标题", StringComparison.Ordinal)) { headerSkipped = true; continue; }
            string[] cells = line.Split('\t');
            string title = cells.Length > 0 ? cells[0] : "";
            string main = cells.Length > 1 ? cells[1] : "";
            string alt = cells.Length > 2 ? cells[2] : "";
            string ulx = cells.Length > 3 ? cells[3] : "";
            string note = cells.Length > 4 ? cells[4] : "";
            if (title.Length == 0) continue;
            map[cur].Add(DevJoinItem(title, main, alt, ulx, note));
        }
        return map;
    }

    void DevSetStatus(string text)
    {
        if (txtDevStatus == null) return;
        txtDevStatus.Text = text ?? "";
        txtDevStatus.SelectionStart = 0;
        txtDevStatus.SelectionLength = 0;
    }

    void DevExport(bool submission)
    {
        try
        {
            string author = "", note = "";
            if (submission)
            {
                author = PromptText("生成本次提交", "提交人（你的名字或昵称）：", "");
                if (author == null) return;
                note = PromptText("生成本次提交", "这次改了什么（支持多行，可留空）：", "", true);
                if (note == null) note = "";
            }
            var dlg = new SaveFileDialog();
            dlg.Filter = submission ? "提交包 (*.rxdata)|*.rxdata|所有文件 (*.*)|*.*"
                                    : "数据快照 (*.rxdata)|*.rxdata|所有文件 (*.*)|*.*";
            dlg.FileName = (submission ? "RX提交-" : "RX数据-") + DateTime.Now.ToString("yyyyMMdd-HHmm") + ".rxdata";
            dlg.Title = submission ? "保存提交包" : "导出数据表";
            // 默认落在数据目录（就是 exe 所在目录），和「打开数据目录」一致
            try { dlg.InitialDirectory = Path.GetDirectoryName(dataOverridePath); } catch { }
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            File.WriteAllText(dlg.FileName, BuildDataSnapshot(submission, author, note), new UTF8Encoding(true));
            DevSetStatus("已保存：" + dlg.FileName + "\r\n\r\n" +
                (submission ? "将该文件发送给作者即可。" : "可用 Excel 打开编辑，完成后再点击“导入数据”。"));
        }
        catch (Exception ex)
        {
            DevSetStatus("导出失败：" + ex.Message);
        }
    }

    void DevImport()
    {
        try
        {
            var dlg = new OpenFileDialog();
            dlg.Filter = "数据文件 (*.rxdata;*.txt;*.tsv)|*.rxdata;*.txt;*.tsv|所有文件 (*.*)|*.*";
            dlg.Title = "导入数据表";
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            string meta;
            var parsed = ParseDataSnapshot(File.ReadAllText(dlg.FileName, Encoding.UTF8), out meta);
            if (parsed.Count == 0)
            {
                DevSetStatus("未在该文件中识别到任何数据表。\r\n请确认其为通过“导出数据”或“生成提交包”生成的 .rxdata 文件。");
                return;
            }

            // 统计差异
            var report = new StringBuilder();
            int totalNew = 0, totalDel = 0, totalChg = 0;
            foreach (var kv in parsed)
            {
                int idx = Array.IndexOf(dataTabLabels, kv.Key);
                if (idx < 0) { report.Append("· ").Append(kv.Key).Append("：本程序没有这个表，已跳过\r\n"); continue; }
                int add, chg, del;
                ComputeTabDiff(dataTabLists[idx], kv.Value, out add, out chg, out del, null, null, null);
                totalNew += add; totalDel += del; totalChg += chg;
                report.Append("· ").Append(kv.Key).Append("：共 ").Append(kv.Value.Count)
                      .Append(" 条，新增 ").Append(add).Append("、修改 ").Append(chg)
                      .Append("、删除 ").Append(del).Append("\r\n");
            }

            string msg = (meta.Length > 0 ? meta + "\r\n\r\n" : "") + report +
                "\r\n合计：新增 " + totalNew + "，修改 " + totalChg + "，删除 " + totalDel + "\r\n\r\n" +
                "应用后将覆盖 data-custom.txt 并立即刷新对应页面，是否继续？";
            if (MessageBox.Show(this, msg, "导入预览", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK)
                return;

            ApplyDataOverrides(parsed);
            DevSetStatus(report.ToString() + "\r\n已写入 data-custom.txt，并已即时刷新页面。");
        }
        catch (Exception ex)
        {
            DevSetStatus("导入失败：" + ex.Message);
        }
    }

    // 把表写进内存并重建页面：导入/下载后不用重启
    void HotApplyOverrides(Dictionary<string, List<string>> tables)
    {
        if (tables == null || dataTabLabels == null) return;
        foreach (var kv in tables)
        {
            int idx = Array.IndexOf(dataTabLabels, kv.Key);
            if (idx < 0 || idx >= dataTabLists.Length) continue;
            dataTabLists[idx] = new List<string>(kv.Value);
            RebuildDataPage(idx);
        }
    }


    void ApplyDataOverrides(Dictionary<string, List<string>> tables)
    {
        var sb = new StringBuilder();
        sb.Append("# 由「开发者工具 → 导入数据」写入\r\n");
        sb.Append("# 时间 ").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm")).Append("\r\n");
        foreach (var kv in tables)
        {
            sb.Append("###").Append(kv.Key).Append("\r\n");
            foreach (string it in kv.Value) sb.Append(EscapeItem(it)).Append("\r\n");
        }
        AtomicWriteText(dataOverridePath, sb.ToString());
        HotApplyOverrides(tables);   // 立刻生效，不用重启
    }

    TabPage BuildDevPage()
    {
        var page = new TabPage("开发者工具");
        page.BackColor = Color.White;
        Font fBody = TrackFont("Microsoft YaHei UI", 9.5f, FontStyle.Regular);
        Font fHint = TrackFont("Microsoft YaHei UI", 9f, FontStyle.Regular);

        var host = new Panel();
        host.Dock = DockStyle.Fill;
        host.BackColor = Color.White;
        host.AutoScroll = true;
        page.Controls.Add(host);

        int x = 20, y = 16, w = 700;

        var t1 = new Label();
        t1.Text = "开发者工具";
        t1.Font = TrackFont("Microsoft YaHei UI", 15f, FontStyle.Bold);
        t1.ForeColor = Color.FromArgb(26, 58, 104);
        t1.SetBounds(x, y, w, 34);
        t1.UseMnemonic = false;
        host.Controls.Add(t1);
        y += 38;

        var t2 = new Label();
        t2.Text = "用来校对、补齐指令表。只处理数据，不涉及程序本体；数据版本 " + APP_VERSION_NUM + "。";
        t2.Font = fHint;
        t2.ForeColor = Color.FromArgb(112, 124, 142);
        t2.SetBounds(x, y, w, 22);
        t2.UseMnemonic = false;
        host.Controls.Add(t2);
        y += 34;

        var bExport = new Button();
        bExport.Text = "导出数据";
        bExport.FlatStyle = FlatStyle.Flat;
        bExport.FlatAppearance.BorderColor = Color.FromArgb(180, 205, 238);
        bExport.SetBounds(x, y, 120, 34);
        bExport.Click += delegate { DevExport(false); };
        host.Controls.Add(bExport);

        var bSubmit = new Button();
        bSubmit.Text = "生成提交包";
        bSubmit.FlatStyle = FlatStyle.Flat;
        bSubmit.FlatAppearance.BorderColor = Color.FromArgb(180, 205, 238);
        bSubmit.SetBounds(x + 130, y, 120, 34);
        bSubmit.Click += delegate { DevExport(true); };
        host.Controls.Add(bSubmit);

        var bImport = new Button();
        bImport.Text = "导入数据";
        bImport.FlatStyle = FlatStyle.Flat;
        bImport.FlatAppearance.BorderColor = Color.FromArgb(180, 205, 238);
        bImport.SetBounds(x + 260, y, 120, 34);
        bImport.Click += delegate { DevImport(); };
        host.Controls.Add(bImport);

        var bFolder = new Button();
        bFolder.Text = "打开数据目录";
        bFolder.FlatStyle = FlatStyle.Flat;
        bFolder.FlatAppearance.BorderColor = Color.FromArgb(180, 205, 238);
        bFolder.SetBounds(x + 390, y, 130, 34);
        bFolder.Click += delegate
        {
            try { using (var p = System.Diagnostics.Process.Start(Path.GetDirectoryName(dataOverridePath))) { } }
            catch (Exception ex) { DevSetStatus("打开失败：" + ex.Message); }
        };
        host.Controls.Add(bFolder);
        y += 46;

        var tip = new Label();
        tip.Text = "本地流程：通过“编辑条目”或 Excel 修改数据 → 生成提交包 → 发送给作者。\r\n" +
                   "在线流程：填写服务器地址与密钥 → 上传当前数据，其他设备即可在列表中选择版本下载。";
        tip.Font = fHint;
        tip.ForeColor = Color.FromArgb(120, 130, 146);
        tip.SetBounds(x, y, w, 44);
        tip.UseMnemonic = false;
        host.Controls.Add(tip);
        y += 50;

        // ---------- 在线版本库 ----------
        y = AddAboutSection(host, "在线版本库", x, y, w + 10);

        LoadHubSettings();

        var lbUrl = new Label();
        lbUrl.Text = "服务器";
        lbUrl.Font = fHint;
        lbUrl.SetBounds(x, y + 3, 50, 22);
        host.Controls.Add(lbUrl);

        txtHubUrl = new TextBox();
        txtHubUrl.Text = hubUrl;
        txtHubUrl.Font = fHint;
        txtHubUrl.SetBounds(x + 52, y, 396, 24);
        host.Controls.Add(txtHubUrl);

        var lbKey = new Label();
        lbKey.Text = "密钥";
        lbKey.Font = fHint;
        lbKey.SetBounds(x + 458, y + 3, 40, 22);
        host.Controls.Add(lbKey);

        txtHubKey = new TextBox();
        txtHubKey.Text = hubKey;
        txtHubKey.Font = fHint;
        txtHubKey.UseSystemPasswordChar = true;
        txtHubKey.SetBounds(x + 496, y, 150, 24);
        host.Controls.Add(txtHubKey);
        y += 34;

        var bSave = new Button();
        bSave.Text = "保存设置";
        bSave.FlatStyle = FlatStyle.Flat;
        bSave.FlatAppearance.BorderColor = Color.FromArgb(180, 205, 238);
        bSave.SetBounds(x, y, 96, 32);
        bSave.Click += delegate
        {
            hubUrl = txtHubUrl.Text.Trim();
            hubKey = txtHubKey.Text.Trim();
            SaveHubSettings();
            DevSetStatus("服务器设置已保存。");
        };
        host.Controls.Add(bSave);

        var bUp = new Button();
        bUp.Text = "上传当前数据";
        bUp.FlatStyle = FlatStyle.Flat;
        bUp.FlatAppearance.BorderColor = Color.FromArgb(180, 205, 238);
        bUp.SetBounds(x + 104, y, 118, 32);
        bUp.Click += delegate
        {
            hubUrl = txtHubUrl.Text.Trim();
            hubKey = txtHubKey.Text.Trim();
            SaveHubSettings();
            DevHubUpload();
        };
        host.Controls.Add(bUp);

        var bList = new Button();
        bList.Text = "刷新列表";
        bList.FlatStyle = FlatStyle.Flat;
        bList.FlatAppearance.BorderColor = Color.FromArgb(180, 205, 238);
        bList.SetBounds(x + 230, y, 96, 32);
        bList.Click += delegate
        {
            hubUrl = txtHubUrl.Text.Trim();
            hubKey = txtHubKey.Text.Trim();
            SaveHubSettings();
            DevHubRefresh();
        };
        host.Controls.Add(bList);

        var bDel = new Button();
        bDel.Text = "删除选中";
        bDel.FlatStyle = FlatStyle.Flat;
        bDel.FlatAppearance.BorderColor = Color.FromArgb(232, 190, 190);
        bDel.ForeColor = Color.FromArgb(150, 60, 60);
        bDel.SetBounds(x + 512, y, 100, 32);
        bDel.Click += delegate { DevHubDelete(); };
        host.Controls.Add(bDel);

        var bDown = new Button();
        bDown.Text = "下载并应用选中版本";
        bDown.FlatStyle = FlatStyle.Flat;
        bDown.FlatAppearance.BorderColor = Color.FromArgb(180, 205, 238);
        bDown.SetBounds(x + 334, y, 170, 32);
        bDown.Click += delegate { DevHubDownload(); };
        host.Controls.Add(bDown);
        y += 42;

        lvVersions = new BufferedListView();
        lvVersions.View = View.Details;
        lvVersions.FullRowSelect = true;
        lvVersions.HideSelection = false;
        lvVersions.ShowItemToolTips = true;
        lvVersions.Font = fHint;
        lvVersions.SetBounds(x, y, w, 168);
        lvVersions.Columns.Add("时间", 132);
        lvVersions.Columns.Add("提交人", 110);
        lvVersions.Columns.Add("备注", 320);
        lvVersions.Columns.Add("大小", 70);
        lvVersions.SelectedIndexChanged += delegate { ShowSelectedVersionDetail(null); };
        lvVersions.DoubleClick += delegate { DevHubDownload(); };

        var cmsVer = new ContextMenuStrip();
        uiComponents.Add(cmsVer);
        var miCopyNote = new ToolStripMenuItem("复制备注说明");
        miCopyNote.Click += delegate
        {
            if (lvVersions.SelectedItems.Count == 0) return;
            var it = lvVersions.SelectedItems[0];
            string rawNote = "";
            if (it.SubItems.Count > 2)
            {
                object tagNote = it.SubItems[2].Tag;
                rawNote = tagNote != null ? Convert.ToString(tagNote) : (it.SubItems[2].Text ?? "");
            }
            if (string.IsNullOrWhiteSpace(rawNote))
            {
                toast.ShowText("该版本没有备注说明");
                return;
            }
            if (TrySetClipboardText(rawNote.Replace("\r\n", "\n").Replace("\n", "\r\n")))
                toast.ShowText("已复制完整备注说明");
        };
        var miDownVer = new ToolStripMenuItem("下载并应用此版本");
        miDownVer.Click += delegate { DevHubDownload(); };
        var miDelVer = new ToolStripMenuItem("删除此版本");
        miDelVer.Click += delegate { DevHubDelete(); };
        cmsVer.Items.AddRange(new ToolStripItem[] { miCopyNote, new ToolStripSeparator(), miDownVer, miDelVer });
        cmsVer.Opening += delegate
        {
            bool hasSel = lvVersions.SelectedItems.Count > 0;
            miCopyNote.Enabled = hasSel;
            miDownVer.Enabled = hasSel;
            miDelVer.Enabled = hasSel;
        };
        lvVersions.ContextMenuStrip = cmsVer;
        host.Controls.Add(lvVersions);
        y += 178;

        txtDevStatus = new TextBox();
        txtDevStatus.Multiline = true;
        txtDevStatus.ReadOnly = true;
        txtDevStatus.WordWrap = true;
        txtDevStatus.ScrollBars = ScrollBars.Vertical;
        txtDevStatus.BorderStyle = BorderStyle.FixedSingle;
        txtDevStatus.BackColor = Color.FromArgb(248, 250, 253);
        txtDevStatus.Font = fHint;
        txtDevStatus.SetBounds(x, y, w, 140);
        txtDevStatus.Text = "（这里显示导出/导入/上传结果，及所选版本的完整备注说明）";
        host.Controls.Add(txtDevStatus);
        y += 152;

        Action layoutDev = delegate
        {
            int cw = Math.Max(w, host.ClientSize.Width - x * 2);
            lvVersions.Width = cw;
            txtDevStatus.Width = cw;
            int fixedW = lvVersions.Columns[0].Width + lvVersions.Columns[1].Width + lvVersions.Columns[3].Width + 24;
            int noteW = Math.Max(240, cw - fixedW);
            if (lvVersions.Columns[2].Width != noteW) lvVersions.Columns[2].Width = noteW;
        };
        host.Resize += delegate { layoutDev(); };
        layoutDev();

        host.AutoScrollMinSize = new Size(w + 60, y + 20);
        return page;
    }
}
