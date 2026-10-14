using System;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("Ferramenta de Captura")]
[assembly: AssemblyProduct("Ferramenta de Captura")]
[assembly: AssemblyVersion("2.1.0.0")]

namespace CapturaTela
{
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            Native.SetProcessDPIAware();

            // Qualquer argumento que nao seja /tray e um comando da CLI (veja Cli.cs).
            // No executavel de console (CapturaTelaCli.exe, compilado com /define:CLI)
            // rodar sem argumentos tambem mostra a ajuda.
            bool gui = args.Length == 0 || args[0] == "/tray";
#if CLI
            gui = args.Length > 0 && args[0] == "/tray";
#endif
            if (!gui) return Cli.Run(args);

            bool created;
            using (var mutex = new Mutex(true, "CapturaTela.SingleInstance", out created))
            {
                if (!created)
                {
                    MessageBox.Show("A Ferramenta de Captura ja esta em execucao (veja a bandeja do sistema).",
                        "Ferramenta de Captura", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 0;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                bool startHidden = Array.IndexOf(args, "/tray") >= 0;
                Application.Run(new MainForm(startHidden));
            }
            return 0;
        }
    }
}
