using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace CapturaTela
{
    // Tela congelada em tela cheia onde o usuario escolhe a area a capturar.
    // Uma barra flutuante no topo permite trocar de modo sem voltar a janela principal.
    class OverlayForm : Form
    {
        static readonly string[] ModeNames = ModeList.Names;

        // painel mais largo que a lupa para caber "x, y   #RRGGBB" (coordenadas grandes em varios monitores)
        const int LoupeCells = 15, LoupeScale = 8, LoupePanelW = 196;

        readonly Bitmap shot;
        CaptureMode mode;
        readonly Point origin;                 // canto do monitor virtual
        List<Rectangle> windows;               // coordenadas de tela
        readonly List<Point> freePoints = new List<Point>();
        readonly List<Rectangle> barButtons = new List<Rectangle>();
        Point start, current, mouse;
        bool dragging;
        int hoverButton = -1;
        Rectangle hoverWindow = Rectangle.Empty;
        Rectangle lastLoupe = Rectangle.Empty;

        // saidas
        public Bitmap Result;                  // captura pronta (retangulo, forma livre, janela, tela inteira)
        public Rectangle SelectedRect;         // area em coordenadas de tela (rolagem e gravacao)
        public CaptureMode ChosenMode { get { return mode; } }

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
            LayoutBar();
            Shown += delegate
            {
                Activate(); Focus();
                mouse = PointToClient(Cursor.Position);
                Invalidate();
            };
        }

        // ---- barra de modos ----

        void LayoutBar()
        {
            barButtons.Clear();
            var scr = Screen.PrimaryScreen.Bounds;
            // largura de cada botao = largura do texto + folga, para nada quebrar em duas linhas
            int h = 36, gap = 4, pad = 32, closeW = 44;
            var widths = new int[ModeNames.Length];
            using (var g = CreateGraphics())
            using (var f = new Font("Segoe UI", 9.5f))
                for (int i = 0; i < widths.Length; i++) widths[i] = (int)Math.Ceiling(g.MeasureString(ModeNames[i], f).Width) + pad;
            int total = closeW + gap * widths.Length;
            foreach (int wd in widths) total += wd;
            int x = scr.X - origin.X + (scr.Width - total) / 2, y = scr.Y - origin.Y + 14;
            for (int i = 0; i < widths.Length; i++) { barButtons.Add(new Rectangle(x, y, widths[i], h)); x += widths[i] + gap; }
            barButtons.Add(new Rectangle(x, y, closeW, h)); // fechar
        }

        Rectangle BarBounds
        {
            get
            {
                var r = barButtons[0];
                var last = barButtons[barButtons.Count - 1];
                var b = Rectangle.FromLTRB(r.Left, r.Top, last.Right, last.Bottom);
                b.Inflate(8, 8);
                return b;
            }
        }

        int ButtonAt(Point p)
        {
            for (int i = 0; i < barButtons.Count; i++) if (barButtons[i].Contains(p)) return i;
            return -1;
        }

        void SetMode(CaptureMode m)
        {
            if (m == CaptureMode.FullScreen)
            {
                Result = (Bitmap)shot.Clone();
                mode = m;
                DialogResult = DialogResult.OK; Close();
                return;
            }
            mode = m;
            dragging = false;
            hoverWindow = Rectangle.Empty;
            freePoints.Clear();
            Invalidate();
        }

        // ---- teclado ----

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // setas movem o cursor 1 pixel para acertar a borda da selecao
            int dx = 0, dy = 0;
            switch (keyData)
            {
                case Keys.Left: dx = -1; break;
                case Keys.Right: dx = 1; break;
                case Keys.Up: dy = -1; break;
                case Keys.Down: dy = 1; break;
            }
            if (dx != 0 || dy != 0)
            {
                var p = Cursor.Position;
                Cursor.Position = new Point(p.X + dx, p.Y + dy);
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) { Result = null; DialogResult = DialogResult.Cancel; Close(); }
            else if (e.KeyCode >= Keys.D1 && e.KeyCode < Keys.D1 + ModeList.Order.Length) SetMode(ModeList.Order[e.KeyCode - Keys.D1]);
            base.OnKeyDown(e);
        }

        // ---- mouse ----

        Rectangle Normalized()
        {
            return Rectangle.FromLTRB(Math.Min(start.X, current.X), Math.Min(start.Y, current.Y),
                                      Math.Max(start.X, current.X), Math.Max(start.Y, current.Y));
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right) { DialogResult = DialogResult.Cancel; Close(); return; }
            if (e.Button != MouseButtons.Left) return;

            int b = ButtonAt(e.Location);
            if (b >= 0)
            {
                if (b == barButtons.Count - 1) { DialogResult = DialogResult.Cancel; Close(); }
                else SetMode(ModeList.Order[b]);
                return;
            }
            if (BarBounds.Contains(e.Location)) return;

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
            int hb = ButtonAt(e.Location);
            bool onBar = BarBounds.Contains(e.Location);
            Cursor = onBar && !dragging ? Cursors.Arrow : Cursors.Cross;
            if (hb != hoverButton) { hoverButton = hb; Invalidate(BarBounds); }

            mouse = e.Location;
            if (mode == CaptureMode.Window)
            {
                if (windows == null) windows = Native.VisibleWindows();
                var screenPt = new Point(e.X + origin.X, e.Y + origin.Y);
                Rectangle found = Rectangle.Empty;
                if (!onBar) foreach (var w in windows) if (w.Contains(screenPt)) { found = w; break; }
                if (found != hoverWindow) { hoverWindow = found; Invalidate(); }
                return;
            }
            if (dragging)
            {
                current = e.Location;
                if (mode == CaptureMode.Freeform) freePoints.Add(e.Location);
                Invalidate();
            }
            else InvalidateLoupe();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (!dragging || e.Button != MouseButtons.Left) return;
            dragging = false;
            current = e.Location;
            if (mode == CaptureMode.Freeform)
            {
                if (freePoints.Count < 3) { Invalidate(); return; }
                FinishFreeform();
                return;
            }
            var r = Normalized();
            if (r.Width < 3 || r.Height < 3) { Invalidate(); return; }
            var screenRect = new Rectangle(r.X + origin.X, r.Y + origin.Y, r.Width, r.Height);
            if (mode == CaptureMode.Rectangle) Finish(screenRect);
            else
            {
                // rolagem / gravacao: so devolve a area escolhida
                SelectedRect = screenRect;
                DialogResult = DialogResult.OK; Close();
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

        // ---- pintura ----

        Rectangle LoupeRect()
        {
            int size = LoupeCells * LoupeScale;
            int x = mouse.X + 24, y = mouse.Y + 24;
            if (x + LoupePanelW + 4 > ClientSize.Width) x = mouse.X - 24 - LoupePanelW;
            if (y + size + 40 > ClientSize.Height) y = mouse.Y - 24 - size - 40;
            return new Rectangle(x, y, LoupePanelW, size + 36);
        }

        bool LoupeVisible
        {
            get { return mode != CaptureMode.Window && mode != CaptureMode.FullScreen && !BarBounds.Contains(mouse); }
        }

        void InvalidateLoupe()
        {
            var r = LoupeRect(); r.Inflate(6, 6);
            var inv = Rectangle.Union(r, lastLoupe);
            lastLoupe = r;
            Invalidate(inv);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.DrawImageUnscaled(shot, 0, 0);
            using (var dim = new SolidBrush(Color.FromArgb(120, 0, 0, 0)))
            {
                bool selecting = dragging && mode != CaptureMode.Window && mode != CaptureMode.FullScreen;
                if (selecting && mode != CaptureMode.Freeform)
                {
                    var r = Normalized();
                    g.FillRectangle(dim, ClientRectangle);
                    if (r.Width > 0 && r.Height > 0)
                    {
                        g.DrawImage(shot, r, r, GraphicsUnit.Pixel);
                        using (var pen = new Pen(mode == CaptureMode.Record ? Color.Red : Color.White, 1)) { pen.DashStyle = DashStyle.Dash; g.DrawRectangle(pen, r); }
                        DrawSizeLabel(g, r);
                    }
                }
                else if (selecting && freePoints.Count > 2)
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
                    DrawHint(g);
                }
            }
            g.SmoothingMode = SmoothingMode.AntiAlias;
            DrawBar(g);
            if (LoupeVisible) DrawLoupe(g);
        }

        void DrawHint(Graphics g)
        {
            string hint = mode == CaptureMode.Window ? "Clique na janela para capturar"
                        : mode == CaptureMode.Freeform ? "Desenhe a forma livre"
                        : mode == CaptureMode.Scroll ? "Selecione a área com conteúdo rolável (o app rola sozinho com a roda do mouse)"
                        : mode == CaptureMode.ScrollManual ? "Selecione a área com conteúdo rolável (você mesmo rola, devagar, com a roda do mouse)"
                        : mode == CaptureMode.Record ? "Selecione a área para gravar em GIF"
                        : "Arraste para selecionar a área";
            hint += "  -  Esc cancela  -  setas ajustam 1 px";
            using (var f = new Font("Segoe UI", 12))
            {
                var sz = g.MeasureString(hint, f);
                var bar = BarBounds;
                float x = bar.X + (bar.Width - sz.Width) / 2, y = bar.Bottom + 8;
                using (var bg = new SolidBrush(Color.FromArgb(200, 0, 0, 0)))
                    g.FillRectangle(bg, x - 10, y - 4, sz.Width + 20, sz.Height + 8);
                g.DrawString(hint, f, Brushes.White, x, y);
            }
        }

        void DrawBar(Graphics g)
        {
            var bb = BarBounds;
            using (var bg = new SolidBrush(Color.FromArgb(235, 28, 28, 28)))
                g.FillRectangle(bg, bb);
            using (var f = new Font("Segoe UI", 9.5f))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                for (int i = 0; i < barButtons.Count; i++)
                {
                    var r = barButtons[i];
                    bool selected = i == ModeList.IndexOf(mode), hover = i == hoverButton;
                    if (selected || hover)
                        using (var b = new SolidBrush(selected ? Color.FromArgb(0, 120, 215) : Color.FromArgb(70, 70, 70)))
                            g.FillRectangle(b, r);
                    string label = i < ModeNames.Length ? ModeNames[i] : "X";
                    g.DrawString(label, f, Brushes.White, r, sf);
                }
            }
        }

        void DrawLoupe(Graphics g)
        {
            var panel = LoupeRect();
            int size = LoupeCells * LoupeScale;
            var box = new Rectangle(panel.X + (panel.Width - size) / 2, panel.Y, size, size);   // lupa centralizada no painel
            int half = LoupeCells / 2;
            var src = new Rectangle(mouse.X - half, mouse.Y - half, LoupeCells, LoupeCells);

            var oldI = g.InterpolationMode; var oldP = g.PixelOffsetMode; var oldS = g.SmoothingMode;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.SmoothingMode = SmoothingMode.None;
            g.FillRectangle(Brushes.Black, panel.X - 2, panel.Y - 2, panel.Width + 4, panel.Height + 4);
            g.DrawImage(shot, new Rectangle(box.X, box.Y, size, size), src, GraphicsUnit.Pixel);
            g.InterpolationMode = oldI; g.PixelOffsetMode = oldP;

            // pixel central e mira
            int c = half * LoupeScale;
            using (var pen = new Pen(Color.FromArgb(200, 255, 255, 255)))
            {
                g.DrawRectangle(pen, box.X + c, box.Y + c, LoupeScale, LoupeScale);
                g.DrawLine(pen, box.X, box.Y + c + LoupeScale / 2, box.X + c, box.Y + c + LoupeScale / 2);
                g.DrawLine(pen, box.X + c + LoupeScale, box.Y + c + LoupeScale / 2, box.X + size, box.Y + c + LoupeScale / 2);
                g.DrawLine(pen, box.X + c + LoupeScale / 2, box.Y, box.X + c + LoupeScale / 2, box.Y + c);
                g.DrawLine(pen, box.X + c + LoupeScale / 2, box.Y + c + LoupeScale, box.X + c + LoupeScale / 2, box.Y + size);
            }

            Color px = Color.Black;
            if (mouse.X >= 0 && mouse.Y >= 0 && mouse.X < shot.Width && mouse.Y < shot.Height) px = shot.GetPixel(mouse.X, mouse.Y);
            string text = (mouse.X + origin.X) + ", " + (mouse.Y + origin.Y) + "   #" + px.R.ToString("X2") + px.G.ToString("X2") + px.B.ToString("X2");
            using (var f = new Font("Segoe UI", 9))
            using (var sw = new SolidBrush(px))
            {
                g.FillRectangle(sw, panel.X + 6, panel.Y + size + 6, 18, 18);
                g.DrawRectangle(Pens.White, panel.X + 6, panel.Y + size + 6, 18, 18);
                g.DrawString(text, f, Brushes.White, panel.X + 30, panel.Y + size + 7);
            }
            g.SmoothingMode = oldS;
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
}
