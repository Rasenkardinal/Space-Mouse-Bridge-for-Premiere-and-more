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
        private readonly MappingStore store;
        private readonly DataGridView axes = Grid();
        private readonly DataGridView buttons = Grid();
        private readonly CheckBox enabled = new CheckBox { Text = "Bridge-Ausgabe in Chrome aktivieren", AutoSize = true };
        private readonly NumericUpDown threshold = new NumericUpDown { Minimum = 15, Maximum = 95, Width = 60 };
        private readonly NumericUpDown neutral = new NumericUpDown { Minimum = 1, Maximum = 30, Width = 60 };
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
            new Choice<BrowserAction>(BrowserAction.End, "Seitenende")
        };

        internal MappingEditor(MappingStore mappingStore, ApplicationMapping active)
        {
            store = mappingStore;
            current = active.Clone();
            driverHash = MappingStore.Hash(store.ReadDriver());
            settingsHash = MappingStore.Hash(MappingStore.Read(store.SettingsPath));
            needsReload = current.DriverHash == null || current.DriverHash != driverHash;
            Text = store.Profile.Name + " - SpaceMouse-Mappings";
            enabled.Text = "Bridge-Ausgabe in " + store.Profile.Name + " aktivieren";
            Size = new Size(1220, 810);
            MinimumSize = new Size(1080, 780);
            StartPosition = FormStartPosition.CenterParent;
            var layout = new TableLayoutPanel {
                Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 8
            };
            foreach (int height in new[] { 88, 40, 245, 35, 116, 62 })
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            layout.Controls.Add(new Label {
                Dock = DockStyle.Fill,
                Text = (store.Profile.SupportsActions ?
                    "Profil: Google Chrome (chrome.exe)\r\nTreiber: kontinuierliches Scrollen/Zoom, auch ohne Bridge. Bridge: einzelne Aktionen beim Loslassen.\r\nUebernehmen sichert Dateien und deaktiviert die Treiberausgabe fuer Bridge-belegte Eingaben." :
                    "Profil: " + store.Profile.Name + " (" + store.Profile.Executable + ")\r\nVorbereitetes Leerprofil: keine Bridge-Aktionen und kein Eingriff in den 3Dconnexion-Treiber.\r\nDie Funktionen fuer Premiere werden im naechsten Schritt gemeinsam festgelegt.") +
                    "\r\n" + ApplicationProfile.DriverFunctionNotice
            });
            var options = new FlowLayoutPanel { Dock = DockStyle.Fill, Tag = "panel" };
            options.Controls.Add(enabled);
            options.Controls.Add(new Label { Text = "Bridge-Ausloesung (%)", AutoSize = true, Padding = new Padding(12, 4, 0, 0) });
            options.Controls.Add(threshold);
            options.Controls.Add(new Label { Text = "Neutralzone (%)", AutoSize = true, Padding = new Padding(12, 4, 0, 0) });
            options.Controls.Add(neutral);
            layout.Controls.Add(options);
            axes.Columns.Add(new DataGridViewTextBoxColumn { Name = "Axis", HeaderText = "Bewegung / HID-Achse", ReadOnly = true, FillWeight = 135 });
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
            axes.Columns.Add(Combo("Negative", "Negative Richtung", Actions));
            axes.Columns.Add(Combo("Positive", "Positive Richtung", Actions));
            axes.CurrentCellDirtyStateChanged += delegate {
                if (axes.IsCurrentCellDirty && (axes.CurrentCell is DataGridViewComboBoxCell || axes.CurrentCell is DataGridViewCheckBoxCell))
                    axes.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            axes.CellValueChanged += delegate { UpdateEditable(); };
            layout.Controls.Add(RoundedSurface.Wrap(axes));
            layout.Controls.Add(new Label { Text = "Tasten (HID-Nummern; bestehende Treiberbelegung kann unveraendert bleiben)", Dock = DockStyle.Fill });
            buttons.Columns.Add(new DataGridViewTextBoxColumn { Name = "Button", HeaderText = "Taste", ReadOnly = true });
            buttons.Columns.Add(Combo("Mode", "Zustaendigkeit", new List<Choice<ButtonMode>> {
                new Choice<ButtonMode>(ButtonMode.KeepDriver, "Treiber unveraendert"),
                new Choice<ButtonMode>(ButtonMode.Disabled, "Deaktiviert"),
                new Choice<ButtonMode>(ButtonMode.Bridge, "Bridge")
            }));
            buttons.Columns.Add(Combo("Action", "Aktion beim Loslassen", Actions));
            buttons.CurrentCellDirtyStateChanged += delegate {
                if (buttons.IsCurrentCellDirty && buttons.CurrentCell is DataGridViewComboBoxCell)
                    buttons.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            buttons.CellValueChanged += delegate { UpdateEditable(); };
            layout.Controls.Add(RoundedSurface.Wrap(buttons));
            layout.Controls.Add(new Label {
                Dock = DockStyle.Fill,
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
            buttons.DataError += DataError;
            Fill();
            if (needsReload) feedback.Text = "Profilzustand nicht bestaetigt. Vor dem Uebernehmen bitte 'Treiber neu einlesen' verwenden.";
            if (!store.Profile.SupportsActions)
            {
                axes.Enabled = false;
                buttons.Enabled = false;
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
            buttons.Rows.Clear();
            foreach (ButtonMapping button in current.Buttons.OrderBy(b => b.Button))
                buttons.Rows.Add("Taste " + button.Button, button.Mode, button.Action);
            enabled.Checked = current.Enabled;
            threshold.Value = current.Threshold;
            neutral.Value = current.Neutral;
            UpdateEditable();
        }

        private void UpdateEditable()
        {
            foreach (DataGridViewRow row in axes.Rows)
            {
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
            foreach (DataGridViewRow row in buttons.Rows)
            {
                bool editable = row.Cells["Mode"].Value is ButtonMode && (ButtonMode)row.Cells["Mode"].Value == ButtonMode.Bridge;
                row.Cells["Action"].ReadOnly = !editable;
                row.Cells["Action"].Style.BackColor = editable ? AppTheme.Current.Surface : AppTheme.Current.Muted;
            }
        }

        protected override void OnAppThemeChanged() { UpdateEditable(); }

        internal ApplicationMapping Collect()
        {
            if (!axes.EndEdit() || !buttons.EndEdit())
                throw new InvalidDataException("Ein Tabellenwert ist ungueltig. Bitte Eingabe korrigieren.");
            ApplicationMapping mapping = current.Clone();
            mapping.Enabled = enabled.Checked;
            mapping.Threshold = (int)threshold.Value;
            mapping.Neutral = (int)neutral.Value;
            foreach (DataGridViewRow row in axes.Rows)
            {
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
            for (int i = 0; i < 2; i++)
            {
                mapping.Buttons[i].Mode = (ButtonMode)buttons.Rows[i].Cells["Mode"].Value;
                mapping.Buttons[i].Action = (BrowserAction)buttons.Rows[i].Cells["Action"].Value;
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
