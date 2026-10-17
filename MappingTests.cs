using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.Xml;

namespace SpaceMouseBridge
{
    internal static class MappingTests
    {
        private static void Assert(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("Mapping-Test fehlgeschlagen: " + name);
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
            var mapping = ApplicationMapping.Default();
            mapping.Validate();
            mapping.Axes.Single(a => a.Axis == "Rx").Mode = AxisMode.Scroll;
            mapping.Axes.Single(a => a.Axis == "Rx").Reverse = true;
            var zoom = mapping.Axes.Single(a => a.Axis == "Z");
            zoom.Mode = AxisMode.Zoom;
            zoom.Scale = 0.50m;
            zoom.Deadband = 35;
            var rotation = mapping.Axes.Single(a => a.Axis == "Rz");
            rotation.Mode = AxisMode.Bridge;
            rotation.Negative = BrowserAction.Back;
            rotation.Positive = BrowserAction.Forward;
            byte[] initial = DriverProfile.Merge(null, mapping);
            var imported = DriverProfile.Import(initial);
            Assert(imported.Axes.Single(a => a.Axis == "Rx").Reverse, "Scrollrichtung importiert");
            Assert(imported.Axes.Single(a => a.Axis == "Z").Scale == 0.50m, "Zoomverstaerkung importiert");
            Assert(imported.Axes.Single(a => a.Axis == "Z").Deadband == 35, "Zoomtotzone importiert");
            Assert(imported.Axes.Single(a => a.Axis == "Rz").Mode == AxisMode.Disabled, "Bridge-Achse im Treiber deaktiviert");
            Assert(MappingStore.Hash(initial) == MappingStore.Hash(DriverProfile.Merge(initial, mapping)), "Idempotentes XML-Merge");

            XmlDocument extra = DriverProfile.Parse(initial);
            XmlElement marker = extra.CreateElement("CustomKeep");
            marker.InnerText = "preserve me";
            extra.DocumentElement.AppendChild(marker);
            byte[] withExtra = Encoding.UTF8.GetBytes(extra.OuterXml);
            var merged = DriverProfile.Parse(DriverProfile.Merge(withExtra, mapping));
            Assert(merged.DocumentElement["CustomKeep"].InnerText == "preserve me", "Fremde XML-Daten erhalten");
            mapping.Buttons[0].Mode = ButtonMode.Bridge;
            mapping.Buttons[0].Action = BrowserAction.NextTab;
            merged = DriverProfile.Parse(DriverProfile.Merge(withExtra, mapping));
            Assert(merged.SelectSingleNode("//Button[Input/ActionID='V3DK_1']/Output/ActionID").InnerText == "",
                "Treiber-Taste unterdrueckt");
            Assert(merged.SelectSingleNode("//Button[Input/ActionID='HIDButton_1']/Output/ActionID").InnerText == "",
                "HID-Tastenalias unterdrueckt");
            Assert(merged.SelectNodes("//Button[Input/ActionID='V3DK_2']").Count == 0, "Unbelegte Taste unangetastet");
            var invalid = mapping.Clone();
            invalid.Neutral = invalid.Threshold;
            Reject(invalid.Validate, "Ungueltige Schwellen abgewiesen");
            invalid = mapping.Clone();
            invalid.Axes[0].Axis = "Rz";
            Reject(invalid.Validate, "Doppelte Achse abgewiesen");
            invalid = mapping.Clone();
            invalid.Buttons[0].Action = (BrowserAction)999;
            Reject(invalid.Validate, "Unbekannte Aktion abgewiesen");
            Reject(delegate { DriverProfile.Parse(Encoding.UTF8.GetBytes("<AppCfg Default='true'/>")); }, "Kein Herstellerprofil ueberschreiben");
            string conflictXml = Encoding.UTF8.GetString(initial).Replace("</Devices>",
                "<Device><ID>ID_ProductID_C62E</ID><AxisBank><ID>Default</ID><Axis><Input><ActionID>HIDMultiAxis_Z</ActionID></Input></Axis></AxisBank></Device></Devices>");
            Reject(delegate { DriverProfile.Merge(Encoding.UTF8.GetBytes(conflictXml), mapping); }, "Geraetespezifischer Konflikt");
            conflictXml = Encoding.UTF8.GetString(initial).Replace("</AxisBank>",
                "<Axis><Input><ActionID>HIDMultiAxis_Z</ActionID></Input></Axis></AxisBank>");
            Reject(delegate { DriverProfile.Merge(Encoding.UTF8.GetBytes(conflictXml), mapping); }, "Doppelte Zuordnung");

            var gesture = new RotationGesture { Threshold = 0.6, Neutral = 0.2 };
            gesture.Update(0.2);
            Assert(gesture.Update(0.59) == 0 && gesture.Update(-0.6) == -1, "Konfigurierbare Schwellen");
            var button = new ButtonGesture();
            Assert(!button.Update(true) && !button.Update(false) && !button.Update(false), "Keine Freigabe fremden Tastendrucks");
            Assert(!button.Update(true) && !button.Update(true) && button.Update(false) && !button.Update(false), "Taste nur beim Loslassen");
            foreach (BrowserAction action in Enum.GetValues(typeof(BrowserAction)))
            {
                var inputs = BrowserCommand.Inputs(action);
                Assert(inputs.Length == (action == BrowserAction.None ? 0 : action == BrowserAction.Reload ? 2 : 4),
                    "Paketform " + action);
                for (uint sent = 0; sent <= inputs.Length; sent++)
                {
                    var release = BrowserCommand.ReleasePartial(inputs, sent);
                    Assert(release.All(k => (k.Flags & 2) != 0), "Fehlerfreigabe nur KeyUp");
                    if (sent == inputs.Length) Assert(release.Length == 0, "Vollstaendige Eingabe braucht keine Freigabe");
                }
            }
            Assert(BrowserCommand.Inputs(BrowserAction.Back)[1].Key == 0x25, "Zurueck-Taste");
            Assert(BrowserCommand.Inputs(BrowserAction.Forward)[1].Key == 0x27, "Vorwaerts-Taste");
            Assert(BrowserCommand.Inputs(BrowserAction.PreviousTab)[1].Key == 0x21, "Vorheriger Tab");
            Assert(BrowserCommand.Inputs(BrowserAction.NextTab)[1].Key == 0x22, "Naechster Tab");
            Assert(BrowserCommand.Inputs(BrowserAction.NextTab)[0].Key == 0x11, "Tabs benutzen Strg");
            Assert(BrowserCommand.Inputs(BrowserAction.Back)[0].Key == 0x12, "Verlauf benutzt Alt");

            string temporary = Path.Combine(Path.GetTempPath(), "SpaceMouseBridge-Tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporary);
            try
            {
                string driverPath = Path.Combine(temporary, "driver", "Chrome.xml");
                string root = Path.Combine(temporary, "settings");
                var store = new MappingStore(root, driverPath);
                using (var editor = new MappingEditor(store, mapping))
                {
                    var roundTrip = editor.Collect();
                    Assert(roundTrip.Enabled == mapping.Enabled && roundTrip.Neutral == mapping.Neutral &&
                        roundTrip.Threshold == mapping.Threshold, "Editor erhaelt Profilschalter und Schwellen");
                    foreach (AxisMapping expected in mapping.Axes)
                    {
                        AxisMapping actual = roundTrip.Axes.Single(a => a.Axis == expected.Axis);
                        Assert(actual.Mode == expected.Mode && actual.Scale == expected.Scale &&
                            actual.Deadband == expected.Deadband && actual.Reverse == expected.Reverse &&
                            actual.Positive == expected.Positive && actual.Negative == expected.Negative,
                            "Editor erhaelt Achse " + expected.Axis);
                    }
                    Assert(roundTrip.Buttons[0].Mode == ButtonMode.Bridge &&
                        roundTrip.Buttons[0].Action == BrowserAction.NextTab, "Editor erhaelt Tastenbelegung");
                }
                string backup = store.Apply(mapping, "absent", "absent");
                Assert(Directory.Exists(backup), "Sicherung erstellt");
                ApplicationMapping loaded = store.Load();
                Assert(loaded.Axes.Single(a => a.Axis == "Rz").Positive == BrowserAction.Forward, "Bridge-Aktion persistent");
                Assert(loaded.Buttons[0].Action == BrowserAction.NextTab, "Tastenaktion persistent");
                Assert(store.Apply(loaded, MappingStore.Hash(MappingStore.Read(driverPath)),
                    MappingStore.Hash(MappingStore.Read(store.SettingsPath))) == null, "Wiederholtes Uebernehmen aendert nichts");
                Reject(delegate { store.Apply(mapping, "wrong", "absent"); }, "Externe Aenderung beim Speichern erkannt");
                byte[] savedDriver = File.ReadAllBytes(driverPath);
                File.AppendAllText(driverPath, "\r\n");
                Reject(delegate { store.Load(); }, "Externe XML-Aenderung beim Laden erkannt");
                Reject(store.Restore, "Externe Aenderung verhindert Ruecknahme");
                File.WriteAllBytes(driverPath, savedDriver);
                store.Restore();
                Assert(!File.Exists(driverPath) && !File.Exists(store.SettingsPath), "Vorherige Abwesenheit wiederhergestellt");
                Assert(!File.Exists(store.LastBackupPath), "Ruecknahme nur einmal");

                MappingStore.AtomicWrite(driverPath, withExtra);
                backup = store.Apply(mapping, MappingStore.Hash(withExtra), "absent");
                byte[] firstSettings = File.ReadAllBytes(store.SettingsPath);
                byte[] firstDriver = File.ReadAllBytes(driverPath);
                loaded = store.Load();
                loaded.Axes.Single(a => a.Axis == "Rz").Positive = BrowserAction.NextTab;
                store.Apply(loaded, MappingStore.Hash(firstDriver), MappingStore.Hash(firstSettings));
                store.Restore();
                Assert(MappingStore.Hash(firstSettings) == MappingStore.Hash(File.ReadAllBytes(store.SettingsPath)), "Vorherige Settings bytegenau wiederhergestellt");
                Assert(MappingStore.Hash(firstDriver) == MappingStore.Hash(File.ReadAllBytes(driverPath)), "Vorherige XML bytegenau wiederhergestellt");
                var badSettings = mapping.Clone();
                badSettings.Axes[0].Scale = 0;
                File.WriteAllText(store.SettingsPath, new JavaScriptSerializer().Serialize(badSettings));
                Reject(delegate { store.Load(); }, "Beschaedigte Settings nicht still ersetzt");

                File.Delete(store.SettingsPath);
                string pointerPath = store.LastBackupPath;
                Directory.CreateDirectory(pointerPath);
                byte[] beforeFailure = File.ReadAllBytes(driverPath);
                bool failed = false;
                try { store.Apply(mapping, MappingStore.Hash(beforeFailure), "absent"); }
                catch (IOException) { failed = true; }
                catch (UnauthorizedAccessException) { failed = true; }
                Assert(failed, "Manifest-Schreibfehler nicht verschluckt");
                Assert(MappingStore.Hash(beforeFailure) == MappingStore.Hash(File.ReadAllBytes(driverPath)) &&
                    !File.Exists(store.SettingsPath), "Manifest-Schreibfehler rollt beide Dateien zurueck");
            }
            finally
            {
                Directory.Delete(temporary, true);
            }
        }
    }
}
