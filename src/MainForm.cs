using System;
using System.Collections.Generic;
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
        IStoppable activeSession;   // gravacao de GIF ou rolagem manual em andamento

        readonly ToolStrip bar = new ToolStrip();
        readonly ToolStrip tools = new ToolStrip();
        readonly ToolStripComboBox modeBox = new ToolStripComboBox();
        readonly ToolStripComboBox delayBox = new ToolStripComboBox();
        readonly ToolStripComboBox widthBox = new ToolStripComboBox();
        readonly Dictionary<string, ToolStripButton> toolBtns = new Dictionary<string, ToolStripButton>();
        readonly List<ToolStripItem> editItems = new List<ToolStripItem>();
        readonly ToolStripLabel zoomLabel = new ToolStripLabel("100%");
        readonly ToolStripButton colorBtn, copyBtn, saveBtn, undoBtn, redoBtn, newBtn, settingsBtn, ocrBtn;
        Action balloonClick;
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
            ClientSize = new Size(960, 640);
            MinimumSize = new Size(700, 400);
            StartPosition = FormStartPosition.CenterScreen;
            Icon = AppIcon.Get(SystemInformation.IconSize);
            KeyPreview = true;
            Theme.Preference = settings.Theme;

            // ---- barra principal: captura e acoes ----
            bar.GripStyle = ToolStripGripStyle.Hidden;
            bar.Padding = new Padding(6, 4, 6, 4);

            newBtn = Btn("+ Novo", "Nova captura (Ctrl+N ou o atalho global)", delegate { StartCapture(CurrentMode); });
            newBtn.Font = new Font(Font, FontStyle.Bold);
            bar.Items.Add(newBtn);

            modeBox.DropDownStyle = ComboBoxStyle.DropDownList;
            modeBox.Items.AddRange(ModeList.Names);
            modeBox.SelectedIndex = ModeList.IndexOf((CaptureMode)settings.Mode);
            modeBox.ToolTipText = "Modo de captura (Rolagem auto: o app rola sozinho; Rolagem manual: voce rola)";
            modeBox.Width = 120;
            modeBox.SelectedIndexChanged += delegate { settings.Mode = (int)CurrentMode; settings.Save(); };
            bar.Items.Add(modeBox);

            delayBox.DropDownStyle = ComboBoxStyle.DropDownList;
            delayBox.Items.AddRange(new object[] { "Sem atraso", "1 segundo", "2 segundos", "3 segundos", "5 segundos", "10 segundos" });
            delayBox.SelectedIndex = settings.Delay;
            delayBox.ToolTipText = "Atraso antes da captura";
            delayBox.Width = 100;
            delayBox.SelectedIndexChanged += delegate { settings.Delay = delayBox.SelectedIndex; settings.Save(); };
            bar.Items.Add(delayBox);

            bar.Items.Add(new ToolStripSeparator());
            undoBtn = Btn("Desfazer", "Desfazer (Ctrl+Z)", delegate { canvas.Undo(); });
            redoBtn = Btn("Refazer", "Refazer (Ctrl+Y)", delegate { canvas.Redo(); });
            copyBtn = Btn("Copiar", "Copiar para a area de transferencia (Ctrl+C)", delegate { CopyImage(true); });
            saveBtn = Btn("Salvar", "Salvar como... (Ctrl+S)", delegate { SaveImage(); });
            ocrBtn = Btn("Copiar texto", "Reconhecer o texto da imagem (OCR) e copiar", delegate { RunOcr(); });
            bar.Items.AddRange(new ToolStripItem[] { undoBtn, redoBtn, copyBtn, saveBtn, ocrBtn });
            editItems.AddRange(new ToolStripItem[] { undoBtn, redoBtn, copyBtn, saveBtn, ocrBtn });

            settingsBtn = Btn("Configuracoes", "Atalho, salvamento automatico, bandeja e inicializacao", delegate { OpenSettings(); });
            settingsBtn.Alignment = ToolStripItemAlignment.Right;
            bar.Items.Add(settingsBtn);

            // ---- segunda barra: ferramentas de edicao ----
            tools.GripStyle = ToolStripGripStyle.Hidden;
            tools.Padding = new Padding(6, 2, 6, 2);
            AddTool("pen", "Caneta", "Caneta");
            AddTool("highlighter", "Marca-texto", "Marca-texto");
            AddTool("arrow", "Seta", "Seta (Shift = 45 graus)");
            AddTool("line", "Linha", "Linha (Shift = 45 graus)");
            AddTool("rect", "Retangulo", "Retangulo (Shift = quadrado)");
            AddTool("ellipse", "Elipse", "Elipse (Shift = circulo)");
            AddTool("text", "Texto", "Caixa de texto");
            AddTool("step", "Passo", "Marcador numerado (1, 2, 3...)");
            AddTool("blur", "Desfocar", "Desfocar uma area (esconde dados sensiveis)");
            AddTool("pixelate", "Pixelar", "Pixelar uma area (esconde dados sensiveis)");
            AddTool("crop", "Cortar", "Cortar a imagem");
            AddTool("picker", "Conta-gotas", "Pegar uma cor da imagem");
            AddTool("eraser", "Borracha", "Borracha (apaga a anotacao tocada)");
            toolBtns["pen"].Checked = true;

            tools.Items.Add(new ToolStripSeparator());
            colorBtn = Btn("Cor", "Cor das anotacoes", delegate { PickColor(); });
            widthBox.DropDownStyle = ComboBoxStyle.DropDownList;
            widthBox.Items.AddRange(new object[] { "Fino", "Medio", "Grosso", "Extra grosso" });
            widthBox.SelectedIndex = settings.PenWidth;
            widthBox.Width = 90;
            widthBox.ToolTipText = "Espessura / tamanho / intensidade";
            widthBox.SelectedIndexChanged += delegate { ApplyWidth(); settings.PenWidth = widthBox.SelectedIndex; settings.Save(); };
            tools.Items.AddRange(new ToolStripItem[] { colorBtn, widthBox });
            editItems.AddRange(new ToolStripItem[] { colorBtn, widthBox });
            canvas.PenColor = Color.FromArgb(settings.PenColor);
            UpdateColorButton();

            tools.Items.Add(new ToolStripSeparator());
            var zoomOut = Btn("-", "Diminuir zoom (Ctrl + roda do mouse)", delegate { canvas.SetZoom(canvas.Zoom / 1.25f); });
            var zoomIn = Btn("+", "Aumentar zoom (Ctrl + roda do mouse)", delegate { canvas.SetZoom(canvas.Zoom * 1.25f); });
            var zoom100 = Btn("100%", "Tamanho real", delegate { canvas.SetZoom(1f); });
            var zoomFit = Btn("Ajustar", "Ajustar a janela", delegate { FitZoom(); });
            tools.Items.AddRange(new ToolStripItem[] { zoomOut, zoomLabel, zoomIn, zoom100, zoomFit });
            editItems.AddRange(new ToolStripItem[] { zoomOut, zoomIn, zoom100, zoomFit });

            canvas.LayoutChanged += delegate { CenterCanvas(); zoomLabel.Text = ((int)Math.Round(canvas.Zoom * 100)) + "%"; };
            canvas.ColorPicked += delegate(Color c) { UpdateColorButton(); settings.PenColor = c.ToArgb(); settings.Save(); };
            canvas.HistoryChanged += delegate { undoBtn.Enabled = canvas.CanUndo; redoBtn.Enabled = canvas.CanRedo; };
            canvas.MouseWheel += OnCanvasWheel;

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

            // ordem de inclusao define o encaixe: o ultimo fica mais externo
            Controls.Add(scroller);
            Controls.Add(tools);
            Controls.Add(bar);
            Controls.Add(status);
            ApplyWidth();
            SetEditEnabled(false);
            BuildTray();
            ApplyTheme();
        }

        void ApplyTheme()
        {
            Theme.Preference = settings.Theme;
            bool dark = Theme.IsDark;
            Theme.Apply(this);
            Theme.Apply(bar, dark);
            Theme.Apply(tools, dark);
            Theme.Apply(status, dark);
            scroller.BackColor = dark ? Color.FromArgb(24, 24, 24) : Color.FromArgb(48, 48, 48);
            UpdateColorButton();
        }

        // Notificacao do Windows (balao da bandeja); o clique executa a acao.
        void Notify(string title, string text, Action onClick)
        {
            if (!settings.Notify) return;
            balloonClick = onClick;
            tray.ShowBalloonTip(4000, title, text, ToolTipIcon.Info);
        }

        static void OpenPath(string path)
        {
            try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); } catch { }
        }

        void RunOcr()
        {
            if (canvas.Image == null) return;
            statusLabel.Text = "Reconhecendo texto...";
            Cursor = Cursors.WaitCursor;
            using (var flat = canvas.Flatten())
            {
                Ocr.Recognize(flat, delegate(string text, string error)
                {
                    BeginInvoke(new Action(delegate
                    {
                        Cursor = Cursors.Default;
                        if (error != null)
                        {
                            statusLabel.Text = "OCR falhou";
                            MessageBox.Show(this, error, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                        else if (string.IsNullOrEmpty(text))
                        {
                            statusLabel.Text = "Nenhum texto encontrado na imagem";
                        }
                        else
                        {
                            try { Clipboard.SetText(text); } catch { }
                            statusLabel.Text = "Texto reconhecido e copiado (" + text.Length + " caracteres)";
                            using (var f = new OcrResultForm(text)) f.ShowDialog(this);
                        }
                    }));
                });
            }
        }

        // ------------------------------------------------------------ Bandeja

        void BuildTray()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("Abrir", null, delegate { ShowMain(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Capturar retangulo", null, delegate { StartCapture(CaptureMode.Rectangle); });
            menu.Items.Add("Capturar forma livre", null, delegate { StartCapture(CaptureMode.Freeform); });
            menu.Items.Add("Capturar janela", null, delegate { StartCapture(CaptureMode.Window); });
            menu.Items.Add("Capturar tela inteira", null, delegate { StartCapture(CaptureMode.FullScreen); });
            menu.Items.Add("Captura com rolagem automatica", null, delegate { StartCapture(CaptureMode.Scroll); });
            menu.Items.Add("Captura com rolagem manual", null, delegate { StartCapture(CaptureMode.ScrollManual); });
            menu.Items.Add("Gravar GIF", null, delegate { StartCapture(CaptureMode.Record); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Configuracoes...", null, delegate { ShowMain(); OpenSettings(); });
            menu.Items.Add("Sair", null, delegate { exiting = true; Close(); });
            tray.ContextMenuStrip = menu;
            tray.Icon = AppIcon.Get(SystemInformation.SmallIconSize);
            tray.Text = "Ferramenta de Captura";
            tray.BalloonTipClicked += delegate
            {
                var a = balloonClick; balloonClick = null;
                if (a != null) a();
            };
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

        // ------------------------------------------------------------ Barras

        ToolStripButton Btn(string text, string tip, EventHandler click)
        {
            var b = new ToolStripButton(text);
            b.ToolTipText = tip;
            b.DisplayStyle = ToolStripItemDisplayStyle.Text;
            if (click != null) b.Click += click;
            return b;
        }

        void AddTool(string tool, string text, string tip)
        {
            var b = Btn(text, tip, null);
            b.Click += delegate { SelectTool(tool); };
            toolBtns[tool] = b;
            tools.Items.Add(b);
            editItems.Add(b);
        }

        void SelectTool(string tool)
        {
            canvas.Tool = tool;
            foreach (var kv in toolBtns) kv.Value.Checked = kv.Key == tool;
            canvas.Cursor = tool == "eraser" ? Cursors.No : tool == "picker" ? Cursors.Hand : tool == "text" ? Cursors.IBeam : Cursors.Cross;
        }

        void SetEditEnabled(bool on)
        {
            foreach (var b in editItems) b.Enabled = on;
            if (on) { undoBtn.Enabled = canvas.CanUndo; redoBtn.Enabled = canvas.CanRedo; }
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
                    ApplyTheme();
                }
            }
        }

        void OnCanvasWheel(object sender, MouseEventArgs e)
        {
            var he = e as HandledMouseEventArgs;
            if (he != null) he.Handled = true;
            if ((ModifierKeys & Keys.Control) == Keys.Control)
            {
                canvas.SetZoom(e.Delta > 0 ? canvas.Zoom * 1.25f : canvas.Zoom / 1.25f);
                return;
            }
            var pos = new Point(-scroller.AutoScrollPosition.X, -scroller.AutoScrollPosition.Y);
            if ((ModifierKeys & Keys.Shift) == Keys.Shift) pos.X -= e.Delta; else pos.Y -= e.Delta;
            scroller.AutoScrollPosition = pos;
            CenterCanvas();
        }

        void FitZoom()
        {
            if (canvas.Image == null) return;
            float zx = (scroller.ClientSize.Width - 20f) / canvas.Image.Width;
            float zy = (scroller.ClientSize.Height - 20f) / canvas.Image.Height;
            canvas.SetZoom(Math.Min(zx, zy));
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
            if (m.Msg == Native.WM_HOTKEY)
            {
                // durante uma gravacao o atalho global para a gravacao
                if (activeSession != null) activeSession.RequestStop();
                else StartCapture(CurrentMode);
            }
            base.WndProc(ref m);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.N) { StartCapture(CurrentMode); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.S) { if (canvas.Image != null) SaveImage(); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.C) { if (canvas.Image != null) CopyImage(true); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.Z) { canvas.Undo(); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.Y) { canvas.Redo(); e.Handled = true; }
            base.OnKeyDown(e);
        }

        // ------------------------------------------------------------ Captura

        CaptureMode CurrentMode { get { return ModeList.Order[Math.Max(0, modeBox.SelectedIndex)]; } }

        void StartCapture(CaptureMode mode)
        {
            if (capturing) return;
            capturing = true;

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
                    CaptureMode chosen;
                    Rectangle area;
                    using (var ov = new OverlayForm(shot, mode))
                    {
                        if (ov.ShowDialog() != DialogResult.OK) return false;
                        result = ov.Result; chosen = ov.ChosenMode; area = ov.SelectedRect;
                    }
                    if (chosen == CaptureMode.Scroll || chosen == CaptureMode.ScrollManual || chosen == CaptureMode.Record)
                    {
                        // deixa o desktop se redesenhar depois de fechar o overlay
                        System.Threading.Thread.Sleep(250);
                        Application.DoEvents();
                        if (chosen == CaptureMode.Record) return RecordGif(area);
                        if (chosen == CaptureMode.ScrollManual)
                        {
                            using (var f = new ScrollManualForm(area))
                            {
                                activeSession = f;
                                try { if (f.ShowDialog() != DialogResult.OK) return false; }
                                finally { activeSession = null; }
                                result = f.Result;
                            }
                        }
                        else
                        {
                            result = ScrollCapture.Run(area);
                            // nada rolou (ex.: o conteudo nao reage a roda do mouse): sugere o modo manual
                            if (result != null && result.Height <= area.Height + 2)
                            {
                                ShowResult(result);
                                statusLabel.Text = "A rolagem automatica nao moveu o conteudo. Tente o modo \"Rolagem manual\" na lista de modos.";
                                Notify("Rolagem automatica sem efeito", "O conteudo nao rolou. Use o modo \"Rolagem manual\" e role voce mesmo.", ShowMain);
                                return true;
                            }
                        }
                    }
                }
                if (result == null) return false;
                ShowResult(result);
                return true;
            }
        }

        // Grava a area em GIF e salva. Devolve true se gerou o arquivo.
        bool RecordGif(Rectangle area)
        {
            List<Bitmap> frames;
            int delay;
            using (var rec = new RecorderForm(area))
            {
                activeSession = rec;
                try
                {
                    if (rec.ShowDialog() != DialogResult.OK) return false;
                }
                finally { activeSession = null; }
                frames = rec.Frames; delay = rec.DelayMs;
            }

            string file = null;
            try
            {
                if (settings.AutoSave)
                {
                    Directory.CreateDirectory(settings.AutoSaveDir);
                    file = Path.Combine(settings.AutoSaveDir, "Gravacao " + DateTime.Now.ToString("yyyy-MM-dd HHmmss") + ".gif");
                }
                else
                {
                    using (var d = new SaveFileDialog())
                    {
                        d.Title = "Salvar gravacao";
                        d.Filter = "GIF animado (*.gif)|*.gif";
                        d.FileName = "Gravacao " + DateTime.Now.ToString("yyyy-MM-dd HHmmss");
                        if (d.ShowDialog() != DialogResult.OK) return false;
                        file = d.FileName;
                    }
                }
                Cursor = Cursors.WaitCursor;
                GifWriter.Save(file, frames, delay);
                long kb = new FileInfo(file).Length / 1024;
                statusLabel.Text = "GIF salvo em " + file + " (" + frames.Count + " quadros, " + kb + " KB)";
                string gif = file;
                Notify("GIF salvo", kb + " KB em " + gif + ". Clique para abrir.", delegate { OpenPath(gif); });
                try
                {
                    var list = new System.Collections.Specialized.StringCollection();
                    list.Add(file);
                    Clipboard.SetFileDropList(list);
                }
                catch { }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Nao foi possivel salvar o GIF:\n" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            finally
            {
                Cursor = Cursors.Default;
                foreach (var f in frames) f.Dispose();
            }
            return true;
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
            string saved = settings.AutoSave ? AutoSaveCurrent() : null;
            if (saved != null) msg += "  -  salva em " + saved;
            statusLabel.Text = msg;
            if (saved != null) Notify("Captura salva", "Copiada e salva em " + saved + ". Clique para abrir.", delegate { OpenPath(saved); });
            else Notify("Captura copiada", "Ela esta na area de transferencia. Clique para editar.", ShowMain);
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
