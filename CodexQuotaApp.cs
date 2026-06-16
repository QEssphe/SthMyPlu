using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;

public class CodexQuotaForm : Form {
    readonly string appDir;
    readonly string statePath;
    readonly string windowPath;
    readonly string pollerPath;
    readonly string nodePath;
    readonly Timer repaintTimer = new Timer();
    Process poller;
    NotifyIcon tray;
    bool dragging;
    bool docked;
    bool animating;
    bool allowExit;
    string dockSide = "None";
    Point dragStart;
    Point formStart;
    QuotaState lastGood;
    double wavePhase;
    const int TailSize = 24;
    const int EdgeThreshold = 52;

    static readonly string TitleText = "\u989d\u5ea6\u5df2\u7528";
    static readonly string FiveText = "5 \u5c0f\u65f6\u5df2\u7528";
    static readonly string WeekText = "\u6bcf\u5468\u5df2\u7528";
    static readonly string ResetLabel = "\u91cd\u7f6e";
    static readonly string UsedText = "\u5df2\u7528";
    static readonly string ConnectingText = "\u8fde\u63a5\u4e2d";
    static readonly string ShowText = "\u663e\u793a";
    static readonly string HideText = "\u9690\u85cf";
    static readonly string RefreshText = "\u5237\u65b0";
    static readonly string ExitText = "\u9000\u51fa";
    static readonly string DetailText = "\u67e5\u770b\u8be6\u60c5";

    public CodexQuotaForm() {
        appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        statePath = Path.Combine(appDir, "quota-live.json");
        windowPath = Path.Combine(appDir, "orb-window.json");
        pollerPath = Path.Combine(appDir, "quota-poller.mjs");
        nodePath = FindNodePath();

        Text = "Codex Quota";
        StartPosition = FormStartPosition.Manual;
        Size = new Size(270, 148);
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = true;
        TopMost = true;
        BackColor = Color.FromArgb(245, 247, 250);
        Icon = LoadAppIcon();
        DoubleBuffered = true;

        LoadWindowState();
        UpdateRegion();
        BuildContextMenu();
        BuildTray();

        repaintTimer.Interval = 40;
        repaintTimer.Tick += (s, e) => {
            wavePhase += 0.16;
            if (wavePhase > Math.PI * 2) wavePhase -= Math.PI * 2;
            Invalidate();
        };

        Shown += (s, e) => {
            StartPoller();
            if (docked && dockSide != "None") {
                var p = GetDockedPosition(dockSide);
                Location = p;
            }
            repaintTimer.Start();
        };
        FormClosing += OnFormClosing;
        FormClosed += (s, e) => {
            repaintTimer.Stop();
            SaveWindowState();
            StopPoller();
            if (tray != null) { tray.Visible = false; tray.Dispose(); }
        };
        Resize += (s, e) => UpdateRegion();
        MouseDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseUp += OnMouseUp;
        DoubleClick += (s, e) => ShowDetails();
    }

    protected override CreateParams CreateParams {
        get {
            var cp = base.CreateParams;
            cp.ClassStyle |= 0x00020000;
            return cp;
        }
    }

    void BuildContextMenu() {
        var menu = new ContextMenuStrip();
        menu.Items.Add(DetailText, null, (s, e) => ShowDetails());
        menu.Items.Add(RefreshText, null, (s, e) => { StopPoller(); StartPoller(); Invalidate(); });
        menu.Items.Add(HideText, null, (s, e) => Hide());
        ContextMenuStrip = menu;
    }

    void BuildTray() {
        tray = new NotifyIcon();
        tray.Text = "Codex " + TitleText;
        tray.Icon = Icon;
        tray.Visible = true;
        var menu = new ContextMenuStrip();
        menu.Items.Add(ShowText, null, (s, e) => { Show(); if (docked) ExpandFromDock(); Activate(); });
        menu.Items.Add(HideText, null, (s, e) => Hide());
        menu.Items.Add(RefreshText, null, (s, e) => { StopPoller(); StartPoller(); Invalidate(); });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(ExitText, null, (s, e) => { allowExit = true; Close(); });
        tray.ContextMenuStrip = menu;
        tray.DoubleClick += (s, e) => { Show(); if (docked) ExpandFromDock(); Activate(); };
    }

    Icon LoadAppIcon() {
        var iconPath = Path.Combine(appDir, "CodexQuota.ico");
        if (File.Exists(iconPath)) {
            try { return new Icon(iconPath); } catch {}
        }
        return SystemIcons.Application;
    }

    void OnFormClosing(object sender, FormClosingEventArgs e) {
        if (allowExit || e.CloseReason == CloseReason.WindowsShutDown) return;
        e.Cancel = true;
        if (!Visible) Show();
        Activate();
    }

    void LoadWindowState() {
        Left = 120;
        Top = 120;
        if (!File.Exists(windowPath)) return;
        var text = File.ReadAllText(windowPath);
        var left = MatchInt(text, "\"Left\"\\s*:\\s*(-?\\d+)", Left);
        var top = MatchInt(text, "\"Top\"\\s*:\\s*(-?\\d+)", Top);
        var side = MatchString(text, "\"DockSide\"\\s*:\\s*\"([^\"]+)\"", "None");
        var isDocked = Regex.IsMatch(text, "\"IsDocked\"\\s*:\\s*true", RegexOptions.IgnoreCase);
        Left = left;
        Top = top;
        dockSide = side;
        docked = isDocked;
        var v = SystemInformation.VirtualScreen;
        Left = Clamp(Left, v.Left - Width + TailSize, v.Right - TailSize);
        Top = Clamp(Top, v.Top - Height + TailSize, v.Bottom - TailSize);
    }

    void SaveWindowState() {
        File.WriteAllText(windowPath, "{\"Left\":" + Left + ",\"Top\":" + Top + ",\"DockSide\":\"" + dockSide + "\",\"IsDocked\":" + (docked ? "true" : "false") + "}");
    }

    static int MatchInt(string text, string pattern, int fallback) {
        var m = Regex.Match(text, pattern, RegexOptions.Singleline);
        int v;
        return m.Success && int.TryParse(m.Groups[1].Value, out v) ? v : fallback;
    }

    static string MatchString(string text, string pattern, string fallback) {
        var m = Regex.Match(text, pattern, RegexOptions.Singleline);
        return m.Success ? m.Groups[1].Value : fallback;
    }

    void StartPoller() {
        if (string.IsNullOrEmpty(nodePath) || !File.Exists(pollerPath)) return;
        if (poller != null && !poller.HasExited) return;
        var psi = new ProcessStartInfo(nodePath, "\"" + pollerPath + "\" \"" + statePath + "\"");
        psi.WorkingDirectory = appDir;
        psi.CreateNoWindow = true;
        psi.UseShellExecute = false;
        poller = Process.Start(psi);
    }

    static string FindNodePath() {
        var explicitPath = Environment.GetEnvironmentVariable("NODE_EXE");
        if (!string.IsNullOrEmpty(explicitPath) && File.Exists(explicitPath)) return explicitPath;
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var part in path.Split(Path.PathSeparator)) {
            try {
                var candidate = Path.Combine(part.Trim(), "node.exe");
                if (File.Exists(candidate)) return candidate;
            } catch {}
        }
        return "node.exe";
    }

    void StopPoller() {
        try { if (poller != null && !poller.HasExited) poller.Kill(); } catch {}
    }

    QuotaState GetQuota() {
        try {
            if (!File.Exists(statePath)) return lastGood ?? new QuotaState();
            var text = File.ReadAllText(statePath);
            if (!Regex.IsMatch(text, "\"ok\"\\s*:\\s*true")) return lastGood ?? new QuotaState();
            var primary = Regex.Match(text, "\"primary\"\\s*:\\s*\\{.*?\"usedPercent\"\\s*:\\s*(\\d+).*?\"resetsAt\"\\s*:\\s*(\\d+)", RegexOptions.Singleline);
            var secondary = Regex.Match(text, "\"secondary\"\\s*:\\s*\\{.*?\"usedPercent\"\\s*:\\s*(\\d+).*?\"resetsAt\"\\s*:\\s*(\\d+)", RegexOptions.Singleline);
            var q = new QuotaState();
            q.Online = primary.Success && secondary.Success;
            if (q.Online) {
                q.FivePercent = int.Parse(primary.Groups[1].Value);
                q.FiveReset = UnixToLocal(long.Parse(primary.Groups[2].Value));
                q.WeekPercent = int.Parse(secondary.Groups[1].Value);
                q.WeekReset = UnixToLocal(long.Parse(secondary.Groups[2].Value));
                q.Plan = MatchString(text, "\"planType\"\\s*:\\s*\"([^\"]+)\"", "--");
                lastGood = q;
            }
            return q.Online ? q : (lastGood ?? q);
        } catch {
            return lastGood ?? new QuotaState();
        }
    }

    static DateTime UnixToLocal(long seconds) {
        return DateTimeOffset.FromUnixTimeSeconds(seconds).LocalDateTime;
    }

    static string FormatTime(DateTime? dt) {
        return dt.HasValue ? dt.Value.ToString("M/d HH:mm") : "--:--";
    }

    static string FormatRemaining(DateTime? dt) {
        if (!dt.HasValue) return "--:--";
        var span = dt.Value - DateTime.Now;
        if (span.TotalSeconds <= 0) return ResetLabel;
        if (span.TotalDays >= 1) return ((int)Math.Floor(span.TotalDays)).ToString() + "d " + span.Hours.ToString("00") + ":" + span.Minutes.ToString("00");
        return ((int)Math.Floor(span.TotalHours)).ToString("00") + ":" + span.Minutes.ToString("00") + ":" + span.Seconds.ToString("00");
    }

    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e);
        var q = GetQuota();
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        g.Clear(BackColor);
        using (var border = new Pen(Color.FromArgb(110, 208, 214, 220), 1))
        using (var path = RoundedPath(new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), 28)) {
            g.DrawPath(border, path);
        }
        using (var dark = new SolidBrush(Color.FromArgb(245, 20, 25, 31)))
        using (var muted = new SolidBrush(Color.FromArgb(185, 80, 86, 97)))
        using (var green = new SolidBrush(Color.FromArgb(255, 52, 199, 89)))
        using (var titleFont = new Font("Microsoft YaHei UI", 10, FontStyle.Bold))
        using (var labelFont = new Font("Microsoft YaHei UI", 9, FontStyle.Bold))
        using (var valueFont = new Font("Segoe UI", 10, FontStyle.Bold))
        using (var metaFont = new Font("Microsoft YaHei UI", 8, FontStyle.Regular)) {
            if (q.Online) {
                g.DrawString(TitleText + "  " + q.Plan, titleFont, muted, 18, 14);
                g.FillEllipse(green, Width - 32, 18, 9, 9);
                DrawRow(g, FiveText, q.FivePercent, q.FiveReset, 42, dark, muted, labelFont, valueFont, metaFont);
                DrawRow(g, WeekText, q.WeekPercent, q.WeekReset, 98, dark, muted, labelFont, valueFont, metaFont);
            } else {
                g.DrawString(TitleText, titleFont, muted, 18, 18);
                g.DrawString(ConnectingText, labelFont, dark, 18, 58);
                DrawProgress(g, 18, 86, Width - 36, 0);
            }
        }
        if (docked) DrawDockTail(g, q);
    }

    void DrawRow(Graphics g, string label, int percent, DateTime? reset, int y, Brush dark, Brush muted, Font labelFont, Font valueFont, Font metaFont) {
        g.DrawString(label, labelFont, dark, 18, y);
        var value = percent.ToString() + "%";
        var valueSize = g.MeasureString(value, valueFont);
        g.DrawString(value, valueFont, dark, Width - 18 - valueSize.Width, y - 2);
        DrawProgress(g, 18, y + 21, Width - 36, percent);
        if (label == FiveText) {
            g.DrawString(ResetLabel + " " + FormatTime(reset) + " / " + FormatRemaining(reset), metaFont, muted, 18, y + 34);
        }
    }

    void DrawProgress(Graphics g, int x, int y, int width, int percent) {
        using (var track = new SolidBrush(Color.FromArgb(58, 60, 67, 75)))
        using (var fill = new SolidBrush(Color.FromArgb(255, 52, 199, 89)))
        using (var trackPath = RoundedPath(new RectangleF(x, y, width, 8), 4)) {
            g.FillPath(track, trackPath);
            var fillWidth = Math.Max(0, Math.Min(width, width * percent / 100f));
            if (fillWidth > 0) {
                using (var fillPath = RoundedPath(new RectangleF(x, y, fillWidth, 8), 4)) {
                    g.FillPath(fill, fillPath);
                }
            }
        }
    }

    void DrawDockTail(Graphics g, QuotaState q) {
        RectangleF rect;
        bool vertical = dockSide == "Left" || dockSide == "Right";
        if (dockSide == "Left") rect = new RectangleF(Width - TailSize, 0, TailSize, Height);
        else if (dockSide == "Right") rect = new RectangleF(0, 0, TailSize, Height);
        else if (dockSide == "Top") rect = new RectangleF(0, Height - TailSize, Width, TailSize);
        else if (dockSide == "Bottom") rect = new RectangleF(0, 0, Width, TailSize);
        else return;

        int percent = q.Online ? q.FivePercent : 0;
        percent = Math.Max(0, Math.Min(100, percent));
        using (var tailPath = RoundedPath(rect, TailSize / 2f))
        using (var shell = new LinearGradientBrush(rect, Color.FromArgb(255, 7, 92, 57), Color.FromArgb(255, 21, 150, 78), vertical ? 90f : 0f))
        using (var rim = new Pen(Color.FromArgb(235, 141, 245, 180), 1.4f))
        using (var glow = new Pen(Color.FromArgb(115, 60, 230, 125), 5f)) {
            g.FillPath(shell, tailPath);

            Region oldClip = g.Clip;
            g.SetClip(tailPath);
            DrawTailLiquid(g, rect, percent, vertical);
            using (var shine = new LinearGradientBrush(rect, Color.FromArgb(120, 220, 255, 230), Color.FromArgb(0, 220, 255, 230), vertical ? 0f : 90f)) {
                if (vertical) {
                    var highlight = dockSide == "Left"
                        ? new RectangleF(rect.Left + rect.Width * 0.48f, rect.Top + 10, rect.Width * 0.24f, rect.Height - 20)
                        : new RectangleF(rect.Left + rect.Width * 0.25f, rect.Top + 10, rect.Width * 0.24f, rect.Height - 20);
                    g.FillEllipse(shine, highlight);
                } else {
                    var highlight = dockSide == "Top"
                        ? new RectangleF(rect.Left + 12, rect.Top + rect.Height * 0.50f, rect.Width - 24, rect.Height * 0.22f)
                        : new RectangleF(rect.Left + 12, rect.Top + rect.Height * 0.24f, rect.Width - 24, rect.Height * 0.22f);
                    g.FillEllipse(shine, highlight);
                }
            }
            g.Clip = oldClip;
            if (oldClip != null) oldClip.Dispose();

            g.DrawPath(glow, tailPath);
            g.DrawPath(rim, tailPath);
        }
    }

    void DrawTailLiquid(Graphics g, RectangleF rect, int percent, bool vertical) {
        using (var liquid = new GraphicsPath())
        using (var fill = new LinearGradientBrush(rect, Color.FromArgb(255, 89, 240, 132), Color.FromArgb(255, 26, 196, 93), vertical ? 90f : 0f))
        using (var cap = new SolidBrush(Color.FromArgb(155, 192, 255, 207))) {
            if (vertical) {
                float filled = rect.Height * percent / 100f;
                float waveY = rect.Bottom - filled;
                liquid.StartFigure();
                liquid.AddLine(rect.Left, rect.Bottom, rect.Left, waveY);
                float prevX = rect.Left;
                float prevY = waveY;
                for (float x = rect.Left; x <= rect.Right + 1; x += 2f) {
                    float y = waveY + (float)(Math.Sin(wavePhase + (x - rect.Left) * 0.55) * 3.2);
                    liquid.AddLine(prevX, prevY, x, y);
                    prevX = x;
                    prevY = y;
                }
                liquid.AddLine(prevX, prevY, rect.Right, rect.Bottom);
                liquid.CloseFigure();
                g.FillPath(fill, liquid);
                g.FillEllipse(cap, rect.Left + 3, Math.Max(rect.Top + 3, waveY - 5), rect.Width - 6, 9);
            } else {
                float filled = rect.Width * percent / 100f;
                float waveX = rect.Left + filled;
                liquid.StartFigure();
                liquid.AddLine(rect.Left, rect.Top, waveX, rect.Top);
                float prevX = waveX;
                float prevY = rect.Top;
                for (float y = rect.Top; y <= rect.Bottom + 1; y += 2f) {
                    float x = waveX + (float)(Math.Sin(wavePhase + (y - rect.Top) * 0.55) * 3.2);
                    liquid.AddLine(prevX, prevY, x, y);
                    prevX = x;
                    prevY = y;
                }
                liquid.AddLine(prevX, prevY, rect.Left, rect.Bottom);
                liquid.CloseFigure();
                g.FillPath(fill, liquid);
                g.FillEllipse(cap, Math.Min(rect.Right - 10, Math.Max(rect.Left + 1, waveX - 5)), rect.Top + 3, 9, rect.Height - 6);
            }
        }
    }

    static GraphicsPath RoundedPath(RectangleF rect, float radius) {
        var path = new GraphicsPath();
        var d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    void UpdateRegion() {
        using (var path = RoundedPath(new RectangleF(0, 0, Width, Height), 28)) {
            var old = Region;
            Region = new Region(path);
            if (old != null) old.Dispose();
        }
    }

    void OnMouseDown(object sender, MouseEventArgs e) {
        if (e.Button != MouseButtons.Left || animating) return;
        if (docked) { ExpandFromDock(); return; }
        dragging = true;
        dragStart = Cursor.Position;
        formStart = Location;
    }

    void OnMouseMove(object sender, MouseEventArgs e) {
        if (!dragging || e.Button != MouseButtons.Left) return;
        var p = Cursor.Position;
        var v = SystemInformation.VirtualScreen;
        Left = Clamp(formStart.X + (p.X - dragStart.X), v.Left - Width + TailSize, v.Right - TailSize);
        Top = Clamp(formStart.Y + (p.Y - dragStart.Y), v.Top - Height + TailSize, v.Bottom - TailSize);
    }

    void OnMouseUp(object sender, MouseEventArgs e) {
        if (e.Button != MouseButtons.Left) return;
        var was = dragging;
        dragging = false;
        if (was) CheckDock();
    }

    Rectangle CurrentScreen() {
        return Screen.FromRectangle(Bounds).WorkingArea;
    }

    int Clamp(int value, int min, int max) {
        return Math.Max(min, Math.Min(max, value));
    }

    Point GetDockedPosition(string side) {
        var s = CurrentScreen();
        var left = Clamp(Left, s.Left, s.Right - Width);
        var top = Clamp(Top, s.Top, s.Bottom - Height);
        if (side == "Left") { left = s.Left - Width + TailSize; top = Clamp(Top, s.Top + 8, s.Bottom - Height - 8); }
        if (side == "Right") { left = s.Right - TailSize; top = Clamp(Top, s.Top + 8, s.Bottom - Height - 8); }
        if (side == "Top") { left = Clamp(Left, s.Left + 8, s.Right - Width - 8); top = s.Top - Height + TailSize; }
        if (side == "Bottom") { left = Clamp(Left, s.Left + 8, s.Right - Width - 8); top = s.Bottom - TailSize; }
        return new Point(left, top);
    }

    void CheckDock() {
        var s = CurrentScreen();
        var left = Math.Abs(Left - s.Left);
        var right = Math.Abs(Right - s.Right);
        var top = Math.Abs(Top - s.Top);
        var bottom = Math.Abs(Bottom - s.Bottom);
        if (Left < s.Left) left = 0;
        if (Right > s.Right) right = 0;
        if (Top < s.Top) top = 0;
        if (Bottom > s.Bottom) bottom = 0;
        var min = Math.Min(Math.Min(left, right), Math.Min(top, bottom));
        if (min > EdgeThreshold) { docked = false; dockSide = "None"; SaveWindowState(); return; }
        if (min == left) DockToEdge("Left");
        else if (min == right) DockToEdge("Right");
        else if (min == top) DockToEdge("Top");
        else DockToEdge("Bottom");
    }

    void DockToEdge(string side) {
        dockSide = side;
        docked = true;
        var p = GetDockedPosition(side);
        AnimateTo(p.X, p.Y, 240);
    }

    void ExpandFromDock() {
        if (!docked) return;
        var s = CurrentScreen();
        var left = Left;
        var top = Top;
        if (dockSide == "Left") left = s.Left + 10;
        if (dockSide == "Right") left = s.Right - Width - 10;
        if (dockSide == "Top") top = s.Top + 10;
        if (dockSide == "Bottom") top = s.Bottom - Height - 10;
        left = Clamp(left, s.Left + 8, s.Right - Width - 8);
        top = Clamp(top, s.Top + 8, s.Bottom - Height - 8);
        docked = false;
        dockSide = "None";
        AnimateTo(left, top, 240);
    }

    void AnimateTo(int targetLeft, int targetTop, int millis) {
        if (animating) return;
        animating = true;
        var startLeft = Left;
        var startTop = Top;
        var start = Environment.TickCount;
        var t = new Timer();
        t.Interval = 15;
        t.Tick += (s, e) => {
            var elapsed = Environment.TickCount - start;
            var k = Math.Min(1.0, elapsed / (double)millis);
            var ease = 1 - Math.Pow(1 - k, 3);
            Left = (int)(startLeft + (targetLeft - startLeft) * ease);
            Top = (int)(startTop + (targetTop - startTop) * ease);
            if (k >= 1) {
                t.Stop();
                t.Dispose();
                Left = targetLeft;
                Top = targetTop;
                animating = false;
                SaveWindowState();
            }
        };
        t.Start();
    }

    void ShowDetails() {
        var q = GetQuota();
        var msg = FiveText + ": " + q.FivePercent + "% " + UsedText + ", " + ResetLabel + " " + FormatTime(q.FiveReset) + Environment.NewLine +
                  WeekText + ": " + q.WeekPercent + "% " + UsedText + ", " + ResetLabel + " " + FormatTime(q.WeekReset);
        MessageBox.Show(msg, TitleText, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    class QuotaState {
        public bool Online;
        public int FivePercent;
        public int WeekPercent;
        public DateTime? FiveReset;
        public DateTime? WeekReset;
        public string Plan = "--";
    }
}

static class Program {
    [STAThread]
    static void Main() {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new CodexQuotaForm());
    }
}
