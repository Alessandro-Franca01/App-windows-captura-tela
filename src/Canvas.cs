using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace CapturaTela
{
    // Area de edicao: mostra a imagem capturada (com zoom) e as anotacoes por cima.
    class Canvas : Control
    {
        public Bitmap Image { get; private set; }
        public readonly List<Annotation> Annotations = new List<Annotation>();
        public string Tool = "pen";
        public Color PenColor = Color.Red;
        public float PenWidth = 4;
        public float Zoom = 1f;

        public event Action LayoutChanged;          // tamanho da imagem ou zoom mudaram
        public event Action<Color> ColorPicked;
        public event Action HistoryChanged;

        readonly List<IEdit> undo = new List<IEdit>();
        readonly List<IEdit> redo = new List<IEdit>();

        Annotation draft;          // anotacao em construcao (arrastando o mouse)
        StrokeAnn stroke;
        Point dragStart;
        Rectangle cropDraft = Rectangle.Empty;
        bool dragging;

        public bool CanUndo { get { return undo.Count > 0; } }
        public bool CanRedo { get { return redo.Count > 0; } }

        public Canvas()
        {
            DoubleBuffered = true;
            TabStop = true;
            SetStyle(ControlStyles.Selectable, true);
        }

        // ---- imagem / zoom ----

        public void SetImage(Bitmap bmp)
        {
            Image = bmp;
            Annotations.Clear(); undo.Clear(); redo.Clear();
            draft = null; stroke = null; cropDraft = Rectangle.Empty;
            Zoom = 1f;
            ApplySize();
            if (HistoryChanged != null) HistoryChanged();
        }

        public void ReplaceImage(Bitmap bmp)
        {
            Image = bmp;
            ApplySize();
        }

        void ApplySize()
        {
            Size = Image == null ? new Size(1, 1)
                 : new Size(Math.Max(1, (int)Math.Round(Image.Width * Zoom)), Math.Max(1, (int)Math.Round(Image.Height * Zoom)));
            Invalidate();
            if (LayoutChanged != null) LayoutChanged();
        }

        public void SetZoom(float z)
        {
            Zoom = Math.Max(0.1f, Math.Min(8f, z));
            ApplySize();
        }

        Point ToImage(Point p) { return new Point((int)Math.Floor(p.X / Zoom), (int)Math.Floor(p.Y / Zoom)); }

        public Bitmap Flatten()
        {
            var bmp = new Bitmap(Image.Width, Image.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.DrawImageUnscaled(Image, 0, 0);
                DrawAnnotations(g);
            }
            return bmp;
        }

        void DrawAnnotations(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            foreach (var a in Annotations) a.Draw(g, Image);
            if (draft != null) draft.Draw(g, Image);
        }

        // ---- historico ----

        void Push(IEdit e)
        {
            undo.Add(e); redo.Clear();
            if (HistoryChanged != null) HistoryChanged();
        }

        public void Undo()
        {
            if (undo.Count == 0) return;
            var e = undo[undo.Count - 1]; undo.RemoveAt(undo.Count - 1);
            e.Undo(this); redo.Add(e);
            Invalidate();
            if (HistoryChanged != null) HistoryChanged();
        }

        public void Redo()
        {
            if (redo.Count == 0) return;
            var e = redo[redo.Count - 1]; redo.RemoveAt(redo.Count - 1);
            e.Redo(this); undo.Add(e);
            Invalidate();
            if (HistoryChanged != null) HistoryChanged();
        }

        // ---- pintura ----

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Image == null) return;
            var g = e.Graphics;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.InterpolationMode = Zoom >= 1f ? InterpolationMode.NearestNeighbor : InterpolationMode.HighQualityBicubic;
            g.ScaleTransform(Zoom, Zoom);
            g.DrawImage(Image, 0, 0, Image.Width, Image.Height);
            DrawAnnotations(g);

            if (!cropDraft.IsEmpty)
            {
                var full = new Rectangle(0, 0, Image.Width, Image.Height);
                using (var dim = new SolidBrush(Color.FromArgb(130, 0, 0, 0)))
                using (var region = new Region(full))
                {
                    region.Exclude(cropDraft);
                    g.FillRegion(dim, region);
                }
                using (var pen = new Pen(Color.White, 1 / Zoom)) { pen.DashStyle = DashStyle.Dash; g.DrawRectangle(pen, cropDraft); }
            }
        }

        // ---- mouse ----

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            if (Form.ActiveForm == FindForm()) Focus();
        }

        Annotation Create(Point p)
        {
            switch (Tool)
            {
                case "arrow": return new ShapeAnn { Kind = ShapeKind.Arrow, A = p, B = p, Color = PenColor, Width = PenWidth };
                case "line": return new ShapeAnn { Kind = ShapeKind.Line, A = p, B = p, Color = PenColor, Width = PenWidth };
                case "rect": return new ShapeAnn { Kind = ShapeKind.Rectangle, A = p, B = p, Color = PenColor, Width = PenWidth };
                case "ellipse": return new ShapeAnn { Kind = ShapeKind.Ellipse, A = p, B = p, Color = PenColor, Width = PenWidth };
                case "blur": return new BlurAnn { Rect = new Rectangle(p, Size.Empty), Pixelate = false, Width = PenWidth };
                case "pixelate": return new BlurAnn { Rect = new Rectangle(p, Size.Empty), Pixelate = true, Width = PenWidth };
            }
            return null;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (Image == null || e.Button != MouseButtons.Left) return;
            var p = ToImage(e.Location);
            dragStart = p; dragging = true;

            switch (Tool)
            {
                case "pen":
                case "highlighter":
                    bool hl = Tool == "highlighter";
                    stroke = new StrokeAnn { Color = PenColor, Highlight = hl, Width = hl ? Math.Max(14, PenWidth * 4) : PenWidth };
                    stroke.Points.Add(p);
                    draft = stroke;
                    break;
                case "eraser":
                    EraseAt(p);
                    break;
                case "picker":
                    PickColorAt(p);
                    dragging = false;
                    break;
                case "text":
                    dragging = false;
                    AddText(p);
                    break;
                case "step":
                    dragging = false;
                    int n = 1;
                    foreach (var a in Annotations) { var s = a as StepAnn; if (s != null && s.Number >= n) n = s.Number + 1; }
                    var step = new StepAnn { Center = p, Number = n, Color = PenColor, Width = PenWidth };
                    Annotations.Add(step); Push(new AddEdit(step));
                    break;
                case "crop":
                    cropDraft = new Rectangle(p, Size.Empty);
                    break;
                default:
                    draft = Create(p);
                    break;
            }
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (!dragging || Image == null) return;
            var p = ToImage(e.Location);
            bool shift = (Control.ModifierKeys & Keys.Shift) == Keys.Shift;

            if (Tool == "eraser") { EraseAt(p); return; }
            if (Tool == "crop")
            {
                cropDraft = Annotation.Normalize(dragStart, p);
                cropDraft.Intersect(new Rectangle(0, 0, Image.Width, Image.Height));
                Invalidate();
                return;
            }
            if (stroke != null) { stroke.Points.Add(p); Invalidate(); return; }

            var shape = draft as ShapeAnn;
            if (shape != null)
            {
                if (shift) p = Constrain(shape.A, p, shape.Kind);
                shape.B = p;
            }
            var blur = draft as BlurAnn;
            if (blur != null) blur.Rect = Annotation.Normalize(dragStart, p);
            Invalidate();
        }

        // Shift: linhas/setas em passos de 45 graus; retangulos/elipses viram quadrado/circulo.
        static Point Constrain(Point a, Point b, ShapeKind kind)
        {
            int dx = b.X - a.X, dy = b.Y - a.Y;
            if (kind == ShapeKind.Rectangle || kind == ShapeKind.Ellipse)
            {
                int m = Math.Max(Math.Abs(dx), Math.Abs(dy));
                return new Point(a.X + Math.Sign(dx) * m, a.Y + Math.Sign(dy) * m);
            }
            double ang = Math.Round(Math.Atan2(dy, dx) / (Math.PI / 4)) * (Math.PI / 4);
            double len = Math.Sqrt(dx * dx + dy * dy);
            return new Point(a.X + (int)Math.Round(len * Math.Cos(ang)), a.Y + (int)Math.Round(len * Math.Sin(ang)));
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (!dragging || e.Button != MouseButtons.Left) return;
            dragging = false;

            if (Tool == "crop")
            {
                var r = cropDraft; cropDraft = Rectangle.Empty;
                if (r.Width >= 5 && r.Height >= 5) ApplyCrop(r);
                Invalidate();
                return;
            }

            var done = draft; draft = null; stroke = null;
            if (done == null) return;
            var shape = done as ShapeAnn;
            if (shape != null && Math.Abs(shape.A.X - shape.B.X) + Math.Abs(shape.A.Y - shape.B.Y) < 3) { Invalidate(); return; }
            var blur = done as BlurAnn;
            if (blur != null && (blur.Rect.Width < 3 || blur.Rect.Height < 3)) { Invalidate(); return; }
            Annotations.Add(done);
            Push(new AddEdit(done));
            Invalidate();
        }

        void EraseAt(Point p)
        {
            for (int i = Annotations.Count - 1; i >= 0; i--)
            {
                if (Annotations[i].Hit(p))
                {
                    var a = Annotations[i];
                    Annotations.RemoveAt(i);
                    Push(new RemoveEdit(a, i));
                    Invalidate();
                    return;
                }
            }
        }

        void PickColorAt(Point p)
        {
            if (p.X < 0 || p.Y < 0 || p.X >= Image.Width || p.Y >= Image.Height) return;
            using (var flat = Flatten())
            {
                var c = flat.GetPixel(p.X, p.Y);
                if (c.A == 0) return;
                PenColor = Color.FromArgb(255, c);
                if (ColorPicked != null) ColorPicked(PenColor);
            }
        }

        void AddText(Point p)
        {
            string text = TextPrompt.Ask(FindForm());
            if (string.IsNullOrEmpty(text)) return;
            var t = new TextAnn { Pos = p, Text = text, Color = PenColor, Width = PenWidth };
            Annotations.Add(t); Push(new AddEdit(t));
            Invalidate();
        }

        void ApplyCrop(Rectangle r)
        {
            var before = Image;
            // corta a imagem base; as anotacoes sao deslocadas para acompanhar
            var after = before.Clone(r, PixelFormat.Format32bppArgb);
            ReplaceImage(after);
            foreach (var a in Annotations) a.Offset(-r.X, -r.Y);
            Push(new CropEdit(before, after, r));
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape && dragging)
            {
                dragging = false; draft = null; stroke = null; cropDraft = Rectangle.Empty;
                Invalidate();
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            return keyData == Keys.Escape || base.IsInputKey(keyData);
        }
    }
}

