using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace CapturaTela
{
    // Codificador GIF animado proprio: paleta global por median-cut, quadros apenas com a
    // area que mudou (pixels iguais ao quadro anterior ficam transparentes) e LZW.
    static class GifWriter
    {
        const int Transparent = 255;

        public static void Save(string path, List<Bitmap> frames, int delayMs)
        {
            if (frames.Count == 0) throw new ArgumentException("Sem quadros");
            int w = frames[0].Width, h = frames[0].Height;

            var pixels = new List<int[]>();
            foreach (var f in frames) pixels.Add(ReadPixels(f));

            byte[] palette; byte[] map;
            BuildPalette(pixels, out palette, out map);

            // quantiza cada quadro
            var idx = new List<byte[]>();
            foreach (var px in pixels)
            {
                var q = new byte[px.Length];
                for (int i = 0; i < px.Length; i++) q[i] = map[Bin(px[i])];
                idx.Add(q);
            }

            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write(new byte[] { (byte)'G', (byte)'I', (byte)'F', (byte)'8', (byte)'9', (byte)'a' });
                bw.Write((ushort)w); bw.Write((ushort)h);
                bw.Write((byte)0xF7); bw.Write((byte)0); bw.Write((byte)0);
                bw.Write(palette);
                // laco infinito
                bw.Write(new byte[] { 0x21, 0xFF, 0x0B });
                bw.Write(System.Text.Encoding.ASCII.GetBytes("NETSCAPE2.0"));
                bw.Write(new byte[] { 0x03, 0x01, 0x00, 0x00, 0x00 });

                int delayCs = Math.Max(2, delayMs / 10);
                var rects = new List<Rectangle>(); var datas = new List<byte[]>(); var delays = new List<int>();

                for (int n = 0; n < idx.Count; n++)
                {
                    Rectangle r; byte[] data;
                    if (n == 0) { r = new Rectangle(0, 0, w, h); data = idx[0]; }
                    else
                    {
                        r = DiffRect(idx[n - 1], idx[n], w, h);
                        if (r.IsEmpty)
                        {
                            // quadro igual ao anterior: apenas alonga o anterior
                            delays[delays.Count - 1] += delayCs;
                            continue;
                        }
                        data = new byte[r.Width * r.Height];
                        for (int yy = 0; yy < r.Height; yy++)
                            for (int xx = 0; xx < r.Width; xx++)
                            {
                                int p = (r.Y + yy) * w + r.X + xx;
                                data[yy * r.Width + xx] = idx[n][p] == idx[n - 1][p] ? (byte)Transparent : idx[n][p];
                            }
                    }
                    rects.Add(r); datas.Add(data); delays.Add(delayCs);
                }

                for (int i = 0; i < rects.Count; i++)
                {
                    var r = rects[i];
                    bw.Write(new byte[] { 0x21, 0xF9, 0x04, (byte)((1 << 2) | 1) });   // disposal 1 + transparencia
                    bw.Write((ushort)Math.Min(delays[i], 65535));
                    bw.Write((byte)Transparent); bw.Write((byte)0);
                    bw.Write((byte)0x2C);
                    bw.Write((ushort)r.X); bw.Write((ushort)r.Y); bw.Write((ushort)r.Width); bw.Write((ushort)r.Height);
                    bw.Write((byte)0);
                    bw.Write((byte)8);
                    var lzw = Lzw(datas[i]);
                    for (int pos = 0; pos < lzw.Length; pos += 255)
                    {
                        int len = Math.Min(255, lzw.Length - pos);
                        bw.Write((byte)len); bw.Write(lzw, pos, len);
                    }
                    bw.Write((byte)0);
                }
                bw.Write((byte)0x3B);
            }
        }

        static Rectangle DiffRect(byte[] a, byte[] b, int w, int h)
        {
            int minX = w, minY = h, maxX = -1, maxY = -1;
            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    if (a[row + x] != b[row + x])
                    {
                        if (x < minX) minX = x; if (x > maxX) maxX = x;
                        if (y < minY) minY = y; if (y > maxY) maxY = y;
                    }
                }
            }
            if (maxX < 0) return Rectangle.Empty;
            return Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
        }

        static int[] ReadPixels(Bitmap bmp)
        {
            var px = new int[bmp.Width * bmp.Height];
            var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                for (int y = 0; y < bmp.Height; y++)
                    System.Runtime.InteropServices.Marshal.Copy(new IntPtr(data.Scan0.ToInt64() + (long)y * data.Stride), px, y * bmp.Width, bmp.Width);
            }
            finally { bmp.UnlockBits(data); }
            return px;
        }

        // 5 bits por canal
        static int Bin(int argb) { return (((argb >> 19) & 31) << 10) | (((argb >> 11) & 31) << 5) | ((argb >> 3) & 31); }

        // ---- paleta (median cut sobre histograma de 15 bits) ----

        class Box
        {
            public List<int> Bins = new List<int>();
            public long Count;
        }

        static void BuildPalette(List<int[]> frames, out byte[] palette, out byte[] map)
        {
            var hist = new long[32768];
            foreach (var px in frames)
            {
                int step = Math.Max(1, px.Length / 150000);
                for (int i = 0; i < px.Length; i += step) hist[Bin(px[i])] += step;
            }
            var first = new Box();
            for (int b = 0; b < hist.Length; b++) if (hist[b] > 0) { first.Bins.Add(b); first.Count += hist[b]; }
            var boxes = new List<Box> { first };

            while (boxes.Count < 255)
            {
                Box target = null; int bestRange = 0, bestChannel = 0;
                foreach (var bx in boxes)
                {
                    if (bx.Bins.Count < 2) continue;
                    for (int ch = 0; ch < 3; ch++)
                    {
                        int mn = 31, mx = 0;
                        foreach (int b in bx.Bins) { int v = Channel(b, ch); if (v < mn) mn = v; if (v > mx) mx = v; }
                        int range = mx - mn;
                        if (range > bestRange) { bestRange = range; target = bx; bestChannel = ch; }
                    }
                }
                if (target == null) break;
                int c = bestChannel;
                target.Bins.Sort(delegate(int x, int y) { return Channel(x, c).CompareTo(Channel(y, c)); });
                long half = target.Count / 2, acc = 0; int cut = 0;
                for (; cut < target.Bins.Count - 1; cut++)
                {
                    acc += hist[target.Bins[cut]];
                    if (acc >= half) { cut++; break; }
                }
                if (cut <= 0 || cut >= target.Bins.Count) cut = target.Bins.Count / 2;
                var other = new Box();
                other.Bins = target.Bins.GetRange(cut, target.Bins.Count - cut);
                target.Bins.RemoveRange(cut, target.Bins.Count - cut);
                target.Count = 0; foreach (int b in target.Bins) target.Count += hist[b];
                other.Count = 0; foreach (int b in other.Bins) other.Count += hist[b];
                boxes.Add(other);
            }

            palette = new byte[256 * 3];
            var pr = new int[256]; var pg = new int[256]; var pb = new int[256];
            int n = boxes.Count;
            for (int i = 0; i < n; i++)
            {
                double r = 0, g = 0, b = 0; long tot = 0;
                foreach (int bin in boxes[i].Bins)
                {
                    long cnt = hist[bin];
                    r += Channel(bin, 0) * cnt; g += Channel(bin, 1) * cnt; b += Channel(bin, 2) * cnt; tot += cnt;
                }
                if (tot == 0) tot = 1;
                pr[i] = Expand((int)Math.Round(r / tot)); pg[i] = Expand((int)Math.Round(g / tot)); pb[i] = Expand((int)Math.Round(b / tot));
                palette[i * 3] = (byte)pr[i]; palette[i * 3 + 1] = (byte)pg[i]; palette[i * 3 + 2] = (byte)pb[i];
            }

            // cor mais proxima da paleta para cada um dos 32768 bins
            map = new byte[32768];
            for (int bin = 0; bin < 32768; bin++)
            {
                int r = Expand(Channel(bin, 0)), g = Expand(Channel(bin, 1)), b = Expand(Channel(bin, 2));
                int best = 0, bestD = int.MaxValue;
                for (int i = 0; i < n; i++)
                {
                    int dr = r - pr[i], dg = g - pg[i], db = b - pb[i];
                    int d = dr * dr * 3 + dg * dg * 4 + db * db * 2;
                    if (d < bestD) { bestD = d; best = i; }
                }
                map[bin] = (byte)best;
            }
        }

        // canal 0=R 1=G 2=B de um bin de 15 bits
        static int Channel(int bin, int ch) { return ch == 0 ? (bin >> 10) & 31 : ch == 1 ? (bin >> 5) & 31 : bin & 31; }
        static int Expand(int v5) { return Math.Min(255, (v5 << 3) | (v5 >> 2)); }

        // ---- LZW (tamanho minimo de codigo 8) ----

        static byte[] Lzw(byte[] data)
        {
            const int clear = 256, eoi = 257;
            var output = new List<byte>(data.Length / 2 + 16);
            uint buf = 0; int bits = 0;
            int codeSize = 9, nextCode = eoi + 1;
            var dict = new Dictionary<int, int>();

            Action<int> emit = delegate(int code)
            {
                buf |= (uint)code << bits; bits += codeSize;
                while (bits >= 8) { output.Add((byte)(buf & 0xFF)); buf >>= 8; bits -= 8; }
            };

            emit(clear);
            int prefix = data[0];
            for (int i = 1; i < data.Length; i++)
            {
                int c = data[i];
                int key = (prefix << 8) | c;
                int code;
                if (dict.TryGetValue(key, out code)) { prefix = code; continue; }

                emit(prefix);
                if (nextCode > (1 << codeSize) - 1 && codeSize < 12) codeSize++;
                if (nextCode < 4096) dict[key] = nextCode++;
                else
                {
                    emit(clear);
                    dict.Clear(); nextCode = eoi + 1; codeSize = 9;
                }
                prefix = c;
            }
            emit(prefix);
            if (nextCode > (1 << codeSize) - 1 && codeSize < 12) codeSize++;
            emit(eoi);
            if (bits > 0) output.Add((byte)(buf & 0xFF));
            return output.ToArray();
        }
    }
}

