using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace CapturaTela
{
    // Os valores sao gravados nas configuracoes: so acrescente no fim.
    enum CaptureMode { Rectangle, Freeform, Window, FullScreen, Scroll, Record, ScrollManual }

    // Ordem e nomes dos modos como o usuario os ve (lista da janela principal, barra e teclas 1-7 da selecao).
    static class ModeList
    {
        public static readonly CaptureMode[] Order =
        {
            CaptureMode.Rectangle, CaptureMode.Freeform, CaptureMode.Window, CaptureMode.FullScreen,
            CaptureMode.Scroll, CaptureMode.ScrollManual, CaptureMode.Record
        };

        public static readonly string[] Names =
        {
            "Retângulo", "Forma livre", "Janela", "Tela inteira", "Rolagem automática", "Rolagem manual", "Gravar GIF"
        };

        public static int IndexOf(CaptureMode m)
        {
            int i = Array.IndexOf(Order, m);
            return i < 0 ? 0 : i;
        }
    }

    // Janela de topo visivel.
    class WindowInfo
    {
        public IntPtr Handle;
        public string Title;
        public string Process;
        public int Pid;
        public Rectangle Bounds;
    }

    static class Native
    {
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mod, uint vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
        [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr hWnd, int idx);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr hWnd, StringBuilder sb, int max);
        [DllImport("user32.dll")] static extern int GetWindowTextLength(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int cmd);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hWnd, int attr, out RECT r, int size);
        [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hWnd, int attr, out int v, int size);
        [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")] public static extern void mouse_event(uint flags, int dx, int dy, int data, UIntPtr extra);
        [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vKey);

        [DllImport("user32.dll")] static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint affinity);

        // Windows 10 2004+: a janela continua visivel para o usuario, mas nao aparece em capturas de tela.
        // Assim paineis e molduras do proprio app nunca entram na imagem. Devolve false em Windows antigos.
        public static bool ExcludeFromCapture(IntPtr hWnd)
        {
            try { return SetWindowDisplayAffinity(hWnd, 0x11); } catch { return false; }
        }

        public const uint MOUSEEVENTF_WHEEL = 0x0800;
        public const int VK_ESCAPE = 0x1B;
        public const int WM_HOTKEY = 0x0312;
        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_TOOLWINDOW = 0x80;
        public const int DWMWA_CLOAKED = 14;
        public const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
        public const int SW_RESTORE = 9;

        public const uint MOD_ALT = 1, MOD_CONTROL = 2, MOD_SHIFT = 4, MOD_WIN = 8;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left, Top, Right, Bottom; }

        // Retangulo real da janela (sem a sombra invisivel do Windows 10/11).
        public static bool GetWindowBounds(IntPtr h, out Rectangle bounds)
        {
            RECT r;
            bounds = Rectangle.Empty;
            if (DwmGetWindowAttribute(h, DWMWA_EXTENDED_FRAME_BOUNDS, out r, Marshal.SizeOf(typeof(RECT))) != 0)
                if (!GetWindowRect(h, out r)) return false;
            bounds = Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
            return true;
        }

        public static string GetTitle(IntPtr h)
        {
            int len = GetWindowTextLength(h);
            if (len <= 0) return "";
            var sb = new StringBuilder(len + 1);
            GetWindowText(h, sb, sb.Capacity);
            return sb.ToString();
        }

        // Janelas de topo visiveis, em ordem Z (da frente para tras).
        public static List<WindowInfo> ListWindows(bool includeSelf)
        {
            var list = new List<WindowInfo>();
            uint self = (uint)Process.GetCurrentProcess().Id;
            EnumWindows(delegate(IntPtr h, IntPtr l)
            {
                if (!IsWindowVisible(h) || IsIconic(h)) return true;
                uint pid; GetWindowThreadProcessId(h, out pid);
                if (!includeSelf && pid == self) return true;
                if ((GetWindowLong(h, GWL_EXSTYLE) & WS_EX_TOOLWINDOW) != 0) return true;
                int cloaked;
                if (DwmGetWindowAttribute(h, DWMWA_CLOAKED, out cloaked, 4) == 0 && cloaked != 0) return true;
                Rectangle rect;
                if (!GetWindowBounds(h, out rect)) return true;
                if (rect.Width < 20 || rect.Height < 20) return true;

                string proc = "";
                try { proc = Process.GetProcessById((int)pid).ProcessName; } catch { }
                list.Add(new WindowInfo { Handle = h, Title = GetTitle(h), Process = proc, Pid = (int)pid, Bounds = rect });
                return true;
            }, IntPtr.Zero);
            return list;
        }

        public static List<Rectangle> VisibleWindows()
        {
            var rects = new List<Rectangle>();
            foreach (var w in ListWindows(false)) rects.Add(w.Bounds);
            return rects;
        }

        // Traz a janela para frente (restaura se estiver minimizada).
        public static void Activate(IntPtr h)
        {
            if (IsIconic(h)) ShowWindow(h, SW_RESTORE);
            SetForegroundWindow(h);
        }
    }
}
