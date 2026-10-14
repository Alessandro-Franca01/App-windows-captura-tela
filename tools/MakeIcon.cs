// Gera assets\icon.ico (varios tamanhos, PNG dentro do ICO).
// Uso: csc /out:MakeIcon.exe tools\MakeIcon.cs /r:System.Drawing.dll && MakeIcon.exe assets\icon.ico
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

static class MakeIcon
{
    static Bitmap Render(int s)
    {
        var bmp = new Bitmap(s, s, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            float m = s * 0.04f, r = s * 0.22f, w = s - 2 * m;

            using (var path = new GraphicsPath())
            {
                path.AddArc(m, m, r * 2, r * 2, 180, 90);
                path.AddArc(m + w - r * 2, m, r * 2, r * 2, 270, 90);
                path.AddArc(m + w - r * 2, m + w - r * 2, r * 2, r * 2, 0, 90);
                path.AddArc(m, m + w - r * 2, r * 2, r * 2, 90, 90);
                path.CloseFigure();
                using (var br = new LinearGradientBrush(new RectangleF(0, 0, s, s), Color.FromArgb(41, 150, 243), Color.FromArgb(13, 71, 161), 60f))
                    g.FillPath(br, path);
            }

            // cantos do visor
            float pad = s * 0.24f, len = s * 0.17f, t = Math.Max(1.5f, s * 0.055f);
            using (var pen = new Pen(Color.White, t))
            {
                pen.StartCap = pen.EndCap = LineCap.Round;
                float l = pad, tp = pad, rt = s - pad, bt = s - pad;
                g.DrawLines(pen, new PointF[] { new PointF(l, tp + len), new PointF(l, tp), new PointF(l + len, tp) });
                g.DrawLines(pen, new PointF[] { new PointF(rt - len, tp), new PointF(rt, tp), new PointF(rt, tp + len) });
                g.DrawLines(pen, new PointF[] { new PointF(rt, bt - len), new PointF(rt, bt), new PointF(rt - len, bt) });
                g.DrawLines(pen, new PointF[] { new PointF(l + len, bt), new PointF(l, bt), new PointF(l, bt - len) });
            }
            // ponto central
            float d = s * 0.2f;
            using (var br = new SolidBrush(Color.White))
                g.FillEllipse(br, (s - d) / 2, (s - d) / 2, d, d);
        }
        return bmp;
    }

    // BITMAPINFOHEADER + pixels BGRA de baixo para cima + mascara AND vazia
    static byte[] Dib(Bitmap bmp)
    {
        int w = bmp.Width, h = bmp.Height;
        using (var ms = new MemoryStream())
        using (var bw = new BinaryWriter(ms))
        {
            bw.Write(40); bw.Write(w); bw.Write(h * 2); bw.Write((ushort)1); bw.Write((ushort)32);
            bw.Write(0); bw.Write(0); bw.Write(0); bw.Write(0); bw.Write(0); bw.Write(0);
            for (int y = h - 1; y >= 0; y--)
                for (int x = 0; x < w; x++)
                {
                    var c = bmp.GetPixel(x, y);
                    bw.Write(c.B); bw.Write(c.G); bw.Write(c.R); bw.Write(c.A);
                }
            int maskRow = ((w + 31) / 32) * 4;
            bw.Write(new byte[maskRow * h]);
            return ms.ToArray();
        }
    }

    static void Main(string[] args)
    {
        string output = args.Length > 0 ? args[0] : "icon.ico";
        int[] sizes = { 16, 24, 32, 48, 64, 128, 256 };
        var pngs = new List<byte[]>();
        // System.Drawing.Icon so le entradas DIB; PNG apenas no tamanho 256 (padrao do Vista+)
        foreach (int s in sizes)
            using (var bmp = Render(s))
            using (var ms = new MemoryStream())
            {
                if (s >= 256) { bmp.Save(ms, ImageFormat.Png); pngs.Add(ms.ToArray()); }
                else pngs.Add(Dib(bmp));
            }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
        using (var bw = new BinaryWriter(File.Create(output)))
        {
            bw.Write((ushort)0); bw.Write((ushort)1); bw.Write((ushort)sizes.Length);
            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                bw.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i])); bw.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                bw.Write((byte)0); bw.Write((byte)0);
                bw.Write((ushort)1); bw.Write((ushort)32);
                bw.Write(pngs[i].Length); bw.Write(offset);
                offset += pngs[i].Length;
            }
            foreach (var p in pngs) bw.Write(p);
        }
        Console.WriteLine("Icone gerado: " + output);
    }
}
