using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using MehrBar.Spikes.Common;

namespace MehrBar.Spikes.WindowHost.Variants;

/// <summary>
/// Variant H1: the bar is a rectangle with an alpha-1 fill. The brush is deliberately not null
/// and not alpha 0: WPF hit-testing needs a non-null brush, and the layered window only catches
/// the mouse on pixels with alpha above 0 (RESEARCH Q1). Five opaque sentinel squares sit on the plate.
/// </summary>
[HostVariant("H1")]
internal sealed class PlateH1Variant : IHostVariant
{
    private IReadOnlyList<UIElement> _elements = Array.Empty<UIElement>();
    private HostContext? _ctx;

    public void Attach(HostContext ctx, string name)
    {
        _ctx = ctx;
        _elements = PlateDrawing.Add(ctx);
    }

    public void Detach()
    {
        if (_ctx == null)
        {
            return;
        }

        foreach (UIElement element in _elements)
        {
            _ctx.Root.Children.Remove(element);
        }

        _elements = Array.Empty<UIElement>();
    }
}

/// <summary>Shared drawing for every variant that uses the alpha-1 plate.</summary>
internal static class PlateDrawing
{
    /// <summary>The nearly invisible plate colour (alpha 1, RGB 0).</summary>
    public static readonly Color PlateColor = Color.FromArgb(1, 0, 0, 0);

    public static Color SentinelColor { get; } = FromRgb(SpikeGeometry.SentinelRgb);

    /// <summary>Adds the plate and, unless <c>--sentinel off</c>, the five sentinel squares.</summary>
    public static IReadOnlyList<UIElement> Add(HostContext ctx)
    {
        var added = new List<UIElement>();

        var plate = new Rectangle
        {
            Width = ctx.Plate.Width,
            Height = ctx.Plate.Height,
            Fill = Frozen(PlateColor),
        };

        // Hand part H1 (--plate-outline on): an opaque 2 px frame inside the plate shows the otherwise invisible hit area;
        // it lies within the plate, so the hit area stays the same.
        if (ctx.Options.TryGetValue("plate-outline", out string? outline) &&
            string.Equals(outline, "on", StringComparison.OrdinalIgnoreCase))
        {
            plate.Stroke = Frozen(SentinelColor);
            plate.StrokeThickness = 2;
        }
        Canvas.SetLeft(plate, ctx.Plate.X);
        Canvas.SetTop(plate, ctx.Plate.Y);
        ctx.Root.Children.Add(plate);
        added.Add(plate);

        bool sentinel = !ctx.Options.TryGetValue("sentinel", out string? value) ||
                        !string.Equals(value, "off", StringComparison.OrdinalIgnoreCase);
        if (sentinel)
        {
            Brush brush = Frozen(SentinelColor);
            int half = SpikeGeometry.SentinelSize / 2;
            foreach ((int x, int y) in SpikeGeometry.ProbeOffsets)
            {
                var square = new Rectangle
                {
                    Width = SpikeGeometry.SentinelSize,
                    Height = SpikeGeometry.SentinelSize,
                    Fill = brush,
                };
                Canvas.SetLeft(square, x - half);
                Canvas.SetTop(square, y - half);
                ctx.Root.Children.Add(square);
                added.Add(square);
            }
        }

        return added;
    }

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static Color FromRgb(int rgb) =>
        Color.FromRgb((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF));
}
