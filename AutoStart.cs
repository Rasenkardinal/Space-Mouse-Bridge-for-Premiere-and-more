using System;
using System.IO;
using Microsoft.Win32;

namespace SpaceMouseBridge
{
    internal static class AutoStart
    {
        internal const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        internal const string ValueName = "SpaceMouseBridge";

        internal static string Command(string executable)
        {
            if (string.IsNullOrWhiteSpace(executable) || !Path.IsPathRooted(executable) ||
                executable.IndexOf('"') >= 0 || executable.IndexOfAny(new[] { '\r', '\n' }) >= 0)
                throw new InvalidDataException("Autostart benoetigt einen absoluten EXE-Pfad.");
            return "\"" + Path.GetFullPath(executable) + "\" --tray";
        }

        internal static bool IsEnabled(string executable, string keyPath = RunKey)
        {
            using (var key = Registry.CurrentUser.OpenSubKey(keyPath))
            {
                string command = key == null ? null : key.GetValue(ValueName) as string;
                return string.Equals(command, Command(executable), StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(command, "\"" + Path.GetFullPath(executable) + "\"", StringComparison.OrdinalIgnoreCase);
            }
        }

        internal static void SetEnabled(bool enabled, string executable, string keyPath = RunKey)
        {
            string command = Command(executable);
            if (enabled && !File.Exists(executable))
                throw new FileNotFoundException("Die EXE fuer den Autostart wurde nicht gefunden.", executable);
            using (var key = Registry.CurrentUser.CreateSubKey(keyPath))
            {
                if (key == null) throw new IOException("Der Autostart-Schluessel konnte nicht geoeffnet werden.");
                if (enabled) key.SetValue(ValueName, command, RegistryValueKind.String);
                else key.DeleteValue(ValueName, false);
                key.Flush();
                if (enabled ? !string.Equals(key.GetValue(ValueName) as string, command, StringComparison.OrdinalIgnoreCase) :
                    key.GetValue(ValueName) != null)
                    throw new IOException("Die Autostart-Einstellung konnte nicht bestaetigt werden.");
            }
        }
    }
}
