using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace CapturaTela
{
    enum IconKind
    {
        Capture, ModeRect, ModeFree, ModeWindow, ModeScreen, ScrollAuto, ScrollManual, Gif, Clock,
        Undo, Redo, Copy, Save, Ocr,
        Pen, Highlighter, Arrow, Line, Rect, Ellipse, Text, Step, Blur, Pixelate, Crop, Picker, Eraser,
        ZoomOut, ZoomIn, Fit, Help, Settings, Shield, Close, Chevron, Check, Cross, Dot
    }

    // Marca um item cujo desenho e uma amostra de espessura de traco (menu de espessura).
    class WidthSample
    {
        public readonly float Width;
        public WidthSample(float width) { Width = width; }
    }

    // Conjunto de icones de traco (grade de 24 unidades, traco de 1,6, pontas redondas), desenhados
    // em GDI+: ficam nitidos em qualquer escala e seguem a cor do tema/estado, sem arquivos externos.
    static class Icons
    {
        const float Stroke = 1.6f;

        public static Bitmap Placeholder(int px)
        {
            return new Bitmap(Math.Max(1, px), Math.Max(1, px));   // transparente: so reserva o espaco no layout
        }

        public static void Draw(Graphics g, IconKind kind, Rectangle bounds, Color color)
        {
            var state = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            float s = Math.Min(bounds.Width, bounds.Height) / 24f;
            g.TranslateTransform(bounds.X + (bounds.Width - 24f * s) / 2f, bounds.Y + (bounds.Height - 24f * s) / 2f);
            g.ScaleTransform(s, s);
            using (var pen = new Pen(color, Stroke))
            using (var brush = new SolidBrush(color))
            {
                pen.StartCap = pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                Shape(g, kind, pen, brush, color);
            }
            g.Restore(state);
        }

        static void Poly(Graphics g, Pen p, params float[] xy)
        {
            var pts = new PointF[xy.Length / 2];
            for (int i = 0; i < pts.Length; i++) pts[i] = new PointF(xy[2 * i], xy[2 * i + 1]);
            g.DrawLines(p, pts);
        }

        static void Closed(Graphics g, Pen p, params float[] xy)
        {
            var pts = new PointF[xy.Length / 2];
            for (int i = 0; i < pts.Length; i++) pts[i] = new PointF(xy[2 * i], xy[2 * i + 1]);
            g.DrawPolygon(p, pts);
        }

        static void RoundRect(Graphics g, Pen p, float x, float y, float w, float h, float r)
        {
            using (var path = new GraphicsPath())
            {
                float d = r * 2;
                path.AddArc(x, y, d, d, 180, 90);
                path.AddArc(x + w - d, y, d, d, 270, 90);
                path.AddArc(x + w - d, y + h - d, d, d, 0, 90);
                path.AddArc(x, y + h - d, d, d, 90, 90);
                path.CloseFigure();
                g.DrawPath(p, path);
            }
        }

        static void Corners(Graphics g, Pen p)
        {
            Poly(g, p, 4, 9, 4, 4, 9, 4);
            Poly(g, p, 15, 4, 20, 4, 20, 9);
            Poly(g, p, 20, 15, 20, 20, 15, 20);
            Poly(g, p, 9, 20, 4, 20, 4, 15);
        }

        static void UndoShape(Graphics g, Pen p)
        {
            Poly(g, p, 9, 6, 5, 10, 9, 14);
            g.DrawBezier(p, 5, 10, 16, 8, 21, 13, 17, 19);
        }

        static void Shape(Graphics g, IconKind kind, Pen p, Brush b, Color color)
        {
            switch (kind)
            {
                case IconKind.Capture:
                    Corners(g, p);
                    g.FillEllipse(b, 9.5f, 9.5f, 5f, 5f);
                    break;
                case IconKind.ModeRect:
                    using (var d = new Pen(color, Stroke) { DashPattern = new float[] { 2.2f, 1.8f } })
                        g.DrawRectangle(d, 4f, 6f, 16f, 12f);
                    break;
                case IconKind.ModeFree:
                    using (var d = new Pen(color, Stroke) { DashPattern = new float[] { 2.2f, 1.8f } })
                        g.DrawClosedCurve(d, new PointF[] { new PointF(5, 15), new PointF(8, 7), new PointF(14, 5), new PointF(19, 9), new PointF(17, 17), new PointF(10, 19) }, 0.4f, FillMode.Alternate);
                    break;
                case IconKind.ModeWindow:
                    RoundRect(g, p, 3.5f, 5f, 17f, 14f, 2f);
                    Poly(g, p, 3.5f, 9f, 20.5f, 9f);
                    break;
                case IconKind.ModeScreen:
                    RoundRect(g, p, 3.5f, 4.5f, 17f, 11.5f, 2f);
                    Poly(g, p, 12, 16, 12, 19.5f);
                    Poly(g, p, 8, 19.5f, 16, 19.5f);
                    break;
                case IconKind.ScrollAuto:
                    RoundRect(g, p, 6f, 3.5f, 12f, 17f, 2f);
                    Poly(g, p, 12, 8, 12, 15.5f);
                    Poly(g, p, 9.5f, 13, 12, 15.5f, 14.5f, 13);
                    break;
                case IconKind.ScrollManual:
                    RoundRect(g, p, 7.5f, 3.5f, 9f, 17f, 4.5f);
                    Poly(g, p, 12, 6.5f, 12, 10f);
                    break;
                case IconKind.Gif:
                    RoundRect(g, p, 3.5f, 5.5f, 17f, 13f, 2.5f);
                    g.FillEllipse(b, 9f, 9f, 6f, 6f);
                    break;
                case IconKind.Clock:
                    g.DrawEllipse(p, 3.5f, 3.5f, 17f, 17f);
                    Poly(g, p, 12, 7.5f, 12, 12, 15.5f, 14);
                    break;
                case IconKind.Undo:
                    UndoShape(g, p);
                    break;
                case IconKind.Redo:
                    g.TranslateTransform(24f, 0f); g.ScaleTransform(-1f, 1f);
                    UndoShape(g, p);
                    break;
                case IconKind.Copy:
                    RoundRect(g, p, 8.5f, 8.5f, 12f, 12f, 2f);
                    Poly(g, p, 8.5f, 15.5f, 5.5f, 15.5f, 3.5f, 13.5f, 3.5f, 5.5f, 5.5f, 3.5f, 13.5f, 3.5f, 15.5f, 5.5f, 15.5f, 8.5f);
                    break;
                case IconKind.Save:
                    Closed(g, p, 4.5f, 4.5f, 16.5f, 4.5f, 19.5f, 7.5f, 19.5f, 19.5f, 4.5f, 19.5f);
                    Poly(g, p, 8, 4.5f, 8, 8.5f, 15, 8.5f, 15, 4.5f);
                    g.DrawRectangle(p, 7.5f, 13.5f, 9f, 6f);
                    break;
                case IconKind.Ocr:
                    Corners(g, p);
                    Poly(g, p, 8, 10, 16, 10);
                    Poly(g, p, 8, 13, 16, 13);
                    Poly(g, p, 8, 16, 13, 16);
                    break;
                case IconKind.Pen:
                    Closed(g, p, 4, 20, 5.5f, 15, 16, 4.5f, 19.5f, 8, 9, 18.5f);
                    Poly(g, p, 13.5f, 7, 17, 10.5f);
                    break;
                case IconKind.Highlighter:
                    Closed(g, p, 8, 13, 14, 7, 18, 11, 12, 17);
                    Poly(g, p, 8, 13, 5.5f, 17.5f, 8, 17.5f, 12, 17);
                    Poly(g, p, 4, 20.5f, 12, 20.5f);
                    break;
                case IconKind.Arrow:
                    Poly(g, p, 5, 19, 19, 5);
                    Poly(g, p, 11, 5, 19, 5, 19, 13);
                    break;
                case IconKind.Line:
                    Poly(g, p, 5, 19, 19, 5);
                    break;
                case IconKind.Rect:
                    RoundRect(g, p, 4.5f, 6.5f, 15f, 11f, 1.2f);
                    break;
                case IconKind.Ellipse:
                    g.DrawEllipse(p, 3.5f, 6f, 17f, 12f);
                    break;
                case IconKind.Text:
                    Poly(g, p, 5, 6, 19, 6);
                    Poly(g, p, 12, 6, 12, 19);
                    Poly(g, p, 9, 19, 15, 19);
                    break;
                case IconKind.Step:
                    g.DrawEllipse(p, 3.5f, 3.5f, 17f, 17f);
                    Poly(g, p, 9.5f, 9.5f, 12.5f, 7.5f, 12.5f, 16.5f);
                    Poly(g, p, 10.5f, 16.5f, 14.5f, 16.5f);
                    break;
                case IconKind.Blur:
                    g.DrawClosedCurve(p, new PointF[] { new PointF(12, 3.5f), new PointF(17.5f, 11), new PointF(18, 15), new PointF(15.5f, 19), new PointF(12, 20.5f), new PointF(8.5f, 19), new PointF(6, 15), new PointF(6.5f, 11) }, 0.35f, FillMode.Alternate);
                    break;
                case IconKind.Pixelate:
                    for (int i = 0; i < 3; i++)
                        for (int j = 0; j < 3; j++)
                        {
                            float x = 4f + i * 5.5f, y = 4f + j * 5.5f;
                            if ((i + j) % 2 == 0) g.FillRectangle(b, x, y, 4.5f, 4.5f);
                            else g.DrawRectangle(p, x + 0.4f, y + 0.4f, 3.7f, 3.7f);
                        }
                    break;
                case IconKind.Crop:
                    Poly(g, p, 7, 3, 7, 17, 21, 17);
                    Poly(g, p, 3, 7, 17, 7, 17, 21);
                    break;
                case IconKind.Picker:
                    Poly(g, p, 5, 19, 13, 11);
                    g.DrawEllipse(p, 13f, 4f, 7f, 7f);
                    Poly(g, p, 11, 9, 15, 13);
                    break;
                case IconKind.Eraser:
                    Closed(g, p, 3.5f, 15.5f, 11, 8, 17.5f, 14.5f, 12.5f, 19.5f, 7.5f, 19.5f);
                    Poly(g, p, 13, 20.5f, 20.5f, 20.5f);
                    break;
                case IconKind.ZoomOut:
                case IconKind.ZoomIn:
                    g.DrawEllipse(p, 3.5f, 3.5f, 12.5f, 12.5f);
                    Poly(g, p, 14, 14, 20, 20);
                    Poly(g, p, 7, 9.75f, 12.5f, 9.75f);
                    if (kind == IconKind.ZoomIn) Poly(g, p, 9.75f, 7, 9.75f, 12.5f);
                    break;
                case IconKind.Fit:
                    Corners(g, p);
                    g.DrawRectangle(p, 8.5f, 8.5f, 7f, 7f);
                    break;
                case IconKind.Help:
                    g.DrawEllipse(p, 3.5f, 3.5f, 17f, 17f);
                    g.DrawArc(p, 9f, 6.5f, 6f, 6f, 180, 220);
                    Poly(g, p, 12, 12.5f, 12, 14);
                    g.FillEllipse(b, 11.2f, 16f, 1.7f, 1.7f);
                    break;
                case IconKind.Settings:
                    var gear = new PointF[32];   // 8 dentes
                    for (int i = 0; i < 32; i++)
                    {
                        double a = Math.PI * 2 * i / 32 - Math.PI / 32;
                        float r = (i % 4 < 2) ? 9.6f : 7.4f;
                        gear[i] = new PointF(12f + (float)Math.Cos(a) * r, 12f + (float)Math.Sin(a) * r);
                    }
                    g.DrawPolygon(p, gear);
                    g.DrawEllipse(p, 8.8f, 8.8f, 6.4f, 6.4f);
                    break;
                case IconKind.Shield:
                    Closed(g, p, 12, 3, 19.5f, 6, 19.5f, 12.5f, 12, 21, 4.5f, 12.5f, 4.5f, 6);
                    Poly(g, p, 8.5f, 12, 11, 14.5f, 15.5f, 9.5f);
                    break;
                case IconKind.Close:
                case IconKind.Cross:
                    Poly(g, p, 6.5f, 6.5f, 17.5f, 17.5f);
                    Poly(g, p, 17.5f, 6.5f, 6.5f, 17.5f);
                    break;
                case IconKind.Chevron:
                    Poly(g, p, 7, 9.5f, 12, 14.5f, 17, 9.5f);
                    break;
                case IconKind.Check:
                    Poly(g, p, 5, 12.5f, 10, 17.5f, 19, 7);
                    break;
                case IconKind.Dot:
                    g.FillEllipse(b, 4f, 4f, 16f, 16f);
                    using (var ring = new Pen(Color.FromArgb(150, 128, 128, 128), 1.2f)) g.DrawEllipse(ring, 4f, 4f, 16f, 16f);   // contorno: a cor clara nao some sobre o fundo
                    break;
            }
        }
    }
}
