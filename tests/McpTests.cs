// Testes do servidor MCP (src\McpServer.cs, src\JsonParser.cs), sem processo filho: McpSession.Handle recebe e devolve linhas JSON.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace CapturaTela.Tests
{
    static class McpTests
    {
        static Dictionary<string, object> D(object o) { return (Dictionary<string, object>)o; }
        static List<object> L(object o) { return (List<object>)o; }

        static Dictionary<string, object> Send(McpSession s, string line)
        {
            string r = s.Handle(line);
            Assert.True(r != null, "esperava resposta para: " + line);
            return D(JsonParser.Parse(r));
        }

        // sessao com arquivo de permissoes temporario; 'ask' responde aos pedidos
        static void WithSession(Func<PromptRequest, PromptAnswer> ask, Action<McpSession, List<string>> body)
        {
            string file = Helpers.TempFile(".ini");
            string old = Environment.GetEnvironmentVariable(PermissionStore.EnvPath);
            var oldGate = Cli.Gate;
            Environment.SetEnvironmentVariable(PermissionStore.EnvPath, file);
            try
            {
                var asked = new List<string>();
                var gate = new PromptGate(delegate(PromptRequest r) { asked.Add(r.Key); return ask(r); });
                Cli.Gate = gate;
                body(new McpSession(gate), asked);
            }
            finally
            {
                Cli.Gate = oldGate;
                Environment.SetEnvironmentVariable(PermissionStore.EnvPath, old);
                try { File.Delete(file); } catch { }
            }
        }

        static string Call(string tool, string argsJson, int id)
        {
            return "{\"jsonrpc\":\"2.0\",\"id\":" + id + ",\"method\":\"tools/call\",\"params\":{\"name\":\"" + tool + "\",\"arguments\":" + argsJson + "}}";
        }

        public static void Register()
        {
            TestRunner.Run("mcp: leitor JSON (escapes, numeros, aninhamento e erros)", delegate
            {
                var d = D(JsonParser.Parse("{\"a\":[1,2.5,-3e2,true,null],\"b\":{\"c\":\"x\\ny\\u00e9\\\"\"}}"));
                var a = L(d["a"]);
                Assert.Equal(1L, a[0], "inteiro vira long");
                Assert.Equal(2.5, a[1], "decimal vira double");
                Assert.Equal(-300.0, a[2], "expoente");
                Assert.Equal(true, a[3], "bool");
                Assert.True(a[4] == null, "null");
                Assert.Equal("x\nyé\"", D(d["b"])["c"], "escapes");
                foreach (string bad in new[] { "{", "[1,]", "{\"a\" 1}", "\"abc", "tru", "{} x" })
                {
                    bool failed = false;
                    try { JsonParser.Parse(bad); } catch (FormatException) { failed = true; }
                    Assert.True(failed, "deveria rejeitar: " + bad);
                }
            });

            TestRunner.Run("mcp: initialize, ping, tools/list e metodos desconhecidos", delegate
            {
                WithSession(delegate { return PromptAnswer.Deny; }, delegate(McpSession s, List<string> asked)
                {
                    var init = Send(s, "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2024-11-05\",\"clientInfo\":{\"name\":\"teste\",\"version\":\"1\"},\"capabilities\":{}}}");
                    var res = D(init["result"]);
                    Assert.Equal("2024-11-05", res["protocolVersion"], "devolve a versao do cliente");
                    Assert.Equal("captura-tela", D(res["serverInfo"])["name"], "nome do servidor");
                    Assert.True(D(res["capabilities"]).ContainsKey("tools"), "declara tools");

                    Assert.True(s.Handle("{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}") == null, "notificacao nao tem resposta");
                    Assert.True(D(Send(s, "{\"jsonrpc\":\"2.0\",\"id\":\"p\",\"method\":\"ping\"}")["result"]).Count == 0, "ping");

                    var tools = L(D(Send(s, "{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/list\"}")["result"])["tools"]);
                    var names = new List<string>();
                    foreach (var t in tools)
                    {
                        var td = D(t);
                        names.Add((string)td["name"]);
                        Assert.Equal("object", D(td["inputSchema"])["type"], "schema de " + td["name"]);
                    }
                    foreach (string n in new[] { "screen_info", "list_windows", "capture", "get_pixel", "ocr", "annotate", "record_gif", "scroll_capture" })
                        Assert.True(names.Contains(n), "falta a ferramenta " + n);

                    var err = D(Send(s, "{\"jsonrpc\":\"2.0\",\"id\":3,\"method\":\"nao/existe\"}")["error"]);
                    Assert.Equal(-32601L, err["code"], "metodo desconhecido");
                    Assert.Equal(-32700L, D(D(Send(s, "{ruim"))["error"])["code"], "JSON invalido");
                    Assert.True(s.Handle("{\"jsonrpc\":\"2.0\",\"method\":\"nao/existe\"}") == null, "notificacao desconhecida e ignorada");
                    Assert.Equal(0, asked.Count, "nenhuma permissao pedida ate aqui");
                });
            });

            TestRunner.Run("mcp: capture devolve imagem inline e respeita o 'uma vez'", delegate
            {
                WithSession(delegate { return PromptAnswer.Once; }, delegate(McpSession s, List<string> asked)
                {
                    var r = D(Send(s, Call("capture", "{\"region\":\"0,0,200,120\"}", 10))["result"]);
                    Assert.Equal(false, r["isError"], "sem erro: " + r);
                    var content = L(r["content"]);
                    var img = D(content[0]);
                    Assert.Equal("image", img["type"], "primeiro item e a imagem");
                    using (var ms = new MemoryStream(Convert.FromBase64String((string)img["data"])))
                    using (var bmp = Image.FromStream(ms))
                        Assert.True(bmp.Width == 200 && bmp.Height == 120, "tamanho da imagem inline: " + bmp.Width + "x" + bmp.Height);
                    Assert.True(((string)D(content[1])["text"]).Contains("\"file\""), "o texto traz o caminho");
                    Assert.Equal(1, asked.Count, "pediu so a permissao de tela");
                    Send(s, Call("capture", "{\"region\":\"0,0,50,50\"}", 11));
                    Assert.Equal(2, asked.Count, "'uma vez' nao vale para a chamada seguinte");
                });
            });

            TestRunner.Run("mcp: permissao salva dispensa a pergunta; negada vira isError com denied", delegate
            {
                WithSession(delegate { return PromptAnswer.Always; }, delegate(McpSession s, List<string> asked)
                {
                    Send(s, Call("screen_info", "{}", 20));
                    Send(s, Call("screen_info", "{}", 21));
                    Assert.Equal(1, asked.Count, "'sempre' foi lembrado");
                });
                WithSession(delegate { return PromptAnswer.DenyAlways; }, delegate(McpSession s, List<string> asked)
                {
                    var r = D(Send(s, Call("list_windows", "{}", 30))["result"]);
                    Assert.Equal(true, r["isError"], "negado e erro");
                    string text = (string)D(L(r["content"])[0])["text"];
                    Assert.True(text.Contains("\"denied\":true"), "texto: " + text);
                    Send(s, Call("list_windows", "{}", 31));
                    Assert.Equal(1, asked.Count, "negar sempre nao pergunta de novo");
                });
            });

            TestRunner.Run("mcp: argumentos invalidos e ferramenta desconhecida", delegate
            {
                WithSession(delegate { return PromptAnswer.Deny; }, delegate(McpSession s, List<string> asked)
                {
                    var r = D(Send(s, Call("capture", "{\"nada\":1}", 40))["result"]);
                    Assert.Equal(true, r["isError"], "argumento desconhecido");
                    r = D(Send(s, Call("capture", "{\"max_width\":\"grande\",\"fullscreen\":true}", 41))["result"]);
                    Assert.Equal(true, r["isError"], "tipo errado");
                    r = D(Send(s, Call("get_pixel", "{}", 42))["result"]);
                    Assert.Equal(true, r["isError"], "falta argumento obrigatorio");
                    var e = D(Send(s, Call("naoexiste", "{}", 43))["error"]);
                    Assert.Equal(-32602L, e["code"], "ferramenta desconhecida");
                    Assert.Equal(0, asked.Count, "argumento invalido nao chega a pedir permissao");
                });
            });

            TestRunner.Run("mcp: annotate monta as operacoes na ordem e devolve a imagem", delegate
            {
                string input = Helpers.TempFile(".png"), output = Helpers.TempFile(".png");
                try
                {
                    using (var b = Helpers.Solid(120, 80, Color.White)) b.Save(input, ImageFormat.Png);
                    WithSession(delegate { return PromptAnswer.Once; }, delegate(McpSession s, List<string> asked)
                    {
                        string args = "{\"input\":" + Json.Quote(input) + ",\"output\":" + Json.Quote(output) +
                            ",\"operations\":[{\"op\":\"color\",\"value\":\"#FF0000\"},{\"op\":\"rect\",\"value\":\"10,10,60,40\"},{\"op\":\"crop\",\"value\":\"5,5,100,60\"}]}";
                        var r = D(Send(s, Call("annotate", args, 50))["result"]);
                        Assert.Equal(false, r["isError"], "annotate: " + r["content"]);
                        Assert.Equal("image", D(L(r["content"])[0])["type"], "devolve a imagem");
                        using (var bmp = Image.FromFile(output)) Assert.True(bmp.Width == 100 && bmp.Height == 60, "recortada para 100x60");
                        var bad = D(Send(s, Call("annotate", "{\"input\":" + Json.Quote(input) + ",\"output\":" + Json.Quote(output) + ",\"operations\":[{\"op\":\"explodir\",\"value\":\"1\"}]}", 51))["result"]);
                        Assert.Equal(true, bad["isError"], "operacao invalida");
                    });
                }
                finally { try { File.Delete(input); File.Delete(output); } catch { } }
            });
        }
    }
}
