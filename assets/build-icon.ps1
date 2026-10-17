$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

public static class OrbitCapAssets
{
    private static void Stroke(Graphics g, Color color, float width, Action<GraphicsPath> shape)
    {
        using (var path = new GraphicsPath())
        using (var pen = new Pen(color, width) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            shape(path);
            g.DrawPath(pen, path);
        }
    }

    private static Bitmap Render(int size)
    {
        // Small frames omit the highlight and use heavier outlines, not a downscaled large icon.
        int scale = 4;
        var bitmap = new Bitmap(size * scale, size * scale, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.ScaleTransform(size * scale / 256f, size * scale / 256f);
            bool small = size <= 32;
            float orbitWidth = small ? 15 : 11;
            Stroke(g, Color.FromArgb(41, 122, 160), small ? 14 : 10, p => {
                p.AddBezier(35, 142, 20, 118, 63, 73, 124, 52);
                p.AddBezier(124, 52, 185, 31, 235, 44, 225, 73);
            });
            using (var fill = new SolidBrush(Color.FromArgb(25, 31, 39)))
            using (var edge = new Pen(Color.FromArgb(101, 115, 130), small ? 6 : 3))
            {
                g.FillEllipse(fill, 76, 152, 104, 36);
                g.DrawEllipse(edge, 76, 152, 104, 36);
            }
            using (var body = new GraphicsPath())
            using (var fill = new LinearGradientBrush(new PointF(66, 62), new PointF(190, 176),
                Color.FromArgb(85, 94, 104), Color.FromArgb(36, 42, 50)))
            using (var edge = new Pen(Color.FromArgb(32, 39, 48), small ? 5 : 3))
            {
                body.AddBezier(66, 94, 66, 76, 94, 62, 128, 62);
                body.AddBezier(128, 62, 162, 62, 190, 76, 190, 94);
                body.AddLine(190, 94, 190, 139);
                body.AddBezier(190, 139, 190, 160, 162, 176, 128, 176);
                body.AddBezier(128, 176, 94, 176, 66, 160, 66, 139);
                body.CloseFigure();
                g.FillPath(fill, body);
                g.DrawPath(edge, body);
            }
            using (var fill = new LinearGradientBrush(new PointF(128, 62), new PointF(128, 126),
                Color.FromArgb(135, 149, 164), Color.FromArgb(65, 76, 89)))
            using (var edge = new Pen(Color.FromArgb(163, 179, 195), small ? 6 : 3))
            {
                g.FillEllipse(fill, 66, 62, 124, 64);
                g.DrawEllipse(edge, 66, 62, 124, 64);
            }
            if (!small)
                Stroke(g, Color.FromArgb(166, 182, 199, 215), 4,
                    p => p.AddBezier(86, 89, 99, 76, 130, 72, 151, 79));
            using (var orbit = new GraphicsPath())
            using (var fill = new LinearGradientBrush(new PointF(31, 150), new PointF(218, 150),
                Color.FromArgb(41, 122, 160), Color.FromArgb(112, 212, 244)))
            using (var pen = new Pen(fill, orbitWidth) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                orbit.AddBezier(218, 109, 195, 150, 141, 184, 88, 192);
                orbit.AddBezier(88, 192, 44, 198, 20, 183, 31, 158);
                g.DrawPath(pen, orbit);
            }
            using (var fill = new SolidBrush(Color.FromArgb(112, 212, 244)))
                g.FillEllipse(fill, 210, 101, 16, 16);
        }
        var result = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(result))
        {
            g.CompositingMode = CompositingMode.SourceCopy;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(bitmap, new Rectangle(0, 0, size, size), 0, 0, bitmap.Width, bitmap.Height, GraphicsUnit.Pixel);
        }
        bitmap.Dispose();
        return result;
    }

    public static void Write(string folder)
    {
        int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
        var frames = new byte[sizes.Length][];
        for (int i = 0; i < sizes.Length; i++)
            using (var bitmap = Render(sizes[i]))
            using (var stream = new MemoryStream())
            {
                bitmap.Save(stream, ImageFormat.Png);
                frames[i] = stream.ToArray();
            }
        using (var writer = new BinaryWriter(File.Create(Path.Combine(folder, "orbit-cap.ico"))))
        {
            writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)sizes.Length);
            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
                writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
                writer.Write((byte)0); writer.Write((byte)0);
                writer.Write((ushort)1); writer.Write((ushort)32);
                writer.Write(frames[i].Length); writer.Write(offset);
                offset += frames[i].Length;
            }
            foreach (byte[] frame in frames) writer.Write(frame);
        }
        using (var preview = Render(512))
            preview.Save(Path.Combine(folder, "orbit-cap.png"), ImageFormat.Png);
    }
}
'@
[OrbitCapAssets]::Write($PSScriptRoot)
Write-Host 'Orbit Cap: SVG companion PNG and nine-size Windows ICO generated.'
