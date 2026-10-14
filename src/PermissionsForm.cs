using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace CapturaTela
{
    // Onde o usuario ve, concede e revoga o que as IAs podem fazer.
    class PermissionsForm : Form
    {
        readonly PermissionStore store = PermissionStore.Load();
        readonly CheckBox enabled = new CheckBox();
        readonly CheckBox gateCli = new CheckBox();
        readonly ListView list = new ListView();
        readonly Label emptyNote = new Label();
        readonly FlatButton revoke = new FlatButton();

        public PermissionsForm()
        {
            Text = "Permissões da IA";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(720, 520);
            Font = new Font("Segoe UI", 9.5f);

            // ---- chaves gerais ----
            enabled.Text = "Permitir que programas de IA (servidor MCP) usem a Ferramenta de Captura";
            enabled.Left = 20; enabled.Top = 16; enabled.Width = 680; enabled.Height = 24; enabled.Checked = store.Enabled;
            Controls.Add(enabled);
            Controls.Add(new Label { Text = "Desligado, nenhum pedido é aceito, nem os já salvos.", Left = 42, Top = 40, Width = 640, Height = 20, ForeColor = Theme.Muted, Tag = "muted" });
            gateCli.Text = "Exigir permissão também na linha de comando (CapturaTelaCli.exe)";
            gateCli.Left = 20; gateCli.Top = 68; gateCli.Width = 680; gateCli.Height = 24; gateCli.Checked = store.GateCli;
            Controls.Add(gateCli);
            Controls.Add(new Label { Text = "Ligado, quem usa o terminal passa pelas mesmas regras e pela mesma janela de pedido.", Left = 42, Top = 92, Width = 640, Height = 20, ForeColor = Theme.Muted, Tag = "muted" });

            Controls.Add(new Label { Text = "Permissões salvas", Left = 20, Top = 130, Width = 400, Height = 24, Font = new Font("Segoe UI Semibold", 10.5f) });

            // ---- lista ----
            list.Left = 20; list.Top = 160; list.Width = 680; list.Height = 232;
            list.View = View.Details; list.FullRowSelect = true; list.MultiSelect = true; list.HideSelection = false;
            list.BorderStyle = BorderStyle.FixedSingle;
            list.OwnerDraw = true; list.ShowItemToolTips = true;
            list.SmallImageList = new ImageList { ImageSize = new Size(1, 34) };   // altura das linhas
            list.Columns.Add("Situação", 118);
            list.Columns.Add("O que a IA pode (ou não) fazer", 440);
            list.Columns.Add("Risco", 100);
            list.DrawColumnHeader += DrawHeader;
            list.SizeChanged += delegate { FitColumns(); };
            list.DrawItem += delegate(object s, DrawListViewItemEventArgs e) { e.DrawDefault = false; };
            list.DrawSubItem += DrawCell;
            list.SelectedIndexChanged += delegate { revoke.Enabled = list.SelectedItems.Count > 0; };
            Controls.Add(list);

            emptyNote.Text = "Nenhuma permissão salva ainda.\nQuando uma IA pedir algo, você escolhe: uma vez, sempre ou negar.";
            emptyNote.TextAlign = ContentAlignment.MiddleCenter;
            emptyNote.ForeColor = Theme.Muted; emptyNote.Tag = "muted";
            emptyNote.Left = 24; emptyNote.Top = 204; emptyNote.Width = 672; emptyNote.Height = 120;
            Controls.Add(emptyNote); emptyNote.BringToFront();

            // ---- acoes ----
            revoke.Text = "Revogar selecionadas"; revoke.Left = 20; revoke.Top = 402; revoke.Width = 180; revoke.Height = 32; revoke.Enabled = false;
            revoke.Click += delegate
            {
                foreach (ListViewItem it in list.SelectedItems) store.Remove((string)it.Tag);
                Fill();
            };
            var revokeAll = new FlatButton { Text = "Revogar todas", Left = 208, Top = 402, Width = 140, Height = 32 };
            revokeAll.Click += delegate
            {
                if (store.Entries.Count == 0) return;
                if (MessageBox.Show(this, "Remover todas as permissões salvas? A IA voltará a pedir cada uma.", Text,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes) { store.Entries.Clear(); Fill(); }
            };
            var add = new FlatButton { Text = "Adicionar…", Left = 356, Top = 402, Width = 130, Height = 32 };
            add.Click += delegate
            {
                using (var f = new AddPermissionForm())
                    if (f.ShowDialog(this) == DialogResult.OK) { store.Set(f.Key, f.Allow); Fill(); }
            };
            Controls.AddRange(new Control[] { revoke, revokeAll, add });

            Controls.Add(new Label
            {
                Text = "O MCP e a CLI rodam com o seu usuário. Este controle protege contra pedidos indevidos da IA, não contra um programa malicioso.",
                Left = 20, Top = 452, Width = 460, Height = 52, ForeColor = Theme.Muted, Tag = "muted"
            });

            var ok = new FlatButton { Text = "OK", Left = 506, Top = 470, Width = 90, Height = 34, Variant = ButtonVariant.Primary, DialogResult = DialogResult.OK };
            var cancel = new FlatButton { Text = "Cancelar", Left = 604, Top = 470, Width = 96, Height = 34, DialogResult = DialogResult.Cancel };
            ok.Click += delegate
            {
                store.Enabled = enabled.Checked; store.GateCli = gateCli.Checked;
                try { store.Save(); }
                catch (Exception ex) { MessageBox.Show(this, "Não foi possível salvar: " + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); }
            };
            Controls.AddRange(new Control[] { ok, cancel });
            AcceptButton = ok; CancelButton = cancel;
            Fill();
            Theme.ScaleForDpi(this);
            Theme.Apply(this);
            foreach (Control c in Controls) { var l = c as Label; if (l != null && l.Tag as string == "muted") l.ForeColor = Theme.Muted; }
        }

        // as colunas somam exatamente a largura da lista (as larguras de coluna nao escalam com o DPI sozinhas)
        void FitColumns()
        {
            float sc;
            using (var g = list.CreateGraphics()) sc = g.DpiX / 96f;
            int total = list.ClientSize.Width;
            if (total <= 0 || list.Columns.Count < 3) return;
            list.Columns[0].Width = (int)(118 * sc);
            list.Columns[2].Width = (int)(100 * sc);
            list.Columns[1].Width = Math.Max(100, total - list.Columns[0].Width - list.Columns[2].Width);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            FitColumns();
        }

        void Fill()
        {
            list.Items.Clear();
            foreach (var e in store.Entries)
            {
                var it = new ListViewItem(e.Value ? "✓ Permitido" : "✕ Negado");
                it.SubItems.Add(PermissionStore.Describe(e.Key));
                it.SubItems.Add(PermissionRisk.Label(PermissionRisk.Of(e.Key)).Replace("Risco ", ""));
                it.Tag = e.Key;
                it.ToolTipText = "Chave técnica: " + e.Key;
                list.Items.Add(it);
            }
            emptyNote.Visible = list.Items.Count == 0;
            revoke.Enabled = false;
        }

        // ---- desenho da lista (cabecalho e celulas seguem o tema) ----

        void DrawHeader(object sender, DrawListViewColumnHeaderEventArgs e)
        {
            var g = e.Graphics;
            using (var b = new SolidBrush(Theme.BarBack)) g.FillRectangle(b, e.Bounds);
            using (var p = new Pen(Theme.BarBorder)) g.DrawLine(p, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
            using (var f = new Font("Segoe UI Semibold", 9f))
                TextRenderer.DrawText(g, e.Header.Text, f, new Rectangle(e.Bounds.X + 12, e.Bounds.Y, e.Bounds.Width - 12, e.Bounds.Height), Theme.Muted,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        }

        void DrawCell(object sender, DrawListViewSubItemEventArgs e)
        {
            var g = e.Graphics;
            bool selected = e.Item.Selected;
            Color back = selected ? Theme.AccentStrong : Theme.SurfaceRaised;
            using (var b = new SolidBrush(back)) g.FillRectangle(b, e.Bounds);
            using (var p = new Pen(Theme.BarBorder)) g.DrawLine(p, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);

            bool allowed = e.Item.Text.StartsWith("✓");
            Color ink = selected ? Color.White : Theme.Ink;
            if (e.ColumnIndex == 0 && !selected) ink = allowed ? Theme.Success : Theme.Danger;
            var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding;
            using (var f = new Font("Segoe UI", 9.5f, e.ColumnIndex == 0 ? FontStyle.Bold : FontStyle.Regular))
                TextRenderer.DrawText(g, e.SubItem.Text, f, new Rectangle(e.Bounds.X + 12, e.Bounds.Y, e.Bounds.Width - 18, e.Bounds.Height), ink, flags);
        }
    }

    // Cria uma permissao manualmente (sem esperar a IA pedir).
    class AddPermissionForm : Form
    {
        public string Key;
        public bool Allow = true;

        readonly ComboBox kind = new ComboBox();
        readonly TextBox param = new TextBox();
        readonly Label paramLabel = new Label();
        readonly Label explain = new Label();
        readonly FlatButton browse = new FlatButton();
        readonly RadioButton allow = new RadioButton(), deny = new RadioButton();

        // texto, prefixo da chave, pede parametro? (0 nao, 1 nome do programa, 2 pasta)
        static readonly object[][] Kinds =
        {
            new object[] { "Ver informações da tela", "info", 0 },
            new object[] { "Listar janelas abertas", "windows", 0 },
            new object[] { "Capturar tela inteira, monitor ou região", "target:screen", 0 },
            new object[] { "Capturar janelas de QUALQUER programa", "target:window:*", 0 },
            new object[] { "Capturar janelas de um programa…", "target:window:", 1 },
            new object[] { "Gravar GIF e capturar com rolagem", "action:record", 0 },
            new object[] { "Ler texto de imagens (OCR)", "action:ocr", 0 },
            new object[] { "Ler arquivos de uma pasta…", "file:read:", 2 },
            new object[] { "Gravar arquivos em uma pasta…", "file:write:", 2 }
        };

        public AddPermissionForm()
        {
            Text = "Adicionar permissão";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(520, 330);
            Font = new Font("Segoe UI", 9.5f);

            Controls.Add(new Label { Text = "O que permitir (ou negar)", Left = 20, Top = 18, Width = 400, Height = 22, Font = new Font("Segoe UI Semibold", 10f) });
            kind.DropDownStyle = ComboBoxStyle.DropDownList;
            kind.Left = 20; kind.Top = 44; kind.Width = 480;
            foreach (var k in Kinds) kind.Items.Add(k[0]);
            kind.SelectedIndex = 0;
            kind.SelectedIndexChanged += delegate { UpdateParam(); };
            Controls.Add(kind);

            explain.Left = 20; explain.Top = 80; explain.Width = 480; explain.Height = 40; explain.ForeColor = Theme.Muted; explain.Tag = "muted";
            Controls.Add(explain);

            paramLabel.Left = 20; paramLabel.Top = 128; paramLabel.Width = 480; paramLabel.Height = 22;
            param.Left = 20; param.Top = 152; param.Width = 372;
            browse.Text = "Procurar…"; browse.Left = 400; browse.Top = 150; browse.Width = 100; browse.Height = 28;
            browse.Click += delegate
            {
                using (var d = new FolderBrowserDialog())
                    if (d.ShowDialog(this) == DialogResult.OK) param.Text = d.SelectedPath;
            };
            Controls.AddRange(new Control[] { paramLabel, param, browse });

            Controls.Add(new Label { Text = "Decisão", Left = 20, Top = 202, Width = 200, Height = 22, Font = new Font("Segoe UI Semibold", 10f) });
            allow.Text = "Permitir"; allow.Left = 20; allow.Top = 228; allow.Checked = true; allow.Width = 120; allow.Height = 24;
            deny.Text = "Negar"; deny.Left = 150; deny.Top = 228; deny.Width = 120; deny.Height = 24;
            Controls.AddRange(new Control[] { allow, deny });

            var ok = new FlatButton { Text = "Adicionar", Left = 304, Top = 280, Width = 100, Height = 34, Variant = ButtonVariant.Primary };
            var cancel = new FlatButton { Text = "Cancelar", Left = 412, Top = 280, Width = 88, Height = 34, DialogResult = DialogResult.Cancel };
            ok.Click += delegate { if (Build()) DialogResult = DialogResult.OK; };
            Controls.AddRange(new Control[] { ok, cancel });
            AcceptButton = ok; CancelButton = cancel;
            UpdateParam();
            Theme.ScaleForDpi(this);
            Theme.Apply(this);
            explain.ForeColor = Theme.Muted;
        }

        void UpdateParam()
        {
            var k = Kinds[kind.SelectedIndex];
            int needs = (int)k[2];
            param.Visible = paramLabel.Visible = needs != 0;
            browse.Visible = needs == 2;
            paramLabel.Text = needs == 1 ? "Nome do programa (como aparece em listar janelas, ex.: code, chrome):" : "Pasta (vale também para as subpastas):";
            string key = (string)k[1];
            explain.Text = PermissionRisk.Label(PermissionRisk.Of(key + (needs == 1 ? "x" : ""))) + ": " + PermissionRisk.Explain(key + (needs == 1 ? "x" : ""));
        }

        bool Build()
        {
            var k = Kinds[kind.SelectedIndex];
            int needs = (int)k[2];
            string key = (string)k[1];
            if (needs != 0)
            {
                string p = param.Text.Trim();
                if (p.Length == 0) { MessageBox.Show(this, "Preencha o campo.", Text); return false; }
                if (needs == 1) key += p.ToLowerInvariant();
                else key += p.TrimEnd('\\') + "\\*";
            }
            Key = key; Allow = allow.Checked;
            return true;
        }
    }
}
