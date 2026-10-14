// Testes da interface de linha de comando (src\Cli.cs). Precisam de uma sessao de desktop ativa
// (capturam a tela de verdade) e leem apenas a saida JSON de Cli.Execute.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace CapturaTela.Tests
{
    static class CliTests
    {
        // executa um comando como o shell faria e confere o codigo de saida
        static string Run(int expectedExit, params string[] args)
        {
            int code;
            string json = Cli.Execute(args, out code);
            Assert.Equal(expectedExit, code, "codigo de saida de '" + string.Join(" ", args) + "': " + json);
            return json;
        }

        static void Contains(string json, string fragment)
        {
            Assert.True(json.IndexOf(fragment, StringComparison.Ordinal) >= 0, "esperava " + fragment + " em: " + json);
        }

        static Bitmap Open(string file)
        {
            using (var img = Image.FromFile(file))
            {
                var b = new Bitmap(img.Width, img.Height, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(b)) g.DrawImage(img, 0, 0, img.Width, img.Height);
                return b;
            }
        }

        public static void Register()
        {
            TestRunner.Run("cli: sem comando mostra a ajuda; comando invalido = codigo 2", delegate
            {
                Contains(Run(0), "\"command\":\"help\"");
                Contains(Run(2, "naoexiste"), "\"ok\":false");
                Contains(Run(2, "capture", "--region", "1,2,3"), "4 numeros");
                Contains(Run(2, "capture"), "exatamente um alvo");
                Contains(Run(2, "capture", "--fullscreen", "--region", "0,0,10,10"), "exatamente um alvo");
            });

            TestRunner.Run("cli: info descreve os monitores", delegate
            {
                string j = Run(0, "info");
                Contains(j, "\"ok\":true"); Contains(j, "\"virtualScreen\""); Contains(j, "\"primary\":true");
            });

            TestRunner.Run("cli: windows lista janelas", delegate
            {
                Contains(Run(0, "windows"), "\"windows\":[");
            });

            TestRunner.Run("cli: capture --region grava um PNG do tamanho pedido", delegate
            {
                string file = Helpers.TempFile(".png");
                try
                {
                    string j = Run(0, "capture", "--region", "0,0,120,80", "--out", file);
                    Contains(j, "\"width\":120"); Contains(j, "\"height\":80");
                    using (var b = Open(file)) { Assert.Equal(120, b.Width, "largura do arquivo"); Assert.Equal(80, b.Height, "altura do arquivo"); }
                }
                finally { try { File.Delete(file); } catch { } }
            });

            TestRunner.Run("cli: capture --fullscreen --max-width reduz a imagem", delegate
            {
                string file = Helpers.TempFile(".jpg");
                try
                {
                    string j = Run(0, "capture", "--fullscreen", "--max-width", "400", "--out", file);
                    using (var b = Open(file)) Assert.True(b.Width == 400, "largura deveria ser 400, foi " + b.Width);
                    Contains(j, "\"capturedWidth\"");
                }
                finally { try { File.Delete(file); } catch { } }
            });

            TestRunner.Run("cli: capture --window inexistente falha com codigo 1", delegate
            {
                Contains(Run(1, "capture", "--window", "zzz-janela-que-nao-existe-zzz"), "Nenhuma janela");
            });

            TestRunner.Run("cli: pixel de imagem devolve a cor", delegate
            {
                string file = Helpers.TempFile(".png");
                try
                {
                    using (var b = Helpers.Solid(10, 10, Color.FromArgb(18, 52, 86))) b.Save(file, ImageFormat.Png);
                    Contains(Run(0, "pixel", "--image", file, "--at", "3,4"), "\"color\":\"#123456\"");
                    Contains(Run(1, "pixel", "--image", file, "--at", "99,4"), "fora da imagem");
                }
                finally { try { File.Delete(file); } catch { } }
            });

            TestRunner.Run("cli: annotate aplica operacoes na ordem", delegate
            {
                string input = Helpers.TempFile(".png"), output = Helpers.TempFile(".png");
                try
                {
                    using (var b = Helpers.Solid(200, 100, Color.White)) b.Save(input, ImageFormat.Png);
                    string j = Run(0, "annotate", "--in", input, "--out", output,
                        "--color", "#FF0000", "--width", "4", "--rect", "20,20,60,40",
                        "--color", "#00AA00", "--step", "150,50",
                        "--crop", "10,10,180,80");
                    Contains(j, "\"operations\":3"); Contains(j, "\"width\":180"); Contains(j, "\"height\":80");
                    using (var b = Open(output))
                    {
                        // a borda esquerda do retangulo (x=20) cai em x=10 depois do corte
                        var red = b.GetPixel(10, 40);
                        Assert.True(red.R > 200 && red.G < 60, "retangulo vermelho esperado: " + red);
                        var green = b.GetPixel(150, 40);   // fora do digito, no disco do marcador
                        Assert.True(green.G > 120 && green.R < 80, "marcador verde esperado: " + green);
                    }
                    Contains(Run(2, "annotate", "--in", input, "--out", output, "--blur", "1,2"), "4 numeros");
                    Contains(Run(2, "annotate", "--in", input, "--out", output, "--nada", "x"), "desconhecida");
                    Contains(Run(1, "annotate", "--in", input + ".nao", "--out", output), "nao encontrado");
                }
                finally { try { File.Delete(input); File.Delete(output); } catch { } }
            });

            TestRunner.Run("cli: annotate --blur esconde o conteudo (nao recuperavel)", delegate
            {
                string input = Helpers.TempFile(".png"), output = Helpers.TempFile(".png");
                try
                {
                    using (var b = Helpers.Solid(200, 100, Color.White))
                    {
                        using (var g = Graphics.FromImage(b)) g.FillRectangle(Brushes.Black, 50, 40, 8, 8);
                        b.Save(input, ImageFormat.Png);
                    }
                    Run(0, "annotate", "--in", input, "--out", output, "--pixelate", "40,30,40,40");
                    using (var b = Open(output))
                    {
                        var c = b.GetPixel(54, 44);
                        Assert.True(c.R > 60, "o quadrado preto deveria ter sido diluido: " + c);
                    }
                }
                finally { try { File.Delete(input); File.Delete(output); } catch { } }
            });

            TestRunner.Run("cli: gif grava um arquivo valido", delegate
            {
                string file = Helpers.TempFile(".gif");
                try
                {
                    string j = Run(0, "gif", "--region", "0,0,160,100", "--seconds", "1", "--fps", "5", "--out", file);
                    Contains(j, "\"frames\":");
                    using (var img = Image.FromFile(file)) Assert.Equal(160, img.Width, "largura do GIF");
                }
                finally { try { File.Delete(file); } catch { } }
            });

            TestRunner.Run("cli: ocr le texto de uma imagem", delegate
            {
                string file = Helpers.TempFile(".png");
                try
                {
                    using (var b = Helpers.Solid(500, 120, Color.White))
                    {
                        using (var g = Graphics.FromImage(b))
                        using (var f = new Font("Segoe UI", 20))
                        {
                            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                            g.DrawString("Ferramenta de Captura", f, Brushes.Black, 10, 10);
                        }
                        b.Save(file, ImageFormat.Png);
                    }
                    int code;
                    string j = Cli.Execute(new[] { "ocr", "--image", file }, out code);
                    if (code != 0 && j.IndexOf("idioma de OCR", StringComparison.OrdinalIgnoreCase) >= 0) Assert.Skip("sem idioma de OCR instalado");
                    Assert.Equal(0, code, "ocr: " + j);
                    Contains(j, "Captura");
                }
                finally { try { File.Delete(file); } catch { } }
            });
        }
    }
}
