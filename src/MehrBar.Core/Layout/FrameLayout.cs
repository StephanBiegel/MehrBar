namespace MehrBar.Core.Layout;

/// <summary>A slot as drawn in one frame, relative to the bar start at rest.</summary>
/// <param name="ItemIndex">Index of the entry in the whole item list.</param>
/// <param name="Start">Displayed near edge along u.</param>
/// <param name="Length">Displayed length along u.</param>
/// <param name="IconSize">Displayed icon size.</param>
public sealed record FrameSlot(int ItemIndex, double Start, double Length, double IconSize);

/// <summary>
/// The layout contract for every frame (D-04): the rest layout plus the zoom at a given progress. With progress 0
/// (phase 2) it is exactly the rest layout; phase 3 only adds the animator and the pointer.
/// </summary>
/// <param name="Slots">Drawn slots in order.</param>
/// <param name="ExtraBefore">How far the near end moved towards smaller u.</param>
/// <param name="ExtraAfter">How far the far end moved towards larger u.</param>
public sealed record FrameLayout(IReadOnlyList<FrameSlot> Slots, double ExtraBefore, double ExtraAfter)
{
    /// <summary>Layout of one frame; wraps <see cref="ZoomEngine.ComputeSmooth"/> without own zoom maths.</summary>
    /// <param name="rest">The bar at rest.</param>
    /// <param name="pointerU">Pointer along u relative to the bar start, or null when the pointer is away.</param>
    /// <param name="progress">Zoom progress, clamped to [0, 1].</param>
    public static FrameLayout Compute(RestLayout rest, double? pointerU, double progress)
    {
        BarMetrics m = rest.Metrics;
        double p = Math.Clamp(double.IsNaN(progress) ? 0 : progress, 0, 1);
        if (pointerU is not double pointer || p == 0 || rest.Slots.Count == 0)
        {
            return AtRest(rest);
        }

        double effectiveZoom = m.IconSize + ((m.ZoomSize - m.IconSize) * p);
        ZoomResult zoom = ZoomEngine.ComputeSmooth(rest.ToRestItems(), m.Pitch, new ZoomParams(m.IconSize, effectiveZoom, m.Radius), pointer);

        var slots = new FrameSlot[rest.Slots.Count];
        for (int k = 0; k < slots.Length; k++)
        {
            RestSlot slot = rest.Slots[k];
            double length = zoom.DisplayedLengths[k];
            slots[k] = new FrameSlot(slot.ItemIndex, zoom.DisplayedStarts[k], length, m.IconSize * length / slot.Length);
        }

        return new FrameLayout(slots, zoom.ExtraBefore, zoom.ExtraAfter);
    }

    private static FrameLayout AtRest(RestLayout rest) => new(
        rest.Slots.Select(s => new FrameSlot(s.ItemIndex, s.Start, s.Length, rest.Metrics.IconSize)).ToArray(),
        0,
        0);
}
