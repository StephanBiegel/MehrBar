using MehrBar.Core.Model;

namespace MehrBar.Core.Layout;

/// <summary>A drawn slot of the bar at rest, relative to the bar start.</summary>
/// <param name="ItemIndex">Index of the entry in the bar's whole item list (hidden types included).</param>
/// <param name="Start">Near edge along u.</param>
/// <param name="Length">Length along u (one pitch).</param>
public sealed record RestSlot(int ItemIndex, double Start, double Length);

/// <summary>The bar at rest: drawn slots, total length along the edge and depth away from it.</summary>
public sealed record RestLayout(IReadOnlyList<RestSlot> Slots, double Length, double Depth, BarMetrics Metrics)
{
    /// <summary>The slots as input for <see cref="ZoomEngine"/>.</summary>
    public IReadOnlyList<RestItem> ToRestItems() => Slots.Select(s => new RestItem(s.Start, s.Length, Zooms: true)).ToArray();
}

/// <summary>Builds the rest layout from the items of a bar.</summary>
public static class RestLayoutBuilder
{
    /// <summary>
    /// Only app and folder entries become slots in phase 2; url, separator and unknown entries stay in the list but are
    /// not drawn. Slot k starts at padding + k × pitch; the bar is at least <see cref="BarMetrics.MinSlots"/> long.
    /// </summary>
    public static RestLayout Build(IReadOnlyList<DockItem> items, BarMetrics metrics)
    {
        var slots = new List<RestSlot>();
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] is AppItem or FolderItem)
            {
                slots.Add(new RestSlot(i, metrics.Padding + (slots.Count * metrics.Pitch), metrics.Pitch));
            }
        }

        double length = (2 * metrics.Padding) + (Math.Max(metrics.MinSlots, slots.Count) * metrics.Pitch);
        return new RestLayout(slots, length, metrics.BackgroundDepth, metrics);
    }
}
