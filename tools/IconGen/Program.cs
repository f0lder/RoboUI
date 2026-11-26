using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace IconGen;

/// <summary>
/// Draws RoboUI's Fluent-style icon (a blue folder with a green "go" badge) on a 256×256 grid
/// and writes RoboUI.svg, RoboUI.ico (16–256 px) and RoboUI.png (256 px).
/// Usage: IconGen &lt;output folder&gt; [preview.png]
/// </summary>
internal static class Program
{
    // ---- Shapes (SVG/WPF path syntax, 256×256 grid) -----------------------------------------

    private const string FolderBack =
        "M36,40 H96 C102,40 107,42.5 111,47 L126,64 H212 C225.3,64 236,74.7 236,88 V176 " +
        "C236,189.3 225.3,200 212,200 H44 C30.7,200 20,189.3 20,176 V56 C20,47.2 27.2,40 36,40 Z";

    private const string FolderFront =
        "M20,104 C20,90.7 30.7,80 44,80 H212 C225.3,80 236,90.7 236,104 V180 " +
        "C236,193.3 225.3,204 212,204 H44 C30.7,204 20,193.3 20,180 Z";

    // Thin lighter band along the top edge of the front panel, for depth.
    private const string FrontHighlight =
        "M20,104 C20,90.7 30.7,80 44,80 H212 C225.3,80 236,90.7 236,104 V108 " +
        "C236,94.7 225.3,84 212,84 H44 C30.7,84 20,94.7 20,108 Z";

    private const string Arrow = "M160,188 H212 M193,168 L214,188 L193,208";

    private static readonly Point BadgeCenter = new(188, 188);
    private const double BadgeRadius = 52;
    private const double BadgeGap = 62; // the folder is cut away around the badge, Fluent-style

    // ---- Colors -----------------------------------------------------------------------------

    private static readonly (string Top, string Bottom) BackColors = ("#3A8EE6", "#1D5FC2");
    private static readonly (string Top, string Bottom) FrontColors = ("#7CC3FF", "#3A8BF0");
    private static readonly (string Top, string Bottom) BadgeColors = ("#3DDC9B", "#0E9466");
    private const string HighlightColor = "#B5DDFF";

    private static readonly int[] IcoSizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256];

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.Error.WriteLine("Usage: IconGen <output folder> [preview.png]");
            return 1;
        }
        var outDir = Directory.CreateDirectory(args[0]).FullName;

        File.WriteAllText(Path.Combine(outDir, "RoboUI.svg"), Svg(), new UTF8Encoding(false));
        File.WriteAllBytes(Path.Combine(outDir, "RoboUI.png"), Png(256));
        File.WriteAllBytes(Path.Combine(outDir, "RoboUI.ico"), Ico());
        if (args.Length > 1) File.WriteAllBytes(args[1], Preview());

        Console.WriteLine($"Wrote RoboUI.svg, RoboUI.png, RoboUI.ico ({string.Join(", ", IcoSizes)} px) to {outDir}");
        return 0;
    }

    // ---- WPF drawing ------------------------------------------------------------------------

    private static Drawing BuildDrawing()
    {
        var cutout = new EllipseGeometry(BadgeCenter, BadgeGap, BadgeGap);
        Geometry Cut(string path) =>
            new CombinedGeometry(GeometryCombineMode.Exclude, Geometry.Parse(path), cutout);

        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(Vertical(BackColors), null, Cut(FolderBack)));
        group.Children.Add(new GeometryDrawing(Vertical(FrontColors), null, Cut(FolderFront)));
        group.Children.Add(new GeometryDrawing(Brush(HighlightColor, 0.55), null, Cut(FrontHighlight)));
        group.Children.Add(new GeometryDrawing(Diagonal(BadgeColors), null,
            new EllipseGeometry(BadgeCenter, BadgeRadius, BadgeRadius)));
        var pen = new Pen(Brushes.White, 13) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        group.Children.Add(new GeometryDrawing(null, pen, Geometry.Parse(Arrow)));
        return group;
    }

    private static BitmapSource Render(int size, Color? background = null)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            if (background is { } bg) dc.DrawRectangle(new SolidColorBrush(bg), null, new Rect(0, 0, size, size));
            dc.PushTransform(new ScaleTransform(size / 256.0, size / 256.0));
            dc.DrawDrawing(BuildDrawing());
        }
        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        return bitmap;
    }

    private static byte[] Png(int size) => Encode(Render(size));

    private static byte[] Encode(BitmapSource source)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }

    /// <summary>A multi-size .ico whose images are PNG-compressed (supported since Windows Vista).</summary>
    private static byte[] Ico()
    {
        var images = IcoSizes.Select(s => (Size: s, Data: Png(s))).ToList();
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write((ushort)0);            // reserved
        w.Write((ushort)1);            // type: icon
        w.Write((ushort)images.Count);
        var offset = 6 + 16 * images.Count;
        foreach (var (size, data) in images)
        {
            w.Write((byte)(size >= 256 ? 0 : size)); // 0 means 256
            w.Write((byte)(size >= 256 ? 0 : size));
            w.Write((byte)0);           // palette colors
            w.Write((byte)0);           // reserved
            w.Write((ushort)1);         // color planes
            w.Write((ushort)32);        // bits per pixel
            w.Write(data.Length);
            w.Write(offset);
            offset += data.Length;
        }
        foreach (var (_, data) in images) w.Write(data);
        return ms.ToArray();
    }

    /// <summary>All sizes on a dark and a light strip, small ones also enlarged, for checking by eye.</summary>
    private static byte[] Preview()
    {
        int[] sizes = [16, 24, 32, 48, 64, 128, 256];
        var width = sizes.Sum() + 20 * (sizes.Length + 1) + 3 * (64 + 20);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var rows = new[] { Color.FromRgb(0x20, 0x20, 0x20), Color.FromRgb(0xF3, 0xF3, 0xF3) };
            for (var r = 0; r < rows.Length; r++)
            {
                double y = r * 296;
                dc.DrawRectangle(new SolidColorBrush(rows[r]), null, new Rect(0, y, width, 296));
                double x = 20;
                foreach (var s in sizes)
                {
                    dc.DrawImage(Render(s), new Rect(x, y + 20 + (256 - s), s, s));
                    x += s + 20;
                }
                foreach (var s in new[] { 16, 24, 32 }) // pixel-doubled to inspect small sizes
                {
                    var img = new FormatConvertedBitmap(Render(s), PixelFormats.Pbgra32, null, 0);
                    dc.PushGuidelineSet(new GuidelineSet());
                    RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.NearestNeighbor);
                    var group = new DrawingGroup();
                    RenderOptions.SetBitmapScalingMode(group, BitmapScalingMode.NearestNeighbor);
                    group.Children.Add(new ImageDrawing(img, new Rect(x, y + 20 + 192, 64, 64)));
                    dc.DrawDrawing(group);
                    dc.Pop();
                    x += 64 + 20;
                }
            }
        }
        var bitmap = new RenderTargetBitmap(width, 592, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        return Encode(bitmap);
    }

    private static Color C(string hex) => (Color)ColorConverter.ConvertFromString(hex);
    private static SolidColorBrush Brush(string hex, double opacity = 1) => new(C(hex)) { Opacity = opacity };
    private static LinearGradientBrush Vertical((string Top, string Bottom) c) => new(C(c.Top), C(c.Bottom), 90);
    private static LinearGradientBrush Diagonal((string Top, string Bottom) c) => new(C(c.Top), C(c.Bottom), 45);

    // ---- SVG (same shapes, for editing and the README) ---------------------------------------

    private static string Svg()
    {
        string F(double v) => v.ToString(CultureInfo.InvariantCulture);
        return $"""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 256 256" width="256" height="256">
              <defs>
                <linearGradient id="back" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0" stop-color="{BackColors.Top}"/><stop offset="1" stop-color="{BackColors.Bottom}"/>
                </linearGradient>
                <linearGradient id="front" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0" stop-color="{FrontColors.Top}"/><stop offset="1" stop-color="{FrontColors.Bottom}"/>
                </linearGradient>
                <linearGradient id="badge" x1="0" y1="0" x2="1" y2="1">
                  <stop offset="0" stop-color="{BadgeColors.Top}"/><stop offset="1" stop-color="{BadgeColors.Bottom}"/>
                </linearGradient>
                <mask id="gap">
                  <rect width="256" height="256" fill="#fff"/>
                  <circle cx="{F(BadgeCenter.X)}" cy="{F(BadgeCenter.Y)}" r="{F(BadgeGap)}" fill="#000"/>
                </mask>
              </defs>
              <g mask="url(#gap)">
                <path d="{FolderBack}" fill="url(#back)"/>
                <path d="{FolderFront}" fill="url(#front)"/>
                <path d="{FrontHighlight}" fill="{HighlightColor}" fill-opacity="0.55"/>
              </g>
              <circle cx="{F(BadgeCenter.X)}" cy="{F(BadgeCenter.Y)}" r="{F(BadgeRadius)}" fill="url(#badge)"/>
              <path d="{Arrow}" fill="none" stroke="#fff" stroke-width="13" stroke-linecap="round" stroke-linejoin="round"/>
            </svg>

            """;
    }
}
