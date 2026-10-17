using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SpaceMouseBridge
{
    internal sealed class ApplicationProfile
    {
        internal const string DriverFunctionNotice =
            "Bei Auswahl von Treiber-Funktionen wird beim Uebernehmen die anwendungsspezifische Treiber-XML angelegt bzw. angepasst.";
        internal static readonly ApplicationProfile Chrome = new ApplicationProfile(
            "chrome", "Google Chrome", "chrome.exe", "Chrome_WidgetWin_1", true, true);
        internal static readonly ApplicationProfile Premiere = new ApplicationProfile(
            "premiere", "Adobe Premiere Pro", "Adobe Premiere Pro.exe", null, true, false);
        internal static readonly ApplicationProfile[] All = { Chrome, Premiere };

        internal readonly string Id;
        internal readonly string Name;
        internal readonly string Executable;
        internal readonly string WindowClass;
        internal readonly bool SupportsActions;
        internal readonly bool SupportsDriverSetup;

        private ApplicationProfile(string id, string name, string executable, string windowClass,
            bool supportsActions, bool supportsDriverSetup)
        {
            Id = id;
            Name = name;
            Executable = executable;
            WindowClass = windowClass;
            SupportsActions = supportsActions;
            SupportsDriverSetup = supportsDriverSetup;
        }

        public override string ToString() { return Name; }

        internal static ApplicationProfile Match(string processName, string windowClass)
        {
            // Match a whole process name, never a title or a substring.
            return All.SingleOrDefault(p =>
                string.Equals(Path.GetFileNameWithoutExtension(p.Executable), processName, StringComparison.OrdinalIgnoreCase) &&
                (p.WindowClass == null || p.WindowClass == windowClass));
        }

        internal ApplicationMapping EmptyMapping()
        {
            var mapping = ApplicationMapping.Default();
            mapping.ProfileId = Id;
            mapping.Enabled = false;
            mapping.DriverHash = "absent";
            return mapping;
        }

        internal void Validate(ApplicationMapping mapping)
        {
            mapping.Validate();
            bool legacyChrome = Id == Chrome.Id && mapping.ProfileId == null;
            if (!legacyChrome && mapping.ProfileId != Id)
                throw new InvalidDataException("Die Mapping-Datei gehoert nicht zum Profil " + Name + ".");
            if (Id == Premiere.Id && (mapping.Axes.Any(a =>
                a.Mode != AxisMode.KeepDriver || a.Negative != BrowserAction.None || a.Positive != BrowserAction.None) ||
                mapping.Buttons.Any(b => b.Mode != ButtonMode.KeepDriver || b.Action != BrowserAction.None)))
                throw new InvalidDataException("Premiere verwendet eigene Eingabezuordnungen, keine Browser- oder Treiberaktionen.");
            if (Id == Premiere.Id && mapping.Enabled && mapping.Jog == null && mapping.PremiereInputs == null)
                throw new InvalidDataException("Zum Aktivieren des Premiere-Profils bitte Eingaben konfigurieren.");
            if (Id != Premiere.Id && mapping.Jog != null)
                throw new InvalidDataException("Jog ist nur im Premiere-Profil verfuegbar.");
            if (Id != Premiere.Id && mapping.TimelineScroll != null)
                throw new InvalidDataException("Timeline-Scrollen ist nur im Premiere-Profil verfuegbar.");
            if (Id != Premiere.Id && mapping.PremiereInputs != null)
                throw new InvalidDataException("Premiere-Eingaben sind nur im Premiere-Profil verfuegbar.");
            if (Id != Premiere.Id && mapping.PremiereMenu != null)
                throw new InvalidDataException("Das Premiere-Menue ist nur im Premiere-Profil verfuegbar.");
            if (!SupportsDriverSetup && mapping.DriverHash != "absent")
                throw new InvalidDataException(Name + " darf kein Treiber-Setup enthalten.");
        }
    }

    internal sealed class ProfileSession
    {
        internal readonly MappingStore Store;
        internal ApplicationProfile Profile { get { return Store.Profile; } }
        internal ApplicationMapping Mapping;
        internal bool Healthy;
        internal string Error;
        internal string SettingsHash;

        internal ProfileSession(MappingStore store)
        {
            Store = store;
            Mapping = store.Profile.EmptyMapping();
        }

        internal bool FilesUnchanged()
        {
            return Mapping.DriverHash == MappingStore.Hash(Store.ReadDriver()) &&
                SettingsHash == MappingStore.Hash(MappingStore.Read(Store.SettingsPath));
        }

        internal void Load()
        {
            Healthy = false;
            Mapping = Store.Load(out SettingsHash);
            Error = null;
            Healthy = true;
        }

        internal void Fail(string message)
        {
            Healthy = false;
            Error = message;
            Mapping = Profile.EmptyMapping();
        }
    }

    internal sealed class ProfileRouter
    {
        private Dictionary<string, ApplicationMapping> mappings = new Dictionary<string, ApplicationMapping>();
        private IntPtr window;
        private uint process;
        internal ApplicationProfile Current { get; private set; }
        internal ApplicationMapping ActiveMapping { get; private set; }
        internal bool CanExecute
        {
            get { return Current != null && Current.SupportsActions &&
                ActiveMapping != null && ActiveMapping.Enabled; }
        }

        internal void Configure(IEnumerable<ProfileSession> sessions)
        {
            mappings = sessions.Where(s => s.Healthy).ToDictionary(s => s.Profile.Id, s => s.Mapping.Clone());
            window = IntPtr.Zero;
            process = 0;
            Current = null;
            ActiveMapping = null;
        }

        internal bool Select(IntPtr targetWindow, uint targetProcess, string processName, string windowClass)
        {
            ApplicationProfile profile = ApplicationProfile.Match(processName, windowClass);
            if (targetWindow == IntPtr.Zero || targetProcess == 0) profile = null;
            if (window == targetWindow && process == targetProcess && Current == profile) return false;
            window = targetWindow;
            process = targetProcess;
            Current = profile;
            ApplicationMapping mapping;
            ActiveMapping = profile != null && mappings.TryGetValue(profile.Id, out mapping) ? mapping : null;
            return true;
        }
    }
}
