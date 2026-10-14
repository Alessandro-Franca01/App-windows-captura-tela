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
        readonly Button doneBtn = new Button();
        readonly Button cancelBtn = new Button();

        public ScrollManualForm(Rectangle region)
        {
            this.region = region;
            Text = "Rolagem manual";
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            ClientSize = new Size(420, 112);
            Font = new Font("Segoe UI", 9);
            KeyPreview = true;

            info.Left = 12; info.Top = 10; info.Width = 396; info.Height = 20; info.Font = new Font("Segoe UI Semibold", 9.5f);
            hint.Left = 12; hint.Top = 32; hint.Width = 396; hint.Height = 34; hint.ForeColor = Theme.Muted;
            doneBtn.Text = "Concluir"; doneBtn.Left = 12; doneBtn.Top = 72; doneBtn.Width = 130; doneBtn.Height = 32;
            cancelBtn.Text = "Cancelar"; cancelBtn.Left = 150; cancelBtn.Top = 72; cancelBtn.Width = 110; cancelBtn.Height = 32;
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
                case ScrollState.Moving: hint.Text = "Aguardando o conteúdo parar de se mexer..."; break;
                case ScrollState.Captured: hint.Text = "Trecho novo capturado. Continue rolando para baixo."; break;
                case ScrollState.Above: hint.Text = "Você voltou para cima: role para baixo até passar do que já foi capturado."; break;
                case ScrollState.Lost: hint.Text = "Rolou demais de uma vez e perdi o encaixe. Volte um pouco para cima, até ver de novo a parte já capturada."; break;
                default: hint.Text = "Role o conteúdo com a roda do mouse, devagar. Clique em Concluir (ou use o atalho) no fim."; break;
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
