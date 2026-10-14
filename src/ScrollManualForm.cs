using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace CapturaTela
{
    // Painel da rolagem manual: o usuario rola o conteudo com a roda do mouse e o app vai
    // fotografando a area e costurando. Concluir (ou o atalho global) termina; Cancelar descarta.
    class ScrollManualForm : Form, IStoppable
    {
        const int FrameBorder = 3, SampleMs = 150;

        public Bitmap Result;       // preenchido ao concluir

        readonly Rectangle region;
        readonly RegionFrame frame;
        readonly ScrollSession session = new ScrollSession();
        readonly Timer timer = new Timer();
        readonly Label info = new Label();
        readonly Label hint = new Label();
        readonly FlatButton doneBtn = new FlatButton();
        readonly FlatButton cancelBtn = new FlatButton();

        public ScrollManualForm(Rectangle region)
        {
            this.region = region;
            Text = "Rolagem manual";
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            ClientSize = new Size(440, 128);
            Font = new Font("Segoe UI", 9.5f);
            KeyPreview = true;

            info.Left = 14; info.Top = 12; info.Width = 412; info.Height = 22; info.Font = new Font("Segoe UI Semibold", 10f);
            hint.Left = 14; hint.Top = 38; hint.Width = 412; hint.Height = 42; hint.ForeColor = Theme.Muted;
            doneBtn.Text = "Concluir"; doneBtn.Left = 14; doneBtn.Top = 84; doneBtn.Width = 140; doneBtn.Height = 36; doneBtn.Variant = ButtonVariant.Primary;
            cancelBtn.Text = "Cancelar"; cancelBtn.Left = 162; cancelBtn.Top = 84; cancelBtn.Width = 120; cancelBtn.Height = 36;
            doneBtn.Click += delegate { Finish(true); };
            cancelBtn.Click += delegate { Finish(false); };
            Controls.AddRange(new Control[] { info, hint, doneBtn, cancelBtn });

            frame = new RegionFrame(region, FrameBorder);
            Location = RegionFrame.PanelLocation(region, Size, FrameBorder);
            timer.Interval = SampleMs;
            timer.Tick += delegate { Tick(); };
            Theme.ScaleForDpi(this);
            Theme.Apply(this);
            hint.ForeColor = Theme.Muted;
            UpdateInfo();
        }

        protected override bool ShowWithoutActivation { get { return true; } }   // o foco continua na janela que rola

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.ExcludeFromCapture(Handle);   // o painel nunca entra na imagem
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            frame.Show();
            Tick();             // primeiro quadro
            timer.Start();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            timer.Stop(); timer.Dispose();
            frame.Close();
            session.Dispose();
            base.OnFormClosed(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) Finish(false);
            else if (e.KeyCode == Keys.Enter) Finish(true);
            base.OnKeyDown(e);
        }

        public void RequestStop() { Finish(true); }

        void Tick()
        {
            var bmp = new Bitmap(region.Width, region.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
                g.CopyFromScreen(region.Location, Point.Empty, region.Size, CopyPixelOperation.SourceCopy);
            session.Sample(bmp);
            UpdateInfo();
            if (session.Full) Finish(true);
        }

        void UpdateInfo()
        {
            info.Text = "Capturado: " + session.Height + " px de altura";
            switch (session.State)
            {
                // cada estado tem simbolo + texto + cor (nunca so cor)
                case ScrollState.Moving: hint.Text = "… Aguardando o conteúdo parar de se mexer."; hint.ForeColor = Theme.Muted; break;
                case ScrollState.Captured: hint.Text = "✓ Trecho novo capturado. Continue rolando para baixo."; hint.ForeColor = Theme.Success; break;
                case ScrollState.Above: hint.Text = "↑ Você voltou para cima: role para baixo até passar do que já foi capturado."; hint.ForeColor = Theme.Warning; break;
                case ScrollState.Lost: hint.Text = "⚠ Rolou demais de uma vez e perdi o encaixe. Volte um pouco para cima, até ver de novo a parte já capturada."; hint.ForeColor = Theme.Danger; break;
                default: hint.Text = "Role o conteúdo com a roda do mouse, devagar. Clique em Concluir (ou use o atalho) no fim."; hint.ForeColor = Theme.Muted; break;
            }
        }

        void Finish(bool keep)
        {
            if (keep)
            {
                Result = session.Build();
                DialogResult = Result != null ? DialogResult.OK : DialogResult.Cancel;
            }
            else DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
