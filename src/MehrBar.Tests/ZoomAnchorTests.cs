using MehrBar.Core.Layout;
using MehrBar.Tests.Fixtures;

namespace MehrBar.Tests;

/// <summary>
/// Q5 (phase 1 spike): the zoom math in <c>MehrBar.Core.Layout</c> against the measured RocketDock reference.
/// </summary>
public class ZoomAnchorTests
{
    private const double RestMiddle = (RocketDockReference.RestStart + RocketDockReference.RestEnd) / 2;

    [Fact]
    public void Rest_layout_reproduces_reference_extent()
    {
        var items = RocketDockReference.BuildRestItems();
        Assert.Equal(RocketDockReference.ItemCount, items.Count);

        // First slot and last slot end span the bar between its measured margins; the bar edges are 109 and 698 (+-1).
        double firstStart = items[0].Start;
        double lastEnd = items[^1].Start + items[^1].Length;
        Assert.InRange(firstStart - RocketDockReference.LeadingMargin, 108, 110);
        Assert.InRange(lastEnd + RocketDockReference.TrailingMargin, 697, 699);

        // 16 icons in groups 6 / 6 / 3 / 1 separated by 3 separators.
        var separators = RocketDockReference.SeparatorIndices;
        Assert.Equal(3, separators.Length);
        foreach (int s in separators)
        {
            Assert.Equal(RocketDockReference.SeparatorLength, items[s].Length);
        }

        int icons = items.Count(i => i.Length == RocketDockReference.Pitch);
        Assert.Equal(16, icons);
        Assert.Equal(new[] { 6, 6, 3, 1 }, GroupSizesOf(items, separators));

        // Items are contiguous.
        for (int i = 1; i < items.Count; i++)
        {
            Assert.Equal(items[i - 1].Start + items[i - 1].Length, items[i].Start, 6);
        }
    }

    [Fact]
    public void Kernel_matches_measured_falloff()
    {
        const double icon = 32, zoom = 121, radius = 2.5;

        double Size(double d) => icon + (zoom - icon) * ZoomKernel.Factor(d, radius);

        Assert.Equal(121, Size(0), 6);
        Assert.InRange(Size(1), 88, 95);   // direct neighbours: about 90..95 px
        Assert.InRange(Size(2), 36, 44);   // next-but-one: about 40 px
        Assert.Equal(32, Size(2.5), 6);    // at and beyond the radius: rest size
        Assert.Equal(32, Size(4), 6);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(1)]
    [InlineData(16)]
    [InlineData(18)]
    public void Center_anchor_keeps_bar_middle_fixed(int itemIndex)
    {
        var items = RocketDockReference.BuildRestItems();
        var p = new ZoomParams(RocketDockReference.IconSize, RocketDockReference.ZoomSize, 2.5);

        for (double fraction = 0; fraction <= 1; fraction += 0.25)
        {
            double pointer = items[itemIndex].Start + fraction * items[itemIndex].Length;
            ZoomResult r = ZoomEngine.Compute(items, RocketDockReference.Pitch, p, pointer, ZoomAnchorMode.Center);

            Assert.Equal(r.ExtraBefore, r.ExtraAfter, 2);
            double displayedMiddle = ((RocketDockReference.RestStart - r.ExtraBefore) + (RocketDockReference.RestEnd + r.ExtraAfter)) / 2;
            Assert.InRange(displayedMiddle, RestMiddle - 0.5, RestMiddle + 0.5);
            Assert.True(r.ExtraBefore > 0);
        }
    }

    private static int[] GroupSizesOf(IReadOnlyList<RestItem> items, int[] separators)
    {
        var sizes = new List<int>();
        int previous = -1;
        foreach (int s in separators)
        {
            sizes.Add(s - previous - 1);
            previous = s;
        }

        sizes.Add(items.Count - previous - 1);
        return sizes.ToArray();
    }
}
