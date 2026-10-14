using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CapturaTela
{
    // Leitor JSON minimo (RFC 8259) para o protocolo MCP.
    // Objetos viram Dictionary<string, object>, arrays viram List<object>, numeros inteiros viram long,
    // os demais numeros double; alem de string, bool e null.
    class JsonParser
    {
        readonly string s;
        int i;

        JsonParser(string text) { s = text; }

        public static object Parse(string text)
        {
            var p = new JsonParser(text);
            p.Skip();
            object v = p.Value();
            p.Skip();
            if (p.i != p.s.Length) throw p.Fail("texto depois do JSON");
            return v;
        }

        Exception Fail(string msg) { return new FormatException("JSON invalido na posicao " + i + ": " + msg); }

        void Skip() { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

        object Value()
        {
            if (i >= s.Length) throw Fail("fim inesperado");
            char c = s[i];
            if (c == '{') return Obj();
            if (c == '[') return Arr();
            if (c == '"') return Str();
            if (c == 't') { Expect("true"); return true; }
            if (c == 'f') { Expect("false"); return false; }
            if (c == 'n') { Expect("null"); return null; }
            return Num();
        }

        void Expect(string word)
        {
            if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0) throw Fail("esperava " + word);
            i += word.Length;
        }

        Dictionary<string, object> Obj()
        {
            var d = new Dictionary<string, object>();
            i++; Skip();
            if (i < s.Length && s[i] == '}') { i++; return d; }
            while (true)
            {
                Skip();
                if (i >= s.Length || s[i] != '"') throw Fail("esperava o nome de uma chave");
                string k = Str();
                Skip();
                if (i >= s.Length || s[i] != ':') throw Fail("esperava ':'");
                i++; Skip();
                d[k] = Value();
                Skip();
                if (i >= s.Length) throw Fail("objeto nao fechado");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; return d; }
                throw Fail("esperava ',' ou '}'");
            }
        }

        List<object> Arr()
        {
            var l = new List<object>();
            i++; Skip();
            if (i < s.Length && s[i] == ']') { i++; return l; }
            while (true)
            {
                Skip();
                l.Add(Value());
                Skip();
                if (i >= s.Length) throw Fail("array nao fechado");
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; return l; }
                throw Fail("esperava ',' ou ']'");
            }
        }

        string Str()
        {
            var sb = new StringBuilder();
            i++; // aspas de abertura
            while (true)
            {
                if (i >= s.Length) throw Fail("texto nao fechado");
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) throw Fail("escape incompleto");
                char e = s[i++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        int code;
                        if (i + 4 > s.Length || !int.TryParse(s.Substring(i, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out code))
                            throw Fail("escape \\u invalido");
                        sb.Append((char)code); i += 4;
                        break;
                    default: throw Fail("escape desconhecido \\" + e);
                }
            }
        }

        object Num()
        {
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            string t = s.Substring(start, i - start);
            if (t.Length == 0) throw Fail("valor inesperado '" + s[start] + "'");
            long l;
            if (long.TryParse(t, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out l)) return l;
            double d;
            if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) return d;
            throw Fail("numero invalido " + t);
        }
    }
}
