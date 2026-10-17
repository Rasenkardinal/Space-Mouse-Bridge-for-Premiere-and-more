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
                if (function == PremiereFunction.None || function == PremiereFunction.ContextMenu ||
                    function == PremiereFunction.ContextMenu2) continue;
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
            Icons();
            MenuAssignments();
            MenuPress();
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
            copy.PremiereMenu = null;
            copy.Validate();
            Assert(copy.PremiereInputs.Items[6].Function == PremiereFunction.ContextMenu,
                "Altes Profil mit Menueaktion ohne Menueliste bleibt ladbar und verliert keine Belegung");
            var legacyLog = new List<string>();
            var legacyRuntime = new ApplicationMappings(legacyLog.Add);
            typeof(ApplicationMappings).GetMethod("OpenContextMenu", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(legacyRuntime, new object[] { null, "Premiere" });
            Assert(legacyLog.Any(message => message.Contains("Kein Radialmenue konfiguriert")),
                "Fehlendes Altmenue meldet Konfigurationsbedarf statt das gesamte Profil zu sperren");
            copy = mapping.Clone();
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
                Reject(delegate { editor.Collect(); }, "Alle vier Startsegmente muessen belegt werden");
            using (var editor = new PremiereMenuEditor(null, "Legacy", 200))
            {
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var radiusControl = (NumericUpDown)typeof(PremiereMenuEditor).GetField("radius", flags).GetValue(editor);
                var radiusTrack = (TrackBar)typeof(PremiereMenuEditor).GetField("radiusSlider", flags).GetValue(editor);
                Assert(editor.MenuRadius == 180 && radiusControl.Maximum == 180 && radiusTrack.Maximum == 180,
                    "Alter Radius 200 bleibt editierbar und wird auf maximal 180 begrenzt");
            }
            var configured = Enumerable.Range(0, 4).Select(i => new PremiereMenuItem {
                Name = "Befehl " + i, Shortcut = new PremiereShortcut { Key = (int)Keys.F1 + i }
            }).ToArray();
            using (var editor = new PremiereMenuEditor(configured))
            {
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var name = (TextBox)typeof(PremiereMenuEditor).GetField("entryName", flags).GetValue(editor);
                name.Text = "Neuer Name";
                Assert(editor.Collect()[0].Name == "Neuer Name" && configured[0].Name == "Befehl 0",
                    "Menueeditor bearbeitet Entwurf ohne Originalaenderung");
                var preview = (RadialMenuView)typeof(PremiereMenuEditor).GetField("preview", flags).GetValue(editor);
                typeof(RadialMenuView).GetMethod("OnMouseDown", flags).Invoke(preview,
                    new object[] { new MouseEventArgs(MouseButtons.Left, 1, 134, 82, 0) });
                Assert(preview.SelectedSlot == 1 && name.Text == "Befehl 1",
                    "Direkter Segmentklick waehlt die zugehoerige Belegung aus");
                name.Text = " ";
                Reject(delegate { editor.Collect(); }, "Leere Segmentbelegung verhindert Uebernahme");
                name.Text = "Einsetzen";
                var count = (NumericUpDown)typeof(PremiereMenuEditor).GetField("segmentCount", flags).GetValue(editor);
                Assert(count.Minimum == 4 && count.Maximum == 8, "Segmentanzahl ist auf 4 bis 8 begrenzt");
                count.Value = 5;
                Assert(preview.SegmentCount == 5, "Anzahl passt die Vorschau sofort an");
                Reject(delegate { editor.Collect(); }, "Neues Segment benoetigt Name und Kuerzel");
                count.Value = 4;
                Assert(editor.Collect().Length == 4, "Entfernen eines unbelegten Entwurfssegments erhaelt bestehende Belegung");
                var radius = (NumericUpDown)typeof(PremiereMenuEditor).GetField("radius", flags).GetValue(editor);
                var slider = (TrackBar)typeof(PremiereMenuEditor).GetField("radiusSlider", flags).GetValue(editor);
                var fixedCenter = new System.Drawing.Point(preview.Left + preview.Width / 2,
                    preview.Top + preview.Height / 2);
                Assert(!((Panel)preview.Parent.Parent).AutoScroll,
                    "Radialmenuevorschau zeigt keine Scrollbalken");
                foreach (int value in new[] { 40, 77, 180 })
                {
                    radius.Value = value;
                    Assert(new System.Drawing.Point(preview.Left + preview.Width / 2,
                        preview.Top + preview.Height / 2) == fixedCenter,
                        "Radiusaenderung behaelt die Kreismitte an derselben Position");
                    Assert(preview.Left >= 0 && preview.Top >= 30 &&
                        preview.Right <= preview.Parent.Width && preview.Bottom <= preview.Parent.Height,
                        "Alle Radien passen vollstaendig auf die Vorschauflaeche");
                    Assert(preview.MenuRadius == value && slider.Value == value &&
                        preview.Width == value * 2 + 10 && editor.MenuRadius == value,
                        "Radius-Zahlenfeld aktualisiert die 1:1-Vorschau und den Regler sofort");
                    Assert(preview.SlotAt(new System.Drawing.Point(preview.Width / 2,
                        preview.Height / 2 - (int)(value * 0.7))) == 0 &&
                        preview.SlotAt(new System.Drawing.Point(preview.Width / 2,
                        preview.Height / 2 - value - 1)) == -1, "Radius-Hit-Test passt zur Vorschau");
                }
                slider.Value = 100;
                Assert(radius.Value == 100 && preview.MenuRadius == 100, "Regler synchronisiert Radius und Vorschau");
                var center = (NumericUpDown)typeof(PremiereMenuEditor).GetField("centerPercent", flags).GetValue(editor);
                var centerSlider = (TrackBar)typeof(PremiereMenuEditor).GetField("centerSlider", flags).GetValue(editor);
                foreach (int value in new[] { 15, 33, 60 })
                {
                    center.Value = value;
                    Assert(preview.CenterPercent == value && centerSlider.Value == value &&
                        editor.MenuCenterPercent == value, "Mittelkreis-Regler und Livevorschau synchronisiert");
                    int midpoint = preview.Width / 2;
                    Assert(preview.SlotAt(new System.Drawing.Point(midpoint,
                        midpoint - (int)(100 * value / 100.0 - 1))) == -1,
                        "Mittelkreis ist keine Segment-Klickflaeche");
                    using (var region = preview.CreateMenuRegion())
                        Assert(region.IsVisible(midpoint, midpoint), "Variable Fensterkontur enthaelt den Mittelkreis");
                    using (var bitmap = new System.Drawing.Bitmap(preview.Width, preview.Height))
                    {
                        preview.DrawToBitmap(bitmap, new System.Drawing.Rectangle(0, 0, preview.Width, preview.Height));
                        var expectedColor = AppTheme.Current.Surface;
                        Assert(bitmap.GetPixel(midpoint, midpoint + (int)(value * 0.8)).ToArgb() == expectedColor.ToArgb(),
                            "Mittelkreis verwendet dieselbe Hintergrundfarbe wie normale Segmente");
                    }
                }
                centerSlider.Value = 40;
                Assert(center.Value == 40 && preview.CenterPercent == 40, "Mittelkreis-Schieberegler aktualisiert Zahlenfeld");
                var border = (NumericUpDown)typeof(PremiereMenuEditor).GetField("borderWidth", flags).GetValue(editor);
                var borderSlider = (TrackBar)typeof(PremiereMenuEditor).GetField("borderSlider", flags).GetValue(editor);
                foreach (decimal value in new[] { 0.5m, 1m, 5m })
                {
                    border.Value = value;
                    Assert(preview.BorderWidth == (float)value && borderSlider.Value == (int)(value * 10) &&
                        editor.MenuBorderWidth == value, "Konturlinienbreite aktualisiert Livevorschau und Regler");
                }
                borderSlider.Value = 25;
                Assert(border.Value == 2.5m, "Konturlinienregler aktualisiert Zahlenfeld");
                var gap = (NumericUpDown)typeof(PremiereMenuEditor).GetField("segmentGap", flags).GetValue(editor);
                var gapSlider = (TrackBar)typeof(PremiereMenuEditor).GetField("gapSlider", flags).GetValue(editor);
                gap.Value = 8;
                Assert(preview.SegmentGap == 8 && gapSlider.Value == 80 && editor.MenuSegmentGap == 8,
                    "Segmentabstand aktualisiert Vorschau und Regler");
                gapSlider.Value = 35;
                Assert(gap.Value == 3.5m && preview.SegmentGap == 3.5f, "Abstandsregler aktualisiert Zahlenfeld");
                var textSize = (NumericUpDown)typeof(PremiereMenuEditor).GetField("textSize", flags).GetValue(editor);
                var textSlider = (TrackBar)typeof(PremiereMenuEditor).GetField("textSlider", flags).GetValue(editor);
                foreach (decimal value in new[] { 4m, 6m, 16m })
                {
                    textSize.Value = value;
                    Assert(preview.TextSize == (float)value && textSlider.Value == (int)(value * 10) &&
                        editor.MenuTextSize == value, "Textgroesse aktualisiert Regler und Vorschau");
                }
                textSlider.Value = 95;
                Assert(textSize.Value == 9.5m, "Textregler aktualisiert Zahlenfeld");
                editor.PerformLayout();
                Assert(slider.Left == borderSlider.Left && centerSlider.Left == gapSlider.Left &&
                    slider.Width == borderSlider.Width && centerSlider.Width == gapSlider.Width &&
                    radius.Left == border.Left && center.Left == gap.Left,
                    "Regler und Zahlenfelder beider Zeilen sind spaltengenau ausgerichtet");
                Assert(radius.Top == center.Top && slider.Top == centerSlider.Top,
                    "Radius und Mittelkreis liegen nebeneinander");
            }
            using (var targetForm = new ThemedForm())
            foreach (bool empty in new[] { true, false })
            {
                uint targetPid;
                Native.GetWindowThreadProcessId(targetForm.Handle, out targetPid);
                using (var radial = new BridgeRadialMenu(empty ? null : mapping.PremiereMenu,
                    Cursor.Position, targetForm.Handle, targetPid))
                {
                    Assert(radial.Controls[0] is RadialMenuView,
                        "Echtes Menue verwendet dieselbe Darstellung wie der Editor");
                if (!empty)
                {
                        radial.ActivateSlot(0);
                        Assert(radial.SelectedShortcut == mapping.PremiereMenu[0].Shortcut,
                            "Radialauswahl gibt das konfigurierte Kuerzel ohne direkte Ausgabe zurueck");
                    }
                }
            }
            using (var targetForm = new ThemedForm())
            {
                targetForm.Show();
                targetForm.Activate();
                uint targetPid;
                Native.GetWindowThreadProcessId(targetForm.Handle, out targetPid);
                foreach (Screen screen in Screen.AllScreens)
                {
                    var area = screen.WorkingArea;
                    foreach (var point in new[] {
                        new System.Drawing.Point(area.Left + area.Width / 2, area.Top + area.Height / 2),
                        area.Location, new System.Drawing.Point(area.Right - 1, area.Bottom - 1)
                    })
                    using (var radial = new BridgeRadialMenu(mapping.PremiereMenu, point, targetForm.Handle, targetPid))
                    {
                        var expected = radial.Location;
                        radial.Show();
                        Assert(radial.Width == 164 && radial.Height == 164,
                            "Radialmenue hat halbe Groesse mit 77 Pixel Aussenradius");
                        var view = (RadialMenuView)radial.Controls[0];
                        Assert(view.SlotAt(new System.Drawing.Point(radial.Width / 2, radial.Height / 2 - 52)) == 0 &&
                            view.SlotAt(new System.Drawing.Point(radial.Width / 2, radial.Height / 2 - 78)) == -1,
                            "Klickflaechen entsprechen dem halbierten Radius");
                        Assert(radial.Location == expected &&
                            area.Contains(radial.Bounds), "Anzeigen behaelt die Mausposition auf jedem Monitor und am Rand");
                        if (point.X == area.Left + area.Width / 2 && point.Y == area.Top + area.Height / 2)
                            Assert(radial.Left + radial.Width / 2 == point.X &&
                                radial.Top + radial.Height / 2 == point.Y, "Radialmittelpunkt liegt exakt am Mauszeiger");
                        radial.Close();
                    }
                }
                var many = Enumerable.Range(0, 13).Select(i => new PremiereMenuItem {
                    Name = "Befehl " + i, Shortcut = new PremiereShortcut { Key = (int)Keys.F1 + i }
                }).ToArray();
                using (var editor = new PremiereMenuEditor(many))
                {
                    Reject(delegate { editor.Collect(); }, "Alte lange Menues werden nicht stillschweigend gekuerzt");
                    Assert(many.Length == 13, "Vorhandene Eintraege bleiben bis zur bewussten Reduktion erhalten");
                }
                for (int count = 4; count <= 8; count++)
                {
                    var entries = many.Take(count).ToArray();
                    PremiereMenuItem.ValidateConfiguration(entries);
                    using (var radial = new BridgeRadialMenu(entries, Cursor.Position, targetForm.Handle, targetPid))
                    using (var editor = new PremiereMenuEditor(entries))
                    {
                        var view = (RadialMenuView)radial.Controls[0];
                        var preview = (RadialMenuView)typeof(PremiereMenuEditor).GetField("preview",
                            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor);
                        Assert(view.SegmentCount == count && preview.SegmentCount == count &&
                            editor.Collect().Length == count, "4 bis 8 Segmente im Editor und zur Laufzeit identisch");
                        for (int slot = 0; slot < count; slot++)
                        {
                            double angle = (-90 + slot * 360.0 / count) * Math.PI / 180;
                            var point = new System.Drawing.Point(82 + (int)Math.Round(52 * Math.Cos(angle)),
                                82 + (int)Math.Round(52 * Math.Sin(angle)));
                            Assert(view.SlotAt(point) == slot && preview.SlotAt(point) == slot,
                                "Gleichmaessige Vollkreisaufteilung und Hit-Tests fuer jedes Segment");
                        }
                        preview.SelectedSlot = 0;
                        preview.BackColor = view.BackColor;
                        typeof(RadialMenuView).GetMethod("OnMouseMove", BindingFlags.Instance | BindingFlags.NonPublic)
                            .Invoke(view, new object[] { new MouseEventArgs(MouseButtons.None, 0, 82, 30, 0) });
                        using (var actual = new System.Drawing.Bitmap(164, 164))
                        using (var expected = new System.Drawing.Bitmap(164, 164))
                        {
                            var bounds = new System.Drawing.Rectangle(0, 0, 164, 164);
                            view.DrawToBitmap(actual, bounds);
                            preview.DrawToBitmap(expected, bounds);
                            for (int y = 0; y < 164; y++)
                            for (int x = 0; x < 164; x++)
                            {
                                Assert(actual.GetPixel(x, y) == expected.GetPixel(x, y),
                                    "Editorvorschau und echtes Menue sind pixelgleich");
                                var pixel = actual.GetPixel(x, y);
                                Assert(!(pixel.R > pixel.G + 30 && pixel.B > pixel.G + 30),
                                    "Keine pinken Transparenzreste an geglaetteten Menuekanten");
                            }
                            Assert(actual.GetPixel(82, 12).ToArgb() == (AppTheme.Current.HighContrast ?
                                System.Drawing.SystemColors.Highlight : System.Drawing.Color.FromArgb(40, 124, 222)).ToArgb(),
                                "Auswahl verwendet das Blau aus dem Entwurf");
                        }
                        radial.ActivateSlot(count - 1);
                        Assert(radial.SelectedShortcut == entries[count - 1].Shortcut,
                            "Letztes Segment ist ein Befehl, keine Seitennavigation");
                    }
                }
                foreach (int radius in new[] { 40, 77, 180 })
                using (var radial = new BridgeRadialMenu(configured, Cursor.Position, targetForm.Handle, targetPid, radius))
                {
                    var view = (RadialMenuView)radial.Controls[0];
                    foreach (int count in new[] { 4, 6, 8 })
                    foreach (int center in new[] { 15, 60 })
                    foreach (float gap in new[] { 0f, 2f, 8f })
                    {
                        view.CenterPercent = center;
                        view.SegmentGap = gap;
                        float sweep = 360f / count;
                        float start = -90 - sweep / 2;
                        var method = typeof(RadialMenuView).GetMethod("Sector", BindingFlags.Instance | BindingFlags.NonPublic);
                        using (var path = (System.Drawing.Drawing2D.GraphicsPath)method.Invoke(view,
                            new object[] { System.Drawing.Point.Empty, start, sweep }))
                        {
                            var points = path.PathPoints;
                            double angle = start * Math.PI / 180;
                            double nx = -Math.Sin(angle), ny = Math.Cos(angle);
                            double innerOffset = (points[0].X * nx + points[0].Y * ny) * radius / 77;
                            double outerOffset = (points[1].X * nx + points[1].Y * ny) * radius / 77;
                            Assert(Math.Abs(innerOffset - gap / 2) < 0.001 &&
                                Math.Abs(outerOffset - gap / 2) < 0.001,
                                "Gerade Segmentkante ist parallel zur Grenze mit konstantem halben Pixelabstand");
                        }
                    }
                    view.CenterPercent = 33;
                    view.SegmentGap = 2;
                    Assert(radial.ClientSize.Width == radius * 2 + 10 &&
                        ((RadialMenuView)radial.Controls[0]).MenuRadius == radius,
                        "Laufzeitmenue verwendet den konfigurierten Radius");
                    Assert(radial.TransparencyKey.IsEmpty && radial.Region != null &&
                        !radial.Region.IsVisible(0, 0) &&
                        radial.Region.IsVisible(radius + 5, radius + 5) &&
                        radial.Region.IsVisible(radius + 5, radius + 5 - radius * 0.7f) &&
                        !radial.Region.IsVisible(radius + 5, radius + 5 - radius - 1),
                        "Fensterkontur statt pinkem Farbschluessel bei allen Radien");
                }
                Reject(delegate { PremiereMenuItem.ValidateConfiguration(many.Take(3).ToArray()); }, "Weniger als vier Segmente abgewiesen");
                Reject(delegate { PremiereMenuItem.ValidateConfiguration(many.Take(9).ToArray()); }, "Mehr als acht Segmente abgewiesen");
            }
        }

        private static void Icons()
        {
            var entries = Enumerable.Range(0, 4).Select(i => new PremiereMenuItem {
                Name = "Werkzeug " + i, Shortcut = new PremiereShortcut { Key = (int)Keys.F1 + i }
            }).ToArray();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            using (var editor = new PremiereMenuEditor(entries))
            {
                var mode = (ComboBox)typeof(PremiereMenuEditor).GetField("displayMode", flags).GetValue(editor);
                var picker = (MenuIconPicker)typeof(PremiereMenuEditor).GetField("iconPicker", flags).GetValue(editor);
                var preview = (RadialMenuView)typeof(PremiereMenuEditor).GetField("preview", flags).GetValue(editor);
                Assert(mode.SelectedIndex == 0 && !picker.Enabled, "Bestehende Segmente bleiben im Textmodus");
                mode.SelectedIndex = 1;
                Assert(picker.Enabled, "Iconmodus aktiviert Iconauswahl");
                var size = (NumericUpDown)typeof(PremiereMenuEditor).GetField("textSize", flags).GetValue(editor);
                var slider = (TrackBar)typeof(PremiereMenuEditor).GetField("textSlider", flags).GetValue(editor);
                var sizeLabel = (Label)typeof(PremiereMenuEditor).GetField("sizeLabel", flags).GetValue(editor);
                Assert(size.Value == 24 && size.Minimum == 8 && size.Maximum == 48 &&
                    sizeLabel.Text == "Icon (px)", "Groessenregler wechselt zu Icons");
                slider.Value = 320;
                Assert(editor.MenuIconSize == 32 && preview.IconSize == 32 && editor.MenuTextSize == 6,
                    "Iconregler aendert nur die menueweite Icongroesse");
                mode.SelectedIndex = 0;
                Assert(size.Value == 6 && size.Maximum == 16 &&
                    sizeLabel.Text == "Text (pt)", "Textgroesse bleibt beim Moduswechsel erhalten");
                size.Value = 9;
                mode.SelectedIndex = 1;
                Assert(size.Value == 32 && editor.MenuTextSize == 9 && preview.TextSize == 9,
                    "Text und Icon besitzen getrennte Groessenwerte");
                Reject(delegate { editor.Collect(); }, "Iconmodus ohne ausgewaehltes Icon verhindert Uebernahme");
                picker.SelectedItem = MenuIcon.Razor;
                Assert(preview.Items[0].Display == MenuSegmentDisplay.Icon && preview.Items[0].Icon == MenuIcon.Razor &&
                    editor.Collect()[0].Icon == MenuIcon.Razor, "Iconauswahl erscheint sofort in Vorschau und Entwurf");
                Assert(entries[0].Display == MenuSegmentDisplay.Text && entries[0].Icon == MenuIcon.None,
                    "Iconentwurf aendert Original nicht");
                typeof(PremiereMenuEditor).GetMethod("SelectSegment", flags).Invoke(editor, new object[] { 1 });
                Assert(mode.SelectedIndex == 0 && picker.SelectedItem.Equals(MenuIcon.None), "Segmentwechsel laedt eigene Darstellung");
                typeof(PremiereMenuEditor).GetMethod("SelectSegment", flags).Invoke(editor, new object[] { 0 });
                Assert(mode.SelectedIndex == 1 && picker.SelectedItem.Equals(MenuIcon.Razor), "Segmentwechsel behaelt Icon");
                mode.SelectedIndex = 0;
                Assert(editor.Collect()[0].Icon == MenuIcon.Razor && editor.Collect()[0].Display == MenuSegmentDisplay.Text,
                    "Wechsel zu Text behaelt Iconauswahl ohne sie anzuzeigen");
                mode.SelectedIndex = 1;
                foreach (var icon in new[] { MenuIcon.Razor, MenuIcon.Split, MenuIcon.Trim, MenuIcon.Play,
                    MenuIcon.Pause, MenuIcon.Render, MenuIcon.Remove, MenuIcon.CloseGap })
                {
                    Assert(picker.Items.Contains(icon), "Neues A1-Icon ist auswaehlbar: " + icon);
                    picker.SelectedItem = icon;
                    Assert(preview.Items[0].Icon == icon && editor.Collect()[0].Icon == icon,
                        "Neues A1-Icon wird sofort angezeigt und uebernommen: " + icon);
                }
                picker.SelectedItem = MenuIcon.Razor;
                Assert(editor.CollectConfiguration().IconSize == 32, "Icongroesse wird in Menuekonfiguration uebernommen");
                mode.SelectedIndex = 0;
                size.Value = 6;
                mode.SelectedIndex = 1;
                size.Value = 24;
                entries = editor.Collect();
                foreach (var palette in new[] { new ThemePalette(true, false), new ThemePalette(false, true) })
                {
                    AppTheme.Apply(editor, palette);
                    Assert(picker.DrawMode == DrawMode.OwnerDrawFixed, "Iconliste bleibt auch in hohem Kontrast gezeichnet");
                }
            }
            var invalid = new PremiereMenuItem { Name = "Test", Shortcut = entries[0].Shortcut,
                Display = (MenuSegmentDisplay)99 };
            Reject(delegate { PremiereMenuItem.Validate(invalid); }, "Unbekannter Anzeigemodus abgewiesen");
            invalid.Display = MenuSegmentDisplay.Icon; invalid.Icon = (MenuIcon)999;
            Reject(delegate { PremiereMenuItem.Validate(invalid); }, "Unbekanntes Icon abgewiesen");
            Assert((int)MenuIcon.Razor == 3 && (int)MenuIcon.Mute == 26,
                "Bestehende gespeicherte Iconnummern bleiben unveraendert");
            foreach (int size in new[] { 16, 24, 48 })
            {
                var signatures = new HashSet<string>();
                foreach (MenuIcon icon in Enum.GetValues(typeof(MenuIcon)).Cast<MenuIcon>().Where(i => i != MenuIcon.None))
                {
                    Assert(!string.IsNullOrWhiteSpace(MenuIcons.Label(icon)), "Jedes Icon hat einen lesbaren Namen");
                    using (var bitmap = new System.Drawing.Bitmap(size, size))
                    using (var graphics = System.Drawing.Graphics.FromImage(bitmap))
                    {
                        graphics.Clear(System.Drawing.Color.Black);
                        MenuIcons.Draw(graphics, icon, new System.Drawing.RectangleF(0, 0, size, size), System.Drawing.Color.White);
                        var pixels = new List<byte>();
                        for (int y = 0; y < size; y++)
                        for (int x = 0; x < size; x++)
                        {
                            var pixel = bitmap.GetPixel(x, y);
                            Assert(pixel.R == pixel.G && pixel.G == pixel.B, "Icons bleiben einfarbig");
                            pixels.Add(pixel.R);
                        }
                        Assert(pixels.Count(p => p > 0) > 20, "Jedes Icon zeichnet sichtbare Linien: " + icon);
                        Assert(signatures.Add(Convert.ToBase64String(pixels.ToArray())), "Jedes Icon hat eine eigene Zeichnung: " + icon);
                    }
                }
            }
            entries[1].Display = entries[2].Display = entries[3].Display = MenuSegmentDisplay.Icon;
            entries[1].Icon = MenuIcon.Render;
            entries[2].Icon = MenuIcon.Play;
            entries[3].Icon = MenuIcon.Remove;
            foreach (int radius in new[] { 40, 77, 180 })
            foreach (int center in new[] { 15, 60 })
            using (var view = new RadialMenuView { Items = entries, MenuRadius = radius, CenterPercent = center })
            using (var bitmap = new System.Drawing.Bitmap(view.Width, view.Height))
                view.DrawToBitmap(bitmap, new System.Drawing.Rectangle(0, 0, view.Width, view.Height));
            using (var target = new ThemedForm())
            using (var runtime = new BridgeRadialMenu(entries, Cursor.Position, target.Handle, 0, 77, 33, 1, 2, 6, 32))
            using (var editor = new PremiereMenuEditor(entries, "Premiere", 77, 33, 1, 2, 6, 32))
            {
                var view = (RadialMenuView)runtime.Controls[0];
                var preview = (RadialMenuView)typeof(PremiereMenuEditor).GetField("preview", flags).GetValue(editor);
                preview.SelectedSlot = -1;
                preview.BackColor = view.BackColor;
                using (var actual = new System.Drawing.Bitmap(164, 164))
                using (var expected = new System.Drawing.Bitmap(164, 164))
                {
                    var bounds = new System.Drawing.Rectangle(0, 0, 164, 164);
                    view.DrawToBitmap(actual, bounds); preview.DrawToBitmap(expected, bounds);
                    for (int y = 0; y < 164; y++)
                    for (int x = 0; x < 164; x++)
                        Assert(actual.GetPixel(x, y) == expected.GetPixel(x, y), "Iconvorschau und echtes Menue sind pixelgleich");
                }
                runtime.ActivateSlot(0);
                Assert(runtime.SelectedShortcut == entries[0].Shortcut, "Iconsegment sendet weiterhin sein eigenes Kuerzel");
            }
        }

        private static void MenuAssignments()
        {
            foreach (bool premiere in new[] { false, true })
            foreach (bool swapped in new[] { false, true })
            {
                var mapping = premiere ? Legacy() : ApplicationMapping.Default();
                mapping.PremiereInputs = premiere ? PremiereBindings.From(mapping) : null;
                for (ushort button = 1; button <= 2; button++)
                {
                    int expected = swapped ? 3 - button : button;
                    if (premiere)
                        mapping.PremiereInputs.Items.Single(b => b.Input == "Button" + button).Function =
                            expected == 1 ? PremiereFunction.ContextMenu : PremiereFunction.ContextMenu2;
                    else
                    {
                        mapping.Buttons[button - 1].Mode = ButtonMode.Bridge;
                        mapping.Buttons[button - 1].Action = expected == 1 ? BrowserAction.ContextMenu : BrowserAction.ContextMenu2;
                    }
                    Assert(ApplicationMappings.MenuForButton(mapping, premiere, button) == expected,
                        "Menue 1 und 2 sind beiden Tasten auch vertauscht frei zuweisbar");
                }
                if (premiere)
                    foreach (var binding in mapping.PremiereInputs.Items.Where(b => b.Button))
                        binding.Function = PremiereFunction.ContextMenu2;
                else
                    foreach (var binding in mapping.Buttons) binding.Action = BrowserAction.ContextMenu2;
                Assert(ApplicationMappings.MenuForButton(mapping, premiere, 1) == 2 &&
                    ApplicationMappings.MenuForButton(mapping, premiere, 2) == 2,
                    "Beide Tasten duerfen dasselbe zweite Menue oeffnen");
            }
            var old = Legacy();
            old.PremiereInputs = PremiereBindings.From(old);
            Assert(ApplicationMappings.MenuForButton(old, true, 1) == 0, "Unbelegte Taste oeffnet kein Menue");
            Assert((int)BrowserAction.ContextMenu == 11 && (int)PremiereFunction.ContextMenu == 7,
                "Bisherige gespeicherte Menueaktionsnummern bleiben unveraendert");
        }

        private static void MenuPress()
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            foreach (ApplicationProfile profile in ApplicationProfile.All)
            foreach (int menuNumber in new[] { 1, 2 })
            foreach (bool withAxes in new[] { false, true })
            foreach (ushort button in new ushort[] { 1, 2 })
            using (var target = new ThemedForm { Text = "Bridge-Menue - Tastendrucktest" })
            using (var device = new Device { Preparsed = System.Runtime.InteropServices.Marshal.AllocHGlobal(1) })
            {
                var mapping = profile.EmptyMapping();
                mapping.Enabled = true;
                var items = new[] { new PremiereMenuItem {
                    Name = "Test", Shortcut = new PremiereShortcut { Key = (int)Keys.F8 }
                } };
                mapping.Menu2 = new RadialMenuConfiguration {
                    Items = Enumerable.Range(0, 4).Select(i => new PremiereMenuItem {
                        Name = "Zweites Menue " + i, Shortcut = new PremiereShortcut { Key = (int)Keys.F9 }
                    }).ToArray(), Radius = 120, CenterPercent = 45, BorderWidth = 2, SegmentGap = 4, TextSize = 8
                };
                if (profile == ApplicationProfile.Premiere)
                {
                    mapping.PremiereMenu = items;
                    mapping.PremiereInputs = PremiereBindings.From(mapping);
                    foreach (var binding in mapping.PremiereInputs.Items) binding.Function = PremiereFunction.None;
                    mapping.PremiereInputs.Items.Single(b => b.Input == "Button" + button).Function =
                        menuNumber == 1 ? PremiereFunction.ContextMenu : PremiereFunction.ContextMenu2;
                }
                else
                {
                    mapping.ChromeMenu = items;
                    mapping.Buttons[button - 1].Mode = ButtonMode.Bridge;
                    mapping.Buttons[button - 1].Action = menuNumber == 1 ? BrowserAction.ContextMenu : BrowserAction.ContextMenu2;
                }
                var session = new ProfileSession(new MappingStore(Path.GetTempPath(),
                    Path.Combine(Path.GetTempPath(), "unused-menu-test.xml"), profile)) {
                    Mapping = mapping, Healthy = true
                };
                var runtime = new ApplicationMappings(delegate { }) { Enabled = true };
                runtime.Configure(new[] { session });
                target.Show();
                target.Activate();
                Native.SetForegroundWindow(target.Handle);
                Application.DoEvents();
                Assert(Native.GetForegroundWindow() == target.Handle, "Testziel erhaelt den Fokus");
                uint process;
                Native.GetWindowThreadProcessId(target.Handle, out process);
                runtime.SelectContext(target.Handle, process,
                    Path.GetFileNameWithoutExtension(profile.Executable), profile.WindowClass);
                if (withAxes)
                    foreach (string name in ApplicationMapping.AxisNames)
                        device.Axes.Add(new Axis { Name = name, Minimum = -350, Maximum = 350, ReportId = 1,
                            Value = name == "X" ? (long?)350 : null });
                var group = new ButtonGroup { Seen = true, ReportId = 2 };
                group.Available.Add(button);
                group.Pressed.Add(button);
                device.Buttons.Add(group);
                if (withAxes)
                {
                    device.LastReportId = 1;
                    device.Reports++;
                    runtime.Receive(device);
                    Assert(typeof(ApplicationMappings).GetField("activeMenu", flags).GetValue(runtime) == null,
                        "Achsenreport mit altem Tastenstatus oeffnet kein Menue");
                }
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    device.LastReportId = 2;
                    group.ValueReport = ++device.Reports;
                    var before = DateTime.UtcNow;
                    var position = Cursor.Position;
                    runtime.Receive(device);
                    var radial = (BridgeRadialMenu)typeof(ApplicationMappings).GetField("activeMenu", flags).GetValue(runtime);
                    Assert(radial != null && radial.Visible && (DateTime.UtcNow - before).TotalMilliseconds < 250,
                        profile.Name + ": Menue beim ersten Druck ohne Loslassen, Wartezeit oder Achsenwerte");
                    var view = (RadialMenuView)radial.Controls[0];
                    Assert(view.MenuRadius == (menuNumber == 1 ? 77 : 120) &&
                        view.Items[0].Shortcut.Key == (int)(menuNumber == 1 ? Keys.F8 : Keys.F9) &&
                        view.CenterPercent == (menuNumber == 1 ? 33 : 45) &&
                        view.BorderWidth == (menuNumber == 1 ? 1 : 2) &&
                        view.SegmentGap == (menuNumber == 1 ? 2 : 4) &&
                        view.TextSize == (menuNumber == 1 ? 6 : 8),
                        "Beide Menues auf beiden Tasten frei zuweisbar mit eigenen Eintraegen und Darstellung");
                    var area = Screen.FromPoint(position).WorkingArea;
                    var expected = new System.Drawing.Point(
                        Math.Max(area.Left, Math.Min(position.X - radial.Width / 2, area.Right - radial.Width)),
                        Math.Max(area.Top, Math.Min(position.Y - radial.Height / 2, area.Bottom - radial.Height)));
                    Assert(radial.StartPosition == FormStartPosition.Manual && radial.Location == expected,
                        "Angezeigtes Radialmenue bleibt am Mauszeiger, am Bildschirmrand begrenzt");
                    runtime.Receive(device);
                    Assert(ReferenceEquals(radial, typeof(ApplicationMappings).GetField("activeMenu", flags).GetValue(runtime)),
                        "Halten oeffnet kein zweites Menue");
                    if (attempt == 0)
                    {
                        // Isolate the debounce from time spent creating and painting the menu.
                        var clock = (System.Diagnostics.Stopwatch)typeof(ApplicationMappings).GetField("jogClock", flags).GetValue(runtime);
                        typeof(ApplicationMappings).GetField("menuOpenedAt", flags).SetValue(runtime, clock.Elapsed.TotalMilliseconds);
                        group.Pressed.Clear();
                        group.ValueReport = ++device.Reports;
                        runtime.Receive(device);
                        group.Pressed.Add(button);
                        group.ValueReport = ++device.Reports;
                        runtime.Receive(device);
                        Assert(radial.Visible, "Schneller Doppeldruck innerhalb 250 ms schliesst das Menue nicht");
                        System.Threading.Thread.Sleep(260);
                        group.ValueReport = ++device.Reports;
                        runtime.Receive(device);
                        Assert(radial.Visible, "Halten ueber die Sperrzeit hinaus schliesst das Menue nicht");
                        group.Pressed.Clear();
                        group.ValueReport = ++device.Reports;
                        runtime.Receive(device);
                        group.Pressed.Add(button);
                        group.ValueReport = ++device.Reports;
                        runtime.Receive(device);
                        Assert(!radial.Visible && radial.SelectedShortcut == null,
                            "Erneuter Druck nach 250 ms schliesst ohne Kuerzel");
                    }
                    else radial.Close();
                    var deadline = DateTime.UtcNow.AddSeconds(1);
                    while ((bool)typeof(ApplicationMappings).GetField("menuOpen", flags).GetValue(runtime) &&
                        DateTime.UtcNow < deadline)
                    {
                        Application.DoEvents();
                        System.Threading.Thread.Sleep(5);
                    }
                    Assert(!(bool)typeof(ApplicationMappings).GetField("menuOpen", flags).GetValue(runtime),
                        "Menueabbruch beendet Eingabepause");
                    group.ValueReport = ++device.Reports;
                    runtime.Receive(device);
                    Assert(typeof(ApplicationMappings).GetField("activeMenu", flags).GetValue(runtime) == null,
                        "Gehaltene Taste oeffnet nach Reset kein Menue erneut");
                    group.Pressed.Clear();
                    group.ValueReport = ++device.Reports;
                    runtime.Receive(device);
                    group.Pressed.Add(button);
                }
            }
        }

        private static void MenuDispatch()
        {
            foreach (string profileName in new[] { "Premiere", "Chrome" })
            foreach (int menuNumber in new[] { 1, 2 })
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
                var items = new[] { new PremiereMenuItem {
                    Name = "Isolierter Test", Shortcut = new PremiereShortcut { Key = (int)Keys.F8, Control = true }
                } };
                if (profileName == "Premiere") mapping.PremiereMenu = items;
                else mapping.ChromeMenu = items;
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
                    var radial = (BridgeRadialMenu)typeof(ApplicationMappings)
                        .GetField("activeMenu", flags).GetValue(runtime);
                    Assert(radial != null && radial.Visible,
                        profileName + "-Radialmenue wird nichtmodal angezeigt");
                    if (mode == "cancel") { radial.Close(); return; }
                    if (mode == "reset") runtime.Reset();
                    radial.ActivateSlot(0);
                };
                timer.Start();
                if (menuNumber == 1)
                    typeof(ApplicationMappings).GetMethod("OpenContextMenu", flags).Invoke(runtime,
                        new object[] { items, profileName });
                else
                    typeof(ApplicationMappings).GetMethod("OpenContextMenuCore", flags).Invoke(runtime,
                        new object[] { items, profileName + " Menue 2", new RadialMenuConfiguration { Items = items, Radius = 120 } });
                Assert((bool)typeof(ApplicationMappings).GetField("menuOpen", flags).GetValue(runtime),
                    "Radialmenue oeffnet ohne blockierenden modalen Aufruf");
                var deadline = DateTime.UtcNow.AddSeconds(1);
                while (DateTime.UtcNow < deadline && (mode == "select" && !released))
                {
                    Application.DoEvents();
                    System.Threading.Thread.Sleep(10);
                }
                deadline = DateTime.UtcNow.AddSeconds(1);
                while (DateTime.UtcNow < deadline &&
                    (bool)typeof(ApplicationMappings).GetField("menuOpen", flags).GetValue(runtime))
                {
                    Application.DoEvents();
                    System.Threading.Thread.Sleep(10);
                }
                Application.DoEvents();
                Assert(mode == "select" ? received == 1 && released : received == 0,
                    "Echter " + profileName + "-Menuepfad: Fokus-Rueckgabe, genau ein Kuerzel, Freigabe; Abbruch/Reset ohne Ausgabe (" +
                    mode + ", KeyDown=" + received + ", KeyUp=" + released + ", MenuOpen=" +
                    typeof(ApplicationMappings).GetField("menuOpen", flags).GetValue(runtime) +
                    ", Foreground=" + Native.GetForegroundWindow() + ", Target=" + target.Handle +
                    ", Log=" + string.Join(" | ", messages) + ")");
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
                    if ((function == PremiereFunction.ContextMenu || function == PremiereFunction.ContextMenu2) && !binding.Button) continue;
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
                        Assert(output.Length == (function == PremiereFunction.None || binding.Menu ? 0 : 1),
                            "Jede Ausgabefunktion auf jeder Eingabe; Menues laufen direkt ueber ApplicationMappings");
                        if (output.Length == 0) continue;
                        Assert(output[0].Steps == (reverse ? -1 : 1) && output[0].Inputs.Length > 0,
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
                    typeof(PremiereMappingEditor).GetField("menuRadius", BindingFlags.Instance | BindingFlags.NonPublic)
                        .SetValue(editor, 180);
                    typeof(PremiereMappingEditor).GetField("menuCenterPercent", BindingFlags.Instance | BindingFlags.NonPublic)
                        .SetValue(editor, 60);
                    typeof(PremiereMappingEditor).GetField("menuBorderWidth", BindingFlags.Instance | BindingFlags.NonPublic)
                        .SetValue(editor, 4m);
                    typeof(PremiereMappingEditor).GetField("menuSegmentGap", BindingFlags.Instance | BindingFlags.NonPublic)
                        .SetValue(editor, 6m);
                    typeof(PremiereMappingEditor).GetField("menuTextSize", BindingFlags.Instance | BindingFlags.NonPublic)
                        .SetValue(editor, 10m);
                    typeof(PremiereMappingEditor).GetField("menuIconSize", BindingFlags.Instance | BindingFlags.NonPublic)
                        .SetValue(editor, 30m);
                    using (var menuEditor = new PremiereMenuEditor(Enumerable.Range(0, 4).Select(i => new PremiereMenuItem {
                        Name = "Menue zwei " + i, Shortcut = new PremiereShortcut { Key = (int)Keys.F2 + i },
                        Display = MenuSegmentDisplay.Icon, Icon = MenuIcon.Render
                    }).ToArray(), "Premiere - Menue 2", 120, 40, 2, 3, 8, 18))
                        typeof(PremiereMappingEditor).GetField("menu2", BindingFlags.Instance | BindingFlags.NonPublic)
                            .SetValue(editor, menuEditor.CollectConfiguration());
                    Control<ComboBox>(editor, "function").SelectedIndex = (int)PremiereFunction.ContextMenu;
                    typeof(PremiereMappingEditor).GetField("menuItems", BindingFlags.Instance | BindingFlags.NonPublic)
                        .SetValue(editor, Enumerable.Range(0, 8).Select(i => new PremiereMenuItem {
                            Name = i == 0 ? "Eigener Befehl" : "Befehl " + i,
                            Display = i == 0 ? MenuSegmentDisplay.Icon : MenuSegmentDisplay.Text,
                            Icon = i == 0 ? MenuIcon.Razor : MenuIcon.None,
                            Shortcut = new PremiereShortcut { Key = (int)Keys.F7 + i % 6, Alt = true }
                        }).ToArray());
                    mapping = editor.Collect();
                    using (var image = new System.Drawing.Bitmap(editor.Width, editor.Height))
                        editor.DrawToBitmap(image, new System.Drawing.Rectangle(System.Drawing.Point.Empty, image.Size));
                }
                Assert(before.SequenceEqual(File.ReadAllBytes(store.SettingsPath)) &&
                    undo.SequenceEqual(File.ReadAllBytes(store.LastBackupPath)), "Abbrechen/Vorschau schreibt keine Dateien");
                store.Apply(mapping, "absent", MappingStore.Hash(before));
                var loaded = store.Load();
                Assert(loaded.MenuRadius == 180, "Premiere-Menue-Radius durch Editor und Speicherung erhalten");
                Assert(loaded.MenuCenterPercent == 60, "Premiere-Mittelkreis durch Editor und Speicherung erhalten");
                Assert(loaded.MenuBorderWidth == 4m, "Premiere-Konturlinie durch Editor und Speicherung erhalten");
                Assert(loaded.MenuSegmentGap == 6m, "Premiere-Segmentabstand gespeichert");
                Assert(loaded.MenuTextSize == 10m, "Premiere-Textgroesse gespeichert");
                Assert(loaded.MenuIconSize == 30 && loaded.Menu2.IconSize == 18,
                    "Unabhaengige Icongroessen beider Premiere-Menues gespeichert");
                Assert(loaded.PremiereMenu[0].Display == MenuSegmentDisplay.Icon && loaded.PremiereMenu[0].Icon == MenuIcon.Razor &&
                    loaded.PremiereMenu[1].Display == MenuSegmentDisplay.Text &&
                    loaded.Menu2.Items[0].Display == MenuSegmentDisplay.Icon && loaded.Menu2.Items[0].Icon == MenuIcon.Render,
                    "Text- und Iconsegmente durch beide Premiere-Menueeditoren persistent");
                Assert(loaded.Menu2.Items.Length == 4 && loaded.Menu2.Items[0].Name == "Menue zwei 0" &&
                    loaded.Menu2.Radius == 120 && loaded.Menu2.CenterPercent == 40 &&
                    loaded.Menu2.BorderWidth == 2 && loaded.Menu2.SegmentGap == 3 && loaded.Menu2.TextSize == 8,
                    "Zweites Premiere-Menue unabhaengig von Menue 1 durch beide Editoren gespeichert");
                Assert(loaded.PremiereMenu.Length == 8 && loaded.PremiereMenu[0].Name == "Eigener Befehl" &&
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
