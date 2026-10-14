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

        public PermissionsForm()
        {
            Text = "Permissoes da IA";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(700, 464);
            Font = new Font("Segoe UI", 9);

            enabled.Text = "Permitir que programas de IA (servidor MCP) usem a Ferramenta de Captura";
            enabled.Left = 16; enabled.Top = 14; enabled.Width = 660; enabled.Checked = store.Enabled;
            gateCli.Text = "Exigir permissao tambem na linha de comando (CapturaTelaCli.exe)";
            gateCli.Left = 16; gateCli.Top = 40; gateCli.Width = 660; gateCli.Checked = store.GateCli;
            Controls.Add(enabled); Controls.Add(gateCli);

            Controls.Add(new Label
            {
                Text = "Permissoes salvas. Quando uma IA pedir algo que nao esta aqui, voce escolhe: uma vez, sempre ou negar.",
                Left = 16, Top = 72, Width = 668, Height = 36
            });

            list.Left = 16; list.Top = 112; list.Width = 668; list.Height = 240;
            list.View = View.Details; list.FullRowSelect = true; list.MultiSelect = true; list.HideSelection = false;
            list.Columns.Add("Estado", 90);
            list.Columns.Add("O que a IA pode (ou nao) fazer", 350);
            list.Columns.Add("Chave", 210);
            Controls.Add(list);
            Fill();

            var revoke = new Button { Text = "Revogar selecionadas", Left = 16, Top = 362, Width = 180 };
            revoke.Click += delegate
            {
                foreach (ListViewItem it in list.SelectedItems) store.Remove((string)it.Tag);
                Fill();
            };
            var revokeAll = new Button { Text = "Revogar todas", Left = 204, Top = 362, Width = 130 };
            revokeAll.Click += delegate
            {
                if (store.Entries.Count == 0) return;
                if (MessageBox.Show(this, "Remover todas as permissoes salvas? A IA voltara a pedir cada uma.", Text,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes) { store.Entries.Clear(); Fill(); }
            };
            var add = new Button { Text = "Adicionar...", Left = 342, Top = 362, Width = 120 };
            add.Click += delegate
            {
                using (var f = new AddPermissionForm())
                    if (f.ShowDialog(this) == DialogResult.OK) { store.Set(f.Key, f.Allow); Fill(); }
            };
            Controls.AddRange(new Control[] { revoke, revokeAll, add });

            Controls.Add(new Label
            {
                Text = "Observacao: o MCP e a CLI rodam com o seu usuario. Este controle protege contra pedidos indevidos da IA, nao contra um programa malicioso.",
                Left = 16, Top = 396, Width = 500, Height = 56, ForeColor = Color.FromArgb(140, 140, 140)
            });

            var ok = new Button { Text = "OK", Left = 528, Top = 414, Width = 75, DialogResult = DialogResult.OK };
            var cancel = new Button { Text = "Cancelar", Left = 609, Top = 414, Width = 75, DialogResult = DialogResult.Cancel };
            ok.Click += delegate
            {
                store.Enabled = enabled.Checked; store.GateCli = gateCli.Checked;
                try { store.Save(); }
                catch (Exception ex) { MessageBox.Show(this, "Nao foi possivel salvar: " + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); }
            };
            Controls.AddRange(new Control[] { ok, cancel });
            AcceptButton = ok; CancelButton = cancel;
            Theme.Apply(this);
        }

        void Fill()
        {
            list.Items.Clear();
            foreach (var e in store.Entries)
            {
                var it = new ListViewItem(e.Value ? "Permitido" : "Negado");
                it.SubItems.Add(PermissionStore.Describe(e.Key));
                it.SubItems.Add(e.Key);
                it.Tag = e.Key;
                it.ForeColor = e.Value ? Color.SeaGreen : Color.Firebrick;
                list.Items.Add(it);
            }
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
        readonly Button browse = new Button();
        readonly RadioButton allow = new RadioButton(), deny = new RadioButton();

        // texto, prefixo da chave, pede parametro? (0 nao, 1 nome do programa, 2 pasta)
        static readonly object[][] Kinds =
        {
            new object[] { "Ver informacoes da tela", "info", 0 },
            new object[] { "Listar janelas abertas", "windows", 0 },
            new object[] { "Capturar tela inteira, monitor ou regiao", "target:screen", 0 },
            new object[] { "Capturar janelas de QUALQUER programa", "target:window:*", 0 },
            new object[] { "Capturar janelas de um programa...", "target:window:", 1 },
            new object[] { "Gravar GIF e capturar com rolagem", "action:record", 0 },
            new object[] { "Ler texto de imagens (OCR)", "action:ocr", 0 },
            new object[] { "Ler arquivos de uma pasta...", "file:read:", 2 },
            new object[] { "Gravar arquivos em uma pasta...", "file:write:", 2 }
        };

        public AddPermissionForm()
        {
            Text = "Adicionar permissao";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(440, 210);
            Font = new Font("Segoe UI", 9);

            Controls.Add(new Label { Text = "Permissao:", Left = 16, Top = 16, Width = 100 });
            kind.DropDownStyle = ComboBoxStyle.DropDownList;
            kind.Left = 16; kind.Top = 38; kind.Width = 408;
            foreach (var k in Kinds) kind.Items.Add(k[0]);
            kind.SelectedIndex = 0;
            kind.SelectedIndexChanged += delegate { UpdateParam(); };
            Controls.Add(kind);

            paramLabel.Left = 16; paramLabel.Top = 72; paramLabel.Width = 300;
            param.Left = 16; param.Top = 94; param.Width = 320;
            browse.Text = "Procurar..."; browse.Left = 344; browse.Top = 93; browse.Width = 80;
            browse.Click += delegate
            {
                using (var d = new FolderBrowserDialog())
                    if (d.ShowDialog(this) == DialogResult.OK) param.Text = d.SelectedPath;
            };
            Controls.AddRange(new Control[] { paramLabel, param, browse });

            allow.Text = "Permitir"; allow.Left = 16; allow.Top = 132; allow.Checked = true; allow.Width = 100;
            deny.Text = "Negar"; deny.Left = 124; deny.Top = 132; deny.Width = 100;
            Controls.AddRange(new Control[] { allow, deny });

            var ok = new Button { Text = "Adicionar", Left = 268, Top = 168, Width = 75 };
            var cancel = new Button { Text = "Cancelar", Left = 349, Top = 168, Width = 75, DialogResult = DialogResult.Cancel };
            ok.Click += delegate { if (Build()) DialogResult = DialogResult.OK; };
            Controls.AddRange(new Control[] { ok, cancel });
            AcceptButton = ok; CancelButton = cancel;
            UpdateParam();
            Theme.Apply(this);
        }

        void UpdateParam()
        {
            int needs = (int)Kinds[kind.SelectedIndex][2];
            param.Visible = paramLabel.Visible = needs != 0;
            browse.Visible = needs == 2;
            paramLabel.Text = needs == 1 ? "Nome do programa (como em 'windows', ex.: code, chrome):" : "Pasta (vale tambem para as subpastas):";
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
