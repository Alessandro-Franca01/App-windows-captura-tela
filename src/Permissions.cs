using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CapturaTela
{
    // ---------------------------------------------------------------------------------
    // Permissoes que o usuario concede a uma IA (via MCP, e opcionalmente via CLI).
    //
    // Chaves (comparacao sem diferenciar maiusculas; um '*' no fim vale como prefixo):
    //   info                       ver monitores e posicao do cursor
    //   windows                    listar janelas abertas (titulos e programas)
    //   target:screen              capturar tela inteira, monitor ou regiao
    //   target:window:<programa>   capturar janelas de um programa (ex.: target:window:code)
    //   action:record              gravar GIF e capturar com rolagem
    //   action:ocr                 ler texto de imagens (OCR)
    //   file:read:<caminho>        ler um arquivo (ou pasta, com \*)
    //   file:write:<caminho>       gravar um arquivo (ou pasta, com \*) fora da pasta de saida
    // ---------------------------------------------------------------------------------

    class PermissionStore
    {
        public const string EnvPath = "CAPTURATELA_PERMISSIONS_FILE";

        public bool Enabled = true;     // chave geral: desligada, nada e permitido
        public bool GateCli;            // exigir permissao tambem na linha de comando
        public readonly List<KeyValuePair<string, bool>> Entries = new List<KeyValuePair<string, bool>>();

        string path;

        public static string DefaultPath()
        {
            string env = Environment.GetEnvironmentVariable(EnvPath);
            return !string.IsNullOrEmpty(env) ? env : AppSettings.DataFile("CapturaTela.permissoes.ini");
        }

        public static PermissionStore Load()
        {
            var s = new PermissionStore { path = DefaultPath() };
            try
            {
                if (!File.Exists(s.path)) return s;
                foreach (var raw in File.ReadAllLines(s.path, Encoding.UTF8))
                {
                    int i = raw.IndexOf('=');
                    if (i <= 0) continue;
                    string k = raw.Substring(0, i).Trim(), v = raw.Substring(i + 1).Trim();
                    bool b;
                    switch (k)
                    {
                        case "enabled": if (bool.TryParse(v, out b)) s.Enabled = b; break;
                        case "gateCli": if (bool.TryParse(v, out b)) s.GateCli = b; break;
                        case "allow": if (v.Length > 0) s.Entries.Add(new KeyValuePair<string, bool>(v, true)); break;
                        case "deny": if (v.Length > 0) s.Entries.Add(new KeyValuePair<string, bool>(v, false)); break;
                    }
                }
            }
            catch { /* arquivo ilegivel: comeca vazio (nada permitido) */ }
            return s;
        }

        public void Save()
        {
            var sb = new StringBuilder();
            sb.AppendLine("enabled=" + Enabled);
            sb.AppendLine("gateCli=" + GateCli);
            foreach (var e in Entries) sb.AppendLine((e.Value ? "allow=" : "deny=") + e.Key);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }

        public static bool Matches(string pattern, string key)
        {
            if (pattern.EndsWith("*"))
                return key.StartsWith(pattern.Substring(0, pattern.Length - 1), StringComparison.OrdinalIgnoreCase);
            return string.Equals(pattern, key, StringComparison.OrdinalIgnoreCase);
        }

        // true = permitido, false = negado, null = o usuario ainda nao decidiu. Uma negacao vence uma permissao.
        public bool? Lookup(string key)
        {
            bool allowed = false;
            foreach (var e in Entries)
            {
                if (!Matches(e.Key, key)) continue;
                if (!e.Value) return false;
                allowed = true;
            }
            return allowed ? (bool?)true : null;
        }

        public void Set(string key, bool allow)
        {
            Remove(key);
            Entries.Add(new KeyValuePair<string, bool>(key, allow));
        }

        public void Remove(string key)
        {
            Entries.RemoveAll(delegate(KeyValuePair<string, bool> e) { return string.Equals(e.Key, key, StringComparison.OrdinalIgnoreCase); });
        }

        // Texto em portugues para mostrar ao usuario.
        public static string Describe(string key)
        {
            string k = key.ToLowerInvariant();
            if (k == "info") return "Ver informacoes dos monitores e a posicao do cursor";
            if (k == "windows") return "Listar as janelas abertas (titulos e programas)";
            if (k == "target:screen") return "Capturar a tela inteira, um monitor ou uma regiao";
            if (k == "target:window:*") return "Capturar janelas de qualquer programa";
            if (k.StartsWith("target:window:")) return "Capturar janelas do programa \"" + key.Substring("target:window:".Length) + "\"";
            if (k == "action:record") return "Gravar GIF e capturar com rolagem";
            if (k == "action:ocr") return "Ler texto de imagens (OCR)";
            if (k.StartsWith("file:read:")) return "Ler " + Target(key.Substring("file:read:".Length));
            if (k.StartsWith("file:write:")) return "Gravar em " + Target(key.Substring("file:write:".Length));
            return key;
        }

        static string Target(string path)
        {
            return path.EndsWith("*") ? "arquivos da pasta " + path.Substring(0, path.Length - 1) : "o arquivo " + path;
        }
    }

    // ---------------------------------------------------------------------------------

    interface IPermissionGate
    {
        // Lanca CliException (codigo 3) se negado. 'alwaysKey' e o que fica salvo se o usuario escolher "sempre".
        void Require(string key, string alwaysKey, string description);
    }

    class AllowAllGate : IPermissionGate
    {
        public void Require(string key, string alwaysKey, string description) { }
    }

    enum PromptAnswer { Deny, DenyAlways, Once, Always }

    class PromptRequest
    {
        public string Client, Key, AlwaysKey, Description;
    }

    // Consulta o arquivo de permissoes e, se o usuario ainda nao decidiu, pergunta.
    class PromptGate : IPermissionGate
    {
        readonly Func<PromptRequest, PromptAnswer> ask;
        readonly HashSet<string> once = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public string Client = "Um aplicativo de IA";

        public PromptGate(Func<PromptRequest, PromptAnswer> ask) { this.ask = ask; }

        // "Permitir uma vez" vale ate o fim da chamada em andamento; cada chamada nova recomeca.
        public void BeginCall() { once.Clear(); }

        public void Require(string key, string alwaysKey, string description)
        {
            var store = PermissionStore.Load();
            if (!store.Enabled) throw Denied("o acesso por IA esta desativado nas configuracoes", description);
            if (once.Contains(key)) return;

            bool? known = store.Lookup(key);
            if (known == false) throw Denied("voce negou essa permissao", description);
            if (known == true) return;

            var answer = ask(new PromptRequest { Client = Client, Key = key, AlwaysKey = alwaysKey, Description = description });
            switch (answer)
            {
                case PromptAnswer.Once: once.Add(key); return;
                case PromptAnswer.Always:
                    store = PermissionStore.Load();
                    store.Set(alwaysKey, true); store.Save();
                    return;
                case PromptAnswer.DenyAlways:
                    store = PermissionStore.Load();
                    store.Set(alwaysKey, false); store.Save();
                    throw Denied("voce negou essa permissao", description);
                default:
                    throw Denied("voce negou (ou nao respondeu a tempo)", description);
            }
        }

        static CliException Denied(string why, string what)
        {
            return new CliException("Permissao negada: " + what + " (" + why + ").", 3);
        }
    }
}
