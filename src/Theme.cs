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

        // ---- tokens semanticos (docs/design/design-system/tokens.json); mudam com o tema ----
        // Os valores de contraste foram ajustados para pelo menos 4,5:1 sobre a superficie do tema.
        public static Color Surface { get { return IsDark ? DarkBack : SystemColors.Control; } }
        public static Color SurfaceRaised { get { return IsDark ? DarkPanel : SystemColors.Window; } }
        public static Color Ink { get { return IsDark ? DarkText : SystemColors.ControlText; } }
        public static Color Muted { get { return IsDark ? Color.FromArgb(0xA8, 0xA8, 0xA8) : Color.FromArgb(0x59, 0x59, 0x59); } }
        public static Color Success { get { return IsDark ? Color.FromArgb(0x4C, 0xC3, 0x8A) : Color.FromArgb(0x18, 0x73, 0x3F); } }
        public static Color Danger { get { return IsDark ? Color.FromArgb(0xFF, 0x7B, 0x72) : Color.FromArgb(0xA6, 0x1C, 0x1C); } }
        public static Color Warning { get { return IsDark ? Color.FromArgb(0xFC, 0xCF, 0x4A) : Color.FromArgb(0x7A, 0x52, 0x00); } }
        // azul para botoes cheios com texto branco (5,7:1) e para links/foco sobre o tema escuro
        public static readonly Color AccentStrong = Color.FromArgb(0x00, 0x67, 0xC0);
        public static Color AccentText { get { return IsDark ? Color.FromArgb(0x4C, 0xC2, 0xFF) : AccentStrong; } }

        // barras de ferramentas, menus e estados de botao
        public static Color BarBack { get { return IsDark ? Color.FromArgb(0x2B, 0x2B, 0x2B) : Color.FromArgb(0xF7, 0xF7, 0xF7); } }
        public static Color BarBorder { get { return IsDark ? Color.FromArgb(0x42, 0x42, 0x42) : Color.FromArgb(0xD6, 0xD6, 0xD6); } }
        public static Color Hover { get { return IsDark ? Color.FromArgb(0x3C, 0x3C, 0x3C) : Color.FromArgb(0xE6, 0xE6, 0xE6); } }
        public static Color Pressed { get { return IsDark ? Color.FromArgb(0x48, 0x48, 0x48) : Color.FromArgb(0xD8, 0xD8, 0xD8); } }
        public static Color MenuBack { get { return IsDark ? Color.FromArgb(0x2B, 0x2B, 0x2B) : Color.White; } }
        public static Color MenuBorder { get { return IsDark ? Color.FromArgb(0x4A, 0x4A, 0x4A) : Color.FromArgb(0xCC, 0xCC, 0xCC); } }
        public static Color DisabledInk { get { return IsDark ? Color.FromArgb(0x78, 0x78, 0x78) : Color.FromArgb(0x9A, 0x9A, 0x9A); } }

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

        // Dialogos tem tamanhos fixos em pixels: sem isto, com a tela em 125%/150% os textos crescem
        // (o app e DPI-aware) e ficam cortados nos botoes e rotulos. Chamar no fim do construtor.
        public static void ScaleForDpi(Form f)
        {
            f.AutoScaleDimensions = new SizeF(96f, 96f);
            f.AutoScaleMode = AutoScaleMode.Dpi;
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
                if (child is EmptyState) continue;   // desenha as proprias cores (fica sobre o fundo escuro da area de edicao)
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
                else if (child is ListView)
                {
                    child.BackColor = dark ? DarkPanel : SystemColors.Window;
                    child.ForeColor = dark ? DarkText : SystemColors.WindowText;
                }
                else if (!(child is Canvas))
                {
                    if (!(child is Label) || child.ForeColor == SystemColors.ControlText || child.ForeColor == DarkText)
                        child.ForeColor = dark ? DarkText : SystemColors.ControlText;
                }
                Walk(child, dark);
            }
        }

        // Barras, menus e botoes de barra usam o AppRenderer (cantos arredondados, icones e estados do tema).
        public static void Apply(ToolStrip strip, bool dark)
        {
            strip.Renderer = new AppRenderer();
            strip.BackColor = strip is ToolStripDropDown ? MenuBack : BarBack;
            strip.ForeColor = Ink;
            foreach (ToolStripItem item in strip.Items)
            {
                var dd = item as ToolStripDropDownItem;
                if (dd != null && dd.HasDropDownItems)
                {
                    dd.DropDown.Renderer = strip.Renderer;
                    dd.DropDown.BackColor = MenuBack;
                    dd.DropDown.ForeColor = Ink;
                    foreach (ToolStripItem sub in dd.DropDownItems) sub.ForeColor = Ink;
                }
                if (!(item is ToolStripSeparator) && item.Tag == null) item.ForeColor = Ink;
            }
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
