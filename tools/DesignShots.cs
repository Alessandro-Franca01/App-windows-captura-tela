// Gera os prints de todas as telas do app (temas claro e escuro) sobre uma area de trabalho FICTICIA,
// para que nada da tela real do usuario apareca nas imagens.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using CapturaTela;

static class DS
{
    static string outDir;
    static Form backdrop;
    static Rectangle screen;
    static readonly BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;

    [DllImport("user32.dll")] static extern bool SetWindowDisplayAffinity(IntPtr h, uint a);

    static T Get<T>(object o, string name) { return (T)o.GetType().GetField(name, NP).GetValue(o); }
    static void Set(object o, string name, object v) { o.GetType().GetField(name, NP).SetValue(o, v); }
    static object Call(object o, string name, params object[] a) { return o.GetType().GetMethod(name, NP).Invoke(o, a); }

    static void Pump(int ms)
    {
        var end = DateTime.Now.AddMilliseconds(ms);
        while (DateTime.Now < end) { Application.DoEvents(); System.Threading.Thread.Sleep(15); }
    }

    // ---------------------------------------------------------------- area de trabalho ficticia

    static Bitmap MakeDesktop(int w, int h)
    {
        var b = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(b))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var br = new LinearGradientBrush(new Rectangle(0, 0, w, h), Color.FromArgb(46, 92, 160), Color.FromArgb(118, 74, 156), 35f))
                g.FillRectangle(br, 0, 0, w, h);
            FakeWindow(g, new Rectangle(140, 110, 980, 640), "Painel de vendas - Navegador", true);
            FakeWindow(g, new Rectangle(1000, 330, 740, 560), "Anotacoes", false);
            using (var br = new SolidBrush(Color.FromArgb(235, 28, 28, 36))) g.FillRectangle(br, 0, h - 48, w, 48);   // barra de tarefas
        }
        return b;
    }

    static void FakeWindow(Graphics g, Rectangle r, string title, bool rich)
    {
        using (var sh = new SolidBrush(Color.FromArgb(60, 0, 0, 0))) g.FillRectangle(sh, r.X + 6, r.Y + 8, r.Width, r.Height);
        g.FillRectangle(Brushes.White, r);
        using (var bar = new SolidBrush(Color.FromArgb(243, 243, 243))) g.FillRectangle(bar, r.X, r.Y, r.Width, 40);
        using (var f = new Font("Segoe UI", 10)) g.DrawString(title, f, Brushes.Black, r.X + 14, r.Y + 10);
        if (!rich) { using (var f = new Font("Segoe UI", 11)) for (int i = 0; i < 12; i++) g.DrawString("Linha de texto de exemplo numero " + (i + 1), f, Brushes.DimGray, r.X + 20, r.Y + 60 + i * 30); return; }
        g.FillRectangle(Brushes.White, r.X, r.Y + 40, r.Width, r.Height - 40);
        using (var side = new SolidBrush(Color.FromArgb(30, 41, 59))) g.FillRectangle(side, r.X, r.Y + 40, 180, r.Height - 40);
        using (var f = new Font("Segoe UI", 11, FontStyle.Bold)) g.DrawString("Vendas", f, Brushes.White, r.X + 18, r.Y + 62);
        using (var f = new Font("Segoe UI", 10)) foreach (var s in new[] { "Resumo", "Clientes", "Produtos", "Relatorios" }) g.DrawString(s, f, Brushes.Silver, r.X + 18, r.Y + 110 + 34 * Array.IndexOf(new[] { "Resumo", "Clientes", "Produtos", "Relatorios" }, s));
        for (int i = 0; i < 3; i++)
        {
            var c = new Rectangle(r.X + 210 + i * 245, r.Y + 62, 225, 110);
            using (var br = new SolidBrush(Color.FromArgb(241, 245, 249))) g.FillRectangle(br, c);
            using (var f = new Font("Segoe UI", 9)) g.DrawString(new[] { "Receita", "Pedidos", "Clientes novos" }[i], f, Brushes.DimGray, c.X + 14, c.Y + 12);
            using (var f = new Font("Segoe UI", 22, FontStyle.Bold)) g.DrawString(new[] { "R$ 48.920", "1.284", "342" }[i], f, Brushes.Black, c.X + 14, c.Y + 40);
        }
        int[] hs = { 120, 180, 150, 230, 200, 260, 220 };
        for (int i = 0; i < hs.Length; i++)
            using (var br = new SolidBrush(Color.FromArgb(59, 130, 246))) g.FillRectangle(br, r.X + 230 + i * 70, r.Y + r.Height - 40 - hs[i], 44, hs[i]);
    }

    // imagem "capturada" mostrada no editor
    static Bitmap MakeAppShot()
    {
        var b = new Bitmap(940, 560, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(b))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(Color.White);
            FakeWindow(g, new Rectangle(0, 0, 940, 560), "Painel de vendas", true);
            using (var f = new Font("Segoe UI", 10)) g.DrawString("Contato: cliente@exemplo.com.br", f, Brushes.Black, 230, 200);
        }
        return b;
    }

    // ---------------------------------------------------------------- captura de janelas reais

    static void Place(Form f, int dx, int dy)
    {
        f.StartPosition = FormStartPosition.Manual;
        f.Location = new Point(screen.X + (screen.Width - f.Width) / 2 + dx, screen.Y + (screen.Height - f.Height) / 2 + dy);
    }

    static void Capture(Rectangle r, string name)
    {
        r.Intersect(screen);
        using (var b = new Bitmap(r.Width, r.Height))
        {
            using (var g = Graphics.FromImage(b)) g.CopyFromScreen(r.Location, Point.Empty, r.Size);
            b.Save(Path.Combine(outDir, name + ".png"), ImageFormat.Png);
        }
        Console.WriteLine("  " + name + ".png  " + r.Width + "x" + r.Height);
    }

    static Rectangle Frame(Form f, int margin)
    {
        Native.RECT r;
        Rectangle b = f.Bounds;
        if (Native.DwmGetWindowAttribute(f.Handle, 9, out r, Marshal.SizeOf(typeof(Native.RECT))) == 0) b = Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
        b.Inflate(margin, margin);
        return b;
    }

    static void Shoot(Form f, string name, int dx, int dy, Action before)
    {
        Place(f, dx, dy);
        f.Show();
        f.BringToFront(); f.Activate();
        SetWindowDisplayAffinity(f.Handle, 0);            // paineis do app sao excluidos da captura; aqui queremos ve-los
        if (before != null) before();
        Pump(900);
        Capture(Frame(f, 22), name);
    }

    static void Done(Form f) { f.Close(); f.Dispose(); Pump(150); }

    // ---------------------------------------------------------------- roteiro

    [STAThread]
    static void Main(string[] a)
    {
        Native.SetProcessDPIAware();
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        outDir = a[0]; Directory.CreateDirectory(outDir);
        screen = Screen.PrimaryScreen.Bounds;

        // permissoes de exemplo (arquivo isolado)
        string permFile = Path.Combine(outDir, "perm-exemplo.ini");
        File.WriteAllText(permFile, "enabled=True\ngateCli=False\nallow=target:window:code\nallow=windows\nallow=info\nallow=action:ocr\ndeny=target:window:chrome\nallow=file:read:C:\\Users\\Ana\\Imagens\\*\n");
        Environment.SetEnvironmentVariable(PermissionStore.EnvPath, permFile);

        var desktop = MakeDesktop(screen.Width, screen.Height);
        backdrop = new Form { FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.Manual, Bounds = screen, ShowInTaskbar = false, BackgroundImage = desktop, BackgroundImageLayout = ImageLayout.None };
        backdrop.Show(); Pump(500);

        foreach (int theme in new[] { 1, 2 })
        {
            string tag = theme == 1 ? "claro" : "escuro";
            Console.WriteLine("== tema " + tag);
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "CapturaTela.ini"), "Theme=" + theme + "\nNotify=True\nMode=0\nAutoSaveDir=C:\\Users\\Ana\\Imagens\\Capturas de Tela\n");
            Theme.Preference = theme;

            // 01 principal vazia
            var m = new MainForm(false);
            Shoot(m, "01-janela-principal-vazia-" + tag, 0, -20, delegate { Get<ToolStripStatusLabel>(m, "statusLabel").Text = "Pronto - atalho: Print Screen"; });
            // 02 principal com captura e anotacoes
            var canvas = Get<CapturaTela.Canvas>(m, "canvas");
            canvas.SetImage(MakeAppShot());
            canvas.Visible = true; Get<Label>(m, "placeholder").Visible = false;
            Call(m, "SetEditEnabled", true); Call(m, "CenterCanvas");
            canvas.Annotations.Add(new ShapeAnn { Kind = ShapeKind.Rectangle, A = new Point(205, 55), B = new Point(455, 180), Color = Color.FromArgb(220, 38, 38), Width = 4 });
            canvas.Annotations.Add(new ShapeAnn { Kind = ShapeKind.Arrow, A = new Point(620, 120), B = new Point(470, 120), Color = Color.FromArgb(220, 38, 38), Width = 4 });
            canvas.Annotations.Add(new TextAnn { Pos = new Point(630, 100), Text = "Receita do mes", Color = Color.FromArgb(220, 38, 38), Width = 4 });
            canvas.Annotations.Add(new StepAnn { Center = new Point(520, 330), Number = 1, Color = Color.FromArgb(37, 99, 235), Width = 4 });
            canvas.Annotations.Add(new StepAnn { Center = new Point(760, 330), Number = 2, Color = Color.FromArgb(37, 99, 235), Width = 4 });
            canvas.Annotations.Add(new BlurAnn { Rect = new Rectangle(218, 192, 330, 30), Pixelate = true, Width = 4 });
            canvas.Invalidate();
            Get<ToolStripStatusLabel>(m, "statusLabel").Text = "Captura: 940 x 560 px  -  copiada para a area de transferencia";
            Pump(300);
            Shoot(m, "02-janela-principal-com-captura-" + tag, 0, -20, null);
            // 03 menu da bandeja
            var tray = Get<NotifyIcon>(m, "tray");
            var menu = tray.ContextMenuStrip;
            menu.Show(new Point(screen.Width - 330, screen.Height - 400));
            Pump(500);
            Capture(new Rectangle(menu.Left - 14, menu.Top - 14, menu.Width + 28, menu.Height + 28), "03-menu-da-bandeja-" + tag);
            menu.Close(); Pump(200);
            Done(m);

            // 04 janela de selecao (overlay)
            var shot = MakeDesktop(screen.Width, screen.Height);
            Action<string, Action<OverlayForm>> overlay = delegate(string name, Action<OverlayForm> setup)
            {
                var o = new OverlayForm(shot, CaptureMode.Rectangle);
                o.Show(); Pump(500);
                setup(o); o.Invalidate(); o.Update(); Pump(400);
                Capture(screen, name);
                o.Close(); o.Dispose(); Pump(200);
            };
            if (theme == 1)
            {
                overlay("04-selecao-inicio", delegate(OverlayForm o) { Set(o, "mouse", new Point(1180, 760)); });
                overlay("05-selecao-retangulo-com-lupa", delegate(OverlayForm o) { Set(o, "dragging", true); Set(o, "start", new Point(330, 260)); Set(o, "current", new Point(930, 610)); Set(o, "mouse", new Point(930, 610)); });
                overlay("06-selecao-janela", delegate(OverlayForm o) { Set(o, "mode", CaptureMode.Window); Set(o, "hoverWindow", new Rectangle(140, 110, 980, 640)); Set(o, "mouse", new Point(600, 400)); });
                overlay("07-selecao-forma-livre", delegate(OverlayForm o)
                {
                    Set(o, "mode", CaptureMode.Freeform); Set(o, "dragging", true);
                    var pts = Get<List<Point>>(o, "freePoints");
                    foreach (var p in new[] { new Point(420, 300), new Point(640, 260), new Point(860, 340), new Point(900, 520), new Point(700, 640), new Point(480, 600), new Point(380, 450) }) pts.Add(p);
                    Set(o, "mouse", new Point(380, 450));
                });
                overlay("08-selecao-rolagem-manual", delegate(OverlayForm o) { Set(o, "mode", CaptureMode.ScrollManual); Set(o, "mouse", new Point(1180, 760)); });
            }

            // 09 configuracoes
            var set = new SettingsForm(AppSettings.Load());
            Shoot(set, "09-configuracoes-" + tag, 0, 0, null); Done(set);
            // 10/11 permissoes
            var pf = new PermissionsForm();
            Shoot(pf, "10-permissoes-da-ia-" + tag, 0, 0, null);
            var add = new AddPermissionForm();
            Place(add, 60, 60); add.Show(); add.BringToFront(); Pump(700);
            Capture(Frame(add, 22), "11-adicionar-permissao-" + tag);
            add.Close(); Done(pf);
            // 12 pedido de permissao
            var pd = new PermissionDialog(new PromptRequest { Client = "Claude Code (IA)", Key = "target:window:code", AlwaysKey = "target:window:code", Description = "Capturar a janela \"capturar-tela - Visual Studio Code\" (programa \"Code\")" }, 0);
            Shoot(pd, "12-pedido-de-permissao-" + tag, 0, 0, null); Done(pd);
            // 13 ajuda
            var hf = new HelpForm(1);
            Shoot(hf, "13-ajuda-" + tag, 0, -10, null); Done(hf);
            // 14 texto e 15 ocr
            var tp = (Form)Activator.CreateInstance(typeof(TextPrompt), true);
            Shoot(tp, "14-caixa-de-texto-" + tag, 0, 0, delegate { Get<TextBox>(tp, "box").Text = "Receita do mes"; }); Done(tp);
            var ocr = new OcrResultForm("Servidor MCP funcionando\r\nTeste de OCR 2026\r\nContato: cliente@exemplo.com.br");
            Shoot(ocr, "15-resultado-do-ocr-" + tag, 0, 0, null); Done(ocr);

            // 16/17 gravador de GIF e rolagem manual (com a area e a moldura vermelha ao redor)
            Action<string, Form, Rectangle, Action> panelShot = delegate(string name, Form f, Rectangle region, Action tweak)
            {
                f.Show(); Pump(700);
                SetWindowDisplayAffinity(f.Handle, 0);
                foreach (Form of in Application.OpenForms) if (of.GetType().Name == "RegionFrame") SetWindowDisplayAffinity(of.Handle, 0);
                if (tweak != null) tweak();
                Pump(500);
                var all = Rectangle.Union(Rectangle.Inflate(region, 20, 20), Frame(f, 20));
                Capture(all, name);
                f.Close(); f.Dispose(); Pump(300);
            };
            var reg = new Rectangle(140, 110, 980, 640);
            var rec = new RecorderForm(reg);
            panelShot("16-gravador-de-gif-pronto-" + tag, rec, reg, null);
            var rec2 = new RecorderForm(reg);
            panelShot("17-gravador-de-gif-gravando-" + tag, rec2, reg, delegate { Get<Label>(rec2, "info").Text = "Gravando 7s / 30s  (Esc ou o atalho para parar)"; Get<Button>(rec2, "startBtn").Text = "\u25A0 Parar"; Get<Button>(rec2, "cancelBtn").Enabled = false; });
            var sm = new ScrollManualForm(reg);
            panelShot("18-rolagem-manual-" + tag, sm, reg, null);
            var sm2 = new ScrollManualForm(reg);
            panelShot("19-rolagem-manual-perdeu-encaixe-" + tag, sm2, reg, delegate { Get<Label>(sm2, "hint").Text = "Rolou demais de uma vez e perdi o encaixe. Volte um pouco para cima, ate ver de novo a parte ja capturada."; Get<Label>(sm2, "info").Text = "Capturado: 1860 px de altura"; });
        }
        backdrop.Close();
        Console.WriteLine("pronto");
    }
}
