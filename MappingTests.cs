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
            Assert(mapping.MenuRadius == 77, "Bisheriger Standardradius bleibt erhalten");
            var legacyRadius = new JavaScriptSerializer().Deserialize<ApplicationMapping>("{}");
            Assert(legacyRadius.MenuRadius == 77, "Alte JSON-Dateien ohne Radius verwenden 77 Pixel");
            Assert(legacyRadius.MenuCenterPercent == 33, "Alte Profile verwenden 33 Prozent Mittelkreis");
            Assert(legacyRadius.MenuBorderWidth == 1m, "Alte Profile verwenden 1 Pixel Konturlinie");
            Assert(legacyRadius.MenuSegmentGap == 2m, "Alte Profile verwenden 2 Pixel Segmentabstand");
            Assert(legacyRadius.MenuTextSize == 6m, "Alte Profile behalten die Textgroesse");
            Assert(legacyRadius.MenuIconSize == 24 && new RadialMenuConfiguration().IconSize == 24,
                "Alte Menues behalten bisherige Icongroesse");
            mapping.Validate();
            mapping.MenuRadius = 120;
            mapping.MenuCenterPercent = 45;
            mapping.MenuBorderWidth = 3.5m;
            mapping.MenuSegmentGap = 4.5m;
            mapping.MenuTextSize = 9m;
            mapping.MenuIconSize = 32m;
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
            mapping.ChromeMenu = Enumerable.Range(0, 4).Select(i => new PremiereMenuItem {
                Name = i == 0 ? "Testmenue" : "Befehl " + i,
                Display = i == 0 ? MenuSegmentDisplay.Icon : MenuSegmentDisplay.Text, Icon = i == 0 ? MenuIcon.Zoom : MenuIcon.None,
                Shortcut = new PremiereShortcut { Key = (int)System.Windows.Forms.Keys.F8 + i, Control = true }
            }).ToArray();
            mapping.Menu2 = new RadialMenuConfiguration {
                Items = Enumerable.Range(0, 6).Select(i => new PremiereMenuItem {
                    Name = "Zweites Menue " + i, Shortcut = new PremiereShortcut { Key = (int)System.Windows.Forms.Keys.F1 + i },
                    Display = MenuSegmentDisplay.Icon, Icon = MenuIcon.Remove
                }).ToArray(), Radius = 150, CenterPercent = 50, BorderWidth = 2, SegmentGap = 3, TextSize = 8, IconSize = 18
            };
            merged = DriverProfile.Parse(DriverProfile.Merge(withExtra, mapping));
            Assert(merged.SelectSingleNode("//Button[Input/ActionID='V3DK_1']/Output/ActionID").InnerText == "",
                "Treiber-Taste unterdrueckt");
            Assert(merged.SelectSingleNode("//Button[Input/ActionID='HIDButton_1']/Output/ActionID").InnerText == "",
                "HID-Tastenalias unterdrueckt");
            Assert(merged.SelectNodes("//Button[Input/ActionID='V3DK_2']").Count == 0, "Unbelegte Taste unangetastet");
            mapping.Buttons[1].Mode = ButtonMode.Bridge;
            mapping.Buttons[1].Action = BrowserAction.ContextMenu2;
            var invalid = mapping.Clone();
            invalid.MenuIconSize = 7;
            Reject(invalid.Validate, "Zu kleine Icongroesse abgewiesen");
            invalid.MenuIconSize = 49;
            Reject(invalid.Validate, "Zu grosse Icongroesse abgewiesen");
            invalid = mapping.Clone();
            invalid.Menu2.IconSize = 49;
            Reject(invalid.Validate, "Icongroesse von Menue 2 validiert");
            invalid = mapping.Clone();
            invalid.MenuTextSize = 3.9m;
            Reject(invalid.Validate, "Textgroesse unter 4 pt abgewiesen");
            invalid.MenuTextSize = 16.1m;
            Reject(invalid.Validate, "Textgroesse ueber 16 pt abgewiesen");
            invalid = mapping.Clone();
            invalid.MenuSegmentGap = -1;
            Reject(invalid.Validate, "Negativer Segmentabstand abgewiesen");
            invalid.MenuSegmentGap = 8.1m;
            Reject(invalid.Validate, "Segmentabstand ueber 8 Pixel abgewiesen");
            invalid = mapping.Clone();
            invalid.MenuBorderWidth = 0.4m;
            Reject(invalid.Validate, "Zu duenne Konturlinie abgewiesen");
            invalid.MenuBorderWidth = 5.1m;
            Reject(invalid.Validate, "Zu breite Konturlinie abgewiesen");
            invalid = mapping.Clone();
            invalid.MenuCenterPercent = 14;
            Reject(invalid.Validate, "Mittelkreis unter 15 Prozent abgewiesen");
            invalid.MenuCenterPercent = 61;
            Reject(invalid.Validate, "Mittelkreis ueber 60 Prozent abgewiesen");
            invalid = mapping.Clone();
            invalid.MenuRadius = 39;
            Reject(invalid.Validate, "Radius unter 40 abgewiesen");
            invalid.MenuRadius = 201;
            Reject(invalid.Validate, "Radius ueber 200 abgewiesen");
            invalid = mapping.Clone();
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
                Assert(inputs.Length == (action == BrowserAction.None || action == BrowserAction.ContextMenu ||
                    action == BrowserAction.ContextMenu2 ? 0 :
                    action == BrowserAction.Reload ? 2 : 4),
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
            invalid = mapping.Clone();
            invalid.ChromeMenu = null;
            invalid.Buttons[0].Action = BrowserAction.ContextMenu;
            Reject(delegate { ApplicationProfile.Chrome.Validate(invalid); }, "Chrome-Kontextmenue benoetigt Eintraege");
            invalid = mapping.Clone();
            invalid.Axes[0].Positive = BrowserAction.ContextMenu;
            Reject(invalid.Validate, "Chrome-Kontextmenue nur per Taste");
            invalid.Axes[0].Positive = BrowserAction.ContextMenu2;
            Reject(invalid.Validate, "Auch Menue 2 nur per Taste");
            invalid = mapping.Clone();
            invalid.Menu2.Items = invalid.Menu2.Items.Take(3).ToArray();
            Reject(invalid.Validate, "Menue 2 benoetigt mindestens vier vollstaendige Segmente");
            invalid = mapping.Clone();
            invalid.PremiereMenu = mapping.ChromeMenu;
            Reject(delegate { ApplicationProfile.Chrome.Validate(invalid); }, "Premiere-Menue bleibt dem Premiere-Profil vorbehalten");

            string temporary = Path.Combine(Path.GetTempPath(), "SpaceMouseBridge-Tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporary);
            try
            {
                string driverPath = Path.Combine(temporary, "driver", "Chrome.xml");
                string root = Path.Combine(temporary, "settings");
                var store = new MappingStore(root, driverPath);
                using (var editor = new MappingEditor(store, mapping))
                {
                    var grid = (System.Windows.Forms.DataGridView)typeof(MappingEditor).GetField("axes",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(editor);
                    Assert(grid.Rows.Count == 8 && grid.Rows[6].Cells["Axis"].Value.Equals("Taste 1") &&
                        grid.Rows[7].Cells["Axis"].Value.Equals("Taste 2"),
                        "Chrome zeigt sechs Achsen und zwei Tasten in einer gemeinsamen Liste");
                    grid.Rows[6].Cells["Mode"].Value = ButtonMode.Bridge;
                    grid.Rows[6].Cells["Positive"].Value = BrowserAction.ContextMenu2;
                    Assert(!grid.Rows[6].Cells["Positive"].ReadOnly &&
                        grid.Rows[6].Cells["Negative"].ReadOnly && grid.Rows[6].Cells["Scale"].ReadOnly &&
                        editor.Collect().Buttons.Single(b => b.Button == 1).Action == BrowserAction.ContextMenu2,
                        "Tastenzeile bietet Menueaktionen ohne Achsenparameter");
                    grid.Rows[6].Cells["Mode"].Value = mapping.Buttons.Single(b => b.Button == 1).Mode;
                    grid.Rows[6].Cells["Positive"].Value = mapping.Buttons.Single(b => b.Button == 1).Action;
                    var roundTrip = editor.Collect();
                    Assert(roundTrip.MenuRadius == 120, "Chrome-Editor erhaelt den Menue-Radius");
                    Assert(roundTrip.MenuCenterPercent == 45, "Chrome-Editor erhaelt den Mittelkreis");
                    Assert(roundTrip.MenuBorderWidth == 3.5m, "Chrome-Editor erhaelt die Konturlinie");
                    Assert(roundTrip.MenuSegmentGap == 4.5m, "Chrome-Editor erhaelt Segmentabstand");
                    Assert(roundTrip.MenuTextSize == 9m, "Chrome-Editor erhaelt Textgroesse");
                    Assert(roundTrip.Menu2.Radius == 150 && roundTrip.Menu2.Items.Length == 6 &&
                        roundTrip.Buttons[1].Action == BrowserAction.ContextMenu2,
                        "Chrome-Editor erhaelt unabhaengiges zweites Menue und Tastenzuweisung");
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
                    Assert(roundTrip.ChromeMenu.Length == 4 && roundTrip.ChromeMenu[0].Name == "Testmenue",
                        "Chrome-Kontextmenue bleibt im Mapping-Editor erhalten");
                }
                string backup = store.Apply(mapping, "absent", "absent");
                Assert(Directory.Exists(backup), "Sicherung erstellt");
                ApplicationMapping loaded = store.Load();
                Assert(loaded.MenuRadius == 120, "Chrome-Menue-Radius persistent gespeichert");
                Assert(loaded.MenuCenterPercent == 45, "Chrome-Mittelkreis persistent gespeichert");
                Assert(loaded.MenuBorderWidth == 3.5m, "Konturlinienbreite persistent gespeichert");
                Assert(loaded.MenuSegmentGap == 4.5m, "Segmentabstand persistent gespeichert");
                Assert(loaded.MenuTextSize == 9m, "Textgroesse persistent gespeichert");
                Assert(loaded.MenuIconSize == 32 && loaded.Menu2.IconSize == 18,
                    "Icongroessen beider Chrome-Menues persistent gespeichert");
                Assert(loaded.ChromeMenu[0].Display == MenuSegmentDisplay.Icon && loaded.ChromeMenu[0].Icon == MenuIcon.Zoom &&
                    loaded.ChromeMenu[1].Display == MenuSegmentDisplay.Text && loaded.Menu2.Items[0].Icon == MenuIcon.Remove,
                    "Text- und Iconsegmente in beiden Chrome-Menues gespeichert");
                Assert(loaded.Menu2.Radius == 150 && loaded.Menu2.CenterPercent == 50 &&
                    loaded.Menu2.BorderWidth == 2 && loaded.Menu2.SegmentGap == 3 && loaded.Menu2.TextSize == 8 &&
                    loaded.Menu2.Items.Length == 6 && loaded.Menu2.Items[0].Shortcut.Key == (int)System.Windows.Forms.Keys.F1 &&
                    loaded.Buttons[1].Action == BrowserAction.ContextMenu2,
                    "Zweites Chrome-Menue mit allen Parametern und Taste persistent");
                Assert(loaded.Axes.Single(a => a.Axis == "Rz").Positive == BrowserAction.Forward, "Bridge-Aktion persistent");
                Assert(loaded.Buttons[0].Action == BrowserAction.NextTab, "Tastenaktion persistent");
                Assert(loaded.ChromeMenu.Length == 4 && loaded.ChromeMenu[0].Shortcut.Control,
                    "Chrome-Kontextmenue persistent gespeichert");
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
