using System;
using System.Drawing;
using System.Windows.Forms;

namespace CapturaTela
{
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
            Text = "Configuracoes";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(440, 340);
            Font = new Font("Segoe UI", 9);

            int y = 16;
            Controls.Add(new Label { Text = "Atalho de captura (clique e pressione as teclas):", Left = 16, Top = y, Width = 400 });
            y += 22;
            hotkeyBox.Left = 16; hotkeyBox.Top = y; hotkeyBox.Width = 408; hotkeyBox.ReadOnly = true;
            hotkeyBox.Text = AppSettings.HotkeyText(mods, key);
            hotkeyBox.KeyDown += OnHotkeyDown;
            hotkeyBox.KeyUp += OnHotkeyUp;
            Controls.Add(hotkeyBox);
            y += 38;

            autoSave.Text = "Salvar cada captura automaticamente em:"; autoSave.Left = 16; autoSave.Top = y; autoSave.Width = 400;
            autoSave.Checked = s.AutoSave;
            Controls.Add(autoSave);
            y += 26;
            dirBox.Left = 16; dirBox.Top = y; dirBox.Width = 330; dirBox.Text = s.AutoSaveDir;
            Controls.Add(dirBox);
            var browse = new Button { Text = "Procurar...", Left = 352, Top = y - 1, Width = 72 };
            browse.Click += delegate
            {
                using (var d = new FolderBrowserDialog())
                {
                    d.SelectedPath = dirBox.Text;
                    if (d.ShowDialog(this) == DialogResult.OK) dirBox.Text = d.SelectedPath;
                }
            };
            Controls.Add(browse);
            y += 30;
            Controls.Add(new Label { Text = "Formato:", Left = 16, Top = y + 3, Width = 60 });
            fmtBox.DropDownStyle = ComboBoxStyle.DropDownList;
            fmtBox.Items.AddRange(new object[] { "PNG", "JPG" });
            fmtBox.SelectedIndex = s.AutoSaveFormat == "jpg" ? 1 : 0;
            fmtBox.Left = 80; fmtBox.Top = y; fmtBox.Width = 80;
            Controls.Add(fmtBox);
            y += 38;

            tray.Text = "Fechar a janela mantem o app na bandeja do sistema"; tray.Left = 16; tray.Top = y; tray.Width = 410;
            tray.Checked = s.CloseToTray;
            Controls.Add(tray);
            y += 26;
            startup.Text = "Iniciar com o Windows (minimizado na bandeja)"; startup.Left = 16; startup.Top = y; startup.Width = 410;
            startup.Checked = AppSettings.GetStartWithWindows();
            Controls.Add(startup);
            y += 26;
            notify.Text = "Mostrar notificacao depois de capturar ou gravar"; notify.Left = 16; notify.Top = y; notify.Width = 410;
            notify.Checked = s.Notify;
            Controls.Add(notify);
            y += 34;
            Controls.Add(new Label { Text = "Tema:", Left = 16, Top = y + 3, Width = 50 });
            themeBox.DropDownStyle = ComboBoxStyle.DropDownList;
            themeBox.Items.AddRange(new object[] { "Seguir o Windows", "Claro", "Escuro" });
            themeBox.SelectedIndex = s.Theme;
            themeBox.Left = 70; themeBox.Top = y; themeBox.Width = 160;
            Controls.Add(themeBox);

            var ok = new Button { Text = "OK", Left = 268, Top = 302, Width = 75, DialogResult = DialogResult.OK };
            var cancel = new Button { Text = "Cancelar", Left = 349, Top = 302, Width = 75, DialogResult = DialogResult.Cancel };
            ok.Click += delegate { Apply(); };
            Controls.Add(ok); Controls.Add(cancel);
            AcceptButton = ok; CancelButton = cancel;
            Theme.Apply(this);
        }

        void OnHotkeyDown(object sender, KeyEventArgs e)
        {
            e.SuppressKeyPress = true; e.Handled = true;
            if (IsModifier(e.KeyCode)) return;
            if (e.KeyCode == Keys.PrintScreen) return; // Print Screen so gera KeyUp
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
