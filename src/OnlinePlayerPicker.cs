#if ADMIN_EDITION
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

public class A2SPlayerInfo
{
    public string Name = "";
    public int Score = 0;
    public int DurationSec = 0;
    public PinyinTitle NamePinyin;
    public bool IsBot = false;

    public static bool DetectBotName(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        string s = name.Trim();
        if (!s.StartsWith("Bot", StringComparison.OrdinalIgnoreCase) || s.Length <= 3) return false;
        for (int i = 3; i < s.Length; i++)
            if (!char.IsDigit(s[i])) return false;
        return true;
    }

    public static string FormatDuration(int sec)
    {
        if (sec < 60) return "刚进服";
        if (sec < 3600) return (sec / 60) + "m";
        int h = sec / 3600;
        int m = (sec % 3600) / 60;
        return h + "h" + m + "m";
    }
}

public class OnlinePlayerCache
{
    public List<A2SPlayerInfo> Players = new List<A2SPlayerInfo>();
    public int PingMs = -1;
    public DateTime FetchedAt = DateTime.MinValue;
    public string DisplayServerName = "";
}

public class OnlineServerEntry
{
    public string Addr = "";
    public string Label = "";
}

internal sealed class OnlinePlayerListView : Control
{
    public List<A2SPlayerInfo> Items = new List<A2SPlayerInfo>();
    public string SelfName = "";
    public int SelectedIndex = -1;

    public event Action<int> SelectionChanged;
    public event Action<int> PickPlayerDoubleClicked;
    public event Action<int> CopyPlayerClicked;
    public event Action<int, Point> RowContextRequested;

    const int RowH = 34;
    const int ScrollW = 10;
    const int CopyBtnW = 48;
    const int CopyBtnH = 22;

    int scrollOffset = 0;
    int hoverIdx = -1;
    bool hoverCopy = false;
    bool draggingThumb = false;
    int dragStartY = 0;
    int dragStartOffset = 0;

    Font fIdx, fName, fMeta, fBadge;

    public OnlinePlayerListView()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint
            | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        fIdx = new Font("Consolas", 8.5f, FontStyle.Regular);
        fName = new Font("Microsoft YaHei UI", 9.2f, FontStyle.Regular);
        fMeta = new Font("Microsoft YaHei UI", 8.2f, FontStyle.Regular);
        fBadge = new Font("Microsoft YaHei UI", 7.5f, FontStyle.Bold);
    }

    public A2SPlayerInfo SelectedPlayer
    {
        get
        {
            if (Items == null || SelectedIndex < 0 || SelectedIndex >= Items.Count) return null;
            return Items[SelectedIndex];
        }
    }

    int ContentWidth
    {
        get
        {
            bool needScroll = Items != null && Items.Count * RowH > ClientSize.Height;
            return Math.Max(120, ClientSize.Width - (needScroll ? ScrollW + 2 : 2));
        }
    }

    int MaxScroll
    {
        get
        {
            if (Items == null) return 0;
            return Math.Max(0, Items.Count * RowH - ClientSize.Height + 4);
        }
    }

    public void SetItems(List<A2SPlayerInfo> list, int preferIndex)
    {
        Items = list ?? new List<A2SPlayerInfo>();
        if (Items.Count == 0) SelectedIndex = -1;
        else if (preferIndex >= 0 && preferIndex < Items.Count) SelectedIndex = preferIndex;
        else SelectedIndex = 0;
        scrollOffset = Math.Max(0, Math.Min(MaxScroll, scrollOffset));
        EnsureVisible(SelectedIndex);
        Invalidate();
    }

    public void EnsureVisible(int idx)
    {
        if (Items == null || idx < 0 || idx >= Items.Count) return;
        int top = idx * RowH;
        int bot = top + RowH;
        if (top < scrollOffset) scrollOffset = top;
        else if (bot > scrollOffset + ClientSize.Height - 2) scrollOffset = Math.Max(0, bot - ClientSize.Height + 2);
        scrollOffset = Math.Max(0, Math.Min(MaxScroll, scrollOffset));
        Invalidate();
    }

    Rectangle GetRowCopyRect(int rowIdx)
    {
        int y = rowIdx * RowH - scrollOffset;
        int cw = ContentWidth;
        return new Rectangle(cw - CopyBtnW - 8, y + (RowH - CopyBtnH) / 2, CopyBtnW, CopyBtnH);
    }

    Rectangle GetThumbRect()
    {
        int totalH = (Items != null ? Items.Count : 0) * RowH;
        int viewH = Math.Max(1, ClientSize.Height);
        if (totalH <= viewH) return Rectangle.Empty;
        int trackH = Math.Max(20, viewH - 6);
        int thumbH = Math.Max(24, trackH * viewH / totalH);
        int maxS = Math.Max(1, totalH - viewH + 4);
        int top = 3 + (int)((long)(trackH - thumbH) * scrollOffset / maxS);
        return new Rectangle(ClientSize.Width - ScrollW + 1, top, ScrollW - 3, thumbH);
    }

    bool IsSelfPlayer(string name)
    {
        if (string.IsNullOrEmpty(SelfName) || string.IsNullOrEmpty(name)) return false;
        string s = SelfName.Trim();
        if (s.Length == 0) return false;
        return string.Equals(name.Trim(), s, StringComparison.OrdinalIgnoreCase)
            || name.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        int w = ClientSize.Width;
        int h = ClientSize.Height;
        g.Clear(ThemePalette.Surface);

        if (Items == null || Items.Count == 0)
        {
            TextRenderer.DrawText(g, "暂无在线玩家，可切换服务器或清空搜索词", fName,
                new Rectangle(12, 0, Math.Max(40, w - 24), h), ThemePalette.TextMuted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak);
            using (var borderPen = new Pen(ThemePalette.BorderSubtle, 1f))
                g.DrawRectangle(borderPen, 0, 0, w - 1, h - 1);
            return;
        }

        int cw = ContentWidth;
        int first = Math.Max(0, scrollOffset / RowH);
        int last = Math.Min(Items.Count - 1, (scrollOffset + h) / RowH + 1);

        SmoothingMode oldSm = g.SmoothingMode;
        for (int i = first; i <= last; i++)
        {
            A2SPlayerInfo p = Items[i];
            int ry = i * RowH - scrollOffset;
            var rc = new Rectangle(2, ry + 1, cw - 3, RowH - 2);
            bool sel = (i == SelectedIndex);
            bool hov = (i == hoverIdx);
            bool isMe = IsSelfPlayer(p.Name);

            if (sel)
            {
                using (var br = new SolidBrush(ThemePalette.Selected))
                    Native.FillRoundedRect(g, br, rc, 5);
                using (var pen = new Pen(ThemePalette.BorderFocus, 1f))
                    Native.DrawRoundedRect(g, pen, new Rectangle(rc.X, rc.Y, rc.Width - 1, rc.Height - 1), 5);
            }
            else if (hov)
            {
                using (var br = new SolidBrush(ThemePalette.Hover))
                    Native.FillRoundedRect(g, br, rc, 5);
            }
            else if ((i & 1) == 1)
            {
                using (var br = new SolidBrush(ThemePalette.SurfaceAlt))
                    g.FillRectangle(br, rc);
            }

            // 序号
            var idxRc = new Rectangle(8, ry, 34, RowH);
            TextRenderer.DrawText(g, "#" + (i + 1), fIdx, idxRc, ThemePalette.TextMuted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);

            // 状态点 / 我徽章 / BOT 徽章
            int nameLeft = 44;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (isMe)
            {
                var meRc = new Rectangle(nameLeft, ry + 8, 22, 18);
                using (var mbr = new SolidBrush(ThemePalette.Accent))
                    Native.FillRoundedRect(g, mbr, meRc, 4);
                TextRenderer.DrawText(g, "我", fBadge, meRc, Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
                nameLeft += 28;
            }
            else
            {
                Color dotCol = p.IsBot ? ThemePalette.TextMuted : ThemePalette.Success;
                using (var dbr = new SolidBrush(dotCol))
                    g.FillEllipse(dbr, nameLeft, ry + (RowH - 6) / 2, 6, 6);
                nameLeft += 12;
                if (p.IsBot)
                {
                    var botRc = new Rectangle(nameLeft, ry + 9, 30, 16);
                    using (var bbr = new SolidBrush(ThemePalette.CodeChipBg))
                        Native.FillRoundedRect(g, bbr, botRc, 3);
                    TextRenderer.DrawText(g, "BOT", fBadge, botRc, ThemePalette.TextMuted,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
                    nameLeft += 34;
                }
            }
            g.SmoothingMode = oldSm;

            // 右侧复制胶囊与在线时长
            Rectangle copyRc = GetRowCopyRect(i);
            int durW = 54;
            var durRc = new Rectangle(copyRc.Left - durW - 6, ry, durW, RowH);
            string durTxt = A2SPlayerInfo.FormatDuration(p.DurationSec);
            Color durCol = (p.DurationSec < 60 && !p.IsBot) ? ThemePalette.Success : ThemePalette.TextMuted;
            TextRenderer.DrawText(g, durTxt, fMeta, durRc, durCol,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);

            bool copyHot = hov && hoverCopy;
            using (var cbg = new SolidBrush(copyHot ? ThemePalette.AccentSoft : ThemePalette.CodeChipBg))
                Native.FillRoundedRect(g, cbg, copyRc, 4);
            using (var ced = new Pen(copyHot ? ThemePalette.Accent : ThemePalette.BorderSubtle, 1f))
                Native.DrawRoundedRect(g, ced, new Rectangle(copyRc.X, copyRc.Y, copyRc.Width - 1, copyRc.Height - 1), 4);
            TextRenderer.DrawText(g, "复制", fMeta, copyRc, copyHot ? ThemePalette.AccentSoftFg : ThemePalette.TextSecondary,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);

            // 玩家原样昵称（务必 NoPrefix 防止 & 变成下划线助记符）
            int nameW = Math.Max(40, durRc.Left - nameLeft - 6);
            var nameRc = new Rectangle(nameLeft, ry, nameW, RowH);
            TextRenderer.DrawText(g, p.Name ?? "", fName, nameRc, ThemePalette.TextPrimary,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        }

        // 自绘细滚动条
        Rectangle thumb = GetThumbRect();
        if (!thumb.IsEmpty)
        {
            using (var tbg = new SolidBrush(ThemePalette.SurfaceAlt))
                g.FillRectangle(tbg, w - ScrollW, 1, ScrollW - 1, h - 2);
            using (var tbr = new SolidBrush(ThemePalette.Border))
                Native.FillRoundedRect(g, tbr, thumb, 3);
        }

        using (var borderPen = new Pen(ThemePalette.BorderSubtle, 1f))
            g.DrawRectangle(borderPen, 0, 0, w - 1, h - 1);
    }

    int RowAtY(int y)
    {
        if (Items == null || Items.Count == 0) return -1;
        int idx = (y + scrollOffset) / RowH;
        return (idx >= 0 && idx < Items.Count) ? idx : -1;
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        int step = (e.Delta > 0 ? -1 : 1) * RowH * 3;
        int next = Math.Max(0, Math.Min(MaxScroll, scrollOffset + step));
        if (next != scrollOffset)
        {
            scrollOffset = next;
            Invalidate();
        }
        base.OnMouseWheel(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        Focus();
        Rectangle thumb = GetThumbRect();
        if (e.Button == MouseButtons.Left && !thumb.IsEmpty && e.X >= ClientSize.Width - ScrollW)
        {
            if (thumb.Contains(e.Location))
            {
                draggingThumb = true;
                dragStartY = e.Y;
                dragStartOffset = scrollOffset;
            }
            else
            {
                int trackH = Math.Max(1, ClientSize.Height - thumb.Height - 6);
                int clickPos = Math.Max(0, Math.Min(trackH, e.Y - thumb.Height / 2 - 3));
                scrollOffset = (int)((long)clickPos * MaxScroll / trackH);
                Invalidate();
            }
            return;
        }

        int idx = RowAtY(e.Y);
        if (idx >= 0 && idx != SelectedIndex)
        {
            SelectedIndex = idx;
            Invalidate();
            if (SelectionChanged != null) SelectionChanged(idx);
        }
        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (draggingThumb)
        {
            Rectangle thumb = GetThumbRect();
            int trackH = Math.Max(1, ClientSize.Height - thumb.Height - 6);
            int deltaY = e.Y - dragStartY;
            scrollOffset = Math.Max(0, Math.Min(MaxScroll, dragStartOffset + (int)((long)deltaY * MaxScroll / trackH)));
            Invalidate();
            return;
        }
        int nextHover = (e.X < ContentWidth) ? RowAtY(e.Y) : -1;
        bool nextCopy = (nextHover >= 0) && GetRowCopyRect(nextHover).Contains(e.Location);
        if (nextHover != hoverIdx || nextCopy != hoverCopy)
        {
            hoverIdx = nextHover;
            hoverCopy = nextCopy;
            Cursor = (nextHover >= 0) ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (draggingThumb)
        {
            draggingThumb = false;
            return;
        }
        int idx = (e.X < ContentWidth) ? RowAtY(e.Y) : -1;
        if (idx >= 0)
        {
            if (e.Button == MouseButtons.Left && GetRowCopyRect(idx).Contains(e.Location))
            {
                if (CopyPlayerClicked != null) CopyPlayerClicked(idx);
                return;
            }
            if (e.Button == MouseButtons.Right && RowContextRequested != null)
            {
                RowContextRequested(idx, PointToScreen(e.Location));
                return;
            }
        }
        base.OnMouseUp(e);
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && e.X < ContentWidth)
        {
            int idx = RowAtY(e.Y);
            if (idx >= 0 && !GetRowCopyRect(idx).Contains(e.Location))
            {
                if (PickPlayerDoubleClicked != null) PickPlayerDoubleClicked(idx);
                return;
            }
        }
        base.OnMouseDoubleClick(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        if (hoverIdx != -1 || hoverCopy)
        {
            hoverIdx = -1;
            hoverCopy = false;
            Cursor = Cursors.Default;
            Invalidate();
        }
        base.OnMouseLeave(e);
    }

    protected override bool IsInputKey(Keys keyData)
    {
        if (keyData == Keys.Up || keyData == Keys.Down || keyData == Keys.PageUp || keyData == Keys.PageDown)
            return true;
        return base.IsInputKey(keyData);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        MoveSelectionByKey(e.KeyCode);
        base.OnKeyDown(e);
    }

    public bool MoveSelectionByKey(Keys key)
    {
        if (Items == null || Items.Count == 0) return false;
        int cur = SelectedIndex;
        int next = cur;
        if (key == Keys.Down) next = Math.Min(Items.Count - 1, Math.Max(0, cur + 1));
        else if (key == Keys.Up) next = Math.Max(0, (cur < 0 ? 0 : cur - 1));
        else if (key == Keys.PageDown) next = Math.Min(Items.Count - 1, Math.Max(0, (cur < 0 ? 0 : cur) + 8));
        else if (key == Keys.PageUp) next = Math.Max(0, (cur < 0 ? 0 : cur) - 8);
        else if (key == Keys.Home) next = 0;
        else if (key == Keys.End) next = Items.Count - 1;
        else return false;

        if (next != SelectedIndex)
        {
            SelectedIndex = next;
            EnsureVisible(next);
            if (SelectionChanged != null) SelectionChanged(next);
        }
        return true;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (fIdx != null) { fIdx.Dispose(); fIdx = null; }
            if (fName != null) { fName.Dispose(); fName = null; }
            if (fMeta != null) { fMeta.Dispose(); fMeta = null; }
            if (fBadge != null) { fBadge.Dispose(); fBadge = null; }
        }
        base.Dispose(disposing);
    }
}

public class OnlinePlayerPickerForm : Form
{
    readonly List<OnlineServerEntry> servers;
    readonly Dictionary<string, OnlinePlayerCache> cache;
    readonly Action<string> onPickTarget;
    readonly Action<string> onCopyRaw;
    readonly Action<string> onCopyQuoted;
    readonly Action<string, bool, int, string> onPrefsChanged;

    ComboBox cmbServer, cmbSort;
    Button btnRefresh, btnLocate, btnSetTarget, btnCopyRaw, btnResetSelf;
    TextBox txtMyName, txtSearch;
    CheckBox chkHideBots;
    Label lblCount, lblState, lblBottomHint;
    OnlinePlayerListView listView;

    List<A2SPlayerInfo> currentRawPlayers = new List<A2SPlayerInfo>();
    string currentAddr = "";
    string myName = "";
    bool suppressServerChange = false;
    bool suppressPrefEvent = false;
    int querySeq = 0;

    Font fReg, fBold, fSmall;

    public OnlinePlayerPickerForm(
        List<OnlineServerEntry> serverList,
        string initialAddr,
        string initialMyName,
        bool initialHideBots,
        int initialSortMode,
        Dictionary<string, OnlinePlayerCache> sharedCache,
        Action<string> onPickTarget,
        Action<string> onCopyRaw,
        Action<string> onCopyQuoted,
        Action<string, bool, int, string> onPrefsChanged)
    {
        servers = serverList ?? new List<OnlineServerEntry>();
        cache = sharedCache ?? new Dictionary<string, OnlinePlayerCache>(StringComparer.OrdinalIgnoreCase);
        this.onPickTarget = onPickTarget;
        this.onCopyRaw = onCopyRaw;
        this.onCopyQuoted = onCopyQuoted;
        this.onPrefsChanged = onPrefsChanged;
        myName = (initialMyName ?? "").Trim();

        AutoScaleMode = AutoScaleMode.None;
        Text = "选择在线玩家";
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = false;
        MaximizeBox = true;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(580, 540);
        MinimumSize = new Size(490, 420);
        KeyPreview = true;
        BackColor = ThemePalette.Canvas;
        ForeColor = ThemePalette.TextPrimary;

        fReg = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular);
        fBold = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
        fSmall = new Font("Microsoft YaHei UI", 8.2f, FontStyle.Regular);
        Font = fReg;

        BuildUi(initialAddr, initialHideBots, initialSortMode);
        HandleCreated += delegate { try { Native.SetDarkTitleBar(Handle, MainForm.IsDark); } catch { } };
    }

    void BuildUi(string initialAddr, bool initialHideBots, int initialSortMode)
    {
        var pnlTop = new Panel();
        pnlTop.Dock = DockStyle.Top;
        pnlTop.Height = 110;
        pnlTop.BackColor = ThemePalette.Surface;
        pnlTop.Paint += delegate(object snd, PaintEventArgs pe)
        {
            using (var pen = new Pen(ThemePalette.BorderSubtle, 1f))
                pe.Graphics.DrawLine(pen, 0, pnlTop.Height - 1, pnlTop.Width, pnlTop.Height - 1);
        };

        var lblSrv = new Label();
        lblSrv.Text = "服务器：";
        lblSrv.ForeColor = ThemePalette.TextSecondary;
        lblSrv.SetBounds(12, 12, 58, 22);
        pnlTop.Controls.Add(lblSrv);

        cmbServer = new ThemedComboBox();
        cmbServer.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbServer.SetBounds(72, 9, 270, 26);
        cmbServer.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        for (int i = 0; i < servers.Count; i++)
            cmbServer.Items.Add(servers[i].Label);
        cmbServer.SelectedIndexChanged += delegate
        {
            if (suppressServerChange) return;
            int ix = cmbServer.SelectedIndex;
            if (ix >= 0 && ix < servers.Count)
            {
                currentAddr = servers[ix].Addr;
                NotifyPrefs();
                LoadServerPlayersAsync(currentAddr, false);
            }
        };
        pnlTop.Controls.Add(cmbServer);

        btnRefresh = new Button();
        btnRefresh.Text = "↻ 刷新";
        btnRefresh.SetBounds(350, 9, 70, 26);
        btnRefresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        StyleBtn(btnRefresh, false);
        btnRefresh.Click += delegate
        {
            if (!string.IsNullOrEmpty(currentAddr))
                LoadServerPlayersAsync(currentAddr, true);
        };
        pnlTop.Controls.Add(btnRefresh);

        btnLocate = new Button();
        btnLocate.Text = "查找我在哪个服";
        btnLocate.SetBounds(426, 9, 140, 26);
        btnLocate.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        StyleBtn(btnLocate, true);
        btnLocate.Click += delegate { LocateMyServerAsync(); };
        pnlTop.Controls.Add(btnLocate);

        // 第 2 行：我的 Steam 昵称 + 过滤 Bot + 排序
        var lblMe = new Label();
        lblMe.Text = "我的昵称：";
        lblMe.ForeColor = ThemePalette.TextSecondary;
        lblMe.SetBounds(12, 45, 72, 22);
        pnlTop.Controls.Add(lblMe);

        txtMyName = new TextBox();
        txtMyName.Text = myName;
        txtMyName.BackColor = ThemePalette.InputBg;
        txtMyName.ForeColor = ThemePalette.TextPrimary;
        txtMyName.SetBounds(84, 42, 168, 24);
        new TextBoxFrame(txtMyName);
        txtMyName.HandleCreated += delegate { Native.SetCueBanner(txtMyName.Handle, "填你的游戏名，方便查找所在服"); };
        txtMyName.Leave += delegate { SaveMyNameFromInput(); };
        txtMyName.KeyDown += delegate(object snd, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                SaveMyNameFromInput();
                LocateMyServerAsync();
            }
        };
        pnlTop.Controls.Add(txtMyName);

        chkHideBots = new CheckBox();
        chkHideBots.Text = "隐藏 Bot";
        chkHideBots.ForeColor = ThemePalette.TextPrimary;
        chkHideBots.BackColor = Color.Transparent;
        chkHideBots.Checked = initialHideBots;
        chkHideBots.SetBounds(264, 44, 86, 22);
        chkHideBots.CheckedChanged += delegate
        {
            if (suppressPrefEvent) return;
            NotifyPrefs();
            ApplyFilterAndSort();
        };
        pnlTop.Controls.Add(chkHideBots);

        var lblSort = new Label();
        lblSort.Text = "排序：";
        lblSort.ForeColor = ThemePalette.TextSecondary;
        lblSort.SetBounds(360, 45, 44, 22);
        lblSort.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        pnlTop.Controls.Add(lblSort);

        cmbSort = new ThemedComboBox();
        cmbSort.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbSort.Items.AddRange(new object[] { "最新进服", "在线最久", "按名称" });
        cmbSort.SelectedIndex = Math.Max(0, Math.Min(2, initialSortMode));
        cmbSort.SetBounds(406, 42, 160, 26);
        cmbSort.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        cmbSort.SelectedIndexChanged += delegate
        {
            if (suppressPrefEvent) return;
            NotifyPrefs();
            ApplyFilterAndSort();
        };
        pnlTop.Controls.Add(cmbSort);

        // 第 3 行：拼音/关键词搜索框 + 人数/状态
        txtSearch = new TextBox();
        txtSearch.BackColor = ThemePalette.InputBg;
        txtSearch.ForeColor = ThemePalette.TextPrimary;
        txtSearch.SetBounds(12, 75, 318, 25);
        txtSearch.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        new TextBoxFrame(txtSearch);
        txtSearch.HandleCreated += delegate { Native.SetCueBanner(txtSearch.Handle, "搜索玩家名 | 支持拼音 | 特殊符号可直接滚动选"); };
        txtSearch.TextChanged += delegate { ApplyFilterAndSort(); };
        Shown += delegate { if (txtSearch != null && !txtSearch.IsDisposed) txtSearch.Focus(); };
        pnlTop.Controls.Add(txtSearch);

        lblCount = new Label();
        lblCount.Text = "0 人";
        lblCount.Font = fBold;
        lblCount.ForeColor = ThemePalette.AccentSoftFg;
        lblCount.TextAlign = ContentAlignment.MiddleRight;
        lblCount.SetBounds(336, 76, 86, 22);
        lblCount.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        pnlTop.Controls.Add(lblCount);

        lblState = new Label();
        lblState.Text = "就绪";
        lblState.Font = fSmall;
        lblState.ForeColor = ThemePalette.TextMuted;
        lblState.TextAlign = ContentAlignment.MiddleRight;
        lblState.AutoEllipsis = true;
        lblState.SetBounds(424, 77, 142, 20);
        lblState.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        pnlTop.Controls.Add(lblState);

        // 底部栏
        var pnlBot = new Panel();
        pnlBot.Dock = DockStyle.Bottom;
        pnlBot.Height = 46;
        pnlBot.BackColor = ThemePalette.Surface;
        pnlBot.Paint += delegate(object snd, PaintEventArgs pe)
        {
            using (var pen = new Pen(ThemePalette.BorderSubtle, 1f))
                pe.Graphics.DrawLine(pen, 0, 0, pnlBot.Width, 0);
        };

        lblBottomHint = new Label();
        lblBottomHint.Text = "双击或回车设为目标 · Ctrl+C 复制昵称 · 右键更多";
        lblBottomHint.Font = fSmall;
        lblBottomHint.ForeColor = ThemePalette.TextMuted;
        lblBottomHint.TextAlign = ContentAlignment.MiddleLeft;
        lblBottomHint.AutoEllipsis = true;
        lblBottomHint.SetBounds(12, 12, 244, 22);
        lblBottomHint.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
        pnlBot.Controls.Add(lblBottomHint);

        btnResetSelf = new Button();
        btnResetSelf.Text = "重置为 ^";
        btnResetSelf.SetBounds(264, 9, 82, 28);
        btnResetSelf.Anchor = AnchorStyles.Right | AnchorStyles.Top;
        StyleBtn(btnResetSelf, false);
        btnResetSelf.Click += delegate
        {
            if (onPickTarget != null) onPickTarget("");
            Close();
        };
        pnlBot.Controls.Add(btnResetSelf);

        btnCopyRaw = new Button();
        btnCopyRaw.Text = "复制昵称";
        btnCopyRaw.SetBounds(352, 9, 92, 28);
        btnCopyRaw.Anchor = AnchorStyles.Right | AnchorStyles.Top;
        StyleBtn(btnCopyRaw, false);
        btnCopyRaw.Click += delegate { CopySelectedPlayerRaw(); };
        pnlBot.Controls.Add(btnCopyRaw);

        btnSetTarget = new Button();
        btnSetTarget.Text = "设为全局目标";
        btnSetTarget.Font = fBold;
        btnSetTarget.SetBounds(450, 9, 118, 28);
        btnSetTarget.Anchor = AnchorStyles.Right | AnchorStyles.Top;
        StyleBtn(btnSetTarget, true);
        btnSetTarget.Click += delegate { PickSelectedPlayerAndClose(); };
        pnlBot.Controls.Add(btnSetTarget);

        // 中部列表
        var pnlListHost = new Panel();
        pnlListHost.Dock = DockStyle.Fill;
        pnlListHost.Padding = new Padding(10, 8, 10, 8);
        pnlListHost.BackColor = ThemePalette.Canvas;

        listView = new OnlinePlayerListView();
        listView.Dock = DockStyle.Fill;
        listView.SelfName = myName;
        listView.SelectionChanged += delegate { UpdateBottomButtons(); };
        listView.PickPlayerDoubleClicked += delegate(int ix)
        {
            listView.SelectedIndex = ix;
            PickSelectedPlayerAndClose();
        };
        listView.CopyPlayerClicked += delegate(int ix)
        {
            listView.SelectedIndex = ix;
            CopySelectedPlayerRaw();
        };
        listView.RowContextRequested += delegate(int ix, Point pt)
        {
            ShowRowContextMenu(ix, pt);
        };
        pnlListHost.Controls.Add(listView);

        Controls.Add(pnlListHost);
        Controls.Add(pnlBot);
        Controls.Add(pnlTop);

        // 默认选中服务器并拉取
        if (servers.Count > 0)
        {
            int initIdx = 0;
            if (!string.IsNullOrEmpty(initialAddr))
            {
                for (int i = 0; i < servers.Count; i++)
                {
                    if (string.Equals(servers[i].Addr, initialAddr, StringComparison.OrdinalIgnoreCase))
                    {
                        initIdx = i;
                        break;
                    }
                }
            }
            suppressServerChange = true;
            cmbServer.SelectedIndex = initIdx;
            suppressServerChange = false;
            currentAddr = servers[initIdx].Addr;
            LoadServerPlayersAsync(currentAddr, false);
        }
        else
        {
            lblState.Text = "收藏夹暂无服务器";
            UpdateBottomButtons();
        }
    }

    static void StyleBtn(Button b, bool primary)
    {
        b.FlatStyle = FlatStyle.Flat;
        b.Cursor = Cursors.Hand;
        if (primary)
        {
            b.BackColor = ThemePalette.AccentSoft;
            b.ForeColor = ThemePalette.AccentSoftFg;
            b.FlatAppearance.BorderColor = ThemePalette.Accent;
        }
        else
        {
            b.BackColor = ThemePalette.Elevated;
            b.ForeColor = ThemePalette.TextPrimary;
            b.FlatAppearance.BorderColor = ThemePalette.Border;
        }
    }

    void SaveMyNameFromInput()
    {
        if (txtMyName == null) return;
        string clean = (txtMyName.Text ?? "").Trim();
        if (clean != myName)
        {
            myName = clean;
            if (listView != null)
            {
                listView.SelfName = myName;
                listView.Invalidate();
            }
            NotifyPrefs();
        }
    }

    void NotifyPrefs()
    {
        if (onPrefsChanged != null)
        {
            onPrefsChanged(
                myName,
                chkHideBots == null || chkHideBots.Checked,
                cmbSort != null ? cmbSort.SelectedIndex : 0,
                currentAddr);
        }
    }

    void UpdateBottomButtons()
    {
        bool has = (listView != null && listView.SelectedPlayer != null);
        if (btnSetTarget != null) btnSetTarget.Enabled = has;
        if (btnCopyRaw != null) btnCopyRaw.Enabled = has;
    }

    public static List<A2SPlayerInfo> FilterAndSort(List<A2SPlayerInfo> source, string query, bool hideBots, int sortMode)
    {
        var result = new List<A2SPlayerInfo>();
        if (source == null || source.Count == 0) return result;

        string q = (query ?? "").Trim();
        bool hasQ = q.Length > 0;
        var scored = hasQ ? new List<KeyValuePair<A2SPlayerInfo, int>>() : null;

        for (int i = 0; i < source.Count; i++)
        {
            A2SPlayerInfo p = source[i];
            if (p == null) continue;
            if (hideBots && p.IsBot) continue;

            if (!hasQ)
            {
                result.Add(p);
                continue;
            }

            int sc = 0;
            string nm = p.Name ?? "";
            int idx = nm.IndexOf(q, StringComparison.OrdinalIgnoreCase);
            if (idx == 0) sc = 120;
            else if (idx > 0) sc = 100;
            else
            {
                if (p.NamePinyin == null) p.NamePinyin = PinyinHelper.BuildTitle(nm);
                int py = PinyinHelper.MatchScore(p.NamePinyin, q);
                if (py > 0) sc = py;
            }
            if (sc > 0) scored.Add(new KeyValuePair<A2SPlayerInfo, int>(p, sc));
        }

        if (hasQ)
        {
            scored.Sort(delegate(KeyValuePair<A2SPlayerInfo, int> a, KeyValuePair<A2SPlayerInfo, int> b)
            {
                int c = b.Value.CompareTo(a.Value);
                if (c != 0) return c;
                if (sortMode == 1) return b.Key.DurationSec.CompareTo(a.Key.DurationSec);
                if (sortMode == 2) return StringComparer.OrdinalIgnoreCase.Compare(a.Key.Name, b.Key.Name);
                return a.Key.DurationSec.CompareTo(b.Key.DurationSec);
            });
            for (int i = 0; i < scored.Count; i++) result.Add(scored[i].Key);
            return result;
        }

        result.Sort(delegate(A2SPlayerInfo a, A2SPlayerInfo b)
        {
            if (sortMode == 1)
            {
                int dc = b.DurationSec.CompareTo(a.DurationSec);
                if (dc != 0) return dc;
            }
            else if (sortMode == 0)
            {
                int dc = a.DurationSec.CompareTo(b.DurationSec);
                if (dc != 0) return dc;
            }
            return StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name);
        });
        return result;
    }

    void ApplyFilterAndSort()
    {
        bool hideBots = chkHideBots == null || chkHideBots.Checked;
        int sortMode = cmbSort != null ? cmbSort.SelectedIndex : 0;
        string q = txtSearch != null ? txtSearch.Text : "";
        List<A2SPlayerInfo> shown = FilterAndSort(currentRawPlayers, q, hideBots, sortMode);

        int totalNonBot = 0;
        for (int i = 0; i < currentRawPlayers.Count; i++)
            if (!currentRawPlayers[i].IsBot) totalNonBot++;

        if (lblCount != null)
        {
            lblCount.Text = hideBots
                ? (shown.Count + " / " + totalNonBot + " 人")
                : (shown.Count + " / " + currentRawPlayers.Count + " 人");
        }
        if (listView != null) listView.SetItems(shown, 0);
        UpdateBottomButtons();
    }

    void LoadServerPlayersAsync(string addr, bool forceRefresh)
    {
        if (string.IsNullOrEmpty(addr)) return;
        OnlinePlayerCache cached;
        if (!forceRefresh && cache.TryGetValue(addr, out cached)
            && (DateTime.Now - cached.FetchedAt).TotalSeconds < 15.0)
        {
            currentRawPlayers = cached.Players ?? new List<A2SPlayerInfo>();
            lblState.Text = cached.PingMs > 0 ? ("缓存 · " + cached.PingMs + "ms") : "已缓存";
            ApplyFilterAndSort();
            return;
        }

        int seq = ++querySeq;
        lblState.Text = "正在查询…";
        btnRefresh.Enabled = false;
        btnLocate.Enabled = false;

        Task.Run(delegate
        {
            string host;
            int port;
            bool ok = false;
            int ping = -1;
            List<A2SPlayerInfo> players = new List<A2SPlayerInfo>();
            if (MainForm.ParseHostPort(addr, out host, out port))
                ok = MainForm.A2SPlayersQuery(host, port, 1800, out players, out ping);

            if (IsDisposed) return;
            try
            {
                BeginInvoke(new Action(delegate
                {
                    if (IsDisposed) return;
                    btnRefresh.Enabled = true;
                    btnLocate.Enabled = true;
                    if (seq != querySeq) return;
                    if (!ok)
                    {
                        currentRawPlayers = new List<A2SPlayerInfo>();
                        lblState.Text = "连接超时或离线";
                        ApplyFilterAndSort();
                        return;
                    }
                    cache[addr] = new OnlinePlayerCache
                    {
                        Players = players,
                        PingMs = ping,
                        FetchedAt = DateTime.Now
                    };
                    currentRawPlayers = players;
                    lblState.Text = ping > 0 ? (ping + "ms · 刚刚") : "刚刚更新";
                    ApplyFilterAndSort();
                }));
            }
            catch { }
        });
    }

    void LocateMyServerAsync()
    {
        SaveMyNameFromInput();
        if (string.IsNullOrEmpty(myName))
        {
            lblState.Text = "请先填你的游戏昵称";
            if (txtMyName != null) txtMyName.Focus();
            return;
        }
        if (servers.Count == 0) return;

        int seq = ++querySeq;
        string target = myName;
        lblState.Text = "正在查找 (" + servers.Count + " 个服)…";
        btnRefresh.Enabled = false;
        btnLocate.Enabled = false;

        var snapshot = new List<OnlineServerEntry>(servers);
        Task.Run(delegate
        {
            int n = snapshot.Count;
            var results = new OnlinePlayerCache[n];
            using (var gate = new SemaphoreSlim(8, 8))
            using (var done = new CountdownEvent(n))
            {
                for (int i = 0; i < n; i++)
                {
                    int ix = i;
                    ThreadPool.QueueUserWorkItem(delegate
                    {
                        try
                        {
                            gate.Wait();
                            try
                            {
                                string host;
                                int port;
                                List<A2SPlayerInfo> plist;
                                int ping;
                                if (MainForm.ParseHostPort(snapshot[ix].Addr, out host, out port)
                                    && MainForm.A2SPlayersQuery(host, port, 1400, out plist, out ping))
                                {
                                    results[ix] = new OnlinePlayerCache
                                    {
                                        Players = plist,
                                        PingMs = ping,
                                        FetchedAt = DateTime.Now
                                    };
                                }
                            }
                            finally { try { gate.Release(); } catch { } }
                        }
                        catch { }
                        finally { try { done.Signal(); } catch { } }
                    });
                }
                done.Wait(4500);
            }

            if (IsDisposed) return;
            try
            {
                BeginInvoke(new Action(delegate
                {
                    if (IsDisposed) return;
                    btnRefresh.Enabled = true;
                    btnLocate.Enabled = true;
                    if (seq != querySeq) return;

                    int onlineResponded = 0;
                    int exactIdx = -1;
                    int fuzzyIdx = -1;

                    for (int i = 0; i < n; i++)
                    {
                        OnlinePlayerCache oc = results[i];
                        if (oc == null) continue;
                        onlineResponded++;
                        cache[snapshot[i].Addr] = oc;
                        for (int j = 0; j < oc.Players.Count; j++)
                        {
                            string pn = (oc.Players[j].Name ?? "").Trim();
                            if (string.Equals(pn, target, StringComparison.OrdinalIgnoreCase))
                            {
                                if (exactIdx < 0) exactIdx = i;
                            }
                            else if (pn.IndexOf(target, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                if (fuzzyIdx < 0) fuzzyIdx = i;
                            }
                        }
                    }

                    int foundIdx = exactIdx >= 0 ? exactIdx : fuzzyIdx;
                    if (foundIdx >= 0)
                    {
                        suppressServerChange = true;
                        cmbServer.SelectedIndex = foundIdx;
                        suppressServerChange = false;
                        currentAddr = snapshot[foundIdx].Addr;
                        NotifyPrefs();
                        currentRawPlayers = results[foundIdx].Players;
                        lblState.Text = "已找到所在服 · " + results[foundIdx].PingMs + "ms";
                        ApplyFilterAndSort();
                    }
                    else
                    {
                        lblState.Text = "查了 " + onlineResponded + " 个服，没找到“" + target + "”";
                    }
                }));
            }
            catch { }
        });
    }

    void PickSelectedPlayerAndClose()
    {
        A2SPlayerInfo p = listView != null ? listView.SelectedPlayer : null;
        if (p == null) return;
        if (onPickTarget != null) onPickTarget(p.Name);
        Close();
    }

    void CopySelectedPlayerRaw()
    {
        A2SPlayerInfo p = listView != null ? listView.SelectedPlayer : null;
        if (p == null) return;
        if (onCopyRaw != null) onCopyRaw(p.Name);
    }

    void ShowRowContextMenu(int idx, Point screenPt)
    {
        if (listView == null || idx < 0 || idx >= listView.Items.Count) return;
        A2SPlayerInfo p = listView.Items[idx];
        var cms = new ContextMenuStrip();
        cms.Items.Add("设为全局目标 (Enter)", null, delegate
        {
            if (onPickTarget != null) onPickTarget(p.Name);
            Close();
        });
        cms.Items.Add("复制原样昵称 (Ctrl+C)", null, delegate
        {
            if (onCopyRaw != null) onCopyRaw(p.Name);
        });
        cms.Items.Add("复制带引号昵称（控制台直接用）", null, delegate
        {
            if (onCopyQuoted != null) onCopyQuoted(p.Name);
        });
        cms.Items.Add(new ToolStripSeparator());
        cms.Items.Add("设为「我的昵称」（用于自动定位）", null, delegate
        {
            if (txtMyName != null) txtMyName.Text = p.Name;
            SaveMyNameFromInput();
        });
        cms.Closed += delegate
        {
            try { BeginInvoke(new Action(delegate { try { cms.Dispose(); } catch { } })); } catch { }
        };
        cms.Show(screenPt);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            if (txtSearch != null && txtSearch.Focused && !string.IsNullOrEmpty(txtSearch.Text))
            {
                txtSearch.Text = "";
                return true;
            }
            Close();
            return true;
        }
        if (keyData == (Keys.Control | Keys.C))
        {
            var tb = ActiveControl as TextBox;
            if (tb == null || tb.SelectionLength == 0)
            {
                CopySelectedPlayerRaw();
                return true;
            }
        }
        if (keyData == Keys.Enter)
        {
            if (ActiveControl != txtMyName && !(ActiveControl is Button) && !(ActiveControl is CheckBox)
                && listView != null && listView.SelectedPlayer != null)
            {
                PickSelectedPlayerAndClose();
                return true;
            }
        }
        if ((keyData == Keys.Up || keyData == Keys.Down || keyData == Keys.PageUp || keyData == Keys.PageDown)
            && ActiveControl != cmbServer && ActiveControl != cmbSort)
        {
            if (listView != null && listView.MoveSelectionByKey(keyData))
                return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (fReg != null) { fReg.Dispose(); fReg = null; }
            if (fBold != null) { fBold.Dispose(); fBold = null; }
            if (fSmall != null) { fSmall.Dispose(); fSmall = null; }
        }
        base.Dispose(disposing);
    }
}
#endif
