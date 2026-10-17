using System.Drawing;
using System.IO;

namespace SpaceMouseBridge
{
    internal static class AppTypography
    {
        private static FontFamily family;

        internal static void Initialize()
        {
            if (family != null) return;
            family = new FontFamily("Segoe UI");
            if (family.Name != "Segoe UI" || !family.IsStyleAvailable(FontStyle.Regular) ||
                !family.IsStyleAvailable(FontStyle.Bold))
                throw new InvalidDataException("Die VS-Code-UI-Schrift Segoe UI Regular/Bold ist nicht verfuegbar.");
        }

        internal static Font CreateFont()
        {
            Initialize();
            // VS Code's Windows workbench uses Segoe UI at 13 CSS pixels.
            return new Font(family, 9.75f, FontStyle.Regular, GraphicsUnit.Point);
        }
    }
}
