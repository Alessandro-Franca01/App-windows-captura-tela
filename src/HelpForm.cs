using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace CapturaTela
{
    class HelpTopic
    {
        public string Title, Body;
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
                topics.Add(new HelpTopic { Title = "Ajuda", Body = "O guia não foi encontrado neste executável. Consulte o arquivo docs\\GUIA.md do projeto." });
                return topics;
            }

            HelpTopic current = null;
            var body = new StringBuilder();
            foreach (var raw in markdown.Replace("\r\n", "\n").Split('\n'))
            {
                if (raw.StartsWith("## "))
                {
                    Close(current, body);
                    current = new HelpTopic { Title = raw.Substring(3).Trim() };
                    topics.Add(current);
                    body.Length = 0;
                    continue;
                }
                if (current == null) continue;            // titulo e introducao do arquivo ficam de fora
                string line = raw.Replace("**", "").Replace("`", "");
                if (line.StartsWith("- ")) line = "• " + line.Substring(2);
                body.Append(line).Append("\r\n");
            }
            Close(current, body);
            return topics;
        }

        static void Close(HelpTopic topic, StringBuilder body)
        {
            if (topic != null) topic.Body = body.ToString().Trim();
        }
    }

    // Janela de ajuda: lista de assuntos a esquerda, texto a direita.
    class HelpForm : Form
    {
        public readonly ListBox TopicList = new ListBox();
        public readonly TextBox Reader = new TextBox();
        readonly Label title = new Label();

        public HelpForm(int firstTopic)
        {
            Text = "Ajuda - Ferramenta de Captura";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(860, 540);
            MinimumSize = new Size(640, 400);
            Font = new Font("Segoe UI", 9.5f);
            ShowInTaskbar = true;
            Icon = AppIcon.Get(SystemInformation.SmallIconSize);

            TopicList.Dock = DockStyle.Left; TopicList.Width = 210; TopicList.IntegralHeight = false;
            TopicList.BorderStyle = BorderStyle.None; TopicList.ItemHeight = 28;
            TopicList.DrawMode = DrawMode.OwnerDrawFixed;
            TopicList.DrawItem += DrawTopic;

            title.Dock = DockStyle.Top; title.Height = 46; title.Padding = new Padding(16, 12, 0, 0);
            title.Font = new Font("Segoe UI Semibold", 15f);

            Reader.Dock = DockStyle.Fill; Reader.Multiline = true; Reader.ReadOnly = true;
            Reader.ScrollBars = ScrollBars.Vertical; Reader.BorderStyle = BorderStyle.None;
            Reader.Font = new Font("Segoe UI", 10.5f);
            Reader.Margin = new Padding(16);

            var padding = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 0, 16, 12) };
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
            Reader.BackColor = Theme.IsDark ? Theme.DarkBack : SystemColors.Window;
            TopicList.BackColor = Theme.IsDark ? Theme.DarkPanel : Color.FromArgb(243, 243, 243);
            title.ForeColor = Theme.IsDark ? Theme.DarkText : SystemColors.ControlText;
            TopicList.SelectedIndex = Math.Max(0, Math.Min(firstTopic, TopicList.Items.Count - 1));
        }

        void ShowSelected()
        {
            var t = TopicList.SelectedItem as HelpTopic;
            if (t == null) return;
            title.Text = t.Title;
            Reader.Text = t.Body;
            Reader.SelectionStart = 0; Reader.SelectionLength = 0;
        }

        void DrawTopic(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            bool selected = (e.State & DrawItemState.Selected) != 0;
            Color back = selected ? Theme.Accent : TopicList.BackColor;
            Color fore = selected ? Color.White : (Theme.IsDark ? Theme.DarkText : SystemColors.ControlText);
            using (var b = new SolidBrush(back)) e.Graphics.FillRectangle(b, e.Bounds);
            var r = new Rectangle(e.Bounds.X + 14, e.Bounds.Y, e.Bounds.Width - 14, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, TopicList.Items[e.Index].ToString(), TopicList.Font, r, fore,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.Left);
        }
    }
}
