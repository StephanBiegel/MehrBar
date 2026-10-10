using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MehrBar.Spikes.Icons;

/// <summary>
/// Alpha oracle for one extracted icon (all values computed on the straight-alpha BGRA the shell returned,
/// except the fringe scores, see below).
/// </summary>
/// <param name="AllOpaque">Every alpha is 255: the bitmap has no transparency at all.</param>
/// <param name="CornerAlphaMax">Highest alpha in the four 4x4 corners; 0 means a clean transparent border.</param>
/// <param name="StraightAlphaRatio">Share of pixels with alpha below 255 where a colour channel exceeds alpha
/// (impossible in premultiplied data, so above 0 proves straight alpha).</param>
/// <param name="FringeScore">Mean compositing error over the edge pixels (alpha 1..254) after the single
/// conversion to Pbgra32: composite over white minus composite over black must equal 255 - alpha. Any clipping
/// (colour above alpha) breaks that. Near 0 means the converted data is a valid premultiplied image.</param>
/// <param name="RawFringeScore">Same measure on the raw shell data wrongly treated as premultiplied; large for
/// straight alpha. The gap to <paramref name="FringeScore"/> is what the conversion fixes.</param>
/// <param name="EdgeShare">Pixels with alpha 1..254 divided by pixels with alpha above 0. An upscaled source has
/// wider soft edges, so a high share indicates a blurry icon.</param>
/// <param name="AllZeroAlpha">Every alpha is 0: the shell returned a bitmap without an alpha channel.</param>
internal sealed record AlphaReport(
    bool AllOpaque,
    int CornerAlphaMax,
    double StraightAlphaRatio,
    double FringeScore,
    double RawFringeScore,
    double EdgeShare,
    bool AllZeroAlpha);

internal static class IconMetrics
{
    /// <summary>
    /// Converts the straight-alpha BGRA pixels once to Pbgra32 and freezes the result (STACK section 1:
    /// drawing straight alpha while scaling produces dark fringes).
    /// </summary>
    public static BitmapSource ToPbgra32(byte[] bgra, int w, int h)
    {
        BitmapSource straight = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, bgra, w * 4);
        straight.Freeze();
        var converted = new FormatConvertedBitmap(straight, PixelFormats.Pbgra32, null, 0);
        converted.Freeze();
        return converted;
    }

    public static AlphaReport Measure(byte[] bgra, int w, int h)
    {
        int pixels = w * h;
        bool allOpaque = true;
        bool allZero = true;
        int translucent = 0; // alpha < 255
        int straight = 0;    // of those, colour channel > alpha
        int edge = 0;        // alpha 1..254
        int visible = 0;     // alpha > 0

        for (int i = 0; i < pixels; i++)
        {
            int o = i * 4;
            byte b = bgra[o], g = bgra[o + 1], r = bgra[o + 2], a = bgra[o + 3];
            if (a != 255)
            {
                allOpaque = false;
                translucent++;
                if (b > a || g > a || r > a)
                {
                    straight++;
                }
            }

            if (a != 0)
            {
                allZero = false;
                visible++;
                if (a != 255)
                {
                    edge++;
                }
            }
        }

        int cw = Math.Min(4, w), ch = Math.Min(4, h);
        int cornerMax = 0;
        for (int y = 0; y < ch; y++)
        {
            for (int x = 0; x < cw; x++)
            {
                cornerMax = Math.Max(cornerMax, bgra[(y * w + x) * 4 + 3]);
                cornerMax = Math.Max(cornerMax, bgra[(y * w + (w - 1 - x)) * 4 + 3]);
                cornerMax = Math.Max(cornerMax, bgra[((h - 1 - y) * w + x) * 4 + 3]);
                cornerMax = Math.Max(cornerMax, bgra[((h - 1 - y) * w + (w - 1 - x)) * 4 + 3]);
            }
        }

        BitmapSource pbgra = ToPbgra32(bgra, w, h);
        byte[] converted = new byte[w * h * 4];
        pbgra.CopyPixels(converted, w * 4, 0);

        return new AlphaReport(
            allOpaque,
            cornerMax,
            translucent == 0 ? 0 : (double)straight / translucent,
            Fringe(converted, pixels),
            Fringe(bgra, pixels),
            visible == 0 ? 0 : (double)edge / visible,
            allZero);
    }

    /// <summary>
    /// Treats <paramref name="data"/> as premultiplied BGRA, composites every edge pixel over white and over
    /// black with per-channel clamping, and returns the mean |(Lwhite - Lblack) - (255 - alpha)|. For valid
    /// premultiplied data the two composites differ by exactly 255 - alpha, so the score is 0.
    /// </summary>
    private static double Fringe(byte[] data, int pixels)
    {
        double sum = 0;
        int count = 0;
        for (int i = 0; i < pixels; i++)
        {
            int o = i * 4;
            int a = data[o + 3];
            if (a == 0 || a == 255)
            {
                continue;
            }

            int add = 255 - a;
            double lw = Luminance(
                Math.Min(255, data[o + 2] + add), Math.Min(255, data[o + 1] + add), Math.Min(255, data[o] + add));
            double lb = Luminance(data[o + 2], data[o + 1], data[o]);
            sum += Math.Abs(lw - lb - add);
            count++;
        }

        return count == 0 ? 0 : sum / count;
    }

    private static double Luminance(int r, int g, int b) => 0.2126 * r + 0.7152 * g + 0.0722 * b;
}
