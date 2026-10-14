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

        public static Bitmap Run(Rectangle region) { return Run(region, MaxSteps); }

        public static Bitmap Run(Rectangle region, int maxSteps)
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

                for (int step = 0; step < maxSteps; step++)
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
                return Stitch(frames, shifts, hashes);
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

        // Hash (FNV-1a) de cada linha de pixels. As bordas ficam de fora: a barra de rolagem (que
        // acompanha o conteudo), sombras e bordas da janela mudam a cada passo e, se entrassem no
        // hash, nenhuma linha seria igual entre dois quadros.
        public static ulong[] RowHashes(Bitmap bmp)
        {
            int left = 0, right = bmp.Width;
            if (bmp.Width >= 120) { left = 2; right = bmp.Width - Math.Max(24, bmp.Width / 50); }
            var hashes = new ulong[bmp.Height];
            var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                var row = new byte[bmp.Width * 4];
                for (int y = 0; y < bmp.Height; y++)
                {
                    System.Runtime.InteropServices.Marshal.Copy(new IntPtr(data.Scan0.ToInt64() + (long)y * data.Stride), row, 0, row.Length);
                    ulong h = 14695981039346656037UL;
                    for (int i = left * 4; i < right * 4; i++) { h ^= row[i]; h *= 1099511628211UL; }
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
            if (bestShift == 0) return 0;
            // So linhas unicas servem de ancora (texto repetitivo tem poucas). Exige que boa parte delas
            // bata no deslocamento escolhido; o resto (cabecalhos, barras, rodape) fica fixo e nao bate.
            int eligible = 0;
            for (int y = 0; y + bestShift < h; y++) if (counts[prev[y + bestShift]] <= 3) eligible++;
            if (bestMatches < Math.Max(6, (int)(eligible * 0.4))) return 0;
            return bestShift;
        }

        // Linhas fixas no topo e no rodape (barra de titulo, ferramentas, barra de status): iguais, na
        // mesma posicao, em todos os pares de quadros. Elas entram uma unica vez no resultado.
        static void FixedBands(List<ulong[]> hashes, List<int> shifts, int h, out int top, out int bottom)
        {
            top = int.MaxValue; bottom = int.MaxValue;
            for (int i = 1; i < hashes.Count; i++)
            {
                var a = hashes[i - 1]; var b = hashes[i];
                int t = 0; while (t < h && a[t] == b[t]) t++;
                int u = 0; while (u < h && a[h - 1 - u] == b[h - 1 - u]) u++;
                top = Math.Min(top, t); bottom = Math.Min(bottom, u);
            }
            if (top == int.MaxValue) { top = 0; bottom = 0; return; }
            top = Math.Min(top, h / 2); bottom = Math.Min(bottom, h / 2);
            // se algum deslocamento nao cabe na faixa que rola, a deteccao foi exagerada: ignora
            foreach (var s in shifts) if (s > h - top - bottom) { top = 0; bottom = 0; return; }
        }

        public static Bitmap Stitch(List<Bitmap> frames, List<int> shifts, List<ulong[]> hashes)
        {
            int w = frames[0].Width, h = frames[0].Height;
            int top, bottom;
            FixedBands(hashes, shifts, h, out top, out bottom);
            int band = h - top - bottom;

            int total = top + band + bottom;
            foreach (var s in shifts) total += s;
            var result = new Bitmap(w, total, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(result))
            {
                // cabecalho e primeira faixa vem do primeiro quadro
                g.DrawImage(frames[0], new Rectangle(0, 0, w, top + band), new Rectangle(0, 0, w, top + band), GraphicsUnit.Pixel);
                int y = top + band;
                for (int i = 1; i < frames.Count; i++)
                {
                    int s = shifts[i - 1];
                    // so as s linhas novas na parte de baixo da faixa que rola
                    g.DrawImage(frames[i], new Rectangle(0, y, w, s), new Rectangle(0, h - bottom - s, w, s), GraphicsUnit.Pixel);
                    y += s;
                }
                // rodape fixo do ultimo quadro
                if (bottom > 0)
                    g.DrawImage(frames[frames.Count - 1], new Rectangle(0, y, w, bottom), new Rectangle(0, h - bottom, w, bottom), GraphicsUnit.Pixel);
            }
            return result;
        }
    }
}
