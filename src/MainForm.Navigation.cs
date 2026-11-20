#if ADMIN_EDITION
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text;
using System.Windows.Forms;

public partial class MainForm
{
    // ===== V1.9 管理版现代工作台壳层（左侧导航轨 + 全局上下文顶栏 + 交互底栏 + 命令面板） =====
    NavRail navRail;
    Panel adminRightHost;
    Panel headerBar;
    Panel pageHost;
    StatusPillBar statusPillBar;

    Label lblHeaderTitle;
    Label lblHeaderSub;
    TextBox txtGlobalTarget;
    Button btnTargetHistory;
    Button btnTargetClear;
    Panel btnGlobalSearch;

    Dictionary<string, Control> navPages = new Dictionary<string, Control>(StringComparer.Ordinal);
    string activeNavKey = "";
    Panel favPagePanel;
    WrapList favWrapList;
    RxSegmentedControl favSegMode;
    Label lblFavSummary;
    List<PaletteEntry> favViewEntries = new List<PaletteEntry>();

    // 各数据表页签的外部联动回调（同步全局目标 ID）
    Dictionary<int, Action> dataTargetSyncCallbacks = new Dictionary<int, Action>();

    // 全局目标玩家 & 历史记忆
    string globalTargetName = "";
    readonly List<string> recentTargets = new List<string>();
    bool suppressGlobalTargetEvent = false;

    // 收藏（pins.txt）与最近使用（单次/持久化）
    string pinsPath = "";
    readonly HashSet<string> pinnedKeys = new HashSet<string>(StringComparer.Ordinal);
    readonly List<string> recentPinKeys = new List<string>();
    readonly Dictionary<string, string> titlePyCache = new Dictionary<string, string>(StringComparer.Ordinal);
    readonly Dictionary<string, PinyinTitle> titlePinyinCache = new Dictionary<string, PinyinTitle>(StringComparer.Ordinal);
    bool navRailCollapsed = false;
    int paletteOpacityPct = CommandPaletteForm.DefaultOpacityPct;
    int paletteDockMode = CommandPaletteForm.DockCenter;
    IntPtr palettePrevFg = IntPtr.Zero;
    ToolTip tipGlobalSearch;
    SearchTriggerPill globalSearchPill;

    // 管理版三个工具一级页面容器
    Panel joinNavPage, crossNavPage, timerNavPage;
    Label lblToolPageTip;

    public static string MakePinKey(string sourceTab, string title)
    {
        return (sourceTab ?? "") + "\u0001" + (title ?? "");
    }

    string GetCachedPinyin(string title)
    {
        if (string.IsNullOrEmpty(title)) return "";
        string py;
        if (titlePyCache.TryGetValue(title, out py)) return py;
        py = PinyinHelper.GetInitials(title);
        titlePyCache[title] = py;
        return py;
    }

    PinyinTitle GetCachedPinyinTitle(string title)
    {
        if (string.IsNullOrEmpty(title)) return PinyinTitle.Empty;
        PinyinTitle pt;
        if (titlePinyinCache.TryGetValue(title, out pt)) return pt;
        pt = PinyinHelper.BuildTitle(title);
        titlePinyinCache[title] = pt;
        return pt;
    }

    void LoadPins()
    {
        pinnedKeys.Clear();
        try
        {
            if (string.IsNullOrEmpty(pinsPath) || !File.Exists(pinsPath)) return;
            foreach (string raw in File.ReadAllLines(pinsPath, Encoding.UTF8))
            {
                string line = (raw ?? "").Trim();
                if (line.Length == 0) continue;
                pinnedKeys.Add(line);
            }
        }
        catch (Exception ex) { Native.Log("pins load fail: " + ex.Message); }
    }

    void SavePins()
    {
        try
        {
            if (string.IsNullOrEmpty(pinsPath)) return;
            var arr = new List<string>(pinnedKeys);
            arr.Sort(StringComparer.Ordinal);
            AtomicWriteText(pinsPath, string.Join("\r\n", arr.ToArray()));
        }
        catch (Exception ex) { Native.Log("pins save fail: " + ex.Message); }
    }

    public bool IsPinnedKey(string pinKey)
    {
        return !string.IsNullOrEmpty(pinKey) && pinnedKeys.Contains(pinKey);
    }

    public bool TogglePinKey(string pinKey, string displayTitle)
    {
        if (string.IsNullOrEmpty(pinKey)) return false;
        bool nowPinned;
        if (pinnedKeys.Contains(pinKey))
        {
            pinnedKeys.Remove(pinKey);
            nowPinned = false;
            if (toast != null && !string.IsNullOrEmpty(displayTitle))
                toast.ShowText("已取消收藏：" + displayTitle);
        }
        else
        {
            pinnedKeys.Add(pinKey);
            nowPinned = true;
            if (toast != null && !string.IsNullOrEmpty(displayTitle))
                toast.ShowText("已收藏到「常用收藏」：" + displayTitle);
        }
        SavePins();
        UpdateNavBadges();
        RefreshFavoritesPage();
        return nowPinned;
    }

    void RecordRecentCopy(string sourceTab, string title)
    {
        if (string.IsNullOrEmpty(sourceTab) || string.IsNullOrEmpty(title)) return;
        string key = MakePinKey(sourceTab, title);
        recentPinKeys.Remove(key);
        recentPinKeys.Insert(0, key);
        if (recentPinKeys.Count > 15) recentPinKeys.RemoveRange(15, recentPinKeys.Count - 15);
        if (activeNavKey == "fav") RefreshFavoritesPage();
    }

    void SetGlobalTarget(string name, bool recordHistory)
    {
        string clean = (name ?? "").Trim();
        globalTargetName = clean;
        if (txtGlobalTarget != null && txtGlobalTarget.Text != clean)
        {
            suppressGlobalTargetEvent = true;
            txtGlobalTarget.Text = clean;
            suppressGlobalTargetEvent = false;
        }
        if (btnTargetClear != null) btnTargetClear.Visible = clean.Length > 0;
        if (recordHistory && clean.Length > 0)
        {
            recentTargets.RemoveAll(delegate(string s) { return string.Equals(s, clean, StringComparison.OrdinalIgnoreCase); });
            recentTargets.Insert(0, clean);
            if (recentTargets.Count > 8) recentTargets.RemoveRange(8, recentTargets.Count - 8);
            SaveSettings();
        }
        foreach (var kv in dataTargetSyncCallbacks)
        {
            try { if (kv.Value != null) kv.Value(); } catch { }
        }
        if (activeNavKey == "fav") RefreshFavoritesPage();
    }

    static NavIconKind IconForDataTab(string title)
    {
        string t = title ?? "";
        if (t.IndexOf("指令", StringComparison.Ordinal) >= 0) return NavIconKind.Terminal;
        if (t.IndexOf("技能", StringComparison.Ordinal) >= 0) return NavIconKind.Bolt;
        if (t.IndexOf("物品", StringComparison.Ordinal) >= 0) return NavIconKind.Box;
        if (t.IndexOf("模型", StringComparison.Ordinal) >= 0) return NavIconKind.Cube;
        if (t.IndexOf("SCP", StringComparison.OrdinalIgnoreCase) >= 0) return NavIconKind.Biohazard;
        if (t.IndexOf("角色", StringComparison.Ordinal) >= 0) return NavIconKind.User;
        return NavIconKind.Terminal;
    }

    int CountDataCommands(List<string> rows)
    {
        if (rows == null) return 0;
        int n = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            string t, b, a, ux, nt;
            DevSplitItem(rows[i] ?? "", out t, out b, out a, out ux, out nt);
            if (!string.IsNullOrEmpty(b)) n++;
        }
        return n;
    }

    void BuildAdminShell(string[] tabTitles, string[][] tabItems)
    {
        pinsPath = Path.Combine(dataRootDir, "pins.txt");
        LoadPins();

        // 底部交互状态胶囊栏
        statusPillBar = new StatusPillBar();
        statusPillBar.Dock = DockStyle.Bottom;
        statusPillBar.Pills.Add(new StatusPillBar.Pill
        {
            Key = "cross", Text = "准星：关闭", DotColor = ThemePalette.TextMuted,
            Tooltip = "单击直接开启/关闭外置屏幕准星",
            OnClick = delegate { ToggleCross(); }
        });
        statusPillBar.Pills.Add(new StatusPillBar.Pill
        {
            Key = "join", Text = "挤服：待命", DotColor = ThemePalette.TextMuted,
            Tooltip = "单击跳转到「挤服助手」",
            OnClick = delegate { SwitchNavPage("join"); }
        });
        statusPillBar.Pills.Add(new StatusPillBar.Pill
        {
            Key = "timer", Text = "倒计时：18:00", DotColor = ThemePalette.TextMuted,
            Tooltip = "单击跳转到「倒计时」",
            OnClick = delegate { SwitchNavPage("timer"); }
        });
        statusPillBar.Pills.Add(new StatusPillBar.Pill
        {
            Key = "hk", Text = "热键：正常", DotColor = ThemePalette.Success,
            Tooltip = "单击打开「热键设置」查看或修改快捷键",
            OnClick = delegate { ShowHotkeyDialog(); }
        });
        statusPillBar.Pills.Add(new StatusPillBar.Pill
        {
            Key = "theme", Text = "护眼浅色", DotColor = ThemePalette.Accent, RightAlign = true,
            Tooltip = "单击循环切换主题：护眼浅色 / 跟随系统 / 夜间深色",
            OnClick = delegate { themeMode = (themeMode + 1) % 3; ApplyTheme(); SaveSettings(); }
        });

        // 左侧可折叠导航轨
        navRail = new NavRail();
        navRail.Dock = DockStyle.Left;
        navRail.SelectionChanged += delegate(string key) { SwitchNavPage(key); };
        navRail.CollapseToggled += delegate
        {
            navRailCollapsed = navRail.Collapsed;
            SaveSettings();
            LayoutHeaderBar();
        };

        // 右侧主工作区
        adminRightHost = new Panel();
        adminRightHost.Dock = DockStyle.Fill;
        adminRightHost.BackColor = ThemePalette.Canvas;

        // 顶部全局上下文栏（页面标题 + 全局目标玩家 + 全局搜索 Alt+K）
        headerBar = new Panel();
        headerBar.Dock = DockStyle.Top;
        headerBar.Height = 50;
        headerBar.BackColor = ThemePalette.Surface;
        headerBar.Paint += delegate(object snd, PaintEventArgs pe)
        {
            using (var pen = new Pen(ThemePalette.BorderSubtle, 1f))
                pe.Graphics.DrawLine(pen, 0, headerBar.Height - 1, headerBar.Width, headerBar.Height - 1);
        };

        lblHeaderTitle = new Label();
        lblHeaderTitle.Font = TrackFont("Microsoft YaHei UI", 11.5f, FontStyle.Bold);
        lblHeaderTitle.ForeColor = ThemePalette.TextPrimary;
        lblHeaderTitle.BackColor = Color.Transparent;
        lblHeaderTitle.AutoEllipsis = true;
        lblHeaderTitle.UseMnemonic = false;
        lblHeaderTitle.SetBounds(16, 6, 170, 22);
        headerBar.Controls.Add(lblHeaderTitle);

        lblHeaderSub = new Label();
        lblHeaderSub.Font = TrackFont("Microsoft YaHei UI", 8.2f, FontStyle.Regular);
        lblHeaderSub.ForeColor = ThemePalette.TextMuted;
        lblHeaderSub.BackColor = Color.Transparent;
        lblHeaderSub.AutoEllipsis = true;
        lblHeaderSub.UseMnemonic = false;
        lblHeaderSub.SetBounds(16, 28, 220, 16);
        headerBar.Controls.Add(lblHeaderSub);

        var lblTgtPrefix = new Label();
        lblTgtPrefix.Name = "lblTgtPrefix";
        lblTgtPrefix.Text = "目标玩家：";
        lblTgtPrefix.Font = TrackFont("Microsoft YaHei UI", 8.8f, FontStyle.Regular);
        lblTgtPrefix.ForeColor = ThemePalette.TextSecondary;
        lblTgtPrefix.BackColor = Color.Transparent;
        lblTgtPrefix.TextAlign = ContentAlignment.MiddleRight;
        lblTgtPrefix.SetBounds(224, 13, 82, 24);
        headerBar.Controls.Add(lblTgtPrefix);

        txtGlobalTarget = new TextBox();
        txtGlobalTarget.Font = TrackFont("Microsoft YaHei UI", 9.2f, FontStyle.Regular);
        txtGlobalTarget.SetBounds(308, 12, 152, 25);
        txtGlobalTarget.HandleCreated += delegate
        {
            try { Native.SetCueBanner(txtGlobalTarget.Handle, "留空 = ^ (自己)"); } catch { }
        };
        txtGlobalTarget.TextChanged += delegate
        {
            if (suppressGlobalTargetEvent) return;
            SetGlobalTarget(txtGlobalTarget.Text, false);
        };
        txtGlobalTarget.Leave += delegate
        {
            if (!string.IsNullOrWhiteSpace(txtGlobalTarget.Text))
                SetGlobalTarget(txtGlobalTarget.Text, true);
        };
        txtGlobalTarget.KeyDown += delegate(object snd, KeyEventArgs ke)
        {
            if (ke.KeyCode == Keys.Enter)
            {
                SetGlobalTarget(txtGlobalTarget.Text, true);
                ke.Handled = true;
                ke.SuppressKeyPress = true;
                toast.ShowText(string.IsNullOrEmpty(globalTargetName) ? "目标已重置为 ^（自己）" : ("全局目标已设为：" + globalTargetName));
            }
            else if (ke.KeyCode == Keys.Escape)
            {
                SetGlobalTarget("", false);
                ke.Handled = true;
                ke.SuppressKeyPress = true;
            }
        };
        headerBar.Controls.Add(txtGlobalTarget);

        btnTargetClear = new Button();
        btnTargetClear.Text = "×";
        btnTargetClear.Font = TrackFont("Microsoft YaHei UI", 9f, FontStyle.Bold);
        btnTargetClear.FlatStyle = FlatStyle.Flat;
        btnTargetClear.Cursor = Cursors.Hand;
        btnTargetClear.Visible = false;
        btnTargetClear.SetBounds(462, 12, 24, 25);
        btnTargetClear.Click += delegate { SetGlobalTarget("", false); txtGlobalTarget.Focus(); };
        var tipClr = new ToolTip(uiComponents);
        tipClr.SetToolTip(btnTargetClear, "清空目标，恢复对 ^（自己）生效");
        headerBar.Controls.Add(btnTargetClear);

        btnTargetHistory = new Button();
        btnTargetHistory.Text = "▾";
        btnTargetHistory.Font = TrackFont("Microsoft YaHei UI", 8.5f, FontStyle.Regular);
        btnTargetHistory.FlatStyle = FlatStyle.Flat;
        btnTargetHistory.Cursor = Cursors.Hand;
        btnTargetHistory.SetBounds(488, 12, 26, 25);
        btnTargetHistory.Click += delegate { ShowTargetHistoryMenu(); };
        var tipHist = new ToolTip(uiComponents);
        tipHist.SetToolTip(btnTargetHistory, "选择最近使用过的目标玩家名（跨 6 张表实时同步）");
        headerBar.Controls.Add(btnTargetHistory);

        // 右侧全局搜索触发胶囊（动态联动全局热键）
        globalSearchPill = new SearchTriggerPill();
        btnGlobalSearch = globalSearchPill;
        btnGlobalSearch.SetBounds(540, 10, 236, 30);
        btnGlobalSearch.Click += delegate { OpenCommandPalette(false); };
        tipGlobalSearch = new ToolTip(uiComponents);
        headerBar.Controls.Add(btnGlobalSearch);

        headerBar.Resize += delegate { LayoutHeaderBar(); };

        pageHost = new Panel();
        pageHost.Dock = DockStyle.Fill;
        pageHost.BackColor = ThemePalette.Surface;

        adminRightHost.Controls.Add(pageHost);
        adminRightHost.Controls.Add(headerBar);

        Controls.Add(adminRightHost);
        Controls.Add(navRail);
        Controls.Add(statusPillBar);

        // 构建全部页面并注册到导航轨
        favPagePanel = BuildFavoritesPage();
        RegisterNavPage("fav", "常用收藏", "指令库", NavIconKind.Star, pinnedKeys.Count, favPagePanel);

        if (tabTitles != null)
        {
            dataTabLabels = new string[tabTitles.Length];
            dataTabLists = new List<string>[tabTitles.Length];
            dataTabBase = new string[tabTitles.Length][];
            var overrides = LoadDataOverrides();

            for (int i = 0; i < tabTitles.Length; i++)
            {
                string[] items = (tabItems != null && i < tabItems.Length) ? tabItems[i] : null;
                dataTabBase[i] = items ?? new string[0];
                var list = new List<string>();
                if (items != null) list.AddRange(items);
                List<string> ov;
                if (overrides.TryGetValue(tabTitles[i], out ov)) list = ov;

                dataTabLabels[i] = tabTitles[i];
                dataTabLists[i] = list;
                Panel tp = BuildTextPage(tabTitles[i], list.ToArray(), i);
                RegisterNavPage("data:" + i, tabTitles[i], "指令库", IconForDataTab(tabTitles[i]), CountDataCommands(list), tp);
            }
        }

        customPage = BuildCustomPage();
        RegisterNavPage("custom", "自定义文案", "实战工具", NavIconKind.Bookmark, cTitles.Length, customPage);

        BuildAdminToolNavPages();

        devPage = BuildDevPage();
        RegisterNavPage("dev", "开发者工具", "BOTTOM", NavIconKind.Wrench, 0, devPage);
        NavItem devNav = navRail.FindItem("dev");
        if (devNav != null) devNav.Visible = devToolsOn;

        aboutPage = BuildAboutPage();
        RegisterNavPage("about", "关于与更新", "BOTTOM", NavIconKind.Info, 0, aboutPage);

        LayoutHeaderBar();
        UpdateGlobalSearchShortcut();
        SwitchNavPage(dataTabLabels.Length > 0 ? "data:0" : "fav");
    }

    void UpdateGlobalSearchShortcut()
    {
        try
        {
            string hk = KeyName(9);
            if (string.IsNullOrEmpty(hk) || hk == "?") hk = "Alt+K";
            if (globalSearchPill != null && !globalSearchPill.IsDisposed)
            {
                globalSearchPill.ShortcutText = hk;
                globalSearchPill.Invalidate();
            }
            if (tipGlobalSearch != null && btnGlobalSearch != null && !btnGlobalSearch.IsDisposed)
            {
                tipGlobalSearch.SetToolTip(btnGlobalSearch,
                    "全局命令面板：任意窗口/工具箱内按 " + hk + " 呼出或收起（可在「热键」中修改）");
            }
        }
        catch { }
    }

    class SearchTriggerPill : Panel
    {
        bool hovered = false;
        Font fHint, fKbd;
        public string ShortcutText = "Alt+K";

        public SearchTriggerPill()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            fHint = new Font("Microsoft YaHei UI", 8.6f, FontStyle.Regular);
            fKbd = new Font("Consolas", 8f, FontStyle.Bold);
        }

        protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovered = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            int w = ClientSize.Width, h = ClientSize.Height;
            g.Clear(ThemePalette.Surface);
            Rectangle rc = new Rectangle(0, 0, w - 1, h - 1);
            using (var bg = new SolidBrush(hovered ? ThemePalette.Hover : ThemePalette.SurfaceAlt))
                Native.FillRoundedRect(g, bg, rc, 7);
            using (var bp = new Pen(hovered ? ThemePalette.Accent : ThemePalette.Border, 1f))
                Native.DrawRoundedRect(g, bp, rc, 7);

            UiIcons.Draw(g, NavIconKind.Search, new Rectangle(9, (h - 14) / 2, 14, 14),
                hovered ? ThemePalette.Accent : ThemePalette.TextMuted, false);

            string sc = string.IsNullOrEmpty(ShortcutText) ? "Alt+K" : ShortcutText;
            Size ksz = TextRenderer.MeasureText(g, sc, fKbd);
            int kbdW = Math.Max(46, Math.Min(96, ksz.Width + 12));
            Rectangle kbd = new Rectangle(w - kbdW - 8, (h - 18) / 2, kbdW, 18);
            using (var kb = new SolidBrush(ThemePalette.Surface))
                Native.FillRoundedRect(g, kb, kbd, 4);
            using (var kp = new Pen(ThemePalette.BorderSubtle, 1f))
                Native.DrawRoundedRect(g, kp, kbd, 4);
            TextRenderer.DrawText(g, sc, fKbd, kbd, ThemePalette.TextMuted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);

            TextRenderer.DrawText(g, "呼出全局搜索弹窗", fHint,
                new Rectangle(28, 0, Math.Max(40, kbd.Left - 32), h),
                hovered ? ThemePalette.TextPrimary : ThemePalette.TextMuted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (fHint != null) { fHint.Dispose(); fHint = null; }
                if (fKbd != null) { fKbd.Dispose(); fKbd = null; }
            }
            base.Dispose(disposing);
        }
    }

    void RegisterNavPage(string key, string title, string group, NavIconKind icon, int count, Control pageCtrl)
    {
        if (pageCtrl == null) return;
        pageCtrl.Dock = DockStyle.Fill;
        pageCtrl.Visible = false;
        navPages[key] = pageCtrl;
        if (pageHost != null && pageCtrl.Parent != pageHost)
            pageHost.Controls.Add(pageCtrl);

        NavItem existing = navRail.FindItem(key);
        if (existing == null)
        {
            navRail.Items.Add(new NavItem
            {
                Key = key,
                Text = title,
                Group = group,
                Icon = icon,
                BadgeCount = count
            });
        }
        else
        {
            existing.Text = title;
            existing.BadgeCount = count;
        }
        navRail.Invalidate();
    }

    void BuildAdminToolNavPages()
    {
        joinNavPage = new Panel();
        joinNavPage.BackColor = ThemePalette.Surface;
        AddAdminToolDivider(joinNavPage);
        CreateJoinPanel(joinNavPage, 50);

        crossNavPage = new Panel();
        crossNavPage.BackColor = ThemePalette.Surface;
        AddAdminToolDivider(crossNavPage);
        CreateCrossPanel(crossNavPage, 50);

        timerNavPage = new Panel();
        timerNavPage.BackColor = ThemePalette.Surface;
        AddAdminToolDivider(timerNavPage);
        CreateTimerPanel(timerNavPage, 50);

        lblToolPageTip = new Label();
        lblToolPageTip.Font = TrackFont("Microsoft YaHei UI", 9f, FontStyle.Regular);
        lblToolPageTip.ForeColor = ThemePalette.TextSecondary;
        lblToolPageTip.AutoEllipsis = true;
        lblToolPageTip.UseMnemonic = false;
        lblToolPageTip.SetBounds(12, 15, 360, 22);
        joinNavPage.Controls.Add(lblToolPageTip);
        CreateToolUtilityButtons(joinNavPage);

        joinNavPage.Resize += delegate { LayoutToolBar(); };
        crossNavPage.Resize += delegate { LayoutToolBar(); };
        timerNavPage.Resize += delegate { LayoutToolBar(); };

        RegisterNavPage("join", "挤服助手", "实战工具", NavIconKind.Server, 0, joinNavPage);
        RegisterNavPage("cross", "屏幕准星", "实战工具", NavIconKind.Crosshair, 0, crossNavPage);
        RegisterNavPage("timer", "倒计时", "实战工具", NavIconKind.Timer, 0, timerNavPage);

        LoadFavs();
        FillSrvList();
        ApplyModeUi();
        UpdateHotkeyLabels();
    }

    void AddAdminToolDivider(Panel host)
    {
        var div = new Panel();
        div.SetBounds(8, 46, Math.Max(100, host.ClientSize.Width - 16), 1);
        div.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        div.BackColor = Color.FromArgb(226, 232, 240);
        host.Controls.Add(div);
    }

    void MountSharedToolStrip(Panel targetPage)
    {
        if (targetPage == null || btnHotkey == null) return;
        if (lblToolPageTip != null && lblToolPageTip.Parent != targetPage) targetPage.Controls.Add(lblToolPageTip);
        if (btnHotkeyOff != null && btnHotkeyOff.Parent != targetPage) targetPage.Controls.Add(btnHotkeyOff);
        if (bLog != null && bLog.Parent != targetPage) targetPage.Controls.Add(bLog);
        if (btnHotkey != null && btnHotkey.Parent != targetPage) targetPage.Controls.Add(btnHotkey);

        if (lblToolPageTip != null)
        {
            if (targetPage == joinNavPage)
                lblToolPageTip.Text = "A2S 状态探测（含地图/延迟/容量条）与自动挤服 · OCR 识别进服自动停";
            else if (targetPage == crossNavPage)
                lblToolPageTip.Text = "32-bit 抗锯齿透明准星 · 支持黑色外描边、不透明度与无损 PNG Alpha";
            else if (targetPage == timerNavPage)
                lblToolPageTip.Text = "32-bit 高清描边倒计时 · 可选胶囊背板、可视化时间轴与平滑脉冲";
        }
        LayoutToolBar();
    }

    void SwitchNavPage(string key)
    {
        if (string.IsNullOrEmpty(key) || !navPages.ContainsKey(key)) return;
        activeNavKey = key;
        if (navRail != null) navRail.SelectKey(key, false);

        foreach (var kv in navPages)
            kv.Value.Visible = (kv.Key == key);

        Control cur = navPages[key];
        cur.BringToFront();

        if (key == "fav") RefreshFavoritesPage();
        else if (key == "join") { MountSharedToolStrip(joinNavPage); LayoutJoin(); }
        else if (key == "cross") { MountSharedToolStrip(crossNavPage); if (crossPreview != null) crossPreview.Invalidate(); }
        else if (key == "timer") { MountSharedToolStrip(timerNavPage); LayoutTimer(); }

        UpdateHeaderBar();
    }

    void UpdateHeaderBar()
    {
        if (lblHeaderTitle == null || lblHeaderSub == null) return;
        NavItem it = (navRail != null) ? navRail.FindItem(activeNavKey) : null;
        string title = (it != null) ? it.Text : "RX 工具箱";
        lblHeaderTitle.Text = title;

        bool showTarget = (activeNavKey == "fav" || activeNavKey == "custom" || activeNavKey.StartsWith("data:", StringComparison.Ordinal));
        Control pref = (headerBar != null) ? headerBar.Controls["lblTgtPrefix"] : null;
        if (pref != null) pref.Visible = showTarget;
        if (txtGlobalTarget != null) txtGlobalTarget.Visible = showTarget;
        if (btnTargetHistory != null) btnTargetHistory.Visible = showTarget;
        if (btnTargetClear != null) btnTargetClear.Visible = showTarget && !string.IsNullOrEmpty(globalTargetName);

        if (activeNavKey == "fav")
            lblHeaderSub.Text = "星标收藏 " + pinnedKeys.Count + " 项 · 最近复制 " + recentPinKeys.Count + " 项";
        else if (activeNavKey.StartsWith("data:", StringComparison.Ordinal))
            lblHeaderSub.Text = (it != null ? ("共 " + it.BadgeCount + " 条指令 · 单击即复制，左星标收藏") : "单击即复制");
        else if (activeNavKey == "custom")
            lblHeaderSub.Text = "共 " + cTitles.Length + " 张卡片 · 支持 {target} 动态目标与多行串联";
        else if (activeNavKey == "join")
            lblHeaderSub.Text = "已收藏 " + favList.Length + " 个服务器 · 支持按空位/延迟排序";
        else if (activeNavKey == "cross")
            lblHeaderSub.Text = "当前样式：" + Overlay.Styles[overlay.StyleIndex] + " · " + Overlay.ColorNames[overlay.ColorIndex];
        else if (activeNavKey == "timer")
            lblHeaderSub.Text = timerRunning ? (timerPaused ? "倒计时已暂停" : "倒计时运行中") : "预设方案与可视化时间链";
        else if (activeNavKey == "dev")
            lblHeaderSub.Text = "数据快照导入导出与在线版本库协同";
        else if (activeNavKey == "about")
            lblHeaderSub.Text = APP_VERSION + " · 构建 " + APP_BUILD_DATE + " · 单文件绿色版";
        LayoutHeaderBar();
    }

    void LayoutHeaderBar()
    {
        if (headerBar == null) return;
        int w = headerBar.ClientSize.Width;
        if (w < 300) return;

        int searchW = Math.Max(180, Math.Min(250, w - 530));
        if (btnGlobalSearch != null)
            btnGlobalSearch.SetBounds(w - searchW - 14, 10, searchW, 30);

        int rightEdge = (btnGlobalSearch != null) ? (btnGlobalSearch.Left - 14) : (w - 14);
        if (btnTargetHistory != null) btnTargetHistory.SetBounds(rightEdge - 26, 12, 26, 25);
        bool hasClr = !string.IsNullOrEmpty(globalTargetName);
        int clrSpace = hasClr ? 26 : 0;
        if (btnTargetClear != null) btnTargetClear.SetBounds(rightEdge - 26 - 26, 12, 24, 25);
        int boxW = Math.Max(110, Math.Min(164, rightEdge - 26 - clrSpace - 286));
        if (txtGlobalTarget != null) txtGlobalTarget.SetBounds(rightEdge - 26 - clrSpace - boxW - 2, 12, boxW, 25);
        Control pref = headerBar.Controls["lblTgtPrefix"];
        if (pref != null && txtGlobalTarget != null) pref.SetBounds(txtGlobalTarget.Left - 84, 13, 82, 24);

        bool showTarget = (activeNavKey == "fav" || activeNavKey == "custom" || activeNavKey.StartsWith("data:", StringComparison.Ordinal));
        int titleMax = (showTarget && pref != null) ? Math.Max(120, pref.Left - 24) : Math.Max(160, rightEdge - 24);
        if (lblHeaderTitle != null) lblHeaderTitle.Width = titleMax;
        if (lblHeaderSub != null) lblHeaderSub.Width = titleMax;
    }

    void UpdateNavBadges()
    {
        if (navRail == null) return;
        NavItem favIt = navRail.FindItem("fav");
        if (favIt != null) favIt.BadgeCount = pinnedKeys.Count;
        for (int i = 0; i < dataTabLists.Length; i++)
        {
            NavItem dit = navRail.FindItem("data:" + i);
            if (dit != null) dit.BadgeCount = CountDataCommands(dataTabLists[i]);
        }
        NavItem custIt = navRail.FindItem("custom");
        if (custIt != null) custIt.BadgeCount = cTitles.Length;
        navRail.Invalidate();
        UpdateHeaderBar();
    }

    void ShowTargetHistoryMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Closed += delegate { SafeBeginInvoke(delegate { menu.Dispose(); }); };

        var miSelf = new ToolStripMenuItem("🎯 自己 (^)");
        miSelf.Click += delegate { SetGlobalTarget("", false); };
        menu.Items.Add(miSelf);

        if (recentTargets.Count > 0)
        {
            menu.Items.Add(new ToolStripSeparator());
            for (int i = 0; i < recentTargets.Count; i++)
            {
                string name = recentTargets[i];
                var mi = new ToolStripMenuItem(name);
                mi.Click += delegate { SetGlobalTarget(name, true); };
                menu.Items.Add(mi);
            }
            menu.Items.Add(new ToolStripSeparator());
            var miClr = new ToolStripMenuItem("清空最近目标历史");
            miClr.Click += delegate { recentTargets.Clear(); SaveSettings(); toast.ShowText("已清空目标历史"); };
            menu.Items.Add(miClr);
        }
        else
        {
            menu.Items.Add(new ToolStripSeparator());
            var miEmpty = new ToolStripMenuItem("（暂无历史目标，输入玩家名按回车自动记住）");
            miEmpty.Enabled = false;
            menu.Items.Add(miEmpty);
        }
        menu.Show(btnTargetHistory, new Point(0, btnTargetHistory.Height + 2));
    }

    // ===== 常用收藏 & 最近使用 聚合视图 =====
    Panel BuildFavoritesPage()
    {
        var page = new Panel();
        page.BackColor = ThemePalette.Surface;

        var bar = new Panel();
        bar.Dock = DockStyle.Top;
        bar.Height = 46;
        bar.BackColor = ThemePalette.SurfaceAlt;
        bar.Paint += delegate(object snd, PaintEventArgs pe)
        {
            using (var pen = new Pen(ThemePalette.BorderSubtle, 1f))
                pe.Graphics.DrawLine(pen, 0, bar.Height - 1, bar.Width, bar.Height - 1);
        };

        favSegMode = new RxSegmentedControl();
        favSegMode.Items = new string[] { "主指令", "控制台 ulx" };
        favSegMode.SetBounds(12, 8, 180, 30);
        favSegMode.SelectedIndexChanged += delegate { RefreshFavoritesPage(); };
        bar.Controls.Add(favSegMode);

        lblFavSummary = new Label();
        lblFavSummary.Text = "点任意指令左侧 ★ 收藏后会自动汇聚于此";
        lblFavSummary.ForeColor = ThemePalette.TextSecondary;
        lblFavSummary.BackColor = Color.Transparent;
        lblFavSummary.AutoEllipsis = true;
        lblFavSummary.SetBounds(206, 13, 380, 22);
        bar.Controls.Add(lblFavSummary);

        var btnClearRec = new Button();
        btnClearRec.Text = "清空最近记录";
        btnClearRec.FlatStyle = FlatStyle.Flat;
        btnClearRec.SetBounds(600, 9, 108, 28);
        btnClearRec.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnClearRec.Click += delegate
        {
            recentPinKeys.Clear();
            RefreshFavoritesPage();
            toast.ShowText("已清空最近复制记录");
        };
        bar.Controls.Add(btnClearRec);
        bar.Resize += delegate
        {
            btnClearRec.Left = Math.Max(340, bar.ClientSize.Width - btnClearRec.Width - 12);
            lblFavSummary.Width = Math.Max(120, btnClearRec.Left - lblFavSummary.Left - 12);
        };

        favWrapList = new WrapList();
        favWrapList.Dock = DockStyle.Fill;
        favWrapList.Font = TrackFont("Microsoft YaHei UI", 9.5f, FontStyle.Regular);
        favWrapList.EmptyText = "还没有收藏或最近使用的指令。\r\n点任意数据表条目前面的 ★ 即可加入常用收藏，或按 Alt+K 快速搜索。";
        favWrapList.IsRowPinned = delegate(int idx)
        {
            if (idx < 0 || idx >= favViewEntries.Count || favViewEntries[idx] == null) return false;
            return IsPinnedKey(favViewEntries[idx].PinKey);
        };
        favWrapList.ItemPinToggled += delegate(int idx)
        {
            if (idx < 0 || idx >= favViewEntries.Count || favViewEntries[idx] == null) return;
            PaletteEntry pe = favViewEntries[idx];
            TogglePinKey(pe.PinKey, pe.Title);
        };
        favWrapList.ItemActivated += delegate
        {
            int k = favWrapList.Sel;
            if (k < 0 || k >= favViewEntries.Count || favViewEntries[k] == null) return;
            PaletteEntry pe = favViewEntries[k];
            bool ulx = (favSegMode != null && favSegMode.SelectedIndex == 1);
            string cmd = ResolvePaletteCommand(pe, ulx);
            CopyToClipboard(pe.Title, cmd);
            RecordRecentCopy(pe.SourceTab, pe.Title);
        };

        page.Controls.Add(favWrapList);
        page.Controls.Add(bar);
        return page;
    }

    List<PaletteEntry> BuildAllPaletteEntries()
    {
        var list = new List<PaletteEntry>(1800);
        var recentIdx = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < recentPinKeys.Count; i++)
            if (!recentIdx.ContainsKey(recentPinKeys[i])) recentIdx[recentPinKeys[i]] = i;

        for (int ti = 0; ti < dataTabLabels.Length; ti++)
        {
            string tabName = dataTabLabels[ti];
            List<string> rows = dataTabLists[ti];
            if (rows == null) continue;
            for (int r = 0; r < rows.Count; r++)
            {
                string t, b, a, ux, nt;
                DevSplitItem(rows[r] ?? "", out t, out b, out a, out ux, out nt);
                if (string.IsNullOrEmpty(b)) continue;
                string pk = MakePinKey(tabName, t);
                int rOrd;
                if (!recentIdx.TryGetValue(pk, out rOrd)) rOrd = -1;
                list.Add(new PaletteEntry
                {
                    PinKey = pk,
                    SourceTab = tabName,
                    Title = t,
                    Body = b,
                    Alt = a,
                    Ulx = ux,
                    Note = nt,
                    TitlePy = GetCachedPinyin(t),
                    TitlePinyin = GetCachedPinyinTitle(t),
                    IsCustom = false,
                    IsPinned = pinnedKeys.Contains(pk),
                    RecentOrder = rOrd
                });
            }
        }

        for (int ci = 0; ci < cTitles.Length; ci++)
        {
            string ct = cTitles[ci] ?? "";
            string cx = (ci < cTexts.Length) ? (cTexts[ci] ?? "") : "";
            if (ct.Length == 0) continue;
            string pk = MakePinKey("自定义", ct);
            int rOrd;
            if (!recentIdx.TryGetValue(pk, out rOrd)) rOrd = -1;
            list.Add(new PaletteEntry
            {
                PinKey = pk,
                SourceTab = "自定义",
                Title = ct,
                Body = cx,
                Alt = "",
                Ulx = "",
                Note = "",
                TitlePy = GetCachedPinyin(ct),
                TitlePinyin = GetCachedPinyinTitle(ct),
                IsCustom = true,
                IsPinned = pinnedKeys.Contains(pk),
                RecentOrder = rOrd
            });
        }
        return list;
    }

    string ResolvePaletteCommand(PaletteEntry entry, bool ulxMode)
    {
        if (entry == null) return "";
        if (entry.IsCustom) return ApplyCustomTarget(entry.Body, globalTargetName);
        string raw = entry.Body;
        if (ulxMode)
        {
            bool ulxAuto = (entry.SourceTab == null || entry.SourceTab.IndexOf("模型", StringComparison.Ordinal) < 0);
            if (!string.IsNullOrEmpty(entry.Ulx)) raw = entry.Ulx;
            else if (ulxAuto && raw.Length > 1 && raw[0] == '!')
                raw = "ulx " + UlxName(raw.Substring(1));
        }
        return ApplyTargetName(raw, globalTargetName);
    }

    public static string ApplyCustomTarget(string body, string targetName)
    {
        if (string.IsNullOrEmpty(body)) return "";
        string idTxt = (targetName ?? "").Trim();
        string rep = idTxt.Length == 0 ? "^" : ((idTxt.IndexOf(' ') >= 0 && !(idTxt.StartsWith("\"") && idTxt.EndsWith("\""))) ? ("\"" + idTxt + "\"") : idTxt);
        string res = body.Replace("{target}", rep);
        if (idTxt.Length > 0 && res.IndexOf('^') >= 0) res = ApplyTargetName(res, idTxt);
        return res;
    }

    void RefreshFavoritesPage()
    {
        if (favWrapList == null) return;
        List<PaletteEntry> all = BuildAllPaletteEntries();
        var pinnedList = new List<PaletteEntry>();
        var recentList = new List<PaletteEntry>();
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i].IsPinned) pinnedList.Add(all[i]);
            else if (all[i].RecentOrder >= 0) recentList.Add(all[i]);
        }
        recentList.Sort(delegate(PaletteEntry a, PaletteEntry b) { return a.RecentOrder.CompareTo(b.RecentOrder); });

        bool ulx = (favSegMode != null && favSegMode.SelectedIndex == 1);
        var labels = new List<string>();
        var values = new List<string>();
        var heads = new List<bool>();
        var notes = new List<string>();
        favViewEntries.Clear();

        if (pinnedList.Count > 0)
        {
            labels.Add("★ 我的常用收藏（" + pinnedList.Count + " 项）");
            values.Add(""); heads.Add(true); notes.Add(""); favViewEntries.Add(null);
            for (int i = 0; i < pinnedList.Count; i++)
            {
                PaletteEntry e = pinnedList[i];
                labels.Add("[" + e.SourceTab.Replace("表", "") + "] " + e.Title);
                values.Add(ResolvePaletteCommand(e, ulx));
                heads.Add(false);
                notes.Add(e.Note);
                favViewEntries.Add(e);
            }
        }
        if (recentList.Count > 0)
        {
            labels.Add("最近复制（" + recentList.Count + " 项）");
            values.Add(""); heads.Add(true); notes.Add(""); favViewEntries.Add(null);
            for (int i = 0; i < recentList.Count; i++)
            {
                PaletteEntry e = recentList[i];
                labels.Add("[" + e.SourceTab.Replace("表", "") + "] " + e.Title);
                values.Add(ResolvePaletteCommand(e, ulx));
                heads.Add(false);
                notes.Add(e.Note);
                favViewEntries.Add(e);
            }
        }

        favWrapList.DefaultCollapsed = false;
        favWrapList.SetData(labels.ToArray(), values.ToArray(), heads.ToArray(), notes.ToArray());
        if (lblFavSummary != null)
            lblFavSummary.Text = "收藏 " + pinnedList.Count + " 项 · 最近 " + recentList.Count + " 项（联动顶部目标玩家，单击即复制）";
    }

    CommandPaletteForm activePalette;

    void OpenCommandPalette(bool fromGlobalHotkey)
    {
        try
        {
            if (activePalette != null && !activePalette.IsDisposed)
            {
                if (fromGlobalHotkey)
                {
                    IntPtr prev = palettePrevFg;
                    try { activePalette.Close(); } catch { }
                    if (prev != IntPtr.Zero && prev != Handle)
                    {
                        try { Native.SetForegroundWindow(prev); } catch { }
                    }
                }
                else
                {
                    activePalette.Activate();
                }
                return;
            }
            IntPtr prevFg = fromGlobalHotkey ? Native.GetForegroundWindow() : IntPtr.Zero;
            palettePrevFg = prevFg;
            List<PaletteEntry> all = BuildAllPaletteEntries();
            activePalette = new CommandPaletteForm(
                all,
                globalTargetName,
                delegate(PaletteEntry entry, bool ulx) { return ResolvePaletteCommand(entry, ulx); },
                delegate(PaletteEntry entry, bool ulx)
                {
                    string cmd = ResolvePaletteCommand(entry, ulx);
                    CopyToClipboard(entry.Title, cmd);
                    RecordRecentCopy(entry.SourceTab, entry.Title);
                    if (fromGlobalHotkey && prevFg != IntPtr.Zero && prevFg != Handle)
                    {
                        try { Native.SetForegroundWindow(prevFg); } catch { }
                    }
                },
                delegate(string pk)
                {
                    return TogglePinKey(pk, "");
                },
                paletteOpacityPct,
                delegate(int pct)
                {
                    paletteOpacityPct = pct;
                    SaveSettings();
                },
                paletteDockMode,
                delegate(int dm)
                {
                    paletteDockMode = dm;
                    SaveSettings();
                },
                fromGlobalHotkey);
            IntPtr palHandle = IntPtr.Zero;
            activePalette.HandleCreated += delegate { if (activePalette != null) palHandle = activePalette.Handle; };
            activePalette.FormClosed += delegate
            {
                activePalette = null;
                if (fromGlobalHotkey && prevFg != IntPtr.Zero && prevFg != Handle)
                {
                    IntPtr curFg = Native.GetForegroundWindow();
                    if (curFg == IntPtr.Zero || curFg == Handle || curFg == palHandle)
                    {
                        try { Native.SetForegroundWindow(prevFg); } catch { }
                    }
                }
            };
            activePalette.Show(fromGlobalHotkey ? null : this);
        }
        catch (Exception ex) { Native.Log("palette EX: " + ex.Message); }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Alt | Keys.K))
        {
            OpenCommandPalette(false);
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    void UpdateAdminStatusPills()
    {
        if (statusPillBar == null) return;
        var pCross = statusPillBar.Find("cross");
        if (pCross != null)
        {
            pCross.Text = overlay.Visible ? ("准星：" + Overlay.Styles[overlay.StyleIndex]) : "准星：关闭";
            pCross.DotColor = overlay.Visible ? ThemePalette.Success : ThemePalette.TextMuted;
        }
        var pJoin = statusPillBar.Find("join");
        if (pJoin != null)
        {
            if (autoOn)
            {
                pJoin.Text = typingOn ? "挤服：粘贴连接中" : "挤服：回车连点中";
                pJoin.DotColor = ThemePalette.Danger;
            }
            else
            {
                int m = JoinModeIdx;
                string mn = (m == 0) ? "手动档" : (m == 2 ? "收藏轮询" : "单一IP");
                pJoin.Text = "挤服：" + mn + " · " + IntervalMs + "ms";
                pJoin.DotColor = ThemePalette.TextMuted;
            }
        }
        var pTimer = statusPillBar.Find("timer");
        if (pTimer != null)
        {
            string mm = CountdownOverlay.FormatMmSs(timerRemainSec);
            pTimer.Text = timerRunning ? (timerPaused ? ("倒计时：" + mm + " (暂停)") : ("倒计时：" + mm)) : ("倒计时：" + mm);
            pTimer.DotColor = timerRunning ? (timerPaused ? ThemePalette.Warning : ThemePalette.Accent) : ThemePalette.TextMuted;
        }
        var pHk = statusPillBar.Find("hk");
        if (pHk != null)
        {
            if (hotkeysOff)
            {
                pHk.Text = "热键：已关闭";
                pHk.DotColor = ThemePalette.Warning;
            }
            else if (HotkeysOk())
            {
                pHk.Text = "热键：正常";
                pHk.DotColor = ThemePalette.Success;
            }
            else
            {
                pHk.Text = "热键：部分冲突";
                pHk.DotColor = ThemePalette.Danger;
            }
        }
        var pTheme = statusPillBar.Find("theme");
        if (pTheme != null)
        {
            if (themeMode == 0) pTheme.Text = IsDark ? "跟随系统(暗)" : "跟随系统(亮)";
            else if (themeMode == 1) pTheme.Text = "夜间深色";
            else pTheme.Text = "护眼浅色";
            pTheme.DotColor = ThemePalette.Accent;
        }
        statusPillBar.Invalidate();
    }
}
#endif
