using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CapturaTela
{
    // Configuracoes persistidas em CapturaTela.ini (ao lado do .exe; se a pasta for
    // somente leitura, em %APPDATA%\CapturaTela) para manter o app portatil.
    class AppSettings
    {
        public int Mode;
        public int Delay;
        public int PenColor = System.Drawing.Color.Red.ToArgb();
        public int PenWidth = 1;
        public bool AutoSave;
        public string AutoSaveDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Capturas de Tela");
        public string AutoSaveFormat = "png";
        public uint HotMods;
        public Keys HotKey = Keys.PrintScreen;
        public bool CloseToTray = true;
        public bool TrayTipShown;
        public int Theme;                  // 0 = seguir o Windows, 1 = claro, 2 = escuro
        public bool Notify = true;

        string path;

        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunName = "FerramentaCaptura";

        public static AppSettings Load()
        {
            var s = new AppSettings();
            s.path = ResolvePath();
            try
            {
                if (File.Exists(s.path))
                {
                    foreach (var raw in File.ReadAllLines(s.path, Encoding.UTF8))
                    {
                        int i = raw.IndexOf('=');
                        if (i <= 0) continue;
                        s.Set(raw.Substring(0, i).Trim(), raw.Substring(i + 1).Trim());
                    }
                }
            }
            catch { /* arquivo corrompido: usa os padroes */ }
            return s;
        }

        static string ResolvePath() { return DataFile("CapturaTela.ini"); }

        // Arquivo de dados do app: ao lado do .exe; se a pasta for somente leitura, em %APPDATA%\CapturaTela.
        public static string DataFile(string fileName)
        {
            string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string local = Path.Combine(dir, fileName);
            if (File.Exists(local)) return local;
            try
            {
                string probe = Path.Combine(dir, ".write-test-" + Guid.NewGuid().ToString("N"));
                File.WriteAllText(probe, "x");
                File.Delete(probe);
                return local;
            }
            catch
            {
                string app = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CapturaTela");
                Directory.CreateDirectory(app);
                return Path.Combine(app, fileName);
            }
        }

        void Set(string k, string v)
        {
            var inv = CultureInfo.InvariantCulture;
            int n; uint u; bool b;
            switch (k)
            {
                case "Mode": if (int.TryParse(v, NumberStyles.Integer, inv, out n) && n >= 0 && n <= 6) Mode = n; break;
                case "Delay": if (int.TryParse(v, NumberStyles.Integer, inv, out n) && n >= 0 && n <= 5) Delay = n; break;
                case "PenColor": if (int.TryParse(v, NumberStyles.Integer, inv, out n)) PenColor = n; break;
                case "PenWidth": if (int.TryParse(v, NumberStyles.Integer, inv, out n) && n >= 0 && n <= 3) PenWidth = n; break;
                case "AutoSave": if (bool.TryParse(v, out b)) AutoSave = b; break;
                case "AutoSaveDir": if (v.Length > 0) AutoSaveDir = v; break;
                case "AutoSaveFormat": if (v == "png" || v == "jpg") AutoSaveFormat = v; break;
                case "HotMods": if (uint.TryParse(v, NumberStyles.Integer, inv, out u)) HotMods = u; break;
                case "HotKey": if (int.TryParse(v, NumberStyles.Integer, inv, out n) && n > 0) HotKey = (Keys)n; break;
                case "CloseToTray": if (bool.TryParse(v, out b)) CloseToTray = b; break;
                case "Theme": if (int.TryParse(v, NumberStyles.Integer, inv, out n) && n >= 0 && n <= 2) Theme = n; break;
                case "Notify": if (bool.TryParse(v, out b)) Notify = b; break;
                case "TrayTipShown": if (bool.TryParse(v, out b)) TrayTipShown = b; break;
            }
        }

        public void Save()
        {
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.AppendLine("Mode=" + Mode.ToString(inv));
            sb.AppendLine("Delay=" + Delay.ToString(inv));
            sb.AppendLine("PenColor=" + PenColor.ToString(inv));
            sb.AppendLine("PenWidth=" + PenWidth.ToString(inv));
            sb.AppendLine("AutoSave=" + AutoSave);
            sb.AppendLine("AutoSaveDir=" + AutoSaveDir);
            sb.AppendLine("AutoSaveFormat=" + AutoSaveFormat);
            sb.AppendLine("HotMods=" + HotMods.ToString(inv));
            sb.AppendLine("HotKey=" + ((int)HotKey).ToString(inv));
            sb.AppendLine("CloseToTray=" + CloseToTray);
            sb.AppendLine("TrayTipShown=" + TrayTipShown);
            sb.AppendLine("Theme=" + Theme.ToString(inv));
            sb.AppendLine("Notify=" + Notify);
            try { File.WriteAllText(path, sb.ToString(), Encoding.UTF8); }
            catch { /* sem permissao: ignora, as configuracoes valem so nesta sessao */ }
        }

        public static string HotkeyText(uint mods, Keys key)
        {
            var parts = new List<string>();
            if ((mods & Native.MOD_CONTROL) != 0) parts.Add("Ctrl");
            if ((mods & Native.MOD_ALT) != 0) parts.Add("Alt");
            if ((mods & Native.MOD_SHIFT) != 0) parts.Add("Shift");
            if ((mods & Native.MOD_WIN) != 0) parts.Add("Win");
            parts.Add(key == Keys.PrintScreen ? "Print Screen" : key.ToString());
            return string.Join("+", parts.ToArray());
        }

        // ---- iniciar com o Windows (unica coisa que toca no registro, e so se o usuario ligar) ----

        public static bool GetStartWithWindows()
        {
            try { using (var k = Registry.CurrentUser.OpenSubKey(RunKey)) return k != null && k.GetValue(RunName) != null; }
            catch { return false; }
        }

        public static void SetStartWithWindows(bool on)
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (k == null) return;
                    if (on) k.SetValue(RunName, "\"" + Application.ExecutablePath + "\" /tray");
                    else k.DeleteValue(RunName, false);
                }
            }
            catch { }
        }
    }
}
