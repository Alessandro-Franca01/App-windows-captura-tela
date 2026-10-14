using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace CapturaTela
{
    // Servidor MCP (Model Context Protocol) sobre stdio: `CapturaTelaCli.exe mcp`.
    // Cada ferramenta vira um comando da CLI; as permissoes passam pelo mesmo gate (PromptGate),
    // entao o usuario decide uma vez / sempre / negar a cada pedido da IA.

    class McpParam
    {
        public string Name, Option, Type, Description, Default;
        public bool Required;

        public McpParam(string name, string option, string type, string description)
        {
            Name = name; Option = option; Type = type; Description = description;
        }
    }

    class McpTool
    {
        public string Name, Command, Description;
        public List<McpParam> Params = new List<McpParam>();
        public bool ReturnsImage;
    }

    static class McpServer
    {
        public static int RunStdio()
        {
            Application.EnableVisualStyles();
            var gate = new PromptGate(PermissionDialog.Ask);
            Cli.Gate = gate;
            var session = new McpSession(gate);
            var utf8 = new UTF8Encoding(false);
            using (var input = new StreamReader(Console.OpenStandardInput(), utf8))
            using (var output = new StreamWriter(Console.OpenStandardOutput(), utf8) { AutoFlush = true, NewLine = "\n" })
            {
                string line;
                while ((line = input.ReadLine()) != null)
                {
                    if (line.Trim().Length == 0) continue;
                    string response = session.Handle(line);
                    if (response != null) output.WriteLine(response);
                }
            }
            return 0;
        }

        // ---------------------------------------------------------------- catalogo de ferramentas

        static McpParam P(string name, string option, string type, string description) { return new McpParam(name, option, type, description); }

        static List<McpParam> TargetParams()
        {
            return new List<McpParam>
            {
                P("window", "--window", "string", "Titulo ou nome do programa da janela (ex.: \"code\", \"Bloco de notas\"). Traz a janela para frente. Use list_windows para ver as opcoes."),
                P("active", "--active", "boolean", "Capturar a janela em primeiro plano."),
                P("region", "--region", "string", "Retangulo de tela \"x,y,largura,altura\" em pixels fisicos."),
                P("screen", "--screen", "integer", "Indice do monitor (0 = primeiro de screen_info)."),
                P("fullscreen", "--fullscreen", "boolean", "Toda a area virtual (todos os monitores). Prefira window: mostra menos dados pessoais.")
            };
        }

        const string TargetHelp = " Informe exatamente UM alvo: window, active, region, screen ou fullscreen.";

        public static readonly List<McpTool> Tools = BuildTools();

        static List<McpTool> BuildTools()
        {
            var tools = new List<McpTool>();

            tools.Add(new McpTool { Name = "screen_info", Command = "info", Description = "Informacoes dos monitores (indice, posicao, tamanho), area virtual e posicao do cursor." });

            var lw = new McpTool { Name = "list_windows", Command = "windows", Description = "Lista as janelas visiveis (titulo, programa, retangulo), da frente para tras." };
            lw.Params.Add(P("filter", "--filter", "string", "So janelas cujo titulo ou programa contem este texto."));
            tools.Add(lw);

            var cap = new McpTool { Name = "capture", Command = "capture", ReturnsImage = true, Description = "Captura a tela, um monitor, uma regiao ou uma janela e devolve a imagem." + TargetHelp };
            cap.Params.AddRange(TargetParams());
            cap.Params.Add(P("max_width", "--max-width", "integer", "Largura maxima em pixels (padrao 1280). Menor = menos tokens."));
            cap.Params[cap.Params.Count - 1].Default = "1280";
            cap.Params.Add(P("delay_ms", "--delay", "integer", "Espera em ms antes de capturar (use 500-1000 para janelas lentas)."));
            cap.Params.Add(P("cursor", "--cursor", "boolean", "Desenhar o ponteiro do mouse."));
            cap.Params.Add(P("out", "--out", "string", "Caminho do arquivo (.png/.jpg). Omitido: pasta temporaria do app. Fora dela exige permissao de gravacao."));
            tools.Add(cap);

            var px = new McpTool { Name = "get_pixel", Command = "pixel", Description = "Cor (#RRGGBB) de um pixel da tela ou de uma imagem." };
            px.Params.Add(P("at", "--at", "string", "Coordenada \"x,y\"."));
            px.Params[0].Required = true;
            px.Params.Add(P("image", "--image", "string", "Caminho de uma imagem; omitido = pixel da tela."));
            tools.Add(px);

            var ocr = new McpTool { Name = "ocr", Command = "ocr", Description = "Reconhece o texto de uma imagem ou de uma captura de tela (OCR do Windows)." + TargetHelp + " Ou informe image." };
            ocr.Params.AddRange(TargetParams());
            ocr.Params.Add(P("image", "--image", "string", "Caminho de uma imagem existente (em vez de um alvo de tela)."));
            tools.Add(ocr);

            var ann = new McpTool { Name = "annotate", Command = "annotate", ReturnsImage = true, Description = "Desenha em uma imagem (retangulo, elipse, seta, linha, texto, passo numerado) e esconde dados sensiveis (blur/pixelate, irreversivel). As operacoes sao aplicadas na ordem." };
            ann.Params.Add(P("input", "--in", "string", "Imagem de entrada."));
            ann.Params.Add(P("output", "--out", "string", "Imagem de saida (.png/.jpg)."));
            ann.Params[0].Required = true; ann.Params[1].Required = true;
            ann.Params.Add(P("max_width", "--max-width", "integer", "Reduz a imagem final."));
            tools.Add(ann);

            var gif = new McpTool { Name = "record_gif", Command = "gif", Description = "Grava a area em GIF animado e devolve o caminho do arquivo (nao e possivel ver GIF aqui; use capture para ver a tela)." + TargetHelp };
            gif.Params.AddRange(TargetParams());
            gif.Params.Add(P("seconds", "--seconds", "integer", "Duracao em segundos (1-120, padrao 5)."));
            gif.Params.Add(P("fps", "--fps", "integer", "Quadros por segundo (1-25, padrao 10)."));
            gif.Params.Add(P("max_width", "--max-width", "integer", "Largura maxima (padrao 960)."));
            gif.Params.Add(P("cursor", "--cursor", "boolean", "Desenhar o ponteiro do mouse."));
            gif.Params.Add(P("out", "--out", "string", "Caminho do .gif."));
            tools.Add(gif);

            var scr = new McpTool { Name = "scroll_capture", Command = "scroll", ReturnsImage = true, Description = "Rola o conteudo sob a area com a roda do mouse e costura uma imagem longa (pagina inteira)." + TargetHelp };
            scr.Params.AddRange(TargetParams());
            scr.Params.Add(P("max_steps", "--max-steps", "integer", "Maximo de rolagens (padrao 60)."));
            scr.Params.Add(P("out", "--out", "string", "Caminho do arquivo de saida."));
            tools.Add(scr);

            return tools;
        }

        static readonly string[] AnnotateOps = { "color", "width", "rect", "ellipse", "arrow", "line", "text", "step", "blur", "pixelate", "crop" };

        public static JObj Schema(McpTool t)
        {
            var props = new JObj();
            var required = new JArr();
            foreach (var p in t.Params)
            {
                props.Add(p.Name, new JObj().Add("type", p.Type).Add("description", p.Description));
                if (p.Required) required.Add(p.Name);
            }
            if (t.Name == "annotate")
            {
                var ops = new JObj().Add("type", "array").Add("description",
                    "Operacoes na ordem. op=color|width valem para as seguintes. Valores: color \"#RRGGBB\"; width \"N\"; rect/ellipse/blur/pixelate/crop \"x,y,w,h\"; arrow/line \"x1,y1,x2,y2\"; text \"x,y,texto\"; step \"x,y[,n]\".")
                    .Add("items", new JObj().Add("type", "object")
                        .Add("properties", new JObj()
                            .Add("op", new JObj().Add("type", "string").Add("enum", Enum(AnnotateOps)))
                            .Add("value", new JObj().Add("type", "string")))
                        .Add("required", Arr("op", "value")));
                props.Add("operations", ops);
                required.Add("operations");
            }
            var schema = new JObj().Add("type", "object").Add("properties", props);
            if (required.Count > 0) schema.Add("required", required);
            return schema;
        }

        static JArr Enum(string[] items) { var a = new JArr(); foreach (var i in items) a.Add(i); return a; }
        static JArr Arr(params string[] items) { return Enum(items); }

        // ---------------------------------------------------------------- argumentos -> linha de comando

        public static List<string> BuildArgs(McpTool t, Dictionary<string, object> args)
        {
            var known = new List<string>();
            foreach (var p in t.Params) known.Add(p.Name);
            if (t.Name == "annotate") known.Add("operations");
            foreach (var k in args.Keys)
                if (!known.Contains(k)) throw new CliException("Argumento desconhecido: \"" + k + "\". Aceitos: " + string.Join(", ", known.ToArray()) + ".");

            var argv = new List<string> { t.Command };
            foreach (var p in t.Params)
            {
                object v;
                bool has = args.TryGetValue(p.Name, out v) && v != null;
                if (!has)
                {
                    if (p.Required) throw new CliException("Falta o argumento \"" + p.Name + "\".");
                    if (p.Default != null) { argv.Add(p.Option); argv.Add(p.Default); }
                    continue;
                }
                if (p.Type == "boolean")
                {
                    if (!(v is bool)) throw new CliException("\"" + p.Name + "\" deve ser true ou false.");
                    if ((bool)v) argv.Add(p.Option);
                }
                else if (p.Type == "integer")
                {
                    if (!(v is long)) throw new CliException("\"" + p.Name + "\" deve ser um numero inteiro.");
                    argv.Add(p.Option); argv.Add(((long)v).ToString(CultureInfo.InvariantCulture));
                }
                else
                {
                    if (!(v is string)) throw new CliException("\"" + p.Name + "\" deve ser texto.");
                    argv.Add(p.Option); argv.Add((string)v);
                }
            }

            if (t.Name == "annotate")
            {
                object o;
                var list = args.TryGetValue("operations", out o) ? o as List<object> : null;
                if (list == null || list.Count == 0) throw new CliException("\"operations\" deve ser uma lista com ao menos uma operacao.");
                foreach (var item in list)
                {
                    var d = item as Dictionary<string, object>;
                    object op, val;
                    if (d == null || !d.TryGetValue("op", out op) || !d.TryGetValue("value", out val) || !(op is string) || !(val is string))
                        throw new CliException("Cada operacao precisa de {\"op\": texto, \"value\": texto}.");
                    if (Array.IndexOf(AnnotateOps, (string)op) < 0)
                        throw new CliException("Operacao desconhecida \"" + op + "\". Use: " + string.Join(", ", AnnotateOps) + ".");
                    argv.Add("--" + (string)op); argv.Add((string)val);
                }
            }
            return argv;
        }

        // ---------------------------------------------------------------- imagem inline

        const int InlineMaxSide = 1568, InlineMaxBytes = 2500000;

        public static JObj InlineImage(string file)
        {
            string ext = Path.GetExtension(file).ToLowerInvariant();
            if (ext != ".png" && ext != ".jpg" && ext != ".jpeg" && ext != ".bmp") return null;
            using (var img = Image.FromFile(file))
            {
                double scale = Math.Min(1.0, (double)InlineMaxSide / Math.Max(img.Width, img.Height));
                int w = Math.Max(1, (int)(img.Width * scale)), h = Math.Max(1, (int)(img.Height * scale));
                using (var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb))
                {
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.DrawImage(img, 0, 0, w, h);
                    }
                    byte[] bytes; string mime = "image/png";
                    using (var ms = new MemoryStream()) { bmp.Save(ms, ImageFormat.Png); bytes = ms.ToArray(); }
                    if (bytes.Length > InlineMaxBytes)
                    {
                        using (var flat = new Bitmap(w, h, PixelFormat.Format24bppRgb))
                        using (var ms = new MemoryStream())
                        {
                            using (var g = Graphics.FromImage(flat)) { g.Clear(Color.White); g.DrawImageUnscaled(bmp, 0, 0); }
                            var jpeg = ImageCodecInfo.GetImageEncoders().First(delegate(ImageCodecInfo c) { return c.MimeType == "image/jpeg"; });
                            var q = new EncoderParameters(1);
                            q.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 80L);
                            flat.Save(ms, jpeg, q);
                            bytes = ms.ToArray(); mime = "image/jpeg";
                        }
                    }
                    return new JObj().Add("type", "image").Add("data", Convert.ToBase64String(bytes)).Add("mimeType", mime);
                }
            }
        }
    }

    // Uma conexao MCP: estado do cliente + tratamento de cada mensagem JSON-RPC.
    class McpSession
    {
        static readonly string[] SupportedVersions = { "2025-06-18", "2025-03-26", "2024-11-05" };

        const string Instructions =
            "Captura de tela do Windows. O USUARIO decide o que voce pode fazer: cada acao nova abre uma janela de permissao " +
            "(uma vez / sempre / negar). Se receber 'Permissao negada', nao tente contornar: explique ao usuario o que precisa. " +
            "Prefira capturar uma janela (window) a tela inteira; use list_windows para descobrir os nomes. " +
            "Imagens voltam reduzidas (max_width 1280); o arquivo completo fica no caminho informado.";

        readonly PromptGate gate;
        string clientName = "Um aplicativo de IA";

        public McpSession(PromptGate gate) { this.gate = gate; }

        // Devolve a linha de resposta, ou null para notificacoes.
        public string Handle(string line)
        {
            object msg;
            try { msg = JsonParser.Parse(line); }
            catch (Exception e) { return ErrorResponse(null, -32700, "JSON invalido: " + e.Message); }

            var d = msg as Dictionary<string, object>;
            if (d == null) return ErrorResponse(null, -32600, "Requisicao invalida");

            object id; d.TryGetValue("id", out id);
            object m; d.TryGetValue("method", out m);
            string method = m as string;
            if (method == null) return null;                 // resposta do cliente a algo nosso: ignorar
            bool isRequest = id != null;

            try
            {
                switch (method)
                {
                    case "initialize": return Ok(id, Initialize(d));
                    case "ping": return Ok(id, new JObj());
                    case "tools/list": return Ok(id, ListTools());
                    case "tools/call": return Ok(id, CallTool(d));
                }
            }
            catch (CliException e) { return isRequest ? ErrorResponse(id, -32602, e.Message) : null; }
            catch (Exception e) { return isRequest ? ErrorResponse(id, -32603, e.GetType().Name + ": " + e.Message) : null; }

            if (!isRequest) return null;                     // notificacoes (initialized, cancelled...) nao tem resposta
            return ErrorResponse(id, -32601, "Metodo nao suportado: " + method);
        }

        static Dictionary<string, object> Dict(object o) { return o as Dictionary<string, object> ?? new Dictionary<string, object>(); }

        JObj Initialize(Dictionary<string, object> req)
        {
            var p = Dict(req.ContainsKey("params") ? req["params"] : null);
            object ci;
            if (p.TryGetValue("clientInfo", out ci))
            {
                object n; var cd = Dict(ci);
                if (cd.TryGetValue("name", out n) && n is string && ((string)n).Length > 0) clientName = (string)n + " (IA)";
            }
            gate.Client = clientName;
            object pv; string version = SupportedVersions[0];
            if (p.TryGetValue("protocolVersion", out pv) && pv is string && Array.IndexOf(SupportedVersions, (string)pv) >= 0) version = (string)pv;
            return new JObj()
                .Add("protocolVersion", version)
                .Add("capabilities", new JObj().Add("tools", new JObj().Add("listChanged", false)))
                .Add("serverInfo", new JObj().Add("name", "captura-tela").Add("version", Assembly.GetExecutingAssembly().GetName().Version.ToString()))
                .Add("instructions", Instructions);
        }

        static JObj ListTools()
        {
            var arr = new JArr();
            foreach (var t in McpServer.Tools)
                arr.Add(new JObj().Add("name", t.Name).Add("description", t.Description).Add("inputSchema", McpServer.Schema(t)));
            return new JObj().Add("tools", arr);
        }

        JObj CallTool(Dictionary<string, object> req)
        {
            var p = Dict(req.ContainsKey("params") ? req["params"] : null);
            object n; p.TryGetValue("name", out n);
            string name = n as string;
            McpTool tool = null;
            foreach (var t in McpServer.Tools) if (t.Name == name) tool = t;
            if (tool == null) throw new CliException("Ferramenta desconhecida: " + name);

            object a; p.TryGetValue("arguments", out a);
            var args = Dict(a);

            gate.BeginCall();
            string json; int exit;
            try
            {
                var argv = McpServer.BuildArgs(tool, args);
                json = Cli.Execute(argv.ToArray(), out exit);
            }
            catch (CliException e)
            {
                json = new JObj().Add("ok", false).Add("command", tool.Command).Add("error", e.Message).ToString();
                exit = e.Code;
            }

            var content = new JArr();
            if (exit == 0 && tool.ReturnsImage)
            {
                try
                {
                    var res = Dict(JsonParser.Parse(json));
                    object f;
                    if (res.TryGetValue("file", out f) && f is string)
                    {
                        var img = McpServer.InlineImage((string)f);
                        if (img != null) content.Add(img);
                    }
                }
                catch { /* sem imagem inline: o texto traz o caminho */ }
            }
            content.Add(new JObj().Add("type", "text").Add("text", json));
            return new JObj().Add("content", content).Add("isError", exit != 0);
        }

        // ---------------------------------------------------------------- respostas JSON-RPC

        static string Ok(object id, JObj result)
        {
            return "{\"jsonrpc\":\"2.0\",\"id\":" + Json.Value(id) + ",\"result\":" + result + "}";
        }

        static string ErrorResponse(object id, int code, string message)
        {
            return "{\"jsonrpc\":\"2.0\",\"id\":" + Json.Value(id) + ",\"error\":{\"code\":" + code + ",\"message\":" + Json.Quote(message) + "}}";
        }
    }
}
