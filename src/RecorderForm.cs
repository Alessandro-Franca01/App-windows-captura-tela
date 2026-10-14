using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace CapturaTela
{
    // Painel de gravacao de GIF: mostra a moldura vermelha ao redor da area (fora dela, para
    // nao aparecer no video) e um pequeno painel com Iniciar / Parar.
    class RecorderForm : Form, IStoppable
    {
        const int Fps = 10, MaxSeconds = 30, MaxWidth = 960, FrameBorder = 3;

        public readonly List<Bitmap> Frames = new List<Bitmap>();
        public int DelayMs = 1000 / Fps;

        readonly Rectangle region;
        readonly RegionFrame frame;
        readonly Timer timer = new Timer();
        readonly Stopwatch clock = new Stopwatch();
        readonly Label info = new Label();
        readonly FlatButton startBtn = new FlatButton();
        readonly FlatButton cancelBtn = new FlatButton();
        readonly Size outSize;
        bool recording;

        public RecorderForm(Rectangle region)
        {
            this.region = region;
            float scale = region.Width > MaxWidth ? (float)MaxWidth / region.Width : 1f;
            outSize = new Size(Math.Max(1, (int)(region.Width * scale)), Math.Max(1, (int)(region.Height * scale)));

            Text = "Gravar GIF";
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            ClientSize = new Size(360, 96);
            Font = new Font("Segoe UI", 9.5f);
            KeyPreview = true;

            info.Text = "Pronto para gravar · " + region.Width + " × " + region.Height + " px";
            info.Left = 14; info.Top = 12; info.Width = 332; info.Height = 22;
            info.Font = new Font("Segoe UI Semibold", 9.5f);
            startBtn.Text = "● Iniciar gravação"; startBtn.Left = 14; startBtn.Top = 46; startBtn.Width = 190; startBtn.Height = 36; startBtn.Variant = ButtonVariant.Danger;
            cancelBtn.Text = "Cancelar"; cancelBtn.Left = 212; cancelBtn.Top = 46; cancelBtn.Width = 134; cancelBtn.Height = 36;
            startBtn.Click += delegate { if (recording) StopRecording(); else StartRecording(); };
            cancelBtn.Click += delegate { Frames.Clear(); DialogResult = DialogResult.Cancel; Close(); };
            Controls.AddRange(new Control[] { info, startBtn, cancelBtn });

            frame = new RegionFrame(region, FrameBorder);
            timer.Interval = 1000 / Fps;
            timer.Tick += delegate { Tick(); };

            // painel abaixo da area; se nao couber, acima; por ultimo, dentro dela
            var wa = Screen.FromRectangle(region).WorkingArea;
            int x = Math.Max(wa.Left, Math.Min(region.Left, wa.Right - Width));
            int below = region.Bottom + FrameBorder + 8;
            int above = region.Top - FrameBorder - 8 - Height;
            int y = below + Height <= wa.Bottom ? below : above >= wa.Top ? above : wa.Bottom - Height - 8;
            Location = new Point(x, y);
            Theme.ScaleForDpi(this);
            Theme.Apply(this);
            info.ForeColor = Theme.Ink;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            frame.Show();
            Activate();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            timer.Stop(); timer.Dispose();
            frame.Close();
            base.OnFormClosed(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) { if (recording) StopRecording(); else cancelBtn.PerformClick(); }
            base.OnKeyDown(e);
        }

        public void RequestStop() { if (recording) StopRecording(); }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.ExcludeFromCapture(Handle);   // o painel nunca aparece no GIF
        }

        void StartRecording()
        {
            recording = true;
            startBtn.Text = "■ Parar gravação";
            cancelBtn.Enabled = false;
            info.ForeColor = Theme.Danger;
            clock.Restart();
            CaptureFrame();
            timer.Start();
        }

        void Tick()
        {
            if (!recording) return;
            CaptureFrame();
            int secs = (int)clock.Elapsed.TotalSeconds;
            info.Text = "● REC  " + Clock(secs) + " / " + Clock(MaxSeconds) + "   (Esc ou o atalho para parar)";
            if (clock.Elapsed.TotalSeconds >= MaxSeconds) StopRecording();
        }

        static string Clock(int totalSeconds) { return (totalSeconds / 60).ToString("00") + ":" + (totalSeconds % 60).ToString("00"); }

        void StopRecording()
        {
            recording = false;
            timer.Stop(); clock.Stop();
            if (Frames.Count > 0) DelayMs = (int)Math.Max(20, clock.ElapsedMilliseconds / Frames.Count);
            DialogResult = Frames.Count > 1 ? DialogResult.OK : DialogResult.Cancel;
            Close();
        }

        void CaptureFrame()
        {
            using (var raw = new Bitmap(region.Width, region.Height, PixelFormat.Format32bppRgb))
            {
                using (var g = Graphics.FromImage(raw))
                {
                    g.CopyFromScreen(region.Location, Point.Empty, region.Size, CopyPixelOperation.SourceCopy);
                    DrawPointer(g, Cursor.Position.X - region.X, Cursor.Position.Y - region.Y);
                }
                var small = new Bitmap(outSize.Width, outSize.Height, PixelFormat.Format24bppRgb);
                using (var g = Graphics.FromImage(small))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(raw, new Rectangle(Point.Empty, outSize), 0, 0, raw.Width, raw.Height, GraphicsUnit.Pixel);
                }
                Frames.Add(small);
            }
        }

        // ponteiro do mouse (o GDI nao o inclui na copia de tela)
        internal static void DrawPointer(Graphics g, int x, int y)
        {
            var pts = new Point[] { new Point(x, y), new Point(x, y + 17), new Point(x + 4, y + 13), new Point(x + 8, y + 20), new Point(x + 11, y + 19), new Point(x + 7, y + 12), new Point(x + 12, y + 12) };
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.FillPolygon(Brushes.White, pts);
            g.DrawPolygon(Pens.Black, pts);
        }

    }
}
