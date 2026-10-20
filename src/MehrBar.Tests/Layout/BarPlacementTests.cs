using MehrBar.Core.Layout;

namespace MehrBar.Tests.Layout;

/// <summary>
/// LEIS-01/LEIS-02: the bar is placed by edge, alignment, offset and edge offset (also negative) on the monitor
/// rectangle, along the abstract bar axis.
/// </summary>
public class BarPlacementTests
{
    private static readonly RectI FullHd = new(0, 0, 1920, 1080);

    [Fact]
    public void Default_top_center_is_pushed_five_pixels_past_the_edge()
    {
        RectI bar = BarPlacement.Compute(FullHd, new PlacementSpec(BarEdge.Top, BarAlign.Center, 0, -5), 108, 44);

        Assert.Equal(new RectI(906, -5, 108, 44), bar);
    }

    [Fact]
    public void Start_and_end_apply_the_signed_offset_after_the_alignment()
    {
        RectI start = BarPlacement.Compute(FullHd, new PlacementSpec(BarEdge.Top, BarAlign.Start, 10, 0), 108, 44);
        RectI end = BarPlacement.Compute(FullHd, new PlacementSpec(BarEdge.Top, BarAlign.End, -10, 0), 108, 44);

        Assert.Equal(10, start.Left);
        Assert.Equal(1802, end.Left);
    }

    [Fact]
    public void Bottom_edge_offset_is_measured_from_the_lower_edge()
    {
        RectI bar = BarPlacement.Compute(FullHd, new PlacementSpec(BarEdge.Bottom, BarAlign.Center, 0, -5), 108, 44);

        Assert.Equal(1041, bar.Top);
        Assert.Equal(906, bar.Left);
    }

    [Fact]
    public void Left_and_right_edges_mirror_each_other()
    {
        RectI left = BarPlacement.Compute(FullHd, new PlacementSpec(BarEdge.Left, BarAlign.Center, 0, -5), 108, 44);
        RectI right = BarPlacement.Compute(FullHd, new PlacementSpec(BarEdge.Right, BarAlign.Center, 0, -5), 108, 44);

        Assert.Equal(new RectI(-5, 486, 44, 108), left);
        Assert.Equal(new RectI(1881, 486, 44, 108), right);
    }

    [Fact]
    public void Bar_longer_than_the_monitor_is_clamped_to_the_monitor_start()
    {
        RectI bar = BarPlacement.Compute(FullHd, new PlacementSpec(BarEdge.Top, BarAlign.Center, 0, 0), 2500, 44);

        Assert.Equal(0, bar.Left);
        Assert.Equal(1920, bar.Width);
    }

    [Theory]
    [InlineData(BarAlign.Start)]
    [InlineData(BarAlign.Center)]
    [InlineData(BarAlign.End)]
    public void Offset_shifts_every_alignment_along_the_edge(BarAlign align)
    {
        RectI plain = BarPlacement.Compute(FullHd, new PlacementSpec(BarEdge.Top, align, 0, 0), 108, 44);
        RectI shifted = BarPlacement.Compute(FullHd, new PlacementSpec(BarEdge.Top, align, 20, 0), 108, 44);

        Assert.Equal(plain.Left + 20, shifted.Left);
        Assert.Equal(plain.Top, shifted.Top);
    }

    [Theory]
    [InlineData(BarEdge.Left)]
    [InlineData(BarEdge.Right)]
    public void Offset_on_the_side_edges_runs_along_y(BarEdge edge)
    {
        RectI plain = BarPlacement.Compute(FullHd, new PlacementSpec(edge, BarAlign.Center, 0, 0), 108, 44);
        RectI shifted = BarPlacement.Compute(FullHd, new PlacementSpec(edge, BarAlign.Center, 20, 0), 108, 44);

        Assert.Equal(plain.Top + 20, shifted.Top);
        Assert.Equal(plain.Left, shifted.Left);
    }

    [Fact]
    public void Monitor_with_a_negative_origin_is_respected()
    {
        var secondary = new RectI(-1600, 0, 1600, 900);

        RectI bar = BarPlacement.Compute(secondary, new PlacementSpec(BarEdge.Top, BarAlign.Center, 0, -5), 108, 44);

        Assert.Equal(new RectI(-854, -5, 108, 44), bar);
    }
}
