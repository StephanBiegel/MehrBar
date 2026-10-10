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
    /// Zoom radius in slot pitches, one value for all rows. Measured from the four neighbours of the cursor icon in the
    /// "Maus mittig" screenshot (image 2) only, never from a bar edge: size = 32 * zoomed glyph width / rest glyph width,
    /// f = (size - 32) / (121 - 32), r = d * pi / acos(2f - 1) with d the neighbour's rest distance from the pointer
    /// (see <see cref="RadiusSamples"/>). Per neighbour: ring 2.72, PDF 2.72, cube 2.41, "a" 2.46; mean 2.577, spread
    /// 2.41 .. 2.72 (the direct neighbours give a larger r than the next-but-one icons: the original's falloff is not a
    /// perfect cosine). Glyph widths are +-1 px, which is about +-0.05 (direct) and +-0.1 (next-but-one) in r.
    /// </summary>
    public const double MeasuredRadius = 2.58;

    /// <summary>One neighbour of the cursor icon in image 2 ("Maus mittig") used to measure the radius.</summary>
    /// <param name="Name">Icon.</param>
    /// <param name="ItemIndex">Item index in <see cref="BuildRestItems"/>.</param>
    /// <param name="RestWidth">Glyph width at rest (image 1, icon pixels inside the slot, rows 3..32).</param>
    /// <param name="ZoomedWidth">Glyph width in image 2 (sky rows below the bar for ring and PDF, bar rows for cube and "a").</param>
    internal sealed record RadiusSample(string Name, int ItemIndex, double RestWidth, double ZoomedWidth);

    /// <summary>The four neighbours behind the radius constant (image 1 / image 2 glyph widths, +-1 px).</summary>
    public static readonly RadiusSample[] RadiusSamples =
    {
        new("Cube (next-but-one, left)", 7, 29, 35),    // image 1 x 245..273; image 2 x 248..282
        new("Ring (neighbour, left)", 8, 31, 92),       // image 1 x 275..305; image 2 x 285..376
        new("PDF (neighbour, right)", 10, 32, 94),      // image 1 x 338..369; image 2 x 496..589
        new("a (next-but-one, right)", 11, 32, 39),     // image 1 x 370..401; image 2 x 591..629
    };

    /// <summary>Separator line centres at rest (image 1 columns 234, 442, 554, +0.5 for the pixel centre, +89 for the table frame).</summary>
    public static readonly double[] RestSeparatorCentres = { 323.5, 531.5, 643.5 };

    /// <summary>
    /// One reference row of the Nachtrag table. <see cref="PointerItemIndex"/> is the icon under the pointer, identified
    /// visually in images 2..5 (middle icon P-touch, 2nd icon Visual Studio Code, Publisher, Excel).
    /// <see cref="PointerFraction"/> is where the pointer sits inside that icon's rest slot (0 = near edge), measured
    /// from the cursor icon's own displacement only: the cursor icon keeps the pointer point fixed, so its centre moves
    /// by (0.5 - f) * (121 - 32) against its rest centre. Uncertainty +-2 px, i.e. +-0.02 in f.
    /// <see cref="ExpectedBefore"/> / <see cref="ExpectedAfter"/> are the table's edge values (the thing predicted);
    /// <see cref="BeforeCropped"/> marks the three identical left edges of -3, which the plan treats as a bound.
    /// <see cref="SeparatorCentres"/> are the displayed centres of the three separator lines, measured as thin vertical
    /// lines in the bar rows of the screenshot (frame: image px - shift to image 1 + 89); they do not depend on the
    /// bar edges and give a second, independent check of the layout.
    /// </summary>
    internal sealed record Row(
        string Name,
        int PointerItemIndex,
        double PointerFraction,
        double ExpectedBefore,
        double ExpectedAfter,
        bool BeforeCropped,
        double[] SeparatorCentres);

    /// <summary>The four zoomed rows of the Nachtrag table (docs/PROJEKT_START.md, "Maus mittig / links / rechts / ganz rechts").</summary>
    public static readonly Row[] Rows =
    {
        // Image 2, shift to image 1: dx 114. P-touch glyph centre 323.0 (x 381..492 minus 114 = 267..378 -> 323; rest x 306..337 -> 322.0):
        // shift +1.0 px -> f = 0.5 - 1.0 / 89 = 0.489. Separator lines at image-2 columns 236, 670, 782.
        new("Maus mittig (P-touch)", 9, 0.489, -3, 812, true, new[] { 211.5, 645.5, 757.5 }),

        // Image 3, dx 90. Visual Studio Code glyph centre 120.5 (x 154..266 minus 90; rest x 66..97 -> 82.0): shift +38.5 px
        // -> f = 0.5 - 38.5 / 89 = 0.067. Separator lines at image-3 columns 438, 646, 758.
        new("Maus links (2. Symbol)", 1, 0.067, 27, 812, false, new[] { 437.5, 645.5, 757.5 }),

        // Image 4, dx 114. Publisher glyph centre 492.0 (x 551..660 minus 114; rest x 515..545 -> 530.5): shift -38.5 px
        // -> f = 0.5 + 38.5 / 89 = 0.933. Separator lines at image-4 columns 236, 444, 692.
        new("Maus rechts (Publisher)", 16, 0.933, -3, 804, true, new[] { 211.5, 419.5, 667.5 }),

        // Image 5, dx 117. Excel glyph centre 563.5 (x 629..731 minus 117; rest x 563..591 -> 577.5): shift -14.0 px
        // -> f = 0.5 + 14.0 / 89 = 0.657. Separator lines at image-5 columns 239, 447, 602.
        new("Maus ganz rechts (Excel)", 18, 0.657, -3, 733, true, new[] { 211.5, 419.5, 574.5 }),
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
