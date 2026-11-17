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
    static string EscapeItem(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        string norm = s.Replace("\r\n", "\n").Replace("\r", "\n");
        string esc = norm.Replace("\\", "\\\\").Replace("\n", "\\n");
        if (esc.StartsWith("###", StringComparison.Ordinal))
            esc = "\\#" + esc.Substring(1);
        return esc;
    }

    static string UnescapeItem(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            char ch = s[i];
            if (ch == '\\' && i + 1 < s.Length)
            {
                char n = s[i + 1];
                if (n == 'n') { sb.Append("\r\n"); i++; continue; }
                if (n == '\\') { sb.Append('\\'); i++; continue; }
                if (n == '#') { sb.Append('#'); i++; continue; }
            }
            sb.Append(ch);
        }
        return sb.ToString();
    }

    void SaveDataOverrides()
    {
        try
        {
            var sb = new StringBuilder();
            bool any = false;
            for (int i = 0; i < dataTabLabels.Length; i++)
            {
                var list = dataTabLists[i];
                string[] baseArr = (i < dataTabBase.Length) ? dataTabBase[i] : null;
                if (TabListEquals(list, baseArr)) continue;
                any = true;
                sb.Append("###").Append(dataTabLabels[i]).Append("\r\n");
                for (int k = 0; k < list.Count; k++)
                    sb.Append(EscapeItem(list[k])).Append("\r\n");
            }
            AtomicWriteText(dataOverridePath, any ? sb.ToString() : "");
        }
        catch (Exception ex) { Native.Log("save data override fail: " + ex.Message); }
    }

    static string ItemDisplay(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        string t, m, a, u, n;
        DevSplitItem(raw, out t, out m, out a, out u, out n);
        return string.IsNullOrEmpty(m) ? t : (t + "  →  " + m);
    }


    bool PromptItem(string caption, ref string raw)
    {
        // 统一走四分隔符协议，避免编辑时把注解 / ulx 拖进命令
        string title, body, alt, ulx, note;
        string src = raw ?? "";
        DevSplitItem(src, out title, out body, out alt, out ulx, out note);
        body = body.Replace("\r\n", "\n").Replace("\n", "\r\n");
        alt = alt.Replace("\r\n", "\n").Replace("\n", "\r\n");

        using (var dlg = new Form())
        {
            dlg.Text = caption;
            dlg.StartPosition = FormStartPosition.CenterParent;
            dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
            dlg.MinimizeBox = false; dlg.MaximizeBox = false;
            dlg.ClientSize = new Size(660, 496);
            dlg.Font = TrackFont("Microsoft YaHei UI", 9.5f, FontStyle.Regular);

            var l1 = new Label();
            l1.Text = "名称（分类标题就只填这一栏）";
            l1.SetBounds(14, 10, 400, 20);
            l1.ForeColor = Color.FromArgb(90, 100, 114);
            dlg.Controls.Add(l1);

            var bTitle = new TextBox();
            bTitle.SetBounds(14, 32, 632, 28);
            bTitle.Text = title;
            dlg.Controls.Add(bTitle);

            var l2 = new Label();
            l2.Text = "指令 / 说明（玩家点这一条会复制这里的内容）";
            l2.SetBounds(14, 68, 500, 20);
            l2.ForeColor = Color.FromArgb(90, 100, 114);
            dlg.Controls.Add(l2);

            var bBody = new TextBox();
            bBody.Multiline = true;
            bBody.ScrollBars = ScrollBars.Vertical;
            bBody.SetBounds(14, 90, 632, 92);
            bBody.Text = body;
            dlg.Controls.Add(bBody);

            var l3 = new Label();
            l3.Text = "备用指令（可选，角色表的 devforcespawn 用；不填就隐藏）";
            l3.SetBounds(14, 188, 560, 20);
            l3.ForeColor = Color.FromArgb(90, 100, 114);
            dlg.Controls.Add(l3);

            var bAlt = new TextBox();
            bAlt.Multiline = true;
            bAlt.ScrollBars = ScrollBars.Vertical;
            bAlt.SetBounds(14, 210, 632, 56);
            bAlt.Text = alt;
            dlg.Controls.Add(bAlt);

            var l4 = new Label();
            l4.Text = "控制台 ulx 写法（可选，留空则对 ! 开头的命令自动加 ulx 前缀）";
            l4.SetBounds(14, 272, 600, 20);
            l4.ForeColor = Color.FromArgb(90, 100, 114);
            dlg.Controls.Add(l4);

            var bUlx = new TextBox();
            bUlx.SetBounds(14, 294, 632, 28);
            bUlx.Text = ulx;
            dlg.Controls.Add(bUlx);

            var l5 = new Label();
            l5.Text = "注解（可选，显示在标题右侧的灰色小字）";
            l5.SetBounds(14, 330, 500, 20);
            l5.ForeColor = Color.FromArgb(90, 100, 114);
            dlg.Controls.Add(l5);

            var bNote = new TextBox();
            bNote.SetBounds(14, 352, 632, 28);
            bNote.Text = note;
            dlg.Controls.Add(bNote);

            var lab = new Label();
            lab.Text = "提示：分类标题只填名称；普通条目填名称 + 指令。";
            lab.SetBounds(14, 392, 500, 20);
            lab.ForeColor = Color.Gray;
            dlg.Controls.Add(lab);

            var ok = new Button();
            ok.Text = "保存";
            ok.SetBounds(474, 440, 80, 32);
            ok.DialogResult = DialogResult.OK;
            dlg.Controls.Add(ok);

            var cancel = new Button();
            cancel.Text = "取消";
            cancel.SetBounds(566, 440, 80, 32);
            cancel.DialogResult = DialogResult.Cancel;
            dlg.Controls.Add(cancel);

            dlg.AcceptButton = ok;
            dlg.CancelButton = cancel;
            ThemeDialog(dlg);

            if (dlg.ShowDialog(this) != DialogResult.OK) return false;

            string t = CleanCtrl(bTitle.Text ?? "", true).Trim();
            string b = CleanCtrl(bBody.Text ?? "", false).Trim();
            string a = CleanCtrl(bAlt.Text ?? "", false).Trim();
            string u = CleanCtrl(bUlx.Text ?? "", false).Trim();
            string n = CleanCtrl(bNote.Text ?? "", false).Replace("\r", " ").Replace("\n", " ").Trim();
            if (t.Length == 0 && b.Length == 0) return false;

            raw = DevJoinItem(t, b, a, u, n);
            return true;
        }
    }

    void EditDataTab(int tabIndex)
    {
        if (tabIndex < 0 || tabIndex >= dataTabLists.Length) return;
        var list = dataTabLists[tabIndex];
        string label = dataTabLabels[tabIndex];

        using (var dlg = new Form())
        {
            dlg.Text = label + " · 编辑条目（共 " + list.Count + " 条）";
            dlg.StartPosition = FormStartPosition.CenterParent;
            dlg.FormBorderStyle = FormBorderStyle.Sizable;
            dlg.MinimumSize = new Size(860, 460);
            dlg.ClientSize = new Size(1000, 640);
            dlg.Font = TrackFont("Microsoft YaHei UI", 9.5f, FontStyle.Regular);

            var grid = new DataGridView();
            grid.Dock = DockStyle.Fill;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.ReadOnly = true;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = false;
            grid.BackgroundColor = IsDark ? Color.FromArgb(21, 25, 34) : Color.White;
            grid.BorderStyle = BorderStyle.FixedSingle;
            grid.GridColor = IsDark ? Color.FromArgb(45, 54, 70) : Color.FromArgb(210, 218, 228);
            grid.CellBorderStyle = DataGridViewCellBorderStyle.Single;
            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.DisplayedCells;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            grid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            grid.DefaultCellStyle.Padding = new Padding(6, 4, 6, 4);
            grid.DefaultCellStyle.BackColor = IsDark ? Color.FromArgb(21, 25, 34) : Color.White;
            grid.DefaultCellStyle.ForeColor = IsDark ? Color.FromArgb(226, 232, 240) : Color.FromArgb(24, 34, 48);
            grid.DefaultCellStyle.SelectionBackColor = IsDark ? Color.FromArgb(36, 52, 77) : Color.FromArgb(210, 228, 250);
            grid.DefaultCellStyle.SelectionForeColor = IsDark ? Color.White : Color.Black;
            grid.AlternatingRowsDefaultCellStyle.BackColor = IsDark ? Color.FromArgb(26, 31, 42) : Color.FromArgb(248, 250, 253);

            var colHandle = new DataGridViewTextBoxColumn();
            colHandle.HeaderText = "";
            colHandle.Width = 34;
            colHandle.MinimumWidth = 34;
            colHandle.SortMode = DataGridViewColumnSortMode.NotSortable;
            colHandle.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            colHandle.DefaultCellStyle.ForeColor = Color.Silver;
            grid.Columns.Add(colHandle);

            var colDesc = new DataGridViewTextBoxColumn();
            colDesc.HeaderText = "描述";
            colDesc.SortMode = DataGridViewColumnSortMode.NotSortable;
            colDesc.MinimumWidth = 160;
            grid.Columns.Add(colDesc);

            var colCmd = new DataGridViewTextBoxColumn();
            colCmd.HeaderText = "指令";
            colCmd.SortMode = DataGridViewColumnSortMode.NotSortable;
            colCmd.MinimumWidth = 200;
            grid.Columns.Add(colCmd);

            bool[] headFlags = new bool[list.Count];

            Action fill = delegate
            {
                if (headFlags.Length < list.Count) headFlags = new bool[list.Count];
                for (int z = 0; z < headFlags.Length; z++) headFlags[z] = false;
                grid.Rows.Clear();
                for (int i = 0; i < list.Count; i++)
                {
                    string raw = list[i] ?? "";
                    string t = raw, b = "", a = "";
                    string uu = "", uun = "";
                    DevSplitItem(raw, out t, out b, out a, out uu, out uun);
                    string cmdShow = b;
                    if (a.Length > 0) cmdShow = cmdShow + "   |   备用：" + a;
                    if (uu.Length > 0) cmdShow = cmdShow + "   |   ulx：" + uu;
                    if (uun.Length > 0) cmdShow = cmdShow + "   |   注：" + uun;
                    bool isHeader = (b.Length == 0);
                    int ri = grid.Rows.Add("≡", t, isHeader ? "" : cmdShow);
                    if (ri < headFlags.Length) headFlags[ri] = isHeader;
                }
            };
            fill();

            Action<int> refreshRow = delegate(int i)
            {
                if (i < 0 || i >= grid.Rows.Count || i >= list.Count) return;
                string raw = list[i] ?? "";
                string t = raw, b = "", a = "";
                string uu2 = "", uu2n = "";
                DevSplitItem(raw, out t, out b, out a, out uu2, out uu2n);
                string cmdShow2 = b;
                if (a.Length > 0) cmdShow2 = cmdShow2 + "   |   备用：" + a;
                if (uu2.Length > 0) cmdShow2 = cmdShow2 + "   |   ulx：" + uu2;
                if (uu2n.Length > 0) cmdShow2 = cmdShow2 + "   |   注：" + uu2n;
                headFlags[i] = (b.Length == 0);
                grid.Rows[i].Cells[1].Value = t;
                grid.Rows[i].Cells[2].Value = cmdShow2;
            };

            Action layoutCols = delegate
            {
                int w = grid.ClientSize.Width - 4 - colHandle.Width;
                if (w < 200) w = 200;
                colDesc.Width = (int)(w * 0.42);
                colCmd.Width = w - colDesc.Width;
            };
            grid.Resize += delegate { layoutCols(); };

            dlg.Controls.Add(grid);

            var bar = new Panel();
            bar.Dock = DockStyle.Bottom;
            bar.Height = 52;
            bar.Padding = new Padding(8, 10, 8, 8);
            dlg.Controls.Add(bar);

            Func<int> selIndex = delegate { return grid.SelectedRows.Count > 0 ? grid.SelectedRows[0].Index : -1; };

            var bAdd = new Button(); bAdd.Text = "＋ 添加"; bAdd.SetBounds(8, 10, 90, 32);
            bAdd.Click += delegate
            {
                string raw = "";
                if (PromptItem("添加条目 · " + label, ref raw) && raw.Trim().Length > 0)
                {
                    list.Add(raw);
                    fill();
                    grid.ClearSelection();
                    if (grid.Rows.Count > 0) grid.Rows[grid.Rows.Count - 1].Selected = true;
                }
            };
            bar.Controls.Add(bAdd);

            var bAddHead = new Button(); bAddHead.Text = "＋ 分类"; bAddHead.SetBounds(104, 10, 90, 32);
            bAddHead.Click += delegate
            {
                string raw = "";
                if (PromptItem("添加分类标题 · " + label, ref raw) && raw.Trim().Length > 0)
                {
                    list.Add(raw);
                    fill();
                    grid.ClearSelection();
                    if (grid.Rows.Count > 0) grid.Rows[grid.Rows.Count - 1].Selected = true;
                }
            };
            bar.Controls.Add(bAddHead);

            var bEdit = new Button(); bEdit.Text = "编辑"; bEdit.SetBounds(200, 10, 80, 32);
            bEdit.Click += delegate
            {
                int i = selIndex();
                if (i < 0) return;
                string raw = list[i];
                if (PromptItem("修改条目 · " + label, ref raw) && raw.Trim().Length > 0)
                {
                    list[i] = raw;
                    fill();
                    grid.ClearSelection();
                    if (i < grid.Rows.Count) grid.Rows[i].Selected = true;
                }
            };
            bar.Controls.Add(bEdit);

            var bDel = new Button(); bDel.Text = "删除"; bDel.SetBounds(286, 10, 80, 32);
            bDel.Click += delegate
            {
                int i = selIndex();
                if (i < 0) return;
                list.RemoveAt(i);
                fill();
                if (grid.Rows.Count > 0)
                {
                    grid.ClearSelection();
                    grid.Rows[Math.Min(i, grid.Rows.Count - 1)].Selected = true;
                }
            };
            bar.Controls.Add(bDel);

            var bUp = new Button(); bUp.Text = "上移"; bUp.SetBounds(372, 10, 72, 32);
            bUp.Click += delegate
            {
                int i = selIndex();
                if (i <= 0) return;
                string a = list[i - 1]; list[i - 1] = list[i]; list[i] = a;
                refreshRow(i - 1);
                refreshRow(i);
                grid.ClearSelection();
                grid.Rows[i - 1].Selected = true;
                grid.CurrentCell = grid.Rows[i - 1].Cells[1];
            };
            bar.Controls.Add(bUp);

            var bDown = new Button(); bDown.Text = "下移"; bDown.SetBounds(450, 10, 72, 32);
            bDown.Click += delegate
            {
                int i = selIndex();
                if (i < 0 || i >= list.Count - 1) return;
                string a = list[i + 1]; list[i + 1] = list[i]; list[i] = a;
                refreshRow(i);
                refreshRow(i + 1);
                grid.ClearSelection();
                grid.Rows[i + 1].Selected = true;
                grid.CurrentCell = grid.Rows[i + 1].Cells[1];
            };
            bar.Controls.Add(bDown);

            var bReset = new Button(); bReset.Text = "恢复内置"; bReset.SetBounds(528, 10, 96, 32);
            bReset.ForeColor = Color.FromArgb(160, 60, 60);
            bReset.Click += delegate
            {
                if (MessageBox.Show(dlg, "清空「" + label + "」的本机修改，恢复成当前基准数据？\n（仅还原这一页，立即生效）",
                        "恢复内置", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
                int baseIdx = Array.IndexOf(dataTabLabels, label);
                if (baseIdx >= 0 && baseIdx < dataTabBase.Length && dataTabBase[baseIdx] != null)
                {
                    list.Clear();
                    list.AddRange(dataTabBase[baseIdx]);
                    dataTabLists[baseIdx] = list;
                    fill();
                    SaveDataOverrides();
                    RebuildDataPage(baseIdx, false);
                }
                else
                {
                    var map = LoadDataOverrides();
                    map.Remove(label);
                    var sb = new StringBuilder();
                    foreach (var kv in map)
                    {
                        sb.Append("###").Append(kv.Key).Append("\r\n");
                        for (int k = 0; k < kv.Value.Count; k++)
                            sb.Append(EscapeItem(kv.Value[k])).Append("\r\n");
                    }
                    AtomicWriteText(dataOverridePath, sb.ToString());
                }
                MessageBox.Show(dlg, "「" + label + "」已恢复为当前基准数据并即时刷新。", "提示");
            };
            bar.Controls.Add(bReset);

            var bBackupMgr = new Button(); bBackupMgr.Text = "备份与更新…"; bBackupMgr.SetBounds(630, 10, 108, 32);
            bBackupMgr.Click += delegate
            {
                SaveDataOverrides();
                ShowDataBackupManager(dlg);
                if (tabIndex < dataTabLists.Length && dataTabLists[tabIndex] != null)
                {
                    if (!object.ReferenceEquals(list, dataTabLists[tabIndex]))
                    {
                        list.Clear();
                        list.AddRange(dataTabLists[tabIndex]);
                        dataTabLists[tabIndex] = list;
                    }
                    fill();
                }
            };
            bar.Controls.Add(bBackupMgr);

            var bClose = new Button();
            bClose.Text = "完成";
            bClose.DialogResult = DialogResult.OK;
            bClose.SetBounds(700, 10, 96, 32);
            bClose.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            bar.Controls.Add(bClose);
            dlg.AcceptButton = bClose;

            Font boldCellFont = TrackFont("Microsoft YaHei UI", 9.5f, FontStyle.Bold);
            grid.CellPainting += delegate(object s, DataGridViewCellPaintingEventArgs e)
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
                if (e.RowIndex >= headFlags.Length || !headFlags[e.RowIndex]) return;

                if (e.ColumnIndex == 2)
                {
                    e.Handled = true;
                    return;
                }
                if (e.ColumnIndex == 1)
                {
                    Rectangle r1 = grid.GetCellDisplayRectangle(1, e.RowIndex, false);
                    Rectangle r2 = grid.GetCellDisplayRectangle(2, e.RowIndex, false);
                    Rectangle merged = Rectangle.FromLTRB(r1.Left, r1.Top, r2.Right, r1.Bottom);

                    using (var brush = new SolidBrush(IsDark ? Color.FromArgb(33, 40, 54) : Color.FromArgb(214, 229, 250)))
                        e.Graphics.FillRectangle(brush, merged);
                    using (var pen = new Pen(IsDark ? Color.FromArgb(52, 66, 90) : Color.FromArgb(160, 190, 230)))
                    {
                        e.Graphics.DrawLine(pen, merged.Left, merged.Bottom - 1, merged.Right, merged.Bottom - 1);
                        e.Graphics.DrawLine(pen, merged.Right - 1, merged.Top, merged.Right - 1, merged.Bottom - 1);
                    }

                    bool selected = grid.Rows[e.RowIndex].Selected;
                    Color tc = IsDark
                        ? (selected ? Color.White : Color.FromArgb(147, 197, 253))
                        : (selected ? Color.Black : Color.FromArgb(26, 62, 120));
                    Rectangle textRect = new Rectangle(merged.Left + 8, merged.Top, merged.Width - 12, merged.Height);
                    TextRenderer.DrawText(e.Graphics, Convert.ToString(e.Value), boldCellFont, textRect, tc,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                    e.Handled = true;
                }
            };

            int dragFrom = -1;
            grid.MouseDown += delegate(object s, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left) return;
                DataGridView.HitTestInfo hit = grid.HitTest(e.X, e.Y);
                if (hit.Type == DataGridViewHitTestType.Cell && hit.ColumnIndex == 0 && hit.RowIndex >= 0)
                {
                    dragFrom = hit.RowIndex;
                    grid.Cursor = Cursors.SizeAll;
                }
            };
            grid.MouseUp += delegate(object s, MouseEventArgs e)
            {
                if (dragFrom < 0) return;
                DataGridView.HitTestInfo hit = grid.HitTest(e.X, e.Y);
                int to = (hit.Type == DataGridViewHitTestType.Cell) ? hit.RowIndex : -1;
                if (to >= 0 && to != dragFrom && dragFrom < list.Count && to < list.Count)
                {
                    string item = list[dragFrom];
                    list.RemoveAt(dragFrom);
                    list.Insert(to, item);
                    fill();
                    grid.ClearSelection();
                    if (to < grid.Rows.Count)
                    {
                        grid.Rows[to].Selected = true;
                        grid.CurrentCell = grid.Rows[to].Cells[1];
                    }
                    Native.Log("data drag: " + dragFrom + " -> " + to);
                }
                dragFrom = -1;
                grid.Cursor = Cursors.Default;
            };

            grid.CellDoubleClick += delegate { bEdit.PerformClick(); };
            grid.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Delete) bDel.PerformClick();
                else if (e.KeyCode == Keys.Enter) { bEdit.PerformClick(); e.Handled = true; }
            };

            dlg.Shown += delegate
            {
                layoutCols();
                bClose.Left = bar.ClientSize.Width - bClose.Width - 8;
            };
            bar.Resize += delegate { bClose.Left = bar.ClientSize.Width - bClose.Width - 8; };
            ThemeDialog(dlg);

            dlg.ShowDialog(this);
        }

        SaveDataOverrides();
        RebuildDataPage(tabIndex);
        Native.Log("data edit: " + label + " -> " + list.Count + " 条");
    }

    void RebuildDataPage(int index)
    {
        RebuildDataPage(index, true);
    }

    void RebuildDataPage(int index, bool selectAfter)
    {
        try
        {
            int tabIdx = index;
            if (tabIdx < 0 || tabIdx >= tabs.TabPages.Count) return;
            int prevSel = tabs.SelectedIndex;
            var old = tabs.TabPages[tabIdx];
            tabs.TabPages.Remove(old);
            old.Dispose();
            tabs.TabPages.Insert(tabIdx, BuildTextPage(dataTabLabels[index], dataTabLists[index].ToArray(), index));
            ThemeControlTree(tabs.TabPages[tabIdx]);
            if (selectAfter) tabs.SelectedIndex = tabIdx;
            else if (prevSel >= 0 && prevSel < tabs.TabPages.Count) tabs.SelectedIndex = prevSel;
        }
        catch (Exception ex) { Native.Log("rebuild data page fail: " + ex.Message); }
    }

    TabPage BuildTextPage(string label, string[] items, int pageIndex)
    {
        int total = (items == null) ? 0 : items.Length;
        string[] titles = new string[total];
        string[] bodies = new string[total];
        string[] alts = new string[total];
        string[] notes = new string[total];
        string[] ulxs = new string[total];
        bool[] isHead = new bool[total];
        bool hasAlt = false;
        bool hasUlx = false;
        int nAlt = 0, nUlx = 0;

        int cmdCount = 0;
        for (int i = 0; i < total; i++)
        {
            string raw = items[i] ?? "";
            int sep = raw.IndexOf('\u0001');
            string t = sep > 0 ? raw.Substring(0, sep) : raw;
            string rest = sep > 0 ? raw.Substring(sep + 1) : "";
            string b = rest, a = "";
            int sep2 = rest.IndexOf('\u0002');
            string nt = "";
            if (sep2 >= 0)
            {
                b = rest.Substring(0, sep2);
                a = rest.Substring(sep2 + 1);
            }
            string ux = "";
            // 顺序很重要：注解在最末尾，先剥注解再剥第三种写法，否则注解会被并进指令
            int sep3 = a.IndexOf('\u0003');   // \u0003 之后是注解（小字灰色，例如"属于哪个角色"）
            if (sep3 >= 0)
            {
                nt = a.Substring(sep3 + 1);
                a = a.Substring(0, sep3);
            }
            else if (sep2 < 0)
            {
                // 兜底：文件里没有 \u0002 占位时，注解直接跟在指令后面
                int sep3b = b.IndexOf('\u0003');
                if (sep3b >= 0)
                {
                    nt = b.Substring(sep3b + 1);
                    b = b.Substring(0, sep3b);
                }
            }
            int sep4 = a.IndexOf('\u0004');   // \u0004 之后是第三种写法（控制台 ulx / 备用代码）
            if (sep4 >= 0)
            {
                ux = a.Substring(sep4 + 1);
                a = a.Substring(0, sep4);
            }
            ulxs[i] = ux;
            titles[i] = t;
            bodies[i] = b;
            alts[i] = a;
            notes[i] = nt;
            isHead[i] = string.IsNullOrEmpty(b);
            if (!isHead[i]) cmdCount++;
            if (!string.IsNullOrEmpty(a) && IsRolePage(label)) { hasAlt = true; nAlt++; }
        }

        // ulx：除了模型表，凡是 ! 开头的命令都能加 "ulx " 前缀执行
        bool ulxAuto = (label == null || label.IndexOf("模型", StringComparison.Ordinal) < 0);
        if (ulxAuto)
        {
            for (int i = 0; i < total; i++)
            {
                if (isHead[i]) continue;
                string bd = bodies[i] ?? "";
                bool bang = bd.Length > 1 && bd[0] == '!';
                bool already = bd.StartsWith("ulx", StringComparison.OrdinalIgnoreCase);
                if (bang || already || !string.IsNullOrEmpty(ulxs[i])) nUlx++;
            }
        }
        hasUlx = (nUlx > 0);

        var page = new TabPage(cmdCount > 0 ? label + " (" + cmdCount + ")" : label);
        page.Padding = new Padding(0);
        page.BackColor = Color.White;

        int mode = 0;   // 0 = 主命令，1 = alt 命令，2 = ulx
        RadioButton rbMain = null, rbAlt = null, rbUlx = null;

        var top = new Panel();
        top.Dock = DockStyle.Top;
        top.Height = 124;
        top.Padding = new Padding(8, 8, 8, 6);
        top.BackColor = Color.FromArgb(247, 249, 252);
        top.Paint += delegate(object snd, PaintEventArgs pe)
        {
            using (var pen = new Pen(IsDark ? Color.FromArgb(48, 54, 61) : Color.FromArgb(226, 232, 240)))
                pe.Graphics.DrawLine(pen, 0, top.Height - 1, top.Width, top.Height - 1);
        };

        const string EmptyPreviewHint = "💡 选中或单击下方任意条目即可自动复制指令，并在此预览完整多行文本（支持鼠标划选局部复制）";

        var preview = new TextBox();
        preview.Tag = "rx-preview";
        preview.Dock = DockStyle.Fill;
        preview.Multiline = true;
        preview.ReadOnly = true;
        preview.ScrollBars = ScrollBars.Vertical;
        preview.BorderStyle = BorderStyle.FixedSingle;
        preview.BackColor = Color.White;
        preview.ForeColor = Color.FromArgb(140, 150, 165);
        preview.Text = EmptyPreviewHint;
        preview.Font = TrackFont("Microsoft YaHei UI", 9.5f, FontStyle.Regular);

        var prevMenu = new ContextMenuStrip(uiComponents);
        var miCopySel = new ToolStripMenuItem("复制选中");
        miCopySel.Click += delegate { if (preview.SelectionLength > 0) preview.Copy(); };
        var miSelAll = new ToolStripMenuItem("全选");
        miSelAll.Click += delegate { preview.SelectAll(); };
        prevMenu.Items.Add(miCopySel);
        prevMenu.Items.Add(miSelAll);
        preview.ContextMenuStrip = prevMenu;

        top.Controls.Add(preview);

        var searchPanel = new Panel();
        searchPanel.Dock = DockStyle.Right;
        searchPanel.Width = 256;
        searchPanel.Padding = new Padding(6, 0, 0, 0);

        var lblSearch = new Label();
        lblSearch.Text = "筛选关键词（空格多词，Esc清空）：";
        lblSearch.SetBounds(6, 0, 240, 18);
        lblSearch.ForeColor = Color.FromArgb(90, 100, 114);
        searchPanel.Controls.Add(lblSearch);

        var box = new TextBox();
        box.SetBounds(6, 20, 206, 26);
        box.Font = TrackFont("Microsoft YaHei UI", 9.5f, FontStyle.Regular);
        searchPanel.Controls.Add(box);

        var btnClearSearch = new Button();
        btnClearSearch.Text = "×";
        btnClearSearch.SetBounds(214, 19, 32, 26);
        btnClearSearch.FlatStyle = FlatStyle.Flat;
        btnClearSearch.FlatAppearance.BorderColor = Color.Silver;
        btnClearSearch.ForeColor = Color.DimGray;
        btnClearSearch.Visible = false;
        searchPanel.Controls.Add(btnClearSearch);

        var lblMatchCount = new Label();
        lblMatchCount.SetBounds(6, 50, 240, 20);
        lblMatchCount.ForeColor = Color.Gray;
        searchPanel.Controls.Add(lblMatchCount);

        var btnCats = new Button();
        btnCats.Text = "分类 ▾";
        btnCats.SetBounds(176, 74, 70, 28);
        btnCats.FlatStyle = FlatStyle.Flat;
        btnCats.FlatAppearance.BorderColor = Color.Silver;
        btnCats.ForeColor = Color.FromArgb(40, 70, 120);
        btnCats.BackColor = Color.FromArgb(232, 240, 252);
        btnCats.Enabled = false;          // 有分组时才可用
        searchPanel.Controls.Add(btnCats);

        var btnToggleFold = new Button();
        btnToggleFold.Text = "全部展开";
        btnToggleFold.SetBounds(92, 74, 80, 28);
        btnToggleFold.FlatStyle = FlatStyle.Flat;
        btnToggleFold.FlatAppearance.BorderColor = Color.Silver;
        btnToggleFold.ForeColor = Color.FromArgb(40, 70, 120);
        btnToggleFold.BackColor = Color.FromArgb(232, 240, 252);
        btnToggleFold.Enabled = false;
        searchPanel.Controls.Add(btnToggleFold);

        var btnEditItems = new Button();
        btnEditItems.Text = "编辑条目";
        btnEditItems.SetBounds(6, 74, 82, 28);
        btnEditItems.BackColor = Color.FromArgb(232, 240, 252);
        btnEditItems.ForeColor = Color.FromArgb(24, 64, 128);
        btnEditItems.FlatStyle = FlatStyle.Flat;
        btnEditItems.FlatAppearance.BorderColor = Color.FromArgb(180, 205, 238);
        int idxForEdit = pageIndex;
        btnEditItems.Click += delegate { EditDataTab(idxForEdit); };
        searchPanel.Controls.Add(btnEditItems);

        top.Controls.Add(searchPanel);
        page.Controls.Add(top);

        TextBox idBox = null;
        bool hasModes = hasAlt || hasUlx;
        {
            var bar = new Panel();
            bar.Dock = DockStyle.Top;
            bar.Height = hasModes ? 68 : 36;           // 有写法切换才需要两行
            bar.Padding = new Padding(8, 4, 8, 2);
            bar.BackColor = Color.FromArgb(242, 246, 252);
            bar.Paint += delegate(object snd, PaintEventArgs pe)
            {
                using (var pen = new Pen(IsDark ? Color.FromArgb(48, 54, 61) : Color.FromArgb(226, 232, 240)))
                    pe.Graphics.DrawLine(pen, 0, bar.Height - 1, bar.Width, bar.Height - 1);
            };

            if (hasModes)
            {
                bool isJobPage = IsRolePage(label);

                rbMain = new RadioButton();
                rbMain.Text = isJobPage ? "常规生成 (forcespawn)" : "主指令";
                rbMain.SetBounds(8, 4, 186, 26);     // 不能用 Dock.Left：会占满整行高度，把 ID 框盖住
                rbMain.Checked = true;
                bar.Controls.Add(rbMain);

                // 备用档只属于角色页；其它页面把不同写法直接列成独立条目，这里不再给选项
                int ulxX = 458;
                if (isJobPage)
                {
                    rbAlt = new RadioButton();
                    rbAlt.Text = "原地替换 (devforcespawn" + (nAlt > 0 ? " · " + nAlt + " 条" : "") + ")";
                    rbAlt.SetBounds(198, 4, 252, 26);      // 放宽，避免"…· 153 条)"被裁掉
                    rbAlt.Visible = hasAlt;
                    bar.Controls.Add(rbAlt);
                }
                else
                {
                    ulxX = 198;
                }

                rbUlx = new RadioButton();
                rbUlx.Text = "控制台 ulx" + (nUlx > 0 ? " · " + nUlx + " 条" : "") + (nUlx == 0 ? "（本页无）" : "");
                rbUlx.SetBounds(ulxX, 4, 176, 26);      // 放得下「控制台 ulx · 870 条」
                rbUlx.Visible = hasUlx;
                bar.Controls.Add(rbUlx);

                var lblAltTip = new Label();
                lblAltTip.Text = hasUlx
                    ? "⚠ ulx 走游戏控制台（~）；主指令走聊天框"
                    : "⚠ 部分角色只能用 devforcespawn 或 ulx 指令刷新，以实测为准";
                int tipX = isJobPage ? (hasUlx ? 642 : 470) : (hasUlx ? 382 : 210);
                lblAltTip.SetBounds(tipX, 4, 300, 26);     // 和单选按钮同一行，省一整行高度
                lblAltTip.TextAlign = ContentAlignment.MiddleLeft;
                lblAltTip.ForeColor = Color.FromArgb(176, 92, 0);
                lblAltTip.Font = TrackFont("Microsoft YaHei UI", 8.5f, FontStyle.Regular);
                lblAltTip.UseMnemonic = false;
                lblAltTip.AutoSize = false;
                lblAltTip.AutoEllipsis = true;   // 宽度不够时显示省略号，不再硬切成半截
                bar.Controls.Add(lblAltTip);
                // 窗口够宽：提示和单选按钮同一行；太窄：挪到下面单独一行
                int tipBaseH = bar.Height;   // 68
                Action applyTipLayout = delegate
                {
                    int barW = bar.Width;
                    if (barW <= 0) return;
                    int avail = barW - tipX - 12;
                    int need = TextRenderer.MeasureText(lblAltTip.Text, lblAltTip.Font).Width + 8;
                    bool drop = avail < need;
                    if (drop)
                    {
                        int y = 66;
                        if (bar.Height != tipBaseH + 28) bar.Height = tipBaseH + 28;
                        if (lblAltTip.Top != y || lblAltTip.Left != 8)
                            lblAltTip.SetBounds(8, y, Math.Max(240, barW - 20), 22);
                        else
                            lblAltTip.Width = Math.Max(240, barW - 20);
                    }
                    else
                    {
                        if (bar.Height != tipBaseH) bar.Height = tipBaseH;
                        if (lblAltTip.Top != 4 || lblAltTip.Left != tipX)
                            lblAltTip.SetBounds(tipX, 4, Math.Max(120, avail), 26);
                        else
                            lblAltTip.Width = Math.Max(120, avail);
                    }
                };
                bar.Resize += delegate { applyTipLayout(); };
                bar.Paint += delegate { applyTipLayout(); };   // 首次布局/尺寸变化后补一次
                bar.HandleCreated += delegate
                {
                    try { BeginInvoke((MethodInvoker)delegate { applyTipLayout(); }); } catch { }
                };
            }

            int idY = hasModes ? 36 : 4;

            var lblId = new Label();
            lblId.Text = "目标 ID：";
            lblId.SetBounds(8, idY + 3, 74, 22);
            lblId.ForeColor = Color.FromArgb(60, 80, 110);
            bar.Controls.Add(lblId);

            idBox = new TextBox();
            idBox.SetBounds(84, idY, 86, 24);
            try { Native.SetCueBanner(idBox.Handle, "玩家游戏名"); } catch { }
            idBox.Font = TrackFont("Microsoft YaHei UI", 9.5f, FontStyle.Regular);
            bar.Controls.Add(idBox);

            var lblIdTip = new Label();
            lblIdTip.Text = "留空 = 用 ^（只对自己生效）；填玩家名自动替换 ^（名字带空格会自动加引号）";
            lblIdTip.SetBounds(180, idY + 3, 760, 22);
            lblIdTip.ForeColor = Color.FromArgb(110, 120, 135);
            lblIdTip.Font = TrackFont("Microsoft YaHei UI", 9f, FontStyle.Regular);
            bar.Controls.Add(lblIdTip);

            page.Controls.Add(bar);
            bar.BringToFront();
        }

        var list = new WrapList();
        list.Dock = DockStyle.Fill;
        list.DefaultCollapsed = true;
        list.Font = TrackFont("Microsoft YaHei UI", 9.5f, FontStyle.Regular);
        page.Controls.Add(list);
        list.BringToFront();

        int[] shown = new int[total];
        int shownN = 0;
        string[] vLabels = new string[0];
        string[] vValues = new string[0];
        string[] vAlts = new string[0];
        string[] vNotes = new string[0];
        bool[] vHeads = new bool[0];

        Func<int, string> cmdOf = delegate(int i)
        {
            string raw = bodies[i];
            if (mode == 1 && !string.IsNullOrEmpty(alts[i])) raw = alts[i];
            else if (mode == 2)
            {
                if (!string.IsNullOrEmpty(ulxs[i])) raw = ulxs[i];
                else if (ulxAuto && raw.Length > 1 && raw[0] == '!')
                    raw = "ulx " + UlxName(raw.Substring(1));  // !xxx → ulx xxx（带下划线的例外见 UlxName）
            }
            if (idBox != null) raw = ApplyTargetName(raw, idBox.Text);
            return raw;
        };

        Action<string> setPreviewContent = delegate(string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                preview.ForeColor = IsDark ? Color.FromArgb(139, 148, 158) : Color.FromArgb(140, 150, 165);
                preview.Text = EmptyPreviewHint;
            }
            else
            {
                preview.ForeColor = IsDark ? Color.FromArgb(230, 237, 243) : Color.FromArgb(20, 28, 40);
                preview.Text = content.Replace("\r\n", "\n").Replace("\n", "\r\n");
                preview.Select(0, 0);
            }
        };

        Action fitPreview = delegate
        {
            int w = preview.ClientSize.Width - 16;
            if (w < 80) w = 80;
            string txt = (string.IsNullOrEmpty(preview.Text) || preview.Text == EmptyPreviewHint) ? " " : preview.Text;
            Size sz = TextRenderer.MeasureText(txt, preview.Font, new Size(w, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
            int h = sz.Height + 22;
            if (h < 124) h = 124;
            if (h > 240) h = 240;
            if (top.Height != h) top.Height = h;
        };
        preview.Resize += delegate { fitPreview(); };

        Action syncFoldBtn = delegate
        {
            bool hasCol = list.HasCollapsible();
            btnToggleFold.Enabled = hasCol;
            btnToggleFold.Text = (!hasCol || list.AreAllCollapsed()) ? "全部展开" : "全部收起";
        };
        list.CollapseChanged += delegate { syncFoldBtn(); };
        btnToggleFold.Click += delegate
        {
            if (!list.HasCollapsible()) return;
            list.SetAllCollapsed(!list.AreAllCollapsed());
            list.Focus();
        };

        Action rebuildView = delegate
        {
            vLabels = new string[shownN];
            vValues = new string[shownN];
            vAlts = new string[shownN];
            vNotes = new string[shownN];
            vHeads = new bool[shownN];
            for (int k = 0; k < shownN; k++)
            {
                int i = shown[k];
                vLabels[k] = titles[i];
                vValues[k] = isHead[i] ? "" : cmdOf(i);
                vAlts[k] = alts[i];
                vNotes[k] = notes[i];
                vHeads[k] = isHead[i];
            }
            int keepScroll = list.ScrollTop;   // 切写法/筛选时不要跳回顶部
            int keepSelect = list.Sel;
            list.SetData(vLabels, vValues, vHeads, vNotes);
            list.ScrollTop = keepScroll;
            list.Sel = (keepSelect >= 0 && keepSelect < shownN && !list.IsRowHidden(keepSelect)) ? keepSelect : -1;
            list.Invalidate();

            bool anyHead = false;
            for (int k = 0; k < shownN; k++) { if (vHeads[k]) { anyHead = true; break; } }
            btnCats.Enabled = anyHead;
            syncFoldBtn();
        };

        Action<string> applyFilter = delegate(string q)
        {
            shownN = 0;
            int hitCmds = 0;
            string[] tokens = string.IsNullOrEmpty(q)
                ? new string[0]
                : q.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < total; i++)
            {
                bool hit;
                if (tokens.Length == 0) hit = true;
                else if (isHead[i]) hit = false;
                else
                {
                    hit = true;
                    for (int tk = 0; tk < tokens.Length; tk++)
                    {
                        string kw = tokens[tk];
                        bool kwHit = titles[i].IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0
                                  || bodies[i].IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0
                                  || alts[i].IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0
                                  || notes[i].IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0;
                        if (!kwHit) { hit = false; break; }
                    }
                }
                if (hit)
                {
                    shown[shownN++] = i;
                    if (!isHead[i]) hitCmds++;
                }
            }
            btnClearSearch.Visible = tokens.Length > 0;
            lblMatchCount.Text = tokens.Length > 0
                ? ("找到 " + hitCmds + " 条匹配（单击即复制）")
                : ("共 " + cmdCount + " 条（单击即复制）");
            list.EmptyText = tokens.Length > 0
                ? ("（未找到匹配“" + q + "”的条目，按 Esc 清空筛选）")
                : "（暂无条目）";
            rebuildView();
            setPreviewContent("");
            fitPreview();
        };

        if (idBox != null)
        {
            idBox.TextChanged += delegate { rebuildView(); list.Invalidate(); };
        }

        // ---------- 分类收纳表：点按钮 → 弹出当前可见分组 → 单击跳转 ----------
        Action showCats = delegate
        {
            var names = new System.Collections.Generic.List<string>();
            var idxs = new System.Collections.Generic.List<int>();
            bool inMajor = false;
            for (int k = 0; k < shownN; k++)
            {
                if (!vHeads[k]) continue;
                string hd = vLabels[k] ?? "";
                bool isMajor = hd.StartsWith("▓");
                if (isMajor) hd = hd.Substring(1).Trim();
                int cnt = 0;
                for (int m = k + 1; m < shownN && !vHeads[m]; m++) if (vValues[m].Length > 0) cnt++;
                if (isMajor)
                {
                    inMajor = true;
                    names.Add("■ " + hd + (cnt > 0 ? "    " + cnt + " 条" : ""));
                }
                else
                {
                    names.Add((inMajor ? "      " : "") + hd + (cnt > 0 ? "    " + cnt + " 条" : ""));
                }
                idxs.Add(k);
            }
            if (names.Count == 0) { toast.ShowText("当前列表没有分组"); return; }

            var pop = new Form();
            pop.FormBorderStyle = FormBorderStyle.FixedSingle;
            pop.ShowInTaskbar = false;
            pop.StartPosition = FormStartPosition.Manual;
            pop.Text = "分类（单击跳转，Esc 关闭）";
            pop.TopMost = true;              // 弹在工具窗口之上，点了就走
            pop.ClientSize = new Size(320, Math.Min(470, 44 + names.Count * 26));
            pop.BackColor = IsDark ? Color.FromArgb(22, 27, 34) : Color.White;
            pop.KeyPreview = true;

            var lb = new ListBox();
            lb.Dock = DockStyle.Fill;
            lb.BorderStyle = BorderStyle.None;
            lb.IntegralHeight = false;
            lb.BackColor = IsDark ? Color.FromArgb(22, 27, 34) : Color.White;
            lb.ForeColor = IsDark ? Color.FromArgb(230, 237, 243) : Color.FromArgb(24, 34, 48);
            lb.HandleCreated += delegate { try { Native.ApplyDarkScrollBar(lb.Handle, IsDark); } catch { } };
            lb.Font = TrackFont("Microsoft YaHei UI", 10f, FontStyle.Regular);
            for (int i = 0; i < names.Count; i++) lb.Items.Add(names[i]);
            pop.Controls.Add(lb);

            Action jump = delegate
            {
                int si = lb.SelectedIndex;
                if (si < 0 || si >= idxs.Count) return;
                int vi = idxs[si];
                pop.Tag = "picked";
                pop.Close();
                list.ScrollToIndex(vi);
                list.Sel = vi;
                list.Invalidate();
                list.Focus();
            };
            lb.Click += delegate { jump(); };
            lb.KeyDown += delegate(object ss, KeyEventArgs ee)
            {
                if (ee.KeyCode == Keys.Enter) { ee.Handled = true; jump(); }
                else if (ee.KeyCode == Keys.Escape) { ee.Handled = true; pop.Close(); }
            };
            pop.Deactivate += delegate
            {
                if (Convert.ToString(pop.Tag) == "picked") return;
                try { pop.Close(); } catch { }
            };

            Point pt = btnCats.PointToScreen(new Point(-pop.Width + btnCats.Width, btnCats.Height + 2));
            pop.Location = pt;
            pop.Show(this);
            lb.Focus();
            if (lb.Items.Count > 0) lb.SelectedIndex = 0;
        };
        btnCats.Click += delegate { showCats(); };
        var miCats = new ToolStripMenuItem("按分类跳转…");
        miCats.Click += delegate { showCats(); };

        list.SelChanged += delegate
        {
            int k = list.Sel;
            if (k < 0 || k >= shownN) { setPreviewContent(""); fitPreview(); return; }
            string pv = vHeads[k] ? "" : vValues[k];
            if (!vHeads[k] && !string.IsNullOrEmpty(vNotes[k]))
                pv = pv + Environment.NewLine + Environment.NewLine + vNotes[k];
            setPreviewContent(pv);
            fitPreview();
        };

        list.ItemActivated += delegate
        {
            int k = list.Sel;
            if (k < 0 || k >= shownN || vHeads[k]) return;
            CopyToClipboard(vLabels[k], vValues[k]);
        };

        list.ItemRightClick += delegate(object s, MouseEventArgs e)
        {
            int k = list.Sel;
            if (k < 0 || k >= shownN || vHeads[k]) return;
            var menu = new ContextMenuStrip();
            menu.Closed += delegate { SafeBeginInvoke(delegate { menu.Dispose(); }); };
            var it1 = new ToolStripMenuItem("复制主命令：" + Shorten(vValues[k]));
            it1.Click += delegate { CopyToClipboard(vLabels[k], vValues[k]); };
            menu.Items.Add(it1);
            if (!string.IsNullOrEmpty(vAlts[k]))
            {
                var it2 = new ToolStripMenuItem("复制替换命令：" + Shorten(vAlts[k]));
                it2.Click += delegate { CopyToClipboard(vLabels[k], vAlts[k]); };
                menu.Items.Add(it2);
            }
            var it3 = new ToolStripMenuItem("仅复制名称：" + Shorten(vLabels[k]));
            it3.Click += delegate { CopyToClipboard("条目名称", vLabels[k]); };
            menu.Items.Add(it3);

            menu.Items.Add(new ToolStripSeparator());

            var itEdit = new ToolStripMenuItem("修改本条…");
            int shownIdx = k;
            int pageForEdit = pageIndex;
            itEdit.Click += delegate
            {
                try
                {
                    int orig = shown[shownIdx];
                    if (pageForEdit < 0 || pageForEdit >= dataTabLists.Length) return;
                    var src = dataTabLists[pageForEdit];
                    if (orig < 0 || orig >= src.Count) return;

                    string raw = src[orig];
                    if (PromptItem("修改条目 · " + label, ref raw) && raw.Trim().Length > 0)
                    {
                        src[orig] = raw;
                        SaveDataOverrides();
                        Native.Log("data edit one: " + label + " #" + orig);
                        RebuildDataPage(pageForEdit);
                    }
                }
                catch (Exception ex) { Native.Log("edit one fail: " + ex.Message); }
            };
            menu.Items.Add(itEdit);

            menu.Show(list, e.Location);
        };

        if (hasAlt || hasUlx)
        {

            rbMain.CheckedChanged += delegate
            {
                if (!rbMain.Checked) return;
                mode = 0;
                int keepSel = list.Sel;
                rebuildView();
                if (keepSel >= 0 && keepSel < shownN)
                {
                    list.Sel = keepSel;
                    setPreviewContent(vHeads[keepSel] ? "" : vValues[keepSel]);
                    list.Invalidate();
                }
                fitPreview();
            };
            rbUlx.CheckedChanged += delegate
            {
                if (!rbUlx.Checked) return;
                mode = 2;
                int keepSel = list.Sel;
                rebuildView();
                if (mode == 2 && nUlx == 0) toast.ShowText("本页没有控制台 ulx 写法");
                if (mode == 1 && nAlt == 0) toast.ShowText("本页没有 devforcespawn 写法");
                if (keepSel >= 0 && keepSel < shownN)
                {
                    list.Sel = keepSel;
                    setPreviewContent(vHeads[keepSel] ? "" : vValues[keepSel]);
                    list.Invalidate();
                }
                fitPreview();
            };
            if (rbAlt != null) rbAlt.CheckedChanged += delegate
            {
                if (!rbAlt.Checked) return;
                mode = 1;
                int keepSel = list.Sel;
                rebuildView();
                if (mode == 2 && nUlx == 0) toast.ShowText("本页没有控制台 ulx 写法");
                if (mode == 1 && nAlt == 0) toast.ShowText("本页没有 devforcespawn 写法");
                if (keepSel >= 0 && keepSel < shownN)
                {
                    list.Sel = keepSel;
                    setPreviewContent(vHeads[keepSel] ? "" : vValues[keepSel]);
                    list.Invalidate();
                }
                fitPreview();
            };
        }

        btnClearSearch.Click += delegate { box.Text = ""; box.Focus(); };
        // 焦点在列表上时按 Esc 也能清空筛选（以前只有筛选框里按才行）
        list.KeyDown += delegate(object s3, KeyEventArgs e3)
        {
            if (e3.KeyCode != Keys.Escape) return;
            if (box.Text.Length == 0) return;
            box.Text = "";
            e3.Handled = true;
            e3.SuppressKeyPress = true;
        };
        box.TextChanged += delegate { applyFilter(box.Text.Trim()); };
        box.KeyDown += delegate(object s, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                box.Text = "";
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Down)
            {
                int start = (list.Sel < 0) ? 0 : (list.Sel + 1);
                int next = list.FindSelectable(start, 1);
                if (next >= 0)
                {
                    list.Sel = next;
                    list.EnsureVisible(next);
                    list.Invalidate();
                    setPreviewContent(vHeads[next] ? "" : vValues[next]);
                    fitPreview();
                }
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Up)
            {
                int start = (list.Sel <= 0) ? 0 : (list.Sel - 1);
                int prev = list.FindSelectable(start, -1);
                if (prev >= 0)
                {
                    list.Sel = prev;
                    list.EnsureVisible(prev);
                    list.Invalidate();
                    setPreviewContent(vHeads[prev] ? "" : vValues[prev]);
                    fitPreview();
                }
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                int target = (list.Sel >= 0 && list.Sel < shownN && !vHeads[list.Sel])
                    ? list.Sel
                    : list.FindSelectable(0, 1);
                if (target >= 0)
                {
                    list.Sel = target;
                    list.EnsureVisible(target);
                    list.Invalidate();
                    setPreviewContent(vValues[target]);
                    fitPreview();
                    CopyToClipboard(vLabels[target], vValues[target]);
                }
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        };

        applyFilter("");

        return page;
    }

    // ================= 自检（RX_SELFTEST=1，不建窗口） =================
    // 目的：纯逻辑改动可以用这个一秒验证，不用截图
    static bool IsRolePage(string label)
    {
        return label != null &&
               (label.IndexOf("角色", StringComparison.Ordinal) >= 0 ||
                label.IndexOf("职业", StringComparison.Ordinal) >= 0);
    }

    // 把命令里的 ^ 换成目标玩家名；名字带空格自动加引号
    static string ApplyTargetName(string raw, string idText)
    {
        if (string.IsNullOrEmpty(raw)) return raw ?? "";
        string idTxt = (idText ?? "").Trim();
        if (idTxt.Length == 0) return raw;
        if (raw.IndexOf('^') < 0) return raw;
        if (idTxt.IndexOf(' ') >= 0 && !(idTxt.StartsWith("\"") && idTxt.EndsWith("\"")))
            idTxt = "\"" + idTxt + "\"";
        int at = raw.IndexOf('^');
        return raw.Substring(0, at) + idTxt + raw.Substring(at + 1);
    }

    // ! 开头的命令默认「ulx + 名字」，但 ULX 里这几个是下划线写法：
    //   !forcespawn → ulx force_spawn 等（!forceround 例外，它本来就没有下划线）
    static string UlxName(string rest)
    {
        // rest 可能是「命令名 参数…」，只替换命令名那一段
        int sp = rest.IndexOf(' ');
        string head = sp < 0 ? rest : rest.Substring(0, sp);
        string tail = sp < 0 ? "" : rest.Substring(sp);
        switch (head)
        {
            case "forcespawn": head = "force_spawn"; break;
            case "forcescp": head = "force_scp"; break;
            case "forcespawn_events": head = "force_spawn_events"; break;
            case "forcespawn_special": head = "force_spawn_special"; break;
            case "devforcespawn": head = "dev_force_spawn"; break;
        }
        return head + tail;
    }

    static string UlxOf(string body, string explicitUlx)
    {
        if (!string.IsNullOrEmpty(explicitUlx))
        {
            // 兜底：表里若把一条 ! 命令塞进 ulx 槽，也统一成控制台写法
            if (explicitUlx.Length > 1 && explicitUlx[0] == '!')
                return "ulx " + UlxName(explicitUlx.Substring(1));
            return explicitUlx;
        }
        if (!string.IsNullOrEmpty(body) && body.Length > 1 && body[0] == '!')
            return "ulx " + UlxName(body.Substring(1));
        return body;
    }

    //   # 注释行
    //   【表名】
    //   标题<TAB>主指令<TAB>备用<TAB>ulx<TAB>备注
    //   ...
    static void DevSplitItem(string item, out string title, out string main, out string alt, out string ulx, out string note)
    {
        title = item ?? ""; main = ""; alt = ""; ulx = ""; note = "";
        int s1 = title.IndexOf('\u0001');
        if (s1 < 0) return;
        string rest = title.Substring(s1 + 1);
        title = title.Substring(0, s1);
        int s3 = rest.IndexOf('\u0003');
        if (s3 >= 0) { note = rest.Substring(s3 + 1); rest = rest.Substring(0, s3); }
        int s4 = rest.IndexOf('\u0004');
        if (s4 >= 0) { ulx = rest.Substring(s4 + 1); rest = rest.Substring(0, s4); }
        int s2 = rest.IndexOf('\u0002');
        if (s2 >= 0) { alt = rest.Substring(s2 + 1); rest = rest.Substring(0, s2); }
        main = rest;
    }

    // ===================== 运行时数据加载（phrases.json） =====================
    // 数据不再编进 exe：公开仓库构建出的 exe 与本机一致，数据文件单独分发
    public static class Data
    {
        public static string[] Command = new string[0];
        public static string[] Ability = new string[0];
        public static string[] Weapon = new string[0];
        public static string[] Model = new string[0];
        public static string[] Job = new string[0];
        public static string[] Scp = new string[0];
        public static string[] Misc = new string[0];

        public static bool Loaded;
        public static string SourcePath = "";

        // 解析 phrases.json 文本为分表字典；格式不对返回 null
        public static Dictionary<string, string[]> ParseTables(string text)
        {
            try
            {
                if (string.IsNullOrEmpty(text)) return null;
                object rootObj = MiniJson.Parse(text);
                var root = rootObj as Dictionary<string, object>;
                if (root == null) return null;

                var map = new Dictionary<string, string[]>();
                string[] keys = { "杂项", "指令表", "技能表", "物品表", "角色表", "SCP角色", "模型表" };
                for (int i = 0; i < keys.Length; i++)
                    map[keys[i]] = Build(root, keys[i]);
                return map;
            }
            catch
            {
                return null;
            }
        }

        // 载入 phrases.json；文件不存在返回 false（界面会显示空表）
        public static bool Load(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
                string text = File.ReadAllText(path, Encoding.UTF8);
                var map = ParseTables(text);
                if (map == null) return false;

                Misc = map["杂项"];
                Command = map["指令表"];
                Ability = map["技能表"];
                Weapon = map["物品表"];
                Job = map["角色表"];
                Scp = map["SCP角色"];
                Model = map["模型表"];
                SourcePath = path;
                Loaded = true;
                return true;
            }
            catch
            {
                return false;
            }
        }

        static string[] Build(Dictionary<string, object> root, string key)
        {
            object v;
            if (!root.TryGetValue(key, out v)) return new string[0];
            var arr = v as List<object>;
            if (arr == null) return new string[0];

            var outList = new List<string>(arr.Count);
            foreach (object item in arr)
            {
                var e = item as Dictionary<string, object>;
                if (e == null) continue;
                string title = Str(e, "title");
                string body = Str(e, "text");
                string alt = Str(e, "alt");
                string ulx = Str(e, "ulx");
                string note = Str(e, "note");
                if (title.Length == 0 && body.Length == 0) continue;
                outList.Add(MainForm.DevJoinItem(title, body, alt, ulx, note));
            }
            return outList.ToArray();
        }

        static string Str(Dictionary<string, object> e, string k)
        {
            object v;
            if (!e.TryGetValue(k, out v) || v == null) return "";
            return Convert.ToString(v);
        }
    }

    // 极简 JSON 解析：只支持本程序需要的子集（对象/数组/字符串/数字/真假/null）
    static class MiniJson
    {
        public static object Parse(string text)
        {
            int i = 0;
            object v = ParseValue(text, ref i);
            return v;
        }

        static void SkipWs(string s, ref int i)
        {
            while (i < s.Length)
            {
                char c = s[i];
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n') { i++; continue; }
                break;
            }
        }

        static object ParseValue(string s, ref int i)
        {
            SkipWs(s, ref i);
            if (i >= s.Length) return null;
            char c = s[i];
            if (c == '{') return ParseObject(s, ref i);
            if (c == '[') return ParseArray(s, ref i);
            if (c == '"') return ParseString(s, ref i);
            if (c == 't' && i + 4 <= s.Length) { i += 4; return true; }
            if (c == 'f' && i + 5 <= s.Length) { i += 5; return false; }
            if (c == 'n' && i + 4 <= s.Length) { i += 4; return null; }
            return ParseNumber(s, ref i);
        }

        static Dictionary<string, object> ParseObject(string s, ref int i)
        {
            var d = new Dictionary<string, object>();
            i++;                     // {
            while (true)
            {
                int stepStart = i;
                SkipWs(s, ref i);
                if (i >= s.Length) break;
                if (s[i] == '}') { i++; break; }
                if (s[i] == ',') { i++; continue; }
                string k = ParseString(s, ref i);
                SkipWs(s, ref i);
                if (i < s.Length && s[i] == ':') i++;
                object v = ParseValue(s, ref i);
                if (k != null) d[k] = v;
                SkipWs(s, ref i);
                if (i < s.Length && s[i] == '}') { i++; break; }
                if (i <= stepStart) { i++; break; }
            }
            return d;
        }

        static List<object> ParseArray(string s, ref int i)
        {
            var list = new List<object>();
            i++;                     // [
            while (true)
            {
                int stepStart = i;
                SkipWs(s, ref i);
                if (i >= s.Length) break;
                if (s[i] == ']') { i++; break; }
                if (s[i] == ',') { i++; continue; }
                list.Add(ParseValue(s, ref i));
                SkipWs(s, ref i);
                if (i < s.Length && s[i] == ']') { i++; break; }
                if (i <= stepStart) { i++; break; }
            }
            return list;
        }

        static string ParseString(string s, ref int i)
        {
            SkipWs(s, ref i);
            if (i >= s.Length || s[i] != '"') return "";
            i++;
            var sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') break;
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) break;
                char e = s[i++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (i + 4 <= s.Length)
                        {
                            int code;
                            if (int.TryParse(s.Substring(i, 4), System.Globalization.NumberStyles.HexNumber, null, out code))
                                sb.Append((char)code);
                            i += 4;
                        }
                        break;
                    default: sb.Append(e); break;
                }
            }
            return sb.ToString();
        }

        static double ParseNumber(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '-' || s[i] == '+' || s[i] == '.' || s[i] == 'e' || s[i] == 'E')) i++;
            if (i == start) { if (i < s.Length) i++; return 0d; }
            double d;
            double.TryParse(s.Substring(start, i - start), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d);
            return d;
        }
    }

    public static string DevJoinItem(string title, string main, string alt, string ulx, string note)
    {
        string s = title + "\u0001" + main;
        if (!string.IsNullOrEmpty(alt) || !string.IsNullOrEmpty(ulx)) s += "\u0002" + alt;
        else if (!string.IsNullOrEmpty(note)) s += "\u0002";   // 只有注解时也要占位，否则注解会并进指令
        if (!string.IsNullOrEmpty(ulx)) s += "\u0004" + ulx;
        if (!string.IsNullOrEmpty(note)) s += "\u0003" + note;
        return s;
    }

    static string DevCell(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            sb.Append(c < ' ' ? ' ' : c);
        }
        return sb.ToString().Trim();
    }

}
