using MehrBar.Core.Layout;
using MehrBar.Core.Model;

namespace MehrBar.Tests.Layout;

/// <summary>A click hits the entry by its index in the whole list, the bar, or nothing.</summary>
public class HitTestTests
{
    private static readonly BarMetrics Standard = new(32, 121, 2.58, 0, 6, 3);

    [Fact]
    public void Hidden_types_do_not_shift_the_hit()
    {
        DockItem[] items =
        [
            new AppItem { Path = @"C:\a.exe" },
            new UrlItem { Url = "https://example.org" },
            new SeparatorItem(),
            new FolderItem { Path = @"C:\f" },
        ];
        RestLayout rest = RestLayoutBuilder.Build(items, Standard);

        HitResult hit = HitTest.At(FrameLayout.Compute(rest, null, 0), rest, 6 + 32 + 16, 20);

        Assert.Equal(new HitResult(HitKind.Item, 3), hit);
    }

    [Fact]
    public void Inner_margin_hits_the_bar()
    {
        RestLayout rest = RestLayoutBuilder.Build(RestLayoutTests.Apps(3), Standard);

        Assert.Equal(HitKind.Bar, HitTest.At(FrameLayout.Compute(rest, null, 0), rest, 2, 20).Kind);
    }

    [Fact]
    public void Point_outside_the_bar_hits_nothing()
    {
        RestLayout rest = RestLayoutBuilder.Build(RestLayoutTests.Apps(3), Standard);
        FrameLayout frame = FrameLayout.Compute(rest, null, 0);

        Assert.Equal(HitKind.None, HitTest.At(frame, rest, -5, 20).Kind);
        Assert.Equal(HitKind.None, HitTest.At(frame, rest, 50, 50).Kind);
    }

    [Fact]
    public void Slot_boundary_belongs_to_the_next_slot()
    {
        RestLayout rest = RestLayoutBuilder.Build(RestLayoutTests.Apps(3), Standard);

        HitResult hit = HitTest.At(FrameLayout.Compute(rest, null, 0), rest, 38, 20);

        Assert.Equal(new HitResult(HitKind.Item, 1), hit);
    }

    [Fact]
    public void Empty_bar_inside_hits_the_bar()
    {
        RestLayout rest = RestLayoutBuilder.Build([], Standard);

        Assert.Equal(HitKind.Bar, HitTest.At(FrameLayout.Compute(rest, null, 0), rest, 50, 20).Kind);
    }
}
