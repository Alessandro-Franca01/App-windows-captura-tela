using System;
using System.Drawing;
using System.Windows.Forms;

namespace CapturaTela
{
    // Pequena janela para digitar o texto de uma anotacao.
    class TextPrompt : Form
    {
        readonly TextBox box = new TextBox();

        TextPrompt()
        {
            Text = "Texto da anotacao";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(360, 150);
            Font = new Font("Segoe UI", 9);

            box.Multiline = true; box.AcceptsReturn = true;
            box.Left = 12; box.Top = 12; box.Width = 336; box.Height = 90;
            Controls.Add(box);
            var hint = new Label { Text = "Enter = nova linha. Ctrl+Enter confirma.", Left = 12, Top = 112, Width = 220, ForeColor = SystemColors.GrayText };
            Controls.Add(hint);
            var ok = new Button { Text = "OK", Left = 273, Top = 112, Width = 75, DialogResult = DialogResult.OK };
            Controls.Add(ok);
            AcceptButton = null;
            box.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.Control && e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; DialogResult = DialogResult.OK; }
            };
            Theme.Apply(this);
        }

        public static string Ask(IWin32Window owner)
        {
            using (var f = new TextPrompt())
            {
                return f.ShowDialog(owner) == DialogResult.OK ? f.box.Text.TrimEnd() : null;
            }
        }
    }
}
