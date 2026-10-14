using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace CapturaTela
{
    // OCR usando o motor nativo do Windows (Windows.Media.Ocr), chamado via PowerShell
    // para nao depender de SDKs. Usa os idiomas de OCR instalados no perfil do usuario.
    static class Ocr
    {
        const string Script = @"
param([string]$Path)
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
Add-Type -AssemblyName System.Runtime.WindowsRuntime
$null = [Windows.Media.Ocr.OcrEngine, Windows.Foundation, ContentType=WindowsRuntime]
$null = [Windows.Graphics.Imaging.BitmapDecoder, Windows.Foundation, ContentType=WindowsRuntime]
$null = [Windows.Storage.StorageFile, Windows.Storage, ContentType=WindowsRuntime]
$asTask = ([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {
    $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' })[0]
function Await($op, $type) {
    $t = $asTask.MakeGenericMethod($type).Invoke($null, @($op))
    $null = $t.Wait(-1)
    $t.Result
}
$file = Await ([Windows.Storage.StorageFile]::GetFileFromPathAsync($Path)) ([Windows.Storage.StorageFile])
$stream = Await ($file.OpenAsync([Windows.Storage.FileAccessMode]::Read)) ([Windows.Storage.Streams.IRandomAccessStream])
$decoder = Await ([Windows.Graphics.Imaging.BitmapDecoder]::CreateAsync($stream)) ([Windows.Graphics.Imaging.BitmapDecoder])
$bitmap = Await ($decoder.GetSoftwareBitmapAsync()) ([Windows.Graphics.Imaging.SoftwareBitmap])
$engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromUserProfileLanguages()
if ($null -eq $engine) { [Console]::Error.WriteLine('NOENGINE'); exit 2 }
$result = Await ($engine.RecognizeAsync($bitmap)) ([Windows.Media.Ocr.OcrResult])
($result.Lines | ForEach-Object { $_.Text }) -join ""`n""
";

        public static void Recognize(Bitmap image, Action<string, string> done)
        {
            // texto pequeno rende melhor ampliado
            Bitmap work = image;
            bool scaled = false;
            if (image.Width < 1200 && image.Height < 1200)
            {
                work = new Bitmap(image.Width * 2, image.Height * 2, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(work))
                {
                    g.Clear(Color.White);
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(image, 0, 0, work.Width, work.Height);
                }
                scaled = true;
            }
            string dir = Path.Combine(Path.GetTempPath(), "CapturaTela-ocr-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            string png = Path.Combine(dir, "img.png"), ps1 = Path.Combine(dir, "ocr.ps1");
            work.Save(png, ImageFormat.Png);
            if (scaled) work.Dispose();
            File.WriteAllText(ps1, Script, new UTF8Encoding(true));

            var worker = new System.Threading.Thread(delegate()
            {
                string text = null, error = null;
                try
                {
                    var psi = new ProcessStartInfo("powershell.exe",
                        "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"" + ps1 + "\" -Path \"" + png + "\"");
                    psi.UseShellExecute = false; psi.CreateNoWindow = true;
                    psi.RedirectStandardOutput = true; psi.RedirectStandardError = true;
                    psi.StandardOutputEncoding = Encoding.UTF8; psi.StandardErrorEncoding = Encoding.UTF8;
                    using (var p = Process.Start(psi))
                    {
                        string err = null;
                        var errTask = new System.Threading.Thread(delegate() { err = p.StandardError.ReadToEnd(); });
                        errTask.Start();
                        string output = p.StandardOutput.ReadToEnd();
                        errTask.Join();
                        if (!p.WaitForExit(60000)) { try { p.Kill(); } catch { } error = "Tempo esgotado no OCR."; }
                        else if (p.ExitCode == 2) error = "Nenhum idioma de OCR instalado. Instale um em Configurações > Hora e idioma > Idioma e região.";
                        else if (p.ExitCode != 0) error = string.IsNullOrEmpty(err) ? "Falha no OCR." : err.Trim();
                        else text = output.Trim();
                    }
                }
                catch (Exception ex) { error = ex.Message; }
                finally { try { Directory.Delete(dir, true); } catch { } }
                done(text, error);
            });
            worker.IsBackground = true;
            worker.Start();
        }
    }

    // Mostra o texto reconhecido.
    class OcrResultForm : Form
    {
        public OcrResultForm(string text)
        {
            Text = "Texto reconhecido (copiado)";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(520, 360);
            MinimumSize = new Size(360, 240);
            Font = new Font("Segoe UI", 9.5f);
            ShowInTaskbar = false;

            var box = new TextBox
            {
                Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill, Text = text.Replace("\r\n", "\n").Replace("\n", "\r\n")
            };
            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 44 };
            var copy = new Button { Text = "Copiar de novo", Left = 12, Top = 8, Width = 120, Height = 28 };
            copy.Click += delegate { try { Clipboard.SetText(box.Text); } catch { } };
            var close = new Button { Text = "Fechar", Width = 90, Height = 28, Top = 8, Anchor = AnchorStyles.Top | AnchorStyles.Right, DialogResult = DialogResult.OK };
            close.Left = bottom.Width - close.Width - 12;
            bottom.Controls.Add(copy); bottom.Controls.Add(close);
            Controls.Add(box); Controls.Add(bottom);
            AcceptButton = close; CancelButton = close;
            box.Select(0, 0);
            Theme.ScaleForDpi(this);
            Theme.Apply(this);
        }
    }
}
