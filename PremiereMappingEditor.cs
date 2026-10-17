using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace SpaceMouseBridge
{
    internal sealed class PremiereMappingEditor : ThemedForm
    {
        private readonly MappingStore store;
        private readonly ApplicationMapping current;
        private readonly string settingsHash;
        private readonly PremiereBindings draft;
        private PremiereMenuItem[] menuItems;
        private int menuRadius;
        private int menuCenterPercent;
        private decimal menuBorderWidth;
        private decimal menuSegmentGap;
        private decimal menuTextSize;
        private decimal menuIconSize;
        private RadialMenuConfiguration menu2;
        private readonly CheckBox enabled = new CheckBox { Text = "Premiere-Bridge aktivieren", AutoSize = true };
        private readonly DataGridView inputs = new DataGridView {
            Dock = DockStyle.Fill, ReadOnly = true, MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };
        private readonly ComboBox function = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
        private readonly CheckBox reverse = new CheckBox { Text = "Richtung umkehren", AutoSize = true };
        private readonly NumericUpDown deadzone = Number(5, 40, 0);
        private readonly NumericUpDown trigger = Number(6, 95, 0);
        private readonly NumericUpDown minimum = Number(0.1m, 120, 2);
        private readonly NumericUpDown maximum = Number(2, 120, 0);
        private readonly NumericUpDown exponent = Number(0.25m, 5, 2);
        private readonly NumericUpDown delay = Number(0, 3000, 0);
        private readonly NumericUpDown endRate = Number(2, 120, 0);
        private readonly ComboBox behavior = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 280 };
        private readonly NumericUpDown singleLimit = Number(6, 95, 0);
        private readonly RoundedButton negative = new RoundedButton { AutoSize = true };
        private readonly RoundedButton positive = new RoundedButton { AutoSize = true };
        private readonly Label feedback = new Label { Dock = DockStyle.Fill, ForeColor = Color.DarkRed };
        private readonly Label rates = new Label { Dock = DockStyle.Fill };
        private readonly PremiereEditor.JogCurvePreview preview = new PremiereEditor.JogCurvePreview {
            Dock = DockStyle.Fill, Unit = "Schritte/s", AxisLabel = "Auslenkung"
        };
        private int selected = -1;
        private bool loading;
        private PremiereShortcut negativeKey, positiveKey;
        private static readonly string[] Functions = {
            "Keine Bridge-Aktion", "Jog / Abspielkopf", "Timeline horizontal scrollen",
            "Spuren vertikal scrollen", "Timeline-Zoom", "Wiedergabe / Stopp", "Eigenes Tastenkuerzel",
            "Menue 1 oeffnen (nur Tasten)", "Menue 2 oeffnen (nur Tasten)"
        };
        private static readonly string[] InputLabels = {
            "X - seitlich schieben", "Y - vor/zurueck schieben", "Z - ziehen/druecken",
            "Rx - vor/zurueck kippen", "Ry - seitlich kippen", "Rz - links/rechts drehen", "Taste 1", "Taste 2"
        };

        private static NumericUpDown Number(decimal min, decimal max, int decimals)
        {
            return new NumericUpDown { Minimum = min, Maximum = max, DecimalPlaces = decimals,
                Increment = decimals > 0 ? 0.25m : 1, Width = 72 };
        }

        private static void Field(FlowLayoutPanel panel, string name, Control control)
        {
            panel.Controls.Add(new Label { Text = name, AutoSize = true, Padding = new Padding(8, 5, 0, 0) });
            panel.Controls.Add(control);
        }

        internal PremiereMappingEditor(MappingStore mappingStore, ApplicationMapping mapping)
        {
            store = mappingStore;
            current = mapping.Clone();
            settingsHash = MappingStore.Hash(MappingStore.Read(store.SettingsPath));
            draft = PremiereBindings.From(current);
            menuItems = current.PremiereMenu;
            menuRadius = Math.Min(180, current.MenuRadius);
            menuCenterPercent = current.MenuCenterPercent;
            menuBorderWidth = current.MenuBorderWidth;
            menuSegmentGap = current.MenuSegmentGap;
            menuTextSize = current.MenuTextSize;
            menuIconSize = current.MenuIconSize;
            menu2 = current.Menu2;
            Text = "Adobe Premiere Pro - alle SpaceMouse-Eingaben";
            Size = new Size(1200, 980);
            MinimumSize = new Size(1120, 920);
            StartPosition = FormStartPosition.CenterParent;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16),
                ColumnCount = 1, RowCount = 12 };
            foreach (int height in new[] { 85, 47, 300, 40, 40, 40, 40, 40 })
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 94));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            layout.Controls.Add(new Label { Dock = DockStyle.Fill, Text =
                "Alle sechs Achsen und beide Tasten frei belegen. Zeile auswaehlen, darunter Funktion und Verhalten einstellen.\r\n" +
                "Entwurf ohne Ausgabe; erst 'Sichern und uebernehmen' aktiviert die Zuordnungen. Chrome und Treiber bleiben unveraendert.\r\n" +
                ApplicationProfile.DriverFunctionNotice + "\r\n" +
                "Premiere-Aktionen werden von dieser App ausgefuehrt. Das Speichern veraendert keine Einstellungen des 3Dconnexion-Treibers." });
            enabled.Checked = current.Enabled;
            var profileActions = new FlowLayoutPanel { Dock = DockStyle.Fill, Tag = "panel" };
            profileActions.Controls.Add(enabled);
            var menuChoice = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90 };
            menuChoice.Items.AddRange(new object[] { "Menue 1", "Menue 2" });
            menuChoice.SelectedIndex = 0;
            var editMenu = new RoundedButton { Text = "Menue bearbeiten", AutoSize = true };
            editMenu.Click += delegate {
                if (menuChoice.SelectedIndex == 1)
                {
                    var configuration = menu2 ?? new RadialMenuConfiguration();
                    using (var editor = new PremiereMenuEditor(configuration.Items, "Premiere - Menue 2",
                        configuration.Radius, configuration.CenterPercent, configuration.BorderWidth,
                        configuration.SegmentGap, configuration.TextSize, configuration.IconSize))
                        if (editor.ShowDialog(this) == DialogResult.OK) menu2 = editor.CollectConfiguration();
                    return;
                }
                using (var editor = new PremiereMenuEditor(menuItems, "Premiere - Menue 1", menuRadius, menuCenterPercent, menuBorderWidth, menuSegmentGap, menuTextSize, menuIconSize))
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
            var menuOptions = MappingEditor.MenuOptions(profileActions, menuChoice, editMenu, 30, 23, 5);
            menuOptions.Margin = new Padding(3, 3, 3, 7);
            layout.Controls.Add(menuOptions);
            inputs.Columns.Add("Input", "SpaceMouse-Eingabe");
            inputs.Columns.Add("Function", "Funktion");
            inputs.Columns.Add("Keys", "Kuerzel / Verhalten");
            foreach (PremiereBinding binding in draft.Items)
            {
                int index = Array.IndexOf(PremiereBindings.Names, binding.Input);
                inputs.Rows.Add(InputLabels[index], Functions[(int)binding.Function], Summary(binding));
            }
            layout.Controls.Add(RoundedSurface.Wrap(inputs));
            function.Items.AddRange(Functions);
            var assignment = new FlowLayoutPanel { Dock = DockStyle.Fill, Tag = "panel" };
            Field(assignment, "Funktion", function);
            assignment.Controls.Add(reverse);
            Field(assignment, "Totzone (%)", deadzone);
            Field(assignment, "Einzelaktion ab (%)", trigger);
            layout.Controls.Add(assignment);
            var speed = new FlowLayoutPanel { Dock = DockStyle.Fill, Tag = "panel" };
            Field(speed, "Minimum /s", minimum);
            Field(speed, "Maximum /s", maximum);
            Field(speed, "Kurvenexponent", exponent);
            Field(speed, "Endtempo /s", endRate);
            Field(speed, "Wiederholungsverzoegerung (ms)", delay);
            layout.Controls.Add(speed);
            behavior.Items.AddRange(new object[] { "Kontinuierlich wiederholen", "Einzelbereich + kontinuierlich" });
            var mode = new FlowLayoutPanel { Dock = DockStyle.Fill, Tag = "panel" };
            mode.Controls.Add(behavior);
            Field(mode, "Einzelschrittbereich bis (%)", singleLimit);
            layout.Controls.Add(mode);
            var shortcuts = new FlowLayoutPanel { Dock = DockStyle.Fill, Tag = "panel" };
            shortcuts.Controls.Add(negative);
            shortcuts.Controls.Add(positive);
            layout.Controls.Add(shortcuts);
            layout.Controls.Add(rates);
            layout.Controls.Add(RoundedSurface.Wrap(preview));
            layout.Controls.Add(new Label { Dock = DockStyle.Fill, Text =
                "Jog: Pfeil links/rechts. Zoom: - / + der Premiere-Standardbelegung. Wiedergabe/Stopp: Leertaste.\r\n" +
                "Scrollen: Mauszeiger ueber der Timeline; Premiere-Mausradpraeferenz Horizontal. Vertikal nutzt Strg+Mausrad.\r\n" +
                "Achsen fuer Wiedergabe/Kuerzel: bei Rueckkehr in die Totzone. Tasten: beim Loslassen; Menue 1/2 sofort beim Druecken.\r\n" +
                "Nach Fokuswechsel/Pause/Datenabbruch neutral stellen. Falscher Panel-/Textfokus kann andere Aktionen ausloesen.\r\n" +
                "Endtempo: Zusatzbeschleunigung oberhalb 60 % (bzw. oberhalb des Einzelbereichs). Gleich Maximum = kein Zusatz." });
            layout.Controls.Add(feedback);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Tag = "panel" };
            var cancel = new RoundedButton { Text = "Abbrechen", AutoSize = true, DialogResult = DialogResult.Cancel };
            var apply = new RoundedButton { Text = "Sichern und uebernehmen", AutoSize = true, Tag = "primary" };
            var restore = new RoundedButton { Text = "Letzte Aenderung rueckgaengig", AutoSize = true,
                Enabled = File.Exists(store.LastBackupPath) };
            actions.Controls.AddRange(new Control[] { cancel, apply, restore });
            layout.Controls.Add(actions);
            Controls.Add(layout);
            CancelButton = cancel;
            inputs.SelectionChanged += delegate {
                if (loading || inputs.CurrentRow == null) return;
                int next = inputs.CurrentRow.Index;
                if (next == selected) return;
                if (!SaveSelection())
                {
                    loading = true;
                    inputs.CurrentCell = inputs.Rows[selected].Cells[0];
                    loading = false;
                    return;
                }
                SelectInput(next);
            };
            foreach (NumericUpDown control in new[] { deadzone, trigger, minimum, maximum, exponent, delay, endRate, singleLimit })
                control.ValueChanged += delegate { UpdatePreview(); };
            function.SelectedIndexChanged += delegate { UpdatePreview(); };
            behavior.SelectedIndexChanged += delegate { UpdatePreview(); };
            reverse.CheckedChanged += delegate { UpdatePreview(); };
            negative.Click += delegate { CaptureKey(false); };
            positive.Click += delegate { CaptureKey(true); };
            apply.Click += delegate { Guard(Apply); };
            restore.Click += delegate { Guard(delegate {
                if (ThemeMessage.Show(this, "Letzte Premiere-Bridge-Aenderung zuruecknehmen? Externe Aenderungen blockieren die Ruecknahme.",
                    "Premiere", MessageBoxButtons.OKCancel) != DialogResult.OK) return;
                store.Restore(); DialogResult = DialogResult.OK; Close();
            }); };
            SelectInput(0);
        }

        private static string Summary(PremiereBinding binding)
        {
            return binding.Function == PremiereFunction.Shortcut ?
                (binding.Button ? "" : (binding.Negative == null ? "fehlt" : binding.Negative.ToString()) + " / ") +
                (binding.Positive == null ? "fehlt" : binding.Positive.ToString()) :
                binding.Function == PremiereFunction.None ? "Treiber unveraendert" :
                binding.Menu ? "Beim Druecken; erneut druecken zum Schliessen" :
                binding.Button ? "Einmal beim Loslassen" : binding.Discrete ? "Einmal bei Neutralstellung" : "Auslenkungsabhaengig";
        }

        internal void SelectInput(int index)
        {
            loading = true;
            selected = index;
            PremiereBinding binding = draft.Items[index];
            function.SelectedIndex = (int)binding.Function;
            reverse.Checked = binding.Response.Reverse;
            deadzone.Value = binding.Response.Deadzone;
            trigger.Value = binding.Trigger;
            minimum.Value = binding.Response.EffectiveMinimumRate;
            maximum.Value = binding.Response.MaximumRate;
            exponent.Value = binding.Response.EffectiveExponent;
            delay.Value = binding.Response.RepeatDelayMs;
            endRate.Value = binding.EndRate ?? binding.Response.MaximumRate;
            behavior.SelectedIndex = (int)binding.Response.Behavior;
            singleLimit.Value = binding.Response.EffectiveSingleFrameLimit;
            negativeKey = binding.Negative;
            positiveKey = binding.Positive;
            inputs.CurrentCell = inputs.Rows[index].Cells[0];
            loading = false;
            UpdatePreview();
        }

        private PremiereBinding ReadSelection()
        {
            var binding = new PremiereBinding {
                Input = draft.Items[selected].Input, Function = (PremiereFunction)function.SelectedIndex,
                Trigger = (int)trigger.Value, EndRate = (int)endRate.Value,
                Negative = negativeKey, Positive = positiveKey,
                Response = new JogSettings {
                    Reverse = reverse.Checked, Deadzone = (int)deadzone.Value,
                    MinimumRate = minimum.Value, MaximumRate = (int)maximum.Value,
                    CurveExponent = exponent.Value, FineControl = exponent.Value > 1,
                    RepeatDelayMs = (int)delay.Value, Behavior = (JogBehavior)behavior.SelectedIndex,
                    SingleFrameLimit = (int)singleLimit.Value
                }
            };
            return binding;
        }

        private void UpdatePreview()
        {
            if (loading || selected < 0) return;
            PremiereBinding binding = ReadSelection();
            bool motion = binding.Function != PremiereFunction.None && !binding.Menu &&
                !binding.Discrete && !binding.Button;
            reverse.Enabled = binding.Function != PremiereFunction.None && binding.Function != PremiereFunction.Playback &&
                !binding.Menu &&
                !(binding.Button && binding.Function == PremiereFunction.Shortcut);
            foreach (Control control in new Control[] { minimum, maximum, exponent, delay, endRate, behavior })
                control.Enabled = motion;
            deadzone.Enabled = !binding.Button && binding.Function != PremiereFunction.None;
            trigger.Enabled = binding.Discrete && !binding.Button;
            singleLimit.Enabled = motion && behavior.SelectedIndex == 1;
            negative.Enabled = binding.Function == PremiereFunction.Shortcut && !binding.Button;
            positive.Enabled = binding.Function == PremiereFunction.Shortcut;
            negative.Text = "Negativ: " + (negativeKey == null ? "Kuerzel aufzeichnen" : negativeKey.ToString());
            positive.Text = (binding.Button ? "Taste: " : "Positiv: ") +
                (positiveKey == null ? "Kuerzel aufzeichnen" : positiveKey.ToString());
            try
            {
                binding.Validate();
                feedback.Text = "";
                preview.Configure(motion ? binding.Response : null, motion ? (Func<double, double>)binding.Rate : null,
                    binding.EndRate ?? binding.Response.MaximumRate);
                rates.Text = motion ? string.Format("Rate bei 25 / 50 / 75 / 100 % Auslenkung: {0:0.##} / {1:0.##} / {2:0.##} / {3:0.##} Schritte/s.",
                    binding.Rate(0.25), binding.Rate(0.5), binding.Rate(0.75), binding.Rate(1)) :
                    binding.Function == PremiereFunction.None ? "Keine Bridge-Ausgabe fuer diese Eingabe; der Treiber bleibt unveraendert." :
                    binding.Menu ?
                    "Oeffnet Menue " + (binding.Function == PremiereFunction.ContextMenu ? "1" : "2") +
                    " beim Druecken. Oben das Menue auswaehlen und 'Menue bearbeiten' oeffnen." :
                    "Einzelaktion: keine automatische Wiederholung. Fuer Kuerzel auf den entsprechenden Aufzeichnen-Button klicken.";
            }
            catch (InvalidDataException ex) { feedback.Text = ex.Message; preview.Configure(null); rates.Text = ""; }
        }

        private bool SaveSelection()
        {
            if (selected < 0) return true;
            PremiereBinding binding = ReadSelection();
            try { binding.Validate(); }
            catch (InvalidDataException ex) { feedback.Text = ex.Message; return false; }
            draft.Items[selected] = binding;
            inputs.Rows[selected].Cells[1].Value = Functions[(int)binding.Function];
            inputs.Rows[selected].Cells[2].Value = Summary(binding);
            return true;
        }

        internal ApplicationMapping Collect()
        {
            if (!SaveSelection()) throw new InvalidDataException(feedback.Text);
            ApplicationMapping mapping = current.Clone();
            mapping.PremiereInputs = draft;
            mapping.PremiereMenu = menuItems;
            mapping.MenuRadius = menuRadius;
            mapping.MenuCenterPercent = menuCenterPercent;
            mapping.MenuBorderWidth = menuBorderWidth;
            mapping.MenuSegmentGap = menuSegmentGap;
            mapping.MenuTextSize = menuTextSize;
            mapping.MenuIconSize = menuIconSize;
            mapping.Menu2 = menu2;
            mapping.Enabled = enabled.Checked;
            store.Profile.Validate(mapping);
            return mapping;
        }

        private void CaptureKey(bool positiveDirection)
        {
            using (var dialog = new ShortcutCapture())
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                if (positiveDirection) positiveKey = dialog.Shortcut; else negativeKey = dialog.Shortcut;
                UpdatePreview();
            }
        }

        private void Apply()
        {
            ApplicationMapping mapping = Collect();
            if (ThemeMessage.Show(this, "Alle Premiere-Zuordnungen mit Sicherung speichern?\r\n" +
                "Chrome und Treiberdateien bleiben unveraendert. Vor dem Test Timeline fokussieren.",
                "Premiere", MessageBoxButtons.OKCancel) != DialogResult.OK) return;
            string backup = store.Apply(mapping, "absent", settingsHash);
            ThemeMessage.Show(this, backup == null ? "Bereits gespeichert." : "Gespeichert. Sicherung:\r\n" + backup, "Premiere");
            DialogResult = DialogResult.OK;
            Close();
        }

        private void Guard(Action action)
        {
            try { action(); }
            catch (IOException ex) { ShowError(ex); }
            catch (UnauthorizedAccessException ex) { ShowError(ex); }
            catch (InvalidDataException ex) { ShowError(ex); }
            catch (InvalidOperationException ex) { ShowError(ex); }
            catch (ArgumentException ex) { ShowError(ex); }
        }
        private void ShowError(Exception ex)
        {
            feedback.Text = ex.Message;
            ThemeMessage.Show(this, ex.Message, "Premiere nicht uebernommen", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        internal sealed class ShortcutCapture : ThemedForm
        {
            internal PremiereShortcut Shortcut;
            private readonly Label label = new Label { Dock = DockStyle.Fill, Padding = new Padding(16),
                Text = "Gewuenschtes Tastenkuerzel druecken (optional Strg/Umschalt/Alt).\r\nEscape bricht ab. Es wird keine Eingabe an Premiere gesendet." };
            internal ShortcutCapture()
            {
                Text = "Tastenkuerzel aufzeichnen"; Size = new Size(600, 180);
                StartPosition = FormStartPosition.CenterParent; MinimizeBox = false; MaximizeBox = false;
                Controls.Add(label);
            }
            protected override bool ProcessCmdKey(ref Message message, Keys keyData)
            {
                if (keyData == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); return true; }
                var value = new PremiereShortcut { Key = (int)(keyData & Keys.KeyCode),
                    Control = (keyData & Keys.Control) != 0, Shift = (keyData & Keys.Shift) != 0, Alt = (keyData & Keys.Alt) != 0 };
                try { value.Validate(); }
                catch (InvalidDataException ex) { label.Text = ex.Message + "\r\nEscape bricht ab."; return true; }
                Shortcut = value; DialogResult = DialogResult.OK; Close(); return true;
            }
        }
    }
}
