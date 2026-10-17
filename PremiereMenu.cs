using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace SpaceMouseBridge
{
    public sealed class PremiereMenuItem
    {
        public string Name { get; set; }
        public PremiereShortcut Shortcut { get; set; }

        internal static void Validate(PremiereMenuItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Name) ||
                item.Name.Any(char.IsControl) || item.Shortcut == null)
                throw new InvalidDataException("Jeder Menueeintrag benoetigt einen Namen ohne Zeilenumbrueche und ein Tastenkuerzel.");
            item.Shortcut.Validate();
        }
    }

    internal sealed class PremiereMenuEditor : ThemedForm
    {
        private readonly DataGridView rows = new DataGridView {
            Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
            RowHeadersVisible = false, MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };
        private readonly Label feedback = new Label { Dock = DockStyle.Fill, ForeColor = Color.DarkRed };
        internal PremiereMenuItem[] Items;

        internal PremiereMenuEditor(PremiereMenuItem[] items)
        {
            Text = "Premiere - Bridge-Menue bearbeiten";
            Size = new Size(780, 540); MinimumSize = new Size(640, 400);
            StartPosition = FormStartPosition.CenterParent;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16),
                ColumnCount = 1, RowCount = 5 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            layout.Controls.Add(new Label { Dock = DockStyle.Fill, Text =
                "Eigene Namen und Premiere-Tastenkuerzel. Reihenfolge entspricht dem Popup-Menue.\r\n" +
                "Leer ist erlaubt. Erst Speichern im uebergeordneten Mapping-Editor uebernimmt den Entwurf." });
            rows.Columns.Add("Name", "Menueeintrag");
            rows.Columns.Add("Shortcut", "Tastenkuerzel (Aufzeichnen)");
            rows.Columns[1].ReadOnly = true;
            foreach (var item in items ?? new PremiereMenuItem[0])
                rows.Rows[rows.Rows.Add(item.Name, item.Shortcut.ToString())].Tag = item.Shortcut;
            layout.Controls.Add(RoundedSurface.Wrap(rows));
            var tools = new FlowLayoutPanel { Dock = DockStyle.Fill, Tag = "panel" };
            var add = new RoundedButton { Text = "Hinzufuegen", AutoSize = true };
            var remove = new RoundedButton { Text = "Entfernen", AutoSize = true };
            var capture = new RoundedButton { Text = "Kuerzel aufzeichnen", AutoSize = true };
            var up = new RoundedButton { Text = "Nach oben", AutoSize = true };
            var down = new RoundedButton { Text = "Nach unten", AutoSize = true };
            tools.Controls.AddRange(new Control[] { add, remove, capture, up, down });
            layout.Controls.Add(tools); layout.Controls.Add(feedback);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Tag = "panel" };
            var cancel = new RoundedButton { Text = "Abbrechen", AutoSize = true, DialogResult = DialogResult.Cancel };
            var accept = new RoundedButton { Text = "Entwurf uebernehmen", AutoSize = true, Tag = "primary" };
            actions.Controls.AddRange(new Control[] { cancel, accept });
            layout.Controls.Add(actions);
            Controls.Add(layout); CancelButton = cancel;
            add.Click += delegate {
                int index = rows.Rows.Add("", "Kuerzel fehlt");
                rows.CurrentCell = rows.Rows[index].Cells[0]; rows.BeginEdit(true);
            };
            remove.Click += delegate { if (rows.CurrentRow != null) rows.Rows.Remove(rows.CurrentRow); };
            capture.Click += delegate { CaptureShortcut(); };
            rows.CellDoubleClick += delegate(object sender, DataGridViewCellEventArgs e) {
                if (e.RowIndex >= 0 && e.ColumnIndex == 1) CaptureShortcut();
            };
            up.Click += delegate { MoveItem(-1); };
            down.Click += delegate { MoveItem(1); };
            accept.Click += delegate {
                try { Items = Collect(); DialogResult = DialogResult.OK; Close(); }
                catch (InvalidDataException ex) { feedback.Text = ex.Message; }
            };
        }

        private void CaptureShortcut()
        {
            if (rows.CurrentRow == null) { feedback.Text = "Bitte zuerst einen Eintrag hinzufuegen oder auswaehlen."; return; }
            rows.EndEdit();
            using (var dialog = new PremiereMappingEditor.ShortcutCapture())
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                rows.CurrentRow.Tag = dialog.Shortcut;
                rows.CurrentRow.Cells[1].Value = dialog.Shortcut.ToString();
                feedback.Text = "";
            }
        }

        private void MoveItem(int direction)
        {
            if (rows.CurrentRow == null) return;
            rows.EndEdit();
            int index = rows.CurrentRow.Index, target = index + direction;
            if (target < 0 || target >= rows.Rows.Count) return;
            DataGridViewRow row = rows.CurrentRow;
            rows.Rows.RemoveAt(index); rows.Rows.Insert(target, row);
            rows.CurrentCell = row.Cells[0];
        }

        internal PremiereMenuItem[] Collect()
        {
            rows.EndEdit();
            var result = rows.Rows.Cast<DataGridViewRow>().Select(row => new PremiereMenuItem {
                Name = Convert.ToString(row.Cells[0].Value).Trim(), Shortcut = (PremiereShortcut)row.Tag
            }).ToArray();
            foreach (var item in result) PremiereMenuItem.Validate(item);
            return result;
        }
    }

    internal sealed class PremiereMenuPopup : ThemedForm
    {
        internal PremiereShortcut SelectedShortcut;
        private bool shown;

        internal PremiereMenuPopup(PremiereMenuItem[] items, Point position)
        {
            Text = "Premiere - Bridge-Menue";
            ShowInTaskbar = false; MinimizeBox = false; MaximizeBox = false;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            StartPosition = FormStartPosition.Manual;
            var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Tag = "panel",
                WrapContents = false, AutoScroll = true, Padding = new Padding(8) };
            panel.Controls.Add(new Label { Text = "PREMIERE  /  BRIDGE-MENUE", Width = 384, Height = 28, Tag = "section" });
            var entries = items ?? new PremiereMenuItem[0];
            if (entries.Length == 0)
                panel.Controls.Add(new Label { Width = 384, Height = 80,
                    Text = "Das Menue ist noch leer.\r\nEintraege unter Premiere > Mappings bearbeiten > Kontextmenue bearbeiten anlegen." });
            foreach (var item in entries)
            {
                PremiereMenuItem.Validate(item);
                var button = new RoundedButton { Text = item.Name + "    (" + item.Shortcut + ")",
                    Width = 384, Height = 40, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true,
                    UseMnemonic = false };
                var shortcut = item.Shortcut;
                button.Click += delegate {
                    if (Native.GetForegroundWindow() != Handle) return;
                    SelectedShortcut = shortcut; DialogResult = DialogResult.OK; Close();
                };
                panel.Controls.Add(button);
            }
            Controls.Add(panel);
            Rectangle area = Screen.FromPoint(position).WorkingArea;
            Size = new Size(Math.Min(440, area.Width), Math.Min(area.Height, entries.Length == 0 ? 170 :
                (int)Math.Min(620L, 90L + entries.Length * 46L)));
            foreach (Control control in panel.Controls)
                control.Width = Math.Max(80, ClientSize.Width - 38);
            Location = new Point(Math.Max(area.Left, Math.Min(position.X, area.Right - Width)),
                Math.Max(area.Top, Math.Min(position.Y, area.Bottom - Height)));
            Shown += delegate { shown = true; };
            Deactivate += delegate { if (shown && !IsDisposed) Close(); };
        }

        protected override bool ProcessCmdKey(ref Message message, Keys keyData)
        {
            if (keyData == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); return true; }
            return base.ProcessCmdKey(ref message, keyData);
        }
    }
}
