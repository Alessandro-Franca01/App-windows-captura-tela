using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;
using System.Windows.Forms;

namespace CapturaTela
{
    // Captura com rolagem: rola o conteudo sob a area escolhida com a roda do mouse,
    // fotografa a area a cada passo e costura as imagens alinhando as linhas repetidas.
    static class ScrollCapture
    {
        const int MaxSteps = 60;

        public static Bitmap Run(Rectangle region)
        {
            var frames = new List<Bitmap>();
            var saved = Cursor.Position;
            try
            {
                Native.SetCursorPos(region.X + region.Width / 2, region.Y + region.Height / 2);
                Thread.Sleep(250);
                frames.Add(Grab(region));
                var hashes = new List<ulong[]>();
                hashes.Add(RowHashes(frames[0]));
                var shifts = new List<int>();

                for (int step = 0; step < MaxSteps; step++)
                {
                    if ((Native.GetAsyncKeyState(Native.VK_ESCAPE) & 0x8000) != 0) break;
                    Native.mouse_event(Native.MOUSEEVENTF_WHEEL, 0, 0, -120 * 3, UIntPtr.Zero);
                    Thread.Sleep(450);                       // espera a animacao de rolagem terminar
                    var next = Grab(region);
                    var nh = RowHashes(next);
                    int shift = FindShift(hashes[hashes.Count - 1], nh);
                    if (shift <= 0) { next.Dispose(); break; } // fim do conteudo (ou sem alinhamento)
                    frames.Add(next); hashes.Add(nh); shifts.Add(shift);
                }
                return Stitch(frames, shifts);
            }
            finally
            {
                Native.SetCursorPos(saved.X, saved.Y);
                foreach (var f in frames) f.Dispose();
            }
        }

        static Bitmap Grab(Rectangle r)
        {
            var bmp = new Bitmap(r.Width, r.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
                g.CopyFromScreen(r.Location, Point.Empty, r.Size, CopyPixelOperation.SourceCopy);
            return bmp;
        }

        // Hash (FNV-1a) de cada linha de pixels.
        public static ulong[] RowHashes(Bitmap bmp)
        {
            var hashes = new ulong[bmp.Height];
            var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                var row = new byte[bmp.Width * 4];
                for (int y = 0; y < bmp.Height; y++)
                {
                    System.Runtime.InteropServices.Marshal.Copy(new IntPtr(data.Scan0.ToInt64() + (long)y * data.Stride), row, 0, row.Length);
                    ulong h = 14695981039346656037UL;
                    for (int i = 0; i < row.Length; i++) { h ^= row[i]; h *= 1099511628211UL; }
                    hashes[y] = h;
                }
            }
            finally { bmp.UnlockBits(data); }
            return hashes;
        }

        // Quantas linhas o conteudo subiu entre prev e next. 0 = nada mudou ou sem alinhamento confiavel.
        public static int FindShift(ulong[] prev, ulong[] next)
        {
            int h = prev.Length;
            bool same = true;
            for (int i = 0; i < h; i++) if (prev[i] != next[i]) { same = false; break; }
            if (same) return 0;

            // linhas muito repetidas (fundo liso) nao servem como ancora
            var counts = new Dictionary<ulong, int>();
            foreach (var x in prev) { int c; counts.TryGetValue(x, out c); counts[x] = c + 1; }

            int bestShift = 0, bestMatches = 0;
            for (int s = 1; s < h; s++)
            {
                int matches = 0;
                for (int y = 0; y + s < h; y++)
                {
                    if (next[y] == prev[y + s] && counts[prev[y + s]] <= 3) matches++;
                }
                if (matches > bestMatches) { bestMatches = matches; bestShift = s; }
            }
            int overlap = h - bestShift;
            // exige que boa parte da sobreposicao bata (tolera cabecalhos fixos)
            if (bestShift == 0 || bestMatches < Math.Max(6, overlap / 4)) return 0;
            return bestShift;
        }

        static Bitmap Stitch(List<Bitmap> frames, List<int> shifts)
        {
            int w = frames[0].Width, h = frames[0].Height;
            int total = h;
            foreach (var s in shifts) total += s;
            var result = new Bitmap(w, total, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(result))
            {
                g.DrawImageUnscaled(frames[0], 0, 0);
                int y = h;
                for (int i = 1; i < frames.Count; i++)
                {
                    int s = shifts[i - 1];
                    // so as s linhas novas na parte de baixo do quadro
                    g.DrawImage(frames[i], new Rectangle(0, y, w, s), new Rectangle(0, h - s, w, s), GraphicsUnit.Pixel);
                    y += s;
                }
            }
            return result;
        }
    }
}
