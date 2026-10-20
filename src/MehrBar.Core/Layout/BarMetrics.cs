using MehrBar.Core.Model;

namespace MehrBar.Core.Layout;

/// <summary>Sizes of one bar in pixels, derived from its configuration.</summary>
/// <param name="IconSize">Icon size at rest (D-09).</param>
/// <param name="ZoomSize">Size of the icon directly under the pointer at full zoom.</param>
/// <param name="Radius">Zoom radius in slot pitches.</param>
/// <param name="Spacing">Gap added to every slot (D-14: 0 like the RocketDock calibration).</param>
/// <param name="Padding">Inner margin of the default background; phase 4 replaces it with the skin margins.</param>
/// <param name="MinSlots">Slots an empty or short bar still shows (D-08).</param>
public sealed record BarMetrics(double IconSize, double ZoomSize, double Radius, double Spacing, double Padding, int MinSlots)
{
    public const double DefaultPadding = 6;
    public const int DefaultMinSlots = 3;

    /// <summary>Slot length along the bar axis.</summary>
    public double Pitch => IconSize + Spacing;

    /// <summary>Depth of the background away from the edge.</summary>
    public double BackgroundDepth => IconSize + (2 * Padding);

    /// <summary>How far a fully zoomed icon reaches beyond the background, away from the edge.</summary>
    public double DepthGrowth => ZoomSize - IconSize;

    public static BarMetrics From(BarConfig bar) => new(
        bar.IconSize,
        bar.ZoomSize,
        RadiusFromZoomWidth(bar.ZoomWidth),
        bar.Spacing,
        DefaultPadding,
        DefaultMinSlots);

    /// <summary>
    /// Zoom radius in pitches from RocketDock's zoom width: 0.645 × zoomWidth, so 4 gives the measured 2.58
    /// (01-SPIKE-zoom-anchor.md). Provisional until the calibration in phase 3.
    /// </summary>
    public static double RadiusFromZoomWidth(double zoomWidth) => 0.645 * zoomWidth;
}
