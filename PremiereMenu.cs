using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace SpaceMouseBridge
{
    public sealed class RadialMenuConfiguration
    {
        public PremiereMenuItem[] Items { get; set; }
        public int Radius { get; set; }
        public int CenterPercent { get; set; }
        public decimal BorderWidth { get; set; }
        public decimal SegmentGap { get; set; }
        public decimal TextSize { get; set; }
        private decimal iconSize = 24m;
        public decimal IconSize { get { return iconSize; } set { iconSize = value; } }

        public RadialMenuConfiguration()
        {
            Radius = 77; CenterPercent = 33; BorderWidth = 1; SegmentGap = 2; TextSize = 6;
        }

        internal void Validate()
        {
            if (Radius < 40 || Radius > 200 || CenterPercent < 15 || CenterPercent > 60 ||
                BorderWidth < 0.5m || BorderWidth > 5 || SegmentGap < 0 || SegmentGap > 8 ||
                TextSize < 4 || TextSize > 16 || IconSize < 8 || IconSize > 48)
                throw new InvalidDataException("Menue 2: ungueltige Darstellungsparameter.");
            PremiereMenuItem.ValidateConfiguration(Items);
        }
    }

    public sealed class PremiereMenuItem
    {
        public string Name { get; set; }
        public PremiereShortcut Shortcut { get; set; }
        public MenuSegmentDisplay Display { get; set; }
        public MenuIcon Icon { get; set; }

        internal static void Validate(PremiereMenuItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Name) ||
                item.Name.Any(char.IsControl) || item.Shortcut == null)
                throw new InvalidDataException("Jeder Menueeintrag benoetigt einen Namen ohne Zeilenumbrueche und ein Tastenkuerzel.");
            item.Shortcut.Validate();
            if (!Enum.IsDefined(typeof(MenuSegmentDisplay), item.Display) || !Enum.IsDefined(typeof(MenuIcon), item.Icon))
                throw new InvalidDataException("Unbekannte Segmentdarstellung oder unbekanntes Icon.");
            if (item.Display == MenuSegmentDisplay.Icon && item.Icon == MenuIcon.None)
                throw new InvalidDataException("Bitte fuer das Icon-Segment ein Icon auswaehlen.");
        }

        internal static void ValidateConfiguration(PremiereMenuItem[] items)
        {
            if (items == null || items.Length < 4 || items.Length > 8)
                throw new InvalidDataException("Bitte 4 bis 8 Segmente konfigurieren.");
            for (int i = 0; i < items.Length; i++)
            {
                try { Validate(items[i]); }
                catch (InvalidDataException ex)
                {
                    throw new InvalidDataException("Segment " + (i + 1) + ": " + ex.Message, ex);
                }
            }
        }
    }

    internal sealed class PremiereMenuEditor : ThemedForm
    {
        private readonly List<PremiereMenuItem> draft = new List<PremiereMenuItem>();
        private readonly NumericUpDown segmentCount = new NumericUpDown {
            Minimum = 4, Maximum = 8, Value = 4, Width = 64
        };
        private readonly RadialMenuView preview = new RadialMenuView();
        private readonly NumericUpDown radius = new NumericUpDown { Minimum = 40, Maximum = 180, Value = 77, Width = 70 };
        private readonly TrackBar radiusSlider = new TrackBar { Minimum = 40, Maximum = 180, Value = 77,
            TickFrequency = 20, Width = 150, Height = 36 };
        private readonly NumericUpDown centerPercent = new NumericUpDown { Minimum = 15, Maximum = 60, Value = 33, Width = 70 };
        private readonly TrackBar centerSlider = new TrackBar { Minimum = 15, Maximum = 60, Value = 33,
            TickFrequency = 5, Width = 150, Height = 36 };
        private readonly NumericUpDown borderWidth = new NumericUpDown { Minimum = 0.5m, Maximum = 5,
            DecimalPlaces = 1, Increment = 0.5m, Value = 1, Width = 70 };
        private readonly TrackBar borderSlider = new TrackBar { Minimum = 5, Maximum = 50, Value = 10,
            TickFrequency = 5, Width = 150, Height = 36 };
        private readonly NumericUpDown segmentGap = new NumericUpDown { Minimum = 0, Maximum = 8,
            DecimalPlaces = 1, Increment = 0.5m, Value = 2, Width = 70 };
        private readonly TrackBar gapSlider = new TrackBar { Minimum = 0, Maximum = 80, Value = 20,
            TickFrequency = 10, Width = 150, Height = 36 };
        private readonly NumericUpDown textSize = new NumericUpDown { Minimum = 4, Maximum = 16,
            DecimalPlaces = 1, Increment = 0.5m, Value = 6, Width = 70 };
        private readonly TrackBar textSlider = new TrackBar { Minimum = 40, Maximum = 160, Value = 60,
            TickFrequency = 20, Dock = DockStyle.Fill };
        private readonly Label sizeLabel = new Label { AutoSize = true, Padding = new Padding(0, 5, 0, 0) };
        private decimal configuredTextSize, configuredIconSize;
        private bool syncingSize;
        private readonly TextBox entryName = new TextBox { Dock = DockStyle.Fill };
        private readonly ComboBox displayMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        private readonly MenuIconPicker iconPicker = new MenuIconPicker();
        private readonly RoundedButton capture = new RoundedButton { AutoSize = true, Text = "Kuerzel aufzeichnen" };
        private readonly Label segmentLabel = new Label { AutoSize = true };
        private readonly Label feedback = new Label { Dock = DockStyle.Fill, ForeColor = Color.DarkRed };
        private int selected;
        private bool loading;
        internal PremiereMenuItem[] Items;
        internal int MenuRadius { get { return (int)radius.Value; } }
        internal int MenuCenterPercent { get { return (int)centerPercent.Value; } }
        internal decimal MenuBorderWidth { get { return borderWidth.Value; } }
        internal decimal MenuSegmentGap { get { return segmentGap.Value; } }
        internal decimal MenuTextSize { get { return configuredTextSize; } }
        internal decimal MenuIconSize { get { return configuredIconSize; } }
        internal RadialMenuConfiguration CollectConfiguration()
        {
            return new RadialMenuConfiguration { Items = Collect(), Radius = MenuRadius,
                CenterPercent = MenuCenterPercent, BorderWidth = MenuBorderWidth,
                SegmentGap = MenuSegmentGap, TextSize = MenuTextSize, IconSize = MenuIconSize };
        }

        internal PremiereMenuEditor(PremiereMenuItem[] items, string profileName = "Premiere", int menuRadius = 77,
            int menuCenterPercent = 33, decimal menuBorderWidth = 1m, decimal menuSegmentGap = 2m, decimal menuTextSize = 6m,
            decimal menuIconSize = 24m)
        {
            menuRadius = Math.Min(180, menuRadius);
            radius.Value = menuRadius;
            radiusSlider.Value = menuRadius;
            preview.MenuRadius = menuRadius;
            centerPercent.Value = menuCenterPercent;
            centerSlider.Value = menuCenterPercent;
            preview.CenterPercent = menuCenterPercent;
            borderWidth.Value = menuBorderWidth;
            borderSlider.Value = (int)(menuBorderWidth * 10);
            preview.BorderWidth = (float)menuBorderWidth;
            segmentGap.Value = menuSegmentGap;
            gapSlider.Value = (int)(menuSegmentGap * 10);
            preview.SegmentGap = (float)menuSegmentGap;
            textSize.Value = menuTextSize;
            textSlider.Value = (int)(menuTextSize * 10);
            preview.TextSize = (float)menuTextSize;
            configuredTextSize = menuTextSize;
            configuredIconSize = menuIconSize;
            preview.IconSize = (float)menuIconSize;
            Text = profileName + " - Radialmenue konfigurieren";
            Size = new Size(1000, 740); MinimumSize = new Size(960, 720);
            StartPosition = FormStartPosition.CenterParent;
            foreach (var item in items ?? new PremiereMenuItem[0])
                draft.Add(new PremiereMenuItem { Name = item.Name, Shortcut = item.Shortcut, Display = item.Display, Icon = item.Icon });
            while (draft.Count < 4) draft.Add(new PremiereMenuItem());
            segmentCount.Value = Math.Min(8, draft.Count);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 5 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            layout.Controls.Add(new Label { Dock = DockStyle.Fill, Text =
                "4 bis 8 feste Segmente ohne Seitenwechsel. Ein Segment direkt anklicken und rechts belegen.\r\n" +
                "Alle Segmente benoetigen Name und Kuerzel. Erst Speichern im Mapping-Editor aktiviert den Entwurf." }, 0, 0);
            var countRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 8, RowCount = 2 };
            countRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            countRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
            countRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
            countRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            countRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
            countRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
            countRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            countRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
            countRow.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            countRow.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            countRow.Controls.Add(new Label { Text = "Anzahl Segmente", AutoSize = true, Padding = new Padding(0, 5, 0, 0) }, 0, 0);
            countRow.Controls.Add(segmentCount, 1, 0);
            var textControls = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
            textControls.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60));
            textControls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            textControls.Controls.Add(sizeLabel, 0, 0);
            textControls.Controls.Add(textSlider, 1, 0);
            countRow.Controls.Add(textControls, 0, 1);
            countRow.Controls.Add(textSize, 1, 1);
            countRow.Controls.Add(new Label { Text = "Radius (Pixel)", AutoSize = true, Padding = new Padding(0, 5, 0, 0) }, 2, 0);
            countRow.Controls.Add(radiusSlider, 3, 0);
            countRow.Controls.Add(radius, 4, 0);
            countRow.Controls.Add(new Label { Text = "Mittelkreis (%)", AutoSize = true, Padding = new Padding(0, 5, 0, 0) }, 5, 0);
            countRow.Controls.Add(centerSlider, 6, 0);
            countRow.Controls.Add(centerPercent, 7, 0);
            countRow.Controls.Add(new Label { Text = "Konturlinie (Pixel)", AutoSize = true, Padding = new Padding(0, 5, 0, 0) }, 2, 1);
            countRow.Controls.Add(borderSlider, 3, 1);
            countRow.Controls.Add(borderWidth, 4, 1);
            countRow.Controls.Add(new Label { Text = "Segmentabstand (Pixel)", AutoSize = true, Padding = new Padding(0, 5, 0, 0) }, 5, 1);
            countRow.Controls.Add(gapSlider, 6, 1);
            countRow.Controls.Add(segmentGap, 7, 1);
            foreach (var slider in new[] { radiusSlider, centerSlider, borderSlider, gapSlider })
                slider.Dock = DockStyle.Fill;
            layout.Controls.Add(countRow, 0, 1);
            var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 450));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var previewHost = new Panel { Dock = DockStyle.Fill };
            var previewCanvas = new Panel { Size = new Size(440, 440) };
            Action centerPreview = delegate {
                preview.Location = new Point(220 - preview.Width / 2, 235 - preview.Height / 2);
            };
            preview.SizeChanged += delegate { centerPreview(); };
            centerPreview();
            preview.EditorMode = true;
            previewCanvas.Controls.Add(preview);
            previewCanvas.Controls.Add(new Label { Text = "Originalgroesse (1:1)", AutoSize = true, Location = new Point(10, 0) });
            previewHost.Controls.Add(previewCanvas);
            body.Controls.Add(previewHost, 0, 0);
            var fields = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 9 };
            foreach (int height in new[] { 28, 24, 38, 24, 46, 24, 38, 24, 44 })
                fields.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            fields.Controls.Add(segmentLabel, 0, 0);
            fields.Controls.Add(new Label { Text = "Name / Beschreibung (auch bei Icons erforderlich)", AutoSize = true }, 0, 1);
            fields.Controls.Add(entryName, 0, 2);
            fields.Controls.Add(new Label { Text = "Tastenkuerzel", AutoSize = true }, 0, 3);
            var captureRow = new FlowLayoutPanel { Dock = DockStyle.Fill };
            captureRow.Controls.Add(capture);
            fields.Controls.Add(captureRow, 0, 4);
            displayMode.Items.AddRange(new object[] { "Text", "Icon" });
            fields.Controls.Add(new Label { Text = "Darstellung im Segment", AutoSize = true }, 0, 5);
            fields.Controls.Add(displayMode, 0, 6);
            fields.Controls.Add(new Label { Text = "Icon (eigener Schnittwerkzeug-Satz)", AutoSize = true }, 0, 7);
            fields.Controls.Add(iconPicker, 0, 8);
            body.Controls.Add(fields, 1, 0);
            layout.Controls.Add(body, 0, 2);
            layout.Controls.Add(feedback, 0, 3);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var cancel = new RoundedButton { Text = "Abbrechen", AutoSize = true, DialogResult = DialogResult.Cancel };
            var accept = new RoundedButton { Text = "Entwurf uebernehmen", AutoSize = true, Tag = "primary" };
            actions.Controls.AddRange(new Control[] { cancel, accept });
            layout.Controls.Add(actions, 0, 4);
            Controls.Add(layout); CancelButton = cancel;
            preview.SlotClicked += SelectSegment;
            segmentCount.ValueChanged += delegate { ChangeCount(); };
            radius.ValueChanged += delegate {
                radiusSlider.Value = MenuRadius;
                preview.MenuRadius = MenuRadius;
            };
            radiusSlider.ValueChanged += delegate { radius.Value = radiusSlider.Value; };
            centerPercent.ValueChanged += delegate {
                centerSlider.Value = MenuCenterPercent;
                preview.CenterPercent = MenuCenterPercent;
            };
            centerSlider.ValueChanged += delegate { centerPercent.Value = centerSlider.Value; };
            borderWidth.ValueChanged += delegate {
                borderSlider.Value = (int)(MenuBorderWidth * 10);
                preview.BorderWidth = (float)MenuBorderWidth;
            };
            borderSlider.ValueChanged += delegate { borderWidth.Value = borderSlider.Value / 10m; };
            segmentGap.ValueChanged += delegate {
                gapSlider.Value = (int)(MenuSegmentGap * 10);
                preview.SegmentGap = (float)MenuSegmentGap;
            };
            gapSlider.ValueChanged += delegate { segmentGap.Value = gapSlider.Value / 10m; };
            textSize.ValueChanged += delegate {
                if (syncingSize) return;
                if (draft[selected].Display == MenuSegmentDisplay.Icon)
                {
                    configuredIconSize = textSize.Value;
                    preview.IconSize = (float)MenuIconSize;
                }
                else
                {
                    configuredTextSize = textSize.Value;
                    preview.TextSize = (float)MenuTextSize;
                }
                textSlider.Value = (int)(textSize.Value * 10);
            };
            textSlider.ValueChanged += delegate { if (!syncingSize) textSize.Value = textSlider.Value / 10m; };
            displayMode.SelectedIndexChanged += delegate {
                if (loading) return;
                draft[selected].Display = (MenuSegmentDisplay)displayMode.SelectedIndex;
                iconPicker.Enabled = draft[selected].Display == MenuSegmentDisplay.Icon;
                SyncSizeControl();
                RefreshPreview();
            };
            iconPicker.SelectedIndexChanged += delegate {
                if (loading) return;
                draft[selected].Icon = (MenuIcon)iconPicker.SelectedItem;
                RefreshPreview();
            };
            entryName.TextChanged += delegate {
                if (loading) return;
                draft[selected].Name = entryName.Text;
                RefreshPreview();
            };
            capture.Click += delegate {
                using (var dialog = new PremiereMappingEditor.ShortcutCapture())
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    draft[selected].Shortcut = dialog.Shortcut;
                    SelectSegment(selected);
                }
            };
            accept.Click += delegate {
                try { Items = Collect(); DialogResult = DialogResult.OK; Close(); }
                catch (InvalidDataException ex) { feedback.Text = ex.Message; }
            };
            SelectSegment(0);
        }

        private void SyncSizeControl()
        {
            bool icon = draft[selected].Display == MenuSegmentDisplay.Icon;
            syncingSize = true;
            try
            {
                sizeLabel.Text = icon ? "Icon (px)" : "Text (pt)";
                textSize.Minimum = 4;
                textSize.Maximum = 48;
                textSlider.Minimum = 40;
                textSlider.Maximum = 480;
                textSize.Value = icon ? MenuIconSize : MenuTextSize;
                textSlider.Value = (int)(textSize.Value * 10);
                textSize.Minimum = icon ? 8 : 4;
                textSize.Maximum = icon ? 48 : 16;
                textSlider.Minimum = icon ? 80 : 40;
                textSlider.Maximum = icon ? 480 : 160;
                textSlider.TickFrequency = icon ? 40 : 20;
            }
            finally { syncingSize = false; }
        }

        private void SelectSegment(int index)
        {
            if (index < 0 || index >= draft.Count) return;
            selected = index;
            loading = true;
            entryName.Text = draft[index].Name ?? "";
            displayMode.SelectedIndex = (int)draft[index].Display;
            iconPicker.SelectedItem = draft[index].Icon;
            iconPicker.Enabled = draft[index].Display == MenuSegmentDisplay.Icon;
            capture.Text = draft[index].Shortcut == null ? "Kuerzel aufzeichnen" : draft[index].Shortcut + " - aendern";
            segmentLabel.Text = "Segment " + (index + 1) + " von " + draft.Count;
            loading = false;
            SyncSizeControl();
            preview.SelectedSlot = selected;
            RefreshPreview();
        }

        private void ChangeCount()
        {
            if (loading) return;
            int count = (int)segmentCount.Value;
            if (count < draft.Count && draft.Skip(count).Any(i => !string.IsNullOrWhiteSpace(i.Name) ||
                i.Shortcut != null || i.Display != MenuSegmentDisplay.Text || i.Icon != MenuIcon.None) &&
                MessageBox.Show(this, "Die letzten " + (draft.Count - count) +
                    " Segmente samt Belegung aus dem Entwurf entfernen?", "Segmentanzahl reduzieren",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                loading = true;
                segmentCount.Value = Math.Min(8, draft.Count);
                loading = false;
                return;
            }
            if (count < draft.Count) draft.RemoveRange(count, draft.Count - count);
            while (draft.Count < count) draft.Add(new PremiereMenuItem());
            SelectSegment(Math.Min(selected, count - 1));
        }

        private void RefreshPreview()
        {
            preview.Items = draft.ToArray();
            preview.Invalidate();
            int missing = draft.Count(i => string.IsNullOrWhiteSpace(i.Name) || i.Shortcut == null ||
                i.Display == MenuSegmentDisplay.Icon && i.Icon == MenuIcon.None);
            feedback.Text = draft.Count > 8 ? "Vorhandenes Menue: " + draft.Count +
                " Eintraege. Bitte bewusst auf 4 bis 8 reduzieren; nichts wird automatisch geloescht." :
                missing == 0 ? "" : missing + " Segmente noch unvollstaendig. Bitte alle Segmente belegen.";
        }

        internal PremiereMenuItem[] Collect()
        {
            var result = draft.Select(i => new PremiereMenuItem {
                Name = (i.Name ?? "").Trim(), Shortcut = i.Shortcut, Display = i.Display, Icon = i.Icon
            }).ToArray();
            PremiereMenuItem.ValidateConfiguration(result);
            return result;
        }
    }

    internal sealed class RadialMenuView : Control
    {
        internal const int Diameter = 164;
        private int menuRadius = 77;
        internal int MenuRadius
        {
            get { return menuRadius; }
            set
            {
                if (value < 40 || value > 180) throw new ArgumentOutOfRangeException("value");
                menuRadius = value;
                Size = new Size(value * 2 + 10, value * 2 + 10);
                Invalidate();
            }
        }
        private const float Radius = 77;
        private float iconSize = 24;
        internal float IconSize
        {
            get { return iconSize; }
            set
            {
                if (float.IsNaN(value) || value < 8 || value > 48) throw new ArgumentOutOfRangeException("value");
                iconSize = value;
                Invalidate();
            }
        }
        private float textSize = 6;
        internal float TextSize
        {
            get { return textSize; }
            set
            {
                if (float.IsNaN(value) || value < 4 || value > 16) throw new ArgumentOutOfRangeException("value");
                textSize = value;
                Invalidate();
            }
        }
        private float segmentGap = 2;
        internal float SegmentGap
        {
            get { return segmentGap; }
            set
            {
                if (float.IsNaN(value) || value < 0 || value > 8) throw new ArgumentOutOfRangeException("value");
                segmentGap = value;
                Invalidate();
            }
        }
        private float borderWidth = 1;
        internal float BorderWidth
        {
            get { return borderWidth; }
            set
            {
                if (float.IsNaN(value) || value < 0.5f || value > 5) throw new ArgumentOutOfRangeException("value");
                borderWidth = value;
                Invalidate();
            }
        }
        private int centerPercent = 33;
        internal int CenterPercent
        {
            get { return centerPercent; }
            set
            {
                if (value < 15 || value > 60) throw new ArgumentOutOfRangeException("value");
                centerPercent = value;
                Invalidate();
            }
        }
        private float CenterRadius { get { return Radius * CenterPercent / 100f; } }
        private float InnerRadius { get { return CenterRadius + 2; } }
        internal PremiereMenuItem[] Items = new PremiereMenuItem[0];
        internal bool EditorMode;
        internal int SelectedSlot = -1;
        internal event Action<int> SlotClicked;
        private int highlighted = -1;
        internal int SegmentCount { get { return Math.Max(4, Items.Length); } }

        internal RadialMenuView()
        {
            Size = new Size(Diameter, Diameter);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer, true);
        }

        internal int SlotAt(Point point)
        {
            double scale = MenuRadius / 77.0;
            double dx = (point.X - Width / 2) / scale, dy = (point.Y - Height / 2) / scale;
            double radius = Math.Sqrt(dx * dx + dy * dy);
            if (radius < InnerRadius || radius > Radius) return -1;
            double sweep = 360.0 / SegmentCount;
            double angle = Math.Atan2(dy, dx) * 180 / Math.PI + 90 + sweep / 2;
            int slot = (int)(((angle % 360 + 360) % 360) / sweep);
            using (var path = Sector(Point.Empty, (float)(-90 - sweep / 2 + slot * sweep), (float)sweep))
                return path.IsVisible((float)dx, (float)dy) ? slot : -1;
        }

        internal Region CreateMenuRegion()
        {
            using (var outline = new GraphicsPath())
            using (var transform = new Matrix())
            {
                float sweep = 360f / SegmentCount;
                for (int slot = 0; slot < SegmentCount; slot++)
                    using (var sector = Sector(Point.Empty, -90 - sweep / 2 + slot * sweep, sweep))
                        outline.AddPath(sector, false);
                outline.AddEllipse(-CenterRadius, -CenterRadius, CenterRadius * 2, CenterRadius * 2);
                transform.Translate(Width / 2f, Height / 2f);
                transform.Scale(MenuRadius / 77f, MenuRadius / 77f);
                outline.Transform(transform);
                return new Region(outline);
            }
        }

        protected override void OnMouseMove(MouseEventArgs args)
        {
            base.OnMouseMove(args);
            int slot = SlotAt(args.Location);
            if (slot == highlighted) return;
            highlighted = slot;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs args)
        {
            base.OnMouseLeave(args);
            highlighted = -1;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs args)
        {
            base.OnMouseDown(args);
            if (args.Button == MouseButtons.Left && SlotClicked != null) SlotClicked(SlotAt(args.Location));
        }

        protected override void OnPaint(PaintEventArgs args)
        {
            base.OnPaint(args);
            Graphics graphics = args.Graphics;
            float scale = MenuRadius / 77f;
            graphics.TranslateTransform(Width / 2f, Height / 2f);
            graphics.ScaleTransform(scale, scale);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            bool highContrast = AppTheme.Current.HighContrast;
            Color surface = AppTheme.Current.Surface;
            Color border = highContrast ? SystemColors.WindowText : AppTheme.Current.ControlBorder;
            Color accent = highContrast ? SystemColors.Highlight : Color.FromArgb(40, 124, 222);
            Color text = highContrast ? SystemColors.WindowText : Color.White;
            Color centerColor = surface;
            var center = Point.Empty;
            float sweep = 360f / SegmentCount;
            using (var font = new Font("Segoe UI", TextSize, FontStyle.Regular))
            using (var format = new StringFormat { Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter })
            {
                for (int slot = 0; slot < SegmentCount; slot++)
                {
                    bool selected = EditorMode ? slot == SelectedSlot : slot == highlighted;
                    using (GraphicsPath path = Sector(center, -90 - sweep / 2 + slot * sweep, sweep))
                    using (var fill = new SolidBrush(selected ? accent : surface))
                    using (var pen = new Pen(border, BorderWidth * 2 / scale))
                    {
                        graphics.FillPath(fill, path);
                        var state = graphics.Save();
                        graphics.SetClip(path, CombineMode.Intersect);
                        graphics.DrawPath(pen, path);
                        graphics.Restore(state);
                    }
                    bool assigned = slot < Items.Length && !string.IsNullOrWhiteSpace(Items[slot].Name);
                    string label = assigned ? Items[slot].Name : EditorMode ? "Belegen" : "";
                    float labelRadius = (InnerRadius + Radius) / 2;
                    PointF position = PointOnCircle(center, labelRadius, -90 + slot * sweep);
                    float width = Math.Min(58, 2 * labelRadius * (float)Math.Sin(sweep * Math.PI / 360) - 4);
                    Color contentColor = selected && highContrast ? SystemColors.HighlightText : text;
                    using (var path = Sector(center, -90 - sweep / 2 + slot * sweep, sweep))
                    {
                        var state = graphics.Save();
                        graphics.SetClip(path, CombineMode.Intersect);
                        if (slot < Items.Length && Items[slot].Display == MenuSegmentDisplay.Icon &&
                            Items[slot].Icon != MenuIcon.None)
                        {
                            float size = Math.Min(IconSize, Math.Min(width, Radius - InnerRadius - 6));
                            MenuIcons.Draw(graphics, Items[slot].Icon,
                                new RectangleF(position.X - size / 2, position.Y - size / 2, size, size), contentColor);
                        }
                        else
                            using (var brush = new SolidBrush(contentColor))
                                graphics.DrawString(label, font, brush,
                                    new RectangleF(position.X - width / 2, position.Y - 13, width, 26), format);
                        graphics.Restore(state);
                    }
                }
                using (var brush = new SolidBrush(centerColor))
                using (var pen = new Pen(border))
                {
                    graphics.FillEllipse(brush, center.X - CenterRadius, center.Y - CenterRadius, CenterRadius * 2, CenterRadius * 2);
                    graphics.DrawEllipse(pen, center.X - CenterRadius, center.Y - CenterRadius, CenterRadius * 2, CenterRadius * 2);
                }
                float iconRadius = CenterRadius * 0.3f;
                using (var pen = new Pen(text, 1.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    graphics.DrawLine(pen, -iconRadius, -iconRadius, iconRadius, iconRadius);
                    graphics.DrawLine(pen, -iconRadius, iconRadius, iconRadius, -iconRadius);
                }
            }
        }

        private static PointF PointOnCircle(Point center, float radius, float angle)
        {
            double radians = angle * Math.PI / 180;
            return new PointF(center.X + (float)Math.Cos(radians) * radius,
                center.Y + (float)Math.Sin(radians) * radius);
        }

        private GraphicsPath Sector(Point center, float start, float sweep)
        {
            // Offset both radial edges by half the pixel gap, not by a fixed angle.
            double halfGap = SegmentGap / 2.0 * 77 / MenuRadius;
            float inner = Math.Max(InnerRadius, (float)(halfGap / Math.Sin(sweep * Math.PI / 360)) + 0.001f);
            float outerOffset = (float)(Math.Asin(halfGap / Radius) * 180 / Math.PI);
            float innerOffset = (float)(Math.Asin(halfGap / inner) * 180 / Math.PI);
            var path = new GraphicsPath();
            path.AddLine(PointOnCircle(center, inner, start + innerOffset),
                PointOnCircle(center, Radius, start + outerOffset));
            path.AddArc(center.X - Radius, center.Y - Radius, Radius * 2, Radius * 2,
                start + outerOffset, sweep - 2 * outerOffset);
            path.AddLine(PointOnCircle(center, Radius, start + sweep - outerOffset),
                PointOnCircle(center, inner, start + sweep - innerOffset));
            path.AddArc(center.X - inner, center.Y - inner, inner * 2, inner * 2,
                start + sweep - innerOffset, -sweep + 2 * innerOffset);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class BridgeRadialMenu : Form
    {
        internal PremiereShortcut SelectedShortcut;
        internal string CloseReason;
        private readonly RadialMenuView view;
        private readonly Timer guardTimer;
        private bool escapeWasDown;
        private bool finished;

        internal BridgeRadialMenu(PremiereMenuItem[] menuItems, Point position, IntPtr target, uint targetProcess,
            int menuRadius = 77, int menuCenterPercent = 33, decimal menuBorderWidth = 1m, decimal menuSegmentGap = 2m,
            decimal menuTextSize = 6m, decimal menuIconSize = 24m)
        {
            var items = menuItems ?? new PremiereMenuItem[0];
            foreach (PremiereMenuItem item in items) PremiereMenuItem.Validate(item);
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = AppTheme.Current.Background;
            view = new RadialMenuView { Items = items, MenuRadius = Math.Min(180, menuRadius), CenterPercent = menuCenterPercent,
                BorderWidth = (float)menuBorderWidth, SegmentGap = (float)menuSegmentGap,
                TextSize = (float)menuTextSize, IconSize = (float)menuIconSize, BackColor = BackColor };
            ClientSize = view.Size;
            // A shaped window avoids antialiasing against a chroma-key colour.
            Region = view.CreateMenuRegion();
            view.Dock = DockStyle.Fill;
            view.SlotClicked += ActivateSlot;
            Controls.Add(view);
            Rectangle area = Screen.FromPoint(position).WorkingArea;
            Location = new Point(Math.Max(area.Left, Math.Min(position.X - Width / 2, area.Right - Width)),
                Math.Max(area.Top, Math.Min(position.Y - Height / 2, area.Bottom - Height)));
            guardTimer = new Timer { Interval = 30 };
            guardTimer.Tick += delegate {
                bool escapeDown = (Native.GetAsyncKeyState(0x1b) & 0x8000) != 0;
                if (escapeDown && !escapeWasDown) Finish(null, "Escape");
                escapeWasDown = escapeDown;
                uint foregroundProcess, bridgeProcess;
                IntPtr foreground = Native.GetForegroundWindow();
                Native.GetWindowThreadProcessId(foreground, out foregroundProcess);
                Native.GetWindowThreadProcessId(Handle, out bridgeProcess);
                if (foregroundProcess != bridgeProcess && (foreground != target || foregroundProcess != targetProcess))
                    Finish(null, "Fokusverlust");
            };
            Shown += delegate { guardTimer.Start(); };
        }

        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                parameters.ExStyle |= 0x08000000 | 0x00000080 | 0x00000008;
                return parameters;
            }
        }

        internal void ActivateSlot(int slot)
        {
            if (slot < 0) { Finish(null, "Klick ausserhalb"); return; }
            if (slot < view.Items.Length) Finish(view.Items[slot].Shortcut, "Auswahl");
        }

        private void Finish(PremiereShortcut shortcut, string reason)
        {
            if (finished) return;
            finished = true;
            SelectedShortcut = shortcut;
            CloseReason = reason;
            Close();
        }

        protected override void OnFormClosing(FormClosingEventArgs args)
        {
            base.OnFormClosing(args);
            if (args.Cancel) return;
            if (!finished) { finished = true; CloseReason = "Geschlossen"; }
            guardTimer.Stop();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) guardTimer.Dispose();
            base.Dispose(disposing);
        }
    }
}
