using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ClaudeStatusBar;

/// <summary>
/// Builds the 18x18 status icons. Spark frames and the resting logo are alpha masks tinted with
/// a solid color; crab frames are full-color sprites used as-is. Decoded sources are frozen and
/// cached at load.
/// </summary>
internal static class IconRenderer
{
    private static readonly BitmapSource[] SparkMasks = Decode(Frames.Spark);
    private static readonly BitmapSource[] CrabSprites = Decode(Frames.Crab);
    private static readonly BitmapSource LogoMask = Decode(new[] { Frames.Logo })[0];

    public static int SparkFrameCount => Math.Max(1, SparkMasks.Length);
    public static int CrabFrameCount => Math.Max(1, CrabSprites.Length);

    private static BitmapSource[] Decode(string[] b64)
    {
        var list = new List<BitmapSource>();
        foreach (var s in b64)
        {
            try
            {
                var bytes = Convert.FromBase64String(s);
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = new MemoryStream(bytes);
                bmp.EndInit();
                bmp.Freeze();
                list.Add(bmp);
            }
            catch { }
        }
        return list.ToArray();
    }

    /// <summary>Spark frame at the given index, tinted with the color (null leaves the mask as-is).</summary>
    public static ImageSource Spark(int frame, Color? color)
        => Tint(SparkMasks.Length > 0 ? SparkMasks[frame % SparkMasks.Length] : LogoMask, color);

    /// <summary>Resting logo mark, tinted with the color.</summary>
    public static ImageSource Logo(Color? color) => Tint(LogoMask, color);

    /// <summary>Full-color crab sprite frame.</summary>
    public static ImageSource Crab(int frame)
        => CrabSprites.Length > 0 ? CrabSprites[frame % CrabSprites.Length] : LogoMask;

    /// <summary>Filled dot used for the awaiting-permission state.</summary>
    public static ImageSource Dot(Color color)
    {
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
            dc.DrawEllipse(new SolidColorBrush(color), null, new Point(9, 9), 4.5, 4.5);
        var rtb = new RenderTargetBitmap(18, 18, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);
        rtb.Freeze();
        return rtb;
    }

    // Fill a solid color through the mask's alpha. A null color returns the mask unchanged.
    private static ImageSource Tint(BitmapSource mask, Color? color)
    {
        if (color is null) return mask;

        const int size = 18;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.PushOpacityMask(new ImageBrush(mask) { Stretch = Stretch.Uniform });
            dc.DrawRectangle(new SolidColorBrush(color.Value), null, new Rect(0, 0, size, size));
            dc.Pop();
        }
        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }
}
