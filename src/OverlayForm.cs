using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace CapturaTela
{
    // Tela congelada em tela cheia onde o usuario escolhe a area a capturar.
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
}
