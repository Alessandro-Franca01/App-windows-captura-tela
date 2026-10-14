using System;
using System.Drawing;
using System.Windows.Forms;

namespace CapturaTela
{
    // Sessao em andamento que o atalho global pode encerrar (gravacao de GIF, rolagem manual).
    interface IStoppable
    {
        void RequestStop();
    }

    // Moldura vermelha ao redor da area capturada. Fica FORA da area (nao aparece na imagem),
    // nao recebe cliques nem foco e, quando o Windows permite, e excluida de qualquer captura.
    class RegionFrame : Form
    {
        readonly int border;

        public RegionFrame(Rectangle r, int border)
        {
            this.border = border;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.Magenta;
            TransparencyKey = Color.Magenta;
            var b = r; b.Inflate(border, border);
            Bounds = b;
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x80000 | 0x20 | 0x80 | 0x08000000; // layered, click-through, toolwindow, noactivate
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.ExcludeFromCapture(Handle);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            using (var br = new SolidBrush(Color.Red))
            {
                e.Graphics.FillRectangle(br, 0, 0, Width, border);
                e.Graphics.FillRectangle(br, 0, Height - border, Width, border);
                e.Graphics.FillRectangle(br, 0, 0, border, Height);
                e.Graphics.FillRectangle(br, Width - border, 0, border, Height);
            }
        }

        // Onde colocar um painel de controle: abaixo da area; se nao couber, acima; por ultimo, dentro dela
        // (nesse caso so nao aparece na imagem se ExcludeFromCapture funcionar).
        public static Point PanelLocation(Rectangle region, Size panel, int border)
        {
            var wa = Screen.FromRectangle(region).WorkingArea;
            int x = Math.Max(wa.Left, Math.Min(region.Left, wa.Right - panel.Width));
            int below = region.Bottom + border + 8;
            int above = region.Top - border - 8 - panel.Height;
            int y = below + panel.Height <= wa.Bottom ? below : above >= wa.Top ? above : wa.Bottom - panel.Height - 8;
            return new Point(x, y);
        }
    }
}
