using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using System.Xml;

namespace SpaceMouseBridge
{
    internal sealed class DiagnosticWindow : ThemedForm
    {
        private readonly InputReader reader = new InputReader();
        private readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        private readonly ComboBox devices = new ComboBox();
        private readonly Label status = new Label();
        private readonly Label connection = new Label();
        private readonly Label buttons = new Label();
        private readonly TextBox path = new TextBox();
        private readonly TextBox raw = new TextBox();
        private readonly TextBox log = new TextBox();
        private readonly DataGridView axes = new DataGridView();
        private readonly List<IntPtr> handles = new List<IntPtr>();
        private readonly CheckBox tabSwitch = new CheckBox();
        private readonly CheckBox autoStart = new CheckBox {
            Text = "Bei Windows-Anmeldung automatisch starten", AutoSize = true
        };
        private bool updatingAutoStart;
        private NotifyIcon trayIcon;
        private ContextMenuStrip trayMenu;
        private ToolStripMenuItem trayOutput;
        private bool exiting;
        private readonly bool startInTray;
        internal static readonly uint ShowWindowMessage = Native.RegisterWindowMessage("SpaceMouseBridge.ShowMainWindow");
        private readonly ApplicationMappings bridge;
        private readonly Dictionary<string, ProfileSession> profiles = ApplicationProfile.All.ToDictionary(
            p => p.Id, p => new ProfileSession(MappingStore.ForUser(p)));
        private readonly ComboBox profileSelector = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 };
        private readonly Label activeProfile = new Label { Dock = DockStyle.Fill };
        private bool editingMappings;
        private readonly int probeSeconds;
        private readonly string output;
        private DateTime started;
        private DateTime nextRefresh;
        private DateTime nextDisplay;
        private bool ready;
        internal int ExitCode;

        internal DiagnosticWindow(int seconds, string outputPath, bool tray = false)
        {
            probeSeconds = seconds;
            output = outputPath;
            startInTray = tray && seconds == 0;
            bridge = new ApplicationMappings(reader.Message);
            Text = "SpaceMouse Bridge - Anwendungsprofile und Diagnose";
            Size = new Size(1000, 900);
            MinimumSize = new Size(850, 850);
            StartPosition = FormStartPosition.CenterScreen;
            var layout = new TableLayoutPanel {
                Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 11
            };
            foreach (int height in new[] { 128, 44, 52, 54, 66, 236, 54, 52, 30 })
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
            var profilePanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Tag = "panel",
                Padding = new Padding(3, 2, 3, 2) };
            profilePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            profilePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            profilePanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            profilePanel.Controls.Add(new Label {
                Text = "Automatische Profilwahl nach Vordergrundanwendung. Die Auswahl unten dient nur zum Bearbeiten.",
                Dock = DockStyle.Fill });
            var selection = new FlowLayoutPanel { Dock = DockStyle.Fill, Tag = "panel" };
            selection.Controls.Add(new Label { Text = "Profil bearbeiten:", AutoSize = true, Padding = new Padding(0, 4, 0, 0) });
            foreach (ApplicationProfile profile in ApplicationProfile.All) profileSelector.Items.Add(profile);
            profileSelector.SelectedIndex = 0;
            selection.Controls.Add(profileSelector);
            profilePanel.Controls.Add(selection);
            profilePanel.Controls.Add(activeProfile);
            layout.Controls.Add(RoundedSurface.Wrap(profilePanel));
            var diagnosticToggle = new RoundedButton {
                Text = "Erweiterte Diagnose anzeigen", AutoSize = true, Anchor = AnchorStyles.Left
            };
            layout.Controls.Add(diagnosticToggle);
            devices.Dock = DockStyle.Fill;
            devices.DropDownStyle = ComboBoxStyle.DropDownList;
            layout.Controls.Add(RoundedSurface.Wrap(devices));
            connection.Dock = DockStyle.Fill;
            layout.Controls.Add(RoundedSurface.Wrap(connection));
            path.Dock = DockStyle.Fill;
            path.Multiline = true;
            path.ReadOnly = true;
            layout.Controls.Add(RoundedSurface.Wrap(path));
            axes.Dock = DockStyle.Fill;
            axes.ReadOnly = true;
            axes.AllowUserToAddRows = false;
            axes.AllowUserToDeleteRows = false;
            axes.RowHeadersVisible = false;
            axes.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            axes.Columns.Add("Axis", "Achse");
            axes.Columns.Add("Value", "Letzter Rohwert");
            axes.Columns.Add("Range", "HID-Wertebereich");
            axes.Columns.Add("Report", "Report-ID");
            layout.Controls.Add(RoundedSurface.Wrap(axes));
            buttons.Dock = DockStyle.Fill;
            layout.Controls.Add(RoundedSurface.Wrap(buttons));
            raw.Dock = DockStyle.Fill;
            raw.ReadOnly = true;
            layout.Controls.Add(RoundedSurface.Wrap(raw));
            status.Text = "AUSGABE  /  EREIGNISSE";
            status.Dock = DockStyle.Fill;
            status.Tag = "section";
            layout.Controls.Add(status);
            log.Dock = DockStyle.Fill;
            log.Multiline = true;
            log.ReadOnly = true;
            log.ScrollBars = ScrollBars.Vertical;
            layout.Controls.Add(RoundedSurface.Wrap(log));
            var actions = new TableLayoutPanel { Dock = DockStyle.Fill, Tag = "panel",
                Padding = new Padding(4, 0, 4, 0), ColumnCount = 3, RowCount = 2 };
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            actions.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            actions.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var save = new RoundedButton { Text = "Diagnose speichern", AutoSize = true };
            save.Click += Save;
            save.Anchor = AnchorStyles.Left;
            actions.Controls.Add(save, 0, 0);
            tabSwitch.Text = "Bridge-Ausgabe (alle Profile)";
            tabSwitch.AutoSize = true;
            tabSwitch.Checked = false;
            tabSwitch.Enabled = probeSeconds == 0;
            bridge.Enabled = tabSwitch.Checked;
            tabSwitch.CheckedChanged += delegate {
                bridge.Enabled = tabSwitch.Checked;
                bridge.Reset();
                UpdateTrayOutput();
            };
            tabSwitch.Anchor = AnchorStyles.Left;
            actions.Controls.Add(tabSwitch, 0, 1);
            actions.SetColumnSpan(tabSwitch, 3);
            var editMappings = new RoundedButton { Text = "Mappings bearbeiten", AutoSize = true, Enabled = probeSeconds == 0, Tag = "primary" };
            editMappings.Click += EditMappings;
            editMappings.Anchor = AnchorStyles.Right;
            actions.Controls.Add(editMappings, 2, 0);
            autoStart.Enabled = probeSeconds == 0;
            autoStart.CheckedChanged += delegate {
                if (updatingAutoStart) return;
                GuardAutoStart(delegate {
                    AutoStart.SetEnabled(autoStart.Checked, Application.ExecutablePath);
                    reader.Message(autoStart.Checked ? "Autostart bei Windows-Anmeldung aktiviert." :
                        "Autostart bei Windows-Anmeldung deaktiviert.");
                });
            };
            autoStart.Anchor = AnchorStyles.None;
            actions.Controls.Add(autoStart, 1, 0);
            var actionSurface = RoundedSurface.Wrap(actions);
            actionSurface.Padding = new Padding(12, 8, 12, 8);
            layout.Controls.Add(actionSurface);
            for (int row = 0; row < layout.Controls.Count; row++)
                layout.SetCellPosition(layout.Controls[row], new TableLayoutPanelCellPosition(0, row));
            int[] diagnosticHeights = { 52, 54, 66, 236, 54, 52 };
            Action<bool> showDiagnostics = delegate(bool expanded) {
                layout.SuspendLayout();
                try
                {
                    for (int i = 0; i < diagnosticHeights.Length; i++)
                    {
                        int row = i + 2;
                        layout.RowStyles[row].Height = expanded ? diagnosticHeights[i] : 0;
                        layout.Controls[row].Visible = expanded;
                    }
                    diagnosticToggle.Text = expanded ? "Erweiterte Diagnose ausblenden" : "Erweiterte Diagnose anzeigen";
                    diagnosticToggle.AccessibleDescription = expanded ? "Diagnosebereich geoeffnet" : "Diagnosebereich geschlossen";
                }
                finally { layout.ResumeLayout(true); }
            };
            bool diagnosticsExpanded = false;
            int compactHeightReduction = 0;
            diagnosticToggle.Click += delegate {
                diagnosticsExpanded = !diagnosticsExpanded;
                Form window = layout.FindForm();
                int height = window.Height + (diagnosticsExpanded ? compactHeightReduction : -compactHeightReduction);
                window.MinimumSize = new Size(850, diagnosticsExpanded ? 850 : 850 - compactHeightReduction);
                if (window.WindowState == FormWindowState.Normal) window.Height = height;
                showDiagnostics(diagnosticsExpanded);
            };
            showDiagnostics(false);
            Controls.Add(layout);
            layout.PerformLayout();
            compactHeightReduction = (log.Height + 1) / 2;
            MinimumSize = new Size(850, 850 - compactHeightReduction);
            Height -= compactHeightReduction;
            if (probeSeconds == 0)
            {
                trayMenu = new ContextMenuStrip();
                trayMenu.Items.Add("Einstellungen öffnen", null, delegate { ShowMainWindow(); });
                trayOutput = new ToolStripMenuItem();
                trayOutput.Click += delegate { tabSwitch.Checked = !tabSwitch.Checked; };
                trayMenu.Items.Add(trayOutput);
                trayMenu.Items.Add(new ToolStripSeparator());
                trayMenu.Items.Add("Beenden", null, delegate { ExitApplication(); });
                trayMenu.Opening += delegate { UpdateTrayOutput(); };
                trayIcon = new NotifyIcon { Icon = Icon, Text = "SpaceMouse Bridge",
                    ContextMenuStrip = trayMenu };
                trayIcon.DoubleClick += delegate { ShowMainWindow(); };
                UpdateTrayOutput();
            }
            reader.Log += AppendLog;
            if (probeSeconds == 0)
                GuardAutoStart(delegate {
                    autoStart.Checked = AutoStart.IsEnabled(Application.ExecutablePath);
                    if (autoStart.Checked) AutoStart.SetEnabled(true, Application.ExecutablePath);
                });
            reader.ReportReceived += delegate(Device device)
            {
                try { bridge.Receive(device); }
                catch (Win32Exception ex) { tabSwitch.Checked = false; reader.Error(ex.Message); }
                catch (InvalidOperationException ex) { tabSwitch.Checked = false; reader.Error(ex.Message); }
            };
            timer.Interval = 25;
            timer.Tick += Tick;
            Shown += delegate
            {
                if (probeSeconds > 0 || startInTray) Hide();
                if (trayIcon != null) trayIcon.Visible = true;
                started = DateTime.UtcNow;
                try
                {
                    reader.Start(Handle);
                    ready = true;
                    reader.Message("Raw Input aktiv (Usage 01/08, Hintergrundempfang).");
                    reader.Message("Empfaenger erkannt bedeutet nicht automatisch: SpaceMouse wach/verbunden.");
                    if (probeSeconds == 0)
                    {
                        foreach (ProfileSession profile in profiles.Values)
                            GuardProfile(profile, delegate {
                                if (!profile.Profile.SupportsActions && MappingStore.Read(profile.Store.SettingsPath) == null)
                                {
                                    var empty = profile.Store.Load();
                                    profile.Store.Apply(empty, "absent", "absent");
                                    reader.Message(profile.Profile.Name + ": inaktives Leerprofil angelegt.");
                                }
                                profile.Load();
                                reader.Message(profile.Profile.Name + ": Profil geladen (" + profile.Store.SettingsPath + ").");
                            });
                        ConfigureBridge();
                        tabSwitch.Checked = tabSwitch.Enabled;
                    }
                }
                catch (Win32Exception ex) { reader.Error(ex.Message); ExitCode = 1; }
                catch (InvalidDataException ex) { reader.Error(ex.Message); ExitCode = 1; }
                timer.Start();
            };
            FormClosed += delegate { timer.Stop(); timer.Dispose(); reader.Dispose(); };
            if (probeSeconds > 0 || startInTray)
            {
                if (probeSeconds > 0) ShowInTaskbar = false;
                Opacity = 0;
            }
        }

        private void UpdateTrayOutput()
        {
            if (trayOutput == null) return;
            trayOutput.Text = tabSwitch.Checked ? "Bridge pausieren" : "Bridge fortsetzen";
            trayOutput.Enabled = tabSwitch.Enabled;
            if (trayIcon != null)
                trayIcon.Text = tabSwitch.Checked ? "SpaceMouse Bridge - aktiv" : "SpaceMouse Bridge - pausiert";
        }

        internal void ShowMainWindow()
        {
            ShowInTaskbar = true;
            Opacity = 1;
            Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
        }

        internal void ExitApplication()
        {
            exiting = true;
            Close();
        }

        protected override void OnFormClosing(FormClosingEventArgs args)
        {
            if (probeSeconds == 0 && !exiting && args.CloseReason == CloseReason.UserClosing)
            {
                args.Cancel = true;
                Hide();
            }
            base.OnFormClosing(args);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (trayIcon != null) { trayIcon.Visible = false; trayIcon.Dispose(); }
                if (trayMenu != null) trayMenu.Dispose();
            }
            base.Dispose(disposing);
        }

        private void GuardAutoStart(Action action)
        {
            updatingAutoStart = true;
            try { action(); }
            catch (IOException ex) { AutoStartError(ex); }
            catch (UnauthorizedAccessException ex) { AutoStartError(ex); }
            catch (System.Security.SecurityException ex) { AutoStartError(ex); }
            finally { updatingAutoStart = false; }
        }

        private void AutoStartError(Exception error)
        {
            autoStart.ThreeState = true;
            autoStart.CheckState = CheckState.Indeterminate;
            autoStart.Enabled = false;
            reader.Error("Autostart konnte nicht gelesen/gesetzt werden: " + error.Message);
            ThemeMessage.Show(this, error.Message + "\r\nAutostart-Status unbekannt; Schalter bis zum Neustart gesperrt.",
                "Autostart fehlgeschlagen", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void AppendLog(string text)
        {
            if (log.TextLength > 24000)
                log.Text = string.Join(Environment.NewLine, log.Lines.Skip(log.Lines.Length / 2));
            log.AppendText(text + Environment.NewLine);
        }

        private void ConfigureBridge()
        {
            bridge.Configure(profiles.Values);
            tabSwitch.Enabled = probeSeconds == 0 && profiles.Values.Any(p => p.Healthy && p.Profile.SupportsActions);
            if (!tabSwitch.Enabled) tabSwitch.Checked = false;
        }

        private bool GuardProfile(ProfileSession profile, Action action)
        {
            try { action(); return true; }
            catch (IOException ex) { MappingError(profile, ex); }
            catch (UnauthorizedAccessException ex) { MappingError(profile, ex); }
            catch (InvalidDataException ex) { MappingError(profile, ex); }
            catch (XmlException ex) { MappingError(profile, ex); }
            catch (ArgumentException ex) { MappingError(profile, ex); }
            catch (InvalidOperationException ex) { MappingError(profile, ex); }
            catch (FormatException ex) { MappingError(profile, ex); }
            catch (OverflowException ex) { MappingError(profile, ex); }
            return false;
        }

        private void MappingError(ProfileSession profile, Exception error)
        {
            profile.Fail(error.Message);
            bridge.Configure(profiles.Values);
            reader.Error(profile.Profile.Name + ": " + error.Message);
            ThemeMessage.Show(this, error.Message + "\r\n\r\nNur dieses Profil ist gesperrt. Bitte Profil auswaehlen und 'Mappings bearbeiten' oeffnen.",
                profile.Profile.Name + " - Mapping-Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void EditMappings(object sender, EventArgs args)
        {
            if (editingMappings) return;
            var selected = (ApplicationProfile)profileSelector.SelectedItem;
            if (selected == null) return;
            ProfileSession profile = profiles[selected.Id];
            if (!profile.Healthy && !profile.Profile.SupportsActions &&
                !GuardProfile(profile, profile.Load)) return;
            editingMappings = true;
            bool wasEnabled = tabSwitch.Checked;
            tabSwitch.Checked = false;
            tabSwitch.Enabled = false;
            try
            {
                GuardProfile(profile, delegate {
                    using (Form editor = selected == ApplicationProfile.Premiere ?
                        (Form)new PremiereMappingEditor(profile.Store, profile.Mapping) :
                        new MappingEditor(profile.Store, profile.Mapping))
                    {
                        if (editor.ShowDialog(this) == DialogResult.OK) profile.Load();
                    }
                });
            }
            finally
            {
                editingMappings = false;
                ConfigureBridge();
                if (tabSwitch.Enabled) tabSwitch.Checked = wasEnabled;
            }
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 0x0010 && probeSeconds == 0 && !exiting)
            {
                Hide();
                return;
            }
            if (ShowWindowMessage != 0 && message.Msg == ShowWindowMessage && probeSeconds == 0)
            {
                ShowMainWindow();
                return;
            }
            try
            {
                if (ready && message.Msg == 0x00ff) reader.Read(message.LParam);
                if (ready && message.Msg == 0x00fe)
                {
                    bridge.Reset();
                    nextRefresh = DateTime.MinValue;
                }
            }
            catch (Win32Exception ex) { bridge.Reset(); reader.Error(ex.Message); }
            catch (InvalidDataException ex) { bridge.Reset(); reader.Error(ex.Message); }
            finally
            {
                // DefWindowProc must still clean up foreground WM_INPUT messages.
                base.WndProc(ref message);
            }
        }

        private void Tick(object sender, EventArgs args)
        {
            bridge.Poll();
            try { bridge.CompletePending(); }
            catch (Win32Exception ex) { tabSwitch.Checked = false; reader.Error(ex.Message); }
            catch (InvalidOperationException ex) { tabSwitch.Checked = false; reader.Error(ex.Message); }
            if (ready && DateTime.UtcNow >= nextRefresh)
            {
                nextRefresh = DateTime.UtcNow.AddSeconds(2);
                try { reader.Refresh(); }
                catch (Win32Exception ex) { reader.Error(ex.Message); }
                catch (InvalidDataException ex) { reader.Error(ex.Message); }
                if (probeSeconds == 0 && !editingMappings)
                {
                    bool changed = false;
                    foreach (ProfileSession profile in profiles.Values.Where(p => p.Healthy).ToArray())
                    {
                        if (!GuardProfile(profile, delegate {
                            if (!profile.FilesUnchanged())
                                throw new InvalidDataException("Anwendungsprofil oder Bridge-Einstellungen wurden extern geaendert. Bitte neu einlesen.");
                        })) changed = true;
                    }
                    if (changed) ConfigureBridge();
                }
            }
            if (DateTime.UtcNow >= nextDisplay)
            {
                nextDisplay = DateTime.UtcNow.AddMilliseconds(100);
                UpdateDisplay();
            }
            if (probeSeconds > 0 && (DateTime.UtcNow - started).TotalSeconds >= probeSeconds)
            {
                try
                {
                    WriteSnapshot(output);
                    if (reader.Errors.Count > 0) ExitCode = 1;
                }
                catch (IOException ex) { reader.Error(ex.Message); ExitCode = 1; }
                catch (UnauthorizedAccessException ex) { reader.Error(ex.Message); ExitCode = 1; }
                Close();
            }
        }

        private void UpdateDisplay()
        {
            if (devices.DroppedDown) return;
            Control layout = devices.Parent;
            layout.SuspendLayout();
            try { UpdateDisplayValues(); }
            finally
            {
                // Fixed-size diagnostic rows do not need layout when only their text changes.
                layout.ResumeLayout(false);
            }
        }

        private void UpdateDisplayValues()
        {
            IntPtr selected = devices.SelectedIndex >= 0 && devices.SelectedIndex < handles.Count
                ? handles[devices.SelectedIndex] : IntPtr.Zero;
            var current = reader.Devices.Keys.ToList();
            if (!handles.SequenceEqual(current))
            {
                devices.BeginUpdate();
                try
                {
                    devices.Items.Clear();
                    handles.Clear();
                    handles.AddRange(current);
                    foreach (IntPtr handle in handles) devices.Items.Add(reader.Devices[handle].Description);
                    if (handles.Count > 0)
                        devices.SelectedIndex = handles.Contains(selected) ? handles.IndexOf(selected) : 0;
                }
                finally { devices.EndUpdate(); }
            }
            status.Text = reader.Errors.Count > 0 ? "AUSGABE  /  EREIGNISSE - Fehler: siehe Protokoll" :
                ready ? "AUSGABE  /  EREIGNISSE" : "AUSGABE  /  EREIGNISSE - Raw Input nicht aktiv";
            status.ForeColor = reader.Errors.Count > 0 || !ready ? AppTheme.Current.Error : AppTheme.Current.Text;
            ApplicationProfile foreground = bridge.ActiveProfile;
            if (foreground == null)
                activeProfile.Text = "Aktuelle Anwendung im Vordergrund: kein passendes Profil - keine Bridge-Ausgabe.";
            else
            {
                ProfileSession profile = profiles[foreground.Id];
                string state = !profile.Healthy ? "gesperrt: " + profile.Error :
                    foreground == ApplicationProfile.Premiere && profile.Mapping.Jog == null && profile.Mapping.PremiereInputs == null ?
                        "Leerprofil, keine Belegung aktiv; Treiber unveraendert" :
                    !profile.Mapping.Enabled ? "im Profil deaktiviert" :
                    !tabSwitch.Checked ? "Bridge pausiert" :
                    foreground == ApplicationProfile.Premiere ?
                        (profile.Mapping.PremiereInputs != null ? "Freie SpaceMouse-Zuordnungen aktiv" :
                        profile.Mapping.TimelineScroll != null && profile.Mapping.TimelineScroll.Enabled ?
                            "Jog (Rz) und Timeline-Scrollen (X) aktiv" : "Jog aktiv (Drehen/Rz)") : "Bridge-Ausgabe aktiv";
                activeProfile.Text = "Aktuelle Anwendung im Vordergrund: " + foreground.Name + " - " + state;
            }
            if (devices.SelectedIndex < 0)
            {
                axes.Rows.Clear();
                connection.Text = "Kein passendes 3Dconnexion-HID (Usage 01/08) erkannt.";
                path.Text = "";
                buttons.Text = "Tasten: keine Daten";
                raw.Text = "";
                return;
            }
            Device device = reader.Devices[handles[devices.SelectedIndex]];
            double age = device.LastReport.HasValue
                ? (DateTime.UtcNow - device.LastReport.Value).TotalSeconds : double.PositiveInfinity;
            connection.Text = device.LastReport.HasValue
                ? string.Format("HID vorhanden | Reports: {0} | Letzter Report vor {1:F1} s (kein Verbindungsnachweis der Funkmaus)",
                    device.Reports, age)
                : "HID vorhanden | Noch keine Reports: bitte Kappe bewegen / Maus aufwecken.";
            path.Text = device.Path;
            if (axes.Rows.Count != device.Axes.Count)
            {
                axes.Rows.Clear();
                foreach (Axis axis in device.Axes) axes.Rows.Add();
            }
            for (int i = 0; i < device.Axes.Count; i++)
            {
                Axis axis = device.Axes[i];
                axes.Rows[i].SetValues(axis.Name, axis.Value.HasValue ? axis.Value.Value.ToString() : "--",
                    axis.Minimum + " ... " + axis.Maximum, axis.ReportId);
            }
            string pressed = string.Join(", ", device.Buttons.SelectMany(b => b.Pressed).Distinct().OrderBy(b => b));
            buttons.Text = device.Buttons.Count == 0 ? "Keine HID-Tastenbeschreibung vorhanden." :
                !device.Buttons.Any(b => b.Seen) ? "Tasten: noch kein Tastenreport" :
                "Zuletzt gemeldete gedrueckte Tasten (HID-Nummern): " + (pressed.Length == 0 ? "keine" : pressed);
            raw.Text = "Letzter HID-Report: " + device.LastHex;
        }

        private void Save(object sender, EventArgs args)
        {
            using (var dialog = new SaveFileDialog {
                Filter = "JSON-Diagnose (*.json)|*.json", FileName = "spacemouse-diagnose.json"
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    WriteSnapshot(dialog.FileName);
                    reader.Message("Diagnose gespeichert: " + dialog.FileName);
                }
                catch (IOException ex) { reader.Error(ex.Message); ThemeMessage.Show(this, ex.Message, "Speicherfehler"); }
                catch (UnauthorizedAccessException ex) { reader.Error(ex.Message); ThemeMessage.Show(this, ex.Message, "Speicherfehler"); }
            }
        }

        private void WriteSnapshot(string file)
        {
            var snapshot = new {
                schemaVersion = 1,
                capturedUtc = DateTime.UtcNow.ToString("o"),
                rawInputRegistered = ready,
                inputOutputEnabled = bridge.Enabled,
                foregroundProfile = bridge.ActiveProfile == null ? null : bridge.ActiveProfile.Id,
                note = "HID presence does not establish wireless mouse connection. Values are last received, not current motion.",
                devices = reader.Devices.Values.Select(d => new {
                    vendorId = d.Vendor.ToString("X4"), productId = d.Product.ToString("X4"),
                    path = d.Path, reportLength = d.ReportLength,
                    reports = d.Reports,
                    lastReportUtc = d.LastReport.HasValue ? d.LastReport.Value.ToString("o") : null,
                    lastReportHex = d.LastHex,
                    axes = d.Axes.Select(a => new {
                        name = a.Name, reportId = a.ReportId, bits = a.Bits,
                        minimum = a.Minimum, maximum = a.Maximum, value = a.Value
                    }).ToArray(),
                    pressedButtons = d.Buttons.SelectMany(b => b.Pressed).Distinct().ToArray(),
                    buttonGroups = d.Buttons.Select(b => new {
                        reportId = b.ReportId, available = b.Available.OrderBy(u => u).ToArray(),
                        reportReceived = b.Seen
                    }).ToArray()
                }).ToArray(),
                errors = reader.Errors.ToArray(),
                events = log.Lines
            };
            File.WriteAllText(file, new JavaScriptSerializer().Serialize(snapshot));
        }
    }

    internal static class Program
    {
        private static bool ShowExistingWindow()
        {
            bool posted = false;
            Native.EnumWindows(delegate(IntPtr handle, IntPtr parameter) {
                var title = new System.Text.StringBuilder(256);
                Native.GetWindowText(handle, title, title.Capacity);
                if (title.ToString() != "SpaceMouse Bridge - Anwendungsprofile und Diagnose") return true;
                posted = Native.PostMessage(handle, DiagnosticWindow.ShowWindowMessage, IntPtr.Zero, IntPtr.Zero);
                return !posted;
            }, IntPtr.Zero);
            return posted;
        }

        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            NativeTheme.Initialize();
            try { AppTypography.Initialize(); }
            catch (InvalidDataException ex)
            {
                MessageBox.Show(ex.Message, "Schrift konnte nicht geladen werden", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
            if (args.Length == 1 && args[0] == "--self-test") return SelfTest.Run();
            if (args.Length == 3 && args[0] == "--descriptor-test" && args[1] == "--output")
                return SelfTest.RunDescriptors(args[2]);
            int seconds = 0;
            string output = null;
            bool tray = args.Length == 1 && args[0] == "--tray";
            if (args.Length != 0 && !tray &&
                (args.Length != 4 || args[0] != "--probe-seconds" ||
                 !int.TryParse(args[1], out seconds) || seconds < 1 || seconds > 3600 ||
                 args[2] != "--output" || string.IsNullOrWhiteSpace(args[3])))
            {
                ThemeMessage.Show("Aufruf: SpaceMouseBridge.exe [--tray] oder [--probe-seconds 5 --output <JSON-Datei>] oder --self-test",
                    "Ungueltige Argumente");
                return 2;
            }
            if (seconds > 0) output = Path.GetFullPath(args[3]);
            bool created;
            using (var instance = new Mutex(true, "Local\\SpaceMouseBridge-Interactive", out created))
            {
                if (!created)
                {
                    if (!tray && !ShowExistingWindow())
                        ThemeMessage.Show("Die Bridge laeuft bereits. Bitte ueber das Tray-Icon oeffnen.", "SpaceMouse Bridge");
                    return 0;
                }
                try
                {
                    using (var window = new DiagnosticWindow(seconds, output, tray))
                    {
                        Application.Run(window);
                        return window.ExitCode;
                    }
                }
                finally { instance.ReleaseMutex(); }
            }
        }
    }
}
