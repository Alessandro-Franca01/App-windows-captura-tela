using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CapturaTela
{
    // Objeto JSON que preserva a ordem das chaves.
    class JObj
    {
        readonly List<KeyValuePair<string, object>> items = new List<KeyValuePair<string, object>>();

        public JObj Add(string key, object value)
        {
            items.Add(new KeyValuePair<string, object>(key, value));
            return this;
        }

        public override string ToString()
        {
            var sb = new StringBuilder("{");
            for (int i = 0; i < items.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(Json.Quote(items[i].Key)).Append(':').Append(Json.Value(items[i].Value));
            }
            return sb.Append('}').ToString();
        }
    }

    class JArr : List<object>
    {
        public override string ToString()
        {
            var sb = new StringBuilder("[");
            for (int i = 0; i < Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(Json.Value(this[i]));
            }
            return sb.Append(']').ToString();
        }
    }

    // Serializador JSON minimo. A saida e sempre ASCII (nao ASCII vira \uXXXX), entao nao
    // depende da codificacao do console.
    static class Json
    {
        public static string Quote(string s)
        {
            if (s == null) return "null";
            var sb = new StringBuilder("\"");
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20 || c > 0x7E) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }

        public static string Value(object v)
        {
            if (v == null) return "null";
            if (v is string) return Quote((string)v);
            if (v is bool) return (bool)v ? "true" : "false";
            if (v is int || v is long) return Convert.ToString(v, CultureInfo.InvariantCulture);
            if (v is double) return ((double)v).ToString("0.###", CultureInfo.InvariantCulture);
            if (v is JObj || v is JArr) return v.ToString();
            return Quote(v.ToString());
        }

        public static JObj Rect(System.Drawing.Rectangle r)
        {
            return new JObj().Add("x", r.X).Add("y", r.Y).Add("width", r.Width).Add("height", r.Height);
        }
    }
}
