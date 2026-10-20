namespace MehrBar.Core.Layout;

/// <summary>What a point on the bar window hits.</summary>
public enum HitKind
{
    None,
    Bar,
    Item,
}

/// <summary>Result of <see cref="HitTest.At"/>; <see cref="ItemIndex"/> is the index in the whole item list.</summary>
public readonly record struct HitResult(HitKind Kind, int? ItemIndex);

/// <summary>
/// Hit test on the drawn frame. Hits go by the index in the whole item list, so hidden types (url, separator,
/// unknown) never shift a hit to the wrong entry (RESEARCH Pitfall 10).
/// </summary>
public static class HitTest
{
    /// <param name="frame">The frame as drawn.</param>
    /// <param name="rest">The bar at rest.</param>
    /// <param name="u">Point along the edge, relative to the bar start at rest.</param>
    /// <param name="v">Point away from the edge, relative to the edge side of the bar.</param>
    public static HitResult At(FrameLayout frame, RestLayout rest, double u, double v)
    {
        if (v < 0 || v > rest.Depth || u < -frame.ExtraBefore || u > rest.Length + frame.ExtraAfter)
        {
            return new HitResult(HitKind.None, null);
        }

        foreach (FrameSlot slot in frame.Slots)
        {
            if (u >= slot.Start && u < slot.Start + slot.Length)
            {
                return new HitResult(HitKind.Item, slot.ItemIndex);
            }
        }

        return new HitResult(HitKind.Bar, null);
    }
}
