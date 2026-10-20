using MehrBar.Core.Layout;
using MehrBar.Core.Model;

namespace MehrBar.Core.Config;

/// <summary>
/// The only source of default values: those of a new bar from Q2 (concept section 2), icon size 32 (D-09), spacing 0
/// like the RocketDock calibration (D-14), autostart off until phase 6.
/// </summary>
public static class Defaults
{
    public const int Version = 1;
    public const bool StartWithWindows = false;
    public const string SkinFolder = "Skins";

    public const string FirstBarId = "b1";
    public const string BarName = "Oben";
    public const string Monitor = "primary";
    public const BarEdge Edge = BarEdge.Top;
    public const BarAlign Align = BarAlign.Center;
    public const double Offset = 0;
    public const double EdgeOffset = -5;
    public const BarLayer Layer = BarLayer.Bottom;
    public const int IconSize = 32;
    public const int ZoomSize = 121;
    public const double ZoomWidth = 4;
    public const int ZoomDurationMs = 200;
    public const ZoomAnchorMode ZoomAnchor = ZoomAnchorMode.Cursor;
    public const int Spacing = 0;
    public const string Skin = "CrystalXP.net";

    public const bool LabelShow = true;
    public const string LabelFontFamily = "Tahoma";
    public const double LabelFontSize = 13;

    /// <summary>Configuration of the first start: one empty bar at the top centre (Q4).</summary>
    public static DockConfig CreateConfig() => new()
    {
        Version = Version,
        General = new GeneralConfig(),
        Bars = [CreateBar(FirstBarId)],
    };

    /// <summary>A new bar with all default values.</summary>
    public static BarConfig CreateBar(string id) => new() { Id = id };
}
