using System.Text.Json;
using System.Text.Json.Serialization;
using MehrBar.Core.Config;
using MehrBar.Core.Layout;

namespace MehrBar.Core.Model;

/// <summary>Window layer of a bar; V1 only knows the bottom layer (Q10).</summary>
public enum BarLayer
{
    Bottom,
}

/// <summary>
/// Root of <c>%APPDATA%\MehrBar\config.json</c> (concept section 5). Every record keeps unknown properties in
/// <c>Extra</c>, which must be settable for the source generator (RESEARCH Pattern 1).
/// </summary>
public sealed record DockConfig
{
    public int Version { get; init; } = Defaults.Version;

    public GeneralConfig General { get; init; } = new();

    public IReadOnlyList<BarConfig> Bars { get; init; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>Settings that apply to the whole app.</summary>
public sealed record GeneralConfig
{
    public bool StartWithWindows { get; init; } = Defaults.StartWithWindows;

    public string SkinFolder { get; init; } = Defaults.SkinFolder;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>One bar. The initialisers equal <see cref="Defaults.CreateBar"/> apart from the id.</summary>
public sealed record BarConfig
{
    public string Id { get; init; } = Defaults.FirstBarId;

    public string Name { get; init; } = Defaults.BarName;

    public string Monitor { get; init; } = Defaults.Monitor;

    public BarEdge Edge { get; init; } = Defaults.Edge;

    public BarAlign Align { get; init; } = Defaults.Align;

    public double Offset { get; init; } = Defaults.Offset;

    public double EdgeOffset { get; init; } = Defaults.EdgeOffset;

    public BarLayer Layer { get; init; } = Defaults.Layer;

    public int IconSize { get; init; } = Defaults.IconSize;

    public int ZoomSize { get; init; } = Defaults.ZoomSize;

    public double ZoomWidth { get; init; } = Defaults.ZoomWidth;

    public int ZoomDurationMs { get; init; } = Defaults.ZoomDurationMs;

    public ZoomAnchorMode ZoomAnchor { get; init; } = Defaults.ZoomAnchor;

    public int Spacing { get; init; } = Defaults.Spacing;

    public LabelConfig Label { get; init; } = new();

    public string Skin { get; init; } = Defaults.Skin;

    public IReadOnlyList<DockItem> Items { get; init; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>Label under the zoomed icon (drawn from phase 3 on).</summary>
public sealed record LabelConfig
{
    public bool Show { get; init; } = Defaults.LabelShow;

    public string FontFamily { get; init; } = Defaults.LabelFontFamily;

    public double FontSize { get; init; } = Defaults.LabelFontSize;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}
