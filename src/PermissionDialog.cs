using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Media;
using System.Windows.Forms;

namespace CapturaTela
{
    // Janela que pergunta ao usuario se uma IA pode fazer algo. Sem resposta em 60 s = negar (so desta vez).
    // Hierarquia: "Permitir uma vez" e o caminho principal; "Negar" e o secundario; "sempre" fica em segundo plano.
    class PermissionDialog : Form
    {
        public const int TimeoutSeconds = 60;

        public PromptAnswer Answer = PromptAnswer.Deny;

        readonly Timer timer = new Timer();
        readonly Label countdown = new Label();
        readonly TimeBar bar = new TimeBar();
        int left;
        readonly int total;

        public readonly FlatButton OnceBtn = new FlatButton(), AlwaysBtn = new FlatButton(), DenyBtn = new FlatButton(), DenyAlwaysBtn = new FlatButton();

        // armMs: os botoes so ficam ativos depois desse tempo, para que uma tecla digitada em outra janela
        // (ex.: Enter) nao aprove o pedido por acidente quando o dialogo roubar o foco.
        public const int DefaultArmMs = 1200;

        public PermissionDialog(PromptRequest req) : this(req, DefaultArmMs) { }

        public PermissionDialog(PromptRequest req, int armMs) : this(req, armMs, TimeoutSeconds) { }

        public PermissionDialog(PromptRequest req, int armMs, int timeoutSeconds)
        {
            left = total = Math.Max(1, timeoutSeconds);
            Text = "Pedido de permissão";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;
            ShowInTaskbar = true;
            ClientSize = new Size(600, 292);
            Font = new Font("Segoe UI", 9.5f);
            Icon = AppIcon.Get(SystemInformation.SmallIconSize);

            var shield = new IconBox(IconKind.Shield) { Left = 22, Top = 20, Width = 32, Height = 32 };
            var who = new Label { Text = req.Client + " quer:", Left = 66, Top = 18, Width = 510, Height = 22, ForeColor = Theme.Muted };
            var what = new Label
            {
                Text = req.Description, Left = 66, Top = 42, Width = 510, Height = 66,
                Font = new Font("Segoe UI Semibold", 12.5f)
            };

            var level = PermissionRisk.Of(req.Key);
            var chip = new RiskChip(level) { Left = 22, Top = 118, Width = 132, Height = 28 };
            var explain = new Label { Text = PermissionRisk.Explain(req.Key), Left = 164, Top = 120, Width = 412, Height = 36, ForeColor = Theme.Muted };

            countdown.Left = 22; countdown.Top = 166; countdown.Width = 554; countdown.Height = 20; countdown.ForeColor = Theme.Muted;
            bar.Left = 22; bar.Top = 188; bar.Width = 556; bar.Height = 6;
            var hint = new Label
            {
                Text = "\"Sempre\" fica salvo e pode ser revogado em Configurações > Permissões da IA.",
                Left = 22, Top = 204, Width = 554, Height = 20, ForeColor = Theme.Muted
            };

            Setup(OnceBtn, "Permitir uma vez", 22, 238, 160, ButtonVariant.Primary, PromptAnswer.Once);
            Setup(DenyBtn, "Negar", 192, 238, 100, ButtonVariant.Normal, PromptAnswer.Deny);
            Setup(AlwaysBtn, "Permitir sempre", 332, 238, 126, ButtonVariant.Subtle, PromptAnswer.Always);
            Setup(DenyAlwaysBtn, "Negar sempre", 462, 238, 116, ButtonVariant.Subtle, PromptAnswer.DenyAlways);
            AcceptButton = null;
            CancelButton = DenyBtn;

            Controls.AddRange(new Control[] { shield, who, what, chip, explain, countdown, bar, hint, OnceBtn, DenyBtn, AlwaysBtn, DenyAlwaysBtn });
            Theme.ScaleForDpi(this);
            Theme.Apply(this);
            what.ForeColor = Theme.Ink;
            who.ForeColor = Theme.Muted; explain.ForeColor = Theme.Muted; countdown.ForeColor = Theme.Muted; hint.ForeColor = Theme.Muted;

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
                DenyBtn.Select();   // o foco começa em "Negar": o caminho seguro
                if (armMs <= 0) return;
                var arm = new Timer { Interval = armMs };
                arm.Tick += delegate { arm.Stop(); arm.Dispose(); foreach (var b in Buttons()) b.Enabled = true; DenyBtn.Select(); };
                arm.Start();
            };
        }

        Button[] Buttons() { return new Button[] { OnceBtn, AlwaysBtn, DenyBtn, DenyAlwaysBtn }; }

        void Setup(FlatButton b, string text, int x, int y, int w, ButtonVariant variant, PromptAnswer answer)
        {
            b.Text = text; b.Left = x; b.Top = y; b.Width = w; b.Height = 36;
            b.Variant = variant;
            b.Font = new Font("Segoe UI", 9.5f, variant == ButtonVariant.Primary ? FontStyle.Bold : FontStyle.Regular);
            b.Click += delegate { Answer = answer; Close(); };
        }

        void UpdateCountdown()
        {
            countdown.Text = "Negando automaticamente em " + left + " s se não houver resposta.";
            bar.Fraction = (float)left / total;
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

        // ---- pecas visuais ----

        // icone desenhado no fundo do formulario
        class IconBox : Control
        {
            readonly IconKind kind;
            public IconBox(IconKind kind) { this.kind = kind; SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer, true); BackColor = Color.Transparent; }
            protected override void OnPaint(PaintEventArgs e) { Icons.Draw(e.Graphics, kind, ClientRectangle, Theme.AccentText); }
        }

        // faixa fina com o tempo que falta
        class TimeBar : Control
        {
            float fraction = 1f;
            public TimeBar() { SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); }
            public float Fraction { get { return fraction; } set { fraction = Math.Max(0f, Math.Min(1f, value)); Invalidate(); } }
            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Parent != null ? Parent.BackColor : Theme.Surface);
                float h = Height - 1;
                using (var track = AppRenderer.RoundPath(new RectangleF(0, 0, Width - 1, h), h / 2))
                using (var tb = new SolidBrush(Theme.Pressed)) g.FillPath(tb, track);
                if (fraction <= 0) return;
                using (var fill = AppRenderer.RoundPath(new RectangleF(0, 0, Math.Max(h, (Width - 1) * fraction), h), h / 2))
                using (var fb = new SolidBrush(fraction > 0.25f ? Theme.AccentText : Theme.Danger)) g.FillPath(fb, fill);
            }
        }

        // "Risco baixo/médio/alto": bolinha colorida + texto (nunca so cor)
        class RiskChip : Control
        {
            readonly RiskLevel level;
            public RiskChip(RiskLevel level) { this.level = level; SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); }
            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Parent != null ? Parent.BackColor : Theme.Surface);
                Color tone = level == RiskLevel.Low ? Theme.Success : level == RiskLevel.High ? Theme.Danger : Theme.Warning;
                using (var path = AppRenderer.RoundPath(new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), (Height - 1.5f) / 2))
                {
                    using (var b = new SolidBrush(Color.FromArgb(38, tone))) g.FillPath(b, path);
                    using (var p = new Pen(Color.FromArgb(120, tone), 1f)) g.DrawPath(p, path);
                }
                float d = Height * 0.30f;
                using (var b = new SolidBrush(tone)) g.FillEllipse(b, Height * 0.42f, (Height - d) / 2f, d, d);
                using (var f = new Font("Segoe UI Semibold", 9f))
                    TextRenderer.DrawText(g, PermissionRisk.Label(level), f, new Rectangle((int)(Height * 0.42f + d + 6), 0, Width, Height), Theme.Ink,
                        TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding);
            }
        }
    }
}
