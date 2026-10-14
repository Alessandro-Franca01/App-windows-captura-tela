using System;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("Ferramenta de Captura")]
[assembly: AssemblyProduct("Ferramenta de Captura")]
[assembly: AssemblyVersion("2.0.0.0")]

namespace CapturaTela
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            bool created;
            using (var mutex = new Mutex(true, "CapturaTela.SingleInstance", out created))
            {
                if (!created)
                {
                    MessageBox.Show("A Ferramenta de Captura ja esta em execucao (veja a bandeja do sistema).",
                        "Ferramenta de Captura", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                Native.SetProcessDPIAware();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                bool startHidden = Array.IndexOf(args, "/tray") >= 0;
                Application.Run(new MainForm(startHidden));
            }
        }
    }
}
