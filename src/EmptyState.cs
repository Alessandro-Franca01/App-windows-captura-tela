using System;
using System.Drawing;
using System.Windows.Forms;

namespace CapturaTela
{
    // Estado vazio da janela principal: logo, frase de acolhimento, o botao da acao principal e o atalho da Ajuda.
    // Fica sobre o fundo escuro da area de edicao, nos dois temas.
    class EmptyState : Panel
    {
        static readonly Color InkOnWell = Color.FromArgb(0xE6, 0xE6, 0xE6);
        static readonly Color MutedOnWell = Color.FromArgb(0xB4, 0xB4, 0xB4);

        readonly PictureBox logo = new PictureBox();
        readonly Label title = new Label();
        readonly Label hint = new Label();
        readonly Button primary = new Button();
        readonly Button helpLink = new Button();
        float sc = 1f;

        public event EventHandler NewCapture;
        public event EventHandler OpenHelp;

        public EmptyState()
        {
            DoubleBuffered = true;
            using (var g = CreateGraphics()) sc = g.DpiX / 96f;

            logo.SizeMode = PictureBoxSizeMode.Zoom;
            logo.BackColor = Color.Transparent;
            int px = (int)Math.Round(72 * sc);
            logo.Size = new Size(px, px);
            using (var ico = AppIcon.Get(new Size(64, 64))) logo.Image = ico.ToBitmap();

            title.Text = "Pronto para capturar";
            title.Font = new Font("Segoe UI Semibold", 16f);
            title.ForeColor = InkOnWell; title.BackColor = Color.Transparent;
            title.TextAlign = ContentAlignment.MiddleCenter; title.AutoSize = false;

            hint.Font = new Font("Segoe UI", 10.5f);
            hint.ForeColor = MutedOnWell; hint.BackColor = Color.Transparent;
            hint.TextAlign = ContentAlignment.TopCenter; hint.AutoSize = false;

            primary.Text = "Nova captura";
            primary.Font = new Font("Segoe UI Semibold", 10.5f);
            primary.FlatStyle = FlatStyle.Flat;
            primary.FlatAppearance.BorderSize = 0;
            primary.BackColor = Theme.AccentStrong; primary.ForeColor = Color.White;
            primary.FlatAppearance.MouseOverBackColor = Color.FromArgb(0x19, 0x75, 0xC8);
            primary.FlatAppearance.MouseDownBackColor = Color.FromArgb(0x00, 0x55, 0xA0);
            primary.Cursor = Cursors.Hand;
            primary.Size = new Size((int)(210 * sc), (int)(42 * sc));
            primary.Click += delegate { if (NewCapture != null) NewCapture(this, EventArgs.Empty); };

            helpLink.Text = "Ver o guia de uso (F1)";
            helpLink.Font = new Font("Segoe UI", 10f);
            helpLink.FlatStyle = FlatStyle.Flat;
            helpLink.FlatAppearance.BorderSize = 0;
            helpLink.BackColor = Color.Transparent; helpLink.ForeColor = Color.FromArgb(0x4C, 0xC2, 0xFF);
            helpLink.FlatAppearance.MouseOverBackColor = Color.FromArgb(0x38, 0x38, 0x38);
            helpLink.FlatAppearance.MouseDownBackColor = Color.FromArgb(0x44, 0x44, 0x44);
            helpLink.Cursor = Cursors.Hand;
            helpLink.Size = new Size((int)(210 * sc), (int)(34 * sc));
            helpLink.Click += delegate { if (OpenHelp != null) OpenHelp(this, EventArgs.Empty); };

            Controls.AddRange(new Control[] { logo, title, hint, primary, helpLink });
            SetHotkey("Print Screen");
        }

        public void SetHotkey(string label)
        {
            hint.Text = "Aperte " + label + " (ou Ctrl+Shift+S) de qualquer lugar,\nou clique no botão abaixo.";
            PerformLayout();
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            int w = (int)(460 * sc);
            int gap = (int)(10 * sc);
            title.Size = new Size(w, (int)(34 * sc));
            hint.Size = new Size(w, (int)(48 * sc));
            int total = logo.Height + gap + title.Height + hint.Height + (int)(18 * sc) + primary.Height + gap + helpLink.Height;
            int y = Math.Max(0, (ClientSize.Height - total) / 2);
            Action<Control> place = delegate(Control c)
            {
                c.Left = (ClientSize.Width - c.Width) / 2; c.Top = y; y += c.Height;
            };
            place(logo); y += gap;
            place(title);
            place(hint); y += (int)(18 * sc);
            place(primary); y += gap;
            place(helpLink);
        }
    }
}
