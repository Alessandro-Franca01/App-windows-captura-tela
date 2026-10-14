using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace CapturaTela
{
    class HelpTopic
    {
        public string Title;
        public string Body;     // texto puro
        public string Markup;   // o mesmo texto com **negrito** (a janela mostra o negrito de verdade)
        public override string ToString() { return Title; }
    }

    // Le o guia do usuario (docs\GUIA.md, embutido no .exe como recurso "guia.md"): cada "## titulo" vira um topico.
    static class HelpContent
    {
        public static List<HelpTopic> Load()
        {
            string text = null;
            try
            {
                using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("guia.md"))
                    if (s != null) using (var r = new StreamReader(s, Encoding.UTF8)) text = r.ReadToEnd();
            }
            catch { }
            return Parse(text);
        }

        public static List<HelpTopic> Parse(string markdown)
        {
            var topics = new List<HelpTopic>();
            if (string.IsNullOrEmpty(markdown))
            {
                string msg = "O guia não foi encontrado neste executável. Consulte o arquivo docs\\GUIA.md do projeto.";
                topics.Add(new HelpTopic { Title = "Ajuda", Body = msg, Markup = msg });
                return topics;
            }

            HelpTopic current = null;
            var plain = new StringBuilder();
            var markup = new StringBuilder();
            foreach (var raw in markdown.Replace("\r\n", "\n").Split('\n'))
            {
                if (raw.StartsWith("## "))
                {
                    Close(current, plain, markup);
                    current = new HelpTopic { Title = raw.Substring(3).Trim() };
                    topics.Add(current);
                    plain.Length = 0; markup.Length = 0;
                    continue;
                }
                if (current == null) continue;            // titulo e introducao do arquivo ficam de fora
                string line = raw.Replace("`", "");
                if (line.StartsWith("- ")) line = "• " + line.Substring(2);
                markup.Append(line).Append("\r\n");
                plain.Append(line.Replace("**", "")).Append("\r\n");
            }
            Close(current, plain, markup);
            return topics;
        }

        static void Close(HelpTopic topic, StringBuilder plain, StringBuilder markup)
        {
            if (topic == null) return;
            topic.Body = plain.ToString().Trim();
            topic.Markup = markup.ToString().Trim();
        }
    }

    // Janela de ajuda: lista de assuntos a esquerda, texto formatado a direita.
    class HelpForm : Form
    {
        public readonly ListBox TopicList = new ListBox();
        public readonly RichTextBox Reader = new RichTextBox();
        readonly Label title = new Label();

        public HelpForm(int firstTopic)
        {
            Text = "Ajuda - Ferramenta de Captura";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(900, 580);
            MinimumSize = new Size(680, 420);
            Font = new Font("Segoe UI", 9.5f);
            ShowInTaskbar = true;
            Icon = AppIcon.Get(SystemInformation.SmallIconSize);

            TopicList.Dock = DockStyle.Left; TopicList.Width = 236; TopicList.IntegralHeight = false;
            TopicList.BorderStyle = BorderStyle.None; TopicList.ItemHeight = 36;
            TopicList.DrawMode = DrawMode.OwnerDrawFixed;
            TopicList.DrawItem += DrawTopic;

            title.Dock = DockStyle.Top; title.Height = 64; title.Padding = new Padding(28, 22, 0, 0);
            title.Font = new Font("Segoe UI Semibold", 17f);

            Reader.Dock = DockStyle.Fill; Reader.ReadOnly = true; Reader.DetectUrls = false;
            Reader.ScrollBars = RichTextBoxScrollBars.Vertical; Reader.BorderStyle = BorderStyle.None;
            Reader.Font = new Font("Segoe UI", 10.5f);
            Reader.ShortcutsEnabled = true;

            var padding = new Panel { Dock = DockStyle.Fill, Padding = new Padding(28, 0, 20, 16) };
            padding.Controls.Add(Reader);

            foreach (var t in HelpContent.Load()) TopicList.Items.Add(t);
            TopicList.SelectedIndexChanged += delegate { ShowSelected(); };

            Controls.Add(padding);
            Controls.Add(title);
            Controls.Add(TopicList);
            KeyPreview = true;
            KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) Close(); };

            Theme.ScaleForDpi(this);
            Theme.Apply(this);
            Reader.BackColor = Theme.Surface;
            TopicList.BackColor = Theme.IsDark ? Theme.DarkPanel : Color.FromArgb(0xF3, 0xF3, 0xF3);
            title.ForeColor = Theme.Ink;
            TopicList.SelectedIndex = Math.Max(0, Math.Min(firstTopic, TopicList.Items.Count - 1));
        }

        void ShowSelected()
        {
            var t = TopicList.SelectedItem as HelpTopic;
            if (t == null) return;
            title.Text = t.Title;
            Render(t.Markup ?? t.Body);
        }

        // Escreve o texto no leitor: negrito de verdade e listas com recuo.
        void Render(string markup)
        {
            Reader.SuspendLayout();
            Reader.Clear();
            Reader.BackColor = Theme.Surface;
            using (var normal = new Font("Segoe UI", 10.5f))
            using (var bold = new Font("Segoe UI Semibold", 10.5f))
            {
                bool first = true;
                foreach (string line in markup.Replace("\r\n", "\n").Split('\n'))
                {
                    if (!first) Append("\n", normal, Theme.Ink);
                    first = false;
                    int start = Reader.TextLength;
                    var parts = line.Split(new string[] { "**" }, StringSplitOptions.None);
                    for (int i = 0; i < parts.Length; i++)
                        if (parts[i].Length > 0) Append(parts[i], i % 2 == 1 ? bold : normal, Theme.Ink);
                    // listas: recuo com "hanging indent" (as linhas seguintes alinham com o texto, nao com o marcador)
                    bool list = line.StartsWith("• ") || Regex.IsMatch(line, @"^\d+\. ");
                    Reader.Select(start, Reader.TextLength - start);
                    Reader.SelectionIndent = list ? 20 : 0;
                    Reader.SelectionHangingIndent = list ? 16 : 0;
                    Reader.SelectionLength = 0;
                }
            }
            Reader.SelectionStart = 0; Reader.SelectionLength = 0;
            Reader.ResumeLayout();
        }

        void Append(string text, Font font, Color color)
        {
            Reader.SelectionStart = Reader.TextLength;
            Reader.SelectionLength = 0;
            Reader.SelectionFont = font;
            Reader.SelectionColor = color;
            Reader.SelectedText = text;
        }

        void DrawTopic(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            bool selected = (e.State & DrawItemState.Selected) != 0;
            using (var b = new SolidBrush(TopicList.BackColor)) g.FillRectangle(b, e.Bounds);
            if (selected)
            {
                var r = new RectangleF(e.Bounds.X + 10, e.Bounds.Y + 2, e.Bounds.Width - 20, e.Bounds.Height - 4);
                using (var path = AppRenderer.RoundPath(r, 6f))
                using (var b = new SolidBrush(Theme.AccentStrong)) g.FillPath(b, path);
            }
            Color fore = selected ? Color.White : Theme.Ink;
            using (var f = new Font("Segoe UI", 10f, selected ? FontStyle.Bold : FontStyle.Regular))
                TextRenderer.DrawText(g, TopicList.Items[e.Index].ToString(), f, new Rectangle(e.Bounds.X + 24, e.Bounds.Y, e.Bounds.Width - 34, e.Bounds.Height), fore,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.Left | TextFormatFlags.NoPadding);
        }
    }
}
