using MehrBar.Core.Layout;
using MehrBar.Tests.Fixtures;

namespace MehrBar.Tests;

/// <summary>
/// Q5 (phase 1 spike): the zoom math in <c>MehrBar.Core.Layout</c> against the measured RocketDock reference.
/// </summary>
public class ZoomAnchorTests
{
    private const double RestMiddle = (RocketDockReference.RestStart + RocketDockReference.RestEnd) / 2;

    /// <summary>The Q5 tolerance (success criterion 5): +-6 px.</summary>
    private const double Tolerance = 6;

    private static readonly ZoomParams Measured =
        new(RocketDockReference.IconSize, RocketDockReference.ZoomSize, RocketDockReference.MeasuredRadius);

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

        // The separator line sits in the middle of its slot: the measured rest line positions (image 1) are reproduced.
        for (int s = 0; s < separators.Length; s++)
        {
            double centre = items[separators[s]].Start + items[separators[s]].Length / 2;
            Assert.Equal(RocketDockReference.RestSeparatorCentres[s], centre, 1);
        }

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

    [Fact]
    public void Measured_radius_is_the_mean_of_the_neighbour_samples()
    {
        // Recomputes the radius from the measured glyph widths of image 2 (neighbours of the cursor icon); the bar edges
        // do not enter. The pointer is the one of the "Maus mittig" row.
        var items = RocketDockReference.BuildRestItems();
        var row = RocketDockReference.Rows[0];
        double pointer = items[row.PointerItemIndex].Start + row.PointerFraction * items[row.PointerItemIndex].Length;

        var radii = new List<double>();
        foreach (var sample in RocketDockReference.RadiusSamples)
        {
            double size = RocketDockReference.IconSize * sample.ZoomedWidth / sample.RestWidth;
            double f = (size - RocketDockReference.IconSize) / (RocketDockReference.ZoomSize - RocketDockReference.IconSize);
            double centre = items[sample.ItemIndex].Start + items[sample.ItemIndex].Length / 2;
            double d = Math.Abs(pointer - centre) / RocketDockReference.Pitch;
            radii.Add(d * Math.PI / Math.Acos(2 * f - 1));
        }

        Assert.Equal(RocketDockReference.MeasuredRadius, radii.Average(), 2);
        Assert.InRange(radii.Min(), 2.3, 2.5);   // next-but-one icons
        Assert.InRange(radii.Max(), 2.6, 2.8);   // direct neighbours
    }

    /// <summary>
    /// Q5 prediction: with the measured pointer fraction and the measured radius, anchor Cursor reproduces the displayed
    /// bar edges of each reference row within +-6 px. A cropped left edge (-3 in three rows) is only a bound: the true
    /// edge must lie at or beyond it (at most -3 + 6).
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Cursor_anchor_predicts_reference_row(int rowIndex)
    {
        var row = RocketDockReference.Rows[rowIndex];
        var (before, after) = PredictEdges(row, ZoomAnchorMode.Cursor);

        Assert.InRange(after, row.ExpectedAfter - Tolerance, row.ExpectedAfter + Tolerance);
        if (row.BeforeCropped)
        {
            Assert.True(
                before <= row.ExpectedBefore + Tolerance,
                $"{row.Name}: before edge {before:F1} lies right of the crop bound {row.ExpectedBefore}");
        }
        else
        {
            Assert.InRange(before, row.ExpectedBefore - Tolerance, row.ExpectedBefore + Tolerance);
        }
    }

    /// <summary>
    /// Second prediction, independent of the bar edges: the three separator lines of each screenshot (thin lines in the
    /// bar rows) sit at the centres of the displayed separator slots.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Cursor_anchor_predicts_separator_lines(int rowIndex)
    {
        var row = RocketDockReference.Rows[rowIndex];
        var centres = PredictSeparatorCentres(row, RocketDockReference.BuildRestItems(), ZoomAnchorMode.Cursor);

        for (int s = 0; s < centres.Length; s++)
        {
            Assert.InRange(centres[s], row.SeparatorCentres[s] - Tolerance, row.SeparatorCentres[s] + Tolerance);
        }
    }

    /// <summary>
    /// Falsification control: with exactly the same measured inputs the Center anchor must miss at least one
    /// uncropped edge by more than 6 px, otherwise the rows could not tell the anchors apart.
    /// </summary>
    [Fact]
    public void Center_anchor_misses_reference_rows()
    {
        double worst = 0;
        string worstWhere = string.Empty;
        foreach (var row in RocketDockReference.Rows)
        {
            var (before, after) = PredictEdges(row, ZoomAnchorMode.Center);
            double afterMiss = Math.Abs(after - row.ExpectedAfter);
            if (afterMiss > worst)
            {
                worst = afterMiss;
                worstWhere = $"{row.Name} after";
            }

            if (!row.BeforeCropped)
            {
                double beforeMiss = Math.Abs(before - row.ExpectedBefore);
                if (beforeMiss > worst)
                {
                    worst = beforeMiss;
                    worstWhere = $"{row.Name} before";
                }
            }
        }

        Assert.True(
            worst > Tolerance,
            $"Center anchor fits every uncropped edge (largest miss {worst:F1} px at {worstWhere}): the rows do not discriminate");

        // The "2. Symbol" row is asymmetric (table: -82 / +114 against rest): Center must miss it on both sides.
        var second = RocketDockReference.Rows[1];
        var (b2, a2) = PredictEdges(second, ZoomAnchorMode.Center);
        Assert.True(Math.Abs(b2 - second.ExpectedBefore) > Tolerance);
        Assert.True(Math.Abs(a2 - second.ExpectedAfter) > Tolerance);
    }

    /// <summary>
    /// Documents why the fixture lets separator slots scale with the zoom: with fixed-length separators (the plan's
    /// first hypothesis) the Publisher row misses its right edge and its separator line by far more than 6 px, and the
    /// Excel row pushes its left edge far right of the crop bound. The scaling is a measured gap, not a fit: image 4
    /// shows 63 px between the Publisher glyph and the Excel glyph against 18 px at rest.
    /// </summary>
    [Fact]
    public void Fixed_separators_miss_the_rows_next_to_a_separator()
    {
        var fixedItems = RocketDockReference.BuildRestItems(separatorsZoom: false);

        var publisher = RocketDockReference.Rows[2];
        var (_, after) = PredictEdges(publisher, ZoomAnchorMode.Cursor, fixedItems);
        Assert.True(Math.Abs(after - publisher.ExpectedAfter) > Tolerance);
        double line = PredictSeparatorCentres(publisher, fixedItems, ZoomAnchorMode.Cursor)[2];
        Assert.True(Math.Abs(line - publisher.SeparatorCentres[2]) > Tolerance);

        var excel = RocketDockReference.Rows[3];
        var (excelBefore, _) = PredictEdges(excel, ZoomAnchorMode.Cursor, fixedItems);
        Assert.True(excelBefore > excel.ExpectedBefore + Tolerance);
    }

    private static (double Before, double After) PredictEdges(
        RocketDockReference.Row row, ZoomAnchorMode anchor, IReadOnlyList<RestItem>? items = null)
    {
        items ??= RocketDockReference.BuildRestItems();
        ZoomResult r = Compute(row, items, anchor);
        return (RocketDockReference.RestStart - r.ExtraBefore, RocketDockReference.RestEnd + r.ExtraAfter);
    }

    private static double[] PredictSeparatorCentres(
        RocketDockReference.Row row, IReadOnlyList<RestItem> items, ZoomAnchorMode anchor)
    {
        ZoomResult r = Compute(row, items, anchor);
        return RocketDockReference.SeparatorIndices
            .Select(i => r.DisplayedStarts[i] + r.DisplayedLengths[i] / 2)
            .ToArray();
    }

    private static ZoomResult Compute(RocketDockReference.Row row, IReadOnlyList<RestItem> items, ZoomAnchorMode anchor)
    {
        var cursorItem = items[row.PointerItemIndex];
        double pointer = cursorItem.Start + row.PointerFraction * cursorItem.Length;
        return ZoomEngine.Compute(items, RocketDockReference.Pitch, Measured, pointer, anchor);
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
