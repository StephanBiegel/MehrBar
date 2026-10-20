using System.Text.Json;
using MehrBar.Core.Config;
using MehrBar.Core.Layout;
using MehrBar.Core.Model;
using MehrBar.Tests.Fixtures;

namespace MehrBar.Tests.Config;

/// <summary>
/// CONF-02: a hand-written config.json with future types and unknown fields survives load and save with the same
/// content; typos in enum values or a single broken item do not cost the whole file.
/// </summary>
public class ConfigSerializerTests
{
    [Fact]
    public void Group_item_round_trips_unchanged()
    {
        JsonElement original = FirstBarItems(ConfigFixtures.Future)[1];

        JsonElement written = FirstBarItems(RoundTrip(ConfigFixtures.Future))[1];

        Assert.True(JsonElement.DeepEquals(original, written), written.GetRawText());
    }

    [Fact]
    public void Unknown_fields_survive_on_every_level()
    {
        using JsonDocument doc = JsonDocument.Parse(RoundTrip(ConfigFixtures.Future));
        JsonElement root = doc.RootElement;
        JsonElement bar = root.GetProperty("bars")[0];

        Assert.Equal(1, root.GetProperty("rootExtra").GetProperty("a").GetInt32());
        Assert.Equal("x", root.GetProperty("general").GetProperty("generalExtra").GetString());
        Assert.Equal(3, bar.GetProperty("barExtra").GetArrayLength());
        Assert.True(bar.GetProperty("label").GetProperty("labelExtra").GetBoolean());
        Assert.Equal(7, bar.GetProperty("items")[0].GetProperty("itemExtra").GetInt32());
    }

    [Fact]
    public void Type_as_last_property_reads_an_app_item()
    {
        DockConfig config = ConfigSerializer.Deserialize(ConfigFixtures.Future, new List<string>());

        var app = Assert.IsType<AppItem>(config.Bars[0].Items[0]);
        Assert.Equal("Firefox", app.Name);
        Assert.Equal(@"C:\Program Files\Mozilla Firefox\firefox.exe", app.Path);
    }

    [Fact]
    public void Type_is_written_first_and_only_once()
    {
        JsonElement[] items = FirstBarItems(RoundTrip(ConfigFixtures.Future));

        foreach (JsonElement item in items.Where(i => i.GetProperty("type").GetString() != "group"))
        {
            JsonProperty[] properties = item.EnumerateObject().ToArray();
            Assert.Equal("type", properties[0].Name);
            Assert.Single(properties, p => p.Name == "type");
        }
    }

    [Fact]
    public void Enums_are_written_in_lower_case()
    {
        string json = ConfigSerializer.Serialize(Defaults.CreateConfig());

        Assert.Contains("\"edge\": \"top\"", json);
        Assert.Contains("\"align\": \"center\"", json);
        Assert.Contains("\"zoomAnchor\": \"cursor\"", json);
        Assert.Contains("\"layer\": \"bottom\"", json);
    }

    [Fact]
    public void Unknown_enum_value_falls_back_and_warns()
    {
        var warnings = new List<string>();

        DockConfig config = ConfigSerializer.Deserialize(ConfigFixtures.BadEnum, warnings);

        Assert.Equal(BarEdge.Top, config.Bars[0].Edge);
        string warning = Assert.Single(warnings);
        Assert.Contains("diagonal", warning);
    }

    [Fact]
    public void Item_with_wrong_value_kind_is_kept_raw()
    {
        var warnings = new List<string>();

        DockConfig config = ConfigSerializer.Deserialize(ConfigFixtures.BadItemValue, warnings);
        string written = ConfigSerializer.Serialize(config);

        var broken = Assert.IsType<UnknownItem>(config.Bars[0].Items[0]);
        Assert.Equal("app", broken.TypeName);
        Assert.IsType<AppItem>(config.Bars[0].Items[1]);
        Assert.Single(warnings);
        Assert.Equal(42, FirstBarItems(written)[0].GetProperty("path").GetInt32());
    }

    [Fact]
    public void Wrong_root_value_throws()
    {
        Assert.Throws<JsonException>(() => ConfigSerializer.Deserialize(ConfigFixtures.BadRoot, new List<string>()));
    }

    [Fact]
    public void Every_model_type_serialises_and_reads()
    {
        using JsonDocument raw = JsonDocument.Parse("""{ "type": "group", "items": [] }""");
        DockConfig config = Defaults.CreateConfig() with
        {
            Bars =
            [
                Defaults.CreateBar("b1") with
                {
                    Items =
                    [
                        new AppItem { Name = "A", Path = @"C:\a.exe" },
                        new FolderItem { Name = "F", Path = @"C:\f" },
                        new UrlItem { Name = "U", Url = "https://example.org" },
                        new SeparatorItem(),
                        new UnknownItem("group", raw.RootElement.Clone()),
                    ],
                },
            ],
        };

        DockConfig read = ConfigSerializer.Deserialize(ConfigSerializer.Serialize(config), new List<string>());

        IReadOnlyList<DockItem> items = read.Bars[0].Items;
        Assert.IsType<AppItem>(items[0]);
        Assert.IsType<FolderItem>(items[1]);
        Assert.IsType<UrlItem>(items[2]);
        Assert.IsType<SeparatorItem>(items[3]);
        Assert.IsType<UnknownItem>(items[4]);
        Assert.Equal("Tahoma", read.Bars[0].Label.FontFamily);
        Assert.Equal("Skins", read.General.SkinFolder);
    }

    [Fact]
    public void Comments_and_trailing_commas_are_accepted()
    {
        DockConfig config = ConfigSerializer.Deserialize(ConfigFixtures.WithComments, new List<string>());

        Assert.Equal("b1", config.Bars[0].Id);
    }

    [Fact]
    public void Environment_variables_stay_unexpanded()
    {
        JsonElement folder = FirstBarItems(RoundTrip(ConfigFixtures.Future))[2];

        Assert.Equal(@"%USERPROFILE%\Downloads", folder.GetProperty("path").GetString());
    }

    private static string RoundTrip(string json) =>
        ConfigSerializer.Serialize(ConfigSerializer.Deserialize(json, new List<string>()));

    private static JsonElement[] FirstBarItems(string json)
    {
        using JsonDocument doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("bars")[0].GetProperty("items").EnumerateArray().Select(e => e.Clone()).ToArray();
    }
}
