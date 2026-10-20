using MehrBar.Core.Config;
using MehrBar.Core.Layout;
using MehrBar.Core.Model;

namespace MehrBar.Tests.Config;

/// <summary>LEIS-01, D-09, D-14: the defaults of a new bar come from one place and equal Q2.</summary>
public class DefaultsTests
{
    [Fact]
    public void First_start_config_has_one_empty_bar_with_the_q2_values()
    {
        DockConfig config = Defaults.CreateConfig();

        Assert.Equal(1, config.Version);
        Assert.False(config.General.StartWithWindows);
        Assert.Equal("Skins", config.General.SkinFolder);
        BarConfig bar = Assert.Single(config.Bars);
        Assert.Equal("b1", bar.Id);
        Assert.Equal("Oben", bar.Name);
        Assert.Equal("primary", bar.Monitor);
        Assert.Equal(BarEdge.Top, bar.Edge);
        Assert.Equal(BarAlign.Center, bar.Align);
        Assert.Equal(0, bar.Offset);
        Assert.Equal(-5, bar.EdgeOffset);
        Assert.Equal(BarLayer.Bottom, bar.Layer);
        Assert.Equal(32, bar.IconSize);
        Assert.Equal(121, bar.ZoomSize);
        Assert.Equal(4, bar.ZoomWidth);
        Assert.Equal(200, bar.ZoomDurationMs);
        Assert.Equal(ZoomAnchorMode.Cursor, bar.ZoomAnchor);
        Assert.Equal(0, bar.Spacing);
        Assert.True(bar.Label.Show);
        Assert.Equal("Tahoma", bar.Label.FontFamily);
        Assert.Equal(13, bar.Label.FontSize);
        Assert.Equal("CrystalXP.net", bar.Skin);
        Assert.Empty(bar.Items);
    }

    [Fact]
    public void Initialisers_equal_the_defaults()
    {
        // Compared as JSON: the records hold list and dictionary references that do not compare by value.
        Assert.Equal(Json(new BarConfig()), Json(Defaults.CreateBar("b1")));
    }

    [Fact]
    public void Serialised_defaults_contain_the_key_values()
    {
        string json = ConfigSerializer.Serialize(Defaults.CreateConfig());

        Assert.Contains("\"edgeOffset\": -5", json);
        Assert.Contains("\"iconSize\": 32", json);
        Assert.Contains("\"spacing\": 0", json);
        Assert.Contains("\"edge\": \"top\"", json);
        Assert.Contains("\"align\": \"center\"", json);
    }

    private static string Json(BarConfig bar) =>
        ConfigSerializer.Serialize(Defaults.CreateConfig() with { Bars = [bar] });
}
