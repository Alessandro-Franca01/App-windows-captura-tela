using System;
using System.Windows.Forms;

namespace CapturaTela
{
    // Botao de barra que abre um menu de opcoes (modo, atraso, espessura) e mostra a escolhida.
    // Substitui os ComboBox: o menu segue o tema, mostra icones e atalhos e tem alvos grandes.
    class ChoiceButton : ToolStripDropDownButton
    {
        readonly string[] names;
        readonly ToolStripMenuItem[] items;
        int selected = -1;
        readonly bool showText;

        public event EventHandler SelectedIndexChanged;

        // tagFor: icone (IconKind) ou amostra (WidthSample) de cada opcao; shortcuts: texto do atalho de cada opcao (ou null)
        public ChoiceButton(string[] names, Func<int, object> tagFor, string[] shortcuts, int iconPx, bool showText, bool itemIcons)
        {
            this.names = names;
            this.showText = showText;
            items = new ToolStripMenuItem[names.Length];
            DisplayStyle = showText ? ToolStripItemDisplayStyle.ImageAndText : ToolStripItemDisplayStyle.Image;
            ShowDropDownArrow = true;
            ImageScaling = ToolStripItemImageScaling.None;
            Image = Icons.Placeholder(iconPx);
            TextImageRelation = TextImageRelation.ImageBeforeText;
            DropDown.ImageScalingSize = new System.Drawing.Size(iconPx, iconPx);

            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                var mi = new ToolStripMenuItem(names[i]);
                mi.Tag = tagFor == null ? null : tagFor(i);
                if (itemIcons) { mi.Image = Icons.Placeholder(iconPx); mi.ImageScaling = ToolStripItemImageScaling.None; }
                if (shortcuts != null && shortcuts[i] != null) mi.ShortcutKeyDisplayString = shortcuts[i];
                mi.Click += delegate { SelectedIndex = index; };
                items[i] = mi;
                DropDownItems.Add(mi);
            }
        }

        public int SelectedIndex
        {
            get { return selected; }
            set
            {
                if (value < 0 || value >= names.Length || value == selected) return;
                selected = value;
                if (showText) Text = names[value];
                if (items[value].Tag != null) Tag = items[value].Tag;
                for (int i = 0; i < items.Length; i++) items[i].Name = i == value ? "sel" : "item";
                if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty);
            }
        }
    }
}
