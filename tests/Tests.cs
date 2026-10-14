// Testes automatizados (sem framework externo). Rode com test.bat.
// Cobrem a logica que nao depende de interacao humana: anotacoes, GIF, rolagem, configuracoes e OCR.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace CapturaTela.Tests
{
    class SkipException : Exception { public SkipException(string why) : base(why) { } }

    static class Assert
    {
        public static void True(bool cond, string msg) { if (!cond) throw new Exception("Falhou: " + msg); }
        public static void Equal(object expected, object actual, string msg)
        {
            if (!object.Equals(expected, actual)) throw new Exception("Falhou: " + msg + " (esperado " + expected + ", obtido " + actual + ")");
        }
        public static void Skip(string why) { throw new SkipException(why); }
    }

    static class TestRunner
    {
        static int passed, failed, skipped;

        public static void Run(string name, Action test)
        {
            try { test(); passed++; Console.WriteLine("ok      " + name); }
            catch (SkipException s) { skipped++; Console.WriteLine("pulado  " + name + " - " + s.Message); }
            catch (Exception e) { failed++; Console.WriteLine("FALHOU  " + name + " - " + e.Message); }
        }

        [STAThread]
        static int Main(string[] args)
        {
            Native.SetProcessDPIAware();
            AnnotationTests.Register();
            GifTests.Register();
            ScrollTests.Register();
            SettingsTests.Register();
            OcrTests.Register();
            CliTests.Register();
            PermissionTests.Register();
            McpTests.Register();
            ScrollSessionTests.Register();
            HelpTests.Register();
            VisualTests.Register();

            Console.WriteLine();
            Console.WriteLine("Resultado: " + passed + " ok, " + failed + " falhas, " + skipped + " pulados");
            return failed == 0 ? 0 : 1;
        }
    }

    static class Helpers
    {
        public static Bitmap Solid(int w, int h, Color c)
        {
            var b = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(b)) g.Clear(c);
            return b;
        }

        public static string TempFile(string ext)
        {
            return Path.Combine(Path.GetTempPath(), "CapturaTela-test-" + Guid.NewGuid().ToString("N") + ext);
        }
    }

    static class AnnotationTests
    {
        public static void Register()
        {
            TestRunner.Run("anotacoes: retangulo, passo e pixelar renderizam", delegate
            {
                var bmp = Helpers.Solid(200, 100, Color.White);
                using (var g = Graphics.FromImage(bmp)) g.FillRectangle(Brushes.Blue, 0, 0, 100, 100);
                var c = new Canvas(); c.SetImage(bmp);
                c.Annotations.Add(new ShapeAnn { Kind = ShapeKind.Rectangle, A = new Point(120, 20), B = new Point(180, 80), Color = Color.Red, Width = 4 });
                c.Annotations.Add(new StepAnn { Center = new Point(50, 50), Number = 1, Color = Color.Green, Width = 4 });
                c.Annotations.Add(new BlurAnn { Rect = new Rectangle(90, 0, 20, 100), Pixelate = true, Width = 4 });
                using (var f = c.Flatten())
                {
                    var red = f.GetPixel(120, 50);
                    Assert.True(red.R > 200 && red.G < 50, "borda do retangulo deveria ser vermelha: " + red);
                    var mix = f.GetPixel(100, 50);
                    Assert.True(mix.R > 0 && mix.B > 0, "pixelado deveria misturar azul e branco: " + mix);
                }
            });

            TestRunner.Run("anotacoes: hit-test", delegate
            {
                var rect = new ShapeAnn { Kind = ShapeKind.Rectangle, A = new Point(120, 20), B = new Point(180, 80), Width = 4 };
                Assert.True(rect.Hit(new Point(120, 50)), "borda do retangulo");
                Assert.True(!rect.Hit(new Point(150, 50)), "miolo do retangulo nao deve acertar");
                var arrow = new ShapeAnn { Kind = ShapeKind.Arrow, A = new Point(10, 90), B = new Point(90, 90), Width = 3 };
                Assert.True(arrow.Hit(new Point(50, 90)), "seta");
            });

            TestRunner.Run("canvas: zoom altera o tamanho", delegate
            {
                var c = new Canvas(); c.SetImage(Helpers.Solid(100, 80, Color.White));
                Assert.True(!c.CanUndo, "historico comeca vazio");
                var a = new ShapeAnn { Kind = ShapeKind.Line, A = new Point(1, 1), B = new Point(50, 50), Width = 2 };
                c.Annotations.Add(a);
                c.SetZoom(2f);
                Assert.Equal(200, c.Width, "largura com zoom 2x");
            });
        }
    }

    static class GifTests
    {
        public static void Register()
        {
            TestRunner.Run("gif: encoder gera GIF animado legivel pelo GDI+", delegate
            {
                var rnd = new Random(1);
                var frames = new List<Bitmap>();
                for (int n = 0; n < 6; n++)
                {
                    var b = new Bitmap(320, 200, PixelFormat.Format24bppRgb);
                    using (var g = Graphics.FromImage(b))
                    {
                        g.Clear(Color.FromArgb(20, 40, 200));
                        for (int i = 0; i < 400; i++)
                            using (var br = new SolidBrush(Color.FromArgb(rnd.Next(256), rnd.Next(256), rnd.Next(256))))
                                g.FillRectangle(br, rnd.Next(300), rnd.Next(180), rnd.Next(2, 30), rnd.Next(2, 30));
                        g.FillEllipse(Brushes.Yellow, 10 + n * 40, 80, 50, 50);
                    }
                    frames.Add(b);
                }
                string path = Helpers.TempFile(".gif");
                try
                {
                    GifWriter.Save(path, frames, 100);
                    using (var img = Image.FromFile(path))
                    {
                        var dim = new FrameDimension(img.FrameDimensionsList[0]);
                        Assert.Equal(6, img.GetFrameCount(dim), "quadros");
                        img.SelectActiveFrame(dim, 5);
                        using (var last = new Bitmap(img))
                        {
                            var c = last.GetPixel(10 + 5 * 40 + 25, 105);
                            Assert.True(c.R > 200 && c.G > 200 && c.B < 80, "ultimo quadro deveria ter a bola amarela: " + c);
                        }
                    }
                }
                finally { foreach (var f in frames) f.Dispose(); try { File.Delete(path); } catch { } }
            });
        }
    }

    static class ScrollTests
    {
        static Bitmap Page()
        {
            var page = new Bitmap(100, 1000, PixelFormat.Format32bppArgb);
            for (int y = 0; y < 1000; y++)
                for (int x = 0; x < 100; x++)
                    page.SetPixel(x, y, Color.FromArgb((y * 37 + x * 11) % 256, (y * 91) % 256, (y * 13 + x) % 256));
            return page;
        }

        static Bitmap View(Bitmap page, int top)
        {
            var v = new Bitmap(100, 300, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(v))
                g.DrawImage(page, new Rectangle(0, 0, 100, 300), new Rectangle(0, top, 100, 300), GraphicsUnit.Pixel);
            return v;
        }

        public static void Register()
        {
            TestRunner.Run("rolagem: encontra o deslocamento entre quadros", delegate
            {
                using (var page = Page())
                using (var a = View(page, 0)) using (var b = View(page, 120))
                {
                    Assert.Equal(120, ScrollCapture.FindShift(ScrollCapture.RowHashes(a), ScrollCapture.RowHashes(b)), "deslocamento");
                    Assert.Equal(0, ScrollCapture.FindShift(ScrollCapture.RowHashes(a), ScrollCapture.RowHashes(a)), "quadros iguais = fim");
                }
            });

            TestRunner.Run("rolagem: tolera cabecalho fixo", delegate
            {
                using (var page = Page())
                using (var a = View(page, 0)) using (var b = View(page, 120))
                {
                    foreach (var v in new[] { a, b }) using (var g = Graphics.FromImage(v)) g.FillRectangle(Brushes.Navy, 0, 0, 100, 40);
                    Assert.Equal(120, ScrollCapture.FindShift(ScrollCapture.RowHashes(a), ScrollCapture.RowHashes(b)), "deslocamento com cabecalho");
                }
            });
            TestRunner.Run("rolagem: texto repetitivo (poucas linhas unicas) e cabecalho/rodape fixos", delegate
            {
                // cada "linha de texto" tem 20 linhas de pixels; 17 iguais em todas as linhas e so 3 distintas (o numero)
                Func<int, Bitmap> view = delegate(int firstLine)
                {
                    var v = new Bitmap(100, 800, PixelFormat.Format32bppArgb);
                    for (int y = 0; y < 800; y++)
                    {
                        int line = firstLine + y / 20, r = y % 20;
                        Color c = r < 17 ? Color.FromArgb(r * 10 % 256, 0, 0) : Color.FromArgb(r * 10 % 256, (line * 37) % 256, (line * 91 + r) % 256);
                        for (int x = 0; x < 100; x++) v.SetPixel(x, y, c);
                    }
                    using (var g = Graphics.FromImage(v)) { g.FillRectangle(Brushes.Navy, 0, 0, 100, 60); g.FillRectangle(Brushes.Gray, 0, 760, 100, 40); }   // barras fixas
                    return v;
                };
                using (var a = view(0)) using (var b = view(3))   // rolou 3 linhas = 60 px
                    Assert.Equal(60, ScrollCapture.FindShift(ScrollCapture.RowHashes(a), ScrollCapture.RowHashes(b)), "deslocamento em texto repetitivo");
            });

            TestRunner.Run("rolagem: barra de rolagem e bordas moveis nao impedem o alinhamento", delegate
            {
                // reproduz o Bloco de Notas: so as colunas da barra de rolagem e das bordas mudam entre os quadros
                Func<int, Bitmap> view = delegate(int firstLine)
                {
                    var v = new Bitmap(400, 600, PixelFormat.Format32bppArgb);
                    for (int y = 0; y < 600; y++)
                    {
                        int line = firstLine + y / 20, r = y % 20;
                        Color c = r < 17 ? Color.FromArgb(r * 10 % 256, 0, 0) : Color.FromArgb(r * 10 % 256, (line * 37) % 256, (line * 91 + r) % 256);
                        for (int x = 0; x < 400; x++) v.SetPixel(x, y, c);
                    }
                    using (var g = Graphics.FromImage(v))
                    {
                        g.FillRectangle(Brushes.Black, 396 - 0, firstLine * 7, 3, 180);   // polegar da barra: muda de lugar
                        g.FillRectangle(firstLine % 2 == 0 ? Brushes.Black : Brushes.White, 0, 0, 1, 600);   // borda que oscila
                    }
                    return v;
                };
                using (var a = view(0)) using (var b = view(4))
                    Assert.Equal(80, ScrollCapture.FindShift(ScrollCapture.RowHashes(a), ScrollCapture.RowHashes(b)), "deslocamento com barra de rolagem movel");
            });

            TestRunner.Run("rolagem: costura com cabecalho e rodape fixos aparecendo uma unica vez", delegate
            {
                // pagina de 1000 linhas unicas; a janela mostra 230 linhas rolaveis entre um cabecalho (40) e um rodape (30)
                var page = new Bitmap(120, 1000, PixelFormat.Format32bppArgb);
                for (int y = 0; y < 1000; y++)
                    for (int x = 0; x < 120; x++)
                        page.SetPixel(x, y, Color.FromArgb((y * 37 + x * 11) % 256, (y * 91) % 256, (y * 13 + x) % 256));
                Func<int, Bitmap> view = delegate(int top)
                {
                    var v = new Bitmap(120, 300, PixelFormat.Format32bppArgb);
                    using (var g = Graphics.FromImage(v))
                    {
                        g.DrawImage(page, new Rectangle(0, 40, 120, 230), new Rectangle(0, top, 120, 230), GraphicsUnit.Pixel);
                        g.FillRectangle(Brushes.Navy, 0, 0, 120, 40);
                        g.FillRectangle(Brushes.Gray, 0, 270, 120, 30);
                    }
                    return v;
                };
                var frames = new List<Bitmap>(); var hashes = new List<ulong[]>(); var shifts = new List<int>();
                foreach (int top in new[] { 0, 100, 200, 300, 400 })
                {
                    var f = view(top); var hh = ScrollCapture.RowHashes(f);
                    if (frames.Count > 0) shifts.Add(ScrollCapture.FindShift(hashes[hashes.Count - 1], hh));
                    frames.Add(f); hashes.Add(hh);
                }
                foreach (int s in shifts) Assert.Equal(100, s, "deslocamento de cada passo");
                using (var result = ScrollCapture.Stitch(frames, shifts, hashes))
                {
                    Assert.Equal(40 + (230 + 400) + 30, result.Height, "altura = cabecalho + conteudo + rodape");
                    Assert.Equal(Color.Navy.ToArgb(), result.GetPixel(5, 10).ToArgb(), "cabecalho no topo");
                    Assert.Equal(Color.Gray.ToArgb(), result.GetPixel(5, result.Height - 5).ToArgb(), "rodape no fim");
                    bool ok = true;
                    for (int y = 0; y < 630 && ok; y += 7) ok = result.GetPixel(60, 40 + y) == page.GetPixel(60, y);
                    Assert.True(ok, "o conteudo do meio e continuo, sem rodape repetido nem linhas perdidas");
                }
                foreach (var f in frames) f.Dispose();
                page.Dispose();
            });

        }
    }

    static class SettingsTests
    {
        public static void Register()
        {
            TestRunner.Run("configuracoes: texto do atalho", delegate
            {
                Assert.Equal("Print Screen", AppSettings.HotkeyText(0, Keys.PrintScreen), "print screen");
                Assert.Equal("Ctrl+Shift+S", AppSettings.HotkeyText(Native.MOD_CONTROL | Native.MOD_SHIFT, Keys.S), "ctrl+shift+s");
            });
        }
    }

    static class OcrTests
    {
        public static void Register()
        {
            TestRunner.Run("ocr: reconhece texto desenhado", delegate
            {
                var b = Helpers.Solid(500, 120, Color.White);
                using (var g = Graphics.FromImage(b))
                using (var f = new Font("Segoe UI", 20))
                {
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                    g.DrawString("Ferramenta de Captura", f, Brushes.Black, 10, 10);
                }
                string text = null, error = null;
                var done = new ManualResetEvent(false);
                Ocr.Recognize(b, delegate(string t, string e) { text = t; error = e; done.Set(); });
                Assert.True(done.WaitOne(70000), "OCR nao terminou em 70 s");
                if (error != null && error.IndexOf("idioma de OCR", StringComparison.OrdinalIgnoreCase) >= 0) Assert.Skip("sem idioma de OCR instalado");
                Assert.True(error == null, "erro no OCR: " + error);
                Assert.True(text.IndexOf("Captura", StringComparison.OrdinalIgnoreCase) >= 0, "texto reconhecido: " + text);
            });
        }
    }
}
