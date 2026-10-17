using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml;

namespace SpaceMouseBridge
{
    internal sealed class Choice<T>
    {
        public T Value { get; set; }
        public string Text { get; set; }
        internal Choice(T value, string text) { Value = value; Text = text; }
    }

    internal sealed class MappingEditor : ThemedForm
    {
        internal static Panel MenuOptions(FlowLayoutPanel fields, ComboBox choice, RoundedButton edit,
            int rightInset, int gap, int choiceTop)
        {
            var panel = new Panel { Dock = DockStyle.Fill, Tag = "panel" };
            edit.MinimumSize = new Size(153, 34);
            fields.Dock = DockStyle.None;
            fields.Margin = Padding.Empty;
            fields.Location = Point.Empty;
            fields.WrapContents = false;
            panel.Controls.Add(fields);
            panel.Controls.Add(choice);
            panel.Controls.Add(edit);
            panel.Layout += delegate {
                int fieldsWidth = fields.GetPreferredSize(Size.Empty).Width;
                int editX = panel.ClientSize.Width - rightInset - edit.Width;
                int choiceX = editX - gap - choice.Width;
                bool secondRow = choiceX < fieldsWidth + 8;
                int top = secondRow ? 33 : 0;
                fields.Bounds = new Rectangle(0, 0, secondRow ? fieldsWidth : choiceX - 8, 36);
                if (secondRow) fields.Height = 33;
                choice.Location = new Point(Math.Max(3, choiceX), top + choiceTop);
                edit.Location = new Point(Math.Max(choice.Right + gap, editX), top + 3);
            };
            return panel;
        }

        private readonly MappingStore store;
        private readonly DataGridView axes = Grid();
        private readonly CheckBox enabled = new CheckBox { Text = "Bridge-Ausgabe in Chrome aktivieren", AutoSize = true };
        private readonly NumericUpDown threshold = new NumericUpDown { Minimum = 15, Maximum = 95, Width = 60 };
        private readonly NumericUpDown neutral = new NumericUpDown { Minimum = 1, Maximum = 30, Width = 60 };
        private PremiereMenuItem[] menuItems;
        private int menuRadius;
        private int menuCenterPercent;
        private decimal menuBorderWidth;
        private decimal menuSegmentGap;
        private decimal menuTextSize;
        private decimal menuIconSize;
        private RadialMenuConfiguration menu2;
        private readonly Label feedback = new Label { Dock = DockStyle.Fill, ForeColor = Color.DarkRed, AutoSize = true };
        private string driverHash;
        private string settingsHash;
        private ApplicationMapping current;
        private bool needsReload;

        private static readonly List<Choice<BrowserAction>> Actions = new List<Choice<BrowserAction>> {
            new Choice<BrowserAction>(BrowserAction.None, "Keine Aktion"),
            new Choice<BrowserAction>(BrowserAction.Back, "Zurueck"),
            new Choice<BrowserAction>(BrowserAction.Forward, "Vorwaerts"),
            new Choice<BrowserAction>(BrowserAction.PreviousTab, "Vorheriger Tab"),
            new Choice<BrowserAction>(BrowserAction.NextTab, "Naechster Tab"),
            new Choice<BrowserAction>(BrowserAction.ZoomIn, "Zoom + (ein Schritt)"),
            new Choice<BrowserAction>(BrowserAction.ZoomOut, "Zoom - (ein Schritt)"),
            new Choice<BrowserAction>(BrowserAction.ResetZoom, "Zoom auf 100 %"),
            new Choice<BrowserAction>(BrowserAction.Reload, "Seite neu laden"),
            new Choice<BrowserAction>(BrowserAction.Home, "Seitenanfang"),
            new Choice<BrowserAction>(BrowserAction.End, "Seitenende"),
            new Choice<BrowserAction>(BrowserAction.ContextMenu, "Menue 1 oeffnen"),
            new Choice<BrowserAction>(BrowserAction.ContextMenu2, "Menue 2 oeffnen")
        };
        private static readonly List<Choice<BrowserAction>> AxisActions =
            Actions.Where(a => a.Value != BrowserAction.ContextMenu && a.Value != BrowserAction.ContextMenu2).ToList();

        internal MappingEditor(MappingStore mappingStore, ApplicationMapping active)
        {
            store = mappingStore;
            current = active.Clone();
            menuItems = current.ChromeMenu;
            menuRadius = Math.Min(180, current.MenuRadius);
            menuCenterPercent = current.MenuCenterPercent;
            menuBorderWidth = current.MenuBorderWidth;
            menuSegmentGap = current.MenuSegmentGap;
            menuTextSize = current.MenuTextSize;
            menuIconSize = current.MenuIconSize;
            menu2 = current.Menu2;
            driverHash = MappingStore.Hash(store.ReadDriver());
            settingsHash = MappingStore.Hash(MappingStore.Read(store.SettingsPath));
            needsReload = current.DriverHash == null || current.DriverHash != driverHash;
            Text = store.Profile.Name + " - SpaceMouse-Mappings";
            enabled.Text = "Bridge-Ausgabe in " + store.Profile.Name + " aktivieren";
            Size = new Size(1220, 810);
            MinimumSize = new Size(1080, 780);
            StartPosition = FormStartPosition.CenterParent;
            var layout = new TableLayoutPanel {
                Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 6
            };
            foreach (int height in new[] { 88, 76, 320, 105 })
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            layout.Controls.Add(new Label {
                Dock = DockStyle.Fill,
                Text = (store.Profile.SupportsActions ?
                    "Profil: Google Chrome (chrome.exe)\r\nTreiber: kontinuierliches Scrollen/Zoom. Bridge: Einzelaktionen beim Loslassen, Menue 1/2 sofort beim Druecken.\r\nUebernehmen sichert Dateien und deaktiviert die Treiberausgabe fuer Bridge-belegte Eingaben." :
                    "Profil: " + store.Profile.Name + " (" + store.Profile.Executable + ")\r\nVorbereitetes Leerprofil: keine Bridge-Aktionen und kein Eingriff in den 3Dconnexion-Treiber.\r\nDie Funktionen fuer Premiere werden im naechsten Schritt gemeinsam festgelegt.") +
                    "\r\n" + ApplicationProfile.DriverFunctionNotice
            });
            var options = new FlowLayoutPanel { Dock = DockStyle.Fill, Tag = "panel" };
            options.Controls.Add(enabled);
            options.Controls.Add(new Label { Text = "Bridge-Ausloesung (%)", AutoSize = true, Padding = new Padding(12, 4, 0, 0) });
            options.Controls.Add(threshold);
            options.Controls.Add(new Label { Text = "Neutralzone (%)", AutoSize = true, Padding = new Padding(12, 4, 0, 0) });
            options.Controls.Add(neutral);
            var menuChoice = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
            menuChoice.Items.AddRange(new object[] { "Menue 1", "Menue 2" });
            menuChoice.SelectedIndex = 0;
            var editMenu = new RoundedButton { Text = "Menue bearbeiten", AutoSize = true };
            editMenu.Click += delegate {
                if (menuChoice.SelectedIndex == 1)
                {
                    var configuration = menu2 ?? new RadialMenuConfiguration();
                    using (var editor = new PremiereMenuEditor(configuration.Items, "Chrome - Menue 2",
                        configuration.Radius, configuration.CenterPercent, configuration.BorderWidth,
                        configuration.SegmentGap, configuration.TextSize, configuration.IconSize))
                        if (editor.ShowDialog(this) == DialogResult.OK) menu2 = editor.CollectConfiguration();
                    return;
                }
                using (var editor = new PremiereMenuEditor(menuItems, "Chrome - Menue 1", menuRadius, menuCenterPercent, menuBorderWidth, menuSegmentGap, menuTextSize, menuIconSize))
                    if (editor.ShowDialog(this) == DialogResult.OK)
                    {
                        menuItems = editor.Items;
                        menuRadius = editor.MenuRadius;
                        menuCenterPercent = editor.MenuCenterPercent;
                        menuBorderWidth = editor.MenuBorderWidth;
                        menuSegmentGap = editor.MenuSegmentGap;
                        menuTextSize = editor.MenuTextSize;
                        menuIconSize = editor.MenuIconSize;
                    }
            };
            layout.Controls.Add(MenuOptions(options, menuChoice, editMenu, 33, 40, 8));
            axes.Columns.Add(new DataGridViewTextBoxColumn { Name = "Axis", HeaderText = "SpaceMouse-Eingabe", ReadOnly = true, FillWeight = 135 });
            axes.Columns.Add(Combo("Mode", "Ausgabe / Funktion", new List<Choice<AxisMode>> {
                new Choice<AxisMode>(AxisMode.KeepDriver, "Treiber unveraendert"),
                new Choice<AxisMode>(AxisMode.Disabled, "Deaktiviert"),
                new Choice<AxisMode>(AxisMode.Scroll, "Treiber: Scrollen"),
                new Choice<AxisMode>(AxisMode.HorizontalScroll, "Treiber: Shift+Scrollen"),
                new Choice<AxisMode>(AxisMode.Zoom, "Treiber: Zoom"),
                new Choice<AxisMode>(AxisMode.Bridge, "Bridge: Einzelaktionen")
            }));
            axes.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Reverse", HeaderText = "Umkehren", FillWeight = 55 });
            axes.Columns.Add(new DataGridViewTextBoxColumn { Name = "Scale", HeaderText = "Verstaerkung", FillWeight = 65 });
            axes.Columns.Add(new DataGridViewTextBoxColumn { Name = "Deadband", HeaderText = "Totzone (HID)", FillWeight = 65 });
            axes.Columns.Add(Combo("Negative", "Negative Richtung", AxisActions));
            axes.Columns.Add(Combo("Positive", "Positive Richtung / Tastenaktion", AxisActions));
            axes.CurrentCellDirtyStateChanged += delegate {
                if (axes.IsCurrentCellDirty && (axes.CurrentCell is DataGridViewComboBoxCell || axes.CurrentCell is DataGridViewCheckBoxCell))
                    axes.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            axes.CellValueChanged += delegate { UpdateEditable(); };
            layout.Controls.Add(RoundedSurface.Wrap(axes));
            layout.Controls.Add(new Label {
                Dock = DockStyle.Fill,
                Margin = new Padding(3, 43, 3, 0),
                Text = store.Profile.SupportsActions ?
                    "Treiber: Verstaerkung 0,05 bis 5,00; Totzone 0 bis 250. Umkehren gilt nur fuer Treiberausgabe.\r\nBridge: Richtungen einzeln zuweisen; ein Schritt pro Bewegung, Freigabe in Neutralstellung.\r\n'Treiber unveraendert' behaelt den aktuellen XML-Zustand bei, stellt fruehere Belegungen nicht wieder her." :
                    "Alle Achsen und Tasten bleiben beim Originaltreiber. Die Bridge sendet in Premiere keine Eingaben.\r\nDie ausgegrauten Felder zeigen nur die gemeinsame Profilstruktur, noch keine Premiere-Funktionen."
            });
            layout.Controls.Add(feedback);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Tag = "panel" };
            var cancel = new RoundedButton { Text = "Abbrechen", AutoSize = true, DialogResult = DialogResult.Cancel };
            var apply = new RoundedButton { Text = "Sichern und uebernehmen", AutoSize = true, Tag = "primary" };
            var reload = new RoundedButton { Text = "Treiber neu einlesen", AutoSize = true };
            var restore = new RoundedButton { Text = "Letzte Aenderung rueckgaengig", AutoSize = true, Enabled = File.Exists(store.LastBackupPath) };
            actions.Controls.Add(cancel);
            actions.Controls.Add(apply);
            actions.Controls.Add(reload);
            actions.Controls.Add(restore);
            layout.Controls.Add(actions);
            Controls.Add(layout);
            CancelButton = cancel;
            apply.Click += delegate { Guard(Apply); };
            reload.Click += delegate { Guard(Reload); };
            restore.Click += delegate { Guard(Restore); };
            axes.DataError += DataError;
            Fill();
            if (needsReload) feedback.Text = "Profilzustand nicht bestaetigt. Vor dem Uebernehmen bitte 'Treiber neu einlesen' verwenden.";
            if (!store.Profile.SupportsActions)
            {
                axes.Enabled = false;
                enabled.Enabled = false;
                threshold.Enabled = false;
                neutral.Enabled = false;
                apply.Visible = false;
                reload.Visible = false;
                restore.Visible = false;
                cancel.Text = "Schliessen";
                feedback.ForeColor = Color.DarkGreen;
                feedback.Text = "Leerprofil gespeichert unter:\r\n" + store.SettingsPath +
                    "\r\nKeine Belegung aktiv. Chrome-Einstellungen und alle Treiberprofile bleiben unveraendert.";
            }
        }

        private static DataGridView Grid()
        {
            return new DataGridView {
                Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
                RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                EditMode = DataGridViewEditMode.EditOnEnter, RowTemplate = { Height = 30 }
            };
        }

        private static DataGridViewComboBoxColumn Combo<T>(string name, string title, List<Choice<T>> choices)
        {
            return new DataGridViewComboBoxColumn {
                Name = name, HeaderText = title, DataSource = choices.ToArray(),
                DisplayMember = "Text", ValueMember = "Value", ValueType = typeof(T), FlatStyle = FlatStyle.Flat
            };
        }

        private void DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            feedback.Text = "Ungueltiger Tabellenwert: " + (e.Exception == null ? "Bitte Auswahl pruefen." : e.Exception.Message);
            e.ThrowException = false;
        }

        private void Fill()
        {
            axes.Rows.Clear();
            string[] names = { "X - seitlich schieben", "Y - vor/zurueck schieben", "Z - ziehen/druecken",
                "Rx - vor/zurueck kippen", "Ry - seitlich kippen", "Rz - links/rechts drehen" };
            for (int i = 0; i < ApplicationMapping.AxisNames.Length; i++)
            {
                AxisMapping axis = current.Axes.Single(a => a.Axis == ApplicationMapping.AxisNames[i]);
                int index = axes.Rows.Add(names[i], axis.Mode, axis.Reverse, axis.Scale.ToString("0.00"),
                    axis.Deadband.ToString(), axis.Negative, axis.Positive);
                axes.Rows[index].Tag = axis.Axis;
            }
            foreach (ButtonMapping button in current.Buttons.OrderBy(b => b.Button))
            {
                int index = axes.Rows.Add();
                var row = axes.Rows[index];
                row.Tag = button.Button;
                row.Cells["Axis"].Value = "Taste " + button.Button;
                row.Cells["Mode"] = new DataGridViewComboBoxCell {
                    DataSource = new[] {
                        new Choice<ButtonMode>(ButtonMode.KeepDriver, "Treiber unveraendert"),
                        new Choice<ButtonMode>(ButtonMode.Disabled, "Deaktiviert"),
                        new Choice<ButtonMode>(ButtonMode.Bridge, "Bridge")
                    }, DisplayMember = "Text", ValueMember = "Value", ValueType = typeof(ButtonMode),
                    FlatStyle = FlatStyle.Flat, Value = button.Mode
                };
                row.Cells["Positive"] = new DataGridViewComboBoxCell {
                    DataSource = Actions.ToArray(), DisplayMember = "Text", ValueMember = "Value",
                    ValueType = typeof(BrowserAction), FlatStyle = FlatStyle.Flat, Value = button.Action
                };
                foreach (string name in new[] { "Reverse", "Scale", "Deadband", "Negative" })
                {
                    row.Cells[name] = new DataGridViewTextBoxCell { Value = "-" };
                    row.Cells[name].ReadOnly = true;
                }
            }
            enabled.Checked = current.Enabled;
            threshold.Value = current.Threshold;
            neutral.Value = current.Neutral;
            UpdateEditable();
        }

        private void UpdateEditable()
        {
            foreach (DataGridViewRow row in axes.Rows)
            {
                if (row.Tag is int)
                {
                    bool editable = row.Cells["Mode"].Value is ButtonMode &&
                        (ButtonMode)row.Cells["Mode"].Value == ButtonMode.Bridge;
                    row.Cells["Positive"].ReadOnly = !editable;
                    row.Cells["Positive"].Style.BackColor = editable ? AppTheme.Current.Surface : AppTheme.Current.Muted;
                    foreach (string name in new[] { "Reverse", "Scale", "Deadband", "Negative" })
                        row.Cells[name].Style.BackColor = AppTheme.Current.Muted;
                    continue;
                }
                if (!(row.Cells["Mode"].Value is AxisMode)) continue;
                AxisMode mode = (AxisMode)row.Cells["Mode"].Value;
                bool driver = mode == AxisMode.Scroll || mode == AxisMode.HorizontalScroll || mode == AxisMode.Zoom;
                foreach (string name in new[] { "Reverse", "Scale", "Deadband", "Negative", "Positive" })
                {
                    bool editable = name == "Negative" || name == "Positive" ? mode == AxisMode.Bridge : driver;
                    row.Cells[name].ReadOnly = !editable;
                    row.Cells[name].Style.BackColor = editable ? AppTheme.Current.Surface : AppTheme.Current.Muted;
                }
            }
        }

        protected override void OnAppThemeChanged() { UpdateEditable(); }

        internal ApplicationMapping Collect()
        {
            if (!axes.EndEdit())
                throw new InvalidDataException("Ein Tabellenwert ist ungueltig. Bitte Eingabe korrigieren.");
            ApplicationMapping mapping = current.Clone();
            mapping.Enabled = enabled.Checked;
            mapping.ChromeMenu = menuItems;
            mapping.MenuRadius = menuRadius;
            mapping.MenuCenterPercent = menuCenterPercent;
            mapping.MenuBorderWidth = menuBorderWidth;
            mapping.MenuSegmentGap = menuSegmentGap;
            mapping.MenuTextSize = menuTextSize;
            mapping.MenuIconSize = menuIconSize;
            mapping.Menu2 = menu2;
            mapping.Threshold = (int)threshold.Value;
            mapping.Neutral = (int)neutral.Value;
            foreach (DataGridViewRow row in axes.Rows)
            {
                if (row.Tag is int)
                {
                    var button = mapping.Buttons.Single(b => b.Button == (int)row.Tag);
                    button.Mode = (ButtonMode)row.Cells["Mode"].Value;
                    button.Action = (BrowserAction)row.Cells["Positive"].Value;
                    continue;
                }
                AxisMapping axis = mapping.Axes.Single(a => a.Axis == (string)row.Tag);
                axis.Mode = (AxisMode)row.Cells["Mode"].Value;
                axis.Reverse = (bool)row.Cells["Reverse"].Value;
                decimal scale;
                int deadband;
                if (!decimal.TryParse(Convert.ToString(row.Cells["Scale"].Value), NumberStyles.Number,
                    CultureInfo.CurrentCulture, out scale) || !int.TryParse(Convert.ToString(row.Cells["Deadband"].Value), out deadband))
                    throw new InvalidDataException("Bitte Verstaerkung und Totzone fuer " + axis.Axis + " als Zahlen eingeben.");
                axis.Scale = scale;
                axis.Deadband = deadband;
                axis.Negative = (BrowserAction)row.Cells["Negative"].Value;
                axis.Positive = (BrowserAction)row.Cells["Positive"].Value;
            }
            store.Profile.Validate(mapping);
            return mapping;
        }

        private void Apply()
        {
            if (needsReload)
                throw new InvalidDataException("Chrome-Profil seit dem Laden geaendert oder nicht bestaetigt. Bitte zuerst 'Treiber neu einlesen'.");
            ApplicationMapping mapping = Collect();
            DriverProfile.Merge(MappingStore.Read(store.DriverPath), mapping);
            if (ThemeMessage.Show(this,
                "Chrome-Mappings speichern und Treiber-XML anpassen?\r\n\r\n" +
                "Vorherige XML und Bridge-Einstellungen werden gesichert. Andere Anwendungen bleiben unveraendert.\r\n" +
                "Treiberfunktionen bleiben auch bei geschlossener Bridge aktiv.\r\n" +
                "Bridge-belegte Achsen/Tasten werden im Chrome-Treiberprofil deaktiviert.",
                "Chrome-Mappings uebernehmen", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK) return;
            string backup = store.Apply(mapping, driverHash, settingsHash);
            ThemeMessage.Show(this, backup == null ? "Die Einstellungen sind bereits gespeichert." :
                "Gespeichert. Sicherung:\r\n" + backup + "\r\n\r\nBitte kurz in eine andere Anwendung und dann nach Chrome wechseln.",
                "Chrome-Mappings");
            DialogResult = DialogResult.OK;
            Close();
        }

        private void Reload()
        {
            if (ThemeMessage.Show(this, "Ungespeicherte Auswahl verwerfen und aktuelle Treiberwerte einlesen?\r\n" +
                "Bridge-Einzelaktionen werden auf den Ausgangssatz gesetzt; vor dem Uebernehmen bitte pruefen.",
                "Treiber neu einlesen", MessageBoxButtons.OKCancel) != DialogResult.OK) return;
            byte[] driver = MappingStore.Read(store.DriverPath);
            current = DriverProfile.Import(driver);
            current.ChromeMenu = menuItems;
            current.MenuRadius = menuRadius;
            current.MenuCenterPercent = menuCenterPercent;
            current.MenuBorderWidth = menuBorderWidth;
            current.MenuSegmentGap = menuSegmentGap;
            current.MenuTextSize = menuTextSize;
            current.MenuIconSize = menuIconSize;
            current.Menu2 = menu2;
            driverHash = MappingStore.Hash(driver);
            settingsHash = MappingStore.Hash(MappingStore.Read(store.SettingsPath));
            needsReload = false;
            Fill();
            feedback.Text = "Treiber neu eingelesen; noch nicht gespeichert.";
        }

        private void Restore()
        {
            if (ThemeMessage.Show(this, "Letzte gespeicherte Mapping-Aenderung zuruecknehmen?\r\n" +
                "Neuere externe Aenderungen fuehren zum Abbruch, nicht zum Ueberschreiben.",
                "Sicherung wiederherstellen", MessageBoxButtons.OKCancel) != DialogResult.OK) return;
            store.Restore();
            ThemeMessage.Show(this, "Vorheriger Zustand wiederhergestellt. Bitte Anwendung wechseln und Chrome erneut aktivieren.", "Chrome-Mappings");
            DialogResult = DialogResult.OK;
            Close();
        }

        private void Guard(Action action)
        {
            try { action(); }
            catch (IOException ex) { ShowError(ex); }
            catch (UnauthorizedAccessException ex) { ShowError(ex); }
            catch (InvalidDataException ex) { ShowError(ex); }
            catch (XmlException ex) { ShowError(ex); }
            catch (ArgumentException ex) { ShowError(ex); }
            catch (FormatException ex) { ShowError(ex); }
            catch (OverflowException ex) { ShowError(ex); }
        }

        private void ShowError(Exception error)
        {
            feedback.Text = error.Message;
            ThemeMessage.Show(this, error.Message, "Mapping nicht uebernommen", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
