// Ferramenta de Captura - clone da Ferramenta de Recorte nativa do Windows.
// Compila com o csc.exe do .NET Framework 4 (C# 5), sem dependencias externas.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

[assembly: AssemblyTitle("Ferramenta de Captura")]
[assembly: AssemblyProduct("Ferramenta de Captura")]
[assembly: AssemblyVersion("1.0.0.0")]

namespace CapturaTela
{
    enum CaptureMode { Rectangle, Freeform, Window, FullScreen }

    static class Native
    {
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mod, uint vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
        [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr hWnd, int idx);
        [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hWnd, int attr, out RECT r, int size);
        [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hWnd, int attr, out int v, int size);

        public const int WM_HOTKEY = 0x0312;
        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_TOOLWINDOW = 0x80;
        public const int DWMWA_CLOAKED = 14;
        public const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left, Top, Right, Bottom; }

        // Janelas de topo visiveis, em ordem Z (da frente para tras), excluindo este processo.
        public static List<Rectangle> VisibleWindows()
        {
            var list = new List<Rectangle>();
            uint self = (uint)Process.GetCurrentProcess().Id;
            EnumWindows(delegate(IntPtr h, IntPtr l)
            {
                if (!IsWindowVisible(h) || IsIconic(h)) return true;
                uint pid; GetWindowThreadProcessId(h, out pid);
                if (pid == self) return true;
                if ((GetWindowLong(h, GWL_EXSTYLE) & WS_EX_TOOLWINDOW) != 0) return true;
                int cloaked;
                if (DwmGetWindowAttribute(h, DWMWA_CLOAKED, out cloaked, 4) == 0 && cloaked != 0) return true;
                RECT r;
                if (DwmGetWindowAttribute(h, DWMWA_EXTENDED_FRAME_BOUNDS, out r, Marshal.SizeOf(typeof(RECT))) != 0)
                    if (!GetWindowRect(h, out r)) return true;
                var rect = Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
                if (rect.Width < 20 || rect.Height < 20) return true;
                list.Add(rect);
                return true;
            }, IntPtr.Zero);
            return list;
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            Native.SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    // ---------------------------------------------------------------- Selecao

    class OverlayForm : Form
    {
        readonly Bitmap shot;
        readonly CaptureMode mode;
        readonly Point origin;                 // canto do monitor virtual
        readonly List<Rectangle> windows;      // coordenadas de tela
        readonly List<Point> freePoints = new List<Point>();
        Point start, current;
        bool dragging;
        Rectangle hoverWindow = Rectangle.Empty;

        public Bitmap Result;

        public OverlayForm(Bitmap screenshot, CaptureMode m)
        {
            shot = screenshot; mode = m;
            var vs = SystemInformation.VirtualScreen;
            origin = vs.Location;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = vs;
            TopMost = true;
            ShowInTaskbar = false;
            DoubleBuffered = true;
            Cursor = Cursors.Cross;
            KeyPreview = true;
            if (m == CaptureMode.Window) windows = Native.VisibleWindows();
            Shown += delegate { Activate(); Focus(); };
        }

        Rectangle Normalized()
        {
            return Rectangle.FromLTRB(Math.Min(start.X, current.X), Math.Min(start.Y, current.Y),
                                      Math.Max(start.X, current.X), Math.Max(start.Y, current.Y));
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) { Result = null; DialogResult = DialogResult.Cancel; Close(); }
            base.OnKeyDown(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right) { DialogResult = DialogResult.Cancel; Close(); return; }
            if (e.Button != MouseButtons.Left) return;
            if (mode == CaptureMode.Window)
            {
                if (!hoverWindow.IsEmpty) Finish(hoverWindow);
                return;
            }
            dragging = true;
            start = current = e.Location;
            freePoints.Clear();
            freePoints.Add(e.Location);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (mode == CaptureMode.Window)
            {
                var screenPt = new Point(e.X + origin.X, e.Y + origin.Y);
                Rectangle found = Rectangle.Empty;
                foreach (var w in windows) if (w.Contains(screenPt)) { found = w; break; }
                if (found != hoverWindow) { hoverWindow = found; Invalidate(); }
                return;
            }
            if (!dragging) return;
            current = e.Location;
            if (mode == CaptureMode.Freeform) freePoints.Add(e.Location);
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (!dragging || e.Button != MouseButtons.Left) return;
            dragging = false;
            current = e.Location;
            if (mode == CaptureMode.Rectangle)
            {
                var r = Normalized();
                if (r.Width < 3 || r.Height < 3) { Invalidate(); return; }
                Finish(new Rectangle(r.X + origin.X, r.Y + origin.Y, r.Width, r.Height));
            }
            else if (mode == CaptureMode.Freeform)
            {
                if (freePoints.Count < 3) { Invalidate(); return; }
                FinishFreeform();
            }
        }

        // r em coordenadas de tela
        void Finish(Rectangle r)
        {
            var local = new Rectangle(r.X - origin.X, r.Y - origin.Y, r.Width, r.Height);
            local.Intersect(new Rectangle(0, 0, shot.Width, shot.Height));
            if (local.Width < 1 || local.Height < 1) return;
            Result = shot.Clone(local, PixelFormat.Format32bppArgb);
            DialogResult = DialogResult.OK;
            Close();
        }

        void FinishFreeform()
        {
            using (var path = new GraphicsPath())
            {
                path.AddPolygon(freePoints.ToArray());
                var b = Rectangle.Ceiling(path.GetBounds());
                b.Intersect(new Rectangle(0, 0, shot.Width, shot.Height));
                if (b.Width < 1 || b.Height < 1) return;
                var bmp = new Bitmap(b.Width, b.Height, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent);
                    // o caminho esta em coordenadas do overlay; desloca para a origem do recorte
                    using (var clipPath = (GraphicsPath)path.Clone())
                    {
                        var m = new Matrix(1, 0, 0, 1, -b.X, -b.Y);
                        clipPath.Transform(m);
                        g.SetClip(clipPath);
                    }
                    g.DrawImage(shot, new Rectangle(0, 0, b.Width, b.Height), b, GraphicsUnit.Pixel);
                }
                Result = bmp;
            }
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.DrawImageUnscaled(shot, 0, 0);
            using (var dim = new SolidBrush(Color.FromArgb(120, 0, 0, 0)))
            {
                if (mode == CaptureMode.Rectangle && dragging)
                {
                    var r = Normalized();
                    g.FillRectangle(dim, ClientRectangle);
                    if (r.Width > 0 && r.Height > 0)
                    {
                        g.DrawImage(shot, r, r, GraphicsUnit.Pixel);
                        using (var pen = new Pen(Color.White, 1)) { pen.DashStyle = DashStyle.Dash; g.DrawRectangle(pen, r); }
                        DrawSizeLabel(g, r);
                    }
                }
                else if (mode == CaptureMode.Freeform && dragging && freePoints.Count > 2)
                {
                    g.FillRectangle(dim, ClientRectangle);
                    using (var path = new GraphicsPath())
                    {
                        path.AddPolygon(freePoints.ToArray());
                        var old = g.Clip;
                        g.SetClip(path);
                        g.DrawImageUnscaled(shot, 0, 0);
                        g.Clip = old;
                        using (var pen = new Pen(Color.White, 2)) g.DrawPath(pen, path);
                    }
                }
                else if (mode == CaptureMode.Window && !hoverWindow.IsEmpty)
                {
                    g.FillRectangle(dim, ClientRectangle);
                    var r = new Rectangle(hoverWindow.X - origin.X, hoverWindow.Y - origin.Y, hoverWindow.Width, hoverWindow.Height);
                    g.DrawImage(shot, r, r, GraphicsUnit.Pixel);
                    using (var pen = new Pen(Color.FromArgb(0, 120, 215), 3)) g.DrawRectangle(pen, r);
                }
                else
                {
                    g.FillRectangle(dim, ClientRectangle);
                    string hint = mode == CaptureMode.Window ? "Clique na janela para capturar  -  Esc cancela"
                                : mode == CaptureMode.Freeform ? "Desenhe a forma livre  -  Esc cancela"
                                : "Arraste para selecionar a area  -  Esc cancela";
                    using (var f = new Font("Segoe UI", 14))
                    {
                        var sz = g.MeasureString(hint, f);
                        var scr = Screen.PrimaryScreen.Bounds;
                        float x = scr.X - origin.X + (scr.Width - sz.Width) / 2, y = scr.Y - origin.Y + 40;
                        g.FillRectangle(Brushes.Black, x - 12, y - 6, sz.Width + 24, sz.Height + 12);
                        g.DrawString(hint, f, Brushes.White, x, y);
                    }
                }
            }
        }

        void DrawSizeLabel(Graphics g, Rectangle r)
        {
            string t = r.Width + " x " + r.Height;
            using (var f = new Font("Segoe UI", 9))
            {
                var sz = g.MeasureString(t, f);
                float y = r.Bottom + 4;
                if (y + sz.Height > ClientSize.Height) y = r.Top - sz.Height - 4;
                g.FillRectangle(Brushes.Black, r.X, y, sz.Width + 6, sz.Height);
                g.DrawString(t, f, Brushes.White, r.X + 3, y);
            }
        }
    }

    // ---------------------------------------------------------------- Edicao

    class Stroke
    {
        public List<Point> Points = new List<Point>();
        public Color Color;
        public float Width;
        public bool Highlight;
    }

    class Canvas : Control
    {
        public Bitmap Image;
        public readonly List<Stroke> Strokes = new List<Stroke>();
        public string Tool = "pen";
        public Color PenColor = Color.Red;
        public float PenWidth = 3;
        Stroke active;

        public Canvas() { DoubleBuffered = true; }

        public void SetImage(Bitmap bmp)
        {
            Image = bmp; Strokes.Clear();
            Size = bmp == null ? new Size(1, 1) : bmp.Size;
            Invalidate();
        }

        static Pen MakePen(Stroke s)
        {
            Color c = s.Highlight ? Color.FromArgb(110, s.Color) : s.Color;
            var p = new Pen(c, s.Width);
            p.StartCap = p.EndCap = s.Highlight ? LineCap.Flat : LineCap.Round;
            p.LineJoin = LineJoin.Round;
            return p;
        }

        public void DrawStrokes(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            foreach (var s in Strokes)
            {
                using (var p = MakePen(s))
                {
                    if (s.Points.Count == 1)
                    {
                        using (var b = new SolidBrush(p.Color))
                            g.FillEllipse(b, s.Points[0].X - s.Width / 2, s.Points[0].Y - s.Width / 2, s.Width, s.Width);
                    }
                    else if (s.Points.Count > 1) g.DrawLines(p, s.Points.ToArray());
                }
            }
        }

        public Bitmap Flatten()
        {
            var bmp = new Bitmap(Image.Width, Image.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.DrawImageUnscaled(Image, 0, 0);
                DrawStrokes(g);
            }
            return bmp;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Image == null) return;
            e.Graphics.DrawImageUnscaled(Image, 0, 0);
            DrawStrokes(e.Graphics);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (Image == null || e.Button != MouseButtons.Left) return;
            if (Tool == "eraser") { Erase(e.Location); return; }
            bool hl = Tool == "highlighter";
            active = new Stroke { Color = PenColor, Width = hl ? Math.Max(14, PenWidth * 4) : PenWidth, Highlight = hl };
            active.Points.Add(e.Location);
            Strokes.Add(active);
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (Tool == "eraser") { Erase(e.Location); return; }
            if (active == null) return;
            active.Points.Add(e.Location);
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e) { active = null; }

        void Erase(Point p)
        {
            for (int i = Strokes.Count - 1; i >= 0; i--)
            {
                var s = Strokes[i];
                float tol = s.Width / 2 + 6;
                bool hit = false;
                if (s.Points.Count == 1) hit = Dist(p, s.Points[0], s.Points[0]) <= tol;
                for (int k = 1; k < s.Points.Count && !hit; k++)
                    hit = Dist(p, s.Points[k - 1], s.Points[k]) <= tol;
                if (hit) { Strokes.RemoveAt(i); Invalidate(); return; }
            }
        }

        static double Dist(Point p, Point a, Point b)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double len2 = dx * dx + dy * dy;
            double t = len2 == 0 ? 0 : Math.Max(0, Math.Min(1, ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / len2));
            double cx = a.X + t * dx, cy = a.Y + t * dy;
            return Math.Sqrt((p.X - cx) * (p.X - cx) + (p.Y - cy) * (p.Y - cy));
        }
    }

    // ---------------------------------------------------------------- Janela principal

    class MainForm : Form
    {
        const int HotkeyPrintScreen = 1, HotkeyCtrlShiftS = 2;

        readonly ToolStrip bar = new ToolStrip();
        readonly ToolStripComboBox modeBox = new ToolStripComboBox();
        readonly ToolStripComboBox delayBox = new ToolStripComboBox();
        readonly ToolStripComboBox widthBox = new ToolStripComboBox();
        readonly ToolStripButton penBtn, hlBtn, eraserBtn, colorBtn, copyBtn, saveBtn, undoBtn, newBtn;
        readonly Panel scroller = new Panel();
        readonly Canvas canvas = new Canvas();
        readonly Label placeholder = new Label();
        readonly StatusStrip status = new StatusStrip();
        readonly ToolStripStatusLabel statusLabel = new ToolStripStatusLabel();
        bool capturing;

        public MainForm()
        {
            Text = "Ferramenta de Captura";
            ClientSize = new Size(900, 600);
            MinimumSize = new Size(640, 360);
            StartPosition = FormStartPosition.CenterScreen;
            Icon = SystemIcons.Application;
            KeyPreview = true;

            bar.GripStyle = ToolStripGripStyle.Hidden;
            bar.Padding = new Padding(6, 4, 6, 4);

            newBtn = Btn("+ Novo", "Nova captura (Ctrl+N, Print Screen ou Ctrl+Shift+S)", delegate { StartCapture(); });
            newBtn.Font = new Font(Font, FontStyle.Bold);
            bar.Items.Add(newBtn);

            modeBox.DropDownStyle = ComboBoxStyle.DropDownList;
            modeBox.Items.AddRange(new object[] { "Retangulo", "Forma livre", "Janela", "Tela inteira" });
            modeBox.SelectedIndex = 0;
            modeBox.ToolTipText = "Modo de captura";
            modeBox.Width = 110;
            bar.Items.Add(modeBox);

            delayBox.DropDownStyle = ComboBoxStyle.DropDownList;
            delayBox.Items.AddRange(new object[] { "Sem atraso", "1 segundo", "2 segundos", "3 segundos", "5 segundos", "10 segundos" });
            delayBox.SelectedIndex = 0;
            delayBox.ToolTipText = "Atraso antes da captura";
            delayBox.Width = 100;
            bar.Items.Add(delayBox);

            bar.Items.Add(new ToolStripSeparator());
            penBtn = Tool("Caneta", "Caneta", "pen");
            hlBtn = Tool("Marca-texto", "Marca-texto", "highlighter");
            eraserBtn = Tool("Borracha", "Borracha (apaga o traco tocado)", "eraser");
            penBtn.Checked = true;
            colorBtn = Btn("Cor", "Cor da caneta", delegate { PickColor(); });
            widthBox.DropDownStyle = ComboBoxStyle.DropDownList;
            widthBox.Items.AddRange(new object[] { "Fino", "Medio", "Grosso", "Extra grosso" });
            widthBox.SelectedIndex = 1;
            widthBox.Width = 90;
            widthBox.ToolTipText = "Espessura";
            widthBox.SelectedIndexChanged += delegate { ApplyWidth(); };
            bar.Items.AddRange(new ToolStripItem[] { penBtn, hlBtn, eraserBtn, colorBtn, widthBox });
            UpdateColorButton();

            bar.Items.Add(new ToolStripSeparator());
            undoBtn = Btn("Desfazer", "Desfazer ultimo traco (Ctrl+Z)", delegate { Undo(); });
            copyBtn = Btn("Copiar", "Copiar para a area de transferencia (Ctrl+C)", delegate { CopyImage(true); });
            saveBtn = Btn("Salvar", "Salvar como... (Ctrl+S)", delegate { SaveImage(); });
            bar.Items.AddRange(new ToolStripItem[] { undoBtn, copyBtn, saveBtn });

            scroller.Dock = DockStyle.Fill;
            scroller.AutoScroll = true;
            scroller.BackColor = Color.FromArgb(48, 48, 48);
            scroller.Controls.Add(canvas);
            scroller.Resize += delegate { CenterCanvas(); };
            canvas.Visible = false;

            placeholder.Text = "Clique em \"+ Novo\" ou pressione Print Screen / Ctrl+Shift+S\npara fazer uma captura.";
            placeholder.ForeColor = Color.Gainsboro;
            placeholder.TextAlign = ContentAlignment.MiddleCenter;
            placeholder.Font = new Font("Segoe UI", 12);
            placeholder.Dock = DockStyle.Fill;
            scroller.Controls.Add(placeholder);

            status.Items.Add(statusLabel);
            statusLabel.Text = "Pronto";

            Controls.Add(scroller);
            Controls.Add(bar);
            Controls.Add(status);
            ApplyWidth();
            SetEditEnabled(false);
        }

        ToolStripButton Btn(string text, string tip, EventHandler click)
        {
            var b = new ToolStripButton(text);
            b.ToolTipText = tip;
            b.DisplayStyle = ToolStripItemDisplayStyle.Text;
            b.Click += click;
            return b;
        }

        ToolStripButton Tool(string text, string tip, string tool)
        {
            var b = Btn(text, tip, null);
            b.CheckOnClick = false;
            b.Click += delegate
            {
                canvas.Tool = tool;
                penBtn.Checked = tool == "pen";
                hlBtn.Checked = tool == "highlighter";
                eraserBtn.Checked = tool == "eraser";
                canvas.Cursor = tool == "eraser" ? Cursors.No : Cursors.Cross;
            };
            return b;
        }

        void SetEditEnabled(bool on)
        {
            foreach (var b in new ToolStripItem[] { penBtn, hlBtn, eraserBtn, colorBtn, widthBox, undoBtn, copyBtn, saveBtn })
                b.Enabled = on;
        }

        void CenterCanvas()
        {
            int x = Math.Max(0, (scroller.ClientSize.Width - canvas.Width) / 2);
            int y = Math.Max(0, (scroller.ClientSize.Height - canvas.Height) / 2);
            canvas.Location = new Point(x + scroller.AutoScrollPosition.X, y + scroller.AutoScrollPosition.Y);
        }

        void ApplyWidth()
        {
            float[] w = { 2, 4, 8, 14 };
            canvas.PenWidth = w[Math.Max(0, widthBox.SelectedIndex)];
        }

        void PickColor()
        {
            using (var d = new ColorDialog())
            {
                d.Color = canvas.PenColor; d.FullOpen = true;
                if (d.ShowDialog(this) == DialogResult.OK) { canvas.PenColor = d.Color; UpdateColorButton(); }
            }
        }

        void UpdateColorButton()
        {
            colorBtn.ForeColor = canvas.PenColor;
            colorBtn.Text = "■ Cor";
        }

        // ------------------------------------------------------------ Captura

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.RegisterHotKey(Handle, HotkeyPrintScreen, 0, (uint)Keys.PrintScreen);
            Native.RegisterHotKey(Handle, HotkeyCtrlShiftS, 0x2 | 0x4, (uint)Keys.S); // Ctrl+Shift
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            Native.UnregisterHotKey(Handle, HotkeyPrintScreen);
            Native.UnregisterHotKey(Handle, HotkeyCtrlShiftS);
            base.OnHandleDestroyed(e);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_HOTKEY) StartCapture();
            base.WndProc(ref m);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.N) { StartCapture(); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.S) { if (canvas.Image != null) SaveImage(); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.C) { if (canvas.Image != null) CopyImage(true); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.Z) { Undo(); e.Handled = true; }
            base.OnKeyDown(e);
        }

        static readonly int[] Delays = { 0, 1, 2, 3, 5, 10 };

        void StartCapture()
        {
            if (capturing) return;
            capturing = true;
            var mode = (CaptureMode)modeBox.SelectedIndex;
            int delay = Delays[Math.Max(0, delayBox.SelectedIndex)];
            var prevState = WindowState;
            Hide();

            var timer = new Timer();
            timer.Interval = Math.Max(300, delay * 1000);
            timer.Tick += delegate
            {
                timer.Stop(); timer.Dispose();
                try { DoCapture(mode); }
                catch (Exception ex) { MessageBox.Show(ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); }
                finally
                {
                    capturing = false;
                    Show();
                    WindowState = prevState == FormWindowState.Minimized ? FormWindowState.Normal : prevState;
                    Activate();
                }
            };
            timer.Start();
        }

        void DoCapture(CaptureMode mode)
        {
            var vs = SystemInformation.VirtualScreen;
            using (var shot = new Bitmap(vs.Width, vs.Height, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(shot))
                    g.CopyFromScreen(vs.Location, Point.Empty, vs.Size, CopyPixelOperation.SourceCopy);

                Bitmap result = null;
                if (mode == CaptureMode.FullScreen)
                {
                    result = (Bitmap)shot.Clone();
                }
                else
                {
                    using (var ov = new OverlayForm(shot, mode))
                    {
                        if (ov.ShowDialog() == DialogResult.OK) result = ov.Result;
                    }
                }
                if (result != null) ShowResult(result);
            }
        }

        void ShowResult(Bitmap bmp)
        {
            if (canvas.Image != null) canvas.Image.Dispose();
            canvas.SetImage(bmp);
            canvas.Visible = true;
            placeholder.Visible = false;
            scroller.AutoScrollPosition = Point.Empty;
            CenterCanvas();
            SetEditEnabled(true);
            CopyImage(false);
            statusLabel.Text = "Captura: " + bmp.Width + " x " + bmp.Height + " px  -  copiada para a area de transferencia";
        }

        // ------------------------------------------------------------ Acoes

        void Undo()
        {
            if (canvas.Strokes.Count == 0) return;
            canvas.Strokes.RemoveAt(canvas.Strokes.Count - 1);
            canvas.Invalidate();
        }

        void CopyImage(bool announce)
        {
            if (canvas.Image == null) return;
            try
            {
                using (var flat = canvas.Flatten())
                    Clipboard.SetImage(flat);
                if (announce) statusLabel.Text = "Copiado para a area de transferencia";
            }
            catch (Exception ex) { statusLabel.Text = "Falha ao copiar: " + ex.Message; }
        }

        void SaveImage()
        {
            if (canvas.Image == null) return;
            using (var d = new SaveFileDialog())
            {
                d.Title = "Salvar captura";
                d.Filter = "PNG (*.png)|*.png|JPEG (*.jpg)|*.jpg|Bitmap (*.bmp)|*.bmp|GIF (*.gif)|*.gif";
                d.FileName = "Captura " + DateTime.Now.ToString("yyyy-MM-dd HHmmss");
                string pics = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                string dir = Path.Combine(pics, "Capturas de Tela");
                d.InitialDirectory = Directory.Exists(dir) ? dir : pics;
                if (d.ShowDialog(this) != DialogResult.OK) return;
                ImageFormat fmt;
                switch (d.FilterIndex)
                {
                    case 2: fmt = ImageFormat.Jpeg; break;
                    case 3: fmt = ImageFormat.Bmp; break;
                    case 4: fmt = ImageFormat.Gif; break;
                    default: fmt = ImageFormat.Png; break;
                }
                try
                {
                    using (var flat = canvas.Flatten())
                    {
                        if (fmt == ImageFormat.Jpeg)
                        {
                            using (var bg = new Bitmap(flat.Width, flat.Height, PixelFormat.Format24bppRgb))
                            {
                                using (var g = Graphics.FromImage(bg)) { g.Clear(Color.White); g.DrawImageUnscaled(flat, 0, 0); }
                                bg.Save(d.FileName, fmt);
                            }
                        }
                        else flat.Save(d.FileName, fmt);
                    }
                    statusLabel.Text = "Salvo em " + d.FileName;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Nao foi possivel salvar:\n" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}
