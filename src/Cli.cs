using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace CapturaTela
{
    class CliException : Exception
    {
        public readonly int Code;
        public CliException(string message, int code) : base(message) { Code = code; }
        public CliException(string message) : base(message) { Code = 2; }   // erro de uso
    }

    // Argumentos "--chave valor" na ordem em que foram dados (o annotate depende da ordem).
    class CliArgs
    {
        static readonly string[] Flags = { "clipboard", "cursor", "no-activate", "fullscreen", "active", "help" };

        public string Command = "";
        public readonly List<KeyValuePair<string, string>> Opts = new List<KeyValuePair<string, string>>();

        public static CliArgs Parse(string[] args)
        {
            var a = new CliArgs();
            int i = 0;
            if (args.Length > 0 && !args[0].StartsWith("--")) { a.Command = args[0].ToLowerInvariant(); i = 1; }
            for (; i < args.Length; i++)
            {
                string t = args[i];
                if (!t.StartsWith("--")) throw new CliException("Argumento inesperado: " + t);
                string key = t.Substring(2).ToLowerInvariant();
                if (Array.IndexOf(Flags, key) >= 0) { a.Opts.Add(new KeyValuePair<string, string>(key, "true")); continue; }
                if (i + 1 >= args.Length) throw new CliException("A opcao --" + key + " precisa de um valor.");
                a.Opts.Add(new KeyValuePair<string, string>(key, args[++i]));
            }
            return a;
        }

        public bool Has(string key) { foreach (var o in Opts) if (o.Key == key) return true; return false; }

        public string Get(string key)
        {
            for (int i = Opts.Count - 1; i >= 0; i--) if (Opts[i].Key == key) return Opts[i].Value;
            return null;
        }

        public int GetInt(string key, int def, int min, int max)
        {
            string s = Get(key);
            if (s == null) return def;
            int v;
            if (!int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v)) throw new CliException("--" + key + " deve ser um numero inteiro: " + s);
            if (v < min || v > max) throw new CliException("--" + key + " deve estar entre " + min + " e " + max + ".");
            return v;
        }
    }

    // Interface de linha de comando: cada comando imprime UMA linha de JSON em stdout.
    // Codigos de saida: 0 = ok, 1 = erro ao executar, 2 = uso incorreto.
    static class Cli
    {
        static readonly string[] Usage =
        {
            "CapturaTelaCli <comando> [opcoes]   (saida: uma linha de JSON; codigo 0 ok, 1 erro, 2 uso incorreto)",
            "",
            "Alvos (use exatamente um):  --fullscreen | --screen N | --region x,y,w,h | --window \"titulo ou processo\" | --active",
            "",
            "  info                                 versao, monitores e posicao do cursor",
            "  windows [--filter texto]             janelas visiveis com titulo, processo e retangulo",
            "  pixel --at x,y | --image arq         cor de um pixel da tela ou de uma imagem",
            "  capture <alvo> [--out arq] [--format png|jpg] [--max-width N] [--delay ms] [--cursor] [--clipboard]",
            "  gif <alvo> [--out arq.gif] [--seconds 5] [--fps 10] [--max-width 960] [--cursor]",
            "  scroll <alvo> [--out arq] [--max-steps 60]      rola o conteudo sob a area e costura as imagens",
            "  ocr (<alvo> | --image arq)           reconhece o texto (motor de OCR do Windows)",
            "  annotate --in arq --out arq [operacoes na ordem]",
            "      --color #RRGGBB  --width N        valem para as operacoes seguintes",
            "      --rect x,y,w,h  --ellipse x,y,w,h  --arrow x1,y1,x2,y2  --line x1,y1,x2,y2",
            "      --text x,y,texto  --step x,y[,n]  --blur x,y,w,h  --pixelate x,y,w,h  --crop x,y,w,h",
            "      --max-width N                     reduz a imagem final",
            "",
            "Coordenadas em pixels fisicos; a origem da tela virtual pode ser negativa (veja info).",
            "Sem --out, os arquivos vao para %TEMP%\\CapturaTela."
        };

        public static int Run(string[] args)
        {
            int code;
            string json = Execute(args, out code);
            var bytes = new UTF8Encoding(false).GetBytes(json + "\n");
            using (var stdout = Console.OpenStandardOutput()) stdout.Write(bytes, 0, bytes.Length);
            return code;
        }

        public static string Execute(string[] args, out int exit)
        {
            exit = 0;
            string command = "";
            try
            {
                var a = CliArgs.Parse(args);
                command = a.Command;
                JObj r;
                switch (a.Command)
                {
                    case "": case "help": r = Help(); break;
                    case "info": r = Info(); break;
                    case "windows": r = Windows(a); break;
                    case "pixel": r = Pixel(a); break;
                    case "capture": r = Capture(a); break;
                    case "gif": r = Gif(a); break;
                    case "scroll": r = Scroll(a); break;
                    case "ocr": r = OcrCmd(a); break;
                    case "annotate": r = Annotate(a); break;
                    default: throw new CliException("Comando desconhecido: " + a.Command + ". Use 'help'.");
                }
                var result = new JObj().Add("ok", true).Add("command", a.Command == "" ? "help" : a.Command);
                return Merge(result, r);
            }
            catch (CliException e) { exit = e.Code; return Error(command, e.Message); }
            catch (Exception e) { exit = 1; return Error(command, e.GetType().Name + ": " + e.Message); }
        }

        static string Error(string command, string message)
        {
            return new JObj().Add("ok", false).Add("command", command).Add("error", message).ToString();
        }

        // junta as chaves de b depois das de a
        static string Merge(JObj a, JObj b)
        {
            string sa = a.ToString(), sb = b.ToString();
            if (sb == "{}") return sa;
            return sa.Substring(0, sa.Length - 1) + "," + sb.Substring(1);
        }

        // ---------------------------------------------------------------- comandos

        static JObj Help()
        {
            var lines = new JArr();
            foreach (var l in Usage) lines.Add(l);
            return new JObj().Add("usage", lines);
        }

        static JObj Info()
        {
            var screens = new JArr();
            var all = Screen.AllScreens;
            for (int i = 0; i < all.Length; i++)
                screens.Add(new JObj().Add("index", i).Add("primary", all[i].Primary).Add("name", all[i].DeviceName)
                    .Add("x", all[i].Bounds.X).Add("y", all[i].Bounds.Y).Add("width", all[i].Bounds.Width).Add("height", all[i].Bounds.Height));
            var cur = Cursor.Position;
            return new JObj()
                .Add("version", Assembly.GetExecutingAssembly().GetName().Version.ToString())
                .Add("virtualScreen", Json.Rect(SystemInformation.VirtualScreen))
                .Add("screens", screens)
                .Add("cursor", new JObj().Add("x", cur.X).Add("y", cur.Y))
                .Add("outputDir", OutputDir());
        }

        static JObj Windows(CliArgs a)
        {
            string filter = a.Get("filter");
            var arr = new JArr();
            foreach (var w in Native.ListWindows(false))
            {
                if (filter != null && !Matches(w, filter)) continue;
                arr.Add(WindowJson(w));
            }
            return new JObj().Add("count", arr.Count).Add("windows", arr);
        }

        static JObj WindowJson(WindowInfo w)
        {
            return new JObj().Add("handle", w.Handle.ToInt64()).Add("title", w.Title).Add("process", w.Process)
                .Add("pid", w.Pid).Add("bounds", Json.Rect(w.Bounds));
        }

        static bool Matches(WindowInfo w, string text)
        {
            return w.Title.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0
                || w.Process.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static JObj Pixel(CliArgs a)
        {
            Color c;
            JObj where;
            if (a.Has("image"))
            {
                var p = ParseInts(a.Get("at"), 2, "--at");
                using (var bmp = LoadImage(a.Get("image")))
                {
                    if (p[0] < 0 || p[1] < 0 || p[0] >= bmp.Width || p[1] >= bmp.Height) throw new CliException("Ponto fora da imagem (" + bmp.Width + "x" + bmp.Height + ").", 1);
                    c = bmp.GetPixel(p[0], p[1]);
                }
                where = new JObj().Add("x", p[0]).Add("y", p[1]);
            }
            else
            {
                var p = ParseInts(a.Get("at"), 2, "--at");
                var vs = SystemInformation.VirtualScreen;
                if (!vs.Contains(p[0], p[1])) throw new CliException("Ponto fora da tela virtual.", 1);
                using (var bmp = Grab(new Rectangle(p[0], p[1], 1, 1), false)) c = bmp.GetPixel(0, 0);
                where = new JObj().Add("x", p[0]).Add("y", p[1]);
            }
            return new JObj().Add("at", where).Add("color", "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2"))
                .Add("r", (int)c.R).Add("g", (int)c.G).Add("b", (int)c.B);
        }

        static JObj Capture(CliArgs a)
        {
            int delay = a.GetInt("delay", 0, 0, 60000);
            int maxWidth = a.GetInt("max-width", 0, 0, 20000);
            JObj targetInfo;
            var region = ResolveTarget(a, out targetInfo);
            if (delay > 0) Thread.Sleep(delay);

            using (var raw = Grab(region, a.Has("cursor")))
            {
                if (a.Has("clipboard")) Clipboard.SetImage(raw);
                using (var outBmp = Downscale(raw, maxWidth))
                {
                    string file = OutPath(a, "captura", ExtFor(a));
                    SaveImage(outBmp, file);
                    var r = new JObj().Add("file", file).Add("width", outBmp.Width).Add("height", outBmp.Height)
                        .Add("bytes", new FileInfo(file).Length).Add("region", Json.Rect(region)).Add("target", targetInfo);
                    if (outBmp.Width != raw.Width) r.Add("capturedWidth", raw.Width).Add("capturedHeight", raw.Height);
                    if (a.Has("clipboard")) r.Add("clipboard", true);
                    return r;
                }
            }
        }

        static JObj Gif(CliArgs a)
        {
            int seconds = a.GetInt("seconds", 5, 1, 120);
            int fps = a.GetInt("fps", 10, 1, 25);
            int maxWidth = a.GetInt("max-width", 960, 16, 20000);
            bool cursor = a.Has("cursor");
            JObj targetInfo;
            var region = ResolveTarget(a, out targetInfo);

            float scale = region.Width > maxWidth ? (float)maxWidth / region.Width : 1f;
            int w = Math.Max(1, (int)(region.Width * scale)), h = Math.Max(1, (int)(region.Height * scale));
            long estimate = (long)w * h * 3 * seconds * fps;
            if (estimate > 900L * 1024 * 1024)
                throw new CliException("A gravacao usaria ~" + (estimate / (1024 * 1024)) + " MB de memoria. Reduza --seconds, --fps ou --max-width.", 1);

            string file = OutPath(a, "gravacao", ".gif");
            var frames = new List<Bitmap>();
            try
            {
                var clock = Stopwatch.StartNew();
                long interval = 1000 / fps;
                while (clock.ElapsedMilliseconds < seconds * 1000L)
                {
                    using (var raw = Grab(region, cursor))
                    {
                        var small = new Bitmap(w, h, PixelFormat.Format24bppRgb);
                        using (var g = Graphics.FromImage(small))
                        {
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            g.DrawImage(raw, new Rectangle(0, 0, w, h), 0, 0, raw.Width, raw.Height, GraphicsUnit.Pixel);
                        }
                        frames.Add(small);
                    }
                    long wait = frames.Count * interval - clock.ElapsedMilliseconds;
                    if (wait > 0) Thread.Sleep((int)wait);
                }
                int delay = (int)Math.Max(20, clock.ElapsedMilliseconds / Math.Max(1, frames.Count));
                GifWriter.Save(file, frames, delay);
                return new JObj().Add("file", file).Add("frames", frames.Count).Add("seconds", seconds).Add("fps", fps)
                    .Add("width", w).Add("height", h).Add("bytes", new FileInfo(file).Length)
                    .Add("region", Json.Rect(region)).Add("target", targetInfo);
            }
            finally { foreach (var f in frames) f.Dispose(); }
        }

        static JObj Scroll(CliArgs a)
        {
            int steps = a.GetInt("max-steps", 60, 1, 200);
            JObj targetInfo;
            var region = ResolveTarget(a, out targetInfo);
            using (var bmp = ScrollCapture.Run(region, steps))
            {
                string file = OutPath(a, "rolagem", ExtFor(a));
                SaveImage(bmp, file);
                return new JObj().Add("file", file).Add("width", bmp.Width).Add("height", bmp.Height)
                    .Add("bytes", new FileInfo(file).Length).Add("region", Json.Rect(region)).Add("target", targetInfo);
            }
        }

        static JObj OcrCmd(CliArgs a)
        {
            Bitmap bmp;
            JObj source;
            if (a.Has("image")) { bmp = LoadImage(a.Get("image")); source = new JObj().Add("image", Path.GetFullPath(a.Get("image"))); }
            else
            {
                JObj targetInfo;
                var region = ResolveTarget(a, out targetInfo);
                bmp = Grab(region, false);
                source = new JObj().Add("region", Json.Rect(region)).Add("target", targetInfo);
            }
            using (bmp)
            {
                string text = null, error = null;
                var done = new ManualResetEvent(false);
                Ocr.Recognize(bmp, delegate(string t, string e) { text = t; error = e; done.Set(); });
                if (!done.WaitOne(90000)) throw new CliException("O OCR nao terminou em 90 s.", 1);
                if (error != null) throw new CliException(error, 1);
                int lines = text.Length == 0 ? 0 : text.Split('\n').Length;
                return new JObj().Add("text", text).Add("lines", lines).Add("chars", text.Length).Add("source", source);
            }
        }

        static JObj Annotate(CliArgs a)
        {
            string input = a.Get("in"), output = a.Get("out");
            if (input == null || output == null) throw new CliException("annotate precisa de --in e --out.");
            int maxWidth = a.GetInt("max-width", 0, 0, 20000);

            Bitmap work = LoadImage(input);
            Color color = Color.Red;
            float width = 4;
            int stepNo = 1, applied = 0;
            try
            {
                foreach (var op in a.Opts)
                {
                    string v = op.Value;
                    Annotation ann = null;
                    switch (op.Key)
                    {
                        case "in": case "out": case "max-width": case "format": break;
                        case "color":
                            try { color = ColorTranslator.FromHtml(v); } catch { throw new CliException("Cor invalida: " + v + " (use #RRGGBB)."); }
                            break;
                        case "width":
                            width = ParseInts(v, 1, "--width")[0];
                            if (width < 1 || width > 100) throw new CliException("--width deve estar entre 1 e 100.");
                            break;
                        case "rect": { var r = ParseRect(v, "--rect"); ann = new ShapeAnn { Kind = ShapeKind.Rectangle, A = r.Location, B = new Point(r.Right, r.Bottom) }; break; }
                        case "ellipse": { var r = ParseRect(v, "--ellipse"); ann = new ShapeAnn { Kind = ShapeKind.Ellipse, A = r.Location, B = new Point(r.Right, r.Bottom) }; break; }
                        case "arrow": { var p = ParseInts(v, 4, "--arrow"); ann = new ShapeAnn { Kind = ShapeKind.Arrow, A = new Point(p[0], p[1]), B = new Point(p[2], p[3]) }; break; }
                        case "line": { var p = ParseInts(v, 4, "--line"); ann = new ShapeAnn { Kind = ShapeKind.Line, A = new Point(p[0], p[1]), B = new Point(p[2], p[3]) }; break; }
                        case "text":
                            {
                                var parts = v.Split(new char[] { ',' }, 3);
                                int x, y;
                                if (parts.Length < 3 || !int.TryParse(parts[0].Trim(), out x) || !int.TryParse(parts[1].Trim(), out y)) throw new CliException("--text deve ser x,y,texto.");
                                ann = new TextAnn { Pos = new Point(x, y), Text = parts[2] };
                                break;
                            }
                        case "step":
                            {
                                var parts = v.Split(',');
                                int x, y, n = stepNo;
                                if (parts.Length < 2 || !int.TryParse(parts[0].Trim(), out x) || !int.TryParse(parts[1].Trim(), out y)) throw new CliException("--step deve ser x,y[,n].");
                                if (parts.Length > 2 && !int.TryParse(parts[2].Trim(), out n)) throw new CliException("--step: numero invalido.");
                                ann = new StepAnn { Center = new Point(x, y), Number = n };
                                stepNo = n + 1;
                                break;
                            }
                        case "blur": ann = new BlurAnn { Rect = ParseRect(v, "--blur"), Pixelate = false }; break;
                        case "pixelate": ann = new BlurAnn { Rect = ParseRect(v, "--pixelate"), Pixelate = true }; break;
                        case "crop":
                            {
                                var r = ParseRect(v, "--crop");
                                r.Intersect(new Rectangle(0, 0, work.Width, work.Height));
                                if (r.Width < 1 || r.Height < 1) throw new CliException("--crop fora da imagem (" + work.Width + "x" + work.Height + ").", 1);
                                var cropped = work.Clone(r, PixelFormat.Format32bppArgb);
                                work.Dispose(); work = cropped; applied++;
                                break;
                            }
                        default: throw new CliException("Opcao desconhecida em annotate: --" + op.Key);
                    }
                    if (ann == null) continue;
                    ann.Color = color; ann.Width = width;
                    using (var g = Graphics.FromImage(work))
                    {
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        ann.Draw(g, work);
                    }
                    applied++;
                }

                using (var final = Downscale(work, maxWidth))
                {
                    string file = Path.GetFullPath(output);
                    SaveImage(final, file);
                    return new JObj().Add("file", file).Add("operations", applied).Add("width", final.Width).Add("height", final.Height)
                        .Add("bytes", new FileInfo(file).Length);
                }
            }
            finally { work.Dispose(); }
        }

        // ---------------------------------------------------------------- alvos e imagens

        // Converte o alvo escolhido em um retangulo de tela (ja cortado pela tela virtual).
        static Rectangle ResolveTarget(CliArgs a, out JObj info)
        {
            int count = 0;
            foreach (var k in new[] { "fullscreen", "screen", "region", "window", "active" }) if (a.Has(k)) count++;
            if (count != 1) throw new CliException("Informe exatamente um alvo: --fullscreen, --screen N, --region x,y,w,h, --window \"titulo\" ou --active.");

            var vs = SystemInformation.VirtualScreen;
            Rectangle r;
            if (a.Has("fullscreen")) { r = vs; info = new JObj().Add("type", "fullscreen"); }
            else if (a.Has("screen"))
            {
                int n = a.GetInt("screen", 0, 0, 63);
                var all = Screen.AllScreens;
                if (n >= all.Length) throw new CliException("--screen " + n + " nao existe (ha " + all.Length + " monitores).", 1);
                r = all[n].Bounds; info = new JObj().Add("type", "screen").Add("index", n);
            }
            else if (a.Has("region")) { r = ParseRect(a.Get("region"), "--region"); info = new JObj().Add("type", "region"); }
            else
            {
                WindowInfo w;
                if (a.Has("active"))
                {
                    var h = Native.GetForegroundWindow();
                    w = null;
                    foreach (var c in Native.ListWindows(true)) if (c.Handle == h) { w = c; break; }
                    if (w == null) throw new CliException("Nao ha janela ativa visivel.", 1);
                }
                else
                {
                    string q = a.Get("window");
                    w = null;
                    foreach (var c in Native.ListWindows(false)) if (Matches(c, q)) { w = c; break; }
                    if (w == null) throw new CliException("Nenhuma janela visivel combina com \"" + q + "\". Use o comando 'windows'.", 1);
                }
                if (!a.Has("no-activate"))
                {
                    Native.Activate(w.Handle);
                    Thread.Sleep(450);   // da tempo de a janela ir para frente e se redesenhar
                    Rectangle fresh;
                    if (Native.GetWindowBounds(w.Handle, out fresh)) w.Bounds = fresh;
                }
                r = w.Bounds;
                info = new JObj().Add("type", "window").Add("title", w.Title).Add("process", w.Process).Add("handle", w.Handle.ToInt64());
            }

            r.Intersect(vs);
            if (r.Width < 1 || r.Height < 1) throw new CliException("A area fica fora da tela virtual (" + vs.X + "," + vs.Y + "," + vs.Width + "," + vs.Height + ").", 1);
            return r;
        }

        static Bitmap Grab(Rectangle r, bool withCursor)
        {
            var bmp = new Bitmap(r.Width, r.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(r.Location, Point.Empty, r.Size, CopyPixelOperation.SourceCopy);
                if (withCursor) RecorderForm.DrawPointer(g, Cursor.Position.X - r.X, Cursor.Position.Y - r.Y);
            }
            return bmp;
        }

        static Bitmap Downscale(Bitmap src, int maxWidth)
        {
            if (maxWidth <= 0 || src.Width <= maxWidth) return (Bitmap)src.Clone();
            int h = Math.Max(1, (int)Math.Round((double)src.Height * maxWidth / src.Width));
            var bmp = new Bitmap(maxWidth, h, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(src, new Rectangle(0, 0, maxWidth, h), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel);
            }
            return bmp;
        }

        static Bitmap LoadImage(string path)
        {
            if (path == null) throw new CliException("Falta --image.");
            if (!File.Exists(path)) throw new CliException("Arquivo nao encontrado: " + path, 1);
            // carrega em memoria para nao manter o arquivo bloqueado
            using (var img = Image.FromFile(path))
            {
                var bmp = new Bitmap(img.Width, img.Height, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(bmp)) g.DrawImage(img, 0, 0, img.Width, img.Height);
                return bmp;
            }
        }

        static string OutputDir()
        {
            return Path.Combine(Path.GetTempPath(), "CapturaTela");
        }

        static string ExtFor(CliArgs a)
        {
            string f = a.Get("format");
            if (f == null) { string o = a.Get("out"); return o != null && Path.HasExtension(o) ? Path.GetExtension(o).ToLowerInvariant() : ".png"; }
            f = f.ToLowerInvariant();
            if (f == "png") return ".png";
            if (f == "jpg" || f == "jpeg") return ".jpg";
            throw new CliException("--format deve ser png ou jpg.");
        }

        static string OutPath(CliArgs a, string prefix, string ext)
        {
            string o = a.Get("out");
            string file;
            if (o != null) file = Path.GetFullPath(o);
            else
            {
                Directory.CreateDirectory(OutputDir());
                file = Path.Combine(OutputDir(), prefix + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ext);
            }
            string dir = Path.GetDirectoryName(file);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            return file;
        }

        static void SaveImage(Bitmap bmp, string file)
        {
            string ext = Path.GetExtension(file).ToLowerInvariant();
            if (ext == ".jpg" || ext == ".jpeg")
            {
                using (var bg = new Bitmap(bmp.Width, bmp.Height, PixelFormat.Format24bppRgb))
                {
                    using (var g = Graphics.FromImage(bg)) { g.Clear(Color.White); g.DrawImageUnscaled(bmp, 0, 0); }
                    bg.Save(file, ImageFormat.Jpeg);
                }
            }
            else if (ext == ".bmp") bmp.Save(file, ImageFormat.Bmp);
            else bmp.Save(file, ImageFormat.Png);
        }

        // ---------------------------------------------------------------- parse

        static int[] ParseInts(string s, int count, string name)
        {
            if (s == null) throw new CliException("Falta " + name + ".");
            var parts = s.Split(',');
            if (parts.Length != count) throw new CliException(name + " deve ter " + count + " numeros separados por virgula: " + s);
            var r = new int[count];
            for (int i = 0; i < count; i++)
                if (!int.TryParse(parts[i].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out r[i]))
                    throw new CliException(name + ": numero invalido \"" + parts[i] + "\".");
            return r;
        }

        static Rectangle ParseRect(string s, string name)
        {
            var p = ParseInts(s, 4, name);
            if (p[2] <= 0 || p[3] <= 0) throw new CliException(name + ": largura e altura devem ser positivas.");
            return new Rectangle(p[0], p[1], p[2], p[3]);
        }
    }
}
