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
        ChoiceButton modeBox, delayBox, widthBox;
        int iconSize;
        float uiScale = 1f;
        readonly Dictionary<string, ToolStripButton> toolBtns = new Dictionary<string, ToolStripButton>();
        readonly List<ToolStripItem> editItems = new List<ToolStripItem>();
        ToolStripButton zoomLabel;
        readonly ToolStripButton colorBtn, copyBtn, saveBtn, undoBtn, redoBtn, newBtn, settingsBtn, helpBtn, ocrBtn;
        HelpForm helpForm;
        Action balloonClick;
        readonly Panel scroller = new Panel();
        readonly Canvas canvas = new Canvas();
        readonly EmptyState placeholder = new EmptyState();
        readonly StatusStrip status = new StatusStrip();
        readonly ToolStripStatusLabel statusLabel = new ToolStripStatusLabel();
        readonly ToolStripStatusLabel sizeLabel = new ToolStripStatusLabel();
        readonly NotifyIcon tray = new NotifyIcon();

        public MainForm(bool startHidden)
        {
            this.startHidden = startHidden;
            Text = "Ferramenta de Captura";
            ClientSize = new Size(1040, 680);
            MinimumSize = new Size(700, 400);
            StartPosition = FormStartPosition.CenterScreen;
            Icon = AppIcon.Get(SystemInformation.IconSize);
            KeyPreview = true;
            Theme.Preference = settings.Theme;

            float sc;
            using (var gdi = CreateGraphics()) sc = gdi.DpiX / 96f;
            int iconPx = (int)Math.Round(20 * sc);
            iconSize = iconPx; uiScale = sc;

            // ---- barra principal: captura e acoes ----
            bar.GripStyle = ToolStripGripStyle.Hidden;
            bar.Padding = new Padding((int)(8 * sc), (int)(6 * sc), (int)(8 * sc), (int)(6 * sc));
            bar.ImageScalingSize = new Size(iconPx, iconPx);

            newBtn = Btn("Nova captura", "Nova captura (Ctrl+N ou o atalho global)", delegate { StartCapture(CurrentMode); }, IconKind.Capture, true);
            newBtn.Name = "primary";
            newBtn.Font = new Font("Segoe UI Semibold", 9.5f);
            bar.Items.Add(newBtn);

            var shortcuts = new string[ModeList.Names.Length];
            for (int i = 0; i < shortcuts.Length; i++) shortcuts[i] = (i + 1).ToString();
            modeBox = new ChoiceButton(ModeList.Names, delegate(int i) { return ModeList.IconOf(ModeList.Order[i]); }, shortcuts, iconPx, true, true);
            modeBox.ToolTipText = "Modo de captura (na seleção, as teclas 1 a 7 trocam de modo). Rolagem automática: o app rola sozinho; Rolagem manual: você rola.";
            Pad(modeBox, 9, 7);
            modeBox.SelectedIndex = ModeList.IndexOf((CaptureMode)settings.Mode);
            modeBox.SelectedIndexChanged += delegate { settings.Mode = (int)CurrentMode; settings.Save(); };
            bar.Items.Add(modeBox);

            delayBox = new ChoiceButton(new string[] { "Sem atraso", "1 segundo", "2 segundos", "3 segundos", "5 segundos", "10 segundos" }, null, null, iconPx, true, false);
            delayBox.Tag = IconKind.Clock;
            delayBox.ToolTipText = "Atraso antes da captura";
            Pad(delayBox, 9, 7);
            delayBox.SelectedIndex = settings.Delay;
            delayBox.SelectedIndexChanged += delegate { settings.Delay = delayBox.SelectedIndex; settings.Save(); };
            bar.Items.Add(delayBox);

            bar.Items.Add(Sep());
            undoBtn = Btn("Desfazer", "Desfazer (Ctrl+Z)", delegate { canvas.Undo(); }, IconKind.Undo, false);
            redoBtn = Btn("Refazer", "Refazer (Ctrl+Y)", delegate { canvas.Redo(); }, IconKind.Redo, false);
            copyBtn = Btn("Copiar", "Copiar para a área de transferência (Ctrl+C)", delegate { CopyImage(true); }, IconKind.Copy, true);
            saveBtn = Btn("Salvar", "Salvar como... (Ctrl+S)", delegate { SaveImage(); }, IconKind.Save, true);
            ocrBtn = Btn("Copiar texto", "Copiar texto da imagem: reconhece o texto (OCR) e copia", delegate { RunOcr(); }, IconKind.Ocr, false);
            bar.Items.AddRange(new ToolStripItem[] { undoBtn, redoBtn, Sep(), copyBtn, saveBtn, ocrBtn });
            editItems.AddRange(new ToolStripItem[] { undoBtn, redoBtn, copyBtn, saveBtn, ocrBtn });

            // itens alinhados a direita: o primeiro adicionado fica mais a direita
            settingsBtn = Btn("Configurações", "Configurações: atalho, salvamento automático, bandeja, tema e permissões da IA", delegate { OpenSettings(); }, IconKind.Settings, false);
            settingsBtn.Alignment = ToolStripItemAlignment.Right;
            bar.Items.Add(settingsBtn);

            helpBtn = Btn("Ajuda", "Guia do usuário (F1)", delegate { OpenHelp(0); }, IconKind.Help, false);
            helpBtn.Alignment = ToolStripItemAlignment.Right;
            bar.Items.Add(helpBtn);

            // ---- segunda barra: ferramentas de edicao ----
            tools.GripStyle = ToolStripGripStyle.Hidden;
            tools.Padding = new Padding((int)(8 * sc), (int)(4 * sc), (int)(8 * sc), (int)(4 * sc));
            tools.ImageScalingSize = new Size(iconPx, iconPx);
            AddTool("pen", "Caneta", IconKind.Pen);
            AddTool("highlighter", "Marca-texto", IconKind.Highlighter);
            tools.Items.Add(Sep());
            AddTool("arrow", "Seta (Shift = 45 graus)", IconKind.Arrow);
            AddTool("line", "Linha (Shift = 45 graus)", IconKind.Line);
            AddTool("rect", "Retângulo (Shift = quadrado)", IconKind.Rect);
            AddTool("ellipse", "Elipse (Shift = círculo)", IconKind.Ellipse);
            tools.Items.Add(Sep());
            AddTool("text", "Texto: clique onde quer escrever", IconKind.Text);
            AddTool("step", "Passo: marcador numerado (1, 2, 3...)", IconKind.Step);
            tools.Items.Add(Sep());
            AddTool("blur", "Desfocar uma área (esconde dados sensíveis)", IconKind.Blur);
            AddTool("pixelate", "Pixelar uma área (esconde dados sensíveis)", IconKind.Pixelate);
            tools.Items.Add(Sep());
            AddTool("crop", "Cortar a imagem", IconKind.Crop);
            AddTool("picker", "Conta-gotas: pegar uma cor da imagem", IconKind.Picker);
            AddTool("eraser", "Borracha: apaga a anotação tocada", IconKind.Eraser);
            toolBtns["pen"].Checked = true;

            tools.Items.Add(Sep());
            colorBtn = Btn("Cor", "Cor das anotações (clique para trocar)", delegate { PickColor(); }, IconKind.Dot, false);
            widthBox = new ChoiceButton(new string[] { "Fino", "Médio", "Grosso", "Extra grosso" },
                delegate(int i) { return new WidthSample(new float[] { 2, 4, 8, 14 }[i]); }, null, iconPx, false, true);
            widthBox.ToolTipText = "Espessura do traço, tamanho do texto e intensidade do desfoque";
            Pad(widthBox, 7, 7);
            widthBox.SelectedIndex = settings.PenWidth;
            widthBox.SelectedIndexChanged += delegate { ApplyWidth(); settings.PenWidth = widthBox.SelectedIndex; settings.Save(); };
            tools.Items.AddRange(new ToolStripItem[] { colorBtn, widthBox });
            editItems.AddRange(new ToolStripItem[] { colorBtn, widthBox });
            canvas.PenColor = Color.FromArgb(settings.PenColor);
            UpdateColorButton();

            // zoom, alinhado a direita (o primeiro adicionado fica mais a direita)
            var zoomFit = Btn("Ajustar", "Ajustar à janela", delegate { FitZoom(); }, IconKind.Fit, false);
            var zoomIn = Btn("Mais zoom", "Aumentar o zoom (Ctrl + roda do mouse)", delegate { canvas.SetZoom(canvas.Zoom * 1.25f); }, IconKind.ZoomIn, false);
            zoomLabel = Btn("100%", "Tamanho real (100%)", delegate { canvas.SetZoom(1f); }, IconKind.Dot, true);
            zoomLabel.Tag = null; zoomLabel.Image = null; zoomLabel.DisplayStyle = ToolStripItemDisplayStyle.Text;
            zoomLabel.AutoSize = false; zoomLabel.Width = (int)(56 * sc); zoomLabel.TextAlign = ContentAlignment.MiddleCenter;
            var zoomOut = Btn("Menos zoom", "Diminuir o zoom (Ctrl + roda do mouse)", delegate { canvas.SetZoom(canvas.Zoom / 1.25f); }, IconKind.ZoomOut, false);
            foreach (var z in new ToolStripItem[] { zoomFit, zoomIn, zoomLabel, zoomOut }) { z.Alignment = ToolStripItemAlignment.Right; tools.Items.Add(z); }
            editItems.AddRange(new ToolStripItem[] { zoomOut, zoomLabel, zoomIn, zoomFit });

            canvas.LayoutChanged += delegate
            {
                CenterCanvas();
                zoomLabel.Text = ((int)Math.Round(canvas.Zoom * 100)) + "%";
                sizeLabel.Text = canvas.Image == null ? "" : canvas.Image.Width + " × " + canvas.Image.Height + " px";
            };
            canvas.ColorPicked += delegate(Color c) { UpdateColorButton(); settings.PenColor = c.ToArgb(); settings.Save(); };
            canvas.HistoryChanged += delegate { undoBtn.Enabled = canvas.CanUndo; redoBtn.Enabled = canvas.CanRedo; };
            canvas.MouseWheel += OnCanvasWheel;

            scroller.Dock = DockStyle.Fill;
            scroller.AutoScroll = true;
            scroller.BackColor = Color.FromArgb(48, 48, 48);
            scroller.Controls.Add(canvas);
            scroller.Resize += delegate { CenterCanvas(); };
            canvas.Visible = false;

            placeholder.Dock = DockStyle.Fill;
            placeholder.NewCapture += delegate { StartCapture(CurrentMode); };
            placeholder.OpenHelp += delegate { OpenHelp(0); };
            scroller.Controls.Add(placeholder);
            UpdatePlaceholder();

            status.Items.Add(statusLabel);
            status.Items.Add(sizeLabel);
            statusLabel.Text = "Pronto";
            statusLabel.Spring = true; statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            sizeLabel.Alignment = ToolStripItemAlignment.Right;
            status.Padding = new Padding((int)(6 * sc), 0, (int)(6 * sc), 0);
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
            if (tray.ContextMenuStrip != null) Theme.Apply(tray.ContextMenuStrip, dark);   // o menu da bandeja tambem segue o tema
            scroller.BackColor = dark ? Color.FromArgb(24, 24, 24) : Color.FromArgb(48, 48, 48);
            Theme.NativeTheme(scroller, true);   // o fundo da area de edicao e escuro nos dois temas: barras de rolagem escuras
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

        void TrayItem(ContextMenuStrip menu, string text, IconKind icon, EventHandler click)
        {
            var item = new ToolStripMenuItem(text);
            item.Tag = icon;
            item.Image = Icons.Placeholder(iconSize);
            item.ImageScaling = ToolStripItemImageScaling.None;
            item.Padding = new Padding((int)(4 * uiScale), (int)(5 * uiScale), (int)(4 * uiScale), (int)(5 * uiScale));
            item.Click += click;
            menu.Items.Add(item);
        }

        void BuildTray()
        {
            var menu = new ContextMenuStrip();
            menu.ImageScalingSize = new Size(iconSize, iconSize);
            TrayItem(menu, "Abrir a Ferramenta de Captura", IconKind.Capture, delegate { ShowMain(); });
            menu.Items.Add(new ToolStripSeparator());
            TrayItem(menu, "Capturar retângulo", IconKind.ModeRect, delegate { StartCapture(CaptureMode.Rectangle); });
            TrayItem(menu, "Capturar forma livre", IconKind.ModeFree, delegate { StartCapture(CaptureMode.Freeform); });
            TrayItem(menu, "Capturar janela", IconKind.ModeWindow, delegate { StartCapture(CaptureMode.Window); });
            TrayItem(menu, "Capturar tela inteira", IconKind.ModeScreen, delegate { StartCapture(CaptureMode.FullScreen); });
            TrayItem(menu, "Captura com rolagem automática", IconKind.ScrollAuto, delegate { StartCapture(CaptureMode.Scroll); });
            TrayItem(menu, "Captura com rolagem manual", IconKind.ScrollManual, delegate { StartCapture(CaptureMode.ScrollManual); });
            TrayItem(menu, "Gravar GIF", IconKind.Gif, delegate { StartCapture(CaptureMode.Record); });
            menu.Items.Add(new ToolStripSeparator());
            TrayItem(menu, "Configurações", IconKind.Settings, delegate { ShowMain(); OpenSettings(); });
            TrayItem(menu, "Ajuda (F1)", IconKind.Help, delegate { ShowMain(); OpenHelp(0); });
            TrayItem(menu, "Sair", IconKind.Close, delegate { exiting = true; Close(); });
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
            placeholder.SetHotkey(HotkeyLabel());
        }

        // ------------------------------------------------------------ Barras

        // Botao de barra com icone vetorial (desenhado pelo AppRenderer); showText mostra o texto ao lado do icone.
        ToolStripButton Btn(string text, string tip, EventHandler click, IconKind icon, bool showText)
        {
            var b = new ToolStripButton(text);
            b.ToolTipText = tip;
            b.AccessibleName = text;
            b.Tag = icon;
            b.Image = Icons.Placeholder(iconSize);
            b.ImageScaling = ToolStripItemImageScaling.None;
            b.DisplayStyle = showText ? ToolStripItemDisplayStyle.ImageAndText : ToolStripItemDisplayStyle.Image;
            b.TextImageRelation = TextImageRelation.ImageBeforeText;
            Pad(b, showText ? 9 : 5, 7);
            if (click != null) b.Click += click;
            return b;
        }

        // margem interna proporcional a escala: botoes de barra com pelo menos 32 px de altura
        void Pad(ToolStripItem item, int h, int v)
        {
            item.Padding = new Padding((int)(h * uiScale), (int)(v * uiScale), (int)(h * uiScale), (int)(v * uiScale));
            item.Margin = new Padding((int)(1 * uiScale), (int)(1 * uiScale), (int)(1 * uiScale), (int)(1 * uiScale));
        }

        ToolStripSeparator Sep()
        {
            var s = new ToolStripSeparator();
            s.Margin = new Padding((int)(4 * uiScale), 0, (int)(4 * uiScale), 0);
            return s;
        }

        void AddTool(string tool, string tip, IconKind icon)
        {
            var b = Btn(tip, tip, null, icon, false);
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
            colorBtn.ForeColor = canvas.PenColor;   // o icone "Dot" usa a cor do item
        }

        void OpenHelp(int topic)
        {
            if (helpForm == null || helpForm.IsDisposed)
            {
                helpForm = new HelpForm(topic);
                helpForm.Show(this);
            }
            else { if (helpForm.WindowState == FormWindowState.Minimized) helpForm.WindowState = FormWindowState.Normal; helpForm.Activate(); }
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
                                  : "Atalho " + HotkeyLabel() + " indisponível (em uso por outro programa). Escolha outro em Configurações.";
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
            if (e.KeyCode == Keys.F1) { OpenHelp(0); e.Handled = true; }
            else if (e.Control && e.KeyCode == Keys.N) { StartCapture(CurrentMode); e.Handled = true; }
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
                                statusLabel.Text = "A rolagem automática não moveu o conteúdo. Tente o modo \"Rolagem manual\" na lista de modos.";
                                Notify("Rolagem automática sem efeito", "O conteúdo não rolou. Use o modo \"Rolagem manual\" e role você mesmo.", ShowMain);
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
                        d.Title = "Salvar gravação";
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
                MessageBox.Show("Não foi possível salvar o GIF:\n" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            string msg = "Captura: " + bmp.Width + " x " + bmp.Height + " px  -  copiada para a área de transferência";
            string saved = settings.AutoSave ? AutoSaveCurrent() : null;
            if (saved != null) msg += "  -  salva em " + saved;
            statusLabel.Text = msg;
            if (saved != null) Notify("Captura salva", "Copiada e salva em " + saved + ". Clique para abrir.", delegate { OpenPath(saved); });
            else Notify("Captura copiada", "Ela está na área de transferência. Clique para editar.", ShowMain);
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
                statusLabel.Text = "Falha no salvamento automático: " + ex.Message;
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
                if (announce) statusLabel.Text = "Copiado para a área de transferência";
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
                    MessageBox.Show("Não foi possível salvar:\n" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}
