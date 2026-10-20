using System.Text.Json;
using MehrBar.Core.Layout;
using MehrBar.Core.Model;

namespace MehrBar.Tests.Layout;

/// <summary>D-08, D-09: the bar at rest; only app and folder entries become slots; axis mapping for all edges.</summary>
public class RestLayoutTests
{
    private static readonly BarMetrics Standard = new(32, 121, 2.58, 0, 6, 3);

    [Fact]
    public void Empty_bar_is_three_slots_long()
    {
        RestLayout rest = RestLayoutBuilder.Build([], Standard);

        Assert.Empty(rest.Slots);
        Assert.Equal(108, rest.Length);
        Assert.Equal(44, rest.Depth);
    }

    [Fact]
    public void Five_apps_follow_each_other_at_one_pitch()
    {
        RestLayout rest = RestLayoutBuilder.Build(Apps(5), Standard);

        Assert.Equal(172, rest.Length);
        Assert.Equal([6.0, 38, 70, 102, 134], rest.Slots.Select(s => s.Start));
        Assert.All(rest.Slots, s => Assert.Equal(32, s.Length));
    }

    [Fact]
    public void Only_app_and_folder_entries_become_slots()
    {
        using JsonDocument raw = JsonDocument.Parse("{}");
        DockItem[] items =
        [
            new AppItem { Path = @"C:\a.exe" },
            new UrlItem { Url = "https://example.org" },
            new SeparatorItem(),
            new FolderItem { Path = @"C:\f" },
            new UnknownItem("group", raw.RootElement.Clone()),
        ];

        RestLayout rest = RestLayoutBuilder.Build(items, Standard);

        Assert.Equal([0, 3], rest.Slots.Select(s => s.ItemIndex));
    }

    [Fact]
    public void Spacing_widens_the_pitch_and_the_minimum_still_applies()
    {
        RestLayout rest = RestLayoutBuilder.Build(Apps(2), Standard with { Spacing = 4 });

        Assert.Equal(36, rest.Slots[0].Length);
        Assert.Equal((2 * 6) + (3 * 36), rest.Length);
    }

    [Theory]
    [InlineData(BarEdge.Top)]
    [InlineData(BarEdge.Bottom)]
    [InlineData(BarEdge.Left)]
    [InlineData(BarEdge.Right)]
    public void Axis_frame_maps_there_and_back(BarEdge edge)
    {
        var frame = new AxisFrame(edge);

        (double u, double v) = frame.ToAxis(30, 10, 200, 100);
        (double x, double y) = frame.FromAxis(u, v, 200, 100);

        Assert.Equal((30.0, 10.0), (x, y));
        double expectedV = edge switch { BarEdge.Top => 10, BarEdge.Bottom => 90, BarEdge.Left => 30, _ => 170 };
        Assert.Equal(expectedV, v);
    }

    internal static DockItem[] Apps(int n) =>
        Enumerable.Range(0, n).Select(i => (DockItem)new AppItem { Path = $@"C:\app{i}.exe" }).ToArray();
}
