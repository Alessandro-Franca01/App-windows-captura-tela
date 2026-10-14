using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace CapturaTela
{
    class Stroke
    {
        public List<Point> Points = new List<Point>();
        public Color Color;
        public float Width;
        public bool Highlight;
    }

    // Area de edicao: mostra a imagem capturada e as anotacoes por cima.
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
}
