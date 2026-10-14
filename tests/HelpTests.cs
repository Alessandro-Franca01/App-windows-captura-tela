// Testes da ajuda (src\HelpForm.cs): o guia docs\GUIA.md vai embutido e vira os topicos da janela.
using System;
using System.Windows.Forms;

namespace CapturaTela.Tests
{
    static class HelpTests
    {
        public static void Register()
        {
            TestRunner.Run("ajuda: o guia embutido vira topicos legiveis", delegate
            {
                var topics = HelpContent.Load();
                Assert.True(topics.Count >= 8, "topicos: " + topics.Count);
                string all = "";
                foreach (var t in topics)
                {
                    Assert.True(t.Title.Length > 0 && t.Body.Length > 40, "topico vazio: " + t.Title);
                    Assert.True(t.Body.IndexOf("**", StringComparison.Ordinal) < 0 && t.Body.IndexOf('`') < 0, "marcacao sobrando em: " + t.Title);
                    all += t.Title + "|";
                }
                foreach (string must in new[] { "Comecando", "Modos de captura", "Rolagem", "Atalhos", "Problemas comuns" })
                    Assert.True(all.IndexOf(must, StringComparison.OrdinalIgnoreCase) >= 0, "falta o topico " + must);
            });

            TestRunner.Run("ajuda: markdown simples e convertido (negrito, codigo, listas) e sem guia mostra aviso", delegate
            {
                var t = HelpContent.Parse("# Titulo\nintro ignorada\n\n## Um\nTexto **forte** e `codigo`.\n- item a\n- item b\n\n## Dois\nfim");
                Assert.Equal(2, t.Count, "dois topicos");
                Assert.Equal("Um", t[0].Title, "titulo");
                Assert.True(t[0].Body.StartsWith("Texto forte e codigo."), "negrito e codigo: " + t[0].Body);
                Assert.True(t[0].Body.Contains("• item a") && t[0].Body.Contains("• item b"), "listas");
                Assert.True(t[0].Body.IndexOf("intro ignorada", StringComparison.Ordinal) < 0, "introducao fica de fora");
                var none = HelpContent.Parse(null);
                Assert.True(none.Count == 1 && none[0].Body.Contains("nao foi encontrado"), "aviso sem guia");
            });

            TestRunner.Run("ajuda: a janela mostra o topico escolhido e troca ao selecionar outro", delegate
            {
                using (var f = new HelpForm(1))
                {
                    string first = null, second = null;
                    f.Shown += delegate
                    {
                        first = f.Reader.Text;
                        f.TopicList.SelectedIndex = 3;
                        second = f.Reader.Text;
                        f.Close();
                    };
                    f.ShowDialog();
                    Assert.True(first != null && first.Length > 40, "texto do topico 1");
                    Assert.True(second != null && second != first && second.Length > 40, "texto mudou para o topico 3");
                }
            });
        }
    }
}
