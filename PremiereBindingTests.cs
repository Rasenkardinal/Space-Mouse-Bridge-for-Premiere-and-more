using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace SpaceMouseBridge
{
    internal static class PremiereBindingTests
    {
        private static void Assert(bool value, string name)
        {
            if (!value) throw new InvalidOperationException("Premiere-Belegungstest: " + name);
        }
        private static void Reject(Action action, string name)
        {
            bool rejected = false;
            try { action(); }
            catch (InvalidDataException) { rejected = true; }
            Assert(rejected, name);
        }
        private static T Control<T>(PremiereMappingEditor editor, string name) where T : System.Windows.Forms.Control
        {
            return (T)typeof(PremiereMappingEditor).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor);
        }

        private static ApplicationMapping Legacy()
        {
            var mapping = ApplicationProfile.Premiere.EmptyMapping();
            mapping.Enabled = true;
            mapping.Jog = JogSettings.Default();
            mapping.TimelineScroll = new TimelineScrollSettings { Enabled = true, MaximumRate = 24, EndRate = 96 };
            return mapping;
        }

        internal static void Run()
        {
            var mapping = Legacy();
            var bindings = PremiereBindings.From(mapping);
            bindings.Validate();
            Assert(mapping.PremiereInputs == null && bindings.Items.Length == 8 &&
                bindings.Items[0].Function == PremiereFunction.HorizontalScroll &&
                bindings.Items[5].Function == PremiereFunction.Jog &&
                bindings.Items.Count(b => b.Function == PremiereFunction.None) == 6,
                "Legacy Rz/X erhalten, andere Eingaben unberuehrt, keine implizite Migration");
            foreach (double value in new[] { 0, 0.12, 0.25, 0.6, 0.75, 1, -1 })
            {
                Assert(bindings.Items[0].Rate(value) == mapping.TimelineScroll.Rate(value), "X-Kurve bytefrei exakt abgeleitet");
                Assert(bindings.Items[5].Rate(value) == mapping.Jog.Rate(value), "Rz-Kurve unveraendert abgeleitet");
            }
            var binding = bindings.Items[1];
            binding.Function = PremiereFunction.Shortcut;
            Reject(binding.Validate, "Kuerzel muessen vorhanden sein");
            binding.Negative = new PremiereShortcut { Key = (int)Keys.Left, Control = true, Shift = true };
            binding.Positive = new PremiereShortcut { Key = (int)Keys.Right, Alt = true };
            binding.Validate();
            var keys = binding.Inputs(-1);
            Assert(keys.Length == 6 && keys[0].Key == 0x11 && keys[1].Key == 0x10 &&
                keys[2].Key == 0x25 && keys[2].Flags == 1 && keys[3].Flags == 3 &&
                keys[4].Key == 0x10 && keys[5].Key == 0x11, "Kuerzel samt Modifiern vollstaendig und rueckwaerts freigegeben");
            Assert(binding.Inputs(1)[0].Key == 0x12, "Positives Kuerzel unabhaengig");
            new PremiereShortcut { Key = (int)Keys.F12, Control = true }.Validate();
            Reject(new PremiereShortcut { Key = (int)Keys.ControlKey }.Validate, "Reiner Modifier ungueltig");
            Reject(new PremiereShortcut { Key = (int)Keys.LWin }.Validate, "Windows-Taste kein regulaerer Shortcut");
            foreach (PremiereFunction function in Enum.GetValues(typeof(PremiereFunction)))
            {
                if (function == PremiereFunction.None || function == PremiereFunction.ContextMenu) continue;
                binding.Function = function;
                foreach (int steps in new[] { -1, 1 })
                {
                    var inputs = binding.Inputs(steps);
                    Assert(inputs.Length > 0, "Jede Funktion erzeugt Pakete");
                    for (uint sent = 0; sent <= inputs.Length; sent++)
                    {
                        var down = new HashSet<ushort>();
                        foreach (var input in inputs.Take((int)sent).Where(k => k.Type == 1))
                        {
                            if ((input.Flags & 2) != 0) down.Remove(input.Key); else down.Add(input.Key);
                        }
                        Assert(BrowserCommand.ReleasePartial(inputs, sent).Select(k => k.Key).OrderBy(k => k)
                            .SequenceEqual(down.OrderBy(k => k)), "Teilfehler gibt ausschliesslich eigene gehaltene Tasten frei");
                    }
                    if (function == PremiereFunction.VerticalScroll)
                        Assert(inputs.Length == 3 && inputs[0].Key == 0x11 && inputs[0].Flags == 0 &&
                            inputs[1].Type == 0 && inputs[1].MouseFlags == 0x800 &&
                            inputs[1].MouseData == -120 * steps && inputs[2].Key == 0x11 &&
                            inputs[2].Flags == 2, "Vertikal mit vollstaendigem Strg+Rad");
                    if (function == PremiereFunction.HorizontalScroll)
                        Assert(inputs.Single().Type == 0, "Horizontal ohne Modifier");
                    if (function == PremiereFunction.TimelineZoom)
                        Assert(inputs[0].Key == (steps < 0 ? 0xbd : 0xbb), "Zoom minus/gleich statt Ctrl-Browserzoom");
                    if (function == PremiereFunction.Playback)
                        Assert(inputs.Length == 2 && inputs.All(k => k.Key == 0x20), "Playback genau ein Space-Tipp");
                }
            }
            mapping.PremiereInputs = PremiereBindings.From(Legacy());
            ApplicationProfile.Premiere.Validate(mapping);
            var chrome = ApplicationMapping.Default();
            chrome.PremiereInputs = mapping.PremiereInputs;
            Reject(delegate { ApplicationProfile.Chrome.Validate(chrome); }, "Premiere-Belegungen nicht in Chrome");
            var invalid = mapping.Clone();
            invalid.PremiereInputs.Items[0].Input = "Rz";
            Reject(invalid.PremiereInputs.Validate, "Doppelte Eingaben abgewiesen");
            invalid = mapping.Clone();
            invalid.PremiereInputs.Items[0].Trigger = 5;
            Reject(invalid.PremiereInputs.Validate, "Ausloesung nicht in Totzone");
            invalid = mapping.Clone();
            invalid.PremiereInputs.Items[0].EndRate = 121;
            Reject(invalid.PremiereInputs.Validate, "Endtempo maximal 120");
            Runtime();
            AllAssignments();
            CaptureShortcuts();
            Menu();
            MenuDispatch();
            PersistenceAndEditor();
        }

        private static void Menu()
        {
            var mapping = Legacy();
            Assert(mapping.PremiereMenu == null, "Alte Profile starten ohne Menueeintraege");
            mapping.PremiereInputs = PremiereBindings.From(mapping);
            mapping.PremiereInputs.Items[6].Function = PremiereFunction.ContextMenu;
            mapping.PremiereMenu = new[] { new PremiereMenuItem {
                Name = "Mein Schnitt", Shortcut = new PremiereShortcut { Key = (int)Keys.K, Control = true }
            } };
            ApplicationProfile.Premiere.Validate(mapping);
            var copy = mapping.Clone();
            Assert(copy.PremiereMenu[0].Name == "Mein Schnitt" && copy.PremiereMenu[0].Shortcut.Control &&
                copy.PremiereInputs.Items[6].Function == PremiereFunction.ContextMenu,
                "Menueeintraege und Aktion per JSON klonbar");
            copy.PremiereInputs.Items[0].Function = PremiereFunction.ContextMenu;
            Reject(copy.Validate, "Menueaktion nur auf Tasten");
            Reject(delegate { PremiereMenuItem.Validate(new PremiereMenuItem { Name = " ", Shortcut = mapping.PremiereMenu[0].Shortcut }); },
                "Leere Eintragsnamen nicht erlaubt");
            Reject(delegate { PremiereMenuItem.Validate(new PremiereMenuItem { Name = "Test" }); }, "Fehlendes Kuerzel sichtbar abgewiesen");
            Reject(delegate { PremiereMenuItem.Validate(null); }, "Null-Eintrag abgewiesen");
            var chrome = ApplicationMapping.Default();
            chrome.PremiereMenu = mapping.PremiereMenu;
            Reject(delegate { ApplicationProfile.Chrome.Validate(chrome); }, "Premiere-Menue nicht im Chrome-Profil");

            IntPtr target = new IntPtr(1), other = new IntPtr(2);
            Assert(ApplicationMappings.CanDispatchMenu(true, target, 10, 10, target, 10, 20) &&
                ApplicationMappings.CanDispatchMenu(true, target, 10, 10, other, 20, 20), "Ursprungsfenster oder Bridge als Fokus-Rueckweg");
            Assert(!ApplicationMappings.CanDispatchMenu(true, target, 10, 10, other, 30, 20) &&
                !ApplicationMappings.CanDispatchMenu(true, target, 10, 11, target, 11, 20) &&
                !ApplicationMappings.CanDispatchMenu(false, target, 10, 10, target, 10, 20) &&
                !ApplicationMappings.CanDispatchMenu(true, target, 10, 10, IntPtr.Zero, 0, 20),
                "Andere App, ersetztes Fenster, Reset und fehlender Fokus sperren Menueausgabe");
            var runtime = new ApplicationMappings(delegate { });
            typeof(ApplicationMappings).GetField("menuOpen", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(runtime, true);
            runtime.Poll(); runtime.Receive(null); runtime.CompletePending();
            Assert(runtime.ActiveProfile == null, "Offenes Menue blockiert Poll, HID-Verarbeitung und Ausgabe");

            using (var editor = new PremiereMenuEditor(null))
                Assert(editor.Collect().Length == 0, "Leeres Menue ist gueltig, keine Vorgaben");
            using (var editor = new PremiereMenuEditor(mapping.PremiereMenu))
            {
                var grid = (DataGridView)typeof(PremiereMenuEditor).GetField("rows", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor);
                grid.Rows[0].Cells[0].Value = "Neuer Name";
                Assert(editor.Collect()[0].Name == "Neuer Name" && mapping.PremiereMenu[0].Name == "Mein Schnitt",
                    "Menueeditor bearbeitet Entwurf ohne Originalaenderung");
                grid.Rows.Add("Ohne Kuerzel", "");
                Reject(delegate { editor.Collect(); }, "Unvollstaendigen Menueeintrag nicht uebernehmen");
            }
            using (var editor = new PremiereMenuEditor(new[] { mapping.PremiereMenu[0], new PremiereMenuItem {
                Name = "Zweiter Befehl", Shortcut = new PremiereShortcut { Key = (int)Keys.F8 }
            } }))
            {
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var grid = (DataGridView)typeof(PremiereMenuEditor).GetField("rows", flags).GetValue(editor);
                grid.CurrentCell = grid.Rows[1].Cells[0];
                typeof(PremiereMenuEditor).GetMethod("MoveItem", flags).Invoke(editor, new object[] { -1 });
                Assert(editor.Collect()[0].Name == "Zweiter Befehl" && editor.Collect()[1].Name == "Mein Schnitt",
                    "Reihenfolge ohne Verlust von Namen und Kuerzeln aenderbar");
                grid.Rows.RemoveAt(0);
                Assert(editor.Collect().Length == 1 && editor.Collect()[0].Shortcut.Control,
                    "Menueeintraege gezielt entfernbar");
            }
            foreach (bool empty in new[] { true, false })
            using (var popup = new PremiereMenuPopup(empty ? null : mapping.PremiereMenu, Cursor.Position))
            {
                popup.Show(); popup.Activate(); Native.SetForegroundWindow(popup.Handle); Application.DoEvents();
                Assert(Screen.FromControl(popup).WorkingArea.Contains(popup.Bounds), "Popup am Zeiger bleibt im Arbeitsbereich");
                if (empty)
                {
                    typeof(PremiereMenuPopup).GetMethod("ProcessCmdKey", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(popup, new object[] { new Message(), Keys.Escape });
                    Assert(popup.SelectedShortcut == null, "Leeres Menue und Escape senden nichts");
                }
                else
                {
                    var button = popup.Controls[0].Controls.OfType<RoundedButton>().Single();
                    button.PerformClick();
                    Assert(popup.SelectedShortcut == mapping.PremiereMenu[0].Shortcut,
                        "Menueauswahl gibt nur das konfigurierte Kuerzel zurueck, keine direkte Ausgabe");
                }
            }
            using (var owner = new ThemedForm())
            using (var popup = new PremiereMenuPopup(mapping.PremiereMenu, Cursor.Position))
            {
                owner.Show(); popup.Show(); popup.Activate(); Native.SetForegroundWindow(popup.Handle); Application.DoEvents();
                owner.Activate(); Application.DoEvents();
                Assert(popup.IsDisposed && popup.SelectedShortcut == null, "Fokusverlust schliesst Menue ohne Befehl");
            }
        }

        private static void MenuDispatch()
        {
            foreach (string mode in new[] { "select", "cancel", "reset" })
            using (var target = new ThemedForm { Text = "Bridge-Menue - isoliertes Testziel", KeyPreview = true })
            using (var timer = new Timer { Interval = 100 })
            {
                int received = 0;
                bool released = false;
                target.KeyDown += delegate(object sender, KeyEventArgs e) {
                    if (e.KeyCode == Keys.F8 && e.Control) received++;
                };
                target.KeyUp += delegate(object sender, KeyEventArgs e) {
                    if (e.KeyCode == Keys.ControlKey) released = true;
                };
                target.Show(); target.Activate(); Native.SetForegroundWindow(target.Handle); Application.DoEvents();
                var mapping = Legacy();
                mapping.PremiereMenu = new[] { new PremiereMenuItem {
                    Name = "Isolierter Test", Shortcut = new PremiereShortcut { Key = (int)Keys.F8, Control = true }
                } };
                var messages = new List<string>();
                var runtime = new ApplicationMappings(messages.Add) { Enabled = true };
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(ApplicationMappings).GetField("context", flags).SetValue(runtime, target.Handle);
                uint process;
                Native.GetWindowThreadProcessId(target.Handle, out process);
                typeof(ApplicationMappings).GetField("contextProcess", flags).SetValue(runtime, process);
                typeof(ApplicationMappings).GetField("mapping", flags).SetValue(runtime, mapping);
                timer.Tick += delegate {
                    timer.Stop();
                    var popup = Application.OpenForms.OfType<PremiereMenuPopup>().Single();
                    Native.SetForegroundWindow(popup.Handle);
                    if (mode == "cancel") { popup.Close(); return; }
                    if (mode == "reset") runtime.Reset();
                    popup.Controls[0].Controls.OfType<RoundedButton>().Single().PerformClick();
                };
                timer.Start();
                typeof(ApplicationMappings).GetMethod("OpenPremiereMenu", flags).Invoke(runtime, null);
                var deadline = DateTime.UtcNow.AddSeconds(1);
                while (DateTime.UtcNow < deadline && (mode == "select" && !released))
                {
                    Application.DoEvents();
                    System.Threading.Thread.Sleep(10);
                }
                Application.DoEvents();
                Assert(mode == "select" ? received == 1 && released : received == 0,
                    "Echter Menuepfad: Fokus-Rueckgabe, genau ein Kuerzel, Freigabe; Abbruch/Reset ohne Ausgabe (" + mode + ")");
                Assert(!(bool)typeof(ApplicationMappings).GetField("menuOpen", flags).GetValue(runtime),
                    "Menuepause auch nach Auswahl oder Abbruch beendet");
                if (mode != "reset")
                    Assert(Native.GetForegroundWindow() == target.Handle, "Ursprungsfenster nach Menue wieder fokussiert");
            }
        }

        private static void CaptureShortcuts()
        {
            Type type = typeof(PremiereMappingEditor).GetNestedType("ShortcutCapture", BindingFlags.NonPublic);
            MethodInfo capture = type.GetMethod("ProcessCmdKey", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo value = type.GetField("Shortcut", BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (Keys key in new[] { Keys.Tab, Keys.Enter, Keys.Control | Keys.Shift | Keys.K, Keys.Alt | Keys.F9 })
            using (var dialog = (Form)Activator.CreateInstance(type, true))
            {
                dialog.Show();
                capture.Invoke(dialog, new object[] { new Message(), Keys.Control | Keys.ControlKey });
                Assert(value.GetValue(dialog) == null && dialog.DialogResult == DialogResult.None,
                    "Nur Modifier beendet Aufzeichnung nicht");
                capture.Invoke(dialog, new object[] { new Message(), key });
                var shortcut = (PremiereShortcut)value.GetValue(dialog);
                Assert(dialog.DialogResult == DialogResult.OK && shortcut.Key == (int)(key & Keys.KeyCode) &&
                    shortcut.Control == ((key & Keys.Control) != 0) &&
                    shortcut.Shift == ((key & Keys.Shift) != 0) && shortcut.Alt == ((key & Keys.Alt) != 0),
                    "Aufzeichnung erfasst Navigationstasten und Modifier exakt");
            }
            using (var dialog = (Form)Activator.CreateInstance(type, true))
            {
                dialog.Show();
                capture.Invoke(dialog, new object[] { new Message(), Keys.Escape });
                Assert(dialog.DialogResult == DialogResult.Cancel && value.GetValue(dialog) == null,
                    "Escape bricht ohne Kuerzel ab");
            }
        }

        private static void AllAssignments()
        {
            var mapping = Legacy();
            var bindings = PremiereBindings.From(mapping);
            var runtime = new PremiereBindingRuntime();
            using (var device = new Device())
            {
                foreach (string name in ApplicationMapping.AxisNames)
                    device.Axes.Add(new Axis { Name = name, Minimum = -350, Maximum = 350, ReportId = 1, Value = 0 });
                var group = new ButtonGroup { ReportId = 2 };
                group.Available.Add(1); group.Available.Add(2); device.Buttons.Add(group);
                foreach (PremiereBinding binding in bindings.Items)
                foreach (PremiereFunction function in Enum.GetValues(typeof(PremiereFunction)))
                {
                    if (function == PremiereFunction.ContextMenu && !binding.Button) continue;
                    foreach (var item in bindings.Items) item.Function = PremiereFunction.None;
                    binding.Function = function;
                    binding.Negative = new PremiereShortcut { Key = (int)Keys.F10, Control = true };
                    binding.Positive = new PremiereShortcut { Key = (int)Keys.F11, Shift = true };
                    foreach (bool reverse in new[] { false, true })
                    {
                        binding.Response.Reverse = reverse;
                        runtime.Reset();
                        foreach (var axis in device.Axes) axis.Value = 0;
                        group.Pressed.Clear();
                        Report(runtime, device, bindings, 0, binding.Button ? (byte)2 : (byte)1);
                        if (binding.Button)
                        {
                            group.Pressed.Add(binding.Input == "Button1" ? (ushort)1 : (ushort)2);
                            Report(runtime, device, bindings, 10, 2);
                            group.Pressed.Clear();
                            Report(runtime, device, bindings, 20, 2);
                        }
                        else
                        {
                            Axis axis = device.Axes.Single(a => a.Name == binding.Input);
                            axis.Value = 350;
                            Report(runtime, device, bindings, 10, 1);
                            if (binding.Discrete)
                            {
                                axis.Value = 0;
                                Report(runtime, device, bindings, 20, 1);
                            }
                        }
                        var output = runtime.Step(bindings, 20, delegate { });
                        Assert(output.Length == (function == PremiereFunction.None ? 0 : 1),
                            "Jede Funktion auf jeder Eingabe, in beiden Richtungen");
                        if (output.Length == 0) continue;
                        Assert(output[0].Steps == (reverse ? -1 : 1) &&
                            (function == PremiereFunction.ContextMenu || output[0].Inputs.Length > 0),
                            "Richtungsumkehr bei allen Funktionen");
                        if (function == PremiereFunction.Shortcut)
                            Assert(output[0].Inputs.Any(k => k.Key ==
                                (binding.Button || !reverse ? (ushort)Keys.F11 : (ushort)Keys.F10)),
                                "Richtungsabhaengiges Kuerzel und einziges Tastenkuerzel");
                    }
                }
                foreach (var item in bindings.Items) item.Function = PremiereFunction.Jog;
                runtime.Reset();
                foreach (var axis in device.Axes) axis.Value = 0;
                Report(runtime, device, bindings, 0, 1);
                foreach (var axis in device.Axes) axis.Value = 350;
                Report(runtime, device, bindings, 10, 1);
                Assert(runtime.Step(bindings, 10, delegate { }).Length == 6, "Sechs Achsen gleichzeitig unabhaengig");
                foreach (var item in bindings.Items) item.Function = PremiereFunction.None;
                var scroll = bindings.Items[0];
                scroll.Function = PremiereFunction.HorizontalScroll;
                scroll.Response = new JogSettings { Deadzone = 12, MinimumRate = 0.5m, MaximumRate = 24, CurveExponent = 2 };
                scroll.EndRate = 96;
                runtime.Reset();
                foreach (var axis in device.Axes) axis.Value = 0;
                Report(runtime, device, bindings, 0, 1);
                int count = 0;
                for (int tick = 1; tick <= 400; tick++)
                {
                    device.Axes[0].Value = 350;
                    Report(runtime, device, bindings, tick * 25, 1);
                    foreach (var emission in runtime.Step(bindings, tick * 25, delegate { }))
                    {
                        count += emission.Steps;
                        Assert(emission.Inputs.Length == 1 && emission.Inputs[0].Type == 0 &&
                            emission.Inputs[0].MouseData == -120 * emission.Steps,
                            "Endtempo erzeugt ausschliesslich Radimpulse ohne Modifier");
                    }
                }
                Assert(Math.Abs(count - 960) <= 3, "Neuer Laufzeitpfad erreicht 96 Radimpulse/s ueber zehn Sekunden");
            }
        }

        private static void Runtime()
        {
            var bindings = PremiereBindings.From(Legacy());
            var logs = new List<string>();
            var runtime = new PremiereBindingRuntime();
            using (var device = new Device { LastReportId = 1 })
            {
                foreach (string name in ApplicationMapping.AxisNames)
                    device.Axes.Add(new Axis { Name = name, Minimum = -350, Maximum = 350, ReportId = 1 });
                var buttons = new ButtonGroup { ReportId = 2 };
                buttons.Available.Add(1); buttons.Available.Add(2);
                device.Buttons.Add(buttons);
                foreach (string name in ApplicationMapping.AxisNames)
                {
                    runtime.Reset();
                    foreach (var item in bindings.Items) item.Function = PremiereFunction.None;
                    var binding = bindings.Items.Single(b => b.Input == name);
                    binding.Function = PremiereFunction.Jog;
                    Axis axis = device.Axes.Single(a => a.Name == name);
                    axis.Value = 350;
                    Report(runtime, device, bindings, 0, 1);
                    Assert(runtime.Step(bindings, 0, logs.Add).Length == 0, "Jede Achse startet ausgelenkt gesperrt");
                    axis.Value = 0;
                    Report(runtime, device, bindings, 1, 1);
                    axis.Value = -350;
                    Report(runtime, device, bindings, 10, 1);
                    var output = runtime.Step(bindings, 10, logs.Add);
                    Assert(output.Length == 1 && output[0].Steps == -1 && output[0].Binding.Input == name,
                        "Jog auf jeder Achse unabhaengig ausfuehrbar");
                    Report(runtime, device, bindings, 240, 2);
                    Assert(runtime.Step(bindings, 260, logs.Add).Length == 0, "Tastenreport verlaengert Achsenlease nicht");
                    Report(runtime, device, bindings, 270, 1);
                    Assert(runtime.Step(bindings, 270, logs.Add).Length == 0, "Nach Timeout Neutral erforderlich");
                    axis.Value = 0; Report(runtime, device, bindings, 280, 1);
                    axis.Value = 350; Report(runtime, device, bindings, 290, 1);
                    Assert(runtime.Step(bindings, 290, logs.Add).Single().Steps == 1, "Neutral reaktiviert jede Achse");
                    runtime.Reset();
                    Report(runtime, device, bindings, 300, 1);
                    Assert(runtime.Step(bindings, 300, logs.Add).Length == 0, "Reset verwirft Bewegung bei Pause/Fokus/Quelle");
                }
                runtime.Reset();
                foreach (var item in bindings.Items) item.Function = PremiereFunction.None;
                var x = bindings.Items[0];
                x.Function = PremiereFunction.Playback;
                Axis motion = device.Axes[0];
                motion.Value = 0; Report(runtime, device, bindings, 0, 1);
                motion.Value = 350; Report(runtime, device, bindings, 10, 1);
                Assert(runtime.Step(bindings, 10, logs.Add).Length == 0, "Playback nicht beim Auslenken");
                motion.Value = 0; Report(runtime, device, bindings, 20, 1);
                Assert(runtime.Step(bindings, 20, logs.Add).Single().Inputs[0].Key == 0x20,
                    "Playback genau einmal bei Rueckkehr in Neutral");
                Report(runtime, device, bindings, 30, 1);
                Assert(runtime.Step(bindings, 30, logs.Add).Length == 0, "Neutral wiederholt Playback nicht");
                motion.Value = 350; Report(runtime, device, bindings, 40, 1);
                runtime.Step(bindings, 300, logs.Add);
                motion.Value = 0; Report(runtime, device, bindings, 310, 1);
                Assert(runtime.Step(bindings, 310, logs.Add).Length == 0, "Datenabbruch verwirft vorgemerktes Playback");
                motion.Value = 100; Report(runtime, device, bindings, 320, 1);
                motion.Value = 0; Report(runtime, device, bindings, 330, 1);
                Assert(runtime.Step(bindings, 330, logs.Add).Length == 0, "Unter Schwelle keine Einzelaktion");
                motion.Value = 350; Report(runtime, device, bindings, 340, 1);
                motion.Value = 0; Report(runtime, device, bindings, 350, 1);
                Assert(runtime.Step(bindings, 600, logs.Add).Length == 0, "Veraltete fertige Einzelaktion nach Timerstau verworfen");
                foreach (int index in new[] { 6, 7 })
                {
                    runtime.Reset();
                    x.Function = PremiereFunction.None;
                    bindings.Items[index].Function = PremiereFunction.Playback;
                    ushort key = index == 6 ? (ushort)1 : (ushort)2;
                    buttons.Pressed.Add(key);
                    Report(runtime, device, bindings, 0, 2);
                    buttons.Pressed.Clear(); Report(runtime, device, bindings, 10, 2);
                    Assert(runtime.Step(bindings, 10, logs.Add).Length == 0, "Initial gehaltene Taste loest nicht aus");
                    buttons.Pressed.Add(key); Report(runtime, device, bindings, 20, 2);
                    Report(runtime, device, bindings, 200, 1);
                    Assert(runtime.Step(bindings, 200, logs.Add).Length == 0, "Halten und Achsenreport loesen Taste nicht aus");
                    buttons.Pressed.Clear(); Report(runtime, device, bindings, 5000, 2);
                    Assert(runtime.Step(bindings, 5000, logs.Add).Length == 1, "Beide Tasten bei frischem Loslassen, auch nach langem Halten");
                    bindings.Items[index].Function = PremiereFunction.None;
                }
            }
            // Button-only HID collections must not require an axis report.
            using (var auxiliary = new Device { LastReportId = 3, Reports = 1 })
            {
                var group = new ButtonGroup { Seen = true, ValueReport = 1, ReportId = 3 };
                group.Available.Add(1); auxiliary.Buttons.Add(group);
                runtime.Reset();
                bindings.Items[6].Function = PremiereFunction.Playback;
                runtime.Receive(auxiliary, bindings, 0);
                auxiliary.Reports = group.ValueReport = 2; group.Pressed.Add(1);
                runtime.Receive(auxiliary, bindings, 10);
                auxiliary.Reports = group.ValueReport = 3; group.Pressed.Clear();
                runtime.Receive(auxiliary, bindings, 20);
                Assert(runtime.Step(bindings, 20, logs.Add).Length == 1, "Tasten einer Zusatzcollection unterstuetzt");
            }
        }

        private static void Report(PremiereBindingRuntime runtime, Device device, PremiereBindings bindings, double now, byte id)
        {
            device.LastReportId = id; device.Reports++;
            foreach (Axis axis in device.Axes.Where(a => a.ReportId == id)) axis.ValueReport = device.Reports;
            foreach (ButtonGroup group in device.Buttons.Where(b => b.ReportId == id))
            { group.Seen = true; group.ValueReport = device.Reports; }
            runtime.Receive(device, bindings, now);
        }

        private static void PersistenceAndEditor()
        {
            string temporary = Path.Combine(Path.GetTempPath(), "SpaceMouseBridge-PremiereBindings-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new MappingStore(temporary, null, ApplicationProfile.Premiere);
                var mapping = Legacy();
                store.Apply(mapping, "absent", "absent");
                byte[] before = File.ReadAllBytes(store.SettingsPath);
                byte[] undo = File.ReadAllBytes(store.LastBackupPath);
                using (var editor = new PremiereMappingEditor(store, mapping))
                {
                    editor.Show();
                    var initial = editor.Collect();
                    Assert(initial.PremiereInputs.Items[0].EndRate == 96 &&
                        initial.PremiereInputs.Items[5].Response.EffectiveMinimumRate == 0.5m,
                        "Neuer Editor uebernimmt bisherige Werte");
                    Control<ComboBox>(editor, "function").SelectedIndex = (int)PremiereFunction.Shortcut;
                    Assert(Control<Label>(editor, "feedback").Text.Length > 0, "Fehlendes Kuerzel sichtbar gemeldet");
                    Reject(delegate { editor.Collect(); }, "Fehlendes Kuerzel verhindert Speichern");
                    Control<ComboBox>(editor, "function").SelectedIndex = (int)PremiereFunction.HorizontalScroll;
                    for (int index = 0; index < 8; index++)
                    {
                        editor.SelectInput(index);
                        Control<ComboBox>(editor, "function").SelectedIndex = (int)PremiereFunction.Jog;
                        Control<CheckBox>(editor, "reverse").Checked = true;
                        if (index < 6)
                        {
                            Control<NumericUpDown>(editor, "minimum").Value = 1;
                            Control<NumericUpDown>(editor, "maximum").Value = 32;
                            Control<NumericUpDown>(editor, "endRate").Value = 100;
                            Control<NumericUpDown>(editor, "exponent").Value = 3;
                            Control<NumericUpDown>(editor, "delay").Value = 400;
                        }
                        mapping = editor.Collect();
                        Assert(mapping.PremiereInputs.Items[index].Function == PremiereFunction.Jog &&
                            mapping.PremiereInputs.Items[index].Response.Reverse, "Alle acht Eingaben editierbar");
                    }
                    editor.SelectInput(1);
                    Control<ComboBox>(editor, "function").SelectedIndex = (int)PremiereFunction.Shortcut;
                    typeof(PremiereMappingEditor).GetField("negativeKey", BindingFlags.Instance | BindingFlags.NonPublic)
                        .SetValue(editor, new PremiereShortcut { Key = (int)Keys.F10, Control = true });
                    typeof(PremiereMappingEditor).GetField("positiveKey", BindingFlags.Instance | BindingFlags.NonPublic)
                        .SetValue(editor, new PremiereShortcut { Key = (int)Keys.F11, Shift = true, Alt = true });
                    mapping = editor.Collect();
                    editor.SelectInput(7);
                    Control<ComboBox>(editor, "function").SelectedIndex = (int)PremiereFunction.ContextMenu;
                    typeof(PremiereMappingEditor).GetField("menuItems", BindingFlags.Instance | BindingFlags.NonPublic)
                        .SetValue(editor, new[] { new PremiereMenuItem {
                            Name = "Eigener Befehl", Shortcut = new PremiereShortcut { Key = (int)Keys.F7, Alt = true }
                        } });
                    mapping = editor.Collect();
                    using (var image = new System.Drawing.Bitmap(editor.Width, editor.Height))
                        editor.DrawToBitmap(image, new System.Drawing.Rectangle(System.Drawing.Point.Empty, image.Size));
                }
                Assert(before.SequenceEqual(File.ReadAllBytes(store.SettingsPath)) &&
                    undo.SequenceEqual(File.ReadAllBytes(store.LastBackupPath)), "Abbrechen/Vorschau schreibt keine Dateien");
                store.Apply(mapping, "absent", MappingStore.Hash(before));
                var loaded = store.Load();
                Assert(loaded.PremiereMenu.Length == 1 && loaded.PremiereMenu[0].Name == "Eigener Befehl" &&
                    loaded.PremiereMenu[0].Shortcut.Key == (int)Keys.F7 && loaded.PremiereMenu[0].Shortcut.Alt,
                    "Eigene Menueeintraege samt Kuerzeln persistent");
                Assert(loaded.PremiereInputs.Items[7].Function == PremiereFunction.ContextMenu,
                    "Menueaktion im Mapping-Editor zuweisbar und gespeichert");
                Assert(loaded.PremiereInputs.Items.Length == 8 && loaded.PremiereInputs.Items.All(b => b.Response.Reverse) &&
                    loaded.PremiereInputs.Items[3].Response.CurveExponent == 3 &&
                    loaded.PremiereInputs.Items[3].EndRate == 100, "Neue Belegungen und Kennlinien persistent");
                Assert(loaded.PremiereInputs.Items[1].Negative.Key == (int)Keys.F10 &&
                    loaded.PremiereInputs.Items[1].Negative.Control &&
                    loaded.PremiereInputs.Items[1].Positive.Key == (int)Keys.F11 &&
                    loaded.PremiereInputs.Items[1].Positive.Shift && loaded.PremiereInputs.Items[1].Positive.Alt,
                    "Getrennte Richtungs-Kuerzel durch Editor und JSON erhalten");
                var session = new ProfileSession(store);
                session.Load();
                var router = new ProfileRouter();
                router.Configure(new[] { session });
                router.Select(new IntPtr(1), 1, "Adobe Premiere Pro", "Premiere");
                Assert(router.CanExecute && router.ActiveMapping.PremiereInputs != null, "Neue Belegungen in Premiere aktiv");
                router.Select(new IntPtr(2), 2, "acad", "CAD");
                Assert(!router.CanExecute, "Neue Belegungen nicht in CAD aktiv");
                store.Restore();
                Assert(before.SequenceEqual(File.ReadAllBytes(store.SettingsPath)), "Undo stellt Legacy-Setup bytegenau wieder her");
            }
            finally { if (Directory.Exists(temporary)) Directory.Delete(temporary, true); }
        }
    }
}
