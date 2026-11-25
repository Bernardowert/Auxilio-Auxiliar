// Launcher nativo (C# / .NET Framework 4.x) - sem dependencias de terceiros.
// Compila com o csc.exe que ja vem no Windows. Compativel com C# 5.
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace GameLauncher
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            bool created;
            using (Mutex mutex = new Mutex(true, "GameLauncher.SingleInstance.7F3A", out created))
            {
                if (!created) { return; }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
        }
    }

    // =====================================================================
    //  Configuracao externa (JSON ao lado do .exe)
    // =====================================================================
    internal sealed class LauncherConfig
    {
        public const string FileName = "launcher-config.json";
        public string GameName;
        public string MagnetLink;
        public string ManifestUrl;

        public static string FilePath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FileName); }
        }

        public static LauncherConfig Load(out string error)
        {
            error = null;
            string path = FilePath;
            if (!File.Exists(path))
            {
                error = "Arquivo " + FileName + " n\u00E3o encontrado ao lado do programa.";
                return null;
            }
            try
            {
                string json = File.ReadAllText(path, System.Text.Encoding.UTF8);
                LauncherConfig cfg = new LauncherConfig();
                cfg.GameName = ReadString(json, "game_name");
                cfg.MagnetLink = ReadString(json, "magnet_link");
                cfg.ManifestUrl = ReadString(json, "github_manifest_url");
                if (string.IsNullOrEmpty(cfg.GameName)) { cfg.GameName = "Grand Theft Aux\u00EDlio"; }
                return cfg;
            }
            catch (Exception ex)
            {
                error = "N\u00E3o foi poss\u00EDvel ler " + FileName + ": " + ex.Message;
                return null;
            }
        }

        private static string ReadString(string json, string key)
        {
            Match m = Regex.Match(json, "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
            if (!m.Success) { return null; }
            return Regex.Unescape(m.Groups[1].Value);
        }
    }

    // =====================================================================
    //  Validacao do magnet
    // =====================================================================
    internal static class MagnetValidator
    {
        private static readonly Regex Btih = new Regex("^urn:btih:([0-9a-f]{40}|[a-z2-7]{32})$", RegexOptions.IgnoreCase);
        private static readonly Regex Btmh = new Regex("^urn:btmh:1220[0-9a-f]{64}$", RegexOptions.IgnoreCase);
        private static readonly Regex Forbidden = new Regex("[\\s\\x00-\\x1F\"'<>`]");

        public static bool TryValidate(string input, out string link, out string error)
        {
            link = null;
            error = null;

            if (string.IsNullOrEmpty(input))
            {
                error = "O link magnet n\u00E3o est\u00E1 configurado em " + LauncherConfig.FileName + ".";
                return false;
            }
            string value = input.Trim();
            if (value.IndexOf("HASH_DO_TORRENT", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                error = "O magnet ainda \u00E9 o exemplo. Edite " + LauncherConfig.FileName + " e informe o link real.";
                return false;
            }
            if (value.Length < 20 || value.Length > 4096)
            {
                error = "O link magnet configurado tem tamanho inv\u00E1lido.";
                return false;
            }
            if (Forbidden.IsMatch(value))
            {
                error = "O link magnet cont\u00E9m caracteres n\u00E3o permitidos.";
                return false;
            }
            if (!value.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase))
            {
                error = "O link configurado n\u00E3o \u00E9 um magnet v\u00E1lido (deve come\u00E7ar com magnet:?).";
                return false;
            }

            bool hasHash = false;
            foreach (string part in value.Substring(8).Split('&'))
            {
                int eq = part.IndexOf('=');
                if (eq <= 0) { continue; }
                string key = part.Substring(0, eq).ToLowerInvariant();
                if (key != "xt" && !key.StartsWith("xt.")) { continue; }
                string val;
                try { val = Uri.UnescapeDataString(part.Substring(eq + 1)); }
                catch (UriFormatException) { continue; }
                if (Btih.IsMatch(val) || Btmh.IsMatch(val)) { hasHash = true; }
            }
            if (!hasHash)
            {
                error = "O magnet n\u00E3o cont\u00E9m um hash de torrent v\u00E1lido (xt=urn:btih:...).";
                return false;
            }

            link = value;
            return true;
        }
    }

    // =====================================================================
    //  Tema escuro e utilitarios de desenho
    // =====================================================================
    internal enum BannerKind { Success, Error }

    internal static class Ui
    {
        public static readonly float Scale = GetScale();

        private static float GetScale()
        {
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) { return g.DpiX / 96f; }
        }

        public static int S(int v) { return (int)Math.Round(v * Scale); }
        private static Color Hex(string h) { return ColorTranslator.FromHtml(h); }

        public static readonly Color Bg = Hex("#0B0D12");
        public static readonly Color Card = Hex("#151922");
        public static readonly Color Border = Hex("#252B38");
        public static readonly Color Ink = Hex("#ECEFF6");
        public static readonly Color Muted = Hex("#8B93A7");
        public static readonly Color Accent = Hex("#F2B544");
        public static readonly Color AccentHover = Hex("#F7C666");
        public static readonly Color AccentPress = Hex("#DDA23A");
        public static readonly Color AccentInk = Hex("#1A1204");
        public static readonly Color Success = Hex("#5CC689");
        public static readonly Color SuccessBg = Hex("#10261F");
        public static readonly Color Danger = Hex("#F08AA3");
        public static readonly Color DangerBg = Hex("#2A1720");

        public const TextFormatFlags Wrap = TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.Left | TextFormatFlags.Top;
        public const TextFormatFlags Center = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis;

        public static Font F(float pt, bool semibold)
        {
            return new Font(semibold ? "Segoe UI Semibold" : "Segoe UI", pt, FontStyle.Regular, GraphicsUnit.Point);
        }

        public static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            int d = radius * 2;
            GraphicsPath p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        // Brilho suave: camadas translucidas de retangulos arredondados que crescem.
        public static void Glow(Graphics g, Rectangle core, int radius, int layers, int step, int alpha)
        {
            for (int i = layers; i >= 1; i--)
            {
                int grow = S(i * step);
                Rectangle r = Rectangle.Inflate(core, grow, grow);
                using (GraphicsPath p = RoundRect(r, radius + grow))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(alpha, Accent)))
                {
                    g.FillPath(b, p);
                }
            }
        }

        private static Pen RoundPen(Color color, float width)
        {
            Pen pen = new Pen(color, width);
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            pen.LineJoin = LineJoin.Round;
            return pen;
        }

        public static void DrawDownload(Graphics g, Point c, Color color, float u)
        {
            using (Pen pen = RoundPen(color, Math.Max(1.8f, 2f * u)))
            {
                g.DrawLine(pen, c.X, c.Y - (int)(7 * u), c.X, c.Y + (int)(3 * u));
                g.DrawLines(pen, new Point[] {
                    new Point(c.X - (int)(5 * u), c.Y - (int)(2 * u)),
                    new Point(c.X, c.Y + (int)(3 * u)),
                    new Point(c.X + (int)(5 * u), c.Y - (int)(2 * u)) });
                g.DrawLine(pen, c.X - (int)(7 * u), c.Y + (int)(8 * u), c.X + (int)(7 * u), c.Y + (int)(8 * u));
            }
        }

        public static void DrawCheck(Graphics g, Point c, Color color, float u)
        {
            using (Pen pen = RoundPen(color, Math.Max(1.8f, 2.2f * u)))
            {
                g.DrawLines(pen, new Point[] {
                    new Point(c.X - (int)(5 * u), c.Y),
                    new Point(c.X - (int)(1.5f * u), c.Y + (int)(4 * u)),
                    new Point(c.X + (int)(5 * u), c.Y - (int)(4 * u)) });
            }
        }
    }

    // =====================================================================
    //  Cabecalho: marca, titulo e nome do jogo
    // =====================================================================
    internal sealed class Hero : Control
    {
        private string gameName = "Grand Theft Aux\u00EDlio";
        private string customTitle = null;
        private string subtitle = "";
        private readonly Font logoFont = Ui.F(30f, true);
        private readonly Font titleFont = Ui.F(24f, true);
        private readonly Font subFont = Ui.F(11f, false);

        public Hero(int width)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Size = new Size(width, Ui.S(196));
            TabStop = false;
        }

        public void SetGame(string name)
        {
            gameName = string.IsNullOrEmpty(name) ? "Grand Theft Aux\u00EDlio" : name;
            Invalidate();
        }

        // Define um titulo diferente do nome do jogo (null volta ao nome do jogo).
        public void SetPage(string title, string sub)
        {
            customTitle = title;
            subtitle = sub ?? "";
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Ui.Bg);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int ls = Ui.S(72);
            Rectangle logo = new Rectangle((Width - ls) / 2, Ui.S(22), ls, ls);
            Ui.Glow(g, logo, Ui.S(20), 8, 2, 7);
            using (GraphicsPath p = Ui.RoundRect(logo, Ui.S(20)))
            using (LinearGradientBrush lg = new LinearGradientBrush(logo, Ui.AccentHover, Ui.AccentPress, 60f))
            {
                g.FillPath(lg, p);
            }
            string initial = gameName.Substring(0, 1).ToUpperInvariant();
            TextRenderer.DrawText(g, initial, logoFont, logo, Ui.AccentInk, Ui.Center);

            string title = customTitle != null ? customTitle : gameName;
            TextRenderer.DrawText(g, title, titleFont, new Rectangle(Ui.S(20), Ui.S(116), Width - Ui.S(40), Ui.S(42)), Ui.Ink, Ui.Center);
            if (subtitle.Length > 0)
            {
                TextRenderer.DrawText(g, subtitle, subFont, new Rectangle(Ui.S(20), Ui.S(160), Width - Ui.S(40), Ui.S(26)), Ui.Muted, Ui.Center);
            }
        }
    }

    // =====================================================================
    //  Botao principal (pilula com brilho)
    // =====================================================================
    internal sealed class DownloadButton : Control, IButtonControl
    {
        private bool hover;
        private bool down;
        private readonly Font textFont = Ui.F(13f, true);

        public DownloadButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            TabStop = true;
            Size = new Size(Ui.S(360), Ui.S(120));
        }

        // Area visivel e clicavel do botao (o resto e o brilho)
        private Rectangle Pill
        {
            get { return new Rectangle(Ui.S(40), Ui.S(30), Width - Ui.S(80), Height - Ui.S(60)); }
        }

        // --- IButtonControl ---
        public DialogResult DialogResult { get { return DialogResult.None; } set { } }
        public void NotifyDefault(bool value) { }
        public void PerformClick()
        {
            if (Enabled && Visible) { OnClick(EventArgs.Empty); }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            bool inside = Pill.Contains(e.Location);
            if (inside != hover)
            {
                hover = inside;
                Cursor = inside ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hover = false;
            down = false;
            Cursor = Cursors.Default;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && Pill.Contains(e.Location))
            {
                down = true;
                Focus();
                Invalidate();
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            bool fire = down && Enabled && Pill.Contains(e.Location);
            down = false;
            Invalidate();
            base.OnMouseUp(e);
            if (fire) { OnClick(EventArgs.Empty); }
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space) { PerformClick(); }
            base.OnKeyUp(e);
        }

        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Ui.Bg);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle pill = Pill;
            int radius = pill.Height / 2;
            bool active = Enabled && (hover || down);

            if (Enabled) { Ui.Glow(g, pill, radius, 10, 2, active ? 9 : 5); }

            Color top, bottom, fore;
            if (!Enabled)
            {
                top = ColorTranslator.FromHtml("#5B4D2B");
                bottom = ColorTranslator.FromHtml("#4C4124");
                fore = ColorTranslator.FromHtml("#A99C78");
            }
            else if (down)
            {
                top = ColorTranslator.FromHtml("#E2A23A");
                bottom = ColorTranslator.FromHtml("#D0902A");
                fore = Ui.AccentInk;
            }
            else if (hover)
            {
                top = ColorTranslator.FromHtml("#FFD680");
                bottom = ColorTranslator.FromHtml("#F2B544");
                fore = Ui.AccentInk;
            }
            else
            {
                top = ColorTranslator.FromHtml("#F7C666");
                bottom = ColorTranslator.FromHtml("#E9A82F");
                fore = Ui.AccentInk;
            }

            using (GraphicsPath p = Ui.RoundRect(pill, radius))
            using (LinearGradientBrush lg = new LinearGradientBrush(pill, top, bottom, 90f))
            {
                g.FillPath(lg, p);
            }

            if (Focused && ShowFocusCues)
            {
                Rectangle fr = Rectangle.Inflate(pill, Ui.S(5), Ui.S(5));
                using (GraphicsPath fp = Ui.RoundRect(fr, fr.Height / 2))
                using (Pen pen = new Pen(Color.FromArgb(210, Ui.Ink), 2f))
                {
                    g.DrawPath(pen, fp);
                }
            }

            Size ts = TextRenderer.MeasureText(Text, textFont, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            int gw = Ui.S(20) + Ui.S(14);
            int x = pill.X + (pill.Width - (gw + ts.Width)) / 2;
            int dy = down ? Ui.S(1) : 0;
            Ui.DrawDownload(g, new Point(x + Ui.S(10), pill.Y + pill.Height / 2 - Ui.S(1) + dy), fore, Ui.Scale * 1.15f);
            Rectangle tr = new Rectangle(x + gw, pill.Y + dy, ts.Width + Ui.S(6), pill.Height);
            TextRenderer.DrawText(g, Text, textFont, tr, fore, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        }
    }

    // =====================================================================
    //  Botao secundario (contorno)
    // =====================================================================
    internal sealed class GhostButton : Control
    {
        private bool hover;
        private bool down;
        private readonly Font textFont = Ui.F(10.5f, true);

        public GhostButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            TabStop = true;
            Cursor = Cursors.Hand;
            Size = new Size(Ui.S(240), Ui.S(48));
        }

        protected override bool IsInputKey(Keys keyData)
        {
            return keyData == Keys.Enter || base.IsInputKey(keyData);
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { down = true; Focus(); Invalidate(); }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            bool fire = down && ClientRectangle.Contains(e.Location);
            down = false;
            Invalidate();
            base.OnMouseUp(e);
            if (fire) { OnClick(EventArgs.Empty); }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
            {
                e.Handled = true;
                OnClick(EventArgs.Empty);
            }
            base.OnKeyDown(e);
        }

        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Ui.Bg);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color fill = down ? ColorTranslator.FromHtml("#1D2330") : (hover ? Ui.Card : Ui.Bg);
            Color border = hover ? Ui.Muted : Ui.Border;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath p = Ui.RoundRect(r, Height / 2))
            {
                using (SolidBrush b = new SolidBrush(fill)) { g.FillPath(b, p); }
                using (Pen pen = new Pen(border, 1.5f)) { g.DrawPath(pen, p); }
            }

            if (Focused && ShowFocusCues)
            {
                Rectangle fr = Rectangle.Inflate(r, -Ui.S(4), -Ui.S(4));
                using (GraphicsPath fp = Ui.RoundRect(fr, fr.Height / 2))
                using (Pen pen = new Pen(Color.FromArgb(200, Ui.Accent), 2f))
                {
                    g.DrawPath(pen, fp);
                }
            }

            TextRenderer.DrawText(g, Text, textFont, new Rectangle(0, down ? Ui.S(1) : 0, Width, Height), Ui.Ink, Ui.Center);
        }
    }

    // =====================================================================
    //  Aviso (sucesso / erro)
    // =====================================================================
    internal sealed class Banner : Control
    {
        private BannerKind kind;
        private string message = "";
        private readonly Font msgFont = Ui.F(10f, false);
        private readonly Font iconFont = Ui.F(11f, true);

        public Banner(int width)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Size = new Size(width, Ui.S(60));
            TabStop = false;
            Visible = false;
        }

        public void ShowMessage(BannerKind k, string text)
        {
            kind = k;
            message = text;
            int textW = Width - Ui.S(18 + 28 + 14 + 20);
            Size ts = TextRenderer.MeasureText(text, msgFont, new Size(textW, int.MaxValue), Ui.Wrap);
            Height = Math.Max(Ui.S(60), ts.Height + Ui.S(28));
            Visible = true;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Ui.Bg);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color bg = kind == BannerKind.Success ? Ui.SuccessBg : Ui.DangerBg;
            Color fg = kind == BannerKind.Success ? Ui.Success : Ui.Danger;

            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath p = Ui.RoundRect(r, Ui.S(14)))
            {
                using (SolidBrush b = new SolidBrush(bg)) { g.FillPath(b, p); }
                using (Pen pen = new Pen(Color.FromArgb(120, fg), 1f)) { g.DrawPath(pen, p); }
            }

            int d = Ui.S(28);
            Rectangle circle = new Rectangle(Ui.S(18), (Height - d) / 2, d, d);
            using (SolidBrush b = new SolidBrush(fg)) { g.FillEllipse(b, circle); }
            if (kind == BannerKind.Success)
            {
                Ui.DrawCheck(g, new Point(circle.X + d / 2, circle.Y + d / 2), bg, Ui.Scale);
            }
            else
            {
                TextRenderer.DrawText(g, "!", iconFont, circle, bg, Ui.Center);
            }

            int textW = Width - Ui.S(18 + 28 + 14 + 20);
            Size ts = TextRenderer.MeasureText(message, msgFont, new Size(textW, int.MaxValue), Ui.Wrap);
            Rectangle tr = new Rectangle(Ui.S(18 + 28 + 14), (Height - ts.Height) / 2, textW, ts.Height);
            TextRenderer.DrawText(g, message, msgFont, tr, Ui.Ink, Ui.Wrap);
        }
    }

    // =====================================================================
    //  Janela principal (modo escuro)
    // =====================================================================
    internal sealed class MainForm : Form
    {
        private const string NoHandlerMessage =
            "N\u00E3o foi poss\u00EDvel abrir o download automaticamente. Verifique se o \u00B5Torrent est\u00E1 instalado e configurado como aplicativo padr\u00E3o para links magnet.";

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        private Hero hero;
        private Banner banner;
        private DownloadButton btnDownload;
        private GhostButton btnHave;
        private GhostButton btnBack;
        private string gameName = "Grand Theft Aux\u00EDlio";
        private System.Windows.Forms.Timer busyTimer;

        public MainForm()
        {
            AutoScaleMode = AutoScaleMode.None;
            DoubleBuffered = true;
            Text = "Grand Theft Aux\u00EDlio";
            BackColor = Ui.Bg;
            ForeColor = Ui.Ink;
            Font = Ui.F(10f, false);
            ClientSize = new Size(Ui.S(720), Ui.S(540));
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            busyTimer = new System.Windows.Forms.Timer();
            busyTimer.Interval = 1200;
            busyTimer.Tick += OnBusyTick;

            BuildUi();
            Shown += OnShown;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            // Barra de titulo escura (Windows 10 1809+ / Windows 11). Falhas sao ignoradas.
            try
            {
                int on = 1;
                if (DwmSetWindowAttribute(Handle, 20, ref on, 4) != 0) { DwmSetWindowAttribute(Handle, 19, ref on, 4); }
                int caption = 0x00120D0B; // #0B0D12 em COLORREF (BGR)
                int text = 0x00F6EFEC;    // #ECEFF6
                DwmSetWindowAttribute(Handle, 35, ref caption, 4);
                DwmSetWindowAttribute(Handle, 34, ref caption, 4);
                DwmSetWindowAttribute(Handle, 36, ref text, 4);
            }
            catch (Exception) { }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && busyTimer != null) { busyTimer.Dispose(); }
            base.Dispose(disposing);
        }

        private void BuildUi()
        {
            int cw = ClientSize.Width;

            hero = new Hero(cw);
            hero.Location = new Point(0, Ui.S(36));
            Controls.Add(hero);

            btnDownload = new DownloadButton();
            btnDownload.Text = "BAIXAR JOGO";
            btnDownload.Location = new Point((cw - btnDownload.Width) / 2, Ui.S(226));
            btnDownload.Click += OnDownloadClick;
            Controls.Add(btnDownload);

            btnHave = new GhostButton();
            btnHave.Text = "J\u00E1 tenho o jogo";
            btnHave.Location = new Point((cw - btnHave.Width) / 2, Ui.S(346));
            btnHave.Click += OnHaveClick;
            Controls.Add(btnHave);

            btnBack = new GhostButton();
            btnBack.Text = "Voltar";
            btnBack.Size = new Size(Ui.S(160), Ui.S(44));
            btnBack.Location = new Point((cw - btnBack.Width) / 2, Ui.S(280));
            btnBack.Click += OnBackClick;
            btnBack.Visible = false;
            Controls.Add(btnBack);

            banner = new Banner(Ui.S(560));
            banner.Location = new Point((cw - banner.Width) / 2, Ui.S(414));
            Controls.Add(banner);

            AcceptButton = btnDownload;
        }

        private void OnShown(object sender, EventArgs e)
        {
            string error;
            LauncherConfig cfg = LauncherConfig.Load(out error);
            if (cfg != null)
            {
                hero.SetGame(cfg.GameName);
                gameName = cfg.GameName;
                Text = cfg.GameName;
            }
            else
            {
                banner.ShowMessage(BannerKind.Error, error);
            }
            ActiveControl = btnDownload;
        }

        // Alterna entre as duas telas dentro da mesma janela.
        private void ShowPage(bool havePage)
        {
            banner.Visible = false;
            btnDownload.Visible = !havePage;
            btnHave.Visible = !havePage;
            btnBack.Visible = havePage;

            if (havePage)
            {
                hero.SetPage("Uma equipe finalizou uma build jog\u00E1vel", gameName);
                AcceptButton = null;
                ActiveControl = btnBack;
            }
            else
            {
                hero.SetPage(null, "");
                AcceptButton = btnDownload;
                ActiveControl = btnDownload;
            }
        }

        private void OnHaveClick(object sender, EventArgs e) { ShowPage(true); }
        private void OnBackClick(object sender, EventArgs e) { ShowPage(false); }

        private void OnBusyTick(object sender, EventArgs e)
        {
            busyTimer.Stop();
            btnDownload.Enabled = true;
            btnDownload.Text = "BAIXAR JOGO";
        }

        private void OnDownloadClick(object sender, EventArgs e)
        {
            banner.Visible = false;
            string error;

            // 1. Ler o magnet do JSON (lido a cada clique: troca sem reabrir)
            LauncherConfig cfg = LauncherConfig.Load(out error);
            if (cfg == null) { banner.ShowMessage(BannerKind.Error, error); return; }
            hero.SetGame(cfg.GameName);
            gameName = cfg.GameName;

            // 2. Validar
            string link;
            if (!MagnetValidator.TryValidate(cfg.MagnetLink, out link, out error))
            {
                banner.ShowMessage(BannerKind.Error, error);
                return;
            }

            // 3. Entregar ao Windows (ShellExecute): ele escolhe o aplicativo do protocolo magnet:
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(link);
                psi.UseShellExecute = true;
                Process p = Process.Start(psi);
                if (p != null) { p.Dispose(); }
                banner.ShowMessage(BannerKind.Success, "Pedido enviado ao Windows. O \u00B5Torrent deve abrir e pedir a confirma\u00E7\u00E3o do download.");
                btnDownload.Text = "ABRINDO...";
                btnDownload.Enabled = false;
                busyTimer.Start();
            }
            catch (Win32Exception)
            {
                banner.ShowMessage(BannerKind.Error, NoHandlerMessage);
            }
            catch (Exception)
            {
                banner.ShowMessage(BannerKind.Error, NoHandlerMessage);
            }
        }
    }
}
