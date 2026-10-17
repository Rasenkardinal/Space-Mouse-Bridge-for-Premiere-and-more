using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace SpaceMouseBridge
{
    internal static class JogTests
    {
        private static void Assert(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("Jog-Test fehlgeschlagen: " + name);
        }

        private static void Reject(Action action, string name)
        {
            bool rejected = false;
            try { action(); }
            catch (InvalidDataException) { rejected = true; }
            Assert(rejected, name);
        }

        internal static void Run()
        {
            ConfigurableBehavior();
            TimelineScrolling();
            var settings = JogSettings.Default();
            settings.FineControl = false;
            settings.MaximumRate = 24;
            var jog = new JogController();
            jog.Receive(1, 0, settings);
            Assert(jog.Step(0, settings) == 0, "Start ausgelenkt gesperrt");
            jog.Receive(0, 10, settings);
            jog.Receive(1, 20, settings);
            Assert(jog.Step(20, settings) == 1, "Rechts erster Schritt sofort");
            Assert(jog.Step(21, settings) == 0, "Kein mehrfacher Schritt im selben Intervall");
            Assert(jog.Step(61, settings) == 0 && jog.Step(62, settings) == 1, "Maximum 24 Schritte/s");
            jog.Receive(-1, 65, settings);
            Assert(jog.Step(65, settings) == -1, "Richtungswechsel sofort");
            jog.Receive(0.12, 70, settings);
            Assert(jog.Step(70, settings) == 0 && jog.Step(80, settings) == 0, "Neutralgrenze stoppt");
            jog.Receive(0.13, 90, settings);
            Assert(jog.Step(90, settings) == 1 && jog.Step(200, settings) == 0, "Kleine Auslenkung langsam");
            jog.Reset();
            jog.Receive(0, 0, settings);
            jog.Receive(1, 1, settings);
            Assert(jog.Step(1, settings) == 1, "Neu gestartet");
            Assert(jog.Step(251, settings) == 0 && jog.TakeTimeout(), "Bei 250 ms Datenstille sicher gestoppt");
            Assert(!jog.TakeTimeout(), "Timeout nur einmal gemeldet");
            jog.Receive(1, 260, settings);
            Assert(jog.Step(260, settings) == 0, "Nach Datenabbruch Neutral erforderlich");
            jog.Receive(0, 270, settings);
            jog.Receive(-1, 280, settings);
            Assert(jog.Step(280, settings) == -1, "Neutral schaltet wieder frei");
            jog.Reset();
            jog.Receive(0, 0, settings);
            jog.Receive(1, 1, settings);
            Assert(jog.Step(1, settings) == 1 && jog.Step(220, settings) == 1 &&
                jog.Step(220, settings) == 0, "Keine Aufholsalve nach Timerverzoegerung");
            jog.Reset();
            settings.Reverse = true;
            jog.Receive(0, 0, settings);
            jog.Receive(1, 1, settings);
            Assert(jog.Step(1, settings) == -1, "Richtung umkehrbar");
            jog.Reset();
            Assert(jog.Step(2, settings) == 0, "Reset stoppt");
            settings.Reverse = false;
            Assert(CountSteps(1, settings) == 24, "24 Schritte in einer Sekunde bei voller Auslenkung");
            int slow = CountSteps(0.25, settings);
            int medium = CountSteps(0.65, settings);
            Assert(slow >= 2 && slow < medium && medium < 24, "Messbar auslenkungsabhaengige Frequenz");
            settings.MaximumRate = 40;
            Assert(CountSteps(1, settings) == 40, "Konfiguriertes Maximum 40 Schritte/s");
            settings.MaximumRate = 120;
            jog.Reset();
            jog.Receive(0, 0, settings);
            jog.Receive(0.12001, 0, settings);
            Assert(jog.Step(0, settings) == 1, "Langsamer Start");
            jog.Receive(1, 25, settings);
            jog.Step(25, settings);
            Assert(jog.Step(50, settings) >= 1, "Beschleunigung wartet nicht auf altes langsames Intervall");
            for (int quarter = 0; quarter <= 4; quarter++)
            {
                double displacement = 0.12 + 0.88 * quarter / 4;
                if (quarter == 0) displacement += 0.00001;
                double expectedRate = 2 + 118 * quarter / 4.0;
                int actual = CountTimerSteps(displacement, settings);
                Assert(Math.Abs(actual - expectedRate * 10) <= 3,
                    "Lineare Frequenz bei " + quarter + "/4 Arbeitsbereich mit echtem 25-ms-Takt");
                Assert(CountTimerSteps(-displacement, settings) == actual, "Beide Richtungen gleich schnell");
            }
            settings.FineControl = true;
            for (int quarter = 0; quarter <= 4; quarter++)
            {
                double fraction = quarter / 4.0;
                double displacement = 0.12 + 0.88 * fraction;
                if (quarter == 0) displacement += 0.00001;
                double expectedRate = 0.5 + 119.5 * fraction * fraction;
                int actual = CountTimerSteps(displacement, settings);
                Assert(Math.Abs(actual - expectedRate * 10) <= 3,
                    "Feinregelung bei " + quarter + "/4 Arbeitsbereich mit 25-ms-Takt");
                Assert(CountTimerSteps(-displacement, settings) == actual, "Feinregelung beidseitig gleich");
            }
            jog.Reset();
            jog.Receive(0, 0, settings);
            for (int ms = 0; ms <= 2100; ms += 25)
            {
                jog.Receive(0.12001, ms, settings);
                int steps = jog.Step(ms, settings);
                Assert(steps == (ms == 0 || ms == 2000 ? 1 : 0),
                    "Ganz leichtes Drehen: sofort ein Bild, danach erst alle zwei Sekunden");
            }
            var batch = JogController.Inputs(1, 6);
            Assert(batch.Length == 12 && batch.Where((k, i) => k.Key != 0x27 ||
                k.Flags != (i % 2 == 0 ? 1u : 3u)).Count() == 0, "Batch aus sechs vollstaendigen Einzelschritten");
            for (uint sent = 0; sent <= batch.Length; sent++)
                Assert(BrowserCommand.ReleasePartial(batch, sent).Length == sent % 2,
                    "Batch-Teilversand gibt nur noch gehaltene Taste frei");
            var left = JogController.Inputs(-1);
            var right = JogController.Inputs(1);
            Assert(left.Length == 2 && left[0].Key == 0x25 && left[0].Flags == 1 &&
                left[1].Key == 0x25 && left[1].Flags == 3, "Links vollstaendiges erweitertes KeyDown/KeyUp");
            Assert(right.Length == 2 && right[0].Key == 0x27 && right[1].Key == 0x27 &&
                right.All(k => k.Type == 1), "Rechts nur Pfeiltaste, keine Modifier oder Wiedergabe");
            Assert(BrowserCommand.ReleasePartial(right, 1).Single().Key == 0x27, "Teilfehler gibt eigene Pfeiltaste frei");

            string temporary = Path.Combine(Path.GetTempPath(), "SpaceMouseBridge-JogTests-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new MappingStore(temporary, null, ApplicationProfile.Premiere);
                var empty = store.Load();
                store.Apply(empty, "absent", "absent");
                byte[] before = File.ReadAllBytes(store.SettingsPath);
                var mapping = empty.Clone();
                mapping.Enabled = true;
                mapping.Jog = JogSettings.Default();
                using (var editor = new PremiereEditor(store, mapping))
                {
                    var result = editor.Collect();
                    Assert(result.Enabled && result.Jog.Deadzone == 12 && result.Jog.MaximumRate == 120 &&
                        !result.Jog.Reverse && result.Jog.FineControl, "Editor uebernimmt Jog exakt");
                }
                store.Apply(mapping, "absent", MappingStore.Hash(before));
                var loaded = store.Load();
                Assert(loaded.Enabled && loaded.Jog.MaximumRate == 120 && loaded.Jog.Deadzone == 12 && loaded.Jog.FineControl,
                    "Aktives Jog-Profil persistent");
                var session = new ProfileSession(store);
                session.Load();
                var router = new ProfileRouter();
                router.Configure(new[] { session });
                router.Select(new IntPtr(1), 1, "Adobe Premiere Pro", "Premiere");
                Assert(router.CanExecute && router.ActiveMapping.Jog != null, "Jog nur im Premiere-Kontext verfuegbar");
                router.Select(new IntPtr(2), 2, "chrome", "Chrome_WidgetWin_1");
                Assert(!router.CanExecute, "Jog-Profil nicht in Chrome aktiv");
                store.Restore();
                Assert(before.SequenceEqual(File.ReadAllBytes(store.SettingsPath)) && !store.Load().Enabled &&
                    store.Load().Jog == null, "Ruecknahme stellt inaktives Leerprofil bytegenau wieder her");
                ConfigurablePersistence(store);
            }
            finally { if (Directory.Exists(temporary)) Directory.Delete(temporary, true); }
        }

        private static void ConfigurableBehavior()
        {
            var json = new JavaScriptSerializer();
            foreach (bool fine in new[] { false, true })
            {
                var legacy = json.Deserialize<JogSettings>("{\"Deadzone\":12,\"MaximumRate\":120,\"FineControl\":" +
                    (fine ? "true" : "false") + ",\"Reverse\":true}");
                legacy.Validate();
                Assert(legacy.EffectiveMinimumRate == (fine ? 0.5m : 2m) &&
                    legacy.EffectiveExponent == (fine ? 2m : 1m) &&
                    legacy.RepeatDelayMs == 0 && legacy.Behavior == JogBehavior.Continuous && legacy.Reverse,
                    "Altes JSON behaelt Kennlinie, Verhalten und Richtung");
            }
            var settings = JogSettings.Default();
            settings.MinimumRate = 0.1m;
            settings.CurveExponent = 3m;
            settings.Validate();
            Assert(settings.Rate(0.12) == 0 && settings.Rate(0) == 0 &&
                settings.Rate(1) == 120 && settings.Rate(-1) == 120 && settings.Rate(2) == 120,
                "Gemeinsame Vorschauformel: Grenzen und Begrenzung");
            for (int quarter = 0; quarter <= 4; quarter++)
            {
                double fraction = quarter / 4.0;
                double value = 0.12 + 0.88 * fraction + (quarter == 0 ? 0.000001 : 0);
                double expected = 0.1 + 119.9 * fraction * fraction * fraction;
                Assert(Math.Abs(settings.Rate(value) - expected) < 0.0001, "Kubische Vorschau exakt");
                Assert(Math.Abs(CountTimerSteps(value, settings) - expected * 10) <= 3,
                    "Kubische Vorschau entspricht echten Schritten");
            }
            settings.MinimumRate = 120;
            Assert(CountTimerSteps(0.13, settings) >= 1197, "Konstante Rate bei Minimum gleich Maximum");
            settings.MinimumRate = 121;
            Reject(settings.Validate, "Minimum ueber Maximum ungueltig");
            settings.MinimumRate = 0;
            Reject(settings.Validate, "Minimum null ungueltig");
            settings.MinimumRate = 0.5m;
            settings.CurveExponent = 0;
            Reject(settings.Validate, "Exponent null ungueltig");
            settings.CurveExponent = 6;
            Reject(settings.Validate, "Exponent zu hoch ungueltig");
            settings.CurveExponent = 2;
            settings.RepeatDelayMs = -1;
            Reject(settings.Validate, "Negative Wartezeit ungueltig");
            settings.RepeatDelayMs = 3001;
            Reject(settings.Validate, "Zu lange Wartezeit ungueltig");
            settings.RepeatDelayMs = 500;
            settings.Behavior = (JogBehavior)99;
            Reject(settings.Validate, "Unbekanntes Verhalten ungueltig");
            settings.Behavior = JogBehavior.Continuous;
            var jog = new JogController();
            jog.Receive(0, 0, settings);
            for (int ms = 0; ms <= 525; ms += 25)
            {
                jog.Receive(1, ms, settings);
                int steps = jog.Step(ms, settings);
                Assert(ms == 0 || ms == 500 ? steps == 1 : ms < 500 ? steps == 0 : steps == 3,
                    "Erstschritt sofort, Wiederholung erst nach 500 ms ohne Aufholsalve");
            }
            jog.Receive(-1, 550, settings);
            Assert(jog.Step(550, settings) == -1, "Richtungswechsel sofort trotz Wiederholungsverzoegerung");
            jog.Receive(-1, 650, settings);
            Assert(jog.Step(650, settings) == 0, "Richtungswechsel startet Wartezeit neu");
            jog.Receive(0, 700, settings);
            Assert(jog.Step(1050, settings) == 0, "Neutral waehrend Wartezeit stoppt");
            jog.Receive(-1, 1060, settings);
            Assert(jog.Step(1060, settings) == 0, "Timeout verlangt Neutral, auch nach Wartezeit");
            jog.Receive(0, 1070, settings);
            jog.Receive(-1, 1080, settings);
            Assert(jog.Step(1080, settings) == -1, "Neue Neutralfreigabe startet wieder");
            jog.Reset();
            jog.Receive(1, 1090, settings);
            Assert(jog.Step(1090, settings) == 0, "Reset verwirft laufende Wartezeit und Freigabe");

            jog.Reset();
            jog.Receive(0, 0, settings);
            for (int ms = 0; ms <= 2000; ms += 25)
            {
                jog.Receive(0.120001, ms, settings);
                Assert(jog.Step(ms, settings) == (ms == 0 || ms == 2000 ? 1 : 0),
                    "Wartezeit verkuerzt nie das natuerliche langsame Schrittintervall");
            }
            settings.RepeatDelayMs = 0;
            settings.Behavior = JogBehavior.SingleFrameRegion;
            settings.SingleFrameLimit = 30;
            settings.Validate();
            Assert(settings.IsSingleFrame(0.3) && !settings.IsSingleFrame(0.12) &&
                !settings.IsSingleFrame(0.31) && settings.Rate(0.3) == 0 &&
                Math.Abs(settings.Rate(0.65) - 30.375) < 0.00001,
                "Einzelbildgrenze und verschobene Vorschaukurve");
            jog.Reset();
            jog.Receive(0, 0, settings);
            for (int ms = 0; ms <= 2000; ms += 25)
            {
                jog.Receive(0.3, ms, settings);
                Assert(jog.Step(ms, settings) == (ms == 0 ? 1 : 0), "Einzelbildbereich wiederholt nie beim Halten");
            }
            jog.Receive(1, 2025, settings);
            Assert(jog.Step(2025, settings) == 1, "Ueber Einzelbildgrenze beginnt kontinuierliches Jog sofort");
            jog.Receive(1, 2050, settings);
            Assert(jog.Step(2050, settings) == 3, "Kontinuierlicher Bereich erreicht Maximum");
            jog.Receive(0.2, 2075, settings);
            Assert(jog.Step(2075, settings) == 0, "Zurueck in Einzelbildbereich keine zusaetzlichen Schritte");
            jog.Receive(-0.2, 2100, settings);
            Assert(jog.Step(2100, settings) == -1, "Einzelbild beim Richtungswechsel");
            jog.Receive(0, 2125, settings);
            jog.Receive(-0.2, 2150, settings);
            Assert(jog.Step(2150, settings) == -1, "Neutral ermoeglicht naechstes Einzelbild");
            settings.Reverse = true;
            jog.Reset();
            jog.Receive(0, 0, settings);
            jog.Receive(0.2, 1, settings);
            Assert(jog.Step(1, settings) == -1, "Einzelbild umkehrbar");
            settings.SingleFrameLimit = 12;
            Reject(settings.Validate, "Einzelbildgrenze muss ueber Totzone liegen");
            settings.SingleFrameLimit = 96;
            Reject(settings.Validate, "Einzelbildgrenze unter Anschlag");
            settings.SingleFrameLimit = 0;
            settings.Deadzone = 40;
            settings.Validate();
            Assert(settings.EffectiveSingleFrameLimit == 41, "Implizite Bereichsgrenze passt oberhalb alter Totzone");
            settings.Deadzone = 12;
            settings.SingleFrameLimit = 30;
            settings.Reverse = false;
            settings.RepeatDelayMs = 500;
            jog.Reset();
            jog.Receive(0, 0, settings);
            jog.Receive(0.2, 0, settings);
            Assert(jog.Step(0, settings) == 1, "Einzelbild vor Wartezeit");
            jog.Receive(1, 100, settings);
            Assert(jog.Step(100, settings) == 0, "Wechsel in kontinuierlichen Bereich umgeht Wartezeit nicht");
            for (int ms = 125; ms <= 500; ms += 25)
            {
                jog.Receive(1, ms, settings);
                Assert(jog.Step(ms, settings) == (ms == 500 ? 1 : 0),
                    "Wartezeit gilt seit erstem Einzelbild ohne Aufholschritte");
            }
        }

        private static T EditorControl<T>(PremiereEditor editor, string name) where T : Control
        {
            return (T)typeof(PremiereEditor).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor);
        }

        private static void ConfigurablePersistence(MappingStore store)
        {
            var mapping = store.Load();
            mapping.Enabled = true;
            mapping.Jog = JogSettings.Default();
            store.Apply(mapping, "absent", MappingStore.Hash(File.ReadAllBytes(store.SettingsPath)));
            byte[] before = File.ReadAllBytes(store.SettingsPath);
            byte[] undoBefore = File.ReadAllBytes(store.LastBackupPath);
            using (var editor = new PremiereEditor(store, mapping))
            {
                var unchanged = editor.Collect().Jog;
                Assert(unchanged.EffectiveMinimumRate == 0.5m && unchanged.EffectiveExponent == 2 &&
                    unchanged.RepeatDelayMs == 0 && unchanged.Behavior == JogBehavior.Continuous &&
                    unchanged.Deadzone == 12 && unchanged.MaximumRate == 120, "Editor erhaelt derzeitiges Verhalten ohne neues Speichern");
                EditorControl<NumericUpDown>(editor, "minimumRate").Value = 0.25m;
                EditorControl<NumericUpDown>(editor, "exponent").Value = 3;
                EditorControl<NumericUpDown>(editor, "repeatDelay").Value = 600;
                EditorControl<ComboBox>(editor, "behavior").SelectedIndex = 1;
                Assert(EditorControl<NumericUpDown>(editor, "singleFrameLimit").Enabled, "Bereichsgrenze nur im Einzelbildmodus editierbar");
                EditorControl<NumericUpDown>(editor, "singleFrameLimit").Value = 35;
                EditorControl<CheckBox>(editor, "scrollEnabled").Checked = true;
                EditorControl<CheckBox>(editor, "scrollReverse").Checked = true;
                EditorControl<NumericUpDown>(editor, "scrollRate").Value = 32;
                EditorControl<NumericUpDown>(editor, "scrollEndRate").Value = 96;
                mapping = editor.Collect();
                Assert(mapping.TimelineScroll.Enabled && mapping.TimelineScroll.Reverse &&
                    mapping.TimelineScroll.MaximumRate == 32 && mapping.TimelineScroll.EndRate == 96,
                    "Timeline-UI uebernimmt Aktivierung, Richtung, Grund- und Endtempo");
                Assert(mapping.Jog.MinimumRate == 0.25m && mapping.Jog.CurveExponent == 3 &&
                    mapping.Jog.RepeatDelayMs == 600 && mapping.Jog.Behavior == JogBehavior.SingleFrameRegion &&
                    mapping.Jog.SingleFrameLimit == 35, "Alle UI-Werte werden uebernommen");
                using (var bitmap = new System.Drawing.Bitmap(600, 240))
                {
                    PremiereEditor.JogCurvePreview preview = EditorControl<PremiereEditor.JogCurvePreview>(editor, "preview");
                    preview.Size = bitmap.Size;
                    preview.DrawToBitmap(bitmap, new System.Drawing.Rectangle(0, 0, 600, 240));
                }
                EditorControl<NumericUpDown>(editor, "singleFrameLimit").Value = 6;
                Assert(EditorControl<Label>(editor, "feedback").Text.Length > 0, "Ungueltige Bereichsgrenze wird sichtbar gemeldet");
                Reject(delegate { editor.Collect(); }, "Ungueltige Editor-Werte nicht gespeichert");
            }
            Assert(before.SequenceEqual(File.ReadAllBytes(store.SettingsPath)) &&
                undoBefore.SequenceEqual(File.ReadAllBytes(store.LastBackupPath)), "Editor-Vorschau und Abbrechen schreiben keine Dateien");
            store.Apply(mapping, "absent", MappingStore.Hash(before));
            var loaded = store.Load();
            Assert(loaded.Jog.MinimumRate == 0.25m && loaded.Jog.CurveExponent == 3 &&
                loaded.Jog.RepeatDelayMs == 600 && loaded.Jog.Behavior == JogBehavior.SingleFrameRegion &&
                loaded.Jog.SingleFrameLimit == 35, "Alle neuen Einstellungen persistent");
            Assert(loaded.TimelineScroll.Enabled && loaded.TimelineScroll.Reverse &&
                loaded.TimelineScroll.MaximumRate == 32 && loaded.TimelineScroll.EndRate == 96, "Timeline-Einstellungen persistent");
            store.Restore();
            Assert(before.SequenceEqual(File.ReadAllBytes(store.SettingsPath)), "Ruecknahme behaelt bisherige Kennlinie bytegenau");
        }

        private static void TimelineScrolling()
        {
            var settings = TimelineScrollSettings.Default();
            settings.Validate();
            Assert(!settings.Enabled && settings.MaximumRate == 24 && !settings.Reverse,
                "Timeline-Scrollen nur explizit aktiviert");
            var timing = settings.Timing();
            timing.Validate();
            Assert(timing.Deadzone == 12 && timing.EffectiveExponent == 2 && timing.EffectiveMinimumRate == 0.5m &&
                timing.Behavior == JogBehavior.Continuous && timing.RepeatDelayMs == 0, "Eigenstaendige Scroll-Kennlinie");
            Assert(CountTimerSteps(1, timing) == 240 && CountTimerSteps(-1, timing) == 240,
                "Scroll-Maximum messbar 24 Impulse/s in beiden Richtungen");
            Assert(settings.EffectiveEndRate == 24 && settings.Rate(1) == 24, "Ohne Endtempo alte Scrollkurve erhalten");
            settings.EndRate = 96;
            settings.Validate();
            for (int percent = 0; percent <= 60; percent++)
                Assert(settings.Rate(percent / 100.0) == timing.Rate(percent / 100.0),
                    "Scrollkurve bis einschliesslich 60 Prozent exakt unveraendert");
            foreach (double value in new[] { 0.600001, 0.75, 0.85, 1.0 })
            {
                double extra = (value - 0.6) / 0.4;
                double expected = timing.Rate(value) + 72 * extra * extra;
                Assert(Math.Abs(settings.Rate(value) - expected) < 0.000001 &&
                    Math.Abs(CountTimerSteps(value, timing, settings.Rate) - expected * 10) <= 3,
                    "Zusatzbeschleunigung im Endbereich entspricht echten 25-ms-Takten");
                Assert(CountTimerSteps(-value, timing, settings.Rate) == CountTimerSteps(value, timing, settings.Rate),
                    "Endbeschleunigung in beiden Richtungen symmetrisch");
            }
            Assert(settings.Rate(1) == 96 && settings.Rate(2) == 96 &&
                Math.Abs(CountTimerSteps(1, timing, settings.Rate) - 960) <= 3,
                "Vierfaches Endtempo mit maximal drei Impulsen Takttoleranz ueber zehn Sekunden");
            settings.EndRate = 23;
            Reject(settings.Validate, "Endtempo unter Grundtempo ungueltig");
            settings.EndRate = 121;
            Reject(settings.Validate, "Endtempo ueber 120 ungueltig");
            settings.EndRate = null;
            settings.MaximumRate = 1;
            Reject(settings.Validate, "Scroll-Maximum zu klein");
            settings.MaximumRate = 121;
            Reject(settings.Validate, "Scroll-Maximum zu gross");
            settings.MaximumRate = 24;
            settings.Reverse = true;
            var scroll = new JogController();
            scroll.Receive(0, 0, settings.Timing());
            scroll.Receive(1, 1, settings.Timing());
            Assert(scroll.Step(1, settings.Timing()) == -1, "Scroll-Richtung umkehrbar");
            foreach (int steps in new[] { -6, -1, 1, 6 })
            {
                var inputs = TimelineScrollSettings.Inputs(steps);
                Assert(inputs.Length == 1 && inputs[0].Type == 0 && inputs[0].MouseFlags == 0x800 &&
                    inputs[0].MouseData == -120 * steps && inputs[0].Key == 0 && inputs[0].Flags == 0,
                    "Nur vertikale Mausradimpulse fuer Premiere-Horizontalpraeferenz, keine Tasten");
                Assert(BrowserCommand.ReleasePartial(inputs, 1).Length == 0, "Mausimpuls benoetigt kein KeyUp bei Teilfehler");
                IntPtr buffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(40);
                try
                {
                    System.Runtime.InteropServices.Marshal.StructureToPtr(inputs[0], buffer, false);
                    Assert(System.Runtime.InteropServices.Marshal.ReadInt32(buffer, 16) == -120 * steps &&
                        System.Runtime.InteropServices.Marshal.ReadInt32(buffer, 20) == 0x800 &&
                        System.Runtime.InteropServices.Marshal.ReadInt32(buffer, 24) == 0,
                        "MOUSEINPUT x64 Daten- und Flag-Offsets");
                }
                finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer); }
            }
            var legacy = new JavaScriptSerializer().Deserialize<ApplicationMapping>(
                new JavaScriptSerializer().Serialize(ApplicationProfile.Premiere.EmptyMapping()));
            Assert(legacy.TimelineScroll == null, "Alte Profile scrollen nicht implizit");
            var chrome = ApplicationMapping.Default();
            chrome.TimelineScroll = TimelineScrollSettings.Default();
            Reject(delegate { ApplicationProfile.Chrome.Validate(chrome); }, "Scrollen nicht in Chrome aktivierbar");

            using (var device = new Device { LastReportId = 1, Reports = 1 })
            {
                var x = new Axis { Name = "X", Minimum = -350, Maximum = 350, ReportId = 1, ValueReport = 1, Value = 0 };
                var rz = new Axis { Name = "Rz", Minimum = -350, Maximum = 350, ReportId = 2, ValueReport = 0, Value = 0 };
                device.Axes.Add(x);
                device.Axes.Add(rz);
                scroll.Reset();
                var jog = new JogController();
                ApplicationMappings.ReceiveAxis(device, "X", scroll, timing, 0);
                device.Reports = 2;
                x.ValueReport = 2;
                x.Value = 350;
                ApplicationMappings.ReceiveAxis(device, "X", scroll, timing, 1);
                Assert(scroll.Step(1, timing) == 1 && jog.Step(1, timing) == 0, "X arbeitet ohne Rz-Jog");
                device.LastReportId = 2;
                device.Reports = 3;
                rz.ValueReport = 3;
                ApplicationMappings.ReceiveAxis(device, "Rz", jog, timing, 10);
                rz.Value = -350;
                device.Reports = 4;
                rz.ValueReport = 4;
                ApplicationMappings.ReceiveAxis(device, "Rz", jog, timing, 50);
                ApplicationMappings.ReceiveAxis(device, "X", scroll, timing, 50);
                Assert(jog.Step(50, timing) == -1 && scroll.Step(50, timing) == 1,
                    "X und Rz unabhaengig gleichzeitig aktiv");
                ApplicationMappings.ReceiveAxis(device, "X", scroll, timing, 200);
                Assert(scroll.Step(251, timing) == 0 && scroll.TakeTimeout(), "Rz-Report frischt X-Lease nicht auf");
                device.LastReportId = 1;
                device.Reports = 5;
                ApplicationMappings.ReceiveAxis(device, "X", scroll, timing, 260);
                Assert(scroll.Step(260, timing) == 0, "Nicht erfolgreich dekodierte X-Werte ignoriert");
                x.ValueReport = 5;
                ApplicationMappings.ReceiveAxis(device, "X", scroll, timing, 270);
                Assert(scroll.Step(270, timing) == 0, "Nach Timeout Neutral erforderlich");
                x.Value = 0;
                ApplicationMappings.ReceiveAxis(device, "X", scroll, timing, 280);
                x.Value = -350;
                ApplicationMappings.ReceiveAxis(device, "X", scroll, timing, 290);
                Assert(scroll.Step(290, timing) == -1, "Neutral schaltet X nach Timeout frei");
                scroll.Reset();
                ApplicationMappings.ReceiveAxis(device, "X", scroll, timing, 300);
                Assert(scroll.Step(300, timing) == 0, "Reset bei Fokus/Pause verlangt Neutral");
            }
        }

        private static int CountSteps(double displacement, JogSettings settings)
        {
            var jog = new JogController();
            jog.Receive(0, 0, settings);
            int count = 0;
            for (int ms = 0; ms < 1000; ms++)
            {
                if (ms % 10 == 0) jog.Receive(displacement, ms, settings);
                count += Math.Abs(jog.Step(ms, settings));
            }
            return count;
        }

        private static int CountTimerSteps(double displacement, JogSettings settings, Func<double, double> rate = null)
        {
            var jog = new JogController();
            jog.Receive(0, 0, settings);
            int count = 0;
            for (int ms = 0; ms < 10000; ms += 25)
            {
                jog.Receive(displacement, ms, settings);
                int steps = jog.Step(ms, settings, rate);
                Assert(Math.Abs(steps) <= 6, "Begrenzte Schrittzahl pro Takt");
                count += Math.Abs(steps);
            }
            jog.Receive(0, 10000, settings);
            Assert(jog.Step(10000, settings) == 0, "Neutral stoppt auch bei hoher Frequenz");
            return count;
        }
    }
}
