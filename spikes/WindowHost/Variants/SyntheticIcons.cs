using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MehrBar.Spikes.WindowHost.Variants;

/// <summary>
/// Generated icon bitmaps for the render-cost workload (Q4). No third-party images: each icon is a soft-edged
/// disc with a radial gradient in its own hue. 256 px, Pbgra32 and frozen, like the shell icons the product
/// will load (256 px source, converted once to premultiplied alpha).
/// </summary>
internal static class SyntheticIcons
{
    public const int PixelSize = 256;

    private const double Radius = 118.0;
    private const double EdgeSoftness = 6.0;

    public static IReadOnlyList<BitmapSource> Create(int count)
    {
        var icons = new List<BitmapSource>(count);
        for (int i = 0; i < count; i++)
        {
            icons.Add(CreateOne(i, count));
        }

        return icons;
    }

    private static BitmapSource CreateOne(int index, int count)
    {
        double hue = 360.0 * index / Math.Max(1, count);
        (double lr, double lg, double lb) = Hsv(hue, 0.30, 1.00);
        (double dr, double dg, double db) = Hsv(hue, 0.95, 0.50);

        const double centre = (PixelSize - 1) / 2.0;
        const double highlightX = 96.0;
        const double highlightY = 84.0;
        const double gradientReach = 170.0;

        var pixels = new byte[PixelSize * PixelSize * 4];
        for (int y = 0; y < PixelSize; y++)
        {
            for (int x = 0; x < PixelSize; x++)
            {
                double distance = Math.Sqrt(((x - centre) * (x - centre)) + ((y - centre) * (y - centre)));
                double alpha = Math.Clamp(((Radius - distance) / EdgeSoftness) + 0.5, 0.0, 1.0);
                int offset = ((y * PixelSize) + x) * 4;
                if (alpha <= 0.0)
                {
                    continue; // fully transparent: all four bytes stay 0
                }

                double hx = x - highlightX;
                double hy = y - highlightY;
                double t = Math.Clamp(Math.Sqrt((hx * hx) + (hy * hy)) / gradientReach, 0.0, 1.0);
                double r = lr + ((dr - lr) * t);
                double g = lg + ((dg - lg) * t);
                double b = lb + ((db - lb) * t);

                // Premultiplied alpha, BGRA byte order.
                pixels[offset] = (byte)Math.Round(b * alpha * 255.0);
                pixels[offset + 1] = (byte)Math.Round(g * alpha * 255.0);
                pixels[offset + 2] = (byte)Math.Round(r * alpha * 255.0);
                pixels[offset + 3] = (byte)Math.Round(alpha * 255.0);
            }
        }

        var bitmap = new WriteableBitmap(PixelSize, PixelSize, 96, 96, PixelFormats.Pbgra32, null);
        bitmap.WritePixels(new Int32Rect(0, 0, PixelSize, PixelSize), pixels, PixelSize * 4, 0);
        bitmap.Freeze();
        return bitmap;
    }

    private static (double R, double G, double B) Hsv(double hueDegrees, double saturation, double value)
    {
        double h = (hueDegrees % 360.0) / 60.0;
        int sector = (int)Math.Floor(h);
        double f = h - sector;
        double p = value * (1.0 - saturation);
        double q = value * (1.0 - (saturation * f));
        double t = value * (1.0 - (saturation * (1.0 - f)));
        return sector switch
        {
            0 => (value, t, p),
            1 => (q, value, p),
            2 => (p, value, t),
            3 => (p, q, value),
            4 => (t, p, value),
            _ => (value, p, q),
        };
    }
}
