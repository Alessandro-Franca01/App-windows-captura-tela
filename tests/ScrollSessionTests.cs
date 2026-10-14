// Testes da rolagem manual (src\ScrollSession.cs): quadros chegam em intervalos curtos enquanto o
// usuario rola; so quadros estaveis e alinhados viram trecho novo.
using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace CapturaTela.Tests
{
    static class ScrollSessionTests
    {
        static Bitmap Page()
        {
            var page = new Bitmap(120, 1000, PixelFormat.Format32bppArgb);
            for (int y = 0; y < 1000; y++)
                for (int x = 0; x < 120; x++)
                    { int v = unchecked((y * 73856093) ^ (x * 19349663) ^ (y * x * 83492791)); page.SetPixel(x, y, Color.FromArgb((v >> 3) & 255, (v >> 11) & 255, (v >> 19) & 255)); }   // sem periodicidade: nenhuma linha se repete
            return page;
        }

        // janela de 300 px: cabecalho fixo (40), 230 px de conteudo que rola, rodape fixo (30)
        static Bitmap View(Bitmap page, int top)
        {
            var v = new Bitmap(120, 300, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(v))
            {
                g.DrawImage(page, new Rectangle(0, 40, 120, 230), new Rectangle(0, top, 120, 230), GraphicsUnit.Pixel);
                g.FillRectangle(Brushes.Navy, 0, 0, 120, 40);
                g.FillRectangle(Brushes.Gray, 0, 270, 120, 30);
            }
            return v;
        }

        public static void Register()
        {
            TestRunner.Run("rolagem manual: so quadros estaveis viram trecho; voltar para cima e seguro", delegate
            {
                using (var page = Page())
                using (var s = new ScrollSession())
                {
                    Action<int, ScrollState> feed = delegate(int top, ScrollState expected)
                    {
                        var state = s.Sample(View(page, top));
                        Assert.Equal(expected, state, "estado depois da posicao " + top);
                    };
                    feed(0, ScrollState.Start);
                    feed(0, ScrollState.Start);          // parado no inicio
                    feed(37, ScrollState.Moving);         // rolagem suave: quadros intermediarios
                    feed(80, ScrollState.Moving);
                    feed(100, ScrollState.Moving);
                    feed(100, ScrollState.Captured);      // estabilizou: trecho novo (+100)
                    feed(100, ScrollState.Idle);
                    feed(140, ScrollState.Moving);
                    feed(200, ScrollState.Moving);
                    feed(200, ScrollState.Captured);      // +100
                    feed(150, ScrollState.Moving);        // o usuario volta para cima...
                    feed(150, ScrollState.Above);         // ...e para: nada novo
                    feed(300, ScrollState.Moving);
                    feed(300, ScrollState.Captured);      // passou do que tinha: +100 em relacao ao ultimo trecho
                    feed(700, ScrollState.Moving);        // salto grande
                    feed(700, ScrollState.Lost);          // sem trecho em comum
                    feed(400, ScrollState.Moving);        // volta para perto
                    feed(400, ScrollState.Captured);      // reencaixa (+100)

                    Assert.Equal(300 + 400, s.Height, "altura aproximada capturada");
                    using (var result = s.Build())
                    {
                        Assert.Equal(40 + (230 + 400) + 30, result.Height, "altura final = cabecalho + conteudo + rodape");
                        Assert.Equal(Color.Navy.ToArgb(), result.GetPixel(5, 10).ToArgb(), "cabecalho uma vez");
                        Assert.Equal(Color.Gray.ToArgb(), result.GetPixel(5, result.Height - 5).ToArgb(), "rodape uma vez");
                        bool ok = true;
                        for (int y = 0; y < 630 && ok; y += 7) ok = result.GetPixel(60, 40 + y) == page.GetPixel(60, y);
                        Assert.True(ok, "conteudo continuo, sem repeticao nem buraco");
                    }
                }
            });

            TestRunner.Run("rolagem manual: sem rolar devolve a propria imagem; sem quadros devolve null", delegate
            {
                using (var page = Page())
                using (var s = new ScrollSession())
                {
                    Assert.True(s.Build() == null, "sem quadros");
                    s.Sample(View(page, 0)); s.Sample(View(page, 0));
                    using (var one = s.Build()) Assert.Equal(300, one.Height, "um quadro so");
                }
            });
        }
    }
}
