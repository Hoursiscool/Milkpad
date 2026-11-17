using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Win32;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

// Version metadata keeps the executable identifiable. A fresh unsigned build with a blank
// version tab is one of the strongest signals antivirus heuristics score against.
[assembly: System.Reflection.AssemblyTitle("Milkpad")]
[assembly: System.Reflection.AssemblyDescription("Milkpad - a small, fast text editor for notes and code")]
[assembly: System.Reflection.AssemblyProduct("Milkpad")]
[assembly: System.Reflection.AssemblyCompany("Milkpad")]
[assembly: System.Reflection.AssemblyCopyright("Milkpad")]
[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.7", FrameworkDisplayName = ".NET Framework 4.8")]
[assembly: System.Reflection.AssemblyVersion("1.1.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.1.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersion("1.1.0")]
[assembly: System.Runtime.InteropServices.ComVisible(false)]

namespace DocsDesktop
{
    sealed class DocState
    {
        public string Name = "Untitled";
        public string Path = "";
        public string Rtf = "";
        public bool Dirty;
        public readonly List<string> Comments = new List<string>();
        public readonly List<string> Versions = new List<string>();
        public string Id = "";
        public bool ReadOnly;
        public DateTime Saved;
    }

    sealed class MenuColors : ProfessionalColorTable
    {
        readonly Color surface, fill, border;
        public MenuColors(Color surface, Color fill, Color border) { this.surface = surface; this.fill = fill; this.border = border; }
        public override Color MenuItemSelected { get { return fill; } }
        public override Color MenuItemSelectedGradientBegin { get { return fill; } }
        public override Color MenuItemSelectedGradientEnd { get { return fill; } }
        public override Color MenuItemPressedGradientBegin { get { return fill; } }
        public override Color MenuItemPressedGradientEnd { get { return fill; } }
        public override Color ToolStripDropDownBackground { get { return surface; } }
        public override Color ImageMarginGradientBegin { get { return surface; } }
        public override Color ImageMarginGradientMiddle { get { return surface; } }
        public override Color ImageMarginGradientEnd { get { return surface; } }
        public override Color MenuBorder { get { return border; } }
        public override Color SeparatorDark { get { return border; } }
        public override Color SeparatorLight { get { return border; } }
    }

    // The stock ComboBox paints its drop-down arrow with the system face colour, which
    // turns into a bright block on a dark toolbar. This repaints that corner after the
    // control draws itself, so the arrow follows the theme like everything else does.
    sealed class FlatCombo : ComboBox
    {
        bool dark = true;
        public bool Dark { get { return dark; } set { if (dark == value) return; dark = value; Invalidate(); } }
        public Color FieldColor = Color.FromArgb(43, 34, 42);
        public Color InkColor = Color.FromArgb(206, 197, 206);



        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg != 0x000F || !IsHandleCreated) return;
            int zone = ClientSize.Height;
            if (zone > ClientSize.Width - 12) zone = Math.Max(12, ClientSize.Width / 3);
            using (Graphics g = Graphics.FromHwnd(Handle))
            {
                Color field = FieldColor;
                Color ink = InkColor;
                using (var brush = new SolidBrush(field)) g.FillRectangle(brush, ClientSize.Width - zone, 1, zone - 1, ClientSize.Height - 2);
                using (var pen = new Pen(ink, 1.6f))
                {
                    int cx = ClientSize.Width - zone / 2, cy = ClientSize.Height / 2, arm = Math.Max(3, zone / 6);
                    g.DrawLine(pen, cx - arm, cy - arm / 2, cx, cy + arm / 2);
                    g.DrawLine(pen, cx, cy + arm / 2, cx + arm, cy - arm / 2);
                }
            }
        }
    }

    sealed class MenuText : ToolStripProfessionalRenderer
    {
        readonly Color surface, line, highlight, text, muted;
        public MenuText(Color surface, Color line, Color highlight, Color text, Color muted) : base(new MenuColors(surface, highlight, line))
        { this.surface = surface; this.line = line; this.highlight = highlight; this.text = text; this.muted = muted; RoundedEdges = false; }

        Color Surface { get { return surface; } }
        Color Line { get { return line; } }
        Color Highlight { get { return highlight; } }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using (var brush = new SolidBrush(Surface)) e.Graphics.FillRectangle(brush, e.AffectedBounds);
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
        {
            using (var brush = new SolidBrush(Surface)) e.Graphics.FillRectangle(brush, e.AffectedBounds);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using (var pen = new Pen(Line)) e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            bool lit = e.Item.Selected || e.Item.Pressed;
            using (var brush = new SolidBrush(lit ? Highlight : Surface)) e.Graphics.FillRectangle(brush, new Rectangle(Point.Empty, e.Item.Size));
            if (lit) using (var pen = new Pen(line)) e.Graphics.DrawRectangle(pen, 0, 0, e.Item.Width - 1, e.Item.Height - 1);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            using (var pen = new Pen(Line)) e.Graphics.DrawLine(pen, 8, e.Item.Height / 2, e.Item.Width - 8, e.Item.Height / 2);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = text;
            base.OnRenderArrow(e);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = !e.Item.Enabled ? muted : text;
            base.OnRenderItemText(e);
        }
    }

    sealed class ZoomSlider : Control
    {
        static readonly Color Accent = Color.FromArgb(220, 145, 196);
        int minimum = 50, maximum = 200, value = 100;
        bool dragging;
        public event EventHandler ValueChanged;

        public ZoomSlider()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand; TabStop = false; Height = 22;
        }

        public int Minimum { get { return minimum; } set { minimum = value; if (this.value < minimum) this.value = minimum; Invalidate(); } }
        public int Maximum { get { return maximum; } set { maximum = value; if (this.value > maximum) this.value = maximum; Invalidate(); } }
        public int Value
        {
            get { return value; }
            set
            {
                int v = Math.Max(minimum, Math.Min(maximum, value));
                if (v == this.value) return;
                this.value = v; Invalidate();
                if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
            }
        }

        bool DarkChrome { get { return BackColor.R + BackColor.G + BackColor.B < 300; } }

        protected override void OnBackColorChanged(EventArgs e) { base.OnBackColorChanged(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            int cy = Height / 2, span = Math.Max(2, Width - 12);
            Color track = DarkChrome ? Color.FromArgb(84, 74, 84) : Color.FromArgb(212, 205, 214);
            using (var brush = new SolidBrush(track)) e.Graphics.FillRectangle(brush, 6, cy - 1, span, 3);
            int x = 6 + (int)Math.Round(span * ((value - minimum) / (double)Math.Max(1, maximum - minimum)));
            using (var brush = new SolidBrush(Accent))
            {
                if (x > 6) e.Graphics.FillRectangle(brush, 6, cy - 1, x - 6, 3);
                e.Graphics.FillEllipse(brush, x - 5, cy - 5, 11, 11);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) { dragging = true; SetFromX(e.X); } }
        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if (dragging) SetFromX(e.X); }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); dragging = false; }
        void SetFromX(int x)
        {
            double t = (x - 6) / (double)Math.Max(1, Width - 12);
            Value = minimum + (int)Math.Round(Math.Max(0, Math.Min(1, t)) * (maximum - minimum));
        }
    }

    // One small change made by one participant: delete Del characters at Pos and put Ins
    // there instead. Positions are in the author's document at the moment of the edit.
    sealed class LiveOp
    {
        public string Author;
        public int Seq, Pos, Del;
        public string Ins;
        // Text around the edit in the author's document. Receivers re-find this anchor in
        // their own copy, so an edit lands on the same sentence even when the numbers in
        // between drifted because somebody else was typing at the same time.
        public string Prefix, Suffix;
        public DateTime SentAt;
    }

    // A tiny line-protocol link used by live sessions. Either end can host: the host listens
    // and every peer connects to it, then all text and caret messages are broadcast to
    // everyone on the link. Records are single lines of UTF-8 so framing stays simple.
    sealed class LiveNode
    {
        readonly List<TcpClient> clients = new List<TcpClient>();
        readonly object gate = new object();
        TcpListener listener;
        public event Action<string> Line;
        public event Action PeersChanged;

        public bool Hosting { get { return listener != null; } }
        public int Port { get; private set; }
        public int Count { get { lock (gate) return clients.Count; } }

        public int Start(int firstPort)
        {
            for (int port = firstPort; port < firstPort + 20; port++)
            {
                try
                {
                    var l = new TcpListener(IPAddress.Any, port);
                    l.Start();
                    listener = l;
                    Port = port;
                    var thread = new Thread(AcceptLoop);
                    thread.IsBackground = true;
                    thread.Start();
                    return port;
                }
                catch (Exception) { }
            }
            return 0;
        }

        void AcceptLoop()
        {
            while (true)
            {
                TcpClient client;
                try { client = listener.AcceptTcpClient(); }
                catch { return; }
                Adopt(client);
            }
        }

        public void Connect(string host, int port)
        {
            var client = new TcpClient();
            client.Connect(host, port);
            Adopt(client);
        }

        void Adopt(TcpClient client)
        {
            lock (gate) clients.Add(client);
            var thread = new Thread(() => ReadLoop(client));
            thread.IsBackground = true;
            thread.Start();
            RaisePeers();
        }

        void ReadLoop(TcpClient client)
        {
            try
            {
                using (var reader = new StreamReader(client.GetStream(), new UTF8Encoding(false)))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        Action<string> handler = Line;
                        if (handler != null) handler(line);
                    }
                }
            }
            catch { }
            lock (gate) clients.Remove(client);
            try { client.Close(); } catch { }
            RaisePeers();
        }

        void RaisePeers() { Action handler = PeersChanged; if (handler != null) handler(); }

        public void Send(string line)
        {
            byte[] bytes = new UTF8Encoding(false).GetBytes(line + "\n");
            TcpClient[] snapshot;
            lock (gate) snapshot = clients.ToArray();
            foreach (TcpClient client in snapshot)
                try { client.GetStream().Write(bytes, 0, bytes.Length); }
                catch { }
        }

        public void Stop()
        {
            try { if (listener != null) listener.Stop(); } catch { }
            listener = null;
            TcpClient[] snapshot;
            lock (gate) { snapshot = clients.ToArray(); clients.Clear(); }
            foreach (TcpClient client in snapshot) try { client.Close(); } catch { }
            RaisePeers();
        }
    }

    // One person in a shared document: the name they joined with, whether they host, their
    // caret and when they last typed. The host keeps the list and mirrors it to everyone.
    sealed class PeerInfo
    {
        public string Name = "";
        public string Role = "guest";
        public int Line, Col;
        public DateTime LastSeen, TypingUntil;
        public bool IsYou;
        public bool Typing { get { return TypingUntil > DateTime.Now; } }
    }

    // Each participant gets one stable colour, so their avatar, caret line and name match
    // everywhere the person appears.
    static class Presence
    {
        static readonly Color[] Palette =
        {
            Color.FromArgb(66, 133, 244), Color.FromArgb(219, 68, 55), Color.FromArgb(15, 157, 88),
            Color.FromArgb(244, 160, 0), Color.FromArgb(156, 39, 176), Color.FromArgb(0, 172, 193),
            Color.FromArgb(233, 30, 99), Color.FromArgb(63, 81, 181)
        };

        public static Color Of(string name)
        {
            if (String.IsNullOrEmpty(name)) return Palette[0];
            int hash = 0;
            foreach (char c in name) hash = unchecked(hash * 31 + c);
            return Palette[(hash & 0x7fffffff) % Palette.Length];
        }

        public static string Initials(string name)
        {
            string trimmed = (name ?? "").Trim();
            if (trimmed.Length == 0) return "?";
            string[] words = trimmed.Split(new[] { ' ', '.', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length >= 2 && words[0].Length > 0 && words[1].Length > 0)
                return (words[0][0].ToString() + words[1][0]).ToUpperInvariant();
            return trimmed.Substring(0, Math.Min(2, trimmed.Length)).ToUpperInvariant();
        }
    }

    // The people list paints itself from a small model instead of rebuilding child controls
    // for every message. That is what keeps the sharing sidebar from flashing while typing.
    sealed class PeerList : ScrollableControl
    {
        const int RowHeight = 52;
        readonly List<PeerInfo> items = new List<PeerInfo>();
        int hovered = -1;
        public bool Dark = true;
        public event Action<PeerInfo> Chosen;

        public PeerList()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            AutoScroll = true; TabStop = false; Cursor = Cursors.Hand;
        }

        public void SetPeers(IEnumerable<PeerInfo> next)
        {
            List<PeerInfo> copy = next == null ? new List<PeerInfo>() : next.ToList();
            bool same = copy.Count == items.Count;
            for (int i = 0; same && i < copy.Count; i++)
            {
                PeerInfo a = copy[i], b = items[i];
                if (a.Name != b.Name || a.Line != b.Line || a.Col != b.Col || a.Typing != b.Typing
                    || a.IsYou != b.IsYou || a.Role != b.Role) same = false;
            }
            items.Clear(); items.AddRange(copy);
            int height = Math.Max(0, items.Count * RowHeight + 8);
            if (AutoScrollMinSize.Height != height) AutoScrollMinSize = new Size(0, height);
            if (!same) Invalidate();
        }

        Color Ink(bool faded)
        {
            if (Dark) return faded ? Color.FromArgb(170, 162, 170) : Color.FromArgb(238, 233, 237);
            return faded ? Color.FromArgb(118, 112, 120) : Color.FromArgb(30, 30, 32);
        }

        void Write(Graphics g, string text, int x, int y, int width, Color color, bool bold, float size)
        {
            using (var font = new Font(bold ? "Segoe UI Semibold" : "Segoe UI", size))
            using (var brush = new SolidBrush(color))
            using (var format = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                g.DrawString(text ?? "", font, brush, new RectangleF(x, y, Math.Max(8, width), size * 2f), format);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var brush = new SolidBrush(Dark ? Color.FromArgb(43, 34, 42) : Color.White)) e.Graphics.FillRectangle(brush, ClientRectangle);
            if (items.Count == 0)
            {
                Write(e.Graphics, "No one else here yet.", 4, 8, ClientSize.Width - 8, Ink(true), false, 9F);
                return;
            }
            int y = AutoScrollPosition.Y;
            for (int i = 0; i < items.Count; i++, y += RowHeight)
            {
                if (y + RowHeight < 0 || y > ClientSize.Height) continue;
                PeerInfo peer = items[i];
                Color row = i == hovered
                    ? (Dark ? Color.FromArgb(56, 47, 56) : Color.FromArgb(241, 236, 242))
                    : (Dark ? Color.FromArgb(49, 40, 48) : Color.FromArgb(248, 246, 249));
                using (var brush = new SolidBrush(row)) e.Graphics.FillRectangle(brush, 0, y + 2, Math.Max(0, ClientSize.Width - 1), RowHeight - 4);
                Color accent = Presence.Of(peer.Name);
                using (var brush = new SolidBrush(accent)) e.Graphics.FillRectangle(brush, 0, y + 2, 3, RowHeight - 4);

                var avatar = new Rectangle(12, y + 12, 28, 28);
                using (var brush = new SolidBrush(accent)) e.Graphics.FillEllipse(brush, avatar);
                using (var font = new Font("Segoe UI Semibold", 8.5F))
                using (var ink = new SolidBrush(Color.White))
                {
                    string initials = Presence.Initials(peer.Name);
                    SizeF measure = e.Graphics.MeasureString(initials, font);
                    e.Graphics.DrawString(initials, font, ink, avatar.Left + (avatar.Width - measure.Width) / 2f, avatar.Top + (avatar.Height - measure.Height) / 2f + 1f);
                }

                string detail = peer.IsYou
                    ? (peer.Role == "host" ? "You · host" : "You")
                    : "Ln " + Math.Max(1, peer.Line) + ", Col " + Math.Max(1, peer.Col);
                if (peer.Typing && !peer.IsYou) detail += " · editing…";
                Write(e.Graphics, peer.Name, 48, y + 8, ClientSize.Width - 96, Ink(false), true, 9.5F);
                Write(e.Graphics, detail, 48, y + 27, ClientSize.Width - 96, peer.Typing && !peer.IsYou ? accent : Ink(true), false, 8.5F);

                if (peer.IsYou) Write(e.Graphics, "YOU", ClientSize.Width - 44, y + 9, 36, accent, true, 7.5F);
                else if (peer.Typing) using (var brush = new SolidBrush(accent)) e.Graphics.FillEllipse(brush, ClientSize.Width - 22, y + 22, 7, 7);
            }
        }

        int RowAt(int y)
        {
            int index = (y - AutoScrollPosition.Y) / RowHeight;
            return index >= 0 && index < items.Count ? index : -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int index = RowAt(e.Y);
            if (index != hovered) { hovered = index; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hovered != -1) { hovered = -1; Invalidate(); }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            int index = RowAt(e.Y);
            if (index < 0 || items[index].IsYou || Chosen == null) return;
            Chosen(items[index]);
        }
    }

    // A slim gutter beside the page that marks the line each collaborator is on, in their
    // own colour. It is a separate control, so the editor itself is never repainted.
    sealed class PresenceStrip : Control
    {
        readonly List<PeerInfo> items = new List<PeerInfo>();
        public RichTextBox Editor;

        public PresenceStrip()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Width = 9; TabStop = false; BackColor = Color.White;
        }

        public void SetPeers(IEnumerable<PeerInfo> next)
        {
            items.Clear();
            if (next != null) items.AddRange(next.Where(p => !p.IsYou));
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            using (var brush = new SolidBrush(Color.White)) e.Graphics.FillRectangle(brush, ClientRectangle);
            using (var pen = new Pen(Color.FromArgb(228, 223, 230))) e.Graphics.DrawLine(pen, ClientSize.Width - 1, 0, ClientSize.Width - 1, ClientSize.Height);
            if (Editor == null || items.Count == 0) return;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            foreach (PeerInfo peer in items)
            {
                int line = Math.Max(0, peer.Line - 1);
                if (line >= Editor.Lines.Length) continue;
                int index = Editor.GetFirstCharIndexFromLine(line);
                if (index < 0) continue;
                index = Math.Min(Editor.TextLength, index + Math.Max(0, peer.Col - 1));
                Point at = Editor.GetPositionFromCharIndex(index);
                int y = at.Y + Editor.Top;
                if (y < 0 || y > ClientSize.Height) continue;
                using (var brush = new SolidBrush(Presence.Of(peer.Name)))
                    e.Graphics.FillRectangle(brush, 2, y, Math.Max(3, ClientSize.Width - 4), 14);
            }
        }
    }


    sealed class BreakProgram
    {
        public string Name;
        public double Hours;
        public string FilePath;
        public string UninstallCommand;
        public string InstalledName;
        public bool UsageKnown;
        public bool Deleted;
        public Panel Row;
        public Label NameLabel, HoursLabel;

        public BreakProgram(string name, double hours, string filePath = null, string uninstallCommand = null, string installedName = null, bool usageKnown = false)
        {
            Name = name;
            Hours = hours;
            FilePath = filePath ?? "";
            UninstallCommand = uninstallCommand ?? "";
            InstalledName = installedName ?? name;
            UsageKnown = usageKnown;
        }
    }

    sealed class BreakUsageEntry
    {
        public string Path;
        public long FocusMilliseconds;
        public int RunCount;
    }

    sealed class MainForm : Form
    {
        static readonly Color DarkChrome = Color.FromArgb(33, 23, 33);
        static readonly Color DarkPanel = Color.FromArgb(43, 34, 42);
        static readonly Color DarkPage = Color.FromArgb(30, 30, 30);
        static readonly Color DarkText = Color.FromArgb(238, 233, 237);
        static readonly Color DarkBorder = Color.FromArgb(56, 48, 56);
        static readonly Color DarkAccent = Color.FromArgb(220, 145, 196);
        static readonly Color DarkMuted = Color.FromArgb(170, 162, 170);

        // A palette is deliberately just data: presets, the settings file and the
        // customization dialog all speak this shape, so a hand-edited theme behaves
        // exactly like one picked in the UI.
        sealed class UiPalette
        {
            public string Name;
            public bool IsDark;
            public Color Chrome, Panel, Page, Text, Muted, Border, Accent, EditorBack, EditorText;
            public Color? DiceAccent, DicePanel, DiceCard;
            public string FontName;
            public float FontSize;
            public int TabWidth;
            public int? DiceSides, DiceCards, DiceWidth, DiceSpeed;
            public bool? SoftTabs, Wrap, AutoIndent, ShowStatus, TopMost, DiceShowHours, DiceUninstall;
        }

        // MUGEN exposes a Lua layer over its motif files. Milkpad's equivalent is
        // deliberately small and safe: scripts are declarative theme recipes, not
        // arbitrary code. The supported calls are theme.color, theme.font,
        // theme.tabs, theme.flag and theme.preset.
        sealed class FeatureFlag
        {
            public string Key, Name, Description;
            public bool DefaultValue;
        }

        sealed class ThemeScriptResult
        {
            public string Preset, FontName;
            public Color? Chrome, Panel, Page, Text, Muted, Border, Accent, EditorBack, EditorText;
            public Color? DiceAccent, DicePanel, DiceCard;
            public float? FontSize;
            public int? TabWidth, DiceSides, DiceCards, DiceWidth, DiceSpeed;
            public bool? SoftTabs, Wrap, AutoIndent, ShowStatus, TopMost, DiceShowHours, DiceUninstall;
        }

        static ThemeScriptResult ReadThemeScript(string path)
        {
            var result = new ThemeScriptResult();
            var vars = new Dictionary<string, object>(StringComparer.CurrentCultureIgnoreCase);
            string[] lines = File.ReadAllLines(path);
            for (int lineNo = 0; lineNo < lines.Length; lineNo++)
            {
                string source = StripLuaComment(lines[lineNo]);
                foreach (string rawStatement in SplitLuaStatements(source))
                {
                    string line = rawStatement.Trim();
                    if (line.Length == 0 || line == "end") continue;
                    if (line.StartsWith("local ", StringComparison.Ordinal))
                    {
                        int eq = line.IndexOf('=');
                        if (eq <= 6) throw ScriptError(path, lineNo, "expected local name = value");
                        string name = line.Substring(6, eq - 6).Trim();
                        if (name.Length == 0) throw ScriptError(path, lineNo, "expected a variable name after local");
                        vars[name] = ParseLuaValue(line.Substring(eq + 1), vars);
                        continue;
                    }
                    if (!line.StartsWith("theme.", StringComparison.Ordinal))
                        throw ScriptError(path, lineNo, "only local values and theme.* calls are allowed");
                    int open = line.IndexOf('(');
                    int close = line.LastIndexOf(')');
                    if (open < 6 || close < open) throw ScriptError(path, lineNo, "expected theme.call(...)");
                    if (line.Substring(close + 1).Trim().Length > 0) throw ScriptError(path, lineNo, "unexpected text after the closing parenthesis");
                    string call = line.Substring(6, open - 6).Trim().ToLowerInvariant();
                    string[] args = SplitLuaArgs(line.Substring(open + 1, close - open - 1));
                    object[] values = new object[args.Length];
                    for (int i = 0; i < args.Length; i++) values[i] = ParseLuaValue(args[i], vars);

                    if (call == "preset")
                    {
                        if (values.Length == 0) throw ScriptError(path, lineNo, "theme.preset needs a preset name");
                        result.Preset = Convert.ToString(values[0], CultureInfo.InvariantCulture);
                    }
                    else if (call == "color" || call == "colour")
                    {
                        if (values.Length < 2) throw ScriptError(path, lineNo, "theme.color needs a colour name and a value");
                        string key = Convert.ToString(values[0], CultureInfo.InvariantCulture);
                        string value = Convert.ToString(values[1], CultureInfo.InvariantCulture);
                        Color parsed;
                        if (!TryColor(value, out parsed)) throw ScriptError(path, lineNo, "theme.color(..., \"" + value + "\") is not a valid colour");
                        if (!SetScriptColor(result, key, parsed)) throw ScriptError(path, lineNo, "theme.color does not know the colour \"" + key + "\"");
                    }
                    else if (call == "font")
                    {
                        if (values.Length == 0) throw ScriptError(path, lineNo, "theme.font needs a font family");
                        result.FontName = Convert.ToString(values[0], CultureInfo.InvariantCulture);
                        if (values.Length > 1)
                        {
                            float size = ToScriptFloat(values[1]);
                            if (size < 5 || size > 200) throw ScriptError(path, lineNo, "theme.font size must be between 5 and 200");
                            result.FontSize = size;
                        }
                    }
                    else if (call == "tabs" || call == "tab")
                    {
                        if (values.Length == 0) throw ScriptError(path, lineNo, "theme.tabs needs a tab width");
                        int width = ToScriptInt(values[0]);
                        if (width < 1 || width > 8) throw ScriptError(path, lineNo, "theme.tabs width must be between 1 and 8");
                        result.TabWidth = width;
                        if (values.Length > 1) result.SoftTabs = ToScriptBool(values[1]);
                    }
                    else if (call == "flag" || call == "option")
                    {
                        if (values.Length < 2) throw ScriptError(path, lineNo, "theme.flag needs an option name and true or false");
                        string key = Convert.ToString(values[0], CultureInfo.InvariantCulture);
                        if (!SetScriptFlag(result, key, ToScriptBool(values[1]))) throw ScriptError(path, lineNo, "theme.flag does not know the option \"" + key + "\"");
                    }
                    else if (call == "dice")
                    {
                        if (values.Length > 0 && values[0] != null)
                        {
                            int sides = ToScriptInt(values[0]);
                            if (sides < 2 || sides > 100) throw ScriptError(path, lineNo, "theme.dice sides must be between 2 and 100");
                            result.DiceSides = sides;
                        }
                        if (values.Length > 1 && values[1] != null)
                        {
                            int cards = ToScriptInt(values[1]);
                            if (cards < 4 || cards > 50) throw ScriptError(path, lineNo, "theme.dice cards must be between 4 and 50");
                            result.DiceCards = cards;
                        }
                        if (values.Length > 2 && values[2] != null)
                        {
                            int width = ToScriptInt(values[2]);
                            if (width < 240 || width > 520) throw ScriptError(path, lineNo, "theme.dice width must be between 240 and 520");
                            result.DiceWidth = width;
                        }
                        if (values.Length > 3 && values[3] != null)
                        {
                            int speed = ToScriptInt(values[3]);
                            if (speed < 20 || speed > 200) throw ScriptError(path, lineNo, "theme.dice speed must be between 20 and 200");
                            result.DiceSpeed = speed;
                        }
                    }
                    else throw ScriptError(path, lineNo, "unsupported theme call \"" + call + "\"");
                }
            }
            return result;
        }

        static FormatException ScriptError(string path, int lineNo, string message)
        {
            string file = "";
            try { file = Path.GetFileName(path); } catch { }
            return new FormatException((file.Length > 0 ? file + " line " : "Line ") + (lineNo + 1) + ": " + message);
        }

        static IEnumerable<string> SplitLuaStatements(string line)
        {
            var parts = new List<string>();
            bool quoted = false;
            char quote = '\0';
            int start = 0;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (quoted)
                {
                    if (c == '\\') i++;
                    else if (c == quote) quoted = false;
                }
                else if (c == '"' || c == '\'') { quoted = true; quote = c; }
                else if (c == ';') { parts.Add(line.Substring(start, i - start)); start = i + 1; }
            }
            parts.Add(line.Substring(start));
            return parts;
        }

        static bool SetScriptColor(ThemeScriptResult result, string key, Color color)
        {
            switch ((key ?? "").Trim().ToLowerInvariant())
            {
                case "chrome": case "window": result.Chrome = color; return true;
                case "panel": case "menu": result.Panel = color; return true;
                case "page": case "background": result.Page = color; return true;
                case "text": case "ink": result.Text = color; return true;
                case "muted": result.Muted = color; return true;
                case "border": case "line": result.Border = color; return true;
                case "accent": case "highlight": result.Accent = color; return true;
                case "editor": case "editorbg": case "editorbackground": result.EditorBack = color; return true;
                case "editorfg": case "editortext": result.EditorText = color; return true;
                case "diceaccent": result.DiceAccent = color; return true;
                case "dicepanel": result.DicePanel = color; return true;
                case "dicecard": result.DiceCard = color; return true;
            }
            return false;
        }

        static bool SetScriptFlag(ThemeScriptResult result, string key, bool value)
        {
            switch ((key ?? "").Trim().ToLowerInvariant())
            {
                case "wrap": case "wordwrap": result.Wrap = value; return true;
                case "autoindent": case "indent": result.AutoIndent = value; return true;
                case "status": case "statusbar": result.ShowStatus = value; return true;
                case "ontop": case "topmost": result.TopMost = value; return true;
                case "dicehours": result.DiceShowHours = value; return true;
                case "diceuninstall": result.DiceUninstall = value; return true;
            }
            return false;
        }

        static string StripLuaComment(string line)
        {
            bool quoted = false;
            char quote = '\0';
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (quoted)
                {
                    if (c == '\\') i++;
                    else if (c == quote) quoted = false;
                }
                else if (c == '"' || c == '\'') { quoted = true; quote = c; }
                else if (c == '-' && i + 1 < line.Length && line[i + 1] == '-') return line.Substring(0, i);
            }
            return line;
        }

        static string[] SplitLuaArgs(string text)
        {
            var args = new List<string>();
            bool quoted = false;
            char quote = '\0';
            int start = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (quoted)
                {
                    if (c == '\\') i++;
                    else if (c == quote) quoted = false;
                }
                else if (c == '"' || c == '\'') { quoted = true; quote = c; }
                else if (c == ',')
                {
                    string arg = text.Substring(start, i - start).Trim();
                    if (arg.Length > 0) args.Add(arg);
                    start = i + 1;
                }
            }
            string tail = text.Substring(start).Trim();
            if (tail.Length > 0) args.Add(tail);
            return args.ToArray();
        }

        static object ParseLuaValue(string text, Dictionary<string, object> vars)
        {
            string value = (text ?? "").Trim();
            if (value.EndsWith(";", StringComparison.Ordinal)) value = value.Substring(0, value.Length - 1).Trim();
            if ((value.StartsWith("\"") && value.EndsWith("\"")) || (value.StartsWith("'") && value.EndsWith("'")))
            {
                string inner = value.Substring(1, value.Length - 2);
                return inner.Replace("\\n", "\n").Replace("\\t", "\t").Replace("\\\"", "\"").Replace("\\\\", "\\");
            }
            if (value == "true") return true;
            if (value == "false") return false;
            if (value == "nil") return null;
            float number;
            if (Single.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number)) return number;
            object variable;
            if (vars.TryGetValue(value, out variable)) return variable;
            return value;
        }

        static float ToScriptFloat(object value)
        {
            try { return Convert.ToSingle(value, CultureInfo.InvariantCulture); } catch { return 0; }
        }

        static int ToScriptInt(object value)
        {
            try { return Convert.ToInt32(value, CultureInfo.InvariantCulture); } catch { return 0; }
        }

        static bool ToScriptBool(object value)
        {
            if (value is bool) return (bool)value;
            string text = Convert.ToString(value, CultureInfo.InvariantCulture);
            return ParseOnOff(text);
        }

        static readonly List<UiPalette> Palettes = new List<UiPalette>
        {
            new UiPalette { Name = "Dark", IsDark = true,
                Chrome = Color.FromArgb(33, 23, 33), Panel = Color.FromArgb(43, 34, 42), Page = Color.FromArgb(30, 30, 30),
                Text = Color.FromArgb(238, 233, 237), Muted = Color.FromArgb(170, 162, 170), Border = Color.FromArgb(56, 48, 56),
                Accent = Color.FromArgb(220, 145, 196), EditorBack = Color.White, EditorText = Color.FromArgb(24, 24, 26) },
            new UiPalette { Name = "Light", IsDark = false,
                Chrome = Color.FromArgb(234, 234, 234), Panel = Color.FromArgb(250, 250, 250), Page = Color.FromArgb(240, 240, 240),
                Text = Color.FromArgb(26, 26, 26), Muted = Color.FromArgb(96, 90, 96), Border = Color.FromArgb(210, 203, 212),
                Accent = Color.FromArgb(163, 18, 76), EditorBack = Color.White, EditorText = Color.FromArgb(24, 24, 26) },
            new UiPalette { Name = "Midnight", IsDark = true,
                Chrome = Color.FromArgb(11, 14, 19), Panel = Color.FromArgb(22, 27, 34), Page = Color.FromArgb(14, 17, 22),
                Text = Color.FromArgb(230, 237, 243), Muted = Color.FromArgb(139, 148, 158), Border = Color.FromArgb(48, 54, 61),
                Accent = Color.FromArgb(88, 166, 255), EditorBack = Color.FromArgb(13, 17, 23), EditorText = Color.FromArgb(230, 237, 243) },
            new UiPalette { Name = "Sepia", IsDark = false,
                Chrome = Color.FromArgb(232, 220, 195), Panel = Color.FromArgb(251, 245, 230), Page = Color.FromArgb(244, 236, 216),
                Text = Color.FromArgb(59, 47, 47), Muted = Color.FromArgb(122, 106, 90), Border = Color.FromArgb(211, 196, 168),
                Accent = Color.FromArgb(160, 82, 45), EditorBack = Color.FromArgb(255, 248, 231), EditorText = Color.FromArgb(59, 47, 47) },
            new UiPalette { Name = "Aurora", IsDark = true,
                Chrome = Color.FromArgb(18, 26, 38), Panel = Color.FromArgb(25, 38, 54), Page = Color.FromArgb(10, 16, 25),
                Text = Color.FromArgb(225, 245, 255), Muted = Color.FromArgb(137, 160, 180), Border = Color.FromArgb(43, 69, 92),
                Accent = Color.FromArgb(94, 242, 194), EditorBack = Color.FromArgb(9, 15, 23), EditorText = Color.FromArgb(216, 245, 255) }
        };
        readonly Panel tabs = new Panel();
        readonly Panel toolsHost = new Panel();
        readonly Button toolsMore = new Button(), tabPrev = new Button(), tabNext = new Button();
        FlowLayoutPanel toolsRow, menuBar;
        Panel contentHost;
        readonly Panel sheet = new Panel();
        readonly Label emptyHint = new Label();
        int tabScroll;
        readonly RichTextBox editor = new RichTextBox();
        readonly Panel caretBar = new Panel();
        readonly Panel selectionOutline = new Panel();
        readonly Timer caretBlink = new Timer();
        readonly Panel status = new Panel();
        readonly Panel commentPane = new Panel();
        readonly FlowLayoutPanel commentList = new FlowLayoutPanel();
        readonly Label positionLabel = new Label(), charactersLabel = new Label(), formatLabel = new Label(), zoomLabel = new Label(), modeLabel = new Label(), lineEndingLabel = new Label(), encodingLabel = new Label();
        readonly ZoomSlider zoom = new ZoomSlider();
        readonly ToolTip tips = new ToolTip();
        readonly List<FlatCombo> combos = new List<FlatCombo>();
        readonly List<DocState> docs = new List<DocState>();
        readonly List<DocState> closedDocs = new List<DocState>();
        readonly Stack<string> history = new Stack<string>();
        int active = -1;
        bool loading, dark = true, wrap = true, fullScreen, showStatus = true, unixEndings, topMost, autoIndent = true, applyingRemote;
        Color uiChrome = DarkChrome, uiPanel = DarkPanel, uiPage = DarkPage, uiText = DarkText, uiMuted = DarkMuted, uiBorder = DarkBorder, uiAccent = DarkAccent;
        Color editorBack = Color.White, editorText = Color.FromArgb(24, 24, 26);
        Color caretColor = Color.Empty, selectionColor = Color.Empty;
        string uiFontName = "Segoe UI";
        string editorFontName = "Consolas";
        float editorFontSize = 11f;
        int tabWidth = 4;
        bool softTabs = true;
        int pageMargin = 16;
        double windowOpacity = 1.0;
        bool showMenu = true, showToolbar = true;
        bool themesLoaded, featuresLoaded;
        readonly List<FeatureFlag> featureFlags = new List<FeatureFlag>();
        readonly Dictionary<string, bool> featureValues = new Dictionary<string, bool>(StringComparer.CurrentCultureIgnoreCase);
        readonly List<int> bookmarks = new List<int>();
        readonly LiveNode live = new LiveNode();
        readonly Panel livePane = new Panel();
        readonly Panel breakPane = new Panel();
        readonly Panel breakHeader = new Panel();
        readonly FlowLayoutPanel breakBody = new FlowLayoutPanel();
        readonly FlowLayoutPanel breakList = new FlowLayoutPanel();
        readonly Label breakResult = new Label(), breakHistory = new Label(), breakShelfNote = new Label();
        readonly Label breakTitle = new Label(), breakKicker = new Label();
        readonly Button breakRollButton = new Button(), breakClose = new Button(), breakReset = new Button();
        readonly CheckBox breakUninstall = new CheckBox();
        readonly Timer breakRollTimer = new Timer();
        int breakSides = 20, breakCards = 20, breakPanelWidth = 292, breakRollSpeed = 55;
        bool breakShowHours = true;
        Color breakAccent = Color.Empty, breakPanelTint = Color.Empty, breakCardTint = Color.Empty;
        readonly List<BreakProgram> breakPrograms = new List<BreakProgram>();
        readonly List<string> breakLastShelfNames = new List<string>();
        readonly Random breakRandom = new Random();
        int breakTicks, breakFinal, breakScanToken;
        bool breakScanning, breakScanned;
        readonly PeerList peerList = new PeerList();
        readonly PresenceStrip presence = new PresenceStrip();
        readonly Label liveLabel = new Label();
        readonly Label shareStatus = new Label(), shareCode = new Label(), sharePeople = new Label(), shareHint = new Label(), joinHint = new Label();
        readonly Panel shareOnline = new Panel(), shareCodeCard = new Panel();
        readonly Button startShareButton = new Button(), joinButton = new Button(), copyInviteButton = new Button(), copyCodeButton = new Button(), stopShareButton = new Button(), shareButton = new Button();
        readonly TextBox joinBox = new TextBox();
        readonly Timer statusTimer = new Timer(), rtfTimer = new Timer(), caretTimer = new Timer(), typingTimer = new Timer(), beat = new Timer();
        readonly Dictionary<string, PeerInfo> peers = new Dictionary<string, PeerInfo>();
        string shareCodeText = "";
        int copyToken, shareToken;
        readonly string myId = Environment.UserName + "#" + Guid.NewGuid().ToString("N").Substring(0, 4);
        readonly List<LiveOp> outstanding = new List<LiveOp>();
        int localSeq;
        string lastLocalText = "", lastSentRtf = "";
        FormWindowState priorState;
        Rectangle windowedBounds = Rectangle.Empty;
        bool windowMaximized;
        int printChar;
        readonly List<string> recent = new List<string>();
        readonly Timer autosave = new Timer();
        int untitledSeq, flashToken;
        DateTime flashUntil;
        int textVersion, lineCacheVersion = -1;
        List<int> lineStarts = new List<int> { 0 };
        readonly System.Drawing.Text.PrivateFontCollection privateFonts = new System.Drawing.Text.PrivateFontCollection();
        readonly Dictionary<string, string> importedFonts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        FlatCombo fontChooser;
        bool refreshingFonts;
        readonly Panel findBar = new Panel();
        readonly FlowLayoutPanel findRow = new FlowLayoutPanel();
        readonly TextBox findBox = new TextBox(), replaceBox = new TextBox();
        readonly Label findStatus = new Label();
        readonly List<int> matchStarts = new List<int>();
        readonly List<Color> matchBacks = new List<Color>();
        readonly RowStyle findRowStyle = new RowStyle(SizeType.Absolute, 0);
        int matchIndex = -1, highlightLength;
        bool highlighting, bulk;
        static readonly Color MatchColor = Color.FromArgb(255, 214, 92);
        static readonly string AppDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Milkpad");
        [DllImport("user32.dll")] static extern uint GetDpiForSystem();
        static readonly float LayoutScale = Math.Max(1f, GetDpiForSystem() / 96f);
        static int Ui(int value) { return (int)Math.Round(value * LayoutScale); }

        DocState Current { get { return active >= 0 && active < docs.Count ? docs[active] : null; } }

        public MainForm()
        {
            Text = "Milkpad"; MinimumSize = new Size(760, 520); StartPosition = FormStartPosition.CenterScreen;
            AutoScaleDimensions = new SizeF(96F, 96F);
            // The chrome is scaled explicitly from LayoutScale below. Leaving WinForms'
            // DPI auto-scale on as well shrank the already-scaled buttons and status
            // fields when the window crossed into a 150% monitor.
            AutoScaleMode = AutoScaleMode.None;
            // Never open larger than the screen: on a small laptop the right edge, the Share
            // button and the whole status bar used to sit off-screen.
            Rectangle work = Screen.PrimaryScreen == null ? new Rectangle(0, 0, 1250, 820) : Screen.PrimaryScreen.WorkingArea;
            Width = Math.Max(Ui(760), Math.Min(1600, work.Width));
            Height = Math.Max(Ui(520), Math.Min(826, work.Height));
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            BackColor = DarkPage; ForeColor = DarkText; Font = new Font("Segoe UI", 9F); KeyPreview = true; DoubleBuffered = true;
            Resize += (s, e) => { if (!fullScreen && WindowState == FormWindowState.Normal) windowedBounds = Bounds; };
            Move += (s, e) => { if (!fullScreen && WindowState == FormWindowState.Normal) windowedBounds = Bounds; };
            LoadImportedFonts();

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Margin = Padding.Empty, BackColor = DarkPage };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, Ui(34))); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, Ui(34))); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, Ui(35))); layout.RowStyles.Add(findRowStyle); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, Ui(31))); Controls.Add(layout);

            var tabBar = new Panel { Dock = DockStyle.Fill, BackColor = DarkChrome, Padding = new Padding(Ui(8), 0, Ui(8), 0), Margin = Padding.Empty }; layout.Controls.Add(tabBar, 0, 0);
            tabBar.Paint += (s, e) => { using (var pen = new Pen(uiBorder)) e.Graphics.DrawLine(pen, 0, tabBar.ClientSize.Height - 1, tabBar.ClientSize.Width, tabBar.ClientSize.Height - 1); };
            tabs.Dock = DockStyle.Fill; tabs.BackColor = DarkChrome; tabBar.Controls.Add(tabs);
            // The fill panel is docked first so the icon keeps its own strip instead of
            // being painted over the first tab.
            var logo = new Label { Text = "▤", Dock = DockStyle.Left, Width = Ui(33), ForeColor = Color.FromArgb(141, 198, 233), Font = new Font("Segoe UI", 14), TextAlign = ContentAlignment.MiddleCenter };
            tabBar.Controls.Add(logo);
            tabs.Resize += (s, e) => LayoutTabs();
            var addTab = Button("＋ New", Ui(116), Ui(32)); addTab.Font = new Font("Segoe UI Semibold", 9F); addTab.ForeColor = Color.FromArgb(33, 23, 33); addTab.BackColor = DarkAccent; addTab.Margin = new Padding(Ui(8), Ui(3), Ui(2), Ui(3)); addTab.Tag = "accent"; addTab.Click += (s, e) => NewDoc(); tips.SetToolTip(addTab, "New document (Ctrl+N)"); addTab.AccessibleName = "New document";
            tabPrev.Text = "‹"; tabNext.Text = "›";
            tabPrev.Click += (s, e) => ScrollTabs(-1); tabNext.Click += (s, e) => ScrollTabs(1);
            tips.SetToolTip(tabPrev, "Earlier tabs"); tips.SetToolTip(tabNext, "Later tabs");
            var tabActions = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, BackColor = DarkChrome, Margin = Padding.Empty };
            foreach (Button b in new[] { tabPrev, tabNext }) { b.Width = Ui(24); b.Height = Ui(28); b.Visible = false; b.TabStop = false; b.BackColor = DarkChrome; b.ForeColor = DarkMuted; b.Font = new Font("Segoe UI", 11F); b.Cursor = Cursors.Hand; b.Margin = new Padding(Ui(2), Ui(3), Ui(2), Ui(3)); b.FlatStyle = FlatStyle.Flat; b.FlatAppearance.BorderSize = 0; }
            tabActions.Controls.Add(tabPrev); tabActions.Controls.Add(tabNext); tabActions.Controls.Add(addTab);
            shareButton.Text = "Share"; shareButton.AutoSize = false; shareButton.Width = Ui(116); shareButton.Height = Ui(32); shareButton.Margin = new Padding(Ui(10), Ui(6), Ui(2), Ui(6));
            shareButton.BackColor = DarkAccent; shareButton.ForeColor = Color.FromArgb(33, 23, 33); shareButton.Tag = "accent";
            shareButton.FlatStyle = FlatStyle.Flat; shareButton.FlatAppearance.BorderSize = 0;
            shareButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(235, 172, 212); shareButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(201, 122, 178);
            shareButton.Font = new Font("Segoe UI Semibold", 9F); shareButton.Cursor = Cursors.Hand; shareButton.TabStop = false;
            shareButton.Click += (s, e) => ToggleSharePane(); tips.SetToolTip(shareButton, "Share this document and edit together");
            tabActions.Controls.Add(shareButton);
            tabBar.Controls.Add(tabActions);

            // Notepad puts its menus on their own bar above the tool area; the toolbar below
            // carries only formatting, so the chrome reads like a plain text editor.
            menuBar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = DarkChrome, Margin = Padding.Empty, Padding = new Padding(0, 0, Ui(8), 0) };
            layout.Controls.Add(menuBar, 0, 1);
            menuBar.Paint += (s, e) => { using (var pen = new Pen(uiBorder)) e.Graphics.DrawLine(pen, 0, menuBar.ClientSize.Height - 1, menuBar.ClientSize.Width, menuBar.ClientSize.Height - 1); };
            foreach (string m in new[] { "File", "Edit", "View", "Insert", "Format", "Tools", "QoL", "Roll The Dice", "Themes", "Help" }) AddMenu(menuBar, m);
            // Menus sit at the left edge like Notepad's. Centring them left the bar looking
            // shifted away from the tab strip above it and the toolbar below it.

            var tools = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, AutoScroll = false, BackColor = DarkChrome, Margin = Padding.Empty, Padding = new Padding(Ui(10), Ui(3), Ui(10), Ui(3)) };
            toolsRow = tools;
            toolsHost.Dock = DockStyle.Fill; toolsHost.BackColor = DarkChrome; toolsHost.Margin = Padding.Empty; layout.Controls.Add(toolsHost, 0, 2);
            toolsHost.Controls.Add(tools);
            toolsMore.Text = "»"; toolsMore.Width = Ui(26); toolsMore.Dock = DockStyle.None; toolsMore.Visible = false; toolsMore.BackColor = DarkChrome; toolsMore.ForeColor = DarkMuted; toolsMore.Font = new Font("Segoe UI", 11F); toolsMore.FlatStyle = FlatStyle.Flat; toolsMore.FlatAppearance.BorderSize = 0; toolsMore.TabStop = false; toolsMore.Cursor = Cursors.Hand;
            toolsMore.Click += (s, e) => ShowToolOverflow(); tips.SetToolTip(toolsMore, "More tools");
            toolsHost.Controls.Add(toolsMore);
            toolsHost.Paint += (s, e) => { using (var pen = new Pen(uiBorder)) e.Graphics.DrawLine(pen, 0, toolsHost.ClientSize.Height - 1, toolsHost.ClientSize.Width, toolsHost.ClientSize.Height - 1); };
            toolsHost.Resize += (s, e) => UpdateToolOverflow();
            tools.Layout += (s, e) => UpdateToolOverflow();
            var style = new FlatCombo { Width = Ui(122), Height = Ui(26), Font = new Font("Segoe UI", 8.5F), DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = DarkChrome, ForeColor = DarkText };
            style.Items.AddRange(new object[] { "Normal text", "Title", "Subtitle", "Heading 1", "Heading 2", "Heading 3" }); style.SelectedIndex = 0; tips.SetToolTip(style, "Text style"); style.SelectedIndexChanged += (s, e) => ApplyStyle(style.SelectedItem.ToString()); tools.Controls.Add(style);
            var fontName = new FlatCombo { Width = Ui(150), Height = Ui(26), Font = new Font("Segoe UI", 8.5F), DropDownStyle = ComboBoxStyle.DropDown, FlatStyle = FlatStyle.Flat, BackColor = DarkChrome, ForeColor = DarkText };
            fontName.DropDownWidth = Ui(260); fontName.MaxDropDownItems = 18;
            fontChooser = fontName;
            refreshingFonts = true; AddFontNames(fontName); fontName.SelectedIndex = Math.Max(0, fontName.Items.IndexOf("Consolas")); refreshingFonts = false;
            tips.SetToolTip(fontName, "Font family — type to search names, or Format → Import font… to add your own");
            fontName.SelectedIndexChanged += (s, e) => { if (!refreshingFonts) SetFontFamily(fontName.Text); };
            fontName.TextUpdate += (s, e) => { if (!refreshingFonts) FilterFontChoices(fontName.Text); };
            fontName.DropDown += (s, e) => { if (!refreshingFonts && (String.IsNullOrWhiteSpace(fontName.Text) || String.Equals(fontName.Text, editorFontName, StringComparison.OrdinalIgnoreCase))) RefreshFontChoices(editorFontName); };
            fontName.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; ApplyFontSearch(); }
                else if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; fontName.DroppedDown = false; RefreshFontChoices((editor.SelectionFont ?? editor.Font).FontFamily.Name); }
            };
            tools.Controls.Add(fontName);
            var fontSize = new FlatCombo { Width = Ui(56), Height = Ui(26), Font = new Font("Segoe UI", 8.5F), DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = DarkChrome, ForeColor = DarkText };
            fontSize.Items.AddRange(new object[] { "8", "9", "10", "11", "12", "14", "16", "18", "20", "24", "28", "36", "48", "72" }); fontSize.SelectedIndex = 3; tips.SetToolTip(fontSize, "Font size");
            foreach (ComboBox combo in new[] { style, fontName, fontSize }) ThemeCombo(combo); fontSize.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) SetFontSize(fontSize.Text); }; fontSize.SelectedIndexChanged += (s, e) => SetFontSize(fontSize.Text); tools.Controls.Add(fontSize);
            Divider(tools);
            AddTool(tools, "B", "Bold", (s, e) => ToggleStyle(FontStyle.Bold), true);
            AddTool(tools, "I", "Italic", (s, e) => ToggleStyle(FontStyle.Italic), false, true);
            AddTool(tools, "U̲", "Underline", (s, e) => ToggleStyle(FontStyle.Underline));
            AddTool(tools, "S̶", "Strikethrough", (s, e) => ToggleStyle(FontStyle.Strikeout));
            AddTool(tools, "A ▾", "Text color", (s, e) => ChooseColor(false));
            AddTool(tools, "▰ ▾", "Highlight color", (s, e) => ChooseColor(true)); Divider(tools);
            AddTool(tools, "☷", "Bulleted list", (s, e) => ToggleBullets()); AddTool(tools, "1.", "Numbered list", (s, e) => ToggleNumbers());
            AddTool(tools, "⇤", "Decrease indent", (s, e) => editor.SelectionIndent = Math.Max(0, editor.SelectionIndent - 24)); AddTool(tools, "⇥", "Increase indent", (s, e) => editor.SelectionIndent += 24); Divider(tools);
            AddTool(tools, "≡", "Align left", (s, e) => Align(HorizontalAlignment.Left)); AddTool(tools, "☰", "Center", (s, e) => Align(HorizontalAlignment.Center));
            Divider(tools); AddTool(tools, "↗", "Insert link", (s, e) => InsertLink()); AddTool(tools, "▦", "Insert table", (s, e) => InsertTable()); AddTool(tools, "☺", "Insert emoji", (s, e) => ShowEmojiPicker());

            findBar.Dock = DockStyle.Fill; findBar.BackColor = DarkPanel; findBar.Visible = false; findBar.Padding = new Padding(10, 4, 10, 2); layout.Controls.Add(findBar, 0, 3);
            findRow.Dock = DockStyle.Fill; findRow.WrapContents = false; findRow.BackColor = DarkPanel; findRow.AutoScroll = true; findBar.Controls.Add(findRow);
            findRow.Controls.Add(new Label { Text = "Find", AutoSize = false, Width = 36, Height = 27, ForeColor = DarkText, TextAlign = ContentAlignment.MiddleLeft });
            findBox.Width = 230; findBox.Height = 27; findBox.BackColor = Color.FromArgb(58, 53, 58); findBox.ForeColor = DarkText; findBox.BorderStyle = BorderStyle.FixedSingle; findRow.Controls.Add(findBox);
            var findPrevButton = Button("↑", 34, 27); tips.SetToolTip(findPrevButton, "Previous match (Shift+F3)"); findPrevButton.Click += (s, e) => FindStep(-1); findRow.Controls.Add(findPrevButton);
            var findNextButton = Button("↓", 34, 27); tips.SetToolTip(findNextButton, "Next match (F3)"); findNextButton.Click += (s, e) => FindStep(1); findRow.Controls.Add(findNextButton);
            findRow.Controls.Add(new Label { Text = "Replace", AutoSize = false, Width = 62, Height = 27, ForeColor = DarkText, TextAlign = ContentAlignment.MiddleLeft });
            replaceBox.Width = 190; replaceBox.Height = 27; replaceBox.BackColor = Color.FromArgb(58, 53, 58); replaceBox.ForeColor = DarkText; replaceBox.BorderStyle = BorderStyle.FixedSingle; findRow.Controls.Add(replaceBox);
            var replaceButton = Button("Replace", 68, 27); replaceButton.Click += (s, e) => ReplaceCurrent(); findRow.Controls.Add(replaceButton);
            var replaceAllButton = Button("All", 40, 27); tips.SetToolTip(replaceAllButton, "Replace every match"); replaceAllButton.Click += (s, e) => ReplaceAllMatches(); findRow.Controls.Add(replaceAllButton);
            findStatus.AutoSize = false; findStatus.Width = 120; findStatus.Height = 27; findStatus.ForeColor = Color.FromArgb(205, 199, 205); findStatus.Font = new Font("Segoe UI", 8.5F); findStatus.TextAlign = ContentAlignment.MiddleLeft; findRow.Controls.Add(findStatus);
            var closeFindButton = Button("×", 34, 27); tips.SetToolTip(closeFindButton, "Close (Esc)"); closeFindButton.Click += (s, e) => HideFindBar(); findRow.Controls.Add(closeFindButton);
            findBox.TextChanged += (s, e) => UpdateMatches();
            findBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { FindStep(e.Shift ? -1 : 1); e.SuppressKeyPress = true; } else if (e.KeyCode == Keys.Escape) { HideFindBar(); e.SuppressKeyPress = true; } };
            replaceBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { ReplaceCurrent(); e.SuppressKeyPress = true; } else if (e.KeyCode == Keys.Escape) { HideFindBar(); e.SuppressKeyPress = true; } };

            var content = new Panel { Dock = DockStyle.Fill, BackColor = DarkPage, Padding = Padding.Empty }; contentHost = content; layout.Controls.Add(content, 0, 4);
            commentPane.Width = 250; commentPane.Dock = DockStyle.Right; commentPane.BackColor = DarkPanel; commentPane.Visible = false; content.Controls.Add(commentPane);
            var cHead = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = DarkPanel };
            cHead.Controls.Add(new Label { Text = "Comments", Dock = DockStyle.Left, Width = 145, ForeColor = DarkText, Font = new Font("Segoe UI Semibold", 10), TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0) });
            var cClose = Button("×", 35, 31); cClose.Dock = DockStyle.Right; cClose.Click += (s, e) => commentPane.Visible = false; cHead.Controls.Add(cClose); commentPane.Controls.Add(cHead);
            commentList.Dock = DockStyle.Fill; commentList.FlowDirection = FlowDirection.TopDown; commentList.WrapContents = false; commentList.AutoScroll = true; commentList.BackColor = DarkPanel; commentPane.Controls.Add(commentList);

            livePane.Width = 292; livePane.Dock = DockStyle.Right; livePane.BackColor = DarkPanel; livePane.Visible = false;
            var lHead = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = DarkPanel, Padding = new Padding(19, 0, 6, 0) };
            lHead.Controls.Add(new Label { Text = "Share", Dock = DockStyle.Left, Width = 200, ForeColor = DarkText, Font = new Font("Segoe UI Semibold", 12), TextAlign = ContentAlignment.MiddleLeft });
            var lClose = Button("×", 34, 32); lClose.Dock = DockStyle.Right; lClose.Font = new Font("Segoe UI", 12); lClose.Click += (s, e) => livePane.Visible = false; lHead.Controls.Add(lClose);
            var shareBody = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = DarkPanel, Padding = new Padding(19, 6, 19, 14) };

            shareStatus.AutoSize = false; shareStatus.Width = 254; shareStatus.Height = 58; shareStatus.ForeColor = DarkMuted; shareStatus.Font = new Font("Segoe UI", 9F); shareStatus.Margin = new Padding(0, 0, 0, 12);
            shareBody.Controls.Add(shareStatus);

            startShareButton.Text = "Start sharing"; startShareButton.Width = 254; startShareButton.Height = 38;
            startShareButton.BackColor = DarkAccent; startShareButton.ForeColor = Color.FromArgb(33, 23, 33); startShareButton.Tag = "accent";
            startShareButton.FlatStyle = FlatStyle.Flat; startShareButton.FlatAppearance.BorderSize = 0;
            startShareButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(235, 172, 212); startShareButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(201, 122, 178);
            startShareButton.Font = new Font("Segoe UI Semibold", 9.5F); startShareButton.Cursor = Cursors.Hand; startShareButton.Margin = new Padding(0, 0, 0, 14);
            tips.SetToolTip(startShareButton, "Open a port and get a code others can join with");
            startShareButton.Click += (s, e) => StartLive();
            shareBody.Controls.Add(startShareButton);

            joinHint.AutoSize = false; joinHint.Width = 254; joinHint.Height = 22; joinHint.ForeColor = DarkMuted; joinHint.Font = new Font("Segoe UI", 8.5F); joinHint.Margin = new Padding(0, 0, 0, 4);
            joinHint.Text = "Or join a document someone shared with you";
            shareBody.Controls.Add(joinHint);

            joinBox.Width = 254; joinBox.Height = 28; joinBox.Font = new Font("Consolas", 10F); joinBox.BorderStyle = BorderStyle.FixedSingle;
            joinBox.BackColor = Color.FromArgb(58, 53, 58); joinBox.ForeColor = DarkText; joinBox.Margin = new Padding(0, 0, 0, 8);
            joinBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { JoinShared(joinBox.Text); e.SuppressKeyPress = true; } };
            shareBody.Controls.Add(joinBox);

            joinButton.Text = "Join session"; joinButton.Width = 254; joinButton.Height = 34; joinButton.Margin = new Padding(0, 0, 0, 0);
            joinButton.Click += (s, e) => JoinShared(joinBox.Text);
            shareBody.Controls.Add(joinButton);

            shareOnline.Width = 254; shareOnline.Height = 430; shareOnline.Visible = false; shareOnline.BackColor = DarkPanel; shareOnline.Margin = Padding.Empty;
            shareBody.Controls.Add(shareOnline);

            shareCodeCard.SetBounds(0, 4, 254, 54); shareCodeCard.BackColor = Color.FromArgb(51, 42, 51);
            shareCodeCard.Paint += (s, e) => { using (var pen = new Pen(dark ? Color.FromArgb(88, 74, 88) : Color.FromArgb(218, 210, 220))) e.Graphics.DrawRectangle(pen, 0, 0, shareCodeCard.Width - 1, shareCodeCard.Height - 1); };
            shareCode.Dock = DockStyle.Fill; shareCode.AutoSize = false; shareCode.Padding = new Padding(12, 0, 0, 0); shareCode.Font = new Font("Consolas", 11F, FontStyle.Bold);
            shareCode.TextAlign = ContentAlignment.MiddleLeft; shareCode.ForeColor = DarkAccent;
            shareCodeCard.Controls.Add(shareCode);
            copyCodeButton.Text = "Copy"; copyCodeButton.Dock = DockStyle.Right; copyCodeButton.Width = 62; copyCodeButton.FlatStyle = FlatStyle.Flat; copyCodeButton.FlatAppearance.BorderSize = 0;
            copyCodeButton.BackColor = Color.FromArgb(62, 51, 62); copyCodeButton.ForeColor = DarkText; copyCodeButton.Cursor = Cursors.Hand;
            copyCodeButton.Click += (s, e) => CopyShareText(shareCodeText, "Session code copied", copyCodeButton);
            tips.SetToolTip(copyCodeButton, "Copy the session code");
            shareCodeCard.Controls.Add(copyCodeButton);
            shareOnline.Controls.Add(shareCodeCard);

            copyInviteButton.Text = "Copy invite"; copyInviteButton.SetBounds(0, 68, 254, 38);
            copyInviteButton.BackColor = DarkAccent; copyInviteButton.ForeColor = Color.FromArgb(33, 23, 33); copyInviteButton.Tag = "accent";
            copyInviteButton.FlatStyle = FlatStyle.Flat; copyInviteButton.FlatAppearance.BorderSize = 0;
            copyInviteButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(235, 172, 212); copyInviteButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(201, 122, 178);
            copyInviteButton.Font = new Font("Segoe UI Semibold", 9.5F); copyInviteButton.Cursor = Cursors.Hand;
            copyInviteButton.Click += (s, e) => CopyShareText(InviteText(), "Invite copied", copyInviteButton);
            tips.SetToolTip(copyInviteButton, "Copy a short message with the code and the join steps");
            shareOnline.Controls.Add(copyInviteButton);

            stopShareButton.Text = "Stop sharing"; stopShareButton.SetBounds(0, 114, 254, 30); stopShareButton.ForeColor = DarkMuted; stopShareButton.FlatStyle = FlatStyle.Flat;
            stopShareButton.FlatAppearance.BorderSize = 0; stopShareButton.Cursor = Cursors.Hand;
            stopShareButton.Click += (s, e) => StopLive();
            shareOnline.Controls.Add(stopShareButton);

            sharePeople.AutoSize = false; sharePeople.SetBounds(0, 154, 254, 22); sharePeople.ForeColor = DarkText; sharePeople.Font = new Font("Segoe UI Semibold", 9.5F);
            shareOnline.Controls.Add(sharePeople);

            peerList.SetBounds(0, 178, 254, 190); peerList.BackColor = DarkPanel; peerList.Dark = true; peerList.Chosen += peer => JumpToPeer(peer);
            tips.SetToolTip(peerList, "Click someone to jump to their cursor");
            shareOnline.Controls.Add(peerList);

            shareHint.AutoSize = false; shareHint.SetBounds(0, 372, 254, 40); shareHint.ForeColor = DarkMuted; shareHint.Font = new Font("Segoe UI", 8.5F);
            shareHint.Text = "Changes appear as you type. Click a person to jump to their cursor.";
            shareOnline.Controls.Add(shareHint);

            var shareNote = new Label { AutoSize = false, Width = 254, Height = 92, ForeColor = Color.FromArgb(120, 114, 122), Font = new Font("Segoe UI", 8.5F), Margin = new Padding(0, 12, 0, 0), Text = "Direct connection between your computers. No account, nothing is uploaded. The first time you share, Windows may ask for firewall access: allow it on private networks so the other person can connect. Only share the code with people you trust." };
            shareBody.Controls.Add(shareNote);
            livePane.Controls.Add(shareBody); livePane.Controls.Add(lHead);

            content.Controls.Add(livePane);

            breakPane.Width = breakPanelWidth; breakPane.Dock = DockStyle.Right; breakPane.BackColor = DarkPanel; breakPane.Visible = false;
            breakHeader.Dock = DockStyle.Top; breakHeader.Height = 48; breakHeader.BackColor = DarkPanel; breakHeader.Padding = new Padding(19, 0, 6, 0);
            breakTitle.Text = "Program Shelf"; breakTitle.Dock = DockStyle.Left; breakTitle.Width = 208; breakTitle.ForeColor = DarkText; breakTitle.Font = new Font("Segoe UI Semibold", 12); breakTitle.TextAlign = ContentAlignment.MiddleLeft;
            breakClose.Text = "×"; breakClose.Dock = DockStyle.Right; breakClose.Width = 34; breakClose.Height = 32; breakClose.Font = new Font("Segoe UI", 12); breakClose.BackColor = DarkPanel; breakClose.ForeColor = DarkMuted; breakClose.FlatStyle = FlatStyle.Flat; breakClose.FlatAppearance.BorderSize = 0; breakClose.Cursor = Cursors.Hand; breakClose.TabStop = false;
            breakClose.Click += (s, e) => breakPane.Visible = false;
            breakHeader.Controls.Add(breakTitle); breakHeader.Controls.Add(breakClose);
            breakBody.Dock = DockStyle.Fill; breakBody.FlowDirection = FlowDirection.TopDown; breakBody.WrapContents = false; breakBody.AutoScroll = true; breakBody.BackColor = DarkPanel; breakBody.Padding = new Padding(19, 6, 19, 14);
            breakKicker.Text = "D20 PROGRAM SHELF  •  ROLL & CLEAR"; breakKicker.AutoSize = false; breakKicker.Width = 254; breakKicker.Height = 20; breakKicker.ForeColor = DarkAccent; breakKicker.Font = new Font("Segoe UI Semibold", 8F); breakKicker.Margin = new Padding(0, 0, 0, 2);
            breakResult.Text = "—"; breakResult.AutoSize = false; breakResult.Width = 254; breakResult.Height = 76; breakResult.TextAlign = ContentAlignment.MiddleCenter;
            breakResult.ForeColor = DarkText; breakResult.Font = new Font("Consolas", 40F, FontStyle.Bold); breakResult.Margin = new Padding(0, 0, 0, 4);
            breakRollButton.Text = "Roll d20"; breakRollButton.Width = 254; breakRollButton.Height = 38; breakRollButton.Margin = new Padding(0, 0, 0, 8);
            breakRollButton.BackColor = DarkAccent; breakRollButton.ForeColor = Color.FromArgb(33, 23, 33); breakRollButton.Tag = "accent";
            breakRollButton.FlatStyle = FlatStyle.Flat; breakRollButton.FlatAppearance.BorderSize = 0; breakRollButton.Font = new Font("Segoe UI Semibold", 9.5F); breakRollButton.Cursor = Cursors.Hand;
            breakRollButton.Click += (s, e) => RollBreak();
            breakUninstall.Text = "Run the matching uninstaller"; breakUninstall.AutoSize = false; breakUninstall.Width = 254; breakUninstall.Height = 24;
            breakUninstall.ForeColor = DarkMuted; breakUninstall.Font = new Font("Segoe UI", 8.5F); breakUninstall.BackColor = DarkPanel; breakUninstall.Margin = new Padding(0, 0, 0, 4); breakUninstall.Checked = true;
            tips.SetToolTip(breakUninstall, "When checked, a die result opens the matching installed app's normal Windows uninstaller. The uninstaller still asks before removing anything.");
            breakShelfNote.Text = "Each new shelf draws a different random set of installed apps. Hours are Windows focus time; — means no usage record.";
            breakShelfNote.AutoSize = false; breakShelfNote.Width = 254; breakShelfNote.Height = 56; breakShelfNote.ForeColor = DarkMuted; breakShelfNote.Font = new Font("Segoe UI", 8.5F); breakShelfNote.Margin = new Padding(0, 0, 0, 8);
            breakList.Width = 254; breakList.AutoSize = true; breakList.AutoSizeMode = AutoSizeMode.GrowAndShrink; breakList.FlowDirection = FlowDirection.TopDown; breakList.WrapContents = false; breakList.BackColor = DarkPanel; breakList.Margin = new Padding(0);
            breakHistory.Text = "No rolls yet. Number 1 erases the app with the highest focus time."; breakHistory.AutoSize = false; breakHistory.Width = 254; breakHistory.Height = 64; breakHistory.ForeColor = DarkMuted; breakHistory.Font = new Font("Segoe UI", 8.5F); breakHistory.Margin = new Padding(0, 8, 0, 4);
            breakReset.Text = "NEW PROGRAM SHELF"; breakReset.Width = 254; breakReset.Height = 30; breakReset.Margin = new Padding(0, 2, 0, 0); breakReset.BackColor = DarkPanel; breakReset.ForeColor = DarkText; breakReset.FlatStyle = FlatStyle.Flat; breakReset.FlatAppearance.BorderSize = 0; breakReset.Cursor = Cursors.Hand; breakReset.TabStop = false;
            breakReset.Click += (s, e) => ResetBreakShelf();
            breakBody.Controls.Add(breakKicker); breakBody.Controls.Add(breakResult); breakBody.Controls.Add(breakRollButton); breakBody.Controls.Add(breakUninstall); breakBody.Controls.Add(breakShelfNote); breakBody.Controls.Add(breakList); breakBody.Controls.Add(breakHistory); breakBody.Controls.Add(breakReset);
            breakPane.Controls.Add(breakBody); breakPane.Controls.Add(breakHeader); content.Controls.Add(breakPane);
            breakRollTimer.Interval = breakRollSpeed; breakRollTimer.Tick += (s, e) => BreakRollTick();
            ResetBreakShelf();

            editor.Dock = DockStyle.Fill; editor.BorderStyle = BorderStyle.None; editor.BackColor = Color.White; editor.ForeColor = Color.FromArgb(32, 32, 32); editor.Font = new Font("Consolas", 11F); editor.WordWrap = true; editor.DetectUrls = true; editor.HideSelection = false; editor.AcceptsTab = true;
            caretBar.Width = 2; caretBar.Height = 16; caretBar.Visible = false; caretBar.Enabled = false; caretBar.TabStop = false; caretBar.BackColor = editorText;
            sheet.Controls.Add(caretBar);
            selectionOutline.Visible = false; selectionOutline.Enabled = false; selectionOutline.TabStop = false; selectionOutline.BackColor = uiAccent;
            sheet.Controls.Add(selectionOutline);
            caretBlink.Interval = 500; caretBlink.Tick += (s, e) =>
            {
                if (caretColor == Color.Empty || !editor.Focused || editor.SelectionLength > 0 || emptyHint.Visible)
                { caretBar.Visible = false; return; }
                UpdateCaretOverlay();
                caretBar.Visible = !caretBar.Visible;
                try { HideCaret(editor.Handle); } catch { }
            };
            editor.GotFocus += (s, e) => { caretBlink.Start(); UpdateOverlays(); };
            editor.LostFocus += (s, e) => { caretBlink.Stop(); caretBar.Visible = false; selectionOutline.Visible = false; try { ShowCaret(editor.Handle); } catch { } };
            editor.KeyUp += (s, e) => UpdateOverlays();
            editor.MouseWheel += (s, e) =>
            {
                if ((ModifierKeys & Keys.Control) == 0) return;
                zoom.Value = Math.Max(zoom.Minimum, Math.Min(zoom.Maximum, zoom.Value + (e.Delta > 0 ? 10 : -10)));
            };
            editor.MouseUp += (s, e) => UpdateOverlays();
            editor.MouseDown += (s, e) => { try { HideCaret(editor.Handle); } catch { } };
            editor.VScroll += (s, e) => UpdateOverlays();
            editor.HScroll += (s, e) => UpdateOverlays();
            editor.Resize += (s, e) => UpdateOverlays();
            editor.SelectionChanged += (s, e) =>
            {
                UpdateOverlays();
                UpdateStatus();
                if (live.Count == 0 || applyingRemote) return;
                caretTimer.Stop(); caretTimer.Start();
            };
            editor.TextChanged += (s, e) =>
            {
                UpdateOverlays();
                textVersion++;
                if (applyingRemote) return;
                if (loading || highlighting || Current == null) { lastLocalText = editor.Text; return; }
                bool wasDirty = Current.Dirty;
                Current.Dirty = true;
                if (!wasDirty) { UpdateTabLabels(); UpdateTitle(); }
                UpdateStatus();
                rtfTimer.Stop(); rtfTimer.Start();
                presence.Invalidate();
                EmitLocalOp();
            };
            editor.KeyDown += EditorKeys;
            // Only web and mail links open from document text; anything else (file:, ms-msdt:, custom
// protocol handlers) is ignored instead of being handed to the shell.
            editor.LinkClicked += (s, e) =>
            {
                try
                {
                    Uri link;
                    string text = e.LinkText ?? "";
                    if (!Uri.TryCreate(text, UriKind.Absolute, out link)) return;
                    string scheme = link.Scheme.ToLowerInvariant();
                    if (scheme != "http" && scheme != "https" && scheme != "mailto") return;
                    System.Diagnostics.Process.Start(link.AbsoluteUri);
                }
                catch { }
            };
            sheet.BackColor = Color.White; sheet.Padding = new Padding(Ui(16), Ui(20), Ui(16), Ui(16));
            content.Controls.Add(sheet);
            presence.Editor = editor; presence.Dock = DockStyle.Left; presence.Width = 9; presence.Visible = false;
            editor.VScroll += (s, e) => presence.Invalidate();
            editor.HScroll += (s, e) => presence.Invalidate();
            editor.Resize += (s, e) => presence.Invalidate();
            sheet.Controls.Add(editor); sheet.Controls.Add(presence);
            sheet.Paint += (s, e) => { using (var pen = new Pen(uiBorder)) e.Graphics.DrawRectangle(pen, 0, 0, sheet.ClientSize.Width - 1, sheet.ClientSize.Height - 1); };
            editor.BringToFront(); commentPane.BringToFront(); livePane.BringToFront();
            emptyHint.AutoSize = false; emptyHint.TextAlign = ContentAlignment.MiddleCenter;
            emptyHint.Text = "Start typing, or press Ctrl+O to open a document";
            emptyHint.Font = new Font("Segoe UI", 10F);
            emptyHint.ForeColor = Color.FromArgb(158, 152, 158); emptyHint.BackColor = Color.White;
            emptyHint.Cursor = Cursors.IBeam; emptyHint.Visible = false;
            emptyHint.Click += (s, e) => editor.Focus();
            sheet.Controls.Add(emptyHint); emptyHint.BringToFront();
            content.Layout += (s, e) => { LayoutSheet(); PlaceHint(); };
            editor.SizeChanged += (s, e) => PlaceHint();

            status.Dock = DockStyle.Fill; status.BackColor = DarkChrome; status.Padding = new Padding(Ui(10), 0, Ui(10), 0); status.Margin = Padding.Empty; layout.Controls.Add(status, 0, 5);
            status.Paint += (s, e) => { using (var pen = new Pen(uiBorder)) e.Graphics.DrawLine(pen, 0, 0, status.ClientSize.Width, 0); };
            var statusLeft = new FlowLayoutPanel { Dock = DockStyle.Left, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, BackColor = DarkChrome, Margin = Padding.Empty, Padding = new Padding(0, Ui(3), 0, 0) };
            var statusRight = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, BackColor = DarkChrome, Margin = Padding.Empty, Padding = new Padding(0, Ui(3), 0, 0) };
            status.Controls.Add(statusLeft); status.Controls.Add(statusRight);
            AddStatus(statusLeft, positionLabel, 160); AddStatus(statusLeft, charactersLabel, 90); AddStatus(statusLeft, formatLabel, 64); AddStatus(statusLeft, modeLabel, 80); AddStatus(statusLeft, liveLabel, 120); liveLabel.Text = "";
            liveLabel.Cursor = Cursors.Hand; tips.SetToolTip(liveLabel, "Open sharing options"); liveLabel.Click += (s, e) => ToggleSharePane();
            zoom.Minimum = 50; zoom.Maximum = 200; zoom.Value = 100; zoom.Width = Ui(96); zoom.Height = Ui(23); zoom.Margin = new Padding(0, Ui(1), Ui(16), 0); zoom.BackColor = DarkChrome; zoom.ValueChanged += (s, e) => { editor.ZoomFactor = zoom.Value / 100f; zoomLabel.Text = zoom.Value + "%"; };
            zoomLabel.Text = "100%"; AddStatus(statusRight, zoomLabel, 46); zoomLabel.Cursor = Cursors.Hand; zoomLabel.Click += (s, e) => zoom.Value = 100;
            statusRight.Controls.Add(zoom);
            lineEndingLabel.Text = "Windows (CRLF)"; encodingLabel.Text = "UTF-8";
            AddStatus(statusRight, lineEndingLabel, 114); AddStatus(statusRight, encodingLabel, 52);
            lineEndingLabel.Cursor = Cursors.Hand;
            lineEndingLabel.Click += (s, e) => { unixEndings = !unixEndings; lineEndingLabel.Text = unixEndings ? "Unix (LF)" : "Windows (CRLF)"; tips.SetToolTip(lineEndingLabel, unixEndings ? "Documents are saved with Unix line endings" : "Documents are saved with Windows line endings"); Flash(unixEndings ? "Unix line endings" : "Windows line endings"); SaveSettings(); };
            tips.SetToolTip(lineEndingLabel, "Documents are saved with Windows line endings"); tips.SetToolTip(encodingLabel, "Documents are saved as UTF-8");

            KeyDown += KeysPressed; FormClosing += HandleFormClosing; NewDoc(); UpdateToolOverflow(); LayoutSheet();
            AllowDrop = true; DragEnter += DragFilesOver; DragDrop += Dropped;
            autosave.Interval = 30000; autosave.Tick += (s, e) => SaveSession(); autosave.Start();
            live.Line += ReceiveLive; live.PeersChanged += LivePeersChanged;
            statusTimer.Interval = 200; statusTimer.Tick += (s, e) => { statusTimer.Stop(); UpdateStatus(); };
            rtfTimer.Interval = 600; rtfTimer.Tick += (s, e) => { rtfTimer.Stop(); SaveEditor(); };
            caretTimer.Interval = 150; caretTimer.Tick += (s, e) => { caretTimer.Stop(); SendCaret(); };
            typingTimer.Interval = 400; typingTimer.Tick += (s, e) => { if (!peers.Values.Any(p => p.Typing)) typingTimer.Stop(); UpdateShareUi(); };
            beat.Interval = 4000; beat.Tick += (s, e) => Beat();
            LoadFeatureFlags();
            breakUninstall.Checked = FeatureEnabled("break-uninstall");
            LoadThemeFiles();
            LoadRecent();
            LoadSettings();
            ApplyTheme();
            ApplyDiceSettings();
            // The toolbar dropdowns are first in tab order, so showing the form used to leave
            // focus there and the first letters typed changed the text style instead.
            Shown += (s, e) => { OfferRecovery(); editor.Focus(); };
        }

        Button Button(string text, int width, int height) { var b = new Button { Text = text, Width = width, Height = height, BackColor = DarkPanel, ForeColor = DarkText, FlatStyle = FlatStyle.Flat, Margin = new Padding(2, 0, 2, 0), Cursor = Cursors.Hand, TabStop = false }; b.FlatAppearance.BorderSize = 0; b.FlatAppearance.MouseOverBackColor = Color.FromArgb(64, 50, 64); b.FlatAppearance.MouseDownBackColor = Color.FromArgb(76, 59, 76); return b; }
        void AddStatus(Control host, Label l, int width) { l.AutoSize = true; l.Width = Ui(width); l.MinimumSize = new Size(0, Ui(25)); l.Margin = new Padding(0, 0, Ui(14), 0); l.TextAlign = ContentAlignment.MiddleLeft; l.ForeColor = DarkMuted; l.Font = new Font("Segoe UI", 8.5F); host.Controls.Add(l); }
        void AddMenu(FlowLayoutPanel row, string name) { var b = Button(name, 40, Ui(34)); b.Font = new Font("Segoe UI", 9F); b.AutoSize = true; b.AutoSizeMode = AutoSizeMode.GrowAndShrink; b.Padding = new Padding(Ui(14), 0, Ui(14), 0); b.MinimumSize = new Size(0, Ui(34)); b.Margin = Padding.Empty; b.Click += (s, e) => ShowMenu(name, b); row.Controls.Add(b); }
        void AddTool(FlowLayoutPanel row, string text, string tip, EventHandler fn, bool bold = false, bool italic = false) { var b = Button(text, Ui(28), Ui(28)); b.Font = new Font("Segoe UI", 9F, (bold ? FontStyle.Bold : FontStyle.Regular) | (italic ? FontStyle.Italic : FontStyle.Regular)); b.AutoSize = true; b.AutoSizeMode = AutoSizeMode.GrowAndShrink; b.Padding = new Padding(Ui(5), 0, Ui(5), 0); b.MinimumSize = new Size(Ui(28), Ui(28)); b.Margin = Padding.Empty; tips.SetToolTip(b, tip); b.Click += fn; row.Controls.Add(b); }
        void Divider(FlowLayoutPanel row) { row.Controls.Add(new Panel { Width = Ui(1), Height = Ui(18), BackColor = DarkBorder, Margin = new Padding(Ui(5), Ui(5), Ui(5), 0) }); }
        //==== persistence: document metadata, autosave session, recent files ====
        static void EnsureDir(string dir) { try { Directory.CreateDirectory(dir); } catch { } }
        static string Hash(string value)
        {
            using (var sha = SHA1.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
                var sb = new StringBuilder();
                foreach (byte b in bytes) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
        string MetaPath(DocState d) { return Path.Combine(AppDataDir, "meta", Hash(d.Path.Length > 0 ? "file:" + d.Path.ToLowerInvariant() : "doc:" + d.Id) + ".meta"); }

        void SaveMetadata(DocState d)
        {
            if (d == null) return;
            try
            {
                EnsureDir(Path.Combine(AppDataDir, "meta"));
                var sb = new StringBuilder();
                sb.Append("comments\n");
                foreach (string c in d.Comments) sb.Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(c))).Append('\n');
                sb.Append("versions\n");
                foreach (string v in d.Versions) sb.Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(v))).Append('\n');
                File.WriteAllText(MetaPath(d), sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        void LoadMetadata(DocState d)
        {
            if (d == null) return;
            try
            {
                string path = MetaPath(d);
                if (!File.Exists(path)) return;
                List<string> target = d.Comments;
                string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                foreach (string raw in lines)
                {
                    string line = raw.Trim();
                    if (line.Length == 0) continue;
                    if (line == "comments") { target = d.Comments; continue; }
                    if (line == "versions") { target = d.Versions; continue; }
                    try { target.Add(Encoding.UTF8.GetString(Convert.FromBase64String(line))); } catch { }
                }
            }
            catch { }
        }

        string SessionDir { get { return Path.Combine(AppDataDir, "session"); } }

        void SaveSession()
        {
            if (loading) return;
            bool lifted = LiftHighlights();
            try
            {
                SaveEditor();
                EnsureDir(SessionDir);
                foreach (string old in Directory.GetFiles(SessionDir)) try { File.Delete(old); } catch { }
                var manifest = new StringBuilder();
                for (int i = 0; i < docs.Count; i++)
                {
                    DocState d = docs[i];
                    if (d.Path.Length == 0 && !d.Dirty && d.Name == "Untitled") continue;
                    File.WriteAllText(Path.Combine(SessionDir, i + ".rtf"), d.Rtf, Encoding.UTF8);
                    manifest.Append(Esc(d.Name)).Append('|').Append(Esc(d.Path)).Append('|').Append(d.Dirty ? "1" : "0").Append('\n');
                }
                File.WriteAllText(Path.Combine(SessionDir, "manifest.txt"), manifest.ToString(), Encoding.UTF8);
            }
            catch { }
            finally { RestoreHighlights(lifted); }
        }

        static string Esc(string s) { return (s ?? "").Replace("%", "%25").Replace("|", "%7C").Replace("\n", "%0A"); }
        static string Unesc(string s) { return (s ?? "").Replace("%0A", "\n").Replace("%7C", "|").Replace("%25", "%"); }

        void OfferRecovery()
        {
            try
            {
                string manifest = Path.Combine(SessionDir, "manifest.txt");
                if (!File.Exists(manifest)) return;
                string[] lines = File.ReadAllLines(manifest, Encoding.UTF8).Where(l => l.Length > 0).ToArray();
                if (lines.Length == 0) return;
                var answer = MessageBox.Show(this, "Milkpad autosaves every 30 seconds and found " + lines.Length + " document(s) from your last session.\n\nRestore them?", "Restore session", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (answer != DialogResult.Yes) { ClearSession(); return; }
                docs.Clear();
                for (int i = 0; i < lines.Length; i++)
                {
                    string[] parts = lines[i].Split('|');
                    var d = new DocState { Name = Unesc(parts[0]), Path = Unesc(parts[1]), Id = "r" + i };
                    string rtf = Path.Combine(SessionDir, i + ".rtf");
                    if (File.Exists(rtf)) d.Rtf = File.ReadAllText(rtf, Encoding.UTF8);
                    d.Dirty = parts.Length > 2 && parts[2] == "1";
                    LoadMetadata(d);
                    docs.Add(d);
                }
                if (docs.Count == 0) docs.Add(new DocState { Id = "0" });
                SwitchDoc(0); DrawComments();
            }
            catch { }
        }

        void ClearSession() { try { if (Directory.Exists(SessionDir)) Directory.Delete(SessionDir, true); } catch { } }

        void LoadRecent() { try { string p = Path.Combine(AppDataDir, "recent.txt"); if (!File.Exists(p)) return; foreach (string l in File.ReadAllLines(p)) if (l.Trim().Length > 0 && recent.Count < 10) recent.Add(l.Trim()); } catch { } }
        void SaveRecent() { try { EnsureDir(AppDataDir); File.WriteAllLines(Path.Combine(AppDataDir, "recent.txt"), recent.ToArray()); } catch { } }
        void AddRecent(string path) { if (String.IsNullOrWhiteSpace(path)) return; recent.RemoveAll(r => String.Equals(r, path, StringComparison.OrdinalIgnoreCase)); recent.Insert(0, path); while (recent.Count > 10) recent.RemoveAt(recent.Count - 1); SaveRecent(); }
        void OpenPath(string path)
        {
            if (!File.Exists(path)) { MessageBox.Show(this, "File not found:\n" + path, "Open", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            SaveEditor();
            string previousRtf = "";
            try { previousRtf = editor.Rtf; } catch { }
            loading = true;
            try
            {
                string ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".rtf") editor.LoadFile(path, RichTextBoxStreamType.RichText);
                else if (ext == ".docx") editor.Rtf = ReadDocxRtf(path);
                else editor.Text = File.ReadAllText(path, Encoding.UTF8);
                var doc = new DocState { Name = Path.GetFileName(path), Path = path, Id = "f" + (untitledSeq++), Rtf = editor.Rtf, Dirty = false };
                docs.Add(doc); active = docs.Count - 1;
                LoadMetadata(doc); AddRecent(path);
                loading = false; RefreshAll(); DrawComments();
            }
            catch (Exception ex)
            {
                loading = false;
                // A file that fails to parse must not leave an empty extra tab behind; put
                // the document that was open back exactly as it was.
                try { loading = true; if (previousRtf.Length > 0) editor.Rtf = previousRtf; else editor.Clear(); }
                catch { }
                finally { loading = false; }
                MessageBox.Show(this, ex.Message, "Open failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        void DragFilesOver(object sender, DragEventArgs e) { e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; }
        void Dropped(object sender, DragEventArgs e) { string[] files = e.Data.GetData(DataFormats.FileDrop) as string[]; if (files == null) return; foreach (string f in files) OpenPath(f); }

        void FindNext(bool forward) { FindStep(forward ? 1 : -1); }

        void Flash(string text)
        {
            // Keep the message on screen until it expires: a caret move used to overwrite it
            // in the same instant, so most feedback was never readable.
            positionLabel.Text = text; flashUntil = DateTime.Now.AddSeconds(2.5); flashToken++; int token = flashToken;
            var t = new Timer { Interval = 2600 };
            t.Tick += (s, e) => { t.Stop(); t.Dispose(); if (token == flashToken) UpdateStatus(); };
            t.Start();
        }

        void SaveAll()
        {
            int startingTab = active;
            int saved = 0, failed = 0;
            for (int i = 0; i < docs.Count; i++)
            {
                if (!docs[i].Dirty) continue;
                SwitchDoc(i);
                if (SaveDoc()) saved++; else failed++;
            }
            // Saving every tab used to leave you on whichever one was saved last.
            if (startingTab >= 0 && startingTab < docs.Count && startingTab != active) SwitchDoc(startingTab);
            Flash(failed > 0 ? failed + " document(s) could not be saved" : "Saved " + saved + (saved == 1 ? " document" : " documents"));
        }

        void ToggleReadOnly()
        {
            if (Current == null) return;
            Current.ReadOnly = !Current.ReadOnly;
            editor.ReadOnly = Current.ReadOnly;
            Flash(Current.ReadOnly ? "Read-only locked" : "Editing enabled");
            UpdateStatus();
        }

        void RenameDoc()
        {
            if (Current == null) return;
            string name = Prompt("Document name:", "Rename");
            if (name.Trim().Length == 0) return;
            Current.Name = name.Trim();
            RefreshAll();
        }

        void CycleTab(int step) { if (docs.Count < 2) return; SwitchDoc((active + step + docs.Count) % docs.Count); editor.Focus(); }

        void SaveEditor() { if (Current != null) try { Current.Rtf = editor.Rtf; } catch { } }
        void NewDoc() { SaveEditor(); docs.Add(new DocState { Id = "n" + (untitledSeq++) }); SwitchDoc(docs.Count - 1); editor.Focus(); }
        void SwitchDoc(int n) { if (n < 0 || n >= docs.Count) return; bool lifted = LiftHighlights(); rtfTimer.Stop(); SaveEditor(); ForgetMatches(); active = n; loading = true; editor.Clear(); try { if (Current.Rtf.Length > 0) editor.Rtf = Current.Rtf; } catch { editor.Text = ""; } editor.ReadOnly = Current.ReadOnly; loading = false; RefreshAll(); if (lifted) UpdateMatches(); }
        void RefreshAll() { lastLocalText = editor.Text; lastSentRtf = editor.Rtf; DrawTabs(); DrawComments(); UpdateTitle(); UpdateStatus(); }
        void DrawTabs()
        {
            tabs.SuspendLayout(); tabs.Controls.Clear();
            for (int i = 0; i < docs.Count; i++)
            {
                int n = i;
                DocState d = docs[i];
                var b = Button((d.Dirty ? "● " : "") + d.Name + (i == active ? "  ×" : ""), Ui(164), Ui(34));
                b.TextAlign = ContentAlignment.MiddleLeft; b.AutoSize = true; b.AutoSizeMode = AutoSizeMode.GrowAndShrink; b.AutoEllipsis = true;
                b.Padding = new Padding(Ui(10), 0, Ui(10), 0); b.MinimumSize = new Size(Ui(78), Ui(34)); b.MaximumSize = new Size(Ui(164), Ui(34)); b.Margin = new Padding(Ui(1), 0, Ui(1), 0);
                b.BackColor = i == active ? Mix(uiChrome, uiPanel, 0.65) : uiChrome; b.ForeColor = i == active ? uiText : uiMuted;
                b.Click += (s, e) => { if (n == active && b.PointToClient(Cursor.Position).X > b.Width - Ui(28)) CloseDoc(n); else SwitchDoc(n); };
                b.MouseUp += (s, e) =>
                {
                    if (e.Button != MouseButtons.Right) return;
                    if (n != active) SwitchDoc(n);
                    ShowTabMenu(tabs, tabs.PointToClient(Cursor.Position));
                };
                tabs.Controls.Add(b);
            }
            tabs.ResumeLayout();
            LayoutTabs();
        }

        // Cheap in-place refresh of the tab captions. DrawTabs rebuilds every button, which
        // made the strip blink each time an edit marked the document dirty.
        void UpdateTabLabels()
        {
            for (int i = 0; i < docs.Count && i < tabs.Controls.Count; i++)
            {
                Button b = tabs.Controls[i] as Button;
                if (b == null) continue;
                string want = (docs[i].Dirty ? "● " : "") + docs[i].Name + (i == active ? "  ×" : "");
                if (b.Text != want) b.Text = want;
            }
            LayoutTabs();
        }


        int TabsContentWidth() { int w = 0; foreach (Control c in tabs.Controls) w += c.Width + Ui(2); return w; }

        void LayoutTabs()
        {
            int max = Math.Max(0, TabsContentWidth() - tabs.ClientSize.Width);
            if (tabScroll > max) tabScroll = max;
            if (tabScroll < 0) tabScroll = 0;
            int x = -tabScroll;
            foreach (Control c in tabs.Controls) { c.Left = x; c.Top = 0; x += c.Width + Ui(2); }
            bool over = max > 0;
            tabPrev.Visible = over; tabNext.Visible = over;
            tabPrev.Enabled = tabScroll > 0; tabNext.Enabled = tabScroll < max;
        }

        void ScrollTabs(int direction)
        {
            tabScroll += direction * Ui(150);
            LayoutTabs();
        }

        void UpdateToolOverflow()
        {
            if (toolsRow == null) return;
            int content = 0;
            foreach (Control c in toolsRow.Controls) content = Math.Max(content, c.Right);
            bool over = content > toolsHost.ClientSize.Width - 2;
            toolsMore.Bounds = new Rectangle(toolsHost.ClientSize.Width - 26, 0, 26, toolsHost.ClientSize.Height);
            if (toolsMore.Visible != over) { toolsMore.Visible = over; toolsMore.BringToFront(); }
        }

        ContextMenuStrip BuildToolOverflowMenu()
        {
            ContextMenuStrip menu = NewMenu();
            foreach (Control c in toolsRow.Controls)
            {
                if (c == toolsMore || c.Right <= toolsRow.ClientSize.Width) continue;
                string label = tips.GetToolTip(c);
                Button button = c as Button;
                ComboBox combo = c as ComboBox;
                if (String.IsNullOrEmpty(label)) label = c.Text;
                if (String.IsNullOrEmpty(label)) continue;
                var item = new ToolStripMenuItem(label);
                if (button != null) { Button target = button; item.Click += (s, e) => target.PerformClick(); }
                else if (combo != null) { ComboBox target = combo; item.Click += (s, e) => { target.Focus(); target.DroppedDown = true; }; }
                else continue;
                menu.Items.Add(item);
            }
            if (menu.Items.Count == 0) menu.Items.Add(new ToolStripMenuItem("Every tool fits") { Enabled = false });
            return menu;
        }

        void ShowToolOverflow()
        {
            ContextMenuStrip menu = BuildToolOverflowMenu();
            menu.Show(toolsMore, new Point(0, toolsMore.Height));
        }
        void UpdateTitle() { if (Current != null) Text = (Current.Dirty ? "● " : "") + Current.Name + " — Milkpad"; }
        // RichTextBox.GetLineFromCharIndex counts visual rows once word wrap is on, while
        // editor.Lines and the line tools work in logical lines. Everything that needs a
        // line number goes through this map so wrapped text cannot shift the target.
        int LogicalLineIndex(int index)
        {
            if (editor == null) return 0;
            if (lineCacheVersion != textVersion)
            {
                string snapshot = editor.Text;
                lineStarts = new List<int> { 0 };
                for (int i = 0; i < snapshot.Length; i++) if (snapshot[i] == (char)10) lineStarts.Add(i + 1);
                lineCacheVersion = textVersion;
            }
            index = Math.Max(0, Math.Min(index, editor.TextLength));
            int at = lineStarts.BinarySearch(index);
            if (at < 0) at = ~at - 1;
            return Math.Max(0, at);
        }

        void UpdateStatus()
        {
            if (editor == null || bulk) return;
            int p = Math.Max(0, Math.Min(editor.SelectionStart, editor.TextLength));
            int at = LogicalLineIndex(p);
            int line = at + 1;
            int column = p - lineStarts[at] + 1;
            if (column < 1) column = 1;
            if (DateTime.Now >= flashUntil)
                positionLabel.Text = "Ln " + line + ", Col " + column + (editor.SelectionLength > 0 ? " (+" + editor.SelectionLength + ")" : "");
            charactersLabel.Text = editor.TextLength.ToString("N0", CultureInfo.InvariantCulture) + " characters";
            formatLabel.Text = "Rich text";
            if (Current != null)
            {
                Color neutral = uiMuted;
                positionLabel.ForeColor = neutral; charactersLabel.ForeColor = neutral; formatLabel.ForeColor = neutral;
                modeLabel.Text = Current.ReadOnly ? "READ-ONLY" : "";
                modeLabel.ForeColor = Current.ReadOnly ? Color.FromArgb(198, 86, 52) : neutral;
            }
            emptyHint.Visible = editor.TextLength == 0 && Current != null;
            PlaceHint();
        }

        void LayoutSheet()
        {
            if (contentHost == null || sheet == null || sheet.Parent == null) return;
            sheet.SetBounds(0, 0, contentHost.ClientSize.Width, contentHost.ClientSize.Height);
            contentHost.Invalidate();
        }

        void PlaceHint()
        {
            if (emptyHint.Parent == null || editor == null || contentHost == null) return;
            Rectangle r = editor.Bounds;
            if (r.Width < 20 || r.Height < 20) r = new Rectangle(0, 0, sheet.ClientSize.Width, sheet.ClientSize.Height);
            var target = new Rectangle(r.Left, r.Top + Math.Max(24, r.Height / 2 - 16), Math.Max(0, r.Width), 32);
            if (emptyHint.Bounds != target) emptyHint.SetBounds(target.X, target.Y, target.Width, target.Height);
            if (emptyHint.Visible && emptyHint.Parent != null && emptyHint.Parent.Controls.GetChildIndex(emptyHint) != 0) emptyHint.BringToFront();
        }

        void ToggleStyle(FontStyle style) { editor.Focus(); Font f = editor.SelectionFont ?? editor.Font; editor.SelectionFont = new Font(f, (f.Style & style) == style ? f.Style & ~style : f.Style | style); SignalFormat(); }
        void SetFontFamily(string name)
        {
            if (String.IsNullOrWhiteSpace(name)) return;
            try
            {
                Font current = editor.SelectionFont ?? editor.Font;
                FontFamily family = null;
                foreach (FontFamily candidate in privateFonts.Families)
                    if (string.Equals(candidate.Name, name, StringComparison.CurrentCultureIgnoreCase)) { family = candidate; break; }
                editor.SelectionFont = family != null ? new Font(family, current.Size, current.Style) : new Font(name, current.Size, current.Style);
            }
            catch { }
            editor.Focus(); SignalFormat();
        }

        [System.Runtime.InteropServices.DllImport("gdi32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        static extern int AddFontResourceEx(string file, uint flags, IntPtr reserved);

        void RegisterFontForEditor(string file)
        {
            // RichEdit draws through GDI, so a GDI+ private font is not enough: the file has
            // to be registered with GDI for the family to render while the app is running.
            try { AddFontResourceEx(file, 0x10, IntPtr.Zero); } catch { }
        }

        void LoadImportedFonts()
        {
            try
            {
                string list = Path.Combine(AppDataDir, "fonts.txt");
                if (!File.Exists(list)) return;
                foreach (string line in File.ReadAllLines(list))
                {
                    string file = line.Trim();
                    if (file.Length == 0 || !File.Exists(file) || importedFonts.ContainsKey(file)) continue;
                    try
                    {
                        privateFonts.AddFontFile(file);
                        var families = privateFonts.Families;
                        if (families.Length == 0) continue;
                        importedFonts[file] = families[families.Length - 1].Name;
                        RegisterFontForEditor(file);
                    }
                    catch { }
                }
            }
            catch { }
        }

        void SaveImportedFonts()
        {
            try { EnsureDir(AppDataDir); File.WriteAllLines(Path.Combine(AppDataDir, "fonts.txt"), importedFonts.Keys.ToArray()); } catch { }
        }

        void ImportFont()
        {
            using (var dialog = new OpenFileDialog { Filter = "Font files|*.ttf;*.otf|All files|*.*", Title = "Import font" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                string file = dialog.FileName;
                string family;
                if (!importedFonts.TryGetValue(file, out family))
                {
                    try
                    {
                        privateFonts.AddFontFile(file);
                        var families = privateFonts.Families;
                        if (families.Length == 0) throw new InvalidOperationException("The file contains no usable font.");
                        family = families[families.Length - 1].Name;
                        importedFonts[file] = family;
                        RegisterFontForEditor(file);
                        SaveImportedFonts();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, "That font could not be loaded.\n\n" + ex.Message, "Import font", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
                RefreshFontChoices(family);
                SetFontFamily(family);
                Flash("Font added: " + family);
                editor.Focus();
            }
        }

        SortedSet<string> AvailableFontNames()
        {
            var names = new SortedSet<string>(StringComparer.CurrentCultureIgnoreCase);
            foreach (FontFamily family in FontFamily.Families) names.Add(family.Name);
            foreach (FontFamily family in privateFonts.Families) names.Add(family.Name);
            return names;
        }

        void AddFontNames(FlatCombo combo)
        {
            foreach (string name in AvailableFontNames()) combo.Items.Add(name);
        }

        void FilterFontChoices(string query)
        {
            if (fontChooser == null) return;
            string filter = (query ?? "").Trim();
            refreshingFonts = true;
            fontChooser.BeginUpdate();
            fontChooser.Items.Clear();
            foreach (string name in AvailableFontNames())
                if (filter.Length == 0 || name.IndexOf(filter, StringComparison.CurrentCultureIgnoreCase) >= 0)
                    fontChooser.Items.Add(name);
            fontChooser.EndUpdate();
            fontChooser.SelectedIndex = -1;
            fontChooser.Text = query ?? "";
            refreshingFonts = false;
            if (fontChooser.Focused && !fontChooser.DroppedDown && fontChooser.Items.Count > 0) fontChooser.DroppedDown = true;
        }

        void ApplyFontSearch()
        {
            if (fontChooser == null) return;
            string name = (fontChooser.Text ?? "").Trim();
            if (name.Length == 0) return;
            SetFontFamily(name);
            RefreshFontChoices(name);
            fontChooser.DroppedDown = false;
            editor.Focus();
        }

        void RefreshFontChoices(string select)
        {
            if (fontChooser == null) return;
            refreshingFonts = true;
            fontChooser.BeginUpdate();
            fontChooser.Items.Clear();
            AddFontNames(fontChooser);
            fontChooser.EndUpdate();
            int index = select == null ? -1 : fontChooser.Items.IndexOf(select);
            if (index < 0) index = Math.Max(0, fontChooser.Items.IndexOf("Consolas"));
            fontChooser.SelectedIndex = index;
            refreshingFonts = false;
        }
        void SetFontSize(string value) { float size; if (Single.TryParse(value, out size) && size >= 5 && size <= 200) { Font f = editor.SelectionFont ?? editor.Font; editor.SelectionFont = new Font(f.FontFamily, size, f.Style); } editor.Focus(); SignalFormat(); }
        void ApplyStyle(string name)
        {
            float size = name == "Title" ? 26 : name == "Subtitle" ? 16 : name == "Heading 1" ? 20 : name == "Heading 2" ? 17 : name == "Heading 3" ? 14 : 11;
            FontStyle style = name == "Normal text" || name == "Subtitle" ? FontStyle.Regular : FontStyle.Bold; Font old = editor.SelectionFont ?? editor.Font; editor.SelectionFont = new Font(old.FontFamily, size, style); SignalFormat();
        }
        void ChooseColor(bool highlight) { using (var d = new ColorDialog { FullOpen = true, Color = highlight ? Color.Yellow : editor.SelectionColor }) if (d.ShowDialog(this) == DialogResult.OK) { if (highlight) editor.SelectionBackColor = d.Color; else editor.SelectionColor = d.Color; } editor.Focus(); SignalFormat(); }
        void Align(HorizontalAlignment a) { editor.SelectionAlignment = a; editor.Focus(); SignalFormat(); }
        void ToggleBullets() { editor.SelectionBullet = !editor.SelectionBullet; editor.Focus(); SignalFormat(); }
        void ToggleNumbers()
        {
            editor.Focus();
            int first = LogicalLineIndex(editor.SelectionStart);
            int last = editor.SelectionLength > 0 ? LogicalLineIndex(editor.SelectionStart + editor.SelectionLength - 1) : first;
            if (last < first) last = first;
            bool numbered = false;
            for (int i = first; i <= last && i < editor.Lines.Length; i++)
                if (System.Text.RegularExpressions.Regex.IsMatch(editor.Lines[i], @"^\s*\d+[\.\)]\s")) { numbered = true; break; }
            int n = 1;
            for (int i = first; i <= last && i < editor.Lines.Length; i++)
            {
                int lineStart = editor.GetFirstCharIndexFromLine(i);
                if (lineStart < 0) continue;
                string line = editor.Lines[i];
                System.Text.RegularExpressions.Match m = System.Text.RegularExpressions.Regex.Match(line, @"^(\s*)\d+[\.\)]\s");
                int strip = m.Success ? m.Length : 0;
                string insert = numbered ? (m.Success ? m.Groups[1].Value : "") : (line.Trim().Length == 0 ? "" : (n++) + ". ");
                editor.Select(lineStart + strip, 0);
                editor.SelectedText = insert;
            }
            editor.Focus();
        }
        void InsertLink() { string u = Prompt("Link URL:", "Insert link"); if (u.Length > 0) { string label = editor.SelectedText.Length == 0 ? u : editor.SelectedText; editor.SelectedText = label; editor.Select(editor.SelectionStart - label.Length, label.Length); editor.SelectionColor = Color.RoyalBlue; editor.SelectionFont = new Font(editor.SelectionFont ?? editor.Font, FontStyle.Underline); editor.SelectedText = label; editor.Focus(); } }
        void InsertTable()
        {
            string answer = Prompt("Table size (rows,columns):", "Insert table"); if (answer.Length == 0) return;
            int rows = 3, cols = 3; string[] sizes = answer.Split(','); int.TryParse(sizes[0], out rows); if (sizes.Length > 1) int.TryParse(sizes[1], out cols);
            rows = Math.Max(1, Math.Min(20, rows)); cols = Math.Max(1, Math.Min(10, cols));
            int cellWidth = 9360 / cols; var rtf = new StringBuilder(@"{\rtf1\ansi\deff0{\fonttbl{\f0 Arial;}}\f0\fs22 ");
            for (int r = 0; r < rows; r++) { rtf.Append(@"\trowd\trgaph80\trleft0"); for (int c = 0; c < cols; c++) rtf.Append(@"\clbrdrt\brdrs\clbrdrl\brdrs\clbrdrb\brdrs\clbrdrr\brdrs\cellx").Append(cellWidth * (c + 1)); for (int c = 0; c < cols; c++) rtf.Append(@"\intbl Cell\cell "); rtf.Append(@"\row "); }
            rtf.Append("}"); editor.SelectedRtf = rtf.ToString(); editor.Focus();
        }
        void InsertImage()
        {
            using (var dialog = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files|*.*", Title = "Insert image" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    using (var source = Image.FromFile(dialog.FileName))
                    {
                        // RichEdit here accepts a DIB picture group but silently drops PNG blips,
                        // so the image is converted to 24-bit pixels instead of embedded as PNG.
                        double displayScale = Math.Min(1.0, Math.Min(6240.0 / Math.Max(1, source.Width * 15.0), 8000.0 / Math.Max(1, source.Height * 15.0)));
                        int goalWidth = Math.Max(1, (int)(source.Width * 15 * displayScale));
                        int goalHeight = Math.Max(1, (int)(source.Height * 15 * displayScale));
                        double pixelScale = Math.Min(1.0, Math.Min(800.0 / Math.Max(1, source.Width), 1200.0 / Math.Max(1, source.Height)));
                        int pixelWidth = Math.Max(1, (int)(source.Width * pixelScale));
                        int pixelHeight = Math.Max(1, (int)(source.Height * pixelScale));
                        using (var bitmap = new Bitmap(pixelWidth, pixelHeight, System.Drawing.Imaging.PixelFormat.Format24bppRgb))
                        {
                            using (var graphics = Graphics.FromImage(bitmap))
                            {
                                graphics.Clear(Color.White);
                                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                                graphics.DrawImage(source, 0, 0, pixelWidth, pixelHeight);
                            }
                            string picture = @"{\pict\dibitmap0\picw" + pixelWidth + @"\pich" + pixelHeight + @"\picwgoal" + goalWidth + @"\pichgoal" + goalHeight + " " + DibHex(bitmap) + "}";
                            int at = editor.SelectionStart;
                            editor.Select(at, 0);
                            editor.SelectedRtf = @"{\rtf1\ansi\deff0{\fonttbl{\f0 Arial;}}\viewkind4\uc1\pard\f0 " + picture + "}";
                            editor.Select(Math.Min(at + 1, editor.TextLength), 0);
                            editor.Focus();
                        }
                    }
                }
                catch (Exception ex) { MessageBox.Show(this, "Could not insert that image.\n\n" + ex.Message, "Image insert failed", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }

        // A device-independent bitmap for an RTF \dibitmap0 picture: 40-byte header, 24-bit
        // BGR rows bottom-up, each row padded to four bytes.
        static string DibHex(Bitmap bitmap)
        {
            int width = bitmap.Width, height = bitmap.Height;
            int stride = ((width * 24 + 31) / 32) * 4;
            int size = stride * height;
            byte[] bytes = new byte[40 + size];
            using (var memory = new MemoryStream(bytes))
            using (var writer = new BinaryWriter(memory))
            {
                writer.Write(40); writer.Write(width); writer.Write(height); writer.Write((short)1); writer.Write((short)24);
                writer.Write(0); writer.Write(size); writer.Write(2835); writer.Write(2835); writer.Write(0); writer.Write(0);
            }
            var data = bitmap.LockBits(new Rectangle(0, 0, width, height), System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            try
            {
                byte[] row = new byte[stride];
                for (int y = 0; y < height; y++)
                {
                    Array.Clear(row, 0, row.Length);
                    System.Runtime.InteropServices.Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, width * 3);
                    Buffer.BlockCopy(row, 0, bytes, 40 + (height - 1 - y) * stride, stride);
                }
            }
            finally { bitmap.UnlockBits(data); }
            return BitConverter.ToString(bytes).Replace("-", "");
        }
        void InsertHeaderFooter(bool header)
        {
            string value = Prompt(header ? "Header text:" : "Footer text:", header ? "Insert header" : "Insert footer"); if (value.Length == 0) return;
            int position = header ? 0 : editor.TextLength; editor.Select(position, 0); editor.SelectionAlignment = HorizontalAlignment.Center; editor.SelectionFont = new Font(editor.Font.FontFamily, 9F, FontStyle.Italic); editor.SelectionColor = Color.DimGray; editor.SelectedText = value + "\r\n"; editor.SelectionAlignment = HorizontalAlignment.Left; editor.SelectionFont = editor.Font; editor.SelectionColor = editor.ForeColor; editor.Focus();
        }
        void InsertPageBreak() { editor.SelectedText = "\r\n────────────── Page break ──────────────\r\n"; }
        //==== emoji ====
        // The picker stores code points rather than literal glyphs so this source file stays
        // plain ASCII while still covering characters outside the basic plane.
        static readonly string[] EmojiGroups = { "Smileys", "Gestures", "Nature", "Food", "Activity", "Travel", "Objects", "Symbols" };
        static readonly int[][] EmojiCodes = new int[][] {
            new int[] { 0x1F600, 0x1F603, 0x1F604, 0x1F601, 0x1F606, 0x1F605, 0x1F923, 0x1F602, 0x1F642, 0x1F643, 0x1F609, 0x1F60A, 0x1F60D, 0x1F618, 0x1F617, 0x1F619, 0x1F61A, 0x1F60B, 0x1F61B, 0x1F92A, 0x1F928, 0x1F9D0, 0x1F610, 0x1F611, 0x1F636, 0x1F60F, 0x1F612, 0x1F644, 0x1F62C, 0x1F620, 0x1F621, 0x1F62E, 0x1F62F, 0x1F632, 0x1F631, 0x1F622, 0x1F62D, 0x1F624, 0x1F92C, 0x1F634, 0x1F914, 0x1F92B, 0x1F910, 0x1F971, 0x1F92F },
            new int[] { 0x1F44B, 0x1F91A, 0x1F590, 0x270B, 0x1F596, 0x1F44C, 0x1F919, 0x1F448, 0x1F449, 0x1F446, 0x1F447, 0x261D, 0x1F44D, 0x1F44E, 0x270A, 0x1F44A, 0x1F91B, 0x1F91C, 0x1F44F, 0x1F64C, 0x1F450, 0x1F932, 0x1F91D, 0x1F64F, 0x1F485 },
            new int[] { 0x1F436, 0x1F431, 0x1F42D, 0x1F439, 0x1F430, 0x1F98A, 0x1F43B, 0x1F43C, 0x1F428, 0x1F42F, 0x1F981, 0x1F42E, 0x1F437, 0x1F438, 0x1F414, 0x1F427, 0x1F426, 0x1F424, 0x1F986, 0x1F985, 0x1F989, 0x1F987, 0x1F43A, 0x1F417, 0x1F434, 0x1F984, 0x1F41D, 0x1F41B, 0x1F98B, 0x1F33B, 0x1F337, 0x1F335, 0x1F332, 0x1F334, 0x1F340, 0x1F343, 0x1F308, 0x2B50, 0x1F319, 0x2600 },
            new int[] { 0x1F34E, 0x1F34C, 0x1F347, 0x1F349, 0x1F353, 0x1F351, 0x1F352, 0x1F350, 0x1F34A, 0x1F34B, 0x1F34D, 0x1F965, 0x1F355, 0x1F35D, 0x1F354, 0x1F35F, 0x1F32D, 0x1F96A, 0x1F363, 0x1F35C, 0x1F372, 0x1F958, 0x1F35A, 0x1F35B, 0x1F366, 0x1F369, 0x1F36A, 0x1F382, 0x1F37F, 0x2615, 0x1F375, 0x1F37A },
            new int[] { 0x26BD, 0x1F3C0, 0x1F3C8, 0x26BE, 0x1F3BE, 0x1F3D0, 0x1F94A, 0x1F3B1, 0x1F3B3, 0x1F3AF, 0x1F3AE, 0x1F3B2, 0x1F3C6, 0x1F947, 0x1F3B5, 0x1F3B8, 0x1F3B9, 0x1F3BA, 0x1F3BB, 0x1F941, 0x1F3AC, 0x1F3AD, 0x1F3A8, 0x1F3AA },
            new int[] { 0x1F697, 0x1F695, 0x1F699, 0x1F68C, 0x1F69A, 0x1F693, 0x1F692, 0x1F691, 0x1F6B2, 0x1F6F5, 0x2708, 0x1F680, 0x1F681, 0x1F6A2, 0x26F5, 0x1F682, 0x1F686, 0x1F687, 0x1F68B, 0x1F3E0, 0x1F3E2, 0x1F3EB, 0x1F3ED, 0x26F0, 0x1F30B, 0x1F3D6, 0x1F5FD, 0x1F5FC },
            new int[] { 0x1F4A1, 0x1F526, 0x1F4BB, 0x1F4F1, 0x2328, 0x1F5A5, 0x1F4BE, 0x1F4BF, 0x1F4F7, 0x1F4F8, 0x1F4FA, 0x1F4FB, 0x23F0, 0x231A, 0x1F4DA, 0x1F4D6, 0x270F, 0x1F4CE, 0x1F4CB, 0x1F4C5, 0x1F4C8, 0x1F4E7, 0x1F512, 0x1F511, 0x1F528, 0x1F527, 0x1F4A3, 0x1F381 },
            new int[] { 0x2764, 0x1F494, 0x1F495, 0x1F49A, 0x1F496, 0x1F49D, 0x1F49E, 0x1F498, 0x1F31F, 0x2728, 0x1F4A5, 0x1F525, 0x1F4AF, 0x2705, 0x274C, 0x2757, 0x2753, 0x203C, 0x2049, 0x1F4A4, 0x1F4CC, 0x1F4CD, 0x2192, 0x2190, 0x2191, 0x2193, 0x2B06, 0x2B07 }
        };

        static bool EmojiFontReady()
        {
            try { using (var family = new FontFamily("Segoe UI Emoji")) return true; }
            catch { return false; }
        }

        Font EmojiFace(float size)
        {
            float points = Math.Max(9f, size);
            return EmojiFontReady() ? new Font("Segoe UI Emoji", points) : new Font(editorFontName, points);
        }

        // The glyph is written as its own run in an emoji font so a plain-text document in
        // Consolas still shows it; the caret then returns to the document font.
        void InsertEmoji(int code)
        {
            if (Current == null) return;
            if (Current.ReadOnly) { Flash("This document is read-only"); return; }
            string glyph = Char.ConvertFromUtf32(code);
            Font before = editor.SelectionFont ?? editor.Font;
            int at = editor.SelectionStart;
            editor.SelectedText = glyph;
            editor.Select(at, glyph.Length);
            editor.SelectionFont = EmojiFace(before.Size);
            editor.Select(Math.Min(editor.TextLength, at + glyph.Length), 0);
            editor.SelectionFont = before;
            editor.Focus();
            SignalFormat();
        }

        void ShowEmojiPicker()
        {
            if (Current == null) return;
            if (Current.ReadOnly) { Flash("This document is read-only"); return; }
            using (var f = new Form
            {
                Text = "Emoji",
                ClientSize = new Size(452, 356),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                ShowInTaskbar = false,
                BackColor = uiPanel,
                ForeColor = uiText,
                Font = UiFont(9F)
            })
            {
                Color field = Mix(uiPanel, uiChrome, 0.35);
                var group = new ComboBox { Left = 14, Top = 16, Width = 180, DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = field, ForeColor = uiText };
                foreach (string name in EmojiGroups) group.Items.Add(name);
                group.SelectedIndex = 0;
                var note = new Label { Text = "Click a character to insert it at the cursor.", Left = 204, Top = 20, Width = 234, Height = 18, ForeColor = uiMuted, TextAlign = ContentAlignment.MiddleRight };
                var grid = new FlowLayoutPanel { Left = 14, Top = 52, Width = 424, Height = 252, AutoScroll = true, BackColor = field, Padding = new Padding(6) };
                Action fill = () =>
                {
                    grid.Controls.Clear();
                    int[] codes = EmojiCodes[Math.Max(0, Math.Min(EmojiCodes.Length - 1, group.SelectedIndex))];
                    foreach (int code in codes)
                    {
                        int picked = code;
                        var cell = new Button { Text = Char.ConvertFromUtf32(code), Width = 40, Height = 40, Margin = new Padding(2), FlatStyle = FlatStyle.Flat, BackColor = uiPanel, ForeColor = uiText, Font = EmojiFace(15F), Cursor = Cursors.Hand, TabStop = false };
                        cell.FlatAppearance.BorderSize = 1;
                        cell.FlatAppearance.BorderColor = uiBorder;
                        cell.FlatAppearance.MouseOverBackColor = Mix(uiPanel, uiAccent, 0.25);
                        cell.Click += (s, e) => InsertEmoji(picked);
                        grid.Controls.Add(cell);
                    }
                };
                group.SelectedIndexChanged += (s, e) => fill();
                fill();
                var close = new Button { Text = "Close", Width = 90, Height = 28, Left = 348, Top = 316, DialogResult = DialogResult.Cancel, FlatStyle = FlatStyle.Flat, BackColor = field, ForeColor = uiText, Cursor = Cursors.Hand };
                close.FlatAppearance.BorderColor = uiBorder;
                f.Controls.Add(group); f.Controls.Add(note); f.Controls.Add(grid); f.Controls.Add(close);
                f.CancelButton = close;
                f.ShowDialog(this);
                editor.Focus();
            }
        }

        void ClearFormatting() { editor.SelectionFont = editor.Font; editor.SelectionColor = editor.ForeColor; editor.SelectionBackColor = editor.BackColor; editor.SelectionBullet = false; editor.SelectionIndent = 0; editor.SelectionHangingIndent = 0; editor.SelectionAlignment = HorizontalAlignment.Left; SignalFormat(); }

        void AddComment() { if (Current == null) return; string quote = editor.SelectedText; string note = Prompt(quote.Length == 0 ? "Comment:" : "Comment on: “" + quote + "”", "New comment"); if (note.Length == 0) return; Current.Comments.Add((quote.Length == 0 ? "Document" : quote) + "\n" + note); commentPane.Visible = true; DrawComments(); SaveMetadata(Current); }
        void DrawComments() { commentList.Controls.Clear(); if (Current == null) return; foreach (string c in Current.Comments) { var card = new Panel { Width = 232, Height = 88, BackColor = dark ? Color.FromArgb(56, 50, 57) : Color.FromArgb(246, 241, 248), Margin = new Padding(8) }; card.Controls.Add(new Label { Text = c, Dock = DockStyle.Fill, Padding = new Padding(8), ForeColor = dark ? DarkText : Color.Black, AutoEllipsis = true }); commentList.Controls.Add(card); } }
        void FindReplace(bool replace) { ShowFindBar(replace); }

        //==== find and replace bar ====
        void ShowFindBar(bool replace)
        {
            if (!findBar.Visible) { findBar.Visible = true; findRowStyle.Height = 38; }
            string selection = editor.SelectedText;
            if (selection.Length > 0 && selection.Length < 100 && selection.IndexOf('\n') < 0 && selection.IndexOf('\r') < 0) findBox.Text = selection;
            UpdateMatches();
            if (replace) { replaceBox.Focus(); replaceBox.SelectAll(); }
            else { findBox.Focus(); findBox.SelectAll(); }
        }

        void HideFindBar()
        {
            ClearHighlights();
            ForgetMatches();
            findBar.Visible = false; findRowStyle.Height = 0; findStatus.Text = "";
            editor.Focus();
        }

        void ForgetMatches() { matchBacks.Clear(); matchStarts.Clear(); matchIndex = -1; }

        void UpdateMatches()
        {
            ClearHighlights();
            matchStarts.Clear(); matchIndex = -1;
            string term = findBox.Text;
            if (term.Length == 0) { findStatus.Text = ""; return; }
            int at = 0;
            while (true)
            {
                at = editor.Find(term, at, RichTextBoxFinds.None);
                if (at < 0) break;
                matchStarts.Add(at);
                at += term.Length;
                if (at >= editor.TextLength) break;
            }
            highlightLength = term.Length;
            ApplyHighlights();
            if (matchStarts.Count > 0) matchIndex = 0;
            findStatus.Text = matchStarts.Count == 0 ? "No matches" : matchStarts.Count + (matchStarts.Count == 1 ? " match" : " matches");
        }

        void ApplyHighlights()
        {
            if (matchStarts.Count == 0) return;
            int savedStart = editor.SelectionStart, savedLength = editor.SelectionLength;
            highlighting = true;
            try
            {
                for (int i = 0; i < matchStarts.Count; i++)
                {
                    editor.Select(matchStarts[i], highlightLength);
                    matchBacks.Add(editor.SelectionBackColor);
                    editor.SelectionBackColor = MatchColor;
                }
            }
            finally { RestoreSelection(savedStart, savedLength); highlighting = false; }
        }

        // Find matches are painted with real character formatting, so the marks have to be
        // lifted before the document is serialised; otherwise a save bakes yellow into the
        // file. They are put back afterwards so the search stays visible.
        bool LiftHighlights()
        {
            if (matchBacks.Count == 0) return false;
            ClearHighlights();
            return true;
        }

        void RestoreHighlights(bool lifted)
        {
            if (lifted) UpdateMatches();
        }

        void ClearHighlights()
        {
            if (matchBacks.Count == 0) return;
            int savedStart = editor.SelectionStart, savedLength = editor.SelectionLength;
            highlighting = true;
            try
            {
                for (int i = 0; i < matchBacks.Count && i < matchStarts.Count; i++)
                {
                    if (matchStarts[i] + highlightLength > editor.TextLength) continue;
                    editor.Select(matchStarts[i], highlightLength);
                    editor.SelectionBackColor = matchBacks[i];
                }
            }
            finally { RestoreSelection(savedStart, savedLength); highlighting = false; }
            matchBacks.Clear();
        }

        void RestoreSelection(int start, int length)
        {
            int safeStart = Math.Min(start, editor.TextLength);
            int safeLength = Math.Min(length, editor.TextLength - safeStart);
            editor.Select(safeStart, Math.Max(0, safeLength));
        }

        void SelectMatch(int i)
        {
            if (i < 0 || i >= matchStarts.Count) return;
            int length = findBox.Text.Length;
            if (matchStarts[i] + length > editor.TextLength) return;
            editor.Select(matchStarts[i], length);
            editor.ScrollToCaret();
            findStatus.Text = (i + 1) + " of " + matchStarts.Count;
        }

        void FindStep(int direction)
        {
            if (!findBar.Visible) { findBar.Visible = true; findRowStyle.Height = 38; }
            if (findBox.Text.Length == 0) { findBox.Focus(); return; }
            if (matchStarts.Count == 0) UpdateMatches();
            if (matchStarts.Count == 0) { findStatus.Text = "No matches"; return; }
            if (matchIndex < 0) matchIndex = 0;
            else matchIndex = ((matchIndex + direction) % matchStarts.Count + matchStarts.Count) % matchStarts.Count;
            SelectMatch(matchIndex);
        }

        void ReplaceCurrent()
        {
            string term = findBox.Text;
            if (term.Length == 0) return;
            if (matchStarts.Count == 0) UpdateMatches();
            if (matchStarts.Count == 0) { findStatus.Text = "No matches"; return; }
            int index = matchIndex < 0 ? 0 : Math.Min(matchIndex, matchStarts.Count - 1);
            int start = matchStarts[index];
            if (start + term.Length > editor.TextLength) return;
            ClearHighlights();
            editor.Select(start, term.Length);
            editor.SelectedText = replaceBox.Text;
            UpdateMatches();
            if (matchStarts.Count > 0) { matchIndex = Math.Min(index, matchStarts.Count - 1); SelectMatch(matchIndex); }
            else findStatus.Text = "No matches";
            findBox.Focus();
        }

        void ReplaceAllMatches()
        {
            string term = findBox.Text;
            if (term.Length == 0) return;
            if (matchStarts.Count == 0) UpdateMatches();
            if (matchStarts.Count == 0) { findStatus.Text = "No matches"; return; }
            int count = matchStarts.Count;
            ClearHighlights();
            for (int i = matchStarts.Count - 1; i >= 0; i--)
            {
                if (matchStarts[i] + term.Length > editor.TextLength) continue;
                editor.Select(matchStarts[i], term.Length);
                editor.SelectedText = replaceBox.Text;
            }
            matchBacks.Clear(); matchStarts.Clear(); matchIndex = -1;
            UpdateMatches();
            findStatus.Text = "Replaced " + count;
            findBox.Focus();
        }
        void ShowWordCount()
        {
            string text = editor.Text;
            int words = text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Length;
            int paragraphs = text.Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries).Length;
            int minutes = (int)Math.Max(1.0, Math.Round(words / 200.0));
            var message = new StringBuilder();
            message.Append("Words: ").Append(words).Append('\n');
            message.Append("Characters: ").Append(editor.TextLength).Append('\n');
            message.Append("Characters without spaces: ").Append(text.Count(c => !Char.IsWhiteSpace(c))).Append('\n');
            message.Append("Paragraphs: ").Append(paragraphs).Append('\n');
            message.Append("Reading time: ").Append(minutes).Append(minutes == 1 ? " minute" : " minutes");
            if (editor.SelectionLength > 0)
                message.Append("\n\nSelection: ").Append(editor.SelectedText.Split((char[])null, StringSplitOptions.RemoveEmptyEntries).Length).Append(" words, ").Append(editor.SelectionLength).Append(" characters");
            MessageBox.Show(this, message.ToString(), "Document statistics", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        void ToggleTheme()
        {
            ApplyPalette(dark ? Palettes[1] : Palettes[0]);
            ApplyTheme(); SaveSettings();
        }

        void ApplyEditorFont()
        {
            if (editor == null) return;
            try
            {
                FontFamily family = null;
                foreach (FontFamily candidate in privateFonts.Families)
                    if (string.Equals(candidate.Name, editorFontName, StringComparison.CurrentCultureIgnoreCase)) { family = candidate; break; }
                if (family == null) family = new FontFamily(editorFontName);
                editor.Font = new Font(family, Math.Max(5, Math.Min(200, editorFontSize)));
            }
            catch
            {
                editorFontName = "Consolas";
                try { editor.Font = new Font("Consolas", 11F); } catch { }
            }
        }

        FontFamily FindUiFamily()
        {
            foreach (FontFamily candidate in privateFonts.Families)
                if (string.Equals(candidate.Name, uiFontName, StringComparison.CurrentCultureIgnoreCase)) return candidate;
            try { return new FontFamily(uiFontName); }
            catch { return new FontFamily("Segoe UI"); }
        }

        Font UiFont(float size, FontStyle style = FontStyle.Regular)
        {
            try { return new Font(FindUiFamily(), size, style); }
            catch { return new Font("Segoe UI", size, style); }
        }

        void ApplyUiFont()
        {
            if (editor == null) return;
            FontFamily family = FindUiFamily();
            try { Font = new Font(family, Font.Size, Font.Style, Font.Unit); } catch { }
            ApplyUiFont(this, family);
        }

        void ApplyUiFont(Control parent, FontFamily family)
        {
            foreach (Control child in parent.Controls)
            {
                if (child == editor || child == emptyHint || child == caretBar || child == selectionOutline) continue;
                try
                {
                    Font current = child.Font;
                    if (current != null) child.Font = new Font(family, current.Size, current.Style, current.Unit);
                }
                catch { }
                ApplyUiFont(child, family);
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern bool HideCaret(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern bool ShowCaret(IntPtr hWnd);

        void UpdateOverlays()
        {
            UpdateCaretOverlay();
            UpdateSelectionOutline();
        }

        void UpdateCaretOverlay()
        {
            if (caretBar == null) return;
            if (caretColor == Color.Empty || editor == null || !editor.Focused || editor.SelectionLength > 0 || emptyHint.Visible)
            {
                caretBar.Visible = false;
                if (caretColor == Color.Empty && editor != null && editor.Focused) { try { ShowCaret(editor.Handle); } catch { } }
                return;
            }
            try
            {
                Point at = editor.GetPositionFromCharIndex(editor.SelectionStart);
                if (at.X < 0 || at.Y < 0 || at.Y > editor.ClientSize.Height) { caretBar.Visible = false; return; }
                int height = Math.Max(8, (int)Math.Round(editor.Font.Height * editor.ZoomFactor));
                caretBar.Bounds = new Rectangle(editor.Left + at.X, editor.Top + at.Y, 2, height);
                caretBar.BackColor = caretColor;
                caretBar.Visible = true;
                caretBar.BringToFront();
                HideCaret(editor.Handle);
            }
            catch { caretBar.Visible = false; }
        }

        void UpdateSelectionOutline()
        {
            if (selectionOutline == null) return;
            if (selectionColor == Color.Empty || editor == null || !editor.Focused || editor.SelectionLength == 0 || emptyHint.Visible)
            {
                selectionOutline.Visible = false;
                return;
            }
            try
            {
                int start = editor.SelectionStart;
                int end = start + editor.SelectionLength - 1;
                Point a = editor.GetPositionFromCharIndex(start);
                Point b = editor.GetPositionFromCharIndex(end);
                int left = Math.Min(a.X, b.X) + editor.Left;
                int top = Math.Min(a.Y, b.Y) + editor.Top;
                int right = Math.Max(a.X, b.X) + editor.Left;
                int bottom = Math.Max(a.Y, b.Y) + editor.Top;
                int height = Math.Max(8, (int)Math.Round(editor.Font.Height * editor.ZoomFactor));
                int width = Math.Max(4, right - left + 2);
                int totalHeight = Math.Max(4, bottom - top + height);
                selectionOutline.Bounds = new Rectangle(left, top, width, totalHeight);
                var region = new Region(new Rectangle(0, 0, width, totalHeight));
                int edge = 2;
                region.Exclude(new Rectangle(edge, edge, Math.Max(0, width - edge * 2), Math.Max(0, totalHeight - edge * 2)));
                Region oldRegion = selectionOutline.Region;
                selectionOutline.Region = region;
                if (oldRegion != null) oldRegion.Dispose();
                selectionOutline.BackColor = selectionColor;
                selectionOutline.Visible = true;
                selectionOutline.BringToFront();
            }
            catch { selectionOutline.Visible = false; }
        }

        UiPalette SegaGenesisFallback()
        {
            return new UiPalette
            {
                Name = "Sega Genesis", IsDark = true,
                Chrome = ColorTranslator.FromHtml("#05070A"), Panel = ColorTranslator.FromHtml("#101820"),
                Page = Color.Black, Text = ColorTranslator.FromHtml("#F2F6FA"), Muted = ColorTranslator.FromHtml("#8A9BAA"),
                Border = ColorTranslator.FromHtml("#1E5FA8"), Accent = ColorTranslator.FromHtml("#1E90FF"),
                EditorBack = Color.Black, EditorText = ColorTranslator.FromHtml("#F5F5F5"),
                FontName = "Consolas", FontSize = 11, TabWidth = 4, SoftTabs = true,
                Wrap = true, AutoIndent = true, ShowStatus = true
            };
        }

        string SegaGenesisSource()
        {
            string themes = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "themes");
            string installer = Path.Combine(themes, "installer", "sega-genesis.cfg");
            if (File.Exists(installer)) return installer;
            return Path.Combine(themes, "sega-genesis.cfg");
        }

        UiPalette ReadSegaGenesisTheme()
        {
            try
            {
                string source = SegaGenesisSource();
                if (File.Exists(source)) return ReadThemeFile(source);
                string installed = Path.Combine(AppDataDir, "themes", "sega-genesis.cfg");
                if (File.Exists(installed)) return ReadThemeFile(installed);
            }
            catch { }
            return SegaGenesisFallback();
        }

        void ApplyPalettePackage(UiPalette p)
        {
            ApplyPalette(p);
            if (!String.IsNullOrEmpty(p.FontName)) editorFontName = p.FontName;
            if (p.FontSize > 0) editorFontSize = p.FontSize;
            if (p.TabWidth > 0) tabWidth = p.TabWidth;
            if (p.SoftTabs.HasValue) softTabs = p.SoftTabs.Value;
            if (p.Wrap.HasValue) wrap = p.Wrap.Value;
            if (p.AutoIndent.HasValue) autoIndent = p.AutoIndent.Value;
            if (p.ShowStatus.HasValue) showStatus = p.ShowStatus.Value;
            if (p.TopMost.HasValue) topMost = p.TopMost.Value;
            if (p.DiceSides.HasValue) breakSides = p.DiceSides.Value;
            if (p.DiceCards.HasValue) breakCards = p.DiceCards.Value;
            if (p.DiceWidth.HasValue) breakPanelWidth = p.DiceWidth.Value;
            if (p.DiceSpeed.HasValue) breakRollSpeed = p.DiceSpeed.Value;
            if (p.DiceShowHours.HasValue) breakShowHours = p.DiceShowHours.Value;
            if (p.DiceUninstall.HasValue) breakUninstall.Checked = p.DiceUninstall.Value;
            if (p.DiceAccent.HasValue) breakAccent = p.DiceAccent.Value;
            if (p.DicePanel.HasValue) breakPanelTint = p.DicePanel.Value;
            if (p.DiceCard.HasValue) breakCardTint = p.DiceCard.Value;
            ApplyTheme(); ApplySettings(); SaveSettings();
        }

        bool ThemeMatches(UiPalette p)
        {
            return p != null && p.Chrome == uiChrome && p.Panel == uiPanel && p.Page == uiPage && p.Text == uiText
                && p.Muted == uiMuted && p.Border == uiBorder && p.Accent == uiAccent
                && p.EditorBack == editorBack && p.EditorText == editorText;
        }

        string ThemesFolder()
        {
            string app = null;
            try { app = Path.GetDirectoryName(Application.ExecutablePath); } catch { }
            string folder = String.IsNullOrEmpty(app) ? Path.Combine(AppDataDir, "themes") : Path.Combine(app, "themes");
            if (!Directory.Exists(folder)) { folder = Path.Combine(AppDataDir, "themes"); EnsureDir(folder); }
            return folder;
        }

        void RunUninstaller()
        {
            string app = null;
            try { app = Path.GetDirectoryName(Application.ExecutablePath); } catch { }
            string executable = String.IsNullOrEmpty(app) ? "" : Path.Combine(app, "Uninstall-Milkpad.exe");
            if (!File.Exists(executable)) executable = Path.Combine(AppDataDir, "Uninstall-Milkpad.exe");
            if (!File.Exists(executable))
            {
                MessageBox.Show(this, "Uninstall-Milkpad.exe was not found beside Milkpad.\n\nUse Settings → Apps → Installed apps → Milkpad, or run the uninstaller from the installer folder.", "Uninstall Milkpad", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (MessageBox.Show(this, "Open the Milkpad uninstaller? Save any open work first. The uninstaller will ask before removing anything.", "Uninstall Milkpad", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(executable) { UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Uninstall Milkpad", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        void InstallSegaGenesis(bool notify = true)
        {
            UiPalette p = ReadSegaGenesisTheme();
            if (String.IsNullOrEmpty(p.Name)) p.Name = "Sega Genesis";
            try
            {
                string source = SegaGenesisSource();
                string folder = Path.Combine(AppDataDir, "themes");
                EnsureDir(folder);
                if (File.Exists(source)) File.Copy(source, Path.Combine(folder, "sega-genesis.cfg"), true);
            }
            catch { }
            UiPalette existing = Palettes.Find(x => String.Equals(x.Name, p.Name, StringComparison.CurrentCultureIgnoreCase));
            if (existing == null) Palettes.Add(p); else p = existing;
            ApplyPalettePackage(p);
            if (notify) MessageBox.Show(this, "Sega Genesis is installed and applied.\n\nIt will stay in the Preset list on future launches.", "Sega Genesis theme installer", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        void ShowSegaGenesisInstaller()
        {
            UiPalette p = ReadSegaGenesisTheme();
            using (var f = new Form
            {
                Text = "Sega Genesis theme installer",
                Width = 520, Height = 390,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false, MinimizeBox = false, ShowInTaskbar = false,
                BackColor = uiPanel, ForeColor = uiText
            })
            {
                var heading = new Label { Text = "SEGA GENESIS", Font = new Font("Segoe UI", 20F, FontStyle.Bold), ForeColor = uiAccent, AutoSize = true, Location = new Point(27, 20) };
                var blurb = new Label { Text = "Black hardware, Sega-blue trim, white lettering.\nInstalls into your Milkpad theme folder and applies immediately.", ForeColor = uiMuted, AutoSize = true, Location = new Point(27, 60) };
                var preview = new Panel { Location = new Point(27, 110), Size = new Size(450, 120), BackColor = p.Page, BorderStyle = BorderStyle.FixedSingle };
                var sample = new Label { Text = "SONIC SPEED. QUIET FOCUS.", ForeColor = p.Text, Font = new Font("Consolas", 13F, FontStyle.Bold), AutoSize = true, Location = new Point(22, 28) };
                var sub = new Label { Text = "16-bit energy, modern editor.", ForeColor = p.Muted, AutoSize = true, Location = new Point(22, 58) };
                var accentBar = new Panel { Location = new Point(22, 88), Size = new Size(160, 4), BackColor = p.Accent };
                preview.Controls.Add(sample); preview.Controls.Add(sub); preview.Controls.Add(accentBar);
                var install = new Button { Text = "Install and apply", Width = 132, Height = 30, FlatStyle = FlatStyle.Flat, BackColor = uiAccent, ForeColor = InkOn(uiAccent), Location = new Point(27, 250), Cursor = Cursors.Hand };
                var cancel = new Button { Text = "Cancel", Width = 86, Height = 30, FlatStyle = FlatStyle.Flat, BackColor = uiPanel, ForeColor = uiText, Location = new Point(170, 250), DialogResult = DialogResult.Cancel };
                var note = new Label { Text = "Adds sega-genesis.cfg to your Milkpad theme folder.", ForeColor = uiMuted, AutoSize = true, Location = new Point(27, 292) };
                install.Click += (s, e) => { f.DialogResult = DialogResult.OK; f.Close(); };
                f.Controls.Add(heading); f.Controls.Add(blurb); f.Controls.Add(preview); f.Controls.Add(install); f.Controls.Add(cancel); f.Controls.Add(note);
                f.AcceptButton = install; f.CancelButton = cancel;
                if (f.ShowDialog(this) == DialogResult.OK) InstallSegaGenesis();
            }
        }

        void ShowMilkBall()
        {
            using (var game = new MilkBallForm(uiPanel, uiAccent, uiText, uiMuted)) game.ShowDialog(this);
        }

        void ShowCustomize(bool focusDice = false)
        {
            using (var f = new Form
            {
                Text = "Customize Milkpad",
                Width = 680,
                Height = Math.Min(840, Math.Max(640, Screen.PrimaryScreen.WorkingArea.Height - 80)),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                ShowInTaskbar = false,
                BackColor = uiPanel,
                ForeColor = uiText,
                Font = UiFont(9F)
            })
            {
                var body = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(18, 14, 18, 14), BackColor = uiPanel };
                var grid = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, BackColor = uiPanel };
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54));
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
                int row = 0;

                Func<string, Label> section = text =>
                {
                    var l = new Label { Text = text, AutoSize = true, Font = UiFont(8.5F, FontStyle.Bold), ForeColor = uiMuted, Margin = new Padding(0, 12, 0, 4) };
                    grid.Controls.Add(l, 0, row); grid.SetColumnSpan(l, 2); row++;
                    return l;
                };
                Func<string, Label> label = text => new Label { Text = text, AutoSize = true, ForeColor = uiText, Margin = new Padding(0, 5, 12, 5) };

                section("THEME");
                grid.Controls.Add(label("Preset"), 0, row);
                var preset = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = Mix(uiPanel, uiChrome, 0.35), ForeColor = uiText, Width = 250, Margin = new Padding(0, 3, 0, 5) };
                preset.Items.Add("Custom");
                foreach (UiPalette p in Palettes) preset.Items.Add(p.Name);
                grid.Controls.Add(preset, 1, row++);

                string[] names = { "Window chrome", "Panels and menus", "Page background", "Text", "Muted text", "Borders", "Accent", "Editor background", "Editor text" };
                Color[] picked = { uiChrome, uiPanel, uiPage, uiText, uiMuted, uiBorder, uiAccent, editorBack, editorText };
                Color caretPicked = caretColor, selectionPicked = selectionColor;
                var swatches = new Button[names.Length];
                for (int i = 0; i < names.Length; i++)
                {
                    int index = i;
                    grid.Controls.Add(label(names[i]), 0, row);
                    var swatch = new Button { Text = "Choose…", Height = 25, Width = 160, FlatStyle = FlatStyle.Flat, BackColor = picked[i], ForeColor = InkOn(picked[i]), Margin = new Padding(0, 2, 0, 2), Cursor = Cursors.Hand };
                    swatch.FlatAppearance.BorderSize = 1;
                    swatch.Click += (s, e) =>
                    {
                        using (var dialog = new ColorDialog { FullOpen = true, Color = picked[index] })
                            if (dialog.ShowDialog(f) == DialogResult.OK)
                            {
                                picked[index] = dialog.Color;
                                swatch.BackColor = dialog.Color;
                                swatch.ForeColor = InkOn(dialog.Color);
                                preset.SelectedIndex = 0;
                            }
                    };
                    swatches[i] = swatch;
                    grid.Controls.Add(swatch, 1, row++);
                }

                Action loadPalette = () =>
                {
                    for (int i = 0; i < swatches.Length; i++)
                    {
                        swatches[i].BackColor = picked[i];
                        swatches[i].ForeColor = InkOn(picked[i]);
                    }
                };
                Action<UiPalette> applyEditorPreset = null;
                preset.SelectedIndexChanged += (s, e) =>
                {
                    int index = preset.SelectedIndex - 1;
                    if (index < 0 || index >= Palettes.Count) return;
                    UiPalette p = Palettes[index];
                    picked[0] = p.Chrome; picked[1] = p.Panel; picked[2] = p.Page; picked[3] = p.Text; picked[4] = p.Muted;
                    picked[5] = p.Border; picked[6] = p.Accent; picked[7] = p.EditorBack; picked[8] = p.EditorText;
                    loadPalette();
                    if (applyEditorPreset != null) applyEditorPreset(p);
                };
                int presetIndex = 0;
                for (int i = 0; i < Palettes.Count; i++)
                {
                    UiPalette p = Palettes[i];
                    if (p.Chrome == uiChrome && p.Panel == uiPanel && p.Page == uiPage && p.Text == uiText && p.Muted == uiMuted && p.Border == uiBorder && p.Accent == uiAccent && p.EditorBack == editorBack && p.EditorText == editorText) { presetIndex = i + 1; break; }
                }

                section("INTERFACE");
                grid.Controls.Add(label("UI font"), 0, row);
                var uiFontBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = Mix(uiPanel, uiChrome, 0.35), ForeColor = uiText, Width = 250, Margin = new Padding(0, 3, 0, 5) };
                foreach (object item in fontChooser.Items) uiFontBox.Items.Add(item);
                if (!uiFontBox.Items.Contains(uiFontName)) uiFontBox.Items.Add(uiFontName);
                uiFontBox.SelectedItem = uiFontName;
                grid.Controls.Add(uiFontBox, 1, row++);

                grid.Controls.Add(label("Caret color"), 0, row);
                var caretSwatch = new Button { Text = "Choose…", Height = 25, Width = 160, FlatStyle = FlatStyle.Flat, BackColor = caretPicked == Color.Empty ? editorText : caretPicked, ForeColor = InkOn(caretPicked == Color.Empty ? editorText : caretPicked), Margin = new Padding(0, 2, 0, 2), Cursor = Cursors.Hand };
                caretSwatch.FlatAppearance.BorderSize = 1;
                tips.SetToolTip(caretSwatch, "Color of the blinking insertion caret; Reset returns to the native caret");
                caretSwatch.Click += (s, e) =>
                {
                    using (var dialog = new ColorDialog { FullOpen = true, Color = caretPicked == Color.Empty ? editorText : caretPicked })
                        if (dialog.ShowDialog(f) == DialogResult.OK)
                        {
                            caretPicked = dialog.Color;
                            caretSwatch.BackColor = caretPicked;
                            caretSwatch.ForeColor = InkOn(caretPicked);
                        }
                };
                grid.Controls.Add(caretSwatch, 1, row++);

                grid.Controls.Add(label("Selection color"), 0, row);
                var selectionSwatch = new Button { Text = "Choose…", Height = 25, Width = 160, FlatStyle = FlatStyle.Flat, BackColor = selectionPicked == Color.Empty ? uiAccent : selectionPicked, ForeColor = InkOn(selectionPicked == Color.Empty ? uiAccent : selectionPicked), Margin = new Padding(0, 2, 0, 2), Cursor = Cursors.Hand };
                selectionSwatch.FlatAppearance.BorderSize = 1;
                tips.SetToolTip(selectionSwatch, "Colors a thin outline around selected text; RichEdit keeps its system fill highlight");
                selectionSwatch.Click += (s, e) =>
                {
                    using (var dialog = new ColorDialog { FullOpen = true, Color = selectionPicked == Color.Empty ? uiAccent : selectionPicked })
                        if (dialog.ShowDialog(f) == DialogResult.OK)
                        {
                            selectionPicked = dialog.Color;
                            selectionSwatch.BackColor = selectionPicked;
                            selectionSwatch.ForeColor = InkOn(selectionPicked);
                        }
                };
                grid.Controls.Add(selectionSwatch, 1, row++);

                section("EDITOR");
                grid.Controls.Add(label("Search fonts"), 0, row);
                var fontSearch = new TextBox { Width = 250, BackColor = Mix(uiPanel, uiChrome, 0.35), ForeColor = uiText, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(0, 3, 0, 5) };
                tips.SetToolTip(fontSearch, "Filter the font list by name");
                grid.Controls.Add(fontSearch, 1, row++);
                grid.Controls.Add(label("Default font"), 0, row);
                var fontBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, BackColor = Mix(uiPanel, uiChrome, 0.35), ForeColor = uiText, Width = 250, Margin = new Padding(0, 3, 0, 5) };
                foreach (object item in fontChooser.Items) fontBox.Items.Add(item);
                if (!fontBox.Items.Contains(editorFontName)) fontBox.Items.Add(editorFontName);
                fontBox.SelectedItem = editorFontName;
                grid.Controls.Add(fontBox, 1, row++);
                fontSearch.TextChanged += (s, e) =>
                {
                    string filter = fontSearch.Text.Trim();
                    string selected = fontBox.SelectedItem as string;
                    if (String.IsNullOrEmpty(selected)) selected = editorFontName;
                    fontBox.BeginUpdate();
                    try
                    {
                        fontBox.Items.Clear();
                        foreach (object item in fontChooser.Items)
                        {
                            string name = Convert.ToString(item, CultureInfo.CurrentCulture);
                            if (filter.Length == 0 || name.IndexOf(filter, StringComparison.CurrentCultureIgnoreCase) >= 0) fontBox.Items.Add(item);
                        }
                        if (!fontBox.Items.Contains(selected)) fontBox.Items.Add(selected);
                        fontBox.SelectedItem = selected;
                    }
                    finally { fontBox.EndUpdate(); }
                };

                grid.Controls.Add(label("Font size"), 0, row);
                var fontSize = new NumericUpDown { Minimum = 5, Maximum = 200, Value = (decimal)Math.Max(5, Math.Min(200, editorFontSize)), Width = 90, DecimalPlaces = 0, BackColor = Mix(uiPanel, uiChrome, 0.35), ForeColor = uiText, Margin = new Padding(0, 3, 0, 5) };
                grid.Controls.Add(fontSize, 1, row++);

                grid.Controls.Add(label("Tab width"), 0, row);
                var tabSize = new NumericUpDown { Minimum = 1, Maximum = 8, Value = Math.Max(1, Math.Min(8, tabWidth)), Width = 90, BackColor = Mix(uiPanel, uiChrome, 0.35), ForeColor = uiText, Margin = new Padding(0, 3, 0, 5) };
                grid.Controls.Add(tabSize, 1, row++);

                var soft = new CheckBox { Text = "Insert spaces instead of tab characters", AutoSize = true, Checked = softTabs, ForeColor = uiText, Margin = new Padding(0, 5, 0, 5) };
                grid.Controls.Add(soft, 0, row); grid.SetColumnSpan(soft, 2); row++;

                section("BEHAVIOUR");
                var wrapBox = new CheckBox { Text = "Word wrap", AutoSize = true, Checked = wrap, ForeColor = uiText, Margin = new Padding(0, 5, 0, 5) };
                grid.Controls.Add(wrapBox, 0, row); grid.SetColumnSpan(wrapBox, 2); row++;
                var autoBox = new CheckBox { Text = "Auto-indent new lines", AutoSize = true, Checked = autoIndent, ForeColor = uiText, Margin = new Padding(0, 5, 0, 5) };
                grid.Controls.Add(autoBox, 0, row); grid.SetColumnSpan(autoBox, 2); row++;
                var statusBox = new CheckBox { Text = "Show status bar", AutoSize = true, Checked = showStatus, ForeColor = uiText, Margin = new Padding(0, 5, 0, 5) };
                grid.Controls.Add(statusBox, 0, row); grid.SetColumnSpan(statusBox, 2); row++;
                var topBox = new CheckBox { Text = "Always on top", AutoSize = true, Checked = topMost, ForeColor = uiText, Margin = new Padding(0, 5, 0, 5) };
                grid.Controls.Add(topBox, 0, row); grid.SetColumnSpan(topBox, 2); row++;

                section("LAYOUT");
                grid.Controls.Add(label("Page margin"), 0, row);
                var marginBox = new NumericUpDown { Minimum = 0, Maximum = 40, Value = Math.Max(0, Math.Min(40, pageMargin)), Width = 90, BackColor = Mix(uiPanel, uiChrome, 0.35), ForeColor = uiText, Margin = new Padding(0, 3, 0, 5) };
                grid.Controls.Add(marginBox, 1, row++);
                grid.Controls.Add(label("Window opacity (%)"), 0, row);
                var opacityBox = new NumericUpDown { Minimum = 50, Maximum = 100, Value = (decimal)Math.Round(Math.Max(0.5, Math.Min(1.0, windowOpacity)) * 100), Width = 90, BackColor = Mix(uiPanel, uiChrome, 0.35), ForeColor = uiText, Margin = new Padding(0, 3, 0, 5) };
                grid.Controls.Add(opacityBox, 1, row++);
                var menuBox = new CheckBox { Text = "Show menu bar", AutoSize = true, Checked = showMenu, ForeColor = uiText, Margin = new Padding(0, 5, 0, 5) };
                grid.Controls.Add(menuBox, 0, row); grid.SetColumnSpan(menuBox, 2); row++;
                var toolbarBox = new CheckBox { Text = "Show formatting toolbar", AutoSize = true, Checked = showToolbar, ForeColor = uiText, Margin = new Padding(0, 5, 0, 5) };
                grid.Controls.Add(toolbarBox, 0, row); grid.SetColumnSpan(toolbarBox, 2); row++;

                Label diceHeading = section("ROLL THE DICE");
                grid.Controls.Add(label("Cards on the shelf"), 0, row);
                var diceCardsBox = new NumericUpDown { Minimum = 4, Maximum = 50, Value = Math.Max(4, Math.Min(50, breakCards)), Width = 90, BackColor = Mix(uiPanel, uiChrome, 0.35), ForeColor = uiText, Margin = new Padding(0, 3, 0, 5) };
                tips.SetToolTip(diceCardsBox, "How many installed apps a fresh shelf may hold");
                grid.Controls.Add(diceCardsBox, 1, row++);

                grid.Controls.Add(label("Sides on the die"), 0, row);
                var diceSidesBox = new NumericUpDown { Minimum = 2, Maximum = 100, Value = Math.Max(2, Math.Min(100, breakSides)), Width = 90, BackColor = Mix(uiPanel, uiChrome, 0.35), ForeColor = uiText, Margin = new Padding(0, 3, 0, 5) };
                tips.SetToolTip(diceSidesBox, "Each face of the die maps to one card on the shelf");
                grid.Controls.Add(diceSidesBox, 1, row++);

                grid.Controls.Add(label("Panel width (px)"), 0, row);
                var diceWidthBox = new NumericUpDown { Minimum = 240, Maximum = 520, Increment = 10, Value = Math.Max(240, Math.Min(520, breakPanelWidth)), Width = 90, BackColor = Mix(uiPanel, uiChrome, 0.35), ForeColor = uiText, Margin = new Padding(0, 3, 0, 5) };
                tips.SetToolTip(diceWidthBox, "Width of the shelf beside the page");
                grid.Controls.Add(diceWidthBox, 1, row++);

                grid.Controls.Add(label("Roll speed (ms)"), 0, row);
                var diceSpeedBox = new NumericUpDown { Minimum = 20, Maximum = 200, Increment = 5, Value = Math.Max(20, Math.Min(200, breakRollSpeed)), Width = 90, BackColor = Mix(uiPanel, uiChrome, 0.35), ForeColor = uiText, Margin = new Padding(0, 3, 0, 5) };
                tips.SetToolTip(diceSpeedBox, "Milliseconds between animation frames; lower is faster");
                grid.Controls.Add(diceSpeedBox, 1, row++);

                var diceHoursBox = new CheckBox { Text = "Show Windows focus time on each card", AutoSize = true, Checked = breakShowHours, ForeColor = uiText, Margin = new Padding(0, 5, 0, 5) };
                grid.Controls.Add(diceHoursBox, 0, row); grid.SetColumnSpan(diceHoursBox, 2); row++;
                var diceUninstallBox = new CheckBox { Text = "Run the matching uninstaller when a card is erased", AutoSize = true, Checked = breakUninstall.Checked, ForeColor = uiText, Margin = new Padding(0, 5, 0, 5) };
                tips.SetToolTip(diceUninstallBox, "Only the app's own uninstaller is opened; it still asks before removing anything");
                grid.Controls.Add(diceUninstallBox, 0, row); grid.SetColumnSpan(diceUninstallBox, 2); row++;

                Color diceAccentPicked = breakAccent, dicePanelPicked = breakPanelTint, diceCardPicked = breakCardTint;
                grid.Controls.Add(label("Shelf accent"), 0, row);
                var diceAccentSwatch = new Button { Text = "Choose…", Height = 25, Width = 160, FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 2, 0, 2), Cursor = Cursors.Hand };
                diceAccentSwatch.FlatAppearance.BorderSize = 1;
                grid.Controls.Add(diceAccentSwatch, 1, row++);
                grid.Controls.Add(label("Shelf panel"), 0, row);
                var dicePanelSwatch = new Button { Text = "Choose…", Height = 25, Width = 160, FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 2, 0, 2), Cursor = Cursors.Hand };
                dicePanelSwatch.FlatAppearance.BorderSize = 1;
                grid.Controls.Add(dicePanelSwatch, 1, row++);
                grid.Controls.Add(label("Card background"), 0, row);
                var diceCardSwatch = new Button { Text = "Choose…", Height = 25, Width = 160, FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 2, 0, 2), Cursor = Cursors.Hand };
                diceCardSwatch.FlatAppearance.BorderSize = 1;
                grid.Controls.Add(diceCardSwatch, 1, row++);

                Action refreshDiceSwatches = () =>
                {
                    Color accentPreview = diceAccentPicked == Color.Empty ? uiAccent : diceAccentPicked;
                    diceAccentSwatch.Text = diceAccentPicked == Color.Empty ? "Theme" : "Choose…";
                    diceAccentSwatch.BackColor = accentPreview;
                    diceAccentSwatch.ForeColor = InkOn(accentPreview);
                    Color panelPreview = dicePanelPicked == Color.Empty ? uiPanel : dicePanelPicked;
                    dicePanelSwatch.Text = dicePanelPicked == Color.Empty ? "Theme" : "Choose…";
                    dicePanelSwatch.BackColor = panelPreview;
                    dicePanelSwatch.ForeColor = InkOn(panelPreview);
                    Color cardPreview = diceCardPicked == Color.Empty ? Mix(uiPanel, uiChrome, 0.25) : diceCardPicked;
                    diceCardSwatch.Text = diceCardPicked == Color.Empty ? "Theme" : "Choose…";
                    diceCardSwatch.BackColor = cardPreview;
                    diceCardSwatch.ForeColor = InkOn(cardPreview);
                };
                refreshDiceSwatches();
                diceAccentSwatch.Click += (s, e) =>
                {
                    using (var dialog = new ColorDialog { FullOpen = true, Color = diceAccentPicked == Color.Empty ? BreakAccentColor() : diceAccentPicked })
                        if (dialog.ShowDialog(f) == DialogResult.OK) { diceAccentPicked = dialog.Color; refreshDiceSwatches(); }
                };
                dicePanelSwatch.Click += (s, e) =>
                {
                    using (var dialog = new ColorDialog { FullOpen = true, Color = dicePanelPicked == Color.Empty ? BreakPanelColor() : dicePanelPicked })
                        if (dialog.ShowDialog(f) == DialogResult.OK) { dicePanelPicked = dialog.Color; refreshDiceSwatches(); }
                };
                diceCardSwatch.Click += (s, e) =>
                {
                    using (var dialog = new ColorDialog { FullOpen = true, Color = diceCardPicked == Color.Empty ? BreakCardColor() : diceCardPicked })
                        if (dialog.ShowDialog(f) == DialogResult.OK) { diceCardPicked = dialog.Color; refreshDiceSwatches(); }
                };
                var diceThemeButton = new Button { Text = "Use theme colours", Height = 25, Width = 160, FlatStyle = FlatStyle.Flat, BackColor = uiPanel, ForeColor = uiText, Margin = new Padding(0, 4, 0, 4), Cursor = Cursors.Hand };
                diceThemeButton.FlatAppearance.BorderSize = 1;
                diceThemeButton.Click += (s, e) => { diceAccentPicked = Color.Empty; dicePanelPicked = Color.Empty; diceCardPicked = Color.Empty; refreshDiceSwatches(); };
                grid.Controls.Add(diceThemeButton, 0, row); grid.SetColumnSpan(diceThemeButton, 2); row++;
                var diceNote = new Label { Text = "Colours left on Theme follow the current Milkpad theme. Theme files can set the same values with [Dice], and Lua recipes with theme.color(\"diceaccent\", …).", AutoSize = true, MaximumSize = new Size(560, 0), ForeColor = uiMuted, Margin = new Padding(0, 2, 0, 8) };
                grid.Controls.Add(diceNote, 0, row); grid.SetColumnSpan(diceNote, 2); row++;

                applyEditorPreset = p =>
                {
                    if (!String.IsNullOrEmpty(p.FontName))
                    {
                        if (!fontBox.Items.Contains(p.FontName)) fontBox.Items.Add(p.FontName);
                        fontBox.SelectedItem = p.FontName;
                    }
                    if (p.FontSize > 0) fontSize.Value = (decimal)Math.Max(5, Math.Min(200, p.FontSize));
                    if (p.TabWidth > 0) tabSize.Value = Math.Max(1, Math.Min(8, p.TabWidth));
                    if (p.SoftTabs.HasValue) soft.Checked = p.SoftTabs.Value;
                    if (p.Wrap.HasValue) wrapBox.Checked = p.Wrap.Value;
                    if (p.AutoIndent.HasValue) autoBox.Checked = p.AutoIndent.Value;
                    if (p.ShowStatus.HasValue) statusBox.Checked = p.ShowStatus.Value;
                    if (p.TopMost.HasValue) topBox.Checked = p.TopMost.Value;
                };
                preset.SelectedIndex = presetIndex;
                Action<ThemeScriptResult> applyScript = script =>
                {
                    UiPalette basePalette = null;
                    if (!String.IsNullOrEmpty(script.Preset))
                        foreach (UiPalette candidate in Palettes)
                            if (String.Equals(candidate.Name, script.Preset, StringComparison.CurrentCultureIgnoreCase)) { basePalette = candidate; break; }
                    if (basePalette != null)
                    {
                        picked[0] = basePalette.Chrome; picked[1] = basePalette.Panel; picked[2] = basePalette.Page; picked[3] = basePalette.Text;
                        picked[4] = basePalette.Muted; picked[5] = basePalette.Border; picked[6] = basePalette.Accent; picked[7] = basePalette.EditorBack; picked[8] = basePalette.EditorText;
                        applyEditorPreset(basePalette);
                    }
                    if (script.Chrome.HasValue) picked[0] = script.Chrome.Value;
                    if (script.Panel.HasValue) picked[1] = script.Panel.Value;
                    if (script.Page.HasValue) picked[2] = script.Page.Value;
                    if (script.Text.HasValue) picked[3] = script.Text.Value;
                    if (script.Muted.HasValue) picked[4] = script.Muted.Value;
                    if (script.Border.HasValue) picked[5] = script.Border.Value;
                    if (script.Accent.HasValue) picked[6] = script.Accent.Value;
                    if (script.EditorBack.HasValue) picked[7] = script.EditorBack.Value;
                    if (script.EditorText.HasValue) picked[8] = script.EditorText.Value;
                    if (!String.IsNullOrEmpty(script.FontName))
                    {
                        if (!fontBox.Items.Contains(script.FontName)) fontBox.Items.Add(script.FontName);
                        fontBox.SelectedItem = script.FontName;
                    }
                    if (script.FontSize.HasValue && !Single.IsNaN(script.FontSize.Value) && !Single.IsInfinity(script.FontSize.Value))
                        fontSize.Value = (decimal)Math.Max(5, Math.Min(200, script.FontSize.Value));
                    if (script.TabWidth.HasValue) tabSize.Value = Math.Max(1, Math.Min(8, script.TabWidth.Value));
                    if (script.SoftTabs.HasValue) soft.Checked = script.SoftTabs.Value;
                    if (script.Wrap.HasValue) wrapBox.Checked = script.Wrap.Value;
                    if (script.AutoIndent.HasValue) autoBox.Checked = script.AutoIndent.Value;
                    if (script.ShowStatus.HasValue) statusBox.Checked = script.ShowStatus.Value;
                    if (script.TopMost.HasValue) topBox.Checked = script.TopMost.Value;
                    if (script.DiceSides.HasValue) diceSidesBox.Value = (decimal)Math.Max(2, Math.Min(100, script.DiceSides.Value));
                    if (script.DiceCards.HasValue) diceCardsBox.Value = (decimal)Math.Max(4, Math.Min(50, script.DiceCards.Value));
                    if (script.DiceWidth.HasValue) diceWidthBox.Value = (decimal)Math.Max(240, Math.Min(520, script.DiceWidth.Value));
                    if (script.DiceSpeed.HasValue) diceSpeedBox.Value = (decimal)Math.Max(20, Math.Min(200, script.DiceSpeed.Value));
                    if (script.DiceShowHours.HasValue) diceHoursBox.Checked = script.DiceShowHours.Value;
                    if (script.DiceUninstall.HasValue) diceUninstallBox.Checked = script.DiceUninstall.Value;
                    if (script.DiceAccent.HasValue) diceAccentPicked = script.DiceAccent.Value;
                    if (script.DicePanel.HasValue) dicePanelPicked = script.DicePanel.Value;
                    if (script.DiceCard.HasValue) diceCardPicked = script.DiceCard.Value;
                    refreshDiceSwatches();
                    loadPalette();
                    preset.SelectedIndex = 0;
                };

                body.Controls.Add(grid);
                var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 46, Padding = new Padding(10, 7, 10, 7), BackColor = uiPanel };
                var cancel = new Button { Text = "Cancel", Width = 86, Height = 28, FlatStyle = FlatStyle.Flat, BackColor = uiPanel, ForeColor = uiText, DialogResult = DialogResult.Cancel };
                var apply = new Button { Text = "Apply", Width = 86, Height = 28, FlatStyle = FlatStyle.Flat, BackColor = uiAccent, ForeColor = InkOn(uiAccent) };
                var reset = new Button { Text = "Reset", Width = 86, Height = 28, FlatStyle = FlatStyle.Flat, BackColor = uiPanel, ForeColor = uiText };
                var scriptButton = new Button { Text = "Load Lua script…", Width = 118, Height = 28, FlatStyle = FlatStyle.Flat, BackColor = uiPanel, ForeColor = uiText };
                scriptButton.Click += (s, e) =>
                {
                    using (var dialog = new OpenFileDialog { Filter = "Milkpad Lua theme scripts|*.lua|All files|*.*", Title = "Load a Milkpad theme script" })
                    {
                        try
                        {
                            string app = Path.GetDirectoryName(Application.ExecutablePath);
                            string dir = String.IsNullOrEmpty(app) ? "" : Path.Combine(app, "themes");
                            if (!Directory.Exists(dir)) dir = Path.Combine(AppDataDir, "themes");
                            if (Directory.Exists(dir)) dialog.InitialDirectory = dir;
                        }
                        catch { }
                        if (dialog.ShowDialog(f) != DialogResult.OK) return;
                        try { applyScript(ReadThemeScript(dialog.FileName)); }
                        catch (Exception ex) { MessageBox.Show(f, ex.Message, "Theme script", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                    }
                };
                reset.Click += (s, e) =>
                {
                    UiPalette d = dark ? Palettes[0] : Palettes[1];
                    picked[0] = d.Chrome; picked[1] = d.Panel; picked[2] = d.Page; picked[3] = d.Text; picked[4] = d.Muted;
                    picked[5] = d.Border; picked[6] = d.Accent; picked[7] = d.EditorBack; picked[8] = d.EditorText;
                    loadPalette();
                    preset.SelectedIndex = dark ? 1 : 2;
                    fontBox.SelectedItem = "Consolas";
                    fontSize.Value = 11;
                    tabSize.Value = 4;
                    soft.Checked = true; wrapBox.Checked = true; autoBox.Checked = true; statusBox.Checked = true; topBox.Checked = false;
                    marginBox.Value = 10; opacityBox.Value = 100; menuBox.Checked = true; toolbarBox.Checked = true;
                    diceCardsBox.Value = 20; diceSidesBox.Value = 20; diceWidthBox.Value = 292; diceSpeedBox.Value = 55;
                    diceHoursBox.Checked = true; diceUninstallBox.Checked = FeatureEnabled("break-uninstall");
                    diceAccentPicked = Color.Empty; dicePanelPicked = Color.Empty; diceCardPicked = Color.Empty;
                    refreshDiceSwatches();
                    if (uiFontBox.Items.Contains("Segoe UI")) uiFontBox.SelectedItem = "Segoe UI";
                    caretPicked = Color.Empty; selectionPicked = Color.Empty;
                    caretSwatch.BackColor = editorText; caretSwatch.ForeColor = InkOn(editorText);
                    selectionSwatch.BackColor = uiAccent; selectionSwatch.ForeColor = InkOn(uiAccent);
                };
                apply.Click += (s, e) =>
                {
                    uiChrome = picked[0]; uiPanel = picked[1]; uiPage = picked[2]; uiText = picked[3]; uiMuted = picked[4];
                    uiBorder = picked[5]; uiAccent = picked[6]; editorBack = picked[7]; editorText = picked[8];
                    editorFontSize = (float)fontSize.Value;
                    if (fontBox.Text.Length > 0) editorFontName = fontBox.Text;
                    tabWidth = (int)tabSize.Value;
                    softTabs = soft.Checked;
                    wrap = wrapBox.Checked; autoIndent = autoBox.Checked; showStatus = statusBox.Checked; topMost = topBox.Checked;
                    pageMargin = (int)marginBox.Value; windowOpacity = (double)opacityBox.Value / 100.0; showMenu = menuBox.Checked; showToolbar = toolbarBox.Checked;
                    if (!String.IsNullOrEmpty(uiFontBox.Text)) uiFontName = uiFontBox.Text;
                    caretColor = caretPicked; selectionColor = selectionPicked;
                    breakSides = (int)diceSidesBox.Value; breakCards = (int)diceCardsBox.Value;
                    breakPanelWidth = (int)diceWidthBox.Value; breakRollSpeed = (int)diceSpeedBox.Value;
                    breakShowHours = diceHoursBox.Checked; breakUninstall.Checked = diceUninstallBox.Checked;
                    breakAccent = diceAccentPicked; breakPanelTint = dicePanelPicked; breakCardTint = diceCardPicked;
                    ApplyTheme();
                    ApplySettings();
                    SaveSettings();
                    f.DialogResult = DialogResult.OK;
                    f.Close();
                };
                footer.Controls.Add(apply); footer.Controls.Add(cancel); footer.Controls.Add(reset);
                if (FeatureEnabled("lua-scripts")) footer.Controls.Add(scriptButton);
                f.Controls.Add(body); f.Controls.Add(footer);
                f.AcceptButton = apply; f.CancelButton = cancel;
                f.Shown += (s, e) => { if (focusDice && diceHeading != null) body.ScrollControlIntoView(diceHeading); };
                f.ShowDialog(this);
            }
        }

        void InsertStamp() { editor.SelectedText = DateTime.Now.ToString("h:mm tt M/d/yyyy"); editor.Focus(); }

        void GoToLine()
        {
            int line;
            if (!Int32.TryParse(Prompt("Line number:", "Go to line").Trim(), out line)) return;
            JumpToLine(line);
        }

        void JumpToLine(int line)
        {
            if (line < 1) return;
            line = Math.Min(line, Math.Max(1, editor.Lines.Length));
            int at = editor.GetFirstCharIndexFromLine(line - 1);
            if (at >= 0) JumpTo(at);
            Flash("Line " + line);
        }

        void ChooseFont()
        {
            using (var d = new FontDialog { Font = editor.SelectionFont ?? editor.Font })
                if (d.ShowDialog(this) == DialogResult.OK) { editor.SelectionFont = d.Font; editor.Focus(); }
        }

        bool WriteCopy(string path)
        {
            bool lifted = LiftHighlights();
            try
            {
                if (Path.GetExtension(path).ToLowerInvariant() == ".rtf") editor.SaveFile(path, RichTextBoxStreamType.RichText);
                else File.WriteAllText(path, unixEndings ? editor.Text.Replace("\r\n", "\n") : editor.Text, new UTF8Encoding(false));
                return true;
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Save a copy", MessageBoxButtons.OK, MessageBoxIcon.Error); return false; }
            finally { RestoreHighlights(lifted); }
        }

        void SaveCopy()
        {
            using (var d = new SaveFileDialog { Filter = "Text files|*.txt|Rich Text Format|*.rtf", FileName = Current == null ? "Untitled.txt" : Current.Name })
                if (d.ShowDialog(this) == DialogResult.OK && WriteCopy(d.FileName)) Flash("Copy saved");
        }

        //==== text tools, bookmarks, export, manual and live sessions ====
        void EditorKeys(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Tab)
            {
                e.SuppressKeyPress = true;
                if (AdjustListLevel(e.Shift)) return;
                if (e.Shift) IndentLines(true, true);
                else if (editor.SelectionLength > 0) IndentLines(false, true);
                else editor.SelectedText = softTabs ? new string(' ', Math.Max(1, tabWidth)) : "\t";
                return;
            }
            if (e.KeyCode != Keys.Enter || !autoIndent || editor.SelectionLength > 0) return;
            int index = LogicalLineIndex(editor.SelectionStart);
            if (index >= editor.Lines.Length) return;
            var indent = new StringBuilder();
            foreach (char ch in editor.Lines[index]) { if (ch == ' ' || ch == '\t') indent.Append(ch); else break; }
            if (indent.Length == 0) return;
            e.SuppressKeyPress = true;
            editor.SelectedText = "\n" + indent.ToString();
        }

        bool LineRange(out int start, out int end)
        {
            start = 0; end = 0;
            if (editor.TextLength == 0) return false;
            int first = LogicalLineIndex(editor.SelectionStart);
            int last = editor.SelectionLength > 0 ? LogicalLineIndex(editor.SelectionStart + editor.SelectionLength - 1) : first;
            if (last < first) last = first;
            start = editor.GetFirstCharIndexFromLine(first);
            if (start < 0) return false;
            end = last + 1 < editor.Lines.Length ? editor.GetFirstCharIndexFromLine(last + 1) : editor.TextLength;
            if (end <= start) end = editor.TextLength;
            return end > start;
        }

        void EditLines(string message, Func<string[], string[]> map)
        {
            int start, end;
            if (!LineRange(out start, out end)) return;
            string raw = editor.Text.Substring(start, end - start);
            bool trailing = raw.EndsWith("\n");
            string block = raw.Replace("\r\n", "\n").TrimEnd('\n');
            string[] result = map(block.Split('\n'));
            string replacement = result == null || result.Length == 0 ? "" : String.Join("\r\n", result);
            // Keep the block's original ending; adding a newline to the last line used to
            // silently grow the document whenever a text tool ran.
            if (replacement.Length > 0 && trailing) replacement += "\r\n";
            editor.Select(start, end - start);
            editor.SelectedText = replacement;
            editor.Select(Math.Min(start, editor.TextLength), 0);
            Flash(message);
        }

        void DeleteLines()
        {
            int start, end;
            if (!LineRange(out start, out end)) return;
            editor.Select(start, end - start);
            editor.SelectedText = "";
            editor.Select(Math.Min(start, editor.TextLength), 0);
            Flash("Line deleted");
        }

        // Google Docs-style list nesting. Bullets use RichEdit's paragraph indent so the
        // marker moves with the paragraph; numbered items are text prefixes, so they use
        // the same four-space step as the text tools. Returns false for ordinary text so
        // Tab keeps its normal insert/indent behaviour.
        bool AdjustListLevel(bool outdent)
        {
            if (editor == null) return false;
            int selStart = editor.SelectionStart;
            int selEnd = selStart + editor.SelectionLength;
            int first = LogicalLineIndex(selStart);
            int last = selEnd > selStart ? LogicalLineIndex(selEnd - 1) : first;
            if (last < first) last = first;

            var starts = new List<int>();
            var bullets = new List<bool>();
            var numbers = new List<bool>();
            bool anyList = false;
            for (int i = first; i <= last && i < editor.Lines.Length; i++)
            {
                int lineStart = editor.GetFirstCharIndexFromLine(i);
                starts.Add(lineStart);
                bool isBullet = false;
                if (lineStart >= 0)
                {
                    editor.Select(lineStart, 0);
                    isBullet = editor.SelectionBullet;
                }
                string line = editor.Lines[i];
                bool isNumber = !isBullet && Regex.IsMatch(line, @"^\s*\d+[\.\)]\s");
                bullets.Add(isBullet);
                numbers.Add(isNumber);
                if (isBullet || isNumber) anyList = true;
            }
            if (!anyList)
            {
                editor.Select(selStart, selEnd - selStart);
                return false;
            }

            bool changed = false;
            for (int i = 0; i < starts.Count; i++)
            {
                if (!bullets[i] || starts[i] < 0) continue;
                editor.Select(starts[i], 0);
                int indent = editor.SelectionIndent;
                int step = Math.Max(8, Math.Max(1, tabWidth) * 6);
                int next = outdent ? Math.Max(0, indent - step) : Math.Min(192, indent + step);
                if (next != indent) { editor.SelectionIndent = next; changed = true; }
            }

            var edits = new List<int[]>();
            for (int i = starts.Count - 1; i >= 0; i--)
            {
                if (!numbers[i] || starts[i] < 0) continue;
                string line = editor.Lines[first + i];
                int cut = 0;
                if (outdent)
                {
                    if (line.StartsWith("\t")) cut = 1;
                    else while (cut < Math.Max(1, tabWidth) && cut < line.Length && line[cut] == ' ') cut++;
                    if (cut == 0) continue;
                }
                else
                {
                    int indent = 0;
                    for (int c = 0; c < line.Length && (line[c] == ' ' || line[c] == '\t'); c++) indent += line[c] == '\t' ? Math.Max(1, tabWidth) : 1;
                    if (indent >= Math.Max(1, tabWidth) * 8) continue;
                }
                string indentUnit = softTabs ? new string(' ', Math.Max(1, tabWidth)) : "\t";
                editor.Select(starts[i], cut);
                editor.SelectedText = outdent ? "" : indentUnit;
                changed = true;
                edits.Add(new[] { starts[i], outdent ? -cut : indentUnit.Length });
            }

            edits.Sort((a, b) => a[0].CompareTo(b[0]));
            foreach (int[] edit in edits)
            {
                int at = edit[0], delta = edit[1];
                if (selStart >= at) selStart = Math.Max(at, selStart + delta);
                if (selEnd >= at) selEnd = Math.Max(selStart, selEnd + delta);
            }
            selStart = Math.Max(0, Math.Min(selStart, editor.TextLength));
            selEnd = Math.Max(selStart, Math.Min(selEnd, editor.TextLength));
            editor.Select(selStart, selEnd - selStart);
            // Paragraph indentation changes RTF without changing Text, so TextChanged
            // would not mark a bullet-only edit dirty.
            if (changed && Current != null) { Current.Dirty = true; UpdateTitle(); }
            editor.Focus();
            return true;
        }

        void IndentLines(bool outdent, bool quiet = false)
        {
            int selStart = editor.SelectionStart, selEnd = selStart + editor.SelectionLength;
            int first = LogicalLineIndex(selStart);
            int last = selEnd > selStart ? LogicalLineIndex(selEnd - 1) : first;
            if (first < 0 || last < first) return;
            int start = editor.GetFirstCharIndexFromLine(first);
            if (start < 0) return;
            int end = last + 1 < editor.Lines.Length ? editor.GetFirstCharIndexFromLine(last + 1) : editor.TextLength;
            string raw = editor.Text.Substring(start, end - start);
            bool trailing = raw.EndsWith("\n");
            string[] original = raw.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
            string[] lines = (string[])original.Clone();
            int[] added = new int[original.Length], removed = new int[original.Length], lineStarts = new int[original.Length];
            int offset = 0;
            bool changed = false;
            for (int i = 0; i < original.Length; i++)
            {
                lineStarts[i] = start + offset;
                offset += original[i].Length + 1;
                if (outdent)
                {
                    int cut = 0;
                    if (original[i].StartsWith("\t")) cut = 1;
                    else while (cut < Math.Max(1, tabWidth) && cut < original[i].Length && original[i][cut] == ' ') cut++;
                    removed[i] = cut;
                    if (cut > 0) { lines[i] = original[i].Substring(cut); changed = true; }
                }
                else
                {
                    string unit = softTabs ? new string(' ', Math.Max(1, tabWidth)) : "\t";
                    added[i] = unit.Length;
                    lines[i] = unit + original[i];
                    changed = true;
                }
            }
            if (!changed) return;
            int newStart = MapIndentPos(selStart, original, lineStarts, added, removed);
            int newEnd = MapIndentPos(selEnd, original, lineStarts, added, removed);
            string replacement = String.Join("\r\n", lines);
            if (trailing) replacement += "\r\n";
            editor.Select(start, end - start);
            editor.SelectedText = replacement;
            int limit = editor.TextLength;
            newStart = Math.Max(0, Math.Min(newStart, limit));
            newEnd = Math.Max(newStart, Math.Min(newEnd, limit));
            editor.Select(newStart, newEnd - newStart);
            if (!quiet) Flash(outdent ? "Outdented" : "Indented");
        }

        static int MapIndentPos(int pos, string[] original, int[] lineStarts, int[] added, int[] removed)
        {
            int delta = 0;
            for (int i = 0; i < original.Length; i++)
            {
                if (pos < lineStarts[i]) return pos + delta;
                int local = pos - lineStarts[i];
                if (local <= original[i].Length)
                {
                    int moved = added[i] > 0 ? local + added[i] : Math.Max(0, local - removed[i]);
                    return lineStarts[i] + delta + moved;
                }
                delta += added[i] - removed[i];
            }
            return pos + delta;
        }

        void SortLines(bool descending)
        {
            EditLines(descending ? "Sorted Z to A" : "Sorted A to Z", lines =>
                descending ? lines.OrderByDescending(l => l, StringComparer.CurrentCultureIgnoreCase).ToArray()
                           : lines.OrderBy(l => l, StringComparer.CurrentCultureIgnoreCase).ToArray());
        }

        void TrimLines() { EditLines("Trailing spaces removed", lines => { for (int i = 0; i < lines.Length; i++) lines[i] = lines[i].TrimEnd(); return lines; }); }
        void BlankLines() { EditLines("Blank lines removed", lines => lines.Where(l => l.Trim().Length > 0).ToArray()); }
        void JoinLines() { EditLines("Lines joined", lines => new[] { String.Join(" ", lines.Select(l => l.Trim()).Where(l => l.Length > 0).ToArray()) }); }
        void DuplicateLine() { EditLines("Line duplicated", lines => lines.SelectMany(l => new[] { l, l }).ToArray()); }
        // Quality-of-life toolkit. Text tools work on the selection when there is one,
        // otherwise on the whole document, which keeps the simple commands quick to use.
        void QolMap(string message, Func<string, string> map)
        {
            if (editor == null) return;
            bool whole = editor.SelectionLength == 0;
            int start = whole ? 0 : editor.SelectionStart;
            int length = whole ? editor.TextLength : editor.SelectionLength;
            string source = editor.Text.Substring(start, length);
            string changed = map(source);
            if (String.Equals(changed, source, StringComparison.Ordinal)) { editor.Focus(); Flash("No change"); return; }
            editor.Select(start, length);
            editor.SelectedText = changed;
            editor.Select(start, Math.Min(changed.Length, editor.TextLength));
            editor.Focus();
            SignalFormat();
            Flash(message);
        }

        void QolInsert(string text)
        {
            editor.SelectedText = text;
            editor.SelectionStart += text.Length;
            editor.Focus();
            SignalFormat();
        }

        static string[] QolWords(string text)
        {
            return Regex.Matches(text ?? "", "[A-Z]+(?![a-z])|[A-Z]?[a-z0-9]+").Cast<Match>().Select(m => m.Value).ToArray();
        }

        static string QolCase(string text, string separator, bool upper)
        {
            string[] words = QolWords(text);
            if (words.Length == 0) return text;
            for (int i = 0; i < words.Length; i++)
                words[i] = upper ? words[i].ToUpperInvariant() : words[i].ToLowerInvariant();
            return String.Join(separator, words);
        }

        static string QolCamel(string text)
        {
            string[] words = QolWords(text);
            if (words.Length == 0) return text;
            var result = new StringBuilder(words[0].ToLowerInvariant());
            for (int i = 1; i < words.Length; i++)
                result.Append(words[i].Substring(0, 1).ToUpperInvariant()).Append(words[i].Substring(1).ToLowerInvariant());
            return result.ToString();
        }

        static string QolPascal(string text)
        {
            string[] words = QolWords(text);
            for (int i = 0; i < words.Length; i++)
                words[i] = words[i].Substring(0, 1).ToUpperInvariant() + words[i].Substring(1).ToLowerInvariant();
            return String.Join("", words);
        }

        static string QolWrap(string text, int width)
        {
            var result = new StringBuilder();
            foreach (string paragraph in text.Replace("\r\n", "\n").Split('\n'))
            {
                int lineLength = 0;
                foreach (string word in Regex.Split(paragraph, @"\s+").Where(w => w.Length > 0))
                {
                    if (lineLength > 0 && lineLength + 1 + word.Length > width) { result.Append('\n'); lineLength = 0; }
                    else if (lineLength > 0) { result.Append(' '); lineLength++; }
                    result.Append(word); lineLength += word.Length;
                }
                result.Append('\n');
            }
            return result.ToString().TrimEnd('\n');
        }

        static string QolUnwrap(string text)
        {
            return Regex.Replace(text, @"\r?\n(?=\S)", " ");
        }

        static string QolStraightQuotes(string text)
        {
            return text.Replace('“', '"').Replace('”', '"').Replace('‘', '\'').Replace('’', '\'');
        }

        void QolDedupeLines() { EditLines("Duplicate lines removed", lines => lines.Distinct(StringComparer.CurrentCultureIgnoreCase).ToArray()); }
        void QolReverseLines() { EditLines("Lines reversed", lines => lines.Reverse().ToArray()); }
        void QolShuffleLines() { EditLines("Lines shuffled", lines => lines.OrderBy(x => Guid.NewGuid()).ToArray()); }
        void QolNumberLines() { EditLines("Lines numbered", lines => lines.Select((line, i) => (i + 1) + ". " + line).ToArray()); }
        void QolSortByLength() { EditLines("Sorted by line length", lines => lines.OrderBy(line => line.Length).ToArray()); }
        void QolBulletLines() { EditLines("Bullets added", lines => lines.Select(line => "• " + line).ToArray()); }
        void QolQuoteLines() { EditLines("Lines quoted", lines => lines.Select(line => "> " + line).ToArray()); }
        void QolTrimLeading() { EditLines("Leading spaces removed", lines => lines.Select(line => line.TrimStart()).ToArray()); }

        void QolSelectLine()
        {
            int start, end;
            if (!LineRange(out start, out end)) return;
            editor.Select(start, end - start);
            editor.Focus();
        }

        void QolSelectParagraph()
        {
            if (editor.Lines.Length == 0) return;
            int line = Math.Max(0, Math.Min(editor.GetLineFromCharIndex(editor.SelectionStart), editor.Lines.Length - 1));
            int first = line, last = line;
            while (first > 0 && editor.Lines[first - 1].Trim().Length > 0) first--;
            while (last + 1 < editor.Lines.Length && editor.Lines[last + 1].Trim().Length > 0) last++;
            int start = editor.GetFirstCharIndexFromLine(first);
            int end = last + 1 < editor.Lines.Length ? editor.GetFirstCharIndexFromLine(last + 1) : editor.TextLength;
            if (start < 0) start = 0;
            if (end < start) end = editor.TextLength;
            editor.Select(start, Math.Max(0, end - start));
            editor.Focus();
        }

        void QolSelectWord()
        {
            string text = editor.Text;
            int pos = Math.Max(0, Math.Min(editor.SelectionStart, text.Length));
            int start = pos, end = pos;
            while (start > 0 && (Char.IsLetterOrDigit(text[start - 1]) || text[start - 1] == '_')) start--;
            while (end < text.Length && (Char.IsLetterOrDigit(text[end]) || text[end] == '_')) end++;
            if (end > start) editor.Select(start, end - start);
            editor.Focus();
        }

        void QolDuplicateTab()
        {
            if (Current == null) return;
            SaveEditor();
            var copy = new DocState { Name = Current.Name + " copy", Path = "", Rtf = Current.Rtf };
            copy.Comments.AddRange(Current.Comments);
            copy.Versions.AddRange(Current.Versions);
            docs.Add(copy);
            SwitchDoc(docs.Count - 1);
            Current.Dirty = true;
            DrawTabs();
            Flash("Tab duplicated");
        }

        void QolMoveTab(int delta)
        {
            if (Current == null) return;
            int target = active + delta;
            if (target < 0 || target >= docs.Count) return;
            SaveEditor();
            DocState doc = docs[active];
            docs.RemoveAt(active);
            docs.Insert(target, doc);
            active = target;
            DrawTabs();
            UpdateTitle();
            UpdateStatus();
            editor.Focus();
        }

        void QolZoom(int value)
        {
            zoom.Value = Math.Max(zoom.Minimum, Math.Min(zoom.Maximum, value));
            editor.Focus();
            Flash("Zoom " + zoom.Value + "%");
        }

        void QolToggleAutoIndent() { autoIndent = !autoIndent; SaveSettings(); Flash(autoIndent ? "Auto-indent on" : "Auto-indent off"); }
        void QolToggleSoftTabs() { softTabs = !softTabs; SaveSettings(); Flash(softTabs ? "Spaces for tabs" : "Tab characters"); }
        void QolSetTabWidth(int width) { tabWidth = Math.Max(1, Math.Min(8, width)); SaveSettings(); Flash("Tab width " + tabWidth); }

        void CaseChange(string message, Func<string, string> map)
        {
            if (editor.SelectionLength == 0) { Flash("Select text first"); return; }
            string selected = editor.SelectedText;
            string changed = map(selected);
            if (changed == selected) return;
            editor.SelectedText = changed;
            editor.Select(Math.Max(0, editor.SelectionStart - changed.Length), changed.Length);
            Flash(message);
        }

        static string ToTitleCase(string value)
        {
            var sb = new StringBuilder(value.Length);
            bool wordStart = true;
            foreach (char ch in value)
            {
                sb.Append(wordStart ? Char.ToUpperInvariant(ch) : Char.ToLowerInvariant(ch));
                wordStart = !Char.IsLetterOrDigit(ch);
            }
            return sb.ToString();
        }

        static string ToSentenceCase(string value)
        {
            var sb = new StringBuilder(value.Length);
            bool sentenceStart = true;
            foreach (char ch in value)
            {
                sb.Append(sentenceStart ? Char.ToUpperInvariant(ch) : Char.ToLowerInvariant(ch));
                sentenceStart = ch == '.' || ch == '!' || ch == '?' || ch == '\n';
            }
            return sb.ToString();
        }

        void CopyPlain()
        {
            if (editor.SelectionLength == 0) { Flash("Select text first"); return; }
            try { Clipboard.SetText(editor.SelectedText); Flash("Copied as plain text"); } catch { }
        }

        void ToggleBookmark()
        {
            int at = editor.GetFirstCharIndexFromLine(LogicalLineIndex(editor.SelectionStart));
            if (at < 0) return;
            if (bookmarks.Contains(at)) bookmarks.Remove(at);
            else { bookmarks.Add(at); bookmarks.Sort(); }
            Flash(bookmarks.Count + (bookmarks.Count == 1 ? " bookmark" : " bookmarks"));
        }

        void NextBookmark()
        {
            if (bookmarks.Count == 0) { Flash("No bookmarks"); return; }
            int at = editor.SelectionStart;
            foreach (int mark in bookmarks)
                if (mark > at) { JumpTo(mark); Flash("Bookmark"); return; }
            JumpTo(bookmarks[0]);
            Flash("Bookmark (wrapped)");
        }

        void ClearBookmarks() { bookmarks.Clear(); Flash("Bookmarks cleared"); }

        void JumpTo(int offset)
        {
            editor.Select(Math.Min(Math.Max(0, offset), editor.TextLength), 0);
            editor.ScrollToCaret();
            editor.Focus();
        }

        void ExportHtml()
        {
            using (var d = new SaveFileDialog { Filter = "Web page|*.html", FileName = (Current == null ? "Document" : Path.GetFileNameWithoutExtension(Current.Name)) + ".html" })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    var sb = new StringBuilder();
                    sb.Append("<!DOCTYPE html>\n<html><head><meta charset=\"utf-8\"><title>").Append(System.Net.WebUtility.HtmlEncode(Current == null ? "Milkpad document" : Current.Name)).Append("</title>");
                    sb.Append("<style>body{font-family:Consolas,monospace;font-size:15px;line-height:1.55;margin:48px auto;max-width:820px;padding:0 24px;color:#1a1a1a}</style></head><body><pre style=\"white-space:pre-wrap\">");
                    sb.Append(System.Net.WebUtility.HtmlEncode(editor.Text));
                    sb.Append("</pre></body></html>");
                    File.WriteAllText(d.FileName, sb.ToString(), new UTF8Encoding(false));
                    Flash("Exported HTML");
                }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "Export failed", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }

        void PrintPreview()
        {
            using (var doc = new PrintDocument())
            {
                doc.PrintPage += PrintPage;
                printChar = 0;
                using (var dlg = new PrintPreviewDialog { Document = doc })
                    try { dlg.ShowDialog(this); }
                    catch (Exception ex) { MessageBox.Show(this, ex.Message, "Print preview", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }

        void ShowManual()
        {
            string appDir = Path.GetDirectoryName(Application.ExecutablePath);
            string path = Path.Combine(appDir, "USER-MANUAL.md");
            if (!File.Exists(path)) path = Path.Combine(appDir, "docs", "USER-MANUAL.md");
            if (!File.Exists(path)) path = Path.Combine(appDir, "..", "docs", "USER-MANUAL.md");
            string body;
            try { body = File.Exists(path) ? File.ReadAllText(path) : "USER-MANUAL.md was not found beside milkpad.exe or in docs."; }
            catch (Exception ex) { body = "The manual could not be read: " + ex.Message; }
            var manual = new Form { Text = "Milkpad user manual", Width = 920, Height = 800, StartPosition = FormStartPosition.CenterParent, BackColor = Color.White, MinimizeBox = false };
            var bar = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Color.FromArgb(246, 246, 246) };
            var actions = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = Color.FromArgb(246, 246, 246), Padding = new Padding(0, 4, 0, 0) };
            var saveAs = Button("Save as...", 112, 30); tips.SetToolTip(saveAs, "Save the manual as PDF, Word, RTF, HTML, Markdown or plain text"); saveAs.Click += (s, e) => ExportManual(manual, body);
            var close = Button("Close", 90, 30); close.Click += (s, e) => manual.Close();
            actions.Controls.Add(saveAs); actions.Controls.Add(close);
            bar.Controls.Add(new Label { Text = "Milkpad user manual  ·  F1", Dock = DockStyle.Left, Width = 360, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(12, 0, 0, 0), ForeColor = Color.FromArgb(40, 40, 40) });
            bar.Controls.Add(actions);
            var text = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, BorderStyle = BorderStyle.None, BackColor = Color.White, ForeColor = Color.FromArgb(28, 28, 30), Font = new Font("Consolas", 10.5F), Text = body, WordWrap = true, ScrollBars = RichTextBoxScrollBars.Vertical };
            manual.Controls.Add(text); manual.Controls.Add(bar);
            manual.ShowDialog(this);
        }

        //==== user manual export: PDF, DOCX, RTF, HTML, Markdown, plain text ====
        static readonly string Slash = ((char)92).ToString();
        static string Rt(string command) { return Slash + command; }
        class PdfLine { public string Text; public int Size; public bool Bold; public PdfLine(string text, int size, bool bold) { Text = text; Size = size; Bold = bold; } }
        static string Fmt(double value) { return value.ToString("0.##", CultureInfo.InvariantCulture); }

        static int MdHeading(string line)
        {
            int n = 0; while (n < line.Length && line[n] == '#') n++;
            return (n > 0 && n < line.Length && line[n] == ' ') ? n : 0;
        }
        static bool MdRule(string line) { string t = line.Trim(); return t.Length >= 3 && t.Trim('-').Length == 0; }
        static bool MdTableRule(string line) { string t = line.Trim(); return t.StartsWith("|") && t.Replace("|", "").Replace("-", "").Replace(":", "").Trim().Length == 0; }
        static string MdClean(string line)
        {
            string s = line.Trim();
            if (s.StartsWith("### ")) return s.Substring(4);
            if (s.StartsWith("## ")) return s.Substring(3);
            if (s.StartsWith("# ")) return s.Substring(2);
            if (s.StartsWith("- ")) return s.Substring(2);
            return s;
        }

        void ExportManual(Form owner, string body)
        {
            using (var d = new SaveFileDialog())
            {
                d.Title = "Save the user manual as";
                d.FileName = "Milkpad user manual.pdf";
                d.Filter = "PDF document (*.pdf)|*.pdf|Word document (*.docx)|*.docx|Rich Text Format (*.rtf)|*.rtf|Web page (*.html)|*.html|Markdown (*.md)|*.md|Plain text (*.txt)|*.txt";
                if (d.ShowDialog(owner) != DialogResult.OK) return;
                string target = d.FileName;
                string ext = Path.GetExtension(target).ToLowerInvariant();
                if (ext.Length == 0) { target = target + ".pdf"; ext = ".pdf"; }
                try
                {
                    if (ext == ".pdf") WriteManualPdf(target, body);
                    else if (ext == ".docx") WriteManualDocx(target, body);
                    else if (ext == ".rtf") WriteManualRtf(target, body);
                    else if (ext == ".html" || ext == ".htm") WriteManualHtml(target, body);
                    else File.WriteAllText(target, body, new UTF8Encoding(false));
                }
                catch (Exception ex) { MessageBox.Show(owner, "The manual could not be saved: " + ex.Message, "Save manual", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }

        static string HtmlInline(string text)
        {
            string s = text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
            var sb = new StringBuilder();
            string[] bold = s.Split(new string[] { "**" }, StringSplitOptions.None);
            for (int i = 0; i < bold.Length; i++)
            {
                if (i % 2 == 1) sb.Append("<b>");
                sb.Append(bold[i]);
                if (i % 2 == 1) sb.Append("</b>");
            }
            string half = sb.ToString(); sb.Length = 0;
            string[] code = half.Split('`');
            for (int i = 0; i < code.Length; i++)
            {
                if (i % 2 == 1) sb.Append("<code>");
                sb.Append(code[i]);
                if (i % 2 == 1) sb.Append("</code>");
            }
            return sb.ToString();
        }

        void WriteManualHtml(string path, string body)
        {
            var sb = new StringBuilder();
            sb.Append("<!DOCTYPE html>").Append((char)10);
            sb.Append("<html lang=\"en\"><head><meta charset=\"utf-8\"><title>Milkpad user manual</title>").Append((char)10);
            sb.Append("<style>body{font-family:Segoe UI,Arial,sans-serif;max-width:46em;margin:2.4em auto;padding:0 1.2em;color:#1c1c1e;line-height:1.55}h1{color:#b3164f;border-bottom:2px solid #f2c3d5;padding-bottom:.25em}h2{color:#8e1450;margin-top:1.7em}h3{margin-top:1.15em}code{background:#f6eef2;padding:.1em .3em;border-radius:3px;font-family:Consolas,monospace}table{border-collapse:collapse;width:100%;margin:1em 0}td,th{border:1px solid #ddd;padding:.45em .6em;text-align:left;vertical-align:top}th{background:#faf4f8}li{margin:.2em 0}hr{border:0;border-top:1px solid #eee;margin:1.8em 0}</style></head><body>").Append((char)10);
            bool para = false, list = false, item = false, table = false, headerRow = false;
            foreach (string raw in body.Split((char)10))
            {
                string line = raw.TrimEnd((char)13);
                string t = line.Trim();
                int h = MdHeading(line);
                if (h > 0)
                {
                    if (para) { sb.Append("</p>").Append((char)10); para = false; }
                    if (item) { sb.Append("</li>").Append((char)10); item = false; }
                    if (list) { sb.Append("</ul>").Append((char)10); list = false; }
                    if (table) { sb.Append("</table>").Append((char)10); table = false; }
                    int level = Math.Min(4, h);
                    sb.Append('<').Append('h').Append(level).Append('>').Append(HtmlInline(MdClean(line))).Append("</h").Append(level).Append('>').Append((char)10);
                    continue;
                }
                if (MdRule(line))
                {
                    if (para) { sb.Append("</p>").Append((char)10); para = false; }
                    if (item) { sb.Append("</li>").Append((char)10); item = false; }
                    if (list) { sb.Append("</ul>").Append((char)10); list = false; }
                    if (table) { sb.Append("</table>").Append((char)10); table = false; }
                    sb.Append("<hr>").Append((char)10);
                    continue;
                }
                if (t.Length == 0)
                {
                    if (para) { sb.Append("</p>").Append((char)10); para = false; }
                    if (item) { sb.Append("</li>").Append((char)10); item = false; }
                    if (list) { sb.Append("</ul>").Append((char)10); list = false; }
                    if (table) { sb.Append("</table>").Append((char)10); table = false; }
                    continue;
                }
                if (t.StartsWith("|"))
                {
                    if (para) { sb.Append("</p>").Append((char)10); para = false; }
                    if (item) { sb.Append("</li>").Append((char)10); item = false; }
                    if (list) { sb.Append("</ul>").Append((char)10); list = false; }
                    if (!table) { sb.Append("<table>").Append((char)10); table = true; headerRow = true; }
                    if (MdTableRule(line)) continue;
                    string[] cells = t.Trim('|').Split('|');
                    sb.Append("<tr>");
                    for (int c = 0; c < cells.Length; c++)
                    {
                        string tag = headerRow ? "th" : "td";
                        sb.Append('<').Append(tag).Append('>').Append(HtmlInline(cells[c].Trim())).Append("</").Append(tag).Append('>');
                    }
                    sb.Append("</tr>").Append((char)10);
                    headerRow = false;
                    continue;
                }
                if (t.StartsWith("- "))
                {
                    if (para) { sb.Append("</p>").Append((char)10); para = false; }
                    if (table) { sb.Append("</table>").Append((char)10); table = false; }
                    if (item) { sb.Append("</li>").Append((char)10); item = false; }
                    if (!list) { sb.Append("<ul>").Append((char)10); list = true; }
                    sb.Append("<li>").Append(HtmlInline(MdClean(t)));
                    item = true;
                    continue;
                }
                if (item && (raw.StartsWith("  ") || raw.StartsWith(((char)9).ToString())))
                {
                    sb.Append(' ').Append(HtmlInline(t));
                    continue;
                }
                if (item) { sb.Append("</li>").Append((char)10); item = false; }
                if (table) { sb.Append("</table>").Append((char)10); table = false; }
                if (!para) { sb.Append("<p>"); para = true; }
                else sb.Append(' ');
                sb.Append(HtmlInline(t));
            }
            if (para) sb.Append("</p>").Append((char)10);
            if (item) sb.Append("</li>").Append((char)10);
            if (list) sb.Append("</ul>").Append((char)10);
            if (table) sb.Append("</table>").Append((char)10);
            sb.Append("</body></html>").Append((char)10);
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }

        static string ManualRtfEscape(string text)
        {
            var sb = new StringBuilder();
            foreach (char c in text)
            {
                if (c == (char)92) sb.Append(Slash).Append(Slash);
                else if (c == '{' || c == '}') sb.Append(Slash).Append(c);
                else if (c == (char)9) sb.Append(Rt("tab "));
                else if (c > 127) sb.Append(Rt("u")).Append(c > 32767 ? c - 65536 : (int)c).Append('?');
                else sb.Append(c);
            }
            return sb.ToString();
        }

        static string RtfInline(string text)
        {
            string[] parts = text.Replace("`", "").Split(new string[] { "**" }, StringSplitOptions.None);
            var sb = new StringBuilder();
            for (int i = 0; i < parts.Length; i++)
            {
                if (i % 2 == 1) sb.Append(Rt("b")).Append(' ');
                sb.Append(ManualRtfEscape(parts[i]));
                if (i % 2 == 1) sb.Append(Rt("b0")).Append(' ');
            }
            return sb.ToString();
        }

        void WriteManualRtf(string path, string body)
        {
            var sb = new StringBuilder();
            sb.Append('{').Append(Rt("rtf1")).Append(Rt("ansi")).Append(Rt("deff0"));
            sb.Append('{').Append(Rt("fonttbl")).Append(Rt("f0 Calibri;")).Append(Rt("f1 Consolas;")).Append('}');
            sb.Append(Rt("f0")).Append(Rt("fs21")).Append((char)10);
            foreach (string raw in body.Split((char)10))
            {
                string line = raw.TrimEnd((char)13);
                string t = line.Trim();
                if (t.Length == 0) { sb.Append(Rt("par")).Append((char)10); continue; }
                if (MdRule(line)) { sb.Append(Rt("par")).Append((char)10); continue; }
                if (t.StartsWith("|"))
                {
                    if (MdTableRule(line)) continue;
                    string[] cells = t.Trim('|').Split('|');
                    for (int c = 0; c < cells.Length; c++)
                    {
                        if (c > 0) sb.Append(Rt("tab"));
                        else sb.Append(Rt("li360"));
                        sb.Append(RtfInline(cells[c].Trim()));
                    }
                    sb.Append(Rt("par")).Append((char)10);
                    continue;
                }
                int h = MdHeading(line);
                if (h > 0)
                {
                    int size = h == 1 ? 36 : h == 2 ? 28 : 24;
                    sb.Append(Rt("par")).Append(Rt("keepn")).Append(Rt("sb240")).Append(Rt("b")).Append(Rt("fs")).Append(size).Append(' ');
                    sb.Append(RtfInline(MdClean(line)));
                    sb.Append(Rt("b0")).Append(Rt("fs21")).Append(Rt("par")).Append((char)10);
                    continue;
                }
                if (t.StartsWith("- "))
                {
                    sb.Append(Rt("li360")).Append(Rt("fi-360")).Append(Rt("u8226?")).Append(Rt("tab"));
                    sb.Append(RtfInline(MdClean(t))).Append(Rt("par")).Append((char)10);
                    continue;
                }
                sb.Append(RtfInline(line)).Append(Rt("par")).Append((char)10);
            }
            sb.Append('}');
            File.WriteAllText(path, sb.ToString(), Encoding.GetEncoding(1252));
        }

        static XElement DocxBorder(XNamespace w, string side)
        {
            return new XElement(w + side, new XAttribute(w + "val", "single"), new XAttribute(w + "sz", "4"), new XAttribute(w + "space", "0"), new XAttribute(w + "color", "DDDDDD"));
        }

        static XElement DocxCell(XNamespace w, string text, bool header)
        {
            var run = new XElement(w + "r");
            if (header) run.Add(new XElement(w + "rPr", new XElement(w + "b")));
            run.Add(new XElement(w + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), text.Replace("**", "").Replace("`", "")));
            return new XElement(w + "tc", new XElement(w + "tcPr"), new XElement(w + "p", run));
        }

        void WriteManualDocx(string path, string body)
        {
            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            var docBody = new XElement(w + "body");
            string[] lines = body.Split((char)10);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].TrimEnd((char)13);
                string t = line.Trim();
                if (t.StartsWith("|"))
                {
                    var rows = new List<string[]>();
                    int j = i;
                    for (; j < lines.Length; j++)
                    {
                        string row = lines[j].TrimEnd((char)13).Trim();
                        if (!row.StartsWith("|")) break;
                        if (MdTableRule(row)) continue;
                        rows.Add(row.Trim('|').Split('|'));
                    }
                    i = j - 1;
                    var tbl = new XElement(w + "tbl",
                        new XElement(w + "tblPr",
                            new XElement(w + "tblW", new XAttribute(w + "w", "5000"), new XAttribute(w + "type", "pct")),
                            new XElement(w + "tblBorders", DocxBorder(w, "top"), DocxBorder(w, "left"), DocxBorder(w, "bottom"), DocxBorder(w, "right"), DocxBorder(w, "insideH"), DocxBorder(w, "insideV"))));
                    bool header = true;
                    foreach (string[] cells in rows)
                    {
                        var tr = new XElement(w + "tr");
                        foreach (string cell in cells) tr.Add(DocxCell(w, cell.Trim(), header));
                        tbl.Add(tr);
                        header = false;
                    }
                    docBody.Add(tbl);
                    docBody.Add(new XElement(w + "p"));
                    continue;
                }
                if (t.Length == 0) { docBody.Add(new XElement(w + "p")); continue; }
                if (MdRule(line))
                {
                    docBody.Add(new XElement(w + "p", new XElement(w + "pPr",
                        new XElement(w + "pBdr", new XElement(w + "bottom", new XAttribute(w + "val", "single"), new XAttribute(w + "sz", "6"), new XAttribute(w + "space", "1"), new XAttribute(w + "color", "CCCCCC"))))));
                    continue;
                }
                int h = MdHeading(line);
                int size = h == 1 ? 36 : h == 2 ? 28 : h == 3 ? 24 : 0;
                bool bullet = t.StartsWith("- ");
                string text = h > 0 || bullet ? MdClean(line) : line;
                var para = new XElement(w + "p");
                var pPr = new XElement(w + "pPr");
                if (bullet) pPr.Add(new XElement(w + "numPr", new XElement(w + "ilvl", new XAttribute(w + "val", "0")), new XElement(w + "numId", new XAttribute(w + "val", "1"))));
                if (size > 0) pPr.Add(new XElement(w + "spacing", new XAttribute(w + "before", h == 1 ? "240" : "180"), new XAttribute(w + "after", "60")));
                if (pPr.HasElements) para.Add(pPr);
                string[] parts = text.Split(new string[] { "**" }, StringSplitOptions.None);
                for (int k = 0; k < parts.Length; k++)
                {
                    string piece = parts[k].Replace("`", "");
                    if (piece.Length == 0) continue;
                    var rPr = new XElement(w + "rPr");
                    if (size > 0 || k % 2 == 1) rPr.Add(new XElement(w + "b"));
                    if (size > 0) rPr.Add(new XElement(w + "sz", new XAttribute(w + "val", size.ToString(CultureInfo.InvariantCulture))));
                    var run = new XElement(w + "r");
                    if (rPr.HasElements) run.Add(rPr);
                    run.Add(new XElement(w + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), piece));
                    para.Add(run);
                }
                docBody.Add(para);
            }
            docBody.Add(new XElement(w + "sectPr", new XElement(w + "pgSz", new XAttribute(w + "w", "12240"), new XAttribute(w + "h", "15840")),
                new XElement(w + "pgMar", new XAttribute(w + "top", "1440"), new XAttribute(w + "right", "1440"), new XAttribute(w + "bottom", "1440"), new XAttribute(w + "left", "1440"))));
            var document = new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), new XElement(w + "document", new XAttribute(XNamespace.Xmlns + "w", w), docBody));
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                AddZipText(archive, "[Content_Types].xml", ContentTypesXml);
                AddZipText(archive, "_rels/.rels", PackageRelsXml);
                AddZipText(archive, "word/_rels/document.xml.rels", DocumentRelsXml);
                AddZipText(archive, "word/numbering.xml", NumberingXml);
                AddZipText(archive, "word/document.xml", document.ToString(SaveOptions.DisableFormatting));
            }
        }

        static IEnumerable<string> WrapText(string text, int limit)
        {
            if (limit < 8) limit = 8;
            if (text.Length == 0) { yield return ""; yield break; }
            string rest = text;
            while (rest.Length > limit)
            {
                int cut = rest.LastIndexOf(' ', limit);
                if (cut <= 0) cut = limit;
                yield return rest.Substring(0, cut).TrimEnd();
                rest = rest.Substring(cut).TrimStart();
            }
            yield return rest;
        }

        static string PdfText(string text)
        {
            var sb = new StringBuilder();
            foreach (char c in text)
            {
                if (c == '(' || c == ')' || c == (char)92) sb.Append(Slash).Append(c);
                else if (c < 32) sb.Append(' ');
                else if (c < 127) sb.Append(c);
                else
                {
                    int code = c;
                    if (c == (char)8212 || c == (char)8211) code = '-';
                    else if (c == (char)8216 || c == (char)8217) code = 39;
                    else if (c == (char)8220 || c == (char)8221) code = 34;
                    else if (c == (char)8226) code = '*';
                    else if (c == (char)8230) { sb.Append("..."); continue; }
                    else if (c == (char)8594) { sb.Append("->"); continue; }
                    else if (c == (char)8592) { sb.Append("<-"); continue; }
                    else if (c == (char)8249) code = '<';
                    else if (c == (char)8250) code = '>';
                    else if (c == (char)215) code = 'x';
                    else if (c == (char)10003 || c == (char)10004) { sb.Append("v"); continue; }
                    else if (c > 255) code = 63;
                    sb.Append(Slash).Append(Convert.ToString(code, 8).PadLeft(3, '0'));
                }
            }
            return sb.ToString();
        }

        static string PdfContent(List<PdfLine> lines, int pageNumber, int total, double margin, double height, double bottom)
        {
            var sb = new StringBuilder();
            double y = height - margin;
            foreach (PdfLine line in lines)
            {
                double lead = line.Size * 1.5;
                y -= lead;
                sb.Append("BT /F").Append(line.Bold ? "2" : "1").Append(' ').Append(line.Size.ToString(CultureInfo.InvariantCulture)).Append(" Tf ")
                  .Append(Fmt(margin)).Append(' ').Append(Fmt(y)).Append(" Td (").Append(PdfText(line.Text)).Append(") Tj ET").Append((char)10);
            }
            sb.Append("BT /F1 9 Tf ").Append(Fmt(margin)).Append(' ').Append(Fmt(bottom - 22)).Append(" Td (Page ").Append(pageNumber).Append(" of ").Append(total).Append(") Tj ET");
            return sb.ToString();
        }

        void WriteManualPdf(string path, string body)
        {
            double width = 612, height = 792, margin = 56, bottom = margin + 26;
            var pages = new List<List<PdfLine>>();
            var page = new List<PdfLine>();
            double y = height - margin;
            foreach (string raw in body.Split((char)10))
            {
                string line = raw.TrimEnd((char)13);
                if (MdTableRule(line)) continue;
                string t = line.Trim();
                int h = MdHeading(line);
                string shown;
                int size;
                bool bold;
                if (h > 0) { shown = MdClean(line); size = h == 1 ? 15 : h == 2 ? 12 : 11; bold = true; }
                else if (t.StartsWith("- ")) { shown = "* " + MdClean(t); size = 10; bold = false; }
                else { shown = line.Replace("**", "").Replace("`", "").TrimEnd(); size = 10; bold = false; }
                int indent = t.StartsWith("- ") ? 2 : 0;
                int limit = (int)Math.Floor((width - margin * 2) / (size * 0.6)) - indent;
                bool first = true;
                foreach (string piece in WrapText(shown, limit))
                {
                    string text = first ? piece : new string(' ', indent) + piece;
                    double lead = size * 1.5;
                    if (y - lead < bottom && page.Count > 0) { pages.Add(page); page = new List<PdfLine>(); y = height - margin; }
                    y -= lead;
                    page.Add(new PdfLine(text, size, bold && first));
                    first = false;
                }
            }
            if (page.Count > 0 || pages.Count == 0) pages.Add(page);

            var kids = new StringBuilder();
            var pageObjects = new List<string>();
            var contentObjects = new List<string>();
            int number = 5;
            for (int i = 0; i < pages.Count; i++)
            {
                int pageNum = number, contentNum = number + 1;
                number += 2;
                kids.Append(pageNum).Append(" 0 R ");
                pageObjects.Add(pageNum + " 0 obj" + (char)10 + "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 " + Fmt(width) + " " + Fmt(height) + "] /Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents " + contentNum + " 0 R >>" + (char)10 + "endobj" + (char)10);
                string content = PdfContent(pages[i], i + 1, pages.Count, margin, height, bottom);
                contentObjects.Add(contentNum + " 0 obj" + (char)10 + "<< /Length " + content.Length + " >>" + (char)10 + "stream" + (char)10 + content + (char)10 + "endstream" + (char)10 + "endobj" + (char)10);
            }

            var objects = new List<string>();
            objects.Add("1 0 obj" + (char)10 + "<< /Type /Catalog /Pages 2 0 R >>" + (char)10 + "endobj" + (char)10);
            objects.Add("2 0 obj" + (char)10 + "<< /Type /Pages /Kids [" + kids.ToString().Trim() + "] /Count " + pages.Count + " >>" + (char)10 + "endobj" + (char)10);
            objects.Add("3 0 obj" + (char)10 + "<< /Type /Font /Subtype /Type1 /BaseFont /Courier /Encoding /WinAnsiEncoding >>" + (char)10 + "endobj" + (char)10);
            objects.Add("4 0 obj" + (char)10 + "<< /Type /Font /Subtype /Type1 /BaseFont /Courier-Bold /Encoding /WinAnsiEncoding >>" + (char)10 + "endobj" + (char)10);
            for (int i = 0; i < pageObjects.Count; i++) { objects.Add(pageObjects[i]); objects.Add(contentObjects[i]); }

            var output = new StringBuilder();
            var offsets = new List<int>();
            output.Append("%PDF-1.4").Append((char)10);
            foreach (string obj in objects)
            {
                offsets.Add(output.Length);
                output.Append(obj);
            }
            int startxref = output.Length;
            output.Append("xref").Append((char)10).Append("0 ").Append(offsets.Count + 1).Append((char)10);
            output.Append("0000000000 65535 f").Append((char)13).Append((char)10);
            foreach (int off in offsets) output.Append(off.ToString("D10")).Append(" 00000 n").Append((char)13).Append((char)10);
            output.Append("trailer").Append((char)10).Append("<< /Size ").Append(offsets.Count + 1).Append(" /Root 1 0 R /Info << /Title (Milkpad user manual) /Producer (Milkpad) >> >>").Append((char)10);
            output.Append("startxref").Append((char)10).Append(startxref).Append((char)10).Append("%%EOF").Append((char)10);
            File.WriteAllText(path, output.ToString(), Encoding.GetEncoding(28591));
        }

        //==== live collaboration: peers talk directly over a TCP link ====
        static string Encode(string value) { return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? "")); }
        static string Decode(string value) { try { return Encoding.UTF8.GetString(Convert.FromBase64String(value)); } catch { return "Guest"; } }

        static string LocalAddress()
        {
            try
            {
                foreach (IPAddress address in Dns.GetHostAddresses(Dns.GetHostName()))
                    if (address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address)) return address.ToString();
            }
            catch { }
            return "127.0.0.1";
        }

        void StartLive()
        {
            if (live.Hosting) { ShowSharePane(); Flash("You are already sharing"); return; }
            if (live.Count > 0) { ShowSharePane(); SetShareNotice("You joined someone else's session. Leave it before you start your own."); return; }
            int port = live.Start(45678);
            if (port == 0)
            {
                ShowSharePane();
                SetShareNotice("Milkpad couldn't open a port. Check your firewall and try again.");
                return;
            }
            shareCodeText = LocalAddress() + ":" + port;
            peers.Clear();
            peers[myId] = new PeerInfo { Name = Environment.UserName, Role = "host", IsYou = true, LastSeen = DateTime.Now };
            lastLocalText = editor.Text;
            ShowSharePane();
            if (!beat.Enabled) beat.Start();
            Flash("Sharing is on");
        }

        void JoinLive()
        {
            if (live.Hosting) { ShowSharePane(); SetShareNotice("Stop sharing before joining another document."); return; }
            ShowSharePane();
            joinBox.Focus(); joinBox.SelectAll();
        }

        void JoinShared(string raw)
        {
            if (live.Hosting || live.Count > 0) { SetShareNotice("You are already in a session. Stop sharing before joining another."); return; }
            string host;
            int port;
            if (!ParseSessionCode(raw, out host, out port))
            {
                SetShareNotice("That code does not look right. Paste the code someone sent you, like 192.168.1.20:45678.");
                joinBox.Focus();
                return;
            }
            ShowSharePane();
            try { live.Connect(host, port); }
            catch
            {
                SetShareNotice("Could not reach " + host + ":" + port + ". Check the code and that the other person is still sharing.");
                return;
            }
            shareCodeText = host + ":" + port;
            peers.Clear();
            peers[myId] = new PeerInfo { Name = Environment.UserName, Role = "guest", IsYou = true, LastSeen = DateTime.Now };
            live.Send("H " + Encode(Environment.UserName) + "|guest|" + myId);
            joinBox.Text = "";
            if (!beat.Enabled) beat.Start();
            UpdateShareUi();
            Flash("Joined the shared document");
        }

        void StopLive()
        {
            bool wasHost = live.Hosting;
            beat.Stop(); typingTimer.Stop(); caretTimer.Stop();
            live.Stop(); peers.Clear(); shareCodeText = "";
            lock (outstanding) outstanding.Clear();
            livePane.Visible = false;
            UpdateShareUi();
            Flash(wasHost ? "Stopped sharing" : "Left the shared document");
        }

        // Accepts a bare address or a whole invite message: anything containing address:port
        // works, because that is what people actually paste.
        static bool ParseSessionCode(string raw, out string host, out int port)
        {
            host = ""; port = 0;
            string text = (raw ?? "").Trim();
            for (int i = text.Length - 1; i > 0; i--)
            {
                if (text[i] != ':') continue;
                int end = i + 1;
                while (end < text.Length && text[end] >= '0' && text[end] <= '9') end++;
                if (end == i + 1) continue;
                int value;
                if (!Int32.TryParse(text.Substring(i + 1, end - i - 1), out value) || value < 1 || value > 65535) continue;
                int start = i;
                while (start > 0 && IsCodeChar(text[start - 1])) start--;
                string candidate = text.Substring(start, i - start);
                if (candidate.Length == 0) continue;
                host = candidate; port = value;
                return true;
            }
            return false;
        }

        static bool IsCodeChar(char c) { return Char.IsLetterOrDigit(c) || c == '.' || c == '-'; }

        void ShowSharePane() { breakPane.Visible = false; livePane.Visible = true; UpdateShareUi(); livePane.BringToFront(); }

        void ToggleSharePane()
        {
            if (livePane.Visible) livePane.Visible = false;
            else ShowSharePane();
        }

        void ToggleBreakPane()
        {
            if (breakPane.Visible) { breakPane.Visible = false; return; }
            livePane.Visible = false; commentPane.Visible = false;
            breakPane.Visible = true; breakPane.BringToFront();
            if (!breakScanned && !breakScanning) BeginBreakScan();
        }

        int BreakInnerWidth() { return Math.Max(160, breakPane.Width - 38); }
        Color BreakAccentColor() { return breakAccent == Color.Empty ? uiAccent : breakAccent; }
        Color BreakPanelColor() { return breakPanelTint == Color.Empty ? uiPanel : breakPanelTint; }
        Color BreakCardColor() { return breakCardTint == Color.Empty ? Mix(BreakPanelColor(), uiChrome, 0.25) : breakCardTint; }

        // One place applies every Roll The Dice preference: the panel width, card limit,
        // die size, animation speed, visibility of usage hours and the colour overrides.
        void ApplyDiceSettings()
        {
            breakSides = Math.Max(2, Math.Min(100, breakSides));
            breakCards = Math.Max(4, Math.Min(50, breakCards));
            breakPanelWidth = Math.Max(240, Math.Min(520, breakPanelWidth));
            breakRollSpeed = Math.Max(20, Math.Min(200, breakRollSpeed));
            if (breakPane == null) return;
            breakPane.Width = breakPanelWidth;
            int inner = BreakInnerWidth();
            breakTitle.Width = Math.Max(80, breakPanelWidth - 84);
            breakKicker.Width = inner; breakResult.Width = inner; breakRollButton.Width = inner;
            breakUninstall.Width = inner; breakShelfNote.Width = inner; breakList.Width = inner;
            breakHistory.Width = inner; breakReset.Width = inner;
            breakKicker.Text = "D" + breakSides + " PROGRAM SHELF  •  ROLL & CLEAR";
            breakRollButton.Text = "Roll d" + breakSides;
            breakRollTimer.Interval = breakRollSpeed;
            breakResult.Font = new Font("Consolas", breakSides >= 100 ? 28F : breakSides >= 10 ? 40F : 46F, FontStyle.Bold);
            foreach (BreakProgram p in breakPrograms)
            {
                if (p.Row != null) p.Row.Width = inner;
                if (p.HoursLabel != null) p.HoursLabel.Visible = breakShowHours;
            }
            ApplyDiceColors();
        }

        void ApplyDiceColors()
        {
            if (breakPane == null) return;
            Color panel = BreakPanelColor();
            Color accent = BreakAccentColor();
            Color card = BreakCardColor();
            breakPane.BackColor = panel; breakHeader.BackColor = panel; breakBody.BackColor = panel; breakList.BackColor = panel;
            breakTitle.ForeColor = uiText;
            breakKicker.ForeColor = accent;
            breakResult.ForeColor = accent;
            breakRollButton.BackColor = accent; breakRollButton.ForeColor = InkOn(accent);
            breakRollButton.FlatAppearance.MouseOverBackColor = Mix(accent, Color.White, 0.18);
            breakRollButton.FlatAppearance.MouseDownBackColor = Mix(accent, Color.Black, 0.18);
            breakClose.BackColor = panel; breakClose.ForeColor = uiText;
            breakClose.FlatAppearance.MouseOverBackColor = Mix(panel, uiText, 0.12);
            breakUninstall.BackColor = panel; breakUninstall.ForeColor = uiMuted;
            breakShelfNote.ForeColor = uiMuted; breakHistory.ForeColor = uiMuted;
            breakReset.BackColor = card; breakReset.ForeColor = uiText;
            breakReset.FlatAppearance.MouseOverBackColor = Mix(card, uiText, 0.12);
            breakReset.FlatAppearance.MouseDownBackColor = Mix(card, uiText, 0.20);
            foreach (BreakProgram p in breakPrograms)
            {
                if (p.Row == null) continue;
                p.Row.BackColor = p.Deleted ? Mix(panel, uiBorder, 0.45) : card;
                if (p.NameLabel != null) p.NameLabel.ForeColor = p.Deleted ? uiMuted : uiText;
                if (p.HoursLabel != null) { p.HoursLabel.ForeColor = uiMuted; p.HoursLabel.Visible = breakShowHours; }
            }
            breakPane.Invalidate(true);
        }

        void ResetBreakShelf()
        {
            breakRollTimer.Stop(); breakRollButton.Enabled = true; breakResult.Text = "—"; breakHistory.Text = "No rolls yet. Number 1 erases the app with the highest focus time.";
            breakScanToken++; breakScanning = false; breakScanned = false;
            breakList.Controls.Clear(); breakPrograms.Clear();
            if (IsHandleCreated) BeginBreakScan();
            else { ApplyBreakShelf(new List<BreakProgram>(), "Open the program shelf to load installed apps and Windows focus time."); breakScanned = false; }
        }

        void BeginBreakScan()
        {
            int token = ++breakScanToken;
            var previous = new HashSet<string>(breakLastShelfNames, StringComparer.OrdinalIgnoreCase);
            breakScanning = true; breakScanned = false; breakRollButton.Enabled = false;
            breakList.Controls.Clear(); breakPrograms.Clear(); breakResult.Text = "…";
            breakHistory.Text = "Reading installed applications and drawing a new shelf…";
            breakShelfNote.Text = "Reading installed applications and drawing a new shelf…";
            ThreadPool.QueueUserWorkItem(delegate
            {
                List<BreakProgram> found = ScanInstalledApplications(previous, breakCards);
                try
                {
                    if (IsDisposed || !IsHandleCreated) return;
                    BeginInvoke((MethodInvoker)delegate
                    {
                        if (token != breakScanToken) return;
                        ApplyBreakShelf(found, found.Count == 0 ? "No installed applications with uninstallers were found." : "Drew " + found.Count + " installed apps for this shelf. Hours are Windows focus time; — means no usage record.");
                    });
                }
                catch { }
            });
        }

        void ApplyBreakShelf(List<BreakProgram> shelf, string note)
        {
            breakScanning = false; breakScanned = true; breakList.Controls.Clear(); breakPrograms.Clear(); breakLastShelfNames.Clear(); breakResult.Text = "—";
            breakShelfNote.Text = note;
            foreach (BreakProgram p in shelf.Take(breakCards))
            {
                var row = new Panel { Width = BreakInnerWidth(), Height = 30, BackColor = BreakCardColor(), Margin = new Padding(0, 0, 0, 4) };
                var name = new Label { Text = p.Name, Dock = DockStyle.Fill, ForeColor = uiText, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0), Font = new Font("Segoe UI Semibold", 8.5F) };
                string hoursText = p.UsageKnown ? FormatBreakUsage(p.Hours) : "—";
                var hoursLabel = new Label { Text = hoursText, Dock = DockStyle.Right, Width = 58, ForeColor = uiMuted, TextAlign = ContentAlignment.MiddleRight, Font = new Font("Consolas", 8.5F), Visible = breakShowHours };
                row.Controls.Add(name); row.Controls.Add(hoursLabel);
                if (!String.IsNullOrEmpty(p.FilePath)) tips.SetToolTip(row, p.FilePath);
                p.Row = row; p.NameLabel = name; p.HoursLabel = hoursLabel;
                breakPrograms.Add(p); breakLastShelfNames.Add(p.Name); breakList.Controls.Add(row);
            }
            breakRollButton.Enabled = breakPrograms.Count > 0;
            if (breakPrograms.Count == 0) breakHistory.Text = "No app cards to roll.";
            else if (breakHistory.Text.StartsWith("Reading")) breakHistory.Text = "No rolls yet. Number 1 erases the app with the highest focus time.";
            ApplyDiceColors();
        }

        static bool IsBreakExecutable(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            if (String.IsNullOrEmpty(name) || name == "milkpad") return false;
            if (name.Contains("unins") || name.Contains("uninstall") || name.Contains("setup") || name.Contains("update") || name.Contains("install") || name.Contains("helper") || name.Contains("license") || name.StartsWith("vc_redist") || name.StartsWith("dotnet") || name == "msiexec" || name == "apphost" || name == "testhost" || name == "csc" || name == "vbc" || name.Contains("crash") || name.Contains("service")) return false;
            if (name == "node" || name == "convert" || name == "pngquant" || name == "sniffer" || name == "amecommand" || name.StartsWith("dynamiclink") || name.StartsWith("importer") || name.StartsWith("droplet") || name.StartsWith("ccx")) return false;
            return true;
        }

        static string BreakNormalizeName(string value)
        {
            if (String.IsNullOrEmpty(value)) return "";
            var sb = new StringBuilder();
            foreach (char c in value.ToLowerInvariant())
                if (Char.IsLetterOrDigit(c)) sb.Append(c);
            return sb.ToString();
        }

        static List<BreakProgram> ScanInstalledApplications(HashSet<string> previousNames, int limit)
        {
            List<BreakUsageEntry> usage = LoadBreakUsage();
            var apps = new List<BreakProgram>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            RegistryKey[] roots = { Registry.LocalMachine, Registry.CurrentUser };
            string[] paths = { @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall" };
            foreach (RegistryKey root in roots)
            {
                foreach (string path in paths)
                {
                    try
                    {
                        using (RegistryKey uninstall = root.OpenSubKey(path))
                        {
                            if (uninstall == null) continue;
                            foreach (string subName in uninstall.GetSubKeyNames())
                            {
                                try
                                {
                                    using (RegistryKey item = uninstall.OpenSubKey(subName))
                                    {
                                        if (item == null) continue;
                                        string display = item.GetValue("DisplayName") as string;
                                        if (String.IsNullOrWhiteSpace(display) || IsBreakSystemInstall(item, display)) continue;
                                        string command = item.GetValue("UninstallString") as string;
                                        if (String.IsNullOrWhiteSpace(command)) continue;
                                        string normalized = BreakNormalizeName(display);
                                        if (normalized.Length == 0 || normalized == "milkpad" || !seen.Add(normalized)) continue;
                                        string install = item.GetValue("InstallLocation") as string;
                                        string icon = item.GetValue("DisplayIcon") as string;
                                        string exe = FindBreakAppExecutable(display, install, icon);
                                        BreakUsageEntry used = FindBreakUsage(usage, exe, display);
                                        bool hasUsage = used != null && used.FocusMilliseconds > 0;
                                        double hours = hasUsage ? used.FocusMilliseconds / 3600000.0 : 0;
                                        apps.Add(new BreakProgram(display, hours, exe, command, display, hasUsage));
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                    catch { }
                }
            }
            var random = new Random(unchecked(Environment.TickCount ^ Guid.NewGuid().GetHashCode()));
            for (int i = apps.Count - 1; i > 0; i--)
            {
                int swap = random.Next(i + 1);
                BreakProgram temp = apps[i];
                apps[i] = apps[swap];
                apps[swap] = temp;
            }

            List<BreakProgram> selected = apps.Take(Math.Max(4, Math.Min(50, limit))).ToList();
            if (previousNames != null && selected.Count > 0 && selected.Count == previousNames.Count && selected.All(a => previousNames.Contains(a.Name)))
            {
                BreakProgram replacement = apps.Skip(selected.Count).FirstOrDefault();
                if (replacement != null) selected[selected.Count - 1] = replacement;
            }
            return selected.OrderByDescending(a => a.UsageKnown).ThenByDescending(a => a.Hours).ThenBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        static bool IsBreakSystemInstall(RegistryKey item, string display)
        {
            try
            {
                object system = item.GetValue("SystemComponent");
                if (system != null && Convert.ToInt32(system) != 0) return true;
                string release = item.GetValue("ReleaseType") as string;
                if (!String.IsNullOrEmpty(release) && (release.IndexOf("Update", StringComparison.OrdinalIgnoreCase) >= 0 || release.IndexOf("Hotfix", StringComparison.OrdinalIgnoreCase) >= 0)) return true;
            }
            catch { }
            string name = display.ToLowerInvariant();
            string[] internalNames = { "update for", "security update", "hotfix", "service pack", "redistributable", "runtime", "sdk", ".net", "language pack", "driver", "application verifier", "windows app certification", "template", "kits configuration", "visual c++", "icecap", "winrt", "wpt", "universal crt", "diagnosticshub", "application compatibility", "gameinput", "clr types", "openjdk", "meeting add-in" };
            foreach (string part in internalNames) if (name.Contains(part)) return true;
            return false;
        }

        static string FindBreakAppExecutable(string display, string install, string icon)
        {
            string iconPath = BreakIconPath(icon);
            try
            {
                if (!String.IsNullOrEmpty(iconPath) && iconPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && IsBreakExecutable(iconPath) && File.Exists(iconPath)) return iconPath;
                if (String.IsNullOrWhiteSpace(install)) return "";
                string dir = Environment.ExpandEnvironmentVariables(install.Trim().Trim('"'));
                if (!Directory.Exists(dir)) return "";
                string wanted = BreakNormalizeName(display);
                foreach (string file in Directory.EnumerateFiles(dir, "*.exe", SearchOption.TopDirectoryOnly))
                {
                    if (!IsBreakExecutable(file)) continue;
                    string stem = BreakNormalizeName(Path.GetFileNameWithoutExtension(file));
                    if (stem.Length > 1 && wanted.Length > 1 && (wanted.Contains(stem) || stem.Contains(wanted))) return file;
                }
            }
            catch { }
            return "";
        }

        static List<BreakUsageEntry> LoadBreakUsage()
        {
            var entries = new List<BreakUsageEntry>();
            try
            {
                using (RegistryKey root = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\UserAssist"))
                {
                    if (root == null) return entries;
                    foreach (string guid in root.GetSubKeyNames())
                    {
                        try
                        {
                            using (RegistryKey count = root.OpenSubKey(guid + @"\Count"))
                            {
                                if (count == null) continue;
                                foreach (string valueName in count.GetValueNames())
                                {
                                    byte[] data = count.GetValue(valueName) as byte[];
                                    if (data == null || data.Length < 16) continue;
                                    string path = BreakRot13(valueName);
                                    if (path.StartsWith("UEME_", StringComparison.OrdinalIgnoreCase)) continue;
                                    entries.Add(new BreakUsageEntry
                                    {
                                        Path = path,
                                        FocusMilliseconds = BitConverter.ToUInt32(data, 12),
                                        RunCount = BitConverter.ToInt32(data, 4)
                                    });
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
            return entries;
        }

        static BreakUsageEntry FindBreakUsage(List<BreakUsageEntry> entries, string exePath, string display)
        {
            if (entries.Count == 0) return null;
            string fullPath = "";
            try { if (!String.IsNullOrEmpty(exePath)) fullPath = Path.GetFullPath(exePath); } catch { }
            if (!String.IsNullOrEmpty(fullPath))
            {
                foreach (BreakUsageEntry entry in entries)
                    if (BreakPathsEqual(entry.Path, fullPath)) return entry;
            }
            string fileName = String.IsNullOrEmpty(exePath) ? "" : Path.GetFileName(exePath);
            if (!String.IsNullOrEmpty(fileName))
            {
                BreakUsageEntry bestFile = null;
                foreach (BreakUsageEntry entry in entries)
                    if (String.Equals(Path.GetFileName(entry.Path), fileName, StringComparison.OrdinalIgnoreCase) && (bestFile == null || entry.FocusMilliseconds > bestFile.FocusMilliseconds)) bestFile = entry;
                if (bestFile != null) return bestFile;
            }
            string wanted = BreakNormalizeName(display);
            if (wanted.Length < 3) return null;
            BreakUsageEntry best = null;
            foreach (BreakUsageEntry entry in entries)
            {
                string normalized = BreakNormalizeName(entry.Path);
                string stem = entry.Path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? BreakNormalizeName(Path.GetFileNameWithoutExtension(entry.Path)) : "";
                bool match = normalized.Contains(wanted) || (stem.Length >= 3 && wanted.Contains(stem));
                if (match && (best == null || entry.FocusMilliseconds > best.FocusMilliseconds)) best = entry;
            }
            return best;
        }

        static string FormatBreakUsage(double hours)
        {
            if (hours <= 0) return "0 h";
            if (hours < 0.01) return Math.Max(1, (int)Math.Round(hours * 3600)) + " s";
            return hours.ToString("0.##", CultureInfo.InvariantCulture) + " h";
        }

        static string BreakRot13(string value)
        {
            if (String.IsNullOrEmpty(value)) return "";
            var sb = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                if (c >= 'A' && c <= 'Z') sb.Append((char)('A' + (c - 'A' + 13) % 26));
                else if (c >= 'a' && c <= 'z') sb.Append((char)('a' + (c - 'a' + 13) % 26));
                else sb.Append(c);
            }
            return sb.ToString();
        }

        void RollBreak()
        {
            if (breakRollTimer.Enabled) return;
            if (!breakPrograms.Any(p => !p.Deleted))
            {
                breakHistory.Text = "The shelf is empty. Reset the shelf to play again.";
                return;
            }
            breakTicks = 0; breakFinal = breakRandom.Next(1, Math.Max(2, breakSides) + 1); breakRollButton.Enabled = false; breakRollTimer.Start();
        }

        void BreakRollTick()
        {
            breakTicks++;
            if (breakTicks < 14) { breakResult.Text = breakRandom.Next(1, Math.Max(2, breakSides) + 1).ToString(CultureInfo.InvariantCulture); return; }
            breakRollTimer.Stop(); breakResult.Text = breakFinal.ToString(CultureInfo.InvariantCulture); breakRollButton.Enabled = true;
            EraseBreakProgram(breakFinal);
        }

        void EraseBreakProgram(int roll)
        {
            var candidates = breakPrograms.Where(p => !p.Deleted).OrderByDescending(p => p.Hours).ToList();
            if (candidates.Count == 0) return;
            BreakProgram victim = candidates[(roll - 1) % candidates.Count];
            victim.Deleted = true;
            victim.NameLabel.Text = victim.Name + " — ERASED";
            victim.HoursLabel.Text = "ERASED";
            ApplyDiceColors();
            string usage = victim.UsageKnown ? FormatBreakUsage(victim.Hours) + " focus" : "usage unknown";
            string line = "d20 " + roll + " → " + victim.Name + " (" + usage + ") erased.";
            if (breakUninstall.Checked)
            {
                string installedName = victim.InstalledName;
                string uninstall = victim.UninstallCommand;
                if (String.IsNullOrEmpty(uninstall)) uninstall = FindBreakUninstaller(victim, out installedName);
                if (String.IsNullOrEmpty(uninstall)) line += "\nNo installed uninstaller found for " + victim.Name + ".";
                else if (LaunchBreakUninstaller(uninstall)) line += "\nUninstaller opened for " + installedName + ".";
                else line += "\nCould not open the uninstaller for " + installedName + ".";
            }
            breakHistory.Text = line + (breakHistory.Text.StartsWith("No rolls") ? "" : "\n" + breakHistory.Text);
        }

        static string[] BreakUninstallAliases(string name)
        {
            switch (name)
            {
                case "Chrome": return new[] { "Google Chrome" };
                case "Firefox": return new[] { "Mozilla Firefox", "Firefox" };
                case "VS Code": return new[] { "Visual Studio Code", "Microsoft Visual Studio Code" };
                case "Word": return new[] { "Microsoft Word" };
                case "Excel": return new[] { "Microsoft Excel" };
                case "Photoshop": return new[] { "Adobe Photoshop" };
                case "Unity": return new[] { "Unity Editor" };
                case "OBS": return new[] { "OBS Studio" };
                case "Terminal": return new[] { "Windows Terminal" };
                case "Paint": return new[] { "Paint" };
                case "VLC": return new[] { "VLC media player" };
                default: return new[] { name };
            }
        }

        static string FindBreakUninstaller(string cardName, out string installedName)
        {
            return FindBreakUninstaller(cardName, null, out installedName);
        }

        static string FindBreakUninstaller(BreakProgram card, out string installedName)
        {
            if (card == null) { installedName = ""; return null; }
            return FindBreakUninstaller(card.Name, card.FilePath, out installedName);
        }

        static string FindBreakUninstaller(string cardName, string filePath, out string installedName)
        {
            installedName = "";
            string[] aliases = BreakUninstallAliases(cardName);
            RegistryKey[] roots = { Registry.LocalMachine, Registry.CurrentUser };
            string[] paths = { @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall" };
            foreach (RegistryKey root in roots)
            {
                foreach (string path in paths)
                {
                    try
                    {
                        using (RegistryKey uninstall = root.OpenSubKey(path))
                        {
                            if (uninstall == null) continue;
                            foreach (string subName in uninstall.GetSubKeyNames())
                            {
                                try
                                {
                                    using (RegistryKey item = uninstall.OpenSubKey(subName))
                                    {
                                        if (item == null) continue;
                                        string display = item.GetValue("DisplayName") as string;
                                        if (String.IsNullOrWhiteSpace(display)) continue;
                                        bool match = BreakUninstallerMatchesPath(item, filePath);
                                        if (!match)
                                        {
                                            foreach (string alias in aliases)
                                            {
                                                if (display.Equals(alias, StringComparison.OrdinalIgnoreCase) || display.StartsWith(alias + " ", StringComparison.OrdinalIgnoreCase))
                                                { match = true; break; }
                                            }
                                        }
                                        if (!match) continue;
                                        string command = item.GetValue("UninstallString") as string;
                                        if (String.IsNullOrWhiteSpace(command)) continue;
                                        installedName = display;
                                        return command;
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                    catch { }
                }
            }
            return null;
        }

        static bool BreakUninstallerMatchesPath(RegistryKey item, string filePath)
        {
            if (item == null || String.IsNullOrEmpty(filePath)) return false;
            try
            {
                string icon = item.GetValue("DisplayIcon") as string;
                string iconPath = BreakIconPath(icon);
                if (BreakPathsEqual(iconPath, filePath)) return true;
                string install = item.GetValue("InstallLocation") as string;
                if (String.IsNullOrWhiteSpace(install)) return false;
                string folder = Path.GetDirectoryName(filePath);
                if (String.IsNullOrEmpty(folder)) return false;
                string root = Path.GetFullPath(Environment.ExpandEnvironmentVariables(install.Trim().Trim('"')));
                return BreakPathsEqual(folder, root);
            }
            catch { return false; }
        }

        static string BreakIconPath(string icon)
        {
            if (String.IsNullOrWhiteSpace(icon)) return "";
            string value = icon.Trim();
            int comma = value.LastIndexOf(',');
            if (comma > 0 && comma < value.Length - 1 && value.Substring(comma + 1).Trim().All(Char.IsDigit)) value = value.Substring(0, comma);
            value = value.Trim();
            if (value.StartsWith("\""))
            {
                int end = value.IndexOf('\"', 1);
                if (end > 1) value = value.Substring(1, end - 1);
            }
            return Environment.ExpandEnvironmentVariables(value.Trim());
        }

        static bool BreakPathsEqual(string first, string second)
        {
            if (String.IsNullOrEmpty(first) || String.IsNullOrEmpty(second)) return false;
            try { return String.Equals(Path.GetFullPath(first).TrimEnd(Path.DirectorySeparatorChar), Path.GetFullPath(second).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase); }
            catch { return false; }
        }

        static bool LaunchBreakUninstaller(string command)
        {
            try
            {
                var info = new System.Diagnostics.ProcessStartInfo("cmd.exe", "/d /s /c \"" + command + "\"");
                info.UseShellExecute = true;
                info.WindowStyle = System.Diagnostics.ProcessWindowStyle.Normal;
                System.Diagnostics.Process.Start(info);
                return true;
            }
            catch { return false; }
        }


        string InviteText()
        {
            string doc = Current == null ? "a document" : "“" + Current.Name + "”";
            string nl = ((char)13).ToString() + ((char)10);
            return "Milkpad — join me in " + doc + nl + nl + "Session code: " + shareCodeText + nl + nl + "Open Milkpad, choose Tools → Share document…, paste the code and press Join session.";
        }

        void CopyShareText(string text, string message, Button source)
        {
            if (String.IsNullOrEmpty(text)) return;
            try { Clipboard.SetText(text); }
            catch { SetShareNotice("Could not copy to the clipboard."); return; }
            Flash(message);
            if (source == null) return;
            string original = source.Text;
            source.Text = "Copied";
            int token = ++copyToken;
            var reset = new Timer { Interval = 1400 };
            reset.Tick += (s, e) => { reset.Stop(); reset.Dispose(); if (token == copyToken) source.Text = original; };
            reset.Start();
        }

        void SetShareNotice(string message)
        {
            shareStatus.Text = message;
            shareStatus.ForeColor = Color.FromArgb(230, 130, 130);
            int token = ++shareToken;
            var reset = new Timer { Interval = 6000 };
            reset.Tick += (s, e) => { reset.Stop(); reset.Dispose(); if (token == shareToken) UpdateShareUi(); };
            reset.Start();
        }

        void Beat()
        {
            if (live.Count == 0) { beat.Stop(); return; }
            PrunePeers();
            SendCaret();
            UpdateShareUi();
        }

        void PrunePeers()
        {
            DateTime cutoff = DateTime.Now.AddSeconds(-20);
            foreach (string name in peers.Where(p => !p.Value.IsYou && p.Value.LastSeen < cutoff).Select(p => p.Key).ToList()) peers.Remove(name);
        }

        static string AuthorName(string id)
        {
            string name = id ?? "";
            int cut = name.IndexOf('#');
            return cut > 0 ? name.Substring(0, cut) : name;
        }

        void JumpToPeer(PeerInfo peer)
        {
            if (peer == null || peer.IsYou || editor.Lines.Length == 0) return;
            int at = editor.GetFirstCharIndexFromLine(Math.Min(Math.Max(0, peer.Line - 1), editor.Lines.Length - 1));
            if (at < 0) return;
            int offset = at + Math.Max(0, peer.Col - 1);
            JumpTo(Math.Min(offset, editor.TextLength));
        }


        // Every local change becomes a small op: the common prefix and suffix of the old and
        // new text are kept and only the changed middle is sent, so peers merge edits instead
        // of replacing each other's document.
        void EmitLocalOp()
        {
            string now = editor.Text;
            string before = lastLocalText;
            lastLocalText = now;
            if (live.Count == 0 || now == before) return;
            if (Math.Abs(now.Length - before.Length) > 4000) { BroadcastText(); return; }
            int prefix = 0, max = Math.Min(now.Length, before.Length);
            while (prefix < max && now[prefix] == before[prefix]) prefix++;
            int suffix = 0;
            while (suffix < max - prefix && now[now.Length - 1 - suffix] == before[before.Length - 1 - suffix]) suffix++;
            int removed = before.Length - prefix - suffix;
            string inserted = now.Substring(prefix, now.Length - prefix - suffix);
            if (removed == 0 && inserted.Length == 0) return;
            string anchorBefore = before.Substring(Math.Max(0, prefix - 24), Math.Min(24, prefix));
            string anchorAfter = before.Length > prefix + removed ? before.Substring(prefix + removed, Math.Min(24, before.Length - prefix - removed)) : "";
            PruneOutstanding();
            lock (outstanding)
                outstanding.Add(new LiveOp { Author = myId, Seq = ++localSeq, Pos = prefix, Del = removed, Ins = inserted, Prefix = anchorBefore, Suffix = anchorAfter, SentAt = DateTime.Now });
            live.Send("E " + Encode(myId) + "|" + localSeq + "|" + prefix + "|" + removed + "|" + Encode(inserted) + "|" + Encode(anchorBefore) + "|" + Encode(anchorAfter));
        }

        void PruneOutstanding()
        {
            DateTime cutoff = DateTime.Now.AddMilliseconds(-2000);
            lock (outstanding) outstanding.RemoveAll(o => o.SentAt < cutoff);
        }

        LiveOp ParseOp(string payload)
        {
            string[] parts = payload.Split('|');
            if (parts.Length < 5) return null;
            int seq, pos, del;
            if (!Int32.TryParse(parts[1], out seq) || !Int32.TryParse(parts[2], out pos) || !Int32.TryParse(parts[3], out del)) return null;
            return new LiveOp
            {
                Author = Decode(parts[0]), Seq = seq, Pos = pos, Del = del, Ins = Decode(parts[4]),
                Prefix = parts.Length > 5 ? Decode(parts[5]) : "",
                Suffix = parts.Length > 6 ? Decode(parts[6]) : "",
                SentAt = DateTime.Now
            };
        }

        void HandleOp(string payload)
        {
            LiveOp op = ParseOp(payload);
            if (op == null) return;
            if (op.Author == myId)
            {
                lock (outstanding) outstanding.RemoveAll(o => o.Seq == op.Seq);
                return;
            }
            if (live.Hosting) live.Send("E " + payload);
            MarkTyping(op.Author);
            TransformIncoming(op);
            ApplyOp(op);
        }

        // A remote edit is also the only "typing" signal needed: the hint fades by itself.
        void MarkTyping(string author)
        {
            PeerInfo peer;
            if (!peers.TryGetValue(author, out peer) || peer.IsYou) return;
            peer.TypingUntil = DateTime.Now.AddSeconds(1.6);
            if (!typingTimer.Enabled) typingTimer.Start();
        }


        // Move a remote edit past the edits this window made after it was produced, so two
        // people typing at once shift each other instead of overwriting the other's text.
        void TransformIncoming(LiveOp op)
        {
            PruneOutstanding();
            lock (outstanding)
            {
                foreach (LiveOp mine in outstanding)
                {
                    if (mine.Ins.Length > 0 && mine.Del == 0)
                    {
                        if (op.Pos > mine.Pos || (op.Pos == mine.Pos && String.CompareOrdinal(op.Author, mine.Author) > 0)) op.Pos += mine.Ins.Length;
                    }
                    else if (mine.Del == 0) continue;
                    else if (op.Pos >= mine.Pos + mine.Del) op.Pos += mine.Ins.Length - mine.Del;
                    else if (op.Pos > mine.Pos) op.Pos = mine.Pos;
                }
            }
        }

        // Find where the edit belongs by its surrounding text, falling back to the number.
        // Once the text before the edit is found, that answer wins: the trailing anchor only
        // breaks ties when the preceding text has itself changed.
        int ResolveAnchor(LiveOp op, string text, out bool anchored)
        {
            int pos = Math.Max(0, Math.Min(op.Pos, text.Length));
            anchored = false;
            if (op.Prefix.Length > 0)
            {
                int at = -1;
                if (text.Length > 0) at = text.LastIndexOf(op.Prefix, Math.Min(pos, text.Length - 1), StringComparison.Ordinal);
                if (at < 0) at = text.IndexOf(op.Prefix, StringComparison.Ordinal);
                if (at >= 0) { pos = Math.Min(text.Length, at + op.Prefix.Length); anchored = true; }
            }
            if (!anchored && op.Suffix.Length > 0)
            {
                // The trailing text marks the end of the edit, so step back over what it removed.
                int at = text.IndexOf(op.Suffix, StringComparison.Ordinal);
                if (at >= 0) pos = Math.Max(0, at - op.Del);
            }
            return Math.Max(0, Math.Min(pos, text.Length));
        }

        void ApplyOp(LiveOp op)
        {
            bool anchored;
            op.Pos = ResolveAnchor(op, editor.Text, out anchored);
            int pos = Math.Max(0, Math.Min(op.Pos, editor.TextLength));
            int del = Math.Max(0, Math.Min(op.Del, editor.TextLength - pos));
            string insert = op.Ins ?? "";
            int caret = editor.SelectionStart, caretLength = editor.SelectionLength, top = TopVisibleChar();
            applyingRemote = true;
            try
            {
                editor.Select(pos, del);
                editor.SelectedText = insert;
            }
            catch { }
            applyingRemote = false;
            // Move this window's caret only when the edit happened at or before it.
            if (pos + del <= caret) caret += insert.Length - del;
            else if (pos < caret) caret = pos + insert.Length;
            caret = Math.Max(0, Math.Min(caret, editor.TextLength));
            RestoreView(top, pos < top ? insert.Length - del : 0);
            editor.Select(caret, Math.Min(caretLength, editor.TextLength - caret));
            lastLocalText = editor.Text;
            MarkRemoteChange();
        }

        int TopVisibleChar()
        {
            try
            {
                if (editor.TextLength == 0) return 0;
                return Math.Max(0, editor.GetCharIndexFromPosition(new Point(2, 2)));
            }
            catch { return 0; }
        }

        // Put the viewport back where the local person was reading, so a collaborator typing
        // further down the document never yanks the page around.
        void RestoreView(int topChar, int shift)
        {
            if (editor.TextLength == 0) return;
            editor.Select(Math.Max(0, Math.Min(topChar + shift, editor.TextLength)), 0);
            editor.ScrollToCaret();
        }

        // Remote typing only marks the document dirty and schedules the expensive refresh;
        // doing that per keystroke rebuilt the tab strip and made the window blink.
        void MarkRemoteChange()
        {
            if (Current != null && !Current.Dirty) { Current.Dirty = true; UpdateTabLabels(); UpdateTitle(); }
            statusTimer.Stop(); statusTimer.Start();
            rtfTimer.Stop(); rtfTimer.Start();
            presence.Invalidate();
        }


        // Formatting carries no text edit, so those actions send one document snapshot.
        void SignalFormat() { if (live.Count > 0 && !applyingRemote) BroadcastText(); }

        void BroadcastText()
        {
            if (live.Count == 0) return;
            try
            {
                string rtf = editor.Rtf;
                lastSentRtf = rtf;
                live.Send("R " + Convert.ToBase64String(Encoding.UTF8.GetBytes(rtf)));
            }
            catch { }
        }

        void SendCaret()
        {
            if (live.Count == 0) return;
            try
            {
                int lineIndex = LogicalLineIndex(editor.SelectionStart);
                int line = lineIndex + 1;
                int col = editor.SelectionStart - lineStarts[lineIndex] + 1;
                live.Send("C " + Encode(myId) + "|" + line + "|" + col);
            }
            catch { }
        }

        void ReceiveLive(string line)
        {
            if (line.Length < 2) return;
            if (InvokeRequired) { try { BeginInvoke(new Action<string>(ReceiveLive), new object[] { line }); } catch { } return; }
            char kind = line[0];
            string payload = line.Length > 2 ? line.Substring(2) : "";
            if (kind == 'R') ApplyRemote(payload);
            else if (kind == 'E') HandleOp(payload);
            else if (kind == 'H') RegisterPeer(payload);
            else if (kind == 'C')
            {
                // The host keeps every participant's caret in sync with everyone else.
                if (live.Hosting) live.Send("C " + payload);
                TrackCaret(payload);
            }
        }

        void ApplyRemote(string payload)
        {
            try
            {
                string rtf = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
                if (rtf == editor.Rtf) return;
                int start = editor.SelectionStart, length = editor.SelectionLength, top = TopVisibleChar();
                applyingRemote = true;
                try { editor.Rtf = rtf; }
                catch { applyingRemote = false; return; }
                applyingRemote = false;
                RestoreView(Math.Min(top, editor.TextLength), 0);
                int at = Math.Min(start, editor.TextLength);
                editor.Select(at, Math.Min(length, Math.Max(0, editor.TextLength - at)));
                lastLocalText = editor.Text; lastSentRtf = editor.Rtf;
                MarkRemoteChange();
            }
            catch { applyingRemote = false; }
        }

        void RegisterPeer(string payload)
        {
            string[] parts = payload.Split('|');
            string name = Decode(parts[0]);
            string role = parts.Length > 1 && parts[1] == "host" ? "host" : "guest";
            // Peers are keyed by the per-instance id, not the Windows name: two windows on one
            // computer run as the same user, and they are still two different people.
            string id = parts.Length > 2 && parts[2].Length > 0 ? parts[2] : name;
            if (id == myId)
            {
                PeerInfo self;
                if (!peers.TryGetValue(id, out self)) peers[id] = self = new PeerInfo { Name = name };
                self.Name = name; self.IsYou = true; self.LastSeen = DateTime.Now;
                UpdateShareUi();
                return;
            }
            PeerInfo peer;
            bool known = peers.TryGetValue(id, out peer);
            if (!known)
            {
                string display = name;
                int n = 2;
                while (peers.Values.Any(p => p.Name == display)) display = name + " (" + n++ + ")";
                peer = new PeerInfo { Name = display };
                peers[id] = peer;
                name = display;
            }
            peer.Role = role; peer.LastSeen = DateTime.Now;
            if (!live.Hosting) { UpdateShareUi(); return; }
            live.Send("H " + Encode(Environment.UserName) + "|host|" + myId);
            if (!known) live.Send("H " + Encode(name) + "|" + role + "|" + id);
            BroadcastText();
            UpdateShareUi();
        }

        void TrackCaret(string payload)
        {
            string[] parts = payload.Split('|');
            if (parts.Length < 3) return;
            string id = Decode(parts[0]);
            int line, col;
            Int32.TryParse(parts[1], out line);
            Int32.TryParse(parts[2], out col);
            PeerInfo peer;
            if (!peers.TryGetValue(id, out peer)) { peer = new PeerInfo { Name = AuthorName(id) }; peers[id] = peer; }
            peer.Line = line; peer.Col = col; peer.LastSeen = DateTime.Now;
            UpdateShareUi();
        }

        // The one place that reflects session state. Labels change only when their text
        // changed, and the people list repaints itself, so nothing is torn down or rebuilt.
        void UpdateShareUi()
        {
            bool sharing = live.Hosting || live.Count > 0;
            if (sharing)
            {
                PeerInfo me;
                if (!peers.TryGetValue(myId, out me))
                {
                    me = new PeerInfo { Name = Environment.UserName };
                    peers[myId] = me;
                }
                me.IsYou = true; me.Role = live.Hosting ? "host" : "guest"; me.LastSeen = DateTime.Now;
            }
            int people = Math.Max(1, peers.Count);
            string doc = Current == null ? "This document" : "“" + Current.Name + "”";
            SetText(shareStatus, sharing
                ? (live.Hosting ? doc + " is shared. Anyone with the code can edit it." : "You are editing together. Changes show up as you type.")
                : "Share this document so someone else can type in it with you. No account needed.");
            shareStatus.ForeColor = uiMuted;
            startShareButton.Visible = !sharing;
            joinHint.Visible = !sharing;
            joinBox.Visible = !sharing;
            joinButton.Visible = !sharing;
            shareOnline.Visible = sharing;
            shareCodeCard.Visible = live.Hosting;
            copyInviteButton.Visible = live.Hosting;
            SetText(shareCode, shareCodeText.Length > 0 ? shareCodeText : "not connected");
            SetText(stopShareButton, live.Hosting ? "Stop sharing" : "Leave session");
            stopShareButton.Top = live.Hosting ? 114 : 4;
            sharePeople.Top = live.Hosting ? 154 : 46;
            peerList.Top = live.Hosting ? 178 : 70;
            shareHint.Top = live.Hosting ? 372 : 264;
            SetText(sharePeople, "People (" + people + ")");
            List<PeerInfo> list = peers.Values.OrderByDescending(p => p.IsYou).ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
            peerList.SetPeers(list);
            presence.SetPeers(list);
            presence.Visible = sharing && list.Any(p => !p.IsYou);
            SetText(liveLabel, sharing ? "● Live · " + people : "");
            liveLabel.ForeColor = sharing ? uiAccent : uiMuted;
            SetText(shareButton, sharing ? "Shared" : "Share");
            if (!sharing) { shareCodeText = ""; peers.Clear(); }
        }

        void RefreshLive() { UpdateShareUi(); }

        static void SetText(Control control, string text) { if (control.Text != text) control.Text = text; }

        void LivePeersChanged()
        {
            if (InvokeRequired) { try { BeginInvoke(new Action(LivePeersChanged)); } catch { } return; }
            if (live.Count == 0)
            {
                if (!live.Hosting)
                {
                    bool ended = peers.Count > 0;
                    peers.Clear(); shareCodeText = "";
                    if (ended) Flash("The shared session ended");
                }
                else
                {
                    // The host keeps its code; only the people who left drop off the list.
                    foreach (string name in peers.Where(p => !p.Value.IsYou).Select(p => p.Key).ToList()) peers.Remove(name);
                }
            }
            if (live.Count > 0 && !beat.Enabled) beat.Start();
            if (live.Count == 0 && !live.Hosting) beat.Stop();
            UpdateShareUi();
        }


        static bool IsDarkColor(Color c) { return (c.R * 299 + c.G * 587 + c.B * 114) / 1000 < 140; }
        static Color Mix(Color a, Color b, double amount)
        {
            if (amount < 0) amount = 0; if (amount > 1) amount = 1;
            return Color.FromArgb((int)Math.Round(a.R + (b.R - a.R) * amount), (int)Math.Round(a.G + (b.G - a.G) * amount), (int)Math.Round(a.B + (b.B - a.B) * amount));
        }
        static Color InkOn(Color background) { return IsDarkColor(background) ? Color.White : Color.FromArgb(24, 24, 26); }
        static string ColorHex(Color c) { return "#" + c.R.ToString("X2", CultureInfo.InvariantCulture) + c.G.ToString("X2", CultureInfo.InvariantCulture) + c.B.ToString("X2", CultureInfo.InvariantCulture); }
        static bool TryColor(string value, out Color color)
        {
            color = Color.Black;
            try
            {
                if (String.IsNullOrWhiteSpace(value)) return false;
                color = ColorTranslator.FromHtml(value.Trim());
                return true;
            }
            catch { return false; }
        }

        void ApplyPalette(UiPalette p)
        {
            if (p == null) return;
            uiChrome = p.Chrome; uiPanel = p.Panel; uiPage = p.Page; uiText = p.Text; uiMuted = p.Muted;
            uiBorder = p.Border; uiAccent = p.Accent; editorBack = p.EditorBack; editorText = p.EditorText;
            dark = p.IsDark;
        }

        UiPalette PaletteFromFields(string name)
        {
            return new UiPalette { Name = name, IsDark = dark, Chrome = uiChrome, Panel = uiPanel, Page = uiPage, Text = uiText, Muted = uiMuted, Border = uiBorder, Accent = uiAccent, EditorBack = editorBack, EditorText = editorText };
        }

        // The feature registry is the one place to expose a new optional
        // customization capability. RegisterFeature gives it a name, a default and
        // a features.cfg key; FeatureEnabled reads it at the point of use. A new
        // feature can therefore be added without threading another bool through
        // the settings loader, the dialog and the menu code.
        void RegisterFeature(string key, string name, string description, bool defaultValue)
        {
            featureFlags.Add(new FeatureFlag { Key = key, Name = name, Description = description, DefaultValue = defaultValue });
            featureValues[key] = defaultValue;
        }

        void LoadFeatureFlags()
        {
            if (featuresLoaded) return;
            featuresLoaded = true;
            RegisterFeature("lua-scripts", "Lua theme scripts", "Show the Load Lua script button in Customize", true);
            RegisterFeature("break-uninstall", "Break uninstallers", "Open the matching installed app's uninstaller when a break card is erased", true);
            string path = Path.Combine(AppDataDir, "features.cfg");
            if (!File.Exists(path))
            {
                try
                {
                    string app = Path.GetDirectoryName(Application.ExecutablePath);
                    if (!String.IsNullOrEmpty(app)) path = Path.Combine(app, "features.cfg");
                }
                catch { }
            }
            if (!File.Exists(path)) return;
            try
            {
                foreach (string raw in File.ReadAllLines(path))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#") || line.StartsWith("//")) continue;
                    if (line.StartsWith("[") && line.EndsWith("]")) continue;
                    int eq = line.IndexOf('=');
                    if (eq < 0) continue;
                    string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                    if (featureValues.ContainsKey(key)) featureValues[key] = ParseOnOff(line.Substring(eq + 1).Trim());
                }
            }
            catch { }
        }

        bool FeatureEnabled(string key)
        {
            bool enabled;
            return featureValues.TryGetValue(key, out enabled) && enabled;
        }

        // MUGEN-style motifs are just data files. Milkpad looks in its own themes
        // folder and in the user data folder, so a theme can ship with the app or be
        // dropped in later without rebuilding it.
        void LoadThemeFiles()
        {
            if (themesLoaded) return;
            themesLoaded = true;
            var seen = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
            foreach (UiPalette built in Palettes) if (!String.IsNullOrEmpty(built.Name)) seen.Add(built.Name);
            var folders = new List<string>();
            try
            {
                string app = Path.GetDirectoryName(Application.ExecutablePath);
                if (!String.IsNullOrEmpty(app)) folders.Add(Path.Combine(app, "themes"));
            }
            catch { }
            folders.Add(Path.Combine(AppDataDir, "themes"));
            var scanned = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
            foreach (string folder in folders)
            {
                try
                {
                    if (!Directory.Exists(folder)) continue;
                    if (!scanned.Add(Path.GetFullPath(folder))) continue;
                    foreach (string file in Directory.GetFiles(folder, "*.cfg"))
                    {
                        try
                        {
                            UiPalette theme = ReadThemeFile(file);
                            if (theme == null) continue;
                            string name = theme.Name;
                            if (String.IsNullOrEmpty(name)) name = Path.GetFileNameWithoutExtension(file);
                            string unique = name;
                            int suffix = 2;
                            while (seen.Contains(unique)) unique = name + " (" + suffix++ + ")";
                            theme.Name = unique;
                            seen.Add(unique);
                            Palettes.Add(theme);
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }

        static UiPalette ReadThemeFile(string path)
        {
            UiPalette p = new UiPalette
            {
                Name = Path.GetFileNameWithoutExtension(path),
                Chrome = Palettes[0].Chrome, Panel = Palettes[0].Panel, Page = Palettes[0].Page,
                Text = Palettes[0].Text, Muted = Palettes[0].Muted, Border = Palettes[0].Border,
                Accent = Palettes[0].Accent, EditorBack = Palettes[0].EditorBack, EditorText = Palettes[0].EditorText
            };
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#") || line.StartsWith("//")) continue;
                if (line.StartsWith("[") && line.EndsWith("]")) continue;
                int eq = line.IndexOf('=');
                if (eq < 0) continue;
                string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                string value = line.Substring(eq + 1).Trim();
                int semicolon = value.IndexOf(';');
                if (semicolon >= 0) value = value.Substring(0, semicolon).Trim();
                int slashes = value.IndexOf("//", StringComparison.Ordinal);
                if (slashes >= 0) value = value.Substring(0, slashes).Trim();
                value = value.Trim('"');
                if (value.Length == 0 && key != "font") continue;
                Color color;
                if (key == "name") p.Name = value;
                else if (key == "dark") p.IsDark = ParseOnOff(value);
                else if (key == "chrome" || key == "window") { if (TryColor(value, out color)) p.Chrome = color; }
                else if (key == "panel" || key == "menu") { if (TryColor(value, out color)) p.Panel = color; }
                else if (key == "page" || key == "background") { if (TryColor(value, out color)) p.Page = color; }
                else if (key == "text" || key == "ink") { if (TryColor(value, out color)) p.Text = color; }
                else if (key == "muted") { if (TryColor(value, out color)) p.Muted = color; }
                else if (key == "border" || key == "line") { if (TryColor(value, out color)) p.Border = color; }
                else if (key == "accent" || key == "highlight") { if (TryColor(value, out color)) p.Accent = color; }
                else if (key == "editor" || key == "editorbg" || key == "editorbackground") { if (TryColor(value, out color)) p.EditorBack = color; }
                else if (key == "editorfg" || key == "editortext") { if (TryColor(value, out color)) p.EditorText = color; }
                else if (key == "font" || key == "fontname") p.FontName = value;
                else if (key == "size" || key == "fontsize") { float size; if (Single.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out size) && size >= 5 && size <= 200) p.FontSize = size; }
                else if (key == "tab" || key == "tabwidth") { int width; if (Int32.TryParse(value, out width)) p.TabWidth = Math.Max(1, Math.Min(8, width)); }
                else if (key == "softtabs") p.SoftTabs = ParseOnOff(value);
                else if (key == "wrap" || key == "wordwrap") p.Wrap = ParseOnOff(value);
                else if (key == "autoindent") p.AutoIndent = ParseOnOff(value);
                else if (key == "statusbar" || key == "status") p.ShowStatus = ParseOnOff(value);
                else if (key == "ontop" || key == "topmost") p.TopMost = ParseOnOff(value);
                else if (key == "dicesides") { int n; if (Int32.TryParse(value, out n)) p.DiceSides = Math.Max(2, Math.Min(100, n)); }
                else if (key == "dicecards") { int n; if (Int32.TryParse(value, out n)) p.DiceCards = Math.Max(4, Math.Min(50, n)); }
                else if (key == "dicewidth") { int n; if (Int32.TryParse(value, out n)) p.DiceWidth = Math.Max(240, Math.Min(520, n)); }
                else if (key == "dicespeed") { int n; if (Int32.TryParse(value, out n)) p.DiceSpeed = Math.Max(20, Math.Min(200, n)); }
                else if (key == "dicehours") p.DiceShowHours = ParseOnOff(value);
                else if (key == "diceuninstall") p.DiceUninstall = ParseOnOff(value);
                else if (key == "diceaccent") { if (TryColor(value, out color)) p.DiceAccent = color; }
                else if (key == "dicepanel") { if (TryColor(value, out color)) p.DicePanel = color; }
                else if (key == "dicecard") { if (TryColor(value, out color)) p.DiceCard = color; }
            }
            p.IsDark = IsDarkColor(p.Page);
            return p;
        }

        static bool ParseOnOff(string value)
        {
            string v = (value ?? "").Trim().ToLowerInvariant();
            return v == "1" || v == "on" || v == "yes" || v == "true";
        }

        void ApplyTheme()
        {
            dark = IsDarkColor(uiPage);
            BackColor = uiPage; ForeColor = uiText;
            editor.BackColor = editorBack; editor.ForeColor = editorText;
            if (sheet != null) sheet.BackColor = editorBack;
            if (emptyHint != null) { emptyHint.BackColor = editorBack; emptyHint.ForeColor = uiMuted; }
            foreach (Control c in Controls) Theme(c, uiPanel, uiText);
            if (tabs.Parent != null) tabs.Parent.BackColor = uiChrome;
            if (menuBar != null) menuBar.BackColor = uiChrome;
            if (toolsHost != null) toolsHost.BackColor = uiChrome;
            if (toolsRow != null) toolsRow.BackColor = uiChrome;
            if (status != null) { status.BackColor = uiChrome; foreach (Control c in status.Controls) c.BackColor = uiChrome; }
            if (findBar != null) findBar.BackColor = uiPanel;
            if (contentHost != null) contentHost.BackColor = uiPage;
            foreach (FlatCombo combo in combos) { combo.Dark = dark; combo.FieldColor = Mix(uiPanel, uiChrome, 0.35); combo.InkColor = uiMuted; combo.Invalidate(); }
            peerList.Dark = dark;
            shareCodeCard.BackColor = Mix(uiPanel, uiChrome, 0.35);
            shareCode.ForeColor = uiAccent;
            copyCodeButton.BackColor = Mix(uiPanel, uiChrome, 0.55);
            DrawTabs();
            UpdateStatus();
            UpdateShareUi();
            peerList.Invalidate(); presence.Invalidate();
            ApplyDiceColors();
            Invalidate(true);
        }


        void ThemeCombo(ComboBox combo)
        {
            combo.DrawMode = DrawMode.OwnerDrawFixed;
            combo.ItemHeight = 20;
            combo.DrawItem += ComboDraw;
            var flat = combo as FlatCombo;
            if (flat != null) { flat.Dark = dark; combos.Add(flat); }
        }

        void ComboDraw(object sender, DrawItemEventArgs e)
        {
            var combo = (ComboBox)sender;
            // Only the open drop-down list highlights its selected row; the closed field stays plain.
            bool lit = combo.DroppedDown && (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            Color back = lit ? Mix(uiPanel, uiAccent, 0.18) : uiPanel;
            Color ink = uiText;
            using (var brush = new SolidBrush(back)) e.Graphics.FillRectangle(brush, e.Bounds);
            if (e.Index < 0) return;
            string text = combo.GetItemText(combo.Items[e.Index]);
            using (var brush = new SolidBrush(ink)) e.Graphics.DrawString(text, combo.Font, brush, e.Bounds.Left + 4, e.Bounds.Top + 2);
        }

        void Theme(Control c, Color panel, Color ink)
        {
            if (c == editor || c == sheet) return;
            if (c == zoom) { c.BackColor = c.Parent != null ? c.Parent.BackColor : panel; return; }
            if (c is Panel || c is FlowLayoutPanel || c is TableLayoutPanel) c.BackColor = c.Parent == null || c.Parent is Form ? uiPage : panel;
            else if (c is Button)
            {
                var button = (Button)c;
                if ((button.Tag as string) == "accent")
                {
                    button.BackColor = uiAccent; button.ForeColor = InkOn(uiAccent);
                    button.FlatAppearance.MouseOverBackColor = Mix(uiAccent, Color.White, 0.18);
                    button.FlatAppearance.MouseDownBackColor = Mix(uiAccent, Color.Black, 0.18);
                }
                else
                {
                    button.BackColor = panel; button.ForeColor = ink;
                    button.FlatAppearance.MouseOverBackColor = Mix(panel, ink, 0.12);
                    button.FlatAppearance.MouseDownBackColor = Mix(panel, ink, 0.20);
                }
            }
            else if (c is Label || c is CheckBox) c.ForeColor = ink;
            else if (c is ComboBox || c is TextBox) { c.BackColor = Mix(panel, uiChrome, 0.35); c.ForeColor = ink; }
            foreach (Control x in c.Controls) Theme(x, panel, ink);
        }


        void SaveSettings()
        {
            try
            {
                EnsureDir(AppDataDir);
                var body = new StringBuilder();
                body.Append("theme=").Append(dark ? "dark" : "light").Append((char)10);
                body.Append("wrap=").Append(wrap ? "on" : "off").Append((char)10);
                body.Append("statusbar=").Append(showStatus ? "on" : "off").Append((char)10);
                body.Append("lineendings=").Append(unixEndings ? "unix" : "windows").Append((char)10);
                body.Append("ontop=").Append(topMost ? "on" : "off").Append((char)10);
                body.Append("autoindent=").Append(autoIndent ? "on" : "off").Append((char)10);
                body.Append("chrome=").Append(ColorHex(uiChrome)).Append((char)10);
                body.Append("panel=").Append(ColorHex(uiPanel)).Append((char)10);
                body.Append("page=").Append(ColorHex(uiPage)).Append((char)10);
                body.Append("text=").Append(ColorHex(uiText)).Append((char)10);
                body.Append("muted=").Append(ColorHex(uiMuted)).Append((char)10);
                body.Append("border=").Append(ColorHex(uiBorder)).Append((char)10);
                body.Append("accent=").Append(ColorHex(uiAccent)).Append((char)10);
                body.Append("editorbg=").Append(ColorHex(editorBack)).Append((char)10);
                body.Append("editorfg=").Append(ColorHex(editorText)).Append((char)10);
                body.Append("font=").Append(editorFontName).Append((char)10);
                body.Append("fontsize=").Append(editorFontSize.ToString("R", CultureInfo.InvariantCulture)).Append((char)10);
                body.Append("tabwidth=").Append(tabWidth.ToString(CultureInfo.InvariantCulture)).Append((char)10);
                body.Append("softtabs=").Append(softTabs ? "on" : "off").Append((char)10);
                body.Append("pagemargin=").Append(pageMargin.ToString(CultureInfo.InvariantCulture)).Append((char)10);
                body.Append("opacity=").Append(windowOpacity.ToString("R", CultureInfo.InvariantCulture)).Append((char)10);
                body.Append("showmenu=").Append(showMenu ? "on" : "off").Append((char)10);
                body.Append("showtoolbar=").Append(showToolbar ? "on" : "off").Append((char)10);
                body.Append("uifont=").Append(uiFontName).Append((char)10);
                if (caretColor != Color.Empty) body.Append("caretcolor=").Append(ColorHex(caretColor)).Append((char)10);
                if (selectionColor != Color.Empty) body.Append("selectioncolor=").Append(ColorHex(selectionColor)).Append((char)10);
                body.Append("dicesides=").Append(breakSides.ToString(CultureInfo.InvariantCulture)).Append((char)10);
                body.Append("dicecards=").Append(breakCards.ToString(CultureInfo.InvariantCulture)).Append((char)10);
                body.Append("dicewidth=").Append(breakPanelWidth.ToString(CultureInfo.InvariantCulture)).Append((char)10);
                body.Append("dicespeed=").Append(breakRollSpeed.ToString(CultureInfo.InvariantCulture)).Append((char)10);
                body.Append("dicehours=").Append(breakShowHours ? "on" : "off").Append((char)10);
                body.Append("diceuninstall=").Append(breakUninstall.Checked ? "on" : "off").Append((char)10);
                if (breakAccent != Color.Empty) body.Append("diceaccent=").Append(ColorHex(breakAccent)).Append((char)10);
                if (breakPanelTint != Color.Empty) body.Append("dicepanel=").Append(ColorHex(breakPanelTint)).Append((char)10);
                if (breakCardTint != Color.Empty) body.Append("dicecard=").Append(ColorHex(breakCardTint)).Append((char)10);
                Rectangle frame;
                if (fullScreen) frame = windowedBounds;
                else if (WindowState == FormWindowState.Normal) frame = Bounds;
                else frame = RestoreBounds;
                if (frame.Width < 400 || frame.Height < 300) frame = windowedBounds;
                if (frame.Width >= 400 && frame.Height >= 300)
                {
                    body.Append("windowx=").Append(frame.X.ToString(CultureInfo.InvariantCulture)).Append((char)10);
                    body.Append("windowy=").Append(frame.Y.ToString(CultureInfo.InvariantCulture)).Append((char)10);
                    body.Append("windowwidth=").Append(frame.Width.ToString(CultureInfo.InvariantCulture)).Append((char)10);
                    body.Append("windowheight=").Append(frame.Height.ToString(CultureInfo.InvariantCulture)).Append((char)10);
                }
                body.Append("windowmaximized=").Append(!fullScreen && WindowState == FormWindowState.Maximized ? "on" : "off").Append((char)10);
                body.Append("layoutversion=2").Append((char)10);
                File.WriteAllText(Path.Combine(AppDataDir, "settings.txt"), body.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        void LoadSettings()
        {
            try
            {
                string path = Path.Combine(AppDataDir, "settings.txt");
                if (!File.Exists(path)) return;
                bool customPalette = false;
                int windowX = 0, windowY = 0, windowWidth = 0, windowHeight = 0, layoutVersion = 0;
                foreach (string line in File.ReadAllLines(path))
                {
                    int eq = line.IndexOf('=');
                    string v = eq < 0 ? "" : line.Substring(eq + 1).Trim();
                    if (line.StartsWith("theme=")) dark = v != "light";
                    else if (line.StartsWith("wrap=")) wrap = v != "off";
                    else if (line.StartsWith("statusbar=")) showStatus = v != "off";
                    else if (line.StartsWith("lineendings=")) unixEndings = v == "unix";
                    else if (line.StartsWith("ontop=")) topMost = v == "on";
                    else if (line.StartsWith("autoindent=")) autoIndent = v != "off";
                    else if (line.StartsWith("chrome=")) { Color c; if (TryColor(v, out c)) { uiChrome = c; customPalette = true; } }
                    else if (line.StartsWith("panel=")) { Color c; if (TryColor(v, out c)) { uiPanel = c; customPalette = true; } }
                    else if (line.StartsWith("page=")) { Color c; if (TryColor(v, out c)) { uiPage = c; customPalette = true; } }
                    else if (line.StartsWith("text=")) { Color c; if (TryColor(v, out c)) { uiText = c; customPalette = true; } }
                    else if (line.StartsWith("muted=")) { Color c; if (TryColor(v, out c)) { uiMuted = c; customPalette = true; } }
                    else if (line.StartsWith("border=")) { Color c; if (TryColor(v, out c)) { uiBorder = c; customPalette = true; } }
                    else if (line.StartsWith("accent=")) { Color c; if (TryColor(v, out c)) { uiAccent = c; customPalette = true; } }
                    else if (line.StartsWith("editorbg=")) { Color c; if (TryColor(v, out c)) { editorBack = c; customPalette = true; } }
                    else if (line.StartsWith("editorfg=")) { Color c; if (TryColor(v, out c)) { editorText = c; customPalette = true; } }
                    else if (line.StartsWith("font=")) { string f = v; if (f.Length > 0) editorFontName = f; }
                    else if (line.StartsWith("fontsize=")) { float size; if (Single.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out size) && size >= 5 && size <= 200) editorFontSize = size; }
                    else if (line.StartsWith("tabwidth=")) { int width; if (Int32.TryParse(v, out width)) tabWidth = Math.Max(1, Math.Min(8, width)); }
                    else if (line.StartsWith("softtabs=")) softTabs = v == "on";
                    else if (line.StartsWith("pagemargin=")) { int margin; if (Int32.TryParse(v, out margin)) pageMargin = Math.Max(0, Math.Min(40, margin)); }
                    else if (line.StartsWith("opacity=")) { double opacity; if (Double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out opacity)) windowOpacity = Math.Max(0.5, Math.Min(1.0, opacity)); }
                    else if (line.StartsWith("showmenu=")) showMenu = v != "off";
                    else if (line.StartsWith("showtoolbar=")) showToolbar = v != "off";
                    else if (line.StartsWith("uifont=")) { string f = v; if (f.Length > 0) uiFontName = f; }
                    else if (line.StartsWith("caretcolor=")) { Color c; if (TryColor(v, out c)) caretColor = c; }
                    else if (line.StartsWith("selectioncolor=")) { Color c; if (TryColor(v, out c)) selectionColor = c; }
                    else if (line.StartsWith("dicesides=")) { int n; if (Int32.TryParse(v, out n)) breakSides = Math.Max(2, Math.Min(100, n)); }
                    else if (line.StartsWith("dicecards=")) { int n; if (Int32.TryParse(v, out n)) breakCards = Math.Max(4, Math.Min(50, n)); }
                    else if (line.StartsWith("dicewidth=")) { int n; if (Int32.TryParse(v, out n)) breakPanelWidth = Math.Max(240, Math.Min(520, n)); }
                    else if (line.StartsWith("dicespeed=")) { int n; if (Int32.TryParse(v, out n)) breakRollSpeed = Math.Max(20, Math.Min(200, n)); }
                    else if (line.StartsWith("dicehours=")) breakShowHours = v != "off";
                    else if (line.StartsWith("diceuninstall=")) breakUninstall.Checked = v != "off";
                    else if (line.StartsWith("diceaccent=")) { if (v.Length == 0) breakAccent = Color.Empty; else { Color c; if (TryColor(v, out c)) breakAccent = c; } }
                    else if (line.StartsWith("dicepanel=")) { if (v.Length == 0) breakPanelTint = Color.Empty; else { Color c; if (TryColor(v, out c)) breakPanelTint = c; } }
                    else if (line.StartsWith("dicecard=")) { if (v.Length == 0) breakCardTint = Color.Empty; else { Color c; if (TryColor(v, out c)) breakCardTint = c; } }
                    else if (line.StartsWith("windowx=")) { int n; if (Int32.TryParse(v, out n)) windowX = n; }
                    else if (line.StartsWith("windowy=")) { int n; if (Int32.TryParse(v, out n)) windowY = n; }
                    else if (line.StartsWith("windowwidth=")) { int n; if (Int32.TryParse(v, out n)) windowWidth = n; }
                    else if (line.StartsWith("windowheight=")) { int n; if (Int32.TryParse(v, out n)) windowHeight = n; }
                    else if (line.StartsWith("windowmaximized=")) windowMaximized = v == "on";
                    else if (line.StartsWith("layoutversion=")) { int n; if (Int32.TryParse(v, out n)) layoutVersion = n; }
                }
                if (layoutVersion >= 2 && windowWidth >= 400 && windowHeight >= 300) windowedBounds = new Rectangle(windowX, windowY, windowWidth, windowHeight);
                if (layoutVersion < 2) { windowedBounds = Rectangle.Empty; windowMaximized = false; pageMargin = 16; }
                if (!customPalette) ApplyPalette(dark ? Palettes[0] : Palettes[1]);
                else dark = IsDarkColor(uiPage);
                ApplySettings();
                RestoreWindowBounds();
            }
            catch { }
        }

        // Pushes the remembered view options onto the live controls.
        void ApplySettings()
        {
            if (editor != null) editor.WordWrap = wrap;
            ApplyEditorFont();
            ApplyUiFont();
            UpdateOverlays();
            if (sheet != null) sheet.Padding = new Padding(Ui(pageMargin), Ui(pageMargin + 4), Ui(pageMargin), Ui(pageMargin));
            if (menuBar != null) menuBar.Visible = showMenu;
            if (toolsHost != null) toolsHost.Visible = showToolbar;
            Opacity = Math.Max(0.5, Math.Min(1.0, windowOpacity));
            if (status != null)
            {
                status.Visible = showStatus;
                var shell = status.Parent as TableLayoutPanel;
                if (shell != null)
                {
                    shell.RowStyles[1].Height = showMenu ? Ui(34) : 0;
                    shell.RowStyles[2].Height = showToolbar ? Ui(35) : 0;
                    shell.RowStyles[5].Height = showStatus ? Ui(31) : 0;
                }
            }
            TopMost = topMost;
            if (lineEndingLabel != null)
            {
                lineEndingLabel.Text = unixEndings ? "Unix (LF)" : "Windows (CRLF)";
                tips.SetToolTip(lineEndingLabel, unixEndings ? "Documents are saved with Unix line endings" : "Documents are saved with Windows line endings");
            }
            ApplyDiceSettings();
        }

        ContextMenuStrip NewMenu()
        {
            var m = new ContextMenuStrip { BackColor = uiPanel, ForeColor = uiText, Font = UiFont(9F) };
            m.Renderer = new MenuText(uiPanel, uiBorder, Mix(uiPanel, uiAccent, 0.20), uiText, uiMuted);
            return m;
        }

        void ShowMenu(string which, Control anchor)
        {
            ContextMenuStrip m = NewMenu();
            if (which == "File")
            {Item(m,"New document", (s,e)=>NewDoc(), Keys.Control|Keys.N); Item(m,"New window",(s,e)=>{ try { System.Diagnostics.Process.Start(Application.ExecutablePath); } catch { } }); Item(m,"Open…",OpenFile,Keys.Control|Keys.O);
                var openRecent = new ToolStripMenuItem("Open recent");
                if (recent.Count == 0) openRecent.DropDownItems.Add(new ToolStripMenuItem("(nothing yet)") { Enabled = false });
                else
                {
                    foreach (string r in recent.ToList()) { string target = r; openRecent.DropDownItems.Add(target, null, (s,e)=>OpenPath(target)); }
                    openRecent.DropDownItems.Add(new ToolStripSeparator());
                    openRecent.DropDownItems.Add("Clear list", null, (s,e)=>{recent.Clear();SaveRecent();});
                }
                m.Items.Add(openRecent);
                Item(m,"Save",(s,e)=>SaveDoc(),Keys.Control|Keys.S); Item(m,"Save a copy…",(s,e)=>SaveCopy());
                var saveAs = new ToolStripMenuItem("Save as…"); saveAs.ShortcutKeys = Keys.Control|Keys.Shift|Keys.S; saveAs.Click += (s,e)=>SaveAs(); m.Items.Add(saveAs);
                Item(m,"Download as RTF…",(s,e)=>ExportRtf()); var saveAll = new ToolStripMenuItem("Save all"); saveAll.ShortcutKeys = Keys.Control|Keys.Alt|Keys.S; saveAll.Click += (s,e)=>SaveAll(); m.Items.Add(saveAll); Sep(m);
                Item(m,"Page setup…",(s,e)=>PageSetup()); Item(m,"Print…",(s,e)=>Print(),Keys.Control|Keys.P); Sep(m);
                Item(m,"Version history",(s,e)=>ShowVersions());
                var closeDocItem = new ToolStripMenuItem("Close document"); closeDocItem.ShortcutKeys = Keys.Control|Keys.W; closeDocItem.Click += (s,e)=>CloseDoc(active); m.Items.Add(closeDocItem);
                var reopenDocItem = new ToolStripMenuItem("Reopen closed tab"); reopenDocItem.ShortcutKeys = Keys.Control|Keys.Shift|Keys.T; reopenDocItem.Click += (s,e)=>ReopenClosedTab(); m.Items.Add(reopenDocItem);
            }
            else if (which == "Edit") { Item(m,"Undo",(s,e)=>editor.Undo(),Keys.Control|Keys.Z); Item(m,"Redo",(s,e)=>editor.Redo(),Keys.Control|Keys.Y); Sep(m); Item(m,"Cut",(s,e)=>editor.Cut(),Keys.Control|Keys.X); Item(m,"Copy",(s,e)=>editor.Copy(),Keys.Control|Keys.C); Item(m,"Paste",(s,e)=>editor.Paste(),Keys.Control|Keys.V); Item(m,"Select all",(s,e)=>editor.SelectAll(),Keys.Control|Keys.A); Item(m,"Go to line…",(s,e)=>GoToLine(),Keys.Control|Keys.G); Sep(m); Item(m,"Find…",(s,e)=>FindReplace(false),Keys.Control|Keys.F); Item(m,"Find and replace…",(s,e)=>FindReplace(true),Keys.Control|Keys.H); Sep(m); var findNext = new ToolStripMenuItem("Find next"); findNext.ShortcutKeys = Keys.F3; findNext.Click += (s,e)=>FindNext(true); m.Items.Add(findNext); var findPrev = new ToolStripMenuItem("Find previous"); findPrev.ShortcutKeys = Keys.Shift|Keys.F3; findPrev.Click += (s,e)=>FindNext(false); m.Items.Add(findPrev); }
            else if (which == "View") { Item(m,"Zoom in",(s,e)=>zoom.Value=Math.Min(zoom.Maximum,zoom.Value+10),Keys.Control|Keys.Oemplus); Item(m,"Zoom out",(s,e)=>zoom.Value=Math.Max(zoom.Minimum,zoom.Value-10),Keys.Control|Keys.OemMinus); Item(m,"Reset zoom",(s,e)=>zoom.Value=100,Keys.Control|Keys.D0); Sep(m); Item(m,wrap?"Word wrap ✓":"Word wrap",(s,e)=>{wrap=!wrap;editor.WordWrap=wrap;SaveSettings();}); Item(m,showStatus?"Status bar ✓":"Status bar",(s,e)=>{showStatus=!showStatus;ApplySettings();SaveSettings();}); Item(m,showMenu?"Menu bar ✓":"Menu bar",(s,e)=>{showMenu=!showMenu;ApplySettings();SaveSettings();}); Item(m,showToolbar?"Toolbar ✓":"Toolbar",(s,e)=>{showToolbar=!showToolbar;ApplySettings();SaveSettings();}); Item(m,topMost?"Always on top ✓":"Always on top",(s,e)=>{topMost=!topMost;ApplySettings();SaveSettings();}); Item(m,fullScreen?"Exit full screen":"Full screen",(s,e)=>ToggleFullScreen(),Keys.F11); Item(m,dark?"Light theme":"Dark theme",(s,e)=>ToggleTheme()); Item(m,"Customize…",(s,e)=>ShowCustomize(),Keys.Control|Keys.Oemcomma); Item(m,(Current!=null&&Current.ReadOnly)?"Allow editing":"Read-only mode",(s,e)=>ToggleReadOnly()); }
            else if (which == "Insert") { Item(m,"Link…",(s,e)=>InsertLink(),Keys.Control|Keys.K); Item(m,"Image…",(s,e)=>InsertImage()); Item(m,"Table…",(s,e)=>InsertTable()); Item(m,"Emoji…",(s,e)=>ShowEmojiPicker(),Keys.Control|Keys.E); Item(m,"Time/date",(s,e)=>InsertStamp(),Keys.F5); Item(m,"Date",(s,e)=>editor.SelectedText=DateTime.Now.ToString("MMMM d, yyyy")); Item(m,"Time",(s,e)=>editor.SelectedText=DateTime.Now.ToString("h:mm tt")); Item(m,"Header…",(s,e)=>InsertHeaderFooter(true)); Item(m,"Footer…",(s,e)=>InsertHeaderFooter(false)); Item(m,"Page number",(s,e)=>editor.SelectedText="Page " + Math.Max(1,(int)Math.Ceiling(editor.TextLength/3000.0))); Item(m,"Page break",(s,e)=>InsertPageBreak()); Item(m,"Comment",(s,e)=>AddComment(),Keys.Control|Keys.Alt|Keys.M); }
            else if (which == "Format") { Item(m,"Bold",(s,e)=>ToggleStyle(FontStyle.Bold),Keys.Control|Keys.B); Item(m,"Italic",(s,e)=>ToggleStyle(FontStyle.Italic),Keys.Control|Keys.I); Item(m,"Underline",(s,e)=>ToggleStyle(FontStyle.Underline),Keys.Control|Keys.U); Item(m,"Text color…",(s,e)=>ChooseColor(false)); Item(m,"Highlight color…",(s,e)=>ChooseColor(true)); Item(m,"Align left",(s,e)=>Align(HorizontalAlignment.Left)); Item(m,"Center",(s,e)=>Align(HorizontalAlignment.Center)); Item(m,"Align right",(s,e)=>Align(HorizontalAlignment.Right)); Item(m,"Bulleted list",(s,e)=>ToggleBullets()); Item(m,"Numbered list",(s,e)=>ToggleNumbers()); Sep(m); Item(m,wrap?"Word wrap ✓":"Word wrap",(s,e)=>{wrap=!wrap;editor.WordWrap=wrap;SaveSettings();});                 Item(m,"Search fonts…",(s,e)=>{ fontChooser.Focus(); fontChooser.DroppedDown = true; RefreshFontChoices(editorFontName); }); Item(m,"Import font…",(s,e)=>ImportFont()); Item(m,"Font…",(s,e)=>ChooseFont()); }
            else if (which == "Tools") { Item(m,"Document statistics",(s,e)=>ShowWordCount()); Item(m,"Add comment",(s,e)=>AddComment()); Item(m,"Show comments",(s,e)=>{commentPane.Visible=!commentPane.Visible;if(commentPane.Visible)breakPane.Visible=false;}); Item(m,"Version history snapshot",(s,e)=>SaveVersion()); Item(m,"Clear formatting",(s,e)=>ClearFormatting()); Sep(m); Item(m,"Rename document",(s,e)=>RenameDoc()); Item(m,"Restore last session",(s,e)=>OfferRecovery()); Item(m,"Autosave now",(s,e)=>{SaveSession(); Flash("Session saved");}); Item(m,"Open data folder",(s,e)=>{EnsureDir(AppDataDir); try { System.Diagnostics.Process.Start(AppDataDir); } catch { } });
                Sep(m);
                var textTools = new ToolStripMenuItem("Text tools");
                Sub(textTools,"Sort lines A→Z",(s,e)=>SortLines(false)); Sub(textTools,"Sort lines Z→A",(s,e)=>SortLines(true));
                Sub(textTools,"Trim trailing spaces",(s,e)=>TrimLines()); Sub(textTools,"Remove blank lines",(s,e)=>BlankLines());
                Sub(textTools,"Join lines",(s,e)=>JoinLines()); Sub(textTools,"Duplicate line",(s,e)=>DuplicateLine()); Sub(textTools,"Delete line",(s,e)=>DeleteLines());
                Sub(textTools,"Indent",(s,e)=>IndentLines(false)); Sub(textTools,"Outdent",(s,e)=>IndentLines(true));
                textTools.DropDownItems.Add(new ToolStripSeparator());
                Sub(textTools,"UPPERCASE",(s,e)=>CaseChange("Uppercase", v=>v.ToUpperInvariant())); Sub(textTools,"lowercase",(s,e)=>CaseChange("Lowercase", v=>v.ToLowerInvariant()));
                Sub(textTools,"Title Case",(s,e)=>CaseChange("Title case", v=>ToTitleCase(v))); Sub(textTools,"Sentence case",(s,e)=>CaseChange("Sentence case", v=>ToSentenceCase(v)));
                m.Items.Add(textTools);
                var bookmarkMenu = new ToolStripMenuItem("Bookmarks");
                Sub(bookmarkMenu,"Toggle bookmark",(s,e)=>ToggleBookmark()); Sub(bookmarkMenu,"Next bookmark",(s,e)=>NextBookmark()); Sub(bookmarkMenu,"Clear bookmarks",(s,e)=>ClearBookmarks());
                m.Items.Add(bookmarkMenu);
                Sep(m); Item(m,"Copy as plain text",(s,e)=>CopyPlain(),Keys.Control|Keys.Shift|Keys.C); Item(m,"Export as HTML…",(s,e)=>ExportHtml()); Item(m,"Print preview",(s,e)=>PrintPreview());
                Sep(m); Item(m,"Share document…",(s,e)=>ShowSharePane()); Item(m,"Join a shared document…",(s,e)=>JoinLive());
                var stopItem = new ToolStripMenuItem("Stop sharing"); stopItem.Enabled = live.Hosting || live.Count > 0; stopItem.Click += (s,e)=>StopLive(); m.Items.Add(stopItem); }
            else if (which == "QoL")
            {
                var tabsMenu = new ToolStripMenuItem("Tabs and windows");
                Sub(tabsMenu, "Duplicate current tab", (s,e)=>QolDuplicateTab());
                Sub(tabsMenu, "Move tab left", (s,e)=>QolMoveTab(-1));
                Sub(tabsMenu, "Move tab right", (s,e)=>QolMoveTab(1));
                Sub(tabsMenu, "Copy current file path", (s,e)=>CopyCurrentPath());
                Sub(tabsMenu, "Open containing folder", (s,e)=>OpenCurrentFolder());
                tabsMenu.DropDownItems.Add(new ToolStripSeparator());
                Sub(tabsMenu, "Close other tabs", (s,e)=>CloseOtherDocs(active));
                Sub(tabsMenu, "Close all tabs", (s,e)=>CloseAllDocs());
                Sub(tabsMenu, "New window", (s,e)=>{ try { System.Diagnostics.Process.Start(Application.ExecutablePath); } catch { } });
                m.Items.Add(tabsMenu);

                var cleanMenu = new ToolStripMenuItem("Clean up text");
                Sub(cleanMenu, "Collapse repeated spaces", (s,e)=>QolMap("Spaces collapsed", v=>Regex.Replace(v, @"[ \t]{2,}", " ")));
                Sub(cleanMenu, "Trim leading spaces", (s,e)=>QolTrimLeading());
                Sub(cleanMenu, "Trim trailing spaces", (s,e)=>TrimLines());
                Sub(cleanMenu, "Tabs to spaces", (s,e)=>QolMap("Tabs converted", v=>v.Replace("\t", new string(' ', Math.Max(1, tabWidth)))));
                Sub(cleanMenu, "Spaces to tabs", (s,e)=>QolMap("Spaces converted", v=>v.Replace(new string(' ', Math.Max(1, tabWidth)), "\t")));
                Sub(cleanMenu, "Remove duplicate lines", (s,e)=>QolDedupeLines());
                Sub(cleanMenu, "Remove blank lines", (s,e)=>BlankLines());
                Sub(cleanMenu, "Reverse lines", (s,e)=>QolReverseLines());
                Sub(cleanMenu, "Shuffle lines", (s,e)=>QolShuffleLines());
                Sub(cleanMenu, "Number lines", (s,e)=>QolNumberLines());
                Sub(cleanMenu, "Sort by line length", (s,e)=>QolSortByLength());
                Sub(cleanMenu, "Wrap at 80 columns", (s,e)=>QolMap("Wrapped at 80 columns", v=>QolWrap(v, 80)));
                Sub(cleanMenu, "Unwrap paragraphs", (s,e)=>QolMap("Paragraphs unwrapped", QolUnwrap));
                Sub(cleanMenu, "Straighten quotes", (s,e)=>QolMap("Quotes straightened", QolStraightQuotes));
                m.Items.Add(cleanMenu);

                var caseMenu = new ToolStripMenuItem("Change case");
                Sub(caseMenu, "camelCase", (s,e)=>QolMap("camelCase", QolCamel));
                Sub(caseMenu, "PascalCase", (s,e)=>QolMap("PascalCase", QolPascal));
                Sub(caseMenu, "snake_case", (s,e)=>QolMap("snake_case", v=>QolCase(v, "_", false)));
                Sub(caseMenu, "kebab-case", (s,e)=>QolMap("kebab-case", v=>QolCase(v, "-", false)));
                Sub(caseMenu, "CONSTANT_CASE", (s,e)=>QolMap("CONSTANT_CASE", v=>QolCase(v, "_", true)));
                Sub(caseMenu, "dot.case", (s,e)=>QolMap("dot.case", v=>QolCase(v, ".", false)));
                Sub(caseMenu, "url-slug", (s,e)=>QolMap("url-slug", v=>QolCase(v, "-", false)));
                m.Items.Add(caseMenu);

                var selectMenu = new ToolStripMenuItem("Select");
                Sub(selectMenu, "Select current line", (s,e)=>QolSelectLine());
                Sub(selectMenu, "Select paragraph", (s,e)=>QolSelectParagraph());
                Sub(selectMenu, "Select word", (s,e)=>QolSelectWord());
                m.Items.Add(selectMenu);

                var lineMenu = new ToolStripMenuItem("Lines");
                Sub(lineMenu, "Add bullets", (s,e)=>QolBulletLines());
                Sub(lineMenu, "Quote lines", (s,e)=>QolQuoteLines());
                Sub(lineMenu, "Duplicate line", (s,e)=>DuplicateLine());
                Sub(lineMenu, "Delete line", (s,e)=>DeleteLines());
                Sub(lineMenu, "Indent", (s,e)=>IndentLines(false));
                Sub(lineMenu, "Outdent", (s,e)=>IndentLines(true));
                m.Items.Add(lineMenu);

                var insertMenu = new ToolStripMenuItem("Quick insert");
                Sub(insertMenu, "Horizontal rule", (s,e)=>QolInsert("\r\n────────────────────\r\n"));
                Sub(insertMenu, "Checklist item", (s,e)=>QolInsert("\r\n☐ "));
                Sub(insertMenu, "ISO date (2026-11-17)", (s,e)=>QolInsert(DateTime.Now.ToString("yyyy-MM-dd")));
                Sub(insertMenu, "Long date", (s,e)=>QolInsert(DateTime.Now.ToString("dddd, MMMM d, yyyy")));
                Sub(insertMenu, "24-hour time", (s,e)=>QolInsert(DateTime.Now.ToString("HH:mm")));
                Sub(insertMenu, "UTC timestamp", (s,e)=>QolInsert(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'")));
                Sub(insertMenu, "Current file name", (s,e)=>QolInsert(Current == null ? "" : Current.Name));
                m.Items.Add(insertMenu);

                var viewMenu = new ToolStripMenuItem("Editor");
                Sub(viewMenu, "Zoom 100%", (s,e)=>QolZoom(100));
                Sub(viewMenu, "Zoom 125%", (s,e)=>QolZoom(125));
                Sub(viewMenu, "Zoom 150%", (s,e)=>QolZoom(150));
                Sub(viewMenu, "Zoom 200%", (s,e)=>QolZoom(200));
                viewMenu.DropDownItems.Add(new ToolStripSeparator());
                Sub(viewMenu, autoIndent ? "Auto-indent ✓" : "Auto-indent", (s,e)=>QolToggleAutoIndent());
                Sub(viewMenu, softTabs ? "Spaces for tabs ✓" : "Spaces for tabs", (s,e)=>QolToggleSoftTabs());
                Sub(viewMenu, "Tab width 2", (s,e)=>QolSetTabWidth(2));
                Sub(viewMenu, "Tab width 4", (s,e)=>QolSetTabWidth(4));
                Sub(viewMenu, "Tab width 8", (s,e)=>QolSetTabWidth(8));
                Sub(viewMenu, (Current != null && Current.ReadOnly) ? "Allow editing" : "Read-only mode", (s,e)=>ToggleReadOnly());
                m.Items.Add(viewMenu);
                Sep(m);
                Item(m, "Search fonts…", (s,e)=>{ fontChooser.Focus(); fontChooser.DroppedDown = true; RefreshFontChoices(editorFontName); });
                Item(m, "Document statistics", (s,e)=>ShowWordCount());
            }
            else if (which == "Roll The Dice")
            {
                Item(m, breakPane.Visible ? "Close program shelf" : "Open program shelf", (s,e)=>ToggleBreakPane());
                Item(m, "Draw a new program shelf", (s,e)=>ResetBreakShelf());
                Item(m, "Customize Roll The Dice…", (s,e)=>ShowCustomize(true));
                Item(m, "milkBall", (s,e)=>ShowMilkBall());
                var uninstallToggle = new ToolStripMenuItem("Run the matching uninstaller") { Checked = breakUninstall.Checked };
                uninstallToggle.Click += (s,e)=> { breakUninstall.Checked = !breakUninstall.Checked; SaveSettings(); };
                m.Items.Add(uninstallToggle);
                Sep(m);
                Item(m, "How the shelf works", (s,e)=>MessageBox.Show(this,"Roll The Dice draws a fresh random set of installed applications each time you reset the shelf. Use Customize Roll The Dice to choose how many cards appear, how many sides the die has, the panel width, the roll speed and the shelf colours. Hours come from Windows focus-time records (UserAssist); — means Windows has no record for that app. The matching uninstaller option opens the app's normal Windows uninstaller when a card is erased.", "Roll The Dice", MessageBoxButtons.OK, MessageBoxIcon.Information));
            }
            else if (which == "Themes")
            {
                foreach (UiPalette palette in Palettes)
                {
                    UiPalette selected = palette;
                    var themeItem = new ToolStripMenuItem(selected.Name) { Checked = ThemeMatches(selected) };
                    themeItem.Click += (s,e)=>ApplyPalettePackage(selected);
                    m.Items.Add(themeItem);
                }
                Sep(m);
                Item(m, "Customize…", (s,e)=>ShowCustomize(), Keys.Control|Keys.Oemcomma);
                Item(m, "Open themes folder", (s,e)=>{ try { System.Diagnostics.Process.Start(ThemesFolder()); } catch { } });
            }
            else { Item(m,"User manual",(s,e)=>ShowManual(),Keys.F1); Sep(m); Item(m,"Uninstall Milkpad…",(s,e)=>RunUninstaller()); Item(m,"About Milkpad",(s,e)=>MessageBox.Show(this,"Milkpad is an open-source (MIT) local editor with tabs, formatting and live sessions. Documents stay on this computer; live sessions connect peers directly and use no server.","About Milkpad",MessageBoxButtons.OK,MessageBoxIcon.Information)); }
            m.Show(anchor, new Point(0, anchor.Height));
        }
        void Item(ContextMenuStrip m,string text,EventHandler fn,Keys shortcut=Keys.None){var i=new ToolStripMenuItem(text);if(shortcut!=Keys.None){i.ShortcutKeys=shortcut;i.ShortcutKeyDisplayString=ShortcutLabel(shortcut);}i.Click+=fn;m.Items.Add(i);} void Sep(ContextMenuStrip m){m.Items.Add(new ToolStripSeparator());}
        string ShortcutLabel(Keys key){if(key==(Keys.Control|Keys.Oemplus))return "Ctrl++";if(key==(Keys.Control|Keys.OemMinus))return "Ctrl+-";if(key==(Keys.Control|Keys.Oemcomma))return "Ctrl+,";return null;}
        void Sub(ToolStripMenuItem parent,string text,EventHandler fn){var i=new ToolStripMenuItem(text);i.Click+=fn;parent.DropDownItems.Add(i);}

        void OpenFile(object sender, EventArgs e)
        {
            using (var d = new OpenFileDialog { Filter = "Documents|*.docx;*.rtf;*.txt;*.md|Word document|*.docx|Rich Text Format|*.rtf|Text files|*.txt;*.md|All files|*.*" })
                if (d.ShowDialog(this) == DialogResult.OK) OpenPath(d.FileName);
        }
        bool SaveDoc() { if (Current == null) return true; if (Current.Path.Length == 0) return SaveAs(); return WriteDoc(Current.Path); }
        bool SaveAs()
        {
            // Keep the document's own format instead of always offering Word, which is a
            // surprise when the open file is .txt or .md.
            string currentExt = Current == null || Current.Path.Length == 0 ? "" : Path.GetExtension(Current.Path).ToLowerInvariant();
            int index = currentExt == ".rtf" ? 2 : currentExt == ".txt" ? 3 : currentExt == ".md" ? 4 : 1;
            string stem = Current == null ? "Document" : Path.GetFileNameWithoutExtension(Current.Name);
            string extension = index == 2 ? ".rtf" : index == 3 ? ".txt" : index == 4 ? ".md" : ".docx";
            using (var d = new SaveFileDialog
            {
                Filter = "Word document|*.docx|Rich Text Format|*.rtf|Plain text|*.txt|Markdown|*.md",
                FilterIndex = index,
                FileName = stem + extension,
                AddExtension = true,
                OverwritePrompt = true
            })
                if (d.ShowDialog(this) == DialogResult.OK) return WriteDoc(d.FileName);
            return false;
        }
        // Writes to a temp file then swaps it in, keeping the previous version as .bak.
        // A crash or a full disk can then never leave a half-written document behind.
        bool Commit(string finalPath, string tempPath)
        {
            if (File.Exists(finalPath)) File.Replace(tempPath, finalPath, finalPath + ".bak", true);
            else File.Move(tempPath, finalPath);
            return true;
        }

        bool WriteDoc(string path)
        {
            string temp = path + ".tmp";
            bool lifted = LiftHighlights();
            try
            {
                SaveEditor();
                string ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".rtf") editor.SaveFile(temp, RichTextBoxStreamType.RichText);
                else if (ext == ".docx") WriteDocx(temp);
                else File.WriteAllText(temp, unixEndings ? editor.Text.Replace("\r\n", "\n") : editor.Text, new UTF8Encoding(false));
                Commit(path, temp);
                Current.Path = path; Current.Name = Path.GetFileName(path); Current.Dirty = false; Current.Saved = DateTime.Now;
                SaveMetadata(Current); AddRecent(path); RefreshAll(); return true;
            }
            catch (Exception ex)
            {
                try { if (File.Exists(temp)) File.Delete(temp); } catch { }
                MessageBox.Show(this, ex.Message + "\n\nYour previous version of the file was left untouched.", "Save failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            finally { RestoreHighlights(lifted); }
        }
        //==== DOCX import: WordprocessingML -> RTF so the editor keeps formatting ====
        string ReadDocxRtf(string path)
        {
            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            XDocument xml;
            using (var archive = ZipFile.OpenRead(path))
            {
                var entry = archive.GetEntry("word/document.xml");
                if (entry == null) throw new InvalidDataException("This DOCX file has no document.xml part.");
                using (var stream = entry.Open()) xml = XDocument.Load(stream);
            }

            var fonts = new List<string>();
            var colors = new List<Color>();
            var body = new StringBuilder();

            foreach (var p in xml.Descendants(w + "p"))
            {
                XElement pPr = p.Element(w + "pPr");
                string align = "\\ql";
                int leftTwips = 0;
                bool bullet = false;
                if (pPr != null)
                {
                    XElement jc = pPr.Element(w + "jc");
                    string jcValue = jc == null ? "" : (string)jc.Attribute(w + "val");
                    if (jcValue == "center") align = "\\qc"; else if (jcValue == "right") align = "\\qr"; else if (jcValue == "both") align = "\\qj";
                    XElement ind = pPr.Element(w + "ind");
                    if (ind != null) int.TryParse((string)ind.Attribute(w + "left"), NumberStyles.Any, CultureInfo.InvariantCulture, out leftTwips);
                    bullet = pPr.Element(w + "numPr") != null;
                }

                body.Append("\\pard").Append(align);
                if (leftTwips > 0) body.Append("\\li").Append(leftTwips / 15);
                if (bullet) body.Append("\\bullet");

                foreach (var r in p.Descendants(w + "r")) AppendDocxRun(body, r, w, fonts, colors);
                body.Append("\\par\r\n");
            }

            if (fonts.Count == 0) fonts.Add("Arial");
            var rtf = new StringBuilder();
            rtf.Append("{\\rtf1\\ansi\\deff0");
            rtf.Append("{\\fonttbl");
            for (int i = 0; i < fonts.Count; i++) rtf.Append("{\\f").Append(i).Append(' ').Append(fonts[i]).Append(";}");
            rtf.Append('}');
            rtf.Append("{\\colortbl;");
            foreach (Color c in colors) rtf.Append("\\red").Append(c.R).Append("\\green").Append(c.G).Append("\\blue").Append(c.B).Append(';');
            rtf.Append('}');
            rtf.Append(' ').Append(body);
            rtf.Append('}');
            return rtf.ToString();
        }

        void AppendDocxRun(StringBuilder body, XElement r, XNamespace w, List<string> fonts, List<Color> colors)
        {
            var text = new StringBuilder();
            foreach (var node in r.Elements())
            {
                if (node.Name == w + "t") text.Append(node.Value);
                else if (node.Name == w + "tab") text.Append('\t');
                else if (node.Name == w + "br") text.Append('\n');
            }
            if (text.Length == 0) return;

            string font = "Arial";
            float size = 11f;
            bool bold = false, italic = false, underline = false, strike = false;
            Color color = Color.Black, back = Color.White;
            XElement rPr = r.Element(w + "rPr");
            if (rPr != null)
            {
                XElement rf = rPr.Element(w + "rFonts");
                if (rf != null && (string)rf.Attribute(w + "ascii") != null) font = (string)rf.Attribute(w + "ascii");
                XElement sz = rPr.Element(w + "sz");
                if (sz != null) { float half; if (float.TryParse((string)sz.Attribute(w + "val"), NumberStyles.Any, CultureInfo.InvariantCulture, out half) && half > 0) size = half / 2f; }
                bold = OnFlag(rPr, w, "b"); italic = OnFlag(rPr, w, "i"); strike = OnFlag(rPr, w, "strike");
                XElement u = rPr.Element(w + "u"); underline = u != null && (string)u.Attribute(w + "val") != "none";
                XElement c = rPr.Element(w + "color"); if (c != null) color = ParseHexColor((string)c.Attribute(w + "val"), Color.Black);
                XElement hl = rPr.Element(w + "highlight"); if (hl != null) back = HighlightColor((string)hl.Attribute(w + "val"));
                XElement shd = rPr.Element(w + "shd");
                if (shd != null) { string fill = (string)shd.Attribute(w + "fill"); if (!String.IsNullOrEmpty(fill) && fill != "auto") back = ParseHexColor(fill, back); }
            }

            int fontIndex = fonts.IndexOf(font);
            if (fontIndex < 0) { fonts.Add(font); fontIndex = fonts.Count - 1; }
            int colorIndex = 0, backIndex = 0;
            if (color.ToArgb() != Color.Black.ToArgb()) { colorIndex = colors.IndexOf(color); if (colorIndex < 0) { colors.Add(color); colorIndex = colors.Count - 1; } colorIndex += 1; }
            if (back.ToArgb() != Color.White.ToArgb()) { backIndex = colors.IndexOf(back); if (backIndex < 0) { colors.Add(back); backIndex = colors.Count - 1; } backIndex += 1; }

            body.Append("\\f").Append(fontIndex).Append("\\fs").Append((int)Math.Round(size * 2));
            body.Append(bold ? "\\b" : "\\b0").Append(italic ? "\\i" : "\\i0").Append(underline ? "\\ul" : "\\ulnone").Append(strike ? "\\strike" : "\\strike0");
            body.Append("\\cf").Append(colorIndex).Append("\\highlight").Append(backIndex);
            body.Append(' ').Append(RtfEscape(text.ToString()));
        }

        static bool OnFlag(XElement props, XNamespace w, string name)
        {
            if (props == null) return false;
            XElement element = props.Element(w + name);
            if (element == null) return false;
            string value = (string)element.Attribute(w + "val");
            return value == null || (value != "0" && value != "false" && value != "off");
        }

        static Color ParseHexColor(string hex, Color fallback)
        {
            if (String.IsNullOrEmpty(hex) || hex == "auto") return fallback;
            try { int value = Convert.ToInt32(hex, 16); return Color.FromArgb((value >> 16) & 0xFF, (value >> 8) & 0xFF, value & 0xFF); }
            catch { return fallback; }
        }

        static Color HighlightColor(string name)
        {
            switch ((name ?? "").ToLowerInvariant())
            {
                case "yellow": return Color.Yellow;
                case "green": return Color.Lime;
                case "cyan": return Color.Cyan;
                case "magenta": return Color.Magenta;
                case "blue": return Color.Blue;
                case "red": return Color.Red;
                case "darkblue": return Color.DarkBlue;
                case "darkcyan": return Color.DarkCyan;
                case "darkgreen": return Color.DarkGreen;
                case "darkmagenta": return Color.DarkMagenta;
                case "darkred": return Color.DarkRed;
                case "darkyellow": return Color.Olive;
                case "gray": return Color.Gray;
                case "grey": return Color.Gray;
                case "lightgray": return Color.LightGray;
                case "black": return Color.Black;
                case "white": return Color.White;
                default: return Color.Yellow;
            }
        }

        static string RtfEscape(string value)
        {
            var sb = new StringBuilder();
            foreach (char ch in value)
            {
                if (ch == '\\' || ch == '{' || ch == '}') sb.Append('\\').Append(ch);
                else if (ch == '\t') sb.Append("\\tab ");
                else if (ch == '\n' || ch == '\r') sb.Append("\\line ");
                else if (ch >= 128) { int code = ch > 32767 ? ch - 65536 : ch; sb.Append("\\u").Append(code).Append('?'); }
                else sb.Append(ch);
            }
            return sb.ToString();
        }
        //==== DOCX export: RichTextBox formatting -> WordprocessingML ====
        const string ContentTypesXml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/><Override PartName=\"/word/numbering.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.numbering+xml\"/></Types>";
        const string PackageRelsXml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/></Relationships>";
        const string DocumentRelsXml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/numbering\" Target=\"numbering.xml\"/></Relationships>";
        const string NumberingXml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><w:numbering xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:abstractNum w:abstractNumId=\"0\"><w:multiLevelType w:val=\"singleLevel\"/><w:lvl w:ilvl=\"0\"><w:start w:val=\"1\"/><w:numFmt w:val=\"bullet\"/><w:lvlText w:val=\"\u2022\"/><w:lvlJc w:val=\"left\"/><w:pPr><w:ind w:left=\"720\" w:hanging=\"360\"/></w:pPr><w:rPr><w:rFonts w:ascii=\"Symbol\" w:hAnsi=\"Symbol\"/></w:rPr></w:lvl></w:abstractNum><w:num w:numId=\"1\"><w:abstractNumId w:val=\"0\"/></w:num></w:numbering>";

        //==== DOCX pictures: RichEdit keeps pictures in the RTF stream, not in Text ====
        class RtfPicture
        {
            public int Offset;
            public byte[] Data;
            public string Extension;
            public string ContentType;
            public int PixelWidth;
            public int PixelHeight;
            public int GoalWidth;
            public int GoalHeight;
        }

        class DocxImage
        {
            public string FileName;
            public string Extension;
            public string ContentType;
            public string RelationshipId;
            public byte[] Data;
            public long Cx;
            public long Cy;
        }

        void WriteDocx(string path)
        {
            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            XNamespace r = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            XNamespace wp = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";
            XNamespace a = "http://schemas.openxmlformats.org/drawingml/2006/main";
            XNamespace pic = "http://schemas.openxmlformats.org/drawingml/2006/picture";

            string text = editor.Text;
            int length = text.Length;
            var pictures = new List<RtfPicture>();
            string rtfText = "";
            try { rtfText = RtfPlainText(editor.Rtf, pictures); }
            catch { pictures.Clear(); }
            // RichEdit writes a final \par that RichTextBox.Text does not expose. Removing that
            // paragraph mark makes the two strings comparable without shifting any earlier offset.
            while (rtfText.Length > text.Length && rtfText.EndsWith("\n")) rtfText = rtfText.Substring(0, rtfText.Length - 1);
            // Pictures occupy one space each in RichTextBox.Text. If the RTF walk agrees with
            // the control's text, those offsets are exact and can be replaced by drawings.
            bool placePictures = pictures.Count > 0 && rtfText == text;
            var pictureAt = new Dictionary<int, RtfPicture>();
            if (placePictures)
                foreach (RtfPicture picture in pictures)
                    if (!pictureAt.ContainsKey(picture.Offset)) pictureAt[picture.Offset] = picture;

            var body = new XElement(w + "body");
            var images = new List<DocxImage>();
            int savedStart = editor.SelectionStart, savedLength = editor.SelectionLength;
            bool bulkWas = bulk;
            bulk = true;
            try
            {
                int i = 0;
                while (i < length)
                {
                    var para = new XElement(w + "p");
                    editor.Select(i, 1);
                    var pPr = new XElement(w + "pPr");
                    if (editor.SelectionBullet)
                        pPr.Add(new XElement(w + "numPr", new XElement(w + "ilvl", new XAttribute(w + "val", "0")), new XElement(w + "numId", new XAttribute(w + "val", "1"))));
                    if (editor.SelectionIndent > 0)
                        pPr.Add(new XElement(w + "ind", new XAttribute(w + "left", (editor.SelectionIndent * 15).ToString(CultureInfo.InvariantCulture))));
                    if (editor.SelectionAlignment == HorizontalAlignment.Center) pPr.Add(new XElement(w + "jc", new XAttribute(w + "val", "center")));
                    else if (editor.SelectionAlignment == HorizontalAlignment.Right) pPr.Add(new XElement(w + "jc", new XAttribute(w + "val", "right")));
                    if (pPr.HasElements) para.Add(pPr);

                    int runStart = i;
                    string runKey = null;
                    while (i < length && text[i] != '\n')
                    {
                        RtfPicture picture;
                        if (placePictures && pictureAt.TryGetValue(i, out picture))
                        {
                            if (i > runStart) para.Add(BuildRun(w, text, runStart, i));
                            DocxImage embedded = PrepareDocxImage(picture, images.Count + 1);
                            if (embedded != null)
                            {
                                images.Add(embedded);
                                para.Add(BuildImageRun(w, r, wp, a, pic, embedded, images.Count));
                            }
                            i++;
                            runStart = i;
                            runKey = null;
                            continue;
                        }
                        editor.Select(i, 1);
                        Font f = editor.SelectionFont ?? editor.Font;
                        string key = RunKey(f, editor.SelectionColor, editor.SelectionBackColor);
                        if (runKey == null) { runKey = key; runStart = i; }
                        else if (key != runKey) { para.Add(BuildRun(w, text, runStart, i)); runKey = key; runStart = i; }
                        i++;
                    }
                    if (i > runStart) para.Add(BuildRun(w, text, runStart, i));
                    body.Add(para);
                    if (i < length) i++;
                }
                if (length == 0) body.Add(new XElement(w + "p"));
            }
            finally
            {
                bulk = bulkWas;
                int safeStart = Math.Min(savedStart, editor.TextLength);
                editor.Select(safeStart, Math.Max(0, Math.Min(savedLength, editor.TextLength - safeStart)));
            }

            body.Add(new XElement(w + "sectPr", new XElement(w + "pgSz", new XAttribute(w + "w", "12240"), new XAttribute(w + "h", "15840")), new XElement(w + "pgMar", new XAttribute(w + "top", "1440"), new XAttribute(w + "right", "1440"), new XAttribute(w + "bottom", "1440"), new XAttribute(w + "left", "1440"))));
            var document = new XDocument(new XDeclaration("1.0", "UTF-8", "yes"),
                new XElement(w + "document",
                    new XAttribute(XNamespace.Xmlns + "w", w),
                    new XAttribute(XNamespace.Xmlns + "r", r),
                    new XAttribute(XNamespace.Xmlns + "wp", wp),
                    new XAttribute(XNamespace.Xmlns + "a", a),
                    new XAttribute(XNamespace.Xmlns + "pic", pic),
                    body));

            string contentTypes = ContentTypesXml;
            string relationships = DocumentRelsXml;
            if (images.Count > 0)
            {
                var defaults = new StringBuilder();
                var rels = new StringBuilder();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (DocxImage image in images)
                {
                    if (seen.Add(image.Extension))
                        defaults.Append("<Default Extension=\"").Append(image.Extension).Append("\" ContentType=\"").Append(image.ContentType).Append("\"/>");
                    rels.Append("<Relationship Id=\"").Append(image.RelationshipId).Append("\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/image\" Target=\"media/").Append(image.FileName).Append("\"/>");
                }
                // Defaults must precede Overrides in [Content_Types].xml.
                int overrideAt = contentTypes.IndexOf("<Override", StringComparison.Ordinal);
                if (overrideAt < 0) overrideAt = contentTypes.IndexOf("</Types>", StringComparison.Ordinal);
                contentTypes = contentTypes.Insert(overrideAt, defaults.ToString());
                relationships = relationships.Replace("</Relationships>", rels.ToString() + "</Relationships>");
            }

            using (var stream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                AddZipText(archive, "[Content_Types].xml", contentTypes);
                AddZipText(archive, "_rels/.rels", PackageRelsXml);
                AddZipText(archive, "word/_rels/document.xml.rels", relationships);
                AddZipText(archive, "word/numbering.xml", NumberingXml);
                AddZipText(archive, "word/document.xml", document.ToString(SaveOptions.DisableFormatting));
                foreach (DocxImage image in images)
                    AddZipBytes(archive, "word/media/" + image.FileName, image.Data);
            }
        }

        // Walks the RTF and returns the same text RichEdit displays. A picture contributes
        // one space, matching RichTextBox.Text, and its offset in that text is recorded.
        static string RtfPlainText(string rtf, List<RtfPicture> pictures)
        {
            if (String.IsNullOrEmpty(rtf)) return "";
            var text = new StringBuilder();
            var groups = new Stack<int>();
            var codes = new Stack<int>();
            int i = 0, depth = 0, skipDepth = 0, uc = 1, fallback = 0;
            while (i < rtf.Length)
            {
                char c = rtf[i];
                if (c == '{')
                {
                    depth++;
                    groups.Push(i);
                    codes.Push(uc);
                    i++;
                    if (i < rtf.Length && rtf[i] == '\\')
                    {
                        if (i + 1 < rtf.Length && rtf[i + 1] == '*')
                        {
                            string destination = RtfControlName(rtf, i + 2);
                            // shppict holds the picture; nonshppict is Word's duplicate fallback.
                            if (destination != "shppict" && destination.Length > 0)
                                skipDepth = depth;
                        }
                        else if (IsRtfDestination(RtfControlName(rtf, i + 1)))
                            skipDepth = depth;
                    }
                    continue;
                }
                if (c == '}')
                {
                    if (skipDepth == depth) skipDepth = 0;
                    if (codes.Count > 0) uc = codes.Pop();
                    if (groups.Count > 0) groups.Pop();
                    if (depth > 0) depth--;
                    i++;
                    continue;
                }
                if (skipDepth != 0)
                {
                    i = c == '\\' ? SkipRtfControl(rtf, i) : i + 1;
                    continue;
                }
                if (c == '\\')
                {
                    int slash = i;
                    i++;
                    if (i >= rtf.Length) break;
                    char symbol = rtf[i];
                    if (!Char.IsLetter(symbol))
                    {
                        if (symbol == '\'')
                        {
                            if (i + 2 < rtf.Length)
                            {
                                int value;
                                if (Int32.TryParse(rtf.Substring(i + 1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value))
                                {
                                    if (fallback > 0) fallback--;
                                    else text.Append((char)value);
                                }
                                i += 3;
                            }
                            else i++;
                        }
                        else
                        {
                            if (fallback > 0) fallback--;
                            else if (symbol == '\\' || symbol == '{' || symbol == '}') text.Append(symbol);
                            else if (symbol == '~') text.Append(' ');
                            else if (symbol == '_') text.Append('-');
                            i++;
                        }
                        continue;
                    }

                    int nameStart = i;
                    while (i < rtf.Length && Char.IsLetter(rtf[i])) i++;
                    string name = rtf.Substring(nameStart, i - nameStart).ToLowerInvariant();
                    int num = 0, sign = 1;
                    bool hasNum = false;
                    if (i < rtf.Length && rtf[i] == '-') { sign = -1; i++; }
                    while (i < rtf.Length && Char.IsDigit(rtf[i])) { num = num * 10 + (rtf[i] - '0'); hasNum = true; i++; }
                    num *= sign;
                    if (i < rtf.Length && rtf[i] == ' ') i++;

                    if (fallback > 0 && name != "u") { fallback--; continue; }
                    if (name == "u" && hasNum)
                    {
                        int code = num < 0 ? num + 65536 : num;
                        text.Append((char)code);
                        fallback = uc;
                        continue;
                    }
                    if (name == "uc" && hasNum) { uc = Math.Max(0, num); continue; }
                    if (name == "par" || name == "line" || name == "row" || name == "nestrow") { text.Append('\n'); continue; }
                    if (name == "tab" || name == "cell" || name == "nestcell") { text.Append('\t'); continue; }
                    if (name == "emdash") { text.Append('\u2014'); continue; }
                    if (name == "endash") { text.Append('\u2013'); continue; }
                    if (name == "bullet") { text.Append('\u2022'); continue; }
                    if (name == "lquote") { text.Append('\u2018'); continue; }
                    if (name == "rquote") { text.Append('\u2019'); continue; }
                    if (name == "ldblquote") { text.Append('\u201C'); continue; }
                    if (name == "rdblquote") { text.Append('\u201D'); continue; }
                    if (name == "pict")
                    {
                        int groupStart = groups.Count > 0 ? groups.Peek() : slash;
                        int close = FindRtfGroupEnd(rtf, groupStart);
                        if (close > slash)
                        {
                            RtfPicture picture = ParseRtfPicture(rtf.Substring(groupStart, close - groupStart + 1));
                            if (picture != null && picture.Data.Length > 0)
                            {
                                text.Append(' ');
                                picture.Offset = text.Length - 1;
                                pictures.Add(picture);
                            }
                            i = close + 1;
                            if (skipDepth == depth) skipDepth = 0;
                            if (codes.Count > 0) uc = codes.Pop();
                            if (groups.Count > 0) groups.Pop();
                            if (depth > 0) depth--;
                        }
                        continue;
                    }
                    continue;
                }
                if (c == '\r' || c == '\n') { i++; continue; }
                if (fallback > 0) { fallback--; i++; continue; }
                text.Append(c);
                i++;
            }
            return text.ToString();
        }

        static string RtfControlName(string rtf, int at)
        {
            int start = at;
            while (at < rtf.Length && Char.IsLetter(rtf[at])) at++;
            return rtf.Substring(start, at - start).ToLowerInvariant();
        }

        static bool IsRtfDestination(string name)
        {
            switch (name)
            {
                case "fonttbl": case "colortbl": case "stylesheet": case "info":
                case "header": case "footer": case "headerl": case "headerr":
                case "footerl": case "footerr": case "object": case "listtable":
                case "listoverridetable": case "rsidtbl": case "generator":
                case "themedata": case "colorschememapping": case "datastore":
                case "latentstyles": case "xmlnstbl": case "filetbl": case "revtbl":
                case "fldinst": case "bkmkstart": case "bkmkend": case "nonshppict":
                    return true;
                default:
                    return false;
            }
        }

        static int SkipRtfControl(string rtf, int at)
        {
            at++;
            if (at >= rtf.Length) return at;
            if (!Char.IsLetter(rtf[at]))
            {
                if (rtf[at] == '\'') return Math.Min(rtf.Length, at + 3);
                return at + 1;
            }
            while (at < rtf.Length && Char.IsLetter(rtf[at])) at++;
            if (at < rtf.Length && rtf[at] == '-') at++;
            while (at < rtf.Length && Char.IsDigit(rtf[at])) at++;
            if (at < rtf.Length && rtf[at] == ' ') at++;
            return at;
        }

        static int FindRtfGroupEnd(string rtf, int open)
        {
            int depth = 0;
            for (int i = open; i < rtf.Length; i++)
            {
                if (rtf[i] == '\\')
                {
                    i++;
                    if (i < rtf.Length && rtf[i] == '\'') i += 2;
                    continue;
                }
                if (rtf[i] == '{') depth++;
                else if (rtf[i] == '}')
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }
            return rtf.Length - 1;
        }

        static RtfPicture ParseRtfPicture(string group)
        {
            Match match = Regex.Match(group, @"\\(pngblip|jpegblip|emfblip|wmetafile\d+|dibitmap\d*|wbitmap\d*)", RegexOptions.IgnoreCase);
            if (!match.Success) return null;
            string kind = match.Groups[1].Value.ToLowerInvariant();
            int at = SkipRtfPictureControls(group, match.Index + match.Length);
            int close = group.LastIndexOf('}');
            if (close <= at) return null;
            string hex = Regex.Replace(group.Substring(at, close - at), @"[^0-9a-fA-F]", "");
            if (hex.Length < 2) return null;
            if ((hex.Length & 1) != 0) hex = hex.Substring(0, hex.Length - 1);
            byte[] data = new byte[hex.Length / 2];
            for (int i = 0; i < data.Length; i++)
            {
                int value;
                if (!Int32.TryParse(hex.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value)) return null;
                data[i] = (byte)value;
            }

            var picture = new RtfPicture
            {
                Data = data,
                GoalWidth = RtfInt(group, "picwgoal"),
                GoalHeight = RtfInt(group, "pichgoal"),
                PixelWidth = RtfInt(group, "picw"),
                PixelHeight = RtfInt(group, "pich")
            };
            if (kind.StartsWith("png")) { picture.Extension = "png"; picture.ContentType = "image/png"; }
            else if (kind.StartsWith("jpeg")) { picture.Extension = "jpg"; picture.ContentType = "image/jpeg"; }
            else if (kind.StartsWith("dibitmap") || kind.StartsWith("wbitmap")) { picture.Extension = "dib"; picture.ContentType = ""; }
            else if (kind.StartsWith("wmetafile")) { picture.Extension = "wmf"; picture.ContentType = "image/x-wmf"; }
            else if (kind.StartsWith("emfblip")) { picture.Extension = "emf"; picture.ContentType = "image/x-emf"; }
            else return null;
            return picture;
        }

        static int SkipRtfPictureControls(string group, int at)
        {
            while (at < group.Length)
            {
                while (at < group.Length && Char.IsWhiteSpace(group[at])) at++;
                if (at >= group.Length) break;
                if (group[at] == '{')
                {
                    at = FindRtfGroupEnd(group, at) + 1;
                    continue;
                }
                if (group[at] != '\\') break;
                at++;
                if (at >= group.Length) break;
                if (!Char.IsLetter(group[at]))
                {
                    if (group[at] == '\'') at = Math.Min(group.Length, at + 3);
                    else at++;
                    continue;
                }
                int nameStart = at;
                while (at < group.Length && Char.IsLetter(group[at])) at++;
                string name = group.Substring(nameStart, at - nameStart).ToLowerInvariant();
                if (name == "blipuid")
                {
                    while (at < group.Length && !Char.IsWhiteSpace(group[at])) at++;
                    continue;
                }
                if (name == "bin")
                {
                    int count = 0;
                    while (at < group.Length && Char.IsDigit(group[at])) { count = count * 10 + (group[at] - '0'); at++; }
                    if (at < group.Length && group[at] == ' ') at++;
                    at = Math.Min(group.Length, at + count);
                    continue;
                }
                if (at < group.Length && group[at] == '-') at++;
                while (at < group.Length && Char.IsDigit(group[at])) at++;
                if (at < group.Length && group[at] == ' ') at++;
            }
            return at;
        }

        static int RtfInt(string group, string name)
        {
            Match match = Regex.Match(group, @"\\" + name + @"(-?\d+)");
            int value;
            return match.Success && Int32.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? value : 0;
        }

        static DocxImage PrepareDocxImage(RtfPicture picture, int number)
        {
            try
            {
                byte[] data = picture.Data;
                string extension = picture.Extension;
                string contentType = picture.ContentType;
                int pixelWidth = picture.PixelWidth, pixelHeight = picture.PixelHeight;
                if (extension == "dib")
                {
                    data = DibToPng(data);
                    extension = "png";
                    contentType = "image/png";
                    using (var stream = new MemoryStream(data))
                    using (var image = Image.FromStream(stream)) { pixelWidth = image.Width; pixelHeight = image.Height; }
                }
                else if (extension == "wmf" || extension == "emf")
                {
                    data = RasterizeToPng(picture.Data, picture.GoalWidth, picture.GoalHeight, out pixelWidth, out pixelHeight);
                    extension = "png";
                    contentType = "image/png";
                }
                if (data == null || data.Length == 0) return null;
                if (pixelWidth <= 0 || pixelHeight <= 0)
                {
                    using (var stream = new MemoryStream(data))
                    using (var image = Image.FromStream(stream)) { pixelWidth = image.Width; pixelHeight = image.Height; }
                }
                long cx = picture.GoalWidth > 0 ? picture.GoalWidth * 635L : Math.Max(1, pixelWidth) * 9525L;
                long cy = picture.GoalHeight > 0 ? picture.GoalHeight * 635L : Math.Max(1, pixelHeight) * 9525L;
                if (cx < 1) cx = 9525;
                if (cy < 1) cy = 9525;
                return new DocxImage
                {
                    Data = data,
                    Extension = extension,
                    ContentType = contentType,
                    FileName = "image" + number.ToString(CultureInfo.InvariantCulture) + "." + extension,
                    RelationshipId = "rIdImg" + number.ToString(CultureInfo.InvariantCulture),
                    Cx = cx,
                    Cy = cy
                };
            }
            catch { return null; }
        }

        // GDI+ cannot load the raw DIB stored by an RTF \dibitmap group, so wrap it in a
        // BITMAPFILEHEADER and let GDI+ do the decoding before writing a real PNG.
        static byte[] DibToPng(byte[] dib)
        {
            if (dib == null || dib.Length < 40) return null;
            int headerSize = BitConverter.ToInt32(dib, 0);
            int bits = BitConverter.ToInt16(dib, 14);
            int colors = BitConverter.ToInt32(dib, 32);
            int palette = bits <= 8 ? (colors > 0 ? colors : 1 << Math.Max(0, bits)) * 4 : 0;
            int offset = 14 + headerSize + palette;
            if (offset < 54 || offset > 14 + dib.Length) offset = 54;
            var file = new byte[14 + dib.Length];
            file[0] = (byte)'B'; file[1] = (byte)'M';
            PutInt32(file, 2, file.Length);
            PutInt32(file, 10, offset);
            Buffer.BlockCopy(dib, 0, file, 14, dib.Length);
            using (var input = new MemoryStream(file))
            using (var image = Image.FromStream(input))
            using (var bitmap = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb))
            {
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(Color.White);
                    graphics.DrawImage(image, 0, 0, bitmap.Width, bitmap.Height);
                }
                using (var output = new MemoryStream())
                {
                    bitmap.Save(output, ImageFormat.Png);
                    return output.ToArray();
                }
            }
        }

        static void PutInt32(byte[] buffer, int at, int value)
        {
            buffer[at] = (byte)value; buffer[at + 1] = (byte)(value >> 8);
            buffer[at + 2] = (byte)(value >> 16); buffer[at + 3] = (byte)(value >> 24);
        }

        // RichEdit stores an inserted DIB as a WMF. Rasterising it at 192 dpi keeps small
        // document images sharp while producing a PNG that every Word version can display.
        static byte[] RasterizeToPng(byte[] data, int goalWidth, int goalHeight, out int pixelWidth, out int pixelHeight)
        {
            pixelWidth = 0; pixelHeight = 0;
            if (data == null || data.Length == 0) return null;
            using (var input = new MemoryStream(data))
            using (var image = Image.FromStream(input))
            {
                pixelWidth = goalWidth > 0 ? Math.Max(1, (int)Math.Round(goalWidth * 192.0 / 1440.0)) : Math.Max(1, image.Width);
                pixelHeight = goalHeight > 0 ? Math.Max(1, (int)Math.Round(goalHeight * 192.0 / 1440.0)) : Math.Max(1, image.Height);
                pixelWidth = Math.Min(pixelWidth, 4000);
                pixelHeight = Math.Min(pixelHeight, 4000);
                using (var bitmap = new Bitmap(pixelWidth, pixelHeight, PixelFormat.Format32bppArgb))
                {
                    using (var graphics = Graphics.FromImage(bitmap))
                    using (var attributes = new ImageAttributes())
                    {
                        graphics.Clear(Color.White);
                        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        // Extend the target by one pixel and let the bitmap clip it: scaling a
                        // metafile otherwise leaves a white seam on the right and bottom edges.
                        attributes.SetWrapMode(System.Drawing.Drawing2D.WrapMode.TileFlipXY);
                        graphics.DrawImage(image, new Rectangle(0, 0, pixelWidth + 1, pixelHeight + 1), 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
                    }
                    using (var output = new MemoryStream())
                    {
                        bitmap.Save(output, ImageFormat.Png);
                        return output.ToArray();
                    }
                }
            }
        }

        static XElement BuildImageRun(XNamespace w, XNamespace r, XNamespace wp, XNamespace a, XNamespace pic, DocxImage image, int id)
        {
            string cx = image.Cx.ToString(CultureInfo.InvariantCulture);
            string cy = image.Cy.ToString(CultureInfo.InvariantCulture);
            string name = "Picture " + id.ToString(CultureInfo.InvariantCulture);
            var blip = new XElement(a + "blip", new XAttribute(r + "embed", image.RelationshipId));
            var blipFill = new XElement(pic + "blipFill", blip, new XElement(a + "stretch", new XElement(a + "fillRect")));
            var transform = new XElement(a + "xfrm",
                new XElement(a + "off", new XAttribute("x", "0"), new XAttribute("y", "0")),
                new XElement(a + "ext", new XAttribute("cx", cx), new XAttribute("cy", cy)));
            var shape = new XElement(pic + "spPr", transform,
                new XElement(a + "prstGeom", new XAttribute("prst", "rect"), new XElement(a + "avLst")));
            var picture = new XElement(pic + "pic",
                new XElement(pic + "nvPicPr",
                    new XElement(pic + "cNvPr", new XAttribute("id", id), new XAttribute("name", image.FileName)),
                    new XElement(pic + "cNvPicPr")),
                blipFill, shape);
            var graphic = new XElement(a + "graphic",
                new XElement(a + "graphicData", new XAttribute("uri", "http://schemas.openxmlformats.org/drawingml/2006/picture"), picture));
            var inline = new XElement(wp + "inline",
                new XAttribute("distT", "0"), new XAttribute("distB", "0"),
                new XAttribute("distL", "0"), new XAttribute("distR", "0"),
                new XElement(wp + "extent", new XAttribute("cx", cx), new XAttribute("cy", cy)),
                new XElement(wp + "docPr", new XAttribute("id", id), new XAttribute("name", name)),
                new XElement(wp + "cNvGraphicFramePr", new XElement(a + "graphicFrameLocks", new XAttribute("noChangeAspect", "1"))),
                graphic);
            return new XElement(w + "r", new XElement(w + "drawing", inline));
        }

        void AddZipBytes(ZipArchive archive, string name, byte[] data)
        {
            var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
            using (var stream = entry.Open()) stream.Write(data, 0, data.Length);
        }

        static string RunKey(Font f, Color color, Color back)
        {
            return f.Name + "|" + f.SizeInPoints.ToString("R", CultureInfo.InvariantCulture) + "|" + (int)f.Style + "|" + color.ToArgb() + "|" + back.ToArgb();
        }

        XElement BuildRun(XNamespace w, string text, int start, int end)
        {
            editor.Select(start, 1);
            Font f = editor.SelectionFont ?? editor.Font;
            Color color = editor.SelectionColor;
            Color back = editor.SelectionBackColor;

            var rPr = new XElement(w + "rPr");
            rPr.Add(new XElement(w + "rFonts", new XAttribute(w + "ascii", f.Name), new XAttribute(w + "hAnsi", f.Name)));
            rPr.Add(new XElement(w + "sz", new XAttribute(w + "val", ((int)Math.Round(f.SizeInPoints * 2)).ToString(CultureInfo.InvariantCulture))));
            if ((f.Style & FontStyle.Bold) != 0) rPr.Add(new XElement(w + "b"));
            if ((f.Style & FontStyle.Italic) != 0) rPr.Add(new XElement(w + "i"));
            if ((f.Style & FontStyle.Underline) != 0) rPr.Add(new XElement(w + "u", new XAttribute(w + "val", "single")));
            if ((f.Style & FontStyle.Strikeout) != 0) rPr.Add(new XElement(w + "strike"));
            if (color.ToArgb() != Color.Black.ToArgb()) rPr.Add(new XElement(w + "color", new XAttribute(w + "val", HexOf(color))));
            if (back.ToArgb() != Color.White.ToArgb())
                rPr.Add(new XElement(w + "shd", new XAttribute(w + "val", "clear"), new XAttribute(w + "color", "auto"), new XAttribute(w + "fill", HexOf(back))));

            var run = new XElement(w + "r", rPr);
            var buffer = new StringBuilder();
            for (int i = start; i < end; i++)
            {
                char ch = text[i];
                if (ch == '\t')
                {
                    if (buffer.Length > 0) { run.Add(TextElement(w, buffer.ToString())); buffer.Length = 0; }
                    run.Add(new XElement(w + "tab"));
                }
                else if (ch == '\r') continue;
                else buffer.Append(ch);
            }
            if (buffer.Length > 0) run.Add(TextElement(w, buffer.ToString()));
            if (!run.Elements().Any(x => x.Name == w + "t" || x.Name == w + "tab")) run.Add(new XElement(w + "t", ""));
            return run;
        }

        static XElement TextElement(XNamespace w, string value)
        {
            var clean = new StringBuilder(value.Length);
            foreach (char ch in value) if (!Char.IsControl(ch) || ch == '\t') clean.Append(ch);
            return new XElement(w + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), clean.ToString());
        }

        static string HexOf(Color c) { return c.R.ToString("X2", CultureInfo.InvariantCulture) + c.G.ToString("X2", CultureInfo.InvariantCulture) + c.B.ToString("X2", CultureInfo.InvariantCulture); }
        void AddZipText(ZipArchive archive, string name, string content) { var entry = archive.CreateEntry(name, CompressionLevel.Optimal); using (var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false))) writer.Write(content); }
        void ExportRtf()
        {
            using (var d = new SaveFileDialog { Filter = "Rich Text Format|*.rtf", FileName = Current.Name + ".rtf" })
                if (d.ShowDialog(this) == DialogResult.OK)
                {
                    bool lifted = LiftHighlights();
                    try { editor.SaveFile(d.FileName, RichTextBoxStreamType.RichText); }
                    catch (Exception ex) { MessageBox.Show(this, ex.Message); }
                    finally { RestoreHighlights(lifted); }
                }
        }
        void CloseDoc(int n)
        {
            if (n < 0 || n >= docs.Count) return;
            if (docs[n].Dirty)
            {
                var q = MessageBox.Show(this, "Save changes to " + docs[n].Name + "?", "Milkpad", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
                if (q == DialogResult.Cancel) return;
                if (q == DialogResult.Yes) { if (n != active) SwitchDoc(n); if (!SaveDoc()) return; }
            }
            SaveEditor();
            RememberClosed(docs[n]);
            docs.RemoveAt(n);
            active = -1;
            if (docs.Count == 0) docs.Add(new DocState { Id = "n" + (untitledSeq++) });
            SwitchDoc(Math.Min(n, docs.Count - 1));
        }

        static DocState CloneDoc(DocState d)
        {
            var copy = new DocState { Name = d.Name, Path = d.Path, Rtf = d.Rtf, Dirty = d.Dirty, ReadOnly = d.ReadOnly, Saved = d.Saved };
            copy.Comments.AddRange(d.Comments);
            copy.Versions.AddRange(d.Versions);
            return copy;
        }

        void RememberClosed(DocState d)
        {
            closedDocs.Add(CloneDoc(d));
            while (closedDocs.Count > 12) closedDocs.RemoveAt(0);
        }

        void ReopenClosedTab()
        {
            if (closedDocs.Count == 0) { Flash("No closed tabs to reopen"); return; }
            DocState d = closedDocs[closedDocs.Count - 1];
            closedDocs.RemoveAt(closedDocs.Count - 1);
            d.Id = "r" + (untitledSeq++);
            docs.Add(d);
            active = docs.Count - 1;
            SwitchDoc(active);
            editor.Focus();
            Flash("Reopened " + d.Name);
        }

        void ShowTabMenu(Control anchor, Point at)
        {
            if (Current == null) return;
            ContextMenuStrip menu = NewMenu();
            Item(menu, "Rename…", (s, e) => RenameDoc());
            Item(menu, "Copy file path", (s, e) => CopyCurrentPath());
            Item(menu, "Open containing folder", (s, e) => OpenCurrentFolder());
            Sep(menu);
            Item(menu, "Close", (s, e) => CloseDoc(active));
            var others = new ToolStripMenuItem("Close other tabs") { Enabled = docs.Count > 1 };
            others.Click += (s, e) => CloseOtherDocs(active);
            menu.Items.Add(others);
            var all = new ToolStripMenuItem("Close all tabs") { Enabled = docs.Count > 1 };
            all.Click += (s, e) => CloseAllDocs();
            menu.Items.Add(all);
            menu.Show(anchor, at);
        }

        void CopyCurrentPath()
        {
            if (Current == null || Current.Path.Length == 0) { Flash("Save this document first"); return; }
            try { Clipboard.SetText(Current.Path); Flash("Path copied"); }
            catch { Flash("Could not copy the path"); }
        }

        void OpenCurrentFolder()
        {
            if (Current == null || Current.Path.Length == 0) { Flash("Save this document first"); return; }
            try { System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + Current.Path + "\""); }
            catch { Flash("Could not open the folder"); }
        }

        void CloseOtherDocs(int keep)
        {
            for (int i = docs.Count - 1; i >= 0 && docs.Count > 1; i--)
            {
                if (i == keep) continue;
                int before = docs.Count;
                CloseDoc(i);
                if (docs.Count < before && i < keep) keep--;
            }
        }

        void CloseAllDocs()
        {
            while (docs.Count > 1)
            {
                int before = docs.Count;
                CloseDoc(docs.Count - 1);
                if (docs.Count == before) break;
            }
            CloseDoc(docs.Count - 1);
        }

        void RestoreWindowBounds()
        {
            if (windowedBounds.Width < 400 || windowedBounds.Height < 300) return;
            Rectangle work = Screen.PrimaryScreen == null ? new Rectangle(0, 0, 1250, 820) : Screen.PrimaryScreen.WorkingArea;
            Rectangle b = windowedBounds;
            b.Width = Math.Min(Math.Max(760, b.Width), Math.Max(760, work.Width));
            b.Height = Math.Min(Math.Max(520, b.Height), Math.Max(520, work.Height));
            bool visible = false;
            foreach (Screen screen in Screen.AllScreens)
                if (screen.WorkingArea.IntersectsWith(new Rectangle(b.Left, b.Top, Math.Min(b.Width, 80), Math.Min(b.Height, 80)))) { visible = true; break; }
            if (!visible)
            {
                b.X = work.X + Math.Max(0, (work.Width - b.Width) / 2);
                b.Y = work.Y + Math.Max(0, (work.Height - b.Height) / 2);
            }
            StartPosition = FormStartPosition.Manual;
            Bounds = b;
            windowedBounds = b;
            if (windowMaximized) WindowState = FormWindowState.Maximized;
        }
        void PageSetup() { using(var d=new PageSetupDialog { PageSettings=new PageSettings(), AllowMargins=true, AllowOrientation=true, AllowPaper=true }) { try { d.ShowDialog(this); } catch { MessageBox.Show(this,"Page setup is available through the Windows printer settings.","Page setup"); } } }
        void Print() { using(var d=new PrintDialog { Document=new PrintDocument(), AllowSomePages=true, UseEXDialog=true }) { d.Document.PrintPage += PrintPage; if(d.ShowDialog(this)==DialogResult.OK){printChar=0;d.Document.Print();} } }
        void PrintPage(object sender, PrintPageEventArgs e) { string text=editor.Text; using(var f=new Font(editor.Font.FontFamily,11)) { int chars,lines; e.Graphics.MeasureString(text.Substring(Math.Min(printChar,text.Length)),f,e.MarginBounds.Size,StringFormat.GenericTypographic,out chars,out lines); e.Graphics.DrawString(text.Substring(Math.Min(printChar,text.Length),Math.Min(chars,text.Length-Math.Min(printChar,text.Length))),f,Brushes.Black,e.MarginBounds); printChar+=chars; e.HasMorePages=printChar<text.Length; } }
        void SaveVersion()
        {
            if (Current == null) return;
            bool lifted = LiftHighlights();
            try { SaveEditor(); Current.Versions.Add(Current.Rtf); }
            finally { RestoreHighlights(lifted); }
            MessageBox.Show(this, "Version snapshot saved for this session.", "Version history");
        }
        void ShowVersions() { if(Current==null||Current.Versions.Count==0){MessageBox.Show(this,"No saved versions yet.","Version history");return;} var f=new Form{Text="Version history",Width=480,Height=330,StartPosition=FormStartPosition.CenterParent};var l=new ListBox{Dock=DockStyle.Fill};for(int i=0;i<Current.Versions.Count;i++)l.Items.Add("Snapshot " +(i+1));var b=new Button{Text="Restore",Dock=DockStyle.Bottom,Height=34};b.Click+=(s,e)=>{if(l.SelectedIndex>=0){loading=true;editor.Rtf=Current.Versions[l.SelectedIndex];loading=false;Current.Dirty=true;SaveEditor();RefreshAll();f.Close();}};f.Controls.Add(l);f.Controls.Add(b);f.ShowDialog(this); }
        string Prompt(string text, string title)
        {
            Color surface = dark ? DarkPanel : Color.FromArgb(247, 244, 248);
            Color ink = dark ? DarkText : Color.FromArgb(28, 28, 30);
            Color field = dark ? Color.FromArgb(58, 53, 58) : Color.White;
            using (var f = new Form { Text = title, Width = 420, Height = 158, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false, BackColor = surface, ForeColor = ink })
            {
                var l = new Label { Text = text, Left = 12, Top = 12, Width = 380, ForeColor = ink };
                var t = new TextBox { Left = 12, Top = 40, Width = 380, BackColor = field, ForeColor = ink, BorderStyle = BorderStyle.FixedSingle };
                var ok = new Button { Text = "OK", Left = 230, Top = 82, Width = 75, Height = 28, DialogResult = DialogResult.OK, BackColor = field, ForeColor = ink, FlatStyle = FlatStyle.Flat };
                var no = new Button { Text = "Cancel", Left = 317, Top = 82, Width = 75, Height = 28, DialogResult = DialogResult.Cancel, BackColor = field, ForeColor = ink, FlatStyle = FlatStyle.Flat };
                ok.FlatAppearance.BorderColor = dark ? Color.FromArgb(88, 76, 88) : Color.FromArgb(200, 194, 202);
                no.FlatAppearance.BorderColor = ok.FlatAppearance.BorderColor;
                f.Controls.AddRange(new Control[] { l, t, ok, no });
                f.AcceptButton = ok; f.CancelButton = no;
                t.SelectAll();
                return f.ShowDialog(this) == DialogResult.OK ? t.Text : "";
            }
        }
        void ToggleFullScreen(){if(!fullScreen){priorState=WindowState;FormBorderStyle=FormBorderStyle.None;WindowState=FormWindowState.Maximized;fullScreen=true;}else{FormBorderStyle=FormBorderStyle.Sizable;WindowState=priorState;fullScreen=false;}}
        void KeysPressed(object sender,KeyEventArgs e){if(e.Control&&e.KeyCode==Keys.N){NewDoc();e.SuppressKeyPress=true;}else if(e.Control&&e.KeyCode==Keys.O){OpenFile(this,EventArgs.Empty);e.SuppressKeyPress=true;}else if(e.Control&&e.KeyCode==Keys.S){SaveDoc();e.SuppressKeyPress=true;}else if(e.Control&&e.KeyCode==Keys.E){ShowEmojiPicker();e.SuppressKeyPress=true;}else if(e.Control&&e.KeyCode==Keys.F){FindReplace(false);e.SuppressKeyPress=true;}else if(e.Control&&e.KeyCode==Keys.H){FindReplace(true);e.SuppressKeyPress=true;}else if(e.KeyCode==Keys.F11){ToggleFullScreen();e.SuppressKeyPress=true;}else if(e.Control&&e.KeyCode==Keys.B){ToggleStyle(FontStyle.Bold);e.SuppressKeyPress=true;}else if(e.Control&&e.KeyCode==Keys.I){ToggleStyle(FontStyle.Italic);e.SuppressKeyPress=true;}else if (e.Control&&e.KeyCode==Keys.U){ToggleStyle(FontStyle.Underline);e.SuppressKeyPress=true;}else if (e.Control&&e.KeyCode==Keys.Oemcomma){ShowCustomize();e.SuppressKeyPress=true;}
            else if(e.Control&&e.Shift&&e.KeyCode==Keys.S){SaveAs();e.SuppressKeyPress=true;}
            else if(e.Control&&e.Shift&&e.KeyCode==Keys.T){ReopenClosedTab();e.SuppressKeyPress=true;}
            else if(e.Control&&e.KeyCode==Keys.W){CloseDoc(active);e.SuppressKeyPress=true;}
            else if(e.Control&&e.KeyCode==Keys.Tab){CycleTab(e.Shift?-1:1);e.SuppressKeyPress=true;}
            else if(e.KeyCode==Keys.F3){FindNext(!e.Shift);e.SuppressKeyPress=true;}
            else if(e.Control&&e.KeyCode==Keys.F2){ToggleBookmark();e.SuppressKeyPress=true;}
            else if(e.KeyCode==Keys.F2){RenameDoc();e.SuppressKeyPress=true;}
            else if(e.KeyCode==Keys.F4){NextBookmark();e.SuppressKeyPress=true;}
            else if(e.KeyCode==Keys.F1){ShowManual();e.SuppressKeyPress=true;}
            else if(e.KeyCode==Keys.F5){InsertStamp();e.SuppressKeyPress=true;}
            else if(e.Control&&e.KeyCode==Keys.G){GoToLine();e.SuppressKeyPress=true;}
            else if(e.Control&&e.KeyCode==Keys.D){DuplicateLine();e.SuppressKeyPress=true;}
            else if(e.Control&&e.Shift&&e.KeyCode==Keys.K){DeleteLines();e.SuppressKeyPress=true;}
            else if(e.Control&&e.KeyCode==Keys.J){JoinLines();e.SuppressKeyPress=true;}
            else if(e.Control&&e.Shift&&e.KeyCode==Keys.C){CopyPlain();e.SuppressKeyPress=true;}
            else if(e.Control&&e.Shift&&e.KeyCode==Keys.Z){editor.Redo();e.SuppressKeyPress=true;}
            else if(e.Control&&(e.KeyCode==Keys.Oemplus||e.KeyCode==Keys.Add)){zoom.Value=Math.Min(zoom.Maximum,zoom.Value+10);e.SuppressKeyPress=true;}
            else if(e.Control&&(e.KeyCode==Keys.OemMinus||e.KeyCode==Keys.Subtract)){zoom.Value=Math.Max(zoom.Minimum,zoom.Value-10);e.SuppressKeyPress=true;}
            else if(e.Control&&e.KeyCode==Keys.D0){zoom.Value=100;e.SuppressKeyPress=true;}
            else if(e.Control&&e.Alt&&e.KeyCode==Keys.S){SaveAll();e.SuppressKeyPress=true;}
            else if(e.KeyCode==Keys.Escape&&findBar.Visible){HideFindBar();e.SuppressKeyPress=true;}}
        void HandleFormClosing(object sender,FormClosingEventArgs e){SaveSettings();SaveEditor();foreach(var d in docs.Where(x=>x.Dirty).ToList()){var q=MessageBox.Show(this,"Save changes to "+d.Name+"?","Milkpad",MessageBoxButtons.YesNoCancel,MessageBoxIcon.Warning);if(q==DialogResult.Cancel){e.Cancel=true;return;}if(q==DialogResult.Yes){int n=docs.IndexOf(d);if(n!=active)SwitchDoc(n);if(!SaveDoc()){e.Cancel=true;return;}}if(!e.Cancel)ClearSession();}}
    }

    // A small Word 97-style pinball homage: Z and M are the flippers, Space launches,
    // R restarts and Esc closes. The table is drawn and simulated in memory, so it
    // adds no assets or external dependencies.
    sealed class MilkBallForm : Form
    {
        [DllImport("user32.dll")] static extern uint GetDpiForSystem();
        const float TableWidth = 420f;
        const float TableHeight = 520f;
        const float BallRadius = 7f;
        const float FlipperLength = 70f;
        const float LaneY = 196f;
        const float PostX = TableWidth / 2f;
        const float PostY = 460f;
        const float PostR = 9f;
        readonly Timer loop = new Timer();
        readonly Random random = new Random();
        readonly Color chrome, accent, ink, muted;
        readonly float[] bumperX = { TableWidth * 0.50f, TableWidth * 0.26f, TableWidth * 0.74f };
        readonly float[] bumperY = { 150f, 270f, 270f };
        readonly float[] bumperR = { 28f, 22f, 22f };
        readonly int[] bumperFlash = new int[3];
        readonly Rectangle table = new Rectangle(20, 68, (int)TableWidth, (int)TableHeight);
        readonly Rectangle[] targets = new Rectangle[6];
        readonly bool[] targetUp = new bool[6];
        readonly int[] targetFlash = new int[6];
        readonly PointF[] leftSling = { new PointF(56f, 398f), new PointF(122f, 458f), new PointF(42f, 458f) };
        readonly PointF[] rightSling = { new PointF(364f, 398f), new PointF(298f, 458f), new PointF(378f, 458f) };
        readonly float[] laneX = { 100f, 173f, 247f, 320f };
        readonly string[] laneLetters = { "M", "I", "L", "K" };
        readonly bool[] laneLit = new bool[4];
        readonly int[] laneFlash = new int[4];
        readonly Rectangle spinner = new Rectangle(206, 312, 12, 38);
        readonly Font titleFont, scoreFont, labelFont, smallFont;
        float ballX, ballY, speedX, speedY;
        bool launched, gameOver, leftDown, rightDown;
        int score, lives = 3, multiplier = 1, combo, comboTimer;
        int spinnerAngle, spinnerFlash, spinnerCooldown;
        int laneCooldown, leftFlash, rightFlash, postFlash;
        string message = "";
        int messageTimer;

        public MilkBallForm(Color chrome, Color accent, Color ink, Color muted)
        {
            this.chrome = chrome; this.accent = accent; this.ink = ink; this.muted = muted;
            Text = "milkBall";
            ClientSize = new Size(460, 650);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = chrome; ForeColor = ink; KeyPreview = true; DoubleBuffered = true;
            MinimumSize = MaximumSize = Size;
            float dpiScale = Math.Max(1f, GetDpiForSystem() / 96f);
            bool compact = dpiScale >= 1.4f;
            titleFont = new Font("Segoe UI Semibold", compact ? 11F : 14F);
            scoreFont = new Font("Consolas", compact ? 10F : 12F, FontStyle.Bold);
            labelFont = new Font("Segoe UI", compact ? 8F : 9F);
            smallFont = new Font("Segoe UI", compact ? 7.5F : 8.5F);
            for (int i = 0; i < 3; i++)
            {
                targets[i] = new Rectangle(42, 300 + i * 34, 20, 16);
                targets[i + 3] = new Rectangle(358, 300 + i * 34, 20, 16);
                targetUp[i] = targetUp[i + 3] = true;
            }
            loop.Interval = 16;
            loop.Tick += (s, e) => Step();
            Shown += (s, e) => { ResetGame(); Focus(); };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                loop.Stop(); loop.Dispose();
                titleFont.Dispose(); scoreFont.Dispose(); labelFont.Dispose(); smallFont.Dispose();
            }
            base.Dispose(disposing);
        }

        float LeftAngle { get { return leftDown ? -0.35f : 0.42f; } }
        float RightAngle { get { return rightDown ? (float)Math.PI + 0.35f : (float)Math.PI - 0.42f; } }
        float LeftPivotX { get { return TableWidth * 0.34f; } }
        float RightPivotX { get { return TableWidth * 0.66f; } }
        float FlipperY { get { return TableHeight - 46f; } }

        void ResetBall()
        {
            ballX = TableWidth / 2f;
            ballY = TableHeight - 92f;
            speedX = 0; speedY = 0; launched = false;
            combo = 0; comboTimer = 0;
        }

        void ResetGame()
        {
            score = 0; lives = 3; multiplier = 1; gameOver = false;
            combo = 0; comboTimer = 0; laneCooldown = 0; leftFlash = rightFlash = 0; postFlash = 0;
            spinnerFlash = spinnerCooldown = 0; spinnerAngle = 0; message = ""; messageTimer = 0;
            for (int i = 0; i < bumperFlash.Length; i++) bumperFlash[i] = 0;
            for (int i = 0; i < targetUp.Length; i++) { targetUp[i] = true; targetFlash[i] = 0; }
            for (int i = 0; i < laneLit.Length; i++) { laneLit[i] = false; laneFlash[i] = 0; }
            ResetBall();
            if (!loop.Enabled) loop.Start();
            Invalidate();
        }

        void Launch()
        {
            if (gameOver) { ResetGame(); return; }
            if (launched) return;
            ballX = TableWidth / 2f;
            ballY = TableHeight - 84f;
            speedX = (float)(random.NextDouble() * 3.2 - 1.6);
            speedY = -13.2f;
            launched = true;
            message = ""; messageTimer = 0;
        }

        void LoseBall()
        {
            if (gameOver) return;
            lives--;
            combo = 0; comboTimer = 0;
            if (lives <= 0) { lives = 0; gameOver = true; launched = false; message = "GAME OVER"; messageTimer = 9999; }
            else { ResetBall(); message = "BALL LOST — PRESS SPACE"; messageTimer = 80; }
        }

        void Step()
        {
            for (int i = 0; i < bumperFlash.Length; i++) if (bumperFlash[i] > 0) bumperFlash[i]--;
            for (int i = 0; i < targetFlash.Length; i++) if (targetFlash[i] > 0) targetFlash[i]--;
            for (int i = 0; i < laneFlash.Length; i++) if (laneFlash[i] > 0) laneFlash[i]--;
            if (leftFlash > 0) leftFlash--;
            if (rightFlash > 0) rightFlash--;
            if (postFlash > 0) postFlash--;
            if (spinnerFlash > 0) spinnerFlash--;
            if (spinnerCooldown > 0) spinnerCooldown--;
            if (laneCooldown > 0) laneCooldown--;
            if (comboTimer > 0) { comboTimer--; if (comboTimer == 0) combo = 0; }
            if (messageTimer > 0 && messageTimer < 9999) messageTimer--;
            if (launched && !gameOver)
            {
                Physics(0.5f);
                Physics(0.5f);
            }
            Invalidate();
        }

        void Physics(float dt)
        {
            if (!launched || gameOver) return;
            speedY += 0.30f * dt;
            ballX += speedX * dt;
            ballY += speedY * dt;
            speedX *= 0.999f;
            if (ballX < BallRadius) { ballX = BallRadius; speedX = Math.Abs(speedX) * 0.9f; }
            if (ballX > TableWidth - BallRadius) { ballX = TableWidth - BallRadius; speedX = -Math.Abs(speedX) * 0.9f; }
            if (ballY < BallRadius) { ballY = BallRadius; speedY = Math.Abs(speedY) * 0.9f; }

            CheckLanes();
            for (int i = 0; i < bumperX.Length; i++)
            {
                float dx = ballX - bumperX[i], dy = ballY - bumperY[i];
                float limit = BallRadius + bumperR[i];
                float distanceSquared = dx * dx + dy * dy;
                if (distanceSquared <= 0.0001f || distanceSquared >= limit * limit) continue;
                float distance = (float)Math.Sqrt(distanceSquared);
                float nx = dx / distance, ny = dy / distance;
                ballX = bumperX[i] + nx * limit;
                ballY = bumperY[i] + ny * limit;
                float dot = speedX * nx + speedY * ny;
                if (dot < 0) { speedX -= 2 * dot * nx; speedY -= 2 * dot * ny; }
                speedX += nx * 1.5f; speedY += ny * 1.5f;
                combo = comboTimer > 0 ? Math.Min(9, combo + 1) : 1;
                comboTimer = 100;
                score += (100 + combo * 20) * multiplier;
                bumperFlash[i] = 9;
            }

            CheckTargets();
            CheckSpinner();
            float pdx = ballX - PostX, pdy = ballY - PostY;
            float postLimit = BallRadius + PostR;
            float postDistanceSquared = pdx * pdx + pdy * pdy;
            if (postDistanceSquared > 0.0001f && postDistanceSquared < postLimit * postLimit)
            {
                float postDistance = (float)Math.Sqrt(postDistanceSquared);
                float nx = pdx / postDistance, ny = pdy / postDistance;
                ballX = PostX + nx * postLimit; ballY = PostY + ny * postLimit;
                float dot = speedX * nx + speedY * ny;
                if (dot < 0) { speedX -= 2 * dot * nx; speedY -= 2 * dot * ny; }
                speedX += nx * 1.8f; speedY += ny * 1.8f;
                postFlash = 8;
                score += 25 * multiplier;
            }
            if (CollideSegment(leftSling[0], leftSling[1], 5f, 3.0f)) { score += 50 * multiplier; leftFlash = 10; }
            if (CollideSegment(rightSling[0], rightSling[1], 5f, 3.0f)) { score += 50 * multiplier; rightFlash = 10; }
            CollideFlipper(LeftPivotX, FlipperY, LeftAngle);
            CollideFlipper(RightPivotX, FlipperY, RightAngle);
            speedX = Math.Max(-13f, Math.Min(13f, speedX));
            speedY = Math.Max(-15f, Math.Min(15f, speedY));
            if (ballY > TableHeight + 26f) LoseBall();
        }

        void CheckLanes()
        {
            if (laneCooldown > 0) return;
            for (int i = 0; i < laneX.Length; i++)
            {
                if (laneLit[i]) continue;
                if (Math.Abs(ballX - laneX[i]) > 21 || Math.Abs(ballY - LaneY) > 15) continue;
                laneLit[i] = true; laneFlash[i] = 16;
                score += 250 * multiplier;
                bool all = true;
                for (int j = 0; j < laneLit.Length; j++) if (!laneLit[j]) all = false;
                if (all)
                {
                    lives++;
                    multiplier = Math.Min(5, multiplier + 1);
                    score += 1000 * multiplier;
                    laneCooldown = 90;
                    for (int j = 0; j < laneLit.Length; j++) laneLit[j] = false;
                    message = "M-I-L-K! EXTRA BALL — MULTIPLIER x" + multiplier;
                    messageTimer = 120;
                }
                return;
            }
        }

        void CheckTargets()
        {
            for (int i = 0; i < targets.Length; i++)
            {
                if (!targetUp[i]) continue;
                RectangleF r = targets[i];
                if (!CircleHitsRect(ballX, ballY, BallRadius, r)) continue;
                float left = ballX - (r.Left - BallRadius);
                float right = (r.Right + BallRadius) - ballX;
                float top = ballY - (r.Top - BallRadius);
                float bottom = (r.Bottom + BallRadius) - ballY;
                float least = Math.Min(Math.Min(left, right), Math.Min(top, bottom));
                if (least == left) { ballX = r.Left - BallRadius; speedX = -Math.Abs(speedX) * 0.95f; }
                else if (least == right) { ballX = r.Right + BallRadius; speedX = Math.Abs(speedX) * 0.95f; }
                else if (least == top) { ballY = r.Top - BallRadius; speedY = -Math.Abs(speedY) * 0.95f; }
                else { ballY = r.Bottom + BallRadius; speedY = Math.Abs(speedY) * 0.95f; }
                targetUp[i] = false; targetFlash[i] = 14;
                score += 250 * multiplier;
                bool all = true;
                for (int j = 0; j < targetUp.Length; j++) if (targetUp[j]) all = false;
                if (all)
                {
                    for (int j = 0; j < targetUp.Length; j++) { targetUp[j] = true; targetFlash[j] = 18; }
                    multiplier = Math.Min(5, multiplier + 1);
                    score += 1000 * multiplier;
                    message = "TARGET BANK CLEARED — MULTIPLIER x" + multiplier;
                    messageTimer = 100;
                }
                return;
            }
        }

        void CheckSpinner()
        {
            if (spinnerCooldown > 0) return;
            if (!CircleHitsRect(ballX, ballY, BallRadius, spinner)) return;
            float left = ballX - (spinner.Left - BallRadius);
            float right = (spinner.Right + BallRadius) - ballX;
            float top = ballY - (spinner.Top - BallRadius);
            float bottom = (spinner.Bottom + BallRadius) - ballY;
            float least = Math.Min(Math.Min(left, right), Math.Min(top, bottom));
            if (least == left) { ballX = spinner.Left - BallRadius; speedX = -Math.Abs(speedX) * 0.95f; }
            else if (least == right) { ballX = spinner.Right + BallRadius; speedX = Math.Abs(speedX) * 0.95f; }
            else if (least == top) { ballY = spinner.Top - BallRadius; speedY = -Math.Abs(speedY) * 0.95f; }
            else { ballY = spinner.Bottom + BallRadius; speedY = Math.Abs(speedY) * 0.95f; }
            spinnerAngle = (spinnerAngle + 23) % 360;
            spinnerFlash = 12; spinnerCooldown = 10;
            score += 75 * multiplier;
        }

        bool CollideSegment(PointF a, PointF b, float radius, float kick)
        {
            float closestX, closestY;
            ClosestPoint(ballX, ballY, a.X, a.Y, b.X, b.Y, out closestX, out closestY);
            float dx = ballX - closestX, dy = ballY - closestY;
            float limit = BallRadius + radius;
            float distanceSquared = dx * dx + dy * dy;
            if (distanceSquared <= 0.0001f || distanceSquared >= limit * limit) return false;
            float distance = (float)Math.Sqrt(distanceSquared);
            float nx = dx / distance, ny = dy / distance;
            ballX = closestX + nx * limit;
            ballY = closestY + ny * limit;
            float dot = speedX * nx + speedY * ny;
            if (dot < 0) { speedX -= 2 * dot * nx; speedY -= 2 * dot * ny; }
            speedX += nx * kick; speedY += ny * kick;
            return true;
        }

        void CollideFlipper(float pivotX, float pivotY, float angle)
        {
            float endX = pivotX + (float)Math.Cos(angle) * FlipperLength;
            float endY = pivotY + (float)Math.Sin(angle) * FlipperLength;
            CollideSegment(new PointF(pivotX, pivotY), new PointF(endX, endY), 5f, 2.8f);
        }

        static bool CircleHitsRect(float cx, float cy, float radius, RectangleF rect)
        {
            float closestX = Math.Max(rect.Left, Math.Min(cx, rect.Right));
            float closestY = Math.Max(rect.Top, Math.Min(cy, rect.Bottom));
            float dx = cx - closestX, dy = cy - closestY;
            return dx * dx + dy * dy < radius * radius;
        }

        static void ClosestPoint(float x, float y, float ax, float ay, float bx, float by, out float closestX, out float closestY)
        {
            float vx = bx - ax, vy = by - ay;
            float wx = x - ax, wy = y - ay;
            float lengthSquared = vx * vx + vy * vy;
            float t = lengthSquared <= 0 ? 0 : Math.Max(0, Math.Min(1, (wx * vx + wy * vy) / lengthSquared));
            closestX = ax + t * vx;
            closestY = ay + t * vy;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) { Close(); return; }
            if (e.KeyCode == Keys.Z) leftDown = true;
            else if (e.KeyCode == Keys.M) rightDown = true;
            else if (e.KeyCode == Keys.R) ResetGame();
            else if (e.KeyCode == Keys.Space) { if (gameOver) ResetGame(); else if (!launched) Launch(); }
            else { base.OnKeyDown(e); return; }
            e.SuppressKeyPress = true;
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Z) leftDown = false;
            else if (e.KeyCode == Keys.M) rightDown = false;
            base.OnKeyUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var brush = new SolidBrush(chrome)) e.Graphics.FillRectangle(brush, ClientRectangle);
            using (var titleBrush = new SolidBrush(ink)) e.Graphics.DrawString("MILKBALL", titleFont, titleBrush, 20f, 12f);
            using (var scoreBrush = new SolidBrush(accent)) e.Graphics.DrawString("SCORE " + score.ToString("N0"), scoreFont, scoreBrush, 200f, 18f);
            using (var mutedBrush = new SolidBrush(muted)) e.Graphics.DrawString("BALLS " + lives, labelFont, mutedBrush, 372f, 20f);
            using (var multiBrush = new SolidBrush(accent)) e.Graphics.DrawString("MULTIPLIER x" + multiplier, labelFont, multiBrush, 20f, 44f);
            if (combo > 1) using (var comboBrush = new SolidBrush(muted)) e.Graphics.DrawString("COMBO x" + combo, labelFont, comboBrush, 200f, 44f);

            using (var tableBrush = new SolidBrush(Color.FromArgb(16, 14, 22))) e.Graphics.FillRectangle(tableBrush, table);
            using (var edge = new Pen(accent, 1.5f)) e.Graphics.DrawRectangle(edge, table);

            System.Drawing.Drawing2D.GraphicsState state = e.Graphics.Save();
            e.Graphics.TranslateTransform(table.Left, table.Top);

            using (var rail = new Pen(Color.FromArgb(150, accent), 2f))
            {
                e.Graphics.DrawArc(rail, 24f, 48f, 372f, 150f, 180f, 180f);
                e.Graphics.DrawLine(rail, 12f, 120f, 12f, 474f);
                e.Graphics.DrawLine(rail, 408f, 120f, 408f, 474f);
            }

            for (int i = 0; i < laneX.Length; i++)
            {
                Color laneColor = laneLit[i] ? Color.White : (laneFlash[i] > 0 ? accent : Color.FromArgb(120, muted));
                using (var fill = new SolidBrush(laneLit[i] ? Color.FromArgb(55, accent) : Color.FromArgb(30, muted))) e.Graphics.FillEllipse(fill, laneX[i] - 19f, LaneY - 17f, 38f, 34f);
                using (var pen = new Pen(laneColor, laneLit[i] ? 2f : 1f)) e.Graphics.DrawEllipse(pen, laneX[i] - 19f, LaneY - 17f, 38f, 34f);
                using (var letter = new SolidBrush(laneColor))
                using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    e.Graphics.DrawString(laneLetters[i], labelFont, letter, new RectangleF(laneX[i] - 19f, LaneY - 17f, 38f, 34f), format);
            }

            for (int i = 0; i < targets.Length; i++)
            {
                Rectangle r = targets[i];
                Color fill = targetFlash[i] > 0 ? Color.White : (targetUp[i] ? accent : Color.FromArgb(44, 39, 48));
                using (var brush = new SolidBrush(fill)) e.Graphics.FillRectangle(brush, r);
                using (var pen = new Pen(targetUp[i] ? Color.FromArgb(220, accent) : Color.FromArgb(100, muted), 1.2f)) e.Graphics.DrawRectangle(pen, r);
                if (targetUp[i]) using (var shine = new SolidBrush(Color.FromArgb(180, 255, 255, 255))) e.Graphics.FillRectangle(shine, r.Left + 3, r.Top + 3, r.Width - 6, 3);
            }

            RectangleF sr = spinner;
            Color spinnerColor = spinnerFlash > 0 ? Color.White : accent;
            using (var pen = new Pen(spinnerColor, 2f)) e.Graphics.DrawRectangle(pen, sr.X, sr.Y, sr.Width, sr.Height);
            e.Graphics.TranslateTransform(sr.X + sr.Width / 2f, sr.Y + sr.Height / 2f);
            e.Graphics.RotateTransform(spinnerAngle);
            using (var pen = new Pen(spinnerColor, 3f)) e.Graphics.DrawLine(pen, 0, -21f, 0, 21f);
            e.Graphics.ResetTransform();
            e.Graphics.TranslateTransform(table.Left, table.Top);

            using (var postGlow = new SolidBrush(Color.FromArgb(45, accent))) e.Graphics.FillEllipse(postGlow, PostX - PostR - 5, PostY - PostR - 5, (PostR + 5) * 2, (PostR + 5) * 2);
            using (var postBrush = new SolidBrush(postFlash > 0 ? Color.White : Color.FromArgb(95, accent))) e.Graphics.FillEllipse(postBrush, PostX - PostR, PostY - PostR, PostR * 2, PostR * 2);
            using (var postPen = new Pen(accent, 1.2f)) e.Graphics.DrawEllipse(postPen, PostX - PostR, PostY - PostR, PostR * 2, PostR * 2);

            DrawSling(e.Graphics, leftSling, leftFlash);
            DrawSling(e.Graphics, rightSling, rightFlash);

            for (int i = 0; i < bumperX.Length; i++)
            {
                Color fill = bumperFlash[i] > 0 ? Color.White : accent;
                using (var glow = new SolidBrush(Color.FromArgb(55, fill))) e.Graphics.FillEllipse(glow, bumperX[i] - bumperR[i] - 6, bumperY[i] - bumperR[i] - 6, (bumperR[i] + 6) * 2, (bumperR[i] + 6) * 2);
                using (var brush = new SolidBrush(fill)) e.Graphics.FillEllipse(brush, bumperX[i] - bumperR[i], bumperY[i] - bumperR[i], bumperR[i] * 2, bumperR[i] * 2);
                using (var pen = new Pen(Color.FromArgb(210, 255, 255, 255), 1.2f)) e.Graphics.DrawEllipse(pen, bumperX[i] - bumperR[i] + 5, bumperY[i] - bumperR[i] + 5, (bumperR[i] - 5) * 2, (bumperR[i] - 5) * 2);
            }

            DrawFlipper(e.Graphics, LeftPivotX, FlipperY, LeftAngle);
            DrawFlipper(e.Graphics, RightPivotX, FlipperY, RightAngle);

            using (var shadow = new SolidBrush(Color.FromArgb(90, 0, 0, 0))) e.Graphics.FillEllipse(shadow, ballX - BallRadius + 1, ballY - BallRadius + 3, BallRadius * 2, BallRadius * 2);
            using (var ball = new SolidBrush(Color.FromArgb(248, 246, 240))) e.Graphics.FillEllipse(ball, ballX - BallRadius, ballY - BallRadius, BallRadius * 2, BallRadius * 2);
            using (var shine = new SolidBrush(Color.White)) e.Graphics.FillEllipse(shine, ballX - 3, ballY - 4, 4, 4);

            if (message.Length > 0 && messageTimer > 0)
            {
                using (var format = new StringFormat { Alignment = StringAlignment.Center })
                using (var brush = new SolidBrush(message.StartsWith("GAME OVER") ? Color.FromArgb(230, 130, 130) : accent))
                    e.Graphics.DrawString(message, labelFont, brush, new RectangleF(0, 350, TableWidth, 24), format);
            }

            e.Graphics.Restore(state);

            string status = gameOver ? "GAME OVER — PRESS R TO PLAY AGAIN" : (!launched ? "PRESS SPACE TO LAUNCH" : "");
            if (status.Length > 0)
            {
                using (var format = new StringFormat { Alignment = StringAlignment.Center })
                using (var brush = new SolidBrush(gameOver ? Color.FromArgb(230, 130, 130) : accent))
                    e.Graphics.DrawString(status, labelFont, brush, new RectangleF(table.Left, table.Bottom + 10, table.Width, 22), format);
            }
            using (var format = new StringFormat { Alignment = StringAlignment.Center })
            using (var brush = new SolidBrush(muted))
                e.Graphics.DrawString("Z / M FLIPPERS   SPACE LAUNCH   R RESTART   ESC CLOSE", smallFont, brush, new RectangleF(20, 620, 420, 20), format);
        }

        void DrawSling(Graphics g, PointF[] points, int flash)
        {
            using (var path = new System.Drawing.Drawing2D.GraphicsPath())
            {
                path.AddPolygon(points);
                using (var brush = new SolidBrush(Color.FromArgb(flash > 0 ? 190 : 55, flash > 0 ? Color.White : accent))) g.FillPath(brush, path);
                using (var pen = new Pen(flash > 0 ? Color.White : accent, 1.5f)) g.DrawPath(pen, path);
            }
        }

        void DrawFlipper(Graphics g, float pivotX, float pivotY, float angle)
        {
            float endX = pivotX + (float)Math.Cos(angle) * FlipperLength;
            float endY = pivotY + (float)Math.Sin(angle) * FlipperLength;
            using (var pen = new Pen(accent, 10f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round })
                g.DrawLine(pen, pivotX, pivotY, endX, endY);
            using (var pivot = new SolidBrush(Color.FromArgb(235, 255, 255, 255))) g.FillEllipse(pivot, pivotX - 3, pivotY - 3, 6, 6);
        }
    }


    static class Program { [STAThread] static void Main(){Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new MainForm());} }
}
