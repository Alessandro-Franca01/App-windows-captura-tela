using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace CapturaTela
{
    class MainForm : Form
    {
        const int HotkeyMain = 1, HotkeyCtrlShiftS = 2;
        static readonly int[] Delays = { 0, 1, 2, 3, 5, 10 };

        readonly AppSettings settings = AppSettings.Load();
        readonly bool startHidden;
        bool firstShow = true;
        bool exiting;
        bool capturing;

        readonly ToolStrip bar = new ToolStrip();
        readonly ToolStripComboBox modeBox = new ToolStripComboBox();
        readonly ToolStripComboBox delayBox = new ToolStripComboBox();
        readonly ToolStripComboBox widthBox = new ToolStripComboBox();
        readonly ToolStripButton penBtn, hlBtn, eraserBtn, colorBtn, copyBtn, saveBtn, undoBtn, newBtn, settingsBtn;
        readonly Panel scroller = new Panel();
        readonly Canvas canvas = new Canvas();
        readonly Label placeholder = new Label();
        readonly StatusStrip status = new StatusStrip();
        readonly ToolStripStatusLabel statusLabel = new ToolStripStatusLabel();
        readonly NotifyIcon tray = new NotifyIcon();

        public MainForm(bool startHidden)
        {
            this.startHidden = startHidden;
            Text = "Ferramenta de Captura";
            ClientSize = new Size(900, 600);
            MinimumSize = new Size(640, 360);
            StartPosition = FormStartPosition.CenterScreen;
            Icon = SystemIcons.Application;
            KeyPreview = true;

            bar.GripStyle = ToolStripGripStyle.Hidden;
            bar.Padding = new Padding(6, 4, 6, 4);

            newBtn = Btn("+ Novo", "Nova captura (Ctrl+N ou o atalho global)", delegate { StartCapture(modeBox.SelectedIndex); });
            newBtn.Font = new Font(Font, FontStyle.Bold);
            bar.Items.Add(newBtn);

            modeBox.DropDownStyle = ComboBoxStyle.DropDownList;
            modeBox.Items.AddRange(new object[] { "Retangulo", "Forma livre", "Janela", "Tela inteira" });
            modeBox.SelectedIndex = settings.Mode;
            modeBox.ToolTipText = "Modo de captura";
            modeBox.Width = 110;
            modeBox.SelectedIndexChanged += delegate { settings.Mode = modeBox.SelectedIndex; settings.Save(); };
            bar.Items.Add(modeBox);

            delayBox.DropDownStyle = ComboBoxStyle.DropDownList;
            delayBox.Items.AddRange(new object[] { "Sem atraso", "1 segundo", "2 segundos", "3 segundos", "5 segundos", "10 segundos" });
            delayBox.SelectedIndex = settings.Delay;
            delayBox.ToolTipText = "Atraso antes da captura";
            delayBox.Width = 100;
            delayBox.SelectedIndexChanged += delegate { settings.Delay = delayBox.SelectedIndex; settings.Save(); };
            bar.Items.Add(delayBox);

            bar.Items.Add(new ToolStripSeparator());
            penBtn = Tool("Caneta", "Caneta", "pen");
            hlBtn = Tool("Marca-texto", "Marca-texto", "highlighter");
            eraserBtn = Tool("Borracha", "Borracha (apaga o traco tocado)", "eraser");
            penBtn.Checked = true;
            colorBtn = Btn("Cor", "Cor da caneta", delegate { PickColor(); });
            widthBox.DropDownStyle = ComboBoxStyle.DropDownList;
            widthBox.Items.AddRange(new object[] { "Fino", "Medio", "Grosso", "Extra grosso" });
            widthBox.SelectedIndex = settings.PenWidth;
            widthBox.Width = 90;
            widthBox.ToolTipText = "Espessura";
            widthBox.SelectedIndexChanged += delegate { ApplyWidth(); settings.PenWidth = widthBox.SelectedIndex; settings.Save(); };
            bar.Items.AddRange(new ToolStripItem[] { penBtn, hlBtn, eraserBtn, colorBtn, widthBox });
            canvas.PenColor = Color.FromArgb(settings.PenColor);
            UpdateColorButton();

            bar.Items.Add(new ToolStripSeparator());
            undoBtn = Btn("Desfazer", "Desfazer ultimo traco (Ctrl+Z)", delegate { Undo(); });
            copyBtn = Btn("Copiar", "Copiar para a area de transferencia (Ctrl+C)", delegate { CopyImage(true); });
            saveBtn = Btn("Salvar", "Salvar como... (Ctrl+S)", delegate { SaveImage(); });
            bar.Items.AddRange(new ToolStripItem[] { undoBtn, copyBtn, saveBtn });

            settingsBtn = Btn("Configuracoes", "Atalho, salvamento automatico, bandeja e inicializacao", delegate { OpenSettings(); });
            settingsBtn.Alignment = ToolStripItemAlignment.Right;
            bar.Items.Add(settingsBtn);

            scroller.Dock = DockStyle.Fill;
            scroller.AutoScroll = true;
            scroller.BackColor = Color.FromArgb(48, 48, 48);
            scroller.Controls.Add(canvas);
            scroller.Resize += delegate { CenterCanvas(); };
            canvas.Visible = false;

            placeholder.ForeColor = Color.Gainsboro;
            placeholder.TextAlign = ContentAlignment.MiddleCenter;
            placeholder.Font = new Font("Segoe UI", 12);
            placeholder.Dock = DockStyle.Fill;
            scroller.Controls.Add(placeholder);
            UpdatePlaceholder();

            status.Items.Add(statusLabel);
            statusLabel.Text = "Pronto";

            Controls.Add(scroller);
            Controls.Add(bar);
            Controls.Add(status);
            ApplyWidth();
            SetEditEnabled(false);
            BuildTray();
        }

        // ------------------------------------------------------------ Bandeja

        void BuildTray()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("Abrir", null, delegate { ShowMain(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Capturar retangulo", null, delegate { StartCapture(0); });
            menu.Items.Add("Capturar forma livre", null, delegate { StartCapture(1); });
            menu.Items.Add("Capturar janela", null, delegate { StartCapture(2); });
            menu.Items.Add("Capturar tela inteira", null, delegate { StartCapture(3); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Configuracoes...", null, delegate { ShowMain(); OpenSettings(); });
            menu.Items.Add("Sair", null, delegate { exiting = true; Close(); });
            tray.ContextMenuStrip = menu;
            tray.Icon = Icon;
            tray.Text = "Ferramenta de Captura";
            tray.Visible = true;
            tray.MouseDoubleClick += delegate(object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) ShowMain(); };
        }

        void ShowMain()
        {
            Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
        }

        protected override void SetVisibleCore(bool value)
        {
            if (startHidden && firstShow)
            {
                firstShow = false;
                if (!IsHandleCreated) CreateHandle();
                value = false;
            }
            base.SetVisibleCore(value);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!exiting && settings.CloseToTray && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                if (!settings.TrayTipShown)
                {
                    settings.TrayTipShown = true; settings.Save();
                    tray.ShowBalloonTip(3000, "Ferramenta de Captura",
                        "O app continua na bandeja. Use " + HotkeyLabel() + " para capturar.", ToolTipIcon.Info);
                }
                return;
            }
            base.OnFormClosing(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            tray.Visible = false;
            tray.Dispose();
            base.OnFormClosed(e);
        }

        string HotkeyLabel() { return AppSettings.HotkeyText(settings.HotMods, settings.HotKey); }

        void UpdatePlaceholder()
        {
            placeholder.Text = "Clique em \"+ Novo\" ou pressione " + HotkeyLabel() + " / Ctrl+Shift+S\npara fazer uma captura.";
        }

        // ------------------------------------------------------------ Barra

        ToolStripButton Btn(string text, string tip, EventHandler click)
        {
            var b = new ToolStripButton(text);
            b.ToolTipText = tip;
            b.DisplayStyle = ToolStripItemDisplayStyle.Text;
            b.Click += click;
            return b;
        }

        ToolStripButton Tool(string text, string tip, string tool)
        {
            var b = Btn(text, tip, null);
            b.CheckOnClick = false;
            b.Click += delegate
            {
                canvas.Tool = tool;
                penBtn.Checked = tool == "pen";
                hlBtn.Checked = tool == "highlighter";
                eraserBtn.Checked = tool == "eraser";
                canvas.Cursor = tool == "eraser" ? Cursors.No : Cursors.Cross;
            };
            return b;
        }

        void SetEditEnabled(bool on)
        {
            foreach (var b in new ToolStripItem[] { penBtn, hlBtn, eraserBtn, colorBtn, widthBox, undoBtn, copyBtn, saveBtn })
                b.Enabled = on;
        }

        void CenterCanvas()
        {
            int x = Math.Max(0, (scroller.ClientSize.Width - canvas.Width) / 2);
            int y = Math.Max(0, (scroller.ClientSize.Height - canvas.Height) / 2);
            canvas.Location = new Point(x + scroller.AutoScrollPosition.X, y + scroller.AutoScrollPosition.Y);
        }

        void ApplyWidth()
        {
            float[] w = { 2, 4, 8, 14 };
            canvas.PenWidth = w[Math.Max(0, widthBox.SelectedIndex)];
        }

        void PickColor()
        {
            using (var d = new ColorDialog())
            {
                d.Color = canvas.PenColor; d.FullOpen = true;
                if (d.ShowDialog(this) == DialogResult.OK)
                {
                    canvas.PenColor = d.Color; UpdateColorButton();
                    settings.PenColor = d.Color.ToArgb(); settings.Save();
                }
            }
        }

        void UpdateColorButton()
        {
            colorBtn.ForeColor = canvas.PenColor;
            colorBtn.Text = "■ Cor";
        }

        void OpenSettings()
        {
            using (var f = new SettingsForm(settings))
            {
                if (f.ShowDialog(this) == DialogResult.OK)
                {
                    RegisterHotkeys();
                    UpdatePlaceholder();
                }
            }
        }

        // ------------------------------------------------------------ Atalhos globais

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            RegisterHotkeys();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            Native.UnregisterHotKey(Handle, HotkeyMain);
            Native.UnregisterHotKey(Handle, HotkeyCtrlShiftS);
            base.OnHandleDestroyed(e);
        }

        void RegisterHotkeys()
        {
            Native.UnregisterHotKey(Handle, HotkeyMain);
            Native.UnregisterHotKey(Handle, HotkeyCtrlShiftS);
            bool ok = Native.RegisterHotKey(Handle, HotkeyMain, settings.HotMods, (uint)settings.HotKey);
            bool isCtrlShiftS = settings.HotMods == (Native.MOD_CONTROL | Native.MOD_SHIFT) && settings.HotKey == Keys.S;
            if (!isCtrlShiftS) Native.RegisterHotKey(Handle, HotkeyCtrlShiftS, Native.MOD_CONTROL | Native.MOD_SHIFT, (uint)Keys.S);
            statusLabel.Text = ok ? "Pronto - atalho: " + HotkeyLabel()
                                  : "Atalho " + HotkeyLabel() + " indisponivel (em uso por outro programa). Escolha outro em Configuracoes.";
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_HOTKEY) StartCapture(modeBox.SelectedIndex);
            base.WndProc(ref m);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.N) { StartCapture(modeBox.SelectedIndex); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.S) { if (canvas.Image != null) SaveImage(); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.C) { if (canvas.Image != null) CopyImage(true); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.Z) { Undo(); e.Handled = true; }
            base.OnKeyDown(e);
        }

        // ------------------------------------------------------------ Captura

        void StartCapture(int modeIndex)
        {
            if (capturing) return;
            capturing = true;
            var mode = (CaptureMode)modeIndex;
            int delay = Delays[Math.Max(0, delayBox.SelectedIndex)];
            bool wasVisible = Visible && WindowState != FormWindowState.Minimized;
            var prevState = WindowState;
            Hide();

            var timer = new Timer();
            timer.Interval = Math.Max(300, delay * 1000);
            timer.Tick += delegate
            {
                timer.Stop(); timer.Dispose();
                bool got = false;
                try { got = DoCapture(mode); }
                catch (Exception ex) { MessageBox.Show(ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); }
                finally
                {
                    capturing = false;
                    // captura nova mostra o editor; cancelar devolve o app ao estado anterior
                    if (got || wasVisible)
                    {
                        Show();
                        WindowState = prevState == FormWindowState.Minimized ? FormWindowState.Normal : prevState;
                        Activate();
                    }
                }
            };
            timer.Start();
        }

        bool DoCapture(CaptureMode mode)
        {
            var vs = SystemInformation.VirtualScreen;
            using (var shot = new Bitmap(vs.Width, vs.Height, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(shot))
                    g.CopyFromScreen(vs.Location, Point.Empty, vs.Size, CopyPixelOperation.SourceCopy);

                Bitmap result = null;
                if (mode == CaptureMode.FullScreen)
                {
                    result = (Bitmap)shot.Clone();
                }
                else
                {
                    using (var ov = new OverlayForm(shot, mode))
                    {
                        if (ov.ShowDialog() == DialogResult.OK) result = ov.Result;
                    }
                }
                if (result == null) return false;
                ShowResult(result);
                return true;
            }
        }

        void ShowResult(Bitmap bmp)
        {
            if (canvas.Image != null) canvas.Image.Dispose();
            canvas.SetImage(bmp);
            canvas.Visible = true;
            placeholder.Visible = false;
            scroller.AutoScrollPosition = Point.Empty;
            CenterCanvas();
            SetEditEnabled(true);
            CopyImage(false);
            string msg = "Captura: " + bmp.Width + " x " + bmp.Height + " px  -  copiada para a area de transferencia";
            if (settings.AutoSave)
            {
                string saved = AutoSaveCurrent();
                if (saved != null) msg += "  -  salva em " + saved;
            }
            statusLabel.Text = msg;
        }

        string AutoSaveCurrent()
        {
            try
            {
                Directory.CreateDirectory(settings.AutoSaveDir);
                bool jpg = settings.AutoSaveFormat == "jpg";
                string name = "Captura " + DateTime.Now.ToString("yyyy-MM-dd HHmmss") + (jpg ? ".jpg" : ".png");
                string file = Path.Combine(settings.AutoSaveDir, name);
                int n = 1;
                while (File.Exists(file))
                    file = Path.Combine(settings.AutoSaveDir, Path.GetFileNameWithoutExtension(name) + " (" + (n++) + ")" + Path.GetExtension(name));
                WriteImage(file, jpg ? ImageFormat.Jpeg : ImageFormat.Png);
                return file;
            }
            catch (Exception ex)
            {
                statusLabel.Text = "Falha no salvamento automatico: " + ex.Message;
                return null;
            }
        }

        // ------------------------------------------------------------ Acoes

        void Undo()
        {
            if (canvas.Strokes.Count == 0) return;
            canvas.Strokes.RemoveAt(canvas.Strokes.Count - 1);
            canvas.Invalidate();
        }

        void CopyImage(bool announce)
        {
            if (canvas.Image == null) return;
            try
            {
                using (var flat = canvas.Flatten())
                    Clipboard.SetImage(flat);
                if (announce) statusLabel.Text = "Copiado para a area de transferencia";
            }
            catch (Exception ex) { statusLabel.Text = "Falha ao copiar: " + ex.Message; }
        }

        void WriteImage(string file, ImageFormat fmt)
        {
            using (var flat = canvas.Flatten())
            {
                if (fmt == ImageFormat.Jpeg)
                {
                    using (var bg = new Bitmap(flat.Width, flat.Height, PixelFormat.Format24bppRgb))
                    {
                        using (var g = Graphics.FromImage(bg)) { g.Clear(Color.White); g.DrawImageUnscaled(flat, 0, 0); }
                        bg.Save(file, fmt);
                    }
                }
                else flat.Save(file, fmt);
            }
        }

        void SaveImage()
        {
            if (canvas.Image == null) return;
            using (var d = new SaveFileDialog())
            {
                d.Title = "Salvar captura";
                d.Filter = "PNG (*.png)|*.png|JPEG (*.jpg)|*.jpg|Bitmap (*.bmp)|*.bmp|GIF (*.gif)|*.gif";
                d.FileName = "Captura " + DateTime.Now.ToString("yyyy-MM-dd HHmmss");
                string pics = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                string dir = Path.Combine(pics, "Capturas de Tela");
                d.InitialDirectory = Directory.Exists(dir) ? dir : pics;
                if (d.ShowDialog(this) != DialogResult.OK) return;
                ImageFormat fmt;
                switch (d.FilterIndex)
                {
                    case 2: fmt = ImageFormat.Jpeg; break;
                    case 3: fmt = ImageFormat.Bmp; break;
                    case 4: fmt = ImageFormat.Gif; break;
                    default: fmt = ImageFormat.Png; break;
                }
                try
                {
                    WriteImage(d.FileName, fmt);
                    statusLabel.Text = "Salvo em " + d.FileName;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Nao foi possivel salvar:\n" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}
