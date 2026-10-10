using MehrBar.Core.Layout;

namespace MehrBar.Tests.Fixtures;

/// <summary>
/// Geometry of the original RocketDock bar, measured from the local reference screenshots
/// <c>samples/reference/rocketdock-zoom-1..5.png</c> (gitignored; third-party icons, never part of the repo).
///
/// Coordinates are the "table" coordinates of docs/PROJEKT_START.md ("Nachtrag zu Q12"): all five screenshots are
/// aligned over the identical wallpaper, and the table frame is image-1 pixel + 89 (the left rim pixel of the rest
/// bar, x = 20 in image 1, is 109 in the table). Every number below was measured with
/// <c>spikes/tools/measure-zoom-fixture.ps1</c>; the source of each value is named in its comment.
///
/// Independence rule: no value in this file is derived from, or adjusted towards, the expected bar edges
/// (<see cref="Rows"/>). The expected edges are only the thing the prediction is compared against.
/// </summary>
internal static class RocketDockReference
{
    /// <summary>Icon size at rest and the unit of the slot pitch: PDF/FileZilla glyphs fill their slot (image 1, rows 3..32, 32 px wide).</summary>
    public const double IconSize = 32;

    /// <summary>
    /// Largest icon size ("121 px under the pointer", docs/PROJEKT_START.md Nachtrag zu Q12). This is a model
    /// parameter, not a bar edge. The visible glyphs of the four cursor icons measure 110..115 px (the art has a
    /// transparent margin); the layout slot of the cursor icon is 121 (see the note 01-SPIKE-zoom-anchor.md).
    /// </summary>
    public const double ZoomSize = 121;

    /// <summary>Left bar edge at rest. Image 1, row 20: brightest rim pixel x = 20, plus the table offset 89.</summary>
    public const double RestStart = 109;

    /// <summary>Right bar edge at rest. Image 1, row 20: first sky pixel x = 609, plus the table offset 89.</summary>
    public const double RestEnd = 698;

    /// <summary>
    /// Slot pitch (one icon). Image 1: the three separator lines sit at x = 234, 442 and 554 (rows 3..33, thin lines
    /// found by the script's separator detection); 6p + q = 442.5 - 234.5 = 208 and 3p + q = 554.5 - 442.5 = 112
    /// give p = 32.00 and q = 16.00 exactly.
    /// </summary>
    public const double Pitch = 32;

    /// <summary>Slot length of a separator. Same two line spacings as <see cref="Pitch"/>: q = 112 - 3 * 32 = 16.</summary>
    public const double SeparatorLength = 16;

    /// <summary>
    /// Distance from the left bar edge to the first slot. Image 1: first slot starts at line1 - q/2 - 6p = 234.5 - 8 - 192
    /// = 34.5, bar edge 20, so 14.5.
    /// </summary>
    public const double LeadingMargin = 14.5;

    /// <summary>Distance from the last slot to the right bar edge. Image 1: last slot ends at 554.5 + 8 + 32 = 594.5, bar edge 609, so 14.5.</summary>
    public const double TrailingMargin = 14.5;

    /// <summary>Icons per group, left to right (image 1, separator lines split them 6 / 6 / 3 / 1).</summary>
    public static readonly int[] GroupSizes = { 6, 6, 3, 1 };

    /// <summary>Item index of each separator in <see cref="BuildRestItems"/> (after 6, 12 and 15 icons).</summary>
    public static readonly int[] SeparatorIndices = { 6, 13, 17 };

    /// <summary>Number of items: 16 icons and 3 separators.</summary>
    public const int ItemCount = 19;

    /// <summary>
    /// One reference row of the Nachtrag table. <see cref="PointerItemIndex"/> is the icon under the pointer, identified
    /// visually in images 2..5 (middle icon P-touch, 2nd icon Visual Studio Code, Publisher, Excel).
    /// <see cref="ExpectedBefore"/> / <see cref="ExpectedAfter"/> are the table's edge values (the thing predicted);
    /// <see cref="BeforeCropped"/> marks the three identical left edges of -3, which the plan treats as a bound.
    /// </summary>
    internal sealed record Row(string Name, int PointerItemIndex, double ExpectedBefore, double ExpectedAfter, bool BeforeCropped);

    /// <summary>The four zoomed rows of the Nachtrag table (docs/PROJEKT_START.md, "Maus mittig / links / rechts / ganz rechts").</summary>
    public static readonly Row[] Rows =
    {
        new("Maus mittig (P-touch)", 9, -3, 812, true),
        new("Maus links (2. Symbol)", 1, 27, 812, false),
        new("Maus rechts (Publisher)", 16, -3, 804, true),
        new("Maus ganz rechts (Excel)", 18, -3, 733, true),
    };

    /// <summary>
    /// The rest layout in table coordinates: 6 icons, separator, 6 icons, separator, 3 icons, separator, 1 icon.
    /// <paramref name="separatorsZoom"/> decides whether the separator slots scale with the zoom factor
    /// (what image 4 and 5 show, see the note) or keep their rest length (the plan's literal hypothesis).
    /// </summary>
    public static IReadOnlyList<RestItem> BuildRestItems(bool separatorsZoom = true)
    {
        var items = new List<RestItem>(ItemCount);
        double x = RestStart + LeadingMargin;
        for (int g = 0; g < GroupSizes.Length; g++)
        {
            for (int i = 0; i < GroupSizes[g]; i++)
            {
                items.Add(new RestItem(x, Pitch, Zooms: true));
                x += Pitch;
            }

            if (g < GroupSizes.Length - 1)
            {
                items.Add(new RestItem(x, SeparatorLength, separatorsZoom));
                x += SeparatorLength;
            }
        }

        return items;
    }
}
