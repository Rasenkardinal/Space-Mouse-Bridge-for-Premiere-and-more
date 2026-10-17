using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SpaceMouseBridge
{
    internal static class ThemeTests
    {
        [DllImport("user32.dll")]
        private static extern bool PrintWindow(IntPtr window, IntPtr dc, uint flags);
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr w, IntPtr l);
        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr window, int message, IntPtr w, IntPtr l);
        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out int value, int size);
        [DllImport("user32.dll")]
        private static extern int GetWindowRgn(IntPtr window, IntPtr region);
        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr window, out Rectangle rect);
        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr window, out Rectangle rect);

        private sealed class RecreatedCombo : ComboBox
        {
            internal void Recreate() { RecreateHandle(); }
        }
        private sealed class ThemeTestForm : ThemedForm
        {
            internal int ThemeChanges;
            protected override void OnAppThemeChanged() { ThemeChanges++; }
            internal void NotifySettingsChanged()
            {
                Message message = Message.Create(Handle, 0x001a, IntPtr.Zero, IntPtr.Zero);
                WndProc(ref message);
            }
        }

        private static void Assert(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("Theme-Test fehlgeschlagen: " + name);
        }

        private static double Luminance(Color color)
        {
            double[] channels = { color.R / 255.0, color.G / 255.0, color.B / 255.0 };
            return channels.Select(v => v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4))
                .Zip(new[] { 0.2126, 0.7152, 0.0722 }, (v, weight) => v * weight).Sum();
        }

        private static void Contrast(Color first, Color second, string name)
        {
            double a = Luminance(first), b = Luminance(second);
            Assert((Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05) >= 4.5, name);
        }

        private static int CountPixels(Bitmap bitmap, Color color)
        {
            int count = 0;
            for (int y = 0; y < bitmap.Height; y++)
            for (int x = 0; x < bitmap.Width; x++)
                if (bitmap.GetPixel(x, y).ToArgb() == color.ToArgb()) count++;
            return count;
        }

        private static Bitmap Capture(Control control)
        {
            var bitmap = new Bitmap(control.Width, control.Height);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                IntPtr dc = graphics.GetHdc();
                try { Assert(PrintWindow(control.Handle, dc, 2), "Windows-Steuerelement renderbar"); }
                finally { graphics.ReleaseHdc(dc); }
            }
            return bitmap;
        }

        private static void NativeControls()
        {
            using (var form = new ThemedForm { Size = new Size(500, 380) })
            using (var combo = new RecreatedCombo { Bounds = new Rectangle(20, 20, 250, 30),
                DropDownStyle = ComboBoxStyle.DropDownList })
            using (var number = new NumericUpDown { Bounds = new Rectangle(20, 60, 120, 30), Value = 12 })
            using (var text = new TextBox { Bounds = new Rectangle(20, 110, 300, 150),
                Multiline = true, ScrollBars = ScrollBars.Vertical, Text = string.Join("\r\n", Enumerable.Range(0, 50)) })
            {
                combo.Items.AddRange(new object[] { "First", "Second" });
                combo.SelectedIndex = 0;
                form.Controls.AddRange(new Control[] { combo, number, text });
                form.Show();
                foreach (ThemePalette palette in new[] { new ThemePalette(true, false), new ThemePalette(false, false),
                    new ThemePalette(true, true), new ThemePalette(true, false) })
                {
                    AppTheme.Current = palette;
                    AppTheme.Apply(form, palette);
                    Application.DoEvents();
                    Assert(form.BackColor == palette.Background && combo.SelectedIndex == 0 && number.Value == 12,
                        "Themewechsel mit lebenden Handles behaelt Palette und Werte");
                    foreach (Control field in new Control[] { combo, number })
                    {
                        Assert(palette.HighContrast || field is NumericUpDown ? field.Region == null :
                            field.Region != null && !field.Region.IsVisible(0, 0) &&
                            field.Region.IsVisible(field.Width / 2, field.Height / 2),
                            "Zahlenfelder und Selektoren abgerundet; Kontrastmodus rechteckig");
                        field.Width += 12;
                        Assert(palette.HighContrast || field is NumericUpDown || field.Region.IsVisible(field.Width - 2, field.Height / 2),
                            "Rundung folgt Groessenaenderungen ohne Inhalt abzuschneiden");
                    }
                    foreach (Control child in number.Controls)
                        Assert(palette.HighContrast ? child.Region == null : child.Region != null,
                            "Zahlenfeld-Kinder lassen den runden Aussenrahmen frei");
                    if (!palette.HighContrast)
                        using (var bitmap = new Bitmap(number.Width, number.Height))
                        {
                            number.DrawToBitmap(bitmap, number.ClientRectangle);
                            Assert(Math.Abs(bitmap.GetPixel(number.Width / 2, 0).R - palette.Input.R) >= 8,
                                "Zahlenfeld zeigt den eigenen Aussenrahmen statt des nativen Rechtecks");
                            Assert(bitmap.GetPixel(number.Width - 2, 1).ToArgb() == form.BackColor.ToArgb(),
                                "Zahlenfeld-Rahmen folgt der Rundung statt der rechteckigen Ecke");
                        }
                    Assert(combo.DrawMode == (palette.Dark ? DrawMode.OwnerDrawFixed : DrawMode.Normal) &&
                        number.BorderStyle == (palette.HighContrast ? BorderStyle.Fixed3D : BorderStyle.FixedSingle),
                        "Native und eigene Darstellung nach Hell-/Dunkel-/Kontrastwechsel");
                    if (!palette.Dark) continue;
                    using (Bitmap bitmap = Capture(combo))
                        Assert(bitmap.GetPixel(combo.Width - 5, 4).GetBrightness() < 0.4,
                            "Dropdown-Pfeilflaeche tatsaechlich dunkel gerendert");
                    Control spinner = number.Controls.Cast<Control>().Single(c => c.GetType().Name == "UpDownButtons");
                    using (Bitmap bitmap = Capture(spinner))
                        Assert(bitmap.GetPixel(3, 3).GetBrightness() < 0.4, "Zahlenfeld-Pfeilflaeche dunkel gerendert");
                    if (!palette.HighContrast)
                    {
                        number.Focus();
                        Application.DoEvents();
                        using (var bitmap = new Bitmap(number.Width, number.Height))
                        {
                            number.DrawToBitmap(bitmap, number.ClientRectangle);
                            Color edge = bitmap.GetPixel(number.Width / 2, 0);
                            Assert(edge.B > edge.R && edge.G > edge.R,
                                "Fokussiertes Zahlenfeld zeigt blauen Rahmen wie Entwurf A");
                            Assert(edge == bitmap.GetPixel(10, 0),
                                "Gerader Zahlenfeldrahmen hat einheitliche Farbe");
                        }
                        number.Enabled = false;
                        Assert(number.ForeColor != palette.Text && number.Value == 12,
                            "Deaktiviertes Zahlenfeld zeigt gedaempfte Ziffern ohne Wertaenderung");
                        number.Enabled = true;
                        Assert(number.ForeColor == palette.Text, "Aktivierung stellt Ziffernfarbe wieder her");
                        combo.Focus();
                    }
                    using (Bitmap bitmap = Capture(form))
                        Assert(bitmap.GetPixel(form.Width / 2, 15).GetBrightness() < 0.4,
                            "Titelleistenflaeche tatsaechlich dunkel gerendert");
                    using (Bitmap bitmap = Capture(text))
                        Assert(bitmap.GetPixel(text.Width - 4, 20).GetBrightness() < 0.4,
                            "Nativer Scrollbalken tatsaechlich dunkel gerendert");
                    combo.Recreate();
                    using (Bitmap bitmap = Capture(combo))
                        Assert(bitmap.GetPixel(combo.Width - 5, 4).GetBrightness() < 0.4,
                            "Neu erstelltes Dropdown-Handle bleibt dunkel");
                    Control editor = number.Controls.Cast<Control>().Single(c => c is TextBox);
                    Assert(((TextBox)editor).BorderStyle == BorderStyle.None, "Zahlenfeld ohne doppelten inneren Rahmen");
                    SendMessage(editor.Handle, 0x0100, new IntPtr(0x26), IntPtr.Zero);
                    SendMessage(editor.Handle, 0x0101, new IntPtr(0x26), IntPtr.Zero);
                    Assert(number.Value == 13, "Zahlenfeld-Pfeiltaste funktioniert trotz eigener Darstellung");
                    number.Value = 12;
                    var upperArrow = new IntPtr(((spinner.Height / 4) << 16) | (spinner.Width / 2));
                    SendMessage(spinner.Handle, 0x0201, new IntPtr(1), upperArrow);
                    SendMessage(spinner.Handle, 0x0202, IntPtr.Zero, upperArrow);
                    Assert(number.Value == 13, "Zahlenfeld-Pfeilklick funktioniert trotz eigener Darstellung");
                    var lowerArrow = new IntPtr(((spinner.Height * 3 / 4) << 16) | (spinner.Width / 2));
                    SendMessage(spinner.Handle, 0x0201, new IntPtr(1), lowerArrow);
                    SendMessage(spinner.Handle, 0x0202, IntPtr.Zero, lowerArrow);
                    Assert(number.Value == 12, "Unterer Zahlenfeld-Pfeil verringert den Wert");
                    number.Value = 12;
                    combo.SelectedIndex = 1;
                    Assert(combo.SelectedIndex == 1, "Dropdown-Auswahl bleibt bedienbar");
                    combo.SelectedIndex = 0;
                    combo.DroppedDown = true;
                    Application.DoEvents();
                    Assert(combo.DroppedDown, "Dunkle Auswahlliste oeffnet");
                    Rectangle popupBounds, popupClient;
                    IntPtr popup = NativeTheme.DropDownHandle(combo);
                    Assert(GetWindowRect(popup, out popupBounds), "Fenstergroesse der aufgeklappten Liste lesbar");
                    Assert(GetClientRect(popup, out popupClient), "Inhaltsgroesse der aufgeklappten Liste lesbar");
                    if (!palette.HighContrast)
                    {
                        Assert(popupClient.Height >= combo.Items.Count * combo.ItemHeight,
                            "Innenabstand schneidet keine Auswahlzeile ab");
                        Assert(popupBounds.Width - popupBounds.X - popupClient.Width == 2 &&
                            popupBounds.Height - popupBounds.Y - popupClient.Height == 2,
                            "Native Listengroesse bleibt ohne kuenstliche Innenabstaende erhalten");
                    }
                    using (var empty = new Region(new Rectangle(0, 0, 1, 1)))
                    using (var graphics = combo.CreateGraphics())
                    {
                        IntPtr region = empty.GetHrgn(graphics);
                        try
                        {
                            int kind = GetWindowRgn(NativeTheme.DropDownHandle(combo), region);
                            Assert(palette.HighContrast ? kind == 0 : kind == 3,
                                "Aufgeklappte Liste hat echte runde Fensterregion; Kontrastmodus bleibt nativ");
                            if (!palette.HighContrast)
                                using (var shape = Region.FromHrgn(region))
                                    Assert(!shape.IsVisible(0, 0) && shape.IsVisible(10, 10),
                                        "Auswahlliste spart Ecken aus und behaelt Eintraege sichtbar");
                        }
                        finally { empty.ReleaseHrgn(region); }
                    }
                    SendMessage(combo.Handle, 0x0100, new IntPtr(0x28), IntPtr.Zero);
                    SendMessage(combo.Handle, 0x0101, new IntPtr(0x28), IntPtr.Zero);
                    Assert(combo.SelectedIndex == 1, "Gerundete Auswahlliste bleibt per Pfeiltaste bedienbar");
                    combo.DroppedDown = false;
                    combo.SelectedIndex = 0;
                    combo.DroppedDown = true;
                    Application.DoEvents();
                    Rectangle reopened;
                    Assert(GetWindowRect(NativeTheme.DropDownHandle(combo), out reopened) &&
                        reopened.Height - reopened.Y == popupBounds.Height - popupBounds.Y,
                        "Wiederholtes Oeffnen vergroessert das Dropdown nicht");
                    combo.DroppedDown = false;
                    if (!palette.HighContrast)
                    {
                        for (int i = 2; i < 30; i++) combo.Items.Add("Item " + i);
                        combo.DroppedDown = true;
                        Application.DoEvents();
                        SendMessage(combo.Handle, 0x0100, new IntPtr(0x23), IntPtr.Zero);
                        SendMessage(combo.Handle, 0x0101, new IntPtr(0x23), IntPtr.Zero);
                        Assert(combo.SelectedIndex == 29, "Lange gerundete Liste kann bis zum letzten Eintrag navigieren");
                        combo.DroppedDown = false;
                        while (combo.Items.Count > 2) combo.Items.RemoveAt(combo.Items.Count - 1);
                        combo.SelectedIndex = 0;
                    }
                }
            }
        }

        private static void RoundedAreas()
        {
            using (var form = new ThemedForm { Size = new Size(400, 260) })
            using (var text = new TextBox { Text = "unchanged", Multiline = true, ScrollBars = ScrollBars.Vertical })
            using (var surface = RoundedSurface.Wrap(text))
            {
                form.Controls.Add(surface);
                form.Show();
                foreach (bool highContrast in new[] { false, true, false })
                {
                    var palette = new ThemePalette(true, highContrast);
                    AppTheme.Apply(form, palette);
                    if (Environment.OSVersion.Version.Build >= 22000)
                    {
                        int preference;
                        int result = DwmGetWindowAttribute(form.Handle, 33, out preference, sizeof(int));
                        Assert(result == 0 && preference == (highContrast ? 0 : 2),
                            "Windows-Fensterrundung explizit gesetzt und im Kontrastmodus zurueckgesetzt");
                    }
                    Assert(surface.CornerRadius == (highContrast ? 0 : 8), "Bereichsradius und Kontrastmodus");
                    Assert(text.Text == "unchanged" && text.Dock == DockStyle.Fill &&
                        text.Bounds.Left >= 12 && text.Bounds.Right <= surface.Width - 12,
                        "Rundung laesst Inhalt und Scrollbalken innerhalb der Innenabstaende");
                    using (var path = RoundedSurface.Outline(new RectangleF(0, 0, 100, 60), surface.CornerRadius))
                        Assert(path.IsVisible(50, 30) && (highContrast || !path.IsVisible(0, 0)),
                            "Bereichspfad hat echte gerundete Ecken");
                    using (var bitmap = new Bitmap(surface.Width, surface.Height))
                    {
                        surface.DrawToBitmap(bitmap, surface.ClientRectangle);
                        if (!highContrast)
                            Assert(bitmap.GetPixel(0, 0).ToArgb() == palette.Background.ToArgb() &&
                                bitmap.GetPixel(4, surface.Height / 2).ToArgb() == palette.Surface.ToArgb(),
                                "Abgerundeter Bereich tatsaechlich mit ausgesparten Ecken gerendert");
                    }
                    form.Size = new Size(form.Width + 20, form.Height + 10);
                    Application.DoEvents();
                    Assert(text.Bounds.Right <= surface.Width - 12 && form.Region == null,
                        "Resize erhaelt Innenabstand und native Fenstergroessenaenderung");
                }
            }
        }

        private static void DropDownItems()
        {
            using (var form = new ThemedForm { Size = new Size(500, 400) })
            using (var combo = new ComboBox { Bounds = new Rectangle(20, 20, 300, 30),
                DropDownStyle = ComboBoxStyle.DropDownList })
            {
                form.Controls.Add(combo);
                form.Show();
                AppTheme.Apply(form, new ThemePalette(true, false));
                foreach (int count in new[] { 2, 3, 30 })
                {
                    combo.Items.Clear();
                    combo.Items.AddRange(Enumerable.Range(0, count).Select(i => (object)("Programm " + i)).ToArray());
                    foreach (int selected in new[] { 0, count - 1, 1, count - 1, 0 })
                    {
                        combo.SelectedIndex = selected;
                        combo.DroppedDown = true;
                        Application.DoEvents();
                        Rectangle client;
                        Assert(GetClientRect(NativeTheme.DropDownHandle(combo), out client),
                            "Inhaltsbereich der Programmauswahl lesbar");
                        int visible = client.Height / combo.ItemHeight;
                        int top = SendMessage(combo.Handle, 0x015b, IntPtr.Zero, IntPtr.Zero).ToInt32();
                        Assert(combo.SelectedIndex == selected && top <= Math.Max(0, count - visible) &&
                            selected >= top && selected < top + visible,
                            "Oeffnen behaelt Auswahl und fuellt die sichtbaren Zeilen ohne fehlende Eintraege");
                        if (count <= 3)
                            Assert(visible >= count && top == 0,
                                "Kurze Programmauswahl zeigt immer alle Eintraege ab der ersten Zeile");
                        combo.DroppedDown = false;
                    }
                }
            }
        }

        private static void MappingComboAppearance()
        {
            using (var form = new ThemedForm { Size = new Size(720, 220) })
            using (var grid = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false,
                RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill })
            {
                foreach (string name in new[] { "Ausgabe / Funktion", "Negative Richtung", "Positive Richtung" })
                {
                    var column = new DataGridViewComboBoxColumn { HeaderText = name, FlatStyle = FlatStyle.Flat };
                    column.Items.AddRange("Keine Aktion", "Scrollen", "Zoom");
                    grid.Columns.Add(column);
                }
                grid.Rows.Add("Scrollen", "Keine Aktion", "Zoom");
                form.Controls.Add(grid);
                form.Show();
                foreach (RightToLeft direction in new[] { RightToLeft.No, RightToLeft.Yes })
                {
                    grid.RightToLeft = direction;
                    AppTheme.Apply(form, new ThemePalette(true, false));
                    foreach (bool readOnly in new[] { false, true })
                    foreach (bool selected in new[] { false, true })
                    {
                        grid.ReadOnly = readOnly;
                        grid.ClearSelection();
                        grid.Rows[0].Selected = selected;
                        Application.DoEvents();
                        using (var bitmap = new Bitmap(grid.Width, grid.Height))
                        {
                            grid.DrawToBitmap(bitmap, grid.ClientRectangle);
                            for (int column = 0; column < 3; column++)
                            {
                                Rectangle cell = grid.GetCellDisplayRectangle(column, 0, false);
                                int width = SystemInformation.VerticalScrollBarWidth + 8;
                                int left = direction == RightToLeft.Yes ? cell.Left + 2 : cell.Right - width;
                                int bright = 0;
                                for (int y = cell.Top + 3; y < cell.Bottom - 3; y++)
                                for (int x = left; x < left + width - 2; x++)
                                {
                                    Color pixel = bitmap.GetPixel(x, y);
                                    if (pixel.R >= 245 && pixel.G >= 245 && pixel.B >= 245) bright++;
                                }
                                Assert(bright == 0, "Mapping-Auswahl hat keine weissen Randpixel: " + bright);
                            }
                        }
                    }
                    grid.ReadOnly = false;
                    for (int column = 0; column < 3; column++)
                    {
                        grid.CurrentCell = grid.Rows[0].Cells[column];
                        Assert(grid.BeginEdit(false), "Mapping-Auswahlzelle bleibt editierbar");
                        var editor = grid.EditingControl as ComboBox;
                        Assert(editor != null, "Mapping-Auswahl verwendet weiterhin den nativen Combo-Editor");
                        Application.DoEvents();
                        using (Bitmap bitmap = Capture(editor))
                            AssertNoWhiteEdges(bitmap, direction, "Aktiver Tabelleneditor " + column);
                        SendMessage(editor.Handle, 0x0100, new IntPtr(0x28), IntPtr.Zero);
                        SendMessage(editor.Handle, 0x0101, new IntPtr(0x28), IntPtr.Zero);
                        Assert(grid.EndEdit(), "Mapping-Auswahl kann weiterhin uebernommen werden");
                    }
                }
            }
        }

        private static void ComboFieldEdges()
        {
            using (var form = new ThemedForm { Size = new Size(640, 300) })
            using (var combo = new RecreatedCombo { Bounds = new Rectangle(20, 20, 280, 34),
                DropDownStyle = ComboBoxStyle.DropDownList })
            using (var other = new TextBox { Bounds = new Rectangle(20, 100, 200, 30) })
            {
                combo.Items.AddRange(new object[] { "Google Chrome", "Funktion", "Kontinuierlich wiederholen" });
                combo.SelectedIndex = 0;
                form.Controls.AddRange(new Control[] { combo, other });
                form.Show();
                foreach (FlatStyle style in new[] { FlatStyle.Flat, FlatStyle.Standard })
                foreach (RightToLeft direction in new[] { RightToLeft.No, RightToLeft.Yes })
                foreach (int width in new[] { 72, 230, 280 })
                {
                    AppTheme.Apply(form, new ThemePalette(true, false));
                    combo.FlatStyle = style;
                    combo.RightToLeft = direction;
                    combo.Width = width;
                    combo.Recreate();
                    foreach (int state in new[] { 0, 1, 2, 3 })
                    {
                        combo.Enabled = state != 3;
                        if (state == 1) combo.Focus(); else other.Focus();
                        var point = new IntPtr(((combo.Height / 2) << 16) | (combo.Width - 8));
                        SendMessage(combo.Handle, state == 2 ? 0x0200 : 0x02a3, IntPtr.Zero, point);
                        Application.DoEvents();
                        using (Bitmap bitmap = Capture(combo))
                            AssertNoWhiteEdges(bitmap, direction, "Auswahlfeld " + style + "/" + width + "/" + state);
                        using (var bitmap = new Bitmap(combo.Width, combo.Height))
                        {
                            combo.DrawToBitmap(bitmap, combo.ClientRectangle);
                            AssertNoWhiteEdges(bitmap, direction, "Bitmap-Auswahlfeld " + style + "/" + width + "/" + state);
                        }
                    }
                    combo.Enabled = true;
                }
            }
        }

        private static void AssertNoWhiteEdges(Bitmap bitmap, RightToLeft direction, string name)
        {
            int bright = 0;
            for (int y = 1; y < bitmap.Height - 1; y++)
            for (int offset = 1; offset < Math.Min(26, bitmap.Width); offset++)
            {
                int x = direction == RightToLeft.Yes ? offset : bitmap.Width - 1 - offset;
                Color pixel = bitmap.GetPixel(x, y);
                if (pixel.R >= 245 && pixel.G >= 245 && pixel.B >= 245) bright++;
            }
            Assert(bright == 0, name + " ohne weisse Randreste: " + bright);
        }

        private static void AllInputFields()
        {
            string temporary = Path.Combine(Path.GetTempPath(), "SpaceMouseBridge-Fields-" + Guid.NewGuid().ToString("N"));
            var chrome = new MappingStore(temporary, Path.Combine(temporary, "Chrome.xml"));
            var premiere = new MappingStore(temporary, null, ApplicationProfile.Premiere);
            var mapping = ApplicationProfile.Premiere.EmptyMapping();
            mapping.Jog = JogSettings.Default();
            using (var diagnostic = new DiagnosticWindow(1, null))
            using (var host = new ThemedForm { Size = diagnostic.Size, MinimumSize = diagnostic.MinimumSize })
            using (var browserEditor = new MappingEditor(chrome, ApplicationMapping.Default()))
            using (var jogEditor = new PremiereEditor(premiere, mapping))
            using (var mappingEditor = new PremiereMappingEditor(premiere, mapping))
            using (var menuEditor = new PremiereMenuEditor(null))
            {
                host.Controls.Add(diagnostic.Controls[0]);
                int fields = 0;
                foreach (Form form in new Form[] { host, browserEditor, jogEditor, mappingEditor, menuEditor })
                {
                    form.Show();
                    Application.DoEvents();
                    CheckInputFields(form, ref fields);
                    form.Hide();
                }
                Assert(fields >= 25, "Alle Eingabefeldarten aus Hauptfenster und Editoren geprueft: " + fields);
            }
            Assert(!Directory.Exists(temporary), "Darstellungstest schreibt keine Profile oder Einstellungen");
        }

        private static void CheckInputFields(Control parent, ref int fields)
        {
            foreach (Control control in parent.Controls)
            {
                if (control is ComboBox || control is NumericUpDown ||
                    control is TextBoxBase && !(control.Parent is NumericUpDown))
                {
                    Assert(control.BackColor == AppTheme.Current.Input, "Alle Eingaben verwenden dieselbe dunkle Flaeche");
                    using (Bitmap bitmap = Capture(control))
                        AssertNoWhiteEdges(bitmap, control.RightToLeft, control.GetType().Name + ": " + control.Text);
                    fields++;
                }
                CheckInputFields(control, ref fields);
            }
        }

        private static void ProfileDropDown()
        {
            using (var diagnostic = new DiagnosticWindow(1, null))
            using (var form = new ThemedForm { Size = diagnostic.Size })
            using (var timer = new Timer { Interval = 100 })
            {
                form.Controls.Add(diagnostic.Controls[0]);
                form.Show();
                var combo = (ComboBox)typeof(DiagnosticWindow).GetField("profileSelector",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(diagnostic);
                foreach (int selected in new[] { 0, 1, 0, 1 })
                {
                    combo.SelectedIndex = selected;
                    bool inspected = false;
                    int top = -1, visible = 0, paintedRows = 0;
                    EventHandler inspect = delegate {
                        if (!combo.DroppedDown) return;
                        timer.Stop();
                        Rectangle client;
                        Assert(GetClientRect(NativeTheme.DropDownHandle(combo), out client),
                            "Echte Profilliste hat einen lesbaren Inhaltsbereich");
                        visible = client.Height / combo.ItemHeight;
                        top = SendMessage(combo.Handle, 0x015b, IntPtr.Zero, IntPtr.Zero).ToInt32();
                        Rectangle window;
                        Assert(GetWindowRect(NativeTheme.DropDownHandle(combo), out window),
                            "Fenstergroesse der echten Profilliste lesbar");
                        using (var bitmap = new Bitmap(window.Width - window.X, window.Height - window.Y))
                        using (Graphics graphics = Graphics.FromImage(bitmap))
                        {
                            IntPtr dc = graphics.GetHdc();
                            try { Assert(PrintWindow(NativeTheme.DropDownHandle(combo), dc, 0),
                                "Echte Profilliste kann gerendert werden"); }
                            finally { graphics.ReleaseHdc(dc); }
                            AssertNoWhiteEdges(bitmap, combo.RightToLeft, "Aufgeklappte Profilauswahl");
                            for (int row = 0; row < 2; row++)
                            {
                                int textPixels = 0;
                                for (int y = 1 + row * combo.ItemHeight; y < 1 + (row + 1) * combo.ItemHeight; y++)
                                for (int x = 10; x < Math.Min(200, bitmap.Width - 10); x++)
                                    if (bitmap.GetPixel(x, y).GetBrightness() > 0.55f) textPixels++;
                                if (textPixels > 20) paintedRows++;
                            }
                        }
                        inspected = true;
                        combo.DroppedDown = false;
                    };
                    timer.Tick += inspect;
                    timer.Start();
                    try
                    {
                        int point = ((combo.Height / 2) << 16) | (combo.Width - 8);
                        Assert(PostMessage(combo.Handle, 0x0201, new IntPtr(1), new IntPtr(point)) &&
                            PostMessage(combo.Handle, 0x0202, IntPtr.Zero, new IntPtr(point)),
                            "Mausklick auf echte Profilauswahl zugestellt");
                        DateTime deadline = DateTime.UtcNow.AddSeconds(2);
                        while (!inspected && DateTime.UtcNow < deadline)
                        {
                            Application.DoEvents();
                            System.Threading.Thread.Sleep(10);
                        }
                        Assert(inspected && visible >= 2 && top == 0 && paintedRows == 2 &&
                            combo.SelectedIndex == selected,
                            "Per Maus geoeffnete Profilauswahl zeichnet beide Profile: top=" + top +
                            ", sichtbar=" + visible + ", gezeichnet=" + paintedRows);
                    }
                    finally { timer.Stop(); timer.Tick -= inspect; }
                }
            }
        }

        private static void DiagnosticDisclosure()
        {
            using (var diagnostic = new DiagnosticWindow(1, null))
            using (var host = new ThemedForm { Size = diagnostic.Size, MinimumSize = diagnostic.MinimumSize })
            {
                var layout = (TableLayoutPanel)diagnostic.Controls[0];
                host.Controls.Add(layout);
                host.Show();
                int compactHeight = host.Height;
                var log = layout.Controls[9].Controls[0];
                int compactLogHeight = log.Height;
                host.Height = 900;
                Assert(Math.Abs(compactLogHeight * 2 - log.Height) <= 1,
                    "Ausgabefeld ist bei Standardgroesse exakt halb so hoch wie zuvor");
                host.Height = compactHeight;
                var toggle = layout.Controls.OfType<RoundedButton>().Single();
                foreach (bool expanded in new[] { false, true, false, true })
                {
                    if (expanded != (layout.RowStyles[2].Height > 0)) toggle.PerformClick();
                    Assert(host.Height == (expanded ? 900 : compactHeight),
                        "Hauptfenster wird kompakter und bietet aufgeklappt weiterhin Platz fuer die Diagnose");
                    for (int row = 2; row <= 7; row++)
                    {
                        Assert(expanded ? layout.RowStyles[row].Height > 0 : layout.RowStyles[row].Height == 0,
                            "Erweiterte Diagnose klappt alle sechs Rohdatenbereiche gemeinsam auf und zu");
                        Assert(layout.Controls[row].Visible == expanded,
                            "Diagnosefelder folgen dem aufgeklappten Zustand");
                    }
                    var actionSurface = layout.Controls[10];
                    var actions = actionSurface.Controls[0];
                    var actionTable = (TableLayoutPanel)actions;
                    var autoStart = (CheckBox)typeof(DiagnosticWindow).GetField("autoStart",
                        BindingFlags.Instance | BindingFlags.NonPublic).GetValue(diagnostic);
                    Assert(actionTable.Controls.Count == 4 &&
                        actionTable.Controls.OfType<RoundedButton>().All(button => actionTable.GetRow(button) == 0) &&
                        actionTable.GetRow(autoStart) == 0 && actionTable.GetColumn(autoStart) == 1 &&
                        actionTable.GetColumnSpan(actionTable.Controls.OfType<CheckBox>().Single(check => check != autoStart)) == 3,
                        "Autostart steht mittig zwischen beiden Buttons, Bridge-Ausgabe darunter");
                    var leftButton = actionTable.GetControlFromPosition(0, 0);
                    var rightButton = actionTable.GetControlFromPosition(2, 0);
                    Assert(autoStart.Left >= leftButton.Right && autoStart.Right <= rightButton.Left &&
                        Math.Abs(autoStart.Left + autoStart.Width / 2 - (leftButton.Right + rightButton.Left) / 2) <= 2,
                        "Autostart ist im freien Bereich zwischen den Buttons zentriert");
                    Assert(actionSurface.Visible && actionSurface.Height >= 90 &&
                        actionSurface.Top >= layout.Controls[9].Bottom &&
                        actionSurface.Bottom <= layout.ClientSize.Height - layout.Padding.Bottom,
                        "Aktionsbereich bleibt sichtbar in seiner eigenen Zeile innerhalb des Fensters");
                    Assert(actions.Controls.OfType<RoundedButton>().All(button => button.Visible) &&
                        actions.Controls.OfType<CheckBox>().All(check => check.Visible),
                        "Mappingbuttons, Bridge-Ausgabe und Autostart bleiben bei beiden Diagnosezustaenden sichtbar");
                    Assert(actions.Controls.Cast<Control>().All(control => actions.ClientRectangle.Contains(control.Bounds)),
                        "Aktionen samt Autostart werden an keiner Seite abgeschnitten");
                    foreach (CheckBox check in actions.Controls.OfType<CheckBox>())
                        using (Graphics graphics = check.CreateGraphics())
                        {
                            int glyph = Math.Max(16, CheckBoxRenderer.GetGlyphSize(graphics,
                                System.Windows.Forms.VisualStyles.CheckBoxState.CheckedNormal).Width);
                            int text = TextRenderer.MeasureText(graphics, check.Text, check.Font, Size.Empty,
                                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;
                            Assert(check.Width >= glyph + 8 + text + check.Padding.Horizontal,
                                "Beschriftungen von Bridge-Ausgabe und Autostart passen ohne Auslassungspunkte");
                        }
                }
                Assert(layout.GetPositionFromControl(layout.Controls[0]).Row == 0,
                    "Profilwahl bleibt ausserhalb der erweiterten Diagnose");
                toggle.PerformClick();
                host.Size = host.MinimumSize;
                var minimumActions = layout.Controls[10].Controls[0];
                Assert(minimumActions.Controls.Cast<Control>().All(control =>
                    minimumActions.ClientRectangle.Contains(control.Bounds)),
                    "Alle Aktionen bleiben auch bei kompakter Mindestgroesse vollstaendig sichtbar");
                var controls = minimumActions.Controls.Cast<Control>().ToArray();
                for (int i = 0; i < controls.Length; i++)
                for (int j = i + 1; j < controls.Length; j++)
                    Assert(!controls[i].Bounds.IntersectsWith(controls[j].Bounds),
                        "Aktionen ueberlappen auch bei Mindestgroesse nicht");
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(DiagnosticWindow).GetField("ready", flags).SetValue(diagnostic, true);
                var update = typeof(DiagnosticWindow).GetMethod("UpdateDisplayValues", flags);
                update.Invoke(diagnostic, null);
                var heading = (Label)layout.Controls[8];
                Assert(heading.Text == "AUSGABE  /  EREIGNISSE" && heading.ForeColor == AppTheme.Current.Text,
                    "Normalbetrieb ohne zusaetzlichen gruenen Erfolgsstatus");
                typeof(DiagnosticWindow).GetField("ready", flags).SetValue(diagnostic, false);
                update.Invoke(diagnostic, null);
                Assert(heading.Text.Contains("Raw Input nicht aktiv") && heading.ForeColor == AppTheme.Current.Error,
                    "Fehlender Raw Input bleibt als Warnhinweis sichtbar");
            }
        }

        private static void DiagnosticRefresh()
        {
            using (var form = new DiagnosticWindow(0, null))
            {
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var reader = (InputReader)typeof(DiagnosticWindow).GetField("reader", flags).GetValue(form);
                var combo = (ComboBox)typeof(DiagnosticWindow).GetField("devices", flags).GetValue(form);
                var update = typeof(DiagnosticWindow).GetMethod("UpdateDisplay", flags);
                var navigation = new Device { Vendor = 0x256f, Product = 0xc652 };
                var auxiliary = new Device { Vendor = 0x256f, Product = 0xc652 };
                reader.Devices.Add(new IntPtr(1), navigation);
                reader.Devices.Add(new IntPtr(2), auxiliary);
                try
                {
                    form.CreateControl();
                    form.Controls[0].Controls.OfType<RoundedButton>().Single().PerformClick();
                    var profilePanel = form.Controls[0].Controls[0].Controls[0];
                    var selection = profilePanel.Controls.OfType<FlowLayoutPanel>().Single();
                    var profileLabel = selection.Controls.OfType<Label>().Single();
                    foreach (bool dark in new[] { true, false })
                    {
                        var palette = new ThemePalette(dark, false);
                        AppTheme.Apply(form, palette);
                        Assert(profileLabel.BackColor == selection.BackColor &&
                            selection.BackColor == profilePanel.BackColor,
                            "Profil bearbeiten hat denselben Hintergrund wie der Profilkopf");
                    }
                    update.Invoke(form, null);
                    combo.SelectedIndex = 1;
                    int layouts = 0, selections = 0;
                    form.Controls[0].Layout += delegate { layouts++; };
                    combo.SelectedIndexChanged += delegate { selections++; };
                    for (int i = 0; i < 30; i++)
                    {
                        auxiliary.Reports++;
                        auxiliary.LastReport = DateTime.UtcNow;
                        auxiliary.LastHex = i.ToString();
                        update.Invoke(form, null);
                    }
                    Assert(layouts == 0, "Laufende Diagnosewerte loesen kein Layout der Geraeteauswahl aus: " + layouts);
                    Assert(selections == 0 && combo.SelectedIndex == 1 && combo.Items.Count == 2,
                        "Unveraenderte Geraeteauswahl bleibt beim Aktualisieren stabil");
                    int width = combo.Width;
                    form.Width += 100;
                    Assert(combo.Width > width, "Fenstergroesse passt Geraeteauswahl weiterhin an");
                    reader.Devices.Remove(new IntPtr(1));
                    update.Invoke(form, null);
                    Assert(combo.Items.Count == 1 && combo.SelectedIndex == 0, "Auswahl nach Entfernen anderer Collection erhalten");
                    reader.Devices.Add(new IntPtr(3), navigation);
                    update.Invoke(form, null);
                    var handles = (System.Collections.Generic.List<IntPtr>)typeof(DiagnosticWindow)
                        .GetField("handles", flags).GetValue(form);
                    Assert(combo.Items.Count == 2 && handles[combo.SelectedIndex] == new IntPtr(2),
                        "Neue Collection ohne Auswahlverlust hinzugefuegt");
                    reader.Devices.Clear();
                    update.Invoke(form, null);
                    Assert(combo.Items.Count == 0 && combo.SelectedIndex == -1, "Entfernte Geraete verschwinden weiterhin");
                }
                finally
                {
                    reader.Dispose();
                    navigation.Dispose();
                    auxiliary.Dispose();
                }
            }
        }

        private static void AppIcon()
        {
            using (var stream = typeof(ThemedForm).Assembly.GetManifestResourceStream("SpaceMouseBridge.OrbitCap.ico"))
            using (var reader = new BinaryReader(stream))
            {
                int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
                Assert(reader.ReadUInt16() == 0 && reader.ReadUInt16() == 1 &&
                    reader.ReadUInt16() == sizes.Length, "Mehrstufiges Windows-App-Icon eingebettet");
                for (int i = 0; i < sizes.Length; i++)
                {
                    stream.Position = 6 + i * 16;
                    int width = reader.ReadByte(), height = reader.ReadByte();
                    reader.ReadUInt16();
                    Assert(reader.ReadUInt16() == 1 && reader.ReadUInt16() == 32, "32-Bit-Icon mit Alphakanal");
                    int length = reader.ReadInt32(), offset = reader.ReadInt32();
                    Assert((width == 0 ? 256 : width) == sizes[i] && width == height &&
                        offset >= 6 + sizes.Length * 16 && offset + length <= stream.Length,
                        "Icongroesse und Bildgrenzen gueltig");
                    stream.Position = offset;
                    using (var imageStream = new MemoryStream(reader.ReadBytes(length)))
                    using (var bitmap = new Bitmap(imageStream))
                    {
                        Assert(bitmap.Width == sizes[i] && bitmap.Height == sizes[i] &&
                            bitmap.GetPixel(0, 0).A == 0 &&
                            bitmap.GetPixel(sizes[i] / 2, sizes[i] / 2).A > 200,
                            "Alle Iconbilder dekodierbar, transparente Ecken und sichtbare Kappe");
                    }
                }
            }
            using (var form = new ThemedForm())
            using (var icon = Icon.ExtractAssociatedIcon(typeof(ThemedForm).Assembly.Location))
                Assert(form.Icon != null && form.Icon.Handle != IntPtr.Zero && icon != null,
                    "App-Fenster und EXE besitzen ein Windows-Icon");
        }

        private static void AutoStartRegistration()
        {
            string keyPath = @"Software\SpaceMouseBridge\Tests\Autostart-" + Guid.NewGuid().ToString("N");
            string executable = typeof(ThemedForm).Assembly.Location;
            try
            {
                Assert(!AutoStart.IsEnabled(executable, keyPath), "Autostart standardmaessig nicht aktiviert");
                AutoStart.SetEnabled(true, executable, keyPath);
                Assert(AutoStart.IsEnabled(executable, keyPath), "Autostart aktivierbar und persistent lesbar");
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(keyPath, true))
                {
                    Assert((string)key.GetValue(AutoStart.ValueName) == "\"" + executable + "\" --tray",
                        "EXE-Pfad fuer Leerzeichen vollstaendig in Anfuehrungszeichen");
                    key.SetValue("Unrelated", "unveraendert");
                    key.SetValue(AutoStart.ValueName, "\"" + executable + "\"");
                    Assert(AutoStart.IsEnabled(executable, keyPath), "Bisherigen Autostart ohne Tray-Argument erkennen");
                    key.SetValue(AutoStart.ValueName, "\"C:\\Andere Bridge\\SpaceMouseBridge.exe\"");
                }
                Assert(!AutoStart.IsEnabled(executable, keyPath), "Andere EXE-Kopie nicht als diese Installation anzeigen");
                AutoStart.SetEnabled(true, executable, keyPath);
                AutoStart.SetEnabled(true, executable, keyPath);
                AutoStart.SetEnabled(false, executable, keyPath);
                AutoStart.SetEnabled(false, executable, keyPath);
                Assert(!AutoStart.IsEnabled(executable, keyPath), "Wiederholtes Aktivieren/Deaktivieren idempotent");
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(keyPath))
                    Assert((string)key.GetValue("Unrelated") == "unveraendert" && key.GetValue(AutoStart.ValueName) == null,
                        "Nur eigener Autostartwert geloescht");
                bool rejected = false;
                try { AutoStart.Command("relative.exe"); }
                catch (InvalidDataException) { rejected = true; }
                Assert(rejected, "Relative Autostartpfade abgewiesen");
                rejected = false;
                try { AutoStart.SetEnabled(true, Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".exe"), keyPath); }
                catch (FileNotFoundException) { rejected = true; }
                Assert(rejected, "Fehlende EXE nicht als Autostart registriert");
                using (var window = new DiagnosticWindow(1, null))
                {
                    var checkbox = (CheckBox)typeof(DiagnosticWindow).GetField("autoStart",
                        BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window);
                    Assert(!checkbox.Enabled && !checkbox.Checked, "Diagnosemodus aendert keinen Autostart");
                }
            }
            finally { Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(keyPath, false); }
        }

        private static void TrayLifecycle()
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            using (var window = new DiagnosticWindow(0, null, true))
            {
                var tray = (NotifyIcon)typeof(DiagnosticWindow).GetField("trayIcon", flags).GetValue(window);
                var menu = (ContextMenuStrip)typeof(DiagnosticWindow).GetField("trayMenu", flags).GetValue(window);
                Assert(tray != null && tray.Icon != null && menu.Items.Count == 4 &&
                    window.Opacity == 0, "Tray-Start mit Icon und Menue ohne sichtbares Hauptfenster vorbereitet");
                var closing = typeof(DiagnosticWindow).GetMethod("OnFormClosing", flags);
                var args = new FormClosingEventArgs(CloseReason.UserClosing, false);
                closing.Invoke(window, new object[] { args });
                Assert(args.Cancel && !window.Visible, "Schliessen blendet Hauptfenster nur aus");
                args = new FormClosingEventArgs(CloseReason.WindowsShutDown, false);
                closing.Invoke(window, new object[] { args });
                Assert(!args.Cancel, "Windows-Abmeldung/Herunterfahren nicht blockieren");
                typeof(DiagnosticWindow).GetField("exiting", flags).SetValue(window, true);
                args = new FormClosingEventArgs(CloseReason.UserClosing, false);
                closing.Invoke(window, new object[] { args });
                Assert(!args.Cancel, "Explizites Beenden darf Fenster schliessen");
                menu.Items[1].PerformClick();
                var checkbox = (CheckBox)typeof(DiagnosticWindow).GetField("tabSwitch", flags).GetValue(window);
                Assert(checkbox.Checked && menu.Items[1].Text == "Bridge pausieren",
                    "Tray-Menue und Hauptfenster teilen Ausgabeschalter");
                menu.Items[1].PerformClick();
                Assert(!checkbox.Checked && menu.Items[1].Text == "Bridge fortsetzen",
                    "Tray-Menue pausiert Ausgabe und synchronisiert Beschriftung");
                typeof(DiagnosticWindow).GetField("exiting", flags).SetValue(window, false);
                IntPtr handle = window.Handle;
                Assert(Native.PostMessage(handle, 0x0010, IntPtr.Zero, IntPtr.Zero), "Native Schliessnachricht gesendet");
                Application.DoEvents();
                Assert(!window.IsDisposed && window.Handle == handle,
                    "Natives X behaelt Hauptfensterhandle fuer Raw Input und beendet nicht die Bridge");
                window.ExitApplication();
                Assert(window.IsDisposed && !tray.Visible, "Beenden schliesst Fenster und entfernt Tray-Icon");
            }
            using (var probe = new DiagnosticWindow(1, null))
                Assert(typeof(DiagnosticWindow).GetField("trayIcon", flags).GetValue(probe) == null,
                    "Diagnosemodus ohne Tray-Icon");
        }

        private static void CheckBoxAppearance()
        {
            using (var form = new ThemedForm())
            using (var check = new CheckBox { Text = "Aktiviert", Checked = true,
                Bounds = new Rectangle(16, 16, 180, 32) })
            using (var grid = new DataGridView { Bounds = new Rectangle(16, 64, 200, 100),
                AllowUserToAddRows = false, RowHeadersVisible = false })
            {
                grid.Columns.Add(new DataGridViewCheckBoxColumn());
                grid.Rows.Add(true);
                form.Controls.AddRange(new Control[] { check, grid });
                form.Show();
                foreach (var palette in new[] { new ThemePalette(true, false), new ThemePalette(false, false),
                    new ThemePalette(false, true) })
                {
                    AppTheme.Apply(form, palette);
                    Application.DoEvents();
                    Assert(check.FlatStyle == (palette.HighContrast ? FlatStyle.Standard : FlatStyle.Flat) && check.Checked,
                        "Checkboxen behalten native Bedienung und ihren Zustand");
                    if (!palette.HighContrast)
                    using (var image = new Bitmap(grid.Width, grid.Height))
                    {
                        grid.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
                        Rectangle cell = grid.GetCellDisplayRectangle(0, 0, false);
                        int visible = 0;
                        for (int y = cell.Top + 2; y < cell.Bottom - 2; y++)
                        for (int x = cell.Left + 2; x < cell.Right - 2; x++)
                        {
                            Color pixel = image.GetPixel(x, y);
                            if (pixel.ToArgb() != palette.Selection.ToArgb()) visible++;
                        }
                        Assert(visible > 10, "Originale Tabellencheckbox weiterhin sichtbar");
                        using (var checkImage = new Bitmap(check.Width, check.Height))
                        {
                            check.DrawToBitmap(checkImage, new Rectangle(Point.Empty, checkImage.Size));
                            Size glyphSize;
                            using (Graphics graphics = Graphics.FromImage(checkImage))
                                glyphSize = CheckBoxRenderer.GetGlyphSize(graphics,
                                    System.Windows.Forms.VisualStyles.CheckBoxState.CheckedNormal);
                            int glyphTop = (check.Height - glyphSize.Height) / 2;
                            Assert(CountPixels(checkImage, palette.Accent) > 10 &&
                                CountPixels(image, palette.Accent) > 10,
                                "Checkboxen und Tabellenhaken verwenden exakt die Akzentfarbe der Buttons");
                            Assert(checkImage.GetPixel(1, glyphTop + 1).ToArgb() != palette.Accent.ToArgb(),
                                "Akzentfeld der Checkbox hat abgerundete Ecken");
                        }
                        foreach (bool autoSize in new[] { false, true })
                        foreach (RightToLeft direction in new[] { RightToLeft.No, RightToLeft.Yes })
                        foreach (CheckState state in new[] { CheckState.Unchecked, CheckState.Checked, CheckState.Indeterminate })
                        {
                            check.AutoSize = autoSize;
                            check.RightToLeft = direction;
                            check.CheckState = state;
                            using (var checkImage = new Bitmap(check.Width, check.Height))
                            {
                                check.DrawToBitmap(checkImage, check.ClientRectangle);
                                int size;
                                int gap;
                                using (Graphics graphics = Graphics.FromImage(checkImage))
                                {
                                    size = Math.Max(CheckBoxRenderer.GetGlyphSize(graphics,
                                        System.Windows.Forms.VisualStyles.CheckBoxState.CheckedNormal).Width,
                                        (int)Math.Round(16 * graphics.DpiX / 96f));
                                    gap = (int)Math.Round(8 * graphics.DpiX / 96f);
                                }
                                int glyphLeft = direction == RightToLeft.Yes
                                    ? check.Width - check.Padding.Right - size : check.Padding.Left;
                                int gapLeft = direction == RightToLeft.Yes ? glyphLeft - gap : glyphLeft + size;
                                for (int y = 0; y < check.Height; y++)
                                {
                                    for (int x = 0; x < check.Padding.Left; x++)
                                        Assert(checkImage.GetPixel(x, y).ToArgb() == check.BackColor.ToArgb(),
                                            "Linker Checkboxrand frei von nativen Zeichenresten");
                                    for (int x = gapLeft; x < gapLeft + gap; x++)
                                        Assert(checkImage.GetPixel(x, y).ToArgb() == check.BackColor.ToArgb(),
                                            "Mindestens acht Pixel freier Abstand zwischen Checkbox und Beschriftung");
                                }
                            }
                        }
                        check.AutoSize = false;
                        check.RightToLeft = RightToLeft.No;
                    }
                    check.Enabled = false;
                    using (var image = new Bitmap(check.Width, check.Height))
                        check.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
                    Assert(check.Checked, "Deaktivierte Checkbox behaelt ihren Wert");
                    check.Enabled = true;
                    check.CheckState = CheckState.Indeterminate;
                    Assert(check.CheckState == CheckState.Indeterminate, "Unbekannter Autostartstatus weiterhin darstellbar");
                    check.Checked = true;
                }
            }
        }

        private static void SpinnerGeometry()
        {
            foreach (int height in new[] { 9, 10, 11, 12 })
            {
                Size? previous = null;
                foreach (bool up in new[] { true, false })
                using (var bitmap = new Bitmap(20, 20))
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(Color.Black);
                    NativeTheme.SpinnerArrow(graphics, new Rectangle(0, 0, 16, height), up, Color.White);
                    int left = 20, top = 20, right = -1, bottom = -1;
                    for (int y = 0; y < 20; y++)
                    for (int x = 0; x < 20; x++)
                        if (bitmap.GetPixel(x, y).R > 0)
                        {
                            left = Math.Min(left, x); right = Math.Max(right, x);
                            top = Math.Min(top, y); bottom = Math.Max(bottom, y);
                        }
                    var size = new Size(right - left + 1, bottom - top + 1);
                    Assert(size.Width >= 5 && size.Width <= 8 && size.Height >= 3 && size.Height <= 5,
                        "Zahlenfeldpfeile haben mittlere Groesse");
                    Assert(!previous.HasValue || previous.Value == size,
                        "Auf-/Ab-Pfeile sind auch bei ungerader Buttonhoehe gleich gross");
                    previous = size;
                }
            }
        }

        internal static void Run()
        {
            ThemePalette previous = AppTheme.Current;
            try
            {
                NativeControls();
                DropDownItems();
                MappingComboAppearance();
                ComboFieldEdges();
                AllInputFields();
                ProfileDropDown();
                RoundedAreas();
                DiagnosticRefresh();
                DiagnosticDisclosure();
                AppIcon();
                AutoStartRegistration();
                TrayLifecycle();
                CheckBoxAppearance();
                SpinnerGeometry();
                using (var form = new ThemedForm())
                using (var grid = new DataGridView())
                using (var text = new TextBox { Text = "unchanged" })
                using (var combo = new ComboBox())
                using (var number = new NumericUpDown { Value = 12 })
                using (var button = new RoundedButton { Tag = "primary" })
                using (var secondary = new RoundedButton())
                using (var error = new Label { ForeColor = Color.DarkRed })
                using (var success = new Label { ForeColor = Color.DarkGreen })
                {
                    grid.Columns.Add("Test", "Test");
                    grid.Rows.Add("value");
                    combo.Items.Add("selection");
                    combo.SelectedIndex = 0;
                    form.Controls.AddRange(new Control[] { grid, text, combo, number, button, secondary, error, success });
                    Assert(form.Font.Name == "Segoe UI" && text.Font.Name == "Segoe UI" &&
                        grid.Font.Name == "Segoe UI" && number.Font.Name == "Segoe UI" &&
                        combo.Font.Name == "Segoe UI" && form.Font.SizeInPoints == 9.75f,
                        "VS-Code-UI-Schrift 13 px an Formular und Kinder vererbt");
                    using (var bold = new Font(form.Font, FontStyle.Bold))
                        Assert(bold.Name == "Segoe UI" && bold.Bold, "Segoe UI Bold verfuegbar");
                    foreach (bool dark in new[] { true, false, true })
                    {
                        var palette = new ThemePalette(dark, false);
                        AppTheme.Current = palette;
                        AppTheme.Apply(form, palette);
                        Assert(form.BackColor == palette.Background && text.BackColor == palette.Input &&
                            combo.BackColor == palette.Input && number.BackColor == palette.Input,
                            "Formular und Eingabefelder folgen Palette");
                        Assert(error.ForeColor == palette.Error && success.ForeColor == palette.Success,
                            "Fehler- und Erfolgsfarben bei laufendem Wechsel erhalten");
                        Assert(grid.DefaultCellStyle.BackColor == palette.Surface &&
                            grid.ColumnHeadersDefaultCellStyle.BackColor == palette.Raised &&
                            grid.DefaultCellStyle.SelectionForeColor == palette.SelectionText,
                            "Raster, Header und Auswahl folgen Palette");
                        Assert(text.Text == "unchanged" && number.Value == 12 && combo.SelectedIndex == 0 &&
                            (string)grid.Rows[0].Cells[0].Value == "value", "Theme aendert keine Eingaben");
                        Assert(number.TextAlign == HorizontalAlignment.Center,
                            "Zahlen stehen mittig im editierbaren Zahlenbereich");
                        Contrast(palette.Text, palette.Surface, "Lesbarer Eingabetext");
                        Contrast(palette.Text, palette.Background, "Lesbare Beschriftung");
                        Contrast(palette.Text, palette.Muted, "Lesbarer deaktivierter Zelltext");
                        Contrast(palette.Error, palette.Surface, "Lesbarer Fehlertext");
                        Contrast(palette.Success, palette.Background, "Lesbarer Statustext");
                        Contrast(palette.SelectionText, palette.Selection, "Lesbare Auswahl");
                        Contrast(Color.White, palette.Accent, "Lesbarer primaerer Button");
                        Assert(button.BackColor == palette.Accent && button.ForeColor == Color.White &&
                            button.FlatAppearance.BorderColor == palette.Accent, "VS-Code-Primaeraktion korrekt abgesetzt");
                        Assert(secondary.BackColor == palette.Raised &&
                            secondary.FlatAppearance.BorderColor == palette.ControlBorder &&
                            secondary.Height >= (palette.HighContrast ? 28 : 34),
                            "Sekundaerbutton folgt Flaeche, Kontur und Bedienhoehe");
                        Assert(button.Region != null && !button.Region.IsVisible(0, 0) &&
                            button.Region.IsVisible(button.Width / 2, button.Height / 2), "Buttons wirklich abgerundet");
                        button.Width += 10;
                        Assert(button.Region.IsVisible(button.Width - 4, button.Height / 2) &&
                            !button.Region.IsVisible(button.Width - 1, 0), "Buttonrundung folgt Groessenaenderung");
                        if (dark)
                            Assert(palette.Background == Color.FromArgb(16, 17, 18) &&
                                palette.Surface == Color.FromArgb(25, 26, 27) &&
                                palette.Panel == Color.FromArgb(21, 23, 24) &&
                                palette.Input == Color.FromArgb(20, 22, 23) &&
                                palette.Raised == Color.FromArgb(32, 34, 36) &&
                                palette.Text == Color.FromArgb(210, 213, 216) &&
                                palette.Accent == Color.FromArgb(41, 122, 160), "Dark-2026-Farben exakt");
                    }

                    var highContrast = new ThemePalette(true, true);
                    Assert(!highContrast.Dark && highContrast.Surface == SystemColors.Window &&
                        highContrast.Text == SystemColors.WindowText, "Windows-Kontrastmodus hat Vorrang");
                    AppTheme.Apply(button, highContrast);
                    Assert(button.Region == null && button.FlatStyle == FlatStyle.Standard,
                        "Button im Kontrastmodus wieder nativ rechteckig");
                    AppTheme.Apply(button, new ThemePalette(true, false));
                    form.Show();
                    button.BringToFront();
                    int clicks = 0;
                    button.Click += delegate { clicks++; };
                    button.PerformClick();
                    Assert(clicks == 1, "Abgerundeter Button behaelt Klickausloesung");
                    button.Enabled = false;
                    button.PerformClick();
                    Assert(clicks == 1, "Deaktivierter abgerundeter Button loest nicht aus");
                }
                string temporary = Path.Combine(Path.GetTempPath(), "SpaceMouseBridge-ThemeTests-" + Guid.NewGuid().ToString("N"));
                var chrome = ApplicationMapping.Default();
                var store = new MappingStore(temporary, Path.Combine(temporary, "Chrome.xml"));
                using (var editor = new MappingEditor(store, chrome))
                {
                    var method = typeof(MappingEditor).GetMethod("OnAppThemeChanged", BindingFlags.Instance | BindingFlags.NonPublic);
                    foreach (bool dark in new[] { true, false })
                    {
                        AppTheme.Current = new ThemePalette(dark, false);
                        AppTheme.Apply(editor, AppTheme.Current);
                        method.Invoke(editor, null);
                        var grid = (DataGridView)typeof(MappingEditor).GetField("axes", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(editor);
                        Assert(grid.Rows[0].Cells["Scale"].Style.BackColor == AppTheme.Current.Muted,
                            "Nicht editierbare Mappingzelle korrekt abgesetzt");
                        Assert(editor.Collect().Axes[0].Mode == AxisMode.KeepDriver, "Editorbelegung bleibt erhalten");
                    }
                }
                var mapping = ApplicationProfile.Premiere.EmptyMapping();
                mapping.Jog = JogSettings.Default();
                var premiere = new MappingStore(temporary, null, ApplicationProfile.Premiere);
                using (var editor = new PremiereEditor(premiere, mapping))
                {
                    foreach (bool dark in new[] { true, false })
                    {
                        AppTheme.Current = new ThemePalette(dark, false);
                        AppTheme.Apply(editor, AppTheme.Current);
                        using (var bitmap = new Bitmap(1040, 870))
                        {
                            editor.Size = bitmap.Size;
                            editor.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                        }
                        Assert(editor.Collect().Jog.EffectiveMinimumRate == 0.5m, "Jog-Werte bei Themewechsel unveraendert");
                    }
                }
                Assert(!Directory.Exists(temporary), "Darstellung erzeugt keine Settingsdateien");
                using (var form = new ThemeTestForm())
                {
                    var number = new NumericUpDown { Value = 23 };
                    form.Controls.Add(number);
                    form.Show();
                    ThemePalette windowsPalette = AppTheme.Current;
                    AppTheme.Current = new ThemePalette(!windowsPalette.Dark, false);
                    AppTheme.Apply(form, AppTheme.Current);
                    int changes = form.ThemeChanges;
                    form.NotifySettingsChanged();
                    Assert(AppTheme.Current.Dark == windowsPalette.Dark &&
                        form.BackColor == windowsPalette.Background && form.ThemeChanges > changes &&
                        number.Value == 23, "WM_SETTINGCHANGE liest Windows neu, aktualisiert offenes Fenster ohne Wertverlust");
                    form.Close();
                }
                using (var timer = new Timer { Interval = 30 })
                {
                    bool sawDialog = false;
                    timer.Tick += delegate {
                        foreach (Form form in Application.OpenForms)
                        {
                            if (form.Text != "Theme-Testdialog") continue;
                            Assert(form.BackColor == AppTheme.Current.Background, "Bestaetigungsdialog folgt Windows-Farbe");
                            sawDialog = true;
                            form.DialogResult = DialogResult.Cancel;
                            break;
                        }
                    };
                    timer.Start();
                    DialogResult result = ThemeMessage.Show("Keine Dateien werden geaendert.", "Theme-Testdialog",
                        MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
                    timer.Stop();
                    Assert(sawDialog && result == DialogResult.Cancel, "Abbrechen im thematisierten Dialog bleibt Abbrechen");
                }
            }
            finally { AppTheme.Current = previous; }
        }
    }
}
