using System;
using System.Drawing;
using System.Media;
using System.Windows.Forms;

namespace CapturaTela
{
    // Janela que pergunta ao usuario se uma IA pode fazer algo. Sem resposta em 60 s = negar (so desta vez).
    class PermissionDialog : Form
    {
        public const int TimeoutSeconds = 60;

        public PromptAnswer Answer = PromptAnswer.Deny;

        readonly Label countdown = new Label();
        readonly Timer timer = new Timer();
        int left;

        public readonly Button OnceBtn = new Button(), AlwaysBtn = new Button(), DenyBtn = new Button(), DenyAlwaysBtn = new Button();

        // armMs: os botoes so ficam ativos depois desse tempo, para que uma tecla digitada em outra janela
        // (ex.: Enter) nao aprove o pedido por acidente quando o dialogo roubar o foco.
        public const int DefaultArmMs = 1200;

        public PermissionDialog(PromptRequest req) : this(req, DefaultArmMs) { }

        public PermissionDialog(PromptRequest req, int armMs) : this(req, armMs, TimeoutSeconds) { }

        public PermissionDialog(PromptRequest req, int armMs, int timeoutSeconds)
        {
            left = timeoutSeconds;
            Text = "Pedido de permissão";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;
            ShowInTaskbar = true;
            ClientSize = new Size(560, 236);
            Font = new Font("Segoe UI", 9.5f);
            Icon = AppIcon.Get(SystemInformation.SmallIconSize);

            var who = new Label { Text = req.Client + " quer:", Left = 20, Top = 18, Width = 520, Height = 22 };
            var what = new Label
            {
                Text = req.Description, Left = 20, Top = 44, Width = 520, Height = 70,
                Font = new Font("Segoe UI Semibold", 12f), AutoEllipsis = false
            };
            var hint = new Label
            {
                Text = "\"Sempre\" fica salvo e pode ser revogado em Configurações > Permissões da IA. " +
                       "\"Uma vez\" vale só para este pedido.",
                Left = 20, Top = 118, Width = 520, Height = 40, ForeColor = Theme.Muted
            };
            countdown.Left = 20; countdown.Top = 160; countdown.Width = 520; countdown.ForeColor = Theme.Muted;

            SetupButton(OnceBtn, "Permitir uma vez", 20, PromptAnswer.Once);
            SetupButton(AlwaysBtn, "Permitir sempre", 164, PromptAnswer.Always);
            SetupButton(DenyBtn, "Negar", 308, PromptAnswer.Deny);
            SetupButton(DenyAlwaysBtn, "Negar sempre", 396, PromptAnswer.DenyAlways);
            OnceBtn.Width = 136; AlwaysBtn.Width = 136; DenyBtn.Width = 80; DenyAlwaysBtn.Width = 128;
            AcceptButton = null;
            CancelButton = DenyBtn;

            Controls.AddRange(new Control[] { who, what, hint, countdown, OnceBtn, AlwaysBtn, DenyBtn, DenyAlwaysBtn });
            Theme.ScaleForDpi(this);
            Theme.Apply(this);
            what.ForeColor = Theme.IsDark ? Theme.DarkText : SystemColors.ControlText;

            UpdateCountdown();
            timer.Interval = 1000;
            timer.Tick += delegate
            {
                left--;
                UpdateCountdown();
                if (left <= 0) { Answer = PromptAnswer.Deny; Close(); }
            };
            foreach (var b in Buttons()) b.Enabled = armMs <= 0;
            Shown += delegate
            {
                timer.Start(); Activate();
                DenyBtn.Select();   // o foco comeca em "Negar": o caminho seguro
                if (armMs <= 0) return;
                var arm = new Timer { Interval = armMs };
                arm.Tick += delegate { arm.Stop(); arm.Dispose(); foreach (var b in Buttons()) b.Enabled = true; DenyBtn.Select(); };
                arm.Start();
            };
        }

        Button[] Buttons() { return new Button[] { OnceBtn, AlwaysBtn, DenyBtn, DenyAlwaysBtn }; }

        void SetupButton(Button b, string text, int x, PromptAnswer answer)
        {
            b.Text = text; b.Left = x; b.Top = 186; b.Width = 100; b.Height = 34;
            b.Click += delegate { Answer = answer; Close(); };
        }

        void UpdateCountdown()
        {
            countdown.Text = "Negando automaticamente em " + left + " s se não houver resposta.";
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            timer.Stop(); timer.Dispose();
            base.OnFormClosed(e);
        }

        public static PromptAnswer Ask(PromptRequest req)
        {
            SystemSounds.Asterisk.Play();
            using (var d = new PermissionDialog(req))
            {
                d.ShowDialog();
                return d.Answer;
            }
        }
    }
}
