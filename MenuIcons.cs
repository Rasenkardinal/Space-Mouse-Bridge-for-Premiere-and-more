using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SpaceMouseBridge
{
    public enum MenuSegmentDisplay { Text, Icon }
    public enum MenuIcon
    {
        None, Selection, TrackSelect, Razor, Hand, Zoom, Text, Pen,
        PlayPause, Stop, StepBack, StepForward, Marker, Undo, Redo, Save,
        RippleEdit, RollingEdit, Slip, Slide, MarkIn, MarkOut, Insert, Overwrite,
        Link, Magnet, Mute, Split, Trim, Play, Pause, Render, Remove, CloseGap
    }

    internal static class MenuIcons
    {
        internal static string Label(MenuIcon icon)
        {
            switch (icon)
            {
                case MenuIcon.None: return "Icon auswaehlen";
                case MenuIcon.Selection: return "Auswahl";
                case MenuIcon.TrackSelect: return "Spurauswahl";
                case MenuIcon.Razor: return "CUT / Rasierklinge";
                case MenuIcon.Split: return "Clip teilen";
                case MenuIcon.Trim: return "Trimmen";
                case MenuIcon.Play: return "Play / Wiedergabe";
                case MenuIcon.Pause: return "Pause";
                case MenuIcon.Render: return "Rendern";
                case MenuIcon.Remove: return "Entfernen";
                case MenuIcon.CloseGap: return "Luecke schliessen";
                case MenuIcon.Hand: return "Verschieben / Hand";
                case MenuIcon.Zoom: return "Zoom / Lupe";
                case MenuIcon.Text: return "Textwerkzeug";
                case MenuIcon.Pen: return "Stift";
                case MenuIcon.PlayPause: return "Wiedergabe / Pause";
                case MenuIcon.Stop: return "Stopp";
                case MenuIcon.StepBack: return "Schritt zurueck";
                case MenuIcon.StepForward: return "Schritt vor";
                case MenuIcon.Marker: return "Marker";
                case MenuIcon.Undo: return "Rueckgaengig";
                case MenuIcon.Redo: return "Wiederholen";
                case MenuIcon.Save: return "Speichern";
                case MenuIcon.RippleEdit: return "Ripple Edit / Schnittkante verschieben";
                case MenuIcon.RollingEdit: return "Rolling Edit / Schnitt rollen";
                case MenuIcon.Slip: return "Slip / Clipinhalt verschieben";
                case MenuIcon.Slide: return "Slide / Clip verschieben";
                case MenuIcon.MarkIn: return "In-Punkt setzen";
                case MenuIcon.MarkOut: return "Out-Punkt setzen";
                case MenuIcon.Insert: return "Einfuegen";
                case MenuIcon.Overwrite: return "Ueberschreiben";
                case MenuIcon.Link: return "Verknuepfen";
                case MenuIcon.Magnet: return "Magnet / Einrasten";
                case MenuIcon.Mute: return "Audio stumm";
                default: throw new ArgumentOutOfRangeException("icon");
            }
        }

        internal static void Draw(Graphics graphics, MenuIcon icon, RectangleF bounds, Color color)
        {
            if (!Enum.IsDefined(typeof(MenuIcon), icon)) throw new ArgumentOutOfRangeException("icon");
            if (icon == MenuIcon.None) return;
            var state = graphics.Save();
            try
            {
                graphics.TranslateTransform(bounds.X, bounds.Y);
                graphics.ScaleTransform(bounds.Width / 24, bounds.Height / 24);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var pen = new Pen(color, 1.6f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    switch (icon)
                    {
                        case MenuIcon.Selection:
                            Lines(graphics, pen, 5, 3, 6, 20, 10, 15, 15, 21, 18, 19, 13, 13, 20, 12, 5, 3);
                            break;
                        case MenuIcon.TrackSelect:
                            Lines(graphics, pen, 3, 5, 15, 5, 15, 9, 3, 9, 3, 5);
                            Lines(graphics, pen, 3, 14, 15, 14, 15, 18, 3, 18, 3, 14);
                            Arrow(graphics, pen, 17, 12, 22, 12); break;
                        case MenuIcon.Razor:
                            Lines(graphics, pen, 8, 3, 21, 16, 16, 21, 3, 8, 8, 3);
                            Lines(graphics, pen, 5, 6, 18, 19);
                            Lines(graphics, pen, 10, 9, 15, 14);
                            Lines(graphics, pen, 11, 8, 9, 10);
                            Lines(graphics, pen, 16, 13, 14, 15); break;
                        case MenuIcon.Split:
                            Lines(graphics, pen, 9, 5, 3, 5, 3, 19, 9, 19);
                            Lines(graphics, pen, 15, 5, 21, 5, 21, 19, 15, 19);
                            Lines(graphics, pen, 12, 2, 12, 22);
                            Lines(graphics, pen, 9, 11, 12, 14, 15, 11); break;
                        case MenuIcon.Trim:
                            Lines(graphics, pen, 8, 3, 8, 21);
                            Lines(graphics, pen, 16, 3, 16, 21);
                            Lines(graphics, pen, 3, 8, 8, 12, 3, 16);
                            Lines(graphics, pen, 21, 8, 16, 12, 21, 16); break;
                        case MenuIcon.Play:
                            Lines(graphics, pen, 7, 3, 21, 12, 7, 21, 7, 3); break;
                        case MenuIcon.Pause:
                            RoundedRectangle(graphics, pen, 5, 3, 4, 18, 1);
                            RoundedRectangle(graphics, pen, 15, 3, 4, 18, 1); break;
                        case MenuIcon.Render:
                            RoundedRectangle(graphics, pen, 2, 5, 20, 15, 2);
                            Lines(graphics, pen, 2, 9, 22, 9);
                            Lines(graphics, pen, 6, 5, 9, 9);
                            Lines(graphics, pen, 13, 5, 16, 9);
                            Lines(graphics, pen, 13, 11, 9, 16, 13, 16, 11, 20, 17, 14, 13, 14, 15, 11); break;
                        case MenuIcon.Remove:
                            Lines(graphics, pen, 3, 6, 21, 6);
                            Lines(graphics, pen, 9, 6, 9, 3, 15, 3, 15, 6);
                            Lines(graphics, pen, 5, 6, 6, 21, 18, 21, 19, 6);
                            Lines(graphics, pen, 10, 10, 10, 17);
                            Lines(graphics, pen, 14, 10, 14, 17); break;
                        case MenuIcon.CloseGap:
                            graphics.DrawRectangle(pen, 3, 5, 5, 14);
                            graphics.DrawRectangle(pen, 16, 5, 5, 14);
                            Lines(graphics, pen, 9, 12, 15, 12);
                            Lines(graphics, pen, 11, 9, 8, 12, 11, 15);
                            Lines(graphics, pen, 13, 9, 16, 12, 13, 15); break;
                        case MenuIcon.Hand:
                            Lines(graphics, pen, 6, 13, 4, 11, 2, 13, 8, 21, 17, 21, 21, 15, 21, 9,
                                18, 9, 18, 13, 18, 6, 15, 6, 15, 12, 15, 3, 12, 3, 12, 12, 12, 5, 9, 5, 9, 14, 6, 10, 6, 13);
                            break;
                        case MenuIcon.Zoom:
                            graphics.DrawEllipse(pen, 3, 3, 12, 12);
                            Lines(graphics, pen, 14, 14, 21, 21);
                            Lines(graphics, pen, 6, 9, 12, 9); Lines(graphics, pen, 9, 6, 9, 12); break;
                        case MenuIcon.Text:
                            Lines(graphics, pen, 4, 6, 4, 3, 20, 3, 20, 6);
                            Lines(graphics, pen, 12, 3, 12, 21); Lines(graphics, pen, 8, 21, 16, 21); break;
                        case MenuIcon.Pen:
                            Lines(graphics, pen, 4, 20, 7, 8, 17, 3, 21, 7, 16, 17, 4, 20, 12, 12);
                            graphics.DrawEllipse(pen, 10.5f, 10.5f, 3, 3); break;
                        case MenuIcon.PlayPause:
                            Lines(graphics, pen, 3, 4, 3, 20, 13, 12, 3, 4);
                            Lines(graphics, pen, 17, 5, 17, 19); Lines(graphics, pen, 21, 5, 21, 19); break;
                        case MenuIcon.Stop:
                            graphics.DrawRectangle(pen, 5, 5, 14, 14); break;
                        case MenuIcon.StepBack:
                        case MenuIcon.StepForward:
                            if (icon == MenuIcon.StepBack) { graphics.TranslateTransform(24, 0); graphics.ScaleTransform(-1, 1); }
                            Lines(graphics, pen, 5, 4, 5, 20, 16, 12, 5, 4);
                            Lines(graphics, pen, 20, 4, 20, 20); break;
                        case MenuIcon.Marker:
                            Lines(graphics, pen, 6, 3, 18, 3, 18, 14, 12, 21, 6, 14, 6, 3);
                            graphics.DrawEllipse(pen, 10, 7, 4, 4); break;
                        case MenuIcon.Undo:
                        case MenuIcon.Redo:
                            if (icon == MenuIcon.Redo) { graphics.TranslateTransform(24, 0); graphics.ScaleTransform(-1, 1); }
                            graphics.DrawArc(pen, 5, 7, 15, 13, 205, 285);
                            Lines(graphics, pen, 3, 4, 3, 11, 10, 11); break;
                        case MenuIcon.Save:
                            Lines(graphics, pen, 4, 3, 17, 3, 21, 7, 21, 21, 4, 21, 4, 3);
                            Lines(graphics, pen, 8, 3, 8, 9, 16, 9, 16, 3);
                            graphics.DrawRectangle(pen, 8, 14, 9, 7); break;
                        case MenuIcon.RippleEdit:
                            Lines(graphics, pen, 12, 3, 12, 21);
                            Arrow(graphics, pen, 3, 8, 9, 8); Arrow(graphics, pen, 21, 16, 15, 16);
                            Lines(graphics, pen, 3, 19, 8, 19); Lines(graphics, pen, 16, 5, 21, 5); break;
                        case MenuIcon.RollingEdit:
                            Lines(graphics, pen, 10, 3, 10, 21); Lines(graphics, pen, 14, 3, 14, 21);
                            Arrow(graphics, pen, 8, 12, 2, 12); Arrow(graphics, pen, 16, 12, 22, 12); break;
                        case MenuIcon.Slip:
                            RoundedRectangle(graphics, pen, 3, 5, 18, 14, 2);
                            Lines(graphics, pen, 9, 9, 6, 12, 9, 15);
                            Lines(graphics, pen, 6, 12, 18, 12);
                            Lines(graphics, pen, 15, 9, 18, 12, 15, 15); break;
                        case MenuIcon.Slide:
                            RoundedRectangle(graphics, pen, 8, 5, 8, 14, 1);
                            Arrow(graphics, pen, 5, 12, 1, 12);
                            Arrow(graphics, pen, 19, 12, 23, 12); break;
                        case MenuIcon.MarkIn:
                        case MenuIcon.MarkOut:
                            if (icon == MenuIcon.MarkOut) { graphics.TranslateTransform(24, 0); graphics.ScaleTransform(-1, 1); }
                            Lines(graphics, pen, 15, 3, 7, 3, 7, 21, 15, 21);
                            Arrow(graphics, pen, 21, 12, 11, 12); break;
                        case MenuIcon.Insert:
                            Lines(graphics, pen, 3, 16, 3, 21, 9, 21, 9, 16);
                            Lines(graphics, pen, 15, 16, 15, 21, 21, 21, 21, 16);
                            Arrow(graphics, pen, 12, 2, 12, 14);
                            Arrow(graphics, pen, 8, 12, 3, 12); Arrow(graphics, pen, 16, 12, 21, 12); break;
                        case MenuIcon.Overwrite:
                            graphics.DrawRectangle(pen, 3, 16, 18, 5);
                            Arrow(graphics, pen, 12, 2, 12, 13); Lines(graphics, pen, 7, 19, 17, 19); break;
                        case MenuIcon.Link:
                            graphics.DrawArc(pen, 2, 7, 13, 10, 60, 240);
                            graphics.DrawArc(pen, 9, 7, 13, 10, 240, 240);
                            Lines(graphics, pen, 8, 12, 16, 12); break;
                        case MenuIcon.Magnet:
                            graphics.DrawArc(pen, 4, 9, 16, 12, 0, 180);
                            Lines(graphics, pen, 4, 15, 4, 3, 9, 3, 9, 14);
                            Lines(graphics, pen, 15, 14, 15, 3, 20, 3, 20, 15);
                            Lines(graphics, pen, 4, 7, 9, 7); Lines(graphics, pen, 15, 7, 20, 7); break;
                        case MenuIcon.Mute:
                            Lines(graphics, pen, 3, 9, 7, 9, 12, 4, 12, 20, 7, 15, 3, 15, 3, 9);
                            Lines(graphics, pen, 16, 9, 22, 15); Lines(graphics, pen, 16, 15, 22, 9); break;
                        default: throw new ArgumentOutOfRangeException("icon");
                    }
                }
            }
            finally { graphics.Restore(state); }
        }

        private static void RoundedRectangle(Graphics graphics, Pen pen, float x, float y, float width, float height, float radius)
        {
            float diameter = radius * 2;
            using (var path = new GraphicsPath())
            {
                path.AddArc(x, y, diameter, diameter, 180, 90);
                path.AddArc(x + width - diameter, y, diameter, diameter, 270, 90);
                path.AddArc(x + width - diameter, y + height - diameter, diameter, diameter, 0, 90);
                path.AddArc(x, y + height - diameter, diameter, diameter, 90, 90);
                path.CloseFigure();
                graphics.DrawPath(pen, path);
            }
        }

        private static void Lines(Graphics graphics, Pen pen, params float[] coordinates)
        {
            var points = new PointF[coordinates.Length / 2];
            for (int i = 0; i < points.Length; i++) points[i] = new PointF(coordinates[i * 2], coordinates[i * 2 + 1]);
            graphics.DrawLines(pen, points);
        }

        private static void Arrow(Graphics graphics, Pen pen, float x1, float y1, float x2, float y2)
        {
            graphics.DrawLine(pen, x1, y1, x2, y2);
            double angle = Math.Atan2(y2 - y1, x2 - x1);
            float dx = (float)Math.Cos(angle), dy = (float)Math.Sin(angle);
            Lines(graphics, pen, x2 - dx * 3 + dy * 2.5f, y2 - dy * 3 - dx * 2.5f,
                x2, y2, x2 - dx * 3 - dy * 2.5f, y2 - dy * 3 + dx * 2.5f);
        }
    }

    internal sealed class MenuIconPicker : ComboBox
    {
        internal MenuIconPicker()
        {
            DropDownStyle = ComboBoxStyle.DropDownList;
            DrawMode = DrawMode.OwnerDrawFixed;
            ItemHeight = 32;
            DropDownHeight = 340;
            IntegralHeight = false;
            Dock = DockStyle.Fill;
            foreach (MenuIcon icon in Enum.GetValues(typeof(MenuIcon))) Items.Add(icon);
        }

        protected override void OnDrawItem(DrawItemEventArgs args)
        {
            args.DrawBackground();
            if (args.Index < 0) return;
            DrawChoice(args.Graphics, (MenuIcon)Items[args.Index], args.Bounds, args.ForeColor);
            args.DrawFocusRectangle();
        }

        internal void DrawChoice(Graphics graphics, MenuIcon icon, Rectangle bounds, Color color)
        {
            float size = Math.Min(24, bounds.Height - 4);
            MenuIcons.Draw(graphics, icon,
                new RectangleF(bounds.X + 5, bounds.Y + (bounds.Height - size) / 2, size, size), color);
            TextRenderer.DrawText(graphics, MenuIcons.Label(icon), Font,
                new Rectangle(bounds.X + 36, bounds.Y, bounds.Width - 36, bounds.Height),
                color, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }
}
