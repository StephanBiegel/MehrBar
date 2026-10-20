namespace MehrBar.Core.Layout;

/// <summary>Screen edge a bar sits on.</summary>
public enum BarEdge
{
    Top,
    Bottom,
    Left,
    Right,
}

/// <summary>Where the bar sits along its edge.</summary>
public enum BarAlign
{
    /// <summary>At the start of the edge (left for top/bottom, top for left/right).</summary>
    Start,

    /// <summary>Centred on the edge.</summary>
    Center,

    /// <summary>At the end of the edge (right for top/bottom, bottom for left/right).</summary>
    End,
}

/// <summary>An integer rectangle in screen pixels.</summary>
public readonly record struct RectI(int Left, int Top, int Width, int Height)
{
    public int Right => Left + Width;

    public int Bottom => Top + Height;
}

/// <summary>How a bar is placed on its monitor.</summary>
/// <param name="Edge">Screen edge the bar sits on.</param>
/// <param name="Align">Position along the edge.</param>
/// <param name="Offset">Signed shift along the edge axis (towards larger x or y), added after the alignment.</param>
/// <param name="EdgeOffset">Distance from the edge into the screen; negative values push the bar past the edge.</param>
public readonly record struct PlacementSpec(BarEdge Edge, BarAlign Align, double Offset, double EdgeOffset);

/// <summary>
/// Places a bar on its monitor along an abstract bar axis u (x for top/bottom, y for left/right), so the right-hand bar
/// needs no separate code (LEIS-01, LEIS-02).
/// </summary>
public static class BarPlacement
{
    /// <summary>Screen rectangle of a bar with the given length along the edge and depth away from it.</summary>
    /// <param name="monitor">Full monitor rectangle (<c>rcMonitor</c>), not the work area.</param>
    /// <param name="spec">Edge, alignment and offsets.</param>
    /// <param name="barLength">Length along the edge in pixels; clamped to the monitor length.</param>
    /// <param name="barDepth">Depth away from the edge in pixels.</param>
    public static RectI Compute(RectI monitor, PlacementSpec spec, double barLength, double barDepth)
    {
        if (barLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(barLength), "The bar length must be positive.");
        }

        if (barDepth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(barDepth), "The bar depth must be positive.");
        }

        bool horizontal = spec.Edge is BarEdge.Top or BarEdge.Bottom;
        double axisStart = horizontal ? monitor.Left : monitor.Top;
        double axisLength = horizontal ? monitor.Width : monitor.Height;
        double length = Math.Min(barLength, axisLength);

        double u = spec.Align switch
        {
            BarAlign.Start => axisStart,
            BarAlign.Center => axisStart + ((axisLength - length) / 2),
            _ => axisStart + axisLength - length,
        };
        u += spec.Offset;

        double depthStart = spec.Edge switch
        {
            BarEdge.Top => monitor.Top + spec.EdgeOffset,
            BarEdge.Bottom => monitor.Bottom - spec.EdgeOffset - barDepth,
            BarEdge.Left => monitor.Left + spec.EdgeOffset,
            _ => monitor.Right - spec.EdgeOffset - barDepth,
        };

        int along = Round(u);
        int across = Round(depthStart);
        int lengthPx = Round(length);
        int depthPx = Round(barDepth);
        return horizontal
            ? new RectI(along, across, lengthPx, depthPx)
            : new RectI(across, along, depthPx, lengthPx);
    }

    private static int Round(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);
}
