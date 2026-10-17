using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace SpaceMouseBridge
{
    internal sealed class ThemePalette
    {
        internal readonly bool Dark;
        internal readonly bool HighContrast;
        internal Color Background, Surface, Panel, Input, Raised, Text, Muted, ButtonHover, Border, ControlBorder,
            Selection, SelectionText, Error, Success, Curve, SingleFrame, Accent;

        internal ThemePalette(bool dark, bool highContrast)
        {
            Dark = dark && !highContrast;
            HighContrast = highContrast;
            Background = highContrast ? SystemColors.Control : Dark ? Color.FromArgb(16, 17, 18) : Color.FromArgb(243, 243, 243);
            Surface = highContrast ? SystemColors.Window : Dark ? Color.FromArgb(25, 26, 27) : Color.White;
            Panel = highContrast ? SystemColors.Control : Dark ? Color.FromArgb(21, 23, 24) : Color.FromArgb(243, 243, 243);
            Input = highContrast ? SystemColors.Window : Dark ? Color.FromArgb(20, 22, 23) : Color.White;
            Raised = highContrast ? SystemColors.Control : Dark ? Color.FromArgb(32, 34, 36) : Color.FromArgb(230, 230, 230);
            Text = highContrast ? SystemColors.WindowText : Dark ? Color.FromArgb(210, 213, 216) : Color.FromArgb(25, 25, 25);
            Muted = highContrast ? SystemColors.Control : Dark ? Color.FromArgb(36, 39, 41) : Color.FromArgb(230, 230, 230);
            ButtonHover = highContrast ? SystemColors.Control : Dark ? Color.FromArgb(43, 48, 51) : Color.FromArgb(215, 215, 215);
            Border = highContrast ? SystemColors.WindowText : Dark ? Color.FromArgb(51, 54, 56) : Color.FromArgb(160, 160, 160);
            ControlBorder = highContrast ? SystemColors.WindowText : Dark ? Color.FromArgb(65, 71, 75) : Color.FromArgb(145, 145, 145);
            Selection = highContrast ? SystemColors.Highlight : Dark ? Color.FromArgb(56, 57, 58) : Color.FromArgb(0, 95, 175);
            SelectionText = highContrast ? SystemColors.HighlightText : Dark ? Color.FromArgb(237, 237, 237) : Color.White;
            Error = highContrast ? SystemColors.WindowText : Dark ? Color.FromArgb(244, 135, 113) : Color.DarkRed;
            Success = highContrast ? SystemColors.WindowText : Dark ? Color.FromArgb(114, 200, 146) : Color.DarkGreen;
            Curve = highContrast ? SystemColors.WindowText : Dark ? Color.FromArgb(41, 122, 160) : Color.RoyalBlue;
            Accent = highContrast ? SystemColors.Highlight : Dark ? Color.FromArgb(41, 122, 160) : Color.FromArgb(0, 95, 175);
            SingleFrame = highContrast ? SystemColors.Control : Dark ? Color.FromArgb(85, 73, 33) : Color.LemonChiffon;
        }
    }

    internal static class AppTheme
    {
        internal static ThemePalette Current = Read();
        [ThreadStatic] private static int applying;
        internal static bool Applying { get { return applying != 0; } }

        private static ThemePalette Read()
        {
            if (SystemInformation.HighContrast) return new ThemePalette(false, true);
            return new ThemePalette(true, false);
        }

        internal static void Refresh() { Current = Read(); }

        internal static void Apply(Control control, ThemePalette palette)
        {
            applying++;
            try { ApplyControl(control, palette); }
            finally { applying--; }
        }

        private static void ApplyControl(Control control, ThemePalette palette)
        {
            Color oldText = control.ForeColor;
            bool error = oldText == Color.DarkRed || oldText == Color.FromArgb(244, 135, 113);
            bool success = oldText == Color.DarkGreen || oldText == Color.FromArgb(114, 200, 146);
            control.BackColor = control is TextBoxBase || control is ComboBox || control is NumericUpDown
                ? palette.Input : control.Parent is RoundedSurface ? palette.Surface :
                control.Parent != null && control.Parent.Tag as string == "panel" ? palette.Panel : palette.Background;
            control.ForeColor = error ? palette.Error : success ? palette.Success : palette.Text;
            Button button = control as Button;
            if (button != null)
            {
                button.UseVisualStyleBackColor = false;
                button.FlatStyle = palette.HighContrast ? FlatStyle.Standard : FlatStyle.Flat;
                button.BackColor = palette.Raised;
                button.FlatAppearance.BorderColor = palette.ControlBorder;
                button.FlatAppearance.MouseOverBackColor = palette.ButtonHover;
                button.FlatAppearance.MouseDownBackColor = palette.Selection;
                button.Padding = palette.HighContrast ? new Padding(8, 2, 8, 2) : new Padding(13, 0, 13, 0);
                button.MinimumSize = new Size(0, palette.HighContrast ? 28 : 34);
                button.Height = Math.Max(button.Height, button.MinimumSize.Height);
                if (!palette.HighContrast && button.Tag as string == "primary")
                {
                    button.BackColor = palette.Accent;
                    button.ForeColor = Color.White;
                    button.FlatAppearance.BorderColor = palette.Accent;
                    button.FlatAppearance.MouseOverBackColor = Color.FromArgb(43, 125, 163);
                    button.FlatAppearance.MouseDownBackColor = Color.FromArgb(35, 105, 138);
                }
                RoundedButton rounded = button as RoundedButton;
                if (rounded != null) rounded.Configure(palette);
            }
            if (control.Tag as string == "panel") control.BackColor = palette.Panel;
            if (control.Tag as string == "section")
            {
                control.BackColor = palette.Surface;
                control.Padding = new Padding(8, 4, 8, 4);
            }
            CheckBox check = control as CheckBox;
            if (check != null)
            {
                check.UseVisualStyleBackColor = false;
                check.FlatStyle = palette.HighContrast ? FlatStyle.Standard : FlatStyle.Flat;
                check.Padding = palette.HighContrast ? Padding.Empty : new Padding(4, 3, 10, 3);
                check.MinimumSize = new Size(0, 24);
                if (check.AutoSize && !palette.HighContrast)
                    using (Graphics graphics = check.CreateGraphics())
                    {
                        Size glyph = CheckGlyphSize(graphics);
                        Size text = TextRenderer.MeasureText(graphics, check.Text, check.Font, Size.Empty,
                            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine |
                            (check.UseMnemonic ? TextFormatFlags.Default : TextFormatFlags.NoPrefix));
                        int gap = (int)Math.Round(8 * graphics.DpiX / 96f);
                        check.MinimumSize = new Size(glyph.Width + gap + text.Width + check.Padding.Horizontal,
                            Math.Max(24, Math.Max(glyph.Height, text.Height) + check.Padding.Vertical));
                    }
                check.Paint -= PaintCheckBox;
                if (!palette.HighContrast) check.Paint += PaintCheckBox;
            }
            ComboBox combo = control as ComboBox;
            if (combo != null)
            {
                combo.FlatStyle = palette.HighContrast ? FlatStyle.Standard : FlatStyle.Flat;
                combo.DrawMode = palette.Dark ? DrawMode.OwnerDrawFixed : DrawMode.Normal;
                combo.DrawItem -= DrawComboItem;
                combo.DrawItem += DrawComboItem;
            }
            TextBoxBase textBox = control as TextBoxBase;
            if (textBox != null)
                textBox.BorderStyle = textBox.Parent is NumericUpDown || textBox.Parent is RoundedSurface && !palette.HighContrast ? BorderStyle.None :
                    palette.HighContrast ? BorderStyle.Fixed3D : BorderStyle.FixedSingle;
            NumericUpDown number = control as NumericUpDown;
            if (number != null)
            {
                number.BorderStyle = palette.HighContrast ? BorderStyle.Fixed3D : BorderStyle.FixedSingle;
                number.Height = Math.Max(number.Height, palette.HighContrast ? 28 : 34);
                number.TextAlign = HorizontalAlignment.Center;
                number.ForeColor = !number.Enabled && !palette.HighContrast ?
                    Color.FromArgb(108, 116, 121) : palette.Text;
            }
            DataGridView grid = control as DataGridView;
            if (grid != null)
            {
                grid.EnableHeadersVisualStyles = palette.HighContrast;
                grid.BorderStyle = palette.HighContrast ? BorderStyle.Fixed3D : BorderStyle.None;
                grid.ColumnHeadersBorderStyle = palette.Dark ? DataGridViewHeaderBorderStyle.Single : DataGridViewHeaderBorderStyle.Raised;
                grid.RowHeadersBorderStyle = palette.Dark ? DataGridViewHeaderBorderStyle.Single : DataGridViewHeaderBorderStyle.Raised;
                grid.BackgroundColor = palette.Surface;
                grid.GridColor = palette.Border;
                grid.DefaultCellStyle.BackColor = palette.Surface;
                grid.DefaultCellStyle.ForeColor = palette.Text;
                grid.DefaultCellStyle.SelectionBackColor = palette.Selection;
                grid.DefaultCellStyle.SelectionForeColor = palette.SelectionText;
                grid.ColumnHeadersDefaultCellStyle.BackColor = palette.Raised;
                grid.ColumnHeadersDefaultCellStyle.ForeColor = palette.Text;
                grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 3, 8, 3);
                grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = palette.Raised;
                grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = palette.Text;
                grid.ColumnHeadersHeight = 32;
                grid.DefaultCellStyle.Padding = new Padding(8, 3, 8, 3);
                grid.RowTemplate.Height = 30;
                foreach (DataGridViewRow row in grid.Rows) row.Height = 30;
                grid.RowHeadersDefaultCellStyle = grid.ColumnHeadersDefaultCellStyle.Clone();
                grid.EditingControlShowing -= StyleEditingControl;
                grid.EditingControlShowing += StyleEditingControl;
                grid.CellPainting -= PaintCheckBoxCell;
                grid.CellPainting += PaintCheckBoxCell;
                grid.CellPainting -= PaintComboCell;
                grid.CellPainting += PaintComboCell;
            }
            foreach (Control child in control.Controls) Apply(child, palette);
            RoundedSurface surface = control as RoundedSurface;
            if (surface != null) surface.Configure(palette);
            NativeTheme.Apply(control, palette);
            control.Invalidate();
        }

        private static void StyleEditingControl(object sender, DataGridViewEditingControlShowingEventArgs args)
        {
            Apply(args.Control, Current);
        }

        private static void PaintCheckBox(object sender, PaintEventArgs args)
        {
            var check = (CheckBox)sender;
            ThemePalette palette = NativeTheme.Palette(check);
            Size size = CheckGlyphSize(args.Graphics);
            bool rtl = check.RightToLeft == RightToLeft.Yes;
            int x = rtl ? check.ClientSize.Width - check.Padding.Right - size.Width : check.Padding.Left;
            var bounds = new Rectangle(x, (check.ClientSize.Height - size.Height) / 2, size.Width, size.Height);
            args.Graphics.Clear(check.BackColor);
            DrawCheckGlyph(args.Graphics, bounds, check.CheckState, check.Enabled, check.BackColor, palette);
            int gap = (int)Math.Round(8 * args.Graphics.DpiX / 96f);
            Rectangle textBounds = Rectangle.FromLTRB(
                rtl ? check.Padding.Left : bounds.Right + gap, check.Padding.Top,
                rtl ? bounds.Left - gap : check.ClientSize.Width - check.Padding.Right,
                check.ClientSize.Height - check.Padding.Bottom);
            TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine |
                (rtl ? TextFormatFlags.RightToLeft | TextFormatFlags.Right : TextFormatFlags.Left);
            if (!check.UseMnemonic) flags |= TextFormatFlags.NoPrefix;
            TextRenderer.DrawText(args.Graphics, check.Text, check.Font, textBounds,
                check.Enabled ? check.ForeColor : SystemColors.GrayText, flags);
            if (check.Focused)
                ControlPaint.DrawFocusRectangle(args.Graphics, textBounds, check.ForeColor, check.BackColor);
        }

        private static Size CheckGlyphSize(Graphics graphics)
        {
            Size native = CheckBoxRenderer.GetGlyphSize(graphics,
                System.Windows.Forms.VisualStyles.CheckBoxState.CheckedNormal);
            int target = Math.Max(1, (int)Math.Round(16 * graphics.DpiX / 96f));
            return new Size(Math.Max(native.Width, target), Math.Max(native.Height, target));
        }

        private static void PaintCheckBoxCell(object sender, DataGridViewCellPaintingEventArgs args)
        {
            var grid = (DataGridView)sender;
            if (args.RowIndex < 0 || args.ColumnIndex < 0) return;
            var cell = grid.Rows[args.RowIndex].Cells[args.ColumnIndex] as DataGridViewCheckBoxCell;
            if (cell == null) return;
            ThemePalette palette = NativeTheme.Palette(grid);
            if (palette.HighContrast) return;
            object value = args.FormattedValue;
            CheckState state = value == null || value == DBNull.Value ? CheckState.Indeterminate :
                Convert.ToBoolean(value) ? CheckState.Checked : CheckState.Unchecked;
            Color background = (args.State & DataGridViewElementStates.Selected) != 0
                ? args.CellStyle.SelectionBackColor : args.CellStyle.BackColor;
            Size size = CheckGlyphSize(args.Graphics);
            Rectangle bounds = new Rectangle(
                args.CellBounds.Left + (args.CellBounds.Width - size.Width) / 2,
                args.CellBounds.Top + (args.CellBounds.Height - size.Height) / 2,
                size.Width, size.Height);
            args.Paint(args.ClipBounds, args.PaintParts & ~DataGridViewPaintParts.ContentForeground);
            DrawCheckGlyph(args.Graphics, bounds, state, grid.Enabled && !cell.ReadOnly, background, palette);
            args.Handled = true;
        }

        private static void DrawCheckGlyph(Graphics graphics, Rectangle bounds, CheckState state,
            bool enabled, Color background, ThemePalette palette)
        {
            GraphicsState graphicsState = graphics.Save();
            try
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var backgroundBrush = new SolidBrush(background))
                    graphics.FillRectangle(backgroundBrush, bounds);
                Rectangle box = Rectangle.Inflate(bounds, -1, -1);
                bool checkedState = state != CheckState.Unchecked;
                Color fill = checkedState ? palette.Accent : background;
                Color border = checkedState ? palette.Accent : enabled ? palette.ControlBorder : palette.Muted;
                float scale = graphics.DpiX / 96f;
                using (GraphicsPath boxPath = RoundedSurface.Outline(box, 3 * scale))
                using (var fillBrush = new SolidBrush(fill))
                using (var borderPen = new Pen(border, 1))
                {
                    graphics.FillPath(fillBrush, boxPath);
                    graphics.DrawPath(borderPen, boxPath);
                }
                if (!checkedState) return;
                using (var checkPen = new Pen(Color.White, 1.8f * scale))
                {
                    checkPen.StartCap = LineCap.Round;
                    checkPen.EndCap = LineCap.Round;
                    if (state == CheckState.Indeterminate)
                        graphics.DrawLine(checkPen, bounds.Left + bounds.Width * 0.28f,
                            bounds.Top + bounds.Height * 0.5f, bounds.Left + bounds.Width * 0.72f,
                            bounds.Top + bounds.Height * 0.5f);
                    else
                    {
                        graphics.DrawLine(checkPen, bounds.Left + bounds.Width * 0.25f,
                            bounds.Top + bounds.Height * 0.52f, bounds.Left + bounds.Width * 0.43f,
                            bounds.Top + bounds.Height * 0.69f);
                        graphics.DrawLine(checkPen, bounds.Left + bounds.Width * 0.43f,
                            bounds.Top + bounds.Height * 0.69f, bounds.Left + bounds.Width * 0.77f,
                            bounds.Top + bounds.Height * 0.31f);
                    }
                }
            }
            finally { graphics.Restore(graphicsState); }
        }

        private static void DrawComboItem(object sender, DrawItemEventArgs args)
        {
            var combo = (ComboBox)sender;
            ThemePalette palette = NativeTheme.Palette(combo);
            if (!palette.Dark) { args.DrawBackground(); args.DrawFocusRectangle(); return; }
            bool selected = (args.State & DrawItemState.Selected) != 0;
            using (var brush = new SolidBrush(palette.Surface))
                args.Graphics.FillRectangle(brush, args.Bounds);
            if (selected)
            {
                args.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = RoundedSurface.Outline(args.Bounds, 4 * args.Graphics.DpiX / 96f))
                using (var brush = new SolidBrush(palette.Selection))
                    args.Graphics.FillPath(brush, path);
            }
            string text = args.Index < 0 ? combo.Text : combo.GetItemText(combo.Items[args.Index]);
            TextRenderer.DrawText(args.Graphics, text, combo.Font, Rectangle.Inflate(args.Bounds, -2, 0),
                combo.Enabled ? selected ? palette.SelectionText : palette.Text : palette.Border,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix |
                (combo.RightToLeft == RightToLeft.Yes ? TextFormatFlags.RightToLeft | TextFormatFlags.Right : TextFormatFlags.Left));
        }

        private static void PaintComboCell(object sender, DataGridViewCellPaintingEventArgs args)
        {
            var grid = (DataGridView)sender;
            ThemePalette palette = NativeTheme.Palette(grid);
            if (!palette.Dark || args.RowIndex < 0 || args.ColumnIndex < 0) return;
            var cell = grid.Rows[args.RowIndex].Cells[args.ColumnIndex] as DataGridViewComboBoxCell;
            if (cell == null || cell.DisplayStyle == DataGridViewComboBoxDisplayStyle.Nothing ||
                (cell.DisplayStyleForCurrentCellOnly && cell != grid.CurrentCell)) return;
            args.Paint(args.ClipBounds, args.PaintParts &
                ~(DataGridViewPaintParts.ContentBackground | DataGridViewPaintParts.ContentForeground));
            int width = Math.Min(SystemInformation.VerticalScrollBarWidth, args.CellBounds.Width - 2);
            var bounds = new Rectangle(grid.RightToLeft == RightToLeft.Yes ? args.CellBounds.Left + 1 :
                args.CellBounds.Right - width - 1, args.CellBounds.Top + 1, width, args.CellBounds.Height - 3);
            bool selected = (args.State & DataGridViewElementStates.Selected) != 0;
            if ((args.PaintParts & (DataGridViewPaintParts.ContentBackground |
                DataGridViewPaintParts.ContentForeground)) != 0)
                using (var brush = new SolidBrush(selected ? args.CellStyle.SelectionBackColor : args.CellStyle.BackColor))
                    args.Graphics.FillRectangle(brush, Rectangle.Inflate(args.CellBounds, -1, -1));
            Rectangle textBounds = Rectangle.Inflate(args.CellBounds, -8, -3);
            if (grid.RightToLeft == RightToLeft.Yes) textBounds.X += width;
            textBounds.Width = Math.Max(0, textBounds.Width - width);
            if ((args.PaintParts & DataGridViewPaintParts.ContentForeground) != 0)
            {
                TextRenderer.DrawText(args.Graphics, Convert.ToString(args.FormattedValue), args.CellStyle.Font,
                    textBounds, selected ? args.CellStyle.SelectionForeColor : args.CellStyle.ForeColor,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix |
                    (grid.RightToLeft == RightToLeft.Yes ? TextFormatFlags.RightToLeft | TextFormatFlags.Right :
                        TextFormatFlags.Left));
                NativeTheme.Arrow(args.Graphics, bounds, false, cell.ReadOnly ? palette.Border : palette.Text);
            }
            args.Handled = true;
        }
    }

    internal static class NativeTheme
    {
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int SetPreferredAppMode(int mode);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private delegate bool AllowDarkModeForWindow(IntPtr window, [MarshalAs(UnmanagedType.Bool)] bool allow);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string name);
        [DllImport("kernel32.dll", ExactSpelling = true)]
        private static extern IntPtr GetProcAddress(IntPtr module, IntPtr ordinal);
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr window, string application, string id);
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

        private static readonly ConditionalWeakTable<Control, WindowTheme> windows =
            new ConditionalWeakTable<Control, WindowTheme>();
        private static AllowDarkModeForWindow allowDark;
        private static bool initialized;

        internal static void Initialize()
        {
            if (initialized) return;
            initialized = true;
            // Ordinals 133/135 have these signatures on Windows 10 1903 and Windows 11.
            if (Environment.OSVersion.Version.Build < 18362)
            {
                System.Diagnostics.Trace.TraceWarning("Native Darkmode-Steuerelemente benoetigen Windows 10 1903 oder neuer.");
                return;
            }
            IntPtr module = LoadLibrary("uxtheme.dll");
            if (module == IntPtr.Zero)
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Windows-Themedienst konnte nicht geladen werden.");
            IntPtr preferred = GetProcAddress(module, new IntPtr(135));
            IntPtr window = GetProcAddress(module, new IntPtr(133));
            if (preferred == IntPtr.Zero || window == IntPtr.Zero)
            {
                System.Diagnostics.Trace.TraceWarning("Native Windows-Darkmode-Schnittstellen nicht verfuegbar.");
                return;
            }
            var setMode = (SetPreferredAppMode)Marshal.GetDelegateForFunctionPointer(preferred, typeof(SetPreferredAppMode));
            setMode(2); // Fixed VS Code Dark 2026, independent of the Windows app preference.
            allowDark = (AllowDarkModeForWindow)Marshal.GetDelegateForFunctionPointer(window, typeof(AllowDarkModeForWindow));
        }

        internal static void Apply(Control control, ThemePalette palette)
        {
            Initialize();
            windows.GetValue(control, c => new WindowTheme(c)).Apply(palette);
        }

        internal static ThemePalette Palette(Control control)
        {
            WindowTheme window;
            return windows.TryGetValue(control, out window) && window.Palette != null ? window.Palette : AppTheme.Current;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ComboInfo
        {
            internal int Size;
            internal Rectangle Item, Button;
            internal int State;
            internal IntPtr Combo, Edit, List;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetComboBoxInfo(IntPtr window, ref ComboInfo info);

        internal static IntPtr DropDownHandle(ComboBox combo)
        {
            var info = new ComboInfo { Size = Marshal.SizeOf(typeof(ComboInfo)) };
            if (!GetComboBoxInfo(combo.Handle, ref info))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Auswahlliste konnte nicht ermittelt werden.");
            return info.List;
        }

        private sealed class DropDownTheme : NativeWindow
        {
            private readonly ComboBox combo;
            private ThemePalette palette;
            private Size shapedSize;
            private bool shapedHighContrast;

            internal DropDownTheme(ComboBox owner) { combo = owner; }

            internal void Apply(ThemePalette value)
            {
                palette = value;
                IntPtr list = DropDownHandle(combo);
                if (Handle != list)
                {
                    Detach();
                    AssignHandle(list);
                }
                if (allowDark != null) allowDark(Handle, value.Dark);
                int themeResult = SetWindowTheme(Handle, value.HighContrast ? null :
                    value.Dark ? "DarkMode_Explorer" : "Explorer", null);
                if (themeResult < 0)
                    System.Diagnostics.Trace.TraceWarning("Auswahllisten-Farbschema: HRESULT " + themeResult);
                Shape();
            }

            internal void Detach()
            {
                if (Handle != IntPtr.Zero) ReleaseHandle();
                shapedSize = Size.Empty;
            }

            [DllImport("user32.dll", SetLastError = true)]
            private static extern bool GetWindowRect(IntPtr window, out Rectangle rect);
            [DllImport("user32.dll", SetLastError = true)]
            private static extern bool GetClientRect(IntPtr window, out Rectangle rect);
            [DllImport("user32.dll")]
            private static extern bool ClientToScreen(IntPtr window, ref Point point);
            [DllImport("user32.dll", SetLastError = true)]
            private static extern int SetWindowRgn(IntPtr window, IntPtr region, bool redraw);
            [DllImport("gdi32.dll")]
            private static extern IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int width, int height);
            [DllImport("gdi32.dll")]
            private static extern bool DeleteObject(IntPtr value);
            [DllImport("user32.dll")]
            private static extern IntPtr GetWindowDC(IntPtr window);
            [DllImport("user32.dll")]
            private static extern int ReleaseDC(IntPtr window, IntPtr dc);

            private float Radius
            {
                get { using (var graphics = combo.CreateGraphics()) return 9 * graphics.DpiX / 96f; }
            }

            private Size WindowSize()
            {
                Rectangle rect;
                if (!GetWindowRect(Handle, out rect))
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Groesse der Auswahlliste konnte nicht gelesen werden.");
                // Win32 RECT stores right/bottom, not width/height.
                return new Size(rect.Width - rect.X, rect.Height - rect.Y);
            }

            private void Shape()
            {
                if (Handle == IntPtr.Zero || palette == null) return;
                Size size = WindowSize();
                if (size.Width < 2 || size.Height < 2 ||
                    size == shapedSize && shapedHighContrast == palette.HighContrast) return;
                IntPtr region = palette.HighContrast ? IntPtr.Zero :
                    CreateRoundRectRgn(0, 0, size.Width + 1, size.Height + 1, (int)(Radius * 2), (int)(Radius * 2));
                if (!palette.HighContrast && region == IntPtr.Zero)
                    throw new System.ComponentModel.Win32Exception("Rundung der Auswahlliste konnte nicht erstellt werden.");
                shapedSize = size;
                shapedHighContrast = palette.HighContrast;
                if (SetWindowRgn(Handle, region, true) == 0)
                {
                    shapedSize = Size.Empty;
                    if (region != IntPtr.Zero) DeleteObject(region);
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Rundung der Auswahlliste konnte nicht gesetzt werden.");
                }
            }

            protected override void WndProc(ref Message message)
            {
                base.WndProc(ref message);
                if (message.Msg == 0x0047)
                    Shape();
                if (palette == null || palette.HighContrast ||
                    (message.Msg != 0x0085 && message.Msg != 0x000f && message.Msg != 0x0047 && message.Msg != 0x0014)) return;
                IntPtr dc = GetWindowDC(Handle);
                if (dc == IntPtr.Zero) return;
                try
                {
                    Size size = WindowSize();
                    using (var graphics = Graphics.FromHdc(dc))
                    {
                        Rectangle client, window;
                        Point origin = Point.Empty;
                        if (!GetClientRect(Handle, out client) || !GetWindowRect(Handle, out window) ||
                            !ClientToScreen(Handle, ref origin))
                            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),
                                "Inhaltsbereich der Auswahlliste konnte nicht gelesen werden.");
                        graphics.ExcludeClip(new Rectangle(origin.X - window.X, origin.Y - window.Y,
                            client.Width, client.Height));
                        using (var brush = new SolidBrush(palette.Surface))
                            graphics.FillRectangle(brush, 0, 0, size.Width, size.Height);
                        graphics.SmoothingMode = SmoothingMode.AntiAlias;
                        using (var path = RoundedSurface.Outline(
                            new RectangleF(1, 1, size.Width - 2, size.Height - 2), Radius - 0.5f))
                        using (var pen = new Pen(palette.Accent))
                            graphics.DrawPath(pen, path);
                    }
                }
                finally { ReleaseDC(Handle, dc); }
            }
        }

        internal static void Arrow(Graphics graphics, Rectangle bounds, bool up, Color color)
        {
            int x = bounds.Left + bounds.Width / 2, y = bounds.Top + bounds.Height / 2;
            int size = Math.Max(3, Math.Min(4, bounds.Height / 3));
            using (var brush = new SolidBrush(color))
                graphics.FillPolygon(brush, new[] {
                    new Point(x - size, y + (up ? 1 : -1)),
                    new Point(x + size, y + (up ? 1 : -1)),
                    new Point(x, y + (up ? 1 - size : size - 1))
                });
        }

        internal static void SpinnerArrow(Graphics graphics, Rectangle bounds, bool up, Color color)
        {
            int x = bounds.Left + bounds.Width / 2;
            int y = bounds.Top + bounds.Height / 2;
            int halfWidth = Math.Max(2, (int)Math.Round(3 * graphics.DpiX / 96f));
            int height = Math.Max(3, (int)Math.Round(4 * graphics.DpiY / 96f));
            int top = y - height / 2;
            using (var brush = new SolidBrush(color))
                for (int row = 0; row < height; row++)
                {
                    int span = halfWidth * (up ? row : height - 1 - row) / (height - 1);
                    graphics.FillRectangle(brush, x - span, top + row, span * 2 + 1, 1);
                }
        }

        private static void Attribute(IntPtr window, int id, int value)
        {
            int result = DwmSetWindowAttribute(window, id, ref value, sizeof(int));
            if (result < 0) System.Diagnostics.Trace.TraceWarning("Fenster-Farbschema (" + id + "): HRESULT " + result);
        }

        private sealed class WindowTheme : NativeWindow
        {
            private readonly Control control;
            private ThemePalette palette;
            private bool applied;
            private bool dark;
            private bool highContrast;
            private readonly DropDownTheme dropDown;
            internal ThemePalette Palette { get { return palette; } }

            internal WindowTheme(Control owner)
            {
                control = owner;
                var combo = owner as ComboBox;
                if (combo != null)
                {
                    dropDown = new DropDownTheme(combo);
                    combo.DropDown += delegate { if (palette != null) dropDown.Apply(palette); };
                    owner.HandleDestroyed += delegate { dropDown.Detach(); };
                    owner.Disposed += delegate { dropDown.Detach(); };
                }
                owner.HandleCreated += delegate { Attach(); applied = false; Update(); UpdateShape(); };
                owner.HandleDestroyed += delegate { if (Handle != IntPtr.Zero) ReleaseHandle(); applied = false; };
                owner.SizeChanged += delegate { UpdateShape(); };
                if (owner is NumericUpDown)
                {
                    owner.Layout += delegate { ClipNumericChildren(); };
                    foreach (Control child in owner.Controls)
                    {
                        child.GotFocus += delegate { owner.Invalidate(); };
                        child.LostFocus += delegate { owner.Invalidate(); };
                    }
                    owner.EnabledChanged += delegate {
                        if (palette == null) return;
                        owner.ForeColor = !owner.Enabled && !palette.HighContrast ?
                            Color.FromArgb(108, 116, 121) : palette.Text;
                        owner.Invalidate(true);
                    };
                }
                owner.MouseEnter += delegate { owner.Invalidate(); };
                owner.MouseLeave += delegate { owner.Invalidate(); };
                owner.GotFocus += delegate { owner.Invalidate(); };
                owner.LostFocus += delegate { owner.Invalidate(); };
                owner.EnabledChanged += delegate { owner.Invalidate(); };
                if (owner.IsHandleCreated) Attach();
            }

            private void Attach()
            {
                if (control is ComboBox || control is NumericUpDown || control.GetType().Name == "UpDownButtons")
                    AssignHandle(control.Handle);
            }

            internal void Apply(ThemePalette value)
            {
                palette = value;
                Update();
                UpdateShape();
                if (dropDown != null && ((ComboBox)control).DroppedDown) dropDown.Apply(value);
            }

            private float CornerRadius
            {
                get { using (var graphics = control.CreateGraphics()) return 8 * graphics.DpiX / 96f; }
            }

            private void UpdateShape()
            {
                if (palette == null || !(control is ComboBox || control is NumericUpDown)) return;
                Region previous = control.Region;
                if (palette.HighContrast || control is NumericUpDown || control.Width < 2 || control.Height < 2) control.Region = null;
                else
                    using (var path = RoundedSurface.Outline(new RectangleF(0, 0, control.Width, control.Height), CornerRadius))
                        control.Region = new Region(path);
                if (previous != null) previous.Dispose();
                ClipNumericChildren();
                control.Invalidate();
            }

            private void ClipNumericChildren()
            {
                if (palette == null || !(control is NumericUpDown)) return;
                foreach (Control child in control.Controls)
                {
                    Region previous = child.Region;
                    if (palette.HighContrast) child.Region = null;
                    else
                        using (var path = RoundedSurface.Outline(
                            new RectangleF(1.5f - child.Left, 1.5f - child.Top,
                                control.Width - 3, control.Height - 3), Math.Max(0, CornerRadius - 1)))
                            child.Region = new Region(path);
                    if (previous != null) previous.Dispose();
                }
            }

            private void Update()
            {
                if (palette == null || !control.IsHandleCreated ||
                    (applied && dark == palette.Dark && highContrast == palette.HighContrast)) return;
                applied = true;
                dark = palette.Dark;
                highContrast = palette.HighContrast;
                if (allowDark != null) allowDark(control.Handle, dark);
                int result = SetWindowTheme(control.Handle, highContrast ? null : dark ? "DarkMode_Explorer" : "Explorer", null);
                if (result < 0) System.Diagnostics.Trace.TraceWarning("Steuerelement-Farbschema: HRESULT " + result);
                if (control is Form)
                {
                    Attribute(control.Handle, 20, dark ? 1 : 0);
                    if (Environment.OSVersion.Version.Build >= 22000)
                    {
                        Attribute(control.Handle, 33, highContrast ? 0 : 2);
                        Attribute(control.Handle, 35, highContrast || !dark ? -1 : ColorTranslator.ToWin32(palette.Surface));
                        Attribute(control.Handle, 36, highContrast || !dark ? -1 : ColorTranslator.ToWin32(palette.Text));
                        Attribute(control.Handle, 34, highContrast || !dark ? -1 : ColorTranslator.ToWin32(palette.Border));
                    }
                }

            }

            [DllImport("user32.dll")]
            private static extern IntPtr GetDC(IntPtr window);
            [DllImport("user32.dll")]
            private static extern int ReleaseDC(IntPtr window, IntPtr dc);

            protected override void WndProc(ref Message message)
            {
                base.WndProc(ref message);
                if (palette == null || (!palette.Dark &&
                    (palette.HighContrast || !(control is NumericUpDown || control.GetType().Name == "UpDownButtons"))) ||
                    (message.Msg != 0x000f && message.Msg != 0x0317 && message.Msg != 0x0318)) return;
                bool print = message.Msg != 0x000f;
                IntPtr dc = print ? message.WParam : GetDC(Handle);
                if (dc == IntPtr.Zero) return;
                try
                {
                    using (var graphics = Graphics.FromHdc(dc))
                    {
                        Rectangle bounds = control.ClientRectangle;
                        var combo = control as ComboBox;
                        if (combo != null)
                        {
                            var info = new ComboInfo { Size = Marshal.SizeOf(typeof(ComboInfo)) };
                            if (!GetComboBoxInfo(combo.Handle, ref info))
                                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),
                                    "Pfeilbereich des Auswahlfelds konnte nicht gelesen werden.");
                            var button = Rectangle.FromLTRB(info.Button.Left, info.Button.Top,
                                info.Button.Width, info.Button.Height);
                            var item = Rectangle.FromLTRB(info.Item.Left, info.Item.Top,
                                info.Item.Width, info.Item.Height);
                            GraphicsState saved = graphics.Save();
                            try
                            {
                                // Paint the entire native shell, not just the arrow: flat combos leave
                                // additional light strips between their item and button rectangles.
                                if (combo.DropDownStyle != ComboBoxStyle.DropDownList)
                                    graphics.ExcludeClip(item);
                                using (var brush = new SolidBrush(palette.Input))
                                    graphics.FillRectangle(brush, bounds);
                                using (var brush = new SolidBrush(control.Enabled ? palette.Raised : palette.Input))
                                    graphics.FillRectangle(brush, button);
                            }
                            finally { graphics.Restore(saved); }
                            if (combo.DropDownStyle == ComboBoxStyle.DropDownList)
                                TextRenderer.DrawText(graphics, combo.Text, combo.Font,
                                    Rectangle.Inflate(item, -2, 0), control.Enabled ? palette.Text : palette.Border,
                                    TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
                                    TextFormatFlags.NoPrefix | (combo.RightToLeft == RightToLeft.Yes ?
                                        TextFormatFlags.RightToLeft | TextFormatFlags.Right : TextFormatFlags.Left));
                            Arrow(graphics, button, false, control.Enabled ? palette.Text : palette.Border);
                        }
                        else if (!(control is NumericUpDown))
                        {
                            using (var brush = new SolidBrush(palette.Raised))
                                graphics.FillRectangle(brush, bounds);
                            var upper = new Rectangle(0, 0, bounds.Width, bounds.Height / 2);
                            var lower = new Rectangle(0, bounds.Height - upper.Height, bounds.Width, upper.Height);
                            Color glyph = control.Enabled ? palette.Text : palette.Border;
                            SpinnerArrow(graphics, upper, true, glyph);
                            SpinnerArrow(graphics, lower, false, glyph);
                            using (var pen = new Pen(palette.ControlBorder))
                            {
                                int x = control.Parent != null && control.Parent.RightToLeft == RightToLeft.Yes ?
                                    bounds.Right - 1 : 0;
                                graphics.DrawLine(pen, x, 0, x, bounds.Bottom);
                                graphics.DrawLine(pen, 0, bounds.Height / 2, bounds.Right, bounds.Height / 2);
                            }
                        }
                        if (!palette.HighContrast && (control is ComboBox || control is NumericUpDown) &&
                            bounds.Width > 2 && bounds.Height > 2)
                        {
                            if (control is NumericUpDown)
                            {
                                graphics.Clear(control.Parent == null ? palette.Background : control.Parent.BackColor);
                            }
                            graphics.SmoothingMode = SmoothingMode.AntiAlias;
                            using (var path = RoundedSurface.Outline(
                                new RectangleF(0.5f, 0.5f, bounds.Width - 1, bounds.Height - 1), CornerRadius))
                            using (var pen = new Pen(FrameColor()))
                            {
                                if (control is NumericUpDown)
                                    using (var brush = new SolidBrush(palette.Input))
                                        graphics.FillPath(brush, path);
                                graphics.DrawPath(pen, path);
                            }
                        }
                    }
                }
                finally { if (!print) ReleaseDC(Handle, dc); }
            }

            private Color FrameColor()
            {
                if (palette.HighContrast) return palette.ControlBorder;
                if (control.ContainsFocus) return palette.Curve;
                return palette.ControlBorder;
            }
        }
    }

    internal sealed class RoundedButton : Button
    {
        private bool highContrast;

        internal RoundedButton()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }

        internal void Configure(ThemePalette palette)
        {
            highContrast = palette.HighContrast;
            FlatAppearance.BorderSize = 1;
            UpdateShape();
        }

        private float Radius
        {
            get { using (Graphics graphics = CreateGraphics()) return 8 * graphics.DpiX / 96f; }
        }

        private void UpdateShape()
        {
            Region previous = Region;
            if (highContrast || Width < 2 || Height < 2) Region = null;
            else
                using (GraphicsPath path = RoundedSurface.Outline(new RectangleF(0, 0, Width, Height), Radius))
                    Region = new Region(path);
            if (previous != null) previous.Dispose();
            Invalidate();
        }

        protected override void OnSizeChanged(EventArgs args)
        {
            base.OnSizeChanged(args);
            UpdateShape();
        }

        protected override void OnHandleCreated(EventArgs args)
        {
            base.OnHandleCreated(args);
            UpdateShape();
        }

        protected override void OnPaint(PaintEventArgs args)
        {
            base.OnPaint(args);
            if (highContrast || Width < 2 || Height < 2) return;
            args.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = RoundedSurface.Outline(new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), Radius))
            using (var pen = new Pen(FlatAppearance.BorderColor))
                args.Graphics.DrawPath(pen, path);
        }
    }

    internal sealed class RoundedSurface : Panel
    {
        private ThemePalette palette = AppTheme.Current;
        internal int CornerRadius { get { return palette.HighContrast ? 0 : Math.Max(6, (int)Math.Round(8 * DeviceDpiScale)); } }
        private float DeviceDpiScale
        {
            get { using (Graphics graphics = CreateGraphics()) return graphics.DpiX / 96f; }
        }

        internal RoundedSurface()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            Dock = DockStyle.Fill;
            Padding = new Padding(12);
        }

        internal static RoundedSurface Wrap(Control content)
        {
            var surface = new RoundedSurface { Margin = content.Margin };
            content.Dock = DockStyle.Fill;
            content.Margin = Padding.Empty;
            surface.Controls.Add(content);
            return surface;
        }

        internal void Configure(ThemePalette value)
        {
            palette = value;
            BackColor = Parent == null ? palette.Background : Parent.BackColor;
            Invalidate();
        }

        internal static GraphicsPath Outline(RectangleF bounds, float radius)
        {
            var path = new GraphicsPath();
            float diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
            if (diameter <= 0) path.AddRectangle(bounds);
            else
            {
                path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
                path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
                path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
                path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
                path.CloseFigure();
            }
            return path;
        }

        protected override void OnPaint(PaintEventArgs args)
        {
            base.OnPaint(args);
            if (Width < 2 || Height < 2) return;
            args.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = Outline(new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), CornerRadius))
            using (var fill = new SolidBrush(palette.Surface))
            using (var border = new Pen(palette.Border))
            {
                args.Graphics.FillPath(fill, path);
                args.Graphics.DrawPath(border, path);
            }
        }
    }

    internal class ThemedForm : Form
    {
        private readonly Font appFont = AppTypography.CreateFont();

        internal ThemedForm()
        {
            Font = appFont;
            using (var stream = typeof(ThemedForm).Assembly.GetManifestResourceStream("SpaceMouseBridge.OrbitCap.ico"))
            {
                if (stream == null) throw new System.IO.InvalidDataException("Das Orbit-Cap-App-Icon fehlt.");
                using (var icon = new Icon(stream))
                    Icon = (Icon)icon.Clone();
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                appFont.Dispose();
                if (Icon != null) Icon.Dispose();
            }
        }

        protected override void OnLoad(EventArgs args)
        {
            base.OnLoad(args);
            RefreshTheme();
        }

        private void RefreshTheme()
        {
            AppTheme.Refresh();
            AppTheme.Apply(this, AppTheme.Current);
            OnAppThemeChanged();
        }

        protected virtual void OnAppThemeChanged() { }

        protected override void WndProc(ref Message message)
        {
            base.WndProc(ref message);
            if ((message.Msg == 0x001a || message.Msg == 0x031a || message.Msg == 0x0015) &&
                !IsDisposed && !AppTheme.Applying)
                RefreshTheme();
        }
    }

    internal static class ThemeMessage
    {
        internal static DialogResult Show(string text, string caption,
            MessageBoxButtons buttons = MessageBoxButtons.OK, MessageBoxIcon icon = MessageBoxIcon.None)
        {
            return Show(null, text, caption, buttons, icon);
        }

        internal static DialogResult Show(IWin32Window owner, string text, string caption,
            MessageBoxButtons buttons = MessageBoxButtons.OK, MessageBoxIcon icon = MessageBoxIcon.None)
        {
            using (var dialog = new ThemedForm {
                Text = caption, Size = new Size(700, 360),
                MinimumSize = new Size(500, 260), StartPosition = FormStartPosition.CenterParent,
                MinimizeBox = false, MaximizeBox = false, ShowInTaskbar = false
            })
            {
                var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), RowCount = 2, ColumnCount = 1 };
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
                layout.Controls.Add(new TextBox {
                    Text = text, Multiline = true, ReadOnly = true, BorderStyle = BorderStyle.None,
                    ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, TabStop = false,
                    ForeColor = icon == MessageBoxIcon.Error ? AppTheme.Current.Error : AppTheme.Current.Text
                });
                var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Tag = "panel" };
                if (buttons == MessageBoxButtons.OKCancel)
                {
                    var cancel = new RoundedButton { Text = "Abbrechen", DialogResult = DialogResult.Cancel, AutoSize = true };
                    actions.Controls.Add(cancel);
                    dialog.CancelButton = cancel;
                }
                var ok = new RoundedButton { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true, Tag = "primary" };
                actions.Controls.Add(ok);
                dialog.AcceptButton = ok;
                if (buttons == MessageBoxButtons.OK) dialog.CancelButton = ok;
                layout.Controls.Add(actions);
                dialog.Controls.Add(layout);
                return dialog.ShowDialog(owner);
            }
        }
    }
}
