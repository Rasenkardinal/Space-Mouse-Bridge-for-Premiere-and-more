using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using System.Xml;

namespace SpaceMouseBridge
{
    public enum AxisMode { KeepDriver, Disabled, Scroll, HorizontalScroll, Zoom, Bridge }
    public enum BrowserAction { None, Back, Forward, PreviousTab, NextTab, ZoomIn, ZoomOut, ResetZoom, Reload, Home, End, ContextMenu, ContextMenu2 }
    public enum ButtonMode { KeepDriver, Disabled, Bridge }

    public sealed class AxisMapping
    {
        public string Axis { get; set; }
        public AxisMode Mode { get; set; }
        public bool Reverse { get; set; }
        public decimal Scale { get; set; }
        public int Deadband { get; set; }
        public BrowserAction Negative { get; set; }
        public BrowserAction Positive { get; set; }
    }

    public sealed class ButtonMapping
    {
        public int Button { get; set; }
        public ButtonMode Mode { get; set; }
        public BrowserAction Action { get; set; }
    }

    public sealed class ApplicationMapping
    {
        public string ProfileId { get; set; }
        public int Version { get; set; }
        public bool Enabled { get; set; }
        public int Threshold { get; set; }
        public int Neutral { get; set; }
        public AxisMapping[] Axes { get; set; }
        public ButtonMapping[] Buttons { get; set; }
        public string DriverHash { get; set; }
        public JogSettings Jog { get; set; }
        public TimelineScrollSettings TimelineScroll { get; set; }
        public PremiereBindings PremiereInputs { get; set; }
        public PremiereMenuItem[] PremiereMenu { get; set; }
        public PremiereMenuItem[] ChromeMenu { get; set; }
        public RadialMenuConfiguration Menu2 { get; set; }
        private int menuRadius = 77;
        public int MenuRadius { get { return menuRadius; } set { menuRadius = value; } }
        private int menuCenterPercent = 33;
        public int MenuCenterPercent { get { return menuCenterPercent; } set { menuCenterPercent = value; } }
        private decimal menuBorderWidth = 1m;
        public decimal MenuBorderWidth { get { return menuBorderWidth; } set { menuBorderWidth = value; } }
        private decimal menuSegmentGap = 2m;
        public decimal MenuSegmentGap { get { return menuSegmentGap; } set { menuSegmentGap = value; } }
        private decimal menuTextSize = 6m;
        public decimal MenuTextSize { get { return menuTextSize; } set { menuTextSize = value; } }
        private decimal menuIconSize = 24m;
        public decimal MenuIconSize { get { return menuIconSize; } set { menuIconSize = value; } }
        internal static readonly string[] AxisNames = { "X", "Y", "Z", "Rx", "Ry", "Rz" };

        internal static ApplicationMapping Default()
        {
            return new ApplicationMapping {
                Version = 1, Enabled = true, Threshold = 45, Neutral = 12,
                Axes = AxisNames.Select(a => new AxisMapping {
                    Axis = a, Mode = AxisMode.KeepDriver, Scale = 1, Deadband = 0
                }).ToArray(),
                Buttons = new[] {
                    new ButtonMapping { Button = 1, Mode = ButtonMode.KeepDriver },
                    new ButtonMapping { Button = 2, Mode = ButtonMode.KeepDriver }
                }
            };
        }

        internal void Validate()
        {
            if (MenuRadius < 40 || MenuRadius > 200)
                throw new InvalidDataException("Der Menue-Radius muss zwischen 40 und 200 Pixeln liegen.");
            if (MenuCenterPercent < 15 || MenuCenterPercent > 60)
                throw new InvalidDataException("Der Mittelkreis muss zwischen 15 und 60 Prozent des Menue-Radius liegen.");
            if (MenuBorderWidth < 0.5m || MenuBorderWidth > 5m)
                throw new InvalidDataException("Die Konturlinienbreite muss zwischen 0,5 und 5 Pixeln liegen.");
            if (MenuSegmentGap < 0 || MenuSegmentGap > 8)
                throw new InvalidDataException("Der Segmentabstand muss zwischen 0 und 8 Pixeln liegen.");
            if (MenuTextSize < 4 || MenuTextSize > 16)
                throw new InvalidDataException("Die Menue-Textgroesse muss zwischen 4 und 16 pt liegen.");
            if (MenuIconSize < 8 || MenuIconSize > 48)
                throw new InvalidDataException("Die Menue-Icongroesse muss zwischen 8 und 48 Pixeln liegen.");
            if (Menu2 != null) Menu2.Validate();
            if (Version != 1 || Threshold < 15 || Threshold > 95 ||
                Neutral < 1 || Neutral > 30 || Neutral >= Threshold)
                throw new InvalidDataException("Ungueltige Profilversion oder Gestenschwellen (Neutral < Ausloesung erforderlich).");
            if (Axes == null || Axes.Length != 6 || Axes.Any(a => a == null) ||
                !Axes.Select(a => a.Axis).OrderBy(a => a).SequenceEqual(AxisNames.OrderBy(a => a)) ||
                Buttons == null || Buttons.Length != 2 || Buttons.Any(b => b == null) ||
                !Buttons.Select(b => b.Button).OrderBy(b => b).SequenceEqual(new[] { 1, 2 }))
                throw new InvalidDataException("Ein Anwendungsprofil muss sechs eindeutige Achsen und zwei Tasten enthalten.");
            foreach (AxisMapping axis in Axes)
            {
                if (!Enum.IsDefined(typeof(AxisMode), axis.Mode) || axis.Scale < 0.05m || axis.Scale > 5 ||
                    axis.Deadband < 0 || axis.Deadband > 250 ||
                    !Enum.IsDefined(typeof(BrowserAction), axis.Negative) ||
                    !Enum.IsDefined(typeof(BrowserAction), axis.Positive) ||
                    axis.Negative == BrowserAction.ContextMenu || axis.Positive == BrowserAction.ContextMenu ||
                    axis.Negative == BrowserAction.ContextMenu2 || axis.Positive == BrowserAction.ContextMenu2)
                    throw new InvalidDataException("Ungueltige Zuordnung fuer " + axis.Axis);
            }
            foreach (ButtonMapping button in Buttons)
                if (!Enum.IsDefined(typeof(ButtonMode), button.Mode) ||
                    !Enum.IsDefined(typeof(BrowserAction), button.Action))
                    throw new InvalidDataException("Ungueltige Tastenbelegung.");
            if (Jog != null) Jog.Validate();
            if (TimelineScroll != null) TimelineScroll.Validate();
            if (PremiereInputs != null) PremiereInputs.Validate();
            if (PremiereMenu != null)
                foreach (var item in PremiereMenu) PremiereMenuItem.Validate(item);
            if (ChromeMenu != null)
                foreach (var item in ChromeMenu) PremiereMenuItem.Validate(item);
            if (Buttons.Any(b => b.Action == BrowserAction.ContextMenu) &&
                (ChromeMenu == null || ChromeMenu.Length == 0))
                throw new InvalidDataException("Zum Chrome-Kontextmenue bitte mindestens einen Menueeintrag konfigurieren.");
        }

        internal ApplicationMapping Clone()
        {
            var json = new JavaScriptSerializer();
            return json.Deserialize<ApplicationMapping>(json.Serialize(this));
        }
    }

    internal static class DriverProfile
    {
        internal static XmlDocument Parse(byte[] content)
        {
            var document = new XmlDocument { XmlResolver = null };
            using (var stream = new MemoryStream(content))
            using (var reader = XmlReader.Create(stream, new XmlReaderSettings {
                DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null
            }))
                document.Load(reader);
            if (document.SelectSingleNode("/AppCfg[@Default='false']/CfgProperties[ID='ID_STR_CHROME' and InheritsFromID='ID_STR_CHROME']") == null ||
                document.SelectSingleNode("/AppCfg/AppInfo/Signature[ExecutableName='CHROME.EXE']") == null)
                throw new InvalidDataException("Das Benutzerprofil ist kein unterstuetztes Chrome-Override. Keine Datei wurde geaendert.");
            return document;
        }

        internal static byte[] Empty()
        {
            return Encoding.UTF8.GetBytes(
                "<?xml version=\"1.0\" encoding=\"utf-8\"?><AppCfg Default=\"false\" CfgFormatVersion=\"1.3\" ThisFileVersion=\"1.2\">" +
                "<AppInfo><Signature><Name>STR_CHROME</Name><ExecutableName>CHROME.EXE</ExecutableName></Signature></AppInfo>" +
                "<CfgProperties><ID>ID_STR_CHROME</ID><Name>STR_CHROME</Name><InheritsFromID>ID_STR_CHROME</InheritsFromID></CfgProperties>" +
                "<Devices><Device><ID>ID_Standard_3D_Mouse</ID></Device></Devices></AppCfg>");
        }

        private static XmlElement Child(XmlNode parent, string name)
        {
            var nodes = parent.SelectNodes(name);
            if (nodes.Count > 1) throw new InvalidDataException("Mehrdeutiger XML-Abschnitt: " + name);
            if (nodes.Count == 1) return (XmlElement)nodes[0];
            var child = parent.OwnerDocument.CreateElement(name);
            parent.AppendChild(child);
            return child;
        }

        private static void Value(XmlNode parent, string name, string value)
        {
            Child(parent, name).InnerText = value;
        }

        private static XmlNode StandardDevice(XmlDocument document)
        {
            XmlNode devices = Child(document.DocumentElement, "Devices");
            var matches = devices.SelectNodes("Device[ID='ID_Standard_3D_Mouse']");
            if (matches.Count > 1) throw new InvalidDataException("Mehrere Standard-3D-Maus-Abschnitte im Profil.");
            if (matches.Count == 1) return matches[0];
            var device = document.CreateElement("Device");
            devices.AppendChild(device);
            Value(device, "ID", "ID_Standard_3D_Mouse");
            return device;
        }

        private static XmlNode Bank(XmlNode device, string name)
        {
            var banks = device.SelectNodes(name + "[ID='Default']");
            if (banks.Count > 1) throw new InvalidDataException("Mehrdeutige Default-Bank: " + name);
            if (banks.Count == 1) return banks[0];
            var bank = device.OwnerDocument.CreateElement(name);
            bank.SetAttribute("Default", "true");
            device.AppendChild(bank);
            Value(bank, "ID", "Default");
            Value(bank, "Name", "STR_DEFAULT_BANK");
            return bank;
        }

        internal static ApplicationMapping Import(byte[] content)
        {
            ApplicationMapping mapping = ApplicationMapping.Default();
            // Known factory default when no user override supplies an axis.
            var scroll = mapping.Axes.Single(a => a.Axis == "Rx");
            scroll.Mode = AxisMode.Scroll;
            var rotation = mapping.Axes.Single(a => a.Axis == "Rz");
            rotation.Mode = AxisMode.Bridge;
            rotation.Negative = BrowserAction.Back;
            rotation.Positive = BrowserAction.Forward;
            XmlDocument document = Parse(content ?? Empty());
            foreach (AxisMapping axis in mapping.Axes)
            {
                var nodes = document.SelectNodes("//Device[ID='ID_Standard_3D_Mouse']/AxisBank[ID='Default']/Axis[Input/ActionID='HIDMultiAxis_" + axis.Axis + "']");
                if (nodes.Count > 1) throw new InvalidDataException("Mehrdeutige Achse im Chrome-Profil: " + axis.Axis);
                if (nodes.Count == 0) continue;
                XmlNode node = nodes[0];
                string enabled = node.SelectSingleNode("Enabled") == null ? "true" : node["Enabled"].InnerText;
                if (enabled == "false") { axis.Mode = AxisMode.Disabled; continue; }
                XmlNode output = node.SelectSingleNode("Output");
                if (output == null || output["ActionID"] == null || output["ActionID"].InnerText != "HIDMouse_Wheel")
                {
                    axis.Mode = AxisMode.KeepDriver;
                    continue;
                }
                string modifiers = output["Modifiers"] == null ? "" : output["Modifiers"].InnerText;
                axis.Mode = modifiers == "" ? AxisMode.Scroll : modifiers == "Control" ? AxisMode.Zoom :
                    modifiers == "Shift" ? AxisMode.HorizontalScroll : AxisMode.KeepDriver;
                axis.Reverse = output["Reversed"] != null && XmlConvert.ToBoolean(output["Reversed"].InnerText);
                if (output["Scale"] != null)
                    axis.Scale = decimal.Parse(output["Scale"].InnerText, CultureInfo.InvariantCulture);
                XmlNode deadband = node.SelectSingleNode("Input/Deadband");
                if (deadband != null) axis.Deadband = XmlConvert.ToInt32(deadband.InnerText);
            }
            mapping.Validate();
            return mapping;
        }

        internal static byte[] Merge(byte[] content, ApplicationMapping mapping)
        {
            mapping.Validate();
            XmlDocument document = Parse(content ?? Empty());
            XmlNode standard = StandardDevice(document);
            foreach (AxisMapping axis in mapping.Axes)
            {
                if (axis.Mode == AxisMode.KeepDriver) continue;
                string selector = "Axis[Input/ActionID='HIDMultiAxis_" + axis.Axis + "']";
                var conflicting = document.SelectNodes("//Device[ID!='ID_Standard_2D_Mouse' and ID!='ID_Standard_Keyboard']/AxisBank/" + selector);
                foreach (XmlNode existing in conflicting)
                    if (existing.ParentNode.ParentNode != standard || existing.ParentNode["ID"] == null ||
                        existing.ParentNode["ID"].InnerText != "Default")
                        throw new InvalidDataException("Geraete-/Bank-spezifische Belegung fuer " + axis.Axis +
                            " gefunden. Bitte zuerst in 3DxWare klaeren; kein Ueberschreiben.");
                XmlNode bank = Bank(standard, "AxisBank");
                var nodes = bank.SelectNodes(selector);
                if (nodes.Count > 1) throw new InvalidDataException("Mehrdeutige Achsenbelegung: " + axis.Axis);
                var entry = document.CreateElement("Axis");
                bool active = axis.Mode != AxisMode.Disabled && axis.Mode != AxisMode.Bridge;
                Value(entry, "Enabled", active ? "true" : "false");
                XmlNode input = Child(entry, "Input");
                Value(input, "ActionID", "HIDMultiAxis_" + axis.Axis);
                Value(input, "Min", "-512");
                Value(input, "Max", "511");
                if (active) Value(input, "Deadband", axis.Deadband.ToString(CultureInfo.InvariantCulture));
                XmlNode output = Child(entry, "Output");
                Value(output, "ActionID", active ? "HIDMouse_Wheel" : "HIDMultiAxis_" + axis.Axis);
                if (active)
                {
                    Value(output, "Scale", axis.Scale.ToString("0.00", CultureInfo.InvariantCulture));
                    Value(output, "Reversed", axis.Reverse ? "true" : "false");
                    if (axis.Mode == AxisMode.Zoom || axis.Mode == AxisMode.HorizontalScroll)
                        Value(Child(output, "Modifiers"), "Modifier", axis.Mode == AxisMode.Zoom ? "Control" : "Shift");
                }
                if (nodes.Count == 1) bank.ReplaceChild(entry, nodes[0]); else bank.AppendChild(entry);
            }
            foreach (ButtonMapping button in mapping.Buttons)
            {
                if (button.Mode == ButtonMode.KeepDriver) continue;
                foreach (string id in new[] { "V3DK_" + button.Button, "HIDButton_" + button.Button })
                {
                    var other = document.SelectNodes("//Device[ID!='ID_Standard_3D_Mouse' and ID!='ID_Standard_2D_Mouse' and ID!='ID_Standard_Keyboard']/ButtonBank/Button[Input/ActionID='" + id + "']");
                    if (other.Count > 0)
                        throw new InvalidDataException("Geraetespezifische Tastenbelegung gefunden: " + id);
                    if (standard.SelectNodes("ButtonBank[ID!='Default' or not(ID)]/Button[Input/ActionID='" + id + "']").Count > 0)
                        throw new InvalidDataException("Zusaetzliche Tastenbank fuer " + id + " gefunden.");
                    XmlNode bank = Bank(standard, "ButtonBank");
                    var entries = bank.SelectNodes("Button[Input/ActionID='" + id + "']");
                    if (entries.Count > 1) throw new InvalidDataException("Mehrdeutige Tastenbelegung: " + id);
                    var entry = document.CreateElement("Button");
                    Value(Child(entry, "Input"), "ActionID", id);
                    Value(Child(entry, "Output"), "ActionID", "");
                    if (entries.Count == 1) bank.ReplaceChild(entry, entries[0]); else bank.AppendChild(entry);
                }
            }
            using (var stream = new MemoryStream())
            {
                using (var writer = XmlWriter.Create(stream, new XmlWriterSettings {
                    Encoding = new UTF8Encoding(false), Indent = true
                })) document.Save(writer);
                byte[] result = stream.ToArray();
                Parse(result);
                return result;
            }
        }
    }

    public sealed class MappingBackup
    {
        public string DriverBeforeHash { get; set; }
        public string SettingsBeforeHash { get; set; }
        public string DriverAfterHash { get; set; }
        public string SettingsAfterHash { get; set; }
        public string Directory { get; set; }
    }

    internal sealed class MappingStore
    {
        internal readonly string Root;
        internal readonly string DriverPath;
        internal readonly ApplicationProfile Profile;
        internal string SettingsPath { get { return Path.Combine(Root, Profile.Id + ".json"); } }
        internal string LastBackupPath { get { return Path.Combine(Root, "last-backup.json"); } }

        internal MappingStore(string root, string driverPath) : this(root, driverPath, ApplicationProfile.Chrome) { }
        internal MappingStore(string root, string driverPath, ApplicationProfile profile)
        {
            Root = root;
            DriverPath = driverPath;
            Profile = profile;
        }
        internal static MappingStore ForUser()
        {
            return ForUser(ApplicationProfile.Chrome);
        }
        internal static MappingStore ForUser(ApplicationProfile profile)
        {
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SpaceMouseBridge");
            if (profile.Id != ApplicationProfile.Chrome.Id)
                root = Path.Combine(root, "profiles", profile.Id);
            string driver = profile.SupportsDriverSetup ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                @"3Dconnexion\3DxWare\Cfg\ChromeWithExtension.xml") : null;
            return new MappingStore(root, driver, profile);
        }

        internal byte[] ReadDriver() { return Profile.SupportsDriverSetup ? Read(DriverPath) : null; }

        internal static byte[] Read(string path)
        {
            try { return File.ReadAllBytes(path); }
            catch (FileNotFoundException) { return null; }
            catch (DirectoryNotFoundException) { return null; }
        }
        internal static string Hash(byte[] data)
        {
            if (data == null) return "absent";
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "");
        }

        internal ApplicationMapping Load()
        {
            string settingsHash;
            return Load(out settingsHash);
        }

        internal ApplicationMapping Load(out string settingsHash)
        {
            byte[] settings = Read(SettingsPath);
            settingsHash = Hash(settings);
            byte[] driver = ReadDriver();
            if (settings == null)
            {
                ApplicationMapping imported = Profile.SupportsDriverSetup ? DriverProfile.Import(driver) : Profile.EmptyMapping();
                imported.ProfileId = Profile.Id;
                imported.DriverHash = Hash(driver);
                return imported;
            }
            var mapping = new JavaScriptSerializer().Deserialize<ApplicationMapping>(Encoding.UTF8.GetString(settings));
            if (mapping == null) throw new InvalidDataException("Leere Mapping-Datei.");
            Profile.Validate(mapping);
            if (mapping.DriverHash != Hash(driver))
                throw new InvalidDataException("Chrome-XML wurde ausserhalb der Bridge geaendert. Bridge-Ausgabe bleibt aus. Im Mapping-Editor Treiber neu einlesen und erneut uebernehmen.");
            return mapping;
        }

        internal static void AtomicWrite(string path, byte[] bytes)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllBytes(temporary, bytes);
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        private static byte[] Json(object value)
        {
            return Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(value));
        }

        internal string Apply(ApplicationMapping mapping, string expectedDriverHash, string expectedSettingsHash)
        {
            Profile.Validate(mapping);
            byte[] driverBefore = ReadDriver();
            byte[] settingsBefore = Read(SettingsPath);
            if (Hash(driverBefore) != expectedDriverHash || Hash(settingsBefore) != expectedSettingsHash)
                throw new InvalidDataException("Die Dateien wurden inzwischen geaendert. Bitte den Editor schliessen und neu oeffnen.");
            byte[] driverAfter = Profile.SupportsDriverSetup ? DriverProfile.Merge(driverBefore, mapping) : null;
            mapping = mapping.Clone();
            mapping.ProfileId = Profile.Id;
            mapping.DriverHash = Hash(driverAfter);
            byte[] settingsAfter = Json(mapping);
            if (Hash(driverBefore) == Hash(driverAfter) && Hash(settingsBefore) == Hash(settingsAfter))
                return null;
            string backupDir = Path.Combine(Root, "backups", DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(backupDir);
            if (driverBefore != null) File.WriteAllBytes(Path.Combine(backupDir, "driver-before.xml"), driverBefore);
            if (settingsBefore != null) File.WriteAllBytes(Path.Combine(backupDir, "settings-before.json"), settingsBefore);
            var backup = new MappingBackup {
                Directory = backupDir, DriverBeforeHash = Hash(driverBefore), SettingsBeforeHash = Hash(settingsBefore),
                DriverAfterHash = Hash(driverAfter), SettingsAfterHash = Hash(settingsAfter)
            };
            File.WriteAllBytes(Path.Combine(backupDir, "manifest.json"), Json(backup));
            if (Hash(ReadDriver()) != expectedDriverHash || Hash(Read(SettingsPath)) != expectedSettingsHash)
                throw new InvalidDataException("Dateiaenderung waehrend der Sicherung. Nichts uebernommen.");
            if (Profile.SupportsDriverSetup) AtomicWrite(DriverPath, driverAfter);
            try
            {
                AtomicWrite(SettingsPath, settingsAfter);
                AtomicWrite(LastBackupPath, Json(backup));
            }
            catch (IOException) { RollBack(driverBefore, settingsBefore, backup); throw; }
            catch (UnauthorizedAccessException) { RollBack(driverBefore, settingsBefore, backup); throw; }
            return backupDir;
        }

        private void RollBack(byte[] driverBefore, byte[] settingsBefore, MappingBackup backup)
        {
            string driverHash = Hash(ReadDriver());
            string settingsHash = Hash(Read(SettingsPath));
            if (driverHash != backup.DriverAfterHash ||
                (settingsHash != backup.SettingsAfterHash && settingsHash != backup.SettingsBeforeHash))
                throw new IOException("Speichern fehlgeschlagen und parallele Dateiaenderung erkannt. Automatische Ruecknahme abgebrochen. Sicherung: " + backup.Directory);
            if (Profile.SupportsDriverSetup) RestoreFile(DriverPath, driverBefore);
            RestoreFile(SettingsPath, settingsBefore);
        }

        private static void RestoreFile(string path, byte[] bytes)
        {
            if (bytes == null) { if (File.Exists(path)) File.Delete(path); }
            else AtomicWrite(path, bytes);
        }

        internal void Restore()
        {
            byte[] manifest = Read(LastBackupPath);
            if (manifest == null) throw new InvalidDataException("Keine Sicherung einer Mapping-Aenderung vorhanden.");
            var backup = new JavaScriptSerializer().Deserialize<MappingBackup>(Encoding.UTF8.GetString(manifest));
            if (backup == null || !Path.GetFullPath(backup.Directory).StartsWith(
                Path.GetFullPath(Path.Combine(Root, "backups")) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Ungueltiger Sicherungspfad.");
            if (Hash(ReadDriver()) != backup.DriverAfterHash || Hash(Read(SettingsPath)) != backup.SettingsAfterHash)
                throw new InvalidDataException("Seit der Sicherung wurden Einstellungen extern geaendert. Keine Wiederherstellung, um neuere Aenderungen zu schuetzen.");
            byte[] driver = Read(Path.Combine(backup.Directory, "driver-before.xml"));
            byte[] settings = Read(Path.Combine(backup.Directory, "settings-before.json"));
            if (Hash(driver) != backup.DriverBeforeHash || Hash(settings) != backup.SettingsBeforeHash)
                throw new InvalidDataException("Sicherung unvollstaendig oder beschaedigt.");
            if (!Profile.SupportsDriverSetup && driver != null)
                throw new InvalidDataException("Dieses Profil darf keine Treiberdateien wiederherstellen.");
            byte[] currentDriver = ReadDriver();
            byte[] currentSettings = Read(SettingsPath);
            if (Profile.SupportsDriverSetup) RestoreFile(DriverPath, driver);
            try
            {
                RestoreFile(SettingsPath, settings);
                File.Delete(LastBackupPath);
            }
            catch (IOException)
            {
                if (Profile.SupportsDriverSetup) RestoreFile(DriverPath, currentDriver);
                RestoreFile(SettingsPath, currentSettings);
                throw;
            }
            catch (UnauthorizedAccessException)
            {
                if (Profile.SupportsDriverSetup) RestoreFile(DriverPath, currentDriver);
                RestoreFile(SettingsPath, currentSettings);
                throw;
            }
        }
    }
}
