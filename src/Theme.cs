using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CapturaTela
{
    // Tema claro/escuro (0 = seguir o Windows, 1 = claro, 2 = escuro).
    static class Theme
    {
        public static int Preference;   // definido a partir das configuracoes

        public static readonly Color DarkBack = Color.FromArgb(32, 32, 32);
        public static readonly Color DarkPanel = Color.FromArgb(45, 45, 45);
        public static readonly Color DarkBorder = Color.FromArgb(80, 80, 80);
        public static readonly Color DarkText = Color.FromArgb(240, 240, 240);
        public static readonly Color Accent = Color.FromArgb(0, 120, 215);

        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        public static bool IsDark
        {
            get
            {
                if (Preference == 1) return false;
                if (Preference == 2) return true;
                try
                {
                    using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                    {
                        object v = k == null ? null : k.GetValue("AppsUseLightTheme");
                        return v is int && (int)v == 0;
                    }
                }
                catch { return false; }
            }
        }

        // Aplica o tema a um formulario inteiro (controles filhos e barra de titulo).
        public static void Apply(Form form)
        {
            bool dark = IsDark;
            form.BackColor = dark ? DarkBack : SystemColors.Control;
            form.ForeColor = dark ? DarkText : SystemColors.ControlText;
            Walk(form, dark);
            if (form.IsHandleCreated) SetTitleBar(form, dark);
            else form.HandleCreated += delegate { SetTitleBar(form, IsDark); };
        }

        static void SetTitleBar(Form f, bool dark)
        {
            try { int v = dark ? 1 : 0; DwmSetWindowAttribute(f.Handle, 20, ref v, 4); } catch { }
        }

        static void Walk(Control c, bool dark)
        {
            foreach (Control child in c.Controls)
            {
                var btn = child as Button;
                var txt = child as TextBox;
                var cmb = child as ComboBox;
                if (btn != null)
                {
                    btn.FlatStyle = dark ? FlatStyle.Flat : FlatStyle.Standard;
                    btn.UseVisualStyleBackColor = !dark;
                    if (dark) { btn.BackColor = DarkPanel; btn.ForeColor = DarkText; btn.FlatAppearance.BorderColor = DarkBorder; }
                    else { btn.BackColor = SystemColors.Control; btn.ForeColor = SystemColors.ControlText; }
                }
                else if (txt != null)
                {
                    txt.BackColor = dark ? DarkPanel : SystemColors.Window;
                    txt.ForeColor = dark ? DarkText : SystemColors.WindowText;
                    txt.BorderStyle = BorderStyle.FixedSingle;
                }
                else if (cmb != null)
                {
                    cmb.FlatStyle = dark ? FlatStyle.Flat : FlatStyle.Standard;
                    cmb.BackColor = dark ? DarkPanel : SystemColors.Window;
                    cmb.ForeColor = dark ? DarkText : SystemColors.WindowText;
                }
                else if (!(child is Canvas))
                {
                    if (!(child is Label) || child.ForeColor == SystemColors.ControlText || child.ForeColor == DarkText)
                        child.ForeColor = dark ? DarkText : SystemColors.ControlText;
                }
                Walk(child, dark);
            }
        }

        public static void Apply(ToolStrip strip, bool dark)
        {
            strip.RenderMode = ToolStripRenderMode.Professional;
            if (dark)
            {
                strip.Renderer = new ToolStripProfessionalRenderer(new DarkColors());
                strip.BackColor = DarkPanel; strip.ForeColor = DarkText;
            }
            else
            {
                strip.Renderer = new ToolStripProfessionalRenderer();
                strip.BackColor = SystemColors.Control; strip.ForeColor = SystemColors.ControlText;
            }
            foreach (ToolStripItem item in strip.Items)
            {
                if (item is ToolStripSeparator) continue;
                var combo = item as ToolStripComboBox;
                if (combo != null)
                {
                    combo.ComboBox.FlatStyle = dark ? FlatStyle.Flat : FlatStyle.Standard;
                    combo.BackColor = dark ? DarkBack : SystemColors.Window;
                    combo.ForeColor = dark ? DarkText : SystemColors.WindowText;
                }
                else if (!(item is ToolStripButton && item.ForeColor != SystemColors.ControlText && item.ForeColor != DarkText))
                {
                    item.ForeColor = dark ? DarkText : SystemColors.ControlText;
                }
            }
        }

        class DarkColors : ProfessionalColorTable
        {
            public override Color ToolStripGradientBegin { get { return DarkPanel; } }
            public override Color ToolStripGradientMiddle { get { return DarkPanel; } }
            public override Color ToolStripGradientEnd { get { return DarkPanel; } }
            public override Color ToolStripBorder { get { return DarkBorder; } }
            public override Color ToolStripDropDownBackground { get { return DarkPanel; } }
            public override Color MenuItemSelected { get { return Color.FromArgb(70, 70, 70); } }
            public override Color MenuItemBorder { get { return DarkBorder; } }
            public override Color MenuItemSelectedGradientBegin { get { return Color.FromArgb(70, 70, 70); } }
            public override Color MenuItemSelectedGradientEnd { get { return Color.FromArgb(70, 70, 70); } }
            public override Color ImageMarginGradientBegin { get { return DarkPanel; } }
            public override Color ImageMarginGradientMiddle { get { return DarkPanel; } }
            public override Color ImageMarginGradientEnd { get { return DarkPanel; } }
            public override Color SeparatorDark { get { return DarkBorder; } }
            public override Color SeparatorLight { get { return DarkBorder; } }
            public override Color ButtonSelectedHighlight { get { return Color.FromArgb(75, 75, 75); } }
            public override Color ButtonSelectedGradientBegin { get { return Color.FromArgb(75, 75, 75); } }
            public override Color ButtonSelectedGradientMiddle { get { return Color.FromArgb(75, 75, 75); } }
            public override Color ButtonSelectedGradientEnd { get { return Color.FromArgb(75, 75, 75); } }
            public override Color ButtonSelectedBorder { get { return DarkBorder; } }
            public override Color ButtonPressedGradientBegin { get { return Accent; } }
            public override Color ButtonPressedGradientMiddle { get { return Accent; } }
            public override Color ButtonPressedGradientEnd { get { return Accent; } }
            public override Color ButtonPressedBorder { get { return Accent; } }
            public override Color ButtonCheckedGradientBegin { get { return Color.FromArgb(0, 95, 170); } }
            public override Color ButtonCheckedGradientMiddle { get { return Color.FromArgb(0, 95, 170); } }
            public override Color ButtonCheckedGradientEnd { get { return Color.FromArgb(0, 95, 170); } }
            public override Color ButtonCheckedHighlight { get { return Color.FromArgb(0, 95, 170); } }
            public override Color CheckBackground { get { return Color.FromArgb(0, 95, 170); } }
            public override Color StatusStripGradientBegin { get { return DarkPanel; } }
            public override Color StatusStripGradientEnd { get { return DarkPanel; } }
        }
    }

    // Icone do aplicativo, embutido no .exe como recurso "icon.ico".
    static class AppIcon
    {
        public static Icon Get(Size size)
        {
            try
            {
                using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("icon.ico"))
                    if (s != null) return new Icon(s, size);
            }
            catch { }
            return SystemIcons.Application;
        }
    }
}
