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

public static class Native
{
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vk);
    [DllImport("user32.dll")] static extern void keybd_event(byte b, byte s, uint f, UIntPtr e);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("kernel32.dll")] static extern void Sleep(uint ms);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern int GetSystemMetrics(int n);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern uint MapVirtualKeyW(uint code, uint mapType);

    [StructLayout(LayoutKind.Sequential)]
    struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Explicit)]
    struct INPUT { [FieldOffset(0)] public uint type; [FieldOffset(8)] public KEYBDINPUT ki; }
    [DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint n, INPUT[] p, int cb);

    const uint KEYEVENTF_KEYUP = 0x0002;
    const uint KEYEVENTF_SCANCODE = 0x0008;

    // ===== 输入法切换（避免中文输入法吞键）=====
    [DllImport("user32.dll")] static extern IntPtr GetKeyboardLayout(uint idThread);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr LoadKeyboardLayoutW(string id, uint flags);

    const uint WM_INPUTLANGCHANGEREQUEST = 0x0050;

    public static IntPtr SwitchToEnglish(IntPtr hwnd)
    {
        IntPtr old = IntPtr.Zero;
        try
        {
            uint pid;
            uint tid = GetWindowThreadProcessId(hwnd, out pid);
            old = GetKeyboardLayout(tid);
            IntPtr en = LoadKeyboardLayoutW("04090409", 0x00000001);
            if (en != IntPtr.Zero) PostMessage(hwnd, WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, en);
        }
        catch { }
        return old;
    }

    public static void RestoreLayout(IntPtr hwnd, IntPtr layout)
    {
        if (hwnd == IntPtr.Zero || layout == IntPtr.Zero) return;
        try { PostMessage(hwnd, WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, layout); }
        catch { }
    }

    public static void PressCtrlV()
    {
        byte ctrl = (byte)(MapVirtualKeyW(0x11, 0) & 0xFF);
        byte v = (byte)(MapVirtualKeyW(0x56, 0) & 0xFF);
        keybd_event(0x11, ctrl, 0, UIntPtr.Zero);
        Sleep(4);
        keybd_event(0x56, v, 0, UIntPtr.Zero);
        Sleep(4);
        keybd_event(0x56, v, 2, UIntPtr.Zero);
        Sleep(4);
        keybd_event(0x11, ctrl, 2, UIntPtr.Zero);
    }

    public static void SendScan(ushort scan, bool up)
    {
        var ins = new INPUT[1];
        ins[0].type = 1;
        ins[0].ki.wVk = 0;
        ins[0].ki.wScan = scan;
        ins[0].ki.dwFlags = KEYEVENTF_SCANCODE | (up ? KEYEVENTF_KEYUP : 0);
        SendInput(1, ins, Marshal.SizeOf(typeof(INPUT)));
    }

    public delegate bool EnumWndProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr h, EnumWndProc cb, IntPtr l);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowTextW(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, uint msg, IntPtr w, StringBuilder l);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr SendMessageW(IntPtr h, uint msg, IntPtr w, string l);
    public static void SetCueBanner(IntPtr h, string text)
    {
        try { SendMessageW(h, 0x1501, (IntPtr)1, text); } catch { }   // EM_SETCUEBANNER
    }
    [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassNameW(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr p);
    public delegate bool EnumProc(IntPtr h, IntPtr p);

    public struct RECT { public int L, T, R, B; }

    static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    static readonly object logLock = new object();

    public static void MakeTopmost(IntPtr h)
    {
        SetWindowPos(h, HWND_TOPMOST, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);
    }

    public static void CenterPhysical(IntPtr h)
    {
        RECT r;
        if (!GetWindowRect(h, out r)) return;
        int w = r.R - r.L, ht = r.B - r.T;
        int sw = GetSystemMetrics(0), sh = GetSystemMetrics(1);
        SetWindowPos(h, IntPtr.Zero, (sw - w) / 2, (sh - ht) / 2, 0, 0, 0x0001 | 0x0004 | 0x0010);
    }

    public static string LogPath = "";

    public static void Log(string s)
    {
        if (string.IsNullOrEmpty(LogPath)) return;
        try
        {
            lock (logLock)
            {
                try
                {
                    var fi = new FileInfo(LogPath);
                    if (fi.Exists && fi.Length > 2 * 1024 * 1024)
                    {
                        string bak = LogPath + ".old";
                        if (File.Exists(bak)) File.Delete(bak);
                        File.Move(LogPath, bak);
                    }
                }
                catch { }
                File.AppendAllText(LogPath, DateTime.Now.ToString("MM-dd HH:mm:ss.fff") + "  " + s + "\r\n", Encoding.UTF8);
            }
        }
        catch { }
    }

    public static int PidOf(IntPtr h)
    {
        uint pid;
        GetWindowThreadProcessId(h, out pid);
        return (int)pid;
    }

    public static bool Down(int vk) { return (GetAsyncKeyState(vk) & 0x8000) != 0; }

    public static void PressEnter()
    {
        byte sc = (byte)(MapVirtualKeyW(0x0D, 0) & 0xFF);
        keybd_event(0x0D, sc, 0, UIntPtr.Zero);
        Sleep(6);
        keybd_event(0x0D, sc, 2, UIntPtr.Zero);
    }

    public static void PressKey(byte vk)
    {
        ushort sc = (ushort)(MapVirtualKeyW(vk, 0) & 0xFF);
        SendScan(sc, false);
        Sleep(3);
        SendScan(sc, true);
    }

    // 找 Garry's Mod 主窗口（class Valve001）
    public static IntPtr FindGameWindow()
    {
        IntPtr found = IntPtr.Zero;
        try
        {
            EnumWindows(delegate(IntPtr h, IntPtr l)
            {
                if (!IsWindowVisible(h)) return true;
                var cls = new StringBuilder(64);
                GetClassNameW(h, cls, 64);
                if (cls.ToString() != "Valve001") return true;
                RECT r;
                if (GetWindowRect(h, out r) && (r.R - r.L) > 400) { found = h; return false; }
                return true;
            }, IntPtr.Zero);
        }
        catch { }
        return found;
    }

    public static string ForegroundProcess()
    {
        IntPtr fg = GetForegroundWindow();
        if (fg == IntPtr.Zero) return "";
        uint pid;
        GetWindowThreadProcessId(fg, out pid);
        if (pid == 0) return "";
        try
        {
            using (var proc = System.Diagnostics.Process.GetProcessById((int)pid))
            {
                return proc.ProcessName ?? "";
            }
        }
        catch { return ""; }
    }

    public static bool ActivateExistingInstance()
    {
        try
        {
            int selfPid;
            string selfName;
            using (var cur = System.Diagnostics.Process.GetCurrentProcess())
            {
                selfPid = cur.Id;
                selfName = cur.ProcessName;
            }
            System.Diagnostics.Process[] list = System.Diagnostics.Process.GetProcessesByName(selfName);
            bool activated = false;
            for (int i = 0; i < list.Length; i++)
            {
                using (System.Diagnostics.Process p = list[i])
                {
                    if (!activated && p.Id != selfPid)
                    {
                        IntPtr targetHwnd = p.MainWindowHandle;
                        if (targetHwnd == IntPtr.Zero)
                        {
                            int targetPid = p.Id;
                            EnumWindows(delegate(IntPtr h, IntPtr l)
                            {
                                uint wpid;
                                GetWindowThreadProcessId(h, out wpid);
                                if ((int)wpid == targetPid && IsWindowVisible(h))
                                {
                                    targetHwnd = h;
                                    return false;
                                }
                                return true;
                            }, IntPtr.Zero);
                        }
                        if (targetHwnd != IntPtr.Zero)
                        {
                            if (IsIconic(targetHwnd)) ShowWindow(targetHwnd, 9);
                            else ShowWindow(targetHwnd, 5);
                            SetForegroundWindow(targetHwnd);
                            activated = true;
                        }
                    }
                }
            }
            return activated;
        }
        catch { return false; }
    }
}

public class Overlay : Form
{
    public static readonly Color[] Palette = { Color.Lime, Color.Cyan, Color.DeepSkyBlue, Color.Yellow, Color.OrangeRed, Color.White };
    public static readonly string[] ColorNames = { "荧光绿", "青色", "天蓝", "亮黄", "橙红", "纯白" };
    public static readonly string[] Styles = { "十字(带缺口)", "完整十字", "圆点", "V 形", "同心圆", "T 形" };

    public Color HairColor = Color.Lime;
    public int Thickness = 2, Length = 9, Gap = 5, DotSize = 2;
    public int StyleIndex = 0;
    // 自定义准星图片（导入 PNG/BMP）；路径为空表示用内置矢量样式
    public Bitmap CustomImage;
    public string CustomImagePath = "";

    public Overlay()
    {
        FormBorderStyle = FormBorderStyle.None;
        TopMost = true;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.Magenta;
        TransparencyKey = Color.Magenta;
        Size = new Size(240, 240);
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        Visible = false;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x20;       // WS_EX_TRANSPARENT
            cp.ExStyle |= 0x80000;    // WS_EX_LAYERED
            cp.ExStyle |= 0x80;       // WS_EX_TOOLWINDOW
            cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
            return cp;
        }
    }

    public int ColorIndex
    {
        get
        {
            for (int i = 0; i < Palette.Length; i++)
                if (Palette[i].ToArgb() == HairColor.ToArgb()) return i;
            return 0;
        }
        set
        {
            int idx = ((value % Palette.Length) + Palette.Length) % Palette.Length;
            HairColor = Palette[idx];
            Invalidate();
        }
    }

    public void CycleColor()
    {
        ColorIndex = ColorIndex + 1;
    }

    public void CycleStyle()
    {
        StyleIndex = (StyleIndex + 1) % Styles.Length;
        Invalidate();
    }

    public void DrawCrosshair(Graphics g, float cx, float cy)
    {
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
        float g0 = Gap, len = Length;

        // 导入的图片优先：按「臂长+间隙」的两倍撑开，取最长边等比缩放，最近邻保持像素锐利
        if (CustomImage != null)
        {
            float side = Math.Max(8f, Math.Min(300f, (Length + Gap) * 2f));
            float sc = side / Math.Max(1, Math.Max(CustomImage.Width, CustomImage.Height));
            float iw = Math.Max(1f, CustomImage.Width * sc);
            float ih = Math.Max(1f, CustomImage.Height * sc);
            var oldInterp = g.InterpolationMode;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
            // 洋红当透明色：叠色窗口里它是透明键，预览控件里也会真透明
            using (var attrs = new System.Drawing.Imaging.ImageAttributes())
            {
                attrs.SetColorKey(Color.Magenta, Color.Magenta);
                int dx = (int)Math.Round(cx - iw / 2f), dy = (int)Math.Round(cy - ih / 2f);
                int dw = Math.Max(1, (int)Math.Round(iw)), dh = Math.Max(1, (int)Math.Round(ih));
                var dest = new Rectangle(dx, dy, dw, dh);
                g.DrawImage(CustomImage, dest, 0, 0, CustomImage.Width, CustomImage.Height,
                    GraphicsUnit.Pixel, attrs);
            }
            g.InterpolationMode = oldInterp;
            return;
        }

        // 粗线宽为奇数时偏移半像素，避免线条跨到两个像素上显得发虚/发歪
        if (Thickness % 2 == 1) { cx += 0.5f; cy += 0.5f; }

        using (var pen = new Pen(HairColor, Thickness))
        using (var brush = new SolidBrush(HairColor))
        {
            switch (StyleIndex)
            {
                case 0:
                    g.DrawLine(pen, cx - g0 - len, cy, cx - g0, cy);
                    g.DrawLine(pen, cx + g0, cy, cx + g0 + len, cy);
                    g.DrawLine(pen, cx, cy - g0 - len, cx, cy - g0);
                    g.DrawLine(pen, cx, cy + g0, cx, cy + g0 + len);
                    if (DotSize > 0)
                        g.FillEllipse(brush, cx - DotSize / 2f, cy - DotSize / 2f, DotSize, DotSize);
                    break;
                case 1:
                    g.DrawLine(pen, cx - g0 - len, cy, cx + g0 + len, cy);
                    g.DrawLine(pen, cx, cy - g0 - len, cx, cy + g0 + len);
                    break;
                case 2:
                    float r = Math.Max(2.5f, (DotSize > 0 ? DotSize : Thickness) * 1.6f);
                    g.FillEllipse(brush, cx - r, cy - r, r * 2, r * 2);
                    break;
                case 3:
                {
                    // V 形：三点折线（左臂→顶点→右臂），偏移取整保证左右完全对称，
                    // 用 Miter 连接让顶点是一个干净的尖角（两条线分开画会留缺口）
                    float s = len + g0;
                    // 比十字样式收一档，别的样式半径是 len+gap，V 只取一半多一点，
                    // 否则同参数下它比别的准星大一圈
                    float half = (float)Math.Round(s * 0.45f);
                    float top = (float)Math.Round(s * 0.27f);
                    float bot = (float)Math.Round(s * 0.45f);
                    if (half > len + 2f) half = len + 2f;
                    if (half < 2f) half = 2f;
                    if (top < 1f) top = 1f;
                    if (bot < 2f) bot = 2f;
                    var pts = new PointF[]
                    {
                        new PointF(cx - half, cy - top),
                        new PointF(cx,        cy + bot),
                        new PointF(cx + half, cy - top)
                    };
                    var oldJoin = pen.LineJoin;
                    var oldStart = pen.StartCap;
                    var oldEnd = pen.EndCap;
                    pen.LineJoin = System.Drawing.Drawing2D.LineJoin.Miter;
                    pen.StartCap = System.Drawing.Drawing2D.LineCap.Flat;
                    pen.EndCap = System.Drawing.Drawing2D.LineCap.Flat;
                    g.DrawLines(pen, pts);
                    pen.LineJoin = oldJoin;
                    pen.StartCap = oldStart;
                    pen.EndCap = oldEnd;
                    if (DotSize > 0)
                        g.FillEllipse(brush, cx - DotSize / 2f, cy - DotSize / 2f, DotSize, DotSize);
                    break;
                }
                case 4:
                    float rOut = len + g0;
                    float rIn = Math.Max(3.5f, rOut * 0.45f);
                    using (var thinPen = new Pen(HairColor, Math.Max(1f, Thickness * 0.6f)))
                    {
                        g.DrawEllipse(thinPen, cx - rOut, cy - rOut, rOut * 2, rOut * 2);
                        g.DrawEllipse(thinPen, cx - rIn, cy - rIn, rIn * 2, rIn * 2);
                    }
                    if (DotSize > 0)
                        g.FillEllipse(brush, cx - DotSize / 2f, cy - DotSize / 2f, DotSize, DotSize);
                    break;
                case 5:
                    g.DrawLine(pen, cx - g0 - len, cy, cx - g0, cy);
                    g.DrawLine(pen, cx + g0, cy, cx + g0 + len, cy);
                    g.DrawLine(pen, cx, cy + g0, cx, cy + g0 + len);
                    if (DotSize > 0)
                        g.FillEllipse(brush, cx - DotSize / 2f, cy - DotSize / 2f, DotSize, DotSize);
                    break;
            }
        }
    }

    // 把导入的图片处理成"透明键安全"的位图：alpha>=128 视为实心，其余涂成透明色洋红。
    // 叠色窗口（TransparencyKey）只认精确的洋红，半透明像素会混出色边，所以先做二值化。
    public static Bitmap PrepareCrosshairImage(string path)
    {
        try
        {
            using (var src = new Bitmap(path))
            {
                // 先看有没有透明通道；全不透明的图（JPG/不透明 PNG）就拿左上角颜色当底色扣掉
                bool hasAlpha = false;
                Color key = Color.Empty;
                Color corner = src.GetPixel(0, 0);
                for (int y = 0; y < src.Height && !hasAlpha; y++)
                    for (int x = 0; x < src.Width; x++)
                        if (src.GetPixel(x, y).A < 250) { hasAlpha = true; break; }
                if (!hasAlpha) key = corner;

                var outBmp = new Bitmap(src.Width, src.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                for (int y = 0; y < src.Height; y++)
                {
                    for (int x = 0; x < src.Width; x++)
                    {
                        Color c = src.GetPixel(x, y);
                        bool transparent;
                        if (hasAlpha) transparent = c.A < 128;
                        else
                        {
                            int d = Math.Abs(c.R - key.R) + Math.Abs(c.G - key.G) + Math.Abs(c.B - key.B);
                            transparent = d < 40;      // 和底色足够接近就算背景
                        }
                        if (transparent) outBmp.SetPixel(x, y, Color.Magenta);
                        else outBmp.SetPixel(x, y, Color.FromArgb(255, c.R, c.G, c.B));
                    }
                }
                return outBmp;
            }
        }
        catch { return null; }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        DrawCrosshair(e.Graphics, Width / 2f, Height / 2f);
    }
}


public class BufferedListBox : ListBox
{
    public BufferedListBox()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
    }
}

public class BufferedListView : ListView
{
    public BufferedListView()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
    }
}

public class WrapList : Control
{
    public string[] Labels = new string[0];
    public string[] Values = new string[0];
    public bool[] Heads = new bool[0];
    public string[] Notes = new string[0];   // 标题后的注解：小一号、斜体、灰色
    public string EmptyText = "（暂无条目）";
    public Font HeadFont;
    public Font SubFont;
    public int Sel = -1;

    public event EventHandler SelChanged;
    public event EventHandler ItemActivated;
    public event MouseEventHandler ItemRightClick;

    VScrollBar bar;
    ToolTip hover = new ToolTip();
    int[] heights = new int[0];
    int[] previewSize = new int[0];    // 0=不画预览，1..3=用第几档小字体
    int[] previewW = new int[0];
    int[] previewLines = new int[0];
    Font prevFontA, prevFontB, noteFont, noteFontS, noteFontXS, majorFont;
    int measuredWidth = -1;
    int scrollY = 0;
    int hoverIdx = -1;
    bool panning = false;
    int panStartY = 0;
    int panStartScroll = 0;

    public WrapList()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
               | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
               | ControlStyles.Selectable, true);
        TabStop = true;
        BackColor = Color.White;
        HeadFont = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold);
        SubFont = new Font("Consolas", 9f, FontStyle.Regular);
        // 右侧指令预览的三档自适应字号（只建一次，避免每帧 new Font 泄漏 GDI）
        noteFont = new Font("Microsoft YaHei UI", 8f, FontStyle.Italic);
        noteFontS = new Font("Microsoft YaHei UI", 7f, FontStyle.Italic);
        noteFontXS = new Font("Microsoft YaHei UI", 6.2f, FontStyle.Italic);
        majorFont = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold);
        prevFontA = new Font("Consolas", 8.5f, FontStyle.Regular);
        prevFontB = new Font("Consolas", 7.5f, FontStyle.Regular);

        bar = new VScrollBar();
        bar.Dock = DockStyle.Right;
        bar.Width = 16;
        bar.Visible = false;
        Controls.Add(bar);
        bar.ValueChanged += delegate { scrollY = bar.Value; Invalidate(); };

        hover.AutoPopDelay = 20000;
        hover.InitialDelay = 260;
        hover.ReshowDelay = 80;
        hover.ShowAlways = true;
    }

    int ViewWidth { get { return Math.Max(60, ClientSize.Width - (bar.Visible ? bar.Width : 0)); } }

    public void SetData(string[] labels, string[] values, bool[] heads)
    {
        SetData(labels, values, heads, null);
    }

    public void SetData(string[] labels, string[] values, bool[] heads, string[] notes)
    {
        Labels = labels ?? new string[0];
        Values = values ?? new string[0];
        Heads = heads ?? new bool[0];
        Notes = notes ?? new string[0];
        heights = new int[0];
        measuredWidth = -1;
        scrollY = 0;
        hoverIdx = -1;
        if (Sel >= Labels.Length) Sel = -1;
        EnsureHeights();
        Invalidate();
    }

    Font PreviewFontOf(int k)
    {
        return (k == 1) ? prevFontA : prevFontB;
    }

    // 预览只取第一行；过长就截断（避免测量超长文本），剩下的交给换行
    static string PreviewTextOf(string v)
    {
        if (string.IsNullOrEmpty(v)) return "";
        string t = v.Trim();
        if (t.Length == 0) return "";
        int nl = t.IndexOfAny(new char[] { (char)13, (char)10 });
        if (nl >= 0) t = t.Substring(0, nl).TrimEnd() + " …";
        if (t.Length > 160) t = t.Substring(0, 160) + "…";
        return t;
    }

    void MeasureAll()
    {
        int w = ViewWidth;
        measuredWidth = w;
        heights = new int[Labels.Length];
        previewSize = new int[Labels.Length];
        previewW = new int[Labels.Length];
        previewLines = new int[Labels.Length];
        for (int i = 0; i < Labels.Length; i++)
        {
            bool isHd = (i < Heads.Length && Heads[i]);
            Font f = isHd ? HeadFont : Font;
            int tw = w - (isHd ? 24 : 36);
            if (tw < 60) tw = 60;
            string text = Labels[i] ?? "";
            Size sz = TextRenderer.MeasureText(text, f, new Size(tw, 4000),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            int h = Math.Max(34, sz.Height + (isHd ? 14 : 12));

            // 标题旁的注解可能换 2~4 行：用最小字号实测一下需要多高，避免被截断
            string nt = (i < Notes.Length) ? (Notes[i] ?? "") : "";
            if (!isHd && nt.Length > 0)
            {
                int noteW = Math.Max(160, (w - 300) / 2);    // 注解可用宽度（与实际绘制接近）
                Size ns = TextRenderer.MeasureText(nt, noteFont, new Size(noteW, 4000),
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
                int need = ns.Height + 14;                   // 上下留白
                if (need > h) h = Math.Min(need, 150);       // 行高跟着注解走，最多 5~6 行
            }

            // 右侧指令预览：先缩字号试一行，仍放不下就换成两行并抬高这一行
            if (!isHd && w >= 460 && i < Values.Length && Values[i] != null)
            {
                string cmd = PreviewTextOf(Values[i]);
                if (cmd.Length > 0)
                {
                    int leftPad = 18;
                    Size labelSz = TextRenderer.MeasureText(text, f);
                    int availRight = w - (leftPad + labelSz.Width + 46);
                    if (availRight >= 150)
                    {
                        bool hasNoteRow = (i < Notes.Length && !string.IsNullOrEmpty(Notes[i]));
                        // 有注解的行把指令预览压窄，省出的宽度给注解，少折几行
                        int maxW = Math.Min((int)(w * (hasNoteRow ? 0.34 : 0.60)), availRight);
                        if (maxW < 150) maxW = 150;
                        int chosen = 0;
                        for (int k = 1; k <= 2; k++)
                        {
                            Size ps = TextRenderer.MeasureText(cmd, PreviewFontOf(k));
                            if (ps.Width <= maxW) { chosen = k; break; }
                        }
                        int lines = 1;
                        int ph = 0;
                        if (chosen == 0)
                        {
                            chosen = 2;
                            Font pf = PreviewFontOf(2);
                            Size ps = TextRenderer.MeasureText(cmd, pf, new Size(maxW, 300),
                                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
                            int lh = pf.Height + 2;
                            lines = Math.Max(1, Math.Min(2, (ps.Height + lh - 1) / lh));
                            ph = lines * lh;
                        }
                        previewSize[i] = chosen;
                        previewW[i] = maxW;
                        previewLines[i] = lines;
                        if (ph > 0)
                        {
                            int need = ph + 12;
                            if (need > h) h = need;
                        }
                    }
                }
            }
            heights[i] = h;
        }
    }

    public int ScrollTop
    {
        get { return scrollY; }
        set
        {
            scrollY = Math.Max(0, value);
            UpdateBar();
            Invalidate();
        }
    }

    /// <summary>把第 idx 条滚到顶部（用于分类跳转）</summary>
    public void ScrollToIndex(int idx)
    {
        if (idx < 0 || idx >= Labels.Length) return;
        EnsureHeights();
        int y = 0;
        for (int i = 0; i < idx && i < heights.Length; i++) y += heights[i];
        int maxScroll = Math.Max(0, TotalHeight() - ClientSize.Height);
        if (y > maxScroll) y = maxScroll;
        if (y < 0) y = 0;
        scrollY = y;
        UpdateBar();
        Invalidate();
    }

    void EnsureHeights()
    {
        int w = ViewWidth;
        if (heights.Length == Labels.Length && measuredWidth == w) return;
        MeasureAll();
        UpdateBar();
    }

    int TotalHeight()
    {
        int t = 0;
        for (int i = 0; i < heights.Length; i++) t += heights[i];
        return t;
    }

    void UpdateBar()
    {
        int total = TotalHeight();
        int h = ClientSize.Height;
        bool need = total > h + 2;
        if (bar.Visible != need)
        {
            bar.Visible = need;
            MeasureAll();
            total = TotalHeight();
        }
        if (!need) { scrollY = 0; return; }

        bar.Minimum = 0;
        bar.Maximum = Math.Max(1, total - 1);
        bar.LargeChange = Math.Max(1, h);
        bar.SmallChange = 40;
        int maxScroll = Math.Max(0, total - h);
        if (scrollY < 0) scrollY = 0;
        if (scrollY > maxScroll) scrollY = maxScroll;
        int v = Math.Min(scrollY, Math.Max(0, bar.Maximum - bar.LargeChange + 1));
        if (bar.Value != v) bar.Value = v;
    }

    void ScrollTo(int y)
    {
        int maxScroll = Math.Max(0, TotalHeight() - ClientSize.Height);
        if (y < 0) y = 0;
        if (y > maxScroll) y = maxScroll;
        if (y == scrollY) return;
        scrollY = y;
        int v = Math.Min(scrollY, Math.Max(0, bar.Maximum - bar.LargeChange + 1));
        if (bar.Visible && bar.Value != v) bar.Value = v;
        Invalidate();
    }

    int HitTest(int y)
    {
        int py = y + scrollY;
        int acc = 0;
        for (int i = 0; i < heights.Length; i++)
        {
            if (py >= acc && py < acc + heights[i]) return i;
            acc += heights[i];
        }
        return -1;
    }

    int ItemTop(int index)
    {
        int acc = 0;
        for (int i = 0; i < index && i < heights.Length; i++) acc += heights[i];
        return acc;
    }

    public void EnsureVisible(int index)
    {
        if (index < 0 || index >= heights.Length) return;
        int top = ItemTop(index);
        int bottom = top + heights[index];
        if (top < scrollY) ScrollTo(top);
        else if (bottom > scrollY + ClientSize.Height) ScrollTo(bottom - ClientSize.Height);
    }

    public int FindSelectable(int start, int dir)
    {
        int n = Labels.Length;
        if (n == 0) return -1;
        int i = start;
        while (i >= 0 && i < n)
        {
            bool isHd = (i < Heads.Length && Heads[i]);
            if (!isHd) return i;
            i += dir;
        }
        return -1;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        EnsureHeights();
        if (heights.Length == 0)
        {
            TextRenderer.DrawText(e.Graphics, EmptyText, Font, new Point(16, 18), Color.FromArgb(120, 130, 145));
            return;
        }

        int w = ViewWidth;
        int y = -scrollY;
        using (var headBg = new SolidBrush(Color.FromArgb(241, 246, 253)))
        using (var headAccent = new SolidBrush(Color.FromArgb(2, 106, 192)))
        using (var selBg = new SolidBrush(Color.FromArgb(219, 236, 255)))
        using (var hoverBg = new SolidBrush(Color.FromArgb(245, 249, 255)))
        using (var badgeBg = new SolidBrush(Color.FromArgb(236, 243, 252)))
        using (var sepPen = new Pen(Color.FromArgb(236, 240, 245)))
        {
            for (int i = 0; i < Labels.Length; i++)
            {
                int h = heights[i];
                if (y + h >= 0 && y <= ClientSize.Height)
                {
                    Rectangle r = new Rectangle(0, y, w, h);
                    bool isHd = (i < Heads.Length && Heads[i]);
                    if (isHd)
                    {
                        e.Graphics.FillRectangle(headBg, r);
                        e.Graphics.FillRectangle(headAccent, new Rectangle(0, y + 5, 4, Math.Max(4, h - 10)));
                    }
                    else if (i == Sel)
                    {
                        e.Graphics.FillRectangle(selBg, r);
                        e.Graphics.FillRectangle(headAccent, new Rectangle(0, y, 4, h));
                    }
                    else if (i == hoverIdx)
                    {
                        e.Graphics.FillRectangle(hoverBg, r);
                    }

                    string labelText = Labels[i] ?? "";
                    bool isMajor = isHd && labelText.StartsWith("▓");
                    if (isMajor) labelText = labelText.Substring(1).Trim();
                    Font f = isMajor ? majorFont : (isHd ? HeadFont : Font);
                    Color c = isMajor ? Color.FromArgb(20, 52, 96) : (isHd ? Color.FromArgb(8, 88, 160)
                            : (i == Sel ? Color.FromArgb(12, 34, 64) : Color.FromArgb(30, 36, 44)));
                    int leftPad = isHd ? 14 : 18;

                    // 右侧指令预览：字体自适应（8.5/7.5/6.8 三档），放不下就换行，不再硬截断
                    int rightReserve = 14;
                    if (!isHd && i < previewSize.Length && previewSize[i] > 0 && i < previewW.Length && previewW[i] > 0)
                    {
                        string cmdPreview = PreviewTextOf(i < Values.Length ? Values[i] : null);
                        if (cmdPreview.Length > 0)
                        {
                            Font pf = PreviewFontOf(previewSize[i]);
                            int lines = (i < previewLines.Length && previewLines[i] > 0) ? previewLines[i] : 1;
                            int lh = pf.Height + 2;
                            int ph = lines * lh;
                            int maxW = previewW[i];
                            int py = r.Y + Math.Max(4, (h - ph) / 2);
                            Rectangle prevRect = new Rectangle(w - maxW - 14, py, maxW, ph);
                            TextFormatFlags pflags = TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak
                                | (lines > 1 ? TextFormatFlags.Left : TextFormatFlags.Right);
                            TextRenderer.DrawText(e.Graphics, cmdPreview, pf, prevRect,
                                (i == Sel || i == hoverIdx) ? Color.FromArgb(36, 96, 168) : Color.FromArgb(126, 138, 156),
                                pflags);
                            rightReserve = maxW + 24;
                        }
                    }

                    int titleW = Math.Max(60, r.Width - leftPad - rightReserve);
                    TextRenderer.DrawText(e.Graphics, labelText, f,
                        new Rectangle(r.X + leftPad, r.Y + 5, titleW, h - 8), c,
                        TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.Left);

                    // 标题旁的注解：小一号斜体灰字，只在标题单行放得下时并排显示
                    string noteText = (i < Notes.Length) ? (Notes[i] ?? "") : "";
                    if (!isHd && noteText.Length > 0)
                    {
                        Size one = TextRenderer.MeasureText(e.Graphics, labelText, f,
                            new Size(4000, int.MaxValue), TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
                        Size wrapped = TextRenderer.MeasureText(e.Graphics, labelText, f,
                            new Size(titleW, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
                        if (wrapped.Height <= f.Height + 6)
                        {
                            int nx = r.X + leftPad + one.Width + 10;
                            int nw = r.Right - rightReserve - nx - 6;   // 右边留给指令预览，不越界
                            if (nw > 40)
                            {
                                // 字号固定 8pt 斜体，不再缩小；放不下就靠行高变高来容纳
                                TextRenderer.DrawText(e.Graphics, noteText, noteFont,
                                    new Rectangle(nx, r.Y, nw, h), Color.FromArgb(146, 154, 166),
                                    TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);
                            }
                        }
                    }

                    e.Graphics.DrawLine(sepPen, 8, r.Bottom - 1, w - 8, r.Bottom - 1);
                }
                y += h;
                if (y > ClientSize.Height) break;
            }
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        measuredWidth = -1;
        EnsureHeights();
        Invalidate();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        ScrollTo(scrollY - e.Delta);
        base.OnMouseWheel(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        Focus();
        if (e.Button == MouseButtons.Middle)
        {
            panning = true;
            panStartY = e.Y;
            panStartScroll = scrollY;
            Capture = true;
            Cursor = Cursors.SizeNS;
            return;
        }
        int idx = HitTest(e.Y);
        if (idx >= 0)
        {
            bool isHd = (idx < Heads.Length && Heads[idx]);
            if (isHd)
            {
                base.OnMouseDown(e);
                return;
            }
            if (e.Button == MouseButtons.Right)
            {
                Sel = idx;
                Invalidate();
                if (SelChanged != null) SelChanged(this, EventArgs.Empty);
                if (ItemRightClick != null) ItemRightClick(this, e);
                return;
            }
            if (Sel != idx)
            {
                Sel = idx;
                Invalidate();
                if (SelChanged != null) SelChanged(this, EventArgs.Empty);
            }
        }
        base.OnMouseDown(e);
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            int idx = HitTest(e.Y);
            if (idx >= 0 && !(idx < Heads.Length && Heads[idx]) && ItemActivated != null)
                ItemActivated(this, EventArgs.Empty);
        }
        base.OnMouseClick(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (panning)
        {
            ScrollTo(panStartScroll - (e.Y - panStartY));
            return;
        }
        int idx = HitTest(e.Y);
        if (idx != hoverIdx)
        {
            hoverIdx = idx;
            Invalidate();
            bool isHd = (idx >= 0 && idx < Heads.Length && Heads[idx]);
            Cursor = (idx >= 0 && !isHd) ? Cursors.Hand : Cursors.Default;
            if (idx >= 0 && idx < Values.Length && !isHd)
            {
                string t = Values[idx] ?? "";
                if (t.Length > 600) t = t.Substring(0, 600) + "…";
                hover.SetToolTip(this, t.Length == 0 ? "" : t + "\r\n（左键单击复制，右键更多选项）");
            }
            else hover.SetToolTip(this, "");
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (panning && e.Button == MouseButtons.Middle)
        {
            panning = false;
            Capture = false;
            Cursor = Cursors.Default;
            return;
        }
        base.OnMouseUp(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        if (hoverIdx != -1)
        {
            hoverIdx = -1;
            Invalidate();
        }
        Cursor = Cursors.Default;
        hover.SetToolTip(this, "");
        base.OnMouseLeave(e);
    }

    protected override bool IsInputKey(Keys keyData)
    {
        if (keyData == Keys.Up || keyData == Keys.Down
            || keyData == Keys.PageUp || keyData == Keys.PageDown
            || keyData == Keys.Home || keyData == Keys.End) return true;
        return base.IsInputKey(keyData);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Down)
        {
            int next = FindSelectable(Sel + 1, 1);
            if (next >= 0 && next != Sel)
            {
                Sel = next;
                EnsureVisible(Sel);
                Invalidate();
                if (SelChanged != null) SelChanged(this, EventArgs.Empty);
            }
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Up)
        {
            int prev = FindSelectable((Sel <= 0 ? 0 : Sel - 1), -1);
            if (prev >= 0 && prev != Sel)
            {
                Sel = prev;
                EnsureVisible(Sel);
                Invalidate();
                if (SelChanged != null) SelChanged(this, EventArgs.Empty);
            }
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.PageDown)
        {
            ScrollTo(scrollY + ClientSize.Height - 40);
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.PageUp)
        {
            ScrollTo(scrollY - ClientSize.Height + 40);
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Home)
        {
            ScrollTo(0);
            int first = FindSelectable(0, 1);
            if (first >= 0) { Sel = first; Invalidate(); if (SelChanged != null) SelChanged(this, EventArgs.Empty); }
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.End)
        {
            ScrollTo(TotalHeight());
            int last = FindSelectable(Labels.Length - 1, -1);
            if (last >= 0) { Sel = last; Invalidate(); if (SelChanged != null) SelChanged(this, EventArgs.Empty); }
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Enter)
        {
            if (ItemActivated != null && Sel >= 0 && !(Sel < Heads.Length && Heads[Sel]))
                ItemActivated(this, EventArgs.Empty);
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (HeadFont != null) { HeadFont.Dispose(); HeadFont = null; }
            if (SubFont != null) { SubFont.Dispose(); SubFont = null; }
            if (hover != null) { hover.Dispose(); hover = null; }
        }
        base.Dispose(disposing);
    }
}

public class Toast : Form
{
    Label lbl;
    System.Windows.Forms.Timer hide;

    public Toast()
    {
        FormBorderStyle = FormBorderStyle.None;
        TopMost = true;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(20, 26, 36);
        Opacity = 0.88;
        Size = new Size(420, 46);

        lbl = new Label();
        lbl.Dock = DockStyle.Fill;
        lbl.TextAlign = ContentAlignment.MiddleCenter;
        lbl.ForeColor = Color.White;
        lbl.Font = new Font("Microsoft YaHei UI", 11f, FontStyle.Bold);
        lbl.BackColor = Color.Transparent;
        Controls.Add(lbl);

        hide = new System.Windows.Forms.Timer();
        hide.Interval = 2000;
        hide.Tick += delegate { hide.Stop(); Visible = false; };

        Visible = false;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x20;
            cp.ExStyle |= 0x80;
            cp.ExStyle |= 0x08000000;
            return cp;
        }
    }

    bool persist = false;

    void Lay(string msg)
    {
        Size sz = TextRenderer.MeasureText(msg, lbl.Font);
        int w = Math.Max(340, Math.Min(820, sz.Width + 56));
        int h = 46;
        if (Width != w || Height != h) Size = new Size(w, h);
        int sw = Native.GetSystemMetrics(0);
        Native.SetWindowPos(Handle, new IntPtr(-1), (sw - w) / 2, 60, w, h, 0x0010);
    }

    public void ShowText(string text)
    {
        if (IsDisposed) return;
        string msg = text ?? "";
        persist = false;
        BackColor = Color.FromArgb(20, 26, 36);
        lbl.Text = msg;
        Lay(msg);
        Visible = true;
        hide.Stop();
        hide.Start();
    }

    // 常驻提示：不自动消失，重复调用同一条文本时不做任何重绘（避免闪烁）
    public void ShowPersistent(string text)
    {
        if (IsDisposed) return;
        string msg = text ?? "";
        if (persist && Visible && lbl.Text == msg) return;
        persist = true;
        BackColor = Color.FromArgb(96, 30, 30);
        lbl.Text = msg;
        Lay(msg);
        Visible = true;
        hide.Stop();
    }

    public bool IsPersistent { get { return persist; } }

    public void HideIfPersistent()
    {
        if (IsDisposed || !persist) return;
        persist = false;
        hide.Stop();
        Visible = false;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (hide != null) { hide.Stop(); hide.Dispose(); hide = null; }
            if (lbl != null && lbl.Font != null) { lbl.Font.Dispose(); }
        }
        base.Dispose(disposing);
    }
}

public class CrosshairPreview : Panel
{
    public Overlay Source;

    public CrosshairPreview(Overlay src)
    {
        Source = src;
        BackColor = Color.FromArgb(26, 30, 38);
        BorderStyle = BorderStyle.FixedSingle;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        int w = ClientSize.Width, h = ClientSize.Height;
        float cx = w / 2f, cy = h / 2f;

        using (var gridPen = new Pen(Color.FromArgb(46, 52, 62), 1f))
        {
            g.DrawLine(gridPen, 0, cy, w, cy);
            g.DrawLine(gridPen, cx, 0, cx, h);
            g.DrawRectangle(gridPen, 16, 16, Math.Max(10, w - 32), Math.Max(10, h - 32));
        }

        if (Source != null)
        {
            Source.DrawCrosshair(g, cx, cy);
        }

        TextRenderer.DrawText(g, "实时预览（点击换色）", Font, new Point(6, h - 22), Color.FromArgb(160, 170, 182));
    }
}

public partial class MainForm : Form
{
    public const string APP_VERSION = "V1.7.0";
    public const string APP_NOTICE = "仅供 RXSEND Breach mio 服使用 · 不保证时效性 · " + APP_VERSION;

    Overlay overlay;
    Toast toast;
    System.Windows.Forms.Timer tick;
    Label lblStatus;
    ComboBox cmbSpeed;
    ComboBox cmbStyle;
    ComboBox cmbColor;
    NumericUpDown nThick, nLen, nGap, nDot;
    CrosshairPreview crossPreview;
    Button btnAuto;
    TabControl tabs;

    bool autoOn = false;
    bool typingOn = false;
    bool joinedStop = false;
    volatile bool isClosing = false;
    int favVersion = 0;
    int ocrBusy = 0;
    int ocrLoadingHits = 0;
    bool ocrHoldSend = false;
    string lastOcrScene = "";   // 已看到一次读条，先别再发 connect（等确认）
    OcrEngine cachedOcrEngine = null;
    bool ocrEngineInitTried = false;
    string savedClipboardText = null;

    bool suppressSettingsSave = false;
    System.ComponentModel.IContainer uiComponents = new System.ComponentModel.Container();
    List<IDisposable> ownedGdi = new List<IDisposable>();
    Dictionary<string, Font> fontPool = new Dictionary<string, Font>(StringComparer.Ordinal);

    int[] favCount = new int[0];
    string[] favName = new string[0];
    int[] favMax = new int[0];
    string favPath = "";
    // ===== 关于 / 自动更新 =====
    public const string UPDATE_URL_DEFAULT = "https://rx-toolbox.pages.dev/version.json";
    public const string APP_VERSION_NUM = "1.7.0";
    public const string APP_BUILD_DATE = "2026-11-15";
    string updateUrl = UPDATE_URL_DEFAULT;
    TabPage aboutPage, spacerPage, devPage;
    TextBox txtDevStatus;
    ListView lvVersions;
    TextBox txtHubUrl, txtHubKey;
    bool devToolsOn = false;
    bool devToolsLoading = false;
    CheckBox chkDevTools;
    string hubUrl = "https://rx-toolbox.pages.dev/api";
    string hubKey = "";
    Label lblUpdState;
    Panel changelogHost;
    string updLatestNotes = "";
    Button btnUpdCheck, btnUpdOpen;
    CheckBox chkAutoUpdate;
    Thread updThread;
    volatile bool updBusy;
    string updLatestUrl = "", updLatestFallback = "", updLatestVersion = "";
    string updLatestSha = "";
    bool startupCheckDone;
    bool autoUpdateOn = true;

    string settingsPath = "";
    string[] favList = new string[0];
    CheckBox chkOcr;

    // ===== V1.4 / V1.5 挤服页控件与状态 =====
    Panel pJoinPanel;
    RadioButton rbTapMode, rbOneIp, rbPollMode;
    BufferedListView lvSrv;
    Button btnRefreshFav, btnCheckAll, btnUncheckAll, btnAddFav, btnPasteFav, btnDelFav, btnEditFav;
    ContextMenuStrip cmsSrv;
    Label lblListHint, lblJoinHint, lblNewIp, lblNewAlias, lblTapGuide, lblCrossHint;
    TextBox txtNewIp, txtNewAlias;
    Button btnGuide;
    bool suppressListCheck = false;
    // 收藏轮询的勾选偏好（addr 集合）；chkPrefLoaded 区分"没存过"和"用户就是要全不选"
    System.Collections.Generic.HashSet<string> chkPref = new System.Collections.Generic.HashSet<string>();
    bool chkPrefLoaded = false;
    bool chkPrefAuthoritative = false;   // 刚读完 ini，这一次重建以文件偏好为准
    bool suppressModeEvent = false;
    string pendingSelAddr = "";
    List<string> targets = new List<string>();
    string currentTargetAddr = "";
    IntPtr savedLayout = IntPtr.Zero;
    int multiIdx = 0;
    DateTime tapOcrAt = DateTime.MinValue;
    int tapOcrFail = 0;
    int srvSortCol = -1;
    bool srvSortAsc = true;

    // ===== 可自定义热键：索引 = id-1 =====
    bool[] prevHkDown = new bool[5];
    bool[] hkRegistered = new bool[8];   // 1..5 = 样式/挤服/颜色/准星/退出，6 = 热键总开关
    bool hotkeysOff;                     // 用户临时关闭热键（F5 等交还系统）
    Button btnHotkeyOff;
    static readonly int[] HKDefVk = { 0x74, 0x75, 0x76, 0x77, 0x78 };
    static readonly int[] HKDefMod = { 0, 0, 0, 0, 0x0002 };
    static readonly string[] HKFunc = { "准星 换样式", "挤服 开始/停止", "准星 换颜色", "准星 显示/隐藏", "退出工具" };
    static readonly string[] HKTip = {
        "在 6 种准星样式之间循环",
        "开始 / 停止挤服（按当前模式）",
        "在 6 种颜色之间循环",
        "把准星显示出来或收起来",
        "直接关掉工具箱"
    };
    int[] hkVk = new int[5];
    int[] hkMod = new int[5];
    bool capturingKey = false;
    Button btnCross, btnColor, btnHotkey, bLog, btnCrossImg, btnCrossImgClear, btnCrossImgPaste;
    Label lblCrossImg;
    string crossImgToLoad = "";

    DateTime lastEnter = DateTime.MinValue;
    int sent = 0;
    DateTime lastUi = DateTime.MinValue;

    static readonly string[] GameProcs = { "gmod", "gmod_win64" };
    static readonly object[] TapIntervals = { "100 ms", "200 ms", "300 ms", "500 ms", "900 ms" };

    Font TrackFont(string family, float size, FontStyle style)
    {
        string key = family + "|" + size.ToString("F2") + "|" + ((int)style).ToString();
        Font f;
        if (fontPool.TryGetValue(key, out f) && f != null)
            return f;
        f = new Font(family, size, style);
        fontPool[key] = f;
        ownedGdi.Add(f);
        return f;
    }

    void SafeBeginInvoke(MethodInvoker action)
    {
        if (isClosing || IsDisposed || !IsHandleCreated) return;
        try
        {
            BeginInvoke(new MethodInvoker(delegate
            {
                if (isClosing || IsDisposed) return;
                try { action(); } catch (Exception ex) { Native.Log("UI invoke EX: " + ex.Message); }
            }));
        }
        catch (ObjectDisposedException) { }
        catch (InvalidOperationException) { }
        catch { }
    }

    // ===== A2S 网络查询引擎（带超时 DNS 解析与分包重组）=====
    static byte[] A2SReq(byte[] challenge)
    {
        var ms = new MemoryStream();
        ms.Write(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }, 0, 4);
        byte[] q = Encoding.ASCII.GetBytes("TSource Engine Query");
        ms.Write(q, 0, q.Length);
        ms.WriteByte(0);
        if (challenge != null) ms.Write(challenge, 0, 4);
        return ms.ToArray();
    }

    static IPEndPoint ResolveEndPoint(string host, int port, int timeoutMs)
    {
        IPAddress ip;
        if (IPAddress.TryParse(host, out ip))
            return new IPEndPoint(ip, port);
        try
        {
            IAsyncResult ar = Dns.BeginGetHostAddresses(host, null, null);
            if (!ar.AsyncWaitHandle.WaitOne(Math.Max(300, timeoutMs)))
                return null;
            IPAddress[] addrs = Dns.EndGetHostAddresses(ar);
            for (int i = 0; i < addrs.Length; i++)
                if (addrs[i].AddressFamily == AddressFamily.InterNetwork)
                    return new IPEndPoint(addrs[i], port);
            if (addrs.Length > 0) return new IPEndPoint(addrs[0], port);
        }
        catch { }
        return null;
    }

    static bool IsSimpleHeader(byte[] buf, byte type)
    {
        return buf != null && buf.Length >= 5
            && buf[0] == 0xFF && buf[1] == 0xFF && buf[2] == 0xFF && buf[3] == 0xFF
            && buf[4] == type;
    }

    static bool IsSplitHeader(byte[] buf)
    {
        return buf != null && buf.Length > 12
            && buf[0] == 0xFE && buf[1] == 0xFF && buf[2] == 0xFF && buf[3] == 0xFF;
    }

    static bool SameEndPoint(IPEndPoint a, IPEndPoint b)
    {
        return a != null && b != null && a.Port == b.Port && a.Address.Equals(b.Address);
    }

    static byte[] ReceiveA2SPayload(UdpClient udp, IPEndPoint ep, byte[] first)
    {
        if (first == null || first.Length < 5) return null;
        if (first[0] == 0xFF && first[1] == 0xFF && first[2] == 0xFF && first[3] == 0xFF)
            return first;
        if (!IsSplitHeader(first)) return null;
        if ((first[7] & 0x80) != 0) return null; // BZip2 压缩分片不直接拼接

        int total = first[8];
        int idx0 = first[9];
        if (total <= 0 || total > 16 || idx0 < 0 || idx0 >= total) return null;

        byte id0 = first[4], id1 = first[5], id2 = first[6], id3 = first[7];
        byte[][] chunks = new byte[total][];
        chunks[idx0] = new byte[first.Length - 12];
        Array.Copy(first, 12, chunks[idx0], 0, chunks[idx0].Length);
        int received = 1;

        int guard = 0;
        while (received < total && guard < 24)
        {
            guard++;
            byte[] rr;
            IPEndPoint from = new IPEndPoint(IPAddress.Any, 0);
            try { rr = udp.Receive(ref from); }
            catch { break; }
            if (!SameEndPoint(from, ep) || !IsSplitHeader(rr)) continue;
            if (rr[4] != id0 || rr[5] != id1 || rr[6] != id2 || rr[7] != id3) continue;
            if (rr[8] != total) continue;
            int idx = rr[9];
            if (idx >= 0 && idx < total && chunks[idx] == null)
            {
                chunks[idx] = new byte[rr.Length - 12];
                Array.Copy(rr, 12, chunks[idx], 0, chunks[idx].Length);
                received++;
            }
        }
        if (received < total) return null;

        var ms = new MemoryStream();
        for (int i = 0; i < total; i++)
        {
            byte[] ch = chunks[i];
            if (ch == null) return null;
            int offset = (i > 0 && ch.Length >= 4 && ch[0] == 0xFF && ch[1] == 0xFF && ch[2] == 0xFF && ch[3] == 0xFF) ? 4 : 0;
            ms.Write(ch, offset, ch.Length - offset);
        }
        return ms.ToArray();
    }

    static bool A2SQuery(string host, int port, int timeoutMs, out string name, out int players, out int maxPlayers)
    {
        name = "";
        players = 0;
        maxPlayers = 0;
        try
        {
            var ep = ResolveEndPoint(host, port, timeoutMs);
            if (ep == null) return false;
            using (var udp = new UdpClient())
            {
                udp.Client.ReceiveTimeout = timeoutMs;
                byte[] req = A2SReq(null);
                udp.Send(req, req.Length, ep);
                IPEndPoint from = new IPEndPoint(IPAddress.Any, 0);
                byte[] resp = udp.Receive(ref from);
                if (!SameEndPoint(from, ep)) return false;

                int tries = 0;
                while (resp != null && resp.Length >= 9 && IsSimpleHeader(resp, 0x41) && tries < 2)
                {
                    tries++;
                    byte[] req2 = A2SReq(new byte[] { resp[5], resp[6], resp[7], resp[8] });
                    udp.Send(req2, req2.Length, ep);
                    from = new IPEndPoint(IPAddress.Any, 0);
                    resp = udp.Receive(ref from);
                    if (!SameEndPoint(from, ep)) return false;
                }

                resp = ReceiveA2SPayload(udp, ep, resp);
                if (resp == null || resp.Length < 6 || !IsSimpleHeader(resp, 0x49)) return false;
                int p = 6;
                int nend = p;
                while (nend < resp.Length && resp[nend] != 0) nend++;
                if (nend >= resp.Length) return false;
                name = Encoding.UTF8.GetString(resp, p, Math.Max(0, nend - p));
                p = nend + 1;

                for (int i = 0; i < 3; i++)
                {
                    while (p < resp.Length && resp[p] != 0) p++;
                    if (p >= resp.Length) return false;
                    p++;
                }

                p += 2; // AppID
                if (p + 1 >= resp.Length) return false;
                players = resp[p];
                maxPlayers = resp[p + 1];
                return true;
            }
        }
        catch { return false; }
    }

    void HandleHotkey(int id)
    {
        switch (id)
        {
            case 1:
                overlay.CycleStyle();
                suppressSettingsSave = true;
                if (cmbStyle != null) cmbStyle.SelectedIndex = overlay.StyleIndex;
                suppressSettingsSave = false;
                if (crossPreview != null) crossPreview.Invalidate();
                SaveSettings();
                toast.ShowText("准星样式：" + Overlay.Styles[overlay.StyleIndex]);
                RefreshUi(true);
                break;
            case 2:
                ToggleStart();
                break;
            case 3:
                overlay.CycleColor();
                suppressSettingsSave = true;
                if (cmbColor != null) cmbColor.SelectedIndex = overlay.ColorIndex;
                suppressSettingsSave = false;
                if (crossPreview != null) crossPreview.Invalidate();
                SaveSettings();
                toast.ShowText("准星颜色：" + Overlay.ColorNames[overlay.ColorIndex]);
                RefreshUi(true);
                break;
            case 4:
                ToggleCross();
                break;
            case 5:
                Native.Log("quit via hotkey");
                Close();
                break;
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x0312 && !capturingKey)   // WM_HOTKEY
        {
            int id = m.WParam.ToInt32();
            if (id == HK_TOGGLE_ID) { ToggleHotkeys(); return; }
            if (hotkeysOff) return;          // 已关闭：忽略其余热键（其实已注销，这里兜底）
            if (id >= 1 && id <= 5) { HandleHotkey(id); return; }
        }
        base.WndProc(ref m);
    }

    public MainForm(string icoPath, string[] tabTitles, string[][] tabItems, string dataDir)
    {
        AutoScaleMode = AutoScaleMode.None;
        Text = "RX 工具箱 " + APP_VERSION;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(960, 720);
        MinimumSize = new Size(820, 560);
        Font = new Font("Microsoft YaHei UI", 9f);
        BackColor = Color.FromArgb(248, 250, 252);

        try
        {
            if (!string.IsNullOrEmpty(icoPath) && File.Exists(icoPath))
                Icon = new Icon(icoPath);
            else
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch { }

        for (int i = 0; i < 5; i++)
        {
            hkVk[i] = HKDefVk[i];
            hkMod[i] = HKDefMod[i];
        }
        dataOverridePath = Path.Combine(dataDir, "data-custom.txt");
        customPath = Path.Combine(dataDir, "custom.txt");
        favPath = Path.Combine(dataDir, "servers.txt");
        settingsPath = Path.Combine(dataDir, "settings.ini");
        Native.Log("==================== " + APP_VERSION + " 启动 ====================");
        Native.Log("exe: " + Application.ExecutablePath);
        Native.Log("os: " + Environment.OSVersion.VersionString + " 64bit=" + Environment.Is64BitProcess
            + " 日志: " + Native.LogPath);
        LoadCustom();

        overlay = new Overlay();
        toast = new Toast();

        var bottomBar = new Panel();
        bottomBar.Dock = DockStyle.Bottom;
        bottomBar.Height = 50;
        bottomBar.BackColor = Color.FromArgb(238, 242, 248);

        var lblNotice = new Label();
        lblNotice.Dock = DockStyle.Top;
        lblNotice.Height = 20;
        lblNotice.Padding = new Padding(10, 0, 0, 0);
        lblNotice.TextAlign = ContentAlignment.MiddleLeft;
        lblNotice.AutoEllipsis = true;
        lblNotice.UseMnemonic = false;
        lblNotice.BackColor = Color.FromArgb(238, 242, 248);
        lblNotice.ForeColor = Color.FromArgb(120, 126, 136);
        lblNotice.Font = TrackFont("Microsoft YaHei UI", 8f, FontStyle.Regular);
        lblNotice.Text = APP_NOTICE;
        bottomBar.Controls.Add(lblNotice);

        lblStatus = new Label();
        lblStatus.Dock = DockStyle.Fill;
        lblStatus.Padding = new Padding(10, 0, 0, 0);
        lblStatus.TextAlign = ContentAlignment.MiddleLeft;
        lblStatus.AutoEllipsis = true;
        lblStatus.UseMnemonic = false;
        lblStatus.BackColor = Color.FromArgb(238, 242, 248);
        lblStatus.ForeColor = Color.FromArgb(52, 64, 84);
        bottomBar.Controls.Add(lblStatus);

        tabs = new TabControl();
        tabs.Dock = DockStyle.Fill;
        Controls.Add(tabs);
        Controls.Add(bottomBar);

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
                tabs.TabPages.Add(BuildTextPage(tabTitles[i], list.ToArray(), i));
            }
        }
        tabs.TabPages.Add(BuildCustomPage());
        tabs.TabPages.Add(BuildToolPage());
        devPage = BuildDevPage();
        aboutPage = BuildAboutPage();
        spacerPage = new TabPage("");
        tabs.TabPages.Add(spacerPage);          // 只占宽度，把右边两个顶到最右
        if (devToolsOn) tabs.TabPages.Add(devPage);   // 开发者工具默认隐藏，在「关于」里开
        tabs.TabPages.Add(aboutPage);
        tabs.Selecting += delegate(object s2, TabControlCancelEventArgs e2)
        {
            if (e2.TabPage == spacerPage) e2.Cancel = true;   // 空白页不可选中
        };
        tabs.Resize += delegate { LayoutAboutTab(); };
        // TabPage 在首次显示前拿到的尺寸不准，切到工具页时重排一次
        tabs.SelectedIndexChanged += delegate
        {
            if (pJoinPanel != null && pJoinPanel.Visible)
            {
                try { LayoutJoin(); } catch { }
            }
        };

        LoadSettings();
        // 开发者工具默认隐藏；设置里开着才显示
        if (chkDevTools != null)
        {
            devToolsLoading = true;
            chkDevTools.Checked = devToolsOn;
            devToolsLoading = false;
        }
        ApplyDevToolsTab(devToolsOn);
        // 设置里带着勾选偏好，读完重建列表（否则界面还是构造时默认的全选）
        FillSrvList();
        ApplyModeUi();
        UpdateHotkeyLabels();
        if (!string.IsNullOrEmpty(crossImgToLoad)) LoadCrosshairImage(crossImgToLoad, true);
        // 构造期有控件抢焦点会让 TabControl 自己切换页签，这里强制回到首页
        try
        {
            if (tabs != null && tabs.TabPages.Count > 0
                && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RX_START_TAB")))
                tabs.SelectedIndex = 0;
        }
        catch { }

        string startTab = Environment.GetEnvironmentVariable("RX_START_TAB");
        if (!string.IsNullOrEmpty(startTab))
        {
            int si;
            if (int.TryParse(startTab, out si) && si >= 0 && si < tabs.TabPages.Count)
                tabs.SelectedIndex = si;
        }

        tick = new System.Windows.Forms.Timer();
        tick.Interval = 20;
        tick.Tick += OnTick;
        tick.Start();

        RefreshUi(true);
        Native.Log("main form ready");
    }

    static bool TrySetClipboardText(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                Clipboard.SetDataObject(text, false, 2, 10);
                return true;
            }
            catch
            {
                if (attempt < 2) Thread.Sleep(5);
            }
        }
        return false;
    }

    void CopyToClipboard(string title, string body)
    {
        try
        {
            if (string.IsNullOrEmpty(body)) return;
            if (TrySetClipboardText(body))
            {
                savedClipboardText = body;
                toast.ShowText("已复制：" + title);
                Native.Log("copy: " + title + " | " + body);
            }
            else
            {
                toast.ShowText("复制失败：剪贴板正被其它软件占用");
                Native.Log("copy busy: " + title);
            }
        }
        catch (Exception ex)
        {
            toast.ShowText("复制失败：" + ex.Message);
            Native.Log("copy EX: " + ex.Message);
        }
    }

    // ===== 数据表本机覆盖（无损多行与行首 ### 转义保护）=====
    Dictionary<string, List<string>> LoadDataOverrides()
    {
        var map = new Dictionary<string, List<string>>();
        try
        {
            if (string.IsNullOrEmpty(dataOverridePath) || !File.Exists(dataOverridePath)) return map;
            string[] lines = File.ReadAllLines(dataOverridePath, Encoding.UTF8);
            string cur = null;
            var buf = new List<string>();
            foreach (string raw in lines)
            {
                string line = raw ?? "";
                if (line.StartsWith("###", StringComparison.Ordinal))
                {
                    if (cur != null) map[cur] = buf;
                    cur = line.Substring(3).Trim();
                    buf = new List<string>();
                    continue;
                }
                if (cur == null) continue;
                buf.Add(UnescapeItem(line));
            }
            if (cur != null) map[cur] = buf;
        }
        catch { }
        return map;
    }

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
            for (int i = 0; i < dataTabLabels.Length; i++)
            {
                sb.Append("###").Append(dataTabLabels[i]).Append("\r\n");
                var list = dataTabLists[i];
                for (int k = 0; k < list.Count; k++)
                    sb.Append(EscapeItem(list[k])).Append("\r\n");
            }
            AtomicWriteText(dataOverridePath, sb.ToString());
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
            dlg.MinimumSize = new Size(760, 460);
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
            grid.BackgroundColor = Color.White;
            grid.BorderStyle = BorderStyle.FixedSingle;
            grid.GridColor = Color.FromArgb(210, 218, 228);
            grid.CellBorderStyle = DataGridViewCellBorderStyle.Single;
            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.DisplayedCells;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            grid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            grid.DefaultCellStyle.Padding = new Padding(6, 4, 6, 4);
            grid.DefaultCellStyle.BackColor = Color.White;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(210, 228, 250);
            grid.DefaultCellStyle.SelectionForeColor = Color.Black;
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 253);

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

            var bReset = new Button(); bReset.Text = "恢复内置"; bReset.SetBounds(528, 10, 100, 32);
            bReset.ForeColor = Color.FromArgb(160, 60, 60);
            bReset.Click += delegate
            {
                if (MessageBox.Show(dlg, "清空本机修改，恢复成内置数据？\n（重启工具后生效）",
                        "恢复内置", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
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
                // 内存里也回滚成内置数据并刷新页面，不用重启
                int baseIdx = Array.IndexOf(dataTabLabels, label);
                if (baseIdx >= 0 && baseIdx < dataTabBase.Length && dataTabBase[baseIdx] != null)
                {
                    dataTabLists[baseIdx] = new List<string>(dataTabBase[baseIdx]);
                    RebuildDataPage(baseIdx);
                }
                MessageBox.Show(dlg, "已恢复内置数据（本页已刷新）。", "提示");
            };
            bar.Controls.Add(bReset);

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

                    using (var brush = new SolidBrush(Color.FromArgb(214, 229, 250)))
                        e.Graphics.FillRectangle(brush, merged);
                    using (var pen = new Pen(Color.FromArgb(160, 190, 230)))
                    {
                        e.Graphics.DrawLine(pen, merged.Left, merged.Bottom - 1, merged.Right, merged.Bottom - 1);
                        e.Graphics.DrawLine(pen, merged.Right - 1, merged.Top, merged.Right - 1, merged.Bottom - 1);
                    }

                    bool selected = grid.Rows[e.RowIndex].Selected;
                    Color tc = selected ? Color.Black : Color.FromArgb(26, 62, 120);
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

            dlg.ShowDialog(this);
        }

        SaveDataOverrides();
        RebuildDataPage(tabIndex);
        Native.Log("data edit: " + label + " -> " + list.Count + " 条");
    }

    void RebuildDataPage(int index)
    {
        try
        {
            int tabIdx = index;
            if (tabIdx < 0 || tabIdx >= tabs.TabPages.Count) return;
            var old = tabs.TabPages[tabIdx];
            tabs.TabPages.Remove(old);
            old.Dispose();
            tabs.TabPages.Insert(tabIdx, BuildTextPage(dataTabLabels[index], dataTabLists[index].ToArray(), index));
            tabs.SelectedIndex = tabIdx;
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
        bool hasExplicitUlx = false;
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
            if (!string.IsNullOrEmpty(ux)) hasExplicitUlx = true;   // 只做标记，条数在下面统一算
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

        const string EmptyPreviewHint = "💡 选中或单击下方任意条目即可自动复制指令，并在此预览完整多行文本（支持鼠标划选局部复制）";

        var preview = new TextBox();
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

        var btnEditItems = new Button();
        btnEditItems.Text = "编辑条目";
        btnEditItems.SetBounds(6, 74, 164, 28);
        btnEditItems.BackColor = Color.FromArgb(232, 240, 252);
        btnEditItems.FlatStyle = FlatStyle.Flat;
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

            if (hasModes)
            {
                bool isJobPage = IsRolePage(label);

                rbMain = new RadioButton();
                rbMain.Text = isJobPage ? "刷到脚下 (forcespawn)" : "主指令";
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
                    ? "⚠ ulx 写法要在游戏控制台（~）里敲；主指令直接在聊天框发送"
                    : "⚠ 部分角色只能用 devforcespawn 或 ulx 指令刷新，以实测为准";
                int tipX = isJobPage ? (hasUlx ? 642 : 470) : (hasUlx ? 382 : 210);
                lblAltTip.SetBounds(tipX, 4, 300, 26);     // 和单选按钮同一行，省一整行高度
                lblAltTip.TextAlign = ContentAlignment.MiddleLeft;
                lblAltTip.ForeColor = Color.FromArgb(176, 92, 0);
                lblAltTip.Font = TrackFont("Microsoft YaHei UI", 8.5f, FontStyle.Regular);
                lblAltTip.UseMnemonic = false;
                bar.Controls.Add(lblAltTip);
                bar.Resize += delegate
                {
                    lblAltTip.Width = Math.Max(120, bar.ClientSize.Width - tipX - 12);
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
                    raw = "ulx " + raw.Substring(1);          // !xxx → ulx xxx
            }
            if (idBox != null) raw = ApplyTargetName(raw, idBox.Text);
            return raw;
        };

        Action<string> setPreviewContent = delegate(string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                preview.ForeColor = Color.FromArgb(140, 150, 165);
                preview.Text = EmptyPreviewHint;
            }
            else
            {
                preview.ForeColor = Color.FromArgb(20, 28, 40);
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
            list.Sel = (keepSelect >= 0 && keepSelect < shownN) ? keepSelect : -1;
            list.Invalidate();

            bool anyHead = false;
            for (int k = 0; k < shownN; k++) { if (vHeads[k]) { anyHead = true; break; } }
            btnCats.Enabled = anyHead;
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
            pop.BackColor = Color.White;
            pop.KeyPreview = true;

            var lb = new ListBox();
            lb.Dock = DockStyle.Fill;
            lb.BorderStyle = BorderStyle.None;
            lb.IntegralHeight = false;
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

    static string Shorten(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Length > 60 ? s.Substring(0, 60) + "…" : s;
    }

    // ===================== 自定义文本 =====================

    string customPath = "";
    string dataOverridePath = "";
    string[] dataTabLabels = new string[0];
    List<string>[] dataTabLists = new List<string>[0];
    string[][] dataTabBase = new string[0][];   // 内置原始数据，恢复内置时可免重启回滚
    string[] cTitles = new string[0];
    string[] cTexts = new string[0];

    void LoadCustom()
    {
        try
        {
            if (!File.Exists(customPath)) return;
            string raw = File.ReadAllText(customPath, Encoding.UTF8);
            string[] recs = raw.Split('\u0002');
            var ts = new List<string>();
            var cs = new List<string>();
            for (int i = 0; i < recs.Length; i++)
            {
                string rec = recs[i];
                if (rec.Length == 0) continue;
                int p = rec.IndexOf('\u0001');
                if (p < 0) { ts.Add(rec); cs.Add(""); }
                else { ts.Add(rec.Substring(0, p)); cs.Add(rec.Substring(p + 1)); }
            }
            cTitles = ts.ToArray();
            cTexts = cs.ToArray();
        }
        catch (Exception ex) { Native.Log("custom load: " + ex.Message); }
    }

    static string CleanCtrl(string s, bool singleLine)
    {
        if (string.IsNullOrEmpty(s)) return "";
        string res = s.Replace('\u0001', ' ').Replace('\u0002', ' ');
        if (singleLine) res = res.Replace('\r', ' ').Replace('\n', ' ');
        return res;
    }

    static bool AtomicWriteText(string path, string content)
    {
        if (string.IsNullOrEmpty(path)) return false;
        string tmp = path + ".tmp";
        try
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(tmp, content ?? "", Encoding.UTF8);
            if (File.Exists(path))
            {
                try
                {
                    File.Replace(tmp, path, null);
                    return true;
                }
                catch
                {
                    File.Copy(tmp, path, true);
                    return true;
                }
            }
            else
            {
                try
                {
                    File.Move(tmp, path);
                    return true;
                }
                catch
                {
                    File.Copy(tmp, path, true);
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            Native.Log("AtomicWriteText fail (" + path + "): " + ex.Message);
            return false;
        }
        finally
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
        }
    }

    bool SaveCustom()
    {
        try
        {
            var sb = new StringBuilder();
            for (int i = 0; i < cTitles.Length; i++)
                sb.Append(CleanCtrl(cTitles[i], true)).Append('\u0001').Append(CleanCtrl(cTexts[i], false)).Append('\u0002');
            return AtomicWriteText(customPath, sb.ToString());
        }
        catch (Exception ex)
        {
            Native.Log("custom save: " + ex.Message);
            return false;
        }
    }

    TabPage BuildCustomPage()
    {
        Font ui = TrackFont("Microsoft YaHei UI", 9.5f, FontStyle.Regular);
        Font mono = TrackFont("Consolas", 10f, FontStyle.Regular);
        Font btnBold = TrackFont("Microsoft YaHei UI", 10f, FontStyle.Bold);
        Font cardFont = TrackFont("Microsoft YaHei UI", 10.5f, FontStyle.Bold);

        var page = new TabPage("自定义");
        page.Padding = new Padding(0);
        page.BackColor = Color.White;

        var pCards = new Panel();
        pCards.SetBounds(0, 0, page.ClientSize.Width, page.ClientSize.Height);
        pCards.Dock = DockStyle.Fill;
        page.Controls.Add(pCards);

        var pEdit = new Panel();
        pEdit.SetBounds(0, 0, page.ClientSize.Width, page.ClientSize.Height);
        pEdit.Dock = DockStyle.Fill;
        pEdit.Visible = false;
        page.Controls.Add(pEdit);

        var bEdit = new Button();
        bEdit.Text = "⚙ 编辑条目";
        bEdit.Font = btnBold;
        bEdit.SetBounds(10, 6, 140, 34);
        pCards.Controls.Add(bEdit);

        var tip = new Label();
        tip.Text = "左键单击卡片 = 复制全文，右键卡片 = 快捷编辑/删除　·　上方预览框可划选复制局部片段";
        tip.ForeColor = Color.DimGray;
        tip.SetBounds(160, 12, 520, 22);
        pCards.Controls.Add(tip);

        var lblCardFilter = new Label();
        lblCardFilter.Text = "筛选：";
        lblCardFilter.ForeColor = Color.Gray;
        lblCardFilter.SetBounds(686, 12, 44, 22);
        lblCardFilter.Anchor = AnchorStyles.Top;
        pCards.Controls.Add(lblCardFilter);

        var txtCardFilter = new TextBox();
        txtCardFilter.Font = ui;
        txtCardFilter.SetBounds(730, 8, 140, 26);
        txtCardFilter.Anchor = AnchorStyles.Top;
        // 靠右的位置在 Resize 里算，避免 Anchor 在面板还没尺寸时算出屏幕外的坐标
        pCards.Resize += delegate
        {
            int right = pCards.ClientSize.Width - 10;
            txtCardFilter.Left = Math.Max(730, right - txtCardFilter.Width);
            lblCardFilter.Left = txtCardFilter.Left - 44;
        };
        pCards.Controls.Add(txtCardFilter);

        var preview = new TextBox();
        preview.Font = mono;
        preview.Multiline = true;
        preview.ReadOnly = true;
        preview.ScrollBars = ScrollBars.Vertical;
        preview.BorderStyle = BorderStyle.FixedSingle;
        preview.BackColor = Color.FromArgb(250, 252, 255);
        preview.SetBounds(10, 46, pCards.ClientSize.Width - 20, 84);
        preview.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        pCards.Controls.Add(preview);

        var flow = new FlowLayoutPanel();
        flow.SetBounds(10, 138, pCards.ClientSize.Width - 20, pCards.ClientSize.Height - 146);
        flow.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        flow.AutoScroll = true;
        flow.FlowDirection = FlowDirection.LeftToRight;
        flow.WrapContents = true;
        flow.Padding = new Padding(0, 4, 0, 0);
        pCards.Controls.Add(flow);

        var hover = new ToolTip(uiComponents);
        hover.AutoPopDelay = 20000;
        hover.InitialDelay = 250;
        hover.ReshowDelay = 80;
        hover.ShowAlways = true;

        var bBack = new Button();
        bBack.Text = "← 返回卡片页";
        bBack.Font = btnBold;
        bBack.SetBounds(10, 6, 136, 34);
        pEdit.Controls.Add(bBack);

        var tip2 = new Label();
        tip2.Text = "选中左侧条目后可在右侧修改名称和内容（切换条目或返回时会自动保存修改）";
        tip2.ForeColor = Color.DimGray;
        tip2.SetBounds(156, 12, 680, 22);
        pEdit.Controls.Add(tip2);

        var gL = new GroupBox();
        gL.Text = "我的文本列表";
        gL.Font = ui;
        gL.SetBounds(10, 46, 380, pEdit.ClientSize.Height - 56);
        gL.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
        pEdit.Controls.Add(gL);

        var list = new BufferedListBox();
        list.Font = TrackFont("Microsoft YaHei UI", 10f, FontStyle.Regular);
        list.BorderStyle = BorderStyle.FixedSingle;
        list.IntegralHeight = false;
        list.SetBounds(12, 24, 356, gL.ClientSize.Height - 76);
        list.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        gL.Controls.Add(list);

        int btnY = gL.ClientSize.Height - 44;
        var bAdd = new Button(); bAdd.Text = "新增"; bAdd.SetBounds(12, btnY, 84, 32); bAdd.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        var bDel = new Button(); bDel.Text = "删除"; bDel.SetBounds(102, btnY, 84, 32); bDel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        var bUp = new Button(); bUp.Text = "上移"; bUp.SetBounds(192, btnY, 84, 32); bUp.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        var bDown = new Button(); bDown.Text = "下移"; bDown.SetBounds(282, btnY, 84, 32); bDown.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        gL.Controls.Add(bAdd); gL.Controls.Add(bDel); gL.Controls.Add(bUp); gL.Controls.Add(bDown);

        var gR = new GroupBox();
        gR.Text = "编辑选中条目";
        gR.Font = ui;
        gR.SetBounds(400, 46, pEdit.ClientSize.Width - 410, pEdit.ClientSize.Height - 56);
        gR.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        pEdit.Controls.Add(gR);

        var lTitle = new Label(); lTitle.Text = "名称（卡片上显示的标题）："; lTitle.SetBounds(12, 26, 240, 22);
        var tTitle = new TextBox(); tTitle.Font = ui; tTitle.SetBounds(12, 50, gR.ClientSize.Width - 24, 26);
        tTitle.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        var lText = new Label(); lText.Text = "内容（点击卡片实际复制的文本）："; lText.SetBounds(12, 86, 280, 22);
        var tText = new TextBox(); tText.Font = mono; tText.Multiline = true;
        tText.ScrollBars = ScrollBars.Vertical;
        tText.SetBounds(12, 110, gR.ClientSize.Width - 24, gR.ClientSize.Height - 166);
        tText.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

        int rBtnY = gR.ClientSize.Height - 44;
        var bSave = new Button(); bSave.Text = "保存修改 (Ctrl+S)"; bSave.SetBounds(12, rBtnY, 146, 34);
        bSave.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        bSave.BackColor = Color.FromArgb(228, 242, 255);
        var bCopy = new Button(); bCopy.Text = "复制内容"; bCopy.SetBounds(168, rBtnY, 110, 34);
        bCopy.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        var lblPos = new Label(); lblPos.ForeColor = Color.Gray; lblPos.SetBounds(290, rBtnY + 6, 160, 22);
        lblPos.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        gR.Controls.Add(lTitle); gR.Controls.Add(tTitle); gR.Controls.Add(lText);
        gR.Controls.Add(tText); gR.Controls.Add(bSave); gR.Controls.Add(bCopy); gR.Controls.Add(lblPos);

        int currentEditIdx = -1;
        bool suppressLoad = false;

        Action commitPendingEdit = delegate
        {
            if (currentEditIdx >= 0 && currentEditIdx < cTitles.Length)
            {
                string newTitle = tTitle.Text.Trim();
                string newText = tText.Text;
                if (newTitle.Length > 0 && (cTitles[currentEditIdx] != newTitle || cTexts[currentEditIdx] != newText))
                {
                    cTitles[currentEditIdx] = newTitle;
                    cTexts[currentEditIdx] = newText;
                    SaveCustom();
                }
            }
        };

        Action<int> loadInto = delegate(int i)
        {
            suppressLoad = true;
            currentEditIdx = i;
            if (i < 0 || i >= cTitles.Length)
            {
                tTitle.Text = ""; tText.Text = ""; lblPos.Text = "";
                tTitle.Enabled = false; tText.Enabled = false; bSave.Enabled = false;
                suppressLoad = false;
                return;
            }
            tTitle.Enabled = true; tText.Enabled = true; bSave.Enabled = true;
            tTitle.Text = cTitles[i];
            tText.Text = cTexts[i];
            lblPos.Text = "第 " + (i + 1) + " / " + cTitles.Length + " 条";
            suppressLoad = false;
        };

        Action rebuildList = delegate
        {
            suppressLoad = true;
            list.BeginUpdate();
            list.Items.Clear();
            for (int i = 0; i < cTitles.Length; i++) list.Items.Add(cTitles[i]);
            list.EndUpdate();
            suppressLoad = false;
            if (list.Items.Count > 0)
            {
                list.SelectedIndex = 0;
                loadInto(0);
            }
            else loadInto(-1);
        };

        Action rebuildCards = null;
        rebuildCards = delegate
        {
            ClearAndDisposeControls(flow);
            string q = txtCardFilter.Text.Trim();
            int shownCards = 0;
            for (int i = 0; i < cTitles.Length; i++)
            {
                if (q.Length > 0)
                {
                    bool match = cTitles[i].IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                              || cTexts[i].IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!match) continue;
                }
                shownCards++;
                int idx = i;
                var b = new Button();
                b.Text = cTitles[i];
                b.Font = cardFont;
                b.Size = new Size(268, 68);
                b.Margin = new Padding(4, 4, 8, 8);
                b.TextAlign = ContentAlignment.MiddleCenter;
                b.BackColor = Color.FromArgb(238, 244, 252);
                b.FlatStyle = FlatStyle.Flat;
                b.FlatAppearance.BorderColor = Color.FromArgb(160, 190, 225);
                b.Cursor = Cursors.Hand;
                string tipText = cTexts[idx];
                if (tipText.Length > 600) tipText = tipText.Substring(0, 600) + " …";
                hover.SetToolTip(b, string.IsNullOrEmpty(tipText) ? "（空）左键复制 / 右键管理" : tipText + Environment.NewLine + "（左键单击复制，右键编辑或删除）");
                b.Click += delegate
                {
                    preview.Text = cTexts[idx];
                    preview.Select(0, 0);
                    CopyToClipboard(cTitles[idx], cTexts[idx]);
                };
                b.MouseUp += delegate(object s, MouseEventArgs e)
                {
                    if (e.Button != MouseButtons.Right) return;
                    var menu = new ContextMenuStrip();
                    menu.Closed += delegate { SafeBeginInvoke(delegate { menu.Dispose(); }); };
                    var mCopy = new ToolStripMenuItem("复制内容：" + Shorten(cTitles[idx]));
                    mCopy.Click += delegate { CopyToClipboard(cTitles[idx], cTexts[idx]); };
                    var mEdit = new ToolStripMenuItem("编辑此条目");
                    mEdit.Click += delegate
                    {
                        pCards.Visible = false;
                        pEdit.Visible = true;
                        rebuildList();
                        if (idx >= 0 && idx < list.Items.Count) list.SelectedIndex = idx;
                        tText.Focus();
                    };
                    var mDel = new ToolStripMenuItem("删除此条目");
                    mDel.Click += delegate
                    {
                        var nt = new string[cTitles.Length - 1];
                        var nx = new string[cTexts.Length - 1];
                        for (int k = 0, j = 0; k < cTitles.Length; k++)
                        {
                            if (k == idx) continue;
                            nt[j] = cTitles[k]; nx[j] = cTexts[k]; j++;
                        }
                        cTitles = nt; cTexts = nx;
                        SaveCustom();
                        rebuildCards();
                        toast.ShowText("已删除条目");
                    };
                    menu.Items.Add(mCopy);
                    menu.Items.Add(mEdit);
                    menu.Items.Add(new ToolStripSeparator());
                    menu.Items.Add(mDel);
                    menu.Show(b, e.Location);
                };
                flow.Controls.Add(b);
            }

            if (cTitles.Length == 0)
            {
                var empty = new Label();
                empty.Text = "还没有自定义条目 —— 点左上角「⚙ 编辑条目」添加常用广播或口令";
                empty.ForeColor = Color.Gray;
                empty.AutoSize = true;
                empty.Margin = new Padding(8, 20, 0, 0);
                flow.Controls.Add(empty);
            }
            else if (shownCards == 0)
            {
                var empty = new Label();
                empty.Text = "没有匹配“" + q + "”的卡片";
                empty.ForeColor = Color.Gray;
                empty.AutoSize = true;
                empty.Margin = new Padding(8, 20, 0, 0);
                flow.Controls.Add(empty);
            }
        };

        txtCardFilter.TextChanged += delegate { rebuildCards(); };
        txtCardFilter.KeyDown += delegate(object s, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                txtCardFilter.Text = "";
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        };

        list.SelectedIndexChanged += delegate
        {
            if (suppressLoad) return;
            if (currentEditIdx >= 0 && currentEditIdx < cTitles.Length && currentEditIdx != list.SelectedIndex)
            {
                string editedTitle = tTitle.Text.Trim();
                if (editedTitle.Length > 0 && (cTitles[currentEditIdx] != editedTitle || cTexts[currentEditIdx] != tText.Text))
                {
                    cTitles[currentEditIdx] = editedTitle;
                    cTexts[currentEditIdx] = tText.Text;
                    SaveCustom();
                    suppressLoad = true;
                    list.Items[currentEditIdx] = editedTitle;
                    suppressLoad = false;
                }
            }
            loadInto(list.SelectedIndex);
        };

        bAdd.Click += delegate
        {
            commitPendingEdit();
            var nt = new string[cTitles.Length + 1];
            var nx = new string[cTexts.Length + 1];
            Array.Copy(cTitles, nt, cTitles.Length);
            Array.Copy(cTexts, nx, cTexts.Length);
            nt[nt.Length - 1] = "新条目 " + (cTitles.Length + 1);
            nx[nx.Length - 1] = "";
            cTitles = nt; cTexts = nx;
            SaveCustom();
            rebuildList();
            list.SelectedIndex = list.Items.Count - 1;
            tTitle.Focus();
            tTitle.SelectAll();
        };

        bDel.Click += delegate
        {
            int i = list.SelectedIndex;
            if (i < 0 || i >= cTitles.Length) return;
            currentEditIdx = -1;
            var nt = new string[cTitles.Length - 1];
            var nx = new string[cTexts.Length - 1];
            for (int k = 0, j = 0; k < cTitles.Length; k++)
            {
                if (k == i) continue;
                nt[j] = cTitles[k]; nx[j] = cTexts[k]; j++;
            }
            cTitles = nt; cTexts = nx;
            SaveCustom();
            rebuildList();
            if (list.Items.Count > 0)
                list.SelectedIndex = Math.Min(i, list.Items.Count - 1);
        };

        Action<int> move = delegate(int dir)
        {
            commitPendingEdit();
            int i = list.SelectedIndex;
            int j = i + dir;
            if (i < 0 || j < 0 || j >= cTitles.Length) return;
            string t = cTitles[i]; cTitles[i] = cTitles[j]; cTitles[j] = t;
            t = cTexts[i]; cTexts[i] = cTexts[j]; cTexts[j] = t;
            SaveCustom();
            currentEditIdx = -1;
            rebuildList();
            list.SelectedIndex = j;
        };
        bUp.Click += delegate { move(-1); };
        bDown.Click += delegate { move(1); };

        Action doSaveCurrent = delegate
        {
            int i = list.SelectedIndex;
            if (i < 0 || i >= cTitles.Length) return;
            string name = tTitle.Text.Trim();
            if (name.Length == 0) { toast.ShowText("名称不能为空"); return; }
            cTitles[i] = name;
            cTexts[i] = tText.Text;
            bool ok = SaveCustom();
            int keep = i;
            currentEditIdx = -1;
            rebuildList();
            list.SelectedIndex = keep;
            toast.ShowText(ok ? ("已保存：" + name) : "保存失败，请检查文件权限");
        };

        bSave.Click += delegate { doSaveCurrent(); };

        KeyEventHandler ctrlSHandler = delegate(object s, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.S)
            {
                doSaveCurrent();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        };
        tTitle.KeyDown += ctrlSHandler;
        tText.KeyDown += ctrlSHandler;

        bCopy.Click += delegate
        {
            CopyToClipboard(tTitle.Text, tText.Text);
        };

        bEdit.Click += delegate
        {
            pCards.Visible = false;
            pEdit.Visible = true;
            rebuildList();
        };

        bBack.Click += delegate
        {
            commitPendingEdit();
            pEdit.Visible = false;
            pCards.Visible = true;
            rebuildCards();
        };

        if (cTitles.Length == 0)
        {
            cTitles = new string[] { "MTF开局广播" };
            cTexts = new string[] { "MTF优先接科研，允许逗留轻收，普科持枪警告，防化持危险品击毙" };
            SaveCustom();
        }
        rebuildCards();

        return page;
    }

    static void ClearAndDisposeControls(Control parent)
    {
        if (parent == null) return;
        while (parent.Controls.Count > 0)
        {
            Control c = parent.Controls[0];
            parent.Controls.RemoveAt(0);
            c.Dispose();
        }
    }



    // ===================== 工具页（挤服助手 + 屏幕准星） =====================

    // 让"关于"贴在标签栏最右侧：中间插一个只有宽度的空白页
    void ApplyDevToolsTab(bool show)
    {
        try
        {
            bool has = tabs.TabPages.Contains(devPage);
            if (show && !has)
            {
                int idx = tabs.TabPages.IndexOf(aboutPage);
                if (idx < 0) idx = tabs.TabPages.Count;
                tabs.TabPages.Insert(idx, devPage);   // 插在「关于」前面，保持贴右
            }
            else if (!show && has)
            {
                tabs.TabPages.Remove(devPage);
            }
            LayoutAboutTab();
        }
        catch { }
    }

    // 开发用：RX_DUMP_UI=1 把每页控件的文本/坐标/可见性写到 dump-ui.log，
    // 布局改动可以先看数字，不必截图。
    void DumpUi()
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("RX 工具箱 UI dump  " + APP_VERSION + "  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("form " + ClientSize.Width + "x" + ClientSize.Height);
            for (int t = 0; t < tabs.TabPages.Count; t++)
            {
                TabPage pg = tabs.TabPages[t];
                sb.AppendLine();
                sb.AppendLine("[tab " + t + "] " + pg.Text + "  (" + pg.Controls.Count + " 个直接子控件)");
                DumpControls(sb, pg, "  ", 0);
            }
            string dir = Path.GetDirectoryName(Application.ExecutablePath);
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

    void LayoutAboutTab()
    {
        if (tabs == null || spacerPage == null || aboutPage == null) return;
        try
        {
            int w = tabs.ClientSize.Width;
            if (w < 260) return;
            int target = w - 6;                       // "关于"右边要贴到这里
            spacerPage.Text = "";
            tabs.PerformLayout();

            // 二分找空格数：让"关于"的右边界尽量贴近 target
            int lo = 0, hi = 400, best = 0;
            for (int iter = 0; iter < 9; iter++)
            {
                int mid = (lo + hi) / 2;
                spacerPage.Text = mid == 0 ? "" : new string(' ', mid);
                tabs.PerformLayout();
                int right = tabs.GetTabRect(tabs.TabPages.IndexOf(aboutPage)).Right;
                if (right <= target) { best = mid; lo = mid + 1; }
                else hi = mid - 1;
                if (lo > hi) break;
            }
            spacerPage.Text = best == 0 ? "" : new string(' ', best);
            tabs.PerformLayout();
        }
        catch { }
    }

    // ---------------- 更新日志 ----------------
    // 格式：版本|日期|第一条~第二条~…（用 ~ 分行）
    static readonly string[] CHANGELOG = new string[]
    {
        "1.7.0|2026-11-15|" +
            "控制台 ulx 写法：! 开头的指令都能改用 ulx 执行，切一档自动换成 ulx xxx~" +
            "在线版本库：上传 / 列表 / 下载 / 删除版本，上传者由密钥识别~" +
            "自动更新：启动时检查，点「立即更新」自动下载、校验、替换并重启，设置和自定义数据都保留~" +
            "条目编辑器支持独立的 ulx 与注解栏，编辑后注解不会再串进命令~" +
            "目标名含空格自动加引号；物品表大类去重；导入数据即时生效；导出默认存程序目录",
    };

    int AddLogBlock(Panel host, int y, string ver, string date, string[] lines, bool highlight, int w)
    {
        Font fVer = TrackFont("Microsoft YaHei UI", 10f, FontStyle.Bold);
        Font fLine = TrackFont("Microsoft YaHei UI", 9f, FontStyle.Regular);

        var head = new Label();
        head.Text = ver + (string.IsNullOrEmpty(date) ? "" : "    " + date);
        head.Font = fVer;
        head.ForeColor = highlight ? Color.FromArgb(20, 130, 70) : Color.FromArgb(40, 74, 120);
        head.SetBounds(10, y, w - 20, 22);
        head.UseMnemonic = false;
        host.Controls.Add(head);
        y += 24;

        for (int i = 0; i < lines.Length; i++)
        {
            if (string.IsNullOrEmpty(lines[i])) continue;
            var ln = new Label();
            ln.Text = "· " + lines[i];
            ln.Font = fLine;
            ln.ForeColor = highlight ? Color.FromArgb(30, 110, 60) : Color.FromArgb(86, 96, 112);
            Size sz = TextRenderer.MeasureText(ln.Text, fLine, new Size(w - 44, 4000),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            ln.SetBounds(14, y, w - 34, Math.Max(20, sz.Height + 4));
            ln.UseMnemonic = false;
            host.Controls.Add(ln);
            y += ln.Height + 2;
        }
        y += 8;
        return y;
    }

    void RenderChangelog()
    {
        if (changelogHost == null) return;
        try
        {
            changelogHost.SuspendLayout();
            changelogHost.Controls.Clear();
            int w = Math.Max(320, changelogHost.ClientSize.Width - (changelogHost.VerticalScroll.Visible ? 20 : 4));
            int y = 8;

            // 远端有更新时，把它的说明放在最上面
            if (!string.IsNullOrEmpty(updLatestVersion) && CompareVersion(updLatestVersion, APP_VERSION_NUM) > 0)
            {
                string[] nl = string.IsNullOrEmpty(updLatestNotes)
                    ? new string[] { "（对方没写更新说明）" }
                    : updLatestNotes.Replace("\r\n", "\n").Split('\n');
                y = AddLogBlock(changelogHost, y, "v" + updLatestVersion, "新版本，可下载", nl, true, w);
            }

            for (int i = 0; i < CHANGELOG.Length; i++)
            {
                string[] parts = CHANGELOG[i].Split('|');
                if (parts.Length < 3) continue;
                string ver = parts[0], date = parts[1];
                string[] lines = parts[2].Split('~');
                bool cur = (CompareVersion(ver, APP_VERSION_NUM) == 0);
                y = AddLogBlock(changelogHost, y, "v" + ver, date + (cur ? "    当前版本" : ""), lines, false, w);
            }

            changelogHost.AutoScrollMinSize = new Size(w + 16, y + 8);
            changelogHost.ResumeLayout();
        }
        catch { }
    }

    // ---------------- 关于 / 更新 ----------------
    int AddAboutSection(Panel host, string text, int x, int y, int w)
    {
        var lbl = new Label();
        lbl.Text = text;
        lbl.Font = TrackFont("Microsoft YaHei UI", 11f, FontStyle.Bold);
        lbl.ForeColor = Color.FromArgb(28, 60, 110);
        lbl.SetBounds(x, y, w, 26);
        lbl.UseMnemonic = false;
        host.Controls.Add(lbl);

        var line = new Panel();
        line.BackColor = Color.FromArgb(226, 234, 245);
        line.SetBounds(x, y + 27, w, 1);
        host.Controls.Add(line);
        return y + 38;
    }

    Label AddAboutText(Panel host, string text, int x, int y, int w, Font f, Color c, int h)
    {
        var lbl = new Label();
        lbl.Text = text;
        lbl.Font = f;
        lbl.ForeColor = c;
        lbl.SetBounds(x, y, w, h);
        lbl.UseMnemonic = false;
        host.Controls.Add(lbl);
        return lbl;
    }

    int AddAboutPara(Panel host, string text, int x, int y, int w, Font f, Color c)
    {
        Size sz = TextRenderer.MeasureText(text, f, new Size(w, 4000),
            TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        var lbl = new Label();
        lbl.Text = text;
        lbl.Font = f;
        lbl.ForeColor = c;
        lbl.SetBounds(x, y, w, Math.Max(22, sz.Height + 8));
        lbl.UseMnemonic = false;
        host.Controls.Add(lbl);
        return y + lbl.Height + 4;
    }

    TabPage BuildAboutPage()
    {
        var page = new TabPage("关于");
        page.BackColor = Color.White;

        var host = new Panel();
        host.Dock = DockStyle.Fill;
        host.AutoScroll = true;
        host.BackColor = Color.White;
        page.Controls.Add(host);

        Font fBody = TrackFont("Microsoft YaHei UI", 9.5f, FontStyle.Regular);
        Font fSmall = TrackFont("Microsoft YaHei UI", 9f, FontStyle.Regular);
        Font fHint = TrackFont("Microsoft YaHei UI", 9f, FontStyle.Italic);
        Color cBody = Color.FromArgb(58, 68, 84);
        Color cHint = Color.FromArgb(150, 158, 170);

        int x = 20, w = 700, y = 16;

        var t1 = new Label();
        t1.Text = "RX 工具箱";
        t1.Font = TrackFont("Microsoft YaHei UI", 17f, FontStyle.Bold);
        t1.ForeColor = Color.FromArgb(26, 58, 104);
        t1.SetBounds(x, y, w, 38);
        t1.UseMnemonic = false;
        host.Controls.Add(t1);
        y += 42;

        var t2 = new Label();
        t2.Text = APP_VERSION + " · 构建 " + APP_BUILD_DATE + " · 单文件绿色版，免安装";
        t2.Font = fBody;
        t2.ForeColor = Color.FromArgb(112, 124, 142);
        t2.SetBounds(x, y, w, 24);
        t2.UseMnemonic = false;
        host.Controls.Add(t2);
        y += 34;

        // ---------- 程序更新 ----------
        y = AddAboutSection(host, "程序更新", x, y, w);

        lblUpdState = AddAboutText(host, "尚未检查", x + 2, y, w - 4, fBody, cBody, 24);
        y += 30;

        btnUpdCheck = new Button();
        btnUpdCheck.Text = "检查更新";
        btnUpdCheck.FlatStyle = FlatStyle.Flat;
        btnUpdCheck.FlatAppearance.BorderColor = Color.FromArgb(180, 205, 238);
        btnUpdCheck.SetBounds(x + 2, y, 128, 34);
        btnUpdCheck.Click += delegate { CheckUpdate(true); };
        host.Controls.Add(btnUpdCheck);

        btnUpdOpen = new Button();
        btnUpdOpen.Text = "立即更新";
        btnUpdOpen.FlatStyle = FlatStyle.Flat;
        btnUpdOpen.FlatAppearance.BorderColor = Color.FromArgb(180, 205, 238);
        btnUpdOpen.SetBounds(x + 140, y, 128, 34);
        btnUpdOpen.Enabled = false;
        btnUpdOpen.Click += delegate { StartSelfUpdate(); };
        host.Controls.Add(btnUpdOpen);

        var btnUpdHelp = new Button();
        btnUpdHelp.Text = "?";
        btnUpdHelp.Font = TrackFont("Microsoft YaHei UI", 11f, FontStyle.Bold);
        btnUpdHelp.FlatStyle = FlatStyle.Flat;
        btnUpdHelp.FlatAppearance.BorderColor = Color.FromArgb(180, 205, 238);
        btnUpdHelp.SetBounds(x + 274, y, 32, 34);
        btnUpdHelp.Click += delegate { ShowUpdateHelp(); };
        var tipHelp = new ToolTip();
        tipHelp.SetToolTip(btnUpdHelp, "更新下载不保证稳定，点这里看说明");
        host.Controls.Add(btnUpdHelp);

        chkAutoUpdate = new CheckBox();
        chkAutoUpdate.Text = "启动时自动检查更新";
        chkAutoUpdate.Font = fSmall;
        chkAutoUpdate.SetBounds(x + 318, y + 6, 220, 24);
        chkAutoUpdate.Checked = autoUpdateOn;
        chkAutoUpdate.CheckedChanged += delegate
        {
            autoUpdateOn = chkAutoUpdate.Checked;
            SaveSettings();
        };
        host.Controls.Add(chkAutoUpdate);
        y += 46;

        AddAboutText(host, "更新日志", x + 2, y, 120, fSmall, Color.FromArgb(110, 122, 140), 22);
        y += 24;

        changelogHost = new Panel();
        changelogHost.BackColor = Color.FromArgb(248, 250, 253);
        changelogHost.BorderStyle = BorderStyle.FixedSingle;
        changelogHost.AutoScroll = true;
        changelogHost.SetBounds(x + 2, y, w - 10, 146);
        changelogHost.Resize += delegate { RenderChangelog(); };
        host.Controls.Add(changelogHost);
        RenderChangelog();
        y += 158;      // 与面板高度 146 对齐，别把「声明」标题压住

        // ---------- 高级 ----------
        y = AddAboutSection(host, "高级", x, y, w);
        chkDevTools = new CheckBox();
        chkDevTools.Text = "显示开发者工具（校对/提交指令表用）";
        chkDevTools.Font = fBody;
        chkDevTools.SetBounds(x + 2, y, 420, 24);
        chkDevTools.Checked = devToolsOn;
        chkDevTools.CheckedChanged += delegate
        {
            if (devToolsLoading) return;
            devToolsOn = chkDevTools.Checked;
            SaveSettings();
            ApplyDevToolsTab(devToolsOn);
        };
        host.Controls.Add(chkDevTools);
        y += 32;

        // ---------- 声明 ----------
        y = AddAboutSection(host, "声明", x, y, w);

        AddAboutText(host, "使用范围", x + 2, y, w - 4, fSmall, Color.FromArgb(110, 122, 140), 20);
        y += 22;
        y = AddAboutPara(host, "仅供 [CN]Neo RXBreach mio 服使用。数据不保证时效性，服务器改版后请以实测为准。",
            x + 2, y, w - 14, fBody, cBody);

        AddAboutText(host, "用途", x + 2, y, w - 4, fSmall, Color.FromArgb(110, 122, 140), 20);
        y += 22;
        y = AddAboutPara(host, "本工具主要面向 mio 服管理员，为日常查询与操作提供便利，部分数据含有管理 / 开发者指令。\n不建议把含有管理指令的版本在公开渠道传播。",
            x + 2, y, w - 14, fBody, cBody);

        AddAboutText(host, "授权", x + 2, y, w - 4, fSmall, Color.FromArgb(110, 122, 140), 20);
        y += 22;
        y = AddAboutPara(host, "完全免费，仅供学习交流使用。允许二次分发与修改（保留出处即可）；禁止商用、倒卖或以此盈利。",
            x + 2, y, w - 14, fBody, cBody);

        AddAboutText(host, "免责", x + 2, y, w - 4, fSmall, Color.FromArgb(110, 122, 140), 20);
        y += 22;
        y = AddAboutPara(host, "因使用本工具产生的任何直接或间接后果，作者不承担任何责任。游戏内行为以服务器规则为准，滥用指令导致的后果与作者无关。\n若本工具存在侵权、违反服务器规则的行为，请联系作者进行修改或删除处理。",
            x + 2, y, w - 14, fBody, cBody);
        y += 10;
        // ---------- 署名与鸣谢 ----------
        y = AddAboutSection(host, "署名与鸣谢", x, y, w);
        AddAboutText(host, "作者：暮嵐拂晓", x + 2, y, w - 4, fBody, cBody, 22);
        y += 24;
        AddAboutText(host, "合作开发者：王月qwe", x + 2, y, w - 4, fBody, cBody, 22);
        y += 24;
        AddAboutText(host, "特别鸣谢（指令列表来源参考）：王月qwe、1不错1、惜雨晨、梦瞳", x + 2, y, w - 4, fBody, cBody, 22);
        y += 24;
        AddAboutText(host, "感谢 CN RXSEND 社区众多开发者和管理员的用爱发电", x + 2, y, w - 4, fHint, cHint, 22);
        y += 26;


        // ---------- 反馈 ----------
        y = AddAboutSection(host, "反馈", x, y, w);
        AddAboutText(host, "指令有误、想加功能，直接发我标题截图即可。", x + 2, y, w - 4, fBody, cBody, 24);
        y += 26;
        AddAboutText(host, "联系方式：QQ 1578785769（加好友，或发邮件到 1578785769@qq.com）", x + 2, y, w - 4, fBody, cBody, 24);
        y += 40;

        host.AutoScrollMinSize = new Size(w + 40, y + 20);
        return page;
    }

    void ShowUpdateHelp()
    {
        string linkUrl = !string.IsNullOrEmpty(updLatestUrl) ? updLatestUrl : "";
        string linkInfo = updateUrl;
        var f = new Form();
        try
        {
            f.Text = "关于自动更新";
            f.FormBorderStyle = FormBorderStyle.FixedDialog;
            f.StartPosition = FormStartPosition.CenterParent;
            f.MinimizeBox = false;
            f.MaximizeBox = false;
            f.ClientSize = new Size(560, 340);
            f.Font = TrackFont("Microsoft YaHei UI", 9.5f, FontStyle.Regular);

            var tip = new Label();
            tip.SetBounds(14, 12, 532, 120);
            tip.ForeColor = Color.FromArgb(70, 80, 96);
            tip.Text = "更新源是个人自建的 Cloudflare 地址，不保证稳定，某些地区/时段可能打不开。\r\n\r\n" +
                       "自动更新会：下载 → 校验 → 替换 → 重启。替换失败会自动保留旧版本，\r\n" +
                       "不会出现程序打不开的情况。\r\n\r\n" +
                       "如果一直失败，可以用下面的地址手动下载，或到 mio 服管理大群群文件找最新版。";
            f.Controls.Add(tip);

            var lab1 = new Label();
            lab1.Text = "手动下载地址（可复制）：";
            lab1.SetBounds(14, 140, 300, 20);
            lab1.ForeColor = Color.FromArgb(110, 122, 140);
            f.Controls.Add(lab1);

            var box = new TextBox();
            box.Multiline = true;
            box.ReadOnly = true;
            box.ScrollBars = ScrollBars.Vertical;
            box.SetBounds(14, 162, 532, 76);
            box.Text = linkUrl.Length > 0
                ? (linkUrl + "\r\n\r\n更新信息：" + linkInfo)
                : ("还没检查到新版本，先点一次「检查更新」。\r\n更新信息地址：" + linkInfo);
            f.Controls.Add(box);

            var lab2 = new Label();
            lab2.Text = "当前版本：" + APP_VERSION + "（" + APP_VERSION_NUM + "）　　QQ：1578785769";
            lab2.SetBounds(14, 246, 460, 20);
            lab2.ForeColor = Color.FromArgb(110, 122, 140);
            f.Controls.Add(lab2);

            var bOpen = new Button();
            bOpen.Text = "打开链接";
            bOpen.SetBounds(14, 282, 110, 32);
            bOpen.Click += delegate
            {
                string u = linkUrl.Length > 0 ? linkUrl : linkInfo;
                try { using (var p = System.Diagnostics.Process.Start(u)) { } }
                catch { }
            };
            f.Controls.Add(bOpen);

            var bCopy = new Button();
            bCopy.Text = "复制地址";
            bCopy.SetBounds(132, 282, 110, 32);
            bCopy.Click += delegate
            {
                try { Clipboard.SetText(linkUrl.Length > 0 ? linkUrl : linkInfo); }
                catch { }
            };
            f.Controls.Add(bCopy);

            var bClose = new Button();
            bClose.Text = "关闭";
            bClose.SetBounds(432, 282, 110, 32);
            bClose.DialogResult = DialogResult.OK;
            f.Controls.Add(bClose);

            f.AcceptButton = bClose;
            f.CancelButton = bClose;
            f.ShowDialog(this);
        }
        finally
        {
            try { f.Dispose(); } catch { }
        }
    }

    void SetUpdState(string text, Color c)
    {
        if (lblUpdState == null) return;
        lblUpdState.Text = text;
        lblUpdState.ForeColor = c;
    }

    static string HttpGetText(string url, int timeoutMs)
    {
        try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; } catch { }
        var req = (HttpWebRequest)WebRequest.Create(url);
        req.Timeout = timeoutMs;
        req.ReadWriteTimeout = timeoutMs;
        req.UserAgent = "RXToolbox/" + APP_VERSION_NUM;
        req.Proxy = null;
        using (var resp = (HttpWebResponse)req.GetResponse())
        using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
            return sr.ReadToEnd();
    }

    static string JsonField(string json, string key)
    {
        if (string.IsNullOrEmpty(json)) return "";
        Match m = Regex.Match(json, "\"" + key + "\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
        if (!m.Success) return "";
        return Regex.Replace(m.Groups[1].Value, "\\\\(.)", delegate(Match mm)
        {
            string c = mm.Groups[1].Value;
            if (c == "n") return "\n";
            if (c == "r") return "\r";
            if (c == "t") return "\t";
            return c;
        });
    }

    static int CompareVersion(string a, string b)
    {
        string[] pa = (a ?? "").Split('.');
        string[] pb = (b ?? "").Split('.');
        for (int i = 0; i < 3; i++)
        {
            int va = 0, vb = 0;
            if (i < pa.Length) int.TryParse(Regex.Replace(pa[i], "[^0-9]", ""), out va);
            if (i < pb.Length) int.TryParse(Regex.Replace(pb[i], "[^0-9]", ""), out vb);
            if (va != vb) return va > vb ? 1 : -1;
        }
        return 0;
    }

    // ================= 自动更新（下载 → 校验 → 替换 → 重启） =================
    // 只替换 exe，同目录的 settings.ini / servers.txt / custom.txt / data-custom.txt 原样保留
    static string Sha256File(string path)
    {
        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var sha = new System.Security.Cryptography.SHA256Managed())
        {
            byte[] hash = sha.ComputeHash(fs);
            var sb = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("x2"));
            return sb.ToString();
        }
    }

    static bool CanWriteDir(string dir)
    {
        try
        {
            string probe = Path.Combine(dir, ".wtest_" + Guid.NewGuid().ToString("N"));
            File.WriteAllText(probe, "x");
            File.Delete(probe);
            return true;
        }
        catch { return false; }
    }

    // 失败时保留旧版本：先改名备份，再拷贝新文件并校验字节数，出错就回滚
    static void LaunchReplacer(string newExe, string targetExe, int selfPid, long expectSize)
    {
        string q = "'" + newExe.Replace("'", "''") + "'";
        string t = "'" + targetExe.Replace("'", "''") + "'";
        string bak = "'" + (targetExe + ".old").Replace("'", "''") + "'";
        string script =
            "$ErrorActionPreference='SilentlyContinue';\r\n" +
            "try { Wait-Process -Id " + selfPid + " -Timeout 45 } catch { }\r\n" +
            "Start-Sleep -Milliseconds 500;\r\n" +
            "Remove-Item -LiteralPath " + bak + " -Force;\r\n" +
            "Move-Item -LiteralPath " + t + " -Destination " + bak + " -Force;\r\n" +
            "Copy-Item -LiteralPath " + q + " -Destination " + t + " -Force;\r\n" +
            "$ok = $false;\r\n" +
            "if (Test-Path -LiteralPath " + t + ") { $ok = ((Get-Item -LiteralPath " + t + ").Length -eq " + expectSize + ") }\r\n" +
            "if (-not $ok) {\r\n" +
            "  Remove-Item -LiteralPath " + t + " -Force;\r\n" +
            "  if (Test-Path -LiteralPath " + bak + ") { Move-Item -LiteralPath " + bak + " -Destination " + t + " -Force }\r\n" +
            "} else {\r\n" +
            "  Remove-Item -LiteralPath " + bak + " -Force;\r\n" +
            "}\r\n" +
            "Start-Sleep -Milliseconds 200;\r\n" +
            "Remove-Item -LiteralPath " + q + " -Force;\r\n" +
            "if (Test-Path -LiteralPath " + t + ") { Start-Process -FilePath " + t + " };\r\n";
        string b64 = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        var psi = new System.Diagnostics.ProcessStartInfo();
        psi.FileName = "powershell.exe";
        psi.Arguments = "-NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -EncodedCommand " + b64;
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        System.Diagnostics.Process.Start(psi);
    }

    void StartSelfUpdate()
    {
        string u = !string.IsNullOrEmpty(updLatestUrl) ? updLatestUrl : updLatestFallback;
        if (string.IsNullOrEmpty(u)) { ShowUpdateHelp(); return; }

        string exePath = Application.ExecutablePath;
        string dir = Path.GetDirectoryName(exePath);

        // 目录不可写（装在 Program Files 之类）→ 只能退回浏览器下载
        if (string.IsNullOrEmpty(dir) || !CanWriteDir(dir))
        {
            try { using (var p = System.Diagnostics.Process.Start(u)) { } }
            catch { ShowUpdateHelp(); }
            return;
        }

        if (MessageBox.Show(this,
                "更新到 v" + updLatestVersion + "？\r\n\r\n" +
                "程序会自动下载、校验、替换并重启，你的设置和自定义数据都会保留。",
                "自动更新", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
            return;
        if (updBusy) return;
        updBusy = true;
        SetUpdState("正在下载新版本…", Color.FromArgb(60, 90, 140));
        if (btnUpdOpen != null) btnUpdOpen.Enabled = false;

        string sha = updLatestSha;
        string tmp = Path.Combine(Path.GetTempPath(), "RXToolbox_upd_" + Guid.NewGuid().ToString("N") + ".exe");
        int pid = System.Diagnostics.Process.GetCurrentProcess().Id;

        var th = new Thread(delegate()
        {
            string err = null;
            try
            {
                using (var wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "RXToolbox-Updater");
                    wc.DownloadFile(u, tmp);
                }
                if (!string.IsNullOrEmpty(sha))
                {
                    string got = Sha256File(tmp);
                    if (!string.Equals(got, sha, StringComparison.OrdinalIgnoreCase))
                        throw new Exception("文件校验不一致（可能下载不完整）");
                }
            }
            catch (Exception ex) { err = ex.Message; }

            BeginInvoke((MethodInvoker)delegate
            {
                updBusy = false;
                if (err != null)
                {
                    SetUpdState("自动更新失败：" + err + "，改为打开浏览器下载", Color.FromArgb(184, 88, 44));
                    if (btnUpdOpen != null) btnUpdOpen.Enabled = true;
                    try { using (var p = System.Diagnostics.Process.Start(u)) { } }
                    catch { ShowUpdateHelp(); }
                    return;
                }
                try
                {
                    long expect = 0;
                    try { expect = new FileInfo(tmp).Length; } catch { }
                    LaunchReplacer(tmp, exePath, pid, expect);
                }
                catch (Exception ex2)
                {
                    SetUpdState("启动替换失败：" + ex2.Message + "，改为打开浏览器下载", Color.FromArgb(184, 88, 44));
                    if (btnUpdOpen != null) btnUpdOpen.Enabled = true;
                    try { using (var p = System.Diagnostics.Process.Start(u)) { } }
                    catch { ShowUpdateHelp(); }
                    return;
                }
                SetUpdState("下载完成，正在重启更新…", Color.FromArgb(20, 130, 70));
                Application.Exit();
            });
        });
        th.IsBackground = true;
        th.Start();
    }

    void CheckUpdate(bool manual)
    {
        if (updBusy) return;
        if (string.IsNullOrEmpty(updateUrl))
        {
            SetUpdState("未配置更新地址（当前版本不会联网）", Color.FromArgb(120, 128, 140));
            return;
        }
        updBusy = true;
        if (manual)
        {
            SetUpdState("正在检查…", Color.FromArgb(60, 90, 140));
            if (btnUpdCheck != null) btnUpdCheck.Enabled = false;
        }
        string url = updateUrl;
        updThread = new Thread(delegate()
        {
            string json = null, err = null;
            // 冷启动 / CDN 抖一下很常见，失败后隔 600ms 再试一次，超时放宽到 12 秒
            try { json = HttpGetText(url, 12000); }
            catch (Exception)
            {
                try
                {
                    System.Threading.Thread.Sleep(600);
                    json = HttpGetText(url, 12000);
                }
                catch (Exception ex2) { err = ex2.GetType().Name; }
            }

            string ver = JsonField(json, "version");
            string dl = JsonField(json, "url");
            string fb = JsonField(json, "fallback");
            string notes = JsonField(json, "notes");
            string sha = JsonField(json, "sha256");
            // JsonField 已把 \n 还原成真换行，这里不再处理

            try
            {
                BeginInvoke((MethodInvoker)delegate
                {
                    updBusy = false;
                    if (btnUpdCheck != null) btnUpdCheck.Enabled = true;

                    if (err != null)
                    {
                        if (manual) SetUpdState("检查失败（" + err + "）：网络不通或服务器没响应，再点一次「检查更新」试试", Color.FromArgb(184, 88, 44));
                        return;
                    }
                    if (string.IsNullOrEmpty(ver))
                    {
                        if (manual) SetUpdState("更新信息格式不对，稍后再试", Color.FromArgb(184, 88, 44));
                        return;
                    }

                    updLatestVersion = ver;
                    updLatestUrl = dl;
                    updLatestFallback = fb;
                    updLatestSha = sha;

                    if (CompareVersion(ver, APP_VERSION_NUM) > 0)
                    {
                        SetUpdState("发现新版本 v" + ver + "（当前 " + APP_VERSION_NUM + "）", Color.FromArgb(20, 130, 70));
                        if (btnUpdOpen != null) btnUpdOpen.Enabled = true;
                        updLatestNotes = notes;
                        RenderChangelog();
                        if (toast != null) toast.ShowText("发现新版本 v" + ver + "，去「关于」页看看");
                    }
                    else
                    {
                        SetUpdState("已是最新版本（" + APP_VERSION_NUM + "）", Color.FromArgb(60, 110, 90));
                        if (btnUpdOpen != null) btnUpdOpen.Enabled = false;
                        if (manual && !string.IsNullOrEmpty(notes)) updLatestNotes = notes;
                    }
                });
            }
            catch { }
        });
        updThread.IsBackground = true;
        updThread.Start();
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

    static string UlxOf(string body, string explicitUlx)
    {
        if (!string.IsNullOrEmpty(explicitUlx))
        {
            // 兜底：表里若把一条 ! 命令塞进 ulx 槽，也统一成控制台写法
            if (explicitUlx.Length > 1 && explicitUlx[0] == '!')
                return "ulx " + explicitUlx.Substring(1);
            return explicitUlx;
        }
        if (!string.IsNullOrEmpty(body) && body.Length > 1 && body[0] == '!')
            return "ulx " + body.Substring(1);
        return body;
    }

    public static int SelfTest()
    {
        int pass = 0, fail = 0;
        var sb = new StringBuilder();
        // 数据在 exe 旁边就先生效，方便跑数据体检
        try
        {
            string d = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "phrases.json");
            Data.Load(d);
        }
        catch { }
        sb.AppendLine("RX 工具箱自检 " + APP_VERSION + " " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        sb.AppendLine("数据：" + (Data.Loaded ? Data.SourcePath : "未找到 phrases.json（数据相关断言会跳过）"));
        sb.AppendLine();

        Action<string, bool, string> chk = delegate(string name, bool ok, string detail)
        {
            if (ok) { pass++; sb.AppendLine("  PASS  " + name); }
            else { fail++; sb.AppendLine("  FAIL  " + name + "    " + detail); }
        };

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

        // ---- 4. JSON 转义 ----
        string raw = "引号\"反斜杠\\换行\n制表\t中文";
        string esc = JsonEsc(raw);
        chk("JSON 往返", JsonUnesc(esc) == raw, "得到 " + Esc0(JsonUnesc(esc)));
        chk("JSON 字段读取", JsonField("{\"a\":\"x\\ny\",\"b\":2}", "a") == "x\ny", "");

        // ---- 5. 版本比较 ----
        chk("版本 1.6.1 > 1.6.0", CompareVersion("1.6.1", "1.6.0") > 0, "");
        chk("版本 1.6.10 > 1.6.9", CompareVersion("1.6.10", "1.6.9") > 0, "");
        chk("版本 1.6.0 == 1.6.0", CompareVersion("1.6.0", "1.6.0") == 0, "");
        chk("版本 1.5.9 < 1.6.0", CompareVersion("1.5.9", "1.6.0") < 0, "");


                // ---- 5.4 目标名替换 ----
        chk("目标名：普通替换", ApplyTargetName("!strip ^", "暮嵐") == "!strip 暮嵐", ApplyTargetName("!strip ^", "暮嵐"));
        chk("目标名：含空格加引号", ApplyTargetName("!strip ^", "John Smith") == "!strip \"John Smith\"", ApplyTargetName("!strip ^", "John Smith"));
        chk("目标名：已带引号不重复加", ApplyTargetName("!strip ^", "\"A B\"") == "!strip \"A B\"", "");
        chk("目标名：留空不动", ApplyTargetName("!strip ^", "  ") == "!strip ^", "");
        chk("目标名：没有 ^ 不动", ApplyTargetName("stopsound", "X") == "stopsound", "");

        // ---- 5.5 条目编辑器（PromptItem 走 DevSplitItem/DevJoinItem）----
                string editRaw = "带注解条目" + "\u0001" + "!cmd ^" + "\u0002" + "\u0003" + "这是注解";
                string et, em, ea, eu, en;
                DevSplitItem(editRaw, out et, out em, out ea, out eu, out en);
                chk("编辑器：读得到注解", en == "这是注解", "得到 " + Esc0(en));
                chk("编辑器：命令没被注解污染", em == "!cmd ^", "得到 " + Esc0(em));
                chk("编辑器：原样存回", DevJoinItem(et, em, ea, eu, en) == editRaw, "不一致");

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

        sb.AppendLine();
        sb.AppendLine(string.Format("结果：{0} 通过，{1} 失败", pass, fail));

        try
        {
            string dir = Path.GetDirectoryName(Application.ExecutablePath);
            File.WriteAllText(Path.Combine(dir, "selftest.log"), sb.ToString(), new UTF8Encoding(true));
        }
        catch { }
        Console.WriteLine(sb.ToString());
        return fail;
    }

    static string Esc0(string s)
    {
        if (s == null) return "null";
        return s.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
    }

    // ---------------- 在线版本库（Pages Functions + KV） ----------------
    static string JsonEsc(string s)
    {
        if (s == null) return "";
        var sb = new StringBuilder(s.Length + 16);
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '"') sb.Append("\\\"");
            else if (c == '\\') sb.Append("\\\\");
            else if (c == '\n') sb.Append("\\n");
            else if (c == '\r') sb.Append("\\r");
            else if (c == '\t') sb.Append("\\t");
            else if (c < ' ') sb.Append(' ');
            else sb.Append(c);
        }
        return sb.ToString();
    }

    static string JsonUnesc(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '\\' && i + 1 < s.Length)
            {
                char n = s[i + 1];
                if (n == 'n') { sb.Append('\n'); i++; continue; }
                if (n == 'r') { sb.Append('\r'); i++; continue; }
                if (n == 't') { sb.Append('\t'); i++; continue; }
                if (n == '"') { sb.Append('"'); i++; continue; }
                if (n == '\\') { sb.Append('\\'); i++; continue; }
                if (n == 'u' && i + 5 < s.Length)
                {
                    int code;
                    if (int.TryParse(s.Substring(i + 2, 4), System.Globalization.NumberStyles.HexNumber,
                        System.Globalization.CultureInfo.InvariantCulture, out code))
                    {
                        sb.Append((char)code);
                        i += 5;
                        continue;
                    }
                }
            }
            sb.Append(c);
        }
        return sb.ToString();
    }

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

    List<string[]> ParseHubList(string json)
    {
        var rows = new List<string[]>();
        if (string.IsNullOrEmpty(json)) return rows;
        MatchCollection ms = Regex.Matches(json, "\\{[^{}]*\\}");
        for (int i = 0; i < ms.Count; i++)
        {
            string o = ms[i].Value;
            string id = JsonField(o, "id");
            if (string.IsNullOrEmpty(id)) continue;
            string ts = JsonField(o, "ts");
            string when = ts;
            try
            {
                DateTime dt = DateTime.Parse(ts, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal);
                when = dt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            }
            catch { }
            string szStr = "";
            int sp = o.IndexOf("\"size\":");
            if (sp >= 0)
            {
                int k = sp + 7;
                while (k < o.Length && !char.IsDigit(o[k])) k++;
                int st = k;
                while (k < o.Length && char.IsDigit(o[k])) k++;
                if (k > st) szStr = o.Substring(st, k - st);
            }
            rows.Add(new string[] {
                when,
                JsonField(o, "author"),
                JsonField(o, "note"),
                szStr,
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

    void DevHubRefresh()
    {
        try
        {
            string json = HubCall("/list", "GET", null);
            List<string[]> rows = ParseHubList(json);
            if (lvVersions == null) return;
            lvVersions.BeginUpdate();
            lvVersions.Items.Clear();
            for (int i = 0; i < rows.Count; i++)
            {
                var it = new ListViewItem(rows[i][0]);
                it.SubItems.Add(rows[i][1]);
                it.SubItems.Add(rows[i][2]);
                string sz = rows[i][3];
                int n;
                if (int.TryParse(sz, out n)) sz = (n / 1024) + " KB";
                it.SubItems.Add(sz);
                it.Tag = rows[i][4];
                lvVersions.Items.Add(it);
            }
            lvVersions.EndUpdate();
            DevSetStatus("服务器上共 " + rows.Count + " 个版本。选中一行点「下载并应用」。");
        }
        catch (Exception ex)
        {
            DevSetStatus("拉取列表失败：" + ex.Message + "\r\n（检查服务器地址和密钥是否填对）");
        }
    }

    void DevHubUpload()
    {
        if (string.IsNullOrEmpty(hubKey))
        {
            DevSetStatus("还没填密钥，先在「密钥」框里填上再上传。");
            return;
        }
        // 上传者由服务器按密钥绑定，客户端不再询问
        string note = PromptText("上传到服务器", "这次改了什么（一句话，可留空）：", "");
        if (note == null) note = "";
        try
        {
            string data = BuildDataSnapshot(false, "", note);
            string body = "{\"note\":\"" + JsonEsc(note) +
                          "\",\"data\":\"" + JsonEsc(data) + "\"}";
            string resp = HubCall("/submit", "POST", body);
            if (resp.IndexOf("\"ok\":true", StringComparison.Ordinal) >= 0)
            {
                string who = JsonField(resp, "author");
                DevSetStatus("已上传（上传者：" + (who.Length > 0 ? who : "未识别") + "）：" + JsonField(resp, "id") + "\r\n列表十几秒后才刷得出来（KV 有缓存）。");
            }
            else
            {
                DevSetStatus("上传失败：" + resp);
            }
        }
        catch (Exception ex)
        {
            DevSetStatus("上传失败：" + HubErrorText(ex));
        }
    }

    void DevHubDownload()
    {
        if (lvVersions == null || lvVersions.SelectedItems.Count == 0)
        {
            DevSetStatus("先在列表里选一个版本。");
            return;
        }
        string id = Convert.ToString(lvVersions.SelectedItems[0].Tag);
        try
        {
            string resp = HubCall("/get?id=" + Uri.EscapeDataString(id), "GET", null);
            string data = JsonUnesc(JsonField(resp, "data"));
            if (string.IsNullOrEmpty(data))
            {
                DevSetStatus("这个版本拿不到数据：" + resp);
                return;
            }
            string meta;
            var parsed = ParseDataSnapshot(data, out meta);
            if (parsed.Count == 0)
            {
                DevSetStatus("数据解析失败（格式不对）。");
                return;
            }
            var sb = new StringBuilder();
            foreach (var kv in parsed) sb.Append("· ").Append(kv.Key).Append("：").Append(kv.Value.Count).Append(" 条\r\n");
            if (MessageBox.Show(this, meta + "\r\n" + sb + "\r\n把它应用为本机数据？（会覆盖 data-custom.txt）",
                "下载并应用", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
            ApplyDataOverrides(parsed);
            DevSetStatus("已应用版本 " + id + "，已即时生效。");
        }
        catch (Exception ex)
        {
            DevSetStatus("下载失败：" + HubErrorText(ex));
        }
    }

    void DevHubDelete()
    {
        if (lvVersions == null || lvVersions.SelectedItems.Count == 0)
        {
            DevSetStatus("先在列表里选一个版本。");
            return;
        }
        var it = lvVersions.SelectedItems[0];
        string id = Convert.ToString(it.Tag);
        string who = it.SubItems.Count > 1 ? it.SubItems[1].Text : "";
        string note = it.SubItems.Count > 2 ? it.SubItems[2].Text : "";
        string ask = "确定从服务器删除这个版本吗？\r\n\r\n" + it.Text + "　" + who + "\r\n" + note +
                     "\r\n\r\n删了就找不回来了（服务器和所有人的列表都会消失）。";
        if (MessageBox.Show(this, ask, "删除版本", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK)
            return;
        try
        {
            string resp = HubCall("/del?id=" + Uri.EscapeDataString(id), "POST", "");
            if (resp.IndexOf("\"ok\":true", StringComparison.Ordinal) >= 0)
            {
                lvVersions.Items.Remove(it);
                DevSetStatus("已删除版本：" + id);
            }
            else if (resp.IndexOf("forbidden", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                DevSetStatus("这个密钥没有删除权限（只有作者密钥可以删）。");
            }
            else
            {
                DevSetStatus("删除失败：" + resp);
            }
        }
        catch (Exception ex)
        {
            DevSetStatus("删除失败：" + HubErrorText(ex));
        }
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
                else if (k == "HotkeysOff") hotkeysOff = (v == "1");
                else if (k == "HubUrl") hubUrl = v;
                else if (k == "HubKey") hubKey = v;
            }
        }
        catch { }
    }

    void SaveHubSettings()
    {
        try
        {
            var sb = new StringBuilder();
            if (File.Exists(settingsPath))
            {
                foreach (string line in File.ReadAllLines(settingsPath, Encoding.UTF8))
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0) { sb.AppendLine(line); continue; }
                    string k = line.Substring(0, eq).Trim();
                    if (k == "HubUrl" || k == "HubKey") continue;
                    sb.AppendLine(line);
                }
            }
            sb.AppendLine("HubUrl=" + hubUrl);
            sb.AppendLine("HubKey=" + hubKey);
            AtomicWriteText(settingsPath, sb.ToString());
        }
        catch { }
    }

    // ---------------- 开发者工具 ----------------
    // 数据快照文件格式（Tab 分隔，Excel 可直接打开）：
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

        // 载入 phrases.json；文件不存在返回 false（界面会显示空表）
        public static bool Load(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
                string text = File.ReadAllText(path, Encoding.UTF8);
                object rootObj = MiniJson.Parse(text);
                var root = rootObj as Dictionary<string, object>;
                if (root == null) return false;

                Misc = Build(root, "杂项");
                Command = Build(root, "指令表");
                Ability = Build(root, "技能表");
                Weapon = Build(root, "物品表");
                Job = Build(root, "角色表");
                Scp = Build(root, "SCP角色");
                Model = Build(root, "模型表");
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
            }
            return d;
        }

        static List<object> ParseArray(string s, ref int i)
        {
            var list = new List<object>();
            i++;                     // [
            while (true)
            {
                SkipWs(s, ref i);
                if (i >= s.Length) break;
                if (s[i] == ']') { i++; break; }
                if (s[i] == ',') { i++; continue; }
                list.Add(ParseValue(s, ref i));
                SkipWs(s, ref i);
                if (i < s.Length && s[i] == ']') { i++; break; }
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
        return s.Replace("\r\n", " ").Replace("\n", " ").Replace("\t", " ").Trim();
    }

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
        txtDevStatus.Text = text;
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
                note = PromptText("生成本次提交", "这次改了什么（一句话，可留空）：", "");
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
                (submission ? "把这个文件发给作者即可。" : "可以用 Excel 打开编辑，改完再用「导入数据」。"));
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
                DevSetStatus("这个文件里没有识别到任何数据表。\r\n确认是「导出数据 / 生成提交包」生成的 .rxdata 文件。");
                return;
            }

            // 统计差异
            var report = new StringBuilder();
            int totalNew = 0, totalDel = 0, totalChg = 0;
            foreach (var kv in parsed)
            {
                int idx = Array.IndexOf(dataTabLabels, kv.Key);
                if (idx < 0) { report.Append("· ").Append(kv.Key).Append("：本程序没有这个表，已跳过\r\n"); continue; }
                // 用“标题+第几次出现”做 key，标题重名也不会串
                var before = new Dictionary<string, string>();
                var seen = new Dictionary<string, int>();
                foreach (string it in dataTabLists[idx])
                {
                    string t, m, a, u, n;
                    DevSplitItem(it, out t, out m, out a, out u, out n);
                    int c0;
                    seen.TryGetValue(t, out c0);
                    seen[t] = c0 + 1;
                    before[t + "#" + c0] = DevJoinItem(t, m, a, u, n);
                }
                int add = 0, del = 0, chg = 0;
                var after = new Dictionary<string, string>();
                var seen2 = new Dictionary<string, int>();
                foreach (string it in kv.Value)
                {
                    string t, m, a, u, n;
                    DevSplitItem(it, out t, out m, out a, out u, out n);
                    int c0;
                    seen2.TryGetValue(t, out c0);
                    seen2[t] = c0 + 1;
                    string key = t + "#" + c0;
                    after[key] = DevJoinItem(t, m, a, u, n);
                    string oldCmd;
                    if (!before.TryGetValue(key, out oldCmd)) add++;
                    else if (oldCmd != DevJoinItem(t, m, a, u, n)) chg++;
                }
                foreach (var b in before) if (!after.ContainsKey(b.Key)) del++;
                totalNew += add; totalDel += del; totalChg += chg;
                report.Append("· ").Append(kv.Key).Append("：共 ").Append(kv.Value.Count)
                      .Append(" 条，新增 ").Append(add).Append("、修改 ").Append(chg)
                      .Append("、删除 ").Append(del).Append("\r\n");
            }

            string msg = (meta.Length > 0 ? meta + "\r\n\r\n" : "") + report +
                "\r\n合计：新增 " + totalNew + "，修改 " + totalChg + "，删除 " + totalDel + "\r\n\r\n" +
                "应用后会覆盖 data-custom.txt，并立刻刷新对应页面。要继续吗？";
            if (MessageBox.Show(this, msg, "导入预览", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK)
                return;

            ApplyDataOverrides(parsed);
            DevSetStatus(report.ToString() + "\r\n已写入 data-custom.txt，并即时刷新了页面。");
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

    string PromptText(string title, string label, string def)
    {
        using (var f = new Form())
        {
            f.Text = title;
            f.FormBorderStyle = FormBorderStyle.FixedDialog;
            f.StartPosition = FormStartPosition.CenterParent;
            f.MaximizeBox = false;
            f.MinimizeBox = false;
            f.ClientSize = new Size(400, 130);
            var lb = new Label();
            lb.Text = label;
            lb.SetBounds(12, 12, 376, 22);
            f.Controls.Add(lb);
            var tb = new TextBox();
            tb.Text = def ?? "";
            tb.SetBounds(12, 38, 376, 24);
            f.Controls.Add(tb);
            var ok = new Button();
            ok.Text = "确定";
            ok.DialogResult = DialogResult.OK;
            ok.SetBounds(212, 80, 80, 30);
            f.Controls.Add(ok);
            var cancel = new Button();
            cancel.Text = "取消";
            cancel.DialogResult = DialogResult.Cancel;
            cancel.SetBounds(300, 80, 80, 30);
            f.Controls.Add(cancel);
            f.AcceptButton = ok;
            f.CancelButton = cancel;
            return f.ShowDialog(this) == DialogResult.OK ? tb.Text.Trim() : null;
        }
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
        tip.Text = "本地流程：用「编辑条目」或 Excel 改数据 → 生成提交包 → 发给作者。\r\n" +
                   "在线流程：填好服务器地址和密钥 → 上传，另一台机器在列表里挑版本下载。";
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

        lvVersions = new ListView();
        lvVersions.View = View.Details;
        lvVersions.FullRowSelect = true;
        lvVersions.HideSelection = false;
        lvVersions.Font = fHint;
        lvVersions.SetBounds(x, y, w, 168);
        lvVersions.Columns.Add("时间", 132);
        lvVersions.Columns.Add("提交人", 110);
        lvVersions.Columns.Add("备注", 320);
        lvVersions.Columns.Add("大小", 70);
        lvVersions.DoubleClick += delegate { DevHubDownload(); };
        host.Controls.Add(lvVersions);
        y += 178;

        txtDevStatus = new TextBox();
        txtDevStatus.Multiline = true;
        txtDevStatus.ReadOnly = true;
        txtDevStatus.ScrollBars = ScrollBars.Vertical;
        txtDevStatus.BorderStyle = BorderStyle.FixedSingle;
        txtDevStatus.BackColor = Color.FromArgb(248, 250, 253);
        txtDevStatus.Font = fHint;
        txtDevStatus.SetBounds(x, y, w, 120);
        txtDevStatus.Text = "（这里显示导出/导入/上传的结果）";
        host.Controls.Add(txtDevStatus);
        y += 132;

        host.AutoScrollMinSize = new Size(w + 60, y + 20);
        return page;
    }

    TabPage BuildToolPage()
    {
        var page = new TabPage("工具");
        page.BackColor = Color.White;
        Font ui = TrackFont("Microsoft YaHei UI", 9.5f, FontStyle.Regular);

        var bJoin = new Button();
        bJoin.Text = "挤服助手";
        bJoin.FlatStyle = FlatStyle.Flat;
        bJoin.FlatAppearance.BorderColor = Color.FromArgb(180, 205, 238);
        bJoin.SetBounds(12, 10, 128, 36);
        page.Controls.Add(bJoin);

        var bCross = new Button();
        bCross.Text = "屏幕准星";
        bCross.FlatStyle = FlatStyle.Flat;
        bCross.FlatAppearance.BorderColor = Color.FromArgb(180, 205, 238);
        bCross.SetBounds(148, 10, 128, 36);
        page.Controls.Add(bCross);

        bLog = new Button();
        bLog.Text = "查看日志";
        bLog.SetBounds(284, 10, 96, 36);
        bLog.Click += delegate
        {
            try
            {
                if (!string.IsNullOrEmpty(Native.LogPath) && File.Exists(Native.LogPath))
                {
                    using (var proc = System.Diagnostics.Process.Start(Native.LogPath)) { }
                }
                else
                {
                    toast.ShowText("还没有日志文件");
                }
            }
            catch (Exception ex) { toast.ShowText("打不开日志：" + ex.Message); }
        };
        page.Controls.Add(bLog);

        // 关闭热键：把 F5/F6… 还给系统（按钮本身也有 Ctrl+F10 开关）
        btnHotkeyOff = new Button();
        btnHotkeyOff.Text = "关闭热键 (" + ToggleKeyName() + ")";
        btnHotkeyOff.FlatStyle = FlatStyle.Flat;
        btnHotkeyOff.SetBounds(500, 10, 176, 36);
        btnHotkeyOff.ForeColor = Color.FromArgb(60, 90, 140);
        btnHotkeyOff.FlatAppearance.BorderColor = Color.FromArgb(180, 205, 238);
        btnHotkeyOff.Click += delegate { ToggleHotkeys(); };
        page.Controls.Add(btnHotkeyOff);

        btnHotkey = new Button();
        btnHotkey.Text = "热键设置";
        btnHotkey.SetBounds(388, 10, 104, 36);
        btnHotkey.Click += delegate { ShowHotkeyDialog(); };
        page.Controls.Add(btnHotkey);

        // ================= 挤服面板 =================
        var pJoin = new Panel();
        pJoin.SetBounds(8, 54, page.ClientSize.Width - 16, page.ClientSize.Height - 62);
        pJoin.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        page.Controls.Add(pJoin);

        btnAuto = new Button();
        btnAuto.Text = "▶ 开始挤服 (" + KeyName(2) + ")";
        btnAuto.Font = TrackFont("Microsoft YaHei UI", 10f, FontStyle.Bold);
        btnAuto.BackColor = Color.FromArgb(224, 240, 255);
        btnAuto.ForeColor = Color.FromArgb(12, 62, 130);
        btnAuto.FlatStyle = FlatStyle.Flat;
        btnAuto.FlatAppearance.BorderColor = Color.FromArgb(140, 185, 240);
        btnAuto.Cursor = Cursors.Hand;
        btnAuto.SetBounds(8, 4, 176, 38);
        btnAuto.Click += delegate { ToggleStart(); };
        pJoin.Controls.Add(btnAuto);

        var lblMode = new Label();
        lblMode.Text = "模式：";
        lblMode.SetBounds(194, 13, 48, 22);
        pJoin.Controls.Add(lblMode);

        rbTapMode = new RadioButton();
        rbTapMode.Text = "手动档";
        rbTapMode.SetBounds(242, 11, 86, 24);
        rbTapMode.Checked = true;
        rbTapMode.CheckedChanged += delegate { if (rbTapMode.Checked) OnModeChanged(); };
        pJoin.Controls.Add(rbTapMode);

        rbOneIp = new RadioButton();
        rbOneIp.Text = "单一 IP";
        rbOneIp.SetBounds(330, 11, 90, 24);
        rbOneIp.CheckedChanged += delegate { if (rbOneIp.Checked) OnModeChanged(); };
        pJoin.Controls.Add(rbOneIp);

        rbPollMode = new RadioButton();
        rbPollMode.Text = "收藏轮询";
        rbPollMode.SetBounds(422, 11, 98, 24);
        rbPollMode.CheckedChanged += delegate { if (rbPollMode.Checked) OnModeChanged(); };
        pJoin.Controls.Add(rbPollMode);

        btnGuide = new Button();
        btnGuide.Text = "?";
        btnGuide.Font = TrackFont("Microsoft YaHei UI", 9f, FontStyle.Bold);
        btnGuide.SetBounds(524, 10, 26, 26);
        btnGuide.Click += delegate { ShowGuide(); };
        pJoin.Controls.Add(btnGuide);

        var tipGuide = new ToolTip(uiComponents);
        tipGuide.InitialDelay = 300;
        tipGuide.SetToolTip(btnGuide, "使用指南：按当前模式查看分步操作与原理说明");

        var lblSpeed = new Label();
        lblSpeed.Text = "间隔：";
        lblSpeed.SetBounds(560, 13, 46, 22);
        pJoin.Controls.Add(lblSpeed);

        cmbSpeed = new ComboBox();
        cmbSpeed.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbSpeed.Items.AddRange(TapIntervals);
        cmbSpeed.SelectedIndex = 1;
        cmbSpeed.SetBounds(606, 10, 92, 26);
        cmbSpeed.SelectedIndexChanged += delegate { SaveSettings(); RefreshUi(true); };
        pJoin.Controls.Add(cmbSpeed);

        chkOcr = new CheckBox();
        chkOcr.Text = "OCR 识别进服后自动停";
        chkOcr.Checked = true;
        chkOcr.SetBounds(712, 12, 210, 24);
        chkOcr.CheckedChanged += delegate
        {
            SaveSettings();
            if (!chkOcr.Checked) tapOcrFail = 0;
            RefreshUi(true);
        };
        pJoin.Controls.Add(chkOcr);

        lblListHint = new Label();
        lblListHint.Text = "服务器列表：单击选中＝单一 IP 目标　·　勾选＝参与收藏轮询（点表头可按空位排序）";
        lblListHint.ForeColor = Color.FromArgb(70, 90, 110);
        lblListHint.SetBounds(8, 52, 540, 22);
        pJoin.Controls.Add(lblListHint);

        btnCheckAll = new Button();
        btnCheckAll.Text = "全选";
        btnCheckAll.SetBounds(680, 46, 64, 28);
        btnCheckAll.Click += delegate { SetAllChecks(true); };
        pJoin.Controls.Add(btnCheckAll);

        btnUncheckAll = new Button();
        btnUncheckAll.Text = "全不选";
        btnUncheckAll.SetBounds(750, 46, 74, 28);
        btnUncheckAll.Click += delegate { SetAllChecks(false); };
        pJoin.Controls.Add(btnUncheckAll);

        btnRefreshFav = new Button();
        btnRefreshFav.Text = "↻ 刷新人数";
        btnRefreshFav.BackColor = Color.FromArgb(236, 244, 255);
        btnRefreshFav.FlatStyle = FlatStyle.Flat;
        btnRefreshFav.FlatAppearance.BorderColor = Color.FromArgb(170, 200, 238);
        btnRefreshFav.SetBounds(832, 46, 104, 28);
        btnRefreshFav.Click += delegate { RefreshAllFavs(); };
        pJoin.Controls.Add(btnRefreshFav);

        lvSrv = new BufferedListView();
        lvSrv.View = View.Details;
        lvSrv.CheckBoxes = true;
        lvSrv.FullRowSelect = true;
        lvSrv.MultiSelect = true;
        lvSrv.HideSelection = false;
        lvSrv.HeaderStyle = ColumnHeaderStyle.Clickable;
        lvSrv.Columns.Add("备注名", 110);
        lvSrv.Columns.Add("真实服务器名（A2S 探测）", 370);
        lvSrv.Columns.Add("人数", 104, HorizontalAlignment.Right);
        lvSrv.Columns.Add("地址", 160);
        lvSrv.SetBounds(8, 80, 900, 290);
        lvSrv.ColumnClick += delegate(object snd, ColumnClickEventArgs ce) { SortSrvListByColumn(ce.Column); };
        lvSrv.SelectedIndexChanged += delegate
        {
            SyncActiveTargets();
            UpdateJoinHint();
            RefreshUi(false);
        };
        lvSrv.ItemCheck += delegate
        {
            if (suppressListCheck) return;
            try
            {
                BeginInvoke(new MethodInvoker(delegate
                {
                    SyncActiveTargets();
                    UpdateJoinHint();
                    RefreshUi(false);
                }));
            }
            catch { }
        };
        pJoin.Controls.Add(lvSrv);

        lblTapGuide = new Label();
        lblTapGuide.BackColor = Color.FromArgb(255, 252, 244);
        lblTapGuide.ForeColor = Color.FromArgb(120, 80, 10);
        lblTapGuide.BorderStyle = BorderStyle.FixedSingle;
        lblTapGuide.Padding = new Padding(14, 12, 14, 12);
        lblTapGuide.Font = TrackFont("Microsoft YaHei UI", 10f, FontStyle.Regular);
        lblTapGuide.SetBounds(8, 80, 900, 300);
        lblTapGuide.Visible = false;
        pJoin.Controls.Add(lblTapGuide);

        lblNewIp = new Label();
        lblNewIp.Text = "IP:端口";
        lblNewIp.SetBounds(8, 396, 54, 22);
        pJoin.Controls.Add(lblNewIp);

        txtNewIp = new TextBox();
        txtNewIp.SetBounds(64, 394, 176, 26);
        txtNewIp.KeyDown += delegate(object s, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter) { AddFavFromBox(); e.Handled = true; e.SuppressKeyPress = true; }
        };
        pJoin.Controls.Add(txtNewIp);

        lblNewAlias = new Label();
        lblNewAlias.Text = "备注:";
        lblNewAlias.SetBounds(248, 396, 40, 22);
        pJoin.Controls.Add(lblNewAlias);

        txtNewAlias = new TextBox();
        txtNewAlias.SetBounds(290, 394, 96, 26);
        txtNewAlias.KeyDown += delegate(object s, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter) { AddFavFromBox(); e.Handled = true; e.SuppressKeyPress = true; }
        };
        pJoin.Controls.Add(txtNewAlias);

        btnAddFav = new Button();
        btnAddFav.Text = "★ 加入收藏";
        btnAddFav.BackColor = Color.FromArgb(236, 244, 255);
        btnAddFav.FlatStyle = FlatStyle.Flat;
        btnAddFav.FlatAppearance.BorderColor = Color.FromArgb(170, 200, 238);
        btnAddFav.SetBounds(394, 392, 104, 30);
        btnAddFav.Click += delegate { AddFavFromBox(); };
        pJoin.Controls.Add(btnAddFav);

        btnPasteFav = new Button();
        btnPasteFav.Text = "粘贴加服";
        btnPasteFav.SetBounds(504, 392, 84, 30);
        btnPasteFav.Click += delegate { PasteAndAddFav(); };
        pJoin.Controls.Add(btnPasteFav);

        btnEditFav = new Button();
        btnEditFav.Text = "编辑…";
        btnEditFav.SetBounds(594, 392, 76, 30);
        btnEditFav.Click += delegate { EditSelectedFav(); };
        pJoin.Controls.Add(btnEditFav);

        btnDelFav = new Button();
        btnDelFav.Text = "删除选中";
        btnDelFav.SetBounds(676, 392, 88, 30);
        btnDelFav.Click += delegate { DeleteSelectedFav(); };
        pJoin.Controls.Add(btnDelFav);

        cmsSrv = new ContextMenuStrip(uiComponents);
        cmsSrv.Items.Add("复制选中服务器 IP:端口", null, delegate
        {
            string a = SelectedAddr();
            if (a.Length > 0) CopyToClipboard("服务器地址", a);
        });
        cmsSrv.Items.Add("复制 connect 连接命令", null, delegate
        {
            string a = SelectedAddr();
            if (a.Length > 0) CopyToClipboard("连接命令", "connect " + a);
        });
        cmsSrv.Items.Add(new ToolStripSeparator());
        cmsSrv.Items.Add("编辑备注名 / IP… (F2)", null, delegate { EditSelectedFav(); });
        cmsSrv.Items.Add("勾选选中行", null, delegate { SetChecksOnSelection(true); });
        cmsSrv.Items.Add("取消勾选选中行", null, delegate { SetChecksOnSelection(false); });
        cmsSrv.Items.Add("刷新选中行的人数", null, delegate { RefreshSelectedFavs(); });
        cmsSrv.Items.Add(new ToolStripSeparator());
        cmsSrv.Items.Add("删除选中 (Delete)", null, delegate { DeleteSelectedFav(); });
        lvSrv.ContextMenuStrip = cmsSrv;
        lvSrv.MouseDown += delegate(object snd, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;
            ListViewItem it = lvSrv.GetItemAt(e.X, e.Y);
            if (it != null && !it.Selected)
            {
                lvSrv.SelectedItems.Clear();
                it.Selected = true;
                it.Focused = true;
            }
        };
        lvSrv.DoubleClick += delegate { EditSelectedFav(); };
        lvSrv.KeyDown += delegate(object snd, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete) { e.Handled = true; DeleteSelectedFav(); }
            else if (e.KeyCode == Keys.F2) { e.Handled = true; EditSelectedFav(); }
            else if (e.Control && e.KeyCode == Keys.C)
            {
                string a = SelectedAddr();
                if (a.Length > 0) CopyToClipboard("服务器地址", a);
                e.Handled = true;
            }
        };

        lblJoinHint = new Label();
        lblJoinHint.ForeColor = Color.FromArgb(40, 70, 110);
        lblJoinHint.SetBounds(8, 428, 900, 44);
        pJoin.Controls.Add(lblJoinHint);

        pJoinPanel = pJoin;
        pJoin.Resize += delegate { LayoutJoin(); };

        // ================= 准星面板 =================
        var pCross = new Panel();
        pCross.SetBounds(8, 54, page.ClientSize.Width - 16, page.ClientSize.Height - 62);
        pCross.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        pCross.Visible = false;
        page.Controls.Add(pCross);

        btnCross = new Button();
        btnCross.Text = "准星开关 (" + KeyName(4) + ")";
        btnCross.Font = TrackFont("Microsoft YaHei UI", 10f, FontStyle.Bold);
        btnCross.BackColor = Color.FromArgb(228, 242, 255);
        btnCross.SetBounds(8, 4, 170, 36);
        btnCross.Click += delegate { ToggleCross(); };
        pCross.Controls.Add(btnCross);

        btnColor = new Button();
        btnColor.Text = "换颜色 (" + KeyName(3) + ")";
        btnColor.SetBounds(186, 4, 120, 36);
        btnColor.Click += delegate
        {
            overlay.CycleColor();
            if (cmbColor != null) cmbColor.SelectedIndex = overlay.ColorIndex;
            if (crossPreview != null) crossPreview.Invalidate();
            SaveSettings();
            RefreshUi(true);
        };
        pCross.Controls.Add(btnColor);

        var lblColorPick = new Label();
        lblColorPick.Text = "颜色：";
        lblColorPick.SetBounds(322, 12, 48, 22);
        pCross.Controls.Add(lblColorPick);

        cmbColor = new ComboBox();
        cmbColor.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbColor.Items.AddRange(Overlay.ColorNames);
        cmbColor.SelectedIndex = 0;
        cmbColor.SetBounds(372, 9, 110, 26);
        cmbColor.SelectedIndexChanged += delegate
        {
            overlay.ColorIndex = cmbColor.SelectedIndex;
            if (crossPreview != null) crossPreview.Invalidate();
            SaveSettings();
            RefreshUi(true);
        };
        pCross.Controls.Add(cmbColor);

        var lblStyle = new Label();
        lblStyle.Text = "样式：";
        lblStyle.SetBounds(8, 56, 48, 22);
        pCross.Controls.Add(lblStyle);

        cmbStyle = new ComboBox();
        cmbStyle.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbStyle.Items.AddRange(Overlay.Styles);
        cmbStyle.SelectedIndex = 0;
        cmbStyle.SetBounds(58, 54, 180, 26);
        cmbStyle.SelectedIndexChanged += delegate
        {
            overlay.StyleIndex = cmbStyle.SelectedIndex;
            overlay.Invalidate();
            if (crossPreview != null) crossPreview.Invalidate();
            if (!suppressSettingsSave) toast.ShowText("准星样式：" + cmbStyle.Text);
            SaveSettings();
            RefreshUi(true);
        };
        pCross.Controls.Add(cmbStyle);

        var lblThick = new Label();
        lblThick.Text = "线宽：";
        lblThick.SetBounds(256, 56, 48, 22);
        pCross.Controls.Add(lblThick);

        nThick = new NumericUpDown();
        nThick.Minimum = 1; nThick.Maximum = 6;
        nThick.Value = overlay.Thickness;
        nThick.SetBounds(306, 54, 56, 26);
        nThick.ValueChanged += delegate
        {
            overlay.Thickness = (int)nThick.Value;
            overlay.Invalidate();
            if (crossPreview != null) crossPreview.Invalidate();
            SaveSettings();
        };
        pCross.Controls.Add(nThick);

        var lblLen = new Label();
        lblLen.Text = "臂长：";
        lblLen.SetBounds(378, 56, 48, 22);
        pCross.Controls.Add(lblLen);

        nLen = new NumericUpDown();
        nLen.Minimum = 3; nLen.Maximum = 40;
        nLen.Value = overlay.Length;
        nLen.SetBounds(428, 54, 56, 26);
        nLen.ValueChanged += delegate
        {
            overlay.Length = (int)nLen.Value;
            overlay.Invalidate();
            if (crossPreview != null) crossPreview.Invalidate();
            SaveSettings();
        };
        pCross.Controls.Add(nLen);

        var lblGap = new Label();
        lblGap.Text = "间隙：";
        lblGap.SetBounds(500, 56, 48, 22);
        pCross.Controls.Add(lblGap);

        nGap = new NumericUpDown();
        nGap.Minimum = 0; nGap.Maximum = 30;
        nGap.Value = overlay.Gap;
        nGap.SetBounds(550, 54, 56, 26);
        nGap.ValueChanged += delegate
        {
            overlay.Gap = (int)nGap.Value;
            overlay.Invalidate();
            if (crossPreview != null) crossPreview.Invalidate();
            SaveSettings();
        };
        pCross.Controls.Add(nGap);

        var lblDot = new Label();
        lblDot.Text = "中心点：";
        lblDot.SetBounds(622, 56, 58, 22);
        pCross.Controls.Add(lblDot);

        nDot = new NumericUpDown();
        nDot.Minimum = 0; nDot.Maximum = 8;
        nDot.Value = overlay.DotSize;
        nDot.SetBounds(682, 54, 56, 26);
        nDot.ValueChanged += delegate
        {
            overlay.DotSize = (int)nDot.Value;
            overlay.Invalidate();
            if (crossPreview != null) crossPreview.Invalidate();
            SaveSettings();
        };
        pCross.Controls.Add(nDot);

        btnCrossImg = new Button();
        btnCrossImg.Text = "导入准星图片…";
        btnCrossImg.SetBounds(8, 284, 132, 30);
        btnCrossImg.Click += delegate { ImportCrosshairImage(); };
        pCross.Controls.Add(btnCrossImg);

        btnCrossImgPaste = new Button();
        btnCrossImgPaste.Text = "粘贴图片";
        btnCrossImgPaste.SetBounds(148, 284, 96, 30);
        btnCrossImgPaste.Click += delegate { PasteCrosshairImage(); };
        pCross.Controls.Add(btnCrossImgPaste);

        btnCrossImgClear = new Button();
        btnCrossImgClear.Text = "清除图片";
        btnCrossImgClear.SetBounds(252, 284, 88, 30);
        btnCrossImgClear.Click += delegate { ClearCrosshairImage(); };
        pCross.Controls.Add(btnCrossImgClear);

        lblCrossImg = new Label();
        lblCrossImg.Text = "当前用内置矢量样式（可导入 PNG 做自定义准星）";
        lblCrossImg.ForeColor = Color.FromArgb(90, 100, 114);
        lblCrossImg.SetBounds(352, 290, 520, 22);
        pCross.Controls.Add(lblCrossImg);

        crossPreview = new CrosshairPreview(overlay);
        crossPreview.SetBounds(8, 96, 180, 180);
        crossPreview.Cursor = Cursors.Hand;
        crossPreview.Click += delegate
        {
            overlay.CycleColor();
            if (cmbColor != null) cmbColor.SelectedIndex = overlay.ColorIndex;
            crossPreview.Invalidate();
            SaveSettings();
            RefreshUi(true);
        };
        pCross.Controls.Add(crossPreview);

        lblCrossHint = new Label();
        lblCrossHint.ForeColor = Color.FromArgb(80, 90, 102);
        lblCrossHint.SetBounds(206, 98, 620, 160);
        pCross.Controls.Add(lblCrossHint);

        Font fToolBold = TrackFont("Microsoft YaHei UI", 10.5f, FontStyle.Bold);
        Font fToolReg = TrackFont("Microsoft YaHei UI", 10.5f, FontStyle.Regular);
        Action<int> switchTo = delegate(int which)
        {
            pJoin.Visible = (which == 0);
            pCross.Visible = (which == 1);
            bJoin.Font = which == 0 ? fToolBold : fToolReg;
            bCross.Font = which == 1 ? fToolBold : fToolReg;
            bJoin.BackColor = (which == 0) ? Color.FromArgb(205, 228, 255) : Color.FromArgb(245, 247, 250);
            bCross.BackColor = (which == 1) ? Color.FromArgb(205, 228, 255) : Color.FromArgb(245, 247, 250);
        };
        bJoin.Click += delegate { switchTo(0); };
        bCross.Click += delegate { switchTo(1); };
        switchTo(0);

        LayoutJoin();
        LoadFavs();
        FillSrvList();
        ApplyModeUi();
        UpdateHotkeyLabels();

        return page;
    }

    // ---------- 配置持久化 ----------
    void LoadSettings()
    {
        try
        {
            if (string.IsNullOrEmpty(settingsPath) || !File.Exists(settingsPath)) return;
            suppressSettingsSave = true;
            string[] lines = File.ReadAllLines(settingsPath, Encoding.UTF8);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string k = line.Substring(0, eq).Trim();
                string v = line.Substring(eq + 1).Trim();
                if (k.Length > 6 && k.StartsWith("Hotkey", StringComparison.Ordinal))
                {
                    int hi, hv;
                    if (int.TryParse(k.Substring(6), out hi) && hi >= 1 && hi <= 5 && int.TryParse(v, out hv))
                    {
                        int nvk = hv / 16, nmod = hv % 16;
                        if (nvk >= 8 && nvk <= 255)
                        {
                            hkVk[hi - 1] = nvk;
                            hkMod[hi - 1] = nmod & 0x000F;
                        }
                    }
                    continue;
                }
                if (k == "UpdateUrl") { updateUrl = v; continue; }
                if (k == "AutoUpdate") { autoUpdateOn = (v == "1"); continue; }
                if (k == "CrossImg")
                {
                    crossImgToLoad = v;
                    continue;
                }
                if (k == "Checked")
                {
                    chkPref.Clear();
                    if (v.Length > 0)
                        foreach (string one in v.Split('|'))
                        {
                            string a = one.Trim();
                            if (a.Length > 0) chkPref.Add(a);
                        }
                    chkPrefLoaded = true;
                    chkPrefAuthoritative = true;
                    continue;
                }
                int iv;
                if (!int.TryParse(v, out iv)) continue;

                if (k == "StyleIndex" && iv >= 0 && iv < Overlay.Styles.Length)
                {
                    overlay.StyleIndex = iv;
                    if (cmbStyle != null) cmbStyle.SelectedIndex = iv;
                }
                else if (k == "ColorIndex" && iv >= 0 && iv < Overlay.Palette.Length)
                {
                    overlay.ColorIndex = iv;
                    if (cmbColor != null) cmbColor.SelectedIndex = iv;
                }
                else if (k == "Thickness" && iv >= 1 && iv <= 6)
                {
                    overlay.Thickness = iv;
                    if (nThick != null) nThick.Value = iv;
                }
                else if (k == "Length" && iv >= 3 && iv <= 40)
                {
                    overlay.Length = iv;
                    if (nLen != null) nLen.Value = iv;
                }
                else if (k == "Gap" && iv >= 0 && iv <= 30)
                {
                    overlay.Gap = iv;
                    if (nGap != null) nGap.Value = iv;
                }
                else if (k == "DotSize" && iv >= 0 && iv <= 8)
                {
                    overlay.DotSize = iv;
                    if (nDot != null) nDot.Value = iv;
                }
                else if (k == "SpeedIndex" && cmbSpeed != null && iv >= 0 && iv < cmbSpeed.Items.Count)
                {
                    cmbSpeed.SelectedIndex = iv;
                }
                else if (k == "OcrOn" && chkOcr != null)
                {
                    chkOcr.Checked = (v == "1");
                }
                else if (k == "JMode" && rbTapMode != null && iv >= 0 && iv <= 2)
                {
                    suppressModeEvent = true;
                    if (iv == 0) rbTapMode.Checked = true;
                    else if (iv == 2) rbPollMode.Checked = true;
                    else rbOneIp.Checked = true;
                    suppressModeEvent = false;
                }
            }
            overlay.Invalidate();
            if (crossPreview != null) crossPreview.Invalidate();
        }
        catch (Exception ex) { Native.Log("settings load: " + ex.Message); }
        finally { suppressSettingsSave = false; }
    }

    // 把当前列表的勾选状态记到偏好集合里（用户手动勾选时调用）
    void CaptureChkPref()
    {
        if (lvSrv == null) return;
        chkPref.Clear();
        for (int i = 0; i < lvSrv.Items.Count; i++)
            if (lvSrv.Items[i].Checked)
            {
                string a = Convert.ToString(lvSrv.Items[i].Tag);
                if (!string.IsNullOrEmpty(a)) chkPref.Add(a);
            }
        chkPrefLoaded = true;
        SaveSettings();
    }

    void SaveSettings()
    {
        if (suppressSettingsSave || string.IsNullOrEmpty(settingsPath)) return;
        try
        {
            var sb = new StringBuilder();
            sb.Append("StyleIndex=").Append(overlay.StyleIndex).Append("\r\n");
            sb.Append("ColorIndex=").Append(overlay.ColorIndex).Append("\r\n");
            sb.Append("Thickness=").Append(overlay.Thickness).Append("\r\n");
            sb.Append("Length=").Append(overlay.Length).Append("\r\n");
            sb.Append("Gap=").Append(overlay.Gap).Append("\r\n");
            sb.Append("DotSize=").Append(overlay.DotSize).Append("\r\n");
            if (cmbSpeed != null) sb.Append("SpeedIndex=").Append(cmbSpeed.SelectedIndex).Append("\r\n");
            if (chkOcr != null) sb.Append("OcrOn=").Append(chkOcr.Checked ? 1 : 0).Append("\r\n");
            if (overlay != null && !string.IsNullOrEmpty(overlay.CustomImagePath))
                sb.Append("CrossImg=").Append(overlay.CustomImagePath).Append("\r\n");
            for (int hi = 0; hi < 5; hi++)
                sb.Append("Hotkey").Append(hi + 1).Append("=").Append(hkVk[hi] * 16 + hkMod[hi]).Append("\r\n");
            sb.Append("JMode=").Append(JoinModeIdx).Append("\r\n");
            sb.Append("AutoUpdate=").Append(autoUpdateOn ? 1 : 0).Append("\r\n");
            sb.Append("DevTools=").Append(devToolsOn ? 1 : 0).Append("\r\n");
            sb.Append("HotkeysOff=").Append(hotkeysOff ? 1 : 0).Append("\r\n");
            if (!string.IsNullOrEmpty(updateUrl)) sb.Append("UpdateUrl=").Append(updateUrl).Append("\r\n");
            // 版本库地址与密钥也要写回，否则改任何设置都会把它们抹掉
            if (!string.IsNullOrEmpty(hubUrl)) sb.Append("HubUrl=").Append(hubUrl).Append("\r\n");
            if (!string.IsNullOrEmpty(hubKey)) sb.Append("HubKey=").Append(hubKey).Append("\r\n");
            // 收藏轮询的勾选偏好；一个都没勾也要写（表示"用户就是要全不选"）
            sb.Append("Checked=");
            if (lvSrv != null)
            {
                var picked = new System.Collections.Generic.List<string>();
                for (int i = 0; i < lvSrv.Items.Count; i++)
                    if (lvSrv.Items[i].Checked) picked.Add(Convert.ToString(lvSrv.Items[i].Tag));
                sb.Append(string.Join("|", picked.ToArray()));
            }
            sb.Append("\r\n");
            AtomicWriteText(settingsPath, sb.ToString());
        }
        catch { }
    }

    // ---------- 收藏夹 ----------
    void LoadFavs()
    {
        try
        {
            if (File.Exists(favPath))
            {
                string[] lines = File.ReadAllLines(favPath, Encoding.UTF8);
                int n = 0;
                for (int i = 0; i < lines.Length; i++) if (lines[i].Trim().Length > 0) n++;
                favList = new string[n];
                int k = 0;
                for (int i = 0; i < lines.Length; i++)
                    if (lines[i].Trim().Length > 0) favList[k++] = lines[i].Trim();
            }
        }
        catch (Exception ex) { Native.Log("favs load: " + ex.Message); }
    }

    bool SaveFavs()
    {
        try { return AtomicWriteText(favPath, string.Join(Environment.NewLine, favList)); }
        catch (Exception ex) { Native.Log("favs save: " + ex.Message); return false; }
    }

    int JoinModeIdx
    {
        get
        {
            if (rbTapMode != null && rbTapMode.Checked) return 0;
            if (rbPollMode != null && rbPollMode.Checked) return 2;
            return 1;
        }
    }

    void ApplyModeUi()
    {
        int m = JoinModeIdx;
        bool tap = (m == 0);
        bool poll = (m == 2);

        if (lvSrv != null)
        {
            lvSrv.Visible = !tap;
            if (lvSrv.CheckBoxes != poll) lvSrv.CheckBoxes = poll;
        }
        if (lblListHint != null)
        {
            lblListHint.Visible = !tap;
            lblListHint.Text = tap
                ? "手动档不需要选服：在游戏里点「连接」，这里负责连按回车"
                : (poll ? "服务器列表：勾选要参与的服（可全选）　·　点「↻ 刷新人数」查在线（点表头可排序）"
                        : "服务器列表：单击选中要挤的那个服　·　点「↻ 刷新人数」查在线（点表头可排序）");
        }
        if (btnCheckAll != null) btnCheckAll.Visible = poll;
        if (btnUncheckAll != null) btnUncheckAll.Visible = poll;
        if (btnRefreshFav != null) btnRefreshFav.Visible = !tap;
        if (lblTapGuide != null) lblTapGuide.Visible = tap;

        if (lblNewIp != null) lblNewIp.Visible = !tap;
        if (txtNewIp != null) txtNewIp.Visible = !tap;
        if (lblNewAlias != null) lblNewAlias.Visible = !tap;
        if (txtNewAlias != null) txtNewAlias.Visible = !tap;
        if (btnAddFav != null) btnAddFav.Visible = !tap;
        if (btnPasteFav != null) btnPasteFav.Visible = !tap;
        if (btnDelFav != null) btnDelFav.Visible = !tap;
        if (btnEditFav != null) btnEditFav.Visible = !tap;

        LayoutJoin();
        UpdateJoinHint();
        if (pJoinPanel != null)
        {
            try
            {
                BeginInvoke(new MethodInvoker(delegate
                {
                    if (pJoinPanel == null || isClosing) return;
                    pJoinPanel.Invalidate(true);
                }));
            }
            catch { }
        }
    }

    static Image guideImgL, guideImgR;

    static Image LoadGuideImg(string resName, ref Image slot)
    {
        if (slot != null) return slot;
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            using (var st = asm.GetManifestResourceStream(resName))
            {
                if (st == null) return null;
                var ms = new MemoryStream();
                st.CopyTo(ms);
                ms.Position = 0;
                slot = Image.FromStream(ms);
            }
        }
        catch { slot = null; }
        return slot;
    }

    void ShowGuide()
    {
        int m = JoinModeIdx;
        string hkStart = KeyName(2);
        string title;
        string[] steps, flow;

        if (m == 0)
        {
            title = "手动档 —— 游戏里点连接，工具替你连按回车";
            steps = new string[] {
                "1. 打开游戏：Play → 服务器列表，搜到目标服务器。",
                "2. 选中服务器点「连接」。人数满了会弹「服务器人数已满」，不用管它，也别关本工具。",
                "3. 点一下游戏窗口让它在前台，然后按 " + hkStart + "。",
                "4. 工具会按你设的「间隔」不停按回车：关掉满员弹框、重新发起连接。",
                "5. 挤进去后按 " + hkStart + " 停；勾了「OCR 识别进服后自动停」会自动停。",
            };
            flow = new string[] {
                "【工具在做什么】",
                "· 只发一个按键：回车（keybd_event + 真实扫描码，游戏才认）。",
                "· 那个满员弹框的默认焦点就在按钮上，所以回车 = 点掉它，游戏随即重新发起连接；",
                "  连按回车 = 关框 + 重连的无限循环，正是挤满员服最原始也最有效的手法。",
                "· 不模拟鼠标、不发文字，所以不怕误点，也不会打断游戏加载。",
                "",
                "【必须满足的条件】",
                "· 游戏必须在前台：工具只往当前前台窗口发键，切到别的窗口时那一拍会被跳过。",
                "· 弹框必须是活动窗口（游戏里点过连接后自动就是，别手动点别处）。",
                "",
                "【什么时候停】",
                "· 手动：按 " + hkStart + "。",
                "· 自动：勾了 OCR 时，每 2 秒后台截一次游戏画面做文字识别，识别到「正在加载插件 / 加载中」",
                "  这类加载画面就认定进服，立即停手。识别不了只会继续挤，不会误停。",
            };
        }
        else if (m == 1)
        {
            title = "单一 IP —— 只挤列表里选中的那一个服";
            steps = new string[] {
                "1. 先打开游戏，停在主菜单就行（不用进服务器列表）。",
                "2. 在下面的列表里点一下要挤的服务器（整行变蓝即选中）。",
                "3. 不在列表里？在「IP:端口」框填地址（可顺手填备注）→「★ 加入收藏」，或复制地址后点「粘贴加服」。",
                "4. ★ 切回游戏，按 「~」 打开控制台（必做：工具打不开它，不开就什么都发不进去）。",
                "5. 控制台开着时直接按 " + hkStart + " 开始：",
                "    工具把 connect 一条条粘进控制台并回车，之后不用管。",
                "6. 挤进去后按 " + hkStart + " 停；勾了 OCR 会自动停。",
            };
            flow = new string[] {
                "【工具在做什么】",
                "· 把一条 \"connect IP:端口\" 写进剪贴板，然后 Ctrl+V 粘到控制台 + 回车执行。",
                "· 开始前自动备份你剪贴板里原有的文字，停止挤服后自动帮你还原。",
                "· 每条命令之间按你设的「间隔」节拍，200ms 左右最稳。",
                "",
                "【为什么要你自己开控制台】",
                "· GMod 引擎会过滤掉模拟按键来开关控制台，任何工具都打不开，只能你按一下。",
                "· 所以顺序是：先进游戏 → 按 ~ 打开控制台 → 再按 " + hkStart + "（热键是全局的，不用切回工具）。",
                "",
                "【输入法 / 焦点】",
                "· 开始时会临时把游戏切到英文输入法，避免中文输入法把命令吃掉；任何方式停止时都会自动还原。",
                "· 已经在游戏里（控制台开着）时不会再抢焦点、也不会把全屏窗口打回窗口化。",
            };
        }
        else
        {
            title = "收藏轮询 —— 在勾选的服之间轮流转，哪个空往哪挤";
            steps = new string[] {
                "1. 先打开游戏，停在主菜单就行（不用进服务器列表）。",
                "2. 在列表里勾选要参与的服务器（勾选框在名字左边；全选 / 全不选可批量）。",
                "3. 想先看看人数就点「↻ 刷新人数」：满员红色，有空位绿色（点「人数」表头可把有空位的排到最前）。",
                "4. ★ 切回游戏，按 「~」 打开控制台（必做：工具打不开它，不开就什么都发不进去）。",
                "5. 控制台开着时直接按 " + hkStart + " 开始：",
                "    工具按顺序把 connect 粘进控制台，一个服一个服试。",
                "6. 挤进去后按 " + hkStart + " 停；勾了 OCR 会自动停。",
            };
            flow = new string[] {
                "【工具在做什么】",
                "· 把勾选的地址排成一个队列，每次发下一条 connect，走完一轮再从第一个开始。",
                "· 列表第一列带 ▶ 标记的即为当前这一拍正在尝试连接的服务器。",
                "· 一轮里的每个服都用同样的间隔节拍，所以勾得越多，单个服的重试频率越低。",
                "",
                "【怎么挑服才高效】",
                "· 3~6 个最合适：太少没意义，太多每个服半天轮不到一次。",
                "· 点一下列表的「人数」列标题，可以把有空位的服务器直接排到最顶上。",
                "",
                "【加服 / 删服】",
                "· 加：「IP:端口」框填地址（可填备注）→「★ 加入收藏」，或直接点「粘贴加服」。",
                "· 删：选中某行 →「删除选中」（或按 Delete 键）。",
            };
        }

        using (var f = new Form())
        {
            f.Text = "使用指南 · RX 工具箱";
            f.StartPosition = FormStartPosition.CenterParent;
            f.FormBorderStyle = FormBorderStyle.FixedDialog;
            f.MinimizeBox = false;
            f.MaximizeBox = false;
            f.ShowInTaskbar = false;
            f.BackColor = Color.White;
            f.ClientSize = new Size(784, 670);

            var lbTitle = new Label();
            lbTitle.Text = "【当前模式】" + title;
            lbTitle.Font = TrackFont("Microsoft YaHei UI", 11f, FontStyle.Bold);
            lbTitle.ForeColor = Color.FromArgb(20, 60, 120);
            lbTitle.SetBounds(16, 10, 752, 26);
            f.Controls.Add(lbTitle);

            var tabsG = new TabControl();
            // 顶部警示条：控制台必须手动开 + 剪贴板会被刷屏
            var warn = new Label();
            warn.SetBounds(16, 40, 752, 40);
            warn.BackColor = Color.FromArgb(255, 240, 236);
            warn.ForeColor = Color.FromArgb(176, 40, 26);
            warn.Font = TrackFont("Microsoft YaHei UI", 10f, FontStyle.Bold);
            warn.TextAlign = ContentAlignment.MiddleLeft;
            warn.Padding = new Padding(10, 0, 10, 0);
            if (m == 0)
            {
                warn.BackColor = Color.FromArgb(255, 249, 232);
                warn.ForeColor = Color.FromArgb(140, 90, 10);
                warn.Text = "⚠ 手动档不需要控制台。游戏必须在前台，工具只往游戏窗口发回车。";
            }
            else
            {
                warn.Text = "⚠ 开始前必须自己按 「~」 打开游戏控制台 —— 工具打不开它，不开就什么都发不进去";
            }
            f.Controls.Add(warn);

            tabsG.SetBounds(12, 88, 760, 524);
            tabsG.Font = TrackFont("Microsoft YaHei UI", 9.5f, FontStyle.Regular);
            f.Controls.Add(tabsG);

            var p1 = new TabPage("① 按步骤操作");
            p1.BackColor = Color.White;
            p1.Padding = new Padding(10);
            var tbSteps = new TextBox();
            tbSteps.Multiline = true;
            tbSteps.ReadOnly = true;
            tbSteps.BorderStyle = BorderStyle.None;
            tbSteps.ScrollBars = ScrollBars.Vertical;
            tbSteps.BackColor = Color.White;
            tbSteps.Dock = DockStyle.Fill;
            tbSteps.Font = TrackFont("Microsoft YaHei UI", 10.5f, FontStyle.Regular);
            tbSteps.Text = string.Join(Environment.NewLine, steps);
            p1.Controls.Add(tbSteps);
            tabsG.TabPages.Add(p1);

            var p2 = new TabPage("② 原理与工作流");
            p2.BackColor = Color.FromArgb(253, 251, 245);
            p2.Padding = new Padding(10);
            var tbFlow = new TextBox();
            tbFlow.Multiline = true;
            tbFlow.ReadOnly = true;
            tbFlow.BorderStyle = BorderStyle.None;
            tbFlow.ScrollBars = ScrollBars.Vertical;
            tbFlow.BackColor = Color.FromArgb(253, 251, 245);
            tbFlow.Dock = DockStyle.Fill;
            tbFlow.Font = TrackFont("Microsoft YaHei UI", 9.5f, FontStyle.Regular);
            tbFlow.Text = string.Join(Environment.NewLine, flow);
            p2.Controls.Add(tbFlow);
            tabsG.TabPages.Add(p2);

            var p3 = new TabPage("③ 如何收藏服务器");
            p3.BackColor = Color.White;
            p3.Padding = new Padding(10);

            var lb31 = new Label();
            lb31.Text = "① 在游戏里点中想收藏的服务器 —— 右上角会弹出它的小窗（下图红框）";
            lb31.Font = TrackFont("Microsoft YaHei UI", 9.5f, FontStyle.Bold);
            lb31.ForeColor = Color.FromArgb(20, 60, 120);
            lb31.SetBounds(10, 10, 720, 22);
            p3.Controls.Add(lb31);

            Image imgL = LoadGuideImg("guide_srv.jpg", ref guideImgL);
            Image imgR = LoadGuideImg("guide_ip.png", ref guideImgR);

            if (imgL != null)
            {
                var pbL = new PictureBox();
                pbL.SizeMode = PictureBoxSizeMode.Zoom;
                pbL.BorderStyle = BorderStyle.FixedSingle;
                pbL.Image = imgL;
                pbL.SetBounds(10, 38, 340, 191);
                p3.Controls.Add(pbL);
            }

            var lb32 = new Label();
            lb32.Text = "左：游戏里的服务器列表，点中任意一行\r\n"
                + "右上角：这一行的服务器信息栏\r\n\r\n"
                + "只有点中某一行，右上角那个小窗才会出现；\r\n"
                + "里面写着服务器名和 IP:端口。";
            lb32.Font = TrackFont("Microsoft YaHei UI", 9f, FontStyle.Regular);
            lb32.ForeColor = Color.FromArgb(70, 80, 95);
            lb32.SetBounds(imgL != null ? 360 : 10, 40, imgL != null ? 380 : 720, 100);
            p3.Controls.Add(lb32);

            var lb33 = new Label();
            lb33.Text = "② 用鼠标选中黄框里的那串 IP:端口，Ctrl+C 复制";
            lb33.Font = TrackFont("Microsoft YaHei UI", 9.5f, FontStyle.Bold);
            lb33.ForeColor = Color.FromArgb(20, 60, 120);
            lb33.SetBounds(10, 236, 720, 22);
            p3.Controls.Add(lb33);

            if (imgR != null)
            {
                var pbR = new PictureBox();
                pbR.SizeMode = PictureBoxSizeMode.Zoom;
                pbR.BorderStyle = BorderStyle.FixedSingle;
                pbR.Image = imgR;
                pbR.SetBounds(10, 262, 720, 172);
                p3.Controls.Add(pbR);
            }

            var lb34 = new Label();
            lb34.Text = "③ 回到工具直接点「粘贴加服」（或粘到「IP:端口」框 → 点「★ 加入收藏」）。\r\n"
                + "    程序会自动探测服务器真名并保存，下次打开直接显示。\r\n"
                + "    想删就选中那一行点「删除选中」。";
            lb34.Font = TrackFont("Microsoft YaHei UI", 9f, FontStyle.Regular);
            lb34.ForeColor = Color.FromArgb(70, 80, 95);
            lb34.SetBounds(10, imgR != null ? 440 : 300, 720, 56);
            p3.Controls.Add(lb34);

            tabsG.TabPages.Add(p3);

            var bOk = new Button();
            bOk.Text = "知道了";
            bOk.SetBounds(676, 622, 92, 32);
            bOk.DialogResult = DialogResult.OK;
            f.Controls.Add(bOk);
            f.AcceptButton = bOk;
            f.CancelButton = bOk;

            Native.Log("guide shown, mode=" + m);
            f.ShowDialog(this);
        }
    }

    void OnModeChanged()
    {
        if (suppressModeEvent) return;
        Native.Log("mode changed -> " + JoinModeIdx);
        if (autoOn || typingOn)
        {
            StopActiveJoin("已切换挤服模式，当前挤服已自动停止", "stop: mode changed");
        }
        ApplyModeUi();
        SaveSettings();
        RefreshUi(true);
    }

    static string AddrOf(string entry)
    {
        if (string.IsNullOrEmpty(entry)) return "";
        int bar = entry.IndexOf('|');
        return (bar >= 0 ? entry.Substring(0, bar) : entry).Trim();
    }

    int laidW = -1, laidH = -1;

    void LayoutJoin()
    {
        if (pJoinPanel == null) return;
        int W = pJoinPanel.ClientSize.Width;
        int H = pJoinPanel.ClientSize.Height;
        try
        {
            if (tabs != null && pJoinPanel.Parent != null)
            {
                int cap = tabs.DisplayRectangle.Height - pJoinPanel.Top - 6;
                if (cap > 200 && cap < H) H = cap;
            }
        }
        catch { }
        laidW = W;
        laidH = H;
        if (W < 360) W = 360;
        if (H < 260) H = 260;

        int bx = W - 8;
        if (btnRefreshFav != null)
        {
            btnRefreshFav.SetBounds(bx - 104, 46, 104, 28);
            bx -= 114;
        }
        if (btnUncheckAll != null)
        {
            btnUncheckAll.SetBounds(bx - 74, 46, 74, 28);
            bx -= 80;
        }
        if (btnCheckAll != null)
        {
            btnCheckAll.SetBounds(bx - 64, 46, 64, 28);
            bx -= 72;
        }
        if (lblListHint != null) lblListHint.SetBounds(8, 52, Math.Max(120, bx - 12), 22);

        int pageW = W + 16;
        if (btnHotkey != null) btnHotkey.SetBounds(pageW - 8 - 104, 10, 104, 36);
        if (bLog != null) bLog.SetBounds(pageW - 8 - 104 - 8 - 96, 10, 96, 36);

        int hintH = 42;
        int hintY = H - 6 - hintH;
        int ipY = hintY - 36;
        if (ipY < 160) ipY = 160;

        if (lblJoinHint != null) lblJoinHint.SetBounds(8, hintY, W - 16, hintH);
        if (lblNewIp != null) lblNewIp.SetBounds(8, ipY + 6, 54, 22);
        if (txtNewIp != null) txtNewIp.SetBounds(64, ipY + 2, 176, 26);
        if (lblNewAlias != null) lblNewAlias.SetBounds(248, ipY + 6, 40, 22);
        if (txtNewAlias != null) txtNewAlias.SetBounds(290, ipY + 2, 96, 26);
        if (btnAddFav != null) btnAddFav.SetBounds(394, ipY, 104, 30);
        if (btnPasteFav != null) btnPasteFav.SetBounds(504, ipY, 84, 30);
        if (btnEditFav != null) btnEditFav.SetBounds(594, ipY, 76, 30);
        if (btnDelFav != null) btnDelFav.SetBounds(676, ipY, 88, 30);

        int listBottom = (JoinModeIdx == 0) ? (hintY - 8) : (ipY - 10);
        int listH = Math.Max(80, listBottom - 80);
        if (lblTapGuide != null) lblTapGuide.SetBounds(8, 80, W - 16, listH);
        if (lvSrv != null) lvSrv.SetBounds(8, 80, W - 16, listH);
        if (lvSrv != null && lvSrv.Columns.Count == 4)
        {
            int wAddr = 156, wPl = 104, wAlias = 106;
            int wReal = W - 16 - wAddr - wPl - wAlias - 26;
            if (wReal < 160) wReal = 160;
            lvSrv.Columns[0].Width = wAlias;
            lvSrv.Columns[1].Width = wReal;
            lvSrv.Columns[2].Width = wPl;
            lvSrv.Columns[3].Width = wAddr;
        }
    }

    int SelectedFavIndex()
    {
        if (lvSrv == null || lvSrv.SelectedIndices.Count == 0) return -1;
        if (lvSrv.FocusedItem != null && lvSrv.FocusedItem.Selected) return lvSrv.FocusedItem.Index;
        return lvSrv.SelectedIndices[0];
    }

    List<int> SelectedIndexList()
    {
        var list = new List<int>();
        if (lvSrv == null) return list;
        foreach (ListViewItem it in lvSrv.SelectedItems) list.Add(it.Index);
        list.Sort();
        return list;
    }

    string SelectedAddr()
    {
        int i = SelectedFavIndex();
        if (i < 0 || i >= lvSrv.Items.Count) return "";
        return Convert.ToString(lvSrv.Items[i].Tag);
    }

    void FillSrvList() { FillSrvList(null); }

    void FillSrvList(string selectAddr)
    {
        if (lvSrv == null) return;

        Interlocked.Increment(ref favVersion);
        favPending = 0;
        favDirty = false;
        if (btnRefreshFav != null) { btnRefreshFav.Enabled = true; btnRefreshFav.Text = "↻ 刷新人数"; }

        var checks = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        string oldSel = SelectedAddr();
        for (int i = 0; i < lvSrv.Items.Count; i++)
        {
            string t = Convert.ToString(lvSrv.Items[i].Tag);
            if (t != null) checks[t] = lvSrv.Items[i].Checked;
        }
        if (string.IsNullOrEmpty(selectAddr)) selectAddr = oldSel;

        suppressListCheck = true;
        lvSrv.BeginUpdate();
        lvSrv.Items.Clear();
        favCount = new int[favList.Length];
        favMax = new int[favList.Length];
        favName = new string[favList.Length];
        for (int i = 0; i < favList.Length; i++)
        {
            favCount[i] = -2;      // -2 = 还没查过（显示 —），-1 = 查询失败
            favMax[i] = 0;
            favName[i] = "";
            string addr = AddrOf(favList[i]);
            string[] parts = favList[i].Split('|');
            string nm = (parts.Length > 1) ? parts[1].Trim() : "";
            string cached = (parts.Length > 2) ? parts[2].Trim() : "";
            if (cached.Length > 0) favName[i] = cached;

            var it = new ListViewItem(nm);
            it.UseItemStyleForSubItems = false;
            it.SubItems.Add(cached.Length > 0 ? cached : "—");
            it.SubItems.Add("—");
            it.SubItems.Add(addr);
            it.Tag = addr;
            bool want;
            if (chkPrefAuthoritative) want = chkPref.Contains(addr);   // 刚读完 ini：以文件为准
            else if (checks.ContainsKey(addr)) want = checks[addr];    // 运行中重建：保持现状
            else if (chkPrefLoaded) want = chkPref.Contains(addr);
            else want = true;                                          // 从没存过 → 默认全选
            it.Checked = want;
            lvSrv.Items.Add(it);
            UpdateSrvRow(i);
        }
        lvSrv.EndUpdate();
        suppressListCheck = false;
        chkPrefAuthoritative = false;

        int sel = -1;
        for (int i = 0; i < lvSrv.Items.Count; i++)
            if (string.Equals(Convert.ToString(lvSrv.Items[i].Tag), selectAddr, StringComparison.OrdinalIgnoreCase)) { sel = i; break; }
        if (sel < 0 && lvSrv.Items.Count > 0) sel = 0;
        if (sel >= 0)
        {
            lvSrv.Items[sel].Selected = true;
            lvSrv.Items[sel].Focused = true;
            lvSrv.EnsureVisible(sel);
            pendingSelAddr = Convert.ToString(lvSrv.Items[sel].Tag);
        }
        UpdateJoinHint();
    }

    void UpdateSrvRow(int i)
    {
        if (lvSrv == null || i < 0 || i >= lvSrv.Items.Count || i >= favCount.Length || i >= favList.Length) return;
        ListViewItem it = lvSrv.Items[i];
        it.UseItemStyleForSubItems = false;

        string[] parts = favList[i].Split('|');
        string alias = (parts.Length > 1) ? parts[1].Trim() : "";
        string addr = parts[0].Trim();
        bool isCurrentTarget = typingOn && autoOn && !string.IsNullOrEmpty(currentTargetAddr)
            && string.Equals(addr, currentTargetAddr, StringComparison.OrdinalIgnoreCase);

        string col0 = isCurrentTarget ? ("▶ " + (alias.Length > 0 ? alias : "挤服中")) : alias;
        if (it.SubItems[0].Text != col0) it.SubItems[0].Text = col0;
        it.SubItems[0].ForeColor = isCurrentTarget ? Color.FromArgb(18, 92, 180) : Color.FromArgb(30, 41, 59);
        it.SubItems[0].Font = TrackFont("Microsoft YaHei UI", 9f, isCurrentTarget ? FontStyle.Bold : FontStyle.Regular);

        string rname = (i < favName.Length && favName[i] != null) ? favName[i].Trim() : "";
        if (rname.Length == 0) rname = "—";
        if (it.SubItems[1].Text != rname) it.SubItems[1].Text = rname;
        it.SubItems[1].ForeColor = Color.FromArgb(34, 44, 58);

        string txt;
        Color fg;
        Color bg = Color.White;
        bool boldCount = false;
        if (favCount[i] == -3)
        {
            txt = "查询中…";
            fg = Color.FromArgb(100, 116, 139);
        }
        else if (favCount[i] == -2)
        {
            txt = "—";
            fg = Color.FromArgb(140, 148, 160);
        }
        else if (favCount[i] < 0)
        {
            txt = "超时/失败";
            fg = Color.FromArgb(140, 148, 160);
        }
        else
        {
            bool full = favMax[i] > 0 && favCount[i] >= favMax[i];
            txt = favCount[i] + "/" + favMax[i] + (full ? " [满]" : " [空]");
            fg = full ? Color.FromArgb(185, 28, 28) : Color.FromArgb(21, 128, 61);
            bg = full ? Color.FromArgb(254, 242, 242) : Color.FromArgb(236, 253, 245);
            boldCount = true;
        }
        if (it.SubItems[2].Text != txt) it.SubItems[2].Text = txt;
        it.SubItems[2].ForeColor = fg;
        it.SubItems[2].BackColor = bg;
        it.SubItems[2].Font = TrackFont("Microsoft YaHei UI", 9f, boldCount ? FontStyle.Bold : FontStyle.Regular);

        it.SubItems[3].ForeColor = Color.FromArgb(100, 116, 139);
    }

    void RefreshActiveTargetRowHighlight()
    {
        if (lvSrv == null) return;
        for (int i = 0; i < lvSrv.Items.Count && i < favList.Length; i++)
            UpdateSrvRow(i);
    }

    void SortSrvListByColumn(int col)
    {
        if (lvSrv == null || favList.Length <= 1 || favPending > 0) return;
        if (srvSortCol == col) srvSortAsc = !srvSortAsc;
        else { srvSortCol = col; srvSortAsc = true; }

        int n = favList.Length;
        var checks = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < lvSrv.Items.Count; i++)
        {
            string a = Convert.ToString(lvSrv.Items[i].Tag);
            if (a != null) checks[a] = lvSrv.Items[i].Checked;
        }
        string selAddr = SelectedAddr();

        int[] order = new int[n];
        for (int i = 0; i < n; i++) order[i] = i;

        Array.Sort(order, delegate(int a, int b)
        {
            int cmp = 0;
            if (col == 0)
            {
                string[] pa = favList[a].Split('|');
                string[] pb = favList[b].Split('|');
                string aa = pa.Length > 1 ? pa[1] : "";
                string ab = pb.Length > 1 ? pb[1] : "";
                cmp = string.Compare(aa, ab, StringComparison.CurrentCultureIgnoreCase);
            }
            else if (col == 1)
            {
                string na = a < favName.Length ? (favName[a] ?? "") : "";
                string nb = b < favName.Length ? (favName[b] ?? "") : "";
                cmp = string.Compare(na, nb, StringComparison.CurrentCultureIgnoreCase);
            }
            else if (col == 2)
            {
                // 按空位优先级排序：有空位(0) < 满员(1) < 未查/失败(2)
                int rankA = (favCount[a] >= 0 && favMax[a] > 0) ? (favCount[a] < favMax[a] ? 0 : 1) : 2;
                int rankB = (favCount[b] >= 0 && favMax[b] > 0) ? (favCount[b] < favMax[b] ? 0 : 1) : 2;
                if (rankA != rankB) cmp = rankA - rankB;
                else cmp = favCount[b] - favCount[a];
            }
            else
            {
                cmp = string.Compare(AddrOf(favList[a]), AddrOf(favList[b]), StringComparison.OrdinalIgnoreCase);
            }
            return srvSortAsc ? cmp : -cmp;
        });

        string[] newFav = new string[n];
        int[] newCnt = new int[n];
        int[] newMax = new int[n];
        string[] newNam = new string[n];
        for (int i = 0; i < n; i++)
        {
            int old = order[i];
            newFav[i] = favList[old];
            newCnt[i] = old < favCount.Length ? favCount[old] : -2;
            newMax[i] = old < favMax.Length ? favMax[old] : 0;
            newNam[i] = old < favName.Length ? favName[old] : "";
        }
        favList = newFav;
        SaveFavs();

        suppressListCheck = true;
        lvSrv.BeginUpdate();
        lvSrv.Items.Clear();
        favCount = newCnt;
        favMax = newMax;
        favName = newNam;
        for (int i = 0; i < n; i++)
        {
            string addr = AddrOf(favList[i]);
            string[] parts = favList[i].Split('|');
            string nm = (parts.Length > 1) ? parts[1].Trim() : "";
            string cached = favName[i] ?? "";
            var it = new ListViewItem(nm);
            it.UseItemStyleForSubItems = false;
            it.SubItems.Add(cached.Length > 0 ? cached : "—");
            it.SubItems.Add("—");
            it.SubItems.Add(addr);
            it.Tag = addr;
            it.Checked = !checks.ContainsKey(addr) || checks[addr];
            lvSrv.Items.Add(it);
            UpdateSrvRow(i);
        }
        lvSrv.EndUpdate();
        suppressListCheck = false;

        for (int i = 0; i < lvSrv.Items.Count; i++)
        {
            if (string.Equals(Convert.ToString(lvSrv.Items[i].Tag), selAddr, StringComparison.OrdinalIgnoreCase))
            {
                lvSrv.Items[i].Selected = true;
                lvSrv.Items[i].Focused = true;
                break;
            }
        }
        SyncActiveTargets();
        UpdateJoinHint();
    }

    void UpdateJoinHint()
    {
        if (lblJoinHint == null) return;
        string a = SelectedAddr();
        string selTxt = (a.Length == 0) ? "（未选中）" : a;
        int ix = SelectedFavIndex();
        if (ix >= 0 && ix < favCount.Length && favCount[ix] >= 0)
        {
            bool full = favMax[ix] > 0 && favCount[ix] >= favMax[ix];
            selTxt += string.Format("　在线 {0}/{1} {2}", favCount[ix], favMax[ix], full ? "[满员]" : "[有空位]");
        }
        int nchk = 0;
        if (lvSrv != null) for (int i = 0; i < lvSrv.Items.Count; i++) if (lvSrv.Items[i].Checked) nchk++;
        int total = (lvSrv == null) ? 0 : lvSrv.Items.Count;
        string mode;
        int m = JoinModeIdx;
        if (m == 0) mode = "手动档：游戏里点「连接」后按 " + KeyName(2) + " 自动连按回车";
        else if (m == 2) mode = "收藏轮询：在勾选的服之间轮流发 connect，满了就换下一个";
        else mode = "单一 IP：只挤列表里选中的这一个服";
        int nsel = (lvSrv == null) ? 0 : lvSrv.SelectedItems.Count;
        string multi = (nsel > 1) ? ("（已选 " + nsel + " 行，单一 IP 用焦点行）") : "";
        lblJoinHint.Text = string.Format(
            "单一 IP 目标：{0}{1}　｜　参与轮询：已勾选 {2} / {3} 个\r\n{4}",
            selTxt, multi, nchk, total, mode);
        if (lblListHint != null && total == 0 && m != 0)
            lblListHint.Text = "列表是空的：在下面填 IP:端口 再点「★ 加入收藏」，或复制地址点「粘贴加服」";
    }

    void SetAllChecks(bool on)
    {
        if (lvSrv == null) return;
        suppressListCheck = true;
        lvSrv.BeginUpdate();
        for (int i = 0; i < lvSrv.Items.Count; i++) lvSrv.Items[i].Checked = on;
        lvSrv.EndUpdate();
        suppressListCheck = false;
        SyncActiveTargets();
        UpdateJoinHint();
        RefreshUi(false);
    }

    int favPending = 0;
    // 启动首刷的补查：只对失败的服再查一次，手动刷新不补
    bool bootRefresh = false;
    System.Windows.Forms.Timer retryTimer;
    System.Collections.Generic.List<int> retryList = new System.Collections.Generic.List<int>();
    int retryPos = 0;
    bool favDirty = false;

    void RefreshAllFavs()
    {
        if (lvSrv == null) return;
        if (favList.Length == 0)
        {
            toast.ShowText("列表是空的，先加一个服务器");
            return;
        }
        if (favPending > 0) return;

        int myVer = Interlocked.Increment(ref favVersion);
        favPending = favList.Length;
        favDirty = false;
        if (btnRefreshFav != null) { btnRefreshFav.Enabled = false; btnRefreshFav.Text = "刷新中…"; }
        for (int i = 0; i < favList.Length && i < lvSrv.Items.Count; i++)
        {
            favCount[i] = -3;
            UpdateSrvRow(i);
        }
        UpdateJoinHint();

        for (int i = 0; i < favList.Length; i++)
        {
            int fi = i;
            string addrQ = AddrOf(favList[i]);
            int colon = addrQ.LastIndexOf(':');
            int pn2;
            if (colon <= 0 || !int.TryParse(addrQ.Substring(colon + 1), out pn2))
            {
                DoneOneFav(myVer, fi, addrQ, false, 0, 0, "");
                continue;
            }
            string host2 = addrQ.Substring(0, colon).Trim();
            Task.Run(delegate
            {
                string nm2 = "";
                int p3 = 0, m3 = 0;
                bool ok2 = false;
                try { ok2 = A2SQuery(host2, pn2, 1400, out nm2, out p3, out m3); }
                catch { ok2 = false; }
                int pv = ok2 ? p3 : 0, mv = ok2 ? m3 : 0;
                bool okv = ok2;
                string nmv = ok2 ? nm2 : "";
                SafeBeginInvoke(delegate { DoneOneFav(myVer, fi, addrQ, okv, pv, mv, nmv); });
            });
        }
    }

    // 启动首刷后，把失败的服再串行补查一遍（间隔 350ms、超时 2.5s），避免并发丢包
    void StartRetryFailedFavs()
    {
        retryList.Clear();
        for (int i = 0; i < favCount.Length; i++)
            if (favCount[i] < 0) retryList.Add(i);
        if (retryList.Count == 0) return;
        Native.Log("startup retry: " + retryList.Count + " failed servers");
        retryPos = 0;
        if (retryTimer == null)
        {
            retryTimer = new System.Windows.Forms.Timer();
            retryTimer.Interval = 350;
            retryTimer.Tick += delegate { RetryNextFav(); };
        }
        retryTimer.Start();
    }

    void RetryNextFav()
    {
        if (isClosing || retryPos >= retryList.Count)
        {
            if (retryTimer != null) retryTimer.Stop();
            Native.Log("startup retry done");
            return;
        }
        int i = retryList[retryPos++];
        if (i < 0 || i >= favList.Length || i >= favCount.Length) return;
        string addrQ = AddrOf(favList[i]);
        int colon = addrQ.LastIndexOf(':');
        int pn2;
        if (colon <= 0 || !int.TryParse(addrQ.Substring(colon + 1), out pn2)) return;
        string host2 = addrQ.Substring(0, colon).Trim();
        int fi = i;
        int myVer = favVersion;
        Task.Run(delegate
        {
            string nm2 = "";
            int p3 = 0, m3 = 0;
            bool ok2 = false;
            try { ok2 = A2SQuery(host2, pn2, 2500, out nm2, out p3, out m3); }
            catch { ok2 = false; }
            if (!ok2) return;                       // 还失败就保留「超时/失败」
            int pv = p3, mv = m3;
            string nmv = nm2;
            SafeBeginInvoke(delegate
            {
                if (myVer != favVersion) return;
                if (fi < 0 || fi >= favCount.Length) return;
                favCount[fi] = pv;
                favMax[fi] = mv;
                if (!string.IsNullOrEmpty(nmv) && fi < favName.Length) favName[fi] = nmv;
                if (CacheSrvName(fi, nmv)) SaveFavs();
                UpdateSrvRow(fi);
                UpdateJoinHint();
                Native.Log("startup retry ok: " + addrQ + " " + pv + "/" + mv);
            });
        });
    }

    void QueryFavOne(int i)
    {
        if (i < 0 || i >= favList.Length || i >= favCount.Length) return;
        int myVer = favVersion;
        string addrQ = AddrOf(favList[i]);
        int colon = addrQ.LastIndexOf(':');
        int pn2;
        if (colon <= 0 || !int.TryParse(addrQ.Substring(colon + 1), out pn2))
        {
            favCount[i] = -1;
            UpdateSrvRow(i);
            return;
        }
        string host2 = addrQ.Substring(0, colon).Trim();
        int fi = i;
        favCount[fi] = -3;
        UpdateSrvRow(fi);

        Task.Run(delegate
        {
            string nm2 = "";
            int p3 = 0, m3 = 0;
            bool ok2 = false;
            try { ok2 = A2SQuery(host2, pn2, 850, out nm2, out p3, out m3); }
            catch { ok2 = false; }
            int pv = ok2 ? p3 : 0, mv = ok2 ? m3 : 0;
            bool okv = ok2;
            SafeBeginInvoke(delegate
            {
                if (myVer != favVersion || fi >= favCount.Length || fi >= favList.Length) return;
                if (!string.Equals(AddrOf(favList[fi]), addrQ, StringComparison.OrdinalIgnoreCase)) return;
                favCount[fi] = okv ? pv : -1;
                favMax[fi] = okv ? mv : 0;
                if (okv && fi < favName.Length && !string.IsNullOrEmpty(nm2)) favName[fi] = nm2;
                if (okv && CacheSrvName(fi, nm2)) SaveFavs();
                UpdateSrvRow(fi);
                if (fi == SelectedFavIndex()) UpdateJoinHint();
            });
        });
    }

    bool CacheSrvName(int i, string name)
    {
        if (i < 0 || i >= favList.Length) return false;
        name = (name == null) ? "" : CleanCtrl(name.Replace('|', ' ').Trim(), true);
        if (name.Length == 0) return false;
        string[] parts = favList[i].Split('|');
        string cur = (parts.Length > 2) ? parts[2].Trim() : "";
        if (cur == name) return false;
        string alias = (parts.Length > 1) ? parts[1].Trim() : "";
        string addr = parts[0].Trim();
        favList[i] = addr + "|" + alias + "|" + name;
        return true;
    }

    void DoneOneFav(int ver, int fi, string expectedAddr, bool ok, int players, int maxP, string srvName)
    {
        if (ver != favVersion) return;
        if (fi >= 0 && fi < favCount.Length && fi < favList.Length
            && string.Equals(AddrOf(favList[fi]), expectedAddr, StringComparison.OrdinalIgnoreCase))
        {
            favCount[fi] = ok ? players : -1;
            favMax[fi] = ok ? maxP : 0;
            if (ok && fi < favName.Length && !string.IsNullOrEmpty(srvName)) favName[fi] = srvName;
            if (ok && CacheSrvName(fi, srvName)) favDirty = true;
            UpdateSrvRow(fi);
        }
        if (favPending > 0) favPending--;
        if (favPending <= 0)
        {
            favPending = 0;
            if (bootRefresh)
            {
                bootRefresh = false;
                StartRetryFailedFavs();
            }
            if (favDirty) { favDirty = false; SaveFavs(); }
            if (btnRefreshFav != null) { btnRefreshFav.Enabled = true; btnRefreshFav.Text = "↻ 刷新人数"; }
            int openSlots = 0, okTotal = 0;
            for (int k = 0; k < favCount.Length; k++)
            {
                if (favCount[k] >= 0)
                {
                    okTotal++;
                    if (favMax[k] > 0 && favCount[k] < favMax[k]) openSlots++;
                }
            }
            if (okTotal > 0)
                toast.ShowText("刷新完成：" + openSlots + " 个服有空位（共响应 " + okTotal + "/" + favCount.Length + "）");
            UpdateJoinHint();
            RefreshUi(true);
        }
    }

    void AddFavEntry(string addr, string aliasInput, string srvRealName)
    {
        addr = (addr == null) ? "" : addr.Trim();
        if (addr.Length == 0) return;
        for (int i = 0; i < favList.Length; i++)
            if (string.Equals(AddrOf(favList[i]), addr, StringComparison.OrdinalIgnoreCase)) return;

        string alias = (aliasInput == null) ? "" : CleanCtrl(aliasInput.Replace('|', ' ').Trim(), true);
        string real = (srvRealName == null) ? "" : CleanCtrl(srvRealName.Replace('|', ' ').Trim(), true);
        var nf = new string[favList.Length + 1];
        Array.Copy(favList, nf, favList.Length);
        nf[nf.Length - 1] = addr + "|" + alias + "|" + real;
        favList = nf;
        SaveFavs();
        FillSrvList(addr);
        Native.Log("fav add: " + addr + " alias=" + alias + " real=" + real);
        toast.ShowText("已加入收藏：" + (alias.Length > 0 ? alias : (real.Length > 0 ? real : addr)));
        QueryFavOne(SelectedFavIndex());
    }

    void PasteAndAddFav()
    {
        try
        {
            string clip = Clipboard.ContainsText() ? (Clipboard.GetText() ?? "").Trim() : "";
            if (clip.Length == 0)
            {
                toast.ShowText("剪贴板为空，请先复制服务器 IP:端口");
                return;
            }
            // 支持从一段带字的群消息里提取 IP:端口
            Match m = Regex.Match(clip, @"([a-zA-Z0-9\.\-]+:\d{2,5})");
            string candidate = m.Success ? m.Groups[1].Value : clip;
            string host;
            int port;
            if (!ParseHostPort(candidate, out host, out port))
            {
                toast.ShowText("剪贴板里没找到有效的 IP:端口");
                return;
            }
            if (txtNewIp != null) txtNewIp.Text = host + ":" + port;
            AddFavFromBox();
        }
        catch (Exception ex)
        {
            toast.ShowText("读取剪贴板失败：" + ex.Message);
        }
    }

    void AddFavFromBox()
    {
        string ip = (txtNewIp == null) ? "" : txtNewIp.Text.Trim();
        string alias = (txtNewAlias == null) ? "" : txtNewAlias.Text.Trim();
        string host;
        int port;
        if (!ParseHostPort(ip, out host, out port))
        {
            toast.ShowText("格式写成 IP:端口，例如 43.249.193.77:27076");
            return;
        }
        string addr = host + ":" + port;
        for (int i = 0; i < favList.Length; i++)
            if (string.Equals(AddrOf(favList[i]), addr, StringComparison.OrdinalIgnoreCase))
            {
                toast.ShowText("这个地址已经在列表里了");
                return;
            }

        toast.ShowText("正在查询服务器信息…");
        string h = host;
        int p = port;
        string al = alias;
        Task.Run(delegate
        {
            string nm = null;
            try
            {
                string n0;
                int pc, mc;
                if (A2SQuery(h, p, 900, out n0, out pc, out mc)) nm = n0;
            }
            catch { }
            SafeBeginInvoke(delegate
            {
                AddFavEntry(h + ":" + p, al, nm);
                if (txtNewIp != null) txtNewIp.Text = "";
                if (txtNewAlias != null) txtNewAlias.Text = "";
            });
        });
    }

    void DeleteSelectedFav()
    {
        if (lvSrv == null) return;
        var idxs = SelectedIndexList();
        if (idxs.Count == 0)
        {
            toast.ShowText("先在列表里选中要删除的服务器");
            return;
        }
        string what = (idxs.Count == 1) ? AddrOf(favList[idxs[0]]) : (idxs.Count + " 个服务器");
        if (MessageBox.Show("从收藏里删除 " + what + " ？\r\n\r\n（只删本地列表，不影响服务器）",
                "RX 工具箱", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
            return;

        var keep = new List<string>();
        for (int i = 0; i < favList.Length; i++)
            if (!idxs.Contains(i)) keep.Add(favList[i]);
        favList = keep.ToArray();
        SaveFavs();
        FillSrvList();
        SyncActiveTargets();
        Native.Log("fav del " + idxs.Count + " rows");
        toast.ShowText("已删除 " + what);
    }

    static bool ParseHostPort(string raw, out string host, out int port)
    {
        host = "";
        port = 0;
        if (raw == null) return false;
        string ip = raw.Trim();
        if (ip.StartsWith("steam://connect/", StringComparison.OrdinalIgnoreCase))
            ip = ip.Substring(16).Trim();
        if (ip.StartsWith("connect ", StringComparison.OrdinalIgnoreCase))
            ip = ip.Substring(8).Trim();
        ip = ip.TrimEnd('/');
        int c = ip.LastIndexOf(':');
        string h = (c > 0) ? ip.Substring(0, c).Trim() : ip;
        string pt = (c > 0) ? ip.Substring(c + 1).Trim() : "27015";
        int pv;
        if (h.Length == 0 || h.Length > 253 || !int.TryParse(pt, out pv) || pv <= 0 || pv > 65535) return false;
        for (int i = 0; i < h.Length; i++)
        {
            char ch = h[i];
            bool okCh = (ch >= '0' && ch <= '9') || (ch >= 'a' && ch <= 'z')
                     || (ch >= 'A' && ch <= 'Z') || ch == '.' || ch == '-';
            if (!okCh) return false;
        }
        host = h;
        port = pv;
        return true;
    }

    bool autoNameDone = false;


    void EditSelectedFav()
    {
        int ix = SelectedFavIndex();
        if (ix < 0 || ix >= favList.Length)
        {
            toast.ShowText("先在列表里选中要编辑的服务器");
            return;
        }
        ShowEditFav(ix);
    }

    void ShowEditFav(int ix)
    {
        if (ix < 0 || ix >= favList.Length) return;
        string[] parts = favList[ix].Split('|');
        string oldAddr = parts[0].Trim();
        string oldAlias = (parts.Length > 1) ? parts[1].Trim() : "";
        string oldName = (parts.Length > 2) ? parts[2].Trim() : "";

        DialogResult dr;
        string alias;
        string rawIp;
        using (var f = new Form())
        {
            f.Text = "编辑收藏 · " + oldAddr;
            f.FormBorderStyle = FormBorderStyle.FixedDialog;
            f.StartPosition = FormStartPosition.CenterParent;
            f.MinimizeBox = false;
            f.MaximizeBox = false;
            f.ShowInTaskbar = false;
            f.ClientSize = new Size(470, 240);
            f.BackColor = Color.White;
            f.Font = TrackFont("Microsoft YaHei UI", 9.5f, FontStyle.Regular);

            var l1 = new Label();
            l1.Text = "备注名（列表里显示的短名字，留空则清空备注）";
            l1.ForeColor = Color.FromArgb(70, 80, 95);
            l1.SetBounds(14, 12, 440, 20);
            f.Controls.Add(l1);

            var tAlias = new TextBox();
            tAlias.SetBounds(14, 34, 440, 26);
            tAlias.Text = oldAlias;
            f.Controls.Add(tAlias);

            var l2 = new Label();
            l2.Text = "IP:端口（服务器换 IP 就改这里）";
            l2.ForeColor = Color.FromArgb(70, 80, 95);
            l2.SetBounds(14, 72, 440, 20);
            f.Controls.Add(l2);

            var tIp = new TextBox();
            tIp.SetBounds(14, 94, 440, 26);
            tIp.Text = oldAddr;
            f.Controls.Add(tIp);

            var hint = new Label();
            hint.Text = "改 IP 会自动重新探测真名和人数；备注名只影响显示，不影响连接。";
            hint.ForeColor = Color.FromArgb(120, 126, 136);
            hint.SetBounds(14, 128, 440, 40);
            f.Controls.Add(hint);

            var bOk = new Button();
            bOk.Text = "保存";
            bOk.SetBounds(262, 194, 92, 32);
            bOk.DialogResult = DialogResult.OK;
            f.Controls.Add(bOk);

            var bNo = new Button();
            bNo.Text = "取消";
            bNo.SetBounds(366, 194, 88, 32);
            bNo.DialogResult = DialogResult.Cancel;
            f.Controls.Add(bNo);

            f.AcceptButton = bOk;
            f.CancelButton = bNo;
            tAlias.SelectAll();
            tAlias.Focus();

            dr = f.ShowDialog(this);
            alias = CleanCtrl(tAlias.Text.Replace('|', ' ').Trim(), true);
            rawIp = tIp.Text.Trim();
        }
        if (dr != DialogResult.OK) return;

        string newHost;
        int newPort;
        if (!ParseHostPort(rawIp, out newHost, out newPort))
        {
            toast.ShowText("IP 格式不对：写成 IP:端口，例如 1.2.3.4:27015");
            return;
        }
        string newAddr = newHost + ":" + newPort;
        bool addrChanged = !string.Equals(newAddr, oldAddr, StringComparison.OrdinalIgnoreCase);

        if (addrChanged)
        {
            for (int i = 0; i < favList.Length; i++)
            {
                if (i == ix) continue;
                if (string.Equals(AddrOf(favList[i]), newAddr, StringComparison.OrdinalIgnoreCase))
                {
                    toast.ShowText("列表里已经有 " + newAddr + " 了");
                    return;
                }
            }
        }

        favList[ix] = newAddr + "|" + alias + "|" + (addrChanged ? "" : oldName);
        SaveFavs();
        FillSrvList(newAddr);
        SyncActiveTargets();
        Native.Log("fav edit: " + oldAddr + " -> " + favList[ix]);
        if (!addrChanged)
        {
            toast.ShowText(alias.Length > 0 ? ("已保存备注名：" + alias) : "已清空备注名");
            return;
        }
        toast.ShowText("已改到 " + newAddr + "，正在重新探测");
        for (int i = 0; i < favList.Length; i++)
            if (string.Equals(AddrOf(favList[i]), newAddr, StringComparison.OrdinalIgnoreCase)) { QueryFavOne(i); break; }
    }

    void SetChecksOnSelection(bool on)
    {
        if (lvSrv == null) return;
        var idxs = SelectedIndexList();
        if (idxs.Count == 0)
        {
            toast.ShowText("先选中几行");
            return;
        }
        suppressListCheck = true;
        for (int k = 0; k < idxs.Count; k++)
            if (idxs[k] < lvSrv.Items.Count) lvSrv.Items[idxs[k]].Checked = on;
        suppressListCheck = false;
        SyncActiveTargets();
        UpdateJoinHint();
        RefreshUi(false);
    }

    void RefreshSelectedFavs()
    {
        var idxs = SelectedIndexList();
        if (idxs.Count == 0)
        {
            toast.ShowText("先选中几行");
            return;
        }
        for (int k = 0; k < idxs.Count; k++) QueryFavOne(idxs[k]);
        toast.ShowText("正在刷新 " + idxs.Count + " 个服务器的人数…");
    }

    bool BuildTargets(int mode, bool quiet)
    {
        var nextTargets = new List<string>();
        if (mode == 2)
        {
            if (lvSrv != null)
                for (int i = 0; i < lvSrv.Items.Count; i++)
                    if (lvSrv.Items[i].Checked)
                    {
                        string a = Convert.ToString(lvSrv.Items[i].Tag);
                        if (!string.IsNullOrEmpty(a)) nextTargets.Add(a);
                    }
            if (nextTargets.Count == 0)
            {
                if (!quiet)
                {
                    toast.ShowText("收藏轮询：先在列表里勾选至少一个服务器");
                    Native.Log("start: poll but nothing checked");
                }
                return false;
            }
        }
        else
        {
            string a = SelectedAddr();
            if (a.Length == 0)
            {
                string typed = (txtNewIp == null) ? "" : txtNewIp.Text.Trim();
                string h; int p;
                if (ParseHostPort(typed, out h, out p)) a = h + ":" + p;
            }
            if (a.Length == 0 && favList.Length > 0) a = AddrOf(favList[0]);
            if (a.Length == 0)
            {
                if (!quiet) toast.ShowText("单一 IP：先在列表里点选一个服务器，或填 IP:端口");
                return false;
            }
            nextTargets.Add(a);
        }
        targets = nextTargets;
        if (multiIdx >= targets.Count) multiIdx = 0;
        if (!quiet)
        {
            Native.Log("start: mode=" + (mode == 2 ? "poll" : "one") + " targets=" + targets.Count
                + " first=" + targets[0]);
        }
        return true;
    }

    void SyncActiveTargets()
    {
        if (!autoOn || !typingOn) return;
        int m = JoinModeIdx;
        if (m == 1 || m == 2)
        {
            if (!BuildTargets(m, true))
            {
                StopActiveJoin("目标服务器已全部取消，挤服自动停止", "stop: targets empty");
            }
        }
    }

    void UpdateStartButton()
    {
        if (btnAuto == null) return;
        bool on = (autoOn || typingOn);
        btnAuto.Text = on ? ("■ 停止挤服 (" + KeyName(2) + ")") : ("▶ 开始挤服 (" + KeyName(2) + ")");
        btnAuto.BackColor = on ? Color.FromArgb(254, 215, 215) : Color.FromArgb(224, 240, 255);
        btnAuto.ForeColor = on ? Color.FromArgb(153, 27, 27) : Color.FromArgb(12, 62, 130);
        btnAuto.FlatAppearance.BorderColor = on ? Color.FromArgb(248, 150, 150) : Color.FromArgb(140, 185, 240);
    }

    // 统一权威停止入口：确保任何原因停止时都还原输入法、还原剪贴板、复位全部状态
    void StopActiveJoin(string toastMsg, string logReason)
    {
        bool wasTyping = typingOn;
        ocrHoldSend = false;
        ocrLoadingHits = 0;
        autoOn = false;
        typingOn = false;
        currentTargetAddr = "";

        if (wasTyping)
        {
            if (savedLayout != IntPtr.Zero)
            {
                IntPtr gameBack = Native.FindGameWindow();
                if (gameBack != IntPtr.Zero)
                {
                    Native.RestoreLayout(gameBack, savedLayout);
                    Native.Log("ime: restored");
                }
                savedLayout = IntPtr.Zero;
            }
            RestoreClipboardAfterStop();
        }

        RefreshActiveTargetRowHighlight();
        UpdateStartButton();
        if (!string.IsNullOrEmpty(logReason)) Native.Log(logReason);
        if (!string.IsNullOrEmpty(toastMsg)) toast.ShowText(toastMsg);
        RefreshUi(true);
    }

    // 停止挤服后收尾：剪贴板里如果还是我们写的 connect，就还原成玩家原来的内容（没有就清空）
    void RestoreClipboardAfterStop()
    {
        try
        {
            string cur = "";
            try { if (Clipboard.ContainsText()) cur = Clipboard.GetText(); } catch { }
            bool ours = cur != null && cur.StartsWith("connect ", StringComparison.OrdinalIgnoreCase) && cur.Length < 120;
            if (!ours)
            {
                // 玩家自己复制了新内容，不动它
                return;
            }
            if (!string.IsNullOrEmpty(savedClipboardText))
            {
                if (TrySetClipboardText(savedClipboardText)) Native.Log("clipboard: restored original");
                else { try { Clipboard.Clear(); } catch { } }
            }
            else
            {
                try { Clipboard.Clear(); Native.Log("clipboard: cleared connect residue"); } catch { }
            }
        }
        catch (Exception ex) { Native.Log("clipboard restore EX: " + ex.Message); }
    }

    void ToggleStart()
    {
        if (autoOn || typingOn)
        {
            StopActiveJoin("挤服：已停止", "stop: user toggle");
            return;
        }

        int m = JoinModeIdx;
        joinedStop = false;
        tapOcrFail = 0;
        tapOcrAt = DateTime.MinValue;
        lastEnter = DateTime.MinValue;

        if (m == 0)
        {
            typingOn = false;
            autoOn = true;
            currentTargetAddr = "";
            UpdateStartButton();
            toast.ShowText("手动档已开启：保持游戏在前台，按 " + KeyName(2) + " 可随时停止");
            Native.Log("start: mode=tap");
            RefreshUi(true);
            return;
        }

        if (!BuildTargets(m, false)) return;

        try
        {
            if (Clipboard.ContainsText())
            {
                string curClip = Clipboard.GetText();
                if (!string.IsNullOrEmpty(curClip) && !curClip.StartsWith("connect ", StringComparison.OrdinalIgnoreCase))
                    savedClipboardText = curClip;
            }
        }
        catch { }

        typingOn = true;
        autoOn = true;
        multiIdx = 0;
        currentTargetAddr = targets[0];

        IntPtr gw = Native.FindGameWindow();
        if (gw != IntPtr.Zero)
        {
            if (Native.GetForegroundWindow() != gw)
            {
                try
                {
                    if (Native.IsIconic(gw)) Native.ShowWindow(gw, 9);
                    else Native.ShowWindow(gw, 5);
                    Native.SetForegroundWindow(gw);
                }
                catch { }
            }
            savedLayout = Native.SwitchToEnglish(gw);
            Native.Log("ime: switched to english, old=" + savedLayout.ToInt64());
        }
        else
        {
            Native.Log("paste: game window not found at start");
        }

        RefreshActiveTargetRowHighlight();
        UpdateStartButton();
        string what = (targets.Count > 1)
            ? ("正在轮询 " + targets.Count + " 个服")
            : ("正在挤 " + targets[0]);
        toast.ShowText("已开启（打开游戏控制台 ~ 即可自动粘贴），" + what);
        RefreshUi(true);
    }

    int IntervalMs
    {
        get
        {
            if (cmbSpeed == null) return 200;
            switch (cmbSpeed.SelectedIndex)
            {
                case 0: return 100;
                case 1: return 200;
                case 2: return 300;
                case 3: return 500;
                default: return 900;
            }
        }
    }

    // ===== Windows 自带 OCR（后台非阻塞 + 引擎缓存 + 严格释放）=====
    static T WaitOp<T>(Windows.Foundation.IAsyncOperation<T> op, int timeoutMs)
    {
        if (op == null) return default(T);
        using (var done = new ManualResetEventSlim(false))
        {
            T result = default(T);
            op.Completed = delegate(Windows.Foundation.IAsyncOperation<T> o, Windows.Foundation.AsyncStatus s)
            {
                try
                {
                    if (s == Windows.Foundation.AsyncStatus.Completed)
                        result = o.GetResults();
                }
                catch { }
                finally
                {
                    try { done.Set(); } catch { }
                }
            };
            if (!done.Wait(timeoutMs))
            {
                try { op.Cancel(); } catch { }
            }
            return result;
        }
    }

    string OcrGameWindow(IntPtr hwnd)
    {
        try
        {
            Native.RECT r;
            if (!Native.GetWindowRect(hwnd, out r)) return null;
            int w = r.R - r.L, h = r.B - r.T;
            if (w < 400 || h < 300) return null;

            byte[] png;
            using (var bmp = new Bitmap(w, h))
            {
                using (var g = Graphics.FromImage(bmp))
                    g.CopyFromScreen(r.L, r.T, 0, 0, new Size(w, h), CopyPixelOperation.SourceCopy);
                int nw = Math.Min(1280, w);
                int nh = Math.Max(1, h * nw / w);
                using (var small = new Bitmap(bmp, nw, nh))
                using (var ms2 = new MemoryStream())
                {
                    small.Save(ms2, System.Drawing.Imaging.ImageFormat.Png);
                    png = ms2.ToArray();
                }
            }

            if (!ocrEngineInitTried)
            {
                ocrEngineInitTried = true;
                try { cachedOcrEngine = OcrEngine.TryCreateFromUserProfileLanguages(); }
                catch { cachedOcrEngine = null; }
            }
            if (cachedOcrEngine == null) return null;

            using (var ras = new InMemoryRandomAccessStream())
            {
                using (var writer = new DataWriter(ras))
                {
                    writer.WriteBytes(png);
                    WaitOp(writer.StoreAsync(), 2500);
                    try { writer.DetachStream(); } catch { }
                }
                ras.Seek(0);

                var decoder = WaitOp(BitmapDecoder.CreateAsync(ras), 2500);
                if (decoder == null) return null;
                using (var sb = WaitOp(decoder.GetSoftwareBitmapAsync(), 2500))
                {
                    if (sb == null) return null;
                    var res = WaitOp(cachedOcrEngine.RecognizeAsync(sb), 3000);
                    if (res == null) return null;

                    var sbText = new StringBuilder();
                    foreach (var line in res.Lines) { sbText.Append(line.Text); sbText.Append('\n'); }
                    return sbText.ToString();
                }
            }
        }
        catch (Exception ex)
        {
            Native.Log("ocr fail: " + ex.Message);
            return null;
        }
    }

    string OcrScene()
    {
        IntPtr gw = Native.FindGameWindow();
        if (gw == IntPtr.Zero) return "nogame";
        string text = OcrGameWindow(gw);
        if (text == null) return "fail";

        // OCR 结果里中文字之间常带空格（"服 务 器 列 表"），先去掉空白再匹配
        string flat = Regex.Replace(text, "\\s+", "");

        // ⓪ 主菜单（开始新游戏/寻找多人游戏/插件/副本/存档/回放/选项/退出）
        //    满员被踢回来就是这个画面，绝对不能当成"已进服"
        if (flat.IndexOf("开始新游戏", StringComparison.Ordinal) >= 0
            || flat.IndexOf("寻找多人游戏", StringComparison.Ordinal) >= 0
            || flat.IndexOf("寻找服务器", StringComparison.Ordinal) >= 0
            || flat.IndexOf("开始游戏", StringComparison.Ordinal) >= 0
            || flat.IndexOf("创意工坊", StringComparison.Ordinal) >= 0)
            return "menu";

        // ① 服务器列表 / 收藏夹：只要还看得见列表页，就绝不算"进服"。
        //    挤服时屏上同时有列表 + 控制台，这一条是防误判的第一道闸。
        if (flat.IndexOf("服务器列表", StringComparison.Ordinal) >= 0
            || flat.IndexOf("当服务器有空位", StringComparison.Ordinal) >= 0
            || flat.IndexOf("加入游戏", StringComparison.Ordinal) >= 0
            || flat.IndexOf("服务器名称", StringComparison.Ordinal) >= 0
            || flat.IndexOf("收藏夹", StringComparison.Ordinal) >= 0
            || flat.IndexOf("历史记录", StringComparison.Ordinal) >= 0)
            return "list";

        // ② 强进服信号：加载画面上的"加载插件/加载地图"。
        //    这两句只在读条时出现，控制台日志里没有，所以放在控制台判断之前。
        if (flat.IndexOf("加载插件", StringComparison.Ordinal) >= 0
            || flat.IndexOf("加载地图", StringComparison.Ordinal) >= 0
            || flat.IndexOf("正在加载插件", StringComparison.Ordinal) >= 0
            || flat.IndexOf("正在加载地图", StringComparison.Ordinal) >= 0)
            return "loading_plugin";

        // ③ 控制台日志 / 满员提示：挤服全程控制台都开着，屏上是 "Connecting to…"
        //    "Disconnected…" 这类文本，里面混着 Loading 字样也不能当读条。
        if (flat.IndexOf("CONNREJECT", StringComparison.OrdinalIgnoreCase) >= 0
            || flat.IndexOf("Disconnected", StringComparison.OrdinalIgnoreCase) >= 0
            || flat.IndexOf("Connectingto", StringComparison.OrdinalIgnoreCase) >= 0
            || flat.IndexOf("Attemptedtoprecache", StringComparison.OrdinalIgnoreCase) >= 0
            || flat.IndexOf("WorkshopID", StringComparison.OrdinalIgnoreCase) >= 0
            || flat.IndexOf("人数已满", StringComparison.Ordinal) >= 0
            || flat.IndexOf("服务器已满", StringComparison.Ordinal) >= 0)
            return "console";

        // ④ 弱信号：只有读条界面才有的词，同样排除控制台干扰
        if (flat.IndexOf("加载中", StringComparison.Ordinal) >= 0
            || flat.IndexOf("正在加载", StringComparison.Ordinal) >= 0
            || flat.IndexOf("Loadingmap", StringComparison.OrdinalIgnoreCase) >= 0
            || flat.IndexOf("Loadingaddons", StringComparison.OrdinalIgnoreCase) >= 0)
            return "loading";

        if (flat.IndexOf("连接服务器", StringComparison.Ordinal) >= 0
            || flat.IndexOf("Connecting", StringComparison.OrdinalIgnoreCase) >= 0)
            return "connecting";

        if (flat.Length < 8) return "blank";
        return "other";
    }

    void TriggerOcrCheckAsync()
    {
        if (Interlocked.CompareExchange(ref ocrBusy, 1, 0) != 0) return;
        tapOcrAt = DateTime.Now;
        Task.Run(delegate
        {
            string scene = "fail";
            try { scene = OcrScene(); }
            catch { scene = "fail"; }
            finally { Interlocked.Exchange(ref ocrBusy, 0); }

            SafeBeginInvoke(delegate
            {
                if (!autoOn) return;
                if (scene != lastOcrScene) { lastOcrScene = scene; Native.Log("enter-mode: ocr scene=" + scene); }
                if (scene == "loading_plugin")
                {
                    // 「加载插件中」是读条界面常驻文案，连续两帧读到才认进服。
                    // 第一帧就先停止发 connect，避免在确认期间又把人踢回菜单。
                    ocrLoadingHits++;
                    ocrHoldSend = true;
                    Native.Log("enter-mode: ocr 加载插件 hit " + ocrLoadingHits + "/2 (暂停发送)");
                    if (ocrLoadingHits < 2) return;
                    joinedStop = true;
                    ocrLoadingHits = 0;
                    StopActiveJoin("已进服（加载插件画面），已自动停止", "enter-mode: ocr 加载插件 x2 -> joined, stop");
                    return;
                }
                if (scene == "loading")
                {
                    // 弱信号（加载中/Loadingmap…）不常驻，单帧不可信，只记日志并恢复发送
                    Native.Log("enter-mode: ocr weak loading ignored");
                    ocrLoadingHits = 0;
                    ocrHoldSend = false;
                    return;
                }
                ocrLoadingHits = 0;
                ocrHoldSend = false;
                // menu / list / console / other 一律视为"还没进服"，什么都不做
                if (scene == "fail")
                {
                    tapOcrFail++;
                    Native.Log("enter-mode: ocr fail " + tapOcrFail + "/3");
                    if (tapOcrFail >= 3)
                    {
                        tapOcrFail = 0;
                        if (chkOcr != null) chkOcr.Checked = false;
                        SaveSettings();
                        Native.Log("enter-mode: ocr disabled automatically");
                        toast.ShowText("当前系统不支持 OCR，已自动关闭进服识别（按 " + KeyName(2) + " 手动停）");
                    }
                }
                else tapOcrFail = 0;
            });
        });
    }

    // ================= 可自定义热键 =================

    static string VkName(int vk)
    {
        switch (vk)
        {
            case 0x30: return "0"; case 0x31: return "1"; case 0x32: return "2";
            case 0x33: return "3"; case 0x34: return "4"; case 0x35: return "5";
            case 0x36: return "6"; case 0x37: return "7"; case 0x38: return "8";
            case 0x39: return "9";
            case 0xC0: return "`";
            case 0xBD: return "-";
            case 0xBB: return "=";
            case 0xDB: return "[";
            case 0xDD: return "]";
            case 0xDC: return "\\";
            case 0xBA: return ";";
            case 0xDE: return "'";
            case 0xBC: return ",";
            case 0xBE: return ".";
            case 0xBF: return "/";
        }
        try { return ((Keys)vk).ToString(); }
        catch { return "?"; }
    }

    string KeyName(int id)
    {
        int i = id - 1;
        if (i < 0 || i > 4) return "?";
        var sb = new StringBuilder();
        if ((hkMod[i] & 0x0002) != 0) sb.Append("Ctrl+");
        if ((hkMod[i] & 0x0001) != 0) sb.Append("Alt+");
        if ((hkMod[i] & 0x0004) != 0) sb.Append("Shift+");
        sb.Append(VkName(hkVk[i]));
        return sb.ToString();
    }

    static bool IsHotkeyDown(int vk, int mod)
    {
        if (vk <= 0 || !Native.Down(vk)) return false;
        bool ctrl = Native.Down(0x11);
        bool alt = Native.Down(0x12);
        bool shift = Native.Down(0x10);
        if (((mod & 0x0002) != 0) != ctrl) return false;
        if (((mod & 0x0001) != 0) != alt) return false;
        if (((mod & 0x0004) != 0) != shift) return false;
        return true;
    }

    // ================= 关闭热键（F5/F6… 交还给系统） =================
    const int HK_TOGGLE_ID = 6;
    const int HK_TOGGLE_VK = 0x79;   // F10
    const int HK_TOGGLE_MOD = 0x0002; // Ctrl

    string ToggleKeyName() { return "Ctrl+F10"; }

    void ReleaseHotkeys()
    {
        for (int id = 1; id <= 5; id++)
        {
            try { if (hkRegistered[id]) Native.UnregisterHotKey(Handle, id); } catch { }
            hkRegistered[id] = false;
        }
        Native.Log("hotkeys released (off)");
    }

    void ToggleHotkeys()
    {
        hotkeysOff = !hotkeysOff;
        if (hotkeysOff) ReleaseHotkeys();
        else ApplyHotkeys();
        UpdateHotkeyToggleButton();
        SaveSettings();
        try { if (toast != null) toast.ShowText(hotkeysOff ? "热键已关闭，" + ToggleKeyName() + " 可恢复" : "热键已恢复"); } catch { }
    }

    void UpdateHotkeyToggleButton()
    {
        if (btnHotkeyOff == null) return;
        btnHotkeyOff.Text = hotkeysOff ? ("开启热键 (" + ToggleKeyName() + ")") : ("关闭热键 (" + ToggleKeyName() + ")");
        btnHotkeyOff.ForeColor = hotkeysOff ? Color.FromArgb(180, 100, 20) : Color.FromArgb(60, 90, 140);
        btnHotkeyOff.FlatAppearance.BorderColor = hotkeysOff
            ? Color.FromArgb(232, 190, 120) : Color.FromArgb(180, 205, 238);
    }

    int RegisterToggleHotkey()
    {
        try { if (hkRegistered[HK_TOGGLE_ID]) Native.UnregisterHotKey(Handle, HK_TOGGLE_ID); } catch { }
        hkRegistered[HK_TOGGLE_ID] = false;
        if (!IsHandleCreated || isClosing) return 0;
        try
        {
            hkRegistered[HK_TOGGLE_ID] = Native.RegisterHotKey(Handle, HK_TOGGLE_ID, (uint)HK_TOGGLE_MOD, (uint)HK_TOGGLE_VK);
        }
        catch { hkRegistered[HK_TOGGLE_ID] = false; }
        return hkRegistered[HK_TOGGLE_ID] ? 0 : 1;
    }

    int ApplyHotkeys()
    {
        int fail = 0;
        for (int id = 1; id <= 5; id++)
        {
            try { if (hkRegistered[id]) { Native.UnregisterHotKey(Handle, id); } } catch { }
            hkRegistered[id] = false;
        }
        if (!IsHandleCreated || isClosing) return 0;
        for (int id = 1; id <= 5; id++)
        {
            try { hkRegistered[id] = Native.RegisterHotKey(Handle, id, (uint)hkMod[id - 1], (uint)hkVk[id - 1]); }
            catch { hkRegistered[id] = false; }
            if (!hkRegistered[id]) fail++;
        }
        Native.Log("hotkeys: " + string.Join(" ", new string[] {
            KeyName(1) + "=" + hkRegistered[1], KeyName(2) + "=" + hkRegistered[2],
            KeyName(3) + "=" + hkRegistered[3], KeyName(4) + "=" + hkRegistered[4],
            KeyName(5) + "=" + hkRegistered[5] }));
        RegisterToggleHotkey();
        return fail;
    }

    void UpdateHotkeyLabels()
    {
        try
        {
            if (btnAuto != null) UpdateStartButton();
            if (btnCross != null) btnCross.Text = "准星开关 (" + KeyName(4) + ")";
            if (btnColor != null) btnColor.Text = "换颜色 (" + KeyName(3) + ")";
            if (lblTapGuide != null)
            {
                string hk = KeyName(2);
                string[] tapGuideLines = new string[] {
                    "手动档：你在游戏里点「连接」，工具负责连按回车。",
                    "",
                    "1. 打开游戏 → Play → 服务器列表，选中目标服务器，点「连接」；",
                    "2. 人数满了会弹「服务器人数已满」，不用点它；",
                    "3. 直接在游戏里按 " + hk + "（不用切回工具，" + hk + " 是全局热键），",
                    "    工具会按你设的间隔不停按回车，替你关掉弹框、重新发起连接；",
                    "4. 挤进去了按 " + hk + " 停（勾了 OCR 识别会自动停）。",
                    "",
                    "提示：游戏保持在前台，工具只往游戏窗口发按键。",
                    "",
                    "细节和原理点上方的「?」看使用指南。"
                };
                lblTapGuide.Text = string.Join(Environment.NewLine, tapGuideLines);
            }
            if (lblCrossHint != null)
            {
                lblCrossHint.Text = "快捷键：\r\n"
                    + "· " + KeyName(1) + " ：循环切换准星样式（6 种）\r\n"
                    + "· " + KeyName(3) + " ：循环切换准星颜色（6 色）\r\n"
                    + "· " + KeyName(4) + " ：开启 / 关闭桌面置顶准星\r\n"
                    + "· " + KeyName(5) + " ：立即安全退出工具箱\r\n\r\n"
                    + "提示：准星为透明穿透置顶层，建议游戏设置使用「无边框窗口化」以获得最佳显示效果。";
            }
            UpdateJoinHint();
        }
        catch { }
    }

    bool CaptureKey(out int vk, out int mod)
    {
        int cvk = 0, cmod = 0;
        bool got = false;
        capturingKey = true;
        int[] oldVk = (int[])hkVk.Clone();
        int[] oldMod = (int[])hkMod.Clone();
        for (int id = 1; id <= 5; id++)
        {
            try { if (hkRegistered[id]) Native.UnregisterHotKey(Handle, id); } catch { }
            hkRegistered[id] = false;
        }

        using (var f = new Form())
        {
            f.Text = "按下你要用的键";
            f.FormBorderStyle = FormBorderStyle.FixedDialog;
            f.StartPosition = FormStartPosition.CenterParent;
            f.MinimizeBox = false;
            f.MaximizeBox = false;
            f.ShowInTaskbar = false;
            f.KeyPreview = true;
            f.ClientSize = new Size(420, 150);
            f.BackColor = Color.White;

            var lb = new Label();
            lb.Text = "请按下要绑定的键（可带 Ctrl / Alt / Shift）\r\n按 Esc 取消";
            lb.Font = TrackFont("Microsoft YaHei UI", 10.5f, FontStyle.Regular);
            lb.TextAlign = ContentAlignment.MiddleCenter;
            lb.SetBounds(16, 26, 388, 60);
            f.Controls.Add(lb);

            var lbNow = new Label();
            lbNow.Text = "（等待按键…）";
            lbNow.Font = TrackFont("Microsoft YaHei UI", 12f, FontStyle.Bold);
            lbNow.ForeColor = Color.FromArgb(20, 80, 150);
            lbNow.TextAlign = ContentAlignment.MiddleCenter;
            lbNow.SetBounds(16, 90, 388, 34);
            f.Controls.Add(lbNow);

            f.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                int k = (int)e.KeyCode;
                if (k == 0x10 || k == 0x11 || k == 0x12 || k == 0x5B || k == 0x5C || k == 0xA0 || k == 0xA1 || k == 0xA2 || k == 0xA3 || k == 0xA4 || k == 0xA5)
                    return;
                if (k == 0x1B)
                {
                    f.DialogResult = DialogResult.Cancel;
                    f.Close();
                    return;
                }
                int m = 0;
                if (e.Control) m |= 0x0002;
                if (e.Alt) m |= 0x0001;
                if (e.Shift) m |= 0x0004;
                cvk = k;
                cmod = m;
                got = true;
                var sb = new StringBuilder();
                if ((m & 0x0002) != 0) sb.Append("Ctrl+");
                if ((m & 0x0001) != 0) sb.Append("Alt+");
                if ((m & 0x0004) != 0) sb.Append("Shift+");
                sb.Append(VkName(k));
                lbNow.Text = sb.ToString();
                f.DialogResult = DialogResult.OK;
                f.Close();
            };

            f.ShowDialog(this);
        }

        for (int hi = 0; hi < 5; hi++) prevHkDown[hi] = true; // 防止松键前边缘误触发
        capturingKey = false;
        vk = cvk;
        mod = cmod;

        if (!got)
        {
            Array.Copy(oldVk, hkVk, 5);
            Array.Copy(oldMod, hkMod, 5);
        }
        ApplyHotkeys();
        return got;
    }

    void ShowHotkeyDialog()
    {
        using (var f = new Form())
        {
            f.Text = "热键设置 · RX 工具箱";
            f.StartPosition = FormStartPosition.CenterParent;
            f.FormBorderStyle = FormBorderStyle.FixedDialog;
            f.MinimizeBox = false;
            f.MaximizeBox = false;
            f.ShowInTaskbar = false;
            f.BackColor = Color.White;
            f.ClientSize = new Size(680, 400);

            var lbTitle = new Label();
            lbTitle.Text = "热键是全局的：游戏在前台也生效。选一行，点「修改按键…」，然后直接按下你想用的键。";
            lbTitle.Font = TrackFont("Microsoft YaHei UI", 9.5f, FontStyle.Regular);
            lbTitle.ForeColor = Color.FromArgb(60, 70, 85);
            lbTitle.SetBounds(16, 12, 648, 22);
            f.Controls.Add(lbTitle);

            var lv = new BufferedListView();
            lv.View = View.Details;
            lv.FullRowSelect = true;
            lv.MultiSelect = false;
            lv.HideSelection = false;
            lv.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            lv.Font = TrackFont("Microsoft YaHei UI", 9.5f, FontStyle.Regular);
            lv.Columns.Add("功能", 150);
            lv.Columns.Add("当前按键", 110);
            lv.Columns.Add("状态", 110);
            lv.Columns.Add("说明", 270);
            lv.SetBounds(16, 42, 648, 268);
            f.Controls.Add(lv);

            Action refill = delegate
            {
                lv.BeginUpdate();
                lv.Items.Clear();
                for (int id = 1; id <= 5; id++)
                {
                    var it = new ListViewItem(HKFunc[id - 1]);
                    it.SubItems.Add(KeyName(id));
                    it.SubItems.Add(hkRegistered[id] ? "全局已注册" : "轮询兜底中");
                    it.SubItems.Add(HKTip[id - 1]);
                    it.ForeColor = hkRegistered[id] ? Color.FromArgb(30, 30, 30) : Color.FromArgb(180, 80, 20);
                    it.Tag = id;
                    lv.Items.Add(it);
                }
                lv.EndUpdate();
                if (lv.Items.Count > 0) lv.Items[0].Selected = true;
            };
            refill();

            var lbHint = new Label();
            lbHint.Text = "「轮询兜底中」= 系统热键被其它软件占用，已自动切换为按键状态轮询，同样可用。\r\n对局里常用 F1~F12，若冲突也可换成 Ctrl+ / Alt+ 组合键。";
            lbHint.ForeColor = Color.FromArgb(120, 126, 136);
            lbHint.SetBounds(16, 314, 520, 38);
            f.Controls.Add(lbHint);

            Action modify = delegate
            {
                if (lv.SelectedItems.Count == 0)
                {
                    toast.ShowText("先选中要改的那一行");
                    return;
                }
                int id = Convert.ToInt32(lv.SelectedItems[0].Tag);
                int nvk, nmod;
                if (!CaptureKey(out nvk, out nmod)) return;

                for (int other = 1; other <= 5; other++)
                {
                    if (other == id) continue;
                    if (hkVk[other - 1] == nvk && hkMod[other - 1] == nmod)
                    {
                        toast.ShowText("「" + HKFunc[other - 1] + "」已经用了 " + KeyName(other) + "，换一个键");
                        return;
                    }
                }

                hkVk[id - 1] = nvk;
                hkMod[id - 1] = nmod;
                int fail = ApplyHotkeys();
                SaveSettings();
                UpdateHotkeyLabels();
                refill();
                for (int i = 0; i < lv.Items.Count; i++)
                    if (Convert.ToInt32(lv.Items[i].Tag) == id) { lv.Items[i].Selected = true; break; }
                Native.Log("hotkey " + HKFunc[id - 1] + " -> " + KeyName(id) + " (fail=" + fail + ")");
                toast.ShowText(HKFunc[id - 1] + " 现在是 " + KeyName(id));
            };

            var bMod = new Button();
            bMod.Text = "修改按键…";
            bMod.SetBounds(16, 356, 120, 32);
            bMod.Click += delegate { modify(); };
            f.Controls.Add(bMod);

            var bDef = new Button();
            bDef.Text = "恢复默认";
            bDef.SetBounds(146, 356, 100, 32);
            bDef.Click += delegate
            {
                if (MessageBox.Show("把 5 个热键都恢复成默认（F5 / F6 / F7 / F8 / Ctrl+F9）？",
                        "RX 工具箱", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
                    return;
                for (int i = 0; i < 5; i++)
                {
                    hkVk[i] = HKDefVk[i];
                    hkMod[i] = HKDefMod[i];
                }
                ApplyHotkeys();
                SaveSettings();
                UpdateHotkeyLabels();
                refill();
                toast.ShowText("已恢复默认热键");
            };
            f.Controls.Add(bDef);

            var bClose = new Button();
            bClose.Text = "关闭";
            bClose.SetBounds(564, 356, 100, 32);
            bClose.DialogResult = DialogResult.OK;
            f.Controls.Add(bClose);
            f.AcceptButton = bClose;
            f.CancelButton = bClose;

            lv.DoubleClick += delegate { modify(); };
            Native.Log("hotkey dialog shown");
            f.ShowDialog(this);
        }
        UpdateHotkeyLabels();
    }

    void ImportCrosshairImage()
    {
        using (var dlg = new OpenFileDialog())
        {
            dlg.Title = "选择准星图片（建议 PNG 透明底，黑底 JPG 也能扣）";
            dlg.Filter = "图片 (*.png;*.bmp;*.jpg;*.jpeg;*.gif)|*.png;*.bmp;*.jpg;*.jpeg;*.gif|所有文件 (*.*)|*.*";
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            LoadCrosshairImage(dlg.FileName, false);
        }
    }

    void LoadCrosshairImage(string path, bool silent)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            if (!silent) toast.ShowText("找不到这张图片：" + path);
            return;
        }
        Bitmap bmp = Overlay.PrepareCrosshairImage(path);
        if (bmp == null)
        {
            if (!silent) toast.ShowText("这张图片读不出来（换 PNG/BMP 试试）");
            return;
        }
        if (overlay.CustomImage != null) { try { overlay.CustomImage.Dispose(); } catch { } }
        overlay.CustomImage = bmp;
        overlay.CustomImagePath = path;
        overlay.Invalidate();
        if (crossPreview != null) crossPreview.Invalidate();
        if (lblCrossImg != null)
            lblCrossImg.Text = "自定义准星：" + System.IO.Path.GetFileName(path)
                + "（原图 " + bmp.Width + "×" + bmp.Height + "，用「臂长+间隙」调大小）";
        SaveSettings();
        RefreshUi(true);
        Native.Log("crosshair image: " + path);
        if (!silent) toast.ShowText("已导入准星：" + System.IO.Path.GetFileName(path));
    }

    void PasteCrosshairImage()
    {
        try
        {
            if (!Clipboard.ContainsImage())
            {
                toast.ShowText("剪贴板里没有图片");
                return;
            }
            Image img = Clipboard.GetImage();
            if (img == null)
            {
                toast.ShowText("剪贴板里的图片读不出来");
                return;
            }
            string dir = System.IO.Path.GetDirectoryName(Application.ExecutablePath);
            string save = System.IO.Path.Combine(dir, "crosshair.png");
            string tmp = save + ".tmp";
            try
            {
                img.Save(tmp, System.Drawing.Imaging.ImageFormat.Png);   // 原图带 alpha 存盘
            }
            finally { img.Dispose(); }
            if (File.Exists(save)) File.Delete(save);
            File.Move(tmp, save);
            Native.Log("crosshair paste -> " + save);
            LoadCrosshairImage(save, false);
        }
        catch (Exception ex)
        {
            Native.Log("crosshair paste EX: " + ex.Message);
            toast.ShowText("粘贴失败：" + ex.Message);
        }
    }

    void ClearCrosshairImage()
    {
        if (overlay.CustomImage == null && string.IsNullOrEmpty(overlay.CustomImagePath))
        {
            toast.ShowText("当前没有导入图片");
            return;
        }
        if (overlay.CustomImage != null) { try { overlay.CustomImage.Dispose(); } catch { } }
        overlay.CustomImage = null;
        overlay.CustomImagePath = "";
        overlay.Invalidate();
        if (crossPreview != null) crossPreview.Invalidate();
        if (lblCrossImg != null) lblCrossImg.Text = "当前用内置矢量样式（可导入 PNG 做自定义准星）";
        SaveSettings();
        RefreshUi(true);
        toast.ShowText("已清除自定义准星图片");
    }

    void ToggleCross()
    {
        try
        {
            if (overlay.Visible)
            {
                overlay.Visible = false;
                toast.ShowText("准星：关闭");
                Native.Log("cross hide");
            }
            else
            {
                overlay.Visible = true;
                Native.MakeTopmost(overlay.Handle);
                Native.CenterPhysical(overlay.Handle);
                toast.ShowText("准星：开启");
                Native.Log("cross show");
            }
        }
        catch (Exception ex) { Native.Log("cross EX: " + ex.Message); }
        RefreshUi(true);
    }

    void RefreshUi(bool force)
    {
        if (!force && (DateTime.Now - lastUi).TotalMilliseconds < 400) return;
        lastUi = DateTime.Now;
        int m = JoinModeIdx;
        string modeName = (m == 0) ? "手动档" : ((m == 2) ? "收藏轮询" : "单一 IP");

        string tg;
        if (autoOn && typingOn && targets != null && targets.Count > 0)
        {
            tg = (targets.Count == 1)
                ? targets[0]
                : ("正在挤 " + (string.IsNullOrEmpty(currentTargetAddr) ? targets[0] : currentTargetAddr) + " (共" + targets.Count + "服)");
        }
        else if (m == 0)
        {
            tg = "连按回车";
        }
        else if (m == 2)
        {
            int nchk = 0;
            if (lvSrv != null) for (int i = 0; i < lvSrv.Items.Count; i++) if (lvSrv.Items[i].Checked) nchk++;
            tg = nchk > 0 ? ("已勾 " + nchk + " 个服") : "未勾选";
        }
        else
        {
            string selAddr = SelectedAddr();
            tg = selAddr.Length > 0 ? selAddr : "未选服";
        }

        string run;
        if (!autoOn) run = "已停止";
        else if (typingOn) run = "粘贴中";
        else run = "回车中";

        // 挤服进行中：顶部常驻提醒（含当前热键），停止后自动收起
        if (autoOn || typingOn)
        {
            string runningMode = typingOn
                ? ("粘贴 connect · " + tg)
                : "连按回车（游戏里点连接）";
            toast.ShowPersistent("正在挤服 · 按 " + KeyName(2) + " 停止　—　" + runningMode);
        }
        else
        {
            toast.HideIfPersistent();
        }

        string ocr = (chkOcr == null || chkOcr.Checked) ? "OCR开" : "OCR关";
        lblStatus.Text = string.Format("准星：{0} ({1})　|　挤服：{2} [{3} · {4} · {5}ms]　|　{6}　|　已发送 {7} 次",
            overlay.Visible ? "开启" : "关闭",
            Overlay.Styles[overlay.StyleIndex],
            run, modeName, tg, IntervalMs, ocr, sent);
    }

    void OnTick(object sender, EventArgs e)
    {
        // 窗口/面板尺寸变了就重排（构造期布局用的是临时尺寸，常常对不上）
        if (pJoinPanel != null && !isClosing
            && (pJoinPanel.ClientSize.Width != laidW || pJoinPanel.ClientSize.Height != laidH))
            LayoutJoin();

        if (pendingSelAddr.Length > 0 && lvSrv != null && lvSrv.Items.Count > 0)
        {
            string want = pendingSelAddr;
            pendingSelAddr = "";
            int wantIx = -1;
            for (int i = 0; i < lvSrv.Items.Count; i++)
                if (string.Equals(Convert.ToString(lvSrv.Items[i].Tag), want, StringComparison.OrdinalIgnoreCase)) { wantIx = i; break; }
            if (wantIx >= 0 && (lvSrv.SelectedIndices.Count != 1 || lvSrv.SelectedIndices[0] != wantIx))
            {
                lvSrv.SelectedIndices.Clear();
                lvSrv.Items[wantIx].Selected = true;
                lvSrv.Items[wantIx].Focused = true;
                lvSrv.EnsureVisible(wantIx);
                UpdateJoinHint();
            }
        }

        if (pJoinPanel != null && !isClosing
            && (pJoinPanel.ClientSize.Width != laidW || pJoinPanel.ClientSize.Height != laidH))
            LayoutJoin();

        // 动态热键轮询兜底（抓键弹窗期间严禁触发）
        if (!capturingKey)
        {
            for (int hi = 0; hi < 5; hi++)
            {
                int id = hi + 1;
                if (!hkRegistered[id])
                {
                    bool down = IsHotkeyDown(hkVk[hi], hkMod[hi]);
                    if (down && !prevHkDown[hi])
                    {
                        prevHkDown[hi] = true;
                        HandleHotkey(id);
                        if (id == 5) return;
                    }
                    else if (!down)
                    {
                        prevHkDown[hi] = false;
                    }
                }
                else
                {
                    prevHkDown[hi] = false;
                }
            }
        }

        if (autoOn)
        {
            if (!typingOn)
            {
                if ((DateTime.Now - lastEnter).TotalMilliseconds >= IntervalMs)
                {
                    string fgOld = Native.ForegroundProcess();
                    if (Array.IndexOf(GameProcs, fgOld) >= 0)
                    {
                        Native.PressEnter();
                        lastEnter = DateTime.Now;
                        sent++;
                    }
                }
            }
            else if (!ocrHoldSend && (DateTime.Now - lastEnter).TotalMilliseconds >= IntervalMs)
            {
                string fg = Native.ForegroundProcess();
                if (Array.IndexOf(GameProcs, fg) >= 0)
                {
                    int n2 = (targets == null) ? 0 : targets.Count;
                    if (n2 > 0)
                    {
                        if (multiIdx >= n2) multiIdx = 0;
                        string addr2 = targets[multiIdx];
                        currentTargetAddr = addr2;
                        multiIdx = (multiIdx + 1) % n2;
                        lastEnter = DateTime.Now;

                        if (TrySetClipboardText("connect " + addr2))
                        {
                            Native.PressCtrlV();
                            Thread.Sleep(24);
                            Native.PressEnter();
                            sent++;
                            Native.Log("paste: connect " + addr2);
                            RefreshActiveTargetRowHighlight();
                        }
                        else
                        {
                            Native.Log("paste skip: clipboard locked for " + addr2);
                        }
                    }
                }
            }

            // 每 2 秒后台异步看一眼画面，判断是不是已经挤进去了（0 毫秒阻塞 UI 线程）
            bool ocrOn = (chkOcr == null || chkOcr.Checked);
            if (ocrOn && (DateTime.Now - tapOcrAt).TotalSeconds >= 2.0)
            {
                TriggerOcrCheckAsync();
            }
        }
        RefreshUi(false);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Native.ShowWindow(Handle, 5);
        try
        {
            if (tabs != null) { tabs.PerformLayout(); LayoutAboutTab(); }
            if (pJoinPanel != null) LayoutJoin();
        }
        catch { }
        // 上次关过热键就保持关闭，只挂开关热键
        if (hotkeysOff) { ReleaseHotkeys(); RegisterToggleHotkey(); }
        else ApplyHotkeys();
        UpdateHotkeyToggleButton();
        // 启动后自动刷一次服务器状态（人数 + 真实名），失败的再串行补查一遍
        if (favList.Length > 0)
        {
            var bootTimer = new System.Windows.Forms.Timer();
            bootTimer.Interval = 1200;
            bootTimer.Tick += delegate
            {
                bootTimer.Stop();
                bootTimer.Dispose();
                if (isClosing || lvSrv == null || favList.Length == 0) return;
                Native.Log("startup refresh: " + favList.Length + " servers");
                bootRefresh = true;
                try { RefreshAllFavs(); }
                catch (Exception ex) { bootRefresh = false; Native.Log("startup refresh EX: " + ex.Message); }
            };
            bootTimer.Start();
        }
        if (Environment.GetEnvironmentVariable("RX_DUMP_UI") == "1") DumpUi();
        // 启动后静默检查更新（失败不打扰）
        if (autoUpdateOn && !startupCheckDone)
        {
            startupCheckDone = true;
            var updTimer = new System.Windows.Forms.Timer();
            updTimer.Interval = 2000;
            updTimer.Tick += delegate
            {
                updTimer.Stop();
                updTimer.Dispose();
                if (isClosing) return;
                try { CheckUpdate(false); } catch { }
            };
            updTimer.Start();
        }
        Native.Log("shown");
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        isClosing = true;
        try
        {
            if (autoOn || typingOn)
                StopActiveJoin(null, "close: stop active join");
        }
        catch { }
        for (int id = 1; id <= 5; id++)
        {
            try { if (hkRegistered[id]) Native.UnregisterHotKey(Handle, id); } catch { }
        }
        SaveSettings();
        try { if (tick != null) { tick.Stop(); tick.Dispose(); tick = null; } } catch (Exception ex) { Native.Log("tick dispose: " + ex.Message); }
        try { if (uiComponents != null) { uiComponents.Dispose(); uiComponents = null; } } catch { }
        try { if (toast != null) { toast.Close(); toast.Dispose(); toast = null; } } catch { }
        try { if (overlay != null) { overlay.Close(); overlay.Dispose(); overlay = null; } } catch { }
        for (int i = 0; i < ownedGdi.Count; i++)
        {
            try { if (ownedGdi[i] != null) ownedGdi[i].Dispose(); } catch { }
        }
        ownedGdi.Clear();
        fontPool.Clear();
        base.OnFormClosed(e);
    }
}