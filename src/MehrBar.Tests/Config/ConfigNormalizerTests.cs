using MehrBar.Core.Config;
using MehrBar.Core.Model;

namespace MehrBar.Tests.Config;

/// <summary>Every loaded configuration is brought into valid limits and reports what was corrected.</summary>
public class ConfigNormalizerTests
{
    [Fact]
    public void Zoom_size_below_icon_size_is_clamped()
    {
        var warnings = new List<string>();
        DockConfig config = WithBar(Defaults.CreateBar("b1") with { IconSize = 48, ZoomSize = 20 });

        DockConfig result = ConfigNormalizer.Normalize(config, warnings);

        Assert.Equal(48, result.Bars[0].ZoomSize);
        Assert.Contains(warnings, w => w.StartsWith("config-clamped", StringComparison.Ordinal) && w.Contains("zoomSize"));
    }

    [Fact]
    public void Empty_bar_list_gives_one_default_bar()
    {
        var warnings = new List<string>();

        DockConfig result = ConfigNormalizer.Normalize(new DockConfig { Bars = [] }, warnings);

        BarConfig bar = Assert.Single(result.Bars);
        Assert.Equal("b1", bar.Id);
        Assert.NotEmpty(warnings);
    }

    [Fact]
    public void Duplicate_or_empty_ids_become_unique()
    {
        var warnings = new List<string>();
        var config = new DockConfig
        {
            Bars = [Defaults.CreateBar("b1"), Defaults.CreateBar("b1"), Defaults.CreateBar(string.Empty)],
        };

        DockConfig result = ConfigNormalizer.Normalize(config, warnings);

        Assert.Equal(["b1", "b2", "b3"], result.Bars.Select(b => b.Id));
        Assert.Equal(2, warnings.Count(w => w.Contains("field=id")));
    }

    [Fact]
    public void Null_items_become_empty_and_null_entries_are_removed()
    {
        var warnings = new List<string>();
        DockConfig config = new()
        {
            Bars =
            [
                Defaults.CreateBar("b1") with { Items = null! },
                Defaults.CreateBar("b2") with { Items = [new AppItem { Path = @"C:\a.exe" }, null!] },
            ],
        };

        DockConfig result = ConfigNormalizer.Normalize(config, warnings);

        Assert.Empty(result.Bars[0].Items);
        Assert.Single(result.Bars[1].Items);
        Assert.Equal(2, warnings.Count(w => w.Contains("field=items")));
    }

    [Fact]
    public void Non_finite_offset_becomes_zero_and_version_zero_becomes_one()
    {
        var warnings = new List<string>();
        DockConfig config = WithBar(Defaults.CreateBar("b1") with { Offset = double.NaN }) with { Version = 0 };

        DockConfig result = ConfigNormalizer.Normalize(config, warnings);

        Assert.Equal(0, result.Bars[0].Offset);
        Assert.Equal(1, result.Version);
        Assert.Contains(warnings, w => w.Contains("field=offset"));
        Assert.Contains(warnings, w => w.Contains("field=version"));
    }

    private static DockConfig WithBar(BarConfig bar) => Defaults.CreateConfig() with { Bars = [bar] };
}
