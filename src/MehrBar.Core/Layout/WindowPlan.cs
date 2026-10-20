namespace MehrBar.Core.Layout;

/// <summary>
/// Window of a bar: the bar at rest plus the largest zoom growth on each side along the edge and the zoom growth away
/// from the edge (LEIS-06). The window keeps this size from phase 2 on, so phase 3 is additive and clicks in the future
/// zoom area pass through already now (only the plate is a hit target).
/// </summary>
/// <param name="WindowRect">Window rectangle on the screen.</param>
/// <param name="BarRect">Bar rectangle at rest on the screen.</param>
/// <param name="BarStartInWindowU">Distance from the window start to the bar start along u.</param>
/// <param name="MaxGrowthBefore">Largest growth towards smaller u over all pointer positions.</param>
/// <param name="MaxGrowthAfter">Largest growth towards larger u over all pointer positions.</param>
/// <param name="DepthGrowth">Extra depth away from the edge for fully zoomed icons.</param>
public sealed record WindowPlan(
    RectI WindowRect,
    RectI BarRect,
    double BarStartInWindowU,
    double MaxGrowthBefore,
    double MaxGrowthAfter,
    double DepthGrowth)
{
    public static WindowPlan Compute(RectI monitor, PlacementSpec spec, RestLayout rest)
    {
        RectI bar = BarPlacement.Compute(monitor, spec, rest.Length, rest.Depth);
        (double before, double after) = MaxGrowth(rest);
        double depthGrowth = rest.Metrics.DepthGrowth;

        bool horizontal = spec.Edge is BarEdge.Top or BarEdge.Bottom;
        int barU = horizontal ? bar.Left : bar.Top;
        int barLength = horizontal ? bar.Width : bar.Height;
        int monitorU = horizontal ? monitor.Left : monitor.Top;
        int monitorEnd = horizontal ? monitor.Right : monitor.Bottom;

        // Clamped to the monitor along the edge without moving the bar (Q18).
        int windowU = Math.Max(monitorU, (int)Math.Floor(barU - before));
        int windowEnd = Math.Min(monitorEnd, (int)Math.Ceiling(barU + barLength + after));
        int depth = (int)Math.Ceiling(rest.Depth + depthGrowth);

        RectI window = spec.Edge switch
        {
            BarEdge.Top => new RectI(windowU, bar.Top, windowEnd - windowU, depth),
            BarEdge.Bottom => new RectI(windowU, bar.Bottom - depth, windowEnd - windowU, depth),
            BarEdge.Left => new RectI(bar.Left, windowU, depth, windowEnd - windowU),
            _ => new RectI(bar.Right - depth, windowU, depth, windowEnd - windowU),
        };

        return new WindowPlan(window, bar, barU - windowU, before, after, depthGrowth);
    }

    /// <summary>Samples the pointer along the whole bar (step at most a quarter pitch, both ends included).</summary>
    private static (double Before, double After) MaxGrowth(RestLayout rest)
    {
        if (rest.Slots.Count == 0)
        {
            return (0, 0);
        }

        double step = rest.Metrics.Pitch / 4;
        int samples = (int)Math.Ceiling(rest.Length / step);
        double before = 0;
        double after = 0;
        for (int i = 0; i <= samples; i++)
        {
            double u = Math.Min(rest.Length, i * step);
            FrameLayout frame = FrameLayout.Compute(rest, u, 1);
            before = Math.Max(before, frame.ExtraBefore);
            after = Math.Max(after, frame.ExtraAfter);
        }

        return (before, after);
    }
}
