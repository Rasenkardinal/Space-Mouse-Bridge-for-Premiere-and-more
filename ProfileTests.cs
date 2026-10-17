using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;

namespace SpaceMouseBridge
{
    internal static class ProfileTests
    {
        private static void Assert(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("Profil-Test fehlgeschlagen: " + name);
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
            Assert(ApplicationProfile.Match("chrome", "Chrome_WidgetWin_1") == ApplicationProfile.Chrome, "Chrome erkannt");
            Assert(ApplicationProfile.Match("CHROME", "Chrome_WidgetWin_1") == ApplicationProfile.Chrome, "Prozessname ohne Gross-/Kleinschreibung");
            Assert(ApplicationProfile.Match("chrome", "OtherWindow") == null, "Chrome-Fensterklasse erforderlich");
            Assert(ApplicationProfile.Match("msedge", "Chrome_WidgetWin_1") == null, "Chromium ist nicht automatisch Chrome");
            Assert(ApplicationProfile.Match("Adobe Premiere Pro", "AnyWindow") == ApplicationProfile.Premiere, "Premiere-Prozess erkannt");
            Assert(ApplicationProfile.Match("Adobe Premiere Pro Helper", "AnyWindow") == null, "Keine Teilstring-Zuordnung");
            Assert(ApplicationProfile.Match("acad", "AnyWindow") == null, "CAD bleibt ohne Profil");
            Assert(ApplicationProfile.Match(null, null) == null, "Fehlende Prozesskennung ohne Profil");

            string temporary = Path.Combine(Path.GetTempPath(), "SpaceMouseBridge-ProfileTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporary);
            try
            {
                string chromeRoot = Path.Combine(temporary, "chrome");
                string premiereRoot = Path.Combine(temporary, "premiere");
                string chromeXml = Path.Combine(temporary, "Chrome.xml");
                string premiereXml = Path.Combine(temporary, "Premiere.xml");
                File.WriteAllText(premiereXml, "untouched driver");
                var chromeStore = new MappingStore(chromeRoot, chromeXml);
                var premiereStore = new MappingStore(premiereRoot, premiereXml, ApplicationProfile.Premiere);
                var chrome = ApplicationMapping.Default();
                chrome.Axes.Single(a => a.Axis == "Z").Mode = AxisMode.Zoom;
                chrome.Axes.Single(a => a.Axis == "Z").Reverse = true;
                chrome.Axes.Single(a => a.Axis == "Z").Scale = 0.50m;
                chrome.Axes.Single(a => a.Axis == "Z").Deadband = 35;
                var rotation = chrome.Axes.Single(a => a.Axis == "Rz");
                rotation.Mode = AxisMode.Bridge;
                rotation.Negative = BrowserAction.ZoomOut;
                rotation.Positive = BrowserAction.ZoomIn;
                chrome.Buttons[0].Mode = ButtonMode.Bridge;
                chrome.Buttons[0].Action = BrowserAction.Back;
                chrome.Buttons[1].Mode = ButtonMode.Bridge;
                chrome.Buttons[1].Action = BrowserAction.Forward;
                chromeStore.Apply(chrome, "absent", "absent");

                // Simulate the already installed Version 1 settings without a profile identifier.
                chrome = chromeStore.Load();
                chrome.ProfileId = null;
                string legacy = new JavaScriptSerializer().Serialize(chrome).Replace("\"ProfileId\":null,", "");
                File.WriteAllText(chromeStore.SettingsPath, legacy, new UTF8Encoding(false));
                byte[] chromeBefore = File.ReadAllBytes(chromeStore.SettingsPath);
                byte[] driverBefore = File.ReadAllBytes(chromeXml);
                byte[] undoBefore = File.ReadAllBytes(chromeStore.LastBackupPath);
                var chromeSession = new ProfileSession(chromeStore);
                chromeSession.Load();
                Assert(chromeSession.Mapping.Axes.Single(a => a.Axis == "Z").Reverse &&
                    chromeSession.Mapping.Axes.Single(a => a.Axis == "Z").Scale == 0.50m &&
                    chromeSession.Mapping.Axes.Single(a => a.Axis == "Z").Deadband == 35 &&
                    chromeSession.Mapping.Axes.Single(a => a.Axis == "Rz").Positive == BrowserAction.ZoomIn &&
                    chromeSession.Mapping.Buttons[1].Action == BrowserAction.Forward, "Bestehende Chrome-Belegung verlustfrei geladen");

                var empty = premiereStore.Load();
                Assert(!empty.Enabled && empty.ProfileId == "premiere", "Premiere initial inaktiv");
                Assert(empty.Axes.All(a => a.Mode == AxisMode.KeepDriver &&
                    a.Negative == BrowserAction.None && a.Positive == BrowserAction.None) &&
                    empty.Buttons.All(b => b.Mode == ButtonMode.KeepDriver && b.Action == BrowserAction.None),
                    "Premiere ohne aktive Achsen oder Tasten");
                premiereStore.Apply(empty, "absent", "absent");
                var premiereSession = new ProfileSession(premiereStore);
                premiereSession.Load();
                Assert(premiereSession.Healthy && !premiereSession.Mapping.Enabled, "Leerprofil nach Neustart geladen");
                Assert(premiereStore.Apply(premiereSession.Mapping, "absent",
                    MappingStore.Hash(File.ReadAllBytes(premiereStore.SettingsPath))) == null, "Leerprofil-Speichern idempotent");
                Assert(File.ReadAllText(premiereXml) == "untouched driver", "Premiere-Treiber nie angefasst");
                Assert(chromeBefore.SequenceEqual(File.ReadAllBytes(chromeStore.SettingsPath)) &&
                    driverBefore.SequenceEqual(File.ReadAllBytes(chromeXml)) &&
                    undoBefore.SequenceEqual(File.ReadAllBytes(chromeStore.LastBackupPath)), "Chrome-Dateien samt Undo bytegenau unveraendert");
                using (var editor = new PremiereEditor(premiereStore, premiereSession.Mapping))
                    ApplicationProfile.Premiere.Validate(editor.Collect());
                var invalid = empty.Clone();
                invalid.Enabled = true;
                Reject(delegate { premiereStore.Apply(invalid, "absent", "wrong"); }, "Leeres Premiere-Profil nicht aktivierbar");
                invalid = empty.Clone();
                invalid.Axes[5].Positive = BrowserAction.Back;
                Reject(delegate { ApplicationProfile.Premiere.Validate(invalid); }, "Keine Browseraktion im Premiere-Profil");
                invalid = empty.Clone();
                invalid.Axes[0].Mode = AxisMode.Disabled;
                Reject(delegate { ApplicationProfile.Premiere.Validate(invalid); }, "Keine Treibermanipulation im Premiere-Profil");
                Reject(delegate { ApplicationProfile.Premiere.Validate(chromeSession.Mapping); }, "Keine Legacy-Chrome-Daten in Premiere");
                Reject(delegate { ApplicationProfile.Chrome.Validate(empty); }, "Keine Premiere-Daten in Chrome");

                var router = new ProfileRouter();
                router.Configure(new[] { chromeSession, premiereSession });
                IntPtr first = new IntPtr(101);
                Assert(router.Select(first, 10, "chrome", "Chrome_WidgetWin_1") &&
                    router.Current == ApplicationProfile.Chrome && router.CanExecute, "Chrome automatisch aktiv");
                Assert(!router.Select(first, 10, "chrome", "Chrome_WidgetWin_1"), "Gleicher Kontext ohne Reset");
                Assert(router.Select(new IntPtr(102), 10, "chrome", "Chrome_WidgetWin_1"), "Anderes Fenster derselben App fordert Reset");
                Assert(router.Select(new IntPtr(201), 20, "Adobe Premiere Pro", "Premiere") &&
                    router.Current == ApplicationProfile.Premiere && !router.CanExecute, "Premiere erkannt, keine Ausgabe");
                Assert(router.ActiveMapping.ProfileId == "premiere", "Premiere erbt keine Chrome-Belegung");
                Assert(router.Select(new IntPtr(301), 30, "acad", "CAD") &&
                    router.Current == null && router.ActiveMapping == null && !router.CanExecute, "Andere Apps ohne Ausgabe");
                Assert(router.Select(first, 11, "chrome", "Chrome_WidgetWin_1") && router.CanExecute, "Zurueck zu Chrome");
                Assert(router.Select(IntPtr.Zero, 0, "chrome", "Chrome_WidgetWin_1") &&
                    !router.CanExecute && router.Current == null, "Ohne Vordergrund keine Ausgabe");

                var profileLog = new List<string>();
                var runtime = new ApplicationMappings(profileLog.Add);
                runtime.Configure(new[] { chromeSession, premiereSession });
                runtime.SelectContext(first, 10, "chrome", "Chrome_WidgetWin_1");
                var gate = (TabReleaseGate)typeof(ApplicationMappings).GetField("releaseGate",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(runtime);
                gate.Request((int)BrowserAction.Back, DateTime.UtcNow);
                runtime.SelectContext(new IntPtr(201), 20, "Adobe Premiere Pro", "Premiere");
                Assert(gate.Update(true, DateTime.UtcNow) == 0, "Vorgemerkte Chrome-Aktion beim Appwechsel verworfen");
                runtime.SelectContext(first, 10, "chrome", "Chrome_WidgetWin_1");
                gate.Request((int)BrowserAction.Back, DateTime.UtcNow);
                runtime.SelectContext(new IntPtr(102), 10, "chrome", "Chrome_WidgetWin_1");
                Assert(gate.Update(true, DateTime.UtcNow) == 0, "Vormerkung auch beim Chrome-Fensterwechsel verworfen");
                Assert(profileLog.Count == 3 && profileLog[0].Contains("Google Chrome") &&
                    profileLog[1].Contains("Adobe Premiere Pro"),
                    "Bekannte Profilwechsel protokolliert, nicht jeder Fensterwechsel");
                gate.Request((int)BrowserAction.Back, DateTime.UtcNow);
                runtime.Configure(new[] { chromeSession, premiereSession });
                Assert(gate.Update(true, DateTime.UtcNow) == 0, "Konfigurationswechsel verwirft Vormerkung");

                chromeSession.Fail("Test");
                router.Configure(new[] { chromeSession, premiereSession });
                router.Select(first, 10, "chrome", "Chrome_WidgetWin_1");
                Assert(!router.CanExecute && router.ActiveMapping == null, "Fehlerhaftes Profil gesperrt");
                router.Select(new IntPtr(201), 20, "Adobe Premiere Pro", "Premiere");
                Assert(router.ActiveMapping != null && premiereSession.Healthy, "Profilfehler bleibt isoliert");
                chromeSession.Load();
                chromeSession.Mapping.Enabled = false;
                router.Configure(new[] { chromeSession, premiereSession });
                router.Select(first, 10, "chrome", "Chrome_WidgetWin_1");
                Assert(!router.CanExecute, "Persistenter Profilschalter respektiert");
                Assert(chromeSession.FilesUnchanged() && premiereSession.FilesUnchanged(), "Ueberwachung pro Profil");
                File.AppendAllText(premiereStore.SettingsPath, " ");
                Assert(!premiereSession.FilesUnchanged() && chromeSession.FilesUnchanged(), "Externe Premiere-Aenderung betrifft nicht Chrome");
                Reject(premiereStore.Restore, "Externe Premiere-Aenderung blockiert Undo");
                File.WriteAllText(premiereStore.SettingsPath,
                    new JavaScriptSerializer().Serialize(premiereSession.Mapping), new UTF8Encoding(false));
                premiereStore.Restore();
                Assert(!File.Exists(premiereStore.SettingsPath) && File.ReadAllText(premiereXml) == "untouched driver",
                    "Leerprofil-Undo ohne Treiberzugriff");
                Assert(chromeBefore.SequenceEqual(File.ReadAllBytes(chromeStore.SettingsPath)) &&
                    driverBefore.SequenceEqual(File.ReadAllBytes(chromeXml)), "Auch Profil-Undo laesst Chrome unveraendert");
            }
            finally { Directory.Delete(temporary, true); }
        }
    }
}
