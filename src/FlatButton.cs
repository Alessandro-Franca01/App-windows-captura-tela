using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CapturaTela
{
    enum ButtonVariant
    {
        Normal,     // contorno + fundo elevado
        Primary,    // azul cheio: a acao principal da tela
        Danger,     // vermelho cheio: acao de gravar/negar com peso
        Subtle      // so texto colorido, sem borda (acoes secundarias)
    }

    // Botao dos dialogos: cantos arredondados, estados (hover, pressionado, foco, desativado) e as cores do tema.
    // E um Button de verdade (DialogResult, PerformClick, teclado), so que desenhado pelo app.
    class FlatButton : Button
    {
        bool hover, pressed;

        public ButtonVariant Variant = ButtonVariant.Normal;
        public IconKind? Icon;

        public FlatButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            Cursor = Cursors.Hand;
            Height = 32;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        static Color Lighten(Color c) { return Color.FromArgb(c.A, Math.Min(255, c.R + 24), Math.Min(255, c.G + 24), Math.Min(255, c.B + 24)); }
        static Color Darken(Color c) { return Color.FromArgb(c.A, Math.Max(0, c.R - 22), Math.Max(0, c.G - 22), Math.Max(0, c.B - 22)); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float sc = g.DpiX / 96f;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Surface);

            Color fill, border, ink;
            switch (Variant)
            {
                case ButtonVariant.Primary:
                    fill = !Enabled ? Theme.Pressed : pressed ? Darken(Theme.AccentStrong) : hover ? Lighten(Theme.AccentStrong) : Theme.AccentStrong;
                    border = fill; ink = Enabled ? Color.White : Theme.DisabledInk;
                    break;
                case ButtonVariant.Danger:
                    Color red = Theme.IsDark ? Color.FromArgb(0xC4, 0x2B, 0x1C) : Color.FromArgb(0xB4, 0x23, 0x18);
                    fill = !Enabled ? Theme.Pressed : pressed ? Darken(red) : hover ? Lighten(red) : red;
                    border = fill; ink = Enabled ? Color.White : Theme.DisabledInk;
                    break;
                case ButtonVariant.Subtle:
                    fill = !Enabled ? Color.Transparent : pressed ? Theme.Pressed : hover ? Theme.Hover : Color.Transparent;
                    border = Color.Transparent; ink = Enabled ? Theme.AccentText : Theme.DisabledInk;
                    break;
                default:
                    fill = !Enabled ? Theme.Surface : pressed ? Theme.Pressed : hover ? Theme.Hover : Theme.SurfaceRaised;
                    border = Theme.IsDark ? Color.FromArgb(0x55, 0x55, 0x55) : Color.FromArgb(0xC8, 0xC8, 0xC8);
                    ink = Enabled ? Theme.Ink : Theme.DisabledInk;
                    break;
            }

            var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (var path = AppRenderer.RoundPath(rect, 4f * sc))
            {
                if (fill.A > 0) using (var b = new SolidBrush(fill)) g.FillPath(b, path);
                if (border.A > 0 && border != fill) using (var p = new Pen(border, 1f)) g.DrawPath(p, path);
            }

            // icone + texto, centralizados
            int iconPx = Icon.HasValue ? (int)Math.Round(18 * sc) : 0;
            int gap = Icon.HasValue ? (int)Math.Round(6 * sc) : 0;
            var flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis;
            var textSize = TextRenderer.MeasureText(g, Text, Font, new Size(Width, Height), flags);
            int total = iconPx + gap + textSize.Width;
            int x = Math.Max(0, (Width - total) / 2);
            if (Icon.HasValue) Icons.Draw(g, Icon.Value, new Rectangle(x, (Height - iconPx) / 2, iconPx, iconPx), ink);
            var tr = new Rectangle(x + iconPx + gap, 0, Math.Max(1, Width - x - iconPx - gap), Height);
            TextRenderer.DrawText(g, Text, Font, tr, ink, flags | TextFormatFlags.VerticalCenter | TextFormatFlags.Left);

            // foco visivel (teclado)
            if (Focused && ShowFocusCues)
                using (var path = AppRenderer.RoundPath(new RectangleF(2.5f * sc, 2.5f * sc, Width - 5f * sc, Height - 5f * sc), 3f * sc))
                using (var p = new Pen(Variant == ButtonVariant.Normal || Variant == ButtonVariant.Subtle ? Theme.AccentText : Color.White, 1.5f))
                    g.DrawPath(p, path);
        }
    }
}
