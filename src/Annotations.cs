using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace CapturaTela
{
    // Anotacao desenhada sobre a imagem capturada. Coordenadas sempre no espaco da imagem.
    abstract class Annotation
    {
        public Color Color = Color.Red;
        public float Width = 3;

        public abstract void Draw(Graphics g, Bitmap baseImage);
        public abstract bool Hit(Point p);
        public abstract void Offset(int dx, int dy);

        protected static double DistToSegment(PointF p, PointF a, PointF b)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double len2 = dx * dx + dy * dy;
            double t = len2 == 0 ? 0 : Math.Max(0, Math.Min(1, ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / len2));
            double cx = a.X + t * dx, cy = a.Y + t * dy;
            return Math.Sqrt((p.X - cx) * (p.X - cx) + (p.Y - cy) * (p.Y - cy));
        }

        public static Rectangle Normalize(Point a, Point b)
        {
            return Rectangle.FromLTRB(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
        }
    }

    // Caneta e marca-texto: traco a mao livre.
    class StrokeAnn : Annotation
    {
        public List<Point> Points = new List<Point>();
        public bool Highlight;

        public override void Draw(Graphics g, Bitmap baseImage)
        {
            Color c = Highlight ? Color.FromArgb(110, Color) : Color;
            using (var p = new Pen(c, Width))
            {
                p.StartCap = p.EndCap = Highlight ? LineCap.Flat : LineCap.Round;
                p.LineJoin = LineJoin.Round;
                if (Points.Count == 1)
                {
                    using (var b = new SolidBrush(c))
                        g.FillEllipse(b, Points[0].X - Width / 2, Points[0].Y - Width / 2, Width, Width);
                }
                else if (Points.Count > 1) g.DrawLines(p, Points.ToArray());
            }
        }

        public override bool Hit(Point p)
        {
            float tol = Width / 2 + 6;
            if (Points.Count == 1) return DistToSegment(p, Points[0], Points[0]) <= tol;
            for (int k = 1; k < Points.Count; k++)
                if (DistToSegment(p, Points[k - 1], Points[k]) <= tol) return true;
            return false;
        }

        public override void Offset(int dx, int dy)
        {
            for (int i = 0; i < Points.Count; i++) Points[i] = new Point(Points[i].X + dx, Points[i].Y + dy);
        }
    }

    enum ShapeKind { Arrow, Line, Rectangle, Ellipse }

    class ShapeAnn : Annotation
    {
        public ShapeKind Kind;
        public Point A, B;

        public override void Draw(Graphics g, Bitmap baseImage)
        {
            using (var pen = new Pen(Color, Width))
            {
                pen.LineJoin = LineJoin.Round;
                switch (Kind)
                {
                    case ShapeKind.Line:
                        pen.StartCap = pen.EndCap = LineCap.Round;
                        g.DrawLine(pen, A, B);
                        break;
                    case ShapeKind.Arrow:
                        DrawArrow(g, pen);
                        break;
                    case ShapeKind.Rectangle:
                        var r = Normalize(A, B);
                        g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
                        break;
                    case ShapeKind.Ellipse:
                        var e = Normalize(A, B);
                        g.DrawEllipse(pen, e.X, e.Y, e.Width, e.Height);
                        break;
                }
            }
        }

        void DrawArrow(Graphics g, Pen pen)
        {
            double dx = B.X - A.X, dy = B.Y - A.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1) return;
            double ux = dx / len, uy = dy / len;
            double head = Math.Min(len, 12 + Width * 2.5);
            double ang = 0.45; // abertura da ponta em radianos
            PointF tip = B;
            PointF l = new PointF((float)(B.X - head * (ux * Math.Cos(ang) - uy * Math.Sin(ang))),
                                  (float)(B.Y - head * (uy * Math.Cos(ang) + ux * Math.Sin(ang))));
            PointF r = new PointF((float)(B.X - head * (ux * Math.Cos(ang) + uy * Math.Sin(ang))),
                                  (float)(B.Y - head * (uy * Math.Cos(ang) - ux * Math.Sin(ang))));
            pen.StartCap = LineCap.Round;
            // a haste termina antes da ponta para nao vazar pelo triangulo
            g.DrawLine(pen, A, new PointF((float)(B.X - ux * head * 0.7), (float)(B.Y - uy * head * 0.7)));
            using (var br = new SolidBrush(Color))
                g.FillPolygon(br, new PointF[] { tip, l, r });
        }

        public override bool Hit(Point p)
        {
            float tol = Width / 2 + 6;
            if (Kind == ShapeKind.Line || Kind == ShapeKind.Arrow) return DistToSegment(p, A, B) <= tol;
            var r = Normalize(A, B);
            if (Kind == ShapeKind.Rectangle)
            {
                var outer = r; outer.Inflate((int)tol, (int)tol);
                var inner = r; inner.Inflate(-(int)tol, -(int)tol);
                return outer.Contains(p) && (inner.Width <= 0 || inner.Height <= 0 || !inner.Contains(p));
            }
            double a = r.Width / 2.0, b = r.Height / 2.0;
            if (a < 1 || b < 1) return false;
            double nx = (p.X - (r.X + a)) / a, ny = (p.Y - (r.Y + b)) / b;
            double d = Math.Sqrt(nx * nx + ny * ny);
            double band = tol / Math.Min(a, b);
            return Math.Abs(d - 1) <= band;
        }

        public override void Offset(int dx, int dy)
        {
            A = new Point(A.X + dx, A.Y + dy); B = new Point(B.X + dx, B.Y + dy);
        }
    }

    class TextAnn : Annotation
    {
        public Point Pos;
        public string Text = "";
        RectangleF bounds;

        float FontSize { get { return 12 + Width * 2.5f; } }

        public override void Draw(Graphics g, Bitmap baseImage)
        {
            var old = g.TextRenderingHint;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            using (var f = new Font("Segoe UI", FontSize, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var br = new SolidBrush(Color))
            {
                var size = g.MeasureString(Text, f);
                bounds = new RectangleF(Pos.X, Pos.Y, size.Width, size.Height);
                g.DrawString(Text, f, br, Pos.X, Pos.Y);
            }
            g.TextRenderingHint = old;
        }

        public override bool Hit(Point p)
        {
            var r = bounds; r.Inflate(4, 4);
            return r.Contains(p);
        }

        public override void Offset(int dx, int dy) { Pos = new Point(Pos.X + dx, Pos.Y + dy); }
    }

    // Marcador numerado (1, 2, 3...) para indicar passos.
    class StepAnn : Annotation
    {
        public Point Center;
        public int Number;

        float Radius { get { return 10 + Width * 1.2f; } }

        public override void Draw(Graphics g, Bitmap baseImage)
        {
            float r = Radius;
            using (var br = new SolidBrush(Color))
                g.FillEllipse(br, Center.X - r, Center.Y - r, r * 2, r * 2);
            using (var pen = new Pen(Color.White, 2))
                g.DrawEllipse(pen, Center.X - r, Center.Y - r, r * 2, r * 2);
            using (var f = new Font("Segoe UI", r * 1.1f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                // texto escuro ou claro conforme o brilho do fundo
                bool light = Color.GetBrightness() > 0.65f;
                g.DrawString(Number.ToString(), f, light ? Brushes.Black : Brushes.White,
                             new RectangleF(Center.X - r, Center.Y - r, r * 2, r * 2), sf);
            }
        }

        public override bool Hit(Point p)
        {
            double dx = p.X - Center.X, dy = p.Y - Center.Y;
            return Math.Sqrt(dx * dx + dy * dy) <= Radius + 2;
        }

        public override void Offset(int dx, int dy) { Center = new Point(Center.X + dx, Center.Y + dy); }
    }

    // Desfoque ou pixelizacao: sempre calculado a partir da imagem original,
    // entao o conteudo escondido nao pode ser recuperado da exportacao.
    class BlurAnn : Annotation
    {
        public Rectangle Rect;
        public bool Pixelate;

        public override void Draw(Graphics g, Bitmap baseImage)
        {
            var r = Rect;
            r.Intersect(new Rectangle(0, 0, baseImage.Width, baseImage.Height));
            if (r.Width < 1 || r.Height < 1) return;

            float factor = 4 + Width * 1.5f;                      // quanto maior, mais forte
            int sw = Math.Max(1, (int)(r.Width / factor)), sh = Math.Max(1, (int)(r.Height / factor));
            using (var small = new Bitmap(sw, sh, PixelFormat.Format32bppArgb))
            {
                using (var sg = Graphics.FromImage(small))
                {
                    sg.InterpolationMode = InterpolationMode.HighQualityBilinear;
                    sg.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    sg.DrawImage(baseImage, new Rectangle(0, 0, sw, sh), r, GraphicsUnit.Pixel);
                }
                var oldI = g.InterpolationMode; var oldP = g.PixelOffsetMode;
                g.InterpolationMode = Pixelate ? InterpolationMode.NearestNeighbor : InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                var oldClip = g.Clip;
                g.SetClip(r, CombineMode.Intersect);
                g.DrawImage(small, r, 0, 0, sw, sh, GraphicsUnit.Pixel);
                g.Clip = oldClip;
                g.InterpolationMode = oldI; g.PixelOffsetMode = oldP;
            }
        }

        public override bool Hit(Point p) { return Rect.Contains(p); }
        public override void Offset(int dx, int dy) { Rect.Offset(dx, dy); }
    }

    // ---- historico de edicao (desfazer / refazer) ----

    interface IEdit
    {
        void Undo(Canvas c);
        void Redo(Canvas c);
    }

    class AddEdit : IEdit
    {
        readonly Annotation a;
        public AddEdit(Annotation a) { this.a = a; }
        public void Undo(Canvas c) { c.Annotations.Remove(a); }
        public void Redo(Canvas c) { c.Annotations.Add(a); }
    }

    class RemoveEdit : IEdit
    {
        readonly Annotation a; readonly int index;
        public RemoveEdit(Annotation a, int index) { this.a = a; this.index = index; }
        public void Undo(Canvas c) { c.Annotations.Insert(Math.Min(index, c.Annotations.Count), a); }
        public void Redo(Canvas c) { c.Annotations.Remove(a); }
    }

    class CropEdit : IEdit
    {
        readonly Bitmap before, after; readonly Rectangle rect;
        public CropEdit(Bitmap before, Bitmap after, Rectangle rect) { this.before = before; this.after = after; this.rect = rect; }
        public void Undo(Canvas c) { c.ReplaceImage(before); foreach (var a in c.Annotations) a.Offset(rect.X, rect.Y); }
        public void Redo(Canvas c) { c.ReplaceImage(after); foreach (var a in c.Annotations) a.Offset(-rect.X, -rect.Y); }
    }
}
