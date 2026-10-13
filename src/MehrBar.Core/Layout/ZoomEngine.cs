namespace MehrBar.Core.Layout;

/// <summary>
/// Which part of the bar stays where it is when the zoomed icons take up extra room.
/// </summary>
public enum ZoomAnchorMode
{
    /// <summary>
    /// The icon under the pointer stays put (RocketDock behaviour, Q12 revised): everything before it gives way by
    /// the extra room of the items before it, everything after it by the extra room of the items after it, and the
    /// icon under the pointer splits its own extra room by the pointer's position inside it.
    /// </summary>
    Cursor,

    /// <summary>The middle of the bar stays put; both ends move outwards by the same amount.</summary>
    Center,
}

/// <summary>Zoom parameters. Sizes in pixels, radius in slot pitches.</summary>
/// <param name="IconSize">Size of an icon at rest; also the size the slot pitch is measured in.</param>
/// <param name="ZoomSize">Slot size of the icon directly under the pointer.</param>
/// <param name="Radius">Zoom radius in slot pitches (about 2.5 for the original at zoom width 4).</param>
public readonly record struct ZoomParams(double IconSize, double ZoomSize, double Radius);

/// <summary>An item of the bar at rest, along the bar axis.</summary>
/// <param name="Start">Rest position of the slot's near edge along the axis ("before" = smaller).</param>
/// <param name="Length">Rest length of the slot along the axis.</param>
/// <param name="Zooms">
/// Whether the slot scales with the zoom factor. Icons always do. For separators this is the open question the
/// reference answers: the measured separator gap grows with the zoom (see 01-SPIKE-zoom-anchor.md), so the fixture
/// sets it to true for them; the line image itself is not scaled, only the room it occupies.
/// </param>
public readonly record struct RestItem(double Start, double Length, bool Zooms);

/// <summary>Result of <see cref="ZoomEngine.Compute"/>, in the same axis as the input.</summary>
/// <param name="ExtraBefore">How far the near end of the bar moved towards "before" (smaller values).</param>
/// <param name="ExtraAfter">How far the far end of the bar moved towards "after" (larger values).</param>
/// <param name="DisplayedStarts">Displayed near edge of each slot.</param>
/// <param name="DisplayedLengths">Displayed length of each slot (rest length plus its extra).</param>
public sealed record ZoomResult(
    double ExtraBefore,
    double ExtraAfter,
    IReadOnlyList<double> DisplayedStarts,
    IReadOnlyList<double> DisplayedLengths);

/// <summary>
/// Pure zoom layout along an abstract bar axis (axis-neutral: the same code serves a horizontal and a vertical bar).
/// </summary>
public static class ZoomEngine
{
    /// <summary>
    /// Computes the displayed layout of a bar for a pointer position.
    /// <para>
    /// Rule 1 (rest space): the distance of an item is measured from its <b>rest</b> centre to the pointer, divided by
    /// <paramref name="pitch"/>; displayed positions never feed back into the distance.
    /// </para>
    /// <para>
    /// Rule 2 (extra room): a zooming item's slot grows by <c>Length * (ZoomSize / IconSize - 1) * Factor(d, Radius)</c>;
    /// for an icon whose slot equals the icon size this is <c>(ZoomSize - IconSize) * Factor</c>. Items that do not zoom
    /// keep their length and are only moved.
    /// </para>
    /// <para>
    /// Rule 3 (anchor): with <see cref="ZoomAnchorMode.Cursor"/> the slot containing <paramref name="pointerU"/> splits
    /// its extra by the pointer's fraction f inside it (<c>f * extra</c> goes before, the rest after); the extras of all
    /// other items go wholly before or after. With <see cref="ZoomAnchorMode.Center"/> the total is split in half. The
    /// displayed bar is <c>[restStart - ExtraBefore, restEnd + ExtraAfter]</c>.
    /// </para>
    /// </summary>
    /// <param name="items">Slots at rest, ascending and contiguous along the axis.</param>
    /// <param name="pitch">Distance unit (one icon slot) in axis units.</param>
    /// <param name="p">Zoom parameters.</param>
    /// <param name="pointerU">Pointer position along the axis, same frame as <paramref name="items"/>.</param>
    /// <param name="anchor">Which part of the bar stays fixed.</param>
    public static ZoomResult Compute(
        IReadOnlyList<RestItem> items,
        double pitch,
        ZoomParams p,
        double pointerU,
        ZoomAnchorMode anchor)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(pitch, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(p.IconSize, 0);

        int n = items.Count;
        var extra = new double[n];
        double growth = p.ZoomSize / p.IconSize - 1;
        double total = 0;
        for (int i = 0; i < n; i++)
        {
            if (!items[i].Zooms)
            {
                continue;
            }

            double centre = items[i].Start + items[i].Length / 2;
            double d = Math.Abs(pointerU - centre) / pitch;
            extra[i] = items[i].Length * growth * ZoomKernel.Factor(d, p.Radius);
            total += extra[i];
        }

        double before;
        if (n == 0)
        {
            before = 0;
        }
        else if (anchor == ZoomAnchorMode.Center)
        {
            before = total / 2;
        }
        else
        {
            int k = CursorItem(items, pointerU);
            double f = Math.Clamp((pointerU - items[k].Start) / items[k].Length, 0, 1);
            before = f * extra[k];
            for (int i = 0; i < k; i++)
            {
                before += extra[i];
            }
        }

        var starts = new double[n];
        var lengths = new double[n];
        double shifted = 0; // extra room of all items before i
        for (int i = 0; i < n; i++)
        {
            starts[i] = items[i].Start - before + shifted;
            lengths[i] = items[i].Length + extra[i];
            shifted += extra[i];
        }

        return new ZoomResult(before, total - before, starts, lengths);
    }

    /// <summary>
    /// Smooth variant of <see cref="ZoomAnchorMode.Cursor"/> (plan 01-19). Slot sizes are exactly those of
    /// <see cref="Compute"/> (sampled kernel, the measured falloff), only the split of the total extra room between the
    /// two sides is continuous: the share that goes before is the share of the kernel's mass that lies before the
    /// pointer, integrated along the rest axis over the zooming slots. Away from the bar ends that share is exactly one
    /// half, so the items outside the zoom radius stand still while the pointer travels (the sampled Cursor split lets
    /// them swing by about +-0.6 px once per slot crossed); near an end the kernel is cut off and the bar grows towards
    /// the open side, as with Cursor. The price: the rest point under the pointer is no longer held exactly, it may
    /// drift by the former swing (well under a pixel at the default parameters).
    /// <para>
    /// With a whole-number radius the sum of the sampled extras is the same for every pointer position, so both bar ends
    /// stand still; with a fractional radius the total varies slightly (1.2 px at r = 2.58) and each end moves by half.
    /// </para>
    /// </summary>
    public static ZoomResult ComputeSmooth(
        IReadOnlyList<RestItem> items,
        double pitch,
        ZoomParams p,
        double pointerU)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(pitch, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(p.IconSize, 0);

        int n = items.Count;
        double growth = p.ZoomSize / p.IconSize - 1;
        var starts = new double[n];
        var lengths = new double[n];
        double total = 0;
        double massBefore = 0;
        double mass = 0;
        for (int i = 0; i < n; i++)
        {
            double extra = 0;
            if (items[i].Zooms)
            {
                double centre = items[i].Start + items[i].Length / 2;
                extra = items[i].Length * growth * ZoomKernel.Factor(Math.Abs(pointerU - centre) / pitch, p.Radius);
                double a = (items[i].Start - pointerU) / pitch;
                double b = (items[i].Start + items[i].Length - pointerU) / pitch;
                mass += ZoomKernel.Integral(a, b, p.Radius);
                if (a < 0)
                {
                    massBefore += ZoomKernel.Integral(a, Math.Min(b, 0), p.Radius);
                }
            }

            lengths[i] = items[i].Length + extra;
            total += extra;
        }

        double before = mass > 0 ? total * massBefore / mass : 0;
        double shifted = 0;
        for (int i = 0; i < n; i++)
        {
            starts[i] = items[i].Start - before + shifted;
            shifted += lengths[i] - items[i].Length;
        }

        return new ZoomResult(before, total - before, starts, lengths);
    }

    /// <summary>The last slot that starts at or before the pointer (the first slot when the pointer is before the bar).</summary>
    private static int CursorItem(IReadOnlyList<RestItem> items, double pointerU)
    {
        int k = 0;
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].Start <= pointerU)
            {
                k = i;
            }
            else
            {
                break;
            }
        }

        return k;
    }
}
