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
        Bitmap bright, dimmed;                 // copias PArgb (pintura rapida): a captura e a captura ja escurecida
        Rectangle lastSel = Rectangle.Empty;   // ultima selecao pintada (para invalidar so o que mudou)
        int fMinX, fMinY, fMaxX, fMaxY;
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
        float sc = 1f;                         // escala do Windows (DPI / 96)

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
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            DoubleBuffered = true;
            bright = new Bitmap(shot.Width, shot.Height, PixelFormat.Format32bppPArgb);
            using (var gb = Graphics.FromImage(bright)) { gb.CompositingMode = CompositingMode.SourceCopy; gb.DrawImageUnscaled(shot, 0, 0); }
            dimmed = (Bitmap)bright.Clone();
            using (var gd = Graphics.FromImage(dimmed))
            using (var db = new SolidBrush(Color.FromArgb(120, 0, 0, 0))) gd.FillRectangle(db, 0, 0, dimmed.Width, dimmed.Height);
            Cursor = Cursors.Cross;
            KeyPreview = true;
            using (var gdi = CreateGraphics()) sc = gdi.DpiX / 96f;
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
            // cada botao = icone + nome + tecla de atalho; a largura vem do texto (nada quebra em duas linhas)
            int h = (int)(40 * sc), gap = (int)(4 * sc), padX = (int)(14 * sc), icon = (int)(20 * sc), g1 = (int)(8 * sc), digitW = (int)(14 * sc), closeW = (int)(44 * sc);
            var widths = new int[ModeNames.Length];
            using (var g = CreateGraphics())
            using (var f = new Font("Segoe UI", 9.5f))
                for (int i = 0; i < widths.Length; i++)
                    widths[i] = padX + icon + g1 + TextRenderer.MeasureText(g, ModeNames[i], f, new Size(1000, 100), TextFormatFlags.NoPadding).Width + g1 + digitW + padX;
            int total = closeW + gap * widths.Length;
            foreach (int wd in widths) total += wd;
            int x = scr.X - origin.X + (scr.Width - total) / 2, y = scr.Y - origin.Y + (int)(18 * sc);
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
                int pad = (int)(6 * sc);
                b.Inflate(pad, pad);
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
            fMinX = fMaxX = e.X; fMinY = fMaxY = e.Y;
            lastSel = Rectangle.Empty;
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
                if (found != hoverWindow)
                {
                    var inv = WindowLocal(hoverWindow);
                    hoverWindow = found;
                    inv = UnionNonEmpty(inv, WindowLocal(found)); inv.Inflate(6, 6);
                    Invalidate(inv);
                }
                return;
            }
            if (dragging)
            {
                current = e.Location;
                if (mode == CaptureMode.Freeform)
                {
                    var lp = freePoints[freePoints.Count - 1];
                    if (Math.Abs(e.X - lp.X) + Math.Abs(e.Y - lp.Y) >= 4) freePoints.Add(e.Location);   // menos pontos = caminho leve
                    fMinX = Math.Min(fMinX, e.X); fMaxX = Math.Max(fMaxX, e.X); fMinY = Math.Min(fMinY, e.Y); fMaxY = Math.Max(fMaxY, e.Y);
                }
                InvalidateSelection();
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
                path.FillMode = FillMode.Winding;
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

        static Rectangle UnionNonEmpty(Rectangle a, Rectangle b)
        {
            if (a.IsEmpty) return b;
            if (b.IsEmpty) return a;
            return Rectangle.Union(a, b);
        }

        Rectangle WindowLocal(Rectangle w)
        {
            return w.IsEmpty ? w : new Rectangle(w.X - origin.X, w.Y - origin.Y, w.Width, w.Height);
        }

        // durante o arraste so repinta a regiao que mudou (a tela toda a cada movimento piscava em telas grandes)
        void InvalidateSelection()
        {
            Rectangle cur = mode == CaptureMode.Freeform ? Rectangle.FromLTRB(fMinX, fMinY, fMaxX + 1, fMaxY + 1) : Normalized();
            var inv = UnionNonEmpty(cur, lastSel);
            lastSel = cur;
            inv.Inflate((int)(60 * sc), (int)(60 * sc));   // rotulo de tamanho e contorno
            Invalidate(inv);
            InvalidateLoupe();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { if (bright != null) bright.Dispose(); if (dimmed != null) dimmed.Dispose(); }
            base.Dispose(disposing);
        }

        void InvalidateLoupe()
        {
            var r = LoupeRect(); r.Height += 14; r.Inflate(12, 12);   // DrawLoupe pinta 14 px alem do painel (sombra incluida)
            var inv = Rectangle.Union(r, lastLoupe);
            lastLoupe = r;
            Invalidate(inv);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var clip = e.ClipRectangle;
            g.CompositingMode = CompositingMode.SourceCopy;
            g.DrawImage(dimmed, clip, clip, GraphicsUnit.Pixel);   // fundo ja escurecido
            g.CompositingMode = CompositingMode.SourceOver;
            bool selecting = dragging && mode != CaptureMode.Window && mode != CaptureMode.FullScreen;
            if (selecting && mode != CaptureMode.Freeform)
            {
                var r = Normalized();
                if (r.Width > 0 && r.Height > 0)
                {
                    g.DrawImage(bright, r, r, GraphicsUnit.Pixel);
                    using (var pen = new Pen(mode == CaptureMode.Record ? Color.Red : Color.White, 1)) { pen.DashStyle = DashStyle.Dash; g.DrawRectangle(pen, r); }
                    DrawSizeLabel(g, r);
                }
            }
            else if (selecting && freePoints.Count > 2)
            {
                using (var path = new GraphicsPath())
                {
                    path.AddPolygon(freePoints.ToArray());
                    path.FillMode = FillMode.Winding;
                    using (var tb = new TextureBrush(bright)) g.FillPath(tb, path);   // preenche com a captura (recortar por regiao era lento)
                    using (var pen = new Pen(Color.White, 2)) g.DrawPath(pen, path);
                }
            }
            else if (mode == CaptureMode.Window && !hoverWindow.IsEmpty)
            {
                var r = WindowLocal(hoverWindow);
                g.DrawImage(bright, r, r, GraphicsUnit.Pixel);
                using (var pen = new Pen(Color.FromArgb(0, 120, 215), 3)) g.DrawRectangle(pen, r);
            }
            else DrawHint(g);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            DrawBar(g);
            if (LoupeVisible) DrawLoupe(g);
        }

        // ---- HUD: barra de modos, dica, rotulo de tamanho e lupa (mesmo estilo, cantos arredondados) ----

        static readonly Color HudBack = Color.FromArgb(236, 28, 28, 28);
        static readonly Color HudMuted = Color.FromArgb(0xB8, 0xB8, 0xB8);

        void FillHud(Graphics g, RectangleF r, float radius, int alpha)
        {
            using (var path = AppRenderer.RoundPath(r, radius))
            {
                using (var b = new SolidBrush(Color.FromArgb(alpha, 28, 28, 28))) g.FillPath(b, path);
                using (var p = new Pen(Color.FromArgb(46, 255, 255, 255), 1f)) g.DrawPath(p, path);
            }
        }

        void DrawHint(Graphics g)
        {
            string main = mode == CaptureMode.Window ? "Clique na janela para capturar"
                        : mode == CaptureMode.Freeform ? "Desenhe a forma livre"
                        : mode == CaptureMode.Scroll ? "Selecione a área com conteúdo rolável (o app rola sozinho com a roda do mouse)"
                        : mode == CaptureMode.ScrollManual ? "Selecione a área com conteúdo rolável (você mesmo rola, devagar, com a roda do mouse)"
                        : mode == CaptureMode.Record ? "Selecione a área para gravar em GIF"
                        : "Arraste para selecionar a área";
            const string sub = "Esc cancela  ·  setas ajustam 1 px";
            using (var fm = new Font("Segoe UI Semibold", 11.5f))
            using (var fs = new Font("Segoe UI", 9.5f))
            {
                var flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
                var sm = TextRenderer.MeasureText(g, main, fm, new Size(2000, 100), flags);
                var ss = TextRenderer.MeasureText(g, sub, fs, new Size(2000, 100), flags);
                int padX = (int)(18 * sc), padY = (int)(12 * sc), gap = (int)(4 * sc);
                int w = Math.Max(sm.Width, ss.Width) + padX * 2, h = sm.Height + gap + ss.Height + padY * 2;
                var bar = BarBounds;
                var r = new Rectangle(bar.X + (bar.Width - w) / 2, bar.Bottom + (int)(10 * sc), w, h);
                FillHud(g, r, 10 * sc, 220);
                TextRenderer.DrawText(g, main, fm, new Rectangle(r.X, r.Y + padY, r.Width, sm.Height), Color.White, flags | TextFormatFlags.HorizontalCenter);
                TextRenderer.DrawText(g, sub, fs, new Rectangle(r.X, r.Y + padY + sm.Height + gap, r.Width, ss.Height), HudMuted, flags | TextFormatFlags.HorizontalCenter);
            }
        }

        void DrawBar(Graphics g)
        {
            var bb = BarBounds;
            FillHud(g, bb, 14 * sc, 236);
            int padX = (int)(14 * sc), icon = (int)(20 * sc), g1 = (int)(8 * sc);
            var flags = TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.Left;
            using (var f = new Font("Segoe UI", 9.5f))
            using (var fb = new Font("Segoe UI Semibold", 9.5f))
            using (var fd = new Font("Segoe UI", 8.5f))
            {
                for (int i = 0; i < barButtons.Count; i++)
                {
                    var r = barButtons[i];
                    bool selected = i == ModeList.IndexOf(mode) && i < ModeNames.Length, hover = i == hoverButton;
                    if (selected || hover)
                        using (var path = AppRenderer.RoundPath(new RectangleF(r.X, r.Y, r.Width, r.Height), 9 * sc))
                        using (var b = new SolidBrush(selected ? Theme.AccentStrong : Color.FromArgb(46, 255, 255, 255)))
                            g.FillPath(b, path);

                    if (i >= ModeNames.Length)
                    {
                        int sz = (int)(18 * sc);
                        Icons.Draw(g, IconKind.Close, new Rectangle(r.X + (r.Width - sz) / 2, r.Y + (r.Height - sz) / 2, sz, sz), Color.White);
                        continue;
                    }
                    Icons.Draw(g, ModeList.IconOf(ModeList.Order[i]), new Rectangle(r.X + padX, r.Y + (r.Height - icon) / 2, icon, icon), Color.White);
                    int tx = r.X + padX + icon + g1;
                    TextRenderer.DrawText(g, ModeNames[i], selected ? fb : f, new Rectangle(tx, r.Y, r.Width, r.Height), Color.White, flags);
                    var dsz = TextRenderer.MeasureText(g, (i + 1).ToString(), fd, new Size(100, 100), TextFormatFlags.NoPadding);
                    TextRenderer.DrawText(g, (i + 1).ToString(), fd, new Rectangle(r.Right - padX - dsz.Width + (int)(2 * sc), r.Y, dsz.Width + 4, r.Height),
                        selected ? Color.FromArgb(210, 255, 255, 255) : HudMuted, flags);
                }
            }
        }

        void DrawLoupe(Graphics g)
        {
            var panel = LoupeRect();
            int size = LoupeCells * LoupeScale;
            var box = new Rectangle(panel.X + (panel.Width - size) / 2, panel.Y + 8, size, size);   // lupa centralizada no painel
            int half = LoupeCells / 2;
            var src = new Rectangle(mouse.X - half, mouse.Y - half, LoupeCells, LoupeCells);
            var full = new Rectangle(panel.X, panel.Y, panel.Width, panel.Height + 14);
            FillHud(g, full, 10 * sc, 244);

            var oldI = g.InterpolationMode; var oldP = g.PixelOffsetMode; var oldS = g.SmoothingMode;
            using (var clip = AppRenderer.RoundPath(box, 6 * sc))
            {
                var oldClip = g.Clip;
                g.SetClip(clip, CombineMode.Replace);
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(shot, box, src, GraphicsUnit.Pixel);
                g.Clip = oldClip;
            }
            g.InterpolationMode = oldI; g.PixelOffsetMode = oldP;

            // pixel central e mira
            int c = half * LoupeScale;
            g.SmoothingMode = SmoothingMode.None;
            using (var pen = new Pen(Color.FromArgb(210, 255, 255, 255)))
            {
                g.DrawRectangle(pen, box.X + c, box.Y + c, LoupeScale, LoupeScale);
                g.DrawLine(pen, box.X, box.Y + c + LoupeScale / 2, box.X + c, box.Y + c + LoupeScale / 2);
                g.DrawLine(pen, box.X + c + LoupeScale, box.Y + c + LoupeScale / 2, box.X + size, box.Y + c + LoupeScale / 2);
                g.DrawLine(pen, box.X + c + LoupeScale / 2, box.Y, box.X + c + LoupeScale / 2, box.Y + c);
                g.DrawLine(pen, box.X + c + LoupeScale / 2, box.Y + c + LoupeScale, box.X + c + LoupeScale / 2, box.Y + size);
            }
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color px = Color.Black;
            if (mouse.X >= 0 && mouse.Y >= 0 && mouse.X < shot.Width && mouse.Y < shot.Height) px = shot.GetPixel(mouse.X, mouse.Y);
            string coords = (mouse.X + origin.X) + ", " + (mouse.Y + origin.Y);
            string hex = "#" + px.R.ToString("X2") + px.G.ToString("X2") + px.B.ToString("X2");
            int sw = 18, sy = box.Bottom + 10;
            using (var swatch = AppRenderer.RoundPath(new RectangleF(panel.X + 12, sy, sw, sw), 4))
            {
                using (var b = new SolidBrush(px)) g.FillPath(b, swatch);
                using (var p = new Pen(Color.FromArgb(170, 255, 255, 255), 1f)) g.DrawPath(p, swatch);
            }
            using (var f = new Font("Segoe UI Semibold", 9f))
            using (var fm = new Font("Segoe UI", 9f))
            {
                var fl = TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine;
                TextRenderer.DrawText(g, coords, f, new Rectangle(panel.X + 38, sy - 1, 100, sw + 2), Color.White, fl);
                TextRenderer.DrawText(g, hex, fm, new Rectangle(panel.X + 38 + 98, sy - 1, 70, sw + 2), HudMuted, fl);
            }
            g.SmoothingMode = oldS;
        }

        void DrawSizeLabel(Graphics g, Rectangle r)
        {
            string t = r.Width + " × " + r.Height + " px";
            using (var f = new Font("Segoe UI Semibold", 9.5f))
            {
                var flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
                var sz = TextRenderer.MeasureText(g, t, f, new Size(500, 100), flags);
                int padX = (int)(10 * sc), padY = (int)(5 * sc);
                int w = sz.Width + padX * 2, h = sz.Height + padY * 2;
                int y = r.Bottom + (int)(8 * sc);
                if (y + h > ClientSize.Height) y = r.Top - h - (int)(8 * sc);
                var tag = new Rectangle(r.X, y, w, h);
                FillHud(g, tag, 6 * sc, 230);
                TextRenderer.DrawText(g, t, f, new Rectangle(tag.X + padX, tag.Y + padY, sz.Width, sz.Height), Color.White, flags);
            }
        }
    }
}