using MehrBar.Core.Layout;

namespace MehrBar.Tests.Layout;

/// <summary>LEIS-06: the window is the bar at rest plus the largest zoom growth, clamped to the monitor.</summary>
public class WindowPlanTests
{
    private static readonly BarMetrics Standard = new(32, 121, 2.58, 0, 6, 3);
    private static readonly RectI FullHd = new(0, 0, 1920, 1080);
    private static readonly PlacementSpec TopCenter = new(BarEdge.Top, BarAlign.Center, 0, -5);

    [Fact]
    public void Twenty_icons_get_room_for_the_zoom_on_both_sides()
    {
        WindowPlan plan = WindowPlan.Compute(FullHd, TopCenter, Rest(20));

        Assert.Equal(new RectI(634, -5, 652, 44), plan.BarRect);

        // ComputeSmooth splits the extra room about half and half (01-SPIKE-frame-pacing.md): about 116 px per side
        // with the defaults, less than the 230 px the plan estimated from the cursor split.
        Assert.InRange(plan.MaxGrowthBefore, 80, 300);
        Assert.InRange(plan.MaxGrowthAfter, 80, 300);
        Assert.True(plan.WindowRect.Left <= plan.BarRect.Left - plan.MaxGrowthBefore);
        Assert.True(plan.WindowRect.Right >= plan.BarRect.Right + plan.MaxGrowthAfter);
        Assert.Equal(plan.BarRect.Left - plan.WindowRect.Left, plan.BarStartInWindowU);
    }

    [Fact]
    public void Empty_bar_has_no_growth_along_the_edge()
    {
        WindowPlan plan = WindowPlan.Compute(FullHd, TopCenter, Rest(0));

        Assert.Equal(0, plan.MaxGrowthBefore);
        Assert.Equal(0, plan.MaxGrowthAfter);
        Assert.Equal(plan.BarRect.Left, plan.WindowRect.Left);
        Assert.Equal(plan.BarRect.Width, plan.WindowRect.Width);
        Assert.Equal(44 + 89, plan.WindowRect.Height);
    }

    [Fact]
    public void Negative_edge_offset_starts_window_and_bar_past_the_edge()
    {
        WindowPlan plan = WindowPlan.Compute(FullHd, TopCenter, Rest(5));

        Assert.Equal(-5, plan.WindowRect.Top);
        Assert.Equal(-5, plan.BarRect.Top);
    }

    [Fact]
    public void Window_is_clamped_to_the_monitor_without_moving_the_bar()
    {
        WindowPlan plan = WindowPlan.Compute(FullHd, TopCenter with { Align = BarAlign.Start }, Rest(10));

        Assert.Equal(0, plan.BarRect.Left);
        Assert.Equal(0, plan.WindowRect.Left);
        Assert.Equal(0, plan.BarStartInWindowU);
    }

    [Fact]
    public void Smaller_monitor_gives_a_new_centre_with_the_same_sizes()
    {
        WindowPlan wide = WindowPlan.Compute(FullHd, TopCenter, Rest(20));
        WindowPlan narrow = WindowPlan.Compute(new RectI(0, 0, 1600, 900), TopCenter, Rest(20));

        Assert.Equal(474, narrow.BarRect.Left);
        Assert.Equal(wide.BarRect.Width, narrow.BarRect.Width);
        Assert.Equal(wide.WindowRect.Width, narrow.WindowRect.Width);
    }

    [Fact]
    public void Bottom_edge_grows_upwards()
    {
        WindowPlan plan = WindowPlan.Compute(FullHd, TopCenter with { Edge = BarEdge.Bottom }, Rest(5));

        Assert.Equal(plan.BarRect.Bottom, plan.WindowRect.Bottom);
        Assert.Equal(133, plan.WindowRect.Height);
    }

    [Fact]
    public void Left_edge_gives_a_vertical_window()
    {
        WindowPlan plan = WindowPlan.Compute(FullHd, TopCenter with { Edge = BarEdge.Left }, Rest(10));

        Assert.Equal(plan.BarRect.Left, plan.WindowRect.Left);
        Assert.Equal(133, plan.WindowRect.Width);
        Assert.True(plan.WindowRect.Height > plan.BarRect.Height);
        Assert.True(plan.WindowRect.Top < plan.BarRect.Top);
    }

    private static RestLayout Rest(int apps) => RestLayoutBuilder.Build(RestLayoutTests.Apps(apps), Standard);
}
