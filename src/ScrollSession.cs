using System;
using System.Collections.Generic;
using System.Drawing;

namespace CapturaTela
{
    enum ScrollState
    {
        Start,      // so o primeiro quadro, ainda sem rolagem
        Moving,     // o conteudo ainda esta se mexendo (rolagem suave, arrastando a barra...)
        Captured,   // um trecho novo acabou de ser anexado
        Idle,       // parado, sem trecho novo
        Above,      // parado acima do que ja foi capturado (o usuario voltou)
        Lost        // parado, mas sem trecho em comum com o ultimo capturado: rolou rapido demais
    }

    // Rolagem manual: recebe fotos da area em intervalos curtos enquanto o usuario rola com o mouse.
    // So um quadro "estavel" (igual ao anterior, ou seja, a rolagem suave terminou) vira trecho novo,
    // e so se alinhar com o ultimo trecho capturado. Voltar para cima e rolar de novo e seguro.
    class ScrollSession : IDisposable
    {
        public const int MaxHeight = 30000;

        readonly List<Bitmap> frames = new List<Bitmap>();
        readonly List<ulong[]> hashes = new List<ulong[]>();
        readonly List<int> shifts = new List<int>();
        Bitmap pending;
        ulong[] pendingHash;

        public ScrollState State { get; private set; }
        public bool Full { get { return Height >= MaxHeight; } }

        // altura aproximada ja capturada
        public int Height
        {
            get
            {
                if (frames.Count == 0) return 0;
                int h = frames[0].Height;
                foreach (var s in shifts) h += s;
                return h;
            }
        }

        // Recebe uma foto da area (a sessao passa a ser dona do bitmap).
        public ScrollState Sample(Bitmap bmp)
        {
            var h = ScrollCapture.RowHashes(bmp);
            if (frames.Count == 0)
            {
                frames.Add(bmp); hashes.Add(h); pendingHash = h;
                return State = ScrollState.Start;
            }

            var previous = pendingHash;
            if (pending != null) pending.Dispose();
            pending = bmp; pendingHash = h;

            if (previous == null || !Same(h, previous)) return State = ScrollState.Moving;   // ainda se mexendo

            var last = hashes[hashes.Count - 1];
            if (Same(h, last)) return State = (shifts.Count == 0 ? ScrollState.Start : ScrollState.Idle);

            int s = ScrollCapture.FindShift(last, h);
            if (s > 0 && !Full)
            {
                frames.Add(pending); hashes.Add(h); shifts.Add(s);
                pending = null;
                return State = ScrollState.Captured;
            }
            // nao alinha para baixo: ou o usuario voltou para cima, ou rolou longe demais
            return State = ScrollCapture.FindShift(h, last) > 0 ? ScrollState.Above : ScrollState.Lost;
        }

        // Junta tudo numa imagem so (null se nao houve nenhuma foto).
        public Bitmap Build()
        {
            if (frames.Count == 0) return null;
            if (frames.Count == 1) return (Bitmap)frames[0].Clone();
            return ScrollCapture.Stitch(frames, shifts, hashes);
        }

        static bool Same(ulong[] a, ulong[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        public void Dispose()
        {
            foreach (var f in frames) f.Dispose();
            frames.Clear();
            if (pending != null) { pending.Dispose(); pending = null; }
        }
    }
}
