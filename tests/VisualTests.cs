// Testes da camada visual: icones, tokens de cor (contraste), botoes, menus de escolha e risco das permissoes.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace CapturaTela.Tests
{
    static class VisualTests
    {
        static double Lum(Color c)
        {
            Func<int, double> f = delegate(int v) { double s = v / 255.0; return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4); };
            return 0.2126 * f(c.R) + 0.7152 * f(c.G) + 0.0722 * f(c.B);
        }

        static double Contrast(Color a, Color b)
        {
            double la = Lum(a), lb = Lum(b);
            return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
        }

        static int InkPixels(Bitmap b)
        {
            int n = 0;
            for (int y = 0; y < b.Height; y++) for (int x = 0; x < b.Width; x++) if (b.GetPixel(x, y).A > 40) n++;
            return n;
        }

        public static void Register()
        {
            TestRunner.Run("visual: todos os icones desenham algo e nao estouram", delegate
            {
                foreach (IconKind k in Enum.GetValues(typeof(IconKind)))
                {
                    using (var bmp = new Bitmap(48, 48, PixelFormat.Format32bppArgb))
                    {
                        using (var g = Graphics.FromImage(bmp)) Icons.Draw(g, k, new Rectangle(0, 0, 48, 48), Color.Black);
                        Assert.True(InkPixels(bmp) > 20, "icone " + k + " ficou vazio");
                    }
                }
            });

            TestRunner.Run("visual: tokens de cor tem contraste minimo de 4,5:1 sobre a superficie do tema", delegate
            {
                int old = Theme.Preference;
                try
                {
                    foreach (int pref in new[] { 1, 2 })
                    {
                        Theme.Preference = pref;
                        string theme = pref == 1 ? "claro" : "escuro";
                        Color surface = pref == 1 ? Color.FromArgb(0xF0, 0xF0, 0xF0) : Theme.DarkBack;
                        Color raised = pref == 1 ? Color.White : Theme.DarkPanel;
                        foreach (var t in new[] { new { N = "Muted", C = Theme.Muted }, new { N = "Success", C = Theme.Success }, new { N = "Danger", C = Theme.Danger },
                                                  new { N = "Warning", C = Theme.Warning }, new { N = "AccentText", C = Theme.AccentText }, new { N = "Ink", C = pref == 1 ? Color.Black : Theme.DarkText } })
                        {
                            Assert.True(Contrast(t.C, surface) >= 4.5, t.N + " sobre a superficie (" + theme + "): " + Math.Round(Contrast(t.C, surface), 2));
                            Assert.True(Contrast(t.C, raised) >= 4.5, t.N + " sobre a superficie elevada (" + theme + "): " + Math.Round(Contrast(t.C, raised), 2));
                        }
                    }
                    Assert.True(Contrast(Color.White, Theme.AccentStrong) >= 4.5, "texto branco sobre o azul dos botoes: " + Math.Round(Contrast(Color.White, Theme.AccentStrong), 2));
                }
                finally { Theme.Preference = old; }
            });

            TestRunner.Run("visual: nomes de modos com acentos e icone para cada um", delegate
            {
                Assert.True(Array.IndexOf(ModeList.Names, "Retângulo") >= 0, "Retângulo");
                Assert.True(Array.IndexOf(ModeList.Names, "Rolagem automática") >= 0, "Rolagem automática");
                Assert.Equal(ModeList.Order.Length, ModeList.Names.Length, "um nome por modo");
                var seen = new HashSet<IconKind>();
                foreach (var m in ModeList.Order) Assert.True(seen.Add(ModeList.IconOf(m)), "icone repetido para " + m);
            });

            TestRunner.Run("visual: ChoiceButton troca a opcao, marca a escolhida e avisa", delegate
            {
                int changes = 0;
                var cb = new ChoiceButton(new[] { "A", "B", "C" }, delegate(int i) { return IconKind.Clock; }, new[] { "1", "2", "3" }, 20, true, true);
                cb.SelectedIndex = 1;
                cb.SelectedIndexChanged += delegate { changes++; };
                cb.SelectedIndex = 2;
                cb.SelectedIndex = 2;   // igual: nao avisa de novo
                Assert.Equal(1, changes, "avisos");
                Assert.Equal("C", cb.Text, "texto do botao");
                Assert.Equal("sel", cb.DropDownItems[2].Name, "item escolhido marcado");
                Assert.Equal("item", cb.DropDownItems[1].Name, "outro item desmarcado");
                Assert.Equal("3", ((ToolStripMenuItem)cb.DropDownItems[2]).ShortcutKeyDisplayString, "atalho no menu");
            });

            TestRunner.Run("visual: FlatButton clica, respeita Enabled e desenha todas as variantes", delegate
            {
                foreach (int pref in new[] { 1, 2 })
                {
                    int old = Theme.Preference; Theme.Preference = pref;
                    try
                    {
                        foreach (ButtonVariant v in Enum.GetValues(typeof(ButtonVariant)))
                        {
                            var b = new FlatButton { Text = "Teste", Width = 140, Height = 34, Variant = v, Icon = IconKind.Copy };
                            int clicks = 0;
                            b.Click += delegate { clicks++; };
                            b.PerformClick();
                            Assert.Equal(1, clicks, "clique (" + v + ")");
                            using (var bmp = new Bitmap(b.Width, b.Height)) b.DrawToBitmap(bmp, new Rectangle(0, 0, b.Width, b.Height));
                            b.Enabled = false;
                            using (var bmp = new Bitmap(b.Width, b.Height)) b.DrawToBitmap(bmp, new Rectangle(0, 0, b.Width, b.Height));
                        }
                    }
                    finally { Theme.Preference = old; }
                }
            });

            TestRunner.Run("visual: barras e menus aceitam o AppRenderer nos dois temas", delegate
            {
                foreach (int pref in new[] { 1, 2 })
                {
                    int old = Theme.Preference; Theme.Preference = pref;
                    try
                    {
                        var strip = new ToolStrip();
                        var btn = new ToolStripButton("x") { Tag = IconKind.Copy, Image = Icons.Placeholder(20) };
                        var cb = new ChoiceButton(new[] { "A", "B" }, null, null, 20, true, false);
                        strip.Items.Add(btn); strip.Items.Add(new ToolStripSeparator()); strip.Items.Add(cb);
                        Theme.Apply(strip, Theme.IsDark);
                        strip.Width = 300; strip.Height = 40;
                        using (var bmp = new Bitmap(300, 40)) strip.DrawToBitmap(bmp, new Rectangle(0, 0, 300, 40));
                        Assert.True(strip.Renderer is AppRenderer, "renderer do app");
                    }
                    finally { Theme.Preference = old; }
                }
            });

            TestRunner.Run("visual: risco das permissoes (nivel e explicacao)", delegate
            {
                Assert.Equal(RiskLevel.Low, PermissionRisk.Of("info"), "info");
                Assert.Equal(RiskLevel.Low, PermissionRisk.Of("windows"), "windows");
                Assert.Equal(RiskLevel.Medium, PermissionRisk.Of("target:window:code"), "janela de um programa");
                Assert.Equal(RiskLevel.High, PermissionRisk.Of("target:screen"), "tela");
                Assert.Equal(RiskLevel.High, PermissionRisk.Of("file:write:C:\\x\\*"), "gravar arquivo");
                Assert.Equal(RiskLevel.High, PermissionRisk.Of("target:window:*"), "qualquer janela");
                foreach (string k in new[] { "info", "windows", "target:screen", "target:window:code", "action:record", "action:ocr", "file:read:C:\\a", "file:write:C:\\a", "outra" })
                    Assert.True(PermissionRisk.Explain(k).Length > 10, "explicacao de " + k);
                Assert.Equal("Risco alto", PermissionRisk.Label(RiskLevel.High), "rotulo");
            });

            TestRunner.Run("visual: ajuda mantem o negrito no Markup e tira do texto puro", delegate
            {
                var t = HelpContent.Parse("## Um\nTexto **forte** aqui.\n- item **a**");
                Assert.True(t[0].Markup.Contains("**forte**"), "markup");
                Assert.True(!t[0].Body.Contains("**"), "texto puro");
                Assert.True(t[0].Markup.Contains("• item"), "lista");
            });
        }
    }
}
