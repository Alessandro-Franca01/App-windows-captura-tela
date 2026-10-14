using System;
using System.Drawing;
using System.Windows.Forms;

namespace CapturaTela
{
    // Configuracoes agrupadas por assunto: Captura, Salvamento, Sistema, Aparencia e Inteligencia artificial.
    class SettingsForm : Form
    {
        readonly AppSettings s;
        readonly TextBox hotkeyBox = new TextBox();
        readonly CheckBox autoSave = new CheckBox();
        readonly TextBox dirBox = new TextBox();
        readonly ComboBox fmtBox = new ComboBox();
        readonly CheckBox tray = new CheckBox();
        readonly CheckBox startup = new CheckBox();
        readonly CheckBox notify = new CheckBox();
        readonly ComboBox themeBox = new ComboBox();
        uint mods;
        Keys key;

        public SettingsForm(AppSettings settings)
        {
            s = settings; mods = s.HotMods; key = s.HotKey;
            Text = "Configurações";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(540, 590);
            Font = new Font("Segoe UI", 9.5f);

            // ---- Captura ----
            Section("Captura", 14);
            Label("Atalho para capturar de qualquer lugar", 20, 46, 330);
            hotkeyBox.Left = 20; hotkeyBox.Top = 68; hotkeyBox.Width = 230; hotkeyBox.ReadOnly = true;
            hotkeyBox.Font = new Font("Segoe UI Semibold", 10.5f);
            hotkeyBox.Text = AppSettings.HotkeyText(mods, key);
            hotkeyBox.KeyDown += OnHotkeyDown;
            hotkeyBox.KeyUp += OnHotkeyUp;
            // o campo somente leitura seleciona o texto ao receber foco: parece um erro, entao tiramos a selecao
            hotkeyBox.GotFocus += delegate { BeginInvoke(new Action(delegate { hotkeyBox.SelectionStart = hotkeyBox.TextLength; hotkeyBox.SelectionLength = 0; })); };
            Controls.Add(hotkeyBox);
            Muted("Clique na caixa e aperte as teclas.\nCtrl+Shift+S continua funcionando.", 262, 66, 258, 34);

            // ---- Salvamento ----
            Section("Salvamento", 116);
            Check(autoSave, "Salvar cada captura automaticamente na pasta abaixo", 20, 148, s.AutoSave);
            dirBox.Left = 20; dirBox.Top = 176; dirBox.Width = 388; dirBox.Text = s.AutoSaveDir;
            Controls.Add(dirBox);
            var browse = new FlatButton { Text = "Procurar…", Left = 418, Top = 174, Width = 102, Height = 28 };
            browse.Click += delegate
            {
                using (var d = new FolderBrowserDialog())
                {
                    d.SelectedPath = dirBox.Text;
                    if (d.ShowDialog(this) == DialogResult.OK) dirBox.Text = d.SelectedPath;
                }
            };
            Controls.Add(browse);
            Label("Formato:", 20, 212, 70);
            fmtBox.DropDownStyle = ComboBoxStyle.DropDownList;
            fmtBox.Items.AddRange(new object[] { "PNG", "JPG" });
            fmtBox.SelectedIndex = s.AutoSaveFormat == "jpg" ? 1 : 0;
            fmtBox.Left = 94; fmtBox.Top = 208; fmtBox.Width = 90;
            Controls.Add(fmtBox);

            // ---- Sistema ----
            Section("Sistema", 252);
            Check(tray, "Fechar a janela mantém o app na bandeja do sistema", 20, 284, s.CloseToTray);
            Check(startup, "Iniciar com o Windows (minimizado na bandeja)", 20, 312, AppSettings.GetStartWithWindows());
            Check(notify, "Mostrar notificação depois de capturar ou gravar", 20, 340, s.Notify);

            // ---- Aparencia ----
            Section("Aparência", 380);
            Label("Tema:", 20, 416, 60);
            themeBox.DropDownStyle = ComboBoxStyle.DropDownList;
            themeBox.Items.AddRange(new object[] { "Seguir o Windows", "Claro", "Escuro" });
            themeBox.SelectedIndex = s.Theme;
            themeBox.Left = 84; themeBox.Top = 412; themeBox.Width = 180;
            Controls.Add(themeBox);

            // ---- Inteligencia artificial ----
            Section("Inteligência artificial", 454);
            Muted("Você decide o que programas de IA podem fazer com a Ferramenta de Captura.", 20, 486, 500, 20);
            var perms = new FlatButton { Text = "Permissões da IA…", Left = 20, Top = 510, Width = 190, Height = 32, Icon = IconKind.Shield };
            perms.Click += delegate { using (var f = new PermissionsForm()) f.ShowDialog(this); };
            Controls.Add(perms);

            var ok = new FlatButton { Text = "OK", Left = 336, Top = 548, Width = 90, Height = 34, Variant = ButtonVariant.Primary, DialogResult = DialogResult.OK };
            var cancel = new FlatButton { Text = "Cancelar", Left = 434, Top = 548, Width = 90, Height = 34, DialogResult = DialogResult.Cancel };
            ok.Click += delegate { Apply(); };
            Controls.Add(ok); Controls.Add(cancel);
            AcceptButton = ok; CancelButton = cancel;
            Theme.ScaleForDpi(this);
            Theme.Apply(this);
            foreach (Control c in Controls) { var l = c as Label; if (l != null && l.Tag as string == "muted") l.ForeColor = Theme.Muted; }
        }

        // ---- construcao ----

        void Section(string title, int y)
        {
            Controls.Add(new Label { Text = title, Left = 20, Top = y, Width = 340, Height = 24, Font = new Font("Segoe UI Semibold", 10.5f), Tag = "section" });
            Controls.Add(new Divider { Left = 20, Top = y + 26, Width = 500, Height = 1 });
        }

        void Label(string text, int x, int y, int w)
        {
            Controls.Add(new Label { Text = text, Left = x, Top = y, Width = w, Height = 22 });
        }

        void Muted(string text, int x, int y, int w, int h)
        {
            Controls.Add(new Label { Text = text, Left = x, Top = y, Width = w, Height = h, ForeColor = Theme.Muted, Tag = "muted" });
        }

        void Check(CheckBox c, string text, int x, int y, bool value)
        {
            c.Text = text; c.Left = x; c.Top = y; c.Width = 500; c.Height = 24; c.Checked = value;
            Controls.Add(c);
        }

        // linha fina entre secoes
        class Divider : Control
        {
            public Divider() { SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); }
            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(Parent != null ? Parent.BackColor : Theme.Surface);
                using (var p = new Pen(Theme.BarBorder)) e.Graphics.DrawLine(p, 0, 0, Width, 0);
            }
        }

        // ---- atalho ----

        void OnHotkeyDown(object sender, KeyEventArgs e)
        {
            e.SuppressKeyPress = true; e.Handled = true;
            if (IsModifier(e.KeyCode)) return;
            if (e.KeyCode == Keys.PrintScreen) return; // Print Screen só gera KeyUp
            SetHotkey(e);
        }

        void OnHotkeyUp(object sender, KeyEventArgs e)
        {
            e.Handled = true;
            if (e.KeyCode == Keys.PrintScreen) SetHotkey(e);
        }

        static bool IsModifier(Keys k)
        {
            return k == Keys.ControlKey || k == Keys.ShiftKey || k == Keys.Menu || k == Keys.LWin || k == Keys.RWin
                || k == Keys.LControlKey || k == Keys.RControlKey || k == Keys.LShiftKey || k == Keys.RShiftKey
                || k == Keys.LMenu || k == Keys.RMenu;
        }

        void SetHotkey(KeyEventArgs e)
        {
            uint m = 0;
            if (e.Control) m |= Native.MOD_CONTROL;
            if (e.Alt) m |= Native.MOD_ALT;
            if (e.Shift) m |= Native.MOD_SHIFT;
            mods = m; key = e.KeyCode;
            hotkeyBox.Text = AppSettings.HotkeyText(mods, key);
            hotkeyBox.SelectionStart = hotkeyBox.TextLength; hotkeyBox.SelectionLength = 0;
        }

        void Apply()
        {
            s.HotMods = mods; s.HotKey = key;
            s.AutoSave = autoSave.Checked;
            if (dirBox.Text.Trim().Length > 0) s.AutoSaveDir = dirBox.Text.Trim();
            s.AutoSaveFormat = fmtBox.SelectedIndex == 1 ? "jpg" : "png";
            s.CloseToTray = tray.Checked;
            s.Notify = notify.Checked;
            s.Theme = themeBox.SelectedIndex;
            AppSettings.SetStartWithWindows(startup.Checked);
            s.Save();
        }
    }
}
