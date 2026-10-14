// Testes do sistema de permissoes da IA (src\Permissions.cs, PermissionDialog.cs e o gate da CLI).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace CapturaTela.Tests
{
    static class PermissionTests
    {
        // roda 'body' com um arquivo de permissoes temporario e o gate padrao restaurado no fim
        static void WithStore(Action<string> body)
        {
            string file = Helpers.TempFile(".ini");
            string old = Environment.GetEnvironmentVariable(PermissionStore.EnvPath);
            var oldGate = Cli.Gate;
            Environment.SetEnvironmentVariable(PermissionStore.EnvPath, file);
            try { body(file); }
            finally
            {
                Cli.Gate = oldGate;
                Environment.SetEnvironmentVariable(PermissionStore.EnvPath, old);
                try { File.Delete(file); } catch { }
            }
        }

        static PromptGate Gate(List<string> asked, PromptAnswer answer)
        {
            return new PromptGate(delegate(PromptRequest r) { asked.Add(r.Key); return answer; });
        }

        static string Run(out int code, params string[] args) { return Cli.Execute(args, out code); }

        public static void Register()
        {
            TestRunner.Run("permissoes: curingas, negacao vence e arquivo ida-e-volta", delegate
            {
                WithStore(delegate(string file)
                {
                    var s = PermissionStore.Load();
                    s.Set("target:window:*", true);
                    s.Set("target:window:chrome", false);
                    s.Set(@"file:read:C:\fotos\*", true);
                    s.GateCli = true; s.Save();

                    var t = PermissionStore.Load();
                    Assert.True(t.GateCli && t.Enabled, "chaves gerais");
                    Assert.Equal(true, t.Lookup("target:window:code"), "curinga permite outro programa");
                    Assert.Equal(false, t.Lookup("target:window:CHROME"), "negacao especifica vence o curinga");
                    Assert.Equal(true, t.Lookup(@"file:read:c:\FOTOS\a\b.png"), "prefixo de pasta, sem diferenciar maiusculas");
                    Assert.True(t.Lookup("info") == null, "o que nao foi decidido fica indefinido");
                    t.Remove("target:window:chrome");
                    Assert.Equal(true, t.Lookup("target:window:chrome"), "revogar a negacao volta ao curinga");
                });
            });

            TestRunner.Run("permissoes: uma vez, sempre, negar sempre e chave geral", delegate
            {
                WithStore(delegate(string file)
                {
                    var asked = new List<string>();
                    var once = Gate(asked, PromptAnswer.Once);
                    once.BeginCall();
                    once.Require("info", "info", "x"); once.Require("info", "info", "x");
                    Assert.Equal(1, asked.Count, "'uma vez' vale por toda a chamada");
                    once.BeginCall();
                    once.Require("info", "info", "x");
                    Assert.Equal(2, asked.Count, "chamada nova pergunta de novo");
                    Assert.True(PermissionStore.Load().Lookup("info") == null, "'uma vez' nao e salvo");

                    asked.Clear();
                    Gate(asked, PromptAnswer.Always).Require("windows", "windows", "x");
                    Gate(asked, PromptAnswer.Deny).Require("windows", "windows", "x");   // nao pergunta: ja esta salvo
                    Assert.Equal(1, asked.Count, "'sempre' fica salvo");

                    asked.Clear();
                    bool denied = false;
                    try { Gate(asked, PromptAnswer.DenyAlways).Require("action:ocr", "action:ocr", "ocr"); }
                    catch (CliException e) { denied = e.Code == 3; }
                    Assert.True(denied, "negar sempre lanca codigo 3");
                    try { Gate(asked, PromptAnswer.Once).Require("action:ocr", "action:ocr", "ocr"); denied = false; }
                    catch (CliException e) { denied = e.Code == 3; }
                    Assert.True(denied && asked.Count == 1, "negacao salva nao pergunta de novo");

                    var s = PermissionStore.Load(); s.Enabled = false; s.Save();
                    asked.Clear();
                    denied = false;
                    try { Gate(asked, PromptAnswer.Always).Require("info", "info", "x"); }
                    catch (CliException) { denied = true; }
                    Assert.True(denied && asked.Count == 0, "com a chave geral desligada, nada e permitido nem perguntado");
                });
            });

            TestRunner.Run("permissoes: negar um pedido resulta em codigo 3 e nenhum arquivo", delegate
            {
                WithStore(delegate(string file)
                {
                    var asked = new List<string>();
                    Cli.Gate = Gate(asked, PromptAnswer.Deny);
                    string output = Helpers.TempFile(".png");
                    int code;
                    string j = Run(out code, "capture", "--region", "0,0,50,50", "--out", output);
                    Assert.Equal(3, code, "codigo: " + j);
                    Assert.True(j.IndexOf("\"denied\":true", StringComparison.Ordinal) >= 0, "campo denied: " + j);
                    Assert.True(!File.Exists(output), "nao deve gravar nada");
                    Assert.Equal("target:screen", asked[0], "primeiro pedido");

                    asked.Clear();
                    Run(out code, "windows"); Assert.Equal(3, code, "windows negado"); Assert.Equal("windows", asked[0], "chave");
                    asked.Clear();
                    Run(out code, "ocr", "--image", output); Assert.Equal(3, code, "ocr negado"); Assert.Equal("action:ocr", asked[0], "chave ocr");
                    asked.Clear();
                    Run(out code, "gif", "--region", "0,0,50,50", "--seconds", "1"); Assert.Equal(3, code, "gif negado"); Assert.Equal("action:record", asked[0], "chave gif");
                });
            });

            TestRunner.Run("permissoes: 'uma vez' libera a captura e o arquivo fora da pasta de saida pede escrita", delegate
            {
                WithStore(delegate(string file)
                {
                    var asked = new List<string>();
                    Cli.Gate = Gate(asked, PromptAnswer.Once);
                    ((PromptGate)Cli.Gate).BeginCall();
                    string output = Helpers.TempFile(".png");
                    int code;
                    string j = Run(out code, "capture", "--region", "0,0,50,50", "--out", output);
                    Assert.Equal(0, code, "captura permitida: " + j);
                    Assert.True(File.Exists(output), "arquivo gravado");
                    Assert.Equal(2, asked.Count, "tela + escrita");
                    Assert.Equal("target:screen", asked[0], "primeiro");
                    Assert.True(asked[1].StartsWith("file:write:", StringComparison.Ordinal), "segundo: " + asked[1]);
                    try { File.Delete(output); } catch { }

                    // sem --out: vai para a pasta de saida do app e nao pede escrita
                    asked.Clear();
                    ((PromptGate)Cli.Gate).BeginCall();
                    j = Run(out code, "capture", "--region", "0,0,50,50");
                    Assert.Equal(0, code, "captura na pasta de saida: " + j);
                    Assert.Equal(1, asked.Count, "so a permissao de tela");
                });
            });

            TestRunner.Run("permissoes: janela e verificada antes de ser trazida para frente", delegate
            {
                WithStore(delegate(string file)
                {
                    var wins = Native.ListWindows(false);
                    if (wins.Count == 0) Assert.Skip("nenhuma janela visivel");
                    var asked = new List<string>();
                    Cli.Gate = Gate(asked, PromptAnswer.Deny);
                    int code;
                    string j = Run(out code, "capture", "--window", wins[0].Title.Length > 0 ? wins[0].Title : wins[0].Process);
                    Assert.Equal(3, code, "negado: " + j);
                    Assert.True(asked[0].StartsWith("target:window:", StringComparison.Ordinal), "chave: " + asked[0]);
                });
            });

            TestRunner.Run("permissoes: botoes do dialogo devolvem a resposta certa", delegate
            {
                var req = new PromptRequest { Client = "Teste", Key = "info", AlwaysKey = "info", Description = "Ver informacoes" };
                var map = new Dictionary<string, PromptAnswer>();
                foreach (string which in new[] { "once", "always", "deny", "denyalways" })
                {
                    using (var d = new PermissionDialog(req, 0))
                    {
                        string w = which;
                        d.Shown += delegate
                        {
                            Button b = w == "once" ? d.OnceBtn : w == "always" ? d.AlwaysBtn : w == "deny" ? d.DenyBtn : d.DenyAlwaysBtn;
                            b.PerformClick();
                        };
                        d.ShowDialog();
                        map[which] = d.Answer;
                    }
                }
                Assert.Equal(PromptAnswer.Once, map["once"], "uma vez");
                Assert.Equal(PromptAnswer.Always, map["always"], "sempre");
                Assert.Equal(PromptAnswer.Deny, map["deny"], "negar");
                Assert.Equal(PromptAnswer.DenyAlways, map["denyalways"], "negar sempre");
            });
            TestRunner.Run("permissoes: botoes ficam desativados no inicio (evita aprovar sem querer com Enter)", delegate
            {
                var req = new PromptRequest { Client = "Teste", Key = "info", AlwaysKey = "info", Description = "x" };
                bool before = true, after = false, denyFocused = false;
                using (var d = new PermissionDialog(req, 500))
                {
                    d.Shown += delegate
                    {
                        before = d.OnceBtn.Enabled;
                        denyFocused = d.ActiveControl == d.DenyBtn;
                        var t = new Timer { Interval = 1200 };
                        t.Tick += delegate { t.Stop(); after = d.OnceBtn.Enabled; d.Close(); };
                        t.Start();
                    };
                    d.ShowDialog();
                    Assert.Equal(PromptAnswer.Deny, d.Answer, "fechar sem escolher nega");
                }
                Assert.True(!before, "desativado logo ao abrir");
                Assert.True(after, "ativado depois do atraso");
                Assert.True(denyFocused, "foco inicial em Negar");
            });


            TestRunner.Run("permissoes: sem resposta o dialogo fecha sozinho e nega", delegate
            {
                var req = new PromptRequest { Client = "Teste", Key = "info", AlwaysKey = "info", Description = "x" };
                using (var d = new PermissionDialog(req, 0, 2))
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    d.ShowDialog();   // ninguem clica: deve fechar em ~2 s
                    Assert.True(sw.ElapsedMilliseconds >= 1500 && sw.ElapsedMilliseconds < 6000, "fechou apos " + sw.ElapsedMilliseconds + " ms");
                    Assert.Equal(PromptAnswer.Deny, d.Answer, "sem resposta = negar");
                }
            });        }
    }
}
