using MehrBar.Core.Layout;
using MehrBar.Tests.Fixtures;

namespace MehrBar.Tests;

/// <summary>
/// Plan 01-19: the items outside the zoom radius must stand still while the pointer travels (Stephan saw them jitter in
/// the Q4 run). Compares the sampled <see cref="ZoomEngine.Compute"/> with <see cref="ZoomEngine.ComputeSmooth"/>.
/// </summary>
public class ZoomSmoothnessTests
{
    private const double Tolerance = 6;

    private static readonly ZoomParams Q4Params = new(32, 121, 2.5);

    [Fact]
    public void Sampled_cursor_anchor_swings_the_outer_items()
    {
        var (minStart, maxStart) = SweepFirstStart(
            pointer => ZoomEngine.Compute(LongBar(), 36, Q4Params, pointer, ZoomAnchorMode.Cursor));

        // Documents the defect: about 1.3 px back and forth once per slot crossed.
        Assert.True(maxStart - minStart > 1.0, $"swing {maxStart - minStart:F2} px");
    }

    [Fact]
    public void Smooth_outer_items_stand_still_away_from_the_bar_ends()
    {
        var (minStart, maxStart) = SweepFirstStart(
            pointer => ZoomEngine.ComputeSmooth(LongBar(), 36, Q4Params, pointer));

        Assert.True(maxStart - minStart < 1e-9, $"swing {maxStart - minStart:E2} px");
    }

    [Fact]
    public void Smooth_layout_moves_continuously()
    {
        // Slots next to the pointer move about (1 + growth) times as fast as the pointer in both variants; a jump would
        // be far more than that.
        var items = LongBar();
        ZoomResult previous = ZoomEngine.ComputeSmooth(items, 36, Q4Params, items[2].Start);
        for (double pointer = items[2].Start + 0.25; pointer < items[17].Start; pointer += 0.25)
        {
            ZoomResult r = ZoomEngine.ComputeSmooth(items, 36, Q4Params, pointer);
            for (int i = 0; i < items.Length; i++)
            {
                Assert.True(
                    Math.Abs(r.DisplayedStarts[i] - previous.DisplayedStarts[i]) < 1.0,
                    $"item {i} jumps at pointer {pointer}");
            }

            previous = r;
        }
    }

    [Fact]
    public void Smooth_keeps_the_sampled_slot_sizes()
    {
        var items = LongBar();
        for (double pointer = 0; pointer <= 20 * 36.0; pointer += 7.3)
        {
            ZoomResult smooth = ZoomEngine.ComputeSmooth(items, 36, Q4Params, pointer);
            ZoomResult sampled = ZoomEngine.Compute(items, 36, Q4Params, pointer, ZoomAnchorMode.Cursor);
            Assert.Equal(sampled.DisplayedLengths, smooth.DisplayedLengths);
        }
    }

    [Fact]
    public void Smooth_holds_the_rest_point_under_the_pointer_closely()
    {
        // Mid-bar the point under the pointer drifts by the former swing (< 1 px); within the zoom radius of a bar end, where the
        // kernel is cut off, by up to 2.5 px (measured 2.50 at r = 2.5). The sampled Cursor split holds it exactly.
        var items = LongBar();
        double worstMiddle = 0;
        double worstEnds = 0;
        for (double pointer = 0.5; pointer < 20 * 36.0; pointer += 0.25)
        {
            ZoomResult r = ZoomEngine.ComputeSmooth(items, 36, Q4Params, pointer);
            int k = Math.Min((int)(pointer / 36), items.Length - 1);
            double f = (pointer - items[k].Start) / items[k].Length;
            double drift = Math.Abs(r.DisplayedStarts[k] + f * r.DisplayedLengths[k] - pointer);
            if (k < 3 || k > 16) // within the zoom radius of a bar end
            {
                worstEnds = Math.Max(worstEnds, drift);
            }
            else
            {
                worstMiddle = Math.Max(worstMiddle, drift);
            }
        }

        Assert.True(worstMiddle < 1.0, $"drift mid-bar {worstMiddle:F2} px");
        Assert.True(worstEnds < 3.0, $"drift near the bar ends {worstEnds:F2} px");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Smooth_predicts_reference_row(int rowIndex)
    {
        var row = RocketDockReference.Rows[rowIndex];
        var items = RocketDockReference.BuildRestItems();
        var cursorItem = items[row.PointerItemIndex];
        double pointer = cursorItem.Start + row.PointerFraction * cursorItem.Length;
        var p = new ZoomParams(RocketDockReference.IconSize, RocketDockReference.ZoomSize, RocketDockReference.MeasuredRadius);
        ZoomResult r = ZoomEngine.ComputeSmooth(items, RocketDockReference.Pitch, p, pointer);

        double before = RocketDockReference.RestStart - r.ExtraBefore;
        double after = RocketDockReference.RestEnd + r.ExtraAfter;
        Assert.InRange(after, row.ExpectedAfter - Tolerance, row.ExpectedAfter + Tolerance);
        if (row.BeforeCropped)
        {
            Assert.True(before <= row.ExpectedBefore + Tolerance, $"{row.Name}: before edge {before:F1}");
        }
        else
        {
            Assert.InRange(before, row.ExpectedBefore - Tolerance, row.ExpectedBefore + Tolerance);
        }

        int[] separators = RocketDockReference.SeparatorIndices;
        for (int s = 0; s < separators.Length; s++)
        {
            double centre = r.DisplayedStarts[separators[s]] + r.DisplayedLengths[separators[s]] / 2;
            Assert.InRange(centre, row.SeparatorCentres[s] - Tolerance, row.SeparatorCentres[s] + Tolerance);
        }
    }

    /// <summary>20 icon slots of 36 px (the Q4 workload with 20 icons).</summary>
    private static RestItem[] LongBar() =>
        Enumerable.Range(0, 20).Select(i => new RestItem(i * 36.0, 36, true)).ToArray();

    /// <summary>Sweeps the pointer over the middle of <see cref="LongBar"/> (far from both ends) and returns the range of the first item's start.</summary>
    private static (double Min, double Max) SweepFirstStart(Func<double, ZoomResult> compute)
    {
        double min = double.MaxValue;
        double max = double.MinValue;
        for (double pointer = 7 * 36.0; pointer <= 13 * 36.0; pointer += 0.25)
        {
            double start = compute(pointer).DisplayedStarts[0];
            min = Math.Min(min, start);
            max = Math.Max(max, start);
        }

        return (min, max);
    }
}
