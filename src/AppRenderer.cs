using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CapturaTela
{
    // Desenha barras de ferramentas, menus e botoes do app: cantos arredondados, estados claros
    // (hover, pressionado, selecionado), icones vetoriais e as cores do tema (tokens em Theme).
    // Um item pode ter como Tag um IconKind (icone) ou um WidthSample (amostra de espessura).
    class AppRenderer : ToolStripProfessionalRenderer
    {
        public AppRenderer() : base(new ProfessionalColorTable()) { RoundedEdges = false; }

        static float Scale(Graphics g) { return g.DpiX / 96f; }

        public static GraphicsPath RoundPath(RectangleF r, float radius)
        {
            var path = new GraphicsPath();
            float d = Math.Max(0.1f, radius * 2);
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        static bool IsPrimary(ToolStripItem item) { return item.Name == "primary"; }
        static bool IsOn(ToolStripItem item)
        {
            var b = item as ToolStripButton;
            return b != null && b.Checked;
        }

        static Color Lighten(Color c) { return Color.FromArgb(c.A, Math.Min(255, c.R + 24), Math.Min(255, c.G + 24), Math.Min(255, c.B + 24)); }
        static Color Darken(Color c) { return Color.FromArgb(c.A, Math.Max(0, c.R - 22), Math.Max(0, c.G - 22), Math.Max(0, c.B - 22)); }

        // ---------------------------------------------------------------- fundos

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            Color back = e.ToolStrip is ToolStripDropDown ? Theme.MenuBack : Theme.BarBack;
            using (var b = new SolidBrush(back)) e.Graphics.FillRectangle(b, e.AffectedBounds);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            var g = e.Graphics;
            var ts = e.ToolStrip;
            if (ts is ToolStripDropDown)
            {
                using (var p = new Pen(Theme.MenuBorder)) g.DrawRectangle(p, 0, 0, ts.Width - 1, ts.Height - 1);
            }
            else if (ts is StatusStrip)
            {
                using (var p = new Pen(Theme.BarBorder)) g.DrawLine(p, 0, 0, ts.Width, 0);
            }
            else
            {
                using (var p = new Pen(Theme.BarBorder)) g.DrawLine(p, 0, ts.Height - 1, ts.Width, ts.Height - 1);
            }
        }

        protected override void OnRenderStatusStripSizingGrip(ToolStripRenderEventArgs e) { }
        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }

        void DrawButtonBack(ToolStripItemRenderEventArgs e)
        {
            var item = e.Item;
            if (!item.Enabled && !IsPrimary(item)) return;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float sc = Scale(g);
            Color fill = Color.Empty;
            if (IsPrimary(item)) fill = !item.Enabled ? Theme.Pressed : item.Pressed ? Darken(Theme.AccentStrong) : item.Selected ? Lighten(Theme.AccentStrong) : Theme.AccentStrong;
            else if (IsOn(item)) fill = item.Selected ? Lighten(Theme.AccentStrong) : Theme.AccentStrong;
            else if (item.Pressed) fill = Theme.Pressed;
            else if (item.Selected) fill = Theme.Hover;
            if (fill.IsEmpty) return;
            var r = new RectangleF(1.5f * sc, 2f * sc, item.Width - 3f * sc, item.Height - 4f * sc);
            using (var path = RoundPath(r, 4f * sc))
            using (var b = new SolidBrush(fill))
                g.FillPath(b, path);
        }

        protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e) { DrawButtonBack(e); }
        protected override void OnRenderDropDownButtonBackground(ToolStripItemRenderEventArgs e) { DrawButtonBack(e); }
        protected override void OnRenderSplitButtonBackground(ToolStripItemRenderEventArgs e) { DrawButtonBack(e); }

        // ---------------------------------------------------------------- texto, icones, setas

        Color InkFor(ToolStripItem item)
        {
            if (!item.Enabled) return Theme.DisabledInk;
            if (IsPrimary(item) || IsOn(item)) return Color.White;
            return Theme.Ink;
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = InkFor(e.Item);
            base.OnRenderItemText(e);
        }

        protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e)
        {
            var item = e.Item;
            var g = e.Graphics;
            if (item.Tag is IconKind)
            {
                var kind = (IconKind)item.Tag;
                Color c = kind == IconKind.Dot && item.Enabled ? item.ForeColor : InkFor(item);
                Icons.Draw(g, kind, e.ImageRectangle, c);
                return;
            }
            var ws = item.Tag as WidthSample;
            if (ws != null)
            {
                float sc = Scale(g);
                float h = Math.Max(1.5f, ws.Width * sc * 0.6f);
                var r = e.ImageRectangle;
                using (var b = new SolidBrush(InkFor(item)))
                using (var path = RoundPath(new RectangleF(r.X + 1 * sc, r.Y + r.Height / 2f - h / 2f, r.Width - 2 * sc, h), h / 2f))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.FillPath(b, path);
                }
                return;
            }
            base.OnRenderItemImage(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            // seta dos botoes com menu: um chevron fino; nos submenus, o padrao
            if (e.Item is ToolStripDropDownButton)
            {
                var r = e.ArrowRectangle;
                int sz = (int)Math.Round(14 * Scale(e.Graphics));
                Icons.Draw(e.Graphics, IconKind.Chevron, new Rectangle(r.X + (r.Width - sz) / 2, r.Y + (r.Height - sz) / 2, sz, sz), InkFor(e.Item));
                return;
            }
            e.ArrowColor = Theme.Ink;
            base.OnRenderArrow(e);
        }

        // ---------------------------------------------------------------- separadores e menus

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            var g = e.Graphics;
            var item = e.Item;
            using (var p = new Pen(Theme.BarBorder))
            {
                if (e.Vertical || !(e.ToolStrip is ToolStripDropDown))
                {
                    int x = item.Width / 2;
                    int pad = (int)(8 * Scale(g));
                    g.DrawLine(p, x, pad, x, item.Height - pad);
                }
                else
                {
                    int y = item.Height / 2;
                    int pad = (int)(8 * Scale(g));
                    g.DrawLine(p, pad, y, item.Width - pad, y);
                }
            }
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            var item = e.Item;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float sc = Scale(g);
            bool selectedChoice = item.Name == "sel";
            if (!(item.Selected && item.Enabled) && !selectedChoice) return;
            var r = new RectangleF(4 * sc, 1 * sc, item.Width - 8 * sc, item.Height - 2 * sc);
            using (var path = RoundPath(r, 4f * sc))
            using (var b = new SolidBrush(item.Selected && item.Enabled ? Theme.Hover : Theme.MenuBack))
                g.FillPath(b, path);
            if (selectedChoice)
                using (var b = new SolidBrush(Theme.AccentText))
                    g.FillRectangle(b, 4 * sc, item.Height * 0.28f, 3 * sc, item.Height * 0.44f);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            Icons.Draw(e.Graphics, IconKind.Check, e.ImageRectangle, Theme.AccentText);
        }
    }
}
