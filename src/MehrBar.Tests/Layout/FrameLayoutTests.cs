using MehrBar.Core.Layout;

namespace MehrBar.Tests.Layout;

/// <summary>D-04: the layout contract; progress 0 is exactly the bar at rest.</summary>
public class FrameLayoutTests
{
    private static readonly BarMetrics Standard = new(32, 121, 2.58, 0, 6, 3);

    [Fact]
    public void Progress_zero_gives_the_rest_positions_for_any_pointer()
    {
        RestLayout rest = RestLayoutBuilder.Build(RestLayoutTests.Apps(8), Standard);

        FrameLayout frame = FrameLayout.Compute(rest, 77, 0);

        Assert.Equal(0, frame.ExtraBefore);
        Assert.Equal(0, frame.ExtraAfter);
        Assert.Equal(rest.Slots.Select(s => (s.Start, s.Length)), frame.Slots.Select(s => (s.Start, s.Length)));
        Assert.All(frame.Slots, s => Assert.Equal(32, s.IconSize));
    }

    [Fact]
    public void No_pointer_gives_the_rest_positions()
    {
        RestLayout rest = RestLayoutBuilder.Build(RestLayoutTests.Apps(5), Standard);

        FrameLayout frame = FrameLayout.Compute(rest, null, 1);

        Assert.Equal(rest.Slots.Select(s => s.Start), frame.Slots.Select(s => s.Start));
    }

    [Fact]
    public void Full_zoom_on_a_slot_centre_gives_the_zoom_size()
    {
        RestLayout rest = RestLayoutBuilder.Build(RestLayoutTests.Apps(9), Standard);
        RestSlot middle = rest.Slots[4];

        FrameLayout frame = FrameLayout.Compute(rest, middle.Start + (middle.Length / 2), 1);

        Assert.Equal(121, frame.Slots[4].Length, 6);
        Assert.Equal(121, frame.Slots[4].IconSize, 6);
        Assert.True(frame.ExtraBefore + frame.ExtraAfter > 0);
    }

    [Fact]
    public void Empty_bar_with_pointer_has_no_slots()
    {
        RestLayout rest = RestLayoutBuilder.Build([], Standard);

        FrameLayout frame = FrameLayout.Compute(rest, 50, 1);

        Assert.Empty(frame.Slots);
        Assert.Equal(0, frame.ExtraBefore);
    }
}
