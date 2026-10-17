using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace SpaceMouseBridge
{
    internal sealed class PremiereEditor : ThemedForm
    {
        private readonly MappingStore store;
        private readonly ApplicationMapping current;
        private readonly string settingsHash;
        private readonly CheckBox enabled = new CheckBox { Text = "Jog in Premiere aktivieren", AutoSize = true };
        private readonly CheckBox reverse = new CheckBox { Text = "Drehrichtung umkehren", AutoSize = true };
        private readonly NumericUpDown deadzone = new NumericUpDown { Minimum = 5, Maximum = 40, Width = 80 };
        private readonly NumericUpDown rate = new NumericUpDown { Minimum = 2, Maximum = 120, Width = 80 };
        private readonly NumericUpDown minimumRate = new NumericUpDown {
            Minimum = 0.1m, Maximum = 120, DecimalPlaces = 2, Increment = 0.1m, Width = 85 };
        private readonly NumericUpDown exponent = new NumericUpDown {
            Minimum = 0.25m, Maximum = 5, DecimalPlaces = 2, Increment = 0.25m, Width = 85 };
        private readonly NumericUpDown repeatDelay = new NumericUpDown { Minimum = 0, Maximum = 3000, Increment = 50, Width = 85 };
        private readonly NumericUpDown singleFrameLimit = new NumericUpDown { Minimum = 6, Maximum = 95, Width = 80 };
        private readonly ComboBox behavior = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
        private readonly JogCurvePreview preview = new JogCurvePreview { Dock = DockStyle.Fill };
        private readonly Label curveValues = new Label { Dock = DockStyle.Fill };
        private readonly Label feedback = new Label { Dock = DockStyle.Fill, ForeColor = Color.DarkRed };
        private readonly CheckBox scrollEnabled = new CheckBox { Text = "X: Timeline horizontal scrollen", AutoSize = true };
        private readonly CheckBox scrollReverse = new CheckBox { Text = "Richtung umkehren", AutoSize = true };
        private readonly NumericUpDown scrollRate = new NumericUpDown { Minimum = 2, Maximum = 120, Width = 70 };
        private readonly NumericUpDown scrollEndRate = new NumericUpDown { Minimum = 2, Maximum = 120, Width = 70 };

        internal PremiereEditor(MappingStore mappingStore, ApplicationMapping active)
        {
            store = mappingStore;
            current = active.Clone();
            settingsHash = MappingStore.Hash(MappingStore.Read(store.SettingsPath));
            JogSettings settings = current.Jog ?? JogSettings.Default();
            Text = "Adobe Premiere Pro - Jog und Timeline";
            Size = new Size(1040, 920);
            MinimumSize = new Size(1000, 870);
            StartPosition = FormStartPosition.CenterParent;
            var layout = new TableLayoutPanel {
                Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 12
            };
            foreach (int height in new[] { 120, 32, 42, 42, 42, 42, 60 })
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 110));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            layout.Controls.Add(new Label {
                Dock = DockStyle.Fill,
                Text = "Drehen (Rz): links ein Bild zurueck, rechts ein Bild vor.\r\n" +
                    "Kennlinie und Wiederholung frei einstellen; neutral stoppt immer.\r\n" +
                    "Vorschau ist nur ein Entwurf: erst 'Sichern und uebernehmen' aendert die Ausgabe.\r\n" +
                    ApplicationProfile.DriverFunctionNotice + "\r\n" +
                    "Premiere-Aktionen werden von dieser App ausgefuehrt. Das Speichern veraendert keine Einstellungen des 3Dconnexion-Treibers."
            });
            enabled.Checked = current.Enabled;
            layout.Controls.Add(enabled);
            var options = new FlowLayoutPanel { Dock = DockStyle.Fill, Tag = "panel" };
            options.Controls.Add(new Label { Text = "Totzone (%)", AutoSize = true, Padding = new Padding(0, 4, 0, 0) });
            deadzone.Value = settings.Deadzone;
            options.Controls.Add(deadzone);
            options.Controls.Add(new Label { Text = "Maximum (Bilder/s)", AutoSize = true, Padding = new Padding(12, 4, 0, 0) });
            rate.Value = settings.MaximumRate;
            options.Controls.Add(rate);
            options.Controls.Add(new Label { Text = "Minimum (Bilder/s)", AutoSize = true, Padding = new Padding(12, 4, 0, 0) });
            minimumRate.Value = settings.EffectiveMinimumRate;
            options.Controls.Add(minimumRate);
            layout.Controls.Add(options);
            reverse.Checked = settings.Reverse;
            var curve = new FlowLayoutPanel { Dock = DockStyle.Fill, Tag = "panel" };
            curve.Controls.Add(new Label { Text = "Kurvenexponent", AutoSize = true, Padding = new Padding(0, 4, 0, 0) });
            exponent.Value = settings.EffectiveExponent;
            curve.Controls.Add(exponent);
            curve.Controls.Add(new Label {
                Text = "1 = linear, 2 = quadratisch, 3 = kubisch; hoeher = unten feiner",
                AutoSize = true, Padding = new Padding(12, 4, 0, 0) });
            curve.Controls.Add(reverse);
            layout.Controls.Add(curve);
            var timing = new FlowLayoutPanel { Dock = DockStyle.Fill, Tag = "panel" };
            timing.Controls.Add(new Label { Text = "Wartezeit vor erster Wiederholung (ms)", AutoSize = true, Padding = new Padding(0, 4, 0, 0) });
            repeatDelay.Value = settings.RepeatDelayMs;
            timing.Controls.Add(repeatDelay);
            timing.Controls.Add(new Label { Text = "Erstes Bild sofort; danach fruehestens nach dieser Zeit.", AutoSize = true, Padding = new Padding(12, 4, 0, 0) });
            layout.Controls.Add(timing);
            var modes = new FlowLayoutPanel { Dock = DockStyle.Fill, Tag = "panel" };
            behavior.Items.AddRange(new object[] { "Kontinuierliches Jog", "Einzelbildbereich + kontinuierliches Jog" });
            behavior.SelectedIndex = (int)settings.Behavior;
            modes.Controls.Add(behavior);
            modes.Controls.Add(new Label { Text = "Einzelbildbereich bis Drehung (%)", AutoSize = true, Padding = new Padding(12, 4, 0, 0) });
            singleFrameLimit.Value = settings.EffectiveSingleFrameLimit;
            modes.Controls.Add(singleFrameLimit);
            layout.Controls.Add(modes);
            layout.Controls.Add(curveValues);
            layout.Controls.Add(RoundedSurface.Wrap(preview));
            TimelineScrollSettings scrolling = current.TimelineScroll ?? TimelineScrollSettings.Default();
            scrollEnabled.Checked = scrolling.Enabled;
            scrollReverse.Checked = scrolling.Reverse;
            scrollRate.Value = scrolling.MaximumRate;
            scrollEndRate.Minimum = scrollRate.Value;
            scrollEndRate.Value = scrolling.EffectiveEndRate;
            scrollRate.ValueChanged += delegate { scrollEndRate.Minimum = scrollRate.Value; };
            var scrollOptions = new FlowLayoutPanel { Dock = DockStyle.Fill, Tag = "panel" };
            scrollOptions.Controls.Add(scrollEnabled);
            scrollOptions.Controls.Add(scrollReverse);
            scrollOptions.Controls.Add(new Label { Text = "Grundtempo (Impulse/s)", AutoSize = true, Padding = new Padding(12, 4, 0, 0) });
            scrollOptions.Controls.Add(scrollRate);
            scrollOptions.Controls.Add(new Label { Text = "Endtempo", AutoSize = true, Padding = new Padding(12, 4, 0, 0) });
            scrollOptions.Controls.Add(scrollEndRate);
            layout.Controls.Add(scrollOptions);
            layout.Controls.Add(new Label {
                Dock = DockStyle.Fill,
                Text = "Vor Benutzung: Wiedergabe stoppen und einmal in die Timeline klicken.\r\n" +
                    "Premiere-Standardbefehle: Pfeil links/rechts = ein Bild zurueck/vor. Eigene Shortcuts ggf. pruefen.\r\n" +
                    "Nach Fokuswechsel, Pause oder Datenabbruch erst neutral stellen. Kein J/K/L-Shuttle.\r\n" +
                    "X-Scrollen: Mauszeiger ueber die Timeline; Premiere-Mausrad auf horizontal einstellen.\r\n" +
                    "X: Totzone 12 %, Zusatzbeschleunigung oberhalb 60 % bis zum Endtempo. Jog und Treiber unveraendert."
            });
            layout.Controls.Add(feedback);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Tag = "panel" };
            var cancel = new RoundedButton { Text = "Abbrechen", AutoSize = true, DialogResult = DialogResult.Cancel };
            var apply = new RoundedButton { Text = "Sichern und uebernehmen", AutoSize = true, Tag = "primary" };
            var restore = new RoundedButton { Text = "Letzte Aenderung rueckgaengig", AutoSize = true,
                Enabled = File.Exists(store.LastBackupPath) };
            actions.Controls.Add(cancel);
            actions.Controls.Add(apply);
            actions.Controls.Add(restore);
            layout.Controls.Add(actions);
            Controls.Add(layout);
            CancelButton = cancel;
            apply.Click += delegate { Guard(Apply); };
            restore.Click += delegate { Guard(Restore); };
            foreach (NumericUpDown control in new[] { deadzone, rate, minimumRate, exponent, repeatDelay, singleFrameLimit })
                control.ValueChanged += delegate { UpdatePreview(); };
            behavior.SelectedIndexChanged += delegate { UpdatePreview(); };
            UpdatePreview();
        }

        private JogSettings CollectSettings()
        {
            return new JogSettings {
                Reverse = reverse.Checked, Deadzone = (int)deadzone.Value, MaximumRate = (int)rate.Value,
                MinimumRate = minimumRate.Value, CurveExponent = exponent.Value,
                FineControl = exponent.Value > 1, RepeatDelayMs = (int)repeatDelay.Value,
                Behavior = (JogBehavior)behavior.SelectedIndex, SingleFrameLimit = (int)singleFrameLimit.Value
            };
        }

        private void UpdatePreview()
        {
            singleFrameLimit.Enabled = behavior.SelectedIndex == (int)JogBehavior.SingleFrameRegion;
            JogSettings settings = CollectSettings();
            try { settings.Validate(); }
            catch (InvalidDataException ex)
            {
                preview.Configure(null);
                curveValues.Text = "";
                feedback.Text = ex.Message;
                return;
            }
            feedback.Text = "";
            preview.Configure(settings);
            double start = settings.ContinuousStart;
            curveValues.Text = string.Format(
                "Kontinuierlicher Bereich: oberhalb {0:0} % Drehung. Rate bei 25 / 50 / 75 / 100 % dieses Bereichs:\r\n" +
                "{1:0.##} / {2:0.##} / {3:0.##} / {4:0.##} Bilder/s. Beide Richtungen symmetrisch.\r\n{5}",
                start * 100, settings.Rate(start + (1 - start) * 0.25), settings.Rate(start + (1 - start) * 0.5),
                settings.Rate(start + (1 - start) * 0.75), settings.Rate(1),
                settings.Behavior == JogBehavior.SingleFrameRegion ?
                    "Gelb: ein Bild beim Auslenken/Richtungswechsel; Halten wiederholt nicht. Ausserhalb: Jog." :
                    "Auch leichtes Drehen wiederholt. Erstes Bild sofort; Loslassen stoppt.");
        }

        internal ApplicationMapping Collect()
        {
            ApplicationMapping mapping = current.Clone();
            mapping.Enabled = enabled.Checked;
            mapping.Jog = CollectSettings();
            if (current.TimelineScroll != null || scrollEnabled.Checked)
                mapping.TimelineScroll = new TimelineScrollSettings {
                    Enabled = scrollEnabled.Checked, Reverse = scrollReverse.Checked, MaximumRate = (int)scrollRate.Value,
                    EndRate = (int)scrollEndRate.Value
                };
            store.Profile.Validate(mapping);
            return mapping;
        }

        private void Apply()
        {
            ApplicationMapping mapping = Collect();
            if (ThemeMessage.Show(this, "Premiere-Jog und Timeline-Scrollen speichern?\r\n\r\n" +
                "Die bisherigen Premiere-Bridge-Einstellungen werden gesichert.\r\n" +
                "Chrome und alle Treiberdateien bleiben unveraendert.\r\n" +
                "Vor dem Test die Wiedergabe stoppen und die Timeline aktivieren.",
                "Premiere Jog", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK) return;
            string backup = store.Apply(mapping, "absent", settingsHash);
            ThemeMessage.Show(this, backup == null ? "Bereits gespeichert." : "Gespeichert. Sicherung:\r\n" + backup, "Premiere Jog");
            DialogResult = DialogResult.OK;
            Close();
        }

        private void Restore()
        {
            if (ThemeMessage.Show(this, "Letzte Premiere-Bridge-Aenderung zuruecknehmen?\r\n" +
                "Externe Aenderungen fuehren zum Abbruch, nicht zum Ueberschreiben.",
                "Premiere Jog", MessageBoxButtons.OKCancel) != DialogResult.OK) return;
            store.Restore();
            DialogResult = DialogResult.OK;
            Close();
        }

        private void Guard(Action action)
        {
            try { action(); }
            catch (IOException ex) { ShowError(ex); }
            catch (UnauthorizedAccessException ex) { ShowError(ex); }
            catch (InvalidDataException ex) { ShowError(ex); }
            catch (ArgumentException ex) { ShowError(ex); }
            catch (InvalidOperationException ex) { ShowError(ex); }
        }

        private void ShowError(Exception error)
        {
            feedback.Text = error.Message;
            ThemeMessage.Show(this, error.Message, "Premiere-Jog nicht uebernommen", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        internal sealed class JogCurvePreview : Control
        {
            private JogSettings settings;
            private Func<double, double> rate;
            private int maximum;
            internal string Unit = "Bilder/s";
            internal string AxisLabel = "Drehung";

            internal JogCurvePreview()
            {
                DoubleBuffered = true;
                BackColor = AppTheme.Current.Input;
            }

            internal void Configure(JogSettings value)
            {
                Configure(value, null, value == null ? 1 : value.MaximumRate);
            }

            internal void Configure(JogSettings value, Func<double, double> rateFunction, int maximumRate)
            {
                settings = value;
                rate = rateFunction;
                maximum = maximumRate;
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs args)
            {
                base.OnPaint(args);
                if (settings == null || Width < 120 || Height < 80) return;
                Graphics graphics = args.Graphics;
                ThemePalette palette = AppTheme.Current;
                graphics.Clear(palette.Input);
                using (var text = new SolidBrush(palette.Text))
                using (var muted = new SolidBrush(palette.Muted))
                using (var single = new SolidBrush(palette.SingleFrame))
                using (var border = new Pen(palette.Border))
                {
                graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                var plot = new Rectangle(60, 24, Width - 85, Height - 65);
                float dead = plot.Left + plot.Width * settings.Deadzone / 100f;
                float start = plot.Left + plot.Width * (float)settings.ContinuousStart;
                graphics.FillRectangle(muted, plot.Left, plot.Top, dead - plot.Left, plot.Height);
                if (settings.Behavior == JogBehavior.SingleFrameRegion)
                    graphics.FillRectangle(single, dead, plot.Top, start - dead, plot.Height);
                for (int i = 0; i <= 4; i++)
                {
                    float y = plot.Bottom - plot.Height * i / 4f;
                    graphics.DrawLine(border, plot.Left, y, plot.Right, y);
                    graphics.DrawString((maximum * i / 4.0).ToString("0.#"), Font, text, 2, y - 8);
                    float x = plot.Left + plot.Width * i / 4f;
                    graphics.DrawString((i * 25).ToString(), Font, text, x - 10, plot.Bottom + 4);
                }
                using (var pen = new Pen(palette.Curve, 2))
                {
                    PointF? previous = null;
                    for (int i = 0; i <= plot.Width; i++)
                    {
                        double normalized = (double)i / plot.Width;
                        double value = rate == null ? settings.Rate(normalized) : rate(normalized);
                        var point = new PointF(plot.Left + i, plot.Bottom - (float)(value / maximum) * plot.Height);
                        if (previous.HasValue && normalized > settings.ContinuousStart)
                            graphics.DrawLine(pen, previous.Value, point);
                        previous = point;
                    }
                }
                graphics.DrawRectangle(border, plot);
                graphics.DrawString(Unit + " (Wiederholungsrate, erster Schritt sofort)", Font, text, plot.Left, 2);
                graphics.DrawString(AxisLabel + " (%) - grau: Totzone; gelb: optionaler Einzelbildbereich", Font, text,
                    plot.Left, Height - 20);
                }
            }
        }
    }
}
